/**
 * Admin -> Engagement -> Notifications.
 *
 * Two tabs. Incoming: every event in the space from /api/admin/notifications — one
 * row per event, read state per admin, "Needs action" worked out from the record
 * (it clears when the ticket is closed, the invoice paid, the task verified), type
 * chips with live counts, filters, day groups and infinite scroll. Announcements
 * sent: drafts, scheduled and sent announcements with channels, read counts and
 * View / Edit / Continue; the announcement form and per-admin notification
 * settings. Channels that cannot deliver on this server are called out before
 * sending and recorded as not sent.
 *
 * Hand-maintained: keep the file name (index-DI5X4c9f.js imports it by name) and
 * bump its revision in sw.js, or the service worker keeps serving the cached copy.
 */
import {
  a as v,
  R as A,
  r as React,
  w as m,
  A as k,
  j as e,
  p as l,
  F as y,
  u as useNavigate,
  x as IconCheckRead,
  b as w,
  c as T,
  I as b,
  o as I,
  s as d,
  a5 as _,
  i as makeIcon,
  U as IconUser,
  G as Spinner,
  D as unwrap,
  E as api
} from "./index-DI5X4c9f.js";
import { c as customersApi } from "./admin-DtGep2Hs.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as B } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { N, e as z } from "./finance-C0-kLKkN.js";
import { f as formatDateTime } from "./formatters-DFe5xCRi.js";
import { B as IconBell, T as IconTrash } from "./trash-2-DL_V1e8k.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { C as IconChevronLeft, a as IconChevronRight } from "./chevron-right-BikENMwC.js";

/* ── Notifications: Incoming tab ────────────────────────────────────────────
   Every event in the space arrives here from the API (/api/admin/notifications):
   one row per event, read state per admin, "needs action" worked out from the
   record itself. The feed is grouped by day, 20 at a time with infinite scroll. */

const icon = (name, parts) => makeIcon(name, parts.map(([tag, attrs], i) => [tag, { ...attrs, key: `${name}-${i}` }]));
const IconBooking = icon("calendar-check", [
  ["path", { d: "M8 2v4" }], ["path", { d: "M16 2v4" }],
  ["rect", { width: "18", height: "18", x: "3", y: "4", rx: "2" }],
  ["path", { d: "M3 10h18" }], ["path", { d: "m9 16 2 2 4-4" }],
]);
const IconSupport = icon("message-square", [
  ["path", { d: "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z" }],
]);
const IconCard = icon("credit-card", [
  ["rect", { width: "20", height: "14", x: "2", y: "5", rx: "2" }],
  ["line", { x1: "2", x2: "22", y1: "10", y2: "10" }],
]);
const IconWallet = icon("wallet", [
  ["path", { d: "M19 7V4a1 1 0 0 0-1-1H5a2 2 0 0 0 0 4h15a1 1 0 0 1 1 1v4h-3a2 2 0 0 0 0 4h3a1 1 0 0 0 1-1v-2a1 1 0 0 0-1-1" }],
  ["path", { d: "M3 5v14a2 2 0 0 0 2 2h15a1 1 0 0 0 1-1v-4" }],
]);
const IconPencil = icon("pencil", [
  ["path", { d: "M21.174 6.812a1 1 0 0 0-3.986-3.987L3.842 16.174a2 2 0 0 0-.5.83l-1.321 4.352a.5.5 0 0 0 .623.622l4.353-1.32a2 2 0 0 0 .83-.497z" }],
]);
const IconWrench = icon("wrench", [
  ["path", { d: "M14.7 6.3a1 1 0 0 0 0 1.4l1.6 1.6a1 1 0 0 0 1.4 0l3.77-3.77a6 6 0 0 1-7.94 7.94l-6.91 6.91a2.12 2.12 0 0 1-3-3l6.91-6.91a6 6 0 0 1 7.94-7.94l-3.76 3.76z" }],
]);
const IconCart = icon("shopping-cart", [
  ["circle", { cx: "8", cy: "21", r: "1" }], ["circle", { cx: "19", cy: "21", r: "1" }],
  ["path", { d: "M2.05 2.05h2l2.66 12.42a2 2 0 0 0 2 1.58h9.78a2 2 0 0 0 1.95-1.57l1.65-7.43H5.12" }],
]);
const IconMegaphone = icon("megaphone", [
  ["path", { d: "m3 11 18-5v12L3 14v-3z" }], ["path", { d: "M11.6 16.8a3 3 0 1 1-5.8-1.6" }],
]);

/* Type → chip label, icon and tint (theme status colours; no new palette). */
const TYPES = [
  { value: 1, label: "Registrations", icon: IconUser, tone: "--color-info" },
  { value: 2, label: "Bookings", icon: IconBooking, tone: "--accent-strong" },
  { value: 3, label: "Payments", icon: IconCard, tone: "--color-success" },
  { value: 4, label: "Wallet", icon: IconWallet, tone: "--color-warning" },
  { value: 5, label: "Support", icon: IconSupport, tone: "--color-danger" },
  { value: 6, label: "Account changes", icon: IconPencil, tone: "--color-info" },
  { value: 7, label: "Maintenance", icon: IconWrench, tone: "--text-secondary" },
  { value: 8, label: "Purchase", icon: IconCart, tone: "--text-secondary" },
];
const typeOf = (value) => TYPES.find((t) => t.value === value) ?? TYPES[0];

const DATE_OPTIONS = [
  { value: "", label: "Last 90 days" },
  { value: "today", label: "Today" },
  { value: "yesterday", label: "Yesterday" },
  { value: "week", label: "Last 7 days" },
  { value: "month", label: "Last 30 days" },
  { value: "custom", label: "Custom range" },
];

const notificationsApi = {
  feed: (params) => unwrap(api.get("/admin/notifications", { params })),
  read: (id) => api.post(`/admin/notifications/${id}/read`),
  readAll: () => api.post("/admin/notifications/read-all"),
};

/* The bell and the sidebar badge refresh on this signal (see index chunk). */
const announceChange = () => window.dispatchEvent(new Event("regalis:notifications-changed"));

const pad = (n) => String(n).padStart(2, "0");
const isoDay = (d) => `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`;
const parseUtc = (value) => new Date(/[zZ]|[+-]\d\d:\d\d$/.test(value) ? value : `${value}Z`);
const dayKey = (value) => isoDay(parseUtc(value));
const clock = (d) => d.toLocaleTimeString("en-IN", { hour: "2-digit", minute: "2-digit", hour12: true }).toLowerCase();

function dayLabel(key) {
  const today = new Date();
  const yesterday = new Date(today); yesterday.setDate(today.getDate() - 1);
  if (key === isoDay(today)) return "Today";
  if (key === isoDay(yesterday)) return "Yesterday";
  const [y, m, d] = key.split("-").map(Number);
  return new Date(y, m - 1, d).toLocaleDateString("en-GB", { weekday: "short", day: "numeric", month: "short", year: "numeric" });
}

