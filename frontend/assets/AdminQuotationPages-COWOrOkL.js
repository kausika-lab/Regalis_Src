/* Quotations — admin portal (specification SS3.1–SS3.6).
 *
 * Patched into the built bundle by hand: this deployment carries no frontend
 * source, so the chunk keeps its hashed filename and sw.js carries a bumped
 * revision marker for it. Identifiers imported from index-DI5X4c9f.js are the
 * minified export letters; they are aliased to readable names on the way in.
 */
import {
  D as unwrap,
  E as api,
  Y as useParams,
  a as useToast,
  u as useNavigate,
  R as usePermission,
  r as React,
  A as ApiClientError,
  j as jsx,
  G as Spinner,
  p as Button,
  F as Select,
  V as invoiceApi,
  z as locationsApi,
  I as Input,
} from "./index-DI5X4c9f.js";
import { c as customersApi, u as usersApi } from "./admin-DtGep2Hs.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { u as useFieldErrors } from "./useFieldErrors-C8qSSg0h.js";
import { c as formatDate, b as formatInr, f as formatDateTime } from "./formatters-DFe5xCRi.js";

const e = jsx;

/* ── Model ──────────────────────────────────────────────────────────────── */

const QUOTATION_STATUS = {
  Draft: 1, Sent: 2, Viewed: 3, Approved: 4,
  Rejected: 5, Expired: 6, Cancelled: 7, Revised: 8,
};

const STATUS_LABEL = {
  1: "Draft", 2: "Sent", 3: "Viewed", 4: "Approved",
  5: "Rejected", 6: "Expired", 7: "Cancelled", 8: "Revised",
};

/* The customer never reads "Sent" or "Revised" — SS3.7 rewords both. */
const CUSTOMER_STATUS_LABEL = {
  2: "Awaiting your approval", 3: "Awaiting your approval", 8: "Replaced",
};

const STATUS_SLUG = {
  1: "draft", 2: "sent", 3: "viewed", 4: "approved",
  5: "rejected", 6: "expired", 7: "cancelled", 8: "revised",
};

/* The tab strip from SS3.1, in the document's order. */
const STATUS_TABS = [
  { value: "", label: "All" },
  { value: "1", label: "Draft" },
  { value: "2", label: "Sent" },
  { value: "3", label: "Viewed" },
  { value: "4", label: "Approved" },
  { value: "5", label: "Rejected" },
  { value: "6", label: "Expired" },
  { value: "8", label: "Revised" },
];

const SERVICE_TYPE = { Cabin: 1, Desk: 2, MeetingRoom: 3, Pass: 4, Other: 5 };
const SERVICE_TYPE_LABEL = {
  1: "Cabin", 2: "Desk", 3: "Meeting Room", 4: "Pass", 5: "Other",
};
const LINE_TYPE = { Service: 1, Custom: 2, Header: 3 };
const DURATION_UNITS = ["Hour", "Day", "Month", "One-time"];
const DISCOUNT_TYPE = { Percentage: 1, Amount: 2 };
const DECISION_MODE = { Portal: 1, Phone: 2, InPerson: 3, Email: 4, WhatsApp: 5 };

/* The list's Date filter. The server resolves each preset to a whole period
   (inclusive); "custom" sends the From / To the admin picked instead. */
const DATE_PRESETS = [
  { value: "today", label: "Today" },
  { value: "week", label: "This week" },
  { value: "month", label: "This month" },
  { value: "lastmonth", label: "Last month" },
  { value: "quarter", label: "This quarter" },
  { value: "fy", label: "This financial year" },
  { value: "lastfy", label: "Last financial year" },
  { value: "custom", label: "Custom range" },
];

const MONTHS_SHORT = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];

/** "23 Sep 2026" for the list. formatDate follows the browser's locale, which
 *  prints "Sept" for en-IN, so the list formats its own dates. */
function formatListDate(value) {
  if (!value) return "";
  const [year, month, day] = String(value).slice(0, 10).split("-");
  return `${day} ${MONTHS_SHORT[Number(month) - 1] ?? month} ${year}`;
}

/* Long references and customer names wrap to two lines, then ellipsis. */
const CLAMP_2 = {
  display: "-webkit-box", WebkitLineClamp: 2, WebkitBoxOrient: "vertical",
  overflow: "hidden", overflowWrap: "anywhere",
};

/* SS3.7: the fixed reasons a customer may pick when rejecting. */
const REJECT_REASONS = [
  { value: "PRICE", label: "Price too high" },
  { value: "OTHER_SPACE", label: "Found another space" },
  { value: "REQUIREMENT", label: "Requirement changed" },
  { value: "TIMING", label: "Timing not right" },
  { value: "OTHER", label: "Other" },
];

async function downloadBlob(url, filename, params) {
  const response = await api.get(url, { responseType: "blob", params });
  const href = URL.createObjectURL(response.data);
  const anchor = document.createElement("a");
  anchor.href = href;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(href);
}

const quotationsApi = {
  list: (query = {}) => unwrap(api.get("/quotations", { params: query })),
  summary: () => unwrap(api.get("/quotations/summary")),
  statusCounts: (query = {}) => unwrap(api.get("/quotations/status-counts", { params: query })),
  formOptions: () => unwrap(api.get("/quotations/form-options")),
  get: (id) => unwrap(api.get(`/quotations/${id}`)),
  create: (payload) => unwrap(api.post("/quotations", payload)),
  update: (id, payload) => unwrap(api.put(`/quotations/${id}`, payload)),
  remove: (id) => unwrap(api.delete(`/quotations/${id}`)),
  send: (id, payload) => unwrap(api.post(`/quotations/${id}/send`, payload)),
  revise: (id, reason) => unwrap(api.post(`/quotations/${id}/revise`, { reason: reason ?? null })),
  clone: (id) => unwrap(api.post(`/quotations/${id}/clone`)),
  recordDecision: (id, payload) => unwrap(api.post(`/quotations/${id}/record-decision`, payload)),
  cancel: (id, reason) => unwrap(api.post(`/quotations/${id}/cancel`, { reason })),
  setInternalNote: (id, note) => unwrap(api.put(`/quotations/${id}/internal-note`, { note })),
  addAttachment: (id, file) => {
    const form = new FormData();
    form.append("file", file);
    return unwrap(api.post(`/quotations/${id}/attachments`, form));
  },
  updateAttachment: (id, attachmentId, sendWithEmail) =>
    unwrap(api.patch(`/quotations/${id}/attachments/${attachmentId}`, { sendWithEmail })),
  removeAttachment: (id, attachmentId) =>
    unwrap(api.delete(`/quotations/${id}/attachments/${attachmentId}`)),

  mine: (query = {}) => unwrap(api.get("/quotations/my", { params: query })),
  mineById: (id) => unwrap(api.get(`/quotations/my/${id}`)),
  approve: (id, payload) => unwrap(api.post(`/quotations/${id}/approve`, payload)),
  reject: (id, payload) => unwrap(api.post(`/quotations/${id}/reject`, payload)),

  pdf: (id, quotationNumber) =>
    downloadBlob(`/quotations/${id}/pdf`, `${quotationNumber.replace(/[\\/]/g, "-")}.pdf`),
  itemRates: (inventoryItemId) =>
    unwrap(api.get("/quotations/item-rates", { params: { inventoryItemId, take: 5 } })),
  exportCsv: (query) =>
    downloadBlob("/quotations/export", "quotations.csv", query),
};

/* ── Shared bits ────────────────────────────────────────────────────────── */

/** A quotation past its validity reads as Expired immediately, even before the
 *  nightly sweeper writes the status — the server reports the same in isExpired. */
function effectiveStatus(q) {
  return q.isExpired ? QUOTATION_STATUS.Expired : q.status;
}

function StatusPill({ quotation, forCustomer = false }) {
  const status = effectiveStatus(quotation);
  const label = (forCustomer && CUSTOMER_STATUS_LABEL[status]) || STATUS_LABEL[status] || "Unknown";
  return e.jsx("span", {
    className: `qtn-pill qtn-pill--${STATUS_SLUG[status] ?? "draft"}`,
    children: label,
  });
}

/** "in 8 days" / "today" / "expired" for an open quotation, else null. */
function validityHint(quotation) {
  const days = quotation.daysToExpiry;
  const open = effectiveStatus(quotation) === QUOTATION_STATUS.Sent
    || effectiveStatus(quotation) === QUOTATION_STATUS.Viewed;
  if (quotation.isExpired) return "expired";
  if (open && days >= 0) return days === 0 ? "today" : `in ${days} day${days === 1 ? "" : "s"}`;
  return null;
}

/** "in 8 days" under the valid-until date, amber once it is 3 days or less. */
function ValidUntil({ quotation }) {
  const days = quotation.daysToExpiry;
  const hint = validityHint(quotation);
  return e.jsxs("div", {
    children: [
      e.jsx("div", { children: formatDate(quotation.validUntil) }),
      hint && e.jsx("div", {
        className: `qtn-due${quotation.isExpired || days <= 3 ? " qtn-due--soon" : ""}`,
        children: hint,
      }),
    ],
  });
}

function Detail({ label, children }) {
  return e.jsxs("div", {
    children: [
      e.jsx("div", { className: "qtn-detail__label", children: label }),
      e.jsx("div", { children: children ?? "—" }),
    ],
  });
}

/** The row "⋯" menu (SS3.1). Only the actions the server says are allowed for
 *  this status appear, so the menu can never offer something the API refuses. */
function RowMenu({ items }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (!open) return;
    const onDown = (ev) => { if (ref.current && !ref.current.contains(ev.target)) setOpen(false); };
    const onKey = (ev) => { if (ev.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onDown);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onDown);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  const visible = items.filter((item) => item && item.show !== false);
  if (visible.length === 0) return null;

  return e.jsxs("div", {
    className: "qtn-menu",
    ref,
    children: [
      e.jsx("button", {
        type: "button",
        className: "qtn-menu__trigger",
        "aria-label": "More actions",
        "aria-expanded": open,
        onClick: (ev) => { ev.stopPropagation(); setOpen((v) => !v); },
        children: "⋯",
      }),
      open && e.jsx("div", {
        className: "qtn-menu__list",
        role: "menu",
        /* An `info` item is a read-only line (e.g. the valid-until date). */
        children: visible.map((item) => item.info ? e.jsx("div", {
          role: "none",
          className: "qtn-menu__item",
          style: { color: "var(--text-muted)", cursor: "default", fontSize: "var(--font-size-xs)" },
          children: item.label,
        }, item.label) : e.jsx("button", {
          type: "button",
          role: "menuitem",
          className: `qtn-menu__item${item.danger ? " qtn-menu__item--danger" : ""}`,
          onClick: (ev) => { ev.stopPropagation(); setOpen(false); item.onSelect(); },
          children: item.label,
        }, item.label)),
      }),
    ],
  });
}

function SummaryCard({ label, value, hint, active, onClick }) {
  return e.jsxs("button", {
    type: "button",
    className: `qtn-stat${active ? " qtn-stat--active" : ""}`,
    onClick,
    children: [
      e.jsx("div", { className: "qtn-stat__l", children: label }),
      e.jsx("div", { className: "qtn-stat__v", children: value }),
      e.jsx("div", { className: "qtn-stat__h", children: hint ?? " " }),
    ],
  });
}

function errorMessage(error, fallback) {
  return error instanceof ApiClientError ? error.message : fallback;
}

/* ── 1. List (SS3.1) ────────────────────────────────────────────────────── */

const ISO_DATE = /^\d{4}-\d{2}-\d{2}$/;

/** Tab and date filter as the URL holds them, so a refresh or a shared link
 *  reopens the same view: ?status=2&date=month or ?from=2026-09-01&to=2026-09-30. */
function readListParams() {
  const params = new URLSearchParams(window.location.search);
  const status = params.get("status") ?? "";
  const from = params.get("from") ?? "";
  const to = params.get("to") ?? "";
  const preset = params.get("date") ?? "";
  const custom = ISO_DATE.test(from) || ISO_DATE.test(to);
  return {
    status: STATUS_TABS.some((t) => t.value === status) ? status : "",
    datePreset: custom ? "custom"
      : DATE_PRESETS.some((p) => p.value === preset && p.value !== "custom") ? preset : "",
    from: ISO_DATE.test(from) ? from : "",
    to: ISO_DATE.test(to) ? to : "",
  };
}

function writeListParams({ status, datePreset, from, to }) {
  const params = new URLSearchParams(window.location.search);
  const set = (key, value) => (value ? params.set(key, value) : params.delete(key));
  set("status", status);
  set("date", datePreset && datePreset !== "custom" ? datePreset : "");
  set("from", datePreset === "custom" ? from : "");
  set("to", datePreset === "custom" ? to : "");
  const search = params.toString();
  window.history.replaceState(null, "", window.location.pathname + (search ? `?${search}` : ""));
}

/* Newest date first by default; a quote number reads naturally A→Z. */
const DEFAULT_SORT_DIR = { date: "desc", number: "asc", amount: "desc" };

/** A column header that sorts the list. Clicking the active column flips it. */
function SortHeader({ label, field, sort, onSort, align = "left" }) {
  const active = sort.by === field;
  return e.jsxs("button", {
    type: "button",
    onClick: () => onSort(field),
    "aria-label": `Sort by ${label}${active ? `, currently ${sort.dir === "asc" ? "ascending" : "descending"}` : ""}`,
    style: {
      background: "none", border: 0, padding: 0, margin: 0, cursor: "pointer",
      font: "inherit", color: "inherit", fontWeight: "inherit", whiteSpace: "nowrap",
      display: "block", width: "100%", textAlign: align,
    },
    children: [
      label,
      e.jsx("span", {
        "aria-hidden": true,
        style: { marginLeft: "4px", color: active ? "inherit" : "var(--text-muted)", opacity: active ? 1 : 0.5 },
        children: active ? (sort.dir === "asc" ? "▲" : "▼") : "↕",
      }),
    ],
  });
}

function AdminQuotationsPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const canCreate = usePermission("QUOTATION.CREATE");
  const canExport = usePermission("QUOTATION.EXPORT");

  /* The active tab and the date filter live in the URL so a filtered list can
     be linked, survives a refresh, and the back button behaves (SS3.1). */
  const initial = React.useMemo(readListParams, []);
  const [status, setStatus] = React.useState(initial.status);
  const [result, setResult] = React.useState(null);
  const [summary, setSummary] = React.useState(null);
  const [counts, setCounts] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [searchTerm, setSearchTerm] = React.useState("");
  const [customerId, setCustomerId] = React.useState("");
  const [serviceType, setServiceType] = React.useState("");
  const [datePreset, setDatePreset] = React.useState(initial.datePreset);
  const [from, setFrom] = React.useState(initial.from);
  const [to, setTo] = React.useState(initial.to);
  const [sort, setSort] = React.useState({ by: "date", dir: "desc" });
  const [expiringInDays, setExpiringInDays] = React.useState("");
  const [customers, setCustomers] = React.useState([]);
  const [selected, setSelected] = React.useState(() => new Set());
  const [busy, setBusy] = React.useState(false);
  const [sendFor, setSendFor] = React.useState(null);
  const [confirming, setConfirming] = React.useState(null);

  const custom = datePreset === "custom";
  const rangeError = custom && from && to && to < from
    ? "“To” date cannot be before “From” date." : null;

  React.useEffect(() => {
    writeListParams({ status, datePreset, from, to });
  }, [status, datePreset, from, to]);

  /* One set of filters drives the table, the tab counts and the export, so
     the three can never disagree. The tab itself is added only to the list. */
  const filters = React.useMemo(() => ({
    search: searchTerm || undefined,
    customerId: customerId ? Number(customerId) : undefined,
    serviceType: serviceType ? Number(serviceType) : undefined,
    datePreset: datePreset && !custom ? datePreset : undefined,
    from: custom && from ? from : undefined,
    to: custom && to ? to : undefined,
    expiringInDays: expiringInDays ? Number(expiringInDays) : undefined,
  }), [searchTerm, customerId, serviceType, datePreset, custom, from, to, expiringInDays]);

  const listQuery = React.useMemo(() => ({
    ...filters,
    status: status ? Number(status) : undefined,
    sortBy: sort.by,
    sortDir: sort.dir,
  }), [filters, status, sort]);

  /* Typing a range fires a request per change; only the newest may land, or a
     slower earlier answer would overwrite the table with stale rows. */
  const listSeq = React.useRef(0);
  const countSeq = React.useRef(0);

  const load = React.useCallback(async () => {
    if (rangeError) return; // keep the last good result until the range is fixed
    const seq = ++listSeq.current;
    setLoading(true);
    try {
      const next = await quotationsApi.list({ ...listQuery, page, pageSize: 20 });
      if (seq === listSeq.current) setResult(next);
    } catch (error) {
      if (seq === listSeq.current) toast.error(errorMessage(error, "Failed to load quotations."));
    } finally {
      if (seq === listSeq.current) setLoading(false);
    }
  }, [listQuery, page, rangeError]);

  const loadCounts = React.useCallback(async () => {
    if (rangeError) return;
    const seq = ++countSeq.current;
    try {
      const next = await quotationsApi.statusCounts(filters);
      if (seq === countSeq.current) setCounts(next);
    } catch {
      if (seq === countSeq.current) setCounts(null); // the tabs still work without their numbers
    }
  }, [filters, rangeError]);

  const loadSummary = React.useCallback(async () => {
    try {
      setSummary(await quotationsApi.summary());
    } catch {
      // The cards are a convenience; the table below is the real answer.
      setSummary(null);
    }
  }, []);

  React.useEffect(() => { void load(); }, [load]);
  React.useEffect(() => { void loadCounts(); }, [loadCounts]);
  React.useEffect(() => { void loadSummary(); }, [loadSummary]);
  /* A selection belongs to the page it was made on. */
  React.useEffect(() => { setSelected(new Set()); }, [result]);

  React.useEffect(() => {
    void (async () => {
      try {
        const page1 = await customersApi.list({ page: 1, pageSize: 200 });
        setCustomers(page1.items.map((c) => ({ value: c.userId, label: c.fullName })));
      } catch {
        setCustomers([]);
      }
    })();
  }, []);

  /* Debounced search: a filter should not fire a request per keystroke. */
  React.useEffect(() => {
    const timer = setTimeout(() => { setPage(1); setSearchTerm(search.trim()); }, 350);
    return () => clearTimeout(timer);
  }, [search]);

  const selectTab = (value) => {
    setStatus(value);
    setPage(1);
    setExpiringInDays("");
  };

  const clearDate = () => {
    setDatePreset("");
    setFrom("");
    setTo("");
    setPage(1);
  };

  const sortBy = (field) => {
    setPage(1);
    setSort((current) => (current.by === field
      ? { by: field, dir: current.dir === "asc" ? "desc" : "asc" }
      : { by: field, dir: DEFAULT_SORT_DIR[field] }));
  };

  const refresh = async () => { await load(); await loadCounts(); await loadSummary(); };

  const runAction = async (label, action) => {
    setBusy(true);
    try {
      await action();
      await refresh();
    } catch (error) {
      toast.error(errorMessage(error, `Could not ${label}.`));
    } finally {
      setBusy(false);
    }
  };

  const openQuotation = (q) => navigate(`/admin/billing/quotations/${q.quotationId}`);

  const pageIds = (result?.items ?? []).map((q) => q.quotationId);
  const allSelected = pageIds.length > 0 && pageIds.every((id) => selected.has(id));
  const someSelected = pageIds.some((id) => selected.has(id));
  const toggleOne = (id) => setSelected((current) => {
    const next = new Set(current);
    if (next.has(id)) next.delete(id); else next.add(id);
    return next;
  });

  const columns = [
    {
      key: "select",
      width: "40px",
      header: e.jsx("input", {
        type: "checkbox",
        "aria-label": "Select all quotations on this page",
        checked: allSelected,
        ref: (el) => { if (el) el.indeterminate = someSelected && !allSelected; },
        onChange: () => setSelected(allSelected ? new Set() : new Set(pageIds)),
      }),
      render: (q) => e.jsx("input", {
        type: "checkbox",
        "aria-label": `Select ${q.quotationNumber}`,
        checked: selected.has(q.quotationId),
        onChange: () => toggleOne(q.quotationId),
      }),
    },
    {
      key: "date",
      width: "120px",
      header: e.jsx(SortHeader, { label: "Date", field: "date", sort, onSort: sortBy }),
      render: (q) => e.jsx("span", { style: { whiteSpace: "nowrap" }, children: formatListDate(q.quotationDate) }),
    },
    {
      key: "number",
      header: e.jsx(SortHeader, { label: "Quote Number", field: "number", sort, onSort: sortBy }),
      render: (q) => e.jsx("button", {
        type: "button",
        className: "qtn-link",
        style: { textAlign: "left", whiteSpace: "nowrap" },
        onClick: (ev) => { ev.stopPropagation(); openQuotation(q); },
        children: q.quotationNumber,
      }),
    },
    {
      key: "reference",
      header: "Reference Number",
      render: (q) => {
        const reference = q.referenceNumber || q.subject || "";
        return e.jsx("div", { style: { ...CLAMP_2, maxWidth: "220px" }, title: reference || undefined, children: reference });
      },
    },
    {
      key: "customer",
      header: "Customer Name",
      render: (q) => e.jsxs("div", {
        style: { maxWidth: "240px" },
        children: [
          e.jsx("div", { style: CLAMP_2, title: q.customerName || undefined, children: q.customerName || "—" }),
          q.customerCompanyName && e.jsx("div", {
            className: "qtn-cell__muted", style: CLAMP_2, children: q.customerCompanyName,
          }),
        ],
      }),
    },
    { key: "status", header: "Status", render: (q) => e.jsx(StatusPill, { quotation: q }) },
    {
      key: "amount",
      header: e.jsx(SortHeader, { label: "Amount", field: "amount", sort, onSort: sortBy, align: "right" }),
      render: (q) => e.jsx("div", {
        style: { textAlign: "right", whiteSpace: "nowrap" },
        children: e.jsx("strong", { children: formatInr(q.totalAmount) }),
      }),
    },
    {
      key: "actions",
      header: "",
      render: (q) => e.jsxs("div", {
        className: "row-actions",
        children: [
          e.jsx(Button, {
            size: "sm",
            variant: "outline",
            onClick: (ev) => { ev.stopPropagation(); openQuotation(q); },
            children: "Open",
          }),
          e.jsx(RowMenu, {
            items: [
              { label: "View", onSelect: () => openQuotation(q) },
              /* Valid-until and the invoice left the table; they live here now. */
              {
                label: `Open invoice ${q.invoiceNumber}`, show: Boolean(q.invoiceNumber),
                onSelect: () => navigate("/admin/billing/invoices"),
              },
              {
                label: "Edit", show: q.actions.canEdit,
                onSelect: () => navigate(`/admin/billing/quotations/${q.quotationId}/edit`),
              },
              { label: "Send", show: q.actions.canSend, onSelect: () => setSendFor(q) },
              {
                label: "Download PDF", show: q.actions.canDownloadPdf,
                onSelect: () => void quotationsApi.pdf(q.quotationId, q.quotationNumber),
              },
              {
                label: "Clone", show: q.actions.canClone,
                onSelect: () => void runAction("clone the quotation", async () => {
                  const clone = await quotationsApi.clone(q.quotationId);
                  toast.success(`Cloned as ${clone.quotationNumber}.`);
                  navigate(`/admin/billing/quotations/${clone.quotationId}/edit`);
                }),
              },
              {
                label: "Revise", show: q.actions.canRevise,
                onSelect: () => void runAction("revise the quotation", async () => {
                  const revision = await quotationsApi.revise(q.quotationId);
                  toast.success(`Revision ${revision.quotationNumber} created.`);
                  navigate(`/admin/billing/quotations/${revision.quotationId}/edit`);
                }),
              },
              {
                label: "Convert to invoice", show: q.actions.canConvertToInvoice,
                onSelect: () => void runAction("raise the invoice", async () => {
                  const invoice = await invoiceApi.fromQuotation(q.quotationId);
                  toast.success(`Invoice ${invoice.invoiceNumber} raised.`);
                }),
              },
              {
                label: "Delete", danger: true, show: q.actions.canDelete,
                onSelect: () => setConfirming(q),
              },
              {
                info: true,
                label: `Valid until ${formatListDate(q.validUntil)}${validityHint(q) ? ` · ${validityHint(q)}` : ""}`,
              },
            ],
          }),
        ],
      }),
    },
  ];

  const tabCount = (value) => {
    if (!counts) return null;
    if (!value) return counts.totalCount;
    return counts.statusCounts?.[value] ?? 0;
  };

  const filtered = Boolean(status || searchTerm || customerId || serviceType || datePreset || expiringInDays);

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Quotations",
        description: "Raise a priced offer for a customer. Once sent it is frozen, and the customer approves or rejects it in their portal.",
        actions: e.jsxs(e.Fragment, {
          children: [
            canExport && e.jsx(Button, {
              variant: "outline",
              disabled: Boolean(rangeError),
              onClick: () => void quotationsApi.exportCsv({ ...listQuery }).catch(
                (error) => toast.error(errorMessage(error, "Could not export.")),
              ),
              children: "Export",
            }),
            canCreate && e.jsx(Button, {
              onClick: () => navigate("/admin/billing/quotations/new"),
              children: "New quotation",
            }),
          ],
        }),
      }),

      e.jsxs("div", {
        className: "qtn-stats",
        children: [
          e.jsx(SummaryCard, {
            label: `Total quotes · FY ${summary?.financialYear ?? "—"}`,
            value: summary ? summary.totalCount : "—",
            hint: summary ? formatInr(summary.totalValue) : null,
            active: !status && !expiringInDays,
            onClick: () => selectTab(""),
          }),
          e.jsx(SummaryCard, {
            label: "Awaiting reply",
            value: summary ? summary.awaitingCount : "—",
            hint: summary ? formatInr(summary.awaitingValue) : null,
            active: status === "2",
            onClick: () => selectTab("2"),
          }),
          e.jsx(SummaryCard, {
            label: "Approved",
            value: summary ? summary.approvedCount : "—",
            hint: summary ? formatInr(summary.approvedValue) : null,
            active: status === "4",
            onClick: () => selectTab("4"),
          }),
          e.jsx(SummaryCard, {
            label: "Expiring in 3 days",
            value: summary ? summary.expiringSoonCount : "—",
            hint: summary ? formatInr(summary.expiringSoonValue) : null,
            active: expiringInDays === "3",
            onClick: () => {
              selectTab("");
              setExpiringInDays("3");
              setPage(1);
            },
          }),
        ],
      }),

      e.jsxs(Card, {
        children: [
          e.jsx("div", {
            className: "qtn-tabs",
            role: "tablist",
            children: STATUS_TABS.map((tab) => {
              const count = tabCount(tab.value);
              return e.jsxs("button", {
                type: "button",
                role: "tab",
                "aria-selected": status === tab.value,
                className: `qtn-tab${status === tab.value ? " qtn-tab--active" : ""}`,
                onClick: () => selectTab(tab.value),
                children: [
                  tab.label,
                  count !== null && e.jsx("span", { className: "qtn-tab__count", children: count }),
                ],
              }, tab.value || "all");
            }),
          }),

          e.jsxs("div", {
            className: "toolbar",
            children: [
              e.jsx(Input, {
                label: "Search",
                placeholder: "Number, reference, customer, company, mobile or subject",
                value: search,
                onChange: (ev) => setSearch(ev.target.value),
              }),
              e.jsx(Select, {
                label: "Customer",
                value: customerId,
                placeholder: "All customers",
                options: customers,
                onChange: (ev) => { setPage(1); setCustomerId(ev.target.value); },
              }),
              e.jsx(Select, {
                label: "Service",
                value: serviceType,
                placeholder: "All services",
                options: Object.entries(SERVICE_TYPE_LABEL)
                  .map(([value, label]) => ({ value, label })),
                onChange: (ev) => { setPage(1); setServiceType(ev.target.value); },
              }),
              e.jsx(Select, {
                /* Clear sits in the label so the toolbar fields stay aligned. */
                label: datePreset
                  ? e.jsxs(e.Fragment, {
                    children: [
                      "Date ",
                      e.jsx("button", {
                        type: "button",
                        className: "qtn-link",
                        style: { marginLeft: "6px", fontSize: "var(--font-size-xs)", fontWeight: 400 },
                        onClick: (ev) => { ev.preventDefault(); clearDate(); },
                        children: "Clear",
                      }),
                    ],
                  })
                  : "Date",
                value: datePreset,
                placeholder: "Any date",
                options: DATE_PRESETS,
                onChange: (ev) => { setPage(1); setDatePreset(ev.target.value); },
              }),
              custom && e.jsx(Input, {
                label: "From",
                type: "date",
                value: from,
                max: to || undefined,
                onChange: (ev) => { setPage(1); setFrom(ev.target.value); },
              }),
              custom && e.jsx(Input, {
                label: "To",
                type: "date",
                value: to,
                min: from || undefined,
                error: rangeError ?? undefined,
                onChange: (ev) => { setPage(1); setTo(ev.target.value); },
              }),
            ],
          }),

          expiringInDays && e.jsxs("div", {
            className: "qtn-filternote",
            children: [
              "Showing quotations expiring within 3 days. ",
              e.jsx("button", {
                type: "button",
                className: "qtn-link",
                onClick: () => { setExpiringInDays(""); setPage(1); },
                children: "Clear",
              }),
            ],
          }),

          selected.size > 0 && e.jsxs("div", {
            className: "qtn-filternote",
            children: [
              `${selected.size} selected on this page. `,
              e.jsx("button", {
                type: "button",
                className: "qtn-link",
                onClick: () => setSelected(new Set()),
                children: "Clear selection",
              }),
            ],
          }),

          e.jsx(DataTable, {
            columns,
            result,
            loading: loading || busy,
            rowKey: (q) => q.quotationId,
            onRowClick: openQuotation,
            onPageChange: setPage,
            emptyTitle: filtered ? "No quotations found for the selected filters." : "No quotations yet",
            emptyDescription: filtered
              ? undefined
              : "Create your first quotation for a cabin, desk or meeting room.",
          }),
        ],
      }),

      sendFor && e.jsx(SendQuotationModal, {
        quotation: sendFor,
        onClose: () => setSendFor(null),
        onSent: () => { setSendFor(null); void refresh(); },
      }),

      confirming && e.jsx(Modal, {
        open: true,
        title: `Delete ${confirming.quotationNumber}?`,
        onClose: () => setConfirming(null),
        footer: e.jsxs(e.Fragment, {
          children: [
            e.jsx(Button, {
              variant: "ghost", onClick: () => setConfirming(null), children: "Keep it",
            }),
            e.jsx(Button, {
              variant: "danger",
              loading: busy,
              onClick: () => {
                const target = confirming;
                setConfirming(null);
                void runAction("delete the draft", async () => {
                  await quotationsApi.remove(target.quotationId);
                  toast.success("Draft deleted.");
                });
              },
              children: "Delete draft",
            }),
          ],
        }),
        children: e.jsx("p", {
          children: "Only a draft can be deleted. The number it used will not be issued again.",
        }),
      }),
    ],
  });
}

