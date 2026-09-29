/**
 * Admin -> Billing & Finance -> Purchase Orders.
 *
 *   AdminPurchaseOrdersPage     the list: search, filters, status-aware row actions
 *   AdminPurchaseOrderDetailPage one PO: workflow actions, totals, attachments, activity
 *   AdminPurchaseOrderFormPage  New / Edit, full page (billing/purchase-orders/new, /:id/edit)
 *
 * Workflow (API: PurchaseOrderService): Draft → Pending approval → Approved → Sent →
 * Partially received → Received, Cancelled until goods arrive. Purchase.RequireApproval
 * off lets an approver approve a draft directly. Only a Draft is editable; once sent the
 * PO is frozen. Every option list (GST rates, TDS, shipment, accounts, terms) comes from
 * GET /purchase-orders/form-options, i.e. Settings → Billing.
 *
 * Money preview (poCompute) mirrors PurchaseOrderPricing.cs exactly, in integer paise,
 * so what the form shows is what the server saves. The server still recomputes.
 *
 * This repo ships the built SPA only, so this chunk is hand-maintained: keep the file
 * name (index-DI5X4c9f.js imports it by name) and bump its revision in sw.js. The
 * receive-goods (ne) and cancel (ie) modals at the end are the original minified code;
 * the single-letter imports are the names they use.
 */
import {
  a as z,
  R as usePermission,
  r as l,
  A as U,
  j as e,
  G as Spinner,
  p as x,
  F as Q,
  I as g,
  z as locationsApi,
  Y as useParams,
  u as useNavigate,
  D as unwrap,
  E as http,
  i as makeIcon
} from "./index-DI5X4c9f.js";
import { p as $, s as supplierApi, e as expenseApi } from "./procurement-D0z-s4Dx.js";
import { i as inventoryApi } from "./operations-BCRENM62.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as G } from "./Modal-BtdVDo1B.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { u as Y } from "./useFieldErrors-C8qSSg0h.js";
import { c as fmtDate, b as fmtMoney, f as fmtDateTime } from "./formatters-DFe5xCRi.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import "./chevron-right-BikENMwC.js";

// ── Status ──────────────────────────────────────────────────────────────────

const PO_STATUS = { 1: "Draft", 6: "Pending approval", 7: "Approved", 2: "Sent", 3: "Partially received", 4: "Received", 5: "Cancelled" };
const PO_TONE = { 1: "neutral", 6: "warning", 7: "info", 2: "gold", 3: "gold", 4: "success", 5: "danger" };
const PO_STATUS_ORDER = [1, 6, 7, 2, 3, 4, 5];
const S = { draft: 1, sent: 2, partial: 3, received: 4, cancelled: 5, pending: 6, approved: 7 };
const StatusBadge = ({ status }) => e.jsx(Badge, { tone: PO_TONE[status] ?? "neutral", children: PO_STATUS[status] ?? "Unknown" });

const BILLING = "/admin/billing/purchase-orders";

// ── API calls the shared procurement helper does not have ───────────────────

const poApi = {
  formOptions: () => unwrap(http.get("/purchase-orders/form-options")),
  submit: (id) => unwrap(http.post(`/purchase-orders/${id}/submit`)),
  approve: (id, comment) => unwrap(http.post(`/purchase-orders/${id}/approve`, { comment: comment || null })),
  reject: (id, comment) => unwrap(http.post(`/purchase-orders/${id}/reject`, { comment })),
  addAttachment: (id, file) => {
    const form = new FormData();
    form.append("file", file, file.name);
    // The shared axios instance defaults to JSON; multipart must be asked for (as SupportPages does).
    return unwrap(http.post(`/purchase-orders/${id}/attachments`, form, { headers: { "Content-Type": "multipart/form-data" } }));
  },
  removeAttachment: (id, attachmentId) => unwrap(http.delete(`/purchase-orders/${id}/attachments/${attachmentId}`)),
  saveBlob: async (url, fileName) => {
    const response = await http.get(url, { responseType: "blob" });
    const href = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = href;
    link.download = fileName;
    link.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  },
  pdf: (order) => poApi.saveBlob(`/purchase-orders/${order.purchaseOrderId}/pdf`, `${order.poNumber.replaceAll("/", "-")}.pdf`),
  attachment: (id, a) => poApi.saveBlob(`/purchase-orders/${id}/attachments/${a.purchaseOrderAttachmentId}`, a.fileName)
};
const errorText = (error, fallback) => (error instanceof U ? error.message : fallback);

// ── Money (mirrors PurchaseOrderPricing.cs) ─────────────────────────────────

/** "12.5" → 1250 at 2 digits; null when not a plain non-negative decimal of that precision. */
function scaled(value, digits) {
  const text = String(value ?? "").trim();
  if (text === "") return 0;
  const hit = /^(\d*)(?:\.(\d*))?$/.exec(text);
  if (!hit || (hit[1] === "" && !hit[2])) return null;
  const fraction = hit[2] ?? "";
  if (fraction.length > digits) return null;
  return Number(hit[1] || "0") * 10 ** digits + Number(fraction.padEnd(digits, "0") || "0");
}
/** Signed variant for the adjustment. */
function signedPaise(value) {
  const text = String(value ?? "").trim();
  if (text.startsWith("-")) {
    const v = scaled(text.slice(1), 2);
    return v === null ? null : -v;
  }
  return scaled(text.replace(/^\+/, ""), 2);
}
/** n / d rounded half away from zero, all integers. */
function divRound(n, d) {
  if (d === 0) return 0;
  const sign = Math.sign(n) * Math.sign(d);
  const a = Math.abs(n), b = Math.abs(d);
  return sign * Math.floor((2 * a + b) / (2 * b));
}

