import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { BellRing, Check, Trash2 } from 'lucide-react';
import { notificationApi } from '@/api/finance';
import { ApiClientError } from '@/api/client';
import { Button, Card, DataTable, PageHeader, type Column } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { PagedResult } from '@/models/api';
import type { AppNotification } from '@/models/finance';
import { formatDateTime } from '@/utils/formatters';

export function NotificationsPage() {
  const toast = useToast();
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<AppNotification> | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await notificationApi.my({ page, pageSize: 20 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load notifications.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<AppNotification>[] = [
    {
      key: 'notification',
      header: 'Notification',
      render: (n) => (
        <div style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'flex-start' }}>
          <BellRing size={18}
            color={n.isRead ? 'var(--neutral-300)' : 'var(--regalis-gold-dark)'}
            style={{ marginTop: 2, flexShrink: 0 }} />
          <div>
            <div style={{ fontWeight: n.isRead ? 500 : 700 }}>{n.title}</div>
            <div style={{ color: 'var(--text-secondary)', fontSize: 'var(--font-size-sm)' }}>
              {n.message}
            </div>
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)', marginTop: 2 }}>
              {formatDateTime(n.createdDate)}
              {n.actionRoute && n.actionLabel && (
                <> · <Link to={n.actionRoute}>{n.actionLabel}</Link></>
              )}
            </div>
          </div>
        </div>
      ),
    },
    {
      key: 'actions',
      header: '',
      width: '160px',
      render: (n) => (
        <div className="row-actions">
          {!n.isRead && (
            <Button size="sm" variant="ghost"
              onClick={async () => { await notificationApi.markRead(n.notificationId); await load(); }}>
              <Check size={14} /> Read
            </Button>
          )}
          <Button size="sm" variant="ghost"
            onClick={async () => { await notificationApi.remove(n.notificationId); await load(); }}>
            <Trash2 size={14} />
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Notifications"
        actions={
          <Button variant="outline"
            onClick={async () => { await notificationApi.readAll(); await load(); }}>
            Mark all read
          </Button>
        } />
      <Card>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(n) => n.notificationId} onPageChange={setPage}
          emptyTitle="All caught up"
          emptyDescription="Booking confirmations, payments and announcements arrive here." />
      </Card>
    </>
  );
}
