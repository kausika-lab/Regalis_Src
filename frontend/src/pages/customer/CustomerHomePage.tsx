import { Link } from 'react-router-dom';
import { icons, type LucideIcon } from 'lucide-react';
import { Card, EmptyState } from '@/components/ui';
import { useAuthStore } from '@/store/authStore';

/**
 * Customer home shell. Quick actions render from the dynamic menu tree, so the
 * tiles grow as modules land without a redeploy.
 */
export function CustomerHomePage() {
  const { user, menus } = useAuthStore();
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
      </Card>

      {tiles.length === 0 ? (
        <Card title="Quick actions">
          <EmptyState
            title="Nothing here yet"
            description="Your menu is assigned by your role. New modules appear here automatically as they go live."
          />
        </Card>
      ) : (
        <div className="dash-grid">
          {tiles.map((tile) => {
            const Icon = resolveIcon(tile.icon);
            return (
              <Link key={tile.menuId} to={tile.route!} className="dash-tile">
                <span className="dash-tile__icon">
                  <Icon size={26} />
                </span>
                <span className="dash-tile__label">{tile.displayName}</span>
              </Link>
            );
          })}
        </div>
      )}
    </>
  );
}

function resolveIcon(name: string | null): LucideIcon {
  if (name && name in icons) {
    return icons[name as keyof typeof icons];
  }
  return icons.LayoutGrid;
}