function poCompute(lines, { discountType, discountValue, interState, reverseCharge, tdsPercent, adjustment }) {
  const rows = lines.map((line) => {
    const qty = scaled(line.quantity, 3), rate = scaled(line.rate, 2), gst = scaled(line.taxPercent, 2);
    if (qty === null || rate === null || gst === null) return null;
    return { gross: divRound(qty * rate, 1000), gst };
  });
  const valid = rows.filter(Boolean);
  const baseTotal = valid.reduce((s, r) => s + r.gross, 0);
  const dv = scaled(discountValue, 2) ?? 0;
  let discount = discountType === "1" ? divRound(baseTotal * dv, 10000) : discountType === "2" ? dv : 0;
  const excessDiscount = discount > baseTotal;
  if (excessDiscount) discount = baseTotal;
  const shares = valid.map((r) => (baseTotal === 0 ? 0 : divRound(discount * r.gross, baseTotal)));
  if (shares.length) {
    const largest = valid.reduce((best, r, i) => (r.gross > valid[best].gross ? i : best), 0);
    shares[largest] += discount - shares.reduce((s, v) => s + v, 0);
  }
  let taxable = 0, tax = 0, cgst = 0, sgst = 0, igst = 0;
  const rates = new Set();
  valid.forEach((r, i) => {
    const t = r.gross - shares[i];
    const lineTax = divRound(t * r.gst, 10000);
    taxable += t;
    tax += lineTax;
    if (interState) igst += lineTax;
    else {
      const c = divRound(lineTax, 2);
      cgst += c;
      sgst += lineTax - c;
    }
    if (lineTax > 0 || r.gst > 0) rates.add(r.gst / 100);
  });
  const tds = divRound(taxable * (scaled(tdsPercent, 2) ?? 0), 10000);
  const adj = signedPaise(adjustment) ?? 0;
  const total = taxable + (reverseCharge ? 0 : tax) - tds + adj;
  return {
    lineGross: rows.map((r) => (r ? r.gross : null)),
    subTotal: rows.reduce((s, r) => s + (r ? r.gross : 0), 0),
    discount, excessDiscount, taxable, tax, cgst, sgst, igst, tds, adjustment: adj, total,
    rates: [...rates].sort((a, b) => a - b)
  };
}
const num = new Intl.NumberFormat("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const paise = (p) => num.format(p / 100);
const rupees = (p) => fmtMoney(p / 100);
const todayIst = () => new Date(Date.now() + 5.5 * 3600e3).toISOString().slice(0, 10);
const fileSize = (bytes) => (bytes >= 1048576 ? `${(bytes / 1048576).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`);

// ── GST state (mirrors PurchaseOrderService.IsInterState) ───────────────────

function stateCodeOf(name, states) {
  if (!name) return null;
  const n = String(name).trim().toLowerCase();
  return states.find((s) => s.name.toLowerCase() === n || s.code === n)?.code ?? null;
}
function supplierStateCode(supplier, states) {
  const gstin = supplier?.gstin?.trim();
  if (gstin && /^\d\d/.test(gstin)) return gstin.slice(0, 2);
  return stateCodeOf(supplier?.state, states);
}
const addressOfLocation = (loc) =>
  loc ? [loc.addressLine1, loc.addressLine2, [loc.city, loc.postalCode].filter(Boolean).join(" "), loc.state].map((p) => (p ?? "").trim()).filter(Boolean).join(", ") : "";
const addressOfSupplier = (s) =>
  s ? [s.addressLine, s.city, [s.state, s.pincode].filter(Boolean).join(" ")].map((p) => (p ?? "").trim()).filter(Boolean).join(", ") : "";

// ── Styles ──────────────────────────────────────────────────────────────────

const PO_STYLE_ID = "po-module-styles";
const PO_STYLES = `
.po-toolbar{display:grid;grid-template-columns:minmax(200px,2fr) repeat(3,minmax(140px,1fr)) repeat(2,minmax(130px,.9fr)) auto;gap:var(--space-3);align-items:end;margin-bottom:var(--space-4)}
.po-toolbar .field{min-width:0}
.po-toolbar .field__input{height:38px;padding:8px 10px}
.po-toolbar__clear{align-self:end}
@media(max-width:1200px){.po-toolbar{grid-template-columns:repeat(3,minmax(0,1fr))}}
@media(max-width:640px){.po-toolbar{grid-template-columns:minmax(0,1fr)}}
.po-sub{color:var(--text-muted);font-size:var(--font-size-xs)}
.po-overdue{color:var(--color-danger);font-weight:600}
.po-menu{position:relative;display:inline-block}
.po-menu__list{position:absolute;inset-inline-end:0;top:calc(100% + 4px);z-index:30;min-width:190px;padding:var(--space-1);border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);box-shadow:var(--shadow-lg);display:flex;flex-direction:column}
.po-menu__item{all:unset;box-sizing:border-box;padding:8px 12px;border-radius:var(--radius-sm);font-size:var(--font-size-sm);color:var(--text-primary);cursor:pointer;white-space:nowrap}
.po-menu__item:hover,.po-menu__item:focus-visible{background:var(--surface-hover)}
.po-menu__item[aria-disabled=true]{color:var(--text-muted);cursor:not-allowed}
.po-menu__item--danger{color:var(--color-danger)}
.po-banner{display:flex;gap:var(--space-3);align-items:flex-start;padding:var(--space-3) var(--space-4);border:1px solid rgb(var(--accent-rgb) / 35%);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 8%);font-size:var(--font-size-sm);color:var(--text-secondary)}
.po-banner b{color:var(--text-primary)}
.po-grid3{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-4)}
@media(max-width:900px){.po-grid3{grid-template-columns:minmax(0,1fr)}}
.po-kv{display:flex;flex-direction:column;gap:2px;font-size:var(--font-size-sm)}
.po-kv__label{color:var(--text-muted);font-size:var(--font-size-xs);font-weight:600;text-transform:uppercase;letter-spacing:.04em}
.po-block-title{margin:0 0 var(--space-2);font-size:var(--font-size-xs);font-weight:700;text-transform:uppercase;letter-spacing:.06em;color:var(--regalis-gold-dark)}
[data-theme=dark] .po-block-title{color:var(--accent-bright)}
.po-totals{display:grid;grid-template-columns:1fr auto;gap:6px var(--space-6);font-size:var(--font-size-sm);font-variant-numeric:tabular-nums}
.po-totals dt{color:var(--text-secondary)}.po-totals dd{margin:0;text-align:right}
.po-totals .po-grand{padding-top:var(--space-2);border-top:1px solid var(--border-subtle);font-family:var(--font-display);font-size:var(--font-size-lg);font-weight:700;color:var(--text-primary)}
.po-num{text-align:right!important;font-variant-numeric:tabular-nums;white-space:nowrap}
.po-timeline{list-style:none;margin:0;padding:0;display:flex;flex-direction:column;gap:var(--space-3)}
.po-timeline li{display:grid;grid-template-columns:10px 1fr;gap:var(--space-3);font-size:var(--font-size-sm)}
.po-timeline li::before{content:"";width:10px;height:10px;margin-top:5px;border-radius:50%;background:var(--accent)}
.po-files{display:flex;flex-direction:column;gap:var(--space-2)}
.po-file{display:flex;align-items:center;gap:var(--space-3);padding:8px 12px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);font-size:var(--font-size-sm)}
.po-file__name{flex:1;min-width:0;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.po-file__remove{all:unset;cursor:pointer;color:var(--color-danger);font-weight:700;padding:0 4px}
.po-detail-cols{display:grid;grid-template-columns:minmax(0,1fr) 340px;gap:var(--space-5);align-items:start}
@media(max-width:1000px){.po-detail-cols{grid-template-columns:minmax(0,1fr)}}

/* ── Form page ── */
.pof{max-width:1180px;min-width:0;width:100%;margin:0 auto;padding-bottom:88px}
.pof__card,.pof__items,.pof__section{min-width:0}
.pof__table-wrap{max-width:100%}
.pof__card{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);overflow:visible}
.pof__head{display:flex;align-items:flex-start;gap:var(--space-4);padding:var(--space-5) var(--space-6);border-bottom:1px solid var(--border-subtle)}
.pof__icon{display:grid;place-items:center;width:40px;height:40px;flex-shrink:0;border-radius:var(--radius-md);background:var(--regalis-black);color:var(--accent)}
.pof__title{margin:0;font-family:var(--font-display);font-size:var(--font-size-xl);font-weight:700;color:var(--text-primary)}
.pof__desc{margin:2px 0 0;color:var(--text-secondary);font-size:var(--font-size-sm)}
.pof__close{margin-inline-start:auto}
.pof__section{padding:var(--space-5) var(--space-6);border-bottom:1px solid var(--border-subtle)}
.pof__section--tint{background:rgb(var(--accent-rgb) / 5%)}
.pof__section:last-child{border-bottom:0}
.pof .field{min-width:0}
.pof .field__input{height:38px;padding:8px 10px}
.pof textarea.field__input{height:auto;min-height:72px;line-height:1.45;resize:vertical}
.pof .field__input[readonly]{background:var(--surface-hover);color:var(--text-secondary)}
.pof__party{display:grid;grid-template-columns:minmax(0,1.1fr) minmax(0,1fr);gap:var(--space-6)}
@media(max-width:860px){.pof__party{grid-template-columns:minmax(0,1fr)}}
.pof__supplier-row{display:flex;gap:0}
.pof__supplier-row .pof-combo{flex:1}
.pof__supplier-row .pof-combo input{border-start-end-radius:0;border-end-end-radius:0}
.pof__search-btn{all:unset;display:grid;place-items:center;width:42px;height:38px;border-radius:0 var(--radius-md) var(--radius-md) 0;background:var(--accent);color:var(--text-on-gold);cursor:pointer}
.pof__search-btn:focus-visible{box-shadow:var(--focus-ring)}
.pof__meta{margin-top:var(--space-2);font-size:var(--font-size-xs);color:var(--text-secondary);line-height:1.6}
.pof__links{display:flex;flex-wrap:wrap;gap:var(--space-4);margin-top:var(--space-2)}
.pof__link{all:unset;cursor:pointer;font-size:var(--font-size-xs);font-weight:700;color:var(--regalis-gold-dark)}
[data-theme=dark] .pof__link{color:var(--accent-bright)}
.pof__link:hover,.pof__link:focus-visible{text-decoration:underline}
.pof__address{font-size:var(--font-size-sm);color:var(--text-secondary);line-height:1.6}
.pof__address b{color:var(--text-primary)}
.pof__details{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-3) var(--space-5);align-items:start}
@media(max-width:860px){.pof__details{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.pof__details{grid-template-columns:minmax(0,1fr)}}
.pof__check{display:flex;flex-wrap:wrap;align-items:center;gap:var(--space-2) var(--space-4);margin-top:var(--space-4);font-size:var(--font-size-sm)}
.pof__check label{display:flex;align-items:center;gap:var(--space-2);cursor:pointer;color:var(--text-primary)}
.pof__check input{width:16px;height:16px;accent-color:var(--accent)}
.pof__hint{font-size:var(--font-size-xs);color:var(--text-muted)}
.pof__items{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card)}
.pof__items-head{display:flex;align-items:center;justify-content:space-between;padding:var(--space-3) var(--space-4);border-bottom:1px solid var(--border-subtle)}
.pof__items-head h3{margin:0;font-size:var(--font-size-md);font-weight:700}
.pof__table-wrap{position:relative;overflow:visible}
@media(max-width:1180px){.pof__table-wrap{overflow-x:auto;padding-bottom:var(--space-2)}}
.pof__table{width:100%;min-width:880px;border-collapse:collapse;font-size:var(--font-size-sm)}
.pof__table th{padding:10px 8px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.05em;text-transform:uppercase;color:var(--text-muted);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.pof__table td{padding:8px;border-bottom:1px solid var(--border-subtle);vertical-align:top}
.pof__table tr:last-child td{border-bottom:0}
.pof__table tr.pof-row--over td{box-shadow:inset 0 2px 0 var(--accent)}
.pof__table tr.pof-row--drag td{opacity:.45}
.pof__table .field__input{height:34px;padding:6px 8px}
.pof__table input.pof-desc{height:28px;margin-top:4px;font-size:var(--font-size-xs);border-style:dashed}
.pof__table input.pof-unit{height:26px;margin-top:4px;font-size:var(--font-size-xs);text-align:right}
.pof__table .pof-num input{text-align:right}
.pof__handle{all:unset;touch-action:none;cursor:grab;display:grid;place-items:center;width:22px;height:34px;color:var(--text-muted)}
.pof__handle:focus-visible{box-shadow:var(--focus-ring);border-radius:4px}
.pof__amount{display:flex;align-items:center;justify-content:flex-end;height:34px;font-weight:700;font-variant-numeric:tabular-nums}
.pof__rowerr{margin-top:3px;font-size:var(--font-size-xs);color:var(--color-danger)}
.pof__rowact{display:flex;align-items:center;gap:2px;height:34px}
.pof__x{all:unset;cursor:pointer;display:grid;place-items:center;width:26px;height:26px;border-radius:var(--radius-sm);color:var(--color-danger);font-weight:700}
.pof__x:hover,.pof__x:focus-visible{background:var(--surface-hover)}
.pof__dots{all:unset;cursor:pointer;display:grid;place-items:center;width:26px;height:26px;border-radius:var(--radius-sm);color:var(--text-secondary);font-weight:700;letter-spacing:1px}
.pof__dots:hover,.pof__dots:focus-visible{background:var(--surface-hover)}
.pof__add{display:flex;flex-wrap:wrap;gap:var(--space-2);margin-top:var(--space-3)}
.pof__lower{display:grid;grid-template-columns:minmax(0,1fr) minmax(320px,400px);gap:var(--space-6);align-items:start;margin-top:var(--space-5)}
@media(max-width:900px){.pof__lower{grid-template-columns:minmax(0,1fr)}}
.pof__summary{border:1px solid rgb(var(--accent-rgb) / 30%);border-radius:var(--radius-lg);background:rgb(var(--accent-rgb) / 6%);padding:var(--space-4) var(--space-5);display:flex;flex-direction:column;gap:var(--space-3);font-size:var(--font-size-sm);font-variant-numeric:tabular-nums}
.pof__sumrow{display:grid;grid-template-columns:74px minmax(0,1fr) auto;gap:var(--space-2);align-items:center}
.pof__sumrow > span:first-child{color:var(--text-secondary);white-space:nowrap}
.pof__sumrow > .pof__sumval{text-align:right;min-width:76px;font-weight:600;color:var(--text-primary)}
.pof__sumrow .pof__neg{color:var(--color-danger)}
.pof__suminputs{display:flex;gap:var(--space-2);justify-content:flex-end}
.pof__suminputs .field__input{height:32px;padding:5px 8px;font-size:var(--font-size-xs)}
.pof__suminputs input{width:74px;flex-shrink:0;text-align:right}
.pof__suminputs select{width:auto;min-width:0;max-width:180px;padding-inline-end:24px}
.pof__suminputs select[aria-label="Adjustment type"]{min-width:104px}
.pof__suminputs select[aria-label="Discount type"]{min-width:52px}
.pof__suminputs{min-width:0}
.pof__swipe{display:none;padding:6px var(--space-4) 0;font-size:var(--font-size-xs);color:var(--text-muted)}
@media(max-width:1180px){.pof__swipe{display:block}}
@media(max-width:560px){
.pof__sumrow{display:flex;flex-wrap:wrap;align-items:center;row-gap:6px}
.pof__sumrow > span:first-child{flex:1}
.pof__sumrow > .pof__suminputs{order:3;flex-basis:100%;justify-content:flex-start}
.pof__sumrow > span:empty{display:none}
}
.pof__total{display:flex;justify-content:space-between;align-items:baseline;padding-top:var(--space-3);border-top:1px solid rgb(var(--accent-rgb) / 30%)}
.pof__total span{font-family:var(--font-display);font-size:var(--font-size-lg);font-weight:700}
.pof__total strong{font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:700;color:var(--text-primary)}
.pof__rcm{font-size:var(--font-size-xs);color:var(--text-muted)}
.pof__terms{display:grid;grid-template-columns:minmax(0,1.3fr) minmax(0,1fr);gap:var(--space-6);align-items:start}
@media(max-width:860px){.pof__terms{grid-template-columns:minmax(0,1fr)}}
.pof__terms textarea.field__input{min-height:130px}
.pof__footer{position:fixed;inset-inline:0;bottom:0;z-index:20;display:flex;justify-content:flex-end;gap:var(--space-3);padding:var(--space-3) var(--space-6);border-top:1px solid var(--border-subtle);background:color-mix(in srgb,var(--surface-card) 92%,transparent);backdrop-filter:blur(8px)}
@media(min-width:1024px){.pof__footer{inset-inline-start:var(--sidebar-width,260px)}}
.pof__footer-note{margin-inline-end:auto;align-self:center;font-size:var(--font-size-xs);color:var(--text-muted)}
@media(max-width:560px){.pof__footer{flex-wrap:wrap;padding:var(--space-3) var(--space-4)}.pof__footer-note{flex-basis:100%}}
.pof-combo{position:relative}
.pof-combo__list{position:absolute;inset-inline:0;top:calc(100% + 4px);z-index:40;max-height:280px;overflow-y:auto;margin:0;padding:4px;list-style:none;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);box-shadow:var(--shadow-lg);min-width:260px}
.pof-combo__opt{padding:7px 10px;border-radius:var(--radius-sm);cursor:pointer;font-size:var(--font-size-sm)}
.pof-combo__opt[aria-selected=true]{background:var(--surface-hover)}
.pof-combo__opt small{display:block;color:var(--text-muted);font-size:var(--font-size-xs)}
.pof-combo__opt--action{font-weight:700;color:var(--regalis-gold-dark);border-top:1px solid var(--border-subtle);margin-top:4px}
.pof-combo__empty{padding:8px 10px;color:var(--text-muted);font-size:var(--font-size-xs)}
.pof-bulk{max-height:340px;overflow-y:auto;display:flex;flex-direction:column;border:1px solid var(--border-subtle);border-radius:var(--radius-md)}
.pof-bulk label{display:grid;grid-template-columns:18px 1fr auto;gap:var(--space-3);align-items:center;padding:8px 12px;border-bottom:1px solid var(--border-subtle);cursor:pointer;font-size:var(--font-size-sm)}
.pof-bulk label:last-child{border-bottom:0}
.pof-bulk input{accent-color:var(--accent)}
.pof-radio{display:flex;gap:var(--space-3);align-items:flex-start;padding:10px 12px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);cursor:pointer;font-size:var(--font-size-sm)}
.pof-radio--on{border-color:var(--accent);background:rgb(var(--accent-rgb) / 6%)}
.pof-radio input{margin-top:3px;accent-color:var(--accent)}
.modal:has(.pof-modal-wide){max-width:640px}
/* ── Reference look (compact item table, tinted add buttons, divided footer) ── */
.pof__head{align-items:center;padding:var(--space-4) var(--space-6)}
.pof__icon{width:36px;height:36px;background:transparent;color:var(--text-primary);border:1.5px solid var(--text-primary);border-radius:10px}
.pof__title{font-size:var(--font-size-xl)}
.pof__closex{all:unset;margin-inline-start:auto;display:grid;place-items:center;width:34px;height:34px;border-radius:var(--radius-sm);cursor:pointer;color:var(--color-danger)}
.pof__closex:hover,.pof__closex:focus-visible{background:var(--surface-hover)}
.pof__section{padding:var(--space-5) var(--space-6)}
.pof .field__label{font-size:var(--font-size-xs);font-weight:700;color:var(--text-primary)}
.pof__address{font-size:var(--font-size-xs);line-height:1.7}
.pof__address-label{font-weight:700;color:var(--text-primary);margin-bottom:4px;font-size:var(--font-size-xs)}
.pof__deliver{margin-top:var(--space-3)}
.pof__meta{font-size:var(--font-size-xs);color:var(--text-secondary)}
.pof__items{border-radius:var(--radius-md);background:var(--surface-card)}
.pof__items-head{padding:10px var(--space-4)}
.pof__items-head h3{font-size:var(--font-size-sm)}
.pof__items-head .btn{color:var(--regalis-gold-dark);font-weight:700}
.pof__table{min-width:860px;table-layout:fixed}
.pof__table th{padding:8px 10px;font-size:10.5px;background:rgb(var(--accent-rgb) / 4%)}
.pof__table th:nth-child(1){width:34px}.pof__table th:nth-child(2){width:auto}
.pof__table th:nth-child(3){width:170px}.pof__table th:nth-child(4){width:92px;text-align:right}
.pof__table th:nth-child(5){width:108px;text-align:right}.pof__table th:nth-child(6){width:84px}
.pof__table th:nth-child(7){width:118px;text-align:right}.pof__table th:nth-child(8){width:64px}
.pof__table td{padding:9px 10px;vertical-align:middle}
.pof__handle{height:28px}
.pof__itemcell{display:flex;gap:10px;align-items:flex-start}
.pof__itemtext{flex:1;min-width:0}
.pof__thumb{flex-shrink:0;display:grid;place-items:center;width:30px;height:30px;border-radius:6px;background:rgb(var(--accent-rgb) / 16%);color:var(--regalis-gold-dark);font-weight:700;font-size:var(--font-size-xs)}
.pof__thumb--empty{display:none}
/* Cells read as text; the box appears on hover and focus. The empty last row keeps a box. */
.pof__table .pof-combo input,.pof-cell{width:100%;height:30px;padding:4px 8px;border:1px solid transparent;border-radius:6px;background:transparent;font:inherit;font-size:var(--font-size-sm);color:var(--text-primary);transition:border-color var(--transition-fast),background var(--transition-fast)}
.pof__table .pof-combo input{font-weight:700}
.pof__table tr:hover .pof-combo input,.pof__table tr:hover .pof-cell{border-color:var(--border-subtle)}
.pof__table .pof-combo input:focus,.pof-cell:focus{outline:none;border-color:var(--accent);background:var(--surface-card);box-shadow:var(--focus-ring)}
.pof__table tr:last-child .pof-combo input{border-color:var(--border-subtle);background:var(--surface-inset);font-weight:400}
.pof__table tr:last-child .pof-cell{color:var(--text-muted)}
.pof-cell--num{text-align:right;font-variant-numeric:tabular-nums}
.pof-cell--muted{color:var(--text-muted)}
.pof-cell--error,.pof__table .pof-combo input[aria-invalid=true]{border-color:var(--color-danger)!important}
select.pof-cell{appearance:none;-webkit-appearance:none;cursor:pointer;padding-inline-end:18px;background-image:linear-gradient(45deg,transparent 50%,currentColor 50%),linear-gradient(135deg,currentColor 50%,transparent 50%);background-position:calc(100% - 9px) 13px,calc(100% - 5px) 13px;background-size:4px 4px;background-repeat:no-repeat}
.pof__table tr:not(:hover) select.pof-cell:not(:focus){background-image:none}
.pof__table input.pof-desc{display:block;width:100%;height:22px;margin-top:1px;padding:0 8px;border:1px solid transparent;border-radius:4px;background:transparent;font:inherit;font-size:var(--font-size-xs);color:var(--text-muted)}
.pof__table input.pof-desc:focus{outline:none;border-color:var(--accent);background:var(--surface-card)}
.pof__amount{height:30px;font-size:var(--font-size-sm)}
.pof__amount--zero{color:var(--text-muted);font-weight:600}
.pof__rowact{height:30px;justify-content:flex-end}
.pof__add{margin-top:var(--space-3)}
.pof__addbtn{all:unset;box-sizing:border-box;display:inline-flex;align-items:center;gap:6px;padding:7px 14px;border:1px solid rgb(var(--accent-rgb) / 45%);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 16%);color:var(--regalis-gold-dark);font-size:var(--font-size-xs);font-weight:700;cursor:pointer}
.pof__addbtn:hover{background:rgb(var(--accent-rgb) / 24%)}
.pof__addbtn:focus-visible{box-shadow:var(--focus-ring)}
[data-theme=dark] .pof__addbtn{color:var(--accent-bright)}
.pof__lower{grid-template-columns:minmax(0,1fr) minmax(300px,420px);gap:var(--space-5)}
.pof__lower .field{align-self:start}
.pof__summary{gap:10px;padding:var(--space-4) var(--space-5);background:rgb(var(--accent-rgb) / 10%);border-color:rgb(var(--accent-rgb) / 35%)}
.pof__summary .pof__sumrow:first-child > span{font-weight:700;color:var(--text-primary)}
.pof__suminputs .field__input{background:var(--surface-card)}
.pof__suminputs input[readonly]{background:var(--surface-card);color:var(--text-secondary)}
.pof__total{margin-top:4px}
.pof__terms{grid-template-columns:minmax(0,1.3fr) minmax(0,1fr);gap:0}
.pof__terms > .field{padding-inline-end:var(--space-6)}
.pof__attach{padding-inline-start:var(--space-6);border-inline-start:1px solid var(--border-subtle);align-self:stretch}
.pof__upload{all:unset;box-sizing:border-box;display:inline-flex;align-items:center;gap:8px;padding:8px 16px;border:1px solid var(--border-strong);border-radius:var(--radius-md);background:var(--surface-card);font-size:var(--font-size-sm);font-weight:600;color:var(--text-primary);cursor:pointer}
.pof__upload:hover{background:var(--surface-hover)}
.pof__upload:focus-visible{box-shadow:var(--focus-ring)}
.pof__attach .po-file{background:rgb(var(--accent-rgb) / 6%)}
.pof__attach .po-file__name{flex:0 1 auto}
.pof__attach .po-file .po-sub{flex:1}
@media(max-width:860px){.pof__terms > .field{padding-inline-end:0}.pof__attach{padding-inline-start:0;border-inline-start:0;padding-top:var(--space-4);border-top:1px solid var(--border-subtle);margin-top:var(--space-4)}}
`;
function usePoStyles() {
  l.useEffect(() => {
    if (document.getElementById(PO_STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = PO_STYLE_ID;
    style.textContent = PO_STYLES;
    document.head.appendChild(style);
  }, []);
}

// ── Icons ───────────────────────────────────────────────────────────────────

const IconBag = makeIcon("shopping-bag", [
  ["path", { d: "M6 2 3 6v14a2 2 0 0 0 2 2h14a2 2 0 0 0 2-2V6l-3-4Z", key: "hou9p0" }],
  ["path", { d: "M3 6h18", key: "d0wm0j" }],
  ["path", { d: "M16 10a4 4 0 0 1-8 0", key: "1ltviw" }]
]);
const IconSearch = makeIcon("search", [
  ["circle", { cx: "11", cy: "11", r: "8", key: "4ej97u" }],
  ["path", { d: "m21 21-4.3-4.3", key: "1qie3q" }]
]);
const IconGrip = makeIcon("grip-vertical", [
  ["circle", { cx: "9", cy: "12", r: "1", key: "1vctgf" }], ["circle", { cx: "9", cy: "5", r: "1", key: "hp0tcf" }],
  ["circle", { cx: "9", cy: "19", r: "1", key: "fkjjf6" }], ["circle", { cx: "15", cy: "12", r: "1", key: "1tmaij" }],
  ["circle", { cx: "15", cy: "5", r: "1", key: "19l28e" }], ["circle", { cx: "15", cy: "19", r: "1", key: "f4zoj3" }]
]);
const IconUpload = makeIcon("upload", [
  ["path", { d: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4", key: "ih7n3h" }],
  ["polyline", { points: "17 8 12 3 7 8", key: "t8dd8p" }],
  ["line", { x1: "12", x2: "12", y1: "3", y2: "15", key: "widbto" }]
]);
const IconX = makeIcon("x", [["path", { d: "M18 6 6 18", key: "1bl5f8" }], ["path", { d: "m6 6 12 12", key: "d8bk6v" }]]);

// ── Small shared components ─────────────────────────────────────────────────

/** A button that opens a list of actions. Closes on outside click and Escape. */
function Menu({ label, items, ariaLabel, buttonClass, variant = "outline" }) {
  const [open, setOpen] = l.useState(false);
  const ref = l.useRef(null);
  l.useEffect(() => {
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
  const visible = items.filter(Boolean);
  if (visible.length === 0) return null;
  const trigger = buttonClass
    ? e.jsx("button", { type: "button", className: buttonClass, "aria-haspopup": "menu", "aria-expanded": open, "aria-label": ariaLabel, onClick: () => setOpen((o) => !o), children: label })
    : e.jsx(x, { size: "sm", variant, "aria-haspopup": "menu", "aria-expanded": open, "aria-label": ariaLabel, onClick: () => setOpen((o) => !o), children: label });
  return e.jsxs("div", {
    className: "po-menu",
    ref,
    children: [
      trigger,
      open &&
        e.jsx("div", {
          className: "po-menu__list",
          role: "menu",
          children: visible.map((item) =>
            e.jsx(
              "button",
              {
                type: "button",
                role: "menuitem",
                className: `po-menu__item${item.danger ? " po-menu__item--danger" : ""}`,
                "aria-disabled": item.disabled ? true : undefined,
                onClick: () => {
                  if (item.disabled) return;
                  setOpen(false);
                  item.onClick();
                },
                children: item.label
              },
              item.label
            )
          )
        })
    ]
  });
}

/** Text input with a filtered option list (keyboard: ↑ ↓ Enter Esc). Free text is allowed. */
function Combobox({ id, value, onText, options, onPick, placeholder, invalid, action, inputRef, ariaLabel }) {
  const [open, setOpen] = l.useState(false);
  const [active, setActive] = l.useState(0);
  const text = (value ?? "").trim().toLowerCase();
  const matches = (text ? options.filter((o) => `${o.label} ${o.sub ?? ""}`.toLowerCase().includes(text)) : options).slice(0, 50);
  const all = action ? [...matches, action] : matches;
  const pick = (option) => {
    setOpen(false);
    if (option.isAction) option.onClick();
    else onPick(option);
  };
  return e.jsxs("div", {
    className: "pof-combo",
    children: [
      e.jsx("input", {
        id,
        ref: inputRef,
        className: `field__input${invalid ? " field__input--error" : ""}`,
        role: "combobox",
        "aria-expanded": open,
        "aria-controls": `${id}-list`,
        "aria-autocomplete": "list",
        "aria-activedescendant": open && all[active] ? `${id}-opt-${active}` : undefined,
        "aria-invalid": !!invalid,
        "aria-label": ariaLabel,
        autoComplete: "off",
        placeholder,
        value: value ?? "",
        onChange: (event) => {
          onText(event.target.value);
          setOpen(true);
          setActive(0);
        },
        onFocus: () => setOpen(true),
        onBlur: () => setTimeout(() => setOpen(false), 150),
        onKeyDown: (event) => {
          if (event.key === "ArrowDown") {
            event.preventDefault();
            setOpen(true);
            setActive((a) => Math.min(a + 1, all.length - 1));
          } else if (event.key === "ArrowUp") {
            event.preventDefault();
            setActive((a) => Math.max(a - 1, 0));
          } else if (event.key === "Enter" && open && all[active]) {
            event.preventDefault();
            pick(all[active]);
          } else if (event.key === "Escape") setOpen(false);
        }
      }),
      open &&
        e.jsxs("ul", {
          id: `${id}-list`,
          role: "listbox",
          className: "pof-combo__list",
          children: [
            matches.length === 0 && e.jsx("li", { className: "pof-combo__empty", children: text ? "No match — the typed name will be used." : "Nothing to pick from yet." }),
            ...all.map((option, index) =>
              e.jsxs(
                "li",
                {
                  id: `${id}-opt-${index}`,
                  role: "option",
                  "aria-selected": index === active,
                  className: `pof-combo__opt${option.isAction ? " pof-combo__opt--action" : ""}`,
                  onMouseDown: (event) => {
                    event.preventDefault();
                    pick(option);
                  },
                  onMouseEnter: () => setActive(index),
                  children: [option.label, option.sub && e.jsx("small", { children: option.sub })]
                },
                option.isAction ? "__action" : option.value
              )
            )
          ]
        })
    ]
  });
}

function TextArea({ id, label, value, onChange, placeholder, rows = 3, error, hint, required, maxLength }) {
  return e.jsxs("div", {
    className: "field",
    children: [
      e.jsxs("label", { className: "field__label", htmlFor: id, children: [label, required && e.jsx("span", { className: "field__required", "aria-hidden": true, children: " *" })] }),
      e.jsx("textarea", { id, rows, maxLength, placeholder, value, "aria-invalid": !!error, className: `field__input${error ? " field__input--error" : ""}`, onChange: (event) => onChange(event.target.value) }),
      error ? e.jsx("span", { className: "field__error", role: "alert", children: error }) : hint && e.jsx("span", { className: "field__hint", children: hint })
    ]
  });
}

function ConfirmModal({ state, onClose }) {
  const [busy, setBusy] = l.useState(false);
  const [comment, setComment] = l.useState("");
  l.useEffect(() => setComment(""), [state]);
  if (!state) return null;
  const run = async () => {
    if (state.commentRequired && !comment.trim()) return;
    setBusy(true);
    try {
      await state.onConfirm(comment.trim());
      onClose();
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(G, {
    open: true,
    title: state.title,
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(x, { variant: "ghost", onClick: onClose, disabled: busy, children: state.cancelLabel ?? "Cancel" }),
        e.jsx(x, { onClick: run, loading: busy, disabled: busy || (state.commentRequired && !comment.trim()), children: state.confirmLabel })
      ]
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        state.body && e.jsx("p", { style: { margin: 0, color: "var(--text-secondary)" }, children: state.body }),
        state.commentLabel && e.jsx(TextArea, { id: "po-confirm-comment", label: state.commentLabel, required: state.commentRequired, value: comment, onChange: setComment, rows: 3, maxLength: 500 })
      ]
    })
  });
}

// ── Workflow actions shared by the list and the detail page ─────────────────

function useWorkflow(onChanged) {
  const toast = z();
  const [confirm, setConfirm] = l.useState(null);
  const [cancelling, setCancelling] = l.useState(null);
  const [receiving, setReceiving] = l.useState(null);
  const perms = {
    create: usePermission("PURCHASE.CREATE"),
    edit: usePermission("PURCHASE.EDIT"),
    approve: usePermission("PURCHASE.APPROVE"),
    cancel: usePermission("PURCHASE.DELETE")
  };
  const act = (fn, done) => async (comment) => {
    try {
      const updated = await fn(comment);
      toast.success(done);
      onChanged(updated);
    } catch (error) {
      toast.error(errorText(error, "The action failed."));
      throw error;
    }
  };
  const actions = (order, requireApproval) => {
    const st = order.status;
    const received = order.lines.some((line) => line.receivedQuantity > 0);
    return {
      edit: st === S.draft && perms.edit,
      submit: st === S.draft && requireApproval && perms.create,
      approveDraft: st === S.draft && !requireApproval && perms.approve,
      approve: st === S.pending && perms.approve,
      reject: st === S.pending && perms.approve,
      send: st === S.approved && perms.approve,
      receive: (st === S.sent || st === S.partial) && perms.edit,
      cancel: st !== S.cancelled && st !== S.received && !received && perms.cancel,
      pdf: true
    };
  };
  const run = {
    submit: (order) =>
      setConfirm({
        title: `Submit ${order.poNumber} for approval?`,
        body: "The purchase order is locked while an approver reviews it. An approver can reject it back to draft with a reason.",
        confirmLabel: "Submit for approval",
        onConfirm: act(() => poApi.submit(order.purchaseOrderId), "Submitted for approval.")
      }),
    approve: (order) =>
      setConfirm({
        title: `Approve ${order.poNumber}?`,
        body: `${order.supplierName} · ${fmtMoney(order.totalAmount)}. Once approved it can be sent to the supplier.`,
        commentLabel: "Comment (optional)",
        confirmLabel: "Approve",
        onConfirm: act((comment) => poApi.approve(order.purchaseOrderId, comment), "Purchase order approved.")
      }),
    reject: (order) =>
      setConfirm({
        title: `Reject ${order.poNumber}?`,
        body: "It goes back to Draft so the requester can change it.",
        commentLabel: "Reason",
        commentRequired: true,
        confirmLabel: "Reject",
        onConfirm: act((comment) => poApi.reject(order.purchaseOrderId, comment), "Sent back to draft.")
      }),
    send: (order) =>
      setConfirm({
        title: `Send ${order.poNumber} to ${order.supplierName}?`,
        body: "After this the purchase order is frozen: it can only be received or cancelled. Download the PDF to share it with the supplier.",
        confirmLabel: "Mark as sent",
        onConfirm: act(() => $.send(order.purchaseOrderId), "Purchase order sent.")
      }),
    cancel: (order) => setCancelling(order),
    receive: (order) => setReceiving(order),
    pdf: async (order) => {
      try {
        await poApi.pdf(order);
      } catch (error) {
        toast.error(errorText(error, "Could not download the PDF."));
      }
    }
  };
  const modals = e.jsxs(e.Fragment, {
    children: [
      e.jsx(ConfirmModal, { state: confirm, onClose: () => setConfirm(null) }),
      cancelling && e.jsx(ie, { open: true, order: cancelling, onClose: () => setCancelling(null), onCancelled: (updated) => { setCancelling(null); onChanged(updated); } }),
      receiving && e.jsx(ne, { order: receiving, onClose: () => setReceiving(null), onReceived: (updated) => { setReceiving(null); onChanged(updated); } })
    ]
  });
  return { actions, run, modals, perms };
}

function useFormOptions() {
  const [options, setOptions] = l.useState(null);
  l.useEffect(() => {
    poApi.formOptions().then(setOptions).catch(() => setOptions(null));
  }, []);
  return options;
}

// ── List page ───────────────────────────────────────────────────────────────

function be() {
  usePoStyles();
  const toast = z();
  const navigate = useNavigate();
  const options = useFormOptions();
  const [result, setResult] = l.useState(null);
  const [loading, setLoading] = l.useState(true);
  const [page, setPage] = l.useState(1);
  const [filters, setFilters] = l.useState({ search: "", status: "", supplierId: "", locationId: "", from: "", to: "" });
  const [search, setSearch] = l.useState("");
  const [suppliers, setSuppliers] = l.useState([]);
  const [locations, setLocations] = l.useState([]);

  l.useEffect(() => {
    const t = setTimeout(() => setFilters((f) => (f.search === search ? f : { ...f, search })), 350);
    return () => clearTimeout(t);
  }, [search]);
  l.useEffect(() => {
    supplierApi.list({ page: 1, pageSize: 100 }).then((r) => setSuppliers(r.items.map((s) => ({ value: s.supplierId, label: s.supplierName })))).catch(() => {});
    locationsApi.list({ page: 1, pageSize: 100 }).then((r) => setLocations(r.items.map((s) => ({ value: s.locationId, label: s.locationName })))).catch(() => {});
  }, []);

  const load = l.useCallback(async () => {
    setLoading(true);
    try {
      setResult(
        await $.list({
          page,
          pageSize: 20,
          search: filters.search.trim() || undefined,
          status: filters.status ? Number(filters.status) : undefined,
          supplierId: filters.supplierId ? Number(filters.supplierId) : undefined,
          locationId: filters.locationId ? Number(filters.locationId) : undefined,
          from: filters.from || undefined,
          to: filters.to || undefined
        })
      );
    } catch (error) {
      toast.error(errorText(error, "Failed to load purchase orders."));
    } finally {
      setLoading(false);
    }
  }, [page, filters]);
  l.useEffect(() => {
    load();
  }, [load]);

  const { actions, run, modals, perms } = useWorkflow(() => load());
  const setFilter = (key) => (event) => {
    setPage(1);
    setFilters((f) => ({ ...f, [key]: event.target.value }));
  };
  const today = todayIst();
  const filtered = Object.entries(filters).some(([, v]) => v) || search;

  const columns = [
    {
      key: "po",
      header: "PO number",
      render: (n) =>
        e.jsxs("div", {
          children: [
            e.jsx("div", { style: { fontWeight: 600 }, children: n.poNumber }),
            n.referenceNumber && e.jsxs("div", { className: "po-sub", children: ["Ref ", n.referenceNumber] })
          ]
        })
    },
    { key: "supplier", header: "Supplier", render: (n) => e.jsxs("div", { children: [e.jsx("div", { children: n.supplierName }), e.jsx("div", { className: "po-sub", children: n.locationName ?? "" })] }) },
    { key: "date", header: "PO date", render: (n) => fmtDate(n.poDate) },
    {
      key: "delivery",
      header: "Expected delivery",
      render: (n) => {
        if (!n.expectedDeliveryDate) return "—";
        const overdue = n.expectedDeliveryDate < today && [S.approved, S.sent, S.partial, S.pending, S.draft].includes(n.status);
        return e.jsx("span", { className: overdue ? "po-overdue" : undefined, title: overdue ? "Overdue" : undefined, children: fmtDate(n.expectedDeliveryDate) + (overdue ? " · overdue" : "") });
      }
    },
    { key: "amount", header: "Amount", render: (n) => e.jsx("strong", { children: fmtMoney(n.totalAmount) }) },
    { key: "status", header: "Status", render: (n) => e.jsx(StatusBadge, { status: n.status }) },
    { key: "by", header: "Created by", render: (n) => n.createdByName ?? "—" },
    {
      key: "actions",
      header: "",
      width: "150px",
      render: (n) => {
        const can = actions(n, options?.requireApproval ?? true);
        return e.jsxs("div", {
          className: "row-actions",
          children: [
            e.jsx(x, { size: "sm", variant: "outline", onClick: () => navigate(`${BILLING}/${n.purchaseOrderId}`), children: "View" }),
            e.jsx(Menu, {
              label: "⋯",
              ariaLabel: `More actions for ${n.poNumber}`,
              items: [
                can.edit && { label: "Edit", onClick: () => navigate(`${BILLING}/${n.purchaseOrderId}/edit`) },
                { label: "Download PDF", onClick: () => run.pdf(n) },
                can.submit && { label: "Submit for approval", onClick: () => run.submit(n) },
                (can.approve || can.approveDraft) && { label: "Approve", onClick: () => run.approve(n) },
                can.reject && { label: "Reject", onClick: () => run.reject(n) },
                can.send && { label: "Send to supplier", onClick: () => run.send(n) },
                can.receive && { label: "Receive goods", onClick: () => navigate(`${BILLING}/${n.purchaseOrderId}?receive=1`) },
                can.cancel && { label: "Cancel PO", danger: true, onClick: () => run.cancel(n) }
              ]
            })
          ]
        });
      }
    }
  ];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Purchase Orders",
        description: "Anything the company buys. Receiving goods against a PO updates inventory in the same step.",
        actions: perms.create && e.jsx(x, { onClick: () => navigate(`${BILLING}/new`), children: "New purchase order" })
      }),
      e.jsxs(Card, {
        children: [
          e.jsxs("div", {
            className: "po-toolbar",
            role: "search",
            children: [
              e.jsx(g, { label: "Search", placeholder: "PO number, supplier or reference", value: search, onChange: (event) => { setPage(1); setSearch(event.target.value); } }),
              e.jsx(Q, { label: "Status", value: filters.status, placeholder: "All statuses", options: PO_STATUS_ORDER.map((s) => ({ value: s, label: PO_STATUS[s] })), onChange: setFilter("status") }),
              e.jsx(Q, { label: "Supplier", value: filters.supplierId, placeholder: "All suppliers", options: suppliers, onChange: setFilter("supplierId") }),
              e.jsx(Q, { label: "Location", value: filters.locationId, placeholder: "All locations", options: locations, onChange: setFilter("locationId") }),
              e.jsx(g, { label: "From", type: "date", value: filters.from, max: filters.to || undefined, onChange: setFilter("from") }),
              e.jsx(g, { label: "To", type: "date", value: filters.to, min: filters.from || undefined, onChange: setFilter("to") }),
              e.jsx("div", { className: "po-toolbar__clear", children: e.jsx(x, {
                variant: "ghost",
                disabled: !filtered,
                onClick: () => {
                  setSearch("");
                  setPage(1);
                  setFilters({ search: "", status: "", supplierId: "", locationId: "", from: "", to: "" });
                },
                children: "Clear"
              }) })
            ]
          }),
          e.jsx(DataTable, {
            columns,
            result,
            loading,
            rowKey: (n) => n.purchaseOrderId,
            onPageChange: setPage,
            onRowClick: (n) => navigate(`${BILLING}/${n.purchaseOrderId}`),
            emptyTitle: filtered ? "No purchase orders match" : "No purchase orders",
            emptyDescription: filtered ? "Try a different search or clear the filters." : "Raise a PO whenever the company buys something."
          })
        ]
      }),
      modals
    ]
  });
}

