/**
 * Admin -> Billing & Finance -> Expenses   (/admin/expenses)
 *
 *   Summary      net total for the filter, entries, vouchers as entered, adjustments
 *   Charts       spend by category (pie) and month-wise spend for a year (columns)
 *   Expenses     the register: add / edit / adjust / delete
 *   Adjustments  history of corrections recorded against expenses: view / edit / delete
 *
 * Every amount is NET of adjustments (Amount + AdjustmentTotal), summed by the API:
 * GET /api/expenses/summary (pie + totals) and /api/expenses/monthly (columns) take the
 * same Search / Category / From / To filters as the list, so all three always agree.
 * An adjustment never edits the voucher, which is why a bank-matched (locked) expense
 * can still be adjusted.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file
 * by name. It replaces AdminExpensesPage-CstB2vPx.js; a fix ships under a NEW name
 * (EX2, EX3 ...) because /assets is cached for a year and this file is not precached.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as e,
  p as Button,
  F as Select,
  I as Input,
  D as unwrap,
  E as http,
  z as locationsApi
} from "./index-DI5X4c9f.js";
import { s as suppliersApi } from "./procurement-D0z-s4Dx.js";
import { b as PAYMENT_METHODS } from "./procurement-CDmpFQ22.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { C as ConfirmDialog } from "./ConfirmDialog-Ct1kLH-J.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { u as useFieldErrors } from "./useFieldErrors-C8qSSg0h.js";
import { b as fmtMoney, c as fmtDate } from "./formatters-DFe5xCRi.js";
import "./EmptyState-B4UbbVj4.js";
import "./chevron-right-BikENMwC.js";

// ── API ─────────────────────────────────────────────────────────────────────

const expensesApi = {
  list: (params) => unwrap(http.get("/expenses", { params })),
  summary: (params) => unwrap(http.get("/expenses/summary", { params })),
  monthly: (params) => unwrap(http.get("/expenses/monthly", { params })),
  categories: () => unwrap(http.get("/expenses/categories")),
  create: (body) => unwrap(http.post("/expenses", body)),
  update: (id, body) => unwrap(http.put(`/expenses/${id}`, body)),
  remove: (id) => http.delete(`/expenses/${id}`),
  exportCsv: async (params) => {
    const response = await http.get("/expenses/export", { params, responseType: "blob" });
    const url = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = url;
    link.download = `expenses_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    URL.revokeObjectURL(url);
  }
};

const adjustmentsApi = {
  list: (params) => unwrap(http.get("/expenses/adjustments", { params })),
  get: (id) => unwrap(http.get(`/expenses/adjustments/${id}`)),
  create: (body) => unwrap(http.post("/expenses/adjustments", body)),
  update: (id, body) => unwrap(http.put(`/expenses/adjustments/${id}`, body)),
  remove: (id) => http.delete(`/expenses/adjustments/${id}`)
};

// ── Helpers ─────────────────────────────────────────────────────────────────

const PAYMENT_OPTIONS = Object.entries(PAYMENT_METHODS).map(([value, label]) => ({
  value: Number(value),
  label
}));

const ADJUSTMENT_TYPES = { 1: "Increase", 2: "Decrease" };
const ADJUSTMENT_TYPE_OPTIONS = [
  { value: 1, label: "Increase — additional charge" },
  { value: 2, label: "Decrease — refund, discount or correction" }
];
const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const MONTH_NAMES = ["January", "February", "March", "April", "May", "June", "July",
  "August", "September", "October", "November", "December"];

// Pie: five categories get their own colour, the rest fold into "Other".
const PIE_SLOTS = 5;

const errorText = (error, fallback) => (error instanceof ApiError ? error.message : fallback);
const today = () => new Date().toISOString().slice(0, 10);
const round2 = (value) => Math.round(value * 100) / 100;
const signedMoney = (value) =>
  value === 0 ? fmtMoney(0) : `${value > 0 ? "+" : "−"}${fmtMoney(Math.abs(value))}`;
const compactMoney = (() => {
  const format = new Intl.NumberFormat("en-IN", { notation: "compact", maximumFractionDigits: 1 });
  return (value) => `₹${format.format(value)}`;
})();
const percent = (part, whole) => (whole > 0 ? (part / whole) * 100 : 0);
const fmtPercent = (value) => `${value >= 10 || value === 0 ? value.toFixed(0) : value.toFixed(1)}%`;

/** Largest "nice" axis maximum (1, 2, 2.5, 5 × 10ⁿ) at or above the data maximum. */
function niceMax(max) {
  if (max <= 0) return 1000;
  const power = 10 ** Math.floor(Math.log10(max));
  for (const step of [1, 2, 2.5, 5, 10]) {
    if (step * power >= max) return step * power;
  }
  return 10 * power;
}

