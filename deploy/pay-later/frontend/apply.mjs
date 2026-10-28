// Pay later at the front desk (27 Oct 2026) - the SPA side.
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// 1. Installs AdminCashPages-PL1.js (Billing & Finance -> Cash Collections, Cash Register)
//    and routes /admin/billing/cash-collections and /admin/billing/cash-register to it.
// 2. Adds the Banknote and Calculator sidebar icons the two MST_Menu rows name.
// 3. Checkout (BookWorkspacePage): a "Pay later at the front desk" button for hourly,
//    multi-day and monthly bookings, shown only when GET /api/bookings/pay-later says the
//    customer may use it; the done screen says what to pay before check-in.
// 4. My Bookings and Admin -> Bookings show what a pay-later booking still owes; admin
//    booking gets "Pay later" next to "Cash at desk" and "Customer wallet".
//
// Patches the chunks in place (keeps their names), bumps their sw.js revisions and adds
// no-cache entries to web.config. Idempotent: a file that has the marker is skipped.
// With a second argument it also patches the staged BookWorkspacePage-MB3.js, so deploying
// workspace types later keeps this change.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const HERE = path.dirname(fileURLToPath(import.meta.url));
const REV = "pay-later-1";
const MARK = "PAY_LATER";

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
const before = (anchor, insert) => (t, where) => { const at = once(t, anchor, where); return t.slice(0, at) + insert + t.slice(at); };
const A = (f) => path.join(front, "assets", f);

// --- 1. New admin chunk + routes, 2. icons (index) -----------------------------------
fs.copyFileSync(path.join(HERE, "assets", "AdminCashPages-PL1.js"), A("AdminCashPages-PL1.js"));
console.log("OK  - AdminCashPages-PL1.js installed");

const DEPS = "__vite__mapDeps([52,17,29,6,7,23,12,13,10,14,41,25,8,20])";
edit(A("index-DI5X4c9f.js"), [
  ["lazy pages", after(
    `RGwalletDetail=fe(()=>ce(()=>import("./AdminWalletPages-RGw1.js"),${DEPS}),"AdminWalletDetailPage")`,
    `,PAY_LATER_cash=fe(()=>ce(()=>import("./AdminCashPages-PL1.js"),${DEPS}),"AdminCashCollectionsPage")` +
    `,PAY_LATER_register=fe(()=>ce(()=>import("./AdminCashPages-PL1.js"),${DEPS}),"AdminCashRegisterPage")`)],
  ["routes", after(
    `g.jsx(P,{path:"wallet/:customerId",element:g.jsx(RGwalletDetail,{})})]}),`,
    `g.jsx(P,{element:g.jsx(Ne,{permission:"PAYMENT.CASH_COLLECT"}),children:g.jsx(P,{path:"cash-collections",element:g.jsx(PAY_LATER_cash,{})})}),` +
    `g.jsx(P,{element:g.jsx(Ne,{permission:"PAYMENT.CASH_CLOSE"}),children:g.jsx(P,{path:"cash-register",element:g.jsx(PAY_LATER_register,{})})}),`)],
  ["icons", after(
    `RGchevron=Oe("chevron-down",RGi4)`,
    `,PAY_LATER_i1=[["rect",{width:"20",height:"12",x:"2",y:"6",rx:"2",key:"plb1"}],["circle",{cx:"12",cy:"12",r:"2",key:"plb2"}],["path",{d:"M6 12h.01M18 12h.01",key:"plb3"}]],PLbanknote=Oe("banknote",PAY_LATER_i1)` +
    `,PAY_LATER_i2=[["rect",{width:"16",height:"20",x:"4",y:"2",rx:"2",key:"plc1"}],["line",{x1:"8",x2:"16",y1:"6",y2:"6",key:"plc2"}],["line",{x1:"16",x2:"16",y1:"14",y2:"18",key:"plc3"}],["path",{d:"M16 10h.01M12 10h.01M8 10h.01M12 14h.01M8 14h.01M12 18h.01M8 18h.01",key:"plc4"}]],PLcalc=Oe("calculator",PAY_LATER_i2)`)],
  ["icon map", swap(`ChevronDown:RGchevron}`, `ChevronDown:RGchevron,Banknote:PLbanknote,Calculator:PLcalc}`)],
]);

