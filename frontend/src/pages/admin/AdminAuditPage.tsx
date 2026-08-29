import { useCallback, useEffect, useState } from 'react';
import { Download } from 'lucide-react';
import { auditApi, type AuditLogRow } from '@/api/admin';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  DataTable,
  Input,
  PageHeader,
  Select,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import { formatDateTime } from '@/utils/formatters';

/** /admin/audit — read-only over the append-only TRN_AuditLog. */
export function AdminAuditPage() {
  const toast = useToast();
  const canExport = usePermission('AUDIT.EXPORT');
  const [page, setPage] = useState(1);
  const [module, setModule] = useState('');
  const [from, setFrom] = useState('');
  const [to, setTo] = useState('');
  const [modules, setModules] = useState<string[]>([]);
  const [rows, setRows] = useState<PagedResult<AuditLogRow> | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await auditApi.list({
        page, pageSize: 20,
        module: module || undefined,
        from: from || undefined,
        to: to || undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the audit trail.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, module, from, to]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    auditApi.modules().then(setModules).catch(() => {});
  }, []);

  const columns: Column<AuditLogRow>[] = [
    { key: 'when', header: 'When (IST)', render: (l) => formatDateTime(l.timestampUtc) },
    { key: 'who', header: 'User', render: (l) => l.userName ?? (l.userId ? `#${l.userId}` : 'system') },
    {
      key: 'action', header: 'Action',
      render: (l) => (
        <Badge tone={l.action.includes('DELETE') ? 'danger'
          : l.action.includes('OVERRIDE') ? 'warning' : 'neutral'}>
          {l.action}
        </Badge>
      ),
    },
    { key: 'module', header: 'Module', render: (l) => l.moduleName },
    {
      key: 'entity', header: 'Entity',
      render: (l) => l.entityName ? `${l.entityName}${l.entityId ? ` #${l.entityId}` : ''}` : '—',
    },
    {
      key: 'change', header: 'Change',
      render: (l) => (
        <span style={{
          fontSize: 'var(--font-size-xs)', color: 'var(--text-muted)',
          display: 'inline-block', maxWidth: 320, overflow: 'hidden',
          textOverflow: 'ellipsis', whiteSpace: 'nowrap',
        }}>
          {l.newValue ?? l.oldValue ?? '—'}
        </span>
      ),
    },
    { key: 'ip', header: 'IP', render: (l) => l.ipAddress ?? '—' },
  ];

  return (
    <>
      <PageHeader title="Audit Trail"
        description="Append-only: who did what, when, from where — nothing here can be edited or deleted."
        actions={canExport && (
          <Button variant="outline" loading={exporting} onClick={async () => {
            setExporting(true);
            try {
              await auditApi.export({
                module: module || undefined,
                from: from || undefined,
                to: to || undefined,
              });
            } catch {
              toast.error('Export failed.');
            } finally {
              setExporting(false);
            }
          }}>
            <Download size={16} /> Export CSV
          </Button>
        )} />
      <Card>
        <div className="toolbar">
          <Select label="Module" value={module} placeholder="All modules"
            options={modules.map((m) => ({ value: m, label: m }))}
            onChange={(e) => { setModule(e.target.value); setPage(1); }} />
          <Input label="From" type="date" value={from}
            onChange={(e) => { setFrom(e.target.value); setPage(1); }} />
          <Input label="To" type="date" value={to}
            onChange={(e) => { setTo(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(l) => l.auditLogId} onPageChange={setPage} emptyTitle="No audit entries" />
      </Card>
    </>
  );
}
