import type { PagedResult } from '@/models/api';
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

export const menusApi = {
  list: (portalType?: number) =>
    unwrap<AdminMenu[]>(api.get('/menus', { params: portalType ? { portalType } : {} })),
  create: (payload: MenuPayload & { menuCode: string }) =>
    unwrap<AdminMenu>(api.post('/menus', payload)),
  update: (id: number, payload: MenuPayload) => unwrap<AdminMenu>(api.put(`/menus/${id}`, payload)),
  remove: (id: number) => api.delete(`/menus/${id}`),
};