// ── Detail page ─────────────────────────────────────────────────────────────

function C({ label: i, children: p }) {
  return e.jsxs("div", { className: "po-kv", children: [e.jsx("span", { className: "po-kv__label", children: i }), e.jsx("div", { children: p })] });
}

function TotalsList({ order }) {
  const rows = [["Sub total", fmtMoney(order.subTotal)]];
  if (order.discountAmount > 0) rows.push([order.discountType === 1 ? `Discount (${order.discountValue}%)` : "Discount", `− ${fmtMoney(order.discountAmount)}`]);
  const rcm = order.isReverseCharge ? " (reverse charge)" : "";
  if (order.isInterState) rows.push([`IGST${rcm}`, fmtMoney(order.igstAmount)]);
  else if (order.cgstAmount || order.sgstAmount || !order.taxAmount) {
    rows.push([`CGST${rcm}`, fmtMoney(order.cgstAmount)]);
    rows.push([`SGST${rcm}`, fmtMoney(order.sgstAmount)]);
  } else rows.push([`GST${rcm}`, fmtMoney(order.taxAmount)]);
  if (order.tdsAmount > 0) rows.push([order.tdsLabel ?? "TDS", `− ${fmtMoney(order.tdsAmount)}`]);
  if (order.adjustmentAmount) rows.push([order.adjustmentLabel ?? "Adjustment", fmtMoney(order.adjustmentAmount)]);
  return e.jsxs("dl", {
    className: "po-totals",
    children: [
      ...rows.flatMap(([k, v]) => [e.jsx("dt", { children: k }, `${k}-t`), e.jsx("dd", { children: v }, `${k}-d`)]),
      e.jsx("dt", { className: "po-grand", children: "Total" }),
      e.jsx("dd", { className: "po-grand", children: fmtMoney(order.totalAmount) })
    ]
  });
}

