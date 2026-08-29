import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  Amenity,
  AppSetting,
  Banner,
  Location,
  PublicSettings,
  SaveBannerPayload,
  SaveLocationPayload,
  SaveServiceItemPayload,
  SaveWorkspacePayload,
  ServiceItem,
  Workspace,
  WorkspaceListQuery,
  WorkspaceType,
} from '@/models/masters';
import { api, unwrap } from './client';

export const locationsApi = {
  list: (query: PagedRequest = {}) =>
    unwrap<PagedResult<Location>>(api.get('/locations', { params: query })),
  get: (id: number) => unwrap<Location>(api.get(`/locations/${id}`)),
  create: (payload: SaveLocationPayload) => unwrap<Location>(api.post('/locations', payload)),
  update: (id: number, payload: SaveLocationPayload) =>
    unwrap<Location>(api.put(`/locations/${id}`, payload)),
  remove: (id: number) => api.delete(`/locations/${id}`),
};

export const workspaceTypesApi = {
  list: () => unwrap<WorkspaceType[]>(api.get('/workspace-types')),
};

export const amenitiesApi = {
  list: () => unwrap<Amenity[]>(api.get('/amenities')),
  create: (payload: { amenityName: string; iconName?: string | null; displayOrder: number }) =>
    unwrap<Amenity>(api.post('/amenities', payload)),
};

export interface ServiceItemListQuery extends PagedRequest {
  category?: string;
  locationId?: number;
  includeUnavailable?: boolean;
}

export const servicesApi = {
  list: (query: ServiceItemListQuery = {}) =>
    unwrap<PagedResult<ServiceItem>>(api.get('/services', { params: query })),
  categories: () => unwrap<string[]>(api.get('/services/categories')),
  create: (payload: SaveServiceItemPayload) =>
    unwrap<ServiceItem>(api.post('/services', payload)),
  update: (id: number, payload: SaveServiceItemPayload) =>
    unwrap<ServiceItem>(api.put(`/services/${id}`, payload)),
  remove: (id: number) => api.delete(`/services/${id}`),
};

export const bannersApi = {
  list: (query: PagedRequest = {}) =>
    unwrap<PagedResult<Banner>>(api.get('/banners', { params: query })),
  active: (locationId?: number) =>
    unwrap<Banner[]>(api.get('/banners/active', { params: { locationId } })),
  create: (payload: SaveBannerPayload) => unwrap<Banner>(api.post('/banners', payload)),
  update: (id: number, payload: SaveBannerPayload) =>
    unwrap<Banner>(api.put(`/banners/${id}`, payload)),
  remove: (id: number) => api.delete(`/banners/${id}`),
};

export const settingsApi = {
  list: () => unwrap<AppSetting[]>(api.get('/settings')),
  update: (settings: { settingKey: string; settingValue: string }[]) =>
    unwrap<AppSetting[]>(api.put('/settings', { settings })),
  public: () => unwrap<PublicSettings>(api.get('/settings/public')),
};

export const workspacesApi = {
  list: (query: WorkspaceListQuery = {}) =>
    unwrap<PagedResult<Workspace>>(api.get('/workspaces', { params: query })),
  get: (id: number) => unwrap<Workspace>(api.get(`/workspaces/${id}`)),
  create: (payload: SaveWorkspacePayload) => unwrap<Workspace>(api.post('/workspaces', payload)),
  update: (id: number, payload: SaveWorkspacePayload) =>
    unwrap<Workspace>(api.put(`/workspaces/${id}`, payload)),
  updateStatus: (id: number, status: number) =>
    unwrap<Workspace>(api.patch(`/workspaces/${id}/status`, { status })),
  remove: (id: number) => api.delete(`/workspaces/${id}`),
};
