// One day pass at a time, upgrades priced from the running pass (23 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// Book Pass reads GET /api/passes/purchase-options?type=1 (new in this release) and:
//   - shows the running day pass above the packs (days left, value of the unused days);
//   - greys out packs that are not an upgrade (same or fewer days) with the server's reason;
//   - marks the bigger packs "Upgrade · <net> after <credit> credit";
//   - for an upgrade fixes the start date (today, or the current pass's start if ahead) and
//     shows Price / Less unused days / Taxable / GST / Total from the server quote; the
//     wallet check and the Pay online amount use that total.
// The API enforces the same rule, so the page is a mirror, not the guard.
// Patches PassesPage-CxAkHUg-.js in place and bumps sw.js; with a second argument also
// patches the staged PassesPage-WT1.js. Idempotent.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "pass-upgrade-1";
const MARK = "/passes/purchase-options";

const once = (t, from, to, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.replace(from, () => to);
};

const OPTIONS_STATE =
  'const[RGopts,RGsetOpts]=r.useState(null);' +
  'r.useEffect(()=>{if(d!==1){RGsetOpts(null);return}let dead=!1;RGunwrap(RGhttp.get("/passes/purchase-options",{params:{type:1}})).then(M=>{dead||RGsetOpts(M??null)}).catch(()=>{dead||RGsetOpts(null)});return()=>{dead=!0}},[d,p,C]);' +
  'const RGcur=RGopts?.current??null,RGopt=o&&RGopts?RGopts.plans?.find(M=>M.passPlanId===o.passPlanId)??null:null,RGtotal=RGopt?RGopt.totalAmount:o?.totalAmount;' +
  'r.useEffect(()=>{RGopt?.isUpgrade&&RGopt.startsOn&&Y(String(RGopt.startsOn).slice(0,10))},[RGopt?.passPlanId,RGopt?.isUpgrade,RGopt?.startsOn]);';

const BANNER =
  'ee&&RGcur?s.jsx("div",{style:{marginBottom:"var(--space-3)"},children:s.jsx(b,{children:s.jsxs("div",{style:{display:"flex",gap:"var(--space-3)",alignItems:"flex-start",flexWrap:"wrap"},children:[' +
  's.jsx("span",{style:{display:"inline-flex",alignItems:"center",justifyContent:"center",width:40,height:40,flex:"none",borderRadius:"var(--radius-full)",background:"rgb(var(--accent-rgb) / 14%)",color:"var(--accent-strong)"},children:s.jsx(S,{size:20})}),' +
  's.jsxs("div",{style:{flex:1,minWidth:220},children:[' +
  's.jsxs("div",{style:{fontWeight:700},children:["You\'re on ",RGcur.planName," (",RGcur.passNumber,") · ",RGcur.remainingDays," of ",RGcur.totalDays," day",RGcur.totalDays===1?"":"s"," left · valid until ",f(RGcur.validTo)]}),' +
  's.jsxs("div",{style:{...MUTED,marginTop:"var(--space-1)"},children:["One pass at a time: while it runs you can only upgrade to a bigger pack, and the unused ",y(RGcur.unusedCredit)," comes off the price. The other packs open up when it ends."]})]})]})})}):null,';

const SUMMARY_OLD =
  's.jsxs("div",{className:"summary-rows",style:{marginTop:"var(--space-2)"},children:[' +
  's.jsxs("div",{children:[s.jsx("span",{children:"Price"}),s.jsx("span",{children:y(o.price)})]}),' +
  's.jsxs("div",{children:[s.jsx("span",{children:"CGST + SGST"}),s.jsx("span",{children:y((o.totalAmount??o.price)-o.price)})]}),' +
  's.jsxs("div",{className:"summary-total",children:[s.jsx("span",{children:"Total"}),s.jsx("span",{children:y(o.totalAmount??o.price)})]})]})';
const SUMMARY_NEW =
  's.jsxs("div",{className:"summary-rows",style:{marginTop:"var(--space-2)"},children:[' +
  'RGopt?.isUpgrade?s.jsx("div",{style:{...MUTED,marginBottom:"var(--space-1)"},children:`Starts ${f(RGopt.startsOn)} and replaces ${RGcur?.passNumber??"your current pass"}; its ${RGcur?.remainingDays??0} unused day${(RGcur?.remainingDays??0)===1?"":"s"} are credited below.`}):null,' +
  's.jsxs("div",{children:[s.jsx("span",{children:"Price"}),s.jsx("span",{children:y(RGopt?RGopt.price:o.price)})]}),' +
  'RGopt?.isUpgrade?s.jsxs("div",{children:[s.jsx("span",{children:`Less unused days of ${RGcur?.passNumber??"current pass"}`}),s.jsxs("span",{children:["− ",y(RGopt.credit)]})]}):null,' +
  'RGopt?.isUpgrade?s.jsxs("div",{children:[s.jsx("span",{children:"Taxable amount"}),s.jsx("span",{children:y(RGopt.taxableAmount)})]}):null,' +
  's.jsxs("div",{children:[s.jsx("span",{children:"CGST + SGST"}),s.jsx("span",{children:y(RGopt?RGopt.cgstAmount+RGopt.sgstAmount:(o.totalAmount??o.price)-o.price)})]}),' +
  's.jsxs("div",{className:"summary-total",children:[s.jsx("span",{children:"Total"}),s.jsx("span",{children:y(RGopt?RGopt.totalAmount:o.totalAmount??o.price)})]})]})';

