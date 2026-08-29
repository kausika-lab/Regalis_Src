import { useCallback, useEffect, useState } from 'react';
import { LogIn, LogOut, Timer } from 'lucide-react';
import { attendanceApi, ATTENDANCE_STATUS_LABELS, type AttendanceRow } from '@/api/access';
import { ApiClientError } from '@/api/client';
import { Badge, Button, Card, DataTable, PageHeader, type Column } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { PagedResult } from '@/models/api';
import { formatDate, formatDateTime, formatTime } from '@/utils/formatters';

export function WorkStatusPage() {
  const toast = useToast();
  const [open, setOpen] = useState<AttendanceRow | null>(null);
  const [history, setHistory] = useState<PagedResult<AttendanceRow> | null>(null);
  const [page, setPage] = useState(1);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      const [openRow, historyPage] = await Promise.all([
        attendanceApi.myOpen(),
        attendanceApi.my({ page, pageSize: 15 }),
      ]);
      setOpen(openRow);
      setHistory(historyPage);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load work status.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  const act = async (action: () => Promise<unknown>) => {
    setBusy(true);
    try {
      await action();
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'The action failed.');
    } finally {
      setBusy(false);
    }
  };

  const columns: Column<AttendanceRow>[] = [
    { key: 'date', header: 'Date', render: (a) => formatDate(a.attendanceDate) },
    { key: 'in', header: 'In (IST)', render: (a) => formatTime(a.checkInUtc) },
    { key: 'out', header: 'Out (IST)', render: (a) => a.checkOutUtc ? formatTime(a.checkOutUtc) : '—' },
    {
      key: 'minutes', header: 'Duration',
      render: (a) => a.totalMinutes !== null
        ? `${Math.floor(a.totalMinutes / 60)}h ${a.totalMinutes % 60}m`
        : '—',
    },
    {
      key: 'status', header: 'Status',
      render: (a) => (
        <Badge tone={a.status === 1 ? 'info' : a.status === 3 ? 'warning' : 'success'}>
          {ATTENDANCE_STATUS_LABELS[a.status]}
        </Badge>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="My Work Status"
        description="Check in when you arrive and out when you leave — the QR gate does this automatically too." />
      <Card>
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-6)', flexWrap: 'wrap' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
            <Timer size={32} color={open ? 'var(--color-success)' : 'var(--text-muted)'} />
            <div>
              <div style={{ fontWeight: 700 }}>
                {open ? 'Checked in' : 'Not checked in'}
              </div>
              <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-sm)' }}>
                {open ? `Since ${formatDateTime(open.checkInUtc)} IST` : 'Have a booking or a pass? Check in below.'}
              </div>
            </div>
          </div>
          {open ? (
            <Button variant="danger" loading={busy} onClick={() => act(() => attendanceApi.checkOut())}>
              <LogOut size={16} /> Check out
            </Button>
          ) : (
            <Button loading={busy} onClick={() => act(() => attendanceApi.checkIn())}>
              <LogIn size={16} /> Check in
            </Button>
          )}
        </div>
      </Card>
      <Card title="History">
        <DataTable columns={columns} result={history} loading={history === null}
          rowKey={(a) => a.attendanceId} onPageChange={setPage}
          emptyTitle="No attendance yet" />
      </Card>
    </>
  );
}