const STYLE_ID = "rg-expenses-ex1";
const STYLES = `
.ex-stats{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:var(--space-4)}
@media(max-width:900px){.ex-stats{grid-template-columns:repeat(2,minmax(0,1fr))}}
.ex-stat__label{color:var(--text-muted);font-size:var(--font-size-xs)}
.ex-stat__value{font-size:var(--font-size-lg);font-weight:600;color:var(--text-primary);font-variant-numeric:tabular-nums}
.ex-stat__sub{color:var(--text-muted);font-size:var(--font-size-xs)}

.ex-charts{display:grid;grid-template-columns:minmax(0,.85fr) minmax(0,1.35fr);gap:var(--space-4);margin-top:var(--space-4)}
@media(max-width:1100px){.ex-charts{grid-template-columns:minmax(0,1fr)}}
.ex-charts>.card{margin:0}
.ex-viz{--ex-s1:#2a78d6;--ex-s2:#eb6834;--ex-s3:#1baf7a;--ex-s4:#eda100;--ex-s5:#e87ba4;--ex-other:#8b8270;--ex-bar:var(--regalis-gold-dark);--ex-grid:var(--border-subtle);position:relative}
[data-theme=dark] .ex-viz{--ex-s1:#3987e5;--ex-s2:#d95926;--ex-s3:#199e70;--ex-s4:#c98500;--ex-s5:#d55181;--ex-other:#8c897c}
.ex-viz__empty{display:grid;place-items:center;min-height:200px;color:var(--text-muted);font-size:var(--font-size-sm);text-align:center}
.ex-viz__tip{position:absolute;z-index:5;pointer-events:none;min-width:150px;padding:8px 10px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);box-shadow:var(--shadow-lg);font-size:var(--font-size-xs);color:var(--text-secondary);transform:translate(-50%,calc(-100% - 10px));white-space:nowrap}
.ex-viz__tip strong{display:block;color:var(--text-primary);font-size:var(--font-size-sm);font-variant-numeric:tabular-nums}
.ex-sub{color:var(--text-muted);font-size:var(--font-size-xs)}

/* The legend sits beside the pie when the card is wide, and under it when it is not. */
.ex-pie-box{container-type:inline-size}
.ex-pie{display:grid;grid-template-columns:180px minmax(0,1fr);gap:var(--space-5);align-items:center}
.ex-pie>*{min-width:0}
@container (max-width:540px){.ex-pie{grid-template-columns:minmax(0,1fr);justify-items:center;gap:var(--space-4)}}
.ex-pie svg{display:block;width:180px;height:180px}
.ex-pie path{transition:opacity var(--transition-fast)}
.ex-pie text{pointer-events:none}
.ex-pie__total{font-variant-numeric:tabular-nums}
.ex-row-actions{flex-wrap:wrap;justify-content:flex-end;max-width:150px;margin-left:auto}
.ex-legend{width:100%;border-collapse:collapse;table-layout:fixed;font-size:var(--font-size-sm)}
.ex-legend col.ex-col-amt{width:118px}
.ex-legend col.ex-col-pct{width:54px}
.ex-legend th{padding:0 6px 6px;text-align:left;font-size:var(--font-size-xs);font-weight:600;color:var(--text-muted)}
.ex-legend td{padding:6px;border-top:1px solid var(--border-subtle);color:var(--text-primary)}
.ex-legend tr[data-clickable]{cursor:pointer}
.ex-legend tbody tr:hover td,.ex-legend tbody tr.is-active td{background:var(--surface-hover)}
.ex-legend .ex-num{text-align:right;font-variant-numeric:tabular-nums;white-space:nowrap}
.ex-legend__name{display:flex;align-items:center;gap:8px;min-width:0}
.ex-legend__name span:last-child{overflow:hidden;text-overflow:ellipsis;white-space:nowrap}
.ex-swatch{flex:none;width:10px;height:10px;border-radius:3px}

.ex-year{height:36px;padding:0 32px 0 12px;width:auto;min-width:96px}
.ex-monthly__head{display:flex;justify-content:space-between;align-items:baseline;gap:var(--space-3);margin-bottom:var(--space-2)}
.ex-monthly__total{font-size:var(--font-size-lg);font-weight:600;color:var(--text-primary);font-variant-numeric:tabular-nums}
.ex-monthly svg{display:block;width:100%;height:280px;overflow:visible}
.ex-monthly text{font-family:inherit;font-variant-numeric:tabular-nums}
.ex-monthly details{margin-top:var(--space-3);font-size:var(--font-size-sm)}
.ex-monthly summary{cursor:pointer;color:var(--text-secondary);font-size:var(--font-size-xs)}
.ex-monthly table{width:100%;margin-top:var(--space-2);border-collapse:collapse}
.ex-monthly td{padding:4px 6px;border-top:1px solid var(--border-subtle)}
.ex-monthly td:last-child{text-align:right;font-variant-numeric:tabular-nums}

.ex-tabs{display:flex;gap:2px;border-bottom:1px solid var(--border-subtle);margin-bottom:var(--space-4)}
.ex-tab{all:unset;cursor:pointer;padding:10px 14px;font-size:var(--font-size-sm);font-weight:600;color:var(--text-muted);border-bottom:2px solid transparent;margin-bottom:-1px}
.ex-tab:hover{color:var(--text-primary)}
.ex-tab:focus-visible{outline:2px solid var(--regalis-gold);outline-offset:2px;border-radius:var(--radius-sm)}
.ex-tab[aria-selected=true]{color:var(--text-primary);border-bottom-color:var(--regalis-gold)}
.ex-tab__count{margin-left:6px;padding:1px 7px;border-radius:var(--radius-full);background:rgb(var(--accent-rgb) / 13%);color:var(--regalis-gold-dark);font-size:var(--font-size-xs)}

/* One aligned filter row: equal 40px controls, labels on one baseline above them. */
.ex-filters{display:grid;grid-template-columns:minmax(0,2fr) repeat(3,minmax(0,1fr));gap:var(--space-4);align-items:end;margin-bottom:var(--space-4)}
@media(max-width:900px){.ex-filters{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.ex-filters{grid-template-columns:minmax(0,1fr)}}
.ex-filters .field{min-width:0;flex:none;gap:6px}
.ex-filters .field__label{line-height:1.25;white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.ex-filters .field__input{box-sizing:border-box;height:40px;min-width:0;width:100%;padding-top:0;padding-bottom:0;line-height:38px}
.ex-filters select.field__input{padding-right:32px}
.ex-chip{display:inline-flex;align-items:center;gap:6px;margin:0 0 var(--space-3);padding:4px 6px 4px 12px;border-radius:var(--radius-full);background:rgb(var(--accent-rgb) / 13%);font-size:var(--font-size-xs);color:var(--text-primary)}
.ex-chip button{all:unset;cursor:pointer;display:grid;place-items:center;width:20px;height:20px;border-radius:50%;color:var(--text-secondary)}
.ex-chip button:hover,.ex-chip button:focus-visible{background:var(--surface-hover)}

.ex-link{all:unset;white-space:nowrap;cursor:pointer;color:var(--regalis-gold-dark);font-size:var(--font-size-xs);font-weight:600}
.ex-link:hover,.ex-link:focus-visible{text-decoration:underline}
.ex-plus{color:var(--color-danger)}
.ex-minus{color:var(--color-success)}
.ex-preview{padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 7%)}
.ex-preview--bad{background:rgb(220 38 38 / 8%)}
`;

function useStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }, []);
}

function useDebounced(value, delay = 300) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(timer);
  }, [value, delay]);
  return debounced;
}

function useWidth(ref) {
  const [width, setWidth] = React.useState(0);
  React.useEffect(() => {
    if (!ref.current) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(ref.current);
    return () => observer.disconnect();
  }, [ref]);
  return width;
}

const Muted = ({ children }) => e.jsx("div", { className: "ex-sub", children });

// ── Page ────────────────────────────────────────────────────────────────────

