import type { LoginRequest, LoginResponse, MenuItem } from '@/models/auth';
import { api, unwrap } from './client';

/** Contracts from API_PLAN §2 — /api/auth. */
export const authApi = {
  login: (request: LoginRequest) => unwrap<LoginResponse>(api.post('/auth/login', request)),

  logout: (refreshToken: string) => api.post('/auth/logout', { refreshToken }),

  me: () => unwrap<LoginResponse['user']>(api.get('/auth/me')),

  myMenus: () => unwrap<MenuItem[]>(api.get('/menus/my')),
};
