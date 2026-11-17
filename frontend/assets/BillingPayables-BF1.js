/**
 * Admin -> Billing & Finance -> MONEY OUT (Billing & Finance build, Nov 2026; build doc §4-6, §10.7).
 *
 *   AdminPurchaseBillsPage       /admin/billing/purchase-bills            the bills Regalis owes, five figures, filters
 *   AdminPurchaseBillFormPage    /admin/billing/purchase-bills/new         New bill (?po=ID, ?supplier=ID)
 *                                /admin/billing/purchase-bills/:id/edit    Edit (Draft, Awaiting approval, Rejected)
 *   AdminPurchaseBillDetailPage  /admin/billing/purchase-bills/:id         one bill: Overview, Payments, Approval, Attachments, History
 *   AdminVendorPaymentsPage      /admin/billing/vendor-payments           the register (?supplier=X&bill=Y opens a payment)
 *   NewVendorPaymentDialog       one payment, several bills, less TDS, advances
 *   AdminSuppliersPageBF         /admin/billing/suppliers                 Suppliers with PAN, GST state, TDS section, terms, Ledger
 *
 * Paid and balance always come from the server (vendor-payment allocations), never typed.
 * Hand-maintained (this repo ships the built SPA): index imports the pages by name
 * (deploy/billing-finance/frontend/apply.mjs). A change ships under a NEW name (BF2 ...).
 */
import {
  React, e, Button, Select, Input, Modal, PageHeader, EmptyState, Spinner, useToast, usePermission, useNavigate, useParams,
  api, money, fmtDate, StatCard, Tabs, Chips, SearchBox, Pager, Table, ReasonModal, DropZone, Crumbs, pill, useStyles, useLoad,
  useDebounced, amountOf, errorText, errorCode, istToday, addDays, fyOptions, currentFy, IconPlus, IconDownload, IconBack
} from "./BillingShared-BF1.js";

// ── Constants ─────────────────────────────────────────────────────────────────

const BILL_STATUS = {
  1: ["Draft", "neutral"],
  2: ["Awaiting approval", "warning"],
  3: ["Approved", "info"],
  4: ["Part paid", "warning"],
  5: ["Paid", "success"],
  6: ["Rejected", "danger"],
  7: ["On hold", "danger"],
  8: ["Cancelled", "neutral"]
};
const STATUS_OPTIONS = [{ value: "", label: "Any status" }, ...Object.entries(BILL_STATUS).map(([v, [l]]) => ({ value: v, label: l }))];
const TERMS = [
  { value: "0", label: "Due on receipt" },
  { value: "7", label: "Net 7" },
  { value: "15", label: "Net 15" },
  { value: "30", label: "Net 30" },
  { value: "45", label: "Net 45" },
  { value: "60", label: "Net 60" },
  { value: "custom", label: "Custom" }
];
const VP_MODES = [
  { value: "1", label: "NEFT" },
  { value: "2", label: "RTGS" },
  { value: "3", label: "IMPS" },
  { value: "4", label: "UPI" },
  { value: "5", label: "Cheque" },
  { value: "6", label: "Cash" }
];
const STATES = [
  ["01", "Jammu and Kashmir"], ["02", "Himachal Pradesh"], ["03", "Punjab"], ["04", "Chandigarh"], ["05", "Uttarakhand"], ["06", "Haryana"],
  ["07", "Delhi"], ["08", "Rajasthan"], ["09", "Uttar Pradesh"], ["10", "Bihar"], ["11", "Sikkim"], ["12", "Arunachal Pradesh"], ["13", "Nagaland"],
  ["14", "Manipur"], ["15", "Mizoram"], ["16", "Tripura"], ["17", "Meghalaya"], ["18", "Assam"], ["19", "West Bengal"], ["20", "Jharkhand"],
  ["21", "Odisha"], ["22", "Chhattisgarh"], ["23", "Madhya Pradesh"], ["24", "Gujarat"], ["26", "Dadra and Nagar Haveli and Daman and Diu"],
  ["27", "Maharashtra"], ["29", "Karnataka"], ["30", "Goa"], ["31", "Lakshadweep"], ["32", "Kerala"], ["33", "Tamil Nadu"], ["34", "Puducherry"],
  ["35", "Andaman and Nicobar Islands"], ["36", "Telangana"], ["37", "Andhra Pradesh"], ["38", "Ladakh"]
];
const stateName = (code) => STATES.find(([c]) => c === code)?.[1] ?? code ?? "unknown";
const PAN_RE = /^[A-Z]{5}[0-9]{4}[A-Z]$/;
const PAN_MESSAGE = "A PAN is five letters, four digits and one letter — for example AABCI1234F.";

const statusPill = (status, overdue, daysLate) => {
  if (overdue) return pill(`Overdue · ${daysLate} day${daysLate === 1 ? "" : "s"}`, "danger");
  const [label, tone] = BILL_STATUS[status] ?? ["—", "neutral"];
  return pill(label, tone);
};
const query = () => new URLSearchParams(window.location.search);
const r2 = (n) => Math.round((Number(n) + Number.EPSILON) * 100) / 100;

// ── Purchase Bills list (doc §4.2-4.3) ───────────────────────────────────────

export function AdminPurchaseBillsPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canCreate = usePermission("BILL.CREATE");
  const initialSupplier = query().get("supplier") ?? "";
  const [quick, setQuick] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [supplierId, setSupplierId] = React.useState(initialSupplier);
  const [status, setStatus] = React.useState("");
  const [fy, setFy] = React.useState("");
  const [page, setPage] = React.useState(1);
  const debounced = useDebounced(search);
  const { data: summary } = useLoad(() => api.get("/purchase-bills/summary"), []);
  const { data: options } = useLoad(() => api.get("/purchase-bills/form-options"), []);
  const params = { page, pageSize: 20, search: debounced.trim() || undefined, supplierId: supplierId || undefined, status: status || undefined, quick: quick || undefined, financialYear: fy || undefined };
  const { data, loading } = useLoad(() => api.get("/purchase-bills", params), [page, debounced, supplierId, status, quick, fy]);
  React.useEffect(() => setPage(1), [debounced, supplierId, status, quick, fy]);
  const pick = (q) => setQuick((cur) => (cur === q ? "" : q));

  const columns = [
    { key: "bill", header: "Bill", render: (b) => e.jsxs("div", { children: [
      e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/purchase-bills/${b.purchaseBillId}`), children: b.supplierBillNo }),
      e.jsx("div", { className: "bf-sub", children: [fmtDate(b.billDate), b.poNumber].filter(Boolean).join(" · ") })
    ] }) },
    { key: "supplier", header: "Supplier", render: (b) => e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/ledger/supplier/${b.supplierId}`), children: b.supplierName }) },
    { key: "due", header: "Due", render: (b) => e.jsxs("div", { className: b.isOverdue ? "bf-red" : undefined, children: [
      fmtDate(b.dueDate), b.isOverdue ? e.jsx("div", { className: "bf-sub bf-red", children: `${b.daysLate} day${b.daysLate === 1 ? "" : "s"} late` }) : null
    ] }) },
    { key: "total", header: "Total", num: true, render: (b) => money(b.billTotal) },
    { key: "paid", header: "Paid", num: true, render: (b) => money(b.paid) },
    { key: "balance", header: "Balance", num: true, render: (b) => e.jsx("span", { className: "bf-strong", children: money(b.balance) }) },
    { key: "status", header: "Status", render: (b) => statusPill(b.status, false) },
    { key: "actions", header: "", render: (b) => e.jsxs("div", { className: "bf-actions", children: [
      e.jsx(Button, { size: "sm", variant: "outline", onClick: () => navigate(`/admin/billing/purchase-bills/${b.purchaseBillId}`), children: "View" }),
      b.canPay ? e.jsx(Button, { size: "sm", className: "btn--gold", onClick: () => navigate(`/admin/billing/vendor-payments?supplier=${b.supplierId}&bill=${b.purchaseBillId}`), children: "Pay this bill" }) : null,
      b.canEdit ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => navigate(`/admin/billing/purchase-bills/${b.purchaseBillId}/edit`), children: "Edit" }) : null
    ] }) }
  ];

  const supplierOptions = [{ value: "", label: "All suppliers" }, ...(options?.suppliers ?? []).map((s) => ({ value: String(s.supplierId), label: s.supplierName }))];

  return e.jsxs("div", { className: "page", children: [
    e.jsx(PageHeader, {
      title: "Purchase Bills",
      description: "What Regalis owes suppliers: their own invoices, recorded when they arrive, paid or not.",
      actions: e.jsxs("div", { className: "bf-head-actions", children: [
        e.jsxs(Button, { variant: "outline", onClick: () => api.download("/purchase-bills/export", { ...params, page: undefined, pageSize: undefined }, "purchase-bills.xlsx").catch((x) => toast.error(errorText(x, "Download failed."))),
          children: [e.jsx(IconDownload, { size: 16 }), " Export Excel"] }),
        canCreate ? e.jsxs(Button, { className: "btn--gold", onClick: () => navigate("/admin/billing/purchase-bills/new"), children: [e.jsx(IconPlus, { size: 16 }), " New bill"] }) : null
      ] })
    }),
    summary ? e.jsxs("div", { className: "bf-cards", children: [
      e.jsx(StatCard, { label: "Total outstanding", value: money(summary.totalOutstanding), sub: `${summary.outstandingCount} bill${summary.outstandingCount === 1 ? "" : "s"}`, onClick: () => pick("unpaid"), pressed: quick === "unpaid" }),
      e.jsx(StatCard, { label: "Due this week", value: money(summary.dueThisWeek), sub: `${summary.dueThisWeekCount} in the next 7 days`, onClick: () => pick("due-week"), pressed: quick === "due-week" }),
      e.jsx(StatCard, { label: "Overdue", value: money(summary.overdue), danger: summary.overdue > 0,
        sub: summary.overdueCount ? `${summary.overdueCount} bill${summary.overdueCount === 1 ? "" : "s"} · oldest ${summary.oldestOverdueDays} days` : "Nothing overdue",
        onClick: () => pick("overdue"), pressed: quick === "overdue" }),
      e.jsx(StatCard, { label: "Awaiting approval", value: String(summary.awaitingCount), sub: money(summary.awaitingValue), onClick: () => pick("awaiting"), pressed: quick === "awaiting" }),
      e.jsx(StatCard, { label: "Input GST this month", value: money(summary.inputGstThisMonth), sub: "Claimable in GSTR-3B", onClick: () => navigate("/admin/billing/gst") })
    ] }) : null,
    e.jsxs("div", { className: "bf-panel", children: [
      e.jsxs("div", { className: "bf-toolbar", children: [
        e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Bill number, supplier, PO" }),
        e.jsx(Select, { label: "Supplier", value: supplierId, options: supplierOptions, onChange: (ev) => setSupplierId(ev.target.value) }),
        e.jsx(Select, { label: "Status", value: status, options: STATUS_OPTIONS, onChange: (ev) => setStatus(ev.target.value) }),
        e.jsx(Select, { label: "Financial year", value: fy, options: [{ value: "", label: "All years" }, ...fyOptions()], onChange: (ev) => setFy(ev.target.value) })
      ] }),
      quick ? e.jsxs("div", { className: "bf-sub", style: { marginBottom: 8 }, children: [
        `Showing: ${{ unpaid: "everything unpaid", "due-week": "due in the next 7 days", overdue: "overdue", awaiting: "awaiting approval" }[quick]}. `,
        e.jsx("button", { type: "button", className: "bf-link", onClick: () => setQuick(""), children: "Clear" })
      ] }) : null,
      e.jsx(Table, {
        columns, rows: data?.page.items, loading, rowKey: (b) => b.purchaseBillId, rowClass: (b) => (b.isOverdue ? "bf-row--overdue" : ""),
        empty: e.jsx(EmptyState, { title: "No bills here", description: "A purchase bill is the supplier's own invoice. Record it when it arrives, paid or not." }),
        footer: data && data.filterCount > 0 ? e.jsxs("tr", { children: [
          e.jsx("td", { colSpan: 3, children: `Total · ${data.filterCount} bill${data.filterCount === 1 ? "" : "s"}` }),
          e.jsx("td", { className: "bf-num", children: money(data.filterBillTotal) }),
          e.jsx("td", { className: "bf-num", children: money(data.filterPaid) }),
          e.jsx("td", { className: "bf-num", children: money(data.filterBalance) }),
          e.jsx("td", { colSpan: 2 })
        ] }) : null
      }),
      data && data.filterCount > 0 ? e.jsx("p", { className: "bf-sub", children: "Totals cover every bill in this filter, not just this page." }) : null,
      e.jsx(Pager, { result: data?.page, page, setPage })
    ] })
  ] });
}

