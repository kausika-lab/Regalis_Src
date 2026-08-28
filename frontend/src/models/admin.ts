import type { PagedRequest } from './api';

// ── Users ──────────────────────────────────────────────────────────────────
export interface UserListItem {
  userId: number;
  fullName: string;
  email: string;
  mobileNumber: string;
  userType: number;
  status: number;
  companyName: string | null;
  roles: string[];
  lastLoginUtc: string | null;
  createdDate: string;
  isActive: boolean;
}

export interface UserDetail extends UserListItem {
  address: string | null;
  photoPath: string | null;
  defaultLocationId: number | null;
  isEmailVerified: boolean;
  isMobileVerified: boolean;
  mustChangePassword: boolean;
  lockoutEndUtc: string | null;
  roleIds: number[];
}

export interface UserListQuery extends PagedRequest {
  userType?: number;
  status?: number;
  roleId?: number;
}

export interface CreateUserPayload {
  fullName: string;
  email: string;
  mobileNumber: string;
  password: string;
  userType: number;
  roleIds: number[];
  companyName?: string | null;
  defaultLocationId?: number | null;
}

export interface UpdateUserPayload {
  fullName: string;
  mobileNumber: string;
  roleIds: number[];
  companyName?: string | null;
  address?: string | null;
  defaultLocationId?: number | null;
}

// ── Roles / permissions ────────────────────────────────────────────────────
export interface Role {
  roleId: number;
  roleCode: string;
  roleName: string;
  description: string | null;
  portalType: number;
  isSystemRole: boolean;
  isActive: boolean;
  userCount: number;
  permissionCount: number;
}

export interface RolePermissionRow {
  permissionId: number;
  permissionCode: string;
  moduleName: string;
  actionName: string;
  isGranted: boolean;
}

export interface RoleMenuRow {
  menuId: number;
  menuCode: string;
  displayName: string;
  portalType: number;
  isGranted: boolean;
}

// ── Menus ──────────────────────────────────────────────────────────────────
export interface AdminMenu {
  menuId: number;
  parentMenuId: number | null;
  menuCode: string;
  menuName: string;
  displayName: string;
  route: string | null;
  icon: string | null;
  displayOrder: number;
  permissionId: number | null;
  portalType: number;
  menuLevel: number;
  isVisible: boolean;
  isActive: boolean;
}

export interface MenuPayload {
  parentMenuId: number | null;
  menuCode?: string;
  menuName: string;
  displayName: string;
  route: string | null;
  icon: string | null;
  displayOrder: number;
  permissionId: number | null;
  portalType: number;
  isVisible: boolean;
}

export const USER_TYPE_LABELS: Record<number, string> = {
  1: 'Customer',
  2: 'Admin',
  3: 'Staff',
};

export const USER_STATUS_LABELS: Record<number, string> = {
  1: 'Active',
  2: 'Inactive',
  3: 'Locked',
  4: 'Pending verification',
};

export const PORTAL_TYPE_LABELS: Record<number, string> = {
  1: 'Customer',
  2: 'Admin',
  3: 'Both',
};
