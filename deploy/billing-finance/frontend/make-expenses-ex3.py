"""Builds assets/AdminExpensesPage-EX3.js from the deployed frontend/assets/AdminExpensesPage-EX2.js
(build doc §12: department, receipts, approval status, approval/reject, reimbursement, recurring).

    python make-expenses-ex3.py <frontend dir>

EX3 is a new file name because /assets is cached for a year. Each edit must match exactly
once, or nothing is written. The result is committed in assets/ as well; rerun this only
if EX2 changes."""
import os, sys

front = sys.argv[1] if len(sys.argv) > 1 else os.path.join(os.path.dirname(__file__), "..", "..", "..", "frontend")
src = os.path.join(front, "assets", "AdminExpensesPage-EX2.js")
dst = os.path.join(os.path.dirname(os.path.abspath(__file__)), "assets", "AdminExpensesPage-EX3.js")
t = open(src, encoding="utf-8").read().replace("\r\n", "\n")

def swap(old, new):
    global t
    n = t.count(old)
    if n != 1:
        raise SystemExit(f"expected 1 match, found {n}: {old[:80]!r}")
    t = t.replace(old, new)

swap(""" * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file
 * by name. It replaces AdminExpensesPage-CstB2vPx.js / -EX1.js (EX2: round axis gridlines); a fix ships under a NEW name
 * (EX3 ...) because /assets is cached for a year and this file is not precached.""",
""" * Billing & Finance build (EX3, Nov 2026, build doc §12): every expense now has a department,
 * receipts (up to five, required above a set amount), an approval status (by limits in
 * Settings; the approver is never the person who raised it), who paid (bank, petty cash, or
 * a member of staff to be reimbursed) and recurring drafts that someone confirms. That side
 * lives in /api/expense-controls; the voucher API is unchanged.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file
 * by name. It replaces AdminExpensesPage-EX2.js (made by deploy/billing-finance/frontend/
 * make-expenses-ex3.py); a fix ships under a NEW name (EX4 ...) because /assets is cached for a year.""")

swap("""import { b as fmtMoney, c as fmtDate } from "./formatters-DFe5xCRi.js";""",
"""import { b as fmtMoney, c as fmtDate } from "./formatters-DFe5xCRi.js";
import { api as bf, ReasonModal, DropZone, pill as bfPill, useStyles as useBfStyles } from "./BillingShared-BF1.js";""")

# ── page state ──────────────────────────────────────────────────────────────
swap("""  const canExport = usePermission("EXPENSE.EXPORT");

  const [tab, setTab] = React.useState("expenses");""",
"""  const canExport = usePermission("EXPENSE.EXPORT");
  const canReimburse = usePermission("EXPENSE.REIMBURSE");
  useBfStyles();

  const [tab, setTab] = React.useState("expenses");
  // Billing & Finance: approval, department, receipts (expense-controls).
  const [status, setStatus] = React.useState("");
  const [department, setDepartment] = React.useState("");
  const [departments, setDepartments] = React.useState([]);
  const [controls, setControls] = React.useState({});
  const [receiptsFor, setReceiptsFor] = React.useState(null);
  const [rejecting, setRejecting] = React.useState(null);
  const [reimbursing, setReimbursing] = React.useState(null);
  const [confirming, setConfirming] = React.useState(null);""")

swap("""      expensesApi.list({ ...filters, page, pageSize: 20 }),""",
"""      status || department
        ? bf.get("/expense-controls/register", { ...filters, status: status || undefined, departmentId: department || undefined, page, pageSize: 20 })
        : expensesApi.list({ ...filters, page, pageSize: 20 }),""")
