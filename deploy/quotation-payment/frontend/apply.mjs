// quotation-payment kit (27 Nov 2026): frontend part.
//   node apply.mjs <frontend dir>
// Needs the billing-finance, quotation-invoice, quotation-account and admin-booking kits' frontends (their
// text is the anchors). String edits that must each match exactly once; an edit already present is skipped
// (idempotent). Anchors are matched with LF or CRLF line ends, whichever the chunk uses. Any anchor not
// found stops the script before that file is written.
//
//   QuotationPayments-QP1.js         NEW: the Payments screen's "Quotations" tab (approved quotations, the
//                                    paid / outstanding of their invoice, Record payment on that invoice).
//   BillingMoneyIn-BF1.js            Payments: tabs "All payments" / "Quotations"; ?view=quotations and
//                                    ?search= open it on a quotation.
//   AdminQuotationPages-COWOrOkL.js  "Open invoice" opens that invoice (it opened the invoice list); the
//                                    approved banner gets "Open invoice" and "Payment status".
//   AdminBookingsPage-BK2.js         Bookings: the cancel dialog no longer talks about the invoice.
//   BookWorkspacePage-MB2.js         Admin New booking only (the `admin` prop): no invoice buttons, invoice or
//                                    payment-due wording on the review and done steps (staff bookings are billed
//                                    through the quotation). The customer page is unchanged.
//   AdminNewBooking-AB1.js           New booking header: no invoice wording.
//   sw.js                            precache revisions for the changed / new chunks and index.html
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "quotation-payment-1";
const HERE = path.dirname(fileURLToPath(import.meta.url));
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

// ---- new chunk --------------------------------------------------------------------------------------------
const NEW = ["QuotationPayments-QP1.js"];
for (const f of NEW) {
  const src = path.join(HERE, "assets", f);
  const dst = A(f);
  const body = fs.readFileSync(src);
  if (fs.existsSync(dst) && Buffer.compare(fs.readFileSync(dst), body) === 0) console.log(`OK  - ${f} (already copied)`);
  else {
    fs.writeFileSync(dst, body);
    console.log(`OK  - ${f} copied`);
  }
}

