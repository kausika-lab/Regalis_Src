// customer-10 kit (14 Nov 2026): Customer Portal brief, frontend part.
//   node apply.mjs <frontend dir>
// Patches the chunks in place (string edits, each must match exactly once) so it also works
// on copies that carry other kits' patches (index, ProfilePage). Copies the two new chunks
// (DeleteAccount-DA1.js, PaymentHistoryPage-PY1.js) from this kit when they are missing,
// and bumps their sw.js revisions. Idempotent: an edit already present is skipped.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "customer-10-1";
const here = path.dirname(fileURLToPath(import.meta.url));
const A = (f) => path.join(front, "assets", f);
const changed = new Set();

function patch(file, edits) {
  const p = A(file);
  let t = fs.readFileSync(p, "utf8");
  for (const [name, from, to] of edits) {
    const n = t.split(from).length - 1;
    // "already done" is checked first: several replacements keep the anchor text inside them.
    if ((to !== "" && t.includes(to)) || (to === "" && n === 0)) console.log(`OK  - ${file}: ${name} (already done)`);
    else if (n === 1) { t = t.replace(from, () => to); console.log(`OK  - ${file}: ${name}`); changed.add(file); }
    else throw new Error(`${file}: '${name}' expected 1 match, found ${n}; nothing written for this file`);
  }
  fs.writeFileSync(p, t, "utf8");
}

// ---- new chunks ------------------------------------------------------------------------
for (const f of ["DeleteAccount-DA1.js", "PaymentHistoryPage-PY1.js"]) {
  const src = path.join(here, "assets", f);
  if (!fs.existsSync(A(f)) || fs.readFileSync(A(f), "utf8") !== fs.readFileSync(src, "utf8")) {
    fs.copyFileSync(src, A(f)); console.log(`OK  - copied ${f}`);
  } else console.log(`OK  - ${f} (already there)`);
  changed.add(f);
}

