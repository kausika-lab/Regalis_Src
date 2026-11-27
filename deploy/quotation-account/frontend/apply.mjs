// quotation-account kit (27 Nov 2026): frontend part.
//   node apply.mjs <frontend dir>
// Needs the quotation-invoice kit's frontend (its approval toast is the anchor). No new files.
// String edits that must each match exactly once; an edit already present is skipped (idempotent). Anchors
// are matched with LF or CRLF line ends, whichever the chunk uses. Any anchor not found stops the script
// before that file is written.
//
//   AdminQuotationPages-COWOrOkL.js  Quotation page: an approved quotation without an invoice says why (the
//                                    "No invoice raised: ..." timeline note) and Convert to invoice opens a
//                                    small dialog (email, mobile) when it has no customer account; it posts
//                                    /quotations/{id}/raise-invoice. Mark approved: a warning toast with the
//                                    reason when no invoice was raised.
//   BillingMoneyIn-BF1.js            Invoices: search placeholder names the quotation; the Source column is
//                                    headed "Quotation / source" (it already showed the quotation number).
//   sw.js                            precache revisions for the two chunks and index.html
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "quotation-account-1";
const A = (f) => path.join(front, "assets", f);
const changed = new Set();

function patch(file, edits) {
  const p = A(file);
  if (!fs.existsSync(p)) throw new Error(`${file} not found in ${front}/assets`);
  let t = fs.readFileSync(p, "utf8");
  const eol = (s) => (t.includes("\r\n") ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo] of edits) {
    const from = eol(rawFrom);
    const to = eol(rawTo);
    if (t.split(to).length - 1 === 1) {
      console.log(`OK  - ${file}: ${name} (already done)`);
      continue;
    }
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`${file}: '${name}' expected 1 match, found ${n}; nothing written for this file`);
    t = t.split(from).join(to);
    console.log(`OK  - ${file}: ${name}`);
    changed.add(file);
  }
  fs.writeFileSync(p, t, "utf8");
}

