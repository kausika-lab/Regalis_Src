/**
 * Wi-Fi Access (19 Nov 2026, kit deploy/wifi) - Regalis_WorkSpace_WiFi_Feature_Specification v1.0.
 * Admin: /admin/wifi (Connected now), /admin/wifi/access, /admin/wifi/usage, /admin/wifi/settings.
 * Customer portal and app: /wifi (My Wi-Fi); /portal/wifi redirects there.
 *
 * Data: /api/admin/wifi/* and /api/portal/wifi (see deploy/wifi/README.md). Hand-maintained chunk:
 * the routes in index-DI5X4c9f.js import it by name; bump its revision in sw.js when it changes.
 * The QR code is drawn with the existing AssetQr-AS1.js encoder.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as X,
  p as Button,
  I as Input,
  F as Select,
  D as unwrap,
  E as http,
  u as useNavigate,
  Y as useParams,
  G as Spinner
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";

/* ---------------------------------------------------------------------------------------- */
/* helpers                                                                                     */
/* ---------------------------------------------------------------------------------------- */

function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  const flat = kids.flat(Infinity).filter((k) => k !== null && k !== void 0 && k !== false && k !== "");
  if (flat.length === 1) p.children = flat[0];
  else if (flat.length > 1) p.children = flat;
  return flat.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}
const F = React.Fragment;

const CSS = `
.wf-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:var(--space-3);margin-bottom:var(--space-4)}
.wf-card{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3) var(--space-4);box-shadow:var(--shadow-sm);min-width:0}
.wf-card__label{font-size:var(--font-size-sm);color:var(--text-secondary)}
.wf-card__value{font-size:1.8rem;font-weight:800;margin:2px 0;font-family:var(--font-display);line-height:1.2}
.wf-card__sub{font-size:var(--font-size-xs);color:var(--text-muted)}
.wf-card--purple .wf-card__value{color:var(--color-info)}
.wf-card--amber .wf-card__value{color:var(--color-warning)}
.wf-panel{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);padding:var(--space-4);box-shadow:var(--shadow-sm);min-width:0}
.wf-tabs{display:flex;gap:4px;border-bottom:1px solid var(--border-subtle);margin-bottom:var(--space-3);overflow-x:auto}
.wf-tab{all:unset;cursor:pointer;padding:9px 14px;font-weight:600;color:var(--text-secondary);border-bottom:2px solid transparent;white-space:nowrap;display:inline-flex;gap:6px;align-items:center}
.wf-tab--on{color:var(--regalis-gold-dark);border-bottom-color:var(--accent)}
.wf-count{background:rgb(var(--accent-rgb) / 16%);border-radius:999px;padding:0 8px;font-size:var(--font-size-xs);font-weight:700}
.wf-filters{display:grid;grid-template-columns:2fr repeat(3,1fr);gap:var(--space-3);align-items:end;margin-bottom:var(--space-3)}
@media (max-width:900px){.wf-filters{grid-template-columns:1fr 1fr}}
.wf-scroll{overflow-x:auto}
.wf-table{width:100%;border-collapse:collapse}
.wf-table th{text-align:left;font-size:var(--font-size-xs);text-transform:uppercase;letter-spacing:.05em;color:var(--text-secondary);padding:10px;background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.wf-table td{padding:12px 10px;border-bottom:1px solid var(--border-subtle);vertical-align:middle;font-size:var(--font-size-sm)}
.wf-table td.wf-right{text-align:right;white-space:nowrap}
.wf-table .badge{white-space:nowrap}
.wf-row--unknown td{background:rgb(217 119 6 / 8%)}
.wf-unknown{color:var(--color-warning);font-weight:700}
.wf-who{display:flex;gap:10px;align-items:center;min-width:180px}
.wf-avatar{width:34px;height:34px;border-radius:50%;display:grid;place-items:center;background:rgb(var(--accent-rgb) / 18%);font-weight:800;font-size:12px;flex:none}
.wf-name{font-weight:700}
.wf-muted{color:var(--text-muted);font-size:var(--font-size-xs)}
.wf-mono{font-family:ui-monospace,SFMono-Regular,Consolas,"Liberation Mono",monospace}
.wf-amber{color:var(--color-warning)}
.wf-red{color:var(--color-danger);font-weight:700}
.wf-foot{display:flex;justify-content:space-between;align-items:center;gap:var(--space-3);margin-top:var(--space-3);flex-wrap:wrap}
.wf-chips{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:var(--space-3)}
.wf-chip{border:1px solid var(--border-strong);background:var(--surface-card);color:var(--text-primary);border-radius:999px;padding:7px 14px;font:inherit;font-size:var(--font-size-sm);font-weight:700;cursor:pointer;display:inline-flex;gap:8px;align-items:center}
.wf-chip--on{background:var(--regalis-black);border-color:var(--regalis-black);color:var(--regalis-gold-light)}
.wf-chip--red .wf-count{background:rgb(220 38 38 / 15%);color:var(--color-danger)}
.wf-menu{position:relative;display:inline-block}
.wf-menu__list{position:absolute;right:0;top:calc(100% + 4px);z-index:30;background:var(--surface-card);border:1px solid var(--border-strong);border-radius:var(--radius-md);box-shadow:var(--shadow-lg);min-width:220px;padding:6px}
.wf-menu__list button{all:unset;display:block;width:100%;box-sizing:border-box;padding:8px 10px;border-radius:6px;cursor:pointer;font-size:var(--font-size-sm)}
.wf-menu__list button:hover{background:var(--surface-hover)}
.wf-menu__list button.wf-danger{color:var(--color-danger)}
.wf-panel-note{border-radius:var(--radius-md);padding:var(--space-3) var(--space-4);margin:0 0 var(--space-3);font-size:var(--font-size-sm);line-height:1.5}
.wf-note--info{background:rgb(var(--accent-rgb) / 9%);border:1px solid rgb(var(--accent-rgb) / 35%)}
.wf-note--amber{background:rgb(217 119 6 / 10%);border:1px solid rgb(217 119 6 / 45%)}
.wf-note--red{background:rgb(220 38 38 / 8%);border:1px solid rgb(220 38 38 / 40%)}
.wf-grid2{display:grid;grid-template-columns:1fr 1fr;gap:var(--space-3)}
.wf-settings{display:grid;grid-template-columns:minmax(0,1fr) minmax(0,1fr);gap:var(--space-4);align-items:start}
@media (max-width:1000px){.wf-settings{grid-template-columns:1fr}}
@media (max-width:640px){.wf-grid2{grid-template-columns:1fr}}
.wf-settings section{margin-bottom:var(--space-4)}
.wf-h3{font-size:var(--font-size-md);font-weight:800;margin:0 0 2px}
.wf-sub{font-size:var(--font-size-sm);color:var(--text-secondary);margin:0 0 var(--space-3)}
.wf-option{display:flex;gap:12px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:12px 14px;margin-bottom:10px;cursor:pointer;background:var(--surface-card)}
.wf-option--on{border-color:var(--accent);background:rgb(var(--accent-rgb) / 10%)}
.wf-option input{margin-top:4px;accent-color:var(--accent)}
.wf-net{display:grid;grid-template-columns:minmax(0,1.4fr) 90px minmax(0,2fr) auto auto;gap:8px;align-items:end;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:10px;margin-bottom:8px}
@media (max-width:700px){.wf-net{grid-template-columns:1fr 1fr}}
.wf-dot{display:inline-block;width:8px;height:8px;border-radius:50%;margin-right:6px;background:var(--color-success)}
.wf-dot--off{background:var(--color-danger)}
.wf-timeline{list-style:none;margin:0;padding:0;max-height:280px;overflow:auto}
.wf-timeline li{padding:6px 0 6px 18px;position:relative;font-size:var(--font-size-sm);border-bottom:1px dashed var(--border-subtle)}
.wf-timeline li:before{content:"";position:absolute;left:4px;top:13px;width:7px;height:7px;border-radius:50%;background:var(--accent)}
.wf-textarea{width:100%;box-sizing:border-box;min-height:90px;font:inherit;padding:8px 10px;border:1px solid var(--border-strong);border-radius:var(--radius-sm);background:var(--surface-card);color:var(--text-primary)}
.modal:has(.wf-wide){max-width:min(820px,calc(100vw - 32px))}
.modal__body .field,.modal__body .wf-grid2{min-width:0}
/* My Wi-Fi */
.wf-my{display:grid;grid-template-columns:minmax(0,1.45fr) minmax(0,1fr);gap:var(--space-4);align-items:start}
@media (max-width:1050px){.wf-my{grid-template-columns:1fr}}
.wf-access{display:grid;grid-template-columns:minmax(0,1fr) 220px;gap:var(--space-4)}
@media (max-width:760px){.wf-access{grid-template-columns:1fr}}
.wf-field-label{font-size:var(--font-size-sm);color:var(--text-secondary);margin:var(--space-3) 0 6px}
.wf-box{display:flex;gap:8px;align-items:center;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:10px 10px 10px 16px;background:rgb(var(--accent-rgb) / 5%)}
.wf-box .wf-mono{flex:1;min-width:0;overflow-wrap:anywhere;font-size:1.25rem;font-weight:700}
.wf-box--pass{border-color:var(--accent);background:rgb(var(--accent-rgb) / 12%)}
.wf-box--pass .wf-mono{font-size:1.45rem;letter-spacing:.04em}
.wf-qr{border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3);text-align:center;background:#fff;color:#241F19}
.wf-qr svg{width:100%;max-width:190px;height:auto;display:block;margin:0 auto var(--space-2)}
.wf-qr p{margin:0;font-size:var(--font-size-sm);color:#6E6559}
.wf-facts{display:flex;gap:var(--space-5);flex-wrap:wrap;margin-top:var(--space-3)}
.wf-facts b{display:block;font-size:1.15rem}
.wf-actions{display:flex;gap:10px;flex-wrap:wrap;border-top:1px solid var(--border-subtle);margin-top:var(--space-4);padding-top:var(--space-4)}
.wf-device{display:flex;gap:10px;align-items:center;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:12px 14px;margin-bottom:10px}
.wf-device>div{flex:1;min-width:0}
.wf-slot{border:1.5px dashed var(--border-strong);border-radius:var(--radius-md);padding:12px;text-align:center;color:var(--text-secondary);font-size:var(--font-size-sm)}
.wf-rules{background:rgb(var(--accent-rgb) / 8%);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);padding:var(--space-4);line-height:1.55}
.wf-steps{margin:6px 0 0;padding-left:20px;line-height:1.8}
.wf-earlier li{display:flex;justify-content:space-between;align-items:center;gap:8px;padding:10px 0;border-bottom:1px solid var(--border-subtle)}
.wf-earlier{list-style:none;margin:0;padding:0}
@media (max-width:640px){
  .wf-my .wf-box--pass{background:var(--regalis-black);border-color:var(--regalis-black)}
  .wf-my .wf-box--pass .wf-mono{color:var(--regalis-gold-light);font-size:1.6rem}
  .wf-my .wf-box--pass .btn--outline{color:var(--regalis-gold-light);border-color:rgb(var(--accent-rgb) / 60%);background:transparent}
  .wf-table--cards thead{display:none}
  .wf-table--cards tr{display:block;border:1px solid var(--border-subtle);border-radius:var(--radius-md);margin-bottom:10px;padding:6px 4px;background:var(--surface-card)}
  .wf-table--cards td{display:block;border:none;padding:4px 10px}
  .wf-table--cards td[data-hide]{display:none}
  .wf-table--cards td.wf-right{text-align:left}
}
`;
function useCss() {
  React.useEffect(() => {
    if (document.getElementById("rg-wifi-css")) return;
    const s = document.createElement("style");
    s.id = "rg-wifi-css";
    s.textContent = CSS;
    document.head.appendChild(s);
  }, []);
}

