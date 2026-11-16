/**
 * Billing & Finance build (Nov 2026) - the COMPLIANCE and CASH DESK screens.
 *
 *   AdminGstPage           /admin/billing/gst            This month, registers, GSTR-2B, returns (doc 11)
 *   AdminTdsPage           /admin/billing/tds            Payable, receivable, challans, certificates (doc 10)
 *   AdminCompliancePage    /admin/billing/compliance     Statutory dates with live amounts (doc 13.1, 13.2)
 *   AdminPeriodLocksPage   /admin/billing/period-locks   Lock a filed month (doc 13.3)
 *   AdminPettyCashPage     /admin/billing/petty-cash     The float: top-ups, cash out, counts (doc 12.3)
 *   AccountingExportDialog                               One workbook for the accountant (doc 20)
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports these by name
 * (deploy/billing-finance/frontend/apply.mjs). A change ships under a NEW file name (BF2).
 */
import {
  React, e, Button, Select, Input, Modal, PageHeader, EmptyState, Spinner, useToast, usePermission, useNavigate,
  api, money, fmtDate, fmtDateTime, StatCard, Tabs, Pager, Table, ReasonModal, DropZone, pill, useStyles, useLoad,
  amountOf, errorText, istToday, monthOptions, monthLabel, thisPeriod, fyOptions, currentFy, IconDownload
} from "./BillingShared-BF1.js";

const XLSX_FAIL = "The download did not work.";
const downloadOr = (toast) => (promise) => promise.catch((x) => toast.error(errorText(x, XLSX_FAIL)));
const head = (children) => e.jsx("div", { className: "bf-head-actions", children });
const note = (tone, children) => e.jsx("div", { className: `bf-note bf-note--${tone}`, style: { margin: "12px 0" }, children });

// ════════════════════════════════════════════════════════════════════════════
// GST (doc 11)
// ════════════════════════════════════════════════════════════════════════════

function TaxCard({ title, totals, total, sub }) {
  return e.jsxs("div", { className: "bf-panel", style: { margin: 0 }, children: [
    e.jsx("h3", { className: "bf-panel__title", children: title }),
    e.jsxs("div", { className: "bf-sum", children: [
      e.jsxs("div", { children: [e.jsx("span", { children: "CGST" }), e.jsx("span", { children: money(totals.cgst) })] }),
      e.jsxs("div", { children: [e.jsx("span", { children: "SGST" }), e.jsx("span", { children: money(totals.sgst) })] }),
      e.jsxs("div", { children: [e.jsx("span", { children: "IGST" }), e.jsx("span", { children: money(totals.igst) })] }),
      e.jsxs("div", { className: "bf-sum__total", children: [e.jsx("span", { children: total }), e.jsx("span", { children: money(totals.total) })] })
    ] }),
    sub ? e.jsx("p", { className: "bf-sub", children: sub }) : null
  ] });
}

function GstThisMonth({ period, goTo2b }) {
  const { data, loading } = useLoad(() => api.get("/gst/summary", { period }), [period]);
  if (!data) return loading ? e.jsx(Spinner, { center: true }) : null;
  return e.jsxs("div", { children: [
    data.companyGstin ? null : note("danger", "Company GSTIN is empty in Settings. Every TAX INVOICE goes out without a seller GSTIN — fill Company.Gstin, Company.Address and Company.Phone."),
    e.jsxs("div", { className: "bf-cards", style: { gridTemplateColumns: "repeat(auto-fit,minmax(240px,1fr))" }, children: [
      e.jsx(TaxCard, { title: "Output tax — on sales", totals: data.output, total: "Total collected", sub: `${data.invoiceCount} invoices` }),
      e.jsx(TaxCard, { title: "Input credit — on purchases", totals: data.input, total: "Total claimable", sub: `${data.billCount} bills with a supplier GSTIN` }),
      e.jsxs("div", { className: "bf-panel", style: { margin: 0 }, children: [
        e.jsx("h3", { className: "bf-panel__title", children: `Cash to pay by ${fmtDate(data.paymentDue)}` }),
        e.jsx("div", { className: "bf-card__value", style: { fontSize: "var(--font-size-3xl, 2rem)" }, children: money(data.cashToPay) }),
        e.jsx("p", { className: "bf-sub", children: `${money(data.output.total)} collected less ${money(data.claimable)} claimable` }),
        data.heldBack > 0 ? e.jsxs("p", { className: "bf-red", style: { fontSize: "var(--font-size-sm)" }, children: [
          `${money(data.heldBack)} of credit is held back — those bills are not in GSTR-2B yet. `,
          e.jsx("button", { type: "button", className: "bf-link", onClick: goTo2b, children: "See below." })
        ] }) : null,
        data.creditCarriedForward > 0 ? e.jsx("p", { className: "bf-sub", children: `${money(data.creditCarriedForward)} of credit carries forward to next month.` }) : null,
        data.has2b ? null : e.jsx("p", { className: "bf-sub", children: "GSTR-2B for this month has not been uploaded, so all input credit is shown as claimable." })
      ] })
    ] }),
    e.jsx("p", { className: "bf-sub", children: "This page produces the numbers and the Excel; the accountant files. There is no connection to the GST portal, no e-invoice and no e-way bill." })
  ] });
}

function RateTable({ reg, docsLabel }) {
  const rateText = (r) => (r === 0 ? "Exempt / nil" : `${r}%`);
  return e.jsx(Table, {
    rows: reg.byRate,
    rowKey: (r) => r.rate,
    columns: [
      { key: "rate", header: "GST rate", render: (r) => rateText(r.rate) },
      { key: "tx", header: "Taxable value", num: true, render: (r) => money(r.taxable) },
      { key: "c", header: "CGST", num: true, render: (r) => money(r.cgst) },
      { key: "s", header: "SGST", num: true, render: (r) => money(r.sgst) },
      { key: "i", header: "IGST", num: true, render: (r) => money(r.igst) },
      { key: "n", header: docsLabel, num: true, render: (r) => r.documents }
    ],
    footer: e.jsxs("tr", { children: [
      e.jsx("td", { children: "Total" }),
      e.jsx("td", { className: "bf-num", children: money(reg.total.taxable) }),
      e.jsx("td", { className: "bf-num", children: money(reg.total.cgst) }),
      e.jsx("td", { className: "bf-num", children: money(reg.total.sgst) }),
      e.jsx("td", { className: "bf-num", children: money(reg.total.igst) }),
      e.jsx("td", { className: "bf-num", children: reg.total.documents })
    ] }),
    empty: e.jsx(EmptyState, { title: "Nothing in this month" })
  });
}

function GstRegister({ kind, period }) {
  const toast = useToast();
  const navigate = useNavigate();
  const sales = kind === "sales";
  const { data, loading } = useLoad(() => api.get(sales ? "/gst/sales-register" : "/gst/purchase-register", { period }), [kind, period]);
  if (!data) return loading ? e.jsx(Spinner, { center: true }) : null;
  const twoBTone = { Matches: "success", "Missing in 2B": "danger", Differs: "warning", "Bill missing here": "info" };
  return e.jsxs("div", { children: [
    e.jsxs("div", { style: { display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, flexWrap: "wrap" }, children: [
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: sales
        ? "Every invoice raised in the month, split by rate. This side already worked before this build."
        : "The GST on every approved bill with a supplier GSTIN, in the shape GSTR-3B asks for, so the accountant copies figures rather than deriving them." }),
      e.jsx(Button, { variant: "outline", onClick: () => downloadOr(toast)(api.download(`/gst/${kind}-register/export`, { period }, `gst-${kind}-register-${period}.xlsx`)),
        children: [e.jsx(IconDownload, { size: 16 }, "i"), " Excel"] })
    ] }),
    e.jsx("div", { style: { height: 12 } }),
    e.jsx(RateTable, { reg: data, docsLabel: sales ? "Invoices" : "Bills" }),
    e.jsx("h3", { className: "bf-panel__title", style: { marginTop: 24 }, children: sales ? "Invoices" : "Bills" }),
    sales
      ? e.jsx(Table, { rows: data.sales, rowKey: (r) => r.invoiceId, columns: [
        { key: "n", header: "Invoice", render: (r) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: r.invoiceNumber }), e.jsx("div", { className: "bf-sub", children: fmtDate(r.invoiceDate) })] }) },
        { key: "c", header: "Customer", render: (r) => e.jsxs("div", { children: [r.customerName, e.jsx("div", { className: "bf-sub", children: r.customerGstin ?? "No GSTIN" })] }) },
        { key: "r", header: "Rate", num: true, render: (r) => `${r.rate}%` },
        { key: "t", header: "Taxable", num: true, render: (r) => money(r.taxable) },
        { key: "g", header: "CGST / SGST / IGST", num: true, render: (r) => `${money(r.cgst)} / ${money(r.sgst)} / ${money(r.igst)}` },
        { key: "x", header: "Total", num: true, render: (r) => money(r.total) }
      ], empty: e.jsx(EmptyState, { title: "No invoices in this month" }) })
      : e.jsx(Table, { rows: data.purchases, rowKey: (r) => r.purchaseBillId, onRowClick: (r) => navigate(`/admin/billing/purchase-bills/${r.purchaseBillId}`), columns: [
        { key: "n", header: "Bill", render: (r) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: r.supplierBillNo }), e.jsx("div", { className: "bf-sub", children: `${fmtDate(r.billDate)} · ${r.billNumber}` })] }) },
        { key: "s", header: "Supplier", render: (r) => e.jsxs("div", { children: [r.supplierName, e.jsx("div", { className: "bf-sub", children: r.supplierGstin })] }) },
        { key: "t", header: "Taxable", num: true, render: (r) => money(r.taxable) },
        { key: "g", header: "CGST / SGST / IGST", num: true, render: (r) => `${money(r.cgst)} / ${money(r.sgst)} / ${money(r.igst)}` },
        { key: "x", header: "Total", num: true, render: (r) => money(r.total) },
        { key: "b", header: "GSTR-2B", render: (r) => (r.gstr2bResult ? pill(r.gstr2bResult, twoBTone[r.gstr2bResult] ?? "neutral") : pill("Not compared", "neutral")) }
      ], empty: e.jsx(EmptyState, { title: "No bills with a supplier GSTIN in this month" }) })
  ] });
}

