import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { useLocation, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { Button, Input } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
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
      if (error instanceof ApiClientError) {
        toast.error(error.message);
      } else {
        toast.error('Sign in failed. Please try again.');
      }
    }
  });

  return (
    <form onSubmit={onSubmit} style={{ display: 'flex', flexDirection: 'column', gap: 'var(--space-4)' }}>
      <div style={{ display: 'flex', gap: 'var(--space-2)' }}>
        <Button
          block
          variant={portal === 'customer' ? 'primary' : 'outline'}
          onClick={() => setPortal('customer')}
        >
          Customer
        </Button>
        <Button
          block
          variant={portal === 'admin' ? 'primary' : 'outline'}
          onClick={() => setPortal('admin')}
        >
          Admin
        </Button>
      </div>

      <Input
        label="Email"
        type="email"
        autoComplete="email"
        placeholder="you@example.com"
        required
        error={errors.email?.message}
        {...register('email')}
      />
      <Input
        label="Password"
        type="password"
        autoComplete="current-password"
        placeholder="Your password"
        required
        error={errors.password?.message}
        {...register('password')}
      />

      <Button type="submit" block size="lg" loading={isSubmitting}>
        Sign in
      </Button>
    </form>
  );
}