function timeLabel(value) {
  const d = parseUtc(value);
  const key = isoDay(d);
  const label = dayLabel(key);
  if (label === "Today") return clock(d);
  if (label === "Yesterday") return `Yesterday, ${clock(d)}`;
  return `${d.toLocaleDateString("en-GB", { day: "numeric", month: "short" })}, ${clock(d)}`;
}

function rangeFor(preset, from, to) {
  const today = new Date();
  const back = (days) => { const d = new Date(today); d.setDate(d.getDate() - days); return isoDay(d); };
  switch (preset) {
    case "today": return { from: isoDay(today), to: isoDay(today) };
    case "yesterday": return { from: back(1), to: back(1) };
    case "week": return { from: back(6), to: isoDay(today) };
    case "month": return { from: back(29), to: isoDay(today) };
    case "custom": return { from: from || undefined, to: to || undefined };
    default: return {};
  }
}

const STYLE_ID = "notif-center-styles-2";
const STYLES = `
.nc-chips{display:flex;flex-wrap:wrap;gap:var(--space-2);margin:var(--space-4) 0}
.nc-chip{display:inline-flex;align-items:center;gap:8px;padding:7px 14px;border-radius:var(--radius-full);border:1px solid var(--border-subtle);background:var(--surface-card);color:var(--text-primary);font:inherit;font-size:var(--font-size-sm);cursor:pointer}
.nc-chip:hover{background:var(--surface-hover)}
.nc-chip--active{background:var(--text-primary);border-color:var(--text-primary);color:var(--surface-card)}
.nc-chip__count{font-size:11px;font-weight:700;padding:1px 7px;border-radius:var(--radius-full);background:rgb(var(--accent-rgb) / 18%);color:inherit}
.nc-chip--active .nc-chip__count{background:rgb(var(--accent-rgb) / 45%)}
.nc-filters{display:grid;grid-template-columns:minmax(220px,2fr) repeat(3,minmax(150px,1fr));gap:var(--space-3);align-items:end}
.nc-filters .field{margin:0}
.nc-range{display:flex;gap:var(--space-2);margin-top:var(--space-3)}
.nc-day{display:flex;align-items:center;gap:var(--space-3);margin:var(--space-5) 0 var(--space-2);font-size:var(--font-size-sm)}
.nc-day strong{color:var(--text-primary)}
.nc-day span{color:var(--text-muted)}
.nc-day::after{content:"";flex:1;height:1px;background:var(--border-subtle)}
.nc-item{position:relative;display:grid;grid-template-columns:40px 1fr auto;gap:var(--space-3);padding:var(--space-4) var(--space-4) var(--space-4) var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);cursor:pointer;margin-bottom:var(--space-2);transition:background var(--transition-fast),border-color var(--transition-fast)}
.nc-item:hover{background:var(--surface-hover)}
.nc-item--unread{background:rgb(var(--accent-rgb) / 6%);border-color:rgb(var(--accent-rgb) / 30%)}
.nc-item__dot{position:absolute;left:10px;top:22px;width:8px;height:8px;border-radius:50%;background:var(--accent-strong)}
.nc-item__icon{width:40px;height:40px;border-radius:var(--radius-md);display:grid;place-items:center}
.nc-item__title{font-weight:700;color:var(--text-primary);display:flex;flex-wrap:wrap;align-items:center;gap:8px}
.nc-item__body{color:var(--text-secondary);font-size:var(--font-size-sm);margin-top:4px;display:-webkit-box;-webkit-line-clamp:2;-webkit-box-orient:vertical;overflow:hidden}
.nc-item__meta{color:var(--text-muted);font-size:var(--font-size-xs);margin-top:6px}
.nc-item__side{display:flex;flex-direction:column;align-items:flex-end;justify-content:space-between;gap:var(--space-2);min-width:160px}
.nc-item__time{color:var(--text-muted);font-size:var(--font-size-xs);white-space:nowrap}
.nc-item__actions{display:flex;gap:var(--space-2);flex-wrap:wrap;justify-content:flex-end}
.nc-flag{font-size:10px;font-weight:700;letter-spacing:.04em;text-transform:uppercase;padding:2px 7px;border-radius:var(--radius-full);color:var(--color-danger);background:color-mix(in srgb,var(--color-danger) 12%,transparent)}
.nc-more{display:flex;justify-content:center;padding:var(--space-4) 0}
.nc-soon{padding:var(--space-8) var(--space-4);text-align:center;color:var(--text-muted)}
@media (max-width:1100px){.nc-filters{grid-template-columns:1fr 1fr}}
@media (max-width:767px){
 .nc-stats{grid-template-columns:1fr 1fr;gap:var(--space-3)}
 .nc-filters{grid-template-columns:1fr}
 .nc-item{grid-template-columns:40px 1fr;padding:var(--space-4)}
 .nc-item__dot{left:6px;top:8px}
 .nc-item__side{grid-column:1/-1;align-items:stretch;min-width:0}
 .nc-item__time{order:-1}
 .nc-item__actions{flex-direction:column}
 .nc-item__actions>*{width:100%}
}`;

function useCenterStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const tag = document.createElement("style");
    tag.id = STYLE_ID;
    tag.textContent = STYLES + ANNOUNCE_STYLES;
    document.head.appendChild(tag);
  }, []);
}

function NotificationRow({ n, onOpen, onRead }) {
  const type = typeOf(n.type);
  const Icon = type.icon;
  /* "Booking REG-000052 · Kausika RS" — the name only once, even when the label has it. */
  const meta = [n.referenceLabel, n.customerName && !(n.referenceLabel ?? "").includes(n.customerName) ? n.customerName : null]
    .filter(Boolean).join(" · ");
  return e.jsxs("div", {
    className: `nc-item${n.isRead ? "" : " nc-item--unread"}`,
    role: "button",
    tabIndex: 0,
    onClick: () => onOpen(n),
    onKeyDown: (ev) => { if (ev.key === "Enter") onOpen(n); },
    children: [
      !n.isRead && e.jsx("span", { className: "nc-item__dot", "aria-label": "Unread" }),
      e.jsx("div", {
        className: "nc-item__icon",
        style: { color: `var(${type.tone})`, background: `color-mix(in srgb, var(${type.tone}) 14%, transparent)` },
        children: e.jsx(Icon, { size: 18 }),
      }),
      e.jsxs("div", {
        style: { minWidth: 0 },
        children: [
          e.jsxs("div", {
            className: "nc-item__title",
            children: [n.title, n.needsAction && e.jsx("span", { className: "nc-flag", children: "Needs action" })],
          }),
          e.jsx("div", { className: "nc-item__body", children: n.body }),
          meta && e.jsx("div", { className: "nc-item__meta", children: meta }),
        ],
      }),
      e.jsxs("div", {
        className: "nc-item__side",
        children: [
          e.jsx("span", { className: "nc-item__time", title: parseUtc(n.createdUtc).toLocaleString("en-IN"), children: timeLabel(n.createdUtc) }),
          e.jsxs("div", {
            className: "nc-item__actions",
            children: [
              n.actionRoute && e.jsx(l, {
                size: "sm",
                onClick: (ev) => { ev.stopPropagation(); onOpen(n); },
                children: n.actionLabel || "Open",
              }),
              !n.isRead && e.jsx(l, {
                size: "sm",
                variant: "outline",
                onClick: (ev) => { ev.stopPropagation(); onRead(n); },
                children: "Mark read",
              }),
            ],
          }),
        ],
      }),
    ],
  });
}

