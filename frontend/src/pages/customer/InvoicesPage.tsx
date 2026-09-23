import { useCallback, useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { invoicesApi as invoicePdfApi, paymentsApi } from '@/api/bookings';
import { invoiceApi, invoicePaymentApi } from '@/api/finance';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  DataTable,
  Input,
  Modal,
  PageHeader,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { Invoice } from '@/models/finance';
import { INVOICE_STATUS_LABELS } from '@/models/finance';
import { formatDate, formatInr } from '@/utils/formatters';

export function InvoicesPage() {
  const toast = useToast();
  const canPay = usePermission('PAYMENT.CREATE');
  const [tab, setTab] = useState<'all' | 'pending' | 'paid'>('all');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<Invoice> | null>(null);
  const [loading, setLoading] = useState(true);
  const [payTarget, setPayTarget] = useState<Invoice | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await invoiceApi.my({
        page,
        pageSize: 20,
        status: tab === 'all' ? undefined : tab,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load invoices.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Invoice>[] = [
    {
      key: 'number',
      header: 'Invoice',
      render: (i) => (
        <div>
          <div style={{ fontWeight: 600 }}>{i.invoiceNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {i.quotationNumber ? `From ${i.quotationNumber}` : i.lines[0]?.description ?? ''}
          </div>
        </div>
      ),
    },
    { key: 'date', header: 'Date', render: (i) => formatDate(i.invoiceDate) },
    {
      key: 'due',
      header: 'Due',
      render: (i) => (
        <span style={{ color: i.isOverdue ? 'var(--color-danger)' : undefined }}>
          {formatDate(i.dueDate)}
        </span>
      ),
    },
    { key: 'total', header: 'Total', render: (i) => formatInr(i.totalAmount) },
    {
      key: 'outstanding',
      header: 'Outstanding',
      render: (i) =>
        i.outstandingAmount > 0 ? <strong>{formatInr(i.outstandingAmount)}</strong> : '—',
    },
    {
      key: 'status',
      header: 'Status',
      render: (i) => (
        <>
          <Badge tone={i.status === 2 ? 'success' : i.status === 4 ? 'neutral' : 'warning'}>
            {INVOICE_STATUS_LABELS[i.status]}
          </Badge>
          {i.isOverdue && <Badge tone="danger">Overdue</Badge>}
        </>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (i) => (
        <div className="row-actions">
          <Button size="sm" variant="outline"
            onClick={() => invoicePdfApi.downloadPdf(i.invoiceId, i.invoiceNumber)}>
            <Download size={14} /> PDF
          </Button>
          {canPay && i.outstandingAmount > 0 && i.status !== 4 && (
            <Button size="sm" onClick={() => setPayTarget(i)}>
              Pay now
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Invoices" description="GST tax invoices for bookings, passes and services." />
      <div className="wizard-steps">
        {(['all', 'pending', 'paid'] as const).map((t) => (
          <button key={t} type="button"
            className={`wizard-step${tab === t ? ' wizard-step--active' : ''}`}
            style={{ border: 'none', cursor: 'pointer' }}
            onClick={() => { setTab(t); setPage(1); }}>
            {t[0].toUpperCase() + t.slice(1)}
          </button>
        ))}
      </div>
      <Card>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(i) => i.invoiceId} onPageChange={setPage}
          emptyTitle="No invoices yet" />
      </Card>

      {payTarget && (
        <PayInvoiceModal
          invoice={payTarget}
          onClose={() => setPayTarget(null)}
          onPaid={() => { setPayTarget(null); void load(); }}
        />
      )}
    </>
  );
}

/**
 * Pay Now (requirements §11). The amount defaults to the full outstanding balance;
 * a smaller amount is allowed and leaves the invoice partially paid. Nothing here
 * decides whether the payment succeeded — the server recomputes the gateway signature
 * and only then moves the invoice.
 */
function PayInvoiceModal({
  invoice,
  onClose,
  onPaid,
}: {
  invoice: Invoice;
  onClose: () => void;
  onPaid: () => void;
}) {
  const toast = useToast();
  const [amount, setAmount] = useState(String(invoice.outstandingAmount));
  const [busy, setBusy] = useState(false);

  const requested = Number(amount);
  const valid = requested > 0 && requested <= invoice.outstandingAmount;

  const pay = async () => {
    setBusy(true);
    try {
      const order = await invoicePaymentApi.createOrder(
        invoice.invoiceId,
        requested === invoice.outstandingAmount ? undefined : requested,
      );
      // Sandbox checkout simulation; the Razorpay widget slots in with real keys.
      const paid = await paymentsApi.sandboxComplete(order.gatewayOrderId);
      const result = await invoicePaymentApi.verify(
        order.gatewayOrderId, paid.gatewayPaymentId, paid.gatewaySignature);

      toast.success(
        result.outstandingAmount > 0
          ? `Received ${formatInr(result.payment.amount)}. ${formatInr(result.outstandingAmount)} still outstanding.`
          : `Invoice ${result.invoiceNumber} is paid in full.`,
      );
      onPaid();
    } catch (error) {
      // A failed payment leaves the invoice unpaid and retryable, by design.
      toast.error(
        error instanceof ApiClientError
          ? error.message
          : 'The payment could not be completed. The invoice is unchanged — please try again.',
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={`Pay ${invoice.invoiceNumber}`}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={pay} loading={busy} disabled={!valid}>
            Pay {valid ? formatInr(requested) : ''}
          </Button>
        </>
      }
    >
      <div className="form-stack">
        <p>
          Outstanding on this invoice: <strong>{formatInr(invoice.outstandingAmount)}</strong>
          {invoice.paidAmount > 0 && ` (${formatInr(invoice.paidAmount)} already paid)`}.
        </p>
        <Input
          label="Amount to pay"
          type="number"
          min="0"
          step="0.01"
          required
          value={amount}
          hint="Pay the full balance, or part of it"
          error={
            requested > invoice.outstandingAmount
              ? 'That is more than the outstanding balance.'
              : undefined
          }
          onChange={(e) => setAmount(e.target.value)}
        />
      </div>
    </Modal>
  );
}
