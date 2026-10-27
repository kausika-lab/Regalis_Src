/**
 * Admin -> Customers.
 *
 *   AdminCustomersPage       the member list
 *   AdminCustomerDetailPage  the full customer profile: identity, the details the
 *                            member filled in on the customer portal, and their
 *                            activity across bookings, money, wallet and support.
 *
 * This repo ships the built SPA only, so this chunk is hand-maintained: keep the
 * file name (the route in index-DI5X4c9f.js imports it by name) and bump its
 * revision in sw.js, or the service worker keeps serving the cached copy.
 */
import {
  Y as useParams,
  a as useToast,
  u as useNavigate,
  r as React,
  A as ApiError,
  j as X,
  G as Spinner,
  p as Button,
  R as usePermission,
  i as makeIcon,
  V as invoicesApi,
  a9 as paymentsApi,
  O as walletApi,
  U as IconUser,
  B as IconBuilding,
  M as IconMail,
  P as IconPhone,
  af as IconMapPin,
  q as IconCalendar
} from "./index-DI5X4c9f.js";
import { c as customersApi } from "./admin-DtGep2Hs.js";
import { a as USER_STATUS } from "./admin-uaE5vem6.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { u as useDebounce } from "./useDebounce-DSDIQF2c.js";
import { b as money, c as formatDate, f as formatDateTime } from "./formatters-DFe5xCRi.js";
import { b as bookingsApi } from "./bookings-DrKATIWm.js";
import { B as BOOKING_STATUS, a as BOOKING_STATUS_TONES } from "./bookings-CeZsOhV5.js";
import {
  I as INVOICE_STATUS,
  c as PAYMENT_STATUS,
  d as PAYMENT_METHOD,
  W as WALLET_CATEGORY
} from "./finance-C0-kLKkN.js";
import {
  t as ticketsApi,
  T as TICKET_STATUS,
  a as TICKET_STATUS_TONES,
  b as TICKET_PRIORITY
} from "./SupportPages-Cf5pV8yQ.js";
import { A as IconArrowLeft } from "./arrow-left-CAirZ0TE.js";
import "./ContactUsPage-WEsv0zRR.js";
import "./chevron-right-BikENMwC.js";

/* ---------------------------------------------------------------- helpers */

/** jsx(...) / jsxs(...) without a JSX compiler; `key` is lifted to the 3rd arg. */
function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  if (kids.length === 1) p.children = kids[0];
  else if (kids.length > 1) p.children = kids;
  return kids.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}

const F = X.Fragment;

const IconGst = makeIcon("file-text", [
  ["path", { d: "M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z", key: "cp-gst-a" }],
  ["path", { d: "M14 2v4a2 2 0 0 0 2 2h4", key: "cp-gst-b" }],
  ["path", { d: "M16 13H8", key: "cp-gst-c" }],
  ["path", { d: "M16 17H8", key: "cp-gst-d" }]
]);
const IconPhoto = makeIcon("image", [
  ["rect", { width: "18", height: "18", x: "3", y: "3", rx: "2", ry: "2", key: "cp-img-a" }],
  ["circle", { cx: "9", cy: "9", r: "2", key: "cp-img-b" }],
  ["path", { d: "m21 15-3.086-3.086a2 2 0 0 0-2.828 0L6 21", key: "cp-img-c" }]
]);
const IconSearch = makeIcon("search", [
  ["circle", { cx: "11", cy: "11", r: "8", key: "cp-find-a" }],
  ["path", { d: "m21 21-4.3-4.3", key: "cp-find-b" }]
]);

const USER_STATUS_TONES = { 1: "success", 2: "neutral", 3: "danger", 4: "warning" };
const TAB_PAGE_SIZE = 10;
/** InvoiceStatus: Pending=1 Paid=2 PartiallyPaid=3 Cancelled=4 Refunded=5 (Sent=6) */
const INVOICE_STATUS_TONES = { 1: "warning", 2: "success", 3: "info", 4: "neutral", 5: "danger", 6: "info" };
/** PaymentStatus: Pending=1 Success=2 Failed=3 Refunded=4 Cancelled=5 */
const PAYMENT_STATUS_TONES = { 1: "warning", 2: "success", 3: "danger", 4: "info", 5: "neutral" };
/** TicketStatus, minus Resolved=5 and Closed=6. */
const OPEN_TICKET_STATUSES = new Set([1, 2, 3, 4, 7]);
const INVOICE_CANCELLED = 4;
/** Ceiling on the invoice sweep behind the money tiles, so one member can never fan out. */
const MAX_AGGREGATE_PAGES = 10;

