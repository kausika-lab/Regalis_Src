import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  CreateTicketPayload,
  TicketCategory,
  TicketDetail,
  TicketListItem,
} from '@/models/support';
import { api, unwrap } from './client';

export interface TicketListQuery extends PagedRequest {
  status?: number;
  priority?: number;
  ticketCategoryId?: number;
  assignedToUserId?: number;
  from?: string;
  to?: string;
}

export const ticketsApi = {
  categories: () => unwrap<TicketCategory[]>(api.get('/ticket-categories')),
  create: (payload: CreateTicketPayload) => unwrap<TicketDetail>(api.post('/tickets', payload)),
  my: (query: PagedRequest = {}) =>
    unwrap<PagedResult<TicketListItem>>(api.get('/tickets/my', { params: query })),
  list: (query: TicketListQuery = {}) =>
    unwrap<PagedResult<TicketListItem>>(api.get('/tickets', { params: query })),
  get: (id: number) => unwrap<TicketDetail>(api.get(`/tickets/${id}`)),
  addMessage: (id: number, message: string, isInternalNote = false) =>
    unwrap<TicketDetail>(api.post(`/tickets/${id}/messages`, { message, isInternalNote })),
  addAttachment: (id: number, file: File) => {
    const form = new FormData();
    form.append('file', file, file.name);
    return unwrap<TicketDetail>(api.post(`/tickets/${id}/attachments`, form, {
      headers: { 'Content-Type': 'multipart/form-data' },
    }));
  },
  downloadAttachment: async (id: number, attachmentId: number, fileName: string) => {
    const response = await api.get(`/tickets/${id}/attachments/${attachmentId}`, {
      responseType: 'blob',
    });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = fileName;
    anchor.click();
    URL.revokeObjectURL(url);
  },
  assign: (id: number, assignedToUserId: number) =>
    unwrap<TicketDetail>(api.patch(`/tickets/${id}/assign`, { assignedToUserId })),
  changeStatus: (id: number, status: number, resolutionNotes?: string) =>
    unwrap<TicketDetail>(api.patch(`/tickets/${id}/status`, {
      status, resolutionNotes: resolutionNotes ?? null,
    })),
  changePriority: (id: number, priority: number) =>
    unwrap<TicketDetail>(api.patch(`/tickets/${id}/priority`, { priority })),
  close: (id: number) => unwrap<TicketDetail>(api.post(`/tickets/${id}/close`)),
  reopen: (id: number) => unwrap<TicketDetail>(api.post(`/tickets/${id}/reopen`)),
  export: async (query: TicketListQuery = {}) => {
    const response = await api.get('/tickets/export', { params: query, responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `tickets_${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};
