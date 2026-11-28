/**
 * quotation-payment kit (27 Nov 2026) - Billing & Finance -> Payments -> "Quotations" tab.
 *
 *   QuotationPaymentsPanel   every APPROVED quotation with the payment position of its invoice
 *
 * The invoice is the single source of truth: Paid and Outstanding are the invoice's own figures (the
 * Invoices screen shows the same), and "Record payment" opens the same Record payment dialog as the
 * Invoices screen, posting to /invoices/{id}/payments. No payment is ever tied to the quotation itself,
 * so nothing here can create a second payment row. Rejected, cancelled, expired, draft and revised
 * quotations are never listed (the server lists Approved only).
 *
 * Hand-maintained chunk (this repo ships the built SPA). BillingMoneyIn-BF1.js imports it by name
 * (deploy/quotation-payment/frontend/apply.mjs). A change ships under a NEW name (QP2).
 */
import {
  React, e, Button, EmptyState, useNavigate,
  api, money, fmtDate, fmtDateTime, StatCard, Chips, SearchBox, Pager, Table, pill, useStyles, useLoad,
  useDebounced, RecordPaymentDialog
} from "./BillingShared-BF1.js";

/* The invoice labels of the Invoices and Payments screens (quotation-invoice kit), plus "Not invoiced". */
const STATUS_TONE = { Due: "warning", "Partially Paid": "warning", Paid: "success", Cancelled: "neutral", Refunded: "neutral", "Not invoiced": "danger" };
const FILTERS = [
  { value: "", label: "All approved" },
  { value: "open", label: "Awaiting payment" },
  { value: "due", label: "Due" },
  { value: "partial", label: "Partially paid" },
  { value: "paid", label: "Paid" },
  { value: "noinvoice", label: "Not invoiced" },
  { value: "cancelled", label: "Invoice cancelled" }
];

