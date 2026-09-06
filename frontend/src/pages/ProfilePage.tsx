import { useEffect, useRef, useState } from 'react';
import { UserRound } from 'lucide-react';
import { authApi, profileApi, type Profile } from '@/api/auth';
import { ApiClientError } from '@/api/client';
import { Button, Card, Input, PageHeader, Spinner } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useAuthStore } from '@/store/authStore';
import { formatDate } from '@/utils/formatters';

/** /profile and /admin/profile — the caller's own record (matrix §6). Email is the
 * login and stays read-only; invoices are unaffected (they snapshot at issue). */
export function ProfilePage() {
  const toast = useToast();
  const updateUser = useAuthStore((state) => state.updateUser);
  const [profile, setProfile] = useState<Profile | null>(null);
  const [photoUrl, setPhotoUrl] = useState<string | null>(null);
  const [form, setForm] = useState({ fullName: '', mobileNumber: '', companyName: '', address: '' });
  const [busy, setBusy] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const applyProfile = (p: Profile) => {
    setProfile(p);
    updateUser({ fullName: p.fullName, email: p.email, userType: p.userType });
    setForm({
      fullName: p.fullName,
      mobileNumber: p.mobileNumber,
      companyName: p.companyName ?? '',
      address: p.address ?? '',
    });
  };

  useEffect(() => {
    profileApi.get().then((p) => {
      applyProfile(p);
      if (p.hasPhoto) void profileApi.photoUrl().then(setPhotoUrl);
    }).catch(() => toast.error('Failed to load your profile.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!profile) {
    return <Spinner center />;
  }

  const save = async () => {
    setBusy(true);
    try {
      applyProfile(await profileApi.update({
        fullName: form.fullName.trim(),
        mobileNumber: form.mobileNumber.trim(),
        companyName: form.companyName.trim() || null,
        address: form.address.trim() || null,
      }));
      toast.success('Profile updated.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not save your profile.');
    } finally {
      setBusy(false);
    }
  };

  const uploadPhoto = async (file: File) => {
    setBusy(true);
    try {
      applyProfile(await profileApi.setPhoto(file));
      setPhotoUrl(await profileApi.photoUrl());
      toast.success('Photo updated.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not upload the photo.');
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  return (
    <>
      <PageHeader title="Profile"
        description={`Member since ${formatDate(profile.createdDate)} · issued invoices keep their original details.`} />

      <div style={{ display: 'grid', gap: 'var(--space-4)' }}>
        <Card title="Your details">
          <div style={{ display: 'flex', gap: 'var(--space-5)', flexWrap: 'wrap', alignItems: 'flex-start' }}>
            <div style={{ textAlign: 'center' }}>
              <div style={{
                width: 96, height: 96, borderRadius: '50%', overflow: 'hidden',
                background: 'var(--surface-inset)', display: 'flex',
                alignItems: 'center', justifyContent: 'center', margin: '0 auto',
              }}>
                {photoUrl
                  ? <img src={photoUrl} alt="Profile" style={{ width: '100%', height: '100%', objectFit: 'cover' }} />
                  : <UserRound size={42} color="var(--text-muted)" />}
              </div>
              <div style={{ marginTop: 'var(--space-2)' }}>
                <Button size="sm" variant="outline" disabled={busy}
                  onClick={() => fileInput.current?.click()}>
                  Change photo
                </Button>
                <input ref={fileInput} type="file" hidden accept=".png,.jpg,.jpeg,.webp"
                  onChange={(e) => e.target.files?.[0] && void uploadPhoto(e.target.files[0])} />
              </div>
            </div>

            <div className="form-stack" style={{ flex: 1, minWidth: 260 }}>
              <div className="form-grid">
                <Input label="Full name" required value={form.fullName}
                  onChange={(e) => setForm({ ...form, fullName: e.target.value })} />
                <Input label="Email" value={profile.email} disabled
                  hint="Your login — contact support to change it" />
                <Input label="Mobile number" required value={form.mobileNumber}
                  onChange={(e) => setForm({ ...form, mobileNumber: e.target.value })} />
                <Input label="Company" value={form.companyName}
                  onChange={(e) => setForm({ ...form, companyName: e.target.value })} />
              </div>
              <Input label="Address" value={form.address}
                onChange={(e) => setForm({ ...form, address: e.target.value })} />
              <div>
                <Button onClick={save} loading={busy}
                  disabled={!form.fullName.trim() || !form.mobileNumber.trim()}>
                  Save changes
                </Button>
              </div>
            </div>
          </div>
        </Card>

        <ChangePasswordCard />
      </div>
    </>
  );
}

function ChangePasswordCard() {
  const toast = useToast();
  const [current, setCurrent] = useState('');
  const [next, setNext] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);

  const change = async () => {
    setBusy(true);
    try {
      await authApi.changePassword(current, next, confirm);
      toast.success('Password changed.');
      setCurrent(''); setNext(''); setConfirm('');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not change the password.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <Card title="Change password">
      <div className="form-stack">
        <div className="form-grid">
          <Input label="Current password" type="password" value={current}
            onChange={(e) => setCurrent(e.target.value)} />
          <Input label="New password" type="password" value={next}
            hint="At least 8 characters with a mix of cases, digits or symbols"
            onChange={(e) => setNext(e.target.value)} />
          <Input label="Confirm new password" type="password" value={confirm}
            onChange={(e) => setConfirm(e.target.value)} />
        </div>
        <div>
          <Button variant="outline" onClick={change} loading={busy}
            disabled={!current || !next || next !== confirm}>
            Change password
          </Button>
        </div>
      </div>
    </Card>
  );
}
