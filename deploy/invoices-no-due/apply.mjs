// Customer Invoices list without the Due column (01 Oct 2026).
//   node apply.mjs <frontend dir>
// Writes InvoicesPage-IV1.js (the old chunk stays), points the index at it and bumps
// sw.js. Overdue invoices still show their status badge. Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "invoices-no-due-1";
const OLD = "InvoicesPage-CPOUVg0j.js", NEW = "InvoicesPage-IV1.js";
const A = (f) => path.join(front, "assets", f);

const page = fs.readFileSync(A(OLD), "utf8");
const due = '{key:"due",header:"Due",render:e=>t.jsx("span",{style:{color:e.isOverdue?"var(--color-danger)":void 0},children:b(e.dueDate)})},';
if (page.split(due).length !== 2) throw new Error(`${OLD}: Due column not found once`);
fs.writeFileSync(A(NEW), page.replace(due, ""), "utf8");
console.log(`OK  - ${NEW}: Due column removed`);

const idx = "index-DI5X4c9f.js";
const t = fs.readFileSync(A(idx), "utf8");
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
