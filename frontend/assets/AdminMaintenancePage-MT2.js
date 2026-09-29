/**
 * Admin -> Operations -> Maintenance  (/admin/housekeeping)
 *
 * Maintenance & Housekeeping (Maintenance Feature Specification v1.0), step 1:
 *   Checklist     every work item, summary cards and a progress bar
 *   Schedules     the old Maintenance page (workspace blocks), with search and filters
 *   Housekeepers  staff with shift, areas, today's work and on-time rate
 *   My tasks      what a housekeeper sees instead: phone-first, Start / Mark done
 *
 * Everything is decided and counted on the server (GET /api/maintenance/…): who may
 * add, verify or delete, the effective Overdue status, and every percentage. Progress
 * counts Done and Verified as complete (confirmed with the business).
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js lazy-loads this
 * file for the /admin/housekeeping route. The schedule form, complete dialog and date
 * picker are reused from AdminHousekeepingPage-BVEQUG24.js. A new file needs no sw.js
 * precache entry.
 */
import {
  a as useToast,
  r as React,
  A as ApiError,
  j as e,
  G as Spinner,
  p as Button,
  F as Select,
  I as Input,
  D as unwrap,
  E as http,
  i as makeIcon
} from "./index-DI5X4c9f.js";
import { h as schedulesApi } from "./operations-BCRENM62.js";
import { H as SCHEDULE_STATUS, a as TASK_TYPE } from "./operations-CV46ADHC.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { C as ConfirmDialog } from "./ConfirmDialog-Ct1kLH-J.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { f as fmtDateTime } from "./formatters-DFe5xCRi.js";
import { ScheduleFormModal, ScheduleCompleteModal, RgDate, toIstInput, fromIstInput } from "./AdminHousekeepingPage-BVEQUG24.js";

// ── API ─────────────────────────────────────────────────────────────────────

const saveBlob = async (url, params, fallbackName) => {
  const response = await http.get(url, { params, responseType: "blob" });
  const disposition = response.headers?.["content-disposition"] ?? "";
  const named = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1];
  const href = URL.createObjectURL(response.data);
  const link = document.createElement("a");
  link.href = href;
  link.download = named ? decodeURIComponent(named) : fallbackName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
};

const api = {
  me: () => unwrap(http.get("/maintenance/me")),
  options: () => unwrap(http.get("/maintenance/options")),
  work: (params) => unwrap(http.get("/maintenance/work", { params })),
  item: (id) => unwrap(http.get(`/maintenance/work/${id}`)),
  create: (body) => unwrap(http.post("/maintenance/work", body)),
  update: (id, body) => unwrap(http.put(`/maintenance/work/${id}`, body)),
  assign: (id, assignedToUserId) => unwrap(http.post(`/maintenance/work/${id}/assign`, { assignedToUserId })),
  remove: (id) => unwrap(http.delete(`/maintenance/work/${id}`)),
  cancel: (id, reason) => unwrap(http.post(`/maintenance/work/${id}/cancel`, { reason })),
  start: (id) => unwrap(http.post(`/maintenance/work/${id}/start`)),
  done: (id, note, photo) => {
    const form = new FormData();
    form.append("note", note);
    if (photo) form.append("photo", photo);
    // The shared axios instance defaults to JSON; multipart must be said out loud.
    return unwrap(http.post(`/maintenance/work/${id}/done`, form, { headers: { "Content-Type": "multipart/form-data" } }));
  },
  verify: (id) => unwrap(http.post(`/maintenance/work/${id}/verify`)),
  reject: (id, reason) => unwrap(http.post(`/maintenance/work/${id}/reject`, { reason })),
  photoUrl: async (id) => URL.createObjectURL((await http.get(`/maintenance/work/${id}/photo`, { responseType: "blob" })).data),
  myTasks: (tab) => unwrap(http.get("/maintenance/my-tasks", { params: { tab } })),
  exportWork: (params) => saveBlob("/maintenance/work/export", params, "maintenance.csv"),
  housekeepers: (params) => unwrap(http.get("/maintenance/housekeepers", { params })),
  createHousekeeper: (body) => unwrap(http.post("/maintenance/housekeepers", body)),
  updateHousekeeper: (userId, body) => unwrap(http.put(`/maintenance/housekeepers/${userId}`, body)),
  deactivate: (userId, reassignToUserId) => unwrap(http.post(`/maintenance/housekeepers/${userId}/deactivate`, { reassignToUserId })),
  activate: (userId) => unwrap(http.post(`/maintenance/housekeepers/${userId}/activate`)),
  workLog: (userId) => unwrap(http.get(`/maintenance/housekeepers/${userId}/log`))
};

// ── Labels and formatting ───────────────────────────────────────────────────

const STATUS = {
  1: { label: "Pending", cls: "pending" },
  2: { label: "In progress", cls: "progress" },
  3: { label: "Done", cls: "done" },
  4: { label: "Verified", cls: "verified" },
  5: { label: "Rework", cls: "rework" },
  6: { label: "Overdue", cls: "overdue" },
  7: { label: "Cancelled", cls: "cancelled" }
};
const PRIORITY = { 3: "High", 2: "Medium", 1: "Low" };
const PRIORITY_CLS = { 3: "high", 2: "medium", 1: "low" };
const SHIFT = { 1: "Morning", 2: "Evening", 3: "Night" };
const OPEN = [1, 2, 5, 6];

const IST = "Asia/Kolkata";
const istDay = (value) => new Intl.DateTimeFormat("en-CA", { timeZone: IST }).format(new Date(value));
const shiftDay = (day, delta) => {
  const d = new Date(`${day}T00:00:00Z`);
  d.setUTCDate(d.getUTCDate() + delta);
  return d.toISOString().slice(0, 10);
};
const istTime = (value) =>
  new Date(value).toLocaleTimeString("en-IN", { timeZone: IST, hour: "2-digit", minute: "2-digit", hour12: true }).toUpperCase();
const dayWord = (value) => {
  const day = istDay(value), today = istDay(Date.now());
  if (day === today) return "Today";
  if (day === shiftDay(today, -1)) return "Yesterday";
  if (day === shiftDay(today, 1)) return "Tomorrow";
  return new Date(value).toLocaleDateString("en-IN", { timeZone: IST, day: "2-digit", month: "short" });
};
const dueLabel = (value) => `${dayWord(value)}, ${istTime(value)}`;
const dateLabel = (day) => new Date(`${day}T00:00:00Z`).toLocaleDateString("en-IN", { timeZone: "UTC", day: "2-digit", month: "short", year: "numeric" });
const minutesBetween = (a, b) => (a && b ? Math.max(0, Math.round((new Date(b) - new Date(a)) / 60000)) : null);
const duration = (mins) => (mins == null ? "—" : mins < 60 ? `${mins} minute${mins === 1 ? "" : "s"}` : `${Math.floor(mins / 60)} h ${mins % 60} min`);

const initials = (name) =>
  (name || "?").trim().split(/\s+/).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? "").join("") || "?";
// A validation envelope says "One or more validation errors…"; the field message is the useful one.
const errorText = (error, fallback) =>
  error instanceof ApiError ? error.response?.errors?.[0]?.message ?? error.message : fallback;
const workSub = (w) => `${w.workNumber} · ${w.areaName} · ${w.category}`;

const IconSearch = makeIcon("search", [
  ["circle", { cx: "11", cy: "11", r: "8", key: "4ej97u" }],
  ["path", { d: "m21 21-4.3-4.3", key: "1qie3q" }]
]);
const IconCheck = makeIcon("check", [["path", { d: "M20 6 9 17l-5-5", key: "1gmf2c" }]]);

// ── Styles ──────────────────────────────────────────────────────────────────

