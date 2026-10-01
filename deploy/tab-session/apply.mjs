// Sign-in lasts only as long as the tab (02 Oct 2026).
//   node apply.mjs <frontend dir>
// The auth store ("regalis-auth": tokens, user, permissions) moves from localStorage to
// sessionStorage: a refresh (F5 / Ctrl+F5) keeps the user signed in, closing the tab or
// the browser ends it, and a new tab starts at the login page. The copy older builds
// left in localStorage is removed on load, so nobody is let in from it. Bumps sw.js.
// Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "tab-session-1";
const idx = path.join(front, "assets", "index-DI5X4c9f.js");

let t = fs.readFileSync(idx, "utf8");
function edit(name, from, to) {
  const n = t.split(from).length - 1;
  if (n === 1) { t = t.replace(from, to); console.log(`OK  - index: ${name}`); }
  else if (n === 0 && t.includes(to)) console.log(`OK  - index: ${name} (already done)`);
  else throw new Error(`index: '${name}' expected 1 match, found ${n}; nothing written`);
}
edit("auth store in sessionStorage",
  '{name:"regalis-auth"}',
  '{name:"regalis-auth",storage:tA(()=>window.sessionStorage)}');
edit("drop the old localStorage copy",
  "Jf=nA,Fn=Ff()(",
  'Jf=nA,RGx=(()=>{try{window.localStorage.removeItem("regalis-auth")}catch{}})(),Fn=Ff()(');
fs.writeFileSync(idx, t, "utf8");

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const url of ["index.html", "assets/index-DI5X4c9f.js"]) {
  const re = new RegExp(`\\{url:"${url.replace(/[.]/g, "\\.")}",revision:(null|"[^"]*")\\}`);
  if (!re.test(sw)) throw new Error(`sw.js has no entry for ${url}`);
  sw = sw.replace(re, `{url:"${url}",revision:"${REV}"}`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
