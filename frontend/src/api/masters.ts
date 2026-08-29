import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  Amenity,
  Location,
  SaveLocationPayload,
  SaveWorkspacePayload,
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
