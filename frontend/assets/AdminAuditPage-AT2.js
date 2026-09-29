/**
 * Admin -> Administration -> Audit Trail  (/admin/audit)
 *
 * Audit Trail Enhancements v1.0 (28 Sep 2026): search, Module / Action / User / Result
 * filters, active-filter chips, a Result column, a readable change summary, IP / device,
 * and a read-only details drawer (before/after diff, raw JSON, related records).
 *
 * Every filter runs on the server (GET /api/admin/audit…); filters live in the URL so a
 * view can be bookmarked or shared. Nothing on this page can edit or delete a record.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js lazy-loads this file
 * for /admin/audit. A new file, so no sw.js precache entry is needed.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as e,
  G as Spinner,
  p as Button,
  D as unwrap,
  E as http,
  i as makeIcon,
  u as useNavigate
} from "./index-DI5X4c9f.js";

// ── API ─────────────────────────────────────────────────────────────────────

const BASE = "/admin/audit";
const api = {
  list: (params) => unwrap(http.get(BASE, { params })),
  detail: (id) => unwrap(http.get(`${BASE}/${id}`)),
  related: (id) => unwrap(http.get(`${BASE}/${id}/related`)),
  lookups: (userSearch) => unwrap(http.get(`${BASE}/lookups`, { params: { userSearch: userSearch || undefined } })),
  export: async (params) => {
    const response = await http.get(`${BASE}/export`, { params, responseType: "blob" });
    const disposition = response.headers?.["content-disposition"] ?? "";
    const named = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1];
    const href = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = href;
    link.download = named ? decodeURIComponent(named) : "audit.csv";
    link.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  }
};

// ── Helpers ─────────────────────────────────────────────────────────────────

const IST = "Asia/Kolkata";
const asDate = (v) => new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(v) ? v : `${v}Z`);
const fmtDay = (v) => asDate(v).toLocaleDateString("en-IN", { timeZone: IST, day: "numeric", month: "short", year: "numeric" });
// Seconds matter here: many events land in the same minute (a sign-in and what it did).
const fmtTime = (v) => asDate(v).toLocaleTimeString("en-IN", { timeZone: IST, hour: "2-digit", minute: "2-digit", second: "2-digit", hour12: true });
const errorText = (error, fallback) =>
  error instanceof ApiError ? error.response?.errors?.[0]?.message ?? error.message : fallback;
const RESULTS = ["Success", "Failed", "Denied"];
const PAGE_SIZES = [25, 50, 100];

/** "Open <Entity>" targets. Detail screens where one exists, else the module's list. */
const ENTITY_ROUTES = {
  Quotation: (id) => `/admin/billing/quotations/${id}`,
  PurchaseOrder: (id) => `/admin/billing/purchase-orders/${id}`,
  Ticket: (id) => `/admin/tickets/${id}`,
  Invoice: () => "/admin/invoices",
  Payment: () => "/admin/payments",
  Booking: () => "/admin/bookings",
  Workspace: () => "/admin/workspaces",
  Supplier: () => "/admin/suppliers",
  Expense: () => "/admin/expenses",
  MaintenanceWorkItem: () => "/admin/housekeeping",
  HousekeepingSchedule: () => "/admin/housekeeping",
  InventoryItem: () => "/admin/inventory/items",
  Location: () => "/admin/locations",
  Role: () => "/admin/roles",
  Banner: () => "/admin/banners"
};
const SESSION_ACTIONS = ["LOGIN", "LOGOUT", "LOGIN_FAILED", "EXPORT", "PASSWORD_CHANGE", "AUTHORIZATION_FAILED"];

const IconSearch = makeIcon("search", [
  ["circle", { cx: "11", cy: "11", r: "8", key: "4ej97u" }],
  ["path", { d: "m21 21-4.3-4.3", key: "1qie3q" }]
]);
const IconEye = makeIcon("eye", [
  ["path", { d: "M2.062 12.348a1 1 0 0 1 0-.696 10.75 10.75 0 0 1 19.876 0 1 1 0 0 1 0 .696 10.75 10.75 0 0 1-19.876 0", key: "1nclc0" }],
  ["circle", { cx: "12", cy: "12", r: "3", key: "1v7zrd" }]
]);

// ── Styles ──────────────────────────────────────────────────────────────────

