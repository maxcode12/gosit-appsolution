import "server-only";

import { env } from "@/lib/env";
import { getAccessToken } from "@/lib/auth/session";

/**
 * Server-side API client.
 *
 * Runs only on the server, so the access token never reaches the browser. Callers get
 * typed results and typed failures rather than a raw Response.
 */

/** The caller is not signed in, or the access token has expired. */
export class SessionExpiredError extends Error {
  constructor() {
    super("The session has expired.");
    this.name = "SessionExpiredError";
  }
}

/** The API rejected the request. `problem` carries the RFC 7807 body when present. */
export class ApiError extends Error {
  constructor(
    readonly status: number,
    readonly problem?: { title?: string; errors?: Record<string, string[]> },
  ) {
    super(problem?.title ?? `The request failed with status ${status}.`);
    this.name = "ApiError";
  }
}

interface RequestOptions {
  method?: "GET" | "POST" | "PUT" | "DELETE";
  body?: unknown;
  /** Set for anonymous endpoints such as the care-service catalogue. */
  anonymous?: boolean;
  /** Passed to fetch. Defaults to no-store, since almost everything here is per-user. */
  cache?: RequestCache;
  revalidate?: number;
}

async function request(path: string, options: RequestOptions = {}): Promise<Response> {
  const { method = "GET", body, anonymous = false, cache = "no-store", revalidate } = options;

  // A file upload passes its body as FormData, already multipart-encoded. It must reach
  // fetch untouched — JSON.stringify would turn it into "[object FormData]", and a
  // hand-set Content-Type would omit the boundary the multipart parser needs, which fetch
  // only supplies when it encodes the body itself.
  const isFormData = typeof FormData !== "undefined" && body instanceof FormData;

  const headers: Record<string, string> = { Accept: "application/json" };

  if (body !== undefined && !isFormData) {
    headers["Content-Type"] = "application/json";
  }

  if (!anonymous) {
    const accessToken = await getAccessToken();

    // No token at all is the same failure as an expired one, from the caller's side.
    if (!accessToken) {
      throw new SessionExpiredError();
    }

    headers.Authorization = `Bearer ${accessToken}`;
  }

  return fetch(`${env.apiBaseUrl}${path}`, {
    method,
    headers,
    body: isFormData ? body : body === undefined ? undefined : JSON.stringify(body),
    ...(revalidate === undefined ? { cache } : { next: { revalidate } }),
  });
}

async function readProblem(response: Response) {
  try {
    return (await response.json()) as { title?: string; errors?: Record<string, string[]> };
  } catch {
    return undefined;
  }
}

/**
 * Performs a request and parses the JSON body.
 *
 * A 401 becomes {@link SessionExpiredError}. This client deliberately does not refresh
 * the token itself: refreshing rotates the refresh token, which means writing cookies,
 * and cookies cannot be written while a Server Component is rendering. Refreshing
 * belongs in a Server Action or Route Handler — see `refreshSession` in
 * features/authentication/actions.ts.
 */
export async function apiFetch<T>(path: string, options: RequestOptions = {}): Promise<T> {
  const response = await request(path, options);

  if (response.status === 401) {
    throw new SessionExpiredError();
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  return (await response.json()) as T;
}

/** For endpoints that return 204 with no body. */
export async function apiSend(path: string, options: RequestOptions = {}): Promise<void> {
  const response = await request(path, options);

  if (response.status === 401) {
    throw new SessionExpiredError();
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }
}

/**
 * Fetches a file, returning the raw response so it can be streamed onward.
 *
 * Deliberately not parsed. Certification documents are streamed straight through to the
 * browser rather than buffered here — a criminal record check has no business sitting in
 * this process's memory any longer than the socket takes.
 */
export async function apiStream(path: string): Promise<Response> {
  const response = await request(path);

  if (response.status === 401) {
    throw new SessionExpiredError();
  }

  if (!response.ok) {
    throw new ApiError(response.status, await readProblem(response));
  }

  return response;
}
