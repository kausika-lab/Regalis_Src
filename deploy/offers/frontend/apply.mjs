// offers kit (23 Nov 2026): Admin-controlled offers, frontend part.
//   node apply.mjs <frontend dir>
// Copies assets/OffersKit-OF1.js and assets/OffersAdminPage-OF1.js and patches existing chunks with
// string edits that must each match exactly once, so it also works on copies carrying other kits'
// patches (it expects the wifi kit's patches, which are in the repo frontend). Idempotent: an edit
// already present is skipped.
//
// Existing behaviour is untouched when no offer is chosen; every edit only ADDS:
//   index-DI5X4c9f.js            admin route /admin/offers (OFFER.VIEW) and the BadgePercent sidebar icon
//   MyBookingsPage-Jm2rP9kD.js   offer cards for My Bookings under the page header
//   BookWorkspacePage-MB2.js     offer picker at "Review & pay" (bookings, and Virtual Office as a
//                                membership); the chosen offers go on the held booking(s) before paying
//   PassesPage-CxAkHUg-.js       offer cards + picker; offer ids on /passes/order and /passes/purchase
//   MembershipPage-Bk9mQr2v.js   offer cards + picker; offer ids on /passes/order and /passes/purchase
//   sw.js                        revisions for the changed and new files
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "offers-1";
const here = path.dirname(fileURLToPath(import.meta.url));
const A = (f) => path.join(front, "assets", f);
const changed = new Set();
const NEW_FILES = ["OffersKit-OF1.js", "OffersAdminPage-OF1.js"];

function patchFile(p, label, edits) {
  let t = fs.readFileSync(p, "utf8");
  const eol = (s) => (t.includes("\r\n") ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo] of edits) {
    const from = eol(rawFrom);
    const to = eol(rawTo);
    // "Already done" is checked first: every replacement keeps its anchor.
    if (t.includes(to)) {
      console.log(`OK  - ${label}: ${name} (already done)`);
      continue;
    }
    const n = t.split(from).length - 1;
    if (n === 1) {
      t = t.replace(from, () => to);
      console.log(`OK  - ${label}: ${name}`);
      changed.add(label);
    } else throw new Error(`${label}: '${name}' expected 1 match, found ${n}; nothing written for this file`);
  }
  fs.writeFileSync(p, t, "utf8");
}
const patch = (file, edits) => patchFile(A(file), file, edits);

// ---- new files ------------------------------------------------------------------------------
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

// ---- index: lazy chunk, admin route, icon ---------------------------------------------------
const WIFI_LAZY = 'RGwifiMy=fe(()=>ce(()=>import("./WifiPages-WF1.js")),"WifiMyPage"),';
const WIFI_ROUTE = 'g.jsx(P,{path:"/admin/wifi/:tab",element:g.jsx(RGwifiAdmin,{})})]}),';
const ICON =
  'const RG_badgePct=[["path",{d:"M3.85 8.62a4 4 0 0 1 4.78-4.77 4 4 0 0 1 6.74 0 4 4 0 0 1 4.78 4.78 4 4 0 0 1 0 6.74 4 4 0 0 1-4.77 4.78 4 4 0 0 1-6.75 0 4 4 0 0 1-4.78-4.77 4 4 0 0 1 0-6.76Z",key:"bp1"}],["path",{d:"m15 9-6 6",key:"bp2"}],["path",{d:"M9 9h.01",key:"bp3"}],["path",{d:"M15 15h.01",key:"bp4"}]],RGbadgePct=Oe("badge-percent",RG_badgePct);';
patch("index-DI5X4c9f.js", [
  ["Offers chunk", WIFI_LAZY, WIFI_LAZY + 'RGoffersAdmin=fe(()=>ce(()=>import("./OffersAdminPage-OF1.js")),"OffersAdminPage"),'],
  ["admin route /admin/offers", WIFI_ROUTE, WIFI_ROUTE + 'g.jsx(P,{element:g.jsx(Ne,{permission:"OFFER.VIEW"}),children:g.jsx(P,{path:"/admin/offers",element:g.jsx(RGoffersAdmin,{})})}),'],
  ["BadgePercent icon", 'RGwifi=Oe("wifi",RG_wifi);', 'RGwifi=Oe("wifi",RG_wifi);' + ICON],
  ["BadgePercent icon name", "Wifi:RGwifi}", "Wifi:RGwifi,BadgePercent:RGbadgePct}"]
]);