const TWO_B = { 1: ["Matches", "success"], 2: ["Missing in 2B", "danger"], 3: ["Differs", "warning"], 4: ["Bill missing here", "info"] };

function Gstr2b({ period }) {
  const toast = useToast();
  const navigate = useNavigate();
  const canManage = usePermission("GST.MANAGE");
  const { data, setData, loading } = useLoad(() => api.get("/gst/gstr2b", { period }), [period]);
  const [busy, setBusy] = React.useState(false);
  const upload = async (file) => {
    setBusy(true);
    try {
      const r = await api.uploadWithMessage("/gst/gstr2b/upload", { period }, file);
      setData(r.data);
      toast.success(r.message || "GSTR-2B compared.");
    } catch (x) {
      toast.error(errorText(x, "The file could not be compared."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsxs("div", { children: [
    note("info", "Input credit can only be claimed on a bill the supplier has actually filed. Claiming on a bill that is not in 2B gets reversed later with interest. The last kind of row is the opposite problem and is just as expensive: a bill nobody entered is credit Regalis is entitled to and never takes."),
    canManage ? e.jsxs("div", { style: { maxWidth: 520 }, children: [
      busy ? e.jsx(Spinner, { center: true }) : e.jsx(DropZone, { accept: ".json,.xlsx", label: `Upload GSTR-2B for ${monthLabel(period)} — the JSON or Excel downloaded from the GST portal`, onFile: upload }),
      e.jsx("p", { className: "bf-sub", children: "Uploading again replaces this month's comparison." })
    ] }) : null,
    !data ? (loading ? e.jsx(Spinner, { center: true }) : null) : e.jsxs("div", { children: [
      data.uploadedUtc
        ? e.jsxs("div", { className: "bf-cards", children: [
          e.jsx(StatCard, { label: "Matches", value: data.matches, sub: "Claim" }),
          e.jsx(StatCard, { label: "Missing in 2B", value: data.missingIn2b, sub: "Hold — the supplier has not filed", danger: data.missingIn2b > 0 }),
          e.jsx(StatCard, { label: "Differs", value: data.differs, sub: "Query the supplier" }),
          e.jsx(StatCard, { label: "Bill missing here", value: data.missingHere, sub: `${money(data.unclaimedCredit)} of credit not taken` }),
          e.jsx(StatCard, { label: "Credit held back", value: money(data.heldBack), danger: data.heldBack > 0 })
        ] })
        : e.jsx(EmptyState, { title: "GSTR-2B has not been uploaded for this month" }),
      data.uploadedUtc ? e.jsx("p", { className: "bf-sub", children: `Compared ${fmtDateTime(data.uploadedUtc)}.` }) : null,
      data.rows.length ? e.jsx(Table, { rows: data.rows, rowKey: (r) => r.gstr2bLineId, columns: [
        { key: "s", header: "Supplier", render: (r) => e.jsxs("div", { children: [r.supplierName ?? "—", e.jsx("div", { className: "bf-sub", children: r.supplierGstin })] }) },
        { key: "b", header: "Bill no.", render: (r) => e.jsxs("div", { children: [r.billNo, r.billDate ? e.jsx("div", { className: "bf-sub", children: fmtDate(r.billDate) }) : null] }) },
        { key: "y", header: "Your tax", num: true, render: (r) => money(r.yourTax) },
        { key: "t", header: "Tax in 2B", num: true, render: (r) => money(r.tax2b) },
        { key: "r", header: "Result", render: (r) => pill(r.resultLabel, (TWO_B[r.result] ?? ["", "neutral"])[1]) },
        { key: "a", header: "What to do", render: (r) => r.result === 4
          ? e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate("/admin/billing/purchase-bills/new"), children: r.action })
          : r.purchaseBillId
            ? e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/purchase-bills/${r.purchaseBillId}`), children: r.action })
            : r.action }
      ] }) : null
    ] })
  ] });
}

const RETURN_STATUS = { 1: ["Not started", "neutral"], 2: ["Ready", "warning"], 3: ["Filed", "success"] };
const RETURN_NAME = { GSTR1: "GSTR-1", GSTR3B: "GSTR-3B" };

function ReturnDialog({ row, onClose, onDone }) {
  const toast = useToast();
  const [status, setStatus] = React.useState(String(row.status));
  const [arn, setArn] = React.useState(row.arn ?? "");
  const [filedOn, setFiledOn] = React.useState(row.filedOn ?? istToday());
  const [busy, setBusy] = React.useState(false);
  const filed = status === "3";
  const submit = async () => {
    setBusy(true);
    try {
      await api.put("/gst/returns", { period: row.period, returnType: row.returnType, status: Number(status), arn: filed ? arn.trim() : null, filedOn: filed ? filedOn : null });
      toast.success("Saved.");
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not save."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `${RETURN_NAME[row.returnType]} — ${monthLabel(row.period)}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy || (filed && (!arn.trim() || !filedOn)), children: "Save" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx(Select, { label: "Status", value: status, options: [{ value: "1", label: "Not started" }, { value: "2", label: "Ready" }, { value: "3", label: "Filed" }], onChange: (ev) => setStatus(ev.target.value) }),
      filed ? e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "ARN", required: true, maxLength: 20, value: arn, onChange: (ev) => setArn(ev.target.value) }),
        e.jsx(Input, { label: "Filed on", type: "date", required: true, max: istToday(), value: filedOn, onChange: (ev) => setFiledOn(ev.target.value) })
      ] }) : null,
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "This does not file anything. It stops the question \"did we file September?\" being unanswerable." })
    ] })
  });
}

function ReturnTracker() {
  const canManage = usePermission("GST.MANAGE");
  const [fy, setFy] = React.useState(currentFy());
  const [editing, setEditing] = React.useState(null);
  const { data, loading, reload } = useLoad(() => api.get("/gst/returns", { fy }), [fy]);
  const today = istToday();
  return e.jsxs("div", { children: [
    e.jsx("div", { style: { maxWidth: 220 }, children: e.jsx(Select, { label: "Financial year", value: fy, options: fyOptions(), onChange: (ev) => setFy(ev.target.value) }) }),
    e.jsx("div", { style: { height: 12 } }),
    e.jsx(Table, { rows: data, loading, rowKey: (r) => `${r.period}-${r.returnType}`,
      rowClass: (r) => (r.status !== 3 && r.dueDate < today ? "bf-row--overdue" : ""),
      columns: [
        { key: "p", header: "Period", render: (r) => monthLabel(r.period) },
        { key: "t", header: "Return", render: (r) => RETURN_NAME[r.returnType] },
        { key: "d", header: "Due", render: (r) => e.jsx("span", { className: r.status !== 3 && r.dueDate < today ? "bf-red" : undefined, children: fmtDate(r.dueDate) }) },
        { key: "s", header: "Status", render: (r) => pill(...(RETURN_STATUS[r.status] ?? ["—", "neutral"])) },
        { key: "a", header: "ARN", render: (r) => r.arn ?? "—" },
        { key: "f", header: "Filed", render: (r) => (r.filedOn ? e.jsxs("div", { children: [fmtDate(r.filedOn), e.jsx("div", { className: "bf-sub", children: r.filedBy ?? "" })] }) : "—") },
        { key: "x", header: "", render: (r) => (canManage ? e.jsx("div", { className: "bf-actions", children: e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(r), children: "Update" }) }) : null) }
      ] }),
    editing ? e.jsx(ReturnDialog, { row: editing, onClose: () => setEditing(null), onDone: () => { setEditing(null); reload(); } }) : null
  ] });
}

