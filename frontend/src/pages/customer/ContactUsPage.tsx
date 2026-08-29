import { useEffect, useState } from 'react';
import { Clock, LifeBuoy, Mail, MapPin, Phone } from 'lucide-react';
import { Link } from 'react-router-dom';
import { settingsApi } from '@/api/masters';
import { Button, Card, PageHeader, Spinner } from '@/components/ui';
import type { PublicSettings } from '@/models/masters';

/** /contact — support and company details straight from MST_AppSetting
 * (M23; the values are data, edited on the admin Settings screen). */
export function ContactUsPage() {
  const [settings, setSettings] = useState<PublicSettings | null>(null);
  const [failed, setFailed] = useState(false);

  useEffect(() => {
    settingsApi.public().then(setSettings).catch(() => setFailed(true));
  }, []);

  if (!settings && !failed) {
    return <Spinner center />;
  }

  const rows = settings ? [
    { icon: Phone, label: 'Support', value: settings.supportContactNumber },
    { icon: Mail, label: 'Email', value: settings.supportEmail ?? settings.companyEmail },
    { icon: Clock, label: 'Hours', value: settings.supportHours },
    { icon: MapPin, label: 'Address', value: settings.companyAddress },
  ].filter((row) => row.value) : [];

  return (
    <>
      <PageHeader title="Contact Us"
        description={settings?.supportMessage ?? 'We are here to help.'} />
      <Card title={settings?.companyName ?? 'Regalis WorkSpace'}>
        {rows.length === 0 ? (
          <p style={{ color: 'var(--text-secondary)' }}>
            Contact details are being set up — meanwhile, raise a support ticket and
            we will get back to you.
          </p>
        ) : (
          <div className="form-stack">
            {rows.map(({ icon: Icon, label, value }) => (
              <div key={label}
                style={{ display: 'flex', gap: 'var(--space-3)', alignItems: 'center' }}>
                <Icon size={18} color="var(--regalis-gold-dark)" style={{ flexShrink: 0 }} />
                <div>
                  <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--text-muted)' }}>
                    {label}
                  </div>
                  <div style={{ fontWeight: 600 }}>{value}</div>
                </div>
              </div>
            ))}
          </div>
        )}
        <div style={{ marginTop: 'var(--space-4)' }}>
          <Link to="/support/raise">
            <Button variant="outline"><LifeBuoy size={16} /> Raise a support ticket</Button>
          </Link>
        </div>
      </Card>
    </>
  );
}