// ── New / edit bill (doc §4.4) ───────────────────────────────────────────────

const emptyLine = () => ({ description: "", hsnSac: "", quantity: "1", rate: "", gstRate: "18" });

export function AdminPurchaseBillFormPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const { id } = useParams();
  const editing = Boolean(id);
  const [options, setOptions] = React.useState(null);
  const [supplierId, setSupplierId] = React.useState("");
  const [poId, setPoId] = React.useState("");
  const [pos, setPos] = React.useState([]);
  const [billNo, setBillNo] = React.useState("");
  const [billDate, setBillDate] = React.useState(istToday());
  const [terms, setTerms] = React.useState("15");
  const [customDays, setCustomDays] = React.useState("");
  const [dueDate, setDueDate] = React.useState(addDays(istToday(), 15));
  const [dueTouched, setDueTouched] = React.useState(false);
  const [locationId, setLocationId] = React.useState("");
  const [tdsSection, setTdsSection] = React.useState("");
  const [lines, setLines] = React.useState([emptyLine()]);
  const [notes, setNotes] = React.useState("");
  const [files, setFiles] = React.useState([]);
  const [errors, setErrors] = React.useState({});
  const [duplicate, setDuplicate] = React.useState(null);
  const [busy, setBusy] = React.useState(false);

  const applyPrefill = (p) => {
    setPoId(String(p.purchaseOrderId));
    setSupplierId(String(p.supplierId));
    setLocationId(String(p.locationId));
    if (p.tdsSection) setTdsSection(p.tdsSection);
    if (p.lines?.length) setLines(p.lines.map((l) => ({ description: l.description, hsnSac: l.hsnSac ?? "", quantity: String(l.quantity), rate: String(l.rate), gstRate: String(l.gstRate) })));
    if (p.existingBills?.length) toast.info?.(`This PO already has bill${p.existingBills.length === 1 ? "" : "s"} ${p.existingBills.join(", ")}.`);
  };

  React.useEffect(() => {
    (async () => {
      try {
        const o = await api.get("/purchase-bills/form-options");
        setOptions(o);
        const q = query();
        if (editing) {
          const b = await api.get(`/purchase-bills/${id}`);
          setSupplierId(String(b.supplierId));
          setPoId(b.purchaseOrderId ? String(b.purchaseOrderId) : "");
          setBillNo(b.supplierBillNo);
          setBillDate(b.billDate);
          const t = TERMS.some((x) => x.value === String(b.paymentTermsDays)) ? String(b.paymentTermsDays) : "custom";
          setTerms(t);
          if (t === "custom") setCustomDays(String(b.paymentTermsDays));
          setDueDate(b.dueDate);
          setDueTouched(true);
          setLocationId(String(b.locationId));
          setTdsSection(b.tdsSection ?? "");
          setNotes(b.notes ?? "");
          setLines(b.lines.map((l) => ({ description: l.description, hsnSac: l.hsnSac ?? "", quantity: String(l.quantity), rate: String(l.rate), gstRate: String(l.gstRate) })));
        } else {
          setTerms(String(o.defaultTermsDays));
          setDueDate(addDays(istToday(), o.defaultTermsDays));
          const def = o.locations.find((l) => l.stateCode === o.homeStateCode) ?? o.locations[0];
          if (def) setLocationId(String(def.locationId));
          if (q.get("po")) applyPrefill(await api.get(`/purchase-bills/from-po/${q.get("po")}`));
          else if (q.get("supplier")) chooseSupplier(q.get("supplier"), o);
        }
      } catch (error) {
        toast.error(errorText(error, "Could not open the bill form."));
      }
    })();
  }, [id]); // eslint-disable-line react-hooks/exhaustive-deps

  React.useEffect(() => {
    if (!supplierId) { setPos([]); return; }
    api.get("/purchase-orders", { supplierId, pageSize: 100 })
      .then((r) => setPos((r.items ?? []).filter((p) => ![1, 5, 6].includes(p.status))))
      .catch(() => setPos([]));
  }, [supplierId]);

  const supplier = options?.suppliers.find((s) => String(s.supplierId) === supplierId) ?? null;
  const location = options?.locations.find((l) => String(l.locationId) === locationId) ?? null;
  const termDays = terms === "custom" ? Number(customDays || 0) : Number(terms);

  function chooseSupplier(value, o = options) {
    setSupplierId(value);
    const s = o?.suppliers.find((x) => String(x.supplierId) === String(value));
    if (!s) return;
    const days = s.paymentTermsDays ?? o.defaultTermsDays;
    const t = TERMS.some((x) => x.value === String(days)) ? String(days) : "custom";
    setTerms(t);
    if (t === "custom") setCustomDays(String(days));
    if (!dueTouched) setDueDate(addDays(billDate, days));
    setTdsSection(s.pan ? s.defaultTdsSection ?? "" : "");
  }

  React.useEffect(() => {
    if (!dueTouched && billDate) setDueDate(addDays(billDate, termDays || 0));
  }, [billDate, terms, customDays]); // eslint-disable-line react-hooks/exhaustive-deps

  // Live totals, worked out the way the server does.
  const placeOfSupply = location?.stateCode ?? options?.homeStateCode ?? null;
  const supplierState = supplier?.stateCode ?? null;
  const interState = Boolean(supplierState && placeOfSupply && supplierState !== placeOfSupply);
  const calc = lines.map((l) => {
    const taxable = r2((amountOf(l.quantity) ?? 0) * (amountOf(l.rate) ?? 0));
    const tax = r2(taxable * Number(l.gstRate || 0) / 100);
    return { taxable, tax };
  });
  const taxable = r2(calc.reduce((s, c) => s + c.taxable, 0));
  const gst = r2(calc.reduce((s, c) => s + c.tax, 0));
  const cgst = interState ? 0 : r2(calc.reduce((s, c) => s + r2(c.tax / 2), 0));
  const total = r2(taxable + gst);
  const section = options?.tdsSections.find((t) => t.section === tdsSection) ?? null;
  const tdsRate = section ? (supplier?.isIndividual && section.individualRate != null ? section.individualRate : section.rate) : 0;
  const tds = r2(taxable * tdsRate / 100);

  const setLine = (i, key, value) => setLines((ls) => ls.map((l, j) => (j === i ? { ...l, [key]: value } : l)));

  const save = async (submit, acknowledgeNoGstin = false) => {
    const errs = {};
    if (!supplierId) errs.supplier = "Choose the supplier.";
    if (!billNo.trim()) errs.billNo = "Enter the supplier's own bill number, exactly as printed on it.";
    if (!billDate) errs.billDate = "Enter the bill date.";
    else if (billDate > istToday()) errs.billDate = "The bill date cannot be in the future.";
    if (dueDate && billDate && dueDate < billDate) errs.dueDate = "The due date cannot be before the bill date.";
    if (lines.length === 0) errs.lines = "Add at least one line to the bill.";
    else if (lines.some((l) => !((amountOf(l.quantity) ?? 0) > 0) || !((amountOf(l.rate) ?? 0) > 0))) errs.lines = "Enter a quantity and a rate on every line.";
    else if (lines.some((l) => !l.description.trim())) errs.lines = "Describe every line.";
    else if (lines.some((l) => Number(l.gstRate) > 0 && !l.hsnSac.trim())) errs.lines = "Enter the HSN or SAC code for a line that carries GST.";
    setErrors(errs);
    setDuplicate(null);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const body = {
        supplierId: Number(supplierId),
        purchaseOrderId: poId ? Number(poId) : null,
        supplierBillNo: billNo.trim(),
        billDate,
        paymentTermsDays: termDays,
        dueDate,
        locationId: locationId ? Number(locationId) : null,
        tdsSection: tdsSection || null,
        notes: notes.trim() || null,
        lines: lines.map((l) => ({ description: l.description.trim(), hsnSac: l.hsnSac.trim() || null, quantity: amountOf(l.quantity), rate: amountOf(l.rate), gstRate: Number(l.gstRate) })),
        submit,
        acknowledgeNoGstin
      };
      const bill = editing ? await api.put(`/purchase-bills/${id}`, body) : await api.post("/purchase-bills", body);
      for (const f of files) {
        try { await api.upload(`/purchase-bills/${bill.purchaseBillId}/attachments`, {}, f); }
        catch (error) { toast.error(`${f.name}: ${errorText(error, "could not be attached.")}`); }
      }
      toast.success(submit
        ? (bill.status === 3 ? `Bill ${bill.supplierBillNo} saved and approved.` : `Bill ${bill.supplierBillNo} saved and sent for approval.`)
        : `Bill ${bill.supplierBillNo} saved as a draft.`);
      navigate(`/admin/billing/purchase-bills/${bill.purchaseBillId}`);
    } catch (error) {
      const code = errorCode(error) ?? "";
      if (code === "NO_GSTIN_CONFIRM" && !acknowledgeNoGstin) {
        setBusy(false);
        if (window.confirm(errorText(error, "This supplier has no GSTIN, so no input credit can be claimed on this bill. Continue?"))) await save(submit, true);
        return;
      }
      if (code.startsWith("DUPLICATE_BILL_")) setDuplicate({ id: code.slice("DUPLICATE_BILL_".length), message: errorText(error, "This bill is already here.") });
      else toast.error(errorText(error, "Could not save the bill."));
    } finally {
      setBusy(false);
    }
  };

  if (!options) return e.jsx(Spinner, { center: true });

  const supplierOptions = [{ value: "", label: "Choose the supplier" }, ...options.suppliers.filter((s) => s.isActive || String(s.supplierId) === supplierId)
    .map((s) => ({ value: String(s.supplierId), label: `${s.supplierName} (${s.supplierCode})` }))];
  const poOptions = [{ value: "", label: "No purchase order" }, ...pos.map((p) => ({ value: String(p.purchaseOrderId), label: `${p.poNumber} · ${money(p.totalAmount)}` }))];
  const gstOptions = options.gstRates.map((r) => ({ value: String(r), label: `${r}%` }));
  const tdsOptions = [{ value: "", label: "No TDS" }, ...options.tdsSections.map((t) => ({ value: t.section, label: `${t.section} · ${t.description} · ${t.rate}%` }))];

  return e.jsxs("div", { className: "page", children: [
    e.jsx(Crumbs, { items: [{ label: "Billing & Finance" }, { label: "Purchase Bills", to: "/admin/billing/purchase-bills" }, { label: editing ? "Edit bill" : "New bill" }] }),
    e.jsx(PageHeader, { title: editing ? "Edit purchase bill" : "New purchase bill", description: `Regalis number ${editing ? "kept" : options.nextBillNumber + " (preview)"} · the supplier's own number, date and GST go below.` }),
    duplicate ? e.jsxs("div", { className: "bf-note bf-note--danger", style: { marginBottom: 16 }, children: [
      duplicate.message, " ",
      e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/purchase-bills/${duplicate.id}`), children: "Open it" })
    ] }) : null,
    e.jsxs("div", { className: "bf-panel", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Select, { label: "Supplier", required: true, value: supplierId, error: errors.supplier, options: supplierOptions, hint: "New suppliers are added on the Suppliers page.",
          onChange: (ev) => chooseSupplier(ev.target.value) }),
        e.jsx(Select, { label: "Against PO", value: poId, options: poOptions, disabled: !supplierId, onChange: async (ev) => {
          const v = ev.target.value;
          setPoId(v);
          if (v) {
            try { applyPrefill(await api.get(`/purchase-bills/from-po/${v}`)); }
            catch (error) { toast.error(errorText(error, "Could not read that purchase order.")); setPoId(""); }
          }
        } }),
        e.jsx(Input, { label: "Their bill number", required: true, maxLength: 60, value: billNo, error: errors.billNo, hint: "Exactly as printed on the supplier's bill.", onChange: (ev) => setBillNo(ev.target.value) }),
        e.jsx(Input, { label: "Bill date", type: "date", required: true, max: istToday(), value: billDate, error: errors.billDate, hint: "The supplier's date, not today.", onChange: (ev) => setBillDate(ev.target.value) }),
        e.jsx(Select, { label: "Payment terms", required: true, value: terms, options: TERMS, onChange: (ev) => { setTerms(ev.target.value); setDueTouched(false); } }),
        terms === "custom" ? e.jsx(Input, { label: "Days", inputMode: "numeric", value: customDays, onChange: (ev) => { setCustomDays(ev.target.value.replace(/\D/g, "")); setDueTouched(false); } }) : null,
        e.jsx(Input, { label: "Due date", type: "date", required: true, min: billDate, value: dueDate, error: errors.dueDate, hint: "Worked out from the terms; a supplier sometimes gives extra time.",
          onChange: (ev) => { setDueDate(ev.target.value); setDueTouched(true); } }),
        e.jsx(Select, { label: "Location", value: locationId, options: options.locations.map((l) => ({ value: String(l.locationId), label: l.locationName })), onChange: (ev) => setLocationId(ev.target.value) }),
        e.jsx(Select, { label: "TDS section", value: tdsSection, options: tdsOptions, disabled: Boolean(supplier && !supplier.pan),
          hint: supplier && !supplier.pan ? "Add the supplier's PAN first (Suppliers page)." : "From the supplier's record; change it if this bill is different.",
          onChange: (ev) => setTdsSection(ev.target.value) })
      ] })
    ] }),
    e.jsxs("div", { className: "bf-panel", children: [
      e.jsx("h3", { className: "bf-panel__title", children: "Lines" }),
      errors.lines ? e.jsx("div", { className: "bf-note bf-note--danger", style: { marginBottom: 12 }, children: errors.lines }) : null,
      e.jsx("div", { className: "bf-table-wrap", children: e.jsxs("table", { className: "bf-table", children: [
        e.jsx("thead", { children: e.jsxs("tr", { children: [
          e.jsx("th", { children: "Description" }), e.jsx("th", { children: "HSN/SAC" }), e.jsx("th", { className: "bf-num", children: "Qty" }),
          e.jsx("th", { className: "bf-num", children: "Rate" }), e.jsx("th", { children: "GST %" }), e.jsx("th", { className: "bf-num", children: "Taxable" }), e.jsx("th", {})
        ] }) }),
        e.jsx("tbody", { children: lines.map((l, i) => e.jsxs("tr", { children: [
          e.jsx("td", { style: { minWidth: 220 }, children: e.jsx("input", { className: "field__input", "aria-label": "Description", maxLength: 300, value: l.description, onChange: (ev) => setLine(i, "description", ev.target.value) }) }),
          e.jsx("td", { style: { width: 120 }, children: e.jsx("input", { className: "field__input", "aria-label": "HSN or SAC", maxLength: 10, value: l.hsnSac, onChange: (ev) => setLine(i, "hsnSac", ev.target.value) }) }),
          e.jsx("td", { style: { width: 90 }, children: e.jsx("input", { className: "field__input bf-num", "aria-label": "Quantity", inputMode: "decimal", value: l.quantity, onChange: (ev) => setLine(i, "quantity", ev.target.value) }) }),
          e.jsx("td", { style: { width: 130 }, children: e.jsx("input", { className: "field__input bf-num", "aria-label": "Rate", inputMode: "decimal", value: l.rate, onChange: (ev) => setLine(i, "rate", ev.target.value) }) }),
          e.jsx("td", { style: { width: 110 }, children: e.jsx("select", { className: "field__input", "aria-label": "GST rate", value: l.gstRate, onChange: (ev) => setLine(i, "gstRate", ev.target.value),
            children: gstOptions.map((o) => e.jsx("option", { value: o.value, children: o.label }, o.value)) }) }),
          e.jsx("td", { className: "bf-num", children: money(calc[i].taxable) }),
          e.jsx("td", { children: lines.length > 1 ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setLines((ls) => ls.filter((_, j) => j !== i)), children: "Remove" }) : null })
        ] }, i)) })
      ] }) }),
      e.jsx("div", { style: { marginTop: 10 }, children: e.jsxs(Button, { variant: "outline", size: "sm", onClick: () => setLines((ls) => [...ls, emptyLine()]), children: [e.jsx(IconPlus, { size: 14 }), " Add line"] }) })
    ] }),
    e.jsxs("div", { className: "bf-split", style: { marginTop: 20 }, children: [
      e.jsxs("div", { className: "bf-panel", children: [
        e.jsx("h3", { className: "bf-panel__title", children: "Attachment" }),
        e.jsx(DropZone, { accept: ".pdf,.jpg,.jpeg,.png", multiple: true, label: "Strongly advised: drop the supplier's bill here, or browse. PDF, JPG, PNG, up to 10 MB each.",
          onFile: (f) => setFiles((fs) => [...fs, f]) }),
        files.length ? e.jsx("ul", { className: "bf-sub", children: files.map((f, i) => e.jsxs("li", { children: [f.name, " ",
          e.jsx("button", { type: "button", className: "bf-link", onClick: () => setFiles((fs) => fs.filter((_, j) => j !== i)), children: "remove" })] }, i)) }) : null,
        e.jsx("div", { style: { height: 12 } }),
        e.jsx(Input, { label: "Notes", maxLength: 500, value: notes, onChange: (ev) => setNotes(ev.target.value) })
      ] }),
      e.jsxs("div", { className: "bf-panel", children: [
        e.jsx("h3", { className: "bf-panel__title", children: "Totals" }),
        e.jsxs("div", { className: "bf-sum", children: [
          e.jsxs("div", { children: [e.jsx("span", { children: "Taxable value" }), e.jsx("span", { children: money(taxable) })] }),
          interState
            ? e.jsxs("div", { children: [e.jsx("span", { children: "IGST" }), e.jsx("span", { children: money(gst) })] })
            : e.jsxs(e.Fragment, { children: [
              e.jsxs("div", { children: [e.jsx("span", { children: "CGST" }), e.jsx("span", { children: money(cgst) })] }),
              e.jsxs("div", { children: [e.jsx("span", { children: "SGST" }), e.jsx("span", { children: money(r2(gst - cgst)) })] })
            ] }),
          e.jsxs("div", { className: "bf-strong", children: [e.jsx("span", { children: "Bill total" }), e.jsx("span", { children: money(total) })] }),
          e.jsxs("div", { className: "bf-red", children: [e.jsx("span", { children: section ? `TDS ${section.section} @ ${tdsRate}%` : "TDS" }), e.jsx("span", { children: `− ${money(tds)}` })] }),
          e.jsxs("div", { className: "bf-sum__total", children: [e.jsx("span", { children: "Payable to supplier" }), e.jsx("span", { children: money(r2(total - tds)) })] })
        ] }),
        e.jsx("p", { className: "bf-sub", children: !supplier ? "Choose the supplier to see how GST is split."
          : interState
            ? `GST is IGST because the supplier's state (${stateName(supplierState)}) differs from the location's (${stateName(placeOfSupply)}).`
            : supplierState
              ? `GST is CGST+SGST because the supplier's state matches the location's (${stateName(placeOfSupply)}). A supplier in another state gives IGST.`
              : "GST is CGST+SGST: the supplier's state is not known, so the location's state is assumed. Add the supplier's state or GSTIN to be sure." }),
        supplier && !supplier.gstin && gst > 0 ? e.jsx("div", { className: "bf-note bf-note--warn", children: "This supplier has no GSTIN, so no input credit can be claimed on this bill." }) : null
      ] })
    ] }),
    e.jsxs("div", { className: "bf-actions", style: { marginTop: 20 }, children: [
      e.jsx(Button, { variant: "outline", onClick: () => navigate(editing ? `/admin/billing/purchase-bills/${id}` : "/admin/billing/purchase-bills"), disabled: busy, children: "Cancel" }),
      e.jsx(Button, { variant: "outline", onClick: () => save(false), loading: busy, disabled: busy, children: "Save as draft" }),
      e.jsx(Button, { className: "btn--gold", onClick: () => save(true), loading: busy, disabled: busy, children: total > options.approvalLimit ? "Save & send for approval" : "Save & submit" })
    ] })
  ] });
}

// ── Bill detail (doc §5) ─────────────────────────────────────────────────────

export function AdminPurchaseBillDetailPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const { id } = useParams();
  const [tab, setTab] = React.useState("overview");
  const [dialog, setDialog] = React.useState(null);
  const { data: bill, setData, loading } = useLoad(() => api.get(`/purchase-bills/${id}`), [id]);

  if (!bill) return loading ? e.jsx(Spinner, { center: true }) : e.jsx(EmptyState, { title: "Bill not found" });

  const act = async (path, body, done) => {
    try {
      setData(await api.post(`/purchase-bills/${id}/${path}`, body));
      toast.success(done);
      setDialog(null);
    } catch (error) {
      toast.error(errorText(error, "That did not work."));
      throw error;
    }
  };
  const pdf = bill.attachments.find((a) => a.contentType === "application/pdf");
  const upload = async (file) => {
    try { setData(await api.upload(`/purchase-bills/${id}/attachments`, {}, file)); toast.success(`${file.name} added.`); }
    catch (error) { toast.error(errorText(error, "Could not add the document.")); }
  };

  const lineCols = [
    { key: "d", header: "Description", render: (l) => l.description },
    { key: "h", header: "HSN/SAC", render: (l) => l.hsnSac ?? "—" },
    { key: "q", header: "Qty", num: true, render: (l) => l.quantity },
    { key: "r", header: "Rate", num: true, render: (l) => money(l.rate) },
    { key: "t", header: "Taxable", num: true, render: (l) => money(l.taxableValue) },
    { key: "g", header: "GST %", num: true, render: (l) => `${l.gstRate}%` }
  ];
  const payCols = [
    { key: "d", header: "Date", render: (p) => fmtDate(p.paidOn) },
    { key: "n", header: "Payment", render: (p) => e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/vendor-payments?search=${encodeURIComponent(p.paymentNumber)}`), children: p.paymentNumber }) },
    { key: "m", header: "Mode", render: (p) => p.mode },
    { key: "r", header: "Reference", render: (p) => p.reference ?? "—" },
    { key: "a", header: "Paid", num: true, render: (p) => money(p.amount) },
    { key: "t", header: "TDS", num: true, render: (p) => money(p.tdsAmount) },
    { key: "s", header: "Bank", render: (p) => (p.isReversed ? pill("Reversed", "neutral") : p.isReconciled ? pill("Reconciled", "success") : pill("Not reconciled", "warning")) }
  ];
  const livePays = bill.payments.filter((p) => !p.isReversed);
  const approvedEvent = [...bill.history].reverse().find((h) => h.eventType === "Approved" || h.eventType === "Rejected");
  const steps = [
    { done: true, title: "Entered", text: `${bill.enteredBy ?? "—"} · ${fmtDate(bill.createdDate)}` },
    bill.status === 6
      ? { done: true, red: true, title: "Rejected", text: `${approvedEvent?.userName ?? ""} · ${approvedEvent ? fmtDate(approvedEvent.eventUtc) : ""} — ${bill.rejectReason ?? ""}` }
      : bill.approvedUtc
        ? { done: true, title: "Approved", text: `${bill.approvedBy ?? "automatically"} · ${fmtDate(bill.approvedUtc)}${bill.approvalComment ? ` — ${bill.approvalComment}` : ""}` }
        : { done: false, title: bill.status === 2 ? "Waiting for approval" : "Approval", text: bill.submittedUtc ? `Submitted ${fmtDate(bill.submittedUtc)}` : "Not submitted yet" },
    { done: bill.status === 5, title: "Fully paid", text: bill.status === 5 ? "Balance is zero." : `${money(bill.balance)} still to settle.` }
  ];

  return e.jsxs("div", { className: "page", children: [
    e.jsx(Crumbs, { items: [{ label: "Billing & Finance" }, { label: "Purchase Bills", to: "/admin/billing/purchase-bills" }, { label: bill.supplierBillNo }] }),
    e.jsx(PageHeader, {
      title: `Bill ${bill.supplierBillNo} — ${bill.supplierName}`,
      description: [`Dated ${fmtDate(bill.billDate)}`, `due ${fmtDate(bill.dueDate)}`, bill.poNumber ? `against ${bill.poNumber}` : null,
        bill.supplierGstin ? `GSTIN ${bill.supplierGstin}` : "No supplier GSTIN", bill.billNumber].filter(Boolean).join(" · "),
      actions: e.jsxs("div", { className: "bf-head-actions", children: [
        statusPill(bill.status, bill.isOverdue, bill.daysLate),
        pdf ? e.jsx(Button, { variant: "outline", onClick: () => api.open(`/purchase-bills/${id}/attachments/${pdf.attachmentId}`).catch((x) => toast.error(errorText(x, "Could not open the PDF."))), children: "Open the PDF" }) : null,
        bill.canEdit ? e.jsx(Button, { variant: "outline", onClick: () => navigate(`/admin/billing/purchase-bills/${id}/edit`), children: "Edit" }) : null,
        bill.canSubmit ? e.jsx(Button, { variant: "outline", onClick: () => act("submit", {}, "Bill submitted.").catch(() => {}), children: "Submit" }) : null,
        bill.canApprove ? e.jsx(Button, { variant: "outline", onClick: () => act("approve", { comment: null }, "Bill approved.").catch(() => {}), children: "Approve" }) : null,
        bill.canApprove ? e.jsx(Button, { variant: "outline", onClick: () => setDialog("reject"), children: "Reject" }) : null,
        bill.canHold && bill.status !== 7 ? e.jsx(Button, { variant: "outline", onClick: () => setDialog("hold"), children: "Hold" }) : null,
        bill.canHold && bill.status === 7 ? e.jsx(Button, { variant: "outline", onClick: () => act("release", {}, "Bill released.").catch(() => {}), children: "Release" }) : null,
        bill.canCancel ? e.jsx(Button, { variant: "ghost", onClick: () => setDialog("cancel"), children: "Cancel bill" }) : null,
        bill.canPay ? e.jsx(Button, { className: "btn--gold", onClick: () => navigate(`/admin/billing/vendor-payments?supplier=${bill.supplierId}&bill=${bill.purchaseBillId}`), children: "Pay this bill" }) : null
      ] })
    }),
    bill.status === 2 && !bill.canApprove ? e.jsx("div", { className: "bf-note bf-note--warn", children: `This bill is above the ${money(bill.approvalLimit)} approval limit and waits for someone with BILL.APPROVE — not the person who entered it.` }) : null,
    bill.holdReason ? e.jsx("div", { className: "bf-note bf-note--danger", children: `On hold: ${bill.holdReason}` }) : null,
    bill.cancelReason ? e.jsx("div", { className: "bf-note bf-note--danger", children: `Cancelled: ${bill.cancelReason}` }) : null,
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [
      { value: "overview", label: "Overview" }, { value: "payments", label: `Payments (${bill.payments.length})` }, { value: "approval", label: "Approval" },
      { value: "attachments", label: `Attachments (${bill.attachments.length})` }, { value: "history", label: "History" }] }),
    tab === "overview" ? e.jsxs(e.Fragment, { children: [
      e.jsx("div", { className: "bf-panel", children: e.jsxs("dl", { className: "bf-kv", style: { margin: 0 }, children: [
        e.jsxs("div", { children: [e.jsx("dt", { children: "Supplier bill number" }), e.jsx("dd", { children: bill.supplierBillNo })] }),
        e.jsxs("div", { children: [e.jsx("dt", { children: "Bill date" }), e.jsx("dd", { children: fmtDate(bill.billDate) })] }),
        e.jsxs("div", { children: [e.jsx("dt", { children: "Payment terms" }), e.jsx("dd", { children: bill.paymentTermsDays === 0 ? "Due on receipt" : `Net ${bill.paymentTermsDays}` })] }),
        e.jsxs("div", { children: [e.jsx("dt", { children: "Due date" }), e.jsx("dd", { className: bill.isOverdue ? "bf-red" : undefined, children: fmtDate(bill.dueDate) })] }),
        e.jsxs("div", { children: [e.jsx("dt", { children: "Place of supply" }), e.jsx("dd", { children: bill.placeOfSupplyName ?? bill.placeOfSupplyCode ?? "—" })] }),
        e.jsxs("div", { children: [e.jsx("dt", { children: "Entered by" }), e.jsx("dd", { children: bill.enteredBy ?? "—" })] })
      ] }) }),
      e.jsxs("div", { className: "bf-panel", children: [
        e.jsx("h3", { className: "bf-panel__title", children: "What was billed" }),
        e.jsx(Table, { columns: lineCols, rows: bill.lines, rowKey: (l) => l.lineNumber }),
        e.jsx("p", { className: "bf-sub", children: bill.gstReason })
      ] }),
      e.jsxs("div", { className: "bf-split", style: { marginTop: 20 }, children: [
        e.jsxs("div", { className: "bf-panel", children: [
          e.jsx("h3", { className: "bf-panel__title", children: "What the bill says" }),
          e.jsxs("div", { className: "bf-sum", children: [
            e.jsxs("div", { children: [e.jsx("span", { children: "Taxable value" }), e.jsx("span", { children: money(bill.taxableValue) })] }),
            bill.isInterState
              ? e.jsxs("div", { children: [e.jsx("span", { children: "IGST" }), e.jsx("span", { children: money(bill.igstAmount) })] })
              : e.jsxs(e.Fragment, { children: [
                e.jsxs("div", { children: [e.jsx("span", { children: "CGST" }), e.jsx("span", { children: money(bill.cgstAmount) })] }),
                e.jsxs("div", { children: [e.jsx("span", { children: "SGST" }), e.jsx("span", { children: money(bill.sgstAmount) })] })
              ] }),
            e.jsxs("div", { className: "bf-sum__total", children: [e.jsx("span", { children: "Bill total" }), e.jsx("span", { children: money(bill.billTotal) })] })
          ] })
        ] }),
        e.jsxs("div", { className: "bf-panel", children: [
          e.jsx("h3", { className: "bf-panel__title", children: "What Regalis pays" }),
          e.jsxs("div", { className: "bf-sum", children: [
            e.jsxs("div", { children: [e.jsx("span", { children: "Bill total" }), e.jsx("span", { children: money(bill.billTotal) })] }),
            e.jsxs("div", { className: "bf-red", children: [e.jsx("span", { children: bill.tdsSection ? `Less TDS ${bill.tdsSection} @ ${bill.tdsRate}%` : "Less TDS" }), e.jsx("span", { children: `− ${money(Math.max(bill.tdsAmount, bill.tdsDeducted))}` })] }),
            e.jsxs("div", { className: "bf-red", children: [e.jsx("span", { children: "Less paid so far" }), e.jsx("span", { children: `− ${money(bill.paid - bill.tdsDeducted)}` })] }),
            e.jsxs("div", { className: "bf-sum__total", children: [e.jsx("span", { children: "Still to pay" }), e.jsx("span", { children: money(bill.stillToPay) })] })
          ] }),
          e.jsx("p", { className: "bf-sub", children: "A supplier's bill and what Regalis actually pays are different numbers: the TDS held back goes to the government, not the supplier." })
        ] })
      ] })
    ] }) : null,
    tab === "payments" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsx(Table, { columns: payCols, rows: bill.payments, rowKey: (p) => p.vendorPaymentId, rowClass: (p) => (p.isReversed ? "bf-row--muted" : ""),
        empty: e.jsx(EmptyState, { title: "No payments yet", description: bill.canPay ? "Use Pay this bill to settle it." : undefined }),
        footer: livePays.length ? e.jsxs("tr", { children: [
          e.jsx("td", { colSpan: 4, children: "Total" }),
          e.jsx("td", { className: "bf-num", children: money(livePays.reduce((s, p) => s + p.amount, 0)) }),
          e.jsx("td", { className: "bf-num", children: money(livePays.reduce((s, p) => s + p.tdsAmount, 0)) }),
          e.jsx("td", {})
        ] }) : null }),
      e.jsx("p", { className: "bf-sub", children: "One bill can have many payments, and one payment can cover many bills. The allocation is what links them." })
    ] }) : null,
    tab === "approval" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsx("ul", { className: "bf-timeline", children: steps.map((s, i) => e.jsxs("li", { children: [
        e.jsx("span", { className: `bf-dot${s.done ? (s.red ? " bf-dot--red" : "") : " bf-dot--open"}` }),
        e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: s.title }), e.jsx("div", { className: "bf-sub", children: s.text })] })
      ] }, i)) }),
      e.jsx("p", { className: "bf-sub", style: { marginTop: 16 }, children: `A bill above the ${money(bill.approvalLimit)} approval limit cannot be paid until somebody with BILL.APPROVE approves it, and that cannot be the person who entered it.` })
    ] }) : null,
    tab === "attachments" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsx(Table, { rows: bill.attachments, rowKey: (a) => a.attachmentId, empty: e.jsx(EmptyState, { title: "No documents yet" }), columns: [
        { key: "n", header: "File", render: (a) => e.jsx("button", { type: "button", className: "bf-link", onClick: () => api.download(`/purchase-bills/${id}/attachments/${a.attachmentId}`, null, a.fileName).catch((x) => toast.error(errorText(x, "Download failed."))), children: a.fileName }) },
        { key: "t", header: "Type", render: (a) => (a.contentType === "application/pdf" ? "PDF" : a.contentType === "image/png" ? "PNG" : "JPG") },
        { key: "s", header: "Size", num: true, render: (a) => `${Math.max(1, Math.round(a.sizeBytes / 1024))} KB` },
        { key: "u", header: "Uploaded", render: (a) => `${fmtDate(a.uploadedUtc)}${a.uploadedBy ? ` · ${a.uploadedBy}` : ""}` }
      ] }),
      bill.status !== 8 ? e.jsx("div", { style: { marginTop: 16 }, children: e.jsx(DropZone, { accept: ".pdf,.jpg,.jpeg,.png", label: "+ Add a document (PDF, JPG or PNG, up to 10 MB)", onFile: upload }) }) : null
    ] }) : null,
    tab === "history" ? e.jsxs("div", { className: "bf-panel", children: [
      e.jsx(Table, { rows: [...bill.history].reverse(), rowKey: (h) => `${h.eventUtc}-${h.eventType}-${h.comment ?? ""}`, columns: [
        { key: "w", header: "When", render: (h) => fmtDate(h.eventUtc) },
        { key: "e", header: "Event", render: (h) => e.jsx("span", { className: "bf-strong", children: h.eventType }) },
        { key: "c", header: "Detail", render: (h) => h.comment ?? "" },
        { key: "u", header: "Who", render: (h) => h.userName ?? "—" }
      ] }),
      e.jsx("p", { className: "bf-sub", children: "Every event on this bill, with who did it and when. It is never edited and never deleted." })
    ] }) : null,
    dialog === "reject" ? e.jsx(ReasonModal, { title: `Reject ${bill.supplierBillNo}`, intro: "The bill goes back to the person who entered it, who can correct it and resubmit.", confirmText: "Reject bill", danger: true,
      onClose: () => setDialog(null), onSubmit: (reason) => act("reject", { comment: reason }, "Bill rejected.") }) : null,
    dialog === "hold" ? e.jsx(ReasonModal, { title: `Put ${bill.supplierBillNo} on hold`, intro: "A bill on hold cannot be paid until it is released.", label: "What is the dispute?", confirmText: "Put on hold",
      onClose: () => setDialog(null), onSubmit: (reason) => act("hold", { comment: reason }, "Bill put on hold.") }) : null,
    dialog === "cancel" ? e.jsx(ReasonModal, { title: `Cancel ${bill.supplierBillNo}`, intro: "Only a bill entered in error, before any payment, is cancelled. It stays on record.", confirmText: "Cancel bill", danger: true,
      onClose: () => setDialog(null), onSubmit: (reason) => act("cancel", { comment: reason }, "Bill cancelled.") }) : null
  ] });
}

