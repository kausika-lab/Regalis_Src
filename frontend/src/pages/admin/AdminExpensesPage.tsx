import { useCallback, useEffect, useState } from 'react';
import { expensesApi, suppliersApi } from '@/api/procurement';
import { locationsApi } from '@/api/masters';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  ConfirmDialog,
  DataTable,
  Input,
  Modal,
  PageHeader,
  Select,
  type Column,
  type SelectOption,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useDebounce } from '@/hooks/useDebounce';
import { useFieldErrors } from '@/hooks/useFieldErrors';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import {
  PAYMENT_MODE_LABELS,
  type Expense,
  type ExpenseSummary,
  type SaveExpensePayload,
} from '@/models/procurement';
import { formatDate, formatInr } from '@/utils/formatters';

const PAYMENT_MODE_OPTIONS: SelectOption[] = Object.entries(PAYMENT_MODE_LABELS).map(
  ([value, label]) => ({ value: Number(value), label }),
);

export function AdminExpensesPage() {
  const toast = useToast();
  const canCreate = usePermission('EXPENSE.CREATE');
  const canEdit = usePermission('EXPENSE.EDIT');
  const canDelete = usePermission('EXPENSE.DELETE');
  const canExport = usePermission('EXPENSE.EXPORT');

  const [result, setResult] = useState<PagedResult<Expense> | null>(null);
  const [summary, setSummary] = useState<ExpenseSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 300);
  const [category, setCategory] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [categories, setCategories] = useState<SelectOption[]>([]);
  const [editing, setEditing] = useState<Expense | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Expense | null>(null);

  const query = {
    page,
    pageSize: 20,
    search: debouncedSearch || undefined,
    category: category || undefined,
    from: from || undefined,
    to: to || undefined,
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [rows, totals] = await Promise.all([
        expensesApi.list(query),
        expensesApi.summary({ ...query, page: 1 }),
      ]);
      setResult(rows);
      setSummary(totals);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load expenses.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, category, from, to]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void (async () => {
      try {
        const list = await expensesApi.categories();
        setCategories(list.map((c) => ({ value: c, label: c })));
      } catch {
        setCategories([]);
      }
    })();
  }, []);

  const columns: Column<Expense>[] = [
    {
      key: 'voucher',
      header: 'Voucher',
      render: (e) => (
        <div>
          <div style={{ fontWeight: 600 }}>{e.expenseNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {formatDate(e.expenseDate)}
          </div>
        </div>
      ),
    },
    { key: 'category', header: 'Category', render: (e) => e.category },
    {
      key: 'description',
      header: 'Description',
      render: (e) => (
        <div>
          <div>{e.description}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {e.remarks}
          </div>
        </div>
      ),
    },
    {
      key: 'amount',
      header: 'Amount',
      render: (e) => <strong>{formatInr(e.amount)}</strong>,
    },
    {
      key: 'mode',
      header: 'Paid by',
      render: (e) => PAYMENT_MODE_LABELS[e.paymentMethod] ?? '—',
    },
    { key: 'enteredBy', header: 'Entered by', render: (e) => e.enteredByName },
    {
      key: 'reconciled',
      header: 'Bank',
      render: (e) =>
        e.isReconciled ? <Badge tone="success">Matched</Badge> : <Badge tone="neutral">—</Badge>,
    },
    {
      key: 'actions',
      header: '',
      render: (e) => (
        <div className="row-actions">
          {canEdit && !e.isReconciled && (
            <Button size="sm" variant="outline" onClick={() => setEditing(e)}>
              Edit
            </Button>
          )}
          {canDelete && !e.isReconciled && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(e)}>
              Delete
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Expenses"
        description="Every daily company expense. An expense matched to a bank statement line is locked — unmatch it first."
        actions={
          <>
            {canExport && (
              <Button variant="outline" onClick={() => void expensesApi.export(query)}>
                Export CSV
              </Button>
            )}
            {canCreate && <Button onClick={() => setEditing('new')}>Add expense</Button>}
          </>
        }
      />

      {summary && (
        <Card>
          <div className="toolbar">
            <div>
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                Total for this filter
              </div>
              <div style={{ fontSize: 'var(--font-size-lg)', fontWeight: 600 }}>
                {formatInr(summary.totalAmount)}
              </div>
            </div>
            <div>
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                Entries
              </div>
              <div style={{ fontSize: 'var(--font-size-lg)', fontWeight: 600 }}>
                {summary.totalCount}
              </div>
            </div>
          </div>
        </Card>
      )}

      <Card>
        <div className="toolbar">
          <Input
            label="Search"
            placeholder="Voucher, description or remarks"
            value={search}
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
          />
          <Select
            label="Category"
            value={category}
            placeholder="All categories"
            options={categories}
            onChange={(e) => {
              setPage(1);
              setCategory(e.target.value);
            }}
          />
          <Input
            label="From"
            type="date"
            value={from}
            onChange={(e) => {
              setPage(1);
              setFrom(e.target.value);
            }}
          />
          <Input
            label="To"
            type="date"
            value={to}
            onChange={(e) => {
              setPage(1);
              setTo(e.target.value);
            }}
          />
        </div>
        <DataTable
          columns={columns}
          result={result}
          loading={loading}
          rowKey={(e) => e.expenseId}
          onPageChange={setPage}
          emptyTitle="No expenses recorded"
          emptyDescription="Record daily company expenses here — they feed the expense report."
        />
      </Card>

      {editing && (
        <ExpenseEditorModal
          expense={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void load();
          }}
        />
      )}

      <ConfirmDialog
        open={deleteTarget !== null}
        danger
        confirmLabel="Delete"
        title={`Delete ${deleteTarget?.expenseNumber ?? ''}?`}
        message="The voucher is retained for audit; it stops appearing in the expense register."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await expensesApi.remove(deleteTarget.expenseId);
            toast.success('Expense deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)}
      />
    </>
  );
}

function ExpenseEditorModal({
  expense,
  onClose,
  onSaved,
}: {
  expense: Expense | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [locations, setLocations] = useState<SelectOption[]>([]);
  const [suppliers, setSuppliers] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    expenseDate: expense?.expenseDate ?? new Date().toISOString().slice(0, 10),
    category: expense?.category ?? '',
    description: expense?.description ?? '',
    amount: expense ? String(expense.amount) : '',
    paymentMethod: expense ? String(expense.paymentMethod) : '6',
    remarks: expense?.remarks ?? '',
    locationId: expense ? String(expense.locationId) : '',
    supplierId: expense?.supplierId ? String(expense.supplierId) : '',
  });

  useEffect(() => {
    void (async () => {
      try {
        const [locationPage, supplierPage] = await Promise.all([
          locationsApi.list({ page: 1, pageSize: 100 }),
          suppliersApi.list({ page: 1, pageSize: 100 }),
        ]);
        setLocations(locationPage.items.map((l) => ({ value: l.locationId, label: l.locationName })));
        setSuppliers(supplierPage.items.map((s) => ({ value: s.supplierId, label: s.supplierName })));
        // Default a new expense to the only location, so the common case is one click.
        if (!expense && locationPage.items.length === 1) {
          setForm((f) => ({ ...f, locationId: String(locationPage.items[0].locationId) }));
        }
      } catch {
        setLocations([]);
        setSuppliers([]);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const set = (name: keyof typeof form, value: string) => {
    clearField(name);
    setForm((f) => ({ ...f, [name]: value }));
  };

  const save = async () => {
    setBusy(true);
    clear();
    const payload: SaveExpensePayload = {
      expenseDate: form.expenseDate || null,
      category: form.category.trim(),
      description: form.description.trim(),
      amount: Number(form.amount),
      paymentMethod: Number(form.paymentMethod),
      remarks: form.remarks.trim(),
      attachmentPath: expense?.attachmentPath ?? null,
      locationId: Number(form.locationId),
      supplierId: form.supplierId ? Number(form.supplierId) : null,
      purchaseOrderId: expense?.purchaseOrderId ?? null,
    };
    try {
      if (expense) {
        await expensesApi.update(expense.expenseId, payload);
      } else {
        await expensesApi.create(payload);
      }
      toast.success(expense ? 'Expense updated.' : 'Expense recorded.');
      onSaved();
    } catch (error) {
      const message = capture(error);
      if (message) {
        toast.error(message);
      }
    } finally {
      setBusy(false);
    }
  };

  // Description and Remarks are required by the requirements document, so the button
  // stays disabled until both are filled — the server enforces it as well.
  const canSave =
    form.category.trim().length > 0 &&
    form.description.trim().length > 0 &&
    form.remarks.trim().length > 0 &&
    form.locationId !== '' &&
    Number(form.amount) > 0;

  return (
    <Modal
      open
      title={expense ? `Edit — ${expense.expenseNumber}` : 'Add expense'}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} loading={busy} disabled={!canSave}>
            Save
          </Button>
        </>
      }
    >
      <div className="form-stack">
        {summary && (
          <div className="form-error" role="alert">
            {summary}
          </div>
        )}
        <div className="form-grid">
          <Input
            label="Expense date"
            type="date"
            required
            value={form.expenseDate}
            error={fieldErrors.expenseDate}
            onChange={(e) => set('expenseDate', e.target.value)}
          />
          <Input
            label="Category"
            required
            value={form.category}
            hint="e.g. Office Maintenance"
            error={fieldErrors.category}
            onChange={(e) => set('category', e.target.value)}
          />
          <Input
            label="Amount"
            type="number"
            min="0"
            step="0.01"
            required
            value={form.amount}
            error={fieldErrors.amount}
            onChange={(e) => set('amount', e.target.value)}
          />
          <Select
            label="Payment mode"
            required
            value={form.paymentMethod}
            options={PAYMENT_MODE_OPTIONS}
            error={fieldErrors.paymentMethod}
            onChange={(e) => set('paymentMethod', e.target.value)}
          />
          <Select
            label="Location"
            required
            value={form.locationId}
            placeholder="Select a location"
            options={locations}
            error={fieldErrors.locationId}
            onChange={(e) => set('locationId', e.target.value)}
          />
          <Select
            label="Supplier"
            value={form.supplierId}
            placeholder="Not linked"
            options={suppliers}
            error={fieldErrors.supplierId}
            onChange={(e) => set('supplierId', e.target.value)}
          />
        </div>
        <Input
          label="Description"
          required
          value={form.description}
          hint="What the money was spent on"
          error={fieldErrors.description}
          onChange={(e) => set('description', e.target.value)}
        />
        <Input
          label="Remarks"
          required
          value={form.remarks}
          hint="Why it was needed — required on every expense"
          error={fieldErrors.remarks}
          onChange={(e) => set('remarks', e.target.value)}
        />
      </div>
    </Modal>
  );
}