/* ── 2. Create / edit / revise (SS3.2–SS3.4) ────────────────────────────── */

/* ── Services line table (specification §2.1 "Services That Can Be Quoted") ──
   One row = one quoted service. Cabins, desks and meeting rooms come from
   Workspaces, passes from Passes (form-options already carries them with every
   configured price), and a Custom line is typed by the admin. Nothing here is
   hard-coded except which units each service type may be sold in. */

const ONE_TIME = "One-time";

/* Service type → allowed units (first = default). The value is the line's
   ServiceType byte; Custom line is saved as Other (5). */
const SERVICE_KINDS = [
  { value: "1", label: "Cabin", units: ["Month", "Day"] },
  { value: "2", label: "Desk", units: ["Month", "Day"] },
  { value: "3", label: "Meeting Room", units: ["Hour", "Day"] },
  { value: "4", label: "Pass", units: [ONE_TIME] },
  { value: "5", label: "Custom line", units: ["Hour", "Day", "Month", ONE_TIME] },
];
const CUSTOM_KIND = "5";
const kindOf = (value) => SERVICE_KINDS.find((k) => k.value === String(value)) ?? SERVICE_KINDS[4];

const emptyLine = (kind = "1") => ({
  key: Math.random().toString(36).slice(2),
  lineType: kind === CUSTOM_KIND ? LINE_TYPE.Custom : LINE_TYPE.Service,
  serviceType: kind,
  workspaceId: "",
  passPlanId: "",
  workspaceTypeId: "",
  description: "",
  quantity: "1",
  durationValue: "1",
  durationUnit: kindOf(kind).units[0],
  unitPrice: "",
  standardRate: null,
  discountAmount: "0",
  taxPercent: "18",
  nonTaxable: false,
  // Carried through untouched for lines written by earlier versions of the form.
  lineNotes: "",
  hsnSacCode: "",
  inventoryItemId: "",
  itemType: "",
  unit: "",
});

/** A row with nothing picked or typed yet — never saved. */
const isBlankLine = (l) => Number(l.lineType) !== LINE_TYPE.Header
  && !String(l.description ?? "").trim();

const isOneTime = (l) => !l.durationUnit || l.durationUnit === ONE_TIME;

/** Non-Taxable charges nothing, whatever rate the row last had. */
const taxOf = (l) => (l.nonTaxable ? 0 : Number(l.taxPercent) || 0);

/** Qty × Duration × Rate; a one-time line is Qty × Rate (§2.1). */
const lineAmountOf = (l) => round2(
  (Number(l.quantity) || 0) * (isOneTime(l) ? 1 : Number(l.durationValue) || 1) * (Number(l.unitPrice) || 0)
  - (Number(l.discountAmount) || 0),
);

/** "8,600.00" in the Amount column. */
const formatAmount = (value) => new Intl.NumberFormat("en-IN", {
  minimumFractionDigits: 2, maximumFractionDigits: 2,
}).format(value || 0);

/** "₹25,000" / "₹1,21,292.50" — paise only when present, as the PDF prints it. */
const rupees = (value) => {
  const v = Number(value) || 0;
  const digits = v % 1 === 0 ? 0 : 2;
  return `₹${new Intl.NumberFormat("en-IN", { minimumFractionDigits: digits, maximumFractionDigits: digits }).format(v)}`;
};
const plainNumber = (value) => String(Number((Number(value) || 0).toFixed(3)));

/** "Cabin C-001 · 1 × 3 Month × ₹25,000" — the same rules as QuotationLineText
 *  on the server, so the preview is exactly what the PDF will print. */
const lineSummary = (l) => {
  const term = isOneTime(l) ? ONE_TIME : `${plainNumber(l.durationValue || 1)} ${l.durationUnit}`;
  return `${String(l.description).trim()} · ${plainNumber(l.quantity)} × ${term} × ${rupees(l.unitPrice)}`;
};

/** The configured price of a service in a unit, or null when none is set. */
const rateFor = (option, unit) => {
  if (!option) return null;
  const rate = unit === "Hour" ? option.ratePerHour
    : unit === "Day" ? option.ratePerDay
      : unit === "Month" ? option.ratePerMonth
        : option.rateOneTime;
  return rate === undefined || rate === null ? null : Number(rate);
};

/** The catalogue entry a row points at (a cabin, desk, room or pass). */
const optionOf = (services, line) => services.find((o) => String(o.serviceType) === String(line.serviceType)
  && String(o.workspaceId ?? "") === String(line.workspaceId ?? "")
  && String(o.passPlanId ?? "") === String(line.passPlanId ?? ""));

/** An existing line back into a row. A line the catalogue can place becomes a
 *  typed row; anything else (typed text, old item-table lines) opens as a
 *  Custom line with its figures unchanged, so nothing breaks. */
function rowFromLine(l, services) {
  const base = {
    key: `l${l.quotationLineId}`,
    lineType: l.lineType,
    workspaceId: l.workspaceId ? String(l.workspaceId) : "",
    passPlanId: l.passPlanId ? String(l.passPlanId) : "",
    workspaceTypeId: l.workspaceTypeId ? String(l.workspaceTypeId) : "",
    description: l.description,
    quantity: String(l.quantity),
    durationValue: l.durationValue === null ? "1" : String(l.durationValue),
    durationUnit: l.durationValue === null ? ONE_TIME : (l.durationUnit || ONE_TIME),
    unitPrice: String(l.unitPrice),
    standardRate: l.standardRate,
    discountAmount: String(l.discountAmount),
    taxPercent: String(l.taxPercent),
    nonTaxable: Boolean(l.isNonTaxable),
    lineNotes: l.lineNotes ?? "",
    hsnSacCode: l.hsnSacCode ?? "",
    inventoryItemId: l.inventoryItemId ? String(l.inventoryItemId) : "",
    itemType: l.itemType ? String(l.itemType) : "",
    unit: l.unit ?? "",
  };
  if (l.lineType === LINE_TYPE.Header) return { ...base, serviceType: CUSTOM_KIND };
  const typed = l.serviceType >= 1 && l.serviceType <= 4
    && optionOf(services, { ...base, serviceType: String(l.serviceType) });
  /* A draft saved with "No price set" stored 0; reopen it empty so it still
     says so and still blocks Save and send. */
  return typed
    ? { ...base, serviceType: String(l.serviceType), lineType: LINE_TYPE.Service,
      unitPrice: l.standardRate === null && Number(l.unitPrice) === 0 ? "" : base.unitPrice }
    : { ...base, serviceType: CUSTOM_KIND, lineType: LINE_TYPE.Custom, workspaceId: "", passPlanId: "", workspaceTypeId: "" };
}

/** Searchable picker over the catalogue for one service type. Type to filter,
 *  ↑/↓ + Enter to choose, Esc to close. The list floats (position: fixed) so
 *  the table's scroll box cannot clip it, and opens upwards near the bottom. */
function ServicePicker({ options, value, unit, placeholder, rowNumber, onPick }) {
  const [open, setOpen] = React.useState(false);
  const [query, setQuery] = React.useState("");
  const [active, setActive] = React.useState(0);
  const [rect, setRect] = React.useState(null);
  const inputRef = React.useRef(null);
  const listRef = React.useRef(null);

  const term = query.trim().toLowerCase();
  const shown = term
    ? options.filter((o) => `${o.name} ${o.code}`.toLowerCase().includes(term))
    : options;

  React.useEffect(() => {
    if (!open) return undefined;
    const place = () => {
      const r = inputRef.current?.getBoundingClientRect();
      if (!r) return;
      const below = window.innerHeight - r.bottom - 8;
      const above = r.top - 8;
      const up = below < 240 && above > below;
      setRect({
        left: r.left, width: Math.max(r.width, 300),
        top: up ? undefined : r.bottom + 4,
        bottom: up ? window.innerHeight - r.top + 4 : undefined,
        room: Math.max(120, (up ? above : below) - 8),
      });
    };
    place();
    window.addEventListener("scroll", place, true);
    window.addEventListener("resize", place);
    return () => {
      window.removeEventListener("scroll", place, true);
      window.removeEventListener("resize", place);
    };
  }, [open]);

  React.useEffect(() => { setActive(0); }, [query]);
  React.useEffect(() => {
    listRef.current?.querySelector(`[data-index="${active}"]`)?.scrollIntoView({ block: "nearest" });
  }, [active]);

  const choose = (option) => { setOpen(false); setQuery(""); onPick(option); };
  const onKeyDown = (ev) => {
    if (ev.key === "ArrowDown") { ev.preventDefault(); if (!open) setOpen(true); else setActive((a) => Math.min(a + 1, shown.length - 1)); }
    else if (ev.key === "ArrowUp") { ev.preventDefault(); setActive((a) => Math.max(a - 1, 0)); }
    else if (ev.key === "Enter" && open) { ev.preventDefault(); if (shown[active]) choose(shown[active]); }
    else if (ev.key === "Escape" && open) { ev.preventDefault(); setOpen(false); setQuery(""); }
  };

  return e.jsxs("div", {
    children: [
      e.jsx("input", {
        ref: inputRef,
        className: "field__input",
        value: open ? query : value,
        placeholder: value || placeholder,
        "aria-label": `Item ${rowNumber}`,
        role: "combobox",
        "aria-expanded": open,
        "aria-autocomplete": "list",
        autoComplete: "off",
        onFocus: () => { setQuery(""); setOpen(true); },
        onClick: () => setOpen(true),
        onBlur: () => { setOpen(false); setQuery(""); },
        onChange: (ev) => { setQuery(ev.target.value); setOpen(true); },
        onKeyDown,
      }),
      open && rect && e.jsx("div", {
        role: "listbox",
        "aria-label": "Services",
        onMouseDown: (ev) => ev.preventDefault(),
        style: {
          position: "fixed", left: rect.left, top: rect.top, bottom: rect.bottom, width: rect.width, zIndex: 1000,
          background: "var(--surface-card)", color: "var(--text-primary)",
          border: "1px solid var(--border-strong)", borderRadius: "var(--radius-md)",
          boxShadow: "var(--shadow-lg)", overflow: "hidden",
        },
        children: e.jsx("div", {
          ref: listRef,
          style: { maxHeight: `${Math.min(280, rect.room)}px`, overflowY: "auto" },
          children: shown.length === 0
            ? e.jsx("div", {
              style: { padding: "10px 12px", color: "var(--text-muted)", fontSize: "var(--font-size-sm)" },
              children: options.length ? "Nothing matches." : "None configured yet.",
            })
            : shown.map((o, i) => {
              const rate = rateFor(o, unit);
              return e.jsxs("div", {
                role: "option",
                "data-index": i,
                "aria-selected": i === active,
                onMouseEnter: () => setActive(i),
                onClick: () => choose(o),
                style: {
                  padding: "8px 12px", cursor: "pointer", borderBottom: "1px solid var(--border-subtle)",
                  background: i === active ? "rgb(var(--accent-rgb) / 16%)" : "transparent",
                },
                children: [
                  e.jsx("div", { style: { fontWeight: 600 }, children: o.name }),
                  e.jsx("div", {
                    style: { fontSize: "var(--font-size-xs)", color: rate === null ? "var(--color-danger)" : "var(--text-muted)" },
                    children: [o.code, o.capacity ? `${o.capacity} seat${o.capacity === 1 ? "" : "s"}` : null,
                      rate === null ? `No ${unit === ONE_TIME ? "" : `${unit.toLowerCase()} `}price set` : `${rupees(rate)}${unit === ONE_TIME ? "" : ` / ${unit}`}`]
                      .filter(Boolean).join(" · "),
                  }),
                ],
              }, `${o.serviceType}:${o.workspaceId ?? ""}:${o.passPlanId ?? ""}:${o.code}`);
            }),
        }),
      }),
    ],
  });
}

/** "+ Add Items in Bulk": tick services of any type, set each quantity, add. */
function BulkServicesModal({ services, onClose, onAdd }) {
  const [term, setTerm] = React.useState("");
  const [picked, setPicked] = React.useState({});
  const keyOf = (o) => `${o.serviceType}:${o.workspaceId ?? ""}:${o.passPlanId ?? ""}`;
  const catalogue = services.filter((o) => o.serviceType >= 1 && o.serviceType <= 4);
  const needle = term.trim().toLowerCase();
  const shown = needle ? catalogue.filter((o) => `${o.name} ${o.code}`.toLowerCase().includes(needle)) : catalogue;
  const chosen = Object.values(picked);
  const toggle = (o) => setPicked((p) => {
    const next = { ...p };
    if (next[keyOf(o)]) delete next[keyOf(o)]; else next[keyOf(o)] = { option: o, quantity: "1" };
    return next;
  });
  return e.jsx(Modal, {
    open: true,
    title: "Add services in bulk",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
        e.jsx(Button, {
          disabled: chosen.length === 0,
          onClick: () => onAdd(chosen.map((c) => ({ option: c.option, quantity: Number(c.quantity) > 0 ? Number(c.quantity) : 1 }))),
          children: chosen.length ? `Add ${chosen.length} line${chosen.length === 1 ? "" : "s"}` : "Add lines",
        }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx(Input, { label: "Search", placeholder: "Cabin, desk, meeting room or pass", value: term, onChange: (ev) => setTerm(ev.target.value) }),
        shown.length === 0
          ? e.jsx("p", { className: "field__hint", children: "Nothing matches." })
          : e.jsx("div", {
            style: { maxHeight: "340px", overflowY: "auto", border: "1px solid var(--border-subtle)", borderRadius: "var(--radius-md)" },
            children: shown.map((o) => {
              const sel = picked[keyOf(o)];
              const unit = kindOf(o.serviceType).units[0];
              const rate = rateFor(o, unit);
              return e.jsxs("div", {
                style: { display: "flex", alignItems: "center", gap: "10px", padding: "8px 12px", borderBottom: "1px solid var(--border-subtle)" },
                children: [
                  e.jsxs("label", {
                    style: { display: "flex", alignItems: "center", gap: "10px", flex: 1, cursor: "pointer" },
                    children: [
                      e.jsx("input", { type: "checkbox", checked: Boolean(sel), onChange: () => toggle(o) }),
                      e.jsxs("span", {
                        children: [
                          e.jsx("div", { style: { fontWeight: 600 }, children: o.name }),
                          e.jsx("div", {
                            className: "qtn-cell__muted",
                            children: `${kindOf(o.serviceType).label} · ${rate === null ? `No ${unit === ONE_TIME ? "" : `${unit.toLowerCase()} `}price set` : `${rupees(rate)}${unit === ONE_TIME ? "" : ` / ${unit}`}`}`,
                          }),
                        ],
                      }),
                    ],
                  }),
                  e.jsx("input", {
                    className: "field__input",
                    type: "number", min: "0", step: "1",
                    "aria-label": `Quantity of ${o.name}`,
                    disabled: !sel,
                    value: sel ? sel.quantity : "",
                    placeholder: "Qty",
                    style: { width: "80px", textAlign: "right" },
                    onChange: (ev) => setPicked((p) => ({ ...p, [keyOf(o)]: { ...p[keyOf(o)], quantity: ev.target.value } })),
                  }),
                ],
              }, keyOf(o));
            }),
          }),
      ],
    }),
  });
}

/** Amounts shown while typing. The server recalculates and stores the real
 *  numbers on save — these exist only so the admin sees the total move. */
