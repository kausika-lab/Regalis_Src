/**
 * Shared mobile number + email validation (13 Nov 2026, kit deploy/admin-customer).
 *
 * Storage: one MST_User.MobileNumber column. New saves are "+<country code><digits>",
 * e.g. "+919876543210" (no spaces), which the API's ^\+?[0-9]{10,15}$ accepts. Older rows
 * hold bare Indian 10-digit numbers ("9876543210"); they are read as +91 and saved in the
 * new form the next time the record is edited. The API treats both forms as the same
 * number when it checks for duplicates (MobileNumbers.Aliases).
 *
 * Used by Register, Onboarding, Profile (customer + admin) and Admin -> Users / Staff.
 * Hand-maintained chunk: bump its revision in sw.js when it changes.
 */
import { r as React, j as X } from "./index-DI5X4c9f.js";

/** Country codes offered in the dropdown. len = allowed digit counts after the code. */
export const COUNTRIES = [
  { iso: "IN", name: "India", dial: "91", len: [10], start: /^[6-9]/ },
  { iso: "AE", name: "United Arab Emirates", dial: "971", len: [9] },
  { iso: "SA", name: "Saudi Arabia", dial: "966", len: [9] },
  { iso: "QA", name: "Qatar", dial: "974", len: [8] },
  { iso: "KW", name: "Kuwait", dial: "965", len: [8] },
  { iso: "OM", name: "Oman", dial: "968", len: [8] },
  { iso: "BH", name: "Bahrain", dial: "973", len: [8] },
  { iso: "SG", name: "Singapore", dial: "65", len: [8] },
  { iso: "MY", name: "Malaysia", dial: "60", len: [9, 10] },
  { iso: "LK", name: "Sri Lanka", dial: "94", len: [9] },
  { iso: "NP", name: "Nepal", dial: "977", len: [10] },
  { iso: "BD", name: "Bangladesh", dial: "880", len: [10] },
  { iso: "US", name: "United States / Canada", dial: "1", len: [10] },
  { iso: "GB", name: "United Kingdom", dial: "44", len: [10] },
  { iso: "IE", name: "Ireland", dial: "353", len: [9] },
  { iso: "DE", name: "Germany", dial: "49", len: [10, 11] },
  { iso: "FR", name: "France", dial: "33", len: [9] },
  { iso: "AU", name: "Australia", dial: "61", len: [9] },
  { iso: "NZ", name: "New Zealand", dial: "64", len: [8, 9, 10] },
  { iso: "ZA", name: "South Africa", dial: "27", len: [9] },
  { iso: "HK", name: "Hong Kong", dial: "852", len: [8] },
  { iso: "JP", name: "Japan", dial: "81", len: [10] },
  { iso: "CN", name: "China", dial: "86", len: [11] }
];
export const DEFAULT_DIAL = "91";
const BY_DIAL = Object.fromEntries(COUNTRIES.map((c) => [c.dial, c]));
const DIALS_LONGEST_FIRST = COUNTRIES.map((c) => c.dial).sort((a, b) => b.length - a.length);

const digitsOf = (s) => String(s ?? "").replace(/\D/g, "");

/** Stored value -> { dial, number }. Bare digits (old rows) are read as India. */
export function parsePhone(stored) {
  const raw = String(stored ?? "").trim();
  if (!raw) return { dial: DEFAULT_DIAL, number: "" };
  const d = digitsOf(raw);
  if (raw.startsWith("+")) {
    const dial = DIALS_LONGEST_FIRST.find((c) => d.startsWith(c));
    if (dial) return { dial, number: d.slice(dial.length) };
    return { dial: "", number: d };
  }
  return { dial: DEFAULT_DIAL, number: d };
}

/** { dial, number } -> stored value "+<dial><digits>"; "" when no number. */
export function formatPhone(dial, number) {
  const n = digitsOf(number);
  if (!n) return "";
  return dial ? `+${dial}${n}` : n;
}

/** Stored value -> "+91 9876543210" for screens (code and number shown apart). */
export function displayPhone(stored) {
  if (!stored) return "";
  const { dial, number } = parsePhone(stored);
  return dial ? `+${dial} ${number}` : number;
}

/** Error text for a stored value, or null when valid. */
export function phoneError(stored, { required = true } = {}) {
  const { dial, number } = parsePhone(stored);
  if (!number) return required ? "Enter a mobile number." : null;
  const c = BY_DIAL[dial];
  if (!c) return "Pick a country code from the list.";
  if (!c.len.includes(number.length)) {
    const want = c.len.length === 1 ? `${c.len[0]}` : `${c.len[0]}–${c.len[c.len.length - 1]}`;
    return `Enter a ${want}-digit mobile number for ${c.name} (+${c.dial}).`;
  }
  if (c.start && !c.start.test(number)) return `A ${c.name} mobile number starts with 6, 7, 8 or 9.`;
  return null;
}

/** Practical email check: one @, a dotted domain with a 2+ letter ending, no spaces,
 *  no leading/trailing/double dots in the name part. */
