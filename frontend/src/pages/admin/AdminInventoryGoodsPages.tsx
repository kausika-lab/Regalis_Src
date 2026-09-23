import { useCallback, useEffect, useState } from 'react';
import { inventoryApi } from '@/api/operations';
import { suppliersApi } from '@/api/procurement';
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
  type SelectOption,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useFieldErrors } from '@/hooks/useFieldErrors';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type {
  CreateInventoryTransactionPayload,
  InventoryItem,
  InventoryStockRow,
  InventoryTransaction,
} from '@/models/operations';
import { formatDate, formatInr } from '@/utils/formatters';

/**
 * Incoming and Outgoing are two views over the one append-only ledger — stock is
 * derived from it, so there is no separate goods-in/goods-out table to drift
 * (Invariant 11). The only difference between the two screens is the transaction
 * type they filter on and record.
 */
const TYPE_IN = 1;
const TYPE_OUT = 2;

export function AdminIncomingGoodsPage() {
  return (
    <GoodsMovementPage
      transactionType={TYPE_IN}
      title="Incoming Goods"
      description="Goods arriving into stock. Receiving against a purchase order records the supplier and PO automatically."
      addLabel="Record incoming"
      counterpartyLabel="Supplier / source"
      emptyTitle="No incoming goods yet"
    />
  );
}

export function AdminOutgoingGoodsPage() {
  return (
    <GoodsMovementPage
      transactionType={TYPE_OUT}
      title="Outgoing Goods"
      description="Goods leaving stock. The ledger refuses a movement that would take stock below zero."
      addLabel="Record outgoing"
      counterpartyLabel="Issued to"
      emptyTitle="No outgoing goods yet"
    />
  );
}