const DASH = "—";
const LIST_PAGE_SIZE = 20;

/** Regalis.Domain.Enums.Gender, as the customer portal offers it. */
const GENDERS = { 1: "Male", 2: "Female", 3: "Other", 4: "Prefer not to say" };
/** The structured address, in the order the customer portal's Profile shows it. */
const ADDRESS_PARTS = [
  ["addressLine1", "Address Line 1"],
  ["addressLine2", "Address Line 2"],
  ["city", "City"],
  ["state", "State"],
  ["postalCode", "PIN Code"],
  ["country", "Country"]
];

/**
 * Sort choices for the list. Only keys the API actually recognises are offered;
 * the first entry sends nothing and takes whatever the server orders by.
 */
const SORT_OPTIONS = [
  { value: "", label: "Sort: Newest first" },
  { value: "fullname:asc", label: "Sort: Name A–Z" },
  { value: "fullname:desc", label: "Sort: Name Z–A" },
  { value: "lastloginutc:desc", label: "Sort: Recently active" }
];

function customerCode(userId) {
  return "CUST-" + String(userId ?? "").padStart(4, "0");
}

function initials(name) {
  const parts = (name || "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : "")).toUpperCase();
}

function message(error, fallback) {
  return error instanceof ApiError ? error.message : fallback;
}

/** Page numbers around the current one, at most five of them. */
function pageWindow(page, totalPages) {
  const span = Math.min(5, totalPages);
  let first = Math.max(1, page - Math.floor(span / 2));
  if (first + span - 1 > totalPages) first = totalPages - span + 1;
  return Array.from({ length: span }, (_, i) => first + i);
}

/**
 * Lifetime spend and the pending balance, swept off the customer's invoices --
 * the API has no per-customer money summary and /api/invoices pages at 100.
 */
async function aggregateInvoices(customerId) {
  let page = 1;
  let paid = 0;
  let outstanding = 0;
  let count = 0;
  for (;;) {
    const result = await invoicesApi.list({ customerId, page, pageSize: 100 });
    for (const invoice of result.items) {
      count += 1;
      if (invoice.status !== INVOICE_CANCELLED) {
        paid += invoice.paidAmount ?? 0;
        outstanding += invoice.outstandingAmount ?? 0;
      }
    }
    if (!result.hasNext || page >= MAX_AGGREGATE_PAGES) break;
    page += 1;
  }
  return { paid, outstanding, count };
}

/**
 * /api/tickets takes no customerId filter, so search by name and keep this
 * member's rows; if the search misses (the API decides what `search` covers),
 * fall back to scanning the most recent page.
 */
async function loadCustomerTickets(customerId, fullName) {
  const mine = (result) => result.items.filter((t) => t.customerId === customerId);
  let rows = mine(await ticketsApi.list({ search: fullName, page: 1, pageSize: 100 }));
  if (rows.length === 0) rows = mine(await ticketsApi.list({ page: 1, pageSize: 100 }));
  return rows;
}

/** A fixed set of rows dressed as a PagedResult, so DataTable can render it. */
function asPagedResult(items) {
  return {
    items,
    page: 1,
    pageSize: items.length,
    totalCount: items.length,
    totalPages: 1,
    hasNext: false,
    hasPrevious: false
  };
}

/** Loads a paged list the first time its tab is opened, then on page changes. */
function usePagedTab(active, fetcher) {
  const toast = useToast();
  const [result, setResult] = React.useState(null);
  const [page, setPage] = React.useState(1);
  // Starts true so the first paint of a freshly opened tab is a spinner, not an
  // empty state that flips to rows a moment later.
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    if (!active) return;
    let cancelled = false;
    setLoading(true);
    fetcher(page)
      .then((r) => {
        if (!cancelled) setResult(r);
      })
      .catch((error) => {
        if (!cancelled) toast.error(message(error, "Failed to load this tab."));
      })
      .finally(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [active, page, fetcher]);

  return { result, loading, page, setPage };
}

/* ------------------------------------------------------------------- list */

function AdminCustomersPage() {
  const toast = useToast();
  const navigate = useNavigate();
  // No "Add Customer" here: customers register themselves on the Customer Portal.

  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [sort, setSort] = React.useState("");
  const debouncedSearch = useDebounce(search, 350);

  const load = React.useCallback(async () => {
    setLoading(true);
    const [sortBy, direction] = sort ? sort.split(":") : [];
    try {
      setResult(
        await customersApi.list({
          page,
          pageSize: LIST_PAGE_SIZE,
          search: debouncedSearch || void 0,
          status: status ? Number(status) : void 0,
          sortBy: sortBy || void 0,
          isDescending: sortBy ? direction === "desc" : void 0
        })
      );
    } catch (error) {
      toast.error(message(error, "Failed to load customers."));
    } finally {
      setLoading(false);
    }
  }, [page, debouncedSearch, status, sort]);

  React.useEffect(() => {
    load();
  }, [load]);

  const open = (userId) => navigate(`/admin/customers/${userId}`);
  const rows = result?.items ?? [];
  const firstRow = rows.length > 0 ? (result.page - 1) * result.pageSize + 1 : 0;
  const lastRow = firstRow + rows.length - 1;

  const pager = [];
  if (result && result.totalPages > 1) {
    if (result.hasPrevious) {
      pager.push(
        el(
          "button",
          {
            key: "prev",
            type: "button",
            className: "custl-page",
            "aria-label": "Previous page",
            disabled: loading,
            onClick: () => setPage(result.page - 1)
          },
          "←"
        )
      );
    }
    for (const number of pageWindow(result.page, result.totalPages)) {
      pager.push(
        el(
          "button",
          {
            key: "page-" + number,
            type: "button",
            className: "custl-page" + (number === result.page ? " custl-page--active" : ""),
            "aria-current": number === result.page ? "page" : void 0,
            disabled: loading,
            onClick: () => setPage(number)
          },
          String(number)
        )
      );
    }
    if (result.hasNext) {
      pager.push(
        el(
          "button",
          {
            key: "next",
            type: "button",
            className: "custl-page",
            "aria-label": "Next page",
            disabled: loading,
            onClick: () => setPage(result.page + 1)
          },
          "→"
        )
      );
    }
  }

  // Built only once a page has arrived -- it reads result.totalCount.
  const renderTable = () =>
    el(
      F,
      null,
      el(
        "div",
        { className: "table-scroll" },
        el(
          "table",
          { className: "data-table custl-table", style: { opacity: loading ? 0.6 : 1 } },
          el(
            "thead",
            null,
            el(
              "tr",
              null,
              el("th", null, "Customer"),
              el("th", null, "Company"),
              el("th", null, "Email"),
              el("th", null, "Phone"),
              el("th", null, "Status"),
              el("th", { className: "custl-actions-col" }, "")
            )
          ),
          el(
            "tbody",
            null,
            rows.map((row) =>
              el(
                "tr",
                {
                  key: row.userId,
                  className: "custl-row",
                  tabIndex: 0,
                  role: "link",
                  "aria-label": "Open " + row.fullName,
                  onClick: () => open(row.userId),
                  onKeyDown: (event) => {
                    if (event.key === "Enter" || event.key === " ") {
                      event.preventDefault();
                      open(row.userId);
                    }
                  }
                },
                el(
                  "td",
                  null,
                  el(
                    "div",
                    { className: "custl-who" },
                    el("div", { className: "custl-avatar" }, initials(row.fullName)),
                    el(
                      "div",
                      null,
                      el("div", { className: "custl-who__name" }, row.fullName),
                      el("div", { className: "custl-who__code" }, customerCode(row.userId))
                    )
                  )
                ),
                el("td", null, row.companyName || el("span", { className: "custl-muted" }, DASH)),
                el("td", null, row.email),
                el("td", null, row.mobileNumber || el("span", { className: "custl-muted" }, DASH)),
                el(
                  "td",
                  null,
                  el(
                    Badge,
                    { tone: USER_STATUS_TONES[row.status] ?? "neutral" },
                    USER_STATUS[row.status] ?? "Unknown"
                  )
                ),
                el(
                  "td",
                  { className: "custl-actions-col" },
                  el(
                    Button,
                    {
                      size: "sm",
                      variant: "outline",
                      /* The row is clickable too; do not let both fire. */
                      onClick: (event) => {
                        event.stopPropagation();
                        open(row.userId);
                      },
                      "aria-label": "View " + row.fullName
                    },
                    "View"
                  )
                )
              )
            )
          )
        )
      ),
      el(
        "div",
        { className: "custl-foot" },
        el(
          "div",
          { className: "custl-foot__info" },
          "Showing " + firstRow + "–" + lastRow + " of " + result.totalCount + " customers"
        ),
        pager.length > 0 ? el("div", { className: "custl-pages" }, pager) : null
      )
    );

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Customers",
      description: "All customers registered on the Customer Portal."
    }),
    el(
      "div",
      { className: "custl-toolbar" },
      el(
        "div",
        { className: "custl-search" },
        el(IconSearch, { size: 18 }),
        el("input", {
          className: "field__input",
          type: "search",
          "aria-label": "Search customers",
          placeholder: "Search by name, company, email or phone…",
          value: search,
          onChange: (event) => {
            setSearch(event.target.value);
            setPage(1);
          }
        })
      ),
      el(
        "select",
        {
          className: "field__input custl-select",
          "aria-label": "Filter by status",
          value: status,
          onChange: (event) => {
            setStatus(event.target.value);
            setPage(1);
          }
        },
        el("option", { value: "" }, "All Status"),
        // Locked (3) is not offered as a filter; a locked customer still shows the Locked pill.
        Object.entries(USER_STATUS).filter(([value]) => value !== "3").map(([value, label]) =>
          el("option", { key: value, value }, label)
        )
      ),
      el(
        "select",
        {
          className: "field__input custl-select",
          "aria-label": "Sort customers",
          value: sort,
          onChange: (event) => {
            setSort(event.target.value);
            setPage(1);
          }
        },
        SORT_OPTIONS.map((option) =>
          el("option", { key: option.value || "default", value: option.value }, option.label)
        )
      )
    ),
    el(
      Card,
      null,
      loading && !result
        ? el(Spinner, { center: true })
        : rows.length === 0
          ? el(EmptyState, {
              title: "No customers match the filters",
              description: "Registrations appear here as soon as members verify their account."
            })
          : renderTable()
    )
  );
}

