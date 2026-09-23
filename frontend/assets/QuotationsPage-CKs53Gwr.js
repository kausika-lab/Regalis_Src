/* Quotations — customer portal (specification SS3.7).
 *
 * Mobile-first: most customers arrive from the WhatsApp link on a phone, so the
 * decision buttons are pinned to the bottom of the screen and everything above
 * them scrolls. Patched into the built bundle by hand; see the admin chunk.
 */
import {
  Y as useParams,
  a as useToast,
  u as useNavigate,
  r as React,
  A as ApiClientError,
  j as jsx,
  G as Spinner,
  p as Button,
  I as Input,
} from "./index-DI5X4c9f.js";
import {
  q as quotationsApi,
  Q as QUOTATION_STATUS,
  b as QuotationLinesCard,
  s as StatusPill,
  r as REJECT_REASONS,
  f as effectiveStatus,
} from "./AdminQuotationPages-COWOrOkL.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { c as formatDate, f as formatDateTime, b as formatInr } from "./formatters-DFe5xCRi.js";
import "./admin-DtGep2Hs.js";
import "./useFieldErrors-C8qSSg0h.js";
import "./EmptyState-B4UbbVj4.js";
import "./chevron-right-BikENMwC.js";

const e = jsx;

function errorMessage(error, fallback) {
  return error instanceof ApiClientError ? error.message : fallback;
}

/* The tabs from SS3.7. "Awaiting action" is Sent and Viewed together — the
   difference between them is the admin's business, not the customer's. */
const TABS = [
  { key: "awaiting", label: "Awaiting action" },
  { key: "approved", label: "Approved", status: QUOTATION_STATUS.Approved },
  { key: "rejected", label: "Rejected", status: QUOTATION_STATUS.Rejected },
  { key: "expired", label: "Expired", status: QUOTATION_STATUS.Expired },
];

const isAwaiting = (q) => {
  const status = effectiveStatus(q);
  return status === QUOTATION_STATUS.Sent || status === QUOTATION_STATUS.Viewed;
};

function countdown(quotation) {
  if (quotation.isExpired) return "Expired";
  const days = quotation.daysToExpiry;
  if (days < 0) return "Expired";
  if (days === 0) return "Expires today";
  return `${days} day${days === 1 ? "" : "s"} left`;
}

/* ── List ───────────────────────────────────────────────────────────────── */

function MyQuotationsPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [page, setPage] = React.useState(1);
  const [tab, setTab] = React.useState("awaiting");

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const active = TABS.find((t) => t.key === tab);
      /* "Awaiting action" spans two statuses, so it is filtered here rather than
         asked of the server, which takes one status at a time. */
      setResult(await quotationsApi.mine({
        page,
        pageSize: 20,
        status: active && active.status ? active.status : undefined,
      }));
    } catch (error) {
      toast.error(errorMessage(error, "Failed to load your quotations."));
    } finally {
      setLoading(false);
    }
  }, [page, tab]);

  React.useEffect(() => { void load(); }, [load]);

  const visible = React.useMemo(() => {
    if (!result) return null;
    if (tab !== "awaiting") return result;
    const items = result.items.filter(isAwaiting);
    return { ...result, items, totalCount: items.length, totalPages: 1, hasNext: false, hasPrevious: false };
  }, [result, tab]);

  const columns = [
    {
      key: "number",
      header: "Quotation",
      render: (q) => e.jsxs("div", {
        children: [
          e.jsx("div", { className: "qtn-cell__strong", children: q.quotationNumber }),
          e.jsx("div", { className: "qtn-cell__muted", children: formatDate(q.quotationDate) }),
        ],
      }),
    },
    {
      key: "total",
      header: "Total",
      render: (q) => e.jsx("strong", { children: formatInr(q.totalAmount) }),
    },
    {
      key: "valid",
      header: "Valid until",
      render: (q) => e.jsxs("div", {
        children: [
          e.jsx("div", { children: formatDate(q.validUntil) }),
          isAwaiting(q) && e.jsx("div", {
            className: `qtn-due${q.daysToExpiry <= 3 ? " qtn-due--soon" : ""}`,
            children: countdown(q),
          }),
        ],
      }),
    },
    {
      key: "status",
      header: "Status",
      render: (q) => e.jsx(StatusPill, { quotation: q, forCustomer: true }),
    },
    {
      key: "actions",
      header: "",
      render: (q) => e.jsx("div", {
        className: "row-actions",
        children: e.jsx(Button, {
          size: "sm",
          variant: isAwaiting(q) ? "primary" : "outline",
          onClick: () => navigate(`/quotations/${q.quotationId}`),
          children: isAwaiting(q) ? "Review" : "View",
        }),
      }),
    },
  ];

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Quotations",
        description: "Offers from Regalis WorkSpace. Approve one and we will raise the invoice.",
      }),
      e.jsxs(Card, {
        children: [
          e.jsx("div", {
            className: "qtn-tabs",
            role: "tablist",
            children: TABS.map((t) => e.jsx("button", {
              type: "button",
              role: "tab",
              "aria-selected": tab === t.key,
              className: `qtn-tab${tab === t.key ? " qtn-tab--active" : ""}`,
              onClick: () => { setTab(t.key); setPage(1); },
              children: t.label,
            }, t.key)),
          }),
          e.jsx(DataTable, {
            columns,
            result: visible,
            loading,
            rowKey: (q) => q.quotationId,
            onRowClick: (q) => navigate(`/quotations/${q.quotationId}`),
            onPageChange: setPage,
            emptyTitle: "Nothing here yet",
            emptyDescription: "Quotations Regalis sends you will appear here.",
          }),
        ],
      }),
    ],
  });
}

