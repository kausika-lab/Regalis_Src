/**
 * Admin -> Billing & Finance -> Wallet.
 *
 *   AdminWalletPage        /admin/billing/wallet               every customer wallet
 *   AdminWalletDetailPage  /admin/billing/wallet/:customerId   one customer's ledger
 *
 * All money comes from GET /api/admin/wallets, which sums the append-only ledger in SQL
 * (credits, debits, balance = credits − debits). Nothing here is computed from stored
 * wallet counters, and the low-balance line is the Wallet.LowBalanceThreshold setting
 * (Settings → Billing), returned with the list — never hard-coded.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file
 * by name. It is a new file, so it needs no sw.js precache entry.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as e,
  G as Spinner,
  p as Button,
  F as Select,
  I as Input,
  u as useNavigate,
  Y as useParams,
  D as unwrap,
  E as http,
  i as makeIcon
} from "./index-DI5X4c9f.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { b as fmtMoney, c as fmtDate, f as fmtDateTime } from "./formatters-DFe5xCRi.js";
import { C as IconChevronLeft, a as IconChevronRight } from "./chevron-right-BikENMwC.js";

const WALLET = "/admin/billing/wallet";

const saveBlob = async (url, params, fallbackName) => {
  const response = await http.get(url, { params, responseType: "blob" });
  const disposition = response.headers?.["content-disposition"] ?? "";
  const named = /filename\*?=(?:UTF-8'')?"?([^";]+)"?/i.exec(disposition)?.[1];
  const href = URL.createObjectURL(response.data);
  const link = document.createElement("a");
  link.href = href;
  link.download = named ? decodeURIComponent(named) : fallbackName;
  link.click();
  setTimeout(() => URL.revokeObjectURL(href), 1000);
};

const walletsApi = {
  list: (params) => unwrap(http.get("/admin/wallets", { params })),
  get: (customerId, params) => unwrap(http.get(`/admin/wallets/${customerId}`, { params })),
  addMoney: (customerId, body) => unwrap(http.post(`/admin/wallets/${customerId}/add-money`, body)),
  adjust: (customerId, body) => unwrap(http.post(`/admin/wallets/${customerId}/adjust`, body)),
  statement: (customerId, params, format) => saveBlob(`/admin/wallets/${customerId}/statement`, { ...params, format }, `wallet-statement.${format}`),
  receipt: (paymentId, paymentNumber) => saveBlob(`/payments/${paymentId}/receipt`, undefined, `${paymentNumber ?? "receipt"}.pdf`),
  exportCsv: async (params) => {
    const response = await http.get("/admin/wallets/export", { params, responseType: "blob" });
    const href = URL.createObjectURL(response.data);
    const link = document.createElement("a");
    link.href = href;
    link.download = `customer-wallets-${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    setTimeout(() => URL.revokeObjectURL(href), 1000);
  }
};

const FILTERS = [
  { value: "all", label: "All wallets" },
  { value: "with-balance", label: "With balance" },
  { value: "zero", label: "Zero balance" },
  { value: "low", label: "Low balance" }
];
const SORTS = [
  { value: "balance-desc", label: "Sort: Balance (high to low)" },
  { value: "balance-asc", label: "Sort: Balance (low to high)" },
  { value: "name", label: "Sort: Name (A–Z)" },
  { value: "last-activity", label: "Sort: Last activity" },
  { value: "spent", label: "Sort: Total spent" }
];
const ACTIVITY = [
  { value: "all", label: "All time" },
  { value: "30d", label: "Active in last 30 days" },
  { value: "90d", label: "Active in last 90 days" },
  { value: "inactive-90d", label: "No activity for 90+ days" }
];

const IconSearch = makeIcon("search", [
  ["circle", { cx: "11", cy: "11", r: "8", key: "4ej97u" }],
  ["path", { d: "m21 21-4.3-4.3", key: "1qie3q" }]
]);

const initials = (name) =>
  (name || "?").trim().split(/\s+/).slice(0, 2).map((p) => p[0]?.toUpperCase() ?? "").join("") || "?";
const plus = (v) => `+ ${fmtMoney(v)}`;
const minus = (v) => `− ${fmtMoney(v)}`;
const errorText = (error, fallback) => (error instanceof ApiError ? error.message : fallback);

// ── Styles ──────────────────────────────────────────────────────────────────

const STYLE_ID = "wallet-pages-styles";
const STYLES = `
.wl-cards{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:var(--space-4);margin:var(--space-5) 0}
@media(max-width:1100px){.wl-cards{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.wl-cards{grid-template-columns:minmax(0,1fr)}}
.wl-card{padding:var(--space-4) var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm)}
.wl-card__label{font-size:var(--font-size-sm);color:var(--text-secondary)}
.wl-card__value{margin:4px 0 2px;font-family:var(--font-display);font-size:var(--font-size-2xl);font-weight:800;color:var(--text-primary);font-variant-numeric:tabular-nums;line-height:1.2}
.wl-card__sub{font-size:var(--font-size-xs);color:var(--text-muted)}
.wl-in{color:var(--color-success)}
.wl-out{color:var(--color-danger)}
.wl-low{color:var(--color-warning)}
.wl-card__value.wl-low{color:var(--regalis-gold-dark)}
.wl-panel{border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:var(--surface-card);background-image:var(--card-sheen);box-shadow:var(--shadow-sm);padding:var(--space-5)}
.wl-toolbar{display:grid;grid-template-columns:minmax(220px,1.6fr) minmax(150px,.8fr) minmax(200px,1fr) minmax(150px,.8fr);gap:var(--space-3);margin-bottom:var(--space-4)}
@media(max-width:900px){.wl-toolbar{grid-template-columns:repeat(2,minmax(0,1fr))}}
@media(max-width:560px){.wl-toolbar{grid-template-columns:minmax(0,1fr)}}
.wl-toolbar .field{min-width:0}
.wl-toolbar .field__label{position:absolute;width:1px;height:1px;overflow:hidden;clip-path:inset(50%)}
.wl-toolbar .field__input{height:40px}
.wl-search{position:relative}
.wl-search svg{position:absolute;inset-inline-start:12px;top:50%;transform:translateY(-50%);color:var(--text-muted);pointer-events:none}
.wl-search input{width:100%;height:40px;padding:8px 12px 8px 36px}
.wl-table-wrap{overflow-x:auto;position:relative}
.wl-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm)}
.wl-table th{padding:10px 12px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.06em;text-transform:uppercase;color:var(--text-secondary);background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.wl-table td{padding:12px;border-bottom:1px solid var(--border-subtle);vertical-align:middle}
.wl-table tbody tr{cursor:pointer;transition:background var(--transition-fast)}
.wl-table tbody tr:hover{background:var(--surface-hover)}
.wl-num{text-align:right!important;font-variant-numeric:tabular-nums;white-space:nowrap}
.wl-cust{display:flex;align-items:center;gap:var(--space-3);min-width:220px}
.wl-avatar{flex-shrink:0;display:grid;place-items:center;width:36px;height:36px;border-radius:50%;background:rgb(var(--accent-rgb) / 18%);color:var(--text-primary);font-size:var(--font-size-xs);font-weight:700}
.wl-cust__name{font-weight:600;color:var(--text-primary)}
.wl-cust__sub{font-size:var(--font-size-xs);color:var(--text-muted);overflow-wrap:anywhere}
.wl-balance{font-weight:800;color:var(--text-primary)}
.wl-balance.wl-low{color:var(--color-warning)}
.wl-actions{display:flex;gap:var(--space-2);justify-content:flex-end;align-items:center}
.wl-totals td{background:rgb(var(--accent-rgb) / 7%);font-weight:700;border-bottom:0}
.wl-pager{display:flex;align-items:center;justify-content:space-between;gap:var(--space-3);margin-top:var(--space-4);font-size:var(--font-size-sm);color:var(--text-secondary)}
.wl-pager div{display:flex;gap:var(--space-2)}
.wl-menu{position:relative}
.wl-menu__btn{all:unset;display:grid;place-items:center;width:34px;height:32px;border:1px solid var(--border-strong);border-radius:var(--radius-md);cursor:pointer;color:var(--text-secondary);font-weight:700}
.wl-menu__btn:hover,.wl-menu__btn:focus-visible{background:var(--surface-hover)}
.wl-menu__list{position:absolute;inset-inline-end:0;top:calc(100% + 4px);z-index:30;min-width:180px;padding:4px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);box-shadow:var(--shadow-lg);display:flex;flex-direction:column}
.wl-menu__item{all:unset;padding:8px 12px;border-radius:var(--radius-sm);font-size:var(--font-size-sm);cursor:pointer;white-space:nowrap}
.wl-menu__item:hover,.wl-menu__item:focus-visible{background:var(--surface-hover)}
.wl-cards-mobile{display:none}
@media(max-width:767px){
.wl-table-wrap{display:none}
.wl-cards-mobile{display:flex;flex-direction:column;gap:var(--space-3)}
.wl-mcard{display:flex;align-items:center;gap:var(--space-3);padding:var(--space-3) var(--space-4);border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card)}
.wl-mcard .wl-cust{min-width:0;flex:1}
.wl-mcard .wl-cust > div{min-width:0}
.wl-mcard .wl-cust__name,.wl-mcard .wl-cust__sub{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.wl-mcard__bal{text-align:right;font-variant-numeric:tabular-nums}
.wl-mcard__bal small{display:block;font-size:var(--font-size-xs);color:var(--text-muted);font-weight:400}
.wl-mtotals{padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 7%);font-size:var(--font-size-sm);font-weight:700;display:flex;justify-content:space-between}
}
.wl-dhead{align-items:flex-start}
.wl-dhead__title{display:flex;gap:var(--space-4);align-items:flex-start;min-width:0;flex:1}
.wl-back{all:unset;flex-shrink:0;display:grid;place-items:center;width:40px;height:40px;margin-top:4px;border:1px solid var(--border-strong);border-radius:var(--radius-md);background:var(--surface-card);cursor:pointer;color:var(--text-primary)}
.wl-back:hover,.wl-back:focus-visible{background:var(--surface-hover)}
.wl-profile{display:flex;align-items:center;justify-content:space-between;gap:var(--space-5);flex-wrap:wrap;margin-top:var(--space-5)}
.wl-profile__who{display:flex;align-items:center;gap:var(--space-4);min-width:0;flex:1 1 360px}
.wl-avatar--lg{width:56px;height:56px;font-size:var(--font-size-md)}
.wl-profile__name{font-family:var(--font-display);font-size:var(--font-size-xl);font-weight:800;color:var(--text-primary)}
.wl-profile .wl-cust__sub{font-size:var(--font-size-sm);margin-top:2px}
.wl-muted{color:var(--text-muted);font-style:italic}
.wl-profile__boxes{display:grid;grid-template-columns:repeat(3,minmax(170px,1fr));gap:var(--space-3);flex:1 1 560px}
@media(max-width:700px){.wl-profile__boxes{grid-template-columns:minmax(0,1fr)}}
.wl-box{padding:var(--space-3) var(--space-4);border:1px solid rgb(var(--accent-rgb) / 30%);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%)}
.wl-box__label{font-size:var(--font-size-xs);color:var(--text-secondary)}
.wl-box__value{font-family:var(--font-display);font-size:var(--font-size-xl);font-weight:800;font-variant-numeric:tabular-nums}
.wl-box--dark{background:var(--regalis-black);border-color:var(--regalis-black)}
.wl-box--dark .wl-box__label{color:rgb(255 255 255 / 70%)}
.wl-box--dark .wl-box__value{color:var(--regalis-white)}
.wl-box--dark .wl-box__value--low{color:var(--accent-bright)}
.wl-toolbar--custom{grid-template-columns:minmax(200px,1.4fr) repeat(3,minmax(140px,.8fr)) repeat(2,minmax(130px,.7fr))}
@media(max-width:1100px){.wl-toolbar--custom{grid-template-columns:repeat(2,minmax(0,1fr))}}
.wl-table--ledger tbody tr{cursor:default}
.wl-pill{display:inline-flex;padding:3px 10px;border-radius:var(--radius-full);font-size:var(--font-size-xs);font-weight:600;white-space:nowrap}
.wl-pill--success{background:color-mix(in srgb,var(--color-success) 14%,transparent);color:var(--color-success)}
.wl-pill--neutral{background:var(--surface-inset);color:var(--text-secondary)}
.wl-pill--info{background:color-mix(in srgb,var(--color-info) 12%,transparent);color:var(--color-info)}
.wl-pill--gold{background:rgb(var(--accent-rgb) / 18%);color:var(--regalis-gold-dark)}
.wl-pill--warning{background:color-mix(in srgb,var(--color-warning) 16%,transparent);color:color-mix(in srgb,var(--color-warning) 80%,black)}
.wl-verified{color:var(--color-success);font-weight:600}
.wl-mbox{display:flex;align-items:center;gap:var(--space-3);padding:var(--space-3) var(--space-4);border:1px solid rgb(var(--accent-rgb) / 30%);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%)}
.wl-mbox__bal{font-size:var(--font-size-lg);font-weight:800;font-variant-numeric:tabular-nums}
.wl-check{display:flex;align-items:center;gap:var(--space-2);font-size:var(--font-size-sm);cursor:pointer}
.wl-check input,.wl-radios input{width:17px;height:17px;accent-color:var(--accent)}
.wl-radios{border:0;margin:0;padding:0;display:flex;flex-direction:column;gap:6px}
.wl-radios > div{display:flex;gap:var(--space-5);flex-wrap:wrap}
.wl-radios label{display:flex;align-items:center;gap:var(--space-2);font-size:var(--font-size-sm);cursor:pointer}
.wl-after{padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 10%);border:1px solid rgb(var(--accent-rgb) / 25%);display:flex;flex-direction:column;gap:4px;font-size:var(--font-size-sm)}
.modal:has(.wl-modal){max-width:620px}
@media(max-width:767px){
.wl-tcard{display:flex;flex-direction:column;gap:6px;padding:var(--space-3) var(--space-4);border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);font-size:var(--font-size-sm)}
.wl-tcard__top{display:flex;justify-content:space-between;gap:var(--space-3)}
.wl-tcard__meta{display:flex;flex-wrap:wrap;gap:6px 12px;align-items:center;color:var(--text-muted);font-size:var(--font-size-xs)}
.wl-tcard .btn{align-self:flex-start}
.wl-dhead .page-header__actions{flex-wrap:wrap}
}
.wl-pick{max-height:320px;overflow-y:auto;display:flex;flex-direction:column;border:1px solid var(--border-subtle);border-radius:var(--radius-md)}
.wl-pick button{all:unset;display:flex;align-items:center;gap:var(--space-3);padding:10px 12px;border-bottom:1px solid var(--border-subtle);cursor:pointer}
.wl-pick button:last-child{border-bottom:0}
.wl-pick button:hover,.wl-pick button:focus-visible{background:var(--surface-hover)}
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

function useDebounced(value, delay = 350) {
  const [debounced, setDebounced] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setDebounced(value), delay);
    return () => clearTimeout(t);
  }, [value, delay]);
  return debounced;
}

function RowMenu({ label, items, trigger }) {
  const [open, setOpen] = React.useState(false);
  const ref = React.useRef(null);
  React.useEffect(() => {
    if (!open) return;
    const close = (event) => ref.current && !ref.current.contains(event.target) && setOpen(false);
    const key = (event) => event.key === "Escape" && setOpen(false);
    document.addEventListener("mousedown", close);
    document.addEventListener("keydown", key);
    return () => {
      document.removeEventListener("mousedown", close);
      document.removeEventListener("keydown", key);
    };
  }, [open]);
  return e.jsxs("div", {
    className: "wl-menu",
    ref,
    onClick: (event) => event.stopPropagation(),
    children: [
      trigger
        ? e.jsx(Button, { variant: "outline", "aria-haspopup": "menu", "aria-expanded": open, onClick: () => setOpen((o) => !o), children: trigger })
        : e.jsx("button", { type: "button", className: "wl-menu__btn", "aria-haspopup": "menu", "aria-expanded": open, "aria-label": label, onClick: () => setOpen((o) => !o), children: "⋯" }),
      open &&
        e.jsx("div", {
          className: "wl-menu__list",
          role: "menu",
          children: items.filter(Boolean).map((item) =>
            e.jsx("button", { type: "button", role: "menuitem", className: "wl-menu__item", onClick: () => { setOpen(false); item.onClick(); }, children: item.label }, item.label)
          )
        })
    ]
  });
}

function StatCard({ label, value, sub, tone }) {
  return e.jsxs("div", {
    className: "wl-card",
    children: [
      e.jsx("div", { className: "wl-card__label", children: label }),
      e.jsx("div", { className: `wl-card__value${tone ? ` ${tone}` : ""}`, children: value }),
      e.jsx("div", { className: "wl-card__sub", children: sub })
    ]
  });
}

/** "Add money" from the list: pick the customer, then go to their wallet with the form open. */
function PickCustomerModal({ onClose, onPick }) {
  const [query, setQuery] = React.useState("");
  const debounced = useDebounced(query, 250);
  const [rows, setRows] = React.useState(null);
  React.useEffect(() => {
    walletsApi.list({ search: debounced.trim() || undefined, sort: "name", pageSize: 20 }).then((r) => setRows(r.wallets.items)).catch(() => setRows([]));
  }, [debounced]);
  return e.jsx(Modal, {
    open: true,
    title: "Add money — choose a customer",
    onClose,
    footer: e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsxs("div", { className: "wl-search", children: [e.jsx(IconSearch, { size: 16 }), e.jsx("input", { className: "field__input", autoFocus: true, "aria-label": "Search customers", placeholder: "Search name, email or mobile…", value: query, onChange: (event) => setQuery(event.target.value) })] }),
        rows === null
          ? e.jsx(Spinner, { center: true })
          : rows.length === 0
            ? e.jsx("p", { className: "wl-cust__sub", children: "No customer wallet matches." })
            : e.jsx("div", {
                className: "wl-pick",
                children: rows.map((r) =>
                  e.jsxs("button", { type: "button", onClick: () => onPick(r), children: [e.jsx("span", { className: "wl-avatar", children: initials(r.fullName) }), e.jsxs("span", { style: { flex: 1 }, children: [e.jsx("div", { className: "wl-cust__name", children: r.fullName }), e.jsx("div", { className: "wl-cust__sub", children: r.email })] }), e.jsx("span", { className: "wl-num", children: fmtMoney(r.balance) })] }, r.customerId)
                )
              })
      ]
    })
  });
}

