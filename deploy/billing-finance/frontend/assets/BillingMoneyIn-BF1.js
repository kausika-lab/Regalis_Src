/**
 * Billing & Finance -> MONEY IN (build doc §3, §7).
 *
 *   AdminInvoicesPageBF        /admin/billing/invoices          the list, now with Paid, Outstanding and Record payment
 *   AdminInvoiceDetailPage     /admin/billing/invoices/:id      one invoice: lines, payments, receipts, reminders
 *   AdminPaymentsPageBF        /admin/billing/payments          every payment whatever its mode; reverse with a reason
 *   AdminPartyLedgerPage       /admin/billing/ledger/:type/:id  a customer's or supplier's ledger
 *   AdminLedgersPage           /admin/billing/ledgers           outstanding and ageing, customers or suppliers
 *
 * Replaces AdminFinancePages-IT1.js for Invoices and Payments (the routes do not change).
 * Everything the old Invoices page did is kept: search, status, overdue, terms & conditions,
 * PDF, mark sent, cancel with a reason. Paid and Outstanding come from the server, which
 * moves them only through recorded payments.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file by
 * name (deploy/billing-finance/frontend/apply.mjs). A change ships under a NEW name (BF2).
 */
import {
  useToast, usePermission, React, e, Button, Select, Input, Modal, PageHeader, EmptyState, Spinner, useNavigate, useParams,
  api, money, fmtDate, fmtDateTime, StatCard, Tabs, Chips, SearchBox, Pager, Table, ReasonModal, Crumbs, pill, useStyles, useLoad,
  useDebounced, errorText, istToday, RecordPaymentDialog, LedgerPanel, MODE_TONE
} from "./BillingShared-BF1.js";

const INVOICE_STATUS = {
  1: ["Pending", "warning"], 2: ["Paid", "success"], 3: ["Part paid", "warning"], 4: ["Cancelled", "neutral"], 5: ["Refunded", "neutral"], 6: ["Sent", "info"]
};
const PAYMENT_STATUS = { 1: ["Pending", "warning"], 2: ["Success", "success"], 3: ["Failed", "danger"], 4: ["Refunded", "neutral"], 5: ["Cancelled", "neutral"] };
/** The same chip colours as the Record payment dialog. */
const MODE_TONE_BY_NAME = { Cash: "success", UPI: "info", "Bank transfer": "neutral", Cheque: "warning", Card: "info", Wallet: "warning", "TDS by customer": "neutral", "Short payment accepted": "danger" };
const invoiceStatus = (s) => pill(...(INVOICE_STATUS[s] ?? ["—", "neutral"]));
const pdfName = (number) => `${String(number ?? "invoice").replaceAll("/", "-")}.pdf`;

/** The newest successful receipt on an invoice, for "View receipt". */
async function openLatestReceipt(invoiceId, toast) {
  try {
    const payments = await api.get(`/invoices/${invoiceId}/payments`);
    const live = payments.filter((p) => p.paymentId && !p.isReversed && p.amount > 0);
    const last = live[live.length - 1];
    if (!last) { toast.error("This invoice was settled without a recorded receipt."); return; }
    await api.open(`/payments/${last.paymentId}/receipt`);
  } catch (error) {
    toast.error(errorText(error, "Could not open the receipt."));
  }
}

// ── Invoices ─────────────────────────────────────────────────────────────────

function TermsModal({ onClose }) {
  const toast = useToast();
  const [text, setText] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => { api.get("/invoices/terms").then((t) => setText(t.termsAndConditions ?? "")).catch(() => setText("")); }, []);
  const save = async () => {
    setBusy(true);
    try {
      await api.put("/invoices/terms", { termsAndConditions: text });
      toast.success("Terms saved.");
      onClose();
    } catch (error) {
      toast.error(errorText(error, "Could not save the terms."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: "Invoice terms & conditions", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: save, loading: busy, disabled: busy || text === null, children: "Save" })
    ] }),
    children: text === null ? e.jsx(Spinner, { center: true }) : e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "Printed at the end of every invoice PDF, for every customer. Invoices raised from a quotation print that quotation's own terms. Leave empty to print none." }),
      e.jsx("textarea", { className: "bf-textarea", style: { minHeight: 180 }, maxLength: 4000, value: text, onChange: (ev) => setText(ev.target.value) }),
      e.jsx("div", { className: "bf-sub", children: `${text.length} / 4000` })
    ] })
  });
}