// ---- item 8: idle sign-out for customers (index) -------------------------------------------
const IDLE = `const RGidle=(()=>{if(typeof window==="undefined")return null;const IDLE=6e5,WARN=54e4,KEEP=6e4,K="rg-last-activity";let last=Date.now(),tok=Date.now(),box=null,busy=!1,saved=0,going=!1;try{const v=Number(sessionStorage.getItem(K));v>0&&(last=v)}catch{}
const cust=()=>{const s=Fn.getState();return!!s.accessToken&&s.user?.userType===1},save=f=>{const n=Date.now();if(!f&&n-saved<5e3)return;saved=n;try{sessionStorage.setItem(K,String(last))}catch{}},hide=()=>{box&&box.remove(),box=null};
const note=()=>{const d=document.createElement("div");d.setAttribute("role","status");d.dataset.rg="idle-note";d.style.cssText="position:fixed;left:50%;top:16px;transform:translateX(-50%);z-index:10000;max-width:calc(100vw - 32px);box-sizing:border-box;background:#1d1d24;color:#fff;border-left:4px solid #c9a24b;padding:12px 16px;border-radius:10px;box-shadow:0 8px 24px rgba(0,0,0,.25);font:500 14px/1.45 system-ui,sans-serif;cursor:pointer";d.textContent="You were signed out after 10 minutes without activity. Please sign in again.";d.onclick=()=>d.remove();document.body.appendChild(d);setTimeout(()=>d.remove(),2e4)};
const out=()=>{if(going)return;going=!0;hide();const s=Fn.getState(),at=s.accessToken,rt=s.refreshToken;try{sessionStorage.removeItem(K)}catch{}rt&&at&&_t.post("/api/auth/logout",{refreshToken:rt},{headers:{Authorization:\`Bearer \${at}\`}}).catch(()=>{});s.clearSession();note();going=!1};
window.__rgIdleBlocked=()=>cust()&&Date.now()-last>=IDLE;
const stay=()=>{last=Date.now();save(!0);hide();busy=!0;pO().finally(()=>{busy=!1})};
const warn=sec=>{if(!box){box=document.createElement("div");box.dataset.rg="idle-warning";box.style.cssText="position:fixed;inset:0;z-index:10000;background:rgba(10,10,14,.55);display:flex;align-items:center;justify-content:center;padding:16px";box.innerHTML='<div role="alertdialog" aria-modal="true" aria-labelledby="rg-idle-t" style="background:#fff;color:#1d1d24;max-width:400px;width:100%;box-sizing:border-box;border-radius:14px;padding:22px;box-shadow:0 18px 48px rgba(0,0,0,.3);border-top:4px solid #c9a24b;font:400 15px/1.5 system-ui,sans-serif"><h2 id="rg-idle-t" style="margin:0 0 8px;font:700 19px/1.3 Georgia,serif">Are you still there?</h2><p style="margin:0 0 18px">For your security you will be signed out in <strong data-s></strong> seconds because there has been no activity.</p><div style="display:flex;gap:10px;justify-content:flex-end;flex-wrap:wrap"><button type="button" data-out style="padding:10px 16px;border-radius:10px;border:1px solid #d6d3cc;background:#fff;color:#1d1d24;font:600 14px system-ui,sans-serif;cursor:pointer">Sign out</button><button type="button" data-stay style="padding:10px 18px;border-radius:10px;border:0;background:#c9a24b;color:#1d1d24;font:700 14px system-ui,sans-serif;cursor:pointer">Stay signed in</button></div></div>';box.querySelector("[data-out]").onclick=out;box.querySelector("[data-stay]").onclick=stay;document.body.appendChild(box);box.querySelector("[data-stay]").focus()}const t=box.querySelector("[data-s]");t&&(t.textContent=String(sec))};
const act=ev=>{if(!cust()||box&&ev&&box.contains(ev.target))return;const n=Date.now();if(n-last>=IDLE){out();return}last=n;save();box&&hide();if(n-tok>KEEP&&!busy){busy=!0;pO().finally(()=>{busy=!1})}};
const tick=()=>{if(!cust()){hide();return}const i=Date.now()-last;if(i>=IDLE){out();return}i>=WARN?warn(Math.max(1,Math.ceil((IDLE-i)/1e3))):hide()};
Fn.subscribe((s,p)=>{if(s.accessToken!==p.accessToken){tok=Date.now();s.accessToken&&!p.accessToken&&(last=Date.now(),save(!0))}});
["pointerdown","keydown","wheel","touchstart","scroll"].forEach(n=>window.addEventListener(n,act,{capture:!0,passive:!0}));let mm=0;window.addEventListener("mousemove",ev=>{const n=Date.now();n-mm>2e3&&(mm=n,act(ev))},{passive:!0});
document.addEventListener("visibilitychange",()=>{document.visibilityState==="visible"&&tick()});window.addEventListener("pageshow",tick);setInterval(tick,1e3);return{tick,act}})();
`;
patch("index-DI5X4c9f.js", [
  ["refresh refused once idle", "const{refreshToken:t}=Fn.getState();if(!t)return null;", "const{refreshToken:t}=Fn.getState();if(!t||window.__rgIdleBlocked?.())return null;"],
  ["idle watcher", "async function he(t){", "/* customer-10: customers are signed out after 10 idle minutes (warning at 9). */" + IDLE + "async function he(t){"],
  ["payment history chunk", 'RGwait=fe(()=>ce(()=>import("./AdminWaitlistPage-WL1.js")),"AdminWaitlistPage"),',
    'RGwait=fe(()=>ce(()=>import("./AdminWaitlistPage-WL1.js")),"AdminWaitlistPage"),RGpayhist=fe(()=>ce(()=>import("./PaymentHistoryPage-PY1.js")),"PaymentHistoryPage"),'],
  ["payment history route", 'g.jsx(P,{element:g.jsx(Ne,{permission:"INVOICE.VIEW"}),children:g.jsx(P,{path:"/invoices",element:g.jsx(Gz,{})})}),',
    'g.jsx(P,{element:g.jsx(Ne,{permission:"INVOICE.VIEW"}),children:g.jsx(P,{path:"/invoices",element:g.jsx(Gz,{})})}),g.jsx(P,{element:g.jsx(Ne,{permission:"PAYMENT.VIEW"}),children:g.jsx(P,{path:"/payments",element:g.jsx(RGpayhist,{})})}),'],
]);