function GstRates() {
  const toast = useToast();
  const canEdit = usePermission("SETTING.EDIT");
  const { data, reload } = useLoad(() => api.get("/gst/rates"), []);
  const [adding, setAdding] = React.useState(false);
  const [form, setForm] = React.useState({ rate: "", label: "", effectiveFrom: istToday(), effectiveTo: "" });
  const [busy, setBusy] = React.useState(false);
  const save = async () => {
    const rate = amountOf(form.rate);
    if (rate === null) return toast.error("Enter the rate as a number.");
    setBusy(true);
    try {
      await api.post("/gst/rates", { rate, label: form.label.trim() || `${rate}%`, effectiveFrom: form.effectiveFrom, effectiveTo: form.effectiveTo || null, isActive: true });
      toast.success("Rate saved.");
      setAdding(false);
      reload();
    } catch (x) {
      toast.error(errorText(x, "Could not save the rate."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsxs("div", { className: "bf-panel", style: { marginTop: 24 }, children: [
    e.jsxs("div", { style: { display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center" }, children: [
      e.jsx("h3", { className: "bf-panel__title", style: { margin: 0 }, children: "GST rates" }),
      canEdit ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setAdding(true), children: "Add a rate" }) : null
    ] }),
    e.jsx("p", { className: "bf-sub", children: "Rates have effective dates, not a single value: when a rate changes, last year's invoices keep the rate they were issued at." }),
    e.jsx(Table, { rows: data, rowKey: (r) => r.gstRateId, columns: [
      { key: "r", header: "Rate", render: (r) => r.label },
      { key: "f", header: "From", render: (r) => fmtDate(r.effectiveFrom) },
      { key: "t", header: "To", render: (r) => (r.effectiveTo ? fmtDate(r.effectiveTo) : "—") },
      { key: "a", header: "", render: (r) => (r.isActive ? pill("Active", "success") : pill("Inactive", "neutral")) }
    ] }),
    adding ? e.jsx(Modal, {
      open: true, title: "Add a GST rate", onClose: () => setAdding(false),
      footer: e.jsxs(e.Fragment, { children: [
        e.jsx(Button, { variant: "outline", onClick: () => setAdding(false), children: "Cancel" }),
        e.jsx(Button, { onClick: save, loading: busy, disabled: busy, children: "Save" })
      ] }),
      children: e.jsx("div", { className: "bf-form-grid bf-modal", children: [
        e.jsx(Input, { label: "Rate %", required: true, inputMode: "decimal", value: form.rate, onChange: (ev) => setForm({ ...form, rate: ev.target.value }) }, "r"),
        e.jsx(Input, { label: "Label", value: form.label, placeholder: "18%", onChange: (ev) => setForm({ ...form, label: ev.target.value }) }, "l"),
        e.jsx(Input, { label: "Effective from", type: "date", required: true, value: form.effectiveFrom, onChange: (ev) => setForm({ ...form, effectiveFrom: ev.target.value }) }, "f"),
        e.jsx(Input, { label: "Effective to", type: "date", value: form.effectiveTo, onChange: (ev) => setForm({ ...form, effectiveTo: ev.target.value }) }, "t")
      ] })
    }) : null
  ] });
}

export function AdminGstPage() {
  useStyles();
  const [period, setPeriod] = React.useState(thisPeriod());
  const [tab, setTab] = React.useState("month");
  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: "GST",
      description: "What is payable this month, the sales and purchase registers, GSTR-2B against the bills, and which returns have been filed.",
      actions: e.jsx("div", { style: { minWidth: 200 }, children: e.jsx(Select, { label: "Month", value: period, options: monthOptions(), onChange: (ev) => setPeriod(ev.target.value) }) })
    }),
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [
      { value: "month", label: "This month" }, { value: "sales", label: "Sales register" }, { value: "purchase", label: "Purchase register" },
      { value: "2b", label: "GSTR-2B reconciliation" }, { value: "returns", label: "Return tracker" }
    ] }),
    tab === "month" ? e.jsx(GstThisMonth, { period, goTo2b: () => setTab("2b") }) : null,
    tab === "sales" ? e.jsx(GstRegister, { kind: "sales", period }) : null,
    tab === "purchase" ? e.jsx(GstRegister, { kind: "purchase", period }) : null,
    tab === "2b" ? e.jsx(Gstr2b, { period }) : null,
    tab === "returns" ? e.jsx(ReturnTracker, {}) : null,
    tab === "month" ? e.jsx(GstRates, {}) : null
  ] });
}

// ════════════════════════════════════════════════════════════════════════════
// TDS (doc 10)
// ════════════════════════════════════════════════════════════════════════════

const QUARTERS = [{ value: "1", label: "Q1 (Apr–Jun)" }, { value: "2", label: "Q2 (Jul–Sep)" }, { value: "3", label: "Q3 (Oct–Dec)" }, { value: "4", label: "Q4 (Jan–Mar)" }];
const currentQuarter = () => {
  const m = Number(thisPeriod().slice(5, 7));
  return String(m >= 4 ? Math.floor((m - 4) / 3) + 1 : 4);
};

function RecordChallanDialog({ onClose, onDone }) {
  const toast = useToast();
  const { data: open } = useLoad(() => api.get("/tds/payable", { open: true }), []);
  const [bsr, setBsr] = React.useState("");
  const [no, setNo] = React.useState("");
  const [paidOn, setPaidOn] = React.useState(istToday());
  const [amount, setAmount] = React.useState("");
  const [ticked, setTicked] = React.useState([]);
  const [busy, setBusy] = React.useState(false);
  const covered = (open ?? []).filter((t) => ticked.includes(t.tdsEntryId)).reduce((s, t) => s + t.tdsAmount, 0);
  const toggle = (id) => setTicked((list) => (list.includes(id) ? list.filter((x) => x !== id) : [...list, id]));
  const value = amountOf(amount);
  const submit = async () => {
    if (!/^\d{7}$/.test(bsr.trim())) return toast.error("The BSR code is the bank's seven-digit code printed on the challan.");
    if (!no.trim()) return toast.error("Enter the challan serial number.");
    if (value === null || value <= 0) return toast.error("Enter the amount paid on the challan.");
    if (ticked.length === 0) return toast.error("Tick the deductions this challan covers.");
    setBusy(true);
    try {
      const r = await api.postWithMessage("/tds/challans", { bsrCode: bsr.trim(), challanNo: no.trim(), paidOn, amount: value, tdsEntryIds: ticked });
      toast.success(r.message || "Challan recorded.");
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not record the challan."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: "Record a challan", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Record challan" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal bf-modal--wide", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "BSR code", required: true, maxLength: 7, inputMode: "numeric", value: bsr, onChange: (ev) => setBsr(ev.target.value) }),
        e.jsx(Input, { label: "Challan no.", required: true, maxLength: 10, value: no, onChange: (ev) => setNo(ev.target.value) }),
        e.jsx(Input, { label: "Date paid", type: "date", required: true, max: istToday(), value: paidOn, onChange: (ev) => setPaidOn(ev.target.value) }),
        e.jsx(Input, { label: "Amount (₹)", required: true, inputMode: "decimal", value: amount, onChange: (ev) => setAmount(ev.target.value) })
      ] }),
      e.jsx("div", { className: "field__label", children: "The deductions it covers" }),
      !open ? e.jsx(Spinner, { center: true }) : e.jsx(Table, { rows: open, rowKey: (t) => t.tdsEntryId, empty: e.jsx(EmptyState, { title: "Every deduction is already on a challan" }), columns: [
        { key: "x", header: "", render: (t) => e.jsx("input", { type: "checkbox", checked: ticked.includes(t.tdsEntryId), onChange: () => toggle(t.tdsEntryId), "aria-label": `Tick ${t.partyName}` }) },
        { key: "d", header: "Date paid", render: (t) => fmtDate(t.deductedOn) },
        { key: "p", header: "Supplier", render: (t) => e.jsxs("div", { children: [t.partyName, e.jsx("div", { className: "bf-sub", children: t.partyTaxId ?? "No PAN" })] }) },
        { key: "s", header: "Section", render: (t) => t.section },
        { key: "a", header: "TDS", num: true, render: (t) => money(t.tdsAmount) }
      ] }),
      e.jsxs("div", { className: `bf-note ${value !== null && covered > value ? "bf-note--danger" : "bf-note--info"}`, children: [
        `Ticked: ${ticked.length} deduction${ticked.length === 1 ? "" : "s"}, ${money(covered)}.`,
        value !== null && covered > value ? ` This challan is ${money(value)} but the deductions ticked come to ${money(covered)}.` : "",
        " One challan covers many deductions; a deduction may not appear on two challans."
      ] })
    ] })
  });
}

