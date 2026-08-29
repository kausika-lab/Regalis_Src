import { useEffect, useRef, useState } from 'react';
import { Check, Monitor, Moon, Palette, Sun } from 'lucide-react';
import { ACCENTS, useThemeStore, type ThemeMode } from '@/store/themeStore';

const MODES: { key: ThemeMode; label: string; icon: typeof Sun }[] = [
  { key: 'light', label: 'Light', icon: Sun },
  { key: 'dark', label: 'Dark', icon: Moon },
  { key: 'system', label: 'System', icon: Monitor },
];

/** Topbar appearance settings: light/dark/system + accent palette. The choice is
 * per device (localStorage) and applies instantly to both portals. */
export function AppearanceMenu() {
  const { mode, accent, setMode, setAccent } = useThemeStore();
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

  return (
    <div className="appearance" ref={ref}>
      <button
        type="button"
        className="portal__bell"
        aria-label="Appearance settings"
        aria-expanded={open}
        onClick={() => setOpen((o) => !o)}
      >
        <Palette size={20} />
      </button>

      {open && (
        <div className="appearance__popover" role="dialog" aria-label="Appearance">
          <div className="appearance__section">Theme</div>
          <div className="appearance__modes">
            {MODES.map(({ key, label, icon: Icon }) => (
              <button
                key={key}
                type="button"
                className={`appearance__mode${mode === key ? ' appearance__mode--active' : ''}`}
                onClick={() => setMode(key)}
              >
                <Icon size={15} /> {label}
              </button>
            ))}
          </div>

          <div className="appearance__section">Accent</div>
          <div className="appearance__accents">
            {ACCENTS.map((option) => (
              <button
                key={option.key}
                type="button"
                className="appearance__accent"
                style={{ background: option.swatch }}
                title={option.label}
                aria-label={option.label}
                aria-pressed={accent === option.key}
                onClick={() => setAccent(option.key)}
              >
                {accent === option.key && <Check size={14} strokeWidth={3} />}
              </button>
            ))}
          </div>
          <div className="appearance__hint">Saved on this device.</div>
        </div>
      )}
    </div>
  );
}
