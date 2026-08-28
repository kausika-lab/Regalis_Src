import { useCallback, useEffect, useState } from 'react';
import { menusApi } from '@/api/admin';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  ConfirmDialog,
  Input,
  Modal,
  PageHeader,
  Select,
  Spinner,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { AdminMenu, MenuPayload } from '@/models/admin';

export function MenusPage() {
  const toast = useToast();
  const canCreate = usePermission('MENU.CREATE');
  const canEdit = usePermission('MENU.EDIT');
  const canDelete = usePermission('MENU.DELETE');

  const [menus, setMenus] = useState<AdminMenu[] | null>(null);
  const [portal, setPortal] = useState('2');
  const [editing, setEditing] = useState<AdminMenu | 'new' | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<AdminMenu | null>(null);

  const load = useCallback(async () => {
    try {
      setMenus(await menusApi.list());
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load menus.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (!menus) {
    return <Spinner center />;
  }

  const portalMenus = menus.filter((m) => m.portalType === Number(portal));
  const roots = portalMenus
    .filter((m) => m.parentMenuId === null)
    .sort((a, b) => a.displayOrder - b.displayOrder);
  const childrenOf = (id: number) =>
    portalMenus.filter((m) => m.parentMenuId === id).sort((a, b) => a.displayOrder - b.displayOrder);

  const rows = roots.flatMap((root) => [
    { menu: root, depth: 0 },
    ...childrenOf(root.menuId).map((child) => ({ menu: child, depth: 1 })),
  ]);

  return (
    <>
      <PageHeader
        title="Menu Management"
        description="The sidebar is data: adding a screen is an insert here plus a role mapping — no redeploy."
        actions={canCreate && <Button onClick={() => setEditing('new')}>New menu</Button>}
      />
      <Card>
        <div className="toolbar">
          <Select label="Portal" value={portal}
            options={[{ value: 2, label: 'Admin' }, { value: 1, label: 'Customer' }]}
            onChange={(e) => setPortal(e.target.value)} />
        </div>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Menu</th>
                <th>Route</th>
                <th>Icon</th>
                <th>Order</th>
                <th>Visible</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {rows.map(({ menu, depth }) => (
                <tr key={menu.menuId}>
                  <td>
                    <span style={{ paddingLeft: depth * 24 }}>
                      {depth > 0 && '└ '}
                      <strong>{menu.displayName}</strong>{' '}
                      <span style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                        {menu.menuCode}
                      </span>
                    </span>
                  </td>
                  <td>{menu.route ?? '—'}</td>
                  <td>{menu.icon ?? '—'}</td>
                  <td>{menu.displayOrder}</td>
                  <td>
                    <Badge tone={menu.isVisible ? 'success' : 'neutral'}>
                      {menu.isVisible ? 'Visible' : 'Hidden'}
                    </Badge>
                  </td>
                  <td>
                    <div className="row-actions">
                      {canEdit && (
                        <Button size="sm" variant="outline" onClick={() => setEditing(menu)}>
                          Edit
                        </Button>
                      )}
                      {canDelete && (
                        <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(menu)}>
                          Delete
                        </Button>
                      )}
                    </div>
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </Card>

      {editing && (
        <MenuEditorModal menu={editing === 'new' ? null : editing} allMenus={menus}
          onClose={() => setEditing(null)}
          onSaved={() => { setEditing(null); void load(); }} />
      )}

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete "${deleteTarget?.displayName ?? ''}"?`}
        message="The menu disappears from every sidebar. Child menus must be deleted or moved first."
        onConfirm={async () => {
          if (!deleteTarget) return;
          try {
            await menusApi.remove(deleteTarget.menuId);
            toast.success('Menu deleted.');
            await load();
          } catch (error) {
            toast.error(error instanceof ApiClientError ? error.message : 'Could not delete the menu.');
          }
        }}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

function MenuEditorModal({ menu, allMenus, onClose, onSaved }: {
  menu: AdminMenu | null;
  allMenus: AdminMenu[];
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [form, setForm] = useState({
    menuCode: menu?.menuCode ?? '',
    displayName: menu?.displayName ?? '',
    route: menu?.route ?? '',
    icon: menu?.icon ?? '',
    displayOrder: menu?.displayOrder ?? 10,
    parentMenuId: menu?.parentMenuId?.toString() ?? '',
    portalType: menu?.portalType.toString() ?? '2',
    isVisible: menu?.isVisible ?? true,
  });
  const [busy, setBusy] = useState(false);

  const parents = allMenus.filter(
    (m) => m.portalType === Number(form.portalType) && m.menuLevel < 4 && m.menuId !== menu?.menuId,
  );

  const save = async () => {
    setBusy(true);
    const payload: MenuPayload = {
      parentMenuId: form.parentMenuId ? Number(form.parentMenuId) : null,
      menuName: form.displayName.trim(),
      displayName: form.displayName.trim(),
      route: form.route.trim() || null,
      icon: form.icon.trim() || null,
      displayOrder: Number(form.displayOrder),
      permissionId: menu?.permissionId ?? null,
      portalType: Number(form.portalType),
      isVisible: form.isVisible,
    };
    try {
      if (menu) {
        await menusApi.update(menu.menuId, payload);
      } else {
        await menusApi.create({ ...payload, menuCode: form.menuCode.trim().toUpperCase() });
      }
      toast.success(menu ? 'Menu updated.' : 'Menu created. Map it to roles to make it visible.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the menu.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={menu ? `Edit — ${menu.displayName}` : 'New menu'} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={save} loading={busy}
            disabled={!form.displayName.trim() || (!menu && !form.menuCode.trim())}>
            Save
          </Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          {!menu && (
            <Input label="Menu code" required value={form.menuCode}
              hint="Uppercase, e.g. ADM_REPORTS"
              onChange={(e) => setForm({ ...form, menuCode: e.target.value })} />
          )}
          <Input label="Display name" required value={form.displayName}
            onChange={(e) => setForm({ ...form, displayName: e.target.value })} />
          <Input label="Route" value={form.route} hint="Empty for a grouping node"
            onChange={(e) => setForm({ ...form, route: e.target.value })} />
          <Input label="Icon" value={form.icon} hint="lucide icon name, e.g. CalendarDays"
            onChange={(e) => setForm({ ...form, icon: e.target.value })} />
          <Input label="Display order" type="number" value={form.displayOrder}
            onChange={(e) => setForm({ ...form, displayOrder: Number(e.target.value) })} />
          <Select label="Portal" value={form.portalType}
            options={[{ value: 2, label: 'Admin' }, { value: 1, label: 'Customer' }]}
            onChange={(e) => setForm({ ...form, portalType: e.target.value, parentMenuId: '' })} />
          <Select label="Parent" value={form.parentMenuId} placeholder="— top level —"
            options={parents.map((p) => ({ value: p.menuId, label: p.displayName }))}
            onChange={(e) => setForm({ ...form, parentMenuId: e.target.value })} />
        </div>
        <Checkbox label="Visible in the sidebar" checked={form.isVisible}
          onChange={(e) => setForm({ ...form, isVisible: e.target.checked })} />
      </div>
    </Modal>
  );
}
