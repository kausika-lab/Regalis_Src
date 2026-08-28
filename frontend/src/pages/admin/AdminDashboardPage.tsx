import { Link } from 'react-router-dom';
import { icons, type LucideIcon } from 'lucide-react';
import { Card, EmptyState } from '@/components/ui';
import { useAuthStore } from '@/store/authStore';

/** Admin dashboard shell. Module tiles render from the dynamic menu tree. */
export function AdminDashboardPage() {
  const { user, roles, menus } = useAuthStore();
  const tiles = menus.filter((m) => m.route && m.route !== '/admin');

  return (
    <>
      <Card>
        <h2 style={{ fontSize: 'var(--font-size-xl)', fontWeight: 800 }}>
          {user?.fullName ?? 'Administrator'}
        </h2>
        <p style={{ color: 'var(--text-secondary)', marginTop: 'var(--space-2)' }}>
          Signed in as {roles.join(', ') || 'no role assigned'}. Operational modules appear
          below as your role is granted access.
        </p>
      </Card>

      {tiles.length === 0 ? (
        <Card title="Modules">
          <EmptyState
            title="No modules assigned"
            description="Menus and permissions are managed in Administration → Roles & Permissions."
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
