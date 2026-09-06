import { useCallback, useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import { customersApi } from '@/api/admin';
import { locationsApi } from '@/api/masters';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  DataTable,
  Input,
  PageHeader,
  Select,
  Spinner,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { useDebounce } from '@/hooks/useDebounce';
import type { UserDetail, UserListItem } from '@/models/admin';
import { USER_STATUS_LABELS } from '@/models/admin';
import type { PagedResult } from '@/models/api';
import { formatDate, formatDateTime } from '@/utils/formatters';

/**
 * The customer register (MODULE_PLAN §3). Backed by api/customers, the read-only
 * window managers, finance and support hold without USER.VIEW — user
 * administration itself stays on /admin/users behind USER.VIEW.
 */

const statusTone: Record<number, 'success' | 'neutral' | 'danger' | 'warning'> = {
  1: 'success',
  2: 'neutral',
  3: 'danger',
  4: 'warning',
};

export function AdminCustomersPage() {
  const toast = useToast();
  const navigate = useNavigate();

  const [result, setResult] = useState<PagedResult<UserListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [status, setStatus] = useState('');
  const debouncedSearch = useDebounce(search, 350);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setResult(
        await customersApi.list({
          page,
          pageSize: 20,
          search: debouncedSearch || undefined,
          status: status ? Number(status) : undefined,
        }),
      );
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load customers.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, debouncedSearch, status]);

  useEffect(() => {
    void load();
  }, [load]);

  const columns: Column<UserListItem>[] = [
    {
      key: 'name',
      header: 'Customer',
      render: (c) => (
        <div>
          <div style={{ fontWeight: 600 }}>{c.fullName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{c.email}</div>
        </div>
      ),
    },
    { key: 'mobile', header: 'Mobile', render: (c) => c.mobileNumber },
    {
      key: 'company',
      header: 'Company',
      render: (c) => c.companyName || <span style={{ color: 'var(--text-muted)' }}>—</span>,
    },
    {
      key: 'status',
      header: 'Status',
      render: (c) => <Badge tone={statusTone[c.status] ?? 'neutral'}>{USER_STATUS_LABELS[c.status]}</Badge>,
    },
    { key: 'joined', header: 'Member since', render: (c) => formatDate(c.createdDate) },
    {
      key: 'lastLogin',
      header: 'Last sign-in',
      render: (c) => (c.lastLoginUtc ? formatDateTime(c.lastLoginUtc) : '—'),
    },
    {
      key: 'actions',
      header: '',
      width: '96px',
      render: (c) => (
        <div className="row-actions">
          <Button size="sm" variant="outline" onClick={() => navigate(`/admin/customers/${c.userId}`)}>
            View
          </Button>
        </div>
      ),
    },
  ];

  return (
    <>
      <PageHeader
        title="Customers"
        description="Every member of the space — contact details, company and account standing."
      />
      <Card>
        <div className="toolbar">
          <Input label="Search" placeholder="Name, email, mobile…" value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }} />
          <Select label="Status" value={status} placeholder="All statuses"
            options={Object.entries(USER_STATUS_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setStatus(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={result} loading={loading}
          rowKey={(c) => c.userId} onPageChange={setPage}
          emptyTitle="No customers match the filters"
          emptyDescription="Registrations appear here as soon as members verify their account." />
      </Card>
    </>
  );
}

export function AdminCustomerDetailPage() {
  const { id } = useParams<{ id: string }>();
  const toast = useToast();
  const navigate = useNavigate();

  const [customer, setCustomer] = useState<UserDetail | null>(null);
  const [locationName, setLocationName] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setCustomer(await customersApi.get(Number(id)));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the customer.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => {
    void load();
  }, [load]);

  // The detail payload carries the location id only; the name is a cosmetic lookup.
  useEffect(() => {
    if (!customer?.defaultLocationId) {
      setLocationName(null);
      return;
    }
    locationsApi
      .get(customer.defaultLocationId)
      .then((location) => setLocationName(location.locationName))
      .catch(() => setLocationName(null));
  }, [customer?.defaultLocationId]);

  if (loading) {
    return <Spinner center />;
  }
  if (!customer) {
    return null;
  }

  return (
    <>
      <PageHeader
        title={customer.fullName}
        description={customer.companyName ? `${customer.email} · ${customer.companyName}` : customer.email}
        actions={
          <Button variant="ghost" onClick={() => navigate('/admin/customers')}>
            Back
          </Button>
        }
      />

      <Card title="Account">
        <div className="toolbar">
          <Detail label="Status">
            <Badge tone={statusTone[customer.status] ?? 'neutral'}>
              {USER_STATUS_LABELS[customer.status] ?? 'Unknown'}
            </Badge>
          </Detail>
          <Detail label="Member since">{formatDate(customer.createdDate)}</Detail>
          <Detail label="Last sign-in">
            {customer.lastLoginUtc ? formatDateTime(customer.lastLoginUtc) : 'Never'}
          </Detail>
          <Detail label="Roles">{customer.roles.length > 0 ? customer.roles.join(', ') : '—'}</Detail>
          <Detail label="Home location">{locationName ?? '—'}</Detail>
        </div>
        {customer.lockoutEndUtc && (
          <div className="form-error" role="alert">
            Locked out until {formatDateTime(customer.lockoutEndUtc)}.
          </div>
        )}
      </Card>

      <Card title="Contact">
        <div className="toolbar">
          <Detail label="Email">
            {customer.email}{' '}
            <Badge tone={customer.isEmailVerified ? 'success' : 'warning'}>
              {customer.isEmailVerified ? 'Verified' : 'Unverified'}
            </Badge>
          </Detail>
          <Detail label="Mobile">
            {customer.mobileNumber}{' '}
            <Badge tone={customer.isMobileVerified ? 'success' : 'warning'}>
              {customer.isMobileVerified ? 'Verified' : 'Unverified'}
            </Badge>
          </Detail>
          <Detail label="Company">{customer.companyName || '—'}</Detail>
          <Detail label="Address">{customer.address || '—'}</Detail>
        </div>
      </Card>
    </>
  );
}

function Detail({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{label}</div>
      <div>{children}</div>
    </div>
  );
}