// ---- My Bookings: offer cards -----------------------------------------------------------------
const MB_HEAD = 'e.jsx($,{title:"My Bookings",description:"Cancellations before the 24-hour cutoff refund fully to your wallet."}),';
patch("MyBookingsPage-Jm2rP9kD.js", [
  ["import", 'import{b as I}from"./bookings-DrKATIWm.js";', 'import{b as I}from"./bookings-DrKATIWm.js";import{OfferStrip as RGofferStrip}from"./OffersKit-OF1.js";'],
  ["offer cards", MB_HEAD, MB_HEAD + 'e.jsx(RGofferStrip,{module:"booking",heading:"Booking offers",ctaLabel:"Book a workspace",ctaTo:"/book"}),']
]);

// ---- Book a workspace (bookings + Virtual Office) ------------------------------------------------
const BW_IMPORT = 'import { a as ae, i as Ne, b as k, p as se } from "./bookings-DrKATIWm.js";';
const BW_PAY_VO = `              e.jsxs("div", {
                className: "pay-options",
                children: [
                  e.jsxs(h, {
                    size: "lg",
                    variant: U ? "primary" : "outline",
                    disabled: !U || S !== null,
                    loading: S === "wallet",
                    onClick: () => payVO("wallet"),`;
const BW_PAY_BOOK = `              e.jsxs("div", {
                className: "pay-options",
                children: [
                  e.jsxs(h, {
                    size: "lg",
                    variant: U ? "primary" : "outline",
                    disabled: !U || S !== null,
                    loading: S === "wallet",
                    onClick: () => (isM ? payMonthly : W ? paySingle : payMulti)("wallet"),`;
patch("BookWorkspacePage-MB2.js", [
  [
    "import + apply helper",
    BW_IMPORT,
    BW_IMPORT +
      `
import { OfferPicker as RGofferPicker, applyBookingOffers as RGapplyOffers } from "./OffersKit-OF1.js";
/* offers kit: put the chosen offers on the held booking(s) before paying. If the server refuses
   (the offer ended or no longer applies) the holds are released, so the slot is free to book again. */
const RGwithOffers = async (ids, offer) => {
  if (!offer || !offer.offerIds || offer.offerIds.length === 0) return;
  try {
    await RGapplyOffers(ids, offer.offerIds);
  } catch (err) {
    for (const id of ids) {
      try {
        await Be.cancel(id, "The offer could not be applied");
      } catch (e2) {}
    }
    throw err;
  }
};`
  ],
  ["offer state", "  const [voDone, setVoDone] = a.useState(null);", "  const [voDone, setVoDone] = a.useState(null);\n  const [RGoffer, RGsetOffer] = a.useState(null);"],
  [
    "hourly: offers on the hold",
    "      const t = await k.hold(d.workspaceId, i, m);\n      F(await settle(t.bookingId, method));",
    "      const t = await k.hold(d.workspaceId, i, m);\n      await RGwithOffers([t.bookingId], RGoffer);\n      F(await settle(t.bookingId, method));"
  ],
  [
    "monthly: offers on the hold",
    "      );\n      F(await settle(t.bookingId, method));",
    "      );\n      await RGwithOffers([t.bookingId], RGoffer);\n      F(await settle(t.bookingId, method));"
  ],
  [
    "multi-day: offers on every hold",
    "    const done = [];\n",
    `    try {
      await RGwithOffers(holds.map((held) => held.bookingId), RGoffer);
    } catch (t) {
      (await V(t)) || o.error(t instanceof y ? t.message : "The offer could not be applied. Nothing was charged.");
      N(null);
      return;
    }
    const done = [];
`
  ],
  ["Virtual Office: offers on wallet / pay later", "          paymentMethod: method,\n", "          paymentMethod: method,\n          offerIds: RGoffer ? RGoffer.offerIds : null,\n"],
  [
    "Virtual Office: offers on the online order",
    "const ord = await PASS.order(voPlan.passPlanId);",
    'const ord = await fe(je.post("/passes/order", { passPlanId: voPlan.passPlanId, offerIds: RGoffer ? RGoffer.offerIds : null }));'
  ],
  [
    "amount to pay",
    "  const payTotal = isVO\n    ? (voPlan?.totalAmount ?? null)\n    : (n?.totalAmount ?? null);",
    "  const payTotal = RGoffer\n    ? RGoffer.total\n    : isVO\n    ? (voPlan?.totalAmount ?? null)\n    : (n?.totalAmount ?? null);"
  ],
  [
    "Virtual Office total",
    'e.jsx("span", { children: PR(voPlan.totalAmount) }),',
    'RGoffer ? e.jsxs("span", { children: [e.jsx("span", { className: "of-strike", children: PR(voPlan.totalAmount) }), PR(RGoffer.total)] }) : e.jsx("span", { children: PR(voPlan.totalAmount) }),'
  ],
  [
    "booking total",
    'e.jsx("span", { children: PR(n.totalAmount) }),',
    'RGoffer ? e.jsxs("span", { children: [e.jsx("span", { className: "of-strike", children: PR(n.totalAmount) }), PR(RGoffer.total)] }) : e.jsx("span", { children: PR(n.totalAmount) }),'
  ],
  [
    "Virtual Office picker",
    BW_PAY_VO,
    `              e.jsx(RGofferPicker, { module: "membership", passPlanId: voPlan.passPlanId, disabled: S !== null, onChange: RGsetOffer }),\n` + BW_PAY_VO
  ],
  [
    "booking picker",
    BW_PAY_BOOK,
    `              e.jsx(RGofferPicker, { module: "booking", workspaceId: d ? d.workspaceId : null, baseAmounts: n ? (n.multi ? n.quotes.map((qq) => qq.baseAmount) : [n.baseAmount]) : null, disabled: S !== null, onChange: RGsetOffer }),\n` +
      BW_PAY_BOOK
  ]
]);