// --- 3. Checkout ---------------------------------------------------------------------
const KIND = `(isM ? "Monthly" : W ? "Hourly" : "MultiDay")`;
const bookEdits = [
  ["status", after(
    `  const [voDone, setVoDone] = a.useState(null);\n`,
    `  /* PAY_LATER: may this customer pay at the front desk (deploy/pay-later). */\n` +
    `  const [payLater, setPayLater] = a.useState(null);\n` +
    `  a.useEffect(() => {\n` +
    `    fe(je.get("/bookings/pay-later")).then(setPayLater).catch(() => setPayLater(null));\n` +
    `  }, []);\n`)],
  ["settle", after(
    `  const settle = async (bookingId, method) => {\n`,
    `    if (method === "paylater") {\n` +
    `      try {\n` +
    `        return await k.confirm(bookingId, { paymentMethod: "paylater" });\n` +
    `      } catch (err) {\n` +
    `        /* Free the slot at once, so paying now does not collide with this hold. */\n` +
    `        try { await Be.cancel(bookingId, "Pay later was not available"); } catch (e2) {}\n` +
    `        throw err;\n` +
    `      }\n` +
    `    }\n`)],
  ["button", after(
    `                    onClick: () => (isM ? payMonthly : W ? paySingle : payMulti)("gateway"),\n` +
    `                    children: [e.jsx(we, { size: 18 }), " Pay online"],\n` +
    `                  }),\n`,
    `                  payLater?.available && payLater.allowedKinds.includes(${KIND}) && e.jsx(h, {\n` +
    `                    size: "lg",\n` +
    `                    variant: "outline",\n` +
    `                    disabled: S !== null,\n` +
    `                    loading: S === "paylater",\n` +
    `                    onClick: () => (isM ? payMonthly : W ? paySingle : payMulti)("paylater"),\n` +
    `                    children: "Pay later at the front desk",\n` +
    `                  }),\n`)],
  ["hint", before(
    `              !U &&\n                c &&\n                e.jsxs("p", {\n                  style: {\n                    color: "var(--text-muted)",\n                    fontSize: "var(--font-size-xs)",\n                  },\n                  children: [\n                    "Wallet balance ",\n                    p(c.balance),\n                    ONLINE_PAY ? " doesn't cover this booking`,
    `              payLater?.available && payLater.allowedKinds.includes(${KIND}) &&\n` +
    `                e.jsx("p", {\n` +
    `                  style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" },\n` +
    `                  children: "Pay later confirms the booking now. Pay at the front desk in cash, UPI or card before you check in — check-in opens once it is paid. An unpaid booking can be released once its start time has passed.",\n` +
    `                }),\n`)],
  ["done notice", before(
    `              multi &&\n                e.jsx("div", {\n                  className: "summary-rows",`,
    `              ((l && l.isPayLater) || (multi && multi.some((t) => t.isPayLater))) &&\n` +
    `                e.jsx("p", {\n` +
    `                  style: { marginTop: "var(--space-3)", padding: "var(--space-3) var(--space-4)", borderRadius: "var(--radius-md)", background: "color-mix(in srgb, var(--color-warning) 14%, transparent)", fontWeight: 600 },\n` +
    `                  children: \`Pay \${p(l ? l.booking.totalAmount : multi.reduce((s, t) => s + t.booking.totalAmount, 0))} at the front desk (cash, UPI or card) before you check in. Your QR pass works once it is paid.\`,\n` +
    `                }),\n`)],
];
edit(A("BookWorkspacePage-MB2.js"), bookEdits);

// --- 4. Booking lists + admin booking ---------------------------------------------------
const OWES = (t, money) =>
  `${t}.isPayLater&&${t}.amountPaid<${t}.totalAmount&&${t}.status!==5&&${t}.status!==6?e.jsxs("div",{children:[${money}(${t}.totalAmount),e.jsx("div",{style:{color:"var(--color-warning)",fontSize:"var(--font-size-xs)",fontWeight:600},children:"Pay later · "+${money}(${t}.totalAmount-${t}.amountPaid)+" due"})]}):${money}(${t}.totalAmount)`;
edit(A("MyBookingsPage-Jm2rP9kD.js"), [
  ["amount due", swap(`{key:"amount",header:"Amount",render:t=>S(t.totalAmount)}`, `{key:"amount",header:"Amount",render:t=>/*PAY_LATER*/${OWES("t", "S")}}`)],
]);
edit(A("AdminBookingsPage-BK2.js"), [
  ["amount due", swap(`{key:"amount",header:"Amount",render:t=>x(t.totalAmount)}`, `{key:"amount",header:"Amount",render:t=>/*PAY_LATER*/${OWES("t", "x")}}`)],
  ["settlement option", swap(
    `options:[{value:"cash",label:"Cash at desk"},{value:"wallet",label:"Customer wallet"}]`,
    `options:[{value:"cash",label:"Cash at desk"},{value:"wallet",label:"Customer wallet"},{value:"paylater",label:"Pay later (collect at the desk)"}]`)],
]);

// --- sw.js: re-fetch what changed in place (entries that exist) -----------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const url of ["assets/index-DI5X4c9f.js", "assets/BookWorkspacePage-MB2.js", "assets/MyBookingsPage-Jm2rP9kD.js", "assets/AdminBookingsPage-BK2.js"]) {
  const entry = new RegExp(`\\{url:"${url.replace(/[.\-/]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  if (!entry.test(sw)) { console.log(`OK  - sw.js: ${url} not precached`); continue; }
  sw = sw.replace(entry, `{url:"${url}",revision:"${REV}"}`);
}
for (const [, url] of sw.matchAll(/\{url:"([^"]+)"/g)) {
  if (!fs.existsSync(path.join(front, url))) throw new Error(`sw.js lists ${url}, which is not on disk`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);

// --- web.config: these files change in place, so they must revalidate --------------
const cfgFile = path.join(front, "web.config");
let cfg = fs.readFileSync(cfgFile, "utf8");
for (const p of ["assets/index-DI5X4c9f.js", "assets/BookWorkspacePage-MB2.js", "assets/MyBookingsPage-Jm2rP9kD.js", "assets/AdminBookingsPage-BK2.js"]) {
  if (cfg.includes(`<location path="${p}">`)) continue;
  cfg = cfg.replace("</configuration>",
    `  <location path="${p}">\n    <system.webServer>\n      <staticContent>\n        <clientCache cacheControlMode="DisableCache" />\n      </staticContent>\n    </system.webServer>\n  </location>\n</configuration>`);
  console.log(`OK  - web.config: no-cache ${p}`);
}
fs.writeFileSync(cfgFile, cfg, "utf8");

// --- staged workspace-types chunk -------------------------------------------------------
if (staged) {
  edit(path.join(staged, "BookWorkspacePage-MB3.js"), bookEdits);
}
console.log("DONE");
