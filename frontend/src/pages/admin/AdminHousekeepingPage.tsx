import { useCallback, useEffect, useState } from 'react';
import { housekeepingApi } from '@/api/operations';
import { usersApi } from '@/api/admin';
import { workspacesApi } from '@/api/masters';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
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
import type { UserListItem } from '@/models/admin';
import type { Workspace } from '@/models/masters';
import {
  HOUSEKEEPING_STATUS_LABELS,
  HOUSEKEEPING_TASK_LABELS,
  type HousekeepingSchedule,
  type SaveHousekeepingPayload,
} from '@/models/operations';
import { formatDateTime, formatTime } from '@/utils/formatters';

const STATUS_TONES: Record<number, 'info' | 'warning' | 'success' | 'danger' | 'neutral'> = {
  1: 'info',
  2: 'warning',
  3: 'success',
  4: 'neutral',
  5: 'danger',
};

export function AdminHousekeepingPage() {
  const toast = useToast();
  const canCreate = usePermission('HOUSEKEEPING.CREATE');
  const canEdit = usePermission('HOUSEKEEPING.EDIT');

  const [rows, setRows] = useState<PagedResult<HousekeepingSchedule> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [editing, setEditing] = useState<HousekeepingSchedule | 'new' | null>(null);
  const [cancelling, setCancelling] = useState<HousekeepingSchedule | null>(null);
  const [completing, setCompleting] = useState<HousekeepingSchedule | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await housekeepingApi.list({
        page, pageSize: 20,
        status: statusFilter ? Number(statusFilter) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load schedules.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, statusFilter]);

  useEffect(() => { void load(); }, [load]);

  const start = async (schedule: HousekeepingSchedule) => {
    try {
      await housekeepingApi.checkIn(schedule.housekeepingScheduleId);
      toast.success('Task started.');
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not start the task.');
    }
  };

  const columns: Column<HousekeepingSchedule>[] = [
    {
      key: 'number', header: 'Schedule',
      render: (h) => (
        <div>
          <div style={{ fontWeight: 600 }}>{h.scheduleNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {HOUSEKEEPING_TASK_LABELS[h.taskType]} · {h.taskDescription}
          </div>
        </div>
      ),
    },
    { key: 'staff', header: 'Staff', render: (h) => h.staffName },
    {
      key: 'where', header: 'Where',
      render: (h) => h.workspaceName ?? h.areaName ?? '—',
    },
    {
      key: 'window', header: 'Window (IST)',
      render: (h) => `${formatDateTime(h.startUtc)} – ${formatTime(h.endUtc)}`,
    },
    {
      key: 'status', header: 'Status',
      render: (h) => (
        <div style={{ display: 'flex', gap: 'var(--space-1)', flexWrap: 'wrap' }}>
          <Badge tone={STATUS_TONES[h.status]}>{HOUSEKEEPING_STATUS_LABELS[h.status]}</Badge>
          {h.blocksWorkspace && h.status === 1 && <Badge tone="warning">Blocks calendar</Badge>}
        </div>
      ),
    },
    {
      key: 'actions', header: '',
      render: (h) => canEdit ? (
        <div className="row-actions">
          {h.status === 1 && (
            <>
              <Button size="sm" variant="outline" onClick={() => setEditing(h)}>Edit</Button>
              <Button size="sm" variant="outline" onClick={() => start(h)}>Start</Button>
            </>
          )}
          {h.status === 2 && (
            <Button size="sm" variant="outline" onClick={() => setCompleting(h)}>Complete</Button>
          )}
          {(h.status === 1 || h.status === 2) && (
            <Button size="sm" variant="ghost" onClick={() => setCancelling(h)}>Cancel</Button>
          )}
        </div>
      ) : null,
    },
  ];

  return (
    <>
      <PageHeader title="Housekeeping"
        description="A schedule that blocks a workspace removes the slot from customer availability; the staff QR opens the door only inside the window."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New schedule</Button>} />
      <Card>
        <div className="toolbar">
          <Select label="Status" value={statusFilter} placeholder="All statuses"
            options={Object.entries(HOUSEKEEPING_STATUS_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(h) => h.housekeepingScheduleId} onPageChange={setPage}
          emptyTitle="No schedules yet" />
      </Card>

      {editing && (
        <ScheduleEditorModal schedule={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      {completing && (
        <CompleteModal schedule={completing} onClose={() => setCompleting(null)}
          onDone={() => { setCompleting(null); void load(); }} />
      )}

      <ConfirmDialog open={cancelling !== null} danger confirmLabel="Cancel schedule"
        title={`Cancel ${cancelling?.scheduleNumber ?? ''}?`}
        message="The calendar block is released and the staff access QR is revoked."
        onConfirm={async () => {
          if (!cancelling) return;
          try {
            await housekeepingApi.cancel(cancelling.housekeepingScheduleId);
            toast.success('Schedule cancelled and the workspace released.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not cancel.');
          }
        }}
        onClose={() => setCancelling(null)} />
    </>
  );
}

/** datetime-local ⇄ UTC ISO, rendered in IST (UTC+5:30). */
const toLocalInput = (utc: string) =>
  new Date(new Date(utc).getTime() + 5.5 * 3600_000).toISOString().slice(0, 16);
const toUtcIso = (istLocal: string) =>
  new Date(new Date(`${istLocal}:00Z`).getTime() - 5.5 * 3600_000).toISOString();

function ScheduleEditorModal({ schedule, onClose, onSaved }: {
  schedule: HousekeepingSchedule | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [staff, setStaff] = useState<UserListItem[]>([]);
  const [workspaces, setWorkspaces] = useState<Workspace[]>([]);
  const [form, setForm] = useState({
    staffUserId: schedule ? String(schedule.staffUserId) : '',
    workspaceId: schedule?.workspaceId ? String(schedule.workspaceId) : '',
    areaName: schedule?.areaName ?? '',
    start: schedule ? toLocalInput(schedule.startUtc) : '',
    end: schedule ? toLocalInput(schedule.endUtc) : '',
    taskType: schedule ? String(schedule.taskType) : '3',
    taskDescription: schedule?.taskDescription ?? '',
    notes: schedule?.notes ?? '',
    blocksWorkspace: schedule?.blocksWorkspace ?? true,
  });
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    usersApi.list({ page: 1, pageSize: 100, userType: 3 })
      .then((result) => setStaff(result.items)).catch(() => {});
    workspacesApi.list({ page: 1, pageSize: 100 })
      .then((result) => setWorkspaces(result.items)).catch(() => {});
  }, []);

  const save = async () => {
    setBusy(true);
    const workspace = workspaces.find((w) => String(w.workspaceId) === form.workspaceId);
    const payload: SaveHousekeepingPayload = {
      staffUserId: Number(form.staffUserId),
      locationId: workspace?.locationId ?? 1,
      workspaceId: form.workspaceId ? Number(form.workspaceId) : null,
      areaName: form.areaName.trim() || null,
      startUtc: toUtcIso(form.start),
      endUtc: toUtcIso(form.end),
      taskType: Number(form.taskType),
      taskDescription: form.taskDescription.trim(),
      notes: form.notes.trim() || null,
      blocksWorkspace: form.blocksWorkspace,
    };
    try {
      if (schedule) {
        await housekeepingApi.update(schedule.housekeepingScheduleId, payload);
        toast.success('Schedule updated — block and QR follow the new window.');
      } else {
        await housekeepingApi.create(payload);
        toast.success('Schedule created. The staff QR is valid for this window.');
      }
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the schedule.');
    } finally {
      setBusy(false);
    }
  };

  const valid = form.staffUserId && form.start && form.end && form.taskDescription.trim() &&
    (!form.blocksWorkspace || form.workspaceId);

  return (
    <Modal open title={schedule ? `Edit — ${schedule.scheduleNumber}` : 'New schedule'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy} disabled={!valid}>Save</Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Select label="Staff" required value={form.staffUserId} placeholder="Select staff"
            options={staff.map((s) => ({ value: s.userId, label: `${s.fullName} · ${s.email}` }))}
            onChange={(e) => setForm({ ...form, staffUserId: e.target.value })} />
          <Select label="Task type" value={form.taskType}
            options={Object.entries(HOUSEKEEPING_TASK_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => setForm({ ...form, taskType: e.target.value })} />
          <Input label="Starts (IST)" type="datetime-local" required value={form.start}
            onChange={(e) => setForm({ ...form, start: e.target.value })} />
          <Input label="Ends (IST)" type="datetime-local" required value={form.end}
            onChange={(e) => setForm({ ...form, end: e.target.value })} />
          <Select label="Workspace" value={form.workspaceId}
            placeholder={form.blocksWorkspace ? 'Select workspace (required)' : 'None — common area'}
            options={workspaces.map((w) => ({
              value: w.workspaceId,
              label: `${w.workspaceCode} · ${w.workspaceName}`,
            }))}
            onChange={(e) => setForm({ ...form, workspaceId: e.target.value })} />
          <Input label="Area (if no workspace)" value={form.areaName}
            placeholder="e.g. Lobby, Pantry"
            onChange={(e) => setForm({ ...form, areaName: e.target.value })} />
        </div>
        <Input label="Task description" required value={form.taskDescription}
          onChange={(e) => setForm({ ...form, taskDescription: e.target.value })} />
        <Input label="Notes" value={form.notes}
          onChange={(e) => setForm({ ...form, notes: e.target.value })} />
        <Checkbox label="Block the workspace on the customer calendar for this window"
          checked={form.blocksWorkspace}
          onChange={(e) => setForm({ ...form, blocksWorkspace: e.target.checked })} />
      </div>
    </Modal>
  );
}

function CompleteModal({ schedule, onClose, onDone }: {
  schedule: HousekeepingSchedule;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      await housekeepingApi.checkOut(schedule.housekeepingScheduleId, notes.trim() || undefined);
      toast.success('Task completed. Any remaining block was released.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not complete the task.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Complete — ${schedule.scheduleNumber}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={submit} loading={busy}>Mark completed</Button>
        </>
      }>
      <div className="form-stack">
        <Input label="Completion notes" value={notes} hint="Optional — kept with the record"
          onChange={(e) => setNotes(e.target.value)} />
      </div>
    </Modal>
  );
}
