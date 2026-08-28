import { useCallback, useEffect, useState } from 'react';
import { rolesApi } from '@/api/admin';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  Input,
  Modal,
  PageHeader,
  Select,
  Spinner,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { Role, RoleMenuRow, RolePermissionRow } from '@/models/admin';
import { PORTAL_TYPE_LABELS } from '@/models/admin';

export function RolesPage() {
  const toast = useToast();
  const canCreate = usePermission('ROLE.CREATE');
  const canEditRole = usePermission('ROLE.EDIT');
  const canEditPermissions = usePermission('PERMISSION.EDIT');

  const [roles, setRoles] = useState<Role[] | null>(null);
  const [createOpen, setCreateOpen] = useState(false);
  const [matrixRole, setMatrixRole] = useState<Role | null>(null);
  const [menusRole, setMenusRole] = useState<Role | null>(null);

  const load = useCallback(async () => {
    try {
      setRoles(await rolesApi.list());
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load roles.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    void load();
  }, [load]);

  if (!roles) {
    return <Spinner center />;
  }

  return (
    <>
      <PageHeader
        title="Roles & Permissions"
        description="Roles, their permission matrix and menu visibility. Changes apply on the next token refresh (within 15 minutes)."
        actions={canCreate && <Button onClick={() => setCreateOpen(true)}>New role</Button>}
      />
      <Card>
        <div className="table-scroll">
          <table className="data-table">
            <thead>
              <tr>
                <th>Role</th>
                <th>Portal</th>
                <th>Users</th>
                <th>Permissions</th>
                <th>Status</th>
                <th></th>
              </tr>
            </thead>
            <tbody>
              {roles.map((role) => (
                <tr key={role.roleId}>
                  <td>
                    <div style={{ fontWeight: 600 }}>{role.roleName}</div>
                    <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                      {role.roleCode}
                      {role.isSystemRole && ' · system'}
                    </div>
                  </td>
                  <td>{PORTAL_TYPE_LABELS[role.portalType]}</td>
                  <td>{role.userCount}</td>
                  <td>{role.permissionCount}</td>
                  <td>
                    <Badge tone={role.isActive ? 'success' : 'neutral'}>
                      {role.isActive ? 'Active' : 'Inactive'}
                    </Badge>
                  </td>
                  <td>
                    <div className="row-actions">
                      <Button size="sm" variant="outline" onClick={() => setMatrixRole(role)}>
                        Permissions
                      </Button>
                      {canEditRole && (
                        <Button size="sm" variant="ghost" onClick={() => setMenusRole(role)}>
                          Menus
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

      <CreateRoleModal open={createOpen} onClose={() => setCreateOpen(false)}
        onCreated={() => { setCreateOpen(false); void load(); }} />
      {matrixRole && (
        <PermissionMatrixModal role={matrixRole} readOnly={!canEditPermissions}
          onClose={() => setMatrixRole(null)} onSaved={() => { setMatrixRole(null); void load(); }} />
      )}
      {menusRole && (
        <RoleMenusModal role={menusRole} onClose={() => setMenusRole(null)}
          onSaved={() => setMenusRole(null)} />
      )}
    </>
  );
}

function CreateRoleModal({ open, onClose, onCreated }: {
  open: boolean;
  onClose: () => void;
  onCreated: () => void;
}) {
  const toast = useToast();
  const [roleCode, setRoleCode] = useState('');
  const [roleName, setRoleName] = useState('');
  const [description, setDescription] = useState('');
  const [portalType, setPortalType] = useState('2');
  const [busy, setBusy] = useState(false);

  const create = async () => {
    setBusy(true);
    try {
      await rolesApi.create({
        roleCode: roleCode.trim().toUpperCase(),
        roleName: roleName.trim(),
        description: description.trim() || null,
        portalType: Number(portalType),
      });
      toast.success('Role created. Grant its permissions next.');
      onCreated();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not create the role.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open={open} title="New role" onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={create} loading={busy} disabled={!roleCode.trim() || !roleName.trim()}>
            Create role
          </Button>
        </>
      }>
      <div className="form-stack">
        <Input label="Role code" required placeholder="e.g. FRONT_DESK" value={roleCode}
          hint="Uppercase letters, digits and underscores" onChange={(e) => setRoleCode(e.target.value)} />
        <Input label="Role name" required value={roleName} onChange={(e) => setRoleName(e.target.value)} />
        <Input label="Description" value={description} onChange={(e) => setDescription(e.target.value)} />
        <Select label="Portal" required value={portalType}
          options={[{ value: 2, label: 'Admin' }, { value: 1, label: 'Customer' }, { value: 3, label: 'Both' }]}
          onChange={(e) => setPortalType(e.target.value)} />
      </div>
    </Modal>
  );
}

function PermissionMatrixModal({ role, readOnly, onClose, onSaved }: {
  role: Role;
  readOnly: boolean;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [rows, setRows] = useState<RolePermissionRow[] | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    rolesApi.permissions(role.roleId).then(setRows)
      .catch(() => toast.error('Failed to load the permission matrix.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [role.roleId]);

  const toggle = (permissionId: number, granted: boolean) =>
    setRows((current) =>
      current!.map((row) => (row.permissionId === permissionId ? { ...row, isGranted: granted } : row)),
    );

  const toggleModule = (module: string, granted: boolean) =>
    setRows((current) =>
      current!.map((row) => (row.moduleName === module ? { ...row, isGranted: granted } : row)),
    );

  const save = async () => {
    setBusy(true);
    try {
      await rolesApi.updatePermissions(
        role.roleId,
        rows!.filter((r) => r.isGranted).map((r) => r.permissionId),
      );
      toast.success('Permissions updated. They apply on the next token refresh.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the matrix.');
    } finally {
      setBusy(false);
    }
  };

  const modules = rows
    ? [...new Set(rows.map((r) => r.moduleName))]
    : [];

  return (
    <Modal open title={`Permissions — ${role.roleName}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Close</Button>
          {!readOnly && <Button onClick={save} loading={busy} disabled={!rows}>Save matrix</Button>}
        </>
      }>
      {!rows ? (
        <Spinner center />
      ) : (
        <div className="perm-matrix">
          {modules.map((module) => {
            const moduleRows = rows.filter((r) => r.moduleName === module);
            const allGranted = moduleRows.every((r) => r.isGranted);
            return (
              <div key={module} className="perm-matrix__module">
                <div className="perm-matrix__module-head">
                  <span className="perm-matrix__module-name">{module}</span>
                  {!readOnly && (
                    <Checkbox label={allGranted ? 'Revoke all' : 'Grant all'} checked={allGranted}
                      onChange={(e) => toggleModule(module, e.target.checked)} />
                  )}
                </div>
                <div className="perm-matrix__actions">
                  {moduleRows.map((row) => (
                    <Checkbox key={row.permissionId} label={row.actionName} checked={row.isGranted}
                      disabled={readOnly}
                      onChange={(e) => toggle(row.permissionId, e.target.checked)} />
                  ))}
                </div>
              </div>
            );
          })}
        </div>
      )}
    </Modal>
  );
}

function RoleMenusModal({ role, onClose, onSaved }: {
  role: Role;
  onClose: () => void;
  onSaved: () => void;
}) {
  const toast = useToast();
  const [rows, setRows] = useState<RoleMenuRow[] | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    rolesApi.menus(role.roleId).then(setRows)
      .catch(() => toast.error('Failed to load the role menus.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [role.roleId]);

  const save = async () => {
    setBusy(true);
    try {
      await rolesApi.updateMenus(role.roleId, rows!.filter((r) => r.isGranted).map((r) => r.menuId));
      toast.success('Role menus updated.');
      onSaved();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save the menus.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Modal open title={`Menus — ${role.roleName}`} onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Close</Button>
          <Button onClick={save} loading={busy} disabled={!rows}>Save menus</Button>
        </>
      }>
      {!rows ? (
        <Spinner center />
      ) : (
        <div className="perm-matrix">
          {rows.map((row) => (
            <Checkbox key={row.menuId} label={row.displayName} checked={row.isGranted}
              onChange={(e) => setRows((current) =>
                current!.map((r) => (r.menuId === row.menuId ? { ...r, isGranted: e.target.checked } : r)),
              )} />
          ))}
        </div>
      )}
    </Modal>
  );
}
