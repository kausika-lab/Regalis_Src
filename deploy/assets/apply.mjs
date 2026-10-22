// Assets module (Regalis_WorkSpace_Assets_Feature_Specification, Oct 2026) — installs the
// staged frontend.
//   node apply.mjs <frontend dir>
// Copies the five new chunks into assets/, adds their lazy loaders and the four
// /admin/assets/* routes to index-DI5X4c9f.js, turns the old /admin/inventory/{items,
// incoming,outgoing,stock} routes into redirects, groups the Assets sidebar entry under
// its new route, and bumps sw.js. New chunk names, so no browser can hold a stale copy;
// the old inventory chunks stay on disk (the hidden Transactions page still uses them).
// Idempotent. Run only after the new API is live: the pages call /api/admin/items etc.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const here = path.dirname(fileURLToPath(import.meta.url));
const REV = "assets-1";
const CHUNKS = ["AssetQr-AS1.js", "AssetsCommon-AS1.js", "AssetItemsPage-AS1.js", "AssetGoodsPages-AS1.js", "AssetStockPage-AS1.js"];
const A = (f) => path.join(front, "assets", f);

for (const chunk of CHUNKS) {
  fs.copyFileSync(path.join(here, "frontend", "assets", chunk), A(chunk));
  console.log(`OK  - copied ${chunk}`);
}

const idx = "index-DI5X4c9f.js";
let t = fs.readFileSync(A(idx), "utf8");
const once = (label, find, replace) => {
  if (t.includes(replace)) { console.log(`OK  - ${idx}: ${label} already in place`); return; }
  const n = t.split(find).length - 1;
  if (n !== 1) throw new Error(`${idx}: ${label}: expected 1 match, found ${n}`);
  t = t.replace(find, replace);
  console.log(`OK  - ${idx}: ${label}`);
};

// 1. Lazy loaders for the new pages, next to the old inventory ones.
const LOADERS =
  'RGasItems=fe(()=>ce(()=>import("./AssetItemsPage-AS1.js")),"AssetItemsPage"),' +
  'RGasIn=fe(()=>ce(()=>import("./AssetGoodsPages-AS1.js")),"AssetIncomingPage"),' +
  'RGasOut=fe(()=>ce(()=>import("./AssetGoodsPages-AS1.js")),"AssetOutgoingPage"),' +
  'RGasStock=fe(()=>ce(()=>import("./AssetStockPage-AS1.js")),"AssetStockPage"),';
const anchor = '"AdminCurrentStockPage"),';
once("asset page loaders", anchor, anchor + LOADERS);

// 2. Routes: the new pages, and the old addresses redirect to them (bookmarks keep working).
const routes = [
  ["items", "v3", "RGasItems"],
  ["incoming", "b3", "RGasIn"],
  ["outgoing", "E3", "RGasOut"],
  ["stock", "S3", "RGasStock"]
];
for (const [name, oldComponent, newComponent] of routes) {
  once(`/admin/assets/${name} route`,
    `g.jsx(P,{path:"/admin/inventory/${name}",element:g.jsx(${oldComponent},{})})`,
    `g.jsx(P,{path:"/admin/assets/${name}",element:g.jsx(${newComponent},{})}),` +
    `g.jsx(P,{path:"/admin/inventory/${name}",element:g.jsx(Os,{to:"/admin/assets/${name}",replace:!0})})`);
}

// 3. The sidebar draws Assets as a group: its parent menu row now routes to /admin/assets/items.
once("sidebar group", 'RGgrouped=new Set(["/admin/billing","/admin/inventory/items",',
  'RGgrouped=new Set(["/admin/billing","/admin/inventory/items","/admin/assets/items",');

fs.writeFileSync(A(idx), t, "utf8");

// 4. sw.js: the index changed in place, so its precache revision must change.
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
sw = sw.replace(/\{url:"assets\/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}/, `{url:"assets/index-DI5X4c9f.js",revision:"${REV}"}`);
if (!sw.includes(`index-DI5X4c9f.js",revision:"${REV}"`)) throw new Error("sw.js: index revision not bumped");
// Every precached URL must exist, or the service worker install fails for everyone.
for (const [, url] of sw.matchAll(/\{url:"([^"]+)"/g)) {
  if (!fs.existsSync(path.join(front, url))) throw new Error(`sw.js lists ${url}, which is not on disk`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js index revision ${REV}`);
