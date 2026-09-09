import{a as ue,r as a,m as ye,y as ge,z as xe,D as fe,E as je,A as y,j as e,I as ee,F as ve,G as te,p as h,W as ke,H as we,Q as be,g as Se,J as Be}from"./index-DI5X4c9f.js";
import{a as ae,i as Ne,b as k,p as se}from"./bookings-DrKATIWm.js";
import{C}from"./Card-CNXipofE.js";
import{E}from"./EmptyState-B4UbbVj4.js";
import{P as Ie}from"./PageHeader-Y96MMVry.js";
import{D as Ce}from"./bookings-CeZsOhV5.js";
import{a as L,b as p,c as DY,f as O}from"./formatters-DFe5xCRi.js";
import{C as Te}from"./circle-check-DvPmllJi.js";
import{D as ze}from"./download-Dlifs1Q2.js";

const MAX_RANGE_DAYS=31;

function ne(o,T){return(o.find(x=>x.locationId===T)??o.find(x=>x.isDefault)??o[0]).locationId}

/* Date maths runs in UTC over the plain YYYY-MM-DD the date inputs produce, so a
   range never slides by a day regardless of the browser timezone. */
function dms(iso){const[Y,M,D]=iso.split("-").map(Number);return Date.UTC(Y,M-1,D)}
function countDays(from,to){return Math.round((dms(to)-dms(from))/864e5)+1}
function eachDay(from,to){const out=[],end=dms(to);for(let t=dms(from);t<=end;t+=864e5)out.push(new Date(t).toISOString().slice(0,10));return out}
function parseUtc(v){return v instanceof Date?v:new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(v)?v:`${v}Z`)}
function dayLabel(iso){return DY(`${iso}T12:00:00Z`)}

/* A whole day is the working window the location itself reports for that date,
   read back from the slots API rather than computed here, so the IST boundaries
   stay owned by the server (Design Invariant 1 - one calendar). */
function windowFromSlots(date,slots){
  if(!slots||slots.length===0)return{date,closed:!0};
  const first=parseUtc(slots[0].startUtc).getTime();
  const last=parseUtc(slots[slots.length-1].startUtc).getTime();
  const gran=slots.length>1?Math.round((parseUtc(slots[1].startUtc).getTime()-first)/6e4):30;
  return{date,closed:!1,startUtc:slots[0].startUtc,minutes:Math.round((last-first)/6e4)+gran}
}

