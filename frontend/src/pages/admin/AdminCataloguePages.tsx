import { useCallback, useEffect, useMemo, useState } from 'react';
import { bannersApi, servicesApi, settingsApi } from '@/api/masters';
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
import {
  PORTAL_TYPE_LABELS,
  type AppSetting,
  type Banner,
  type SaveBannerPayload,
  type SaveServiceItemPayload,
  type ServiceItem,
} from '@/models/masters';
import { formatDate, formatInr } from '@/utils/formatters';

// ── /admin/services — Services & Coffee catalogue ───────────────────────────

export function AdminServicesPage() {
  const toast = useToast();
  const canEdit = usePermission('SERVICE.EDIT');
  const canDelete = usePermission('SERVICE.DELETE');

  const [rows, setRows] = useState<PagedResult<ServiceItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<ServiceItem | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<ServiceItem | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await servicesApi.list({ page, pageSize: 20, includeUnavailable: true }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the catalogue.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<ServiceItem>[] = [
    {
      key: 'item', header: 'Item',
      render: (s) => (
        <div>
          <div style={{ fontWeight: 600 }}>{s.itemName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
            {s.itemCode} · {s.category}
          </div>
        </div>
      ),
    },
    {
      key: 'price', header: 'Price',
      render: (s) => `${formatInr(s.price)}${s.unit ? ` / ${s.unit}` : ''}`,
    },
    {
      key: 'available', header: 'Availability',
      render: (s) => s.isAvailable
        ? <Badge tone="success">Available</Badge>
        : <Badge tone="neutral">Hidden</Badge>,
    },
    { key: 'order', header: 'Order', render: (s) => s.displayOrder },
    {
      key: 'actions', header: '',
      render: (s) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(s)}>Edit</Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(s)}>Delete</Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Services / Coffee"
        description="The catalogue customers browse — coffee, snacks, printing and featured services."
        actions={canEdit && <Button onClick={() => setEditing('new')}>New item</Button>} />
      <Card>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(s) => s.serviceItemId} onPageChange={setPage} emptyTitle="No catalogue items" />
      </Card>

      {editing && (
        <ServiceEditorModal item={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete ${deleteTarget?.itemName ?? ''}?`}
        message="An item that appears on orders cannot be deleted — mark it unavailable instead."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await servicesApi.remove(deleteTarget.serviceItemId);
            toast.success('Item deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

function ServiceEditorModal({ item, onClose, onSaved }: {
  item: ServiceItem | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    itemCode: item?.itemCode ?? '',
    itemName: item?.itemName ?? '',
    category: item?.category ?? 'Coffee',
    price: item ? String(item.price) : '',
    unit: item?.unit ?? '',
    imagePath: item?.imagePath ?? '',
    description: item?.description ?? '',
    isAvailable: item?.isAvailable ?? true,
    displayOrder: item ? String(item.displayOrder) : '0',
  });
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    const payload: SaveServiceItemPayload = {
      itemCode: form.itemCode.trim().toUpperCase(),
      itemName: form.itemName.trim(),
      category: form.category.trim(),
      price: Number(form.price) || 0,
      unit: form.unit.trim() || null,
      imagePath: form.imagePath.trim() || null,
      description: form.description.trim() || null,
      isAvailable: form.isAvailable,
      displayOrder: Number(form.displayOrder) || 0,
      locationId: item?.locationId ?? null,
    };
    try {
      if (item) {
        await servicesApi.update(item.serviceItemId, payload);
      } else {
        await servicesApi.create(payload);
      }
      toast.success(item ? 'Item updated.' : 'Item created.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the item.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={item ? `Edit — ${item.itemName}` : 'New catalogue item'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.itemCode.trim() || !form.itemName.trim() || !form.category.trim()}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Code" required value={form.itemCode} hint="e.g. COF-CAPP"
            disabled={item !== null}
            onChange={(e) => setForm({ ...form, itemCode: e.target.value })} />
          <Input label="Name" required value={form.itemName}
            onChange={(e) => setForm({ ...form, itemName: e.target.value })} />
          <Input label="Category" required value={form.category}
            hint="Coffee, Tea, Snacks, Printing, Scanning, Locker"
            onChange={(e) => setForm({ ...form, category: e.target.value })} />
          <Input label="Price (₹)" type="number" required value={form.price}
            onChange={(e) => setForm({ ...form, price: e.target.value })} />
          <Input label="Unit" value={form.unit} placeholder="Cup, Page…"
            onChange={(e) => setForm({ ...form, unit: e.target.value })} />
          <Input label="Display order" type="number" value={form.displayOrder}
            onChange={(e) => setForm({ ...form, displayOrder: e.target.value })} />
        </div>
        <Input label="Image path" value={form.imagePath}
          onChange={(e) => setForm({ ...form, imagePath: e.target.value })} />
        <Input label="Description" value={form.description}
          onChange={(e) => setForm({ ...form, description: e.target.value })} />
        <Checkbox label="Available to customers" checked={form.isAvailable}
          onChange={(e) => setForm({ ...form, isAvailable: e.target.checked })} />
      </div>
    </Modal>
  );
}

// ── /admin/banners ───────────────────────────────────────────────────────────

export function AdminBannersPage() {
  const toast = useToast();
  const canEdit = usePermission('BANNER.EDIT');
  const canCreate = usePermission('BANNER.CREATE');
  const canDelete = usePermission('BANNER.DELETE');

  const [rows, setRows] = useState<PagedResult<Banner> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [editing, setEditing] = useState<Banner | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<Banner | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await bannersApi.list({ page, pageSize: 20 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load banners.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<Banner>[] = [
    {
      key: 'banner', header: 'Banner',
      render: (b) => (
        <div>
          <div style={{ fontWeight: 600 }}>{b.title}</div>
          {b.subtitle && (
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{b.subtitle}</div>
          )}
        </div>
      ),
    },
    {
      key: 'window', header: 'Showing',
      render: (b) => `${formatDate(b.validFromUtc)} – ${formatDate(b.validToUtc)}`,
    },
    { key: 'portal', header: 'Portal', render: (b) => PORTAL_TYPE_LABELS[b.portalType] },
    {
      key: 'live', header: 'Status',
      render: (b) => {
        const now = Date.now();
        const live = now >= new Date(b.validFromUtc).getTime() &&
          now < new Date(b.validToUtc).getTime();
        return live ? <Badge tone="success">Live</Badge> : <Badge tone="neutral">Off-window</Badge>;
      },
    },
    {
      key: 'actions', header: '',
      render: (b) => (
        <div className="row-actions">
          {canEdit && (
            <Button size="sm" variant="outline" onClick={() => setEditing(b)}>Edit</Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(b)}>Delete</Button>
          )}
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader title="Banners"
        description="The home-screen carousel. A banner shows only inside its validity window."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New banner</Button>} />
      <Card>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(b) => b.bannerId} onPageChange={setPage} emptyTitle="No banners yet" />
      </Card>

      {editing && (
        <BannerEditorModal banner={editing === 'new' ? null : editing}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete "${deleteTarget?.title ?? ''}"?`}
        message="The banner disappears from the carousel immediately."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await bannersApi.remove(deleteTarget.bannerId);
            toast.success('Banner deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete.');
          }
        }}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

const toDateInput = (utc: string) => utc.slice(0, 10);

function BannerEditorModal({ banner, onClose, onSaved }: {
  banner: Banner | null;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const today = new Date().toISOString().slice(0, 10);
  const [form, setForm] = useState({
    title: banner?.title ?? '',
    subtitle: banner?.subtitle ?? '',
    imagePath: banner?.imagePath ?? '',
    linkUrl: banner?.linkUrl ?? '',
    promotionalPrice: banner?.promotionalPrice != null ? String(banner.promotionalPrice) : '',
    displayOrder: banner ? String(banner.displayOrder) : '0',
    from: banner ? toDateInput(banner.validFromUtc) : today,
    to: banner ? toDateInput(banner.validToUtc) : '',
    portalType: banner ? String(banner.portalType) : '1',
  });
  const [busy, setBusy] = useState(false);

  const save = async () => {
    setBusy(true);
    const payload: SaveBannerPayload = {
      title: form.title.trim(),
      subtitle: form.subtitle.trim() || null,
      imagePath: form.imagePath.trim(),
      linkUrl: form.linkUrl.trim() || null,
      promotionalPrice: form.promotionalPrice ? Number(form.promotionalPrice) : null,
      displayOrder: Number(form.displayOrder) || 0,
      validFromUtc: `${form.from}T00:00:00Z`,
      validToUtc: `${form.to}T23:59:59Z`,
      locationId: banner?.locationId ?? null,
      portalType: Number(form.portalType),
    };
    try {
      if (banner) {
        await bannersApi.update(banner.bannerId, payload);
      } else {
        await bannersApi.create(payload);
      }
      toast.success(banner ? 'Banner updated.' : 'Banner created.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the banner.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={banner ? `Edit — ${banner.title}` : 'New banner'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.title.trim() || !form.imagePath.trim() || !form.from || !form.to}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Title" required value={form.title}
            onChange={(e) => setForm({ ...form, title: e.target.value })} />
          <Input label="Subtitle" value={form.subtitle}
            onChange={(e) => setForm({ ...form, subtitle: e.target.value })} />
          <Input label="Image path" required value={form.imagePath}
            onChange={(e) => setForm({ ...form, imagePath: e.target.value })} />
          <Input label="Link URL" value={form.linkUrl}
            onChange={(e) => setForm({ ...form, linkUrl: e.target.value })} />
          <Input label="Promotional price (₹)" type="number" value={form.promotionalPrice}
            onChange={(e) => setForm({ ...form, promotionalPrice: e.target.value })} />
          <Input label="Display order" type="number" value={form.displayOrder}
            onChange={(e) => setForm({ ...form, displayOrder: e.target.value })} />
          <Input label="Shows from" type="date" required value={form.from}
            onChange={(e) => setForm({ ...form, from: e.target.value })} />
          <Input label="Until" type="date" required value={form.to}
            onChange={(e) => setForm({ ...form, to: e.target.value })} />
          <Select label="Portal" value={form.portalType}
            options={Object.entries(PORTAL_TYPE_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => setForm({ ...form, portalType: e.target.value })} />
        </div>
      </div>
    </Modal>
  );
}

// ── /admin/settings ──────────────────────────────────────────────────────────

export function AdminSettingsPage() {
  const toast = useToast();
  const canEdit = usePermission('SETTING.EDIT');

  const [settings, setSettings] = useState<AppSetting[]>([]);
  const [loading, setLoading] = useState(true);
  const [draft, setDraft] = useState<Record<string, string>>({});
  const [saving, setSaving] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setSettings(await settingsApi.list());
      setDraft({});
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load settings.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => { void load(); }, [load]);

  const groups = useMemo(() => {
    const byGroup = new Map<string, AppSetting[]>();
    for (const setting of settings) {
      const list = byGroup.get(setting.settingGroup) ?? [];
      list.push(setting);
      byGroup.set(setting.settingGroup, list);
    }
    return [...byGroup.entries()];
  }, [settings]);

  const dirtyKeys = Object.keys(draft).filter((key) => {
    const original = settings.find((s) => s.settingKey === key);
    return original !== undefined && draft[key] !== original.settingValue;
  });

  const save = async () => {
    setSaving(true);
    try {
      setSettings(await settingsApi.update(
        dirtyKeys.map((key) => ({ settingKey: key, settingValue: draft[key] }))));
      setDraft({});
      toast.success('Settings saved and applied.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save settings.');
    } finally {
      setSaving(false);
    }
  };

  return (
    <>
      <PageHeader title="Settings"
        description="The no-hard-coding table: booking rules, tax, company details, security policy. Changes apply immediately."
        actions={canEdit && dirtyKeys.length > 0 && (
          <Button onClick={save} loading={saving}>
            Save {dirtyKeys.length} change{dirtyKeys.length > 1 ? 's' : ''}
          </Button>
        )} />
      {loading ? (
        <Card><div style={{ padding: 'var(--space-4)' }}>Loading…</div></Card>
      ) : (
        <div style={{ display: 'grid', gap: 'var(--space-4)' }}>
          {groups.map(([group, rows]) => (
            <Card key={group} title={group}>
              <div className="form-grid">
                {rows.map((setting) => (
                  <Input key={setting.settingKey}
                    label={setting.settingKey.split('.').slice(1).join('.')}
                    hint={setting.description ?? undefined}
                    value={draft[setting.settingKey] ?? setting.settingValue}
                    disabled={!canEdit || !setting.isEditableByAdmin || setting.isSecret}
                    onChange={(e) => setDraft({ ...draft, [setting.settingKey]: e.target.value })} />
                ))}
              </div>
            </Card>
          ))}
        </div>
      )}
    </>
  );
}
