// Book Pass / My Passes split and GST-free plan cards (23 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// Passes page (PassesPage-CxAkHUg-.js, route /passes) becomes "Book Pass": the plan
// cards show the pack price before GST ("<price> / 5 days" with the per-day rate under
// it, or "<price> / day"), a Price / CGST + SGST / Total summary appears under the date
// once a plan is picked, and the "Your passes" list is gone; a successful purchase
// opens My Passes. The chunk's MyPassesPage export (already there, never routed) gets
// the route /passes/my, a "Buy a pass" header button and a Buy link in its empty state.
// The sidebar rows come from db/migrations/20261023050000_PassesMenuAndPrice.sql.
//
// Patches the live chunk and index in place and bumps sw.js; with a second argument
// also patches the staged PassesPage-WT1.js. Idempotent.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "my-passes-2";
const MARK = "/passes/my";

// v2 (23 Oct 2026): no QR action on the pass cards. The "Use a Day" / "Access" button
// opened a check-in modal that only pointed at My QR / Access; every card now offers
// View receipt, and live ones keep Cancel.
const QR_OLD =
  'children:[d.live?s.jsxs(g,{size:"sm",onClick:()=>a(t),children:[s.jsx(T,{size:14})," ",i?"Use a Day":"Access"]}):s.jsx(g,{size:"sm",variant:"outline",onClick:()=>l(t),children:"View receipt"}),d.live&&cc?';
const QR_NEW =
  'children:[s.jsx(g,{size:"sm",variant:"outline",onClick:()=>l(t),children:"View receipt"}),d.live&&cc?';

const once = (t, from, to, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.replace(from, () => to);
};

const SUMMARY =
  's.jsxs("div",{className:"summary-rows",style:{marginTop:"var(--space-2)"},children:[' +
  's.jsxs("div",{children:[s.jsx("span",{children:"Price"}),s.jsx("span",{children:y(o.price)})]}),' +
  's.jsxs("div",{children:[s.jsx("span",{children:"CGST + SGST"}),s.jsx("span",{children:y((o.totalAmount??o.price)-o.price)})]}),' +
  's.jsxs("div",{className:"summary-total",children:[s.jsx("span",{children:"Total"}),s.jsx("span",{children:y(o.totalAmount??o.price)})]})]})';

