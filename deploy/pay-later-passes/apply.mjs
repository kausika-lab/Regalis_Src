// Pay later for day passes and memberships (28 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// Needs the API from Regalis-live feature/pay-later-passes (GET /api/passes/pay-later,
// paymentMethod "paylater" on POST /api/passes/purchase, pass rows in /api/cash/outstanding,
// customerPassId on POST /api/cash/collections) and migration 20261028064444_PayLaterPasses.
//
//   - Book Pass: a "Pay later at the front desk" button beside Wallet / Pay online when the
//     member may pay later for a day pass; buying with it reserves the pass and opens
//     My Passes. My Passes shows "₹… due at the front desk" on an unpaid pay-later pass.
//   - Membership: the same button for memberships.
//   - Cash Collections: pay-later passes are listed beside bookings and take payments the
//     same way. That chunk is not on the no-cache list, so it ships as a new file
//     (AdminCashPages-PL2.js) and the index points at it.
// Idempotent. With a second argument it also patches the staged workspace-types
// PassesPage-WT1.js.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "pay-later-passes-2";
const A = (f) => path.join(front, "assets", f);

function edit(file, mark, edits, out = file) {
  let t = fs.readFileSync(file, "utf8").replace(/\r\n/g, "\n");
  const name = path.basename(out);
  if (t.includes(mark)) { console.log(`OK  - ${name}: already patched`); if (out !== file && !fs.existsSync(out)) fs.writeFileSync(out, t, "utf8"); return; }
  for (const [what, from, to] of edits) {
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`${name}: ${what}: expected 1 match, found ${n}; nothing written`);
    t = t.replace(from, () => to);
  }
  fs.writeFileSync(out, t, "utf8");
  console.log(`OK  - ${name}: pay later for passes`);
}

const PL_NOTE = "Pay later reserves it now. Pay at the front desk in cash, UPI or card before you check in — the door opens once it is paid. Unpaid by the end of the first day, it is released.";

// ── Book Pass + My Passes ────────────────────────────────────────────────────
function passesPage(file) {
  edit(file, "/passes/pay-later", [
    ["state",
      "const a=v(),l=U0(),[d,n]=r.useState(t),",
      "const a=v(),l=U0(),[d,n]=r.useState(t),[RGpl,RGsetPl]=r.useState(null),"],
    ["status effect",
      "const RGcur=RGopts?.current??null,",
      "r.useEffect(()=>{if(d!==1){RGsetPl(null);return}let dead=!1;RGunwrap(RGhttp.get(\"/passes/pay-later\")).then(M=>{dead||RGsetPl(M??null)}).catch(()=>{dead||RGsetPl(null)});return()=>{dead=!0}},[d,C]);const RGcur=RGopts?.current??null,"],
    ["purchase",
      "try{if(X===\"wallet\"){const Q=await x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:\"wallet\"});a.success(`Pass ${Q.pass.passNumber} activated.`)}else{",
      "try{if(X===\"paylater\"){const Q=await x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:\"paylater\"});a.success(`Pass ${Q.pass.passNumber} reserved — pay ${y(Q.pass.totalAmount)} at the front desk before you check in.`)}else if(X===\"wallet\"){const Q=await x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:\"wallet\"});a.success(`Pass ${Q.pass.passNumber} activated.`)}else{"],
    ["button",
      "\"Pay online \",y(RGtotal??o.totalAmount)]})]}),",
      "\"Pay online \",y(RGtotal??o.totalAmount)]}),RGpl?.available&&(RGpl.allowedKinds||[]).includes(\"DayPass\")&&s.jsx(g,{variant:\"outline\",disabled:G!==null||!RGready,loading:G===\"paylater\",onClick:()=>re(\"paylater\"),children:\"Pay later at the front desk\"})]}),RGpl?.available&&(RGpl.allowedKinds||[]).includes(\"DayPass\")?s.jsx(\"div\",{style:MUTED,children:\"" + PL_NOTE + "\"}):null,"],
    ["my passes due line",
      "s.jsxs(\"div\",{className:\"choice-card__meta\",children:[\"Purchased \",f(t.purchaseUtc),\" · valid until \",f(t.validTo)]})",
      "s.jsxs(\"div\",{className:\"choice-card__meta\",children:[\"Purchased \",f(t.purchaseUtc),\" · valid until \",f(t.validTo)]}),t.isPayLater&&d.live&&(t.amountPaid??0)<t.totalAmount?s.jsx(\"div\",{style:{marginTop:\"var(--space-1)\",fontSize:\"var(--font-size-xs)\",fontWeight:700,color:\"var(--color-warning)\"},children:`Pay later · ${y(t.totalAmount-(t.amountPaid??0))} due at the front desk — the door opens once it is paid`}):null"],
  ]);
}
passesPage(A("PassesPage-CxAkHUg-.js"));
if (staged && fs.existsSync(path.join(staged, "PassesPage-WT1.js"))) passesPage(path.join(staged, "PassesPage-WT1.js"));