function InvoiceActions({ inv, onRecord, onChanged }) {
  const toast = useToast();
  const navigate = useNavigate();
  const canEdit = usePermission("INVOICE.EDIT");
  const canPrint = usePermission("INVOICE.PRINT");
  const canRecord = usePermission("PAYMENT.RECORD");
  const [cancelling, setCancelling] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const cancelled = inv.status === 4 || inv.status === 5;
  const send = async () => {
    setBusy(true);
    try {
      await api.post(`/invoices/${inv.invoiceId}/send`);
      toast.success("Invoice marked as sent.");
      onChanged();
    } catch (error) {
      toast.error(errorText(error, "Could not update the invoice."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsxs("div", { className: "bf-actions", children: [
    !cancelled && inv.outstandingAmount > 0 && canRecord
      ? e.jsx(Button, { size: "sm", className: "btn--gold", onClick: () => onRecord(inv), children: "+ Record payment" }) : null,
    inv.status === 2 ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => openLatestReceipt(inv.invoiceId, toast), children: "View receipt" }) : null,
    e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => navigate(`/admin/billing/invoices/${inv.invoiceId}`), children: "View" }),
    canPrint ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => api.download(`/invoices/${inv.invoiceId}/pdf`, null, pdfName(inv.invoiceNumber)).catch((x) => toast.error(errorText(x, "Could not download the PDF."))), children: "PDF" }) : null,
    canEdit && inv.status === 1 ? e.jsx(Button, { size: "sm", variant: "ghost", loading: busy, disabled: busy, onClick: send, children: "Mark sent" }) : null,
    canEdit && !cancelled && inv.paidAmount === 0 ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setCancelling(true), children: "Cancel" }) : null,
    cancelling ? e.jsx(ReasonModal, {
      title: `Cancel ${inv.invoiceNumber}`,
      intro: "The invoice keeps its number and stays on the register — cancelling records why, it does not erase it. An invoice with payments against it cannot be cancelled.",
      confirmText: "Cancel invoice", danger: true,
      onClose: () => setCancelling(false),
      onSubmit: async (reason) => {
        await api.post(`/invoices/${inv.invoiceId}/cancel`, { reason });
        toast.success(`${inv.invoiceNumber} cancelled.`);
        setCancelling(false);
        onChanged();
      }
    }) : null
  ] });
}

