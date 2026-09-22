import{M as Modal}from"./Modal-BtdVDo1B.js";import{Y as D,a as N,r,A as T,j as t,G as F,F as h,I as R,p as I,R as $,g as z}from"./index-DI5X4c9f.js";import{u as B}from"./admin-DtGep2Hs.js";import{t as l,T as f,a as A,b,c as O,S as _}from"./SupportPages-Cf5pV8yQ.js";import{B as C}from"./Badge-BU5iAxex.js";import{C as E}from"./Card-CNXipofE.js";import{C as L}from"./Checkbox-CO1-Cn20.js";import{D as K}from"./DataTable-7lkt-S8A.js";import{P}from"./PageHeader-Y96MMVry.js";import{f as w}from"./formatters-DFe5xCRi.js";import{D as U}from"./download-Dlifs1Q2.js";import"./ContactUsPage-WEsv0zRR.js";import"./EmptyState-B4UbbVj4.js";import"./chevron-right-BikENMwC.js";function se(){const m=N(),p=$("TICKET.EXPORT"),qEdit=$("TICKET.EDIT"),[qRow,qSetRow]=r.useState(null),[a,d]=r.useState(1),[n,v]=r.useState(""),[i,y]=r.useState(""),[u,j]=r.useState(null),[S,g]=r.useState(!0),[x,c]=r.useState(!1),s=r.useCallback(async()=>{g(!0);try{j(await l.list({page:a,pageSize:20,status:n?Number(n):void 0,priority:i?Number(i):void 0}))}catch(e){m.error(e instanceof T?e.message:"Failed to load tickets.")}finally{g(!1)}},[a,n,i]);r.useEffect(()=>{s()},[s]);const o=[{key:"ticket",header:"Ticket",render:e=>t.jsxs("div",{children:[t.jsx(z,{to:`/admin/tickets/${e.ticketId}`,style:{fontWeight:600},children:e.ticketNumber}),t.jsx("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:e.subject})]})},{key:"customer",header:"Customer",render:e=>e.customerName},{key:"category",header:"Category",render:e=>t.jsxs("div",{children:[t.jsx("div",{children:e.categoryName}),t.jsx("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:e.issueTypeName})]})},{key:"priority",header:"Priority",render:e=>t.jsx(C,{tone:e.priority>=4?"danger":e.priority===3?"warning":"neutral",children:b[e.priority]})},{key:"status",header:"Status",render:e=>t.jsx(C,{tone:A[e.status],children:f[e.status]})},{key:"assignee",header:"Assignee",render:e=>e.assignedToName??"—"},{key:"raised",header:"Raised (IST)",render:e=>w(e.createdDate)},{key:"actions",header:"",width:"96px",render:e=>qEdit?t.jsx("div",{className:"row-actions",children:t.jsx(I,{size:"sm",variant:"outline",onClick:()=>qSetRow(e),children:"Edit"})}):null}];return t.jsxs(t.Fragment,{children:[t.jsx(P,{title:"Support / Tickets",description:"Triage: assign, converse (internal notes stay internal), resolve.",actions:p&&t.jsxs(I,{variant:"outline",loading:x,onClick:async()=>{c(!0);try{await l.export({status:n?Number(n):void 0,priority:i?Number(i):void 0})}catch{m.error("Export failed.")}finally{c(!1)}},children:[t.jsx(U,{size:16})," Export CSV"]})}),t.jsxs(E,{children:[t.jsxs("div",{className:"toolbar",children:[t.jsx(h,{label:"Status",value:n,placeholder:"All statuses",options:Object.entries(f).map(([e,k])=>({value:e,label:k})),onChange:e=>{v(e.target.value),d(1)}}),t.jsx(h,{label:"Priority",value:i,placeholder:"All priorities",options:Object.entries(b).map(([e,k])=>({value:e,label:k})),onChange:e=>{y(e.target.value),d(1)}})]}),t.jsx(K,{columns:o,result:u,loading:S,rowKey:e=>e.ticketId,onPageChange:d,emptyTitle:"No tickets"})]}),qRow&&t.jsx(TriageDialog,{ticket:qRow,onClose:()=>qSetRow(null),onSaved:()=>{qSetRow(null),s()}})]})}function ae(){const{id:m}=D(),p=N(),[a,d]=r.useState(null),[n,v]=r.useState([]),[i,y]=r.useState(""),[u,j]=r.useState(!1),[S,g]=r.useState(!1),x=r.useCallback(async()=>{try{d(await l.get(Number(m)))}catch(s){p.error(s instanceof T?s.message:"Failed to load the ticket.")}},[m]);if(r.useEffect(()=>{x(),B.list({page:1,pageSize:100,userType:2}).then(s=>v(s.items)).catch(()=>{})},[x]),!a)return t.jsx(F,{center:!0});const c=async(s,o)=>{g(!0);try{d(await s()),o&&p.success(o)}catch(e){p.error(e instanceof T?e.message:"Action failed.")}finally{g(!1)}};return t.jsxs(t.Fragment,{children:[t.jsx(P,{title:`${a.ticketNumber} — ${a.subject}`,description:`${a.customerName} · ${a.categoryName} · ${a.issueTypeName} · raised ${w(a.createdDate)} IST${a.reopenCount>0?` · reopened ×${a.reopenCount}`:""}`,actions:t.jsx(C,{tone:A[a.status],children:f[a.status]})}),t.jsxs(E,{children:[t.jsxs("div",{className:"toolbar",children:[t.jsx(h,{label:"Assignee",value:a.assignedToUserId?String(a.assignedToUserId):"",placeholder:"Unassigned",options:n.map(s=>({value:s.userId,label:s.fullName})),onChange:s=>s.target.value&&void c(()=>l.assign(a.ticketId,Number(s.target.value)),"Assigned.")}),t.jsx(h,{label:"Status",value:String(a.status),options:Object.entries(f).map(([s,o])=>({value:s,label:o})),onChange:s=>{c(()=>l.changeStatus(a.ticketId,Number(s.target.value)),"Status updated.")}}),t.jsx(h,{label:"Priority",value:String(a.priority),options:Object.entries(b).map(([s,o])=>({value:s,label:o})),onChange:s=>{c(()=>l.changePriority(a.ticketId,Number(s.target.value)),"Priority updated.")}})]}),t.jsx(O,{ticket:a,onDownload:s=>{const o=a.attachments.find(e=>e.ticketAttachmentId===s);l.downloadAttachment(a.ticketId,s,o?.fileName??"file")}}),a.status!==6&&t.jsxs("div",{className:"form-stack",style:{marginTop:"var(--space-4)"},children:[t.jsxs("div",{className:"toolbar",style:{marginBottom:0},children:[t.jsx(R,{label:u?"Internal note (never shown to the customer)":"Reply to the customer",value:i,onChange:s=>y(s.target.value)}),t.jsxs(I,{onClick:()=>{c(async()=>{const s=await l.addMessage(a.ticketId,i.trim(),u);return y(""),s})},loading:S,disabled:!i.trim(),children:[t.jsx(_,{size:16})," ",u?"Add note":"Send"]})]}),t.jsx(L,{label:"Internal note — visible to the team only",checked:u,onChange:s=>j(s.target.checked)})]})]})]})}
/**
 * Row-level triage for the ticket list.
 *
 * A ticket has no "edit" endpoint — the subject, category and customer belong to
 * whoever raised it. What staff can change is the assignee, the status and the
 * priority, each behind its own PATCH, so this dialog fires only the ones that
 * actually moved and leaves the conversation to the ticket page.
 */