// ── Vendor payments (doc §6) ─────────────────────────────────────────────────

/** One payment, several bills, less TDS; with no bill ticked it is an advance. */
export function NewVendorPaymentDialog({ initialSupplierId, initialBillId, onClose, onDone }) {
  const toast = useToast();
  const [options, setOptions] = React.useState(null);
  const [accounts, setAccounts] = React.useState([]);
  const [supplierId, setSupplierId] = React.useState(initialSupplierId ? String(initialSupplierId) : "");
  const [payables, setPayables] = React.useState(null);
  const [paidOn, setPaidOn] = React.useState(istToday());
  const [from, setFrom] = React.useState("");
  const [mode, setMode] = React.useState("1");
  const [reference, setReference] = React.useState("");
  const [note, setNote] = React.useState("");
  const [rows, setRows] = React.useState({});
  const [useAdvance, setUseAdvance] = React.useState(false);
  const [advance, setAdvance] = React.useState("");
  const [advancePay, setAdvancePay] = React.useState("");
  const [errors, setErrors] = React.useState({});
  const [busy, setBusy] = React.useState(false);

  React.useEffect(() => {
    api.get("/purchase-bills/form-options").then(setOptions).catch(() => setOptions({ suppliers: [] }));
    api.get("/bank/accounts").then((list) => {
      const active = (Array.isArray(list) ? list : list?.items ?? []).filter((a) => a.isActive !== false);
      setAccounts(active);
      if (active.length === 1) setFrom(String(active[0].bankAccountId));
    }).catch(() => setAccounts([]));
  }, []);

  React.useEffect(() => {
    setPayables(null);
    setRows({});
    setUseAdvance(false);
    if (!supplierId) return;
    api.get(`/suppliers/${supplierId}/outstanding-bills`).then((p) => {
      setPayables(p);
      if (initialBillId && String(supplierId) === String(initialSupplierId)) {
        const b = p.bills.find((x) => String(x.purchaseBillId) === String(initialBillId));
        if (b) setRows({ [b.purchaseBillId]: { on: true, allocate: String(r2(b.balance - b.tdsOnBalance)), tds: b.tdsOnBalance } });
      }
    }).catch((error) => toast.error(errorText(error, "Could not load the supplier's bills.")));
  }, [supplierId]); // eslint-disable-line react-hooks/exhaustive-deps

  const petty = from === "petty";
  React.useEffect(() => { if (petty) setMode("6"); }, [petty]);
  const cash = mode === "6";
  const tick = (b, on) => setRows((rs) => ({ ...rs, [b.purchaseBillId]: on ? { on: true, allocate: String(r2(b.balance - b.tdsOnBalance)), tds: b.tdsOnBalance } : { on: false } }));
  const ticked = (payables?.bills ?? []).filter((b) => rows[b.purchaseBillId]?.on);
  const allocTotal = r2(ticked.reduce((s, b) => s + (amountOf(rows[b.purchaseBillId].allocate) ?? 0), 0));
  const tdsTotal = r2(ticked.reduce((s, b) => s + (rows[b.purchaseBillId].tds ?? 0), 0));
  const billTotal = r2(ticked.reduce((s, b) => s + b.balance, 0));
  const advUsed = useAdvance ? (amountOf(advance) ?? 0) : 0;
  const toPay = ticked.length ? r2(allocTotal - advUsed) : (amountOf(advancePay) ?? 0);

  const submit = async () => {
    const errs = {};
    if (!supplierId) errs.supplier = "Choose the supplier.";
    if (!from) errs.from = "Choose which account the money is paid from.";
    if (!cash && !reference.trim()) errs.reference = "Enter the UTR or cheque number.";
    if (!paidOn || paidOn > istToday()) errs.paidOn = "The payment date cannot be in the future.";
    for (const b of ticked) {
      const a = amountOf(rows[b.purchaseBillId].allocate);
      const max = r2(b.balance - (rows[b.purchaseBillId].tds ?? 0));
      if (a === null || a <= 0) errs.alloc = `Enter the amount to allocate to ${b.supplierBillNo}.`;
      else if (a > max) errs.alloc = `${money(max)} is the most that can be allocated to ${b.supplierBillNo}.`;
    }
    if (useAdvance && advUsed > (payables?.advanceAvailable ?? 0)) errs.advance = `Only ${money(payables.advanceAvailable)} of advance is available with this supplier.`;
    if (useAdvance && advUsed > allocTotal) errs.advance = "The advance adjusted cannot be more than the bills being paid.";
    if (!ticked.length && !(toPay > 0)) errs.alloc = "Tick the bills this payment settles, or save it as an advance.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const r = await api.postWithMessage("/vendor-payments", {
        supplierId: Number(supplierId),
        paidOn,
        bankAccountId: petty ? null : Number(from),
        fromPettyCash: petty,
        mode: Number(mode),
        reference: cash ? (reference.trim() || null) : reference.trim(),
        note: note.trim() || null,
        amount: toPay,
        advanceToAdjust: ticked.length ? advUsed : 0,
        allocations: ticked.map((b) => ({ purchaseBillId: b.purchaseBillId, amount: amountOf(rows[b.purchaseBillId].allocate), tdsAmount: rows[b.purchaseBillId].tds ?? 0 }))
      });
      toast.success(r.message || "Payment recorded.");
      onDone?.(r.data);
    } catch (error) {
      toast.error(errorText(error, "Could not record the payment."));
    } finally {
      setBusy(false);
    }
  };

  const supplierOptions = [{ value: "", label: "Choose the supplier" }, ...(options?.suppliers ?? []).map((s) => ({ value: String(s.supplierId), label: s.supplierName }))];
  const fromOptions = [{ value: "", label: "Choose" }, ...accounts.map((a) => ({ value: String(a.bankAccountId), label: `${a.bankName} · ${a.accountCode}` })), { value: "petty", label: "Petty cash" }];

  return e.jsx(Modal, {
    open: true,
    title: "New vendor payment",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { className: "btn--gold", onClick: submit, loading: busy, disabled: busy || !supplierId, children: ticked.length ? `Pay ${money(toPay)}` : "Save advance" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal--wide", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Select, { label: "Supplier", required: true, value: supplierId, error: errors.supplier, options: supplierOptions, onChange: (ev) => setSupplierId(ev.target.value) }),
        e.jsx(Input, { label: "Payment date", type: "date", required: true, max: istToday(), value: paidOn, error: errors.paidOn, onChange: (ev) => setPaidOn(ev.target.value) }),
        e.jsx(Select, { label: "Pay from", required: true, value: from, error: errors.from, options: fromOptions, onChange: (ev) => setFrom(ev.target.value) }),
        e.jsx(Select, { label: "Mode", required: true, value: mode, disabled: petty, options: VP_MODES, onChange: (ev) => setMode(ev.target.value) }),
        !cash ? e.jsx(Input, { label: "Reference / UTR", required: true, maxLength: 60, value: reference, error: errors.reference, hint: "What the bank line will be matched on later.", onChange: (ev) => setReference(ev.target.value) }) : null
      ] }),
      !supplierId ? null : !payables ? e.jsx(Spinner, { center: true }) : e.jsxs("div", { className: "bf-split", children: [
        e.jsxs("div", { children: [
          e.jsx("div", { className: "bf-strong", style: { marginBottom: 8 }, children: "Which bills this payment settles" }),
          errors.alloc ? e.jsx("div", { className: "bf-note bf-note--danger", style: { marginBottom: 8 }, children: errors.alloc }) : null,
          payables.bills.length === 0 ? e.jsx("div", { className: "bf-sub", children: "No approved, unpaid bills for this supplier." }) : e.jsx("div", { className: "bf-table-wrap", children: e.jsxs("table", { className: "bf-table", children: [
            e.jsx("thead", { children: e.jsxs("tr", { children: [e.jsx("th", {}), e.jsx("th", { children: "Bill" }), e.jsx("th", { children: "Due" }), e.jsx("th", { className: "bf-num", children: "Balance" }), e.jsx("th", { className: "bf-num", children: "TDS" }), e.jsx("th", { className: "bf-num", children: "Allocate" })] }) }),
            e.jsx("tbody", { children: payables.bills.map((b) => {
              const r = rows[b.purchaseBillId] ?? {};
              return e.jsxs("tr", { className: r.on ? undefined : "bf-row--muted", children: [
                e.jsx("td", { children: e.jsx("input", { type: "checkbox", "aria-label": `Pay ${b.supplierBillNo}`, checked: Boolean(r.on), onChange: (ev) => tick(b, ev.target.checked) }) }),
                e.jsx("td", { children: e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: b.supplierBillNo }), e.jsx("div", { className: "bf-sub", children: [fmtDate(b.billDate), b.poNumber].filter(Boolean).join(" · ") })] }) }),
                e.jsx("td", { className: b.isOverdue ? "bf-red" : undefined, children: fmtDate(b.dueDate) }),
                e.jsx("td", { className: "bf-num", children: money(b.balance) }),
                e.jsx("td", { className: "bf-num", children: r.on ? money(r.tds ?? 0) : money(b.tdsOnBalance) }),
                e.jsx("td", { className: "bf-num", style: { width: 130 }, children: r.on
                  ? e.jsx("input", { className: "field__input bf-num", inputMode: "decimal", "aria-label": `Allocate to ${b.supplierBillNo}`, value: r.allocate,
                    onChange: (ev) => setRows((rs) => ({ ...rs, [b.purchaseBillId]: { ...rs[b.purchaseBillId], allocate: ev.target.value } })) })
                  : e.jsx("span", { className: "bf-sub", children: "not selected" }) })
              ] }, b.purchaseBillId);
            }) })
          ] }) }),
          payables.notPayable.length ? e.jsxs("div", { style: { marginTop: 12 }, children: [
            e.jsx("div", { className: "bf-sub", children: "Not payable yet:" }),
            payables.notPayable.map((b) => e.jsx("div", { className: "bf-sub", children: `${b.supplierBillNo} · ${money(b.balance)} — ${b.status === 2 ? "awaiting approval" : b.status === 7 ? "on hold" : b.status === 6 ? "rejected" : "draft"}, cannot be paid yet` }, b.purchaseBillId))
          ] }) : null,
          payables.advanceAvailable > 0 && ticked.length ? e.jsxs("div", { className: "bf-note bf-note--info", style: { marginTop: 12 }, children: [
            e.jsxs("label", { style: { display: "flex", gap: 8, alignItems: "center" }, children: [
              e.jsx("input", { type: "checkbox", checked: useAdvance, onChange: (ev) => { setUseAdvance(ev.target.checked); if (ev.target.checked && !advance) setAdvance(String(Math.min(payables.advanceAvailable, allocTotal))); } }),
              `Advance already with this supplier: ${money(payables.advanceAvailable)}. Adjust it against these bills`
            ] }),
            useAdvance ? e.jsx(Input, { label: "Advance to adjust", inputMode: "decimal", value: advance, error: errors.advance, onChange: (ev) => setAdvance(ev.target.value) }) : null
          ] }) : null,
          !ticked.length ? e.jsx("div", { style: { marginTop: 12 }, children: e.jsx(Input, { label: "Or pay an advance (₹)", inputMode: "decimal", value: advancePay,
            hint: "With no bill ticked, the payment is an advance against the supplier. It sits unadjusted until a bill arrives.", onChange: (ev) => setAdvancePay(ev.target.value) }) }) : null
        ] }),
        e.jsxs("div", { children: [
          e.jsxs("div", { className: "bf-panel", children: [
            e.jsxs("div", { className: "bf-sum", children: [
              e.jsxs("div", { children: [e.jsx("span", { children: `Bills selected (${ticked.length})` }), e.jsx("span", { children: money(billTotal) })] }),
              e.jsxs("div", { className: "bf-red", children: [e.jsx("span", { children: "TDS deducted" }), e.jsx("span", { children: `− ${money(tdsTotal)}` })] }),
              e.jsxs("div", { className: "bf-red", children: [e.jsx("span", { children: "Advance adjusted" }), e.jsx("span", { children: `− ${money(advUsed)}` })] }),
              e.jsxs("div", { className: "bf-sum__total", children: [e.jsx("span", { children: "Amount to pay" }), e.jsx("span", { children: money(toPay) })] })
            ] }),
            e.jsx("p", { className: "bf-sub", children: "This is the figure that must match the bank statement line. Reconciliation uses it." })
          ] }),
          tdsTotal > 0 ? e.jsx("div", { className: "bf-note bf-note--info", style: { marginTop: 12 }, children:
            `The TDS, in plain words: Regalis keeps back ${money(tdsTotal)} from ${payables.supplierName} and pays it to the government instead. The supplier's bill is still fully settled; the ${money(tdsTotal)} becomes a TDS payable, waiting for a challan and then a certificate.` }) : null,
          e.jsx("div", { style: { marginTop: 12 }, children: e.jsx(Input, { label: "Note", maxLength: 300, value: note, onChange: (ev) => setNote(ev.target.value) }) })
        ] })
      ] })
    ] })
  });
}

