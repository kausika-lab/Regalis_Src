/**
 * Admin -> Billing & Finance -> Cash Collections / Cash Register (Pay later, 27 Oct 2026).
 *
 *   AdminCashCollectionsPage  /admin/billing/cash-collections   what is owed + the receipts
 *   AdminCashRegisterPage     /admin/billing/cash-register      each collector's drawer per day
 *
 * Customers who chose "Pay later at the front desk" pay here in cash, UPI or card. Every
 * payment is a numbered receipt (GET /api/cash/collections/{id}/receipt). A receipt is
 * never edited or deleted; an Admin voids it with a reason. The register opens itself on
 * the first receipt of the day (zero float) unless it was opened with a float; closing it
 * compares the counted cash with the float + cash receipts, and an Admin approves it.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file by
 * name (deploy/pay-later/apply.mjs). It is a new file, so it needs no sw.js precache entry.
 */
import {
  a as useToast,
  R as usePermission,
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
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { b as fmtMoney, c as fmtDate, f as fmtDateTime } from "./formatters-DFe5xCRi.js";

const cashApi = {
  outstanding: (params) => unwrap(http.get("/cash/outstanding", { params })),
  collections: (params) => unwrap(http.get("/cash/collections", { params })),
  record: (body) => unwrap(http.post("/cash/collections", body)),
  void: (id, reason) => unwrap(http.post(`/cash/collections/${id}/void`, { reason })),
  /* No session today = data null, which unwrap() treats as an error: read the envelope. */
  current: (locationId) => http.get("/cash/register/current", { params: { locationId } }).then((r) => r.data?.data ?? null),
  open: (body) => unwrap(http.post("/cash/register/open", body)),
  close: (id, body) => unwrap(http.post(`/cash/register/${id}/close`, body)),
  approve: (id) => unwrap(http.post(`/cash/register/${id}/approve`)),
  sessions: (params) => unwrap(http.get("/cash/register", { params })),
  locations: () => unwrap(http.get("/locations", { params: { pageSize: 100 } })),
  receipt: async (id, receiptNumber) => {
    const response = await http.get(`/cash/collections/${id}/receipt`, { responseType: "blob" });
    const href = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = href;
    link.download = `${(receiptNumber ?? "receipt").replaceAll("/", "-")}.pdf`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  }
};

const METHODS = [
  { value: "6", label: "Cash" },
  { value: "2", label: "UPI" },
  { value: "4", label: "Debit card" },
  { value: "3", label: "Credit card" }
];
const METHOD_NAME = { 6: "Cash", 2: "UPI", 4: "Debit card", 3: "Credit card" };
const SESSION_STATUS = { 1: ["Open", "info"], 2: ["Closed — awaiting approval", "warning"], 3: ["Approved", "success"] };
const BOOKING_STATUS = { 2: "Confirmed", 3: "Checked in", 4: "Completed" };

const IconSearch = makeIcon("search", [
  ["circle", { cx: "11", cy: "11", r: "8", key: "4ej97u" }],
  ["path", { d: "m21 21-4.3-4.3", key: "1qie3q" }]
]);

const istToday = () => new Date(Date.now() + 5.5 * 3600e3).toISOString().slice(0, 10);
const errorText = (error, fallback) => (error instanceof ApiError ? error.message : fallback);
/** "1,234.5" -> 1234.5; null when not a positive amount with at most 2 decimals. */
const amountOf = (text) => {
  const t = String(text ?? "").replace(/[,\s₹]/g, "");
  if (!/^\d+(\.\d{1,2})?$/.test(t)) return null;
  return Number(t);
};
const pill = (label, tone) => e.jsx("span", { className: `pl-pill pl-pill--${tone}`, children: label });
const when = (startUtc, endUtc, months) =>
  months ? `${fmtDate(startUtc)} – ${fmtDate(endUtc)}` : `${fmtDateTime(startUtc)}`;

// ── Styles ──────────────────────────────────────────────────────────────────

const STYLE_ID = "cash-pages-styles";
const STYLES = `
.pl-cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:var(--space-4);margin:var(--space-5) 0}
@media(max-width:1100px){.pl-cards{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.pl-cards{grid-template-columns:minmax(0,1fr)}}
.pl-card{padding:var(--space-4) var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm)}
.pl-card__label{font-size:var(--font-size-sm);color:var(--text-secondary)}
.pl-card__value{margin:4px 0 2px;font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:800;color:var(--text-primary);font-variant-numeric:tabular-nums;line-height:1.2}
.pl-card__sub{font-size:var(--font-size-xs);color:var(--text-muted)}
.pl-panel{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);padding:var(--space-5)}
.pl-tabs{display:flex;gap:var(--space-2);margin-bottom:var(--space-4);flex-wrap:wrap}
.pl-tab{all:unset;padding:8px 14px;border-radius:var(--radius-full);border:1px solid var(--border-strong);font-size:var(--font-size-sm);font-weight:600;cursor:pointer;color:var(--text-secondary)}
.pl-tab[aria-selected="true"]{background:var(--regalis-black);border-color:var(--regalis-black);color:var(--regalis-white)}
.pl-toolbar{display:grid;grid-template-columns:minmax(220px,1.6fr) repeat(3,minmax(150px,.8fr));gap:var(--space-3);margin-bottom:var(--space-4)}
@media(max-width:900px){.pl-toolbar{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.pl-toolbar{grid-template-columns:minmax(0,1fr)}}
.pl-toolbar .field{min-width:0}
.pl-toolbar .field__label{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}
.pl-toolbar .field__input{height:40px}
.pl-search{position:relative}
.pl-search svg{position:absolute;inset-inline-start:12px;top:50%;transform:translateY(-50%);color:var(--text-muted);pointer-events:none}
.pl-search input{width:100%;height:40px;padding:8px 12px 8px 36px}
.pl-table-wrap{overflow-x:auto}
.pl-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.pl-table th{padding:10px 12px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--text-secondary);background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.pl-table td{padding:12px;border-bottom:1px solid var(--border-subtle);vertical-align:middle}
.pl-num{text-align:right!important;font-variant-numeric:tabular-nums;white-space:nowrap}
.pl-strong{font-weight:700;color:var(--text-primary)}
.pl-sub{font-size:var(--font-size-xs);color:var(--text-muted);overflow-wrap:anywhere}
.pl-void td{color:var(--text-muted)}
.pl-void .pl-num{text-decoration:line-through}
.pl-actions{display:flex;gap:var(--space-2);justify-content:flex-end;flex-wrap:wrap}
.pl-pager{display:flex;align-items:center;justify-content:space-between;gap:var(--space-3);margin-top:var(--space-4);font-size:var(--font-size-sm);color:var(--text-secondary)}
.pl-pager div{display:flex;gap:var(--space-2)}
.pl-pill{display:inline-flex;padding:3px 10px;border-radius:var(--radius-full);font-size:var(--font-size-xs);font-weight:600;white-space:nowrap}
.pl-pill--success{background:color-mix(in srgb,var(--color-success) 14%,transparent);color:var(--color-success)}
.pl-pill--neutral{background:var(--surface-inset);color:var(--text-secondary)}
.pl-pill--info{background:color-mix(in srgb,var(--color-info) 12%,transparent);color:var(--color-info)}
.pl-pill--warning{background:color-mix(in srgb,var(--color-warning) 16%,transparent);color:color-mix(in srgb,var(--color-warning) 80%,black)}
.pl-pill--danger{background:color-mix(in srgb,var(--color-danger) 12%,transparent);color:var(--color-danger)}
.pl-box{padding:var(--space-3) var(--space-4);border:1px solid rgb(var(--accent-rgb) / 30%);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%);display:flex;flex-direction:column;gap:4px;font-size:var(--font-size-sm)}
.pl-box__row{display:flex;justify-content:space-between;gap:var(--space-3)}
.pl-register{display:flex;align-items:center;justify-content:space-between;gap:var(--space-4);flex-wrap:wrap;padding:var(--space-4) var(--space-5);margin:var(--space-5) 0;border:1px solid rgb(var(--accent-rgb) / 30%);border-radius:var(--radius-lg);background:rgb(var(--accent-rgb) / 8%)}
.pl-register__facts{display:flex;gap:var(--space-5);flex-wrap:wrap;font-size:var(--font-size-sm)}
.pl-register__facts b{display:block;font-family:var(--font-display);font-size:var(--font-size-lg);font-variant-numeric:tabular-nums}
.modal:has(.pl-modal){max-width:560px}
@media(max-width:767px){.pl-table th:nth-child(3),.pl-table td:nth-child(3){display:none}}
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

/** The location the desk works at: remembered per browser, first location by default. */
function useLocation() {
  const [locations, setLocations] = React.useState([]);
  const [locationId, setLocationId] = React.useState(() => {
    try { return localStorage.getItem("cash.locationId") ?? ""; } catch { return ""; }
  });
  React.useEffect(() => {
    cashApi.locations().then((r) => {
      const list = (Array.isArray(r) ? r : r?.items ?? []).filter((l) => l.isActive !== false);
      setLocations(list);
      setLocationId((current) => (current && list.some((l) => String(l.locationId) === String(current)) ? current : String(list[0]?.locationId ?? "")));
    }).catch(() => setLocations([]));
  }, []);
  const choose = (value) => {
    setLocationId(value);
    try { localStorage.setItem("cash.locationId", value); } catch { /* private mode */ }
  };
  return { locations, locationId, choose };
}

function StatCard({ label, value, sub }) {
  return e.jsxs("div", {
    className: "pl-card",
    children: [
      e.jsx("div", { className: "pl-card__label", children: label }),
      e.jsx("div", { className: "pl-card__value", children: value }),
      e.jsx("div", { className: "pl-card__sub", children: sub })
    ]
  });
}

function Pager({ result, page, setPage }) {
  if (!result || result.totalPages <= 1) return null;
  return e.jsxs("div", {
    className: "pl-pager",
    children: [
      e.jsx("span", { children: `Page ${result.page} of ${result.totalPages} · ${result.totalCount} rows` }),
      e.jsxs("div", { children: [
        e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasPrevious, onClick: () => setPage(page - 1), children: "Previous" }),
        e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasNext, onClick: () => setPage(page + 1), children: "Next" })
      ] })
    ]
  });
}

// ── Take payment ────────────────────────────────────────────────────────────

function CollectModal({ booking, onClose, onDone }) {
  const toast = useToast();
  const [amount, setAmount] = React.useState(String(booking.balance));
  const [method, setMethod] = React.useState("6");
  const [tendered, setTendered] = React.useState("");
  const [reference, setReference] = React.useState("");
  const [notes, setNotes] = React.useState("");
  const [errors, setErrors] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  const cash = method === "6";
  const value = amountOf(amount);
  const given = amountOf(tendered);
  const change = cash && value !== null && given !== null && given >= value ? given - value : null;

  const submit = async () => {
    const errs = {};
    if (value === null || value <= 0) errs.amount = "Enter an amount, up to 2 decimals.";
    else if (value > booking.balance) errs.amount = `Only ${fmtMoney(booking.balance)} is due.`;
    if (cash && tendered && (given === null || (value !== null && given < value))) errs.tendered = "Less than the amount.";
    if (!cash && !reference.trim()) errs.reference = "Enter the UPI transaction id or the card slip number.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const receipt = await cashApi.record({
        bookingId: booking.bookingId,
        amount: value,
        paymentMethod: Number(method),
        amountTendered: cash && given !== null ? given : null,
        referenceNumber: cash ? null : reference.trim(),
        notes: notes.trim() || null
      });
      toast.success(`${fmtMoney(value)} received — receipt ${receipt.receiptNumber}.${receipt.bookingBalance > 0 ? ` ${fmtMoney(receipt.bookingBalance)} still due.` : " Paid in full."}`);
      onDone(receipt);
    } catch (error) {
      toast.error(errorText(error, "Could not record the payment."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: `Take payment — ${booking.bookingNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Record payment" })
    ] }),
    children: e.jsxs("div", {
      className: "form-stack pl-modal",
      children: [
        e.jsxs("div", { className: "pl-box", children: [
          e.jsxs("div", { className: "pl-box__row", children: [e.jsx("span", { className: "pl-strong", children: booking.customerName }), e.jsx("span", { children: booking.customerMobile })] }),
          e.jsxs("div", { className: "pl-box__row", children: [e.jsx("span", { children: booking.workspaceName }), e.jsx("span", { children: when(booking.startUtc, booking.endUtc, booking.billingMonths) })] }),
          e.jsxs("div", { className: "pl-box__row", children: [e.jsx("span", { children: `Total ${fmtMoney(booking.totalAmount)} · paid ${fmtMoney(booking.amountPaid)}` }), e.jsx("span", { className: "pl-strong", children: `Due ${fmtMoney(booking.balance)}` })] })
        ] }),
        e.jsxs("div", { className: "form-grid", children: [
          e.jsx(Input, { label: "Amount", required: true, inputMode: "decimal", value: amount, error: errors.amount, autoFocus: true, onChange: (ev) => setAmount(ev.target.value) }),
          e.jsx(Select, { label: "Paid by", required: true, value: method, options: METHODS, onChange: (ev) => setMethod(ev.target.value) }),
          cash
            ? e.jsx(Input, { label: "Cash handed over", inputMode: "decimal", placeholder: "Optional", value: tendered, error: errors.tendered, hint: change !== null ? `Change to return: ${fmtMoney(change)}` : "To work out the change.", onChange: (ev) => setTendered(ev.target.value) })
            : e.jsx(Input, { label: method === "2" ? "UPI transaction id" : "Card slip number", required: true, maxLength: 50, value: reference, error: errors.reference, onChange: (ev) => setReference(ev.target.value) })
        ] }),
        e.jsx(Input, { label: "Notes", maxLength: 500, value: notes, onChange: (ev) => setNotes(ev.target.value) }),
        e.jsx("p", { className: "pl-sub", style: { margin: 0 }, children: "Saving issues a numbered receipt and updates the booking and its invoice. A receipt cannot be edited; an Admin can void it with a reason." })
      ]
    })
  });
}