const STYLE_ID = "maintenance-page-styles";
const STYLES = `
.mt-cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:var(--space-4);margin:var(--space-5) 0 var(--space-4)}
@media(max-width:1100px){.mt-cards{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.mt-cards{gap:var(--space-3)}.mt-card{padding:var(--space-3) var(--space-4)}}
.mt-card,.mt-panel{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm)}
.mt-card{padding:var(--space-4) var(--space-5)}
.mt-card__label{font-size:var(--font-size-sm);color:var(--text-secondary)}
.mt-card__value{margin:4px 0 2px;font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:800;color:var(--text-primary);font-variant-numeric:tabular-nums;line-height:1.2}
.mt-card__value--ok{color:var(--color-success)}
.mt-card__value--late{color:#A8761B}
.mt-card__sub{font-size:var(--font-size-xs);color:var(--text-muted)}
.mt-progress{display:flex;align-items:center;gap:var(--space-5);padding:var(--space-4) var(--space-5);margin-bottom:var(--space-4)}
.mt-progress__body{flex:1;min-width:0}
.mt-progress__head{display:flex;justify-content:space-between;gap:var(--space-3);flex-wrap:wrap;margin-bottom:10px;font-size:var(--font-size-sm)}
.mt-progress__head strong{color:var(--text-primary)}
.mt-progress__head span{color:var(--text-secondary)}
.mt-progress__pct{font-family:var(--font-display);font-size:2.25rem;font-weight:800;color:#CC9A3D;font-variant-numeric:tabular-nums;line-height:1}
.mt-bar{position:relative;height:12px;border-radius:999px;background:rgb(var(--accent-rgb) / 16%);overflow:hidden;display:flex}
.mt-bar__verified{background:#CC9A3D}
.mt-bar__waiting{background:rgb(204 154 61 / 45%)}
.mt-legend{display:flex;gap:var(--space-4);margin-top:8px;font-size:var(--font-size-xs);color:var(--text-muted)}
.mt-legend i{display:inline-block;width:10px;height:10px;border-radius:3px;margin-inline-end:6px;vertical-align:-1px}
.mt-panel{padding:var(--space-4) var(--space-5) var(--space-5)}
.mt-tabs{display:flex;gap:var(--space-6);border-bottom:1px solid var(--border-subtle);margin-bottom:var(--space-4);overflow-x:auto}
.mt-tab{all:unset;padding:10px 0 12px;font-weight:700;color:var(--text-secondary);cursor:pointer;border-bottom:3px solid transparent;margin-bottom:-1px;white-space:nowrap}
.mt-tab:hover,.mt-tab:focus-visible{color:var(--text-primary)}
.mt-tab[aria-selected=true]{color:#A8761B;border-bottom-color:#CC9A3D}
.mt-toolbar{display:grid;grid-template-columns:minmax(220px,1.6fr) repeat(5,minmax(130px,1fr));gap:var(--space-3);margin-bottom:var(--space-4)}
.mt-toolbar--hk{grid-template-columns:minmax(220px,1.6fr) minmax(150px,.7fr) minmax(190px,.8fr) 1.6fr}
.mt-toolbar--sch{grid-template-columns:minmax(220px,1.6fr) repeat(3,minmax(140px,1fr))}
@media(max-width:1100px){.mt-toolbar,.mt-toolbar--hk,.mt-toolbar--sch{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.mt-toolbar,.mt-toolbar--hk,.mt-toolbar--sch{grid-template-columns:minmax(0,1fr)}}
.mt-toolbar .field{min-width:0}
.mt-toolbar .field__label{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}
.mt-toolbar .field__input{height:40px}
.mt-search{position:relative}
.mt-search svg{position:absolute;inset-inline-start:12px;top:50%;transform:translateY(-50%);color:var(--text-muted);pointer-events:none}
.mt-search input{width:100%;height:40px;padding:8px 12px 8px 36px}
.mt-date{display:flex;gap:6px;min-width:0}
.mt-date .field{flex:1}
.mt-table-wrap{overflow-x:auto;position:relative}
.mt-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.mt-table th{padding:10px 12px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--text-secondary);background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.mt-table td{padding:12px;border-bottom:1px solid var(--border-subtle);vertical-align:middle}
.mt-table tbody tr:hover{background:var(--surface-hover)}
.mt-tick{display:grid;place-items:center;width:22px;height:22px;border:1.5px solid var(--border-strong);border-radius:6px;color:transparent}
.mt-tick--on{background:#CC9A3D;border-color:#CC9A3D;color:#241F19}
.mt-title{font-weight:700;color:var(--text-primary)}
.mt-sub{font-size:var(--font-size-xs);color:var(--text-muted);margin-top:2px;overflow-wrap:anywhere}
.mt-who{display:flex;align-items:center;gap:10px;white-space:nowrap}
.mt-avatar{flex-shrink:0;display:grid;place-items:center;width:32px;height:32px;border-radius:50%;background:rgb(var(--accent-rgb) / 18%);color:var(--text-primary);font-size:var(--font-size-xs);font-weight:700}
.mt-avatar--lg{width:44px;height:44px;font-size:var(--font-size-sm)}
.mt-due{white-space:nowrap}
.mt-due--late{color:#A8761B;font-weight:700}
.mt-pill,.mt-chip{display:inline-flex;align-items:center;padding:3px 10px;border-radius:999px;font-size:var(--font-size-xs);font-weight:700;white-space:nowrap}
.mt-chip{border-radius:6px}
.mt-pill--pending{background:#EEEAE3;color:#4F4A42}
.mt-pill--progress{background:#E4ECF7;color:#255089}
.mt-pill--done{background:#F5EBD0;color:#7A5D10}
.mt-pill--verified,.mt-pill--active{background:#E3F3EA;color:#15603C}
.mt-pill--rework{background:#FBE7E5;color:#93190F}
.mt-pill--overdue{background:#FBE0CF;color:#8A4A0C}
.mt-pill--cancelled,.mt-pill--inactive{background:#EDEAE2;color:#6E6559}
.mt-pill--scheduled{background:#E4ECF7;color:#255089}
.mt-chip--high{background:#FBE7E5;color:#93190F}
.mt-chip--medium{background:#FBE0CF;color:#8A4A0C}
.mt-chip--low{background:#EDEAE2;color:#4F4A42}
.mt-actions{display:flex;gap:var(--space-2);justify-content:flex-end;align-items:center;white-space:nowrap}
.mt-pager{display:flex;align-items:center;justify-content:space-between;gap:var(--space-3);margin-top:var(--space-4);font-size:var(--font-size-sm);color:var(--text-secondary)}
.mt-pager div{display:flex;gap:var(--space-2);align-items:center}
.mt-note{margin-top:var(--space-4);font-size:var(--font-size-sm);color:var(--text-muted)}
.mt-notebar{margin-bottom:var(--space-4);padding:10px 14px;border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%);border:1px solid rgb(var(--accent-rgb) / 25%);font-size:var(--font-size-sm);color:var(--text-secondary)}
.mt-menu{position:relative}
.mt-menu__btn{all:unset;display:grid;place-items:center;width:34px;height:32px;border:1px solid var(--border-strong);border-radius:var(--radius-md);cursor:pointer;color:var(--text-secondary);font-weight:700}
.mt-menu__btn:hover,.mt-menu__btn:focus-visible{background:var(--surface-hover)}
.mt-menu__list{position:absolute;inset-inline-end:0;top:calc(100% + 4px);z-index:30;min-width:190px;padding:4px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);box-shadow:var(--shadow-lg);display:flex;flex-direction:column}
.mt-menu__item{all:unset;padding:8px 12px;border-radius:var(--radius-sm);font-size:var(--font-size-sm);cursor:pointer;white-space:nowrap}
.mt-menu__item:hover,.mt-menu__item:focus-visible{background:var(--surface-hover)}
.mt-menu__item--danger{color:var(--color-danger)}
.mt-rate{display:flex;align-items:center;gap:10px;min-width:150px}
.mt-rate .mt-bar{flex:1;height:8px}
.mt-rate .mt-bar__verified{border-radius:999px}
.mt-rate b{font-variant-numeric:tabular-nums;min-width:38px;text-align:right}
.mt-mcards{display:none}
@media(max-width:767px){
.mt-table-wrap{display:none}
.mt-mcards{display:flex;flex-direction:column;gap:var(--space-3)}
.mt-mcard{display:flex;flex-direction:column;gap:8px;padding:var(--space-3) var(--space-4);border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card)}
.mt-mcard__top{display:flex;justify-content:space-between;gap:var(--space-3);align-items:flex-start}
.mt-mcard__meta{display:flex;flex-wrap:wrap;gap:6px 12px;align-items:center;font-size:var(--font-size-xs);color:var(--text-secondary)}
.mt-mcard .mt-actions{justify-content:flex-start;flex-wrap:wrap}
.mt-progress{flex-direction:column-reverse;align-items:stretch;gap:var(--space-2)}
.page-header__actions{flex-wrap:wrap}
}
.mt-form{display:flex;flex-direction:column;gap:var(--space-4)}
.mt-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--space-4)}
@media(max-width:600px){.mt-grid{grid-template-columns:minmax(0,1fr)}}
.mt-textarea{width:100%;min-height:92px;resize:vertical;padding:10px 12px;font:inherit}
.mt-block{display:flex;gap:12px;align-items:flex-start;padding:12px 14px;border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%);border:1px solid rgb(var(--accent-rgb) / 25%);font-size:var(--font-size-sm);cursor:pointer}
.mt-block--off{opacity:.6;cursor:not-allowed}
.mt-block span small{color:var(--text-muted)}
.mt-check{display:flex;align-items:center;gap:10px;font-size:var(--font-size-sm);cursor:pointer}
.mt-block input,.mt-check input{width:18px;height:18px;margin-top:1px;accent-color:#CC9A3D;flex-shrink:0}
.mt-areas{display:flex;flex-wrap:wrap;gap:6px}
.mt-areas button{all:unset;padding:6px 12px;border:1px solid var(--border-strong);border-radius:999px;font-size:var(--font-size-xs);font-weight:600;cursor:pointer;color:var(--text-secondary)}
.mt-areas button[aria-pressed=true]{background:rgb(var(--accent-rgb) / 18%);border-color:#CC9A3D;color:var(--text-primary)}
.mt-areas button:focus-visible{outline:2px solid #CC9A3D;outline-offset:2px}
.mt-verify{display:grid;grid-template-columns:220px minmax(0,1fr);gap:var(--space-5)}
@media(max-width:640px){.mt-verify{grid-template-columns:minmax(0,1fr)}}
.mt-photo{display:grid;place-items:center;aspect-ratio:4/3;border-radius:var(--radius-md);background:#EFE6D6;color:#6E6559;font-size:var(--font-size-sm);overflow:hidden;border:1px solid var(--border-subtle)}
.mt-photo img{width:100%;height:100%;object-fit:cover;cursor:zoom-in}
.mt-facts{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-3);margin:var(--space-3) 0}
.mt-facts div small{display:block;font-size:var(--font-size-xs);color:var(--text-muted)}
.mt-facts div b{font-weight:600}
.mt-quote{padding:10px 14px;border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%);border:1px solid rgb(var(--accent-rgb) / 25%);font-size:var(--font-size-sm);white-space:pre-wrap}
.mt-quote--rework{background:#FBE7E5;border-color:#F3C6C1;color:#93190F}
.mt-kv{display:grid;grid-template-columns:150px minmax(0,1fr);gap:8px 16px;font-size:var(--font-size-sm)}
.mt-kv dt{color:var(--text-muted)}
.mt-kv dd{margin:0;font-weight:600}
.mt-secret{display:flex;align-items:center;justify-content:space-between;gap:var(--space-3);padding:12px 14px;border-radius:var(--radius-md);background:var(--regalis-black);color:var(--regalis-white);font-family:ui-monospace,Consolas,monospace;font-size:1.1rem;letter-spacing:.04em}
.mt-stats{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-3)}
.mt-stat{padding:10px 14px;border:1px solid rgb(var(--accent-rgb) / 30%);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%)}
.mt-stat small{display:block;font-size:var(--font-size-xs);color:var(--text-secondary)}
.mt-stat b{font-family:var(--font-display);font-size:var(--font-size-xl);font-variant-numeric:tabular-nums}
.mt-list{display:flex;flex-direction:column;border:1px solid var(--border-subtle);border-radius:var(--radius-md);max-height:280px;overflow-y:auto}
.mt-list > div{display:flex;justify-content:space-between;gap:var(--space-3);align-items:center;padding:8px 12px;border-bottom:1px solid var(--border-subtle);font-size:var(--font-size-sm)}
.mt-list > div:last-child{border-bottom:0}
.modal:has(.mt-modal){max-width:680px}
.modal:has(.mt-modal--wide){max-width:760px}
/* Housekeeper phone view */
.mt-phone{max-width:560px;margin:0 auto;display:flex;flex-direction:column;gap:var(--space-4)}
.mt-phone__head{padding:var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);box-shadow:var(--shadow-sm)}
.mt-phone__who{display:flex;justify-content:space-between;align-items:flex-start;gap:var(--space-3)}
.mt-phone__title{margin:0;font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:800;color:var(--text-primary)}
.mt-phone__line{display:flex;justify-content:space-between;margin:var(--space-4) 0 8px;font-weight:700}
.mt-phone__line span{color:#A8761B}
.mt-pills{display:flex;gap:var(--space-2);flex-wrap:wrap}
.mt-pills button{all:unset;min-height:44px;padding:0 18px;display:inline-flex;align-items:center;border:1px solid var(--border-strong);border-radius:999px;font-weight:700;cursor:pointer;background:var(--surface-card);color:var(--text-primary)}
.mt-pills button[aria-selected=true]{background:#241F19;border-color:#241F19;color:#E9C77B}
.mt-pills button:focus-visible{outline:2px solid #CC9A3D;outline-offset:2px}
.mt-task{display:flex;flex-direction:column;gap:10px;padding:var(--space-4);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card)}
.mt-task__top{display:flex;justify-content:space-between;gap:var(--space-3);align-items:flex-start}
.mt-task__title{font-weight:800;font-size:var(--font-size-md);color:var(--text-primary)}
.mt-task__meta{display:flex;align-items:center;gap:10px;font-size:var(--font-size-sm);color:var(--text-secondary);flex-wrap:wrap}
.mt-task__btns{display:flex;gap:var(--space-2)}
.mt-task__btns > *{flex:1}
.mt-bigbtn{all:unset;box-sizing:border-box;min-height:48px;padding:0 16px;display:flex;align-items:center;justify-content:center;border-radius:var(--radius-md);font-weight:800;cursor:pointer;background:#CC9A3D;color:#241F19;border:1px solid #B8872E;text-align:center}
.mt-bigbtn--ghost{background:var(--surface-card);border-color:var(--border-strong);color:var(--text-primary)}
.mt-bigbtn[disabled]{opacity:.55;cursor:progress}
.mt-bigbtn:focus-visible{outline:2px solid #241F19;outline-offset:2px}
.mt-task__btns .mt-bigbtn{flex:1}
.mt-sheet-backdrop{position:fixed;inset:0;z-index:110;background:rgb(36 31 25 / 45%);display:flex;align-items:flex-end;justify-content:center}
.mt-sheet{width:100%;max-width:560px;max-height:92vh;overflow-y:auto;padding:var(--space-5);border-radius:20px 20px 0 0;background:var(--surface-card);box-shadow:var(--shadow-lg);display:flex;flex-direction:column;gap:var(--space-4)}
.mt-sheet h3{margin:0;font-family:var(--font-display);font-size:var(--font-size-xl)}
.mt-shots{display:grid;grid-template-columns:1fr 1fr;gap:var(--space-2)}
.mt-shot{position:relative;min-height:48px;display:flex;align-items:center;justify-content:center;border:1.5px dashed var(--border-strong);border-radius:var(--radius-md);font-weight:700;cursor:pointer;background:rgb(var(--accent-rgb) / 5%)}
.mt-shot input{position:absolute;inset:0;opacity:0;cursor:pointer}
.mt-shot:focus-within{outline:2px solid #CC9A3D;outline-offset:2px}
.mt-preview{display:flex;align-items:center;gap:var(--space-3);font-size:var(--font-size-sm)}
.mt-preview img{width:64px;height:64px;object-fit:cover;border-radius:var(--radius-sm);border:1px solid var(--border-subtle)}
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

function useDebounced(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

// ── Small pieces ────────────────────────────────────────────────────────────

const StatusPill = ({ status }) => e.jsx("span", { className: `mt-pill mt-pill--${STATUS[status]?.cls ?? "pending"}`, children: STATUS[status]?.label ?? "—" });
const PriorityChip = ({ priority }) => e.jsx("span", { className: `mt-chip mt-chip--${PRIORITY_CLS[priority]}`, children: PRIORITY[priority] });
const Who = ({ name }) => e.jsxs("span", { className: "mt-who", children: [e.jsx("span", { className: "mt-avatar", "aria-hidden": true, children: initials(name) }), name] });

function RowMenu({ label, items }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (!open) return;
    const close = (event) => ref.current && !ref.current.contains(event.target) && setOpen(false);
    const key = (event) => event.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", key);
    };
  }, [open]);
  const shown = items.filter(Boolean);
  if (shown.length === 0) return null;
  return e.jsxs("div", {
    className: "mt-menu",
    ref,
    children: [
      e.jsx("button", { type: "button", className: "mt-menu__btn", "aria-haspopup": "menu", "aria-expanded": open, "aria-label": label, onClick: () => setOpen((o) => !o), children: "⋯" }),
      open &&
        e.jsx("div", {
          className: "mt-menu__list",
          role: "menu",
          children: shown.map((item) =>
            e.jsx("button", { type: "button", role: "menuitem", className: `mt-menu__item${item.danger ? " mt-menu__item--danger" : ""}`, onClick: () => { setOpen(false); item.onClick(); }, children: item.label }, item.label)
          )
        })
    ]
  });
}

function Pager({ result, onPage }) {
  if (!result || result.totalCount === 0) return null;
  return e.jsxs("div", {
    className: "mt-pager",
    children: [
      e.jsx("span", { children: `Page ${result.page} of ${Math.max(1, result.totalPages)} · ${result.totalCount} record${result.totalCount === 1 ? "" : "s"}` }),
      e.jsxs("div", {
        children: [
          e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasPrevious, onClick: () => onPage(result.page - 1), "aria-label": "Previous page", children: "‹" }),
          e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasNext, onClick: () => onPage(result.page + 1), "aria-label": "Next page", children: "›" })
        ]
      })
    ]
  });
}

function SearchBox({ value, onChange, placeholder }) {
  return e.jsxs("div", {
    className: "mt-search",
    children: [
      e.jsx(IconSearch, { size: 16 }),
      e.jsx("input", { className: "field__input", "aria-label": placeholder, placeholder, value, onChange: (event) => onChange(event.target.value) })
    ]
  });
}

function TextArea({ label, value, onChange, placeholder, required, hint, autoFocus }) {
  const id = React.useId();
  return e.jsxs("div", {
    className: "field",
    children: [
      e.jsxs("label", { className: "field__label", htmlFor: id, children: [label, required && e.jsx("span", { className: "field__required", "aria-hidden": true, children: " *" })] }),
      e.jsx("textarea", { id, className: "field__input mt-textarea", value, placeholder, autoFocus, onChange: (event) => onChange(event.target.value) }),
      hint && e.jsx("span", { className: "field__hint", children: hint })
    ]
  });
}

/** Photo proof, fetched with the caller's token (an <img src> would not send it). */
function PhotoProof({ work }) {
  const [url, setUrl] = React.useState(null);
  const [failed, setFailed] = React.useState(false);
  React.useEffect(() => {
    if (!work.hasPhoto) return undefined;
    let href = null, live = true;
    api.photoUrl(work.workItemId).then((u) => { href = u; live ? setUrl(u) : URL.revokeObjectURL(u); }).catch(() => live && setFailed(true));
    return () => { live = false; if (href) URL.revokeObjectURL(href); };
  }, [work.workItemId, work.hasPhoto]);
  return e.jsx("div", {
    className: "mt-photo",
    children: !work.hasPhoto
      ? "No photo added"
      : url
        ? e.jsx("img", { src: url, alt: `Photo proof for ${work.workNumber}`, onClick: () => window.open(url, "_blank") })
        : failed ? "Photo unavailable" : e.jsx(Spinner, {})
  });
}

// ── Checklist ───────────────────────────────────────────────────────────────

// The list opens on every date (open work first, soonest due on top) so newly added
// work is always visible; the summary cards above still count today.
const DATE_OPTIONS = [
  { value: "all", label: "All dates" },
  { value: "today", label: "Due today" },
  { value: "tomorrow", label: "Due tomorrow" },
  { value: "yesterday", label: "Due yesterday" },
  { value: "pick", label: "Pick a date…" }
];

function rowAction(w) {
  if (w.canVerify) return "verify";
  if ((w.status === 1 || w.status === 6) && w.canEdit) return "reassign";
  return "open";
}

function ChecklistTab({ me, options, reloadKey, focus, onChanged, onSummary, onFilters, onEdit }) {
  const toast = useToast();
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [area, setArea] = React.useState("");
  const [category, setCategory] = React.useState("");
  const [assignee, setAssignee] = React.useState("");
  const [dateMode, setDateMode] = React.useState("all");
  const [pickedDate, setPickedDate] = React.useState(istDay(Date.now()));
  // After a save, show the day the work is due — otherwise work added late in the
  // evening for tomorrow seems to vanish from the Today view. Filters that would hide
  // it are cleared too.
  React.useEffect(() => {
    if (!focus) return;
    const today = istDay(Date.now());
    if (dateMode !== "all") {
      if (focus.day === today) setDateMode("today");
      else if (focus.day === shiftDay(today, 1)) setDateMode("tomorrow");
      else if (focus.day === shiftDay(today, -1)) setDateMode("yesterday");
      else { setPickedDate(focus.day); setDateMode("pick"); }
    }
    setSearch(""); setStatus(""); setArea(""); setCategory(""); setAssignee("");
  }, [focus]);
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [modal, setModal] = React.useState(null); // { kind, work }
  const debounced = useDebounced(search);

  const today = istDay(Date.now());
  const date = dateMode === "today" ? today : dateMode === "yesterday" ? shiftDay(today, -1) : dateMode === "tomorrow" ? shiftDay(today, 1) : dateMode === "pick" ? pickedDate : undefined;
  const params = React.useMemo(() => ({
    search: debounced.trim() || undefined,
    status: status ? Number(status) : undefined,
    area: area || undefined,
    category: category || undefined,
    assigneeId: assignee ? Number(assignee) : undefined,
    date: dateMode === "all" ? undefined : date,
    all: dateMode === "all" ? true : undefined
  }), [debounced, status, area, category, assignee, dateMode, date]);

  React.useEffect(() => { onFilters(params); }, [params]);
  React.useEffect(() => { setPage(1); }, [params]);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const result = await api.work({ ...params, page, pageSize: 10 });
      setData(result);
      onSummary(result.summary);
    } catch (error) {
      toast.error(errorText(error, "Could not load the checklist."));
    } finally {
      setLoading(false);
    }
  }, [params, page, reloadKey]);
  React.useEffect(() => { load(); }, [load]);

  const changed = () => { setModal(null); load(); onChanged?.(); };
  const filtered = !!(debounced.trim() || status || area || category || assignee);
  const rows = data?.work.items ?? [];

  const actions = (w) => {
    const kind = rowAction(w);
    return e.jsxs("div", {
      className: "mt-actions",
      children: [
        kind === "verify" && e.jsxs(e.Fragment, { children: [
          e.jsx(Button, { size: "sm", onClick: () => setModal({ kind: "verify", work: w }), children: "Verify" }),
          e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setModal({ kind: "verify", work: w, reject: true }), children: "Reject" })
        ] }),
        kind === "reassign" && e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setModal({ kind: "reassign", work: w }), children: "Reassign" }),
        kind === "open" && e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setModal({ kind: "open", work: w }), children: "Open" }),
        e.jsx(RowMenu, {
          label: `More actions for ${w.workNumber}`,
          items: [
            kind !== "open" && { label: "Open", onClick: () => setModal({ kind: "open", work: w }) },
            w.canEdit && { label: "Edit", onClick: () => onEdit(w, load) },
            w.canEdit && kind !== "reassign" && w.status !== 3 && { label: "Reassign", onClick: () => setModal({ kind: "reassign", work: w }) },
            w.canEdit && { label: "Cancel work", onClick: () => setModal({ kind: "cancel", work: w }), danger: true },
            w.canDelete && { label: "Delete", onClick: () => setModal({ kind: "delete", work: w }), danger: true }
          ]
        })
      ]
    });
  };

  const late = (w) => w.status === 6;
  const tick = (w) => e.jsx("span", { className: `mt-tick${w.status === 4 ? " mt-tick--on" : ""}`, role: "img", "aria-label": w.status === 4 ? "Verified" : "Not verified", children: e.jsx(IconCheck, { size: 14, strokeWidth: 3 }) });

  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("div", {
        className: "mt-toolbar",
        children: [
          e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Search work ID, task, area…" }),
          e.jsx(Select, { label: "Status", value: status, placeholder: "All statuses", options: [1, 2, 3, 4, 5, 6, 7].map((s) => ({ value: String(s), label: s === 3 ? "Done (waiting)" : STATUS[s].label })), onChange: (ev) => setStatus(ev.target.value) }),
          e.jsx(Select, { label: "Area", value: area, placeholder: "All areas", options: options.areas.map((a) => ({ value: a.name, label: a.name })), onChange: (ev) => setArea(ev.target.value) }),
          e.jsx(Select, { label: "Category", value: category, placeholder: "All categories", options: options.categories.map((c) => ({ value: c, label: c })), onChange: (ev) => setCategory(ev.target.value) }),
          me.isHousekeeper
            ? e.jsx("span", {})
            : e.jsx(Select, { label: "Housekeeper", value: assignee, placeholder: "All housekeepers", options: options.housekeepers.map((h) => ({ value: String(h.userId), label: h.fullName })), onChange: (ev) => setAssignee(ev.target.value) }),
          e.jsxs("div", {
            className: "mt-date",
            children: [
              e.jsx(Select, { label: "Date", value: dateMode, options: DATE_OPTIONS, onChange: (ev) => setDateMode(ev.target.value) }),
              dateMode === "pick" && e.jsx(Input, { label: "Pick a date", type: "date", value: pickedDate, onChange: (ev) => ev.target.value && setPickedDate(ev.target.value) })
            ]
          })
        ]
      }),
      loading && !data
        ? e.jsx(Spinner, { center: true })
        : rows.length === 0
          ? e.jsx(EmptyState, {
              title: filtered ? "No work matches these filters" : dateMode === "all" ? "No work added yet — add the first task." : dateMode === "today" ? "No work due today." : "No work due on this date",
              description: filtered ? "Try a different status, area or housekeeper." : me.canManageWork ? "Use + Add work to create a task and assign it to a housekeeper." : "Tasks assigned to you will appear here."
            })
          : e.jsxs("div", {
              style: { opacity: loading ? 0.6 : 1 },
              children: [
                e.jsx("div", {
                  className: "mt-table-wrap",
                  children: e.jsxs("table", {
                    className: "mt-table",
                    children: [
                      e.jsx("thead", { children: e.jsxs("tr", { children: [
                        e.jsx("th", { style: { width: 44 }, children: e.jsx("span", { className: "sr-only", children: "Verified" }) }),
                        e.jsx("th", { children: "Work item" }),
                        e.jsx("th", { children: "Priority" }),
                        e.jsx("th", { children: "Assigned to" }),
                        e.jsx("th", { children: "Due" }),
                        e.jsx("th", { children: "Status" }),
                        e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Actions" }) })
                      ] }) }),
                      e.jsx("tbody", {
                        children: rows.map((w) =>
                          e.jsxs("tr", { children: [
                            e.jsx("td", { children: tick(w) }),
                            e.jsxs("td", { children: [e.jsx("div", { className: "mt-title", children: w.title }), e.jsx("div", { className: "mt-sub", children: workSub(w) })] }),
                            e.jsx("td", { children: e.jsx(PriorityChip, { priority: w.priority }) }),
                            e.jsx("td", { children: e.jsx(Who, { name: w.assignedToName }) }),
                            e.jsx("td", { className: `mt-due${late(w) ? " mt-due--late" : ""}`, children: dueLabel(w.dueUtc) }),
                            e.jsx("td", { children: e.jsx(StatusPill, { status: w.status }) }),
                            e.jsx("td", { children: actions(w) })
                          ] }, w.workItemId)
                        )
                      })
                    ]
                  })
                }),
                e.jsx("div", {
                  className: "mt-mcards",
                  children: rows.map((w) =>
                    e.jsxs("div", { className: "mt-mcard", children: [
                      e.jsxs("div", { className: "mt-mcard__top", children: [
                        e.jsxs("div", { style: { minWidth: 0 }, children: [e.jsx("div", { className: "mt-title", children: w.title }), e.jsx("div", { className: "mt-sub", children: workSub(w) })] }),
                        e.jsx(StatusPill, { status: w.status })
                      ] }),
                      e.jsxs("div", { className: "mt-mcard__meta", children: [
                        e.jsx(PriorityChip, { priority: w.priority }),
                        e.jsx("span", { children: w.assignedToName }),
                        e.jsx("span", { className: late(w) ? "mt-due--late" : "", children: dueLabel(w.dueUtc) })
                      ] }),
                      actions(w)
                    ] }, w.workItemId)
                  )
                }),
                e.jsx(Pager, { result: data.work, onPage: setPage })
              ]
            }),
      modal?.kind === "verify" && e.jsx(VerifyModal, { work: modal.work, startWithReject: !!modal.reject, onClose: () => setModal(null), onDone: changed }),
      modal?.kind === "reassign" && e.jsx(ReassignModal, { work: modal.work, housekeepers: options.housekeepers, onClose: () => setModal(null), onDone: changed }),
      modal?.kind === "open" && e.jsx(WorkDetailModal, { workId: modal.work.workItemId, onClose: () => setModal(null), onChanged: load, onEdit: (w) => { setModal(null); onEdit(w, load); } }),
      modal?.kind === "cancel" && e.jsx(ReasonModal, {
        title: `Cancel ${modal.work.workNumber}?`,
        intro: "The task leaves the checklist totals; its history stays. A linked schedule is cancelled and the workspace released.",
        label: "Reason",
        placeholder: "e.g. Room handed back early, no longer needed",
        confirm: "Cancel work",
        run: (reason) => api.cancel(modal.work.workItemId, reason),
        success: "Work cancelled.",
        onClose: () => setModal(null),
        onDone: changed
      }),
      e.jsx(ConfirmDialog, {
        open: modal?.kind === "delete",
        danger: true,
        confirmLabel: "Delete work",
        title: `Delete ${modal?.work?.workNumber ?? ""}?`,
        message: "Only tasks that have not started can be deleted. The work ID is not reused.",
        onConfirm: async () => {
          try {
            await api.remove(modal.work.workItemId);
            toast.success("Work deleted.");
            changed();
          } catch (error) {
            toast.error(errorText(error, "Could not delete the work."));
          }
        },
        onClose: () => setModal(null)
      })
    ]
  });
}

function ReasonModal({ title, intro, label, placeholder, confirm, run, success, onClose, onDone }) {
  const toast = useToast();
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    if (!reason.trim()) { toast.error("Give a reason."); return; }
    setBusy(true);
    try {
      await run(reason.trim());
      toast.success(success);
      onDone();
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
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Back" }), e.jsx(Button, { onClick: submit, loading: busy, children: confirm })] }),
    children: e.jsxs("div", { className: "mt-form mt-modal", children: [
      e.jsx("p", { className: "mt-sub", style: { margin: 0, fontSize: "var(--font-size-sm)" }, children: intro }),
      e.jsx(TextArea, { label, required: true, value: reason, placeholder, autoFocus: true, onChange: setReason })
    ] })
  });
}

function ReassignModal({ work, housekeepers, onClose, onDone }) {
  const toast = useToast();
  const [to, setTo] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const choices = housekeepers.filter((h) => h.userId !== work.assignedToUserId);
  const submit = async () => {
    if (!to) { toast.error("Select an active housekeeper."); return; }
    setBusy(true);
    try {
      await api.assign(work.workItemId, Number(to));
      toast.success(`${work.workNumber} reassigned.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not reassign the work."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Reassign — ${work.workNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, children: "Reassign" })] }),
    children: e.jsxs("div", { className: "mt-form mt-modal", children: [
      e.jsxs("div", { children: [e.jsx("div", { className: "mt-title", children: work.title }), e.jsx("div", { className: "mt-sub", children: `${workSub(work)} · due ${dueLabel(work.dueUtc)} · now with ${work.assignedToName}` })] }),
      e.jsx(Select, { label: "Assign to", required: true, value: to, placeholder: choices.length ? "Select a housekeeper" : "No other active housekeeper", options: choices.map((h) => ({ value: String(h.userId), label: `${h.fullName} (${SHIFT[h.shift] ?? "—"})${h.assignedAreas && h.assignedAreas.split(",").map((a) => a.trim()).includes(work.areaName) ? ` · covers ${work.areaName}` : ""}` })), hint: "Only active housekeepers appear here. They are notified in the app.", onChange: (ev) => setTo(ev.target.value) })
    ] })
  });
}

