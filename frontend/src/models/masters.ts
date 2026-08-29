import type { PagedRequest } from './api';

export interface Location {
  locationId: number;
  locationCode: string;
  locationName: string;
  addressLine1: string | null;
  addressLine2: string | null;
  city: string | null;
  state: string | null;
  postalCode: string | null;
  contactNumber: string | null;
  email: string | null;
  workingHoursStart: string;
  workingHoursEnd: string;
  workingDays: string;
  latitude: number | null;
  longitude: number | null;
  description: string | null;
  isDefault: boolean;
  isActive: boolean;
  workspaceCount: number;
}

export interface SaveLocationPayload {
  locationCode: string;
  locationName: string;
  addressLine1?: string | null;
  addressLine2?: string | null;
  city?: string | null;
  state?: string | null;
  postalCode?: string | null;
  contactNumber?: string | null;
  email?: string | null;
  workingHoursStart: string;
  workingHoursEnd: string;
  workingDays: string[];
  latitude?: number | null;
  longitude?: number | null;
  description?: string | null;
  isDefault: boolean;
}

export interface WorkspaceType {
  workspaceTypeId: number;
  typeCode: string;
  typeName: string;
  iconName: string | null;
  displayOrder: number;
  requiresCapacity: boolean;
  workspaceCount: number;
}

export interface Amenity {
  amenityId: number;
  amenityName: string;
  iconName: string | null;
  displayOrder: number;
}

export interface Workspace {
  workspaceId: number;
  locationId: number;
  locationName: string;
  workspaceTypeId: number;
  workspaceTypeName: string;
  workspaceCode: string;
  workspaceName: string;
  floorNumber: string | null;
  capacity: number;
  pricePerHour: number;
  pricePerDay: number | null;
  equipment: string | null;
  description: string | null;
  status: number;
  isBookable: boolean;
  isActive: boolean;
  amenities: Amenity[];
}

export interface WorkspaceListQuery extends PagedRequest {
  locationId?: number;
  workspaceTypeId?: number;
  status?: number;
}

export interface SaveWorkspacePayload {
  locationId: number;
  workspaceTypeId: number;
  workspaceCode: string;
  workspaceName: string;
  floorNumber?: string | null;
  capacity: number;
  pricePerHour: number;
  pricePerDay?: number | null;
  equipment?: string | null;
  description?: string | null;
  isBookable: boolean;
  amenityIds: number[];
}

export const WORKSPACE_STATUS_LABELS: Record<number, string> = {
  1: 'Available',
  2: 'Occupied',
  3: 'Maintenance',
  4: 'Disabled',
};

export const WEEK_DAYS = ['MON', 'TUE', 'WED', 'THU', 'FRI', 'SAT', 'SUN'] as const;