// ── Membership ───────────────────────────────────────────────────────────────
edit(A("MembershipPage-Bk9mQr2v.js"), "/passes/pay-later", [
  ["imports", "import{j as e,p as g,r,Z as PA,", "import{j as e,p as g,r,Z as PA,E as RGhttp,D as RGunwrap,"],
  ["state + status",
    "G=async a=>{if(!E||M2)return;",
    "[RGpl,RGsetPl]=r.useState(null),RGplFx=r.useEffect(()=>{let dead=!1;RGunwrap(RGhttp.get(\"/passes/pay-later\")).then(x=>{dead||RGsetPl(x??null)}).catch(()=>{dead||RGsetPl(null)});return()=>{dead=!0}},[]),G=async a=>{if(!E||M2)return;"],
  ["purchase",
    "let k;if(a===\"wallet\")k=await PA.purchase({passPlanId:f,validFrom:s,paymentMethod:\"wallet\"});else{",
    "let k;if(a===\"paylater\")k=await PA.purchase({passPlanId:f,validFrom:s,paymentMethod:\"paylater\"});else if(a===\"wallet\")k=await PA.purchase({passPlanId:f,validFrom:s,paymentMethod:\"wallet\"});else{"],
  ["history paid",
    "e.jsx(\"td\",{children:y(Number(d.totalAmount)||0)})",
    "e.jsx(\"td\",{children:d.isPayLater?((Number(d.amountPaid)||0)>0?y(Number(d.amountPaid)):\"Not paid\"):y(Number(d.totalAmount)||0)})"],
  ["toast",
    "t.success(`Membership ${k.pass.passNumber} activated.`)",
    "t.success(a===\"paylater\"?`Membership ${k.pass.passNumber} reserved — pay ${y(k.pass.totalAmount)} at the front desk before you check in.`:`Membership ${k.pass.passNumber} activated.`)"],
  ["button",
    "onClick:()=>G(\"gateway\"),children:[\"Pay online \",y(E.total)]})]}),",
    "onClick:()=>G(\"gateway\"),children:[\"Pay online \",y(E.total)]}),RGpl?.available&&(RGpl.allowedKinds||[]).includes(\"Membership\")&&e.jsx(g,{variant:\"outline\",disabled:M2!==null,loading:M2===\"paylater\",onClick:()=>G(\"paylater\"),children:\"Pay later at the front desk\"})]}),RGpl?.available&&(RGpl.allowedKinds||[]).includes(\"Membership\")?e.jsx(\"div\",{style:MUTED,children:\"" + PL_NOTE + "\"}):null,"],
]);

