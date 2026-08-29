import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  AvailableWorkspace,
  Booking,
  BookingListQuery,
  ConfirmResult,
  PaymentOrder,
  Quote,
  Slot,
} from '@/models/bookings';
import { api, unwrap } from './client';

export const availabilityApi = {
  slots: (locationId: number, workspaceTypeId: number, date: string) =>
    unwrap<Slot[]>(api.get('/availability/slots', { params: { locationId, workspaceTypeId, date } })),
  workspaces: (locationId: number, workspaceTypeId: number, startUtc: string, durationMinutes: number) =>
    unwrap<AvailableWorkspace[]>(
      api.get('/availability/workspaces', {
        params: { locationId, workspaceTypeId, startUtc, durationMinutes },
      }),
    ),
};

export const bookingsApi = {
  quote: (workspaceId: number, startUtc: string, durationMinutes: number) =>
    unwrap<Quote>(api.post('/bookings/quote', { workspaceId, startUtc, durationMinutes })),
  hold: (workspaceId: number, startUtc: string, durationMinutes: number, notes?: string | null) =>
    unwrap<Booking>(api.post('/bookings', { workspaceId, startUtc, durationMinutes, notes })),
  confirm: (
    bookingId: number,
    payload:
      | { paymentMethod: 'wallet' | 'cash' }
      | {
          paymentMethod: 'gateway';
          gatewayOrderId: string;
          gatewayPaymentId: string;
          gatewaySignature: string;
        },
  ) => unwrap<ConfirmResult>(api.post(`/bookings/${bookingId}/confirm`, payload)),
  get: (bookingId: number) => unwrap<Booking>(api.get(`/bookings/${bookingId}`)),
  my: (tab: 'upcoming' | 'completed' | 'cancelled', query: PagedRequest = {}) =>
    unwrap<PagedResult<Booking>>(api.get('/bookings/my', { params: { tab, ...query } })),
  list: (query: BookingListQuery = {}) =>
    unwrap<PagedResult<Booking>>(api.get('/bookings', { params: query })),
  adminCreate: (payload: {
    customerId: number;
    workspaceId: number;
    startUtc: string;
    durationMinutes: number;
    notes?: string | null;
    paymentMethod: 'wallet' | 'cash';
  }) => unwrap<ConfirmResult>(api.post('/bookings/admin', payload)),
};

export interface CalendarItem {
  kind: 'booking' | 'block';
  referenceId: number;
  startUtc: string;
  endUtc: string;
  title: string;
  bookingStatus: number | null;
  blockReason: number | null;
  isOwn: boolean;
}

export interface CalendarWorkspaceRow {
  workspaceId: number;
  workspaceCode: string;
  workspaceName: string;
  workspaceTypeName: string;
  items: CalendarItem[];
}

export interface CalendarGrid {
  view: string;
  rangeStartUtc: string;
  rangeEndUtc: string;
  workspaces: CalendarWorkspaceRow[];
}

export const calendarApi = {
  get: (view: 'day' | 'week' | 'month', date: string, filters: {
    locationId?: number;
    workspaceTypeId?: number;
    workspaceId?: number;
  } = {}) =>
    unwrap<CalendarGrid>(api.get('/calendar', { params: { view, date, ...filters } })),
  block: (payload: {
    workspaceId: number;
    startUtc: string;
    endUtc: string;
    blockReason: number;
    reasonNotes?: string | null;
  }) => unwrap<{ workspaceBlockId: number }>(api.post('/calendar/block', payload)),
  unblock: (blockId: number) => api.post(`/calendar/unblock/${blockId}`),
};

export const paymentsApi = {
  createOrder: (bookingId: number) =>
    unwrap<PaymentOrder>(api.post('/payments/order', { bookingId })),
  sandboxComplete: (gatewayOrderId: string) =>
    unwrap<{ gatewayPaymentId: string; gatewaySignature: string }>(
      api.post('/payments/sandbox-complete', { gatewayOrderId }),
    ),
};

export const invoicesApi = {
  downloadPdf: async (invoiceId: number, invoiceNumber: string) => {
    const response = await api.get(`/invoices/${invoiceId}/pdf`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${invoiceNumber.replaceAll('/', '-')}.pdf`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};