/* ----------------------------------------------------------------- detail */

function ProfileField({ icon, label, value, muted }) {
  return el(
    "div",
    null,
    el("div", { className: "custp-field__label" }, el(icon, { size: 14 }), label),
    el(
      "div",
      { className: "custp-field__value" + (muted ? " custp-field__value--muted" : "") },
      value
    )
  );
}

function StatTile({ value, label }) {
  return el(
    "div",
    { className: "custp-tile" },
    el("div", { className: "custp-tile__value" }, value),
    el("div", { className: "custp-tile__label" }, label)
  );
}

function StandingItem({ label, children }) {
  return el(
    "div",
    null,
    el("div", { className: "custp-standing__label" }, label),
    el("div", { className: "custp-standing__value" }, children)
  );
}

function AdminCustomerDetailPage() {
  const { id } = useParams();
  const customerId = Number(id);
  const toast = useToast();
  const navigate = useNavigate();

  const [customer, setCustomer] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [tab, setTab] = React.useState("overview");
  const [summary, setSummary] = React.useState(null);
  const [tickets, setTickets] = React.useState(null);
  const [wallet, setWallet] = React.useState(null);

  const canViewBookings = usePermission("BOOKING.VIEW");
  const canViewPayments = usePermission("PAYMENT.VIEW");
  const canViewInvoices = usePermission("INVOICE.VIEW");
  const canViewWallet = usePermission("WALLET.VIEW");
  const canViewTickets = usePermission("TICKET.VIEW");

  const load = React.useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setCustomer(await customersApi.get(customerId));
    } catch (error) {
      toast.error(message(error, "Failed to load the customer."));
    } finally {
      setLoading(false);
    }
  }, [id]);

  React.useEffect(() => {
    load();
  }, [load]);

  // Overview tiles. Each source fails independently: a tile the signed-in user
  // cannot see, or one that errored, stays a dash rather than blocking the page.
  React.useEffect(() => {
    if (!customer) return;
    let cancelled = false;
    const next = { bookings: null, spend: null, pending: null, openTickets: null };
    const jobs = [];

    if (canViewBookings) {
      jobs.push(
        bookingsApi
          .list({ customerId, page: 1, pageSize: 1 })
          .then((r) => {
            next.bookings = r.totalCount;
          })
          .catch(() => {})
      );
    }
    if (canViewInvoices) {
      jobs.push(
        aggregateInvoices(customerId)
          .then((totals) => {
            next.spend = totals.paid;
            next.pending = totals.outstanding;
          })
          .catch(() => {})
      );
    }
    if (canViewTickets) {
      jobs.push(
        loadCustomerTickets(customerId, customer.fullName)
          .then((rows) => {
            next.openTickets = rows.filter((t) => OPEN_TICKET_STATUSES.has(t.status)).length;
            if (!cancelled) setTickets(rows);
          })
          .catch(() => {
            if (!cancelled) setTickets([]);
          })
      );
    }

    Promise.all(jobs).then(() => {
      if (!cancelled) setSummary(next);
    });
    return () => {
      cancelled = true;
    };
  }, [customer, customerId, canViewBookings, canViewInvoices, canViewTickets]);

  React.useEffect(() => {
    if (tab !== "wallet" || wallet || !canViewWallet) return;
    walletApi
      .ofCustomer(customerId)
      .then(setWallet)
      .catch((error) => toast.error(message(error, "Failed to load the wallet.")));
  }, [tab, wallet, canViewWallet, customerId]);

  const fetchBookings = React.useCallback(
    (page) => bookingsApi.list({ customerId, page, pageSize: TAB_PAGE_SIZE }),
    [customerId]
  );
  const fetchPayments = React.useCallback(
    (page) => paymentsApi.list({ customerId, page, pageSize: TAB_PAGE_SIZE }),
    [customerId]
  );
  const fetchInvoices = React.useCallback(
    (page) => invoicesApi.list({ customerId, page, pageSize: TAB_PAGE_SIZE }),
    [customerId]
  );
  const fetchWalletTransactions = React.useCallback(
    (page) => walletApi.customerTransactions(customerId, { page, pageSize: TAB_PAGE_SIZE }),
    [customerId]
  );

  const bookings = usePagedTab(tab === "bookings" && canViewBookings, fetchBookings);
  const payments = usePagedTab(tab === "payments" && canViewPayments, fetchPayments);
  const invoices = usePagedTab(tab === "invoices" && canViewInvoices, fetchInvoices);
  const walletTransactions = usePagedTab(tab === "wallet" && canViewWallet, fetchWalletTransactions);

  if (loading) return el(Spinner, { center: true });
  if (!customer) return null;

  // Everything below comes from GET /customers/{id}; a field the API does not send
  // reads "Not captured" (see deploy/customer-profile-fields). Invoices are no
  // fallback for the GSTIN: the only GSTIN /api/invoices returns is Regalis's own.
  const gstin = customer.gstin ?? null;
  const dateOfBirth = customer.dateOfBirth ?? null;
  const gender = GENDERS[customer.gender] ?? null;
  // Accounts from before the structured address only have the single line.
  const hasAddressParts = ADDRESS_PARTS.some(([key]) => customer[key]);
  const captured = (icon, label, value) =>
    el(ProfileField, { icon, label, value: value || "Not captured", muted: !value });
  const tile = (value) => (value == null ? DASH : value);

  const tabs = [
    { key: "overview", label: "Overview", show: true },
    { key: "bookings", label: "Bookings", show: canViewBookings },
    { key: "payments", label: "Payments", show: canViewPayments },
    { key: "invoices", label: "Invoices", show: canViewInvoices },
    { key: "wallet", label: "Wallet", show: canViewWallet },
    { key: "support", label: "Support", show: canViewTickets }
  ].filter((t) => t.show);

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Customer Profile",
      description: "Full profile as filled by the customer in their Customer Portal.",
      actions: el(
        Button,
        { variant: "outline", onClick: () => navigate("/admin/customers") },
        el(IconArrowLeft, { size: 16 }),
        " Back to Customers"
      )
    }),

    // identity
    el(
      Card,
      null,
      el(
        "div",
        { className: "custp-id" },
        el("div", { className: "custp-id__avatar" }, initials(customer.fullName)),
        el(
          "div",
          { className: "custp-id__main" },
          el("div", { className: "custp-id__name" }, customer.fullName),
          el(
            "div",
            { className: "custp-id__meta" },
            "Customer ID: " + customerCode(customer.userId),
            "  ·  Member since " + formatDate(customer.createdDate)
          )
        ),
        el(
          "div",
          { className: "custp-id__actions" },
          el(
            Badge,
            { tone: USER_STATUS_TONES[customer.status] ?? "neutral" },
            USER_STATUS[customer.status] ?? "Unknown"
          )
        )
      ),
      customer.lockoutEndUtc
        ? el(
            "div",
            { className: "form-error", role: "alert", style: { marginTop: "var(--space-4)" } },
            "Locked out until " + formatDateTime(customer.lockoutEndUtc) + "."
          )
        : null
    ),

    // the details the member filled in on the customer portal
    el(
      Card,
      {
        title: "Profile Details",
        actions: el(Badge, { tone: "info" }, "Filled by Customer")
      },
      el(
        "div",
        { className: "custp-fields" },
        el(ProfileField, { icon: IconUser, label: "Full Name", value: customer.fullName }),
        el(ProfileField, {
          icon: IconBuilding,
          label: "Company Name",
          value: customer.companyName || DASH,
          muted: !customer.companyName
        }),
        el(ProfileField, {
          icon: IconGst,
          label: "Company GST Number",
          value: gstin ?? "Not captured",
          muted: !gstin
        }),
        el(ProfileField, { icon: IconMail, label: "Email ID", value: customer.email }),
        el(ProfileField, {
          icon: IconPhone,
          label: "Contact Number",
          value: customer.mobileNumber || DASH,
          muted: !customer.mobileNumber
        }),
        captured(IconUser, "Gender", gender),
        captured(IconCalendar, "Date of Birth", dateOfBirth && formatDate(dateOfBirth)),
        el(ProfileField, {
          icon: IconPhoto,
          label: "Photo",
          value: customer.photoPath ? "Uploaded by customer — not viewable here yet" : "Not uploaded",
          muted: true
        }),
        hasAddressParts
          ? ADDRESS_PARTS.map(([key, label]) =>
              el(F, { key }, captured(IconMapPin, label, customer[key]))
            )
          : captured(IconMapPin, "Address", customer.address)
      )
    ),

    // activity
    el(
      "div",
      { className: "custp-tabs", role: "tablist" },
      tabs.map((item) =>
        el(
          "button",
          {
            key: item.key,
            type: "button",
            role: "tab",
            "aria-selected": tab === item.key,
            className: "custp-tabs__tab" + (tab === item.key ? " custp-tabs__tab--active" : ""),
            onClick: () => setTab(item.key)
          },
          item.label
        )
      )
    ),

    tab === "overview"
      ? el(
          F,
          null,
          el(
            "div",
            { className: "custp-tiles" },
            el(StatTile, { value: tile(summary?.bookings), label: "Total Bookings" }),
            el(StatTile, {
              value: summary?.spend == null ? DASH : money(summary.spend),
              label: "Lifetime Spend"
            }),
            el(StatTile, {
              value: summary?.pending == null ? DASH : money(summary.pending),
              label: "Pending Amount"
            }),
            el(StatTile, { value: tile(summary?.openTickets), label: "Open Tickets" })
          ),
          el(
            Card,
            { title: "Account standing" },
            el(
              "div",
              { className: "custp-standing" },
              el(
                StandingItem,
                { label: "Status" },
                el(
                  Badge,
                  { tone: USER_STATUS_TONES[customer.status] ?? "neutral" },
                  USER_STATUS[customer.status] ?? "Unknown"
                )
              ),
              el(
                StandingItem,
                { label: "Email" },
                el(
                  Badge,
                  { tone: customer.isEmailVerified ? "success" : "warning" },
                  customer.isEmailVerified ? "Verified" : "Unverified"
                )
              ),
              el(
                StandingItem,
                { label: "Last sign-in" },
                customer.lastLoginUtc ? formatDateTime(customer.lastLoginUtc) : "Never"
              ),
              el(
                StandingItem,
                { label: "Roles" },
                customer.roles?.length > 0 ? customer.roles.join(", ") : DASH
              ),
              el(StandingItem, { label: "Member since" }, formatDate(customer.createdDate))
            )
          )
        )
      : null,

    tab === "bookings"
      ? el(
          Card,
          null,
          el(DataTable, {
            columns: [
              {
                key: "booking",
                header: "Booking",
                render: (row) =>
                  el(
                    "div",
                    null,
                    el("div", { style: { fontWeight: 600 } }, row.bookingNumber),
                    el(
                      "div",
                      { style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" } },
                      [row.workspaceName, row.workspaceTypeName].filter(Boolean).join(" · ")
                    )
                  )
              },
              { key: "when", header: "When", render: (row) => formatDateTime(row.startUtc) },
              {
                key: "duration",
                header: "Duration",
                render: (row) => (row.durationMinutes ? row.durationMinutes + " min" : DASH)
              },
              { key: "amount", header: "Amount", render: (row) => money(row.totalAmount) },
              {
                key: "status",
                header: "Status",
                render: (row) =>
                  el(
                    Badge,
                    { tone: BOOKING_STATUS_TONES[row.status] ?? "neutral" },
                    BOOKING_STATUS[row.status] ?? "Unknown"
                  )
              }
            ],
            result: bookings.result,
            loading: bookings.loading,
            rowKey: (row) => row.bookingId,
            onPageChange: bookings.setPage,
            emptyTitle: "No bookings yet",
            emptyDescription: "Desk, cabin and meeting-room bookings appear here."
          })
        )
      : null,

    tab === "payments"
      ? el(
          Card,
          null,
          el(DataTable, {
            columns: [
              { key: "number", header: "Payment", render: (row) => row.paymentNumber },
              {
                key: "method",
                header: "Method",
                render: (row) => PAYMENT_METHOD[row.paymentMethod] ?? DASH
              },
              { key: "amount", header: "Amount", render: (row) => money(row.amount) },
              {
                key: "invoice",
                header: "Invoice",
                render: (row) =>
                  row.invoiceNumber || el("span", { style: { color: "var(--text-muted)" } }, DASH)
              },
              {
                key: "status",
                header: "Status",
                render: (row) =>
                  el(
                    Badge,
                    { tone: PAYMENT_STATUS_TONES[row.status] ?? "neutral" },
                    PAYMENT_STATUS[row.status] ?? "Unknown"
                  )
              },
              {
                key: "paid",
                header: "Paid",
                render: (row) => (row.paidUtc ? formatDateTime(row.paidUtc) : DASH)
              }
            ],
            result: payments.result,
            loading: payments.loading,
            rowKey: (row) => row.paymentId,
            onPageChange: payments.setPage,
            emptyTitle: "No payments yet",
            emptyDescription: "Gateway and wallet payments appear here once they settle."
          })
        )
      : null,

    tab === "invoices"
      ? el(
          Card,
          null,
          el(DataTable, {
            columns: [
              { key: "number", header: "Invoice", render: (row) => row.invoiceNumber },
              { key: "date", header: "Date", render: (row) => formatDate(row.invoiceDate) },
              { key: "total", header: "Total", render: (row) => money(row.totalAmount) },
              {
                key: "outstanding",
                header: "Outstanding",
                render: (row) =>
                  row.outstandingAmount > 0
                    ? el(
                        "span",
                        { style: { color: "var(--color-danger)", fontWeight: 600 } },
                        money(row.outstandingAmount)
                      )
                    : money(0)
              },
              {
                key: "status",
                header: "Status",
                render: (row) =>
                  el(
                    "div",
                    { className: "custp-badges" },
                    el(
                      Badge,
                      { tone: INVOICE_STATUS_TONES[row.status] ?? "neutral" },
                      INVOICE_STATUS[row.status] ?? "Unknown"
                    ),
                    row.isOverdue ? el(Badge, { tone: "danger" }, "Overdue") : null
                  )
              }
            ],
            result: invoices.result,
            loading: invoices.loading,
            rowKey: (row) => row.invoiceId,
            onPageChange: invoices.setPage,
            emptyTitle: "No invoices yet",
            emptyDescription: "Every confirmed booking and pass raises a GST invoice here."
          })
        )
      : null,

    tab === "wallet"
      ? el(
          Card,
          null,
          el(
            "div",
            { className: "custp-tiles", style: { marginBottom: "var(--space-5)" } },
            el(StatTile, { value: wallet ? money(wallet.balance) : DASH, label: "Wallet balance" }),
            el(StatTile, {
              value: wallet ? money(wallet.totalCredited) : DASH,
              label: "Total credited"
            }),
            el(StatTile, {
              value: wallet ? money(wallet.totalDebited) : DASH,
              label: "Total debited"
            })
          ),
          el(DataTable, {
            columns: [
              { key: "when", header: "When", render: (row) => formatDateTime(row.transactionUtc) },
              {
                key: "narration",
                header: "Narration",
                render: (row) =>
                  el(
                    "div",
                    null,
                    el("div", null, row.narration || WALLET_CATEGORY[row.category] || DASH),
                    el(
                      "div",
                      { style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" } },
                      row.transactionNumber
                    )
                  )
              },
              {
                key: "category",
                header: "Category",
                render: (row) => WALLET_CATEGORY[row.category] ?? DASH
              },
              {
                key: "amount",
                header: "Amount",
                render: (row) =>
                  el(
                    "span",
                    {
                      style: {
                        fontWeight: 600,
                        color: row.direction === 1 ? "var(--color-success)" : "var(--color-danger)"
                      }
                    },
                    (row.direction === 1 ? "+" : "−") + money(row.amount)
                  )
              },
              { key: "balance", header: "Balance", render: (row) => money(row.balanceAfter) }
            ],
            result: walletTransactions.result,
            loading: walletTransactions.loading,
            rowKey: (row) => row.walletTransactionId,
            onPageChange: walletTransactions.setPage,
            emptyTitle: "No wallet activity",
            emptyDescription: "Recharges, booking debits and refunds appear here."
          })
        )
      : null,

    tab === "support"
      ? el(
          Card,
          null,
          tickets == null
            ? el(Spinner, { center: true })
            : el(DataTable, {
                columns: [
                  { key: "number", header: "Ticket", render: (row) => row.ticketNumber },
                  {
                    key: "subject",
                    header: "Subject",
                    render: (row) =>
                      el(
                        "div",
                        null,
                        el("div", { style: { fontWeight: 600 } }, row.subject),
                        el(
                          "div",
                          {
                            style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" }
                          },
                          [row.categoryName, row.issueTypeName].filter(Boolean).join(" · ")
                        )
                      )
                  },
                  {
                    key: "priority",
                    header: "Priority",
                    render: (row) => TICKET_PRIORITY[row.priority] ?? DASH
                  },
                  {
                    key: "status",
                    header: "Status",
                    render: (row) =>
                      el(
                        Badge,
                        { tone: TICKET_STATUS_TONES[row.status] ?? "neutral" },
                        TICKET_STATUS[row.status] ?? "Unknown"
                      )
                  },
                  {
                    key: "activity",
                    header: "Last activity",
                    render: (row) => formatDateTime(row.lastMessageUtc ?? row.createdDate)
                  },
                  {
                    key: "actions",
                    header: "",
                    width: "96px",
                    render: (row) =>
                      el(
                        "div",
                        { className: "row-actions" },
                        el(
                          Button,
                          {
                            size: "sm",
                            variant: "outline",
                            onClick: () => navigate(`/admin/tickets/${row.ticketId}`)
                          },
                          "Open"
                        )
                      )
                  }
                ],
                result: asPagedResult(tickets),
                loading: false,
                rowKey: (row) => row.ticketId,
                onPageChange: () => {},
                emptyTitle: "No support tickets",
                emptyDescription: "Tickets this member raises appear here."
              })
        )
      : null
  );
}

export { AdminCustomerDetailPage, AdminCustomersPage };