function AdminExpensesPage() {
  useStyles();
  const toast = useToast();
  const canCreate = usePermission("EXPENSE.CREATE");
  const canEdit = usePermission("EXPENSE.EDIT");
  const canDelete = usePermission("EXPENSE.DELETE");
  const canExport = usePermission("EXPENSE.EXPORT");

  const [tab, setTab] = React.useState("expenses");
  const [version, setVersion] = React.useState(0);
  const refresh = React.useCallback(() => setVersion((v) => v + 1), []);

  // Filters shared by the register, the summary and both charts.
  const [search, setSearch] = React.useState("");
  const debouncedSearch = useDebounced(search);
  const [category, setCategory] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const filters = {
    search: debouncedSearch || undefined,
    category: category || undefined,
    from: from || undefined,
    to: to || undefined
  };

  const [year, setYear] = React.useState(() => new Date().getFullYear());
  // The monthly chart follows the date filter's year.
  React.useEffect(() => {
    const source = from || to;
    if (source) setYear(Number(source.slice(0, 4)));
  }, [from, to]);

  const [result, setResult] = React.useState(null);
  const [summary, setSummary] = React.useState(null);
  const [monthly, setMonthly] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [categories, setCategories] = React.useState([]);

  const [editing, setEditing] = React.useState(null); // expense | "new" | null
  const [deleting, setDeleting] = React.useState(null);
  const [adjusting, setAdjusting] = React.useState(null); // { expense?, adjustment? } | null
  const [historyFor, setHistoryFor] = React.useState(null); // expense filter for the history tab

  const filterKey = JSON.stringify(filters);

  React.useEffect(() => {
    let live = true;
    setLoading(true);
    Promise.all([
      expensesApi.list({ ...filters, page, pageSize: 20 }),
      expensesApi.summary(filters)
    ])
      .then(([list, totals]) => {
        if (!live) return;
        setResult(list);
        setSummary(totals);
      })
      .catch((error) => live && toast.error(errorText(error, "Failed to load expenses.")))
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
  }, [filterKey, page, version]);

  React.useEffect(() => {
    let live = true;
    expensesApi
      .monthly({ ...filters, year })
      .then((data) => live && setMonthly(data))
      .catch(() => live && setMonthly(null));
    return () => {
      live = false;
    };
  }, [filterKey, year, version]);

  React.useEffect(() => {
    expensesApi
      .categories()
      .then((list) => setCategories(list.map((c) => ({ value: c, label: c }))))
      .catch(() => setCategories([]));
  }, [version]);

  const onFilter = (setter) => (event) => {
    setPage(1);
    setter(event.target.value);
  };

  const openHistory = (expense) => {
    setHistoryFor(expense);
    setTab("adjustments");
  };

  const columns = [
    {
      key: "voucher",
      header: "Voucher",
      render: (x) =>
        e.jsxs("div", {
          children: [
            e.jsx("div", { style: { fontWeight: 600 }, children: x.expenseNumber }),
            e.jsx(Muted, { children: fmtDate(x.expenseDate) })
          ]
        })
    },
    { key: "category", header: "Category", render: (x) => x.category },
    {
      key: "description",
      header: "Description",
      render: (x) =>
        e.jsxs("div", { children: [e.jsx("div", { children: x.description }), e.jsx(Muted, { children: x.remarks })] })
    },
    {
      key: "amount",
      header: "Amount",
      render: (x) =>
        e.jsxs("div", {
          children: [
            e.jsx("strong", { children: fmtMoney(x.netAmount) }),
            x.adjustmentCount > 0 &&
              e.jsx("div", {
                children: e.jsx("button", {
                  type: "button",
                  className: "ex-link",
                  title: `Voucher ${fmtMoney(x.amount)} · ${x.adjustmentCount} adjustment(s) — view history`,
                  onClick: () => openHistory(x),
                  children: `${signedMoney(x.adjustmentTotal)} adj.`
                })
              })
          ]
        })
    },
    { key: "mode", header: "Paid by", render: (x) => PAYMENT_METHODS[x.paymentMethod] ?? "—" },
    { key: "enteredBy", header: "Entered by", render: (x) => x.enteredByName },
    {
      key: "reconciled",
      header: "Bank",
      render: (x) =>
        x.isReconciled
          ? e.jsx(Badge, { tone: "success", children: "Matched" })
          : e.jsx(Badge, { tone: "neutral", children: "—" })
    },
    {
      key: "actions",
      header: "",
      render: (x) =>
        e.jsxs("div", {
          className: "row-actions ex-row-actions",
          children: [
            canEdit && !x.isReconciled &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(x), children: "Edit" }),
            canEdit &&
              e.jsx(Button, { size: "sm", variant: "outline", onClick: () => setAdjusting({ expense: x }), children: "Adjust" }),
            canDelete && !x.isReconciled &&
              e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setDeleting(x), children: "Delete" })
          ]
        })
    }
  ];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Expenses",
        description:
          "Every daily company expense. A bank-matched expense is locked — adjust it, or unmatch it first.",
        actions: e.jsxs(e.Fragment, {
          children: [
            canExport &&
              e.jsx(Button, { variant: "outline", onClick: () => expensesApi.exportCsv(filters), children: "Export CSV" }),
            canEdit &&
              e.jsx(Button, { variant: "outline", onClick: () => setAdjusting({}), children: "Add adjustment" }),
            canCreate && e.jsx(Button, { onClick: () => setEditing("new"), children: "Add expense" })
          ]
        })
      }),

      summary &&
        e.jsx(Card, {
          children: e.jsxs("div", {
            className: "ex-stats",
            children: [
              e.jsx(Stat, { label: "Total for this filter", value: fmtMoney(summary.totalAmount), sub: "Net of adjustments" }),
              e.jsx(Stat, { label: "Entries", value: summary.totalCount }),
              e.jsx(Stat, { label: "Vouchers as entered", value: fmtMoney(summary.grossAmount) }),
              e.jsx(Stat, { label: "Adjustments", value: signedMoney(summary.adjustmentTotal), sub: "Increases minus decreases" })
            ]
          })
        }),

      e.jsxs("div", {
        className: "ex-charts",
        children: [
          e.jsx(Card, {
            title: "Expense by category",
            children: e.jsx("div", { className: "ex-pie-box", children: e.jsx(CategoryPie, {
              summary,
              selected: category,
              onSelect: (value) => {
                setPage(1);
                setCategory(value);
              }
            }) })
          }),
          e.jsx(Card, {
            title: "Monthly expense",
            actions: e.jsx(YearSelect, { year, onChange: setYear }),
            children: e.jsx(MonthlyChart, { monthly })
          })
        ]
      }),

      e.jsxs(Card, {
        children: [
          e.jsxs("div", {
            className: "ex-tabs",
            role: "tablist",
            "aria-label": "Expense views",
            children: [
              e.jsx("button", {
                type: "button",
                role: "tab",
                className: "ex-tab",
                "aria-selected": tab === "expenses",
                onClick: () => setTab("expenses"),
                children: "Expenses"
              }),
              e.jsx("button", {
                type: "button",
                role: "tab",
                className: "ex-tab",
                "aria-selected": tab === "adjustments",
                onClick: () => setTab("adjustments"),
                children: "Adjustment history"
              })
            ]
          }),

          tab === "expenses"
            ? e.jsxs(e.Fragment, {
                children: [
                  e.jsxs("div", {
                    className: "ex-filters",
                    children: [
                      e.jsx(Input, { label: "Search", placeholder: "Voucher, description or remarks", value: search, onChange: onFilter(setSearch) }),
                      e.jsx(Select, { label: "Category", value: category, placeholder: "All categories", options: categories, onChange: onFilter(setCategory) }),
                      e.jsx(Input, { label: "From", type: "date", value: from, max: to || undefined, onChange: onFilter(setFrom) }),
                      e.jsx(Input, { label: "To", type: "date", value: to, min: from || undefined, onChange: onFilter(setTo) })
                    ]
                  }),
                  e.jsx(DataTable, {
                    columns,
                    result,
                    loading,
                    rowKey: (x) => x.expenseId,
                    onPageChange: setPage,
                    emptyTitle: "No expenses recorded",
                    emptyDescription: "Record daily company expenses here — they feed the expense report."
                  })
                ]
              })
            : e.jsx(AdjustmentHistory, {
                version,
                expense: historyFor,
                onClearExpense: () => setHistoryFor(null),
                canEdit,
                canDelete,
                onEdit: (adjustment) => setAdjusting({ adjustment }),
                onChanged: refresh
              })
        ]
      }),

      editing &&
        e.jsx(ExpenseForm, {
          expense: editing === "new" ? null : editing,
          onClose: () => setEditing(null),
          onSaved: () => {
            setEditing(null);
            refresh();
          }
        }),

      adjusting &&
        e.jsx(AdjustmentForm, {
          expense: adjusting.expense ?? null,
          adjustment: adjusting.adjustment ?? null,
          onClose: () => setAdjusting(null),
          onSaved: () => {
            setAdjusting(null);
            refresh();
          }
        }),

      e.jsx(ConfirmDialog, {
        open: deleting !== null,
        danger: true,
        confirmLabel: "Delete",
        title: `Delete ${deleting?.expenseNumber ?? ""}?`,
        message:
          deleting?.adjustmentCount > 0
            ? `The voucher and its ${deleting.adjustmentCount} adjustment(s) are retained for audit; they stop appearing in the expense register.`
            : "The voucher is retained for audit; it stops appearing in the expense register.",
        onConfirm: async () => {
          if (!deleting) return;
          try {
            await expensesApi.remove(deleting.expenseId);
            toast.success("Expense deleted.");
            refresh();
          } catch (error) {
            toast.error(errorText(error, "Could not delete."));
          }
        },
        onClose: () => setDeleting(null)
      })
    ]
  });
}

