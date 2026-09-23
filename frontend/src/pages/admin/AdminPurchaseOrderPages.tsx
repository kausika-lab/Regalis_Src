import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { purchaseOrdersApi, suppliersApi } from '@/api/procurement';
import { inventoryApi } from '@/api/operations';
import { locationsApi } from '@/api/masters';
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
  Spinner,
  type Column,
  type SelectOption,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useFieldErrors } from '@/hooks/useFieldErrors';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import {
  PO_STATUS_LABELS,
  PO_STATUS_TONES,
  type PurchaseOrder,
  type PurchaseOrderLine,
  type ReceiveGoodsPayload,
  type SavePurchaseOrderPayload,
} from '@/models/procurement';
import { formatDate, formatInr } from '@/utils/formatters';

const STATUS_OPTIONS: SelectOption[] = Object.entries(PO_STATUS_LABELS).map(([value, label]) => ({
  value: Number(value),
  label,
}));

// ── List ────────────────────────────────────────────────────────────────────

export function AdminPurchaseOrdersPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const canCreate = usePermission('PURCHASE.CREATE');

  const [result, setResult] = useState<PagedResult<PurchaseOrder> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await purchaseOrdersApi.list({
        page,
        pageSize: 20,
        status: status ? Number(status) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load purchase orders.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, status]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<PurchaseOrder>[] = [
    {
      key: 'po',
      header: 'PO',
      render: (p) => (
        <div>
          <div style={{ fontWeight: 600 }}>{p.poNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {formatDate(p.poDate)}
          </div>
        </div>
      ),
    },
    { key: 'supplier', header: 'Supplier', render: (p) => p.supplierName },
    { key: 'location', header: 'Location', render: (p) => p.locationName ?? '—' },
    {
      key: 'delivery',
      header: 'Expected',
      render: (p) => (p.expectedDeliveryDate ? formatDate(p.expectedDeliveryDate) : '—'),
    },
    { key: 'lines', header: 'Lines', render: (p) => p.lines.length },
    { key: 'total', header: 'Total', render: (p) => <strong>{formatInr(p.totalAmount)}</strong> },
    {
      key: 'status',
      header: 'Status',
      render: (p) => (
        <Badge tone={PO_STATUS_TONES[p.status] ?? 'neutral'}>
          {PO_STATUS_LABELS[p.status] ?? 'Unknown'}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (p) => (
        <div className="row-actions">
          <Button
            size="sm"
            variant="outline"
            onClick={() => navigate(`/admin/purchase-orders/${p.purchaseOrderId}`)}
          >
            Open
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Purchase Orders"
        description="Anything the company buys. Receiving goods against a PO updates inventory in the same step."
        actions={canCreate && <Button onClick={() => setCreating(true)}>New purchase order</Button>}
      />
      <Card>
        <div className="toolbar">
          <Select
            label="Status"
            value={status}
            placeholder="All statuses"
            options={STATUS_OPTIONS}
            onChange={(e) => {
              setPage(1);
              setStatus(e.target.value);
            }}
          />
        </div>
        <DataTable
          columns={columns}
          result={result}
          loading={loading}
          rowKey={(p) => p.purchaseOrderId}
          onPageChange={setPage}
          emptyTitle="No purchase orders"
          emptyDescription="Raise a PO whenever the company buys something."
        />
      </Card>

      {creating && (
        <PurchaseOrderEditorModal
          onClose={() => setCreating(false)}
          onSaved={(created) => {
            setCreating(false);
            navigate(`/admin/purchase-orders/${created.purchaseOrderId}`);
          }}
        />
      )}
    </>
  );
}

// ── Detail ──────────────────────────────────────────────────────────────────

export function AdminPurchaseOrderDetailPage() {
  const { id } = useParams<{ id: string }>();
  const toast = useToast();
  const navigate = useNavigate();
  const canEdit = usePermission('PURCHASE.EDIT');
  const canApprove = usePermission('PURCHASE.APPROVE');
  const canCancel = usePermission('PURCHASE.DELETE');

  const [order, setOrder] = useState<PurchaseOrder | null>(null);
  const [loading, setLoading] = useState(true);
  const [receiving, setReceiving] = useState(false);
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setOrder(await purchaseOrdersApi.get(Number(id)));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the purchase order.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  if (loading) {
    return <Spinner center />;
  }
  if (!order) {
    return null;
  }

  const isDraft = order.status === 1;
  const canReceive = order.status === 2 || order.status === 3;

  const send = async () => {
    setBusy(true);
    try {
      setOrder(await purchaseOrdersApi.send(order.purchaseOrderId));
      toast.success('Purchase order sent.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not send the PO.');
    } finally {
      setBusy(false);
    }
  };

  const lineColumns: Column<PurchaseOrderLine>[] = [
    { key: 'no', header: '#', render: (l) => l.lineNumber, width: '48px' },
    {
      key: 'item',
      header: 'Item',
      render: (l) => (
        <div>
          <div style={{ fontWeight: 600 }}>{l.itemName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {l.itemCode ? `${l.itemCode} · stocked` : 'Not stocked'}
            {l.hsnSacCode ? ` · HSN ${l.hsnSacCode}` : ''}
          </div>
        </div>
      ),
    },
    { key: 'qty', header: 'Ordered', render: (l) => `${l.quantity} ${l.unit ?? ''}`.trim() },
    {
      key: 'received',
      header: 'Received',
      render: (l) =>
        l.receivedQuantity > 0 ? (
          <Badge tone={l.pendingQuantity > 0 ? 'gold' : 'success'}>
            {l.receivedQuantity} of {l.quantity}
          </Badge>
        ) : (
          <span style={{ color: 'var(--text-muted)' }}>—</span>
        ),
    },
    { key: 'rate', header: 'Rate', render: (l) => formatInr(l.rate) },
    { key: 'discount', header: 'Discount', render: (l) => formatInr(l.discountAmount) },
    { key: 'tax', header: 'Tax', render: (l) => `${l.taxPercent}% · ${formatInr(l.taxAmount)}` },
    { key: 'total', header: 'Total', render: (l) => <strong>{formatInr(l.totalAmount)}</strong> },
  ];

  return (
    <>
      <PageHeader
        title={order.poNumber}
        description={`${order.supplierName} · ${formatDate(order.poDate)}`}
        actions={
          <>
            <Button variant="ghost" onClick={() => navigate('/admin/purchase-orders')}>
              Back
            </Button>
            {isDraft && canEdit && (
              <Button variant="outline" onClick={() => setEditing(true)}>
                Edit
              </Button>
            )}
            {isDraft && canApprove && (
              <Button onClick={send} loading={busy}>
                Send to supplier
              </Button>
            )}
            {canReceive && canEdit && <Button onClick={() => setReceiving(true)}>Receive goods</Button>}
            {order.status !== 5 && order.status !== 4 && canCancel && (
              <Button variant="ghost" onClick={() => setCancelling(true)}>
                Cancel PO
              </Button>
            )}
          </>
        }
      />

      <Card>
        <div className="toolbar">
          <Detail label="Status">
            <Badge tone={PO_STATUS_TONES[order.status] ?? 'neutral'}>
              {PO_STATUS_LABELS[order.status] ?? 'Unknown'}
            </Badge>
          </Detail>
          <Detail label="Supplier GSTIN">{order.supplierGstin ?? '—'}</Detail>
          <Detail label="Location">{order.locationName ?? '—'}</Detail>
          <Detail label="Expected delivery">
            {order.expectedDeliveryDate ? formatDate(order.expectedDeliveryDate) : '—'}
          </Detail>
          <Detail label="Sent">{order.sentUtc ? formatDate(order.sentUtc) : '—'}</Detail>
        </div>
        {order.cancellationReason && (
          <div className="form-error" role="alert">
            Cancelled: {order.cancellationReason}
          </div>
        )}
        {order.remarks && (
          <p style={{ color: 'var(--text-muted)' }}>{order.remarks}</p>
        )}
      </Card>

      <Card>
        <DataTable
          columns={lineColumns}
          result={{
            items: order.lines,
            page: 1,
            pageSize: order.lines.length || 1,
            totalCount: order.lines.length,
            totalPages: 1,
            hasNext: false,
            hasPrevious: false,
          }}
          loading={false}
          rowKey={(l) => l.purchaseOrderLineId}
          onPageChange={() => undefined}
        />
        <div className="toolbar">
          <Detail label="Sub total">{formatInr(order.subTotal)}</Detail>
          <Detail label="Discount">{formatInr(order.discountAmount)}</Detail>
          <Detail label="Taxable">{formatInr(order.taxableAmount)}</Detail>
          <Detail label="Tax">{formatInr(order.taxAmount)}</Detail>
          <Detail label="Total">
            <strong style={{ fontSize: 'var(--font-size-lg)' }}>{formatInr(order.totalAmount)}</strong>
          </Detail>
        </div>
      </Card>

      {receiving && (
        <ReceiveGoodsModal
          order={order}
          onClose={() => setReceiving(false)}
          onReceived={(updated) => {
            setReceiving(false);
            setOrder(updated);
          }}
        />
      )}

      {editing && (
        <PurchaseOrderEditorModal
          order={order}
          onClose={() => setEditing(false)}
          onSaved={(updated) => {
            setEditing(false);
            setOrder(updated);
          }}
        />
      )}

      <ConfirmCancelDialog
        open={cancelling}
        order={order}
        onClose={() => setCancelling(false)}
        onCancelled={(updated) => {
          setCancelling(false);
          setOrder(updated);
        }}
      />
    </>
  );
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{label}</div>
      <div>{children}</div>
    </div>
  );
}

// ── Receive goods ───────────────────────────────────────────────────────────

function ReceiveGoodsModal({
  order,
  onClose,
  onReceived,
}: {
  order: PurchaseOrder;
  onClose: () => void;
  onReceived: (updated: PurchaseOrder) => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [busy, setBusy] = useState(false);
  const pending = order.lines.filter((l) => l.pendingQuantity > 0);
  const [receivedDate, setReceivedDate] = useState(new Date().toISOString().slice(0, 10));
  const [purchaseInvoiceNumber, setPurchaseInvoiceNumber] = useState('');
  const [notes, setNotes] = useState('');
  // Pre-filled with the full pending quantity: receiving everything is the common case.
  const [quantities, setQuantities] = useState<Record<number, string>>(
    Object.fromEntries(pending.map((l) => [l.purchaseOrderLineId, String(l.pendingQuantity)])),
  );

  const submit = async () => {
    setBusy(true);
    clear();
    const lines = pending
      .map((l) => ({
        purchaseOrderLineId: l.purchaseOrderLineId,
        receivedQuantity: Number(quantities[l.purchaseOrderLineId] ?? 0),
      }))
      .filter((l) => l.receivedQuantity > 0);

    const payload: ReceiveGoodsPayload = {
      receivedDate: receivedDate || null,
      purchaseInvoiceNumber: purchaseInvoiceNumber || null,
      attachmentPath: null,
      notes: notes || null,
      lines,
    };
    try {
      const updated = await purchaseOrdersApi.receive(order.purchaseOrderId, payload);
      toast.success('Goods received and stock updated.');
      onReceived(updated);
    } catch (error) {
      const message = capture(error);
      if (message) {
        toast.error(message);
      }
    } finally {
      setBusy(false);
    }
  };

  const anyQuantity = pending.some((l) => Number(quantities[l.purchaseOrderLineId] ?? 0) > 0);

  return (
    <Modal
      open
      title={`Receive goods — ${order.poNumber}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} loading={busy} disabled={!anyQuantity}>
            Record receipt
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
          Each stocked line writes an incoming inventory transaction. Receiving less than
          ordered leaves the PO partially received.
        </p>
        <div className="form-grid">
          <Input
            label="Received date"
            type="date"
            required
            value={receivedDate}
            error={fieldErrors.receivedDate}
            onChange={(e) => {
              clearField('receivedDate');
              setReceivedDate(e.target.value);
            }}
          />
          <Input
            label="Supplier invoice no."
            value={purchaseInvoiceNumber}
            error={fieldErrors.purchaseInvoiceNumber}
            onChange={(e) => {
              clearField('purchaseInvoiceNumber');
              setPurchaseInvoiceNumber(e.target.value);
            }}
          />
        </div>

        {pending.length === 0 ? (
          <p>Every line on this purchase order has already been received.</p>
        ) : (
          <div className="form-stack">
            {pending.map((line) => (
              <Input
                key={line.purchaseOrderLineId}
                label={`${line.itemName} — ${line.pendingQuantity} ${line.unit ?? ''} pending`.trim()}
                type="number"
                min="0"
                max={String(line.pendingQuantity)}
                step="0.001"
                value={quantities[line.purchaseOrderLineId] ?? ''}
                error={fieldErrors[`lines[${pending.indexOf(line)}].receivedQuantity`]}
                onChange={(e) =>
                  setQuantities((q) => ({ ...q, [line.purchaseOrderLineId]: e.target.value }))
                }
              />
            ))}
          </div>
        )}

        <Input
          label="Notes"
          value={notes}
          error={fieldErrors.notes}
          onChange={(e) => {
            clearField('notes');
            setNotes(e.target.value);
          }}
        />
      </div>
    </Modal>
  );
}

// ── Cancel ──────────────────────────────────────────────────────────────────

function ConfirmCancelDialog({
  open,
  order,
  onClose,
  onCancelled,
}: {
  open: boolean;
  order: PurchaseOrder;
  onClose: () => void;
  onCancelled: (updated: PurchaseOrder) => void;
}) {
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  if (!open) {
    return null;
  }

  const submit = async () => {
    setBusy(true);
    try {
      const updated = await purchaseOrdersApi.cancel(order.purchaseOrderId, reason.trim());
      toast.success('Purchase order cancelled.');
      onCancelled(updated);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not cancel the PO.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={`Cancel ${order.poNumber}?`}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Keep it
          </Button>
          <Button onClick={submit} loading={busy} disabled={reason.trim().length === 0}>
            Cancel PO
          </Button>
        </>
      }
    >
      <div className="form-stack">
        <p>
          A PO with goods already received cannot be cancelled — correct the stock with an
          Adjustment transaction instead.
        </p>
        <Input
          label="Reason"
          required
          value={reason}
          onChange={(e) => setReason(e.target.value)}
        />
      </div>
    </Modal>
  );
}

// ── Editor ──────────────────────────────────────────────────────────────────

interface LineDraft {
  inventoryItemId: string;
  itemName: string;
  hsnSacCode: string;
  unit: string;
  quantity: string;
  rate: string;
  discountAmount: string;
  taxPercent: string;
  remarks: string;
}

const emptyLine: LineDraft = {
  inventoryItemId: '',
  itemName: '',
  hsnSacCode: '',
  unit: '',
  quantity: '1',
  rate: '0',
  discountAmount: '0',
  taxPercent: '18',
  remarks: '',
};

function PurchaseOrderEditorModal({
  order,
  onClose,
  onSaved,
}: {
  order?: PurchaseOrder;
  onClose: () => void;
  onSaved: (saved: PurchaseOrder) => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [suppliers, setSuppliers] = useState<SelectOption[]>([]);
  const [locations, setLocations] = useState<SelectOption[]>([]);
  const [items, setItems] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [header, setHeader] = useState({
    poDate: order?.poDate ?? new Date().toISOString().slice(0, 10),
    supplierId: order ? String(order.supplierId) : '',
    locationId: order ? String(order.locationId) : '',
    expectedDeliveryDate: order?.expectedDeliveryDate ?? '',
    remarks: order?.remarks ?? '',
  });
  const [lines, setLines] = useState<LineDraft[]>(
    order
      ? order.lines.map((l) => ({
          inventoryItemId: l.inventoryItemId ? String(l.inventoryItemId) : '',
          itemName: l.itemName,
          hsnSacCode: l.hsnSacCode ?? '',
          unit: l.unit ?? '',
          quantity: String(l.quantity),
          rate: String(l.rate),
          discountAmount: String(l.discountAmount),
          taxPercent: String(l.taxPercent),
          remarks: l.remarks ?? '',
        }))
      : [{ ...emptyLine }],
  );

  useEffect(() => {
    void (async () => {
      try {
        const [supplierPage, locationPage, itemPage] = await Promise.all([
          suppliersApi.list({ page: 1, pageSize: 100 }),
          locationsApi.list({ page: 1, pageSize: 100 }),
          inventoryApi.items({ page: 1, pageSize: 200 }),
        ]);
        setSuppliers(supplierPage.items.map((s) => ({ value: s.supplierId, label: s.supplierName })));
        setLocations(locationPage.items.map((l) => ({ value: l.locationId, label: l.locationName })));
        setItems(
          itemPage.items.map((i) => ({ value: i.inventoryItemId, label: `${i.itemCode} — ${i.itemName}` })),
        );
        if (!order && locationPage.items.length === 1) {
          setHeader((h) => ({ ...h, locationId: String(locationPage.items[0].locationId) }));
        }
      } catch {
        setSuppliers([]);
        setLocations([]);
        setItems([]);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const setLine = (index: number, patch: Partial<LineDraft>) =>
    setLines((current) => current.map((l, i) => (i === index ? { ...l, ...patch } : l)));

  // Preview only. The server recomputes every one of these from the same inputs and
  // its answer is the one that gets stored.
  const preview = lines.reduce(
    (acc, l) => {
      const gross = Number(l.quantity || 0) * Number(l.rate || 0);
      const net = gross - Number(l.discountAmount || 0);
      const tax = (net * Number(l.taxPercent || 0)) / 100;
      return { taxable: acc.taxable + net, tax: acc.tax + tax, total: acc.total + net + tax };
    },
    { taxable: 0, tax: 0, total: 0 },
  );

  const save = async () => {
    setBusy(true);
    clear();
    const payload: SavePurchaseOrderPayload = {
      poDate: header.poDate || null,
      supplierId: Number(header.supplierId),
      locationId: Number(header.locationId),
      expectedDeliveryDate: header.expectedDeliveryDate || null,
      remarks: header.remarks || null,
      lines: lines.map((l) => ({
        inventoryItemId: l.inventoryItemId ? Number(l.inventoryItemId) : null,
        itemName: l.itemName.trim(),
        hsnSacCode: l.hsnSacCode || null,
        unit: l.unit || null,
        quantity: Number(l.quantity || 0),
        rate: Number(l.rate || 0),
        discountAmount: Number(l.discountAmount || 0),
        taxPercent: Number(l.taxPercent || 0),
        remarks: l.remarks || null,
      })),
    };
    try {
      const saved = order
        ? await purchaseOrdersApi.update(order.purchaseOrderId, payload)
        : await purchaseOrdersApi.create(payload);
      toast.success(order ? 'Purchase order updated.' : 'Purchase order created.');
      onSaved(saved);
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
    header.supplierId !== '' &&
    header.locationId !== '' &&
    lines.length > 0 &&
    lines.every((l) => l.itemName.trim().length > 0 && Number(l.quantity) > 0);

  return (
    <Modal
      open
      title={order ? `Edit — ${order.poNumber}` : 'New purchase order'}
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
            label="PO date"
            type="date"
            required
            value={header.poDate}
            error={fieldErrors.poDate}
            onChange={(e) => {
              clearField('poDate');
              setHeader({ ...header, poDate: e.target.value });
            }}
          />
          <Select
            label="Supplier"
            required
            value={header.supplierId}
            placeholder="Select a supplier"
            options={suppliers}
            error={fieldErrors.supplierId}
            onChange={(e) => {
              clearField('supplierId');
              setHeader({ ...header, supplierId: e.target.value });
            }}
          />
          <Select
            label="Location"
            required
            value={header.locationId}
            placeholder="Select a location"
            options={locations}
            error={fieldErrors.locationId}
            onChange={(e) => {
              clearField('locationId');
              setHeader({ ...header, locationId: e.target.value });
            }}
          />
          <Input
            label="Expected delivery"
            type="date"
            value={header.expectedDeliveryDate}
            error={fieldErrors.expectedDeliveryDate}
            onChange={(e) => {
              clearField('expectedDeliveryDate');
              setHeader({ ...header, expectedDeliveryDate: e.target.value });
            }}
          />
        </div>
        <Input
          label="Remarks"
          value={header.remarks}
          error={fieldErrors.remarks}
          onChange={(e) => setHeader({ ...header, remarks: e.target.value })}
        />

        {lines.map((line, index) => (
          <Card key={index}>
            <div className="form-grid">
              <Select
                label="Stock item"
                value={line.inventoryItemId}
                placeholder="Not stocked"
                options={items}
                onChange={(e) => {
                  const option = items.find((i) => String(i.value) === e.target.value);
                  setLine(index, {
                    inventoryItemId: e.target.value,
                    // Pre-fill the name from the master; still editable for the PO copy.
                    itemName: option ? String(option.label).split(' — ')[1] ?? line.itemName : line.itemName,
                  });
                }}
              />
              <Input
                label="Item name"
                required
                value={line.itemName}
                error={fieldErrors[`lines[${index}].itemName`]}
                onChange={(e) => setLine(index, { itemName: e.target.value })}
              />
              <Input
                label="Unit"
                value={line.unit}
                onChange={(e) => setLine(index, { unit: e.target.value })}
              />
              <Input
                label="HSN / SAC"
                value={line.hsnSacCode}
                onChange={(e) => setLine(index, { hsnSacCode: e.target.value })}
              />
              <Input
                label="Quantity"
                type="number"
                min="0"
                step="0.001"
                required
                value={line.quantity}
                error={fieldErrors[`lines[${index}].quantity`]}
                onChange={(e) => setLine(index, { quantity: e.target.value })}
              />
              <Input
                label="Rate"
                type="number"
                min="0"
                step="0.01"
                required
                value={line.rate}
                error={fieldErrors[`lines[${index}].rate`]}
                onChange={(e) => setLine(index, { rate: e.target.value })}
              />
              <Input
                label="Discount"
                type="number"
                min="0"
                step="0.01"
                value={line.discountAmount}
                error={fieldErrors[`lines[${index}].discountAmount`]}
                onChange={(e) => setLine(index, { discountAmount: e.target.value })}
              />
              <Input
                label="Supplier GST %"
                type="number"
                min="0"
                max="100"
                step="0.01"
                value={line.taxPercent}
                hint="From the supplier's quote"
                error={fieldErrors[`lines[${index}].taxPercent`]}
                onChange={(e) => setLine(index, { taxPercent: e.target.value })}
              />
            </div>
            {lines.length > 1 && (
              <Button
                size="sm"
                variant="ghost"
                onClick={() => setLines(lines.filter((_, i) => i !== index))}
              >
                Remove line
              </Button>
            )}
          </Card>
        ))}

        <Button variant="outline" onClick={() => setLines([...lines, { ...emptyLine }])}>
          Add line
        </Button>

        <div className="toolbar">
          <Detail label="Taxable">{formatInr(preview.taxable)}</Detail>
          <Detail label="Tax">{formatInr(preview.tax)}</Detail>
          <Detail label="Total (preview)">
            <strong>{formatInr(preview.total)}</strong>
          </Detail>
        </div>
      </div>
    </Modal>
  );
}