const IST = "Asia/Kolkata";
const fmtDT = (u) => (u ? new Intl.DateTimeFormat("en-IN", { timeZone: IST, day: "numeric", month: "short", hour: "numeric", minute: "2-digit" }).format(new Date(u)) : "—");
const fmtD = (u) => (u ? new Intl.DateTimeFormat("en-IN", { timeZone: IST, day: "numeric", month: "short", year: "numeric" }).format(new Date(u)) : "—");
const fmtLong = (u) => (u ? new Intl.DateTimeFormat("en-IN", { timeZone: IST, day: "numeric", month: "long", year: "numeric" }).format(new Date(u)) : "—");
const fmtT = (u) => (u ? new Intl.DateTimeFormat("en-IN", { timeZone: IST, hour: "numeric", minute: "2-digit" }).format(new Date(u)) : "");
const sameDay = (a, b) => fmtD(a) === fmtD(b);
const isToday = (u) => sameDay(u, new Date().toISOString());
const since = (u) => (isToday(u) ? "Today, " + fmtT(u) : fmtDT(u));
const dur = (fromIso) => {
  const m = Math.max(0, Math.round((Date.now() - new Date(fromIso).getTime()) / 60000));
  if (m < 60) return `${m} m`;
  if (m < 1440) return `${Math.floor(m / 60)} h ${String(m % 60).padStart(2, "0")} m`;
  return `${Math.floor(m / 1440)} d ${Math.floor((m % 1440) / 60)} h`;
};
const left = (untilIso) => {
  const m = Math.round((new Date(untilIso).getTime() - Date.now()) / 60000);
  if (m <= 0) return "ended";
  if (m < 60) return `expires in ${m} min`;
  if (m < 1440) return `expires in ${Math.round(m / 60)} h`;
  return null;
};
const bytes = (n) => {
  if (!n) return "0 MB";
  if (n < 1024 ** 3) return `${Math.max(1, Math.round(n / 1024 ** 2))} MB`;
  return `${(n / 1024 ** 3).toFixed(1)} GB`;
};
const initials = (s) =>
  (s || "?")
    .split(/\s+/)
    .filter(Boolean)
    .slice(0, 2)
    .map((w) => w[0].toUpperCase())
    .join("");
const errMsg = (e, fallback) => (e instanceof ApiError ? e.message : fallback);
const toIstInput = (u) => (u ? new Date(new Date(u).getTime() + 330 * 60000).toISOString().slice(0, 16) : "");
const fromIstInput = (v) => (v ? new Date(v + ":00+05:30").toISOString() : null);
const dayStartIso = (v) => (v ? new Date(v + "T00:00:00+05:30").toISOString() : null);
const dayEndIso = (v) => (v ? new Date(new Date(v + "T00:00:00+05:30").getTime() + 864e5 - 1000).toISOString() : null);
const istDate = (offsetDays) => new Date(Date.now() + 330 * 60000 + offsetDays * 864e5).toISOString().slice(0, 10);

const TONE = { active: "success", inuse: "info", expiring: "warning", issued: "gold", expired: "neutral", paused: "info", revoked: "danger" };
function StatusPill({ row }) {
  if (row.controllerState === "failed" && (row.status === "issued" || row.status === "active")) return el(Badge, { tone: "danger" }, "Not created");
  if (row.status === "issued" && (row.emailStatus === "failed" || row.emailStatus === "none") && !row.deliveredUtc)
    return el(Badge, { tone: "danger" }, row.emailStatus === "none" ? "No email address" : "Email failed");
  return el(Badge, { tone: TONE[row.status] || "neutral" }, row.statusLabel);
}

async function download(path, params, name) {
  const r = await http.get(path, { params, responseType: "blob" });
  const url = URL.createObjectURL(r.data);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

async function copyText(text) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const t = document.createElement("textarea");
    t.value = text;
    t.style.position = "fixed";
    t.style.opacity = "0";
    document.body.appendChild(t);
    t.select();
    const ok = document.execCommand("copy");
    t.remove();
    return ok;
  }
}

function openWhatsApp(url) {
  const w = window.open(url, "_blank", "noopener");
  if (!w) window.location.href = url;
}

function Textarea({ label, hint, value, onChange, required, maxLength, placeholder }) {
  const id = React.useId();
  return el(
    "div",
    { className: "field" },
    el("label", { className: "field__label", htmlFor: id }, label, required ? el("span", { className: "field__required" }, " *") : null),
    el("textarea", { id, className: "wf-textarea", value, onChange, maxLength, placeholder }),
    hint ? el("span", { className: "field__hint" }, hint) : null
  );
}

function Pager({ page, onPage }) {
  if (!page || page.totalPages <= 1) return null;
  return el(
    "div",
    { style: { display: "flex", gap: 6 } },
    el(Button, { size: "sm", variant: "outline", disabled: !page.hasPrevious, onClick: () => onPage(page.page - 1), "aria-label": "Previous page" }, "‹"),
    el(Button, { size: "sm", variant: "outline", disabled: !page.hasNext, onClick: () => onPage(page.page + 1), "aria-label": "Next page" }, "›")
  );
}

/* ---------------------------------------------------------------------------------------- */
/* QR (existing AssetQr encoder)                                                               */
/* ---------------------------------------------------------------------------------------- */

function QrSvg({ text, size }) {
  const [path, setPath] = React.useState(null);
  React.useEffect(() => {
    let live = true;
    if (!text) return;
    import("./AssetQr-AS1.js")
      .then((m) => live && setPath(m.qrPath(text, "M", 2)))
      .catch(() => live && setPath(null));
    return () => {
      live = false;
    };
  }, [text]);
  if (!path) return el("div", { style: { height: size || 190 } });
  return el(
    "svg",
    { viewBox: `0 0 ${path.viewBox} ${path.viewBox}`, role: "img", "aria-label": "Wi-Fi QR code", shapeRendering: "crispEdges", style: size ? { width: size, height: size } : null },
    el("rect", { width: path.viewBox, height: path.viewBox, fill: "#fff" }),
    el("path", { d: path.d, fill: "#241F19" })
  );
}

/* ---------------------------------------------------------------------------------------- */
/* Admin: the page with four tabs (spec 3, 5.1-5.3)                                             */
/* ---------------------------------------------------------------------------------------- */

const TABS = [
  ["connected", "Connected now"],
  ["access", "Access issued"],
  ["usage", "Usage log"],
  ["settings", "Settings"]
];

export function WifiAdminPage() {
  useCss();
  const { tab: raw } = useParams();
  const tab = TABS.some(([k]) => k === raw) ? raw : "connected";
  const navigate = useNavigate();
  const canManage = usePermission("WIFI.MANAGE");
  const [guest, setGuest] = React.useState(false);
  const [counts, setCounts] = React.useState({});
  const [stamp, setStamp] = React.useState(0);
  const go = (k) => navigate(k === "connected" ? "/admin/wifi" : `/admin/wifi/${k}`);
  const exportBtn =
    tab === "connected" || tab === "usage"
      ? el(Button, { variant: "outline", onClick: () => window.dispatchEvent(new CustomEvent("rg-wifi-export")) }, "Export")
      : null;
  return el(
    F,
    null,
    el(PageHeader, {
      title: "Wi-Fi",
      description:
        tab === "access"
          ? "One row per booking. Access is created the moment the payment succeeds, sent to the customer by email and in the app, and expires by itself when the booking ends."
          : tab === "settings"
            ? "How access is created, how long it lasts, and how the app talks to the Wi-Fi equipment. Set once, then the rest happens on its own."
            : "Every device on the network right now, and the customer it belongs to. Access is issued automatically when a booking is paid.",
      actions: el(
        F,
        null,
        exportBtn,
        tab !== "settings" && el(Button, { variant: "outline", onClick: () => go("settings") }, "Settings"),
        tab === "settings" && el(Button, { variant: "outline", onClick: () => go("connected") }, "Connected now"),
        canManage && el(Button, { variant: "primary", onClick: () => setGuest(true) }, "Issue guest access")
      )
    }),
    el(
      "div",
      { className: "wf-panel" },
      el(
        "div",
        { className: "wf-tabs", role: "tablist" },
        TABS.map(([k, label]) =>
          el(
            "button",
            { key: k, type: "button", role: "tab", "aria-selected": tab === k, className: "wf-tab" + (tab === k ? " wf-tab--on" : ""), onClick: () => go(k) },
            label,
            counts[k] != null && el("span", { className: "wf-count" }, counts[k])
          )
        )
      ),
      tab === "connected" && el(ConnectedTab, { key: "c" + stamp, canManage, onCount: (n) => setCounts((c) => ({ ...c, connected: n })) }),
      tab === "access" && el(AccessTab, { key: "a" + stamp, canManage, onCount: (n) => setCounts((c) => ({ ...c, access: n })) }),
      tab === "usage" && el(UsageTab, null),
      tab === "settings" && el(SettingsTab, { canManage })
    ),
    guest &&
      el(GuestDialog, {
        onClose: () => setGuest(false),
        onSaved: () => {
          setGuest(false);
          setStamp((s) => s + 1);
          if (tab !== "access") go("access");
        }
      })
  );
}

/* ---- Connected now ------------------------------------------------------------------------- */

const WHO = [
  ["", "Everyone"],
  ["members", "Members"],
  ["daypass", "Day pass and bookings"],
  ["guests", "Guests"],
  ["unknown", "Unrecognised devices"]
];