// ---- Passes ---------------------------------------------------------------------------------------
const PS_TOTAL = "children:RGprice(RGopt?RGopt.totalAmount:o.totalAmount??o.price)";
const PS_PAY = '{style:{marginTop:"var(--space-4)"},children:[s.jsxs("div",{className:"pay-options"';
patch("PassesPage-CxAkHUg-.js", [
  ["import", 'import{p as E,a as RGavail}from"./bookings-DrKATIWm.js";', 'import{p as E,a as RGavail}from"./bookings-DrKATIWm.js";import{OfferStrip as RGofferStrip,OfferPicker as RGofferPicker}from"./OffersKit-OF1.js";'],
  ["offer state", "[RGopts,RGsetOpts]=r.useState(null)", "[RGopts,RGsetOpts]=r.useState(null),[RGoffer,RGsetOffer]=r.useState(null)"],
  ["amount to pay", "RGtotal=RGopt?RGopt.totalAmount:o?.totalAmount;", "RGtotal=RGoffer?RGoffer.total:RGopt?RGopt.totalAmount:o?.totalAmount;"],
  ["total row", PS_TOTAL, 'children:RGoffer?s.jsxs("span",{children:[s.jsx("span",{className:"of-strike",children:RGprice(RGopt?RGopt.totalAmount:o.totalAmount??o.price)}),RGprice(RGoffer.total)]}):RGprice(RGopt?RGopt.totalAmount:o.totalAmount??o.price)'],
  ["picker", PS_PAY, '{style:{marginTop:"var(--space-4)"},children:[s.jsx(RGofferPicker,{module:d===1?"pass":"membership",passPlanId:o.passPlanId,disabled:G!==null,onChange:RGsetOffer}),s.jsxs("div",{className:"pay-options"'],
  ["offer cards", 's.jsx(A,{title:"Book Pass"}),', 's.jsx(A,{title:"Book Pass"}),s.jsx(RGofferStrip,{module:d===1?"pass":"membership",heading:"Pass offers"}),'],
  ["pay later with offers", 'x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:"paylater"})', 'x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:"paylater",offerIds:RGoffer?RGoffer.offerIds:null})'],
  ["wallet with offers", 'x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:"wallet"})', 'x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:"wallet",offerIds:RGoffer?RGoffer.offerIds:null})'],
  ["online order with offers", "x.order(o.passPlanId)", 'RGunwrap(RGhttp.post("/passes/order",{passPlanId:o.passPlanId,offerIds:RGoffer?RGoffer.offerIds:null}))']
]);

