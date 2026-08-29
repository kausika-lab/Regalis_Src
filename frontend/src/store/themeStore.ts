import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type ThemeMode = 'light' | 'dark' | 'system';
export type AccentKey = 'gold' | 'emerald' | 'sapphire' | 'ruby' | 'violet';

export const ACCENTS: { key: AccentKey; label: string; swatch: string }[] = [
  { key: 'gold', label: 'Regalis Gold', swatch: '#c9a227' },
  { key: 'emerald', label: 'Emerald', swatch: '#0e9f6e' },
  { key: 'sapphire', label: 'Sapphire', swatch: '#2563eb' },
  { key: 'ruby', label: 'Ruby', swatch: '#e11d48' },
  { key: 'violet', label: 'Violet', swatch: '#7c3aed' },
];

interface ThemeState {
  mode: ThemeMode;
  accent: AccentKey;
  setMode: (mode: ThemeMode) => void;
  setAccent: (accent: AccentKey) => void;
}

const media = window.matchMedia('(prefers-color-scheme: dark)');

function apply(mode: ThemeMode, accent: AccentKey) {
  const resolved = mode === 'system' ? (media.matches ? 'dark' : 'light') : mode;
  const root = document.documentElement;
  root.dataset.theme = resolved;
  root.dataset.accent = accent;
  // Keep the browser UI (mobile address bar) in step with the theme.
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute('content', resolved === 'dark' ? '#101014' : '#0b0b0d');
}

/** Appearance preference (Day 13 client request): light/dark/system + accent
 * palette, persisted per device and applied before first paint. */
export const useThemeStore = create<ThemeState>()(
  persist(
    (set) => ({
      mode: 'light',
      accent: 'gold',
      setMode: (mode) => set({ mode }),
      setAccent: (accent) => set({ accent }),
    }),
    { name: 'regalis-appearance' },
  ),
);

/** Called once from main.tsx before render, then kept live on every change. */
export function initTheme() {
  const applyCurrent = () => {
    const { mode, accent } = useThemeStore.getState();
    apply(mode, accent);
  };
  applyCurrent();
  useThemeStore.subscribe(applyCurrent);
  media.addEventListener('change', () => {
    if (useThemeStore.getState().mode === 'system') {
      applyCurrent();
    }
  });
}
