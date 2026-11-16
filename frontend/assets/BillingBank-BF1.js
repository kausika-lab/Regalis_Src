/**
 * Admin -> Billing & Finance -> BANK (Billing & Finance build, Nov 2026; build doc §8, §9).
 *
 *   AdminBankTransactionsPageBF   /admin/billing/transactions     "Bank Statement" (replaces the old page)
 *   AdminReconciliationPageBF     /admin/billing/reconciliation   the same screen, open lines only
 *   ImportStatementWizard         Import statement: file -> columns -> preview -> result
 *   MatchDialog                   one bank line to several documents, every rupee explained
 *
 * Lines come from GET /api/bank-lines; matching is POST /api/bank-lines/{id}/match, which
 * refuses unless applied amounts plus explained differences equal the bank line. Import
 * is POST /api/bank-import/parse (saves nothing) then /commit with the same file.
 * ?import=<bankAccountId> on the Transactions URL opens the import wizard for that account.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file by
 * name (deploy/billing-finance/frontend/apply.mjs). A change ships under a NEW name (BF2).
 */
import {
  React, e, Button, Select, Input, Modal, PageHeader, EmptyState, Spinner, useToast, usePermission,
  api, money, fmtDate, fmtDateTime, StatCard, Tabs, SearchBox, Pager, Table, ReasonModal, DropZone,
  pill, useStyles, useDebounced, amountOf, errorText, istToday, IconPlus, IconDownload, IconUpload
} from "./BillingShared-BF1.js";

const STATUS = { 1: ["Unreconciled", "warning"], 2: ["Matched", "info"], 3: ["Reconciled", "success"] };
const DOC_LABEL = { 1: "Invoice", 2: "Customer payment", 3: "Purchase bill", 4: "Vendor payment", 5: "Expense", 6: "Petty cash top-up" };
const NO_MAP = "";

const accountLabel = (a) => `${a.bankName} · ${a.accountCode}`;
const lineAmount = (l) => e.jsx("span", { className: `bf-strong ${l.direction === 1 ? "bf-green" : "bf-red"}`, children: `${l.direction === 1 ? "+" : "−"}${money(l.amount)}` });
const signed = (v) => `${v < 0 ? "−" : "+"}${money(Math.abs(v))}`;

function useAccounts() {
  const [accounts, setAccounts] = React.useState([]);
  React.useEffect(() => {
    api.get("/bank/accounts").then((r) => setAccounts(Array.isArray(r) ? r : r?.items ?? [])).catch(() => setAccounts([]));
  }, []);
  return accounts;
}

function ConfirmModal({ title, text, confirmText, danger, onClose, onConfirm }) {
  const toast = useToast();
  const [busy, setBusy] = React.useState(false);
  const go = async () => {
    setBusy(true);
    try { await onConfirm(); } catch (error) { toast.error(errorText(error, "That did not work.")); } finally { setBusy(false); }
  };
  return e.jsx(Modal, {
    open: true, title, onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { variant: danger ? "danger" : "primary", onClick: go, loading: busy, disabled: busy, children: confirmText })
    ] }),
    children: e.jsx("p", { className: "bf-modal", style: { margin: 0 }, children: text })
  });
}

// ── Add transaction (the existing dialog, kept) ────────────────────────────────

function AddTransactionModal({ accounts, onClose, onDone }) {
  const toast = useToast();
  const [locations, setLocations] = React.useState([]);
  const [f, setF] = React.useState({ bankAccountId: accounts[0] ? String(accounts[0].bankAccountId) : "", transactionDate: istToday(), direction: "1", amount: "",
    referenceNumber: "", bankReferenceId: "", category: "", locationId: "", narration: "", remarks: "" });
  const [errors, setErrors] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => {
    api.get("/locations", { pageSize: 100 }).then((r) => {
      const list = (Array.isArray(r) ? r : r?.items ?? []).filter((l) => l.isActive !== false);
      setLocations(list);
      setF((x) => ({ ...x, locationId: x.locationId || String(list[0]?.locationId ?? "") }));
    }).catch(() => setLocations([]));
  }, []);
  const set = (k) => (ev) => setF((x) => ({ ...x, [k]: ev.target.value }));
  const submit = async () => {
    const errs = {};
    const value = amountOf(f.amount);
    if (!f.bankAccountId) errs.bankAccountId = "Choose the account.";
    if (!f.transactionDate) errs.transactionDate = "Choose the date.";
    if (value === null || value <= 0) errs.amount = "Enter an amount above zero.";
    if (!f.locationId) errs.locationId = "Choose the location.";
    if (!f.narration.trim()) errs.narration = "Enter the narration as printed on the statement.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      await api.post("/bank/transactions", {
        bankAccountId: Number(f.bankAccountId), transactionDate: f.transactionDate, direction: Number(f.direction), amount: value,
        narration: f.narration.trim(), referenceNumber: f.referenceNumber.trim() || null, bankReferenceId: f.bankReferenceId.trim() || null,
        category: f.category.trim() || null, locationId: Number(f.locationId), remarks: f.remarks.trim() || null
      });
      toast.success("Bank line added.");
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not add the line."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true, title: "Add transaction", onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Add transaction" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal", children: [
      e.jsxs("div", { className: "bf-form-grid", children: [
        e.jsx(Select, { label: "Account", required: true, value: f.bankAccountId, error: errors.bankAccountId, options: [{ value: "", label: "Choose" }, ...accounts.map((a) => ({ value: String(a.bankAccountId), label: accountLabel(a) }))], onChange: set("bankAccountId") }),
        e.jsx(Input, { label: "Date", type: "date", required: true, value: f.transactionDate, error: errors.transactionDate, onChange: set("transactionDate") }),
        e.jsx(Select, { label: "Type", required: true, value: f.direction, options: [{ value: "1", label: "Credit (money in)" }, { value: "2", label: "Debit (money out)" }], onChange: set("direction") }),
        e.jsx(Input, { label: "Amount (₹)", required: true, inputMode: "decimal", value: f.amount, error: errors.amount, onChange: set("amount") }),
        e.jsx(Input, { label: "Reference number", maxLength: 80, value: f.referenceNumber, onChange: set("referenceNumber") }),
        e.jsx(Input, { label: "Bank transaction ID", maxLength: 120, value: f.bankReferenceId, hint: "Unique per account.", onChange: set("bankReferenceId") }),
        e.jsx(Input, { label: "Category", maxLength: 100, value: f.category, onChange: set("category") }),
        e.jsx(Select, { label: "Location", required: true, value: f.locationId, error: errors.locationId, options: [{ value: "", label: "Choose" }, ...locations.map((l) => ({ value: String(l.locationId), label: l.locationName }))], onChange: set("locationId") })
      ] }),
      e.jsx(Input, { label: "Narration", required: true, maxLength: 500, value: f.narration, error: errors.narration, onChange: set("narration") }),
      e.jsx(Input, { label: "Remarks", maxLength: 500, value: f.remarks, onChange: set("remarks") }),
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "Recording a line does not create a payment or an expense — you match it to what it pays for. A month of statement is quicker with Import statement." })
    ] })
  });
}

