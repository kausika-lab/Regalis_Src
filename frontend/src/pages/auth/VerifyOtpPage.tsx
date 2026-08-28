import { useState } from 'react';
import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, Navigate, useLocation, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { authApi, type OtpPurpose } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { Button, Input } from '@/components/ui';
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
        navigate('/reset-password', {
          replace: true,
          state: { resetToken: result.resetToken },
        });
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
    <form onSubmit={onSubmit} className="form-stack">
      <p style={{ fontSize: 'var(--font-size-sm)', color: 'var(--text-secondary)' }}>
        Enter the 6-digit code sent to <strong>{state.destination}</strong>. It expires in 10 minutes.
      </p>
      <Input
        label="Verification code"
        required
        inputMode="numeric"
        maxLength={6}
        autoComplete="one-time-code"
        error={errors.code?.message}
        {...register('code')}
      />
      <Button type="submit" block size="lg" loading={isSubmitting}>
        Verify
      </Button>
      <Button variant="ghost" block onClick={resend} loading={resending}>
        Resend code
      </Button>
      <p style={{ textAlign: 'center', fontSize: 'var(--font-size-sm)' }}>
        <Link to="/login">Back to sign in</Link>
      </p>
    </form>
  );
}
