import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { Building2, Lock, Mail, Phone, UserRound } from 'lucide-react';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { AuthField } from '@/components/auth/AuthField';
import { useToast } from '@/components/ui/Toast';
import { AuthBrand } from '@/layouts/AuthLayout';

const passwordRule = z
  .string()
  .min(8, 'At least 8 characters.')
  .regex(/[A-Z]/, 'Add an uppercase letter.')
  .regex(/[a-z]/, 'Add a lowercase letter.')
  .regex(/[0-9]/, 'Add a digit.')
  .regex(/[^A-Za-z0-9]/, 'Add a symbol.');

const registerSchema = z
  .object({
    fullName: z.string().min(1, 'Your name is required.').max(150),
    email: z.email('Enter a valid email address.'),
    mobileNumber: z.string().regex(/^\+?[0-9]{10,15}$/, 'Enter a valid mobile number (10–15 digits).'),
    password: passwordRule,
    confirmPassword: z.string(),
    companyName: z.string().max(200).optional(),
  })
  .refine((data) => data.password === data.confirmPassword, {
    message: 'Passwords do not match.',
    path: ['confirmPassword'],
  });

type RegisterForm = z.infer<typeof registerSchema>;

export function RegisterPage() {
  const navigate = useNavigate();
  const toast = useToast();
  const {
    register,
    handleSubmit,
    formState: { errors, isSubmitting },
  } = useForm<RegisterForm>({ resolver: zodResolver(registerSchema) });

  const onSubmit = handleSubmit(async (values) => {
    try {
      const result = await authApi.register({ ...values, companyName: values.companyName || null });
      toast.success('Verification code sent. Check your inbox.');
      navigate('/verify-otp', {
        state: { destination: result.destination, purpose: 'registration' },
      });
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Registration failed.');
    }
  });

  return (
    <div className="auth__panel auth__panel--wide">
      <AuthBrand />
      <h1 className="auth__title">Create Account</h1>
      <p className="auth__subtitle">
        Join Regalis WorkSpace. Mandatory fields are marked with{' '}
        <span className="auth__required">*</span>
      </p>

      <form onSubmit={onSubmit} noValidate>
        <div className="auth__grid">
          <AuthField
            label="Full Name"
            icon={<UserRound size={19} strokeWidth={1.9} />}
            placeholder="Enter full name"
            autoComplete="name"
            error={errors.fullName?.message}
            registration={register('fullName')}
          />
          <AuthField
            label="Email Address"
            icon={<Mail size={19} strokeWidth={1.9} />}
            type="email"
            placeholder="Enter email address"
            autoComplete="email"
            error={errors.email?.message}
            registration={register('email')}
          />
          <AuthField
            label="Phone Number"
            icon={<Phone size={19} strokeWidth={1.9} />}
            placeholder="Enter phone number"
            autoComplete="tel"
            error={errors.mobileNumber?.message}
            registration={register('mobileNumber')}
          />
          <AuthField
            label="Company Name"
            icon={<Building2 size={19} strokeWidth={1.9} />}
            placeholder="Enter company name"
            optional
            error={errors.companyName?.message}
            registration={register('companyName')}
          />
          <AuthField
            label="Password"
            icon={<Lock size={19} strokeWidth={1.9} />}
            type="password"
            placeholder="Create a password"
            autoComplete="new-password"
            error={errors.password?.message}
            registration={register('password')}
          />
          <AuthField
            label="Confirm Password"
            icon={<Lock size={19} strokeWidth={1.9} />}
            type="password"
            placeholder="Re-enter the password"
            autoComplete="new-password"
            error={errors.confirmPassword?.message}
            registration={register('confirmPassword')}
          />
        </div>
        <button type="submit" className="auth__btn" disabled={isSubmitting}>
          {isSubmitting ? 'Creating account…' : 'Create Account'}
        </button>
      </form>

      <p className="auth__footer">
        Already a member? <Link to="/login">Sign In</Link>
      </p>
    </div>
  );
}