export function AdminVendorPaymentsPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canPay = usePermission("BILL.PAY");
  const q = query();
  const [search, setSearch] = React.useState(q.get("search") ?? "");
  const [supplierId, setSupplierId] = React.useState("");
  const [reconciled, setReconciled] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [paying, setPaying] = React.useState(q.get("supplier") ? { supplier: q.get("supplier"), bill: q.get("bill") } : null);
  const [reversing, setReversing] = React.useState(null);
  const debounced = useDebounced(search);
  const { data: options } = useLoad(() => api.get("/purchase-bills/form-options"), []);
  const { data, loading, reload } = useLoad(() => api.get("/vendor-payments", { page, pageSize: 20, search: debounced.trim() || undefined, supplierId: supplierId || undefined, reconciled: reconciled || undefined }),
    [page, debounced, supplierId, reconciled]);
  React.useEffect(() => setPage(1), [debounced, supplierId, reconciled]);

  const columns = [
    { key: "d", header: "Date", render: (p) => fmtDate(p.paidOn) },
    { key: "n", header: "Payment", render: (p) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: p.paymentNumber }), e.jsx("div", { className: "bf-sub", children: p.paidFrom ?? "" })] }) },
    { key: "s", header: "Supplier", render: (p) => e.jsx("button", { type: "button", className: "bf-link", onClick: () => navigate(`/admin/billing/ledger/supplier/${p.supplierId}`), children: p.supplierName }) },
    { key: "m", header: "Mode", render: (p) => p.modeName },
    { key: "r", header: "Reference", render: (p) => p.reference ?? "—" },
    { key: "a", header: "Amount", num: true, render: (p) => e.jsx("span", { className: "bf-strong", children: money(p.amount) }) },
    { key: "t", header: "TDS", num: true, render: (p) => money(p.tdsAmount) },
    { key: "b", header: "Bills", render: (p) => (p.billCount ? e.jsx("span", { title: p.allocations.map((a) => `${a.supplierBillNo} ${money(a.amount)}`).join(", "), children: `${p.billCount} bill${p.billCount === 1 ? "" : "s"}` }) : pill("Advance", "info")) },
    { key: "st", header: "Bank", render: (p) => (p.isReversed ? pill("Reversed", "neutral") : p.fromPettyCash ? pill("Petty cash", "neutral") : p.isReconciled ? pill("Reconciled", "success") : pill("Waiting for the bank line", "warning")) },
    { key: "x", header: "", render: (p) => (p.canReverse ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setReversing(p), children: "Reverse" }) : null) }
  ];
  const supplierOptions = [{ value: "", label: "All suppliers" }, ...(options?.suppliers ?? []).map((s) => ({ value: String(s.supplierId), label: s.supplierName }))];
  const closeDialog = () => { setPaying(null); if (window.location.search) navigate("/admin/billing/vendor-payments", { replace: true }); };

  return e.jsxs("div", { className: "page", children: [
    e.jsx(PageHeader, {
      title: "Vendor Payments",
      description: "Payments made to suppliers. One payment usually settles several bills, less the TDS Regalis must deduct.",
      actions: canPay ? e.jsxs(Button, { className: "btn--gold", onClick: () => setPaying({}), children: [e.jsx(IconPlus, { size: 16 }), " New payment"] }) : null
    }),
    e.jsxs("div", { className: "bf-panel", children: [
      e.jsxs("div", { className: "bf-toolbar", children: [
        e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Payment number, supplier, UTR" }),
        e.jsx(Select, { label: "Supplier", value: supplierId, options: supplierOptions, onChange: (ev) => setSupplierId(ev.target.value) }),
        e.jsx(Select, { label: "Reconciled", value: reconciled, options: [{ value: "", label: "All" }, { value: "true", label: "Reconciled" }, { value: "false", label: "Not reconciled" }], onChange: (ev) => setReconciled(ev.target.value) })
      ] }),
      e.jsx(Table, { columns, rows: data?.items, loading, rowKey: (p) => p.vendorPaymentId, rowClass: (p) => (p.isReversed ? "bf-row--muted" : ""),
        empty: e.jsx(EmptyState, { title: "No vendor payments yet", description: "Pay this bill on a purchase bill, or + New payment, records one." }) }),
      e.jsx(Pager, { result: data, page, setPage })
    ] }),
    paying ? e.jsx(NewVendorPaymentDialog, { initialSupplierId: paying.supplier, initialBillId: paying.bill, onClose: closeDialog, onDone: () => { closeDialog(); reload(); } }) : null,
    reversing ? e.jsx(ReasonModal, {
      title: `Reverse ${reversing.paymentNumber}`,
      intro: `A posted payment is reversed, never edited. ${money(reversing.amount)} goes back onto the bills it settled, and any TDS it raised is withdrawn. Both entries stay on the record.`,
      confirmText: "Reverse payment",
      danger: true,
      onClose: () => setReversing(null),
      onSubmit: async (reason) => {
        await api.post(`/vendor-payments/${reversing.vendorPaymentId}/reverse`, { reason });
        toast.success(`${reversing.paymentNumber} reversed.`);
        setReversing(null);
        reload();
      }
    }) : null
  ] });
}