// ── Cash Collections → AdminCashPages-PL2.js ─────────────────────────────────
edit(A("AdminCashPages-PL1.js"), "customerPassId: booking.kind", [
  ["when helper",
    "const when = (startUtc, endUtc, months) =>\n  months ? `${fmtDate(startUtc)} – ${fmtDate(endUtc)}` : `${fmtDateTime(startUtc)}`;",
    "const when = (startUtc, endUtc, months) =>\n  months ? `${fmtDate(startUtc)} – ${fmtDate(endUtc)}` : `${fmtDateTime(startUtc)}`;\n/* Pay-later passes (28 Oct 2026): first day to last day; End is the midnight after. */\nconst whenOf = (b) => {\n  if (b.kind !== \"Pass\") return when(b.startUtc, b.endUtc, b.billingMonths);\n  const last = new Date(new Date(b.endUtc).getTime() - 1);\n  const a = fmtDate(b.startUtc), z = fmtDate(last);\n  return a === z ? a : `${a} – ${z}`;\n};"],
  ["record target",
    "        bookingId: booking.bookingId,\n",
    "        bookingId: booking.kind === \"Pass\" ? null : booking.bookingId,\n        customerPassId: booking.kind === \"Pass\" ? booking.customerPassId : null,\n"],
  ["dialog when",
    "e.jsx(\"span\", { children: when(booking.startUtc, booking.endUtc, booking.billingMonths) })",
    "e.jsx(\"span\", { children: whenOf(booking) })"],
  ["table header",
    "e.jsx(\"th\", { children: \"Booking\" }), e.jsx(\"th\", { children: \"Customer\" }), e.jsx(\"th\", { children: \"Workspace · when\" }),",
    "e.jsx(\"th\", { children: \"Booking / pass\" }), e.jsx(\"th\", { children: \"Customer\" }), e.jsx(\"th\", { children: \"What · when\" }),"],
  ["description",
    "Bookings made with “Pay later at the front desk”, and every payment taken for them in cash, UPI or card. Check-in stays blocked until a booking is paid in full.",
    "Bookings, day passes and memberships bought with “Pay later at the front desk”, and every payment taken for them in cash, UPI or card. Check-in stays blocked until each one is paid in full."],
  ["card label", "Bookings to collect", "To collect"],
  ["search placeholder", "Booking no., name, mobile", "Booking / pass no., name, mobile"],
  ["empty text", "Every pay-later booking at this location is paid.", "Every pay-later booking and pass at this location is paid."],
  ["row status",
    "e.jsx(\"div\", { className: \"pl-sub\", children: BOOKING_STATUS[b.status] ?? \"\" })",
    "e.jsx(\"div\", { className: \"pl-sub\", children: b.kind === \"Pass\" ? \"Pass / membership\" : (BOOKING_STATUS[b.status] ?? \"\") })"],
  ["row when",
    "e.jsx(\"div\", { className: \"pl-sub\", children: when(b.startUtc, b.endUtc, b.billingMonths) })",
    "e.jsx(\"div\", { className: \"pl-sub\", children: whenOf(b) })"],
  ["row key", "    ] }, b.bookingId)) })", "    ] }, `${b.kind ?? \"Booking\"}-${b.bookingId || b.customerPassId}`)) })"],
], A("AdminCashPages-PL2.js"));

const idxFile = A("index-DI5X4c9f.js");
let idx = fs.readFileSync(idxFile, "utf8");
if (idx.includes("AdminCashPages-PL1.js")) {
  idx = idx.split("AdminCashPages-PL1.js").join("AdminCashPages-PL2.js");
  fs.writeFileSync(idxFile, idx, "utf8");
  console.log("OK  - index-DI5X4c9f.js: Cash pages -> AdminCashPages-PL2.js");
} else console.log("OK  - index-DI5X4c9f.js: already points at AdminCashPages-PL2.js");

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const f of ["PassesPage-CxAkHUg-.js", "MembershipPage-Bk9mQr2v.js", "index-DI5X4c9f.js"]) {
  const re = new RegExp(`\\{url:"assets/${f.replace(/[.-]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  if (!re.test(sw)) throw new Error(`sw.js: no precache entry for ${f}`);
  sw = sw.replace(re, `{url:"assets/${f}",revision:"${REV}"}`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js: Passes, Membership and index revision ${REV}`);
