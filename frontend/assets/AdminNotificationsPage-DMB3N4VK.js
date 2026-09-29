/**
 * Admin -> Engagement -> Notifications.
 *
 * Two tabs. Incoming: every event in the space from /api/admin/notifications — one
 * row per event, read state per admin, "Needs action" worked out from the record
 * (it clears when the ticket is closed, the invoice paid, the task verified), type
 * chips with live counts, filters, day groups and infinite scroll. Announcements
 * sent: next step. The announcement modal (U) at the end is the original minified
 * code, unchanged until the Announcements step replaces it.
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

const STYLE_ID = "notif-center-styles";
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
    tag.textContent = STYLES;
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
  const [announceOpen, setAnnounceOpen] = React.useState(false);
  const seq = React.useRef(0);
  const sentinel = React.useRef(null);

  React.useEffect(() => {
    const timer = setTimeout(() => setTerm(search.trim()), 300);
    return () => clearTimeout(timer);
  }, [search]);

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
            e.jsx(l, { variant: "outline", onClick: readAll, disabled: !feed || feed.unreadCount === 0, children: "Mark all read" }),
            canBroadcast && e.jsx(l, { onClick: () => setAnnounceOpen(true), children: "+ New announcement" }),
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
            ? e.jsx("div", {
              className: "nc-soon",
              children: "The list of sent announcements, drafts and scheduling arrives in the next step. You can still send one with “+ New announcement”.",
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

      announceOpen && e.jsx(U, {
        open: true,
        onClose: () => setAnnounceOpen(false),
        onSent: () => { setAnnounceOpen(false); void load(1); },
      }),
    ],
  });
}

const M=I({audience:_(["2","3"]),category:d().min(1,"Pick a category."),title:d().min(1,"A title is required.").max(200),message:d().min(1,"A message is required.").max(2e3),actionLabel:d().max(60).optional(),actionRoute:d().max(200).optional()});function U({open:p,onClose:u,onSent:g}){const c=v(),{register:i,handleSubmit:x,reset:f,formState:{errors:r,isSubmitting:j}}=w({resolver:T(M),defaultValues:{audience:"2",category:"9"}}),h=x(async t=>{try{const s=await m.broadcast({audience:Number(t.audience),category:Number(t.category),title:t.title,message:t.message,actionLabel:t.actionLabel||null,actionRoute:t.actionRoute||null});c.success(`Announcement sent to ${s} recipient(s).`),f(),g()}catch(s){c.error(s instanceof k?s.message:"Could not send the announcement.")}});return e.jsx(B,{open:p,title:"New announcement",onClose:u,footer:e.jsxs(e.Fragment,{children:[e.jsx(l,{variant:"ghost",onClick:u,children:"Cancel"}),e.jsx(l,{onClick:h,loading:j,children:"Send"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsxs("div",{className:"form-grid",children:[e.jsx(y,{label:"Audience",required:!0,error:r.audience?.message,options:Object.entries(z).map(([t,s])=>({value:t,label:s})),...i("audience")}),e.jsx(y,{label:"Category",required:!0,error:r.category?.message,options:Object.entries(N).map(([t,s])=>({value:t,label:s})),...i("category")})]}),e.jsx(b,{label:"Title",required:!0,error:r.title?.message,placeholder:"Lift maintenance this Saturday",...i("title")}),e.jsxs("div",{className:"field",children:[e.jsxs("label",{className:"field__label",htmlFor:"broadcast-message",children:["Message",e.jsx("span",{className:"field__required","aria-hidden":!0,children:" *"})]}),e.jsx("textarea",{id:"broadcast-message",rows:5,className:`field__input${r.message?" field__input--error":""}`,"aria-invalid":!!r.message,placeholder:"What members need to know, and when.",...i("message")}),r.message&&e.jsx("span",{className:"field__error",role:"alert",children:r.message.message})]}),e.jsxs("div",{className:"form-grid",children:[e.jsx(b,{label:"Action label",error:r.actionLabel?.message,hint:"Optional link shown under the message",placeholder:"View bookings",...i("actionLabel")}),e.jsx(b,{label:"Action route",error:r.actionRoute?.message,hint:"An in-app path, e.g. /bookings",placeholder:"/bookings",...i("actionRoute")})]})]})})}
export{ee as AdminNotificationsPage};
