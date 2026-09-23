import { create } from 'zustand';
import { persist } from 'zustand/middleware';

interface UiState {
  /** Desktop only — below 1024px the sidebar is a drawer instead. */
  sidebarCollapsed: boolean;
  toggleSidebar: () => void;
  /** The workspace the member is looking at; null until the list loads. */
  locationId: number | null;
  setLocationId: (locationId: number) => void;
}

/** Shell preferences that should outlive a reload, kept per device. */
export const useUiStore = create<UiState>()(
  persist(
    (set) => ({
      sidebarCollapsed: false,
      toggleSidebar: () => set((state) => ({ sidebarCollapsed: !state.sidebarCollapsed })),
      locationId: null,
      setLocationId: (locationId) => set({ locationId }),
    }),
    { name: 'regalis-ui' },
  ),
);
