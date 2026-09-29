/**
 * Admin -> Engagement -> Notifications.
 *
 * The feed is a list of cards: category icon, title, status badge, the message, the
 * record it is about (booking / ticket / profile), when it arrived, and read state.
 * Clicking a card marks it read and opens the related screen. The State filter
 * covers read state and category (Booking = 1, Support ticket = 8, Account = 13).
 *
 * The alerts themselves come from the API's AdminAlerts helper, one row per
 * admin-portal user, so read state is per person.
 *
 * This repo ships the built SPA only, so this chunk is hand-maintained: keep the
 * file name (the route in index-DI5X4c9f.js imports it by name) and bump its
 * revision in sw.js, or the service worker keeps serving the cached copy.
 * The announcement modal (U) at the end is the original minified code, unchanged;
 * the single-letter imports below are the names it uses.
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
  G as Spinner
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as B } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { N, e as z } from "./finance-C0-kLKkN.js";
import { f as formatDateTime } from "./formatters-DFe5xCRi.js";
import { B as IconBell, T as IconTrash } from "./trash-2-DL_V1e8k.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { C as IconChevronLeft, a as IconChevronRight } from "./chevron-right-BikENMwC.js";

const IconBooking = makeIcon("calendar-check", [
  ["path", { d: "M8 2v4", key: "1cmpym" }],
  ["path", { d: "M16 2v4", key: "4m81vk" }],
  ["rect", { width: "18", height: "18", x: "3", y: "4", rx: "2", key: "1hopcy" }],
  ["path", { d: "M3 10h18", key: "8toen8" }],
  ["path", { d: "m9 16 2 2 4-4", key: "19s6y9" }]
]);
const IconSupport = makeIcon("life-buoy", [
  ["circle", { cx: "12", cy: "12", r: "10", key: "1mglay" }],
  ["path", { d: "m4.93 4.93 4.24 4.24", key: "1ymg45" }],
  ["path", { d: "m14.83 9.17 4.24-4.24", key: "1cb5xl" }],
  ["path", { d: "m14.83 14.83 4.24 4.24", key: "q42g0n" }],
  ["path", { d: "m9.17 14.83-4.24 4.24", key: "bqpfvv" }],
  ["circle", { cx: "12", cy: "12", r: "4", key: "4exip2" }]
]);
const IconShield = makeIcon("shield-check", [
  ["path", { d: "M20 13c0 5-3.5 7.5-7.66 8.95a1 1 0 0 1-.67-.01C7.5 20.5 4 18 4 13V6a1 1 0 0 1 1-1c2 0 4.5-1.2 6.24-2.72a1.17 1.17 0 0 1 1.52 0C14.51 3.81 17 5 19 5a1 1 0 0 1 1 1z", key: "oel41y" }],
  ["path", { d: "m9 12 2 2 4-4", key: "dzmm74" }]
]);
const IconMegaphone = makeIcon("megaphone", [
  ["path", { d: "m3 11 18-5v12L3 14v-3z", key: "n962bs" }],
  ["path", { d: "M11.6 16.8a3 3 0 1 1-5.8-1.6", key: "1yl0tm" }]
]);
const IconCard = makeIcon("credit-card", [
  ["rect", { width: "20", height: "14", x: "2", y: "5", rx: "2", key: "ynyp8z" }],
  ["line", { x1: "2", x2: "22", y1: "10", y2: "10", key: "1b3vmo" }]
]);

const CATEGORY_LABELS = { ...N, 13: "Account" };

// The State dropdown: read state or a category, one at a time.
const FILTERS = {
  unread: { isRead: false },
  read: { isRead: true },
  booking: { category: 1 },
  support: { category: 8 },
  profile: { category: 13 }
};
const FILTER_OPTIONS = [
  { value: "unread", label: "Unread" },
  { value: "read", label: "Read" },
  { value: "booking", label: "Booking" },
  { value: "support", label: "Support / Tickets" },
  { value: "profile", label: "Profile" }
];

function kindOf(n) {
  if (n.referenceType === "Booking" || [1, 2, 3].includes(n.category)) return "booking";
  if (n.referenceType === "Ticket" || n.category === 8) return "ticket";
  if (n.referenceType === "User" || n.category === 10 || n.category === 13) return "profile";
  if (n.category === 4 || n.category === 5) return "payment";
  if (n.category === 9) return "announcement";
  return "other";
}

function iconFor(kind, title) {
  if (kind === "booking") return IconBooking;
  if (kind === "ticket") return IconSupport;
  if (kind === "profile") return /password|lock|security/i.test(title) ? IconShield : IconUser;
  if (kind === "payment") return IconCard;
  if (kind === "announcement") return IconMegaphone;
  return IconBell;
}

/** The status badge, read off the event in the title (and "Status: X." on tickets). */
function statusOf(kind, title, message) {
  const t = title.toLowerCase();
  const has = (word) => t.includes(word);
  if (kind === "booking") {
    if (has("cancel")) return ["Cancelled", "danger"];
    if (has("reschedul")) return ["Rescheduled", "info"];
    if (has("complet")) return ["Completed", "neutral"];
    if (has("extend")) return ["Extended", "info"];
    if (has("confirm")) return ["Confirmed", "success"];
    if (has("new")) return ["Pending payment", "warning"];
    return null;
  }
  if (kind === "ticket") {
    const stated = /Status: ([A-Za-z]+)\./.exec(message);
    if (has("resolved")) return ["Resolved", "success"];
    if (has("closed")) return ["Closed", "neutral"];
    if (has("reopen")) return ["Reopened", "warning"];
    if (has("replied")) return ["Customer replied", "gold"];
    if (has("assigned")) return ["Assigned", "info"];
    if (has("new")) return ["Open", "warning"];
    if (stated) return [stated[1].replace(/([a-z])([A-Z])/g, "$1 $2"), "info"];
    return ["Updated", "info"];
  }
  if (kind === "profile") {
    if (has("locked") && !has("unlocked")) return ["Locked", "danger"];
    if (has("password")) return ["Security", "warning"];
    if (has("deactivat")) return ["Deactivated", "neutral"];
    if (has("created") || has("completed")) return ["Created", "success"];
    if (has("activat") || has("unlock")) return ["Active", "success"];
    return ["Updated", "info"];
  }
  return null;
}

