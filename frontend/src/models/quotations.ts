/** Quotations (requirements §6–§8): admin raises, customer approves or rejects. */

export interface QuotationLine {
  quotationLineId: number;
  lineNumber: number;
  workspaceTypeId: number | null;
  workspaceTypeName: string | null;
  serviceItemId: number | null;
  serviceItemName: string | null;
  description: string;
  hsnSacCode: string | null;
  quantity: number;
  durationValue: number | null;
  durationUnit: string | null;
  unitPrice: number;
  discountAmount: number;
  lineAmount: number;
  taxPercent: number;
  taxAmount: number;
  totalAmount: number;
}

export interface QuotationStatusHistoryEntry {
  fromStatus: number | null;
  toStatus: number;
  changedByUserId: number;
  changedByName: string;
  changedUtc: string;
  reason: string | null;
}

export interface Quotation {
  quotationId: number;
  quotationNumber: string;
  customerId: number;
  customerName: string;
  customerEmail: string;
  customerMobile: string | null;
  customerGstin: string | null;
  billingAddress: string | null;
  locationId: number;
  locationName: string | null;
  quotationDate: string;
  validUntil: string;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  cgstPercent: number;
  cgstAmount: number;
  sgstPercent: number;
  sgstAmount: number;
  igstPercent: number;
  igstAmount: number;
  totalAmount: number;
  status: number;
  sentUtc: string | null;
  viewedUtc: string | null;
  respondedUtc: string | null;
  respondedByUserId: number | null;
  rejectionReason: string | null;
  invoiceId: number | null;
  invoiceNumber: string | null;
  companyName: string;
  companyGstin: string | null;
  companyAddress: string | null;
  placeOfSupply: string | null;
  termsAndConditions: string | null;
  remarks: string | null;
  isExpired: boolean;
  createdDate: string;
  lines: QuotationLine[];
  statusHistory: QuotationStatusHistoryEntry[];
}

export interface SaveQuotationLinePayload {
  workspaceTypeId: number | null;
  serviceItemId: number | null;
  description: string;
  hsnSacCode: string | null;
  quantity: number;
  durationValue: number | null;
  durationUnit: string | null;
  unitPrice: number;
  discountAmount: number;
}

export interface SaveQuotationPayload {
  customerId: number;
  locationId: number;
  quotationDate: string | null;
  validUntil: string | null;
  termsAndConditions: string | null;
  remarks: string | null;
  lines: SaveQuotationLinePayload[];
}

export const QUOTATION_STATUS = {
  Draft: 1,
  Sent: 2,
  Viewed: 3,
  Approved: 4,
  Rejected: 5,
  Expired: 6,
  Cancelled: 7,
} as const;

export const QUOTATION_STATUS_LABELS: Record<number, string> = {
  1: 'Draft',
  2: 'Sent',
  3: 'Viewed',
  4: 'Approved',
  5: 'Rejected',
  6: 'Expired',
  7: 'Cancelled',
};

export const QUOTATION_STATUS_TONES: Record<number, 'neutral' | 'gold' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'gold',
  3: 'gold',
  4: 'success',
  5: 'danger',
  6: 'neutral',
  7: 'danger',
};

/** Duration units offered on a quotation line; free text on the server. */
export const DURATION_UNITS = ['Hours', 'Days', 'Weeks', 'Months'] as const;