// ── Screen 1: every customer wallet ─────────────────────────────────────────

function AdminWalletPage() {
  useStyles();
  const toast = useToast();
  const navigate = useNavigate();
  const canEdit = usePermission("WALLET.EDIT");
  const [search, setSearch] = React.useState("");
  const [filter, setFilter] = React.useState("all");
  const [sort, setSort] = React.useState("balance-desc");
  const [activity, setActivity] = React.useState("all");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [picking, setPicking] = React.useState(false);
  const [exporting, setExporting] = React.useState(false);
  const debouncedSearch = useDebounced(search);

  const params = { search: debouncedSearch.trim() || undefined, filter, sort, activity, page, pageSize: 20 };
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setData(await walletsApi.list(params));
    } catch (error) {
      toast.error(errorText(error, "Failed to load customer wallets."));
    } finally {
      setLoading(false);
    }
  }, [debouncedSearch, filter, sort, activity, page]);
  React.useEffect(() => {
    load();
  }, [load]);

  const change = (setter) => (event) => {
    setter(event.target.value);
    setPage(1);
  };
  const open = (row, action) => navigate(`${WALLET}/${row.customerId}${action ? `?${action}=1` : ""}`);
  const exportList = async () => {
    setExporting(true);
    try {
      await walletsApi.exportCsv({ ...params, page: undefined, pageSize: undefined });
    } catch (error) {
      toast.error(errorText(error, "Could not export the list."));
    } finally {
      setExporting(false);
    }
  };

  const summary = data?.summary;
  const threshold = summary?.lowBalanceThreshold ?? 100;
  const result = data?.wallets;
  const items = result?.items ?? [];
  const totals = data?.filteredTotals;
  const narrowed = filter !== "all" || activity !== "all" || debouncedSearch.trim();

  const lastActivity = (r) => (r.lastActivityUtc ? fmtDate(r.lastActivityUtc) : e.jsx("span", { className: "wl-cust__sub", children: "No activity" }));
  const customer = (r) =>
    e.jsxs("div", {
      className: "wl-cust",
      children: [
        e.jsx("span", { className: "wl-avatar", "aria-hidden": true, children: initials(r.fullName) }),
        e.jsxs("div", { children: [e.jsx("div", { className: "wl-cust__name", children: r.fullName }), e.jsx("div", { className: "wl-cust__sub", title: [r.email, r.mobileNumber].filter(Boolean).join(" · "), children: [r.email, r.mobileNumber].filter(Boolean).join(" · ") })] })
      ]
    });
  const actions = (r) =>
    e.jsxs("div", {
      className: "wl-actions",
      onClick: (event) => event.stopPropagation(),
      children: [
        e.jsx(Button, { size: "sm", onClick: () => open(r), "aria-label": `View wallet of ${r.fullName}`, children: "View" }),
        e.jsx(RowMenu, {
          label: `More actions for ${r.fullName}`,
          items: [
            { label: "View wallet", onClick: () => open(r) },
            canEdit && { label: "Add money", onClick: () => open(r, "add") },
            canEdit && { label: "Adjust balance", onClick: () => open(r, "adjust") },
            { label: "Export statement", onClick: () => open(r, "export") }
          ]
        })
      ]
    });

  let body;
  if (loading && !data) body = e.jsx(Spinner, { center: true });
  else if (items.length === 0)
    body = e.jsx(EmptyState, {
      title: narrowed ? "No wallets match" : "No customer wallets yet",
      description: narrowed ? "Try a different search or filter." : "A wallet appears here as soon as a customer account is created."
    });
  else
    body = e.jsxs("div", {
      style: { opacity: loading ? 0.6 : 1 },
      children: [
        e.jsx("div", {
          className: "wl-table-wrap",
          children: e.jsxs("table", {
            className: "wl-table",
            children: [
              e.jsx("thead", {
                children: e.jsxs("tr", {
                  children: [
                    e.jsx("th", { children: "Customer" }),
                    e.jsx("th", { className: "wl-num", children: "Total incoming" }),
                    e.jsx("th", { className: "wl-num", children: "Total spent" }),
                    e.jsx("th", { className: "wl-num", children: "Current balance" }),
                    e.jsx("th", { children: "Last activity" }),
                    e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Actions" }) })
                  ]
                })
              }),
              e.jsxs("tbody", {
                children: [
                  ...items.map((r) =>
                    e.jsxs(
                      "tr",
                      {
                        onClick: () => open(r),
                        children: [
                          e.jsx("td", { children: customer(r) }),
                          e.jsx("td", { className: "wl-num wl-in", children: plus(r.totalCredit) }),
                          e.jsx("td", { className: "wl-num wl-out", children: minus(r.totalDebit) }),
                          e.jsx("td", { className: `wl-num wl-balance${r.isLowBalance ? " wl-low" : ""}`, title: r.isLowBalance ? `Low balance — under ${fmtMoney(threshold)}` : undefined, children: fmtMoney(r.balance) }),
                          e.jsx("td", { style: { whiteSpace: "nowrap" }, children: lastActivity(r) }),
                          e.jsx("td", { children: actions(r) })
                        ]
                      },
                      r.customerId
                    )
                  ),
                  totals &&
                    e.jsxs("tr", {
                      className: "wl-totals",
                      onClick: undefined,
                      style: { cursor: "default" },
                      children: [
                        e.jsx("td", { children: `Totals (${totals.customerCount} customer${totals.customerCount === 1 ? "" : "s"})` }),
                        e.jsx("td", { className: "wl-num wl-in", children: plus(totals.totalCredit) }),
                        e.jsx("td", { className: "wl-num wl-out", children: minus(totals.totalDebit) }),
                        e.jsx("td", { className: "wl-num wl-balance", children: fmtMoney(totals.balance) }),
                        e.jsx("td", {}),
                        e.jsx("td", {})
                      ]
                    })
                ]
              })
            ]
          })
        }),
        e.jsxs("div", {
          className: "wl-cards-mobile",
          children: [
            ...items.map((r) =>
              e.jsxs("div", {
                className: "wl-mcard",
                children: [
                  customer(r),
                  e.jsxs("div", { className: "wl-mcard__bal", children: [e.jsx("div", { className: `wl-balance${r.isLowBalance ? " wl-low" : ""}`, children: fmtMoney(r.balance) }), e.jsx("small", { children: "balance" })] }),
                  e.jsx(Button, { size: "sm", onClick: () => open(r), children: "View" })
                ]
              }, r.customerId)
            ),
            totals && e.jsxs("div", { className: "wl-mtotals", children: [e.jsx("span", { children: `Totals (${totals.customerCount})` }), e.jsx("span", { children: fmtMoney(totals.balance) })] })
          ]
        }),
        e.jsxs("div", {
          className: "wl-pager",
          children: [
            e.jsxs("span", { children: ["Page ", result.page, " of ", Math.max(1, result.totalPages), " · ", result.totalCount, " customer", result.totalCount === 1 ? "" : "s"] }),
            e.jsxs("div", {
              children: [
                e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasPrevious || loading, onClick: () => setPage(result.page - 1), "aria-label": "Previous page", children: e.jsx(IconChevronLeft, { size: 16 }) }),
                e.jsx(Button, { variant: "outline", size: "sm", disabled: !result.hasNext || loading, onClick: () => setPage(result.page + 1), "aria-label": "Next page", children: e.jsx(IconChevronRight, { size: 16 }) })
              ]
            })
          ]
        })
      ]
    });

  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("header", {
        className: "page-header",
        children: [
          e.jsxs("div", {
            children: [
              e.jsx("h2", { className: "page-header__title", children: "Customer wallets" }),
              e.jsx("p", { className: "page-header__description", children: "Every customer with money in their wallet. The ledger is append-only and self-checking: credits − debits always equal the balance." })
            ]
          }),
          e.jsxs("div", {
            className: "page-header__actions",
            children: [
              e.jsx(Button, { variant: "outline", onClick: exportList, loading: exporting, disabled: exporting || !items.length, children: "Export" }),
              canEdit && e.jsx(Button, { onClick: () => setPicking(true), children: "+ Add money" })
            ]
          })
        ]
      }),
      e.jsxs("div", {
        className: "wl-cards",
        children: [
          e.jsx(StatCard, { label: "Wallet balance held", value: summary ? fmtMoney(summary.balanceHeld) : "—", sub: summary ? `Across ${summary.customerCount} customer${summary.customerCount === 1 ? "" : "s"}` : "" }),
          e.jsx(StatCard, { label: "Total recharged", tone: "wl-in", value: summary ? fmtMoney(summary.totalRecharged) : "—", sub: "All time" }),
          e.jsx(StatCard, { label: "Total spent", tone: "wl-out", value: summary ? fmtMoney(summary.totalSpent) : "—", sub: "All time" }),
          e.jsx(StatCard, { label: "Low balance", tone: "wl-low", value: summary ? String(summary.lowBalanceCount) : "—", sub: `Under ${fmtMoney(threshold).replace(".00", "")}` })
        ]
      }),
      e.jsxs("section", {
        className: "wl-panel",
        "aria-label": "Customer wallets",
        children: [
          e.jsxs("div", {
            className: "wl-toolbar",
            role: "search",
            children: [
              e.jsxs("div", { className: "wl-search", children: [e.jsx(IconSearch, { size: 16 }), e.jsx("input", { className: "field__input", "aria-label": "Search name, email or mobile", placeholder: "Search name, email or mobile…", value: search, onChange: (event) => { setSearch(event.target.value); setPage(1); } })] }),
              e.jsx(Select, { label: "Show", value: filter, options: FILTERS, onChange: change(setFilter) }),
              e.jsx(Select, { label: "Sort", value: sort, options: SORTS, onChange: change(setSort) }),
              e.jsx(Select, { label: "Activity", value: activity, options: ACTIVITY, onChange: change(setActivity) })
            ]
          }),
          body
        ]
      }),
      picking && e.jsx(PickCustomerModal, { onClose: () => setPicking(false), onPick: (r) => { setPicking(false); open(r, "add"); } })
    ]
  });
}

