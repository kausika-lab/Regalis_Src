import { useCallback, useEffect, useMemo, useState } from 'react';
import { Link } from 'react-router-dom';
import { CheckCircle2, Download, QrCode, Wallet as WalletIcon, CreditCard } from 'lucide-react';
import { availabilityApi, bookingsApi, invoicesApi, paymentsApi } from '@/api/bookings';
import { ApiClientError } from '@/api/client';
import { api, unwrap } from '@/api/client';
import { locationsApi, workspaceTypesApi } from '@/api/masters';
import { Button, Card, EmptyState, Input, PageHeader, Select, Spinner } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { AvailableWorkspace, ConfirmResult, Quote, Slot } from '@/models/bookings';
import { DURATION_OPTIONS } from '@/models/bookings';
import type { Location, WorkspaceType } from '@/models/masters';
import { useUiStore } from '@/store/uiStore';
import { formatDateTime, formatInr, formatTime } from '@/utils/formatters';

/** The header's choice when it is bookable here, else the house default. */
function resolveLocation(locations: Location[], headerLocationId: number | null): number {
  const chosen = locations.find((l) => l.locationId === headerLocationId);
  const preferred = chosen ?? locations.find((l) => l.isDefault) ?? locations[0];
  return preferred.locationId;
}

type Step = 'when' | 'workspace' | 'pay' | 'done';

interface WalletSummary {
  balance: number;
}

/** The customer booking flow: type → date → time → duration → workspace →
 * summary → payment (wallet / online) → confirmation (MODULE_PLAN M10). */
