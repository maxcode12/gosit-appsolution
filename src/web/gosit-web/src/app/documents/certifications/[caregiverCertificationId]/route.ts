import { NextResponse } from "next/server";
import { ApiError, SessionExpiredError, apiStream } from "@/lib/api/client";

/**
 * Hands a certification document to the staff member reading it.
 *
 * This exists because the API endpoint needs the admin's bearer token, and that token
 * lives in an httpOnly cookie the browser cannot read — so a plain link from the page
 * would arrive unauthenticated. The request is made server-side with the session, and the
 * bytes are piped through.
 *
 * The API is still the authority. This route adds no access of its own: it forwards a
 * request that the API authorises against the AdminOnly policy and audits against the
 * staff member, and a non-admin gets the same 403 they would get calling it directly.
 *
 * Nothing is cached. A response holding a vulnerable-sector check must not sit in a CDN,
 * a proxy, or the browser's disk cache after the tab is closed.
 */
export async function GET(
  _request: Request,
  { params }: { params: Promise<{ caregiverCertificationId: string }> },
): Promise<Response> {
  const { caregiverCertificationId } = await params;

  try {
    const upstream = await apiStream(
      `/api/v1/admin/certifications/${caregiverCertificationId}/document`,
    );

    return new NextResponse(upstream.body, {
      headers: {
        "Content-Type": upstream.headers.get("content-type") ?? "application/octet-stream",
        // Preserved from the API, which sets `attachment` so the file is never rendered
        // in the origin an admin is signed in to.
        "Content-Disposition":
          upstream.headers.get("content-disposition") ?? "attachment",
        "Cache-Control": "no-store, private",
      },
    });
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      return NextResponse.redirect(new URL("/login", _request.url));
    }

    if (error instanceof ApiError) {
      // The API's status, unchanged. A 403 here means a non-admin asked, and turning it
      // into a 404 would only make the failure harder to explain.
      return new NextResponse(null, { status: error.status });
    }

    throw error;
  }
}
