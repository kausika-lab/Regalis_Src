import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { Button, Input } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';

const schema = z.object({ email: z.email('Enter a valid email address.') });

export function ForgotPasswordPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema) });

  const onSubmit = handleSubmit(async ({ email }) => {
    // Always proceeds — the API never reveals whether the address is registered.
    await authApi.forgotPassword(email);
    toast.success('If the address is registered, a reset code has been sent.');
    navigate('/verify-otp', { state: { destination: email, purpose: 'forgot-password' } });
  });

  return (
    <form onSubmit={onSubmit} className="form-stack">
      <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--text-secondary)' }}>
        Enter your registered email and we&apos;ll send a code to reset your password.
      </p>
      <Input label="Email" type="email" required autoComplete="email" error={errors.email?.message} {...register('email')} />
      <Button type="submit" block size="lg" loading={isSubmitting}>
        Send reset code
      </Button>
      <p style={{ textAlign: 'center', fontSize: 'var(--font-size-sm)' }}>
        <Link to="/login">Back to sign in</Link>
      </p>
    </form>
  );
}