function CertificateDialog({ entry, onClose, onDone }) {
  const toast = useToast();
  const [no, setNo] = React.useState(entry.certificateNo ?? "");
  const [on, setOn] = React.useState(entry.certificateReceivedOn ?? istToday());
  const [file, setFile] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const payable = entry.direction === 1;
  const submit = async () => {
    if (!no.trim()) return toast.error("Enter the Form 16A certificate number.");
    setBusy(true);
    try {
      await api.upload(`/tds/entries/${entry.tdsEntryId}/certificate`, { certificateNo: no.trim(), receivedOn: on }, file);
      toast.success("Certificate saved.");
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not save the certificate."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: payable ? `Form 16A issued — ${entry.partyName}` : `Form 16A received — ${entry.partyName}`, onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Save" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: `${entry.section} · ${money(entry.tdsAmount)} · Q${entry.quarter} ${entry.financialYear}${entry.documentNumber ? ` · ${entry.documentNumber}` : ""}` }),
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Certificate no.", required: true, maxLength: 40, value: no, onChange: (ev) => setNo(ev.target.value) }),
        e.jsx(Input, { label: payable ? "Issued on" : "Received on", type: "date", required: true, max: istToday(), value: on, onChange: (ev) => setOn(ev.target.value) })
      ] }),
      e.jsx(DropZone, { accept: ".pdf,.jpg,.jpeg,.png", label: file ? file.name : "Attach the PDF (optional) — drop it here or click to browse", onFile: setFile })
    ] })
  });
}

function NewRateDialog({ sections, onClose, onDone }) {
  const toast = useToast();
  const [f, setF] = React.useState({ section: "", description: "", rate: "", individualRate: "", noPanRate: "20", effectiveFrom: istToday() });
  const [busy, setBusy] = React.useState(false);
  const pick = (code) => {
    const s = sections.find((x) => x.section === code && !x.effectiveTo);
    setF({ ...f, section: code, description: s?.description ?? f.description });
  };
  const submit = async () => {
    const rate = amountOf(f.rate), noPan = amountOf(f.noPanRate);
    if (!f.section.trim() || rate === null || noPan === null) return toast.error("Enter the section, the rate and the no-PAN rate.");
    setBusy(true);
    try {
      await api.post("/tds/sections", { section: f.section.trim(), description: f.description.trim(), rate, individualRate: f.individualRate ? amountOf(f.individualRate) : null, noPanRate: noPan, effectiveFrom: f.effectiveFrom });
      toast.success("Rate saved. The previous rate closes the day before.");
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not save the rate."));
    } finally {
      setBusy(false);
    }
  };
  const codes = [...new Set(sections.map((s) => s.section))];
  return e.jsx(Modal, {
    open: true, title: "New rate from a date", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Save" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Section", required: true, maxLength: 10, list: "bf-tds-codes", value: f.section, onChange: (ev) => pick(ev.target.value) }),
        e.jsx("datalist", { id: "bf-tds-codes", children: codes.map((c) => e.jsx("option", { value: c }, c)) }),
        e.jsx(Input, { label: "Effective from", type: "date", required: true, value: f.effectiveFrom, onChange: (ev) => setF({ ...f, effectiveFrom: ev.target.value }) })
      ] }),
      e.jsx(Input, { label: "Applies to", required: true, maxLength: 200, value: f.description, onChange: (ev) => setF({ ...f, description: ev.target.value }) }),
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Rate %", required: true, inputMode: "decimal", value: f.rate, onChange: (ev) => setF({ ...f, rate: ev.target.value }) }),
        e.jsx(Input, { label: "Individual / HUF rate %", inputMode: "decimal", hint: "Only where it differs (194C 1%).", value: f.individualRate, onChange: (ev) => setF({ ...f, individualRate: ev.target.value }) }),
        e.jsx(Input, { label: "No PAN rate %", required: true, inputMode: "decimal", value: f.noPanRate, onChange: (ev) => setF({ ...f, noPanRate: ev.target.value }) })
      ] }),
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "A new rate is a new row from its date; earlier deductions keep the rate they were made at. Overwriting a single setting would rewrite history." })
    ] })
  });
}

function TdsSections() {
  const canEdit = usePermission("SETTING.EDIT");
  const { data, reload } = useLoad(() => api.get("/tds/sections"), []);
  const [adding, setAdding] = React.useState(false);
  return e.jsxs("div", { className: "bf-panel", style: { marginTop: 24 }, children: [
    e.jsxs("div", { style: { display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center" }, children: [
      e.jsx("h3", { className: "bf-panel__title", style: { margin: 0 }, children: "Sections & rates" }),
      canEdit ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setAdding(true), children: "New rate from a date" }) : null
    ] }),
    e.jsx("div", { style: { height: 12 } }),
    e.jsx(Table, { rows: data, rowKey: (s) => s.tdsSectionId, columns: [
      { key: "s", header: "Section", render: (s) => e.jsx("span", { className: "bf-strong", children: s.section }) },
      { key: "d", header: "Applies to", render: (s) => s.description },
      { key: "r", header: "Rate", num: true, render: (s) => `${s.rate}%${s.individualRate != null ? ` (${s.individualRate}% individual/HUF)` : ""}` },
      { key: "n", header: "No PAN", num: true, render: (s) => `${s.noPanRate}%` },
      { key: "f", header: "In force", render: (s) => `${fmtDate(s.effectiveFrom)} – ${s.effectiveTo ? fmtDate(s.effectiveTo) : "now"}` }
    ] }),
    adding ? e.jsx(NewRateDialog, { sections: data ?? [], onClose: () => setAdding(false), onDone: () => { setAdding(false); reload(); } }) : null
  ] });
}

