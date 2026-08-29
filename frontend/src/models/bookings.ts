import type { PagedRequest } from './api';

export interface Slot {
  startUtc: string;
  endUtc: string;
  isAvailable: boolean;
}

export interface AvailableWorkspace {
  workspaceId: number;
  workspaceCode: string;
  workspaceName: string;
  workspaceTypeName: string;
  floorNumber: string | null;
  capacity: number;
  pricePerHour: number;
  equipment: string | null;
  amenities: string[];
}

export interface Quote {
  workspaceId: number;
  workspaceName: string;
  startUtc: string;
  endUtc: string;
  durationMinutes: number;
  baseAmount: number;
  cgstPercent: number;
  cgstAmount: number;
  sgstPercent: number;
  sgstAmount: number;
  totalAmount: number;
}

export interface Booking {
  bookingId: number;
  bookingNumber: string;
  customerId: number;
  customerName: string;
  locationId: number;
  workspaceId: number;
  workspaceCode: string;
  workspaceName: string;
  workspaceTypeName: string;
  startUtc: string;
  endUtc: string;
  durationMinutes: number;
  baseAmount: number;
  taxAmount: number;
  totalAmount: number;
  status: number;
  paymentStatus: number;
  bookingSource: number;
  notes: string | null;
  createdDate: string;
  holdExpiresUtc: string | null;
}

export interface ConfirmResult {
  booking: Booking;
  paymentId: number;
  paymentNumber: string;
  invoiceId: number;
  invoiceNumber: string;
  accessTokenValue: string;
  qrPayload: string;
}

export interface PaymentOrder {
  paymentId: number;
  paymentNumber: string;
  gatewayOrderId: string;
  keyId: string;
  amount: number;
  currency: string;
  provider: string;
}

export interface BookingListQuery extends PagedRequest {
  from?: string;
  to?: string;
  status?: number;
  locationId?: number;
  workspaceTypeId?: number;
  customerId?: number;
}

export const BOOKING_STATUS_LABELS: Record<number, string> = {
  1: 'Pending payment',
  2: 'Confirmed',
  3: 'Checked in',
  4: 'Completed',
  5: 'Cancelled',
  6: 'Expired',
  7: 'No show',
};

export const BOOKING_STATUS_TONES: Record<number, 'warning' | 'success' | 'info' | 'neutral' | 'danger'> = {
  1: 'warning',
  2: 'success',
  3: 'info',
  4: 'neutral',
  5: 'danger',
  6: 'neutral',
  7: 'danger',
};

/** Booking durations offered by the picker; the API validates against settings. */
export const DURATION_OPTIONS = [30, 60, 90, 120, 180, 240, 300, 360, 420, 480];