/* ── Approve / reject dialogs ───────────────────────────────────────────── */

function ApproveModal({ quotation, onClose, onDone }) {
  const toast = useToast();
  const [accepted, setAccepted] = React.useState(false);
  const [busy, setBusy] = React.useState(false);

  const submit = async () => {
    setBusy(true);
    try {
      const saved = await quotationsApi.approve(quotation.quotationId, { acceptedTerms: true });
      onDone(saved);
    } catch (error) {
      toast.error(errorMessage(error, "Could not approve the quotation."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: "Approve this quotation?",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, disabled: busy, children: "Not yet" }),
        e.jsx(Button, { loading: busy, disabled: !accepted, onClick: submit, children: "Approve" }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsxs("p", {
          children: [
            "You are approving ",
            e.jsx("strong", { children: quotation.quotationNumber }),
            " for ",
            e.jsx("strong", { children: formatInr(quotation.totalAmount) }),
            ".",
          ],
        }),
        e.jsx("p", { className: "field__hint", children: quotation.totalInWords }),
        e.jsx(Checkbox, {
          label: "I have read and accept the terms & conditions",
          checked: accepted,
          onChange: (ev) => setAccepted(ev.target.checked),
        }),
      ],
    }),
  });
}

function RejectModal({ quotation, onClose, onDone }) {
  const toast = useToast();
  const [reasonCode, setReasonCode] = React.useState("");
  const [comment, setComment] = React.useState("");
  const [busy, setBusy] = React.useState(false);

  /* "Other" is the one reason that needs saying out loud (SS3.7). */
  const needsComment = reasonCode === "OTHER";
  const canSubmit = reasonCode !== "" && (!needsComment || comment.trim().length > 0);

  const submit = async () => {
    setBusy(true);
    try {
      const saved = await quotationsApi.reject(quotation.quotationId, {
        reasonCode,
        reason: comment.trim() || null,
        acceptedTerms: false,
      });
      onDone(saved);
    } catch (error) {
      toast.error(errorMessage(error, "Could not record your decision."));
    } finally {
      setBusy(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: "Reject this quotation?",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, disabled: busy, children: "Go back" }),
        e.jsx(Button, {
          variant: "danger", loading: busy, disabled: !canSubmit, onClick: submit, children: "Reject",
        }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsx("p", { children: "Telling us why helps us make a better offer." }),
        e.jsxs("div", {
          className: "field",
          children: [
            e.jsx("label", { className: "field__label", children: "Reason" }),
            e.jsxs("select", {
              className: "field__input",
              value: reasonCode,
              onChange: (ev) => setReasonCode(ev.target.value),
              children: [
                e.jsx("option", { value: "", children: "Select a reason" }),
                ...REJECT_REASONS.map((r) => e.jsx("option", { value: r.value, children: r.label }, r.value)),
              ],
            }),
          ],
        }),
        e.jsx(Input, {
          label: needsComment ? "Tell us more" : "Comment (optional)",
          required: needsComment,
          value: comment,
          onChange: (ev) => setComment(ev.target.value),
        }),
      ],
    }),
  });
}

/* ── Detail ─────────────────────────────────────────────────────────────── */