// ---- item 9: Delete Account on the profile -----------------------------------------------
patch("ProfilePage-DRLk3DkQ.js", [
  ["import", 'from"./PhoneField-PH1.js";', 'from"./PhoneField-PH1.js";import{DeleteAccount as RGDelAcc}from"./DeleteAccount-DA1.js";'],
  ["button", 'onClick:pwOpen,children:"Change Password"})]})}),e.jsx(MD,{open:PO', 'onClick:pwOpen,children:"Change Password"}),e.jsx(RGDelAcc,{})]})}),e.jsx(MD,{open:PO'],
]);

// ---- item 2: pass wording + "Awaiting payment" ------------------------------------------------
patch("finance-C0-kLKkN.js", [
  ["pass status 5", 't={1:"Active",2:"Used",3:"Expired",4:"Cancelled"}', 't={1:"Active",2:"Used",3:"Expired",4:"Cancelled",5:"Awaiting payment"}'],
]);
patch("PassesPage-CxAkHUg-.js", [
  ["awaiting payment is current, not usable", "function PS(t){if(t.status===1){", 'function PS(t){if(t.status===5)return{label:"Awaiting payment",tone:"warning",live:!0};if(t.status===1){'],
  ["description wording", "function UD(t,a){return t.status===2?a:0}",
    'function UD(t,a){return t.status===2?a:0} function RGdesc(t){return String(t??"").replace(/\\s*[-\\u2013\\u2014]\\s*the best per-day rate/i,"").replace(/\\bconsecutive\\s+/gi,"").replace(/\\s+at\\s+\\d+(?:\\.\\d+)?%\\s*off/gi,"")}'],
  ["card description", 'X.description?s.jsx("div",{className:"choice-card__meta",children:X.description}):null', 'X.description?s.jsx("div",{className:"choice-card__meta",children:RGdesc(X.description)}):null'],
  ["empty state", "A day pass runs from the start date you pick — a single day, or a pack of consecutive days.", "A day pass runs from the start date you pick."],
  ["header", "pick a start date and the pass runs from there, one day or a run of consecutive days. No booking needed.", "pick a start date and the pass runs from there. No booking needed."],
  ["card meta", '[X.validityDays,ee?" consecutive day":" day",X.validityDays>1?"s":"",ee?"":" validity"]', '[X.validityDays," day",X.validityDays>1?"s":"",ee?"":" validity"]'],
  ["per-day price line", 'ee&&M>1?s.jsxs("div",{style:MUTED,children:[RGprice(Q)," per day"]}):null,', ""],
  ["single-day suffix", 'children:M>1?`/ ${M} days`:"/ day"}', 'children:M>1?`/ ${M} days`:"/ 1 day"}'],
  ["date hint", "`The pass runs for ${o.validityDays} consecutive days from this date.", "`The pass runs for ${o.validityDays} days from this date."],
  ["covers line", "`Covers ${o.validityDays} consecutive days from your start date — one day credit per day.`", "`Covers ${o.validityDays} days from your start date.`"],
  ["credits exclude unpaid", "O=C.filter(u=>u.passType===1&&PS(u).live)", "O=C.filter(u=>u.passType===1&&PS(u).live&&u.status!==5)"],
  ["due note", "due at the front desk — the door opens once it is paid`", "due at the front desk — the pass becomes active once it is paid`"],
  ["pay later note", "before you check in — the door opens once it is paid.", "— the pass becomes active once it is paid."],
  ["pay later toast", "reserved — pay ${y(Q.pass.totalAmount)} at the front desk before you check in.`", "reserved — it becomes active once you pay ${y(Q.pass.totalAmount)} at the front desk.`"],
]);