swap("""  }, [filterKey, page, version]);""", """  }, [filterKey, page, version, status, department]);

  React.useEffect(() => {
    const ids = (result?.items ?? []).map((x) => x.expenseId);
    if (ids.length === 0) {
      setControls({});
      return;
    }
    bf.get("/expense-controls", { ids: ids.join(",") })
      .then((list) => setControls(Object.fromEntries(list.map((c) => [c.expenseId, c]))))
      .catch(() => setControls({}));
  }, [result]);

  React.useEffect(() => {
    bf.get("/expense-controls/departments")
      .then((list) => setDepartments(list.map((d) => ({ value: String(d.departmentId), label: d.name }))))
      .catch(() => setDepartments([]));
  }, []);

  const decide = async (x) => {
    try {
      await bf.post(`/expense-controls/${x.expenseId}/approve`, { comment: null });
      toast.success(`${x.expenseNumber} approved.`);
      refresh();
    } catch (error) {
      toast.error(errorText(error, "Could not approve."));
    }
  };
  const resubmit = async (x) => {
    try {
      await bf.post(`/expense-controls/${x.expenseId}/resubmit`);
      toast.success(`${x.expenseNumber} sent for approval again.`);
      refresh();
    } catch (error) {
      toast.error(errorText(error, "Could not resubmit."));
    }
  };""")

# ── columns ─────────────────────────────────────────────────────────────────
swap("""    { key: "category", header: "Category", render: (x) => x.category },""",
"""    { key: "category", header: "Category", render: (x) => x.category },
    { key: "department", header: "Department", render: (x) => controls[x.expenseId]?.departmentName ?? e.jsx(Muted, { children: "—" }) },""")
swap("""    { key: "mode", header: "Paid by", render: (x) => PAYMENT_METHODS[x.paymentMethod] ?? "—" },""",
"""    {
      key: "receipt",
      header: "Receipt",
      render: (x) => {
        const c = controls[x.expenseId];
        const r = c?.receipts?.[0];
        if (r)
          return e.jsx("button", {
            type: "button",
            className: "bf-link",
            onClick: () => bf.open(`/expense-controls/${x.expenseId}/receipts/${r.expenseReceiptId}`).catch(() => toast.error("Could not open the receipt.")),
            children: `${r.contentType.includes("pdf") ? "PDF" : r.contentType.includes("png") ? "PNG" : "JPG"}${c.receipts.length > 1 ? ` +${c.receipts.length - 1}` : ""}`
          });
        return c?.receiptMissing ? bfPill("missing", "danger") : e.jsx(Muted, { children: "—" });
      }
    },
    {
      key: "status",
      header: "Status",
      render: (x) => {
        const c = controls[x.expenseId];
        if (!c) return e.jsx(Muted, { children: "—" });
        const tone = c.isRecurringDraft ? "info" : c.approvalStatus === 1 ? "warning" : c.approvalStatus === 3 ? "danger" : c.isReimbursement && !c.reimbursedUtc ? "info" : "success";
        return e.jsxs("div", { children: [
          bfPill(c.statusLabel, tone),
          c.approvalStatus === 3 && c.decisionComment ? e.jsx(Muted, { children: c.decisionComment }) : null,
          c.approvalStatus === 1 && !c.isRecurringDraft ? e.jsx(Muted, { children: c.approvalLevel === 2 ? "Needs the owner" : "Needs the office manager" }) : null
        ] });
      }
    },
    { key: "mode", header: "Paid by", render: (x) => PAYMENT_METHODS[x.paymentMethod] ?? "—" },""")
swap("""            canEdit && !x.isReconciled &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(x), children: "Edit" }),""",
"""            controls[x.expenseId]?.canApprove &&
              e.jsx(Button, { size: "sm", onClick: () => decide(x), children: "Approve" }),
            controls[x.expenseId]?.canApprove &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setRejecting(x), children: "Reject" }),
            canEdit && controls[x.expenseId]?.approvalStatus === 3 &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => resubmit(x), children: "Resubmit" }),
            canCreate && controls[x.expenseId]?.isRecurringDraft &&
              e.jsx(Button, { size: "sm", onClick: () => setConfirming(x), children: "Confirm" }),
            canReimburse && controls[x.expenseId]?.isReimbursement && controls[x.expenseId]?.approvalStatus === 2 && !controls[x.expenseId]?.reimbursedUtc &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setReimbursing(x), children: "Reimburse" }),
            e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setReceiptsFor(x), children: "Receipts" }),
            canEdit && !x.isReconciled &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(x), children: "Edit" }),""")

