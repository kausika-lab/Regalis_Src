// Online payments switch (15 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// Production takes no online payments (no gateway): customers pay at the front
// desk, staff credit the wallet ("Add money"), customers pay from the wallet.
// Each site's /env.js sets window.__REGALIS_ONLINE_PAY__; false hides every
// online option and says "pay at the front desk" instead:
//   - Book workspace (single / multi / monthly, Virtual Office): "Pay online"
//   - Membership and Day pass: "Pay online"
//   - Invoices: "Pay now" -> "Pay at the front desk"
//   - Wallet: no "Add funds"; the description says how to add money.
// Missing env.js, or true, keeps today's behaviour, so UAT does not change.
// The production web server also refuses the online-payment API calls
// (sync-frontend-config.ps1), so hiding the buttons is not the only guard.
//
// Patches the chunks in place (keeps their names, so the staged workspace-types
// install still finds them), adds index.html's <script src="/env.js">, bumps
// sw.js, and adds no-cache entries to web.config. Idempotent.
// With a second argument it also patches the staged BookWorkspacePage-MB3.js and
// PassesPage-WT1.js, so deploying workspace types later keeps this change.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "offline-pay-1";
const MARK = "__REGALIS_ONLINE_PAY__";
const ON = `(window.${MARK}!==false)`;
const DESK = "add money at the front desk (cash, UPI or card), then pay from your wallet.";

function edit(file, edits) {
  let t = fs.readFileSync(file, "utf8");
  const name = path.basename(file);
  if (t.includes(MARK)) { console.log(`OK  - ${name}: already patched`); return; }
  for (const [what, fn] of edits) { t = fn(t, `${name}: ${what}`); console.log(`OK  - ${name}: ${what}`); }
  fs.writeFileSync(file, t, "utf8");
}
const once = (t, from, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.indexOf(from);
};
const swap = (from, to) => (t, where) => { once(t, from, where); return t.replace(from, to); };
// Prefix the nearest `opener` before a unique `anchor` with `insert`.
const guard = (anchor, opener, insert) => (t, where) => {
  const at = t.lastIndexOf(opener, once(t, anchor, where));
  if (at < 0) throw new Error(`${where}: '${opener}' not found before the anchor`);
  return t.slice(0, at) + insert + t.slice(at);
};

// --- Book workspace (readable chunk) ------------------------------------------
const bookEdits = [
  ["switch", (t) => `/* Online payments switch: /env.js (deploy/offline-payments). */\nconst ONLINE_PAY = window.${MARK} !== false;\n` + t],
  ["Virtual Office Pay online", guard('onClick: () => payVO("gateway")', "e.jsxs(h, {", "ONLINE_PAY && ")],
  ["booking Pay online", guard('onClick: () => (isM ? payMonthly : W ? paySingle : payMulti)("gateway")', "e.jsxs(h, {", "ONLINE_PAY && ")],
  ["Virtual Office wallet hint", swap(
    `" doesn't cover this — pay online or top up from the Wallet page.",`,
    `ONLINE_PAY ? " doesn't cover this — pay online or top up from the Wallet page." : " doesn't cover this — ${DESK}",`)],
  ["booking wallet hint", swap(
    `" doesn't cover this booking — pay online or top up from the Wallet page.",`,
    `ONLINE_PAY ? " doesn't cover this booking — pay online or top up from the Wallet page." : " doesn't cover this booking — ${DESK}",`)],
];

// --- Day pass -------------------------------------------------------------------
const passEdits = [
  ["Pay online", swap(
    `s.jsxs(g,{disabled:G!==null||!RGready,loading:G==="gateway",onClick:()=>re("gateway"),children:["Pay online ",y(o.totalAmount)]})]}),`,
    `${ON}&&s.jsxs(g,{disabled:G!==null||!RGready,loading:G==="gateway",onClick:()=>re("gateway"),children:["Pay online ",y(o.totalAmount)]})]}),` +
    `!${ON}&&R&&!se?s.jsx("div",{style:MUTED,children:"Your wallet doesn't cover this pass — ${DESK}"}):null,`)],
];

const A = (f) => path.join(front, "assets", f);
edit(A("BookWorkspacePage-MB2.js"), bookEdits);
edit(A("PassesPage-CxAkHUg-.js"), passEdits);

