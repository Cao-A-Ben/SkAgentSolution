import type { TranslateFn } from "./i18n";
import type { ReplayEvent, ReplayRunSummary } from "./types";

export const milestoneEventTypes = [
  "persona_selected",
  "model_selected",
  "prompt_composed",
  "recall_summary_built",
  "suggestion_saved",
  "daily_job_finished",
] as const;

export type EventGroup =
  | "all"
  | "milestone"
  | "memory"
  | "model"
  | "step"
  | "daily"
  | "system";

export type EventPriority = "critical" | "high" | "normal" | "quiet";

export type ReplaySignalTone = "acceptance" | "insight" | "risk";

export type ReplaySignal = {
  tone: ReplaySignalTone;
  title: string;
  copy: string;
};

export type ReplayHealthSummary = {
  acceptanceHits: number;
  acceptanceTotal: number;
  acceptanceRatio: number;
  memoryEvents: number;
  modelEvents: number;
  stepEvents: number;
  dailyEvents: number;
  riskEvents: number;
  latestRiskEvent: ReplayEvent | null;
};

export function getRunDisplayTitle(run: ReplayRunSummary) {
  return run.goal ?? run.inputPreview ?? run.runId;
}

export function getRunDisplayCopy(run: ReplayRunSummary, fallbackText: string) {
  return run.finalOutputPreview ?? run.inputPreview ?? fallbackText;
}

export function getEventGroup(type: string): EventGroup {
  if (milestoneEventTypes.includes(type as (typeof milestoneEventTypes)[number])) {
    return "milestone";
  }

  if (type.includes("memory") || type.includes("recall") || type.includes("vector")) {
    return "memory";
  }

  if (type.includes("model") || type.includes("prompt")) {
    return "model";
  }

  if (type.includes("step") || type.includes("plan")) {
    return "step";
  }

  if (type.includes("daily") || type.includes("suggestion")) {
    return "daily";
  }

  return "system";
}

export function matchesEventGroup(event: ReplayEvent, filter: EventGroup) {
  if (filter === "all") {
    return true;
  }

  return getEventGroup(event.type) === filter;
}

export function countEventsByGroup(events: ReplayEvent[]) {
  return events.reduce<Record<EventGroup, number>>(
    (counts, event) => {
      const group = getEventGroup(event.type);
      counts[group] += 1;
      counts.all += 1;
      return counts;
    },
    {
      all: 0,
      milestone: 0,
      memory: 0,
      model: 0,
      step: 0,
      daily: 0,
      system: 0,
    },
  );
}

export function summarizeEvent(event: ReplayEvent, t: TranslateFn) {
  const payload = asRecord(event.payload);

  switch (event.type) {
    case "persona_selected":
      return payload?.personaName
        ? t("event.summary.personaLocked", { personaName: String(payload.personaName) })
        : t("event.summary.personaSelected");
    case "model_selected":
      return payload?.model
        ? t("event.summary.modelResolved", {
            purpose: String(payload.purpose ?? "purpose"),
            model: String(payload.model),
          })
        : t("event.summary.modelSelected");
    case "prompt_composed":
      return payload?.target
        ? t("event.summary.promptBuilt", { target: String(payload.target) })
        : t("event.summary.promptComposed");
    case "suggestion_saved":
      return payload?.date
        ? t("event.summary.suggestionSaved", { date: String(payload.date) })
        : t("event.summary.suggestionStored");
    case "daily_job_finished":
      return payload?.created === false
        ? t("event.summary.dailyFinishedNoCreate")
        : t("event.summary.dailyFinished");
    case "run_completed":
      return payload?.finalOutput
        ? truncate(String(payload.finalOutput), 120)
        : t("event.summary.runCompleted");
    case "step_completed":
      return payload?.target
        ? t("event.summary.stepCompleted", { target: String(payload.target) })
        : t("event.summary.stepDone");
    case "vector_query_executed":
      return payload?.topK
        ? t("event.summary.vectorRecall", {
            topK: String(payload.topK),
            latencyMs: String(payload.latencyMs ?? t("common.na")),
          })
        : t("event.summary.vectorQuery");
    case "memory_fused":
      return payload?.totalItems !== undefined
        ? t("event.summary.memoryFused", { totalItems: String(payload.totalItems) })
        : t("event.summary.memoryMerged");
    default:
      return event.type.split("_").join(" ");
  }
}

export function getEventPriority(event: ReplayEvent): EventPriority {
  const type = event.type.toLowerCase();
  const group = getEventGroup(event.type);

  if (
    type.includes("failed") ||
    type.includes("error") ||
    type.includes("exception") ||
    type.includes("rejected")
  ) {
    return "critical";
  }

  if (group === "milestone") {
    return "high";
  }

  if (group === "model" || group === "memory" || group === "step") {
    return "normal";
  }

  return "quiet";
}

