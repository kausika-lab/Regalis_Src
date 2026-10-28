// Register page: "Back to Sign In" link, as on Forgot Password and Verify OTP (28 Oct 2026).
//   node apply.mjs <frontend dir>
// Patches RegisterPage-ClfJATK5.js in place and bumps its sw.js revision. Idempotent.
import fs from "fs";
import path from "path";
const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "register-back-1";
const file = path.join(front, "assets", "RegisterPage-ClfJATK5.js");
let t = fs.readFileSync(file, "utf8");
if (t.includes("RGarrowBack")) console.log("OK  - RegisterPage-ClfJATK5.js: already patched");
else {
  const edits = [
    ["icon import", 'from"./index-DI5X4c9f.js";', 'from"./index-DI5X4c9f.js";import{A as RGarrowBack}from"./arrow-left-CAirZ0TE.js";'],
    ["link", 'e.jsxs("div",{className:"auth__panel auth__panel--wide",children:[e.jsx(w,{}),e.jsx("h1",{className:"auth__title",children:"Create Account"})',
      'e.jsxs("div",{className:"auth__panel auth__panel--wide",children:[e.jsxs(_,{to:"/login",className:"auth__back",children:[e.jsx(RGarrowBack,{size:19})," Back to Sign In"]}),e.jsx(w,{}),e.jsx("h1",{className:"auth__title",children:"Create Account"})'],
  ];
  for (const [what, from, to] of edits) {
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`RegisterPage: ${what}: expected 1 match, found ${n}; nothing written`);
    t = t.replace(from, () => to);
  }
  fs.writeFileSync(file, t, "utf8");
  console.log("OK  - RegisterPage-ClfJATK5.js: Back to Sign In link");
}
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const re = /\{url:"assets\/RegisterPage-ClfJATK5\.js",revision:(null|"[^"]*")\}/;
if (!re.test(sw)) throw new Error("sw.js: no entry for RegisterPage-ClfJATK5.js");
fs.writeFileSync(swf, sw.replace(re, `{url:"assets/RegisterPage-ClfJATK5.js",revision:"${REV}"}`), "utf8");
console.log(`OK  - sw.js: RegisterPage-ClfJATK5.js revision ${REV}`);
