import { j as x, r as R, G as Spinner, g as Link, D as unwrap, E as http, l as authStore, a3 as iconFor } from "./index-DI5X4c9f.js";
import { d as dashboards } from "./reports-CNtuzowc.js";
import { C as Card } from "./Card-CNXipofE.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { b as money, c as dateOnly } from "./formatters-DFe5xCRi.js";

const CREDIT = 1;
const MONTH = new Intl.DateTimeFormat("en-IN", { timeZone: "Asia/Kolkata", month: "short" });
const COUNT = new Intl.NumberFormat("en-IN");

/* Sub-modules of Billing & Finance. `perm` mirrors the route guard, so a card
   is hidden for exactly the roles that would be bounced to /forbidden.
   Reconciliation has no sidebar entry by design -- this grid is the only way
   into it, so it must never be dropped from here. */
const MODULES = [
  { to: "/admin/billing/invoices", icon: "ReceiptText", title: "Invoices", desc: "Raise, send and settle customer invoices", perm: "INVOICE.VIEW", count: "invoices" },
  { to: "/admin/billing/payments", icon: "CreditCard", title: "Payments", desc: "Every payment captured, across all channels", perm: "PAYMENT.VIEW", count: "payments" },
  { to: "/admin/billing/wallet", icon: "Wallet", title: "Wallet", desc: "Customer balances, top-ups and adjustments", perm: "WALLET.VIEW" },
  { to: "/admin/billing/quotations", icon: "FileSpreadsheet", title: "Quotations", desc: "Draft, send and convert quotes to invoices", perm: "QUOTATION.VIEW", count: "quotations" },
  { to: "/admin/billing/expenses", icon: "Receipt", title: "Expenses", desc: "Recorded company spend, by category", perm: "EXPENSE.VIEW", count: "expenses" },
  { to: "/admin/billing/purchase-orders", icon: "ShoppingCart", title: "Purchase Orders", desc: "Commitments raised against suppliers", perm: "PURCHASE.VIEW", count: "purchaseOrders" },
  { to: "/admin/billing/suppliers", icon: "Building2", title: "Suppliers", desc: "Vendor master, contacts and payment terms", perm: "SUPPLIER.VIEW", count: "suppliers" },
  { to: "/admin/billing/bank-accounts", icon: "Landmark", title: "Bank Accounts", desc: "The accounts the business banks through", perm: "BANK.VIEW", count: "bankAccounts" },
  { to: "/admin/billing/transactions", icon: "ArrowLeftRight", title: "Transactions", desc: "Bank statement lines, money in and out", perm: "BANK.VIEW", count: "bankTransactions" },
  { to: "/admin/billing/reconciliation", icon: "ListChecks", title: "Reconciliation", desc: "Match statement lines to payments and expenses", perm: "BANK.VIEW" }
];

/* Each entry resolves to a paged list endpoint; we ask for one row and read
   totalCount, so a badge costs one cheap request instead of a full page. */
const COUNT_SOURCES = [
  { key: "invoices", perm: "INVOICE.VIEW", url: "/invoices" },
  { key: "payments", perm: "PAYMENT.VIEW", url: "/payments" },
  { key: "quotations", perm: "QUOTATION.VIEW", url: "/quotations" },
  { key: "expenses", perm: "EXPENSE.VIEW", url: "/expenses" },
  { key: "purchaseOrders", perm: "PURCHASE.VIEW", url: "/purchase-orders" },
  { key: "suppliers", perm: "SUPPLIER.VIEW", url: "/suppliers" },
  { key: "bankAccounts", perm: "BANK.VIEW", url: "/bank/accounts" },
  { key: "bankTransactions", perm: "BANK.VIEW", url: "/bank/transactions" },
  { key: "overdueInvoices", perm: "INVOICE.VIEW", url: "/invoices", params: { Overdue: true } }
];

function Icon({ name, size = 18 }) {
  const Glyph = iconFor(name);
  return x.jsx(Glyph, { size });
}