function VoidModal({ receipt, onClose, onDone }) {
  const toast = useToast();
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    if (!reason.trim()) return;
    setBusy(true);
    try {
      await cashApi.void(receipt.cashCollectionId, reason.trim());
      toast.success(`Receipt ${receipt.receiptNumber} voided. The booking owes ${fmtMoney(receipt.amount)} more.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not void the receipt."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Void receipt ${receipt.receiptNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { variant: "danger", onClick: submit, loading: busy, disabled: busy || !reason.trim(), children: "Void receipt" })
    ] }),
    children: e.jsxs("div", { className: "form-stack pl-modal", children: [
      e.jsx("p", { style: { margin: 0 }, children: `${fmtMoney(receipt.amount)} by ${METHOD_NAME[receipt.paymentMethod] ?? "—"} for ${receipt.bookingNumber}. The receipt stays on record, marked void; the amount goes back onto the booking's balance.` }),
      e.jsx(Input, { label: "Reason", required: true, maxLength: 500, autoFocus: true, value: reason, onChange: (ev) => setReason(ev.target.value) })
    ] })
  });
}

// ── Screen 1: Cash Collections ──────────────────────────────────────────────

function AdminCashCollectionsPage() {
  useStyles();
  const toast = useToast();
  const canCollect = usePermission("PAYMENT.CASH_COLLECT");
  const canVoid = usePermission("PAYMENT.CASH_VOID");
  const { locations, locationId, choose } = useLocation();
  const [tab, setTab] = React.useState("due");
  const [search, setSearch] = React.useState("");
  const [overdue, setOverdue] = React.useState("all");
  const [method, setMethod] = React.useState("");
  const [from, setFrom] = React.useState(istToday());
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [session, setSession] = React.useState(undefined);
  const [loading, setLoading] = React.useState(true);
  const [collecting, setCollecting] = React.useState(null);
  const [voiding, setVoiding] = React.useState(null);
  const debounced = useDebounced(search);

  const load = React.useCallback(async () => {
    if (!locationId) return;
    setLoading(true);
    try {
      const common = { locationId, search: debounced.trim() || undefined, page, pageSize: 20 };
      setData(tab === "due"
        ? await cashApi.outstanding({ ...common, overdueOnly: overdue === "overdue" })
        : await cashApi.collections({ ...common, paymentMethod: method || undefined, from: from || undefined }));
      if (canCollect) setSession(await cashApi.current(locationId));
    } catch (error) {
      toast.error(errorText(error, "Failed to load."));
    } finally {
      setLoading(false);
    }
  }, [tab, locationId, debounced, overdue, method, from, page, canCollect]);
  React.useEffect(() => { load(); }, [load]);

  const switchTab = (t) => { setTab(t); setPage(1); setData(null); };
  const items = data?.items ?? [];
  const cashToday = session?.totals?.find((t) => t.paymentMethod === 6)?.amount ?? 0;
  const otherToday = (session?.totals ?? []).filter((t) => t.paymentMethod !== 6).reduce((s, t) => s + t.amount, 0);

  const dueTable = () => e.jsx("div", { className: "pl-table-wrap", children: e.jsxs("table", { className: "pl-table", children: [
    e.jsx("thead", { children: e.jsxs("tr", { children: [
      e.jsx("th", { children: "Booking" }), e.jsx("th", { children: "Customer" }), e.jsx("th", { children: "Workspace · when" }),
      e.jsx("th", { className: "pl-num", children: "Total" }), e.jsx("th", { className: "pl-num", children: "Paid" }), e.jsx("th", { className: "pl-num", children: "Due" }),
      e.jsx("th", { children: "Due by" }), e.jsx("th", {})
    ] }) }),
    e.jsx("tbody", { children: items.map((b) => e.jsxs("tr", { children: [
      e.jsxs("td", { children: [e.jsx("div", { className: "pl-strong", children: b.bookingNumber }), e.jsx("div", { className: "pl-sub", children: BOOKING_STATUS[b.status] ?? "" })] }),
      e.jsxs("td", { children: [e.jsx("div", { className: "pl-strong", children: b.customerName }), e.jsx("div", { className: "pl-sub", children: b.customerMobile })] }),
      e.jsxs("td", { children: [e.jsx("div", { children: b.workspaceName }), e.jsx("div", { className: "pl-sub", children: when(b.startUtc, b.endUtc, b.billingMonths) })] }),
      e.jsx("td", { className: "pl-num", children: fmtMoney(b.totalAmount) }),
      e.jsx("td", { className: "pl-num", children: fmtMoney(b.amountPaid) }),
      e.jsx("td", { className: "pl-num pl-strong", children: fmtMoney(b.balance) }),
      e.jsx("td", { children: b.isOverdue ? pill("Overdue", "danger") : b.paymentDueUtc ? fmtDateTime(b.paymentDueUtc) : "—" }),
      e.jsx("td", { children: e.jsx("div", { className: "pl-actions", children: canCollect && e.jsx(Button, { size: "sm", onClick: () => setCollecting(b), children: "Take payment" }) }) })
    ] }, b.bookingId)) })
  ] }) });

  const receiptsTable = () => e.jsx("div", { className: "pl-table-wrap", children: e.jsxs("table", { className: "pl-table", children: [
    e.jsx("thead", { children: e.jsxs("tr", { children: [
      e.jsx("th", { children: "Receipt" }), e.jsx("th", { children: "Customer · booking" }), e.jsx("th", { children: "Received by" }),
      e.jsx("th", { children: "Paid by" }), e.jsx("th", { className: "pl-num", children: "Amount" }), e.jsx("th", { children: "Status" }), e.jsx("th", {})
    ] }) }),
    e.jsx("tbody", { children: items.map((r) => e.jsxs("tr", { className: r.status === 2 ? "pl-void" : "", children: [
      e.jsxs("td", { children: [e.jsx("div", { className: "pl-strong", children: r.receiptNumber }), e.jsx("div", { className: "pl-sub", children: fmtDateTime(r.collectedUtc) })] }),
      e.jsxs("td", { children: [e.jsx("div", { children: r.customerName }), e.jsx("div", { className: "pl-sub", children: `${r.bookingNumber}${r.invoiceNumber ? ` · ${r.invoiceNumber}` : ""}` })] }),
      e.jsxs("td", { children: [e.jsx("div", { children: r.collectedByName }), e.jsx("div", { className: "pl-sub", children: r.sessionNumber ?? "" })] }),
      e.jsxs("td", { children: [e.jsx("div", { children: METHOD_NAME[r.paymentMethod] ?? "—" }), e.jsx("div", { className: "pl-sub", children: r.referenceNumber ?? (r.changeReturned ? `Change ${fmtMoney(r.changeReturned)}` : "") })] }),
      e.jsx("td", { className: "pl-num pl-strong", children: fmtMoney(r.amount) }),
      e.jsx("td", { children: r.status === 2 ? e.jsxs("div", { children: [pill("Void", "danger"), e.jsx("div", { className: "pl-sub", title: r.voidReason, children: `${r.voidedByName ?? ""}: ${r.voidReason ?? ""}` })] }) : pill("Recorded", "success") }),
      e.jsx("td", { children: e.jsxs("div", { className: "pl-actions", children: [
        e.jsx(Button, { variant: "outline", size: "sm", onClick: () => cashApi.receipt(r.cashCollectionId, r.receiptNumber).catch(() => toast.error("Could not download the receipt.")), children: "Receipt" }),
        canVoid && r.status === 1 && e.jsx(Button, { variant: "ghost", size: "sm", onClick: () => setVoiding(r), children: "Void" })
      ] }) })
    ] }, r.cashCollectionId)) })
  ] }) });

  return e.jsxs(e.Fragment, { children: [
    e.jsxs("header", { className: "page-header", children: [
      e.jsxs("div", { children: [
        e.jsx("h2", { className: "page-header__title", children: "Cash collections" }),
        e.jsx("p", { className: "page-header__description", children: "Bookings made with “Pay later at the front desk”, and every payment taken for them in cash, UPI or card. Check-in stays blocked until a booking is paid in full." })
      ] }),
      e.jsx("div", { className: "page-header__actions", children: locations.length > 1 && e.jsx(Select, { label: "Location", value: locationId, options: locations.map((l) => ({ value: String(l.locationId), label: l.locationName ?? l.name ?? `Location ${l.locationId}` })), onChange: (ev) => { choose(ev.target.value); setPage(1); } }) })
    ] }),
    canCollect && e.jsxs("div", { className: "pl-cards", children: [
      e.jsx(StatCard, { label: "Your cash today", value: session === undefined ? "—" : fmtMoney(cashToday), sub: session ? `${session.sessionNumber} · float ${fmtMoney(session.openingFloat)}` : "No receipt yet today" }),
      e.jsx(StatCard, { label: "Your UPI & card today", value: session === undefined ? "—" : fmtMoney(otherToday), sub: "Reconciled against the bank" }),
      e.jsx(StatCard, { label: "Expected in your drawer", value: session ? fmtMoney(session.expectedCash) : "—", sub: session ? (SESSION_STATUS[session.status]?.[0] ?? "") : "Opens with your first receipt" }),
      e.jsx(StatCard, { label: tab === "due" ? "Bookings to collect" : "Receipts", value: data ? String(data.totalCount) : "—", sub: tab === "due" ? "At this location" : `From ${from ? fmtDate(from) : "the start"}` })
    ] }),
    e.jsxs("section", { className: "pl-panel", "aria-label": "Cash collections", children: [
      e.jsxs("div", { className: "pl-tabs", role: "tablist", children: [
        e.jsx("button", { type: "button", role: "tab", className: "pl-tab", "aria-selected": tab === "due", onClick: () => switchTab("due"), children: "To collect" }),
        e.jsx("button", { type: "button", role: "tab", className: "pl-tab", "aria-selected": tab === "receipts", onClick: () => switchTab("receipts"), children: "Receipts" })
      ] }),
      e.jsxs("div", { className: "pl-toolbar", role: "search", children: [
        e.jsxs("div", { className: "pl-search", children: [e.jsx(IconSearch, { size: 16 }), e.jsx("input", { className: "field__input", "aria-label": "Search", placeholder: tab === "due" ? "Booking no., name, mobile…" : "Receipt, booking, name, reference…", value: search, onChange: (ev) => { setSearch(ev.target.value); setPage(1); } })] }),
        tab === "due"
          ? e.jsx(Select, { label: "Show", value: overdue, options: [{ value: "all", label: "All unpaid" }, { value: "overdue", label: "Overdue only" }], onChange: (ev) => { setOverdue(ev.target.value); setPage(1); } })
          : e.jsx(Select, { label: "Paid by", value: method, options: [{ value: "", label: "All methods" }, ...METHODS], onChange: (ev) => { setMethod(ev.target.value); setPage(1); } }),
        tab === "receipts" && e.jsx(Input, { label: "From", type: "date", max: istToday(), value: from, onChange: (ev) => { setFrom(ev.target.value); setPage(1); } })
      ] }),
      loading && !data
        ? e.jsx(Spinner, { center: true })
        : items.length === 0
          ? e.jsx(EmptyState, { title: tab === "due" ? "Nothing to collect" : "No receipts", description: tab === "due" ? "Every pay-later booking at this location is paid." : "No payments match these filters." })
          : tab === "due" ? dueTable() : receiptsTable(),
      e.jsx(Pager, { result: data, page, setPage })
    ] }),
    collecting && e.jsx(CollectModal, { booking: collecting, onClose: () => setCollecting(null), onDone: (receipt) => {
      setCollecting(null);
      load();
      cashApi.receipt(receipt.cashCollectionId, receipt.receiptNumber).catch(() => {});
    } }),
    voiding && e.jsx(VoidModal, { receipt: voiding, onClose: () => setVoiding(null), onDone: () => { setVoiding(null); load(); } })
  ] });
}

