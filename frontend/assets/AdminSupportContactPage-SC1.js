/**
 * Admin -> Support & Contact.
 *
 *   AdminSupportContactPage  edits the contact details customers see on their
 *                            Support & Contact page (ContactUsPage-WEsv0zRR.js).
 *
 * Same look as the Customer Profile (AdminCustomersPages-IA5T7i_C.js): identity
 * card, uppercase icon labels, two-column field grid -- with inputs in place of
 * the read-only values. The values are the Support.* rows in MST_AppSetting,
 * read and saved through /api/settings exactly as Admin -> Settings does, so no
 * API change is needed and both screens always show the same values.
 *
 * This repo ships the built SPA only, so this chunk is hand-maintained: keep the
 * file name (the route in index-DI5X4c9f.js imports it by name) and bump its
 * revision in sw.js, or the service worker keeps serving the cached copy.
 */
import {
  a as useToast,
  u as useNavigate,
  r as React,
  A as ApiError,
  j as X,
  G as Spinner,
  p as Button,
  R as usePermission,
  i as makeIcon,
  ad as settingsApi,
  M as IconMail,
  P as IconPhone,
  ae as IconClock,
  af as IconMapPin
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";

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

const IconMessage = makeIcon("message-square", [
  ["path", { d: "M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z", key: "sc-msg-a" }]
]);
const IconCalendarDays = makeIcon("calendar-days", [
  ["rect", { width: "18", height: "18", x: "3", y: "4", rx: "2", key: "sc-cal-a" }],
  ["path", { d: "M16 2v4", key: "sc-cal-b" }],
  ["path", { d: "M8 2v4", key: "sc-cal-c" }],
  ["path", { d: "M3 10h18", key: "sc-cal-d" }]
]);
const IconHeadset = makeIcon("headset", [
  ["path", { d: "M3 14h3a2 2 0 0 1 2 2v3a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-7a9 9 0 0 1 18 0v7a2 2 0 0 1-2 2h-1a2 2 0 0 1-2-2v-3a2 2 0 0 1 2-2h3", key: "sc-hs-a" }]
]);

const DASH = "—";
const KEYS = {
  phone: "Support.ContactNumber",
  email: "Support.Email",
  hours: "Support.Hours",
  message: "Support.Message"
};
const MESSAGE_MAX = 300;

/** The day spans offered; anything else is kept as free text under "Custom". */
const DAY_OPTIONS = [
  { value: "Mon–Fri", label: "Monday to Friday" },
  { value: "Mon–Sat", label: "Monday to Saturday" },
  { value: "Mon–Sun", label: "Every day" },
  { value: "custom", label: "Custom…" }
];
/** 06:00 to 23:30 in half hours. */
const TIME_OPTIONS = Array.from({ length: 36 }, (_, i) => {
  const minutes = 6 * 60 + i * 30;
  return String(Math.floor(minutes / 60)).padStart(2, "0") + ":" + String(minutes % 60).padStart(2, "0");
});
/** "Mon–Sat 09:00–19:00 IST" -- the shape this screen writes back. */
const HOURS_PATTERN = /^(Mon–Fri|Mon–Sat|Mon–Sun)\s+(\d{2}:\d{2})\s*[–-]\s*(\d{2}:\d{2})\s+IST$/;

const PHONE_PATTERN = /^\+?[0-9][0-9 -]{6,18}[0-9]$/;
const EMAIL_PATTERN = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function message(error, fallback) {
  return error instanceof ApiError ? error.message : fallback;
}

function initials(name) {
  const parts = (name || "").trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  return (parts[0][0] + (parts.length > 1 ? parts[parts.length - 1][0] : "")).toUpperCase();
}

/** Splits a stored hours line into the dropdowns; unknown shapes go to Custom. */
function parseHours(value) {
  const match = HOURS_PATTERN.exec((value || "").trim());
  if (match && TIME_OPTIONS.includes(match[2]) && TIME_OPTIONS.includes(match[3])) {
    return { days: match[1], opens: match[2], closes: match[3], custom: "" };
  }
  if (!value) return { days: "Mon–Sat", opens: "09:00", closes: "19:00", custom: "" };
  return { days: "custom", opens: "09:00", closes: "19:00", custom: value };
}

function formatHours(form) {
  return form.days === "custom"
    ? form.custom.trim()
    : form.days + " " + form.opens + "–" + form.closes + " IST";
}

/** The four settings as the form edits them. */
function toForm(settings) {
  const value = (key) => settings.find((s) => s.settingKey === key)?.settingValue ?? "";
  return {
    phone: value(KEYS.phone),
    email: value(KEYS.email),
    message: value(KEYS.message),
    ...parseHours(value(KEYS.hours))
  };
}

function toValues(form) {
  return {
    [KEYS.phone]: form.phone.trim(),
    [KEYS.email]: form.email.trim(),
    [KEYS.hours]: formatHours(form),
    [KEYS.message]: form.message.trim()
  };
}

function validate(form) {
  const errors = {};
  const phone = form.phone.trim();
  const email = form.email.trim();
  if (phone && !PHONE_PATTERN.test(phone)) errors.phone = "Enter a valid phone number (digits, spaces, + or -).";
  if (email && !EMAIL_PATTERN.test(email)) errors.email = "Enter a valid email address.";
  if (form.days === "custom") {
    if (!form.custom.trim()) errors.hours = "Enter the support hours, or pick the days above.";
  } else if (form.opens >= form.closes) {
    errors.hours = "Closing time must be after opening time.";
  }
  if (form.message.trim().length > MESSAGE_MAX) {
    errors.message = "Keep the message under " + MESSAGE_MAX + " characters.";
  }
  return errors;
}

/* ------------------------------------------------------------------ parts */

/** A Customer Profile field (icon + uppercase label) with a control under it. */
function EditField({ icon, label, htmlFor, hint, error, wide, children }) {
  return el(
    "div",
    { className: "supc-field" + (wide ? " supc-field--wide" : "") },
    el("label", { className: "custp-field__label", htmlFor }, el(icon, { size: 14 }), label),
    el("div", { className: "supc-field__control" }, children),
    error
      ? el("span", { className: "field__error", role: "alert" }, error)
      : hint
        ? el("span", { className: "field__hint" }, hint)
        : null
  );
}

function input(props, error) {
  return el("input", {
    className: "field__input" + (error ? " field__input--error" : ""),
    "aria-invalid": !!error,
    ...props
  });
}

function select(props, options) {
  return el(
    "select",
    { className: "field__input", ...props },
    options.map((option) =>
      el("option", { key: option.value ?? option, value: option.value ?? option }, option.label ?? option)
    )
  );
}

/* ------------------------------------------------------------------- page */

function AdminSupportContactPage() {
  const toast = useToast();
  const navigate = useNavigate();
  const canEdit = usePermission("SETTING.EDIT");

  const [settings, setSettings] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [saving, setSaving] = React.useState(false);
  const [form, setForm] = React.useState(null);
  const [touched, setTouched] = React.useState(false);

  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      const rows = await settingsApi.list();
      setSettings(rows);
      setForm(toForm(rows));
      setTouched(false);
    } catch (error) {
      toast.error(message(error, "Failed to load the support details."));
    } finally {
      setLoading(false);
    }
  }, []);

  React.useEffect(() => {
    load();
  }, [load]);

  if (loading) return el(Spinner, { center: true });
  if (!settings || !form) return null;

  const companyName =
    settings.find((s) => s.settingKey === "Company.Name")?.settingValue || "Regalis WorkSpace";
  const companyAddress = settings.find((s) => s.settingKey === "Company.Address")?.settingValue || "";
  const companyEmail = settings.find((s) => s.settingKey === "Company.Email")?.settingValue || "";
  // A key that is missing, locked or secret is shown but not sent.
  const editable = (key) => {
    const row = settings.find((s) => s.settingKey === key);
    return !!row && row.isEditableByAdmin && !row.isSecret;
  };

  const saved = toValues(toForm(settings));
  const draft = toValues(form);
  const changed = Object.keys(draft).filter((key) => editable(key) && draft[key] !== saved[key]);
  const errors = validate(form);
  const shownErrors = touched ? errors : {};
  const hasErrors = Object.keys(errors).length > 0;
  const live = Boolean(saved[KEYS.phone] || saved[KEYS.email] || companyEmail);

  const set = (field) => (event) => setForm({ ...form, [field]: event.target.value });
  const disabled = (key) => !canEdit || saving || !editable(key);

  const cancel = () => {
    setForm(toForm(settings));
    setTouched(false);
  };

  const save = async (event) => {
    event.preventDefault();
    setTouched(true);
    if (hasErrors || changed.length === 0) return;
    setSaving(true);
    try {
      const rows = await settingsApi.update(
        changed.map((key) => ({ settingKey: key, settingValue: draft[key] }))
      );
      setSettings(rows);
      setForm(toForm(rows));
      setTouched(false);
      toast.success("Support details saved. Customers see them right away.");
    } catch (error) {
      toast.error(message(error, "Could not save the support details."));
    } finally {
      setSaving(false);
    }
  };

  // What the customer's Support & Contact card will show, from the draft.
  const preview = [
    { icon: IconPhone, label: "Support", value: draft[KEYS.phone] },
    { icon: IconMail, label: "Email", value: draft[KEYS.email] || companyEmail },
    { icon: IconClock, label: "Hours", value: draft[KEYS.hours] },
    { icon: IconMapPin, label: "Address", value: companyAddress }
  ].filter((row) => row.value);

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Support & Contact",
      description: "The contact details customers see on their Support & Contact page.",
      actions: el(
        Button,
        { variant: "outline", onClick: () => navigate("/admin/tickets") },
        el(IconHeadset, { size: 16 }),
        " Support Tickets"
      )
    }),

    // identity
    el(
      Card,
      null,
      el(
        "div",
        { className: "custp-id" },
        el("div", { className: "custp-id__avatar" }, initials(companyName)),
        el(
          "div",
          { className: "custp-id__main" },
          el("div", { className: "custp-id__name" }, companyName),
          el(
            "div",
            { className: "custp-id__meta" },
            "Customer Portal  ·  Support & Contact",
            companyAddress ? "  ·  " + companyAddress : ""
          )
        ),
        el(
          "div",
          { className: "custp-id__actions" },
          changed.length > 0 ? el(Badge, { tone: "warning" }, "Unsaved changes") : null,
          el(Badge, { tone: live ? "success" : "warning" }, live ? "Visible to Customers" : "Not set up"),
          canEdit ? null : el(Badge, { tone: "neutral" }, "View only")
        )
      )
    ),

    el(
      "form",
      { className: "supc-form", onSubmit: save, noValidate: true },
      el(
        Card,
        { title: "Contact Details", actions: el(Badge, { tone: "info" }, "Shown to Customers") },
        el(
          "div",
          { className: "custp-fields" },
          el(
            EditField,
            {
              icon: IconPhone,
              label: "Support Number",
              htmlFor: "supc-phone",
              hint: "Shown as Support on the customer page.",
              error: shownErrors.phone
            },
            input(
              {
                id: "supc-phone",
                type: "tel",
                inputMode: "tel",
                autoComplete: "off",
                placeholder: "e.g. 7483337555",
                value: form.phone,
                disabled: disabled(KEYS.phone),
                onChange: set("phone")
              },
              shownErrors.phone
            )
          ),
          el(
            EditField,
            {
              icon: IconMail,
              label: "Support Email",
              htmlFor: "supc-email",
              hint: companyEmail
                ? "Leave empty to show the company email (" + companyEmail + ")."
                : "Shown as Email on the customer page.",
              error: shownErrors.email
            },
            input(
              {
                id: "supc-email",
                type: "email",
                autoComplete: "off",
                placeholder: "e.g. support@regalis.in",
                value: form.email,
                disabled: disabled(KEYS.email),
                onChange: set("email")
              },
              shownErrors.email
            )
          ),
          el(
            EditField,
            { icon: IconCalendarDays, label: "Support Days", htmlFor: "supc-days" },
            select(
              {
                id: "supc-days",
                value: form.days,
                disabled: disabled(KEYS.hours),
                onChange: (event) =>
                  setForm({
                    ...form,
                    days: event.target.value,
                    // Starting a custom line from what the dropdowns said keeps the edit small.
                    custom:
                      event.target.value === "custom" && !form.custom ? formatHours(form) : form.custom
                  })
              },
              DAY_OPTIONS
            )
          ),
          form.days === "custom"
            ? el(
                EditField,
                {
                  icon: IconClock,
                  label: "Support Hours",
                  htmlFor: "supc-hours",
                  hint: "Free text, e.g. Mon–Sat 09:00–19:00 IST, Sun closed.",
                  error: shownErrors.hours
                },
                input(
                  {
                    id: "supc-hours",
                    type: "text",
                    value: form.custom,
                    disabled: disabled(KEYS.hours),
                    onChange: set("custom")
                  },
                  shownErrors.hours
                )
              )
            : el(
                EditField,
                {
                  icon: IconClock,
                  label: "Support Hours (IST)",
                  htmlFor: "supc-opens",
                  error: shownErrors.hours
                },
                el(
                  "div",
                  { className: "supc-hours" },
                  select(
                    {
                      id: "supc-opens",
                      "aria-label": "Opens at",
                      value: form.opens,
                      disabled: disabled(KEYS.hours),
                      onChange: set("opens")
                    },
                    TIME_OPTIONS
                  ),
                  el("span", { className: "supc-hours__to" }, "to"),
                  select(
                    {
                      "aria-label": "Closes at",
                      value: form.closes,
                      disabled: disabled(KEYS.hours),
                      onChange: set("closes")
                    },
                    TIME_OPTIONS
                  )
                )
              ),
          el(
            EditField,
            {
              icon: IconMessage,
              label: "Support Message",
              htmlFor: "supc-message",
              wide: true,
              hint: form.message.length + " / " + MESSAGE_MAX + " — shown under the Contact Us heading.",
              error: shownErrors.message
            },
            el("textarea", {
              id: "supc-message",
              className: "field__input supc-textarea" + (shownErrors.message ? " field__input--error" : ""),
              rows: 3,
              placeholder: "e.g. We usually respond within one business day.",
              value: form.message,
              disabled: disabled(KEYS.message),
              onChange: set("message")
            })
          )
        ),
        canEdit
          ? el(
              "div",
              { className: "supc-actions" },
              el(
                Button,
                { variant: "outline", onClick: cancel, disabled: saving || changed.length === 0 },
                "Cancel"
              ),
              el(
                Button,
                {
                  type: "submit",
                  loading: saving,
                  disabled: changed.length === 0 || (touched && hasErrors)
                },
                "Save Changes"
              )
            )
          : null
      )
    ),

    // the customer's view, from the unsaved draft
    el(
      Card,
      { title: "Customer Preview", actions: el(Badge, { tone: "neutral" }, "As customers see it") },
      el("div", { className: "supc-preview__title" }, companyName),
      draft[KEYS.message]
        ? el("div", { className: "supc-preview__message" }, draft[KEYS.message])
        : null,
      preview.length === 0
        ? el(
            "div",
            { className: "custp-field__value custp-field__value--muted" },
            "Contact details are being set up " + DASH + " customers are asked to raise a ticket instead."
          )
        : el(
            "div",
            { className: "supc-preview" },
            preview.map(({ icon, label, value }) =>
              el(
                "div",
                { key: label, className: "supc-preview__row" },
                el(icon, { size: 18 }),
                el(
                  "div",
                  null,
                  el("div", { className: "supc-preview__label" }, label),
                  el("div", { className: "supc-preview__value" }, value)
                )
              )
            )
          )
    )
  );
}

export { AdminSupportContactPage };
