/**
 * Assets: what the four pages share (Regalis_WorkSpace_Assets_Feature_Specification).
 *
 *   api            every /api/admin/… call the Assets pages make
 *   formatting     rupees in Indian grouping (₹2,05,435), IST dates, quantities
 *   styles         one <style id="as-styles"> for all four pages, built on the app's
 *                  own colour variables so light and dark themes both work
 *   shells         Drawer (item, slides in from the right), Dialog (wide forms)
 *   QR             QrImage (SVG, error correction Q, made in the browser) and the
 *                  A4 LabelSheet with its print panel — only the sheet prints
 *   ScanDialog     a QR sticker read with the camera (BarcodeDetector), or typed /
 *                  read by a USB scanner into the box
 *   ItemPicker     find an item by name or code, or scan it
 *   ImportDialog   the team's asset sheet → one opening-balance receipt (§12)
 *
 * Hand-maintained (this repo ships the built SPA). Imported by AssetItemsPage-AS2.js,
 * AssetGoodsPages-AS1.js and AssetStockPage-AS1.js. The next fix ships as AS2 — see
 * the memory note on hand-written chunks (IIS caches /assets/* for a year).
 */
import {
  a as useToast,
  r as React,
  A as ApiError,
  G as Spinner,
  p as Button,
  F as Select,
  I as Input,
  D as unwrap,
  E as http,
  i as makeIcon
} from "./index-DI5X4c9f.js";
import { qrPath } from "./AssetQr-AS1.js";

export const h = React.createElement;
export const Frag = React.Fragment;

// ── API ─────────────────────────────────────────────────────────────────────

const multipart = { headers: { "Content-Type": "multipart/form-data" } };

/** Downloads a file response; 204 (nothing matched) returns false. */
export const saveBlob = async (url, params, fallbackName) => {
  const response = await http.get(url, { params, responseType: "blob" });
  if (response.status === 204) return false;
  const disposition = response.headers?.["content-disposition"] ?? "";
  const named = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1];
  const href = URL.createObjectURL(response.data);
  const link = document.createElement("a");
  link.href = href;
  link.download = named ? decodeURIComponent(named) : fallbackName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
  return true;
};

export const api = {
  options: () => unwrap(http.get("/admin/items/options")),
  items: (params) => unwrap(http.get("/admin/items", { params })),
  item: (id) => unwrap(http.get(`/admin/items/${id}`)),
  byCode: (code) => unwrap(http.get("/admin/items/by-code", { params: { code } })),
  createItem: (body) => unwrap(http.post("/admin/items", body)),
  updateItem: (id, body) => unwrap(http.put(`/admin/items/${id}`, body)),
  deactivate: (id, reason) => unwrap(http.post(`/admin/items/${id}/deactivate`, { reason })),
  activate: (id) => unwrap(http.post(`/admin/items/${id}/activate`)),
  nextCodes: (prefix, count) => unwrap(http.get("/admin/items/next-codes", { params: { prefix, count } })),
  serialCheck: (serial, excludeItemId) => unwrap(http.get("/admin/items/serial-check", { params: { serial, excludeItemId } })),
  events: (id) => unwrap(http.get(`/admin/items/${id}/events`)),
  repairs: (id) => unwrap(http.get(`/admin/items/${id}/repairs`)),
  closeRepair: (id, repairId, body) => unwrap(http.post(`/admin/items/${id}/repairs/${repairId}/close`, body)),
  documents: (id) => unwrap(http.get(`/admin/items/${id}/documents`)),
  addDocument: (id, file, documentType) => {
    const form = new FormData();
    form.append("file", file);
    form.append("documentType", String(documentType));
    return unwrap(http.post(`/admin/items/${id}/documents`, form, multipart));
  },
  openDocument: async (id, documentId) => {
    const response = await http.get(`/admin/items/${id}/documents/${documentId}`, { responseType: "blob" });
    const href = URL.createObjectURL(response.data);
    window.open(href, "_blank", "noopener");
    setTimeout(() => URL.revokeObjectURL(href), 60000);
  },
  labels: (params) => unwrap(http.get("/admin/items/labels", { params })),
  labelsPrinted: (inventoryItemIds) => unwrap(http.post("/admin/items/labels/printed", { inventoryItemIds })),
  exportItems: (params) => saveBlob("/admin/items/export", params, "assets_items.xlsx"),
  importTemplate: () => saveBlob("/admin/items/import/template", undefined, "asset_list_template.xlsx"),
  importSheet: (file, receiptDate, save) => {
    const form = new FormData();
    form.append("file", file);
    if (receiptDate) form.append("receiptDate", receiptDate);
    return unwrap(http.post(save ? "/admin/items/import" : "/admin/items/import/preview", form, multipart));
  },
  categories: () => unwrap(http.get("/admin/asset-categories")),
  saveCategory: (id, body) => unwrap(id ? http.put(`/admin/asset-categories/${id}`, body) : http.post("/admin/asset-categories", body)),
  locations: () => unwrap(http.get("/admin/asset-locations")),
  saveLocation: (id, body) => unwrap(id ? http.put(`/admin/asset-locations/${id}`, body) : http.post("/admin/asset-locations", body)),
  workspaces: () => unwrap(http.get("/workspaces", { params: { page: 1, pageSize: 100 } })),

  receipts: (params) => unwrap(http.get("/admin/goods-receipts", { params })),
  receipt: (id) => unwrap(http.get(`/admin/goods-receipts/${id}`)),
  createReceipt: (body) => unwrap(http.post("/admin/goods-receipts", body)),
  updateReceipt: (id, body) => unwrap(http.put(`/admin/goods-receipts/${id}`, body)),
  deleteReceipt: (id) => http.delete(`/admin/goods-receipts/${id}`),
  postReceipt: (id, confirmMissingCost) => unwrap(http.post(`/admin/goods-receipts/${id}/post`, { confirmMissingCost })),
  reverseReceipt: (id, reason) => unwrap(http.post(`/admin/goods-receipts/${id}/reverse`, { reason })),
  previewReceipt: (body) => unwrap(http.post("/admin/goods-receipts/preview", body)),
  openPurchaseOrders: (supplierId) => unwrap(http.get("/admin/goods-receipts/open-purchase-orders", { params: { supplierId } })),
  exportReceipts: (params) => saveBlob("/admin/goods-receipts/export", params, "incoming_goods.xlsx"),

  issues: (params) => unwrap(http.get("/admin/goods-issues", { params })),
  issue: (id) => unwrap(http.get(`/admin/goods-issues/${id}`)),
  createIssue: (body) => unwrap(http.post("/admin/goods-issues", body)),
  reverseIssue: (id, reason) => unwrap(http.post(`/admin/goods-issues/${id}/reverse`, { reason })),
  previewIssue: (body) => unwrap(http.post("/admin/goods-issues/preview", body)),
  exportIssues: (params) => saveBlob("/admin/goods-issues/export", params, "outgoing_goods.xlsx"),

  stock: (params) => unwrap(http.get("/admin/stock", { params })),
  assetsByLocation: () => unwrap(http.get("/admin/stock/assets-by-location")),
  ledger: (params) => unwrap(http.get("/admin/stock/ledger", { params })),
  exportStock: (params) => saveBlob("/admin/stock/export", params, "current_stock.xlsx"),
  counts: () => unwrap(http.get("/admin/stock-counts")),
  count: (id) => unwrap(http.get(`/admin/stock-counts/${id}`)),
  startCount: (body) => unwrap(http.post("/admin/stock-counts", body)),
  submitCount: (id, lines) => unwrap(http.post(`/admin/stock-counts/${id}/submit`, { lines })),
  confirmCount: (id) => unwrap(http.post(`/admin/stock-counts/${id}/confirm`)),
  cancelCount: (id) => unwrap(http.post(`/admin/stock-counts/${id}/cancel`))
};

