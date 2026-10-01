// New Regalis logo everywhere in the SPA (01 Oct 2026).
//   node apply.mjs <frontend dir>
// - copies the new PNGs (staged next to this script) into brand/ and icons/
// - sidebar: the gold "R" tile (/icons/icon.svg) -> the crown
// - sign-in pages: crown + "REGALIS" text -> the full logo (crown, Regalis WORKSPACE,
//   Premium Office Solutions)
// - favicon (index.html) and app icons (manifest.webmanifest) -> the crown
// - bumps the sw.js revisions of index.html, the manifest and the index chunk
// New file names (…-v2.png) so no browser or proxy can keep serving the old image.
// Old files stay on disk. Idempotent.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const here = path.dirname(fileURLToPath(import.meta.url));
const REV = "new-logo-1";

for (const rel of ["brand/regalis-logo-v2.png", "brand/regalis-crown-v2.png", "icons/icon-v2.png",
  "icons/icon-192-v2.png", "icons/icon-512-v2.png"]) {
  fs.copyFileSync(path.join(here, "files", rel), path.join(front, rel));
}
console.log("OK  - logo files copied");

function patch(file, edits) {
  const f = path.join(front, file);
  let t = fs.readFileSync(f, "utf8"), changed = false;
  for (const [name, from, to] of edits) {
    const n = t.split(from).length - 1;
    if (n === 1) { t = t.replace(from, to); changed = true; console.log(`OK  - ${file}: ${name}`); }
    else if (n === 0 && t.includes(to)) console.log(`OK  - ${file}: ${name} (already done)`);
    else throw new Error(`${file}: '${name}' expected 1 match, found ${n}; nothing written`);
  }
  if (changed) fs.writeFileSync(f, t, "utf8");
}

patch("assets/index-DI5X4c9f.js", [
  ["sidebar mark",
    'g.jsx("img",{src:"/icons/icon.svg",alt:""})',
    'g.jsx("img",{src:"/brand/regalis-crown-v2.png",alt:"Regalis",style:{width:"40px",height:"32px",objectFit:"contain"}})'],
  ["sign-in logo",
    'g.jsx("img",{className:"auth__crown",src:"/brand/regalis-crown.png",alt:""}),g.jsx("span",{className:"auth__wordmark",children:"REGALIS"})',
    'g.jsx("img",{src:"/brand/regalis-logo-v2.png",alt:"Regalis Workspace - Premium Office Solutions",style:{height:"132px",width:"auto",objectFit:"contain"}})'],
]);
patch("index.html", [
  ["favicon",
    '<link rel="icon" type="image/svg+xml" href="/icons/icon.svg" />',
    '<link rel="icon" type="image/png" href="/icons/icon-v2.png" />\n    <link rel="apple-touch-icon" href="/icons/icon-192-v2.png" />'],
]);
{
  const f = path.join(front, "manifest.webmanifest");
  let t = fs.readFileSync(f, "utf8");
  const before = t;
  t = t.replaceAll('"icons/icon-192.png"', '"icons/icon-192-v2.png"').replaceAll('"icons/icon-512.png"', '"icons/icon-512-v2.png"');
  if (t !== before) fs.writeFileSync(f, t, "utf8");
  console.log("OK  - manifest.webmanifest: app icons");
}
{
  const f = path.join(front, "sw.js");
  let t = fs.readFileSync(f, "utf8");
  for (const url of ["index.html", "manifest.webmanifest", "assets/index-DI5X4c9f.js"]) {
    const re = new RegExp(`\\{url:"${url.replace(/[.]/g, "\\.")}",revision:(null|"[^"]*")\\}`, "g");
    if (!re.test(t)) throw new Error(`sw.js has no entry for ${url}`);
    t = t.replace(re, `{url:"${url}",revision:"${REV}"}`);
  }
  fs.writeFileSync(f, t, "utf8");
  console.log(`OK  - sw.js revisions ${REV}`);
}
