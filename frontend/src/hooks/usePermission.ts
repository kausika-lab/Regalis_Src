import { useAuthStore } from '@/store/authStore';

/**
 * Cosmetic permission check for hiding buttons/sections. The API is the real
 * gate — every controller action enforces its own permission.
 */
export function usePermission(code: string): boolean {
  return useAuthStore((state) => state.permissions.includes(code));
}
