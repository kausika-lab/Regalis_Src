import { forwardRef, useId, type InputHTMLAttributes } from 'react';

interface InputProps extends InputHTMLAttributes<HTMLInputElement> {
  label: string;
  error?: string;
  hint?: string;
}

/** Labelled input with error + hint slots. forwardRef so react-hook-form registers it. */
export const Input = forwardRef<HTMLInputElement, InputProps>(function Input(
  { label, error, hint, required, id, ...rest },
  ref,
) {
  const autoId = useId();
  const inputId = id ?? autoId;

  return (
    <div className="field">
      <label className="field__label" htmlFor={inputId}>
        {label}
        {required && (
          <span className="field__required" aria-hidden>
            {' '}
            *
          </span>
        )}
      </label>
      <input
        ref={ref}
        id={inputId}
        className={`field__input${error ? ' field__input--error' : ''}`}
        aria-invalid={Boolean(error)}
        aria-describedby={error ? `${inputId}-error` : undefined}
        required={required}
        {...rest}
      />
      {hint && !error && <span className="field__hint">{hint}</span>}
      {error && (
        <span className="field__error" id={`${inputId}-error`} role="alert">
          {error}
        </span>
      )}
    </div>
  );
});