export function AdminInvoicesPageBF() {
  useStyles();
  const navigate = useNavigate();
  const canEdit = usePermission("INVOICE.EDIT");
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [overdue, setOverdue] = React.useState(false);
  const [page, setPage] = React.useState(1);
  const [recording, setRecording] = React.useState(null);
  const [terms, setTerms] = React.useState(false);
  const debounced = useDebounced(search);
  React.useEffect(() => setPage(1), [debounced, status, overdue]);
  const { data, loading, reload } = useLoad(
    () => api.get("/invoices", { page, pageSize: 20, search: debounced.trim() || undefined, status: status || undefined, overdue: overdue || undefined }),
    [page, debounced, status, overdue]);
  const missingGstin = data?.items?.some((i) => !i.companyGstin);

  const columns = [
    { key: "number", header: "Invoice", render: (i) => e.jsxs("div", { children: [
      e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/invoices/${i.invoiceId}`), children: i.invoiceNumber }),
      e.jsx("div", { className: "bf-sub", children: `${i.customerName} · ${i.customerEmail}` })
    ] }) },
    { key: "source", header: "Source", render: (i) => (i.quotationNumber ?? (i.bookingId ? "Booking" : "—")) },
    { key: "date", header: "Date / Due", render: (i) => e.jsxs("div", { children: [
      e.jsx("div", { children: fmtDate(i.invoiceDate) }),
      e.jsx("div", { className: i.isOverdue ? "bf-sub bf-red" : "bf-sub", children: `Due ${fmtDate(i.dueDate)}` })
    ] }) },
    { key: "total", header: "Total", num: true, render: (i) => money(i.totalAmount) },
    { key: "paid", header: "Paid", num: true, render: (i) => money(i.paidAmount) },
    { key: "out", header: "Outstanding", num: true, render: (i) => (i.status === 4 ? "—" : e.jsx("span", { className: "bf-strong", children: money(i.outstandingAmount) })) },
    { key: "status", header: "Status", render: (i) => e.jsxs("div", { style: { display: "flex", gap: 6, flexWrap: "wrap" }, children: [invoiceStatus(i.status), i.isOverdue ? pill("Overdue", "danger") : null] }) },
    { key: "actions", header: "", render: (i) => e.jsx(InvoiceActions, { inv: i, onRecord: setRecording, onChanged: reload }) }
  ];

  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: "Invoices",
      description: "Every GST invoice issued by the system. An issued invoice is a snapshot — corrections are a cancellation, never an edit. Payments taken at the desk or by bank are recorded here.",
      actions: e.jsxs("div", { className: "bf-head-actions", children: [
        e.jsx(Button, { variant: "outline", onClick: () => navigate("/admin/billing/ledgers?type=customer"), children: "Outstanding & ageing" }),
        canEdit ? e.jsx(Button, { variant: "outline", onClick: () => setTerms(true), children: "Terms & Conditions" }) : null
      ] })
    }),
    missingGstin ? e.jsx("div", { className: "bf-note bf-note--danger", style: { marginBottom: 16 }, children:
      "Invoices on this page were issued without the company GSTIN. Every document headed TAX INVOICE must carry it, or the customer cannot claim input credit. Fill Company.Gstin, Company.Address and Company.Phone in Settings — new invoices pick them up straight away." }) : null,
    e.jsxs("div", { className: "bf-toolbar", children: [
      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Number, customer…" }),
      e.jsx(Select, { label: "Status", value: status, onChange: (ev) => setStatus(ev.target.value), options: [{ value: "", label: "Any status" },
        ...Object.entries(INVOICE_STATUS).map(([v, [l]]) => ({ value: v, label: l }))] }),
      e.jsx(Chips, { options: [{ value: false, label: "All" }, { value: true, label: "Overdue only" }], value: overdue, onChange: setOverdue })
    ] }),
    e.jsx("div", { className: "bf-panel", children: e.jsxs(e.Fragment, { children: [
      e.jsx(Table, { columns, rows: data?.items, loading, rowKey: (i) => i.invoiceId, rowClass: (i) => (i.isOverdue ? "bf-row--overdue" : ""),
        empty: e.jsx(EmptyState, { title: "No invoices found", description: "Try a different search or status." }) }),
      e.jsx(Pager, { result: data, page, setPage })
    ] }) }),
    recording ? e.jsx(RecordPaymentDialog, { invoiceId: recording.invoiceId, onClose: () => setRecording(null), onDone: () => { setRecording(null); reload(); } }) : null,
    terms ? e.jsx(TermsModal, { onClose: () => setTerms(false) }) : null
  ] });
}

// ── One invoice ──────────────────────────────────────────────────────────────

function PaymentsOnInvoice({ invoiceId, reloadKey, onChanged }) {
  const toast = useToast();
  const canReverse = usePermission("PAYMENT.REVERSE");
  const [reversing, setReversing] = React.useState(null);
  const { data, loading, reload } = useLoad(() => api.get(`/invoices/${invoiceId}/payments`), [invoiceId, reloadKey]);
  const columns = [
    { key: "date", header: "Date", render: (p) => fmtDate(p.paidOn) },
    { key: "receipt", header: "Receipt", render: (p) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: p.receiptNumber }), p.paymentNumber ? e.jsx("div", { className: "bf-sub", children: p.paymentNumber }) : null] }) },
    { key: "mode", header: "Mode", render: (p) => pill(p.modeName, MODE_TONE[p.mode] ?? "neutral") },
    { key: "ref", header: "Reference", render: (p) => e.jsxs("div", { children: [e.jsx("div", { children: p.reference ?? "—" }), p.bankAccountName ? e.jsx("div", { className: "bf-sub", children: p.bankAccountName }) : null] }) },
    { key: "amount", header: "Amount", num: true, render: (p) => e.jsx("span", { className: p.amount < 0 ? "bf-red" : "bf-strong", children: money(p.amount) }) },
    { key: "state", header: "", render: (p) => e.jsxs("div", { children: [
      p.isReversed ? pill("Reversed", "neutral") : p.reversalOfId ? pill("Reversal", "neutral") : p.isReconciled ? pill("Reconciled", "success") : null,
      p.reverseReason ? e.jsx("div", { className: "bf-sub", children: p.reverseReason }) : null,
      e.jsx("div", { className: "bf-sub", children: `${p.recordedBy ?? ""} · ${fmtDateTime(p.recordedUtc)}` })
    ] }) },
    { key: "actions", header: "", render: (p) => e.jsxs("div", { className: "bf-actions", children: [
      p.paymentId && !p.isReversed && p.amount > 0 ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => api.open(`/payments/${p.paymentId}/receipt`).catch((x) => toast.error(errorText(x, "Could not open the receipt."))), children: "Receipt" }) : null,
      canReverse && p.paymentId && !p.isReversed && !p.reversalOfId && !p.isReconciled ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setReversing(p), children: "Reverse" }) : null
    ] }) }
  ];
  return e.jsxs(e.Fragment, { children: [
    e.jsx(Table, { columns, rows: data, loading, rowKey: (p) => p.invoicePaymentId, rowClass: (p) => (p.isReversed || p.reversalOfId ? "bf-row--muted" : ""),
      empty: e.jsx(EmptyState, { title: "No payments recorded", description: "Gateway payments made when the booking or pass was bought show on the Payments page." }) }),
    reversing ? e.jsx(ReasonModal, {
      title: `Reverse ${reversing.receiptNumber}`,
      intro: `A payment is never edited. Reversing writes an opposite entry of ${money(-reversing.amount)} and leaves both on the record; the invoice goes back to owing ${money(reversing.amount)} more.`,
      confirmText: "Reverse payment", danger: true,
      onClose: () => setReversing(null),
      onSubmit: async (reason) => {
        await api.post(`/payments/${reversing.paymentId}/reverse`, { reason });
        toast.success(`${reversing.receiptNumber} reversed.`);
        setReversing(null);
        reload();
        onChanged();
      }
    }) : null
  ] });
}

export function AdminInvoiceDetailPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const { id } = useParams();
  const canRecord = usePermission("PAYMENT.RECORD");
  const canPrint = usePermission("INVOICE.PRINT");
  const seesReminders = usePermission("COMPLIANCE.VIEW");
  const [recording, setRecording] = React.useState(false);
  const [tick, setTick] = React.useState(0);
  const [tab, setTab] = React.useState("payments");
  const { data: inv, loading, reload } = useLoad(() => api.get(`/invoices/${id}`), [id, tick]);
  const { data: reminders } = useLoad(() => (seesReminders ? api.get("/reminders/log", { documentType: "Invoice", documentId: id }) : Promise.resolve([])), [id, seesReminders]);
  if (!inv) return loading ? e.jsx(Spinner, { center: true }) : e.jsx(EmptyState, { title: "Invoice not found" });
  const cancelled = inv.status === 4 || inv.status === 5;
  return e.jsxs("div", { children: [
    e.jsx(Crumbs, { items: [{ label: "Billing & Finance" }, { label: "Invoices", to: "/admin/billing/invoices" }, { label: inv.invoiceNumber }] }),
    e.jsx(PageHeader, {
      title: `Invoice ${inv.invoiceNumber} — ${inv.customerName}`,
      description: `Dated ${fmtDate(inv.invoiceDate)} · due ${fmtDate(inv.dueDate)}${inv.quotationNumber ? ` · from ${inv.quotationNumber}` : ""}${inv.companyGstin ? ` · seller GSTIN ${inv.companyGstin}` : " · issued without the company GSTIN"}`,
      actions: e.jsxs("div", { className: "bf-head-actions", children: [
        invoiceStatus(inv.status), inv.isOverdue ? pill("Overdue", "danger") : null,
        canPrint ? e.jsx(Button, { variant: "outline", onClick: () => api.download(`/invoices/${inv.invoiceId}/pdf`, null, pdfName(inv.invoiceNumber)).catch((x) => toast.error(errorText(x, "Could not download the PDF."))), children: "PDF" }) : null,
        e.jsx(Button, { variant: "outline", onClick: () => navigate(`/admin/billing/ledger/customer/${inv.customerId}`), children: "Customer ledger" }),
        !cancelled && inv.outstandingAmount > 0 && canRecord ? e.jsx(Button, { className: "btn--gold", onClick: () => setRecording(true), children: "+ Record payment" }) : null
      ] })
    }),
    e.jsxs("div", { className: "bf-cards", children: [
      e.jsx(StatCard, { label: "Invoice total", value: money(inv.totalAmount), sub: `Taxable ${money(inv.taxableAmount)} · GST ${money(inv.cgstAmount + inv.sgstAmount + inv.igstAmount)}` }),
      e.jsx(StatCard, { label: "Paid", value: money(inv.paidAmount), sub: inv.paidUtc ? `Settled ${fmtDate(inv.paidUtc)}` : "Summed from the payments below" }),
      e.jsx(StatCard, { label: "Outstanding", value: cancelled ? "—" : money(inv.outstandingAmount), danger: inv.isOverdue, sub: cancelled ? inv.cancellationReason : inv.isOverdue ? "Past its due date" : `Due ${fmtDate(inv.dueDate)}` })
    ] }),
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [{ value: "payments", label: "Payments" }, { value: "lines", label: "Lines" }, { value: "reminders", label: "Reminders" }] }),
    e.jsx("div", { className: "bf-panel", children: tab === "payments"
      ? e.jsx(PaymentsOnInvoice, { invoiceId: inv.invoiceId, reloadKey: tick, onChanged: () => setTick((t) => t + 1) })
      : tab === "lines"
        ? e.jsx(Table, { rowKey: (l) => l.lineNumber, rows: inv.lines, columns: [
          { key: "n", header: "#", render: (l) => l.lineNumber },
          { key: "d", header: "Description", render: (l) => e.jsxs("div", { children: [e.jsx("div", { children: l.description }), l.hsnSacCode ? e.jsx("div", { className: "bf-sub", children: `HSN/SAC ${l.hsnSacCode}` }) : null] }) },
          { key: "q", header: "Qty", num: true, render: (l) => l.quantity },
          { key: "r", header: "Rate", num: true, render: (l) => money(l.unitPrice) },
          { key: "a", header: "Amount", num: true, render: (l) => money(l.lineAmount) },
          { key: "t", header: "GST", num: true, render: (l) => `${l.taxPercent}% · ${money(l.taxAmount)}` },
          { key: "x", header: "Total", num: true, render: (l) => money(l.totalAmount) }
        ] })
        : e.jsxs(e.Fragment, { children: [
          e.jsx("p", { className: "bf-sub", style: { marginTop: 0 }, children: "Every reminder sent about this invoice: what, to whom, when, and whether it was delivered — so \"we never received it\" can be answered." }),
          e.jsx(Table, { rows: reminders, rowKey: (r) => r.reminderLogId, columns: [
            { key: "w", header: "Sent", render: (r) => fmtDateTime(r.sentUtc) },
            { key: "t", header: "Reminder", render: (r) => r.reminderType },
            { key: "to", header: "To", render: (r) => `${r.recipient} · ${r.channel}` },
            { key: "d", header: "Delivered", render: (r) => (r.delivered ? pill("Delivered", "success") : e.jsxs("div", { children: [pill("Not delivered", "danger"), e.jsx("div", { className: "bf-sub", children: r.error })] })) }
          ], empty: e.jsx(EmptyState, { title: "No reminders sent", description: seesReminders ? "Reminders are switched on in Settings once email from production is proven." : "You cannot see the reminder log." }) })
        ] }) }),
    recording ? e.jsx(RecordPaymentDialog, { invoiceId: inv.invoiceId, onClose: () => setRecording(false), onDone: () => { setRecording(false); setTick((t) => t + 1); reload(); } }) : null
  ] });
}

// ── Payments ─────────────────────────────────────────────────────────────────

export function AdminPaymentsPageBF() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [source, setSource] = React.useState("all");
  const [mode, setMode] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [reversing, setReversing] = React.useState(null);
  const debounced = useDebounced(search);
  React.useEffect(() => setPage(1), [debounced, status, source, mode, from, to]);
  const { data, loading, reload } = useLoad(() => api.get("/payments/register", {
    page, pageSize: 20, search: debounced.trim() || undefined, status: status || undefined, source, mode: mode || undefined, from: from || undefined, to: to || undefined
  }), [page, debounced, status, source, mode, from, to]);

  const email = async (p) => {
    try {
      const r = await api.postWithMessage(`/payments/${p.paymentId}/receipt/email`);
      toast.success(r.data || r.message || "Receipt emailed.");
    } catch (error) {
      toast.error(errorText(error, "Could not email the receipt."));
    }
  };

  const columns = [
    { key: "payment", header: "Payment", render: (p) => e.jsxs("div", { children: [
      e.jsx("div", { className: "bf-strong", children: p.receiptNumber ?? p.paymentNumber }),
      e.jsx("div", { className: "bf-sub", children: `${p.receiptNumber ? `${p.paymentNumber} · ` : ""}${p.customerName}` })
    ] }) },
    { key: "mode", header: "Mode", render: (p) => pill(p.modeName, MODE_TONE_BY_NAME[p.modeName] ?? (p.isOffline ? "neutral" : "info")) },
    { key: "ref", header: "Reference", render: (p) => e.jsxs("div", { children: [e.jsx("div", { children: p.reference ?? "—" }), p.gateway && p.gateway !== "Offline" ? e.jsx("div", { className: "bf-sub", children: p.gateway }) : null] }) },
    { key: "against", header: "Against", render: (p) => (p.invoiceId
      ? e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/invoices/${p.invoiceId}`), children: p.invoiceNumber })
      : e.jsx("span", { className: "bf-sub", children: "—" })) },
    { key: "amount", header: "Amount", num: true, render: (p) => e.jsx("span", { className: "bf-strong", children: money(p.amount) }) },
    { key: "status", header: "Status", render: (p) => e.jsxs("div", { style: { display: "flex", gap: 6, flexWrap: "wrap" }, children: [
      pill(...(PAYMENT_STATUS[p.status] ?? ["—", "neutral"])),
      p.isReversed ? pill("Reversed", "neutral") : null,
      p.isReconciled ? pill("Reconciled", "success") : null
    ] }) },
    { key: "when", header: "Paid (IST)", render: (p) => e.jsxs("div", { children: [e.jsx("div", { children: p.paidUtc ? fmtDateTime(p.paidUtc) : "—" }), p.reverseReason ? e.jsx("div", { className: "bf-sub", children: `Reversed: ${p.reverseReason}` }) : null] }) },
    { key: "actions", header: "", render: (p) => e.jsxs("div", { className: "bf-actions", children: [
      p.status === 2 ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => api.open(`/payments/${p.paymentId}/receipt`).catch((x) => toast.error(errorText(x, "Could not open the receipt."))), children: "Receipt" }) : null,
      p.status === 2 ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => email(p), children: "Email" }) : null,
      p.canReverse ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setReversing(p), children: "Reverse" }) : null
    ] }) }
  ];

  return e.jsxs("div", { children: [
    e.jsx(PageHeader, { title: "Payments", description: "Every payment, whatever its mode — gateway, wallet, cash, UPI, bank transfer, cheque or card — with the receipt and the invoice it settled. A payment is never edited: a mistake is reversed, with a reason." }),
    e.jsxs("div", { className: "bf-toolbar", children: [
      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Payment, receipt, customer, invoice, UTR…" }),
      e.jsx(Select, { label: "Status", value: status, onChange: (ev) => setStatus(ev.target.value), options: [{ value: "", label: "Any status" }, ...Object.entries(PAYMENT_STATUS).map(([v, [l]]) => ({ value: v, label: l }))] }),
      e.jsx(Select, { label: "Mode (recorded)", value: mode, onChange: (ev) => setMode(ev.target.value), options: [{ value: "", label: "Any mode" },
        { value: "1", label: "Cash" }, { value: "2", label: "UPI" }, { value: "3", label: "Bank transfer" }, { value: "4", label: "Cheque" }, { value: "5", label: "Card" }, { value: "6", label: "Wallet" }, { value: "7", label: "TDS by customer" }, { value: "8", label: "Short payment accepted" }] }),
      e.jsx(Input, { label: "From", type: "date", value: from, max: istToday(), onChange: (ev) => setFrom(ev.target.value) }),
      e.jsx(Input, { label: "To", type: "date", value: to, max: istToday(), onChange: (ev) => setTo(ev.target.value) })
    ] }),
    e.jsx("div", { style: { marginBottom: 12 }, children: e.jsx(Chips, { value: source, onChange: setSource, options: [{ value: "all", label: "All" }, { value: "online", label: "Online gateway" }, { value: "offline", label: "Offline" }] }) }),
    e.jsx("div", { className: "bf-panel", children: e.jsxs(e.Fragment, { children: [
      e.jsx(Table, { columns, rows: data?.items, loading, rowKey: (p) => p.paymentId, rowClass: (p) => (p.isReversed || p.status === 4 ? "bf-row--muted" : ""),
        empty: e.jsx(EmptyState, { title: "No payments found" }) }),
      e.jsx(Pager, { result: data, page, setPage })
    ] }) }),
    reversing ? e.jsx(ReasonModal, {
      title: `Reverse ${reversing.receiptNumber ?? reversing.paymentNumber}`,
      intro: `A payment is never edited. Reversing writes an opposite entry of ${money(-reversing.amount)} against ${reversing.invoiceNumber}, keeps both on the record${reversing.modeName === "Wallet" ? ", and puts the money back in the wallet" : ""}.`,
      confirmText: "Reverse payment", danger: true,
      onClose: () => setReversing(null),
      onSubmit: async (reason) => {
        await api.post(`/payments/${reversing.paymentId}/reverse`, { reason });
        toast.success("Payment reversed.");
        setReversing(null);
        reload();
      }
    }) : null
  ] });
}

