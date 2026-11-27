// quotation-invoice kit (27 Nov 2026): quotation -> invoice -> payment fixes, frontend part.
//   node apply.mjs <frontend dir>
// Needs the Billing & Finance kit's frontend (BillingMoneyIn-BF1.js, BillingShared-BF1.js). No new files.
// String edits that must each match exactly once; an edit already present is skipped (idempotent). Anchors
// are matched with LF or CRLF line ends, whichever the chunk uses. Any anchor not found stops the script
// before that file is written.
//
//   BillingMoneyIn-BF1.js            Invoices: payment status reads Due / Partially Paid / Paid (label map only;
//                                    status 6 "Sent" reads Due plus a "Sent" chip; column header Payment Status).
//                                    Payments: the ten columns of the brief (Payment ID, Customer Name,
//                                    Quotation Number, Invoice Number, Payment Date, Payment Method, Total Invoice
//                                    Amount, Amount Paid, Outstanding, Payment Status); filters unchanged.
//   BillingShared-BF1.js             Record payment preview says "Partially Paid".
//   AdminQuotationPages-COWOrOkL.js  Send: the success toast names the recipient (errors were already shown in
//                                    the popup from the server message). Mark approved: names the invoice raised.
//   sw.js                            precache revisions for the three chunks (the 1-year /assets cache on IIS
//                                    is bypassed through the service worker's revisioned precache)
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "quotation-invoice-1";
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
 "BillingMoneyIn-BF1.js": [
  [
   "invoice status labels Due / Partially Paid / Paid",
   "const INVOICE_STATUS = {\n  1: [\"Pending\", \"warning\"], 2: [\"Paid\", \"success\"], 3: [\"Part paid\", \"warning\"], 4: [\"Cancelled\", \"neutral\"], 5: [\"Refunded\", \"neutral\"], 6: [\"Sent\", \"info\"]\n};\n",
   "/* quotation-invoice kit (B4): payment status in the user's words. 1 Pending and 6 Sent are both Due (6 also shows \"Sent\"). */\nconst INVOICE_STATUS = {\n  1: [\"Due\", \"warning\"], 2: [\"Paid\", \"success\"], 3: [\"Partially Paid\", \"warning\"], 4: [\"Cancelled\", \"neutral\"], 5: [\"Refunded\", \"neutral\"], 6: [\"Due\", \"warning\"]\n};\nconst INVOICE_FILTER_LABEL = { 6: \"Due (marked sent)\" };\n/* B5: Payment Status on the Payments page (the invoice's status for an invoice payment, else the payment's own). */\nconst PAY_LABEL_TONE = { Due: \"warning\", \"Partially Paid\": \"warning\", Paid: \"success\", Cancelled: \"neutral\", Refunded: \"neutral\", Pending: \"warning\", Success: \"success\", Failed: \"danger\" };\n"
  ],
  [
   "invoice list column Payment Status",
   "    { key: \"status\", header: \"Status\", render: (i) => e.jsxs(\"div\", { style: { display: \"flex\", gap: 6, flexWrap: \"wrap\" }, children: [invoiceStatus(i.status), i.isOverdue ? pill(\"Overdue\", \"danger\") : null] }) },\n",
   "    { key: \"status\", header: \"Payment Status\", render: (i) => e.jsxs(\"div\", { style: { display: \"flex\", gap: 6, flexWrap: \"wrap\" }, children: [invoiceStatus(i.status), i.status === 6 ? pill(\"Sent\", \"info\") : null, i.isOverdue ? pill(\"Overdue\", \"danger\") : null] }) },\n"
  ],
  [
   "invoice status filter labels",
   "        ...Object.entries(INVOICE_STATUS).map(([v, [l]]) => ({ value: v, label: l }))] }),\n",
   "        ...Object.entries(INVOICE_STATUS).map(([v, [l]]) => ({ value: v, label: INVOICE_FILTER_LABEL[v] ?? l }))] }),\n"
  ],
  [
   "payments page columns",
   "    { key: \"payment\", header: \"Payment\", render: (p) => e.jsxs(\"div\", { children: [\n      e.jsx(\"div\", { className: \"bf-strong\", children: p.receiptNumber ?? p.paymentNumber }),\n      e.jsx(\"div\", { className: \"bf-sub\", children: `${p.receiptNumber ? `${p.paymentNumber} · ` : \"\"}${p.customerName}` })\n    ] }) },\n    { key: \"mode\", header: \"Mode\", render: (p) => pill(p.modeName, MODE_TONE_BY_NAME[p.modeName] ?? (p.isOffline ? \"neutral\" : \"info\")) },\n    { key: \"ref\", header: \"Reference\", render: (p) => e.jsxs(\"div\", { children: [e.jsx(\"div\", { children: p.reference ?? \"—\" }), p.gateway && p.gateway !== \"Offline\" ? e.jsx(\"div\", { className: \"bf-sub\", children: p.gateway }) : null] }) },\n    { key: \"against\", header: \"Against\", render: (p) => (p.invoiceId\n      ? e.jsx(\"button\", { type: \"button\", className: \"bf-link\", onClick: () => navigate(`/admin/billing/invoices/${p.invoiceId}`), children: p.invoiceNumber })\n      : e.jsx(\"span\", { className: \"bf-sub\", children: \"—\" })) },\n    { key: \"amount\", header: \"Amount\", num: true, render: (p) => e.jsx(\"span\", { className: \"bf-strong\", children: money(p.amount) }) },\n    { key: \"status\", header: \"Status\", render: (p) => e.jsxs(\"div\", { style: { display: \"flex\", gap: 6, flexWrap: \"wrap\" }, children: [\n      pill(...(PAYMENT_STATUS[p.status] ?? [\"—\", \"neutral\"])),\n      p.isReversed ? pill(\"Reversed\", \"neutral\") : null,\n      p.isReconciled ? pill(\"Reconciled\", \"success\") : null\n    ] }) },\n    { key: \"when\", header: \"Paid (IST)\", render: (p) => e.jsxs(\"div\", { children: [e.jsx(\"div\", { children: p.paidUtc ? fmtDateTime(p.paidUtc) : \"—\" }), p.reverseReason ? e.jsx(\"div\", { className: \"bf-sub\", children: `Reversed: ${p.reverseReason}` }) : null] }) },\n",
   "    /* quotation-invoice kit (B5): Payment ID, Customer Name, Quotation Number, Invoice Number, Payment Date,\n       Payment Method, Total Invoice Amount, Amount Paid, Outstanding, Payment Status (same figures as Invoices). */\n    { key: \"payment\", header: \"Payment ID\", render: (p) => e.jsxs(\"div\", { children: [\n      e.jsx(\"div\", { className: \"bf-strong\", children: p.paymentNumber }),\n      p.receiptNumber ? e.jsx(\"div\", { className: \"bf-sub\", children: p.receiptNumber }) : null\n    ] }) },\n    { key: \"customer\", header: \"Customer Name\", render: (p) => p.customerName || \"—\" },\n    { key: \"quotation\", header: \"Quotation Number\", render: (p) => p.quotationNumber ?? \"—\" },\n    { key: \"against\", header: \"Invoice Number\", render: (p) => (p.invoiceId\n      ? e.jsx(\"button\", { type: \"button\", className: \"bf-link\", onClick: () => navigate(`/admin/billing/invoices/${p.invoiceId}`), children: p.invoiceNumber })\n      : e.jsx(\"span\", { className: \"bf-sub\", children: \"—\" })) },\n    { key: \"when\", header: \"Payment Date\", render: (p) => e.jsxs(\"div\", { children: [e.jsx(\"div\", { children: p.paidUtc ? fmtDateTime(p.paidUtc) : \"—\" }), p.reverseReason ? e.jsx(\"div\", { className: \"bf-sub\", children: `Reversed: ${p.reverseReason}` }) : null] }) },\n    { key: \"mode\", header: \"Payment Method\", render: (p) => e.jsxs(\"div\", { children: [\n      pill(p.modeName, MODE_TONE_BY_NAME[p.modeName] ?? (p.isOffline ? \"neutral\" : \"info\")),\n      (p.reference || (p.gateway && p.gateway !== \"Offline\")) ? e.jsx(\"div\", { className: \"bf-sub\", children: [p.reference, p.gateway && p.gateway !== \"Offline\" ? p.gateway : null].filter(Boolean).join(\" · \") }) : null\n    ] }) },\n    { key: \"invtotal\", header: \"Total Invoice Amount\", num: true, render: (p) => (p.totalInvoiceAmount == null ? \"—\" : money(p.totalInvoiceAmount)) },\n    { key: \"amount\", header: \"Amount Paid\", num: true, render: (p) => e.jsx(\"span\", { className: \"bf-strong\", children: money(p.amount) }) },\n    { key: \"out\", header: \"Outstanding\", num: true, render: (p) => (p.outstandingAmount == null ? \"—\" : money(p.outstandingAmount)) },\n    { key: \"status\", header: \"Payment Status\", render: (p) => e.jsxs(\"div\", { style: { display: \"flex\", gap: 6, flexWrap: \"wrap\" }, children: [\n      p.paymentStatus ? pill(p.paymentStatus, PAY_LABEL_TONE[p.paymentStatus] ?? \"neutral\") : pill(...(PAYMENT_STATUS[p.status] ?? [\"—\", \"neutral\"])),\n      p.paymentStatus && p.invoiceId && p.status !== 2 ? pill(...(PAYMENT_STATUS[p.status] ?? [\"—\", \"neutral\"])) : null,\n      p.isReversed ? pill(\"Reversed\", \"neutral\") : null,\n      p.isReconciled ? pill(\"Reconciled\", \"success\") : null\n    ] }) },\n"
  ],
  [
   "payments search hint",
   "      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: \"Payment, receipt, customer, invoice, UTR…\" }),\n",
   "      e.jsx(SearchBox, { value: search, onChange: setSearch, placeholder: \"Payment, receipt, customer, quotation, invoice, UTR…\" }),\n"
  ]
 ],
 "BillingShared-BF1.js": [
  [
   "record payment preview says Partially Paid",
   "becomes Part paid with ${money(after)} still outstanding.",
   "becomes Partially Paid with ${money(after)} still outstanding."
  ]
 ],
 "AdminQuotationPages-COWOrOkL.js": [
  [
   "send toast names the recipient",
   "      toast.success(`Quotation sent by ${channels}.`);\n",
   "      // quotation-invoice kit (B1): a success only after the server says it was delivered; it names the recipient.\n      toast.success(`Quotation sent by ${channels}${form.sendEmail ? ` to ${form.emailTo.trim()}` : \"\"}.`);\n"
  ],
  [
   "approval toast names the invoice raised",
   "      toast.success(approved ? \"Recorded as approved.\" : \"Recorded as rejected.\");\n",
   "      // quotation-invoice kit (B2): approval raises the invoice; say which.\n      toast.success(approved ? (saved && saved.invoiceNumber ? `Recorded as approved. Invoice ${saved.invoiceNumber} raised.` : \"Recorded as approved.\") : \"Recorded as rejected.\");\n"
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
if (changed.size > 0) sw = sw.replace(/\{url:"index\.html",revision:(null|"[^"]*")\}/, `{url:"index.html",revision:"${REV}"}`);
if (sw !== before) fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}${sw === before ? " (already done)" : ""}`);