// ── Formatting ──────────────────────────────────────────────────────────────

const rupee0 = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", maximumFractionDigits: 0 });
const rupee2 = new Intl.NumberFormat("en-IN", { style: "currency", currency: "INR", minimumFractionDigits: 2, maximumFractionDigits: 2 });
const number3 = new Intl.NumberFormat("en-IN", { maximumFractionDigits: 3 });
const dayFormat = new Intl.DateTimeFormat("en-IN", { day: "numeric", month: "short", year: "numeric" });
const longDay = new Intl.DateTimeFormat("en-IN", { day: "numeric", month: "long", year: "numeric" });
const dateTime = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", day: "numeric", month: "long", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: false });
const shortDateTime = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit", hour12: true });

/** ₹2,05,435 — whole rupees without paise; ₹45.50 when there are paise; "—" for none. */
export const money = (v, { paise = false } = {}) => {
  if (v == null || Number.isNaN(Number(v))) return "—";
  const n = Number(v);
  return paise || !Number.isInteger(Math.round(n * 100) / 100) ? rupee2.format(n) : rupee0.format(n);
};
export const qty = (v) => (v == null ? "—" : number3.format(Number(v)));
const utc = (s) => new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(s) ? s : `${s}Z`);
/** A DateOnly ("2026-10-15") is a calendar day — never shifted by a time zone. */
const day = (s) => { const [y, m, d] = String(s).slice(0, 10).split("-").map(Number); return new Date(y, m - 1, d); };
export const fmtDay = (s) => (s ? dayFormat.format(day(s)) : "—");
export const fmtLongDay = (s) => (s ? longDay.format(day(s)) : "—");
export const fmtAsAt = (s) => (s ? dateTime.format(utc(s)).replace(" at ", ", ") : "—");
export const fmtWhen = (s) => (s ? shortDateTime.format(utc(s)) : "—");
/** Today in IST as yyyy-mm-dd, for date inputs and their max. */
export const todayIst = () => new Date(Date.now() + 330 * 60000).toISOString().slice(0, 10);
export const daysAgoIst = (n) => new Date(Date.now() + 330 * 60000 - n * 86400000).toISOString().slice(0, 10);

/** A validation envelope says "One or more validation errors…"; the field message is the useful one. */
export const errorText = (error, fallback) =>
  error instanceof ApiError ? error.response?.errors?.[0]?.message ?? error.message ?? fallback : fallback;
export const isCostMissing = (error) => error instanceof ApiError && error.hasCode?.("COST_MISSING");

export function useDebounced(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const id = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(id);
  }, [value, delay]);
  return debounced;
}

// ── Labels for codes ────────────────────────────────────────────────────────

export const STATUS = {
  1: { label: "In store", cls: "store" },
  2: { label: "In use", cls: "use" },
  3: { label: "In repair", cls: "repair" },
  4: { label: "Scrapped", cls: "gone" },
  5: { label: "Lost", cls: "gone" },
  6: { label: "Returned to supplier", cls: "returned" }
};
export const CONDITION = { 1: "Good", 2: "Fair", 3: "Needs attention", 4: "Damaged" };
export const REASON = {
  1: { label: "Used up", cls: "used" },
  2: { label: "Moved", cls: "moved" },
  3: { label: "Sent for repair", cls: "repair" },
  4: { label: "Returned to supplier", cls: "returned" },
  5: { label: "Scrapped", cls: "gone" },
  6: { label: "Lost", cls: "gone" },
  7: { label: "Reversal", cls: "reversal" }
};
export const RECEIPT_STATUS = { 1: { label: "Draft", cls: "draft" }, 2: { label: "Posted", cls: "posted" }, 3: { label: "Imported", cls: "imported" } };
export const DOCUMENT_TYPE = { 1: "Purchase invoice", 2: "Warranty card", 3: "Photo", 4: "Other" };

export const KindTag = ({ kind, service }) =>
  service ? h("span", { className: "as-tag as-tag--service" }, "Service")
    : kind === 2 ? h("span", { className: "as-tag as-tag--asset" }, "Asset")
      : h("span", { className: "as-tag as-tag--consumable" }, "Consumable");
export const StatusPill = ({ status }) =>
  STATUS[status] ? h("span", { className: `as-pill as-pill--${STATUS[status].cls}` }, STATUS[status].label) : null;
export const Pill = ({ cls, children }) => h("span", { className: `as-pill as-pill--${cls}` }, children);

// ── Icons (lucide shapes, drawn by the app's own icon factory) ─────────────

export const IconSearch = makeIcon("search", [["circle", { cx: "11", cy: "11", r: "8", key: "a" }], ["path", { d: "m21 21-4.3-4.3", key: "b" }]]);
export const IconX = makeIcon("x", [["path", { d: "M18 6 6 18", key: "a" }], ["path", { d: "m6 6 12 12", key: "b" }]]);
export const IconQr = makeIcon("qr-code", [
  ["rect", { width: "5", height: "5", x: "3", y: "3", rx: "1", key: "a" }], ["rect", { width: "5", height: "5", x: "16", y: "3", rx: "1", key: "b" }],
  ["rect", { width: "5", height: "5", x: "3", y: "16", rx: "1", key: "c" }], ["path", { d: "M21 16h-3a2 2 0 0 0-2 2v3", key: "d" }],
  ["path", { d: "M21 21v.01", key: "e" }], ["path", { d: "M12 7v3a2 2 0 0 1-2 2H7", key: "f" }], ["path", { d: "M3 12h.01", key: "g" }],
  ["path", { d: "M12 3h.01", key: "h" }], ["path", { d: "M12 16v.01", key: "i" }], ["path", { d: "M16 12h1", key: "j" }],
  ["path", { d: "M21 12v.01", key: "k" }], ["path", { d: "M12 21v-1", key: "l" }]
]);
export const IconScan = makeIcon("scan-line", [
  ["path", { d: "M3 7V5a2 2 0 0 1 2-2h2", key: "a" }], ["path", { d: "M17 3h2a2 2 0 0 1 2 2v2", key: "b" }],
  ["path", { d: "M21 17v2a2 2 0 0 1-2 2h-2", key: "c" }], ["path", { d: "M7 21H5a2 2 0 0 1-2-2v-2", key: "d" }], ["path", { d: "M7 12h10", key: "e" }]
]);
export const IconBox = makeIcon("package", [
  ["path", { d: "M11 21.73a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73z", key: "a" }],
  ["path", { d: "M12 22V12", key: "b" }], ["path", { d: "m3.3 7 7.703 4.734a2 2 0 0 0 2.094 0L20.7 7", key: "c" }]
]);
export const IconCoffee = makeIcon("coffee", [
  ["path", { d: "M10 2v2", key: "a" }], ["path", { d: "M14 2v2", key: "b" }], ["path", { d: "M6 2v2", key: "c" }],
  ["path", { d: "M16 8a1 1 0 0 1 1 1v8a4 4 0 0 1-4 4H7a4 4 0 0 1-4-4V9a1 1 0 0 1 1-1h14a4 4 0 1 1 0 8h-1", key: "d" }]
]);
export const IconChevronLeft = makeIcon("chevron-left", [["path", { d: "m15 18-6-6 6-6", key: "a" }]]);
export const IconChevronRight = makeIcon("chevron-right", [["path", { d: "m9 18 6-6-6-6", key: "a" }]]);

// ── Styles ──────────────────────────────────────────────────────────────────