function VerifyModal({ work, startWithReject, onClose, onDone }) {
  const toast = useToast();
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(null);
  const reasonRef = React.useRef(null);
  React.useEffect(() => { if (startWithReject) reasonRef.current?.focus(); }, [startWithReject]);
  const verify = async () => {
    setBusy("verify");
    try {
      await api.verify(work.workItemId);
      toast.success(`${work.workNumber} verified.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not verify the work."));
    } finally {
      setBusy(null);
    }
  };
  const reject = async () => {
    if (!reason.trim()) { toast.error("Tell the housekeeper what needs redoing."); reasonRef.current?.focus(); return; }
    setBusy("reject");
    try {
      await api.reject(work.workItemId, reason.trim());
      toast.success("Sent back for rework. The housekeeper has been notified.");
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not send the work back."));
    } finally {
      setBusy(null);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Verify work — ${work.workNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: reject, loading: busy === "reject", disabled: !!busy, children: "Send back for rework" }),
      e.jsx(Button, { onClick: verify, loading: busy === "verify", disabled: !!busy, children: "Verify and tick" })
    ] }),
    children: e.jsxs("div", { className: "mt-verify mt-modal--wide", children: [
      e.jsxs("div", { children: [e.jsx(PhotoProof, { work }), e.jsx("div", { className: "mt-sub", style: { marginTop: 6 }, children: work.hasPhoto ? `Uploaded by ${work.completedByName ?? work.assignedToName}` : "The photo is optional." })] }),
      e.jsxs("div", { style: { minWidth: 0 }, children: [
        e.jsx("div", { className: "mt-title", style: { fontSize: "var(--font-size-md)" }, children: work.title }),
        e.jsx("div", { className: "mt-sub", children: `${workSub(work)} · ${PRIORITY[work.priority]} priority` }),
        e.jsxs("div", { className: "mt-facts", children: [
          e.jsxs("div", { children: [e.jsx("small", { children: "Due" }), e.jsx("b", { children: dueLabel(work.dueUtc) })] }),
          e.jsxs("div", { children: [e.jsx("small", { children: "Completed at" }), e.jsx("b", { children: work.completedUtc ? dueLabel(work.completedUtc) : "—" })] }),
          e.jsxs("div", { children: [e.jsx("small", { children: "Time taken" }), e.jsx("b", { children: duration(minutesBetween(work.startedUtc, work.completedUtc)) })] })
        ] }),
        e.jsx("div", { className: "mt-sub", style: { marginBottom: 4 }, children: `Completion note${work.completedByName ? ` — ${work.completedByName}` : ""}` }),
        e.jsx("div", { className: "mt-quote", children: work.completionNote || "—" }),
        work.reworkCount > 0 && e.jsx("div", { className: "mt-sub", style: { marginTop: 6 }, children: `Sent back ${work.reworkCount} time${work.reworkCount === 1 ? "" : "s"} before.` }),
        e.jsxs("div", { className: "field", style: { marginTop: "var(--space-4)" }, children: [
          e.jsx("label", { className: "field__label", htmlFor: "mt-reject-reason", children: "Reason (required when sending back for rework)" }),
          e.jsx("input", { id: "mt-reject-reason", ref: reasonRef, className: "field__input", value: reason, placeholder: "e.g. Glass still has marks near the handle", onChange: (ev) => setReason(ev.target.value) })
        ] })
      ] })
    ] })
  });
}

function WorkDetailModal({ workId, onClose, onChanged, onEdit }) {
  const toast = useToast();
  const [work, setWork] = React.useState(null);
  const [marking, setMarking] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const load = React.useCallback(() => api.item(workId).then(setWork).catch((error) => { toast.error(errorText(error, "Could not open the work.")); onClose(); }), [workId]);
  React.useEffect(() => { load(); }, [load]);
  const start = async () => {
    setBusy(true);
    try {
      setWork(await api.start(workId));
      toast.success("Task started.");
      onChanged();
    } catch (error) {
      toast.error(errorText(error, "Could not start the task."));
    } finally {
      setBusy(false);
    }
  };
  if (!work) return e.jsx(Modal, { open: true, title: "Work", onClose, children: e.jsx(Spinner, { center: true }) });
  // The sheet replaces this dialog while it is open: stacked, the dialog's overlay
  // (z-index 100) covered the sheet and swallowed every click on it.
  if (marking) return e.jsx(MarkDoneSheet, { work, onClose: () => setMarking(false), onDone: () => { setMarking(false); load(); onChanged(); } });
  const facts = [
    ["Status", e.jsx(StatusPill, { status: work.status })],
    ["Priority", e.jsx(PriorityChip, { priority: work.priority })],
    ["Assigned to", work.assignedToName],
    ["Due", dueLabel(work.dueUtc)],
    work.startedUtc && ["Started", fmtDateTime(work.startedUtc)],
    work.completedUtc && ["Completed", `${fmtDateTime(work.completedUtc)}${work.completedByName ? ` · ${work.completedByName}` : ""}`],
    work.verifiedUtc && ["Verified", `${fmtDateTime(work.verifiedUtc)} · ${work.verifiedByName ?? ""}`],
    work.reworkCount > 0 && ["Rework count", String(work.reworkCount)],
    work.linkedScheduleNumber && ["Linked schedule", work.linkedScheduleNumber],
    work.cancelReason && ["Cancel reason", work.cancelReason]
  ].filter(Boolean);
  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(Modal, {
        open: true,
        title: `${work.workNumber} — ${work.title}`,
        onClose,
        footer: e.jsxs(e.Fragment, { children: [
          e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Close" }),
          work.canEdit && e.jsx(Button, { variant: "outline", onClick: () => onEdit(work), children: "Edit" }),
          work.canStart && e.jsx(Button, { variant: "outline", onClick: start, loading: busy, children: work.status === 5 ? "Start again" : "Start task" }),
          work.canMarkDone && e.jsx(Button, { onClick: () => setMarking(true), children: "Mark done" })
        ] }),
        children: e.jsxs("div", { className: "mt-form mt-modal", children: [
          e.jsx("div", { className: "mt-sub", style: { fontSize: "var(--font-size-sm)", marginTop: 0 }, children: `${work.areaName} · ${work.category}` }),
          work.status === 5 && work.reworkReason && e.jsx("div", { className: "mt-quote mt-quote--rework", children: `Sent back: ${work.reworkReason}` }),
          e.jsx("dl", { className: "mt-kv", style: { margin: 0 }, children: facts.map(([k, v]) => e.jsxs(React.Fragment, { children: [e.jsx("dt", { children: k }), e.jsx("dd", { children: v })] }, k)) }),
          work.instructions && e.jsxs("div", { children: [e.jsx("div", { className: "mt-sub", style: { marginBottom: 4 }, children: "Instructions" }), e.jsx("div", { className: "mt-quote", children: work.instructions })] }),
          work.completionNote && e.jsxs("div", { children: [e.jsx("div", { className: "mt-sub", style: { marginBottom: 4 }, children: "Completion note" }), e.jsx("div", { className: "mt-quote", children: work.completionNote })] }),
          work.hasPhoto && e.jsx("div", { style: { maxWidth: 260 }, children: e.jsx(PhotoProof, { work }) })
        ] })
      }),
      null
    ]
  });
}

// ── Add / edit work ─────────────────────────────────────────────────────────

const defaultDue = () => {
  const d = new Date(Date.now() + 2 * 3600e3);
  d.setUTCMinutes(d.getUTCMinutes() < 30 ? 30 : 60, 0, 0); // IST is :30 off UTC, so this lands on the IST hour
  return toIstInput(d.toISOString());
};

function WorkFormModal({ work, options, onClose, onSaved }) {
  const toast = useToast();
  const [form, setForm] = React.useState(() => ({
    title: work?.title ?? "",
    areaName: work?.areaName ?? "",
    category: work?.category ?? "",
    priority: work ? String(work.priority) : "2",
    assignedToUserId: work ? String(work.assignedToUserId) : "",
    due: work ? toIstInput(work.dueUtc) : defaultDue(),
    instructions: work?.instructions ?? "",
    blockWorkspace: false,
    notify: true
  }));
  const [busy, setBusy] = React.useState(false);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const areaOption = options.areas.find((a) => a.name === form.areaName);
  const canBlock = !work && !!areaOption?.workspaceId;
  const covers = (h) => !!form.areaName && (h.assignedAreas ?? "").split(",").map((a) => a.trim()).includes(form.areaName);
  const people = [...options.housekeepers].sort((a, b) => Number(covers(b)) - Number(covers(a)));
  if (work && !people.some((h) => h.userId === work.assignedToUserId)) people.push({ userId: work.assignedToUserId, fullName: work.assignedToName, shift: null });

  const save = async () => {
    if (!form.title.trim()) { toast.error("Enter a title for the work."); return; }
    if (!form.areaName || !form.category || !form.priority) { toast.error("Select an area, a category and a priority."); return; }
    if (!form.assignedToUserId) { toast.error("Select an active housekeeper."); return; }
    if (!form.due) { toast.error("Choose the due date and time."); return; }
    const dueUtc = fromIstInput(form.due);
    if ((!work || dueUtc !== work.dueUtc) && new Date(dueUtc) < new Date(Date.now() - 60e3)) { toast.error("Due time cannot be in the past."); return; }
    const body = {
      title: form.title.trim(),
      areaName: form.areaName,
      workspaceId: areaOption?.workspaceId ?? null,
      category: form.category,
      priority: Number(form.priority),
      assignedToUserId: Number(form.assignedToUserId),
      dueUtc,
      instructions: form.instructions.trim() || null,
      blockWorkspace: canBlock && form.blockWorkspace,
      blockFromUtc: null,
      notifyHousekeeper: form.notify
    };
    setBusy(true);
    try {
      const saved = work ? await api.update(work.workItemId, body) : await api.create(body);
      toast.success(`${work ? `${saved.workNumber} updated` : `${saved.workNumber} added${form.notify ? " and the housekeeper notified" : ""}`} — due ${dueLabel(saved.dueUtc).replace(/^(Today|Yesterday|Tomorrow)/, (m) => m.toLowerCase())}.`);
      onSaved(saved);
    } catch (error) {
      toast.error(errorText(error, "Could not save the work."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: work ? `Edit work — ${work.workNumber}` : "Add new work",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(Button, { onClick: save, loading: busy, children: work ? "Save changes" : "Save and assign" })] }),
    children: e.jsxs("div", { className: "mt-form mt-modal", children: [
      e.jsx(Input, { label: "Title", required: true, value: form.title, placeholder: "e.g. Clean lobby glass doors", maxLength: 200, onChange: (ev) => set({ title: ev.target.value }) }),
      e.jsxs("div", { className: "mt-grid", children: [
        e.jsx(Select, { label: "Area / room", required: true, value: form.areaName, placeholder: "Select area", options: options.areas.map((a) => ({ value: a.name, label: a.workspaceId ? `${a.name} (workspace)` : a.name })), onChange: (ev) => set({ areaName: ev.target.value, blockWorkspace: false }) }),
        e.jsx(Select, { label: "Category", required: true, value: form.category, placeholder: "Select category", options: options.categories.map((c) => ({ value: c, label: c })), onChange: (ev) => set({ category: ev.target.value }) }),
        e.jsx(Select, { label: "Priority", required: true, value: form.priority, options: [3, 2, 1].map((p) => ({ value: String(p), label: PRIORITY[p] })), onChange: (ev) => set({ priority: ev.target.value }) }),
        e.jsx(Select, {
          label: "Assign to",
          required: true,
          value: form.assignedToUserId,
          placeholder: people.length ? "Select housekeeper" : "No active housekeeper yet",
          options: people.map((h) => ({ value: String(h.userId), label: `${h.fullName}${h.shift ? ` (${SHIFT[h.shift]})` : ""}${covers(h) ? " · covers this area" : ""}` })),
          hint: people.length ? "Only active housekeepers appear here" : "Add a housekeeper on the Housekeepers tab first.",
          onChange: (ev) => set({ assignedToUserId: ev.target.value })
        }),
        e.jsx(RgDate, { label: "Due date & time (IST)", required: true, value: form.due, onChange: (v) => set({ due: v }) }),
        e.jsx(Select, { label: "Repeat", value: "none", disabled: true, options: [{ value: "none", label: "Does not repeat" }], hint: "Phase 2 — recurring tasks", onChange: () => {} })
      ] }),
      e.jsx(TextArea, { label: "Instructions for the housekeeper", value: form.instructions, placeholder: "Optional: what exactly to do, materials to use", onChange: (v) => set({ instructions: v }) }),
      !work && e.jsxs("label", {
        className: `mt-block${canBlock ? "" : " mt-block--off"}`,
        children: [
          e.jsx("input", { type: "checkbox", disabled: !canBlock, checked: canBlock && form.blockWorkspace, onChange: (ev) => set({ blockWorkspace: ev.target.checked }) }),
          e.jsxs("span", { children: ["Block this workspace during the task ", e.jsx("small", { children: canBlock ? "— creates a schedule (from one hour before the due time) and removes the slot from customer availability" : "— pick a bookable workspace as the area to block it" })] })
        ]
      }),
      !work && e.jsxs("label", { className: "mt-check", children: [e.jsx("input", { type: "checkbox", checked: form.notify, onChange: (ev) => set({ notify: ev.target.checked }) }), "Notify the housekeeper now (in-app notification)"] })
    ] })
  });
}

// ── Schedules (the previous Maintenance page) ───────────────────────────────

const SCHEDULE_CLS = { 1: "scheduled", 2: "progress", 3: "verified", 4: "cancelled", 5: "overdue" };

function SchedulesTab({ options, reloadKey, canCreate, canEdit, onNew }) {
  const toast = useToast();
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [workspace, setWorkspace] = React.useState("");
  const [date, setDate] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [editing, setEditing] = React.useState(null);
  const [completing, setCompleting] = React.useState(null);
  const [cancelling, setCancelling] = React.useState(null);
  const debounced = useDebounced(search);
  const workspaces = options.areas.filter((a) => a.workspaceId);

  React.useEffect(() => { setPage(1); }, [debounced, status, workspace, date]);
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setData(await schedulesApi.list({
        page, pageSize: 10,
        search: debounced.trim() || undefined,
        status: status ? Number(status) : undefined,
        workspaceId: workspace ? Number(workspace) : undefined,
        from: date || undefined,
        to: date || undefined
      }));
    } catch (error) {
      toast.error(errorText(error, "Failed to load schedules."));
    } finally {
      setLoading(false);
    }
  }, [page, debounced, status, workspace, date, reloadKey]);
  React.useEffect(() => { load(); }, [load]);

  const start = async (s) => {
    try {
      await schedulesApi.checkIn(s.housekeepingScheduleId);
      toast.success("Task started.");
      load();
    } catch (error) {
      toast.error(errorText(error, "Could not start the task."));
    }
  };
  const rows = data?.items ?? [];
  const actions = (s) => canEdit && e.jsxs("div", { className: "mt-actions", children: [
    s.status === 1 && e.jsx(Button, { size: "sm", variant: "outline", onClick: () => start(s), children: "Start" }),
    s.status === 2 && e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setCompleting(s), children: "Complete" }),
    e.jsx(RowMenu, { label: `More actions for ${s.scheduleNumber}`, items: [
      s.status === 1 && { label: "Edit", onClick: () => setEditing(s) },
      (s.status === 1 || s.status === 2) && { label: "Cancel schedule", onClick: () => setCancelling(s), danger: true }
    ] })
  ] });

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx("div", { className: "mt-notebar", children: "A schedule that blocks a workspace removes that slot from customer availability, and the staff QR opens the door only inside the window. Housekeeping schedules also add a task to the checklist." }),
      e.jsxs("div", { className: "mt-toolbar mt-toolbar--sch", children: [
        e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Search schedule ID, task, staff…" }),
        e.jsx(Select, { label: "Status", value: status, placeholder: "All statuses", options: Object.entries(SCHEDULE_STATUS).map(([v, l]) => ({ value: v, label: l })), onChange: (ev) => setStatus(ev.target.value) }),
        e.jsx(Select, { label: "Workspace", value: workspace, placeholder: "All workspaces", options: workspaces.map((w) => ({ value: String(w.workspaceId), label: w.name })), onChange: (ev) => setWorkspace(ev.target.value) }),
        e.jsx(Input, { label: "Date", type: "date", value: date, onChange: (ev) => setDate(ev.target.value) })
      ] }),
      loading && !data
        ? e.jsx(Spinner, { center: true })
        : rows.length === 0
          ? e.jsx(EmptyState, { title: "No schedules match", description: canCreate ? "Use + New schedule to block a workspace for cleaning or repair." : "Schedules will appear here." })
          : e.jsxs("div", { style: { opacity: loading ? 0.6 : 1 }, children: [
              e.jsx("div", { className: "mt-table-wrap", children: e.jsxs("table", { className: "mt-table", children: [
                e.jsx("thead", { children: e.jsxs("tr", { children: [
                  e.jsx("th", { children: "Schedule" }), e.jsx("th", { children: "Staff" }), e.jsx("th", { children: "Where" }), e.jsx("th", { children: "Window (IST)" }), e.jsx("th", { children: "Status" }), e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Actions" }) })
                ] }) }),
                e.jsx("tbody", { children: rows.map((s) => e.jsxs("tr", { children: [
                  e.jsxs("td", { children: [e.jsx("div", { className: "mt-title", children: s.scheduleNumber }), e.jsx("div", { className: "mt-sub", children: `${TASK_TYPE[s.taskType]} · ${s.taskDescription}` })] }),
                  e.jsx("td", { children: e.jsx(Who, { name: s.staffName }) }),
                  e.jsx("td", { children: s.workspaceName ?? s.areaName ?? "—" }),
                  e.jsx("td", { style: { whiteSpace: "nowrap" }, children: `${dueLabel(s.startUtc)} – ${istTime(s.endUtc)}` }),
                  e.jsx("td", { children: e.jsxs("div", { style: { display: "flex", gap: 6, flexWrap: "wrap" }, children: [
                    e.jsx("span", { className: `mt-pill mt-pill--${SCHEDULE_CLS[s.status]}`, children: SCHEDULE_STATUS[s.status] }),
                    s.blocksWorkspace && s.status === 1 && e.jsx("span", { className: "mt-pill mt-pill--overdue", children: "Blocks calendar" })
                  ] }) }),
                  e.jsx("td", { children: actions(s) })
                ] }, s.housekeepingScheduleId)) })
              ] }) }),
              e.jsx("div", { className: "mt-mcards", children: rows.map((s) => e.jsxs("div", { className: "mt-mcard", children: [
                e.jsxs("div", { className: "mt-mcard__top", children: [
                  e.jsxs("div", { style: { minWidth: 0 }, children: [e.jsx("div", { className: "mt-title", children: s.scheduleNumber }), e.jsx("div", { className: "mt-sub", children: `${TASK_TYPE[s.taskType]} · ${s.taskDescription}` })] }),
                  e.jsx("span", { className: `mt-pill mt-pill--${SCHEDULE_CLS[s.status]}`, children: SCHEDULE_STATUS[s.status] })
                ] }),
                e.jsxs("div", { className: "mt-mcard__meta", children: [e.jsx("span", { children: s.staffName }), e.jsx("span", { children: s.workspaceName ?? s.areaName ?? "—" }), e.jsx("span", { children: `${dueLabel(s.startUtc)} – ${istTime(s.endUtc)}` })] }),
                actions(s)
              ] }, s.housekeepingScheduleId)) }),
              e.jsx(Pager, { result: data, onPage: setPage })
            ] }),
      editing && e.jsx(ScheduleFormModal, { schedule: editing, onClose: () => setEditing(null), onSaved: () => { setEditing(null); load(); } }),
      completing && e.jsx(ScheduleCompleteModal, { schedule: completing, onClose: () => setCompleting(null), onDone: () => { setCompleting(null); load(); } }),
      e.jsx(ConfirmDialog, {
        open: cancelling !== null,
        danger: true,
        confirmLabel: "Cancel schedule",
        title: `Cancel ${cancelling?.scheduleNumber ?? ""}?`,
        message: "The calendar block is released, the staff access QR is revoked, and a linked checklist task is cancelled.",
        onConfirm: async () => {
          if (!cancelling) return;
          try {
            await schedulesApi.cancel(cancelling.housekeepingScheduleId);
            toast.success("Schedule cancelled and the workspace released.");
            load();
          } catch (error) {
            toast.error(errorText(error, "Could not cancel."));
          }
        },
        onClose: () => setCancelling(null)
      })
    ]
  });
}

// ── Housekeepers ────────────────────────────────────────────────────────────

function HousekeepersTab({ me, options, reloadKey, onChanged, onEdit }) {
  const toast = useToast();
  const [search, setSearch] = React.useState("");
  const [shift, setShift] = React.useState("");
  const [status, setStatus] = React.useState("active");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [modal, setModal] = React.useState(null);
  const debounced = useDebounced(search);
  const admin = me.canManageHousekeepers;

  React.useEffect(() => { setPage(1); }, [debounced, shift, status]);
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setData(await api.housekeepers({ page, pageSize: 10, search: debounced.trim() || undefined, shift: shift ? Number(shift) : undefined, status }));
    } catch (error) {
      toast.error(errorText(error, "Could not load housekeepers."));
    } finally {
      setLoading(false);
    }
  }, [page, debounced, shift, status, reloadKey]);
  React.useEffect(() => { load(); }, [load]);
  const changed = () => { setModal(null); load(); onChanged(); };

  const activate = async (h) => {
    try {
      await api.activate(h.userId);
      toast.success(`${h.fullName} is active again.`);
      changed();
    } catch (error) {
      toast.error(errorText(error, "Could not reactivate."));
    }
  };
  const today = (h) => {
    if (!h.isActive && h.todayDone + h.todayPending + h.todayRework === 0) return "—";
    const parts = [`${h.todayDone} done`, `${h.todayPending} pending`];
    if (h.todayRework) parts.push(`${h.todayRework} rework`);
    return parts.join(" / ");
  };
  const rate = (h) => e.jsxs("div", { className: "mt-rate", title: h.verifiedCount ? `${h.onTimeCount} of ${h.verifiedCount} verified tasks were on time` : "No verified tasks yet", children: [
    e.jsx("div", { className: "mt-bar", children: e.jsx("div", { className: "mt-bar__verified", style: { width: `${h.onTimeRate ?? 0}%` } }) }),
    e.jsx("b", { children: h.onTimeRate == null ? "—" : `${h.onTimeRate}%` })
  ] });
  const actions = (h) => e.jsxs("div", { className: "mt-actions", children: [
    e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setModal({ kind: "log", hk: h }), children: "Work log" }),
    admin && e.jsx(RowMenu, { label: `More actions for ${h.fullName}`, items: [
      { label: "Edit", onClick: () => onEdit(h, load) },
      h.isActive && h.openTasks > 0 && { label: "Reassign open tasks", onClick: () => setModal({ kind: "reassign", hk: h }) },
      h.isActive ? { label: "Deactivate", onClick: () => setModal({ kind: "deactivate", hk: h }), danger: true } : { label: "Reactivate", onClick: () => activate(h) }
    ] })
  ] });
  const who = (h) => e.jsxs("div", { className: "mt-who", style: { whiteSpace: "normal" }, children: [
    e.jsx("span", { className: "mt-avatar", "aria-hidden": true, children: initials(h.fullName) }),
    e.jsxs("div", { children: [e.jsx("div", { className: "mt-title", children: h.fullName }), e.jsx("div", { className: "mt-sub", children: `${h.housekeeperCode} · ${h.mobileNumber ? `+91 ${h.mobileNumber}` : "no phone"}` })] })
  ] });
  const pill = (h) => e.jsx("span", { className: `mt-pill mt-pill--${h.isActive ? "active" : "inactive"}`, children: h.isActive ? "Active" : "Inactive" });
  const rows = data?.items ?? [];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("div", { className: "mt-toolbar mt-toolbar--hk", children: [
        e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Search name, phone or ID…" }),
        e.jsx(Select, { label: "Shift", value: shift, placeholder: "All shifts", options: [1, 2, 3].map((s) => ({ value: String(s), label: SHIFT[s] })), onChange: (ev) => setShift(ev.target.value) }),
        e.jsx(Select, { label: "Status", value: status, options: [{ value: "active", label: "Active" }, { value: "inactive", label: "Inactive" }, { value: "all", label: "Active and inactive" }], onChange: (ev) => setStatus(ev.target.value) })
      ] }),
      loading && !data
        ? e.jsx(Spinner, { center: true })
        : rows.length === 0
          ? e.jsx(EmptyState, { title: status === "inactive" ? "No inactive housekeepers" : "No housekeepers yet", description: admin ? "Use + Add housekeeper to create one. They get a login for My tasks." : "An admin adds housekeepers." })
          : e.jsxs("div", { style: { opacity: loading ? 0.6 : 1 }, children: [
              e.jsx("div", { className: "mt-table-wrap", children: e.jsxs("table", { className: "mt-table", children: [
                e.jsx("thead", { children: e.jsxs("tr", { children: [
                  e.jsx("th", { children: "Housekeeper" }), e.jsx("th", { children: "Shift" }), e.jsx("th", { children: "Assigned areas" }), e.jsx("th", { children: "Today" }), e.jsx("th", { children: "On-time rate" }), e.jsx("th", { children: "Status" }), e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Actions" }) })
                ] }) }),
                e.jsx("tbody", { children: rows.map((h) => e.jsxs("tr", { children: [
                  e.jsx("td", { children: who(h) }),
                  e.jsx("td", { children: SHIFT[h.shift] }),
                  e.jsx("td", { children: h.assignedAreas || "—" }),
                  e.jsx("td", { style: { whiteSpace: "nowrap" }, children: today(h) }),
                  e.jsx("td", { children: rate(h) }),
                  e.jsx("td", { children: pill(h) }),
                  e.jsx("td", { children: actions(h) })
                ] }, h.userId)) })
              ] }) }),
              e.jsx("div", { className: "mt-mcards", children: rows.map((h) => e.jsxs("div", { className: "mt-mcard", children: [
                e.jsxs("div", { className: "mt-mcard__top", children: [who(h), pill(h)] }),
                e.jsxs("div", { className: "mt-mcard__meta", children: [e.jsx("span", { children: SHIFT[h.shift] }), e.jsx("span", { children: h.assignedAreas || "No areas" }), e.jsx("span", { children: `Today: ${today(h)}` })] }),
                rate(h),
                actions(h)
              ] }, h.userId)) }),
              e.jsx(Pager, { result: data, onPage: setPage })
            ] }),
      e.jsx("p", { className: "mt-note", children: "On-time rate = tasks verified before their due time ÷ tasks verified. A housekeeper cannot be deleted, only deactivated, so their work history stays." }),
      modal?.kind === "log" && e.jsx(WorkLogModal, { hk: modal.hk, onClose: () => setModal(null) }),
      modal?.kind === "deactivate" && e.jsx(DeactivateModal, { hk: modal.hk, housekeepers: options.housekeepers, onClose: () => setModal(null), onDone: changed }),
      modal?.kind === "reassign" && e.jsx(ReassignAllModal, { hk: modal.hk, housekeepers: options.housekeepers, onClose: () => setModal(null), onDone: changed })
    ]
  });
}

function HousekeeperFormModal({ hk, options, onClose, onSaved }) {
  const toast = useToast();
  const [form, setForm] = React.useState(() => ({
    fullName: hk?.fullName ?? "",
    mobileNumber: hk?.mobileNumber ?? "",
    email: hk?.email ?? "",
    shift: hk ? String(hk.shift) : "1",
    joiningDate: hk?.joiningDate ?? istDay(Date.now()),
    areas: hk?.assignedAreas ? hk.assignedAreas.split(",").map((a) => a.trim()).filter(Boolean) : [],
    isActive: hk ? hk.isActive : true
  }));
  const [busy, setBusy] = React.useState(false);
  const [created, setCreated] = React.useState(null);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const toggleArea = (name) => set({ areas: form.areas.includes(name) ? form.areas.filter((a) => a !== name) : [...form.areas, name] });
  const areaNames = [...new Set([...options.areas.map((a) => a.name), ...form.areas])];

  const save = async () => {
    const mobile = form.mobileNumber.replace(/\D/g, "").replace(/^91(?=\d{10}$)/, "");
    if (!form.fullName.trim()) { toast.error("Enter the housekeeper's full name."); return; }
    if (!/^[6-9]\d{9}$/.test(mobile)) { toast.error("Enter a 10-digit mobile number."); return; }
    if (!/^\S+@\S+\.\S+$/.test(form.email.trim())) { toast.error("Enter an email — it is the housekeeper's sign-in for My tasks."); return; }
    if (!form.joiningDate) { toast.error("Choose the joining date."); return; }
    const body = { fullName: form.fullName.trim(), mobileNumber: mobile, email: form.email.trim(), shift: Number(form.shift), joiningDate: form.joiningDate, assignedAreas: form.areas, isActive: form.isActive };
    setBusy(true);
    try {
      if (hk) {
        await api.updateHousekeeper(hk.userId, body);
        toast.success("Housekeeper updated.");
        onSaved();
      } else {
        const result = await api.createHousekeeper(body);
        setCreated(result);
      }
    } catch (error) {
      toast.error(errorText(error, "Could not save the housekeeper."));
    } finally {
      setBusy(false);
    }
  };

  if (created) {
    const copy = () => navigator.clipboard?.writeText(`Regalis sign-in\nEmail: ${created.housekeeper.email}\nTemporary password: ${created.temporaryPassword}`).then(() => toast.success("Copied."), () => {});
    return e.jsx(Modal, {
      open: true,
      title: `${created.housekeeper.fullName} added — ${created.housekeeper.housekeeperCode}`,
      onClose: onSaved,
      footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "outline", onClick: copy, children: "Copy sign-in details" }), e.jsx(Button, { onClick: onSaved, children: "Done" })] }),
      children: e.jsxs("div", { className: "mt-form mt-modal", children: [
        e.jsx("p", { style: { margin: 0 }, children: "Hand these sign-in details to the housekeeper. They sign in on the Admin tab, are asked to set their own password, and land on My tasks." }),
        e.jsxs("dl", { className: "mt-kv", style: { margin: 0 }, children: [e.jsx("dt", { children: "Email" }), e.jsx("dd", { children: created.housekeeper.email })] }),
        e.jsxs("div", { className: "mt-secret", children: [e.jsx("span", { children: created.temporaryPassword }), e.jsx("small", { style: { fontFamily: "var(--font-body)", opacity: 0.7 }, children: "temporary password" })] }),
        e.jsx("p", { className: "mt-sub", style: { margin: 0, fontSize: "var(--font-size-sm)" }, children: "This password is shown only once. If it is lost, reset it from Users & Staff." })
      ] })
    });
  }

  return e.jsx(Modal, {
    open: true,
    title: hk ? `Edit housekeeper — ${hk.housekeeperCode}` : "Add housekeeper",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(Button, { onClick: save, loading: busy, children: hk ? "Save changes" : "Save housekeeper" })] }),
    children: e.jsxs("div", { className: "mt-form mt-modal", children: [
      e.jsxs("div", { className: "mt-grid", children: [
        e.jsx(Input, { label: "Full name", required: true, value: form.fullName, placeholder: "e.g. Priya S.", maxLength: 150, onChange: (ev) => set({ fullName: ev.target.value }) }),
        e.jsx(Input, { label: "Phone", required: true, value: form.mobileNumber, placeholder: "10-digit mobile number", inputMode: "numeric", maxLength: 14, hint: "Must be unique — a repeated number is rejected", onChange: (ev) => set({ mobileNumber: ev.target.value }) }),
        e.jsx(Input, { label: "Email (sign-in)", required: true, type: "email", value: form.email, placeholder: "name@example.com", hint: hk ? "Changing it changes their sign-in" : "They sign in with this to see My tasks", onChange: (ev) => set({ email: ev.target.value }) }),
        e.jsx(Select, { label: "Shift", required: true, value: form.shift, options: [1, 2, 3].map((s) => ({ value: String(s), label: SHIFT[s] })), onChange: (ev) => set({ shift: ev.target.value }) }),
        e.jsx(Input, { label: "Joining date", required: true, type: "date", value: form.joiningDate, onChange: (ev) => set({ joiningDate: ev.target.value }) }),
        !hk && e.jsx(Select, { label: "Status", required: true, value: form.isActive ? "1" : "0", options: [{ value: "1", label: "Active" }, { value: "0", label: "Inactive" }], onChange: (ev) => set({ isActive: ev.target.value === "1" }) })
      ] }),
      e.jsxs("div", { className: "field", children: [
        e.jsx("span", { className: "field__label", id: "mt-areas-label", children: "Assigned areas" }),
        e.jsx("div", { className: "mt-areas", role: "group", "aria-labelledby": "mt-areas-label", children: areaNames.map((name) => e.jsx("button", { type: "button", "aria-pressed": form.areas.includes(name), onClick: () => toggleArea(name), children: name }, name)) }),
        e.jsx("span", { className: "field__hint", children: "Used to suggest the right person when assigning work" })
      ] })
    ] })
  });
}

function DeactivateModal({ hk, housekeepers, onClose, onDone }) {
  const toast = useToast();
  const [to, setTo] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const others = housekeepers.filter((h) => h.userId !== hk.userId);
  const submit = async () => {
    if (hk.openTasks > 0 && !to) { toast.error(`Choose who takes ${hk.fullName}'s open tasks.`); return; }
    setBusy(true);
    try {
      await api.deactivate(hk.userId, to ? Number(to) : null);
      toast.success(`${hk.fullName} deactivated.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not deactivate."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Deactivate ${hk.fullName}?`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, children: "Deactivate" })] }),
    children: e.jsxs("div", { className: "mt-form mt-modal", children: [
      e.jsx("p", { style: { margin: 0 }, children: "They can no longer sign in or be assigned work. Their work history stays, and they can be reactivated later." }),
      hk.openTasks > 0
        ? e.jsx(Select, { label: `Reassign ${hk.openTasks} open task${hk.openTasks === 1 ? "" : "s"} to`, required: true, value: to, placeholder: others.length ? "Select a housekeeper" : "No other active housekeeper", options: others.map((h) => ({ value: String(h.userId), label: `${h.fullName} (${SHIFT[h.shift]})` })), onChange: (ev) => setTo(ev.target.value) })
        : e.jsx("p", { className: "mt-sub", style: { margin: 0 }, children: "No open tasks to reassign." })
    ] })
  });
}

