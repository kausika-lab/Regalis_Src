/**
 * Internal finance chain (requirements §4, §5, §21): Supplier → Purchase Order →
 * Goods Received → Inventory, and the daily Expense register alongside it.
 */

export interface Supplier {
  supplierId: number;
  supplierCode: string;
  supplierName: string;
  contactPerson: string | null;
  email: string | null;
  mobileNumber: string | null;
  gstin: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  paymentTerms: string | null;
  locationId: number | null;
  locationName: string | null;
  notes: string | null;
  openPurchaseOrders: number;
  isActive: boolean;
}

export interface SaveSupplierPayload {
  supplierCode: string;
  supplierName: string;
  contactPerson: string | null;
  email: string | null;
  mobileNumber: string | null;
  gstin: string | null;
  addressLine: string | null;
  city: string | null;
  state: string | null;
  pincode: string | null;
  paymentTerms: string | null;
  locationId: number | null;
  notes: string | null;
}

export interface PurchaseOrderLine {
  purchaseOrderLineId: number;
  lineNumber: number;
  inventoryItemId: number | null;
  itemCode: string | null;
  itemName: string;
  hsnSacCode: string | null;
  unit: string | null;
  quantity: number;
  receivedQuantity: number;
  pendingQuantity: number;
  rate: number;
  discountAmount: number;
  lineAmount: number;
  taxPercent: number;
  taxAmount: number;
  totalAmount: number;
  remarks: string | null;
}

export interface PurchaseOrder {
  purchaseOrderId: number;
  poNumber: string;
  poDate: string;
  supplierId: number;
  supplierName: string;
  supplierGstin: string | null;
  locationId: number;
  locationName: string | null;
  expectedDeliveryDate: string | null;
  subTotal: number;
  discountAmount: number;
  taxableAmount: number;
  taxAmount: number;
  totalAmount: number;
  status: number;
  sentUtc: string | null;
  cancelledUtc: string | null;
  cancellationReason: string | null;
  remarks: string | null;
  attachmentPath: string | null;
  createdDate: string;
  lines: PurchaseOrderLine[];
}

export interface SavePurchaseOrderLinePayload {
  inventoryItemId: number | null;
  itemName: string;
  hsnSacCode: string | null;
  unit: string | null;
  quantity: number;
  rate: number;
  discountAmount: number;
  /** The supplier's GST rate from their quote — their output tax, not ours. */
  taxPercent: number;
  remarks: string | null;
}

export interface SavePurchaseOrderPayload {
  poDate: string | null;
  supplierId: number;
  locationId: number;
  expectedDeliveryDate: string | null;
  remarks: string | null;
  lines: SavePurchaseOrderLinePayload[];
}

export interface ReceiveGoodsPayload {
  receivedDate: string | null;
  purchaseInvoiceNumber: string | null;
  attachmentPath: string | null;
  notes: string | null;
  lines: { purchaseOrderLineId: number; receivedQuantity: number }[];
}

export const PO_STATUS_LABELS: Record<number, string> = {
  1: 'Draft',
  2: 'Sent',
  3: 'Partially received',
  4: 'Received',
  5: 'Cancelled',
};

/** Badge tones mirror the booking grid: neutral draft, gold in-flight, green done. */
export const PO_STATUS_TONES: Record<number, 'neutral' | 'gold' | 'success' | 'danger'> = {
  1: 'neutral',
  2: 'gold',
  3: 'gold',
  4: 'success',
  5: 'danger',
};

export interface Expense {
  expenseId: number;
  expenseNumber: string;
  expenseDate: string;
  category: string;
  description: string;
  amount: number;
  paymentMethod: number;
  remarks: string;
  attachmentPath: string | null;
  locationId: number;
  locationName: string | null;
  enteredByUserId: number;
  enteredByName: string;
  supplierId: number | null;
  supplierName: string | null;
  purchaseOrderId: number | null;
  poNumber: string | null;
  bankTransactionId: number | null;
  isReconciled: boolean;
  createdDate: string;
}

export interface SaveExpensePayload {
  expenseDate: string | null;
  category: string;
  description: string;
  amount: number;
  paymentMethod: number;
  remarks: string;
  attachmentPath: string | null;
  locationId: number;
  supplierId: number | null;
  purchaseOrderId: number | null;
}

export interface ExpenseCategoryTotal {
  category: string;
  amount: number;
  count: number;
}

export interface ExpenseSummary {
  totalAmount: number;
  totalCount: number;
  byCategory: ExpenseCategoryTotal[];
}

/** Mirrors PaymentMethod in the API — reused for an expense's payment mode. */
export const PAYMENT_MODE_LABELS: Record<number, string> = {
  1: 'Wallet',
  2: 'UPI',
  3: 'Credit card',
  4: 'Debit card',
  5: 'Net banking',
  6: 'Cash',
  7: 'Other',
};
