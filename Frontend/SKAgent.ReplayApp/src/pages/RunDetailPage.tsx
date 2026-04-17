import { useEffect, useMemo, useState } from "react";
import { Link, useParams } from "react-router-dom";
import { replayApi } from "../api";
import { useI18n, type TranslateFn } from "../i18n";
import type { ReplayEvent, ReplayRunDetail } from "../types";
import {
  buildReplaySignals,
  countEventsByGroup,
  getEventPriority,
  getEventGroup,
  matchesEventGroup,
  milestoneEventTypes,
  summarizePayloadPreview,
  summarizeEvent,
  type EventGroup,
} from "../replay-utils";

export function RunDetailPage() {
  const { formatDateTime, t } = useI18n();
  const { runId = "" } = useParams();
  const [detail, setDetail] = useState<ReplayRunDetail | null>(null);
  const [events, setEvents] = useState<ReplayEvent[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [eventFilter, setEventFilter] = useState<EventGroup>("all");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        const [detailResponse, eventResponse] = await Promise.all([
          replayApi.getRunDetail(runId),
          replayApi.getRunEvents(runId),
        ]);

        if (!cancelled) {
          setDetail(detailResponse);
          setEvents(eventResponse);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(err instanceof Error ? err.message : t("detail.error"));
        }
      } finally {
        if (!cancelled) {
          setLoading(false);
        }
      }
    }

    if (runId) {
      void load();
    }

    return () => {
      cancelled = true;
    };
  }, [reloadToken, runId]);

  const eventTypes = new Set(events.map((event) => event.type));
  const highlightedEventList = Array.from(milestoneEventTypes);
  const matchedHighlights = highlightedEventList.filter((type) =>
    eventTypes.has(type),
  );
  const eventCounts = useMemo(() => countEventsByGroup(events), [events]);
  const visibleEvents = useMemo(
    () => events.filter((event) => matchesEventGroup(event, eventFilter)),
    [eventFilter, events],
  );
  const recentRailEvents = useMemo(() => {
    return events
      .filter((event) => {
        const group = getEventGroup(event.type);
        return group === "milestone" || group === "model" || group === "memory";
      })
      .slice(-4)
      .reverse();
  }, [events]);
  const replaySignals = useMemo(
    () => buildReplaySignals(events, t),
    [events, t],
  );

  return (
    <section className="page">
      <header className="page-header page-header--detail">
        <div>
          <p className="eyebrow">{t("detail.eyebrow")}</p>
          <h2>{detail?.summary.goal ?? detail?.summary.runId ?? t("detail.titleFallback")}</h2>
          <p className="page-copy">{t("detail.copy")}</p>
        </div>
        <div className="header-actions">
          <button
            className="action-button action-button--secondary"
            onClick={() => setReloadToken((value) => value + 1)}
            type="button"
          >
            {t("common.refresh")}
          </button>
          <Link className="action-link" to="/runs">
            {t("detail.backToRuns")}
          </Link>
        </div>
      </header>

      {loading ? <div className="panel">{t("detail.loading")}</div> : null}
      {error ? <div className="panel panel--error">{error}</div> : null}

      {!loading && !error && detail ? (
        <div className="detail-layout">
          <main className="detail-main">
            <section className="panel panel--timeline" id="timeline">
              <div className="section-title">
                <div>
                  <p className="eyebrow">{t("detail.section.timeline")}</p>
                  <h3>{t("detail.timeline.title")}</h3>
                </div>
                <p>{t("detail.timeline.copy")}</p>
              </div>

              <div className="filter-pills filter-pills--sticky">
                {([
                  ["all", `${t("detail.filter.all")} (${eventCounts.all})`],
                  ["milestone", `${t("detail.filter.milestone")} (${eventCounts.milestone})`],
                  ["memory", `${t("detail.filter.memory")} (${eventCounts.memory})`],
                  ["model", `${t("detail.filter.model")} (${eventCounts.model})`],
                  ["step", `${t("detail.filter.step")} (${eventCounts.step})`],
                  ["daily", `${t("detail.filter.daily")} (${eventCounts.daily})`],
                  ["system", `${t("detail.filter.system")} (${eventCounts.system})`],
                ] as const).map(([value, label]) => (
                  <button
                    className={
                      eventFilter === value
                        ? "filter-pill filter-pill--active"
                        : "filter-pill"
                    }
                    key={value}
                    onClick={() => setEventFilter(value)}
                    type="button"
                  >
                    {label}
                  </button>
                ))}
              </div>

              {events.length === 0 ? (
                <div className="panel panel--empty">{t("detail.timeline.empty")}</div>
              ) : visibleEvents.length === 0 ? (
                <div className="panel panel--empty">{t("detail.timeline.filtered")}</div>
              ) : (
                <div className="timeline">
                  {visibleEvents.map((event) => {
                    const group = getEventGroup(event.type);
                    const priority = getEventPriority(event);
                    const payloadPreview = summarizePayloadPreview(event, t);
                    const payloadExpanded =
                      priority === "critical" || priority === "high";
                    return (
                      <article
                        className={`timeline-item timeline-item--${priority} timeline-item--group-${group}`}
                        key={`${event.runId}-${event.seq}`}
                      >
                        <div className="timeline-item__marker" />
                        <div className="timeline-item__header">
                          <span className="timeline-index">#{event.seq}</span>
                          <span className="timeline-type">{event.type}</span>
                          <span className={`timeline-group timeline-group--${group}`}>
                            {getGroupLabel(group, t)}
                          </span>
                          <span className={`timeline-priority timeline-priority--${priority}`}>
                            {getPriorityLabel(priority, t)}
                          </span>
                          <span className="timeline-time">
                            {formatDateTime(event.timestamp)}
                          </span>
                        </div>
                        <p className="timeline-summary">{summarizeEvent(event, t)}</p>
                        {payloadPreview ? (
                          <p className="timeline-payload-preview">
                            <span>{t("detail.timeline.payloadPreview")}</span>
                            {payloadPreview}
                          </p>
                        ) : null}
                        <details className="payload-toggle" open={payloadExpanded}>
                          <summary>
                            {payloadExpanded
                              ? t("detail.timeline.rawPayloadExpanded")
                              : t("detail.timeline.rawPayloadCollapsed")}
                          </summary>
                          <pre>{JSON.stringify(event.payload, null, 2)}</pre>
                        </details>
                      </article>
                    );
                  })}
                </div>
              )}
            </section>

            <section className="panel" id="prompt">
              <div className="section-title">
                <div>
                  <p className="eyebrow">{t("detail.section.context")}</p>
                  <h3>{t("detail.prompt.title")}</h3>
                </div>
                <p>{t("detail.prompt.copy")}</p>
              </div>
              <div className="diagnostic-grid">
                <article className="diagnostic-card">
                  <dl className="meta-list meta-list--wide">
                    <div>
                      <dt>{t("common.target")}</dt>
                      <dd>{detail.prompt?.target ?? t("common.na")}</dd>
                    </div>
                    <div>
                      <dt>{t("common.hash")}</dt>
                      <dd>{detail.prompt?.hash ?? t("common.na")}</dd>
                    </div>
                    <div>
                      <dt>{t("common.layers")}</dt>
                      <dd>{detail.prompt?.layersUsed.join(", ") || t("common.na")}</dd>
                    </div>
                    <div>
                      <dt>{t("common.chars")}</dt>
                      <dd>
                        system={detail.prompt?.systemChars ?? t("common.na")}, user={detail.prompt?.userChars ?? t("common.na")}
                      </dd>
                    </div>
                  </dl>
                </article>
                <div className="code-panels">
                  <article>
                    <h4>{t("common.system")}</h4>
                    <pre>{detail.prompt?.systemText ?? t("common.noPromptText")}</pre>
                  </article>
                  <article>
                    <h4>{t("common.user")}</h4>
                    <pre>{detail.prompt?.userText ?? t("common.noPromptText")}</pre>
                  </article>
                </div>
              </div>
            </section>

            <section className="panel" id="steps">
              <div className="section-title">
                <h3>{t("detail.steps.title")}</h3>
                <p>{t("detail.steps.copy")}</p>
              </div>

              {detail.steps.length === 0 ? (
                <div className="panel panel--empty">{t("detail.steps.empty")}</div>
              ) : (
                <div className="stack">
                  {detail.steps.map((step) => (
                    <article className="step-card" key={step.order}>
                      <div className="run-card__header">
                        <span className="pill pill--muted">{`${t("detail.stats.steps")} ${step.order}`}</span>
                        <span className={`pill pill--status-${step.status}`}>
                          {getStatusLabel(step.status, t)}
                        </span>
                      </div>
                      <h4>{step.target ?? step.kind ?? t("detail.stats.steps")}</h4>
                      <p>{step.outputPreview ?? step.error ?? t("common.noPreview")}</p>
                    </article>
                  ))}
                </div>
              )}
            </section>

            <section className="panel" id="memory">
              <div className="section-title">
                <h3>{t("detail.memory.title")}</h3>
                <p>{t("detail.memory.copy")}</p>
              </div>

              <div className="diagnostic-grid">
                <article className="diagnostic-card">
                  <dl className="meta-list meta-list--wide">
                    <div>
                      <dt>{t("detail.memory.source")}</dt>
                      <dd>{detail.memory?.recallSource ?? t("detail.memory.recallNone")}</dd>
                    </div>
                    <div>
                      <dt>{t("detail.memory.preview")}</dt>
                      <dd>{detail.memory?.recallPreview ?? t("detail.memory.recallNone")}</dd>
                    </div>
                    <div>
                      <dt>{t("detail.memory.vector")}</dt>
                      <dd>
                        {detail.memory?.vectorTopK
                          ? `topK=${detail.memory.vectorTopK}, latency=${detail.memory.vectorLatencyMs ?? t("common.na")}ms`
                          : t("common.na")}
                      </dd>
                    </div>
                    <div>
                      <dt>{t("common.budget")}</dt>
                      <dd>{detail.memory?.budgetUsed ?? 0}</dd>
                    </div>
                    <div>
                      <dt>{t("common.totalItems")}</dt>
                      <dd>{detail.memory?.totalItems ?? 0}</dd>
                    </div>
                  </dl>

                  {Object.keys(detail.memory?.byRouteCounts ?? {}).length === 0 ? (
                    <div className="panel panel--empty">{t("detail.memory.emptyRoutes")}</div>
                  ) : (
                    <div className="memory-routes">
                      {Object.entries(detail.memory?.byRouteCounts ?? {}).map(
                        ([route, count]) => (
                          <div className="memory-route" key={route}>
                            <span>{route}</span>
                            <strong>{count}</strong>
                          </div>
                        ),
                      )}
                    </div>
                  )}
                </article>

                {(detail.memory?.layers.length ?? 0) === 0 ? (
                  <div className="panel panel--empty">{t("detail.memory.emptyLayers")}</div>
                ) : (
                  <div className="stack">
                    {(detail.memory?.layers ?? []).map((layer) => (
                      <article className="step-card" key={layer.layer}>
                        <h4>{layer.layer}</h4>
                        <p>
                          before={layer.countBefore ?? t("common.na")}, after={layer.countAfter ?? t("common.na")},
                          budget={layer.budgetChars ?? t("common.na")}
                        </p>
                        <p>{layer.truncateReason ?? t("common.noTruncateReason")}</p>
                      </article>
                    ))}
                  </div>
                )}
              </div>
            </section>
          </main>

          <aside className="detail-rail">
            <div className="detail-rail__sticky">
              <section className="panel panel--hero">
                <div className="section-title">
                  <div>
                    <p className="eyebrow">{t("detail.rail.title")}</p>
                    <h3>{detail.summary.goal ?? detail.summary.runId}</h3>
                  </div>
                  <p>{t("detail.rail.copy")}</p>
                </div>

                <div className="run-card__header">
                  <span className={`pill pill--${detail.summary.kind}`}>
                    {detail.summary.kind === "daily" ? t("common.daily") : t("common.agent")}
                  </span>
                  <span className={`pill pill--status-${detail.summary.status}`}>
                    {getStatusLabel(detail.summary.status, t)}
                  </span>
                </div>

                <p className="hero-output">
                  {detail.summary.finalOutputPreview ??
                    detail.summary.inputPreview ??
                    t("common.noSummary")}
                </p>

                <dl className="meta-list preview-meta">
                  <div>
                    <dt>{t("common.run")}</dt>
                    <dd>{detail.summary.runId}</dd>
                  </div>
                  <div>
                    <dt>{t("common.conversation")}</dt>
                    <dd>{detail.summary.conversationId ?? t("common.na")}</dd>
                  </div>
                  <div>
                    <dt>{t("common.persona")}</dt>
                    <dd>{detail.summary.personaName ?? t("common.na")}</dd>
                  </div>
                  <div>
                    <dt>{t("common.started")}</dt>
                    <dd>{formatDateTime(detail.summary.startedAt)}</dd>
                  </div>
                  <div>
                    <dt>{t("common.finished")}</dt>
                    <dd>{formatDateTime(detail.summary.finishedAt)}</dd>
                  </div>
                  <div>
                    <dt>{t("common.events")}</dt>
                    <dd>{detail.summary.eventCount}</dd>
                  </div>
                </dl>
              </section>

              <section className="stats-grid stats-grid--rail">
                <article className="stat-card">
                  <span className="stat-card__label">{t("detail.stats.timeline")}</span>
                  <strong>{events.length}</strong>
                </article>
                <article className="stat-card">
                  <span className="stat-card__label">{t("detail.stats.milestones")}</span>
                  <strong>{matchedHighlights.length}</strong>
                </article>
                <article className="stat-card">
                  <span className="stat-card__label">{t("detail.stats.steps")}</span>
                  <strong>{detail.steps.length}</strong>
                </article>
                <article className="stat-card">
                  <span className="stat-card__label">{t("detail.stats.memoryLayers")}</span>
                  <strong>{detail.memory?.layers.length ?? 0}</strong>
                </article>
                <article className="stat-card">
                  <span className="stat-card__label">{t("detail.stats.promptChars")}</span>
                  <strong>
                    {(detail.prompt?.systemChars ?? 0) + (detail.prompt?.userChars ?? 0)}
                  </strong>
                </article>
              </section>

              <section className="panel">
                <div className="section-title">
                  <h3>{t("detail.milestones.title")}</h3>
                  <p>{t("detail.milestones.copy")}</p>
                </div>

                <div className="milestone-grid milestone-grid--rail">
                  {highlightedEventList.map((type) => {
                    const active = eventTypes.has(type);
                    return (
                      <div
                        className={active ? "milestone-chip milestone-chip--active" : "milestone-chip"}
                        key={type}
                      >
                        {type}
                      </div>
                    );
                  })}
                </div>
              </section>

              <section className="panel">
                <div className="section-title">
                  <h3>{t("detail.rail.jump")}</h3>
                </div>
                <nav className="section-nav">
                  <a className="section-nav__link" href="#timeline">
                    {t("detail.section.timeline")}
                  </a>
                  <a className="section-nav__link" href="#prompt">
                    {t("detail.section.prompt")}
                  </a>
                  <a className="section-nav__link" href="#steps">
                    {t("detail.section.steps")}
                  </a>
                  <a className="section-nav__link" href="#memory">
                    {t("detail.section.memory")}
                  </a>
                </nav>
              </section>

              <section className="panel">
                <div className="section-title">
                  <h3>{t("detail.rail.recent")}</h3>
                </div>
                {recentRailEvents.length > 0 ? (
                  <div className="activity-list">
                    {recentRailEvents.map((event) => (
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
                  <p className="preview-copy">{t("detail.rail.recentEmpty")}</p>
                )}
              </section>

              <section className="panel">
                <div className="section-title">
                  <h3>{t("detail.rail.signals")}</h3>
                </div>
                {replaySignals.length > 0 ? (
                  <div className="signal-list">
                    {replaySignals.map((signal) => (
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
                  <p className="preview-copy">{t("detail.rail.signalsEmpty")}</p>
                )}
              </section>
            </div>
          </aside>
        </div>
      ) : null}
    </section>
  );
}

function getGroupLabel(
  value: EventGroup,
  t: TranslateFn,
) {
  switch (value) {
    case "milestone":
      return t("detail.filter.milestone");
    case "memory":
      return t("detail.filter.memory");
    case "model":
      return t("detail.filter.model");
    case "step":
      return t("detail.filter.step");
    case "daily":
      return t("detail.filter.daily");
    case "system":
      return t("detail.filter.system");
    default:
      return t("detail.filter.all");
  }
}

function getStatusLabel(value: string, t: TranslateFn) {
  switch (value) {
    case "running":
      return t("common.running");
    case "failed":
      return t("common.failed");
    default:
      return t("common.completed");
  }
}

function getPriorityLabel(
  value: "critical" | "high" | "normal" | "quiet",
  t: TranslateFn,
) {
  switch (value) {
    case "critical":
      return t("detail.priority.critical");
    case "high":
      return t("detail.priority.high");
    case "normal":
      return t("detail.priority.normal");
    default:
      return t("detail.priority.quiet");
  }
}
