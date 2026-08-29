import { useCallback, useEffect, useState } from 'react';
import { bookingsApi } from '@/api/bookings';
import { bookingMutationApi } from '@/api/finance';
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
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { PagedResult } from '@/models/api';
import type { Booking } from '@/models/bookings';
import { BOOKING_STATUS_LABELS, BOOKING_STATUS_TONES } from '@/models/bookings';
import { formatDateTime, formatInr, formatTime } from '@/utils/formatters';

type Tab = 'upcoming' | 'completed' | 'cancelled';

export function MyBookingsPage() {
  const toast = useToast();
  const [tab, setTab] = useState<Tab>('upcoming');
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<Booking> | null>(null);
  const [loading, setLoading] = useState(true);
  const [cancelTarget, setCancelTarget] = useState<Booking | null>(null);
  const [rescheduleTarget, setRescheduleTarget] = useState<Booking | null>(null);
  const [extendTarget, setExtendTarget] = useState<Booking | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await bookingsApi.my(tab, { page, pageSize: 20 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load bookings.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [tab, page]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Booking>[] = [
    {
      key: 'booking',
      header: 'Booking',
      render: (b) => (
        <div>
          <div style={{ fontWeight: 600 }}>{b.bookingNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {b.workspaceName} ({b.workspaceCode})
          </div>
        </div>
      ),
    },
    {
      key: 'when',
      header: 'When (IST)',
      render: (b) => `${formatDateTime(b.startUtc)} – ${formatTime(b.endUtc)}`,
    },
    { key: 'amount', header: 'Amount', render: (b) => formatInr(b.totalAmount) },
    {
      key: 'status',
      header: 'Status',
      render: (b) => (
        <Badge tone={BOOKING_STATUS_TONES[b.status] ?? 'neutral'}>
          {BOOKING_STATUS_LABELS[b.status]}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (b) =>
        tab === 'upcoming' && (b.status === 1 || b.status === 2 || b.status === 3) ? (
          <div className="row-actions">
            {b.status === 2 && (
              <>
                <Button size="sm" variant="outline" onClick={() => setRescheduleTarget(b)}>
                  Reschedule
                </Button>
                <Button size="sm" variant="outline" onClick={() => setExtendTarget(b)}>
                  Extend
                </Button>
              </>
            )}
            <Button size="sm" variant="ghost" onClick={() => setCancelTarget(b)}>
              Cancel
            </Button>
          </div>
        ) : null,
    },
  ];

  return (
    <>
      <PageHeader title="My Bookings"
        description="Cancellations before the 24-hour cutoff refund fully to your wallet." />
      <div className="wizard-steps">
        {(['upcoming', 'completed', 'cancelled'] as Tab[]).map((t) => (
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
          rowKey={(b) => b.bookingId} onPageChange={setPage}
          emptyTitle={`No ${tab} bookings`}
          emptyDescription={tab === 'upcoming' ? 'Book a workspace to see it here.' : undefined} />
      </Card>

      <ConfirmDialog open={cancelTarget !== null} danger confirmLabel="Cancel booking"
        title={`Cancel ${cancelTarget?.bookingNumber}?`}
        message="If the booking starts more than 24 hours from now the full amount is refunded to your wallet; inside 24 hours there is no refund."
        onConfirm={async () => {
          if (!cancelTarget) return;
          try {
            const result = await bookingMutationApi.cancel(cancelTarget.bookingId);
            toast.success(result.refundAmount > 0
              ? `Cancelled. ${formatInr(result.refundAmount)} refunded to your wallet.`
              : 'Cancelled. No refund applies inside the cutoff.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Cancellation failed.');
          }
        }}
        onClose={() => setCancelTarget(null)} />

      {rescheduleTarget && (
        <RescheduleModal booking={rescheduleTarget}
          onClose={() => setRescheduleTarget(null)}
          onDone={() => { setRescheduleTarget(null); void load(); }} />
      )}
      {extendTarget && (
        <ExtendModal booking={extendTarget}
          onClose={() => setExtendTarget(null)}
          onDone={() => { setExtendTarget(null); void load(); }} />
      )}
    </>
  );
}

function RescheduleModal({ booking, onClose, onDone }: {
  booking: Booking;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  // Prefill with the current IST start.
  const startIst = new Date(new Date(booking.startUtc + 'Z').getTime() + 5.5 * 3600_000);
  const [date, setDate] = useState(startIst.toISOString().slice(0, 10));
  const [time, setTime] = useState(startIst.toISOString().slice(11, 16));
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      // IST wall-clock → UTC ISO.
      const newStartUtc = new Date(new Date(`${date}T${time}:00Z`).getTime() - 5.5 * 3600_000)
        .toISOString().replace('.000Z', 'Z');
      await bookingMutationApi.reschedule(booking.bookingId, newStartUtc);
      toast.success('Booking rescheduled. Your QR pass follows the new time.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Reschedule failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Reschedule ${booking.bookingNumber}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Close</Button>
          <Button onClick={submit} loading={busy}>Reschedule</Button>
        </>
      }>
      <div className="form-stack">
        <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--text-secondary)' }}>
          Same workspace, same duration ({booking.durationMinutes} min), same price — just a
          new time. Slots align to 30 minutes (IST).
        </p>
        <div className="form-grid">
          <Input label="New date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          <Input label="New start (IST)" type="time" step={1800} value={time}
            onChange={(e) => setTime(e.target.value)} />
        </div>
      </div>
    </Modal>
  );
}

function ExtendModal({ booking, onClose, onDone }: {
  booking: Booking;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [minutes, setMinutes] = useState(30);
  const [busy, setBusy] = useState(false);
  const perHour = booking.durationMinutes > 0
    ? booking.baseAmount / (booking.durationMinutes / 60)
    : 0;
  const estimate = (perHour * minutes) / 60 * 1.18;

  const submit = async () => {
    setBusy(true);
    try {
      const result = await bookingMutationApi.extend(booking.bookingId, minutes);
      toast.success(`Extended. ${formatInr(result.additionalAmount)} charged to your wallet ` +
        `(invoice ${result.invoiceNumber}).`);
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Extension failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Extend ${booking.bookingNumber}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Close</Button>
          <Button onClick={submit} loading={busy}>Extend & pay from wallet</Button>
        </>
      }>
      <div className="form-stack">
        <Select label="Extra time" value={String(minutes)}
          options={[30, 60, 90, 120].map((m) => ({ value: m, label: `${m} minutes` }))}
          onChange={(e) => setMinutes(Number(e.target.value))} />
        <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--text-secondary)' }}>
          Estimated charge ≈ {formatInr(Math.round(estimate * 100) / 100)} (incl. GST), debited
          from your wallet if the slot after your booking is free.
        </p>
      </div>
    </Modal>
  );
}
