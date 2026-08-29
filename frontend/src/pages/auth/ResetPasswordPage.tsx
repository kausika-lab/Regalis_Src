import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft, Lock, ShieldCheck } from 'lucide-react';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { AuthField } from '@/components/auth/AuthField';
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
      toast.error(
        error instanceof ApiClientError ? error.message : 'Reset failed. Request a new code.',
      );
    }
  });

  return (
    <div className="auth__panel">
      <Link to="/login" className="auth__back">
        <ArrowLeft size={19} /> Back to Sign In
      </Link>

      <div className="auth__medallion-wrap">
        <div className="auth__medallion">
          <Lock size={38} strokeWidth={1.7} color="#c8912f" />
        </div>
        <h1 className="auth__title" style={{ fontSize: 34 }}>
          Set a New Password
        </h1>
        <p className="auth__subtitle">Choose a strong password you haven&apos;t used before.</p>
      </div>

      <form onSubmit={onSubmit} noValidate>
        <AuthField
          label="New Password"
          icon={<Lock size={19} strokeWidth={1.8} />}
          type="password"
          placeholder="Create a password"
          autoComplete="new-password"
          error={errors.newPassword?.message}
          registration={register('newPassword')}
        />
        <AuthField
          label="Confirm Password"
          icon={<Lock size={19} strokeWidth={1.8} />}
          type="password"
          placeholder="Re-enter the password"
          autoComplete="new-password"
          error={errors.confirmPassword?.message}
          registration={register('confirmPassword')}
        />
        <button type="submit" className="auth__btn" disabled={isSubmitting}>
          {isSubmitting ? 'Saving…' : 'Reset Password'}
        </button>
      </form>

      <div className="auth__info">
        <ShieldCheck size={22} strokeWidth={1.9} />
        <div>
          <div className="auth__info-title">Signed out everywhere</div>
          <div className="auth__info-text">
            Resetting your password signs you out of all other devices.
          </div>
        </div>
      </div>
    </div>
  );
}
