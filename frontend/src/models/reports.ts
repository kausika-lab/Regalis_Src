/** Day 9 — the report engine (M24): one tabular contract for all 16 reports. */

export interface ReportInfo {
  key: string;
  title: string;
  description: string;
  isAvailable: boolean;
}

/** type: string | number | money | percent | date | datetime — drives rendering. */
export interface ReportColumn {
  key: string;
  header: string;
  type: string;
}

export interface ReportSummaryItem {
  label: string;
  value: string;
}

export interface ReportResult {
  key: string;
  title: string;
  description: string;
  from: string;
  to: string;
  columns: ReportColumn[];
  rows: Record<string, unknown>[];
  summary: ReportSummaryItem[];
  totalRows: number;
  truncated: boolean;
}

/** The common filter set (API_PLAN §12); a report ignores what does not apply. */
export interface ReportFilters {
  from?: string;
  to?: string;
  locationId?: number;
  workspaceTypeId?: number;
  workspaceId?: number;
  customerId?: number;
  bookingStatus?: number;
  paymentStatus?: number;
}