const EDITS = {
 "BillingMoneyIn-BF1.js": [
  [
   "import the Quotations tab",
   "} from \"./BillingShared-BF1.js\";\n",
   "} from \"./BillingShared-BF1.js\";\n/* quotation-payment kit: the Payments screen's Quotations tab. */\nimport { QuotationPaymentsPanel } from \"./QuotationPayments-QP1.js\";\n"
  ],
  [
   "payments: search from ?search=",
   "export function AdminPaymentsPageBF() {\n  useStyles();\n  const toast = useToast();\n  const navigate = useNavigate();\n  const [search, setSearch] = React.useState(\"\");\n",
   "export function AdminPaymentsPageBF() {\n  useStyles();\n  const toast = useToast();\n  const navigate = useNavigate();\n  /* quotation-payment kit: ?view=quotations opens the Quotations tab, ?search= fills the search. */\n  const [view, setView] = React.useState(() => (new URLSearchParams(window.location.search).get(\"view\") === \"quotations\" ? \"quotations\" : \"payments\"));\n  const [search, setSearch] = React.useState(() => new URLSearchParams(window.location.search).get(\"search\") ?? \"\");\n"
  ],
  [
   "payments: tabs, Quotations panel",
   "    e.jsxs(\"div\", { className: \"bf-toolbar\", children: [\n      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: \"Payment, receipt, customer, quotation, invoice, UTR…\" }),\n",
   "    e.jsx(Tabs, { value: view, onChange: setView, tabs: [{ value: \"payments\", label: \"All payments\" }, { value: \"quotations\", label: \"Quotations\" }] }),\n    view === \"quotations\" ? e.jsx(QuotationPaymentsPanel, { initialSearch: search, onShowPayments: (q) => { setSearch(q); setView(\"payments\"); } }) : e.jsxs(e.Fragment, { children: [\n    e.jsxs(\"div\", { className: \"bf-toolbar\", children: [\n      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: \"Payment, receipt, customer, quotation, invoice, UTR…\" }),\n"
  ],
  [
   "payments: close the payments view",
   "      e.jsx(Pager, { result: data, page, setPage })\n    ] }) }),\n    reversing ? e.jsx(ReasonModal, {\n      title: `Reverse ${reversing.receiptNumber ?? reversing.paymentNumber}`,\n",
   "      e.jsx(Pager, { result: data, page, setPage })\n    ] }) })\n    ] }),\n    reversing ? e.jsx(ReasonModal, {\n      title: `Reverse ${reversing.receiptNumber ?? reversing.paymentNumber}`,\n"
  ]
 ],
 "AdminQuotationPages-COWOrOkL.js": [
  [
   "list menu: Open invoice opens that invoice",
   "                onSelect: () => navigate(\"/admin/billing/invoices\"),\n",
   "                onSelect: () => navigate(q.invoiceId ? `/admin/billing/invoices/${q.invoiceId}` : \"/admin/billing/invoices\"),\n"
  ],
  [
   "banner: navigate",
   "function StateBanner({ quotation, onConvert, onRevise, busy }) {\n  const status = effectiveStatus(quotation);\n",
   "function StateBanner({ quotation, onConvert, onRevise, busy }) {\n  const navigate = useNavigate();\n  const status = effectiveStatus(quotation);\n"
  ],
  [
   "banner: Open invoice, Payment status",
   "        quotation.invoiceNumber && e.jsxs(\"span\", {\n          children: [\"Invoiced as \", e.jsx(\"strong\", { children: quotation.invoiceNumber }), \".\"],\n        }),\n",
   "        quotation.invoiceNumber && e.jsxs(\"span\", {\n          children: [\"Invoiced as \", e.jsx(\"strong\", { children: quotation.invoiceNumber }), \".\"],\n        }),\n        // quotation-payment kit: straight to the invoice, and to its payment position on the Payments screen.\n        quotation.invoiceId && e.jsx(Button, {\n          size: \"sm\", variant: \"outline\", \"data-testid\": \"qtn-open-invoice\",\n          onClick: () => navigate(`/admin/billing/invoices/${quotation.invoiceId}`), children: \"Open invoice\",\n        }),\n        quotation.invoiceId && e.jsx(Button, {\n          size: \"sm\", variant: \"ghost\", \"data-testid\": \"qtn-payment-status\",\n          onClick: () => navigate(`/admin/billing/payments?view=quotations&search=${encodeURIComponent(quotation.quotationNumber)}`), children: \"Payment status\",\n        }),\n"
  ]
 ],
 "AdminBookingsPage-BK2.js": [
  [
   "cancel dialog: no invoice wording",
   "An unpaid pay-later invoice is cancelled with the booking. The customer is notified.",
   "Nothing stays due for an unpaid pay-later booking once it is cancelled. The customer is notified."
  ]
 ],
 "BookWorkspacePage-MB2.js": [
  [
   "admin Virtual Office review: no invoice wording",
   "a business address with mail handling. The GST invoice is raised now and stays due; the Virtual Office becomes active once its payment is recorded on the Payments screen.\"",
   "a business address with mail handling. Nothing is collected here: the Virtual Office is active straight away and is billed through the customer's quotation.\""
  ],
  [
   "admin monthly review: no invoice wording",
   "? `The workspace is held for the customer for every day of the term, ${dayLabel(n.startDate)} to ${dayLabel(n.endDate)}; one GST invoice covers the whole term.`",
   "? `The workspace is held for the customer for every day of the term, ${dayLabel(n.startDate)} to ${dayLabel(n.endDate)}, as one booking.`"
  ],
  [
   "admin single review: no invoice wording",
   "                        : \"One GST invoice is raised for the booking.\") +\n                    \" Nothing is collected here: the booking is confirmed and the invoice stays due until the payment is recorded on the Payments screen.\"",
   "                        : \"The booking is confirmed straight away.\") +\n                    \" Nothing is collected here: the booking is billed through the customer's quotation.\""
  ],
  [
   "admin Virtual Office done: no invoice wording",
   "? \"The GST invoice is under Finance > Invoices and the Virtual Office under Passes. It becomes active once its payment is recorded.\"",
   "? \"The Virtual Office is under Passes and is active now. It is billed through the customer's quotation.\""
  ],
  [
   "admin Virtual Office done: price line without payment wording",
   "voDone.pass?.isPayLater ? (RGadm ? \" including GST, due from the customer\" : \" including GST, to pay at the front desk\") : \" paid, including GST\",",
   "RGadm ? \" including GST\" : voDone.pass?.isPayLater ? \" including GST, to pay at the front desk\" : \" paid, including GST\","
  ],
  [
   "admin done: the note shows for every staff booking",
   "              ((l && l.isPayLater) || (multi && multi.some((t) => t.isPayLater))) &&\n",
   "              (RGadm || (l && l.isPayLater) || (multi && multi.some((t) => t.isPayLater))) &&\n"
  ],
  [
   "admin done: note wording without invoice or payment",
   "? `${p(l ? l.booking.totalAmount : multi.reduce((s, t) => s + t.booking.totalAmount, 0))} is due from the customer. Record it on the Payments screen when it is paid.`",
   "? \"Nothing is collected for this booking: it is billed through the customer's quotation.\""
  ],
  [
   "admin done: no invoice button (single)",
   "                  l &&\n                    e.jsxs(h, {\n                      variant: \"outline\",\n                      onClick: () =>\n                        Ne.downloadPdf(l.invoiceId, l.invoiceNumber),\n",
   "                  !RGadm && l &&\n                    e.jsxs(h, {\n                      variant: \"outline\",\n                      onClick: () =>\n                        Ne.downloadPdf(l.invoiceId, l.invoiceNumber),\n"
  ],
  [
   "admin done: no invoice buttons (range)",
   "                  multi &&\n                    multi.map((t) =>\n                      e.jsxs(\n                        h,\n                        {\n                          variant: \"outline\",\n                          onClick: () =>\n                            Ne.downloadPdf(t.invoiceId, t.invoiceNumber),\n",
   "                  !RGadm && multi &&\n                    multi.map((t) =>\n                      e.jsxs(\n                        h,\n                        {\n                          variant: \"outline\",\n                          onClick: () =>\n                            Ne.downloadPdf(t.invoiceId, t.invoiceNumber),\n"
  ]
 ],
 "AdminNewBooking-AB1.js": [
  [
   "header: no invoice wording",
   "The booking is confirmed straight away; its invoice stays due until the payment is recorded on the Payments screen.",
   "The booking is confirmed straight away. Nothing is collected here: it is billed through the customer's quotation."
  ]
 ]
};

for (const [file, edits] of Object.entries(EDITS)) patch(file, edits);

// ---- sw.js ------------------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const before = sw;
for (const f of [...NEW, ...Object.keys(EDITS)]) {
  const url = `assets/${f}`;
  const re = new RegExp(`\\{url:"${url.replace(/[.\-]/g, (c) => "\\" + c)}",revision:(null|"[^"]*")\\}`);
  if (re.test(sw)) sw = sw.replace(re, `{url:"${url}",revision:"${REV}"}`);
  else {
    const anchor = '{url:"index.html"';
    if (!sw.includes(anchor)) throw new Error("sw.js: no index.html entry to insert before");
    sw = sw.replace(anchor, `{url:"${url}",revision:"${REV}"},${anchor}`);
  }
}
sw = sw.replace(/\{url:"index\.html",revision:(null|"[^"]*")\}/, `{url:"index.html",revision:"${REV}"}`);
if (sw !== before) fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}${sw === before ? " (already done)" : ""}`);