function Stat({ label, value, sub }) {
  return e.jsxs("div", {
    children: [
      e.jsx("div", { className: "ex-stat__label", children: label }),
      e.jsx("div", { className: "ex-stat__value", children: value }),
      sub && e.jsx("div", { className: "ex-stat__sub", children: sub })
    ]
  });
}

// ── Charts ──────────────────────────────────────────────────────────────────

function arcPath(cx, cy, outer, inner, start, end) {
  // A full ring cannot be one arc: split it in two.
  if (end - start >= Math.PI * 2 - 1e-6) {
    const mid = start + Math.PI;
    return arcPath(cx, cy, outer, inner, start, mid) + arcPath(cx, cy, outer, inner, mid, end);
  }
  const point = (r, a) => [cx + r * Math.sin(a), cy - r * Math.cos(a)];
  const [x0, y0] = point(outer, start);
  const [x1, y1] = point(outer, end);
  const [x2, y2] = point(inner, end);
  const [x3, y3] = point(inner, start);
  const large = end - start > Math.PI ? 1 : 0;
  return (
    `M${x0} ${y0}A${outer} ${outer} 0 ${large} 1 ${x1} ${y1}` +
    `L${x2} ${y2}A${inner} ${inner} 0 ${large} 0 ${x3} ${y3}Z`
  );
}

