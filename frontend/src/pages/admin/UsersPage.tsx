import { useCallback, useEffect, useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { usersApi, rolesApi } from '@/api/admin';
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
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useDebounce } from '@/hooks/useDebounce';
import { usePermission } from '@/hooks/usePermission';
import type { Role, UserListItem } from '@/models/admin';
import { USER_STATUS_LABELS, USER_TYPE_LABELS } from '@/models/admin';
import type { PagedResult } from '@/models/api';
import { formatDateTime } from '@/utils/formatters';

const passwordRule = z
  .string()
  .min(8, 'At least 8 characters.')
  .regex(/[A-Z]/, 'Add an uppercase letter.')
  .regex(/[a-z]/, 'Add a lowercase letter.')
  .regex(/[0-9]/, 'Add a digit.')
  .regex(/[^A-Za-z0-9]/, 'Add a symbol.');

const userSchema = z.object({
  fullName: z.string().min(1, 'Name is required.').max(150),
  email: z.email('Enter a valid email.'),
  mobileNumber: z.string().regex(/^\+?[0-9]{10,15}$/, 'Enter a valid mobile number.'),
  password: passwordRule,
  // Selects bind strings; converted to a number on submit.
  userType: z.enum(['1', '2', '3']),
  roleIds: z.array(z.number()).min(1, 'Assign at least one role.'),
  companyName: z.string().max(200).optional(),
});

type UserForm = z.infer<typeof userSchema>;

const statusTone: Record<number, 'success' | 'neutral' | 'danger' | 'warning'> = {
  1: 'success',
  2: 'neutral',
  3: 'danger',
  4: 'warning',
};

