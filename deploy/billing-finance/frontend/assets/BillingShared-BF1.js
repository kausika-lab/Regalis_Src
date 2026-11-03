/**
 * Billing & Finance build (Nov 2026) - what every new finance screen shares:
 * the API client, money/date helpers, the bf-* styles, small components (stat cards,
 * tabs, pills, pager, reason dialogs), the Record payment dialog (Invoices and Payments)
 * and the party ledger panel (customers and suppliers).
 *
 * Hand-maintained (this repo ships the built SPA). The page chunks BillingMoneyIn-BF1,
 * BillingPayables-BF1, BillingBank-BF1 and BillingCompliance-BF1 import from here; index
 * imports the pages by name (deploy/billing-finance/frontend/apply.mjs). A change ships
 * under a NEW file name (BF2 ...) because /assets is cached for a year.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as e,
  G as Spinner,
  p as Button,
  F as Select,
  I as Input,
  D as unwrap,
  E as http,
  i as makeIcon,
  u as useNavigate,
  Y as useParams
} from "./index-DI5X4c9f.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { b as fmtMoney, c as fmtDate, f as fmtDateTime } from "./formatters-DFe5xCRi.js";

export { useToast, usePermission, React, ApiError, e, Spinner, Button, Select, Input, unwrap, http, makeIcon, useNavigate, useParams, Modal, EmptyState, PageHeader, fmtMoney, fmtDate, fmtDateTime };

// ── API ──────────────────────────────────────────────────────────────────────

const clean = (params) => {
  if (!params) return undefined;
  const out = {};
  for (const [k, v] of Object.entries(params)) if (v !== undefined && v !== null && v !== "") out[k] = v;
  return out;
};

/** Thin wrapper over the app's axios instance; every call returns the envelope's data. */
export const api = {
  get: (path, params) => unwrap(http.get(path, { params: clean(params) })),
  /** For endpoints whose data may legitimately be null (unwrap treats null as an error). */
  getMaybe: (path, params) => http.get(path, { params: clean(params) }).then((r) => r.data?.data ?? null),
  post: (path, body) => unwrap(http.post(path, body ?? {})),
  put: (path, body) => unwrap(http.put(path, body ?? {})),
  del: (path) => unwrap(http.delete(path)),
  /** POST and keep the envelope message (for the server's "what happened" sentence). */
  postWithMessage: (path, body) => http.post(path, body ?? {}).then((r) => ({ data: r.data?.data, message: r.data?.message })),
  upload: (path, fields, file) => {
    const form = new FormData();
    for (const [k, v] of Object.entries(fields ?? {})) if (v !== undefined && v !== null) form.append(k, v);
    if (file) form.append("file", file, file.name);
    return unwrap(http.post(path, form, { headers: { "Content-Type": "multipart/form-data" } }));
  },
  uploadWithMessage: (path, fields, file) => {
    const form = new FormData();
    for (const [k, v] of Object.entries(fields ?? {})) if (v !== undefined && v !== null) form.append(k, v);
    if (file) form.append("file", file, file.name);
    return http.post(path, form, { headers: { "Content-Type": "multipart/form-data" } }).then((r) => ({ data: r.data?.data, message: r.data?.message }));
  },
  /** Downloads a file response (PDF, Excel) with the server's name or the fallback. */
  download: async (path, params, fallbackName) => {
    const response = await http.get(path, { params: clean(params), responseType: "blob" });
    const header = response.headers?.["content-disposition"] ?? "";
    const match = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(header);
    const name = match ? decodeURIComponent(match[1]) : fallbackName;
    const href = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = href;
    link.download = name;
    link.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  },
  /** Opens a file response (a PDF or an image) in a new tab. */
  open: async (path) => {
    const response = await http.get(path, { responseType: "blob" });
    const href = URL.createObjectURL(response.data);
    window.open(href, "_blank", "noopener");
    setTimeout(() => URL.revokeObjectURL(href), 60000);
  }
};

// ── Helpers ──────────────────────────────────────────────────────────────────