function fe() {
  usePoStyles();
  const { id } = useParams();
  const toast = z();
  const navigate = useNavigate();
  const options = useFormOptions();
  const [order, setOrder] = l.useState(null);
  const [loading, setLoading] = l.useState(true);
  const load = l.useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setOrder(await $.get(Number(id)));
    } catch (error) {
      toast.error(errorText(error, "Failed to load the purchase order."));
    } finally {
      setLoading(false);
    }
  }, [id]);
  l.useEffect(() => {
    load();
  }, [load]);
  const { actions, run, modals } = useWorkflow((updated) => (updated ? setOrder(updated) : load()));
  const wantsReceive = l.useRef(new URLSearchParams(window.location.search).get("receive") === "1");
  l.useEffect(() => {
    if (order && wantsReceive.current) {
      wantsReceive.current = false;
      if (order.status === S.sent || order.status === S.partial) run.receive(order);
    }
  }, [order]);

  if (loading && !order) return e.jsx(Spinner, { center: true });
  if (!order) return null;
  const can = actions(order, options?.requireApproval ?? true);
  const frozen = [S.sent, S.partial, S.received, S.cancelled].includes(order.status);
  const lineColumns = [
    { key: "no", header: "#", width: "40px", render: (a) => a.lineNumber },
    {
      key: "item",
      header: "Item details",
      render: (a) =>
        e.jsxs("div", {
          children: [
            e.jsx("div", { style: { fontWeight: 600 }, children: a.itemName }),
            a.remarks && e.jsx("div", { className: "po-sub", children: a.remarks }),
            e.jsx("div", { className: "po-sub", children: [a.itemCode ? `${a.itemCode} · stocked` : "Not stocked", a.hsnSacCode ? `HSN/SAC ${a.hsnSacCode}` : null].filter(Boolean).join(" · ") })
          ]
        })
    },
    { key: "account", header: "Account", render: (a) => a.accountCategory ?? "—" },
    { key: "qty", header: "Quantity", render: (a) => e.jsx("span", { className: "po-num", children: `${a.quantity} ${a.unit ?? ""}`.trim() }) },
    { key: "received", header: "Received", render: (a) => (a.receivedQuantity > 0 ? e.jsxs(Badge, { tone: a.pendingQuantity > 0 ? "gold" : "success", children: [a.receivedQuantity, " of ", a.quantity] }) : e.jsx("span", { style: { color: "var(--text-muted)" }, children: "—" })) },
    { key: "rate", header: "Rate (₹)", render: (a) => e.jsx("span", { className: "po-num", children: num.format(a.rate) }) },
    { key: "gst", header: "GST", render: (a) => `${a.taxPercent}%` },
    { key: "amount", header: "Amount (₹)", render: (a) => e.jsx("strong", { className: "po-num", children: num.format(a.quantity * a.rate) }) }
  ];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: order.poNumber,
        description: `${order.supplierName} · ${fmtDate(order.poDate)}`,
        actions: e.jsxs(e.Fragment, {
          children: [
            e.jsx(x, { variant: "ghost", onClick: () => navigate(BILLING), children: "Back" }),
            e.jsx(x, { variant: "outline", onClick: () => run.pdf(order), children: "Download PDF" }),
            can.edit && e.jsx(x, { variant: "outline", onClick: () => navigate(`${BILLING}/${order.purchaseOrderId}/edit`), children: "Edit" }),
            can.reject && e.jsx(x, { variant: "outline", onClick: () => run.reject(order), children: "Reject" }),
            can.submit && e.jsx(x, { onClick: () => run.submit(order), children: "Submit for approval" }),
            (can.approve || can.approveDraft) && e.jsx(x, { onClick: () => run.approve(order), children: "Approve" }),
            can.send && e.jsx(x, { onClick: () => run.send(order), children: "Send to supplier" }),
            can.receive && e.jsx(x, { onClick: () => run.receive(order), children: "Receive goods" }),
            can.cancel && e.jsx(x, { variant: "ghost", onClick: () => run.cancel(order), children: "Cancel PO" })
          ]
        })
      }),
      e.jsxs("div", {
        className: "form-stack",
        children: [
          frozen && order.status !== S.cancelled && e.jsxs("div", { className: "po-banner", role: "note", children: [e.jsx(IconBag, { size: 18 }), e.jsxs("span", { children: [e.jsx("b", { children: "Frozen." }), " This purchase order was sent to the supplier on ", order.sentUtc ? fmtDate(order.sentUtc) : "—", ". It can only be received or cancelled."] })] }),
          order.status === S.pending && e.jsxs("div", { className: "po-banner", role: "note", children: [e.jsx(IconBag, { size: 18 }), e.jsxs("span", { children: [e.jsx("b", { children: "Awaiting approval." }), " Submitted ", order.submittedUtc ? fmtDateTime(order.submittedUtc) : "", ". It is locked until an approver approves it or sends it back to draft."] })] }),
          order.cancellationReason && e.jsxs("div", { className: "form-error", role: "alert", children: ["Cancelled: ", order.cancellationReason] }),
          e.jsx(Card, {
            children: e.jsxs("div", {
              className: "po-grid3",
              children: [
                e.jsxs("div", {
                  children: [
                    e.jsx("h3", { className: "po-block-title", children: "Supplier" }),
                    e.jsx("div", { style: { fontWeight: 700 }, children: order.supplierName }),
                    e.jsx("div", { className: "po-sub", children: [order.supplierEmail, order.supplierMobile].filter(Boolean).join(" · ") }),
                    order.supplierGstin && e.jsxs("div", { className: "po-sub", children: ["GSTIN ", order.supplierGstin] }),
                    order.supplierAddress && e.jsx("div", { className: "po-sub", children: order.supplierAddress })
                  ]
                }),
                e.jsxs("div", {
                  children: [
                    e.jsx("h3", { className: "po-block-title", children: "Deliver to" }),
                    e.jsx("div", { style: { fontWeight: 700 }, children: order.locationName ?? "—" }),
                    e.jsx("div", { className: "po-sub", children: order.deliveryAddress ?? "" })
                  ]
                }),
                e.jsxs("div", {
                  style: { display: "grid", gridTemplateColumns: "1fr 1fr", gap: "var(--space-3)" },
                  children: [
                    e.jsx(C, { label: "Status", children: e.jsx(StatusBadge, { status: order.status }) }),
                    e.jsx(C, { label: "Reference", children: order.referenceNumber ?? "—" }),
                    e.jsx(C, { label: "Expected delivery", children: order.expectedDeliveryDate ? fmtDate(order.expectedDeliveryDate) : "—" }),
                    e.jsx(C, { label: "Shipment", children: order.shipmentPreference ?? "—" }),
                    e.jsx(C, { label: "Tax", children: order.isInterState ? "IGST (inter-state)" : "CGST + SGST" }),
                    e.jsx(C, { label: "Reverse charge", children: order.isReverseCharge ? "Yes" : "No" }),
                    e.jsx(C, { label: "Created by", children: order.createdByName ?? "—" }),
                    e.jsx(C, { label: "Approved by", children: order.approvedByName ? `${order.approvedByName}${order.approvedUtc ? " · " + fmtDate(order.approvedUtc) : ""}` : "—" })
                  ]
                })
              ]
            })
          }),
          e.jsx(Card, {
            children: e.jsx(DataTable, {
              columns: lineColumns,
              result: { items: order.lines, page: 1, pageSize: order.lines.length || 1, totalCount: order.lines.length, totalPages: 1, hasNext: false, hasPrevious: false },
              loading: false,
              rowKey: (a) => a.purchaseOrderLineId,
              onPageChange: () => {}
            })
          }),
          e.jsxs("div", {
            className: "po-detail-cols",
            children: [
              e.jsxs("div", {
                className: "form-stack",
                children: [
                  (order.remarks || order.termsAndConditions) &&
                    e.jsx(Card, {
                      children: e.jsxs("div", {
                        className: "form-stack",
                        children: [
                          order.remarks && e.jsxs("div", { children: [e.jsx("h3", { className: "po-block-title", children: "Notes" }), e.jsx("p", { style: { margin: 0, whiteSpace: "pre-wrap" }, children: order.remarks })] }),
                          order.termsAndConditions && e.jsxs("div", { children: [e.jsx("h3", { className: "po-block-title", children: "Terms & conditions" }), e.jsx("p", { style: { margin: 0, whiteSpace: "pre-wrap", color: "var(--text-secondary)", fontSize: "var(--font-size-sm)" }, children: order.termsAndConditions })] })
                        ]
                      })
                    }),
                  e.jsx(Card, {
                    children: e.jsxs("div", {
                      children: [
                        e.jsx("h3", { className: "po-block-title", children: "Attachments" }),
                        (order.attachments ?? []).length === 0
                          ? e.jsx("p", { className: "po-sub", style: { margin: 0 }, children: "No files attached." })
                          : e.jsx("div", {
                              className: "po-files",
                              children: order.attachments.map((a) =>
                                e.jsxs("div", { className: "po-file", children: [e.jsx("span", { className: "po-file__name", children: a.fileName }), e.jsx("span", { className: "po-sub", children: fileSize(a.fileSizeBytes) }), e.jsx(x, { size: "sm", variant: "ghost", onClick: () => poApi.attachment(order.purchaseOrderId, a).catch(() => toast.error("Could not download the file.")), children: "Download" })] }, a.purchaseOrderAttachmentId)
                              )
                            })
                      ]
                    })
                  }),
                  (order.history ?? []).length > 0 &&
                    e.jsx(Card, {
                      children: e.jsxs("div", {
                        children: [
                          e.jsx("h3", { className: "po-block-title", children: "Activity" }),
                          e.jsx("ol", {
                            className: "po-timeline",
                            children: order.history
                              .slice()
                              .reverse()
                              .map((h, index) => e.jsxs("li", { children: [e.jsxs("div", { children: [e.jsx("strong", { children: h.action }), " · ", e.jsx("span", { children: h.changedByName ?? "System" }), e.jsx("div", { className: "po-sub", children: fmtDateTime(h.changedUtc) }), h.reason && e.jsx("div", { style: { color: "var(--text-secondary)" }, children: h.reason })] })] }, index))
                          })
                        ]
                      })
                    })
                ]
              }),
              e.jsx(Card, { children: e.jsx(TotalsList, { order }) })
            ]
          })
        ]
      }),
      modals
    ]
  });
}

