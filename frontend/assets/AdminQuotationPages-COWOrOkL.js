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

const DATE_PRESETS = [
  { value: "today", label: "Today" },
  { value: "week", label: "This week" },
  { value: "month", label: "This month" },
  { value: "fy", label: "This financial year" },
];

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

/** "in 8 days" under the valid-until date, amber once it is 3 days or less. */
function ValidUntil({ quotation }) {
  const days = quotation.daysToExpiry;
  const open = effectiveStatus(quotation) === QUOTATION_STATUS.Sent
    || effectiveStatus(quotation) === QUOTATION_STATUS.Viewed;
  let hint = null;
  if (quotation.isExpired) hint = "expired";
  else if (open && days >= 0) hint = days === 0 ? "today" : `in ${days} day${days === 1 ? "" : "s"}`;
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
        children: visible.map((item) => e.jsx("button", {
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

function AdminQuotationsPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const canCreate = usePermission("QUOTATION.CREATE");
  const canExport = usePermission("QUOTATION.EXPORT");

  /* The active tab lives in the URL so a filtered list can be linked and the
     back button behaves (SS3.1). */
  const initialStatus = new URLSearchParams(window.location.search).get("status") ?? "";
  const [status, setStatus] = React.useState(
    STATUS_TABS.some((t) => t.value === initialStatus) ? initialStatus : "",
  );
  const [result, setResult] = React.useState(null);
  const [summary, setSummary] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [searchTerm, setSearchTerm] = React.useState("");
  const [customerId, setCustomerId] = React.useState("");
  const [serviceType, setServiceType] = React.useState("");
  const [datePreset, setDatePreset] = React.useState("");
  const [expiringInDays, setExpiringInDays] = React.useState("");
  const [customers, setCustomers] = React.useState([]);
  const [busy, setBusy] = React.useState(false);
  const [sendFor, setSendFor] = React.useState(null);
  const [confirming, setConfirming] = React.useState(null);

  const query = React.useMemo(() => ({
    page,
    pageSize: 20,
    status: status ? Number(status) : undefined,
    search: searchTerm || undefined,
    customerId: customerId ? Number(customerId) : undefined,
    serviceType: serviceType ? Number(serviceType) : undefined,
    datePreset: datePreset || undefined,
    expiringInDays: expiringInDays ? Number(expiringInDays) : undefined,
  }), [page, status, searchTerm, customerId, serviceType, datePreset, expiringInDays]);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setResult(await quotationsApi.list(query));
    } catch (error) {
      toast.error(errorMessage(error, "Failed to load quotations."));
    } finally {
      setLoading(false);
    }
  }, [query]);

  const loadSummary = React.useCallback(async () => {
    try {
      setSummary(await quotationsApi.summary());
    } catch {
      // The cards are a convenience; the table below is the real answer.
      setSummary(null);
    }
  }, []);

  React.useEffect(() => { void load(); }, [load]);
  React.useEffect(() => { void loadSummary(); }, [loadSummary]);

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
    const params = new URLSearchParams(window.location.search);
    if (value) params.set("status", value); else params.delete("status");
    const search2 = params.toString();
    window.history.replaceState(
      null, "", window.location.pathname + (search2 ? `?${search2}` : ""),
    );
  };

  const refresh = async () => { await load(); await loadSummary(); };

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

  const columns = [
    {
      key: "number",
      header: "Quotation",
      render: (q) => e.jsxs("div", {
        children: [
          e.jsx("div", { className: "qtn-cell__strong", children: q.quotationNumber }),
          e.jsx("div", { className: "qtn-cell__muted", children: formatDate(q.quotationDate) }),
        ],
      }),
    },
    {
      key: "customer",
      header: "Customer",
      render: (q) => e.jsxs("div", {
        children: [
          e.jsx("div", { children: q.customerName || "—" }),
          q.customerCompanyName && e.jsx("div", {
            className: "qtn-cell__muted", children: q.customerCompanyName,
          }),
        ],
      }),
    },
    { key: "lines", header: "Lines", render: (q) => q.lines.length },
    {
      key: "total",
      header: "Total",
      render: (q) => e.jsx("strong", { children: formatInr(q.totalAmount) }),
    },
    { key: "valid", header: "Valid until", render: (q) => e.jsx(ValidUntil, { quotation: q }) },
    { key: "status", header: "Status", render: (q) => e.jsx(StatusPill, { quotation: q }) },
    {
      key: "invoice",
      header: "Invoice",
      render: (q) => q.invoiceNumber
        ? e.jsx("button", {
          type: "button",
          className: "qtn-link",
          onClick: (ev) => { ev.stopPropagation(); navigate("/admin/billing/invoices"); },
          children: q.invoiceNumber,
        })
        : "—",
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
            ],
          }),
        ],
      }),
    },
  ];

  const tabCount = (value) => {
    if (!summary) return null;
    if (!value) return summary.totalCount;
    return summary.statusCounts?.[value] ?? 0;
  };

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Quotations",
        description: "Raise a priced offer for a customer. Once sent it is frozen, and the customer approves or rejects it in their portal.",
        actions: e.jsxs(e.Fragment, {
          children: [
            canExport && e.jsx(Button, {
              variant: "outline",
              onClick: () => void quotationsApi.exportCsv(query).catch(
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
                label: "Date",
                value: datePreset,
                placeholder: "Any date",
                options: DATE_PRESETS,
                onChange: (ev) => { setPage(1); setDatePreset(ev.target.value); },
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

          e.jsx(DataTable, {
            columns,
            result,
            loading: loading || busy,
            rowKey: (q) => q.quotationId,
            onRowClick: openQuotation,
            onPageChange: setPage,
            emptyTitle: "No quotations yet",
            emptyDescription: "Create your first quotation for a cabin, desk or meeting room.",
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

const emptyLine = () => ({
  key: Math.random().toString(36).slice(2),
  lineType: LINE_TYPE.Service,
  serviceType: String(SERVICE_TYPE.Other),
  workspaceId: "",
  passPlanId: "",
  workspaceTypeId: "",
  serviceKey: "",
  description: "",
  lineNotes: "",
  hsnSacCode: "",
  quantity: "1",
  durationValue: "1",
  durationUnit: "Month",
  unitPrice: "0",
  standardRate: null,
  discountAmount: "0",
  taxPercent: "18",
});

const serviceKeyOf = (option) =>
  `${option.serviceType}:${option.workspaceId ?? ""}:${option.passPlanId ?? ""}:${option.code}`;

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
    tax += round2((lineTaxable * (Number(paying[index].taxPercent) || 0)) / 100);
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
  const [locations, setLocations] = React.useState([]);
  const [salespeople, setSalespeople] = React.useState([]);
  const [dirty, setDirty] = React.useState(false);
  const [discardOpen, setDiscardOpen] = React.useState(false);
  const [sendAfterSave, setSendAfterSave] = React.useState(null);

  const [header, setHeader] = React.useState({
    customerId: "", locationId: "", quotationDate: new Date().toISOString().slice(0, 10),
    validUntil: "", referenceNumber: "", subject: "", salespersonId: "",
    placeOfSupplyStateCode: "", discountType: String(DISCOUNT_TYPE.Percentage),
    discountValue: "0", tdsCode: "", adjustmentLabel: "Adjustment", adjustmentAmount: "0",
    advanceInvoiceEnabled: false, advanceType: "1", advanceValue: "1",
    termsAndConditions: "", remarks: "", internalNote: "",
  });
  const [lines, setLines] = React.useState([emptyLine()]);

  const patchHeader = (patch) => { setDirty(true); setHeader((h) => ({ ...h, ...patch })); };
  const patchLine = (index, patch) => {
    setDirty(true);
    setLines((current) => current.map((l, i) => (i === index ? { ...l, ...patch } : l)));
  };

  React.useEffect(() => {
    void (async () => {
      setLoading(true);
      try {
        const [formOptions, customerPage, locationPage] = await Promise.all([
          quotationsApi.formOptions(),
          customersApi.list({ page: 1, pageSize: 200 }),
          locationsApi.list({ page: 1, pageSize: 100 }),
        ]);
        setOptions(formOptions);
        setCustomers(customerPage.items.map((c) => ({
          value: c.userId,
          label: [c.fullName, c.companyName, c.mobileNumber].filter(Boolean).join(" · "),
        })));
        setLocations(locationPage.items.map((l) => ({
          value: l.locationId, label: l.locationName,
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
            customerId: String(quotation.customerId),
            locationId: String(quotation.locationId),
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
          setLines(quotation.lines.map((l) => ({
            key: `l${l.quotationLineId}`,
            lineType: l.lineType,
            serviceType: String(l.serviceType),
            workspaceId: l.workspaceId ? String(l.workspaceId) : "",
            passPlanId: l.passPlanId ? String(l.passPlanId) : "",
            workspaceTypeId: l.workspaceTypeId ? String(l.workspaceTypeId) : "",
            serviceKey: "",
            description: l.description,
            lineNotes: l.lineNotes ?? "",
            hsnSacCode: l.hsnSacCode ?? "",
            quantity: String(l.quantity),
            durationValue: l.durationValue === null ? "" : String(l.durationValue),
            durationUnit: l.durationUnit ?? "",
            unitPrice: String(l.unitPrice),
            standardRate: l.standardRate,
            discountAmount: String(l.discountAmount),
            taxPercent: String(l.taxPercent),
          })));
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
            locationId: locationPage.items.length === 1
              ? String(locationPage.items[0].locationId) : "",
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

  const serviceOptions = React.useMemo(() => {
    if (!options) return [];
    const groups = new Map();
    for (const service of options.services) {
      if (!groups.has(service.group)) groups.set(service.group, []);
      groups.get(service.group).push(service);
    }
    return [...groups.entries()];
  }, [options]);

  const findService = (key) => options?.services.find((s) => serviceKeyOf(s) === key);

  /** Picking a service fills the rate, the unit and the quantity lock from the
   *  price already configured for it — nothing is hard-coded (SS2.1). */
  const applyService = (index, key) => {
    const service = findService(key);
    if (!service) {
      patchLine(index, { serviceKey: key });
      return;
    }
    const unit = service.defaultDurationUnit;
    const rate = unit === "Hour" ? service.ratePerHour
      : unit === "Day" ? service.ratePerDay
        : unit === "Month" ? service.ratePerMonth
          : service.rateOneTime;
    patchLine(index, {
      serviceKey: key,
      serviceType: String(service.serviceType),
      workspaceId: service.workspaceId ? String(service.workspaceId) : "",
      passPlanId: service.passPlanId ? String(service.passPlanId) : "",
      workspaceTypeId: service.workspaceTypeId ? String(service.workspaceTypeId) : "",
      description: service.name,
      durationUnit: unit,
      durationValue: unit === "One-time" ? "" : "1",
      unitPrice: rate === null || rate === undefined ? "0" : String(rate),
      standardRate: rate ?? null,
      quantity: service.quantityLocked ? "1" : "1",
    });
  };

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

  const buildPayload = () => ({
    customerId: Number(header.customerId),
    locationId: Number(header.locationId),
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
    lines: lines.map((l) => ({
      lineType: Number(l.lineType),
      serviceType: Number(l.serviceType),
      workspaceTypeId: l.workspaceTypeId ? Number(l.workspaceTypeId) : null,
      serviceItemId: null,
      workspaceId: l.workspaceId ? Number(l.workspaceId) : null,
      passPlanId: l.passPlanId ? Number(l.passPlanId) : null,
      description: l.description.trim(),
      lineNotes: l.lineNotes || null,
      hsnSacCode: l.hsnSacCode || null,
      quantity: Number(l.quantity || 0),
      durationValue: l.durationValue ? Number(l.durationValue) : null,
      durationUnit: l.durationUnit || null,
      unitPrice: Number(l.unitPrice || 0),
      standardRate: l.standardRate,
      discountAmount: Number(l.discountAmount || 0),
      taxPercent: Number(l.taxPercent || 0),
    })),
  });

  const save = async (mode) => {
    setSaving(mode);
    clear();
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

  const gstOptions = options.gstRateOptions.map((rate) => ({
    value: String(rate), label: `${rate}%`,
  }));
  const isRevision = existing && existing.revisionNumber > 0;
  const title = existing
    ? (isRevision ? `Revise — ${existing.quotationNumber}` : `Edit — ${existing.quotationNumber}`)
    : "New quotation";

  const addLine = (patch = {}) => {
    setDirty(true);
    setLines((current) => [...current, {
      ...emptyLine(), taxPercent: String(options.defaultGstPercent), ...patch,
    }]);
  };

  const removeLine = (index) => {
    setDirty(true);
    setLines((current) => (current.length === 1
      ? [{ ...emptyLine(), taxPercent: String(options.defaultGstPercent) }]
      : current.filter((_, i) => i !== index)));
  };

  const moveLine = (index, delta) => {
    const target = index + delta;
    if (target < 0 || target >= lines.length) return;
    setDirty(true);
    setLines((current) => {
      const next = [...current];
      [next[index], next[target]] = [next[target], next[index]];
      return next;
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
            e.jsx(Select, {
              label: "Customer",
              required: true,
              value: header.customerId,
              placeholder: "Search by name, company or mobile",
              options: customers,
              error: fieldErrors.customerId,
              onChange: (ev) => { clearField("customerId"); patchHeader({ customerId: ev.target.value }); },
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
            e.jsx(Select, {
              label: "Location",
              required: true,
              value: header.locationId,
              placeholder: "Select a location",
              options: locations,
              error: fieldErrors.locationId,
              onChange: (ev) => { clearField("locationId"); patchHeader({ locationId: ev.target.value }); },
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

      /* Item table */
      e.jsxs(Card, {
        title: "Services",
        actions: e.jsxs("div", {
          className: "row-actions",
          children: [
            e.jsx(Button, { size: "sm", variant: "outline", onClick: () => addLine(), children: "Add row" }),
            e.jsx(Button, {
              size: "sm", variant: "ghost",
              onClick: () => addLine({ lineType: LINE_TYPE.Header, description: "Section" }),
              children: "Add header",
            }),
          ],
        }),
        children: [
          e.jsx("div", {
            className: "qtn-lines",
            children: lines.map((line, index) => {
              const isHeader = Number(line.lineType) === LINE_TYPE.Header;
              const service = findService(line.serviceKey);
              const rateChanged = line.standardRate !== null
                && line.standardRate !== undefined
                && Number(line.unitPrice) !== Number(line.standardRate);
              const amount = isHeader ? 0 : round2(
                (Number(line.quantity) || 0)
                * (Number(line.durationValue) || 1)
                * (Number(line.unitPrice) || 0)
                - (Number(line.discountAmount) || 0),
              );

              return e.jsxs("div", {
                className: `qtn-line${isHeader ? " qtn-line--header" : ""}`,
                children: [
                  e.jsxs("div", {
                    className: "qtn-line__bar",
                    children: [
                      e.jsx("span", { className: "qtn-line__no", children: index + 1 }),
                      e.jsxs("div", {
                        className: "row-actions",
                        children: [
                          e.jsx("button", {
                            type: "button", className: "qtn-iconbtn", "aria-label": "Move up",
                            onClick: () => moveLine(index, -1), children: "↑",
                          }),
                          e.jsx("button", {
                            type: "button", className: "qtn-iconbtn", "aria-label": "Move down",
                            onClick: () => moveLine(index, 1), children: "↓",
                          }),
                          e.jsx("button", {
                            type: "button", className: "qtn-iconbtn", "aria-label": "Duplicate row",
                            onClick: () => {
                              setDirty(true);
                              setLines((current) => [
                                ...current.slice(0, index + 1),
                                { ...current[index], key: Math.random().toString(36).slice(2) },
                                ...current.slice(index + 1),
                              ]);
                            },
                            children: "⧉",
                          }),
                          e.jsx("button", {
                            type: "button", className: "qtn-iconbtn qtn-iconbtn--danger",
                            "aria-label": "Remove row",
                            onClick: () => removeLine(index), children: "✕",
                          }),
                        ],
                      }),
                    ],
                  }),

                  isHeader
                    ? e.jsx(Input, {
                      label: "Section heading",
                      value: line.description,
                      onChange: (ev) => patchLine(index, { description: ev.target.value }),
                    })
                    : e.jsxs("div", {
                      className: "qtn-line__grid",
                      children: [
                        e.jsxs("div", {
                          className: "field qtn-line__service",
                          children: [
                            e.jsx("label", { className: "field__label", children: "Service" }),
                            e.jsxs("select", {
                              className: "field__input",
                              value: line.serviceKey,
                              onChange: (ev) => applyService(index, ev.target.value),
                              children: [
                                e.jsx("option", { value: "", children: "Custom line" }),
                                ...serviceOptions.map(([group, items]) => e.jsx("optgroup", {
                                  label: group,
                                  children: items.map((option) => e.jsx("option", {
                                    value: serviceKeyOf(option),
                                    children: option.capacity
                                      ? `${option.name} · ${option.capacity} seat${option.capacity === 1 ? "" : "s"}`
                                      : option.name,
                                  }, serviceKeyOf(option))),
                                }, group)),
                              ],
                            }),
                          ],
                        }),
                        e.jsx(Input, {
                          label: "Description",
                          required: true,
                          value: line.description,
                          error: fieldErrors[`lines[${index}].description`],
                          onChange: (ev) => patchLine(index, { description: ev.target.value }),
                        }),
                        e.jsx(Input, {
                          label: "Qty",
                          type: "number", min: "0", step: "0.001",
                          readOnly: Boolean(service && service.quantityLocked),
                          hint: service && service.quantityLocked
                            ? "One per line — add another line for a second one" : undefined,
                          value: line.quantity,
                          error: fieldErrors[`lines[${index}].quantity`],
                          onChange: (ev) => patchLine(index, { quantity: ev.target.value }),
                        }),
                        e.jsx(Input, {
                          label: "Duration",
                          type: "number", min: "0", step: "0.001",
                          value: line.durationValue,
                          error: fieldErrors[`lines[${index}].durationValue`],
                          onChange: (ev) => patchLine(index, { durationValue: ev.target.value }),
                        }),
                        e.jsx(Select, {
                          label: "Unit",
                          value: line.durationUnit,
                          placeholder: "—",
                          options: DURATION_UNITS.map((u) => ({ value: u, label: u })),
                          error: fieldErrors[`lines[${index}].durationUnit`],
                          onChange: (ev) => patchLine(index, { durationUnit: ev.target.value }),
                        }),
                        e.jsx(Input, {
                          label: "Rate",
                          type: "number", min: "0", step: "0.01",
                          value: line.unitPrice,
                          hint: rateChanged
                            ? `Changed from ${formatInr(line.standardRate)} (standard price)`
                            : undefined,
                          error: fieldErrors[`lines[${index}].unitPrice`],
                          onChange: (ev) => patchLine(index, { unitPrice: ev.target.value }),
                        }),
                        e.jsx(Select, {
                          label: "GST",
                          value: line.taxPercent,
                          options: gstOptions,
                          hint: intraState ? "CGST + SGST" : "IGST",
                          onChange: (ev) => patchLine(index, { taxPercent: ev.target.value }),
                        }),
                        e.jsx(Input, {
                          label: "Line discount",
                          type: "number", min: "0", step: "0.01",
                          value: line.discountAmount,
                          error: fieldErrors[`lines[${index}].discountAmount`],
                          onChange: (ev) => patchLine(index, { discountAmount: ev.target.value }),
                        }),
                        e.jsx(Input, {
                          label: "HSN / SAC",
                          value: line.hsnSacCode,
                          onChange: (ev) => patchLine(index, { hsnSacCode: ev.target.value }),
                        }),
                        e.jsxs("div", {
                          className: "qtn-line__amount",
                          children: [
                            e.jsx("div", { className: "qtn-detail__label", children: "Amount" }),
                            e.jsx("strong", { children: formatInr(amount) }),
                          ],
                        }),
                      ],
                    }),
                ],
              }, line.key);
            }),
          }),
          lines.length >= 100 && e.jsx("p", {
            className: "field__error", children: "A quotation carries at most 100 lines.",
          }),
        ],
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
          e.jsx(Detail, { label: "Customer", children: quotation.customerName }),
          e.jsx(Detail, { label: "Company", children: quotation.customerCompanyName }),
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
    title: "Services",
    children: [
      e.jsx("div", {
        className: "table-scroll",
        children: e.jsxs("table", {
          className: "data-table",
          children: [
            e.jsx("thead", {
              children: e.jsx("tr", {
                children: ["#", "Service", "Qty", "Duration", "Rate", "GST", "Amount"].map(
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
                        e.jsx("div", {
                          className: "qtn-cell__muted",
                          children: [
                            SERVICE_TYPE_LABEL[line.serviceType],
                            line.hsnSacCode ? `SAC ${line.hsnSacCode}` : null,
                          ].filter(Boolean).join(" · "),
                        }),
                      ],
                    }),
                    e.jsx("td", { children: line.quantity }),
                    e.jsx("td", {
                      children: line.durationValue
                        ? `${line.durationValue} ${line.durationUnit ?? ""}`.trim() : "—",
                    }),
                    e.jsx("td", { children: formatInr(line.unitPrice) }),
                    e.jsx("td", { children: `${line.taxPercent}%` }),
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
