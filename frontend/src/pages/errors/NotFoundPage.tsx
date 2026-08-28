import { Link } from 'react-router-dom';
import { Button } from '@/components/ui';

export function NotFoundPage() {
  return (
    <div className="error-page">
      <div className="error-page__code">404</div>
      <h1>Page not found</h1>
      <p style={{ color: 'var(--text-secondary)' }}>
        The page you are looking for doesn&apos;t exist or has been moved.
      </p>
      <Link to="/">
        <Button variant="outline">Back to home</Button>
      </Link>
    </div>
  );
}
