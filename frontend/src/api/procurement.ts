import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  Expense,
  ExpenseSummary,
  PurchaseOrder,
  ReceiveGoodsPayload,
  SaveExpensePayload,
  SavePurchaseOrderPayload,
  SaveSupplierPayload,
  Supplier,
} from '@/models/procurement';
import { api, unwrap } from './client';

export interface SupplierListQuery extends PagedRequest {
  locationId?: number;
  isActive?: boolean;
}

export const suppliersApi = {
  list: (query: SupplierListQuery = {}) =>
    unwrap<PagedResult<Supplier>>(api.get('/suppliers', { params: query })),
  get: (id: number) => unwrap<Supplier>(api.get(`/suppliers/${id}`)),
  create: (payload: SaveSupplierPayload) => unwrap<Supplier>(api.post('/suppliers', payload)),
  update: (id: number, payload: SaveSupplierPayload) =>
    unwrap<Supplier>(api.put(`/suppliers/${id}`, payload)),
  remove: (id: number) => api.delete(`/suppliers/${id}`),
};

export interface PurchaseOrderListQuery extends PagedRequest {
  status?: number;
  supplierId?: number;
  locationId?: number;
  from?: string;
  to?: string;
}

export const purchaseOrdersApi = {
  list: (query: PurchaseOrderListQuery = {}) =>
    unwrap<PagedResult<PurchaseOrder>>(api.get('/purchase-orders', { params: query })),
  get: (id: number) => unwrap<PurchaseOrder>(api.get(`/purchase-orders/${id}`)),
  create: (payload: SavePurchaseOrderPayload) =>
    unwrap<PurchaseOrder>(api.post('/purchase-orders', payload)),
  update: (id: number, payload: SavePurchaseOrderPayload) =>
    unwrap<PurchaseOrder>(api.put(`/purchase-orders/${id}`, payload)),
  send: (id: number) => unwrap<PurchaseOrder>(api.post(`/purchase-orders/${id}/send`)),
  receive: (id: number, payload: ReceiveGoodsPayload) =>
    unwrap<PurchaseOrder>(api.post(`/purchase-orders/${id}/receive`, payload)),
  cancel: (id: number, reason: string) =>
    unwrap<PurchaseOrder>(api.post(`/purchase-orders/${id}/cancel`, { reason })),
};

export interface ExpenseListQuery extends PagedRequest {
  category?: string;
  paymentMethod?: number;
  locationId?: number;
  supplierId?: number;
  from?: string;
  to?: string;
}

export const expensesApi = {
  list: (query: ExpenseListQuery = {}) =>
    unwrap<PagedResult<Expense>>(api.get('/expenses', { params: query })),
  get: (id: number) => unwrap<Expense>(api.get(`/expenses/${id}`)),
  summary: (query: ExpenseListQuery = {}) =>
    unwrap<ExpenseSummary>(api.get('/expenses/summary', { params: query })),
  categories: () => unwrap<string[]>(api.get('/expenses/categories')),
  create: (payload: SaveExpensePayload) => unwrap<Expense>(api.post('/expenses', payload)),
  update: (id: number, payload: SaveExpensePayload) =>
    unwrap<Expense>(api.put(`/expenses/${id}`, payload)),
  remove: (id: number) => api.delete(`/expenses/${id}`),
  export: async (query: ExpenseListQuery = {}) => {
    const response = await api.get('/expenses/export', { params: query, responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `expenses_${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};
