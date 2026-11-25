/**
 * Offers in the customer portal (23 Nov 2026, kit deploy/offers).
 *   OfferStrip   promotional cards for one module (My Bookings, Passes, Membership)
 *   OfferPicker  choose offers / type a code at "Review & pay"; shows original, discount, final
 *   applyBookingOffers(bookingIds, offerIds)  puts the chosen offers on held bookings before paying
 *
 * Every amount comes from the server (/api/offers/preview), which prices exactly as checkout will
 * charge; the server re-checks the offers again before any money moves. Nothing about an offer is
 * hard-coded here. Hand-maintained chunk, imported by the patched page chunks (see apply.mjs).
 */
import { r as React, j as X, p as Button, I as Input, D as unwrap, E as http, A as ApiError, u as useNavigate } from "./index-DI5X4c9f.js";
import { previewCoupon as RGpreviewCoupon, CouponChips as RGcouponChips, couponDeal as RGcouponDeal } from "./CouponsKit-CP1.js";

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
.of-strip{margin:0 0 var(--space-4)}
.of-strip__head{display:flex;align-items:baseline;justify-content:space-between;gap:var(--space-3);margin-bottom:var(--space-2)}
.of-strip__title{font-family:var(--font-display);font-size:var(--font-size-lg);font-weight:700;margin:0}
.of-strip__list{display:grid;grid-template-columns:repeat(auto-fill,minmax(260px,1fr));gap:var(--space-3)}
@media (max-width:640px){.of-strip__list{display:flex;overflow-x:auto;scroll-snap-type:x mandatory;padding-bottom:4px}.of-strip__list>.of-card{flex:0 0 84%;scroll-snap-align:start}}
.of-card{position:relative;display:flex;flex-direction:column;background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);box-shadow:var(--shadow-sm);overflow:hidden;min-width:0}
.of-card__img{display:block;width:100%;aspect-ratio:16/6;object-fit:cover;background:rgb(var(--accent-rgb) / 10%)}
.of-card__body{padding:var(--space-3) var(--space-4) var(--space-4);display:flex;flex-direction:column;gap:6px;flex:1}
.of-card__deal{display:inline-flex;align-self:flex-start;align-items:center;gap:6px;background:var(--regalis-black);color:var(--regalis-gold-light);border-radius:999px;padding:4px 12px;font-weight:800;font-size:var(--font-size-sm);letter-spacing:.02em}
.of-card__title{font-weight:800;font-size:var(--font-size-md);line-height:1.3}
.of-card__desc{color:var(--text-secondary);font-size:var(--font-size-sm);line-height:1.45}
.of-card__meta{color:var(--text-muted);font-size:var(--font-size-xs)}
.of-code{display:inline-flex;align-items:center;gap:8px;border:1px dashed var(--accent);border-radius:var(--radius-sm);padding:4px 6px 4px 10px;background:rgb(var(--accent-rgb) / 8%);align-self:flex-start;max-width:100%}
.of-code b{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;letter-spacing:.06em;overflow-wrap:anywhere}
.of-terms{font-size:var(--font-size-xs);color:var(--text-secondary)}
.of-terms summary{cursor:pointer;color:var(--regalis-gold-dark);font-weight:600}
.of-terms p{white-space:pre-line;margin:6px 0 0;line-height:1.45}
.of-pick{margin:var(--space-3) 0;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3);background:rgb(var(--accent-rgb) / 4%);min-width:0}
.of-pick__h{display:flex;align-items:center;justify-content:space-between;gap:8px;font-weight:700;margin-bottom:var(--space-2)}
.of-pick__list{display:flex;flex-direction:column;gap:8px}
.of-opt{all:unset;box-sizing:border-box;display:flex;gap:10px;align-items:flex-start;width:100%;border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:10px 12px;background:var(--surface-card);cursor:pointer}
.of-opt:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.of-opt--on{border-color:var(--accent);background:rgb(var(--accent-rgb) / 12%)}
.of-opt--off{cursor:not-allowed;opacity:.62}
.of-opt--static{cursor:default}
.of-pick--quiet{border:0;background:none;padding:0}
.of-opt__tick{flex:none;width:18px;height:18px;border-radius:50%;border:2px solid var(--border-strong);margin-top:2px;display:grid;place-items:center;font-size:11px;font-weight:900;color:var(--regalis-black)}
.of-opt--on .of-opt__tick{background:var(--accent);border-color:var(--accent)}
.of-opt__main{flex:1;min-width:0}
.of-opt__title{font-weight:700;font-size:var(--font-size-sm)}
.of-opt__sub{font-size:var(--font-size-xs);color:var(--text-secondary);margin-top:2px}
.of-opt__deal{flex:none;font-weight:800;font-size:var(--font-size-sm);color:var(--regalis-gold-dark);white-space:nowrap}
.of-codeform{display:flex;gap:8px;align-items:flex-end;margin-top:var(--space-2)}
.of-codeform .field{flex:1;min-width:0;margin:0}
.of-msg{font-size:var(--font-size-sm);margin-top:var(--space-2);color:var(--color-danger)}
.of-sum{margin-top:var(--space-3);border-top:1px dashed var(--border-strong);padding-top:var(--space-2);font-size:var(--font-size-sm)}
.of-sum div{display:flex;justify-content:space-between;gap:8px;padding:3px 0}
.of-sum .of-save{color:var(--color-success);font-weight:700}
.of-sum .of-final{font-weight:800;font-size:var(--font-size-md);border-top:1px solid var(--border-subtle);margin-top:4px;padding-top:6px}
.of-strike{text-decoration:line-through;color:var(--text-muted);font-weight:400;margin-right:6px}
.of-link{all:unset;cursor:pointer;color:var(--regalis-gold-dark);font-weight:600;font-size:var(--font-size-sm)}
`;
function useCss() {
  React.useEffect(() => {
    if (document.getElementById("rg-offers-css")) return;
    const s = document.createElement("style");
    s.id = "rg-offers-css";
    s.textContent = CSS;
    document.head.appendChild(s);
  }, []);
}

const money = (n) => "₹" + Number(n || 0).toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const short = (n) => {
  const v = Number(n || 0);
  return "₹" + v.toLocaleString("en-IN", { minimumFractionDigits: v % 1 ? 2 : 0, maximumFractionDigits: 2 });
};
const day = (d) => {
  if (!d) return "";
  const [y, m, dd] = String(d).slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, dd).toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" });
};
const errMsg = (e, fb) => (e instanceof ApiError ? e.message : fb);

/** "20% OFF" / "₹500 OFF". */
export function dealText(o) {
  return o.discountType === 1 ? `${Number(o.discountValue)}% OFF` : `${short(o.discountValue)} OFF`;
}
/** "Up to ₹1,000 off · on ₹5,000 or more (before GST)". */
export function ruleText(o) {
  const parts = [];
  if (o.discountType === 1 && o.maxDiscountAmount) parts.push(`Up to ${short(o.maxDiscountAmount)} off`);
  if (o.minAmount) parts.push(`On ${short(o.minAmount)} or more (before GST)`);
  if (o.appliesTo && o.appliesTo.length)
    parts.push(o.appliesTo.length <= 3 ? `For ${o.appliesTo.join(", ")}` : `For ${o.appliesTo.slice(0, 2).join(", ")} and ${o.appliesTo.length - 2} more`);
  if (o.usesLeft !== null && o.usesLeft !== void 0) parts.push(o.usesLeft === 0 ? "Already used" : `${o.usesLeft} use${o.usesLeft === 1 ? "" : "s"} left for you`);
  return parts.join(" · ");
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

function CodeChip({ code }) {
  const [done, setDone] = React.useState(false);
  return el(
    "span",
    { className: "of-code" },
    el("b", null, code),
    el(
      Button,
      {
        size: "sm",
        variant: "ghost",
        "aria-label": `Copy code ${code}`,
        onClick: async (e) => {
          e.stopPropagation();
          if (await copyText(code)) {
            setDone(true);
            setTimeout(() => setDone(false), 1500);
          }
        }
      },
      done ? "Copied" : "Copy"
    )
  );
}

function OfferCard({ offer: o }) {
  const rules = ruleText(o);
  return el(
    "article",
    { className: "of-card", "data-offer": o.offerId },
    o.imageUrl ? el("img", { className: "of-card__img", src: o.imageUrl, alt: "", loading: "lazy" }) : null,
    el(
      "div",
      { className: "of-card__body" },
      el("span", { className: "of-card__deal" }, dealText(o)),
      el("div", { className: "of-card__title" }, o.title),
      o.description ? el("div", { className: "of-card__desc" }, o.description) : null,
      rules ? el("div", { className: "of-card__meta" }, rules) : null,
      el("div", { className: "of-card__meta" }, `Valid till ${day(o.endDate)}`),
      o.code ? el(CodeChip, { code: o.code }) : null,
      o.terms ? el("details", { className: "of-terms" }, el("summary", null, "Terms & conditions"), el("p", null, o.terms)) : null
    )
  );
}

/** Promotional cards for one module; renders nothing when the admin has no live offer there. */
export function OfferStrip({ module, heading = "Offers for you", ctaLabel = null, ctaTo = null }) {
  useCss();
  const navigate = useNavigate();
  const action = ctaLabel && ctaTo ? el(Button, { size: "sm", variant: "outline", onClick: () => navigate(ctaTo) }, ctaLabel) : null;
  const [offers, setOffers] = React.useState(null);
  React.useEffect(() => {
    let dead = false;
    unwrap(http.get("/offers", { params: { module } }))
      .then((d) => dead || setOffers(Array.isArray(d) ? d : []))
      .catch(() => dead || setOffers([]));
    return () => {
      dead = true;
    };
  }, [module]);
  if (!offers || offers.length === 0) return null;
  return el(
    "section",
    { className: "of-strip", "aria-label": heading, "data-rg": "offer-strip" },
    el("div", { className: "of-strip__head" }, el("h2", { className: "of-strip__title" }, heading), action),
    el(
      "div",
      { className: "of-strip__list" },
      offers.map((o) => el(OfferCard, { key: o.offerId, offer: o }))
    )
  );
}

/**
 * Offer selection at checkout.
 *   module       "booking" | "pass" | "membership"
 *   passPlanId   pass / membership plan being bought
 *   workspaceId  + baseAmounts (price before GST of each booking) for bookings
 *   onChange(sel) sel = { offerIds, discount, total, original, applied } or null when no offer
 */
export function OfferPicker({ module, passPlanId = null, workspaceId = null, baseAmounts = null, disabled = false, onChange }) {
  useCss();
  const ctx = JSON.stringify([module, passPlanId, workspaceId, baseAmounts]);
  const [sel, setSel] = React.useState([]);
  const [data, setData] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const [msg, setMsg] = React.useState("");
  const [code, setCode] = React.useState("");
  const [showCode, setShowCode] = React.useState(false);
  /* Offers apply by themselves (the best one for this purchase); once the customer removes or
     changes them, their choice is kept. */
  const touched = React.useRef(false);
  /* coupons kit: the coupon in use (it replaces the automatic offer), and why a typed code is unknown. */
  const couponRef = React.useRef(null);
  const notFound = React.useRef(null);
  const cb = React.useRef(onChange);
  cb.current = onChange;
  const ready = module === "booking" ? Array.isArray(baseAmounts) && baseAmounts.length > 0 : !!passPlanId;

  const publish = (d) => {
    const applied = d && d.applied ? d.applied : [];
    cb.current &&
      cb.current(
        applied.length
          ? { offerIds: d.coupon ? [] : applied.map((a) => a.offerId), couponCode: d.coupon ? d.coupon.code : null, discount: d.discountAmount, total: d.totalAmount, original: d.originalTotal, applied }
          : null
      );
  };
  const preview = (ids, typed, auto = false) =>
    unwrap(
      http.post("/offers/preview", {
        module,
        offerIds: ids,
        code: typed || null,
        auto,
        passPlanId,
        workspaceId,
        baseAmounts: module === "booking" ? baseAmounts : null
      })
    );
  const couponBody = (c) => ({ module, code: c, passPlanId, workspaceId, baseAmounts: module === "booking" ? baseAmounts : null });

  /* New purchase context: keep the choice if it still applies there, otherwise drop it. */
  React.useEffect(() => {
    if (!ready) {
      setData(null);
      publish(null);
      return;
    }
    let dead = false;
    setBusy(true);
    publish(null); /* nothing is charged at an old discount while the new purchase is priced */
    (async () => {
      try {
        if (couponRef.current) {
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
          if (!dead) setMsg(`Coupon removed: ${cp.error}`);
        }
        const auto = !touched.current;
        let d = await preview(auto ? [] : sel, null, auto);
        if (d.error && sel.length) {
          const why = d.error;
          d = await preview([], null);
          if (!dead) setMsg(`Offer removed: ${why}`);
        }
        if (!dead) {
          setSel((d.applied || []).map((a) => a.offerId));
          setData(d);
          publish(d);
        }
      } catch (e) {
        if (!dead) {
          setData(null);
          publish(null);
        }
      } finally {
        if (!dead) setBusy(false);
      }
    })();
    return () => {
      dead = true;
    };
  }, [ctx]);

  const choose = async (ids, typed, unknown) => {
    touched.current = true;
    setBusy(true);
    setMsg("");
    try {
      const d = await preview(ids, typed);
      if (d.error) {
        setMsg(unknown && /^No offer matches/.test(d.error) ? unknown : d.error);
        return false;
      }
      couponRef.current = null;
      const applied = d.applied.map((a) => a.offerId);
      setSel(applied);
      setData(d);
      publish(d);
      return true;
    } catch (e) {
      setMsg(errMsg(e, "Could not check the offer. Try again."));
      return false;
    } finally {
      setBusy(false);
    }
  };
  /* coupons kit: true when the code is a coupon (applied, or refused with the reason); false when no
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
  const applyCode = async () => {
    const c = code.trim();
    if (!c) return;
    if (await tryCoupon(c)) return;
    if (await choose([], c, notFound.current)) {
      setCode("");
      setShowCode(false);
    }
  };

  if (!ready || !data) return null;
  const applied = data.applied || [];
  const byId = Object.fromEntries((data.offers || []).map((r) => [r.offer.offerId, r.offer]));
  const codeForm = showCode
    ? el(
        "div",
        { className: "of-codeform" },
        el(Input, {
          label: "Coupon or offer code",
          value: code,
          maxLength: 30,
          placeholder: "Enter code",
          autoComplete: "off",
          onChange: (e) => setCode(e.target.value.toUpperCase()),
          onKeyDown: (e) => {
            if (e.key === "Enter") {
              e.preventDefault();
              applyCode();
            }
          }
        }),
        el(Button, { variant: "outline", disabled: disabled || busy || !code.trim(), loading: busy && !!code.trim(), onClick: applyCode }, "Apply")
      )
    : el("button", { type: "button", className: "of-link", style: { marginTop: "var(--space-2)" }, onClick: () => setShowCode(true) }, "Have a coupon or offer code?");
  /* No offer for this purchase: only the small code link, nothing to choose. */
  if (applied.length === 0)
    return el("div", { className: "of-pick of-pick--quiet", "data-rg": "offer-picker" }, codeForm, el(RGcouponChips, { module, disabled: disabled || busy, onPick: pickCoupon }), msg ? el("div", { className: "of-msg", role: "alert" }, msg) : null);
  /* Applied automatically by the server (the best saving); shown, not chosen. */
  return el(
    "div",
    { className: "of-pick", "data-rg": "offer-picker", "aria-busy": busy ? "true" : "false" },
    el("div", { className: "of-pick__h" }, el("span", null, data.coupon ? "Coupon applied" : applied.length > 1 ? "Offers applied" : "Offer applied")),
    el(
      "div",
      { className: "of-pick__list" },
      applied.map((a) => {
        const o = byId[a.offerId] || { title: a.title, code: a.code };
        const sub = [o.code ? `Code ${o.code}` : null, o.endDate ? `Valid till ${day(o.endDate)}` : null].filter(Boolean).join(" · ");
        return el(
          "div",
          { key: a.offerId, className: "of-opt of-opt--on of-opt--static", "data-offer": a.offerId, "data-applied": "true" },
          el("span", { className: "of-opt__main" }, el("div", { className: "of-opt__title" }, o.title || a.title), sub ? el("div", { className: "of-opt__sub" }, sub) : null),
          el("span", { className: "of-opt__deal" }, data.coupon && a.offerId === 0 ? RGcouponDeal(data.coupon) : o.discountType ? dealText(o) : "− " + money(a.discountAmount))
        );
      })
    ),
    data.coupon
      ? el("button", { type: "button", className: "of-link", "data-rg": "coupon-remove", disabled: disabled || busy, style: { marginTop: "var(--space-2)", marginRight: "var(--space-3)" }, onClick: removeCoupon }, "Remove coupon")
      : null,
    codeForm,
    el(RGcouponChips, { module, disabled: disabled || busy, current: data.coupon ? data.coupon.code : null, onPick: pickCoupon }),
    msg ? el("div", { className: "of-msg", role: "alert" }, msg) : null,
    applied.length
      ? el(
          "div",
          { className: "of-sum", "data-rg": "offer-summary" },
          el("div", null, el("span", null, "Original amount (incl. GST)"), el("span", null, money(data.originalTotal))),
          applied.map((a) => el("div", { key: a.offerId, className: "of-save" }, el("span", null, `${a.code ? a.code : a.title} discount (before GST)`), el("span", null, "− " + money(a.discountAmount)))),
          el("div", null, el("span", null, "Price after discount"), el("span", null, money(data.taxableAmount))),
          el("div", null, el("span", null, `CGST + SGST on the discounted price`), el("span", null, money(data.cgstAmount + data.sgstAmount))),
          el("div", { className: "of-final" }, el("span", null, "You pay"), el("span", null, el("span", { className: "of-strike" }, money(data.originalTotal)), money(data.totalAmount))),
          el("div", { className: "of-save", style: { justifyContent: "flex-end" } }, `You save ${money(data.originalTotal - data.totalAmount)}`)
        )
      : null
  );
}

/** Put the chosen offers on the customer's held booking(s) just before paying. The server
 *  validates and re-prices them; the payment then charges the discounted total. */
export function applyBookingOffers(bookingIds, offerIds) {
  return unwrap(http.post("/offers/bookings/apply", { bookingIds, offerIds }));
}