/** The record the alert is about, parsed from the API's message formats. */
function relatedOf(kind, message) {
  if (kind === "booking") {
    const hit = /^(\S+) for (.+?) · /.exec(message);
    if (hit) return [["Booking", hit[1]], ["Customer", hit[2]]];
    const number = /^([A-Z]+-\d+)/.exec(message);
    return number ? [["Booking", number[1]]] : [];
  }
  if (kind === "ticket") {
    const hit = /^(\S+?): .+? · (.+?) · /.exec(message);
    if (hit) return [["Ticket", hit[1]], ["Customer", hit[2]]];
    const number = /^(\S+?):/.exec(message);
    return number ? [["Ticket", number[1]]] : [];
  }
  if (kind === "profile") {
    const hit = /^(Customer|Admin|Staff) profile (.+?) (?:was|had|finished)\b/.exec(message);
    return hit ? [[`${hit[1]} profile`, hit[2]]] : [];
  }
  return [];
}

/** Where a click goes. Admin routes from the API win; older rows fall back by type. */
function routeOf(n, kind) {
  if (n.actionRoute && n.actionRoute.startsWith("/admin")) return n.actionRoute;
  if (kind === "booking") return n.referenceId ? `/admin/bookings?booking=${n.referenceId}` : "/admin/bookings";
  if (kind === "ticket") return n.referenceId ? `/admin/tickets/${n.referenceId}` : "/admin/tickets";
  if (kind === "profile") return n.referenceId ? "/admin/users" : "/admin/profile";
  if (n.category === 4) return "/admin/payments";
  if (n.category === 5) return "/admin/invoices";
  return n.actionRoute || null;
}

