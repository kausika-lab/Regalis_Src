import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { Bell, CalendarPlus, Coffee, LifeBuoy, QrCode, Ticket, Wallet } from 'lucide-react';
import { resolveMenuIcon } from '@/utils/menuIcons';
import { bannersApi, servicesApi } from '@/api/masters';
import { dashboardApi, type CustomerDashboard } from '@/api/reports';
import { Badge, Button, Card } from '@/components/ui';
import { useAuthStore } from '@/store/authStore';
import type { Banner, ServiceItem } from '@/models/masters';
import { formatDateTime, formatInr, formatTime } from '@/utils/formatters';

const BOOKING_STATUS: Record<number, { label: string; tone: 'info' | 'success' | 'warning' }> = {
  1: { label: 'Payment pending', tone: 'warning' },
  2: { label: 'Confirmed', tone: 'info' },
  3: { label: 'Checked in', tone: 'success' },
};

/** Customer home (UI_PLAN §5): banner carousel · featured services · today's
 * booking with its QR · quick tiles from the dynamic menu. */
export function CustomerHomePage() {
  const { user, menus } = useAuthStore();
  const [dashboard, setDashboard] = useState<CustomerDashboard | null>(null);
  const [banners, setBanners] = useState<Banner[]>([]);
  const [services, setServices] = useState<ServiceItem[]>([]);

  useEffect(() => {
    dashboardApi.customer().then(setDashboard).catch(() => {});
    bannersApi.active().then(setBanners).catch(() => {});
    servicesApi.list({ page: 1, pageSize: 10 })
      .then((result) => setServices(result.items)).catch(() => {});
  }, []);

  const booking = dashboard?.todaysBooking ?? null;
  const tiles = menus.filter((m) => m.route && m.route !== '/');

  return (
    <>
      <Card>
        <h2 style={{ fontSize: 'var(--font-size-xl)', fontWeight: 800 }}>
          Welcome, {user?.fullName ?? 'Guest'}
        </h2>
        <p style={{ color: 'var(--text-secondary)', marginTop: 'var(--space-2)' }}>
          Book a desk, cabin or meeting room, manage your wallet and passes — all in one place.
        </p>
        {dashboard && (
          <div className="row-actions" style={{ marginTop: 'var(--space-3)', flexWrap: 'wrap' }}>
            <Link to="/wallet"><Badge tone="gold">
              <Wallet size={12} /> {formatInr(dashboard.walletBalance)}
            </Badge></Link>
            <Link to="/passes"><Badge tone="info">
              <Ticket size={12} /> {dashboard.activePasses} active pass{dashboard.activePasses === 1 ? '' : 'es'}
            </Badge></Link>
            <Link to="/notifications"><Badge tone={dashboard.unreadNotifications > 0 ? 'warning' : 'neutral'}>
              <Bell size={12} /> {dashboard.unreadNotifications} unread
            </Badge></Link>
            {dashboard.openTickets > 0 && (
              <Link to="/support"><Badge tone="warning">
                <LifeBuoy size={12} /> {dashboard.openTickets} open ticket{dashboard.openTickets === 1 ? '' : 's'}
              </Badge></Link>
            )}
          </div>
        )}
      </Card>

      {banners.length > 0 && <BannerCarousel banners={banners} />}

      <Card title="Today's booking">
        {booking ? (
          <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'center', flexWrap: 'wrap' }}>
            <div style={{ flex: 1, minWidth: 220 }}>
              <div style={{ fontWeight: 700 }}>
                {booking.workspaceName}{' '}
                <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>
                  · {booking.workspaceTypeName}
                </span>
              </div>
              <div style={{ color: 'var(--text-secondary)', fontSize: 'var(--font-size-sm)' }}>
                {booking.bookingNumber} · {formatDateTime(booking.startUtc)} – {formatTime(booking.endUtc)} IST
              </div>
              <div style={{ marginTop: 'var(--space-2)' }}>
                <Badge tone={BOOKING_STATUS[booking.status]?.tone ?? 'neutral'}>
                  {BOOKING_STATUS[booking.status]?.label ?? 'Booked'}
                </Badge>
              </div>
            </div>
            <div className="row-actions">
              <Link to="/qr"><Button><QrCode size={16} /> View QR</Button></Link>
              <Link to="/bookings"><Button variant="outline">View booking</Button></Link>
            </div>
          </div>
        ) : (
          <div style={{ display: 'flex', gap: 'var(--space-4)', alignItems: 'center', flexWrap: 'wrap' }}>
            <div style={{ flex: 1, minWidth: 220 }}>
              <div style={{ fontWeight: 700 }}>No booking found</div>
              <div style={{ color: 'var(--text-secondary)', fontSize: 'var(--font-size-sm)' }}>
                {dashboard?.nextBooking
                  ? `Next up: ${dashboard.nextBooking.workspaceName}, ${formatDateTime(dashboard.nextBooking.startUtc)} IST`
                  : 'Nothing scheduled for today.'}
              </div>
            </div>
            <Link to="/book"><Button><CalendarPlus size={16} /> Book now</Button></Link>
          </div>
        )}
      </Card>

      {services.length > 0 && (
        <Card title="Featured services"
          actions={<Link to="/services"><Button size="sm" variant="ghost">See all</Button></Link>}>
          <div style={{ display: 'flex', gap: 'var(--space-3)', overflowX: 'auto', paddingBottom: 'var(--space-2)' }}>
            {services.map((item) => (
              <Link key={item.serviceItemId} to="/services" className="choice-card"
                style={{ minWidth: 150, flexShrink: 0, textDecoration: 'none' }}>
                <Coffee size={16} color="var(--regalis-gold-dark)" />
                <div className="choice-card__title" style={{ fontSize: 'var(--font-size-sm)' }}>
                  {item.itemName}
                </div>
                <div className="choice-card__meta">{formatInr(item.price)}</div>
              </Link>
            ))}
          </div>
        </Card>
      )}

      <div className="dash-grid">
        {tiles.map((tile) => {
          const Icon = resolveMenuIcon(tile.icon);
          return (
            <Link key={tile.menuId} to={tile.route!} className="dash-tile">
              <span className="dash-tile__icon"><Icon size={26} /></span>
              <span className="dash-tile__label">{tile.displayName}</span>
            </Link>
          );
        })}
      </div>
    </>
  );
}

