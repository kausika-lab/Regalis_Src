import { useCallback, useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { inventoryApi } from '@/api/operations';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
  DataTable,
  Input,
  Modal,
  PageHeader,
  Select,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import {
  INVENTORY_TXN_LABELS,
  type CreateInventoryTransactionPayload,
  type InventoryItem,
  type InventoryTransaction,
  type SaveInventoryItemPayload,
} from '@/models/operations';
import { formatDate } from '@/utils/formatters';

// ── /admin/inventory/items — item master with derived stock ─────────────────

export function AdminInventoryItemsPage() {
  const toast = useToast();
  const canCreate = usePermission('INVENTORY.CREATE');
  const canEdit = usePermission('INVENTORY.EDIT');
  const canDelete = usePermission('INVENTORY.DELETE');

  const [rows, setRows] = useState<PagedResult<InventoryItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [belowReorder, setBelowReorder] = useState(false);
  const [editing, setEditing] = useState<InventoryItem | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<InventoryItem | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await inventoryApi.items({
        page, pageSize: 20,
        belowReorder: belowReorder || undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load items.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, belowReorder]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<InventoryItem>[] = [
    {
      key: 'item', header: 'Item',
      render: (i) => (
        <div>
          <div style={{ fontWeight: 600 }}>{i.itemName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {i.itemCode}{i.category ? ` · ${i.category}` : ''}
          </div>
        </div>
      ),
    },
    {
      key: 'stock', header: 'Stock',
      render: (i) => (
        <div style={{ display: 'flex', gap: 'var(--space-1)', alignItems: 'center' }}>
          <span style={{ fontWeight: 600 }}>{i.currentStock} {i.unit}</span>
          {i.needsReorder && <Badge tone="danger">Reorder</Badge>}
        </div>
      ),
    },
    { key: 'reorder', header: 'Reorder level', render: (i) => `${i.reorderLevel} ${i.unit}` },
    { key: 'cost', header: 'Unit cost', render: (i) => i.unitCost !== null ? `₹${i.unitCost}` : '—' },
    {
      key: 'actions', header: '',
      render: (i) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(i)}>Edit</Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(i)}>Delete</Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Inventory Items"
        description="Stock is derived from the transaction ledger — it is never edited directly."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New item</Button>} />
      <Card>
        <div className="toolbar">
          <Checkbox label="Only items at or below reorder level" checked={belowReorder}
            onChange={(e) => { setBelowReorder(e.target.checked); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(i) => i.inventoryItemId} onPageChange={setPage} emptyTitle="No inventory items" />
      </Card>

      {editing && (
        <ItemEditorModal item={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete ${deleteTarget?.itemName ?? ''}?`}
        message="An item with ledger entries cannot be deleted — deactivate it instead."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await inventoryApi.removeItem(deleteTarget.inventoryItemId);
            toast.success('Item deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

function ItemEditorModal({ item, onClose, onSaved }: {
  item: InventoryItem | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    itemCode: item?.itemCode ?? '',
    itemName: item?.itemName ?? '',
    category: item?.category ?? '',
    unit: item?.unit ?? 'Nos',
    openingQuantity: item ? String(item.openingQuantity) : '0',
    reorderLevel: item ? String(item.reorderLevel) : '0',
    unitCost: item?.unitCost !== null && item !== null ? String(item.unitCost) : '',
    description: item?.description ?? '',
  });
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    const payload: SaveInventoryItemPayload = {
      itemCode: form.itemCode.trim().toUpperCase(),
      itemName: form.itemName.trim(),
      category: form.category.trim() || null,
      unit: form.unit.trim(),
      openingQuantity: Number(form.openingQuantity) || 0,
      reorderLevel: Number(form.reorderLevel) || 0,
      unitCost: form.unitCost ? Number(form.unitCost) : null,
      locationId: item?.locationId ?? 1,
      description: form.description.trim() || null,
    };
    try {
      if (item) {
        await inventoryApi.updateItem(item.inventoryItemId, payload);
      } else {
        await inventoryApi.createItem(payload);
      }
      toast.success(item ? 'Item updated.' : 'Item created.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the item.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={item ? `Edit — ${item.itemName}` : 'New item'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.itemCode.trim() || !form.itemName.trim() || !form.unit.trim()}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Code" required value={form.itemCode} hint="e.g. COF-BEANS"
            disabled={item !== null}
            onChange={(e) => setForm({ ...form, itemCode: e.target.value })} />
          <Input label="Name" required value={form.itemName}
            onChange={(e) => setForm({ ...form, itemName: e.target.value })} />
          <Input label="Category" value={form.category} placeholder="e.g. Pantry"
            onChange={(e) => setForm({ ...form, category: e.target.value })} />
          <Input label="Unit" required value={form.unit} hint="Nos, Kg, Ltr, Box"
            onChange={(e) => setForm({ ...form, unit: e.target.value })} />
          <Input label="Opening quantity" type="number" value={form.openingQuantity}
            disabled={item !== null}
            hint={item ? 'Frozen — correct stock with an Adjustment' : undefined}
            onChange={(e) => setForm({ ...form, openingQuantity: e.target.value })} />
          <Input label="Reorder level" type="number" value={form.reorderLevel}
            onChange={(e) => setForm({ ...form, reorderLevel: e.target.value })} />
          <Input label="Unit cost (₹)" type="number" value={form.unitCost}
            onChange={(e) => setForm({ ...form, unitCost: e.target.value })} />
        </div>
        <Input label="Description" value={form.description}
          onChange={(e) => setForm({ ...form, description: e.target.value })} />
      </div>
    </Modal>
  );
}

// ── /admin/inventory/transactions — the append-only ledger ──────────────────

export function AdminInventoryTransactionsPage() {
  const toast = useToast();
  const canCreate = usePermission('INVENTORY.CREATE');
  const canExport = usePermission('INVENTORY.EXPORT');

  const [rows, setRows] = useState<PagedResult<InventoryTransaction> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [typeFilter, setTypeFilter] = useState('');
  const [creating, setCreating] = useState(false);
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await inventoryApi.transactions({
        page, pageSize: 20,
        transactionType: typeFilter ? Number(typeFilter) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the ledger.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, typeFilter]);

  useEffect(() => { void load(); }, [load]);

  const exportCsv = async () => {
    setExporting(true);
    try {
      await inventoryApi.export({ transactionType: typeFilter ? Number(typeFilter) : undefined });
      toast.success('Inventory CSV downloaded.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Export failed.');
    } finally {
      setExporting(false);
    }
  };

  const columns: Column<InventoryTransaction>[] = [
    { key: 'number', header: 'Txn', render: (t) => t.transactionNumber },
    { key: 'date', header: 'Date', render: (t) => formatDate(t.transactionDate) },
    {
      key: 'item', header: 'Item',
      render: (t) => (
        <div>
          <div style={{ fontWeight: 600 }}>{t.itemName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{t.itemCode}</div>
        </div>
      ),
    },
    {
      key: 'type', header: 'Type',
      render: (t) => (
        <Badge tone={t.transactionType === 1 || t.transactionType === 4 ? 'success'
          : t.transactionType === 3 ? 'danger'
          : t.transactionType === 5 ? 'warning' : 'info'}>
          {INVENTORY_TXN_LABELS[t.transactionType]}
        </Badge>
      ),
    },
    { key: 'qty', header: 'Qty', render: (t) => `${t.quantity} ${t.unit}` },
    {
      key: 'source', header: 'Source / destination',
      render: (t) => t.sourceOrDestination ?? t.damageDescription ?? '—',
    },
    { key: 'by', header: 'Entered by', render: (t) => t.enteredByName },
  ];

  return (
    <>
      <PageHeader title="Inventory Transactions"
        description="Append-only: a mistake is corrected with a compensating Adjustment row, never an edit."
        actions={
          <div className="row-actions">
            {canExport && (
              <Button variant="outline" onClick={exportCsv} loading={exporting}>
                <Download size={16} /> Export CSV
              </Button>
            )}
            {canCreate && <Button onClick={() => setCreating(true)}>New transaction</Button>}
          </div>
        } />
      <Card>
        <div className="toolbar">
          <Select label="Type" value={typeFilter} placeholder="All types"
            options={Object.entries(INVENTORY_TXN_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(t) => t.inventoryTransactionId} onPageChange={setPage}
          emptyTitle="No transactions yet" />
      </Card>

      {creating && (
        <TransactionModal onClose={() => setCreating(false)}
          onDone={() => { setCreating(false); void load(); }} />
      )}
    </>
  );
}

function TransactionModal({ onClose, onDone }: {
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [items, setItems] = useState<InventoryItem[]>([]);
  const [form, setForm] = useState({
    inventoryItemId: '',
    transactionType: '1',
    quantity: '',
    sourceOrDestination: '',
    referenceNumber: '',
    unitCost: '',
    damageDescription: '',
    damageCause: '',
    notes: '',
  });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    inventoryApi.items({ page: 1, pageSize: 100 })
      .then((result) => setItems(result.items)).catch(() => {});
  }, []);

  const type = Number(form.transactionType);
  const selected = items.find((i) => String(i.inventoryItemId) === form.inventoryItemId);

  const submit = async () => {
    setBusy(true);
    const payload: CreateInventoryTransactionPayload = {
      inventoryItemId: Number(form.inventoryItemId),
      transactionType: type,
      quantity: Number(form.quantity),
      sourceOrDestination: form.sourceOrDestination.trim() || null,
      referenceNumber: form.referenceNumber.trim() || null,
      unitCost: form.unitCost ? Number(form.unitCost) : null,
      damageDescription: form.damageDescription.trim() || null,
      damageCause: form.damageCause.trim() || null,
      notes: form.notes.trim() || null,
    };
    try {
      await inventoryApi.createTransaction(payload);
      toast.success('Transaction recorded.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not record it.');
    } finally {
      setBusy(false);
    }
  };

  const quantityOk = type === 5 ? Number(form.quantity) !== 0 : Number(form.quantity) > 0;

  return (
    <Modal open title="New inventory transaction" onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={submit} loading={busy}
            disabled={!form.inventoryItemId || !form.quantity || !quantityOk ||
              (type === 3 && !form.damageDescription.trim())}>
            Record
          </Button>
        </>
      }>
      <div className="form-stack">
        <Select label="Item" required value={form.inventoryItemId} placeholder="Select item"
          options={items.map((i) => ({
            value: i.inventoryItemId,
            label: `${i.itemName} (${i.itemCode}) — ${i.currentStock} ${i.unit} in stock`,
          }))}
          onChange={(e) => setForm({ ...form, inventoryItemId: e.target.value })} />
        <div className="form-grid">
          <Select label="Type" value={form.transactionType}
            options={[
              { value: 1, label: 'In (purchase / receipt)' },
              { value: 2, label: 'Out (issue / use)' },
              { value: 3, label: 'Damage' },
              { value: 5, label: 'Adjustment (± correction)' },
            ]}
            onChange={(e) => setForm({ ...form, transactionType: e.target.value })} />
          <Input label="Quantity" type="number" required value={form.quantity}
            hint={type === 5
              ? 'Signed: negative reduces stock'
              : selected ? `${selected.currentStock} ${selected.unit} in stock` : undefined}
            onChange={(e) => setForm({ ...form, quantity: e.target.value })} />
          <Input label={type === 1 ? 'Supplier' : 'Department / use'} value={form.sourceOrDestination}
            onChange={(e) => setForm({ ...form, sourceOrDestination: e.target.value })} />
          <Input label="Reference no." value={form.referenceNumber}
            onChange={(e) => setForm({ ...form, referenceNumber: e.target.value })} />
          {type === 1 && (
            <Input label="Unit cost (₹)" type="number" value={form.unitCost}
              onChange={(e) => setForm({ ...form, unitCost: e.target.value })} />
          )}
        </div>
        {type === 3 && (
          <div className="form-grid">
            <Input label="Damage description" required value={form.damageDescription}
              onChange={(e) => setForm({ ...form, damageDescription: e.target.value })} />
            <Input label="Cause" value={form.damageCause}
              onChange={(e) => setForm({ ...form, damageCause: e.target.value })} />
          </div>
        )}
        <Input label="Notes" value={form.notes}
          onChange={(e) => setForm({ ...form, notes: e.target.value })} />
      </div>
    </Modal>
  );
}