// ---- item 3: membership price + benefits, no percentages --------------------------------------
patch("MembershipPage-Bk9mQr2v.js", [
  ["price style", 'const MUTED={color:"var(--text-muted)",fontSize:"var(--font-size-sm)"},PILL=',
    'const RGPRICE={display:"flex",flexWrap:"wrap",alignItems:"baseline",columnGap:"4px",fontSize:"clamp(1.4rem,2.3vw,1.8rem)",fontWeight:800,lineHeight:1.15,color:"var(--regalis-gold-dark)",letterSpacing:"-0.01em",fontVariantNumeric:"tabular-nums",marginTop:"var(--space-3)",whiteSpace:"nowrap"},MUTED={color:"var(--text-muted)",fontSize:"var(--font-size-sm)"},PILL='],
  ["plan card price", 'e.jsxs("div",{className:"choice-card__price",children:["From ",', 'e.jsxs("div",{className:"choice-card__price",style:RGPRICE,"data-rg":"mem-price",children:["From ",'],
  ["term card price", 'e.jsxs("div",{className:"choice-card__price",children:[RGprice(s.monthly)', 'e.jsxs("div",{className:"choice-card__price",style:RGPRICE,"data-rg":"mem-price",children:[RGprice(s.monthly)'],
  ["term % badge", 's.discountPct?e.jsx("div",{style:PILL,children:e.jsxs(z,{tone:"gold",children:[s.discountPct,"% off"]})}):null,', ""],
  ["6-month after", 'after:"10% off standard"', 'after:"Discounted rate"'],
  ["12-month after", 'after:"15% off standard"', 'after:"Best discounted rate"'],
  ["6-month benefit", '"10% off extra meeting room bookings"', '"Discount on extra meeting room bookings"'],
  ["12-month benefit", '"15% off extra bookings"', '"Bigger discount on extra bookings"'],
  ["awaiting membership is the current plan", "D=MEM.filter(v=>v.status===1&&", "D=MEM.filter(v=>(v.status===1||v.status===5)&&"],
  ["awaiting badge", 'e.jsx(z,{tone:"success",children:PST[t.status]??"Active"})', 'e.jsx(z,{tone:t.status===5?"warning":"success",children:PST[t.status]??"Active"})'],
  ["awaiting note", 'e.jsx(g,{variant:"outline",onClick:m,children:"Change plan"})]})]}),',
    'e.jsx(g,{variant:"outline",onClick:m,children:"Change plan"})]})]}),t.status===5?e.jsxs("div",{"data-rg":"mem-awaiting",style:{margin:"0 0 var(--space-4)",padding:"12px 16px",borderRadius:"var(--radius-md)",background:"rgb(217 119 6 / 9%)",border:"1px solid rgb(217 119 6 / 35%)",fontWeight:600},children:["Awaiting payment: pay ",RGprice(Math.max(0,(Number(t.totalAmount)||0)-(Number(t.amountPaid)||0)))," at the front desk (cash, UPI or card). Your membership becomes active once it is paid."]}):null,'],
  ["total label", 'e.jsx(Row,{label:"Total paid",value:RGprice(Number(t.totalAmount)||0)})', 'e.jsx(Row,{label:t.status===5?"Total to pay at the front desk":"Total paid",value:RGprice(Number(t.totalAmount)||0)})'],
  ["pay later note", "before you check in — the door opens once it is paid. Unpaid by the end of the first day", "— your membership becomes active once it is paid. Unpaid by the end of the first day"],
  ["benefit chips",'function List({items:t}){return e.jsx("ul",{style:{listStyle:"none",margin:0,padding:0,display:"grid",gap:"var(--space-2)"},children:t.map(l=>e.jsxs("li",{style:{display:"flex",gap:"var(--space-2)",alignItems:"flex-start",fontSize:"var(--font-size-md)"}',
    'function List({items:t}){return e.jsx("ul",{"data-rg":"mem-benefits",style:{listStyle:"none",margin:0,padding:0,display:"grid",gap:"var(--space-2)"},children:t.map(l=>e.jsxs("li",{style:{display:"flex",gap:"var(--space-2)",alignItems:"center",fontSize:"var(--font-size-md)",fontWeight:600,background:"rgb(var(--accent-rgb) / 9%)",border:"1px solid rgb(var(--accent-rgb) / 28%)",borderRadius:"var(--radius-md)",padding:"9px 12px",lineHeight:1.35}'],
]);