// ── Screen 2: one customer's wallet ─────────────────────────────────────────

const CATEGORY = {
  1: ["Recharge", "success"],
  2: ["Booking", "neutral"],
  3: ["Refund", "info"],
  4: ["Day pass", "gold"],
  5: ["Membership", "gold"],
  6: ["Service", "neutral"],
  7: ["Deposit", "neutral"],
  8: ["Adjustment", "warning"],
  9: ["Refund", "info"]
};
const DIRECTIONS = [
  { value: "", label: "All transactions" },
  { value: "credit", label: "Credits only" },
  { value: "debit", label: "Debits only" }
];
const CATEGORIES = [
  { value: "", label: "All categories" },
  { value: "recharge", label: "Recharge" },
  { value: "booking", label: "Booking" },
  { value: "refund", label: "Refund" },
  { value: "day-pass", label: "Day pass" },
  { value: "monthly-pass", label: "Membership" },
  { value: "service", label: "Service" },
  { value: "deposit", label: "Deposit" },
  { value: "adjustment", label: "Adjustment" }
];
const PERIODS = [
  { value: "all", label: "All time" },
  { value: "this-month", label: "This month" },
  { value: "last-month", label: "Last month" },
  { value: "30d", label: "Last 30 days" },
  { value: "90d", label: "Last 90 days" },
  { value: "custom", label: "Custom range…" }
];
// PaymentMethod values on the API: 2 UPI, 3 credit card, 4 debit card, 5 bank transfer, 6 cash, 7 other.
const PAY_MODES = [
  { value: "2", label: "UPI" },
  { value: "6", label: "Cash" },
  { value: "4", label: "Debit card" },
  { value: "3", label: "Credit card" },
  { value: "5", label: "Bank transfer (NEFT / IMPS / RTGS)" },
  { value: "7", label: "Other" }
];
const ADJUST_REASONS = ["Booking correction", "Goodwill credit", "Duplicate charge reversal", "Refund correction", "Opening balance", "Other"];

