import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { quotationsApi } from '@/api/quotations';
import { invoiceApi } from '@/api/finance';
import { customersApi } from '@/api/admin';
import { locationsApi, workspaceTypesApi } from '@/api/masters';
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
  DURATION_UNITS,
  QUOTATION_STATUS,
  QUOTATION_STATUS_LABELS,
  QUOTATION_STATUS_TONES,
  type Quotation,
  type QuotationLine,
  type SaveQuotationPayload,
} from '@/models/quotations';
import { formatDate, formatDateTime, formatInr } from '@/utils/formatters';

const STATUS_OPTIONS: SelectOption[] = Object.entries(QUOTATION_STATUS_LABELS).map(
  ([value, label]) => ({ value: Number(value), label }),
);

export function AdminQuotationsPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const canCreate = usePermission('QUOTATION.CREATE');

  const [result, setResult] = useState<PagedResult<Quotation> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [status, setStatus] = useState('');
  const [creating, setCreating] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await quotationsApi.list({
        page,
        pageSize: 20,
        status: status ? Number(status) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load quotations.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, status]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Quotation>[] = [
    {
      key: 'number',
      header: 'Quotation',
      render: (q) => (
        <div>
          <div style={{ fontWeight: 600 }}>{q.quotationNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {formatDate(q.quotationDate)}
          </div>
        </div>
      ),
    },
    { key: 'customer', header: 'Customer', render: (q) => q.customerName || '—' },
    { key: 'lines', header: 'Lines', render: (q) => q.lines.length },
    { key: 'total', header: 'Total', render: (q) => <strong>{formatInr(q.totalAmount)}</strong> },
    {
      key: 'valid',
      header: 'Valid until',
      render: (q) => (
        <span style={{ color: q.isExpired ? 'var(--color-danger)' : undefined }}>
          {formatDate(q.validUntil)}
        </span>
      ),
    },
    {
      key: 'status',
      header: 'Status',
      render: (q) => <StatusBadge quotation={q} />,
    },
    {
      key: 'invoice',
      header: 'Invoice',
      render: (q) => q.invoiceNumber ?? '—',
    },
    {
      key: 'actions',
      header: '',
      render: (q) => (
        <div className="row-actions">
          <Button
            size="sm"
            variant="outline"
            onClick={() => navigate(`/admin/quotations/${q.quotationId}`)}
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
        title="Quotations"
        description="Raise a priced offer for a customer. Once sent it is frozen, and the customer approves or rejects it in their portal."
        actions={canCreate && <Button onClick={() => setCreating(true)}>New quotation</Button>}
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
          rowKey={(q) => q.quotationId}
          onPageChange={setPage}
          emptyTitle="No quotations yet"
          emptyDescription="A quotation is the normal starting point for an invoice."
        />
      </Card>

      {creating && (
        <QuotationEditorModal
          onClose={() => setCreating(false)}
          onSaved={(saved) => {
            setCreating(false);
            navigate(`/admin/quotations/${saved.quotationId}`);
          }}
        />
      )}
    </>
  );
}

function StatusBadge({ quotation }: { quotation: Quotation }) {
  // A quotation past its validity reads as Expired immediately, even before the
  // nightly sweeper writes the status.
  if (quotation.isExpired) {
    return <Badge tone="neutral">Expired</Badge>;
  }
  return (
    <Badge tone={QUOTATION_STATUS_TONES[quotation.status] ?? 'neutral'}>
      {QUOTATION_STATUS_LABELS[quotation.status] ?? 'Unknown'}
    </Badge>
  );
}

// ── Detail ──────────────────────────────────────────────────────────────────

export function AdminQuotationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const toast = useToast();
  const navigate = useNavigate();
  const canEdit = usePermission('QUOTATION.EDIT');
  const canCancel = usePermission('QUOTATION.DELETE');
  const canPrint = usePermission('QUOTATION.PRINT');
  const canInvoice = usePermission('INVOICE.CREATE');

  const [quotation, setQuotation] = useState<Quotation | null>(null);
  const [loading, setLoading] = useState(true);
  const [busy, setBusy] = useState(false);
  const [editing, setEditing] = useState(false);
  const [cancelling, setCancelling] = useState(false);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setQuotation(await quotationsApi.get(Number(id)));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the quotation.');
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
  if (!quotation) {
    return null;
  }

  const isDraft = quotation.status === QUOTATION_STATUS.Draft;
  const isClosed =
    quotation.status === QUOTATION_STATUS.Approved ||
    quotation.status === QUOTATION_STATUS.Rejected ||
    quotation.status === QUOTATION_STATUS.Cancelled;

  const send = async () => {
    setBusy(true);
    try {
      setQuotation(await quotationsApi.send(quotation.quotationId));
      toast.success('Quotation sent to the customer.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not send the quotation.');
    } finally {
      setBusy(false);
    }
  };

  // Rules 4 and 5: only an approved quotation can be invoiced, and only once.
  const generateInvoice = async () => {
    setBusy(true);
    try {
      const invoice = await invoiceApi.fromQuotation(quotation.quotationId);
      toast.success(`Invoice ${invoice.invoiceNumber} raised.`);
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not raise the invoice.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader
        title={quotation.quotationNumber}
        description={`${quotation.customerName || 'Customer'} · ${formatDate(quotation.quotationDate)}`}
        actions={
          <>
            <Button variant="ghost" onClick={() => navigate('/admin/quotations')}>
              Back
            </Button>
            {canPrint && !isDraft && (
              <Button
                variant="outline"
                onClick={() => void quotationsApi.pdf(quotation.quotationId, quotation.quotationNumber)}
              >
                Download PDF
              </Button>
            )}
            {isDraft && canEdit && (
              <Button variant="outline" onClick={() => setEditing(true)}>
                Edit
              </Button>
            )}
            {isDraft && canEdit && (
              <Button onClick={send} loading={busy}>
                Send to customer
              </Button>
            )}
            {quotation.status === QUOTATION_STATUS.Approved && !quotation.invoiceId && canInvoice && (
              <Button onClick={generateInvoice} loading={busy}>
                Generate invoice
              </Button>
            )}
            {!isClosed && canCancel && (
              <Button variant="ghost" onClick={() => setCancelling(true)}>
                Cancel
              </Button>
            )}
          </>
        }
      />

      <QuotationSummaryCard quotation={quotation} />
      <QuotationLinesCard quotation={quotation} />
      <QuotationHistoryCard quotation={quotation} />

      {editing && (
        <QuotationEditorModal
          quotation={quotation}
          onClose={() => setEditing(false)}
          onSaved={(saved) => {
            setEditing(false);
            setQuotation(saved);
          }}
        />
      )}

      {cancelling && (
        <CancelQuotationModal
          quotation={quotation}
          onClose={() => setCancelling(false)}
          onCancelled={(saved) => {
            setCancelling(false);
            setQuotation(saved);
          }}
        />
      )}
    </>
  );
}

export function QuotationSummaryCard({ quotation }: { quotation: Quotation }) {
  return (
    <Card>
      <div className="toolbar">
        <Detail label="Status">
          <StatusBadge quotation={quotation} />
        </Detail>
        <Detail label="Valid until">{formatDate(quotation.validUntil)}</Detail>
        <Detail label="Location">{quotation.locationName ?? '—'}</Detail>
        <Detail label="Sent">{quotation.sentUtc ? formatDateTime(quotation.sentUtc) : '—'}</Detail>
        <Detail label="Viewed">{quotation.viewedUtc ? formatDateTime(quotation.viewedUtc) : '—'}</Detail>
        <Detail label="Decision">
          {quotation.respondedUtc ? formatDateTime(quotation.respondedUtc) : '—'}
        </Detail>
      </div>
      {quotation.rejectionReason && (
        <div className="form-error" role="alert">
          Rejected: {quotation.rejectionReason}
        </div>
      )}
      {quotation.invoiceNumber && (
        <p>
          Invoiced as <strong>{quotation.invoiceNumber}</strong>.
        </p>
      )}
      {quotation.remarks && <p style={{ color: 'var(--text-muted)' }}>{quotation.remarks}</p>}
    </Card>
  );
}

export function QuotationLinesCard({ quotation }: { quotation: Quotation }) {
  const columns: Column<QuotationLine>[] = [
    { key: 'no', header: '#', render: (l) => l.lineNumber, width: '48px' },
    {
      key: 'description',
      header: 'Description',
      render: (l) => (
        <div>
          <div style={{ fontWeight: 600 }}>{l.description}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {l.workspaceTypeName ?? l.serviceItemName ?? 'Other'}
            {l.hsnSacCode ? ` · HSN ${l.hsnSacCode}` : ''}
          </div>
        </div>
      ),
    },
    { key: 'qty', header: 'Qty', render: (l) => l.quantity },
    {
      key: 'duration',
      header: 'Duration',
      render: (l) => (l.durationValue ? `${l.durationValue} ${l.durationUnit ?? ''}`.trim() : '—'),
    },
    { key: 'rate', header: 'Rate', render: (l) => formatInr(l.unitPrice) },
    { key: 'discount', header: 'Discount', render: (l) => formatInr(l.discountAmount) },
    { key: 'amount', header: 'Amount', render: (l) => formatInr(l.lineAmount) },
    { key: 'total', header: 'Total', render: (l) => <strong>{formatInr(l.totalAmount)}</strong> },
  ];

  return (
    <Card>
      <DataTable
        columns={columns}
        result={{
          items: quotation.lines,
          page: 1,
          pageSize: quotation.lines.length || 1,
          totalCount: quotation.lines.length,
          totalPages: 1,
          hasNext: false,
          hasPrevious: false,
        }}
        loading={false}
        rowKey={(l) => l.quotationLineId}
        onPageChange={() => undefined}
      />
      <div className="toolbar">
        <Detail label="Sub total">{formatInr(quotation.subTotal)}</Detail>
        <Detail label="Discount">{formatInr(quotation.discountAmount)}</Detail>
        <Detail label="Taxable">{formatInr(quotation.taxableAmount)}</Detail>
        <Detail label={`CGST @ ${quotation.cgstPercent}%`}>{formatInr(quotation.cgstAmount)}</Detail>
        <Detail label={`SGST @ ${quotation.sgstPercent}%`}>{formatInr(quotation.sgstAmount)}</Detail>
        <Detail label="Total">
          <strong style={{ fontSize: 'var(--font-size-lg)' }}>{formatInr(quotation.totalAmount)}</strong>
        </Detail>
      </div>
      {quotation.termsAndConditions && (
        <div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            Terms &amp; conditions
          </div>
          <p style={{ whiteSpace: 'pre-wrap' }}>{quotation.termsAndConditions}</p>
        </div>
      )}
    </Card>
  );
}

function QuotationHistoryCard({ quotation }: { quotation: Quotation }) {
  if (quotation.statusHistory.length === 0) {
    return null;
  }
  return (
    <Card>
      <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>History</div>
      <ul style={{ margin: 0, paddingLeft: '1.1rem' }}>
        {quotation.statusHistory.map((entry, index) => (
          <li key={index}>
            <strong>{QUOTATION_STATUS_LABELS[entry.toStatus] ?? entry.toStatus}</strong>
            {' · '}
            {formatDateTime(entry.changedUtc)}
            {' · '}
            {entry.changedByName || 'System'}
            {entry.reason ? ` — ${entry.reason}` : ''}
          </li>
        ))}
      </ul>
    </Card>
  );
}

export function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{label}</div>
      <div>{children}</div>
    </div>
  );
}

// ── Cancel ──────────────────────────────────────────────────────────────────

function CancelQuotationModal({
  quotation,
  onClose,
  onCancelled,
}: {
  quotation: Quotation;
  onClose: () => void;
  onCancelled: (saved: Quotation) => void;
}) {
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      const saved = await quotationsApi.cancel(quotation.quotationId, reason.trim());
      toast.success('Quotation cancelled.');
      onCancelled(saved);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not cancel.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={`Cancel ${quotation.quotationNumber}?`}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Keep it
          </Button>
          <Button onClick={submit} loading={busy} disabled={reason.trim().length === 0}>
            Cancel quotation
          </Button>
        </>
      }
    >
      <div className="form-stack">
        <p>The customer will no longer be able to act on it. The reason is recorded in the history.</p>
        <Input label="Reason" required value={reason} onChange={(e) => setReason(e.target.value)} />
      </div>
    </Modal>
  );
}

