// admin-booking kit (27 Nov 2026): Admin > Bookings > New booking = the customer booking page
// without payment, frontend part.
//   node apply.mjs <frontend dir>
// Needs the offers and coupons kits' frontend applied first (it patches their chunks). Copies
// assets/AdminNewBooking-AB1.js and makes string edits that must each match exactly the expected
// number of times; a missing anchor stops the script before that file is written. Idempotent: an
// edit already present is skipped. Works on LF and CRLF chunks.
//
// The customer screen is unchanged: every edit in the customer chunks only acts when the page is
// given props.admin, which only AdminNewBooking-AB1.js does.
//   BookWorkspacePage-MB2.js   props.admin: Customer + Location fields, no Customisation tile, holds /
//                              confirm / offers / coupons / Virtual Office through
//                              /api/admin/on-behalf/{customerId}/..., "Review & confirm" with one
//                              Confirm button, no wallet / pay-later fetches, no payment wording
//   OffersKit-OF1.js           OfferPicker: optional api (preview URLs for the chosen customer) and
//                              staff (wording "Total after discount" instead of "You pay")
//   AdminBookingsPage-BK2.js   New booking opens AdminNewBooking-AB1.js in place of the list; the old
//                              "New booking (front desk)" modal with its Settlement dropdown is removed
//   sw.js                      revisions for the changed and new files
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "admin-booking-2";
const here = path.dirname(fileURLToPath(import.meta.url));
const A = (f) => path.join(front, "assets", f);
const L = (...lines) => lines.join("\n");
const changed = new Set();
const NEW_FILES = ["AdminNewBooking-AB1.js"];

// edits: [name, from, to, count = 1]. "Already done" is checked first: every replacement keeps its anchor.
function patch(file, edits) {
  const p = A(file);
  let t = fs.readFileSync(p, "utf8");
  const eol = (s) => (t.includes("\r\n") ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo, count = 1] of edits) {
    const from = eol(rawFrom);
    const to = eol(rawTo);
    if (t.split(to).length - 1 === count) {
      console.log(`OK  - ${file}: ${name} (already done)`);
      continue;
    }
    const n = t.split(from).length - 1;
    if (n !== count) throw new Error(`${file}: '${name}' expected ${count} match(es), found ${n}; nothing written for this file`);
    t = t.split(from).join(to);
    console.log(`OK  - ${file}: ${name}`);
    changed.add(file);
  }
  fs.writeFileSync(p, t, "utf8");
}

for (const f of ["OffersKit-OF1.js", "CouponsKit-CP1.js", "BookWorkspacePage-MB2.js", "AdminBookingsPage-BK2.js"])
  if (!fs.existsSync(A(f))) throw new Error(`${f} not found: apply the offers and coupons kits' frontend first`);
if (!fs.readFileSync(A("BookWorkspacePage-MB2.js"), "utf8").includes("RGapplyCoupon"))
  throw new Error("BookWorkspacePage-MB2.js has no coupon support: apply the coupons kit's frontend first");

// ---- new file ---------------------------------------------------------------------------------
for (const f of NEW_FILES) {
  const src = path.join(here, "assets", f);
  const dst = A(f);
  const same = fs.existsSync(dst) && fs.readFileSync(dst, "utf8") === fs.readFileSync(src, "utf8");
  if (!same) {
    fs.copyFileSync(src, dst);
    changed.add(f);
  }
  console.log(`OK  - ${f} ${same ? "(already there)" : "copied"}`);
}