const STYLE_ID = "as-styles";
const STYLES = `
.as-page{display:flex;flex-direction:column;gap:var(--space-4)}
.as-head{display:flex;justify-content:space-between;align-items:flex-start;gap:var(--space-4);flex-wrap:wrap}
.as-head h2{margin:0;font-family:var(--font-display);font-size:var(--font-size-2xl);color:var(--text-primary)}
.as-head p{margin:6px 0 0;max-width:720px;color:var(--text-secondary);font-size:var(--font-size-sm);line-height:1.5}
.as-head__actions{display:flex;gap:var(--space-2);flex-wrap:wrap;align-items:center}
.as-muted{color:var(--text-muted)}
.as-sub{color:var(--text-secondary);font-size:var(--font-size-sm)}
.as-mono{font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;letter-spacing:.02em}
.as-cards{display:grid;grid-template-columns:repeat(5,minmax(0,1fr));gap:var(--space-3)}
.as-cards--4{grid-template-columns:repeat(4,minmax(0,1fr))}
@media(max-width:1200px){.as-cards{grid-template-columns:repeat(3,minmax(0,1fr))}}
@media(max-width:700px){.as-cards,.as-cards--4{grid-template-columns:repeat(2,minmax(0,1fr))}}
.as-card{text-align:left;border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);padding:var(--space-4) var(--space-5);font:inherit;color:inherit}
button.as-card{cursor:pointer;transition:border-color var(--transition-fast),box-shadow var(--transition-fast)}
button.as-card:hover{border-color:rgb(var(--accent-rgb) / 55%)}
button.as-card:focus-visible{outline:2px solid var(--focus-ring);outline-offset:2px}
.as-card--on{border-color:var(--accent);box-shadow:0 0 0 1px var(--accent)}
.as-card__label{font-size:11px;font-weight:700;letter-spacing:.12em;text-transform:uppercase;color:var(--text-secondary)}
.as-card__value{margin:6px 0 2px;font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:800;color:var(--text-primary);font-variant-numeric:tabular-nums;line-height:1.15}
.as-card__value--red{color:var(--color-danger)}
.as-card__value--gold{color:var(--regalis-gold-dark)}
.as-card__sub{font-size:var(--font-size-xs);color:var(--text-muted);overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.as-panel{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);box-shadow:var(--shadow-sm)}
.as-panel__pad{padding:var(--space-4) var(--space-5)}
.as-filters{display:flex;flex-wrap:wrap;gap:var(--space-2);align-items:flex-end;padding:var(--space-4) var(--space-5) var(--space-3)}
.as-filters .field{margin:0;min-width:150px}
.as-filters .field__label{position:absolute;width:1px;height:1px;overflow:hidden;clip:rect(0 0 0 0)}
.as-search{position:relative;flex:1 1 280px;min-width:220px}
.as-search svg{position:absolute;left:12px;top:50%;transform:translateY(-50%);color:var(--text-muted);pointer-events:none}
.as-search input{padding-left:38px;width:100%}
.as-checks{display:flex;flex-wrap:wrap;gap:var(--space-4);align-items:center;padding:0 var(--space-5) var(--space-3);font-size:var(--font-size-sm)}
.as-checks label{display:inline-flex;gap:8px;align-items:center;cursor:pointer;color:var(--text-primary)}
.as-checks__count{margin-left:auto;color:var(--text-muted)}
.as-chips{display:flex;flex-wrap:wrap;gap:6px;padding:0 var(--space-5) var(--space-3)}
.as-chip{display:inline-flex;align-items:center;gap:6px;border:1px solid var(--border-subtle);background:var(--surface-inset);border-radius:999px;padding:3px 6px 3px 10px;font-size:var(--font-size-xs);color:var(--text-primary)}
.as-chip button{border:0;background:none;color:var(--text-muted);cursor:pointer;padding:0;display:inline-flex}
.as-chip button:hover{color:var(--color-danger)}
.as-table-wrap{position:relative;overflow-x:auto;border-top:1px solid var(--border-subtle)}
.as-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.as-table th{text-align:left;font-size:11px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:var(--text-secondary);padding:12px 14px;border-bottom:1px solid var(--border-subtle);white-space:nowrap;background:var(--surface-card)}
.as-table td{padding:12px 14px;border-bottom:1px solid var(--border-subtle);vertical-align:middle;color:var(--text-primary)}
.as-table tr:last-child td{border-bottom:0}
.as-table .num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
.as-table tbody tr.as-row--click{cursor:pointer}
.as-table tbody tr.as-row--click:hover td{background:var(--surface-hover)}
.as-table tbody tr.as-row--alert td{background:rgb(220 38 38 / 5%)}
.as-table tbody tr.as-row--on td{background:rgb(var(--accent-rgb) / 10%)}
.as-table tbody tr.as-row--muted td{opacity:.62}
.as-table tfoot td{font-weight:700;border-top:2px solid var(--border-subtle)}
.as-cell-main{font-weight:700}
.as-cell-sub{font-size:var(--font-size-xs);color:var(--text-muted);margin-top:2px}
.as-in{color:var(--color-success)}
.as-out{color:var(--color-danger)}
.as-costless{color:var(--color-danger)}
.as-link{border:0;background:none;padding:0;font:inherit;font-weight:700;color:var(--regalis-gold-dark);cursor:pointer}
.as-link:hover{text-decoration:underline}
.as-link:disabled{color:var(--text-muted);cursor:not-allowed;text-decoration:none}
.as-footer{display:flex;justify-content:space-between;align-items:center;gap:var(--space-3);padding:var(--space-3) var(--space-5);border-top:1px solid var(--border-subtle);font-size:var(--font-size-sm);color:var(--text-secondary);flex-wrap:wrap}
.as-pager{display:flex;gap:6px}
.as-note{border:1px solid var(--border-subtle);border-left:4px solid var(--regalis-gold-dark);border-radius:var(--radius-md);background:var(--surface-card);padding:var(--space-3) var(--space-4);font-size:var(--font-size-sm);color:var(--text-secondary)}
.as-note strong{color:var(--text-primary)}
.as-note--blue{border-left-color:var(--color-info);background:rgb(37 99 235 / 6%)}
.as-note--amber{border-left-color:var(--color-warning);background:rgb(217 119 6 / 9%);color:var(--text-primary)}
.as-note--red{border-left-color:var(--color-danger);background:rgb(220 38 38 / 7%);color:var(--text-primary)}
.as-note--green{border-left-color:var(--color-success);background:rgb(22 163 74 / 8%);color:var(--text-primary)}
.as-tag,.as-pill{display:inline-flex;align-items:center;white-space:nowrap;border-radius:6px;padding:3px 9px;font-size:var(--font-size-xs);font-weight:700;line-height:1.3}
.as-pill{border-radius:999px}
.as-tag--asset{background:rgb(37 99 235 / 12%);color:#2754a8}
.as-tag--consumable{background:rgb(var(--accent-rgb) / 20%);color:var(--regalis-gold-dark)}
.as-tag--service{background:var(--surface-inset);color:var(--text-secondary)}
[data-theme=dark] .as-tag--asset{color:#8fb0f0}
.as-pill--use,.as-pill--posted,.as-pill--fine{background:rgb(22 163 74 / 13%);color:#166534}
.as-pill--store,.as-pill--imported,.as-pill--moved{background:rgb(37 99 235 / 12%);color:#2754a8}
.as-pill--repair,.as-pill--draft{background:rgb(217 119 6 / 15%);color:#92400e}
.as-pill--gone,.as-pill--order{background:rgb(220 38 38 / 12%);color:#b42318}
.as-pill--returned,.as-pill--used,.as-pill--reversal,.as-pill--inactive{background:var(--surface-inset);color:var(--text-secondary)}
[data-theme=dark] .as-pill--use,[data-theme=dark] .as-pill--posted,[data-theme=dark] .as-pill--fine{color:#6fd391}
[data-theme=dark] .as-pill--store,[data-theme=dark] .as-pill--imported,[data-theme=dark] .as-pill--moved{color:#8fb0f0}
[data-theme=dark] .as-pill--repair,[data-theme=dark] .as-pill--draft{color:#f3b05a}
[data-theme=dark] .as-pill--gone,[data-theme=dark] .as-pill--order{color:#f08b80}
.as-bulk{display:flex;align-items:center;gap:var(--space-2);flex-wrap:wrap;padding:var(--space-3) var(--space-5);background:rgb(var(--accent-rgb) / 12%);border-top:1px solid var(--border-subtle);font-size:var(--font-size-sm)}
.as-bulk strong{margin-right:var(--space-2)}
.as-overlay{position:fixed;inset:0;z-index:1000;background:rgb(10 10 12 / 45%);display:flex}
.as-drawer{margin-left:auto;width:min(980px,100%);height:100%;background:var(--surface-page);box-shadow:var(--shadow-lg);display:flex;flex-direction:column;animation:as-slide .18s ease-out}
@keyframes as-slide{from{transform:translateX(40px);opacity:.4}to{transform:none;opacity:1}}
.as-drawer__head{display:flex;gap:var(--space-4);align-items:flex-start;padding:var(--space-5) var(--space-6);background:var(--surface-card);border-bottom:1px solid var(--border-subtle)}
.as-drawer__icon{width:48px;height:48px;border-radius:12px;display:flex;align-items:center;justify-content:center;background:rgb(var(--accent-rgb) / 18%);color:var(--regalis-gold-dark);flex:none}
.as-drawer__title{margin:0;font-family:var(--font-display);font-size:var(--font-size-xl);color:var(--text-primary)}
.as-drawer__actions{margin-left:auto;display:flex;gap:var(--space-2);flex-wrap:wrap;align-items:center;justify-content:flex-end}
.as-drawer__body{flex:1;overflow:auto;padding:var(--space-5) var(--space-6)}
.as-drawer__foot{display:flex;justify-content:space-between;align-items:center;padding:var(--space-3) var(--space-6);border-top:1px solid var(--border-subtle);background:var(--surface-card);font-size:var(--font-size-sm);color:var(--text-secondary)}
.as-close{border:0;background:none;color:var(--text-muted);cursor:pointer;padding:6px;border-radius:8px;display:inline-flex}
.as-close:hover{background:var(--surface-hover);color:var(--text-primary)}
.as-tabs{display:flex;gap:4px;flex-wrap:wrap;margin-bottom:var(--space-4)}
.as-tab{border:1px solid transparent;background:none;border-radius:10px;padding:8px 16px;font:inherit;font-weight:600;color:var(--text-secondary);cursor:pointer}
.as-tab:hover{color:var(--text-primary)}
.as-tab--on{background:rgb(var(--accent-rgb) / 18%);border-color:rgb(var(--accent-rgb) / 45%);color:var(--regalis-gold-dark)}
.as-grid{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:var(--space-4) var(--space-5)}
@media(max-width:800px){.as-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
.as-kv__k{font-size:11px;font-weight:700;letter-spacing:.1em;text-transform:uppercase;color:var(--text-secondary);margin-bottom:4px}
.as-kv__v{color:var(--text-primary);overflow-wrap:anywhere}
.as-kv__v--missing{color:var(--color-danger)}
.as-overview{display:grid;grid-template-columns:minmax(0,1fr) 300px;gap:var(--space-5)}
@media(max-width:860px){.as-overview{grid-template-columns:1fr}}
.as-qrbox{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);padding:var(--space-4);margin-bottom:var(--space-4)}
.as-qrbox svg{display:block;margin:var(--space-3) auto;width:170px;height:170px;background:#fff;border-radius:6px}
.as-qrtext{white-space:pre-wrap;font-family:ui-monospace,SFMono-Regular,Menlo,Consolas,monospace;font-size:12px;line-height:1.6;color:var(--text-primary);margin:var(--space-2) 0}
.as-section-title{font-family:var(--font-display);font-size:var(--font-size-lg);margin:var(--space-5) 0 var(--space-2);color:var(--text-primary)}
.as-dialog{margin:auto;width:min(760px,calc(100% - 24px));max-height:calc(100% - 32px);display:flex;flex-direction:column;background:var(--surface-card);border-radius:var(--radius-lg);box-shadow:var(--shadow-lg)}
.as-dialog--wide{width:min(1080px,calc(100% - 24px))}
.as-dialog__head{display:flex;align-items:center;justify-content:space-between;padding:var(--space-4) var(--space-5);border-bottom:1px solid var(--border-subtle)}
.as-dialog__head h3{margin:0;font-family:var(--font-display);font-size:var(--font-size-xl);color:var(--text-primary)}
.as-dialog__body{overflow:auto;padding:var(--space-5)}
.as-dialog__foot{display:flex;justify-content:flex-end;gap:var(--space-2);align-items:center;padding:var(--space-3) var(--space-5);border-top:1px solid var(--border-subtle);flex-wrap:wrap}
.as-dialog__foot .as-grow{margin-right:auto;font-size:var(--font-size-sm);color:var(--text-secondary)}
.as-form{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:var(--space-3) var(--space-4)}
.as-form .field{margin:0}
.as-form .as-span2{grid-column:1/-1}
@media(max-width:640px){.as-form{grid-template-columns:1fr}}
.as-textarea{width:100%;min-height:76px;resize:vertical}
.as-kind{border:1px solid rgb(var(--accent-rgb) / 35%);border-radius:var(--radius-lg);background:rgb(var(--accent-rgb) / 6%);padding:var(--space-4);margin-bottom:var(--space-4)}
.as-kind__q{font-weight:700;margin-bottom:var(--space-3);color:var(--text-primary)}
.as-kind__opts{display:grid;grid-template-columns:1fr 1fr;gap:var(--space-3)}
.as-kind__opt{text-align:left;border:1.5px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);padding:var(--space-3) var(--space-4);cursor:pointer;font:inherit;color:var(--text-primary)}
.as-kind__opt strong{display:block;font-size:var(--font-size-md)}
.as-kind__opt span{display:block;margin-top:2px;font-size:var(--font-size-sm);color:var(--text-secondary)}
.as-kind__opt--on{border-color:var(--regalis-gold-dark);background:rgb(var(--accent-rgb) / 16%)}
.as-kind__opt--on strong{color:var(--regalis-gold-dark)}
.as-kind__opt:disabled{cursor:not-allowed;opacity:.55}
.as-reasons{display:flex;flex-wrap:wrap;gap:8px}
.as-reason{border:1.5px solid var(--border-subtle);border-radius:999px;background:var(--surface-card);padding:7px 16px;font:inherit;font-weight:600;color:var(--text-primary);cursor:pointer}
.as-reason--on{border-color:var(--regalis-gold-dark);background:rgb(var(--accent-rgb) / 18%);color:var(--regalis-gold-dark)}
.as-split{display:grid;grid-template-columns:minmax(0,1fr) 460px;gap:var(--space-4);align-items:start}
@media(max-width:1180px){.as-split{grid-template-columns:1fr}}
.as-side{position:sticky;top:var(--space-4)}
.as-side__head{display:flex;justify-content:space-between;align-items:center;padding:var(--space-4) var(--space-5);border-bottom:1px solid var(--border-subtle)}
.as-side__head h3{margin:0;font-family:var(--font-display);font-size:var(--font-size-xl);color:var(--text-primary)}
.as-side__body{padding:var(--space-4) var(--space-5);display:flex;flex-direction:column;gap:var(--space-3)}
.as-side__foot{display:flex;justify-content:flex-end;gap:var(--space-2);align-items:center;padding:var(--space-3) var(--space-5);border-top:1px solid var(--border-subtle);flex-wrap:wrap}
.as-line{border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3);background:var(--surface-card)}
.as-line__head{display:flex;justify-content:space-between;gap:var(--space-2);align-items:flex-start}
.as-line__grid{display:grid;grid-template-columns:1fr 1fr 1fr;gap:var(--space-2);margin-top:var(--space-2);align-items:end}
.as-line__amount{font-weight:800;padding-bottom:10px;font-variant-numeric:tabular-nums}
.as-line__says{margin-top:var(--space-2);border-radius:var(--radius-sm);padding:8px 12px;font-size:var(--font-size-sm)}
.as-line__says--asset{background:rgb(var(--accent-rgb) / 14%);color:var(--text-primary)}
.as-line__says--stock{background:rgb(37 99 235 / 8%);color:var(--text-primary)}
.as-line__says--warn{background:rgb(217 119 6 / 12%);color:var(--text-primary)}
.as-line__says--bad{background:rgb(220 38 38 / 9%);color:var(--color-danger)}
.as-total{display:flex;justify-content:space-between;align-items:center;border-top:1px solid var(--border-subtle);padding-top:var(--space-3)}
.as-total strong{font-family:var(--font-display);font-size:var(--font-size-xl)}
.as-remove{border:0;background:none;color:var(--color-danger);font:inherit;font-size:var(--font-size-sm);cursor:pointer}
.as-picker{position:relative}
.as-picker__list{position:absolute;z-index:30;left:0;right:0;top:100%;margin-top:4px;max-height:280px;overflow:auto;background:var(--surface-card);border:1px solid var(--border-strong);border-radius:var(--radius-md);box-shadow:var(--shadow-md)}
.as-picker__opt{display:block;width:100%;text-align:left;border:0;background:none;padding:9px 12px;font:inherit;color:var(--text-primary);cursor:pointer;border-bottom:1px solid var(--border-subtle)}
.as-picker__opt:hover,.as-picker__opt--on{background:var(--surface-hover)}
.as-picker__row{display:flex;gap:8px;align-items:flex-end}
.as-picker__row .field{flex:1;margin:0}
.as-skeleton{display:block;height:14px;border-radius:6px;background:linear-gradient(90deg,var(--surface-inset),var(--surface-hover),var(--surface-inset));background-size:200% 100%;animation:as-shimmer 1.2s infinite}
@keyframes as-shimmer{to{background-position:-200% 0}}
.as-empty{padding:var(--space-10) var(--space-5);text-align:center;color:var(--text-secondary)}
.as-empty strong{display:block;font-size:var(--font-size-lg);color:var(--text-primary);margin-bottom:var(--space-3)}
.as-empty div{display:flex;justify-content:center;gap:var(--space-2);flex-wrap:wrap}
.as-error{padding:var(--space-6);text-align:center;color:var(--text-primary)}
.as-video{width:100%;max-height:340px;background:#000;border-radius:var(--radius-md);object-fit:cover}
.as-labels{display:grid;grid-template-columns:300px minmax(0,1fr);gap:var(--space-4);align-items:start}
@media(max-width:860px){.as-labels{grid-template-columns:1fr}}
.as-print-panel{display:flex;flex-direction:column;gap:var(--space-3)}
.as-print-panel fieldset{border:0;padding:0;margin:0;display:flex;flex-direction:column;gap:6px}
.as-print-panel legend{font-weight:700;margin-bottom:6px;color:var(--text-primary)}
.as-print-panel label{display:flex;gap:8px;align-items:center;font-size:var(--font-size-sm);color:var(--text-primary);cursor:pointer}
.as-sheet-preview{background:var(--surface-inset);border-radius:var(--radius-md);padding:var(--space-4);overflow:auto;max-height:70vh}
.as-sheet{width:210mm;height:297mm;padding:8mm;box-sizing:border-box;background:#fff;color:#111;margin:0 auto var(--space-4);box-shadow:var(--shadow-md);display:grid;gap:0;transform-origin:top left}
.as-sheet--large{grid-template-columns:repeat(3,1fr);grid-template-rows:repeat(8,1fr)}
.as-sheet--small{grid-template-columns:repeat(4,1fr);grid-template-rows:repeat(10,1fr)}
.as-label{display:flex;gap:2.5mm;align-items:center;padding:2mm 2.5mm;border:0.2mm dashed #c9b994;overflow:hidden;box-sizing:border-box;font-family:Arial,Helvetica,sans-serif}
.as-label svg{flex:none;background:#fff}
.as-sheet--large .as-label svg{width:26mm;height:26mm}
.as-sheet--small .as-label svg{width:20mm;height:20mm}
.as-label__text{min-width:0;line-height:1.2}
.as-label__brand{font-size:5.5pt;font-weight:700;letter-spacing:.14em;color:#6b5a32}
.as-label__code{font-family:"Courier New",ui-monospace,monospace;font-weight:700;color:#111;white-space:nowrap}
.as-sheet--large .as-label__code{font-size:9pt}
.as-sheet--small .as-label__code{font-size:7pt}
.as-label__name{font-size:7pt;color:#111;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.as-label__where{font-size:5.5pt;color:#555;overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.as-sheet--small .as-label__name{font-size:6pt}
#as-print-root{display:none}
@media print{
  @page{size:A4;margin:0}
  body>*:not(#as-print-root){display:none!important}
  #as-print-root{display:block!important}
  #as-print-root .as-sheet{box-shadow:none;margin:0;page-break-after:always;break-after:page}
  #as-print-root .as-label{border-color:#e4dcc6}
}
`;

