/**
 * Coupon codes in the customer portal (25 Nov 2026, kit deploy/coupons).
 *   CouponStrip   cards for the listed coupons of one module (Passes, Membership), with Copy
 *   CouponChips   "Available coupons" at checkout; tapping one applies it (used by OfferPicker)
 *   previewCoupon(body)               checks a typed code against the purchase and prices it
 *   applyBookingCoupon(ids, code)     puts the coupon on held bookings before paying
 *
 * Every amount and every message comes from the server (/api/coupons/*), which prices exactly as
 * checkout will charge and checks the coupon again before any money moves. A coupon replaces the
 * automatic offer. Hand-maintained chunk, imported by OffersKit-OF1.js and the page chunks (apply.mjs).
 */
import { r as React, j as X, p as Button, D as unwrap, E as http } from "./index-DI5X4c9f.js";

function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  const flat = kids.flat(Infinity).filter((k) => k !== null && k !== void 0 && k !== false && k !== "");
  if (flat.length === 1) p.children = flat[0];
  else if (flat.length > 1) p.children = flat;
  return flat.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}

const CSS = `
.cp-strip{margin:0 0 var(--space-4)}
.cp-strip__title{font-family:var(--font-display);font-size:var(--font-size-lg);font-weight:700;margin:0 0 var(--space-2)}
.cp-strip__list{display:grid;grid-template-columns:repeat(auto-fill,minmax(240px,1fr));gap:var(--space-3)}
@media (max-width:640px){.cp-strip__list{display:flex;overflow-x:auto;scroll-snap-type:x mandatory;padding-bottom:4px}.cp-strip__list>.cp-card{flex:0 0 80%;scroll-snap-align:start}}
.cp-card{display:flex;flex-direction:column;gap:6px;background:var(--surface-card);border:1px dashed var(--accent);border-radius:var(--radius-lg);padding:var(--space-3) var(--space-4);box-shadow:var(--shadow-sm);min-width:0}
.cp-card__top{display:flex;align-items:center;justify-content:space-between;gap:var(--space-2)}
.cp-code{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-weight:800;letter-spacing:.06em;font-size:var(--font-size-md);overflow-wrap:anywhere}
.cp-deal{display:inline-flex;align-self:flex-start;background:var(--regalis-black);color:var(--regalis-gold-light);border-radius:999px;padding:3px 10px;font-weight:800;font-size:var(--font-size-xs);white-space:nowrap}
.cp-desc{color:var(--text-secondary);font-size:var(--font-size-sm);line-height:1.45}
.cp-meta{color:var(--text-muted);font-size:var(--font-size-xs)}
.cp-chips{margin-top:var(--space-2)}
.cp-chips__h{font-size:var(--font-size-xs);color:var(--text-secondary);font-weight:700;text-transform:uppercase;letter-spacing:.05em;margin-bottom:4px}
.cp-chips__list{display:flex;flex-wrap:wrap;gap:6px}
.cp-chip{all:unset;box-sizing:border-box;cursor:pointer;display:inline-flex;align-items:center;gap:6px;border:1px dashed var(--accent);border-radius:var(--radius-sm);padding:4px 10px;background:rgb(var(--accent-rgb) / 8%);font-size:var(--font-size-sm);max-width:100%}
.cp-chip:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.cp-chip[aria-disabled=true]{opacity:.55;cursor:not-allowed}
.cp-chip b{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;letter-spacing:.05em}
.cp-chip span{color:var(--text-secondary);font-size:var(--font-size-xs)}
`;
function useCss() {
  React.useEffect(() => {
    if (document.getElementById("rg-coupons-css")) return;
    const s = document.createElement("style");
    s.id = "rg-coupons-css";
    s.textContent = CSS;
    document.head.appendChild(s);
  }, []);
}

const short = (n) => {
  const v = Number(n || 0);
  return "₹" + v.toLocaleString("en-IN", { minimumFractionDigits: v % 1 ? 2 : 0, maximumFractionDigits: 2 });
};
const day = (d) => {
  if (!d) return "";
  const [y, m, dd] = String(d).slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, dd).toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" });
};

