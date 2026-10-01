// Separate sign-in pages: /login (customers) and /admin/login (admins and staff).
// Patches the built SPA in <frontend dir> in place and bumps the sw.js precache revisions.
// Idempotent: a second run reports "already patched" and writes nothing.
//   node apply.mjs <frontend dir>
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const assets = path.join(front, "assets");
const REV = "split-login-1";
const read = (f) => fs.readFileSync(path.join(assets, f), "utf8");
const write = (f, t) => fs.writeFileSync(path.join(assets, f), t, "utf8");

// Where the shared password-recovery pages send people back to: the sign-in page they came from.
const BACK = '(()=>{try{return sessionStorage.getItem("regalis-login-portal")==="admin"?"/admin/login":"/login"}catch{return"/login"}})()';
const PORTAL_LOGIN = 'Mz(window.location.pathname)?"/admin/login":"/login"';

function patch(file, edits) {
  let text = read(file);
  let changed = false;
  for (const [name, from, to, count = 1] of edits) {
    const n = text.split(from).length - 1;
    if (n === count) {
      text = text.split(from).join(to);
      changed = true;
      console.log(`OK  - ${file}: ${name}`);
    } else if (n === 0 && text.includes(to)) {
      console.log(`OK  - ${file}: ${name} (already patched)`);
    } else {
      throw new Error(`${file}: '${name}' expected ${count} match(es), found ${n}; nothing written.`);
    }
  }
  if (changed) write(file, text);
}

// ── index: the login page, its routes, and the two redirects to sign-in ──────
{
  const file = "index-DI5X4c9f.js";
  const text = read(file);
  const done = text.includes('function Uz({portal:s="customer"}){');
  const start = done ? -1 : text.indexOf("function Uz(){");
  let depth = 0, end = start < 0 ? -1 : text.indexOf("{", start);
  for (let k = end; start >= 0 && k < text.length; k++) {
    if (text[k] === "{") depth++;
    else if (text[k] === "}" && --depth === 0) { end = k; break; }
  }
  const oldUz = start < 0 ? "" : text.slice(start, end + 1);
  let newUz = oldUz;
  const uzEdits = [
    // The portal is fixed by the route instead of a Customer/Admin switch.
    ['function Uz(){', 'function Uz({portal:s="customer"}){'],
    [',[s,c]=M.useState("customer")', ''],
    ['g.jsxs("div",{className:"auth__seg",role:"tablist","aria-label":"Portal",children:[g.jsx("button",{type:"button",role:"tab","aria-selected":s==="customer",className:`auth__seg-opt${s==="customer"?" auth__seg-opt--active":""}`,onClick:()=>c("customer"),children:"Customer"}),g.jsx("button",{type:"button",role:"tab","aria-selected":s==="admin",className:`auth__seg-opt${s==="admin"?" auth__seg-opt--active":""}`,onClick:()=>c("admin"),children:"Admin"})]}),', ''],
    ['children:"Welcome Back"})', 'children:s==="admin"?"Admin Sign In":"Welcome Back"})'],
    ['children:"Please enter your details to sign in"})', 'children:s==="admin"?"Sign in to the Regalis admin console":"Please enter your details to sign in"})'],
    // Remember which sign-in page this is, for the shared password-recovery pages.
    ['return g.jsxs("div",{className:"auth__panel"', 'M.useEffect(()=>{try{sessionStorage.setItem("regalis-login-portal",s)}catch{}},[s]);return g.jsxs("div",{className:"auth__panel"'],
  ];
  if (done) {
    console.log(`OK  - ${file}: login page (already patched)`);
  } else {
    for (const [from, to] of uzEdits) {
      if (!newUz.includes(from)) throw new Error(`${file}: login page code not found: ${from.slice(0, 60)}…; nothing written.`);
      newUz = newUz.replace(from, to);
    }
    write(file, text.replace(oldUz, newUz));
    console.log(`OK  - ${file}: login page takes its portal from the route (no Customer/Admin switch)`);
  }
  patch(file, [
    ["routes: /login customer, /admin/login admin",
      'g.jsx(P,{path:"/login",element:g.jsx(Uz,{})}),',
      'g.jsx(P,{path:"/login",element:g.jsx(Uz,{portal:"customer"},"customer")}),g.jsx(P,{path:"/admin/login",element:g.jsx(Uz,{portal:"admin"},"admin")}),'],
    ["signed-out guard sends /admin/* to the admin sign-in",
      'g.jsx(O0,{center:!0}):g.jsx(Os,{to:"/login",replace:!0})}',
      `g.jsx(O0,{center:!0}):g.jsx(Os,{to:${PORTAL_LOGIN},replace:!0})}`],
    ["logout returns to the sign-in page of its portal",
      'finally{h(),i("/login",{replace:!0})}',
      `finally{h(),i(${PORTAL_LOGIN},{replace:!0})}`],
  ]);
}

// ── shared password-recovery pages go back to the sign-in page they came from ─
patch("ForgotPasswordPage-COYpk7Vw.js", [["back link", 'to:"/login"', `to:${BACK}`]]);
patch("ResetPasswordPage-BB_P8AiB.js", [
  ["back link", 'to:"/login"', `to:${BACK}`],
  ["after reset", 'e("/login",{replace:!0})', `e(${BACK},{replace:!0})`],
]);
patch("VerifyOtpPage-B1bv1-9D.js", [
  ["no-state redirect and back link", 'to:"/login"', `to:${BACK}`, 2],
  ["after verifying", 'n("/login",{replace:!0})', `n(${BACK},{replace:!0})`],
]);

// ── sw.js: make browsers fetch the patched files ─────────────────────────────
{
  const sw = path.join(front, "sw.js");
  let text = fs.readFileSync(sw, "utf8");
  for (const f of ["index-DI5X4c9f.js", "ForgotPasswordPage-COYpk7Vw.js", "ResetPasswordPage-BB_P8AiB.js", "VerifyOtpPage-B1bv1-9D.js"]) {
    const re = new RegExp(`\\{url:"assets/${f.replace(/[.-]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
    if (!re.test(text)) throw new Error(`sw.js has no precache entry for ${f}`);
    text = text.replace(re, `{url:"assets/${f}",revision:"${REV}"}`);
  }
  fs.writeFileSync(sw, text, "utf8");
  console.log(`sw.js entries at ${REV}: ${text.split(`"${REV}"`).length - 1} (expected 4)`);
}