function CategoryPie({ summary, selected, onSelect }) {
  const [active, setActive] = React.useState(null);
  const [tip, setTip] = React.useState(null);
  const wrap = React.useRef(null);

  if (!summary) return e.jsx("div", { className: "ex-viz__empty", children: "Loading…" });

  const positive = summary.byCategory.filter((c) => c.amount > 0);
  const total = positive.reduce((sum, c) => sum + c.amount, 0);
  if (total <= 0) {
    return e.jsx("div", { className: "ex-viz__empty", children: "No expenses in this filter." });
  }

  // Colour follows rank within this filter; beyond five, the rest fold into "Other".
  const head = positive.slice(0, PIE_SLOTS).map((c, i) => ({ ...c, color: `var(--ex-s${i + 1})` }));
  const tail = positive.slice(PIE_SLOTS);
  const slices = tail.length
    ? [
        ...head,
        {
          category: "Other",
          amount: round2(tail.reduce((s, c) => s + c.amount, 0)),
          count: tail.reduce((s, c) => s + c.count, 0),
          color: "var(--ex-other)",
          other: tail.map((c) => c.category)
        }
      ]
    : head;

  let angle = 0;
  const arcs = slices.map((slice) => {
    const sweep = (slice.amount / total) * Math.PI * 2;
    const arc = { ...slice, start: angle, end: angle + sweep, pct: percent(slice.amount, total) };
    angle += sweep;
    return arc;
  });

  const show = (index, event) => {
    setActive(index);
    if (!event || !wrap.current) return setTip(null);
    const box = wrap.current.getBoundingClientRect();
    setTip({ x: event.clientX - box.left, y: event.clientY - box.top });
  };
  const hide = () => {
    setActive(null);
    setTip(null);
  };
  const pick = (slice) => {
    if (slice.other) return;
    onSelect(selected === slice.category ? "" : slice.category);
  };
  const current = active !== null ? arcs[active] : null;

  return e.jsxs("div", {
    className: "ex-viz ex-pie",
    ref: wrap,
    children: [
      e.jsxs("svg", {
        viewBox: "0 0 200 200",
        role: "img",
        "aria-label": `Expense by category, total ${fmtMoney(total)}. ` +
          arcs.map((a) => `${a.category} ${fmtMoney(a.amount)} (${fmtPercent(a.pct)})`).join(", "),
        children: [
          arcs.map((a, i) =>
            e.jsx("path", {
              d: arcPath(100, 100, 94, 58, a.start, a.end),
              fill: a.color,
              stroke: "var(--surface-card)",
              strokeWidth: arcs.length > 1 ? 2 : 0,
              opacity: active === null || active === i ? 1 : 0.35,
              style: { cursor: a.other ? "default" : "pointer" },
              onMouseMove: (event) => show(i, event),
              onMouseLeave: hide,
              onClick: () => pick(a)
            }, a.category)
          ),
          e.jsx("text", {
            x: 100, y: 94, textAnchor: "middle", fontSize: 11, fill: "var(--text-muted)",
            children: current ? current.category.slice(0, 16) : "Total"
          }),
          e.jsx("text", {
            className: "ex-pie__total",
            x: 100, y: 114, textAnchor: "middle", fontSize: 16, fontWeight: 700, fill: "var(--text-primary)",
            children: compactMoney(current ? current.amount : total)
          })
        ]
      }),
      e.jsxs("table", {
        className: "ex-legend",
        children: [
          e.jsxs("colgroup", { children: [e.jsx("col", {}), e.jsx("col", { className: "ex-col-amt" }), e.jsx("col", { className: "ex-col-pct" })] }),
          e.jsx("thead", {
            children: e.jsxs("tr", {
              children: [
                e.jsx("th", { children: "Category" }),
                e.jsx("th", { className: "ex-num", children: "Amount" }),
                e.jsx("th", { className: "ex-num", children: "%" })
              ]
            })
          }),
          e.jsx("tbody", {
            children: arcs.map((a, i) =>
              e.jsxs("tr", {
                className: active === i ? "is-active" : undefined,
                "data-clickable": a.other ? undefined : "",
                title: a.other ? a.other.join(", ") : selected === a.category ? "Show all categories" : `Filter by ${a.category}`,
                onMouseEnter: () => setActive(i),
                onMouseLeave: hide,
                onClick: () => pick(a),
                children: [
                  e.jsx("td", {
                    children: e.jsxs("div", {
                      className: "ex-legend__name",
                      children: [
                        e.jsx("span", { className: "ex-swatch", style: { background: a.color } }),
                        e.jsx("span", { children: a.other ? `Other (${a.other.length})` : a.category })
                      ]
                    })
                  }),
                  e.jsx("td", { className: "ex-num", children: fmtMoney(a.amount) }),
                  e.jsx("td", { className: "ex-num", children: fmtPercent(a.pct) })
                ]
              }, a.category)
            )
          })
        ]
      }),
      current && tip &&
        e.jsxs("div", {
          className: "ex-viz__tip",
          style: { left: tip.x, top: tip.y },
          children: [
            current.category,
            e.jsx("strong", { children: fmtMoney(current.amount) }),
            `${fmtPercent(current.pct)} of spend · ${current.count} ${current.count === 1 ? "entry" : "entries"}`
          ]
        })
    ]
  });
}

function YearSelect({ year, onChange }) {
  const now = new Date().getFullYear();
  const years = [];
  for (let y = now + 1; y >= now - 5; y--) years.push(y);
  if (!years.includes(year)) years.push(year);
  years.sort((a, b) => b - a);
  return e.jsx("select", {
    className: "field__input ex-year",
    "aria-label": "Year",
    value: year,
    onChange: (event) => onChange(Number(event.target.value)),
    children: years.map((y) => e.jsx("option", { value: y, children: y }, y))
  });
}

function MonthlyChart({ monthly }) {
  const wrap = React.useRef(null);
  const width = useWidth(wrap);
  const [active, setActive] = React.useState(null);

  const months = monthly?.months ?? [];
  const height = 280;
  const pad = { top: 22, right: 8, bottom: 26, left: 52 };
  const plotW = Math.max(0, width - pad.left - pad.right);
  const plotH = height - pad.top - pad.bottom;
  const max = niceMax(Math.max(0, ...months.map((m) => m.amount)));
  const ticks = [0, 0.25, 0.5, 0.75, 1].map((t) => t * max);
  const band = plotW / 12;
  const barW = Math.max(4, Math.min(24, band * 0.55));
  // Month totals sit above the bars whenever they fit; otherwise the tooltip and the
  // table view carry them.
  const showValues = band >= 32;
  const y = (value) => pad.top + plotH - (value / max) * plotH;

  const bar = (m, i) => {
    if (m.amount <= 0) return null;
    const x = pad.left + band * i + (band - barW) / 2;
    const top = y(m.amount);
    const h = pad.top + plotH - top;
    const r = Math.min(4, h, barW / 2);
    // Rounded data end, square at the baseline.
    return e.jsx("path", {
      d: `M${x} ${top + h}V${top + r}Q${x} ${top} ${x + r} ${top}H${x + barW - r}Q${x + barW} ${top} ${x + barW} ${top + r}V${top + h}Z`,
      fill: "var(--ex-bar)",
      opacity: active === null || active === i ? 1 : 0.45
    }, `bar-${i}`);
  };

  const current = active !== null ? months[active] : null;

  return e.jsxs("div", {
    className: "ex-viz ex-monthly",
    children: [
      e.jsxs("div", {
        className: "ex-monthly__head",
        children: [
          e.jsx("span", { className: "ex-sub", children: monthly ? `Total for ${monthly.year}, January – December` : "Loading…" }),
          monthly && e.jsx("span", { className: "ex-monthly__total", children: fmtMoney(monthly.totalAmount) })
        ]
      }),
      e.jsxs("div", {
        ref: wrap,
        style: { position: "relative" },
        children: [
          width > 0 && monthly &&
            e.jsxs("svg", {
              viewBox: `0 0 ${width} ${height}`,
              role: "img",
              "aria-label": `Monthly expense for ${monthly.year}: ` +
                months.map((m) => `${MONTH_NAMES[m.month - 1]} ${fmtMoney(m.amount)}`).join(", "),
              onMouseLeave: () => setActive(null),
              children: [
                ticks.map((t, i) =>
                  e.jsxs("g", {
                    children: [
                      e.jsx("line", {
                        x1: pad.left, x2: width - pad.right, y1: y(t), y2: y(t),
                        stroke: i === 0 ? "var(--border-strong)" : "var(--ex-grid)", strokeWidth: 1
                      }),
                      e.jsx("text", {
                        x: pad.left - 8, y: y(t) + 4, textAnchor: "end", fontSize: 11, fill: "var(--text-muted)",
                        children: t === 0 ? "₹0" : compactMoney(t)
                      })
                    ]
                  }, `tick-${i}`)
                ),
                active !== null &&
                  e.jsx("rect", {
                    x: pad.left + band * active, y: pad.top, width: band, height: plotH,
                    fill: "rgb(var(--accent-rgb) / 8%)", rx: 4
                  }),
                months.map(bar),
                showValues &&
                  months.map((m, i) =>
                    m.amount > 0 &&
                      e.jsx("text", {
                        x: pad.left + band * i + band / 2, y: y(m.amount) - 6, textAnchor: "middle",
                        fontSize: 10, fontWeight: 600, fill: "var(--text-secondary)",
                        children: compactMoney(m.amount)
                      }, `val-${i}`)
                  ),
                months.map((m, i) =>
                  e.jsx("text", {
                    x: pad.left + band * i + band / 2, y: height - 8, textAnchor: "middle",
                    fontSize: 11, fill: active === i ? "var(--text-primary)" : "var(--text-muted)",
                    children: band < 30 ? MONTHS[m.month - 1][0] : MONTHS[m.month - 1]
                  }, `m-${i}`)
                ),
                // Hit targets: the whole column, not just the bar.
                months.map((m, i) =>
                  e.jsx("rect", {
                    x: pad.left + band * i, y: pad.top, width: band, height: plotH + pad.bottom,
                    fill: "transparent",
                    onMouseEnter: () => setActive(i)
                  }, `hit-${i}`)
                )
              ]
            }),
          current &&
            e.jsxs("div", {
              className: "ex-viz__tip",
              style: {
                left: Math.min(Math.max(pad.left + band * active + band / 2, 80), width - 80),
                top: Math.min(y(current.amount), pad.top + plotH - 10)
              },
              children: [
                `${MONTH_NAMES[current.month - 1]} ${monthly.year}`,
                e.jsx("strong", { children: fmtMoney(current.amount) }),
                `${current.count} ${current.count === 1 ? "entry" : "entries"}`
              ]
            })
        ]
      }),
      monthly &&
        e.jsxs("details", {
          children: [
            e.jsx("summary", { children: "View as table" }),
            e.jsx("table", {
              children: e.jsx("tbody", {
                children: months.map((m) =>
                  e.jsxs("tr", {
                    children: [
                      e.jsx("td", { children: MONTH_NAMES[m.month - 1] }),
                      e.jsx("td", { children: fmtMoney(m.amount) })
                    ]
                  }, m.month)
                )
              })
            })
          ]
        })
    ]
  });
}

