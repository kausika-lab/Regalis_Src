import{l as h,r as t,j as e,g as n,a3 as u,a8 as WS,y as WT}from"./index-DI5X4c9f.js";
import{b as BK}from"./bookings-DrKATIWm.js";
import{d as x}from"./reports-CNtuzowc.js";
import{C as o}from"./Card-CNXipofE.js";
import{E as p}from"./EmptyState-B4UbbVj4.js";
import{g as j}from"./greeting-JECElu04.js";
import{a as HM}from"./formatters-DFe5xCRi.js";
import{computeAvailability as CALC,istDate as ISTD}from"./availability-Bq7nK2xR.js";

/* Card presentation is derived from the server's own label, so a card the API
   adds later still lands with a sensible icon and period chip instead of
   needing a contract change (DashboardCard is "generic on purpose"). */
const ICONS=[[/booking/i,"CalendarDays"],[/occupanc/i,"LayoutGrid"],[/check/i,"ShieldCheck"],
  [/revenue.*month/i,"BarChart3"],[/revenue/i,"IndianRupee"],[/member/i,"CalendarRange"],
  [/pass/i,"Ticket"],[/ticket/i,"LifeBuoy"],[/customer/i,"UsersRound"],
  [/housekeep|maintenance/i,"Sparkles"]];
const MON=["Jan","Feb","Mar","Apr","May","Jun","Jul","Aug","Sep","Oct","Nov","Dec"];
function iconFor(l){for(const[re,nm]of ICONS)if(re.test(l))return nm;return"LayoutGrid"}
function badgeFor(l){
  if(/this month/i.test(l))return MON[new Date().getMonth()];
  if(/\bnow\b/i.test(l))return"Now";
  if(/today/i.test(l))return"Today";
  if(/^\s*active/i.test(l))return"Active";
  if(/^\s*open/i.test(l))return"Open";
  if(/customer/i.test(l))return"Total";
  return null
}
/* Card labels are built server-side in the reports payload, so the renames the
   admin portal wants are applied on the way out. */
function labelFor(l){return String(l??"")
  .replace(/housekeeping/ig,"Maintenance")
  .replace(/checked\s+in\s+now/ig,"Check in today")
  .replace(/\b[a-z]/g,c=>c.toUpperCase())}

const IST="Asia/Kolkata";
const ISTDAY=new Intl.DateTimeFormat("en-CA",{timeZone:IST,year:"numeric",month:"2-digit",day:"2-digit"});
function toUtc(v){return v instanceof Date?v:new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(v)?v:`${v}Z`)}
function istDate(v){return ISTDAY.format(toUtc(v))}
function plural(s){const v=String(s??"").trim();return!v||/s$/i.test(v)?v:`${v}s`}

function Stat({card:c}){
  const L=labelFor(c.label),I=u(iconFor(L)),b=badgeFor(L);
  const body=e.jsxs(e.Fragment,{children:[
    e.jsxs("div",{className:"stat-card__top",children:[
      e.jsx("span",{className:"stat-card__icon",children:e.jsx(I,{size:17})}),
      b&&e.jsx("span",{className:"stat-card__badge",children:b})
    ]}),
    e.jsx("div",{className:"stat-card__value",children:c.value}),
    e.jsx("div",{className:"stat-card__label",children:L}),
    c.hint&&e.jsx("div",{className:"stat-card__hint",children:c.hint})
  ]});
  return c.route
    ?e.jsx(n,{to:c.route,className:"stat-card",children:body})
    :e.jsx("div",{className:"stat-card stat-card--flat",children:body})
}

function Avail({rows:r}){
  if(r.length===0)return e.jsx("p",{className:"stat-card__hint",style:{margin:0},children:"No workspaces are set up yet."});
  return e.jsx("div",{className:"avail",children:r.map(w=>{
    /* total===null is a type with no seat cap: there is nothing to divide by,
       so it reports how many are live, keeps the name as the API spells it and
       carries no occupancy bar. */
    const uncapped=w.total===null;
    const pct=!uncapped&&w.total>0?Math.round(w.occupied/w.total*100):0;
    return e.jsxs("div",{className:"avail__row",children:[
      e.jsxs("div",{className:"avail__head",children:[
        e.jsx("span",{className:"avail__name",children:uncapped?w.name:plural(w.name)}),
        e.jsx("span",{className:"avail__count",children:uncapped?(w.booked>0?`${w.active} active · ${w.booked} booked today`:`${w.active} active`):`${w.occupied} / ${w.total} occupied today`})
      ]}),
      uncapped?null:e.jsx("div",{className:"avail__bar",children:e.jsx("span",{className:"avail__fill",style:{width:`${pct}%`}})})
    ]},w.name)
  })})
}

