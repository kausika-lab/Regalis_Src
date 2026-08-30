import { describe, expect, it } from 'vitest';
import { formatDate, formatDateTime, formatInr, formatTime } from './formatters';

describe('formatInr', () => {
  it('formats amounts in Indian digit grouping with the rupee symbol', () => {
    // Normalize the non-breaking spaces Intl may emit between symbol and digits.
    const normalized = formatInr(1234567.5).replace(/ | /g, ' ');
    expect(normalized).toContain('₹');
    expect(normalized).toContain('12,34,567.5');
  });

  it('formats zero without sign artifacts', () => {
    expect(formatInr(0)).toContain('0');
    expect(formatInr(0)).not.toContain('-');
  });
});

describe('UTC to IST rendering (A13: store UTC, render IST)', () => {
  // 10:00 UTC = 15:30 IST on the same day.
  const withZone = '2026-08-30T10:00:00Z';
  const withoutZone = '2026-08-30T10:00:00';

  it('treats zone-less API timestamps as UTC, never local time', () => {
    expect(formatDateTime(withoutZone)).toBe(formatDateTime(withZone));
    expect(formatTime(withoutZone)).toBe(formatTime(withZone));
  });

  it('renders the IST date', () => {
    expect(formatDate(withZone)).toBe('30 Aug 2026');
  });

  it('shifts the clock by +05:30', () => {
    expect(formatTime(withZone).toLowerCase()).toContain('3:30');
  });

  it('rolls the date forward when IST crosses midnight', () => {
    // 20:00 UTC on the 30th is 01:30 IST on the 31st.
    expect(formatDate('2026-08-30T20:00:00Z')).toBe('31 Aug 2026');
  });

  it('accepts Date instances as well as ISO strings', () => {
    const asDate = new Date(withZone);
    expect(formatDateTime(asDate)).toBe(formatDateTime(withZone));
  });
});