function EL(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  if (kids.length === 1) p.children = kids[0];
  else if (kids.length > 1) p.children = kids;
  return kids.length > 1 ? t.jsxs(type, p, key) : t.jsx(type, p, key);
}

const FRAG = t.Fragment;

function TriageDialog({ ticket, onClose, onSaved }) {
  const toast = N();
  const [admins, setAdmins] = r.useState([]);
  const [assignee, setAssignee] = r.useState(ticket.assignedToUserId ? String(ticket.assignedToUserId) : "");
  const [status, setStatus] = r.useState(String(ticket.status));
  const [priority, setPriority] = r.useState(String(ticket.priority));
  const [saving, setSaving] = r.useState(false);

  r.useEffect(() => {
    B.list({ page: 1, pageSize: 100, userType: 2 })
      .then((page) => setAdmins(page.items))
      .catch(() => {});
  }, []);

  const assigneeMoved = Boolean(assignee) && Number(assignee) !== ticket.assignedToUserId;
  const statusMoved = Number(status) !== ticket.status;
  const priorityMoved = Number(priority) !== ticket.priority;
  const dirty = assigneeMoved || statusMoved || priorityMoved;

  const save = async () => {
    setSaving(true);
    try {
      if (assigneeMoved) await l.assign(ticket.ticketId, Number(assignee));
      if (priorityMoved) await l.changePriority(ticket.ticketId, Number(priority));
      if (statusMoved) await l.changeStatus(ticket.ticketId, Number(status));
      toast.success("Ticket updated.");
      onSaved();
    } catch (error) {
      toast.error(error instanceof T ? error.message : "Could not update the ticket.");
    } finally {
      setSaving(false);
    }
  };

  return EL(
    Modal,
    {
      open: true,
      title: `Triage — ${ticket.ticketNumber}`,
      onClose,
      footer: EL(
        FRAG,
        null,
        EL(I, { variant: "ghost", onClick: onClose }, "Cancel"),
        EL(I, { onClick: save, loading: saving, disabled: !dirty }, "Save")
      )
    },
    EL(
      "div",
      { className: "form-stack" },
      EL("p", { style: { color: "var(--text-secondary)", fontSize: "var(--font-size-sm)", margin: 0 } }, ticket.subject),
      EL(h, {
        label: "Assignee",
        value: assignee,
        placeholder: "Unassigned",
        options: admins.map((admin) => ({ value: admin.userId, label: admin.fullName })),
        onChange: (event) => setAssignee(event.target.value)
      }),
      EL(
        "div",
        { className: "form-grid" },
        EL(h, {
          label: "Status",
          value: status,
          options: Object.entries(f).map(([value, label]) => ({ value, label })),
          onChange: (event) => setStatus(event.target.value)
        }),
        EL(h, {
          label: "Priority",
          value: priority,
          options: Object.entries(b).map(([value, label]) => ({ value, label })),
          onChange: (event) => setPriority(event.target.value)
        })
      ),
      EL(
        "p",
        { style: { fontSize: "var(--font-size-xs)", color: "var(--text-muted)", margin: 0 } },
        "Replies and internal notes live on the ticket itself."
      )
    )
  );
}

export{ae as AdminTicketDetailPage,se as AdminTicketsPage};
