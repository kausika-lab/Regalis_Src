import{a as v,r,Z as x,A as w,j as s,T as S,G as I,p as g,Q as T,O as _,I as D,u as U0,z as L0,y as RGwsTypes}from"./index-DI5X4c9f.js";import{p as E,a as RGavail}from"./bookings-DrKATIWm.js";import{B as z}from"./Badge-BU5iAxex.js";import{C as b}from"./Card-CNXipofE.js";import{M as N}from"./Modal-BtdVDo1B.js";import{P as A}from"./PageHeader-Y96MMVry.js";import{C as CD}from"./ConfirmDialog-Ct1kLH-J.js";import{P as $,a as B,b as F}from"./finance-C0-kLKkN.js";import{b as y,c as f}from"./formatters-DFe5xCRi.js";
/* The availability answer, shown under the date field. Deliberately plain text
   in the page's own muted style rather than a new component: the brief was to
   change the flow, not the look of the page. */
function RGavailPanel({ date, state, plan, location, days }) {
  const line = (color, text, key) => s.jsx("div", {
    style: { marginTop: "var(--space-2)", fontSize: "var(--font-size-sm)", color },
    children: text
  }, key);

  if (!date) {
    return line("var(--text-muted)", "Pick your first visit date to check desk availability.", "prompt");
  }
  if (state.status === "checking") {
    return line("var(--text-muted)", "Checking desk availability…", "checking");
  }
  if (state.status === "closed") {
    const day = RGparseDay(date);
    return line("var(--color-danger)",
      `The office is closed on ${RGfmtDay(day)}. Pick another date.`, "closed");
  }
  if (state.status === "full") {
    return line("var(--color-danger)",
      "Every desk is taken that day. Pick another date.", "full");
  }
  if (state.status === "error") {
    /* An availability lookup that fails is not a reason to refuse a sale, but the
       member should know the check did not happen. Payment stays blocked so the
       decision is theirs to retry rather than ours to guess. */
    return line("var(--color-danger)",
      "We could not check desk availability just now. Try again in a moment.", "error");
  }
  if (state.status === "ok") {
    const cover = RGcoverage(location, date, Number(days) || 1);
    const closedNote = cover && cover.closed.length > 0
      ? ` ${cover.open} of the ${cover.total} days fall on days we are open; the office is closed on ${cover.closed.map(RGfmtDay).join(", ")}.`
      : "";
    return line("var(--color-success)",
      `Desks are available on ${RGfmtDay(RGparseDay(date))}.${closedNote}`, "ok");
  }
  return null;
}

/* ── Desk availability for a day pass ────────────────────────────────────────
   A day pass is walk-in access to shared desks. Nothing is reserved when it is
   bought — credits come off at check-in — so the office can still be full on the
   day the member turns up. This is the check that stops someone buying a pass
   for a day they cannot use.

   Only the FIRST VISIT DATE blocks the purchase. A multi-day pack runs over
   consecutive calendar days and the office is shut on Sundays, so a 20-day pack
   always spans days it is closed; blocking on those would make every pack
   unbuyable. They are reported instead, so the member sees what they are
   actually getting before they pay.

   Availability comes from the same endpoint the booking calendar uses, so a day
   with every desk booked reads as unavailable here too. It stays advisory by
   nature: nothing is held until check-in. */

const RGdayCodes = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

/* "2026-09-25" -> local midnight. Deliberately not new Date(iso), which parses a
   bare date as UTC and lands on the previous day west of Greenwich. */
function RGparseDay(iso) {
  const parts = String(iso).split("-").map(Number);
  return new Date(parts[0], parts[1] - 1, parts[2]);
}

function RGisOpen(location, date) {
  const days = String(location?.workingDays ?? "")
    .split(",").map(d => d.trim().toUpperCase());
  return days.includes(RGdayCodes[date.getDay()]);
}

function RGfmtDay(date) {
  return date.toLocaleDateString("en-IN", { weekday: "long", day: "numeric", month: "short" });
}

