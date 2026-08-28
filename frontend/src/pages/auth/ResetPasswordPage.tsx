import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Navigate, useLocation, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { Button, Input } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';

const schema = z
  .object({
    newPassword: z
      .string()
      .min(8, 'At least 8 characters.')
      .regex(/[A-Z]/, 'Add an uppercase letter.')
      .regex(/[a-z]/, 'Add a lowercase letter.')
      .regex(/[0-9]/, 'Add a digit.')
      .regex(/[^A-Za-z0-9]/, 'Add a symbol.'),
    confirmPassword: z.string(),
  })
  .refine((data) => data.newPassword === data.confirmPassword, {
    message: 'Passwords do not match.',
    path: ['confirmPassword'],
  });

export function ResetPasswordPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const resetToken = (location.state as { resetToken?: string } | null)?.resetToken;

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof schema>>({ resolver: zodResolver(schema) });

  if (!resetToken) {
    return <Navigate to="/forgot-password" replace />;
  }

  const onSubmit = handleSubmit(async ({ newPassword, confirmPassword }) => {
    try {
      await authApi.resetPassword(resetToken, newPassword, confirmPassword);
      toast.success('Password reset. Sign in with your new password.');
      navigate('/login', { replace: true });
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Reset failed. Request a new code.');
    }
  });

  return (
    <form onSubmit={onSubmit} className="form-stack">
      <Input label="New password" type="password" required autoComplete="new-password" error={errors.newPassword?.message} {...register('newPassword')} />
      <Input label="Confirm password" type="password" required autoComplete="new-password" error={errors.confirmPassword?.message} {...register('confirmPassword')} />
      <Button type="submit" block size="lg" loading={isSubmitting}>
        Reset password
      </Button>
    </form>
  );
}