export function AdminTdsPage() {
  useStyles();
  const toast = useToast();
  const canManage = usePermission("TDS.MANAGE");
  const [fy, setFy] = React.useState(currentFy());
  const [quarter, setQuarter] = React.useState(currentQuarter());
  const [section, setSection] = React.useState("");
  const [tab, setTab] = React.useState("payable");
  const [challan, setChallan] = React.useState(false);
  const [cert, setCert] = React.useState(null);
  const [version, setVersion] = React.useState(0);
  const bump = () => setVersion((v) => v + 1);
  const params = { fy, quarter: quarter || undefined };
  const summary = useLoad(() => api.get("/tds/summary", params), [fy, quarter, version]);
  const payable = useLoad(() => api.get("/tds/payable", { ...params, section: section || undefined }), [fy, quarter, section, version]);
  const receivable = useLoad(() => api.get("/tds/receivable", params), [fy, quarter, version]);
  const challans = useLoad(() => api.get("/tds/challans", { fy }), [fy, version]);
  const allReceivable = useLoad(() => api.get("/tds/receivable", { fy }), [fy, version]);
  const allPayable = useLoad(() => api.get("/tds/payable", { fy }), [fy, version]);
  const s = summary.data;
  const unpaid = (payable.data ?? []).filter((t) => !t.tdsChallanId);
  const sum = (rows, k) => rows.reduce((a, r) => a + r[k], 0);
  const totalRow = (rows, span) => e.jsxs("tr", { children: [
    e.jsx("td", { colSpan: span, children: "Total" }),
    e.jsx("td", { className: "bf-num", children: money(sum(rows, "baseAmount")) }),
    e.jsx("td", {}),
    e.jsx("td", { className: "bf-num", children: money(sum(rows, "tdsAmount")) }),
    e.jsx("td", {})
  ] });
  const sections = [...new Set((allPayable.data ?? []).map((t) => t.section))];

  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: "TDS",
      description: "Payable is tax Regalis keeps back from suppliers and must hand to the government. Receivable is tax customers kept back from Regalis, which Regalis claims against its own income tax.",
      actions: head([
        e.jsx(Select, { label: "Financial year", value: fy, options: fyOptions(), onChange: (ev) => setFy(ev.target.value) }, "fy"),
        e.jsx(Select, { label: "Quarter", value: quarter, options: [{ value: "", label: "Whole year" }, ...QUARTERS], onChange: (ev) => setQuarter(ev.target.value) }, "q")
      ])
    }),
    s ? e.jsxs("div", { className: "bf-cards", children: [
      e.jsx(StatCard, { label: "Deducted this quarter", value: money(s.deductedThisQuarter), sub: `${s.paymentsThisQuarter} payment${s.paymentsThisQuarter === 1 ? "" : "s"} · Q${s.quarter} ${s.financialYear}` }),
      e.jsx(StatCard, { label: "Not yet paid to govt", value: money(s.notYetPaid), danger: s.notYetPaid > 0, sub: s.challanDue ? `Challan due ${fmtDate(s.challanDue)}` : "Nothing waiting for a challan" }),
      e.jsx(StatCard, { label: "Receivable this year", value: money(s.receivableThisYear), sub: `From ${s.receivableCustomers} customer${s.receivableCustomers === 1 ? "" : "s"}` }),
      e.jsx(StatCard, { label: "Certificates missing", value: s.certificatesMissing, danger: s.certificatesMissing > 0, sub: `${money(s.certificatesMissingValue)} cannot be claimed until they arrive` }),
      e.jsx(StatCard, { label: "Next return", value: s.nextReturn, sub: `Due ${fmtDate(s.nextReturnDue)}` })
    ] }) : e.jsx(Spinner, { center: true }),
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [
      { value: "payable", label: "Payable — deducted from suppliers" }, { value: "receivable", label: "Receivable — deducted by customers" },
      { value: "challans", label: "Challans" }, { value: "certificates", label: "Certificates" }
    ] }),

    tab === "payable" ? e.jsxs("div", { children: [
      e.jsxs("div", { className: "bf-toolbar", children: [
        e.jsx(Select, { label: "Section", value: section, options: [{ value: "", label: "All sections" }, ...sections.map((c) => ({ value: c, label: c }))], onChange: (ev) => setSection(ev.target.value) }),
        e.jsx("div", { style: { flex: "0 0 auto" }, children: e.jsx(Button, { variant: "outline", onClick: () => downloadOr(toast)(api.download("/tds/export", { direction: 1, ...params }, "tds-payable.xlsx")), children: [e.jsx(IconDownload, { size: 16 }, "i"), " Excel"] }) })
      ] }),
      e.jsx(Table, { rows: payable.data, loading: payable.loading, rowKey: (t) => t.tdsEntryId, columns: [
        { key: "d", header: "Date paid", render: (t) => fmtDate(t.deductedOn) },
        { key: "p", header: "Supplier", render: (t) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: t.partyName }), e.jsx("div", { className: "bf-sub", children: t.partyTaxId ? `PAN ${t.partyTaxId}` : "No PAN" })] }) },
        { key: "s", header: "Section", render: (t) => e.jsxs("div", { children: [t.section, t.documentNumber ? e.jsx("div", { className: "bf-sub", children: t.documentNumber }) : null] }) },
        { key: "b", header: "Base", num: true, render: (t) => money(t.baseAmount) },
        { key: "r", header: "Rate", num: true, render: (t) => `${t.rate}%` },
        { key: "t", header: "TDS", num: true, render: (t) => money(t.tdsAmount) },
        { key: "c", header: "Challan", render: (t) => (t.challanNo ? t.challanNo : pill("Not paid", "danger")) }
      ], footer: payable.data?.length ? totalRow(payable.data, 3) : null,
      empty: e.jsx(EmptyState, { title: "No TDS deducted in this period" }) }),
      unpaid.length ? e.jsxs("div", { className: "bf-note bf-note--danger", style: { margin: "12px 0", display: "flex", justifyContent: "space-between", alignItems: "center", gap: 12, flexWrap: "wrap" }, children: [
        e.jsx("span", { children: `${money(sum(unpaid, "tdsAmount"))} across ${unpaid.length} row${unpaid.length === 1 ? "" : "s"} has no challan. Due to the government by ${s?.challanDue ? fmtDate(s.challanDue) : "the 7th of next month"}.` }),
        canManage ? e.jsx(Button, { size: "sm", onClick: () => setChallan(true), children: "Record a challan" }) : null
      ] }) : null,
      e.jsx("p", { className: "bf-sub", children: "Payable is written when a vendor payment is posted with TDS — never when the bill is entered. Tax is due on payment, not on the bill. The quarter's 26Q return is built from these rows plus each supplier's PAN. This page does not file anything; the accountant files." })
    ] }) : null,

    tab === "receivable" ? e.jsxs("div", { children: [
      e.jsx("div", { className: "bf-toolbar", children: e.jsx("div", { style: { flex: "0 0 auto" }, children: e.jsx(Button, { variant: "outline", onClick: () => downloadOr(toast)(api.download("/tds/export", { direction: 2, ...params }, "tds-receivable.xlsx")), children: [e.jsx(IconDownload, { size: 16 }, "i"), " Excel"] }) }) }),
      e.jsx(Table, { rows: receivable.data, loading: receivable.loading, rowKey: (t) => t.tdsEntryId, columns: [
        { key: "d", header: "Date", render: (t) => fmtDate(t.deductedOn) },
        { key: "p", header: "Customer", render: (t) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: t.partyName }), t.documentNumber ? e.jsx("div", { className: "bf-sub", children: t.documentNumber }) : null] }) },
        { key: "n", header: "Their TAN", render: (t) => t.partyTaxId ?? "—" },
        { key: "s", header: "Section", render: (t) => t.section },
        { key: "b", header: "Base", num: true, render: (t) => money(t.baseAmount) },
        { key: "r", header: "Rate", num: true, render: (t) => `${t.rate}%` },
        { key: "t", header: "TDS", num: true, render: (t) => money(t.tdsAmount) },
        { key: "c", header: "Certificate received", render: (t) => (t.certificateNo ? `Yes · ${t.certificateNo}` : pill("Missing", "danger")) }
      ], empty: e.jsx(EmptyState, { title: "No TDS deducted by customers in this period" }) }),
      e.jsx("p", { className: "bf-sub", children: "A customer pays less than the invoice and sends a TDS certificate. The short payment is matched on the bank screen and explained as \"TDS deducted by the customer\"; the amount lands here. Each quarter compare this register with Form 26AS, chase what is missing, and claim the rest in the income tax return." })
    ] }) : null,

    tab === "challans" ? e.jsxs("div", { children: [
      canManage ? e.jsx("div", { className: "bf-toolbar", children: e.jsx("div", { style: { flex: "0 0 auto" }, children: e.jsx(Button, { onClick: () => setChallan(true), children: "Record a challan" }) }) }) : null,
      e.jsx(Table, { rows: challans.data, loading: challans.loading, rowKey: (c) => c.tdsChallanId, columns: [
        { key: "d", header: "Date paid", render: (c) => fmtDate(c.paidOn) },
        { key: "b", header: "BSR code", render: (c) => c.bsrCode },
        { key: "n", header: "Challan no.", render: (c) => e.jsx("span", { className: "bf-strong", children: c.challanNo }) },
        { key: "q", header: "Quarter", render: (c) => `Q${c.quarter} ${c.financialYear}` },
        { key: "k", header: "Deductions", num: true, render: (c) => c.deductions },
        { key: "v", header: "Covers", num: true, render: (c) => money(c.covered) },
        { key: "a", header: "Amount", num: true, render: (c) => money(c.amount) }
      ], empty: e.jsx(EmptyState, { title: "No challans recorded this year" }) })
    ] }) : null,

    tab === "certificates" ? e.jsxs("div", { children: [
      e.jsx("h3", { className: "bf-panel__title", children: "From customers — Form 16A received" }),
      e.jsx(Table, { rows: allReceivable.data, loading: allReceivable.loading, rowKey: (t) => t.tdsEntryId,
        rowClass: (t) => (t.certificateOverdue ? "bf-row--tint" : ""), columns: [
          { key: "p", header: "Customer", render: (t) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: t.partyName }), e.jsx("div", { className: "bf-sub", children: `Q${t.quarter} ${t.financialYear} · ${t.section}` })] }) },
          { key: "t", header: "TDS", num: true, render: (t) => money(t.tdsAmount) },
          { key: "c", header: "Certificate", render: (t) => t.certificateNo
            ? e.jsxs("div", { children: [t.certificateNo, e.jsx("div", { className: "bf-sub", children: t.certificateReceivedOn ? fmtDate(t.certificateReceivedOn) : "" })] })
            : t.certificateOverdue ? e.jsx("span", { className: "bf-red", children: "Missing — money Regalis cannot claim" }) : pill("Missing", "danger") },
          { key: "a", header: "", render: (t) => e.jsxs("div", { className: "bf-actions", children: [
            t.hasCertificateFile ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => api.open(`/tds/entries/${t.tdsEntryId}/certificate`).catch((x) => toast.error(errorText(x, "Could not open the file."))), children: "Open" }) : null,
            canManage ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setCert(t), children: t.certificateNo ? "Update" : "Attach Form 16A" }) : null
          ] }) }
        ], empty: e.jsx(EmptyState, { title: "No customer has deducted TDS this year" }) }),
      e.jsx("h3", { className: "bf-panel__title", style: { marginTop: 24 }, children: "To suppliers — Form 16A issued" }),
      e.jsx(Table, { rows: allPayable.data, loading: allPayable.loading, rowKey: (t) => t.tdsEntryId, columns: [
        { key: "p", header: "Supplier", render: (t) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: t.partyName }), e.jsx("div", { className: "bf-sub", children: `Q${t.quarter} ${t.financialYear} · ${t.section}` })] }) },
        { key: "t", header: "TDS", num: true, render: (t) => money(t.tdsAmount) },
        { key: "c", header: "Issued", render: (t) => (t.certificateNo ? `${t.certificateNo}${t.certificateReceivedOn ? ` · ${fmtDate(t.certificateReceivedOn)}` : ""}` : "—") },
        { key: "a", header: "", render: (t) => e.jsxs("div", { className: "bf-actions", children: [
          t.hasCertificateFile ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => api.open(`/tds/entries/${t.tdsEntryId}/certificate`).catch((x) => toast.error(errorText(x, "Could not open the file."))), children: "Open" }) : null,
          canManage ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setCert(t), children: t.certificateNo ? "Update" : "Record issued" }) : null
        ] }) }
      ], empty: e.jsx(EmptyState, { title: "No TDS deducted from suppliers this year" }) }),
      e.jsx("p", { className: "bf-sub", children: "Every receivable row waits for a Form 16A from that customer. Anything still missing a month after the quarter closed is money Regalis cannot claim." })
    ] }) : null,

    e.jsx(TdsSections, {}),
    challan ? e.jsx(RecordChallanDialog, { onClose: () => setChallan(false), onDone: () => { setChallan(false); bump(); } }) : null,
    cert ? e.jsx(CertificateDialog, { entry: cert, onClose: () => setCert(null), onDone: () => { setCert(null); bump(); } }) : null
  ] });
}

