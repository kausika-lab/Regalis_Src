import { useCallback, useEffect, useState } from 'react';
import { amenitiesApi, locationsApi, workspaceTypesApi, workspacesApi } from '@/api/masters';
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
import { useDebounce } from '@/hooks/useDebounce';
import { usePermission } from '@/hooks/usePermission';
import type { Amenity, Location, SaveWorkspacePayload, Workspace, WorkspaceType } from '@/models/masters';
import { WORKSPACE_STATUS_LABELS } from '@/models/masters';
import type { PagedResult } from '@/models/api';
import { formatInr } from '@/utils/formatters';

const statusTone: Record<number, 'success' | 'info' | 'warning' | 'neutral'> = {
  1: 'success',
  2: 'info',
  3: 'warning',
  4: 'neutral',
};

/** One screen serves /admin/workspaces and the Desks/Cabins/Meeting Rooms
 * sub-menus — those arrive with the type pre-filtered (typeCode prop). */
export function WorkspacesPage({ typeCode }: { typeCode?: string }) {
  const toast = useToast();
  const canCreate = usePermission('WORKSPACE.CREATE');
  const canEdit = usePermission('WORKSPACE.EDIT');
  const canDelete = usePermission('WORKSPACE.DELETE');

  const [types, setTypes] = useState<WorkspaceType[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [amenities, setAmenities] = useState<Amenity[]>([]);
  const [result, setResult] = useState<PagedResult<Workspace> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [locationFilter, setLocationFilter] = useState('');
  const [typeFilter, setTypeFilter] = useState('');
  const debouncedSearch = useDebounce(search, 350);

  const [editing, setEditing] = useState<Workspace | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Workspace | null>(null);

  const fixedType = typeCode ? types.find((t) => t.typeCode === typeCode) : undefined;

  useEffect(() => {
    Promise.all([workspaceTypesApi.list(), locationsApi.list({ pageSize: 100 }), amenitiesApi.list()])
      .then(([typeList, locationPage, amenityList]) => {
        setTypes(typeList);
        setLocations(locationPage.items);
        setAmenities(amenityList);
      })
      .catch(() => toast.error('Failed to load reference data.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const load = useCallback(async () => {
    if (typeCode && !fixedType) {
      return; // reference data still loading
    }
    setLoading(true);
    try {
      setResult(
        await workspacesApi.list({
          page,
          pageSize: 20,
          search: debouncedSearch || undefined,
          locationId: locationFilter ? Number(locationFilter) : undefined,
          workspaceTypeId: fixedType?.workspaceTypeId ?? (typeFilter ? Number(typeFilter) : undefined),
        }),
      );
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load workspaces.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, locationFilter, typeFilter, fixedType?.workspaceTypeId, typeCode]);

  useEffect(() => {
    void load();
  }, [load]);

  const cycleStatus = async (workspace: Workspace, status: number) => {
    try {
      await workspacesApi.updateStatus(workspace.workspaceId, status);
      toast.success('Status updated.');
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not update the status.');
    }
  };

  const columns: Column<Workspace>[] = [
    {
      key: 'name',
      header: 'Workspace',
      render: (w) => (
        <div>
          <div style={{ fontWeight: 600 }}>{w.workspaceName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {w.workspaceCode} · {w.workspaceTypeName} · {w.locationName}
          </div>
        </div>
      ),
    },
    { key: 'capacity', header: 'Capacity', render: (w) => w.capacity },
    { key: 'price', header: 'Price / hour', render: (w) => formatInr(w.pricePerHour) },
    {
      key: 'status',
      header: 'Status',
      render: (w) => (
        <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center' }}>
          <Badge tone={statusTone[w.status] ?? 'neutral'}>{WORKSPACE_STATUS_LABELS[w.status]}</Badge>
          {!w.isBookable && <Badge tone="neutral">Not bookable</Badge>}
        </div>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (w) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(w)}>
              Edit
            </Button>
          )}
          {canEdit && w.status !== 3 && (
            <Button size="sm" variant="ghost" onClick={() => cycleStatus(w, 3)}>
              Maintenance
            </Button>
          )}
          {canEdit && w.status !== 1 && (
            <Button size="sm" variant="ghost" onClick={() => cycleStatus(w, 1)}>
              Available
            </Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(w)}>
              Delete
            </Button>
          )}
        </div>
      ),
    },
  ];

  const title = fixedType ? `${fixedType.typeName}s` : 'Workspaces';

  return (
    <>
      <PageHeader
        title={title}
        description="Bookable inventory. Maintenance status removes a workspace from customer availability immediately."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New workspace</Button>}
      />
      <Card>
        <div className="toolbar">
          <Input label="Search" placeholder="Name or code…" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          <Select label="Location" value={locationFilter} placeholder="All locations"
            options={locations.map((l) => ({ value: l.locationId, label: l.locationName }))}
            onChange={(e) => { setLocationFilter(e.target.value); setPage(1); }} />
          {!fixedType && (
            <Select label="Type" value={typeFilter} placeholder="All types"
              options={types.map((t) => ({ value: t.workspaceTypeId, label: t.typeName }))}
              onChange={(e) => { setTypeFilter(e.target.value); setPage(1); }} />
          )}
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(w) => w.workspaceId} onPageChange={setPage}
          emptyTitle="No workspaces yet"
          emptyDescription="Create desks, cabins and meeting rooms to open bookings." />
      </Card>

      {editing && (
        <WorkspaceEditorModal workspace={editing === 'new' ? null : editing}
          types={types} locations={locations} amenities={amenities}
          defaultTypeId={fixedType?.workspaceTypeId}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete ${deleteTarget?.workspaceName ?? ''}?`}
        message="A workspace with upcoming bookings cannot be deleted. Booking history is preserved."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await workspacesApi.remove(deleteTarget.workspaceId);
            toast.success('Workspace deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

function WorkspaceEditorModal({ workspace, types, locations, amenities, defaultTypeId, onClose, onSaved }: {
  workspace: Workspace | null;
  types: WorkspaceType[];
  locations: Location[];
  amenities: Amenity[];
  defaultTypeId?: number;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    locationId: workspace?.locationId.toString() ?? locations[0]?.locationId.toString() ?? '',
    workspaceTypeId: workspace?.workspaceTypeId.toString() ?? defaultTypeId?.toString() ?? '',
    workspaceCode: workspace?.workspaceCode ?? '',
    workspaceName: workspace?.workspaceName ?? '',
    floorNumber: workspace?.floorNumber ?? '',
    capacity: workspace?.capacity ?? 1,
    pricePerHour: workspace?.pricePerHour ?? 0,
    pricePerDay: workspace?.pricePerDay ?? ('' as number | ''),
    equipment: workspace?.equipment ?? '',
    description: workspace?.description ?? '',
    isBookable: workspace?.isBookable ?? true,
    amenityIds: workspace?.amenities.map((a) => a.amenityId) ?? [],
  });
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    const payload: SaveWorkspacePayload = {
      locationId: Number(form.locationId),
      workspaceTypeId: Number(form.workspaceTypeId),
      workspaceCode: form.workspaceCode.trim().toUpperCase(),
      workspaceName: form.workspaceName.trim(),
      floorNumber: form.floorNumber || null,
      capacity: Number(form.capacity),
      pricePerHour: Number(form.pricePerHour),
      pricePerDay: form.pricePerDay === '' ? null : Number(form.pricePerDay),
      equipment: form.equipment || null,
      description: form.description || null,
      isBookable: form.isBookable,
      amenityIds: form.amenityIds,
    };
    try {
      if (workspace) {
        await workspacesApi.update(workspace.workspaceId, payload);
      } else {
        await workspacesApi.create(payload);
      }
      toast.success(workspace ? 'Workspace updated.' : 'Workspace created.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the workspace.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={workspace ? `Edit — ${workspace.workspaceName}` : 'New workspace'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.workspaceCode.trim() || !form.workspaceName.trim() ||
              !form.locationId || !form.workspaceTypeId}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Select label="Location" required value={form.locationId}
            options={locations.map((l) => ({ value: l.locationId, label: l.locationName }))}
            onChange={(e) => setForm({ ...form, locationId: e.target.value })} />
          <Select label="Type" required value={form.workspaceTypeId} placeholder="Select type"
            options={types.map((t) => ({ value: t.workspaceTypeId, label: t.typeName }))}
            onChange={(e) => setForm({ ...form, workspaceTypeId: e.target.value })} />
          <Input label="Code" required value={form.workspaceCode} hint="e.g. D-001, C-001, MR-001"
            onChange={(e) => setForm({ ...form, workspaceCode: e.target.value })} />
          <Input label="Name" required value={form.workspaceName}
            onChange={(e) => setForm({ ...form, workspaceName: e.target.value })} />
          <Input label="Floor" value={form.floorNumber}
            onChange={(e) => setForm({ ...form, floorNumber: e.target.value })} />
          <Input label="Capacity" type="number" min={1} required value={form.capacity}
            onChange={(e) => setForm({ ...form, capacity: Number(e.target.value) })} />
          <Input label="Price / hour (₹)" type="number" min={0} step="0.01" required value={form.pricePerHour}
            onChange={(e) => setForm({ ...form, pricePerHour: Number(e.target.value) })} />
          <Input label="Price / day (₹)" type="number" min={0} step="0.01" value={form.pricePerDay}
            hint="Optional — used by day passes"
            onChange={(e) => setForm({ ...form, pricePerDay: e.target.value === '' ? '' : Number(e.target.value) })} />
          <Input label="Equipment" value={form.equipment} hint="Comma list, e.g. Projector,Whiteboard,VC"
            onChange={(e) => setForm({ ...form, equipment: e.target.value })} />
        </div>
        {amenities.length > 0 && (
          <div className="field">
            <span className="field__label">Amenities</span>
            <div className="perm-matrix__actions">
              {amenities.map((amenity) => (
                <Checkbox key={amenity.amenityId} label={amenity.amenityName}
                  checked={form.amenityIds.includes(amenity.amenityId)}
                  onChange={(e) => setForm({
                    ...form,
                    amenityIds: e.target.checked
                      ? [...form.amenityIds, amenity.amenityId]
                      : form.amenityIds.filter((id) => id !== amenity.amenityId),
                  })} />
              ))}
            </div>
          </div>
        )}
        <Checkbox label="Open for customer booking" checked={form.isBookable}
          onChange={(e) => setForm({ ...form, isBookable: e.target.checked })} />
      </div>
    </Modal>
  );
}