function Ee(){
  const o=ue();
  const[T,R]=a.useState([]);
  const[g,x]=a.useState([]);
  const[c,oe]=a.useState(null);
  const[f,r]=a.useState("when");
  const z=ye(t=>t.locationId);
  const[j,M]=a.useState("");
  const[v,Q]=a.useState("");
  const w=new Date().toISOString().slice(0,10);
  const[b,ie]=a.useState(w);
  const[A,q]=a.useState(w);
  const W=b===A;
  const nDays=b&&A&&A>=b?countDays(b,A):0;
  const[D,re]=a.useState(null);
  const[le,G]=a.useState(!1);
  const[i,H]=a.useState(null);
  const[m,ce]=a.useState(60);
  const[_,Y]=a.useState(null);
  const[d,$]=a.useState(null);
  const[n,J]=a.useState(null);
  const[S,N]=a.useState(null);
  const[l,F]=a.useState(null);
  const[plan,setPlan]=a.useState(null);
  const[busy,setBusy]=a.useState(!1);
  const[multi,setMulti]=a.useState(null);
  const K=g.find(t=>String(t.locationId)===j)??null;

  a.useEffect(()=>{g.length!==0&&M(String(ne(g,z)))},[z,g]);
  a.useEffect(()=>{
    Promise.all([ge.list(),xe.list({pageSize:100})]).then(([t,s])=>{
      R(t);const u=s.items.filter(B=>B.isActive);x(u);u.length>0&&M(String(ne(u,z)))
    }).catch(()=>o.error("Failed to load booking options."));
    fe(je.get("/wallet/me")).then(oe).catch(()=>{})
  },[]);

  /* Slots are fetched only once BOTH a date and a workspace type are chosen. */
  const I=a.useCallback(async()=>{
    if(!j||!v||!b||!W){re(null);return}
    G(!0);H(null);
    try{re(await ae.slots(Number(j),Number(v),b))}
    catch(t){o.error(t instanceof y?t.message:"Failed to load slots.")}
    finally{G(!1)}
  },[j,v,b,W]);
  a.useEffect(()=>{I()},[I]);
  a.useEffect(()=>{setPlan(null);Y(null);$(null);J(null)},[b,A,v,j]);

  /* Builds the whole-day window for every date in the range and keeps only the
     workspaces that are free on every one of them. */
  const planRange=async()=>{
    const dates=eachDay(b,A);
    const slotSets=await Promise.all(dates.map(t=>ae.slots(Number(j),Number(v),t)));
    const windows=dates.map((t,ix)=>windowFromSlots(t,slotSets[ix]));
    const closed=windows.filter(t=>t.closed);
    if(closed.length>0){
      const names=closed.map(t=>dayLabel(t.date)).join(", ");
      throw new Error(`${K?K.locationName:"This location"} is closed on ${names}. Pick a range without ${closed.length===1?"that date":"those dates"}.`)
    }
    const perDay=await Promise.all(windows.map(t=>ae.workspaces(Number(j),Number(v),t.startUtc,t.minutes)));
    const seen=new Map();
    perDay.forEach((list,ix)=>{
      (list||[]).filter(u=>u.isAvailable!==!1).forEach(u=>{
        const cur=seen.get(u.workspaceId)??{ws:u,days:new Set()};
        cur.days.add(windows[ix].date);seen.set(u.workspaceId,cur)
      })
    });
    const free=[...seen.values()].filter(t=>t.days.size===windows.length).map(t=>t.ws);
    return{windows,free}
  };

  const de=async()=>{
    if(!v){o.error("Pick a workspace type first.");return}
    if(A<b){o.error("The To date cannot be before the From date.");return}
    if(nDays>MAX_RANGE_DAYS){o.error(`Ranges are limited to ${MAX_RANGE_DAYS} days. Buy a monthly pass for a longer stay.`);return}
    if(W){
      if(!i)return;
      r("workspace");Y(null);$(null);
      try{Y(await ae.workspaces(Number(j),Number(v),i,m))}
      catch(t){o.error(t instanceof y?t.message:"Failed to load workspaces."),r("when")}
      return
    }
    setBusy(!0);
    try{
      const res=await planRange();
      setPlan(res.windows);Y(res.free);$(null);r("workspace")
    }catch(t){o.error(t instanceof y?t.message:t.message||"Failed to check availability.")}
    finally{setBusy(!1)}
  };

  /* Every amount on screen comes from the server quote - one per booking day - so
     the summary can never disagree with what payment actually charges. */
  const he=async t=>{
    $(t);J(null);
    if(W){
      try{J(await k.quote(t.workspaceId,i,m));r("pay")}
      catch(s){o.error(s instanceof y?s.message:"Could not price the booking."),$(null)}
      return
    }
    setBusy(!0);
    try{
      const quotes=[];
      for(const win of plan)quotes.push(await k.quote(t.workspaceId,win.startUtc,win.minutes));
      const sum=u=>quotes.reduce((acc,qq)=>acc+(qq[u]??0),0);
      const first=quotes[0];
      const uniform=quotes.every(qq=>qq.totalAmount===first.totalAmount);
      J({multi:!0,quotes,days:quotes.length,perDayTotal:uniform?first.totalAmount:null,
        perDayMinutes:plan[0].minutes,workspaceName:first.workspaceName,
        cgstPercent:first.cgstPercent,sgstPercent:first.sgstPercent,
        baseAmount:sum("baseAmount"),cgstAmount:sum("cgstAmount"),
        sgstAmount:sum("sgstAmount"),totalAmount:sum("totalAmount")});
      r("pay")
    }catch(s){o.error(s instanceof y?s.message:"Could not price the booking."),$(null)}
    finally{setBusy(!1)}
  };

  const V=async t=>t instanceof y&&t.status===409?(o.warning("That slot was just taken. Availability has been refreshed."),r("when"),await I(),!0):!1;

  const settle=async(bookingId,method)=>{
    if(method==="wallet")return k.confirm(bookingId,{paymentMethod:"wallet"});
    const order=await se.createOrder(bookingId);
    const paid=await se.sandboxComplete(order.gatewayOrderId);
    return k.confirm(bookingId,{paymentMethod:"gateway",gatewayOrderId:order.gatewayOrderId,gatewayPaymentId:paid.gatewayPaymentId,gatewaySignature:paid.gatewaySignature})
  };

  const paySingle=async method=>{
    if(!d||!i)return;
    N(method);
    try{const t=await k.hold(d.workspaceId,i,m);F(await settle(t.bookingId,method));r("done")}
    catch(t){await V(t)||o.error(t instanceof y?t.message:"Payment failed.")}
    finally{N(null)}
  };

  /* Multi-day: take every hold FIRST, so a range that cannot be fully booked is
     abandoned before any money moves. Only then settle them one at a time. */
  const payMulti=async method=>{
    if(!d||!plan)return;
    N(method);
    const holds=[];
    const release=async list=>{for(const held of list){try{await Be.cancel(held.bookingId,"Multi-day booking could not be completed")}catch(err){}}};
    try{
      for(let ix=0;ix<plan.length;ix++){
        const win=plan[ix];
        holds.push(await k.hold(d.workspaceId,win.startUtc,win.minutes,`Whole-day booking ${dayLabel(b)} to ${dayLabel(A)} (day ${ix+1} of ${plan.length})`))
      }
    }catch(t){
      await release(holds);
      await V(t)||o.error(t instanceof y?t.message:"Could not hold every day in the range. Nothing was charged.");
      N(null);return
    }
    const done=[];
    try{
      for(const held of holds)done.push(await settle(held.bookingId,method))
    }catch(t){
      await release(holds.slice(done.length));
      if(done.length===0){await V(t)||o.error(t instanceof y?t.message:"Payment failed. Nothing was charged.");N(null);return}
      o.warning(`${done.length} of ${holds.length} days were booked. The remaining days were released - please book them again.`);
      setMulti(done);r("done");N(null);return
    }
    setMulti(done);r("done");N(null)
  };

  const X=[{key:"when",label:"1 · Date, type & time"},{key:"workspace",label:"2 · Pick a workspace"},{key:"pay",label:"3 · Review & pay"},{key:"done",label:"4 · Confirmed"}];
  const Z=X.findIndex(t=>t.key===f);
  const U=a.useMemo(()=>c!==null&&n!==null&&c.balance>=n.totalAmount,[c,n]);
  const dayRateOf=t=>plan&&plan.length>0?t.pricePerHour*plan[0].minutes/60:null;
  const canContinue=!!v&&A>=b&&nDays>0&&(W?!!i:!0);
  const reset=()=>{F(null);setMulti(null);setPlan(null);Y(null);$(null);J(null);r("when");I()};

  return e.jsxs(e.Fragment,{children:[
    e.jsx(Ie,{title:"Book a Workspace",description:K?`Live availability at ${K.locationName} — the calendar you see is the calendar the front desk sees.`:"Live availability — the calendar you see is the calendar the front desk sees."}),
    e.jsx("div",{className:"wizard-steps",children:X.map((t,s)=>e.jsx("span",{className:`wizard-step${s===Z?" wizard-step--active":""}${s<Z?" wizard-step--done":""}`,children:t.label},t.key))}),

    f==="when"&&e.jsx(C,{title:"What, where and when",children:e.jsxs("div",{className:"form-stack",children:[
      e.jsxs("div",{className:"toolbar toolbar--when",style:{marginBottom:0},children:[
        e.jsx(ee,{label:"From date",type:"date",value:b,min:w,onChange:t=>{const s=t.target.value;ie(s);s>A&&q(s)}}),
        e.jsx(ee,{label:"To date",type:"date",value:A,min:b||w,onChange:t=>q(t.target.value)}),
        W&&e.jsx(ve,{label:"Duration",value:String(m),options:Ce.map(t=>({value:t,label:t<60?`${t} min`:`${t/60} hour${t>60?"s":""}`})),onChange:t=>ce(Number(t.target.value))})
      ]}),
      e.jsx("p",{className:"choice-card__meta",style:{margin:0},children:nDays>0?`${nDays} day${nDays===1?"":"s"}${W?" · pick a start time below":" · booked as a whole day on each date"}`:"The To date cannot be before the From date."}),
      e.jsx("div",{className:"field__label",children:"Workspace"}),
      e.jsx("div",{className:"choice-grid",children:T.map(t=>e.jsxs("button",{type:"button",className:`choice-card${v===String(t.workspaceTypeId)?" choice-card--selected":""}`,onClick:()=>Q(String(t.workspaceTypeId)),children:[
        e.jsx("div",{className:"choice-card__title",children:t.typeName}),
        e.jsxs("div",{className:"choice-card__meta",children:[t.workspaceCount," workspace",t.workspaceCount===1?"":"s"]})
      ]},t.workspaceTypeId))}),

      !v?e.jsx(E,{title:"Pick a workspace type",description:"Choose Desk, Cabin, Meeting Room or Virtual Workspace to see live availability for the dates above."})
        :W?(le?e.jsx(te,{center:!0})
          :!D||D.length===0?e.jsx(E,{title:"Closed on this date",description:"Pick another date — the location has no working hours that day."})
          :e.jsxs("div",{children:[
            e.jsx("div",{className:"field__label",style:{marginBottom:"var(--space-2)"},children:"Start time (IST)"}),
            e.jsx("div",{className:"slot-grid",children:D.map(t=>e.jsx("button",{type:"button",disabled:!t.isAvailable||parseUtc(t.startUtc).getTime()<=Date.now(),className:`slot-chip${i===t.startUtc?" slot-chip--selected":""}`,onClick:()=>H(t.startUtc),children:L(t.startUtc)},t.startUtc))})
          ]})
        ):e.jsx(E,{title:`Whole-day booking · ${nDays} days`,description:`${dayLabel(b)} to ${dayLabel(A)} — both dates included. Every date is booked for the full working day, and the range is only offered if the same workspace is free on all ${nDays} days.`}),

      e.jsx(h,{size:"lg",disabled:!canContinue||busy,loading:busy,onClick:de,children:"Find available workspaces"})
    ]})}),

    f==="workspace"&&e.jsx(C,{title:W?`Available ${O(i)} · ${m} min`:`Available all ${nDays} days · ${dayLabel(b)} to ${dayLabel(A)}`,actions:e.jsx(h,{variant:"ghost",onClick:()=>r("when"),children:"Change dates"}),children:_?_.length===0?e.jsx(E,{title:W?"Nothing free in that window":"Nothing free for the whole range",description:W?"Try a different time, duration or workspace type.":"No workspace of this type is free on every date. Try a shorter range or another workspace type."}):e.jsx("div",{className:"choice-grid",children:_.map(t=>e.jsxs("button",{type:"button",className:`choice-card${d?.workspaceId===t.workspaceId?" choice-card--selected":""}`,onClick:()=>he(t),disabled:busy,children:[
      e.jsx("div",{className:"choice-card__title",children:t.workspaceName}),
      e.jsxs("div",{className:"choice-card__meta",children:[t.workspaceCode,t.floorNumber?` · Floor ${t.floorNumber}`:""," · seats"," ",t.capacity]}),
      t.amenities.length>0&&e.jsx("div",{className:"choice-card__meta",children:t.amenities.join(" · ")}),
      e.jsxs("div",{className:"choice-card__price",children:[W?p(t.pricePerHour):p(dayRateOf(t)??0),W?"/hour":"/day"]})
    ]},t.workspaceId))}):e.jsx(te,{center:!0})}),

    f==="pay"&&n&&d&&e.jsx(C,{title:"Review & pay",actions:e.jsx(h,{variant:"ghost",onClick:()=>r("workspace"),children:"Back"}),children:e.jsxs("div",{className:"form-stack",children:[
      e.jsxs("div",{className:"summary-rows",children:[
        e.jsxs("div",{children:[e.jsx("span",{children:"Workspace"}),e.jsxs("strong",{children:[n.workspaceName," (",d.workspaceCode,")"]})]}),
        e.jsxs("div",{children:[e.jsx("span",{children:"Location"}),e.jsx("strong",{children:K?K.locationName:"—"})]}),
        n.multi?e.jsxs(e.Fragment,{children:[
          e.jsxs("div",{children:[e.jsx("span",{children:"From date"}),e.jsx("strong",{children:dayLabel(b)})]}),
          e.jsxs("div",{children:[e.jsx("span",{children:"To date"}),e.jsx("strong",{children:dayLabel(A)})]}),
          e.jsxs("div",{children:[e.jsx("span",{children:"Number of days"}),e.jsxs("strong",{children:[n.days," day",n.days===1?"":"s"]})]}),
          e.jsxs("div",{children:[e.jsx("span",{children:"Rate per day"}),e.jsx("strong",{children:n.perDayTotal!==null?`${p(n.perDayTotal)} · ${n.perDayMinutes} min`:"varies by day"})]})
        ]}):e.jsxs(e.Fragment,{children:[
          e.jsxs("div",{children:[e.jsx("span",{children:"When"}),e.jsxs("strong",{children:[O(n.startUtc)," – ",L(n.endUtc)]})]}),
          e.jsxs("div",{children:[e.jsx("span",{children:"Duration"}),e.jsxs("strong",{children:[n.durationMinutes," minutes"]})]})
        ]}),
        e.jsxs("div",{children:[e.jsx("span",{children:"Base amount"}),e.jsx("span",{children:p(n.baseAmount)})]}),
        e.jsxs("div",{children:[e.jsxs("span",{children:["CGST @ ",n.cgstPercent,"%"]}),e.jsx("span",{children:p(n.cgstAmount)})]}),
        e.jsxs("div",{children:[e.jsxs("span",{children:["SGST @ ",n.sgstPercent,"%"]}),e.jsx("span",{children:p(n.sgstAmount)})]}),
        e.jsxs("div",{className:"summary-total",children:[e.jsx("span",{children:"Total"}),e.jsx("span",{children:p(n.totalAmount)})]})
      ]}),
      n.multi&&e.jsx("div",{className:"summary-rows",children:n.quotes.map((qq,ix)=>e.jsxs("div",{children:[e.jsx("span",{children:dayLabel(plan[ix].date)}),e.jsxs("span",{children:[L(qq.startUtc)," – ",L(qq.endUtc)," · ",p(qq.totalAmount)]})]},plan[ix].date))}),
      e.jsxs("div",{className:"pay-options",children:[
        e.jsxs(h,{size:"lg",variant:U?"primary":"outline",disabled:!U||S!==null,loading:S==="wallet",onClick:()=>(W?paySingle:payMulti)("wallet"),children:[e.jsx(ke,{size:18})," Wallet",c&&` (${p(c.balance)})`]}),
        e.jsxs(h,{size:"lg",variant:"primary",disabled:S!==null,loading:S==="gateway",onClick:()=>(W?paySingle:payMulti)("gateway"),children:[e.jsx(we,{size:18})," Pay online"]})
      ]}),
      !U&&c&&e.jsxs("p",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:["Wallet balance ",p(c.balance)," doesn't cover this booking — pay online or top up from the Wallet page."]}),
      e.jsx("p",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:n.multi?`Each of the ${n.days} days is held for 10 minutes while payment completes. If any day cannot be held, the whole range is released and nothing is charged.`:"The slot is held for 10 minutes while payment completes."})
    ]})}),

    f==="done"&&(l||multi)&&e.jsx(C,{children:e.jsxs("div",{style:{textAlign:"center",padding:"var(--space-6) 0"},children:[
      e.jsx(Te,{size:56,color:"var(--color-success)"}),
      e.jsx("h2",{style:{fontSize:"var(--font-size-2xl)",fontWeight:800,marginTop:"var(--space-3)"},children:multi?`${multi.length} day${multi.length===1?"":"s"} confirmed`:"Booking confirmed"}),
      l&&e.jsxs("p",{style:{color:"var(--text-secondary)",marginTop:"var(--space-2)"},children:[e.jsx("strong",{children:l.booking.bookingNumber})," · ",l.booking.workspaceName," ·"," ",O(l.booking.startUtc)," – ",L(l.booking.endUtc)]}),
      multi&&e.jsx("div",{className:"summary-rows",style:{marginTop:"var(--space-4)",textAlign:"left"},children:multi.map(t=>e.jsxs("div",{children:[e.jsx("span",{children:O(t.booking.startUtc)}),e.jsxs("strong",{children:[t.booking.bookingNumber," · ",L(t.booking.startUtc)," – ",L(t.booking.endUtc)]})]},t.booking.bookingNumber))}),
      e.jsxs("p",{style:{color:"var(--text-muted)",marginTop:"var(--space-2)",fontSize:"var(--font-size-sm)"},children:[e.jsx(be,{size:16,style:{verticalAlign:"text-bottom"}})," Your QR access pass is ready — find it under ",e.jsx(Se,{to:"/qr",children:"My QR / Access"})," when the access module opens."]}),
      e.jsxs("div",{style:{display:"flex",gap:"var(--space-3)",justifyContent:"center",marginTop:"var(--space-6)",flexWrap:"wrap"},children:[
        l&&e.jsxs(h,{variant:"outline",onClick:()=>Ne.downloadPdf(l.invoiceId,l.invoiceNumber),children:[e.jsx(ze,{size:16})," Invoice ",l.invoiceNumber]}),
        multi&&multi.map(t=>e.jsxs(h,{variant:"outline",onClick:()=>Ne.downloadPdf(t.invoiceId,t.invoiceNumber),children:[e.jsx(ze,{size:16})," ",t.invoiceNumber]},t.invoiceId)),
        e.jsx(h,{onClick:reset,children:"Book another"})
      ]})
    ]})})
  ]})
}

export{Ee as BookWorkspacePage};
