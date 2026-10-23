// Company name and GSTIN optional for members (23 Oct 2026).
//   node apply.mjs <frontend dir>
//
// Profile, Register and the onboarding form ("Complete Your Profile") showed Company Name
// and GST Number as required. They now carry the "(Optional)" tag and accept a blank
// value; a GSTIN that is typed is still checked for its 15-character format. The API side
// (User.IsProfileComplete, ProfileService.MissingFields, the onboarding validator) ships
// in the same release. Patches the three chunks in place and bumps sw.js. Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "optional-company-gst-1";
const A = (f) => path.join(front, "assets", f);

function patch(file, edits) {
  let t = fs.readFileSync(A(file), "utf8");
  let changed = 0;
  for (const [what, from, to] of edits) {
    if (t.includes(to)) continue;
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`${file}: ${what}: expected 1 match, found ${n}; nothing written`);
    t = t.replace(from, () => to);
    changed++;
  }
  fs.writeFileSync(A(file), t, "utf8");
  console.log(`OK  - ${file}: ${changed ? `${changed} change(s)` : "already patched"}`);
}

patch("ProfilePage-DRLk3DkQ.js", [
  ["company rule",
    'companyName:t=>(t||"").trim()?(t||"").trim().length>200?"Keep this under 200 characters.":null:"Company name is required."',
    'companyName:t=>(t||"").trim().length>200?"Keep this under 200 characters.":null'],
  ["company label", 'label:"Company Name",icon:IB,', 'label:"Company Name",optional:!0,icon:IB,'],
  ["gst label", 'label:"GST Number",icon:IB,', 'label:"GST Number",optional:!0,icon:IB,'],
]);

patch("RegisterPage-ClfJATK5.js", [
  ["company rule",
    'companyName:t().min(1,"Company name is required.").max(200,"Keep this under 200 characters.")',
    'companyName:t().max(200,"Keep this under 200 characters.")'],
  ["gst rule",
    'gstin:t().min(1,"GST number is required.").refine(s=>/^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][A-Z0-9]Z[A-Z0-9]$/.test(s.trim().toUpperCase())',
    'gstin:t().refine(s=>!s.trim()||/^[0-9]{2}[A-Z]{5}[0-9]{4}[A-Z][A-Z0-9]Z[A-Z0-9]$/.test(s.trim().toUpperCase())'],
  ["company label", 'e.jsx(r,{label:"Company Name",', 'e.jsx(r,{label:"Company Name",optional:!0,'],
  ["gst label", 'e.jsx(r,{label:"GST Number",', 'e.jsx(r,{label:"GST Number",optional:!0,'],
]);

patch("OnboardingPage-RGo1nb2d.js", [
  ["company field",
    '["Company Name",   "companyName",  ICONS.building, { placeholder: "Registered business name" }]',
    '["Company Name",   "companyName",  ICONS.building, { placeholder: "Registered business name", optional: true }]'],
  ["gst field",
    '["GST Number",     "gstin",        ICONS.receipt,  { placeholder: "15-character GSTIN", maxLength: 15, style: { textTransform: "uppercase" } }]',
    '["GST Number",     "gstin",        ICONS.receipt,  { placeholder: "15-character GSTIN", maxLength: 15, style: { textTransform: "uppercase" }, optional: true }]'],
  ["subtitle",
    '"issued against them. All fields are required.",',
    '"issued against them. Fields marked * are required; company name and GST number are optional.",'],
]);

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const f of ["ProfilePage-DRLk3DkQ.js", "RegisterPage-ClfJATK5.js", "OnboardingPage-RGo1nb2d.js"]) {
  const re = new RegExp(`\\{url:"assets/${f.replace(/[.-]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  if (re.test(sw)) sw = sw.replace(re, `{url:"assets/${f}",revision:"${REV}"}`);
  else console.log(`--  - sw.js has no entry for ${f} (served from the network, nothing to bump)`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
