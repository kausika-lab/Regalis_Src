import { useEffect, useState } from 'react';
import { Link } from 'react-router-dom';
import { dashboardApi, type AdminDashboard } from '@/api/reports';
import { Card, EmptyState } from '@/components/ui';
import { useAuthStore } from '@/store/authStore';
import { resolveMenuIcon } from '@/utils/menuIcons';

/** Admin dashboard: live aggregate cards + module tiles from the menu tree. */
export function AdminDashboardPage() {
  const { user, roles, menus } = useAuthStore();
  const tiles = menus.filter((m) => m.route && m.route !== '/admin');
  const [dashboard, setDashboard] = useState<AdminDashboard | null>(null);

  useEffect(() => {
    dashboardApi.admin().then(setDashboard).catch(() => {});
  }, []);

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

      {dashboard && (
        <div className="choice-grid" style={{ marginBottom: 'var(--space-4)' }}>
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
      )}

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
            const Icon = resolveMenuIcon(tile.icon);
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
