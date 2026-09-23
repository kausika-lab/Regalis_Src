import { useCallback, useEffect, useState } from 'react';
import { QrCode, Ticket } from 'lucide-react';
import { paymentsApi } from '@/api/bookings';
import { passApi, walletApi } from '@/api/finance';
import { ApiClientError } from '@/api/client';
import { Badge, Button, Card, Input, Modal, PageHeader, Spinner, type Column, DataTable } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { PagedResult } from '@/models/api';
import type { CustomerPass, PassPlan, Wallet } from '@/models/finance';
import { PASS_STATUS_LABELS, PASS_TYPES, PASS_TYPE_LABELS } from '@/models/finance';
import { formatDate, formatInr } from '@/utils/formatters';

/**
 * /passes — every kind of pass in one place, filtered by the badges across the
 * top; /day-pass and /monthly-pass still land here with that badge preselected
 * so older links and bookmarks keep working.
 */
export function PassPlansPage({ passType = 1 }: { passType?: number }) {
  const toast = useToast();
  const [type, setType] = useState<number>(passType);
  const [plans, setPlans] = useState<PassPlan[] | null>(null);
  const [buying, setBuying] = useState<PassPlan | null>(null);

  useEffect(() => {
    setType(passType);
  }, [passType]);

  useEffect(() => {
    setPlans(null);
    passApi.activePlans(type)
      .then(setPlans)
      .catch(() => toast.error('Failed to load plans.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [type]);

  const title = PASS_TYPE_LABELS[type] ?? 'Passes';

  return (
    <>
      <PageHeader title="Passes"
        description="A pass carries its own QR access valid for the whole window — no per-visit booking needed." />

      <div className="passtabs" role="tablist" aria-label="Pass type">
        {PASS_TYPES.map((option) => (
          <button
            key={option.value}
            type="button"
            role="tab"
            aria-selected={type === option.value}
            className={`passtabs__tab${type === option.value ? ' passtabs__tab--active' : ''}`}
            onClick={() => setType(option.value)}
          >
            <Ticket size={15} aria-hidden /> {option.label}
          </button>
        ))}
      </div>

      {!plans ? (
        <Spinner center />
      ) : plans.length === 0 ? (
        <Card>
          <p style={{ color: 'var(--text-secondary)' }}>
            No {title.toLowerCase()} plans are on sale right now. Please check back soon.
          </p>
        </Card>
      ) : (
        <div className="choice-grid">
          {plans.map((plan) => (
            <div key={plan.passPlanId} className="choice-card" style={{ cursor: 'default' }}>
              <div className="choice-card__title">{plan.planName}</div>
              <div className="choice-card__meta">
                {plan.validityDays} day{plan.validityDays > 1 ? 's' : ''} of access
                {plan.locationName ? ` · ${plan.locationName}` : ' · all locations'}
              </div>
              {plan.description && <div className="choice-card__meta">{plan.description}</div>}
              <div className="choice-card__price">
                {formatInr(plan.totalAmount)}{' '}
                <span style={{ fontWeight: 400, color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                  incl. GST
                </span>
              </div>
              <div style={{ marginTop: 'var(--space-3)' }}>
                <Button size="sm" onClick={() => setBuying(plan)}>Buy pass</Button>
              </div>
            </div>
          ))}
        </div>
      )}

      {buying && (
        <PurchaseModal plan={buying} onClose={() => setBuying(null)}
          onDone={() => setBuying(null)} />
      )}

      <MyPassesCard />
    </>
  );
}

function PurchaseModal({ plan, onClose, onDone }: {
  plan: PassPlan;
  onClose: () => void;
  onDone: () => void;
}) {
  const toast = useToast();
  const [wallet, setWallet] = useState<Wallet | null>(null);
  const [validFrom, setValidFrom] = useState(() => {
    const tomorrow = new Date(Date.now() + 24 * 3600_000);
    return tomorrow.toISOString().slice(0, 10);
  });
  const [paying, setPaying] = useState<'wallet' | 'gateway' | null>(null);

  useEffect(() => {
    walletApi.me().then(setWallet).catch(() => {});
  }, []);

  const buy = async (method: 'wallet' | 'gateway') => {
    setPaying(method);
    try {
      if (method === 'wallet') {
        const result = await passApi.purchase({ passPlanId: plan.passPlanId, validFrom, paymentMethod: 'wallet' });
        toast.success(`Pass ${result.pass.passNumber} activated. QR pass ready.`);
      } else {
        const order = await passApi.order(plan.passPlanId);
        const paid = await paymentsApi.sandboxComplete(order.gatewayOrderId);
        const result = await passApi.purchase({
          passPlanId: plan.passPlanId,
          validFrom,
          paymentMethod: 'gateway',
          gatewayOrderId: order.gatewayOrderId,
          gatewayPaymentId: paid.gatewayPaymentId,
          gatewaySignature: paid.gatewaySignature,
        });
        toast.success(`Pass ${result.pass.passNumber} activated. QR pass ready.`);
      }
      onDone();
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Purchase failed.');
    } finally {
      setPaying(null);
    }
  };

  const walletCovers = wallet !== null && wallet.balance >= plan.totalAmount;

  return (
    <Modal open title={`Buy ${plan.planName}`} onClose={onClose}>
      <div className="form-stack">
        <div className="summary-rows">
          <div><span>Plan</span><strong>{plan.planName}</strong></div>
          <div><span>Validity</span><span>{plan.validityDays} day{plan.validityDays > 1 ? 's' : ''}</span></div>
          <div><span>Price</span><span>{formatInr(plan.price)}</span></div>
          <div><span>CGST + SGST</span><span>{formatInr(plan.cgstAmount + plan.sgstAmount)}</span></div>
          <div className="summary-total"><span>Total</span><span>{formatInr(plan.totalAmount)}</span></div>
        </div>
        <Input label="Starts on" type="date" value={validFrom}
          min={new Date().toISOString().slice(0, 10)}
          onChange={(e) => setValidFrom(e.target.value)} />
        <div className="pay-options">
          <Button variant={walletCovers ? 'primary' : 'outline'} disabled={!walletCovers || paying !== null}
            loading={paying === 'wallet'} onClick={() => buy('wallet')}>
            Wallet {wallet && `(${formatInr(wallet.balance)})`}
          </Button>
          <Button disabled={paying !== null} loading={paying === 'gateway'} onClick={() => buy('gateway')}>
            Pay online
          </Button>
        </div>
      </div>
    </Modal>
  );
}

/** The customer's purchased passes — shared by /passes and the plan pages. */
export function MyPassesCard() {
  const toast = useToast();
  const [page, setPage] = useState(1);
  const [result, setResult] = useState<PagedResult<CustomerPass> | null>(null);
  const [loading, setLoading] = useState(true);
  const [qrTarget, setQrTarget] = useState<CustomerPass | null>(null);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(await passApi.my({ page, pageSize: 10 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load passes.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<CustomerPass>[] = [
    {
      key: 'pass',
      header: 'Pass',
      render: (p) => (
        <div>
          <div style={{ fontWeight: 600 }}>{p.passNumber}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{p.planName}</div>
        </div>
      ),
    },
    {
      key: 'validity',
      header: 'Valid',
      render: (p) => `${formatDate(p.validFrom)} – ${formatDate(p.validTo)}`,
    },
    { key: 'amount', header: 'Paid', render: (p) => formatInr(p.totalAmount) },
    {
      key: 'status',
      header: 'Status',
      render: (p) => (
        <Badge tone={p.status === 1 ? 'success' : p.status === 4 ? 'danger' : 'neutral'}>
          {PASS_STATUS_LABELS[p.status]}
        </Badge>
      ),
    },
    {
      key: 'actions',
      header: '',
      render: (p) =>
        p.status === 1 ? (
          <Button size="sm" variant="outline" onClick={() => setQrTarget(p)}>
            <QrCode size={14} /> Access
          </Button>
        ) : null,
    },
  ];

  return (
    <>
      <Card title="My passes">
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(p) => p.customerPassId} onPageChange={setPage}
          emptyTitle="No passes yet"
          emptyDescription="A pass gives all-day access without booking a slot each time." />
      </Card>
      <Modal open={qrTarget !== null} title={`Access — ${qrTarget?.passNumber ?? ''}`}
        onClose={() => setQrTarget(null)}>
        <div style={{ textAlign: 'center', padding: 'var(--space-4) 0' }}>
          <Ticket size={44} color="var(--regalis-gold-dark)" />
          <p style={{ marginTop: 'var(--space-3)', color: 'var(--text-secondary)' }}>
            Your QR access pass for this pass lives under <strong>My QR / Access</strong>, which
            opens with the access-control module. At the desk, quote{' '}
            <strong>{qrTarget?.passNumber}</strong> to check in meanwhile.
          </p>
        </div>
      </Modal>
    </>
  );
}

export function MyPassesPage() {
  return (
    <>
      <PageHeader title="My Passes" description="Day and monthly passes you have purchased." />
      <MyPassesCard />
    </>
  );
}