// ── Ledgers ──────────────────────────────────────────────────────────────────

function CustomerTaxCard({ customerId }) {
  const toast = useToast();
  const canEdit = usePermission("CUSTOMER.EDIT");
  const { data, reload } = useLoad(() => api.get(`/customers/${customerId}/tax-profile`), [customerId]);
  const [tan, setTan] = React.useState("");
  const [chase, setChase] = React.useState(false);
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => { if (data) { setTan(data.tan ?? ""); setChase(data.doNotChase); setReason(data.doNotChaseReason ?? ""); } }, [data]);
  if (!data) return null;
  const save = async () => {
    setBusy(true);
    try {
      await api.put(`/customers/${customerId}/tax-profile`, { tan: tan.trim() || null, doNotChase: chase, doNotChaseReason: chase ? reason.trim() : null });
      toast.success("Saved.");
      reload();
    } catch (error) {
      toast.error(errorText(error, "Could not save."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsxs("div", { className: "bf-panel", children: [
    e.jsx("h3", { className: "bf-panel__title", children: "Tax and chasing" }),
    e.jsxs("div", { className: "bf-form-grid", children: [
      e.jsx(Input, { label: "TAN", maxLength: 10, disabled: !canEdit, value: tan, hint: "Needed only where the customer deducts TDS. Four letters, five digits, one letter.", onChange: (ev) => setTan(ev.target.value.toUpperCase()) }),
      e.jsxs("label", { style: { display: "flex", gap: 8, alignItems: "center", alignSelf: "center" }, children: [
        e.jsx("input", { type: "checkbox", disabled: !canEdit, checked: chase, onChange: (ev) => setChase(ev.target.checked) }),
        "Do not chase (an open dispute)"
      ] }),
      chase ? e.jsx(Input, { label: "Why", required: true, disabled: !canEdit, maxLength: 300, value: reason, onChange: (ev) => setReason(ev.target.value) }) : null
    ] }),
    canEdit ? e.jsx("div", { style: { marginTop: 12 }, children: e.jsx(Button, { size: "sm", onClick: save, loading: busy, disabled: busy, children: "Save" }) }) : null
  ] });
}

export function AdminPartyLedgerPage() {
  useStyles();
  const navigate = useNavigate();
  const { type, id } = useParams();
  const supplier = type === "supplier";
  const [tab, setTab] = React.useState("ledger");
  const docs = useLoad(() => (tab === "documents"
    ? (supplier ? api.get("/purchase-bills", { supplierId: id, pageSize: 100 }).then((r) => r.page.items) : api.get("/invoices", { customerId: id, pageSize: 100 }).then((r) => r.items))
    : Promise.resolve(null)), [tab, id, supplier]);
  return e.jsxs("div", { children: [
    e.jsx(Crumbs, { items: [{ label: "Billing & Finance" }, supplier ? { label: "Suppliers", to: "/admin/billing/suppliers" } : { label: "Invoices", to: "/admin/billing/invoices" },
      { label: "Ledgers", to: `/admin/billing/ledgers?type=${type}` }, { label: "Ledger" }] }),
    e.jsx(PageHeader, {
      title: supplier ? "Supplier ledger" : "Customer ledger",
      description: supplier ? "What Regalis owes this supplier, document by document, with a running balance." : "What this customer owes Regalis, document by document, with a running balance.",
      actions: e.jsx(Button, { variant: "outline", onClick: () => navigate(`/admin/billing/ledgers?type=${type}`), children: "Outstanding & ageing" })
    }),
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [{ value: "ledger", label: "Ledger" }, { value: "documents", label: supplier ? "Bills" : "Invoices" }] }),
    tab === "ledger"
      ? e.jsxs(e.Fragment, { children: [e.jsx(LedgerPanel, { partyType: supplier ? "supplier" : "customer", partyId: id }), !supplier ? e.jsx(CustomerTaxCard, { customerId: id }) : null] })
      : e.jsx("div", { className: "bf-panel", children: e.jsx(Table, { rows: docs.data, loading: docs.loading,
        rowKey: (d) => d.purchaseBillId ?? d.invoiceId,
        onRowClick: (d) => navigate(supplier ? `/admin/billing/purchase-bills/${d.purchaseBillId}` : `/admin/billing/invoices/${d.invoiceId}`),
        columns: supplier ? [
          { key: "n", header: "Bill", render: (b) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: b.supplierBillNo }), e.jsx("div", { className: "bf-sub", children: fmtDate(b.billDate) })] }) },
          { key: "d", header: "Due", render: (b) => e.jsx("span", { className: b.isOverdue ? "bf-red" : "", children: fmtDate(b.dueDate) }) },
          { key: "t", header: "Total", num: true, render: (b) => money(b.billTotal) },
          { key: "p", header: "Paid", num: true, render: (b) => money(b.paid) },
          { key: "b", header: "Balance", num: true, render: (b) => e.jsx("span", { className: "bf-strong", children: money(b.balance) }) }
        ] : [
          { key: "n", header: "Invoice", render: (i) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: i.invoiceNumber }), e.jsx("div", { className: "bf-sub", children: fmtDate(i.invoiceDate) })] }) },
          { key: "d", header: "Due", render: (i) => e.jsx("span", { className: i.isOverdue ? "bf-red" : "", children: fmtDate(i.dueDate) }) },
          { key: "t", header: "Total", num: true, render: (i) => money(i.totalAmount) },
          { key: "p", header: "Paid", num: true, render: (i) => money(i.paidAmount) },
          { key: "b", header: "Outstanding", num: true, render: (i) => e.jsx("span", { className: "bf-strong", children: i.status === 4 ? "—" : money(i.outstandingAmount) }) },
          { key: "s", header: "Status", render: (i) => invoiceStatus(i.status) }
        ],
        empty: e.jsx(EmptyState, { title: supplier ? "No bills from this supplier" : "No invoices for this customer" }) }) })
  ] });
}