const istToday = () => new Date(Date.now() + 5.5 * 3600e3).toISOString().slice(0, 10);
const shiftDays = (iso, days) => new Date(Date.parse(`${iso}T00:00:00Z`) + days * 864e5).toISOString().slice(0, 10);
function periodRange(period, custom) {
  const today = istToday();
  const [y, m] = today.split("-").map(Number);
  const first = (yy, mm) => `${yy}-${String(mm).padStart(2, "0")}-01`;
  switch (period) {
    case "this-month": return { from: first(y, m), to: today };
    case "last-month": {
      const py = m === 1 ? y - 1 : y, pm = m === 1 ? 12 : m - 1;
      return { from: first(py, pm), to: shiftDays(first(y, m), -1) };
    }
    case "30d": return { from: shiftDays(today, -29), to: today };
    case "90d": return { from: shiftDays(today, -89), to: today };
    case "custom": return { from: custom.from || undefined, to: custom.to || undefined };
    default: return {};
  }
}

/** Money typed by a person: up to 2 decimals, positive; returns paise or null. */
function paiseOf(text) {
  const t = String(text ?? "").trim().replace(/,/g, "");
  const hit = /^(\d+)(?:\.(\d{0,2}))?$/.exec(t);
  if (!hit) return null;
  return Number(hit[1]) * 100 + Number((hit[2] ?? "").padEnd(2, "0") || "0");
}