/* How a run of consecutive days falls across the location's working week. */
function RGcoverage(location, startIso, dayCount) {
  if (!startIso || !location || !(dayCount > 0)) return null;
  const start = RGparseDay(startIso);
  const closed = [];
  let open = 0;
  for (let i = 0; i < dayCount; i++) {
    const day = new Date(start);
    day.setDate(start.getDate() + i);
    if (RGisOpen(location, day)) open++; else closed.push(day);
  }
  return { open, closed, total: dayCount };
}

const POP=10,SOON=7,HERO={borderRadius:"var(--radius-lg)",background:"radial-gradient(700px 300px at 85% -20%,rgb(var(--accent-rgb) / 30%),transparent),linear-gradient(120deg,var(--regalis-black),#1d1d24)",color:"var(--regalis-white)",boxShadow:"var(--shadow-md)",padding:"var(--space-5) var(--space-6)",display:"flex",alignItems:"center",justifyContent:"space-between",gap:"var(--space-4)",flexWrap:"wrap",marginBottom:"var(--space-4)"},MUTED={color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},PILL={position:"absolute",top:"var(--space-3)",right:"var(--space-3)"};
function CR(t){const a=`${t?.planCode??""} ${t?.planName??""}`;if(/single/i.test(a))return 1;const l=a.match(/(\d+)\s*-?\s*day/i);return l?Math.max(1,parseInt(l[1],10)):1}
function DL(t){return Math.ceil((new Date(t).getTime()-Date.now())/864e5)}
function HM(t){return String(t??"").slice(0,5)}
function WD(t){return String(t??"").split(",").map(a=>a.trim()).filter(Boolean).map(a=>a.charAt(0).toUpperCase()+a.slice(1).toLowerCase()).join(", ")}
function PS(t){if(t.status===1){const a=DL(t.validTo);if(a<0)return{label:F[3]??"Expired",tone:"neutral",live:!1};return a<=SOON?{label:"Expiring soon",tone:"warning",live:!0}:{label:"Active",tone:"success",live:!0}}return{label:F[t.status]??"—",tone:t.status===4?"danger":t.status===2?"info":"neutral",live:!1}}
function UD(t,a){return t.status===2?a:0}
function TODAY(){const t=new Date();return`${t.getFullYear()}-${String(t.getMonth()+1).padStart(2,"0")}-${String(t.getDate()).padStart(2,"0")}`}
function RF(t){return String(t?.validFrom??"").slice(0,10)>TODAY()}
function CXMSG(t){if(!t)return"";const n=`${t.planName} (${t.passNumber})`;return RF(t)?`${n} starts on ${f(t.validFrom)}. Cancelling now refunds ${y(t.totalAmount)} to your wallet. This cannot be undone.`:`${n} has already started, so cancelling refunds nothing. This cannot be undone.`}
function Dots({total:t,used:a}){return s.jsx("div",{style:{display:"flex",gap:6,flexWrap:"wrap",marginTop:"var(--space-2)"},"aria-hidden":!0,children:Array.from({length:t},(l,d)=>s.jsx("span",{style:{width:12,height:12,borderRadius:"50%",background:d<a?"var(--regalis-gold-dark)":"transparent",border:`1px solid ${d<a?"var(--regalis-gold-dark)":"var(--border-strong)"}`}},d))})}
function Office({office:t}){if(!t)return null;const a=[t.addressLine1,t.addressLine2,t.city,t.state,t.postalCode].map(d=>(d||"").trim()).filter(Boolean).join(", "),l=t.workingHoursStart&&t.workingHoursEnd?`${HM(t.workingHoursStart)} – ${HM(t.workingHoursEnd)}`:null;return s.jsx(b,{children:s.jsxs("div",{style:{display:"flex",alignItems:"center",gap:"var(--space-3)",flexWrap:"wrap"},children:[s.jsx(S,{size:20,color:"var(--regalis-gold-dark)","aria-hidden":!0}),s.jsxs("div",{style:{minWidth:0},children:[s.jsx("div",{style:{fontWeight:700},children:t.locationName}),s.jsxs("div",{style:MUTED,children:[a||"Address on request",l?` · ${l}`:"",t.workingDays?` · ${WD(t.workingDays)}`:""]})]})]})})}
function PassCard({pass:t,onUse:a,onReceipt:l,onCancel:cc}){const d=PS(t),n=CR(t),h=UD(t,n),i=t.passType===1,o=i?n-h:0;return s.jsxs("div",{className:"choice-card",style:{cursor:"default",opacity:d.live?1:.62},children:[s.jsxs("div",{style:{display:"flex",alignItems:"flex-start",justifyContent:"space-between",gap:"var(--space-2)"},children:[s.jsx("div",{className:"choice-card__title",children:t.planName}),s.jsx("div",{style:{flex:"none"},children:s.jsx(z,{tone:d.tone,children:d.label})})]}),s.jsx("div",{className:"choice-card__meta",children:t.passNumber}),i?s.jsxs(s.Fragment,{children:[s.jsx(Dots,{total:n,used:h}),s.jsxs("div",{className:"choice-card__meta",children:[h," of ",n," used · ",s.jsxs("strong",{style:{color:"var(--text-primary)"},children:[o," remaining"]})]})]}):null,s.jsxs("div",{className:"choice-card__meta",children:["Purchased ",f(t.purchaseUtc)," · valid until ",f(t.validTo)]}),s.jsxs("div",{style:{marginTop:"var(--space-3)",display:"flex",gap:"var(--space-2)",flexWrap:"wrap"},children:[d.live?s.jsxs(g,{size:"sm",onClick:()=>a(t),children:[s.jsx(T,{size:14})," ",i?"Use a Day":"Access"]}):s.jsx(g,{size:"sm",variant:"outline",onClick:()=>l(t),children:"View receipt"}),d.live&&cc?s.jsx(g,{size:"sm",variant:"ghost",onClick:()=>cc(t),children:"Cancel"}):null,d.live&&!RF(t)?s.jsx("span",{style:{...MUTED,alignSelf:"center"},children:"Started — cancelling now refunds nothing."}):null,!d.live&&t.status===3&&i?s.jsx("span",{style:{...MUTED,alignSelf:"center"},children:"Unused credits forfeited at expiry"}):null]})]})}
function PassGroup({title:t,passes:a,note:l,empty:o,onUse:d,onReceipt:n,onCancel:cg}){return s.jsxs("div",{children:[s.jsxs("div",{style:{display:"flex",alignItems:"center",gap:"var(--space-2)",marginBottom:"var(--space-2)"},children:[s.jsx("h3",{style:{fontFamily:"var(--font-display)",fontSize:"var(--font-size-md)",fontWeight:700,margin:0},children:t}),s.jsx(z,{tone:"neutral",children:a.length})]}),a.length&&l?s.jsx("div",{style:{...MUTED,marginBottom:"var(--space-3)"},children:l}):null,a.length?s.jsx("div",{className:"choice-grid",children:a.map(h=>s.jsx(PassCard,{pass:h,onUse:d,onReceipt:n,onCancel:cg},h.customerPassId))}):s.jsx("div",{style:MUTED,children:o})]})}
function k({reloadKey:t=0,onBuyMore:a,onChanged:oc}={}){const l=v(),d=U0(),[i,o]=r.useState(null),[c,e]=r.useState(!0),[j,p]=r.useState(null),[CXL,setCXL]=r.useState(null),m=r.useCallback(async()=>{e(!0);try{o(await x.my({page:1,pageSize:100}))}catch(u){l.error(u instanceof w?u.message:"Failed to load passes.")}finally{e(!1)}},[t]);r.useEffect(()=>{m()},[m]);const doCancel=async()=>{if(!CXL)return;try{const rf=RF(CXL);await x.cancel(CXL.customerPassId);l.success(rf?`${CXL.passNumber} cancelled — ${y(CXL.totalAmount)} refunded to your wallet.`:`${CXL.passNumber} cancelled.`);await m();oc?.()}catch(u){l.error(u instanceof w&&u.status===403?"Your account cannot cancel passes yet. Please ask the front desk to cancel this one.":u instanceof w?u.message:"Could not cancel this pass.")}};const C=i?.items??[],BY=(u,P)=>new Date(u.validTo)-new Date(P.validTo),CUR=C.filter(u=>PS(u).live).sort(BY),CAN=C.filter(u=>!PS(u).live&&u.status===4).sort((u,P)=>BY(P,u)),FIN=C.filter(u=>!PS(u).live&&u.status!==4).sort((u,P)=>BY(P,u)),O=C.filter(u=>u.passType===1&&PS(u).live),R=O.reduce((u,P)=>u+(CR(P)-UD(P,CR(P))),0),V=O.length?O.slice().sort((u,P)=>new Date(u.validTo)-new Date(P.validTo))[0]:null;return s.jsxs(s.Fragment,{children:[c&&!i?s.jsx(I,{center:!0}):C.length===0?s.jsx(b,{children:s.jsxs("div",{children:[s.jsx("p",{style:{color:"var(--text-secondary)"},children:"You have not bought a pass yet. A day pass runs from the start date you pick — a single day, or a pack of consecutive days."}),a?s.jsx("div",{style:{marginTop:"var(--space-3)"},children:s.jsx(g,{size:"sm",onClick:a,children:"Buy a day pass"})}):null]})}):s.jsxs(s.Fragment,{children:[s.jsxs("div",{style:{display:"flex",flexDirection:"column",gap:"var(--space-5)"},children:[s.jsx(PassGroup,{title:"Current",passes:CUR,empty:"Nothing active right now.",onUse:p,onReceipt:()=>d("/invoices"),onCancel:setCXL}),s.jsx(PassGroup,{title:"Cancelled",passes:CAN,empty:"No cancelled passes.",onUse:p,onReceipt:()=>d("/invoices")}),s.jsx(PassGroup,{title:"Completed",passes:FIN,note:"Used up, or past their validity date.",empty:"Nothing here yet — a pass moves down once its credits run out or its validity date passes.",onUse:p,onReceipt:()=>d("/invoices")})]}),(i?.totalCount??0)>C.length?s.jsxs("div",{style:{...MUTED,marginTop:"var(--space-3)",textAlign:"right"},children:["Showing your ",C.length," most recent passes of ",i.totalCount,"."]}):null]}),s.jsx(CD,{open:CXL!==null,title:"Cancel this pass?",danger:!0,confirmLabel:"Cancel pass",message:CXMSG(CXL),onConfirm:doCancel,onClose:()=>setCXL(null)}),s.jsx(N,{open:j!==null,title:j?.passType===1?"Use a day credit":`Access — ${j?.passNumber??""}`,onClose:()=>p(null),children:s.jsxs("div",{style:{textAlign:"center",padding:"var(--space-4) 0"},children:[s.jsx(S,{size:44,color:"var(--regalis-gold-dark)"}),s.jsxs("p",{style:{marginTop:"var(--space-3)",color:"var(--text-secondary)"},children:["Show ",s.jsx("strong",{children:j?.passNumber})," at the desk to check in for today. One credit is deducted on check-in, not now."]}),s.jsxs("p",{style:{marginTop:"var(--space-2)",...MUTED},children:["Your QR access pass lives under ",s.jsx("strong",{children:"My QR / Access"}),", which opens with the access-control module."]})]})})]})}
function Z({passType:t=1}){const a=v(),l=U0(),[d,n]=r.useState(t),[h,i]=r.useState(null),[o,c]=r.useState(null),[e,j]=r.useState(!1),[p,m]=r.useState(0),[C,O]=r.useState(0),[R,V]=r.useState(null),[u,P]=r.useState(null),[K,Y]=r.useState(""),[G,q]=r.useState(null),[RGdesk,RGsetDesk]=r.useState(null),[RGav,RGsetAv]=r.useState({status:"idle"}),H=r.useRef(null);r.useEffect(()=>{n(t)},[t]),r.useEffect(()=>{let X=!1;return i(null),c(null),j(!1),x.activePlans(d).then(M=>{X||i(M)}).catch(()=>{X||(j(!0),a.error("Failed to load plans."))}),()=>{X=!0}},[d,p]),r.useEffect(()=>{RGwsTypes.list().then(M=>{const Q=Array.isArray(M)?M:M?.items??[];const J=Q.find(oe=>String(oe.typeCode).toUpperCase()==="DESK");RGsetDesk(J?J.workspaceTypeId:null)}).catch(()=>{}),_.me().then(V).catch(()=>{}),L0.list({}).then(M=>{const Q=Array.isArray(M)?M:M?.items??[];P(Q.find(J=>J.isDefault&&J.isActive)??Q.find(J=>J.isActive)??Q[0]??null)}).catch(()=>{})},[]);r.useEffect(()=>{if(d!==1||!o||!K||!u||!RGdesk){RGsetAv({status:"idle"});return}let dead=!1;RGsetAv({status:"checking"});RGavail.slots(u.locationId,RGdesk,K).then(M=>{if(dead)return;const Q=Array.isArray(M)?M:[];if(Q.length===0){RGsetAv({status:"closed"});return}RGsetAv(Q.some(J=>J.isAvailable)?{status:"ok"}:{status:"full"})}).catch(()=>{if(!dead)RGsetAv({status:"error"})});return()=>{dead=!0}},[d,o,K,u,RGdesk]);const RGready=!(d===1)||(K!==""&&RGav.status==="ok");const W=B[d]??"Passes",ee=d===1,te=o?CR(o):0,ae=o?(o.price??0)/Math.max(1,te):0,se=R!==null&&o!==null&&R.balance>=o.totalAmount,ne=()=>H.current?.scrollIntoView({behavior:"smooth",block:"start"}),re=async X=>{if(!o)return;if(ee&&!K){a.error("Pick your first visit date before paying.");return}if(ee&&RGav.status!=="ok"){a.error("We need a free desk on your first visit date before you can pay.");return}q(X);const M=K||new Date().toISOString().slice(0,10);try{if(X==="wallet"){const Q=await x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:"wallet"});a.success(`Pass ${Q.pass.passNumber} activated.`)}else{const Q=await x.order(o.passPlanId),J=await E.sandboxComplete(Q.gatewayOrderId),oe=await x.purchase({passPlanId:o.passPlanId,validFrom:M,paymentMethod:"gateway",gatewayOrderId:Q.gatewayOrderId,gatewayPaymentId:J.gatewayPaymentId,gatewaySignature:J.gatewaySignature});a.success(`Pass ${oe.pass.passNumber} activated.`)}c(null),Y(""),O(Q=>Q+1),_.me().then(V).catch(()=>{})}catch(Q){a.error(Q instanceof w?Q.message:"Purchase failed.")}finally{q(null)}};return s.jsxs(s.Fragment,{children:[s.jsx(A,{title:"Passes",description:ee?"Walk-in access to the office — pick a start date and the pass runs from there, one day or a run of consecutive days. No booking needed.":"A pass carries its own QR access valid for the whole window — no per-visit booking needed."}),s.jsx("div",{className:"passtabs",role:"tablist","aria-label":"Pass type",children:$.filter(X=>X.value===1).map(X=>{const MB=X.value===2;return s.jsxs("button",{type:"button",role:"tab","aria-selected":!MB&&d===X.value,className:`passtabs__tab${!MB&&d===X.value?" passtabs__tab--active":""}`,onClick:()=>MB?l("/membership"):n(X.value),children:[s.jsx(S,{size:15,"aria-hidden":!0})," ",MB?"Membership":X.label]},X.value)})}),s.jsxs("div",{ref:H,style:{marginTop:"var(--space-5)"},children:[s.jsx("h2",{style:{fontFamily:"var(--font-display)",fontSize:"var(--font-size-lg)",fontWeight:700,marginBottom:"var(--space-3)"},children:ee?"Buy day credits":`Buy a ${W.toLowerCase().replace(/es$/,"")}`}),s.jsxs("div",{children:[s.jsxs("div",{children:[h?h.length===0?s.jsx(b,{children:s.jsxs("p",{style:{color:"var(--text-secondary)"},children:["No ",W.toLowerCase()," plans are on sale right now. Please check back soon."]})}):s.jsx("div",{className:"choice-grid",children:h.map(X=>{const M=CR(X),Q=(X.price??0)/Math.max(1,M),J=o?.passPlanId===X.passPlanId;return s.jsxs("button",{type:"button",className:`choice-card${J?" choice-card--selected":""}`,style:{position:"relative"},onClick:()=>c(X),children:[ee&&M===POP?s.jsx("div",{style:PILL,children:s.jsx(z,{tone:"gold",children:"Most Popular"})}):null,s.jsx("div",{className:"choice-card__title",children:X.planName}),s.jsxs("div",{className:"choice-card__meta",children:[X.validityDays,ee?" consecutive day":" day",X.validityDays>1?"s":"",ee?"":" validity"]}),X.description?s.jsx("div",{className:"choice-card__meta",children:X.description}):null,s.jsxs("div",{className:"choice-card__price",children:[y(X.totalAmount)," ",s.jsx("span",{style:{fontWeight:400,color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:"incl. GST"})]}),ee?s.jsxs("div",{style:MUTED,children:[y(Q)," per day before GST"]}):null]},X.passPlanId)})}):e?s.jsx(b,{children:s.jsxs("div",{children:[s.jsx("p",{style:{color:"var(--text-secondary)"},children:"We could not load the plans just now."}),s.jsx("div",{style:{marginTop:"var(--space-3)"},children:s.jsx(g,{size:"sm",variant:"outline",onClick:()=>m(X=>X+1),children:"Try again"})})]})}):s.jsx(I,{center:!0}),o?s.jsx("div",{style:{marginTop:"var(--space-4)"},children:s.jsx(b,{title:ee?"First visit date":"Starts on",children:s.jsxs("div",{className:"form-stack",children:[s.jsx(D,{label:ee?"First visit date":"Starts on",type:"date",required:ee,value:K,min:new Date().toISOString().slice(0,10),hint:ee?(Number(o.validityDays)===1?"Pick the day you will come in. We check a desk is free before you pay.":`The pass runs for ${o.validityDays} consecutive days from this date. We check a desk is free on day one before you pay.`):void 0,onChange:X=>Y(X.target.value)}),ee?RGavailPanel({date:K,state:RGav,plan:o,location:u,days:te}):null,ee?s.jsx("div",{style:MUTED,children:Number(o.validityDays)>1?`Covers ${o.validityDays} consecutive days from your start date — one day credit per day.`:"Covers the single day you pick."}):null]})})}):null]}),o?s.jsxs("div",{style:{marginTop:"var(--space-4)"},children:[s.jsxs("div",{className:"pay-options",children:[s.jsxs(g,{variant:se?"primary":"outline",disabled:!se||G!==null||!RGready,loading:G==="wallet",onClick:()=>re("wallet"),children:["Wallet ",R&&`(${y(R.balance)})`]}),s.jsxs(g,{disabled:G!==null||!RGready,loading:G==="gateway",onClick:()=>re("gateway"),children:["Pay online ",y(o.totalAmount)]})]}),ee?s.jsx("div",{style:MUTED,children:"Credits are deducted when you check in at the office, not when you buy."}):null]}):null]})]}),s.jsxs("div",{style:{marginTop:"var(--space-4)"},children:[s.jsx("h2",{style:{fontFamily:"var(--font-display)",fontSize:"var(--font-size-lg)",fontWeight:700,marginBottom:"var(--space-3)"},children:"Your passes"}),s.jsx(k,{reloadKey:C,onBuyMore:ne,onChanged:()=>{_.me().then(V).catch(()=>{})}})]})]})}
function J(){return s.jsxs(s.Fragment,{children:[s.jsx(A,{title:"My Passes",description:"Day and monthly passes you have purchased."}),s.jsx(k,{})]})}
export{k as MyPassesCard,J as MyPassesPage,Z as PassPlansPage};
