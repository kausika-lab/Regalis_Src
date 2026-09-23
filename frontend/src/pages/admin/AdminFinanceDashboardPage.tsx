import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { dashboardApi, type AdminDashboard } from '@/api/reports';
import { ApiClientError } from '@/api/client';
import { Card, PageHeader, Spinner } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { formatDateTime } from '@/utils/formatters';

/**
 * The Billing & Finance dashboard (requirements §16). Every figure is derived on read
 * from the same rows the module's screens use — there is no summary table to fall out
 * of step — and each card links to the screen its number came from.
 */
export function AdminFinanceDashboardPage() {
  const toast = useToast();
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    void (async () => {
      try {
        setDashboard(await dashboardApi.finance());
      } catch (error) {
        toast.error(
          error instanceof ApiClientError ? error.message : 'Failed to load the dashboard.',
        );
      } finally {
        setLoading(false);
      }
    })();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (loading) {
    return <Spinner center />;
  }

  return (
    <>
      <PageHeader
        title="Billing &amp; Finance"
        description={
          dashboard
            ? `Derived live from the ledgers · ${formatDateTime(dashboard.generatedUtc)}`
            : 'Quotations, invoices, payments, expenses, purchasing and bank in one view.'
        }
      />

      {dashboard ? (
        <div className="choice-grid">
          {dashboard.cards.map((card) => {
            const body = (
              <>
                <div className="choice-card__meta">{card.label}</div>
                <div className="choice-card__title" style={{ fontSize: 'var(--font-size-xl)' }}>
                  {card.value}
                </div>
                {card.hint && <div className="choice-card__meta">{card.hint}</div>}
              </>
            );
            return card.route ? (
              <Link key={card.key} to={card.route} className="choice-card"
                style={{ textDecoration: 'none' }}>
                {body}
              </Link>
            ) : (
              <div key={card.key} className="choice-card" style={{ cursor: 'default' }}>
                {body}
              </div>
            );
          })}
        </div>
      ) : (
        <Card>
          <p>The dashboard could not be loaded.</p>
        </Card>
      )}
    </>
  );
}
