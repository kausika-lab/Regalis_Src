import type { ReactNode } from 'react';

interface CardProps {
  title?: string;
  actions?: ReactNode;
  children: ReactNode;
  className?: string;
}

export function Card({ title, actions, children, className }: CardProps) {
  return (
    <section className={`card${className ? ` ${className}` : ''}`}>
      {(title || actions) && (
        <header className="card__header">
          {title && <h2 className="card__title">{title}</h2>}
          {actions}
        </header>
      )}
      <div className="card__body">{children}</div>
    </section>
  );
}