// ── Import a bank statement (doc §8) ───────────────────────────────────────────

const RESULT_PILL = (r) => (r.result === "new" ? pill("New", "success") : r.result === "duplicate" ? pill("Already here — skipped", "neutral") : pill(r.message ?? "Cannot be read", "danger"));

/** Four steps: the file, the columns, the check, the result. Nothing is saved until Import. */
export function ImportStatementWizard({ accounts: given, initialAccountId, onClose, onDone }) {
  const toast = useToast();
  const loaded = useAccounts();
  const accounts = given?.length ? given : loaded;
  const [step, setStep] = React.useState(1);
  const [accountId, setAccountId] = React.useState(initialAccountId ? String(initialAccountId) : "");
  const [file, setFile] = React.useState(null);
  const [preview, setPreview] = React.useState(null);
  const [mapping, setMapping] = React.useState(null);
  const [parsing, setParsing] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const [result, setResult] = React.useState(null);
  const [error, setError] = React.useState("");
  const account = accounts.find((a) => String(a.bankAccountId) === accountId);

  React.useEffect(() => {
    if (!accountId && accounts.length === 1) setAccountId(String(accounts[0].bankAccountId));
  }, [accounts]); // eslint-disable-line react-hooks/exhaustive-deps

  const parse = React.useCallback(async (m) => {
    if (!file || !accountId) return;
    setParsing(true);
    setError("");
    try {
      const p = await api.upload("/bank-import/parse", { bankAccountId: accountId, mapping: m ? JSON.stringify(m) : undefined }, file);
      setPreview(p);
      if (!m) setMapping(p.appliedMapping);
    } catch (x) {
      setError(errorText(x, "The file could not be read."));
      if (!m) setPreview(null);
    } finally {
      setParsing(false);
    }
  }, [file, accountId]);

  React.useEffect(() => { if (file && accountId) { setMapping(null); parse(null); } }, [file, accountId]); // eslint-disable-line react-hooks/exhaustive-deps

  const changeMap = (key) => (ev) => {
    const next = { ...mapping, [key]: ev.target.value === NO_MAP ? null : ev.target.value };
    setMapping(next);
    if (next.dateColumn && next.descriptionColumn && (next.amountColumn || next.outColumn || next.inColumn)) parse(next);
  };

  const mappingOk = mapping && mapping.dateColumn && mapping.descriptionColumn && (mapping.amountColumn || mapping.outColumn || mapping.inColumn) && mapping.dateFormat;

  const next = () => {
    setError("");
    if (step === 1) {
      if (!accountId) return setError("Choose the bank account.");
      if (!file) return setError("Choose the statement file downloaded from the bank.");
      if (!preview) return setError(error || "The file could not be read.");
      setStep(2);
    } else if (step === 2) {
      if (!mappingOk) return setError("Tell us which column holds the date, the description and the amount.");
      parse(mapping).then(() => setStep(3));
    }
  };

  const commit = async () => {
    setBusy(true);
    setError("");
    try {
      const r = await api.uploadWithMessage("/bank-import/commit", { bankAccountId: accountId, mapping: JSON.stringify(mapping) }, file);
      setResult(r.data);
      setStep(4);
      onDone?.(r.data);
    } catch (x) {
      setError(errorText(x, "The import did not go through."));
    } finally {
      setBusy(false);
    }
  };

  const headerOptions = (optional) => [{ value: NO_MAP, label: optional ? "— not in this file —" : "Choose a column" }, ...(preview?.headers ?? []).map((h) => ({ value: h, label: h }))];
  const saved = preview?.savedMapping;
  const rows = preview?.rows ?? [];

  const body = (() => {
    if (step === 1) {
      return e.jsxs("div", { className: "form-stack", children: [
        e.jsx(Select, { label: "Bank account", required: true, value: accountId, options: [{ value: "", label: "Choose the account" }, ...accounts.map((a) => ({ value: String(a.bankAccountId), label: accountLabel(a) }))],
          onChange: (ev) => setAccountId(ev.target.value) }),
        e.jsx(DropZone, { accept: ".csv,.txt,.xlsx,.xlsm", label: "Drop the CSV or Excel exactly as the bank gives it, or click to browse. No template to fill in.", onFile: (f) => { setFile(f); setPreview(null); } }),
        file ? e.jsxs("div", { className: "bf-note bf-note--info", children: [
          e.jsx("div", { className: "bf-strong", children: file.name }),
          parsing ? "Reading the file…" : preview ? `${preview.rowCount} rows${preview.firstDate ? ` · ${fmtDate(preview.firstDate)} to ${fmtDate(preview.lastDate)}` : ""}` : null
        ] }) : null
      ] });
    }
    if (step === 2) {
      return e.jsxs("div", { className: "form-stack", children: [
        saved ? e.jsx("div", { className: "bf-note bf-note--ok", children: `${account ? accountLabel(account) : "This account"} — saved mapping applied. Change anything that does not fit this file.` })
          : e.jsx("div", { className: "bf-note bf-note--info", children: "Match each item to a column of the file. The choice is remembered for this account, so next month this step is two clicks." }),
        e.jsxs("div", { className: "bf-form-grid", children: [
          e.jsx(Select, { label: "Date", required: true, value: mapping?.dateColumn ?? NO_MAP, options: headerOptions(false), onChange: changeMap("dateColumn") }),
          e.jsx(Select, { label: "Description", required: true, value: mapping?.descriptionColumn ?? NO_MAP, options: headerOptions(false), onChange: changeMap("descriptionColumn") }),
          e.jsx(Select, { label: "Money out", value: mapping?.outColumn ?? NO_MAP, options: headerOptions(true), onChange: changeMap("outColumn") }),
          e.jsx(Select, { label: "Money in", value: mapping?.inColumn ?? NO_MAP, options: headerOptions(true), onChange: changeMap("inColumn") }),
          e.jsx(Select, { label: "Bank reference", value: mapping?.referenceColumn ?? NO_MAP, options: headerOptions(true), onChange: changeMap("referenceColumn") }),
          e.jsx(Select, { label: "Date format", required: true, value: mapping?.dateFormat ?? "dd/MM/yyyy",
            options: (preview?.dateFormats ?? ["dd/MM/yyyy"]).map((d) => ({ value: d, label: d.toUpperCase() })), onChange: changeMap("dateFormat") }),
          e.jsx(Select, { label: "One signed amount column (instead of out/in)", value: mapping?.amountColumn ?? NO_MAP, options: headerOptions(true), onChange: changeMap("amountColumn") }),
          e.jsx(Select, { label: "Balance (for the balance check)", value: mapping?.balanceColumn ?? NO_MAP, options: headerOptions(true), onChange: changeMap("balanceColumn") })
        ] }),
        parsing ? e.jsx(Spinner, {}) : preview ? e.jsx("div", { className: "bf-sub", children: `With these columns: ${preview.newCount} new · ${preview.duplicateCount} already here · ${preview.errorCount} cannot be read.` }) : null
      ] });
    }
    if (step === 3) {
      return e.jsxs("div", { className: "form-stack", children: [
        e.jsx(Table, {
          rows, rowKey: (r) => r.rowNumber, loading: parsing,
          rowClass: (r) => (r.result === "duplicate" ? "bf-row--muted" : r.result === "error" ? "bf-row--tint" : ""),
          columns: [
            { key: "row", header: "Row", render: (r) => r.rowNumber },
            { key: "date", header: "Date", render: (r) => (r.date ? fmtDate(r.date) : "—") },
            { key: "desc", header: "Description", render: (r) => e.jsx("span", { className: "bf-sub", style: { color: "var(--text-primary)" }, children: r.description }) },
            { key: "out", header: "Out", num: true, render: (r) => (r.out ? money(r.out) : "") },
            { key: "in", header: "In", num: true, render: (r) => (r.in ? money(r.in) : "") },
            { key: "ref", header: "Reference", render: (r) => r.reference ?? "" },
            { key: "res", header: "Result", render: RESULT_PILL }
          ],
          empty: e.jsx(EmptyState, { title: "No rows were found in this file" })
        }),
        e.jsx("div", { className: "bf-strong", children: `${preview?.newCount ?? 0} new lines · ${preview?.duplicateCount ?? 0} already here · ${preview?.errorCount ?? 0} cannot be read` }),
        e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "Nothing is saved until Import is pressed. Rows that cannot be read are left out; fix the file and import it again — the lines already here are then skipped. Importing the same file twice adds nothing." })
      ] });
    }
    return e.jsxs("div", { className: "form-stack", children: [
      e.jsx("div", { className: "bf-note bf-note--ok", children: result?.message }),
      e.jsx("div", { className: `bf-note ${result?.balancesAgree ? "bf-note--ok" : "bf-note--warn"}`, children: result?.balanceMessage }),
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "The batch can be undone from Imports while none of its lines is matched." })
    ] });
  })();

  const STEPS = ["The file", "The columns", "Check", "Done"];
  return e.jsx(Modal, {
    open: true,
    title: `Import a bank statement — step ${step} of 4: ${STEPS[step - 1]}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: step === 4
      ? [e.jsx(Button, { onClick: onClose, children: "Close" }, "c")]
      : [
        e.jsx(Button, { variant: "outline", onClick: step === 1 ? onClose : () => { setError(""); setStep(step - 1); }, disabled: busy, children: step === 1 ? "Cancel" : "Back" }, "b"),
        step < 3
          ? e.jsx(Button, { onClick: next, disabled: parsing, loading: parsing && step === 2, children: "Next" }, "n")
          : e.jsx(Button, { onClick: commit, loading: busy, disabled: busy || !preview || preview.newCount === 0, children: `Import ${preview?.newCount ?? 0} lines` }, "i")
      ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal bf-modal--wide", children: [
      body,
      error ? e.jsx("div", { className: "bf-note bf-note--danger", children: error }) : null,
      step === 3 && preview && preview.newCount === 0 && preview.duplicateCount > 0 && preview.errorCount === 0
        ? e.jsx("div", { className: "bf-note bf-note--warn", children: "Every line in this file is already here. Nothing was imported." }) : null
    ] })
  });
}

// ── Matching (doc §9) ──────────────────────────────────────────────────────────

/** bank = applied + sum(signed). Money in: TDS, bank charge, short payment reduce; interest, advance add. */
const signFor = (reason, credit, amount) => {
  if (reason === 4) return amount;
  const a = Math.abs(amount);
  if (reason === 1 || reason === 5) return -a;
  if (reason === 3 || reason === 6) return a;
  if (reason === 2) return credit ? -a : a;
  return a;
};
const round2 = (v) => Math.round(v * 100) / 100;

export function MatchDialog({ lineId, onClose, onDone }) {
  const toast = useToast();
  const [ctx, setCtx] = React.useState(null);
  const [q, setQ] = React.useState("");
  const dq = useDebounced(q);
  const [ticked, setTicked] = React.useState({}); // key -> { row, apply }
  const [diffs, setDiffs] = React.useState([]);
  const [busy, setBusy] = React.useState(false);
  const first = React.useRef(true);

  React.useEffect(() => {
    api.get(`/bank-lines/${lineId}/suggestions`, { q: dq.trim() || undefined }).then((c) => {
      setCtx(c);
      if (first.current) {
        first.current = false;
        const pre = {};
        for (const r of c.candidates.filter((x) => x.suggested)) pre[`${r.docType}-${r.docId}`] = { row: r, apply: String(r.outstanding) };
        setTicked(pre);
      }
    }).catch((x) => { toast.error(errorText(x, "Could not load the candidates.")); if (first.current) onClose(); });
  }, [lineId, dq]); // eslint-disable-line react-hooks/exhaustive-deps

  if (!ctx) return e.jsx(Modal, { open: true, title: "Match a bank line", onClose, children: e.jsx(Spinner, { center: true }) });
  const line = ctx.line;
  const credit = line.direction === 1;
  const reasons = ctx.reasons;
  const reasonName = (id) => reasons.find((r) => r.reason === id)?.label ?? "";

  const tickedList = Object.values(ticked);
  const applied = round2(tickedList.reduce((s, t) => s + (amountOf(t.apply) ?? 0), 0));
  const diffValues = diffs.map((d) => {
    const raw = d.reason === 4 ? Number(String(d.amount).replace(/[,\s₹]/g, "")) : amountOf(d.amount);
    return Number.isFinite(raw) && raw !== null ? raw : 0;
  });
  const explained = round2(diffs.reduce((s, d, i) => s + (d.reason ? signFor(d.reason, credit, diffValues[i]) : 0), 0));
  const leftover = round2(line.amount - applied);
  const gap = round2(line.amount - (applied + explained));
  const balanced = Math.abs(gap) < 0.005 && (tickedList.length > 0 || diffs.length > 0);
  const targets = tickedList.filter((t) => t.row.docType === 1 || t.row.docType === 3);
  const parties = [...new Map(ctx.candidates.filter((c) => c.partyId).map((c) => [c.partyId, c.partyName])).entries()];
  const partyKnown = tickedList.some((t) => t.row.partyId);

  const toggle = (row) => setTicked((cur) => {
    const key = `${row.docType}-${row.docId}`;
    const next = { ...cur };
    if (next[key]) delete next[key]; else next[key] = { row, apply: String(row.outstanding) };
    return next;
  });
  const setApply = (key, value) => setTicked((cur) => ({ ...cur, [key]: { ...cur[key], apply: value } }));
  const setDiff = (i, patch) => setDiffs((cur) => cur.map((d, j) => (j === i ? { ...d, ...patch } : d)));
  const addDiff = () => setDiffs((cur) => [...cur, { reason: "", amount: leftover !== 0 && cur.length === 0 ? String(Math.abs(leftover)) : "", tdsSection: "", target: "", partyId: "", note: "" }]);

  // What recording it this way will do, in words.
  const effects = [];
  for (const t of tickedList) {
    const r = t.row;
    const apply = amountOf(t.apply) ?? 0;
    if (r.docType === 1) effects.push(`settles ${money(apply)} of invoice ${r.number}${apply >= r.outstanding ? ", which becomes fully paid" : ""}`);
    else if (r.docType === 3) effects.push(`writes a vendor payment settling ${money(apply)} of bill ${r.number}`);
    else effects.push(`matches ${DOC_LABEL[r.docType].toLowerCase()} ${r.number}`);
  }
  diffs.forEach((d, i) => {
    const v = Math.abs(diffValues[i]);
    if (!d.reason || !v) return;
    const target = targets.find((t) => `${t.row.docType}-${t.row.docId}` === d.target) ?? (targets.length === 1 ? targets[0] : null);
    const who = target?.row.partyName ?? "the party";
    if (d.reason === 1) effects.push(credit ? `raises a TDS receivable of ${money(v)} against ${who}, which then waits for their Form 16A` : `raises a TDS payable of ${money(v)} for ${who}`);
    if (d.reason === 2) effects.push(`records a bank charge of ${money(v)} as an expense`);
    if (d.reason === 3) effects.push(`records ${money(v)} of interest as other income on this line`);
    if (d.reason === 4) effects.push(`writes off ${money(v)} as rounding`);
    if (d.reason === 5) effects.push(`closes ${target ? target.row.number : "the invoice"} with a short payment of ${money(v)} accepted`);
    if (d.reason === 6) effects.push(credit ? `adds ${money(v)} to ${parties.find(([id]) => String(id) === d.partyId)?.[1] ?? tickedList[0]?.row.partyName ?? "the customer"}'s wallet` : `records an advance of ${money(v)} to the supplier`);
  });

  const submit = async () => {
    for (const t of tickedList) {
      const v = amountOf(t.apply);
      if (v === null || v <= 0) return toast.error(`Enter the amount applied to ${t.row.number}.`);
      if (v > t.row.outstanding) return toast.error(`${money(t.row.outstanding)} is the most that can be applied to ${t.row.number}.`);
    }
    for (const [i, d] of diffs.entries()) {
      if (!d.reason) return toast.error("Choose what the difference is.");
      const info = reasons.find((r) => r.reason === d.reason);
      if (info && !info.allowed) return toast.error(d.reason === 5 ? "Accepting a short payment needs the write-off permission. Ask the office manager." : `${info.label} needs ${info.requiredPermission}.`);
      if (!diffValues[i]) return toast.error("Enter the amount of each difference.");
      if (d.reason === 4 && Math.abs(diffValues[i]) > ctx.roundingCap)
        return toast.error(`${money(Math.abs(diffValues[i]))} is more than the ${money(ctx.roundingCap)} rounding cap. Choose a different reason.`);
      if (d.reason === 1 && !d.tdsSection) return toast.error("Choose the TDS section.");
    }
    if (!balanced) return toast.error(`${money(Math.abs(gap))} is still unexplained. Say what it is before reconciling.`);
    setBusy(true);
    try {
      await api.post(`/bank-lines/${lineId}/match`, {
        allocations: tickedList.map((t) => ({ docType: t.row.docType, docId: t.row.docId, amount: amountOf(t.apply) })),
        differences: diffs.map((d, i) => {
          const target = targets.find((t) => `${t.row.docType}-${t.row.docId}` === d.target) ?? ((d.reason === 1 || d.reason === 5) && targets.length === 1 ? targets[0] : null);
          return {
            reason: d.reason,
            amount: d.reason === 4 ? diffValues[i] : Math.abs(diffValues[i]),
            tdsSection: d.reason === 1 ? d.tdsSection : null,
            targetDocType: target ? target.row.docType : null,
            targetDocId: target ? target.row.docId : null,
            partyId: d.reason === 6 && d.partyId ? Number(d.partyId) : null,
            note: d.note?.trim() || null
          };
        })
      });
      toast.success("Matched and reconciled.");
      onDone?.();
    } catch (x) {
      toast.error(errorText(x, "Could not match this line."));
    } finally {
      setBusy(false);
    }
  };

  const candidateRows = [...tickedList.map((t) => t.row).filter((r) => !ctx.candidates.some((c) => c.docType === r.docType && c.docId === r.docId)), ...ctx.candidates];

  return e.jsx(Modal, {
    open: true,
    title: "Match a bank line",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx("span", { className: balanced ? "bf-green bf-strong" : "bf-red bf-strong", style: { marginRight: "auto", fontSize: "var(--font-size-sm)" },
        children: `Applied ${money(applied)} + difference ${explained < 0 ? "−" : ""}${money(Math.abs(explained))} = ${money(round2(applied + explained))}. `
          + (balanced ? "It balances." : `${money(Math.abs(gap))} is still unexplained.`) }),
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { className: "btn--gold", onClick: submit, loading: busy, disabled: busy || !balanced, children: "Match and reconcile" })
    ] }),
    children: e.jsxs("div", { className: "form-stack bf-modal bf-modal--wide", children: [
      e.jsxs("div", { className: "bf-band", style: { gridTemplateColumns: "auto 1fr auto" }, children: [
        e.jsxs("div", { children: [e.jsx("span", { children: "Date" }), e.jsx("b", { children: fmtDate(line.transactionDate) })] }),
        e.jsxs("div", { children: [e.jsx("span", { children: line.accountName }), e.jsx("div", { className: "bf-strong", style: { overflowWrap: "anywhere" }, children: line.narration }),
          e.jsx("div", { className: "bf-sub", children: [line.bankReferenceId, line.referenceNumber].filter(Boolean).join(" · ") || "No reference" })] }),
        e.jsxs("div", { children: [e.jsx("span", { children: credit ? "Money in" : "Money out" }), e.jsx("b", { className: credit ? "bf-green" : "bf-red", children: money(line.amount) })] })
      ] }),
      e.jsxs("div", { children: [
        e.jsx("div", { className: "bf-strong", style: { marginBottom: 6 }, children: "What this money is for — tick as many as apply" }),
        e.jsx(SearchBox, { value: q, onChange: setQ, placeholder: credit ? "Search invoices or payments by number or customer" : "Search bills, payments, expenses by number or supplier" }),
        e.jsx("div", { style: { height: 8 } }),
        e.jsx(Table, {
          rows: candidateRows, rowKey: (r) => `${r.docType}-${r.docId}`,
          columns: [
            { key: "tick", header: "", render: (r) => e.jsx("input", { type: "checkbox", checked: !!ticked[`${r.docType}-${r.docId}`], "aria-label": `Tick ${r.number}`, onChange: () => toggle(r) }) },
            { key: "type", header: "Document", render: (r) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: r.number }),
              e.jsx("div", { className: "bf-sub", children: [r.docTypeLabel, r.hint].filter(Boolean).join(" · ") })] }) },
            { key: "party", header: "Party", render: (r) => r.partyName ?? "—" },
            { key: "date", header: "Date", render: (r) => fmtDate(r.date) },
            { key: "out", header: "Outstanding", num: true, render: (r) => money(r.outstanding) },
            { key: "apply", header: "Apply", num: true, render: (r) => {
              const key = `${r.docType}-${r.docId}`;
              const t = ticked[key];
              if (!t) return e.jsx("span", { className: "bf-sub", children: "not selected" });
              return r.mustBeFull ? money(r.outstanding)
                : e.jsx("input", { className: "field__input", style: { width: 120, height: 34, textAlign: "right" }, inputMode: "decimal", value: t.apply, "aria-label": `Apply to ${r.number}`, onChange: (ev) => setApply(key, ev.target.value) });
            } }
          ],
          empty: e.jsx(EmptyState, { title: "No matching documents", description: "Search by number, or check that the document has been recorded and approved." })
        }),
        e.jsx("p", { className: "bf-sub", style: { margin: "6px 0 0" }, children: "Ranked by party, then closeness of amount, then date. Suggest, never decide: a person presses the button." })
      ] }),
      (leftover !== 0 || diffs.length > 0) ? e.jsxs("div", { className: "bf-panel", children: [
        e.jsxs("div", { style: { display: "flex", justifyContent: "space-between", gap: 12, flexWrap: "wrap" }, children: [
          e.jsx("div", { className: "bf-strong", children: leftover > 0 ? `There is ${money(leftover)} left over` : leftover < 0 ? `The bank line is ${money(-leftover)} short of what is applied` : "Differences" }),
          e.jsx("div", { className: "bf-sub", children: `Bank ${money(line.amount)} · applied ${money(applied)} · rounding cap ${money(ctx.roundingCap)}` })
        ] }),
        diffs.map((d, i) => e.jsxs("div", { className: "bf-form-grid", style: { marginTop: 10, alignItems: "end" }, children: [
          e.jsx(Select, { label: "What is the difference?", value: d.reason ? String(d.reason) : "",
            options: [{ value: "", label: "Choose a reason" }, ...reasons.map((r) => ({ value: String(r.reason), label: r.allowed ? r.label : `${r.label} (needs ${r.requiredPermission})`, disabled: !r.allowed }))],
            onChange: (ev) => setDiff(i, { reason: ev.target.value ? Number(ev.target.value) : "" }) }),
          e.jsx(Input, { label: d.reason === 4 ? "Amount (₹, − if the bank is lower)" : "Amount (₹)", inputMode: "decimal", value: d.amount, onChange: (ev) => setDiff(i, { amount: ev.target.value }) }),
          d.reason === 1 ? e.jsx(Select, { label: "TDS section", value: d.tdsSection, options: [{ value: "", label: "Choose" }, ...ctx.tdsSections.map((s) => ({ value: s.section, label: `${s.section} · ${s.rate}%` }))], onChange: (ev) => setDiff(i, { tdsSection: ev.target.value }) }) : null,
          (d.reason === 1 || d.reason === 5) ? e.jsx(Select, { label: "Against", value: d.target, options: [{ value: "", label: targets.length === 1 ? targets[0].row.number : "Choose the document" }, ...targets.map((t) => ({ value: `${t.row.docType}-${t.row.docId}`, label: t.row.number }))], onChange: (ev) => setDiff(i, { target: ev.target.value }) }) : null,
          d.reason === 6 && !partyKnown ? e.jsx(Select, { label: credit ? "Customer" : "Supplier", value: d.partyId, options: [{ value: "", label: "Choose" }, ...parties.map(([id, name]) => ({ value: String(id), label: name }))], onChange: (ev) => setDiff(i, { partyId: ev.target.value }) }) : null,
          d.reason === 2 || d.reason === 3 ? e.jsx(Input, { label: "Note", maxLength: 300, value: d.note, placeholder: d.reason === 2 ? "NEFT fee, cheque return fee…" : "", onChange: (ev) => setDiff(i, { note: ev.target.value }) }) : null,
          e.jsx(Button, { variant: "outline", size: "sm", onClick: () => setDiffs((cur) => cur.filter((_, j) => j !== i)), children: "Remove" })
        ] }, i)),
        e.jsx("div", { style: { marginTop: 10 }, children: e.jsx(Button, { variant: "outline", size: "sm", onClick: addDiff, children: diffs.length ? "+ Add another reason" : "+ Explain the difference" }) })
      ] }) : null,
      effects.length ? e.jsx("div", { className: "bf-note bf-note--info", children: `Recording it this way ${effects.join("; ")}.` }) : null,
      e.jsx("p", { className: "bf-sub", style: { margin: 0 }, children: "Applied amounts plus explained differences must equal the bank line, to the rupee. Once reconciled, the line, its allocations and anything created by a difference are locked until someone unmatches it, with a reason." })
    ] })
  });
}