function previewTotals(lines, discountType, discountValue, tdsPercent, adjustment, roundOff) {
  const paying = lines.filter((l) => Number(l.lineType) !== LINE_TYPE.Header);
  const amounts = paying.map((l) => {
    const gross = (Number(l.quantity) || 0) * (Number(l.durationValue) || 1) * (Number(l.unitPrice) || 0);
    return Math.max(0, round2(gross - (Number(l.discountAmount) || 0)));
  });
  const subTotal = round2(amounts.reduce((a, b) => a + b, 0));
  const discountTotal = Number(discountType) === DISCOUNT_TYPE.Percentage
    ? round2((subTotal * (Number(discountValue) || 0)) / 100)
    : Math.min(round2(Number(discountValue) || 0), subTotal);

  let allocated = 0;
  let taxable = 0;
  let tax = 0;
  amounts.forEach((amount, index) => {
    const share = index === amounts.length - 1
      ? round2(discountTotal - allocated)
      : subTotal === 0 ? 0 : round2((discountTotal * amount) / subTotal);
    allocated += share;
    const lineTaxable = round2(amount - share);
    taxable += lineTaxable;
    tax += round2((lineTaxable * taxOf(paying[index])) / 100);
  });
  taxable = round2(taxable);
  tax = round2(tax);
  const tds = round2((taxable * (Number(tdsPercent) || 0)) / 100);
  const beforeRounding = round2(taxable + tax - tds + (Number(adjustment) || 0));
  const rounding = roundOff ? round2(Math.round(beforeRounding) - beforeRounding) : 0;
  return {
    subTotal, discountTotal, taxable, tax, tds,
    roundOff: rounding, total: round2(beforeRounding + rounding),
  };
}

const round2 = (value) => Math.round((value + Number.EPSILON) * 100) / 100;

function AdminQuotationFormPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const { id } = useParams();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();

  const [options, setOptions] = React.useState(null);
  const [existing, setExisting] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState("");
  const [customers, setCustomers] = React.useState([]);
  const [salespeople, setSalespeople] = React.useState([]);
  const [dirty, setDirty] = React.useState(false);
  const [discardOpen, setDiscardOpen] = React.useState(false);
  const [sendAfterSave, setSendAfterSave] = React.useState(null);

  const [header, setHeader] = React.useState({
    customerId: "", customerName: "", billingAddress: "", customerGstin: "", quotationDate: new Date().toISOString().slice(0, 10),
    validUntil: "", referenceNumber: "", subject: "", salespersonId: "",
    placeOfSupplyStateCode: "", discountType: String(DISCOUNT_TYPE.Percentage),
    discountValue: "0", tdsCode: "", adjustmentLabel: "Adjustment", adjustmentAmount: "0",
    advanceInvoiceEnabled: false, advanceType: "1", advanceValue: "1",
    termsAndConditions: "", remarks: "", internalNote: "",
  });
  const [lines, setLines] = React.useState([emptyLine()]);
  const [dragFrom, setDragFrom] = React.useState(null);
  const [dragOver, setDragOver] = React.useState(null);
  const [bulkOpen, setBulkOpen] = React.useState(false);
  const [itemsError, setItemsError] = React.useState("");

  const patchHeader = (patch) => { setDirty(true); setHeader((h) => ({ ...h, ...patch })); };
  const patchLine = (index, patch) => {
    setDirty(true);
    setLines((current) => current.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  };

  React.useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const [formOptions, customerPage] = await Promise.all([
          quotationsApi.formOptions(),
          customersApi.list({ page: 1, pageSize: 200 }),
        ]);
        setOptions(formOptions);
        /* Suggestions for the free-text customer box. Picking one links the
           account (portal approval, invoicing); a new name stays unlinked. */
        setCustomers(customerPage.items.map((c) => ({
          value: c.userId,
          name: c.fullName,
          label: [c.fullName, c.companyName, c.mobileNumber].filter(Boolean).join(" · "),
        })));

        /* The salesperson is an employee, never a customer. A staff list the
           admin cannot read is not an error — the field simply stays on the
           default, which is whoever is logged in. */
        try {
          const staff = await usersApi.list({ page: 1, pageSize: 200 });
          setSalespeople(staff.items
            .filter((u) => u.userType !== 1)
            .map((u) => ({ value: u.userId, label: u.fullName })));
        } catch {
          setSalespeople([]);
        }

        if (id) {
          const quotation = await quotationsApi.get(Number(id));
          setExisting(quotation);
          setHeader({
            customerId: quotation.customerId ? String(quotation.customerId) : "",
            customerName: quotation.customerName ?? "",
            billingAddress: quotation.billingAddress ?? "",
            customerGstin: quotation.customerGstin ?? "",
            quotationDate: quotation.quotationDate,
            validUntil: quotation.validUntil,
            referenceNumber: quotation.referenceNumber ?? "",
            subject: quotation.subject ?? "",
            salespersonId: quotation.salespersonId ? String(quotation.salespersonId) : "",
            placeOfSupplyStateCode: quotation.placeOfSupplyStateCode ?? "",
            discountType: String(quotation.discountType),
            discountValue: String(quotation.discountValue),
            tdsCode: quotation.tdsCode ?? "",
            adjustmentLabel: quotation.adjustmentLabel ?? "Adjustment",
            adjustmentAmount: String(quotation.adjustmentAmount),
            advanceInvoiceEnabled: quotation.advanceInvoiceEnabled,
            advanceType: String(quotation.advanceType),
            advanceValue: String(quotation.advanceValue),
            termsAndConditions: quotation.termsAndConditions ?? "",
            remarks: quotation.remarks ?? "",
            internalNote: quotation.internalNote ?? "",
          });
          setLines(quotation.lines.length
            ? quotation.lines.map((l) => rowFromLine(l, formOptions.services))
            : [{ ...emptyLine(), taxPercent: String(formOptions.defaultGstPercent) }]);
        } else {
          const today = new Date();
          const validUntil = new Date(today);
          validUntil.setDate(validUntil.getDate() + formOptions.defaultValidityDays);
          setHeader((h) => ({
            ...h,
            validUntil: validUntil.toISOString().slice(0, 10),
            placeOfSupplyStateCode: formOptions.homeStateCode,
            termsAndConditions: formOptions.defaultTermsAndConditions ?? "",
            remarks: formOptions.defaultCustomerNotes ?? "",
            advanceValue: String(formOptions.defaultAdvanceMonths),
          }));
          setLines([{ ...emptyLine(), taxPercent: String(formOptions.defaultGstPercent) }]);
        }

      } catch (error) {
        toast.error(errorMessage(error, "Could not open the quotation form."));
      } finally {
        setLoading(false);
      }
    })();
  }, [id]);

  /** The customer is typed freely. Choosing one of the suggestions links that
   *  account and fills a blank address from its profile; editing the name
   *  afterwards unlinks it again, so what is on screen is what gets saved. */
  const typeCustomer = (text) => {
    clearField("customerName");
    clearField("customerId");
    const match = customers.find((c) => c.label === text || c.name === text);
    if (!match) {
      patchHeader({ customerName: text, customerId: "" });
      return;
    }
    const id = String(match.value);
    patchHeader({ customerName: match.name, customerId: id });
    void customersApi.get(match.value).then((detail) => {
      if (!detail?.address) return;
      setHeader((h) => (h.customerId === id && !h.billingAddress.trim()
        ? { ...h, billingAddress: detail.address } : h));
    }).catch(() => { /* the address stays blank for the admin to type */ });
  };
  const linkedCustomer = customers.find((c) => String(c.value) === header.customerId);

  const tdsPercent = React.useMemo(() => {
    const option = options?.tdsOptions.find((t) => t.code === header.tdsCode);
    return option ? option.percent : 0;
  }, [options, header.tdsCode]);

  const totals = previewTotals(
    lines, header.discountType, header.discountValue, tdsPercent,
    header.adjustmentAmount, options?.roundOffEnabled ?? true,
  );

  const intraState = !header.placeOfSupplyStateCode
    || header.placeOfSupplyStateCode === options?.homeStateCode;

  /* Rows that go to the server (the blank trailing row never does). The API
     reports errors as lines[i] of this list; payloadRows maps i back. */
  const payloadRows = lines.map((l, i) => (isBlankLine(l) ? -1 : i)).filter((i) => i >= 0);

  const buildPayload = () => ({
    customerId: header.customerId ? Number(header.customerId) : null,
    customerName: header.customerName.trim() || null,
    billingAddress: header.billingAddress.trim() || null,
    customerGstin: header.customerGstin.trim() || null,
    // No location on the form: an edit keeps its own, a new one gets the default.
    locationId: null,
    quotationDate: header.quotationDate || null,
    validUntil: header.validUntil || null,
    referenceNumber: header.referenceNumber || null,
    subject: header.subject || null,
    salespersonId: header.salespersonId ? Number(header.salespersonId) : null,
    placeOfSupplyStateCode: header.placeOfSupplyStateCode || null,
    discountType: Number(header.discountType),
    discountValue: Number(header.discountValue || 0),
    tdsCode: header.tdsCode || null,
    adjustmentLabel: header.adjustmentLabel || null,
    adjustmentAmount: Number(header.adjustmentAmount || 0),
    advanceInvoiceEnabled: header.advanceInvoiceEnabled,
    advanceType: Number(header.advanceType),
    advanceValue: Number(header.advanceValue || 0),
    termsAndConditions: header.termsAndConditions || null,
    remarks: header.remarks || null,
    internalNote: header.internalNote || null,
    lines: payloadRows.map((i) => {
      const l = lines[i];
      const header = Number(l.lineType) === LINE_TYPE.Header;
      const custom = l.serviceType === CUSTOM_KIND;
      const oneTime = isOneTime(l);
      return {
        lineType: header ? LINE_TYPE.Header : custom ? LINE_TYPE.Custom : LINE_TYPE.Service,
        serviceType: custom ? SERVICE_TYPE.Other : Number(l.serviceType),
        workspaceTypeId: !custom && l.workspaceTypeId ? Number(l.workspaceTypeId) : null,
        serviceItemId: null,
        workspaceId: !custom && l.workspaceId ? Number(l.workspaceId) : null,
        passPlanId: !custom && l.passPlanId ? Number(l.passPlanId) : null,
        description: l.description.trim(),
        lineNotes: l.lineNotes?.trim() || null,
        hsnSacCode: l.hsnSacCode || null,
        quantity: Number(l.quantity || 0),
        // One-time is charged once: no duration, so the server multiplies by 1.
        durationValue: header || oneTime ? null : Number(l.durationValue || 1),
        durationUnit: header ? null : l.durationUnit || ONE_TIME,
        unitPrice: Number(l.unitPrice || 0),
        // The configured price; the server reports RateEdited when it differs.
        standardRate: custom ? null : l.standardRate,
        discountAmount: Number(l.discountAmount || 0),
        taxPercent: taxOf(l),
        inventoryItemId: l.inventoryItemId ? Number(l.inventoryItemId) : null,
        itemType: l.itemType ? Number(l.itemType) : null,
        unit: l.unit || null,
        isNonTaxable: Boolean(l.nonTaxable),
      };
    }),
  });

  const save = async (mode) => {
    clear();
    setItemsError("");
    /* At least one row with an item; quantity > 0 and rate >= 0 (the server
       checks the same, this just says so before the round trip). */
    const filled = payloadRows.filter((i) => Number(lines[i].lineType) !== LINE_TYPE.Header);
    const problem = filled.length === 0 ? "Add at least one service."
      : filled.some((i) => !(Number(lines[i].quantity) > 0)) ? "Every line needs a quantity greater than 0."
        : filled.some((i) => !isOneTime(lines[i]) && !(Number(lines[i].durationValue) > 0)) ? "Every line needs a duration greater than 0."
          : filled.some((i) => !(Number(lines[i].unitPrice || 0) >= 0)) ? "A rate cannot be negative."
            : mode === "send" && filled.some((i) => String(lines[i].unitPrice).trim() === "")
              ? "No price set on one or more lines — enter a rate before sending. You can still save a draft." : "";
    if (problem) {
      setItemsError(problem);
      toast.error(problem);
      return;
    }
    setSaving(mode);
    try {
      const payload = buildPayload();
      const saved = existing
        ? await quotationsApi.update(existing.quotationId, payload)
        : await quotationsApi.create(payload);
      setDirty(false);
      if (mode === "draft") {
        toast.success(existing ? "Quotation updated." : "Quotation saved as a draft.");
        navigate(`/admin/billing/quotations/${saved.quotationId}`);
      } else {
        setExisting(saved);
        setSendAfterSave(saved);
      }
    } catch (error) {
      const message = capture(error);
      if (message) toast.error(message);
      // SS3.4: take the admin to the first thing that needs fixing.
      const firstError = document.querySelector(".field__error, .form-error");
      if (firstError) firstError.scrollIntoView({ behavior: "smooth", block: "center" });
    } finally {
      setSaving("");
    }
  };

  /* Ctrl/Cmd + S saves the draft (SS3.4). */
  React.useEffect(() => {
    const onKey = (ev) => {
      if ((ev.ctrlKey || ev.metaKey) && ev.key.toLowerCase() === "s") {
        ev.preventDefault();
        if (!saving) void save("draft");
      }
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  });

  const leave = () => {
    if (dirty) setDiscardOpen(true);
    else navigate("/admin/billing/quotations");
  };

  if (loading) return e.jsx(Spinner, { center: true });
  if (!options) return null;

  const isRevision = existing && existing.revisionNumber > 0;
  const title = existing
    ? (isRevision ? `Revise — ${existing.quotationNumber}` : `Edit — ${existing.quotationNumber}`)
    : "New quotation";

  const blankRow = (kind = "1") => ({ ...emptyLine(kind), taxPercent: String(options.defaultGstPercent) });
  const services = options.services;

  /* Tax choices: the configured GST rates (plus Non-Taxable, and the line's own
     rate should the settings no longer list it). */
  const taxOptions = (line) => {
    const rates = options.gstRateOptions.map((rate) => String(rate));
    if (!line.nonTaxable && line.taxPercent !== "" && !rates.includes(String(Number(line.taxPercent)))) {
      rates.push(String(Number(line.taxPercent)));
    }
    return [{ value: "nt", label: "Non-Taxable" },
      ...rates.map((rate) => ({ value: rate, label: `${rate}%` }))];
  };

  const insertLineAt = (index, rows = [blankRow()]) => {
    setDirty(true);
    setLines((current) => [...current.slice(0, index), ...rows, ...current.slice(index)]);
  };

  const removeLine = (index) => {
    setDirty(true);
    setLines((current) => (current.length === 1 ? [blankRow()] : current.filter((_, i) => i !== index)));
  };

  /** Drag-and-drop reordering. */
  const moveLine = (from, to) => {
    if (from === to || from < 0 || to < 0) return;
    setDirty(true);
    setLines((current) => {
      const next = [...current];
      const [row] = next.splice(from, 1);
      next.splice(Math.min(to, next.length), 0, row);
      return next;
    });
  };

  /** Changing the service type clears the item and the rate and resets the
   *  unit to that type's default (§2.1 behaviour rules). */
  const changeKind = (index, kind) => {
    setDirty(true);
    setItemsError("");
    setLines((current) => current.map((l, i) => (i === index
      ? { ...blankRow(kind), key: l.key, quantity: l.quantity, taxPercent: l.taxPercent, nonTaxable: l.nonTaxable }
      : l)));
  };

  /** Picking an item fills the rate from its configured price in the row's unit. */
  const pickService = (index, option) => {
    clearField(`lines[${payloadRows.indexOf(index)}].description`);
    setItemsError("");
    setDirty(true);
    setLines((current) => current.map((l, i) => {
      if (i !== index) return l;
      const rate = rateFor(option, l.durationUnit);
      return {
        ...l,
        lineType: LINE_TYPE.Service,
        description: option.name,
        workspaceId: option.workspaceId ? String(option.workspaceId) : "",
        workspaceTypeId: option.workspaceTypeId ? String(option.workspaceTypeId) : "",
        passPlanId: option.passPlanId ? String(option.passPlanId) : "",
        standardRate: rate,
        unitPrice: rate === null ? "" : String(rate),
      };
    }));
  };

  /** Changing the unit re-fills the rate with that unit's configured price. */
  const changeUnit = (index, unit) => {
    setDirty(true);
    setLines((current) => current.map((l, i) => {
      if (i !== index) return l;
      const next = { ...l, durationUnit: unit, durationValue: unit === ONE_TIME ? "1" : l.durationValue || "1" };
      if (l.serviceType === CUSTOM_KIND) return next;
      const option = optionOf(services, l);
      if (!option) return next;
      const rate = rateFor(option, unit);
      return { ...next, standardRate: rate, unitPrice: rate === null ? "" : String(rate) };
    }));
  };

  const resetRate = (index) => patchLine(index, { unitPrice: String(lines[index].standardRate) });

  /** Bulk add: each picked service becomes a line at its default unit and price. */
  const addServicesInBulk = (picks) => {
    if (picks.length === 0) return;
    setDirty(true);
    setLines((current) => {
      const body = current.filter((l) => !isBlankLine(l));
      const rows = picks.map(({ option, quantity }) => {
        const kind = String(option.serviceType);
        const row = blankRow(kind);
        const rate = rateFor(option, row.durationUnit);
        return {
          ...row,
          description: option.name,
          workspaceId: option.workspaceId ? String(option.workspaceId) : "",
          workspaceTypeId: option.workspaceTypeId ? String(option.workspaceTypeId) : "",
          passPlanId: option.passPlanId ? String(option.passPlanId) : "",
          quantity: String(quantity),
          standardRate: rate,
          unitPrice: rate === null ? "" : String(rate),
        };
      });
      return [...body, ...rows].slice(0, 100);
    });
  };

  return e.jsxs("div", {
    className: "qtn-form",
    children: [
      e.jsx(PageHeader, {
        title,
        description: existing
          ? "Only a draft can be edited. Sending freezes these figures."
          : "The server issues the final number when you first save.",
        actions: e.jsx(Button, { variant: "ghost", onClick: leave, children: "✕ Close" }),
      }),

      summary && e.jsx("div", { className: "form-error", role: "alert", children: summary }),

      /* Customer band */
      e.jsx("div", {
        className: "qtn-band",
        children: e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Input, {
              label: "Customer",
              required: true,
              maxLength: 150,
              autoComplete: "off",
              list: "qtn-customer-suggestions",
              placeholder: "Type the customer name",
              value: header.customerName,
              hint: linkedCustomer
                ? `Linked to ${linkedCustomer.label} — portal approval and invoicing available`
                : header.customerName.trim()
                  ? "Not linked to a customer account — pick a suggestion to link one"
                  : "Existing customers are suggested as you type",
              error: fieldErrors.customerName || fieldErrors.customerId,
              onChange: (ev) => typeCustomer(ev.target.value),
            }),
            e.jsx("datalist", {
              id: "qtn-customer-suggestions",
              children: customers.map((c) => e.jsx("option", { value: c.label }, c.value)),
            }),
            e.jsx(Input, {
              label: "GSTIN",
              maxLength: 15,
              autoComplete: "off",
              placeholder: "e.g. 29ABCDE1234F1Z5",
              value: header.customerGstin,
              hint: "Optional; printed on the quotation",
              error: fieldErrors.customerGstin,
              onChange: (ev) => {
                clearField("customerGstin");
                patchHeader({ customerGstin: ev.target.value.toUpperCase().replace(/\s/g, "") });
              },
            }),
            e.jsx(Select, {
              label: "Place of supply",
              required: true,
              value: header.placeOfSupplyStateCode,
              placeholder: "Select a state",
              options: options.states.map((s) => ({ value: s.code, label: `${s.code} · ${s.name}` })),
              hint: intraState ? "CGST + SGST (intra-state)" : "IGST (inter-state)",
              error: fieldErrors.placeOfSupplyStateCode,
              onChange: (ev) => {
                clearField("placeOfSupplyStateCode");
                patchHeader({ placeOfSupplyStateCode: ev.target.value });
              },
            }),
            e.jsxs("div", {
              className: "field",
              style: { gridColumn: "1 / -1" },
              children: [
                e.jsx("label", { className: "field__label", htmlFor: "qtn-billing-address", children: "Address" }),
                e.jsx("textarea", {
                  id: "qtn-billing-address",
                  className: `field__input${fieldErrors.billingAddress ? " field__input--error" : ""}`,
                  rows: 3,
                  maxLength: 500,
                  placeholder: "Street, area, city, state and PIN code",
                  value: header.billingAddress,
                  onChange: (ev) => { clearField("billingAddress"); patchHeader({ billingAddress: ev.target.value }); },
                }),
                fieldErrors.billingAddress
                  ? e.jsx("span", { className: "field__error", role: "alert", children: fieldErrors.billingAddress })
                  : e.jsx("span", { className: "field__hint", children: "The customer's complete address, as printed on the quotation." }),
              ],
            }),
          ],
        }),
      }),

      /* Quote details */
      e.jsx(Card, {
        title: "Quotation details",
        children: e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Input, {
              label: "Quotation #",
              readOnly: true,
              value: existing ? existing.quotationNumber : options.nextQuotationNumber,
              hint: existing ? "Issued" : "🔒 Preview — the server issues the final number on save",
            }),
            e.jsx(Input, {
              label: "Reference #",
              maxLength: 50,
              value: header.referenceNumber,
              hint: "Customer PO or enquiry number",
              error: fieldErrors.referenceNumber,
              onChange: (ev) => patchHeader({ referenceNumber: ev.target.value }),
            }),
            e.jsx(Input, {
              label: "Quote date",
              type: "date",
              required: true,
              value: header.quotationDate,
              error: fieldErrors.quotationDate,
              onChange: (ev) => patchHeader({ quotationDate: ev.target.value }),
            }),
            e.jsx(Input, {
              label: "Valid until",
              type: "date",
              required: true,
              value: header.validUntil,
              min: header.quotationDate,
              hint: `Defaults to ${options.defaultValidityDays} days after the quote date`,
              error: fieldErrors.validUntil,
              onChange: (ev) => { clearField("validUntil"); patchHeader({ validUntil: ev.target.value }); },
            }),
            e.jsx(Select, {
              label: "Salesperson",
              value: header.salespersonId,
              placeholder: "Logged-in admin",
              options: salespeople,
              hint: "Defaults to you; used in reports",
              onChange: (ev) => patchHeader({ salespersonId: ev.target.value }),
            }),
          ],
        }),
      }),

      e.jsx(Card, {
        children: e.jsxs("div", {
          className: "field",
          children: [
            e.jsx("label", { className: "field__label", children: "Subject" }),
            e.jsx("textarea", {
              className: "field__input",
              rows: 2,
              maxLength: 250,
              value: header.subject,
              onChange: (ev) => patchHeader({ subject: ev.target.value }),
            }),
            e.jsx("span", {
              className: "field__hint",
              children: "Shown at the top of the quotation PDF and in the email subject.",
            }),
          ],
        }),
      }),

      /* Services (specification §2.1). One CSS grid template drives the header
         and every row, so the columns cannot drift apart; header text starts
         where the text inside the control below it starts (8px padding + 1px
         border). Below 768px each row becomes a card with its own labels. */
      e.jsxs(Card, {
        title: "Services",
        children: [
          e.jsx("style", {
            children: [
              ".qtn-grid{--qtn-cols:16px 150px minmax(120px,1fr) 56px 60px 112px 92px 100px 100px 32px;--qtn-pad:9px;overflow-x:auto}",
              /* The same floor for header and rows: the column minimums + 9 gaps of 6. */
              ".qtn-grid__head,.qtn-grid__row{display:grid;grid-template-columns:var(--qtn-cols);column-gap:6px;align-items:start;min-width:892px}",
              ".qtn-grid__head{padding:0 0 10px;border-bottom:2px solid var(--border-subtle);font-size:var(--font-size-sm);font-weight:700;color:var(--text-secondary);white-space:nowrap}",
              ".qtn-grid__head>.qtn-col{padding:0 var(--qtn-pad)}",
              ".qtn-grid__row{padding:12px 0 10px;border-bottom:1px solid var(--border-subtle);row-gap:6px;transition:background var(--transition-fast)}",
              ".qtn-grid__row:hover{background:var(--surface-hover)}",
              /* Fixed 38px controls with no vertical padding, so descenders are never clipped. */
              ".qtn-grid__row input.field__input,.qtn-grid__row select.field__input{height:38px;padding:0 8px;line-height:36px}",
              ".qtn-grid__row select.field__input{padding-right:4px}",
              ".qtn-grid__row .field__input:disabled{opacity:.6;cursor:not-allowed}",
              ".qtn-grid .qtn-num,.qtn-grid .qtn-num input{text-align:right}",
              ".qtn-grid__amount{padding:0 var(--qtn-pad);line-height:38px;text-align:right;font-weight:700;white-space:nowrap}",
              ".qtn-grid__handle{display:block;line-height:38px;text-align:center;color:var(--text-muted);cursor:grab;user-select:none;opacity:0;transition:opacity var(--transition-fast)}",
              ".qtn-grid__row:hover .qtn-grid__handle,.qtn-grid__handle:focus-visible{opacity:1}",
              ".qtn-grid__actions{display:flex;align-items:center;justify-content:center;height:38px}",
              ".qtn-grid__sub{margin-top:4px;padding:0 var(--qtn-pad);font-size:var(--font-size-xs);color:var(--text-muted);white-space:nowrap}",
              ".qtn-grid__sub.qtn-num{text-align:right}",
              ".qtn-grid__warn{margin-top:4px;font-size:var(--font-size-xs);color:var(--color-danger);white-space:normal;text-align:right;line-height:1.3}",
              ".qtn-grid__summary{grid-column:2/-1;padding:0 var(--qtn-pad);font-size:var(--font-size-xs);color:var(--text-muted)}",
              ".qtn-grid__footer{display:flex;gap:var(--space-2);flex-wrap:wrap;margin-top:var(--space-3);padding-left:22px}",
              ".qtn-grid__hint{padding-left:22px}",
              ".qtn-grid .qtn-col--label{display:none}",
              "@media (max-width:767px){",
              ".qtn-grid__head{display:none}",
              ".qtn-grid__row{grid-template-columns:1fr 1fr;row-gap:12px;min-width:0;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:12px;margin-bottom:12px}",
              ".qtn-grid__row>.qtn-col--handle{display:none}",
              ".qtn-grid__row>.qtn-col--wide,.qtn-grid__row>.qtn-grid__summary{grid-column:1/-1}",
              ".qtn-grid__actions{justify-content:flex-end}",
              ".qtn-grid .qtn-col--label{display:block;margin-bottom:4px;font-size:var(--font-size-xs);font-weight:700;color:var(--text-secondary)}",
              ".qtn-grid__amount{line-height:1.4;padding-top:10px}",
              ".qtn-grid__summary{padding:0}",
              ".qtn-grid__footer,.qtn-grid__hint{padding-left:0}",
              "}",
            ].join(""),
          }),
          e.jsxs("div", {
            className: "qtn-grid",
            role: "table",
            "aria-label": "Quoted services",
            children: [
              e.jsxs("div", {
                className: "qtn-grid__head",
                role: "row",
                children: [
                  e.jsx("div", { role: "columnheader", "aria-label": "Reorder" }),
                  e.jsx("div", { className: "qtn-col", role: "columnheader", children: "Service type" }),
                  e.jsx("div", { className: "qtn-col", role: "columnheader", children: "Item" }),
                  e.jsx("div", { className: "qtn-col qtn-num", role: "columnheader", children: "Qty" }),
                  e.jsx("div", { className: "qtn-col qtn-num", role: "columnheader", children: "Duration" }),
                  e.jsx("div", { className: "qtn-col", role: "columnheader", children: "Unit" }),
                  e.jsx("div", { className: "qtn-col qtn-num", role: "columnheader", children: "Rate (₹)" }),
                  e.jsx("div", { className: "qtn-col", role: "columnheader", children: "Tax" }),
                  e.jsx("div", { className: "qtn-col qtn-num", role: "columnheader", children: "Amount" }),
                  e.jsx("div", { role: "columnheader", "aria-label": "Delete" }),
                ],
              }),
              ...lines.map((line, index) => {
                const isHeader = Number(line.lineType) === LINE_TYPE.Header;
                const onlyRow = lines.length === 1;
                const pi = payloadRows.indexOf(index);
                const err = (field) => (pi >= 0 ? fieldErrors[`lines[${pi}].${field}`] : undefined);
                const label = (text) => e.jsx("span", { className: "qtn-col--label", children: text });
                const kind = kindOf(line.serviceType);
                const custom = line.serviceType === CUSTOM_KIND;
                const oneTime = isOneTime(line);
                const picked = !custom && Boolean(String(line.description).trim());
                const rateEmpty = String(line.unitPrice).trim() === "";
                const noPrice = picked && line.standardRate === null && rateEmpty;
                const edited = !custom && line.standardRate !== null && !rateEmpty
                  && Number(line.unitPrice) !== Number(line.standardRate);
                const rowStyle = {
                  boxShadow: dragOver === index && dragFrom !== null && dragFrom !== index
                    ? "inset 0 2px 0 var(--accent-strong)" : undefined,
                  opacity: dragFrom === index ? 0.5 : 1,
                };
                const dragProps = {
                  onDragOver: (ev) => { if (dragFrom !== null) { ev.preventDefault(); setDragOver(index); } },
                  onDrop: (ev) => { ev.preventDefault(); if (dragFrom !== null) moveLine(dragFrom, index); setDragFrom(null); setDragOver(null); },
                };
                const handle = e.jsx("div", {
                  className: "qtn-col--handle",
                  role: "cell",
                  children: e.jsx("span", {
                    className: "qtn-grid__handle",
                    draggable: true,
                    role: "button",
                    tabIndex: 0,
                    "aria-label": `Drag row ${index + 1} to reorder`,
                    title: "Drag to reorder",
                    onDragStart: (ev) => { setDragFrom(index); ev.dataTransfer.effectAllowed = "move"; ev.dataTransfer.setData("text/plain", String(index)); },
                    onDragEnd: () => { setDragFrom(null); setDragOver(null); },
                    children: "⋮⋮",
                  }),
                });
                const del = e.jsx("div", {
                  role: "cell",
                  children: e.jsx("div", {
                    className: "qtn-grid__actions",
                    children: e.jsx("button", {
                      type: "button",
                      className: "qtn-iconbtn qtn-iconbtn--danger",
                      "aria-label": `Delete row ${index + 1}`,
                      title: onlyRow ? "A quotation keeps at least one row" : "Delete row",
                      disabled: onlyRow,
                      style: onlyRow ? { opacity: 0.35, cursor: "not-allowed" } : undefined,
                      onClick: () => removeLine(index),
                      children: "✕",
                    }),
                  }),
                });

                /* A section heading saved by an older form stays editable. */
                if (isHeader) {
                  return e.jsxs("div", {
                    className: "qtn-grid__row", role: "row", style: rowStyle, ...dragProps,
                    children: [
                      handle,
                      e.jsx("div", {
                        className: "qtn-col--wide", role: "cell", style: { gridColumn: "2 / 10" },
                        children: e.jsx("input", {
                          className: "field__input", style: { fontWeight: 700 },
                          "aria-label": `Section heading ${index + 1}`,
                          value: line.description,
                          onChange: (ev) => patchLine(index, { description: ev.target.value }),
                        }),
                      }),
                      del,
                    ],
                  }, line.key);
                }

                return e.jsxs("div", {
                  className: "qtn-grid__row", role: "row", style: rowStyle, ...dragProps,
                  children: [
                    handle,
                    e.jsxs("div", {
                      className: "qtn-col--wide", role: "cell",
                      children: [
                        label("Service type"),
                        e.jsx("select", {
                          className: "field__input",
                          "aria-label": `Service type ${index + 1}`,
                          value: line.serviceType,
                          onChange: (ev) => changeKind(index, ev.target.value),
                          children: SERVICE_KINDS.map((k) => e.jsx("option", { value: k.value, children: k.label }, k.value)),
                        }),
                      ],
                    }),
                    e.jsxs("div", {
                      className: "qtn-col--wide", role: "cell", style: { minWidth: 0 },
                      children: [
                        label("Item"),
                        custom
                          ? e.jsx("input", {
                            className: `field__input${err("description") ? " field__input--error" : ""}`,
                            "aria-label": `Item ${index + 1}`,
                            placeholder: "e.g. Setup / branding charges",
                            maxLength: 300,
                            value: line.description,
                            onChange: (ev) => { clearField(`lines[${pi}].description`); patchLine(index, { description: ev.target.value }); },
                          })
                          : e.jsx(ServicePicker, {
                            options: services.filter((o) => String(o.serviceType) === line.serviceType),
                            value: line.description,
                            unit: line.durationUnit,
                            rowNumber: index + 1,
                            placeholder: `Select a ${kind.label.toLowerCase()}`,
                            onPick: (option) => pickService(index, option),
                          }),
                        err("description") && e.jsx("div", { className: "field__error", role: "alert", children: err("description") }),
                      ],
                    }),
                    e.jsxs("div", {
                      className: "qtn-num", role: "cell",
                      children: [
                        label("Qty"),
                        e.jsx("input", {
                          className: `field__input${err("quantity") ? " field__input--error" : ""}`,
                          "aria-label": `Quantity ${index + 1}`,
                          type: "number", min: "0", step: "1",
                          value: line.quantity,
                          onChange: (ev) => { clearField(`lines[${pi}].quantity`); patchLine(index, { quantity: ev.target.value }); },
                        }),
                        err("quantity") && e.jsx("div", { className: "field__error", role: "alert", children: err("quantity") }),
                      ],
                    }),
                    e.jsxs("div", {
                      className: "qtn-num", role: "cell",
                      children: [
                        label("Duration"),
                        e.jsx("input", {
                          className: `field__input${err("durationValue") ? " field__input--error" : ""}`,
                          "aria-label": `Duration ${index + 1}`,
                          type: "number", min: "0", step: "1",
                          disabled: oneTime,
                          title: oneTime ? "One-time: charged once, so the duration is 1" : undefined,
                          value: oneTime ? "1" : line.durationValue,
                          onChange: (ev) => { clearField(`lines[${pi}].durationValue`); patchLine(index, { durationValue: ev.target.value }); },
                        }),
                        err("durationValue") && e.jsx("div", { className: "field__error", role: "alert", children: err("durationValue") }),
                      ],
                    }),
                    e.jsxs("div", {
                      role: "cell",
                      children: [
                        label("Unit"),
                        e.jsx("select", {
                          className: "field__input",
                          "aria-label": `Unit ${index + 1}`,
                          value: line.durationUnit || ONE_TIME,
                          disabled: kind.units.length === 1,
                          onChange: (ev) => changeUnit(index, ev.target.value),
                          /* An older line may carry a unit this type no longer offers. */
                          children: [...new Set([...kind.units, line.durationUnit || ONE_TIME])]
                            .map((u) => e.jsx("option", { value: u, children: u }, u)),
                        }),
                      ],
                    }),
                    e.jsxs("div", {
                      className: "qtn-num", role: "cell",
                      children: [
                        label("Rate (₹)"),
                        e.jsx("input", {
                          className: `field__input${err("unitPrice") || noPrice ? " field__input--error" : ""}`,
                          "aria-label": `Rate ${index + 1}`,
                          type: "number", min: "0", step: "0.01",
                          placeholder: noPrice ? "Enter rate" : undefined,
                          value: line.unitPrice,
                          onChange: (ev) => { clearField(`lines[${pi}].unitPrice`); patchLine(index, { unitPrice: ev.target.value }); },
                        }),
                        noPrice && e.jsx("div", { className: "qtn-grid__warn", role: "status", children: "No price set – enter rate" }),
                        edited && e.jsxs("div", {
                          className: "qtn-grid__sub qtn-num",
                          title: `Configured price ${rupees(line.standardRate)}`,
                          children: [
                            e.jsx("span", { style: { color: "var(--regalis-gold-dark)", fontWeight: 600 }, children: "edited" }),
                            " ",
                            e.jsx("button", {
                              type: "button",
                              "aria-label": `Reset row ${index + 1} to the configured price ${rupees(line.standardRate)}`,
                              title: `Reset to ${rupees(line.standardRate)}`,
                              onClick: () => resetRate(index),
                              style: { background: "none", border: 0, padding: 0, cursor: "pointer", color: "var(--regalis-gold-dark)", font: "inherit" },
                              children: "↺",
                            }),
                          ],
                        }),
                        err("unitPrice") && e.jsx("div", { className: "field__error", role: "alert", children: err("unitPrice") }),
                      ],
                    }),
                    e.jsxs("div", {
                      role: "cell",
                      children: [
                        label("Tax"),
                        e.jsx("select", {
                          className: "field__input",
                          "aria-label": `Tax ${index + 1}`,
                          value: line.nonTaxable ? "nt" : line.taxPercent,
                          onChange: (ev) => patchLine(index, ev.target.value === "nt"
                            ? { nonTaxable: true }
                            : { nonTaxable: false, taxPercent: ev.target.value }),
                          children: taxOptions(line).map((o) => e.jsx("option", { value: o.value, children: o.label }, o.value)),
                        }),
                      ],
                    }),
                    e.jsxs("div", {
                      role: "cell",
                      children: [
                        label("Amount"),
                        e.jsx("div", { className: "qtn-grid__amount", children: formatAmount(lineAmountOf(line)) }),
                      ],
                    }),
                    del,
                    /* The §2.1 line text, exactly as the PDF will print it. */
                    String(line.description).trim() && e.jsx("div", {
                      className: "qtn-grid__summary", role: "cell",
                      children: lineSummary(line),
                    }),
                  ],
                }, line.key);
              }),
            ],
          }),
          e.jsxs("div", {
            className: "qtn-grid__footer",
            children: [
              e.jsx(Button, { size: "sm", variant: "outline", disabled: lines.length >= 100, onClick: () => insertLineAt(lines.length), children: "+ Add New Row" }),
              e.jsx(Button, { size: "sm", variant: "outline", disabled: lines.length >= 100, onClick: () => setBulkOpen(true), children: "+ Add Items in Bulk" }),
            ],
          }),
          e.jsx("p", {
            className: "field__hint qtn-grid__hint",
            children: `Rates are auto-filled from prices configured in the app. You can change the rate on any line. Tax is charged as ${intraState ? "CGST + SGST (intra-state)" : "IGST (inter-state)"}.`,
          }),
          itemsError && e.jsx("p", { className: "field__error qtn-grid__hint", role: "alert", children: itemsError }),
          payloadRows.length >= 100 && e.jsx("p", {
            className: "field__error qtn-grid__hint", children: "A quotation carries at most 100 lines.",
          }),
        ],
      }),

      bulkOpen && e.jsx(BulkServicesModal, {
        services,
        onClose: () => setBulkOpen(false),
        onAdd: (picks) => { addServicesInBulk(picks); setBulkOpen(false); },
      }),

      /* Notes beside the totals box, as in the document */
      e.jsxs("div", {
        className: "qtn-closing",
        children: [
          e.jsxs(Card, {
            title: "Customer notes",
            children: [
              e.jsx("textarea", {
                className: "field__input",
                rows: 4,
                maxLength: 1000,
                value: header.remarks,
                onChange: (ev) => patchHeader({ remarks: ev.target.value }),
              }),
              e.jsx("span", { className: "field__hint", children: "Printed on the PDF." }),
            ],
          }),

          e.jsxs(Card, {
            className: "qtn-totals",
            title: "Totals",
            children: [
              e.jsxs("div", {
                className: "qtn-totals__row",
                children: [e.jsx("span", { children: "Sub total" }), e.jsx("span", { children: formatInr(totals.subTotal) })],
              }),
              e.jsxs("div", {
                className: "qtn-totals__row qtn-totals__row--input",
                children: [
                  e.jsx("span", { children: "Discount" }),
                  e.jsxs("span", {
                    className: "qtn-totals__control",
                    children: [
                      e.jsx("input", {
                        className: "field__input qtn-totals__num",
                        type: "number", min: "0", step: "0.01",
                        value: header.discountValue,
                        onChange: (ev) => { clearField("discountValue"); patchHeader({ discountValue: ev.target.value }); },
                      }),
                      e.jsxs("select", {
                        className: "field__input qtn-totals__unit",
                        value: header.discountType,
                        onChange: (ev) => patchHeader({ discountType: ev.target.value }),
                        children: [
                          e.jsx("option", { value: "1", children: "%" }),
                          e.jsx("option", { value: "2", children: "₹" }),
                        ],
                      }),
                    ],
                  }),
                ],
              }),
              fieldErrors.discountValue && e.jsx("div", {
                className: "field__error", children: fieldErrors.discountValue,
              }),
              e.jsxs("div", {
                className: "qtn-totals__row",
                children: [
                  e.jsx("span", { children: "Taxable" }),
                  e.jsx("span", { children: formatInr(totals.taxable) }),
                ],
              }),
              intraState
                ? e.jsxs(e.Fragment, {
                  children: [
                    e.jsxs("div", {
                      className: "qtn-totals__row",
                      children: [e.jsx("span", { children: "CGST" }), e.jsx("span", { children: formatInr(round2(totals.tax / 2)) })],
                    }),
                    e.jsxs("div", {
                      className: "qtn-totals__row",
                      children: [e.jsx("span", { children: "SGST" }), e.jsx("span", { children: formatInr(round2(totals.tax - round2(totals.tax / 2))) })],
                    }),
                  ],
                })
                : e.jsxs("div", {
                  className: "qtn-totals__row",
                  children: [e.jsx("span", { children: "IGST" }), e.jsx("span", { children: formatInr(totals.tax) })],
                }),
              e.jsxs("div", {
                className: "qtn-totals__row qtn-totals__row--input",
                children: [
                  e.jsx("span", { children: "TDS" }),
                  e.jsx("select", {
                    className: "field__input qtn-totals__unit qtn-totals__tds",
                    value: header.tdsCode,
                    onChange: (ev) => patchHeader({ tdsCode: ev.target.value }),
                    children: [
                      e.jsx("option", { value: "", children: "None" }),
                      ...options.tdsOptions.map((t) => e.jsx("option", { value: t.code, children: t.label }, t.code)),
                    ],
                  }),
                ],
              }),
              totals.tds > 0 && e.jsxs("div", {
                className: "qtn-totals__row",
                children: [e.jsx("span", { children: "TDS deducted" }), e.jsx("span", { children: `− ${formatInr(totals.tds)}` })],
              }),
              e.jsxs("div", {
                className: "qtn-totals__row qtn-totals__row--input",
                children: [
                  e.jsx("input", {
                    className: "field__input qtn-totals__label",
                    value: header.adjustmentLabel,
                    maxLength: 50,
                    "aria-label": "Adjustment label",
                    onChange: (ev) => patchHeader({ adjustmentLabel: ev.target.value }),
                  }),
                  e.jsx("input", {
                    className: "field__input qtn-totals__num",
                    type: "number", step: "0.01",
                    value: header.adjustmentAmount,
                    "aria-label": "Adjustment amount",
                    onChange: (ev) => patchHeader({ adjustmentAmount: ev.target.value }),
                  }),
                ],
              }),
              options.roundOffEnabled && e.jsxs("div", {
                className: "qtn-totals__row",
                children: [e.jsx("span", { children: "Round off" }), e.jsx("span", { children: formatInr(totals.roundOff) })],
              }),
              e.jsxs("div", {
                className: "qtn-totals__row qtn-totals__row--grand",
                children: [e.jsx("span", { children: "Total" }), e.jsx("span", { children: formatInr(totals.total) })],
              }),
              e.jsx("p", {
                className: "field__hint",
                children: "The server recalculates every figure on save; this is a preview.",
              }),
            ],
          }),
        ],
      }),

      /* Terms, attachments, advance */
      e.jsxs("div", {
        className: "qtn-closing",
        children: [
          e.jsxs(Card, {
            title: "Terms & conditions",
            children: [
              e.jsx("textarea", {
                className: "field__input qtn-terms",
                rows: 8,
                maxLength: 2000,
                value: header.termsAndConditions,
                onChange: (ev) => patchHeader({ termsAndConditions: ev.target.value }),
              }),
              e.jsx("span", {
                className: "field__hint",
                children: "Pre-filled from Settings; editable for this quotation.",
              }),
            ],
          }),
          e.jsxs(Card, {
            title: "Attachments & advance",
            children: [
              existing
                ? e.jsx(AttachmentBox, {
                  quotation: existing,
                  onChanged: setExisting,
                  disabled: existing.status !== QUOTATION_STATUS.Draft,
                })
                : e.jsx("p", {
                  className: "field__hint",
                  children: "Save the draft once, then attach up to 5 files of 10 MB each.",
                }),
              e.jsx("div", {
                className: "qtn-advance",
                children: e.jsx(Checkbox, {
                  label: "Raise an advance (security deposit) invoice when the customer approves",
                  checked: header.advanceInvoiceEnabled,
                  onChange: (ev) => patchHeader({ advanceInvoiceEnabled: ev.target.checked }),
                }),
              }),
              header.advanceInvoiceEnabled && e.jsxs("div", {
                className: "form-grid",
                children: [
                  e.jsx(Input, {
                    label: "Advance amount",
                    type: "number", min: "0", step: "0.01",
                    value: header.advanceValue,
                    onChange: (ev) => patchHeader({ advanceValue: ev.target.value }),
                  }),
                  e.jsx(Select, {
                    label: "Advance basis",
                    value: header.advanceType,
                    options: [
                      { value: "1", label: "month(s) of rent" },
                      { value: "2", label: "fixed ₹" },
                    ],
                    onChange: (ev) => patchHeader({ advanceType: ev.target.value }),
                  }),
                ],
              }),
              e.jsxs("div", {
                className: "field",
                children: [
                  e.jsx("label", { className: "field__label", children: "Internal note (admin only)" }),
                  e.jsx("textarea", {
                    className: "field__input",
                    rows: 3,
                    maxLength: 1000,
                    value: header.internalNote,
                    onChange: (ev) => patchHeader({ internalNote: ev.target.value }),
                  }),
                  e.jsx("span", {
                    className: "field__hint",
                    children: "Never shown to the customer and never printed.",
                  }),
                ],
              }),
            ],
          }),
        ],
      }),

      /* Sticky action bar */
      e.jsxs("div", {
        className: "qtn-actionbar",
        children: [
          e.jsxs("div", {
            className: "qtn-actionbar__meta",
            children: [
              "PDF template: ",
              e.jsx("strong", { children: "Regalis Standard – India GST" }),
            ],
          }),
          e.jsxs("div", {
            className: "row-actions",
            children: [
              e.jsx(Button, { variant: "ghost", onClick: leave, disabled: Boolean(saving), children: "Cancel" }),
              e.jsx(Button, {
                variant: "outline",
                loading: saving === "draft",
                disabled: Boolean(saving),
                onClick: () => void save("draft"),
                children: "Save as draft",
              }),
              e.jsx(Button, {
                loading: saving === "send",
                disabled: Boolean(saving),
                onClick: () => void save("send"),
                children: "Save and send",
              }),
            ],
          }),
        ],
      }),

      discardOpen && e.jsx(Modal, {
        open: true,
        title: "Discard changes?",
        onClose: () => setDiscardOpen(false),
        footer: e.jsxs(e.Fragment, {
          children: [
            e.jsx(Button, { variant: "ghost", onClick: () => setDiscardOpen(false), children: "Keep editing" }),
            e.jsx(Button, {
              variant: "danger",
              onClick: () => navigate("/admin/billing/quotations"),
              children: "Discard",
            }),
          ],
        }),
        children: e.jsx("p", { children: "Anything you have typed since the last save will be lost." }),
      }),

      sendAfterSave && e.jsx(SendQuotationModal, {
        quotation: sendAfterSave,
        onClose: () => {
          setSendAfterSave(null);
          navigate(`/admin/billing/quotations/${sendAfterSave.quotationId}`);
        },
        onSent: () => {
          const target = sendAfterSave;
          setSendAfterSave(null);
          navigate(`/admin/billing/quotations/${target.quotationId}`);
        },
      }),
    ],
  });
}

