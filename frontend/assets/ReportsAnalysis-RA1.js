/**
 * Admin -> Reports & Analysis (Regalis_WorkSpace_Reports_Analysis_Feature_Specification).
 *
 *   ReportsOverviewPage  /admin/reports/overview, /admin/reports/charts/:section — the charts
 *   ReportPage           /admin/reports/:key — any of the 24 reports, driven by its definition
 *   ReportLibraryPage    /admin/reports/library — grouped, searchable, favourites, schedules
 *   ReportDownloadPage   /admin/reports/download/:token — a large export's emailed link
 *
 * Every total, count, chart series and percentage comes from the API already
 * calculated over all matching rows (spec §8.1); this file only draws.
 *
 * Hand-maintained (this repo ships the built SPA). Not in the sw.js precache, and IIS
 * caches /assets/* for a year: the next fix ships as ReportsAnalysis-RA2.js with the
 * imports in index-DI5X4c9f.js repointed (see the memory note on hand-written chunks).
 */
import {
  a as useToast,
  r as React,
  A as ApiError,
  p as Button,
  D as unwrap,
  E as http,
  i as makeIcon,
  g as Link,
  Y as useParams,
  u as useNavigate,
  l as useAuth
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";

const h = React.createElement;
const Frag = React.Fragment;

// ── Icons ───────────────────────────────────────────────────────────────────

const IconStar = makeIcon("star", [["path", { d: "M11.525 2.295a.53.53 0 0 1 .95 0l2.31 4.679a2.123 2.123 0 0 0 1.595 1.16l5.166.756a.53.53 0 0 1 .294.904l-3.736 3.638a2.123 2.123 0 0 0-.611 1.878l.882 5.14a.53.53 0 0 1-.771.56l-4.618-2.428a2.122 2.122 0 0 0-1.973 0L6.396 21.01a.53.53 0 0 1-.77-.56l.881-5.139a2.122 2.122 0 0 0-.611-1.879L2.16 9.795a.53.53 0 0 1 .294-.906l5.165-.755a2.122 2.122 0 0 0 1.597-1.16z", key: "a" }]]);
const IconMail = makeIcon("mail", [["rect", { width: "20", height: "16", x: "2", y: "4", rx: "2", key: "a" }], ["path", { d: "m22 7-8.97 5.7a1.94 1.94 0 0 1-2.06 0L2 7", key: "b" }]]);
const IconDownload = makeIcon("download", [["path", { d: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4", key: "a" }], ["polyline", { points: "7 10 12 15 17 10", key: "b" }], ["line", { x1: "12", x2: "12", y1: "15", y2: "3", key: "c" }]]);
const IconSearch = makeIcon("search", [["circle", { cx: "11", cy: "11", r: "8", key: "a" }], ["path", { d: "m21 21-4.3-4.3", key: "b" }]]);
const IconX = makeIcon("x", [["path", { d: "M18 6 6 18", key: "a" }], ["path", { d: "m6 6 12 12", key: "b" }]]);
const IconColumns = makeIcon("columns-3", [["rect", { width: "18", height: "18", x: "3", y: "3", rx: "2", key: "a" }], ["path", { d: "M9 3v18", key: "b" }], ["path", { d: "M15 3v18", key: "c" }]]);
const IconChevronDown = makeIcon("chevron-down", [["path", { d: "m6 9 6 6 6-6", key: "a" }]]);
const IconUp = makeIcon("arrow-up", [["path", { d: "m5 12 7-7 7 7", key: "a" }], ["path", { d: "M12 19V5", key: "b" }]]);
const IconDown = makeIcon("arrow-down", [["path", { d: "M12 5v14", key: "a" }], ["path", { d: "m19 12-7 7-7-7", key: "b" }]]);
const IconPlus = makeIcon("plus", [["path", { d: "M5 12h14", key: "a" }], ["path", { d: "M12 5v14", key: "b" }]]);
const IconTable = makeIcon("table", [["path", { d: "M12 3v18", key: "a" }], ["rect", { width: "18", height: "18", x: "3", y: "3", rx: "2", key: "b" }], ["path", { d: "M3 9h18", key: "c" }], ["path", { d: "M3 15h18", key: "d" }]]);
const IconChart = makeIcon("chart-column", [["path", { d: "M3 3v16a2 2 0 0 0 2 2h16", key: "a" }], ["path", { d: "M18 17V9", key: "b" }], ["path", { d: "M13 17V5", key: "c" }], ["path", { d: "M8 17v-3", key: "d" }]]);

// ── API ─────────────────────────────────────────────────────────────────────

/** Saves a file response. Returns false when the server queued it instead (202). */
async function saveBlobResponse(response, fallbackName) {
  const disposition = response.headers?.["content-disposition"] ?? "";
  const named = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1];
  const href = URL.createObjectURL(response.data);
  const link = document.createElement("a");
  link.href = href;
  link.download = named ? decodeURIComponent(named) : fallbackName;
  document.body.appendChild(link);
  link.click();
  link.remove();
  setTimeout(() => URL.revokeObjectURL(href), 1500);
}

async function blobJson(blob) {
  try {
    return JSON.parse(await blob.text());
  } catch {
    return null;
  }
}

const api = {
  overview: (params) => unwrap(http.get("/admin/reports/overview", { params })),
  overviewPdf: async (params) => {
    const response = await http.get("/admin/reports/overview/pdf", { params, responseType: "blob" });
    await saveBlobResponse(response, "reports-and-analysis.pdf");
  },
  definitions: () => unwrap(http.get("/admin/reports/definitions")),
  run: (key, body) => unwrap(http.post(`/admin/reports/${key}/run`, body)),
  /** Downloads the file, or returns { queued: true, message } for a background export. */
  exportFile: async (key, body) => {
    try {
      const response = await http.post(`/admin/reports/${key}/export`, body, { responseType: "blob" });
      if (response.status === 202) {
        const json = await blobJson(response.data);
        return { queued: true, message: json?.message ?? "We will email the export to you when it is ready." };
      }
      await saveBlobResponse(response, `${key}.${body.format}`);
      return { queued: false };
    } catch (error) {
      // A blob error body hides the server's message; read it back out.
      const data = error?.response?.data instanceof Blob ? await blobJson(error.response.data) : null;
      if (data?.message) throw new Error(data.message);
      throw error;
    }
  },
  addFavourite: (key) => http.post(`/admin/reports/favourites/${key}`),
  removeFavourite: (key) => http.delete(`/admin/reports/favourites/${key}`),
  schedules: () => unwrap(http.get("/admin/reports/schedules")),
  createSchedule: (body) => unwrap(http.post("/admin/reports/schedules", body)),
  updateSchedule: (id, body) => unwrap(http.patch(`/admin/reports/schedules/${id}`, body)),
  stopSchedule: (id) => http.delete(`/admin/reports/schedules/${id}`),
  savePreferences: (key, columns) => http.put(`/admin/reports/${key}/preferences`, { columns }),
  customers: (q) => unwrap(http.get("/admin/reports/customers", { params: { q } })),
  download: async (token) => {
    const response = await http.get(`/admin/reports/exports/${token}`, { responseType: "blob" });
    await saveBlobResponse(response, "report");
  }
};

function errorText(error, fallback) {
  if (error instanceof ApiError) return error.errors?.[0]?.message ?? error.message ?? fallback;
  return error?.message ?? fallback;
}

/** Field-level messages from a 400, keyed by field. */
function fieldErrors(error) {
  const map = {};
  if (error instanceof ApiError) for (const e of error.errors ?? []) if (e.field) map[e.field] = e.message;
  return map;
}

// ── Formatting (spec §8.3, §8.4) ────────────────────────────────────────────

const IST_MS = 5.5 * 3600 * 1000;
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

function num(value) {
  if (value == null || value === "") return null;
  const n = Number(value);
  return Number.isFinite(n) ? n : null;
}

/** ₹2,70,900 — Indian grouping; paise only where they matter. */
function money(value, paise = false) {
  const n = num(value);
  if (n == null) return "—";
  const text = Math.abs(n).toLocaleString("en-IN", {
    minimumFractionDigits: paise ? 2 : 0,
    maximumFractionDigits: paise ? 2 : 0
  });
  return (n < 0 ? "−₹" : "₹") + text;
}

function count(value) {
  const n = num(value);
  return n == null ? "—" : Math.round(n).toLocaleString("en-IN");
}

function percent(value, decimals = 0) {
  const n = num(value);
  return n == null ? "—" : n.toFixed(decimals) + "%";
}

function hours(value) {
  const n = num(value);
  if (n == null) return "—";
  return (Math.round(n * 10) / 10).toLocaleString("en-IN", { maximumFractionDigits: 1 }) + " h";
}

function day(dateString) {
  if (!dateString) return "—";
  const [y, m, d] = String(dateString).slice(0, 10).split("-").map(Number);
  return `${d} ${MONTHS[m - 1]} ${y}`;
}

function istParts(utc) {
  const date = new Date(new Date(utc).getTime() + IST_MS);
  return {
    y: date.getUTCFullYear(), m: date.getUTCMonth(), d: date.getUTCDate(),
    hh: String(date.getUTCHours()).padStart(2, "0"), mm: String(date.getUTCMinutes()).padStart(2, "0")
  };
}

function dateTime(utc) {
  if (!utc) return "—";
  const p = istParts(utc);
  return `${p.d} ${MONTHS[p.m]} ${p.y}, ${p.hh}:${p.mm}`;
}

function dateOfUtc(utc) {
  if (!utc) return "—";
  const p = istParts(utc);
  return `${p.d} ${MONTHS[p.m]} ${p.y}`;
}

function month(dateString) {
  if (!dateString) return "—";
  const [y, m] = String(dateString).slice(0, 7).split("-").map(Number);
  return `${MONTHS[m - 1]} ${y}`;
}

/** Any value by the type its column or chart declares. Unknown is a dash, never a zero. */
function fmt(value, type) {
  switch (type) {
    case "money": return money(value);
    case "percent": return percent(value, 1);
    case "hours": return hours(value);
    case "number":
    case "count": return count(value);
    case "date": return typeof value === "string" && value.length <= 10 ? day(value) : dateOfUtc(value);
    case "datetime": return dateTime(value);
    case "month": return month(value);
    default: return value == null || value === "" ? "—" : String(value);
  }
}

/** Short axis money: 10k, 2.4L, 1.2Cr (spec §3.3). */
function short(value, type) {
  if (type === "percent") return `${Math.round(value)}%`;
  const prefix = type === "money" ? "₹" : "";
  const n = Math.abs(value);
  if (n >= 1e7) return prefix + (value / 1e7).toFixed(n >= 1e8 ? 0 : 1).replace(/\.0$/, "") + "Cr";
  if (n >= 1e5) return prefix + (value / 1e5).toFixed(n >= 1e6 ? 0 : 1).replace(/\.0$/, "") + "L";
  if (n >= 1000) return prefix + (value / 1000).toFixed(n >= 1e4 ? 0 : 1).replace(/\.0$/, "") + "k";
  return prefix + String(Math.round(value * 10) / 10);
}

function istToday() {
  return new Date(Date.now() + IST_MS).toISOString().slice(0, 10);
}

function shiftDays(dateString, days) {
  const date = new Date(`${dateString}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

function fyStart(today) {
  const [y, m] = today.split("-").map(Number);
  return `${m >= 4 ? y : y - 1}-04-01`;
}

const ALL_TIME_START = "2020-04-01";

function presetRange(preset, defaultRange) {
  const today = istToday();
  switch (preset) {
    case "7d": return { from: shiftDays(today, -6), to: today };
    case "90d": return { from: shiftDays(today, -89), to: today };
    case "fy": return { from: fyStart(today), to: today };
    case "all": return { from: ALL_TIME_START, to: today };
    case "30d": return { from: shiftDays(today, -29), to: today };
    default: return defaultRange === "all" ? { from: ALL_TIME_START, to: today } : { from: shiftDays(today, -29), to: today };
  }
}

function presetOf(from, to, allowAll) {
  if (!from || !to || to !== istToday()) return "custom";
  for (const preset of allowAll ? ["all", "7d", "30d", "90d", "fy"] : ["7d", "30d", "90d", "fy"]) {
    const range = presetRange(preset);
    if (range.from === from) return preset;
  }
  return "custom";
}

function ago(utc) {
  if (!utc) return null;
  const minutes = Math.round((Date.now() - new Date(utc).getTime()) / 60000);
  if (minutes < 2) return "just now";
  if (minutes < 60) return `${minutes} minutes ago`;
  const hrs = Math.round(minutes / 60);
  if (hrs < 24) return `${hrs} hour${hrs === 1 ? "" : "s"} ago`;
  const days = Math.round(hrs / 24);
  return days === 1 ? "yesterday" : `${days} days ago`;
}

function friendlyNext(utc) {
  if (!utc) return null;
  const p = istParts(utc);
  const today = istToday();
  const key = `${p.y}-${String(p.m + 1).padStart(2, "0")}-${String(p.d).padStart(2, "0")}`;
  if (key === today) return `Today, ${p.hh}:${p.mm}`;
  if (key === shiftDays(today, 1)) return `Tomorrow, ${p.hh}:${p.mm}`;
  return `${p.d} ${MONTHS[p.m]}${p.y !== Number(today.slice(0, 4)) ? " " + p.y : ""}`;
}

// ── Styles ──────────────────────────────────────────────────────────────────

const STYLE_ID = "ra-styles";
const STYLES = `
.ra{--ra-c1:#B07215;--ra-c2:#2E6DB4;--ra-c3:#C2452A;--ra-c4:#0F7D55;--ra-c5:#9B3D8C;--ra-c0:#9A958C;
  --ra-h0:#F7EDD6;--ra-h1:#EAD5A4;--ra-h2:#D9B468;--ra-h3:#C2912C;--ra-h4:#9A6A12;
  --ra-up:#0F7D55;--ra-down:#C2452A;--ra-tip-bg:#1F1B16;--ra-tip-fg:#FFFFFF;--ra-ink:var(--text-primary);
  --ra-muted:var(--text-muted);--ra-line:var(--border-subtle);--ra-track:var(--surface-inset)}
[data-theme=dark] .ra{--ra-c1:#C98A2B;--ra-c2:#5A93D8;--ra-c3:#E0644B;--ra-c4:#2AA678;--ra-c5:#C065B2;--ra-c0:#8C897C;
  --ra-h0:#2B2519;--ra-h1:#4A3B1D;--ra-h2:#7A5C23;--ra-h3:#B0832C;--ra-h4:#E3AC3D;
  --ra-up:#3DBB8A;--ra-down:#EF7A63;--ra-tip-bg:#EFEDE5;--ra-tip-fg:#17171D}
.ra{display:flex;flex-direction:column;gap:var(--space-5);min-width:0}
.ra *{box-sizing:border-box}
.ra-head{display:flex;justify-content:space-between;align-items:flex-start;gap:var(--space-4);flex-wrap:wrap}
.ra-title{margin:0;font-family:var(--font-display);font-size:var(--font-size-2xl);color:var(--ra-ink);font-weight:700}
.ra-sub{margin:4px 0 0;color:var(--text-secondary);font-size:var(--font-size-sm);max-width:780px}
.ra-quiet{color:var(--ra-muted);font-size:var(--font-size-xs);font-style:italic}
.ra-controls{display:flex;align-items:center;gap:var(--space-3);flex-wrap:wrap}
.ra-seg{display:inline-flex;gap:2px;padding:3px;border-radius:var(--radius-full);background:var(--surface-inset);border:1px solid var(--border-subtle)}
.ra-seg button{border:0;background:transparent;border-radius:var(--radius-full);padding:6px 12px;font:inherit;font-size:var(--font-size-xs);font-weight:600;color:var(--text-secondary);cursor:pointer}
.ra-seg button[aria-pressed=true]{background:var(--text-primary);color:var(--surface-card)}
.ra-seg button:focus-visible,.ra-link:focus-visible,.ra-row:focus-visible{outline:2px solid var(--focus-ring, var(--accent));outline-offset:2px}
.ra-select,.ra-input{height:34px;border:1px solid var(--border-strong);border-radius:var(--radius-md);background:var(--surface-card);color:var(--ra-ink);font:inherit;font-size:var(--font-size-sm);padding:0 10px}
.ra-input--date{padding:0 6px}
.ra-check{display:inline-flex;align-items:center;gap:6px;font-size:var(--font-size-sm);color:var(--ra-ink);cursor:pointer;user-select:none}
.ra-check input{width:16px;height:16px;accent-color:var(--text-primary)}
.ra-card{background:var(--surface-card);background-image:var(--card-sheen);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);box-shadow:var(--shadow-raised);padding:var(--space-5);min-width:0;position:relative}
.ra-card__head{display:flex;justify-content:space-between;align-items:flex-start;gap:var(--space-3);margin-bottom:var(--space-4)}
.ra-card__title{margin:0;font-size:var(--font-size-md);font-weight:700;color:var(--ra-ink)}
.ra-card__sub{margin:2px 0 0;font-size:var(--font-size-xs);color:var(--ra-muted)}
.ra-card__tools{display:flex;gap:var(--space-2);align-items:center;flex-wrap:wrap}
.ra-note{margin:var(--space-3) 0 0;font-size:var(--font-size-sm);color:var(--text-secondary)}
.ra-link{background:none;border:0;padding:0;font:inherit;font-size:var(--font-size-sm);color:var(--accent-strong, var(--regalis-gold-dark));font-weight:600;cursor:pointer;text-decoration:none}
.ra-link:hover{text-decoration:underline}
.ra-tablebtn{background:none;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:4px 8px;font:inherit;font-size:var(--font-size-xs);color:var(--text-secondary);cursor:pointer;display:inline-flex;gap:4px;align-items:center}
.ra-grid4{display:grid;grid-template-columns:repeat(auto-fit,minmax(220px,1fr));gap:var(--space-4)}
.ra-grid2{display:grid;grid-template-columns:minmax(0,1.6fr) minmax(0,1fr);gap:var(--space-4)}
.ra-grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-4)}
@media (max-width:1100px){.ra-grid2,.ra-grid3{grid-template-columns:minmax(0,1fr)}}
.ra-kpi{display:flex;flex-direction:column;gap:6px;text-align:left;font:inherit;color:inherit;cursor:pointer;border:1px solid var(--border-subtle)}
.ra-kpi:hover{border-color:var(--border-strong)}
.ra-kpi__label{font-size:var(--font-size-sm);color:var(--text-secondary);font-weight:600}
.ra-kpi__value{font-size:28px;line-height:1.1;font-weight:700;color:var(--ra-ink);font-variant-numeric:tabular-nums}
.ra-kpi__prev{font-size:var(--font-size-xs);color:var(--ra-muted)}
.ra-change{font-size:var(--font-size-xs);font-weight:700;font-variant-numeric:tabular-nums}
.ra-change--up{color:var(--ra-up)}.ra-change--down{color:var(--ra-down)}.ra-change--none{color:var(--ra-muted);font-weight:600}
.ra-spark{display:block;margin-top:4px}
.ra-chart{position:relative;width:100%;min-width:0}
.ra-chart svg{display:block;overflow:visible}
.ra-grid-line{stroke:var(--ra-line);stroke-width:1}
.ra-axis{fill:var(--ra-muted);font-size:11px}
.ra-mark-label{fill:var(--ra-ink);font-size:11px;font-weight:600}
.ra-cross{stroke:var(--ra-muted);stroke-width:1;stroke-dasharray:3 3}
.ra-tip{position:absolute;pointer-events:none;background:var(--ra-tip-bg);color:var(--ra-tip-fg);border-radius:var(--radius-md);padding:6px 10px;font-size:12px;line-height:1.4;white-space:nowrap;box-shadow:var(--shadow-md);transform:translate(-50%,calc(-100% - 10px));z-index:5}
.ra-tip b{font-weight:700}
.ra-anim{animation:ra-in .35s ease-out both}
@keyframes ra-in{from{opacity:0;transform:translateY(4px)}to{opacity:1;transform:none}}
@media (prefers-reduced-motion:reduce){.ra-anim{animation:none}}
.ra-legend{display:flex;flex-wrap:wrap;gap:var(--space-3) var(--space-4);margin-bottom:var(--space-3);font-size:var(--font-size-xs);color:var(--ra-ink)}
.ra-legend span{display:inline-flex;align-items:center;gap:6px}
.ra-swatch{width:10px;height:10px;border-radius:2px;display:inline-block;flex:none}
.ra-hbars{display:flex;flex-direction:column;gap:var(--space-3)}
.ra-hbar__top{display:flex;justify-content:space-between;gap:var(--space-3);font-size:var(--font-size-sm);color:var(--ra-ink);margin-bottom:4px}
.ra-hbar__name{display:inline-flex;align-items:center;gap:6px;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.ra-hbar__vals{display:inline-flex;gap:var(--space-3);font-variant-numeric:tabular-nums;white-space:nowrap}
.ra-hbar__vals em{font-style:normal;color:var(--ra-muted);min-width:38px;text-align:right}
.ra-hbar__track{height:10px;border-radius:var(--radius-full);background:var(--ra-track);overflow:hidden}
.ra-hbar__fill{height:100%;border-radius:var(--radius-full);transition:width .35s ease-out}
.ra-hbar__sub{font-size:var(--font-size-xs);color:var(--ra-muted);margin-top:3px}
.ra-donut{display:flex;gap:var(--space-5);align-items:center;flex-wrap:wrap}
.ra-donut__legend{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:8px;font-size:var(--font-size-sm);min-width:160px;flex:1}
.ra-donut__legend li{display:flex;align-items:center;gap:8px}
.ra-donut__legend em{margin-left:auto;font-style:normal;font-variant-numeric:tabular-nums;color:var(--ra-ink);font-weight:600}
.ra-stackbar{display:flex;height:16px;border-radius:var(--radius-sm);overflow:hidden;gap:2px;background:var(--ra-track)}
.ra-stackbar__labels{display:flex;flex-wrap:wrap;gap:var(--space-3);margin-top:8px;font-size:var(--font-size-xs);color:var(--ra-ink)}
.ra-heat{display:grid;gap:3px;align-items:center}
.ra-heat__cell{height:26px;border-radius:4px;border:1px solid transparent}
.ra-heat__cell--closed{background:transparent;border:1px dashed var(--ra-line)}
.ra-heat__label{font-size:11px;color:var(--ra-muted);text-align:center}
.ra-heat__row{font-size:12px;color:var(--ra-ink);padding-right:6px}
.ra-heatwrap{display:grid;grid-template-columns:minmax(0,1fr) 150px;gap:var(--space-5);align-items:start}
@media (max-width:900px){.ra-heatwrap{grid-template-columns:minmax(0,1fr)}}
.ra-heatlegend{display:flex;flex-direction:column;gap:6px;font-size:var(--font-size-xs);color:var(--ra-ink)}
.ra-heatlegend span{display:flex;align-items:center;gap:8px}
.ra-skel{background:linear-gradient(90deg,var(--surface-inset),var(--surface-hover),var(--surface-inset));background-size:200% 100%;animation:ra-shine 1.4s linear infinite;border-radius:var(--radius-md)}
@keyframes ra-shine{to{background-position:-200% 0}}
.ra-empty{padding:var(--space-8) var(--space-4);text-align:center;color:var(--text-secondary);display:flex;flex-direction:column;align-items:center;gap:var(--space-3)}
.ra-error{border:1px solid var(--color-danger);background:color-mix(in srgb,var(--color-danger) 8%,transparent);color:var(--ra-ink);border-radius:var(--radius-md);padding:var(--space-3) var(--space-4);display:flex;justify-content:space-between;align-items:center;gap:var(--space-3)}
.ra-strip{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:var(--space-3)}
.ra-strip a{display:block;padding:var(--space-4);border:1px solid var(--border-subtle);border-radius:var(--radius-md);text-decoration:none;color:var(--ra-ink);background:var(--surface-card)}
.ra-strip a:hover{border-color:var(--border-strong)}
.ra-strip b{display:block;font-size:var(--font-size-sm);margin-bottom:4px}
.ra-strip small{color:var(--ra-muted);font-size:var(--font-size-xs);line-height:1.4;display:block}
.ra-mini{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.ra-mini th{font-size:var(--font-size-xs);color:var(--text-secondary);text-align:left;font-weight:700;padding:6px 8px;border-bottom:1px solid var(--ra-line)}
.ra-mini td{padding:7px 8px;border-bottom:1px solid var(--ra-line);color:var(--ra-ink)}
.ra-mini .num{text-align:right;font-variant-numeric:tabular-nums}
.ra-mini tr.ra-rest td{color:var(--text-secondary)}
.ra-crumb{display:flex;gap:6px;flex-wrap:wrap;font-size:var(--font-size-sm);color:var(--ra-muted)}
.ra-crumb a{color:var(--text-secondary);text-decoration:none}.ra-crumb a:hover{text-decoration:underline}
.ra-actions{display:flex;gap:var(--space-2);flex-wrap:wrap;align-items:center}
.ra-star[aria-pressed=true] svg{fill:var(--ra-c1);stroke:var(--ra-c1)}
.ra-filters{display:flex;flex-wrap:wrap;gap:var(--space-3);align-items:flex-end}
.ra-field{display:flex;flex-direction:column;gap:4px;min-width:0}
.ra-field>label,.ra-field>.ra-label{font-size:var(--font-size-xs);font-weight:700;color:var(--text-secondary)}
.ra-field__error{font-size:var(--font-size-xs);color:var(--color-danger)}
.ra-dates{display:flex;gap:6px;align-items:center;flex-wrap:wrap}
.ra-chips{display:flex;flex-wrap:wrap;gap:var(--space-2);margin-top:var(--space-3)}
.ra-chip{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--border-strong);border-radius:var(--radius-full);padding:3px 6px 3px 10px;font-size:var(--font-size-xs);color:var(--ra-ink);background:var(--surface-inset)}
.ra-chip button{border:0;background:none;padding:2px;cursor:pointer;color:var(--ra-muted);display:inline-flex;border-radius:50%}
.ra-chip button:hover{color:var(--ra-ink);background:var(--surface-hover)}
.ra-pop{position:relative}
.ra-pop__btn{height:34px;border:1px solid var(--border-strong);border-radius:var(--radius-md);background:var(--surface-card);color:var(--ra-ink);font:inherit;font-size:var(--font-size-sm);padding:0 10px;display:inline-flex;align-items:center;gap:8px;cursor:pointer;max-width:260px}
.ra-pop__btn span{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.ra-pop__panel{position:absolute;top:calc(100% + 6px);left:0;z-index:30;min-width:220px;max-height:320px;overflow:auto;background:var(--surface-card);border:1px solid var(--border-strong);border-radius:var(--radius-md);box-shadow:var(--shadow-lg);padding:var(--space-2)}
.ra-pop__panel--right{left:auto;right:0}
.ra-pop__item{display:flex;align-items:center;gap:8px;padding:6px 8px;border-radius:var(--radius-sm);font-size:var(--font-size-sm);color:var(--ra-ink);cursor:pointer}
.ra-pop__item:hover{background:var(--surface-hover)}
.ra-figures{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:var(--space-4)}
.ra-figure__label{font-size:var(--font-size-sm);color:var(--text-secondary);font-weight:600}
.ra-figure__value{font-size:24px;font-weight:700;color:var(--ra-ink);margin:4px 0;font-variant-numeric:tabular-nums}
.ra-figure__sub{font-size:var(--font-size-xs);color:var(--ra-muted)}
.ra-strip2{display:grid;grid-template-columns:minmax(0,1.7fr) minmax(0,1fr);gap:var(--space-4)}
@media (max-width:1100px){.ra-strip2{grid-template-columns:minmax(0,1fr)}}
.ra-side{display:flex;flex-direction:column;gap:var(--space-4)}
.ra-tablebar{display:flex;justify-content:space-between;gap:var(--space-3);align-items:center;flex-wrap:wrap;margin-bottom:var(--space-3)}
.ra-search{position:relative;display:inline-flex;align-items:center}
.ra-search svg{position:absolute;left:10px;color:var(--ra-muted)}
.ra-search input{padding-left:32px;width:260px;max-width:100%}
.ra-tablewrap{overflow-x:auto;border:1px solid var(--ra-line);border-radius:var(--radius-md);position:relative}
.ra-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.ra-table th{position:sticky;top:0;background:var(--surface-inset);font-size:var(--font-size-xs);text-align:left;color:var(--text-secondary);font-weight:700;padding:9px 12px;border-bottom:1px solid var(--ra-line);white-space:nowrap}
.ra-table th button{background:none;border:0;padding:0;font:inherit;color:inherit;cursor:pointer;display:inline-flex;gap:4px;align-items:center}
.ra-table td{padding:9px 12px;border-bottom:1px solid var(--ra-line);color:var(--ra-ink);white-space:nowrap;max-width:320px;overflow:hidden;text-overflow:ellipsis}
.ra-table .num{text-align:right;font-variant-numeric:tabular-nums}
.ra-table tbody tr{cursor:pointer}
.ra-table tbody tr:hover td{background:var(--surface-hover)}
.ra-table tfoot td{border-top:2px solid var(--text-primary);border-bottom:0;font-weight:700;background:var(--surface-card)}
.ra-pager{display:flex;justify-content:space-between;align-items:center;gap:var(--space-3);margin-top:var(--space-3);font-size:var(--font-size-sm);color:var(--text-secondary);flex-wrap:wrap}
.ra-overlay{position:fixed;inset:0;background:rgb(0 0 0 / 35%);z-index:60;display:flex;justify-content:flex-end}
.ra-overlay--center{justify-content:center;align-items:center;padding:var(--space-4)}
.ra-drawer{width:min(440px,100%);height:100%;background:var(--surface-card);box-shadow:var(--shadow-float);padding:var(--space-5);overflow:auto;display:flex;flex-direction:column;gap:var(--space-4)}
.ra-dialog{width:min(560px,100%);max-height:90vh;overflow:auto;background:var(--surface-card);border-radius:var(--radius-lg);box-shadow:var(--shadow-float);display:flex;flex-direction:column}
.ra-dialog__head{display:flex;justify-content:space-between;align-items:center;padding:var(--space-4) var(--space-5);border-bottom:1px solid var(--ra-line)}
.ra-dialog__head h3{margin:0;font-size:var(--font-size-lg);color:var(--ra-ink)}
.ra-dialog__body{padding:var(--space-5);display:flex;flex-direction:column;gap:var(--space-4)}
.ra-dialog__foot{padding:var(--space-4) var(--space-5);border-top:1px solid var(--ra-line);display:flex;justify-content:flex-end;gap:var(--space-2)}
.ra-close{background:none;border:0;cursor:pointer;color:var(--ra-muted);padding:4px;border-radius:var(--radius-sm);display:inline-flex}
.ra-close:hover{background:var(--surface-hover);color:var(--ra-ink)}
.ra-kv{display:grid;grid-template-columns:140px minmax(0,1fr);gap:8px 12px;font-size:var(--font-size-sm)}
.ra-kv dt{color:var(--text-secondary)}.ra-kv dd{margin:0;color:var(--ra-ink);word-break:break-word}
.ra-emails{display:flex;flex-wrap:wrap;gap:6px;border:1px solid var(--border-strong);border-radius:var(--radius-md);padding:6px;background:var(--surface-card)}
.ra-emails input{border:0;outline:0;background:transparent;color:var(--ra-ink);font:inherit;font-size:var(--font-size-sm);flex:1;min-width:180px;height:26px}
.ra-lib-search{display:flex;gap:var(--space-3);align-items:center;flex-wrap:wrap;justify-content:space-between}
.ra-lib-search .ra-search input{width:360px}
.ra-catchips{display:flex;flex-wrap:wrap;gap:var(--space-2)}
.ra-catchips button{border:1px solid var(--border-strong);background:var(--surface-card);color:var(--ra-ink);border-radius:var(--radius-full);padding:5px 12px;font:inherit;font-size:var(--font-size-xs);font-weight:600;cursor:pointer}
.ra-catchips button[aria-pressed=true]{background:var(--text-primary);color:var(--surface-card);border-color:var(--text-primary)}
.ra-catchips em{font-style:normal;opacity:.7;margin-left:4px}
.ra-fav{display:block;text-decoration:none;color:var(--ra-ink);border-left:4px solid var(--fav,var(--ra-c1));padding:var(--space-4);border-radius:var(--radius-md);background:var(--surface-card);border-top:1px solid var(--ra-line);border-right:1px solid var(--ra-line);border-bottom:1px solid var(--ra-line)}
.ra-fav:hover{border-right-color:var(--border-strong);border-top-color:var(--border-strong);border-bottom-color:var(--border-strong)}
.ra-fav b{display:block;margin-bottom:4px}
.ra-fav small{display:block;color:var(--text-secondary);font-size:var(--font-size-xs);line-height:1.4}
.ra-fav span{display:block;margin-top:8px;font-size:var(--font-size-xs);color:var(--ra-ink);font-weight:600}
.ra-group__head{display:flex;gap:10px;align-items:flex-start;margin-bottom:var(--space-3)}
.ra-dot{width:10px;height:10px;border-radius:50%;margin-top:6px;flex:none}
.ra-row{display:flex;justify-content:space-between;gap:var(--space-3);padding:10px 8px;border-top:1px solid var(--ra-line);text-decoration:none;color:var(--ra-ink);font-size:var(--font-size-sm);border-radius:var(--radius-sm)}
.ra-row:hover{background:var(--surface-hover)}
.ra-row em{font-style:normal;color:var(--text-secondary);font-size:var(--font-size-xs);text-align:right}
.ra-row em.ra-row__fig{color:var(--ra-ink);font-weight:600}
.ra-failed{color:var(--color-danger);font-size:var(--font-size-xs);margin-top:4px;white-space:normal}
.ra-stopped{color:var(--ra-muted)}
.ra-sr{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0);white-space:nowrap}
@media print{.portal__sidebar,.portal__topbar,.ra-controls,.ra-tablebtn{display:none!important}}
`;

function useStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }, []);
}

/** Palette slot → colour. Slot 0 is "Other" grey; the five are fixed (spec §10.1). */
const slotColour = (slot) => `var(--ra-c${slot >= 1 && slot <= 5 ? slot : 0})`;
const bandColour = (band) => (band === "high" ? "var(--ra-h4)" : band === "mid" ? "var(--ra-h3)" : "var(--ra-h1)");
const heatColour = (step) => (step < 0 ? "transparent" : `var(--ra-h${step})`);

// ── Small pieces ────────────────────────────────────────────────────────────

function useWidth(fallback = 640) {
  const ref = React.useRef(null);
  const [width, setWidth] = React.useState(fallback);
  React.useEffect(() => {
    const node = ref.current;
    if (!node) return;
    setWidth(Math.max(120, Math.round(node.getBoundingClientRect().width)) || fallback);
    if (typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver((entries) => {
      const next = Math.round(entries[0].contentRect.width);
      if (next > 0) setWidth(next);
    });
    observer.observe(node);
    return () => observer.disconnect();
  }, []);
  return [ref, width];
}

function useOutside(ref, onOutside, active) {
  React.useEffect(() => {
    if (!active) return;
    const handler = (event) => {
      if (ref.current && !ref.current.contains(event.target)) onOutside();
    };
    const key = (event) => {
      if (event.key === "Escape") onOutside();
    };
    document.addEventListener("mousedown", handler);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("mousedown", handler);
      document.removeEventListener("keydown", key);
    };
  }, [active]);
}

/** ▲ 12.4% green when better, ▼ red when worse; "no comparison" when before was zero. */
function Change({ kind, change, format, upIsGood = true }) {
  if (kind == null) return null;
  if (kind === "none") return h("span", { className: "ra-change ra-change--none" }, "no comparison");
  const up = change >= 0;
  const good = up === upIsGood;
  const text = kind === "percent" ? `${Math.abs(change).toFixed(1)}%` : fmt(Math.abs(change), format);
  return h(
    "span",
    { className: `ra-change ra-change--${good ? "up" : "down"}` },
    h("span", { "aria-hidden": true }, up ? "▲ " : "▼ "),
    h("span", { className: "ra-sr" }, up ? "Up " : "Down "),
    text
  );
}

function Skeleton({ height = 220 }) {
  return h("div", { className: "ra-skel", style: { height } });
}

/** Nice axis: three steps of 1 / 2 / 2.5 / 5 × 10ⁿ, from zero (spec §10.3). */
function niceAxis(max) {
  if (!(max > 0)) return { top: 3, step: 1 };
  const raw = max / 3;
  const magnitude = Math.pow(10, Math.floor(Math.log10(raw)));
  for (const s of [1, 2, 2.5, 5, 10]) {
    if (raw <= s * magnitude) return { top: 3 * s * magnitude, step: s * magnitude };
  }
  return { top: 30 * magnitude, step: 10 * magnitude };
}

function Tip({ x, y, children }) {
  return h("div", { className: "ra-tip", style: { left: x, top: y }, role: "status" }, children);
}

/** Catches a chart that fails to draw, so the rest of the page still works (spec §10.4). */
class ChartBoundary extends React.Component {
  constructor(props) {
    super(props);
    this.state = { failed: false };
  }
  static getDerivedStateFromError() {
    return { failed: true };
  }
  componentDidCatch(error) {
    console.error("Chart failed", error);
  }
  render() {
    if (!this.state.failed) return this.props.children;
    return h(
      "div",
      { className: "ra-empty" },
      "Could not load this chart",
      h("button", { type: "button", className: "ra-link", onClick: () => this.setState({ failed: false }) }, "Retry")
    );
  }
}

/** A chart card with a "View as table" switch for anyone who wants the numbers. */
function ChartCard({ title, subtitle, tools, chart, children, note, id, className }) {
  const [asTable, setAsTable] = React.useState(false);
  return h(
    "section",
    { className: "ra-card" + (className ? " " + className : ""), id },
    h(
      "div",
      { className: "ra-card__head" },
      h("div", null, h("h3", { className: "ra-card__title" }, title), subtitle ? h("p", { className: "ra-card__sub" }, subtitle) : null),
      h(
        "div",
        { className: "ra-card__tools" },
        tools,
        chart
          ? h(
              "button",
              { type: "button", className: "ra-tablebtn", onClick: () => setAsTable((v) => !v), "aria-pressed": asTable },
              h(asTable ? IconChart : IconTable, { size: 13 }),
              asTable ? "View as chart" : "View as table"
            )
          : null
      )
    ),
    h(ChartBoundary, null, asTable && chart ? h(ChartAsTable, { chart }) : children),
    note ? h("p", { className: "ra-note" }, note) : null
  );
}

/** The numbers behind any server-shaped chart. */
function ChartAsTable({ chart }) {
  const f = chart.valueFormat;
  let head = [];
  let rows = [];
  if (chart.kind === "heatmap") {
    head = ["", ...(chart.colLabels ?? [])];
    rows = (chart.rowLabels ?? []).map((label, r) => [label, ...(chart.cells?.[r] ?? []).map((c) => percent(c.percent))]);
  } else if (chart.kind === "stackedcolumns") {
    head = ["", ...(chart.series ?? []).map((s) => s.name), "Total"];
    rows = (chart.stacks ?? []).map((s) => [s.label, ...s.values.map((v) => fmt(v, f)), fmt(s.total, f)]);
  } else if (chart.slices) {
    head = ["", "Value", "Share"];
    rows = chart.slices.map((s) => [s.label, fmt(s.value, f), percent(s.share)]);
  } else {
    const prev = chart.previous ?? null;
    head = ["", "Value", ...(chart.value2Label ? [chart.value2Label] : []), ...(prev ? ["Previous"] : [])];
    rows = (chart.points ?? []).map((p, i) => [
      p.label,
      fmt(p.value, f),
      ...(chart.value2Label ? [fmt(p.value2, chart.value2Format)] : []),
      ...(prev ? [fmt(prev[i]?.value, f)] : [])
    ]);
  }
  return h(
    "div",
    { style: { maxHeight: 320, overflow: "auto" } },
    h(
      "table",
      { className: "ra-mini" },
      h("thead", null, h("tr", null, head.map((c, i) => h("th", { key: i, className: i ? "num" : undefined }, c)))),
      h("tbody", null, rows.map((r, i) => h("tr", { key: i }, r.map((c, j) => h("td", { key: j, className: j ? "num" : undefined }, c)))))
    )
  );
}

// ── Charts (spec §10) ───────────────────────────────────────────────────────

const PAD = { top: 18, right: 14, bottom: 28, left: 54 };

/** Line, or line with area when there is one series; the previous period dashed grey. */
function LineChart({ points, previous, format = "money", height = 240, tip }) {
  const [ref, width] = useWidth(700);
  const [hover, setHover] = React.useState(null);
  if (!points || points.length === 0) return h("div", { className: "ra-empty" }, "Nothing in this range.");
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const values = points.map((p) => num(p.value) ?? 0).concat((previous ?? []).map((p) => num(p.value) ?? 0));
  const { top, step } = niceAxis(Math.max(...values, 0));
  const stepX = points.length > 1 ? plotW / (points.length - 1) : 0;
  const x = (i) => PAD.left + (points.length > 1 ? i * stepX : plotW / 2);
  const y = (v) => PAD.top + plotH - ((num(v) ?? 0) / top) * plotH;
  const path = (series) => series.map((p, i) => `${i ? "L" : "M"}${x(i)} ${y(p.value)}`).join(" ");
  const line = path(points);
  const area = `${line} L${x(points.length - 1)} ${PAD.top + plotH} L${x(0)} ${PAD.top + plotH} Z`;
  const prev = previous ? previous.slice(0, points.length) : null;
  let highest = 0;
  points.forEach((p, i) => {
    if ((num(p.value) ?? 0) > (num(points[highest].value) ?? 0)) highest = i;
  });
  // Room for the widest label (about 6.5px a character at 11px), so labels never collide.
  const labelRoom = Math.max(60, Math.max(...points.map((p) => String(p.label).length)) * 6.5 + 14);
  const every = Math.max(1, Math.ceil(points.length / Math.max(2, Math.floor(plotW / labelRoom))));
  const onMove = (event) => {
    const box = event.currentTarget.getBoundingClientRect();
    const px = event.clientX - box.left;
    const i = Math.max(0, Math.min(points.length - 1, stepX ? Math.round((px - PAD.left) / stepX) : 0));
    setHover(i);
  };
  const label = (i) => (i === highest && (num(points[i].value) ?? 0) > 0) || i === points.length - 1;
  return h(
    "div",
    { className: "ra-chart", ref },
    h(
      "svg",
      {
        width: "100%", height, viewBox: `0 0 ${width} ${height}`, role: "img",
        "aria-label": "Line chart", onMouseMove: onMove, onMouseLeave: () => setHover(null)
      },
      [0, 1, 2, 3].map((t) =>
        h(
          "g",
          { key: t },
          h("line", { className: "ra-grid-line", x1: PAD.left, x2: PAD.left + plotW, y1: y(step * t), y2: y(step * t) }),
          h("text", { className: "ra-axis", x: PAD.left - 8, y: y(step * t) + 4, textAnchor: "end" }, short(step * t, format))
        )
      ),
      h(
        "g",
        { className: "ra-anim" },
        h("path", { d: area, fill: "var(--ra-c1)", fillOpacity: 0.12 }),
        prev && prev.length > 1
          ? h("path", { d: path(prev), fill: "none", stroke: "var(--ra-c0)", strokeWidth: 1.5, strokeDasharray: "5 4" })
          : null,
        h("path", { d: line, fill: "none", stroke: "var(--ra-c1)", strokeWidth: 2.25, strokeLinejoin: "round" })
      ),
      points.map((p, i) =>
        // Every nth label plus the last — but never one so close to the last that they overlap.
        (i % every === 0 && points.length - 1 - i >= every) || i === points.length - 1
          ? h("text", { key: "x" + i, className: "ra-axis", x: x(i), y: height - 8, textAnchor: i === 0 ? "start" : i === points.length - 1 ? "end" : "middle" }, p.label)
          : null
      ),
      points.map((p, i) =>
        label(i) && hover == null
          ? h("text", { key: "l" + i, className: "ra-mark-label", x: x(i), y: y(p.value) - 8, textAnchor: i === points.length - 1 ? "end" : "middle" }, short(num(p.value) ?? 0, format))
          : null
      ),
      hover != null
        ? h(
            "g",
            null,
            h("line", { className: "ra-cross", x1: x(hover), x2: x(hover), y1: PAD.top, y2: PAD.top + plotH }),
            h("circle", { cx: x(hover), cy: y(points[hover].value), r: 4.5, fill: "var(--ra-c1)", stroke: "var(--surface-card)", strokeWidth: 2 })
          )
        : null
    ),
    hover != null ? h(Tip, { x: x(hover), y: y(points[hover].value) }, tip ? tip(points[hover], prev?.[hover]) : `${points[hover].label} · ${fmt(points[hover].value, format)}`) : null
  );
}

/** One column per bucket; the busiest labelled above its column. */
function ColumnsChart({ chart, height = 220 }) {
  const [ref, width] = useWidth(640);
  const [hover, setHover] = React.useState(null);
  const points = chart.points ?? [];
  if (points.length === 0) return h("div", { className: "ra-empty" }, "Nothing in this range.");
  const f = chart.valueFormat;
  const plotW = Math.max(60, width - PAD.left - PAD.right);
  const plotH = height - PAD.top - PAD.bottom;
  const { top, step } = niceAxis(Math.max(...points.map((p) => num(p.value) ?? 0), 0));
  const slot = plotW / points.length;
  const bar = Math.max(2, Math.min(42, slot * 0.68));
  const y = (v) => PAD.top + plotH - ((num(v) ?? 0) / top) * plotH;
  const labelRoom = Math.max(56, Math.max(...points.map((p) => String(p.label).length)) * 6.5 + 12);
  const every = Math.max(1, Math.ceil(points.length / Math.max(2, Math.floor(plotW / labelRoom))));
  return h(
    "div",
    { className: "ra-chart", ref },
    h(
      "svg",
      { width: "100%", height, viewBox: `0 0 ${width} ${height}`, role: "img", "aria-label": chart.title },
      [0, 1, 2, 3].map((t) =>
        h(
          "g",
          { key: t },
          h("line", { className: "ra-grid-line", x1: PAD.left, x2: PAD.left + plotW, y1: y(step * t), y2: y(step * t) }),
          h("text", { className: "ra-axis", x: PAD.left - 8, y: y(step * t) + 4, textAnchor: "end" }, short(step * t, f))
        )
      ),
      h(
        "g",
        { className: "ra-anim" },
        points.map((p, i) => {
          const cx = PAD.left + slot * i + slot / 2;
          const value = num(p.value) ?? 0;
          return h(
            "g",
            { key: i, onMouseEnter: () => setHover(i), onMouseLeave: () => setHover(null) },
            h("rect", { x: PAD.left + slot * i, y: PAD.top, width: slot, height: plotH, fill: "transparent" }),
            h("rect", {
              x: cx - bar / 2, y: y(value), width: bar, height: Math.max(value > 0 ? 1 : 0, PAD.top + plotH - y(value)),
              rx: 2, fill: "var(--ra-c1)", opacity: hover == null || hover === i ? 1 : 0.55
            }),
            chart.highlightIndex === i && value > 0
              ? h("text", { className: "ra-mark-label", x: cx, y: y(value) - 6, textAnchor: "middle" }, short(value, f))
              : null
          );
        })
      ),
      points.map((p, i) =>
        i % every === 0
          ? h("text", { key: "x" + i, className: "ra-axis", x: PAD.left + slot * i + slot / 2, y: height - 8, textAnchor: "middle" }, p.label)
          : null
      )
    ),
    hover != null
      ? h(
          Tip,
          { x: PAD.left + slot * hover + slot / 2, y: y(points[hover].value) },
          h("b", null, points[hover].label),
          ` · ${fmt(points[hover].value, f)}`,
          chart.value2Label ? ` · ${fmt(points[hover].value2, chart.value2Format)} ${chart.value2Label}` : ""
        )
      : null
  );
}

/** Horizontal bars, sorted, value at the end; the track is always full width. */
function HBars({ chart, limit, showShare = true }) {
  const slices = (chart.slices ?? []).slice(0, limit ?? undefined);
  if (slices.length === 0) return h("div", { className: "ra-empty" }, "Nothing in this range.");
  const f = chart.valueFormat;
  const max = f === "percent" ? 100 : Math.max(...slices.map((s) => num(s.value) ?? 0), 0) || 1;
  return h(
    "div",
    { className: "ra-hbars ra-anim" },
    slices.map((s, i) =>
      h(
        "div",
        { key: s.label + i, title: `${s.label}: ${fmt(s.value, f)}${f !== "percent" && showShare ? ` (${percent(s.share)})` : ""}` },
        h(
          "div",
          { className: "ra-hbar__top" },
          h("span", { className: "ra-hbar__name" }, s.band ? null : h("i", { className: "ra-swatch", style: { background: slotColour(s.slot) } }), s.label),
          h(
            "span",
            { className: "ra-hbar__vals" },
            h("span", null, fmt(s.value, f)),
            f !== "percent" && showShare ? h("em", null, percent(s.share)) : null
          )
        ),
        h(
          "div",
          { className: "ra-hbar__track" },
          h("div", {
            className: "ra-hbar__fill",
            style: { width: `${Math.max(0, Math.min(100, ((num(s.value) ?? 0) / max) * 100))}%`, background: s.band ? bandColour(s.band) : slotColour(s.slot) }
          })
        ),
        s.sub ? h("div", { className: "ra-hbar__sub" }, s.sub) : null
      )
    )
  );
}

/** A doughnut for five slices or fewer, the total in the middle, its legend part of it. */
function Doughnut({ chart, size = 156 }) {
  const [hover, setHover] = React.useState(null);
  const slices = chart.slices ?? [];
  const total = slices.reduce((t, s) => t + Math.max(0, num(s.value) ?? 0), 0);
  if (total <= 0) return h("div", { className: "ra-empty" }, "Nothing in this range.");
  const r = size / 2 - 14;
  const c = size / 2;
  const gap = (2 / (2 * Math.PI * r)) * 360;
  let angle = -90;
  const arcs = slices.filter((s) => (num(s.value) ?? 0) > 0).map((s) => {
    const sweep = ((num(s.value) ?? 0) / total) * 360;
    const a0 = angle + (sweep < 359 ? gap / 2 : 0);
    const a1 = angle + sweep - (sweep < 359 ? gap / 2 : 0.01);
    angle += sweep;
    const pt = (deg) => [c + r * Math.cos((deg * Math.PI) / 180), c + r * Math.sin((deg * Math.PI) / 180)];
    const [x0, y0] = pt(a0);
    const [x1, y1] = pt(Math.max(a1, a0 + 0.5));
    return { s, d: `M${x0} ${y0} A${r} ${r} 0 ${a1 - a0 > 180 ? 1 : 0} 1 ${x1} ${y1}` };
  });
  const f = chart.valueFormat;
  return h(
    "div",
    { className: "ra-donut ra-anim" },
    h(
      "div",
      { style: { position: "relative", width: size, height: size, flex: "none" } },
      h(
        "svg",
        { width: size, height: size, viewBox: `0 0 ${size} ${size}`, role: "img", "aria-label": chart.title },
        arcs.map((a) =>
          h("path", {
            key: a.s.label, d: a.d, fill: "none", stroke: slotColour(a.s.slot), strokeWidth: hover === a.s.label ? 24 : 20,
            onMouseEnter: () => setHover(a.s.label), onMouseLeave: () => setHover(null)
          })
        ),
        h("text", { x: c, y: c - 2, textAnchor: "middle", style: { fontSize: 18, fontWeight: 700, fill: "var(--ra-ink)" } }, f === "money" ? short(chart.total ?? total, f) : fmt(chart.total ?? total, f)),
        h("text", { x: c, y: c + 16, textAnchor: "middle", className: "ra-axis" }, "in all")
      ),
      hover ? h(Tip, { x: size / 2, y: 12 }, `${hover} · ${fmt(slices.find((s) => s.label === hover)?.value, f)}`) : null
    ),
    h(
      "ul",
      { className: "ra-donut__legend" },
      slices.map((s) =>
        h("li", { key: s.label }, h("i", { className: "ra-swatch", style: { background: slotColour(s.slot) } }), s.label, h("em", null, `${fmt(s.value, f)} · ${percent(s.share)}`))
      )
    )
  );
}

/** One stacked bar with the percentages named beneath it. */
function StackedBar({ chart }) {
  const slices = (chart.slices ?? []).filter((s) => (num(s.value) ?? 0) > 0);
  if (slices.length === 0) return h("div", { className: "ra-empty" }, "Nothing in this range.");
  return h(
    "div",
    { className: "ra-anim" },
    h("div", { className: "ra-stackbar" }, slices.map((s) => h("div", { key: s.label, title: `${s.label}: ${fmt(s.value, chart.valueFormat)}`, style: { width: `${s.share}%`, background: slotColour(s.slot) } }))),
    h("div", { className: "ra-stackbar__labels" }, slices.map((s) => h("span", { key: s.label }, h("i", { className: "ra-swatch", style: { background: slotColour(s.slot), marginRight: 6 } }), `${s.label} ${percent(s.share)}`)))
  );
}

/** Stacked columns with the total above each; a 2px gap keeps segments apart in print. */
function StackedColumns({ chart, height = 240 }) {
  const [ref, width] = useWidth(700);
  const [hover, setHover] = React.useState(null);
  const stacks = chart.stacks ?? [];
  const series = chart.series ?? [];
  if (stacks.length === 0) return h("div", { className: "ra-empty" }, "Nothing in this range.");
  const f = chart.valueFormat;
  const plotW = Math.max(60, width - PAD.left - PAD.right - (series.length <= 4 ? 80 : 0));
  const plotH = height - PAD.top - PAD.bottom - 8;
  const top0 = PAD.top + 8;
  const { top, step } = niceAxis(Math.max(...stacks.map((s) => num(s.total) ?? 0), 0));
  const slot = plotW / stacks.length;
  const bar = Math.max(4, Math.min(46, slot * 0.6));
  const y = (v) => top0 + plotH - (v / top) * plotH;
  const labelRoom = Math.max(56, Math.max(...stacks.map((s) => String(s.label).length)) * 6.5 + 12);
  const every = Math.max(1, Math.ceil(stacks.length / Math.max(2, Math.floor(plotW / labelRoom))));
  const last = stacks.length - 1;
  return h(
    "div",
    null,
    h(
      "div",
      { className: "ra-legend" },
      series.map((s) => h("span", { key: s.name }, h("i", { className: "ra-swatch", style: { background: slotColour(s.slot) } }), s.name))
    ),
    h(
      "div",
      { className: "ra-chart", ref },
      h(
        "svg",
        { width: "100%", height, viewBox: `0 0 ${width} ${height}`, role: "img", "aria-label": chart.title },
        [0, 1, 2, 3].map((t) =>
          h(
            "g",
            { key: t },
            h("line", { className: "ra-grid-line", x1: PAD.left, x2: PAD.left + plotW, y1: y(step * t), y2: y(step * t) }),
            h("text", { className: "ra-axis", x: PAD.left - 8, y: y(step * t) + 4, textAnchor: "end" }, short(step * t, f))
          )
        ),
        h(
          "g",
          { className: "ra-anim" },
          stacks.map((stack, i) => {
            const cx = PAD.left + slot * i + slot / 2;
            let base = 0;
            const segments = stack.values.map((v, j) => {
              const value = num(v) ?? 0;
              const y0 = y(base);
              const y1 = y(base + value);
              base += value;
              if (value <= 0 || !series[j]) return null;
              const segH = Math.max(0, y0 - y1 - 2);
              return h(
                "g",
                { key: j },
                h("rect", { x: cx - bar / 2, y: y1, width: bar, height: segH, fill: slotColour(series[j].slot) }),
                // Four or fewer series are also labelled directly, beside the last column.
                i === last && series.length <= 4 && segH >= 14
                  ? h("text", { className: "ra-axis", x: cx + bar / 2 + 6, y: y1 + segH / 2 + 4 }, series[j].name)
                  : null
              );
            });
            return h(
              "g",
              { key: i, onMouseEnter: () => setHover(i), onMouseLeave: () => setHover(null) },
              h("rect", { x: PAD.left + slot * i, y: top0, width: slot, height: plotH, fill: "transparent" }),
              segments,
              (num(stack.total) ?? 0) > 0
                ? h("text", { className: "ra-mark-label", x: cx, y: y(num(stack.total)) - 6, textAnchor: "middle" }, short(num(stack.total), f))
                : null
            );
          })
        ),
        stacks.map((s, i) =>
          i % every === 0
            ? h("text", { key: "x" + i, className: "ra-axis", x: PAD.left + slot * i + slot / 2, y: height - 8, textAnchor: "middle" }, s.label)
            : null
        )
      ),
      hover != null
        ? h(
            Tip,
            { x: PAD.left + slot * hover + slot / 2, y: y(num(stacks[hover].total) ?? 0) },
            h("b", null, stacks[hover].label),
            ` · ${fmt(stacks[hover].total, f)} in all`,
            series.map((s, j) => h("div", { key: j }, `${s.name}: ${fmt(stacks[hover].values[j], f)}`))
          )
        : null
    )
  );
}

const HEAT_STEPS = ["0–20%", "20–45%", "45–65%", "65–80%", "80%+"];

/** Weekday × hour; one warm colour in five steps — never a rainbow (spec §3.6). */
function Heatmap({ chart }) {
  const [hover, setHover] = React.useState(null);
  const rows = chart.rowLabels ?? [];
  const cols = chart.colLabels ?? [];
  const cells = chart.cells ?? [];
  if (rows.length === 0 || cols.length === 0) return h("div", { className: "ra-empty" }, "No opening hours to show.");
  const every = cols.length > 12 ? 2 : 1;
  return h(
    "div",
    { className: "ra-heatwrap" },
    h(
      "div",
      { style: { position: "relative", overflowX: "auto" } },
      h(
        "div",
        { className: "ra-heat ra-anim", style: { gridTemplateColumns: `48px repeat(${cols.length}, minmax(22px, 1fr))`, minWidth: 48 + cols.length * 26 } },
        h("span", null),
        cols.map((c, i) => h("span", { key: "c" + i, className: "ra-heat__label" }, i % every === 0 ? c : "")),
        rows.map((r, ri) =>
          h(
            Frag,
            { key: r },
            h("span", { className: "ra-heat__row" }, r),
            cols.map((c, ci) => {
              const cell = cells[ri]?.[ci] ?? { percent: null, people: 0, step: -1 };
              const closed = cell.step < 0;
              return h("span", {
                key: c,
                className: "ra-heat__cell" + (closed ? " ra-heat__cell--closed" : ""),
                style: { background: heatColour(cell.step), outline: hover && hover.r === ri && hover.c === ci ? "2px solid var(--ra-ink)" : undefined },
                title: closed ? `${r} ${c}: closed` : `${r} ${c}: ${percent(cell.percent)} · ${count(cell.people)} people`,
                onMouseEnter: () => setHover({ r: ri, c: ci }),
                onMouseLeave: () => setHover(null)
              });
            })
          )
        )
      ),
      hover
        ? h(
            "div",
            { className: "ra-note", "aria-live": "polite" },
            (() => {
              const cell = cells[hover.r]?.[hover.c];
              return !cell || cell.step < 0
                ? `${rows[hover.r]} ${cols[hover.c]} — closed`
                : `${rows[hover.r]} ${cols[hover.c]} — ${percent(cell.percent)} occupied, about ${count(cell.people)} people`;
            })()
          )
        : null
    ),
    h(
      "div",
      { className: "ra-heatlegend" },
      HEAT_STEPS.map((label, i) => h("span", { key: label }, h("i", { className: "ra-swatch", style: { width: 18, height: 14, background: heatColour(i) } }), label)),
      chart.note ? h("p", { className: "ra-note", style: { marginTop: 8 } }, chart.note) : null
    )
  );
}

function Sparkline({ values, slot, width = 160, height = 34 }) {
  const list = (values ?? []).map((v) => num(v));
  if (list.length < 2) return h("div", { style: { height } });
  const max = Math.max(...list.map((v) => v ?? 0), 0);
  const step = width / (list.length - 1);
  const path = list.map((v, i) => `${i ? "L" : "M"}${(i * step).toFixed(1)} ${(height - 3 - ((v ?? 0) / (max || 1)) * (height - 6)).toFixed(1)}`).join(" ");
  return h("svg", { className: "ra-spark", width: "100%", height, viewBox: `0 0 ${width} ${height}`, preserveAspectRatio: "none", "aria-hidden": true }, h("path", { d: path, fill: "none", stroke: slotColour(slot), strokeWidth: 2, vectorEffect: "non-scaling-stroke" }));
}

/** Draws any server-shaped chart by its kind. */
function AnyChart({ chart }) {
  switch (chart.kind) {
    case "columns": return h(ColumnsChart, { chart });
    case "line":
      return h(LineChart, {
        points: chart.points, previous: chart.previous, format: chart.valueFormat,
        tip: (p, prev) => [h("b", { key: "a" }, p.label), ` · ${fmt(p.value, chart.valueFormat)}`, prev ? ` (${prev.label}: ${fmt(prev.value, chart.valueFormat)})` : ""]
      });
    case "hbars": return h(HBars, { chart });
    case "doughnut": return h(Doughnut, { chart });
    case "stackedbar": return h(StackedBar, { chart });
    case "stackedcolumns": return h(StackedColumns, { chart });
    case "heatmap": return h(Heatmap, { chart });
    default: return null;
  }
}

// ── Controls ────────────────────────────────────────────────────────────────

function Seg({ options, value, onChange, label }) {
  return h(
    "div",
    { className: "ra-seg", role: "group", "aria-label": label },
    options.map((o) => h("button", { key: o.value, type: "button", "aria-pressed": value === o.value, onClick: () => onChange(o.value) }, o.label))
  );
}

const PRESETS = [
  { value: "7d", label: "7d" },
  { value: "30d", label: "30d" },
  { value: "90d", label: "90d" },
  { value: "fy", label: "FY" },
  { value: "custom", label: "Custom" }
];

/** Custom range in a small panel with Apply and Cancel (spec §3.1). */
function CustomRange({ from, to, onApply, onCancel }) {
  const [a, setA] = React.useState(from);
  const [b, setB] = React.useState(to);
  const ref = React.useRef(null);
  useOutside(ref, onCancel, true);
  const bad = a && b && a > b;
  return h(
    "div",
    { className: "ra-pop__panel ra-pop__panel--right", ref, style: { padding: 12, minWidth: 300 } },
    h(
      "div",
      { className: "ra-dates" },
      h("input", { type: "date", className: "ra-input ra-input--date", value: a, max: istToday(), onChange: (e) => setA(e.target.value), "aria-label": "From" }),
      "to",
      h("input", { type: "date", className: "ra-input ra-input--date", value: b, max: istToday(), onChange: (e) => setB(e.target.value), "aria-label": "To" })
    ),
    bad ? h("div", { className: "ra-field__error", style: { marginTop: 6 } }, "The start date is after the end date.") : null,
    h(
      "div",
      { style: { display: "flex", justifyContent: "flex-end", gap: 8, marginTop: 10 } },
      h(Button, { variant: "ghost", size: "sm", onClick: onCancel }, "Cancel"),
      h(Button, { size: "sm", disabled: !a || !b || bad, onClick: () => onApply(a, b) }, "Apply")
    )
  );
}

function MultiSelect({ options, value, onChange, allLabel, label }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  useOutside(ref, () => setOpen(false), open);
  const chosen = options.filter((o) => value.includes(o.value));
  const text = chosen.length === 0 ? allLabel ?? "All" : chosen.map((o) => o.label).join(", ");
  const toggle = (v) => onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);
  return h(
    "div",
    { className: "ra-pop", ref },
    h("button", { type: "button", className: "ra-pop__btn", "aria-haspopup": "listbox", "aria-expanded": open, "aria-label": label, onClick: () => setOpen((v) => !v) }, h("span", null, text), h(IconChevronDown, { size: 14 })),
    open
      ? h(
          "div",
          { className: "ra-pop__panel", role: "listbox", "aria-multiselectable": true },
          h("label", { className: "ra-pop__item" }, h("input", { type: "checkbox", checked: value.length === 0, onChange: () => onChange([]) }), allLabel ?? "All"),
          options.map((o) =>
            h("label", { key: o.value, className: "ra-pop__item" }, h("input", { type: "checkbox", checked: value.includes(o.value), onChange: () => toggle(o.value) }), o.label)
          )
        )
      : null
  );
}

function CustomerPicker({ value, label, onChange }) {
  const [text, setText] = React.useState(label ?? "");
  const [options, setOptions] = React.useState([]);
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  useOutside(ref, () => setOpen(false), open);
  React.useEffect(() => setText(label ?? ""), [label]);
  React.useEffect(() => {
    if (!open || text.trim().length < 2 || (value && text === label)) return;
    const timer = setTimeout(() => {
      api.customers(text.trim()).then(setOptions).catch(() => setOptions([]));
    }, 250);
    return () => clearTimeout(timer);
  }, [text, open]);
  return h(
    "div",
    { className: "ra-pop", ref },
    h("input", {
      className: "ra-input", style: { width: 220 }, placeholder: "Name, company, phone or email", value: text, "aria-label": "Customer",
      onFocus: () => setOpen(true),
      onChange: (e) => {
        setText(e.target.value);
        setOpen(true);
        if (!e.target.value) onChange(null, null);
      }
    }),
    open && options.length > 0
      ? h(
          "div",
          { className: "ra-pop__panel" },
          options.map((o) =>
            h(
              "div",
              {
                key: o.customerId, className: "ra-pop__item", role: "option", tabIndex: 0,
                onClick: () => { onChange(String(o.customerId), o.name); setText(o.name); setOpen(false); },
                onKeyDown: (e) => { if (e.key === "Enter") { onChange(String(o.customerId), o.name); setText(o.name); setOpen(false); } }
              },
              h("div", null, h("div", null, o.name), h("small", { style: { color: "var(--ra-muted)" } }, [o.company, o.mobile, o.email].filter(Boolean).join(" · ")))
            )
          )
        )
      : null
  );
}

function Overlay({ center, label, onClose, children }) {
  React.useEffect(() => {
    const onKey = (e) => { if (e.key === "Escape") onClose(); };
    document.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [onClose]);
  return h("div", { className: "ra-overlay" + (center ? " ra-overlay--center" : ""), role: "dialog", "aria-modal": true, "aria-label": label, onMouseDown: (e) => { if (e.target === e.currentTarget) onClose(); } }, children);
}

// ── Schedule dialog (spec §12.1) ────────────────────────────────────────────

const WEEKDAYS = ["Sunday", "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday"];
const TIMES = Array.from({ length: 48 }, (_, i) => `${String(Math.floor(i / 2)).padStart(2, "0")}:${i % 2 ? "30" : "00"}`);

function ordinal(n) {
  return n === 1 || n === 21 ? `${n}st` : n === 2 || n === 22 ? `${n}nd` : n === 3 || n === 23 ? `${n}rd` : `${n}th`;
}

function ScheduleDialog({ reportKey, reportName, filters, existing, onClose, onSaved }) {
  const toast = useToast();
  const me = useAuth((s) => s.user)?.email ?? "";
  const [frequency, setFrequency] = React.useState(existing?.frequency ?? "weekly");
  const [dayOfWeek, setDayOfWeek] = React.useState(existing?.dayOfWeek ?? 1);
  const [dayOfMonth, setDayOfMonth] = React.useState(existing?.dayOfMonth ?? 1);
  const [time, setTime] = React.useState(existing?.time ?? "09:00");
  const [recipients, setRecipients] = React.useState(existing?.recipients ?? (me ? [me] : []));
  const [draft, setDraft] = React.useState("");
  const [format, setFormat] = React.useState(existing?.format ?? "xlsx");
  const [onlyIfData, setOnlyIfData] = React.useState(existing?.onlyIfData ?? false);
  const [errors, setErrors] = React.useState({});
  const [busy, setBusy] = React.useState(false);

  const addEmail = (raw) => {
    const parts = raw.split(/[,;\s]+/).map((p) => p.trim()).filter(Boolean);
    if (parts.length === 0) return;
    setRecipients((list) => [...list, ...parts.filter((p) => !list.includes(p))]);
    setDraft("");
  };
  const covers = frequency === "daily" ? "yesterday" : frequency === "weekly" ? "the previous week, Monday to Sunday" : "the previous month";
  const save = async () => {
    const pending = draft.trim() ? [...recipients, draft.trim()] : recipients;
    setBusy(true);
    setErrors({});
    const body = {
      reportKey,
      filters: existing ? undefined : filters,
      frequency,
      dayOfWeek: frequency === "weekly" ? Number(dayOfWeek) : null,
      dayOfMonth: frequency === "monthly" ? Number(dayOfMonth) : null,
      time,
      recipients: pending,
      format,
      onlyIfData
    };
    if (existing) body.filters = existing.filters;
    try {
      const saved = existing ? await api.updateSchedule(existing.scheduleId, body) : await api.createSchedule(body);
      toast.success(`${reportName} will email itself ${saved.howOften.toLowerCase()}. Next: ${friendlyNext(saved.nextRunUtc) ?? "—"}.`);
      onSaved?.(saved);
      onClose();
    } catch (error) {
      setErrors(fieldErrors(error));
      toast.error(errorText(error, "The schedule could not be saved."));
    } finally {
      setBusy(false);
    }
  };
  return h(
    Overlay,
    { center: true, label: "Email this report", onClose },
    h(
      "div",
      { className: "ra-dialog" },
      h("div", { className: "ra-dialog__head" }, h("h3", null, existing ? `Edit schedule — ${reportName}` : `Email ${reportName}`), h("button", { type: "button", className: "ra-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 18 }))),
      h(
        "div",
        { className: "ra-dialog__body" },
        h(
          "div",
          { className: "ra-field" },
          h("span", { className: "ra-label" }, "How often"),
          h(Seg, { label: "How often", value: frequency, onChange: setFrequency, options: [{ value: "daily", label: "Daily" }, { value: "weekly", label: "Weekly" }, { value: "monthly", label: "Monthly" }] }),
          errors.frequency ? h("span", { className: "ra-field__error" }, errors.frequency) : null
        ),
        h(
          "div",
          { style: { display: "flex", gap: 12, flexWrap: "wrap" } },
          frequency === "weekly"
            ? h("div", { className: "ra-field" }, h("label", { htmlFor: "ra-dow" }, "Day"), h("select", { id: "ra-dow", className: "ra-select", value: dayOfWeek, onChange: (e) => setDayOfWeek(Number(e.target.value)) }, WEEKDAYS.map((d, i) => h("option", { key: d, value: i }, d))), errors.dayOfWeek ? h("span", { className: "ra-field__error" }, errors.dayOfWeek) : null)
            : null,
          frequency === "monthly"
            ? h("div", { className: "ra-field" }, h("label", { htmlFor: "ra-dom" }, "Date"), h("select", { id: "ra-dom", className: "ra-select", value: dayOfMonth, onChange: (e) => setDayOfMonth(Number(e.target.value)) }, Array.from({ length: 28 }, (_, i) => h("option", { key: i + 1, value: i + 1 }, `The ${ordinal(i + 1)}`)), h("option", { value: 0 }, "The last day of the month")), errors.dayOfMonth ? h("span", { className: "ra-field__error" }, errors.dayOfMonth) : null)
            : null,
          h("div", { className: "ra-field" }, h("label", { htmlFor: "ra-time" }, "Time (IST)"), h("select", { id: "ra-time", className: "ra-select", value: time, onChange: (e) => setTime(e.target.value) }, TIMES.map((t) => h("option", { key: t, value: t }, t))), errors.time ? h("span", { className: "ra-field__error" }, errors.time) : null)
        ),
        h(
          "div",
          { className: "ra-field" },
          h("label", { htmlFor: "ra-to" }, "Send to"),
          h(
            "div",
            { className: "ra-emails" },
            recipients.map((r) => h("span", { key: r, className: "ra-chip" }, r === me ? `${r} (you)` : r, h("button", { type: "button", "aria-label": `Remove ${r}`, onClick: () => setRecipients((list) => list.filter((x) => x !== r)) }, h(IconX, { size: 12 })))),
            h("input", {
              id: "ra-to", value: draft, placeholder: recipients.length ? "Add another address" : "name@company.com",
              onChange: (e) => setDraft(e.target.value),
              onKeyDown: (e) => { if (e.key === "Enter" || e.key === "," || e.key === ";") { e.preventDefault(); addEmail(draft); } },
              onBlur: () => addEmail(draft)
            })
          ),
          h("span", { className: "ra-card__sub" }, "Anyone can receive it, including people who are not Regalis admins."),
          errors.recipients ? h("span", { className: "ra-field__error" }, errors.recipients) : null
        ),
        h(
          "div",
          { className: "ra-field" },
          h("span", { className: "ra-label" }, "Format"),
          h(Seg, { label: "Format", value: format, onChange: setFormat, options: [{ value: "xlsx", label: "Excel" }, { value: "pdf", label: "PDF" }] })
        ),
        h("div", { className: "ra-field" }, h("span", { className: "ra-label" }, "Which dates"), h("span", { style: { fontSize: "var(--font-size-sm)", color: "var(--ra-ink)" } }, `Rolling — each email covers ${covers}. The other filters stay as they are now.`)),
        h("label", { className: "ra-check" }, h("input", { type: "checkbox", checked: onlyIfData, onChange: (e) => setOnlyIfData(e.target.checked) }), "Only if there is data — send nothing instead of an empty file")
      ),
      h("div", { className: "ra-dialog__foot" }, h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { onClick: save, loading: busy }, existing ? "Save schedule" : "Start emailing"))
    )
  );
}

// ── Screen 1: the overview (spec §3) ────────────────────────────────────────

const SECTION_ANCHORS = { revenue: "ra-revenue", occupancy: "ra-busy", customers: "ra-customers" };

export function ReportsOverviewPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const { section } = useParams();
  const [query, setQuery] = React.useState(null);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [failed, setFailed] = React.useState(false);
  const [grouping, setGrouping] = React.useState(null);
  const [custom, setCustom] = React.useState(false);
  const [exporting, setExporting] = React.useState(false);

  const load = React.useCallback(async (params) => {
    setLoading(true);
    setFailed(false);
    try {
      const result = await api.overview(params ?? {});
      setData(result);
      setGrouping(null);
      setQuery({ preset: result.preset, from: result.from, to: result.to, locationId: result.locationId ?? "", compare: result.compare });
    } catch (error) {
      setFailed(true);
      if (error instanceof ApiError && error.status === 403) toast.error("You do not have access to Reports & Analysis.");
    } finally {
      setLoading(false);
    }
  }, []);

  // No parameters on the first call: the server opens on this admin's last choice.
  React.useEffect(() => { load(null); }, [load]);

  React.useEffect(() => {
    if (!data || !section || !SECTION_ANCHORS[section]) return;
    const node = document.getElementById(SECTION_ANCHORS[section]);
    if (node) node.scrollIntoView({ behavior: "smooth", block: "start" });
  }, [data, section]);

  const change = (patch) => {
    const next = { ...query, ...patch };
    // The control moves at once; the figures follow when the response arrives.
    setQuery(next);
    const params = { preset: next.preset, compare: next.compare, locationId: next.locationId || 0 };
    if (next.preset === "custom") { params.from = next.from; params.to = next.to; }
    load(params);
  };

  const exportSummary = async () => {
    setExporting(true);
    try {
      const params = { preset: query.preset, compare: query.compare, locationId: query.locationId || 0 };
      if (query.preset === "custom") { params.from = query.from; params.to = query.to; }
      await api.overviewPdf(params);
    } catch (error) {
      toast.error(errorText(error, "The summary could not be exported."));
    } finally {
      setExporting(false);
    }
  };

  const openReport = (key, extra) => {
    const filters = { from: data.from, to: data.to };
    if (data.locationId) filters.location = [String(data.locationId)];
    navigate(`/admin/reports/${key}`, { state: { filters: { ...filters, ...(extra ?? {}) } } });
  };

  if (failed && !data) {
    return h("div", { className: "ra" }, h("div", { className: "ra-error", role: "alert" }, "Could not load the reports.", h(Button, { variant: "outline", size: "sm", onClick: () => load(null) }, "Retry")));
  }

  const d = data;
  const revenueSeries = d?.revenue;
  const activeGrouping = grouping ?? revenueSeries?.defaultGrouping ?? "daily";
  const series = revenueSeries?.current?.[activeGrouping] ?? [];
  const previous = revenueSeries?.previous?.[activeGrouping] ?? [];

  return h(
    "div",
    { className: "ra" },
    h(
      "div",
      { className: "ra-head" },
      h(
        "div",
        null,
        h("h1", { className: "ra-title" }, "Reports & analysis"),
        h("p", { className: "ra-sub" }, d ? d.periodText : " "),
        d?.calculatedLive ? h("span", { className: "ra-quiet" }, "Figures calculated live.") : null
      ),
      query
        ? h(
            "div",
            { className: "ra-controls" },
            h(
              "div",
              { className: "ra-pop" },
              h(Seg, {
                label: "Date range", options: PRESETS, value: query.preset,
                onChange: (v) => (v === "custom" ? setCustom(true) : change({ preset: v }))
              }),
              custom
                ? h(CustomRange, {
                    from: query.from, to: query.to, onCancel: () => setCustom(false),
                    onApply: (from, to) => { setCustom(false); change({ preset: "custom", from, to }); }
                  })
                : null
            ),
            d && d.locations.length > 1
              ? h(
                  "select",
                  { className: "ra-select", "aria-label": "Location", value: query.locationId ?? "", onChange: (e) => change({ locationId: e.target.value }) },
                  h("option", { value: "" }, "All locations"),
                  d.locations.map((l) => h("option", { key: l.value, value: l.value }, l.label))
                )
              : null,
            h("label", { className: "ra-check" }, h("input", { type: "checkbox", checked: query.compare, onChange: (e) => change({ compare: e.target.checked }) }), "Compare"),
            d?.canExport ? h(Button, { variant: "outline", onClick: exportSummary, loading: exporting }, h(IconDownload, { size: 15 }), " Export summary") : null
          )
        : null
    ),
    failed ? h("div", { className: "ra-error", role: "alert" }, "Could not load the reports.", h(Button, { variant: "outline", size: "sm", onClick: () => change({}) }, "Retry")) : null,

    d && !d.hasData && !loading
      ? h(
          "div",
          { className: "ra-card ra-empty" },
          `Nothing happened between these dates (${day(d.from)} – ${day(d.to)}).`,
          h(Button, { variant: "outline", onClick: () => change({ preset: "90d" }) }, "Show the last 90 days")
        )
      : null,

    // Four headline cards — three when revenue is not this admin's to see, closed up.
    h(
      "div",
      { className: "ra-grid4", id: "ra-customers-cards" },
      loading && !d
        ? [0, 1, 2, 3].map((i) => h("div", { key: i, className: "ra-card" }, h(Skeleton, { height: 118 })))
        : d.cards.map((card) =>
            h(
              "button",
              { key: card.key, type: "button", className: "ra-card ra-kpi", onClick: () => openReport(card.reportKey), style: { opacity: loading ? 0.6 : 1 } },
              h("span", { className: "ra-kpi__label" }, card.label),
              // Occupancy is a whole percentage on the card (spec §3.2).
              h("span", { className: "ra-kpi__value" }, card.format === "percent" ? percent(card.value) : fmt(card.value, card.format)),
              d.compare ? h(Change, { kind: card.changeKind, change: card.change, format: card.format }) : null,
              d.compare ? h("span", { className: "ra-kpi__prev" }, card.previousText) : null,
              h(Sparkline, { values: card.sparkline, slot: card.slot })
            )
          )
    ),

    d?.revenue || (loading && !d)
      ? h(
          "div",
          { className: "ra-grid2", id: "ra-revenue" },
          h(
            ChartCard,
            {
              title: "Revenue over time",
              subtitle: d?.compare ? "Money received each day; the dashed line is the previous period" : "Money received, net of refunds",
              tools: d?.revenue ? h(Seg, { label: "Grouping", value: activeGrouping, onChange: setGrouping, options: [{ value: "daily", label: "Daily" }, { value: "weekly", label: "Weekly" }, { value: "monthly", label: "Monthly" }] }) : null,
              chart: d?.revenue ? { kind: "line", valueFormat: "money", points: series.map((p) => ({ label: p.label, value: p.revenue, value2: p.bookings })), previous: d.compare ? previous.map((p) => ({ label: p.label, value: p.revenue })) : null, value2Label: "bookings", value2Format: "count" } : null
            },
            !d ? h(Skeleton, { height: 240 })
              : h(LineChart, {
                  points: series.map((p) => ({ label: p.label, value: p.revenue, bookings: p.bookings, date: p.date })),
                  previous: d.compare ? previous.map((p) => ({ label: p.label, value: p.revenue })) : null,
                  format: "money",
                  tip: (p, prev) => [h("b", { key: "a" }, activeGrouping === "daily" ? day(p.date) : p.label), ` · ${money(p.value)} · ${count(p.bookings)} booking${p.bookings === 1 ? "" : "s"}`, prev ? h("div", { key: "b", style: { opacity: 0.75 } }, `Before: ${prev.label}, ${money(prev.value)}`) : null]
                })
          ),
          h(
            ChartCard,
            { title: "Revenue by category", subtitle: "Where the money came from, as a share of the period total", chart: d?.revenueByCategory, note: d?.biggestMover },
            d?.revenueByCategory ? h(HBars, { chart: d.revenueByCategory }) : h(Skeleton, { height: 240 })
          )
        )
      : null,

    h(
      ChartCard,
      { title: d?.bookingsByType.title ?? "Bookings by type", subtitle: d?.bookingsByType.subtitle, chart: d?.bookingsByType },
      d ? h(StackedColumns, { chart: d.bookingsByType }) : h(Skeleton, { height: 260 })
    ),

    h(
      ChartCard,
      { title: "When the space is busy", subtitle: "Occupancy by weekday and hour — the darker the cell, the busier the hour", chart: d?.heatmap, id: "ra-busy" },
      d ? h(Heatmap, { chart: d.heatmap }) : h(Skeleton, { height: 220 })
    ),

    h(
      "div",
      { className: "ra-grid3", id: "ra-customers" },
      h(
        ChartCard,
        {
          title: "Busiest workspaces", subtitle: "Hours booked out of hours available", chart: d?.busiest,
          tools: h("button", { type: "button", className: "ra-link", onClick: () => openReport("desk-occupancy") }, "See all"),
          note: d?.busiestNote
        },
        d ? h(HBars, { chart: d.busiest, limit: 6 }) : h(Skeleton, { height: 220 })
      ),
      d?.moneyOwed
        ? h(
            ChartCard,
            {
              title: "Money owed",
              subtitle: `${money(d.moneyOwed.total)} outstanding on ${count(d.moneyOwed.invoices)} invoice${d.moneyOwed.invoices === 1 ? "" : "s"}`,
              chart: { kind: "hbars", valueFormat: "money", slices: d.moneyOwed.buckets }
            },
            h(HBars, { chart: { valueFormat: "money", slices: d.moneyOwed.buckets }, showShare: false }),
            h("p", { className: "ra-note" }, h("button", { type: "button", className: "ra-link", onClick: () => openReport("money-owed") }, "Open the outstanding report"))
          )
        : null,
      d?.topCustomers
        ? h(
            "section",
            { className: "ra-card" },
            h("div", { className: "ra-card__head" }, h("div", null, h("h3", { className: "ra-card__title" }, "Top customers"), h("p", { className: "ra-card__sub" }, "Revenue in the period")), h("button", { type: "button", className: "ra-link", onClick: () => openReport("top-customers") }, "See all")),
            h(
              "table",
              { className: "ra-mini" },
              h("thead", null, h("tr", null, h("th", null, "Customer"), h("th", { className: "num" }, "Revenue"), h("th", { className: "num" }, "Bookings"))),
              h(
                "tbody",
                null,
                d.topCustomers.rows.length === 0 ? h("tr", null, h("td", { colSpan: 3 }, "No revenue in this period.")) : null,
                d.topCustomers.rows.map((r) =>
                  h("tr", { key: r.customerId }, h("td", null, h(Link, { to: `/admin/customers/${r.customerId}`, style: { color: "inherit" } }, r.name), r.company ? h("div", { className: "ra-card__sub" }, r.company) : null), h("td", { className: "num" }, money(r.revenue)), h("td", { className: "num" }, count(r.bookings)))
                ),
                d.topCustomers.everyoneElse
                  ? h("tr", { className: "ra-rest" }, h("td", null, d.topCustomers.everyoneElse.name), h("td", { className: "num" }, money(d.topCustomers.everyoneElse.revenue)), h("td", { className: "num" }, count(d.topCustomers.everyoneElse.bookings)))
                  : null
              )
            ),
            h("p", { className: "ra-note" }, d.topCustomers.sentence)
          )
        : null
    ),

    d && d.shortcuts.length
      ? h(
          "section",
          null,
          h("div", { className: "ra-card__head", style: { marginBottom: 8 } }, h("h3", { className: "ra-card__title" }, "Reports used most"), h(Link, { to: "/admin/reports/library", className: "ra-link" }, "See all")),
          h(
            "div",
            { className: "ra-strip" },
            d.shortcuts.map((s) =>
              h("a", { key: s.reportKey, href: `/admin/reports/${s.reportKey}`, onClick: (e) => { e.preventDefault(); openReport(s.reportKey); } }, h("b", null, s.name), h("small", null, s.description))
            )
          )
        )
      : null
  );
}

// ── Screen 2: a single report (spec §4) ─────────────────────────────────────

/** Old Report Library keys, so bookmarks keep working. */
const OLD_KEYS = {
  revenue: "revenue-by-month", customer: "customer-list", occupancy: "desk-occupancy", desk: "booking",
  cabin: "booking", "meeting-room": "meeting-room-usage", "day-pass": "day-pass-sales", payment: "payments-received",
  invoice: "invoice-register", outstanding: "money-owed", wallet: "wallet-balances", "check-in-out": "check-in-log",
  "purchase-order": "purchase-orders"
};

const STATUS_TONES = [
  [/^(confirmed|paid|active|done|verified|received|complete|yes|in use|sent)$/i, "success"],
  [/^(completed|used|in store|returned|approved|draft)$/i, "neutral"],
  [/^(cancelled|overdue|failed|unpaid|over 60 days late|31–60 days late|scrapped|lost|high|damaged|locked)$/i, "danger"],
  [/^(no-show|part-paid|part received|awaiting|awaiting payment|awaiting approval|pending|1–30 days late|incomplete|not yet|in repair|in progress|rework|medium|part refunded|not verified)$/i, "warning"]
];

function toneOf(value) {
  for (const [pattern, tone] of STATUS_TONES) if (pattern.test(String(value ?? ""))) return tone;
  return "neutral";
}

const numericType = (t) => t === "money" || t === "number" || t === "percent" || t === "hours";

function defaultsOf(definition) {
  const values = {};
  for (const f of definition.filters) if (f.kind !== "dateRange") values[f.key] = [...f.default];
  const range = presetRange(definition.defaultRange === "all" ? "all" : "30d", definition.defaultRange);
  values.from = [range.from];
  values.to = [range.to];
  return values;
}

export function ReportPage() {
  useStyles();
  const { key } = useParams();
  const navigate = useNavigate();

  React.useEffect(() => {
    if (OLD_KEYS[key]) navigate(`/admin/reports/${OLD_KEYS[key]}`, { replace: true });
  }, [key]);
  if (OLD_KEYS[key]) return null;
  return h(ReportScreen, { key, reportKey: key });
}

function ReportScreen({ reportKey }) {
  const toast = useToast();
  const navigate = useNavigate();
  const [catalogue, setCatalogue] = React.useState(null);
  const [result, setResult] = React.useState(null);
  const [draft, setDraft] = React.useState(null);
  const [labels, setLabels] = React.useState({});
  const [errors, setErrors] = React.useState({});
  const [loading, setLoading] = React.useState(true);
  const [denied, setDenied] = React.useState(null);
  const [page, setPage] = React.useState(1);
  const [sort, setSort] = React.useState(null);
  const [search, setSearch] = React.useState("");
  const [columns, setColumns] = React.useState(null);
  const [showColumns, setShowColumns] = React.useState(false);
  const [selected, setSelected] = React.useState(null);
  const [scheduling, setScheduling] = React.useState(false);
  const [exporting, setExporting] = React.useState(null);
  const [favourite, setFavourite] = React.useState(false);
  const applied = React.useRef(null);
  const columnsRef = React.useRef(null);
  useOutside(columnsRef, () => setShowColumns(false), showColumns);

  const definition = catalogue?.reports.find((r) => r.key === reportKey) ?? null;

  const run = React.useCallback(async ({ filters, nextPage = 1, nextSort, nextSearch, opened = false } = {}) => {
    setLoading(true);
    setErrors({});
    try {
      const body = { page: nextPage, sort: nextSort ?? undefined, search: nextSearch || undefined, opened };
      if (filters) body.filters = filters;
      const data = await api.run(reportKey, body);
      setResult(data);
      applied.current = data.appliedFilters;
      setDraft(JSON.parse(JSON.stringify(data.appliedFilters)));
      setPage(data.page);
      setSort(data.sort);
      if (opened) setColumns(data.savedColumns ?? null);
    } catch (error) {
      if (error instanceof ApiError && (error.status === 403 || error.status === 404)) {
        setDenied(error.status === 404 ? "This report does not exist." : "You do not have access to this report.");
      } else {
        setErrors(fieldErrors(error));
        toast.error(errorText(error, "The report could not be run."));
      }
    } finally {
      setLoading(false);
    }
  }, [reportKey]);

  React.useEffect(() => {
    let cancelled = false;
    api.definitions().then((c) => {
      if (cancelled) return;
      setCatalogue(c);
      const def = c.reports.find((r) => r.key === reportKey);
      if (!def) {
        setDenied("You do not have access to this report.");
        setLoading(false);
        return;
      }
      setFavourite(def.isFavourite);
      // Opened from the overview: its date range and location come along (spec §3.8).
      const carried = window.history.state?.usr?.filters;
      run({ filters: carried ?? undefined, opened: true });
    }).catch(() => {
      if (!cancelled) { setDenied("Could not load the reports."); setLoading(false); }
    });
    return () => { cancelled = true; };
  }, [reportKey]);

  // Search inside the result set, after the admin stops typing.
  const searchTimer = React.useRef(null);
  const onSearch = (value) => {
    setSearch(value);
    clearTimeout(searchTimer.current);
    searchTimer.current = setTimeout(() => run({ filters: applied.current, nextSort: sort, nextSearch: value }), 400);
  };

  if (denied) {
    return h("div", { className: "ra" }, h("div", { className: "ra-card ra-empty" }, denied, h(Link, { to: "/admin/reports/library", className: "ra-link" }, "Back to the report library")));
  }
  if (!definition || !draft) {
    return h("div", { className: "ra" }, h(Skeleton, { height: 40 }), h("div", { className: "ra-figures" }, [0, 1, 2, 3].map((i) => h("div", { key: i, className: "ra-card" }, h(Skeleton, { height: 80 })))), h("div", { className: "ra-card" }, h(Skeleton, { height: 260 })), h("div", { className: "ra-card" }, h(Skeleton, { height: 360 })));
  }

  const canExport = catalogue.canExport;
  const defaults = defaultsOf(definition);
  const allColumns = result?.columns ?? definition.columns;
  const visible = (columns ?? allColumns.filter((c) => c.defaultVisible).map((c) => c.key))
    .map((k) => allColumns.find((c) => c.key === k)).filter(Boolean);

  const setValue = (k, v) => setDraft((d) => ({ ...d, [k]: v }));
  const runDraft = () => run({ filters: draft, nextSort: sort, nextSearch: search });
  const clear = () => { setSearch(""); setLabels({}); run({ filters: defaults }); };
  const removeChip = (k) => {
    const next = { ...applied.current };
    if (k === "dates") { next.from = defaults.from; next.to = defaults.to; }
    else next[k] = defaults[k] ?? [];
    run({ filters: next, nextSort: sort, nextSearch: search });
  };
  const toggleFavourite = async () => {
    const next = !favourite;
    setFavourite(next);
    try {
      await (next ? api.addFavourite(reportKey) : api.removeFavourite(reportKey));
      toast.success(next ? "Saved as a favourite." : "Removed from favourites.");
    } catch (error) {
      setFavourite(!next);
      toast.error(errorText(error, "That did not save."));
    }
  };
  const doExport = async (format) => {
    setExporting(format);
    try {
      const outcome = await api.exportFile(reportKey, { filters: applied.current, format, sort });
      if (outcome.queued) toast.success(outcome.message);
    } catch (error) {
      toast.error(errorText(error, "The export failed."));
    } finally {
      setExporting(null);
    }
  };
  const saveColumns = (next) => {
    setColumns(next);
    api.savePreferences(reportKey, next).catch(() => {});
  };
  const sortBy = (col) => {
    if (!col.sortable) return;
    const [k, dir] = (sort ?? "").split(":");
    const next = `${col.key}:${k === col.key && dir === "asc" ? "desc" : "asc"}`;
    run({ filters: applied.current, nextSort: next, nextSearch: search });
  };

  const from = draft.from?.[0] ?? defaults.from[0];
  const to = draft.to?.[0] ?? defaults.to[0];
  const allowAll = definition.defaultRange === "all";
  const preset = presetOf(from, to, allowAll);
  const rangeOptions = allowAll ? [{ value: "all", label: "All time" }, ...PRESETS] : PRESETS;
  const r = result;
  const pageCount = r ? Math.max(1, Math.ceil(r.totalRows / r.pageSize)) : 1;
  const firstRow = r && r.totalRows ? (r.page - 1) * r.pageSize + 1 : 0;
  const lastRow = r ? Math.min(r.totalRows, r.page * r.pageSize) : 0;
  const sideCharts = r ? r.charts.slice(1) : [];

  return h(
    "div",
    { className: "ra" },
    h(
      "nav",
      { className: "ra-crumb", "aria-label": "Breadcrumb" },
      h(Link, { to: "/admin/reports/overview" }, "Reports & analysis"), "/",
      h(Link, { to: "/admin/reports/library" }, "Report library"), "/",
      h("span", { "aria-current": "page" }, definition.name)
    ),
    h(
      "div",
      { className: "ra-head" },
      h("div", null, h("h1", { className: "ra-title" }, definition.name), h("p", { className: "ra-sub" }, r ? r.sentence : definition.description)),
      h(
        "div",
        { className: "ra-actions" },
        h(Button, { variant: "outline", onClick: toggleFavourite, "aria-pressed": favourite }, h("span", { className: "ra-star", "aria-pressed": favourite, style: { display: "inline-flex" } }, h(IconStar, { size: 15 })), favourite ? " Favourite" : " Save as favourite"),
        canExport ? h(Button, { variant: "outline", onClick: () => setScheduling(true) }, h(IconMail, { size: 15 }), " Email weekly") : null,
        canExport ? h(Button, { variant: "outline", loading: exporting === "pdf", onClick: () => doExport("pdf") }, "PDF") : null,
        canExport ? h(Button, { loading: exporting === "xlsx", onClick: () => doExport("xlsx") }, h(IconDownload, { size: 15 }), " Export to Excel") : null
      )
    ),

    // The filter bar: one component, driven by the report's definition (spec §4.2).
    h(
      "section",
      { className: "ra-card" },
      h(
        "div",
        { className: "ra-filters" },
        definition.filters.map((f) => {
          if (f.kind === "dateRange") {
            return h(
              "div",
              { key: f.key, className: "ra-field" },
              h("span", { className: "ra-label" }, f.label),
              h(
                "div",
                { className: "ra-dates" },
                h(Seg, {
                  label: f.label, options: rangeOptions, value: preset,
                  onChange: (v) => { if (v !== "custom") { const range = presetRange(v, definition.defaultRange); setDraft((d) => ({ ...d, from: [range.from], to: [range.to] })); } else setDraft((d) => ({ ...d })); }
                }),
                preset === "custom" || true
                  ? h(
                      Frag,
                      null,
                      h("input", { type: "date", className: "ra-input ra-input--date", value: from, onChange: (e) => setValue("from", [e.target.value]), "aria-label": "From" }),
                      h("span", { style: { color: "var(--ra-muted)" } }, "to"),
                      h("input", { type: "date", className: "ra-input ra-input--date", value: to, onChange: (e) => setValue("to", [e.target.value]), "aria-label": "To" })
                    )
                  : null
              ),
              errors.from || errors.to ? h("span", { className: "ra-field__error" }, errors.from ?? errors.to) : null
            );
          }
          if (f.kind === "location") {
            if ((f.options ?? []).length <= 1) return null;
            return h("div", { key: f.key, className: "ra-field" }, h("label", { htmlFor: `ra-f-${f.key}` }, f.label), h("select", { id: `ra-f-${f.key}`, className: "ra-select", value: draft[f.key]?.[0] ?? "", onChange: (e) => setValue(f.key, e.target.value ? [e.target.value] : []) }, h("option", { value: "" }, f.allLabel ?? "All locations"), f.options.map((o) => h("option", { key: o.value, value: o.value }, o.label))));
          }
          if (f.kind === "select") {
            return h("div", { key: f.key, className: "ra-field" }, h("label", { htmlFor: `ra-f-${f.key}` }, f.label), h("select", { id: `ra-f-${f.key}`, className: "ra-select", value: draft[f.key]?.[0] ?? "", onChange: (e) => setValue(f.key, e.target.value ? [e.target.value] : []) }, f.default.length === 0 ? h("option", { value: "" }, f.allLabel ?? "All") : null, f.options.map((o) => h("option", { key: o.value, value: o.value }, o.label))));
          }
          if (f.kind === "multi") {
            return h("div", { key: f.key, className: "ra-field" }, h("span", { className: "ra-label" }, f.label), h(MultiSelect, { label: f.label, options: f.options, value: draft[f.key] ?? [], allLabel: f.allLabel, onChange: (v) => setValue(f.key, v) }));
          }
          if (f.kind === "customer") {
            const id = draft[f.key]?.[0] ?? null;
            const chipLabel = r?.chips.find((c) => c.key === "customer")?.label?.replace(/^Customer: /, "");
            return h("div", { key: f.key, className: "ra-field" }, h("span", { className: "ra-label" }, f.label), h(CustomerPicker, { value: id, label: id ? labels[id] ?? chipLabel ?? "" : "", onChange: (v, name) => { setValue(f.key, v ? [v] : []); if (v) setLabels((l) => ({ ...l, [v]: name })); } }));
          }
          if (f.kind === "toggle") {
            return h("label", { key: f.key, className: "ra-check", style: { height: 34 } }, h("input", { type: "checkbox", checked: (draft[f.key] ?? []).includes("true"), onChange: (e) => setValue(f.key, e.target.checked ? ["true"] : []) }), f.label);
          }
          return null;
        }),
        h("div", { className: "ra-actions" }, h(Button, { onClick: runDraft, loading: loading }, "Run report"), h(Button, { variant: "ghost", onClick: clear }, "Clear"))
      ),
      r && r.chips.length
        ? h(
            "div",
            { className: "ra-chips", "aria-label": "Filters applied" },
            r.chips.map((c) => h("span", { key: c.key, className: "ra-chip" }, c.label, h("button", { type: "button", "aria-label": `Remove ${c.label}`, onClick: () => removeChip(c.key) }, h(IconX, { size: 12 }))))
          )
        : null
    ),

    // Summary figures: their own aggregate over every matching row (spec §4.3).
    r && r.summary.length
      ? h(
          "div",
          { className: "ra-figures" },
          r.summary.map((s) =>
            h(
              "div",
              { key: s.key, className: "ra-card" },
              h("div", { className: "ra-figure__label" }, s.label),
              h("div", { className: "ra-figure__value" }, s.value == null && s.sub ? "—" : fmt(r.totalRows === 0 && s.value == null ? 0 : s.value, s.format)),
              h(Change, { kind: s.changeKind, change: s.change, format: s.format, upIsGood: s.upIsGood }),
              s.sub ? h("div", { className: "ra-figure__sub" }, s.sub) : null
            )
          )
        )
      : null,

    // The chart strip — left out when a report has no useful chart (spec §4.4).
    r && r.charts.length
      ? h(
          "div",
          { className: sideCharts.length ? "ra-strip2" : "" },
          h(ChartCard, { title: r.charts[0].title, subtitle: r.charts[0].subtitle, chart: r.charts[0], note: r.charts[0].kind === "heatmap" ? null : r.charts[0].note }, h(AnyChart, { chart: r.charts[0] })),
          sideCharts.length
            ? h("div", { className: "ra-side" }, sideCharts.map((c) => h(ChartCard, { key: c.id, title: c.title, subtitle: c.subtitle, chart: c }, h(AnyChart, { chart: c }))))
            : null
        )
      : null,

    // The table: one page of rows, and a total row over all of them (spec §4.5).
    r
      ? h(
          "section",
          { className: "ra-card" },
          h(
            "div",
            { className: "ra-tablebar" },
            h("div", { className: "ra-search" }, h(IconSearch, { size: 15 }), h("input", { className: "ra-input", placeholder: "Search in results", value: search, onChange: (e) => onSearch(e.target.value), "aria-label": "Search in results" })),
            h(
              "div",
              { className: "ra-pop", ref: columnsRef },
              h(Button, { variant: "outline", size: "sm", onClick: () => setShowColumns((v) => !v), "aria-expanded": showColumns }, h(IconColumns, { size: 14 }), " Columns"),
              showColumns
                ? h(
                    "div",
                    { className: "ra-pop__panel ra-pop__panel--right", style: { minWidth: 260 } },
                    h("div", { className: "ra-card__sub", style: { padding: "4px 8px 8px" } }, "Tick to show; arrows to reorder. Remembered for you."),
                    [...visible, ...allColumns.filter((c) => !visible.includes(c))].map((c) => {
                      const on = visible.includes(c);
                      const index = visible.indexOf(c);
                      const keys = visible.map((v) => v.key);
                      const move = (delta) => { const next = [...keys]; const [m] = next.splice(index, 1); next.splice(index + delta, 0, m); saveColumns(next); };
                      return h(
                        "div",
                        { key: c.key, className: "ra-pop__item", style: { justifyContent: "space-between" } },
                        h("label", { style: { display: "flex", gap: 8, alignItems: "center", cursor: "pointer" } }, h("input", { type: "checkbox", checked: on, onChange: () => saveColumns(on ? keys.filter((k) => k !== c.key) : [...keys, c.key]) }), c.header),
                        on
                          ? h("span", { style: { display: "inline-flex", gap: 2 } }, h("button", { type: "button", className: "ra-close", disabled: index === 0, "aria-label": `Move ${c.header} up`, onClick: () => move(-1) }, h(IconUp, { size: 13 })), h("button", { type: "button", className: "ra-close", disabled: index === visible.length - 1, "aria-label": `Move ${c.header} down`, onClick: () => move(1) }, h(IconDown, { size: 13 })))
                          : null
                      );
                    }),
                    h("div", { style: { padding: "6px 8px" } }, h("button", { type: "button", className: "ra-link", onClick: () => saveColumns(null) }, "Back to the default columns"))
                  )
                : null
            )
          ),
          r.totalRows === 0
            ? h("div", { className: "ra-empty" }, search ? `No ${r.rowNoun} match "${search}".` : `No ${r.rowNoun} match these filters.`, h(Button, { variant: "outline", onClick: clear }, "Clear filters"))
            : h(
                Frag,
                null,
                h(
                  "div",
                  { className: "ra-tablewrap", style: { opacity: loading ? 0.6 : 1 } },
                  h(
                    "table",
                    { className: "ra-table" },
                    h(
                      "thead",
                      null,
                      h(
                        "tr",
                        null,
                        visible.map((c) => {
                          const [k, dir] = (sort ?? "").split(":");
                          const active = k === c.key;
                          return h(
                            "th",
                            { key: c.key, className: numericType(c.type) ? "num" : undefined, "aria-sort": active ? (dir === "asc" ? "ascending" : "descending") : "none" },
                            c.sortable ? h("button", { type: "button", onClick: () => sortBy(c) }, c.header, active ? (dir === "asc" ? " ↑" : " ↓") : "") : c.header
                          );
                        })
                      )
                    ),
                    h(
                      "tbody",
                      null,
                      r.rows.map((row, i) =>
                        h(
                          "tr",
                          { key: row._id ?? i, tabIndex: 0, onClick: () => setSelected(row), onKeyDown: (e) => { if (e.key === "Enter") setSelected(row); } },
                          visible.map((c) =>
                            h("td", { key: c.key, className: numericType(c.type) ? "num" : undefined, title: row[c.key] == null ? undefined : String(row[c.key]) }, c.type === "status" && row[c.key] ? h(Badge, { tone: toneOf(row[c.key]) }, row[c.key]) : fmt(row[c.key], c.type))
                          )
                        )
                      )
                    ),
                    h(
                      "tfoot",
                      null,
                      h(
                        "tr",
                        null,
                        visible.map((c, i) =>
                          h("td", { key: c.key, className: numericType(c.type) ? "num" : undefined }, i === 0 ? `Total of all ${count(r.totalRows)} ${r.totalRows === 1 ? r.rowNoun.replace(/s$/, "") : r.rowNoun}` : c.totalled && c.key in r.totals ? fmt(r.totals[c.key], c.type) : "")
                        )
                      )
                    )
                  )
                ),
                h(
                  "div",
                  { className: "ra-pager" },
                  h("span", null, `Showing ${count(firstRow)}–${count(lastRow)} of ${count(r.totalRows)} — the total row above counts all ${count(r.totalRows)}, not just this page.`),
                  pageCount > 1
                    ? h(
                        "div",
                        { className: "ra-actions" },
                        h(Button, { variant: "outline", size: "sm", disabled: page <= 1 || loading, onClick: () => run({ filters: applied.current, nextPage: page - 1, nextSort: sort, nextSearch: search }) }, "Previous"),
                        h("span", null, `Page ${page} of ${pageCount}`),
                        h(Button, { variant: "outline", size: "sm", disabled: page >= pageCount || loading, onClick: () => run({ filters: applied.current, nextPage: page + 1, nextSort: sort, nextSearch: search }) }, "Next")
                      )
                    : null
                )
              )
        )
      : h("div", { className: "ra-card" }, h(Skeleton, { height: 360 })),

    selected
      ? h(
          Overlay,
          { label: "Row details", onClose: () => setSelected(null) },
          h(
            "aside",
            { className: "ra-drawer" },
            h("div", { className: "ra-dialog__head", style: { padding: 0, border: 0 } }, h("h3", null, String(selected[allColumns[0].key] ?? "Details")), h("button", { type: "button", className: "ra-close", onClick: () => setSelected(null), "aria-label": "Close" }, h(IconX, { size: 18 }))),
            h("dl", { className: "ra-kv" }, allColumns.map((c) => h(Frag, { key: c.key }, h("dt", null, c.header), h("dd", null, c.type === "status" && selected[c.key] ? h(Badge, { tone: toneOf(selected[c.key]) }, selected[c.key]) : fmt(selected[c.key], c.type))))),
            selected._link ? h(Link, { to: selected._link, className: "ra-link" }, selected._link.includes("/customers/") ? "Open the customer" : "Open the record") : null,
            h("p", { className: "ra-card__sub" }, "The report stays behind this panel with its filters as they were.")
          )
        )
      : null,

    scheduling
      ? h(ScheduleDialog, { reportKey, reportName: definition.name, filters: applied.current, onClose: () => setScheduling(false) })
      : null
  );
}

// ── Screen 3: the library (spec §5) ─────────────────────────────────────────

const CHIPS = [
  { key: "all", label: "All" },
  { key: "favourites", label: "Favourites" },
  { key: "bookings", label: "Bookings" },
  { key: "revenue", label: "Revenue & tax" },
  { key: "customers", label: "Customers" },
  { key: "occupancy", label: "Occupancy" },
  { key: "operations", label: "Operations" }
];

function matches(report, chip, term) {
  if (chip === "favourites" && !report.isFavourite) return false;
  if (chip !== "all" && chip !== "favourites" && report.chip !== chip && report.group !== chip) return false;
  if (!term) return true;
  const hay = `${report.name} ${report.description} ${report.keywords.join(" ")}`.toLowerCase();
  return term.toLowerCase().split(/\s+/).every((word) => hay.includes(word));
}

/** What each report holds, in a few words, for the right of its library row (spec §5.3). */
const HINTS = {
  booking: "Every booking, with totals", cancellations: "Reason, notice and refund", "meeting-room-usage": "Hours per room, per day",
  "day-pass-sales": "Walk-ins and online", "event-bookings": "Event spaces only", "booking-source": "App, front desk or pass",
  "revenue-by-month": "Against last year", "revenue-by-category": "Desks, cabins, passes, services", "gst-summary": "By rate, for GSTR-1",
  "invoice-register": "Every invoice raised", "payments-received": "By mode, with reference", "money-owed": "Aged by due date",
  refunds: "Back to wallets", "customer-list": "With GSTIN and profile", "top-customers": "Ranked by spend",
  "new-signups": "Source and first booking", "gone-quiet": "No booking in 60 days", "wallet-balances": "Money held",
  "desk-occupancy": "Per space", "busy-hours": "Weekday against hour", "check-in-log": "Who came in and when",
  "maintenance-tickets": "Raised, closed, time taken", "asset-register": "Cost and current value", "purchase-orders": "By supplier"
};

function statusLine(report) {
  if (report.liveFigure) return report.liveFigure;
  if (report.nextEmailUtc) return `Next emailed ${friendlyNext(report.nextEmailUtc)}`;
  if (report.lastOpenedUtc) return `Opened ${ago(report.lastOpenedUtc)}${report.lastRowCount != null ? ` · ${count(report.lastRowCount)} rows` : ""}`;
  return "Not opened yet";
}

function recipientsText(list, me) {
  const names = list.map((r) => (r === me ? "You" : r));
  if (names.length <= 3) return names.join(", ");
  const first = names.includes("You") ? "You" : names[0];
  return `${first} + ${names.length - 1} others (${names.length})`;
}

export function ReportLibraryPage() {
  useStyles();
  const toast = useToast();
  const { section } = useParams();
  const me = useAuth((s) => s.user)?.email ?? "";
  const [catalogue, setCatalogue] = React.useState(null);
  const [schedules, setSchedules] = React.useState(null);
  const [chip, setChip] = React.useState(section && CHIPS.some((c) => c.key === section) ? section : "all");
  const [term, setTerm] = React.useState("");
  const [failed, setFailed] = React.useState(false);
  const [editing, setEditing] = React.useState(null);
  const [custom, setCustom] = React.useState(false);

  const load = React.useCallback(() => {
    setFailed(false);
    api.definitions().then(setCatalogue).catch(() => setFailed(true));
    api.schedules().then(setSchedules).catch(() => setSchedules([]));
  }, []);
  React.useEffect(load, [load]);

  if (failed) return h("div", { className: "ra" }, h("div", { className: "ra-error", role: "alert" }, "Could not load the reports.", h(Button, { variant: "outline", size: "sm", onClick: load }, "Retry")));
  if (!catalogue) return h("div", { className: "ra" }, h(Skeleton, { height: 40 }), h("div", { className: "ra-grid4" }, [0, 1, 2, 3].map((i) => h("div", { key: i, className: "ra-card" }, h(Skeleton, { height: 90 })))), h("div", { className: "ra-card" }, h(Skeleton, { height: 300 })));

  const reports = catalogue.reports;
  const byKey = Object.fromEntries(reports.map((r) => [r.key, r]));
  const groupSlot = Object.fromEntries(catalogue.groups.map((g) => [g.key, g.slot]));
  const counts = Object.fromEntries(CHIPS.map((c) => [c.key, reports.filter((r) => matches(r, c.key, "")).length]));
  const shown = reports.filter((r) => matches(r, chip, term));
  const pinned = catalogue.pinned.map((k) => byKey[k]).filter(Boolean);

  const stop = async (schedule) => {
    if (!window.confirm(`Stop emailing ${schedule.reportName}? It is kept, so it can be turned back on.`)) return;
    try {
      await api.stopSchedule(schedule.scheduleId);
      toast.success("Schedule stopped.");
      load();
    } catch (error) {
      toast.error(errorText(error, "The schedule could not be stopped."));
    }
  };
  const turnOn = async (schedule) => {
    try {
      await api.updateSchedule(schedule.scheduleId, { ...schedule, reportKey: schedule.reportKey, isActive: true });
      toast.success("Schedule turned back on.");
      load();
    } catch (error) {
      toast.error(errorText(error, "The schedule could not be turned on."));
    }
  };

  return h(
    "div",
    { className: "ra" },
    h(
      "div",
      { className: "ra-head" },
      h("div", null, h("h1", { className: "ra-title" }, "Report library"), h("p", { className: "ra-sub" }, `${reports.length} reports you can open, export and email.`)),
      h(Button, { variant: "outline", onClick: () => setCustom(true) }, h(IconPlus, { size: 15 }), " Build a custom report")
    ),
    h(
      "div",
      { className: "ra-lib-search" },
      h("div", { className: "ra-search" }, h(IconSearch, { size: 15 }), h("input", { className: "ra-input", placeholder: "Search reports — try “GST” or “money”", value: term, onChange: (e) => setTerm(e.target.value), "aria-label": "Search reports" })),
      h("div", { className: "ra-catchips", role: "group", "aria-label": "Categories" }, CHIPS.map((c) => h("button", { key: c.key, type: "button", "aria-pressed": chip === c.key, onClick: () => setChip(c.key) }, c.label, h("em", null, counts[c.key]))))
    ),

    !term && chip === "all" && pinned.length
      ? h(
          "section",
          null,
          h("h3", { className: "ra-card__title", style: { marginBottom: 10 } }, catalogue.pinnedAreMostUsed ? "Most used" : "Favourites"),
          h(
            "div",
            { className: "ra-grid4", style: { gridTemplateColumns: "repeat(auto-fill, minmax(240px, 1fr))" } },
            pinned.map((r) =>
              h(Link, { key: r.key, to: `/admin/reports/${r.key}`, className: "ra-fav", style: { "--fav": slotColour(groupSlot[r.group]) } }, h("b", null, r.name), h("small", null, r.description), h("span", null, statusLine(r)))
            )
          )
        )
      : null,

    shown.length === 0
      ? h("div", { className: "ra-card ra-empty" }, `No reports match “${term}”.`, h("button", { type: "button", className: "ra-link", onClick: () => { setTerm(""); setChip("all"); } }, "Show every report"))
      : h(
          "div",
          { className: "ra-grid2", style: { gridTemplateColumns: "repeat(auto-fit, minmax(360px, 1fr))" } },
          catalogue.groups.map((g) => {
            const list = shown.filter((r) => r.group === g.key);
            if (list.length === 0) return null;
            return h(
              "section",
              { key: g.key, className: "ra-card" },
              h("div", { className: "ra-group__head" }, h("i", { className: "ra-dot", style: { background: slotColour(g.slot) } }), h("div", null, h("h3", { className: "ra-card__title" }, g.label), h("p", { className: "ra-card__sub" }, g.description))),
              list.map((r) =>
                h(Link, { key: r.key, to: `/admin/reports/${r.key}`, className: "ra-row" }, h("span", null, r.name), r.isFavourite ? h("em", null, "★ favourite") : r.liveFigure ? h("em", { className: "ra-row__fig" }, r.liveFigure) : h("em", null, r.hint ?? HINTS[r.key] ?? ""))
              )
            );
          })
        ),

    h(
      "section",
      { className: "ra-card" },
      h("div", { className: "ra-card__head" }, h("div", null, h("h3", { className: "ra-card__title" }, "Reports that email themselves"), h("p", { className: "ra-card__sub" }, "Times are Indian Standard Time. Open any report and choose “Email weekly” to add one."))),
      schedules == null
        ? h(Skeleton, { height: 120 })
        : schedules.length === 0
          ? h("div", { className: "ra-empty" }, "No reports email themselves yet.")
          : h(
              "div",
              { className: "ra-tablewrap" },
              h(
                "table",
                { className: "ra-table" },
                h("thead", null, h("tr", null, ["Report", "How often", "Sent to", "Format", "Next send", ""].map((c, i) => h("th", { key: i }, c)))),
                h(
                  "tbody",
                  null,
                  schedules.map((s) =>
                    h(
                      "tr",
                      { key: s.scheduleId, style: { cursor: "default" } },
                      h(
                        "td",
                        { style: { whiteSpace: "normal" } },
                        h(Link, { to: `/admin/reports/${s.reportKey}`, className: "ra-link" }, s.reportName),
                        s.lastRunStatus === "failed" && s.lastError ? h("div", { className: "ra-failed" }, `Last run failed — ${s.lastError}`) : null,
                        s.disabledReason ? h("div", { className: "ra-failed" }, s.disabledReason) : null
                      ),
                      h("td", null, s.howOften),
                      h("td", { title: s.recipients.join(", ") }, recipientsText(s.recipients, me)),
                      h("td", null, s.format === "pdf" ? "PDF" : "Excel"),
                      h("td", { className: s.isActive ? undefined : "ra-stopped" }, s.isActive ? s.nextRunText ?? "—" : "Stopped"),
                      h(
                        "td",
                        null,
                        s.canEdit
                          ? h(
                              "span",
                              { className: "ra-actions" },
                              h("button", { type: "button", className: "ra-link", onClick: () => setEditing(s) }, "Edit"),
                              s.isActive ? h("button", { type: "button", className: "ra-link", onClick: () => stop(s) }, "Stop") : h("button", { type: "button", className: "ra-link", onClick: () => turnOn(s) }, "Turn on")
                            )
                          : h("span", { className: "ra-card__sub" }, `By ${s.createdByName}`)
                      )
                    )
                  )
                )
              )
            )
    ),

    editing ? h(ScheduleDialog, { reportKey: editing.reportKey, reportName: editing.reportName, existing: editing, onClose: () => setEditing(null), onSaved: load }) : null,
    custom
      ? h(
          Overlay,
          { center: true, label: "Build a custom report", onClose: () => setCustom(false) },
          h(
            "div",
            { className: "ra-dialog" },
            h("div", { className: "ra-dialog__head" }, h("h3", null, "Build a custom report"), h("button", { type: "button", className: "ra-close", onClick: () => setCustom(false), "aria-label": "Close" }, h(IconX, { size: 18 }))),
            h("div", { className: "ra-dialog__body" }, h("p", { style: { margin: 0 } }, "The custom report builder comes in a later phase. Until then, every report here can be filtered, its columns chosen, and exported or emailed."), h("p", { className: "ra-card__sub", style: { margin: 0 } }, "Tell us which report you are missing and it can be added to the library.")),
            h("div", { className: "ra-dialog__foot" }, h(Button, { onClick: () => setCustom(false) }, "Close"))
          )
        )
      : null
  );
}

// ── The emailed link of a large export ──────────────────────────────────────

export function ReportDownloadPage() {
  useStyles();
  const { token } = useParams();
  const [state, setState] = React.useState("working");
  const [message, setMessage] = React.useState("");
  const start = React.useCallback(async () => {
    setState("working");
    try {
      await api.download(token);
      setState("done");
    } catch (error) {
      const data = error?.response?.data instanceof Blob ? await blobJson(error.response.data) : null;
      setMessage(data?.message ?? errorText(error, "This download link does not work."));
      setState("failed");
    }
  }, [token]);
  React.useEffect(() => { start(); }, [start]);
  return h(
    "div",
    { className: "ra" },
    h(
      "div",
      { className: "ra-card ra-empty" },
      state === "working" ? "Preparing your download…" : state === "done" ? "Your export has downloaded." : message,
      state === "done" ? h("button", { type: "button", className: "ra-link", onClick: start }, "Download it again") : null,
      h(Link, { to: "/admin/reports/library", className: "ra-link" }, "Go to the report library")
    )
  );
}