// ---- OffersKit: the checkout picker can preview for a chosen customer ---------------------------------
patch("OffersKit-OF1.js", [
  [
    "picker takes api + staff",
    "export function OfferPicker({ module, passPlanId = null, workspaceId = null, baseAmounts = null, disabled = false, onChange }) {",
    "export function OfferPicker({ module, passPlanId = null, workspaceId = null, baseAmounts = null, disabled = false, onChange, api = null, staff = false }) {",
  ],
  ["offer preview URL", '      http.post("/offers/preview", {', '      http.post(api ? api.offersPreview : "/offers/preview", {'],
  [
    "coupon preview for the chosen customer",
    "  const couponBody = (c) => ({ module, code: c, passPlanId, workspaceId, baseAmounts: module === \"booking\" ? baseAmounts : null });",
    L(
      "  const couponBody = (c) => ({ module, code: c, passPlanId, workspaceId, baseAmounts: module === \"booking\" ? baseAmounts : null });",
      "  /* admin-booking kit: on the admin New booking screen (api set) the previews run for the chosen customer. */",
      "  const RGcp = (body) => (api ? unwrap(http.post(api.couponPreview, body)) : RGpreviewCoupon(body));"
    ),
  ],
  ["coupon previews go through RGcp", "RGpreviewCoupon(couponBody(", "RGcp(couponBody(", 2],
  [
    "staff wording: total",
    'el("div", { className: "of-final" }, el("span", null, "You pay"),',
    'el("div", { className: "of-final" }, el("span", null, staff ? "Total after discount" : "You pay"),',
  ],
  ["staff wording: saving", "`You save ${money(data.originalTotal - data.totalAmount)}`", "staff ? `Saving ${money(data.originalTotal - data.totalAmount)}` : `You save ${money(data.originalTotal - data.totalAmount)}`"],
]);