function ConnectedTab({ canManage, onCount }) {
  const toast = useToast();
  const [f, setF] = React.useState({ search: "", who: "", ssid: "", area: "" });
  const [term, setTerm] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [tick, setTick] = React.useState(0);
  const [busy, setBusy] = React.useState(null);
  const [blockRow, setBlockRow] = React.useState(null);
  React.useEffect(() => {
    const t = setTimeout(() => setTerm(f.search), 300);
    return () => clearTimeout(t);
  }, [f.search]);
  const params = { search: term || void 0, who: f.who || void 0, ssid: f.ssid || void 0, area: f.area || void 0, page, pageSize: 25 };
  const load = React.useCallback(async () => {
    try {
      const d = await unwrap(http.get("/admin/wifi/connected", { params }));
      setData(d);
      onCount(d.page.totalCount);
    } catch (e) {
      toast.error(errMsg(e, "Could not load who is connected."));
    }
  }, [term, f.who, f.ssid, f.area, page]);
  React.useEffect(() => {
    load();
    const t = setInterval(load, 30000);
    return () => clearInterval(t);
  }, [load]);
  React.useEffect(() => {
    const t = setInterval(() => setTick((x) => x + 1), 5000);
    const ex = () => download("/admin/wifi/connected/export", params, "wifi_connected.xlsx").catch(() => toast.error("Export failed."));
    window.addEventListener("rg-wifi-export", ex);
    return () => {
      clearInterval(t);
      window.removeEventListener("rg-wifi-export", ex);
    };
  }, [term, f.who, f.ssid, f.area]);
  const act = async (row, block, reason) => {
    setBusy(row.mac);
    try {
      await unwrap(http.post(`/admin/wifi/clients/${encodeURIComponent(row.mac)}/disconnect`, { block, reason: reason || null }));
      toast.success(block ? "Device blocked." : "Device disconnected.");
      setBlockRow(null);
      load();
    } catch (e) {
      toast.error(errMsg(e, "That did not work."));
    } finally {
      setBusy(null);
    }
  };
  if (!data) return el(Spinner, { center: true });
  const c = data.cards;
  const failedBits = [c.failedExpired && `${c.failedExpired} expired`, c.failedPassword && `${c.failedPassword} wrong password`, c.failedLimit && `${c.failedLimit} device limit`].filter(Boolean);
  const ago = data.lastPollUtc ? Math.max(0, Math.round((Date.now() - new Date(data.lastPollUtc).getTime()) / 1000)) : null;
  void tick;
  return el(
    F,
    null,
    data.controllerType === "simulated" &&
      el(
        "div",
        { className: "wf-panel-note wf-note--info" },
        el("b", null, "Simulated Wi-Fi controller. "),
        "No Wi-Fi equipment is connected yet (open item 1 of the specification). Devices appear here only when added from Settings → Simulate a device. Everything else — issuing, delivery, expiry — is real."
      ),
    data.mode === "shared" &&
      el("div", { className: "wf-panel-note wf-note--amber" }, "Everyone shares one password, so devices cannot be named: this list shows MAC addresses only. Change it in Settings → How access is created."),
    !data.lastPollOk &&
      el("div", { className: "wf-panel-note wf-note--red" }, el("b", null, "The Wi-Fi controller is not answering. "), data.lastPollError || "", " This list may be out of date."),
    el(
      "div",
      { className: "wf-cards" },
      el(Card, { label: "Online now", value: c.onlinePeople, sub: `on ${c.onlineDevices} device${c.onlineDevices === 1 ? "" : "s"}` }),
      el(Card, { label: "Customers today", value: c.customersToday, sub: `of ${c.customersWithAccess} with access` }),
      el(Card, { label: "Guests online", value: c.guestsOnline, sub: "day passes, bookings and visitors", tone: "purple" }),
      el(Card, { label: "Data today", value: bytes(c.bytesToday), sub: c.peakAtUtc ? `Peak ${c.peakDevices} devices at ${fmtT(c.peakAtUtc)}` : `Peak ${c.peakDevices} devices` }),
      el(Card, { label: "Failed logins", value: c.failedToday, sub: failedBits.length ? failedBits.join(", ") : "none today", tone: c.failedToday ? "amber" : null })
    ),
    el(
      "div",
      { className: "wf-filters" },
      el(Input, { label: "Search", placeholder: "Search customer, device, MAC or IP", value: f.search, onChange: (e) => (setF({ ...f, search: e.target.value }), setPage(1)) }),
      el(Select, { label: "Who", value: f.who, options: WHO.map(([v, l]) => ({ value: v, label: l })), onChange: (e) => (setF({ ...f, who: e.target.value }), setPage(1)) }),
      el(Select, { label: "Network", value: f.ssid, options: [{ value: "", label: "All networks" }, ...data.networks.map((n) => ({ value: n, label: n }))], onChange: (e) => (setF({ ...f, ssid: e.target.value }), setPage(1)) }),
      el(Select, { label: "Area", value: f.area, options: [{ value: "", label: "All areas" }, ...data.areas.map((n) => ({ value: n, label: n }))], onChange: (e) => (setF({ ...f, area: e.target.value }), setPage(1)) })
    ),
    data.page.items.length === 0
      ? el(EmptyState, { title: "Nobody is connected", description: f.search || f.who || f.ssid || f.area ? "No device matches these filters." : "When a device joins the Wi-Fi it appears here, named after the customer whose password it used." })
      : el(
          "div",
          { className: "wf-scroll" },
          el(
            "table",
            { className: "wf-table wf-table--cards" },
            el("thead", null, el("tr", null, ["Customer", "Device", "Network / area", "Connected since", "Data used", "Booking", ""].map((h, i) => el("th", { key: i }, h)))),
            el(
              "tbody",
              null,
              data.page.items.map((r) => {
                const unknown = r.accessId == null;
                const who =
                  r.who === "team"
                    ? "Team member of a company account"
                    : r.who === "guest"
                      ? "Guest" + (r.email ? " · " + r.email : "")
                      : r.who === "daypass"
                        ? "Day pass · " + (r.email || r.mobile || "")
                        : [r.company, r.mobile].filter(Boolean).join(" · ");
                return el(
                  "tr",
                  { key: r.sessionId, className: unknown ? "wf-row--unknown" : "" },
                  el(
                    "td",
                    null,
                    el(
                      "div",
                      { className: "wf-who" },
                      el("span", { className: "wf-avatar" }, unknown ? "?" : initials(r.holderName)),
                      el(
                        "div",
                        null,
                        unknown
                          ? el(F, null, el("div", { className: "wf-unknown" }, "Unrecognised device"), el("div", { className: "wf-muted" }, "No customer matched — see Settings"))
                          : el(
                              F,
                              null,
                              el("div", { className: "wf-name" }, r.who === "team" && r.company ? `${r.company} — ${r.holderName}` : r.holderName),
                              el("div", { className: "wf-muted" }, r.otherDevice ? "Another device on the same access" : who)
                            )
                      )
                    )
                  ),
                  el("td", null, el("div", null, r.deviceName || "Unknown"), el("div", { className: "wf-muted wf-mono" }, r.mac)),
                  el("td", { "data-hide": "" }, el("div", null, r.ssid || "—"), el("div", { className: "wf-muted" }, r.accessPoint || "")),
                  el("td", null, el("div", null, since(r.startedUtc)), el("div", { className: "wf-muted" }, dur(r.startedUtc))),
                  el("td", { "data-hide": "" }, bytes(r.bytes)),
                  el(
                    "td",
                    { "data-hide": "" },
                    el("div", null, r.bookingNumber || "—"),
                    r.validUntilUtc && left(r.validUntilUtc)
                      ? el("div", { className: "wf-muted wf-amber" }, "Ends " + fmtT(r.validUntilUtc))
                      : el("div", { className: "wf-muted" }, r.bookingLabel || "")
                  ),
                  el(
                    "td",
                    { className: "wf-right" },
                    canManage &&
                      (unknown
                        ? el(Button, { size: "sm", variant: "danger-outline", loading: busy === r.mac, onClick: () => setBlockRow(r) }, "Block")
                        : el(Button, { size: "sm", variant: "outline", loading: busy === r.mac, onClick: () => act(r, false) }, "Disconnect"))
                  )
                );
              })
            )
          )
        ),
    el(
      "div",
      { className: "wf-foot" },
      el(
        "span",
        { className: "wf-muted", style: { fontSize: "var(--font-size-sm)" } },
        `Live from the Wi-Fi controller${data.controllerType === "simulated" ? " (simulated)" : ""} · `,
        ago == null ? "not read yet" : `refreshed ${ago < 60 ? ago + " seconds" : Math.round(ago / 60) + " minutes"} ago`,
        ` · ${data.page.items.length} of ${data.page.totalCount} devices`
      ),
      el(Pager, { page: data.page, onPage: setPage })
    ),
    blockRow && el(BlockDialog, { row: blockRow, onClose: () => setBlockRow(null), onBlock: (reason) => act(blockRow, true, reason) })
  );
}

function Card({ label, value, sub, tone }) {
  return el(
    "div",
    { className: "wf-card" + (tone ? " wf-card--" + tone : "") },
    el("div", { className: "wf-card__label" }, label),
    el("div", { className: "wf-card__value" }, value),
    el("div", { className: "wf-card__sub" }, sub)
  );
}

function BlockDialog({ row, onClose, onBlock }) {
  const [reason, setReason] = React.useState("");
  return el(
    Modal,
    {
      open: true,
      title: "Block this device?",
      onClose,
      footer: el(F, null, el(Button, { variant: "ghost", onClick: onClose }, "Cancel"), el(Button, { variant: "danger", onClick: () => onBlock(reason) }, "Block device"))
    },
    el("p", null, el("b", null, row.deviceName || "Unknown device"), " ", el("span", { className: "wf-mono" }, row.mac), " is taken off the Wi-Fi and cannot join again until it is unblocked."),
    el(Input, { label: "Why (kept in the access log)", value: reason, maxLength: 300, onChange: (e) => setReason(e.target.value) })
  );
}

/* ---- Access issued ------------------------------------------------------------------------- */

const CHIPS = [
  ["", "All", "all"],
  ["active", "Active", "active"],
  ["expiring", "Expiring today", "expiringToday"],
  ["expired", "Expired", "expired"],
  ["paused", "Paused", "paused"],
  ["revoked", "Revoked", "revoked"],
  ["undelivered", "Not delivered", "notDelivered"]
];
const KINDS = [
  ["", "All booking types"],
  ["member", "Members"],
  ["daypass", "Day passes"],
  ["short", "Desks and cabins by the hour or day"],
  ["meeting", "Meeting rooms"],
  ["guest", "Guests"],
  ["team", "Company accounts (team members)"]
];
const PERIODS = [
  ["month", "This month"],
  ["7", "Last 7 days"],
  ["30", "Last 30 days"],
  ["90", "Last 90 days"],
  ["all", "All time"]
];