// ── Form page: New / Edit purchase order ────────────────────────────────────

const ALLOWED_FILES = [".pdf", ".jpg", ".jpeg", ".png", ".docx", ".xlsx"];
// "Rounded" computes the adjustment itself (to the nearest rupee); the others take a typed amount.
const ROUNDED = "Rounded";
const ADJUSTMENT_LABELS = [ROUNDED, "Additional charge", "Freight", "Other adjustment"];
let rowSeed = 0;
// A new row starts with GST unset ("Select"); it takes the item's rate, or the default,
// as soon as an item is picked or typed.
const newRow = (gst = "") => ({ key: `r${++rowSeed}`, inventoryItemId: "", itemName: "", remarks: "", hsnSacCode: "", unit: "", accountCategory: "", quantity: "1.00", rate: "", taxPercent: gst });
const isBlankRow = (r) => !r.itemName.trim() && !r.inventoryItemId && String(r.rate).trim() === "";

function rowErrors(row) {
  const errors = {};
  if (!row.itemName.trim()) errors.itemName = "Item is required.";
  if (String(row.taxPercent).trim() === "") errors.taxPercent = "Pick a GST rate.";
  const qty = scaled(row.quantity, 3);
  if (qty === null) errors.quantity = "Up to 3 decimals.";
  else if (qty <= 0) errors.quantity = "Must be more than 0.";
  if (String(row.rate).trim() === "") errors.rate = "Rate is required.";
  else if (scaled(row.rate, 2) === null) errors.rate = "A non-negative amount.";
  if (scaled(row.taxPercent, 2) === null) errors.taxPercent = "Pick a GST rate.";
  return errors;
}

/** Create or edit a supplier without leaving the PO. */
function SupplierModal({ supplier, states, onClose, onSaved }) {
  const toast = z();
  const { fieldErrors, summary, capture, clear } = Y();
  const [busy, setBusy] = l.useState(false);
  const [form, setForm] = l.useState(() => ({
    supplierCode: supplier?.supplierCode ?? `SUP-${String(Date.now()).slice(-5)}`,
    supplierName: supplier?.supplierName ?? supplier?.draftName ?? "",
    contactPerson: supplier?.contactPerson ?? "",
    email: supplier?.email ?? "",
    mobileNumber: supplier?.mobileNumber ?? "",
    gstin: supplier?.gstin ?? "",
    addressLine: supplier?.addressLine ?? "",
    city: supplier?.city ?? "",
    state: supplier?.state ?? "",
    pincode: supplier?.pincode ?? "",
    paymentTerms: supplier?.paymentTerms ?? ""
  }));
  const set = (key) => (event) => setForm((f) => ({ ...f, [key]: event.target.value }));
  const editing = !!supplier?.supplierId;
  const save = async () => {
    setBusy(true);
    clear();
    const body = {
      ...form,
      gstin: form.gstin.trim().toUpperCase() || null,
      contactPerson: form.contactPerson || null, email: form.email || null, mobileNumber: form.mobileNumber || null,
      addressLine: form.addressLine || null, city: form.city || null, state: form.state || null, pincode: form.pincode || null,
      paymentTerms: form.paymentTerms || null,
      locationId: supplier?.locationId ?? null,
      notes: supplier?.notes ?? null
    };
    try {
      const saved = editing ? await supplierApi.update(supplier.supplierId, body) : await supplierApi.create(body);
      toast.success(editing ? "Supplier updated." : "Supplier added.");
      onSaved(saved);
    } catch (error) {
      const message = capture(error);
      message && toast.error(message);
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(G, {
    open: true,
    title: editing ? `Edit supplier — ${supplier.supplierName}` : "Add new supplier",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(x, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(x, { onClick: save, loading: busy, disabled: busy || !form.supplierName.trim() || !form.supplierCode.trim(), children: editing ? "Save supplier" : "Add supplier" })] }),
    children: e.jsxs("div", {
      className: "form-stack pof-modal-wide",
      children: [
        summary && e.jsx("div", { className: "form-error", role: "alert", children: summary }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(g, { label: "Supplier name", required: true, value: form.supplierName, error: fieldErrors.supplierName, onChange: set("supplierName") }),
            e.jsx(g, { label: "Supplier code", required: true, value: form.supplierCode, error: fieldErrors.supplierCode, onChange: set("supplierCode") }),
            e.jsx(g, { label: "Contact person", value: form.contactPerson, error: fieldErrors.contactPerson, onChange: set("contactPerson") }),
            e.jsx(g, { label: "Email", type: "email", value: form.email, error: fieldErrors.email, onChange: set("email") }),
            e.jsx(g, { label: "Phone", value: form.mobileNumber, error: fieldErrors.mobileNumber, onChange: set("mobileNumber") }),
            e.jsx(g, { label: "GSTIN", value: form.gstin, maxLength: 15, error: fieldErrors.gstin, hint: "Its first two digits decide CGST+SGST or IGST.", onChange: set("gstin") })
          ]
        }),
        e.jsx(g, { label: "Address", value: form.addressLine, error: fieldErrors.addressLine, onChange: set("addressLine") }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(g, { label: "City", value: form.city, error: fieldErrors.city, onChange: set("city") }),
            e.jsx(Q, { label: "State", value: form.state, placeholder: "Select a state", options: states.map((s) => ({ value: s.name, label: `${s.name} (${s.code})` })), onChange: set("state") }),
            e.jsx(g, { label: "PIN code", value: form.pincode, error: fieldErrors.pincode, onChange: set("pincode") }),
            e.jsx(g, { label: "Payment terms", placeholder: "Net 30", value: form.paymentTerms, error: fieldErrors.paymentTerms, onChange: set("paymentTerms") })
          ]
        })
      ]
    })
  });
}

/** Pick the branch the goods go to, and optionally type a different address for it. */
function DeliveryModal({ locations, locationId, address, onClose, onSaved }) {
  const [picked, setPicked] = l.useState(String(locationId ?? ""));
  const [text, setText] = l.useState(address ?? "");
  const choose = (loc) => {
    setPicked(String(loc.locationId));
    setText(addressOfLocation(loc) || loc.locationName);
  };
  return e.jsx(G, {
    open: true,
    title: "Change delivery address",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(x, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(x, { disabled: !picked || !text.trim(), onClick: () => onSaved(Number(picked), text.trim()), children: "Use this address" })] }),
    children: e.jsxs("div", {
      className: "form-stack pof-modal-wide",
      children: [
        e.jsx("div", {
          className: "form-stack",
          role: "radiogroup",
          "aria-label": "Delivery location",
          children: locations.map((loc) =>
            e.jsxs(
              "label",
              {
                className: `pof-radio${picked === String(loc.locationId) ? " pof-radio--on" : ""}`,
                children: [
                  e.jsx("input", { type: "radio", name: "po-location", checked: picked === String(loc.locationId), onChange: () => choose(loc) }),
                  e.jsxs("span", { children: [e.jsx("strong", { children: loc.locationName }), loc.isDefault && e.jsx("span", { className: "po-sub", children: " · default" }), e.jsx("div", { className: "po-sub", children: addressOfLocation(loc) || "No address saved for this location" })] })
                ]
              },
              loc.locationId
            )
          )
        }),
        e.jsx(TextArea, { id: "po-delivery-text", label: "Delivery address printed on the PO", required: true, value: text, onChange: setText, rows: 3, maxLength: 600, hint: "Edit it for a different floor, gate or site. This PO keeps its own copy." })
      ]
    })
  });
}

/** Tick several stock items and add them as rows in one go. */
function BulkItemsModal({ items, onRow, onClose, onAdd }) {
  const [query, setQuery] = l.useState("");
  const [picked, setPicked] = l.useState(() => new Set());
  const q = query.trim().toLowerCase();
  const shown = items.filter((i) => !q || `${i.itemName} ${i.itemCode} ${i.category ?? ""}`.toLowerCase().includes(q));
  const toggle = (id) => setPicked((set) => {
    const next = new Set(set);
    next.has(id) ? next.delete(id) : next.add(id);
    return next;
  });
  return e.jsx(G, {
    open: true,
    title: "Add items in bulk",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(x, { variant: "ghost", onClick: onClose, children: "Cancel" }), e.jsx(x, { disabled: picked.size === 0, onClick: () => onAdd(items.filter((i) => picked.has(i.inventoryItemId))), children: picked.size ? `Add ${picked.size} item${picked.size === 1 ? "" : "s"}` : "Add items" })] }),
    children: e.jsxs("div", {
      className: "form-stack pof-modal-wide",
      children: [
        e.jsx(g, { label: "Search items", placeholder: "Name, code or category", value: query, onChange: (event) => setQuery(event.target.value) }),
        items.length === 0
          ? e.jsx("p", { className: "po-sub", children: "No stock items yet. Add them under Assets → Items, or type item names straight into the table." })
          : e.jsx("div", {
              className: "pof-bulk",
              children: shown.map((item) => {
                const already = onRow(item.inventoryItemId);
                return e.jsxs(
                  "label",
                  {
                    children: [
                      e.jsx("input", { type: "checkbox", checked: picked.has(item.inventoryItemId), onChange: () => toggle(item.inventoryItemId) }),
                      e.jsxs("span", { children: [e.jsx("strong", { children: item.itemName }), e.jsx("div", { className: "po-sub", children: [item.itemCode, item.category, item.unit].filter(Boolean).join(" · ") })] }),
                      already ? e.jsx(Badge, { tone: "warning", children: "Already on this PO" }) : item.unitCost ? e.jsx("span", { className: "po-sub", children: fmtMoney(item.unitCost) }) : e.jsx("span", {})
                    ]
                  },
                  item.inventoryItemId
                );
              })
            }),
        e.jsx("p", { className: "po-sub", style: { margin: 0 }, children: "Items marked “Already on this PO” are added as a second row only if you tick them." })
      ]
    })
  });
}

