// po-last-used-defaults kit (28 Nov 2026): frontend part.
//   node apply.mjs <frontend dir>
// 1. Terms hint: the terms saved on the last PO become the default for the next new one.
// 2. New PO: pre-fill location + delivery address from the last PO saved
//    (form-options lastDeliveryLocationId / lastDeliveryAddress) when that location is still in
//    the active location list; otherwise the old behaviour (default location + its address).
//    Editing an existing PO is unchanged.
// 3. Purchase.ShipToName (form-options shipToName): shown in place of the location name on the
//    PO detail "Shipping to" block, the PO form "Shipping to:" line and the "Change delivery
//    address" picker. Location ids and selection logic are unchanged; the PO list keeps the
//    location name.
// Requires po-shipping-label applied first ("Shipping to" labels).
// Each edit must match exactly once; an edit already present is skipped (idempotent).
// Anchors are written with \n and matched as \r\n when the file uses CRLF.
//
//   AdminPurchaseOrderPages-C1FTHC0p.js  the edits above
//   sw.js                                precache revision for that chunk
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "po-last-used-defaults-1";
const CHUNK = "AdminPurchaseOrderPages-C1FTHC0p.js";

function patch(p, edits) {
  if (!fs.existsSync(p)) throw new Error(`${p} not found`);
  let t = fs.readFileSync(p, "utf8");
  const crlf = t.includes("\r\n");
  const eol = (s) => (crlf ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo] of edits) {
    const from = eol(rawFrom), to = eol(rawTo);
    if (t.split(to).length - 1 === 1) { console.log(`OK  - ${name} (already done)`); continue; }
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`'${name}' expected 1 match, found ${n}; nothing written for ${p}`);
    t = t.split(from).join(to);
    console.log(`OK  - ${name}`);
  }
  fs.writeFileSync(p, t, "utf8");
}

patch(path.join(front, "assets", CHUNK), [
  ["form: terms hint",
   'hint: "Pre-filled from Settings → Billing → Default purchase order terms. Editable for this PO."',
   'hint: "Pre-filled with the terms saved on the last purchase order. The terms you save here become the default for the next new PO."'],

  ["helper: poShipTo",
   "const addressOfLocation = (loc) =>\n",
   "/* po-last-used-defaults: Purchase.ShipToName, shown in place of the location name on PO screens. */\n" +
   'const poShipTo = (opts) => String(opts?.shipToName ?? "").trim();\n' +
   "const addressOfLocation = (loc) =>\n"],

  ["form: new PO pre-fill from the last PO",
   `          const def = locationPage.items.find((l2) => l2.isDefault) ?? (locationPage.items.length === 1 ? locationPage.items[0] : null);
          setHead((h) => ({
            ...h,
            terms: opts.defaultTermsAndConditions ?? "",
            shipmentPreference: opts.shipmentPreferences[0] ?? "",
            locationId: def ? String(def.locationId) : "",
            deliveryAddress: def ? addressOfLocation(def) || def.locationName : ""
          }));`,
   `          const def = locationPage.items.find((l2) => l2.isDefault) ?? (locationPage.items.length === 1 ? locationPage.items[0] : null);
          /* po-last-used-defaults: start from the last PO's location + address while that location is active */
          const last = opts.lastDeliveryLocationId && String(opts.lastDeliveryAddress ?? "").trim()
            ? locationPage.items.find((l2) => l2.locationId === opts.lastDeliveryLocationId && l2.isActive !== false) : null;
          setHead((h) => ({
            ...h,
            terms: opts.defaultTermsAndConditions ?? "",
            shipmentPreference: opts.shipmentPreferences[0] ?? "",
            locationId: last ? String(last.locationId) : def ? String(def.locationId) : "",
            deliveryAddress: last ? opts.lastDeliveryAddress : def ? addressOfLocation(def) || poShipTo(opts) || def.locationName : ""
          }));`],

  ["detail: Shipping to name",
   'e.jsx("div", { style: { fontWeight: 700 }, children: order.locationName ?? "—" }),',
   'e.jsx("div", { style: { fontWeight: 700 }, children: poShipTo(options) || (order.locationName ?? "—") }),'],

  ["form: Shipping to line",
   'e.jsx("span", { children: head.deliveryAddress || location.locationName })',
   'e.jsx("span", { children: head.deliveryAddress || poShipTo(options) || location.locationName })'],

  ["modal: signature",
   "function DeliveryModal({ locations, locationId, address, onClose, onSaved }) {",
   "function DeliveryModal({ locations, locationId, address, shipToName, onClose, onSaved }) {"],

  ["modal: address fallback",
   "setText(addressOfLocation(loc) || loc.locationName);",
   "setText(addressOfLocation(loc) || shipToName || loc.locationName);"],

  ["modal: option label",
   'e.jsx("strong", { children: loc.locationName }), loc.isDefault',
   'e.jsx("strong", { children: shipToName || loc.locationName }), loc.isDefault'],

  ["modal: call site",
   `        e.jsx(DeliveryModal, {
          locations,
          locationId: head.locationId,`,
   `        e.jsx(DeliveryModal, {
          locations,
          shipToName: poShipTo(options),
          locationId: head.locationId,`],
]);

const sw = path.join(front, "sw.js");
let s = fs.readFileSync(sw, "utf8");
const re = new RegExp(`(\\{url:"assets/${CHUNK.replace(/[.-]/g, "\\$&")}",revision:)("[^"]*"|null)\\}`);
if (!re.test(s)) throw new Error(`sw.js: no precache entry for ${CHUNK}`);
s = s.replace(re, `$1"${REV}"}`);
fs.writeFileSync(sw, s, "utf8");
console.log(`OK  - sw.js revision ${REV}`);
