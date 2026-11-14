/**
 * Admin -> Waiting List (13 Nov 2026, kit deploy/admin-customer).
 * Customers waiting for a desk on a day-pass first-visit date that was full. The API worker
 * tells every waiting customer (in-app + email) when a desk frees up; the first to buy gets
 * it. Staff can remove an entry with a reason (PASS.EDIT); the customer is notified.
 *
 * Data: GET /api/waitlist?page=&pageSize=&search=&status=&locationId=&from=&to=
 *       POST /api/waitlist/{id}/remove {reason}
 * The location follows the picker in the admin top bar.
 *
 * Hand-maintained chunk: the route in index-DI5X4c9f.js imports it by name; bump its
 * revision in sw.js when it changes.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as X,
  p as Button,
  I as Input,
  F as Select,
  D as unwrap,
  E as http,
  m as useUiStore
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { f as formatDateTime, c as formatDate } from "./formatters-DFe5xCRi.js";
import { displayPhone } from "./PhoneField-PH1.js";

function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  const flat = kids.flat().filter((k) => k !== null && k !== void 0 && k !== false);
  if (flat.length === 1) p.children = flat[0];
  else if (flat.length > 1) p.children = flat;
  return flat.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}

const STATUS = [
  { value: "1", label: "Waiting", tone: "warning" },
  { value: "2", label: "Booked", tone: "success" },
  { value: "3", label: "Removed by customer", tone: "neutral" },
  { value: "4", label: "Removed by staff", tone: "danger" },
  { value: "5", label: "Expired", tone: "neutral" }
];
const TONE = Object.fromEntries(STATUS.map((s) => [Number(s.value), s.tone]));
const MUTED = { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" };

function useDebounced(value, ms) {
  const [v, setV] = React.useState(value);
  React.useEffect(() => {
    const t = setTimeout(() => setV(value), ms);
    return () => clearTimeout(t);
  }, [value, ms]);
  return v;
}

export function AdminWaitlistPage() {
  const toast = useToast();
  const canRemove = usePermission("PASS.EDIT");
  const locationId = useUiStore((s) => s.locationId);
  const [page, setPage] = React.useState(1);
  const [search, setSearch] = React.useState("");
  const [status, setStatus] = React.useState("1");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [removing, setRemoving] = React.useState(null);
  const term = useDebounced(search, 350);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setResult(
        await unwrap(
          http.get("/waitlist", {
            params: {
              page,
              pageSize: 20,
              search: term || void 0,
              status: Number(status),
              locationId: locationId ?? void 0,
              from: from || void 0,
              to: to || void 0
            }
          })
        )
      );
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Failed to load the waiting list.");
    } finally {
      setLoading(false);
    }
  }, [page, term, status, locationId, from, to]);

  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    setPage(1);
  }, [term, status, locationId, from, to]);

  const remove = async () => {
    const reason = removing.reason.trim();
    if (!reason) {
      toast.error("Enter a reason for removing this entry.");
      return;
    }
    setRemoving((r) => ({ ...r, busy: true }));
    try {
      await unwrap(http.post(`/waitlist/${removing.row.seatWaitlistId}/remove`, { reason }));
      toast.success(`${removing.row.customerName} removed from the waiting list. They have been notified.`);
      setRemoving(null);
      load();
    } catch (e) {
      toast.error(e instanceof ApiError ? e.message : "Could not remove the entry.");
      setRemoving((r) => r && { ...r, busy: false });
    }
  };

  const columns = [
    {
      key: "customer",
      header: "Customer",
      render: (r) =>
        el(
          "div",
          null,
          el("div", { style: { fontWeight: 600 } }, r.customerName),
          el("div", { style: MUTED }, [r.customerEmail, displayPhone(r.customerMobile)].filter(Boolean).join(" · "))
        )
    },
    {
      key: "date",
      header: "Desk wanted on",
      render: (r) =>
        el(
          "div",
          null,
          el("div", { style: { fontWeight: 600 } }, formatDate(r.waitDate)),
          r.position > 0 ? el("div", { style: MUTED }, `#${r.position} in line`) : null
        )
    },
    {
      key: "where",
      header: "Location",
      render: (r) => el("div", null, el("div", null, r.locationName), el("div", { style: MUTED }, r.passPlanName || r.workspaceTypeName))
    },
    { key: "joined", header: "Joined", render: (r) => formatDateTime(r.joinedUtc) },
    {
      key: "notice",
      header: "Desk free?",
      render: (r) =>
        el(
          "div",
          null,
          r.status === 1 ? el(Badge, { tone: r.isSeatFree ? "success" : "neutral" }, r.isSeatFree ? "Free now" : "Full") : "—",
          r.notifyCount > 0
            ? el("div", { style: MUTED }, `Told ${r.notifyCount}× · last ${formatDateTime(r.lastNotifiedUtc)}`)
            : null
        )
    },
    {
      key: "status",
      header: "Status",
      render: (r) =>
        el(
          "div",
          null,
          el(Badge, { tone: TONE[r.status] ?? "neutral" }, r.statusName),
          r.closeReason && r.status !== 1 ? el("div", { style: { ...MUTED, maxWidth: 260 } }, r.closeReason) : null
        )
    },
    {
      key: "actions",
      header: "",
      render: (r) =>
        canRemove && r.status === 1
          ? el(Button, { size: "sm", variant: "danger-outline", onClick: () => setRemoving({ row: r, reason: "", busy: false }) }, "Remove")
          : null
    }
  ];

  return el(
    React.Fragment,
    null,
    el(PageHeader, {
      title: "Waiting List",
      description:
        "Customers waiting for a desk on a day-pass date that was full. When a desk frees up, everyone waiting for that date is told in the app and by email; the first to buy gets it."
    }),
    el(
      Card,
      null,
      el(
        "div",
        { className: "toolbar bk-filters" },
        el(Input, { label: "Search", placeholder: "Name, email or mobile…", value: search, onChange: (e) => setSearch(e.target.value) }),
        el(Select, {
          label: "Status",
          value: status,
          options: [...STATUS.map((s) => ({ value: s.value, label: s.label })), { value: "0", label: "All statuses" }],
          onChange: (e) => setStatus(e.target.value)
        }),
        el(Input, { label: "From", type: "date", value: from, onChange: (e) => setFrom(e.target.value) }),
        el(Input, { label: "To", type: "date", value: to, onChange: (e) => setTo(e.target.value) })
      ),
      el(DataTable, {
        columns,
        result,
        loading,
        rowKey: (r) => r.seatWaitlistId,
        onPageChange: setPage,
        emptyTitle: status === "1" ? "Nobody is waiting" : "No entries match the filters",
        emptyDescription: status === "1" ? "Customers join from Passes when the date they pick has no free desk." : void 0
      })
    ),
    removing &&
      el(
        Modal,
        {
          open: true,
          title: "Remove from the waiting list",
          onClose: () => {
            removing.busy || setRemoving(null);
          },
          footer: el(
            React.Fragment,
            null,
            el(Button, { variant: "ghost", disabled: removing.busy, onClick: () => setRemoving(null) }, "Keep"),
            el(Button, { variant: "danger", loading: removing.busy, disabled: !removing.reason.trim(), onClick: remove }, "Remove")
          )
        },
        el(
          "div",
          { className: "form-stack" },
          el(
            "p",
            { style: { margin: 0 } },
            `${removing.row.customerName} is waiting for a desk on ${formatDate(removing.row.waitDate)} at ${removing.row.locationName}. ` +
              "They will get a notification with your reason."
          ),
          el(Input, {
            label: "Reason",
            required: true,
            maxLength: 300,
            placeholder: "Why is this entry being removed?",
            value: removing.reason,
            onChange: (e) => {
              const v = e.target.value;
              setRemoving((r) => ({ ...r, reason: v }));
            }
          })
        )
      )
  );
}
