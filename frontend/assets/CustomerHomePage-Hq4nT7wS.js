/* Customer dashboard (home-month-1, 22 Oct 2026): "This month's bookings" replaces the
   single "Today's booking" card, and a "Customisation available" banner under it opens
   Book Workspace. Data: GET /bookings/my (upcoming + completed), filtered to the current
   IST month in the browser. */
import{i as k,l as N,m as w,r as c,n as B,j as e,T as b,C,Q as f,g as i,p as d,q as z,W as I,t as S,v as T}from"./index-DI5X4c9f.js";
import{d as P}from"./reports-CNtuzowc.js";
import{B as m}from"./Badge-BU5iAxex.js";
import{C as u}from"./Card-CNXipofE.js";
import{f as _,a as M,b as v}from"./formatters-DFe5xCRi.js";
import{g as U}from"./greeting-JECElu04.js";
import{C as A,a as W}from"./chevron-right-BikENMwC.js";
import{b as BK}from"./bookings-DrKATIWm.js";
import{B as SL,a as ST}from"./bookings-CeZsOhV5.js";

const E=[["path",{d:"M5 12h14",key:"1ays0h"}],["path",{d:"m12 5 7 7-7 7",key:"xquz4c"}]],L=k("arrow-right",E);
const R=[["path",{d:"M16 19h6",key:"xwg31i"}],["path",{d:"M16 2v4",key:"4m81vk"}],["path",{d:"M19 16v6",key:"tddt3s"}],["path",{d:"M21 12.598V6a2 2 0 0 0-2-2H5a2 2 0 0 0-2 2v14a2 2 0 0 0 2 2h8.5",key:"1glfrc"}],["path",{d:"M3 10h18",key:"8toen8"}],["path",{d:"M8 2v4",key:"1cmpym"}]],y=k("calendar-plus",R);
const SLD=[["line",{x1:"21",x2:"14",y1:"4",y2:"4",key:"s1"}],["line",{x1:"10",x2:"3",y1:"4",y2:"4",key:"s2"}],["line",{x1:"21",x2:"12",y1:"12",y2:"12",key:"s3"}],["line",{x1:"8",x2:"3",y1:"12",y2:"12",key:"s4"}],["line",{x1:"21",x2:"16",y1:"20",y2:"20",key:"s5"}],["line",{x1:"12",x2:"3",y1:"20",y2:"20",key:"s6"}],["line",{x1:"14",x2:"14",y1:"2",y2:"6",key:"s7"}],["line",{x1:"8",x2:"8",y1:"10",y2:"14",key:"s8"}],["line",{x1:"16",x2:"16",y1:"18",y2:"22",key:"s9"}]],Sliders=k("sliders-horizontal",SLD);

const $=[{to:"/book",icon:y,title:"Book a space",meta:"Desks, cabins and meeting rooms"},{to:"/day-pass",icon:b,title:"Day pass",meta:"Flexible access for a single day"},{to:"/membership",icon:C,title:"Membership",meta:"Unlimited access, one price"}];

// ---- IST date helpers (same parsing rule as formatters-*.js) --------------------------
const IST="Asia/Kolkata";
const ymdFmt=new Intl.DateTimeFormat("en-IN",{timeZone:IST,year:"numeric",month:"2-digit",day:"2-digit"});
const monthFmt=new Intl.DateTimeFormat("en-IN",{timeZone:IST,month:"long",year:"numeric"});
const dayFmt=new Intl.DateTimeFormat("en-IN",{timeZone:IST,day:"2-digit"});
const wdFmt=new Intl.DateTimeFormat("en-IN",{timeZone:IST,weekday:"short"});
const dmFmt=new Intl.DateTimeFormat("en-IN",{timeZone:IST,day:"2-digit",month:"short"});
function toDate(t){if(t instanceof Date)return t;const s=String(t);return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(s)?s:`${s}Z`)}
function ymd(dt){const o={};for(const p of ymdFmt.formatToParts(dt))o[p.type]=p.value;return `${o.year}-${o.month}-${o.day}`}

const MAX_ROWS=6;

