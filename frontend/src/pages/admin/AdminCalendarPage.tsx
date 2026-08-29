import { useCallback, useEffect, useMemo, useState } from 'react';
import { calendarApi, type CalendarGrid, type CalendarItem } from '@/api/bookings';
import { locationsApi, workspaceTypesApi } from '@/api/masters';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  Input,
  Modal,
  PageHeader,
  Select,
  Spinner,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { Location, WorkspaceType } from '@/models/masters';
import { BOOKING_STATUS_LABELS } from '@/models/bookings';
import { formatDateTime } from '@/utils/formatters';

const BLOCK_REASONS: Record<number, string> = {
  1: 'Maintenance',
  2: 'Internal use',
  3: 'Cleaning',
  4: 'Repair',
  5: 'Private event',
  6: 'Housekeeping',
  7: 'Other',
};

/** Timeline window rendered for the day grid (IST). */
const DAY_START_HOUR = 7;
const DAY_END_HOUR = 22;
const HOURS = Array.from({ length: DAY_END_HOUR - DAY_START_HOUR }, (_, i) => DAY_START_HOUR + i);

/** The single calendar (Invariant 1): the same data source the customers book
 * against, rendered as a per-workspace day timeline with block/unblock. */
export function AdminCalendarPage() {
  const toast = useToast();
  const canBlock = usePermission('CALENDAR.CREATE');
  const canUnblock = usePermission('CALENDAR.EDIT');

  const [locations, setLocations] = useState<Location[]>([]);
  const [types, setTypes] = useState<WorkspaceType[]>([]);
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [locationId, setLocationId] = useState('');
  const [typeId, setTypeId] = useState('');
  const [grid, setGrid] = useState<CalendarGrid | null>(null);
  const [loading, setLoading] = useState(true);
  const [detail, setDetail] = useState<CalendarItem | null>(null);
  const [blockOpen, setBlockOpen] = useState(false);

  useEffect(() => {
    Promise.all([locationsApi.list({ pageSize: 100 }), workspaceTypesApi.list()])
      .then(([locationPage, typeList]) => {
        setLocations(locationPage.items);
        setTypes(typeList);
        const preferred = locationPage.items.find((l) => l.isDefault) ?? locationPage.items[0];
        if (preferred) {
          setLocationId(String(preferred.locationId));
        }
      })
      .catch(() => toast.error('Failed to load reference data.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const load = useCallback(async () => {
    if (!locationId) {
      return;
    }
    setLoading(true);
    try {
      setGrid(await calendarApi.get('day', date, {
        locationId: Number(locationId),
        workspaceTypeId: typeId ? Number(typeId) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the calendar.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [date, locationId, typeId]);

  useEffect(() => {
    void load();
  }, [load]);

  /** Position an item on the IST timeline as percentage offsets. */
  const positionOf = useMemo(() => {
    const windowMinutes = (DAY_END_HOUR - DAY_START_HOUR) * 60;
    return (item: CalendarItem) => {
      const startIst = new Date(new Date(item.startUtc).getTime() + 5.5 * 3600_000);
      const endIst = new Date(new Date(item.endUtc).getTime() + 5.5 * 3600_000);
      const dayAnchor = Date.UTC(startIst.getUTCFullYear(), startIst.getUTCMonth(),
        startIst.getUTCDate(), DAY_START_HOUR);
      const startMin = Math.max(0, (startIst.getTime() - dayAnchor) / 60000);
      const endMin = Math.min(windowMinutes, (endIst.getTime() - dayAnchor) / 60000);
      return {
        left: `${(startMin / windowMinutes) * 100}%`,
        width: `${Math.max(1.5, ((endMin - startMin) / windowMinutes) * 100)}%`,
      };
    };
  }, []);

  return (
    <>
      <PageHeader title="Calendar"
        description="One calendar for both portals — a block here removes the slot from customer availability instantly."
        actions={canBlock && <Button onClick={() => setBlockOpen(true)}>Block workspace</Button>} />
      <Card>
        <div className="toolbar">
          <Input label="Date" type="date" value={date} onChange={(e) => setDate(e.target.value)} />
          <Select label="Location" value={locationId}
            options={locations.map((l) => ({ value: l.locationId, label: l.locationName }))}
            onChange={(e) => setLocationId(e.target.value)} />
          <Select label="Type" value={typeId} placeholder="All types"
            options={types.map((t) => ({ value: t.workspaceTypeId, label: t.typeName }))}
            onChange={(e) => setTypeId(e.target.value)} />
        </div>

        {loading || !grid ? (
          <Spinner center />
        ) : (
          <div className="table-scroll">
            <div className="cal">
              <div className="cal__row cal__row--head">
                <div className="cal__label" />
                <div className="cal__timeline">
                  {HOURS.map((hour) => (
                    <span key={hour} className="cal__hour"
                      style={{ left: `${((hour - DAY_START_HOUR) / (DAY_END_HOUR - DAY_START_HOUR)) * 100}%` }}>
                      {hour}:00
                    </span>
                  ))}
                </div>
              </div>
              {grid.workspaces.map((workspace) => (
                <div key={workspace.workspaceId} className="cal__row">
                  <div className="cal__label">
                    <div style={{ fontWeight: 600 }}>{workspace.workspaceCode}</div>
                    <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                      {workspace.workspaceName}
                    </div>
                  </div>
                  <div className="cal__timeline">
                    {HOURS.map((hour) => (
                      <span key={hour} className="cal__grid-line"
                        style={{ left: `${((hour - DAY_START_HOUR) / (DAY_END_HOUR - DAY_START_HOUR)) * 100}%` }} />
                    ))}
                    {workspace.items.map((item) => (
                      <button key={`${item.kind}-${item.referenceId}`} type="button"
                        className={`cal__item cal__item--${item.kind}${item.bookingStatus === 1 ? ' cal__item--pending' : ''}`}
                        style={positionOf(item)}
                        title={item.title}
                        onClick={() => setDetail(item)}>
                        {item.title}
                      </button>
                    ))}
                  </div>
                </div>
              ))}
              {grid.workspaces.length === 0 && (
                <p style={{ padding: 'var(--space-6)', color: 'var(--text-muted)' }}>
                  No workspaces match the filters.
                </p>
              )}
            </div>
          </div>
        )}
      </Card>

      <Modal open={detail !== null}
        title={detail?.kind === 'block' ? 'Workspace block' : 'Booking'}
        onClose={() => setDetail(null)}
        footer={detail?.kind === 'block' && canUnblock ? (
          <Button variant="danger" onClick={async () => {
            try {
              await calendarApi.unblock(detail.referenceId);
              toast.success('Block released. The slot is bookable again.');
              setDetail(null);
              await load();
            } catch (error) {
              toast.error(error instanceof ApiClientError ? error.message : 'Unblock failed.');
            }
          }}>
            Release block
          </Button>
        ) : undefined}>
        {detail && (
          <div className="summary-rows">
            <div><span>What</span><strong>{detail.title}</strong></div>
            <div><span>From</span><span>{formatDateTime(detail.startUtc)}</span></div>
            <div><span>To</span><span>{formatDateTime(detail.endUtc)}</span></div>
            {detail.bookingStatus !== null && (
              <div><span>Status</span>
                <Badge tone="info">{BOOKING_STATUS_LABELS[detail.bookingStatus]}</Badge>
              </div>
            )}
            {detail.blockReason !== null && (
              <div><span>Reason</span><span>{BLOCK_REASONS[detail.blockReason]}</span></div>
            )}
          </div>
        )}
      </Modal>

      {blockOpen && grid && (
        <BlockModal date={date} workspaces={grid.workspaces}
          onClose={() => setBlockOpen(false)}
          onDone={() => { setBlockOpen(false); void load(); }} />
      )}
    </>
  );
}

function BlockModal({ date, workspaces, onClose, onDone }: {
  date: string;
  workspaces: { workspaceId: number; workspaceCode: string; workspaceName: string }[];
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [workspaceId, setWorkspaceId] = useState('');
  const [from, setFrom] = useState('09:00');
  const [to, setTo] = useState('12:00');
  const [reason, setReason] = useState('1');
  const [notes, setNotes] = useState('');
  const [busy, setBusy] = useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      // IST wall-clock → UTC.
      const startUtc = new Date(new Date(`${date}T${from}:00Z`).getTime() - 5.5 * 3600_000).toISOString();
      const endUtc = new Date(new Date(`${date}T${to}:00Z`).getTime() - 5.5 * 3600_000).toISOString();
      await calendarApi.block({
        workspaceId: Number(workspaceId),
        startUtc,
        endUtc,
        blockReason: Number(reason),
        reasonNotes: notes || null,
      });
      toast.success('Workspace blocked. Customer availability updated immediately.');
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError
        ? error.message
        : 'Block failed — check for overlapping bookings.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Block workspace · ${date}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={submit} loading={busy} disabled={!workspaceId}>Block</Button>
        </>
      }>
      <div className="form-stack">
        <Select label="Workspace" required value={workspaceId} placeholder="Select workspace"
          options={workspaces.map((w) => ({
            value: w.workspaceId,
            label: `${w.workspaceName} (${w.workspaceCode})`,
          }))}
          onChange={(e) => setWorkspaceId(e.target.value)} />
        <div className="form-grid">
          <Input label="From (IST)" type="time" value={from} onChange={(e) => setFrom(e.target.value)} />
          <Input label="To (IST)" type="time" value={to} onChange={(e) => setTo(e.target.value)} />
          <Select label="Reason" value={reason}
            options={Object.entries(BLOCK_REASONS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => setReason(e.target.value)} />
        </div>
        <Input label="Notes" value={notes} onChange={(e) => setNotes(e.target.value)} />
        <p style={{ fontSize: 'var(--font-size-xs)', color: 'var(--text-muted)' }}>
          A window with confirmed bookings cannot be blocked — cancel or reschedule them first.
        </p>
      </div>
    </Modal>
  );
}