function SummaryCard({ label, value, hint, active, onClick, danger }) {
  return e.jsxs("button", {
    type: "button",
    className: `qtn-stat${active ? " qtn-stat--active" : ""}`,
    onClick,
    disabled: !onClick,
    style: onClick ? undefined : { cursor: "default" },
    children: [
      e.jsx("div", { className: "qtn-stat__l", children: label }),
      e.jsx("div", { className: "qtn-stat__v", style: danger && value > 0 ? { color: "var(--color-danger)" } : undefined, children: value }),
      e.jsx("div", { className: "qtn-stat__h", children: hint ?? " " }),
    ],
  });
}

/* ── Announcements tab, announcement form, notification settings ──────────── */

const announcementsApi = {
  list: (params) => unwrap(api.get("/admin/announcements", { params })),
  get: (id) => unwrap(api.get(`/admin/announcements/${id}`)),
  channels: () => unwrap(api.get("/admin/announcements/channels")),
  audienceCount: (audience, customerId) => unwrap(api.get("/admin/announcements/audience-count", { params: { audience, customerId: customerId || undefined } })),
  create: (body) => unwrap(api.post("/admin/announcements", body)),
  update: (id, body) => unwrap(api.put(`/admin/announcements/${id}`, body)),
  remove: (id) => api.delete(`/admin/announcements/${id}`),
};
const settingsApi = {
  get: () => unwrap(api.get("/admin/notification-settings")),
  save: (items) => unwrap(api.put("/admin/notification-settings", { items })),
};

const AUDIENCES = [
  { value: "1", label: "All customers" },
  { value: "2", label: "Companies only" },
  { value: "3", label: "Individual members" },
  { value: "4", label: "One customer" },
];
const audienceLabel = (a) => AUDIENCES.find((x) => x.value === String(a.audience))?.label ?? "—";
const STATUS = {
  1: { label: "Draft", pill: "qtn-pill--draft" },
  2: { label: "Scheduled", pill: "qtn-pill--sent" },
  3: { label: "Sent", pill: "qtn-pill--approved" },
};
const MAX_MESSAGE = 500;
const SHORT_MESSAGE = 300;

const ANN_DATE_OPTIONS = [
  { value: "", label: "Any time" },
  { value: "month", label: "This month" },
  { value: "lastmonth", label: "Last month" },
  { value: "90", label: "Last 90 days" },
];
function annRange(preset) {
  const now = new Date();
  if (preset === "month") return { from: isoDay(new Date(now.getFullYear(), now.getMonth(), 1)) };
  if (preset === "lastmonth") return { from: isoDay(new Date(now.getFullYear(), now.getMonth() - 1, 1)), to: isoDay(new Date(now.getFullYear(), now.getMonth(), 0)) };
  if (preset === "90") { const d = new Date(now); d.setDate(d.getDate() - 89); return { from: isoDay(d) }; }
  return {};
}

const whenLabel = (a) => {
  const fmt = (v) => { const d = parseUtc(v); return `${d.toLocaleDateString("en-GB", { day: "numeric", month: "short" })}, ${clock(d)}`; };
  if (a.status === 3 && a.sentUtc) return `Sent ${fmt(a.sentUtc)}`;
  if (a.status === 2 && a.scheduledForUtc) return `Scheduled ${fmt(a.scheduledForUtc)}`;
  return "Draft";
};

/* What each channel actually did — a channel that could not deliver says so. */
function ChannelList({ a }) {
  const emailSkipped = a.sendEmail && (a.deliveryNote ?? "").includes("Email NOT sent");
  const parts = [e.jsx("span", { children: "App" }, "app")];
  if (a.sendEmail) {
    parts.push(" · ", e.jsx("span", {
      style: emailSkipped ? { color: "var(--color-danger)" } : undefined,
      title: emailSkipped ? "No mail server is configured: the email copy was not sent." : undefined,
      children: emailSkipped ? "Email (not sent)" : "Email",
    }, "email"));
  }
  if (a.sendWhatsApp) {
    parts.push(" · ", e.jsx("span", {
      style: a.whatsAppManualShare ? { color: "var(--color-danger)" } : undefined,
      title: a.whatsAppManualShare ? "No WhatsApp provider is configured: it was NOT sent. Share it by hand." : undefined,
      children: a.whatsAppManualShare ? "WhatsApp (manual share)" : "WhatsApp",
    }, "wa"));
  }
  return e.jsx("span", { children: parts });
}

function RowMenuButton({ items }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (!open) return undefined;
    const close = (ev) => { if (ref.current && !ref.current.contains(ev.target)) setOpen(false); };
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);
  const shown = items.filter(Boolean);
  if (shown.length === 0) return null;
  return e.jsxs("div", {
    ref, style: { position: "relative" },
    children: [
      e.jsx("button", { type: "button", className: "nc-iconbtn", "aria-label": "More actions", "aria-expanded": open, onClick: () => setOpen((v2) => !v2), children: "⋯" }),
      open && e.jsx("div", {
        className: "nc-menu", role: "menu",
        children: shown.map((it) => e.jsx("button", {
          type: "button", role: "menuitem", className: `nc-menu__item${it.danger ? " nc-menu__item--danger" : ""}`,
          onClick: () => { setOpen(false); it.onSelect(); }, children: it.label,
        }, it.label)),
      }),
    ],
  });
}

