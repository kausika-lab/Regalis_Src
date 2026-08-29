import { useEffect, useState } from 'react';
import QRCode from 'qrcode';
import { accessApi, type MyAccessToken } from '@/api/access';
import { Badge, Card, EmptyState, PageHeader, Spinner } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { formatDateTime } from '@/utils/formatters';

const PURPOSE_LABELS: Record<number, string> = {
  1: 'Booking',
  2: 'Day pass',
  3: 'Monthly pass',
  4: 'Housekeeping',
};

/** My QR / Access: every active pass rendered as a scannable QR. The payload is
 * signed server-side; the reader posts it to /access/validate. */
export function MyQrPage() {
  const toast = useToast();
  const [tokens, setTokens] = useState<MyAccessToken[] | null>(null);
  const [images, setImages] = useState<Record<number, string>>({});

  useEffect(() => {
    accessApi.myTokens()
      .then(async (list) => {
        setTokens(list);
        const rendered: Record<number, string> = {};
        for (const token of list) {
          rendered[token.accessTokenId] = await QRCode.toDataURL(token.qrPayload, {
            width: 240,
            margin: 1,
            color: { dark: '#0B0B0D', light: '#FFFFFF' },
          });
        }
        setImages(rendered);
      })
      .catch(() => toast.error('Failed to load your access passes.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <>
      <PageHeader title="My QR / Access"
        description="Show the QR at the gate. Each pass is valid only for its window and is revoked if the booking or pass is cancelled." />
      {!tokens ? (
        <Spinner center />
      ) : tokens.length === 0 ? (
        <Card>
          <EmptyState title="No active access passes"
            description="Confirm a booking or buy a pass — the QR appears here immediately." />
        </Card>
      ) : (
        <div className="choice-grid" style={{ gridTemplateColumns: 'repeat(auto-fill, minmax(280px, 1fr))' }}>
          {tokens.map((token) => (
            <div key={token.accessTokenId} className="choice-card" style={{ cursor: 'default', textAlign: 'center' }}>
              {images[token.accessTokenId] ? (
                <img src={images[token.accessTokenId]} alt={`QR for ${token.reference}`}
                  style={{ width: 200, height: 200, margin: '0 auto', display: 'block' }} />
              ) : (
                <Spinner center />
              )}
              <div className="choice-card__title" style={{ marginTop: 'var(--space-2)' }}>
                {token.reference}{' '}
                <Badge tone="gold">{PURPOSE_LABELS[token.tokenPurpose]}</Badge>
              </div>
              {token.workspaceName && (
                <div className="choice-card__meta">{token.workspaceName}</div>
              )}
              <div className="choice-card__meta">
                {formatDateTime(token.validFromUtc)} → {formatDateTime(token.validToUtc)} IST
              </div>
            </div>
          ))}
        </div>
      )}
    </>
  );
}