export function UsersPage() {
  const toast = useToast();
  const canCreate = usePermission('USER.CREATE');
  const canEdit = usePermission('USER.EDIT');
  const canDelete = usePermission('USER.DELETE');

  const [result, setResult] = useState<PagedResult<UserListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [userType, setUserType] = useState('');
  const [roles, setRoles] = useState<Role[]>([]);
  const debouncedSearch = useDebounce(search, 350);

  const [createOpen, setCreateOpen] = useState(false);
  const [resetTarget, setResetTarget] = useState<UserListItem | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<UserListItem | null>(null);
  const [newPassword, setNewPassword] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(
        await usersApi.list({
          page,
          pageSize: 20,
          search: debouncedSearch || undefined,
          userType: userType ? Number(userType) : undefined,
        }),
      );
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load users.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, userType]);

  useEffect(() => {
    void load();
  }, [load]);

  useEffect(() => {
    rolesApi.list().then(setRoles).catch(() => {});
  }, []);

  const act = async (action: () => Promise<unknown>, message: string) => {
    try {
      await action();
      toast.success(message);
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'The action failed.');
    }
  };

  const columns: Column<UserListItem>[] = [
    {
      key: 'name',
      header: 'Name',
      render: (u) => (
        <div>
          <div style={{ fontWeight: 600 }}>{u.fullName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{u.email}</div>
        </div>
      ),
    },
    { key: 'mobile', header: 'Mobile', render: (u) => u.mobileNumber },
    { key: 'type', header: 'Type', render: (u) => USER_TYPE_LABELS[u.userType] ?? u.userType },
    { key: 'roles', header: 'Roles', render: (u) => u.roles.join(', ') },
    {
      key: 'status',
      header: 'Status',
      render: (u) => <Badge tone={statusTone[u.status] ?? 'neutral'}>{USER_STATUS_LABELS[u.status]}</Badge>,
    },
    {
      key: 'lastLogin',
      header: 'Last sign-in',
      render: (u) => (u.lastLoginUtc ? formatDateTime(u.lastLoginUtc) : '—'),
    },
    {
      key: 'actions',
      header: '',
      render: (u) => (
        <div className="row-actions">
          {canEdit && u.status !== 1 && (
            <Button size="sm" variant="outline" onClick={() => act(() => usersApi.activate(u.userId), 'User activated.')}>
              Activate
            </Button>
          )}
          {canEdit && u.status === 1 && (
            <Button size="sm" variant="outline" onClick={() => act(() => usersApi.deactivate(u.userId), 'User deactivated.')}>
              Deactivate
            </Button>
          )}
          {canEdit && u.status === 3 && (
            <Button size="sm" variant="outline" onClick={() => act(() => usersApi.unlock(u.userId), 'User unlocked.')}>
              Unlock
            </Button>
          )}
          {canEdit && (
            <Button size="sm" variant="ghost" onClick={() => { setNewPassword(''); setResetTarget(u); }}>
              Reset password
            </Button>
          )}
          {canDelete && (
            <Button size="sm" variant="ghost" onClick={() => setDeleteTarget(u)}>
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
        title="Users"
        description="Admin and staff accounts, customer records and their role assignments."
        actions={canCreate && <Button onClick={() => setCreateOpen(true)}>New user</Button>}
      />
      <Card>
        <div className="toolbar">
          <Input label="Search" placeholder="Name, email, mobile…" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          <Select label="Type" value={userType} placeholder="All types"
            options={[{ value: 1, label: 'Customer' }, { value: 2, label: 'Admin' }, { value: 3, label: 'Staff' }]}
            onChange={(e) => { setUserType(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(u) => u.userId} onPageChange={setPage}
          emptyTitle="No users match the filters" />
      </Card>

      <CreateUserModal open={createOpen} roles={roles} onClose={() => setCreateOpen(false)}
        onCreated={() => { setCreateOpen(false); void load(); }} />

      <Modal open={resetTarget !== null} title={`Reset password — ${resetTarget?.fullName ?? ''}`}
        onClose={() => setResetTarget(null)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setResetTarget(null)}>Cancel</Button>
            <Button onClick={() => resetTarget && act(async () => {
              await usersApi.resetPassword(resetTarget.userId, newPassword);
              setResetTarget(null);
            }, 'Password reset. The user must change it at next sign-in.')}>
              Reset
            </Button>
          </>
        }>
        <Input label="New password" type="text" value={newPassword} required
          hint="Min 8 chars with upper, lower, digit and symbol. The user must change it at next sign-in."
          onChange={(e) => setNewPassword(e.target.value)} />
      </Modal>

      <ConfirmDialog open={deleteTarget !== null} danger confirmLabel="Delete"
        title={`Delete ${deleteTarget?.fullName ?? ''}?`}
        message="The account is soft-deleted and can be restored by the DBA. The user can no longer sign in."
        onConfirm={() => deleteTarget ? act(() => usersApi.remove(deleteTarget.userId), 'User deleted.') : undefined}
        onClose={() => setDeleteTarget(null)} />
    </>
  );
}

function CreateUserModal({ open, roles, onClose, onCreated }: {
  open: boolean;
  roles: Role[];
  onClose: () => void;
  onCreated: () => void;
}) {
  const toast = useToast();
  const {
    register,
    handleSubmit,
    watch,
    setValue,
    reset,
    formState: { errors, isSubmitting },
  } = useForm<UserForm>({
    resolver: zodResolver(userSchema),
    defaultValues: { userType: '2', roleIds: [] },
  });

  const selectedType = Number(watch('userType'));
  const selectedRoleIds = watch('roleIds');
  // Role portal must match the user type (enforced again server-side).
  const eligibleRoles = roles.filter((r) =>
    selectedType === 1 ? r.portalType === 1 : r.portalType === 2 || r.portalType === 3,
  );

  const onSubmit = handleSubmit(async (values) => {
    try {
      await usersApi.create({
        ...values,
        userType: Number(values.userType),
        companyName: values.companyName || null,
      });
      toast.success('User created. They must change the password at first sign-in.');
      reset();
      onCreated();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not create the user.');
    }
  });

  return (
    <Modal open={open} title="New user" onClose={onClose}
      footer={
        <>
          <Button variant="ghost" onClick={onClose}>Cancel</Button>
          <Button onClick={onSubmit} loading={isSubmitting}>Create user</Button>
        </>
      }>
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Full name" required error={errors.fullName?.message} {...register('fullName')} />
          <Input label="Email" type="email" required error={errors.email?.message} {...register('email')} />
          <Input label="Mobile" required error={errors.mobileNumber?.message} {...register('mobileNumber')} />
          <Select label="User type" required error={errors.userType?.message}
            options={[{ value: 2, label: 'Admin' }, { value: 3, label: 'Staff' }, { value: 1, label: 'Customer' }]}
            {...register('userType', {
              onChange: () => setValue('roleIds', []),
            })} />
          <Input label="Company" error={errors.companyName?.message} {...register('companyName')} />
          <Input label="Initial password" type="text" required error={errors.password?.message}
            hint="The user must change it at first sign-in" {...register('password')} />
        </div>
        <div className="field">
          <span className="field__label">
            Roles <span className="field__required">*</span>
          </span>
          <div className="perm-matrix__actions">
            {eligibleRoles.map((role) => (
              <label key={role.roleId} className="checkbox">
                <input type="checkbox"
                  checked={selectedRoleIds.includes(role.roleId)}
                  onChange={(e) => setValue('roleIds',
                    e.target.checked
                      ? [...selectedRoleIds, role.roleId]
                      : selectedRoleIds.filter((id) => id !== role.roleId),
                    { shouldValidate: true })} />
                <span>{role.roleName}</span>
              </label>
            ))}
          </div>
          {errors.roleIds && <span className="field__error" role="alert">{errors.roleIds.message}</span>}
        </div>
      </div>
    </Modal>
  );
}