export function summarizePayloadPreview(event: ReplayEvent, t: TranslateFn) {
  const payload = asRecord(event.payload);
  if (!payload) {
    return null;
  }

  const preferredKeys = [
    "purpose",
    "model",
    "personaName",
    "target",
    "date",
    "topK",
    "latencyMs",
    "totalItems",
    "created",
    "status",
  ];

  const parts = preferredKeys
    .filter((key) => payload[key] !== undefined && payload[key] !== null)
    .slice(0, 3)
    .map((key) => `${key}: ${formatPayloadValue(payload[key], t)}`);

  if (parts.length > 0) {
    return parts.join(" • ");
  }

  const fallback = Object.entries(payload)
    .filter(([, value]) => value !== undefined && value !== null)
    .slice(0, 2)
    .map(([key, value]) => `${key}: ${formatPayloadValue(value, t)}`);

  return fallback.length > 0 ? fallback.join(" • ") : null;
}

export function buildReplaySignals(events: ReplayEvent[], t: TranslateFn): ReplaySignal[] {
  const eventTypes = new Set(events.map((event) => event.type));
  const milestoneHits = milestoneEventTypes.filter((type) => eventTypes.has(type));
  const memoryCount = events.filter((event) => getEventGroup(event.type) === "memory").length;
  const modelCount = events.filter((event) => getEventGroup(event.type) === "model").length;
  const stepCount = events.filter((event) => getEventGroup(event.type) === "step").length;
  const failureEvents = events.filter((event) => getEventPriority(event) === "critical").length;
  const terminalEventPresent = events.some(
    (event) => event.type === "run_completed" || event.type === "daily_job_finished",
  );

  const signals: ReplaySignal[] = [];

  if (milestoneHits.length > 0) {
    signals.push({
      tone: "acceptance",
      title: t("signal.acceptance.title"),
      copy: t("signal.acceptance.copy", {
        count: milestoneHits.length,
        total: milestoneEventTypes.length,
      }),
    });
  }

  if (memoryCount > 0) {
    signals.push({
      tone: "insight",
      title: t("signal.insight.memoryTitle"),
      copy: t("signal.insight.memoryCopy", { count: memoryCount }),
    });
  } else if (modelCount > 0) {
    signals.push({
      tone: "insight",
      title: t("signal.insight.modelTitle"),
      copy: t("signal.insight.modelCopy", { count: modelCount }),
    });
  } else if (stepCount > 0) {
    signals.push({
      tone: "insight",
      title: t("signal.insight.stepTitle"),
      copy: t("signal.insight.stepCopy", { count: stepCount }),
    });
  }

  if (failureEvents > 0) {
    signals.push({
      tone: "risk",
      title: t("signal.risk.failureTitle"),
      copy: t("signal.risk.failureCopy", { count: failureEvents }),
    });
  } else if (!terminalEventPresent && events.length > 0) {
    signals.push({
      tone: "risk",
      title: t("signal.risk.terminalTitle"),
      copy: t("signal.risk.terminalCopy"),
    });
  } else if (milestoneHits.length < 2 && events.length > 0) {
    signals.push({
      tone: "risk",
      title: t("signal.risk.coverageTitle"),
      copy: t("signal.risk.coverageCopy", {
        count: milestoneHits.length,
        total: milestoneEventTypes.length,
      }),
    });
  }

  return signals.slice(0, 3);
}

export function buildReplayHealthSummary(events: ReplayEvent[]): ReplayHealthSummary {
  const acceptanceTotal = milestoneEventTypes.length;
  const eventTypes = new Set(events.map((event) => event.type));
  const acceptanceHits = milestoneEventTypes.filter((type) => eventTypes.has(type)).length;
  const memoryEvents = events.filter((event) => getEventGroup(event.type) === "memory").length;
  const modelEvents = events.filter((event) => getEventGroup(event.type) === "model").length;
  const stepEvents = events.filter((event) => getEventGroup(event.type) === "step").length;
  const dailyEvents = events.filter((event) => getEventGroup(event.type) === "daily").length;
  const riskEventsList = events.filter((event) => getEventPriority(event) === "critical");

  return {
    acceptanceHits,
    acceptanceTotal,
    acceptanceRatio: acceptanceHits / acceptanceTotal,
    memoryEvents,
    modelEvents,
    stepEvents,
    dailyEvents,
    riskEvents: riskEventsList.length,
    latestRiskEvent: riskEventsList.at(-1) ?? null,
  };
}

function asRecord(value: unknown) {
  if (!value || typeof value !== "object" || Array.isArray(value)) {
    return null;
  }

  return value as Record<string, unknown>;
}

function truncate(value: string, maxLength: number) {
  return value.length <= maxLength
    ? value
    : `${value.slice(0, maxLength - 1).trimEnd()}…`;
}

function formatPayloadValue(value: unknown, t: TranslateFn) {
  if (typeof value === "string") {
    return truncate(value, 60);
  }

  if (typeof value === "number" || typeof value === "boolean") {
    return String(value);
  }

  if (Array.isArray(value)) {
    return value.length === 0 ? t("common.na") : truncate(value.map(String).join(", "), 60);
  }

  if (value && typeof value === "object") {
    return truncate(JSON.stringify(value), 60);
  }

  return t("common.na");
}
