// coupons kit (25 Nov 2026): coupon codes, frontend part.
//   node apply.mjs <frontend dir>
// Needs the offers kit's frontend (deploy/offers/frontend/apply.mjs) applied first: it patches the
// offers chunks and the pages the offers kit patched. Copies assets/CouponsKit-CP1.js and
// assets/CouponsAdminPage-CP1.js and makes string edits that must each match exactly the expected
// number of times. Idempotent: an edit already present is skipped.
//
// Existing behaviour is untouched when no coupon is used; every edit only ADDS:
//   OffersAdminPage-OF1.js       tabs "Offers | Coupon codes" (?tab=coupons); the offers tab is unchanged
//   OffersKit-OF1.js             checkout picker: a typed code is tried as a coupon first, then as an
//                                offer code (as before); "Available coupons" chips; "Remove coupon"
//                                brings the automatic offer back; the selection carries couponCode
//   BookWorkspacePage-MB2.js     a coupon goes on the held booking(s) before paying; Virtual Office
//                                sends couponCode on /passes/order and /passes/purchase
//   PassesPage-CxAkHUg-.js       coupon cards; couponCode on /passes/order and /passes/purchase; the order
//                                summary gets a discount row and shows the GST charged (offer or coupon)
//   MembershipPage-Bk9mQr2v.js   coupon cards; couponCode on /passes/order and /passes/purchase
//   sw.js                        revisions for the changed and new files
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "coupons-1";
const here = path.dirname(fileURLToPath(import.meta.url));
const A = (f) => path.join(front, "assets", f);
const changed = new Set();
const NEW_FILES = ["CouponsKit-CP1.js", "CouponsAdminPage-CP1.js"];

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

if (!fs.existsSync(A("OffersKit-OF1.js"))) throw new Error("OffersKit-OF1.js not found: apply the offers kit's frontend first");

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

// ---- admin: Offers | Coupon codes ---------------------------------------------------------------
patch("OffersAdminPage-OF1.js", [
  ["import the coupons tab", 'import { dealText } from "./OffersKit-OF1.js";', 'import { dealText } from "./OffersKit-OF1.js";\nimport { CouponsTab as RGcouponsTab } from "./CouponsAdminPage-CP1.js";'],
  ["tab style", ".oa-preview{max-width:340px}\n", ".oa-preview{max-width:340px}\n.oa-tabs{margin:0 0 var(--space-4)}\n"],
  ["offers list becomes a tab", "export function OffersAdminPage() {\n  useCss();", "function OffersTab({ tabs = null }) {\n  useCss();"],
  [
    "tabs under the header",
    '      actions: canManage ? el(Button, { variant: "primary", onClick: () => setDialog({ kind: "edit", id: null }) }, "New offer") : null\n    }),\n',
    '      actions: canManage ? el(Button, { variant: "primary", onClick: () => setDialog({ kind: "edit", id: null }) }, "New offer") : null\n    }),\n    tabs,\n'
  ],
  [
    "page with tabs",
    "function UsesDialog({ offer, onClose }) {",
    `/* coupons kit: the page is two tabs, Offers and Coupon codes. ?tab=coupons opens the second. */
export function OffersAdminPage() {
  useCss();
  const [tab, setTab] = React.useState(() => (new URLSearchParams(window.location.search).get("tab") === "coupons" ? "coupons" : "offers"));
  const go = (t) => {
    setTab(t);
    try {
      const u = new URL(window.location.href);
      if (t === "coupons") u.searchParams.set("tab", "coupons");
      else u.searchParams.delete("tab");
      window.history.replaceState(window.history.state, "", u);
    } catch (e) {}
  };
  const btn = (t, label) => el("button", { type: "button", role: "tab", "aria-selected": tab === t ? "true" : "false", "aria-pressed": tab === t ? "true" : "false", "data-tab": t, onClick: () => go(t) }, label);
  const tabs = el("div", { className: "oa-seg oa-tabs", role: "tablist", "aria-label": "Offers and coupon codes" }, btn("offers", "Offers"), btn("coupons", "Coupon codes"));
  return tab === "coupons" ? el(RGcouponsTab, { tabs }) : el(OffersTab, { tabs });
}

function UsesDialog({ offer, onClose }) {`
  ]
]);

