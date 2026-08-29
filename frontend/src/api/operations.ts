import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  CreateInventoryTransactionPayload,
  HousekeepingSchedule,
  InventoryItem,
  InventoryStockRow,
  InventoryTransaction,
  SaveHousekeepingPayload,
  SaveInventoryItemPayload,
} from '@/models/operations';
import { api, unwrap } from './client';

export interface HousekeepingListQuery extends PagedRequest {
  from?: string;
  to?: string;
  status?: number;
  staffUserId?: number;
  locationId?: number;
}

export const housekeepingApi = {
  list: (query: HousekeepingListQuery = {}) =>
    unwrap<PagedResult<HousekeepingSchedule>>(
      api.get('/housekeeping/schedules', { params: query })),
  my: (query: PagedRequest = {}) =>
    unwrap<PagedResult<HousekeepingSchedule>>(
      api.get('/housekeeping/my-schedules', { params: query })),
  create: (payload: SaveHousekeepingPayload) =>
    unwrap<HousekeepingSchedule>(api.post('/housekeeping/schedules', payload)),
  update: (id: number, payload: SaveHousekeepingPayload) =>
    unwrap<HousekeepingSchedule>(api.put(`/housekeeping/schedules/${id}`, payload)),
  cancel: (id: number) =>
    unwrap<HousekeepingSchedule>(api.post(`/housekeeping/schedules/${id}/cancel`)),
  checkIn: (id: number) =>
    unwrap<HousekeepingSchedule>(api.post(`/housekeeping/schedules/${id}/check-in`)),
  checkOut: (id: number, completionNotes?: string) =>
    unwrap<HousekeepingSchedule>(api.post(`/housekeeping/schedules/${id}/check-out`, {
      completionNotes: completionNotes ?? null,
    })),
};

export interface InventoryItemListQuery extends PagedRequest {
  category?: string;
  locationId?: number;
  belowReorder?: boolean;
}

export interface InventoryTransactionListQuery extends PagedRequest {
  inventoryItemId?: number;
  transactionType?: number;
  from?: string;
  to?: string;
  locationId?: number;
}

export const inventoryApi = {
  items: (query: InventoryItemListQuery = {}) =>
    unwrap<PagedResult<InventoryItem>>(api.get('/inventory/items', { params: query })),
  item: (id: number) => unwrap<InventoryItem>(api.get(`/inventory/items/${id}`)),
  createItem: (payload: SaveInventoryItemPayload) =>
    unwrap<InventoryItem>(api.post('/inventory/items', payload)),
  updateItem: (id: number, payload: SaveInventoryItemPayload) =>
    unwrap<InventoryItem>(api.put(`/inventory/items/${id}`, payload)),
  removeItem: (id: number) => api.delete(`/inventory/items/${id}`),
  stock: (locationId?: number, belowReorder?: boolean) =>
    unwrap<InventoryStockRow[]>(api.get('/inventory/stock', {
      params: { locationId, belowReorder },
    })),
  transactions: (query: InventoryTransactionListQuery = {}) =>
    unwrap<PagedResult<InventoryTransaction>>(
      api.get('/inventory/transactions', { params: query })),
  createTransaction: (payload: CreateInventoryTransactionPayload) =>
    unwrap<InventoryTransaction>(api.post('/inventory/transactions', payload)),
  export: async (query: InventoryTransactionListQuery = {}) => {
    const response = await api.get('/inventory/export', { params: query, responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `inventory_${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};