export const EMAIL_RE = /^(?!\.)(?!.*\.\.)[A-Za-z0-9._%+'-]+(?<!\.)@(?:[A-Za-z0-9](?:[A-Za-z0-9-]{0,61}[A-Za-z0-9])?\.)+[A-Za-z]{2,24}$/;

export function emailError(value, { required = true } = {}) {
  const v = String(value ?? "").trim();
  if (!v) return required ? "Enter an email address." : null;
  if (v.length > 254) return "This email address is too long.";
  if (!EMAIL_RE.test(v)) return "Enter a valid email address, e.g. name@example.com.";
  return null;
}

/* ------------------------------------------------------------------ component */

function useSplit(value, onChange) {
  const [parts, setParts] = React.useState(() => parsePhone(value));
  React.useEffect(() => {
    if ((value ?? "") !== formatPhone(parts.dial, parts.number)) setParts(parsePhone(value));
  }, [value]);
  const update = (next) => {
    setParts(next);
    onChange && onChange(formatPhone(next.dial, next.number));
  };
  const country = BY_DIAL[parts.dial];
  const maxLen = country ? Math.max(...country.len) : 15;
  const onNumber = (e) => {
    let d = digitsOf(e.target.value);
    // Pasted with the country code or a leading 0: keep only the subscriber number.
    if (country && d.length > maxLen && d.startsWith(parts.dial)) d = d.slice(parts.dial.length);
    if (d.length > maxLen && d.startsWith("0")) d = d.replace(/^0+/, "");
    update({ dial: parts.dial, number: d.slice(0, maxLen) });
  };
  const onDial = (e) => update({ dial: e.target.value, number: parts.number });
  const options = [
    ...(parts.dial && !country ? [{ dial: parts.dial, iso: "", name: "Other" }] : []),
    ...COUNTRIES
  ];
  const placeholder = country
    ? country.len.length === 1 ? `${country.len[0]}-digit number` : `${country.len[0]}–${country.len[country.len.length - 1]} digits`
    : "Mobile number";
  return { parts, onNumber, onDial, options, maxLen, placeholder };
}

const opt = (c) => X.jsx("option", { value: c.dial, title: c.name, children: `${c.iso ? c.iso + " " : ""}+${c.dial}` }, c.iso + c.dial);

/**
 * Country code dropdown + digits-only number input.
 * value / onChange carry the stored form ("+919876543210").
 * variant "auth" = the sign-up / profile look (auth__*), "app" = admin forms (field__*).
 */
export function PhoneField({
  label = "Mobile Number",
  value,
  onChange,
  onBlur,
  error,
  hint,
  required = true,
  disabled = false,
  variant = "app",
  icon = null,
  id
}) {
  const auto = React.useId();
  const fid = id ?? auto;
  const { parts, onNumber, onDial, options, maxLen, placeholder } = useSplit(value, onChange);
  const select = X.jsx("select", {
    className: variant === "auth" ? "rg-phone__cc rg-phone__cc--auth" : "field__input rg-phone__cc",
    "aria-label": "Country code",
    value: parts.dial,
    disabled,
    onChange: onDial,
    onBlur,
    children: options.map(opt)
  });
  const input = X.jsx("input", {
    id: fid,
    type: "tel",
    inputMode: "numeric",
    autoComplete: "tel-national",
    pattern: "[0-9]*",
    maxLength: maxLen,
    placeholder,
    value: parts.number,
    disabled,
    "aria-invalid": !!error,
    "aria-describedby": error ? `${fid}-error` : void 0,
    className: variant === "auth" ? "rg-phone__num" : `field__input rg-phone__num${error ? " field__input--error" : ""}`,
    onKeyDown: (e) => {
      if (e.key.length === 1 && !/[0-9]/.test(e.key) && !e.ctrlKey && !e.metaKey) e.preventDefault();
    },
    onChange: onNumber,
    onBlur
  });
  if (variant === "auth") {
    return X.jsxs("div", {
      className: "rg-phone-field",
      children: [
        X.jsxs("label", {
          className: "auth__label",
          htmlFor: fid,
          children: [
            label,
            " ",
            required
              ? X.jsx("span", { className: "auth__required", "aria-hidden": !0, children: "*" })
              : X.jsx("span", { className: "auth__optional", children: "(Optional)" })
          ]
        }),
        X.jsxs("div", {
          className: `auth__input rg-phone rg-phone--auth${error ? " auth__input--error" : ""}`,
          children: [icon, select, X.jsx("span", { className: "rg-phone__sep", "aria-hidden": !0 }), input]
        }),
        X.jsx("div", { className: "auth__error", id: `${fid}-error`, role: error ? "alert" : void 0, children: error || (hint ?? null) })
      ]
    });
  }
  return X.jsxs("div", {
    className: "field rg-phone-field",
    children: [
      X.jsxs("label", {
        className: "field__label",
        htmlFor: fid,
        children: [label, required && X.jsxs("span", { className: "field__required", "aria-hidden": !0, children: [" ", "*"] })]
      }),
      X.jsxs("div", { className: "rg-phone", children: [select, input] }),
      hint && !error && X.jsx("span", { className: "field__hint", children: hint }),
      error && X.jsx("span", { className: "field__error", id: `${fid}-error`, role: "alert", children: error })
    ]
  });
}
