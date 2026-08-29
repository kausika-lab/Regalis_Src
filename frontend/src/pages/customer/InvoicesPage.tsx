import { useCallback, useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { invoicesApi as invoicePdfApi } from '@/api/bookings';
import { invoiceApi } from '@/api/finance';
import { ApiClientError } from '@/api/client';
import { Badge, Button, Card, DataTable, PageHeader, type Column } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { PagedResult } from '@/models/api';
import type { Invoice } from '@/models/finance';
import { INVOICE_STATUS_LABELS } from '@/models/finance';
import { formatDate, formatInr } from '@/utils/formatters';

export function InvoicesPage() {
  const toast = useToast();
  const [tab, setTab] = useState<'all' | 'pending' | 'paid'>('all');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<Invoice> | null>(null);
  const [loading, setLoading] = useState(true);

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
            {i.lines[0]?.description ?? ''}
          </div>
        </div>
      ),
    },
    { key: 'date', header: 'Date', render: (i) => formatDate(i.invoiceDate) },
    { key: 'total', header: 'Total', render: (i) => formatInr(i.totalAmount) },
    {
      key: 'status',
      header: 'Status',
      render: (i) => (
        <Badge tone={i.status === 2 ? 'success' : i.status === 4 ? 'neutral' : 'warning'}>
          {INVOICE_STATUS_LABELS[i.status]}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
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
    </>
  );
}