// ── Screen 2: Cash Register ─────────────────────────────────────────────────

function OpenModal({ locationId, onClose, onDone }) {
  const toast = useToast();
  const [float, setFloat] = React.useState("0");
  const [busy, setBusy] = React.useState(false);
  const value = amountOf(float) ?? (float.trim() === "0" ? 0 : null);
  const submit = async () => {
    if (value === null) return;
    setBusy(true);
    try {
      await cashApi.open({ locationId: Number(locationId), openingFloat: value });
      toast.success("Register opened.");
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not open the register."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: "Open today's register",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, disabled: busy || value === null, children: "Open register" })] }),
    children: e.jsxs("div", { className: "form-stack pl-modal", children: [
      e.jsx(Input, { label: "Opening float (cash in the drawer now)", inputMode: "decimal", autoFocus: true, value: float, onChange: (ev) => setFloat(ev.target.value) }),
      e.jsx("p", { className: "pl-sub", style: { margin: 0 }, children: "If you skip this, the register opens by itself with a zero float on your first receipt of the day." })
    ] })
  });
}

function CloseModal({ session, onClose, onDone }) {
  const toast = useToast();
  const [counted, setCounted] = React.useState("");
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const value = amountOf(counted) ?? (counted.trim() === "0" ? 0 : null);
  const diff = value === null ? null : Math.round((value - session.expectedCash) * 100) / 100;
  const submit = async () => {
    if (value === null || (diff !== 0 && !reason.trim())) return;
    setBusy(true);
    try {
      await cashApi.close(session.cashRegisterSessionId, { countedCash: value, varianceReason: reason.trim() || null });
      toast.success("Register closed. An Admin approves it when the cash is handed over.");
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not close the register."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Close register ${session.sessionNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, disabled: busy || value === null || (diff !== 0 && !reason.trim()), children: "Close register" })] }),
    children: e.jsxs("div", { className: "form-stack pl-modal", children: [
      e.jsxs("div", { className: "pl-box", children: [
        e.jsxs("div", { className: "pl-box__row", children: [e.jsx("span", { children: "Opening float" }), e.jsx("span", { children: fmtMoney(session.openingFloat) })] }),
        e.jsxs("div", { className: "pl-box__row", children: [e.jsx("span", { children: "Cash receipts" }), e.jsx("span", { children: fmtMoney(session.cashCollected) })] }),
        e.jsxs("div", { className: "pl-box__row pl-strong", children: [e.jsx("span", { children: "Expected in the drawer" }), e.jsx("span", { children: fmtMoney(session.expectedCash) })] })
      ] }),
      e.jsx(Input, { label: "Cash counted", required: true, inputMode: "decimal", autoFocus: true, value: counted, hint: diff === null ? undefined : diff === 0 ? "Matches." : `${diff > 0 ? "Over" : "Short"} by ${fmtMoney(Math.abs(diff))}.`, onChange: (ev) => setCounted(ev.target.value) }),
      diff !== null && diff !== 0 && e.jsx(Input, { label: "Why is it different?", required: true, maxLength: 500, value: reason, onChange: (ev) => setReason(ev.target.value) })
    ] })
  });
}

