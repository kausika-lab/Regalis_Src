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

/** Accepts an ISO UTC string from the API and renders it in IST. */
export function formatDateTime(utc: string | Date): string {
  return dateTime.format(typeof utc === 'string' ? new Date(utc) : utc);
}

export function formatDate(utc: string | Date): string {
  return dateOnly.format(typeof utc === 'string' ? new Date(utc) : utc);
}

export function formatTime(utc: string | Date): string {
  return timeOnly.format(typeof utc === 'string' ? new Date(utc) : utc);
}
