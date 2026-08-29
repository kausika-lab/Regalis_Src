import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { BarChart3, Download } from 'lucide-react';
import { reportsApi } from '@/api/reports';
import { locationsApi, workspaceTypesApi } from '@/api/masters';
import { ApiClientError } from '@/api/client';
import { Button, Card, Input, PageHeader, Select, Spinner } from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { Location, WorkspaceType } from '@/models/masters';
import type { ReportFilters, ReportInfo, ReportResult } from '@/models/reports';
import { formatDate, formatDateTime, formatInr } from '@/utils/formatters';

// ── /admin/reports — the landing grid of 16 ─────────────────────────────────

export function AdminReportsPage() {
  const toast = useToast();
  const [catalogue, setCatalogue] = useState<ReportInfo[] | null>(null);

  useEffect(() => {
    reportsApi.catalogue().then(setCatalogue)
      .catch(() => toast.error('Failed to load the report catalogue.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!catalogue) {
    return <Spinner center />;
  }

  return (
    <>
      <PageHeader title="Reports & Analysis"
        description="One shell drives every report: filter, read the summary, export Excel or PDF." />
      <div className="choice-grid">
        {catalogue.map((report) => report.isAvailable ? (
          <Link key={report.key} to={`/admin/reports/${report.key}`}
            className="choice-card" style={{ textDecoration: 'none' }}>
            <div style={{ display: 'flex', gap: 'var(--space-2)', alignItems: 'center' }}>
              <BarChart3 size={18} color="var(--regalis-gold-dark)" />
              <div className="choice-card__title">{report.title}</div>
            </div>
            <div className="choice-card__meta">{report.description}</div>
          </Link>
        ) : (
          <div key={report.key} className="choice-card"
            style={{ cursor: 'default', opacity: 0.5 }}>
            <div className="choice-card__title">{report.title}</div>
            <div className="choice-card__meta">{report.description}</div>
            <div className="choice-card__meta">Arrives with the Day 10 set</div>
          </div>
        ))}
      </div>
    </>
  );
}

// ── /admin/reports/:key — the shared ReportShell ────────────────────────────

function formatCell(type: string, value: unknown): string {
  if (value === null || value === undefined || value === '') return '—';
  switch (type) {
    case 'money': return formatInr(Number(value));
    case 'percent': return `${Number(value).toFixed(1)}%`;
    case 'date': return formatDate(String(value));
    case 'datetime': return formatDateTime(String(value));
    case 'number': return String(value);
    default: return String(value);
  }
}

export function ReportShellPage() {
  const { key } = useParams();
  const toast = useToast();
  const canExport = usePermission('REPORT.EXPORT');

  const today = new Date().toISOString().slice(0, 10);
  const monthAgo = new Date(Date.now() - 29 * 24 * 3600_000).toISOString().slice(0, 10);
  const [from, setFrom] = useState(monthAgo);
  const [to, setTo] = useState(today);
  const [locationId, setLocationId] = useState('');
  const [workspaceTypeId, setWorkspaceTypeId] = useState('');
  const [locations, setLocations] = useState<Location[]>([]);
  const [types, setTypes] = useState<WorkspaceType[]>([]);
  const [report, setReport] = useState<ReportResult | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState<'xlsx' | 'pdf' | null>(null);

  const filters = (): ReportFilters => ({
    from, to,
    locationId: locationId ? Number(locationId) : undefined,
    workspaceTypeId: workspaceTypeId ? Number(workspaceTypeId) : undefined,
  });

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setReport(await reportsApi.get(key!, filters()));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to run the report.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [key, from, to, locationId, workspaceTypeId]);

  useEffect(() => { void load(); }, [load]);
  useEffect(() => {
    locationsApi.list({ page: 1, pageSize: 50 }).then((r) => setLocations(r.items)).catch(() => {});
    workspaceTypesApi.list().then(setTypes).catch(() => {});
  }, []);

  const exportAs = async (format: 'xlsx' | 'pdf') => {
    setExporting(format);
    try {
      await reportsApi.export(key!, format, filters());
      toast.success(`${format.toUpperCase()} downloaded.`);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Export failed.');
    } finally {
      setExporting(null);
    }
  };

  return (
    <>
      <PageHeader title={report?.title ?? 'Report'}
        description={report?.description ?? ''}
        actions={
          <div className="row-actions">
            <Link to="/admin/reports"><Button variant="ghost">All reports</Button></Link>
            {canExport && (
              <>
                <Button variant="outline" loading={exporting === 'xlsx'}
                  onClick={() => exportAs('xlsx')}>
                  <Download size={16} /> Excel
                </Button>
                <Button variant="outline" loading={exporting === 'pdf'}
                  onClick={() => exportAs('pdf')}>
                  <Download size={16} /> PDF
                </Button>
              </>
            )}
          </div>
        } />

      <Card>
        <div className="toolbar">
          <Input label="From" type="date" value={from} onChange={(e) => setFrom(e.target.value)} />
          <Input label="To" type="date" value={to} onChange={(e) => setTo(e.target.value)} />
          <Select label="Location" value={locationId} placeholder="All locations"
            options={locations.map((l) => ({ value: l.locationId, label: l.locationName }))}
            onChange={(e) => setLocationId(e.target.value)} />
          <Select label="Type" value={workspaceTypeId} placeholder="All types"
            options={types.map((t) => ({ value: t.workspaceTypeId, label: t.typeName }))}
            onChange={(e) => setWorkspaceTypeId(e.target.value)} />
        </div>

        {loading && !report ? (
          <Spinner center />
        ) : report && (
          <>
            <div className="table-scroll">
              <table className="data-table" style={{ opacity: loading ? 0.6 : 1 }}>
                <thead>
                  <tr>
                    {report.columns.map((column) => (
                      <th key={column.key} style={
                        ['money', 'number', 'percent'].includes(column.type)
                          ? { textAlign: 'right' } : undefined}>
                        {column.header}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {report.rows.length === 0 ? (
                    <tr>
                      <td colSpan={report.columns.length}
                        style={{ textAlign: 'center', color: 'var(--text-muted)' }}>
                        Nothing in this window — widen the filters.
                      </td>
                    </tr>
                  ) : report.rows.map((row, index) => (
                    <tr key={index}>
                      {report.columns.map((column) => (
                        <td key={column.key} style={
                          ['money', 'number', 'percent'].includes(column.type)
                            ? { textAlign: 'right' } : undefined}>
                          {formatCell(column.type, row[column.key])}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            {report.truncated && (
              <p style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>
                Showing the first {report.rows.length} of {report.totalRows} rows — narrow the
                filters, or export for the full set.
              </p>
            )}
            <div className="toolbar" style={{ marginTop: 'var(--space-4)', marginBottom: 0, flexWrap: 'wrap' }}>
              {report.summary.map((item) => (
                <div key={item.label} className="choice-card" style={{ cursor: 'default', minWidth: 140 }}>
                  <div className="choice-card__meta">{item.label}</div>
                  <div className="choice-card__title">{item.value}</div>
                </div>
              ))}
            </div>
          </>
        )}
      </Card>
    </>
  );
}
