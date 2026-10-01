// Monthly desk/cabin booking - frontend install.
//   node apply.mjs <frontend dir>
// 1. installs BookWorkspacePage-MB1.js (staged next to this script)
// 2. builds WorkspacesPage-MB1.js from the live WorkspacesPage-BEbdp5Ro.js with a
//    "Price / month" field (without it every admin save wiped the monthly price)
// 3. points index-DI5X4c9f.js at both new files (lazy import AND __vite__mapDeps entry)
// 4. bumps the index's sw.js precache revision so browsers fetch the new index
// Old files are left in place so a stale cached index never 404s. Idempotent.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const here = path.dirname(fileURLToPath(import.meta.url));
const assets = path.join(front, "assets");
const REV = "monthly-booking-1";
const at = (f) => path.join(assets, f);

// 1. The booking page.
fs.copyFileSync(path.join(here, "frontend", "assets", "BookWorkspacePage-MB1.js"), at("BookWorkspacePage-MB1.js"));
console.log("OK  - BookWorkspacePage-MB1.js installed");

// 2. The admin Workspaces page, with the monthly price.
{
  let t = fs.readFileSync(at("WorkspacesPage-BEbdp5Ro.js"), "utf8");
  const edits = [
    ['pricePerDay:o?.pricePerDay??"",', 'pricePerDay:o?.pricePerDay??"",pricePerMonth:o?.pricePerMonth??"",'],
    ['pricePerDay:t.pricePerDay===""?null:Number(t.pricePerDay),',
      'pricePerDay:t.pricePerDay===""?null:Number(t.pricePerDay),pricePerMonth:t.pricePerMonth===""?null:Number(t.pricePerMonth),'],
    ['hint:"Optional — used by day passes",onChange:s=>i({...t,pricePerDay:s.target.value===""?"":Number(s.target.value)})}),',
      'hint:"Optional — used by day passes",onChange:s=>i({...t,pricePerDay:s.target.value===""?"":Number(s.target.value)})}),' +
      'a.jsx(l,{label:"Price / month (₹)",type:"number",min:0,step:"0.01",value:t.pricePerMonth,' +
      'hint:"Desks and cabins: customers book by the month at this rate. Leave empty to keep it off monthly booking.",' +
      'onChange:s=>i({...t,pricePerMonth:s.target.value===""?"":Number(s.target.value)})}),'],
  ];
  for (const [from, to] of edits) {
    if (t.split(from).length !== 2) throw new Error(`WorkspacesPage: anchor not found once: ${from.slice(0, 50)}`);
    t = t.replace(from, to);
  }
  fs.writeFileSync(at("WorkspacesPage-MB1.js"), t, "utf8");
  console.log("OK  - WorkspacesPage-MB1.js built (Price / month field)");
}

// 3. Repoint the index.
{
  const f = at("index-DI5X4c9f.js");
  let t = fs.readFileSync(f, "utf8");
  let changed = false;
  for (const [oldName, newName] of [
    ["BookWorkspacePage-Nx5vB8tK.js", "BookWorkspacePage-MB1.js"],
    ["WorkspacesPage-BEbdp5Ro.js", "WorkspacesPage-MB1.js"],
  ]) {
    for (const [from, to] of [[`"./${oldName}"`, `"./${newName}"`], [`"assets/${oldName}"`, `"assets/${newName}"`]]) {
      const n = t.split(from).length - 1;
      if (n === 1) { t = t.replace(from, to); changed = true; }
      else if (n === 0 && t.includes(to)) { /* already patched */ }
      else throw new Error(`index: expected one ${from}, found ${n}; nothing written`);
    }
    console.log(`OK  - index points at ${newName}`);
  }
  if (changed) fs.writeFileSync(f, t, "utf8");
}

// 4. sw.js
{
  const sw = path.join(front, "sw.js");
  let t = fs.readFileSync(sw, "utf8");
  const re = /\{url:"assets\/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}/;
  if (!re.test(t)) throw new Error("sw.js has no precache entry for the index");
  t = t.replace(re, `{url:"assets/index-DI5X4c9f.js",revision:"${REV}"}`);
  fs.writeFileSync(sw, t, "utf8");
  console.log(`OK  - sw.js index revision ${REV}`);
}