# ── filters and tabs ────────────────────────────────────────────────────────
swap("""                      e.jsx(Select, { label: "Category", value: category, placeholder: "All categories", options: categories, onChange: onFilter(setCategory) }),""",
"""                      e.jsx(Select, { label: "Category", value: category, placeholder: "All categories", options: categories, onChange: onFilter(setCategory) }),
                      e.jsx(Select, { label: "Status", value: status, placeholder: "Any status", options: STATUS_FILTERS, onChange: onFilter(setStatus) }),
                      e.jsx(Select, { label: "Department", value: department, placeholder: "All departments", options: departments, onChange: onFilter(setDepartment) }),""")
swap("""                onClick: () => setTab("adjustments"),
                children: "Adjustment history"
              })""",
"""                onClick: () => setTab("adjustments"),
                children: "Adjustment history"
              }),
              e.jsx("button", { type: "button", role: "tab", className: "ex-tab", "aria-selected": tab === "approvals", onClick: () => setTab("approvals"), children: "Awaiting approval" }),
              e.jsx("button", { type: "button", role: "tab", className: "ex-tab", "aria-selected": tab === "reimburse", onClick: () => setTab("reimburse"), children: "Reimbursements" }),
              e.jsx("button", { type: "button", role: "tab", className: "ex-tab", "aria-selected": tab === "recurring", onClick: () => setTab("recurring"), children: "Recurring" })""")
swap("""            : e.jsx(AdjustmentHistory, {""", """            : tab === "approvals" || tab === "reimburse"
            ? e.jsx(ExpenseQueue, { queue: tab === "approvals" ? "approval" : "reimbursement", version, onChanged: refresh,
                onReject: setRejecting, onReimburse: setReimbursing, onReceipts: setReceiptsFor })
            : tab === "recurring"
            ? e.jsx(RecurringPanel, { version, onChanged: refresh, canCreate, onConfirm: setConfirming })
            : e.jsx(AdjustmentHistory, {""")

# ── dialogs ─────────────────────────────────────────────────────────────────
swap("""      editing &&
        e.jsx(ExpenseForm, {
          expense: editing === "new" ? null : editing,""",
"""      receiptsFor && e.jsx(ReceiptsModal, { expense: receiptsFor, canEdit, onClose: () => setReceiptsFor(null), onChanged: refresh }),
      rejecting &&
        e.jsx(ReasonModal, {
          title: `Reject ${rejecting.expenseNumber}`,
          intro: "It goes back to the person who raised it, who can correct it and resubmit.",
          confirmText: "Reject",
          danger: true,
          onClose: () => setRejecting(null),
          onSubmit: async (reason) => {
            await bf.post(`/expense-controls/${rejecting.expenseId}/reject`, { comment: reason });
            toast.success(`${rejecting.expenseNumber} rejected.`);
            setRejecting(null);
            refresh();
          }
        }),
      reimbursing && e.jsx(ReimburseModal, { expense: reimbursing, onClose: () => setReimbursing(null), onDone: () => { setReimbursing(null); refresh(); } }),
      confirming && e.jsx(ConfirmRecurringModal, { expense: confirming, onClose: () => setConfirming(null), onDone: () => { setConfirming(null); refresh(); } }),

      editing &&
        e.jsx(ExpenseForm, {
          control: editing === "new" ? null : controls[editing.expenseId] ?? null,
          departments,
          expense: editing === "new" ? null : editing,""")

# ── the form: department, who paid, receipts ────────────────────────────────
swap("""function ExpenseForm({ expense, onClose, onSaved }) {""", """function ExpenseForm({ expense, control, departments, onClose, onSaved }) {""")
swap("""    supplierId: expense?.supplierId ? String(expense.supplierId) : ""
  });""", """    supplierId: expense?.supplierId ? String(expense.supplierId) : "",
    departmentId: control?.departmentId ? String(control.departmentId) : "",
    paidFrom: control ? String(control.paidFrom) : "1"
  });
  const [files, setFiles] = React.useState([]);
  const [rules, setRules] = React.useState(null);
  const [me, setMe] = React.useState(null);
  React.useEffect(() => {
    bf.get("/expense-controls/rules").then(setRules).catch(() => setRules(null));
    bf.get("/auth/me").then((m) => setMe(m?.user?.userId ?? m?.userId ?? null)).catch(() => setMe(null));
  }, []);
  const needsReceipt = rules && Number(form.amount) > rules.receiptRequiredAbove && !(control?.receipts?.length) && files.length === 0;""")