/** "Reassign open tasks" without deactivating: moves each open task in turn. */
function ReassignAllModal({ hk, housekeepers, onClose, onDone }) {
  const toast = useToast();
  const [to, setTo] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const others = housekeepers.filter((h) => h.userId !== hk.userId);
  const submit = async () => {
    if (!to) { toast.error("Select an active housekeeper."); return; }
    setBusy(true);
    try {
      const page = await api.work({ all: true, assigneeId: hk.userId, pageSize: 100 });
      const open = page.work.items.filter((w) => OPEN.includes(w.status));
      for (const w of open) await api.assign(w.workItemId, Number(to));
      toast.success(`${open.length} task${open.length === 1 ? "" : "s"} reassigned.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not reassign every task."));
      onDone();
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Reassign ${hk.fullName}'s open tasks`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, children: "Reassign" })] }),
    children: e.jsx("div", { className: "mt-form mt-modal", children: e.jsx(Select, { label: `Move ${hk.openTasks} open task${hk.openTasks === 1 ? "" : "s"} to`, required: true, value: to, placeholder: "Select a housekeeper", options: others.map((h) => ({ value: String(h.userId), label: `${h.fullName} (${SHIFT[h.shift]})` })), onChange: (ev) => setTo(ev.target.value) }) })
  });
}

