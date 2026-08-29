import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { ArrowLeft, KeyRound, MailCheck } from 'lucide-react';
import { z } from 'zod';
import { authApi, type OtpPurpose } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { AuthField } from '@/components/auth/AuthField';
import { useToast } from '@/components/ui/Toast';

const otpSchema = z.object({
  code: z.string().regex(/^[0-9]{6}$/, 'Enter the 6-digit code.'),
});

interface OtpState {
  destination: string;
  purpose: OtpPurpose;
}

export function VerifyOtpPage() {
  const navigate = useNavigate();
  const location = useLocation();
  const toast = useToast();
  const state = location.state as OtpState | null;
  const [resending, setResending] = useState(false);

  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<z.infer<typeof otpSchema>>({ resolver: zodResolver(otpSchema) });

  if (!state?.destination) {
    return <Navigate to="/login" replace />;
  }

  const onSubmit = handleSubmit(async ({ code }) => {
    try {
      const result = await authApi.verifyOtp(state.destination, state.purpose, code);
      if (state.purpose === 'registration') {
        toast.success('Account verified. Sign in to continue.');
        navigate('/login', { replace: true });
      } else {
        navigate('/reset-password', { replace: true, state: { resetToken: result.resetToken } });
      }
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Verification failed.');
    }
  });

  const resend = async () => {
    setResending(true);
    try {
      await authApi.resendOtp(state.destination, state.purpose);
      toast.success('A new code has been sent.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not resend the code.');
    } finally {
      setResending(false);
    }
  };

  return (
    <div className="auth__panel">
      <Link to="/login" className="auth__back">
        <ArrowLeft size={19} /> Back to Sign In
      </Link>

      <div className="auth__medallion-wrap">
        <div className="auth__medallion">
          <MailCheck size={40} strokeWidth={1.7} color="#c8912f" />
        </div>
        <h1 className="auth__title" style={{ fontSize: 34 }}>
          Check Your Inbox
        </h1>
        <p className="auth__subtitle">
          Enter the 6-digit code sent to <strong>{state.destination}</strong>.
          <br />
          It expires in 10 minutes.
        </p>
      </div>

      <form onSubmit={onSubmit} noValidate>
        <AuthField
          label="Verification Code"
          icon={<KeyRound size={19} strokeWidth={1.8} />}
          placeholder="6-digit code"
          inputMode="numeric"
          maxLength={6}
          autoComplete="one-time-code"
          error={errors.code?.message}
          registration={register('code')}
        />
        <button type="submit" className="auth__btn" disabled={isSubmitting}>
          {isSubmitting ? 'Verifying…' : 'Verify'}
        </button>
        <button
          type="button"
          className="auth__btn auth__btn--ghost"
          style={{ marginTop: 14 }}
          onClick={resend}
          disabled={resending}
        >
          {resending ? 'Sending…' : 'Resend code'}
        </button>
      </form>
    </div>
  );
}