function GoodsMovementPage({
  transactionType,
  title,
  description,
  addLabel,
  counterpartyLabel,
  emptyTitle,
}: {
  transactionType: number;
  title: string;
  description: string;
  addLabel: string;
  counterpartyLabel: string;
  emptyTitle: string;
}) {
  const toast = useToast();
  const canCreate = usePermission('INVENTORY.CREATE');
  const canExport = usePermission('INVENTORY.EXPORT');

  const [result, setResult] = useState<PagedResult<InventoryTransaction> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [adding, setAdding] = useState(false);

  const query = {
    page,
    pageSize: 20,
    transactionType,
    from: from || undefined,
    to: to || undefined,
  };

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await inventoryApi.transactions(query));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the ledger.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, transactionType, from, to]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<InventoryTransaction>[] = [
    {
      key: 'ref',
      header: 'Reference',
      render: (t) => (
        <div>
          <div style={{ fontWeight: 600 }}>{t.transactionNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {formatDate(t.transactionDate)}
          </div>
        </div>
      ),
    },
    {
      key: 'item',
      header: 'Item',
      render: (t) => (
        <div>
          <div>{t.itemName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {t.itemCode}
          </div>
        </div>
      ),
    },
    {
      key: 'qty',
      header: 'Quantity',
      render: (t) => (
        <strong>
          {t.quantity} {t.unit}
        </strong>
      ),
    },
    {
      key: 'party',
      header: counterpartyLabel,
      render: (t) => t.supplierName ?? t.sourceOrDestination ?? '—',
    },
    ...(transactionType === TYPE_IN
      ? [
          {
            key: 'po',
            header: 'PO / Invoice',
            render: (t: InventoryTransaction) => (
              <div>
                <div>{t.poNumber ?? '—'}</div>
                <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                  {t.purchaseInvoiceNumber ?? ''}
                </div>
              </div>
            ),
          } satisfies Column<InventoryTransaction>,
          {
            key: 'cost',
            header: 'Rate',
            render: (t: InventoryTransaction) => (t.unitCost !== null ? formatInr(t.unitCost) : '—'),
          } satisfies Column<InventoryTransaction>,
        ]
      : [
          {
            key: 'purpose',
            header: 'Purpose',
            render: (t: InventoryTransaction) => t.notes ?? '—',
          } satisfies Column<InventoryTransaction>,
        ]),
    { key: 'by', header: 'Entered by', render: (t) => t.enteredByName },
  ];

  return (
    <>
      <PageHeader
        title={title}
        description={description}
        actions={
          <>
            {canExport && (
              <Button variant="outline" onClick={() => void inventoryApi.export(query)}>
                Export CSV
              </Button>
            )}
            {canCreate && <Button onClick={() => setAdding(true)}>{addLabel}</Button>}
          </>
        }
      />
      <Card>
        <div className="toolbar">
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
          rowKey={(t) => t.inventoryTransactionId}
          onPageChange={setPage}
          emptyTitle={emptyTitle}
          emptyDescription="The ledger is append-only — a mistake is corrected with an Adjustment, never an edit."
        />
      </Card>

      {adding && (
        <GoodsMovementModal
          transactionType={transactionType}
          counterpartyLabel={counterpartyLabel}
          onClose={() => setAdding(false)}
          onSaved={() => {
            setAdding(false);
            void load();
          }}
        />
      )}
    </>
  );
}

function GoodsMovementModal({
  transactionType,
  counterpartyLabel,
  onClose,
  onSaved,
}: {
  transactionType: number;
  counterpartyLabel: string;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const isIncoming = transactionType === TYPE_IN;
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [suppliers, setSuppliers] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    inventoryItemId: '',
    quantity: '',
    transactionDate: new Date().toISOString().slice(0, 10),
    sourceOrDestination: '',
    supplierId: '',
    referenceNumber: '',
    purchaseInvoiceNumber: '',
    unitCost: '',
    notes: '',
  });

  useEffect(() => {
    void (async () => {
      try {
        const itemPage = await inventoryApi.items({ page: 1, pageSize: 200 });
        setItems(itemPage.items);
        if (isIncoming) {
          const supplierPage = await suppliersApi.list({ page: 1, pageSize: 100 });
          setSuppliers(
            supplierPage.items.map((s) => ({ value: s.supplierId, label: s.supplierName })),
          );
        }
      } catch {
        setItems([]);
        setSuppliers([]);
      }
    })();
  }, [isIncoming]);

  const selected = items.find((i) => String(i.inventoryItemId) === form.inventoryItemId);

  const set = (name: keyof typeof form, value: string) => {
    clearField(name);
    setForm((f) => ({ ...f, [name]: value }));
  };

  const save = async () => {
    setBusy(true);
    clear();
    const payload: CreateInventoryTransactionPayload = {
      inventoryItemId: Number(form.inventoryItemId),
      transactionType,
      quantity: Number(form.quantity),
      transactionDate: form.transactionDate || null,
      sourceOrDestination: form.sourceOrDestination || null,
      referenceNumber: form.referenceNumber || null,
      unitCost: form.unitCost ? Number(form.unitCost) : null,
      notes: form.notes || null,
      supplierId: isIncoming && form.supplierId ? Number(form.supplierId) : null,
      purchaseInvoiceNumber: isIncoming ? form.purchaseInvoiceNumber || null : null,
    };
    try {
      await inventoryApi.createTransaction(payload);
      toast.success('Movement recorded and stock updated.');
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

  const canSave = form.inventoryItemId !== '' && Number(form.quantity) > 0;

  return (
    <Modal
      open
      title={isIncoming ? 'Record incoming goods' : 'Record outgoing goods'}
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
        {!isIncoming && selected && (
          <p style={{ color: 'var(--text-muted)' }}>
            In stock now: <strong>{selected.currentStock} {selected.unit}</strong>
          </p>
        )}
        <div className="form-grid">
          <Select
            label="Item"
            required
            value={form.inventoryItemId}
            placeholder="Select an item"
            options={items.map((i) => ({
              value: i.inventoryItemId,
              label: `${i.itemCode} — ${i.itemName}`,
            }))}
            error={fieldErrors.inventoryItemId}
            onChange={(e) => set('inventoryItemId', e.target.value)}
          />
          <Input
            label="Quantity"
            type="number"
            min="0"
            step="0.001"
            required
            value={form.quantity}
            hint={selected ? selected.unit : undefined}
            error={fieldErrors.quantity}
            onChange={(e) => set('quantity', e.target.value)}
          />
          <Input
            label="Date"
            type="date"
            required
            value={form.transactionDate}
            error={fieldErrors.transactionDate}
            onChange={(e) => set('transactionDate', e.target.value)}
          />
          {isIncoming ? (
            <Select
              label="Supplier"
              value={form.supplierId}
              placeholder="Not from a registered supplier"
              options={suppliers}
              error={fieldErrors.supplierId}
              onChange={(e) => set('supplierId', e.target.value)}
            />
          ) : (
            <Input
              label={counterpartyLabel}
              value={form.sourceOrDestination}
              error={fieldErrors.sourceOrDestination}
              onChange={(e) => set('sourceOrDestination', e.target.value)}
            />
          )}
          <Input
            label="Reference"
            value={form.referenceNumber}
            error={fieldErrors.referenceNumber}
            onChange={(e) => set('referenceNumber', e.target.value)}
          />
          {isIncoming && (
            <>
              <Input
                label="Supplier invoice no."
                value={form.purchaseInvoiceNumber}
                error={fieldErrors.purchaseInvoiceNumber}
                onChange={(e) => set('purchaseInvoiceNumber', e.target.value)}
              />
              <Input
                label="Rate"
                type="number"
                min="0"
                step="0.01"
                value={form.unitCost}
                error={fieldErrors.unitCost}
                onChange={(e) => set('unitCost', e.target.value)}
              />
            </>
          )}
        </div>
        <Input
          label={isIncoming ? 'Remarks' : 'Purpose / remarks'}
          value={form.notes}
          error={fieldErrors.notes}
          onChange={(e) => set('notes', e.target.value)}
        />
      </div>
    </Modal>
  );
}

// ── Current stock ───────────────────────────────────────────────────────────

export function AdminCurrentStockPage() {
  const toast = useToast();
  const [rows, setRows] = useState<InventoryStockRow[] | null>(null);
  const [loading, setLoading] = useState(true);
  const [belowReorder, setBelowReorder] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await inventoryApi.stock(undefined, belowReorder || undefined));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load stock.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [belowReorder]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<InventoryStockRow>[] = [
    {
      key: 'item',
      header: 'Item',
      render: (s) => (
        <div>
          <div style={{ fontWeight: 600 }}>{s.itemName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {s.itemCode}
            {s.category ? ` · ${s.category}` : ''}
          </div>
        </div>
      ),
    },
    { key: 'opening', header: 'Opening', render: (s) => s.openingQuantity },
    { key: 'in', header: 'In', render: (s) => s.totalIn },
    { key: 'out', header: 'Out', render: (s) => s.totalOut },
    { key: 'damage', header: 'Damage', render: (s) => s.totalDamage },
    { key: 'adjust', header: 'Adjustment', render: (s) => s.totalAdjustment },
    {
      key: 'current',
      header: 'Current stock',
      render: (s) => (
        <strong>
          {s.currentStock} {s.unit}
        </strong>
      ),
    },
    {
      key: 'reorder',
      header: 'Reorder',
      render: (s) =>
        s.needsReorder ? (
          <Badge tone="warning">At / below {s.reorderLevel}</Badge>
        ) : (
          <span style={{ color: 'var(--text-muted)' }}>{s.reorderLevel}</span>
        ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Current Stock"
        description="Opening + in + adjustments − out − damage. Derived from the ledger on every read, never stored."
        actions={
          <Button variant={belowReorder ? 'primary' : 'outline'} onClick={() => setBelowReorder((v) => !v)}>
            {belowReorder ? 'Showing low stock' : 'Show low stock only'}
          </Button>
        }
      />
      <Card>
        <DataTable
          columns={columns}
          result={
            rows
              ? {
                  items: rows,
                  page: 1,
                  pageSize: rows.length || 1,
                  totalCount: rows.length,
                  totalPages: 1,
                  hasNext: false,
                  hasPrevious: false,
                }
              : null
          }
          loading={loading}
          rowKey={(s) => s.inventoryItemId}
          onPageChange={() => undefined}
          emptyTitle={belowReorder ? 'Nothing is below its reorder level' : 'No stock items'}
        />
      </Card>
    </>
  );
}