function WorkLogModal({ hk, onClose }) {
  const toast = useToast();
  const [log, setLog] = React.useState(null);
  React.useEffect(() => { api.workLog(hk.userId).then(setLog).catch((error) => { toast.error(errorText(error, "Could not load the work log.")); onClose(); }); }, [hk.userId]);
  const stat = (label, value) => e.jsxs("div", { className: "mt-stat", children: [e.jsx("small", { children: label }), e.jsx("b", { children: value })] });
  return e.jsx(Modal, {
    open: true,
    title: `Work log — ${hk.fullName}`,
    onClose,
    footer: e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Close" }),
    children: !log
      ? e.jsx(Spinner, { center: true })
      : e.jsxs("div", { className: "mt-form mt-modal--wide", children: [
          e.jsx("div", { className: "mt-sub", style: { marginTop: 0, fontSize: "var(--font-size-sm)" }, children: `${log.housekeeper.housekeeperCode} · ${SHIFT[log.housekeeper.shift]} shift · ${log.housekeeper.assignedAreas || "no areas"} · joined ${dateLabel(log.housekeeper.joiningDate)}` }),
          e.jsxs("div", { className: "mt-stats", children: [
            stat("Tasks assigned", log.assigned), stat("Verified", log.completed), stat("Pending", log.pending),
            stat("Overdue", log.overdue), stat("Rework (times)", log.rework), stat("Average time", log.averageMinutes == null ? "—" : duration(log.averageMinutes))
          ] }),
          e.jsx("div", { className: "mt-title", children: "Recent tasks" }),
          log.recent.length === 0
            ? e.jsx("p", { className: "mt-sub", children: "No tasks yet." })
            : e.jsx("div", { className: "mt-list", children: log.recent.map((w) => e.jsxs("div", { children: [
                e.jsxs("div", { style: { minWidth: 0 }, children: [e.jsx("div", { style: { fontWeight: 600 }, children: w.title }), e.jsx("div", { className: "mt-sub", children: `${workSub(w)} · due ${dueLabel(w.dueUtc)}` })] }),
                e.jsx(StatusPill, { status: w.status })
              ] }, w.workItemId)) })
        ] })
  });
}

