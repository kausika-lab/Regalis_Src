import { describe, expect, it } from 'vitest';
import { greeting } from './greeting';

/** Local time, since the greeting follows the device clock. */
const at = (hour: number, minute = 0) => new Date(2026, 8, 5, hour, minute);

describe('greeting', () => {
  it('greets the morning from midnight until noon', () => {
    expect(greeting(at(0))).toBe('Good morning');
    expect(greeting(at(6, 30))).toBe('Good morning');
    expect(greeting(at(11, 59))).toBe('Good morning');
  });

  it('greets the afternoon from noon until 5pm', () => {
    expect(greeting(at(12))).toBe('Good afternoon');
    expect(greeting(at(16, 59))).toBe('Good afternoon');
  });

  it('greets the evening from 5pm until midnight', () => {
    expect(greeting(at(17))).toBe('Good evening');
    expect(greeting(at(23, 59))).toBe('Good evening');
  });
});
