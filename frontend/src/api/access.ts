import type { PagedRequest, PagedResult } from '@/models/api';
import { api, unwrap } from './client';

export interface MyAccessToken {
  accessTokenId: number;
  qrPayload: string;
  tokenPurpose: number; // 1 booking, 2 day pass, 3 monthly pass, 4 housekeeping
  reference: string;
  workspaceName: string | null;
  validFromUtc: string;
  validToUtc: string;
  status: number;
}

export interface AccessDecision {
  isGranted: boolean;
  result: number; // 1 granted, 2 denied, 3 device error
  denyReason: string | null;
  accessLogId: number;
  personName: string | null;
  reference: string | null;
  validToUtc: string | null;
}

export interface AccessLogRow {
  accessLogId: number;
  userId: number | null;
  personName: string | null;
  workspaceId: number | null;
  locationId: number;
  direction: number;
  accessMethod: number;
  result: number;
  denyReason: string | null;
  deviceId: string | null;
  isManualOverride: boolean;
  overrideReason: string | null;
  loggedUtc: string;
}

export interface AttendanceRow {
  attendanceId: number;
  userId: number;
  personName: string;
  subjectType: number;
  locationId: number;
  workspaceId: number | null;
  bookingId: number | null;
  attendanceDate: string;
  checkInUtc: string;
  checkOutUtc: string | null;
  totalMinutes: number | null;
  status: number; // 1 in, 2 out, 3 auto-closed
  checkInMethod: number;
  isCorrected: boolean;
  correctionReason: string | null;
  originalCheckInUtc: string | null;
  originalCheckOutUtc: string | null;
}

export const accessApi = {
  myTokens: () => unwrap<MyAccessToken[]>(api.get('/access/my-tokens')),
  validate: (payload: string, direction: 1 | 2, deviceId?: string, workspaceId?: number) =>
    unwrap<AccessDecision>(api.post('/access/validate', {
      payload, direction, deviceId: deviceId ?? null, workspaceId: workspaceId ?? null,
      accessMethod: 1,
    })),
  manualOverride: (payload: {
    userId: number;
    locationId: number;
    workspaceId?: number | null;
    direction: 1 | 2;
    bookingId?: number | null;
    reason: string;
  }) => unwrap<AccessDecision>(api.post('/access/manual-override', payload)),
  logs: (query: PagedRequest & { result?: number } = {}) =>
    unwrap<PagedResult<AccessLogRow>>(api.get('/access/logs', { params: query })),
};

export const attendanceApi = {
  checkIn: (workspaceId?: number) =>
    unwrap<AttendanceRow>(api.post('/attendance/check-in', { workspaceId: workspaceId ?? null })),
  checkOut: () => unwrap<AttendanceRow>(api.post('/attendance/check-out')),
  my: (query: PagedRequest = {}) =>
    unwrap<PagedResult<AttendanceRow>>(api.get('/attendance/my', { params: query })),
  myOpen: () => unwrap<AttendanceRow | null>(api.get('/attendance/my/open')),
  list: (query: PagedRequest & { date?: string; userId?: number } = {}) =>
    unwrap<PagedResult<AttendanceRow>>(api.get('/attendance', { params: query })),
  correct: (attendanceId: number, checkInUtc: string, checkOutUtc: string | null, reason: string) =>
    unwrap<AttendanceRow>(api.put(`/attendance/${attendanceId}`, { checkInUtc, checkOutUtc, reason })),
  payrollExport: async (from: string, to: string) => {
    const response = await api.get(`/payroll/export?from=${from}&to=${to}`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `payroll_${from}_${to}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};

export const ACCESS_RESULT_LABELS: Record<number, string> = {
  1: 'Granted',
  2: 'Denied',
  3: 'Device error',
};

export const ATTENDANCE_STATUS_LABELS: Record<number, string> = {
  1: 'Checked in',
  2: 'Checked out',
  3: 'Auto-closed',
};
