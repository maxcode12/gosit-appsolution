import { NextResponse } from "next/server";
import { ApiError, SessionExpiredError, apiStream } from "@/lib/api/client";

/**
 * Hands a proof-of-insurance document to the staff member reading it. Mirrors
 * `documents/certifications/[id]/route.ts` exactly, for the same reason: the admin's
 * bearer token lives in an httpOnly cookie the browser cannot read, so a plain link to
 * the API would arrive unauthenticated. The API still authorises and audits the read.
 */
export async function GET(
  _request: Request,
  { params }: { params: Promise<{ caregiverInsurancePolicyId: string }> },
): Promise<Response> {
  const { caregiverInsurancePolicyId } = await params;

  try {
    const upstream = await apiStream(
      `/api/v1/admin/insurance/${caregiverInsurancePolicyId}/document`,
    );

    return new NextResponse(upstream.body, {
      headers: {
        "Content-Type": upstream.headers.get("content-type") ?? "application/octet-stream",
        "Content-Disposition": upstream.headers.get("content-disposition") ?? "attachment",
        "Cache-Control": "no-store, private",
      },
    });
  } catch (error) {
    if (error instanceof SessionExpiredError) {
      return NextResponse.redirect(new URL("/login", _request.url));
    }

    if (error instanceof ApiError) {
      return new NextResponse(null, { status: error.status });
    }

    throw error;
  }
}