/* ── Attachments (SS3.4) ────────────────────────────────────────────────── */

function AttachmentBox({ quotation, onChanged, disabled }) {
  const toast = useToast();
  const [busy, setBusy] = React.useState(false);
  const inputRef = React.useRef(null);

  const run = async (label, action) => {
    setBusy(true);
    try {
      onChanged(await action());
    } catch (error) {
      toast.error(errorMessage(error, `Could not ${label}.`));
    } finally {
      setBusy(false);
    }
  };

  return e.jsxs("div", {
    className: "qtn-files",
    children: [
      e.jsx("input", {
        type: "file",
        ref: inputRef,
        style: { display: "none" },
        accept: ".pdf,.jpg,.jpeg,.png,.docx,.xlsx",
        onChange: (ev) => {
          const file = ev.target.files?.[0];
          ev.target.value = "";
          if (file) void run("attach the file", () => quotationsApi.addAttachment(quotation.quotationId, file));
        },
      }),
      e.jsx(Button, {
        size: "sm",
        variant: "outline",
        disabled: disabled || busy || quotation.attachments.length >= 5,
        onClick: () => inputRef.current?.click(),
        children: "Upload file",
      }),
      e.jsx("span", { className: "field__hint", children: "Maximum of 5 files, 10 MB each." }),
      e.jsx("div", {
        className: "qtn-chips",
        children: quotation.attachments.map((file) => e.jsxs("div", {
          className: "qtn-chip",
          children: [
            e.jsx("span", { className: "qtn-chip__name", children: file.fileName }),
            e.jsx("span", { className: "qtn-chip__size", children: `${Math.round(file.fileSizeBytes / 1024)} KB` }),
            e.jsx(Checkbox, {
              label: "Send with email",
              checked: file.sendWithEmail,
              disabled: disabled || busy,
              onChange: (ev) => void run("update the attachment", () => quotationsApi.updateAttachment(
                quotation.quotationId, file.quotationAttachmentId, ev.target.checked,
              )),
            }),
            !disabled && e.jsx("button", {
              type: "button",
              className: "qtn-iconbtn qtn-iconbtn--danger",
              "aria-label": `Remove ${file.fileName}`,
              onClick: () => void run("remove the attachment", () => quotationsApi.removeAttachment(
                quotation.quotationId, file.quotationAttachmentId,
              )),
              children: "✕",
            }),
          ],
        }, file.quotationAttachmentId)),
      }),
    ],
  });
}