// ── Suppliers (existing page, with PAN, state, TDS section and terms) ─────────

const blankSupplier = { supplierCode: "", supplierName: "", contactPerson: "", email: "", mobileNumber: "", gstin: "", addressLine: "", city: "", state: "", pincode: "", paymentTerms: "", locationId: "", notes: "" };

function SupplierDialog({ supplier, profile, locations, tdsSections, onClose, onDone }) {
  const toast = useToast();
  const editing = Boolean(supplier);
  const [f, setF] = React.useState(() => editing
    ? Object.fromEntries(Object.keys(blankSupplier).map((k) => [k, supplier[k] == null ? "" : String(supplier[k])]))
    : { ...blankSupplier, supplierCode: `SUP-${String(Date.now()).slice(-6)}` });
  const initialTerms = profile?.paymentTermsDays;
  const [pan, setPan] = React.useState(profile?.pan ?? "");
  const [stateCode, setStateCode] = React.useState(profile?.stateCode ?? "");
  const [section, setSection] = React.useState(profile?.defaultTdsSection ?? "");
  const [terms, setTerms] = React.useState(initialTerms == null ? "" : TERMS.some((t) => t.value === String(initialTerms)) ? String(initialTerms) : "custom");
  const [customDays, setCustomDays] = React.useState(initialTerms != null && !TERMS.some((t) => t.value === String(initialTerms)) ? String(initialTerms) : "");
  const [individual, setIndividual] = React.useState(Boolean(profile?.isIndividual));
  const [errors, setErrors] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  const set = (k) => (ev) => setF((x) => ({ ...x, [k]: ev.target.value }));
  const panUpper = pan.trim().toUpperCase();

  const submit = async () => {
    const errs = {};
    if (!f.supplierCode.trim()) errs.supplierCode = "Enter a code.";
    if (!f.supplierName.trim()) errs.supplierName = "Enter the supplier's name.";
    if (f.gstin.trim() && !/^[0-9]{2}[A-Z0-9]{13}$/i.test(f.gstin.trim())) errs.gstin = "A GSTIN is 15 characters, starting with the two-digit state code.";
    if (f.pincode.trim() && !/^[1-9][0-9]{5}$/.test(f.pincode.trim())) errs.pincode = "A pincode is six digits.";
    if (panUpper && !PAN_RE.test(panUpper)) errs.pan = PAN_MESSAGE;
    if (section && !panUpper) errs.section = "This supplier has no PAN, so TDS must be deducted at 20%. Add their PAN first.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const body = Object.fromEntries(Object.entries(f).map(([k, v]) => [k, k === "locationId" ? (v ? Number(v) : null) : (v.trim() || null)]));
      body.supplierCode = body.supplierCode.toUpperCase();
      const saved = editing ? await api.put(`/suppliers/${supplier.supplierId}`, body) : await api.post("/suppliers", body);
      const id = saved?.supplierId ?? supplier?.supplierId;
      const gstState = stateCode || (/^[0-9]{2}/.test(f.gstin.trim()) ? f.gstin.trim().slice(0, 2) : "");
      const days = terms === "" ? null : terms === "custom" ? Number(customDays || 0) : Number(terms);
      try {
        await api.put(`/suppliers/${id}/tax-profile`, { pan: panUpper || null, defaultTdsSection: section || null, paymentTermsDays: days, stateCode: gstState || null, isIndividual: individual });
      } catch (error) {
        toast.error(`Supplier saved, but the tax details were not: ${errorText(error, "check them and save again.")}`);
        onDone?.();
        return;
      }
      toast.success(editing ? "Supplier saved." : "Supplier added.");
      onDone?.();
    } catch (error) {
      toast.error(errorText(error, "Could not save the supplier."));
    } finally {
      setBusy(false);
    }
  };

  const stateOptions = [{ value: "", label: "From the GSTIN" }, ...STATES.map(([c, n]) => ({ value: c, label: `${c} · ${n}` }))];
  const tdsOptions = [{ value: "", label: "No TDS" }, ...tdsSections.map((t) => ({ value: t.section, label: `${t.section} · ${t.rate}%` }))];

  return e.jsx(Modal, {
    open: true,
    title: editing ? `Edit ${supplier.supplierName}` : "New supplier",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Save supplier" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal--wide", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "Code", required: true, maxLength: 30, value: f.supplierCode, error: errors.supplierCode, onChange: set("supplierCode") }),
        e.jsx(Input, { label: "Supplier name", required: true, maxLength: 200, value: f.supplierName, error: errors.supplierName, onChange: set("supplierName") }),
        e.jsx(Input, { label: "Contact person", maxLength: 100, value: f.contactPerson, onChange: set("contactPerson") }),
        e.jsx(Input, { label: "Email", type: "email", maxLength: 150, value: f.email, onChange: set("email") }),
        e.jsx(Input, { label: "Mobile", maxLength: 20, value: f.mobileNumber, onChange: set("mobileNumber") }),
        e.jsx(Input, { label: "GSTIN", maxLength: 15, value: f.gstin, error: errors.gstin, onChange: set("gstin") }),
        e.jsx(Input, { label: "Address", maxLength: 300, value: f.addressLine, onChange: set("addressLine") }),
        e.jsx(Input, { label: "City", maxLength: 100, value: f.city, onChange: set("city") }),
        e.jsx(Input, { label: "State", maxLength: 100, value: f.state, onChange: set("state") }),
        e.jsx(Input, { label: "Pincode", maxLength: 6, value: f.pincode, error: errors.pincode, onChange: set("pincode") }),
        e.jsx(Input, { label: "Payment terms (text)", maxLength: 200, value: f.paymentTerms, onChange: set("paymentTerms") }),
        e.jsx(Select, { label: "Location", value: f.locationId, options: [{ value: "", label: "All locations" }, ...locations.map((l) => ({ value: String(l.locationId), label: l.locationName }))], onChange: set("locationId") })
      ] }),
      e.jsx(Input, { label: "Notes", maxLength: 500, value: f.notes, onChange: set("notes") }),
      e.jsx("div", { className: "bf-strong", children: "Tax & terms" }),
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Input, { label: "PAN", maxLength: 10, value: pan, error: errors.pan, hint: "Required before TDS can be deducted. Without it, TDS is 20%.", onChange: (ev) => setPan(ev.target.value.toUpperCase()) }),
        e.jsx(Select, { label: "GST state", value: stateCode, options: stateOptions, hint: "Decides CGST+SGST against IGST on bills.", onChange: (ev) => setStateCode(ev.target.value) }),
        e.jsx(Select, { label: "Default TDS section", value: section, error: errors.section, options: tdsOptions, disabled: !panUpper, hint: panUpper ? "Filled in on every new bill." : "Add the PAN first.", onChange: (ev) => setSection(ev.target.value) }),
        e.jsx(Select, { label: "Payment terms", value: terms, options: [{ value: "", label: "Company default" }, ...TERMS], onChange: (ev) => setTerms(ev.target.value) }),
        terms === "custom" ? e.jsx(Input, { label: "Days", inputMode: "numeric", value: customDays, onChange: (ev) => setCustomDays(ev.target.value.replace(/\D/g, "")) }) : null
      ] }),
      e.jsxs("label", { style: { display: "flex", gap: 8, alignItems: "center" }, children: [
        e.jsx("input", { type: "checkbox", checked: individual, onChange: (ev) => setIndividual(ev.target.checked) }),
        "Individual or HUF (194C at 1% instead of 2%)"
      ] })
    ] })
  });
}