const BUCKETS = [["notDue", "not-due", "Not due"], ["d1To30", "1-30", "1–30 days"], ["d31To60", "31-60", "31–60"], ["d61To90", "61-90", "61–90"], ["over90", "90+", "90+"], ["total", "total", "Total"]];

function AgeingDocuments({ type, partyId, bucket, title, onClose }) {
  const navigate = useNavigate();
  const { data, loading } = useLoad(() => api.get(`/parties/${type}/ageing/documents`, { partyId: partyId || undefined, bucket }), [type, partyId, bucket]);
  return e.jsx(Modal, { open: true, title, onClose, children: e.jsx("div", { className: "bf-modal--wide", children: e.jsx(Table, { rows: data, loading, rowKey: (d) => `${d.docType}-${d.docId}`,
    onRowClick: (d) => navigate(d.docType === "bill" ? `/admin/billing/purchase-bills/${d.docId}` : `/admin/billing/invoices/${d.docId}`),
    columns: [
      { key: "n", header: "Document", render: (d) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: d.number }), e.jsx("div", { className: "bf-sub", children: d.partyName })] }) },
      { key: "dt", header: "Dated", render: (d) => fmtDate(d.documentDate) },
      { key: "due", header: "Due", render: (d) => e.jsxs("div", { children: [e.jsx("div", { children: fmtDate(d.dueDate) }), d.daysOverdue ? e.jsx("div", { className: "bf-sub bf-red", children: `${d.daysOverdue} days late` }) : null] }) },
      { key: "t", header: "Total", num: true, render: (d) => money(d.total) },
      { key: "b", header: "Balance", num: true, render: (d) => e.jsx("span", { className: "bf-strong", children: money(d.balance) }) }
    ], empty: e.jsx(EmptyState, { title: "Nothing in this bucket" }) }) }) });
}