function patchPage(file) {
  const name = path.basename(file);
  let t = fs.readFileSync(file, "utf8");
  if (t.includes(MARK)) {
    console.log(`OK  - ${name}: already patched`);
    return;
  }
  const w = (what) => `${name}: ${what}`;

  // 1. raw http + envelope unwrap from the index chunk
  t = once(t, "import{a as v,r,Z as x,", "import{a as v,r,Z as x,E as RGhttp,D as RGunwrap,", w("imports"));

  // 2. options state, quote lookup, fixed start date for an upgrade
  t = once(
    t,
    'RGsetAv({status:"error"})});return()=>{dead=!0}},[d,o,K,u,RGdesk]);',
    'RGsetAv({status:"error"})});return()=>{dead=!0}},[d,o,K,u,RGdesk]);' + OPTIONS_STATE,
    w("options state"),
  );
  t = once(
    t,
    'const RGready=!(d===1)||(K!==""&&RGav.status==="ok");',
    'const RGready=(!(d===1)||(K!==""&&RGav.status==="ok"))&&!(RGopt&&!RGopt.allowed);',
    w("ready"),
  );

  // 3. wallet check and Pay online amount use the quoted total
  t = once(
    t,
    "se=R!==null&&o!==null&&R.balance>=o.totalAmount,",
    "se=R!==null&&o!==null&&R.balance>=(RGtotal??o.totalAmount),",
    w("wallet check"),
  );
  t = once(t, 'children:["Pay online ",y(o.totalAmount)]', 'children:["Pay online ",y(RGtotal??o.totalAmount)]', w("pay online"));

  // 4. the running pass, above the packs
  t = once(
    t,
    'children:ee?"Buy day credits":`Buy a ${W.toLowerCase().replace(/es$/,"")}`}),',
    'children:ee?"Buy day credits":`Buy a ${W.toLowerCase().replace(/es$/,"")}`}),' + BANNER,
    w("current pass banner"),
  );

  // 5. plan cards: blocked ones disabled with the reason, upgrades priced
  t = once(
    t,
    'const M=CR(X),Q=(X.price??0)/Math.max(1,M),J=o?.passPlanId===X.passPlanId;return s.jsxs("button",{type:"button",className:`choice-card${J?" choice-card--selected":""}`,style:{position:"relative"},onClick:()=>c(X),children:[',
    'const M=CR(X),Q=(X.price??0)/Math.max(1,M),J=o?.passPlanId===X.passPlanId,OPT=RGopts?.plans?.find(z2=>z2.passPlanId===X.passPlanId)??null,BLK=!!(OPT&&!OPT.allowed);return s.jsxs("button",{type:"button",disabled:BLK,title:BLK?OPT.reason:void 0,className:`choice-card${J?" choice-card--selected":""}`,style:{position:"relative"},onClick:()=>c(X),children:[',
    w("card: open"),
  );
  t = once(
    t,
    'ee&&M>1?s.jsxs("div",{style:MUTED,children:[y(Q)," per day"]}):null]},X.passPlanId)',
    'ee&&M>1?s.jsxs("div",{style:MUTED,children:[y(Q)," per day"]}):null,' +
      'OPT?.isUpgrade?s.jsxs("div",{style:{marginTop:"var(--space-2)",fontSize:"var(--font-size-xs)",fontWeight:700,color:"var(--regalis-gold-dark)"},children:["Upgrade · ",y(OPT.taxableAmount)," after ",y(OPT.credit)," credit"]}):' +
      'BLK?s.jsx("div",{style:{marginTop:"var(--space-2)",fontSize:"var(--font-size-xs)",color:"var(--text-muted)"},children:"Not available while your current pass runs"}):null]},X.passPlanId)',
    w("card: price lines"),
  );

  // 6. an upgrade's start date is fixed by the server
  t = once(
    t,
    's.jsx(D,{label:ee?"First visit date":"Starts on",type:"date",required:ee,value:K,',
    's.jsx(D,{label:ee?"First visit date":"Starts on",type:"date",disabled:!!RGopt?.isUpgrade,required:ee,value:K,',
    w("date input"),
  );

  // 7. the quote, line by line
  t = once(t, SUMMARY_OLD, SUMMARY_NEW, w("summary"));

  fs.writeFileSync(file, t, "utf8");
  console.log(`OK  - ${name}: one pass at a time, upgrade pricing from the server quote`);
}

patchPage(path.join(front, "assets", "PassesPage-CxAkHUg-.js"));

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const entry = /\{url:"assets\/PassesPage-CxAkHUg-\.js",revision:(null|"[^"]*")\}/;
if (!entry.test(sw)) throw new Error("sw.js: no precache entry for PassesPage-CxAkHUg-.js");
sw = sw.replace(entry, `{url:"assets/PassesPage-CxAkHUg-.js",revision:"${REV}"}`);
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js: PassesPage-CxAkHUg-.js revision ${REV}`);

if (staged) {
  const wt1 = path.join(staged, "PassesPage-WT1.js");
  if (fs.existsSync(wt1)) patchPage(wt1);
  else console.log(`--  - ${wt1}: not found, skipped`);
}