function _(){
  const{user:l,roles:c}=h();
  const[r,m]=t.useState(null);
  const[today,setToday]=t.useState(null);
  const[avail,setAvail]=t.useState(null);

  t.useEffect(()=>{x.admin().then(m).catch(()=>{})},[]);

  /* Today's bookings: the list endpoint takes no date filter, so a page is
     pulled and narrowed on the IST calendar day the front desk works to. */
  t.useEffect(()=>{
    let dead=!1;
    /* Bookings, workspaces and types are fetched together because the
       availability card needs all three: a workspace row carries
       workspaceTypeId but no typeName (the label lives on /workspace-types,
       which is how WorkspacesPage resolves it too), and "occupied" is read off
       the bookings, not off the workspace's own status flag - that flag is a
       staff marker and never moves when a customer books. */
    /* Today's bookings come filtered by the server (From/To), not by pulling a
       page and narrowing it here -- page 1 of 100 could miss today's rows once
       the table grows. Yesterday is included so a booking that runs through
       midnight still counts for today. */
    const k=ISTD(new Date()),
      y=ISTD(new Date(Date.now()-864e5));
    Promise.all([BK.list({page:1,pageSize:200,from:y,to:k}),WS.list({page:1,pageSize:200}),WT.list()]).then(([bres,wres,types])=>{
      if(dead)return;
      const bookings=bres?.items??(Array.isArray(bres)?bres:[]),
        rows=wres?.items??(Array.isArray(wres)?wres:[]),
        tl=Array.isArray(types)?types:types?.items??[];
      setToday(bookings.filter(b=>ISTD(b.startUtc)===k).sort((a,b)=>toUtc(a.startUtc)-toUtc(b.startUtc)));
      setAvail(CALC({bookings:bookings,workspaces:rows,types:tl,today:k}))
    }).catch(()=>{if(!dead){setToday([]);setAvail([])}});
    return()=>{dead=!0}
  },[]);

  return e.jsxs(e.Fragment,{children:[
    e.jsxs(o,{className:"dash-hello",children:[
      e.jsxs("h2",{style:{fontSize:"var(--font-size-xl)",fontWeight:800},children:[labelFor(j()),", ",l?.fullName??"Administrator"," ",e.jsx("span",{"aria-hidden":!0,children:"👋"})]}),
      e.jsxs("p",{style:{color:"var(--text-secondary)",marginTop:"var(--space-2)"},children:["Here's what's happening at Regalis WorkSpace today. Signed in as ",c.join(", ")||"no role assigned","."]})
    ]}),

    r&&e.jsx("div",{className:"stat-grid",children:r.cards.map(s=>e.jsx(Stat,{card:s},s.key))}),

    e.jsxs("div",{className:"home__columns",style:{marginTop:"var(--space-4)"},children:[
      e.jsx(o,{title:"Today's Bookings",actions:e.jsx(n,{to:"/admin/bookings",className:"dash-link",children:"View All"}),
        children:today===null?e.jsx("p",{className:"stat-card__hint",style:{margin:0},children:"Loading…"})
          :today.length===0?e.jsx(p,{title:"No bookings yet today",description:"New bookings appear here as they are made."})
          :e.jsx("div",{className:"booking-list",children:today.slice(0,6).map(b=>e.jsxs(n,{to:"/admin/bookings",className:"booking-row",children:[
            e.jsxs("div",{children:[
              e.jsx("div",{className:"booking-row__who",children:b.customerName}),
              e.jsxs("div",{className:"booking-row__what",children:[b.bookingNumber," · ",b.workspaceName]})
            ]}),
            e.jsxs("div",{className:"booking-row__when",children:[HM(b.startUtc)," – ",HM(b.endUtc)]})
          ]},b.bookingId))})}),
      e.jsx(o,{title:"Workspace Availability",
        children:avail===null?e.jsx("p",{className:"stat-card__hint",style:{margin:0},children:"Loading…"})
          :e.jsx(Avail,{rows:avail})})
    ]})
  ]})
}
export{_ as AdminDashboardPage};
