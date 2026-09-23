import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  AdminMenu,
  CreateUserPayload,
  MenuPayload,
  Role,
  RoleMenuRow,
  RolePermissionRow,
  UpdateUserPayload,
  UserDetail,
  UserListItem,
  UserListQuery,
} from '@/models/admin';
import { api, unwrap } from './client';

export const usersApi = {
  list: (query: UserListQuery) =>
    unwrap<PagedResult<UserListItem>>(api.get('/users', { params: query })),
  get: (id: number) => unwrap<UserDetail>(api.get(`/users/${id}`)),
  create: (payload: CreateUserPayload) => unwrap<UserDetail>(api.post('/users', payload)),
  update: (id: number, payload: UpdateUserPayload) =>
    unwrap<UserDetail>(api.put(`/users/${id}`, payload)),
  activate: (id: number) => api.patch(`/users/${id}/activate`),
  deactivate: (id: number) => api.patch(`/users/${id}/deactivate`),
  lock: (id: number) => api.patch(`/users/${id}/lock`),
  unlock: (id: number) => api.patch(`/users/${id}/unlock`),
  resetPassword: (id: number, newPassword: string) =>
    api.post(`/users/${id}/reset-password`, { newPassword }),
  remove: (id: number) => api.delete(`/users/${id}`),
};

/**
 * Customers are User rows with UserType 1; api/customers is the read-only
 * customer register the admin portal uses for pickers and the customer grid.
 */
export const customersApi = {
  list: (query: PagedRequest & { status?: number } = {}) =>
    unwrap<PagedResult<UserListItem>>(api.get('/customers', { params: query })),
  get: (id: number) => unwrap<UserDetail>(api.get(`/customers/${id}`)),
};

export const rolesApi = {
  list: () => unwrap<Role[]>(api.get('/roles')),
  create: (payload: { roleCode: string; roleName: string; description: string | null; portalType: number }) =>
    unwrap<Role>(api.post('/roles', payload)),
  update: (
    id: number,
    payload: { roleName: string; description: string | null; portalType: number; isActive: boolean },
  ) => unwrap<Role>(api.put(`/roles/${id}`, payload)),
  remove: (id: number) => api.delete(`/roles/${id}`),
  permissions: (id: number) => unwrap<RolePermissionRow[]>(api.get(`/roles/${id}/permissions`)),
  updatePermissions: (id: number, permissionIds: number[]) =>
    api.put(`/roles/${id}/permissions`, { permissionIds }),
  menus: (id: number) => unwrap<RoleMenuRow[]>(api.get(`/roles/${id}/menus`)),
  updateMenus: (id: number, menuIds: number[]) => api.put(`/roles/${id}/menus`, { menuIds }),
};

export interface AuditLogRow {
  auditLogId: number;
  userId: number | null;
  userName: string | null;
  action: string;
  moduleName: string;
  entityName: string | null;
  entityId: string | null;
  oldValue: string | null;
  newValue: string | null;
  ipAddress: string | null;
  correlationId: string | null;
  timestampUtc: string;
}

export interface AuditListQuery extends PagedRequest {
  userId?: number;
  module?: string;
  action?: string;
  entityName?: string;
  from?: string;
  to?: string;
}

export const auditApi = {
  list: (query: AuditListQuery = {}) =>
    unwrap<PagedResult<AuditLogRow>>(api.get('/audit', { params: query })),
  modules: () => unwrap<string[]>(api.get('/audit/modules')),
  export: async (query: AuditListQuery = {}) => {
    const response = await api.get('/audit/export', { params: query, responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `audit_${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};

export const menusApi = {
  list: (portalType?: number) =>
    unwrap<AdminMenu[]>(api.get('/menus', { params: portalType ? { portalType } : {} })),
  create: (payload: MenuPayload & { menuCode: string }) =>
    unwrap<AdminMenu>(api.post('/menus', payload)),
  update: (id: number, payload: MenuPayload) => unwrap<AdminMenu>(api.put(`/menus/${id}`, payload)),
  remove: (id: number) => api.delete(`/menus/${id}`),
};
