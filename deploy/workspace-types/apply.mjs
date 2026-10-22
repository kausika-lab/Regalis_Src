// Workspace types as data (14 Oct 2026) — installs the staged frontend.
//   node apply.mjs <frontend dir>
// Copies the three staged chunks into assets/, points index-DI5X4c9f.js at them (import +
// __vite__mapDeps entry each), and bumps sw.js. The old chunks stay on disk so a browser
// still holding the old index does not 404. Idempotent. Run only after the new API is live:
// WorkspacesPage-WT1 calls /api/admin/workspace-types, which the old API does not have.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const here = path.dirname(fileURLToPath(import.meta.url));
const REV = "workspace-types-1";
const SWAPS = [
  ["WorkspacesPage-MB1.js", "WorkspacesPage-WT1.js"],
  ["BookWorkspacePage-MB2.js", "BookWorkspacePage-MB3.js"],
  ["PassesPage-CxAkHUg-.js", "PassesPage-WT1.js"],
];
const A = (f) => path.join(front, "assets", f);

for (const [, NEW] of SWAPS) {
  fs.copyFileSync(path.join(here, "frontend", "assets", NEW), A(NEW));
  console.log(`OK  - copied ${NEW}`);
}

const idx = "index-DI5X4c9f.js";
let t = fs.readFileSync(A(idx), "utf8");
for (const [OLD, NEW] of SWAPS) {
  if (t.includes(OLD)) {
    const n = t.split(OLD).length - 1;
    if (n !== 2) throw new Error(`${idx}: expected 2 references to ${OLD}, found ${n}`);
    t = t.replaceAll(OLD, NEW);
    console.log(`OK  - ${idx}: import + mapDeps -> ${NEW}`);
  } else if (t.includes(NEW)) console.log(`OK  - ${idx}: already points at ${NEW}`);
  else throw new Error(`${idx}: no reference to ${OLD}`);
}
fs.writeFileSync(A(idx), t, "utf8");

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
sw = sw.replace(/\{url:"assets\/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}/, `{url:"assets/index-DI5X4c9f.js",revision:"${REV}"}`);
for (const [, NEW] of SWAPS) {
  const entry = new RegExp(`\\{url:"assets/${NEW.replace(/[.-]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  sw = entry.test(sw)
    ? sw.replace(entry, `{url:"assets/${NEW}",revision:"${REV}"}`)
    : sw.replace(/(\{url:"assets\/index-DI5X4c9f\.js",revision:"[^"]*"\})/, `$1,{url:"assets/${NEW}",revision:"${REV}"}`);
}
for (const [, NEW] of SWAPS) if (!sw.includes(`{url:"assets/${NEW}",revision:"${REV}"}`)) throw new Error(`sw.js: ${NEW} not added`);
if (!sw.includes(`index-DI5X4c9f.js",revision:"${REV}"`)) throw new Error("sw.js: index revision not bumped");
// Every precached URL must exist, or the service worker install fails for everyone.
for (const [, url] of sw.matchAll(/\{url:"([^"]+)"/g)) {
  if (!fs.existsSync(path.join(front, url))) throw new Error(`sw.js lists ${url}, which is not on disk`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