export function AdminLedgersPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const initial = new URLSearchParams(window.location.search).get("type") === "supplier" ? "supplier" : "customer";
  const [type, setType] = React.useState(initial);
  const [tab, setTab] = React.useState("outstanding");
  const [sort, setSort] = React.useState("balance");
  const [expanded, setExpanded] = React.useState(false);
  const [drill, setDrill] = React.useState(null);
  const [search, setSearch] = React.useState("");
  const debounced = useDebounced(search);
  const canCustomers = usePermission("INVOICE.VIEW");
  const canSuppliers = usePermission("BILL.VIEW");
  const outstanding = useLoad(() => (tab === "outstanding" ? api.get(`/parties/${type}/outstanding`) : Promise.resolve(null)), [tab, type]);
  const ageing = useLoad(() => (tab === "ageing" ? api.get(`/parties/${type}/ageing`) : Promise.resolve(null)), [tab, type]);
  const parties = useLoad(() => (tab === "find" ? api.get(`/parties/${type}/search`, { q: debounced.trim() || undefined }) : Promise.resolve(null)), [tab, type, debounced]);
  const label = type === "supplier" ? "Supplier" : "Customer";

  const sorted = outstanding.data ? [...outstanding.data].sort((a, b) => (sort === "age" ? b.oldestDays - a.oldestDays : b.balance - a.balance)) : null;
  const exportOutstanding = () => {
    const rows = [[label, "Total", "Documents", "Oldest unpaid", "Advance", "Balance"],
      ...(sorted ?? []).map((p) => [p.partyName, p.total, p.documents, p.oldestUnpaid ?? "", p.advance, p.balance])];
    const csv = rows.map((r) => r.map((c) => `"${String(c).replaceAll('"', '""')}"`).join(",")).join("\r\n");
    const href = URL.createObjectURL(new Blob([csv], { type: "text/csv" }));
    const a = document.createElement("a");
    a.href = href; a.download = `outstanding-${type}.csv`; a.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  };

  let ageingRows = ageing.data?.rows ?? null;
  let rest = null;
  if (ageingRows && ageingRows.length > 10 && !expanded) {
    const others = ageingRows.slice(10);
    rest = others.reduce((acc, r) => { for (const [k] of BUCKETS) acc[k] += r[k]; return acc; },
      { partyId: 0, partyName: `Everyone else (${others.length})`, notDue: 0, d1To30: 0, d31To60: 0, d61To90: 0, over90: 0, total: 0, rest: true });
    ageingRows = [...ageingRows.slice(0, 10), rest];
  }

  return e.jsxs("div", { children: [
    e.jsx(PageHeader, { title: "Ledgers, outstanding and ageing",
      description: "Three views of the same data, for customers and suppliers alike. Nothing is stored: every figure is worked out from the documents each time. Ageing counts from each document's own due date, not its date." }),
    e.jsx(Chips, { value: type, onChange: (v) => { setType(v); setExpanded(false); }, options: [
      { value: "customer", label: "Customers", disabled: !canCustomers }, { value: "supplier", label: "Suppliers", disabled: !canSuppliers }] }),
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [{ value: "outstanding", label: "Outstanding" }, { value: "ageing", label: "Ageing" }, { value: "find", label: `Find a ${type}` }] }),
    tab === "outstanding" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsxs("div", { className: "bf-toolbar", style: { justifyContent: "space-between" }, children: [
        e.jsx(Chips, { value: sort, onChange: setSort, options: [{ value: "balance", label: "Largest balance" }, { value: "age", label: "Oldest first" }] }),
        e.jsx("div", { style: { flex: "0 0 auto" }, children: e.jsx(Button, { variant: "outline", onClick: exportOutstanding, children: "Excel (CSV)" }) })
      ] }),
      e.jsx(Table, { rows: sorted, loading: outstanding.loading, rowKey: (p) => p.partyId, onRowClick: (p) => navigate(`/admin/billing/ledger/${type}/${p.partyId}`), columns: [
        { key: "p", header: label, render: (p) => e.jsx("span", { className: "bf-strong", children: p.partyName }) },
        { key: "t", header: "Total", num: true, render: (p) => money(p.total) },
        { key: "n", header: "Documents", num: true, render: (p) => p.documents },
        { key: "o", header: "Oldest unpaid", render: (p) => (p.oldestUnpaid ? `${fmtDate(p.oldestUnpaid)} · ${p.oldestDays} days` : "—") },
        { key: "a", header: type === "supplier" ? "Advance held" : "", num: true, render: (p) => (p.advance ? money(p.advance) : "") },
        { key: "b", header: "Balance", num: true, render: (p) => e.jsx("span", { className: "bf-strong", children: money(p.balance) }) }
      ], empty: e.jsx(EmptyState, { title: "Nobody owes anything", description: "A party with a zero balance is not listed." }) })
    ] }) : null,
    tab === "ageing" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsxs("div", { className: "bf-toolbar", style: { justifyContent: "flex-end" }, children: [
        e.jsx("div", { style: { flex: "0 0 auto" }, children: e.jsx(Button, { variant: "outline", onClick: () => api.download(`/parties/${type}/ageing/export`, null, `ageing-${type}.xlsx`).catch((x) => toast.error(errorText(x, "Download failed."))), children: "Excel" }) })
      ] }),
      e.jsx(Table, { rows: ageingRows, loading: ageing.loading, rowKey: (r) => r.partyId,
        rowClass: (r) => (r.d61To90 > 0 || r.over90 > 0 ? "bf-row--tint" : ""),
        columns: [
          { key: "p", header: label, render: (r) => (r.rest
            ? e.jsx("button", { type: "button", className: "bf-link", onClick: () => setExpanded(true), children: r.partyName })
            : e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/ledger/${type}/${r.partyId}`), children: r.partyName })) },
          ...BUCKETS.map(([k, bucket, head]) => ({ key: k, header: head, num: true, render: (r) => (r[k]
            ? (r.rest ? money(r[k]) : e.jsx("button", { type: "button", className: k === "total" ? "bf-link bf-strong" : "bf-link", onClick: () => setDrill({ partyId: r.partyId, bucket, title: `${r.partyName} · ${head}` }), children: money(r[k]) }))
            : "") }))
        ],
        footer: ageing.data ? e.jsxs("tr", { children: [e.jsx("td", { children: "Total" }), ...BUCKETS.map(([k, bucket, head]) => e.jsx("td", { className: "bf-num", children: ageing.data.totals[k]
          ? e.jsx("button", { type: "button", className: "bf-link", onClick: () => setDrill({ partyId: null, bucket, title: `Everyone · ${head}` }), children: money(ageing.data.totals[k]) }) : "" }, k))] }) : null,
        empty: e.jsx(EmptyState, { title: "Nothing outstanding" }) }),
      e.jsx("p", { className: "bf-sub", children: "Buckets are counted from each document's own due date. A bill dated 1 September on Net 60 terms is not overdue in October." })
    ] }) : null,
    tab === "find" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsx("div", { className: "bf-toolbar", children: e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: type === "supplier" ? "Supplier name or code" : "Customer name, email or mobile" }) }),
      e.jsx(Table, { rows: parties.data, loading: parties.loading, rowKey: (p) => p.partyId, onRowClick: (p) => navigate(`/admin/billing/ledger/${type}/${p.partyId}`), columns: [
        { key: "n", header: label, render: (p) => e.jsx("span", { className: "bf-strong", children: p.name }) },
        { key: "d", header: "", render: (p) => e.jsx("span", { className: "bf-sub", children: p.detail }) }
      ], empty: e.jsx(EmptyState, { title: "No match" }) })
    ] }) : null,
    drill ? e.jsx(AgeingDocuments, { type, partyId: drill.partyId, bucket: drill.bucket, title: drill.title, onClose: () => setDrill(null) }) : null
  ] });
}