// ── Housekeeper: My tasks (phone view) ──────────────────────────────────────

function MarkDoneSheet({ work, onClose, onDone }) {
  const toast = useToast();
  const [note, setNote] = React.useState("");
  const [photo, setPhoto] = React.useState(null);
  const [preview, setPreview] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => {
    const key = (event) => event.key === "Escape" && !busy && onClose();
    document.addEventListener("keydown", key);
    return () => document.removeEventListener("keydown", key);
  }, [busy]);
  React.useEffect(() => () => preview && URL.revokeObjectURL(preview), [preview]);
  const pick = (event) => {
    const file = event.target.files?.[0];
    event.target.value = "";
    if (!file) return;
    if (!/^image\/(jpeg|png|webp)$/.test(file.type)) { toast.error("Upload a photo (.jpg, .png or .webp)."); return; }
    if (file.size > 10 * 1024 * 1024) { toast.error("The photo must be under 10 MB."); return; }
    setPhoto(file);
    setPreview(URL.createObjectURL(file));
  };
  const submit = async () => {
    if (!note.trim()) { toast.error("Add a short note about what you did."); return; }
    setBusy(true);
    try {
      await api.done(work.workItemId, note.trim(), photo);
      toast.success("Sent for verification.");
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not mark the task done."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx("div", {
    className: "mt-sheet-backdrop",
    onMouseDown: (event) => event.target === event.currentTarget && !busy && onClose(),
    children: e.jsxs("div", { className: "mt-sheet", role: "dialog", "aria-modal": true, "aria-labelledby": "mt-sheet-title", children: [
      e.jsxs("div", { children: [e.jsx("h3", { id: "mt-sheet-title", children: "Mark this task done" }), e.jsx("div", { className: "mt-sub", style: { fontSize: "var(--font-size-sm)" }, children: `${work.title} · ${work.areaName}` })] }),
      e.jsx(TextArea, { label: "Completion note", required: true, value: note, autoFocus: true, placeholder: "What did you do? Anything the supervisor should know?", onChange: setNote }),
      e.jsxs("div", { className: "mt-shots", children: [
        e.jsxs("label", { className: "mt-shot", children: ["Take photo", e.jsx("input", { type: "file", accept: "image/*", capture: "environment", onChange: pick, "aria-label": "Take photo" })] }),
        e.jsxs("label", { className: "mt-shot", children: ["Upload from gallery", e.jsx("input", { type: "file", accept: "image/jpeg,image/png,image/webp", onChange: pick, "aria-label": "Upload from gallery" })] })
      ] }),
      preview && e.jsxs("div", { className: "mt-preview", children: [
        e.jsx("img", { src: preview, alt: "Selected photo" }),
        e.jsxs("div", { style: { flex: 1, minWidth: 0 }, children: [e.jsx("div", { style: { fontWeight: 600, overflowWrap: "anywhere" }, children: photo.name }), e.jsx("div", { className: "mt-sub", children: `${Math.max(1, Math.round(photo.size / 1024))} KB` })] }),
        e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => { setPhoto(null); setPreview(null); }, children: "Remove" })
      ] }),
      !photo && work.category === "Cleaning" && e.jsx("div", { className: "mt-sub", style: { marginTop: -8 }, children: "A photo is optional but recommended for cleaning tasks." }),
      e.jsxs("div", { className: "mt-task__btns", children: [
        e.jsx("button", { type: "button", className: "mt-bigbtn mt-bigbtn--ghost", style: { flex: "0 0 32%" }, onClick: onClose, disabled: busy, children: "Cancel" }),
        e.jsx("button", { type: "button", className: "mt-bigbtn", onClick: submit, disabled: busy, children: busy ? "Sending…" : "Submit for verification" })
      ] })
    ] })
  });
}

