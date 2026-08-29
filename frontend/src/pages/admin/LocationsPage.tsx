import { useCallback, useEffect, useState } from 'react';
import { locationsApi } from '@/api/masters';
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
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { Location, SaveLocationPayload } from '@/models/masters';
import { WEEK_DAYS } from '@/models/masters';
import type { PagedResult } from '@/models/api';

export function LocationsPage() {
  const toast = useToast();
  const canCreate = usePermission('LOCATION.CREATE');
  const canEdit = usePermission('LOCATION.EDIT');
  const canDelete = usePermission('LOCATION.DELETE');

  const [result, setResult] = useState<PagedResult<Location> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Location | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Location | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await locationsApi.list({ page, pageSize: 20 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load locations.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Location>[] = [
    {
      key: 'name',
      header: 'Location',
      render: (l) => (
        <div>
          <div style={{ fontWeight: 600 }}>
            {l.locationName} {l.isDefault && <Badge tone="gold">Default</Badge>}
          </div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{l.locationCode}</div>
        </div>
      ),
    },
    { key: 'city', header: 'City', render: (l) => l.city ?? '—' },
    { key: 'hours', header: 'Working hours (IST)', render: (l) => `${l.workingHoursStart}–${l.workingHoursEnd}` },
    { key: 'days', header: 'Days', render: (l) => l.workingDays },
    { key: 'workspaces', header: 'Workspaces', render: (l) => l.workspaceCount },
    {
      key: 'actions',
      header: '',
      render: (l) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(l)}>
              Edit
            </Button>
          )}
          {canDelete && !l.isDefault && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(l)}>
              Delete
            </Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Locations"
        description="Working hours and days set the bookable window for every workspace at the location."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New location</Button>}
      />
      <Card>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(l) => l.locationId} onPageChange={setPage} />
      </Card>

      {editing && (
        <LocationEditorModal location={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete ${deleteTarget?.locationName ?? ''}?`}
        message="A location with workspaces cannot be deleted — move or delete them first."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await locationsApi.remove(deleteTarget.locationId);
            toast.success('Location deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

function LocationEditorModal({ location, onClose, onSaved }: {
  location: Location | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    locationCode: location?.locationCode ?? '',
    locationName: location?.locationName ?? '',
    addressLine1: location?.addressLine1 ?? '',
    addressLine2: location?.addressLine2 ?? '',
    city: location?.city ?? '',
    state: location?.state ?? '',
    postalCode: location?.postalCode ?? '',
    contactNumber: location?.contactNumber ?? '',
    email: location?.email ?? '',
    workingHoursStart: location?.workingHoursStart ?? '08:00',
    workingHoursEnd: location?.workingHoursEnd ?? '21:00',
    workingDays: location ? location.workingDays.split(',') : ['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT'],
    description: location?.description ?? '',
    isDefault: location?.isDefault ?? false,
  });
  const [busy, setBusy] = useState(false);

  const toggleDay = (day: string, on: boolean) =>
    setForm((f) => ({
      ...f,
      workingDays: on ? [...f.workingDays, day] : f.workingDays.filter((d) => d !== day),
    }));

  const save = async () => {
    setBusy(true);
    const payload: SaveLocationPayload = {
      ...form,
      locationCode: form.locationCode.trim().toUpperCase(),
      locationName: form.locationName.trim(),
      addressLine1: form.addressLine1 || null,
      addressLine2: form.addressLine2 || null,
      city: form.city || null,
      state: form.state || null,
      postalCode: form.postalCode || null,
      contactNumber: form.contactNumber || null,
      email: form.email || null,
      description: form.description || null,
    };
    try {
      if (location) {
        await locationsApi.update(location.locationId, payload);
      } else {
        await locationsApi.create(payload);
      }
      toast.success(location ? 'Location updated.' : 'Location created.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the location.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={location ? `Edit — ${location.locationName}` : 'New location'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.locationCode.trim() || !form.locationName.trim() || form.workingDays.length === 0}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Code" required value={form.locationCode} hint="e.g. REG-HQ"
            disabled={location !== null}
            onChange={(e) => setForm({ ...form, locationCode: e.target.value })} />
          <Input label="Name" required value={form.locationName}
            onChange={(e) => setForm({ ...form, locationName: e.target.value })} />
          <Input label="Address line 1" value={form.addressLine1}
            onChange={(e) => setForm({ ...form, addressLine1: e.target.value })} />
          <Input label="Address line 2" value={form.addressLine2}
            onChange={(e) => setForm({ ...form, addressLine2: e.target.value })} />
          <Input label="City" value={form.city} onChange={(e) => setForm({ ...form, city: e.target.value })} />
          <Input label="State" value={form.state} onChange={(e) => setForm({ ...form, state: e.target.value })} />
          <Input label="Postal code" value={form.postalCode}
            onChange={(e) => setForm({ ...form, postalCode: e.target.value })} />
          <Input label="Contact number" value={form.contactNumber}
            onChange={(e) => setForm({ ...form, contactNumber: e.target.value })} />
          <Input label="Email" type="email" value={form.email}
            onChange={(e) => setForm({ ...form, email: e.target.value })} />
          <Input label="Opens (IST)" type="time" required value={form.workingHoursStart}
            onChange={(e) => setForm({ ...form, workingHoursStart: e.target.value })} />
          <Input label="Closes (IST)" type="time" required value={form.workingHoursEnd}
            onChange={(e) => setForm({ ...form, workingHoursEnd: e.target.value })} />
        </div>
        <div className="field">
          <span className="field__label">Working days <span className="field__required">*</span></span>
          <div className="perm-matrix__actions">
            {WEEK_DAYS.map((day) => (
              <Checkbox key={day} label={day} checked={form.workingDays.includes(day)}
                onChange={(e) => toggleDay(day, e.target.checked)} />
            ))}
          </div>
        </div>
        <Checkbox label="Default location" checked={form.isDefault}
          onChange={(e) => setForm({ ...form, isDefault: e.target.checked })} />
      </div>
    </Modal>
  );
}
