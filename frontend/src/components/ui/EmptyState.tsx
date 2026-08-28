import type { ReactNode } from 'react';
import { Inbox } from 'lucide-react';

interface EmptyStateProps {
  title: string;
  description?: string;
  icon?: ReactNode;
  action?: ReactNode;
}

export function EmptyState({ title, description, icon, action }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <div className="empty-state__icon">{icon ?? <Inbox size={44} />}</div>
      <p className="empty-state__title">{title}</p>
      {description && <p>{description}</p>}
      {action}
    </div>
  );
}