// --- Membership ---------------------------------------------------------------
edit(A("MembershipPage-Bk9mQr2v.js"), [
  ["Pay online", swap(
    `e.jsxs(g,{disabled:M2!==null,loading:M2==="gateway",onClick:()=>G("gateway"),children:["Pay online ",y(E.total)]})`,
    `${ON}&&e.jsxs(g,{disabled:M2!==null,loading:M2==="gateway",onClick:()=>G("gateway"),children:["Pay online ",y(E.total)]})`)],
  ["wallet hint", swap(
    `". Top it up under Wallet, or pay online for the full ",y(E.total),"."`,
    `${ON}?". Top it up under Wallet, or pay online for the full "+y(E.total)+".":" — ${DESK}"`)],
]);

// --- Invoices ---------------------------------------------------------------------
edit(A("InvoicesPage-IV1.js"), [
  ["Pay now", swap(
    `t.jsx(v,{size:"sm",onClick:()=>s(e),children:"Pay now"})`,
    `(${ON}?t.jsx(v,{size:"sm",onClick:()=>s(e),children:"Pay now"}):t.jsx("span",{style:{fontSize:"var(--font-size-xs)",color:"var(--text-muted)"},children:"Pay at the front desk"}))`)],
]);

// --- Wallet -------------------------------------------------------------------------
edit(A("WalletPage-WR1.js"), [
  ["Add funds", swap(
    `actions:e.jsx(c,{onClick:()=>n(!0),children:"Add funds"})`,
    `actions:${ON}?e.jsx(c,{onClick:()=>n(!0),children:"Add funds"}):null`)],
  ["description", swap(
    `description:"Refunds land here automatically. The balance is non-withdrawable and spends on bookings, passes and services."`,
    `description:"Refunds land here automatically. The balance is non-withdrawable and spends on bookings, passes and services."+(${ON}?"":" To add money, pay at the front desk (cash, UPI or card) and staff will credit your wallet.")`)],
]);

// --- env.js (this site's default: online payments on) -------------------------
const envFile = path.join(front, "env.js");
if (!fs.existsSync(envFile)) {
  fs.writeFileSync(envFile,
    "// Per-site switches. UAT: online (sandbox) payments on. Production's copy is\n" +
    "// written by deploy/production/sync-frontend-config.ps1 and is never copied over.\n" +
    `window.${MARK} = true;\n`, "utf8");
  console.log("OK  - env.js (online payments on)");
}

// --- index.html loads env.js before the app ---------------------------------------
const htmlFile = path.join(front, "index.html");
let html = fs.readFileSync(htmlFile, "utf8");
if (!html.includes('src="/env.js"')) {
  const app = '<script type="module" crossorigin src="/assets/index-DI5X4c9f.js"></script>';
  once(html, app, "index.html");
  html = html.replace(app, `<script src="/env.js"></script>\n    ${app}`);
  fs.writeFileSync(htmlFile, html, "utf8");
  console.log("OK  - index.html loads /env.js");
}

// --- sw.js: re-fetch everything changed ---------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const url of ["index.html", "assets/BookWorkspacePage-MB2.js", "assets/PassesPage-CxAkHUg-.js",
                   "assets/MembershipPage-Bk9mQr2v.js", "assets/InvoicesPage-IV1.js", "assets/WalletPage-WR1.js"]) {
  const entry = new RegExp(`\\{url:"${url.replace(/[.\-/]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  if (!entry.test(sw)) throw new Error(`sw.js: no entry for ${url}`);
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
for (const p of ["env.js", "assets/BookWorkspacePage-MB2.js", "assets/InvoicesPage-IV1.js", "assets/WalletPage-WR1.js"]) {
  if (cfg.includes(`<location path="${p}">`)) continue;
  cfg = cfg.replace("</configuration>",
    `  <location path="${p}">\n    <system.webServer>\n      <staticContent>\n        <clientCache cacheControlMode="DisableCache" />\n      </staticContent>\n    </system.webServer>\n  </location>\n</configuration>`);
  console.log(`OK  - web.config: no-cache ${p}`);
}
fs.writeFileSync(cfgFile, cfg, "utf8");

// --- staged workspace-types chunks ------------------------------------------------
if (staged) {
  edit(path.join(staged, "BookWorkspacePage-MB3.js"), bookEdits);
  edit(path.join(staged, "PassesPage-WT1.js"), passEdits);
}
console.log("DONE");