function parseUtc(value) {
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`);
}

function timeAgo(value) {
  const seconds = Math.max(0, (Date.now() - parseUtc(value).getTime()) / 1000);
  const plural = (count, unit) => `${count} ${unit}${count === 1 ? "" : "s"} ago`;
  if (seconds < 60) return "Just now";
  if (seconds < 3600) return plural(Math.floor(seconds / 60), "minute");
  if (seconds < 86400) return plural(Math.floor(seconds / 3600), "hour");
  if (seconds < 172800) return "Yesterday";
  if (seconds < 604800) return plural(Math.floor(seconds / 86400), "day");
  return null; // past a week the date alone reads better
}

const STYLE_ID = "notif-feed-styles";
const STYLES = `
.notif-feed{display:flex;flex-direction:column;gap:var(--space-3);margin-top:var(--space-4)}
.notif-card{position:relative;display:flex;gap:var(--space-4);align-items:flex-start;padding:var(--space-4) var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);cursor:pointer;transition:background var(--transition-fast),border-color var(--transition-fast)}
.notif-card:hover{background:var(--surface-hover);border-color:var(--border-strong)}
.notif-card:focus-visible{outline:none;box-shadow:var(--focus-ring)}
.notif-card--static{cursor:default}
.notif-card--unread{background:rgb(var(--accent-rgb) / 7%);border-color:rgb(var(--accent-rgb) / 35%)}
.notif-card--unread:hover{background:rgb(var(--accent-rgb) / 11%)}
.notif-card--unread::before{content:"";position:absolute;inset-block:12px;inset-inline-start:-1px;width:3px;border-radius:var(--radius-full);background:var(--accent)}
.notif-card__icon{flex-shrink:0;display:grid;place-items:center;width:40px;height:40px;border-radius:var(--radius-md);background:var(--surface-inset);color:var(--text-muted)}
.notif-card--unread .notif-card__icon{background:rgb(var(--accent-rgb) / 16%);color:var(--regalis-gold-dark)}
[data-theme=dark] .notif-card--unread .notif-card__icon{color:var(--accent-bright)}
.notif-card__body{flex:1;min-width:0}
.notif-card__head{display:flex;flex-wrap:wrap;align-items:center;gap:var(--space-2)}
.notif-card__head .badge{margin-inline-start:0}
.notif-card__title{font-weight:500;color:var(--text-primary)}
.notif-card--unread .notif-card__title{font-weight:700}
.notif-card__dot{width:8px;height:8px;border-radius:50%;background:var(--accent);flex-shrink:0}
.notif-card__message{margin-top:2px;color:var(--text-secondary);font-size:var(--font-size-sm);overflow-wrap:anywhere}
.notif-card__meta{display:flex;flex-wrap:wrap;gap:var(--space-1) var(--space-4);margin-top:var(--space-2);font-size:var(--font-size-xs);color:var(--text-muted)}
.notif-card__meta b{font-weight:600;color:var(--text-secondary)}
.notif-card__side{flex-shrink:0;display:flex;flex-direction:column;align-items:flex-end;gap:var(--space-2);text-align:end}
.notif-card__when{font-size:var(--font-size-xs);font-weight:600;color:var(--text-secondary);white-space:nowrap}
.notif-card__actions{display:flex;gap:var(--space-1)}
@media(max-width:640px){
.notif-card{flex-wrap:wrap;padding:var(--space-4);gap:var(--space-3)}
.notif-card__icon{width:34px;height:34px}
.notif-card__body{flex-basis:calc(100% - 50px)}
.notif-card__side{flex-basis:100%;flex-direction:row;flex-wrap:wrap;justify-content:space-between;align-items:center;text-align:start}
.notif-card__actions{flex-wrap:wrap;margin-inline-start:auto}
}`;

function useFeedStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }, []);
}

function NotificationCard({ n, onOpen, onRead, onRemove }) {
  const kind = kindOf(n);
  const Icon = iconFor(kind, n.title);
  const status = statusOf(kind, n.title, n.message);
  const related = relatedOf(kind, n.message);
  const route = routeOf(n, kind);
  const stop = (fn) => (event) => {
    event.stopPropagation();
    fn();
  };
  return e.jsxs("div", {
    className: `notif-card${n.isRead ? "" : " notif-card--unread"}${route ? "" : " notif-card--static"}`,
    role: route ? "link" : undefined,
    tabIndex: route ? 0 : undefined,
    onClick: route ? () => onOpen(n, route) : undefined,
    onKeyDown: route
      ? (event) => {
          if (event.target === event.currentTarget && (event.key === "Enter" || event.key === " ")) {
            event.preventDefault();
            onOpen(n, route);
          }
        }
      : undefined,
    children: [
      e.jsx("div", { className: "notif-card__icon", "aria-hidden": true, children: e.jsx(Icon, { size: 20 }) }),
      e.jsxs("div", {
        className: "notif-card__body",
        children: [
          e.jsxs("div", {
            className: "notif-card__head",
            children: [
              !n.isRead && e.jsx("span", { className: "notif-card__dot", title: "Unread" }),
              e.jsx("span", { className: "notif-card__title", children: n.title }),
              status && e.jsx(Badge, { tone: status[1], children: status[0] }),
              e.jsx(Badge, { tone: "neutral", children: CATEGORY_LABELS[n.category] ?? "Other" })
            ]
          }),
          e.jsx("div", { className: "notif-card__message", children: n.message }),
          e.jsxs("div", {
            className: "notif-card__meta",
            children: [
              ...related.map(([label, value]) =>
                e.jsxs("span", { children: [label, ": ", e.jsx("b", { children: value })] }, label)
              ),
              e.jsxs("span", { children: ["Date: ", e.jsx("b", { children: formatDateTime(n.createdDate) })] }),
              e.jsx("span", { children: n.isRead ? "Read" : "Unread" })
            ]
          })
        ]
      }),
      e.jsxs("div", {
        className: "notif-card__side",
        children: [
          e.jsx("div", { className: "notif-card__when", children: timeAgo(n.createdDate) ?? formatDateTime(n.createdDate) }),
          e.jsxs("div", {
            className: "notif-card__actions",
            children: [
              route &&
                e.jsxs(l, {
                  size: "sm",
                  variant: "ghost",
                  onClick: stop(() => onOpen(n, route)),
                  children: ["View details ", e.jsx(IconChevronRight, { size: 14 })]
                }),
              !n.isRead &&
                e.jsx(l, {
                  size: "sm",
                  variant: "ghost",
                  "aria-label": "Mark as read",
                  title: "Mark as read",
                  onClick: stop(() => onRead(n)),
                  children: e.jsx(IconCheckRead, { size: 14 })
                }),
              e.jsx(l, {
                size: "sm",
                variant: "ghost",
                "aria-label": "Delete notification",
                title: "Delete",
                onClick: stop(() => onRemove(n)),
                children: e.jsx(IconTrash, { size: 14 })
              })
            ]
          })
        ]
      })
    ]
  });
}

function ee() {
  useFeedStyles();
  const toast = v();
  const navigate = useNavigate();
  const canAnnounce = A("NOTIFICATION.CREATE");
  const [page, setPage] = React.useState(1);
  const [filter, setFilter] = React.useState("");
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [composing, setComposing] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setResult(await m.my({ page, pageSize: 20, ...(FILTERS[filter] ?? {}) }));
    } catch (error) {
      toast.error(error instanceof k ? error.message : "Failed to load notifications.");
    } finally {
      setLoading(false);
    }
  }, [page, filter]);
  React.useEffect(() => {
    load();
  }, [load]);

  const guarded = (fn, failure) => async (...args) => {
    try {
      await fn(...args);
    } catch (error) {
      toast.error(error instanceof k ? error.message : failure);
    }
  };
  const open = guarded(async (n, route) => {
    if (!n.isRead) await m.markRead(n.notificationId);
    navigate(route);
  }, "Could not open the notification.");
  const markRead = guarded(async (n) => {
    await m.markRead(n.notificationId);
    await load();
  }, "Could not mark the notification read.");
  const remove = guarded(async (n) => {
    await m.remove(n.notificationId);
    await load();
  }, "Could not delete the notification.");
  const markAllRead = guarded(async () => {
    await m.readAll();
    await load();
  }, "Could not mark notifications read.");

  const items = result?.items ?? [];
  let feed;
  if (loading && !result) {
    feed = e.jsx(Spinner, { center: true });
  } else if (items.length === 0) {
    feed = e.jsx(EmptyState, {
      title: "All caught up",
      description:
        "Bookings, payments, support tickets and account activities that need attention will appear here."
    });
  } else {
    feed = e.jsxs("div", {
      children: [
        e.jsx("div", {
          className: "notif-feed",
          style: { opacity: loading ? 0.6 : 1 },
          children: items.map((n) =>
            e.jsx(NotificationCard, { n, onOpen: open, onRead: markRead, onRemove: remove }, n.notificationId)
          )
        }),
        e.jsxs("div", {
          className: "data-table__pager",
          children: [
            e.jsxs("span", {
              className: "data-table__pager-info",
              children: ["Page ", result.page, " of ", Math.max(1, result.totalPages), " · ", result.totalCount, " notifications"]
            }),
            e.jsxs("div", {
              className: "data-table__pager-buttons",
              children: [
                e.jsx(l, {
                  variant: "outline",
                  size: "sm",
                  disabled: !result.hasPrevious || loading,
                  onClick: () => setPage(result.page - 1),
                  "aria-label": "Previous page",
                  children: e.jsx(IconChevronLeft, { size: 16 })
                }),
                e.jsx(l, {
                  variant: "outline",
                  size: "sm",
                  disabled: !result.hasNext || loading,
                  onClick: () => setPage(result.page + 1),
                  "aria-label": "Next page",
                  children: e.jsx(IconChevronRight, { size: 16 })
                })
              ]
            })
          ]
        })
      ]
    });
  }

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Notifications",
        description: "Alerts raised for your account, and announcements you send to the space.",
        actions: e.jsxs(e.Fragment, {
          children: [
            e.jsx(l, { variant: "outline", onClick: markAllRead, children: "Mark all read" }),
            canAnnounce && e.jsx(l, { onClick: () => setComposing(true), children: "New announcement" })
          ]
        })
      }),
      e.jsxs(Card, {
        children: [
          e.jsx("div", {
            className: "toolbar",
            children: e.jsx(y, {
              label: "State",
              value: filter,
              placeholder: "All",
              options: FILTER_OPTIONS,
              onChange: (event) => {
                setFilter(event.target.value);
                setPage(1);
              }
            })
          }),
          feed
        ]
      }),
      e.jsx(U, {
        open: composing,
        onClose: () => setComposing(false),
        onSent: () => {
          setComposing(false);
          load();
        }
      })
    ]
  });
}

// ── The announcement modal: original minified code, unchanged ─────────────────
const M=I({audience:_(["2","3"]),category:d().min(1,"Pick a category."),title:d().min(1,"A title is required.").max(200),message:d().min(1,"A message is required.").max(2e3),actionLabel:d().max(60).optional(),actionRoute:d().max(200).optional()});function U({open:p,onClose:u,onSent:g}){const c=v(),{register:i,handleSubmit:x,reset:f,formState:{errors:r,isSubmitting:j}}=w({resolver:T(M),defaultValues:{audience:"2",category:"9"}}),h=x(async t=>{try{const s=await m.broadcast({audience:Number(t.audience),category:Number(t.category),title:t.title,message:t.message,actionLabel:t.actionLabel||null,actionRoute:t.actionRoute||null});c.success(`Announcement sent to ${s} recipient(s).`),f(),g()}catch(s){c.error(s instanceof k?s.message:"Could not send the announcement.")}});return e.jsx(B,{open:p,title:"New announcement",onClose:u,footer:e.jsxs(e.Fragment,{children:[e.jsx(l,{variant:"ghost",onClick:u,children:"Cancel"}),e.jsx(l,{onClick:h,loading:j,children:"Send"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsxs("div",{className:"form-grid",children:[e.jsx(y,{label:"Audience",required:!0,error:r.audience?.message,options:Object.entries(z).map(([t,s])=>({value:t,label:s})),...i("audience")}),e.jsx(y,{label:"Category",required:!0,error:r.category?.message,options:Object.entries(N).map(([t,s])=>({value:t,label:s})),...i("category")})]}),e.jsx(b,{label:"Title",required:!0,error:r.title?.message,placeholder:"Lift maintenance this Saturday",...i("title")}),e.jsxs("div",{className:"field",children:[e.jsxs("label",{className:"field__label",htmlFor:"broadcast-message",children:["Message",e.jsx("span",{className:"field__required","aria-hidden":!0,children:" *"})]}),e.jsx("textarea",{id:"broadcast-message",rows:5,className:`field__input${r.message?" field__input--error":""}`,"aria-invalid":!!r.message,placeholder:"What members need to know, and when.",...i("message")}),r.message&&e.jsx("span",{className:"field__error",role:"alert",children:r.message.message})]}),e.jsxs("div",{className:"form-grid",children:[e.jsx(b,{label:"Action label",error:r.actionLabel?.message,hint:"Optional link shown under the message",placeholder:"View bookings",...i("actionLabel")}),e.jsx(b,{label:"Action route",error:r.actionRoute?.message,hint:"An in-app path, e.g. /bookings",placeholder:"/bookings",...i("actionRoute")})]})]})})}
export{ee as AdminNotificationsPage};