function Kpi({ icon, label, value, badge, tone, hint }) {
  return x.jsxs("div", {
    className: "bill-kpi",
    children: [
      x.jsxs("div", {
        className: "bill-kpi__top",
        children: [
          x.jsx("span", { className: "bill-icon", children: x.jsx(Icon, { name: icon }) }),
          badge && x.jsx(Badge, { tone: tone ?? "neutral", children: badge })
        ]
      }),
      x.jsx("div", { className: "bill-kpi__value", children: value ?? "—" }),
      x.jsx("div", { className: "bill-kpi__label", children: label }),
      hint && x.jsx("div", { className: "bill-kpi__hint", children: hint })
    ]
  });
}

function NavCard({ mod, count }) {
  return x.jsxs(Link, {
    to: mod.to,
    className: "bill-nav__card",
    children: [
      x.jsxs("div", {
        className: "bill-nav__top",
        children: [
          x.jsx("span", { className: "bill-icon", children: x.jsx(Icon, { name: mod.icon }) }),
          x.jsx("span", { className: "bill-nav__title", children: mod.title }),
          count !== undefined && x.jsx(Badge, { tone: "neutral", children: COUNT.format(count) })
        ]
      }),
      x.jsx("div", { className: "bill-nav__desc", children: mod.desc })
    ]
  });
}

function RecentTransactions({ rows, allowed }) {
  const body = !allowed
    ? x.jsx("p", { className: "bill-empty", children: "Your role does not have access to bank transactions." })
    : rows === null
      ? x.jsx("p", { className: "bill-empty", children: "Could not load recent transactions." })
      : rows.length === 0
        ? x.jsx("p", { className: "bill-empty", children: "No bank transactions recorded yet. Import or add a statement line and it will show up here." })
        : x.jsx("div", {
            children: rows.map(t => {
              const credit = t.direction === CREDIT;
              return x.jsxs("div", {
                className: "bill-txn__row",
                children: [
                  x.jsxs("div", {
                    className: "bill-txn__main",
                    children: [
                      x.jsx("div", { className: "bill-txn__title", children: t.narration || t.referenceNumber || "Statement line" }),
                      x.jsx("div", {
                        className: "bill-txn__meta",
                        children: [t.bankName, t.transactionDate && dateOnly(t.transactionDate), t.category].filter(Boolean).join(" · ")
                      })
                    ]
                  }),
                  x.jsxs("div", {
                    className: `bill-txn__amt bill-txn__amt--${credit ? "in" : "out"}`,
                    children: [credit ? "+" : "−", " ", money(Number(t.amount) || 0)]
                  })
                ]
              }, t.bankTransactionId);
            })
          });

  return x.jsx(Card, {
    title: "Recent transactions",
    actions: allowed ? x.jsx(Link, { to: "/admin/billing/transactions", children: "View all" }) : null,
    children: body
  });
}

