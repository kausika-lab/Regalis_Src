import type { ReactNode } from 'react';
import { ChevronLeft, ChevronRight } from 'lucide-react';
import type { PagedResult } from '@/models/api';
import { Button } from './Button';
import { EmptyState } from './EmptyState';
import { Spinner } from './Spinner';

export interface Column<T> {
  key: string;
  header: string;
  render: (row: T) => ReactNode;
  width?: string;
}

interface DataTableProps<T> {
  columns: Column<T>[];
  result: PagedResult<T> | null;
  loading: boolean;
  rowKey: (row: T) => string | number;
  onPageChange: (page: number) => void;
  emptyTitle?: string;
  emptyDescription?: string;
}

/**
 * Server-side paged table (API_PLAN §1). Sorting/search/filters live in the
 * page toolbar; this component renders one page and the pager. The table
 * scrolls inside its own container — the page never scrolls horizontally.
 */
export function DataTable<T>({
  columns,
  result,
  loading,
  rowKey,
  onPageChange,
  emptyTitle = 'No records found',
  emptyDescription,
}: DataTableProps<T>) {
  if (loading && !result) {
    return <Spinner center />;
  }
  if (!result || result.items.length === 0) {
    return <EmptyState title={emptyTitle} description={emptyDescription} />;
  }

  return (
    <div>
      <div className="table-scroll">
        <table className="data-table" style={{ opacity: loading ? 0.6 : 1 }}>
          <thead>
            <tr>
              {columns.map((column) => (
                <th key={column.key} style={column.width ? { width: column.width } : undefined}>
                  {column.header}
                </th>
              ))}
            </tr>
          </thead>
          <tbody>
            {result.items.map((row) => (
              <tr key={rowKey(row)}>
                {columns.map((column) => (
                  <td key={column.key}>{column.render(row)}</td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <div className="data-table__pager">
        <span className="data-table__pager-info">
          Page {result.page} of {result.totalPages} · {result.totalCount} records
        </span>
        <div className="data-table__pager-buttons">
          <Button
            variant="outline"
            size="sm"
            disabled={!result.hasPrevious || loading}
            onClick={() => onPageChange(result.page - 1)}
            aria-label="Previous page"
          >
            <ChevronLeft size={16} />
          </Button>
          <Button
            variant="outline"
            size="sm"
            disabled={!result.hasNext || loading}
            onClick={() => onPageChange(result.page + 1)}
            aria-label="Next page"
          >
            <ChevronRight size={16} />
          </Button>
        </div>
      </div>
    </div>
  );
}
