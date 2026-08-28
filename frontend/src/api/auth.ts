import type { LoginRequest, LoginResponse, MenuItem } from '@/models/auth';
import { api, unwrap } from './client';

export interface RegisterPayload {
  fullName: string;
  email: string;
  mobileNumber: string;
  password: string;
  confirmPassword: string;
  companyName?: string | null;
}

export type OtpPurpose = 'registration' | 'forgot-password';

/** Contracts from API_PLAN §2 — /api/auth. */
export const authApi = {
  login: (request: LoginRequest) => unwrap<LoginResponse>(api.post('/auth/login', request)),

  register: (payload: RegisterPayload) =>
    unwrap<{ destination: string; otpTtlMinutes: number }>(api.post('/auth/register', payload)),

  verifyOtp: (destination: string, purpose: OtpPurpose, code: string) =>
    unwrap<{ verified: boolean; resetToken: string | null }>(
      api.post('/auth/verify-otp', { destination, purpose, code }),
    ),

  resendOtp: (destination: string, purpose: OtpPurpose) =>
    api.post('/auth/resend-otp', { destination, purpose }),

  forgotPassword: (email: string) => api.post('/auth/forgot-password', { email }),

  resetPassword: (resetToken: string, newPassword: string, confirmPassword: string) =>
    api.post('/auth/reset-password', { resetToken, newPassword, confirmPassword }),

  changePassword: (currentPassword: string, newPassword: string, confirmPassword: string) =>
    api.post('/auth/change-password', { currentPassword, newPassword, confirmPassword }),

  logout: (refreshToken: string) => api.post('/auth/logout', { refreshToken }),

  me: () => unwrap<LoginResponse['user']>(api.get('/auth/me')),

  myMenus: () => unwrap<MenuItem[]>(api.get('/menus/my')),
};