const STYLE_ID = "audit-trail-styles";
const STYLES = `
.at-card{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);padding:var(--space-4) var(--space-5) var(--space-5)}
.at-filters{display:grid;grid-template-columns:minmax(220px,2.2fr) repeat(4,minmax(125px,1fr)) repeat(2,minmax(135px,1fr));gap:10px;align-items:end}
@media(max-width:1200px){.at-filters{grid-template-columns:repeat(4,minmax(0,1fr))}.at-f--search{grid-column:span 4}}
.at-f{min-width:0}
.at-f > label,.at-f > .at-lbl{display:block;font-size:var(--font-size-xs);font-weight:700;color:var(--text-secondary);margin-bottom:4px}
.at-ctl{box-sizing:border-box;width:100%;height:40px;display:flex;align-items:center;gap:6px;padding:0 10px;border:1px solid var(--border-strong);border-radius:var(--radius-md);background:var(--surface-input,var(--surface-card));color:var(--text-primary);font:inherit;font-size:var(--font-size-sm);text-align:left;cursor:pointer}
.at-ctl--on{border-color:#CC9A3D;box-shadow:0 0 0 2px rgb(204 154 61 / 18%)}
.at-ctl input{border:0;outline:0;background:transparent;flex:1;min-width:0;height:100%;font:inherit;color:inherit}
.at-ctl input[type=date]{cursor:text}
.at-ctl .at-v{flex:1;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.at-ctl .at-caret{color:var(--text-muted);font-size:10px}
.at-ctl svg{flex-shrink:0;color:var(--text-muted)}
.at-x{all:unset;cursor:pointer;color:var(--text-muted);padding:2px 4px;font-size:15px;line-height:1}
.at-x:focus-visible{outline:2px solid #CC9A3D}
.at-hint{font-size:var(--font-size-xs);color:var(--text-muted);margin-top:4px}
.at-dd{position:relative}
.at-menu{position:absolute;z-index:40;top:calc(100% + 4px);left:0;min-width:100%;width:max-content;max-width:300px;background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-md);box-shadow:var(--shadow-lg);padding:6px}
.at-menu__list{max-height:260px;overflow:auto;display:flex;flex-direction:column}
.at-opt{all:unset;box-sizing:border-box;display:flex;align-items:center;gap:8px;padding:7px 8px;border-radius:6px;cursor:pointer;font-size:var(--font-size-sm)}
.at-opt:hover,.at-opt:focus-visible,.at-opt--sel{background:var(--surface-hover)}
.at-opt input{accent-color:#CC9A3D;width:15px;height:15px;pointer-events:none}
.at-opt small{display:block;color:var(--text-muted);font-size:11px}
.at-menu__search{box-sizing:border-box;width:100%;height:34px;border:1px solid var(--border-strong);border-radius:6px;padding:0 8px;margin-bottom:4px;font:inherit;font-size:var(--font-size-sm);background:var(--surface-card);color:var(--text-primary)}
.at-menu__foot{display:flex;justify-content:space-between;border-top:1px solid var(--border-subtle);margin-top:4px;padding:6px 4px 2px}
.at-link{all:unset;cursor:pointer;color:#A8761B;font-weight:700;font-size:var(--font-size-xs);padding:2px 4px}
.at-link:hover,.at-link:focus-visible{text-decoration:underline}
.at-chips{display:flex;gap:6px;align-items:center;flex-wrap:wrap;margin:12px 0 2px;min-height:26px;font-size:var(--font-size-xs);color:var(--text-muted)}
.at-chip{display:inline-flex;align-items:center;gap:2px;padding:2px 4px 2px 10px;border-radius:999px;background:rgb(var(--accent-rgb) / 16%);border:1px solid rgb(204 154 61 / 45%);color:var(--text-primary);font-weight:600}
.at-chip button{all:unset;cursor:pointer;padding:0 5px;font-size:13px}
.at-meta{display:flex;justify-content:space-between;align-items:center;gap:10px;flex-wrap:wrap;margin:10px 0 8px;font-size:var(--font-size-sm);color:var(--text-secondary)}
.at-meta b{color:var(--text-primary)}
.at-meta select{height:30px;border:1px solid var(--border-strong);border-radius:6px;background:var(--surface-card);color:var(--text-primary);padding:0 6px}
.at-tw{overflow-x:auto;position:relative}
.at-table{width:100%;border-collapse:collapse;min-width:1080px;font-size:var(--font-size-sm)}
.at-table th{padding:10px 8px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.05em;text-transform:uppercase;color:var(--text-secondary);background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.at-th-sort{all:unset;cursor:pointer}
.at-th-sort:focus-visible{outline:2px solid #CC9A3D}
.at-table td{padding:10px 8px;border-bottom:1px solid var(--border-subtle);vertical-align:middle}
.at-table tbody tr{cursor:pointer}
.at-table tbody tr:hover td{background:var(--surface-hover)}
.at-table tbody tr.at-cur td{background:rgb(var(--accent-rgb) / 14%)}
.at-muted{color:var(--text-muted);font-size:var(--font-size-xs)}
.at-user{all:unset;cursor:pointer;font-weight:600;color:var(--text-primary)}
.at-user:hover,.at-user:focus-visible{color:#A8761B;text-decoration:underline}
.at-tag{display:inline-block;font-size:10.5px;font-weight:800;letter-spacing:.04em;padding:3px 8px;border-radius:5px;white-space:nowrap;background:#EFE9DD;color:#5D5344}
.at-tag--CREATE,.at-tag--APPROVE{background:#E2F2E7;color:#2F7D4F}
.at-tag--UPDATE{background:#E3ECF8;color:#2F5F9E}
.at-tag--DELETE,.at-tag--LOGIN_FAILED,.at-tag--REJECT,.at-tag--AUTHORIZATION_FAILED{background:#F8E1E1;color:#B23A3A}
.at-res{display:inline-flex;align-items:center;gap:5px;font-weight:700;font-size:var(--font-size-xs);padding:3px 9px;border-radius:999px;white-space:nowrap}
.at-res i{width:7px;height:7px;border-radius:50%;background:currentColor}
.at-res--Success{background:#E2F2E7;color:#2F7D4F}
.at-res--Failed{background:#F8E1E1;color:#B23A3A}
.at-res--Denied{background:#FBEFD6;color:#8F6310}
.at-summary{max-width:290px;overflow-wrap:anywhere}
.at-view{all:unset;cursor:pointer;display:inline-flex;align-items:center;gap:5px;padding:5px 10px;border:1px solid rgb(204 154 61 / 55%);border-radius:6px;background:rgb(var(--accent-rgb) / 12%);color:#6B4D10;font-weight:700;font-size:var(--font-size-xs);white-space:nowrap}
.at-view:hover,.at-view:focus-visible{background:rgb(var(--accent-rgb) / 24%)}
.at-empty{text-align:center;padding:46px 10px;color:var(--text-muted)}
.at-empty strong{display:block;color:var(--text-primary);font-size:var(--font-size-md);margin-bottom:4px}
.at-pager{display:flex;justify-content:space-between;align-items:center;margin-top:12px;font-size:var(--font-size-sm);color:var(--text-secondary);gap:10px;flex-wrap:wrap}
.at-pages{display:flex;gap:4px;align-items:center}
.at-pg{all:unset;box-sizing:border-box;min-width:32px;height:32px;display:grid;place-items:center;border:1px solid var(--border-strong);border-radius:6px;cursor:pointer;background:var(--surface-card);font-size:var(--font-size-sm)}
.at-pg[disabled]{opacity:.4;cursor:not-allowed}
.at-pg--on{background:#CC9A3D;border-color:#CC9A3D;color:#241F19;font-weight:800}
.at-mfilter{display:none}
.at-scrim{position:fixed;inset:0;z-index:100;background:#0b0b0d8c;opacity:0;pointer-events:none;transition:opacity .2s}
.at-scrim--on{opacity:1;pointer-events:auto}
.at-drawer{position:fixed;top:0;right:0;z-index:101;height:100%;width:min(560px,100%);background:var(--surface-card);border-left:1px solid var(--border-subtle);box-shadow:-10px 0 30px rgba(0,0,0,.15);display:flex;flex-direction:column;transform:translateX(100%);transition:transform .22s ease}
.at-drawer--on{transform:none}
@media(prefers-reduced-motion:reduce){.at-drawer,.at-scrim{transition:none}}
.at-dh{padding:16px 20px;border-bottom:1px solid var(--border-subtle);display:flex;justify-content:space-between;align-items:flex-start;gap:10px}
.at-close{all:unset;cursor:pointer;display:grid;place-items:center;width:34px;height:34px;border:1px solid var(--border-strong);border-radius:var(--radius-md);color:var(--text-secondary)}
.at-close:hover,.at-close:focus-visible{background:var(--surface-hover);outline:2px solid transparent}
.at-dh h2{font-family:var(--font-display);font-size:var(--font-size-xl);margin:0 0 6px}
.at-db{padding:16px 20px;overflow:auto;flex:1}
.at-kv{display:grid;grid-template-columns:130px 1fr;gap:6px 12px;font-size:var(--font-size-sm);margin:0 0 16px}
.at-kv dt{color:var(--text-muted)}
.at-kv dd{margin:0;overflow-wrap:anywhere}
.at-reason{border-radius:8px;padding:9px 12px;margin-bottom:14px;font-size:var(--font-size-sm);background:#F8E1E1;color:#B23A3A}
.at-reason--Denied{background:#FBEFD6;color:#8F6310}
.at-tabs{display:flex;gap:2px;border-bottom:1px solid var(--border-subtle);margin-bottom:12px}
.at-tab{all:unset;cursor:pointer;padding:8px 12px;font-size:var(--font-size-sm);color:var(--text-muted);border-bottom:2px solid transparent;margin-bottom:-1px}
.at-tab[aria-selected=true]{color:#A8761B;border-color:#CC9A3D;font-weight:800}
.at-tab:focus-visible{outline:2px solid #CC9A3D}
.at-diff{width:100%;border-collapse:collapse;border:1px solid var(--border-subtle);font-size:var(--font-size-xs);table-layout:fixed}
.at-diff th{background:rgb(var(--accent-rgb) / 8%);text-align:left;padding:7px 8px}
.at-diff td{padding:7px 8px;border-top:1px solid var(--border-subtle);font-family:ui-monospace,Consolas,monospace;overflow-wrap:anywhere;vertical-align:top}
.at-diff td:first-child{font-family:inherit;font-weight:700}
.at-old{background:#F8E1E1;color:#B23A3A;text-decoration:line-through}
.at-new{background:#E2F2E7;color:#2F7D4F}
.at-same{color:var(--text-muted)}
.at-toggle{display:flex;align-items:center;gap:6px;margin-top:10px;font-size:var(--font-size-xs);color:var(--text-muted)}
.at-toggle input{accent-color:#CC9A3D}
.at-json{background:rgb(var(--accent-rgb) / 8%);border:1px solid var(--border-subtle);border-radius:8px;padding:12px;font-size:11.5px;overflow:auto;margin:0;white-space:pre-wrap;overflow-wrap:anywhere;max-height:420px}
.at-rel{display:flex;justify-content:space-between;align-items:center;gap:8px;padding:9px 4px;border-bottom:1px solid var(--border-subtle)}
.at-df{padding:12px 20px;border-top:1px solid var(--border-subtle);display:flex;justify-content:space-between;gap:8px;flex-wrap:wrap}
.at-df > div{display:flex;gap:8px;flex-wrap:wrap}
.at-sheet-scrim{position:fixed;inset:0;z-index:100;background:#0b0b0d8c;display:flex;align-items:flex-end}
.at-sheet{width:100%;max-height:88vh;overflow:auto;background:var(--surface-card);border-radius:18px 18px 0 0;padding:16px}
.at-sheet .at-filters{grid-template-columns:1fr 1fr}
.at-sheet .at-f--search{grid-column:span 2}
.at-sheet-head{display:flex;justify-content:space-between;align-items:center;margin-bottom:12px}
.at-sheet-head h3{margin:0;font-family:var(--font-display)}
@media(max-width:860px){
.at-card > .at-filters{display:none}
.at-mfilter{display:inline-flex}
.at-drawer{width:100%;border-left:0}
}
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

// ── URL <-> filter state (§4.6: filters can be bookmarked or shared) ─────────

const DEFAULTS = { q: "", module: [], action: [], user: [], result: "", from: "", to: "", sort: "desc", page: 1, pp: 25 };
function readUrl() {
  const p = new URLSearchParams(window.location.search);
  const list = (k) => (p.get(k) ? p.get(k).split(",").filter(Boolean) : []);
  const pp = Number(p.get("pp"));
  return {
    q: p.get("q") ?? "",
    module: list("module"),
    action: list("actions"),
    user: list("users"),
    result: RESULTS.includes(p.get("result")) ? p.get("result") : "",
    from: p.get("from") ?? "",
    to: p.get("to") ?? "",
    sort: p.get("sort") === "asc" ? "asc" : "desc",
    page: Math.max(1, Number(p.get("page")) || 1),
    pp: PAGE_SIZES.includes(pp) ? pp : 25
  };
}
function writeUrl(s) {
  const p = new URLSearchParams();
  if (s.q) p.set("q", s.q);
  if (s.module.length) p.set("module", s.module.join(","));
  if (s.action.length) p.set("actions", s.action.join(","));
  if (s.user.length) p.set("users", s.user.join(","));
  if (s.result) p.set("result", s.result);
  if (s.from) p.set("from", s.from);
  if (s.to) p.set("to", s.to);
  if (s.sort !== "desc") p.set("sort", s.sort);
  if (s.page > 1) p.set("page", String(s.page));
  if (s.pp !== 25) p.set("pp", String(s.pp));
  const qs = p.toString();
  const url = `${window.location.pathname}${qs ? `?${qs}` : ""}`;
  if (url !== `${window.location.pathname}${window.location.search}`) window.history.replaceState(window.history.state, "", url);
}
const toParams = (s) => ({
  search: s.q.trim().length >= 2 ? s.q.trim() : undefined,
  module: s.module.length ? s.module.join(",") : undefined,
  actions: s.action.length ? s.action.join(",") : undefined,
  userIds: s.user.length ? s.user.join(",") : undefined,
  result: s.result || undefined,
  from: s.from || undefined,
  to: s.to || undefined,
  sort: `createdAt:${s.sort}`
});

// ── Dropdown (single or multi) ──────────────────────────────────────────────

function Dropdown({ label, placeholder, options, value, multi, onChange, searchable, onSearch, labelFor }) {
  const [open, setOpen] = React.useState(false);
  const [term, setTerm] = React.useState("");
  const ref = React.useRef(null);
  const id = React.useId();
  React.useEffect(() => {
    if (!open) return;
    const away = (ev) => ref.current && !ref.current.contains(ev.target) && setOpen(false);
    const esc = (ev) => { if (ev.key === "Escape") { ev.stopPropagation(); setOpen(false); } };
    document.addEventListener("mousedown", away);
    document.addEventListener("keydown", esc, true);
    return () => { document.removeEventListener("mousedown", away); document.removeEventListener("keydown", esc, true); };
  }, [open]);
  React.useEffect(() => { if (searchable && onSearch) { const t = setTimeout(() => onSearch(term), 300); return () => clearTimeout(t); } }, [term]);
  const nameOf = (v) => labelFor?.(v) ?? options.find((o) => o.value === v)?.label ?? v;
  const on = multi ? value.length > 0 : !!value;
  const shown = multi ? (value.length === 0 ? placeholder : value.length === 1 ? nameOf(value[0]) : `${value.length} selected`) : value ? nameOf(value) : placeholder;
  const filtered = searchable && !onSearch && term ? options.filter((o) => `${o.label} ${o.sub ?? ""}`.toLowerCase().includes(term.toLowerCase())) : options;
  const toggle = (v) => {
    if (!multi) { onChange(v); setOpen(false); return; }
    onChange(value.includes(v) ? value.filter((x) => x !== v) : [...value, v]);
  };
  return e.jsxs("div", { className: "at-f", children: [
    e.jsx("span", { className: "at-lbl", id: `${id}-l`, children: label }),
    e.jsxs("div", { className: "at-dd", ref, children: [
      e.jsxs("button", { type: "button", className: `at-ctl${on ? " at-ctl--on" : ""}`, "aria-haspopup": "listbox", "aria-expanded": open, "aria-labelledby": `${id}-l ${id}-v`, onClick: () => setOpen((o) => !o), children: [
        e.jsx("span", { className: "at-v", id: `${id}-v`, children: shown }), e.jsx("span", { className: "at-caret", "aria-hidden": true, children: "▼" })
      ] }),
      open && e.jsxs("div", { className: "at-menu", role: "listbox", "aria-multiselectable": multi || undefined, children: [
        searchable && e.jsx("input", { className: "at-menu__search", autoFocus: true, placeholder: `Search ${label.toLowerCase()}…`, "aria-label": `Search ${label.toLowerCase()}`, value: term, onChange: (ev) => setTerm(ev.target.value) }),
        e.jsxs("div", { className: "at-menu__list", children: [
          !multi && e.jsx("button", { type: "button", role: "option", "aria-selected": !value, className: `at-opt${!value ? " at-opt--sel" : ""}`, onClick: () => toggle(""), children: placeholder }),
          filtered.length === 0
            ? e.jsx("div", { className: "at-muted", style: { padding: 8 }, children: "No match" })
            : filtered.map((o) => {
                const sel = multi ? value.includes(o.value) : value === o.value;
                return e.jsxs("button", { type: "button", role: "option", "aria-selected": sel, className: `at-opt${sel ? " at-opt--sel" : ""}`, onClick: () => toggle(o.value), children: [
                  multi && e.jsx("input", { type: "checkbox", tabIndex: -1, readOnly: true, checked: sel, "aria-hidden": true }),
                  e.jsxs("span", { children: [o.label, o.sub && e.jsx("small", { children: o.sub })] })
                ] }, o.value);
              })
        ] }),
        multi && e.jsxs("div", { className: "at-menu__foot", children: [
          e.jsx("button", { type: "button", className: "at-link", onClick: () => onChange([...new Set([...value, ...filtered.map((o) => o.value)])]), children: "Select all" }),
          e.jsx("button", { type: "button", className: "at-link", onClick: () => onChange([]), children: "Clear" })
        ] })
      ] })
    ] })
  ] });
}

// ── Filter bar ──────────────────────────────────────────────────────────────

function FilterBar({ s, set, lookups, userOptions, onUserSearch, userName }) {
  const [text, setText] = React.useState(s.q);
  React.useEffect(() => { setText(s.q); }, [s.q]);
  // §4.1: 400 ms after typing stops, immediately on Enter; fewer than 2 characters sends nothing.
  React.useEffect(() => {
    if (text === s.q) return;
    const t = setTimeout(() => { if (text.trim().length !== 1) set({ q: text }); }, 400);
    return () => clearTimeout(t);
  }, [text]);
  const idQ = React.useId();
  return e.jsxs("div", { className: "at-filters", children: [
    e.jsxs("div", { className: "at-f at-f--search", children: [
      e.jsx("label", { htmlFor: idQ, children: "Search" }),
      e.jsxs("div", { className: `at-ctl${s.q ? " at-ctl--on" : ""}`, style: { cursor: "text" }, children: [
        e.jsx(IconSearch, { size: 16 }),
        e.jsx("input", { id: idQ, placeholder: "Search user, entity, ID or change…", autoComplete: "off", value: text, onChange: (ev) => setText(ev.target.value), onKeyDown: (ev) => { if (ev.key === "Enter" && text.trim().length !== 1) set({ q: text }); } }),
        text && e.jsx("button", { type: "button", className: "at-x", "aria-label": "Clear search", onClick: () => { setText(""); set({ q: "" }); }, children: "×" })
      ] }),
      text.trim().length === 1 && e.jsx("div", { className: "at-hint", children: "Type at least 2 characters." })
    ] }),
    e.jsx(Dropdown, { label: "Module", placeholder: "All modules", multi: true, value: s.module, options: lookups.modules.map((m) => ({ value: m, label: m })), onChange: (v) => set({ module: v }) }),
    e.jsx(Dropdown, { label: "Action", placeholder: "All actions", multi: true, value: s.action, options: lookups.actions.map((a) => ({ value: a, label: a })), onChange: (v) => set({ action: v }) }),
    e.jsx(Dropdown, { label: "User", placeholder: "All users", multi: true, searchable: true, onSearch: onUserSearch, value: s.user, options: userOptions, labelFor: userName, onChange: (v) => set({ user: v }) }),
    e.jsx(Dropdown, { label: "Result", placeholder: "All results", value: s.result, options: RESULTS.map((r) => ({ value: r, label: r })), onChange: (v) => set({ result: v }) }),
    e.jsx(DateField, { label: "From", value: s.from, max: s.to || undefined, onChange: (v) => set({ from: v }) }),
    e.jsx(DateField, { label: "To", value: s.to, min: s.from || undefined, onChange: (v) => set({ to: v }) })
  ] });
}

function DateField({ label, value, onChange, min, max }) {
  const id = React.useId();
  return e.jsxs("div", { className: "at-f", children: [
    e.jsx("label", { htmlFor: id, children: label }),
    e.jsx("div", { className: `at-ctl${value ? " at-ctl--on" : ""}`, style: { cursor: "text" }, children: e.jsx("input", { id, type: "date", value, min, max, onChange: (ev) => onChange(ev.target.value) }) })
  ] });
}

// ── Drawer ──────────────────────────────────────────────────────────────────

function Badges({ r }) {
  return e.jsxs("span", { style: { display: "inline-flex", gap: 6, flexWrap: "wrap" }, children: [
    e.jsx("span", { className: `at-tag at-tag--${r.action}`, children: r.action }),
    e.jsxs("span", { className: `at-res at-res--${r.result}`, children: [e.jsx("i", {}), r.result] })
  ] });
}

function Drawer({ id, onClose, onStep, canPrev, canNext, onOpenEntity }) {
  const toast = useToast();
  const [detail, setDetail] = React.useState(null);
  const [related, setRelated] = React.useState(null);
  const [tab, setTab] = React.useState("changes");
  const [showSame, setShowSame] = React.useState(false);
  const closeRef = React.useRef(null);
  React.useEffect(() => {
    if (id == null) return;
    setDetail(null); setRelated(null); setTab("changes"); setShowSame(false);
    api.detail(id).then(setDetail).catch((error) => toast.error(errorText(error, "Could not load the record.")));
  }, [id]);
  React.useEffect(() => { if (tab === "related" && id != null && related === null) api.related(id).then(setRelated).catch(() => setRelated([])); }, [tab, id, related]);
  React.useEffect(() => {
    if (id == null) return undefined;
    setTimeout(() => closeRef.current?.focus(), 60);
    const key = (ev) => {
      if (ev.key === "Escape") onClose();
      if (/INPUT|SELECT|TEXTAREA/.test(document.activeElement?.tagName ?? "")) return;
      if (ev.key === "ArrowDown" && canNext) onStep(1);
      if (ev.key === "ArrowUp" && canPrev) onStep(-1);
    };
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [id, canNext, canPrev]);

  const open = id != null;
  const r = detail?.record;
  const isSession = r && (SESSION_ACTIONS.includes(r.action) || detail.fields.length === 0) && !(detail.fields.length > 0 && r.action === "EXPORT");
  // §4.5: DELETE shows only Before values. Deletes are soft, so the row also holds an
  // "after" snapshot (IsDeleted = true …) — that is not what an admin wants to read.
  const isDelete = r?.action === "DELETE" && !!detail?.oldValues;
  const isUpdate = !isDelete && !!(detail?.oldValues && detail?.newValues);
  const rows = detail ? detail.fields.filter((f) => isDelete ? f.before != null : !isUpdate || showSame || f.changed) : [];
  const route = r && r.entityId && !/^-/.test(r.entityId) && r.action !== "DELETE" && !detail.entityDeleted && r.module !== "Auth" ? ENTITY_ROUTES[r.entityType]?.(r.entityId) : null;
  const rawJson = detail ? JSON.stringify({
    id: r.id, timestampUtc: r.timestampUtc, user: r.user, action: r.action, module: r.module, entityType: r.entityType,
    entityId: r.entityId, result: r.result, failureReason: r.failureReason, ipAddress: r.ipAddress, device: r.device,
    requestId: detail.requestId, changedFields: detail.fields.filter((f) => f.changed).map((f) => f.field),
    oldValues: detail.oldValues ? JSON.parse(detail.oldValues) : null, newValues: detail.newValues ? JSON.parse(detail.newValues) : null
  }, null, 2) : "";
  const copy = () => (navigator.clipboard ? navigator.clipboard.writeText(rawJson) : Promise.reject()).then(() => toast.success("JSON copied."), () => toast.error("Copy is not available in this browser."));

  const changesTab = () => {
    if (isSession) {
      const pairs = [
        ["Event", r.changeSummary],
        ["Account", r.user.email ?? r.user.name],
        ["IP address", r.ipAddress ?? "—"],
        ["Device", r.device ?? "—"]
      ];
      return e.jsxs(e.Fragment, { children: [
        e.jsx("table", { className: "at-diff", children: e.jsx("tbody", { children: pairs.map(([k, v]) => e.jsxs("tr", { children: [e.jsx("td", { style: { width: 130 }, children: k }), e.jsx("td", { children: v })] }, k)) }) }),
        e.jsx("p", { className: "at-toggle", children: "No field values are stored for this type of event. Passwords are never logged." })
      ] });
    }
    return e.jsxs(e.Fragment, { children: [
      e.jsxs("table", { className: "at-diff", children: [
        e.jsx("thead", { children: e.jsxs("tr", { children: [e.jsx("th", { style: { width: "34%" }, children: "Field" }), e.jsx("th", { children: "Before" }), !isDelete && e.jsx("th", { children: "After" })] }) }),
        e.jsx("tbody", { children: rows.map((f) => e.jsxs("tr", { children: [
          e.jsx("td", { children: f.field }),
          e.jsx("td", { className: detail.oldValues ? (f.changed && isUpdate ? "at-old" : isDelete ? "" : "at-same") : "at-same", children: detail.oldValues ? f.before ?? "—" : "—" }),
          !isDelete && e.jsx("td", { className: detail.newValues ? (f.changed ? "at-new" : "at-same") : "at-same", children: detail.newValues ? f.after ?? "—" : "—" })
        ] }, f.field)) })
      ] }),
      isUpdate && e.jsxs("label", { className: "at-toggle", children: [e.jsx("input", { type: "checkbox", checked: showSame, onChange: (ev) => setShowSame(ev.target.checked) }), `Show unchanged fields (${detail.fields.length - detail.changedCount})`] }),
      r.result !== "Success" && detail.newValues && e.jsx("p", { className: "at-toggle", children: "This change was not saved. “After” shows what the user tried to save." }),
      e.jsx("p", { className: "at-toggle", children: "Values are read-only; this record cannot be edited or deleted." })
    ] });
  };

  return e.jsxs(e.Fragment, { children: [
    e.jsx("div", { className: `at-scrim${open ? " at-scrim--on" : ""}`, onClick: onClose }),
    e.jsxs("aside", { className: `at-drawer${open ? " at-drawer--on" : ""}`, role: "dialog", "aria-modal": true, "aria-label": r ? `Audit record ${r.id}` : "Audit record", "aria-hidden": !open, children: [
      e.jsxs("div", { className: "at-dh", children: [
        e.jsxs("div", { children: [e.jsx("h2", { children: `Audit record #${id ?? ""}` }), r && e.jsx(Badges, { r })] }),
        e.jsx("button", { type: "button", className: "at-close", ref: closeRef, onClick: onClose, "aria-label": "Close", children: "✕" })
      ] }),
      e.jsx("div", { className: "at-db", children: !detail ? (open ? e.jsx(Spinner, { center: true }) : null) : e.jsxs(e.Fragment, { children: [
        r.failureReason && r.result !== "Success" && e.jsxs("div", { className: `at-reason at-reason--${r.result}`, children: [e.jsx("b", { children: r.result === "Denied" ? "Why it was blocked: " : "What went wrong: " }), r.failureReason] }),
        e.jsxs("dl", { className: "at-kv", children: [
          e.jsx("dt", { children: "When (IST)" }), e.jsx("dd", { children: `${fmtDay(r.timestampUtc)}, ${fmtTime(r.timestampUtc)}` }),
          e.jsx("dt", { children: "User" }), e.jsx("dd", { children: r.user.isSystem ? "System (automated job)" : `${r.user.name}${r.user.email && r.user.email !== r.user.name ? ` (${r.user.email})` : ""}${detail.userRole ? ` · Role: ${detail.userRole}` : ""}` }),
          e.jsx("dt", { children: "Module / Entity" }), e.jsx("dd", { children: `${r.module} · ${r.entityLabel}` }),
          e.jsx("dt", { children: "IP address" }), e.jsx("dd", { children: r.ipAddress ?? "—" }),
          e.jsx("dt", { children: "Device" }), e.jsx("dd", { title: detail.userAgent ?? undefined, children: r.device ?? "—" }),
          e.jsx("dt", { children: "Request ID" }), e.jsx("dd", { className: "at-muted", children: detail.requestId ?? "—" })
        ] }),
        e.jsx("div", { className: "at-tabs", role: "tablist", children: [["changes", isSession ? "Session" : isDelete ? `Before delete (${rows.length})` : `Changes (${detail.changedCount})`], ["json", "Raw JSON"], ["related", "Related records"]].map(([k, l]) =>
          e.jsx("button", { type: "button", role: "tab", className: "at-tab", "aria-selected": tab === k, onClick: () => setTab(k), children: l }, k)) }),
        tab === "changes" && changesTab(),
        tab === "json" && e.jsxs(e.Fragment, { children: [e.jsx("pre", { className: "at-json", children: rawJson }), e.jsx("div", { style: { marginTop: 8 }, children: e.jsx(Button, { variant: "outline", size: "sm", onClick: copy, children: "⧉ Copy" }) })] }),
        tab === "related" && (related === null ? e.jsx(Spinner, {}) : related.length === 0
          ? e.jsx("p", { className: "at-muted", children: "No other audit records for this entity." })
          : related.map((x) => e.jsxs("div", { className: "at-rel", children: [
              e.jsxs("div", { children: [e.jsx(Badges, { r: x }), e.jsx("div", { className: "at-muted", style: { marginTop: 4 }, children: `${fmtDay(x.timestampUtc)}, ${fmtTime(x.timestampUtc)} · ${x.user.name} · ${x.changeSummary}` })] }),
              e.jsx("button", { type: "button", className: "at-view", onClick: () => onStep(0, x.id), children: "View" })
            ] }, x.id)))
      ] }) }),
      e.jsxs("div", { className: "at-df", children: [
        e.jsxs("div", { children: [
          e.jsx(Button, { variant: "outline", size: "sm", disabled: !canPrev, onClick: () => onStep(-1), children: "‹ Previous" }),
          e.jsx(Button, { variant: "outline", size: "sm", disabled: !canNext, onClick: () => onStep(1), children: "Next ›" })
        ] }),
        e.jsxs("div", { children: [
          e.jsx(Button, { variant: "outline", size: "sm", disabled: !detail, onClick: copy, children: "⧉ Copy JSON" }),
          route && e.jsx(Button, { size: "sm", onClick: () => onOpenEntity(route), children: `Open ${r.entityLabel}` })
        ] })
      ] })
    ] })
  ] });
}

