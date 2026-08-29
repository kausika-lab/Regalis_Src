import { useCallback, useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { invoicesApi as invoicePdfApi } from '@/api/bookings';
import { invoiceApi, passApi, paymentAdminApi, walletApi } from '@/api/finance';
import { usersApi } from '@/api/admin';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  DataTable,
  Input,
  Modal,
  PageHeader,
  Select,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useDebounce } from '@/hooks/useDebounce';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { UserListItem } from '@/models/admin';
import type {
  CustomerPass,
  Invoice,
  PassPlan,
  PaymentRow,
  Wallet,
  WalletTransaction,
} from '@/models/finance';
import {
  INVOICE_STATUS_LABELS,
  PASS_STATUS_LABELS,
  PAYMENT_METHOD_LABELS,
  PAYMENT_STATUS_LABELS,
  WALLET_CATEGORY_LABELS,
} from '@/models/finance';
import { formatDate, formatDateTime, formatInr } from '@/utils/formatters';

// ── /admin/invoices ─────────────────────────────────────────────────────────

export function AdminInvoicesPage() {
  const toast = useToast();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [result, setResult] = useState<PagedResult<Invoice> | null>(null);
  const [loading, setLoading] = useState(true);
  const debouncedSearch = useDebounce(search, 350);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await invoiceApi.list({
        page, pageSize: 20,
        search: debouncedSearch || undefined,
        status: status ? Number(status) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load invoices.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, status]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<Invoice>[] = [
    {
      key: 'number', header: 'Invoice',
      render: (i) => (
        <div>
          <div style={{ fontWeight: 600 }}>{i.invoiceNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {i.customerName} · {i.customerEmail}
          </div>
        </div>
      ),
    },
    { key: 'date', header: 'Date', render: (i) => formatDate(i.invoiceDate) },
    { key: 'taxable', header: 'Taxable', render: (i) => formatInr(i.taxableAmount) },
    { key: 'gst', header: 'GST', render: (i) => formatInr(i.cgstAmount + i.sgstAmount) },
    { key: 'total', header: 'Total', render: (i) => formatInr(i.totalAmount) },
    {
      key: 'status', header: 'Status',
      render: (i) => (
        <Badge tone={i.status === 2 ? 'success' : i.status === 4 ? 'neutral' : 'warning'}>
          {INVOICE_STATUS_LABELS[i.status]}
        </Badge>
      ),
    },
    {
      key: 'pdf', header: '',
      render: (i) => (
        <Button size="sm" variant="outline"
          onClick={() => invoicePdfApi.downloadPdf(i.invoiceId, i.invoiceNumber)}>
          <Download size={14} /> PDF
        </Button>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Invoices" description="Every GST invoice issued by the system." />
      <Card>
        <div className="toolbar">
          <Input label="Search" placeholder="Number, customer…" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          <Select label="Status" value={status} placeholder="All statuses"
            options={Object.entries(INVOICE_STATUS_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(i) => i.invoiceId} onPageChange={setPage} emptyTitle="No invoices yet" />
      </Card>
    </>
  );
}

// ── /admin/payments ─────────────────────────────────────────────────────────

export function AdminPaymentsPage() {
  const toast = useToast();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [result, setResult] = useState<PagedResult<PaymentRow> | null>(null);
  const [loading, setLoading] = useState(true);
  const debouncedSearch = useDebounce(search, 350);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await paymentAdminApi.list({
        page, pageSize: 20,
        search: debouncedSearch || undefined,
        status: status ? Number(status) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load payments.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, status]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<PaymentRow>[] = [
    {
      key: 'number', header: 'Payment',
      render: (p) => (
        <div>
          <div style={{ fontWeight: 600 }}>{p.paymentNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{p.customerName}</div>
        </div>
      ),
    },
    { key: 'amount', header: 'Amount', render: (p) => formatInr(p.amount) },
    { key: 'method', header: 'Method', render: (p) => PAYMENT_METHOD_LABELS[p.paymentMethod] ?? '—' },
    { key: 'gateway', header: 'Gateway', render: (p) => p.gateway ?? '—' },
    {
      key: 'status', header: 'Status',
      render: (p) => (
        <Badge tone={p.status === 2 ? 'success' : p.status === 3 ? 'danger' : 'warning'}>
          {PAYMENT_STATUS_LABELS[p.status]}
        </Badge>
      ),
    },
    { key: 'when', header: 'Paid (IST)', render: (p) => p.paidUtc ? formatDateTime(p.paidUtc) : '—' },
  ];

  return (
    <>
      <PageHeader title="Payments" description="Wallet, gateway and cash settlements." />
      <Card>
        <div className="toolbar">
          <Input label="Search" placeholder="Payment no., order id…" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          <Select label="Status" value={status} placeholder="All statuses"
            options={Object.entries(PAYMENT_STATUS_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(p) => p.paymentId} onPageChange={setPage} emptyTitle="No payments yet" />
      </Card>
    </>
  );
}

// ── /admin/wallet ───────────────────────────────────────────────────────────

export function AdminWalletPage() {
  const toast = useToast();
  const canAdjust = usePermission('WALLET.EDIT');
  const [search, setSearch] = useState('');
  const [customers, setCustomers] = useState<UserListItem[]>([]);
  const [selected, setSelected] = useState<UserListItem | null>(null);
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [transactions, setTransactions] = useState<PagedResult<WalletTransaction> | null>(null);
  const [page, setPage] = useState(1);
  const [adjustOpen, setAdjustOpen] = useState(false);
  const debouncedSearch = useDebounce(search, 350);

  useEffect(() => {
    if (!debouncedSearch) { setCustomers([]); return; }
    usersApi.list({ page: 1, pageSize: 8, search: debouncedSearch, userType: 1 })
      .then((result) => setCustomers(result.items)).catch(() => {});
  }, [debouncedSearch]);

  const loadWallet = useCallback(async (customer: UserListItem, pageNumber: number) => {
    try {
      const [walletData, transactionPage] = await Promise.all([
        walletApi.ofCustomer(customer.userId),
        walletApi.customerTransactions(customer.userId, { page: pageNumber, pageSize: 15 }),
      ]);
      setWallet(walletData);
      setTransactions(transactionPage);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the wallet.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (selected) { void loadWallet(selected, page); }
  }, [selected, page, loadWallet]);

  const columns: Column<WalletTransaction>[] = [
    { key: 'number', header: 'Txn', render: (t) => t.transactionNumber },
    { key: 'narration', header: 'Narration', render: (t) => t.narration },
    { key: 'category', header: 'Category', render: (t) => WALLET_CATEGORY_LABELS[t.category] },
    {
      key: 'amount', header: 'Amount',
      render: (t) => (
        <span style={{ color: t.direction === 1 ? 'var(--color-success)' : 'var(--color-danger)' }}>
          {t.direction === 1 ? '+' : '−'}{formatInr(t.amount)}
        </span>
      ),
    },
    { key: 'balance', header: 'Balance', render: (t) => formatInr(t.balanceAfter) },
    { key: 'when', header: 'When (IST)', render: (t) => formatDateTime(t.transactionUtc) },
  ];

  return (
    <>
      <PageHeader title="Customer Wallets"
        description="The ledger is append-only and self-checking: credits − debits always equal the balance." />
      <Card>
        <div className="toolbar">
          <Input label="Find customer" placeholder="Name, email or mobile…" value={search}
            onChange={(e) => setSearch(e.target.value)} />
          {customers.length > 0 && (
            <Select label="Customer" value={selected ? String(selected.userId) : ''}
              placeholder="Select customer"
              options={customers.map((c) => ({ value: c.userId, label: `${c.fullName} · ${c.email}` }))}
              onChange={(e) => {
                const customer = customers.find((c) => String(c.userId) === e.target.value) ?? null;
                setSelected(customer);
                setPage(1);
              }} />
          )}
          {selected && wallet && (
            <div className="dash-tile" style={{ cursor: 'default', padding: 'var(--space-3) var(--space-4)' }}>
              <div>
                <div className="dash-tile__label">{selected.fullName}</div>
                <div style={{ fontWeight: 800, fontSize: 'var(--font-size-lg)' }}>{formatInr(wallet.balance)}</div>
              </div>
            </div>
          )}
          {selected && canAdjust && (
            <Button onClick={() => setAdjustOpen(true)}>Adjust</Button>
          )}
        </div>
        {selected && (
          <DataTable columns={columns} result={transactions} loading={false}
            rowKey={(t) => t.walletTransactionId} onPageChange={setPage}
            emptyTitle="No transactions" />
        )}
      </Card>

      {selected && adjustOpen && (
        <AdjustModal customer={selected}
          onClose={() => setAdjustOpen(false)}
          onDone={() => { setAdjustOpen(false); void loadWallet(selected, page); }} />
      )}
    </>
  );
}

function AdjustModal({ customer, onClose, onDone }: {
  customer: UserListItem;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [direction, setDirection] = useState<'credit' | 'debit'>('credit');
  const [amount, setAmount] = useState(100);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await walletApi.adjust(customer.userId, direction, amount, reason.trim());
      toast.success('Wallet adjusted. The reason is on the audit trail.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Adjustment failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Adjust wallet — ${customer.fullName}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={submit} loading={busy} disabled={!reason.trim()}>Apply</Button>
        </>
      }>
      <div className="form-stack">
        <Select label="Direction" value={direction}
          options={[{ value: 'credit', label: 'Credit (add funds)' }, { value: 'debit', label: 'Debit (remove funds)' }]}
          onChange={(e) => setDirection(e.target.value as 'credit' | 'debit')} />
        <Input label="Amount (₹)" type="number" min={1} value={amount}
          onChange={(e) => setAmount(Number(e.target.value))} />
        <Input label="Reason" required value={reason} hint="Mandatory — written to the audit trail"
          onChange={(e) => setReason(e.target.value)} />
      </div>
    </Modal>
  );
}

// ── /admin/passes/day + /admin/passes/monthly ───────────────────────────────

export function AdminPassesPage({ passType }: { passType: 1 | 2 }) {
  const toast = useToast();
  const canCreate = usePermission('PASS.CREATE');
  const canEdit = usePermission('PASS.EDIT');
  const [plans, setPlans] = useState<PassPlan[]>([]);
  const [purchases, setPurchases] = useState<PagedResult<CustomerPass> | null>(null);
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<PassPlan | 'new' | null>(null);

  const load = useCallback(async () => {
    try {
      const [allPlans, purchasePage] = await Promise.all([
        passApi.allPlans(),
        passApi.list({ page, pageSize: 15, passType }),
      ]);
      setPlans(allPlans.filter((p) => p.passType === passType));
      setPurchases(purchasePage);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load passes.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, passType]);

  useEffect(() => { void load(); }, [load]);

  const title = passType === 1 ? 'Day Pass' : 'Monthly Pass';

  const purchaseColumns: Column<CustomerPass>[] = [
    {
      key: 'pass', header: 'Pass',
      render: (p) => (
        <div>
          <div style={{ fontWeight: 600 }}>{p.passNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {p.customerName} · {p.planName}
          </div>
        </div>
      ),
    },
    { key: 'valid', header: 'Valid', render: (p) => `${formatDate(p.validFrom)} – ${formatDate(p.validTo)}` },
    { key: 'amount', header: 'Paid', render: (p) => formatInr(p.totalAmount) },
    {
      key: 'status', header: 'Status',
      render: (p) => (
        <Badge tone={p.status === 1 ? 'success' : p.status === 4 ? 'danger' : 'neutral'}>
          {PASS_STATUS_LABELS[p.status]}
        </Badge>
      ),
    },
    {
      key: 'actions', header: '',
      render: (p) => canEdit && p.status === 1 ? (
        <Button size="sm" variant="ghost" onClick={async () => {
          try {
            await passApi.cancel(p.customerPassId);
            toast.success('Pass cancelled. Pre-start passes refund to the wallet.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Cancel failed.');
          }
        }}>
          Cancel
        </Button>
      ) : null,
    },
  ];

  return (
    <>
      <PageHeader title={`${title} plans`}
        actions={canCreate && <Button onClick={() => setEditing('new')}>New plan</Button>} />
      <Card>
        <div className="choice-grid">
          {plans.map((plan) => (
            <div key={plan.passPlanId} className="choice-card" style={{ cursor: 'default' }}>
              <div className="choice-card__title">
                {plan.planName} {!plan.isActive && <Badge tone="neutral">Inactive</Badge>}
              </div>
              <div className="choice-card__meta">
                {plan.planCode} · {plan.validityDays} day{plan.validityDays > 1 ? 's' : ''}
              </div>
              <div className="choice-card__price">{formatInr(plan.totalAmount)} incl. GST</div>
              {canEdit && (
                <div style={{ marginTop: 'var(--space-2)' }}>
                  <Button size="sm" variant="outline" onClick={() => setEditing(plan)}>Edit</Button>
                </div>
              )}
            </div>
          ))}
          {plans.length === 0 && (
            <p style={{ color: 'var(--text-muted)' }}>No plans yet — create the first one.</p>
          )}
        </div>
      </Card>
      <Card title="Purchases">
        <DataTable columns={purchaseColumns} result={purchases} loading={false}
          rowKey={(p) => p.customerPassId} onPageChange={setPage} emptyTitle="No purchases yet" />
      </Card>

      {editing && (
        <PlanEditorModal passType={passType} plan={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onDone={() => { setEditing(null); void load(); }} />
      )}
    </>
  );
}

function PlanEditorModal({ passType, plan, onClose, onDone }: {
  passType: 1 | 2;
  plan: PassPlan | null;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    planCode: plan?.planCode ?? '',
    planName: plan?.planName ?? '',
    price: plan?.price ?? (passType === 1 ? 800 : 12000),
    validityDays: plan?.validityDays ?? (passType === 1 ? 1 : 30),
    description: plan?.description ?? '',
    termsAndConditions: plan?.termsAndConditions ?? '',
    displayOrder: plan?.displayOrder ?? 10,
    isActive: plan?.isActive ?? true,
  });
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    const payload = {
      planCode: form.planCode.trim().toUpperCase(),
      planName: form.planName.trim(),
      passType,
      locationId: null,
      price: Number(form.price),
      validityDays: Number(form.validityDays),
      maxGuests: null,
      description: form.description || null,
      termsAndConditions: form.termsAndConditions || null,
      displayOrder: Number(form.displayOrder),
      isActive: form.isActive,
    };
    try {
      if (plan) {
        await passApi.updatePlan(plan.passPlanId, payload);
      } else {
        await passApi.createPlan(payload);
      }
      toast.success(plan ? 'Plan updated.' : 'Plan created and on sale.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the plan.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={plan ? `Edit — ${plan.planName}` : 'New plan'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.planName.trim() || (!plan && !form.planCode.trim())}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          {!plan && (
            <Input label="Code" required value={form.planCode} hint="e.g. DAY-STD"
              onChange={(e) => setForm({ ...form, planCode: e.target.value })} />
          )}
          <Input label="Name" required value={form.planName}
            onChange={(e) => setForm({ ...form, planName: e.target.value })} />
          <Input label="Price (₹, before GST)" type="number" min={1} value={form.price}
            onChange={(e) => setForm({ ...form, price: Number(e.target.value) })} />
          <Input label="Validity (days)" type="number" min={1} value={form.validityDays}
            onChange={(e) => setForm({ ...form, validityDays: Number(e.target.value) })} />
          <Input label="Display order" type="number" value={form.displayOrder}
            onChange={(e) => setForm({ ...form, displayOrder: Number(e.target.value) })} />
        </div>
        <Input label="Description" value={form.description}
          onChange={(e) => setForm({ ...form, description: e.target.value })} />
        <label className="checkbox">
          <input type="checkbox" checked={form.isActive}
            onChange={(e) => setForm({ ...form, isActive: e.target.checked })} />
          <span>On sale</span>
        </label>
      </div>
    </Modal>
  );
}
