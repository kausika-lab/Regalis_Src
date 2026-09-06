import { useEffect, useRef, useState } from 'react';
import { Link } from 'react-router-dom';
import { ChevronsUpDown, LogOut, Settings, UserRound } from 'lucide-react';
import { usePermission } from '@/hooks/usePermission';
import type { Portal } from '@/models/auth';
import { useAuthStore } from '@/store/authStore';

/** "Kausika Ramesh" → "KR"; a single name gives one letter. */
function initialsOf(name: string): string {
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return '?';
  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : '')).toUpperCase();
}

/**
 * Topbar account control: the signed-in identity, opening a menu with Profile,
 * Settings and Logout. Settings is admin-only — it is the one entry that has no
 * customer-portal route, and offering it to a customer would only bounce them.
 */
export function AccountMenu({ portal, onLogout }: { portal: Portal; onLogout: () => void }) {
  const user = useAuthStore((state) => state.user);
  const canOpenSettings = usePermission('SETTING.VIEW');
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (ref.current && !ref.current.contains(event.target as Node)) {
        setOpen(false);
      }
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === 'Escape') setOpen(false);
    };
    document.addEventListener('pointerdown', onPointerDown);
    document.addEventListener('keydown', onKeyDown);
    return () => {
      document.removeEventListener('pointerdown', onPointerDown);
      document.removeEventListener('keydown', onKeyDown);
    };
  }, [open]);

  const close = () => setOpen(false);
  const showSettings = portal === 'admin' && canOpenSettings;

  return (
    <div className="account" ref={ref}>
      <button
        type="button"
        className="account__trigger"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <span className="account__avatar" aria-hidden>
          {initialsOf(user?.fullName ?? '')}
        </span>
        <span className="account__identity">
          <span className="account__name">{user?.fullName ?? 'Signed in'}</span>
          <span className="account__email">{user?.email}</span>
        </span>
        <ChevronsUpDown size={16} className="account__chevron" aria-hidden />
      </button>

      {open && (
        <div className="account__popover" role="menu" aria-label="My Account">
          <div className="account__section">My Account</div>
          <Link
            to={portal === 'admin' ? '/admin/profile' : '/profile'}
            className="account__item"
            role="menuitem"
            onClick={close}
          >
            <UserRound size={16} /> Profile
          </Link>
          {showSettings && (
            <Link to="/admin/settings" className="account__item" role="menuitem" onClick={close}>
              <Settings size={16} /> Settings
            </Link>
          )}
          <button
            type="button"
            className="account__item"
            role="menuitem"
            onClick={() => {
              close();
              onLogout();
            }}
          >
            <LogOut size={16} /> Logout
          </button>
        </div>
      )}
    </div>
  );
}
