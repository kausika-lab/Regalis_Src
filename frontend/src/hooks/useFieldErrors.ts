import { useCallback, useState } from 'react';
import { ApiClientError } from '@/api/client';

/**
 * Turns the API's field-level validation errors into a per-input lookup.
 *
 * FluentValidation and the model-binding filter both return errors carrying the
 * offending property name (`Description`, `Lines[0].Quantity`), so the same 400 that
 * shows a summary can also mark the individual field. Property names arrive
 * PascalCase from .NET while form state is camelCase, so the first character is
 * lowered before matching.
 */
export function useFieldErrors() {
  const [fieldErrors, setFieldErrors] = useState<Record<string, string>>({});
  const [summary, setSummary] = useState<string | null>(null);

  const clear = useCallback(() => {
    setFieldErrors({});
    setSummary(null);
  }, []);

  /**
   * Records an API failure. Returns the message worth showing in a toast — null when
   * every error was field-level and is already rendered against its input.
   */
  const capture = useCallback((error: unknown): string | null => {
    if (!(error instanceof ApiClientError)) {
      const message = error instanceof Error ? error.message : 'Something went wrong.';
      setFieldErrors({});
      setSummary(message);
      return message;
    }

    const mapped: Record<string, string> = {};
    for (const item of error.errors) {
      if (!item.field) {
        continue;
      }
      const key = toCamelCase(item.field);
      // Keep the first error per field: FluentValidation reports in rule order and
      // the first one is the most specific thing to fix.
      mapped[key] ??= item.message;
    }

    setFieldErrors(mapped);

    const unfielded = error.errors.filter((e) => !e.field);
    const message = unfielded.length > 0 ? unfielded[0].message : error.message;
    // A pure field-level 400 needs no banner — the inputs carry it.
    const showSummary = Object.keys(mapped).length === 0 || unfielded.length > 0;
    setSummary(showSummary ? message : null);
    return showSummary ? message : null;
  }, []);

  /** Clears one field's error as the user edits it. */
  const clearField = useCallback((name: string) => {
    setFieldErrors((current) => {
      if (!(name in current)) {
        return current;
      }
      const next = { ...current };
      delete next[name];
      return next;
    });
  }, []);

  return { fieldErrors, summary, capture, clear, clearField };
}

/** `Description` → `description`; `Lines[0].Quantity` → `lines[0].quantity`. */
function toCamelCase(field: string): string {
  return field
    .split('.')
    .map((part) => (part.length > 0 ? part[0].toLowerCase() + part.slice(1) : part))
    .join('.');
}
