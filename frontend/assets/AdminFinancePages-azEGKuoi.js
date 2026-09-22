import{a as P,r as n,R as z,V as W,A,j as e,I as b,F,p as f,Z as w,a9 as V,O as M,X as Y,D as UW,E as HTTP,g as LK}from"./index-DI5X4c9f.js";import{i as H}from"./bookings-DrKATIWm.js";import{u as X}from"./admin-DtGep2Hs.js";import{B as T}from"./Badge-BU5iAxex.js";import{C as D}from"./Card-CNXipofE.js";import{M as R}from"./Modal-BtdVDo1B.js";import{D as L}from"./DataTable-7lkt-S8A.js";import{P as B}from"./PageHeader-Y96MMVry.js";import{u as $}from"./useDebounce-DSDIQF2c.js";import{I as G,c as U,d as Z,W as J,b as Q}from"./finance-C0-kLKkN.js";import{b as C,c as _,f as q}from"./formatters-DFe5xCRi.js";import{D as K}from"./download-Dlifs1Q2.js";import"./EmptyState-B4UbbVj4.js";import"./chevron-right-BikENMwC.js";function xe(){const d=P(),[i,u]=n.useState(1),[h,m]=n.useState(""),[s,l]=n.useState(""),[g,v]=n.useState(!1),[j,c]=n.useState(null),[x,p]=n.useState(!0),[y,a]=n.useState(null),[r,S]=n.useState(null),N=$(h,350),E=z("INVOICE.EDIT"),o=n.useCallback(async()=>{p(!0);try{c(await W.list({page:i,pageSize:20,search:N||void 0,status:s?Number(s):void 0,overdue:g||void 0}))}catch(t){d.error(t instanceof A?t.message:"Failed to load invoices.")}finally{p(!1)}},[i,N,s,g]);n.useEffect(()=>{o()},[o]);const I=async t=>{S(t.invoiceId);try{await W.send(t.invoiceId),d.success("Invoice marked as sent."),await o()}catch(O){d.error(O instanceof A?O.message:"Could not update the invoice.")}finally{S(null)}},k=[{key:"number",header:"Invoice",render:t=>e.jsxs("div",{children:[e.jsx("div",{style:{fontWeight:600},children:t.invoiceNumber}),e.jsxs("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:[t.customerName," · ",t.customerEmail]})]})},{key:"source",header:"Source",render:t=>t.quotationNumber??(t.bookingId?"Booking":"—")},{key:"date",header:"Date",render:t=>_(t.invoiceDate)},{key:"due",header:"Due",render:t=>e.jsx("span",{style:{color:t.isOverdue?"var(--color-danger)":void 0},children:_(t.dueDate)})},{key:"taxable",header:"Taxable",render:t=>C(t.taxableAmount)},{key:"gst",header:"GST",render:t=>C(t.cgstAmount+t.sgstAmount+t.igstAmount)},{key:"total",header:"Total",render:t=>C(t.totalAmount)},{key:"status",header:"Status",render:t=>e.jsxs(e.Fragment,{children:[e.jsx(T,{tone:t.status===2?"success":t.status===4?"neutral":"warning",children:G[t.status]}),t.isOverdue&&e.jsx(T,{tone:"danger",children:"Overdue"})]})},{key:"actions",header:"",render:t=>e.jsxs("div",{className:"row-actions",children:[e.jsxs(f,{size:"sm",variant:"outline",onClick:()=>H.downloadPdf(t.invoiceId,t.invoiceNumber),children:[e.jsx(K,{size:14})," PDF"]}),E&&t.status===1&&e.jsx(f,{size:"sm",variant:"outline",loading:r===t.invoiceId,onClick:()=>{I(t)},children:"Mark sent"}),E&&t.paidAmount===0&&t.status!==4&&e.jsx(f,{size:"sm",variant:"ghost",onClick:()=>a(t),children:"Cancel"})]})}];return e.jsxs(e.Fragment,{children:[e.jsx(B,{title:"Invoices",description:"Every GST invoice issued by the system. An issued invoice is a snapshot — corrections are a cancellation, never an edit."}),e.jsxs(D,{children:[e.jsxs("div",{className:"toolbar",children:[e.jsx(b,{label:"Search",placeholder:"Number, customer…",value:h,onChange:t=>{m(t.target.value),u(1)}}),e.jsx(F,{label:"Status",value:s,placeholder:"All statuses",options:Object.entries(G).map(([t,O])=>({value:t,label:O})),onChange:t=>{l(t.target.value),u(1)}}),e.jsx(f,{variant:g?"primary":"outline",onClick:()=>{v(t=>!t),u(1)},children:g?"Showing overdue":"Overdue only"})]}),e.jsx(L,{columns:k,result:j,loading:x,rowKey:t=>t.invoiceId,onPageChange:u,emptyTitle:"No invoices yet"})]}),y&&e.jsx(ee,{invoice:y,onClose:()=>a(null),onCancelled:()=>{a(null),o()}})]})}function ee({invoice:d,onClose:i,onCancelled:u}){const h=P(),[m,s]=n.useState(""),[l,g]=n.useState(!1),v=async()=>{g(!0);try{await W.cancel(d.invoiceId,m.trim()),h.success("Invoice cancelled."),u()}catch(j){h.error(j instanceof A?j.message:"Could not cancel the invoice.")}finally{g(!1)}};return e.jsx(R,{open:!0,title:`Cancel ${d.invoiceNumber}?`,onClose:i,footer:e.jsxs(e.Fragment,{children:[e.jsx(f,{variant:"ghost",onClick:i,children:"Keep it"}),e.jsx(f,{onClick:v,loading:l,disabled:m.trim().length===0,children:"Cancel invoice"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsx("p",{children:"The invoice keeps its number and stays on the register — cancelling records why, it does not erase it. An invoice with payments against it cannot be cancelled."}),e.jsx(b,{label:"Reason",required:!0,value:m,onChange:j=>s(j.target.value)})]})})}function je(){const d=P(),[i,u]=n.useState(1),[h,m]=n.useState(""),[s,l]=n.useState(""),[g,v]=n.useState(null),[j,c]=n.useState(!0),x=$(h,350),p=n.useCallback(async()=>{c(!0);try{v(await V.list({page:i,pageSize:20,search:x||void 0,status:s?Number(s):void 0}))}catch(a){d.error(a instanceof A?a.message:"Failed to load payments.")}finally{c(!1)}},[i,x,s]);n.useEffect(()=>{p()},[p]);const y=[{key:"number",header:"Payment",render:a=>e.jsxs("div",{children:[e.jsx("div",{style:{fontWeight:600},children:a.paymentNumber}),e.jsx("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:a.customerName})]})},{key:"invoice",header:"Invoice",render:a=>e.jsxs("div",{children:[e.jsx("div",{children:a.invoiceNumber??(a.bookingId?"Booking":"")}),a.invoiceAmount!==null&&e.jsxs("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:["of ",C(a.invoiceAmount)]})]})},{key:"amount",header:"Amount paid",render:a=>e.jsx("strong",{children:C(a.amount)})},{key:"method",header:"Method",render:a=>Z[a.paymentMethod]??"—"},{key:"gateway",header:"Gateway",render:a=>a.gateway??"—"},{key:"status",header:"Status",render:a=>e.jsx(T,{tone:a.status===2?"success":a.status===3?"danger":"warning",children:U[a.status]})},{key:"txn",header:"Transaction ID",render:a=>e.jsxs("div",{children:[e.jsx("div",{children:a.gatewayPaymentId??""}),a.gatewayOrderId&&e.jsx("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:a.gatewayOrderId})]})},{key:"when",header:"Paid (IST)",render:a=>a.paidUtc?q(a.paidUtc):"—"},{key:"receipt",header:"",render:a=>a.status===2?e.jsxs(f,{size:"sm",variant:"outline",onClick:()=>{Y.receipt(a.paymentId,a.paymentNumber)},children:[e.jsx(K,{size:14})," Receipt"]}):null}];return e.jsxs(e.Fragment,{children:[e.jsx(B,{title:"Payments",description:"Wallet, gateway and cash settlements."}),e.jsxs(D,{children:[e.jsxs("div",{className:"toolbar",children:[e.jsx(b,{label:"Search",placeholder:"Payment no., order id…",value:h,onChange:a=>{m(a.target.value),u(1)}}),e.jsx(F,{label:"Status",value:s,placeholder:"All statuses",options:Object.entries(U).map(([a,r])=>({value:a,label:r})),onChange:a=>{l(a.target.value),u(1)}})]}),e.jsx(L,{columns:y,result:g,loading:j,rowKey:a=>a.paymentId,onPageChange:u,emptyTitle:"No payments yet"})]})]})}function pe(){const d=P(),i=z("WALLET.EDIT"),[u,h]=n.useState(""),[m,s]=n.useState([]),[l,g]=n.useState(null),[v,j]=n.useState(null),[c,x]=n.useState(null),[p,y]=n.useState(1),[a,r]=n.useState(!1),S=$(u,350);n.useEffect(()=>{let cancelled=!1;X.list({page:1,pageSize:50,search:S||void 0,userType:1}).then(o=>{cancelled||s(o.items??[])}).catch(()=>{cancelled||s([])});return()=>{cancelled=!0}},[S]);const N=n.useCallback(async(o,I)=>{try{const[k,t]=await Promise.all([M.ofCustomer(o.userId),M.customerTransactions(o.userId,{page:I,pageSize:15})]);j(k),x(t)}catch(k){d.error(k instanceof A?k.message:"Failed to load the wallet.")}},[]);n.useEffect(()=>{l&&N(l,p)},[l,p,N]);const E=[{key:"number",header:"Txn",render:o=>o.transactionNumber},{key:"narration",header:"Narration",render:o=>o.narration},{key:"category",header:"Category",render:o=>J[o.category]},{key:"amount",header:"Amount",render:o=>e.jsxs("span",{style:{color:o.direction===1?"var(--color-success)":"var(--color-danger)"},children:[o.direction===1?"+":"−",C(o.amount)]})},{key:"balance",header:"Balance",render:o=>C(o.balanceAfter)},{key:"when",header:"When (IST)",render:o=>q(o.transactionUtc)}];return e.jsxs(e.Fragment,{children:[e.jsx(B,{title:"Customer Wallets",description:"The ledger is append-only and self-checking: credits − debits always equal the balance."}),e.jsxs(D,{children:[e.jsxs("div",{className:"toolbar",children:[e.jsx(b,{label:"Find customer",placeholder:"Name, email or mobile…",value:u,onChange:o=>h(o.target.value)}),e.jsx(F,{label:"Customer",value:l?String(l.userId):"",placeholder:m.length?"Select customer":"No customer matches",options:m.map(o=>({value:o.userId,label:`${o.fullName} · ${o.email}`})),onChange:o=>{const I=m.find(k=>String(k.userId)===o.target.value)??null;g(I),y(1)}}),l&&v&&e.jsx("div",{className:"dash-tile",style:{cursor:"default",padding:"var(--space-3) var(--space-4)"},children:e.jsxs("div",{children:[e.jsx("div",{className:"dash-tile__label",children:l.fullName}),e.jsx("div",{style:{fontWeight:800,fontSize:"var(--font-size-lg)"},children:C(v.balance)})]})}),l&&i&&e.jsx(f,{onClick:()=>r(!0),children:"Adjust"})]}),l&&e.jsx(L,{columns:E,result:c,loading:!1,rowKey:o=>o.walletTransactionId,onPageChange:y,emptyTitle:"No transactions"})]}),l&&a&&e.jsx(te,{customer:l,onClose:()=>r(!1),onDone:()=>{r(!1),N(l,p)}})]})}function te({customer:d,onClose:i,onDone:u}){const h=P(),[m,s]=n.useState("credit"),[l,g]=n.useState(100),[v,j]=n.useState(""),[nt,snt]=n.useState(""),[c,x]=n.useState(!1),RS=()=>{const a1=v.trim(),a2=nt.trim();return(a2?`${a1} — note: ${a2}`:a1).slice(0,240)},p=async()=>{x(!0);try{await M.adjust(d.userId,m,l,RS()),h.success("Wallet adjusted. The reason is on the audit trail."),u()}catch(y){h.error(y instanceof A?y.message:"Adjustment failed.")}finally{x(!1)}};return e.jsx(R,{open:!0,title:`Adjust wallet — ${d.fullName}`,onClose:i,footer:e.jsxs(e.Fragment,{children:[e.jsx(f,{variant:"ghost",onClick:i,children:"Cancel"}),e.jsx(f,{onClick:p,loading:c,disabled:!v.trim()||!(l>0),children:"Apply"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsx(F,{label:"Direction",value:m,options:[{value:"credit",label:"Credit (add funds)"},{value:"debit",label:"Debit (remove funds)"}],onChange:y=>s(y.target.value)}),e.jsx(b,{label:"Amount (₹)",type:"number",min:1,value:l,onChange:y=>g(Number(y.target.value))}),e.jsx(b,{label:"Reason",required:!0,value:v,hint:"Mandatory — written to the audit trail",onChange:y=>j(y.target.value)}),e.jsx(b,{label:"Internal note",value:nt,hint:"Optional — appended to the reason on the ledger entry",onChange:y=>snt(y.target.value)})]})})}/* Purchase details for a membership / day pass.

   There is no GET /api/passes/{id}; the row we already hold is the whole
   CustomerPassDto. Everything else is stitched from endpoints that exist:
     customer profile  GET /api/customers/{id}   -> email, phone, company
     invoice           GET /api/invoices/{id}    -> CGST/SGST split, invoice no.
     payment           GET /api/payments         -> method, refs, refund
   PaymentDto carries customerPassId, so the payment is matched on that rather
   than guessed from amounts.

   Not shown because the data is not recorded anywhere reachable:
     - who created the purchase (TRN_CustomerPass.CreatedBy is stored but the
       DTO does not expose it)
     - cancelled-on / cancelled-by (the table has no cancellation columns at
       all; cancelling only flips Status) */
function PVRow(k, v) {
  return e.jsxs("div", { children: [
    e.jsx("div", { className: "inv-kv__k", children: k }),
    e.jsx("div", { className: "inv-kv__v", children: v ?? "—" })
  ] }, k);
}
function PVSection(title, children) {
  return e.jsxs("div", { children: [
    e.jsx("div", { style: { fontWeight: 700, fontSize: "var(--font-size-sm)", margin: "0 0 var(--space-2)" }, children: title }),
    e.jsx("div", { className: "inv-kv", children: children })
  ] });
}
function PV({ pass: pv, plans: pl, onClose: cl }) {
  const [state, setState] = n.useState("loading"), [cust, setCust] = n.useState(null),
    [inv, setInv] = n.useState(null), [pay, setPay] = n.useState(null), [partial, setPartial] = n.useState(!1);

  n.useEffect(() => {
    let dead = !1;
    const miss = [];
    Promise.all([
      UW(HTTP.get(`/customers/${pv.customerId}`)).catch(() => { miss.push("member"); return null }),
      pv.invoiceId ? UW(HTTP.get(`/invoices/${pv.invoiceId}`)).catch(() => { miss.push("invoice"); return null }) : Promise.resolve(null),
      UW(HTTP.get("/payments", { params: { customerId: pv.customerId, page: 1, pageSize: 50 } })).catch(() => { miss.push("payment"); return null })
    ]).then(([c, i2, p2]) => {
      if (dead) return;
      const list = p2?.items ?? (Array.isArray(p2) ? p2 : []);
      /* Every lookup failing means the detail genuinely could not be loaded;
         one failing just means that section is unavailable. */
      if (miss.length === 3) { setState("error"); return }
      setCust(c); setInv(i2);
      setPay(list.find(x => x.customerPassId === pv.customerPassId) ?? null);
      setPartial(miss.length > 0); setState("ok")
    }).catch(() => { if (!dead) setState("error") });
    return () => { dead = !0 }
  }, []);

  const plan = pl.find(x => x.passPlanId === pv.passPlanId) ?? null,
    cancelled = pv.status === 4,
    daysLeft = pv.status === 1 ? Math.max(0, Math.ceil((new Date(pv.validTo).getTime() - Date.now()) / 864e5)) : null,
    body = state === "loading"
      ? e.jsx("p", { style: { color: "var(--text-muted)", margin: 0 }, children: "Loading purchase details…" })
      : state === "error"
        ? e.jsxs("div", { children: [
            e.jsx("p", { style: { color: "var(--color-danger)", fontWeight: 600, margin: "0 0 var(--space-2)" }, children: "This purchase could not be loaded." }),
            e.jsx("p", { style: { color: "var(--text-muted)", margin: 0, fontSize: "var(--font-size-sm)" }, children: "The member, invoice and payment lookups all failed. Check your connection and try again — the purchase itself has not been changed." })
          ] })
        : e.jsxs("div", { className: "form-stack", children: [
            PVSection("Purchase", [
              PVRow("Pass number", pv.passNumber),
              PVRow("Purchased", pv.purchaseUtc ? q(pv.purchaseUtc) : null),
              PVRow("Status", e.jsx(T, { tone: pv.status === 1 ? "success" : cancelled ? "danger" : "neutral", children: Q[pv.status] })),
              PVRow("Created by", e.jsx("span", { style: { color: "var(--text-muted)", fontWeight: 400 }, children: "Not recorded" }))
            ]),
            PVSection("Member", [
              PVRow("Full name", cust?.fullName ?? pv.customerName),
              PVRow("Member ID", pv.customerId),
              PVRow("Email", cust?.email),
              PVRow("Phone", cust?.mobileNumber),
              PVRow("Company", cust?.companyName)
            ]),
            PVSection("Plan", [
              PVRow("Plan", pv.planName),
              PVRow("Plan code", plan?.planCode),
              PVRow("Duration", plan ? `${plan.validityDays} day${plan.validityDays > 1 ? "s" : ""}` : null),
              PVRow("Valid from", _(pv.validFrom)),
              PVRow("Valid to", _(pv.validTo)),
              PVRow("Days remaining", daysLeft === null ? null : `${daysLeft} day${daysLeft === 1 ? "" : "s"}`)
            ]),
            PVSection("Pricing", [
              PVRow("Base amount", C(pv.price)),
              inv ? PVRow(`CGST${inv.cgstPercent ? ` @ ${inv.cgstPercent}%` : ""}`, C(inv.cgstAmount)) : null,
              inv ? PVRow(`SGST${inv.sgstPercent ? ` @ ${inv.sgstPercent}%` : ""}`, C(inv.sgstAmount)) : null,
              inv && inv.igstAmount ? PVRow(`IGST${inv.igstPercent ? ` @ ${inv.igstPercent}%` : ""}`, C(inv.igstAmount)) : null,
              inv ? null : PVRow("GST", C(pv.taxAmount)),
              PVRow("Total paid", e.jsx("strong", { children: C(pv.totalAmount) }))
            ].filter(Boolean)),
            PVSection("Payment", [
              PVRow("Method", pay ? (Z[pay.paymentMethod] ?? pay.paymentMethod) : null),
              PVRow("Payment status", pay ? (U[pay.paymentStatus ?? pay.status] ?? pay.status) : null),
              PVRow("Payment number", pay?.paymentNumber),
              PVRow("Reference", pay?.gatewayPaymentId ?? pay?.gatewayOrderId),
              PVRow("Paid on", pay?.paidUtc ? q(pay.paidUtc) : null),
              PVRow("Invoice", inv?.invoiceNumber
                ? e.jsx(LK, { to: "/admin/invoices", onClick: cl, children: inv.invoiceNumber })
                : (pv.invoiceId ? `#${pv.invoiceId}` : null))
            ]),
            cancelled ? PVSection("Cancellation", [
              PVRow("Refunded", pay?.refundedAmount ? C(pay.refundedAmount) : "No refund recorded"),
              PVRow("Refund reference", pay?.refundReference),
              PVRow("Cancelled on", e.jsx("span", { style: { color: "var(--text-muted)", fontWeight: 400 }, children: "Not recorded" })),
              PVRow("Cancelled by", e.jsx("span", { style: { color: "var(--text-muted)", fontWeight: 400 }, children: "Not recorded" }))
            ]) : null,
            partial ? e.jsx("p", { style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)", margin: 0 }, children: "Some linked records could not be loaded, so a few fields are blank." }) : null,
            e.jsx("p", { style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)", margin: 0 }, children: "Created-by and cancellation details are not stored against a pass, so they cannot be shown." })
          ].filter(Boolean) });

  return e.jsx(R, { open: !0, title: `Purchase — ${pv.passNumber}`, onClose: cl,
    footer: e.jsx(f, { variant: "ghost", onClick: cl, children: "Close" }), children: body })
}
function fe({passType:d}){const i=P(),u=z("PASS.CREATE"),h=z("PASS.EDIT"),[m,s]=n.useState([]),[l,g]=n.useState(null),[v,j]=n.useState(1),[c,x]=n.useState(null),[pv,spv]=n.useState(null),[q0,q1]=n.useState(!0),p=n.useCallback(async()=>{q1(!0);try{const[r,S]=await Promise.all([w.allPlans(),w.list({page:v,pageSize:15,passType:d})]);s(r.filter(N=>N.passType===d)),g(S)}catch(r){i.error(r instanceof A?r.message:"Failed to load passes.")}finally{q1(!1)}},[v,d]);n.useEffect(()=>{p()},[p]);const y=d===1?"Day Pass":"Monthly Pass",a=[{key:"pass",header:"Pass",render:r=>e.jsxs("div",{children:[e.jsx("div",{style:{fontWeight:600},children:r.passNumber}),e.jsxs("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:[r.customerName," · ",r.planName]})]})},{key:"valid",header:"Valid",render:r=>`${_(r.validFrom)} – ${_(r.validTo)}`},{key:"amount",header:"Paid",render:r=>C(r.totalAmount)},{key:"status",header:"Status",render:r=>e.jsx(T,{tone:r.status===1?"success":r.status===4?"danger":"neutral",children:Q[r.status]})},{key:"actions",header:"",render:r=>e.jsxs("div",{className:"row-actions",children:[e.jsx(f,{size:"sm",variant:"ghost",onClick:()=>spv(r),children:"View"}),h&&r.status===1?e.jsx(f,{size:"sm",variant:"ghost",onClick:async()=>{try{await w.cancel(r.customerPassId),i.success("Pass cancelled. Pre-start passes refund to the wallet."),await p()}catch(S){i.error(S instanceof A?S.message:"Cancel failed.")}},children:"Cancel"}):null]})}];return e.jsxs(e.Fragment,{children:[e.jsx(B,{title:`${y} plans`,actions:u&&e.jsx(f,{onClick:()=>x("new"),children:"New plan"})}),e.jsx(D,{children:e.jsxs("div",{className:"choice-grid",children:[m.map(r=>e.jsxs("div",{className:"choice-card",style:{cursor:"default"},children:[e.jsxs("div",{className:"choice-card__title",children:[r.planName," ",!r.isActive&&e.jsx(T,{tone:"neutral",children:"Inactive"})]}),e.jsxs("div",{className:"choice-card__meta",children:[r.planCode," · ",r.validityDays," day",r.validityDays>1?"s":""]}),e.jsxs("div",{className:"choice-card__price",children:[C(r.totalAmount)," incl. GST"]}),h&&e.jsx("div",{style:{marginTop:"var(--space-2)"},children:e.jsx(f,{size:"sm",variant:"outline",onClick:()=>x(r),children:"Edit"})})]},r.passPlanId)),!q0&&m.length===0&&e.jsx("p",{style:{color:"var(--text-muted)"},children:"No plans yet — create the first one."})]})}),e.jsx(D,{title:"Purchases",children:e.jsx(L,{columns:a,result:l,loading:q0,rowKey:r=>r.customerPassId,onPageChange:j,emptyTitle:"No purchases yet"})}),c&&e.jsx(ae,{passType:d,plan:c==="new"?null:c,onClose:()=>x(null),onDone:()=>{x(null),p()}}),pv&&e.jsx(PV,{pass:pv,plans:m,onClose:()=>spv(null)})]})}function ae({passType:d,plan:i,onClose:u,onDone:h}){const m=P(),[s,l]=n.useState({planCode:i?.planCode??"",planName:i?.planName??"",price:i?.price??(d===1?800:12e3),validityDays:i?.validityDays??(d===1?1:30),description:i?.description??"",termsAndConditions:i?.termsAndConditions??"",displayOrder:i?.displayOrder??10,isActive:i?.isActive??!0}),[g,v]=n.useState(!1),j=async()=>{v(!0);const c={planCode:s.planCode.trim().toUpperCase(),planName:s.planName.trim(),passType:d,locationId:null,price:Number(s.price),validityDays:Number(s.validityDays),maxGuests:null,description:s.description||null,termsAndConditions:s.termsAndConditions||null,displayOrder:Number(s.displayOrder),isActive:s.isActive};try{i?await w.updatePlan(i.passPlanId,c):await w.createPlan(c),m.success(i?"Plan updated.":"Plan created and on sale."),h()}catch(x){m.error(x instanceof A?x.message:"Could not save the plan.")}finally{v(!1)}};return e.jsx(R,{open:!0,title:i?`Edit — ${i.planName}`:"New plan",onClose:u,footer:e.jsxs(e.Fragment,{children:[e.jsx(f,{variant:"ghost",onClick:u,children:"Cancel"}),e.jsx(f,{onClick:j,loading:g,disabled:!s.planName.trim()||!i&&!s.planCode.trim(),children:"Save"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsxs("div",{className:"form-grid",children:[!i&&e.jsx(b,{label:"Code",required:!0,value:s.planCode,hint:"e.g. DAY-STD",onChange:c=>l({...s,planCode:c.target.value})}),e.jsx(b,{label:"Name",required:!0,value:s.planName,onChange:c=>l({...s,planName:c.target.value})}),e.jsx(b,{label:"Price (₹, before GST)",type:"number",min:1,value:s.price,onChange:c=>l({...s,price:Number(c.target.value)})}),e.jsx(b,{label:"Validity (days)",type:"number",min:1,value:s.validityDays,onChange:c=>l({...s,validityDays:Number(c.target.value)})}),e.jsx(b,{label:"Display order",type:"number",value:s.displayOrder,onChange:c=>l({...s,displayOrder:Number(c.target.value)})})]}),e.jsx(b,{label:"Description",value:s.description,onChange:c=>l({...s,description:c.target.value})}),e.jsxs("div",{className:"field",children:[e.jsx("label",{className:"field__label",htmlFor:"pass-plan-terms",children:"Terms and conditions"}),e.jsx("textarea",{id:"pass-plan-terms",rows:4,className:"field__input",value:s.termsAndConditions,onChange:c=>l({...s,termsAndConditions:c.target.value})}),e.jsx("span",{className:"field__hint",children:"Shown to the customer before payment."})]}),e.jsxs("label",{className:"checkbox",children:[e.jsx("input",{type:"checkbox",checked:s.isActive,onChange:c=>l({...s,isActive:c.target.checked})}),e.jsx("span",{children:"On sale"})]})]})})}export{xe as AdminInvoicesPage,fe as AdminPassesPage,je as AdminPaymentsPage,pe as AdminWalletPage};