export function AdminSuppliersPageBF() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canCreate = usePermission("SUPPLIER.CREATE");
  const canEdit = usePermission("SUPPLIER.EDIT");
  const canDelete = usePermission("SUPPLIER.DELETE");
  const canLedger = usePermission("BILL.VIEW");
  const [search, setSearch] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [editing, setEditing] = React.useState(null);
  const [deleting, setDeleting] = React.useState(null);
  const debounced = useDebounced(search);
  const { data, loading, reload } = useLoad(() => api.get("/suppliers", { page, pageSize: 20, search: debounced.trim() || undefined }), [page, debounced]);
  const { data: options, reload: reloadOptions } = useLoad(() => api.get("/purchase-bills/form-options").catch(() => ({ suppliers: [], locations: [], tdsSections: [] })), []);
  const { data: locations } = useLoad(() => api.get("/locations", { pageSize: 100 }).then((r) => r.items ?? r).catch(() => []), []);
  React.useEffect(() => setPage(1), [debounced]);
  const profileOf = (id) => options?.suppliers.find((s) => s.supplierId === id) ?? null;

  const columns = [
    { key: "s", header: "Supplier", render: (s) => e.jsxs("div", { children: [
      e.jsxs("div", { className: "bf-strong", children: [s.supplierName, !s.isActive ? e.jsx("span", { style: { marginLeft: 6 }, children: pill("Inactive", "neutral") }) : null] }),
      e.jsx("div", { className: "bf-sub", children: s.supplierCode })
    ] }) },
    { key: "c", header: "Contact", render: (s) => e.jsxs("div", { children: [e.jsx("div", { children: s.contactPerson ?? "—" }), e.jsx("div", { className: "bf-sub", children: [s.mobileNumber, s.email].filter(Boolean).join(" · ") })] }) },
    { key: "g", header: "GSTIN", render: (s) => s.gstin ?? "—" },
    { key: "p", header: "PAN", render: (s) => profileOf(s.supplierId)?.pan ?? e.jsx("span", { className: "bf-sub", children: "—" }) },
    { key: "city", header: "City", render: (s) => s.city ?? "—" },
    { key: "l", header: "Location", render: (s) => s.locationName ?? "All locations" },
    { key: "o", header: "Open POs", num: true, render: (s) => s.openPurchaseOrders },
    { key: "a", header: "", render: (s) => e.jsxs("div", { className: "bf-actions", children: [
      canLedger ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => navigate(`/admin/billing/ledger/supplier/${s.supplierId}`), children: "Ledger" }) : null,
      canLedger ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => navigate(`/admin/billing/purchase-bills?supplier=${s.supplierId}`), children: "Bills" }) : null,
      canEdit ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setEditing(s), children: "Edit" }) : null,
      canDelete ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setDeleting(s), children: "Delete" }) : null
    ] }) }
  ];

  return e.jsxs("div", { className: "page", children: [
    e.jsx(PageHeader, {
      title: "Suppliers",
      description: "Vendors a purchase order or a bill can be raised against. A supplier with no location serves every location.",
      actions: canCreate ? e.jsxs(Button, { onClick: () => setEditing("new"), children: [e.jsx(IconPlus, { size: 16 }), " New supplier"] }) : null
    }),
    e.jsxs("div", { className: "bf-panel", children: [
      e.jsx("div", { className: "bf-toolbar", children: e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Name, code or GSTIN" }) }),
      e.jsx(Table, { columns, rows: data?.items, loading, rowKey: (s) => s.supplierId, empty: e.jsx(EmptyState, { title: "No suppliers yet" }) }),
      e.jsx(Pager, { result: data, page, setPage })
    ] }),
    editing ? e.jsx(SupplierDialog, {
      supplier: editing === "new" ? null : editing,
      profile: editing === "new" ? null : profileOf(editing.supplierId),
      locations: locations ?? [],
      tdsSections: options?.tdsSections ?? [],
      onClose: () => setEditing(null),
      onDone: () => { setEditing(null); reload(); reloadOptions(); }
    }) : null,
    deleting ? e.jsx(Modal, {
      open: true,
      title: `Delete ${deleting.supplierName}`,
      onClose: () => setDeleting(null),
      footer: e.jsxs(e.Fragment, { children: [
        e.jsx(Button, { variant: "outline", onClick: () => setDeleting(null), children: "Keep" }),
        e.jsx(Button, { variant: "danger", onClick: async () => {
          try { await api.del(`/suppliers/${deleting.supplierId}`); toast.success("Supplier deleted."); setDeleting(null); reload(); }
          catch (error) { toast.error(errorText(error, "Could not delete the supplier.")); }
        }, children: "Delete" })
      ] }),
      children: e.jsx("p", { style: { margin: 0 }, children: "A supplier with purchase orders or bills cannot be deleted." })
    }) : null
  ] });
}
