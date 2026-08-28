import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';

export function ForbiddenPage() {
  return (
    <div className="error-page">
      <div className="error-page__code">403</div>
      <h1>Access denied</h1>
      <p style={{ color: 'var(--text-secondary)' }}>
        Your account doesn&apos;t have permission to view this page. Contact your
        administrator if you believe this is a mistake.
      </p>
      <Link to="/">
        <Button variant="outline">Back to home</Button>
      </Link>
    </div>
  );
}
