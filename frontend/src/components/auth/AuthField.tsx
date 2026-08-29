import { useState, type InputHTMLAttributes, type ReactNode } from 'react';
import { Eye, EyeOff } from 'lucide-react';
import type { UseFormRegisterReturn } from 'react-hook-form';

interface AuthFieldProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: string;
  icon: ReactNode;
  type?: string;
  error?: string;
  optional?: boolean;
  registration?: UseFormRegisterReturn;
}

/** Iconed cream input in the design language, with an eye toggle for passwords. */
export function AuthField({
  label,
  icon,
  type = 'text',
  error,
  optional = false,
  registration,
  ...rest
}: AuthFieldProps) {
  const [revealed, setRevealed] = useState(false);
  const isPassword = type === 'password';
  const effectiveType = isPassword && revealed ? 'text' : type;

  return (
    <div>
      <div className="auth__label">
        {label}{' '}
        {optional ? (
          <span className="auth__optional">(Optional)</span>
        ) : (
          <span className="auth__required" aria-hidden>
            *
          </span>
        )}
      </div>
      <div className={`auth__input${error ? ' auth__input--error' : ''}`}>
        {icon}
        <input type={effectiveType} aria-invalid={Boolean(error)} {...registration} {...rest} />
        {isPassword && (
          <button
            type="button"
            className="auth__eye"
            onClick={() => setRevealed((r) => !r)}
            aria-label={revealed ? 'Hide password' : 'Show password'}
          >
            {revealed ? <EyeOff size={20} /> : <Eye size={20} />}
          </button>
        )}
      </div>
      <div className="auth__error" role={error ? 'alert' : undefined}>
        {error}
      </div>
    </div>
  );
}
