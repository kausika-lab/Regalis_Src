import { create } from 'zustand';
import { persist } from 'zustand/middleware';

export type ThemeMode = 'light' | 'dark';

interface ThemeState {
  mode: ThemeMode;
  setMode: (mode: ThemeMode) => void;
  toggleMode: () => void;
}

const media = window.matchMedia('(prefers-color-scheme: dark)');

function apply(mode: ThemeMode) {
  document.documentElement.dataset.theme = mode;
  // Keep the browser UI (mobile address bar) in step with the theme.
  document
    .querySelector('meta[name="theme-color"]')
    ?.setAttribute('content', mode === 'dark' ? '#101014' : '#0b0b0d');
}

/** Appearance preference: light or dark, persisted per device and applied
 * before first paint. The topbar toggle flips between the two. */
export const useThemeStore = create<ThemeState>()(
  persist(
    (set) => ({
      mode: 'light',
      setMode: (mode) => set({ mode }),
      toggleMode: () => set((state) => ({ mode: state.mode === 'dark' ? 'light' : 'dark' })),
    }),
    {
      name: 'regalis-appearance',
      version: 2,
      // v1 stored a third "system" mode and an accent palette. A device left on
      // "system" settles onto whichever theme it was already resolving to, and
      // the stored accent is dropped — Regalis gold is the only accent now.
      migrate: (persisted) => {
        const previous = persisted as { mode?: string } | null;
        const mode: ThemeMode =
          previous?.mode === 'dark' || previous?.mode === 'light'
            ? previous.mode
            : media.matches
              ? 'dark'
              : 'light';
        return { mode } as ThemeState;
      },
    },
  ),
);

/** Called once from main.tsx before render, then kept live on every change. */
export function initTheme() {
  apply(useThemeStore.getState().mode);
  useThemeStore.subscribe((state) => apply(state.mode));
}