function AccessTab({ canManage, onCount }) {
  const toast = useToast();
  const navigate = useNavigate();
  const [f, setF] = React.useState({ chip: "", search: "", kind: "", period: "month" });
  const [term, setTerm] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [dialog, setDialog] = React.useState(null);
  const [busy, setBusy] = React.useState(null);
  React.useEffect(() => {
    const t = setTimeout(() => setTerm(f.search), 300);
    return () => clearTimeout(t);
  }, [f.search]);
  const load = React.useCallback(async () => {
    try {
      const d = await unwrap(http.get("/admin/wifi/access", { params: { chip: f.chip || void 0, search: term || void 0, kind: f.kind || void 0, period: f.period, page, pageSize: 25 } }));
      setData(d);
      onCount(d.counts.all);
    } catch (e) {
      toast.error(errMsg(e, "Could not load the access list."));
    }
  }, [f.chip, term, f.kind, f.period, page]);
  React.useEffect(() => {
    load();
  }, [load]);
  const resend = async (row, channel) => {
    setBusy(row.accessId + channel);
    try {
      const r = await unwrap(http.post(`/admin/wifi/access/${row.accessId}/resend`, { channel }));
      if (r.url) openWhatsApp(r.url);
      r.ok ? toast.success(r.message) : toast.error(r.message);
      load();
    } catch (e) {
      toast.error(errMsg(e, "Could not resend."));
    } finally {
      setBusy(null);
    }
  };
  const simple = async (row, path, done) => {
    setBusy(row.accessId + path);
    try {
      await unwrap(http.post(`/admin/wifi/access/${row.accessId}/${path}`, {}));
      toast.success(done);
      load();
    } catch (e) {
      toast.error(errMsg(e, "That did not work."));
    } finally {
      setBusy(null);
    }
  };
  const ended = (r) => r.status === "expired" || r.status === "revoked";
  const menu = (r) =>
    [
      r.customerId && ["View customer", () => navigate(`/admin/customers/${r.customerId}`)],
      ["See devices and history", () => setDialog({ kind: "detail", row: r })],
      canManage && !ended(r) && r.email && ["Resend by email", () => resend(r, "email")],
      canManage && !ended(r) && r.mobile && ["Send by WhatsApp", () => resend(r, "whatsapp")],
      canManage && !ended(r) && ["Reset password", () => setDialog({ kind: "reset", row: r })],
      canManage && r.status !== "revoked" && ["Change device limit", () => setDialog({ kind: "extend", row: r, focus: "limit" })],
      canManage && r.status !== "revoked" && [r.status === "expired" ? "Re-issue (extend validity)" : "Extend validity", () => setDialog({ kind: "extend", row: r })],
      canManage && (r.kind === "booking" || r.kind === "pass") && !ended(r) && ["Add a team member", () => setDialog({ kind: "detail", row: r, addMember: true })],
      canManage && r.controllerState === "failed" && ["Retry the Wi-Fi controller", () => simple(r, "retry", "Retried.")],
      canManage && r.status !== "revoked" && ["Revoke now", () => setDialog({ kind: "revoke", row: r }), true]
    ].filter(Boolean);
  if (!data) return el(Spinner, { center: true });
  return el(
    F,
    null,
    el(
      "div",
      { className: "wf-chips", role: "group", "aria-label": "Filter by status" },
      CHIPS.map(([k, label, ck]) =>
        el(
          "button",
          {
            key: k || "all",
            type: "button",
            className: "wf-chip" + (f.chip === k ? " wf-chip--on" : "") + (k === "undelivered" ? " wf-chip--red" : ""),
            onClick: () => (setF({ ...f, chip: k }), setPage(1))
          },
          label,
          el("span", { className: "wf-count" }, data.counts[ck] ?? 0)
        )
      )
    ),
    el(
      "div",
      { className: "wf-filters", style: { gridTemplateColumns: "2fr 1fr 1fr" } },
      el(Input, { label: "Search", placeholder: "Search customer, booking or username", value: f.search, onChange: (e) => (setF({ ...f, search: e.target.value }), setPage(1)) }),
      el(Select, { label: "Booking type", value: f.kind, options: KINDS.map(([v, l]) => ({ value: v, label: l })), onChange: (e) => (setF({ ...f, kind: e.target.value }), setPage(1)) }),
      el(Select, { label: "Period", value: f.period, options: PERIODS.map(([v, l]) => ({ value: v, label: l })), onChange: (e) => (setF({ ...f, period: e.target.value }), setPage(1)) })
    ),
    data.page.items.length === 0
      ? el(EmptyState, { title: "No Wi-Fi access here", description: "Access appears the moment a booking or pass is paid, or when you issue guest access." })
      : el(
          "div",
          { className: "wf-scroll" },
          el(
            "table",
            { className: "wf-table wf-table--cards" },
            el("thead", null, el("tr", null, ["Customer", "Booking", "Wi-Fi access", "Valid", "Devices", "Status", ""].map((h, i) => el("th", { key: i }, h)))),
            el(
              "tbody",
              null,
              data.page.items.map((r) => {
                const soon = r.status !== "expired" && r.status !== "revoked" ? left(r.validUntilUtc) : null;
                const failedMail = r.status === "issued" && (r.emailStatus === "failed" || r.emailStatus === "none") && !r.deliveredUtc && r.controllerState === "live";
                return el(
                  "tr",
                  { key: r.accessId },
                  el(
                    "td",
                    null,
                    el("div", { className: "wf-name" }, r.memberCount > 0 && r.company ? r.company : r.holderName),
                    el(
                      "div",
                      { className: "wf-muted" },
                      r.memberCount > 0
                        ? `${r.memberCount} team member${r.memberCount === 1 ? "" : "s"} · one access each`
                        : r.status === "revoked" && r.revokedReason
                          ? r.revokedReason
                          : [r.email, r.mobile].filter(Boolean).join(" · ")
                    )
                  ),
                  el("td", null, el("div", { className: "wf-mono" }, r.bookingNumber || r.accessNo), el("div", { className: "wf-muted" }, r.bookingLabel || "")),
                  el(
                    "td",
                    null,
                    el("div", { className: "wf-mono" }, r.username, r.memberCount > 0 ? ` + ${r.memberCount} more` : ""),
                    el("div", { className: "wf-muted" }, r.ssid, " · password hidden")
                  ),
                  el(
                    "td",
                    null,
                    el("div", null, sameDay(r.validFromUtc, r.validUntilUtc) ? `${fmtDT(r.validFromUtc)} – ${fmtT(r.validUntilUtc)}` : `${fmtD(r.validFromUtc)} – ${fmtD(r.validUntilUtc)}`),
                    soon ? el("div", { className: "wf-muted wf-amber" }, soon) : r.renews && r.status !== "revoked" ? el("div", { className: "wf-muted" }, "renews with the booking") : null
                  ),
                  el(
                    "td",
                    null,
                    r.memberCount > 0
                      ? `${r.devicesUsed + r.memberDevicesUsed} of ${r.deviceLimit + r.memberDeviceLimit} used`
                      : ended(r)
                        ? `${r.devicesUsed} active`
                        : `${r.devicesUsed} of ${r.deviceLimit} used`
                  ),
                  el("td", null, el(StatusPill, { row: r }), r.status === "paused" && el("div", { className: "wf-muted" }, r.pauseReason)),
                  el(
                    "td",
                    { className: "wf-right" },
                    el(
                      "div",
                      { style: { display: "inline-flex", gap: 6, alignItems: "center" } },
                      canManage &&
                        (failedMail && r.mobile
                          ? el(Button, { size: "sm", variant: "primary", loading: busy === r.accessId + "whatsapp", onClick: () => resend(r, "whatsapp") }, "Send by WhatsApp")
                          : r.memberCount > 0
                            ? el(Button, { size: "sm", variant: "outline", onClick: () => setDialog({ kind: "detail", row: r }) }, "Members")
                            : r.status === "expired"
                              ? el(Button, { size: "sm", variant: "outline", onClick: () => setDialog({ kind: "extend", row: r }) }, "Re-issue")
                              : r.controllerState === "failed"
                                ? el(Button, { size: "sm", variant: "danger-outline", loading: busy === r.accessId + "retry", onClick: () => simple(r, "retry", "Retried.") }, "Retry")
                                : !ended(r) && r.status !== "paused" && r.email
                                  ? el(Button, { size: "sm", variant: "outline", loading: busy === r.accessId + "email", onClick: () => resend(r, "email") }, "Resend")
                                  : null),
                      el(RowMenu, { items: menu(r) })
                    )
                  )
                );
              })
            )
          )
        ),
    el(
      "div",
      { className: "wf-foot" },
      el(
        "span",
        { className: "wf-muted", style: { fontSize: "var(--font-size-sm)", maxWidth: 760 } },
        "Row menu: view the customer, see their devices, resend by email or WhatsApp, reset the password, change the device limit, extend the validity, or revoke access now. Every one of these is written to the access log with the admin's name."
      ),
      el(Pager, { page: data.page, onPage: setPage })
    ),
    dialog?.kind === "detail" && el(AccessDetailDialog, { id: dialog.row.accessId, canManage, addMember: dialog.addMember, onClose: () => setDialog(null), onChanged: load }),
    dialog?.kind === "extend" && el(ExtendDialog, { row: dialog.row, onClose: () => setDialog(null), onSaved: () => (setDialog(null), load()) }),
    dialog?.kind === "revoke" && el(RevokeDialog, { row: dialog.row, onClose: () => setDialog(null), onSaved: () => (setDialog(null), load()) }),
    dialog?.kind === "reset" &&
      el(ConfirmDialog, {
        title: "Reset the Wi-Fi password?",
        text: `${dialog.row.holderName} gets a new password by email and in the app. Every device on the old one is signed out. Nobody else is affected.`,
        okLabel: "Reset password",
        onClose: () => setDialog(null),
        onOk: async () => {
          try {
            await unwrap(http.post(`/admin/wifi/access/${dialog.row.accessId}/reset`, {}));
            toast.success("New password issued and sent.");
            setDialog(null);
            load();
          } catch (e) {
            toast.error(errMsg(e, "Could not reset."));
          }
        }
      })
  );
}

function RowMenu({ items }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (!open) return;
    const close = (e) => ref.current && !ref.current.contains(e.target) && setOpen(false);
    document.addEventListener("mousedown", close);
    return () => document.removeEventListener("mousedown", close);
  }, [open]);
  return el(
    "div",
    { className: "wf-menu", ref },
    el(Button, { size: "sm", variant: "outline", "aria-label": "More actions", "aria-haspopup": "menu", "aria-expanded": open, onClick: () => setOpen((o) => !o) }, "⋯"),
    open &&
      el(
        "div",
        { className: "wf-menu__list", role: "menu" },
        items.map(([label, fn, danger]) =>
          el("button", { key: label, type: "button", role: "menuitem", className: danger ? "wf-danger" : "", onClick: () => (setOpen(false), fn()) }, label)
        )
      )
  );
}

function ConfirmDialog({ title, text, okLabel, onClose, onOk, danger }) {
  const [busy, setBusy] = React.useState(false);
  return el(
    Modal,
    {
      open: true,
      title,
      onClose: () => busy || onClose(),
      footer: el(
        F,
        null,
        el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"),
        el(Button, { variant: danger ? "danger" : "primary", loading: busy, onClick: async () => (setBusy(true), await onOk(), setBusy(false)) }, okLabel)
      )
    },
    el("p", null, text)
  );
}

