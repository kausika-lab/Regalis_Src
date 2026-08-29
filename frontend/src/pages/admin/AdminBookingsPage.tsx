import { useCallback, useEffect, useState } from 'react';
import { usersApi } from '@/api/admin';
import { availabilityApi, bookingsApi } from '@/api/bookings';
import { locationsApi, workspaceTypesApi } from '@/api/masters';
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
  Spinner,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useDebounce } from '@/hooks/useDebounce';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { UserListItem } from '@/models/admin';
import type { AvailableWorkspace, Booking } from '@/models/bookings';
import { BOOKING_STATUS_LABELS, BOOKING_STATUS_TONES, DURATION_OPTIONS } from '@/models/bookings';
import type { Location, WorkspaceType } from '@/models/masters';
import { formatDateTime, formatInr, formatTime } from '@/utils/formatters';

export function AdminBookingsPage() {
  const toast = useToast();
  const canCreate = usePermission('BOOKING.CREATE');

  const [result, setResult] = useState<PagedResult<Booking> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const [detail, setDetail] = useState<Booking | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const debouncedSearch = useDebounce(search, 350);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(
        await bookingsApi.list({
          page,
          pageSize: 20,
          search: debouncedSearch || undefined,
          status: status ? Number(status) : undefined,
        }),
      );
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load bookings.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, status]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Booking>[] = [
    {
      key: 'number',
      header: 'Booking',
      render: (b) => (
        <div>
          <div style={{ fontWeight: 600 }}>{b.bookingNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {b.customerName}
          </div>
        </div>
      ),
    },
    {
      key: 'workspace',
      header: 'Workspace',
      render: (b) => `${b.workspaceName} (${b.workspaceCode})`,
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
          {BOOKING_STATUS_LABELS[b.status] ?? b.status}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (b) => (
        <Button size="sm" variant="outline" onClick={() => setDetail(b)}>
          Detail
        </Button>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Bookings"
        description="Every booking in the system — the same engine and calendar the customers see."
        actions={canCreate && <Button onClick={() => setCreateOpen(true)}>New booking</Button>}
      />
      <Card>
        <div className="toolbar">
          <Input label="Search" placeholder="Booking no., customer…" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          <Select label="Status" value={status} placeholder="All statuses"
            options={Object.entries(BOOKING_STATUS_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(b) => b.bookingId} onPageChange={setPage}
          emptyTitle="No bookings match the filters" />
      </Card>

      <Modal open={detail !== null} title={detail?.bookingNumber ?? ''} onClose={() => setDetail(null)}>
        {detail && (
          <div className="summary-rows">
            <div><span>Customer</span><strong>{detail.customerName}</strong></div>
            <div><span>Workspace</span><strong>{detail.workspaceName} ({detail.workspaceCode})</strong></div>
            <div><span>Type</span><span>{detail.workspaceTypeName}</span></div>
            <div><span>When (IST)</span><strong>{formatDateTime(detail.startUtc)} – {formatTime(detail.endUtc)}</strong></div>
            <div><span>Duration</span><span>{detail.durationMinutes} minutes</span></div>
            <div><span>Base</span><span>{formatInr(detail.baseAmount)}</span></div>
            <div><span>Tax</span><span>{formatInr(detail.taxAmount)}</span></div>
            <div className="summary-total"><span>Total</span><span>{formatInr(detail.totalAmount)}</span></div>
            <div>
              <span>Status</span>
              <Badge tone={BOOKING_STATUS_TONES[detail.status] ?? 'neutral'}>
                {BOOKING_STATUS_LABELS[detail.status]}
              </Badge>
            </div>
            <div><span>Source</span><span>{detail.bookingSource === 2 ? 'Front desk' : detail.bookingSource === 3 ? 'Pass' : 'Customer portal'}</span></div>
            {detail.notes && <div><span>Notes</span><span>{detail.notes}</span></div>}
          </div>
        )}
      </Modal>

      {createOpen && (
        <AdminCreateBookingModal onClose={() => setCreateOpen(false)}
          onCreated={() => { setCreateOpen(false); void load(); }} />
      )}
    </>
  );
}

function AdminCreateBookingModal({ onClose, onCreated }: {
  onClose: () => void;
  onCreated: () => void;
}) {
  const toast = useToast();
  const [types, setTypes] = useState<WorkspaceType[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [customerSearch, setCustomerSearch] = useState('');
  const [customers, setCustomers] = useState<UserListItem[]>([]);
  const debouncedCustomer = useDebounce(customerSearch, 350);

  const [customerId, setCustomerId] = useState('');
  const [locationId, setLocationId] = useState('');
  const [typeId, setTypeId] = useState('');
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [slots, setSlots] = useState<{ startUtc: string; isAvailable: boolean }[]>([]);
  const [startUtc, setStartUtc] = useState('');
  const [duration, setDuration] = useState(60);
  const [workspaces, setWorkspaces] = useState<AvailableWorkspace[]>([]);
  const [workspaceId, setWorkspaceId] = useState('');
  const [paymentMethod, setPaymentMethod] = useState<'cash' | 'wallet'>('cash');
  const [busy, setBusy] = useState(false);
  const [loadingRefs, setLoadingRefs] = useState(true);

  useEffect(() => {
    Promise.all([workspaceTypesApi.list(), locationsApi.list({ pageSize: 100 })])
      .then(([typeList, locationPage]) => {
        setTypes(typeList);
        setLocations(locationPage.items);
        if (locationPage.items.length > 0) {
          setLocationId(String(locationPage.items[0].locationId));
        }
        if (typeList.length > 0) {
          setTypeId(String(typeList[0].workspaceTypeId));
        }
      })
      .finally(() => setLoadingRefs(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (!debouncedCustomer) {
      setCustomers([]);
      return;
    }
    usersApi.list({ page: 1, pageSize: 8, search: debouncedCustomer, userType: 1 })
      .then((page) => setCustomers(page.items))
      .catch(() => {});
  }, [debouncedCustomer]);

  useEffect(() => {
    if (!locationId || !typeId || !date) {
      return;
    }
    availabilityApi.slots(Number(locationId), Number(typeId), date)
      .then((all) => setSlots(all.map((s) => ({ startUtc: s.startUtc, isAvailable: s.isAvailable }))))
      .catch(() => setSlots([]));
    setStartUtc('');
    setWorkspaceId('');
  }, [locationId, typeId, date]);

  useEffect(() => {
    if (!startUtc) {
      setWorkspaces([]);
      return;
    }
    availabilityApi.workspaces(Number(locationId), Number(typeId), startUtc, duration)
      .then(setWorkspaces)
      .catch(() => setWorkspaces([]));
    setWorkspaceId('');
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [startUtc, duration]);

  const create = async () => {
    setBusy(true);
    try {
      const created = await bookingsApi.adminCreate({
        customerId: Number(customerId),
        workspaceId: Number(workspaceId),
        startUtc,
        durationMinutes: duration,
        paymentMethod,
      });
      toast.success(`Booking ${created.booking.bookingNumber} confirmed (${paymentMethod}).`);
      onCreated();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not create the booking.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title="New booking (front desk)" onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={create} loading={busy}
            disabled={!customerId || !workspaceId || !startUtc}>
            Confirm booking
          </Button>
        </>
      }>
      {loadingRefs ? (
        <Spinner center />
      ) : (
        <div className="form-stack">
          <Input label="Find customer" placeholder="Name, email or mobile…" value={customerSearch}
            onChange={(e) => setCustomerSearch(e.target.value)} />
          {customers.length > 0 && (
            <Select label="Customer" required value={customerId} placeholder="Select customer"
              options={customers.map((c) => ({ value: c.userId, label: `${c.fullName} · ${c.email}` }))}
              onChange={(e) => setCustomerId(e.target.value)} />
          )}
          <div className="form-grid">
            <Select label="Location" value={locationId}
              options={locations.map((l) => ({ value: l.locationId, label: l.locationName }))}
              onChange={(e) => setLocationId(e.target.value)} />
            <Select label="Type" value={typeId}
              options={types.map((t) => ({ value: t.workspaceTypeId, label: t.typeName }))}
              onChange={(e) => setTypeId(e.target.value)} />
            <Input label="Date" type="date" value={date}
              min={new Date().toISOString().slice(0, 10)}
              onChange={(e) => setDate(e.target.value)} />
            <Select label="Duration" value={String(duration)}
              options={DURATION_OPTIONS.map((m) => ({
                value: m,
                label: m < 60 ? `${m} min` : `${m / 60} h`,
              }))}
              onChange={(e) => setDuration(Number(e.target.value))} />
            <Select label="Start (IST)" required value={startUtc} placeholder="Select time"
              options={slots.filter((s) => s.isAvailable).map((s) => ({
                value: s.startUtc,
                label: formatTime(s.startUtc),
              }))}
              onChange={(e) => setStartUtc(e.target.value)} />
            <Select label="Workspace" required value={workspaceId} placeholder="Select workspace"
              options={workspaces.map((w) => ({
                value: w.workspaceId,
                label: `${w.workspaceName} (${w.workspaceCode}) · ${formatInr(w.pricePerHour)}/h`,
              }))}
              onChange={(e) => setWorkspaceId(e.target.value)} />
            <Select label="Settlement" value={paymentMethod}
              options={[{ value: 'cash', label: 'Cash at desk' }, { value: 'wallet', label: 'Customer wallet' }]}
              onChange={(e) => setPaymentMethod(e.target.value as 'cash' | 'wallet')} />
          </div>
        </div>
      )}
    </Modal>
  );
}