/** Auto-sliding promotional carousel from MST_Banner. Content-first: the banner
 * renders as styled text over the house palette, with the image when it loads. */
function BannerCarousel({ banners }: { banners: Banner[] }) {
  const [index, setIndex] = useState(0);

  useEffect(() => {
    if (banners.length < 2) return;
    const timer = setInterval(() => setIndex((i) => (i + 1) % banners.length), 5000);
    return () => clearInterval(timer);
  }, [banners.length]);

  const banner = banners[index];
  return (
    <div style={{
      position: 'relative',
      background: 'var(--regalis-black, #0B0B0D)',
      borderRadius: 'var(--radius-lg, 12px)',
      padding: 'var(--space-6) var(--space-5)',
      marginBottom: 'var(--space-4)',
      overflow: 'hidden',
      color: '#fff',
    }}>
      <div style={{ position: 'relative', zIndex: 1 }}>
        <div style={{ fontWeight: 800, fontSize: 'var(--font-size-lg)', color: 'var(--regalis-gold, #C9A227)' }}>
          {banner.title}
        </div>
        {banner.subtitle && (
          <div style={{ marginTop: 'var(--space-1)', opacity: 0.85 }}>{banner.subtitle}</div>
        )}
        {banner.promotionalPrice !== null && (
          <div style={{ marginTop: 'var(--space-2)', fontWeight: 700 }}>
            {formatInr(banner.promotionalPrice)}
          </div>
        )}
      </div>
      {banners.length > 1 && (
        <div style={{ display: 'flex', gap: 6, marginTop: 'var(--space-4)', position: 'relative', zIndex: 1 }}>
          {banners.map((b, i) => (
            <button key={b.bannerId} type="button" aria-label={`Banner ${i + 1}`}
              onClick={() => setIndex(i)}
              style={{
                width: i === index ? 18 : 8, height: 8, borderRadius: 4, border: 'none',
                cursor: 'pointer', transition: 'width .2s',
                background: i === index ? 'var(--regalis-gold, #C9A227)' : 'rgba(255,255,255,.35)',
              }} />
          ))}
        </div>
      )}
    </div>
  );
}