function ExtendDialog({ row, onClose, onSaved }) {
  const toast = useToast();
  const [until, setUntil] = React.useState(toIstInput(new Date(Math.max(Date.now() + 864e5, new Date(row.validUntilUtc).getTime())).toISOString()));
  const [limit, setLimit] = React.useState(String(row.deviceLimit));
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const save = async () => {
    setBusy(true);
    try {
      await unwrap(http.patch(`/admin/wifi/access/${row.accessId}`, { validUntilUtc: fromIstInput(until), deviceLimit: Number(limit), reason }));
      toast.success(row.status === "expired" ? "Re-issued with the same password." : "Access updated.");
      onSaved();
    } catch (e) {
      toast.error(errMsg(e, "Could not save."));
    } finally {
      setBusy(false);
    }
  };
  return el(
    Modal,
    {
      open: true,
      title: row.status === "expired" ? `Re-issue ${row.username}` : `Extend or change ${row.username}`,
      onClose: () => busy || onClose(),
      footer: el(F, null, el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"), el(Button, { variant: "primary", loading: busy, disabled: !reason.trim(), onClick: save }, "Save"))
    },
    row.status === "expired" && el("div", { className: "wf-panel-note wf-note--info" }, "The access comes back with the same username and password, and the customer is told."),
    el(
      "div",
      { className: "wf-grid2" },
      el(Input, { label: "Valid until", type: "datetime-local", value: until, onChange: (e) => setUntil(e.target.value) }),
      el(Input, { label: "Device limit", type: "number", min: 1, max: 20, value: limit, onChange: (e) => setLimit(e.target.value) })
    ),
    el(Input, { label: "Why", required: true, value: reason, maxLength: 300, hint: "Written to the access log with your name.", onChange: (e) => setReason(e.target.value) })
  );
}

function RevokeDialog({ row, onClose, onSaved }) {
  const toast = useToast();
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const save = async () => {
    setBusy(true);
    try {
      await unwrap(http.post(`/admin/wifi/access/${row.accessId}/revoke`, { reason }));
      toast.success("Access revoked.");
      onSaved();
    } catch (e) {
      toast.error(errMsg(e, "Could not revoke."));
    } finally {
      setBusy(false);
    }
  };
  return el(
    Modal,
    {
      open: true,
      title: `Revoke ${row.username}?`,
      onClose: () => busy || onClose(),
      footer: el(F, null, el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"), el(Button, { variant: "danger", loading: busy, disabled: !reason.trim(), onClick: save }, "Revoke now"))
    },
    el("p", null, "The password stops working at once and every device on it is disconnected", row.memberCount > 0 ? ", including the team members' access" : "", ". Revoked access stays in the list with its history; it is never deleted."),
    el(Input, { label: "Reason", required: true, value: reason, maxLength: 500, placeholder: "Cancelled, ended early, password shared …", onChange: (e) => setReason(e.target.value) })
  );
}

function AccessDetailDialog({ id, canManage, addMember, onClose, onChanged }) {
  const toast = useToast();
  const [d, setD] = React.useState(null);
  const [m, setM] = React.useState({ name: "", email: "", mobile: "", open: !!addMember });
  const [busy, setBusy] = React.useState(false);
  const load = React.useCallback(async () => {
    try {
      setD(await unwrap(http.get(`/admin/wifi/access/${id}`)));
    } catch (e) {
      toast.error(errMsg(e, "Could not open the access."));
      onClose();
    }
  }, [id]);
  React.useEffect(() => {
    load();
  }, [load]);
  const addMemberSave = async () => {
    setBusy(true);
    try {
      const r = await unwrap(http.post(`/admin/wifi/access/${id}/members`, { name: m.name, email: m.email || null, mobile: m.mobile || null }));
      toast.success(`${r.holderName} added as ${r.username}. Their details were sent to them.`);
      setM({ name: "", email: "", mobile: "", open: false });
      load();
      onChanged();
    } catch (e) {
      toast.error(errMsg(e, "Could not add the team member."));
    } finally {
      setBusy(false);
    }
  };
  const removeDevice = async (dev) => {
    try {
      await unwrap(http.delete(`/admin/wifi/access/${id}/devices/${dev.deviceId}`));
      toast.success("Device removed; its slot is free.");
      load();
      onChanged();
    } catch (e) {
      toast.error(errMsg(e, "Could not remove the device."));
    }
  };
  const r = d?.row;
  return el(
    Modal,
    { open: true, title: r ? `${r.accessNo} · ${r.holderName}` : "Wi-Fi access", onClose, footer: el(Button, { variant: "ghost", onClick: onClose }, "Close") },
    !d
      ? el(Spinner, { center: true })
      : el(
          "div",
          { className: "wf-wide" },
          el(
            "div",
            { className: "wf-grid2", style: { marginBottom: "var(--space-3)" } },
            el(
              "div",
              null,
              el("div", { className: "wf-muted" }, "Wi-Fi access"),
              el("div", { className: "wf-mono wf-name" }, r.username),
              el("div", { className: "wf-muted" }, `${r.ssid} · password hidden · issued by ${r.issuedBy}`)
            ),
            el(
              "div",
              null,
              el("div", { className: "wf-muted" }, "Valid"),
              el("div", null, `${fmtDT(r.validFromUtc)} – ${fmtDT(r.validUntilUtc)}`),
              el("div", null, el(StatusPill, { row: r }), " ", el("span", { className: "wf-muted" }, `${r.devicesUsed} of ${r.deviceLimit} devices`))
            )
          ),
          r.controllerError && r.controllerState !== "live" && el("div", { className: "wf-panel-note wf-note--red" }, "Controller: ", r.controllerError),
          r.emailError && el("div", { className: "wf-panel-note wf-note--amber" }, "Email: ", r.emailError),
          d.sources.length > 0 &&
            el(
              "section",
              null,
              el("h3", { className: "wf-h3" }, "Paid for by"),
              el(
                "ul",
                { className: "wf-earlier" },
                d.sources.map((s) =>
                  el(
                    "li",
                    { key: s.number },
                    el("span", null, el("b", { className: "wf-mono" }, s.number), " · ", s.label, el("div", { className: "wf-muted" }, `${fmtDT(s.startUtc)} – ${fmtDT(s.endUtc)}`)),
                    s.cancelled && el(Badge, { tone: "danger" }, "Cancelled")
                  )
                )
              )
            ),
          el(
            "section",
            null,
            el("h3", { className: "wf-h3", style: { marginTop: "var(--space-3)" } }, "Devices"),
            d.devices.length === 0
              ? el("p", { className: "wf-muted" }, "No device has connected yet.")
              : d.devices.map((dev) =>
                  el(
                    "div",
                    { key: dev.deviceId, className: "wf-device" },
                    el(
                      "div",
                      null,
                      el("b", null, dev.deviceName || "Unknown device"),
                      " ",
                      el("span", { className: "wf-mono wf-muted" }, dev.mac),
                      el(
                        "div",
                        { className: "wf-muted" },
                        dev.removedUtc ? `Removed ${fmtDT(dev.removedUtc)} by the ${dev.removedBy}` : dev.online ? `Connected now · since ${fmtT(dev.onlineSinceUtc)}` : `Last seen ${fmtDT(dev.lastSeenUtc)}`
                      )
                    ),
                    canManage && !dev.removedUtc && el(Button, { size: "sm", variant: "outline", onClick: () => removeDevice(dev) }, "Remove")
                  )
                )
          ),
          (r.kind === "booking" || r.kind === "pass") &&
            el(
              "section",
              null,
              el("h3", { className: "wf-h3", style: { marginTop: "var(--space-3)" } }, "Team members"),
              el("p", { className: "wf-sub" }, "On a company booking each named person gets their own password, so you can still see who is online."),
              d.members.length === 0
                ? el("p", { className: "wf-muted" }, "No team members yet.")
                : el(
                    "ul",
                    { className: "wf-earlier" },
                    d.members.map((mm) =>
                      el(
                        "li",
                        { key: mm.accessId },
                        el("span", null, el("b", null, mm.holderName), " · ", el("span", { className: "wf-mono" }, mm.username), el("div", { className: "wf-muted" }, [mm.email, mm.mobile].filter(Boolean).join(" · "), ` · ${mm.devicesUsed} of ${mm.deviceLimit} devices`)),
                        el(StatusPill, { row: mm })
                      )
                    )
                  ),
              canManage &&
                r.status !== "revoked" &&
                r.status !== "expired" &&
                (m.open
                  ? el(
                      "div",
                      { className: "wf-panel-note wf-note--info", style: { marginTop: "var(--space-3)" } },
                      el(
                        "div",
                        { className: "wf-grid2" },
                        el(Input, { label: "Name", required: true, value: m.name, maxLength: 150, onChange: (e) => setM({ ...m, name: e.target.value }) }),
                        el(Input, { label: "Email", type: "email", value: m.email, maxLength: 256, onChange: (e) => setM({ ...m, email: e.target.value }) }),
                        el(Input, { label: "Mobile", value: m.mobile, maxLength: 20, onChange: (e) => setM({ ...m, mobile: e.target.value }) })
                      ),
                      el(
                        "div",
                        { style: { display: "flex", gap: 8, marginTop: 8 } },
                        el(Button, { size: "sm", variant: "primary", loading: busy, disabled: !m.name.trim() || (!m.email.trim() && !m.mobile.trim()), onClick: addMemberSave }, "Add and send their details"),
                        el(Button, { size: "sm", variant: "ghost", onClick: () => setM({ ...m, open: false }) }, "Cancel")
                      )
                    )
                  : el(Button, { size: "sm", variant: "outline", onClick: () => setM({ ...m, open: true }) }, "+ Add a team member"))
            ),
          el(
            "section",
            null,
            el("h3", { className: "wf-h3", style: { marginTop: "var(--space-4)" } }, "Access log"),
            el(
              "ul",
              { className: "wf-timeline" },
              d.events.map((e) => el("li", { key: e.eventId }, el("div", null, e.detail), el("div", { className: "wf-muted" }, `${fmtDT(e.createdUtc)} · ${e.actor}`)))
            )
          )
        )
  );
}

function GuestDialog({ onClose, onSaved }) {
  const toast = useToast();
  const [f, setF] = React.useState({ name: "", email: "", mobile: "", until: toIstInput(new Date(Date.now() + 4 * 3600e3).toISOString()), limit: "2", note: "" });
  const [busy, setBusy] = React.useState(false);
  const save = async () => {
    setBusy(true);
    try {
      const r = await unwrap(
        http.post("/admin/wifi/access", { name: f.name, email: f.email || null, mobile: f.mobile || null, validUntilUtc: fromIstInput(f.until), deviceLimit: Number(f.limit) || null, note: f.note || null })
      );
      toast.success(`Guest access ${r.accessNo} issued: ${r.username}.` + (r.email ? " The details were emailed." : ""));
      onSaved(r);
    } catch (e) {
      toast.error(errMsg(e, "Could not issue guest access."));
    } finally {
      setBusy(false);
    }
  };
  return el(
    Modal,
    {
      open: true,
      title: "Issue guest access",
      onClose: () => busy || onClose(),
      footer: el(
        F,
        null,
        el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"),
        el(Button, { variant: "primary", loading: busy, disabled: !f.name.trim() || (!f.email.trim() && !f.mobile.trim()) || !f.until, onClick: save }, "Issue access")
      )
    },
    el("p", { className: "wf-sub" }, "For a visitor, an event or an interview. The guest gets their own password on the guest network, which stops working at the end time."),
    el(
      "div",
      { className: "wf-grid2" },
      el(Input, { label: "Guest name", required: true, value: f.name, maxLength: 150, onChange: (e) => setF({ ...f, name: e.target.value }) }),
      el(Input, { label: "Email", type: "email", value: f.email, maxLength: 256, hint: "The details are emailed here.", onChange: (e) => setF({ ...f, email: e.target.value }) }),
      el(Input, { label: "Mobile", value: f.mobile, maxLength: 20, hint: "For Send by WhatsApp.", onChange: (e) => setF({ ...f, mobile: e.target.value }) }),
      el(Input, { label: "Works until", type: "datetime-local", required: true, value: f.until, onChange: (e) => setF({ ...f, until: e.target.value }) }),
      el(Input, { label: "Devices", type: "number", min: 1, max: 10, value: f.limit, onChange: (e) => setF({ ...f, limit: e.target.value }) }),
      el(Input, { label: "Note (for the access log)", value: f.note, maxLength: 500, placeholder: "Interview with …", onChange: (e) => setF({ ...f, note: e.target.value }) })
    )
  );
}

/* ---- Usage log ---------------------------------------------------------------------------------- */

const RESULTS = [
  ["", "All"],
  ["connected", "Connected (named)"],
  ["unknown", "Connected (no customer matched)"],
  ["refused", "Refused"]
];

function UsageTab() {
  const toast = useToast();
  const [f, setF] = React.useState({ from: istDate(-7), to: istDate(0), search: "", result: "" });
  const [term, setTerm] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  React.useEffect(() => {
    const t = setTimeout(() => setTerm(f.search), 300);
    return () => clearTimeout(t);
  }, [f.search]);
  const params = { fromUtc: dayStartIso(f.from), toUtc: dayEndIso(f.to), search: term || void 0, result: f.result || void 0 };
  React.useEffect(() => {
    let live = true;
    unwrap(http.get("/admin/wifi/sessions", { params: { ...params, page, pageSize: 50 } }))
      .then((d) => live && setData(d))
      .catch((e) => toast.error(errMsg(e, "Could not load the usage log.")));
    const ex = () => download("/admin/wifi/sessions/export", params, "wifi_usage.xlsx").catch(() => toast.error("Export failed."));
    window.addEventListener("rg-wifi-export", ex);
    return () => {
      live = false;
      window.removeEventListener("rg-wifi-export", ex);
    };
  }, [f.from, f.to, term, f.result, page]);
  return el(
    F,
    null,
    el("p", { className: "wf-sub" }, "Who connected when, over any date range — for a dispute, an audit or a capacity question. Records are kept for the period set in Settings; websites visited are never recorded."),
    el(
      "div",
      { className: "wf-filters" },
      el(Input, { label: "Search", placeholder: "Customer, device, MAC, IP or username", value: f.search, onChange: (e) => (setF({ ...f, search: e.target.value }), setPage(1)) }),
      el(Input, { label: "From", type: "date", value: f.from, onChange: (e) => (setF({ ...f, from: e.target.value }), setPage(1)) }),
      el(Input, { label: "To", type: "date", value: f.to, onChange: (e) => (setF({ ...f, to: e.target.value }), setPage(1)) }),
      el(Select, { label: "Result", value: f.result, options: RESULTS.map(([v, l]) => ({ value: v, label: l })), onChange: (e) => (setF({ ...f, result: e.target.value }), setPage(1)) })
    ),
    !data
      ? el(Spinner, { center: true })
      : data.items.length === 0
        ? el(EmptyState, { title: "Nothing in this range", description: "Connections and refused attempts appear here." })
        : el(
            "div",
            { className: "wf-scroll" },
            el(
              "table",
              { className: "wf-table wf-table--cards" },
              el("thead", null, el("tr", null, ["Started", "Customer", "Device", "Network / access point", "Duration", "Data", "Result"].map((h) => el("th", { key: h }, h)))),
              el(
                "tbody",
                null,
                data.items.map((s) =>
                  el(
                    "tr",
                    { key: s.sessionId },
                    el("td", null, fmtDT(s.startedUtc), s.endedUtc && !sameDay(s.startedUtc, s.endedUtc) && el("div", { className: "wf-muted" }, "to " + fmtDT(s.endedUtc))),
                    el("td", null, s.holderName ? el(F, null, el("div", { className: "wf-name" }, s.holderName), el("div", { className: "wf-muted wf-mono" }, s.accessNo)) : el("span", { className: "wf-unknown" }, "No customer matched")),
                    el("td", null, el("div", null, s.deviceName || "Unknown"), el("div", { className: "wf-muted wf-mono" }, s.mac)),
                    el("td", { "data-hide": "" }, el("div", null, s.ssid || "—"), el("div", { className: "wf-muted" }, s.accessPoint || "")),
                    el("td", null, s.result.startsWith("refused") ? "—" : s.minutes < 60 ? `${s.minutes} m` : `${Math.floor(s.minutes / 60)} h ${s.minutes % 60} m`),
                    el("td", { "data-hide": "" }, s.result.startsWith("refused") ? "—" : bytes(s.bytes)),
                    el("td", null, s.result.startsWith("refused") ? el("span", { className: "wf-red" }, s.result) : s.result)
                  )
                )
              )
            )
          ),
    data && el("div", { className: "wf-foot" }, el("span", { className: "wf-muted" }, `${data.items.length} of ${data.totalCount} records`), el(Pager, { page: data, onPage: setPage }))
  );
}

/* ---- Settings (spec 5.3) ------------------------------------------------------------------------- */

const CONTROLLERS = [
  ["simulated", "Simulated (no equipment yet)"],
  ["unifi", "UniFi (Ubiquiti) — adapter not installed yet"],
  ["omada", "TP-Link Omada — adapter not installed yet"],
  ["radius", "RADIUS — adapter not installed yet"],
  ["mikrotik", "Mikrotik — adapter not installed yet"]
];
const MODES = [
  [
    "per_customer",
    "One password per customer",
    "Each booking gets its own password on the same network. The controller reports which password a device used, so every device has a name against it. Recommended."
  ],
  ["portal", "Login page with username and password", "The customer signs in on a captive portal. Strongest record of who connected, but needs a RADIUS server and the customer must sign in on each device."],
  [
    "shared",
    "One shared password for everyone",
    "Simplest to set up, but the admin screen can then only show unnamed devices — never who they belong to — and the password cannot be withdrawn from one person."
  ]
];

function SettingsTab({ canManage }) {
  const toast = useToast();
  const [s, setS] = React.useState(null);
  const [busy, setBusy] = React.useState(null);
  const [test, setTest] = React.useState(null);
  const load = React.useCallback(async () => {
    try {
      setS(await unwrap(http.get("/admin/wifi/settings")));
    } catch (e) {
      toast.error(errMsg(e, "Could not load the Wi-Fi settings."));
    }
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  if (!s) return el(Spinner, { center: true });
  const set = (p) => setS({ ...s, ...p });
  const save = async (extra) => {
    setBusy("save");
    try {
      setS(await unwrap(http.put("/admin/wifi/settings", { ...s, ...(extra || {}) })));
      toast.success("Wi-Fi settings saved.");
    } catch (e) {
      toast.error(errMsg(e, "Could not save the settings."));
    } finally {
      setBusy(null);
    }
  };
  const runTest = async () => {
    setBusy("test");
    try {
      setTest(await unwrap(http.post("/admin/wifi/settings/test", {})));
    } catch (e) {
      toast.error(errMsg(e, "The test did not run."));
    } finally {
      setBusy(null);
    }
  };
  const ro = !canManage;
  const customerNets = s.networks.filter((n) => !n.internal);
  const setNet = (i, p) => set({ networks: s.networks.map((n, j) => (j === i ? { ...n, ...p } : n)) });
  const setProfile = (key, p) => set({ profiles: s.profiles.map((x) => (x.key === key ? { ...x, ...p } : x)) });
  const usedBy = (ssid) => s.profiles.filter((p) => p.ssid === ssid).length > 0;
  const connected = test ? test.connected : s.connected;
  return el(
    "div",
    { className: "wf-settings" },
    el(
      "div",
      null,
      el(
        "section",
        { className: "wf-panel" },
        el("h3", { className: "wf-h3" }, "Wi-Fi controller"),
        el("p", { className: "wf-sub" }, "The app reads who is connected from here, and writes new access into it."),
        el(
          "div",
          { className: "wf-grid2" },
          el(Select, { label: "Controller type", value: s.controllerType, disabled: ro, options: CONTROLLERS.map(([v, l]) => ({ value: v, label: l })), onChange: (e) => set({ controllerType: e.target.value }) }),
          el(Input, { label: "Controller address", value: s.controllerUrl || "", disabled: ro, placeholder: "https://unifi.regalis.local:8443", onChange: (e) => set({ controllerUrl: e.target.value }) }),
          el(Input, { label: "API user", value: s.controllerUser || "", disabled: ro, onChange: (e) => set({ controllerUser: e.target.value }) }),
          el(Input, {
            label: "API key",
            type: "password",
            autoComplete: "new-password",
            value: s.controllerKey || "",
            disabled: ro,
            placeholder: s.hasControllerKey ? "•••••••••• (saved; type to replace)" : "",
            hint: "Stored encrypted. Reached on the local network only.",
            onChange: (e) => set({ controllerKey: e.target.value })
          })
        ),
        s.controllerType !== "simulated" &&
          el("div", { className: "wf-panel-note wf-note--amber", style: { marginTop: 10 } }, "There is no adapter for this controller in this build yet. Confirm the Wi-Fi hardware (open item 1); until its adapter is added, keep “Simulated”."),
        el(
          "div",
          { className: "wf-foot" },
          el(
            "span",
            null,
            el(Badge, { tone: connected ? "success" : "danger" }, el("span", { className: "wf-dot" + (connected ? "" : " wf-dot--off") }), connected ? "Connected" : "Not connected"),
            " ",
            el(
              "span",
              { className: "wf-muted" },
              s.lastPollUtc ? `Last sync ${fmtT(s.lastPollUtc)}` : "Not synced yet",
              s.accessPoints != null ? ` · ${s.accessPoints} access points` : "",
              ` · ${s.clients ?? 0} clients`
            )
          ),
          el(Button, { variant: "outline", loading: busy === "test", disabled: ro, onClick: runTest }, "Test connection")
        ),
        test && el("div", { className: "wf-panel-note " + (test.connected ? "wf-note--info" : "wf-note--red"), style: { marginTop: 10 } }, test.message),
        !s.connected && s.controllerMessage && !test && el("div", { className: "wf-panel-note wf-note--red", style: { marginTop: 10 } }, s.controllerMessage)
      ),
      el(
        "section",
        { className: "wf-panel" },
        el("h3", { className: "wf-h3" }, "Networks"),
        el("p", { className: "wf-sub" }, "The network names (SSIDs) on the equipment, the speed limit per person on each, and which kind of booking is given which."),
        s.networks.map((n, i) =>
          el(
            "div",
            { key: i, className: "wf-net" },
            el(Input, { label: "Network name", value: n.ssid, disabled: ro, maxLength: 32, onChange: (e) => setNet(i, { ssid: e.target.value }) }),
            el(Input, { label: "Mbps each", type: "number", min: 0, value: String(n.speedMbps), disabled: ro, onChange: (e) => setNet(i, { speedMbps: Number(e.target.value) || 0 }) }),
            el(Input, { label: "Used for", value: n.purpose, disabled: ro, maxLength: 200, onChange: (e) => setNet(i, { purpose: e.target.value }) }),
            el(Checkbox, { label: "Internal (staff only)", checked: n.internal, disabled: ro, onChange: (e) => setNet(i, { internal: e.target.checked }) }),
            el(Badge, { tone: n.internal ? "neutral" : usedBy(n.ssid) ? "success" : "neutral" }, n.internal ? "Internal" : usedBy(n.ssid) ? "In use" : "Not used")
          )
        ),
        !ro && el(Button, { size: "sm", variant: "outline", onClick: () => set({ networks: [...s.networks, { ssid: "", speedMbps: 20, purpose: "", internal: false }] }) }, "+ Add a network"),
        el("h3", { className: "wf-h3", style: { marginTop: "var(--space-4)" } }, "Which network each kind of booking gets"),
        el(
          "div",
          { className: "wf-scroll" },
          el(
            "table",
            { className: "wf-table" },
            el("thead", null, el("tr", null, el("th", null, "Booking"), el("th", null, "Network"), el("th", null, "Devices"))),
            el(
              "tbody",
              null,
              s.profiles.map((p) =>
                el(
                  "tr",
                  { key: p.key },
                  el("td", null, p.label),
                  el(
                    "td",
                    { style: { minWidth: 170 } },
                    el(Select, {
                      "aria-label": "Network for " + p.label,
                      value: p.ssid,
                      disabled: ro,
                      options: [{ value: "", label: "No Wi-Fi" }, ...customerNets.map((n) => ({ value: n.ssid, label: n.ssid }))],
                      onChange: (e) => setProfile(p.key, { ssid: e.target.value, deviceLimit: e.target.value && !p.deviceLimit ? 2 : p.deviceLimit })
                    })
                  ),
                  el(
                    "td",
                    { style: { width: 110 } },
                    el(Input, { "aria-label": "Devices for " + p.label, type: "number", min: 1, max: 20, value: p.ssid ? String(p.deviceLimit) : "", disabled: ro || !p.ssid, onChange: (e) => setProfile(p.key, { deviceLimit: Number(e.target.value) || 1 }) })
                  )
                )
              )
            )
          )
        )
      ),
      el(
        "section",
        { className: "wf-panel" },
        el("h3", { className: "wf-h3" }, "Delivery to the customer"),
        el(Checkbox, { label: "Email, as soon as the payment succeeds", checked: s.deliverEmail, disabled: ro, onChange: (e) => set({ deliverEmail: e.target.checked }) }),
        el(Checkbox, { label: "In the customer app and portal, under My Wi-Fi (always on)", checked: true, disabled: true, onChange: () => {} }),
        el(Checkbox, {
          label: "WhatsApp: a “Send by WhatsApp” button (click-to-chat), shown in red when the email fails",
          checked: s.deliverWhatsApp,
          disabled: ro,
          onChange: (e) => set({ deliverWhatsApp: e.target.checked })
        }),
        el(Checkbox, { label: "Include a QR code so the customer joins without typing", checked: s.deliverQr, disabled: ro, onChange: (e) => set({ deliverQr: e.target.checked }) }),
        el(Checkbox, { label: "Remind the customer 1 day before the access expires (08:00 IST)", checked: s.reminder, disabled: ro, onChange: (e) => set({ reminder: e.target.checked }) }),
        el(
          "p",
          { className: "wf-muted", style: { marginTop: 8 } },
          "WhatsApp messages open in the sender's own WhatsApp with the text ready (there is no WhatsApp sending service yet — open item 7), so whoever presses the button can see the message."
        )
      )
    ),
    el(
      "div",
      null,
      el(
        "section",
        { className: "wf-panel" },
        el("h3", { className: "wf-h3" }, "How access is created"),
        el("p", { className: "wf-sub" }, "This choice decides whether the admin can see who is online."),
        MODES.map(([v, title, text]) =>
          el(
            "label",
            { key: v, className: "wf-option" + (s.mode === v ? " wf-option--on" : "") },
            el("input", { type: "radio", name: "wf-mode", value: v, checked: s.mode === v, disabled: ro, onChange: () => set({ mode: v }) }),
            el("span", null, el("b", null, title), el("div", { className: "wf-muted", style: { fontSize: "var(--font-size-sm)" } }, text))
          )
        ),
        s.mode === "shared" &&
          el(Input, {
            label: "The shared password",
            type: "password",
            autoComplete: "new-password",
            value: s.sharedPassword || "",
            disabled: ro,
            placeholder: s.hasSharedPassword ? "•••••••••• (saved; type to replace)" : "8 to 63 characters",
            hint: "Everyone is sent this one. Stored encrypted.",
            onChange: (e) => set({ sharedPassword: e.target.value })
          })
      ),
      el(
        "section",
        { className: "wf-panel" },
        el("h3", { className: "wf-h3" }, "Rules"),
        el(
          "div",
          { className: "wf-grid2" },
          el(Select, {
            label: "Day pass valid until",
            value: s.dayPassUntil,
            disabled: ro,
            options: [
              { value: "closing", label: "Closing time that day" },
              { value: "24h", label: "24 hours from first use" }
            ],
            onChange: (e) => set({ dayPassUntil: e.target.value })
          }),
          el(Select, { label: "Membership valid until", value: "paid", disabled: true, options: [{ value: "paid", label: "End of the paid period" }], onChange: () => {} }),
          el(Select, {
            label: "Disconnect idle devices after",
            value: String(s.idleMinutes),
            disabled: ro,
            options: [
              ["0", "Never"],
              ["15", "15 minutes"],
              ["30", "30 minutes"],
              ["60", "1 hour"],
              ["120", "2 hours"]
            ].map(([v, l]) => ({ value: v, label: l })),
            hint: "Applied by the Wi-Fi controller.",
            onChange: (e) => set({ idleMinutes: Number(e.target.value) })
          }),
          el(Input, {
            label: "Pause after an invoice is overdue by (days)",
            type: "number",
            min: 1,
            max: 90,
            value: s.overdueDays ? String(s.overdueDays) : "7",
            disabled: ro || !s.overdueDays,
            onChange: (e) => set({ overdueDays: Math.max(1, Number(e.target.value) || 7) })
          })
        ),
        el(Checkbox, { label: "Cut access the moment a booking is cancelled or a membership ends", checked: s.ruleCancel, disabled: ro, onChange: (e) => set({ ruleCancel: e.target.checked }) }),
        el(Checkbox, {
          label: `Pause access when an invoice is ${s.overdueDays || 7} days overdue (paying restores it, same password)`,
          checked: s.overdueDays > 0,
          disabled: ro,
          onChange: (e) => set({ overdueDays: e.target.checked ? 7 : 0 })
        }),
        el(Checkbox, { label: "Warn the admin when a device connects that matches no customer", checked: s.ruleUnknownDevice, disabled: ro, onChange: (e) => set({ ruleUnknownDevice: e.target.checked }) }),
        el(Checkbox, { label: "Issue access automatically when a booking or pass is paid", checked: s.autoIssue, disabled: ro, onChange: (e) => set({ autoIssue: e.target.checked }) }),
        el(Checkbox, {
          label: "Also give Wi-Fi to pay-later bookings before they are paid",
          checked: s.payLater,
          disabled: ro,
          onChange: (e) => set({ payLater: e.target.checked })
        }),
        s.issueSinceUtc &&
          el(
            "p",
            { className: "wf-muted", style: { marginTop: 8 } },
            `Automatic access covers bookings and passes paid since ${fmtDT(s.issueSinceUtc)}. `,
            !ro &&
              el(
                "button",
                { type: "button", className: "btn btn--ghost btn--sm", onClick: () => save({ issueSinceUtc: new Date(Date.now() - 62 * 864e5).toISOString() }) },
                "Include bookings already paid (still running)"
              )
          )
      ),
      el(
        "section",
        { className: "wf-rules" },
        el("h3", { className: "wf-h3" }, "What is kept, and for how long"),
        el(
          "p",
          { style: { margin: "6px 0" } },
          `Connection records (customer, device, times, access point, data used) are kept for ${s.retentionMonths} months and then deleted. Regalis never records which websites anyone visits or anything inside their traffic. Passwords are stored encrypted and are shown to the customer only, never in the admin list; an admin can reset one but never read it.`
        ),
        el(Input, { label: "Keep connection records for (months)", type: "number", min: 1, max: 120, value: String(s.retentionMonths), disabled: ro, onChange: (e) => set({ retentionMonths: Number(e.target.value) || 12 }) })
      ),
      canManage && el("div", { style: { display: "flex", justifyContent: "flex-end", margin: "var(--space-3) 0" } }, el(Button, { variant: "primary", loading: busy === "save", onClick: () => save() }, "Save settings")),
      canManage && s.controllerType === "simulated" && el(SimulatorPanel, null)
    )
  );
}

function SimulatorPanel() {
  const toast = useToast();
  const [accesses, setAccesses] = React.useState([]);
  const [f, setF] = React.useState({ accessId: "", how: "right", deviceName: "iPhone 14", mac: "", ap: "", ssid: "Regalis-Guest", leaveMac: "" });
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => {
    unwrap(http.get("/admin/wifi/access", { params: { period: "all", pageSize: 200 } }))
      .then((d) => setAccesses(d.page.items))
      .catch(() => {});
  }, []);
  const join = async () => {
    setBusy(true);
    try {
      const r = await unwrap(
        http.post("/admin/wifi/simulator/join", { accessId: f.accessId ? Number(f.accessId) : null, how: f.how, deviceName: f.deviceName, mac: f.mac || null, accessPoint: f.ap || null, ssid: f.ssid })
      );
      const text = { connected: "joined", "refused-password": "was refused (wrong password)", "refused-expired": "was refused (access ended)", blocked: "is blocked" }[r.outcome] || r.outcome;
      toast.success(`${f.deviceName} (${r.mac}) ${text}. Connected now is updated.`);
      setF((x) => ({ ...x, leaveMac: r.mac, mac: "" }));
    } catch (e) {
      toast.error(errMsg(e, "Could not simulate the device."));
    } finally {
      setBusy(false);
    }
  };
  const leave = async () => {
    try {
      const r = await unwrap(http.post("/admin/wifi/simulator/leave", { mac: f.leaveMac }));
      toast.success(`${r.mac}: ${r.outcome}.`);
    } catch (e) {
      toast.error(errMsg(e, "Could not do that."));
    }
  };
  const usable = accesses.filter((a) => a.status !== "revoked");
  return el(
    "section",
    { className: "wf-panel" },
    el("h3", { className: "wf-h3" }, "Simulate a device"),
    el(
      "p",
      { className: "wf-sub" },
      "Until the Wi-Fi equipment is confirmed, try the whole flow here: a device joins with a customer's password and appears on Connected now with their name; a wrong password or a full device limit is refused, as on a real controller."
    ),
    el(
      "div",
      { className: "wf-grid2" },
      el(Select, {
        label: "How it joins",
        value: f.how,
        options: [
          { value: "right", label: "With this access's password" },
          { value: "wrong", label: "With a mistyped password" },
          { value: "shared", label: "With a password that belongs to nobody" }
        ],
        onChange: (e) => setF({ ...f, how: e.target.value })
      }),
      f.how === "shared"
        ? el(Select, { label: "Network", value: f.ssid, options: ["Regalis-Members", "Regalis-Guest"].map((v) => ({ value: v, label: v })), onChange: (e) => setF({ ...f, ssid: e.target.value }) })
        : el(Select, {
            label: "Access",
            placeholder: "Choose…",
            value: f.accessId,
            options: usable.map((a) => ({ value: String(a.accessId), label: `${a.holderName} · ${a.username} (${a.statusLabel})` })),
            onChange: (e) => setF({ ...f, accessId: e.target.value })
          }),
      el(Input, { label: "Device name", value: f.deviceName, maxLength: 120, onChange: (e) => setF({ ...f, deviceName: e.target.value }) }),
      el(Input, { label: "MAC address", value: f.mac, placeholder: "blank = a new random one", onChange: (e) => setF({ ...f, mac: e.target.value }) }),
      el(Select, {
        label: "Access point",
        value: f.ap,
        options: ["", "Floor 1 · AP-01", "Floor 1 · AP-02", "Floor 2 · AP-04", "Board Room · AP-07"].map((v) => ({ value: v, label: v || "Any" })),
        onChange: (e) => setF({ ...f, ap: e.target.value })
      })
    ),
    el(
      "div",
      { style: { display: "flex", gap: 8, flexWrap: "wrap", marginTop: 10, alignItems: "end" } },
      el(Button, { variant: "outline", loading: busy, disabled: f.how !== "shared" && !f.accessId, onClick: join }, "Join the Wi-Fi"),
      el("div", { style: { flex: 1, minWidth: 180 } }, el(Input, { label: "Device to take off (MAC)", value: f.leaveMac, onChange: (e) => setF({ ...f, leaveMac: e.target.value }) })),
      el(Button, { variant: "ghost", disabled: !f.leaveMac, onClick: leave }, "Leave")
    )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* Customer: My Wi-Fi (spec 5.4, 5.5)                                                          */
/* ---------------------------------------------------------------------------------------- */

export function WifiMyPage() {
  useCss();
  const toast = useToast();
  const [d, setD] = React.useState(null);
  const [help, setHelp] = React.useState(false);
  const [remove, setRemove] = React.useState(null);
  const load = React.useCallback(async () => {
    try {
      setD(await unwrap(http.get("/portal/wifi")));
    } catch (e) {
      toast.error(errMsg(e, "Could not load your Wi-Fi."));
      setD({ current: [], earlier: [], whatsAppEnabled: false, memberDevices: 3, passDevices: 2, recordedNotice: "" });
    }
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  if (!d) return el(Spinner, { center: true });
  const main = d.current[0];
  const resend = async (channel) => {
    try {
      const r = await unwrap(http.post("/portal/wifi/resend", { channel }));
      if (r.url) openWhatsApp(r.url);
      r.ok ? toast.success(r.message) : toast.error(r.message);
    } catch (e) {
      toast.error(errMsg(e, "Could not send it just now."));
    }
  };
  const doRemove = async () => {
    try {
      await unwrap(http.delete(`/portal/wifi/devices/${remove.deviceId}`));
      toast.success("Device removed. Forget the network on that device too, or it will join again when in range.");
      setRemove(null);
      load();
    } catch (e) {
      toast.error(errMsg(e, "Could not remove the device."));
    }
  };
  const limit = main ? main.deviceLimit : d.memberDevices;
  return el(
    F,
    null,
    el(PageHeader, {
      title: "My Wi-Fi",
      description: main
        ? `Your own network name and password. They work for the length of your booking, on up to ${limit} devices.`
        : "Your own Wi-Fi password arrives here the moment a booking or pass is paid."
    }),
    el(
      "div",
      { className: "wf-my" },
      el(
        "div",
        null,
        d.current.length === 0
          ? el("div", { className: "wf-panel" }, el(EmptyState, { title: "No Wi-Fi access right now", description: "When a booking or pass is paid, your network name, password and QR code appear here, and are emailed to you." }))
          : d.current.map((a) => el(AccessCard, { key: a.accessId, a, whatsApp: d.whatsAppEnabled, onResend: resend, onHelp: () => setHelp(true) }))
      ),
      el(
        "div",
        null,
        main &&
          el(
            "section",
            { className: "wf-panel", style: { marginBottom: "var(--space-4)" } },
            el("h3", { className: "wf-h3", style: { marginBottom: 12 } }, "Your devices"),
            d.current.flatMap((a) =>
              a.devices.map((dev) =>
                el(
                  "div",
                  { key: dev.deviceId, className: "wf-device" },
                  el(
                    "div",
                    null,
                    el("b", null, dev.deviceName || "Unknown device"),
                    el("div", { className: "wf-muted" }, dev.online ? `Connected now · since ${fmtT(dev.onlineSinceUtc)}` : `Joined ${fmtD(dev.firstSeenUtc)} · last seen ${fmtDT(dev.lastSeenUtc)}`)
                  ),
                  el(Button, { size: "sm", variant: "outline", onClick: () => setRemove(dev) }, "Remove")
                )
              )
            ),
            el(
              "div",
              { className: "wf-slot" },
              main.devices.length >= main.deviceLimit
                ? "All device slots are in use: remove one to add another"
                : `${main.deviceLimit - main.devices.length} device slot${main.deviceLimit - main.devices.length === 1 ? "" : "s"} free`
            )
          ),
        d.earlier.length > 0 &&
          el(
            "section",
            { className: "wf-panel", style: { marginBottom: "var(--space-4)" } },
            el("h3", { className: "wf-h3" }, "Earlier access"),
            el(
              "ul",
              { className: "wf-earlier" },
              d.earlier.map((h) =>
                el("li", { key: h.accessId }, el("span", null, el("div", null, h.title), el("div", { className: "wf-muted" }, h.ssid)), el(Badge, { tone: "neutral" }, h.statusLabel))
              )
            )
          ),
        el(
          "section",
          { className: "wf-rules" },
          el("h3", { className: "wf-h3" }, "House rules"),
          el(
            "p",
            { style: { margin: "6px 0 0" } },
            `Up to ${main ? main.deviceLimit : `${d.memberDevices} devices for members and ${d.passDevices} for a day pass`}${main ? " devices" : ""}. The password stops working when your booking ends, and starts again the moment you renew. If you think someone else has your password, use “Having trouble connecting?” and we will issue a new one.`
          ),
          d.recordedNotice && el("p", { className: "wf-muted", style: { margin: "8px 0 0" } }, d.recordedNotice)
        )
      )
    ),
    help && el(HelpDialog, { onClose: () => setHelp(false) }),
    remove &&
      el(ConfirmDialog, {
        title: "Remove this device?",
        text: `${remove.deviceName || "This device"} is signed off the Wi-Fi and its slot is freed. Forget the network on the device too, or it will join again next time it is in range.`,
        okLabel: "Remove device",
        onClose: () => setRemove(null),
        onOk: doRemove
      })
  );
}

function AccessCard({ a, whatsApp, onResend, onHelp }) {
  const toast = useToast();
  const [hide, setHide] = React.useState(false);
  const copy = async (text, what) => ((await copyText(text)) ? toast.success(`${what} copied.`) : toast.error("Could not copy. Select it and copy by hand."));
  const tone = TONE[a.status] || "neutral";
  return el(
    "section",
    { className: "wf-panel", style: { marginBottom: "var(--space-4)" } },
    el("div", { style: { display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center" } }, el("h3", { className: "wf-h3" }, a.title), el(Badge, { tone }, a.statusLabel)),
    !a.ready
      ? el(
          F,
          null,
          el("div", { className: "wf-panel-note " + (a.status === "paused" ? "wf-note--amber" : "wf-note--info"), style: { marginTop: 12 } }, a.notice),
          el("div", { className: "wf-facts" }, el("div", null, el("span", { className: "wf-muted" }, "Network"), el("b", { className: "wf-mono" }, a.ssid)), el("div", null, el("span", { className: "wf-muted" }, "Valid"), el("b", null, `${fmtD(a.validFromUtc)} – ${fmtD(a.validUntilUtc)}`)))
        )
      : el(
          F,
          null,
          el(
            "div",
            { className: "wf-access" },
            el(
              "div",
              null,
              el("div", { className: "wf-field-label" }, "Network name"),
              el("div", { className: "wf-box" }, el("span", { className: "wf-mono" }, a.ssid), el(Button, { size: "sm", variant: "outline", onClick: () => copy(a.ssid, "Network name") }, "Copy")),
              a.mode === "portal" &&
                el(
                  F,
                  null,
                  el("div", { className: "wf-field-label" }, "Username (on the Wi-Fi login page)"),
                  el("div", { className: "wf-box" }, el("span", { className: "wf-mono" }, a.username), el(Button, { size: "sm", variant: "outline", onClick: () => copy(a.username, "Username") }, "Copy"))
                ),
              el("div", { className: "wf-field-label" }, "Your password"),
              el(
                "div",
                { className: "wf-box wf-box--pass" },
                el("span", { className: "wf-mono", "aria-label": hide ? "Password hidden" : "Password" }, hide ? "•••• - •••• - ••••" : a.password),
                el(Button, { size: "sm", variant: "outline", onClick: () => setHide((h) => !h) }, hide ? "Show" : "Hide"),
                el(Button, { size: "sm", variant: "primary", onClick: () => copy(a.password, "Password") }, "Copy")
              ),
              el(
                "p",
                { className: "wf-muted", style: { fontSize: "var(--font-size-sm)", marginTop: 8 } },
                a.mode === "shared"
                  ? "This is the Wi-Fi password for the building."
                  : "This password is yours alone. Please do not share it — your bookings and your devices are tied to it."
              ),
              el(
                "div",
                { className: "wf-facts" },
                el(
                  "div",
                  null,
                  el("span", { className: "wf-muted" }, "Valid until"),
                  el("b", null, fmtLong(a.validUntilUtc)),
                  el("span", { className: "wf-muted" }, left(a.validUntilUtc) || (a.renews ? "renews when you renew" : ""))
                ),
                el(
                  "div",
                  null,
                  el("span", { className: "wf-muted" }, "Devices"),
                  el("b", null, `${a.devices.length} of ${a.deviceLimit} used`),
                  el("span", { className: "wf-muted" }, a.devices.map((x) => x.deviceName || "Unknown").join(", "))
                )
              )
            ),
            el(
              "div",
              { className: "wf-qr" },
              el(QrSvg, { text: a.qrText }),
              el("p", null, "Point your phone camera at this to join without typing the password.")
            )
          ),
          el(
            "details",
            { style: { marginTop: "var(--space-3)" } },
            el("summary", { style: { cursor: "pointer", fontWeight: 700 } }, "How to connect"),
            el(
              "ol",
              { className: "wf-steps" },
              el("li", null, "Open Wi-Fi settings on your phone or laptop"),
              el("li", null, "Pick ", el("b", null, a.ssid)),
              el("li", null, a.mode === "portal" ? "Sign in on the page that opens with your username and password" : "Paste the password above")
            )
          ),
          el(
            "div",
            { className: "wf-actions" },
            el(Button, { variant: "outline", onClick: () => onResend("email") }, "Email this to me again"),
            whatsApp && el(Button, { variant: "outline", onClick: () => onResend("whatsapp") }, "Send on WhatsApp"),
            el(Button, { variant: "outline", onClick: onHelp }, "Having trouble connecting?")
          )
        ),
    !a.ready && el("div", { className: "wf-actions" }, el(Button, { variant: "outline", onClick: onHelp }, "Having trouble connecting?"))
  );
}

function HelpDialog({ onClose }) {
  const toast = useToast();
  const [msg, setMsg] = React.useState("");
  const [shared, setShared] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const send = async () => {
    setBusy(true);
    try {
      const r = await unwrap(http.post("/portal/wifi/help", { message: msg || null, passwordShared: shared }));
      toast.success(`Support ticket ${r.ticketNumber} raised. We will get back to you.`);
      onClose();
    } catch (e) {
      toast.error(errMsg(e, "Could not raise the ticket. Please ask at the front desk."));
    } finally {
      setBusy(false);
    }
  };
  return el(
    Modal,
    {
      open: true,
      title: "Having trouble connecting?",
      onClose: () => busy || onClose(),
      footer: el(F, null, el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"), el(Button, { variant: "primary", loading: busy, onClick: send }, "Raise a support ticket"))
    },
    el(
      "p",
      { className: "wf-sub" },
      "Check you picked the right network and typed the password exactly (it is case-sensitive). If a new device will not join, your device slots may be full — remove one under Your devices."
    ),
    el(Textarea, { label: "What happens?", value: msg, maxLength: 2000, placeholder: "My laptop says the password is wrong …", onChange: (e) => setMsg(e.target.value) }),
    el(Checkbox, { label: "I think someone else has my password — please issue a new one", checked: shared, onChange: (e) => setShared(e.target.checked) })
  );
}
