import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { Lock, Mail } from 'lucide-react';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { AuthField } from '@/components/auth/AuthField';
import { useToast } from '@/components/ui/Toast';
import { AuthBrand } from '@/layouts/AuthLayout';
import type { Portal } from '@/models/auth';
import { useAuthStore } from '@/store/authStore';

const loginSchema = z.object({
  email: z.email('Enter a valid email address.'),
  password: z.string().min(1, 'Password is required.'),
});

type LoginForm = z.infer<typeof loginSchema>;

export function LoginPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const setSession = useAuthStore((state) => state.setSession);
  const [portal, setPortal] = useState<Portal>('customer');

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<LoginForm>({ resolver: zodResolver(loginSchema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const session = await authApi.login({ ...values, portal });
      setSession(session);
      const from = (location.state as { from?: string } | null)?.from;
      navigate(from ?? (portal === 'admin' ? '/admin' : '/'), { replace: true });
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Sign in failed. Please try again.');
    }
  });

  return (
    <div className="auth__panel">
      <AuthBrand />
      <h1 className="auth__title">Welcome Back</h1>
      <p className="auth__subtitle">Please enter your details to sign in</p>

      <div className="auth__seg" role="tablist" aria-label="Portal">
        <button
          type="button"
          role="tab"
          aria-selected={portal === 'customer'}
          className={`auth__seg-opt${portal === 'customer' ? ' auth__seg-opt--active' : ''}`}
          onClick={() => setPortal('customer')}
        >
          Customer
        </button>
        <button
          type="button"
          role="tab"
          aria-selected={portal === 'admin'}
          className={`auth__seg-opt${portal === 'admin' ? ' auth__seg-opt--active' : ''}`}
          onClick={() => setPortal('admin')}
        >
          Admin
        </button>
      </div>

      <form onSubmit={onSubmit} noValidate>
        <AuthField
          label="Email Address"
          icon={<Mail size={19} strokeWidth={1.8} />}
          type="email"
          placeholder="name@company.com"
          autoComplete="email"
          error={errors.email?.message}
          registration={register('email')}
        />
        <AuthField
          label="Password"
          icon={<Lock size={19} strokeWidth={1.8} />}
          type="password"
          placeholder="Enter your password"
          autoComplete="current-password"
          error={errors.password?.message}
          registration={register('password')}
        />
        <div className="auth__row-right">
          <Link to="/forgot-password">Forgot Password?</Link>
        </div>
        <button type="submit" className="auth__btn" disabled={isSubmitting}>
          {isSubmitting ? 'Signing in…' : 'Sign In'}
        </button>
      </form>

      {portal === 'customer' && (
        <>
          <div className="auth__divider">
            <span>OR</span>
          </div>
          <p className="auth__footer">
            Don&apos;t have an account? <Link to="/register">Create Account</Link>
          </p>
        </>
      )}
    </div>
  );
}
