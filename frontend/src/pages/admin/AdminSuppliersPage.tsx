import { useCallback, useEffect, useState } from 'react';
import { suppliersApi } from '@/api/procurement';
import { locationsApi } from '@/api/masters';
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
  type SelectOption,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useDebounce } from '@/hooks/useDebounce';
import { useFieldErrors } from '@/hooks/useFieldErrors';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { SaveSupplierPayload, Supplier } from '@/models/procurement';

export function AdminSuppliersPage() {
  const toast = useToast();
  const canCreate = usePermission('SUPPLIER.CREATE');
  const canEdit = usePermission('SUPPLIER.EDIT');
  const canDelete = usePermission('SUPPLIER.DELETE');

  const [result, setResult] = useState<PagedResult<Supplier> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const debouncedSearch = useDebounce(search, 300);
  const [editing, setEditing] = useState<Supplier | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Supplier | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await suppliersApi.list({
        page,
        pageSize: 20,
        search: debouncedSearch || undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load suppliers.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<Supplier>[] = [
    {
      key: 'supplier',
      header: 'Supplier',
      render: (s) => (
        <div>
          <div style={{ fontWeight: 600 }}>
            {s.supplierName} {!s.isActive && <Badge tone="neutral">Inactive</Badge>}
          </div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {s.supplierCode}
          </div>
        </div>
      ),
    },
    {
      key: 'contact',
      header: 'Contact',
      render: (s) => (
        <div>
          <div>{s.contactPerson ?? '—'}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {s.mobileNumber ?? s.email ?? ''}
          </div>
        </div>
      ),
    },
    { key: 'gstin', header: 'GSTIN', render: (s) => s.gstin ?? '—' },
    { key: 'city', header: 'City', render: (s) => s.city ?? '—' },
    {
      key: 'location',
      header: 'Location',
      render: (s) => s.locationName ?? 'All locations',
    },
    {
      key: 'openPos',
      header: 'Open POs',
      render: (s) => (s.openPurchaseOrders > 0 ? <Badge tone="gold">{s.openPurchaseOrders}</Badge> : '—'),
    },
    {
      key: 'actions',
      header: '',
      render: (s) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(s)}>
              Edit
            </Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(s)}>
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
        title="Suppliers"
        description="Vendors a purchase order can be raised against. A supplier with no location serves every location."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New supplier</Button>}
      />
      <Card>
        <div className="toolbar">
          <Input
            label="Search"
            placeholder="Name, code or GSTIN"
            value={search}
            onChange={(e) => {
              setPage(1);
              setSearch(e.target.value);
            }}
          />
        </div>
        <DataTable
          columns={columns}
          result={result}
          loading={loading}
          rowKey={(s) => s.supplierId}
          onPageChange={setPage}
          emptyTitle="No suppliers yet"
          emptyDescription="Add the vendors you buy from, then raise purchase orders against them."
        />
      </Card>

      {editing && (
        <SupplierEditorModal
          supplier={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => {
            setEditing(null);
            void load();
          }}
        />
      )}

      <ConfirmDialog
        open={deleteTarget !== null}
        danger
        confirmLabel="Delete"
        title={`Delete ${deleteTarget?.supplierName ?? ''}?`}
        message="A supplier with purchase orders cannot be deleted — deactivate it instead."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await suppliersApi.remove(deleteTarget.supplierId);
            toast.success('Supplier deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)}
      />
    </>
  );
}

function SupplierEditorModal({
  supplier,
  onClose,
  onSaved,
}: {
  supplier: Supplier | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [locations, setLocations] = useState<SelectOption[]>([]);
  const [busy, setBusy] = useState(false);
  const [form, setForm] = useState({
    supplierCode: supplier?.supplierCode ?? '',
    supplierName: supplier?.supplierName ?? '',
    contactPerson: supplier?.contactPerson ?? '',
    email: supplier?.email ?? '',
    mobileNumber: supplier?.mobileNumber ?? '',
    gstin: supplier?.gstin ?? '',
    addressLine: supplier?.addressLine ?? '',
    city: supplier?.city ?? '',
    state: supplier?.state ?? '',
    pincode: supplier?.pincode ?? '',
    paymentTerms: supplier?.paymentTerms ?? '',
    locationId: supplier?.locationId ? String(supplier.locationId) : '',
    notes: supplier?.notes ?? '',
  });

  useEffect(() => {
    void (async () => {
      try {
        const page = await locationsApi.list({ page: 1, pageSize: 100 });
        setLocations(page.items.map((l) => ({ value: l.locationId, label: l.locationName })));
      } catch {
        // A failed lookup only costs the dropdown; the form still saves without one.
        setLocations([]);
      }
    })();
  }, []);

  const set = (name: keyof typeof form, value: string) => {
    clearField(name);
    setForm((f) => ({ ...f, [name]: value }));
  };

  const save = async () => {
    setBusy(true);
    clear();
    const payload: SaveSupplierPayload = {
      supplierCode: form.supplierCode.trim().toUpperCase(),
      supplierName: form.supplierName.trim(),
      contactPerson: form.contactPerson || null,
      email: form.email || null,
      mobileNumber: form.mobileNumber || null,
      gstin: form.gstin ? form.gstin.trim().toUpperCase() : null,
      addressLine: form.addressLine || null,
      city: form.city || null,
      state: form.state || null,
      pincode: form.pincode || null,
      paymentTerms: form.paymentTerms || null,
      locationId: form.locationId ? Number(form.locationId) : null,
      notes: form.notes || null,
    };
    try {
      if (supplier) {
        await suppliersApi.update(supplier.supplierId, payload);
      } else {
        await suppliersApi.create(payload);
      }
      toast.success(supplier ? 'Supplier updated.' : 'Supplier created.');
      onSaved();
    } catch (error) {
      const message = capture(error);
      if (message) {
        toast.error(message);
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal
      open
      title={supplier ? `Edit — ${supplier.supplierName}` : 'New supplier'}
      onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          <Button
            onClick={save}
            loading={busy}
            disabled={!form.supplierCode.trim() || !form.supplierName.trim()}
          >
            Save
          </Button>
        </>
      }
    >
      <div className="form-stack">
        {summary && (
          <div className="form-error" role="alert">
            {summary}
          </div>
        )}
        <div className="form-grid">
          <Input
            label="Code"
            required
            value={form.supplierCode}
            hint="e.g. SUP-ACME"
            error={fieldErrors.supplierCode}
            onChange={(e) => set('supplierCode', e.target.value)}
          />
          <Input
            label="Supplier name"
            required
            value={form.supplierName}
            error={fieldErrors.supplierName}
            onChange={(e) => set('supplierName', e.target.value)}
          />
          <Input
            label="Contact person"
            value={form.contactPerson}
            error={fieldErrors.contactPerson}
            onChange={(e) => set('contactPerson', e.target.value)}
          />
          <Input
            label="Email"
            type="email"
            value={form.email}
            error={fieldErrors.email}
            onChange={(e) => set('email', e.target.value)}
          />
          <Input
            label="Mobile"
            value={form.mobileNumber}
            error={fieldErrors.mobileNumber}
            onChange={(e) => set('mobileNumber', e.target.value)}
          />
          <Input
            label="GSTIN"
            value={form.gstin}
            hint="15 characters"
            error={fieldErrors.gstin}
            onChange={(e) => set('gstin', e.target.value)}
          />
          <Input
            label="Address"
            value={form.addressLine}
            error={fieldErrors.addressLine}
            onChange={(e) => set('addressLine', e.target.value)}
          />
          <Input
            label="City"
            value={form.city}
            error={fieldErrors.city}
            onChange={(e) => set('city', e.target.value)}
          />
          <Input
            label="State"
            value={form.state}
            error={fieldErrors.state}
            onChange={(e) => set('state', e.target.value)}
          />
          <Input
            label="Pincode"
            value={form.pincode}
            error={fieldErrors.pincode}
            onChange={(e) => set('pincode', e.target.value)}
          />
          <Input
            label="Payment terms"
            value={form.paymentTerms}
            hint="e.g. Net 30"
            error={fieldErrors.paymentTerms}
            onChange={(e) => set('paymentTerms', e.target.value)}
          />
          <Select
            label="Location"
            value={form.locationId}
            placeholder="All locations"
            options={locations}
            error={fieldErrors.locationId}
            onChange={(e) => set('locationId', e.target.value)}
          />
        </div>
        <Input
          label="Notes"
          value={form.notes}
          error={fieldErrors.notes}
          onChange={(e) => set('notes', e.target.value)}
        />
      </div>
    </Modal>
  );
}