function AnnouncementsTab({ refreshKey, onOpen, onCopy, onChanged }) {
  const toast = v();
  const [search, setSearch] = React.useState("");
  const [term, setTerm] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [audience, setAudience] = React.useState("");
  const [date, setDate] = React.useState("");
  const [rows, setRows] = React.useState(null);
  const [page, setPage] = React.useState(1);
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(false);

  React.useEffect(() => { const t = setTimeout(() => setTerm(search.trim()), 300); return () => clearTimeout(t); }, [search]);

  const load = React.useCallback(async (pageNo) => {
    setLoading(true);
    try {
      const res = await announcementsApi.list({
        page: pageNo, pageSize: 20, search: term || undefined,
        status: status || undefined, audience: audience || undefined, ...annRange(date),
      });
      setResult(res);
      setRows((current) => (pageNo === 1 ? res.items : [...(current ?? []), ...res.items]));
      setPage(pageNo);
    } catch (error) {
      toast.error(error instanceof k ? error.message : "Could not load announcements.");
    } finally {
      setLoading(false);
    }
  }, [term, status, audience, date, refreshKey]);
  React.useEffect(() => { void load(1); }, [load]);

  const remove = async (a) => {
    if (!window.confirm(`Delete “${a.title}”? This cannot be undone.`)) return;
    try { await announcementsApi.remove(a.announcementId); toast.success("Announcement deleted."); await load(1); onChanged(); }
    catch (error) { toast.error(error instanceof k ? error.message : "Could not delete it."); }
  };

  const filtered = Boolean(term || status || audience || date);
  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("div", {
        className: "nc-filters", style: { marginTop: "var(--space-4)" },
        children: [
          e.jsx(b, { label: "Search", placeholder: "Search announcement title or text…", value: search, onChange: (ev) => setSearch(ev.target.value) }),
          e.jsx(y, { label: "Status", value: status, placeholder: "All statuses", options: [{ value: "3", label: "Sent" }, { value: "2", label: "Scheduled" }, { value: "1", label: "Draft" }], onChange: (ev) => setStatus(ev.target.value) }),
          e.jsx(y, { label: "Audience", value: audience, placeholder: "All audiences", options: AUDIENCES, onChange: (ev) => setAudience(ev.target.value) }),
          e.jsx(y, { label: "Date", value: date, options: ANN_DATE_OPTIONS, onChange: (ev) => setDate(ev.target.value) }),
        ],
      }),
      rows === null
        ? e.jsx(Spinner, { center: true })
        : rows.length === 0
          ? e.jsx(EmptyState, {
            title: filtered ? "No announcements match these filters." : "No announcements yet",
            description: filtered ? "Try another status, audience or date." : "Send your first one with “+ New announcement”.",
          })
          : e.jsxs("div", {
            className: "table-scroll nc-ann", style: { marginTop: "var(--space-4)" },
            children: [
              e.jsxs("table", {
                className: "data-table",
                children: [
                  e.jsx("thead", { children: e.jsx("tr", { children: ["Announcement", "Audience", "Channels", "When", "Status", "Read", ""].map((h) => e.jsx("th", { children: h }, h)) }) }),
                  e.jsx("tbody", {
                    children: rows.map((a) => e.jsxs("tr", {
                      children: [
                        e.jsxs("td", {
                          className: "nc-ann__main", "data-label": "Announcement",
                          children: [
                            e.jsx("div", { style: { fontWeight: 700, color: "var(--text-primary)" }, children: a.title }),
                            e.jsx("div", { className: "nc-ann__msg", children: a.message }),
                          ],
                        }),
                        e.jsx("td", { "data-label": "Audience", children: a.audience === 4 ? (a.targetCustomerName ?? "One customer") : audienceLabel(a) }),
                        e.jsx("td", { "data-label": "Channels", style: { color: "var(--text-secondary)" }, children: e.jsx(ChannelList, { a }) }),
                        e.jsx("td", { "data-label": "When", style: { whiteSpace: "nowrap" }, children: whenLabel(a) }),
                        e.jsx("td", { "data-label": "Status", children: e.jsx("span", { className: `qtn-pill ${STATUS[a.status]?.pill ?? ""}`, children: STATUS[a.status]?.label }) }),
                        e.jsx("td", { "data-label": "Read", style: { whiteSpace: "nowrap" }, children: a.status === 3 ? `${a.readCount} of ${a.recipientCount}` : "—" }),
                        e.jsx("td", {
                          children: e.jsxs("div", {
                            className: "row-actions", style: { flexWrap: "nowrap", justifyContent: "flex-end" },
                            children: [
                              e.jsx(l, { size: "sm", variant: "outline", onClick: () => onOpen(a), children: a.status === 3 ? "View" : a.status === 2 ? "Edit" : "Continue" }),
                              e.jsx(RowMenuButton, {
                                items: [
                                  { label: "Copy as new draft", onSelect: () => onCopy(a) },
                                  a.status !== 3 && { label: a.status === 2 ? "Cancel and delete" : "Delete draft", danger: true, onSelect: () => remove(a) },
                                ],
                              }),
                            ],
                          }),
                        }),
                      ],
                    }, a.announcementId)),
                  }),
                ],
              }),
              e.jsx("div", {
                className: "nc-more",
                children: result?.hasNext
                  ? e.jsx(l, { variant: "ghost", loading, onClick: () => void load(page + 1), children: "Load 20 more" })
                  : e.jsx("span", { className: "field__hint", children: `${result?.totalCount ?? rows.length} announcement${(result?.totalCount ?? rows.length) === 1 ? "" : "s"}` }),
              }),
            ],
          }),
    ],
  });
}