// ── Imports list ───────────────────────────────────────────────────────────────

function ImportsTab({ accountId, canImport, onShowBatch, refreshKey }) {
  const toast = useToast();
  const [rows, setRows] = React.useState(null);
  const [undo, setUndo] = React.useState(null);
  const load = React.useCallback(() => {
    api.get("/bank-import/batches", { bankAccountId: accountId || undefined }).then(setRows).catch((x) => toast.error(errorText(x, "Could not load the imports.")));
  }, [accountId]); // eslint-disable-line react-hooks/exhaustive-deps
  React.useEffect(() => { load(); }, [load, refreshKey]);
  return e.jsxs("div", { children: [
    e.jsx(Table, {
      rows, loading: rows === null, rowKey: (r) => r.bankImportBatchId, rowClass: (r) => (r.isUndone ? "bf-row--muted" : ""),
      columns: [
        { key: "when", header: "Imported", render: (r) => e.jsxs("div", { children: [e.jsx("div", { children: fmtDateTime(r.importedUtc) }), e.jsx("div", { className: "bf-sub", children: r.importedBy ?? "" })] }) },
        { key: "file", header: "File", render: (r) => e.jsxs("div", { children: [e.jsx("div", { className: "bf-strong", children: r.fileName }), e.jsx("div", { className: "bf-sub", children: r.accountName })] }) },
        { key: "range", header: "Covers", render: (r) => (r.firstDate ? `${fmtDate(r.firstDate)} – ${fmtDate(r.lastDate)}` : "—") },
        { key: "counts", header: "Lines", render: (r) => `${r.importedCount} new · ${r.skippedCount} already here · ${r.errorCount} unreadable` },
        { key: "status", header: "Status", render: (r) => (r.isUndone ? pill("Undone", "neutral") : r.matchedLines ? pill(`${r.matchedLines} matched`, "info") : pill("Imported", "success")) },
        { key: "act", header: "", render: (r) => e.jsxs("div", { className: "bf-actions", children: [
          !r.isUndone && r.importedCount > 0 ? e.jsx(Button, { variant: "outline", size: "sm", onClick: () => onShowBatch(r.batchCode), children: "Show lines" }) : null,
          canImport && !r.isUndone ? e.jsx(Button, { variant: "outline", size: "sm", onClick: () => setUndo(r), children: "Undo" }) : null
        ] }) }
      ],
      empty: e.jsx(EmptyState, { title: "No statements imported yet" })
    }),
    undo ? e.jsx(ConfirmModal, {
      title: `Undo import of ${undo.fileName}`,
      text: `Removes the ${undo.importedCount} lines this import added. It can only be done while none of them is matched.`,
      confirmText: "Undo import", danger: true,
      onClose: () => setUndo(null),
      onConfirm: async () => { await api.post(`/bank-import/${undo.bankImportBatchId}/undo`); toast.success("Import undone."); setUndo(null); load(); }
    }) : null
  ] });
}