export function useAssetStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }, []);
}

// ── Small building blocks ───────────────────────────────────────────────────

export const KV = ({ k, v, missing }) =>
  h("div", null,
    h("div", { className: "as-kv__k" }, k),
    h("div", { className: `as-kv__v${missing ? " as-kv__v--missing" : ""}` }, missing ?? (v == null || v === "" ? "—" : v)));

export const SummaryCard = ({ label, value, sub, tone, active, onClick }) =>
  h(onClick ? "button" : "div", {
    type: onClick ? "button" : undefined,
    className: `as-card${active ? " as-card--on" : ""}`,
    onClick,
    "aria-pressed": onClick ? !!active : undefined
  },
    h("div", { className: "as-card__label" }, label),
    h("div", { className: `as-card__value${tone ? ` as-card__value--${tone}` : ""}` }, value),
    h("div", { className: "as-card__sub", title: typeof sub === "string" ? sub : undefined }, sub || " "));

export const SkeletonRows = ({ rows = 6, cols = 6 }) =>
  h("tbody", null, Array.from({ length: rows }, (_, r) =>
    h("tr", { key: r }, Array.from({ length: cols }, (_, c) => h("td", { key: c }, h("span", { className: "as-skeleton", style: { width: `${50 + ((r * 7 + c * 13) % 40)}%` } }))))));

