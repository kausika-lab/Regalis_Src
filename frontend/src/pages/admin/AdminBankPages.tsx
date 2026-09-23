import { useCallback, useEffect, useState } from 'react';
import { bankApi } from '@/api/banking';
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
  Spinner,
  type Column,
  type SelectOption,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useFieldErrors } from '@/hooks/useFieldErrors';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import {
  BANK_DIRECTION,
  BANK_DIRECTION_LABELS,
  RECONCILIATION_LABELS,
  RECONCILIATION_STATUS,
  RECONCILIATION_TONES,
  type BankAccount,
  type BankTransaction,
  type MatchCandidate,
  type SaveBankAccountPayload,
  type SaveBankTransactionPayload,
} from '@/models/banking';
import { formatDate, formatDateTime, formatInr } from '@/utils/formatters';

const DIRECTION_OPTIONS: SelectOption[] = Object.entries(BANK_DIRECTION_LABELS).map(
  ([value, label]) => ({ value: Number(value), label }),
);
const STATUS_OPTIONS: SelectOption[] = Object.entries(RECONCILIATION_LABELS).map(
  ([value, label]) => ({ value: Number(value), label }),
);

// ── /admin/bank/accounts ────────────────────────────────────────────────────

export function AdminBankAccountsPage() {
  const toast = useToast();
  const canCreate = usePermission('BANK.CREATE');
  const canEdit = usePermission('BANK.EDIT');
  const canDelete = usePermission('BANK.DELETE');

  const [accounts, setAccounts] = useState<BankAccount[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [editing, setEditing] = useState<BankAccount | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<BankAccount | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setAccounts(await bankApi.accounts());
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load bank accounts.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<BankAccount>[] = [
    {
      key: 'account',
      header: 'Account',
      render: (a) => (
        <div>
          <div style={{ fontWeight: 600 }}>{a.bankName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {a.accountCode} · {a.accountNumberMasked}
          </div>
        </div>
      ),
    },
    { key: 'name', header: 'Held as', render: (a) => a.accountName },
    { key: 'ifsc', header: 'IFSC', render: (a) => a.ifsc ?? '—' },
    { key: 'location', header: 'Location', render: (a) => a.locationName ?? 'Company-wide' },
    { key: 'credits', header: 'Credits', render: (a) => formatInr(a.totalCredits) },
    { key: 'debits', header: 'Debits', render: (a) => formatInr(a.totalDebits) },
    {
      key: 'balance',
      header: 'Balance',
      render: (a) => <strong>{formatInr(a.currentBalance)}</strong>,
    },
    {
      key: 'unreconciled',
      header: 'Unreconciled',
      render: (a) =>
        a.unreconciledCount > 0 ? <Badge tone="gold">{a.unreconciledCount}</Badge> : '—',
    },
    {
      key: 'actions',
      header: '',
      render: (a) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(a)}>
              Edit
            </Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(a)}>
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
        title="Bank Accounts"
        description="The balance is derived from the statement lines — opening + credits − debits — so it can never drift from the ledger."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New bank account</Button>}
      />
      <Card>
        <DataTable
          columns={columns}
          result={
            accounts
              ? {
                  items: accounts,
                  page: 1,
                  pageSize: accounts.length || 1,
                  totalCount: accounts.length,
                  totalPages: 1,
                  hasNext: false,
                  hasPrevious: false,
                }
              : null
          }
          loading={loading}
          rowKey={(a) => a.bankAccountId}
          onPageChange={() => undefined}
          emptyTitle="No bank accounts yet"
          emptyDescription="Add the accounts the company banks with, then record their statement lines."
        />
      </Card>

      {editing && (
        <BankAccountEditorModal
          account={editing === 'new' ? null : editing}
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
        title={`Delete ${deleteTarget?.bankName ?? ''}?`}
        message="An account with transactions cannot be deleted — deactivate it instead."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await bankApi.removeAccount(deleteTarget.bankAccountId);
            toast.success('Bank account deleted.');
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

function BankAccountEditorModal({
  account,
  onClose,
  onSaved,
}: {
  account: BankAccount | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [locations, setLocations] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    accountCode: account?.accountCode ?? '',
    bankName: account?.bankName ?? '',
    accountName: account?.accountName ?? '',
    accountNumberMasked: account?.accountNumberMasked ?? '',
    ifsc: account?.ifsc ?? '',
    branch: account?.branch ?? '',
    accountType: account?.accountType ?? '',
    openingBalance: account ? String(account.openingBalance) : '0',
    openingBalanceAsOn: account?.openingBalanceAsOn ?? '',
    locationId: account?.locationId ? String(account.locationId) : '',
    notes: account?.notes ?? '',
  });

  useEffect(() => {
    void (async () => {
      try {
        const page = await locationsApi.list({ page: 1, pageSize: 100 });
        setLocations(page.items.map((l) => ({ value: l.locationId, label: l.locationName })));
      } catch {
        setLocations([]);
      }
    })();
  }, []);

  const set = (name: keyof typeof form, value: string) => {
    clearField(name);
    setForm((f) => ({ ...f, [name]: value }));
  };

  const save = async () => {
    setBusy(true);
    clear();
    const payload: SaveBankAccountPayload = {
      accountCode: form.accountCode.trim().toUpperCase(),
      bankName: form.bankName.trim(),
      accountName: form.accountName.trim(),
      accountNumberMasked: form.accountNumberMasked.trim(),
      ifsc: form.ifsc ? form.ifsc.trim().toUpperCase() : null,
      branch: form.branch || null,
      accountType: form.accountType || null,
      openingBalance: Number(form.openingBalance || 0),
      openingBalanceAsOn: form.openingBalanceAsOn || null,
      locationId: form.locationId ? Number(form.locationId) : null,
      notes: form.notes || null,
    };
    try {
      if (account) {
        await bankApi.updateAccount(account.bankAccountId, payload);
      } else {
        await bankApi.createAccount(payload);
      }
      toast.success(account ? 'Bank account updated.' : 'Bank account created.');
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

  const canSave =
    form.accountCode.trim() !== '' &&
    form.bankName.trim() !== '' &&
    form.accountName.trim() !== '' &&
    form.accountNumberMasked.trim() !== '';

  return (
    <Modal
      open
      title={account ? `Edit — ${account.bankName}` : 'New bank account'}
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
          <Input label="Code" required value={form.accountCode} hint="e.g. HDFC-CA-01"
            error={fieldErrors.accountCode} onChange={(e) => set('accountCode', e.target.value)} />
          <Input label="Bank name" required value={form.bankName}
            error={fieldErrors.bankName} onChange={(e) => set('bankName', e.target.value)} />
          <Input label="Account held as" required value={form.accountName}
            error={fieldErrors.accountName} onChange={(e) => set('accountName', e.target.value)} />
          <Input label="Account number" required value={form.accountNumberMasked}
            hint="Store it masked — only the last four are needed"
            error={fieldErrors.accountNumberMasked}
            onChange={(e) => set('accountNumberMasked', e.target.value)} />
          <Input label="IFSC" value={form.ifsc} hint="11 characters"
            error={fieldErrors.ifsc} onChange={(e) => set('ifsc', e.target.value)} />
          <Input label="Branch" value={form.branch}
            error={fieldErrors.branch} onChange={(e) => set('branch', e.target.value)} />
          <Input label="Account type" value={form.accountType} hint="Current / Savings"
            error={fieldErrors.accountType} onChange={(e) => set('accountType', e.target.value)} />
          <Input label="Opening balance" type="number" step="0.01" value={form.openingBalance}
            disabled={account !== null && (account.totalCredits > 0 || account.totalDebits > 0)}
            hint={account ? 'Frozen once the account has transactions' : undefined}
            error={fieldErrors.openingBalance}
            onChange={(e) => set('openingBalance', e.target.value)} />
          <Input label="Opening balance as on" type="date" value={form.openingBalanceAsOn}
            error={fieldErrors.openingBalanceAsOn}
            onChange={(e) => set('openingBalanceAsOn', e.target.value)} />
          <Select label="Location" value={form.locationId} placeholder="Company-wide"
            options={locations} error={fieldErrors.locationId}
            onChange={(e) => set('locationId', e.target.value)} />
        </div>
        <Input label="Notes" value={form.notes} error={fieldErrors.notes}
          onChange={(e) => set('notes', e.target.value)} />
      </div>
    </Modal>
  );
}

// ── /admin/bank/transactions and /admin/bank/reconciliation ─────────────────

export function AdminBankTransactionsPage() {
  return <BankLedger mode="all" />;
}

export function AdminBankReconciliationPage() {
  return <BankLedger mode="reconcile" />;
}

/**
 * One ledger, two framings. "Transactions" is the full statement; "Reconciliation"
 * opens on the lines still needing attention. Both read the same rows — there is no
 * second source of bank data.
 */
function BankLedger({ mode }: { mode: 'all' | 'reconcile' }) {
  const toast = useToast();
  const canCreate = usePermission('BANK.CREATE');
  const canEdit = usePermission('BANK.EDIT');
  const canReconcile = usePermission('BANK.RECONCILE');
  const canExport = usePermission('BANK.EXPORT');

  const [accounts, setAccounts] = useState<BankAccount[]>([]);
  const [result, setResult] = useState<PagedResult<BankTransaction> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [accountId, setAccountId] = useState('');
  const [direction, setDirection] = useState('');
  const [status, setStatus] = useState(
    mode === 'reconcile' ? String(RECONCILIATION_STATUS.Unreconciled) : '',
  );
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [adding, setAdding] = useState(false);
  const [matchTarget, setMatchTarget] = useState<BankTransaction | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  const query = {
    page,
    pageSize: 20,
    bankAccountId: accountId ? Number(accountId) : undefined,
    direction: direction ? Number(direction) : undefined,
    reconciliationStatus: status ? Number(status) : undefined,
    from: from || undefined,
    to: to || undefined,
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await bankApi.transactions(query));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the statement.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, accountId, direction, status, from, to]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    void (async () => {
      try {
        setAccounts(await bankApi.accounts());
      } catch {
        setAccounts([]);
      }
    })();
  }, []);

  const act = async (id: number, action: 'unmatch' | 'reconcile') => {
    setBusyId(id);
    try {
      if (action === 'unmatch') {
        await bankApi.unmatch(id);
        toast.success('Match removed. The payment or expense is unchanged.');
      } else {
        await bankApi.reconcile(id);
        toast.success('Line reconciled.');
      }
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not update the line.');
    } finally {
      setBusyId(null);
    }
  };

  const columns: Column<BankTransaction>[] = [
    {
      key: 'date',
      header: 'Date',
      render: (t) => (
        <div>
          <div>{formatDate(t.transactionDate)}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {t.accountCode}
          </div>
        </div>
      ),
    },
    {
      key: 'narration',
      header: 'Narration',
      render: (t) => (
        <div>
          <div>{t.narration}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {[t.referenceNumber, t.bankReferenceId, t.category].filter(Boolean).join(' · ')}
          </div>
        </div>
      ),
    },
    {
      key: 'amount',
      header: 'Amount',
      render: (t) => (
        <strong
          style={{
            color:
              t.direction === BANK_DIRECTION.Credit ? 'var(--color-success)' : 'var(--color-danger)',
          }}
        >
          {t.direction === BANK_DIRECTION.Credit ? '+' : '−'} {formatInr(t.amount)}
        </strong>
      ),
    },
    {
      key: 'matched',
      header: 'Matched to',
      render: (t) => (
        <div>
          <div>{t.matchedPaymentNumber ?? t.matchedExpenseNumber ?? '—'}</div>
          {t.matchedByName && (
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
              {t.matchedByName} · {t.matchedUtc ? formatDateTime(t.matchedUtc) : ''}
            </div>
          )}
        </div>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (t) => (
        <Badge tone={RECONCILIATION_TONES[t.reconciliationStatus] ?? 'neutral'}>
          {RECONCILIATION_LABELS[t.reconciliationStatus]}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (t) => (
        <div className="row-actions">
          {canEdit && t.reconciliationStatus === RECONCILIATION_STATUS.Unreconciled && (
            <Button size="sm" variant="outline" onClick={() => setMatchTarget(t)}>
              Match
            </Button>
          )}
          {canReconcile && t.reconciliationStatus === RECONCILIATION_STATUS.Matched && (
            <Button size="sm" loading={busyId === t.bankTransactionId}
              onClick={() => void act(t.bankTransactionId, 'reconcile')}>
              Reconcile
            </Button>
          )}
          {canEdit && t.reconciliationStatus !== RECONCILIATION_STATUS.Unreconciled && (
            <Button size="sm" variant="ghost" loading={busyId === t.bankTransactionId}
              onClick={() => void act(t.bankTransactionId, 'unmatch')}>
              Unmatch
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title={mode === 'reconcile' ? 'Reconciliation' : 'Bank Statement'}
        description={
          mode === 'reconcile'
            ? 'Match each bank line to a payment or expense that already exists. Matching never creates a record, and never changes the one it points at.'
            : 'Actual bank transactions. Credits are money in, debits money out.'
        }
        actions={
          <>
            {canExport && (
              <Button variant="outline" onClick={() => void bankApi.export(query)}>
                Export CSV
              </Button>
            )}
            {canCreate && <Button onClick={() => setAdding(true)}>Add transaction</Button>}
          </>
        }
      />
      <Card>
        <div className="toolbar">
          <Select label="Account" value={accountId} placeholder="All accounts"
            options={accounts.map((a) => ({
              value: a.bankAccountId,
              label: `${a.bankName} — ${a.accountCode}`,
            }))}
            onChange={(e) => { setPage(1); setAccountId(e.target.value); }} />
          <Select label="Type" value={direction} placeholder="Credit and debit"
            options={DIRECTION_OPTIONS}
            onChange={(e) => { setPage(1); setDirection(e.target.value); }} />
          <Select label="Status" value={status} placeholder="Any status"
            options={STATUS_OPTIONS}
            onChange={(e) => { setPage(1); setStatus(e.target.value); }} />
          <Input label="From" type="date" value={from}
            onChange={(e) => { setPage(1); setFrom(e.target.value); }} />
          <Input label="To" type="date" value={to}
            onChange={(e) => { setPage(1); setTo(e.target.value); }} />
        </div>
        <DataTable
          columns={columns}
          result={result}
          loading={loading}
          rowKey={(t) => t.bankTransactionId}
          onPageChange={setPage}
          emptyTitle={mode === 'reconcile' ? 'Nothing left to reconcile' : 'No bank transactions'}
          emptyDescription={
            mode === 'reconcile'
              ? 'Every line in this window has been matched and confirmed.'
              : 'Record the lines from your bank statement here.'
          }
        />
      </Card>

      {adding && (
        <BankTransactionModal
          accounts={accounts}
          onClose={() => setAdding(false)}
          onSaved={() => { setAdding(false); void load(); }}
        />
      )}

      {matchTarget && (
        <MatchModal
          transaction={matchTarget}
          onClose={() => setMatchTarget(null)}
          onMatched={() => { setMatchTarget(null); void load(); }}
        />
      )}
    </>
  );
}

function BankTransactionModal({
  accounts,
  onClose,
  onSaved,
}: {
  accounts: BankAccount[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [locations, setLocations] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    bankAccountId: accounts.length === 1 ? String(accounts[0].bankAccountId) : '',
    transactionDate: new Date().toISOString().slice(0, 10),
    direction: String(BANK_DIRECTION.Credit),
    amount: '',
    narration: '',
    referenceNumber: '',
    bankReferenceId: '',
    category: '',
    locationId: '',
    remarks: '',
  });

  useEffect(() => {
    void (async () => {
      try {
        const page = await locationsApi.list({ page: 1, pageSize: 100 });
        setLocations(page.items.map((l) => ({ value: l.locationId, label: l.locationName })));
        if (page.items.length === 1) {
          setForm((f) => ({ ...f, locationId: String(page.items[0].locationId) }));
        }
      } catch {
        setLocations([]);
      }
    })();
  }, []);

  const set = (name: keyof typeof form, value: string) => {
    clearField(name);
    setForm((f) => ({ ...f, [name]: value }));
  };

  const save = async () => {
    setBusy(true);
    clear();
    const payload: SaveBankTransactionPayload = {
      bankAccountId: Number(form.bankAccountId),
      transactionDate: form.transactionDate || null,
      direction: Number(form.direction),
      amount: Number(form.amount),
      narration: form.narration.trim(),
      referenceNumber: form.referenceNumber || null,
      bankReferenceId: form.bankReferenceId || null,
      category: form.category || null,
      locationId: Number(form.locationId),
      remarks: form.remarks || null,
    };
    try {
      await bankApi.createTransaction(payload);
      toast.success('Bank transaction recorded.');
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

  const canSave =
    form.bankAccountId !== '' &&
    form.locationId !== '' &&
    form.narration.trim() !== '' &&
    Number(form.amount) > 0;

  return (
    <Modal
      open
      title="Add bank transaction"
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
        <p style={{ color: 'var(--text-muted)' }}>
          Recording a line does not create a payment or an expense — you match it to one
          that already exists.
        </p>
        <div className="form-grid">
          <Select label="Account" required value={form.bankAccountId}
            placeholder="Select an account"
            options={accounts.map((a) => ({
              value: a.bankAccountId,
              label: `${a.bankName} — ${a.accountCode}`,
            }))}
            error={fieldErrors.bankAccountId}
            onChange={(e) => set('bankAccountId', e.target.value)} />
          <Input label="Date" type="date" required value={form.transactionDate}
            error={fieldErrors.transactionDate}
            onChange={(e) => set('transactionDate', e.target.value)} />
          <Select label="Type" required value={form.direction} options={DIRECTION_OPTIONS}
            error={fieldErrors.direction} onChange={(e) => set('direction', e.target.value)} />
          <Input label="Amount" type="number" min="0" step="0.01" required value={form.amount}
            error={fieldErrors.amount} onChange={(e) => set('amount', e.target.value)} />
          <Input label="Reference number" value={form.referenceNumber}
            hint="Cheque no., UTR, NEFT reference"
            error={fieldErrors.referenceNumber}
            onChange={(e) => set('referenceNumber', e.target.value)} />
          <Input label="Bank transaction ID" value={form.bankReferenceId}
            hint="Stops the same line being imported twice"
            error={fieldErrors.bankReferenceId}
            onChange={(e) => set('bankReferenceId', e.target.value)} />
          <Input label="Category" value={form.category}
            error={fieldErrors.category} onChange={(e) => set('category', e.target.value)} />
          <Select label="Location" required value={form.locationId}
            placeholder="Select a location" options={locations}
            error={fieldErrors.locationId} onChange={(e) => set('locationId', e.target.value)} />
        </div>
        <Input label="Narration" required value={form.narration}
          error={fieldErrors.narration} onChange={(e) => set('narration', e.target.value)} />
        <Input label="Remarks" value={form.remarks}
          error={fieldErrors.remarks} onChange={(e) => set('remarks', e.target.value)} />
      </div>
    </Modal>
  );
}

function MatchModal({
  transaction,
  onClose,
  onMatched,
}: {
  transaction: BankTransaction;
  onClose: () => void;
  onMatched: () => void;
}) {
  const toast = useToast();
  const [candidates, setCandidates] = useState<MatchCandidate[] | null>(null);
  const [busyId, setBusyId] = useState<number | null>(null);

  useEffect(() => {
    void (async () => {
      try {
        setCandidates(await bankApi.candidates(transaction.bankTransactionId));
      } catch (error) {
        toast.error(
          error instanceof ApiClientError ? error.message : 'Could not load candidates.',
        );
        setCandidates([]);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [transaction.bankTransactionId]);

  const match = async (candidate: MatchCandidate) => {
    setBusyId(candidate.id);
    try {
      await bankApi.match(transaction.bankTransactionId,
        candidate.kind === 'Payment' ? { paymentId: candidate.id } : { expenseId: candidate.id });
      toast.success(`Matched to ${candidate.number}.`);
      onMatched();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not match the line.');
    } finally {
      setBusyId(null);
    }
  };

  const isCredit = transaction.direction === BANK_DIRECTION.Credit;

  return (
    <Modal
      open
      title={`Match ${formatInr(transaction.amount)} ${isCredit ? 'credit' : 'debit'}`}
      onClose={onClose}
      footer={
        <Button variant="ghost" onClick={onClose}>
          Close
        </Button>
      }
    >
      <div className="form-stack">
        <p style={{ color: 'var(--text-muted)' }}>
          {transaction.narration} · {formatDate(transaction.transactionDate)}
        </p>
        <p>
          {isCredit
            ? 'A credit is money in, so it matches a customer payment.'
            : 'A debit is money out, so it matches a company expense.'}{' '}
          Only records already in the system are listed — nothing is created here.
        </p>

        {candidates === null ? (
          <Spinner center />
        ) : candidates.length === 0 ? (
          <p>
            No {isCredit ? 'payments' : 'expenses'} found within a week of this date. Record the{' '}
            {isCredit ? 'payment' : 'expense'} first, then come back to match it.
          </p>
        ) : (
          <div className="form-stack">
            {candidates.map((candidate) => (
              <Card key={`${candidate.kind}-${candidate.id}`}>
                <div className="toolbar">
                  <div>
                    <div style={{ fontWeight: 600 }}>
                      {candidate.number}{' '}
                      {candidate.amountMatches && <Badge tone="success">Exact amount</Badge>}
                      {candidate.alreadyMatched && <Badge tone="neutral">Already matched</Badge>}
                    </div>
                    <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                      {candidate.description} · {formatDate(candidate.date)}
                    </div>
                  </div>
                  <div>
                    <strong>{formatInr(candidate.amount)}</strong>
                  </div>
                  <Button
                    size="sm"
                    loading={busyId === candidate.id}
                    disabled={candidate.alreadyMatched}
                    onClick={() => void match(candidate)}
                  >
                    Match
                  </Button>
                </div>
              </Card>
            ))}
          </div>
        )}
      </div>
    </Modal>
  );
}
