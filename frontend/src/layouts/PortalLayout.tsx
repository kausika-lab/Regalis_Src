import { useCallback, useEffect, useState } from 'react';
import { Link, NavLink, Outlet, useLocation, useNavigate } from 'react-router-dom';
import { Bell, PanelLeft } from 'lucide-react';
import { AccountMenu } from '@/components/AccountMenu';
import { ThemeToggle } from '@/components/ThemeToggle';
import { resolveMenuIcon } from '@/utils/menuIcons';
import { authApi } from '@/api/auth';
import { notificationApi } from '@/api/finance';
import { Button } from '@/components/ui';
import type { MenuItem, Portal } from '@/models/auth';
import { useAuthStore } from '@/store/authStore';
import { useUiStore } from '@/store/uiStore';

/**
 * The shared shell for both portals: dark sidebar built from GET /menus/my
 * (dynamic — adding a screen is a data insert, ARCHITECTURE §5), topbar with
 * the signed-in user, content outlet. Responsive: the sidebar collapses below 1024px.
 */
export function PortalLayout({ title, portal }: { title: string; portal: Portal }) {
  const navigate = useNavigate();
  const location = useLocation();
  const { refreshToken, menus, setMenus, clearSession } = useAuthStore();
  const [sidebarOpen, setSidebarOpen] = useState(false);
  const { sidebarCollapsed, toggleSidebar } = useUiStore();
  const [unreadCount, setUnreadCount] = useState(0);

  // Refresh the bell on navigation — cheap and keeps the badge honest.
  useEffect(() => {
    notificationApi.unreadCount().then(setUnreadCount).catch(() => {});
  }, [location.pathname]);

  useEffect(() => {
    let cancelled = false;
    authApi
      .myMenus()
      .then((items) => {
        if (!cancelled) {
          setMenus(items);
        }
      })
      .catch(() => {
        // Menus are cosmetic navigation; API authorization still guards every call.
      });
    return () => {
      cancelled = true;
    };
  }, [setMenus]);

  const logout = useCallback(async () => {
    try {
      if (refreshToken) {
        await authApi.logout(refreshToken);
      }
    } finally {
      clearSession();
      navigate('/login', { replace: true });
    }
  }, [refreshToken, clearSession, navigate]);

  return (
    <div className={`portal${sidebarCollapsed ? ' portal--collapsed' : ''}`}>
      {sidebarOpen && <div className="portal__backdrop" onClick={() => setSidebarOpen(false)} />}
      <aside className={`portal__sidebar${sidebarOpen ? ' portal__sidebar--open' : ''}`}>
        <div className="portal__brand">
          <img src="/icons/icon.svg" alt="" />
          <span className="portal__brand-name">
            REGALIS <em>WS</em>
          </span>
        </div>
        <nav className="portal__nav" aria-label="Main navigation">
          {menus.length === 0 ? (
            <p className="portal__nav-empty">No menus assigned to your role yet.</p>
          ) : (
            menus.map((item) => (
              <SidebarLink key={item.menuId} item={item} onNavigate={() => setSidebarOpen(false)} />
            ))
          )}
        </nav>
      </aside>

      <div className="portal__main">
        <header className="portal__topbar">
          <div style={{ display: 'flex', alignItems: 'center', gap: 'var(--space-3)' }}>
            <span className="portal__menu-toggle">
              <Button
                variant="ghost"
                size="sm"
                onClick={() => setSidebarOpen((open) => !open)}
                aria-label="Toggle menu"
                aria-expanded={sidebarOpen}
              >
                <PanelLeft size={20} />
              </Button>
            </span>
            <span className="portal__collapse-toggle">
              <Button
                variant="ghost"
                size="sm"
                onClick={toggleSidebar}
                aria-label={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
                aria-expanded={!sidebarCollapsed}
                title={sidebarCollapsed ? 'Expand sidebar' : 'Collapse sidebar'}
              >
                <PanelLeft size={20} />
              </Button>
            </span>
            {/* The brand already names the product in the sidebar; the topbar
                carries only controls. The heading stays for assistive tech. */}
            <h1 className="sr-only">{title}</h1>
          </div>
          <div className="portal__user">
            <ThemeToggle />
            <Link
              to={portal === 'admin' ? '/admin/notifications' : '/notifications'}
              className="portal__bell"
              aria-label="Notifications"
            >
              <Bell size={20} />
              {unreadCount > 0 && (
                <span className="portal__bell-badge">{unreadCount > 99 ? '99+' : unreadCount}</span>
              )}
            </Link>
            <AccountMenu portal={portal} onLogout={logout} />
          </div>
        </header>
        <main className="portal__content">
          <Outlet />
        </main>
      </div>
    </div>
  );
}

function SidebarLink({ item, onNavigate }: { item: MenuItem; onNavigate: () => void }) {
  const Icon = resolveMenuIcon(item.icon);
  if (!item.route) {
    // Pure grouping node: render its children flat for now.
    return (
      <>
        {item.children.map((child) => (
          <SidebarLink key={child.menuId} item={child} onNavigate={onNavigate} />
        ))}
      </>
    );
  }
  return (
    <NavLink
      to={item.route}
      className="portal__nav-link"
      onClick={onNavigate}
      title={item.displayName}
    >
      <Icon size={18} />
      <span className="portal__nav-label">{item.displayName}</span>
    </NavLink>
  );
}