export const LoadFailed = ({ onRetry }) =>
  h("div", { className: "as-error" }, h("p", null, "Could not load."), h(Button, { variant: "outline", onClick: onRetry }, "Retry"));

export const TextArea = ({ label, value, onChange, required, placeholder, hint, rows = 3 }) =>
  h("div", { className: "field" },
    h("label", { className: "field__label" }, label, required ? h("span", { className: "field__required", "aria-hidden": true }, " *") : null),
    h("textarea", { className: "field__input as-textarea", rows, value: value ?? "", placeholder, onChange: (ev) => onChange(ev.target.value) }),
    hint ? h("span", { className: "field__hint" }, hint) : null);

/** Esc closes; the page behind does not scroll while it is open. */
function useOverlay(onClose) {
  React.useEffect(() => {
    const onKey = (ev) => { if (ev.key === "Escape") onClose?.(); };
    document.addEventListener("keydown", onKey);
    const overflow = document.body.style.overflow;
    document.body.style.overflow = "hidden";
    return () => { document.removeEventListener("keydown", onKey); document.body.style.overflow = overflow; };
  }, [onClose]);
}

export function Dialog({ title, onClose, footer, wide, children }) {
  useOverlay(onClose);
  return h("div", { className: "as-overlay", role: "dialog", "aria-modal": true, "aria-label": title, onMouseDown: (ev) => { if (ev.target === ev.currentTarget) onClose?.(); } },
    h("div", { className: `as-dialog${wide ? " as-dialog--wide" : ""}` },
      h("div", { className: "as-dialog__head" }, h("h3", null, title), h("button", { type: "button", className: "as-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 20 }))),
      h("div", { className: "as-dialog__body" }, children),
      footer ? h("div", { className: "as-dialog__foot" }, footer) : null));
}

export function Drawer({ label, onClose, children }) {
  useOverlay(onClose);
  return h("div", { className: "as-overlay", role: "dialog", "aria-modal": true, "aria-label": label, onMouseDown: (ev) => { if (ev.target === ev.currentTarget) onClose?.(); } },
    h("aside", { className: "as-drawer" }, children));
}

/** A reason the admin must give before something changes history. */
export function ReasonDialog({ title, intro, confirm, onClose, onSubmit }) {
  const toast = useToast();
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    if (!reason.trim()) { toast.error("Say why — the trail keeps it."); return; }
    setBusy(true);
    try { await onSubmit(reason.trim()); } catch (error) { toast.error(errorText(error, "That did not work.")); } finally { setBusy(false); }
  };
  return h(Dialog, {
    title, onClose,
    footer: h(Frag, null, h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { variant: "danger", onClick: submit, loading: busy }, confirm))
  },
    h("p", { className: "as-sub", style: { marginTop: 0 } }, intro),
    h(TextArea, { label: "Reason", required: true, value: reason, onChange: setReason, placeholder: "What went wrong" }));
}

// ── QR ──────────────────────────────────────────────────────────────────────

const qrCache = new Map();
const qrOf = (text) => {
  if (!qrCache.has(text)) {
    if (qrCache.size > 600) qrCache.clear();
    qrCache.set(text, qrPath(text, "Q", 2));
  }
  return qrCache.get(text);
};

/** The sticker's code as SVG: sharp at any size, made here with no server round-trip. */
export function QrImage({ text, title }) {
  const qr = qrOf(text);
  return h("svg", { viewBox: `0 0 ${qr.viewBox} ${qr.viewBox}`, role: "img", "aria-label": title ?? "QR code", shapeRendering: "crispEdges" },
    h("rect", { width: qr.viewBox, height: qr.viewBox, fill: "#fff" }),
    h("path", { d: qr.d, fill: "#111" }));
}

const chunk = (list, size) => Array.from({ length: Math.ceil(list.length / size) }, (_, i) => list.slice(i * size, i * size + size));

const esc = (s) => String(s ?? "").replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]);
const labelLines = (l, include) => [
  include.name ? `<div class="as-label__name">${esc(l.itemName)}</div>` : "",
  include.brand && l.brandModel ? `<div class="as-label__where">${esc(l.brandModel)}</div>` : "",
  include.serial && l.serialNumber ? `<div class="as-label__where">S/N ${esc(l.serialNumber)}</div>` : "",
  `<div class="as-label__where">${esc([include.where ? l.locationName : null, "Property of Regalis"].filter(Boolean).join(" · "))}</div>`
].join("");