function CustomerBox({ wallet }) {
  return e.jsxs("div", {
    className: "wl-mbox",
    children: [
      e.jsx("span", { className: "wl-avatar", "aria-hidden": true, children: initials(wallet.customer.fullName) }),
      e.jsxs("div", { style: { flex: 1, minWidth: 0 }, children: [e.jsx("div", { className: "wl-cust__name", children: wallet.customer.fullName }), e.jsx("div", { className: "wl-cust__sub", children: wallet.customer.email })] }),
      e.jsxs("div", { style: { textAlign: "right" }, children: [e.jsx("div", { className: "wl-cust__sub", children: "Current balance" }), e.jsx("div", { className: "wl-mbox__bal", children: fmtMoney(wallet.balance) })] })
    ]
  });
}

function AddMoneyModal({ wallet, onClose, onDone }) {
  const toast = useToast();
  const [amount, setAmount] = React.useState("");
  const [mode, setMode] = React.useState("2");
  const [reference, setReference] = React.useState("");
  const [receivedOn, setReceivedOn] = React.useState(istToday());
  const [narration, setNarration] = React.useState(`Wallet recharge ${wallet.nextPaymentNumber}`);
  const [notify, setNotify] = React.useState(false);
  const [busy, setBusy] = React.useState(false);
  const [errors, setErrors] = React.useState({});
  const submit = async () => {
    const errs = {};
    const p = paiseOf(amount);
    if (p === null || p <= 0) errs.amount = "Enter an amount, up to 2 decimals.";
    else if (p > 100000000) errs.amount = "At most ₹10,00,000 in one entry.";
    if (!receivedOn) errs.receivedOn = "Enter the date the money was received.";
    else if (receivedOn > istToday()) errs.receivedOn = "Cannot be in the future.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      const result = await walletsApi.addMoney(wallet.customer.customerId, {
        amount: p / 100,
        paymentMethod: Number(mode),
        reference: reference.trim() || null,
        receivedOn,
        narration: narration.trim() || null,
        notifyCustomer: notify,
        expectedPaymentNumber: wallet.nextPaymentNumber
      });
      toast.success(`${fmtMoney(p / 100)} added (${result.paymentNumber}). New balance ${fmtMoney(result.balance)}.`);
      if (result.notificationNote) toast.success(result.notificationNote);
      onDone();
    } catch (error) {
      if (error instanceof ApiError) {
        const fieldErrs = {};
        for (const err of error.errors ?? []) if (err.field) fieldErrs[err.field[0].toLowerCase() + err.field.slice(1)] = err.message;
        setErrors(fieldErrs);
      }
      toast.error(errorText(error, "Could not add the money."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Add money to wallet — ${wallet.customer.fullName}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Add money" })] }),
    children: e.jsxs("div", {
      className: "form-stack wl-modal",
      children: [
        e.jsx(CustomerBox, { wallet }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Input, { label: "Amount", required: true, inputMode: "decimal", placeholder: "0.00", value: amount, error: errors.amount, onChange: (ev) => setAmount(ev.target.value), autoFocus: true }),
            e.jsx(Select, { label: "Payment mode", required: true, value: mode, options: PAY_MODES, onChange: (ev) => setMode(ev.target.value) }),
            e.jsx(Input, { label: "Reference / UTR", placeholder: "Payment reference", maxLength: 80, value: reference, error: errors.reference, onChange: (ev) => setReference(ev.target.value) }),
            e.jsx(Input, { label: "Received on", required: true, type: "date", max: istToday(), value: receivedOn, error: errors.receivedOn, onChange: (ev) => setReceivedOn(ev.target.value) })
          ]
        }),
        e.jsx(Input, { label: "Narration", maxLength: 240, value: narration, error: errors.narration, hint: "This text appears on the customer's wallet statement.", onChange: (ev) => setNarration(ev.target.value) }),
        e.jsxs("label", { className: "wl-check", children: [e.jsx("input", { type: "checkbox", checked: notify, onChange: (ev) => setNotify(ev.target.checked) }), "Notify the customer and send the receipt"] }),
        e.jsx("p", { className: "wl-cust__sub", style: { margin: 0 }, children: "Records a payment and credits the wallet in one step, so the credit always has a payment behind it." })
      ]
    })
  });
}

