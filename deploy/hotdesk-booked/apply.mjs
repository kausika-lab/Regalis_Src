// Hot desks on the monthly pick list (01 Oct 2026).
//   node apply.mjs <frontend dir>
// A booked workspace with no monthly price (every hot desk) shows "Booked" on its price
// line instead of "Monthly price not set". Writes BookWorkspacePage-MB2.js (MB1 stays),
// points the index at it and bumps sw.js. Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "hotdesk-booked-1";
const OLD = "BookWorkspacePage-MB1.js", NEW = "BookWorkspacePage-MB2.js";
const A = (f) => path.join(front, "assets", f);

let page = fs.readFileSync(A(OLD), "utf8");
const from = ': ["Monthly price not set"]';
const to = ': [t.unavailableReason === "Booked" ? "Booked" : "Monthly price not set"]';
if (page.split(from).length !== 2) throw new Error(`${OLD}: price line anchor not found once`);
fs.writeFileSync(A(NEW), page.replace(from, to), "utf8");
console.log(`OK  - ${NEW}: booked price line`);

const idx = "index-DI5X4c9f.js";
let t = fs.readFileSync(A(idx), "utf8");
if (t.includes(OLD)) {
  const n = t.split(OLD).length - 1;
  if (n !== 2) throw new Error(`${idx}: expected 2 references to ${OLD}, found ${n}`);
  fs.writeFileSync(A(idx), t.replaceAll(OLD, NEW), "utf8");
  console.log(`OK  - ${idx}: import + mapDeps -> ${NEW}`);
} else if (t.includes(NEW)) console.log(`OK  - ${idx}: already points at ${NEW}`);
else throw new Error(`${idx}: no reference to ${OLD}`);

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
sw = sw.replace(/\{url:"assets\/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}/, `{url:"assets/index-DI5X4c9f.js",revision:"${REV}"}`);
if (!sw.includes(`assets/${NEW}`)) {
  sw = sw.replace(/(\{url:"assets\/index-DI5X4c9f\.js",revision:"[^"]*"\})/, `$1,{url:"assets/${NEW}",revision:"${REV}"}`);
}
if (!sw.includes(`assets/${NEW}`) || !sw.includes(`index-DI5X4c9f.js",revision:"${REV}"`)) throw new Error("sw.js not updated");
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
