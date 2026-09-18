import { NextResponse } from "next/server";
import { ApiError, SessionExpiredError, apiStream } from "@/lib/api/client";

/**
 * Plays an interview recording for the staff member watching it.
 *
 * Exists for the reason the certification document route does: the API needs the admin's
 * bearer token, which lives in an httpOnly cookie the browser cannot read, so a plain
 * `<video src>` pointing at the API would arrive unauthenticated. The request is made
 * server-side with the session and the bytes are piped through.
 *
 * The API is still the authority: it authorises against AdminOnly and audits the view
 * against the staff member before the first byte is sent. Nothing is cached — a response
 * holding video of a family's living room must not sit in a proxy or the browser's disk
 * cache after the tab is closed.
 */
export async function GET(
  request: Request,
  { params }: { params: Promise<{ recordingId: string }> },
): Promise<Response> {
  const { recordingId } = await params;

  try {
    const upstream = await apiStream(`/api/v1/admin/interviews/recordings/${recordingId}`);

    return new NextResponse(upstream.body, {
      headers: {
        "Content-Type": upstream.headers.get("content-type") ?? "video/mp4",
        "Content-Disposition": "inline",
        "Cache-Control": "no-store, private",
        "X-Content-Type-Options": "nosniff",
      },
    });
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      return NextResponse.redirect(new URL("/login", request.url));
    }

    if (error instanceof ApiError) {
      return new NextResponse(null, { status: error.status });
    }

    throw error;
  }
}
