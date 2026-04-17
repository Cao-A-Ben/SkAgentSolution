import { NavLink, Route, Routes } from "react-router-dom";
import { useI18n, type Locale } from "./i18n";
import { RunDetailPage } from "./pages/RunDetailPage";
import { RunsPage } from "./pages/RunsPage";
import { SuggestionsPage } from "./pages/SuggestionsPage";

export function App() {
  const { locale, setLocale, t } = useI18n();

  return (
    <div className="shell">
      <aside className="sidebar">
        <div className="brand-block">
          <p className="eyebrow">Week9</p>
          <h1>SkAgent Replay</h1>
          <p className="brand-copy">{t("app.brand.copy")}</p>
        </div>

        <div className="language-switch">
          <span className="sidebar-label">{t("language.label")}</span>
          <div className="language-switch__buttons">
            {(["zh-CN", "en"] as Locale[]).map((item) => (
              <button
                className={
                  locale === item
                    ? "language-button language-button--active"
                    : "language-button"
                }
                key={item}
                onClick={() => setLocale(item)}
                type="button"
              >
                {getLocaleLabel(item, t)}
              </button>
            ))}
          </div>
        </div>

        <nav className="nav-list">
          <NavLink
            to="/runs"
            className={({ isActive }) =>
              isActive ? "nav-link nav-link--active" : "nav-link"
            }
          >
            {t("nav.runs")}
          </NavLink>
          <NavLink
            to="/suggestions"
            className={({ isActive }) =>
              isActive ? "nav-link nav-link--active" : "nav-link"
            }
          >
            {t("nav.suggestions")}
          </NavLink>
        </nav>

        <div className="sidebar-card">
          <p className="sidebar-label">{t("sidebar.focus.title")}</p>
          <p className="sidebar-value">{t("sidebar.focus.copy")}</p>
        </div>

        <div className="sidebar-card">
          <p className="sidebar-label">{t("sidebar.shipping.title")}</p>
          <div className="scope-stack">
            <span className="scope-chip">{t("sidebar.shipping.runList")}</span>
            <span className="scope-chip">{t("sidebar.shipping.runDetail")}</span>
            <span className="scope-chip">{t("sidebar.shipping.timeline")}</span>
            <span className="scope-chip">{t("sidebar.shipping.dailyReplay")}</span>
          </div>
        </div>

        <div className="sidebar-card">
          <p className="sidebar-label">{t("sidebar.later.title")}</p>
          <div className="roadmap-list">
            <div>
              <strong>Week10</strong>
              <p>{t("sidebar.week10.copy")}</p>
            </div>
            <div>
              <strong>Week11</strong>
              <p>{t("sidebar.week11.copy")}</p>
            </div>
            <div>
              <strong>Week12</strong>
              <p>{t("sidebar.week12.copy")}</p>
            </div>
          </div>
        </div>
      </aside>

      <main className="main-panel">
        <div className="app-topbar">
          <div>
            <p className="eyebrow">Week9</p>
            <h2 className="app-topbar__title">{t("app.shell.title")}</h2>
            <p className="page-copy">{t("app.shell.copy")}</p>
          </div>
          <div className="app-chip-row">
            <span className="app-chip">{t("app.shell.chip.replay")}</span>
            <span className="app-chip">{t("app.shell.chip.api")}</span>
            <span className="app-chip">{t("app.shell.chip.locale")}</span>
          </div>
        </div>

        <div className="content-frame">
          <Routes>
            <Route path="/" element={<RunsPage />} />
            <Route path="/runs" element={<RunsPage />} />
            <Route path="/runs/:runId" element={<RunDetailPage />} />
            <Route path="/suggestions" element={<SuggestionsPage />} />
          </Routes>
        </div>
      </main>
    </div>
  );
}

function getLocaleLabel(
  locale: Locale,
  t: (key: "language.zh-CN" | "language.en") => string,
) {
  return locale === "zh-CN" ? t("language.zh-CN") : t("language.en");
}