// ---- This month's bookings ------------------------------------------------------------
function MonthBookings({dashboard:a}){
  const[rows,setRows]=c.useState(null);
  c.useEffect(()=>{
    let live=!0;
    const today=ymd(new Date()),month=today.slice(0,7);
    Promise.all([BK.my("upcoming",{page:1,pageSize:100}),BK.my("completed",{page:1,pageSize:100})]).then(([up,done])=>{
      if(!live)return;
      const seen=new Set,list=[];
      for(const o of[...up?.items??[],...done?.items??[]]){
        if(seen.has(o.bookingId))continue;seen.add(o.bookingId);
        const s=toDate(o.startUtc),en=toDate(o.endUtc),sd=ymd(s),ed=ymd(en);
        if(sd.slice(0,7)<=month&&ed.slice(0,7)>=month)list.push({...o,_s:s,_e:en,_sd:sd,_ed:ed,_today:sd<=today&&ed>=today});
      }
      list.sort((p,q2)=>p._s-q2._s);
      setRows(list);
    }).catch(()=>{live&&setRows([])});
    return()=>{live=!1};
  },[]);
  const n=rows?.length??0;
  return e.jsxs(u,{title:"This month's bookings",actions:e.jsx(i,{to:"/bookings",children:e.jsx(d,{size:"sm",variant:"ghost",children:"View all"})}),children:[
    e.jsxs("p",{style:{color:"var(--text-secondary)",fontSize:"var(--font-size-sm)",marginBottom:"var(--space-2)"},children:[monthFmt.format(new Date()),n>0?` · ${n} booking${n===1?"":"s"}`:""]}),
    rows===null?e.jsx("p",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-sm)",padding:"var(--space-4) 0"},children:"Loading your bookings…"})
    :n===0?e.jsxs("div",{className:"home__empty",children:[
        e.jsx("span",{className:"home__empty-icon",children:e.jsx(z,{size:22})}),
        e.jsx("div",{style:{fontWeight:700},children:"No bookings this month"}),
        e.jsx("p",{children:a?.nextBooking?`Next up: ${a.nextBooking.workspaceName}, ${_(a.nextBooking.startUtc)} IST`:"Nothing booked yet this month."}),
        e.jsx(i,{to:"/book",children:e.jsxs(d,{children:[e.jsx(y,{size:16})," Book now"]})})]})
    :e.jsxs(e.Fragment,{children:[
        e.jsx("ul",{style:{listStyle:"none",margin:0,padding:0},children:rows.slice(0,MAX_ROWS).map((o,idx)=>e.jsx(Row,{b:o,first:idx===0},o.bookingId))}),
        n>MAX_ROWS&&e.jsx(i,{to:"/bookings",style:{display:"inline-block",marginTop:"var(--space-3)",fontSize:"var(--font-size-sm)",fontWeight:600},children:`+${n-MAX_ROWS} more this month`})]})
  ]});
}

function Row({b:o,first}){
  const sameDay=o._sd===o._ed;
  const when=sameDay?`${M(o._s)} – ${M(o._e)} IST`:`${dmFmt.format(o._s)} – ${dmFmt.format(o._e)}${o.billingMonths?` · ${o.billingMonths}-month`:""}`;
  return e.jsxs("li",{style:{display:"flex",flexWrap:"wrap",gap:"var(--space-3)",alignItems:"center",padding:"var(--space-3) 0",borderTop:first?"none":"1px solid var(--border-subtle)"},children:[
    e.jsxs("div",{"aria-hidden":!0,style:{width:50,flex:"none",textAlign:"center",padding:"6px 0",borderRadius:"var(--radius-md)",background:o._today?"var(--regalis-gold)":"var(--surface-inset)",color:o._today?"var(--text-on-gold)":"var(--text-primary)"},children:[
      e.jsx("div",{style:{fontWeight:800,fontSize:"var(--font-size-lg)",lineHeight:1.1},children:dayFmt.format(o._s)}),
      e.jsx("div",{style:{fontSize:"var(--font-size-xs)",textTransform:"uppercase",letterSpacing:".04em",opacity:.85},children:wdFmt.format(o._s)})]}),
    e.jsxs("div",{style:{flex:"1 1 200px",minWidth:0},children:[
      e.jsxs("div",{style:{fontWeight:700,lineHeight:1.3},children:[o.workspaceName,e.jsxs("span",{style:{color:"var(--text-muted)",fontWeight:400},children:[" · ",o.workspaceTypeName]})]}),
      e.jsxs("div",{style:{color:"var(--text-secondary)",fontSize:"var(--font-size-sm)"},children:[o.bookingNumber," · ",when]})]}),
    e.jsxs("div",{style:{display:"flex",gap:"var(--space-2)",flexWrap:"wrap",justifyContent:"flex-end",marginInlineStart:"auto"},children:[
      o._today&&e.jsx(m,{tone:"info",children:"Today"}),
      e.jsx(m,{tone:ST[o.status]??"neutral",children:SL[o.status]??"Booked"})]})
  ]});
}