// ── Editor ──────────────────────────────────────────────────────────────────

interface LineDraft {
  workspaceTypeId: string;
  description: string;
  hsnSacCode: string;
  quantity: string;
  durationValue: string;
  durationUnit: string;
  unitPrice: string;
  discountAmount: string;
}

const emptyLine: LineDraft = {
  workspaceTypeId: '',
  description: '',
  hsnSacCode: '',
  quantity: '1',
  durationValue: '',
  durationUnit: '',
  unitPrice: '0',
  discountAmount: '0',
};

function QuotationEditorModal({
  quotation,
  onClose,
  onSaved,
}: {
  quotation?: Quotation;
  onClose: () => void;
  onSaved: (saved: Quotation) => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [customers, setCustomers] = useState<SelectOption[]>([]);
  const [locations, setLocations] = useState<SelectOption[]>([]);
  const [workspaceTypes, setWorkspaceTypes] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [header, setHeader] = useState({
    customerId: quotation ? String(quotation.customerId) : '',
    locationId: quotation ? String(quotation.locationId) : '',
    quotationDate: quotation?.quotationDate ?? new Date().toISOString().slice(0, 10),
    validUntil: quotation?.validUntil ?? '',
    termsAndConditions: quotation?.termsAndConditions ?? '',
    remarks: quotation?.remarks ?? '',
  });
  const [lines, setLines] = useState<LineDraft[]>(
    quotation
      ? quotation.lines.map((l) => ({
          workspaceTypeId: l.workspaceTypeId ? String(l.workspaceTypeId) : '',
          description: l.description,
          hsnSacCode: l.hsnSacCode ?? '',
          quantity: String(l.quantity),
          durationValue: l.durationValue ? String(l.durationValue) : '',
          durationUnit: l.durationUnit ?? '',
          unitPrice: String(l.unitPrice),
          discountAmount: String(l.discountAmount),
        }))
      : [{ ...emptyLine }],
  );

  useEffect(() => {
    void (async () => {
      try {
        const [customerPage, locationPage, types] = await Promise.all([
          customersApi.list({ page: 1, pageSize: 200 }),
          locationsApi.list({ page: 1, pageSize: 100 }),
          workspaceTypesApi.list(),
        ]);
        setCustomers(
          customerPage.items.map((c) => ({ value: c.userId, label: `${c.fullName} — ${c.email}` })),
        );
        setLocations(locationPage.items.map((l) => ({ value: l.locationId, label: l.locationName })));
        setWorkspaceTypes(types.map((t) => ({ value: t.workspaceTypeId, label: t.typeName })));
        if (!quotation && locationPage.items.length === 1) {
          setHeader((h) => ({ ...h, locationId: String(locationPage.items[0].locationId) }));
        }
      } catch {
        setCustomers([]);
        setLocations([]);
        setWorkspaceTypes([]);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const setLine = (index: number, patch: Partial<LineDraft>) =>
    setLines((current) => current.map((l, i) => (i === index ? { ...l, ...patch } : l)));

  const save = async () => {
    setBusy(true);
    clear();
    const payload: SaveQuotationPayload = {
      customerId: Number(header.customerId),
      locationId: Number(header.locationId),
      quotationDate: header.quotationDate || null,
      validUntil: header.validUntil || null,
      termsAndConditions: header.termsAndConditions || null,
      remarks: header.remarks || null,
      lines: lines.map((l) => ({
        workspaceTypeId: l.workspaceTypeId ? Number(l.workspaceTypeId) : null,
        serviceItemId: null,
        description: l.description.trim(),
        hsnSacCode: l.hsnSacCode || null,
        quantity: Number(l.quantity || 0),
        durationValue: l.durationValue ? Number(l.durationValue) : null,
        durationUnit: l.durationUnit || null,
        unitPrice: Number(l.unitPrice || 0),
        discountAmount: Number(l.discountAmount || 0),
      })),
    };
    try {
      const saved = quotation
        ? await quotationsApi.update(quotation.quotationId, payload)
        : await quotationsApi.create(payload);
      toast.success(quotation ? 'Quotation updated.' : 'Quotation created as a draft.');
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
    header.customerId !== '' &&
    header.locationId !== '' &&
    lines.length > 0 &&
    lines.every((l) => l.description.trim().length > 0 && Number(l.quantity) > 0);

  return (
    <Modal
      open
      title={quotation ? `Edit — ${quotation.quotationNumber}` : 'New quotation'}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={save} loading={busy} disabled={!canSave}>
            Save draft
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
          GST is applied server-side from the tax settings when the quotation is sent, so the
          customer sees the same figures the invoice will carry.
        </p>
        <div className="form-grid">
          <Select
            label="Customer"
            required
            value={header.customerId}
            placeholder="Select a customer"
            options={customers}
            error={fieldErrors.customerId}
            onChange={(e) => {
              clearField('customerId');
              setHeader({ ...header, customerId: e.target.value });
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
            label="Quotation date"
            type="date"
            required
            value={header.quotationDate}
            error={fieldErrors.quotationDate}
            onChange={(e) => setHeader({ ...header, quotationDate: e.target.value })}
          />
          <Input
            label="Valid until"
            type="date"
            value={header.validUntil}
            hint="Defaults to the configured validity window"
            error={fieldErrors.validUntil}
            onChange={(e) => {
              clearField('validUntil');
              setHeader({ ...header, validUntil: e.target.value });
            }}
          />
        </div>

        {lines.map((line, index) => (
          <Card key={index}>
            <div className="form-grid">
              <Select
                label="Service type"
                value={line.workspaceTypeId}
                placeholder="Other"
                options={workspaceTypes}
                onChange={(e) => setLine(index, { workspaceTypeId: e.target.value })}
              />
              <Input
                label="Description"
                required
                value={line.description}
                error={fieldErrors[`lines[${index}].description`]}
                onChange={(e) => setLine(index, { description: e.target.value })}
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
                label="Duration"
                type="number"
                min="0"
                step="0.001"
                value={line.durationValue}
                hint="Optional — multiplies the quantity"
                error={fieldErrors[`lines[${index}].durationValue`]}
                onChange={(e) => setLine(index, { durationValue: e.target.value })}
              />
              <Select
                label="Duration unit"
                value={line.durationUnit}
                placeholder="—"
                options={DURATION_UNITS.map((u) => ({ value: u, label: u }))}
                error={fieldErrors[`lines[${index}].durationUnit`]}
                onChange={(e) => setLine(index, { durationUnit: e.target.value })}
              />
              <Input
                label="Rate"
                type="number"
                min="0"
                step="0.01"
                required
                value={line.unitPrice}
                error={fieldErrors[`lines[${index}].unitPrice`]}
                onChange={(e) => setLine(index, { unitPrice: e.target.value })}
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
                label="HSN / SAC"
                value={line.hsnSacCode}
                onChange={(e) => setLine(index, { hsnSacCode: e.target.value })}
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

        <Input
          label="Terms & conditions"
          value={header.termsAndConditions}
          error={fieldErrors.termsAndConditions}
          onChange={(e) => setHeader({ ...header, termsAndConditions: e.target.value })}
        />
        <Input
          label="Remarks"
          value={header.remarks}
          error={fieldErrors.remarks}
          onChange={(e) => setHeader({ ...header, remarks: e.target.value })}
        />
      </div>
    </Modal>
  );
}
