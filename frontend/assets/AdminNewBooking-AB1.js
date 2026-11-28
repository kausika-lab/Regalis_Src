/**
 * Admin > Bookings > New booking (27 Nov 2026, kit deploy/admin-booking).
 *
 * This is the customer "Book a Workspace" page itself (BookWorkspacePage-MB2.js), rendered with
 * props.admin for a customer the staff user picks here. Same steps, fields, validation and engine
 * as the customer portal; the differences are inside that page and only apply when props.admin is
 * set: a Customer and a Location field, no Customisation tile, "Review & confirm" with a single
 * Confirm button (no wallet, online or pay-later choice), and every write goes to
 * /api/admin/on-behalf/{customerId}/..., which confirms the booking without payment. The invoice
 * stays due until the payment is recorded on Finance > Payments.
 *
 * Hand-maintained chunk, imported by AdminBookingsPage-BK2.js (apply.mjs).
 */
import { r as React, j as X, p as Button, I as Input, F as Select } from "./index-DI5X4c9f.js";
import { u as users } from "./admin-DtGep2Hs.js";
import { u as useDebounce } from "./useDebounce-DSDIQF2c.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { BookWorkspacePage } from "./BookWorkspacePage-MB2.js";

const label = (u) => [u.fullName, u.email, u.mobileNumber].filter(Boolean).join(" · ");

/** Find a customer by name, email or mobile (the search the old modal used), then pick one. */
function CustomerField({ customer, onChange }) {
  const [q, setQ] = React.useState("");
  const [found, setFound] = React.useState([]);
  /* The search term the current results belong to, so an empty result can say so (27 Nov 2026:
     "kaushika" found nothing for "Kausika R" and the box still read "Search for the customer first"). */
  const [searched, setSearched] = React.useState("");
  const dq = useDebounce(q, 350);
  React.useEffect(() => {
    if (!dq.trim()) {
      setFound([]);
      setSearched("");
      return;
    }
    let dead = false;
    users
      .list({ page: 1, pageSize: 8, search: dq.trim(), userType: 1 })
      .then((r) => {
        if (dead) return;
        const list = (r?.items ?? []).filter((u) => u.status === void 0 || u.status === 1);
        setFound(list);
        setSearched(dq.trim());
        /* One match: pick it, so the name shows straight away. */
        if (list.length === 1) onChange(list[0]);
      })
      .catch(() => {
        if (dead) return;
        setFound([]);
        setSearched(dq.trim());
      });
    return () => {
      dead = true;
    };
  }, [dq]);
  const options = [...(customer && !found.some((u) => u.userId === customer.userId) ? [customer] : []), ...found];
  return X.jsxs(X.Fragment, {
    children: [
      X.jsx(Input, {
        label: "Find customer",
        placeholder: "Name, email or mobile…",
        value: q,
        autoComplete: "off",
        "data-rg": "admin-booking-search",
        onChange: (e) => setQ(e.target.value),
      }),
      X.jsx(Select, {
        label: "Customer",
        required: !0,
        value: customer ? String(customer.userId) : "",
        placeholder: options.length
          ? `Select customer (${options.length} found)`
          : searched
            ? `No customer matches "${searched}" - check the spelling or try the email or mobile`
            : "Search for the customer first",
        options: options.map((u) => ({ value: String(u.userId), label: label(u) })),
        "data-rg": "admin-booking-customer",
        onChange: (e) => onChange(options.find((u) => String(u.userId) === e.target.value) ?? null),
      }),
    ],
  });
}

export function AdminNewBooking({ onClose, onDone }) {
  const [customer, setCustomer] = React.useState(null);
  React.useEffect(() => {
    window.scrollTo(0, 0);
  }, []);
  return X.jsxs("div", {
    "data-rg": "admin-new-booking",
    children: [
      X.jsx(PageHeader, {
        title: "New booking",
        description: "Book for a customer with the same steps as the customer portal. The booking is confirmed straight away. Nothing is collected here: it is billed through the customer's quotation.",
        actions: X.jsx(Button, { variant: "ghost", onClick: onClose, children: "Back to bookings" }),
      }),
      X.jsx(BookWorkspacePage, {
        admin: {
          customerId: customer ? customer.userId : null,
          customerName: customer ? customer.fullName : null,
          header: X.jsx(CustomerField, { customer, onChange: setCustomer }),
          onDone,
        },
      }),
    ],
  });
}