// ---- BookWorkspacePage: admin mode (props.admin) ----------------------------------------------------
patch("BookWorkspacePage-MB2.js", [
  [
    "on-behalf calls + offers helper takes the customer",
    L(
      "const RGwithOffers = async (ids, offer) => {",
      "  if (!offer || ((!offer.offerIds || offer.offerIds.length === 0) && !offer.couponCode)) return;",
      "  try {",
      "    if (offer.couponCode) await RGapplyCoupon(ids, offer.couponCode);",
      "    else await RGapplyOffers(ids, offer.offerIds);"
    ),
    L(
      "/* admin-booking kit (27 Nov 2026): Admin > Bookings > New booking renders this page with",
      "   props.admin = { customerId, header, onDone }. Every write then goes to",
      "   /api/admin/on-behalf/{customerId}/..., which runs the same booking engine for that customer",
      "   and confirms without payment. Quotes, slots and availability use the same calls as below.",
      "   Without props.admin (the customer portal) nothing here is used. */",
      "const RGob = (cid) => ({",
      "  hold: (workspaceId, startUtc, durationMinutes, notes) => fe(je.post(`/admin/on-behalf/${cid}/bookings`, { workspaceId, startUtc, durationMinutes, notes })),",
      "  monthlyUrl: `/admin/on-behalf/${cid}/bookings/monthly`,",
      "  confirm: (bookingId) => fe(je.post(`/admin/on-behalf/${cid}/bookings/${bookingId}/confirm`, {})),",
      "  applyOffers: (ids, offerIds) => fe(je.post(`/admin/on-behalf/${cid}/offers/bookings/apply`, { bookingIds: ids, offerIds })),",
      "  applyCoupon: (ids, code) => fe(je.post(`/admin/on-behalf/${cid}/coupons/bookings/apply`, { bookingIds: ids, code })),",
      "  vo: (body) => fe(je.post(`/admin/on-behalf/${cid}/virtual-office`, body)),",
      "  offerApi: { offersPreview: `/admin/on-behalf/${cid}/offers/preview`, couponPreview: `/admin/on-behalf/${cid}/coupons/preview` },",
      "});",
      "/* admin-booking kit: the single button that replaces the payment choices on the admin screen. */",
      "const RGconfirm = (busyKey, onClick, label) =>",
      "  e.jsx(\"div\", {",
      "    className: \"pay-options\",",
      "    \"data-rg\": \"admin-booking-confirm\",",
      "    children: e.jsx(h, { size: \"lg\", variant: \"primary\", disabled: busyKey !== null, loading: busyKey === \"confirm\", onClick, children: label }),",
      "  });",
      "const RGwithOffers = async (ids, offer, adm = null) => {",
      "  if (!offer || ((!offer.offerIds || offer.offerIds.length === 0) && !offer.couponCode)) return;",
      "  try {",
      "    if (offer.couponCode) await (adm ? RGob(adm).applyCoupon(ids, offer.couponCode) : RGapplyCoupon(ids, offer.couponCode));",
      "    else await (adm ? RGob(adm).applyOffers(ids, offer.offerIds) : RGapplyOffers(ids, offer.offerIds));"
    ),
  ],
  [
    "page takes props.admin",
    L("function Ee() {", "  const o = ue();"),
    L(
      "function Ee(RGprops) {",
      "  /* admin-booking kit: set only by AdminNewBooking-AB1.js (staff booking for a customer). */",
      "  const RGadm = RGprops && RGprops.admin ? RGprops.admin : null;",
      "  const RGcid = RGadm ? RGadm.customerId : null;",
      "  const o = ue();"
    ),
  ],
  [
    "no pay-later check on the admin screen",
    L("  a.useEffect(() => {", '    fe(je.get("/bookings/pay-later")).then(setPayLater)'),
    L("  a.useEffect(() => {", "    if (RGadm) return;", '    fe(je.get("/bookings/pay-later")).then(setPayLater)'),
  ],
  [
    "no pass pay-later check on the admin screen",
    L("  a.useEffect(() => {", '    fe(je.get("/passes/pay-later"))'),
    L("  a.useEffect(() => {", "    if (RGadm) return;", '    fe(je.get("/passes/pay-later"))'),
  ],
  ["no wallet on the admin screen", L('    fe(je.get("/wallet/me"))', "      .then(oe)"), L("    RGadm ||", '    fe(je.get("/wallet/me"))', "      .then(oe)")],
  ["another customer starts the choice again", "  }, [b, A, v, j, mMonths]);", "  }, [b, A, v, j, mMonths, RGcid]);"],
  [
    "admin screen has its own header",
    L("      e.jsx(Ie, {", '        title: "Book a Workspace",', "      }),"),
    L("      !RGadm &&", "      e.jsx(Ie, {", '        title: "Book a Workspace",', "      }),"),
  ],
  [
    "Customer and Location fields",
    L("            children: [", '              e.jsx("div", {', '                className: "field__label",', '                children: "1 · Choose your workspace",'),
    L(
      "            children: [",
      "              RGadm &&",
      '                e.jsxs("div", {',
      '                  className: "form-grid",',
      '                  "data-rg": "admin-booking-for",',
      "                  children: [",
      "                    RGadm.header,",
      "                    e.jsx(ve, {",
      '                      label: "Location",',
      "                      required: !0,",
      "                      value: j,",
      "                      options: g.map((t) => ({ value: String(t.locationId), label: t.locationName })),",
      "                      onChange: (t) => M(t.target.value),",
      "                    }),",
      "                  ],",
      "                }),",
      '              e.jsx("div", {',
      '                className: "field__label",',
      '                children: "1 · Choose your workspace",'
    ),
  ],
  ["no Customisation tile for staff", "                  customCard(),\n", "                  RGadm ? null : customCard(),\n"],
  [
    "empty-state text without Customisation",
    '                      "Choose Desk, Cabin or Meeting Room, then pick your dates and time — or Virtual Office for a monthly business address. Pick Customisation to tell us what you need.",',
    L(
      "                      RGadm",
      '                        ? "Choose Desk, Cabin or Meeting Room, then pick the dates and time — or Virtual Office for a monthly business address."',
      '                        : "Choose Desk, Cabin or Meeting Room, then pick your dates and time — or Virtual Office for a monthly business address. Pick Customisation to tell us what you need.",'
    ),
  ],
  ["a customer is required", "  const canContinue = !pastStart && (isVO", "  const canContinue = !pastStart && (!RGadm || !!RGcid) && (isVO"],
  [
    "hint + Virtual Office button wording",
    L(
      "              !!v &&",
      "              !isCustom &&",
      "              e.jsx(h, {",
      '                size: "lg",',
      "                disabled: !canContinue || busy,",
      "                loading: busy,",
      '                onClick: isVO ? () => r("pay") : de,',
      "                children: isVO",
      '                  ? "Continue to payment"'
    ),
    L(
      "              RGadm &&",
      "                !RGcid &&",
      '                e.jsx("p", { className: "choice-card__meta", "data-rg": "admin-booking-need-customer", style: { margin: 0, color: "var(--color-danger)" }, children: "Choose the customer to book for." }),',
      "              !!v &&",
      "              !isCustom &&",
      "              e.jsx(h, {",
      '                size: "lg",',
      "                disabled: !canContinue || busy,",
      "                loading: busy,",
      '                onClick: isVO ? () => r("pay") : de,',
      "                children: isVO",
      '                  ? (RGadm ? "Continue to review" : "Continue to payment")'
    ),
  ],
  [
    "staff confirm without payment",
    L("  const settle = async (bookingId, method) => {", '    if (method === "paylater") {'),
    L("  const settle = async (bookingId, method) => {", "    if (RGadm) return RGob(RGcid).confirm(bookingId);", '    if (method === "paylater") {'),
  ],
  ["single hold for the customer", "      const t = await k.hold(d.workspaceId, i, m);", "      const t = await (RGadm ? RGob(RGcid).hold : k.hold)(d.workspaceId, i, m);"],
  ["offers on the customer's hold", "await RGwithOffers([t.bookingId], RGoffer);", "await RGwithOffers([t.bookingId], RGoffer, RGcid);", 2],
  ["monthly hold for the customer", '        je.post("/bookings/monthly", {', '        je.post(RGadm ? RGob(RGcid).monthlyUrl : "/bookings/monthly", {'],
  ["multi-day holds for the customer", L("          await k.hold(", "            d.workspaceId,"), L("          await (RGadm ? RGob(RGcid).hold : k.hold)(", "            d.workspaceId,")],
  [
    "offers on the customer's multi-day holds",
    "await RGwithOffers(holds.map((held) => held.bookingId), RGoffer);",
    "await RGwithOffers(holds.map((held) => held.bookingId), RGoffer, RGcid);",
  ],
  [
    "Virtual Office for the customer",
    L('      if (method === "wallet" || method === "paylater")', "        res = await PASS.purchase({"),
    L(
      "      if (RGadm)",
      "        res = await RGob(RGcid).vo({",
      "          passPlanId: voPlan.passPlanId,",
      "          validFrom: b,",
      "          offerIds: RGoffer ? RGoffer.offerIds : null, couponCode: RGoffer ? RGoffer.couponCode : null,",
      "        });",
      '      else if (method === "wallet" || method === "paylater")',
      "        res = await PASS.purchase({"
    ),
  ],
  ["no payment wording in errors", '"Payment failed.")', '(RGadm ? "Could not create the booking." : "Payment failed."))', 3],
  [
    "no payment wording in the multi-day error",
    '            t instanceof y ? t.message : "Payment failed. Nothing was charged.",',
    '            t instanceof y ? t.message : RGadm ? "Could not confirm the booking." : "Payment failed. Nothing was charged.",',
  ],
  ["Review & confirm", 'title: "Review & pay",', 'title: RGadm ? "Review & confirm" : "Review & pay",', 2],
  [
    "Virtual Office: one Confirm button",
    L(
      '              e.jsx(RGofferPicker, { module: "membership", passPlanId: voPlan.passPlanId, disabled: S !== null, onChange: RGsetOffer }),',
      '              e.jsxs("div", {',
      '                className: "pay-options",'
    ),
    L(
      '              e.jsx(RGofferPicker, { module: "membership", passPlanId: voPlan.passPlanId, disabled: S !== null, onChange: RGsetOffer, api: RGadm ? RGob(RGcid).offerApi : null, staff: !!RGadm }),',
      '              RGadm ? RGconfirm(S, () => payVO("confirm"), "Confirm Virtual Office") :',
      '              e.jsxs("div", {',
      '                className: "pay-options",'
    ),
  ],
  [
    "booking: one Confirm button",
    L(
      '              e.jsx(RGofferPicker, { module: "booking", workspaceId: d ? d.workspaceId : null, baseAmounts: n ? (n.multi ? n.quotes.map((qq) => qq.baseAmount) : [n.baseAmount]) : null, disabled: S !== null, onChange: RGsetOffer }),',
      '              e.jsxs("div", {',
      '                className: "pay-options",'
    ),
    L(
      '              e.jsx(RGofferPicker, { module: "booking", workspaceId: d ? d.workspaceId : null, baseAmounts: n ? (n.multi ? n.quotes.map((qq) => qq.baseAmount) : [n.baseAmount]) : null, disabled: S !== null, onChange: RGsetOffer, api: RGadm ? RGob(RGcid).offerApi : null, staff: !!RGadm }),',
      '              RGadm ? RGconfirm(S, () => (isM ? payMonthly : W ? paySingle : payMulti)("confirm"), n.multi ? `Confirm ${n.days} days` : "Confirm booking") :',
      '              e.jsxs("div", {',
      '                className: "pay-options",'
    ),
  ],
  [
    "Virtual Office review note",
    '                  "No desk or room is reserved — a Virtual Office is a business address with mail handling. A GST invoice is raised as soon as the payment goes through.",',
    L(
      "                  RGadm",
      '                    ? "No desk or room is reserved — a Virtual Office is a business address with mail handling. The GST invoice is raised now and stays due; the Virtual Office becomes active once its payment is recorded on the Payments screen."',
      '                    : "No desk or room is reserved — a Virtual Office is a business address with mail handling. A GST invoice is raised as soon as the payment goes through.",'
    ),
  ],
  [
    "booking review note",
    L("                children: n.monthly", "                  ? `The workspace is yours for every day of the term,"),
    L(
      "                children: RGadm",
      "                  ? (n.monthly",
      "                      ? `The workspace is held for the customer for every day of the term, ${dayLabel(n.startDate)} to ${dayLabel(n.endDate)}; one GST invoice covers the whole term.`",
      "                      : n.multi",
      "                        ? `Every one of the ${n.days} days is held first; if any day cannot be held, the whole range is released.`",
      '                        : "One GST invoice is raised for the booking.") +',
      '                    " Nothing is collected here: the booking is confirmed and the invoice stays due until the payment is recorded on the Payments screen."',
      "                  : n.monthly",
      "                  ? `The workspace is yours for every day of the term,"
    ),
  ],
  [
    "confirmation: amount due",
    "                  children: `Pay ${p(l ? l.booking.totalAmount : multi.reduce((s, t) => s + t.booking.totalAmount, 0))} at the front desk (cash, UPI or card) before you check in. Your QR pass works once it is paid.`,",
    L(
      "                  children: RGadm",
      "                    ? `${p(l ? l.booking.totalAmount : multi.reduce((s, t) => s + t.booking.totalAmount, 0))} is due from the customer. Record it on the Payments screen when it is paid.`",
      "                    : `Pay ${p(l ? l.booking.totalAmount : multi.reduce((s, t) => s + t.booking.totalAmount, 0))} at the front desk (cash, UPI or card) before you check in. Your QR pass works once it is paid.`,"
    ),
  ],
  [
    "confirmation: no 'your QR pass' line for staff",
    L(
      '              e.jsxs("p", {',
      "                style: {",
      '                  color: "var(--text-muted)",',
      '                  marginTop: "var(--space-2)",',
      '                  fontSize: "var(--font-size-sm)",',
      "                },",
      "                children: [",
      "                  e.jsx(be, {"
    ),
    L(
      "              !RGadm &&",
      '              e.jsxs("p", {',
      "                style: {",
      '                  color: "var(--text-muted)",',
      '                  marginTop: "var(--space-2)",',
      '                  fontSize: "var(--font-size-sm)",',
      "                },",
      "                children: [",
      "                  e.jsx(be, {"
    ),
  ],
  [
    "confirmation: back to bookings",
    '                  e.jsx(h, { onClick: reset, children: "Book another" }),',
    L(
      '                  e.jsx(h, { onClick: reset, children: "Book another" }),',
      '                  RGadm && e.jsx(h, { variant: "outline", onClick: RGadm.onDone, children: "Back to bookings" }),'
    ),
  ],
  [
    "Virtual Office confirmation: amount due",
    'voDone.pass?.isPayLater ? " including GST, to pay at the front desk" : " paid, including GST",',
    'voDone.pass?.isPayLater ? (RGadm ? " including GST, due from the customer" : " including GST, to pay at the front desk") : " paid, including GST",',
  ],
  [
    "Virtual Office confirmation note",
    '                  "Your GST invoice is under Invoices, and the address service shows under Passes. No desk or room was reserved.",',
    L(
      "                  RGadm",
      '                    ? "The GST invoice is under Finance > Invoices and the Virtual Office under Passes. It becomes active once its payment is recorded."',
      '                    : "Your GST invoice is under Invoices, and the address service shows under Passes. No desk or room was reserved.",'
    ),
  ],
  [
    "Virtual Office confirmation: buttons",
    '                children: e.jsx(h, { onClick: reset, children: "Done" }),',
    L(
      "                children: RGadm",
      '                  ? [e.jsx(h, { onClick: reset, children: "Book another" }, "again"), e.jsx(h, { variant: "outline", onClick: RGadm.onDone, children: "Back to bookings" }, "back")]',
      '                  : e.jsx(h, { onClick: reset, children: "Done" }),'
    ),
  ],
]);

