import { useDeferredValue, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { replayApi } from "../api";
import { useI18n } from "../i18n";
import type { ReplayEvent } from "../types";
import type { ReplayRunSummary } from "../types";
import {
  buildReplayHealthSummary,
  buildReplaySignals,
  getEventGroup,
  getRunDisplayCopy,
  getRunDisplayTitle,
  milestoneEventTypes,
  summarizeEvent,
} from "../replay-utils";

export function RunsPage() {
  const { formatDateTime, t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [runs, setRuns] = useState<ReplayRunSummary[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [searchText, setSearchText] = useState("");
  const [kindFilter, setKindFilter] = useState<"all" | "agent" | "daily">("all");
  const [statusFilter, setStatusFilter] = useState<"all" | "completed" | "running" | "failed">("all");
  const [previewEvents, setPreviewEvents] = useState<ReplayEvent[]>([]);
  const [previewLoading, setPreviewLoading] = useState(false);
  const deferredSearchText = useDeferredValue(searchText);
  const selectedRunId = searchParams.get("selected");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        const response = await replayApi.listRuns(40);
        if (!cancelled) {
          setRuns(response);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : t("runs.error"));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    void load();
    return () => {
      cancelled = true;
    };
  }, [reloadToken]);

  const dailyRuns = runs.filter((run) => run.kind === "daily").length;
  const agentRuns = runs.filter((run) => run.kind === "agent").length;
  const failedRuns = runs.filter((run) => run.status === "failed").length;
  const filteredRuns = useMemo(() => {
    const query = deferredSearchText.trim().toLowerCase();
    return runs.filter((run) => {
      if (kindFilter !== "all" && run.kind !== kindFilter) {
        return false;
      }

      if (statusFilter !== "all" && run.status !== statusFilter) {
        return false;
      }

      if (!query) {
        return true;
      }

      return [
        run.runId,
        run.conversationId,
        run.personaName,
        run.goal,
        run.inputPreview,
        run.finalOutputPreview,
      ]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(query));
    });
  }, [deferredSearchText, kindFilter, runs, statusFilter]);

  useEffect(() => {
    if (filteredRuns.length === 0) {
      if (selectedRunId !== null) {
        setSearchParams({}, { replace: true });
      }
      return;
    }

    if (!selectedRunId || !filteredRuns.some((run) => run.runId === selectedRunId)) {
      setSearchParams({ selected: filteredRuns[0].runId }, { replace: true });
    }
  }, [filteredRuns, selectedRunId, setSearchParams]);

  const selectedRun =
    filteredRuns.find((run) => run.runId === selectedRunId) ?? filteredRuns[0] ?? null;

  useEffect(() => {
    let cancelled = false;

    async function loadPreviewEvents() {
      if (!selectedRun?.runId) {
        setPreviewEvents([]);
        return;
      }

      try {
        setPreviewLoading(true);
        const response = await replayApi.getRunEvents(selectedRun.runId);
        if (!cancelled) {
          setPreviewEvents(response);
        }
      } catch {
        if (!cancelled) {
          setPreviewEvents([]);
        }
      } finally {
        if (!cancelled) {
          setPreviewLoading(false);
        }
      }
    }

    void loadPreviewEvents();
    return () => {
      cancelled = true;
    };
  }, [selectedRun?.runId]);

  const previewHighlights = useMemo(() => {
    return previewEvents
      .filter((event) => {
        const group = getEventGroup(event.type);
        return group === "milestone" || group === "model" || group === "memory";
      })
      .slice(-4)
      .reverse();
  }, [previewEvents]);
  const previewSignals = useMemo(
    () => buildReplaySignals(previewEvents, t),
    [previewEvents, t],
  );
  const previewHealth = useMemo(
    () => buildReplayHealthSummary(previewEvents),
    [previewEvents],
  );
  const previewAcceptance = useMemo(() => {
    const eventTypes = new Set(previewEvents.map((event) => event.type));
    return milestoneEventTypes.map((type) => ({
      type,
      hit: eventTypes.has(type),
    }));
  }, [previewEvents]);

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="eyebrow">{t("runs.eyebrow")}</p>
          <h2>{t("runs.title")}</h2>
          <p className="page-copy">{t("runs.copy")}</p>
        </div>
        <button
          className="action-button action-button--secondary"
          onClick={() => setReloadToken((value) => value + 1)}
          type="button"
        >
          {t("common.refresh")}
        </button>
      </header>

      <section className="stats-grid">
        <article className="stat-card">
          <span className="stat-card__label">{t("runs.stats.total")}</span>
          <strong>{runs.length}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("runs.stats.agent")}</span>
          <strong>{agentRuns}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("runs.stats.daily")}</span>
          <strong>{dailyRuns}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("runs.stats.failed")}</span>
          <strong>{failedRuns}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("runs.stats.shown")}</span>
          <strong>{filteredRuns.length}</strong>
        </article>
      </section>

      <section className="panel filter-panel">
        <div className="filter-toolbar">
          <label className="field">
            <span>{t("common.search")}</span>
            <input
              onChange={(event) => setSearchText(event.target.value)}
              placeholder={t("runs.searchPlaceholder")}
              type="search"
              value={searchText}
            />
          </label>

          <label className="field">
            <span>{t("common.kind")}</span>
            <select
              onChange={(event) =>
                setKindFilter(event.target.value as "all" | "agent" | "daily")
              }
              value={kindFilter}
            >
              <option value="all">{t("common.all")}</option>
              <option value="agent">{t("common.agent")}</option>
              <option value="daily">{t("common.daily")}</option>
            </select>
          </label>

          <label className="field">
            <span>{t("common.status")}</span>
            <select
              onChange={(event) =>
                setStatusFilter(
                  event.target.value as "all" | "completed" | "running" | "failed",
                )
              }
              value={statusFilter}
            >
              <option value="all">{t("common.all")}</option>
              <option value="completed">{t("common.completed")}</option>
              <option value="running">{t("common.running")}</option>
              <option value="failed">{t("common.failed")}</option>
            </select>
          </label>
        </div>
      </section>

      {loading ? <div className="panel">{t("runs.loading")}</div> : null}
      {error ? <div className="panel panel--error">{error}</div> : null}
      {!loading && !error && runs.length === 0 ? (
        <div className="panel panel--empty">{t("runs.empty.none")}</div>
      ) : null}
      {!loading && !error && runs.length > 0 && filteredRuns.length === 0 ? (
        <div className="panel panel--empty">{t("runs.empty.filtered")}</div>
      ) : null}

      {!loading && !error && filteredRuns.length > 0 ? (
        <div className="explorer-layout">
          <section className="panel explorer-list">
            <div className="explorer-list__header">
              <div>
                <h3>{t("nav.runs")}</h3>
                <p>{t("runs.copy")}</p>
              </div>
            </div>

            <div className="list-stack">
              {filteredRuns.map((run) => {
                const isActive = run.runId === selectedRun?.runId;
                return (
                  <button
                    className={isActive ? "list-row list-row--active" : "list-row"}
                    key={run.runId}
                    onClick={() => setSearchParams({ selected: run.runId }, { replace: true })}
                    type="button"
                  >
                    <div className="list-row__meta">
                      <span className={`pill pill--${run.kind}`}>
                        {run.kind === "daily" ? t("common.daily") : t("common.agent")}
                      </span>
                      <span className={`pill pill--status-${run.status}`}>
                        {getStatusLabel(run.status, t)}
                      </span>
                    </div>
                    <h3>{getRunDisplayTitle(run)}</h3>
                    <p>{getRunDisplayCopy(run, t("runs.copyFallback"))}</p>
                    <div className="list-row__footer">
                      <span>{formatDateTime(run.startedAt)}</span>
                      <span>{`${t("common.events")}: ${run.eventCount}`}</span>
                    </div>
                  </button>
                );
              })}
            </div>
          </section>

          <aside className="panel explorer-preview">
            <div className="section-title">
              <h3>{t("runs.preview.title")}</h3>
              <p>{t("runs.preview.copy")}</p>
            </div>

            {selectedRun ? (
              <>
                <section className="preview-hero">
                  <div className="run-card__header">
                    <span className={`pill pill--${selectedRun.kind}`}>
                      {selectedRun.kind === "daily" ? t("common.daily") : t("common.agent")}
                    </span>
                    <span className={`pill pill--status-${selectedRun.status}`}>
                      {getStatusLabel(selectedRun.status, t)}
                    </span>
                  </div>
                  <h3>{getRunDisplayTitle(selectedRun)}</h3>
                  <p>{getRunDisplayCopy(selectedRun, t("runs.copyFallback"))}</p>
                </section>

                <dl className="meta-list preview-meta">
                  <div>
                    <dt>{t("common.run")}</dt>
                    <dd>{selectedRun.runId}</dd>
                  </div>
                  <div>
                    <dt>{t("common.conversation")}</dt>
                    <dd>{selectedRun.conversationId ?? t("common.na")}</dd>
                  </div>
                  <div>
                    <dt>{t("common.persona")}</dt>
                    <dd>{selectedRun.personaName ?? t("common.na")}</dd>
                  </div>
                  <div>
                    <dt>{t("common.started")}</dt>
                    <dd>{formatDateTime(selectedRun.startedAt)}</dd>
                  </div>
                  <div>
                    <dt>{t("common.finished")}</dt>
                    <dd>{formatDateTime(selectedRun.finishedAt)}</dd>
                  </div>
                  <div>
                    <dt>{t("common.events")}</dt>
                    <dd>{selectedRun.eventCount}</dd>
                  </div>
                </dl>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.output")}</p>
                  <p className="preview-copy">
                    {selectedRun.finalOutputPreview ??
                      selectedRun.inputPreview ??
                      t("common.noSummary")}
                  </p>
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.comparison")}</p>
                  <div className="preview-stat-grid">
                    <article className="preview-stat">
                      <span>{t("runs.preview.coverage")}</span>
                      <strong>{`${Math.round(previewHealth.acceptanceRatio * 100)}%`}</strong>
                      <small>
                        {previewHealth.acceptanceHits}/{previewHealth.acceptanceTotal}
                      </small>
                    </article>
                    <article className="preview-stat">
                      <span>{t("runs.preview.traceMemory")}</span>
                      <strong>{previewHealth.memoryEvents}</strong>
                      <small>{t("runs.preview.traceDepth")}</small>
                    </article>
                    <article className="preview-stat">
                      <span>{t("runs.preview.traceModel")}</span>
                      <strong>{previewHealth.modelEvents}</strong>
                      <small>{t("runs.preview.traceDepth")}</small>
                    </article>
                    <article className="preview-stat">
                      <span>{t("runs.preview.traceStep")}</span>
                      <strong>{previewHealth.stepEvents}</strong>
                      <small>{t("runs.preview.traceDepth")}</small>
                    </article>
                  </div>
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.acceptance")}</p>
                  {previewLoading ? (
                    <p className="preview-copy">{t("runs.preview.activityLoading")}</p>
                  ) : previewAcceptance.some((item) => item.hit) ? (
                    <div className="acceptance-list">
                      {previewAcceptance.map((item) => (
                        <article
                          className={
                            item.hit
                              ? "acceptance-item acceptance-item--hit"
                              : "acceptance-item"
                          }
                          key={item.type}
                        >
                          <strong>{item.type}</strong>
                          <span>
                            {item.hit
                              ? t("runs.preview.acceptanceHit")
                              : t("runs.preview.acceptanceMiss")}
                          </span>
                        </article>
                      ))}
                    </div>
                  ) : (
                    <p className="preview-copy">{t("runs.preview.acceptanceEmpty")}</p>
                  )}
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.signals")}</p>
                  {previewLoading ? (
                    <p className="preview-copy">{t("runs.preview.activityLoading")}</p>
                  ) : previewSignals.length > 0 ? (
                    <div className="signal-list">
                      {previewSignals.map((signal) => (
                        <article
                          className={`signal-card signal-card--${signal.tone}`}
                          key={`${signal.tone}-${signal.title}`}
                        >
                          <strong>{signal.title}</strong>
                          <p>{signal.copy}</p>
                        </article>
                      ))}
                    </div>
                  ) : (
                    <p className="preview-copy">{t("runs.preview.signalsEmpty")}</p>
                  )}
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.riskWatch")}</p>
                  {previewLoading ? (
                    <p className="preview-copy">{t("runs.preview.activityLoading")}</p>
                  ) : previewHealth.latestRiskEvent ? (
                    <article className="activity-item activity-item--risk">
                      <div className="activity-item__meta">
                        <span>{previewHealth.latestRiskEvent.type}</span>
                        <span>{formatDateTime(previewHealth.latestRiskEvent.timestamp)}</span>
                      </div>
                      <p>{summarizeEvent(previewHealth.latestRiskEvent, t)}</p>
                    </article>
                  ) : (
                    <p className="preview-copy">
                      {previewEvents.length > 0
                        ? t("runs.preview.riskPending")
                        : t("runs.preview.riskNone")}
                    </p>
                  )}
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.activity")}</p>
                  {previewLoading ? (
                    <p className="preview-copy">{t("runs.preview.activityLoading")}</p>
                  ) : previewHighlights.length > 0 ? (
                    <div className="activity-list">
                      {previewHighlights.map((event) => (
                        <article className="activity-item" key={`${event.runId}-${event.seq}`}>
                          <div className="activity-item__meta">
                            <span>{event.type}</span>
                            <span>{formatDateTime(event.timestamp)}</span>
                          </div>
                          <p>{summarizeEvent(event, t)}</p>
                        </article>
                      ))}
                    </div>
                  ) : (
                    <p className="preview-copy">{t("runs.preview.activityEmpty")}</p>
                  )}
                </div>

                <div className="preview-actions">
                  <Link className="action-link" to={`/runs/${selectedRun.runId}`}>
                    {t("runs.preview.open")}
                  </Link>
                </div>
              </>
            ) : (
              <div className="panel panel--empty">{t("runs.preview.empty")}</div>
            )}
          </aside>
        </div>
      ) : null}
    </section>
  );
}

function getStatusLabel(
  value: ReplayRunSummary["status"],
  t: (key: "common.completed" | "common.running" | "common.failed") => string,
) {
  switch (value) {
    case "running":
      return t("common.running");
    case "failed":
      return t("common.failed");
    default:
      return t("common.completed");
  }
}