export function BookWorkspacePage() {
  const toast = useToast();

  const [types, setTypes] = useState<WorkspaceType[]>([]);
  const [locations, setLocations] = useState<Location[]>([]);
  const [wallet, setWallet] = useState<WalletSummary | null>(null);

  const [step, setStep] = useState<Step>('when');
  const headerLocationId = useUiStore((state) => state.locationId);
  const [locationId, setLocationId] = useState('');
  const [typeId, setTypeId] = useState('');
  const today = new Date().toISOString().slice(0, 10);
  const [fromDate, setFromDate] = useState(today);
  const [toDate, setToDate] = useState(today);
  // Duration only means something inside a single day; a range is whole days.
  const singleDay = fromDate === toDate;
  const date = fromDate;
  const [slots, setSlots] = useState<Slot[] | null>(null);
  const [slotsLoading, setSlotsLoading] = useState(false);
  const [startUtc, setStartUtc] = useState<string | null>(null);
  const [duration, setDuration] = useState(60);
  const [workspaces, setWorkspaces] = useState<AvailableWorkspace[] | null>(null);
  const [selectedWorkspace, setSelectedWorkspace] = useState<AvailableWorkspace | null>(null);
  const [quote, setQuote] = useState<Quote | null>(null);
  const [paying, setPaying] = useState<'wallet' | 'gateway' | null>(null);
  const [result, setResult] = useState<ConfirmResult | null>(null);
  const currentLocation = locations.find((l) => String(l.locationId) === locationId) ?? null;

  // Follow the header switcher once the locations are known.
  useEffect(() => {
    if (locations.length === 0) return;
    setLocationId(String(resolveLocation(locations, headerLocationId)));
  }, [headerLocationId, locations]);

  useEffect(() => {
    Promise.all([workspaceTypesApi.list(), locationsApi.list({ pageSize: 100 })])
      .then(([typeList, locationPage]) => {
        setTypes(typeList);
        // A deactivated location must not be bookable.
        const activeLocations = locationPage.items.filter((l) => l.isActive);
        setLocations(activeLocations);
        if (activeLocations.length > 0) {
          setLocationId(String(resolveLocation(activeLocations, headerLocationId)));
        }
        if (typeList.length > 0) {
          setTypeId(String(typeList[0].workspaceTypeId));
        }
      })
      .catch(() => toast.error('Failed to load booking options.'));
    unwrap<WalletSummary>(api.get('/wallet/me')).then(setWallet).catch(() => {});
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const loadSlots = useCallback(async () => {
    if (!locationId || !typeId || !date) {
      return;
    }
    setSlotsLoading(true);
    setStartUtc(null);
    try {
      setSlots(await availabilityApi.slots(Number(locationId), Number(typeId), date));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load slots.');
    } finally {
      setSlotsLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [locationId, typeId, date]);

  useEffect(() => {
    void loadSlots();
  }, [loadSlots]);

  const findWorkspaces = async () => {
    if (!startUtc) {
      return;
    }
    setStep('workspace');
    setWorkspaces(null);
    setSelectedWorkspace(null);
    try {
      setWorkspaces(
        await availabilityApi.workspaces(Number(locationId), Number(typeId), startUtc, duration),
      );
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load workspaces.');
      setStep('when');
    }
  };

  const chooseWorkspace = async (workspace: AvailableWorkspace) => {
    setSelectedWorkspace(workspace);
    setQuote(null);
    try {
      setQuote(await bookingsApi.quote(workspace.workspaceId, startUtc!, duration));
      setStep('pay');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not price the booking.');
      setSelectedWorkspace(null);
    }
  };

  /** 409 SLOT_UNAVAILABLE is the documented race outcome: re-render availability. */
  const handleSlotRace = async (error: unknown) => {
    if (error instanceof ApiClientError && error.status === 409) {
      toast.warning('That slot was just taken. Availability has been refreshed.');
      setStep('when');
      await loadSlots();
      return true;
    }
    return false;
  };

  const payWithWallet = async () => {
    if (!selectedWorkspace || !startUtc) {
      return;
    }
    setPaying('wallet');
    try {
      const hold = await bookingsApi.hold(selectedWorkspace.workspaceId, startUtc, duration);
      const confirmed = await bookingsApi.confirm(hold.bookingId, { paymentMethod: 'wallet' });
      setResult(confirmed);
      setStep('done');
    } catch (error) {
      if (!(await handleSlotRace(error))) {
        toast.error(error instanceof ApiClientError ? error.message : 'Payment failed.');
      }
    } finally {
      setPaying(null);
    }
  };

  const payOnline = async () => {
    if (!selectedWorkspace || !startUtc) {
      return;
    }
    setPaying('gateway');
    try {
      const hold = await bookingsApi.hold(selectedWorkspace.workspaceId, startUtc, duration);
      const order = await paymentsApi.createOrder(hold.bookingId);
      // Sandbox provider: the checkout is simulated server-side. With Razorpay
      // the real checkout widget slots in here using order.keyId.
      const paid = await paymentsApi.sandboxComplete(order.gatewayOrderId);
      const confirmed = await bookingsApi.confirm(hold.bookingId, {
        paymentMethod: 'gateway',
        gatewayOrderId: order.gatewayOrderId,
        gatewayPaymentId: paid.gatewayPaymentId,
        gatewaySignature: paid.gatewaySignature,
      });
      setResult(confirmed);
      setStep('done');
    } catch (error) {
      if (!(await handleSlotRace(error))) {
        toast.error(error instanceof ApiClientError ? error.message : 'Payment failed.');
      }
    } finally {
      setPaying(null);
    }
  };

  const steps: { key: Step; label: string }[] = [
    { key: 'when', label: '1 · Type, date & time' },
    { key: 'workspace', label: '2 · Pick a workspace' },
    { key: 'pay', label: '3 · Review & pay' },
    { key: 'done', label: '4 · Confirmed' },
  ];
  const stepIndex = steps.findIndex((s) => s.key === step);

  const walletCovers = useMemo(
    () => wallet !== null && quote !== null && wallet.balance >= quote.totalAmount,
    [wallet, quote],
  );

  return (
    <>
      {/* The location field is gone from the form, so name it here instead —
          it is still what every slot below is filtered by. */}
      <PageHeader title="Book a Workspace"
        description={
          currentLocation
            ? `Live availability at ${currentLocation.locationName} — the calendar you see is the calendar the front desk sees.`
            : "Live availability — the calendar you see is the calendar the front desk sees."
        } />
      <div className="wizard-steps">
        {steps.map((s, index) => (
          <span key={s.key}
            className={`wizard-step${index === stepIndex ? ' wizard-step--active' : ''}${index < stepIndex ? ' wizard-step--done' : ''}`}>
            {s.label}
          </span>
        ))}
      </div>

      {step === 'when' && (
        <Card title="What, where and when">
          <div className="form-stack">
            <div className="choice-grid">
              {types.map((type) => (
                <button key={type.workspaceTypeId} type="button"
                  className={`choice-card${typeId === String(type.workspaceTypeId) ? ' choice-card--selected' : ''}`}
                  onClick={() => setTypeId(String(type.workspaceTypeId))}>
                  <div className="choice-card__title">{type.typeName}</div>
                  <div className="choice-card__meta">
                    {type.workspaceCount} workspace{type.workspaceCount === 1 ? '' : 's'}
                  </div>
                </button>
              ))}
            </div>
            <div className="toolbar" style={{ marginBottom: 0 }}>
              <Input label="From date" type="date" value={fromDate}
                min={today}
                onChange={(e) => {
                  const next = e.target.value;
                  setFromDate(next);
                  // Never leave To before From.
                  if (next > toDate) setToDate(next);
                }} />
              <Input label="To date" type="date" value={toDate}
                min={fromDate || today}
                onChange={(e) => setToDate(e.target.value)} />
              <Select label="Duration" value={String(duration)}
                disabled={!singleDay}
                hint={singleDay ? undefined : 'Same From and To date to pick a duration'}
                options={DURATION_OPTIONS.map((m) => ({
                  value: m,
                  label: m < 60 ? `${m} min` : `${m / 60} hour${m > 60 ? 's' : ''}`,
                }))}
                onChange={(e) => setDuration(Number(e.target.value))} />
            </div>

            {!singleDay ? (
              <EmptyState title="Multi-day booking is not available yet"
                description="Booking runs a day at a time, so set From and To to the same date. For a longer stay, buy a weekly or monthly pass instead." />
            ) : slotsLoading ? (
              <Spinner center />
            ) : !slots || slots.length === 0 ? (
              <EmptyState title="Closed on this date"
                description="Pick another date — the location has no working hours that day." />
            ) : (
              <div>
                <div className="field__label" style={{ marginBottom: 'var(--space-2)' }}>
                  Start time (IST)
                </div>
                <div className="slot-grid">
                  {slots.map((slot) => (
                    <button key={slot.startUtc} type="button" disabled={!slot.isAvailable}
                      className={`slot-chip${startUtc === slot.startUtc ? ' slot-chip--selected' : ''}`}
                      onClick={() => setStartUtc(slot.startUtc)}>
                      {formatTime(slot.startUtc)}
                    </button>
                  ))}
                </div>
              </div>
            )}

            <Button size="lg" disabled={!startUtc} onClick={findWorkspaces}>
              Find available workspaces
            </Button>
          </div>
        </Card>
      )}

      {step === 'workspace' && (
        <Card title={`Available ${formatDateTime(startUtc!)} · ${duration} min`}
          actions={<Button variant="ghost" onClick={() => setStep('when')}>Change time</Button>}>
          {!workspaces ? (
            <Spinner center />
          ) : workspaces.length === 0 ? (
            <EmptyState title="Nothing free in that window"
              description="Try a different time, duration or workspace type." />
          ) : (
            <div className="choice-grid">
              {workspaces.map((workspace) => (
                <button key={workspace.workspaceId} type="button"
                  className={`choice-card${selectedWorkspace?.workspaceId === workspace.workspaceId ? ' choice-card--selected' : ''}`}
                  onClick={() => chooseWorkspace(workspace)}>
                  <div className="choice-card__title">{workspace.workspaceName}</div>
                  <div className="choice-card__meta">
                    {workspace.workspaceCode}
                    {workspace.floorNumber ? ` · Floor ${workspace.floorNumber}` : ''} · seats{' '}
                    {workspace.capacity}
                  </div>
                  {workspace.amenities.length > 0 && (
                    <div className="choice-card__meta">{workspace.amenities.join(' · ')}</div>
                  )}
                  <div className="choice-card__price">{formatInr(workspace.pricePerHour)}/hour</div>
                </button>
              ))}
            </div>
          )}
        </Card>
      )}

      {step === 'pay' && quote && selectedWorkspace && (
        <Card title="Review & pay"
          actions={<Button variant="ghost" onClick={() => setStep('workspace')}>Back</Button>}>
          <div className="form-stack">
            <div className="summary-rows">
              <div><span>Workspace</span><strong>{quote.workspaceName} ({selectedWorkspace.workspaceCode})</strong></div>
              <div><span>When</span><strong>{formatDateTime(quote.startUtc)} – {formatTime(quote.endUtc)}</strong></div>
              <div><span>Duration</span><strong>{quote.durationMinutes} minutes</strong></div>
              <div><span>Base amount</span><span>{formatInr(quote.baseAmount)}</span></div>
              <div><span>CGST @ {quote.cgstPercent}%</span><span>{formatInr(quote.cgstAmount)}</span></div>
              <div><span>SGST @ {quote.sgstPercent}%</span><span>{formatInr(quote.sgstAmount)}</span></div>
              <div className="summary-total"><span>Total</span><span>{formatInr(quote.totalAmount)}</span></div>
            </div>

            <div className="pay-options">
              <Button size="lg" variant={walletCovers ? 'primary' : 'outline'}
                disabled={!walletCovers || paying !== null} loading={paying === 'wallet'}
                onClick={payWithWallet}>
                <WalletIcon size={18} /> Wallet
                {wallet && ` (${formatInr(wallet.balance)})`}
              </Button>
              <Button size="lg" variant="primary" disabled={paying !== null}
                loading={paying === 'gateway'} onClick={payOnline}>
                <CreditCard size={18} /> Pay online
              </Button>
            </div>
            {!walletCovers && wallet && (
              <p style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                Wallet balance {formatInr(wallet.balance)} doesn&apos;t cover this booking — pay
                online or top up from the Wallet page.
              </p>
            )}
            <p style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
              The slot is held for 10 minutes while payment completes.
            </p>
          </div>
        </Card>
      )}

      {step === 'done' && result && (
        <Card>
          <div style={{ textAlign: 'center', padding: 'var(--space-6) 0' }}>
            <CheckCircle2 size={56} color="var(--color-success)" />
            <h2 style={{ fontSize: 'var(--font-size-2xl)', fontWeight: 800, marginTop: 'var(--space-3)' }}>
              Booking confirmed
            </h2>
            <p style={{ color: 'var(--text-secondary)', marginTop: 'var(--space-2)' }}>
              <strong>{result.booking.bookingNumber}</strong> · {result.booking.workspaceName} ·{' '}
              {formatDateTime(result.booking.startUtc)} – {formatTime(result.booking.endUtc)}
            </p>
            <p style={{ color: 'var(--text-muted)', marginTop: 'var(--space-2)', fontSize: 'var(--font-size-sm)' }}>
              <QrCode size={16} style={{ verticalAlign: 'text-bottom' }} /> Your QR access pass is
              ready — find it under <Link to="/qr">My QR / Access</Link> when the access module
              opens.
            </p>
            <div style={{ display: 'flex', gap: 'var(--space-3)', justifyContent: 'center', marginTop: 'var(--space-6)' }}>
              <Button variant="outline"
                onClick={() => invoicesApi.downloadPdf(result.invoiceId, result.invoiceNumber)}>
                <Download size={16} /> Invoice {result.invoiceNumber}
              </Button>
              <Button onClick={() => {
                setResult(null);
                setStep('when');
                void loadSlots();
              }}>
                Book another
              </Button>
            </div>
          </div>
        </Card>
      )}
    </>
  );
}
