import type { ReportFilters, ReportInfo, ReportResult } from '@/models/reports';
import { api, unwrap } from './client';

export interface DashboardCard {
  key: string;
  label: string;
  value: string;
  hint: string | null;
  route: string | null;
}

export interface AdminDashboard {
  cards: DashboardCard[];
  generatedUtc: string;
}

export interface DashboardBooking {
  bookingId: number;
  bookingNumber: string;
  workspaceName: string;
  workspaceTypeName: string;
  startUtc: string;
  endUtc: string;
  status: number;
}

export interface CustomerDashboard {
  walletBalance: number;
  activePasses: number;
  unreadNotifications: number;
  openTickets: number;
  todaysBooking: DashboardBooking | null;
  nextBooking: DashboardBooking | null;
}

export const dashboardApi = {
  admin: () => unwrap<AdminDashboard>(api.get('/dashboard/admin')),
  customer: () => unwrap<CustomerDashboard>(api.get('/dashboard/customer')),
};

export const reportsApi = {
  catalogue: () => unwrap<ReportInfo[]>(api.get('/reports')),
  get: (key: string, filters: ReportFilters = {}) =>
    unwrap<ReportResult>(api.get(`/reports/${key}`, { params: filters })),
  export: async (key: string, format: 'xlsx' | 'pdf', filters: ReportFilters = {}) => {
    const response = await api.get(`/reports/${key}/export`, {
      params: { ...filters, format },
      responseType: 'blob',
    });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${key}-report.${format}`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};
