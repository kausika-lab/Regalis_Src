import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { quotationsApi } from '@/api/quotations';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  DataTable,
  Input,
  Modal,
  PageHeader,
  Spinner,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import {
  QUOTATION_STATUS,
  QUOTATION_STATUS_LABELS,
  QUOTATION_STATUS_TONES,
  type Quotation,
} from '@/models/quotations';
import { formatDate, formatDateTime, formatInr } from '@/utils/formatters';
import { QuotationLinesCard, QuotationSummaryCard } from '@/pages/admin/AdminQuotationPages';

/** The customer's own quotations. Drafts never reach this list — the server filters
 * them out, and every row is scoped to the signed-in customer regardless of any id
 * in the URL. */
export function MyQuotationsPage() {
  const toast = useToast();
  const navigate = useNavigate();

  const [result, setResult] = useState<PagedResult<Quotation> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await quotationsApi.mine({ page, pageSize: 20 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load quotations.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

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
    {
      key: 'summary',
      header: 'For',
      render: (q) =>
        q.lines.length === 1
          ? q.lines[0].description
          : `${q.lines.length} items`,
    },
    { key: 'total', header: 'Total', render: (q) => <strong>{formatInr(q.totalAmount)}</strong> },
    { key: 'valid', header: 'Valid until', render: (q) => formatDate(q.validUntil) },
    {
      key: 'status',
      header: 'Status',
      render: (q) =>
        q.isExpired ? (
          <Badge tone="neutral">Expired</Badge>
        ) : (
          <Badge tone={QUOTATION_STATUS_TONES[q.status] ?? 'neutral'}>
            {QUOTATION_STATUS_LABELS[q.status] ?? 'Unknown'}
          </Badge>
        ),
    },
    {
      key: 'actions',
      header: '',
      render: (q) => (
        <div className="row-actions">
          <Button size="sm" variant="outline" onClick={() => navigate(`/quotations/${q.quotationId}`)}>
            {q.status === QUOTATION_STATUS.Sent || q.status === QUOTATION_STATUS.Viewed
              ? 'Review'
              : 'View'}
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Quotations"
        description="Offers we have prepared for you. Approve one to have it invoiced."
      />
      <Card>
        <DataTable
          columns={columns}
          result={result}
          loading={loading}
          rowKey={(q) => q.quotationId}
          onPageChange={setPage}
          emptyTitle="No quotations yet"
          emptyDescription="When we prepare a quotation for you it will appear here."
        />
      </Card>
    </>
  );
}

export function MyQuotationDetailPage() {
  const { id } = useParams<{ id: string }>();
  const toast = useToast();
  const navigate = useNavigate();
  const canDecide = usePermission('QUOTATION.APPROVE');
  const canPrint = usePermission('QUOTATION.PRINT');

  const [quotation, setQuotation] = useState<Quotation | null>(null);
  const [loading, setLoading] = useState(true);
  const [decision, setDecision] = useState<'approve' | 'reject' | null>(null);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      // Opening a sent quotation marks it Viewed server-side.
      setQuotation(await quotationsApi.mineById(Number(id)));
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

  const isOpen =
    !quotation.isExpired &&
    (quotation.status === QUOTATION_STATUS.Sent || quotation.status === QUOTATION_STATUS.Viewed);

  return (
    <>
      <PageHeader
        title={quotation.quotationNumber}
        description={`Valid until ${formatDate(quotation.validUntil)}`}
        actions={
          <>
            <Button variant="ghost" onClick={() => navigate('/quotations')}>
              Back
            </Button>
            {canPrint && (
              <Button
                variant="outline"
                onClick={() => void quotationsApi.pdf(quotation.quotationId, quotation.quotationNumber)}
              >
                Download PDF
              </Button>
            )}
            {isOpen && canDecide && (
              <>
                <Button variant="ghost" onClick={() => setDecision('reject')}>
                  Reject
                </Button>
                <Button onClick={() => setDecision('approve')}>Approve</Button>
              </>
            )}
          </>
        }
      />

      {quotation.status === QUOTATION_STATUS.Approved && (
        <Card>
          <p>
            You approved this quotation on {formatDateTime(quotation.respondedUtc ?? '')}.
            {quotation.invoiceNumber
              ? ` It has been invoiced as ${quotation.invoiceNumber}.`
              : ' An invoice will follow shortly.'}
          </p>
        </Card>
      )}
      {quotation.status === QUOTATION_STATUS.Rejected && (
        <Card>
          <p>
            You rejected this quotation on {formatDateTime(quotation.respondedUtc ?? '')}.
            {quotation.rejectionReason ? ` Reason: ${quotation.rejectionReason}` : ''}
          </p>
        </Card>
      )}
      {quotation.isExpired && (
        <Card>
          <p>
            This quotation lapsed on {formatDate(quotation.validUntil)}. Please ask us for a fresh one.
          </p>
        </Card>
      )}

      <QuotationSummaryCard quotation={quotation} />
      <QuotationLinesCard quotation={quotation} />

      {decision && (
        <DecisionModal
          quotation={quotation}
          decision={decision}
          onClose={() => setDecision(null)}
          onDecided={(saved) => {
            setDecision(null);
            setQuotation(saved);
          }}
        />
      )}
    </>
  );
}

function DecisionModal({
  quotation,
  decision,
  onClose,
  onDecided,
}: {
  quotation: Quotation;
  decision: 'approve' | 'reject';
  onClose: () => void;
  onDecided: (saved: Quotation) => void;
}) {
  const toast = useToast();
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const approving = decision === 'approve';

  const submit = async () => {
    setBusy(true);
    try {
      const saved = approving
        ? await quotationsApi.approve(quotation.quotationId, reason.trim() || undefined)
        : await quotationsApi.reject(quotation.quotationId, reason.trim() || undefined);
      toast.success(approving ? 'Quotation approved.' : 'Quotation rejected.');
      onDecided(saved);
    } catch (error) {
      toast.error(
        error instanceof ApiClientError ? error.message : 'Could not record your decision.',
      );
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={approving ? `Approve ${quotation.quotationNumber}?` : `Reject ${quotation.quotationNumber}?`}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button onClick={submit} loading={busy}>
            {approving ? 'Approve' : 'Reject'}
          </Button>
        </>
      }
    >
      <div className="form-stack">
        <p>
          {approving
            ? `You are approving ${formatInr(quotation.totalAmount)} including GST. We will raise an invoice against this quotation.`
            : 'We will be told that this quotation was not accepted. No invoice will be raised.'}
        </p>
        <Input
          label={approving ? 'Note (optional)' : 'Reason (optional)'}
          value={reason}
          onChange={(e) => setReason(e.target.value)}
        />
      </div>
    </Modal>
  );
}
