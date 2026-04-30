import type {
  ReplayEvent,
  ReplayRunDetail,
  ReplayRunSummary,
  ReplaySuggestion,
} from "./types";

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL ?? "";

async function request<T>(path: string): Promise<T> {
  const url = `${API_BASE_URL}${path}`;
  let response: Response;

  try {
    response = await fetch(url);
  } catch (error) {
    const detail =
      error instanceof Error ? error.message : "Unknown network failure";
    throw new Error(
      `Failed to reach replay API at ${url}. Check CORS, HTTPS certificate trust, and API base URL. ${detail}`,
    );
  }

  if (!response.ok) {
    const message = await response.text();
    throw new Error(message || `Request failed: ${response.status}`);
  }

  return (await response.json()) as T;
}

export const replayApi = {
  listRuns: (take = 30) =>
    request<ReplayRunSummary[]>(`/api/replay/runs?take=${take}`),
  getRunDetail: (runId: string) =>
    request<ReplayRunDetail>(`/api/replay/runs/${runId}`),
  getRunEvents: (runId: string) =>
    request<ReplayEvent[]>(`/api/replay/runs/${runId}/events`),
  listSuggestions: (take = 30) =>
    request<ReplaySuggestion[]>(`/api/replay/suggestions?take=${take}`),
};
