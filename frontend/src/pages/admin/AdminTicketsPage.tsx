import { useCallback, useEffect, useState } from 'react';
import { Link, useParams } from 'react-router-dom';
import { Download, Send } from 'lucide-react';
import { usersApi } from '@/api/admin';
import { ticketsApi } from '@/api/support';
import { ApiClientError } from '@/api/client';
import {
  Badge,
  Button,
  Card,
  Checkbox,
  DataTable,
  Input,
  PageHeader,
  Select,
  Spinner,
  type Column,
} from '@/components/ui';
import { useToast } from '@/components/ui/Toast';
import { usePermission } from '@/hooks/usePermission';
import type { PagedResult } from '@/models/api';
import type { UserListItem } from '@/models/admin';
import {
  TICKET_PRIORITY_LABELS,
  TICKET_STATUS_ADMIN_LABELS,
  type TicketDetail,
  type TicketListItem,
} from '@/models/support';
import { TICKET_STATUS_TONES, TicketThread } from '@/pages/customer/SupportPages';
import { formatDateTime } from '@/utils/formatters';

// ── /admin/tickets — triage grid ────────────────────────────────────────────

export function AdminTicketsPage() {
  const toast = useToast();
  const canExport = usePermission('TICKET.EXPORT');
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState('');
  const [priorityFilter, setPriorityFilter] = useState('');
  const [rows, setRows] = useState<PagedResult<TicketListItem> | null>(null);
  const [loading, setLoading] = useState(true);
  const [exporting, setExporting] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await ticketsApi.list({
        page, pageSize: 20,
        status: statusFilter ? Number(statusFilter) : undefined,
        priority: priorityFilter ? Number(priorityFilter) : undefined,
      }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load tickets.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page, statusFilter, priorityFilter]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<TicketListItem>[] = [
    {
      key: 'ticket', header: 'Ticket',
      render: (t) => (
        <div>
          <Link to={`/admin/tickets/${t.ticketId}`} style={{ fontWeight: 600 }}>
            {t.ticketNumber}
          </Link>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{t.subject}</div>
        </div>
      ),
    },
    { key: 'customer', header: 'Customer', render: (t) => t.customerName },
    {
      key: 'category', header: 'Category',
      render: (t) => (
        <div>
          <div>{t.categoryName}</div>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{t.issueTypeName}</div>
        </div>
      ),
    },
    {
      key: 'priority', header: 'Priority',
      render: (t) => (
        <Badge tone={t.priority >= 4 ? 'danger' : t.priority === 3 ? 'warning' : 'neutral'}>
          {TICKET_PRIORITY_LABELS[t.priority]}
        </Badge>
      ),
    },
    {
      key: 'status', header: 'Status',
      render: (t) => (
        <Badge tone={TICKET_STATUS_TONES[t.status]}>{TICKET_STATUS_ADMIN_LABELS[t.status]}</Badge>
      ),
    },
    { key: 'assignee', header: 'Assignee', render: (t) => t.assignedToName ?? '—' },
    { key: 'raised', header: 'Raised (IST)', render: (t) => formatDateTime(t.createdDate) },
  ];

  return (
    <>
      <PageHeader title="Support / Tickets"
        description="Triage: assign, converse (internal notes stay internal), resolve."
        actions={canExport && (
          <Button variant="outline" loading={exporting} onClick={async () => {
            setExporting(true);
            try {
              await ticketsApi.export({
                status: statusFilter ? Number(statusFilter) : undefined,
                priority: priorityFilter ? Number(priorityFilter) : undefined,
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
          <Select label="Status" value={statusFilter} placeholder="All statuses"
            options={Object.entries(TICKET_STATUS_ADMIN_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setStatusFilter(e.target.value); setPage(1); }} />
          <Select label="Priority" value={priorityFilter} placeholder="All priorities"
            options={Object.entries(TICKET_PRIORITY_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) => { setPriorityFilter(e.target.value); setPage(1); }} />
        </div>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(t) => t.ticketId} onPageChange={setPage} emptyTitle="No tickets" />
      </Card>
    </>
  );
}

// ── /admin/tickets/:id — triage detail ──────────────────────────────────────

export function AdminTicketDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [admins, setAdmins] = useState<UserListItem[]>([]);
  const [reply, setReply] = useState('');
  const [internal, setInternal] = useState(false);
  const [busy, setBusy] = useState(false);

  const load = useCallback(async () => {
    try {
      setTicket(await ticketsApi.get(Number(id)));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the ticket.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => {
    void load();
    usersApi.list({ page: 1, pageSize: 100, userType: 2 })
      .then((result) => setAdmins(result.items)).catch(() => {});
  }, [load]);

  if (!ticket) {
    return <Spinner center />;
  }

  const act = async (action: () => Promise<TicketDetail>, message?: string) => {
    setBusy(true);
    try {
      setTicket(await action());
      if (message) toast.success(message);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Action failed.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title={`${ticket.ticketNumber} — ${ticket.subject}`}
        description={`${ticket.customerName} · ${ticket.categoryName} · ${ticket.issueTypeName} · raised ${formatDateTime(ticket.createdDate)} IST${ticket.reopenCount > 0 ? ` · reopened ×${ticket.reopenCount}` : ''}`}
        actions={
          <Badge tone={TICKET_STATUS_TONES[ticket.status]}>
            {TICKET_STATUS_ADMIN_LABELS[ticket.status]}
          </Badge>
        } />

      <Card>
        <div className="toolbar">
          <Select label="Assignee" value={ticket.assignedToUserId ? String(ticket.assignedToUserId) : ''}
            placeholder="Unassigned"
            options={admins.map((a) => ({ value: a.userId, label: a.fullName }))}
            onChange={(e) => e.target.value &&
              void act(() => ticketsApi.assign(ticket.ticketId, Number(e.target.value)), 'Assigned.')} />
          <Select label="Status" value={String(ticket.status)}
            options={Object.entries(TICKET_STATUS_ADMIN_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) =>
              void act(() => ticketsApi.changeStatus(ticket.ticketId, Number(e.target.value)), 'Status updated.')} />
          <Select label="Priority" value={String(ticket.priority)}
            options={Object.entries(TICKET_PRIORITY_LABELS).map(([value, label]) => ({ value, label }))}
            onChange={(e) =>
              void act(() => ticketsApi.changePriority(ticket.ticketId, Number(e.target.value)), 'Priority updated.')} />
        </div>

        <TicketThread ticket={ticket} onDownload={(attachmentId) => {
          const file = ticket.attachments.find((a) => a.ticketAttachmentId === attachmentId);
          void ticketsApi.downloadAttachment(ticket.ticketId, attachmentId, file?.fileName ?? 'file');
        }} />

        {ticket.status !== 6 && (
          <div className="form-stack" style={{ marginTop: 'var(--space-4)' }}>
            <div className="toolbar" style={{ marginBottom: 0 }}>
              <Input label={internal ? 'Internal note (never shown to the customer)' : 'Reply to the customer'}
                value={reply} onChange={(e) => setReply(e.target.value)} />
              <Button onClick={() =>
                void act(async () => {
                  const updated = await ticketsApi.addMessage(ticket.ticketId, reply.trim(), internal);
                  setReply('');
                  return updated;
                })}
                loading={busy} disabled={!reply.trim()}>
                <Send size={16} /> {internal ? 'Add note' : 'Send'}
              </Button>
            </div>
            <Checkbox label="Internal note — visible to the team only" checked={internal}
              onChange={(e) => setInternal(e.target.checked)} />
          </div>
        )}
      </Card>
    </>
  );
}