// ---- Membership -----------------------------------------------------------------------------------
patch("MembershipPage-Bk9mQr2v.js", [
  ["import", 'import{p as PY}from"./bookings-DrKATIWm.js";', 'import{p as PY}from"./bookings-DrKATIWm.js";import{OfferStrip as RGofferStrip,OfferPicker as RGofferPicker}from"./OffersKit-OF1.js";'],
  ["offer state", "[hist,sh]=r.useState([])", "[hist,sh]=r.useState([]),[RGoffer,RGsetOffer]=r.useState(null)"],
  ["wallet covers", "W=E&&o?Number(o.balance)>=E.total:!1", "W=E&&o?Number(o.balance)>=(RGoffer?RGoffer.total:E.total):!1"],
  ["pay later with offers", 'PA.purchase({passPlanId:f,validFrom:s,paymentMethod:"paylater"})', 'PA.purchase({passPlanId:f,validFrom:s,paymentMethod:"paylater",offerIds:RGoffer?RGoffer.offerIds:null})'],
  ["wallet with offers", 'PA.purchase({passPlanId:f,validFrom:s,paymentMethod:"wallet"})', 'PA.purchase({passPlanId:f,validFrom:s,paymentMethod:"wallet",offerIds:RGoffer?RGoffer.offerIds:null})'],
  ["online order with offers", "PA.order(f)", 'RGunwrap(RGhttp.post("/passes/order",{passPlanId:f,offerIds:RGoffer?RGoffer.offerIds:null}))'],
  ["total payable", 'children:"Total payable"}),RGprice(E.total)', 'children:"Total payable"}),RGoffer?e.jsxs("span",{children:[e.jsx("span",{className:"of-strike",children:RGprice(E.total)}),RGprice(RGoffer.total)]}):RGprice(E.total)'],
  ["pay online amount", '"Pay online ",y(E.total)', '"Pay online ",y(RGoffer?RGoffer.total:E.total)'],
  ["wallet hint amount", '"+y(E.total)+"', '"+y(RGoffer?RGoffer.total:E.total)+"'],
  ["order summary gets the offer", 'e.jsx(Order,{space:V,q:E,title:"Order summary"})', 'e.jsx(Order,{space:V,q:E,title:"Order summary",offer:RGoffer})'],
  ["order summary signature", "function Order({space:t,q:l,title:d}){", "function Order({space:t,q:l,title:d,offer:RGo}){"],
  [
    "order summary: offer row + total",
    'e.jsx(Row,{label:"Total amount payable",value:RGprice(l.total),strong:!0})',
    'RGo?e.jsx(Row,{label:"Offer discount (before GST)",value:"− "+y(RGo.discount),tone:"var(--color-success)"}):null,e.jsx(Row,{label:"Total amount payable",value:RGo?e.jsxs("span",{children:[e.jsx("span",{className:"of-strike",children:RGprice(l.total)}),RGprice(RGo.total)]}):RGprice(l.total),strong:!0})'
  ],
  ["picker", 'className:"form-stack mem-pay__panel",children:[','className:"form-stack mem-pay__panel",children:[e.jsx(RGofferPicker,{module:"membership",passPlanId:E.plan.passPlanId,disabled:M2!==null,onChange:RGsetOffer}),'],
  ["offer cards", 'e.jsx(A,{title:"Membership"}),', 'e.jsx(A,{title:"Membership"}),e.jsx(RGofferStrip,{module:"membership",heading:"Membership offers"}),']
]);

// ---- sw.js -------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const KIT = [...NEW_FILES, "index-DI5X4c9f.js", "MyBookingsPage-Jm2rP9kD.js", "BookWorkspacePage-MB2.js", "PassesPage-CxAkHUg-.js", "MembershipPage-Bk9mQr2v.js"];
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