function MyTasks({ me }) {
  const toast = useToast();
  const [tab, setTab] = React.useState("todo");
  const [data, setData] = React.useState(null);
  const [marking, setMarking] = React.useState(null);
  const [busyId, setBusyId] = React.useState(null);
  const load = React.useCallback(async () => {
    try {
      setData(await api.myTasks(tab));
    } catch (error) {
      toast.error(errorText(error, "Could not load your tasks."));
    }
  }, [tab]);
  React.useEffect(() => { load(); }, [load]);
  // Keep the list fresh while the phone sits open between tasks.
  React.useEffect(() => { const t = setInterval(load, 60000); return () => clearInterval(t); }, [load]);

  const start = async (w) => {
    setBusyId(w.workItemId);
    try {
      await api.start(w.workItemId);
      toast.success("Task started.");
      await load();
    } catch (error) {
      toast.error(errorText(error, "Could not start the task."));
    } finally {
      setBusyId(null);
    }
  };
  const hk = data?.housekeeper ?? me.housekeeper;
  const name = hk?.fullName ?? me.fullName;
  const card = (w) => e.jsxs("div", { className: "mt-task", children: [
    e.jsxs("div", { className: "mt-task__top", children: [e.jsx("div", { className: "mt-task__title", children: w.title }), e.jsx(StatusPill, { status: w.status })] }),
    e.jsx("div", { className: "mt-sub", style: { fontSize: "var(--font-size-sm)", marginTop: -4 }, children: workSub(w) }),
    e.jsxs("div", { className: "mt-task__meta", children: [e.jsx(PriorityChip, { priority: w.priority }), e.jsx("span", { className: w.status === 6 ? "mt-due--late" : "", children: `Due ${dueLabel(w.dueUtc).replace(",", "").replace(/^(Today|Yesterday|Tomorrow)/, (m) => m.toLowerCase())}` })] }),
    w.instructions && e.jsx("div", { className: "mt-quote", children: w.instructions }),
    w.status === 5 && w.reworkReason && e.jsx("div", { className: "mt-quote mt-quote--rework", children: `Redo: ${w.reworkReason}` }),
    (w.canStart || w.canMarkDone) && e.jsxs("div", { className: "mt-task__btns", children: [
      w.canStart && e.jsx("button", { type: "button", className: "mt-bigbtn", disabled: busyId === w.workItemId, onClick: () => start(w), children: w.status === 5 ? "Start again" : "Start task" }),
      w.canMarkDone && e.jsxs(e.Fragment, { children: [
        e.jsx("button", { type: "button", className: "mt-bigbtn", onClick: () => setMarking(w), children: "Mark done" }),
        e.jsx("button", { type: "button", className: "mt-bigbtn mt-bigbtn--ghost", style: { flex: "0 0 38%" }, onClick: () => setMarking(w), children: "Add photo" })
      ] })
    ] }),
    w.status === 3 && e.jsx("div", { className: "mt-sub", children: "Waiting for a supervisor to verify." })
  ] }, w.workItemId);

  return e.jsxs("div", {
    className: "mt-phone",
    children: [
      e.jsxs("div", { className: "mt-phone__head", children: [
        e.jsxs("div", { className: "mt-phone__who", children: [
          e.jsxs("div", { children: [
            e.jsx("h2", { className: "mt-phone__title", children: "My tasks" }),
            e.jsx("div", { className: "mt-sub", style: { fontSize: "var(--font-size-sm)" }, children: [name, hk && `${SHIFT[hk.shift]} shift`, hk?.assignedAreas].filter(Boolean).join(" · ") })
          ] }),
          e.jsx("span", { className: "mt-avatar mt-avatar--lg", "aria-hidden": true, children: initials(name) })
        ] }),
        data && e.jsxs(e.Fragment, { children: [
          e.jsxs("div", { className: "mt-phone__line", children: [e.jsx("b", { children: `${data.todayCompleted} of ${data.todayTotal} done today` }), e.jsx("span", { children: `${data.percent}%` })] }),
          e.jsx("div", { className: "mt-bar", role: "progressbar", "aria-valuenow": data.percent, "aria-valuemin": 0, "aria-valuemax": 100, "aria-label": "Done today", children: e.jsx("div", { className: "mt-bar__verified", style: { width: `${data.percent}%` } }) })
        ] })
      ] }),
      e.jsx("div", { className: "mt-pills", role: "tablist", "aria-label": "My tasks", children: [["todo", "To do", data?.toDoCount], ["done", "Done", data?.doneCount], ["rework", "Rework", data?.reworkCount]].map(([key, label, count]) =>
        e.jsx("button", { type: "button", role: "tab", "aria-selected": tab === key, onClick: () => setTab(key), children: `${label}${count == null ? "" : ` ${count}`}` }, key)
      ) }),
      !data
        ? e.jsx(Spinner, { center: true })
        : data.tasks.length === 0
          ? e.jsx(EmptyState, { title: tab === "todo" ? "Nothing to do right now" : tab === "done" ? "Nothing finished this week yet" : "No rework — well done", description: tab === "todo" ? "New tasks appear here as soon as they are assigned to you." : undefined })
          : data.tasks.map(card),
      e.jsx("p", { className: "mt-note", style: { marginTop: 0 }, children: "Marking done asks for a short note and an optional photo, then a supervisor verifies it." + (tab === "done" ? " Done shows the last 7 days." : "") }),
      marking && e.jsx(MarkDoneSheet, { work: marking, onClose: () => setMarking(null), onDone: () => { setMarking(null); load(); } })
    ]
  });
}