// ---- AdminBookingsPage: New booking opens the full booking page; the old modal is removed -------------
patch("AdminBookingsPage-BK2.js", [
  [
    "import the New booking page",
    'import{BookingsMonthlyChart as BMC}from"./BookingsMonthlyChart-BK2.js";',
    'import{BookingsMonthlyChart as BMC}from"./BookingsMonthlyChart-BK2.js";import{AdminNewBooking as RGnewBooking}from"./AdminNewBooking-AB1.js";',
  ],
  [
    "New booking replaces the list while open",
    'return e.jsxs(e.Fragment,{children:[e.jsx(ce,{title:"Bookings",',
    'if(i)return e.jsx(RGnewBooking,{onClose:()=>u(!1),onDone:()=>{u(!1),d()}});return e.jsxs(e.Fragment,{children:[e.jsx(ce,{title:"Bookings",',
  ],
]);
{
  // The old modal: its use in the page and the component itself (cash / wallet / pay-later Settlement).
  const f = "AdminBookingsPage-BK2.js";
  const p = A(f);
  let t = fs.readFileSync(p, "utf8");
  const MARK = "/* admin-booking kit: the old New booking (front desk) modal";
  if (t.includes(MARK)) console.log(`OK  - ${f}: old modal removed (already done)`);
  else {
    const start = ",i&&e.jsx(de,{onClose:()=>u(!1),onCreated:()=>{u(!1),d()}})]})}function de({";
    const end = "export{Ne as AdminBookingsPage};";
    const a = t.split(start).length - 1;
    const b = t.split(end).length - 1;
    if (a !== 1 || b !== 1) throw new Error(`${f}: old modal anchors expected once each, found ${a} and ${b}; nothing written`);
    const i0 = t.indexOf(start);
    const i1 = t.indexOf(end);
    if (i1 < i0) throw new Error(`${f}: old modal anchors out of order; nothing written`);
    t = t.slice(0, i0) + "]})}" + MARK + " (cash / wallet / pay later) is gone; New booking opens AdminNewBooking-AB1.js. */" + t.slice(i1);
    fs.writeFileSync(p, t, "utf8");
    changed.add(f);
    console.log(`OK  - ${f}: old modal removed`);
  }
}

// ---- sw.js -------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const KIT = [...NEW_FILES, "OffersKit-OF1.js", "BookWorkspacePage-MB2.js", "AdminBookingsPage-BK2.js"];
for (const f of new Set([...changed, ...KIT])) {
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
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV} (${changed.size} files)`);
