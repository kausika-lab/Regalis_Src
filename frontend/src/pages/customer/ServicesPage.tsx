import { useCallback, useEffect, useState } from 'react';
import { Coffee } from 'lucide-react';
import { servicesApi } from '@/api/masters';
import { ApiClientError } from '@/api/client';
import { Badge, Button, Card, PageHeader, Spinner } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import type { ServiceItem } from '@/models/masters';
import { formatInr } from '@/utils/formatters';

/** /services — the Services & Coffee catalogue customers browse. Ordering with
 * wallet payment arrives with the customer home revamp (Day 10); today staff at
 * the counter take the order against the listed price. */
export function ServicesPage() {
  const toast = useToast();
  const [categories, setCategories] = useState<string[]>([]);
  const [category, setCategory] = useState('');
  const [items, setItems] = useState<ServiceItem[] | null>(null);

  useEffect(() => {
    servicesApi.categories().then(setCategories).catch(() => {});
  }, []);

  const load = useCallback(async () => {
    setItems(null);
    try {
      const result = await servicesApi.list({
        page: 1, pageSize: 100,
        category: category || undefined,
      });
      setItems(result.items);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load services.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [category]);

  useEffect(() => { void load(); }, [load]);

  return (
    <>
      <PageHeader title="Services"
        description="Coffee, snacks and workspace services — order at the counter or the front desk." />

      {categories.length > 0 && (
        <div className="toolbar" style={{ flexWrap: 'wrap' }}>
          <Button size="sm" variant={category === '' ? 'primary' : 'outline'}
            onClick={() => setCategory('')}>
            All
          </Button>
          {categories.map((c) => (
            <Button key={c} size="sm" variant={category === c ? 'primary' : 'outline'}
              onClick={() => setCategory(c)}>
              {c}
            </Button>
          ))}
        </div>
      )}

      {!items ? (
        <Spinner center />
      ) : items.length === 0 ? (
        <Card>
          <p style={{ color: 'var(--text-secondary)' }}>
            Nothing here yet — please check back soon.
          </p>
        </Card>
      ) : (
        <div className="choice-grid">
          {items.map((item) => (
            <div key={item.serviceItemId} className="choice-card" style={{ cursor: 'default' }}>
              <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center' }}>
                <Coffee size={18} color="var(--regalis-gold-dark)" />
                <div className="choice-card__title">{item.itemName}</div>
              </div>
              <div className="choice-card__meta">
                <Badge tone="neutral">{item.category}</Badge>
              </div>
              {item.description && <div className="choice-card__meta">{item.description}</div>}
              <div className="choice-card__price">
                {formatInr(item.price)}
                {item.unit && (
                  <span style={{ fontWeight: 400, color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                    {' '}/ {item.unit}
                  </span>
                )}
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  );
}