function MyQuotationDetailPage() {
  const { id } = useParams();
  const toast = useToast();
  const navigate = useNavigate();
  const [quotation, setQuotation] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [approving, setApproving] = React.useState(false);
  const [rejecting, setRejecting] = React.useState(false);
  const [thanks, setThanks] = React.useState(false);

  const load = React.useCallback(async () => {
    if (!id) return;
    setLoading(true);
    try {
      setQuotation(await quotationsApi.mineById(Number(id)));
    } catch (error) {
      toast.error(errorMessage(error, "Failed to load the quotation."));
    } finally {
      setLoading(false);
    }
  }, [id]);

  React.useEffect(() => { void load(); }, [load]);

  if (loading) return e.jsx(Spinner, { center: true });
  if (!quotation) return null;

  const status = effectiveStatus(quotation);
  const open = status === QUOTATION_STATUS.Sent || status === QUOTATION_STATUS.Viewed;

  const banner = () => {
    if (status === QUOTATION_STATUS.Approved) {
      return e.jsx("div", {
        className: "qtn-banner qtn-banner--approved",
        children: e.jsxs("span", {
          children: [
            "You approved this quotation",
            quotation.respondedUtc ? ` on ${formatDateTime(quotation.respondedUtc)}` : "",
            ". Our team will share the invoice shortly.",
          ],
        }),
      });
    }
    if (status === QUOTATION_STATUS.Rejected) {
      return e.jsx("div", {
        className: "qtn-banner qtn-banner--rejected",
        children: e.jsxs("span", {
          children: [
            "You rejected this quotation",
            quotation.rejectionReason ? ` — ${quotation.rejectionReason}` : "",
            ".",
          ],
        }),
      });
    }
    if (status === QUOTATION_STATUS.Expired) {
      return e.jsxs("div", {
        className: "qtn-banner qtn-banner--expired",
        children: [
          e.jsx("span", { children: `This quotation expired on ${formatDate(quotation.validUntil)}.` }),
          e.jsx(Button, {
            size: "sm",
            variant: "outline",
            /* Straight into a ticket with the number already filled in (SS3.7). */
            onClick: () => navigate(`/support?subject=${encodeURIComponent(`Quotation ${quotation.quotationNumber}`)}`),
            children: "Contact Regalis",
          }),
        ],
      });
    }
    if (status === QUOTATION_STATUS.Revised) {
      return e.jsx("div", {
        className: "qtn-banner qtn-banner--revised",
        children: e.jsx("span", { children: "This quotation has been replaced by a newer one." }),
      });
    }
    if (status === QUOTATION_STATUS.Cancelled) {
      return e.jsx("div", {
        className: "qtn-banner qtn-banner--rejected",
        children: e.jsx("span", { children: "This quotation was withdrawn." }),
      });
    }
    return e.jsx("div", {
      className: "qtn-banner qtn-banner--sent",
      children: e.jsxs("span", {
        children: [`Valid until ${formatDate(quotation.validUntil)} · `, countdown(quotation)],
      }),
    });
  };

  if (thanks) {
    return e.jsx(Card, {
      children: e.jsxs("div", {
        className: "qtn-thanks",
        children: [
          e.jsx("div", { className: "qtn-thanks__mark", children: "✓" }),
          e.jsx("h3", { children: "Thank you!" }),
          e.jsx("p", { children: "Your quotation has been approved. Our team will share the invoice shortly." }),
          e.jsx(Button, { onClick: () => navigate("/quotations"), children: "Back to my quotations" }),
        ],
      }),
    });
  }

  return e.jsxs("div", {
    className: open ? "qtn-portal qtn-portal--decidable" : "qtn-portal",
    children: [
      e.jsx(PageHeader, {
        title: quotation.quotationNumber,
        description: `${formatDate(quotation.quotationDate)} · ${formatInr(quotation.totalAmount)}`,
        actions: e.jsxs(e.Fragment, {
          children: [
            e.jsx(Button, {
              variant: "ghost", onClick: () => navigate("/quotations"), children: "Back",
            }),
            e.jsx(Button, {
              variant: "outline",
              onClick: () => void quotationsApi.pdf(quotation.quotationId, quotation.quotationNumber),
              children: "Download PDF",
            }),
          ],
        }),
      }),

      e.jsx("div", {
        className: "qtn-portal__status",
        children: e.jsx(StatusPill, { quotation, forCustomer: true }),
      }),
      banner(),

      e.jsx(QuotationLinesCard, { quotation }),

      /* Approve / reject pinned to the bottom of the screen on a phone. */
      open && e.jsxs("div", {
        className: "qtn-decisionbar",
        children: [
          e.jsx(Button, {
            variant: "outline", onClick: () => setRejecting(true), children: "Reject",
          }),
          e.jsx(Button, { onClick: () => setApproving(true), children: "Approve" }),
        ],
      }),

      approving && e.jsx(ApproveModal, {
        quotation,
        onClose: () => setApproving(false),
        onDone: (saved) => { setApproving(false); setQuotation(saved); setThanks(true); },
      }),

      rejecting && e.jsx(RejectModal, {
        quotation,
        onClose: () => setRejecting(false),
        onDone: (saved) => {
          setRejecting(false);
          setQuotation(saved);
          toast.success("Thanks — we have recorded your decision.");
        },
      }),
    ],
  });
}

export { MyQuotationDetailPage, MyQuotationsPage };