patch("BookWorkspacePage-MB2.js", [
  ["VO pay later note", "Pay at the front desk in cash, UPI or card — the invoice stays open until it is paid.", "Pay at the front desk in cash, UPI or card — it becomes active once it is paid."],
]);

// ---- item 5: ticket Back button + highlighted heading --------------------------------------------
const STY = 'const RGBACK={border:"1.5px solid var(--regalis-gold-dark)",borderRadius:"var(--radius-md)",padding:"8px 16px",fontWeight:700,background:"rgb(var(--accent-rgb) / 12%)",color:"var(--text-primary)",boxShadow:"var(--shadow-sm)"},RGHEAD={background:"linear-gradient(90deg,rgb(var(--accent-rgb) / 18%),rgb(var(--accent-rgb) / 4%))",borderLeft:"5px solid var(--regalis-gold-dark)",borderRadius:"var(--radius-lg)",padding:"16px 18px 2px",marginBottom:"var(--space-4)"};';
patch("SupportPages-Cf5pV8yQ.js", [
  ["styles", 'const G=[["path",{d:"m16 6-8.414', STY + 'const G=[["path",{d:"m16 6-8.414'],
  ["list heading open", 'e.jsx($,{title:"Support & Contact",', 'e.jsx("div",{style:RGHEAD,"data-rg":"ticket-head",children:e.jsx($,{title:"Support & Contact",'],
  ["list heading close", ' Raise ticket"]})})}),e.jsx(R,{children:e.jsx(q,', ' Raise ticket"]})})})}),e.jsx(R,{children:e.jsx(q,'],
  ["raise heading", 'e.jsx($,{title:"Raise Ticket",description:v===1?"What is it about?":"Tell us the details"})', 'e.jsx("div",{style:RGHEAD,"data-rg":"ticket-head",children:e.jsx($,{title:"Raise Ticket",description:v===1?"What is it about?":"Tell us the details"})})'],
  ["raise back 1", 'e.jsx(f,{variant:"ghost",onClick:()=>i("/support/tickets"),children:"← Back"})', 'e.jsx(f,{variant:"outline",style:RGBACK,"data-rg":"ticket-back",onClick:()=>i("/support/tickets"),children:"← Back"})'],
  ["raise back 2", 'e.jsx(f,{variant:"ghost",onClick:()=>{h(""),p(null)},children:"← Back"})', 'e.jsx(f,{variant:"outline",style:RGBACK,"data-rg":"ticket-back",onClick:()=>{h(""),p(null)},children:"← Back"})'],
  ["detail nav hook", "function Z(){const{id:s}=Y(),i=w(),", "function Z(){const RGnav=K(),{id:s}=Y(),i=w(),"],
  ["detail back + heading open", "return e.jsxs(e.Fragment,{children:[e.jsx($,{title:`${t.ticketNumber}",
    'return e.jsxs(e.Fragment,{children:[e.jsx("div",{className:"toolbar",children:e.jsx(f,{variant:"outline",style:RGBACK,"data-rg":"ticket-back",onClick:()=>RGnav("/support/tickets"),children:"← Back to tickets"})}),e.jsx("div",{style:RGHEAD,"data-rg":"ticket-head",children:e.jsx($,{title:`${t.ticketNumber}'],
  ["detail heading close", "]})}),e.jsxs(R,{children:[e.jsx(U,{ticket:t", "]})})}),e.jsxs(R,{children:[e.jsx(U,{ticket:t"],
]);

// ---- sw.js -------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const f of changed) {
  const url = `assets/${f}`;
  const re = new RegExp(`\\{url:"${url.replace(/[.\-]/g, (c) => "\\" + c)}",revision:(null|"[^"]*")\\}`);
  if (re.test(sw)) sw = sw.replace(re, `{url:"${url}",revision:"${REV}"}`);
  else {
    const anchor = '{url:"index.html"';
    if (!sw.includes(anchor)) throw new Error("sw.js: no index.html entry to insert before");
    sw = sw.replace(anchor, `{url:"${url}",revision:"${REV}"},${anchor}`);
  }
}
sw = sw.replace(/\{url:"index\.html",revision:(null|"[^"]*")\}/, `{url:"index.html",revision:"${REV}"}`);
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV} (${changed.size} files)`);
