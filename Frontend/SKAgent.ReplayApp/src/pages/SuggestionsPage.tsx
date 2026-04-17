import { useDeferredValue, useEffect, useMemo, useState } from "react";
import { Link, useSearchParams } from "react-router-dom";
import { replayApi } from "../api";
import { useI18n } from "../i18n";
import {
  buildReplayHealthSummary,
  buildReplaySignals,
  summarizeEvent,
} from "../replay-utils";
import type { ReplayEvent } from "../types";
import type { ReplaySuggestion } from "../types";

export function SuggestionsPage() {
  const { formatDateTime, t } = useI18n();
  const [searchParams, setSearchParams] = useSearchParams();
  const [items, setItems] = useState<ReplaySuggestion[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [reloadToken, setReloadToken] = useState(0);
  const [searchText, setSearchText] = useState("");
  const [previewEvents, setPreviewEvents] = useState<ReplayEvent[]>([]);
  const [previewLoading, setPreviewLoading] = useState(false);
  const deferredSearchText = useDeferredValue(searchText);
  const selectedRunId = searchParams.get("selected");

  useEffect(() => {
    let cancelled = false;

    async function load() {
      try {
        setLoading(true);
        const response = await replayApi.listSuggestions(30);
        if (!cancelled) {
          setItems(response);
          setError(null);
        }
      } catch (err) {
        if (!cancelled) {
          setError(
            err instanceof Error ? err.message : t("suggestions.error"),
          );
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

  const replayReadyCount = items.filter((item) => item.replayAvailable).length;
  const filteredItems = useMemo(() => {
    const query = deferredSearchText.trim().toLowerCase();
    if (!query) {
      return items;
    }

    return items.filter((item) =>
      [item.date, item.personaName, item.conversationId, item.suggestion, item.runId]
        .filter(Boolean)
        .some((value) => String(value).toLowerCase().includes(query)),
    );
  }, [deferredSearchText, items]);

  useEffect(() => {
    if (filteredItems.length === 0) {
      if (selectedRunId !== null) {
        setSearchParams({}, { replace: true });
      }
      return;
    }

    if (!selectedRunId || !filteredItems.some((item) => item.runId === selectedRunId)) {
      setSearchParams({ selected: filteredItems[0].runId }, { replace: true });
    }
  }, [filteredItems, selectedRunId, setSearchParams]);

  const selectedItem =
    filteredItems.find((item) => item.runId === selectedRunId) ?? filteredItems[0] ?? null;
  useEffect(() => {
    let cancelled = false;

    async function loadPreviewEvents() {
      if (!selectedItem?.runId || !selectedItem.replayAvailable) {
        setPreviewEvents([]);
        return;
      }

      try {
        setPreviewLoading(true);
        const response = await replayApi.getRunEvents(selectedItem.runId);
        if (!cancelled) {
          setPreviewEvents(response.slice(-4).reverse());
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
  }, [selectedItem?.replayAvailable, selectedItem?.runId]);

  const previewSignals = useMemo(
    () => buildReplaySignals(previewEvents, t),
    [previewEvents, t],
  );
  const previewHealth = useMemo(
    () => buildReplayHealthSummary(previewEvents),
    [previewEvents],
  );

  return (
    <section className="page">
      <header className="page-header">
        <div>
          <p className="eyebrow">{t("suggestions.eyebrow")}</p>
          <h2>{t("suggestions.title")}</h2>
          <p className="page-copy">{t("suggestions.copy")}</p>
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
          <span className="stat-card__label">{t("suggestions.stats.total")}</span>
          <strong>{items.length}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("suggestions.stats.ready")}</span>
          <strong>{replayReadyCount}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("suggestions.stats.missing")}</span>
          <strong>{items.length - replayReadyCount}</strong>
        </article>
        <article className="stat-card">
          <span className="stat-card__label">{t("suggestions.stats.shown")}</span>
          <strong>{filteredItems.length}</strong>
        </article>
      </section>

      <section className="panel filter-panel">
        <div className="filter-toolbar">
          <label className="field field--grow">
            <span>{t("common.search")}</span>
            <input
              onChange={(event) => setSearchText(event.target.value)}
              placeholder={t("suggestions.searchPlaceholder")}
              type="search"
              value={searchText}
            />
          </label>
        </div>
      </section>

      {loading ? <div className="panel">{t("suggestions.loading")}</div> : null}
      {error ? <div className="panel panel--error">{error}</div> : null}
      {!loading && !error && items.length === 0 ? (
        <div className="panel panel--empty">{t("suggestions.empty.none")}</div>
      ) : null}
      {!loading && !error && items.length > 0 && filteredItems.length === 0 ? (
        <div className="panel panel--empty">{t("suggestions.empty.filtered")}</div>
      ) : null}

      {!loading && !error && filteredItems.length > 0 ? (
        <div className="explorer-layout">
          <section className="panel explorer-list">
            <div className="explorer-list__header">
              <div>
                <h3>{t("nav.suggestions")}</h3>
                <p>{t("suggestions.copy")}</p>
              </div>
            </div>

            <div className="list-stack">
              {filteredItems.map((item) => {
                const isActive = item.runId === selectedItem?.runId;
                return (
                  <button
                    className={isActive ? "list-row list-row--active" : "list-row"}
                    key={`${item.date}-${item.runId}`}
                    onClick={() =>
                      setSearchParams({ selected: item.runId }, { replace: true })
                    }
                    type="button"
                  >
                    <div className="list-row__meta">
                      <span className="pill pill--daily">{t("common.daily")}</span>
                      <span
                        className={
                          item.replayAvailable
                            ? "pill pill--status-completed"
                            : "pill pill--muted"
                        }
                      >
                        {item.replayAvailable
                          ? t("suggestions.badge.ready")
                          : t("suggestions.badge.missing")}
                      </span>
                    </div>
                    <h3>{item.date}</h3>
                    <p>{item.suggestion}</p>
                    <div className="list-row__footer">
                      <span>{item.personaName}</span>
                      <span>{formatDateTime(item.createdAtUtc)}</span>
                    </div>
                  </button>
                );
              })}
            </div>
          </section>

          <aside className="panel explorer-preview">
            <div className="section-title">
              <h3>{t("suggestions.preview.title")}</h3>
              <p>{t("suggestions.preview.copy")}</p>
            </div>

            {selectedItem ? (
              <>
                <section className="preview-hero">
                  <div className="run-card__header">
                    <span className="pill pill--daily">{t("common.daily")}</span>
                    <span
                      className={
                        selectedItem.replayAvailable
                          ? "pill pill--status-completed"
                          : "pill pill--muted"
                      }
                    >
                      {selectedItem.replayAvailable
                        ? t("suggestions.badge.ready")
                        : t("suggestions.badge.missing")}
                    </span>
                  </div>
                  <h3>{selectedItem.date}</h3>
                  <p>{selectedItem.suggestion}</p>
                </section>

                <dl className="meta-list preview-meta">
                  <div>
                    <dt>{t("common.persona")}</dt>
                    <dd>{selectedItem.personaName}</dd>
                  </div>
                  <div>
                    <dt>{t("common.run")}</dt>
                    <dd>{selectedItem.runId}</dd>
                  </div>
                  <div>
                    <dt>{t("common.conversation")}</dt>
                    <dd>{selectedItem.conversationId}</dd>
                  </div>
                  <div>
                    <dt>{t("common.created")}</dt>
                    <dd>{formatDateTime(selectedItem.createdAtUtc)}</dd>
                  </div>
                  <div>
                    <dt>{t("common.hash")}</dt>
                    <dd>{selectedItem.promptHash}</dd>
                  </div>
                </dl>

                <div className="preview-block">
                  <p className="sidebar-label">{t("suggestions.preview.trace")}</p>
                  <p className="preview-copy">
                    {selectedItem.replayAvailable
                      ? t("suggestions.preview.traceReady")
                      : t("suggestions.preview.traceMissing")}
                  </p>
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("suggestions.preview.diagnostics")}</p>
                  <div className="preview-stat-grid">
                    <article className="preview-stat">
                      <span>{t("runs.preview.coverage")}</span>
                      <strong>{`${Math.round(previewHealth.acceptanceRatio * 100)}%`}</strong>
                      <small>
                        {previewHealth.acceptanceHits}/{previewHealth.acceptanceTotal}
                      </small>
                    </article>
                    <article className="preview-stat">
                      <span>{t("suggestions.preview.events")}</span>
                      <strong>{previewEvents.length}</strong>
                      <small>{t("runs.preview.traceDepth")}</small>
                    </article>
                    <article className="preview-stat">
                      <span>{t("runs.preview.traceDaily")}</span>
                      <strong>{previewHealth.dailyEvents}</strong>
                      <small>{t("runs.preview.traceDepth")}</small>
                    </article>
                    <article className="preview-stat">
                      <span>{t("suggestions.preview.risk")}</span>
                      <strong>{previewHealth.riskEvents}</strong>
                      <small>{t("runs.preview.riskWatch")}</small>
                    </article>
                  </div>
                </div>

                <div className="preview-block">
                  <p className="sidebar-label">{t("runs.preview.signals")}</p>
                  {selectedItem.replayAvailable && previewLoading ? (
                    <p className="preview-copy">{t("runs.preview.activityLoading")}</p>
                  ) : selectedItem.replayAvailable && previewSignals.length > 0 ? (
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

                {selectedItem.replayAvailable ? (
                  <div className="preview-block">
                    <p className="sidebar-label">{t("runs.preview.activity")}</p>
                    {previewLoading ? (
                      <p className="preview-copy">{t("runs.preview.activityLoading")}</p>
                    ) : previewEvents.length > 0 ? (
                      <div className="activity-list">
                        {previewEvents.map((event) => (
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
                ) : null}

                <div className="preview-actions">
                  {selectedItem.replayAvailable ? (
                    <Link className="action-link" to={`/runs/${selectedItem.runId}`}>
                      {t("common.openReplay")}
                    </Link>
                  ) : (
                    <span className="action-link action-link--disabled">
                      {t("common.replayUnavailable")}
                    </span>
                  )}
                </div>
              </>
            ) : (
              <div className="panel panel--empty">{t("suggestions.preview.empty")}</div>
            )}
          </aside>
        </div>
      ) : null}
    </section>
  );
}
