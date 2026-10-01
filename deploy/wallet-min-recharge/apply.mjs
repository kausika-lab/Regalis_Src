// Wallet minimum recharge ₹1,000 (01 Oct 2026).
//   node apply.mjs <frontend dir>
// Customer "Add funds" dialog: min 1000, hint, default amount 1000, quick amounts
// 1,000 / 2,000 / 5,000 / 10,000, and a check before the payment opens. The API
// enforces the same limit from the Wallet.MinRecharge setting.
// Writes WalletPage-WR1.js (the old chunk stays), points the index at it and
// bumps sw.js. Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "wallet-min-1000-1";
const OLD = "WalletPage-BliFPSBM.js", NEW = "WalletPage-WR1.js";
const A = (f) => path.join(front, "assets", f);

function edit(t, file, edits) {
  for (const [name, from, to] of edits) {
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`${file}: '${name}' expected 1 match, found ${n}; nothing written`);
    t = t.replace(from, to);
    console.log(`OK  - ${file}: ${name}`);
  }
  return t;
}

let page = edit(fs.readFileSync(A(OLD), "utf8"), NEW, [
  ["default amount", "[i,p]=s.useState(500)", "[i,p]=s.useState(1e3)"],
  ["amount field",
    'min:100,max:5e4,step:100,value:i,hint:"Between ₹100 and ₹50,000 per recharge"',
    'min:1e3,max:5e4,step:100,value:i,hint:"Minimum ₹1,000, maximum ₹50,000 per recharge"'],
  ["quick amounts", "[500,1e3,2e3,5e3].map", "[1e3,2e3,5e3,1e4].map"],
  ["check before paying", "A=async()=>{g(!0);try{",
    'A=async()=>{if(!(i>=1e3&&i<=5e4)){d.error(i<1e3?"Minimum recharge is ₹1,000.":"Maximum recharge is ₹50,000.");return}g(!0);try{'],
]);
fs.writeFileSync(A(NEW), page, "utf8");

const idx = "index-DI5X4c9f.js";
let t = fs.readFileSync(A(idx), "utf8");
if (t.includes(OLD)) {
  const n = t.split(OLD).length - 1;
  if (n !== 2) throw new Error(`${idx}: expected 2 references to ${OLD}, found ${n}`);
  t = t.replaceAll(OLD, NEW);
  fs.writeFileSync(A(idx), t, "utf8");
  console.log(`OK  - ${idx}: import + mapDeps -> ${NEW}`);
} else if (t.includes(NEW)) console.log(`OK  - ${idx}: already points at ${NEW}`);
else throw new Error(`${idx}: no reference to ${OLD}`);

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
sw = sw.replace(/\{url:"assets\/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}/, `{url:"assets/index-DI5X4c9f.js",revision:"${REV}"}`);
if (!sw.includes(`assets/${NEW}`)) {
  sw = sw.replace(`{url:"assets/${OLD}",revision:null}`, `{url:"assets/${OLD}",revision:null},{url:"assets/${NEW}",revision:"${REV}"}`);
}
if (!sw.includes(`assets/${NEW}`) || !sw.includes(`index-DI5X4c9f.js",revision:"${REV}"`)) throw new Error("sw.js not updated");
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