// ── Page ────────────────────────────────────────────────────────────────────

const TABS = [
  { key: "checklist", label: "Checklist" },
  { key: "schedules", label: "Schedules" },
  { key: "housekeepers", label: "Housekeepers" }
];
const DESCRIPTIONS = {
  checklist: "Every task, who is doing it and how much is done. A schedule that blocks a workspace also removes the slot from customer availability.",
  schedules: "Workspace blocks for cleaning and repair. A blocking schedule removes the slot from customer availability.",
  housekeepers: "Housekeepers and their work record. Only active housekeepers can be assigned new work."
};

function Summary({ summary }) {
  if (!summary) return null;
  const today = summary.date === istDay(Date.now());
  const on = today ? "today" : `on ${dateLabel(summary.date)}`;
  const pctVerified = summary.total ? (summary.verified / summary.total) * 100 : 0;
  const pctWaiting = summary.total ? (summary.waitingForVerification / summary.total) * 100 : 0;
  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("div", { className: "mt-cards", children: [
        e.jsxs("div", { className: "mt-card", children: [e.jsx("div", { className: "mt-card__label", children: today ? "Tasks today" : `Tasks ${on}` }), e.jsx("div", { className: "mt-card__value", children: summary.total }), e.jsx("div", { className: "mt-card__sub", children: `Across ${summary.categories} categor${summary.categories === 1 ? "y" : "ies"}` })] }),
        e.jsxs("div", { className: "mt-card", children: [e.jsx("div", { className: "mt-card__label", children: "Completed" }), e.jsx("div", { className: "mt-card__value mt-card__value--ok", children: summary.completed }), e.jsx("div", { className: "mt-card__sub", children: `${summary.percent}% complete · ${summary.verified} verified` })] }),
        e.jsxs("div", { className: "mt-card", children: [e.jsx("div", { className: "mt-card__label", children: "Overdue" }), e.jsx("div", { className: "mt-card__value mt-card__value--late", children: summary.overdue }), e.jsx("div", { className: "mt-card__sub", children: "Past due time" })] }),
        e.jsxs("div", { className: "mt-card", children: [e.jsx("div", { className: "mt-card__label", children: "On duty now" }), e.jsx("div", { className: "mt-card__value", children: `${summary.onDutyNow} of ${summary.activeHousekeepers}` }), e.jsx("div", { className: "mt-card__sub", children: `${summary.currentShift} shift` })] })
      ] }),
      e.jsxs("div", { className: "mt-card mt-progress", children: [
        e.jsxs("div", { className: "mt-progress__body", children: [
          e.jsxs("div", { className: "mt-progress__head", children: [
            e.jsx("strong", { children: today ? "Today's checklist progress" : `Checklist progress ${on}` }),
            e.jsx("span", { children: `${summary.completed} of ${summary.total} tasks done · ${summary.verified} verified · ${summary.overdue} overdue · ${summary.waitingForVerification} waiting for verification` })
          ] }),
          e.jsxs("div", { className: "mt-bar", role: "progressbar", "aria-valuenow": summary.percent, "aria-valuemin": 0, "aria-valuemax": 100, "aria-label": "Checklist progress", children: [
            e.jsx("div", { className: "mt-bar__verified", style: { width: `${pctVerified}%` } }),
            e.jsx("div", { className: "mt-bar__waiting", style: { width: `${pctWaiting}%` } })
          ] }),
          e.jsxs("div", { className: "mt-legend", children: [
            e.jsxs("span", { children: [e.jsx("i", { style: { background: "#CC9A3D" } }), "Verified"] }),
            e.jsxs("span", { children: [e.jsx("i", { style: { background: "rgb(204 154 61 / 45%)" } }), "Done, waiting for verification"] })
          ] })
        ] }),
        e.jsx("div", { className: "mt-progress__pct", children: `${summary.percent}%` })
      ] })
    ]
  });
}

function ManagerView({ me, options, reloadOptions }) {
  const [tab, setTab] = React.useState("checklist");
  const [summary, setSummary] = React.useState(null);
  const [filters, setFilters] = React.useState({});
  const [reloadKey, setReloadKey] = React.useState(0);
  const [focus, setFocus] = React.useState(null);
  const [modal, setModal] = React.useState(null);
  const [exporting, setExporting] = React.useState(false);
  const toast = useToast();
  const bump = () => setReloadKey((k) => k + 1);
  const canSchedule = me.canManageWork;
  const tabs = TABS;

  const exportWork = async () => {
    setExporting(true);
    try {
      await api.exportWork(filters);
    } catch (error) {
      toast.error(errorText(error, "Could not export."));
    } finally {
      setExporting(false);
    }
  };
  const actions = tab === "checklist"
    ? [
        e.jsx(Button, { variant: "outline", onClick: exportWork, loading: exporting, children: "Export" }, "export"),
        canSchedule && e.jsx(Button, { variant: "outline", onClick: () => setModal({ kind: "schedule" }), children: "+ New schedule" }, "schedule"),
        me.canManageWork && e.jsx(Button, { onClick: () => setModal({ kind: "work" }), children: "+ Add work" }, "work")
      ]
    : tab === "schedules"
      ? [canSchedule && e.jsx(Button, { onClick: () => setModal({ kind: "schedule" }), children: "+ New schedule" }, "schedule")]
      : [me.canManageHousekeepers && e.jsx(Button, { onClick: () => setModal({ kind: "housekeeper" }), children: "+ Add housekeeper" }, "hk")];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("div", { className: "page-header", children: [
        e.jsxs("div", { children: [e.jsx("h2", { className: "page-header__title", children: "Maintenance" }), e.jsx("p", { className: "page-header__description", children: DESCRIPTIONS[tab] })] }),
        e.jsx("div", { className: "page-header__actions", children: actions.filter(Boolean) })
      ] }),
      tab === "checklist" ? e.jsx(Summary, { summary }) : e.jsx("div", { style: { height: "var(--space-5)" } }),
      e.jsxs("div", { className: "mt-panel", children: [
        e.jsx("div", { className: "mt-tabs", role: "tablist", "aria-label": "Maintenance", children: tabs.map((t) =>
          e.jsx("button", { type: "button", role: "tab", className: "mt-tab", "aria-selected": tab === t.key, onClick: () => setTab(t.key), children: t.label }, t.key)
        ) }),
        tab === "checklist" && e.jsx(ChecklistTab, { me, options, reloadKey, focus, onSummary: setSummary, onFilters: setFilters, onChanged: () => {}, onEdit: (w) => setModal({ kind: "work", work: w }) }),
        tab === "schedules" && e.jsx(SchedulesTab, { options, reloadKey, canCreate: canSchedule, canEdit: canSchedule }),
        tab === "housekeepers" && e.jsx(HousekeepersTab, { me, options, reloadKey, onChanged: reloadOptions, onEdit: (h) => setModal({ kind: "housekeeper", hk: h }) })
      ] }),
      modal?.kind === "work" && e.jsx(WorkFormModal, { work: modal.work, options, onClose: () => setModal(null), onSaved: (saved) => { setModal(null); setTab("checklist"); setFocus({ day: istDay(saved.dueUtc), at: Date.now() }); bump(); } }),
      modal?.kind === "schedule" && e.jsx(ScheduleFormModal, { schedule: null, onClose: () => setModal(null), onSaved: () => { setModal(null); bump(); } }),
      modal?.kind === "housekeeper" && e.jsx(HousekeeperFormModal, { hk: modal.hk, options, onClose: () => setModal(null), onSaved: () => { setModal(null); reloadOptions(); bump(); } })
    ]
  });
}

function AdminMaintenancePage() {
  useStyles();
  const toast = useToast();
  const [me, setMe] = React.useState(null);
  const [options, setOptions] = React.useState(null);
  const [failed, setFailed] = React.useState(false);
  const loadOptions = React.useCallback(() => api.options().then(setOptions), []);
  React.useEffect(() => {
    Promise.all([api.me(), api.options()])
      .then(([m, o]) => { setMe(m); setOptions(o); })
      .catch((error) => { setFailed(true); toast.error(errorText(error, "Could not open Maintenance.")); });
  }, []);
  if (failed) return e.jsx(EmptyState, { title: "Maintenance is unavailable", description: "Refresh the page, or ask an admin to check your access." });
  if (!me || !options) return e.jsx(Spinner, { center: true });
  if (me.isHousekeeper) return e.jsx(MyTasks, { me });
  return e.jsx(ManagerView, { me, options, reloadOptions: loadOptions });
}

export { AdminMaintenancePage };