export function QuotationPaymentsPanel({ initialSearch = "", onShowPayments }) {
  useStyles();
  const navigate = useNavigate();
  const [search, setSearch] = React.useState(initialSearch);
  const [status, setStatus] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [recording, setRecording] = React.useState(null);
  const debounced = useDebounced(search);
  React.useEffect(() => setPage(1), [debounced, status]);
  const { data, loading, reload } = useLoad(
    () => api.get("/payments/quotations", { page, pageSize: 20, search: debounced.trim() || undefined, status: status || undefined }),
    [page, debounced, status]);
  const { data: sum, reload: reloadSum } = useLoad(
    () => api.get("/payments/quotations/summary", { search: debounced.trim() || undefined }), [debounced]);
  const recorded = () => {
    setRecording(null);
    reload();
    reloadSum();
  };

  const columns = [
    { key: "quotation", header: "Quotation", render: (r) => e.jsxs("div", { children: [
      e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/quotations/${r.quotationId}`), children: r.quotationNumber }),
      e.jsx("div", { className: "bf-sub", children: [r.approvedUtc ? `Approved ${fmtDate(r.approvedUtc)}` : "Approved", r.referenceNumber ? `Ref ${r.referenceNumber}` : null].filter(Boolean).join(" · ") })
    ] }) },
    { key: "customer", header: "Customer", render: (r) => e.jsxs("div", { children: [
      e.jsx("div", { children: r.customerName || "—" }),
      r.customerEmail ? e.jsx("div", { className: "bf-sub", children: r.customerEmail }) : null
    ] }) },
    { key: "invoice", header: "Invoice", render: (r) => (r.invoiceId
      ? e.jsxs("div", { children: [
        e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/invoices/${r.invoiceId}`), children: r.invoiceNumber }),
        e.jsx("div", { className: r.isOverdue ? "bf-sub bf-red" : "bf-sub", children: `Due ${fmtDate(r.dueDate)}` })
      ] })
      : e.jsxs("div", { children: [
        e.jsx("span", { className: "bf-sub", children: "No invoice yet" }),
        r.noInvoiceReason ? e.jsx("div", { className: "bf-sub", style: { maxWidth: 260 }, children: r.noInvoiceReason }) : null
      ] })) },
    { key: "total", header: "Total", num: true, render: (r) => money(r.invoiceTotal ?? r.quotationTotal) },
    { key: "paid", header: "Paid", num: true, render: (r) => (r.invoiceId ? money(r.paidAmount) : "—") },
    { key: "out", header: "Outstanding", num: true, render: (r) => (r.outstandingAmount == null ? "—" : e.jsx("span", { className: "bf-strong", children: money(r.outstandingAmount) })) },
    { key: "status", header: "Payment Status", render: (r) => e.jsxs("div", { style: { display: "flex", gap: 6, flexWrap: "wrap" }, children: [
      pill(r.paymentStatus, STATUS_TONE[r.paymentStatus] ?? "neutral"),
      r.isOverdue ? pill("Overdue", "danger") : null,
      r.paymentCount > 0 ? e.jsx("div", { className: "bf-sub", style: { flexBasis: "100%" }, children: `${r.paymentCount} payment${r.paymentCount === 1 ? "" : "s"}${r.lastPaymentUtc ? ` · last ${fmtDateTime(r.lastPaymentUtc)}` : ""}` }) : null,
      r.deposit ? e.jsx("div", { className: "bf-sub", style: { flexBasis: "100%" }, "data-testid": "qp-deposit",
        children: `Security deposit ${r.deposit.invoiceNumber}: ${money(r.deposit.totalAmount)}, paid ${money(r.deposit.paidAmount)} · ${r.deposit.paymentStatus}` }) : null
    ] }) },
    { key: "actions", header: "", render: (r) => e.jsxs("div", { className: "bf-actions", children: [
      r.canRecordPayment ? e.jsx(Button, { size: "sm", className: "btn--gold", onClick: () => setRecording({ invoiceId: r.invoiceId }), children: "+ Record payment" }) : null,
      r.deposit && r.deposit.canRecordPayment ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setRecording({ invoiceId: r.deposit.invoiceId }), children: "+ Deposit payment" }) : null,
      r.paymentCount > 0 ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => (onShowPayments ? onShowPayments(r.quotationNumber) : navigate(`/admin/billing/payments?search=${encodeURIComponent(r.quotationNumber)}`)), children: "Payments" }) : null,
      !r.invoiceId ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => navigate(`/admin/billing/quotations/${r.quotationId}`), children: "Open quotation" }) : null
    ] }) }
  ];

  return e.jsxs("div", { "data-testid": "qp-panel", children: [
    e.jsx("p", { className: "bf-sub", style: { marginTop: 0 }, children:
      "Approved quotations and the payment of the invoice each one raised. The figures are the invoice's own, as on the Invoices screen; a payment is recorded against the invoice. Rejected, cancelled and expired quotations are not listed." }),
    sum ? e.jsxs("div", { className: "bf-cards", children: [
      e.jsx(StatCard, { label: "Approved quotations", value: String(sum.approved), sub: `${sum.notInvoiced} not invoiced · ${sum.cancelled} invoice cancelled`, onClick: () => setStatus(""), pressed: status === "" }),
      e.jsx(StatCard, { label: "Invoiced", value: money(sum.invoicedTotal), sub: `${sum.due} due · ${sum.partiallyPaid} partially paid · ${sum.paid} paid`, onClick: () => setStatus("open"), pressed: status === "open" }),
      e.jsx(StatCard, { label: "Paid", value: money(sum.paidTotal), sub: "Received against these invoices", onClick: () => setStatus("paid"), pressed: status === "paid" }),
      e.jsx(StatCard, { label: "Outstanding", value: money(sum.outstandingTotal), danger: sum.outstandingTotal > 0, sub: "Still to be paid", onClick: () => setStatus("open"), pressed: false })
    ] }) : null,
    e.jsxs("div", { className: "bf-toolbar", children: [
      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Quotation, reference or invoice no., customer…" })
    ] }),
    e.jsx("div", { style: { marginBottom: 12 }, children: e.jsx(Chips, { value: status, onChange: setStatus, options: FILTERS }) }),
    e.jsx("div", { className: "bf-panel", children: e.jsxs(e.Fragment, { children: [
      e.jsx(Table, { columns, rows: data?.items, loading, rowKey: (r) => r.quotationId, rowClass: (r) => (r.isOverdue ? "bf-row--overdue" : (r.paymentStatus === "Cancelled" ? "bf-row--muted" : "")),
        empty: e.jsx(EmptyState, { title: "No approved quotations found", description: "A quotation shows here once it is approved." }) }),
      e.jsx(Pager, { result: data, page, setPage })
    ] }) }),
    recording ? e.jsx(RecordPaymentDialog, { invoiceId: recording.invoiceId, onClose: () => setRecording(null), onDone: recorded }) : null
  ] });
}