// ── Adjustment history ──────────────────────────────────────────────────────

function AdjustmentHistory({ version, expense, onClearExpense, canEdit, canDelete, onEdit, onChanged }) {
  const toast = useToast();
  const [search, setSearch] = React.useState("");
  const debouncedSearch = useDebounced(search);
  const [type, setType] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [viewing, setViewing] = React.useState(null);
  const [deleting, setDeleting] = React.useState(null);

  const params = {
    page,
    pageSize: 20,
    expenseId: expense?.expenseId,
    search: debouncedSearch || undefined,
    adjustmentType: type || undefined,
    from: from || undefined,
    to: to || undefined
  };
  const key = JSON.stringify(params);

  React.useEffect(() => {
    let live = true;
    setLoading(true);
    adjustmentsApi
      .list(params)
      .then((data) => live && setResult(data))
      .catch((error) => live && toast.error(errorText(error, "Failed to load adjustments.")))
      .finally(() => live && setLoading(false));
    return () => {
      live = false;
    };
  }, [key, version]);

  const onFilter = (setter) => (event) => {
    setPage(1);
    setter(event.target.value);
  };

  const columns = [
    { key: "date", header: "Date", render: (a) => e.jsx("span", { style: { whiteSpace: "nowrap" }, children: fmtDate(a.adjustmentDate) }) },
    {
      key: "voucher",
      header: "Expense / Voucher",
      render: (a) =>
        e.jsxs("div", {
          children: [
            e.jsx("div", { style: { fontWeight: 600 }, children: a.expenseNumber }),
            e.jsx(Muted, { children: a.category })
          ]
        })
    },
    {
      key: "type",
      header: "Type",
      render: (a) =>
        e.jsx(Badge, { tone: a.adjustmentType === 1 ? "warning" : "success", children: ADJUSTMENT_TYPES[a.adjustmentType] })
    },
    {
      key: "amount",
      header: "Amount",
      render: (a) =>
        e.jsx("strong", { className: a.signedAmount > 0 ? "ex-plus" : "ex-minus", style: { whiteSpace: "nowrap" }, children: signedMoney(a.signedAmount) })
    },
    {
      key: "reason",
      header: "Reason",
      render: (a) =>
        e.jsxs("div", { children: [e.jsx("div", { children: a.reason }), a.remarks && e.jsx(Muted, { children: a.remarks })] })
    },
    {
      key: "net",
      header: "Expense total",
      render: (a) =>
        e.jsxs("div", {
          children: [e.jsx("div", { children: fmtMoney(a.expenseNetAmount) }), e.jsx(Muted, { children: `Voucher ${fmtMoney(a.expenseAmount)}` })]
        })
    },
    {
      key: "actions",
      header: "",
      render: (a) =>
        e.jsxs("div", {
          className: "row-actions ex-row-actions",
          children: [
            e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setViewing(a), children: "View" }),
            canEdit && e.jsx(Button, { size: "sm", variant: "outline", onClick: () => onEdit(a), children: "Edit" }),
            canDelete && e.jsx(Button, { size: "sm", variant: "ghost", onClick: () => setDeleting(a), children: "Delete" })
          ]
        })
    }
  ];

  return e.jsxs(e.Fragment, {
    children: [
      expense &&
        e.jsxs("div", {
          className: "ex-chip",
          children: [
            `Showing ${expense.expenseNumber}`,
            e.jsx("button", { type: "button", "aria-label": "Show all expenses", onClick: onClearExpense, children: "×" })
          ]
        }),
      e.jsxs("div", {
        className: "ex-filters",
        children: [
          e.jsx(Input, { label: "Search", placeholder: "Voucher, reason or remarks", value: search, onChange: onFilter(setSearch) }),
          e.jsx(Select, { label: "Adjustment type", value: type, placeholder: "All types", options: ADJUSTMENT_TYPE_OPTIONS.map((o) => ({ value: o.value, label: ADJUSTMENT_TYPES[o.value] })), onChange: onFilter(setType) }),
          e.jsx(Input, { label: "From", type: "date", value: from, max: to || undefined, onChange: onFilter(setFrom) }),
          e.jsx(Input, { label: "To", type: "date", value: to, min: from || undefined, onChange: onFilter(setTo) })
        ]
      }),
      e.jsx(DataTable, {
        columns,
        result,
        loading,
        rowKey: (a) => a.expenseAdjustmentId,
        onPageChange: setPage,
        onRowClick: setViewing,
        emptyTitle: "No adjustments",
        emptyDescription: "Use Adjust on an expense to record a refund, extra charge or correction."
      }),
      viewing &&
        e.jsx(AdjustmentView, {
          adjustment: viewing,
          canEdit,
          onEdit: () => {
            setViewing(null);
            onEdit(viewing);
          },
          onClose: () => setViewing(null)
        }),
      e.jsx(ConfirmDialog, {
        open: deleting !== null,
        danger: true,
        confirmLabel: "Delete",
        title: "Delete this adjustment?",
        message: deleting
          ? `${deleting.expenseNumber} goes back to ${fmtMoney(round2(deleting.expenseNetAmount - deleting.signedAmount))}. The adjustment is retained for audit.`
          : "",
        onConfirm: async () => {
          if (!deleting) return;
          try {
            await adjustmentsApi.remove(deleting.expenseAdjustmentId);
            toast.success("Adjustment deleted.");
            onChanged();
          } catch (error) {
            toast.error(errorText(error, "Could not delete the adjustment."));
          }
        },
        onClose: () => setDeleting(null)
      })
    ]
  });
}

