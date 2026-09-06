import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  AppNotification,
  BroadcastNotificationPayload,
  CustomerPass,
  Invoice,
  InvoicePaymentResult,
  PassPlan,
  PaymentRow,
  Wallet,
  WalletTransaction,
} from '@/models/finance';
import type { Booking, ConfirmResult, PaymentOrder } from '@/models/bookings';
import { api, unwrap } from './client';

export const walletApi = {
  me: () => unwrap<Wallet>(api.get('/wallet/me')),
  myTransactions: (query: PagedRequest & { category?: number } = {}) =>
    unwrap<PagedResult<WalletTransaction>>(api.get('/wallet/me/transactions', { params: query })),
  rechargeOrder: (amount: number) =>
    unwrap<PaymentOrder>(api.post('/wallet/recharge', { amount })),
  rechargeVerify: (gatewayOrderId: string, gatewayPaymentId: string, gatewaySignature: string) =>
    unwrap<{ wallet: Wallet; transactionNumber: string }>(
      api.post('/wallet/recharge/verify', { gatewayOrderId, gatewayPaymentId, gatewaySignature }),
    ),
  ofCustomer: (customerId: number) => unwrap<Wallet>(api.get(`/wallet/${customerId}`)),
  customerTransactions: (customerId: number, query: PagedRequest = {}) =>
    unwrap<PagedResult<WalletTransaction>>(
      api.get(`/wallet/${customerId}/transactions`, { params: query }),
    ),
  adjust: (customerId: number, direction: 'credit' | 'debit', amount: number, reason: string) =>
    unwrap<Wallet>(api.post(`/wallet/${customerId}/adjust`, { direction, amount, reason })),
};

export const invoiceApi = {
  my: (query: PagedRequest & { status?: 'pending' | 'paid' } = {}) =>
    unwrap<PagedResult<Invoice>>(api.get('/invoices/my', { params: query })),
  list: (query: PagedRequest & { status?: number; overdue?: boolean } = {}) =>
    unwrap<PagedResult<Invoice>>(api.get('/invoices', { params: query })),
  get: (invoiceId: number) => unwrap<Invoice>(api.get(`/invoices/${invoiceId}`)),
  /** Rules 4 and 5: an approved quotation is the only source of a new invoice. */
  fromQuotation: (quotationId: number) =>
    unwrap<Invoice>(api.post(`/invoices/from-quotation/${quotationId}`)),
  send: (invoiceId: number) => unwrap<Invoice>(api.post(`/invoices/${invoiceId}/send`)),
  cancel: (invoiceId: number, reason: string) =>
    unwrap<Invoice>(api.post(`/invoices/${invoiceId}/cancel`, { reason })),
};

export const passApi = {
  activePlans: (type?: 1 | 2) =>
    unwrap<PassPlan[]>(api.get('/pass-plans/active', { params: type ? { type } : {} })),
  allPlans: () => unwrap<PassPlan[]>(api.get('/pass-plans')),
  createPlan: (payload: Omit<PassPlan, 'passPlanId' | 'locationName' | 'cgstAmount' | 'sgstAmount' | 'totalAmount'>) =>
    unwrap<PassPlan>(api.post('/pass-plans', payload)),
  updatePlan: (id: number, payload: Omit<PassPlan, 'passPlanId' | 'locationName' | 'cgstAmount' | 'sgstAmount' | 'totalAmount'>) =>
    unwrap<PassPlan>(api.put(`/pass-plans/${id}`, payload)),
  deletePlan: (id: number) => api.delete(`/pass-plans/${id}`),
  order: (passPlanId: number) => unwrap<PaymentOrder>(api.post('/passes/order', { passPlanId })),
  purchase: (payload: {
    passPlanId: number;
    validFrom: string;
    paymentMethod: 'wallet' | 'gateway' | 'cash';
    gatewayOrderId?: string;
    gatewayPaymentId?: string;
    gatewaySignature?: string;
  }) =>
    unwrap<{
      pass: CustomerPass;
      paymentNumber: string;
      invoiceId: number;
      invoiceNumber: string;
      qrPayload: string;
    }>(api.post('/passes/purchase', payload)),
  my: (query: PagedRequest = {}) =>
    unwrap<PagedResult<CustomerPass>>(api.get('/passes/my', { params: query })),
  list: (query: PagedRequest & { passType?: number; status?: number } = {}) =>
    unwrap<PagedResult<CustomerPass>>(api.get('/passes', { params: query })),
  cancel: (id: number) => unwrap<CustomerPass>(api.post(`/passes/${id}/cancel`)),
};

export const notificationApi = {
  my: (query: PagedRequest & { isRead?: boolean } = {}) =>
    unwrap<PagedResult<AppNotification>>(api.get('/notifications/my', { params: query })),
  unreadCount: () => unwrap<number>(api.get('/notifications/my/unread-count')),
  markRead: (id: number) => api.patch(`/notifications/${id}/read`),
  readAll: () => api.post('/notifications/read-all'),
  remove: (id: number) => api.delete(`/notifications/${id}`),
  /** Announcement to every customer or every admin-portal user; returns the recipient count. */
  broadcast: (payload: BroadcastNotificationPayload) =>
    unwrap<number>(api.post('/notifications/broadcast', payload)),
};

export const paymentAdminApi = {
  list: (query: PagedRequest & { status?: number; paymentMethod?: number; customerId?: number } = {}) =>
    unwrap<PagedResult<PaymentRow>>(api.get('/payments', { params: query })),
};

/** Pay Now against an invoice (requirements §11). */
export const invoicePaymentApi = {
  createOrder: (invoiceId: number, amount?: number) =>
    unwrap<PaymentOrder>(api.post('/payments/invoice-order', { invoiceId, amount: amount ?? null })),
  verify: (gatewayOrderId: string, gatewayPaymentId: string, gatewaySignature: string) =>
    unwrap<InvoicePaymentResult>(api.post('/payments/invoice-verify', {
      gatewayOrderId, gatewayPaymentId, gatewaySignature,
    })),
  receipt: async (paymentId: number, paymentNumber: string) => {
    const response = await api.get(`/payments/${paymentId}/receipt`, { responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `${paymentNumber}.pdf`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};

export const bookingMutationApi = {
  cancel: (bookingId: number, reason?: string) =>
    unwrap<{ booking: Booking; refundAmount: number; refundPercent: number }>(
      api.post(`/bookings/${bookingId}/cancel`, { reason }),
    ),
  reschedule: (bookingId: number, newStartUtc: string) =>
    unwrap<Booking>(api.put(`/bookings/${bookingId}`, { newStartUtc })),
  extend: (bookingId: number, additionalMinutes: number) =>
    unwrap<{ booking: Booking; additionalAmount: number; invoiceNumber: string }>(
      api.post(`/bookings/${bookingId}/extend`, { additionalMinutes, paymentMethod: 'wallet' }),
    ),
};

export type { ConfirmResult };
