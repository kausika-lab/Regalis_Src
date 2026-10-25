// Reports & Analysis (Regalis_WorkSpace_Reports_Analysis_Feature_Specification, Oct 2026) —
// installs the staged frontend.
//   node apply.mjs <frontend dir>
// Copies ReportsAnalysis-RA1.js into assets/, adds its four lazy loaders and the
// /admin/reports/* routes to index-DI5X4c9f.js (overview, charts/:section, library,
// download/:token, :key), makes /admin/reports open the overview, draws the sidebar entry
// as a group so its five sub-items show and highlight, and bumps sw.js. The old
// AdminReportsPages chunk stays on disk (sw.js precaches it); nothing imports it any more.
// Idempotent. Run only after the new API is live: the pages call /api/admin/reports/*.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const here = path.dirname(fileURLToPath(import.meta.url));
const REV = "reports-1";
const CHUNK = "ReportsAnalysis-RA1.js";
const A = (f) => path.join(front, "assets", f);

fs.copyFileSync(path.join(here, "frontend", "assets", CHUNK), A(CHUNK));
console.log(`OK  - copied ${CHUNK}`);

const idx = "index-DI5X4c9f.js";
let t = fs.readFileSync(A(idx), "utf8");
const once = (label, find, replace) => {
  if (t.includes(replace)) { console.log(`OK  - ${idx}: ${label} already in place`); return; }
  const n = t.split(find).length - 1;
  if (n !== 1) throw new Error(`${idx}: ${label}: expected 1 match, found ${n}`);
  t = t.replace(find, replace);
  console.log(`OK  - ${idx}: ${label}`);
};

// 1. Lazy loaders, next to the old report pages'.
const LOADERS =
  `RGraOverview=fe(()=>ce(()=>import("./${CHUNK}")),"ReportsOverviewPage"),` +
  `RGraReport=fe(()=>ce(()=>import("./${CHUNK}")),"ReportPage"),` +
  `RGraLibrary=fe(()=>ce(()=>import("./${CHUNK}")),"ReportLibraryPage"),` +
  `RGraDownload=fe(()=>ce(()=>import("./${CHUNK}")),"ReportDownloadPage"),`;
const anchor = '"ReportShellPage"),';
once("report page loaders", anchor, anchor + LOADERS);

// 2. Routes. Static paths outrank /:key in React Router, so "library" is never a report key.
once("report routes",
  'g.jsx(P,{path:"/admin/reports",element:g.jsx(B3,{})}),g.jsx(P,{path:"/admin/reports/:key",element:g.jsx(Z3,{})})',
  'g.jsx(P,{path:"/admin/reports",element:g.jsx(Os,{to:"/admin/reports/overview",replace:!0})}),' +
  'g.jsx(P,{path:"/admin/reports/overview",element:g.jsx(RGraOverview,{})}),' +
  'g.jsx(P,{path:"/admin/reports/charts/:section",element:g.jsx(RGraOverview,{})}),' +
  'g.jsx(P,{path:"/admin/reports/library",element:g.jsx(RGraLibrary,{})}),' +
  'g.jsx(P,{path:"/admin/reports/download/:token",element:g.jsx(RGraDownload,{})}),' +
  'g.jsx(P,{path:"/admin/reports/:key",element:g.jsx(RGraReport,{})})');

// 3. The sidebar draws Reports & Analysis as a group with its five children.
once("sidebar group", 'RGgrouped=new Set(["/admin/billing",', 'RGgrouped=new Set(["/admin/reports","/admin/billing",');

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
