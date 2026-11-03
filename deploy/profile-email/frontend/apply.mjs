// Own sign-in email on the admin portal profile (2 Nov 2026) - the SPA side.
//   node apply.mjs <frontend dir>
//
// ProfilePage (shared by both portals): for staff and admins the Edit Profile form gets an
// Email Address field; once it differs from the current email a Current Password field
// appears, and Save calls PUT /api/profile/email before the usual PUT /api/profile.
// Customers see no change (their email stays read-only).
//
// Patches the chunk in place (keeps its name), bumps its sw.js revision and makes sure
// web.config does not cache it. Idempotent: a file that has the marker is skipped.
import fs from "fs";
import path from "path";

const [front] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "profile-email-1";
const MARK = "PROFILE_EMAIL";
const CHUNK = "assets/ProfilePage-DRLk3DkQ.js";

function edit(file, edits) {
  const raw = fs.readFileSync(file, "utf8");
  const crlf = raw.includes("\r\n");
  let t = crlf ? raw.replace(/\r\n/g, "\n") : raw;
  const name = path.basename(file);
  if (t.includes(MARK)) { console.log(`OK  - ${name}: already patched`); return; }
  for (const [what, fn] of edits) { t = fn(t, `${name}: ${what}`); console.log(`OK  - ${name}: ${what}`); }
  fs.writeFileSync(file, crlf ? t.replace(/\n/g, "\r\n") : t, "utf8");
}
const once = (t, from, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.indexOf(from);
};
const swap = (from, to) => (t, where) => { once(t, from, where); return t.replace(from, to); };
const after = (anchor, insert) => (t, where) => { const at = once(t, anchor, where) + anchor.length; return t.slice(0, at) + insert + t.slice(at); };

// index exports the API client as E and the envelope unwrapper as D.
const STAFF = `n.userType!==1`;
const CHANGED = `${STAFF}&&EM.trim()!==n.email`;
edit(path.join(front, CHUNK), [
  ["imports", swap(`p as BT}from"./index-DI5X4c9f.js"`, `p as BT,D as EMu,E as EMx}from"./index-DI5X4c9f.js"`)],
  ["state", after(`[PB,pb]=r.useState(!1)`, `,[EM,em]=r.useState(""),[EP,ep]=r.useState(""),[EE,ee]=r.useState({})`)],
  ["open edit", swap(`q=()=>{i(R(n)),`, `q=()=>{em(n.email||""),ep(""),ee({}),i(R(n)),`)],
  ["save", swap(
    `return}c(!0);try{g(await f.update({`,
    `return}/*${MARK}*/const EMc=${CHANGED};if(EMc){const x={};` +
    `/^[^\\s@]+@[^\\s@]+\\.[^\\s@]+$/.test(EM.trim())||(x.em="Enter a valid email address."),` +
    `EP||(x.ep="Enter your current password to change your email."),ee(x);` +
    `if(Object.keys(x).length){l.error("Check the highlighted fields.");return}}else ee({});` +
    `c(!0);try{if(EMc){g(await EMu(EMx.put("/profile/email",{newEmail:EM.trim(),currentPassword:EP}))),ep("")}g(await f.update({`)],
  ["fields", after(
    `onChange:a=>y("fullName",a.target.value)}),`,
    `${STAFF}&&e.jsx(F0,{label:"Email Address",icon:IM,type:"email",placeholder:"name@example.com",autoComplete:"email",value:EM,error:EE.em,onChange:a=>{em(a.target.value),ee(z=>({...z,em:void 0}))}}),` +
    `${CHANGED}&&e.jsx(F0,{label:"Current Password",icon:IL,type:"password",placeholder:"Needed to change your email",autoComplete:"current-password",value:EP,error:EE.ep,onChange:a=>{ep(a.target.value),ee(z=>({...z,ep:void 0}))}}),`)],
]);

// --- sw.js: re-fetch the chunk that changed in place ----------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const entry = new RegExp(`\\{url:"${CHUNK.replace(/[.\-/]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
if (entry.test(sw)) {
  sw = sw.replace(entry, `{url:"${CHUNK}",revision:"${REV}"}`);
  fs.writeFileSync(swf, sw, "utf8");
  console.log(`OK  - sw.js revision ${REV}`);
} else console.log(`OK  - sw.js: ${CHUNK} not precached`);

// --- web.config: the chunk changes in place, so it must revalidate ------------------
const cfgFile = path.join(front, "web.config");
let cfg = fs.readFileSync(cfgFile, "utf8");
if (!cfg.includes(`<location path="${CHUNK}">`)) {
  cfg = cfg.replace("</configuration>",
    `  <location path="${CHUNK}">\n    <system.webServer>\n      <staticContent>\n        <clientCache cacheControlMode="DisableCache" />\n      </staticContent>\n    </system.webServer>\n  </location>\n</configuration>`);
  fs.writeFileSync(cfgFile, cfg, "utf8");
  console.log(`OK  - web.config: no-cache ${CHUNK}`);
}
console.log("DONE");