function AdjustModal({ wallet, onClose, onDone }) {
  const toast = useToast();
  const [direction, setDirection] = React.useState("");
  const [amount, setAmount] = React.useState("");
  const [reason, setReason] = React.useState("Booking correction");
  const [narration, setNarration] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const [errors, setErrors] = React.useState({});
  const p = paiseOf(amount);
  const balancePaise = Math.round(wallet.balance * 100);
  const after = direction && p !== null ? (direction === "credit" ? balancePaise + p : balancePaise - p) : null;
  const tooMuch = direction === "debit" && p !== null && p > balancePaise;
  const submit = async () => {
    const errs = {};
    if (!direction) errs.direction = "Choose credit or debit.";
    if (p === null || p <= 0) errs.amount = "Enter an amount, up to 2 decimals.";
    else if (tooMuch) errs.amount = `A debit cannot take the balance below zero (max ${fmtMoney(wallet.balance)}).`;
    if (!reason.trim()) errs.reason = "Give a reason — it goes on the audit trail.";
    if (!narration.trim()) errs.narration = "Enter the text the customer will see.";
    setErrors(errs);
    if (Object.keys(errs).length) return;
    setBusy(true);
    try {
      await walletsApi.adjust(wallet.customer.customerId, { direction, amount: p / 100, reason: reason.trim(), narration: narration.trim() });
      toast.success("Adjustment posted. It is on the ledger with your name against it.");
      onDone();
    } catch (error) {
      toast.error(errorText(error, "The adjustment was not posted."));
    } finally {
      setBusy(false);
    }
  };
  return e.jsx(Modal, {
    open: true,
    title: `Adjust balance — ${wallet.customer.fullName}`,
    onClose,
    footer: e.jsxs(e.Fragment, { children: [e.jsx(Button, { variant: "outline", onClick: onClose, disabled: busy, children: "Cancel" }), e.jsx(Button, { onClick: submit, loading: busy, disabled: busy, children: "Post adjustment" })] }),
    children: e.jsxs("div", {
      className: "form-stack wl-modal",
      children: [
        e.jsxs("fieldset", {
          className: "wl-radios",
          children: [
            e.jsxs("legend", { className: "field__label", children: ["Adjustment type", e.jsx("span", { className: "field__required", "aria-hidden": true, children: " *" })] }),
            e.jsxs("div", {
              children: [
                e.jsxs("label", { children: [e.jsx("input", { type: "radio", name: "wl-dir", checked: direction === "credit", onChange: () => setDirection("credit") }), "Credit (add money)"] }),
                e.jsxs("label", { children: [e.jsx("input", { type: "radio", name: "wl-dir", checked: direction === "debit", onChange: () => setDirection("debit") }), "Debit (deduct money)"] })
              ]
            }),
            errors.direction && e.jsx("span", { className: "field__error", role: "alert", children: errors.direction })
          ]
        }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Input, { label: "Amount", required: true, inputMode: "decimal", placeholder: "0.00", value: amount, error: errors.amount ?? (tooMuch ? `More than the balance of ${fmtMoney(wallet.balance)}.` : undefined), hint: "A debit cannot take the balance below zero.", onChange: (ev) => setAmount(ev.target.value) }),
            e.jsx(Input, { label: "Reason", required: true, list: "wl-reasons", maxLength: 120, value: reason, error: errors.reason, onChange: (ev) => setReason(ev.target.value) })
          ]
        }),
        e.jsx("datalist", { id: "wl-reasons", children: ADJUST_REASONS.map((r) => e.jsx("option", { value: r }, r)) }),
        e.jsx(Input, { label: "Narration", required: true, maxLength: 200, placeholder: "Shown to the customer on their statement", value: narration, error: errors.narration, onChange: (ev) => setNarration(ev.target.value) }),
        e.jsxs("div", {
          className: "wl-after",
          children: [
            e.jsxs("div", { children: ["Balance after this adjustment: ", e.jsx("strong", { className: after !== null && after < 0 ? "wl-out" : undefined, children: after === null ? fmtMoney(wallet.balance) : fmtMoney(after / 100) })] }),
            e.jsx("div", { className: "wl-cust__sub", children: "The ledger is append-only. This adds a new entry with your name against it; nothing is edited or deleted." })
          ]
        })
      ]
    })
  });
}

