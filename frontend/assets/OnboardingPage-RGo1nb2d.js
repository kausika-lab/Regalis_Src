/* Onboarding — the details we cannot get from Google.

   A Google sign-in gives us a name and a verified email and nothing else. An invoice
   needs a billing address and a GSTIN, so a member who arrived that way is sent here
   before they can book or buy. The same form repairs accounts that predate the
   structured address columns, which is every account created before this shipped.

   Written as a readable module rather than minified: this chunk is maintained by hand,
   like the rest of the patched-in-place bundle. It imports the shared components from
   the index chunk by their published export letters — see the export map at the bottom
   of index-DI5X4c9f.js:
       j  jsx runtime      r  React            f  AuthField
       e  Brand mark       l  auth store       N  Navigate
       D  unwrap(response) E  http client      a  useToast
       A  ApiClientError

   The page renders inside the `.auth` layout, which is what supplies the --auth-* CSS
   variables the auth__ classes depend on. Mounting it anywhere else makes it unstyled. */

import {
  j as jsx,
  r as React,
  f as AuthField,
  e as Brand,
  l as authStore,
  N as Navigate,
  D as unwrap,
  E as http,
  a as useToast,
  A as ApiClientError
} from "./index-DI5X4c9f.js";

/* Matches Regalis.Domain.Enums.Gender. The values are the wire format. */
const GENDERS = [
  { value: "1", label: "Male" },
  { value: "2", label: "Female" },
  { value: "3", label: "Other" },
  { value: "4", label: "Prefer not to say" }
];

const icon = (...paths) => jsx.jsxs("svg", {
  xmlns: "http://www.w3.org/2000/svg", width: 19, height: 19, viewBox: "0 0 24 24",
  fill: "none", stroke: "currentColor", strokeWidth: 1.9,
  strokeLinecap: "round", strokeLinejoin: "round", "aria-hidden": true,
  children: paths
});

const ICONS = {
  user: icon(
    jsx.jsx("path", { d: "M19 21v-2a4 4 0 0 0-4-4H9a4 4 0 0 0-4 4v2" }, "a"),
    jsx.jsx("circle", { cx: 12, cy: 7, r: 4 }, "b")),
  phone: icon(jsx.jsx("path", {
    d: "M22 16.92v3a2 2 0 0 1-2.18 2 19.79 19.79 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.79 19.79 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.13.96.36 1.9.68 2.81a2 2 0 0 1-.45 2.11L8.09 9.91a16 16 0 0 0 6 6l1.27-1.27a2 2 0 0 1 2.11-.45c.91.32 1.85.55 2.81.68A2 2 0 0 1 22 16.92Z"
  }, "a")),
  building: icon(
    jsx.jsx("rect", { x: 4, y: 2, width: 16, height: 20, rx: 2 }, "a"),
    jsx.jsx("path", { d: "M9 22v-4h6v4M8 6h.01M16 6h.01M8 10h.01M16 10h.01M8 14h.01M16 14h.01" }, "b")),
  pin: icon(
    jsx.jsx("path", { d: "M20 10c0 6-8 12-8 12s-8-6-8-12a8 8 0 0 1 16 0Z" }, "a"),
    jsx.jsx("circle", { cx: 12, cy: 10, r: 3 }, "b")),
  globe: icon(
    jsx.jsx("circle", { cx: 12, cy: 12, r: 10 }, "a"),
    jsx.jsx("path", { d: "M2 12h20M12 2a15.3 15.3 0 0 1 4 10 15.3 15.3 0 0 1-4 10 15.3 15.3 0 0 1-4-10 15.3 15.3 0 0 1 4-10Z" }, "b")),
  calendar: icon(
    jsx.jsx("rect", { x: 3, y: 4, width: 18, height: 18, rx: 2 }, "a"),
    jsx.jsx("path", { d: "M16 2v4M8 2v4M3 10h18" }, "b")),
  receipt: icon(
    jsx.jsx("path", { d: "M4 2v20l2-1 2 1 2-1 2 1 2-1 2 1 2-1V2l-2 1-2-1-2 1-2-1-2 1-2-1Z" }, "a"),
    jsx.jsx("path", { d: "M8 8h8M8 12h8M8 16h5" }, "b"))
};