// ── The page (Transactions and Reconciliation) ─────────────────────────────────

function BankLinesScreen({ mode }) {
  useStyles();
  const toast = useToast();
  const reconcile = mode === "reconcile";
  const canCreate = usePermission("BANK.CREATE");
  const canExport = usePermission("BANK.EXPORT");
  const canImport = usePermission("BANK.IMPORT");
  const canMatch = usePermission("BANK.MATCH");
  const accounts = useAccounts();
  const params = React.useMemo(() => new URLSearchParams(window.location.search), []);
  const [tab, setTab] = React.useState("lines");
  const [accountId, setAccountId] = React.useState(params.get("account") ?? "");
  const [direction, setDirection] = React.useState("");
  const [status, setStatus] = React.useState(reconcile ? "open" : "");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [search, setSearch] = React.useState("");
  const [batch, setBatch] = React.useState(params.get("batch") ?? "");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [adding, setAdding] = React.useState(false);
  const [importing, setImporting] = React.useState(params.get("import") ? { accountId: params.get("import") } : null);
  const [matching, setMatching] = React.useState(null);
  const [unmatching, setUnmatching] = React.useState(null);
  const [importsKey, setImportsKey] = React.useState(0);
  const debounced = useDebounced(search);

  const filters = { bankAccountId: accountId || undefined, direction: direction || undefined, status: status || undefined, from: from || undefined, to: to || undefined,
    search: debounced.trim() || undefined, batchCode: batch || undefined };
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setData(await api.get("/bank-lines", { ...filters, page, pageSize: 20 }));
    } catch (x) {
      toast.error(errorText(x, "Could not load the bank lines."));
    } finally {
      setLoading(false);
    }
  }, [accountId, direction, status, from, to, debounced, batch, page]); // eslint-disable-line react-hooks/exhaustive-deps
  React.useEffect(() => { load(); }, [load]);
  React.useEffect(() => { setPage(1); }, [accountId, direction, status, from, to, debounced, batch]);

  const exportCsv = () => api.download("/bank/export", {
    bankAccountId: accountId || undefined, direction: direction || undefined,
    reconciliationStatus: /^[123]$/.test(status) ? status : undefined, from: from || undefined, to: to || undefined
  }, "bank-statement.csv").catch((x) => toast.error(errorText(x, "Export failed.")));

  const rows = data?.page?.items ?? null;
  const filtered = !!(accountId || direction || from || to || debounced || batch || (status && status !== (reconcile ? "open" : "")));

  const columns = [
    { key: "date", header: "Date", render: (l) => e.jsxs("div", { children: [e.jsx("div", { children: fmtDate(l.transactionDate) }), e.jsx("div", { className: "bf-sub", children: l.accountName })] }) },
    { key: "narr", header: "Narration", render: (l) => e.jsxs("div", { children: [
      e.jsx("div", { style: { overflowWrap: "anywhere" }, children: l.narration }),
      e.jsx("div", { className: "bf-sub", children: [l.referenceNumber, l.bankReferenceId && l.bankReferenceId !== l.referenceNumber ? `Bank id ${l.bankReferenceId}` : null, l.category,
        l.importBatchId ? "Imported" : null].filter(Boolean).join(" · ") })
    ] }) },
    { key: "amt", header: "Amount", num: true, render: lineAmount },
    { key: "matched", header: "Matched to", render: (l) => {
      if (!l.documents.length && !l.differences.length) return l.legacyMatch ? e.jsx("span", { className: "bf-strong", children: l.legacyMatch }) : e.jsx("span", { className: "bf-sub", children: "—" });
      return e.jsxs("div", { children: [
        ...l.documents.map((d) => e.jsx("div", { children: [e.jsx("span", { className: "bf-strong", children: d.docNumber }, "n"), ` ${money(d.amount)}`] }, `d${d.docType}-${d.docId}`)),
        ...l.differences.map((d, i) => e.jsx("div", { className: "bf-sub", children: `${d.reasonLabel} ${signed(d.signedAmount)}${d.tdsSection ? ` · ${d.tdsSection}` : ""}` }, `x${i}`)),
        l.reconciledUtc ? e.jsx("div", { className: "bf-sub", children: `${l.reconciledBy ?? ""} · ${fmtDateTime(l.reconciledUtc)}` }) : null
      ] });
    } },
    { key: "status", header: "Status", render: (l) => pill(...(STATUS[l.status] ?? ["—", "neutral"])) },
    { key: "act", header: "", render: (l) => e.jsxs("div", { className: "bf-actions", children: [
      canMatch && l.status !== 3 ? e.jsx(Button, { size: "sm", onClick: () => setMatching(l), children: "Match" }) : null,
      canMatch && (l.status === 3 || l.status === 2) ? e.jsx(Button, { variant: "outline", size: "sm", onClick: () => setUnmatching(l), children: "Unmatch" }) : null
    ] }) }
  ];

  const accountOptions = [{ value: "", label: "All accounts" }, ...accounts.map((a) => ({ value: String(a.bankAccountId), label: accountLabel(a) }))];
  const emptyState = reconcile && data?.unreconciledCount === 0 && !filtered
    ? e.jsx(EmptyState, { title: "Nothing left to reconcile", description: "Every bank line has been matched and reconciled." })
    : e.jsx(EmptyState, { title: "No bank lines match these filters", description: canImport ? "Import a statement to bring a month of lines in at once." : undefined });

  return e.jsxs("div", { children: [
    e.jsx(PageHeader, {
      title: reconcile ? "Reconciliation" : "Bank Statement",
      description: reconcile
        ? "Match each bank line to what it pays for. Applied amounts plus explained differences must equal the bank line to the rupee."
        : "Actual bank transactions. Credits are money in, debits money out. Import the statement, then match each line.",
      actions: e.jsxs("div", { className: "bf-head-actions", children: [
        canImport ? e.jsxs(Button, { onClick: () => setImporting({ accountId }), children: [e.jsx(IconUpload, { size: 16 }), " Import statement"] }) : null,
        canExport ? e.jsxs(Button, { variant: "outline", onClick: exportCsv, children: [e.jsx(IconDownload, { size: 16 }), " Export CSV"] }) : null,
        canCreate && !reconcile ? e.jsxs(Button, { variant: "outline", onClick: () => setAdding(true), children: [e.jsx(IconPlus, { size: 16 }), " Add transaction"] }) : null
      ] })
    }),
    e.jsxs("div", { className: "bf-cards", children: [
      e.jsx(StatCard, { label: "Not yet reconciled", value: data ? String(data.unreconciledCount) : "—", sub: "lines in this filter", onClick: () => setStatus("open"), pressed: status === "open" }),
      accountId ? e.jsx(StatCard, { label: "Balance in Regalis", value: data ? money(data.accountBalance) : "—", sub: "opening + credits − debits" }) : null,
      e.jsx(StatCard, { label: "Lines shown", value: data ? String(data.page.totalCount) : "—", sub: batch ? "from one import" : "in this filter" })
    ] }),
    e.jsx(Tabs, { value: tab, onChange: setTab, tabs: [{ value: "lines", label: "Lines" }, { value: "imports", label: "Imports" }] }),
    tab === "imports"
      ? e.jsx(ImportsTab, { accountId, canImport, refreshKey: importsKey, onShowBatch: (code) => { setBatch(code); setStatus(""); setTab("lines"); } })
      : e.jsxs("div", { className: "bf-panel", children: [
        e.jsxs("div", { className: "bf-toolbar", children: [
          e.jsx("div", { className: "bf-search", children: e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: "Narration, reference, document number" }) }),
          e.jsx(Select, { label: "Account", value: accountId, options: accountOptions, onChange: (ev) => setAccountId(ev.target.value) }),
          e.jsx(Select, { label: "Type", value: direction, options: [{ value: "", label: "Credit and debit" }, { value: "1", label: "Credit (in)" }, { value: "2", label: "Debit (out)" }], onChange: (ev) => setDirection(ev.target.value) }),
          e.jsx(Select, { label: "Status", value: status, options: [{ value: "", label: "Any status" }, { value: "open", label: "Not reconciled" }, { value: "1", label: "Unreconciled" }, { value: "2", label: "Matched" }, { value: "3", label: "Reconciled" }], onChange: (ev) => setStatus(ev.target.value) }),
          e.jsx(Input, { label: "From", type: "date", value: from, onChange: (ev) => setFrom(ev.target.value) }),
          e.jsx(Input, { label: "To", type: "date", value: to, onChange: (ev) => setTo(ev.target.value) })
        ] }),
        batch ? e.jsxs("div", { className: "bf-note bf-note--info", style: { marginBottom: 12, display: "flex", justifyContent: "space-between", gap: 12 }, children: [
          e.jsx("span", { children: `Showing the lines of one import (${batch}).` }),
          e.jsx("button", { type: "button", className: "bf-link", onClick: () => setBatch(""), children: "Show all" })
        ] }) : null,
        e.jsx(Table, { columns, rows, loading, rowKey: (l) => l.bankTransactionId, empty: emptyState }),
        e.jsx(Pager, { result: data?.page, page, setPage })
      ] }),
    adding ? e.jsx(AddTransactionModal, { accounts, onClose: () => setAdding(false), onDone: () => { setAdding(false); load(); } }) : null,
    importing ? e.jsx(ImportStatementWizard, { accounts, initialAccountId: importing.accountId, onClose: () => setImporting(null), onDone: () => { load(); setImportsKey((k) => k + 1); } }) : null,
    matching ? e.jsx(MatchDialog, { lineId: matching.bankTransactionId, onClose: () => setMatching(null), onDone: () => { setMatching(null); load(); } }) : null,
    unmatching ? e.jsx(ReasonModal, {
      title: `Unmatch the line of ${fmtDate(unmatching.transactionDate)}`,
      intro: "Unreconciles the line and reverses whatever the match created (receipts, a vendor payment, a bank-charge expense, TDS). It is recorded with who did it and why.",
      confirmText: "Unmatch", danger: true,
      onClose: () => setUnmatching(null),
      onSubmit: async (reason) => { await api.post(`/bank-lines/${unmatching.bankTransactionId}/unmatch`, { reason }); toast.success("Line unmatched."); setUnmatching(null); load(); }
    }) : null
  ] });
}

export function AdminBankTransactionsPageBF() {
  return e.jsx(BankLinesScreen, { mode: "all" });
}

export function AdminReconciliationPageBF() {
  return e.jsx(BankLinesScreen, { mode: "reconcile" });
}
