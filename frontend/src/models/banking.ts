/** The bank book (requirements §13–§14). Admin-only — no customer role holds BANK.*. */

export interface BankAccount {
  bankAccountId: number;
  accountCode: string;
  bankName: string;
  accountName: string;
  accountNumberMasked: string;
  ifsc: string | null;
  branch: string | null;
  accountType: string | null;
  openingBalance: number;
  openingBalanceAsOn: string | null;
  locationId: number | null;
  locationName: string | null;
  notes: string | null;
  /** Derived: opening + credits − debits. Never stored. */
  currentBalance: number;
  totalCredits: number;
  totalDebits: number;
  unreconciledCount: number;
  isActive: boolean;
}

export interface SaveBankAccountPayload {
  accountCode: string;
  bankName: string;
  accountName: string;
  accountNumberMasked: string;
  ifsc: string | null;
  branch: string | null;
  accountType: string | null;
  openingBalance: number;
  openingBalanceAsOn: string | null;
  locationId: number | null;
  notes: string | null;
}

export interface BankTransaction {
  bankTransactionId: number;
  bankAccountId: number;
  bankName: string;
  accountCode: string;
  transactionDate: string;
  direction: number;
  amount: number;
  narration: string;
  referenceNumber: string | null;
  bankReferenceId: string | null;
  category: string | null;
  reconciliationStatus: number;
  matchedPaymentId: number | null;
  matchedPaymentNumber: string | null;
  matchedExpenseId: number | null;
  matchedExpenseNumber: string | null;
  matchedUtc: string | null;
  matchedByName: string | null;
  reconciledUtc: string | null;
  reconciledByName: string | null;
  locationId: number;
  remarks: string | null;
  importBatchId: string | null;
  createdDate: string;
}

export interface SaveBankTransactionPayload {
  bankAccountId: number;
  transactionDate: string | null;
  direction: number;
  amount: number;
  narration: string;
  referenceNumber: string | null;
  bankReferenceId: string | null;
  category: string | null;
  locationId: number;
  remarks: string | null;
}

export interface MatchCandidate {
  kind: 'Payment' | 'Expense';
  id: number;
  number: string;
  description: string;
  amount: number;
  date: string;
  amountMatches: boolean;
  alreadyMatched: boolean;
}

export const BANK_DIRECTION = { Credit: 1, Debit: 2 } as const;

export const BANK_DIRECTION_LABELS: Record<number, string> = {
  1: 'Credit',
  2: 'Debit',
};

export const RECONCILIATION_STATUS = {
  Unreconciled: 1,
  Matched: 2,
  Reconciled: 3,
} as const;

export const RECONCILIATION_LABELS: Record<number, string> = {
  1: 'Unreconciled',
  2: 'Matched',
  3: 'Reconciled',
};

export const RECONCILIATION_TONES: Record<number, 'neutral' | 'gold' | 'success'> = {
  1: 'neutral',
  2: 'gold',
  3: 'success',
};
