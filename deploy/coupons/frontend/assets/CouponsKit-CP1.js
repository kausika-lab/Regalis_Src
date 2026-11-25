/**
 * Coupon codes in the customer portal (25 Nov 2026, kit deploy/coupons).
 *   previewCoupon(body)               checks a typed code against the purchase and prices it
 *   applyBookingCoupon(ids, code)     puts the coupon on held bookings before paying
 *   couponDeal(c)                     "20% OFF" / "₹500 OFF"
 *
 * Coupons are never listed to customers (product-owner decision, 25 Nov 2026): a coupon is applied
 * only when the customer types a code that matches one. Every amount and every message comes from
 * the server (/api/coupons/*), which prices exactly as checkout will charge and checks the coupon
 * again before any money moves. A coupon replaces the automatic offer. Hand-maintained chunk,
 * imported by OffersKit-OF1.js, BookWorkspacePage-MB2.js and CouponsAdminPage-CP1.js (apply.mjs).
 */
import { D as unwrap, E as http } from "./index-DI5X4c9f.js";

const short = (n) => {
  const v = Number(n || 0);
  return "₹" + v.toLocaleString("en-IN", { minimumFractionDigits: v % 1 ? 2 : 0, maximumFractionDigits: 2 });
};

/** "10% OFF" / "₹500 OFF". */
export function couponDeal(c) {
  return c.discountType === 1 ? `${Number(c.discountValue)}% OFF` : `${short(c.discountValue)} OFF`;
}

export const previewCoupon = (body) => unwrap(http.post("/coupons/preview", body));

/** Put the coupon on the customer's held booking(s) just before paying. It replaces any offer. */
export const applyBookingCoupon = (bookingIds, code) => unwrap(http.post("/coupons/bookings/apply", { bookingIds, code }));
