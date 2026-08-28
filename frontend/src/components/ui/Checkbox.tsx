import { useId, type InputHTMLAttributes } from 'react';

interface CheckboxProps extends Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> {
  label: string;
}

export function Checkbox({ label, id, ...rest }: CheckboxProps) {
  const autoId = useId();
  const inputId = id ?? autoId;
  return (
    <label className="checkbox" htmlFor={inputId}>
      <input type="checkbox" id={inputId} {...rest} />
      <span>{label}</span>
    </label>
  );
}