/** A number cell: raw digits while editing, "7,500.00" otherwise (Indian grouping). */
const qtyFormat = new Intl.NumberFormat("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 3 });
function NumCell({ value, digits, onChange, invalid, ...rest }) {
  const [editing, setEditing] = l.useState(false);
  const n = scaled(value, digits);
  const shown = editing || String(value).trim() === "" || n === null ? value : (digits === 2 ? num : qtyFormat).format(n / 10 ** digits);
  return e.jsx("input", {
    ...rest,
    className: `pof-cell pof-cell--num${invalid ? " pof-cell--error" : ""}`,
    "aria-invalid": !!invalid,
    inputMode: "decimal",
    value: shown,
    onFocus: () => setEditing(true),
    onBlur: () => setEditing(false),
    onChange: (event) => onChange(event.target.value.replace(/,/g, ""))
  });
}

function AdminPurchaseOrderFormPage() {
  usePoStyles();
  const { id } = useParams();
  const editId = id ? Number(id) : null;
  const toast = z();
  const navigate = useNavigate();
  const canApprove = usePermission("PURCHASE.APPROVE");
  const { fieldErrors: serverErrors, summary, capture, clear } = Y();

  const [options, setOptions] = l.useState(null);
  const [suppliers, setSuppliers] = l.useState([]);
  const [locations, setLocations] = l.useState([]);
  const [stock, setStock] = l.useState([]);
  const [existing, setExisting] = l.useState(null);
  const [loadError, setLoadError] = l.useState(null);
  const [ready, setReady] = l.useState(false);

  const [head, setHead] = l.useState({
    supplierId: "", supplierText: "", locationId: "", deliveryAddress: "", poDate: todayIst(), expectedDeliveryDate: "",
    referenceNumber: "", shipmentPreference: "", isReverseCharge: false, remarks: "", terms: "",
    discountType: "1", discountValue: "", tdsCode: "", adjustmentLabel: ROUNDED, adjustmentAmount: ""
  });
  const [rows, setRows] = l.useState(() => [newRow()]);
  const [files, setFiles] = l.useState([]);
  const [errors, setErrors] = l.useState({});
  const [rowErrs, setRowErrs] = l.useState({});
  const [saving, setSaving] = l.useState(null);
  const [modal, setModal] = l.useState(null);
  const [dragIndex, setDragIndex] = l.useState(null);
  const [overIndex, setOverIndex] = l.useState(null);
  const fileInput = l.useRef(null);
  const dragRef = l.useRef(null);
  l.useEffect(() => {
    setErrors((errs) => (errs.lines ? { ...errs, lines: undefined } : errs));
  }, [rows]);
  const supplierInput = l.useRef(null);

  // Load everything the form needs, then the PO being edited.
  l.useEffect(() => {
    (async () => {
      try {
        const [opts, supplierPage, locationPage, itemPage] = await Promise.all([
          poApi.formOptions(),
          supplierApi.list({ page: 1, pageSize: 200 }),
          locationsApi.list({ page: 1, pageSize: 100 }),
          inventoryApi.items({ page: 1, pageSize: 200 }).catch(() => ({ items: [] }))
        ]);
        setOptions(opts);
        setSuppliers(supplierPage.items.filter((s) => s.isActive !== false));
        setLocations(locationPage.items.filter((l2) => l2.isActive !== false));
        setStock(itemPage.items.filter((i) => i.isActive !== false));
        const gst = String(opts.defaultGstPercent ?? 18);
        if (editId) {
          const order = await $.get(editId);
          setExisting(order);
          const legacyLineDiscount = order.discountType == null ? order.lines.reduce((s, ln) => s + (ln.discountAmount || 0), 0) : 0;
          setHead({
            supplierId: String(order.supplierId), supplierText: order.supplierName, locationId: String(order.locationId),
            deliveryAddress: order.deliveryAddress ?? "", poDate: order.poDate, expectedDeliveryDate: order.expectedDeliveryDate ?? "",
            referenceNumber: order.referenceNumber ?? "", shipmentPreference: order.shipmentPreference ?? "",
            isReverseCharge: !!order.isReverseCharge, remarks: order.remarks ?? "", terms: order.termsAndConditions ?? "",
            discountType: order.discountType ? String(order.discountType) : legacyLineDiscount ? "2" : "1",
            discountValue: order.discountType ? String(order.discountValue) : legacyLineDiscount ? String(legacyLineDiscount) : "",
            tdsCode: order.tdsCode ?? "", adjustmentLabel: !order.adjustmentAmount || order.adjustmentLabel === "Round off" ? ROUNDED : order.adjustmentLabel ?? "Other adjustment",
            adjustmentAmount: order.adjustmentAmount ? String(order.adjustmentAmount) : ""
          });
          setRows(order.lines.map((ln) => ({ ...newRow(gst), inventoryItemId: ln.inventoryItemId ? String(ln.inventoryItemId) : "", itemName: ln.itemName, remarks: ln.remarks ?? "", hsnSacCode: ln.hsnSacCode ?? "", unit: ln.unit ?? "", accountCategory: ln.accountCategory ?? "", quantity: Number(ln.quantity).toFixed(Number.isInteger(Number(ln.quantity)) ? 2 : 3).replace(/0$/, ""), rate: Number(ln.rate).toFixed(2), taxPercent: String(ln.taxPercent) })).concat([newRow()]));
        } else {
          const def = locationPage.items.find((l2) => l2.isDefault) ?? (locationPage.items.length === 1 ? locationPage.items[0] : null);
          setHead((h) => ({
            ...h,
            terms: opts.defaultTermsAndConditions ?? "",
            shipmentPreference: opts.shipmentPreferences[0] ?? "",
            locationId: def ? String(def.locationId) : "",
            deliveryAddress: def ? addressOfLocation(def) || def.locationName : ""
          }));
          setRows([newRow()]);
        }
        setReady(true);
      } catch (error) {
        setLoadError(errorText(error, "Could not load the purchase order form."));
      }
    })();
  }, [editId]);

  const states = options?.states ?? [];
  const supplier = suppliers.find((s) => String(s.supplierId) === head.supplierId) ?? null;
  const location = locations.find((l2) => String(l2.locationId) === head.locationId) ?? null;
  const supplierState = supplierStateCode(supplier, states);
  const locationState = stateCodeOf(location?.state, states) ?? options?.homeStateCode ?? "29";
  const interState = !!supplierState && supplierState !== locationState;
  const tds = options?.tdsOptions.find((t) => t.code === head.tdsCode) ?? null;
  const rounding = head.adjustmentLabel === ROUNDED;
  const beforeAdjustment = poCompute(rows, {
    discountType: head.discountValue ? head.discountType : null,
    discountValue: head.discountValue,
    interState,
    reverseCharge: head.isReverseCharge,
    tdsPercent: tds ? String(tds.percent) : "0",
    adjustment: rounding ? "0" : head.adjustmentAmount
  });
  const roundDiff = rounding ? divRound(beforeAdjustment.total, 100) * 100 - beforeAdjustment.total : 0;
  const math = rounding ? { ...beforeAdjustment, adjustment: roundDiff, total: beforeAdjustment.total + roundDiff } : beforeAdjustment;
  const gstOptions = (options?.gstRateOptions ?? [0, 5, 12, 18, 28]).map((r) => ({ value: String(r), label: `${r}%` }));
  const accountOptions = (options?.accountCategories ?? []).map((a) => ({ value: a, label: a }));
  const itemOptions = stock.map((i) => ({ value: String(i.inventoryItemId), label: i.itemName, sub: [i.itemCode, i.category, i.unit].filter(Boolean).join(" · "), item: i }));
  const supplierOptions = suppliers.map((s) => ({ value: String(s.supplierId), label: s.supplierName, sub: [s.gstin, s.city].filter(Boolean).join(" · "), supplier: s }));
  const rateLabel = (list) => (list.length ? `(${list.map((r) => (interState ? r : r / 2) + "%").join(" / ")})` : "");

  const setH = (key, value) => {
    setHead((h) => ({ ...h, [key]: value }));
    setErrors((errs) => ({ ...errs, [key]: undefined }));
  };
  const patchRow = (index, patch) => {
    setRows((list) => list.map((r, i) => (i === index ? { ...r, ...patch } : r)));
    setRowErrs((errs) => {
      const key = rows[index]?.key;
      if (!key || !errs[key]) return errs;
      const next = { ...errs, [key]: { ...errs[key] } };
      Object.keys(patch).forEach((k) => delete next[key][k]);
      return next;
    });
  };
  const hasItem = (itemId, exceptIndex = -1) => rows.some((r, i) => i !== exceptIndex && r.inventoryItemId === String(itemId));
  const applyItem = (index, item) =>
    patchRow(index, {
      inventoryItemId: String(item.inventoryItemId),
      itemName: item.itemName,
      unit: item.unit ?? "",
      hsnSacCode: item.hsnSacCode ?? "",
      remarks: rows[index].remarks || item.category || "",
      taxPercent: item.taxPercent != null && !item.isNonTaxable ? String(item.taxPercent) : rows[index].taxPercent || String(options?.defaultGstPercent ?? 18),
      rate: String(rows[index].rate).trim() === "" && item.unitCost != null ? Number(item.unitCost).toFixed(2) : rows[index].rate
    });
  const pickItem = (index, option) => {
    if (hasItem(option.item.inventoryItemId, index)) {
      setModal({ kind: "confirm", state: { title: "Add this item again?", body: `${option.item.itemName} is already on this purchase order. Add it as a separate row anyway?`, confirmLabel: "Add another row", cancelLabel: "No, keep one row", onConfirm: async () => applyItem(index, option.item) } });
      return;
    }
    applyItem(index, option.item);
  };
  // "+ Add new row" focuses the empty row that is always kept at the end.
  const addRow = () => {
    const last = rows[rows.length - 1];
    if (last && isBlankRow(last)) document.getElementById(`po-item-${last.key}`)?.focus();
    else setRows((list) => [...list, newRow()]);
  };
  const removeRow = (index) => setRows((list) => (list.length === 1 ? [newRow()] : list.filter((_, i) => i !== index)));
  // Keep exactly one empty row at the end, like the reference item table.
  l.useEffect(() => {
    if (!ready) return;
    const last = rows[rows.length - 1];
    if (!last || !isBlankRow(last)) setRows((list) => [...list, newRow()]);
  }, [rows, ready]);
  const moveRow = (from, to) => {
    if (to < 0 || to >= rows.length || from === to) return;
    setRows((list) => {
      const next = [...list];
      const [moved] = next.splice(from, 1);
      next.splice(to, 0, moved);
      return next;
    });
  };
  const duplicateRow = (index) => setRows((list) => [...list.slice(0, index + 1), { ...list[index], key: `r${++rowSeed}` }, ...list.slice(index + 1)]);
  const bulkAdd = (items) => {
    const gst = String(options?.defaultGstPercent ?? 18);
    setRows((list) => {
      const kept = list.filter((r) => !isBlankRow(r));
      const added = items.map((item) => ({
        ...newRow(gst), inventoryItemId: String(item.inventoryItemId), itemName: item.itemName, unit: item.unit ?? "", hsnSacCode: item.hsnSacCode ?? "", remarks: item.category ?? "",
        rate: item.unitCost != null ? Number(item.unitCost).toFixed(2) : "", taxPercent: item.taxPercent != null && !item.isNonTaxable ? String(item.taxPercent) : gst
      }));
      return [...kept, ...added];
    });
    setModal(null);
    toast.success(`${items.length} item${items.length === 1 ? "" : "s"} added.`);
  };
  const applyToAll = (key) => {
    const value = rows[0]?.[key];
    if (value === undefined) return;
    setRows((list) => list.map((r) => ({ ...r, [key]: value })));
  };

  const addFiles = (list) => {
    const max = options?.maxAttachments ?? 10;
    const maxBytes = options?.maxAttachmentBytes ?? 10 * 1024 * 1024;
    const current = (existing?.attachments?.length ?? 0) + files.length;
    const accepted = [];
    for (const file of list) {
      const ext = (file.name.match(/\.[^.]+$/)?.[0] ?? "").toLowerCase();
      if (!ALLOWED_FILES.includes(ext)) toast.error(`${file.name}: allowed types are ${ALLOWED_FILES.join(" ")}.`);
      else if (file.size > maxBytes) toast.error(`${file.name} is larger than 10 MB.`);
      else if (current + accepted.length >= max) {
        toast.error(`A purchase order can carry at most ${max} files.`);
        break;
      } else accepted.push({ key: `f${++rowSeed}`, file });
    }
    if (accepted.length) setFiles((f) => [...f, ...accepted]);
  };
  const removeExisting = (a) =>
    setModal({
      kind: "confirm",
      state: {
        title: `Remove ${a.fileName}?`,
        confirmLabel: "Remove file",
        onConfirm: async () => {
          try {
            const updated = await poApi.removeAttachment(existing.purchaseOrderId, a.purchaseOrderAttachmentId);
            setExisting(updated);
          } catch (error) {
            toast.error(errorText(error, "Could not remove the file."));
          }
        }
      }
    });

  /** Client-side checks; the server repeats all of them. */
  const validate = (issuing) => {
    const errs = {};
    if (!head.supplierId) errs.supplierId = head.supplierText.trim() ? "Pick the supplier from the list, or add it as a new supplier." : "Select a supplier.";
    if (!head.locationId || !head.deliveryAddress.trim()) errs.deliveryAddress = "Choose a delivery address.";
    if (!head.poDate) errs.poDate = "PO date is required.";
    if (issuing && !head.expectedDeliveryDate) errs.expectedDeliveryDate = "Expected delivery is required.";
    if (head.expectedDeliveryDate && head.poDate && head.expectedDeliveryDate < head.poDate) errs.expectedDeliveryDate = "Cannot be before the PO date.";
    if (issuing && !head.terms.trim()) errs.terms = "Terms & conditions are required before the PO is issued.";
    if (head.discountValue && scaled(head.discountValue, 2) === null) errs.discountValue = "Enter a valid discount.";
    if (head.discountType === "1" && (scaled(head.discountValue, 2) ?? 0) > 10000) errs.discountValue = "Cannot exceed 100%.";
    if (math.excessDiscount) errs.discountValue = "Discount cannot be more than the sub total.";
    if (!rounding && head.adjustmentAmount && signedPaise(head.adjustmentAmount) === null) errs.adjustmentAmount = "Enter a valid amount.";
    if (math.total < 0) errs.adjustmentAmount = "Total cannot be negative.";
    const filled = rows.filter((r) => !isBlankRow(r));
    if (filled.length === 0) errs.lines = "Add at least one item.";
    const perRow = {};
    filled.forEach((r) => {
      const re2 = rowErrors(r);
      if (Object.keys(re2).length) perRow[r.key] = re2;
    });
    setErrors(errs);
    setRowErrs(perRow);
    return Object.keys(errs).length === 0 && Object.keys(perRow).length === 0;
  };

  const persist = async (mode) => {
    if (saving) return;
    const issuing = mode !== "draft";
    clear();
    if (!validate(issuing)) {
      toast.error("Check the highlighted fields.");
      document.querySelector(".pof [aria-invalid=true]")?.focus();
      return;
    }
    const filled = rows.filter((r) => !isBlankRow(r));
    const payload = {
      poDate: head.poDate || null,
      supplierId: Number(head.supplierId),
      locationId: Number(head.locationId),
      expectedDeliveryDate: head.expectedDeliveryDate || null,
      remarks: head.remarks.trim() || null,
      referenceNumber: head.referenceNumber.trim() || null,
      shipmentPreference: head.shipmentPreference || null,
      isReverseCharge: head.isReverseCharge,
      deliveryAddress: head.deliveryAddress.trim() || null,
      discountType: head.discountValue ? Number(head.discountType) : null,
      discountValue: head.discountValue ? Number(head.discountValue) : null,
      tdsCode: head.tdsCode || null,
      adjustmentLabel: math.adjustment ? (rounding ? "Round off" : head.adjustmentLabel) : null,
      adjustmentAmount: math.adjustment ? math.adjustment / 100 : null,
      termsAndConditions: head.terms,
      lines: filled.map((r) => ({
        inventoryItemId: r.inventoryItemId ? Number(r.inventoryItemId) : null,
        itemName: r.itemName.trim(),
        hsnSacCode: r.hsnSacCode.trim() || null,
        unit: r.unit.trim() || null,
        quantity: Number(r.quantity),
        rate: Number(r.rate),
        discountAmount: 0,
        taxPercent: Number(r.taxPercent),
        remarks: r.remarks.trim() || null,
        accountCategory: r.accountCategory || null
      }))
    };
    setSaving(mode);
    let order = null;
    try {
      order = editId ? await $.update(editId, payload) : await $.create(payload);
    } catch (error) {
      const message = capture(error);
      // Map "lines[i].field" from the server back onto the rows that were sent.
      if (error instanceof U) {
        const perRow = {};
        for (const err of error.errors ?? []) {
          const hit = /^lines\[(\d+)\]\.(\w+)/i.exec(err.field ?? "");
          if (hit && filled[Number(hit[1])]) {
            const key = filled[Number(hit[1])].key;
            perRow[key] = { ...(perRow[key] ?? {}), [hit[2][0].toLowerCase() + hit[2].slice(1)]: err.message };
          }
        }
        setRowErrs(perRow);
      }
      toast.error(message ?? "Check the highlighted fields.");
      setSaving(null);
      return;
    }
    let uploadFailed = 0;
    for (const f of files) {
      try {
        order = await poApi.addAttachment(order.purchaseOrderId, f.file);
      } catch (error) {
        uploadFailed++;
        toast.error(`${f.file.name}: ${errorText(error, "upload failed")}`);
      }
    }
    if (uploadFailed && mode !== "draft") {
      setSaving(null);
      toast.error(`${order.poNumber} was saved as a draft. Re-attach the file that failed, then submit again.`);
      navigate(`${BILLING}/${order.purchaseOrderId}/edit`, { replace: true });
      return;
    }
    try {
      if (mode === "submit") {
        order = await poApi.submit(order.purchaseOrderId);
        toast.success(`${order.poNumber} submitted for approval.`);
      } else if (mode === "approve") {
        order = await poApi.approve(order.purchaseOrderId, null);
        toast.success(`${order.poNumber} saved and approved.`);
      } else {
        toast.success(`${order.poNumber} saved as draft.`);
      }
    } catch (error) {
      capture(error);
      toast.error(`${order.poNumber} was saved as a draft, but: ${errorText(error, "it could not be submitted.")}`);
    }
    setSaving(null);
    navigate(uploadFailed ? `${BILLING}/${order.purchaseOrderId}/edit` : `${BILLING}/${order.purchaseOrderId}`, { replace: true });
  };

  const err = (key) => errors[key] ?? serverErrors[key];
  if (loadError) return e.jsx(Card, { children: e.jsx(EmptyState, { title: "Could not open the form", description: loadError, action: e.jsx(x, { onClick: () => navigate(BILLING), children: "Back to purchase orders" }) }) });
  if (!ready) return e.jsx(Spinner, { center: true });
  if (existing && existing.status !== S.draft)
    return e.jsx(Card, {
      children: e.jsx(EmptyState, {
        title: `${existing.poNumber} is ${PO_STATUS[existing.status]?.toLowerCase()}`,
        description: existing.status === S.pending ? "It is locked while it waits for approval. An approver can reject it back to draft." : "Only a draft purchase order can be edited. Once sent, it can only be received or cancelled.",
        action: e.jsx(x, { onClick: () => navigate(`${BILLING}/${existing.purchaseOrderId}`), children: "View purchase order" })
      })
    });

  const requireApproval = options?.requireApproval ?? true;
  const issueLabel = requireApproval ? "Submit for approval" : canApprove ? "Save & approve" : null;
  const issueMode = requireApproval ? "submit" : "approve";
  const title = editId ? `Edit ${existing?.poNumber ?? "purchase order"}` : "New purchase order";
  const lastIndex = rows.length - 1;

  const row = (r, index) => {
    const re2 = rowErrs[r.key] ?? {};
    const gross = math.lineGross[index];
    return e.jsxs(
      "tr",
      {
        className: [dragIndex === index ? "pof-row--drag" : "", overIndex === index && dragIndex !== null && dragIndex !== index ? "pof-row--over" : ""].join(" ").trim() || undefined,
        "data-row": index,
        children: [
          e.jsx("td", {
            children: e.jsx("button", {
              type: "button",
              className: "pof__handle",
              "aria-label": `Reorder row ${index + 1} (drag, or use Alt+↑ / Alt+↓)`,
              title: "Drag to reorder",
              // Pointer events rather than HTML5 drag-and-drop: works with mouse, pen and touch.
              onPointerDown: (event) => {
                if (event.button !== 0) return;
                event.preventDefault();
                event.currentTarget.setPointerCapture(event.pointerId);
                dragRef.current = { from: index, over: index };
                setDragIndex(index);
                setOverIndex(index);
              },
              onPointerMove: (event) => {
                if (!dragRef.current) return;
                const target = document.elementFromPoint(event.clientX, event.clientY)?.closest("tr[data-row]");
                if (!target) return;
                const over = Number(target.getAttribute("data-row"));
                if (over !== dragRef.current.over) {
                  dragRef.current.over = over;
                  setOverIndex(over);
                }
              },
              onPointerUp: () => {
                const drag = dragRef.current;
                dragRef.current = null;
                setDragIndex(null);
                setOverIndex(null);
                if (drag) moveRow(drag.from, drag.over);
              },
              onPointerCancel: () => {
                dragRef.current = null;
                setDragIndex(null);
                setOverIndex(null);
              },
              onKeyDown: (event) => {
                if (event.altKey && event.key === "ArrowUp") { event.preventDefault(); moveRow(index, index - 1); }
                if (event.altKey && event.key === "ArrowDown") { event.preventDefault(); moveRow(index, index + 1); }
              },
              children: e.jsx(IconGrip, { size: 16 })
            })
          }),
          e.jsx("td", {
            children: e.jsxs("div", {
              className: "pof__itemcell",
              children: [
                e.jsx("span", { className: `pof__thumb${r.itemName.trim() ? "" : " pof__thumb--empty"}`, "aria-hidden": true, children: r.itemName.trim() ? r.itemName.trim()[0].toUpperCase() : "" }),
                e.jsxs("div", {
                  className: "pof__itemtext",
                  children: [
                    e.jsx(Combobox, {
                      id: `po-item-${r.key}`,
                      ariaLabel: `Item ${index + 1}`,
                      placeholder: "Type or click to select an item",
                      value: r.itemName,
                      invalid: re2.itemName,
                      options: itemOptions,
                      onText: (text) => patchRow(index, { itemName: text, inventoryItemId: "", taxPercent: r.taxPercent || String(options?.defaultGstPercent ?? 18) }),
                      onPick: (option) => pickItem(index, option)
                    }),
                    (r.itemName.trim() || r.remarks) && e.jsx("input", { className: "pof-desc", "aria-label": `Description for item ${index + 1}`, placeholder: "Add a description", maxLength: 500, value: r.remarks, onChange: (event) => patchRow(index, { remarks: event.target.value }) }),
                    re2.itemName && e.jsx("div", { className: "pof__rowerr", role: "alert", children: re2.itemName })
                  ]
                })
              ]
            })
          }),
          e.jsx("td", {
            children: e.jsxs("select", {
              className: `pof-cell${r.accountCategory ? "" : " pof-cell--muted"}`,
              "aria-label": `Account for item ${index + 1}`,
              value: r.accountCategory,
              onChange: (event) => patchRow(index, { accountCategory: event.target.value }),
              children: [e.jsx("option", { value: "", children: "Select account" }), ...accountOptions.map((a) => e.jsx("option", { value: a.value, children: a.label }, a.value))]
            })
          }),
          e.jsxs("td", {
            children: [
              e.jsx(NumCell, { "aria-label": `Quantity for item ${index + 1}`, digits: 3, invalid: re2.quantity, placeholder: "1.00", value: r.quantity, onChange: (v) => patchRow(index, { quantity: v }), title: r.unit ? `Unit: ${r.unit}` : undefined }),
              re2.quantity && e.jsx("div", { className: "pof__rowerr", role: "alert", children: re2.quantity })
            ]
          }),
          e.jsxs("td", {
            children: [
              e.jsx(NumCell, { "aria-label": `Rate for item ${index + 1}`, digits: 2, invalid: re2.rate, placeholder: "0.00", value: r.rate, onChange: (v) => patchRow(index, { rate: v }) }),
              re2.rate && e.jsx("div", { className: "pof__rowerr", role: "alert", children: re2.rate })
            ]
          }),
          e.jsxs("td", {
            children: [
              e.jsx("select", {
                className: `pof-cell${r.taxPercent === "" ? " pof-cell--muted" : ""}${re2.taxPercent ? " pof-cell--error" : ""}`,
                "aria-label": `GST for item ${index + 1}`,
                value: r.taxPercent,
                onChange: (event) => patchRow(index, { taxPercent: event.target.value }),
                children: [{ value: "", label: "Select" }, ...gstOptions, ...(r.taxPercent === "" || gstOptions.some((o) => o.value === String(Number(r.taxPercent))) ? [] : [{ value: r.taxPercent, label: `${r.taxPercent}%` }])].map((o) => e.jsx("option", { value: o.value, children: o.label }, o.value))
              }),
              re2.taxPercent && e.jsx("div", { className: "pof__rowerr", role: "alert", children: re2.taxPercent })
            ]
          }),
          e.jsx("td", { children: e.jsx("div", { className: `pof__amount${gross ? "" : " pof__amount--zero"}`, children: gross == null ? "—" : paise(gross) }) }),
          e.jsx("td", {
            children: index === lastIndex && isBlankRow(r) ? null : e.jsxs("div", {
              className: "pof__rowact",
              children: [
                e.jsx(Menu, {
                  label: "⋯",
                  buttonClass: "pof__dots",
                  ariaLabel: `More actions for item ${index + 1}`,
                  items: [
                    { label: "Item details (unit, HSN/SAC)…", onClick: () => setModal({ kind: "details", index }) },
                    { label: "Duplicate row", onClick: () => duplicateRow(index) },
                    { label: "Move up", disabled: index === 0, onClick: () => moveRow(index, index - 1) },
                    { label: "Move down", disabled: index === rows.length - 1, onClick: () => moveRow(index, index + 1) }
                  ]
                }),
                e.jsx("button", { type: "button", className: "pof__x", "aria-label": `Remove item ${index + 1}`, title: "Remove row", onClick: () => removeRow(index), children: e.jsx(IconX, { size: 15 }) })
              ]
            })
          })
        ]
      },
      r.key
    );
  };

  const detailsRow = modal?.kind === "details" ? rows[modal.index] : null;

  return e.jsxs("div", {
    className: "pof",
    children: [
      e.jsxs("form", {
        className: "pof__card",
        noValidate: true,
        onSubmit: (event) => event.preventDefault(),
        "aria-label": title,
        children: [
          // Title
          e.jsxs("div", {
            className: "pof__head",
            children: [
              e.jsx("div", { className: "pof__icon", "aria-hidden": true, children: e.jsx(IconBag, { size: 20 }) }),
              e.jsxs("div", { children: [e.jsx("h2", { className: "pof__title", children: title }), e.jsx("p", { className: "pof__desc", children: "Create and issue a purchase order to a supplier. Once sent, the PO is frozen and can only be received or cancelled." })] }),
              e.jsx("button", { type: "button", className: "pof__closex", "aria-label": "Close and go back", title: "Close", onClick: () => navigate(editId ? `${BILLING}/${editId}` : BILLING), children: e.jsx(IconX, { size: 20 }) })
            ]
          }),
          summary && e.jsx("div", { className: "pof__section", style: { paddingBottom: 0, borderBottom: 0 }, children: e.jsx("div", { className: "form-error", role: "alert", children: summary }) }),

          // Supplier & delivery
          e.jsx("section", {
            className: "pof__section pof__section--tint",
            "aria-label": "Supplier and delivery",
            children: e.jsxs("div", {
              className: "pof__party",
              children: [
                e.jsxs("div", {
                  children: [
                    e.jsxs("div", {
                      className: "field",
                      children: [
                        e.jsxs("label", { className: "field__label", htmlFor: "po-supplier", children: ["Supplier name", e.jsx("span", { className: "field__required", "aria-hidden": true, children: " *" })] }),
                        e.jsxs("div", {
                          className: "pof__supplier-row",
                          children: [
                            e.jsx(Combobox, {
                              id: "po-supplier",
                              inputRef: supplierInput,
                              placeholder: "Search suppliers",
                              value: head.supplierText,
                              invalid: err("supplierId"),
                              options: supplierOptions,
                              onText: (text) => setHead((h) => ({ ...h, supplierText: text, supplierId: "" })),
                              onPick: (option) => {
                                setHead((h) => ({ ...h, supplierId: option.value, supplierText: option.label }));
                                setErrors((errs) => ({ ...errs, supplierId: undefined }));
                              },
                              action: { isAction: true, label: "+ Add new supplier", onClick: () => setModal({ kind: "supplier", supplier: { draftName: head.supplierId ? "" : head.supplierText } }) }
                            }),
                            e.jsx("button", { type: "button", className: "pof__search-btn", "aria-label": "Search suppliers", onClick: () => supplierInput.current?.focus(), children: e.jsx(IconSearch, { size: 16 }) })
                          ]
                        }),
                        err("supplierId") && e.jsx("span", { className: "field__error", role: "alert", children: err("supplierId") })
                      ]
                    }),
                    supplier &&
                      e.jsxs("div", {
                        className: "pof__meta",
                        children: [
                          e.jsx("div", { children: [supplier.email, supplier.mobileNumber].filter(Boolean).join(" · ") || "No email or phone saved" }),
                          e.jsx("div", { children: supplier.gstin ? `GSTIN ${supplier.gstin}${supplierState ? ` · ${states.find((s) => s.code === supplierState)?.name ?? supplierState} (${supplierState})` : ""}` : "No GSTIN — treated as intra-state" })
                        ]
                      }),
                    e.jsx("div", {
                      className: "pof__links",
                      children: supplier
                        ? e.jsx("button", { type: "button", className: "pof__link", onClick: () => setModal({ kind: "supplier", supplier }), children: "Edit supplier" })
                        : e.jsx("button", { type: "button", className: "pof__link", onClick: () => setModal({ kind: "supplier", supplier: { draftName: head.supplierText } }), children: "+ Add new supplier" })
                    })
                  ]
                }),
                e.jsxs("div", {
                  className: "pof__address",
                  children: [
                    e.jsx("div", { className: "pof__address-label", children: "Supplier address" }),
                    supplier
                      ? e.jsxs("div", { children: [e.jsx("div", { children: supplier.supplierName }), e.jsx("div", { children: addressOfSupplier(supplier) || "No address saved" }), supplier.mobileNumber && e.jsx("div", { children: supplier.mobileNumber })] })
                      : e.jsx("div", { className: "po-sub", children: "Select a supplier to see their address." }),
                    e.jsxs("div", {
                      className: "pof__deliver",
                      children: [
                        e.jsxs("div", { children: [e.jsx("b", { children: "Deliver to: " }), location ? e.jsx("span", { children: head.deliveryAddress || location.locationName }) : e.jsx("span", { className: "po-sub", children: "Not chosen" })] }),
                        err("deliveryAddress") && e.jsx("div", { className: "field__error", role: "alert", children: err("deliveryAddress") }),
                        e.jsx("button", { type: "button", className: "pof__link", style: { marginTop: 6 }, onClick: () => setModal({ kind: "delivery" }), children: "Change delivery address" })
                      ]
                    })
                  ]
                })
              ]
            })
          }),

          // PO details
          e.jsxs("section", {
            className: "pof__section",
            "aria-label": "Purchase order details",
            children: [
              e.jsxs("div", {
                className: "pof__details",
                children: [
                  e.jsx(g, { label: "Purchase order #", required: true, readOnly: true, value: existing?.poNumber ?? options?.nextPoNumber ?? "", hint: existing ? "Issued when the PO was created" : "Generated automatically, per financial year" }),
                  e.jsx(g, { label: "Reference #", placeholder: "Supplier quotation / indent no.", maxLength: 80, value: head.referenceNumber, error: err("referenceNumber"), onChange: (event) => setH("referenceNumber", event.target.value) }),
                  e.jsx(g, { label: "Date", type: "date", required: true, value: head.poDate, error: err("poDate"), onChange: (event) => setH("poDate", event.target.value) }),
                  e.jsx(g, { label: "Expected delivery", type: "date", required: true, min: head.poDate || undefined, value: head.expectedDeliveryDate, error: err("expectedDeliveryDate"), hint: "Drives the overdue flag on the PO list", onChange: (event) => setH("expectedDeliveryDate", event.target.value) }),
                  e.jsx(Q, { label: "Shipment preference", value: head.shipmentPreference, placeholder: "Select", options: (options?.shipmentPreferences ?? []).map((s) => ({ value: s, label: s })), onChange: (event) => setH("shipmentPreference", event.target.value) })
                ]
              }),
              e.jsxs("div", {
                className: "pof__check",
                children: [
                  e.jsxs("label", { children: [e.jsx("input", { type: "checkbox", checked: head.isReverseCharge, onChange: (event) => setH("isReverseCharge", event.target.checked) }), "This purchase is applicable for reverse charge"] }),
                  e.jsx("span", { className: "pof__hint", children: "Tick only for unregistered suppliers, as advised by your accountant." })
                ]
              })
            ]
          }),

          // Items
          e.jsxs("section", {
            className: "pof__section",
            "aria-label": "Items",
            children: [
              e.jsxs("div", {
                className: "pof__items",
                children: [
                  e.jsxs("div", {
                    className: "pof__items-head",
                    children: [
                      e.jsx("h3", { children: "Item table" }),
                      e.jsx(Menu, {
                        label: "Bulk actions",
                        variant: "ghost",
                        items: [
                          { label: "Add items in bulk…", onClick: () => setModal({ kind: "bulk" }) },
                          { label: "Use row 1's account on every row", disabled: !rows[0]?.accountCategory, onClick: () => applyToAll("accountCategory") },
                          { label: "Use row 1's GST on every row", onClick: () => applyToAll("taxPercent") },
                          { label: "Remove empty rows", onClick: () => setRows((list) => (list.some((r) => !isBlankRow(r)) ? list.filter((r) => !isBlankRow(r)) : list)) }
                        ]
                      })
                    ]
                  }),
                  e.jsx("div", { className: "pof__swipe", "aria-hidden": true, children: "Swipe the table sideways for quantity, rate, GST and amount." }),
                  e.jsx("div", {
                    className: "pof__table-wrap",
                    children: e.jsxs("table", {
                      className: "pof__table",
                      children: [
                        e.jsx("thead", {
                          children: e.jsxs("tr", {
                            children: [
                              e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Reorder" }) }),
                              e.jsx("th", { children: "Item details" }),
                              e.jsx("th", { children: "Account" }),
                              e.jsx("th", { className: "po-num", children: "Quantity" }),
                              e.jsx("th", { className: "po-num", children: "Rate (₹)" }),
                              e.jsx("th", { children: "GST" }),
                              e.jsx("th", { className: "po-num", children: "Amount (₹)" }),
                              e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Actions" }) })
                            ]
                          })
                        }),
                        e.jsx("tbody", { children: rows.map(row) })
                      ]
                    })
                  })
                ]
              }),
              err("lines") && e.jsx("div", { className: "field__error", role: "alert", style: { marginTop: 6 }, children: err("lines") }),
              e.jsxs("div", {
                className: "pof__add",
                children: [
                  e.jsxs("button", { type: "button", className: "pof__addbtn", onClick: addRow, children: [e.jsx("span", { "aria-hidden": true, children: "+" }), " Add new row"] }),
                  e.jsxs("button", { type: "button", className: "pof__addbtn", onClick: () => setModal({ kind: "bulk" }), children: [e.jsx("span", { "aria-hidden": true, children: "+" }), " Add items in bulk"] })
                ]
              }),
              e.jsxs("div", {
                className: "pof__lower",
                children: [
                  e.jsx(TextArea, { id: "po-notes", label: "Notes", value: head.remarks, maxLength: 1000, rows: 4, placeholder: "Add any notes or instructions for the supplier.", hint: "These notes will be displayed on the Purchase Order PDF.", error: err("remarks"), onChange: (v) => setH("remarks", v) }),
                  e.jsxs("div", {
                    className: "pof__summary",
                    "aria-label": "Summary",
                    "aria-live": "polite",
                    children: [
                      e.jsxs("div", { className: "pof__sumrow", children: [e.jsx("span", { children: "Sub total" }), e.jsx("span", {}), e.jsx("span", { className: "pof__sumval", children: paise(math.subTotal) })] }),
                      e.jsxs("div", {
                        className: "pof__sumrow",
                        children: [
                          e.jsx("span", { children: "Discount" }),
                          e.jsxs("div", {
                            className: "pof__suminputs",
                            children: [
                              e.jsx("input", { className: `field__input${err("discountValue") ? " field__input--error" : ""}`, "aria-label": "Discount value", "aria-invalid": !!err("discountValue"), inputMode: "decimal", type: "number", min: "0", step: "0.01", placeholder: "0", value: head.discountValue, onChange: (event) => setH("discountValue", event.target.value) }),
                              e.jsxs("select", { className: "field__input", "aria-label": "Discount type", value: head.discountType, onChange: (event) => setH("discountType", event.target.value), children: [e.jsx("option", { value: "1", children: "%" }), e.jsx("option", { value: "2", children: "₹" })] })
                            ]
                          }),
                          e.jsx("span", { className: "pof__sumval", children: math.discount ? `− ${paise(math.discount)}` : "0.00" })
                        ]
                      }),
                      err("discountValue") && e.jsx("div", { className: "field__error", role: "alert", children: err("discountValue") }),
                      e.jsxs("div", {
                        className: "pof__sumrow",
                        children: [
                          e.jsx("span", { children: "TDS" }),
                          e.jsx("div", {
                            className: "pof__suminputs",
                            children: e.jsxs("select", { className: "field__input", "aria-label": "TDS section", value: head.tdsCode, onChange: (event) => setH("tdsCode", event.target.value), children: [e.jsx("option", { value: "", children: "None" }), ...(options?.tdsOptions ?? []).map((t) => e.jsx("option", { value: t.code, children: t.label }, t.code))] })
                          }),
                          e.jsx("span", { className: `pof__sumval${math.tds ? " pof__neg" : ""}`, children: `− ${paise(math.tds)}` })
                        ]
                      }),
                      interState
                        ? e.jsxs("div", { className: "pof__sumrow", children: [e.jsxs("span", { children: ["IGST ", rateLabel(math.rates)] }), e.jsx("span", {}), e.jsx("span", { className: "pof__sumval", children: paise(math.igst) })] })
                        : e.jsxs(e.Fragment, {
                            children: [
                              e.jsxs("div", { className: "pof__sumrow", children: [e.jsxs("span", { children: ["CGST ", rateLabel(math.rates)] }), e.jsx("span", {}), e.jsx("span", { className: "pof__sumval", children: paise(math.cgst) })] }),
                              e.jsxs("div", { className: "pof__sumrow", children: [e.jsxs("span", { children: ["SGST ", rateLabel(math.rates)] }), e.jsx("span", {}), e.jsx("span", { className: "pof__sumval", children: paise(math.sgst) })] })
                            ]
                          }),
                      e.jsxs("div", {
                        className: "pof__sumrow",
                        children: [
                          e.jsx("span", { children: "Adjustment" }),
                          e.jsxs("div", {
                            className: "pof__suminputs",
                            children: [
                              e.jsx("select", { className: "field__input", "aria-label": "Adjustment type", value: head.adjustmentLabel, onChange: (event) => setH("adjustmentLabel", event.target.value), children: ADJUSTMENT_LABELS.map((a) => e.jsx("option", { value: a, children: a }, a)) }),
                              e.jsx("input", {
                                className: `field__input${err("adjustmentAmount") ? " field__input--error" : ""}`,
                                "aria-label": "Adjustment amount",
                                "aria-invalid": !!err("adjustmentAmount"),
                                inputMode: "decimal",
                                placeholder: "0.00",
                                readOnly: rounding,
                                title: rounding ? "Worked out automatically to round the total to the nearest rupee" : "Use a minus sign to reduce the total",
                                value: rounding ? (roundDiff / 100).toFixed(2) : head.adjustmentAmount,
                                onChange: (event) => setH("adjustmentAmount", event.target.value)
                              })
                            ]
                          }),
                          e.jsx("span", { className: "pof__sumval", children: paise(math.adjustment) })
                        ]
                      }),
                      err("adjustmentAmount") && e.jsx("span", { className: "field__error", role: "alert", children: err("adjustmentAmount") }),
                      e.jsxs("div", { className: "pof__total", children: [e.jsx("span", { children: "Total (₹)" }), e.jsx("strong", { children: paise(math.total) })] }),
                      head.isReverseCharge && e.jsxs("div", { className: "pof__rcm", children: ["Reverse charge: GST of ", rupees(math.tax), " is payable by Regalis to the government and is not included in the total."] })
                    ]
                  })
                ]
              })
            ]
          }),

          // Terms & attachments
          e.jsx("section", {
            className: "pof__section",
            "aria-label": "Terms and attachments",
            children: e.jsxs("div", {
              className: "pof__terms",
              children: [
                e.jsx(TextArea, { id: "po-terms", label: "Terms & conditions", required: true, rows: 4, maxLength: 4000, value: head.terms, error: err("terms") ?? err("termsAndConditions"), hint: "Pre-filled from Settings → Billing → Default purchase order terms. Editable for this PO.", onChange: (v) => setH("terms", v) }),
                e.jsxs("div", {
                  className: "pof__attach",
                  children: [
                    e.jsx("div", { className: "field__label", style: { marginBottom: 8 }, children: "Attach file(s) to purchase order" }),
                    e.jsx("input", { ref: fileInput, type: "file", multiple: true, hidden: true, accept: ALLOWED_FILES.join(","), onChange: (event) => { addFiles([...event.target.files]); event.target.value = ""; } }),
                    e.jsxs("button", { type: "button", className: "pof__upload", onClick: () => fileInput.current?.click(), children: [e.jsx(IconUpload, { size: 15 }), " Upload file"] }),
                    e.jsx("div", { className: "pof__hint", style: { margin: "6px 0 10px" }, title: `Allowed: ${ALLOWED_FILES.join(" ")}`, children: `You can upload a maximum of ${options?.maxAttachments ?? 10} files, 10 MB each` }),
                    e.jsxs("div", {
                      className: "po-files",
                      children: [
                        ...(existing?.attachments ?? []).map((a) =>
                          e.jsxs("div", { className: "po-file", children: [e.jsx("span", { className: "po-file__name", children: a.fileName }), e.jsx("span", { className: "po-sub", children: `· ${fileSize(a.fileSizeBytes)}` }), e.jsx("button", { type: "button", className: "po-file__remove", "aria-label": `Remove ${a.fileName}`, onClick: () => removeExisting(a), children: "✕" })] }, `a${a.purchaseOrderAttachmentId}`)
                        ),
                        ...files.map((f) =>
                          e.jsxs("div", { className: "po-file", children: [e.jsx("span", { className: "po-file__name", children: f.file.name }), e.jsx("span", { className: "po-sub", title: "Uploads when the PO is saved", children: `· ${fileSize(f.file.size)}` }), e.jsx("button", { type: "button", className: "po-file__remove", "aria-label": `Remove ${f.file.name}`, onClick: () => setFiles((list) => list.filter((y) => y.key !== f.key)), children: "✕" })] }, f.key)
                        )
                      ]
                    })
                  ]
                })
              ]
            })
          })
        ]
      }),

      // Sticky action bar
      e.jsxs("div", {
        className: "pof__footer",
        children: [
          e.jsxs("span", { className: "pof__footer-note", children: ["Total ", e.jsx("strong", { children: rupees(math.total) }), requireApproval ? " · needs approval before it can be sent" : ""] }),
          e.jsx(x, { variant: "ghost", disabled: !!saving, onClick: () => navigate(editId ? `${BILLING}/${editId}` : BILLING), children: "Cancel" }),
          e.jsx(x, { variant: "outline", loading: saving === "draft", disabled: !!saving, onClick: () => persist("draft"), children: "Save as Draft" }),
          issueLabel && e.jsx(x, { loading: saving === issueMode, disabled: !!saving, onClick: () => persist(issueMode), children: issueLabel })
        ]
      }),

      // Modals
      modal?.kind === "supplier" &&
        e.jsx(SupplierModal, {
          supplier: modal.supplier,
          states,
          onClose: () => setModal(null),
          onSaved: (saved) => {
            setSuppliers((list) => [...list.filter((s) => s.supplierId !== saved.supplierId), saved].sort((a, b) => a.supplierName.localeCompare(b.supplierName)));
            setHead((h) => ({ ...h, supplierId: String(saved.supplierId), supplierText: saved.supplierName }));
            setErrors((errs) => ({ ...errs, supplierId: undefined }));
            setModal(null);
          }
        }),
      modal?.kind === "delivery" &&
        e.jsx(DeliveryModal, {
          locations,
          locationId: head.locationId,
          address: head.deliveryAddress,
          onClose: () => setModal(null),
          onSaved: (locationId, address) => {
            setHead((h) => ({ ...h, locationId: String(locationId), deliveryAddress: address }));
            setErrors((errs) => ({ ...errs, deliveryAddress: undefined }));
            setModal(null);
          }
        }),
      modal?.kind === "bulk" && e.jsx(BulkItemsModal, { items: stock, onRow: (itemId) => hasItem(itemId), onClose: () => setModal(null), onAdd: bulkAdd }),
      modal?.kind === "confirm" && e.jsx(ConfirmModal, { state: modal.state, onClose: () => setModal(null) }),
      detailsRow &&
        e.jsx(G, {
          open: true,
          title: `Item details — ${detailsRow.itemName || `row ${modal.index + 1}`}`,
          onClose: () => setModal(null),
          footer: e.jsx(x, { onClick: () => setModal(null), children: "Done" }),
          children: e.jsxs("div", {
            className: "form-grid",
            children: [
              e.jsx(g, { label: "Unit", placeholder: "Nos", maxLength: 20, value: detailsRow.unit, onChange: (event) => patchRow(modal.index, { unit: event.target.value }) }),
              e.jsx(g, { label: "HSN / SAC", maxLength: 20, value: detailsRow.hsnSacCode, onChange: (event) => patchRow(modal.index, { hsnSacCode: event.target.value }) })
            ]
          })
        })
    ]
  });
}

// ── Receive goods (ne) and cancel (ie): the original minified modals, unchanged ──
function ne({order:i,onClose:p,onReceived:E}){const k=z(),{fieldErrors:u,summary:b,capture:r,clear:y,clearField:h}=Y(),[f,v]=l.useState(!1),j=i.lines.filter(o=>o.pendingQuantity>0),[O,A]=l.useState(new Date().toISOString().slice(0,10)),[R,n]=l.useState(""),[T,m]=l.useState(""),[S,N]=l.useState(Object.fromEntries(j.map(o=>[o.purchaseOrderLineId,String(o.pendingQuantity)]))),w=async()=>{v(!0),y();const o=j.map(D=>({purchaseOrderLineId:D.purchaseOrderLineId,receivedQuantity:Number(S[D.purchaseOrderLineId]??0)})).filter(D=>D.receivedQuantity>0),a={receivedDate:O||null,purchaseInvoiceNumber:R||null,attachmentPath:null,notes:T||null,lines:o};try{const D=await $.receive(i.purchaseOrderId,a);k.success("Goods received and stock updated."),E(D)}catch(D){const t=r(D);t&&k.error(t)}finally{v(!1)}},P=j.some(o=>Number(S[o.purchaseOrderLineId]??0)>0);return e.jsx(G,{open:!0,title:`Receive goods — ${i.poNumber}`,onClose:p,footer:e.jsxs(e.Fragment,{children:[e.jsx(x,{variant:"ghost",onClick:p,children:"Cancel"}),e.jsx(x,{onClick:w,loading:f,disabled:!P,children:"Record receipt"})]}),children:e.jsxs("div",{className:"form-stack",children:[b&&e.jsx("div",{className:"form-error",role:"alert",children:b}),e.jsx("p",{style:{color:"var(--text-muted)"},children:"Each stocked line writes an incoming inventory transaction. Receiving less than ordered leaves the PO partially received."}),e.jsxs("div",{className:"form-grid",children:[e.jsx(g,{label:"Received date",type:"date",required:!0,value:O,error:u.receivedDate,onChange:o=>{h("receivedDate"),A(o.target.value)}}),e.jsx(g,{label:"Supplier invoice no.",value:R,error:u.purchaseInvoiceNumber,onChange:o=>{h("purchaseInvoiceNumber"),n(o.target.value)}})]}),j.length===0?e.jsx("p",{children:"Every line on this purchase order has already been received."}):e.jsx("div",{className:"form-stack",children:j.map(o=>e.jsx(g,{label:`${o.itemName} — ${o.pendingQuantity} ${o.unit??""} pending`.trim(),type:"number",min:"0",max:String(o.pendingQuantity),step:"0.001",value:S[o.purchaseOrderLineId]??"",error:u[`lines[${j.indexOf(o)}].receivedQuantity`],onChange:a=>N(D=>({...D,[o.purchaseOrderLineId]:a.target.value}))},o.purchaseOrderLineId))}),e.jsx(g,{label:"Notes",value:T,error:u.notes,onChange:o=>{h("notes"),m(o.target.value)}})]})})}function ie({open:i,order:p,onClose:E,onCancelled:k}){const u=z(),[b,r]=l.useState(""),[y,h]=l.useState(!1);if(!i)return null;const f=async()=>{h(!0);try{const v=await $.cancel(p.purchaseOrderId,b.trim());u.success("Purchase order cancelled."),k(v)}catch(v){u.error(v instanceof U?v.message:"Could not cancel the PO.")}finally{h(!1)}};return e.jsx(G,{open:!0,title:`Cancel ${p.poNumber}?`,onClose:E,footer:e.jsxs(e.Fragment,{children:[e.jsx(x,{variant:"ghost",onClick:E,children:"Keep it"}),e.jsx(x,{onClick:f,loading:y,disabled:b.trim().length===0,children:"Cancel PO"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsx("p",{children:"A PO with goods already received cannot be cancelled — correct the stock with an Adjustment transaction instead."}),e.jsx(g,{label:"Reason",required:!0,value:b,onChange:v=>r(v.target.value)})]})})}
export{fe as AdminPurchaseOrderDetailPage,be as AdminPurchaseOrdersPage,AdminPurchaseOrderFormPage};
