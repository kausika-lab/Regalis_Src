/**
 * Time-of-day greeting for the dashboards, read from the device clock — the
 * greeting should match the light outside the member's window, not the IST
 * timestamps the rest of the app renders.
 */
export function greeting(now: Date = new Date()): string {
  const hour = now.getHours();
  if (hour < 12) return 'Good morning';
  if (hour < 17) return 'Good afternoon';
  return 'Good evening';
}