/** The same sheet as plain HTML, for #as-print-root: what the printer gets. */
function sheetHtml(labels, size, include) {
  return `<div class="as-sheet as-sheet--${size}">` + labels.map((l) => {
    const qr = qrOf(l.qrText);
    return `<div class="as-label"><svg viewBox="0 0 ${qr.viewBox} ${qr.viewBox}" shape-rendering="crispEdges">` +
      `<rect width="${qr.viewBox}" height="${qr.viewBox}" fill="#fff"/><path d="${qr.d}" fill="#111"/></svg>` +
      `<div class="as-label__text"><div class="as-label__brand">REGALIS WS</div><div class="as-label__code">${esc(l.itemCode)}</div>` +
      labelLines(l, include) + `</div></div>`;
  }).join("") + `</div>`;
}

function Sheet({ labels, size, include }) {
  return h("div", { className: `as-sheet as-sheet--${size}` },
    labels.map((l) => h("div", { className: "as-label", key: l.inventoryItemId },
      h(QrImage, { text: l.qrText, title: `QR code for ${l.itemCode}` }),
      h("div", { className: "as-label__text" },
        h("div", { className: "as-label__brand" }, "REGALIS WS"),
        h("div", { className: "as-label__code" }, l.itemCode),
        include.name ? h("div", { className: "as-label__name" }, l.itemName) : null,
        include.brand && l.brandModel ? h("div", { className: "as-label__where" }, l.brandModel) : null,
        include.serial && l.serialNumber ? h("div", { className: "as-label__where" }, `S/N ${l.serialNumber}`) : null,
        h("div", { className: "as-label__where" }, [include.where ? l.locationName : null, "Property of Regalis"].filter(Boolean).join(" · "))))));
}

/**
 * The label sheet (§9.2, §9.3). Large: 3 × 8 = 24 a page, QR about 26 mm. Small: 4 × 10 =
 * 40 a page, QR about 20 mm. A4, 8 mm margins. Printing renders the sheets into
 * #as-print-root, and the print stylesheet hides everything else — no panel, no sidebar.
 */
