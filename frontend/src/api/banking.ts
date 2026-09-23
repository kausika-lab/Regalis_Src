import type { PagedRequest, PagedResult } from '@/models/api';
import type {
  BankAccount,
  BankTransaction,
  MatchCandidate,
  SaveBankAccountPayload,
  SaveBankTransactionPayload,
} from '@/models/banking';
import { api, unwrap } from './client';

export interface BankTransactionListQuery extends PagedRequest {
  bankAccountId?: number;
  direction?: number;
  reconciliationStatus?: number;
  locationId?: number;
  from?: string;
  to?: string;
}

export const bankApi = {
  accounts: () => unwrap<BankAccount[]>(api.get('/bank/accounts')),
  account: (id: number) => unwrap<BankAccount>(api.get(`/bank/accounts/${id}`)),
  createAccount: (payload: SaveBankAccountPayload) =>
    unwrap<BankAccount>(api.post('/bank/accounts', payload)),
  updateAccount: (id: number, payload: SaveBankAccountPayload) =>
    unwrap<BankAccount>(api.put(`/bank/accounts/${id}`, payload)),
  removeAccount: (id: number) => api.delete(`/bank/accounts/${id}`),

  transactions: (query: BankTransactionListQuery = {}) =>
    unwrap<PagedResult<BankTransaction>>(api.get('/bank/transactions', { params: query })),
  transaction: (id: number) => unwrap<BankTransaction>(api.get(`/bank/transactions/${id}`)),
  createTransaction: (payload: SaveBankTransactionPayload) =>
    unwrap<BankTransaction>(api.post('/bank/transactions', payload)),
  updateTransaction: (id: number, payload: SaveBankTransactionPayload) =>
    unwrap<BankTransaction>(api.put(`/bank/transactions/${id}`, payload)),
  removeTransaction: (id: number) => api.delete(`/bank/transactions/${id}`),

  /** Existing payments or expenses that could correspond to this line. */
  candidates: (id: number) =>
    unwrap<MatchCandidate[]>(api.get(`/bank/transactions/${id}/candidates`)),
  /** Points the line at an existing record — never creates one. */
  match: (id: number, target: { paymentId?: number; expenseId?: number }) =>
    unwrap<BankTransaction>(api.post(`/bank/transactions/${id}/match`, {
      paymentId: target.paymentId ?? null,
      expenseId: target.expenseId ?? null,
    })),
  unmatch: (id: number) => unwrap<BankTransaction>(api.post(`/bank/transactions/${id}/unmatch`)),
  reconcile: (id: number) => unwrap<BankTransaction>(api.post(`/bank/transactions/${id}/reconcile`)),

  export: async (query: BankTransactionListQuery = {}) => {
    const response = await api.get('/bank/export', { params: query, responseType: 'blob' });
    const url = URL.createObjectURL(response.data as Blob);
    const anchor = document.createElement('a');
    anchor.href = url;
    anchor.download = `bank_statement_${new Date().toISOString().slice(0, 10)}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
  },
};
