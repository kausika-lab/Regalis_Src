export type Portal = 'customer' | 'admin';

export interface LoginRequest {
  email: string;
  password: string;
  portal: Portal;
}

export interface AuthUser {
  userId: number;
  fullName: string;
  email: string;
  userType: number;
  photoPath?: string | null;
  defaultLocationId?: number | null;
}

export interface MenuItem {
  menuId: number;
  parentMenuId: number | null;
  menuCode: string;
  displayName: string;
  route: string | null;
  icon: string | null;
  displayOrder: number;
  menuLevel: number;
  children: MenuItem[];
}

export interface LoginResponse {
  accessToken: string;
  accessTokenExpiresUtc: string;
  refreshToken: string;
  user: AuthUser;
  roles: string[];
  permissions: string[];
}

export interface RefreshRequest {
  refreshToken: string;
}