// ---- Customisation banner -> Book Workspace -------------------------------------------
const PROMO={display:"flex",alignItems:"center",gap:"var(--space-4)",flexWrap:"wrap",padding:"var(--space-5) var(--space-6)",borderRadius:"var(--radius-lg)",background:"radial-gradient(600px 240px at 90% -30%,rgb(var(--accent-rgb) / 32%),transparent),linear-gradient(120deg,var(--regalis-black),#1d1d24)",color:"var(--regalis-white)",boxShadow:"var(--shadow-md)",textDecoration:"none"};
function Promo(){
  return e.jsxs(i,{to:"/book",className:"home__promo",style:PROMO,"aria-label":"Customisation available: set up a workspace your way in Book Workspace",children:[
    e.jsx("span",{"aria-hidden":!0,style:{display:"inline-flex",alignItems:"center",justifyContent:"center",width:48,height:48,flex:"none",borderRadius:"var(--radius-full)",background:"rgb(var(--accent-rgb) / 18%)",color:"var(--accent-bright)"},children:e.jsx(Sliders,{size:22})}),
    e.jsxs("div",{style:{flex:1,minWidth:220},children:[
      e.jsx("div",{style:{fontSize:"var(--font-size-xs)",fontWeight:700,letterSpacing:".08em",textTransform:"uppercase",color:"var(--accent-bright)"},children:"Customisation available"}),
      e.jsx("div",{style:{fontFamily:"var(--font-display)",fontSize:"var(--font-size-lg)",fontWeight:800,marginTop:2},children:"Set up a workspace your way"}),
      e.jsx("p",{style:{marginTop:"var(--space-1)",color:"#ffffffd1",fontSize:"var(--font-size-sm)",maxWidth:"48ch",lineHeight:1.5},children:"Pick a desk, cabin or meeting room, choose your dates and hours, and tell us what else you need — we'll tailor the space for you."})]}),
    e.jsxs(d,{children:["Customise & book ",e.jsx(L,{size:16})]})
  ]});
}

