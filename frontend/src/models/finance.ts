export interface Wallet {
  walletId: number;
  customerId: number;
  balance: number;
  totalCredited: number;
  totalDebited: number;
  lastTransactionUtc: string | null;
}

export interface WalletTransaction {
  walletTransactionId: number;
  transactionNumber: string;
  direction: number; // 1 credit, 2 debit
  category: number;
  amount: number;
  balanceAfter: number;
  narration: string;
  transactionUtc: string;
}

export interface InvoiceLine {
  lineNumber: number;
  description: string;
  hsnSacCode: string | null;
  quantity: number;
  unitPrice: number;
  lineAmount: number;
  taxPercent: number;
  taxAmount: number;
  totalAmount: number;
}

export interface Invoice {
  invoiceId: number;
  invoiceNumber: string;
  customerId: number;
  bookingId: number | null;
  quotationId: number | null;
  quotationNumber: string | null;
  invoiceDate: string;
  dueDate: string;
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
  paidAmount: number;
  outstandingAmount: number;
  status: number;
  isOverdue: boolean;
  paidUtc: string | null;
  cancellationReason: string | null;
  termsAndConditions: string | null;
  customerName: string;
  customerEmail: string;
  companyName: string;
  lines: InvoiceLine[];
}

export interface PassPlan {
  passPlanId: number;
  planCode: string;
  planName: string;
  passType: number; // 1 day, 2 monthly
  locationId: number | null;
  locationName: string | null;
  price: number;
  validityDays: number;
  maxGuests: number | null;
  description: string | null;
  termsAndConditions: string | null;
  displayOrder: number;
  isActive: boolean;
  cgstAmount: number;
  sgstAmount: number;
  totalAmount: number;
}

export interface CustomerPass {
  customerPassId: number;
  passNumber: string;
  customerId: number;
  customerName: string;
  passPlanId: number;
  planName: string;
  passType: number;
  validFrom: string;
  validTo: string;
  price: number;
  taxAmount: number;
  totalAmount: number;
  status: number;
  purchaseUtc: string;
  invoiceId: number | null;
}

export interface AppNotification {
  notificationId: number;
  title: string;
  message: string;
  category: number;
  priority: number;
  actionLabel: string | null;
  actionRoute: string | null;
  isRead: boolean;
  readUtc: string | null;
  createdDate: string;
}

export interface InvoicePaymentResult {
  payment: PaymentRow;
  invoiceId: number;
  invoiceNumber: string;
  paidAmount: number;
  outstandingAmount: number;
  invoiceStatus: number;
}

export interface PaymentRow {
  paymentId: number;
  paymentNumber: string;
  customerId: number;
  customerName: string;
  bookingId: number | null;
  invoiceId: number | null;
  invoiceNumber: string | null;
  invoiceAmount: number | null;
  amount: number;
  paymentMethod: number;
  gateway: string | null;
  gatewayOrderId: string | null;
  gatewayPaymentId: string | null;
  refundedAmount: number | null;
  refundReference: string | null;
  isSignatureVerified: boolean;
  status: number;
  paidUtc: string | null;
  createdDate: string;
}

export const WALLET_CATEGORY_LABELS: Record<number, string> = {
  1: 'Recharge',
  2: 'Booking',
  3: 'Refund',
  4: 'Day pass',
  5: 'Monthly pass',
  6: 'Service order',
  7: 'Security deposit',
  8: 'Adjustment',
  9: 'Cancellation',
};

export const INVOICE_STATUS_LABELS: Record<number, string> = {
  1: 'Pending',
  2: 'Paid',
  3: 'Partially paid',
  4: 'Cancelled',
  5: 'Refunded',
  6: 'Sent',
};

/** PassType. Listed in duration order, which is how the Passes page reads. */
export const PASS_TYPES: { value: number; label: string }[] = [
  { value: 1, label: 'Day pass' },
  { value: 3, label: 'Weekly pass' },
  { value: 2, label: 'Monthly pass' },
  { value: 4, label: 'Yearly pass' },
];

export const PASS_TYPE_LABELS: Record<number, string> = {
  1: 'Day pass',
  2: 'Monthly pass',
  3: 'Weekly pass',
  4: 'Yearly pass',
};

export const PASS_STATUS_LABELS: Record<number, string> = {
  1: 'Active',
  2: 'Used',
  3: 'Expired',
  4: 'Cancelled',
};

export const PAYMENT_METHOD_LABELS: Record<number, string> = {
  1: 'Wallet',
  2: 'UPI',
  3: 'Credit card',
  4: 'Debit card',
  5: 'Net banking',
  6: 'Cash',
  7: 'Other',
};

export const PAYMENT_STATUS_LABELS: Record<number, string> = {
  1: 'Pending',
  2: 'Success',
  3: 'Failed',
  4: 'Refunded',
  5: 'Cancelled',
};

/** POST /notifications/broadcast — audience 2 all customers, 3 all admin-portal users. */
export interface BroadcastNotificationPayload {
  audience: number;
  title: string;
  message: string;
  category: number;
  actionLabel: string | null;
  actionRoute: string | null;
}

export const NOTIFICATION_CATEGORY_LABELS: Record<number, string> = {
  1: 'Booking',
  2: 'Meeting room',
  3: 'Check-in / out',
  4: 'Payment',
  5: 'Invoice',
  6: 'Service order',
  7: 'Event',
  8: 'Support ticket',
  9: 'Announcement',
  10: 'Registration',
  11: 'Pass',
  12: 'Housekeeping',
};

export const NOTIFICATION_AUDIENCE_LABELS: Record<number, string> = {
  2: 'All customers',
  3: 'All admin-portal users',
};