// ---- checkout picker ------------------------------------------------------------------------------
patch("OffersKit-OF1.js", [
  [
    "import",
    'import { r as React, j as X, p as Button, I as Input, D as unwrap, E as http, A as ApiError, u as useNavigate } from "./index-DI5X4c9f.js";',
    'import { r as React, j as X, p as Button, I as Input, D as unwrap, E as http, A as ApiError, u as useNavigate } from "./index-DI5X4c9f.js";\nimport { previewCoupon as RGpreviewCoupon, CouponChips as RGcouponChips, couponDeal as RGcouponDeal } from "./CouponsKit-CP1.js";'
  ],
  ["coupon state", "  const touched = React.useRef(false);\n", "  const touched = React.useRef(false);\n  /* coupons kit: the coupon in use (it replaces the automatic offer), and why a typed code is unknown. */\n  const couponRef = React.useRef(null);\n  const notFound = React.useRef(null);\n"],
  [
    "selection carries the coupon",
    "          ? { offerIds: applied.map((a) => a.offerId), discount: d.discountAmount, total: d.totalAmount, original: d.originalTotal, applied }",
    "          ? { offerIds: d.coupon ? [] : applied.map((a) => a.offerId), couponCode: d.coupon ? d.coupon.code : null, discount: d.discountAmount, total: d.totalAmount, original: d.originalTotal, applied }"
  ],
  [
    "coupon preview body",
    '        baseAmounts: module === "booking" ? baseAmounts : null\n      })\n    );\n',
    '        baseAmounts: module === "booking" ? baseAmounts : null\n      })\n    );\n  const couponBody = (c) => ({ module, code: c, passPlanId, workspaceId, baseAmounts: module === "booking" ? baseAmounts : null });\n'
  ],
  [
    "new purchase: keep the coupon if it still fits",
    "        const auto = !touched.current;\n",
    `        if (couponRef.current) {
          const cp = await RGpreviewCoupon(couponBody(couponRef.current));
          if (cp.valid) {
            if (!dead) {
              setSel([]);
              setData(cp);
              publish(cp);
            }
            return;
          }
          couponRef.current = null;
          touched.current = false;
          if (!dead) setMsg(\`Coupon removed: \${cp.error}\`);
        }
        const auto = !touched.current;
`
  ],
  ["unknown code message", "  const choose = async (ids, typed) => {", "  const choose = async (ids, typed, unknown) => {"],
  ["unknown code message 2", "      if (d.error) {\n        setMsg(d.error);\n        return false;\n      }", "      if (d.error) {\n        setMsg(unknown && /^No offer matches/.test(d.error) ? unknown : d.error);\n        return false;\n      }"],
  ["an offer replaces the coupon", "      const applied = d.applied.map((a) => a.offerId);\n      setSel(applied);", "      couponRef.current = null;\n      const applied = d.applied.map((a) => a.offerId);\n      setSel(applied);"],
  [
    "typed code: coupon first",
    `    if (await choose([], c)) {
      setCode("");
      setShowCode(false);
    }`,
    `    if (await tryCoupon(c)) return;
    if (await choose([], c, notFound.current)) {
      setCode("");
      setShowCode(false);
    }`
  ],
  [
    "coupon helpers",
    "  const applyCode = async () => {",
    `  /* coupons kit: true when the code is a coupon (applied, or refused with the reason); false when no
     coupon has it, so it is tried as an offer code as before. */
  const tryCoupon = async (c) => {
    setBusy(true);
    setMsg("");
    try {
      const cp = await RGpreviewCoupon(couponBody(c));
      if (!cp.found) {
        notFound.current = cp.error;
        return false;
      }
      if (!cp.valid) {
        setMsg(cp.error);
        return true;
      }
      couponRef.current = cp.coupon.code;
      setSel([]);
      setData(cp);
      publish(cp);
      setCode("");
      setShowCode(false);
      return true;
    } catch (e) {
      setMsg(errMsg(e, "Could not check the coupon. Try again."));
      return true;
    } finally {
      setBusy(false);
    }
  };
  const removeCoupon = async () => {
    couponRef.current = null;
    touched.current = false;
    setMsg("");
    setBusy(true);
    try {
      const d = await preview([], null, true);
      setSel((d.applied || []).map((a) => a.offerId));
      setData(d);
      publish(d);
    } catch (e) {
      setData(null);
      publish(null);
    } finally {
      setBusy(false);
    }
  };
  const pickCoupon = async (c) => {
    if (!(await tryCoupon(c))) setMsg(notFound.current || "This coupon is not available any more.");
  };
  const applyCode = async () => {`
  ],
  ["code field label", 'label: "Offer code",', 'label: "Coupon or offer code",'],
  ["code link label", '"Have an offer code?"', '"Have a coupon or offer code?"'],
  [
    "available coupons (no offer)",
    'return el("div", { className: "of-pick of-pick--quiet", "data-rg": "offer-picker" }, codeForm, msg',
    'return el("div", { className: "of-pick of-pick--quiet", "data-rg": "offer-picker" }, codeForm, el(RGcouponChips, { module, disabled: disabled || busy, onPick: pickCoupon }), msg'
  ],
  ["heading", 'el("span", null, applied.length > 1 ? "Offers applied" : "Offer applied")', 'el("span", null, data.coupon ? "Coupon applied" : applied.length > 1 ? "Offers applied" : "Offer applied")'],
  ["coupon deal", 'el("span", { className: "of-opt__deal" }, o.discountType ? dealText(o)', 'el("span", { className: "of-opt__deal" }, data.coupon && a.offerId === 0 ? RGcouponDeal(data.coupon) : o.discountType ? dealText(o)'],
  [
    "remove coupon + available coupons",
    "\n    codeForm,\n",
    `
    data.coupon
      ? el("button", { type: "button", className: "of-link", "data-rg": "coupon-remove", disabled: disabled || busy, style: { marginTop: "var(--space-2)", marginRight: "var(--space-3)" }, onClick: removeCoupon }, "Remove coupon")
      : null,
    codeForm,
    el(RGcouponChips, { module, disabled: disabled || busy, current: data.coupon ? data.coupon.code : null, onPick: pickCoupon }),
`
  ]
]);