function patchPage(file) {
  const name = path.basename(file);
  let t = fs.readFileSync(file, "utf8");
  const w = (what) => `${name}: ${what}`;
  if (t.includes(MARK)) {
    if (t.includes(QR_OLD)) {
      fs.writeFileSync(file, once(t, QR_OLD, QR_NEW, w("qr button")), "utf8");
      console.log(`OK  - ${name}: QR action removed from the pass cards (v2)`);
    } else console.log(`OK  - ${name}: already patched`);
    return;
  }

  // a. page title
  t = once(t, 's.jsx(A,{title:"Passes",description:', 's.jsx(A,{title:"Book Pass",description:', w("title"));

  // b. plan cards: price before GST, per-day rate only for packs
  t = once(
    t,
    's.jsxs("div",{className:"choice-card__price",children:[y(X.totalAmount)," ",s.jsx("span",{style:{fontWeight:400,color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:"incl. GST"})]}),ee?s.jsxs("div",{style:MUTED,children:[y(Q)," per day before GST"]}):null',
    's.jsxs("div",{className:"choice-card__price",children:[y(X.price)," ",s.jsx("span",{style:{fontWeight:400,color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:M>1?`/ ${M} days`:"/ day"})]}),ee&&M>1?s.jsxs("div",{style:MUTED,children:[y(Q)," per day"]}):null',
    w("plan card price"),
  );

  // c. GST breakdown under the date once a plan is picked
  t = once(
    t,
    ':"Covers the single day you pick."}):null]})})}):null',
    ':"Covers the single day you pick."}):null,' + SUMMARY + ']})})}):null',
    w("GST summary"),
  );

  // d. the purchased list moves to My Passes
  t = once(
    t,
    ',s.jsxs("div",{style:{marginTop:"var(--space-4)"},children:[s.jsx("h2",{style:{fontFamily:"var(--font-display)",fontSize:"var(--font-size-lg)",fontWeight:700,marginBottom:"var(--space-3)"},children:"Your passes"}),s.jsx(k,{reloadKey:C,onBuyMore:ne,onChanged:()=>{_.me().then(V).catch(()=>{})}})]})',
    ',s.jsx("div",{style:{marginTop:"var(--space-4)"},children:s.jsx(b,{children:s.jsxs("div",{style:{display:"flex",alignItems:"center",justifyContent:"space-between",gap:"var(--space-3)",flexWrap:"wrap"},children:[s.jsx("span",{style:{color:"var(--text-secondary)",fontSize:"var(--font-size-sm)"},children:"Passes you have bought are listed under My Passes."}),s.jsx(g,{size:"sm",variant:"outline",onClick:()=>l("/passes/my"),children:"Open My Passes"})]})})})',
    w("your passes section"),
  );

  // e. after a purchase, show the new pass in My Passes
  t = once(
    t,
    'O(Q=>Q+1),_.me().then(V).catch(()=>{})}catch(Q){a.error(Q instanceof w?Q.message:"Purchase failed.")}',
    'O(Q=>Q+1),_.me().then(V).catch(()=>{}),l("/passes/my")}catch(Q){a.error(Q instanceof w?Q.message:"Purchase failed.")}',
    w("purchase success"),
  );

  // f. My Passes page: Buy a pass button, Buy link in the empty state
  t = once(
    t,
    'function J(){return s.jsxs(s.Fragment,{children:[s.jsx(A,{title:"My Passes",description:"Day and monthly passes you have purchased."}),s.jsx(k,{})]})}',
    'function J(){const nav=U0();return s.jsxs(s.Fragment,{children:[s.jsx(A,{title:"My Passes",description:"Day and monthly passes you have purchased.",actions:s.jsx(g,{size:"sm",onClick:()=>nav("/passes"),children:"Buy a pass"})}),s.jsx(k,{onBuyMore:()=>nav("/passes")})]})}',
    w("my passes page"),
  );

  // g. (v2) no QR action on the cards
  t = once(t, QR_OLD, QR_NEW, w("qr button"));

  fs.writeFileSync(file, t, "utf8");
  console.log(`OK  - ${name}: Book Pass cards, GST summary, list moved to My Passes, no QR action`);
}

patchPage(path.join(front, "assets", "PassesPage-CxAkHUg-.js"));

// index: lazy MyPassesPage + route /passes/my
const idxFile = path.join(front, "assets", "index-DI5X4c9f.js");
let idx = fs.readFileSync(idxFile, "utf8");
if (idx.includes('path:"/passes/my"')) {
  console.log("OK  - index-DI5X4c9f.js: already routes /passes/my");
} else {
  const m = idx.match(/Ef=fe\(\(\)=>ce\(\(\)=>import\("\.\/PassesPage-[^"]+\.js"\),(__vite__mapDeps\(\[[^\]]*\]\))\),"PassPlansPage"\),/);
  if (!m) throw new Error("index-DI5X4c9f.js: PassPlansPage lazy import not found");
  const chunk = m[0].match(/import\("([^"]+)"\)/)[1];
  idx = once(
    idx,
    m[0],
    `${m[0]}RGmyPasses=fe(()=>ce(()=>import("${chunk}"),${m[1]}),"MyPassesPage"),`,
    "index: lazy MyPassesPage",
  );
  idx = once(
    idx,
    'g.jsx(P,{path:"/passes",element:g.jsx(Ef,{})})]})',
    'g.jsx(P,{path:"/passes",element:g.jsx(Ef,{})}),g.jsx(P,{path:"/passes/my",element:g.jsx(RGmyPasses,{})})]})',
    "index: route /passes/my",
  );
  fs.writeFileSync(idxFile, idx, "utf8");
  console.log("OK  - index-DI5X4c9f.js: MyPassesPage routed at /passes/my");
}

// sw.js: both files refetch
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const f of ["index-DI5X4c9f.js", "PassesPage-CxAkHUg-.js"]) {
  const re = new RegExp(`\\{url:"assets/${f.replace(/[.-]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  if (!re.test(sw)) throw new Error(`sw.js: no precache entry for ${f}`);
  sw = sw.replace(re, `{url:"assets/${f}",revision:"${REV}"}`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js: index + PassesPage revision ${REV}`);

if (staged) {
  const wt1 = path.join(staged, "PassesPage-WT1.js");
  if (fs.existsSync(wt1)) patchPage(wt1);
  else console.log(`--  - ${wt1}: not found, skipped`);
}