export function LabelSheetDialog({ params, title = "QR labels", onClose, onPrinted }) {
  const toast = useToast();
  const [sheet, setSheet] = React.useState(null);
  const [failed, setFailed] = React.useState(false);
  const [size, setSize] = React.useState("large");
  const [include, setInclude] = React.useState({ name: true, where: true, brand: false, serial: false });
  const [printing, setPrinting] = React.useState(false);
  const load = React.useCallback(async () => {
    setFailed(false);
    try { setSheet(await api.labels(params)); } catch (error) { setFailed(true); toast.error(errorText(error, "Could not load the labels.")); }
  }, [params]);
  React.useEffect(() => { load(); }, [load]);

  const perPage = size === "large" ? 24 : 40;
  const pages = sheet ? chunk(sheet.labels, perPage) : [];
  const skipped = sheet ? sheet.skippedConsumables : 0;
  const message = sheet
    ? `${sheet.labels.length} label${sheet.labels.length === 1 ? "" : "s"} ready.` +
      (skipped ? ` ${skipped} consumable${skipped === 1 ? " was" : "s were"} skipped — they have no sticker.` : "") +
      (sheet.skippedRetired ? ` ${sheet.skippedRetired} retired asset${sheet.skippedRetired === 1 ? " was" : "s were"} skipped.` : "")
    : "";

  const print = async (asPdf) => {
    if (!sheet || sheet.labels.length === 0) return;
    setPrinting(true);
    let root = document.getElementById("as-print-root");
    if (!root) { root = document.createElement("div"); root.id = "as-print-root"; document.body.appendChild(root); }
    root.innerHTML = pages.map((p) => sheetHtml(p, size, include)).join("");
    const done = async () => {
      window.removeEventListener("afterprint", done);
      root.innerHTML = "";
      setPrinting(false);
      // Recorded once the print dialogue was offered: it is what empties "No QR printed".
      try {
        await api.labelsPrinted(sheet.labels.map((l) => l.inventoryItemId));
        onPrinted?.();
        toast.success(`${sheet.labels.length} label${sheet.labels.length === 1 ? "" : "s"} recorded as printed.`);
      } catch (error) { toast.error(errorText(error, "Could not record the printing.")); }
    };
    window.addEventListener("afterprint", done);
    if (asPdf) toast.info?.("In the print dialogue, choose \"Save as PDF\" as the printer.");
    setTimeout(() => window.print(), 120);
  };

  return h(Dialog, {
    title, onClose, wide: true,
    footer: h(Frag, null, h("span", { className: "as-grow" }, message), h(Button, { variant: "ghost", onClick: onClose }, "Close"))
  },
    failed ? h(LoadFailed, { onRetry: load })
      : !sheet ? h(Spinner, { center: true })
        : h("div", { className: "as-labels" },
          h("div", { className: "as-print-panel as-panel as-panel__pad" },
            h("fieldset", null, h("legend", null, "Label size"),
              h("label", null, h("input", { type: "radio", name: "as-size", checked: size === "large", onChange: () => setSize("large") }), "Large — 24 a sheet, QR about 26 mm"),
              h("label", null, h("input", { type: "radio", name: "as-size", checked: size === "small", onChange: () => setSize("small") }), "Small — 40 a sheet, QR about 20 mm")),
            h("fieldset", null, h("legend", null, "What to include"),
              [["name", "Item name"], ["where", "Where it belongs"], ["brand", "Brand"], ["serial", "Serial number"]].map(([key, label]) =>
                h("label", { key }, h("input", { type: "checkbox", checked: include[key], onChange: (ev) => setInclude({ ...include, [key]: ev.target.checked }) }), label))),
            h("p", { className: "as-sub", style: { margin: 0 } }, "A4, 8 mm margins. Only the sheet prints. A crowded label is harder to read."),
            h(Button, { onClick: () => print(false), loading: printing, disabled: sheet.labels.length === 0 }, "Print"),
            h(Button, { variant: "outline", onClick: () => print(true), disabled: printing || sheet.labels.length === 0 }, "Save PDF"),
            h("p", { className: "as-sub", style: { margin: 0 } }, "Reprinting is always allowed — the code never changes, so a reprint is identical.")),
          h("div", { className: "as-sheet-preview" },
            sheet.labels.length === 0
              ? h("div", { className: "as-empty" }, h("strong", null, "No labels to print"), "Only assets in the register get a sticker.")
              : pages.map((p, i) => h("div", { key: i },
                h("div", { className: "as-sub", style: { margin: "0 0 6px" } }, `A4 sheet ${i + 1} of ${pages.length} · ${perPage} labels a page`),
                h("div", { style: { width: "calc(210mm * 0.62)", height: "calc(297mm * 0.62)", overflow: "hidden", margin: "0 auto 16px" } },
                  h("div", { style: { transform: "scale(0.62)", transformOrigin: "top left" } }, h(Sheet, { labels: p, size, include }))))))));
}

// ── Scanning ────────────────────────────────────────────────────────────────

/** The code inside a scanned sticker ("Code: …" line), or a bare code. */
export const codeFromScan = (text) => {
  const line = String(text || "").split(/\r?\n/).map((l) => l.trim()).find((l) => /^code:/i.test(l));
  return (line ? line.slice(5) : String(text || "")).trim().toUpperCase();
};

/**
 * Point the phone camera at a sticker (BarcodeDetector: Chrome on Android), or type the
 * code — a USB scanner types it into the box too. Calls onFound(item).
 */
export function ScanDialog({ onClose, onFound, title = "Scan a QR sticker" }) {
  const toast = useToast();
  const videoRef = React.useRef(null);
  const [typed, setTyped] = React.useState("");
  const [camera, setCamera] = React.useState("starting");
  const [busy, setBusy] = React.useState(false);
  const busyRef = React.useRef(false);
  const lookUp = React.useCallback(async (text) => {
    const code = codeFromScan(text);
    if (!code || busyRef.current) return;
    busyRef.current = true;
    setBusy(true);
    try { onFound(await api.byCode(text.includes("\n") ? text : code)); }
    catch (error) { toast.error(error instanceof ApiError && error.status === 404 ? `No item has the code ${code}.` : errorText(error, "Could not look that code up.")); }
    finally { busyRef.current = false; setBusy(false); }
  }, [onFound]);

  React.useEffect(() => {
    let stream, timer, stopped = false;
    (async () => {
      if (!("BarcodeDetector" in window) || !navigator.mediaDevices?.getUserMedia) { setCamera("unsupported"); return; }
      try {
        const detector = new window.BarcodeDetector({ formats: ["qr_code"] });
        stream = await navigator.mediaDevices.getUserMedia({ video: { facingMode: "environment" } });
        if (stopped) { stream.getTracks().forEach((t) => t.stop()); return; }
        videoRef.current.srcObject = stream;
        await videoRef.current.play();
        setCamera("on");
        const tick = async () => {
          if (stopped) return;
          try {
            const found = await detector.detect(videoRef.current);
            if (found[0]?.rawValue) { await lookUp(found[0].rawValue); return; }
          } catch { /* a frame that cannot be read */ }
          timer = setTimeout(tick, 300);
        };
        tick();
      } catch { setCamera("denied"); }
    })();
    return () => { stopped = true; clearTimeout(timer); stream?.getTracks().forEach((t) => t.stop()); };
  }, [lookUp]);

  return h(Dialog, {
    title, onClose,
    footer: h(Frag, null, h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { onClick: () => lookUp(typed), loading: busy, disabled: !typed.trim() }, "Find"))
  },
    camera === "unsupported"
      ? h("div", { className: "as-note as-note--blue" }, "This browser cannot read QR codes with the camera. Chrome on an Android phone can. Type the code below, or click in the box and use a USB scanner.")
      : camera === "denied"
        ? h("div", { className: "as-note as-note--amber" }, "The camera is not available. Allow camera access, or type the code below.")
        : h("video", { ref: videoRef, className: "as-video", muted: true, playsInline: true }),
    h("div", { style: { marginTop: "var(--space-3)" } },
      h(Input, { label: "Code", value: typed, autoFocus: camera !== "on", placeholder: "R-2W-E-EAC-001", onChange: (ev) => setTyped(ev.target.value),
        onKeyDown: (ev) => { if (ev.key === "Enter") { ev.preventDefault(); lookUp(typed); } } })));
}

// ── Finding an item ─────────────────────────────────────────────────────────

/**
 * Type a name or code, or scan the sticker. `kind` limits the list (1 consumables,
 * 2 assets). Calls onPick(row) with an Items list row.
 */