/* ── Send popup (SS3.5) ─────────────────────────────────────────────────── */

function SendQuotationModal({ quotation, onClose, onSent }) {
  const toast = useToast();
  const [busy, setBusy] = React.useState(false);
  const [failure, setFailure] = React.useState(null);
  const [form, setForm] = React.useState({
    sendEmail: true,
    sendWhatsApp: Boolean(quotation.customerMobile),
    emailTo: quotation.customerEmail ?? "",
    emailCc: "",
    bccMe: false,
    emailSubject: "",
    emailMessage: "",
    whatsAppTo: quotation.customerMobile ?? "",
    whatsAppMessage: "",
  });
  const patch = (p) => setForm((f) => ({ ...f, ...p }));

  const submit = async () => {
    setBusy(true);
    setFailure(null);
    try {
      await quotationsApi.send(quotation.quotationId, {
        ...form,
        emailSubject: form.emailSubject || null,
        emailMessage: form.emailMessage || null,
        whatsAppMessage: form.whatsAppMessage || null,
        emailCc: form.emailCc || null,
      });
      const channels = [form.sendEmail && "Email", form.sendWhatsApp && "WhatsApp"]
        .filter(Boolean).join(" and ");
      toast.success(`Quotation sent by ${channels}.`);
      onSent();
    } catch (error) {
      // SS3.5: the failure is shown inside the popup with a Retry, not as a toast
      // that disappears before it can be read.
      setFailure(errorMessage(error, "The quotation could not be sent."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: `Send ${quotation.quotationNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, disabled: busy, children: "Cancel" }),
        e.jsx(Button, {
          loading: busy,
          disabled: !form.sendEmail && !form.sendWhatsApp,
          onClick: submit,
          children: failure ? "Retry" : "Send",
        }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        failure && e.jsx("div", { className: "form-error", role: "alert", children: failure }),
        e.jsx("p", {
          className: "field__hint",
          children: "Sending publishes the quotation to the customer portal and freezes the PDF.",
        }),
        e.jsxs("div", {
          className: "row-actions",
          children: [
            e.jsx(Checkbox, {
              label: "Email", checked: form.sendEmail,
              onChange: (ev) => patch({ sendEmail: ev.target.checked }),
            }),
            e.jsx(Checkbox, {
              label: "WhatsApp", checked: form.sendWhatsApp,
              onChange: (ev) => patch({ sendWhatsApp: ev.target.checked }),
            }),
          ],
        }),

        form.sendEmail && e.jsxs("div", {
          className: "form-stack",
          children: [
            e.jsx(Input, {
              label: "To", required: true, type: "email", value: form.emailTo,
              onChange: (ev) => patch({ emailTo: ev.target.value }),
            }),
            e.jsx(Input, {
              label: "Cc", type: "email", value: form.emailCc,
              onChange: (ev) => patch({ emailCc: ev.target.value }),
            }),
            e.jsx(Checkbox, {
              label: "Bcc me", checked: form.bccMe,
              onChange: (ev) => patch({ bccMe: ev.target.checked }),
            }),
            e.jsx(Input, {
              label: "Subject", value: form.emailSubject,
              hint: "Leave blank to use the template from Settings",
              onChange: (ev) => patch({ emailSubject: ev.target.value }),
            }),
            e.jsxs("div", {
              className: "field",
              children: [
                e.jsx("label", { className: "field__label", children: "Message" }),
                e.jsx("textarea", {
                  className: "field__input", rows: 4, value: form.emailMessage,
                  onChange: (ev) => patch({ emailMessage: ev.target.value }),
                }),
                e.jsx("span", {
                  className: "field__hint",
                  children: "{customer_name} {quote_number} {total} {valid_until} {portal_link} are filled in.",
                }),
              ],
            }),
          ],
        }),

        form.sendWhatsApp && e.jsxs("div", {
          className: "form-stack",
          children: [
            e.jsx(Input, {
              label: "WhatsApp number", required: true, value: form.whatsAppTo,
              hint: "+91 format",
              onChange: (ev) => patch({ whatsAppTo: ev.target.value }),
            }),
            e.jsxs("div", {
              className: "field",
              children: [
                e.jsx("label", { className: "field__label", children: "Message" }),
                e.jsx("textarea", {
                  className: "field__input", rows: 3, value: form.whatsAppMessage,
                  onChange: (ev) => patch({ whatsAppMessage: ev.target.value }),
                }),
                e.jsx("span", {
                  className: "field__hint",
                  children: "Sent through the WhatsApp Business template, or as a wa.me link when no provider is configured.",
                }),
              ],
            }),
          ],
        }),
      ],
    }),
  });
}

/* ── Record a decision on the customer's behalf (SS3.6) ─────────────────── */

function RecordDecisionModal({ quotation, approved, onClose, onSaved }) {
  const toast = useToast();
  const [busy, setBusy] = React.useState(false);
  const [confirmedBy, setConfirmedBy] = React.useState(quotation.customerName ?? "");
  const [mode, setMode] = React.useState(String(DECISION_MODE.Phone));
  const [note, setNote] = React.useState("");
  const [reasonCode, setReasonCode] = React.useState("");

  const submit = async () => {
    setBusy(true);
    try {
      const saved = await quotationsApi.recordDecision(quotation.quotationId, {
        approved, mode: Number(mode), confirmedBy: confirmedBy.trim(),
        note: note || null, reasonCode: reasonCode || null,
      });
      toast.success(approved ? "Recorded as approved." : "Recorded as rejected.");
      onSaved(saved);
    } catch (error) {
      toast.error(errorMessage(error, "Could not record the decision."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: approved ? "Mark approved on behalf" : "Mark rejected on behalf",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, disabled: busy, children: "Cancel" }),
        e.jsx(Button, {
          loading: busy, disabled: confirmedBy.trim().length === 0, onClick: submit,
          children: approved ? "Record approval" : "Record rejection",
        }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx("p", {
          children: "For a decision the customer gave by phone or in person. The record shows it was taken on their behalf.",
        }),
        e.jsx(Input, {
          label: "Confirmed by", required: true, value: confirmedBy,
          onChange: (ev) => setConfirmedBy(ev.target.value),
        }),
        e.jsx(Select, {
          label: "Mode", value: mode,
          options: [
            { value: "2", label: "Phone" }, { value: "3", label: "In person" },
            { value: "4", label: "Email" }, { value: "5", label: "WhatsApp" },
          ],
          onChange: (ev) => setMode(ev.target.value),
        }),
        !approved && e.jsx(Select, {
          label: "Reason", value: reasonCode, placeholder: "Not given",
          options: REJECT_REASONS,
          onChange: (ev) => setReasonCode(ev.target.value),
        }),
        e.jsx(Input, {
          label: "Note", value: note, onChange: (ev) => setNote(ev.target.value),
        }),
      ],
    }),
  });
}

function CancelQuotationModal({ quotation, onClose, onCancelled }) {
  const toast = useToast();
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      const saved = await quotationsApi.cancel(quotation.quotationId, reason.trim());
      toast.success("Quotation cancelled.");
      onCancelled(saved);
    } catch (error) {
      toast.error(errorMessage(error, "Could not cancel."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: `Cancel ${quotation.quotationNumber}?`,
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Keep it" }),
        e.jsx(Button, {
          variant: "danger", onClick: submit, loading: busy,
          disabled: reason.trim().length === 0, children: "Cancel quotation",
        }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx("p", { children: "The customer will no longer be able to act on it. The reason is recorded in the history." }),
        e.jsx(Input, {
          label: "Reason", required: true, value: reason,
          onChange: (ev) => setReason(ev.target.value),
        }),
      ],
    }),
  });
}

/* ── 3. Detail (SS3.6) ──────────────────────────────────────────────────── */

/** The banner under the toolbar, explaining the state in the document's words. */
function StateBanner({ quotation, onConvert, onRevise, busy }) {
  const status = effectiveStatus(quotation);
  if (status === QUOTATION_STATUS.Approved) {
    return e.jsxs("div", {
      className: "qtn-banner qtn-banner--approved",
      children: [
        e.jsxs("span", {
          children: [
            "Approved",
            quotation.respondedUtc ? ` on ${formatDateTime(quotation.respondedUtc)}` : "",
            quotation.decisionOnBehalf ? " (recorded on the customer's behalf)" : "",
            ".",
          ],
        }),
        quotation.actions.canConvertToInvoice && e.jsx(Button, {
          size: "sm", loading: busy, onClick: onConvert, children: "Convert to invoice",
        }),
        quotation.invoiceNumber && e.jsxs("span", {
          children: ["Invoiced as ", e.jsx("strong", { children: quotation.invoiceNumber }), "."],
        }),
      ],
    });
  }
  if (status === QUOTATION_STATUS.Rejected) {
    return e.jsxs("div", {
      className: "qtn-banner qtn-banner--rejected",
      children: [
        e.jsxs("span", {
          children: ["Rejected", quotation.rejectionReason ? ` — ${quotation.rejectionReason}` : "."],
        }),
        quotation.actions.canRevise && e.jsx(Button, {
          size: "sm", variant: "outline", loading: busy, onClick: onRevise, children: "Revise",
        }),
      ],
    });
  }
  if (status === QUOTATION_STATUS.Expired) {
    return e.jsxs("div", {
      className: "qtn-banner qtn-banner--expired",
      children: [
        e.jsxs("span", { children: [`Expired on ${formatDate(quotation.validUntil)} with no decision.`] }),
        quotation.actions.canRevise && e.jsx(Button, {
          size: "sm", variant: "outline", loading: busy, onClick: onRevise, children: "Revise",
        }),
      ],
    });
  }
  if (status === QUOTATION_STATUS.Revised) {
    return e.jsx("div", {
      className: "qtn-banner qtn-banner--revised",
      children: e.jsxs("span", {
        children: ["Replaced by ", e.jsx("strong", { children: quotation.replacedByQuotationNumber ?? "a newer revision" }), "."],
      }),
    });
  }
  if (status === QUOTATION_STATUS.Draft) {
    return e.jsx("div", {
      className: "qtn-banner qtn-banner--draft",
      children: e.jsx("span", { children: "This is a draft — the customer cannot see it yet." }),
    });
  }
  if (status === QUOTATION_STATUS.Cancelled) {
    return e.jsx("div", {
      className: "qtn-banner qtn-banner--rejected",
      children: e.jsx("span", { children: "This quotation was cancelled." }),
    });
  }
  return e.jsx("div", {
    className: "qtn-banner qtn-banner--sent",
    children: e.jsxs("span", {
      children: [
        status === QUOTATION_STATUS.Viewed
          ? "The customer has opened it and not yet decided."
          : "Waiting for the customer to open it.",
        ` Valid until ${formatDate(quotation.validUntil)}.`,
      ],
    }),
  });
}

function QuotationSummaryCard({ quotation }) {
  return e.jsxs(Card, {
    children: [
      e.jsxs("div", {
        className: "qtn-details",
        children: [
          e.jsx(Detail, { label: "Status", children: e.jsx(StatusPill, { quotation }) }),
          e.jsx(Detail, {
            label: "Customer",
            children: quotation.customerId
              ? quotation.customerName
              : e.jsxs(e.Fragment, {
                children: [
                  quotation.customerName,
                  e.jsx("div", { className: "qtn-cell__muted", children: "No customer account" }),
                ],
              }),
          }),
          e.jsx(Detail, { label: "Company", children: quotation.customerCompanyName }),
          e.jsx(Detail, { label: "GSTIN", children: quotation.customerGstin }),
          e.jsx(Detail, {
            label: "Address",
            children: quotation.billingAddress
              ? e.jsx("span", { style: { whiteSpace: "pre-line" }, children: quotation.billingAddress })
              : null,
          }),
          e.jsx(Detail, { label: "Reference", children: quotation.referenceNumber }),
          e.jsx(Detail, { label: "Quote date", children: formatDate(quotation.quotationDate) }),
          e.jsx(Detail, { label: "Valid until", children: e.jsx(ValidUntil, { quotation }) }),
          e.jsx(Detail, { label: "Place of supply", children: quotation.placeOfSupply }),
          e.jsx(Detail, { label: "Salesperson", children: quotation.salespersonName }),
          e.jsx(Detail, { label: "Sent", children: quotation.sentUtc ? formatDateTime(quotation.sentUtc) : null }),
          e.jsx(Detail, { label: "Viewed", children: quotation.viewedUtc ? formatDateTime(quotation.viewedUtc) : null }),
          e.jsx(Detail, { label: "Decision", children: quotation.respondedUtc ? formatDateTime(quotation.respondedUtc) : null }),
          e.jsx(Detail, { label: "Invoice", children: quotation.invoiceNumber }),
        ],
      }),
      quotation.subject && e.jsxs("p", {
        children: [e.jsx("strong", { children: "Subject: " }), quotation.subject],
      }),
      quotation.revisionOfQuotationNumber && e.jsxs("p", {
        className: "field__hint",
        children: ["Revision ", quotation.revisionNumber, " of ", quotation.revisionOfQuotationNumber, "."],
      }),
    ],
  });
}

function QuotationLinesCard({ quotation }) {
  const intra = quotation.taxMode === 1;
  return e.jsxs(Card, {
    title: "Items",
    children: [
      e.jsx("div", {
        className: "table-scroll",
        children: e.jsxs("table", {
          className: "data-table",
          children: [
            e.jsx("thead", {
              children: e.jsx("tr", {
                children: ["#", "Item", "Qty", "Duration", "Rate", "Tax", "Amount"].map(
                  (h) => e.jsx("th", { children: h }, h),
                ),
              }),
            }),
            e.jsx("tbody", {
              children: quotation.lines.map((line) => (line.lineType === LINE_TYPE.Header
                ? e.jsx("tr", {
                  children: e.jsx("td", {
                    colSpan: 7,
                    className: "qtn-row-header",
                    children: line.description,
                  }),
                }, line.quotationLineId)
                : e.jsxs("tr", {
                  children: [
                    e.jsx("td", { children: line.lineNumber }),
                    e.jsxs("td", {
                      children: [
                        e.jsx("div", { children: line.description }),
                        /* The §2.1 line text, as printed on the PDF. */
                        line.summary && e.jsx("div", { className: "qtn-cell__muted", children: line.summary }),
                        line.lineNotes && e.jsx("div", {
                          className: "qtn-cell__muted", style: { whiteSpace: "pre-line" }, children: line.lineNotes,
                        }),
                        e.jsx("div", {
                          className: "qtn-cell__muted",
                          children: [
                            line.itemType ? (line.itemType === 2 ? "Service" : "Goods")
                              : line.lineType === LINE_TYPE.Custom ? "Custom line" : SERVICE_TYPE_LABEL[line.serviceType],
                            /* Goods carry an HSN code; services (and older lines) a SAC code. */
                            line.hsnSacCode ? `${line.itemType === 1 ? "HSN" : "SAC"} ${line.hsnSacCode}` : null,
                          ].filter(Boolean).join(" · "),
                        }),
                      ],
                    }),
                    e.jsx("td", { children: line.unit ? `${line.quantity} ${line.unit}` : line.quantity }),
                    e.jsx("td", {
                      children: line.durationValue
                        ? `${line.durationValue} ${line.durationUnit ?? ""}`.trim() : "—",
                    }),
                    e.jsxs("td", {
                      children: [
                        formatInr(line.unitPrice),
                        line.rateEdited && e.jsx("div", {
                          className: "qtn-cell__muted",
                          children: `edited · configured ${formatInr(line.standardRate)}`,
                        }),
                      ],
                    }),
                    e.jsx("td", { children: line.isNonTaxable ? "Non-Taxable" : `${line.taxPercent}%` }),
                    e.jsx("td", { children: e.jsx("strong", { children: formatInr(line.lineAmount) }) }),
                  ],
                }, line.quotationLineId))),
            }),
          ],
        }),
      }),
      e.jsxs("div", {
        className: "qtn-totals qtn-totals--read",
        children: [
          e.jsxs("div", { className: "qtn-totals__row", children: [e.jsx("span", { children: "Sub total" }), e.jsx("span", { children: formatInr(quotation.subTotal) })] }),
          quotation.discountAmount > 0 && e.jsxs("div", {
            className: "qtn-totals__row",
            children: [
              e.jsx("span", {
                /* Only name the rate when there is one: a quotation whose discount
                   came from its lines has no whole-document percentage to show. */
                children: quotation.discountType === DISCOUNT_TYPE.Percentage
                  && quotation.discountValue > 0
                  ? `Discount (${quotation.discountValue}%)` : "Discount",
              }),
              e.jsx("span", { children: `− ${formatInr(quotation.discountAmount)}` }),
            ],
          }),
          e.jsxs("div", { className: "qtn-totals__row", children: [e.jsx("span", { children: "Taxable" }), e.jsx("span", { children: formatInr(quotation.taxableAmount) })] }),
          intra
            ? e.jsxs(e.Fragment, {
              children: [
                e.jsxs("div", { className: "qtn-totals__row", children: [e.jsx("span", { children: `CGST @ ${quotation.cgstPercent}%` }), e.jsx("span", { children: formatInr(quotation.cgstAmount) })] }),
                e.jsxs("div", { className: "qtn-totals__row", children: [e.jsx("span", { children: `SGST @ ${quotation.sgstPercent}%` }), e.jsx("span", { children: formatInr(quotation.sgstAmount) })] }),
              ],
            })
            : e.jsxs("div", { className: "qtn-totals__row", children: [e.jsx("span", { children: `IGST @ ${quotation.igstPercent}%` }), e.jsx("span", { children: formatInr(quotation.igstAmount) })] }),
          quotation.tdsAmount > 0 && e.jsxs("div", {
            className: "qtn-totals__row",
            children: [e.jsx("span", { children: quotation.tdsLabel ?? "TDS" }), e.jsx("span", { children: `− ${formatInr(quotation.tdsAmount)}` })],
          }),
          quotation.adjustmentAmount !== 0 && e.jsxs("div", {
            className: "qtn-totals__row",
            children: [e.jsx("span", { children: quotation.adjustmentLabel ?? "Adjustment" }), e.jsx("span", { children: formatInr(quotation.adjustmentAmount) })],
          }),
          quotation.roundOffAmount !== 0 && e.jsxs("div", {
            className: "qtn-totals__row",
            children: [e.jsx("span", { children: "Round off" }), e.jsx("span", { children: formatInr(quotation.roundOffAmount) })],
          }),
          e.jsxs("div", {
            className: "qtn-totals__row qtn-totals__row--grand",
            children: [e.jsx("span", { children: "Total" }), e.jsx("span", { children: formatInr(quotation.totalAmount) })],
          }),
          e.jsx("p", { className: "field__hint", children: quotation.totalInWords }),
        ],
      }),
      quotation.remarks && e.jsxs("div", {
        children: [
          e.jsx("div", { className: "qtn-detail__label", children: "Notes" }),
          e.jsx("p", { style: { whiteSpace: "pre-wrap" }, children: quotation.remarks }),
        ],
      }),
      quotation.termsAndConditions && e.jsxs("div", {
        children: [
          e.jsx("div", { className: "qtn-detail__label", children: "Terms & conditions" }),
          e.jsx("p", { style: { whiteSpace: "pre-wrap" }, children: quotation.termsAndConditions }),
        ],
      }),
    ],
  });
}

/** The activity timeline (SS3.6): every event, who caused it and, for a send,
 *  which channel it went out on and whether it landed. */
function QuotationTimelineCard({ quotation }) {
  const entries = quotation.events.length > 0
    ? quotation.events.map((ev) => ({
      key: `e${ev.quotationEventId}`,
      when: ev.createdUtc,
      who: ev.actorName || "System",
      text: ev.summary,
      channel: ev.channel,
      failed: ev.deliveryStatus === 5,
    }))
    : quotation.statusHistory.map((h, index) => ({
      key: `h${index}`,
      when: h.changedUtc,
      who: h.changedByName || "System",
      text: `${STATUS_LABEL[h.toStatus] ?? h.toStatus}${h.reason ? ` — ${h.reason}` : ""}`,
    }));

  return e.jsx(Card, {
    title: "Activity",
    children: entries.length === 0
      ? e.jsx("p", { className: "field__hint", children: "Nothing has happened yet." })
      : e.jsx("ol", {
        className: "qtn-timeline",
        children: entries.map((entry) => e.jsxs("li", {
          className: `qtn-timeline__item${entry.failed ? " qtn-timeline__item--failed" : ""}`,
          children: [
            e.jsx("div", { className: "qtn-timeline__text", children: entry.text }),
            e.jsxs("div", {
              className: "qtn-timeline__meta",
              children: [
                formatDateTime(entry.when), " · ", entry.who,
                entry.channel ? ` · ${entry.channel}` : "",
              ],
            }),
          ],
        }, entry.key)),
      }),
  });
}

function AdminQuotationDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const navigate = useNavigate();

  const [quotation, setQuotation] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [busy, setBusy] = React.useState(false);
  const [sending, setSending] = React.useState(false);
  const [cancelling, setCancelling] = React.useState(false);
  const [deciding, setDeciding] = React.useState(null);

  const load = React.useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setQuotation(await quotationsApi.get(Number(id)));
    } catch (error) {
      toast.error(errorMessage(error, "Failed to load the quotation."));
    } finally {
      setLoading(false);
    }
  }, [id]);

  React.useEffect(() => { void load(); }, [load]);

  if (loading) return e.jsx(Spinner, { center: true });
  if (!quotation) return null;

  const run = async (label, action) => {
    setBusy(true);
    try {
      await action();
    } catch (error) {
      toast.error(errorMessage(error, `Could not ${label}.`));
    } finally {
      setBusy(false);
    }
  };

  const actions = quotation.actions;

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: quotation.quotationNumber,
        description: `${quotation.customerName || "Customer"} · ${formatDate(quotation.quotationDate)} · ${formatInr(quotation.totalAmount)}`,
        actions: e.jsxs(e.Fragment, {
          children: [
            e.jsx(Button, {
              variant: "ghost",
              onClick: () => navigate("/admin/billing/quotations"),
              children: "Back",
            }),
            actions.canEdit && e.jsx(Button, {
              variant: "outline",
              onClick: () => navigate(`/admin/billing/quotations/${quotation.quotationId}/edit`),
              children: "Edit",
            }),
            actions.canDownloadPdf && e.jsx(Button, {
              variant: "outline",
              onClick: () => void quotationsApi.pdf(quotation.quotationId, quotation.quotationNumber),
              children: "Download PDF",
            }),
            actions.canCopyPortalLink && e.jsx(Button, {
              variant: "outline",
              onClick: () => {
                const link = `${window.location.origin}/quotations/${quotation.quotationId}`;
                void navigator.clipboard?.writeText(link);
                toast.success("Portal link copied.");
              },
              children: "Copy portal link",
            }),
            actions.canSend && e.jsx(Button, {
              onClick: () => setSending(true),
              children: quotation.status === QUOTATION_STATUS.Draft ? "Send" : "Resend",
            }),
            actions.canRecordDecision && e.jsxs(e.Fragment, {
              children: [
                e.jsx(Button, {
                  variant: "outline",
                  onClick: () => setDeciding({ approved: true }),
                  children: "Mark approved",
                }),
                e.jsx(Button, {
                  variant: "outline",
                  onClick: () => setDeciding({ approved: false }),
                  children: "Mark rejected",
                }),
              ],
            }),
            actions.canClone && e.jsx(Button, {
              variant: "ghost",
              onClick: () => void run("clone the quotation", async () => {
                const clone = await quotationsApi.clone(quotation.quotationId);
                toast.success(`Cloned as ${clone.quotationNumber}.`);
                navigate(`/admin/billing/quotations/${clone.quotationId}/edit`);
              }),
              children: "Clone",
            }),
            actions.canCancel && e.jsx(Button, {
              variant: "ghost", onClick: () => setCancelling(true), children: "Cancel",
            }),
          ],
        }),
      }),

      e.jsx(StateBanner, {
        quotation,
        busy,
        onConvert: () => void run("raise the invoice", async () => {
          const invoice = await invoiceApi.fromQuotation(quotation.quotationId);
          toast.success(`Invoice ${invoice.invoiceNumber} raised.`);
          await load();
        }),
        onRevise: () => void run("revise the quotation", async () => {
          const revision = await quotationsApi.revise(quotation.quotationId);
          toast.success(`Revision ${revision.quotationNumber} created.`);
          navigate(`/admin/billing/quotations/${revision.quotationId}/edit`);
        }),
      }),

      e.jsxs("div", {
        className: "qtn-detail-layout",
        children: [
          e.jsxs("div", {
            children: [
              e.jsx(QuotationSummaryCard, { quotation }),
              e.jsx(QuotationLinesCard, { quotation }),
              quotation.internalNote && e.jsxs(Card, {
                title: "Internal note",
                children: [
                  e.jsx("p", { style: { whiteSpace: "pre-wrap" }, children: quotation.internalNote }),
                  e.jsx("span", {
                    className: "field__hint",
                    children: "Admin only — never shown to the customer and never printed.",
                  }),
                ],
              }),
              quotation.attachments.length > 0 && e.jsx(Card, {
                title: "Attachments",
                children: e.jsx("div", {
                  className: "qtn-chips",
                  children: quotation.attachments.map((file) => e.jsx("a", {
                    className: "qtn-chip",
                    href: `/api/quotations/${quotation.quotationId}/attachments/${file.quotationAttachmentId}`,
                    children: file.fileName,
                  }, file.quotationAttachmentId)),
                }),
              }),
            ],
          }),
          e.jsx("div", { children: e.jsx(QuotationTimelineCard, { quotation }) }),
        ],
      }),

      sending && e.jsx(SendQuotationModal, {
        quotation,
        onClose: () => setSending(false),
        onSent: () => { setSending(false); void load(); },
      }),

      cancelling && e.jsx(CancelQuotationModal, {
        quotation,
        onClose: () => setCancelling(false),
        onCancelled: (saved) => { setCancelling(false); setQuotation(saved); },
      }),

      deciding && e.jsx(RecordDecisionModal, {
        quotation,
        approved: deciding.approved,
        onClose: () => setDeciding(null),
        onSaved: (saved) => { setDeciding(null); setQuotation(saved); void load(); },
      }),
    ],
  });
}

/* ── Exports ────────────────────────────────────────────────────────────── */

const moduleNamespace = Object.freeze(Object.defineProperty({
  __proto__: null,
  AdminQuotationDetailPage,
  AdminQuotationFormPage,
  AdminQuotationsPage,
  Detail,
  QuotationLinesCard,
  QuotationSummaryCard,
}, Symbol.toStringTag, { value: "Module" }));

export {
  moduleNamespace as A,
  QUOTATION_STATUS as Q,
  QuotationSummaryCard as a,
  QuotationLinesCard as b,
  STATUS_LABEL as c,
  CUSTOMER_STATUS_LABEL as d,
  quotationsApi as q,
  StatusPill as s,
  REJECT_REASONS as r,
  effectiveStatus as f,
  Detail as D,
};