// ════════════════════════════════════════════════════════════════════════════
// Accounting export (doc 20)
// ════════════════════════════════════════════════════════════════════════════

export function AccountingExportDialog({ onClose }) {
  const toast = useToast();
  const fy = currentFy();
  const fyStart = `${fy.slice(0, 4)}-04-01`;
  const [from, setFrom] = React.useState(fyStart);
  const [to, setTo] = React.useState(istToday());
  const [busy, setBusy] = React.useState(false);
  const run = async () => {
    if (!from || !to || to < from) return toast.error("The end date cannot be before the start date.");
    setBusy(true);
    try {
      await api.download("/export/accounting", { from, to }, `regalis-accounts-${from}-${to}.xlsx`);
    } catch (x) {
      toast.error(errorText(x, XLSX_FAIL));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: "Accounting export", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Close" }),
      e.jsx(Button, { onClick: run, loading: busy, disabled: busy, children: [e.jsx(IconDownload, { size: 16 }, "i"), " Download Excel"] })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx("p", { style: { margin: 0 }, children: "One workbook, one sheet per list, for the accountant to load into Tally or Zoho Books. Regalis is not a ledger; it hands the numbers over cleanly instead." }),
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "From", type: "date", value: from, onChange: (ev) => setFrom(ev.target.value) }),
        e.jsx(Input, { label: "To", type: "date", value: to, max: istToday(), onChange: (ev) => setTo(ev.target.value) })
      ] }),
      e.jsx("div", { children: e.jsx("button", { type: "button", className: "bf-chip", onClick: () => { setFrom(fyStart); setTo(istToday()); }, children: `This financial year (${fy})` }) }),
      e.jsx("div", { className: "bf-note bf-note--info", children: "Sheets: Sales · Receipts · Purchases · Payments made · Expenses · TDS payable · TDS receivable · Bank · Parties." }),
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "Tally XML and the Zoho Books CSV come later; the accountant can work from the Excel on day one." })
    ] })
  });
}

// ════════════════════════════════════════════════════════════════════════════
// Compliance calendar (doc 13.1, 13.2)
// ════════════════════════════════════════════════════════════════════════════

const STATE = { 1: ["Not started", "neutral"], 2: ["Not done", "danger"], 3: ["Ready to file", "warning"], 4: ["Filed", "success"] };

function StatusDialog({ item, onClose, onDone }) {
  const toast = useToast();
  const [status, setStatus] = React.useState(String(item.status === 2 ? 1 : item.status));
  const [reference, setReference] = React.useState(item.reference ?? "");
  const [completedOn, setCompletedOn] = React.useState(item.completedOn ?? istToday());
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    setBusy(true);
    try {
      await api.put("/compliance/status", { obligationId: item.obligationId, period: item.period, status: Number(status), reference: reference.trim() || null, completedOn: status === "4" ? completedOn : null });
      toast.success("Saved.");
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not save."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: `${item.name} — ${item.periodLabel}`, onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Save" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx(Select, { label: "Status", value: status, options: [{ value: "1", label: "Not started" }, { value: "3", label: "Ready to file" }, { value: "4", label: "Filed" }], onChange: (ev) => setStatus(ev.target.value) }),
      e.jsx(Input, { label: "Reference", required: status === "4", maxLength: 60, hint: "The ARN, challan or acknowledgement number.", value: reference, onChange: (ev) => setReference(ev.target.value) }),
      status === "4" ? e.jsx(Input, { label: "Done on", type: "date", max: istToday(), value: completedOn, onChange: (ev) => setCompletedOn(ev.target.value) }) : null
    ] })
  });
}

function ReminderLogDialog({ onClose }) {
  const { data, loading } = useLoad(() => api.get("/reminders/log"), []);
  return e.jsx(Modal, {
    open: true, title: "Reminder log", onClose,
    footer: e.jsx(Button, { variant: "outline", onClick: onClose, children: "Close" }),
    children: e.jsxs("div", { className: "bf-modal bf-modal--wide", children: [
      e.jsx("p", { className: "bf-sub", style: { marginTop: 0 }, children: "Every reminder: what was sent, to whom, when, and whether it was delivered." }),
      e.jsx(Table, { rows: data, loading, rowKey: (r) => r.reminderLogId, columns: [
        { key: "s", header: "Sent", render: (r) => fmtDateTime(r.sentUtc) },
        { key: "t", header: "Reminder", render: (r) => r.reminderType },
        { key: "d", header: "Document", render: (r) => `${r.documentType}${r.documentNumber ? ` ${r.documentNumber}` : ""}` },
        { key: "r", header: "To", render: (r) => e.jsxs("div", { children: [r.recipient, e.jsx("div", { className: "bf-sub", children: r.channel })] }) },
        { key: "o", header: "Delivered", render: (r) => (r.delivered ? pill("Delivered", "success") : e.jsxs("div", { children: [pill("Not delivered", "danger"), r.error ? e.jsx("div", { className: "bf-sub", children: r.error }) : null] })) }
      ], empty: e.jsx(EmptyState, { title: "No reminder has been sent yet" }) })
    ] })
  });
}

const FREQUENCIES = [
  { value: "MonthlyNext", label: "Monthly — due the month after (TDS challan, GSTR-1, GSTR-3B)" },
  { value: "Monthly", label: "Monthly — due in the same month (professional tax)" },
  { value: "Quarterly", label: "Quarterly — due months list Q1..Q4 (26Q: 7,10,1,5)" },
  { value: "Yearly", label: "Yearly — due month after the financial year" }
];

function ObligationDialog({ item, onClose, onDone }) {
  const toast = useToast();
  const [f, setF] = React.useState(item ?? { code: "", name: "", frequency: "MonthlyNext", dueDay: 7, dueMonths: "", amountSource: "", ownerRole: "Finance", displayOrder: 100, isActive: true });
  const [busy, setBusy] = React.useState(false);
  const set = (k) => (ev) => setF({ ...f, [k]: ev.target.type === "checkbox" ? ev.target.checked : ev.target.value });
  const submit = async () => {
    setBusy(true);
    try {
      const body = { code: f.code, name: f.name, frequency: f.frequency, dueDay: Number(f.dueDay), dueMonths: f.dueMonths || null, amountSource: f.amountSource || null,
        ownerRole: f.ownerRole || null, displayOrder: Number(f.displayOrder) || 0, isActive: !!f.isActive };
      if (item) await api.put(`/compliance/obligations/${item.complianceObligationId}`, body);
      else await api.post("/compliance/obligations", body);
      toast.success("Saved.");
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not save."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: item ? `Edit ${item.name}` : "Add an obligation", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Save" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Code", required: true, maxLength: 30, value: f.code, onChange: set("code") }),
        e.jsx(Input, { label: "Name", required: true, maxLength: 150, value: f.name, onChange: set("name") })
      ] }),
      e.jsx(Select, { label: "Frequency", value: f.frequency, options: FREQUENCIES, onChange: set("frequency") }),
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Due day", type: "number", min: 1, max: 31, value: f.dueDay, onChange: set("dueDay") }),
        e.jsx(Input, { label: "Due months", placeholder: "7,10,1,5", hint: "Quarterly: the due month of Q1..Q4. Yearly: the month.", value: f.dueMonths ?? "", onChange: set("dueMonths") }),
        e.jsx(Input, { label: "Amount from", placeholder: "TdsUnpaid", hint: "TdsUnpaid, GstCash, Setting:<key>, or empty for no amount.", value: f.amountSource ?? "", onChange: set("amountSource") }),
        e.jsx(Input, { label: "Who", value: f.ownerRole ?? "", onChange: set("ownerRole") }),
        e.jsx(Input, { label: "Order", type: "number", value: f.displayOrder, onChange: set("displayOrder") })
      ] }),
      e.jsxs("label", { style: { display: "flex", gap: 8, alignItems: "center" }, children: [e.jsx("input", { type: "checkbox", checked: !!f.isActive, onChange: set("isActive") }), "Active"] }),
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "The dates come from these rows, not from code, so a new obligation can be added without a release." })
    ] })
  });
}