export function ItemPicker({ label = "Item", required, kind, value, onPick, placeholder = "Type a name or code", hint, allowScan = true, extraParams }) {
  const [text, setText] = React.useState("");
  const [open, setOpen] = React.useState(false);
  const [rows, setRows] = React.useState([]);
  const [scan, setScan] = React.useState(false);
  const search = useDebounced(text, 250);
  React.useEffect(() => {
    if (!open) return;
    let live = true;
    api.items({ search: search || undefined, kind, ...extraParams }).then((r) => { if (live) setRows(r.page.items); }).catch(() => {});
    return () => { live = false; };
  }, [search, open, kind]);
  const pick = (row) => { onPick(row); setText(""); setOpen(false); };
  return h("div", { className: "as-picker" },
    h("div", { className: "as-picker__row" },
      h(Input, {
        label, required, value: open ? text : value ? `${value.itemName} — ${value.itemCode}` : text, placeholder,
        onFocus: () => setOpen(true), onBlur: () => setTimeout(() => setOpen(false), 180),
        onChange: (ev) => { setText(ev.target.value); setOpen(true); }
      }),
      allowScan ? h(Button, { variant: "outline", onClick: () => setScan(true), "aria-label": "Scan a QR sticker" }, h(IconScan, { size: 18 })) : null),
    hint ? h("span", { className: "field__hint" }, hint) : null,
    open && rows.length > 0 ? h("div", { className: "as-picker__list", role: "listbox" },
      rows.map((r) => h("button", { type: "button", key: r.inventoryItemId, className: "as-picker__opt", onMouseDown: (ev) => ev.preventDefault(), onClick: () => pick(r) },
        h("div", { className: "as-cell-main" }, r.itemName, " ", h(KindTag, { kind: r.kind, service: r.isService })),
        h("div", { className: "as-cell-sub as-mono" }, [r.itemCode, r.kind === 2 ? STATUS[r.status]?.label : r.onHand != null ? `${qty(r.onHand)} ${r.unit} on hand` : null, r.locationName].filter(Boolean).join(" · "))))) : null,
    scan ? h(ScanDialog, { onClose: () => setScan(false), onFound: (item) => { setScan(false); pick({ ...item, onHand: item.onHand, status: item.status, locationName: item.locationName ?? item.keptInLocationName }); } }) : null);
}

// ── Import (§12) ────────────────────────────────────────────────────────────

/** The team's asset sheet → preview (codes by prefix, missing costs, errors) → one
 *  opening-balance receipt. Nothing is written until every row is clean. */
export function ImportDialog({ onClose, onDone }) {
  const toast = useToast();
  const [file, setFile] = React.useState(null);
  const [date, setDate] = React.useState(todayIst());
  const [result, setResult] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const check = async (save) => {
    if (!file) { toast.error("Choose the Excel file first."); return; }
    setBusy(true);
    try {
      const r = await api.importSheet(file, date, save);
      setResult(r);
      if (save) { toast.success(`Imported ${r.units} assets on ${r.receiptNumber}.`); onDone?.(r); }
    } catch (error) { toast.error(errorText(error, "The file could not be read.")); }
    finally { setBusy(false); }
  };
  const ok = result && result.errorRows === 0 && !result.saved;
  return h(Dialog, {
    title: "Import the asset list", onClose, wide: true,
    footer: h(Frag, null,
      h("span", { className: "as-grow" }, result ? (result.saved ? `Saved as ${result.receiptNumber}.` : `${result.validRows} of ${result.totalRows} rows ready · ${result.units} assets`) : "Nothing is saved until you confirm."),
      h(Button, { variant: "ghost", onClick: onClose }, result?.saved ? "Close" : "Cancel"),
      result?.saved ? null : h(Button, { variant: "outline", onClick: () => check(false), loading: busy && !ok, disabled: !file }, result ? "Check again" : "Check the file"),
      ok ? h(Button, { onClick: () => check(true), loading: busy }, `Create ${result.units} assets`) : null)
  },
    h("div", { className: "as-note", style: { marginBottom: "var(--space-4)" } },
      h("strong", null, "One row per kind of thing, in the team's own columns. "),
      "Quantity is how many physical units the row describes: each becomes its own asset with its own code and QR label. The whole sheet comes in as one Incoming Goods receipt marked opening balance. ",
      h("button", { type: "button", className: "as-link", onClick: () => api.importTemplate() }, "Download the template")),
    h("div", { className: "as-form" },
      h(Input, { label: "Excel file (.xlsx)", type: "file", accept: ".xlsx", required: true, onChange: (ev) => { setFile(ev.target.files?.[0] ?? null); setResult(null); } }),
      h(Input, { label: "Receipt date — the day Regalis started counting", type: "date", value: date, max: todayIst(), onChange: (ev) => { setDate(ev.target.value); setResult(null); } })),
    result ? h(ImportResult, { result }) : null);
}

function ImportResult({ result }) {
  const errors = result.rows.filter((r) => r.status === "Error");
  return h("div", { style: { marginTop: "var(--space-4)", display: "flex", flexDirection: "column", gap: "var(--space-3)" } },
    h("div", { className: "as-cards as-cards--4" },
      h(SummaryCard, { label: "Rows", value: result.totalRows, sub: `${result.validRows} ready · ${result.errorRows} with errors`, tone: result.errorRows ? "red" : undefined }),
      h(SummaryCard, { label: "Asset records", value: result.units, sub: "one per physical unit" }),
      h(SummaryCard, { label: "Cost missing", value: result.costMissingRows, sub: "rows — shown in red until filled in", tone: result.costMissingRows ? "red" : undefined }),
      h(SummaryCard, { label: "Value recorded", value: money(result.totalValue), sub: result.newLocations.length ? `New rooms: ${result.newLocations.join(", ")}` : "rooms all known" })),
    errors.length ? h("div", { className: "as-note as-note--red" }, h("strong", null, `${errors.length} row${errors.length === 1 ? "" : "s"} must be fixed in the sheet first. `), "Nothing is saved while any row has an error.") : null,
    result.ignoredColumns.length ? h("div", { className: "as-sub" }, `Columns not read: ${result.ignoredColumns.join(", ")}.`) : null,
    result.prefixes.length ? h("div", { className: "as-panel" },
      h("table", { className: "as-table" },
        h("thead", null, h("tr", null, h("th", null, "Prefix"), h("th", { className: "num" }, "Rows"), h("th", { className: "num" }, "Units"), h("th", null, result.saved ? "Codes created" : "Codes after import"))),
        h("tbody", null, result.prefixes.map((p) => h("tr", { key: p.prefix }, h("td", { className: "as-mono" }, p.prefix), h("td", { className: "num" }, p.rows), h("td", { className: "num" }, p.units), h("td", { className: "as-mono" }, p.range)))))) : null,
    h("div", { className: "as-panel as-table-wrap", style: { maxHeight: 360, overflow: "auto" } },
      h("table", { className: "as-table" },
        h("thead", null, h("tr", null, h("th", null, "Row"), h("th", null, "Item"), h("th", null, "Codes"), h("th", { className: "num" }, "Qty"), h("th", { className: "num" }, "Cost each"), h("th", null, "Room"), h("th", null, "Checks"))),
        h("tbody", null, result.rows.map((r) => h("tr", { key: r.rowNumber, className: r.status === "Error" ? "as-row--alert" : undefined },
          h("td", null, r.rowNumber),
          h("td", null, h("div", { className: "as-cell-main" }, r.itemName ?? "—"), h("div", { className: "as-cell-sub" }, r.category ?? "")),
          h("td", { className: "as-mono" }, r.codes ?? r.prefix ?? "—"),
          h("td", { className: "num" }, r.quantity || "—"),
          h("td", { className: `num${r.unitCost == null ? " as-costless" : ""}` }, r.unitCost == null ? "cost missing" : money(r.unitCost)),
          h("td", null, r.location ?? "—"),
          h("td", null,
            r.errors.map((e, i) => h("div", { key: `e${i}`, className: "as-out" }, e)),
            r.notes.map((n, i) => h("div", { key: `n${i}`, className: "as-cell-sub" }, n)))))))));
}