// ---- Page ------------------------------------------------------------------------------
function H(){
  const{user:h}=N(),r=w(s=>s.locationId),[a,x]=c.useState(null),[j,t]=c.useState([]);
  c.useEffect(()=>{P.customer().then(x).catch(()=>{})},[]);
  c.useEffect(()=>{B.active(r??void 0).then(t).catch(()=>{})},[r]);
  return e.jsxs(e.Fragment,{children:[
    e.jsxs("header",{className:"home__greeting",children:[e.jsxs("h2",{children:[U(),", ",h?.fullName??"Guest"]}),e.jsx("p",{children:"Let's make today productive."})]}),
    e.jsx(q,{banners:j}),
    e.jsx("div",{className:"home__quick",children:$.map(({to:s,icon:p,title:n,meta:l})=>e.jsxs(i,{to:s,className:"home__tile",children:[e.jsx("span",{className:"home__tile-icon",children:e.jsx(p,{size:20})}),e.jsx("span",{className:"home__tile-title",children:n}),e.jsx("span",{className:"home__tile-meta",children:l}),e.jsx(L,{size:16,className:"home__tile-arrow","aria-hidden":!0})]},s))}),
    e.jsxs("div",{className:"home__columns",children:[
      e.jsxs("div",{className:"home__aside",children:[e.jsx(MonthBookings,{dashboard:a}),e.jsx(Promo,{})]}),
      e.jsxs("div",{className:"home__aside",children:[
        e.jsxs(u,{title:"Wallet balance",children:[e.jsx("div",{className:"home__balance",children:v(a?.walletBalance??0)}),e.jsx("p",{style:{color:"var(--text-secondary)",fontSize:"var(--font-size-sm)"},children:"Available credit for bookings, passes and services."}),e.jsx(i,{to:"/wallet",style:{display:"block",marginTop:"var(--space-4)"},children:e.jsxs(d,{variant:"outline",style:{width:"100%"},children:[e.jsx(I,{size:16})," Add money"]})})]}),
        e.jsx(u,{title:"At a glance",children:e.jsxs("ul",{className:"home__glance",children:[
          e.jsx("li",{children:e.jsxs(i,{to:"/passes",children:[e.jsx(b,{size:16})," Active passes",e.jsx(m,{tone:(a?.activePasses??0)>0?"info":"neutral",children:a?.activePasses??0})]})}),
          e.jsx("li",{children:e.jsxs(i,{to:"/notifications",children:[e.jsx(S,{size:16})," Unread notifications",e.jsx(m,{tone:(a?.unreadNotifications??0)>0?"warning":"neutral",children:a?.unreadNotifications??0})]})}),
          e.jsx("li",{children:e.jsxs(i,{to:"/support",children:[e.jsx(T,{size:16})," Open tickets",e.jsx(m,{tone:(a?.openTickets??0)>0?"warning":"neutral",children:a?.openTickets??0})]})})]})})
      ]})
    ]})
  ]});
}

// ---- Hero carousel (unchanged) ---------------------------------------------------------
function q({banners:h}){const[r,a]=c.useState(0),[x,j]=c.useState({}),t=h.length,o=c.useCallback(n=>a((n%t+t)%t),[t]);if(c.useEffect(()=>{if(t<2)return;const n=setInterval(()=>a(l=>(l+1)%t),2e3);return()=>clearInterval(n)},[t]),t===0)return e.jsx("section",{className:"hero",children:e.jsxs("div",{className:"hero__body",children:[e.jsx("h3",{className:"hero__title",children:"Your workspace, your success"}),e.jsx("p",{className:"hero__subtitle",children:"Premium desks, private cabins and meeting rooms — designed for focus and growth."}),e.jsx(i,{to:"/book",children:e.jsx(d,{children:"Book a space"})})]})});const s=h[Math.min(r,t-1)],p=!!s.imagePath&&!x[s.bannerId];return e.jsxs("section",{className:"hero","aria-roledescription":"carousel","aria-label":"Workspace highlights",children:[e.jsxs("div",{className:"hero__body",children:[e.jsx("h3",{className:"hero__title",children:s.title}),s.subtitle&&e.jsx("p",{className:"hero__subtitle",children:s.subtitle}),s.promotionalPrice!==null&&e.jsx("p",{className:"hero__price",children:v(s.promotionalPrice)}),e.jsx(i,{to:s.linkUrl?.startsWith("/")?s.linkUrl:"/book",children:e.jsx(d,{children:"Book a space"})})]}),e.jsx("div",{className:"hero__figure",children:p&&e.jsx("img",{className:"hero__image",src:s.imagePath,alt:"",onError:()=>j(n=>({...n,[s.bannerId]:!0}))})}),t>1&&e.jsxs("div",{className:"hero__controls",children:[e.jsx("button",{type:"button",className:"hero__arrow","aria-label":"Previous slide",onClick:()=>o(r-1),children:e.jsx(A,{size:18})}),e.jsx("div",{className:"hero__dots",children:h.map((n,l)=>e.jsx("button",{type:"button",className:`hero__dot${l===r?" hero__dot--active":""}`,"aria-label":`Slide ${l+1} of ${t}`,"aria-current":l===r,onClick:()=>o(l)},n.bannerId))}),e.jsx("button",{type:"button",className:"hero__arrow","aria-label":"Next slide",onClick:()=>o(r+1),children:e.jsx(W,{size:18})})]})]})}
export{H as CustomerHomePage};
