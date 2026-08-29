import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { ArrowLeft, Lock, Mail, ShieldCheck } from 'lucide-react';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { AuthField } from '@/components/auth/AuthField';
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
    <div className="auth__panel">
      <Link to="/login" className="auth__back">
        <ArrowLeft size={19} /> Back to Sign In
      </Link>

      <div className="auth__medallion-wrap">
        <div className="auth__medallion">
          <Mail size={40} strokeWidth={1.7} color="#c8912f" />
          <span className="auth__medallion-badge">
            <Lock size={13} strokeWidth={2.4} />
          </span>
        </div>
        <h1 className="auth__title" style={{ fontSize: 34 }}>
          Forgot Password?
        </h1>
        <p className="auth__subtitle">
          No worries! Enter your email address and we&apos;ll
          <br />
          send you a code to reset your password.
        </p>
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
        <button type="submit" className="auth__btn" disabled={isSubmitting}>
          {isSubmitting ? 'Sending…' : 'Send Reset Code'}
        </button>
      </form>

      <div className="auth__info">
        <ShieldCheck size={22} strokeWidth={1.9} />
        <div>
          <div className="auth__info-title">Secure &amp; Private</div>
          <div className="auth__info-text">We&apos;ll never share your information with anyone.</div>
        </div>
      </div>
    </div>
  );
}
