import { useCallback, useEffect, useRef, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { LifeBuoy, Paperclip, Send } from 'lucide-react';
import { ticketsApi } from '@/api/support';
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
import type { PagedResult } from '@/models/api';
import {
  TICKET_PRIORITY_LABELS,
  TICKET_STATUS_LABELS,
  type TicketCategory,
  type TicketDetail,
  type TicketListItem,
} from '@/models/support';
import { formatDateTime } from '@/utils/formatters';

export const TICKET_STATUS_TONES: Record<number, 'info' | 'warning' | 'success' | 'danger' | 'neutral'> = {
  1: 'info',
  2: 'info',
  3: 'warning',
  4: 'warning',
  5: 'success',
  6: 'neutral',
  7: 'danger',
};

// ── /support and /support/tickets — my tickets ──────────────────────────────

export function MyTicketsPage() {
  const toast = useToast();
  const [page, setPage] = useState(1);
  const [rows, setRows] = useState<PagedResult<TicketListItem> | null>(null);
  const [loading, setLoading] = useState(true);

  const load = useCallback(async () => {
    setLoading(true);
    try {
      setRows(await ticketsApi.my({ page, pageSize: 20 }));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load tickets.');
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [page]);

  useEffect(() => { void load(); }, [load]);

  const columns: Column<TicketListItem>[] = [
    {
      key: 'ticket', header: 'Ticket',
      render: (t) => (
        <div>
          <Link to={`/support/tickets/${t.ticketId}`} style={{ fontWeight: 600 }}>
            {t.ticketNumber}
          </Link>
          <div style={{ color: 'var(--text-muted)', fontSize: 'var(--font-size-xs)' }}>{t.subject}</div>
        </div>
      ),
    },
    { key: 'category', header: 'Category', render: (t) => t.categoryName },
    {
      key: 'priority', header: 'Priority',
      render: (t) => TICKET_PRIORITY_LABELS[t.priority],
    },
    {
      key: 'status', header: 'Status',
      render: (t) => <Badge tone={TICKET_STATUS_TONES[t.status]}>{TICKET_STATUS_LABELS[t.status]}</Badge>,
    },
    { key: 'created', header: 'Raised (IST)', render: (t) => formatDateTime(t.createdDate) },
  ];

  return (
    <>
      <PageHeader title="Support"
        description="Raise an issue and follow the conversation — we usually respond within one business day."
        actions={
          <Link to="/support/raise">
            <Button><LifeBuoy size={16} /> Raise ticket</Button>
          </Link>
        } />
      <Card>
        <DataTable columns={columns} result={rows} loading={loading}
          rowKey={(t) => t.ticketId} onPageChange={setPage}
          emptyTitle="No tickets yet"
          emptyDescription="Something not right? Raise a ticket and we will take it from there." />
      </Card>
    </>
  );
}

// ── /support/raise — the guided three-step flow ─────────────────────────────

export function RaiseTicketPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const [categories, setCategories] = useState<TicketCategory[] | null>(null);
  const [categoryId, setCategoryId] = useState<number | null>(null);
  const [issueTypeId, setIssueTypeId] = useState<number | null>(null);
  const [subject, setSubject] = useState('');
  const [description, setDescription] = useState('');
  const [priority, setPriority] = useState('2');
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    ticketsApi.categories().then(setCategories)
      .catch(() => toast.error('Failed to load categories.'));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (!categories) {
    return <Spinner center />;
  }
  const category = categories.find((c) => c.ticketCategoryId === categoryId) ?? null;
  const step = categoryId === null ? 1 : issueTypeId === null ? 2 : 3;

  const submit = async () => {
    setBusy(true);
    try {
      const ticket = await ticketsApi.create({
        ticketCategoryId: categoryId!,
        ticketIssueTypeId: issueTypeId!,
        subject: subject.trim(),
        description: description.trim(),
        priority: Number(priority),
      });
      toast.success(`Ticket ${ticket.ticketNumber} raised.`);
      navigate(`/support/tickets/${ticket.ticketId}`);
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not raise the ticket.');
    } finally {
      setBusy(false);
    }
  };

  return (
    <>
      <PageHeader title="Raise Ticket"
        description={`Step ${step} of 3 — ${step === 1 ? 'what is it about?' : step === 2 ? 'what kind of issue?' : 'tell us the details'}`} />

      {step === 1 && (
        <div className="choice-grid">
          {categories.map((c) => (
            <button key={c.ticketCategoryId} type="button" className="choice-card"
              onClick={() => setCategoryId(c.ticketCategoryId)}>
              <div className="choice-card__title">{c.categoryName}</div>
              <div className="choice-card__meta">
                {c.issueTypes.length} issue type{c.issueTypes.length > 1 ? 's' : ''}
              </div>
            </button>
          ))}
        </div>
      )}

      {step === 2 && category && (
        <>
          <div className="toolbar">
            <Button variant="ghost" onClick={() => setCategoryId(null)}>← {category.categoryName}</Button>
          </div>
          <div className="choice-grid">
            {category.issueTypes.map((t) => (
              <button key={t.ticketIssueTypeId} type="button" className="choice-card"
                onClick={() => setIssueTypeId(t.ticketIssueTypeId)}>
                <div className="choice-card__title">{t.issueTypeName}</div>
              </button>
            ))}
          </div>
        </>
      )}

      {step === 3 && category && (
        <Card>
          <div className="form-stack">
            <div className="toolbar" style={{ marginBottom: 0 }}>
              <Button variant="ghost" onClick={() => setIssueTypeId(null)}>
                ← {category.categoryName} · {category.issueTypes.find(
                  (t) => t.ticketIssueTypeId === issueTypeId)?.issueTypeName}
              </Button>
            </div>
            <Input label="Subject" required value={subject} maxLength={200}
              onChange={(e) => setSubject(e.target.value)} />
            <Input label="Describe the issue" required value={description}
              hint="What happened, when, and what you expected"
              onChange={(e) => setDescription(e.target.value)} />
            <Select label="Priority" value={priority}
              options={Object.entries(TICKET_PRIORITY_LABELS).map(([value, label]) => ({ value, label }))}
              onChange={(e) => setPriority(e.target.value)} />
            <div>
              <Button onClick={submit} loading={busy}
                disabled={!subject.trim() || !description.trim()}>
                Raise ticket
              </Button>
            </div>
            <p style={{ fontSize: 'var(--font-size-xs)', color: 'var(--text-muted)', margin: 0 }}>
              You can attach photos or a PDF from the ticket page after raising it.
            </p>
          </div>
        </Card>
      )}
    </>
  );
}

// ── /support/tickets/:id — the conversation ─────────────────────────────────

export function TicketDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const [ticket, setTicket] = useState<TicketDetail | null>(null);
  const [reply, setReply] = useState('');
  const [busy, setBusy] = useState(false);
  const fileInput = useRef<HTMLInputElement>(null);

  const load = useCallback(async () => {
    try {
      setTicket(await ticketsApi.get(Number(id)));
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Failed to load the ticket.');
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id]);

  useEffect(() => { void load(); }, [load]);

  if (!ticket) {
    return <Spinner center />;
  }
  const closed = ticket.status === 6;

  const send = async () => {
    setBusy(true);
    try {
      setTicket(await ticketsApi.addMessage(ticket.ticketId, reply.trim()));
      setReply('');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not send the reply.');
    } finally {
      setBusy(false);
    }
  };

  const attach = async (file: File) => {
    setBusy(true);
    try {
      setTicket(await ticketsApi.addAttachment(ticket.ticketId, file));
      toast.success('Attachment added.');
    } catch (error) {
      toast.error(error instanceof ApiClientError ? error.message : 'Could not attach the file.');
    } finally {
      setBusy(false);
      if (fileInput.current) fileInput.current.value = '';
    }
  };

  return (
    <>
      <PageHeader title={`${ticket.ticketNumber} — ${ticket.subject}`}
        description={`${ticket.categoryName} · ${ticket.issueTypeName} · raised ${formatDateTime(ticket.createdDate)} IST`}
        actions={
          <div className="row-actions">
            <Badge tone={TICKET_STATUS_TONES[ticket.status]}>{TICKET_STATUS_LABELS[ticket.status]}</Badge>
            {closed
              ? <Button variant="outline" onClick={async () => setTicket(await ticketsApi.reopen(ticket.ticketId))}>Reopen</Button>
              : <Button variant="ghost" onClick={async () => setTicket(await ticketsApi.close(ticket.ticketId))}>Close ticket</Button>}
          </div>
        } />

      <Card>
        <TicketThread ticket={ticket} onDownload={(attachmentId) => {
          const file = ticket.attachments.find((a) => a.ticketAttachmentId === attachmentId);
          void ticketsApi.downloadAttachment(ticket.ticketId, attachmentId, file?.fileName ?? 'file');
        }} />

        {!closed && (
          <div className="toolbar" style={{ marginTop: 'var(--space-4)', marginBottom: 0 }}>
            <Input label="Reply" value={reply} placeholder="Write a reply…"
              onChange={(e) => setReply(e.target.value)} />
            <Button onClick={send} loading={busy} disabled={!reply.trim()}>
              <Send size={16} /> Send
            </Button>
            <Button variant="outline" disabled={busy || ticket.attachments.length >= 5}
              onClick={() => fileInput.current?.click()}>
              <Paperclip size={16} /> Attach
            </Button>
            <input ref={fileInput} type="file" hidden accept=".png,.jpg,.jpeg,.webp,.pdf"
              onChange={(e) => e.target.files?.[0] && void attach(e.target.files[0])} />
          </div>
        )}
      </Card>
    </>
  );
}

/** The conversation thread — shared by the customer page and admin triage. */
export function TicketThread({ ticket, onDownload }: {
  ticket: TicketDetail;
  onDownload: (attachmentId: number) => void;
}) {
  return (
    <div className="form-stack">
      <div className="choice-card" style={{ cursor: 'default' }}>
        <div className="choice-card__meta">
          {ticket.customerName} · {formatDateTime(ticket.createdDate)} IST
        </div>
        <div style={{ whiteSpace: 'pre-wrap' }}>{ticket.description}</div>
      </div>

      {ticket.messages.map((m) => (
        <div key={m.ticketMessageId} className="choice-card" style={{
          cursor: 'default',
          marginLeft: m.senderType === 2 ? 'var(--space-6)' : 0,
          marginRight: m.senderType === 1 ? 'var(--space-6)' : 0,
          borderColor: m.isInternalNote ? 'var(--color-warning)' : undefined,
        }}>
          <div className="choice-card__meta">
            {m.senderName}{m.senderType === 2 ? ' · Support' : ''}
            {m.isInternalNote && <> · <Badge tone="warning">Internal note</Badge></>}
            {' · '}{formatDateTime(m.sentUtc)} IST
          </div>
          <div style={{ whiteSpace: 'pre-wrap' }}>{m.message}</div>
        </div>
      ))}

      {ticket.attachments.length > 0 && (
        <div>
          <div style={{ fontSize: 'var(--font-size-xs)', color: 'var(--text-muted)', marginBottom: 'var(--space-2)' }}>
            Attachments
          </div>
          <div className="row-actions" style={{ flexWrap: 'wrap' }}>
            {ticket.attachments.map((a) => (
              <Button key={a.ticketAttachmentId} size="sm" variant="outline"
                onClick={() => onDownload(a.ticketAttachmentId)}>
                <Paperclip size={14} /> {a.fileName} ({Math.ceil(a.fileSizeBytes / 1024)} KB)
              </Button>
            ))}
          </div>
        </div>
      )}
    </div>
  );
}