function AdjustmentView({ adjustment: a, canEdit, onEdit, onClose }) {
  const row = (label, value, className) =>
    e.jsxs("div", { className, children: [e.jsx("span", { children: label }), e.jsx("span", { children: value ?? "—" })] });
  return e.jsx(Modal, {
    open: true,
    title: `Adjustment — ${a.expenseNumber}`,
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Close" }),
        canEdit && e.jsx(Button, { onClick: onEdit, children: "Edit" })
      ]
    }),
    children: e.jsxs("div", {
      className: "summary-rows",
      children: [
        row("Expense / Voucher", `${a.expenseNumber} · ${a.category}`),
        row("Expense date", fmtDate(a.expenseDate)),
        row("Adjustment type", ADJUSTMENT_TYPES[a.adjustmentType]),
        row("Adjustment amount", signedMoney(a.signedAmount)),
        row("Adjustment date", fmtDate(a.adjustmentDate)),
        row("Reason", a.reason),
        row("Remarks", a.remarks),
        row("Entered by", a.createdByName),
        row("Voucher amount", fmtMoney(a.expenseAmount)),
        row("Expense total after all adjustments", fmtMoney(a.expenseNetAmount), "summary-total")
      ]
    })
  });
}

function AdjustmentForm({ expense: presetExpense, adjustment, onClose, onSaved }) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [saving, setSaving] = React.useState(false);

  // The expense is fixed when editing, or when opened from an expense row.
  const fixed = adjustment
    ? { expenseId: adjustment.expenseId, expenseNumber: adjustment.expenseNumber, category: adjustment.category, amount: adjustment.expenseAmount, netAmount: adjustment.expenseNetAmount }
    : presetExpense;
  const [expense, setExpense] = React.useState(fixed ?? null);
  const [lookup, setLookup] = React.useState("");
  const debouncedLookup = useDebounced(lookup);
  const [matches, setMatches] = React.useState([]);

  React.useEffect(() => {
    if (fixed) return;
    let live = true;
    expensesApi
      .list({ page: 1, pageSize: 50, search: debouncedLookup || undefined })
      .then((data) => live && setMatches(data.items))
      .catch(() => live && setMatches([]));
    return () => {
      live = false;
    };
  }, [debouncedLookup]);

  const [form, setForm] = React.useState({
    adjustmentType: adjustment ? String(adjustment.adjustmentType) : "2",
    amount: adjustment ? String(adjustment.amount) : "",
    adjustmentDate: adjustment?.adjustmentDate ?? today(),
    reason: adjustment?.reason ?? "",
    remarks: adjustment?.remarks ?? ""
  });
  const set = (field, value) => {
    clearField(field);
    setForm((f) => ({ ...f, [field]: value }));
  };

  const amount = Number(form.amount);
  const signed = form.adjustmentType === "1" ? amount : -amount;
  // When editing, this row's old effect is already inside the expense's net total.
  const base = expense ? expense.netAmount - (adjustment ? adjustment.signedAmount : 0) : 0;
  const after = round2(base + (amount > 0 ? signed : 0));
  const valid = expense && amount > 0 && form.reason.trim().length > 0 && form.adjustmentDate && after >= 0;

  const save = async () => {
    setSaving(true);
    clear();
    const body = {
      expenseId: expense.expenseId,
      adjustmentType: Number(form.adjustmentType),
      amount,
      adjustmentDate: form.adjustmentDate || null,
      reason: form.reason.trim(),
      remarks: form.remarks.trim() || null
    };
    try {
      if (adjustment) await adjustmentsApi.update(adjustment.expenseAdjustmentId, body);
      else await adjustmentsApi.create(body);
      toast.success(adjustment ? "Adjustment updated." : "Adjustment recorded.");
      onSaved();
    } catch (error) {
      const message = capture(error);
      if (message) toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const expenseOptions = matches.map((x) => ({
    value: x.expenseId,
    label: `${x.expenseNumber} · ${x.category} · ${fmtMoney(x.netAmount)}`
  }));

  return e.jsx(Modal, {
    open: true,
    title: adjustment ? `Edit adjustment — ${adjustment.expenseNumber}` : "Add adjustment",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
        e.jsx(Button, { onClick: save, loading: saving, disabled: !valid, children: "Save" })
      ]
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        summary && e.jsx("div", { className: "form-error", role: "alert", children: summary }),
        fixed
          ? e.jsx(Input, { label: "Expense / Voucher", value: `${fixed.expenseNumber} · ${fixed.category}`, disabled: true, readOnly: true })
          : e.jsxs("div", {
              className: "form-grid",
              children: [
                e.jsx(Input, { label: "Find voucher", placeholder: "Voucher, description or remarks", value: lookup, onChange: (event) => setLookup(event.target.value) }),
                e.jsx(Select, {
                  label: "Expense / Voucher",
                  required: true,
                  value: expense ? String(expense.expenseId) : "",
                  placeholder: matches.length ? "Select an expense" : "No matching expenses",
                  options: expenseOptions,
                  error: fieldErrors.expenseId,
                  onChange: (event) => {
                    clearField("expenseId");
                    setExpense(matches.find((x) => String(x.expenseId) === event.target.value) ?? null);
                  }
                })
              ]
            }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Select, { label: "Adjustment type", required: true, value: form.adjustmentType, options: ADJUSTMENT_TYPE_OPTIONS, error: fieldErrors.adjustmentType, onChange: (event) => set("adjustmentType", event.target.value) }),
            e.jsx(Input, { label: "Adjustment amount", type: "number", min: "0", step: "0.01", required: true, value: form.amount, error: fieldErrors.amount, onChange: (event) => set("amount", event.target.value) }),
            e.jsx(Input, { label: "Date", type: "date", required: true, value: form.adjustmentDate, error: fieldErrors.adjustmentDate, onChange: (event) => set("adjustmentDate", event.target.value) })
          ]
        }),
        e.jsx(Input, { label: "Reason / Description", required: true, value: form.reason, hint: "e.g. Supplier refund, delivery charge billed later", error: fieldErrors.reason, onChange: (event) => set("reason", event.target.value) }),
        e.jsx(Input, { label: "Remarks", value: form.remarks, hint: "Optional — credit note, reference or approval", error: fieldErrors.remarks, onChange: (event) => set("remarks", event.target.value) }),
        expense &&
          e.jsxs("div", {
            className: `summary-rows ex-preview${after < 0 ? " ex-preview--bad" : ""}`,
            children: [
              e.jsxs("div", { children: [e.jsx("span", { children: "Voucher amount" }), e.jsx("span", { children: fmtMoney(expense.amount) })] }),
              e.jsxs("div", { children: [e.jsx("span", { children: "Total before this adjustment" }), e.jsx("span", { children: fmtMoney(base) })] }),
              e.jsxs("div", { children: [e.jsx("span", { children: "This adjustment" }), e.jsx("span", { children: amount > 0 ? signedMoney(signed) : "—" })] }),
              e.jsxs("div", {
                className: "summary-total",
                children: [e.jsx("span", { children: "New expense total" }), e.jsx("span", { children: fmtMoney(after) })]
              }),
              after < 0 && e.jsx("div", { role: "alert", style: { color: "var(--color-danger)" }, children: "An adjustment cannot take an expense below zero." })
            ]
          })
      ]
    })
  });
}

