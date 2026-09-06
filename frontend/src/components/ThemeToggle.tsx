import { Moon, Sun } from 'lucide-react';
import { useThemeStore } from '@/store/themeStore';

/**
 * Topbar light/dark switch. Only the theme currently in use is shown — the sun
 * on a light screen, the moon on a dark one — and a click flips to the other.
 * The choice is per device (localStorage) and applies to both portals.
 */
export function ThemeToggle() {
  const mode = useThemeStore((state) => state.mode);
  const toggleMode = useThemeStore((state) => state.toggleMode);
  const isDark = mode === 'dark';
  const Icon = isDark ? Moon : Sun;

  return (
    <button
      type="button"
      className="portal__bell"
      onClick={toggleMode}
      title={isDark ? 'Dark theme — switch to light' : 'Light theme — switch to dark'}
      aria-label={isDark ? 'Switch to light theme' : 'Switch to dark theme'}
    >
      <Icon size={20} />
    </button>
  );
}