function Obligations() {
  const canManage = usePermission("COMPLIANCE.MANAGE");
  const { data, reload } = useLoad(() => api.get("/compliance/obligations"), []);
  const [editing, setEditing] = React.useState(undefined);
  return e.jsxs("div", { className: "bf-panel", style: { marginTop: 24 }, children: [
    e.jsxs("div", { style: { display: "flex", justifyContent: "space-between", gap: 12, alignItems: "center" }, children: [
      e.jsx("h3", { className: "bf-panel__title", style: { margin: 0 }, children: "Obligations" }),
      canManage ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(null), children: "Add an obligation" }) : null
    ] }),
    e.jsx("div", { style: { height: 12 } }),
    e.jsx(Table, { rows: data, rowKey: (o) => o.complianceObligationId, columns: [
      { key: "n", header: "What", render: (o) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: o.name }), e.jsx("div", { className: "bf-sub", children: o.code })] }) },
      { key: "f", header: "When", render: (o) => `${o.frequency}, day ${o.dueDay}${o.dueMonths ? ` (months ${o.dueMonths})` : ""}` },
      { key: "a", header: "Amount from", render: (o) => o.amountSource ?? "No amount" },
      { key: "w", header: "Who", render: (o) => o.ownerRole ?? "—" },
      { key: "s", header: "", render: (o) => (o.isActive ? pill("Active", "success") : pill("Off", "neutral")) },
      { key: "x", header: "", render: (o) => (canManage ? e.jsx("div", { className: "bf-actions", children: e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(o), children: "Edit" }) }) : null) }
    ] }),
    editing !== undefined ? e.jsx(ObligationDialog, { item: editing, onClose: () => setEditing(undefined), onDone: () => { setEditing(undefined); reload(); } }) : null
  ] });
}

const shiftPeriod = (period, n) => {
  const [y, m] = period.split("-").map(Number);
  return new Date(Date.UTC(y, m - 1 + n, 1)).toISOString().slice(0, 7);
};

export function AdminCompliancePage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canManage = usePermission("COMPLIANCE.MANAGE");
  const canExport = usePermission("FINANCE.EXPORT");
  const [period, setPeriod] = React.useState(thisPeriod());
  const [months] = React.useState(3);
  const [marking, setMarking] = React.useState(null);
  const [exporting, setExporting] = React.useState(false);
  const [log, setLog] = React.useState(false);
  const [running, setRunning] = React.useState(false);
  const { data, loading, reload } = useLoad(() => api.get("/compliance/calendar", { period, months }), [period, months]);

  const runReminders = async () => {
    setRunning(true);
    try {
      const r = await api.post("/reminders/run");
      if (!r.enabled) toast.error("Reminders are switched off (Settings > Reminders.Enabled). They stay off until email from production is proven.");
      else toast.success(`Reminders sent: ${r.sent}${r.failed ? `, not delivered: ${r.failed}` : ""}.${r.notes?.length ? ` ${r.notes.join(" ")}` : ""}`);
    } catch (x) {
      toast.error(errorText(x, "Could not run the reminders."));
    } finally {
      setRunning(false);
    }
  };

  const days = (n) => (n === 0 ? "today" : n > 0 ? `in ${n} day${n === 1 ? "" : "s"}` : `${-n} day${n === -1 ? "" : "s"} late`);
  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: "Compliance Calendar",
      description: "Every statutory date, with whether it has been dealt with. The amounts are pulled live from GST and TDS.",
      actions: head([
        e.jsx(Button, { variant: "outline", onClick: () => navigate("/admin/billing/period-locks"), children: "Period lock" }, "l"),
        canExport ? e.jsx(Button, { variant: "outline", onClick: () => setExporting(true), children: "Accounting export" }, "x") : null,
        e.jsx(Button, { variant: "outline", onClick: () => setLog(true), children: "Reminder log" }, "g"),
        canManage ? e.jsx(Button, { variant: "outline", onClick: runReminders, loading: running, disabled: running, children: "Run reminders now" }, "r") : null
      ])
    }),
    e.jsxs("div", { className: "bf-toolbar", style: { alignItems: "center" }, children: [
      e.jsxs("div", { style: { flex: "0 0 auto", display: "flex", gap: 8, alignItems: "center" }, children: [
        e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setPeriod(shiftPeriod(period, -months)), children: "Previous" }),
        e.jsx("span", { className: "bf-strong", children: `${monthLabel(period)} – ${monthLabel(shiftPeriod(period, months - 1))}` }),
        e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setPeriod(shiftPeriod(period, months)), children: "Next" }),
        period !== thisPeriod() ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setPeriod(thisPeriod()), children: "This month" }) : null
      ] })
    ] }),
    e.jsx(Table, { rows: data, loading, rowKey: (i) => i.key,
      rowClass: (i) => (i.status === 2 ? "bf-row--overdue" : ""),
      columns: [
        { key: "d", header: "Due", render: (i) => e.jsxs("div", { children: [
          e.jsx("div", { className: "bf-strong", children: fmtDate(i.dueDate) }),
          e.jsx("div", { className: i.status !== 4 && i.daysToGo <= 2 ? "bf-sub bf-red" : "bf-sub", children: days(i.daysToGo) })
        ] }) },
        { key: "w", header: "What", render: (i) => (i.link
          ? e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(i.link), children: i.name })
          : i.name) },
        { key: "p", header: "Period", render: (i) => i.periodLabel },
        { key: "a", header: "Amount", num: true, render: (i) => (i.amount == null ? "—" : e.jsxs("div", { children: [money(i.amount), i.amountNote ? e.jsx("div", { className: "bf-sub", children: i.amountNote }) : null] })) },
        { key: "s", header: "Status", render: (i) => e.jsxs("div", { children: [pill(...(STATE[i.status] ?? ["—", "neutral"])), i.reference ? e.jsx("div", { className: "bf-sub", children: i.reference }) : null] }) },
        { key: "o", header: "Who", render: (i) => e.jsxs("div", { children: [i.owner ?? "—", i.completedBy ? e.jsx("div", { className: "bf-sub", children: `${i.completedBy}${i.completedOn ? ` · ${fmtDate(i.completedOn)}` : ""}` }) : null] }) },
        { key: "x", header: "", render: (i) => (canManage && i.editable && i.obligationId
          ? e.jsx("div", { className: "bf-actions", children: e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setMarking(i), children: "Mark" }) })
          : null) }
      ], empty: e.jsx(EmptyState, { title: "Nothing falls due in these months" }) }),
    e.jsx("p", { className: "bf-sub", children: "Nothing is filed from here. Reminders go to finance and the accountant 7 days before, 2 days before and on the day — once reminders are switched on in Settings, after email from production is proven." }),
    e.jsx(Obligations, {}),
    marking ? e.jsx(StatusDialog, { item: marking, onClose: () => setMarking(null), onDone: () => { setMarking(null); reload(); } }) : null,
    exporting ? e.jsx(AccountingExportDialog, { onClose: () => setExporting(false) }) : null,
    log ? e.jsx(ReminderLogDialog, { onClose: () => setLog(false) }) : null
  ] });
}

// ════════════════════════════════════════════════════════════════════════════
// Period lock (doc 13.3)
// ════════════════════════════════════════════════════════════════════════════

export function AdminPeriodLocksPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canLock = usePermission("FINANCE.LOCK");
  const canUnlock = usePermission("FINANCE.UNLOCK");
  const { data, loading, reload } = useLoad(() => api.get("/period-locks"), []);
  const [locking, setLocking] = React.useState(null);
  const [unlocking, setUnlocking] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const lock = async () => {
    setBusy(true);
    try {
      await api.post(`/period-locks/${locking.period}/lock`);
      toast.success(`${locking.label} is locked.`);
      setLocking(null);
      reload();
    } catch (x) {
      toast.error(errorText(x, "Could not lock the month."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: "Period lock",
      description: "Once a month is filed, nothing in it may change. Without this, somebody back-dates an invoice into a filed month and the GSTR-1 already filed stops matching the books.",
      actions: e.jsx(Button, { variant: "outline", onClick: () => navigate("/admin/billing/compliance"), children: "Compliance Calendar" })
    }),
    e.jsx("p", { className: "bf-sub", children: "Inside a locked month nothing can be created, edited, cancelled or reversed, and no document date may be set into it — invoices, payments, expenses, bills, bank lines and petty cash alike. Unlocking needs FINANCE.UNLOCK and a reason, which is written to the audit log." }),
    e.jsx(Table, { rows: data, loading, rowKey: (p) => p.period, columns: [
      { key: "p", header: "Month", render: (p) => e.jsx("span", { className: "bf-strong", children: p.label }) },
      { key: "s", header: "Status", render: (p) => (p.isLocked ? pill("Locked", "danger") : pill("Open", "success")) },
      { key: "l", header: "Locked", render: (p) => (p.lockedUtc ? e.jsxs("div", { children: [p.lockedBy ?? "—", e.jsx("div", { className: "bf-sub", children: fmtDateTime(p.lockedUtc) })] }) : "—") },
      { key: "u", header: "Last unlocked", render: (p) => (p.unlockedUtc ? e.jsxs("div", { children: [`${p.unlockedBy ?? "—"} · ${fmtDateTime(p.unlockedUtc)}`, e.jsx("div", { className: "bf-sub", children: p.unlockReason })] }) : "—") },
      { key: "x", header: "", render: (p) => e.jsxs("div", { className: "bf-actions", children: [
        canLock && p.canLock ? e.jsx(Button, { size: "sm", onClick: () => setLocking(p), children: "Lock" }) : null,
        canUnlock && p.isLocked ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setUnlocking(p), children: "Unlock" }) : null
      ] }) }
    ] }),
    locking ? e.jsx(Modal, {
      open: true, title: `Lock ${locking.label}?`, onClose: () => setLocking(null),
      footer: e.jsxs(e.Fragment, { children: [
        e.jsx(Button, { variant: "outline", onClick: () => setLocking(null), disabled: busy, children: "Cancel" }),
        e.jsx(Button, { onClick: lock, loading: busy, disabled: busy, children: "Lock the month" })
      ] }),
      children: e.jsx("p", { className: "bf-modal", style: { margin: 0 }, children: `Once a month is filed, nothing in it may change. After locking, nothing dated in ${locking.label} can be created, edited, cancelled or reversed until someone with the unlock permission unlocks it, with a reason.` })
    }) : null,
    unlocking ? e.jsx(ReasonModal, {
      title: `Unlock ${unlocking.label}`,
      intro: "Say why this month is being unlocked. The reason is written to the audit log.",
      confirmText: "Unlock",
      danger: true,
      onClose: () => setUnlocking(null),
      onSubmit: async (reason) => {
        await api.post(`/period-locks/${unlocking.period}/unlock`, { reason });
        toast.success(`${unlocking.label} is open again.`);
        setUnlocking(null);
        reload();
      }
    }) : null
  ] });
}

