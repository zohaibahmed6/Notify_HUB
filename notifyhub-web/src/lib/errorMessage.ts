import { ApiError } from "./apiClient";

/** Extracts a display-ready message from an ApiError (RFC 7807 ProblemDetails "title"), falling back for statuses with no body. */
export function errorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    const problem = error.problem as { title?: string } | null;
    if (problem?.title) return problem.title;
    if (error.status === 409) return "Already assigned";
    if (error.status === 403) return "Not allowed";
  }
  return fallback;
}
