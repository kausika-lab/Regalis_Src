import { useCallback, useEffect, useState } from 'react';
import { ArrowDownLeft, ArrowUpRight, Wallet as WalletIcon } from 'lucide-react';
import { paymentsApi } from '@/api/bookings';
import { walletApi } from '@/api/finance';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  DataTable,
  Input,
  Modal,
  PageHeader,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { PagedResult } from '@/models/api';
import type { Wallet, WalletTransaction } from '@/models/finance';
import { WALLET_CATEGORY_LABELS } from '@/models/finance';
import { formatDateTime, formatInr } from '@/utils/formatters';

export function WalletPage() {
  const toast = useToast();
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [transactions, setTransactions] = useState<PagedResult<WalletTransaction> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [rechargeOpen, setRechargeOpen] = useState(false);
  const [amount, setAmount] = useState(500);
  const [recharging, setRecharging] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const [walletData, transactionPage] = await Promise.all([
        walletApi.me(),
        walletApi.myTransactions({ page, pageSize: 20 }),
      ]);
      setWallet(walletData);
      setTransactions(transactionPage);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the wallet.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  const recharge = async () => {
    setRecharging(true);
    try {
      const order = await walletApi.rechargeOrder(amount);
      // Sandbox checkout simulation; the Razorpay widget slots in with real keys.
      const paid = await paymentsApi.sandboxComplete(order.gatewayOrderId);
      const result = await walletApi.rechargeVerify(
        order.gatewayOrderId, paid.gatewayPaymentId, paid.gatewaySignature);
      toast.success(`Wallet recharged (${result.transactionNumber}).`);
      setRechargeOpen(false);
      await load();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Recharge failed.');
    } finally {
      setRecharging(false);
    }
  };

  const columns: Column<WalletTransaction>[] = [
    {
      key: 'txn',
      header: 'Transaction',
      render: (t) => (
        <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-2)' }}>
          {t.direction === 1
            ? <ArrowDownLeft size={16} color="var(--color-success)" />
            : <ArrowUpRight size={16} color="var(--color-danger)" />}
          <div>
            <div style={{ fontWeight: 600 }}>{t.narration}</div>
            <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
              {t.transactionNumber} · {formatDateTime(t.transactionUtc)}
            </div>
          </div>
        </div>
      ),
    },
    {
      key: 'category',
      header: 'Category',
      render: (t) => <Badge tone="neutral">{WALLET_CATEGORY_LABELS[t.category] ?? t.category}</Badge>,
    },
    {
      key: 'amount',
      header: 'Amount',
      render: (t) => (
        <span style={{ color: t.direction === 1 ? 'var(--color-success)' : 'var(--color-danger)', fontWeight: 600 }}>
          {t.direction === 1 ? '+' : '−'}{formatInr(t.amount)}
        </span>
      ),
    },
    { key: 'balance', header: 'Balance after', render: (t) => formatInr(t.balanceAfter) },
  ];

  return (
    <>
      <PageHeader title="Wallet"
        description="Refunds land here automatically. The balance is non-withdrawable and spends on bookings, passes and services."
        actions={<Button onClick={() => setRechargeOpen(true)}>Add funds</Button>} />

      <div className="dash-grid" style={{ gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))' }}>
        <div className="dash-tile" style={{ cursor: 'default' }}>
          <span className="dash-tile__icon"><WalletIcon size={26} /></span>
          <div>
            <div className="dash-tile__label">Balance</div>
            <div style={{ fontSize: 'var(--font-size-xl)', fontWeight: 800 }}>
              {wallet ? formatInr(wallet.balance) : '—'}
            </div>
          </div>
        </div>
        <div className="dash-tile" style={{ cursor: 'default' }}>
          <span className="dash-tile__icon"><ArrowDownLeft size={26} /></span>
          <div>
            <div className="dash-tile__label">Total credited</div>
            <div style={{ fontWeight: 700 }}>{wallet ? formatInr(wallet.totalCredited) : '—'}</div>
          </div>
        </div>
        <div className="dash-tile" style={{ cursor: 'default' }}>
          <span className="dash-tile__icon"><ArrowUpRight size={26} /></span>
          <div>
            <div className="dash-tile__label">Total spent</div>
            <div style={{ fontWeight: 700 }}>{wallet ? formatInr(wallet.totalDebited) : '—'}</div>
          </div>
        </div>
      </div>

      <Card title="Transactions">
        <DataTable columns={columns} result={transactions} loading={loading}
          rowKey={(t) => t.walletTransactionId} onPageChange={setPage}
          emptyTitle="No transactions yet"
          emptyDescription="Recharges, payments and refunds appear here with a running balance." />
      </Card>

      <Modal open={rechargeOpen} title="Add funds" onClose={() => setRechargeOpen(false)}
        footer={
          <>
            <Button variant="ghost" onClick={() => setRechargeOpen(false)}>Cancel</Button>
            <Button onClick={recharge} loading={recharging}>Pay {formatInr(amount)}</Button>
          </>
        }>
        <div className="form-stack">
          <Input label="Amount (₹)" type="number" min={100} max={50000} step={100} value={amount}
            hint="Between ₹100 and ₹50,000 per recharge"
            onChange={(e) => setAmount(Number(e.target.value))} />
          <div className="pay-options">
            {[500, 1000, 2000, 5000].map((preset) => (
              <Button key={preset} variant={amount === preset ? 'primary' : 'outline'} size="sm"
                onClick={() => setAmount(preset)}>
                {formatInr(preset)}
              </Button>
            ))}
          </div>
        </div>
      </Modal>
    </>
  );
}