// ── Page ────────────────────────────────────────────────────────────────────

function AdminAuditPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canExport = usePermission("AUDIT.EXPORT");
  const [s, setS] = React.useState(readUrl);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [lookups, setLookups] = React.useState({ actions: [], modules: [], users: [] });
  const [userOptions, setUserOptions] = React.useState([]);
  const knownUsers = React.useRef(new Map([["system", "System"]]));
  const [drawerId, setDrawerId] = React.useState(null);
  const [exporting, setExporting] = React.useState(false);
  const [sheet, setSheet] = React.useState(false);
  const [refreshKey, setRefreshKey] = React.useState(0);
  const pendingStep = React.useRef(null);

  const set = (patch) => setS((prev) => ({ ...prev, ...patch, page: patch.page ?? 1 }));
  React.useEffect(() => { writeUrl(s); }, [s]);

  const remember = (users) => users.forEach((u) => knownUsers.current.set(u.value, u.value === "system" ? "System" : u.name));
  React.useEffect(() => {
    api.lookups().then((l) => {
      setLookups(l);
      remember(l.users);
      setUserOptions(l.users.map((u) => ({ value: u.value, label: u.value === "system" ? "System (automated jobs)" : u.name, sub: u.email ?? undefined })));
    }).catch(() => {});
  }, []);
  const searchUsers = (term) => api.lookups(term).then((l) => {
    remember(l.users);
    setUserOptions(l.users.map((u) => ({ value: u.value, label: u.value === "system" ? "System (automated jobs)" : u.name, sub: u.email ?? undefined })));
  }).catch(() => {});
  const userName = (v) => knownUsers.current.get(v) ?? `User #${v}`;

  React.useEffect(() => {
    let live = true;
    setLoading(true);
    api.list({ ...toParams(s), page: s.page, pageSize: s.pp })
      .then((d) => {
        if (!live) return;
        setData(d);
        d.items.forEach((r) => r.user.id != null && knownUsers.current.set(String(r.user.id), r.user.name));
        if (pendingStep.current && d.items.length) {
          setDrawerId(pendingStep.current === "first" ? d.items[0].id : d.items[d.items.length - 1].id);
          pendingStep.current = null;
        }
      })
      .catch((error) => live && toast.error(errorText(error, "Failed to load the audit trail.")))
      .finally(() => live && setLoading(false));
    return () => { live = false; };
  }, [s, refreshKey]);

  const items = data?.items ?? [];
  const index = items.findIndex((r) => r.id === drawerId);
  const totalPages = data ? Math.max(1, data.totalPages) : 1;
  const canPrev = drawerId != null && (index > 0 || s.page > 1);
  const canNext = drawerId != null && (index >= 0 && (index < items.length - 1 || s.page < totalPages));
  // §4.5 / AC 7: Previous / Next walk the filtered order, across pages.
  const step = (delta, directId) => {
    if (directId != null) { setDrawerId(directId); return; }
    const next = index + delta;
    if (next >= 0 && next < items.length) { setDrawerId(items[next].id); return; }
    if (delta > 0 && s.page < totalPages) { pendingStep.current = "first"; setS((p) => ({ ...p, page: p.page + 1 })); }
    if (delta < 0 && s.page > 1) { pendingStep.current = "last"; setS((p) => ({ ...p, page: p.page - 1 })); }
  };

  const doExport = async () => {
    setExporting(true);
    try {
      await api.export(toParams(s));
      toast.success(`Exported ${data?.totalCount ?? ""} record${data?.totalCount === 1 ? "" : "s"}.`);
    } catch (error) {
      toast.error(errorText(error, "Export failed."));
    } finally {
      setExporting(false);
    }
  };

  const chips = [];
  if (s.q) chips.push([`Search: “${s.q}”`, { q: "" }]);
  if (s.module.length) chips.push([`Module: ${s.module.join(", ")}`, { module: [] }]);
  if (s.action.length) chips.push([`Action: ${s.action.join(", ")}`, { action: [] }]);
  if (s.user.length) chips.push([`User: ${s.user.map(userName).join(", ")}`, { user: [] }]);
  if (s.result) chips.push([`Result: ${s.result}`, { result: "" }]);
  if (s.from || s.to) {
    const f = (d) => (d ? d.split("-").reverse().join("-") : "…");
    chips.push([`${f(s.from)} → ${f(s.to)}`, { from: "", to: "" }]);
  }
  const clearAll = () => setS({ ...DEFAULTS, pp: s.pp });

  const from = data && data.totalCount ? (data.page - 1) * data.pageSize + 1 : 0;
  const to = data ? from + items.length - 1 : 0;
  const pageButtons = [];
  for (let i = 1; i <= totalPages; i++) {
    if (totalPages > 7 && i > 2 && i < totalPages - 1 && Math.abs(i - s.page) > 1) {
      if (pageButtons[pageButtons.length - 1] !== "…") pageButtons.push("…");
      continue;
    }
    pageButtons.push(i);
  }

  const filterBar = e.jsx(FilterBar, { s, set, lookups, userOptions, onUserSearch: searchUsers, userName });

  return e.jsxs(e.Fragment, { children: [
    e.jsxs("div", { className: "page-header", children: [
      e.jsxs("div", { children: [
        e.jsx("h2", { className: "page-header__title", children: "Audit Trail" }),
        e.jsx("p", { className: "page-header__description", children: "Append-only: who did what, when, from where — nothing here can be edited or deleted." })
      ] }),
      e.jsxs("div", { className: "page-header__actions", children: [
        e.jsx("span", { className: "at-mfilter", children: e.jsx(Button, { variant: "outline", onClick: () => setSheet(true), children: `Filters${chips.length ? ` (${chips.length})` : ""}` }) }),
        e.jsx(Button, { variant: "outline", onClick: () => { setRefreshKey((k) => k + 1); toast.success("Audit trail refreshed."); }, children: "⟳ Refresh" }),
        canExport && e.jsx(Button, { variant: "outline", loading: exporting, onClick: doExport, children: "⬇ Export CSV" })
      ] })
    ] }),
    e.jsxs("section", { className: "at-card", style: { marginTop: "var(--space-5)" }, children: [
      filterBar,
      e.jsx("div", { className: "at-chips", children: chips.length === 0
        ? "No filters applied — showing all activity."
        : e.jsxs(e.Fragment, { children: [
            e.jsx("span", { children: "Active filters:" }),
            chips.map(([label, patch]) => e.jsxs("span", { className: "at-chip", children: [label, e.jsx("button", { type: "button", "aria-label": `Remove ${label}`, onClick: () => set(patch), children: "×" })] }, label)),
            e.jsx("button", { type: "button", className: "at-link", onClick: clearAll, children: "Clear all" })
          ] }) }),
      e.jsxs("div", { className: "at-meta", children: [
        e.jsx("span", { children: data ? (data.totalCount ? e.jsxs(e.Fragment, { children: ["Showing ", e.jsx("b", { children: `${from}–${to}` }), " of ", e.jsx("b", { children: data.totalCount.toLocaleString("en-IN") }), " records"] }) : "No records") : "Loading…" }),
        e.jsxs("label", { children: ["Rows per page ", e.jsx("select", { value: s.pp, onChange: (ev) => set({ pp: Number(ev.target.value) }), children: PAGE_SIZES.map((n) => e.jsx("option", { value: n, children: n }, n)) })] })
      ] }),
      loading && !data
        ? e.jsx(Spinner, { center: true })
        : e.jsx("div", { className: "at-tw", style: { opacity: loading ? 0.6 : 1 }, children: e.jsxs("table", { className: "at-table", children: [
            e.jsx("thead", { children: e.jsxs("tr", { children: [
              e.jsx("th", { "aria-sort": s.sort === "desc" ? "descending" : "ascending", children: e.jsx("button", { type: "button", className: "at-th-sort", onClick: () => set({ sort: s.sort === "desc" ? "asc" : "desc" }), children: `When (IST) ${s.sort === "desc" ? "▾" : "▴"}` }) }),
              e.jsx("th", { children: "User" }), e.jsx("th", { children: "Action" }), e.jsx("th", { children: "Module" }), e.jsx("th", { children: "Entity" }),
              e.jsx("th", { children: "Result" }), e.jsx("th", { children: "Change summary" }), e.jsx("th", { children: "IP / Device" }),
              e.jsx("th", { style: { textAlign: "right" }, children: "Details" })
            ] }) }),
            e.jsx("tbody", { children: items.length === 0
              ? e.jsx("tr", { children: e.jsx("td", { colSpan: 9, className: "at-empty", style: { cursor: "default" }, children: e.jsxs(e.Fragment, { children: [
                  e.jsx("strong", { children: "No audit records match your filters" }), "Try a different search or remove a filter.",
                  chips.length > 0 && e.jsx("div", { style: { marginTop: 12 }, children: e.jsx(Button, { variant: "outline", onClick: clearAll, children: "Clear all filters" }) })
                ] }) }) })
              : items.map((r) => e.jsxs("tr", { className: r.id === drawerId ? "at-cur" : "", onClick: () => setDrawerId(r.id), children: [
                  e.jsxs("td", { style: { whiteSpace: "nowrap" }, children: [fmtDay(r.timestampUtc), e.jsx("br", {}), e.jsx("span", { className: "at-muted", children: fmtTime(r.timestampUtc) })] }),
                  e.jsxs("td", { children: [
                    e.jsx("button", { type: "button", className: "at-user", title: "Show only this user", onClick: (ev) => {
                      ev.stopPropagation();
                      const v = r.user.isSystem ? "system" : r.user.id != null ? String(r.user.id) : null;
                      if (v) { knownUsers.current.set(v, r.user.isSystem ? "System" : r.user.name); set({ user: [v] }); toast.success(`Filtered to ${r.user.isSystem ? "System" : r.user.name}`); }
                    }, children: r.user.isSystem ? "system" : r.user.name }),
                    e.jsx("div", { className: "at-muted", children: r.user.isSystem ? "Automated jobs" : r.user.email && r.user.email !== r.user.name ? r.user.email : r.action === "LOGIN_FAILED" ? "Unknown account" : "" })
                  ] }),
                  e.jsx("td", { children: e.jsx("span", { className: `at-tag at-tag--${r.action}`, children: r.action }) }),
                  e.jsx("td", { children: r.module }),
                  e.jsx("td", { style: { whiteSpace: "nowrap" }, children: r.entityLabel }),
                  e.jsx("td", { children: e.jsxs("span", { className: `at-res at-res--${r.result}`, children: [e.jsx("i", {}), r.result] }) }),
                  e.jsx("td", { className: "at-summary", children: r.changeSummary }),
                  e.jsxs("td", { className: "at-muted", children: [r.ipAddress ?? "—", e.jsx("br", {}), r.device ?? ""] }),
                  e.jsx("td", { style: { textAlign: "right" }, children: e.jsxs("button", { type: "button", className: "at-view", "aria-label": `View audit record ${r.id}`, onClick: (ev) => { ev.stopPropagation(); setDrawerId(r.id); }, children: [e.jsx(IconEye, { size: 14 }), "View"] }) })
                ] }, r.id)) })
          ] }) }),
      data && data.totalCount > 0 && e.jsxs("div", { className: "at-pager", children: [
        e.jsx("span", { children: `Page ${s.page} of ${totalPages}` }),
        e.jsxs("div", { className: "at-pages", children: [
          e.jsx("button", { type: "button", className: "at-pg", disabled: s.page <= 1, "aria-label": "Previous page", onClick: () => set({ page: s.page - 1 }), children: "‹" }),
          pageButtons.map((p, i) => p === "…"
            ? e.jsx("span", { children: "…" }, `gap${i}`)
            : e.jsx("button", { type: "button", className: `at-pg${p === s.page ? " at-pg--on" : ""}`, "aria-current": p === s.page ? "page" : undefined, onClick: () => set({ page: p }), children: p }, p)),
          e.jsx("button", { type: "button", className: "at-pg", disabled: s.page >= totalPages, "aria-label": "Next page", onClick: () => set({ page: s.page + 1 }), children: "›" })
        ] })
      ] })
    ] }),
    sheet && e.jsx("div", { className: "at-sheet-scrim", onClick: (ev) => ev.target === ev.currentTarget && setSheet(false), children: e.jsxs("div", { className: "at-sheet", role: "dialog", "aria-modal": true, "aria-label": "Filters", children: [
      e.jsxs("div", { className: "at-sheet-head", children: [e.jsx("h3", { children: "Filters" }), e.jsx(Button, { variant: "outline", size: "sm", onClick: () => setSheet(false), children: "Done" })] }),
      filterBar,
      chips.length > 0 && e.jsx("div", { style: { marginTop: 12 }, children: e.jsx("button", { type: "button", className: "at-link", onClick: clearAll, children: "Clear all filters" }) })
    ] }) }),
    e.jsx(Drawer, { id: drawerId, onClose: () => setDrawerId(null), onStep: step, canPrev, canNext, onOpenEntity: (route) => navigate(route) })
  ] });
}

export { AdminAuditPage };
