import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { BellRing, Check, Trash2 } from 'lucide-react';
import { notificationApi } from '@/api/finance';
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
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { AppNotification } from '@/models/finance';
import { NOTIFICATION_AUDIENCE_LABELS, NOTIFICATION_CATEGORY_LABELS } from '@/models/finance';
import { formatDateTime } from '@/utils/formatters';

/**
 * The admin-portal notification centre (MODULE_PLAN §3): the operator's own
 * inbox, plus the announcement composer behind NOTIFICATION.CREATE. Customers
 * get the same inbox at /notifications without the composer.
 */
export function AdminNotificationsPage() {
  const toast = useToast();
  const canBroadcast = usePermission('NOTIFICATION.CREATE');

  const [page, setPage] = useState(1);
  const [readFilter, setReadFilter] = useState('');
  const [result, setResult] = useState<PagedResult<AppNotification> | null>(null);
  const [loading, setLoading] = useState(true);
  const [composing, setComposing] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(
        await notificationApi.my({
          page,
          pageSize: 20,
          isRead: readFilter === '' ? undefined : readFilter === 'read',
        }),
      );
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load notifications.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, readFilter]);

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
      key: 'category',
      header: 'Category',
      width: '150px',
      render: (n) => <Badge tone="neutral">{NOTIFICATION_CATEGORY_LABELS[n.category] ?? 'Other'}</Badge>,
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
      <PageHeader
        title="Notifications"
        description="Alerts raised for your account, and announcements you send to the space."
        actions={
          <>
            <Button variant="outline"
              onClick={async () => { await notificationApi.readAll(); await load(); }}>
              Mark all read
            </Button>
            {canBroadcast && <Button onClick={() => setComposing(true)}>New announcement</Button>}
          </>
        }
      />
      <Card>
        <div className="toolbar">
          <Select label="State" value={readFilter} placeholder="All"
            options={[{ value: 'unread', label: 'Unread' }, { value: 'read', label: 'Read' }]}
            onChange={(e) => { setReadFilter(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(n) => n.notificationId} onPageChange={setPage}
          emptyTitle="All caught up"
          emptyDescription="Bookings, payments and tickets that need attention arrive here." />
      </Card>

      <BroadcastModal open={composing} onClose={() => setComposing(false)}
        onSent={() => { setComposing(false); void load(); }} />
    </>
  );
}

const broadcastSchema = z.object({
  // Selects bind strings; converted to numbers on submit.
  audience: z.enum(['2', '3']),
  category: z.string().min(1, 'Pick a category.'),
  title: z.string().min(1, 'A title is required.').max(200),
  message: z.string().min(1, 'A message is required.').max(2000),
  actionLabel: z.string().max(60).optional(),
  actionRoute: z.string().max(200).optional(),
});

type BroadcastForm = z.infer<typeof broadcastSchema>;

function BroadcastModal({ open, onClose, onSent }: {
  open: boolean;
  onClose: () => void;
  onSent: () => void;
}) {
  const toast = useToast();
  const {
    register,
    handleSubmit,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<BroadcastForm>({
    resolver: zodResolver(broadcastSchema),
    // Announcement (9) is the category an operator-authored notice belongs to.
    defaultValues: { audience: '2', category: '9' },
  });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const recipients = await notificationApi.broadcast({
        audience: Number(values.audience),
        category: Number(values.category),
        title: values.title,
        message: values.message,
        actionLabel: values.actionLabel || null,
        actionRoute: values.actionRoute || null,
      });
      toast.success(`Announcement sent to ${recipients} recipient(s).`);
      reset();
      onSent();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not send the announcement.');
    }
  });

  return (
    <Modal open={open} title="New announcement" onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={onSubmit} loading={isSubmitting}>Send</Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Select label="Audience" required error={errors.audience?.message}
            options={Object.entries(NOTIFICATION_AUDIENCE_LABELS).map(([value, label]) => ({ value, label }))}
            {...register('audience')} />
          <Select label="Category" required error={errors.category?.message}
            options={Object.entries(NOTIFICATION_CATEGORY_LABELS).map(([value, label]) => ({ value, label }))}
            {...register('category')} />
        </div>
        <Input label="Title" required error={errors.title?.message}
          placeholder="Lift maintenance this Saturday" {...register('title')} />
        <div className="field">
          <label className="field__label" htmlFor="broadcast-message">
            Message
            <span className="field__required" aria-hidden> *</span>
          </label>
          <textarea id="broadcast-message" rows={5}
            className={`field__input${errors.message ? ' field__input--error' : ''}`}
            aria-invalid={Boolean(errors.message)}
            placeholder="What members need to know, and when."
            {...register('message')} />
          {errors.message && (
            <span className="field__error" role="alert">{errors.message.message}</span>
          )}
        </div>
        <div className="form-grid">
          <Input label="Action label" error={errors.actionLabel?.message}
            hint="Optional link shown under the message" placeholder="View bookings"
            {...register('actionLabel')} />
          <Input label="Action route" error={errors.actionRoute?.message}
            hint="An in-app path, e.g. /bookings" placeholder="/bookings"
            {...register('actionRoute')} />
        </div>
      </div>
    </Modal>
  );
}