function AdminCashRegisterPage() {
  useStyles();
  const toast = useToast();
  const canClose = usePermission("PAYMENT.CASH_CLOSE");
  const canApprove = usePermission("PAYMENT.CASH_APPROVE");
  const { locations, locationId, choose } = useLocation();
  const [status, setStatus] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [mine, setMine] = React.useState(undefined);
  const [opening, setOpening] = React.useState(false);
  const [closing, setClosing] = React.useState(null);
  const [approving, setApproving] = React.useState(null);

  const load = React.useCallback(async () => {
    if (!locationId) return;
    try {
      setData(await cashApi.sessions({ locationId, status: status || undefined, page, pageSize: 20 }));
      if (canClose) setMine(await cashApi.current(locationId));
    } catch (error) {
      toast.error(errorText(error, "Failed to load the registers."));
    }
  }, [locationId, status, page, canClose]);
  React.useEffect(() => { load(); }, [load]);

  const approve = async (s) => {
    setApproving(s.cashRegisterSessionId);
    try {
      await cashApi.approve(s.cashRegisterSessionId);
      toast.success(`${s.sessionNumber} approved.`);
      load();
    } catch (error) {
      toast.error(errorText(error, "Could not approve."));
    } finally {
      setApproving(null);
    }
  };
  const totalsText = (s) => s.totals.filter((t) => t.paymentMethod !== 6).map((t) => `${METHOD_NAME[t.paymentMethod]} ${fmtMoney(t.amount)}`).join(" · ") || "—";
  const items = data?.items ?? [];

  return e.jsxs(e.Fragment, { children: [
    e.jsxs("header", { className: "page-header", children: [
      e.jsxs("div", { children: [
        e.jsx("h2", { className: "page-header__title", children: "Cash register" }),
        e.jsx("p", { className: "page-header__description", children: "Each collector's drawer for the day. Close it with the counted cash; any difference needs a reason, and an Admin approves the close when the cash is handed over. UPI and card are listed here but reconciled against the bank." })
      ] }),
      e.jsx("div", { className: "page-header__actions", children: locations.length > 1 && e.jsx(Select, { label: "Location", value: locationId, options: locations.map((l) => ({ value: String(l.locationId), label: l.locationName ?? l.name ?? `Location ${l.locationId}` })), onChange: (ev) => { choose(ev.target.value); setPage(1); } }) })
    ] }),
    canClose && mine !== undefined && e.jsxs("div", { className: "pl-register", children: [
      mine
        ? e.jsxs("div", { className: "pl-register__facts", children: [
            e.jsxs("div", { children: [e.jsx("span", { className: "pl-sub", children: `Your register ${mine.sessionNumber}` }), e.jsx("b", { children: SESSION_STATUS[mine.status]?.[0] })] }),
            e.jsxs("div", { children: [e.jsx("span", { className: "pl-sub", children: "Float" }), e.jsx("b", { children: fmtMoney(mine.openingFloat) })] }),
            e.jsxs("div", { children: [e.jsx("span", { className: "pl-sub", children: "Cash receipts" }), e.jsx("b", { children: fmtMoney(mine.cashCollected) })] }),
            e.jsxs("div", { children: [e.jsx("span", { className: "pl-sub", children: mine.status === 1 ? "Expected in the drawer" : "Counted" }), e.jsx("b", { children: fmtMoney(mine.status === 1 ? mine.expectedCash : mine.countedCash) })] })
          ] })
        : e.jsx("div", { className: "pl-sub", children: "You have no register open at this location today." }),
      mine
        ? mine.status === 1 && e.jsx(Button, { onClick: () => setClosing(mine), children: "Count & close" })
        : e.jsx(Button, { onClick: () => setOpening(true), children: "Open register" })
    ] }),
    e.jsxs("section", { className: "pl-panel", "aria-label": "Register sessions", children: [
      e.jsx("div", { className: "pl-toolbar", children: e.jsx(Select, { label: "Status", value: status, options: [{ value: "", label: "All sessions" }, { value: "1", label: "Open" }, { value: "2", label: "Awaiting approval" }, { value: "3", label: "Approved" }], onChange: (ev) => { setStatus(ev.target.value); setPage(1); } }) }),
      data === null
        ? e.jsx(Spinner, { center: true })
        : items.length === 0
          ? e.jsx(EmptyState, { title: "No register sessions", description: "A session starts when a collector opens the register or takes the first payment of the day." })
          : e.jsx("div", { className: "pl-table-wrap", children: e.jsxs("table", { className: "pl-table", children: [
              e.jsx("thead", { children: e.jsxs("tr", { children: [
                e.jsx("th", { children: "Session" }), e.jsx("th", { children: "Collector" }), e.jsx("th", { children: "UPI & card" }),
                e.jsx("th", { className: "pl-num", children: "Expected" }), e.jsx("th", { className: "pl-num", children: "Counted" }), e.jsx("th", { className: "pl-num", children: "Difference" }),
                e.jsx("th", { children: "Status" }), e.jsx("th", {})
              ] }) }),
              e.jsx("tbody", { children: items.map((s) => e.jsxs("tr", { children: [
                e.jsxs("td", { children: [e.jsx("div", { className: "pl-strong", children: s.sessionNumber }), e.jsx("div", { className: "pl-sub", children: fmtDate(s.businessDate) })] }),
                e.jsxs("td", { children: [e.jsx("div", { children: s.userName }), e.jsx("div", { className: "pl-sub", children: `Float ${fmtMoney(s.openingFloat)} · cash ${fmtMoney(s.cashCollected)}` })] }),
                e.jsx("td", { className: "pl-sub", children: totalsText(s) }),
                e.jsx("td", { className: "pl-num", children: fmtMoney(s.expectedCash) }),
                e.jsx("td", { className: "pl-num", children: s.countedCash === null ? "—" : fmtMoney(s.countedCash) }),
                e.jsxs("td", { className: "pl-num", children: [s.variance === null ? "—" : s.variance === 0 ? "None" : pill(`${s.variance > 0 ? "+" : "−"}${fmtMoney(Math.abs(s.variance))}`, "warning"), s.varianceReason && e.jsx("div", { className: "pl-sub", title: s.varianceReason, children: s.varianceReason })] }),
                e.jsxs("td", { children: [pill(SESSION_STATUS[s.status]?.[0] ?? "", SESSION_STATUS[s.status]?.[1] ?? "neutral"), s.handedOverToName && e.jsx("div", { className: "pl-sub", children: `by ${s.handedOverToName}` })] }),
                e.jsx("td", { children: e.jsx("div", { className: "pl-actions", children: canApprove && s.status === 2 && e.jsx(Button, { size: "sm", loading: approving === s.cashRegisterSessionId, onClick: () => approve(s), children: "Approve" }) }) })
              ] }, s.cashRegisterSessionId)) })
            ] }) }),
      e.jsx(Pager, { result: data, page, setPage })
    ] }),
    opening && e.jsx(OpenModal, { locationId, onClose: () => setOpening(false), onDone: () => { setOpening(false); load(); } }),
    closing && e.jsx(CloseModal, { session: closing, onClose: () => setClosing(null), onDone: () => { setClosing(null); load(); } })
  ] });
}

export { AdminCashCollectionsPage, AdminCashRegisterPage };
