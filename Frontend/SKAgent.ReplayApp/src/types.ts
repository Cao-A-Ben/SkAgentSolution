export type ReplayEvent = {
  runId: string;
  seq: number;
  timestamp: string;
  type: string;
  payload: unknown;
};

export type ReplayRunSummary = {
  runId: string;
  kind: string;
  conversationId?: string | null;
  personaName?: string | null;
  status: string;
  startedAt?: string | null;
  finishedAt?: string | null;
  goal?: string | null;
  inputPreview?: string | null;
  finalOutputPreview?: string | null;
  eventCount: number;
};

export type ReplayPrompt = {
  target?: string | null;
  hash?: string | null;
  charBudget?: number | null;
  layersUsed: string[];
  systemChars?: number | null;
  userChars?: number | null;
  systemText?: string | null;
  userText?: string | null;
};

export type ReplayStep = {
  order: number;
  kind?: string | null;
  target?: string | null;
  status: string;
  outputPreview?: string | null;
  error?: string | null;
};

export type ReplayMemoryLayer = {
  layer: string;
  countBefore?: number | null;
  countAfter?: number | null;
  budgetChars?: number | null;
  truncateReason?: string | null;
};

export type ReplayMemory = {
  recallSource?: string | null;
  recallPreview?: string | null;
  byRouteCounts: Record<string, number>;
  totalItems?: number | null;
  budgetUsed?: number | null;
  conflictsResolved?: number | null;
  layers: ReplayMemoryLayer[];
  vectorTopK?: number | null;
  vectorLatencyMs?: number | null;
  vectorScoreMin?: number | null;
  vectorScoreMax?: number | null;
};

export type ReplayRunDetail = {
  summary: ReplayRunSummary;
  prompt?: ReplayPrompt | null;
  steps: ReplayStep[];
  memory?: ReplayMemory | null;
};

export type ReplaySuggestion = {
  date: string;
  suggestion: string;
  runId: string;
  conversationId: string;
  personaName: string;
  promptHash: string;
  profileHash: string;
  createdAtUtc: string;
  eventLogPath?: string | null;
  replayAvailable: boolean;
};
