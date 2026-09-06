import type { PagedRequest, PagedResult } from '@/models/api';
import type { Quotation, SaveQuotationPayload } from '@/models/quotations';
import { api, unwrap } from './client';

export interface QuotationListQuery extends PagedRequest {
  status?: number;
  customerId?: number;
  locationId?: number;
  from?: string;
  to?: string;
}

async function downloadPdf(id: number, quotationNumber: string) {
  const response = await api.get(`/quotations/${id}/pdf`, { responseType: 'blob' });
  const url = URL.createObjectURL(response.data as Blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = `${quotationNumber.replace(/[\\/]/g, '-')}.pdf`;
  anchor.click();
  URL.revokeObjectURL(url);
}

export const quotationsApi = {
  // Admin
  list: (query: QuotationListQuery = {}) =>
    unwrap<PagedResult<Quotation>>(api.get('/quotations', { params: query })),
  get: (id: number) => unwrap<Quotation>(api.get(`/quotations/${id}`)),
  create: (payload: SaveQuotationPayload) => unwrap<Quotation>(api.post('/quotations', payload)),
  update: (id: number, payload: SaveQuotationPayload) =>
    unwrap<Quotation>(api.put(`/quotations/${id}`, payload)),
  send: (id: number) => unwrap<Quotation>(api.post(`/quotations/${id}/send`)),
  cancel: (id: number, reason: string) =>
    unwrap<Quotation>(api.post(`/quotations/${id}/cancel`, { reason })),

  // Customer portal — scoped server-side to the caller
  mine: (query: QuotationListQuery = {}) =>
    unwrap<PagedResult<Quotation>>(api.get('/quotations/my', { params: query })),
  /** Opening a sent quotation marks it Viewed. */
  mineById: (id: number) => unwrap<Quotation>(api.get(`/quotations/my/${id}`)),
  approve: (id: number, reason?: string) =>
    unwrap<Quotation>(api.post(`/quotations/${id}/approve`, { reason: reason ?? null })),
  reject: (id: number, reason?: string) =>
    unwrap<Quotation>(api.post(`/quotations/${id}/reject`, { reason: reason ?? null })),

  pdf: downloadPdf,
};
