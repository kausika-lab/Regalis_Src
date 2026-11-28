// po-shipping-label kit (28 Nov 2026): frontend part.
//   node apply.mjs <frontend dir>
// "Deliver to" -> "Shipping to" on the admin purchase order screens (the PDF label is the backend part).
// Each edit must match exactly once; an edit already present is skipped (idempotent).
//
//   AdminPurchaseOrderPages-C1FTHC0p.js  PO detail block title; PO form "Deliver to:" line
//   sw.js                                precache revision for that chunk
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "po-shipping-label-1";
const CHUNK = "AdminPurchaseOrderPages-C1FTHC0p.js";

function patch(p, edits) {
  if (!fs.existsSync(p)) throw new Error(`${p} not found`);
  let t = fs.readFileSync(p, "utf8");
  for (const [name, from, to] of edits) {
    if (t.split(to).length - 1 === 1) { console.log(`OK  - ${name} (already done)`); continue; }
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`'${name}' expected 1 match, found ${n}; nothing written for ${p}`);
    t = t.split(from).join(to);
    console.log(`OK  - ${name}`);
  }
  fs.writeFileSync(p, t, "utf8");
}

patch(path.join(front, "assets", CHUNK), [
  ["detail: block title",
   'e.jsx("h3", { className: "po-block-title", children: "Deliver to" })',
   'e.jsx("h3", { className: "po-block-title", children: "Shipping to" })'],
  ["form: address line",
   'e.jsx("b", { children: "Deliver to: " })',
   'e.jsx("b", { children: "Shipping to: " })'],
]);

const sw = path.join(front, "sw.js");
let s = fs.readFileSync(sw, "utf8");
const re = new RegExp(`(\{url:"assets/${CHUNK.replace(/[.-]/g, "\$&")}",revision:)("[^"]*"|null)\}`);
if (!re.test(s)) throw new Error(`sw.js: no precache entry for ${CHUNK}`);
s = s.replace(re, `$1"${REV}"}`);
fs.writeFileSync(sw, s, "utf8");
console.log(`OK  - sw.js revision ${REV}`);
