import { create } from 'zustand';
import { persist } from 'zustand/middleware';
import type { AuthUser, LoginResponse, MenuItem } from '@/models/auth';

interface AuthState {
  accessToken: string | null;
  refreshToken: string | null;
  user: AuthUser | null;
  roles: string[];
  permissions: string[];
  menus: MenuItem[];

  isAuthenticated: () => boolean;
  hasPermission: (code: string) => boolean;
  setSession: (login: LoginResponse) => void;
  updateUser: (patch: Partial<AuthUser>) => void;
  setTokens: (accessToken: string, refreshToken: string) => void;
  setMenus: (menus: MenuItem[]) => void;
  clearSession: () => void;
}

/**
 * Session store. Persisted so a reload keeps the session; the refresh token is
 * rotated server-side on every use and revoked on logout, so a stale copy is
 * unusable. Permission checks here are cosmetic — the API is the enforcement
 * point (ROLE_PERMISSION_MATRIX §6).
 */
export const useAuthStore = create<AuthState>()(
  persist(
    (set, get) => ({
      accessToken: null,
      refreshToken: null,
      user: null,
      roles: [],
      permissions: [],
      menus: [],

      isAuthenticated: () => get().accessToken !== null,

      hasPermission: (code) => get().permissions.includes(code),

      setSession: (login) =>
        set({
          accessToken: login.accessToken,
          refreshToken: login.refreshToken,
          user: login.user,
          roles: login.roles,
          permissions: login.permissions,
        }),

      /** Keeps the session's copy of the identity in step when the user edits
       *  their own profile — the topbar and dashboard greeting read from here,
       *  and previously only a fresh sign-in refreshed them. */
      updateUser: (patch) =>
        set((state) => (state.user ? { user: { ...state.user, ...patch } } : {})),

      setTokens: (accessToken, refreshToken) => set({ accessToken, refreshToken }),

      setMenus: (menus) => set({ menus }),

      clearSession: () =>
        set({
          accessToken: null,
          refreshToken: null,
          user: null,
          roles: [],
          permissions: [],
          menus: [],
        }),
    }),
    { name: 'regalis-auth' },
  ),
);
