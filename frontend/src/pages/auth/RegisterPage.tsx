import { useForm } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { Link, useNavigate } from 'react-router-dom';
import { z } from 'zod';
import { authApi } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { Button, Input } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';

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
      const result = await authApi.register({
        ...values,
        companyName: values.companyName || null,
      });
      toast.success('Verification code sent. Check your inbox.');
      navigate('/verify-otp', {
        state: { destination: result.destination, purpose: 'registration' },
      });
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Registration failed.');
    }
  });

  return (
    <form onSubmit={onSubmit} className="form-stack">
      <Input label="Full name" required autoComplete="name" error={errors.fullName?.message} {...register('fullName')} />
      <Input label="Email" type="email" required autoComplete="email" error={errors.email?.message} {...register('email')} />
      <Input label="Mobile number" required autoComplete="tel" placeholder="10-digit mobile" error={errors.mobileNumber?.message} {...register('mobileNumber')} />
      <Input label="Company name" hint="Optional — individuals are welcome" error={errors.companyName?.message} {...register('companyName')} />
      <Input label="Password" type="password" required autoComplete="new-password" error={errors.password?.message} {...register('password')} />
      <Input label="Confirm password" type="password" required autoComplete="new-password" error={errors.confirmPassword?.message} {...register('confirmPassword')} />
      <Button type="submit" block size="lg" loading={isSubmitting}>
        Create account
      </Button>
      <p style={{ textAlign: 'center', fontSize: 'var(--font-size-sm)' }}>
        Already a member? <Link to="/login">Sign in</Link>
      </p>
    </form>
  );
}