// ── Expense form (unchanged behaviour from the original page) ───────────────

function ExpenseForm({ expense, onClose, onSaved }) {
  const toast = useToast();
  const { fieldErrors, summary, capture, clear, clearField } = useFieldErrors();
  const [locations, setLocations] = React.useState([]);
  const [suppliers, setSuppliers] = React.useState([]);
  const [saving, setSaving] = React.useState(false);
  const [form, setForm] = React.useState({
    expenseDate: expense?.expenseDate ?? today(),
    category: expense?.category ?? "",
    description: expense?.description ?? "",
    amount: expense ? String(expense.amount) : "",
    paymentMethod: expense ? String(expense.paymentMethod) : "6",
    remarks: expense?.remarks ?? "",
    locationId: expense ? String(expense.locationId) : "",
    supplierId: expense?.supplierId ? String(expense.supplierId) : ""
  });

  React.useEffect(() => {
    (async () => {
      try {
        const [locs, sups] = await Promise.all([
          locationsApi.list({ page: 1, pageSize: 100 }),
          suppliersApi.list({ page: 1, pageSize: 100 })
        ]);
        setLocations(locs.items.map((l) => ({ value: l.locationId, label: l.locationName })));
        setSuppliers(sups.items.map((s) => ({ value: s.supplierId, label: s.supplierName })));
        if (!expense && locs.items.length === 1) {
          setForm((f) => ({ ...f, locationId: String(locs.items[0].locationId) }));
        }
      } catch {
        setLocations([]);
        setSuppliers([]);
      }
    })();
  }, []);

  const set = (field, value) => {
    clearField(field);
    setForm((f) => ({ ...f, [field]: value }));
  };

  const save = async () => {
    setSaving(true);
    clear();
    const body = {
      expenseDate: form.expenseDate || null,
      category: form.category.trim(),
      description: form.description.trim(),
      amount: Number(form.amount),
      paymentMethod: Number(form.paymentMethod),
      remarks: form.remarks.trim(),
      attachmentPath: expense?.attachmentPath ?? null,
      locationId: Number(form.locationId),
      supplierId: form.supplierId ? Number(form.supplierId) : null,
      purchaseOrderId: expense?.purchaseOrderId ?? null
    };
    try {
      if (expense) await expensesApi.update(expense.expenseId, body);
      else await expensesApi.create(body);
      toast.success(expense ? "Expense updated." : "Expense recorded.");
      onSaved();
    } catch (error) {
      const message = capture(error);
      if (message) toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  const valid =
    form.category.trim().length > 0 &&
    form.description.trim().length > 0 &&
    form.remarks.trim().length > 0 &&
    form.locationId !== "" &&
    Number(form.amount) > 0;

  return e.jsx(Modal, {
    open: true,
    title: expense ? `Edit — ${expense.expenseNumber}` : "Add expense",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
        e.jsx(Button, { onClick: save, loading: saving, disabled: !valid, children: "Save" })
      ]
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        summary && e.jsx("div", { className: "form-error", role: "alert", children: summary }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Input, { label: "Expense date", type: "date", required: true, value: form.expenseDate, error: fieldErrors.expenseDate, onChange: (ev) => set("expenseDate", ev.target.value) }),
            e.jsx(Input, { label: "Category", required: true, value: form.category, hint: "e.g. Office Maintenance", error: fieldErrors.category, onChange: (ev) => set("category", ev.target.value) }),
            e.jsx(Input, {
              label: "Amount", type: "number", min: "0", step: "0.01", required: true, value: form.amount,
              hint: expense?.adjustmentCount > 0 ? `Voucher amount — adjustments add ${signedMoney(expense.adjustmentTotal)}` : undefined,
              error: fieldErrors.amount, onChange: (ev) => set("amount", ev.target.value)
            }),
            e.jsx(Select, { label: "Payment mode", required: true, value: form.paymentMethod, options: PAYMENT_OPTIONS, error: fieldErrors.paymentMethod, onChange: (ev) => set("paymentMethod", ev.target.value) }),
            e.jsx(Select, { label: "Location", required: true, value: form.locationId, placeholder: "Select a location", options: locations, error: fieldErrors.locationId, onChange: (ev) => set("locationId", ev.target.value) }),
            e.jsx(Select, { label: "Supplier", value: form.supplierId, placeholder: "Not linked", options: suppliers, error: fieldErrors.supplierId, onChange: (ev) => set("supplierId", ev.target.value) })
          ]
        }),
        e.jsx(Input, { label: "Description", required: true, value: form.description, hint: "What the money was spent on", error: fieldErrors.description, onChange: (ev) => set("description", ev.target.value) }),
        e.jsx(Input, { label: "Remarks", required: true, value: form.remarks, hint: "Why it was needed — required on every expense", error: fieldErrors.remarks, onChange: (ev) => set("remarks", ev.target.value) })
      ]
    })
  });
}

export { AdminExpensesPage };