const toLocalInput = (utc) => {
  if (!utc) return "";
  const d = parseUtc(utc);
  return `${isoDay(d)}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
};

/* New / edit announcement. Channels that cannot deliver on this server are called out
   before sending, and recorded as not sent — never silently dropped. */
function AnnouncementForm({ initial, channels, customers, onClose, onSaved }) {
  const toast = v();
  const editing = initial?.announcementId && initial.status !== 3 ? initial : null;
  const [form, setForm] = React.useState(() => ({
    title: initial?.title ?? "",
    message: initial?.message ?? "",
    audience: String(initial?.audience ?? 1),
    customerId: initial?.targetCustomerId ? String(initial.targetCustomerId) : "",
    when: editing?.status === 2 ? "schedule" : "now",
    at: editing?.status === 2 ? toLocalInput(initial.scheduledForUtc) : "",
    email: Boolean(initial?.sendEmail),
    whatsApp: Boolean(initial?.sendWhatsApp),
  }));
  const [reach, setReach] = React.useState(null);
  const [busy, setBusy] = React.useState("");
  const [errors, setErrors] = React.useState({});
  const patch = (p) => setForm((f) => ({ ...f, ...p }));

  React.useEffect(() => {
    if (form.audience === "4" && !form.customerId) { setReach(0); return; }
    announcementsApi.audienceCount(Number(form.audience), form.customerId ? Number(form.customerId) : undefined)
      .then(setReach).catch(() => setReach(null));
  }, [form.audience, form.customerId]);

  const validate = (action) => {
    const errs = {};
    if (!form.title.trim()) errs.title = "Give the announcement a title.";
    if (!form.message.trim()) errs.message = "Write the message customers will see.";
    if (form.message.length > MAX_MESSAGE) errs.message = `Keep the message to ${MAX_MESSAGE} characters.`;
    if (form.audience === "4" && !form.customerId) errs.customerId = "Pick the customer.";
    if (action === "schedule") {
      if (!form.at) errs.at = "Pick when to send it.";
      else if (new Date(form.at) <= new Date(Date.now() + 60000)) errs.at = "Pick a time in the future.";
    }
    setErrors(errs);
    return Object.keys(errs).length === 0;
  };

  const save = async (action) => {
    if (!validate(action)) return;
    setBusy(action);
    const body = {
      title: form.title.trim(), message: form.message.trim(), audience: Number(form.audience),
      targetCustomerId: form.audience === "4" ? Number(form.customerId) : null,
      sendEmail: form.email, sendWhatsApp: form.whatsApp,
      scheduledForUtc: action === "schedule" ? new Date(form.at).toISOString() : null, action,
    };
    try {
      const saved = editing ? await announcementsApi.update(editing.announcementId, body) : await announcementsApi.create(body);
      toast.success(saved.status === 3 ? `Sent in-app to ${saved.recipientCount} customer${saved.recipientCount === 1 ? "" : "s"}.`
        : saved.status === 2 ? "Announcement scheduled." : "Draft saved.");
      onSaved(saved);
    } catch (error) {
      toast.error(error instanceof k ? error.message : "Could not save the announcement.");
    } finally {
      setBusy("");
    }
  };

  const whatsAppBlocked = form.whatsApp && channels && !channels.whatsAppConfigured;
  const emailBlocked = form.email && channels && !channels.emailConfigured;
  const primary = form.when === "schedule" ? "schedule" : "send";
  const short = `${form.title.trim()}\n${form.message.trim()}`;

  return e.jsx(B, {
    open: true,
    title: editing ? (editing.status === 2 ? "Edit scheduled announcement" : "Continue draft") : "New announcement",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(l, { variant: "outline", loading: busy === "draft", disabled: Boolean(busy), onClick: () => save("draft"), children: "Save as draft" }),
        e.jsx(l, { loading: busy === primary, disabled: Boolean(busy), onClick: () => save(primary), children: primary === "schedule" ? "Schedule" : "Send now" }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx(b, { label: "Title", required: true, maxLength: 200, placeholder: "e.g. Diwali holiday — office closed 1 Nov", value: form.title, error: errors.title, onChange: (ev) => patch({ title: ev.target.value }) }),
        e.jsxs("div", {
          className: "field",
          children: [
            e.jsxs("label", { className: "field__label", htmlFor: "ann-message", children: ["Message", e.jsx("span", { className: "field__required", "aria-hidden": !0, children: " *" })] }),
            e.jsx("textarea", {
              id: "ann-message", rows: 4, maxLength: MAX_MESSAGE,
              className: `field__input${errors.message ? " field__input--error" : ""}`,
              placeholder: "Keep it short. This is what customers see in the app.",
              value: form.message, onChange: (ev) => patch({ message: ev.target.value }),
            }),
            errors.message
              ? e.jsx("span", { className: "field__error", role: "alert", children: errors.message })
              : e.jsx("span", { className: "field__hint", children: `${form.message.length} / ${MAX_MESSAGE}. Maximum ${MAX_MESSAGE} characters. WhatsApp and SMS are trimmed to ${SHORT_MESSAGE}.` }),
          ],
        }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(y, { label: "Audience", required: true, value: form.audience, options: AUDIENCES, onChange: (ev) => patch({ audience: ev.target.value }) }),
            e.jsx(y, { label: "Send", required: true, value: form.when, options: [{ value: "now", label: "Now" }, { value: "schedule", label: "Schedule for later" }], onChange: (ev) => patch({ when: ev.target.value }) }),
          ],
        }),
        form.audience === "4" && e.jsx(y, { label: "Customer", required: true, value: form.customerId, placeholder: "Pick a customer", options: customers, error: errors.customerId, onChange: (ev) => patch({ customerId: ev.target.value }) }),
        form.when === "schedule" && e.jsx(b, { label: "Send at", type: "datetime-local", required: true, value: form.at, error: errors.at, onChange: (ev) => patch({ at: ev.target.value }) }),
        e.jsxs("div", {
          className: "field",
          children: [
            e.jsxs("span", { className: "field__label", children: ["Channels", e.jsx("span", { className: "field__required", "aria-hidden": !0, children: " *" })] }),
            e.jsxs("div", {
              className: "nc-checks",
              children: [
                e.jsxs("label", { children: [e.jsx("input", { type: "checkbox", checked: true, disabled: true }), " In-app"] }),
                e.jsxs("label", { children: [e.jsx("input", { type: "checkbox", checked: form.email, onChange: (ev) => patch({ email: ev.target.checked }) }), " Email"] }),
                e.jsxs("label", { children: [e.jsx("input", { type: "checkbox", checked: form.whatsApp, onChange: (ev) => patch({ whatsApp: ev.target.checked }) }), " WhatsApp"] }),
              ],
            }),
            e.jsx("span", { className: "field__hint", children: reach === null ? "In-app is always on." : `In-app is always on. It reaches ${reach} customer${reach === 1 ? "" : "s"}.` }),
          ],
        }),
        (whatsAppBlocked || emailBlocked) && e.jsxs("div", {
          className: "nc-warn", role: "alert",
          children: [
            e.jsx("strong", { children: "Not everything will go out:" }),
            whatsAppBlocked && e.jsx("div", { children: "WhatsApp is not connected on this server. The message will NOT be sent on WhatsApp — it will be recorded as a manual share, for you to send yourself." }),
            emailBlocked && e.jsx("div", { children: "Email is not set up on this server (no mail server). The email copy will NOT be sent — only the in-app message goes out." }),
          ],
        }),
        e.jsxs("div", {
          className: "nc-preview",
          children: [
            e.jsx("div", { className: "nc-preview__label", children: "Preview — customer app" }),
            e.jsxs("div", {
              className: "nc-preview__card",
              children: [
                e.jsx("div", { style: { fontWeight: 700 }, children: form.title.trim() || "Regalis WorkSpace" }),
                e.jsx("div", { style: { color: "var(--text-secondary)", whiteSpace: "pre-wrap" }, children: form.message.trim() || "Your message appears here." }),
              ],
            }),
            form.whatsApp && e.jsxs(e.Fragment, {
              children: [
                e.jsx("div", { className: "nc-preview__label", style: { marginTop: "var(--space-3)" }, children: `WhatsApp — first ${SHORT_MESSAGE} characters` }),
                e.jsx("div", { className: "nc-preview__card", style: { whiteSpace: "pre-wrap" }, children: short.length > SHORT_MESSAGE ? `${short.slice(0, SHORT_MESSAGE - 1)}…` : short }),
              ],
            }),
          ],
        }),
      ],
    }),
  });
}

/* A sent announcement: read-only, with what each channel actually did. */
function AnnouncementView({ a, onClose, onCopy }) {
  const kv = (k2, val) => e.jsxs("div", { children: [e.jsx("div", { className: "qtn-detail__label", children: k2 }), e.jsx("div", { children: val ?? "—" })] }, k2);
  const fmt = (v2) => (v2 ? parseUtc(v2).toLocaleString("en-IN", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }) : null);
  return e.jsx(B, {
    open: true, title: a.title, onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(l, { variant: "ghost", onClick: onClose, children: "Close" }),
        e.jsx(l, { variant: "outline", onClick: () => onCopy(a), children: "Copy as new draft" }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx("div", { className: "nc-preview__card", style: { whiteSpace: "pre-wrap" }, children: a.message }),
        e.jsxs("div", {
          className: "nc-kv",
          children: [
            kv("Audience", a.audience === 4 ? (a.targetCustomerName ?? "One customer") : audienceLabel(a)),
            kv("Sent", fmt(a.sentUtc)),
            kv("Sent by", a.sentByName),
            kv("Recipients", String(a.recipientCount)),
            kv("Read", `${a.readCount} of ${a.recipientCount}`),
            kv("Channels", e.jsx(ChannelList, { a })),
            a.sendEmail && kv("Emails sent", String(a.emailSentCount)),
            a.sendWhatsApp && kv("WhatsApp sent", a.whatsAppManualShare ? "0 — manual share" : String(a.whatsAppSentCount)),
          ],
        }),
        a.channelsPending && e.jsx("p", { className: "field__hint", children: "Email / WhatsApp copies are going out now; this updates within a minute." }),
        a.deliveryNote && e.jsx("div", { className: (a.deliveryNote.includes("NOT sent") ? "nc-warn" : "qtn-filternote"), children: a.deliveryNote }),
      ],
    }),
  });
}

/* Notification settings: per event type, per admin. In-app is always on. */
function SettingsModal({ channels, onClose }) {
  const toast = v();
  const [rows, setRows] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => { settingsApi.get().then(setRows).catch(() => setRows([])); }, []);
  const set = (type, key, value) => setRows((r) => r.map((row) => (row.type === type ? { ...row, [key]: value } : row)));
  const save = async () => {
    setBusy(true);
    try {
      await settingsApi.save(rows.map((r) => ({ type: r.type, email: r.email, whatsApp: r.whatsApp })));
      toast.success("Notification settings saved.");
      onClose();
    } catch (error) {
      toast.error(error instanceof k ? error.message : "Could not save the settings.");
    } finally {
      setBusy(false);
    }
  };
  const toggle = (checked, onChange, label, disabled) => e.jsx("input", {
    type: "checkbox", role: "switch", className: "nc-switch", checked, disabled, "aria-label": label,
    onChange: (ev) => onChange(ev.target.checked),
  });
  return e.jsx(B, {
    open: true, title: "Notification settings", onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(l, { variant: "ghost", onClick: onClose, children: "Cancel" }),
        e.jsx(l, { loading: busy, disabled: !rows, onClick: save, children: "Save settings" }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx("p", { style: { margin: 0, color: "var(--text-secondary)" }, children: "Choose which events reach you, and how. In-app notifications always appear on this page; the toggles control the extra alerts. These are your own settings." }),
        channels && (!channels.emailConfigured || !channels.whatsAppConfigured) && e.jsxs("div", {
          className: "nc-warn",
          children: [
            !channels.emailConfigured && e.jsx("div", { children: "Email is not set up on this server — email alerts are recorded as not sent until a mail server is configured." }),
            !channels.whatsAppConfigured && e.jsx("div", { children: "WhatsApp is not connected — WhatsApp alerts are recorded as not sent (manual share)." }),
          ],
        }),
        rows === null ? e.jsx(Spinner, { center: true }) : e.jsx("div", {
          className: "table-scroll",
          children: e.jsxs("table", {
            className: "data-table nc-settings",
            children: [
              e.jsx("thead", { children: e.jsxs("tr", { children: [e.jsx("th", { children: "Event" }), e.jsx("th", { children: "In-app" }), e.jsx("th", { children: "Email" }), e.jsx("th", { children: "WhatsApp" })] }) }),
              e.jsx("tbody", {
                children: rows.map((r) => e.jsxs("tr", {
                  children: [
                    e.jsxs("td", { children: [e.jsx("div", { style: { fontWeight: 600 }, children: r.label }), e.jsx("div", { className: "qtn-cell__muted", children: r.description })] }),
                    e.jsx("td", { children: toggle(true, () => {}, `${r.label}: in-app (always on)`, true) }),
                    e.jsx("td", { children: toggle(r.email, (val) => set(r.type, "email", val), `${r.label}: email`) }),
                    e.jsx("td", { children: toggle(r.whatsApp, (val) => set(r.type, "whatsApp", val), `${r.label}: WhatsApp`) }),
                  ],
                }, r.type)),
              }),
            ],
          }),
        }),
      ],
    }),
  });
}

const ANNOUNCE_STYLES = `
.nc-ann__msg{color:var(--text-secondary);font-size:var(--font-size-xs);margin-top:4px;max-width:360px;display:-webkit-box;-webkit-line-clamp:3;-webkit-box-orient:vertical;overflow:hidden}
.nc-iconbtn{width:2rem;height:2rem;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);color:var(--text-secondary);cursor:pointer;line-height:1}
.nc-menu{position:absolute;right:0;top:calc(100% + 4px);z-index:30;min-width:180px;background:var(--surface-card);border:1px solid var(--border-strong);border-radius:var(--radius-md);box-shadow:var(--shadow-lg);padding:4px}
.nc-menu__item{display:block;width:100%;text-align:left;padding:8px 10px;border:0;background:none;font:inherit;font-size:var(--font-size-sm);color:var(--text-primary);border-radius:var(--radius-sm);cursor:pointer}
.nc-menu__item:hover{background:var(--surface-hover)}
.nc-menu__item--danger{color:var(--color-danger)}
.nc-checks{display:flex;gap:var(--space-5);flex-wrap:wrap;padding:6px 0}
.nc-checks label{display:inline-flex;align-items:center;gap:6px;cursor:pointer}
.nc-warn{padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);border:1px solid color-mix(in srgb,var(--color-danger) 35%,transparent);background:color-mix(in srgb,var(--color-danger) 8%,transparent);color:var(--text-primary);font-size:var(--font-size-sm);display:grid;gap:4px}
.nc-warn strong{color:var(--color-danger)}
.nc-preview{padding:var(--space-4);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 8%)}
.nc-preview__label{font-size:var(--font-size-xs);color:var(--text-muted);margin-bottom:var(--space-2)}
.nc-preview__card{padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);background:var(--surface-card);border:1px solid var(--border-subtle);display:grid;gap:4px}
.nc-kv{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--space-3) var(--space-5)}
.nc-settings td,.nc-settings th{vertical-align:middle}
.nc-settings th:not(:first-child),.nc-settings td:not(:first-child){text-align:center;width:90px}
.nc-switch{appearance:none;-webkit-appearance:none;width:40px;height:22px;border-radius:999px;background:var(--border-strong);position:relative;cursor:pointer;transition:background var(--transition-fast);margin:0}
.nc-switch::after{content:"";position:absolute;top:3px;left:3px;width:16px;height:16px;border-radius:50%;background:#fff;box-shadow:0 1px 2px rgb(0 0 0 / 25%);transition:transform var(--transition-fast)}
.nc-switch:checked{background:var(--accent-strong)}
.nc-switch:checked::after{transform:translateX(18px)}
.nc-switch:disabled{opacity:.6;cursor:not-allowed}
.nc-switch:focus-visible{outline:2px solid var(--focus-ring, var(--accent-strong));outline-offset:2px}
@media (max-width:767px){
 .nc-ann table,.nc-ann thead,.nc-ann tbody,.nc-ann tr,.nc-ann td{display:block}
 .nc-ann thead{display:none}
 .nc-ann tr{border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3);margin-bottom:var(--space-3)}
 .nc-ann td{border:0;padding:4px 0}
 .nc-ann td[data-label]:not(.nc-ann__main)::before{content:attr(data-label) ": ";color:var(--text-muted);font-size:var(--font-size-xs)}
 .nc-ann .row-actions{justify-content:stretch!important}
 .nc-ann .row-actions>button:first-child{flex:1}
 .nc-kv{grid-template-columns:1fr}
}`;

function ee() {
  useCenterStyles();
  const toast = v();
  const navigate = useNavigate();
  const canBroadcast = A("NOTIFICATION.CREATE");

  const [tab, setTab] = React.useState("incoming");
  const [type, setType] = React.useState(0);
  const [search, setSearch] = React.useState("");
  const [term, setTerm] = React.useState("");
  const [state, setState] = React.useState("");
  const [customerId, setCustomerId] = React.useState("");
  const [datePreset, setDatePreset] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [needsAction, setNeedsAction] = React.useState(false);
  const [customers, setCustomers] = React.useState([]);
  const [items, setItems] = React.useState([]);
  const [feed, setFeed] = React.useState(null);
  const [page, setPage] = React.useState(1);
  const [loading, setLoading] = React.useState(true);
  const [formFor, setFormFor] = React.useState(null);
  const [viewFor, setViewFor] = React.useState(null);
  const [settingsOpen, setSettingsOpen] = React.useState(false);
  const [channels, setChannels] = React.useState(null);
  const [annKey, setAnnKey] = React.useState(0);
  const seq = React.useRef(0);
  const sentinel = React.useRef(null);

  React.useEffect(() => {
    const timer = setTimeout(() => setTerm(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

  React.useEffect(() => { announcementsApi.channels().then(setChannels).catch(() => setChannels(null)); }, []);

  React.useEffect(() => {
    customersApi.list({ page: 1, pageSize: 200 })
      .then((res) => setCustomers(res.items.map((c) => ({ value: String(c.userId), label: c.fullName }))))
      .catch(() => setCustomers([]));
  }, []);

  const params = React.useMemo(() => ({
    type: type || undefined,
    state: state || undefined,
    customerId: customerId || undefined,
    search: term || undefined,
    needsAction: needsAction || undefined,
    ...rangeFor(datePreset, from, to),
  }), [type, state, customerId, term, needsAction, datePreset, from, to]);

  const load = React.useCallback(async (pageNo) => {
    const mine = ++seq.current;
    setLoading(true);
    try {
      const res = await notificationsApi.feed({ ...params, page: pageNo, pageSize: 20 });
      if (mine !== seq.current) return;
      setFeed(res);
      setItems((current) => (pageNo === 1 ? res.page.items : [...current, ...res.page.items]));
      setPage(pageNo);
    } catch (error) {
      if (mine === seq.current) toast.error(error instanceof k ? error.message : "Could not load notifications.");
    } finally {
      if (mine === seq.current) setLoading(false);
    }
  }, [params]);

  React.useEffect(() => { void load(1); }, [load]);

  /* Infinite scroll: the next 20 load as the sentinel comes into view. */
  const hasMore = feed ? feed.page.hasNext : false;
  React.useEffect(() => {
    const node = sentinel.current;
    if (!node || !hasMore) return undefined;
    const observer = new IntersectionObserver((entries) => {
      if (entries.some((en) => en.isIntersecting) && !loading) void load(page + 1);
    }, { rootMargin: "200px" });
    observer.observe(node);
    return () => observer.disconnect();
  }, [hasMore, loading, page, load]);

  const markLocal = (id) => setItems((current) => current.map((n) => (n.adminNotificationId === id ? { ...n, isRead: true } : n)));
  const bumpSummary = (delta) => setFeed((f) => (f ? { ...f, unreadCount: Math.max(0, f.unreadCount - delta), summary: { ...f.summary, unread: Math.max(0, f.summary.unread - delta) } } : f));

  const markRead = async (n) => {
    if (n.isRead) return;
    markLocal(n.adminNotificationId);
    bumpSummary(1);
    try { await notificationsApi.read(n.adminNotificationId); announceChange(); } catch { /* the next load corrects it */ }
  };

  const open = async (n) => {
    void markRead(n);
    if (n.actionRoute) navigate(n.actionRoute);
  };

  const readAll = async () => {
    try {
      await notificationsApi.readAll();
      announceChange();
      toast.success("All notifications marked as read.");
      await load(1);
    } catch (error) {
      toast.error(error instanceof k ? error.message : "Could not mark them as read.");
    }
  };

  const filtered = Boolean(type || term || state || customerId || datePreset || needsAction);
  const summary = feed?.summary;

  /* Group the loaded rows by local day, newest first. */
  const groups = [];
  for (const n of items) {
    const key = dayKey(n.createdUtc);
    const last = groups[groups.length - 1];
    if (last && last.key === key) last.items.push(n); else groups.push({ key, items: [n] });
  }

  const chips = [{ value: 0, label: "All", count: feed?.allCount }, ...TYPES.map((t) => ({ ...t, count: feed?.typeCounts?.[t.value] ?? 0 }))];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Notifications",
        description: "Everything happening in the space, and the announcements you send out.",
        actions: e.jsxs("div", {
          className: "row-actions",
          children: [
            e.jsx(l, { variant: "outline", onClick: () => setSettingsOpen(true), children: "Notification settings" }),
            e.jsx(l, { variant: "outline", onClick: readAll, disabled: !feed || feed.unreadCount === 0, children: "Mark all read" }),
            canBroadcast && e.jsx(l, { onClick: () => setFormFor({}), children: "+ New announcement" }),
          ],
        }),
      }),

      e.jsxs("div", {
        className: "qtn-stats nc-stats",
        children: [
          e.jsx(SummaryCard, {
            label: "Unread", value: summary ? summary.unread : "—", hint: "Last 90 days",
            active: state === "unread" && !needsAction,
            onClick: () => { setTab("incoming"); setNeedsAction(false); setState("unread"); },
          }),
          e.jsx(SummaryCard, {
            label: "Today", value: summary ? summary.today : "—",
            hint: summary ? `Across ${summary.todayTypes} type${summary.todayTypes === 1 ? "" : "s"}` : null,
            active: datePreset === "today" && !needsAction,
            onClick: () => { setTab("incoming"); setNeedsAction(false); setDatePreset("today"); },
          }),
          e.jsx(SummaryCard, {
            label: "Needs action", value: summary ? summary.needsAction : "—", danger: true,
            hint: "Tickets, payments and tasks", active: needsAction,
            onClick: () => { setTab("incoming"); setNeedsAction(true); setState(""); setDatePreset(""); },
          }),
          e.jsx(SummaryCard, {
            label: "Announcements sent", value: summary ? summary.announcementsSent : "—",
            hint: summary?.lastAnnouncementUtc ? `Last one ${timeLabel(summary.lastAnnouncementUtc)}` : "None yet",
            active: tab === "announcements",
            onClick: () => setTab("announcements"),
          }),
        ],
      }),

      e.jsxs(Card, {
        children: [
          e.jsxs("div", {
            className: "qtn-tabs",
            role: "tablist",
            children: [
              e.jsxs("button", {
                type: "button", role: "tab", "aria-selected": tab === "incoming",
                className: `qtn-tab${tab === "incoming" ? " qtn-tab--active" : ""}`,
                onClick: () => setTab("incoming"),
                children: ["Incoming notifications", feed && e.jsx("span", { className: "qtn-tab__count", children: feed.unreadCount })],
              }),
              e.jsxs("button", {
                type: "button", role: "tab", "aria-selected": tab === "announcements",
                className: `qtn-tab${tab === "announcements" ? " qtn-tab--active" : ""}`,
                onClick: () => setTab("announcements"),
                children: ["Announcements sent", summary && e.jsx("span", { className: "qtn-tab__count", children: summary.announcementsSent })],
              }),
            ],
          }),

          tab === "announcements"
            ? e.jsx(AnnouncementsTab, {
              refreshKey: annKey,
              onChanged: () => void load(1),
              onOpen: async (a) => {
                if (a.status !== 3) { setFormFor(a); return; }
                try { setViewFor(await announcementsApi.get(a.announcementId)); } catch { setViewFor(a); }
              },
              onCopy: (a) => { setViewFor(null); setFormFor({ title: a.title, message: a.message, audience: a.audience, targetCustomerId: a.targetCustomerId, sendEmail: a.sendEmail, sendWhatsApp: a.sendWhatsApp }); },
            })
            : e.jsxs(e.Fragment, {
              children: [
                e.jsx("div", {
                  className: "nc-chips",
                  role: "group",
                  "aria-label": "Filter by type",
                  children: chips.map((c) => e.jsxs("button", {
                    type: "button",
                    className: `nc-chip${type === c.value ? " nc-chip--active" : ""}`,
                    "aria-pressed": type === c.value,
                    onClick: () => setType(c.value),
                    children: [c.label, c.count !== undefined && e.jsx("span", { className: "nc-chip__count", children: c.count })],
                  }, c.value)),
                }),
                e.jsxs("div", {
                  className: "nc-filters",
                  children: [
                    e.jsx(b, { label: "Search", placeholder: "Search customer, reference or text…", value: search, onChange: (ev) => setSearch(ev.target.value) }),
                    e.jsx(y, {
                      label: "State", value: state, placeholder: "All states",
                      options: [{ value: "unread", label: "Unread" }, { value: "read", label: "Read" }],
                      onChange: (ev) => setState(ev.target.value),
                    }),
                    e.jsx(y, { label: "Customer", value: customerId, placeholder: "All customers", options: customers, onChange: (ev) => setCustomerId(ev.target.value) }),
                    e.jsx(y, {
                      label: "Date", value: datePreset,
                      options: DATE_OPTIONS,
                      onChange: (ev) => setDatePreset(ev.target.value),
                    }),
                  ],
                }),
                datePreset === "custom" && e.jsxs("div", {
                  className: "nc-range",
                  children: [
                    e.jsx(b, { label: "From", type: "date", value: from, max: to || undefined, onChange: (ev) => setFrom(ev.target.value) }),
                    e.jsx(b, { label: "To", type: "date", value: to, min: from || undefined, onChange: (ev) => setTo(ev.target.value) }),
                  ],
                }),
                needsAction && e.jsxs("div", {
                  className: "qtn-filternote",
                  style: { marginTop: "var(--space-3)" },
                  children: [
                    "Showing only items that still need action. They drop out when the ticket is closed, the invoice paid or the task verified. ",
                    e.jsx("button", { type: "button", className: "qtn-link", onClick: () => setNeedsAction(false), children: "Show everything" }),
                  ],
                }),

                !feed && loading
                  ? e.jsx(Spinner, { center: true })
                  : items.length === 0
                    ? e.jsx(EmptyState, {
                      title: filtered ? "No notifications match these filters." : "All caught up",
                      description: filtered ? "Try another type, customer or date range." : "Nothing has happened in the space in the last 90 days.",
                    })
                    : e.jsxs("div", {
                      children: [
                        ...groups.map((g) => e.jsxs("section", {
                          "aria-label": dayLabel(g.key),
                          children: [
                            e.jsxs("div", {
                              className: "nc-day",
                              children: [e.jsx("strong", { children: dayLabel(g.key) }), e.jsx("span", { children: `${g.items.length} notification${g.items.length === 1 ? "" : "s"}` })],
                            }),
                            ...g.items.map((n) => e.jsx(NotificationRow, { n, onOpen: open, onRead: markRead }, n.adminNotificationId)),
                          ],
                        }, g.key)),
                        e.jsx("div", {
                          ref: sentinel,
                          className: "nc-more",
                          children: hasMore
                            ? e.jsx(l, { variant: "ghost", loading, onClick: () => void load(page + 1), children: "Load 20 more" })
                            : e.jsx("span", { className: "field__hint", children: `${feed.page.totalCount} notification${feed.page.totalCount === 1 ? "" : "s"}` }),
                        }),
                      ],
                    }),
              ],
            }),
        ],
      }),

      formFor && e.jsx(AnnouncementForm, {
        initial: formFor.title !== undefined ? formFor : null,
        channels,
        customers,
        onClose: () => setFormFor(null),
        onSaved: () => { setFormFor(null); setTab("announcements"); setAnnKey((n) => n + 1); void load(1); },
      }),
      viewFor && e.jsx(AnnouncementView, {
        a: viewFor,
        onClose: () => setViewFor(null),
        onCopy: (a) => { setViewFor(null); setFormFor({ title: a.title, message: a.message, audience: a.audience, targetCustomerId: a.targetCustomerId, sendEmail: a.sendEmail, sendWhatsApp: a.sendWhatsApp }); },
      }),
      settingsOpen && e.jsx(SettingsModal, { channels, onClose: () => setSettingsOpen(false) }),
    ],
  });
}

export{ee as AdminNotificationsPage};