swap("""      if (expense) await expensesApi.update(expense.expenseId, body);
      else await expensesApi.create(body);
      toast.success(expense ? "Expense updated." : "Expense recorded.");
      onSaved();""", """      const saved = expense ? await expensesApi.update(expense.expenseId, body) : await expensesApi.create(body);
      const id = saved?.expenseId ?? expense?.expenseId;
      if (id) {
        const staff = form.paidFrom === "3";
        try {
          await bf.put(`/expense-controls/${id}`, {
            departmentId: form.departmentId ? Number(form.departmentId) : null,
            paidFrom: Number(form.paidFrom),
            isReimbursement: staff,
            reimburseToUserId: staff ? control?.reimburseToUserId ?? me : null
          });
          for (const f of files) await bf.upload(`/expense-controls/${id}/receipts`, {}, f);
        } catch (error) {
          toast.error(errorText(error, "The expense is saved, but its department, receipts or who paid could not be saved."));
        }
      }
      toast.success(expense ? "Expense updated." : "Expense recorded.");
      onSaved();""")
swap("""        e.jsx(Input, { label: "Remarks", required: true, value: form.remarks, hint: "Why it was needed — required on every expense", error: fieldErrors.remarks, onChange: (ev) => set("remarks", ev.target.value) })
      ]""", """        e.jsx(Input, { label: "Remarks", required: true, value: form.remarks, hint: "Why it was needed — required on every expense", error: fieldErrors.remarks, onChange: (ev) => set("remarks", ev.target.value) }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Select, { label: "Department", value: form.departmentId, placeholder: "Not set", options: departments ?? [], onChange: (ev) => set("departmentId", ev.target.value) }),
            e.jsx(Select, {
              label: "Who paid",
              value: form.paidFrom,
              options: [
                { value: "1", label: "Company bank account" },
                { value: "2", label: "Petty cash" },
                { value: "3", label: "I paid it myself — reimburse me" }
              ],
              hint: form.paidFrom === "2" ? "Taken from the petty cash tin once approved." : form.paidFrom === "3" ? "Becomes Reimbursement due once approved." : undefined,
              onChange: (ev) => set("paidFrom", ev.target.value)
            })
          ]
        }),
        e.jsx(DropZone, { accept: ".pdf,.jpg,.jpeg,.png", multiple: true, label: "Photograph the bill, or drop a PDF (up to 5 files, 10 MB each)", onFile: (f) => setFiles((list) => [...list, f].slice(0, 5)) }),
        files.length > 0 && e.jsx(Muted, { children: `To attach: ${files.map((f) => f.name).join(", ")}` }),
        needsReceipt && e.jsx("div", { className: "bf-note bf-note--warn", children: `A receipt is required for anything above ${fmtMoney(rules.receiptRequiredAbove)}. Attach the bill.` }),
        rules && Number(form.amount) > rules.noApprovalUpTo && e.jsx(Muted, { children: Number(form.amount) > rules.managerUpTo
          ? `Above ${fmtMoney(rules.managerUpTo)}: the owner approves it before it can be paid or matched to the bank.`
          : `Above ${fmtMoney(rules.noApprovalUpTo)}: the office manager approves it before it can be paid or matched to the bank.` })
      ]""")

