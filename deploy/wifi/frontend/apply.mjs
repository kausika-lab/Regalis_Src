// wifi kit (19 Nov 2026): Wi-Fi Access, frontend part.
//   node apply.mjs <frontend dir>
// Copies assets/WifiPages-WF1.js and patches existing chunks with string edits that must each match
// exactly once, so it also works on copies carrying other kits' patches (it expects the crm kit's
// patches, which are in the repo frontend). Idempotent: an edit already present is skipped.
//
// Existing behaviour is untouched; every edit only ADDS:
//   index-DI5X4c9f.js   admin routes /admin/wifi and /admin/wifi/:tab (WIFI.VIEW), customer route /wifi
//                       (and /portal/wifi -> /wifi), and the "Wifi" sidebar icon used by both menu rows
//   sw.js               revisions for the changed and new files
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "wifi-1";
const here = path.dirname(fileURLToPath(import.meta.url));
const A = (f) => path.join(front, "assets", f);
const changed = new Set();

function patchFile(p, label, edits) {
  let t = fs.readFileSync(p, "utf8");
  const eol = (s) => (t.includes("\r\n") ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo] of edits) {
    const from = eol(rawFrom);
    const to = eol(rawTo);
    const n = t.split(from).length - 1;
    if (t.includes(to)) console.log(`OK  - ${label}: ${name} (already done)`);
    else if (n === 1) {
      t = t.replace(from, () => to);
      console.log(`OK  - ${label}: ${name}`);
      changed.add(label);
    } else throw new Error(`${label}: '${name}' expected 1 match, found ${n}; nothing written for this file`);
  }
  fs.writeFileSync(p, t, "utf8");
}
const patch = (file, edits) => patchFile(A(file), file, edits);

// ---- new file -------------------------------------------------------------------------------
{
  const src = path.join(here, "assets", "WifiPages-WF1.js");
  const dst = A("WifiPages-WF1.js");
  const same = fs.existsSync(dst) && fs.readFileSync(dst, "utf8") === fs.readFileSync(src, "utf8");
  if (!same) {
    fs.copyFileSync(src, dst);
    changed.add("WifiPages-WF1.js");
  }
  console.log(`OK  - WifiPages-WF1.js ${same ? "(already there)" : "copied"}`);
}

// ---- index: lazy chunk, routes, icon ----------------------------------------------------------
const LAZY =
  'RGwifiAdmin=fe(()=>ce(()=>import("./WifiPages-WF1.js")),"WifiAdminPage"),RGwifiMy=fe(()=>ce(()=>import("./WifiPages-WF1.js")),"WifiMyPage"),';
const ADMIN_ROUTES =
  'g.jsxs(P,{element:g.jsx(Ne,{permission:"WIFI.VIEW"}),children:[g.jsx(P,{path:"/admin/wifi",element:g.jsx(RGwifiAdmin,{})}),' +
  'g.jsx(P,{path:"/admin/wifi/:tab",element:g.jsx(RGwifiAdmin,{})})]}),';
const CUSTOMER_ROUTES = 'g.jsx(P,{path:"/wifi",element:g.jsx(RGwifiMy,{})}),g.jsx(P,{path:"/portal/wifi",element:g.jsx(Os,{to:"/wifi",replace:!0})}),';
const ICON =
  'const RG_wifi=[["path",{d:"M12 20h.01",key:"wf1"}],["path",{d:"M2 8.82a15 15 0 0 1 20 0",key:"wf2"}],["path",{d:"M5 12.859a10 10 0 0 1 14 0",key:"wf3"}],["path",{d:"M8.5 16.429a5 5 0 0 1 7 0",key:"wf4"}]],RGwifi=Oe("wifi",RG_wifi);';
const ACCESS_LOGS = 'g.jsx(P,{element:g.jsx(Ne,{permission:"ACCESS.VIEW"}),children:g.jsx(P,{path:"/admin/access/logs",element:g.jsx(m3,{})})}),';
const PAYMENTS = 'g.jsx(P,{element:g.jsx(Ne,{permission:"PAYMENT.VIEW"}),children:g.jsx(P,{path:"/payments",element:g.jsx(RGpayhist,{})})}),';
patch("index-DI5X4c9f.js", [
  ["Wi-Fi chunk", 'RGcrmEnquire=fe(()=>ce(()=>import("./CrmEnquirePage-CRM1.js")),"CrmEnquirePage"),', 'RGcrmEnquire=fe(()=>ce(()=>import("./CrmEnquirePage-CRM1.js")),"CrmEnquirePage"),' + LAZY],
  ["admin routes /admin/wifi", ACCESS_LOGS, ACCESS_LOGS + ADMIN_ROUTES],
  ["customer route /wifi", PAYMENTS, PAYMENTS + CUSTOMER_ROUTES],
  ["Wifi icon", 'RGglobe=Oe("globe",RG_globe);', 'RGglobe=Oe("globe",RG_globe);' + ICON],
  ["Wifi icon name", "Globe:RGglobe}", "Globe:RGglobe,Wifi:RGwifi}"]
]);

// ---- sw.js -------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
// Both kit files always carry the kit revision, so a re-run after sw.js was restored still lists them.
for (const f of new Set([...changed, "WifiPages-WF1.js", "index-DI5X4c9f.js"])) {
  const url = `assets/${f}`;
  const re = new RegExp(`\\{url:"${url.replace(/[.\-]/g, (c) => "\\" + c)}",revision:(null|"[^"]*")\\}`);
  if (re.test(sw)) sw = sw.replace(re, `{url:"${url}",revision:"${REV}"}`);
  else {
    const anchor = '{url:"index.html"';
    if (!sw.includes(anchor)) throw new Error("sw.js: no index.html entry to insert before");
    sw = sw.replace(anchor, `{url:"${url}",revision:"${REV}"},${anchor}`);
  }
}
sw = sw.replace(/\{url:"index\.html",revision:(null|"[^"]*")\}/, `{url:"index.html",revision:"${REV}"}`);
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV} (${changed.size} files)`);