function AdminBillingPage() {
  const permissions = authStore(s => s.permissions);
  const can = R.useCallback(p => permissions.includes(p), [permissions]);

  const [finance, setFinance] = R.useState(null);
  const [admin, setAdmin] = R.useState(null);
  const [counts, setCounts] = R.useState({});
  const [recent, setRecent] = R.useState([]);
  const [partial, setPartial] = R.useState(false);
  const [loading, setLoading] = R.useState(true);

  R.useEffect(() => {
    let cancelled = false;

    (async () => {
      try {
        /* Both dashboards are permission-gated server side. A role that can
           see some finance modules but not the roll-up (Operations Manager,
           for one) gets a 403 here -- that is expected, not an error worth
           shouting about, so the affected tiles just read "—". */
        const [fin, adm] = await Promise.allSettled([dashboards.finance(), dashboards.admin()]);
        if (cancelled) return;
        if (fin.status === "fulfilled") setFinance(fin.value); else setPartial(true);
        if (adm.status === "fulfilled") setAdmin(adm.value); else setPartial(true);

        const wanted = COUNT_SOURCES.filter(s => permissions.includes(s.perm));
        const results = await Promise.allSettled(
          wanted.map(s => unwrap(http.get(s.url, { params: { Page: 1, PageSize: 1, ...(s.params ?? {}) } })))
        );
        if (cancelled) return;
        const next = {};
        results.forEach((r, i) => {
          if (r.status === "fulfilled" && typeof r.value?.totalCount === "number") next[wanted[i].key] = r.value.totalCount;
        });
        setCounts(next);

        if (permissions.includes("BANK.VIEW")) {
          try {
            const page = await unwrap(http.get("/bank/transactions", {
              params: { Page: 1, PageSize: 5, SortBy: "transactionDate", IsDescending: true }
            }));
            if (!cancelled) setRecent(page?.items ?? []);
          } catch {
            if (!cancelled) setRecent(null);
          }
        }
      } finally {
        if (!cancelled) setLoading(false);
      }
    })();

    return () => { cancelled = true; };
  }, [permissions]);

  if (loading) return x.jsx(Spinner, { center: true });

  const card = key => finance?.cards?.find(c => c.key === key) ?? null;
  const adminCard = key => admin?.cards?.find(c => c.key === key) ?? null;
  const thisMonth = MONTH.format(new Date());

  const received = card("received-month");
  const outstanding = card("outstanding");
  const today = adminCard("revenue-today");
  const expenses = card("expenses-month");
  const overdue = counts.overdueInvoices;

  const kpis = [
    { icon: "IndianRupee", label: "Revenue this month", value: received?.value, badge: thisMonth, tone: "gold", hint: received?.hint },
    { icon: "ReceiptText", label: "Outstanding / unpaid", value: outstanding?.value, badge: "Due", tone: "warning", hint: outstanding?.hint },
    { icon: "CreditCard", label: "Payments received today", value: today?.value, badge: "Today", tone: "neutral", hint: today?.hint },
    { icon: "Wallet", label: "Wallet balance", value: "—", badge: "Wallets", tone: "neutral", hint: "Needs an aggregate wallet endpoint" },
    { icon: "Clock", label: "Overdue invoices", value: overdue === undefined ? "—" : COUNT.format(overdue), badge: "Overdue", tone: overdue ? "danger" : "neutral", hint: overdue ? "Past the due date and unsettled" : "Nothing past its due date" },
    { icon: "Receipt", label: "Expenses this month", value: expenses?.value, badge: thisMonth, tone: "neutral", hint: expenses?.hint }
  ];

  const visible = MODULES.filter(m => can(m.perm));

  return x.jsxs(x.Fragment, {
    children: [
      x.jsx(PageHeader, {
        title: "Billing & Finance",
        description: "Invoicing, collections, spend and banking for Regalis WorkSpace — in one place."
      }),

      x.jsxs("div", {
        children: [
          x.jsx("div", { className: "bill-kpis", children: kpis.map(k => x.jsx(Kpi, { ...k }, k.label)) }),
          partial && x.jsx("p", {
            className: "bill-kpi__hint",
            style: { marginTop: "var(--space-3)" },
            children: "Some figures are not available to your role and show as —."
          })
        ]
      }),

      x.jsxs("div", {
        children: [
          x.jsx("div", {
            className: "bill-section__head",
            style: { marginBottom: "var(--space-4)" },
            children: x.jsx("h3", { className: "bill-section__title", children: "Modules" })
          }),
          visible.length === 0
            ? x.jsx(Card, { children: x.jsx("p", { className: "bill-empty", children: "Your role has no finance modules assigned." }) })
            : x.jsx("div", {
                className: "bill-nav",
                children: visible.map(m => x.jsx(NavCard, { mod: m, count: m.count ? counts[m.count] : undefined }, m.to))
              })
        ]
      }),

      x.jsx(RecentTransactions, { rows: recent, allowed: can("BANK.VIEW") })
    ]
  });
}

export { AdminBillingPage };