# ── new components ───────────────────────────────────────────────────────────
swap("""export { AdminExpensesPage };""", r'''// ── Billing & Finance additions (build doc §12) ─────────────────────────────

const STATUS_FILTERS = [
  { value: "awaiting", label: "Awaiting approval" },
  { value: "approved", label: "Approved" },
  { value: "rejected", label: "Rejected" },
  { value: "reimbursement-due", label: "Reimbursement due" },
  { value: "petty-cash", label: "Paid from petty cash" },
  { value: "recurring-draft", label: "Recurring draft" }
];

function ReceiptsModal({ expense, canEdit, onClose, onChanged }) {
  const toast = useToast();
  const [control, setControl] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const load = React.useCallback(() => bf.get("/expense-controls", { ids: String(expense.expenseId) }).then((l) => setControl(l[0] ?? null)), [expense.expenseId]);
  React.useEffect(() => { load().catch(() => setControl(null)); }, [load]);
  const add = async (file) => {
    setBusy(true);
    try {
      setControl(await bf.upload(`/expense-controls/${expense.expenseId}/receipts`, {}, file));
      onChanged();
    } catch (error) {
      toast.error(errorText(error, "Could not attach the receipt."));
    } finally {
      setBusy(false);
    }
  };
  const remove = async (r) => {
    try {
      setControl(await bf.del(`/expense-controls/${expense.expenseId}/receipts/${r.expenseReceiptId}`));
      onChanged();
    } catch (error) {
      toast.error(errorText(error, "Could not remove the receipt."));
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Receipts — ${expense.expenseNumber}`,
    onClose,
    footer: e.jsx(Button, { variant: "outline", onClick: onClose, children: "Close" }),
    children: e.jsxs("div", { className: "form-stack", children: [
      control?.receiptMissing ? e.jsx("div", { className: "bf-note bf-note--warn", children: "A receipt is required for this amount before it can be approved." }) : null,
      (control?.receipts ?? []).length === 0
        ? e.jsx(Muted, { children: "No receipt attached yet." })
        : e.jsx("ul", { style: { margin: 0, paddingLeft: 18 }, children: control.receipts.map((r) => e.jsxs("li", { style: { marginBottom: 6 }, children: [
          e.jsx("button", { type: "button", className: "bf-link", onClick: () => bf.open(`/expense-controls/${expense.expenseId}/receipts/${r.expenseReceiptId}`), children: r.fileName }),
          ` · ${Math.max(1, Math.round(r.sizeBytes / 1024))} KB · ${fmtDate(r.uploadedUtc)} `,
          canEdit ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => remove(r), children: "Remove" }) : null
        ] }, r.expenseReceiptId)) }),
      canEdit && (control?.receipts?.length ?? 0) < 5
        ? e.jsx(DropZone, { accept: ".pdf,.jpg,.jpeg,.png", label: busy ? "Uploading…" : "Photograph the bill, or drop a PDF (10 MB each, up to 5)", onFile: add })
        : null
    ] })
  });
}

function ReimburseModal({ expense, onClose, onDone }) {
  const toast = useToast();
  const [via, setVia] = React.useState("bank");
  const [accounts, setAccounts] = React.useState([]);
  const [account, setAccount] = React.useState("");
  const [reference, setReference] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => {
    bf.get("/bank/accounts").then((r) => setAccounts((Array.isArray(r) ? r : r?.items ?? []).map((a) => ({ value: String(a.bankAccountId), label: `${a.bankName} · ${a.accountCode}` })))).catch(() => setAccounts([]));
  }, []);
  const submit = async () => {
    setBusy(true);
    try {
      await bf.post(`/expense-controls/${expense.expenseId}/reimburse`, { via, bankAccountId: via === "bank" && account ? Number(account) : null, reference: via === "bank" ? reference.trim() : null, paidOn: null });
      toast.success(`${expense.expenseNumber} reimbursed.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not record the reimbursement."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Reimburse — ${expense.expenseNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy || (via === "bank" && (!account || !reference.trim())), children: "Record reimbursement" })
    ] }),
    children: e.jsxs("div", { className: "form-stack", children: [
      e.jsx("p", { style: { margin: 0 }, children: `${fmtMoney(expense.netAmount ?? expense.amount)} is owed to the person who paid. Clear it by bank transfer or from petty cash.` }),
      e.jsx(Select, { label: "Paid by", value: via, options: [{ value: "bank", label: "Bank transfer" }, { value: "petty", label: "Petty cash" }], onChange: (ev) => setVia(ev.target.value) }),
      via === "bank" && e.jsx(Select, { label: "From account", required: true, value: account, placeholder: "Choose an account", options: accounts, onChange: (ev) => setAccount(ev.target.value) }),
      via === "bank" && e.jsx(Input, { label: "UTR", required: true, value: reference, maxLength: 60, onChange: (ev) => setReference(ev.target.value) })
    ] })
  });
}

function ConfirmRecurringModal({ expense, onClose, onDone }) {
  const toast = useToast();
  const [amount, setAmount] = React.useState(String(expense.amount));
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    setBusy(true);
    try {
      await bf.post(`/expense-controls/${expense.expenseId}/confirm-recurring`, { amount: Number(amount) });
      toast.success(`${expense.expenseNumber} confirmed.`);
      onDone();
    } catch (error) {
      toast.error(errorText(error, "Could not confirm."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Confirm — ${expense.expenseNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }),
      e.jsx(Button, { onClick: submit, loading: busy, disabled: busy || !(Number(amount) > 0), children: "Confirm" })
    ] }),
    children: e.jsxs("div", { className: "form-stack", children: [
      e.jsx("p", { style: { margin: 0 }, children: `${expense.description} — raised three days early so a price change is noticed rather than silently paid. Check this month's amount.` }),
      e.jsx(Input, { label: "This month's amount", type: "number", min: "0", step: "0.01", required: true, value: amount, onChange: (ev) => setAmount(ev.target.value) })
    ] })
  });
}

function ExpenseQueue({ queue, version, onChanged, onReject, onReimburse, onReceipts }) {
  const toast = useToast();
  const [result, setResult] = React.useState(null);
  const [page, setPage] = React.useState(1);
  const [loading, setLoading] = React.useState(true);
  React.useEffect(() => {
    setLoading(true);
    bf.get("/expense-controls/queue", { queue, page, pageSize: 20 })
      .then(setResult)
      .catch((error) => toast.error(errorText(error, "Could not load the list.")))
      .finally(() => setLoading(false));
  }, [queue, page, version]);
  const approve = async (x) => {
    try {
      await bf.post(`/expense-controls/${x.expenseId}/approve`, { comment: null });
      toast.success(`${x.expenseNumber} approved.`);
      onChanged();
    } catch (error) {
      toast.error(errorText(error, "Could not approve."));
    }
  };
  const columns = [
    { key: "voucher", header: "Voucher", render: (x) => e.jsxs("div", { children: [e.jsx("strong", { children: x.expenseNumber }), e.jsx(Muted, { children: fmtDate(x.expenseDate) })] }) },
    { key: "what", header: "Expense", render: (x) => e.jsxs("div", { children: [e.jsx("div", { children: x.description }), e.jsx(Muted, { children: [x.category, x.departmentName].filter(Boolean).join(" · ") })] }) },
    { key: "amount", header: "Amount", render: (x) => fmtMoney(x.amount) },
    { key: "who", header: queue === "approval" ? "Raised by" : "Owed to", render: (x) => (queue === "approval" ? x.enteredBy : x.reimburseToName) ?? "—" },
    { key: "receipts", header: "Receipts", render: (x) => (x.receiptCount ? `${x.receiptCount}` : bfPill("none", "danger")) },
    { key: "level", header: "", render: (x) => (queue === "approval" ? e.jsx(Muted, { children: x.approvalLevel === 2 ? "Needs the owner" : "Needs the office manager" }) : null) },
    {
      key: "actions",
      header: "",
      render: (x) => e.jsxs("div", { className: "row-actions ex-row-actions", children: [
        queue === "approval" && x.canApprove && e.jsx(Button, { size: "sm", onClick: () => approve(x), children: "Approve" }),
        queue === "approval" && x.canApprove && e.jsx(Button, { size: "sm", variant: "outline", onClick: () => onReject({ expenseId: x.expenseId, expenseNumber: x.expenseNumber }), children: "Reject" }),
        queue === "approval" && !x.canApprove && e.jsx(Muted, { children: "Someone else approves this" }),
        queue === "reimbursement" && e.jsx(Button, { size: "sm", onClick: () => onReimburse({ expenseId: x.expenseId, expenseNumber: x.expenseNumber, amount: x.amount, netAmount: x.amount }), children: "Reimburse" }),
        e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => onReceipts({ expenseId: x.expenseId, expenseNumber: x.expenseNumber }), children: "Receipts" })
      ] })
    }
  ];
  return e.jsxs(e.Fragment, { children: [
    e.jsx(Muted, { children: queue === "approval"
      ? "An expense above the first limit waits here and cannot be paid or matched to a bank line until approved. The approver can never be the person who raised it."
      : "Money a member of staff spent from their own pocket. Until it is paid, Regalis owes them." }),
    e.jsx(DataTable, { columns, result, loading, rowKey: (x) => x.expenseId, onPageChange: setPage,
      emptyTitle: queue === "approval" ? "Nothing waiting for approval" : "Nobody is owed anything" })
  ] });
}

function RecurringPanel({ version, onChanged, canCreate, onConfirm }) {
  const toast = useToast();
  const [rows, setRows] = React.useState(null);
  const [drafts, setDrafts] = React.useState(null);
  const [editing, setEditing] = React.useState(null);
  const [tick, setTick] = React.useState(0);
  React.useEffect(() => {
    bf.get("/expense-controls/recurring").then(setRows).catch(() => setRows([]));
    bf.get("/expense-controls/queue", { queue: "drafts", pageSize: 50 }).then(setDrafts).catch(() => setDrafts(null));
  }, [version, tick]);
  const raise = async () => {
    try {
      const r = await bf.postWithMessage("/expense-controls/recurring/raise");
      toast.success(r.message);
      setTick((t) => t + 1);
      onChanged();
    } catch (error) {
      toast.error(errorText(error, "Could not raise the drafts."));
    }
  };
  return e.jsxs("div", { className: "form-stack", children: [
    e.jsx(Muted, { children: "Rent, internet, the housekeeping contract and subscriptions repeat every month. A recurring expense is never posted automatically: it appears as a draft three days early for someone to confirm, so a price change is noticed rather than silently paid." }),
    canCreate && e.jsxs("div", { style: { display: "flex", gap: 8 }, children: [
      e.jsx(Button, { size: "sm", onClick: () => setEditing("new"), children: "New recurring expense" }),
      e.jsx(Button, { size: "sm", variant: "outline", onClick: raise, children: "Raise due drafts now" })
    ] }),
    (drafts?.items ?? []).length > 0 && e.jsxs("div", { className: "bf-note bf-note--info", children: [
      e.jsx("strong", { children: "Drafts waiting for confirmation" }),
      e.jsx("ul", { style: { margin: "6px 0 0", paddingLeft: 18 }, children: drafts.items.map((d) => e.jsxs("li", { children: [
        `${d.expenseNumber} · ${fmtDate(d.expenseDate)} · ${d.description} · ${fmtMoney(d.amount)} `,
        canCreate ? e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => onConfirm({ expenseId: d.expenseId, expenseNumber: d.expenseNumber, amount: d.amount, description: d.description }), children: "Confirm" }) : null
      ] }, d.expenseId)) })
    ] }),
    rows === null ? e.jsx(Muted, { children: "Loading…" }) : rows.length === 0 ? e.jsx(Muted, { children: "No recurring expenses set up." }) :
      e.jsx("div", { className: "bf-table-wrap", children: e.jsxs("table", { className: "bf-table", children: [
        e.jsx("thead", { children: e.jsxs("tr", { children: ["What", "Department", "Amount", "Day", "Runs", "Next draft", ""].map((h) => e.jsx("th", { children: h }, h)) }) }),
        e.jsx("tbody", { children: rows.map((r) => e.jsxs("tr", { children: [
          e.jsx("td", { children: e.jsxs("div", { children: [e.jsx("strong", { children: r.description }), e.jsx(Muted, { children: [r.category, r.supplierName].filter(Boolean).join(" · ") })] }) }),
          e.jsx("td", { children: r.departmentName ?? "—" }),
          e.jsx("td", { className: "bf-num", children: fmtMoney(r.amount) }),
          e.jsx("td", { children: r.dayOfMonth }),
          e.jsx("td", { children: `${fmtDate(r.startOn)}${r.endOn ? ` – ${fmtDate(r.endOn)}` : " onwards"}${r.isActive ? "" : " · stopped"}` }),
          e.jsx("td", { children: r.nextDue ? fmtDate(r.nextDue) : "—" }),
          e.jsx("td", { children: canCreate ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(r), children: "Edit" }) : null })
        ] }, r.recurringExpenseId)) })
      ] }) }),
    editing && e.jsx(RecurringForm, { row: editing === "new" ? null : editing, onClose: () => setEditing(null), onSaved: () => { setEditing(null); setTick((t) => t + 1); } })
  ] });
}

function RecurringForm({ row, onClose, onSaved }) {
  const toast = useToast();
  const [locations, setLocations] = React.useState([]);
  const [departments, setDepartments] = React.useState([]);
  const [busy, setBusy] = React.useState(false);
  const [form, setForm] = React.useState({
    category: row?.category ?? "",
    description: row?.description ?? "",
    departmentId: row?.departmentId ? String(row.departmentId) : "",
    locationId: row ? String(row.locationId) : "",
    amount: row ? String(row.amount) : "",
    paymentMethod: row ? String(row.paymentMethod) : "5",
    dayOfMonth: row ? String(row.dayOfMonth) : "1",
    startOn: row?.startOn ?? today(),
    endOn: row?.endOn ?? "",
    isActive: row?.isActive ?? true
  });
  React.useEffect(() => {
    locationsApi.list({ page: 1, pageSize: 100 }).then((l) => setLocations(l.items.map((x) => ({ value: String(x.locationId), label: x.locationName })))).catch(() => setLocations([]));
    bf.get("/expense-controls/departments").then((d) => setDepartments(d.map((x) => ({ value: String(x.departmentId), label: x.name })))).catch(() => setDepartments([]));
  }, []);
  const set = (k, v) => setForm((f) => ({ ...f, [k]: v }));
  const save = async () => {
    setBusy(true);
    try {
      const body = {
        category: form.category.trim(), description: form.description.trim(), departmentId: form.departmentId ? Number(form.departmentId) : null, supplierId: null,
        locationId: Number(form.locationId), amount: Number(form.amount), paymentMethod: Number(form.paymentMethod), dayOfMonth: Number(form.dayOfMonth),
        startOn: form.startOn, endOn: form.endOn || null, isActive: form.isActive
      };
      if (row) await bf.put(`/expense-controls/recurring/${row.recurringExpenseId}`, body);
      else await bf.post("/expense-controls/recurring", body);
      toast.success("Saved.");
      onSaved();
    } catch (error) {
      toast.error(errorText(error, "Could not save."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: row ? "Edit recurring expense" : "New recurring expense",
    onClose,
    footer: e.jsxs(e.Fragment, { children: [
      e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
      e.jsx(Button, { onClick: save, loading: busy, disabled: busy || !form.category.trim() || !form.description.trim() || !form.locationId || !(Number(form.amount) > 0), children: "Save" })
    ] }),
    children: e.jsxs("div", { className: "form-stack", children: [
      e.jsxs("div", { className: "form-grid", children: [
        e.jsx(Input, { label: "Category", required: true, value: form.category, onChange: (ev) => set("category", ev.target.value) }),
        e.jsx(Input, { label: "Amount each month", type: "number", min: "0", step: "0.01", required: true, value: form.amount, onChange: (ev) => set("amount", ev.target.value) }),
        e.jsx(Select, { label: "Department", value: form.departmentId, placeholder: "Not set", options: departments, onChange: (ev) => set("departmentId", ev.target.value) }),
        e.jsx(Select, { label: "Location", required: true, value: form.locationId, placeholder: "Select a location", options: locations, onChange: (ev) => set("locationId", ev.target.value) }),
        e.jsx(Select, { label: "Paid by", value: form.paymentMethod, options: PAYMENT_OPTIONS, onChange: (ev) => set("paymentMethod", ev.target.value) }),
        e.jsx(Input, { label: "Day of the month", type: "number", min: "1", max: "28", required: true, value: form.dayOfMonth, onChange: (ev) => set("dayOfMonth", ev.target.value) }),
        e.jsx(Input, { label: "Starts", type: "date", required: true, value: form.startOn, onChange: (ev) => set("startOn", ev.target.value) }),
        e.jsx(Input, { label: "Ends", type: "date", value: form.endOn, hint: "Leave empty to keep going", onChange: (ev) => set("endOn", ev.target.value) })
      ] }),
      e.jsx(Input, { label: "Description", required: true, value: form.description, onChange: (ev) => set("description", ev.target.value) }),
      e.jsxs("label", { style: { display: "flex", gap: 8, alignItems: "center" }, children: [
        e.jsx("input", { type: "checkbox", checked: form.isActive, onChange: (ev) => set("isActive", ev.target.checked) }), "Active"
      ] })
    ] })
  });
}

export { AdminExpensesPage };''')

open(dst, "w", encoding="utf-8", newline="\n").write(t)
print("wrote", dst)