// ---- Book a workspace (bookings + Virtual Office) ------------------------------------------------
patch("BookWorkspacePage-MB2.js", [
  [
    "import",
    'import { OfferPicker as RGofferPicker, applyBookingOffers as RGapplyOffers } from "./OffersKit-OF1.js";',
    'import { OfferPicker as RGofferPicker, applyBookingOffers as RGapplyOffers } from "./OffersKit-OF1.js";\nimport { applyBookingCoupon as RGapplyCoupon } from "./CouponsKit-CP1.js";'
  ],
  [
    "coupon on the hold",
    "  if (!offer || !offer.offerIds || offer.offerIds.length === 0) return;\n  try {\n    await RGapplyOffers(ids, offer.offerIds);",
    "  if (!offer || ((!offer.offerIds || offer.offerIds.length === 0) && !offer.couponCode)) return;\n  try {\n    if (offer.couponCode) await RGapplyCoupon(ids, offer.couponCode);\n    else await RGapplyOffers(ids, offer.offerIds);"
  ],
  ["Virtual Office: coupon code", "offerIds: RGoffer ? RGoffer.offerIds : null", "offerIds: RGoffer ? RGoffer.offerIds : null, couponCode: RGoffer ? RGoffer.couponCode : null", 2]
]);

// ---- Passes ---------------------------------------------------------------------------------------
patch("PassesPage-CxAkHUg-.js", [
  ["import", 'import{OfferStrip as RGofferStrip,OfferPicker as RGofferPicker}from"./OffersKit-OF1.js";', 'import{OfferStrip as RGofferStrip,OfferPicker as RGofferPicker}from"./OffersKit-OF1.js";import{CouponStrip as RGcouponStrip}from"./CouponsKit-CP1.js";'],
  ["coupon code on purchase and order", "offerIds:RGoffer?RGoffer.offerIds:null", "offerIds:RGoffer?RGoffer.offerIds:null,couponCode:RGoffer?RGoffer.couponCode:null", 3],
  ["coupon cards", 's.jsx(RGofferStrip,{module:d===1?"pass":"membership",heading:"Pass offers"}),', 's.jsx(RGofferStrip,{module:d===1?"pass":"membership",heading:"Pass offers"}),s.jsx(RGcouponStrip,{module:d===1?"pass":"membership",heading:"Coupon codes"}),'],
  /* The order summary showed the price and the GST without the discount (also with an offer), so its
     lines did not add up to the discounted total. Add the discount row and show the GST charged. */
  [
    "order summary: discount row and the GST charged",
    's.jsxs("div",{children:[s.jsx("span",{children:"CGST + SGST"}),s.jsx("span",{children:RGprice(RGopt?RGopt.cgstAmount+RGopt.sgstAmount:(o.totalAmount??o.price)-o.price)})]})',
    'RGoffer?s.jsxs("div",{"data-rg":"pass-discount-row",children:[s.jsx("span",{children:(RGoffer.couponCode?"Coupon":"Offer")+" discount (before GST)"}),s.jsxs("span",{style:{color:"var(--color-success)"},children:["− ",RGprice(RGoffer.discount)]})]}):null,s.jsxs("div",{children:[s.jsx("span",{children:"CGST + SGST"}),s.jsx("span",{children:RGprice(RGoffer?Math.round((RGoffer.total-((RGopt?RGopt.taxableAmount:o.price)-RGoffer.discount))*100)/100:RGopt?RGopt.cgstAmount+RGopt.sgstAmount:(o.totalAmount??o.price)-o.price)})]})'
  ]
]);

