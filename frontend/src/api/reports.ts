import type { ReportFilters, ReportInfo, ReportResult } from '@/models/reports';
import { api, unwrap } from './client';

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