/* label, key, icon, and the input attributes that make a browser autofill usefully. */
const FIELDS = [
  ["Full Name",      "fullName",     ICONS.user,     { autoComplete: "name", placeholder: "Enter full name" }],
  ["Phone Number",   "mobileNumber", ICONS.phone,    { autoComplete: "tel", placeholder: "10-digit mobile number" }],
  ["Company Name",   "companyName",  ICONS.building, { placeholder: "Registered business name", optional: true }],
  ["Address Line 1", "addressLine1", ICONS.pin,      { autoComplete: "address-line1", placeholder: "Flat / building / street" }],
  ["Address Line 2", "addressLine2", ICONS.pin,      { autoComplete: "address-line2", placeholder: "Area / landmark" }],
  ["City",           "city",         ICONS.pin,      { autoComplete: "address-level2", placeholder: "Enter city" }],
  ["State",          "state",        ICONS.pin,      { autoComplete: "address-level1", placeholder: "Enter state" }],
  ["Country",        "country",      ICONS.globe,    { autoComplete: "country-name", placeholder: "Enter country" }],
  ["Postal Code",    "postalCode",   ICONS.pin,      { autoComplete: "postal-code", placeholder: "6-digit PIN", inputMode: "numeric", maxLength: 6 }],
  ["Date of Birth",  "dateOfBirth",  ICONS.calendar, { autoComplete: "bday", type: "date" }],
  ["GST Number",     "gstin",        ICONS.receipt,  { placeholder: "15-character GSTIN", maxLength: 15, style: { textTransform: "uppercase" }, optional: true }]
];

const EMPTY = {
  fullName: "", mobileNumber: "", companyName: "", addressLine1: "", addressLine2: "",
  city: "", state: "", country: "India", postalCode: "", dateOfBirth: "", gender: "", gstin: ""
};

