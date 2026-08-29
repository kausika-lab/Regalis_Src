import { useCallback, useEffect, useState } from 'react';
import { CheckCircle2, ScanLine, ShieldAlert, XCircle } from 'lucide-react';
import {
  accessApi,
  attendanceApi,
  ACCESS_RESULT_LABELS,
  ATTENDANCE_STATUS_LABELS,
  type AccessDecision,
  type AccessLogRow,
  type AttendanceRow,
} from '@/api/access';
import { usersApi } from '@/api/admin';
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
import { useDebounce } from '@/hooks/useDebounce';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { UserListItem } from '@/models/admin';
import { formatDate, formatDateTime, formatTime } from '@/utils/formatters';

// ── /admin/access — scan / validate console ─────────────────────────────────

export function AdminAccessScanPage() {
  const toast = useToast();
  const canOverride = usePermission('ACCESS.OVERRIDE');
  const [payload, setPayload] = useState('');
  const [direction, setDirection] = useState<1 | 2>(1);
  const [deviceId, setDeviceId] = useState('FRONT-DESK');
  const [decision, setDecision] = useState<AccessDecision | null>(null);
  const [busy, setBusy] = useState(false);
  const [overrideOpen, setOverrideOpen] = useState(false);

  const validate = async () => {
    setBusy(true);
    try {
      setDecision(await accessApi.validate(payload.trim(), direction, deviceId || undefined));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Validation failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title="Scan / Validate"
        description="Paste or scan a QR payload. Every attempt — granted or denied — is written to the access log."
        actions={canOverride && (
          <Button variant="outline" onClick={() => setOverrideOpen(true)}>
            <ShieldAlert size={16} /> Manual override
          </Button>
        )} />
      <Card>
        <div className="form-stack">
          <Input label="QR payload or token" required value={payload}
            placeholder="RGL1.…" onChange={(e) => setPayload(e.target.value)} />
          <div className="toolbar" style={{ marginBottom: 0 }}>
            <Select label="Direction" value={String(direction)}
              options={[{ value: 1, label: 'Entry (in)' }, { value: 2, label: 'Exit (out)' }]}
              onChange={(e) => setDirection(Number(e.target.value) as 1 | 2)} />
            <Input label="Device id" value={deviceId} hint="OFFLINE-* simulates a downed device"
              onChange={(e) => setDeviceId(e.target.value)} />
            <Button size="lg" onClick={validate} loading={busy} disabled={!payload.trim()}>
              <ScanLine size={18} /> Validate
            </Button>
          </div>

          {decision && (
            <div className="choice-card" style={{
              cursor: 'default',
              borderColor: decision.isGranted ? 'var(--color-success)' : 'var(--color-danger)',
            }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
                {decision.isGranted
                  ? <CheckCircle2 size={32} color="var(--color-success)" />
                  : <XCircle size={32} color="var(--color-danger)" />}
                <div>
                  <div className="choice-card__title">
                    {ACCESS_RESULT_LABELS[decision.result]}
                    {decision.denyReason && ` — ${decision.denyReason}`}
                  </div>
                  <div className="choice-card__meta">
                    {decision.personName ?? 'Unknown person'}
                    {decision.reference && ` · ${decision.reference}`}
                    {decision.validToUtc && ` · valid until ${formatDateTime(decision.validToUtc)} IST`}
                  </div>
                </div>
              </div>
            </div>
          )}
        </div>
      </Card>

      {overrideOpen && (
        <OverrideModal onClose={() => setOverrideOpen(false)}
          onDone={(result) => { setOverrideOpen(false); setDecision(result); }} />
      )}
    </>
  );
}

function OverrideModal({ onClose, onDone }: {
  onClose: () => void;
  onDone: (decision: AccessDecision) => void;
}) {
  const toast = useToast();
  const [search, setSearch] = useState('');
  const [people, setPeople] = useState<UserListItem[]>([]);
  const [userId, setUserId] = useState('');
  const [direction, setDirection] = useState<1 | 2>(1);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);
  const debouncedSearch = useDebounce(search, 350);

  useEffect(() => {
    if (!debouncedSearch) { setPeople([]); return; }
    usersApi.list({ page: 1, pageSize: 8, search: debouncedSearch })
      .then((result) => setPeople(result.items)).catch(() => {});
  }, [debouncedSearch]);

  const submit = async () => {
    setBusy(true);
    try {
      const decision = await accessApi.manualOverride({
        userId: Number(userId),
        locationId: 1,
        direction,
        reason: reason.trim(),
      });
      toast.warning('Override granted — the exception is on the audit trail.');
      onDone(decision);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Override failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title="Manual override (device down)" onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button variant="danger" onClick={submit} loading={busy}
            disabled={!userId || !reason.trim()}>
            Grant access
          </Button>
        </>
      }>
      <div className="form-stack">
        <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--text-secondary)' }}>
          Use only when the reader is down and you have verified the person's identity.
          Every override writes an exception log row and an audit entry with your name.
        </p>
        <Input label="Find person" value={search} placeholder="Name, email or mobile…"
          onChange={(e) => setSearch(e.target.value)} />
        {people.length > 0 && (
          <Select label="Person" required value={userId} placeholder="Select person"
            options={people.map((p) => ({ value: p.userId, label: `${p.fullName} · ${p.email}` }))}
            onChange={(e) => setUserId(e.target.value)} />
        )}
        <Select label="Direction" value={String(direction)}
          options={[{ value: 1, label: 'Entry (in)' }, { value: 2, label: 'Exit (out)' }]}
          onChange={(e) => setDirection(Number(e.target.value) as 1 | 2)} />
        <Input label="Reason" required value={reason}
          hint="Mandatory — e.g. 'Reader offline, identity verified by ID card'"
          onChange={(e) => setReason(e.target.value)} />
      </div>
    </Modal>
  );
}

