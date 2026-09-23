import { useCallback, useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import {
  ArrowRight,
  Bell,
  CalendarDays,
  CalendarPlus,
  CalendarRange,
  ChevronLeft,
  ChevronRight,
  LifeBuoy,
  QrCode,
  Ticket,
  Wallet,
} from 'lucide-react';
import { bannersApi } from '@/api/masters';
import { dashboardApi, type CustomerDashboard } from '@/api/reports';
import { Badge, Button, Card } from '@/components/ui';
import { useAuthStore } from '@/store/authStore';
import { useUiStore } from '@/store/uiStore';
import type { Banner } from '@/models/masters';
import { formatDateTime, formatInr, formatTime } from '@/utils/formatters';
import { greeting } from '@/utils/greeting';

const BOOKING_STATUS: Record<number, { label: string; tone: 'info' | 'success' | 'warning' }> = {
  1: { label: 'Payment pending', tone: 'warning' },
  2: { label: 'Confirmed', tone: 'info' },
  3: { label: 'Checked in', tone: 'success' },
};

/** The four things a member does most; each is a real customer route. */
const QUICK_LINKS = [
  { to: '/book', icon: CalendarPlus, title: 'Book a space', meta: 'Desks, cabins and meeting rooms' },
  { to: '/day-pass', icon: Ticket, title: 'Day pass', meta: 'Flexible access for a single day' },
  { to: '/monthly-pass', icon: CalendarRange, title: 'Monthly pass', meta: 'Unlimited access, one price' },
  { to: '/qr', icon: QrCode, title: 'My QR / access', meta: 'Scan in at the door' },
];

/** Customer home (UI_PLAN §5): greeting, workspace carousel, quick links,
 * today's booking and wallet. */
export function CustomerHomePage() {
  const { user } = useAuthStore();
  const locationId = useUiStore((state) => state.locationId);
  const [dashboard, setDashboard] = useState<CustomerDashboard | null>(null);
  const [banners, setBanners] = useState<Banner[]>([]);

  useEffect(() => {
    dashboardApi.customer().then(setDashboard).catch(() => {});
  }, []);

  // Re-fetch when the header switches workspace: banners are location-scoped.
  useEffect(() => {
    bannersApi.active(locationId ?? undefined).then(setBanners).catch(() => {});
  }, [locationId]);

  const booking = dashboard?.todaysBooking ?? null;

  return (
    <>
      <header className="home__greeting">
        <h2>
          {greeting()}, {user?.fullName ?? 'Guest'}
        </h2>
        <p>Let&apos;s make today productive.</p>
      </header>

      <BannerCarousel banners={banners} />

      <div className="home__quick">
        {QUICK_LINKS.map(({ to, icon: Icon, title, meta }) => (
          <Link key={to} to={to} className="home__tile">
            <span className="home__tile-icon">
              <Icon size={20} />
            </span>
            <span className="home__tile-title">{title}</span>
            <span className="home__tile-meta">{meta}</span>
            <ArrowRight size={16} className="home__tile-arrow" aria-hidden />
          </Link>
        ))}
      </div>

      <div className="home__columns">
        <Card
          title="Today's booking"
          actions={
            <Link to="/bookings">
              <Button size="sm" variant="ghost">
                View all
              </Button>
            </Link>
          }
        >
          {booking ? (
            <div className="home__booking">
              <div style={{ flex: 1, minWidth: 200 }}>
                <div style={{ fontWeight: 700 }}>
                  {booking.workspaceName}{' '}
                  <span style={{ color: 'var(--text-muted)', fontWeight: 400 }}>
                    · {booking.workspaceTypeName}
                  </span>
                </div>
                <div style={{ color: 'var(--text-secondary)', fontSize: 'var(--font-size-sm)' }}>
                  {booking.bookingNumber} · {formatDateTime(booking.startUtc)} –{' '}
                  {formatTime(booking.endUtc)} IST
                </div>
                <div style={{ marginTop: 'var(--space-2)' }}>
                  <Badge tone={BOOKING_STATUS[booking.status]?.tone ?? 'neutral'}>
                    {BOOKING_STATUS[booking.status]?.label ?? 'Booked'}
                  </Badge>
                </div>
              </div>
              <Link to="/qr">
                <Button>
                  <QrCode size={16} /> View QR
                </Button>
              </Link>
            </div>
          ) : (
            <div className="home__empty">
              <span className="home__empty-icon">
                <CalendarDays size={22} />
              </span>
              <div style={{ fontWeight: 700 }}>No booking today</div>
              <p>
                {dashboard?.nextBooking
                  ? `Next up: ${dashboard.nextBooking.workspaceName}, ${formatDateTime(
                      dashboard.nextBooking.startUtc,
                    )} IST`
                  : 'Nothing scheduled for today.'}
              </p>
              <Link to="/book">
                <Button>
                  <CalendarPlus size={16} /> Book now
                </Button>
              </Link>
            </div>
          )}
        </Card>

        <div className="home__aside">
          <Card title="Wallet balance">
            <div className="home__balance">{formatInr(dashboard?.walletBalance ?? 0)}</div>
            <p style={{ color: 'var(--text-secondary)', fontSize: 'var(--font-size-sm)' }}>
              Available credit for bookings, passes and services.
            </p>
            <Link to="/wallet" style={{ display: 'block', marginTop: 'var(--space-4)' }}>
              <Button variant="outline" style={{ width: '100%' }}>
                <Wallet size={16} /> Add money
              </Button>
            </Link>
          </Card>

          <Card title="At a glance">
            <ul className="home__glance">
              <li>
                <Link to="/passes">
                  <Ticket size={16} /> Active passes
                  <Badge tone={(dashboard?.activePasses ?? 0) > 0 ? 'info' : 'neutral'}>
                    {dashboard?.activePasses ?? 0}
                  </Badge>
                </Link>
              </li>
              <li>
                <Link to="/notifications">
                  <Bell size={16} /> Unread notifications
                  <Badge tone={(dashboard?.unreadNotifications ?? 0) > 0 ? 'warning' : 'neutral'}>
                    {dashboard?.unreadNotifications ?? 0}
                  </Badge>
                </Link>
              </li>
              <li>
                <Link to="/support">
                  <LifeBuoy size={16} /> Open tickets
                  <Badge tone={(dashboard?.openTickets ?? 0) > 0 ? 'warning' : 'neutral'}>
                    {dashboard?.openTickets ?? 0}
                  </Badge>
                </Link>
              </li>
            </ul>
          </Card>
        </div>
      </div>
    </>
  );
}

/**
 * The workspace hero. Slides come from MST_Banner (Admin → Banners), so the
 * imagery is content the team owns rather than anything hardcoded here: each
 * row's image path becomes the backdrop. A banner whose image will not load
 * still reads correctly — the house gradient shows through instead of a broken
 * frame — and with no banners at all this falls back to one brand slide.
 */
function BannerCarousel({ banners }: { banners: Banner[] }) {
  const [index, setIndex] = useState(0);
  const [broken, setBroken] = useState<Record<number, true>>({});
  const count = banners.length;

  const go = useCallback((next: number) => setIndex(((next % count) + count) % count), [count]);

  useEffect(() => {
    if (count < 2) return;
    // Two seconds a slide, per the brief.
    const timer = setInterval(() => setIndex((i) => (i + 1) % count), 2000);
    return () => clearInterval(timer);
  }, [count]);

  if (count === 0) {
    return (
      <section className="hero">
        <div className="hero__body">
          <h3 className="hero__title">Your workspace, your success</h3>
          <p className="hero__subtitle">
            Premium desks, private cabins and meeting rooms — designed for focus and growth.
          </p>
          <Link to="/book">
            <Button>Book a space</Button>
          </Link>
        </div>
      </section>
    );
  }

  const banner = banners[Math.min(index, count - 1)];
  const showImage = Boolean(banner.imagePath) && !broken[banner.bannerId];

  return (
    <section className="hero" aria-roledescription="carousel" aria-label="Workspace highlights">
      <div className="hero__body">
        <h3 className="hero__title">{banner.title}</h3>
        {banner.subtitle && <p className="hero__subtitle">{banner.subtitle}</p>}
        {banner.promotionalPrice !== null && (
          <p className="hero__price">{formatInr(banner.promotionalPrice)}</p>
        )}
        <Link to={banner.linkUrl?.startsWith('/') ? banner.linkUrl : '/book'}>
          <Button>Book a space</Button>
        </Link>
      </div>

      {/* The image lives in its own column rather than behind the copy, so the
          two can never overlap however light or busy the picture is. */}
      <div className="hero__figure">
        {showImage && (
          <img
            className="hero__image"
            src={banner.imagePath}
            alt=""
            onError={() => setBroken((b) => ({ ...b, [banner.bannerId]: true }))}
          />
        )}
      </div>

      {count > 1 && (
        <div className="hero__controls">
          <button
            type="button"
            className="hero__arrow"
            aria-label="Previous slide"
            onClick={() => go(index - 1)}
          >
            <ChevronLeft size={18} />
          </button>
          <div className="hero__dots">
            {banners.map((b, i) => (
              <button
                key={b.bannerId}
                type="button"
                className={`hero__dot${i === index ? ' hero__dot--active' : ''}`}
                aria-label={`Slide ${i + 1} of ${count}`}
                aria-current={i === index}
                onClick={() => go(i)}
              />
            ))}
          </div>
          <button
            type="button"
            className="hero__arrow"
            aria-label="Next slide"
            onClick={() => go(index + 1)}
          >
            <ChevronRight size={18} />
          </button>
        </div>
      )}
    </section>
  );
}