/** "10% OFF" / "₹500 OFF". */
export function couponDeal(c) {
  return c.discountType === 1 ? `${Number(c.discountValue)}% OFF` : `${short(c.discountValue)} OFF`;
}
/** "Up to ₹1,000 off · On ₹5,000 or more (before GST) · 1 use left for you". */
export function couponRules(c) {
  const parts = [];
  if (c.discountType === 1 && c.maxDiscountAmount) parts.push(`Up to ${short(c.maxDiscountAmount)} off`);
  if (c.minAmount) parts.push(`On ${short(c.minAmount)} or more (before GST)`);
  if (c.usesLeft !== null && c.usesLeft !== void 0) parts.push(c.usesLeft === 0 ? "Already used" : `${c.usesLeft} use${c.usesLeft === 1 ? "" : "s"} left for you`);
  return parts.join(" · ");
}

export const previewCoupon = (body) => unwrap(http.post("/coupons/preview", body));

/** Put the coupon on the customer's held booking(s) just before paying. It replaces any offer. */
export const applyBookingCoupon = (bookingIds, code) => unwrap(http.post("/coupons/bookings/apply", { bookingIds, code }));

/** The listed coupons of a module (cached for the page, refreshed when the module changes). */
function useCoupons(module) {
  const [list, setList] = React.useState(null);
  React.useEffect(() => {
    let dead = false;
    unwrap(http.get("/coupons", { params: { module } }))
      .then((d) => dead || setList(Array.isArray(d) ? d : []))
      .catch(() => dead || setList([]));
    return () => {
      dead = true;
    };
  }, [module]);
  return list;
}

async function copyText(text) {
  try {
    await navigator.clipboard.writeText(text);
    return true;
  } catch {
    const t = document.createElement("textarea");
    t.value = text;
    t.style.position = "fixed";
    t.style.opacity = "0";
    document.body.appendChild(t);
    t.select();
    const ok = document.execCommand("copy");
    t.remove();
    return ok;
  }
}

function CouponCard({ c }) {
  const [done, setDone] = React.useState(false);
  const rules = couponRules(c);
  return el(
    "article",
    { className: "cp-card", "data-coupon": c.code },
    el(
      "div",
      { className: "cp-card__top" },
      el("span", { className: "cp-code" }, c.code),
      el(
        Button,
        {
          size: "sm",
          variant: "ghost",
          "aria-label": `Copy coupon code ${c.code}`,
          onClick: async () => {
            if (await copyText(c.code)) {
              setDone(true);
              setTimeout(() => setDone(false), 1500);
            }
          }
        },
        done ? "Copied" : "Copy"
      )
    ),
    el("span", { className: "cp-deal" }, couponDeal(c)),
    c.description ? el("div", { className: "cp-desc" }, c.description) : null,
    rules ? el("div", { className: "cp-meta" }, rules) : null,
    el("div", { className: "cp-meta" }, `Valid till ${day(c.validUntil)} · enter the code at payment`)
  );
}

/** Coupon cards for one module; renders nothing when no coupon is listed there. */
export function CouponStrip({ module, heading = "Coupon codes" }) {
  useCss();
  const list = useCoupons(module);
  if (!list || list.length === 0) return null;
  return el(
    "section",
    { className: "cp-strip", "aria-label": heading, "data-rg": "coupon-strip" },
    el("h2", { className: "cp-strip__title" }, heading),
    el(
      "div",
      { className: "cp-strip__list" },
      list.map((c) => el(CouponCard, { key: c.couponId, c }))
    )
  );
}

/** "Available coupons" at checkout. onPick(code) applies it; the server decides whether it fits. */
export function CouponChips({ module, disabled = false, current = null, onPick }) {
  useCss();
  const list = useCoupons(module);
  const usable = (list || []).filter((c) => c.code !== current && c.usesLeft !== 0);
  if (usable.length === 0) return null;
  return el(
    "div",
    { className: "cp-chips", "data-rg": "coupon-chips" },
    el("div", { className: "cp-chips__h" }, "Available coupons"),
    el(
      "div",
      { className: "cp-chips__list" },
      usable.map((c) =>
        el(
          "button",
          {
            key: c.couponId,
            type: "button",
            className: "cp-chip",
            "data-coupon": c.code,
            "aria-disabled": disabled ? "true" : "false",
            title: [c.description, couponRules(c)].filter(Boolean).join(" · ") || void 0,
            onClick: () => disabled || onPick(c.code)
          },
          el("b", null, c.code),
          el("span", null, couponDeal(c)),
          el("span", null, "Apply")
        )
      )
    )
  );
}
