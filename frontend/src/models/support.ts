/** Day 9 — support tickets (M21). */

export interface TicketIssueType {
  ticketIssueTypeId: number;
  issueTypeName: string;
  displayOrder: number;
}

export interface TicketCategory {
  ticketCategoryId: number;
  categoryName: string;
  displayOrder: number;
  issueTypes: TicketIssueType[];
}

export interface TicketListItem {
  ticketId: number;
  ticketNumber: string;
  subject: string;
  categoryName: string;
  issueTypeName: string;
  customerId: number;
  customerName: string;
  priority: number; // 1 low, 2 medium, 3 high, 4 critical
  status: number; // 1 open … 7 reopened
  assignedToUserId: number | null;
  assignedToName: string | null;
  createdDate: string;
  lastMessageUtc: string | null;
  messageCount: number;
}

export interface TicketMessage {
  ticketMessageId: number;
  senderUserId: number;
  senderName: string;
  senderType: number; // 1 customer, 2 admin
  message: string;
  isInternalNote: boolean;
  sentUtc: string;
}

export interface TicketAttachment {
  ticketAttachmentId: number;
  ticketMessageId: number | null;
  fileName: string;
  contentType: string;
  fileSizeBytes: number;
  uploadedByUserId: number;
  uploadedUtc: string;
}

export interface TicketDetail {
  ticketId: number;
  ticketNumber: string;
  customerId: number;
  customerName: string;
  ticketCategoryId: number;
  categoryName: string;
  ticketIssueTypeId: number;
  issueTypeName: string;
  subject: string;
  description: string;
  priority: number;
  status: number;
  assignedToUserId: number | null;
  assignedToName: string | null;
  firstResponseUtc: string | null;
  resolvedUtc: string | null;
  closedUtc: string | null;
  resolutionNotes: string | null;
  reopenCount: number;
  createdDate: string;
  messages: TicketMessage[];
  attachments: TicketAttachment[];
}

export interface CreateTicketPayload {
  ticketCategoryId: number;
  ticketIssueTypeId: number;
  subject: string;
  description: string;
  priority: number;
}

export const TICKET_STATUS_LABELS: Record<number, string> = {
  1: 'Open',
  2: 'Assigned',
  3: 'In progress',
  4: 'Waiting for you',
  5: 'Resolved',
  6: 'Closed',
  7: 'Reopened',
};

export const TICKET_STATUS_ADMIN_LABELS: Record<number, string> = {
  ...TICKET_STATUS_LABELS,
  4: 'Waiting for customer',
};

export const TICKET_PRIORITY_LABELS: Record<number, string> = {
  1: 'Low',
  2: 'Medium',
  3: 'High',
  4: 'Critical',
};
