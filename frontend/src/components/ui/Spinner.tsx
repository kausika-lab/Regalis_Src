interface SpinnerProps {
  size?: 'sm' | 'md' | 'lg';
  /** Centers the spinner in a padded block — the standard loading state. */
  center?: boolean;
  label?: string;
}

export function Spinner({ size = 'md', center = false, label = 'Loading' }: SpinnerProps) {
  const spinner = <span className={`spinner spinner--${size}`} role="status" aria-label={label} />;
  return center ? <div className="spinner-center">{spinner}</div> : spinner;
}