export const istToday = () => new Date(Date.now() + 5.5 * 3600e3).toISOString().slice(0, 10);
export const thisPeriod = () => istToday().slice(0, 7);
export const addDays = (iso, days) => {
  const d = new Date(`${iso}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + days);
  return d.toISOString().slice(0, 10);
};
export const errorText = (error, fallback) => (error instanceof ApiError ? error.message : fallback);
/** The server's error code (e.g. DUPLICATE_BILL_12, NO_GSTIN_CONFIRM), when there is one. */
export const errorCode = (error) => error?.errors?.[0]?.code ?? error?.code ?? error?.response?.data?.errors?.[0]?.code ?? null;
/** "1,234.5" -> 1234.5; null when not a non-negative amount with at most 2 decimals. */
export const amountOf = (text) => {
  const t = String(text ?? "").replace(/[,\s₹]/g, "");
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null;
  return Number(t);
};
export const money = (v) => fmtMoney(Number(v ?? 0));
export const plural = (n, word, many) => `${n} ${n === 1 ? word : many ?? `${word}s`}`;
/** "2026-10" -> "October 2026". */
export const monthLabel = (period) => {
  const [y, m] = String(period).split("-").map(Number);
  return new Date(Date.UTC(y, m - 1, 1)).toLocaleDateString("en-IN", { month: "long", year: "numeric", timeZone: "UTC" });
};
/** Last N months as select options, newest first. */
export const monthOptions = (count = 18) => {
  const out = [];
  const [y, m] = thisPeriod().split("-").map(Number);
  for (let i = 0; i < count; i++) {
    const d = new Date(Date.UTC(y, m - 1 - i, 1));
    const value = d.toISOString().slice(0, 7);
    out.push({ value, label: monthLabel(value) });
  }
  return out;
};
/** Indian financial years, newest first: "2026-27". */
export const fyOptions = (count = 4) => {
  const [y, m] = thisPeriod().split("-").map(Number);
  const start = m >= 4 ? y : y - 1;
  return Array.from({ length: count }, (_, i) => {
    const s = start - i;
    const value = `${s}-${String((s + 1) % 100).padStart(2, "0")}`;
    return { value, label: `FY ${value}` };
  });
};
export const currentFy = () => fyOptions(1)[0].value;

export function useDebounced(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

/** Loads data with a reload function; keeps the last result while reloading. */
export function useLoad(loader, deps) {
  const toast = useToast();
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setData(await loader());
    } catch (error) {
      toast.error(errorText(error, "Could not load this page."));
    } finally {
      setLoading(false);
    }
  }, deps); // eslint-disable-line react-hooks/exhaustive-deps
  React.useEffect(() => { load(); }, [load]);
  return { data, setData, loading, reload: load };
}

// ── Icons ────────────────────────────────────────────────────────────────────

export const IconSearch = makeIcon("search", [["circle", { cx: "11", cy: "11", r: "8", key: "bfs1" }], ["path", { d: "m21 21-4.3-4.3", key: "bfs2" }]]);
export const IconPlus = makeIcon("plus", [["path", { d: "M5 12h14", key: "bfp1" }], ["path", { d: "M12 5v14", key: "bfp2" }]]);
export const IconDownload = makeIcon("download", [["path", { d: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4", key: "bfd1" }], ["polyline", { points: "7 10 12 15 17 10", key: "bfd2" }], ["line", { x1: "12", x2: "12", y1: "15", y2: "3", key: "bfd3" }]]);
export const IconUpload = makeIcon("upload", [["path", { d: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4", key: "bfu1" }], ["polyline", { points: "17 8 12 3 7 8", key: "bfu2" }], ["line", { x1: "12", x2: "12", y1: "3", y2: "15", key: "bfu3" }]]);
export const IconFile = makeIcon("file-text", [["path", { d: "M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z", key: "bff1" }], ["path", { d: "M14 2v4a2 2 0 0 0 2 2h4", key: "bff2" }], ["path", { d: "M10 13H8M16 17H8M16 13h-2", key: "bff3" }]]);
export const IconBack = makeIcon("arrow-left", [["path", { d: "m12 19-7-7 7-7", key: "bfb1" }], ["path", { d: "M19 12H5", key: "bfb2" }]]);

// ── Styles ───────────────────────────────────────────────────────────────────

const STYLE_ID = "bf-shared-styles";
const STYLES = `
.bf-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:var(--space-4);margin:var(--space-5) 0}
.bf-card{all:unset;box-sizing:border-box;display:block;padding:var(--space-4) var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);min-width:0}
button.bf-card{cursor:pointer}
button.bf-card:hover,button.bf-card[aria-pressed="true"]{border-color:rgb(var(--accent-rgb) / 55%)}
.bf-card__label{font-size:var(--font-size-sm);color:var(--text-secondary)}
.bf-card__value{margin:4px 0 2px;font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:800;color:var(--text-primary);font-variant-numeric:tabular-nums;line-height:1.2;overflow-wrap:anywhere}
.bf-card__value--danger{color:var(--color-danger)}
.bf-card__sub{font-size:var(--font-size-xs);color:var(--text-muted)}
.bf-panel{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);padding:var(--space-5);min-width:0}
.bf-panel + .bf-panel{margin-top:var(--space-5)}
.bf-panel__title{margin:0 0 var(--space-3);font-size:var(--font-size-lg);font-weight:700}
.bf-tabs{display:flex;gap:var(--space-2);margin:var(--space-4) 0;flex-wrap:wrap}
.bf-tab{all:unset;padding:8px 14px;border-radius:var(--radius-full);border:1px solid var(--border-strong);font-size:var(--font-size-sm);font-weight:600;cursor:pointer;color:var(--text-secondary)}
.bf-tab[aria-selected="true"]{background:var(--regalis-black);border-color:var(--regalis-black);color:var(--regalis-white)}
.bf-toolbar{display:flex;gap:var(--space-3);margin-bottom:var(--space-4);flex-wrap:wrap;align-items:flex-end}
.bf-toolbar > *{flex:1 1 160px;min-width:0}
.bf-toolbar > .bf-search{flex:2 1 240px}
.bf-toolbar .field__label{font-size:var(--font-size-xs)}
.bf-toolbar .field__input{height:40px}
.bf-search{position:relative}
.bf-search svg{position:absolute;inset-inline-start:12px;bottom:12px;color:var(--text-muted);pointer-events:none}
.bf-search input{width:100%;height:40px;padding:8px 12px 8px 36px;box-sizing:border-box}
.bf-table-wrap{overflow-x:auto}
.bf-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.bf-table th{padding:10px 12px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--text-secondary);background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.bf-table td{padding:11px 12px;border-bottom:1px solid var(--border-subtle);vertical-align:middle}
.bf-table tfoot td{font-weight:700;background:rgb(var(--accent-rgb) / 5%)}
.bf-table tr.bf-row--overdue td:first-child{box-shadow:inset 3px 0 0 color-mix(in srgb,var(--color-danger) 60%,transparent)}
.bf-table tr.bf-row--tint td{background:color-mix(in srgb,var(--color-danger) 6%,transparent)}
.bf-table tr.bf-row--muted td{color:var(--text-muted)}
.bf-table tr.bf-row--click{cursor:pointer}
.bf-table tr.bf-row--click:hover td{background:rgb(var(--accent-rgb) / 5%)}
.bf-num{text-align:right!important;font-variant-numeric:tabular-nums;white-space:nowrap}
.bf-strong{font-weight:700;color:var(--text-primary)}
.bf-sub{font-size:var(--font-size-xs);color:var(--text-muted);overflow-wrap:anywhere}
.bf-red{color:var(--color-danger)}
.bf-green{color:var(--color-success)}
.bf-link{all:unset;cursor:pointer;color:var(--color-info);font-weight:600}
.bf-link:hover{text-decoration:underline}
.bf-actions{display:flex;gap:var(--space-2);justify-content:flex-end;flex-wrap:wrap}
.bf-pager{display:flex;align-items:center;justify-content:space-between;gap:var(--space-3);margin-top:var(--space-4);font-size:var(--font-size-sm);color:var(--text-secondary);flex-wrap:wrap}
.bf-pager div{display:flex;gap:var(--space-2)}
.bf-pill{display:inline-flex;padding:3px 10px;border-radius:var(--radius-full);font-size:var(--font-size-xs);font-weight:600;white-space:nowrap}
.bf-pill--success{background:color-mix(in srgb,var(--color-success) 14%,transparent);color:var(--color-success)}
.bf-pill--neutral{background:var(--surface-inset);color:var(--text-secondary)}
.bf-pill--info{background:color-mix(in srgb,var(--color-info) 12%,transparent);color:var(--color-info)}
.bf-pill--warning{background:color-mix(in srgb,var(--color-warning) 16%,transparent);color:color-mix(in srgb,var(--color-warning) 80%,black)}
.bf-pill--danger{background:color-mix(in srgb,var(--color-danger) 12%,transparent);color:var(--color-danger)}
.bf-chips{display:flex;gap:var(--space-2);flex-wrap:wrap}
.bf-chip{all:unset;padding:7px 14px;border-radius:var(--radius-full);border:1px solid var(--border-strong);font-size:var(--font-size-sm);font-weight:600;cursor:pointer;color:var(--text-secondary)}
.bf-chip[aria-pressed="true"]{border-color:var(--regalis-gold,#b8954a);background:color-mix(in srgb,var(--regalis-gold,#b8954a) 16%,transparent);color:var(--text-primary)}
.bf-chip[aria-disabled="true"]{opacity:.45;cursor:not-allowed}
.bf-band{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-3);padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 9%);border:1px solid rgb(var(--accent-rgb) / 22%)}
.bf-band > div{min-width:0}
.bf-band b{display:block;font-family:var(--font-display);font-size:var(--font-size-lg);font-variant-numeric:tabular-nums}
.bf-band span{font-size:var(--font-size-xs);color:var(--text-secondary)}
.bf-note{padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);font-size:var(--font-size-sm);line-height:1.5}
.bf-note--ok{background:color-mix(in srgb,var(--color-success) 10%,transparent);border:1px solid color-mix(in srgb,var(--color-success) 30%,transparent)}
.bf-note--info{background:color-mix(in srgb,var(--color-info) 9%,transparent);border:1px solid color-mix(in srgb,var(--color-info) 28%,transparent)}
.bf-note--warn{background:color-mix(in srgb,var(--color-warning) 12%,transparent);border:1px solid color-mix(in srgb,var(--color-warning) 35%,transparent)}
.bf-note--danger{background:color-mix(in srgb,var(--color-danger) 8%,transparent);border:1px solid color-mix(in srgb,var(--color-danger) 30%,transparent)}
.bf-kv{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:var(--space-3) var(--space-5);font-size:var(--font-size-sm)}
.bf-kv dt{font-size:var(--font-size-xs);color:var(--text-muted);text-transform:uppercase;letter-spacing:.05em}
.bf-kv dd{margin:2px 0 0;font-weight:600;overflow-wrap:anywhere}
.bf-split{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:var(--space-5)}
@media(max-width:900px){.bf-split{grid-template-columns:minmax(0,1fr)}}
.bf-sum{display:flex;flex-direction:column;gap:6px;font-size:var(--font-size-sm)}
.bf-sum div{display:flex;justify-content:space-between;gap:var(--space-3)}
.bf-sum .bf-sum__total{border-top:1px solid var(--border-subtle);padding-top:8px;font-weight:800;font-size:var(--font-size-lg)}
.bf-form-grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(200px,1fr));gap:var(--space-4)}
.bf-textarea{width:100%;min-height:72px;box-sizing:border-box;padding:8px 12px;border:1px solid var(--border-strong);border-radius:var(--radius-md);font:inherit;background:var(--surface-card);color:var(--text-primary)}
.bf-drop{display:flex;flex-direction:column;align-items:center;gap:6px;padding:var(--space-5);border:2px dashed var(--border-strong);border-radius:var(--radius-lg);text-align:center;color:var(--text-secondary);cursor:pointer}
.bf-drop[data-over="true"]{border-color:rgb(var(--accent-rgb) / 70%);background:rgb(var(--accent-rgb) / 6%)}
.bf-timeline{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:var(--space-3)}
.bf-timeline li{display:grid;grid-template-columns:18px 1fr;gap:var(--space-3);align-items:start;font-size:var(--font-size-sm)}
.bf-timeline .bf-dot{width:12px;height:12px;margin-top:4px;border-radius:50%;background:var(--color-success)}
.bf-timeline .bf-dot--open{background:transparent;border:2px solid var(--border-strong)}
.bf-timeline .bf-dot--red{background:var(--color-danger)}
.bf-head-actions{display:flex;gap:var(--space-2);flex-wrap:wrap}
.btn--gold{background:var(--regalis-gold,#b8954a);border-color:var(--regalis-gold,#b8954a);color:#1a1a1a}
.btn--gold:hover:not(:disabled){filter:brightness(1.05)}
.modal:has(.bf-modal){max-width:620px}
.modal:has(.bf-modal--wide){max-width:960px}
.bf-crumbs{font-size:var(--font-size-sm);color:var(--text-muted);margin-bottom:var(--space-2)}
.bf-crumbs button{all:unset;cursor:pointer;color:var(--text-secondary)}
.bf-crumbs button:hover{text-decoration:underline}
@media(max-width:560px){.bf-band{grid-template-columns:minmax(0,1fr)}}
`;

export function useStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }, []);
}

// ── Small components ─────────────────────────────────────────────────────────

export const pill = (label, tone = "neutral") => e.jsx("span", { className: `bf-pill bf-pill--${tone}`, children: label });

export function StatCard({ label, value, sub, danger, onClick, pressed }) {
  const inner = [
    e.jsx("div", { className: "bf-card__label", children: label }, "l"),
    e.jsx("div", { className: `bf-card__value${danger ? " bf-card__value--danger" : ""}`, children: value }, "v"),
    sub ? e.jsx("div", { className: "bf-card__sub", children: sub }, "s") : null
  ];
  return onClick
    ? e.jsx("button", { type: "button", className: "bf-card", onClick, "aria-pressed": pressed ? "true" : "false", children: inner })
    : e.jsx("div", { className: "bf-card", children: inner });
}

export function Tabs({ tabs, value, onChange }) {
  return e.jsx("div", {
    className: "bf-tabs",
    role: "tablist",
    children: tabs.map((t) => e.jsx("button", { type: "button", role: "tab", className: "bf-tab", "aria-selected": value === t.value, onClick: () => onChange(t.value), children: t.label }, t.value))
  });
}

export function Chips({ options, value, onChange }) {
  return e.jsx("div", {
    className: "bf-chips",
    children: options.map((o) => e.jsx("button", {
      type: "button",
      className: "bf-chip",
      "aria-pressed": value === o.value,
      "aria-disabled": o.disabled ? "true" : undefined,
      title: o.title,
      onClick: () => !o.disabled && onChange(o.value),
      children: o.label
    }, o.value))
  });
}

export function SearchBox({ value, onChange, placeholder }) {
  return e.jsxs("div", { className: "bf-search", children: [
    e.jsx(IconSearch, { size: 16 }),
    e.jsx("input", { className: "field__input", type: "search", value, placeholder: placeholder ?? "Search", "aria-label": placeholder ?? "Search", onChange: (ev) => onChange(ev.target.value) })
  ] });
}

export function Pager({ result, page, setPage }) {
  if (!result || result.totalPages <= 1) return null;
  return e.jsxs("div", { className: "bf-pager", children: [
    e.jsx("span", { children: `Page ${result.page} of ${result.totalPages} · ${result.totalCount} rows` }),
    e.jsxs("div", { children: [
      e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasPrevious, onClick: () => setPage(page - 1), children: "Previous" }),
      e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasNext, onClick: () => setPage(page + 1), children: "Next" })
    ] })
  ] });
}

/** A plain table: columns = [{ key, header, num, render(row) }]. */
export function Table({ columns, rows, rowKey, rowClass, onRowClick, footer, empty, loading }) {
  if (loading && !rows) return e.jsx(Spinner, { center: true });
  if (!rows || rows.length === 0) return empty ?? e.jsx(EmptyState, { title: "Nothing here yet" });
  return e.jsx("div", {
    className: "bf-table-wrap",
    style: { opacity: loading ? 0.6 : 1 },
    children: e.jsxs("table", { className: "bf-table", children: [
      e.jsx("thead", { children: e.jsx("tr", { children: columns.map((c) => e.jsx("th", { className: c.num ? "bf-num" : undefined, style: c.width ? { width: c.width } : undefined, children: c.header }, c.key)) }) }),
      e.jsx("tbody", { children: rows.map((row, i) => e.jsx("tr", {
        className: [rowClass ? rowClass(row) : "", onRowClick ? "bf-row--click" : ""].filter(Boolean).join(" ") || undefined,
        onClick: onRowClick ? (ev) => { if (!ev.target.closest("button,a,input,select,label,textarea")) onRowClick(row); } : undefined,
        children: columns.map((c) => e.jsx("td", { className: c.num ? "bf-num" : undefined, children: c.render(row) }, c.key))
      }, rowKey ? rowKey(row) : i)) }),
      footer ? e.jsx("tfoot", { children: footer }) : null
    ] })
  });
}

/** Asks for a reason (or any one line of text) and calls onSubmit(text). */
export function ReasonModal({ title, intro, label = "Reason", confirmText = "Confirm", danger, required = true, onClose, onSubmit }) {
  const toast = useToast();
  const [text, setText] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    if (required && !text.trim()) return;
    setBusy(true);
    try {
      await onSubmit(text.trim());
    } catch (error) {
      toast.error(errorText(error, "That did not work."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { variant: danger ? "danger" : "primary", onClick: submit, loading: busy, disabled: busy || (required && !text.trim()), children: confirmText })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      intro ? e.jsx("p", { style: { margin: 0 }, children: intro }) : null,
      e.jsx(Input, { label, required, maxLength: 300, autoFocus: true, value: text, onChange: (ev) => setText(ev.target.value) })
    ] })
  });
}

/** A file picker with drag and drop. accept like ".pdf,.jpg,.jpeg,.png". */
export function DropZone({ accept, label, onFile, multiple }) {
  const [over, setOver] = React.useState(false);
  const input = React.useRef(null);
  const take = (files) => { for (const f of Array.from(files ?? [])) { onFile(f); if (!multiple) break; } };
  return e.jsxs("div", {
    className: "bf-drop",
    "data-over": over,
    role: "button",
    tabIndex: 0,
    onClick: () => input.current?.click(),
    onKeyDown: (ev) => { if (ev.key === "Enter" || ev.key === " ") input.current?.click(); },
    onDragOver: (ev) => { ev.preventDefault(); setOver(true); },
    onDragLeave: () => setOver(false),
    onDrop: (ev) => { ev.preventDefault(); setOver(false); take(ev.dataTransfer.files); },
    children: [
      e.jsx(IconUpload, { size: 22 }),
      e.jsx("span", { children: label ?? "Drop a file here, or click to browse" }),
      e.jsx("input", { ref: input, type: "file", accept, multiple, hidden: true, onChange: (ev) => { take(ev.target.files); ev.target.value = ""; } })
    ]
  });
}

/** Names the current page in the Billing & Finance breadcrumb, which the layout draws
 * (Dashboard › Billing & Finance › Section › this). Renders nothing itself. */
export function Crumbs({ items }) {
  const label = items?.[items.length - 1]?.label;
  React.useEffect(() => {
    if (!label) return;
    window.dispatchEvent(new CustomEvent("rg:crumb", { detail: { path: window.location.pathname, label } }));
  }, [label]);
  return null;
}

// ── Record payment (build doc §3) ─────────────────────────────────────────────

export const PAYMENT_MODES = [
  { value: 1, label: "Cash" },
  { value: 2, label: "UPI" },
  { value: 3, label: "Bank transfer" },
  { value: 4, label: "Cheque" },
  { value: 5, label: "Card" },
  { value: 6, label: "Wallet" }
];
export const MODE_TONE = { 1: "success", 2: "info", 3: "neutral", 4: "warning", 5: "info", 6: "warning", 7: "neutral", 8: "danger" };
const REFERENCE_LABEL = { 2: "UPI id or UTR", 3: "UTR", 4: "Cheque number", 5: "Last 4 digits or terminal reference" };

/**
 * The Record payment dialog: amount (pre-filled with the outstanding), date, mode chips,
 * reference, bank account, note, and a live sentence saying what saving will do. The
 * server re-checks everything inside one transaction.
 */
export function RecordPaymentDialog({ invoiceId, onClose, onDone }) {
  const toast = useToast();
  const [ctx, setCtx] = React.useState(null);
  const [amount, setAmount] = React.useState("");
  const [paidOn, setPaidOn] = React.useState(istToday());
  const [mode, setMode] = React.useState(1);
  const [reference, setReference] = React.useState("");
  const [account, setAccount] = React.useState("");
  const [note, setNote] = React.useState("");
  const [errors, setErrors] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  const amountRef = React.useRef(null);

  React.useEffect(() => {
    api.get(`/invoices/${invoiceId}/payment-context`).then((c) => {
      setCtx(c);
      setAmount(String(c.outstanding));
      if (c.bankAccounts.length === 1) setAccount(String(c.bankAccounts[0].bankAccountId));
    }).catch((error) => { toast.error(errorText(error, "Could not open the invoice.")); onClose(); });
  }, [invoiceId]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!ctx) return e.jsx(Modal, { open: true, title: "Record payment", onClose, children: e.jsx(Spinner, { center: true }) });

  const value = amountOf(amount);
  const wallet = mode === 6;
  const needsRef = mode !== 1 && mode !== 6;
  const needsAccount = mode === 3 || mode === 4 || mode === 5;
  const cap = wallet ? Math.min(ctx.outstanding, ctx.walletBalance) : ctx.outstanding;
  const after = value !== null ? Math.max(0, ctx.outstanding - value) : ctx.outstanding;

  const chooseMode = (m) => {
    setMode(m);
    setErrors({});
    if (m === 6 && (amountOf(amount) ?? 0) > ctx.walletBalance) setAmount(String(Math.min(ctx.outstanding, ctx.walletBalance)));
  };

  const submit = async () => {
    const errs = {};
    if (value === null || value <= 0) errs.amount = "Enter the amount received.";
    else if (value > ctx.outstanding) errs.amount = `This is more than the ${money(ctx.outstanding)} outstanding. Reduce the amount, or add the extra to the customer's wallet separately.`;
    else if (wallet && value > ctx.walletBalance) errs.amount = `The wallet only has ${money(ctx.walletBalance)}. Record ${money(ctx.walletBalance)} now and the rest will stay outstanding.`;
    if (!paidOn) errs.paidOn = "Choose the date the money was received.";
    else if (paidOn > istToday()) errs.paidOn = "The payment date cannot be in the future.";
    else if (paidOn < ctx.invoiceDate) errs.paidOn = "The payment date cannot be before the invoice date.";
    if (needsRef && !reference.trim()) errs.reference = "Enter the reference for this payment mode.";
    if (needsAccount && !account) errs.account = "Choose which account the money went into.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const result = await api.post(`/invoices/${invoiceId}/payments`, {
        amount: value,
        paidOn,
        mode,
        reference: needsRef ? reference.trim() : null,
        bankAccountId: needsAccount || (mode === 2 && account) ? Number(account) : null,
        note: note.trim() || null
      });
      toast.success(result.message);
      onDone?.(result);
    } catch (error) {
      toast.error(errorText(error, "Could not record the payment."));
    } finally {
      setBusy(false);
    }
  };

  const modeChips = PAYMENT_MODES.map((m) => m.value === 6
    ? { ...m, label: `Wallet · ${money(ctx.walletBalance)}`, disabled: ctx.walletBalance <= 0, title: ctx.walletBalance <= 0 ? "No wallet balance" : undefined }
    : m);
  const accountOptions = [{ value: "", label: "Choose an account" }, ...ctx.bankAccounts.map((a) => ({ value: String(a.bankAccountId), label: a.label }))];

  return e.jsx(Modal, {
    open: true,
    title: `Record payment — ${ctx.invoiceNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { className: "btn--gold", onClick: submit, loading: busy, disabled: busy || ctx.outstanding <= 0, children: "Save payment" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx("div", { className: "bf-sub", style: { marginTop: -8 }, children: `${ctx.invoiceNumber} · ${ctx.customerName}` }),
      e.jsxs("div", { className: "bf-band", children: [
        e.jsxs("div", { children: [e.jsx("span", { children: "Invoice total" }), e.jsx("b", { children: money(ctx.invoiceTotal) })] }),
        e.jsxs("div", { children: [e.jsx("span", { children: "Already paid" }), e.jsx("b", { children: money(ctx.paidAmount) })] }),
        e.jsxs("div", { children: [e.jsx("span", { children: "Outstanding" }), e.jsx("b", { className: "bf-strong", children: money(ctx.outstanding) })] })
      ] }),
      ctx.periodLockedToday ? e.jsx("div", { className: "bf-note bf-note--warn", children: "This month is locked. Date the payment in an open month, or unlock it in Settings." }) : null,
      e.jsxs("div", { children: [
        e.jsx(Input, { ref: amountRef, label: "Amount received (₹)", required: true, inputMode: "decimal", value: amount, error: errors.amount, autoFocus: true, onChange: (ev) => setAmount(ev.target.value) }),
        e.jsx("div", { className: "bf-chips", style: { marginTop: 6 }, children: [
          e.jsx("button", { type: "button", className: "bf-chip", onClick: () => setAmount(String(cap)), children: `Full ${money(cap)}` }, "full"),
          e.jsx("button", { type: "button", className: "bf-chip", onClick: () => { setAmount(""); amountRef.current?.focus(); }, children: "Part" }, "part")
        ] })
      ] }),
      e.jsx(Input, { label: "Date received", type: "date", required: true, max: istToday(), min: ctx.invoiceDate, value: paidOn, error: errors.paidOn,
        hint: "Cannot be in the future or before the invoice date.", onChange: (ev) => setPaidOn(ev.target.value) }),
      e.jsxs("div", { children: [
        e.jsx("div", { className: "field__label", style: { marginBottom: 6 }, children: "How was it paid? *" }),
        e.jsx(Chips, { options: modeChips, value: mode, onChange: chooseMode })
      ] }),
      wallet
        ? e.jsxs("div", { className: "bf-sum bf-note bf-note--info", children: [
          e.jsxs("div", { children: [e.jsx("span", { children: "Wallet balance" }), e.jsx("span", { children: money(ctx.walletBalance) })] }),
          e.jsxs("div", { children: [e.jsx("span", { children: "This payment" }), e.jsx("span", { children: money(value ?? 0) })] }),
          e.jsxs("div", { className: "bf-strong", children: [e.jsx("span", { children: "Balance after" }), e.jsx("span", { children: money(Math.max(0, ctx.walletBalance - (value ?? 0))) })] })
        ] })
        : e.jsxs("div", { className: "bf-form-grid", children: [
          needsRef ? e.jsx(Input, { label: REFERENCE_LABEL[mode] ?? "Reference", required: true, maxLength: 60, value: reference, error: errors.reference,
            hint: "UTR, UPI id, cheque number. Not asked for Cash.", onChange: (ev) => setReference(ev.target.value) }) : null,
          mode === 1
            ? e.jsx("div", { className: "bf-sub", style: { alignSelf: "center" }, children: "Cash goes to the cash drawer instead." })
            : e.jsx(Select, { label: "Into which account", required: needsAccount, value: account, error: errors.account, options: accountOptions, onChange: (ev) => setAccount(ev.target.value) })
        ] }),
      e.jsx(Input, { label: "Note", maxLength: 300, value: note, onChange: (ev) => setNote(ev.target.value) }),
      e.jsx("div", { className: "bf-note bf-note--ok", children: value === null || value <= 0
        ? "Enter the amount to see what will happen."
        : after <= 0
          ? `After saving, ${ctx.invoiceNumber} becomes Paid, outstanding ${money(0)}. A receipt numbered ${ctx.nextReceiptNumber} is generated and can be printed or emailed.`
          : `After saving, ${ctx.invoiceNumber} becomes Part paid with ${money(after)} still outstanding. A receipt numbered ${ctx.nextReceiptNumber} is generated and can be printed or emailed.` }),
      ctx.payments.length > 0 ? e.jsxs("div", { className: "bf-sub", children: [
        "Earlier on this invoice: ",
        ctx.payments.map((p) => `${p.receiptNumber} ${p.modeName} ${money(p.amount)}${p.isReversed ? " (reversed)" : ""}`).join(" · ")
      ] }) : null
    ] })
  });
}

// ── Party ledger (build doc §7.2) ─────────────────────────────────────────────

/** Ledger for one customer or supplier, with a running balance, FY picker, Excel, PDF, email. */
export function LedgerPanel({ partyType, partyId }) {
  const toast = useToast();
  const [fy, setFy] = React.useState(currentFy());
  const [emailing, setEmailing] = React.useState(false);
  const range = React.useMemo(() => {
    const y = Number(fy.slice(0, 4));
    const to = `${y + 1}-03-31`;
    return { from: `${y}-04-01`, to: to < istToday() ? to : istToday() };
  }, [fy]);
  const { data, loading } = useLoad(() => api.get(`/parties/${partyType}/${partyId}/ledger`, range), [partyType, partyId, range]);
  const supplier = partyType === "supplier";
  const columns = [
    { key: "date", header: "Date", render: (r) => fmtDate(r.date) },
    { key: "what", header: "What", render: (r) => r.kind },
    { key: "ref", header: "Reference", render: (r) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: r.reference }), r.detail ? e.jsx("div", { className: "bf-sub", children: r.detail }) : null] }) },
    { key: "debit", header: "Debit", num: true, render: (r) => (r.debit ? money(r.debit) : "") },
    { key: "credit", header: "Credit", num: true, render: (r) => (r.credit ? money(r.credit) : "") },
    { key: "balance", header: "Balance", num: true, render: (r) => e.jsx("span", { className: "bf-strong", children: money(r.balance) }) }
  ];
  const rows = data ? [{ date: data.from, kind: "Opening balance", reference: "", debit: 0, credit: 0, balance: data.openingBalance, opening: true }, ...data.rows] : null;
  return e.jsxs("div", { className: "bf-panel", children: [
    e.jsxs("div", { style: { display: "flex", justifyContent: "space-between", gap: 16, flexWrap: "wrap", alignItems: "flex-end" }, children: [
      e.jsxs("div", { children: [
        e.jsx("h3", { className: "bf-panel__title", style: { marginBottom: 4 }, children: data?.partyName ?? "Ledger" }),
        data ? e.jsx("div", { className: "bf-sub", children: [data.gstin ? `GSTIN ${data.gstin}` : "No GSTIN", data.taxId ? `${supplier ? "PAN" : "TAN"} ${data.taxId}` : null, data.paymentTerms || null,
          `${fmtDate(data.from)} – ${fmtDate(data.to)}`].filter(Boolean).join(" · ") }) : null
      ] }),
      e.jsxs("div", { className: "bf-head-actions", children: [
        e.jsx(Select, { label: "Financial year", value: fy, options: fyOptions(), onChange: (ev) => setFy(ev.target.value) }),
        e.jsx(Button, { variant: "outline", onClick: () => api.download(`/parties/${partyType}/${partyId}/ledger/export`, { ...range, format: "xlsx" }, "ledger.xlsx").catch((x) => toast.error(errorText(x, "Download failed."))), children: "Excel" }),
        e.jsx(Button, { variant: "outline", onClick: () => api.download(`/parties/${partyType}/${partyId}/ledger/export`, { ...range, format: "pdf" }, "statement.pdf").catch((x) => toast.error(errorText(x, "Download failed."))), children: "PDF" }),
        e.jsx(Button, { variant: "outline", onClick: () => setEmailing(true), children: "Email statement" })
      ] })
    ] }),
    e.jsx("div", { style: { height: 12 } }),
    e.jsx(Table, { columns, rows, loading, rowKey: (r) => `${r.kind}-${r.reference}-${r.date}-${r.balance}`, rowClass: (r) => (r.opening ? "bf-row--muted" : ""),
      footer: data ? e.jsxs("tr", { children: [
        e.jsx("td", { colSpan: 3, children: "Closing balance" }),
        e.jsx("td", { className: "bf-num", children: money(data.totalDebit) }),
        e.jsx("td", { className: "bf-num", children: money(data.totalCredit) }),
        e.jsx("td", { className: "bf-num", children: money(data.closingBalance) })
      ] }) : null,
      empty: e.jsx(EmptyState, { title: "No entries in this year" }) }),
    e.jsx("p", { className: "bf-sub", children: supplier
      ? "A bill is a credit: it raises what Regalis owes. A payment, a TDS deduction and an advance are debits. Nothing here is stored; it is assembled from the bills and payments every time."
      : "An invoice is a debit: it raises what the customer owes. A payment, TDS they deducted and a short payment accepted are credits." }),
    emailing ? e.jsx(ReasonModal, {
      title: "Email statement",
      intro: `Sends this ledger as a PDF on the Regalis letterhead, with the closing balance and a line asking ${data?.partyName ?? "them"} to confirm it. Leave the address empty to use the one on record${data?.email ? ` (${data.email})` : ""}.`,
      label: "Send to (optional)",
      required: false,
      confirmText: "Send",
      onClose: () => setEmailing(false),
      onSubmit: async (address) => {
        const r = await api.postWithMessage(`/parties/${partyType}/${partyId}/statement/email`, { ...range, toAddress: address || null });
        toast.success(r.message || "Statement sent.");
        setEmailing(false);
      }
    }) : null
  ] });
}