// ---- Membership -----------------------------------------------------------------------------------
patch("MembershipPage-Bk9mQr2v.js", [
  ["import", 'import{OfferStrip as RGofferStrip,OfferPicker as RGofferPicker}from"./OffersKit-OF1.js";', 'import{OfferStrip as RGofferStrip,OfferPicker as RGofferPicker}from"./OffersKit-OF1.js";import{CouponStrip as RGcouponStrip}from"./CouponsKit-CP1.js";'],
  ["coupon code on purchase and order", "offerIds:RGoffer?RGoffer.offerIds:null", "offerIds:RGoffer?RGoffer.offerIds:null,couponCode:RGoffer?RGoffer.couponCode:null", 3],
  ["coupon cards", 'e.jsx(RGofferStrip,{module:"membership",heading:"Membership offers"}),', 'e.jsx(RGofferStrip,{module:"membership",heading:"Membership offers"}),e.jsx(RGcouponStrip,{module:"membership",heading:"Coupon codes"}),'],
  ["order summary names a coupon", 'label:"Offer discount (before GST)"', 'label:(RGo.couponCode?"Coupon":"Offer")+" discount (before GST)"']
]);

// ---- sw.js -------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const KIT = [...NEW_FILES, "OffersAdminPage-OF1.js", "OffersKit-OF1.js", "BookWorkspacePage-MB2.js", "PassesPage-CxAkHUg-.js", "MembershipPage-Bk9mQr2v.js"];
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