// ── /admin/access/logs ──────────────────────────────────────────────────────

export function AdminAccessLogsPage() {
  const toast = useToast();
  const [page, setPage] = useState(1);
  const [resultFilter, setResultFilter] = useState('');
  const [rows, setRows] = useState<PagedResult<AccessLogRow> | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await accessApi.logs({
        page, pageSize: 20,
        result: resultFilter ? Number(resultFilter) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load access logs.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, resultFilter]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<AccessLogRow>[] = [
    { key: 'when', header: 'When (IST)', render: (l) => formatDateTime(l.loggedUtc) },
    { key: 'who', header: 'Person', render: (l) => l.personName ?? '—' },
    { key: 'direction', header: 'Dir', render: (l) => l.direction === 1 ? 'In' : 'Out' },
    {
      key: 'result', header: 'Result',
      render: (l) => (
        <div style={{ display: 'flex', gap: 'var(--space-1)', alignItems: 'center' }}>
          <Badge tone={l.result === 1 ? 'success' : l.result === 3 ? 'warning' : 'danger'}>
            {ACCESS_RESULT_LABELS[l.result]}
          </Badge>
          {l.isManualOverride && <Badge tone="warning">Override</Badge>}
        </div>
      ),
    },
    { key: 'reason', header: 'Deny reason', render: (l) => l.denyReason ?? l.overrideReason ?? '—' },
    { key: 'device', header: 'Device', render: (l) => l.deviceId ?? '—' },
  ];

  return (
    <>
      <PageHeader title="Access Logs"
        description="Append-only: every attempt at every gate, granted or denied." />
      <Card>
        <div className="toolbar">
          <Select label="Result" value={resultFilter} placeholder="All results"
            options={Object.entries(ACCESS_RESULT_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setResultFilter(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(l) => l.accessLogId} onPageChange={setPage} emptyTitle="No access events yet" />
      </Card>
    </>
  );
}

// ── /admin/attendance (+ payroll) ───────────────────────────────────────────

export function AdminAttendancePage() {
  const toast = useToast();
  const canEdit = usePermission('ATTENDANCE.EDIT');
  const [page, setPage] = useState(1);
  const [date, setDate] = useState('');
  const [rows, setRows] = useState<PagedResult<AttendanceRow> | null>(null);
  const [loading, setLoading] = useState(true);
  const [correcting, setCorrecting] = useState<AttendanceRow | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await attendanceApi.list({ page, pageSize: 20, date: date || undefined }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load attendance.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, date]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<AttendanceRow>[] = [
    {
      key: 'who', header: 'Person',
      render: (a) => (
        <div>
          <div style={{ fontWeight: 600 }}>{a.personName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {a.subjectType === 2 ? 'Staff' : 'Customer'}
          </div>
        </div>
      ),
    },
    { key: 'date', header: 'Date', render: (a) => formatDate(a.attendanceDate) },
    {
      key: 'in', header: 'In (IST)',
      render: (a) => (
        <span>
          {formatTime(a.checkInUtc)}
          {a.isCorrected && a.originalCheckInUtc && (
            <span style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
              {' '}(was {formatTime(a.originalCheckInUtc)})
            </span>
          )}
        </span>
      ),
    },
    { key: 'out', header: 'Out (IST)', render: (a) => a.checkOutUtc ? formatTime(a.checkOutUtc) : '—' },
    {
      key: 'duration', header: 'Duration',
      render: (a) => a.totalMinutes !== null
        ? `${Math.floor(a.totalMinutes / 60)}h ${a.totalMinutes % 60}m`
        : '—',
    },
    {
      key: 'status', header: 'Status',
      render: (a) => (
        <div style={{ display: 'flex', gap: 'var(--space-1)' }}>
          <Badge tone={a.status === 1 ? 'info' : a.status === 3 ? 'warning' : 'success'}>
            {ATTENDANCE_STATUS_LABELS[a.status]}
          </Badge>
          {a.isCorrected && <Badge tone="warning">Corrected</Badge>}
        </div>
      ),
    },
    {
      key: 'actions', header: '',
      render: (a) => canEdit ? (
        <Button size="sm" variant="outline" onClick={() => setCorrecting(a)}>Correct</Button>
      ) : null,
    },
  ];

  return (
    <>
      <PageHeader title="Attendance"
        description="Corrections keep the original times — the payroll hand-off stays auditable." />
      <Card>
        <div className="toolbar">
          <Input label="Date" type="date" value={date}
            onChange={(e) => { setDate(e.target.value); setPage(1); }} />
          {date && <Button variant="ghost" onClick={() => setDate('')}>Clear</Button>}
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(a) => a.attendanceId} onPageChange={setPage} emptyTitle="No attendance records" />
      </Card>

      {correcting && (
        <CorrectionModal row={correcting} onClose={() => setCorrecting(null)}
          onDone={() => { setCorrecting(null); void load(); }} />
      )}
    </>
  );
}

function CorrectionModal({ row, onClose, onDone }: {
  row: AttendanceRow;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const toLocalInput = (utc: string) =>
    new Date(new Date(utc).getTime() + 5.5 * 3600_000).toISOString().slice(0, 16);
  const [checkIn, setCheckIn] = useState(toLocalInput(row.checkInUtc));
  const [checkOut, setCheckOut] = useState(row.checkOutUtc ? toLocalInput(row.checkOutUtc) : '');
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  const toUtcIso = (istLocal: string) =>
    new Date(new Date(`${istLocal}:00Z`).getTime() - 5.5 * 3600_000).toISOString();

  const submit = async () => {
    setBusy(true);
    try {
      await attendanceApi.correct(row.attendanceId, toUtcIso(checkIn),
        checkOut ? toUtcIso(checkOut) : null, reason.trim());
      toast.success('Corrected. Original times preserved.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Correction failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Correct — ${row.personName}, ${formatDate(row.attendanceDate)}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={submit} loading={busy} disabled={!reason.trim()}>Save correction</Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Check-in (IST)" type="datetime-local" value={checkIn}
            onChange={(e) => setCheckIn(e.target.value)} />
          <Input label="Check-out (IST)" type="datetime-local" value={checkOut}
            hint="Leave empty to reopen the session"
            onChange={(e) => setCheckOut(e.target.value)} />
        </div>
        <Input label="Reason" required value={reason} hint="Mandatory — kept with the record"
          onChange={(e) => setReason(e.target.value)} />
      </div>
    </Modal>
  );
}

// ── /admin/attendance/payroll ───────────────────────────────────────────────

export function AdminPayrollPage() {
  const toast = useToast();
  const today = new Date().toISOString().slice(0, 10);
  const monthStart = today.slice(0, 8) + '01';
  const [from, setFrom] = useState(monthStart);
  const [to, setTo] = useState(today);
  const [busy, setBusy] = useState(false);

  const download = async () => {
    setBusy(true);
    try {
      await attendanceApi.payrollExport(from, to);
      toast.success('Payroll CSV downloaded.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Export failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title="Payroll Export"
        description="The hand-off file for the payroll team (A03): attendance with IST times, corrections flagged." />
      <Card>
        <div className="toolbar" style={{ marginBottom: 0 }}>
          <Input label="From" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          <Input label="To" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          <Button size="lg" onClick={download} loading={busy}>Download CSV</Button>
        </div>
      </Card>
    </>
  );
}