const EDITS = {
 "AdminQuotationPages-COWOrOkL.js": [
  [
   "api: raise-invoice",
   "  recordDecision: (id, payload) => unwrap(api.post(`/quotations/${id}/record-decision`, payload)),\n",
   "  recordDecision: (id, payload) => unwrap(api.post(`/quotations/${id}/record-decision`, payload)),\n  // quotation-account kit: Convert to invoice for a quotation without a customer account (links or creates it).\n  raiseInvoice: (id, payload) => unwrap(api.post(`/quotations/${id}/raise-invoice`, payload)),\n"
  ],
  [
   "helper: why no invoice",
   "function errorMessage(error, fallback) {\n  return error instanceof ApiClientError ? error.message : fallback;\n}\n",
   "function errorMessage(error, fallback) {\n  return error instanceof ApiClientError ? error.message : fallback;\n}\n\n/* quotation-account kit: the latest \"No invoice raised: ...\" timeline note of an approved quotation without an invoice. */\nfunction noInvoiceReason(q) {\n  if (!q || q.invoiceId || q.status !== 4) return null;\n  const notes = (q.events || []).filter((ev) => (ev.summary || \"\").startsWith(\"No invoice raised:\"));\n  if (notes.length > 0) return notes[notes.length - 1].summary;\n  return q.customerId ? null : \"No invoice yet: this quotation has no customer account.\";\n}\n"
  ],
  [
   "approval toast: warn when no invoice",
   "      toast.success(approved ? (saved && saved.invoiceNumber ? `Recorded as approved. Invoice ${saved.invoiceNumber} raised.` : \"Recorded as approved.\") : \"Recorded as rejected.\");\n",
   "      // quotation-account kit: when no invoice could be raised, say why (a warning, not a success).\n      if (approved && saved && !saved.invoiceNumber && noInvoiceReason(saved)) toast.warning(`Recorded as approved. ${noInvoiceReason(saved)}`);\n      else toast.success(approved ? (saved && saved.invoiceNumber ? `Recorded as approved. Invoice ${saved.invoiceNumber} raised.` : \"Recorded as approved.\") : \"Recorded as rejected.\");\n"
  ],
  [
   "banner: say why there is no invoice",
   "        quotation.invoiceNumber && e.jsxs(\"span\", {\n          children: [\"Invoiced as \", e.jsx(\"strong\", { children: quotation.invoiceNumber }), \".\"],\n        }),\n      ],\n    });\n  }\n",
   "        quotation.invoiceNumber && e.jsxs(\"span\", {\n          children: [\"Invoiced as \", e.jsx(\"strong\", { children: quotation.invoiceNumber }), \".\"],\n        }),\n        // quotation-account kit: why there is no invoice, and what to do.\n        !quotation.invoiceNumber && noInvoiceReason(quotation) && e.jsx(\"span\", {\n          \"data-testid\": \"qtn-no-invoice\", style: { flexBasis: \"100%\", color: \"#8A5A00\" },\n          children: noInvoiceReason(quotation) + (quotation.customerId ? \"\" : \" Convert to invoice links the customer account with this email or mobile, or creates one; you can correct the email or mobile there.\"),\n        }),\n      ],\n    });\n  }\n"
  ],
  [
   "detail page: raise-invoice dialog state",
   "  const [deciding, setDeciding] = React.useState(null);\n\n  const load = React.useCallback(async () => {\n",
   "  const [deciding, setDeciding] = React.useState(null);\n  const [raising, setRaising] = React.useState(false);\n\n  const load = React.useCallback(async () => {\n"
  ],
  [
   "detail page: Convert opens the dialog without a customer account",
   "        onConvert: () => void run(\"raise the invoice\", async () => {\n          const invoice = await invoiceApi.fromQuotation(quotation.quotationId);\n",
   "        onConvert: () => (!quotation.customerId ? setRaising(true) : void run(\"raise the invoice\", async () => {\n          const invoice = await invoiceApi.fromQuotation(quotation.quotationId);\n"
  ],
  [
   "detail page: close the conditional",
   "          toast.success(`Invoice ${invoice.invoiceNumber} raised.`);\n          await load();\n        }),\n        onRevise:",
   "          toast.success(`Invoice ${invoice.invoiceNumber} raised.`);\n          await load();\n        })),\n        onRevise:"
  ],
  [
   "detail page: render the dialog",
   "      deciding && e.jsx(RecordDecisionModal, {\n        quotation,\n        approved: deciding.approved,\n        onClose: () => setDeciding(null),\n        onSaved: (saved) => { setDeciding(null); setQuotation(saved); void load(); },\n      }),\n",
   "      deciding && e.jsx(RecordDecisionModal, {\n        quotation,\n        approved: deciding.approved,\n        onClose: () => setDeciding(null),\n        onSaved: (saved) => { setDeciding(null); setQuotation(saved); void load(); },\n      }),\n\n      raising && e.jsx(RaiseInvoiceModal, {\n        quotation,\n        onClose: () => setRaising(false),\n        onDone: () => { setRaising(false); void load(); },\n      }),\n"
  ],
  [
   "RaiseInvoiceModal",
   "function CancelQuotationModal({ quotation, onClose, onCancelled }) {\n",
   "/* quotation-account kit: Convert to invoice for a quotation without a customer account. The server links the\n   customer with this email (else mobile) or creates the account, then raises the one invoice. Never a staff account. */\nfunction RaiseInvoiceModal({ quotation, onClose, onDone }) {\n  const toast = useToast();\n  const [email, setEmail] = React.useState(quotation.customerEmail ?? \"\");\n  const [mobile, setMobile] = React.useState(quotation.customerMobile ?? \"\");\n  const [busy, setBusy] = React.useState(false);\n  const [problem, setProblem] = React.useState(null);\n  const reason = noInvoiceReason(quotation);\n\n  const submit = async () => {\n    setBusy(true);\n    setProblem(null);\n    try {\n      const result = await quotationsApi.raiseInvoice(quotation.quotationId, {\n        customerEmail: email.trim() || null, customerMobile: mobile.trim() || null,\n      });\n      toast.success(result?.message ?? `Invoice ${result?.invoiceNumber} raised.`);\n      onDone(result);\n    } catch (error) {\n      setProblem(errorMessage(error, \"Could not raise the invoice.\"));\n    } finally {\n      setBusy(false);\n    }\n  };\n\n  return e.jsx(Modal, {\n    open: true,\n    title: `Convert ${quotation.quotationNumber} to invoice`,\n    onClose,\n    footer: e.jsxs(e.Fragment, {\n      children: [\n        e.jsx(Button, { variant: \"ghost\", onClick: onClose, disabled: busy, children: \"Cancel\" }),\n        e.jsx(Button, { loading: busy, disabled: email.trim().length === 0, onClick: submit, children: \"Raise invoice\" }),\n      ],\n    }),\n    children: e.jsxs(\"div\", {\n      className: \"form-stack\",\n      \"data-testid\": \"qtn-raise-invoice\",\n      children: [\n        e.jsx(\"p\", {\n          children: `${quotation.customerName} has no customer account yet. The invoice goes to the customer account with this email (or else this mobile number); if there is none, one is created from the quotation's details and they sign in with \\u201cForgot password\\u201d or Google. A staff email is refused.`,\n        }),\n        reason && e.jsx(\"p\", { style: { color: \"#8A5A00\" }, children: reason }),\n        e.jsx(Input, { label: \"Customer email\", required: true, type: \"email\", value: email, onChange: (ev) => setEmail(ev.target.value) }),\n        e.jsx(Input, { label: \"Customer mobile\", value: mobile, hint: \"10 digits or +91\\u2026; needed when a new account is created\", onChange: (ev) => setMobile(ev.target.value) }),\n        problem && e.jsx(\"p\", { role: \"alert\", style: { color: \"#B42318\" }, children: problem }),\n      ],\n    }),\n  });\n}\n\nfunction CancelQuotationModal({ quotation, onClose, onCancelled }) {\n"
  ]
 ],
 "BillingMoneyIn-BF1.js": [
  [
   "invoices search placeholder names the quotation",
   "      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: \"Number, customer…\" }),\n",
   "      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: \"Invoice, quotation or reference no., customer…\" }),\n"
  ],
  [
   "invoices source column names the quotation",
   "    { key: \"source\", header: \"Source\", render: (i) => (i.quotationNumber ?? (i.bookingId ? \"Booking\" : \"—\")) },\n",
   "    { key: \"source\", header: \"Quotation / source\", render: (i) => (i.quotationNumber ?? (i.bookingId ? \"Booking\" : \"—\")) },\n"
  ]
 ]
};

for (const [file, edits] of Object.entries(EDITS)) patch(file, edits);

// ---- sw.js ------------------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const before = sw;
for (const f of Object.keys(EDITS)) {
  const url = `assets/${f}`;
  const re = new RegExp(`\\{url:"${url.replace(/[.\-]/g, (c) => "\\" + c)}",revision:(null|"[^"]*")\\}`);
  if (re.test(sw)) sw = sw.replace(re, `{url:"${url}",revision:"${REV}"}`);
  else {
    const anchor = '{url:"index.html"';
    if (!sw.includes(anchor)) throw new Error("sw.js: no index.html entry to insert before");
    sw = sw.replace(anchor, `{url:"${url}",revision:"${REV}"},${anchor}`);
  }
}
if (changed.size > 0 || !sw.includes(`{url:"index.html",revision:"${REV}"}`)) {
  sw = sw.replace(/\{url:"index\.html",revision:(null|"[^"]*")\}/, `{url:"index.html",revision:"${REV}"}`);
}
if (sw !== before) fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}${sw === before ? " (already done)" : ""}`);
