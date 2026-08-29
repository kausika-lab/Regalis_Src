/** INR currency and IST date/time formatting (A13: store UTC, render IST). */

const TIME_ZONE = 'Asia/Kolkata';

const currency = new Intl.NumberFormat('en-IN', {
  style: 'currency',
  currency: 'INR',
  maximumFractionDigits: 2,
});

const dateTime = new Intl.DateTimeFormat('en-IN', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: 'short',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hour12: true,
});

const dateOnly = new Intl.DateTimeFormat('en-IN', {
  timeZone: TIME_ZONE,
  day: '2-digit',
  month: 'short',
  year: 'numeric',
});

const timeOnly = new Intl.DateTimeFormat('en-IN', {
  timeZone: TIME_ZONE,
  hour: '2-digit',
  minute: '2-digit',
  hour12: true,
});

export function formatInr(amount: number): string {
  return currency.format(amount);
}

/** API timestamps are UTC; older payloads may lack the Z suffix — never let the
 * browser parse them as local time. */
function toUtcDate(utc: string | Date): Date {
  if (utc instanceof Date) {
    return utc;
  }
  const needsZone = !/[zZ]|[+-]\d{2}:?\d{2}$/.test(utc);
  return new Date(needsZone ? `${utc}Z` : utc);
}

/** Accepts an ISO UTC string from the API and renders it in IST. */
export function formatDateTime(utc: string | Date): string {
  return dateTime.format(toUtcDate(utc));
}

export function formatDate(utc: string | Date): string {
  return dateOnly.format(toUtcDate(utc));
}

export function formatTime(utc: string | Date): string {
  return timeOnly.format(toUtcDate(utc));
}