// ════════════════════════════════════════════════════════════════════════════
// Petty cash (doc 12.3)
// ════════════════════════════════════════════════════════════════════════════

function TopUpDialog({ onClose, onDone }) {
  const toast = useToast();
  const { data: accounts } = useLoad(() => api.get("/bank/accounts"), []);
  const [amount, setAmount] = React.useState("");
  const [on, setOn] = React.useState(istToday());
  const [account, setAccount] = React.useState("");
  const [note, setNoteText] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const list = Array.isArray(accounts) ? accounts : accounts?.items ?? [];
  React.useEffect(() => { if (list.length === 1 && !account) setAccount(String(list[0].bankAccountId)); }, [list.length]); // eslint-disable-line react-hooks/exhaustive-deps
  const submit = async () => {
    const value = amountOf(amount);
    if (value === null || value <= 0) return toast.error("Enter the amount put into the tin.");
    if (!account) return toast.error("Choose the bank account the cash was withdrawn from.");
    setBusy(true);
    try {
      await api.post("/petty-cash/top-up", { amount: value, happenedOn: on, bankAccountId: Number(account), note: note.trim() || null });
      toast.success(`${money(value)} added to the tin.`);
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not record the top-up."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: "Top up the tin", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Record top-up" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Amount (₹)", required: true, inputMode: "decimal", autoFocus: true, value: amount, onChange: (ev) => setAmount(ev.target.value) }),
        e.jsx(Input, { label: "Date", type: "date", required: true, max: istToday(), value: on, onChange: (ev) => setOn(ev.target.value) })
      ] }),
      e.jsx(Select, { label: "Withdrawn from", required: true, value: account,
        options: [{ value: "", label: "Choose an account" }, ...list.filter((a) => a.isActive !== false).map((a) => ({ value: String(a.bankAccountId), label: `${a.bankName} · ${a.accountCode}` }))],
        onChange: (ev) => setAccount(ev.target.value) }),
      e.jsx(Input, { label: "Note", maxLength: 300, value: note, onChange: (ev) => setNoteText(ev.target.value) }),
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "A top-up is a withdrawal from a bank account, so it also shows on the bank statement and gets reconciled there." })
    ] })
  });
}

function CountDialog({ balance, onClose, onDone }) {
  const toast = useToast();
  const [counted, setCounted] = React.useState("");
  const [reason, setReason] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const value = amountOf(counted);
  const diff = value === null ? 0 : Math.round((value - balance) * 100) / 100;
  const submit = async () => {
    if (value === null) return toast.error("Enter what the tin holds.");
    if (diff !== 0 && !reason.trim()) return;
    setBusy(true);
    try {
      await api.post("/petty-cash/count", { countedAmount: value, reason: reason.trim() || null });
      toast.success(diff === 0 ? "Counted — the tin agrees." : `Counted — an adjustment of ${money(diff)} was written.`);
      onDone();
    } catch (x) {
      toast.error(errorText(x, "Could not record the count."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: "Count the tin", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy || value === null || (diff !== 0 && !reason.trim()), children: "Record count" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsx("p", { style: { margin: 0 }, children: `The running balance says the tin should hold ${money(balance)}.` }),
      e.jsx(Input, { label: "Counted (₹)", required: true, inputMode: "decimal", autoFocus: true, value: counted, onChange: (ev) => setCounted(ev.target.value) }),
      value !== null && diff !== 0 ? e.jsxs(e.Fragment, { children: [
        e.jsx("div", { className: "bf-note bf-note--warn", children: diff < 0
          ? `The tin counts ${money(value)} but should hold ${money(balance)}. Say what happened to the ${money(-diff)}.`
          : `The tin counts ${money(value)} but should hold ${money(balance)}. Say where the extra ${money(diff)} came from.` }),
        e.jsx(Input, { label: "Reason", required: true, maxLength: 300, value: reason, onChange: (ev) => setReason(ev.target.value) })
      ] }) : null,
      value !== null && diff === 0 ? e.jsx("div", { className: "bf-note bf-note--ok", children: "It agrees. The count is recorded with no adjustment." }) : null
    ] })
  });
}

const PETTY_TYPE = { 1: "Top-up", 2: "Cash out", 3: "Count / adjustment" };

export function AdminPettyCashPage() {
  useStyles();
  const [page, setPage] = React.useState(1);
  const [topUp, setTopUp] = React.useState(false);
  const [counting, setCounting] = React.useState(false);
  const { data, loading, reload } = useLoad(() => api.get("/petty-cash", { page, pageSize: 20 }), [page]);
  const done = () => { setTopUp(false); setCounting(false); reload(); };
  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: "Petty Cash",
      description: "A float with top-ups and a running balance. Every row is either a top-up from the bank, cash spent from the tin, or a count.",
      actions: head([
        e.jsx(Button, { variant: "outline", onClick: () => setCounting(true), disabled: !data, children: "Count the tin" }, "c"),
        e.jsx(Button, { onClick: () => setTopUp(true), children: "Top up" }, "t")
      ])
    }),
    data ? e.jsxs("div", { className: "bf-cards", children: [
      e.jsx(StatCard, { label: "In the tin right now", value: money(data.balance), danger: data.balance < 0, sub: `Float ${money(data.float)}${data.balance < data.float ? ` · ${money(data.float - data.balance)} below the float` : ""}` }),
      e.jsx(StatCard, { label: "Last counted", value: data.lastCountedUtc ? fmtDate(data.lastCountedUtc) : "Never",
        sub: data.lastCountedUtc ? `${data.lastCountedBy ?? ""}${data.lastCountDifference ? ` · ${data.lastCountDifference < 0 ? "short" : "over"} by ${money(Math.abs(data.lastCountDifference))}` : " · agreed"}` : "Count the tin to start the record" }),
      e.jsx(StatCard, { label: "In this month", value: money(data.inThisMonth) }),
      e.jsx(StatCard, { label: "Out this month", value: money(data.outThisMonth) })
    ] }) : e.jsx(Spinner, { center: true }),
    e.jsx(Table, { rows: data?.entries.items, loading, rowKey: (r) => r.pettyCashEntryId, columns: [
      { key: "d", header: "Date", render: (r) => fmtDate(r.happenedOn) },
      { key: "w", header: "What", render: (r) => e.jsxs("div", { children: [
        e.jsx("div", { className: "bf-strong", children: r.description }),
        e.jsx("div", { className: "bf-sub", children: [PETTY_TYPE[r.entryType], r.expenseNumber, r.reason, r.enteredBy].filter(Boolean).join(" · ") })
      ] }) },
      { key: "i", header: "In", num: true, render: (r) => (r.in ? money(r.in) : "") },
      { key: "o", header: "Out", num: true, render: (r) => (r.out ? money(r.out) : "") },
      { key: "l", header: "Left", num: true, render: (r) => e.jsx("span", { className: "bf-strong", children: money(r.balanceAfter) }) },
      { key: "r", header: "", render: (r) => (r.entryType === 1 ? (r.isReconciled ? pill("Reconciled", "success") : pill("Not on a bank line yet", "neutral")) : null) }
    ], empty: e.jsx(EmptyState, { title: "Nothing in the tin yet", description: "Top up the tin to start the float." }) }),
    e.jsx(Pager, { result: data?.entries, page, setPage }),
    e.jsx("div", { className: "bf-note bf-note--info", style: { marginTop: 16 }, children: "Petty Cash is not Cash Register. Cash Register is money taken from customers at the front desk. Petty Cash is money spent from a float. Two different tins, two different pages, and they must never be mixed." }),
    topUp ? e.jsx(TopUpDialog, { onClose: () => setTopUp(false), onDone: done }) : null,
    counting && data ? e.jsx(CountDialog, { balance: data.balance, onClose: () => setCounting(false), onDone: done }) : null
  ] });
}