function AdminWalletDetailPage() {
  useStyles();
  const { customerId } = useParams();
  const toast = useToast();
  const navigate = useNavigate();
  const canEdit = usePermission("WALLET.EDIT");
  const [search, setSearch] = React.useState("");
  const [direction, setDirection] = React.useState("");
  const [category, setCategory] = React.useState("");
  const [period, setPeriod] = React.useState("all");
  const [custom, setCustom] = React.useState({ from: "", to: "" });
  const [page, setPage] = React.useState(1);
  const [wallet, setWallet] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [missing, setMissing] = React.useState(false);
  const [modal, setModal] = React.useState(null);
  const debouncedSearch = useDebounced(search);
  const range = periodRange(period, custom);
  const intent = React.useRef(new URLSearchParams(window.location.search));

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const data = await walletsApi.get(customerId, { search: debouncedSearch.trim() || undefined, direction: direction || undefined, category: category || undefined, from: range.from, to: range.to, page, pageSize: 20 });
      setWallet(data);
      window.dispatchEvent(new CustomEvent("rg:crumb", { detail: { path: window.location.pathname, label: data.customer.fullName } }));
    } catch (error) {
      if (error instanceof ApiError && /not found/i.test(error.message)) setMissing(true);
      else toast.error(errorText(error, "Failed to load the wallet."));
    } finally {
      setLoading(false);
    }
  }, [customerId, debouncedSearch, direction, category, range.from, range.to, page]);
  React.useEffect(() => {
    load();
  }, [load]);

  const exportStatement = async (format) => {
    try {
      await walletsApi.statement(customerId, { from: range.from, to: range.to }, format);
    } catch (error) {
      toast.error(errorText(error, "Could not export the statement."));
    }
  };
  // Actions chosen from the list's ⋯ menu arrive as ?add=1, ?adjust=1 or ?export=1.
  React.useEffect(() => {
    if (!wallet) return;
    const q = intent.current;
    if (q.get("add") && canEdit) setModal("add");
    else if (q.get("adjust") && canEdit) setModal("adjust");
    else if (q.get("export")) exportStatement("pdf");
    if (q.toString()) {
      intent.current = new URLSearchParams();
      window.history.replaceState(null, "", window.location.pathname);
    }
  }, [wallet]);

  if (missing)
    return e.jsx("section", { className: "wl-panel", children: e.jsx(EmptyState, { title: "No such customer wallet", description: "The customer may have been removed.", action: e.jsx(Button, { variant: "outline", onClick: () => navigate(WALLET), children: "Back to all wallets" }) }) });
  if (!wallet) return e.jsx(Spinner, { center: true });

  const c = wallet.customer;
  const tx = wallet.transactions;
  const filtered = debouncedSearch.trim() || direction || category || period !== "all";
  const membership = c.membershipName
    ? e.jsxs("span", { children: ["Membership: ", e.jsx("strong", { children: c.membershipName }), c.membershipValidTo ? ` · valid till ${fmtDate(c.membershipValidTo)}` : ""] })
    : e.jsx("span", { className: "wl-muted", children: "No active membership" });
  const categoryPill = (t) => {
    const [label, tone] = CATEGORY[t.category] ?? ["Other", "neutral"];
    return e.jsx("span", { className: `wl-pill wl-pill--${tone}`, children: label });
  };
  const amount = (t) => e.jsx("span", { className: t.direction === 1 ? "wl-in" : "wl-out", style: { fontWeight: 700 }, children: t.direction === 1 ? plus(t.amount) : minus(t.amount) });
  const receipt = (t) =>
    t.receiptPaymentId
      ? e.jsx(Button, { size: "sm", variant: "outline", onClick: () => walletsApi.receipt(t.receiptPaymentId, t.paymentNumber).catch((error) => toast.error(errorText(error, "Could not download the receipt."))), "aria-label": `Receipt for ${t.transactionNumber}`, children: "Receipt" })
      : null;
  const narration = (t) =>
    e.jsxs("div", { children: [e.jsx("div", { children: t.narration }), t.performedByName && e.jsxs("div", { className: "wl-cust__sub", children: ["by ", t.performedByName] })] });

  let ledger;
  if (loading && !tx) ledger = e.jsx(Spinner, { center: true });
  else if (tx.items.length === 0)
    ledger = e.jsx(EmptyState, { title: filtered ? "No transactions match" : "No transactions yet", description: filtered ? "Try a different search, type or period." : "Recharges, bookings, refunds and adjustments will appear here." });
  else
    ledger = e.jsxs("div", {
      style: { opacity: loading ? 0.6 : 1 },
      children: [
        e.jsx("div", {
          className: "wl-table-wrap",
          children: e.jsxs("table", {
            className: "wl-table wl-table--ledger",
            children: [
              e.jsx("thead", { children: e.jsxs("tr", { children: [e.jsx("th", { children: "Txn" }), e.jsx("th", { children: "Narration" }), e.jsx("th", { children: "Category" }), e.jsx("th", { className: "wl-num", children: "Amount" }), e.jsx("th", { className: "wl-num", children: "Balance" }), e.jsx("th", { children: "When (IST)" }), e.jsx("th", { children: e.jsx("span", { className: "sr-only", children: "Receipt" }) })] }) }),
              e.jsx("tbody", {
                children: tx.items.map((t) =>
                  e.jsxs("tr", {
                    children: [
                      e.jsx("td", { style: { whiteSpace: "nowrap", fontWeight: 600 }, children: t.transactionNumber }),
                      e.jsx("td", { children: narration(t) }),
                      e.jsx("td", { children: categoryPill(t) }),
                      e.jsx("td", { className: "wl-num", children: amount(t) }),
                      e.jsx("td", { className: "wl-num", children: fmtMoney(t.balanceAfter) }),
                      e.jsx("td", { style: { whiteSpace: "nowrap" }, children: fmtDateTime(t.transactionUtc) }),
                      e.jsx("td", { style: { textAlign: "right" }, children: receipt(t) })
                    ]
                  }, t.walletTransactionId)
                )
              })
            ]
          })
        }),
        e.jsx("div", {
          className: "wl-cards-mobile",
          children: tx.items.map((t) =>
            e.jsxs("div", {
              className: "wl-tcard",
              children: [
                e.jsxs("div", { className: "wl-tcard__top", children: [e.jsx("strong", { children: t.transactionNumber }), amount(t)] }),
                e.jsx("div", { children: t.narration }),
                e.jsxs("div", { className: "wl-tcard__meta", children: [categoryPill(t), e.jsx("span", { children: fmtDateTime(t.transactionUtc) }), e.jsxs("span", { children: ["Bal ", fmtMoney(t.balanceAfter)] })] }),
                receipt(t)
              ]
            }, t.walletTransactionId)
          )
        })
      ]
    });

  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("header", {
        className: "page-header wl-dhead",
        children: [
          e.jsxs("div", {
            className: "wl-dhead__title",
            children: [
              e.jsx("button", { type: "button", className: "wl-back", "aria-label": "Back to all wallets", onClick: () => navigate(WALLET), children: e.jsx(IconChevronLeft, { size: 18 }) }),
              e.jsxs("div", { children: [e.jsxs("h2", { className: "page-header__title", children: ["Wallet — ", c.fullName] }), e.jsx("p", { className: "page-header__description", children: "Every credit and debit on this wallet, newest first. Entries can never be edited or deleted, only corrected by a new adjustment." })] })
            ]
          }),
          e.jsxs("div", {
            className: "page-header__actions",
            children: [
              e.jsx(RowMenu, { label: "Export statement", trigger: "Export statement", items: [{ label: "Statement (PDF)", onClick: () => exportStatement("pdf") }, { label: "Statement (CSV)", onClick: () => exportStatement("csv") }] }),
              canEdit && e.jsx(Button, { variant: "outline", onClick: () => setModal("adjust"), children: "Adjust balance" }),
              canEdit && e.jsx(Button, { onClick: () => setModal("add"), children: "+ Add money" })
            ]
          })
        ]
      }),
      e.jsxs("section", {
        className: "wl-panel wl-profile",
        "aria-label": "Customer",
        children: [
          e.jsxs("div", {
            className: "wl-profile__who",
            children: [
              e.jsx("span", { className: "wl-avatar wl-avatar--lg", "aria-hidden": true, children: initials(c.fullName) }),
              e.jsxs("div", {
                style: { minWidth: 0 },
                children: [
                  e.jsx("div", { className: "wl-profile__name", children: c.fullName }),
                  e.jsx("div", { className: "wl-cust__sub", children: [c.email, c.mobileNumber, `Customer since ${fmtDate(c.customerSinceUtc)}`].filter(Boolean).join(" · ") }),
                  e.jsxs("div", { className: "wl-cust__sub", children: [membership, c.walletOpenedUtc ? ` · Wallet opened ${fmtDate(c.walletOpenedUtc)}` : ""] })
                ]
              })
            ]
          }),
          e.jsxs("div", {
            className: "wl-profile__boxes",
            children: [
              e.jsxs("div", { className: "wl-box", children: [e.jsx("div", { className: "wl-box__label", children: "Total incoming" }), e.jsx("div", { className: "wl-box__value wl-in", children: plus(wallet.totalCredit) })] }),
              e.jsxs("div", { className: "wl-box", children: [e.jsx("div", { className: "wl-box__label", children: "Total spent" }), e.jsx("div", { className: "wl-box__value wl-out", children: minus(wallet.totalDebit) })] }),
              e.jsxs("div", { className: "wl-box wl-box--dark", children: [e.jsx("div", { className: "wl-box__label", children: wallet.isLowBalance ? `Current balance · low (under ${fmtMoney(wallet.lowBalanceThreshold).replace(".00", "")})` : "Current balance" }), e.jsx("div", { className: `wl-box__value${wallet.isLowBalance ? " wl-box__value--low" : ""}`, children: fmtMoney(wallet.balance) })] })
            ]
          })
        ]
      }),
      e.jsxs("section", {
        className: "wl-panel",
        "aria-label": "Transactions",
        style: { marginTop: "var(--space-5)" },
        children: [
          e.jsxs("div", {
            className: `wl-toolbar${period === "custom" ? " wl-toolbar--custom" : ""}`,
            role: "search",
            children: [
              e.jsxs("div", { className: "wl-search", children: [e.jsx(IconSearch, { size: 16 }), e.jsx("input", { className: "field__input", "aria-label": "Search transaction ID or narration", placeholder: "Search transaction ID or narration…", value: search, onChange: (ev) => { setSearch(ev.target.value); setPage(1); } })] }),
              e.jsx(Select, { label: "Type", value: direction, options: DIRECTIONS, onChange: (ev) => { setDirection(ev.target.value); setPage(1); } }),
              e.jsx(Select, { label: "Category", value: category, options: CATEGORIES, onChange: (ev) => { setCategory(ev.target.value); setPage(1); } }),
              e.jsx(Select, { label: "Period", value: period, options: PERIODS, onChange: (ev) => { setPeriod(ev.target.value); setPage(1); } }),
              period === "custom" && e.jsx(Input, { label: "From", type: "date", max: custom.to || istToday(), value: custom.from, onChange: (ev) => { setCustom((x) => ({ ...x, from: ev.target.value })); setPage(1); } }),
              period === "custom" && e.jsx(Input, { label: "To", type: "date", min: custom.from || undefined, max: istToday(), value: custom.to, onChange: (ev) => { setCustom((x) => ({ ...x, to: ev.target.value })); setPage(1); } })
            ]
          }),
          ledger,
          tx &&
            e.jsxs("div", {
              className: "wl-pager",
              children: [
                e.jsxs("span", {
                  children: [
                    "Page ", tx.page, " of ", Math.max(1, tx.totalPages), " · ", tx.totalCount, " record", tx.totalCount === 1 ? "" : "s",
                    " · Credits ", fmtMoney(wallet.totalCredit), " − debits ", fmtMoney(wallet.totalDebit), " = balance ", fmtMoney(wallet.balance),
                    wallet.ledgerVerified
                      ? e.jsx("span", { className: "wl-verified", title: "The stored wallet balance equals the ledger total", children: " ✓ verified" })
                      : e.jsx("span", { className: "wl-out", title: "The stored balance differs from the ledger — contact support", children: " ⚠ stored balance differs" })
                  ]
                }),
                e.jsxs("div", {
                  children: [
                    e.jsx(Button, { variant: "outline", size: "sm", disabled: !tx.hasPrevious || loading, onClick: () => setPage(tx.page - 1), "aria-label": "Previous page", children: e.jsx(IconChevronLeft, { size: 16 }) }),
                    e.jsx(Button, { variant: "outline", size: "sm", disabled: !tx.hasNext || loading, onClick: () => setPage(tx.page + 1), "aria-label": "Next page", children: e.jsx(IconChevronRight, { size: 16 }) })
                  ]
                })
              ]
            })
        ]
      }),
      modal === "add" && e.jsx(AddMoneyModal, { wallet, onClose: () => setModal(null), onDone: () => { setModal(null); setPage(1); load(); } }),
      modal === "adjust" && e.jsx(AdjustModal, { wallet, onClose: () => setModal(null), onDone: () => { setModal(null); setPage(1); load(); } })
    ]
  });
}

export { AdminWalletPage, AdminWalletDetailPage };
