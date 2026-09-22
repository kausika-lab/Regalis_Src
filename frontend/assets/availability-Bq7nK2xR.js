/* Workspace availability for the admin dashboard.

   Deliberately dependency-free (no app imports, no React) so the calculation can
   be unit tested outside a browser -- see frontend/tests/availability.test.mjs.

   The rule: a workspace is occupied TODAY if it has at least one booking that
   touches today's IST calendar day and was not cancelled or expired. Check-in is
   irrelevant. This replaced an "active at this exact instant" test, which made a
   morning booking disappear from the panel by lunchtime. */

const IST = "Asia/Kolkata";
const ISTDAY = new Intl.DateTimeFormat("en-CA", { timeZone: IST, year: "numeric", month: "2-digit", day: "2-digit" });

/* A naked timestamp from the API is UTC; Date.parse would read it as local. */
export function toUtc(v) {
  return v instanceof Date ? v : new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(v) ? v : `${v}Z`);
}
export function istDate(v) {
  return ISTDAY.format(toUtc(v));
}

/* BookingStatus: 1 Pending, 2 Confirmed, 3 CheckedIn, 4 Completed,
   5 Cancelled, 6 Expired, 7 NoShow.
   Cancelled and Expired never occupied the seat. Everything else did -- including
   Completed (the booking happened) and NoShow (the seat was held and nobody else
   could take it). Pending is a live hold that range-locks the slot. */
export const DEAD_STATUSES = new Set([5, 6]);

/* WorkspaceStatus: 1 Available, 2 Occupied, 3 Maintenance, 4 Disabled.
   Maintenance and Disabled units are out of service, so they belong in neither
   the numerator nor the denominator. */
export function isCountable(w) {
  return w.isActive !== false && w.isBookable !== false && w.status !== 3 && w.status !== 4;
}

/* Counts a booking that spans midnight, not just one starting today. Dates are
   YYYY-MM-DD so string comparison is chronological. A booking ending exactly at
   midnight is attributed to the day it ends, which is conservative. */
export function occupiesDay(b, day) {
  const s = istDate(b.startUtc), e = istDate(b.endUtc);
  return s === day || (s < day && e >= day);
}

/* Virtual Office is an address service sold by the month, not a seat with a cap,
   so it reports "N active" rather than an occupancy ratio: total stays null,
   which keeps it out of every occupancy sum. Detected by name, as
   BookWorkspacePage does -- the API's requiresCapacity flag is false for Desk
   too, so it cannot stand in for "unlimited". */
const isUncapped = (ty) => /virtual/i.test(ty.typeName ?? "");

export function computeAvailability({ bookings = [], workspaces = [], types = [], today } = {}) {
  const day = today ?? istDate(new Date());

  const occupied = new Set();
  for (const b of bookings) {
    if (DEAD_STATUSES.has(b.status)) continue;
    if (!occupiesDay(b, day)) continue;
    occupied.add(b.workspaceId);
  }

  const by = new Map();
  for (const ty of types) {
    by.set(ty.workspaceTypeId, {
      name: ty.typeName,
      total: isUncapped(ty) ? null : 0,
      occupied: 0,
      active: 0,
      booked: 0,
    });
  }

  for (const w of workspaces) {
    if (!isCountable(w)) continue;
    const cur = by.get(w.workspaceTypeId);
    if (!cur) continue;
    cur.active += 1;
    const occ = occupied.has(w.workspaceId);
    if (occ) cur.booked += 1;
    if (cur.total !== null) {
      cur.total += 1;
      if (occ) cur.occupied += 1;
    }
  }

  /* Capped types keep the server order; uncapped ones sit under them. */
  const live = [...by.values()].filter((v) => v.total === null || v.total > 0);
  return live.filter((v) => v.total !== null).concat(live.filter((v) => v.total === null));
}
