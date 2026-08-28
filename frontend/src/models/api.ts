/** Mirrors Regalis.Shared.Api.ApiResponse — the envelope on every endpoint. */
export interface ApiError {
  code: string;
  message: string;
  field?: string | null;
}

export interface ApiResponse<T = unknown> {
  success: boolean;
  message: string;
  data: T | null;
  errors: ApiError[];
  correlationId?: string | null;
}

/** Mirrors Regalis.Shared.Api.PagedResult. */
export interface PagedResult<T> {
  items: T[];
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
  hasNext: boolean;
  hasPrevious: boolean;
}

/** The common list-endpoint query contract (API_PLAN §1). */
export interface PagedRequest {
  page?: number;
  pageSize?: number;
  sortBy?: string;
  sortDir?: 'asc' | 'desc';
  search?: string;
}