export function OnboardingPage() {
  const toast = useToast();
  const updateUser = authStore(s => s.updateUser);
  const clearSession = authStore(s => s.clearSession);
  const alreadyComplete = authStore(s => s.user?.isProfileComplete === true);

  const [phase, setPhase] = React.useState("loading");   // loading | ready | saved | error
  const [form, setForm] = React.useState(EMPTY);
  const [errors, setErrors] = React.useState({});
  const [saving, setSaving] = React.useState(false);

  React.useEffect(() => {
    let cancelled = false;
    unwrap(http.get("/profile/onboarding"))
      .then(status => {
        if (cancelled) return;
        if (status.isComplete) { setPhase("saved"); return; }
        const p = status.profile ?? {};
        /* Pre-fill whatever we already hold — the name from Google, and anything a
           half-finished registration left behind — so nobody retypes what we have. */
        setForm({
          ...EMPTY,
          fullName: p.fullName ?? "",
          mobileNumber: p.mobileNumber ?? "",
          companyName: p.companyName ?? "",
          addressLine1: p.addressLine1 ?? "",
          addressLine2: p.addressLine2 ?? "",
          city: p.city ?? "",
          state: p.state ?? "",
          country: p.country ?? "India",
          postalCode: p.postalCode ?? "",
          dateOfBirth: p.dateOfBirth ?? "",
          gender: p.gender != null ? String(p.gender) : "",
          gstin: p.gstin ?? ""
        });
        setPhase("ready");
      })
      .catch(() => { if (!cancelled) setPhase("error"); });
    return () => { cancelled = true; };
  }, []);

  const set = (key, value) => {
    setForm(f => ({ ...f, [key]: value }));
    /* Clear the field's error as soon as it is touched — leaving a stale complaint
       under a field the member has just fixed reads as though it is still wrong. */
    setErrors(e => (e[key] ? { ...e, [key]: undefined } : e));
  };

  const submit = async event => {
    event.preventDefault();
    setSaving(true);
    setErrors({});
    try {
      await unwrap(http.post("/profile/onboarding", {
        fullName: form.fullName.trim(),
        mobileNumber: form.mobileNumber.trim(),
        companyName: form.companyName.trim(),
        addressLine1: form.addressLine1.trim(),
        addressLine2: form.addressLine2.trim(),
        city: form.city.trim(),
        state: form.state.trim(),
        country: form.country.trim(),
        postalCode: form.postalCode.trim(),
        dateOfBirth: form.dateOfBirth,
        gender: Number(form.gender),
        gstin: form.gstin.trim().toUpperCase()
      }));
      /* The route guard reads this flag, so it has to be set before we navigate or the
         guard bounces us straight back here. */
      updateUser({ isProfileComplete: true });
      toast.success("Thanks — your profile is complete.");
      setPhase("saved");
    } catch (error) {
      if (error instanceof ApiClientError) {
        /* FluentValidation returns the offending field on each error; anything without
           one is a rule that spans the form and belongs in the toast. */
        const byField = {};
        for (const item of error.errors) {
          if (item.field) byField[lowerFirst(item.field)] = item.message;
        }
        setErrors(byField);
        toast.error(Object.keys(byField).length
          ? "Check the highlighted fields."
          : error.message);
      } else {
        toast.error("Could not save your details. Please try again.");
      }
    } finally {
      setSaving(false);
    }
  };

  if (phase === "saved" || alreadyComplete) {
    return jsx.jsx(Navigate, { to: "/", replace: true });
  }

  const panel = children => jsx.jsxs("div", {
    className: "auth__panel auth__panel--wide",
    children: [jsx.jsx(Brand, {}), ...children]
  });

  if (phase === "loading") {
    return panel([
      jsx.jsx("h1", { className: "auth__title", children: "One moment" }, "t"),
      jsx.jsx("p", { className: "auth__subtitle", children: "Loading your details…" }, "s")
    ]);
  }

  if (phase === "error") {
    return panel([
      jsx.jsx("h1", { className: "auth__title", children: "We could not load your details" }, "t"),
      jsx.jsx("p", {
        className: "auth__subtitle",
        children: "Check your connection and reload the page. Nothing has been changed."
      }, "s")
    ]);
  }

  return panel([
    jsx.jsx("h1", { className: "auth__title", children: "Complete Your Profile" }, "t"),
    jsx.jsxs("p", {
      className: "auth__subtitle",
      children: [
        "We need a few more details before you can book a workspace — your invoices are ",
        "issued against them. Fields marked * are required; company name and GST number are optional.",
        " ",
        jsx.jsx("span", { className: "auth__required", children: "*" }, "r")
      ]
    }, "s"),
    jsx.jsxs("form", {
      onSubmit: submit,
      noValidate: true,
      children: [
        jsx.jsxs("div", {
          className: "auth__grid",
          children: [
            ...FIELDS.map(([label, key, glyph, attrs]) => jsx.jsx(AuthField, {
              label,
              icon: glyph,
              error: errors[key],
              value: form[key],
              onChange: event => set(key, event.target.value),
              ...attrs
            }, key)),
            /* Gender is a select, which AuthField does not render, so the markup below
               mirrors what AuthField produces to keep the row visually identical. */
            jsx.jsxs("div", {
              children: [
                jsx.jsxs("div", {
                  className: "auth__label",
                  children: ["Gender", " ", jsx.jsx("span", {
                    className: "auth__required", "aria-hidden": true, children: "*"
                  }, "r")]
                }, "l"),
                jsx.jsxs("div", {
                  className: `auth__input${errors.gender ? " auth__input--error" : ""}`,
                  children: [
                    ICONS.user,
                    jsx.jsxs("select", {
                      value: form.gender,
                      onChange: event => set("gender", event.target.value),
                      "aria-invalid": !!errors.gender,
                      style: {
                        flex: 1, minWidth: 0, border: "none", background: "transparent",
                        fontFamily: "var(--auth-font-body)", fontSize: "15.5px",
                        color: "var(--auth-label)", outline: "none", height: "100%"
                      },
                      children: [
                        jsx.jsx("option", { value: "", children: "Select gender" }, ""),
                        ...GENDERS.map(g => jsx.jsx("option", { value: g.value, children: g.label }, g.value))
                      ]
                    }, "s")
                  ]
                }, "i"),
                jsx.jsx("div", {
                  className: "auth__error",
                  role: errors.gender ? "alert" : undefined,
                  children: errors.gender
                }, "e")
              ]
            }, "gender")
          ]
        }, "grid"),
        jsx.jsx("button", {
          type: "submit",
          className: "auth__btn",
          disabled: saving,
          children: saving ? "Saving…" : "Save and Continue"
        }, "btn"),
        /* Without this the member is stuck: there is no navigation on this page by
           design, so signing out has to be reachable from it. */
        jsx.jsxs("p", {
          className: "auth__footer",
          children: [
            "Not you? ",
            jsx.jsx("a", {
              href: "/login",
              onClick: event => { event.preventDefault(); clearSession(); window.location.assign("/login"); },
              children: "Sign out"
            }, "out")
          ]
        }, "foot")
      ]
    }, "form")
  ]);
}

/* The API returns PascalCase field names; the form keys are camelCase. */
function lowerFirst(value) {
  return value.charAt(0).toLowerCase() + value.slice(1);
}
