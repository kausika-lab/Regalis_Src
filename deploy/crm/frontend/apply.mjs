// crm kit (19 Nov 2026): CRM — Leads and Web Enquiries, frontend part.
//   node apply.mjs <frontend dir>
// Copies the new files (assets/CrmPages-CRM1.js, assets/CrmEnquirePage-CRM1.js, crm-track.js) and
// patches existing chunks with string edits that must each match exactly once, so it also works on
// copies carrying other kits' patches. Idempotent: an edit already present is skipped.
//
// Existing behaviour is untouched; every edit only ADDS:
//   index-DI5X4c9f.js         CRM routes (admin + public /enquire), 3 sidebar icons, "Book a tour" link on sign-in
//   AdminQuotationPages-*.js  a new quotation opened as ?lead=<id> is pre-filled from that lead
//   AdminDashboardPage-*.js   "+ Add lead" on the greeting card (CRM.LEAD.CREATE only)
//   index.html                loads /crm-track.js (records anonymous visits on the public pages only)
//   sw.js                     revisions for the changed and new files
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "crm-1";
const here = path.dirname(fileURLToPath(import.meta.url));
const A = (f) => path.join(front, "assets", f);
const changed = new Set();

function patchFile(p, label, edits) {
  let t = fs.readFileSync(p, "utf8");
  // Some chunks are stored with CRLF line endings: match them in kind.
  const eol = (s) => (t.includes("\r\n") ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo] of edits) {
    const from = eol(rawFrom);
    const to = eol(rawTo);
    const n = t.split(from).length - 1;
    // "already done" is checked first: replacements keep the anchor text inside them.
    if (t.includes(to)) console.log(`OK  - ${label}: ${name} (already done)`);
    else if (n === 1) {
      t = t.replace(from, () => to);
      console.log(`OK  - ${label}: ${name}`);
      changed.add(label);
    } else throw new Error(`${label}: '${name}' expected 1 match, found ${n}; nothing written for this file`);
  }
  fs.writeFileSync(p, t, "utf8");
}
const patch = (file, edits) => patchFile(A(file), file, edits);

// ---- new files ------------------------------------------------------------------------------
for (const [src, dst] of [
  [path.join(here, "assets", "CrmPages-CRM1.js"), A("CrmPages-CRM1.js")],
  [path.join(here, "assets", "CrmEnquirePage-CRM1.js"), A("CrmEnquirePage-CRM1.js")],
  [path.join(here, "crm-track.js"), path.join(front, "crm-track.js")]
]) {
  const same = fs.existsSync(dst) && fs.readFileSync(dst, "utf8") === fs.readFileSync(src, "utf8");
  if (!same) fs.copyFileSync(src, dst);
  console.log(`OK  - ${path.basename(dst)} ${same ? "(already there)" : "copied"}`);
  changed.add(path.relative(path.join(front, "assets"), dst).startsWith("..") ? "../crm-track.js" : path.basename(dst));
}

// ---- index: lazy chunks, routes, icons, sign-in link ----------------------------------------------
const LAZY =
  'RGcrmLeads=fe(()=>ce(()=>import("./CrmPages-CRM1.js")),"CrmLeadsPage"),RGcrmLead=fe(()=>ce(()=>import("./CrmPages-CRM1.js")),"CrmLeadPage"),' +
  'RGcrmFollow=fe(()=>ce(()=>import("./CrmPages-CRM1.js")),"CrmFollowUpsPage"),RGcrmVisitors=fe(()=>ce(()=>import("./CrmPages-CRM1.js")),"CrmVisitorsPage"),' +
  'RGcrmReports=fe(()=>ce(()=>import("./CrmPages-CRM1.js")),"CrmReportsPage"),RGcrmSettings=fe(()=>ce(()=>import("./CrmPages-CRM1.js")),"CrmSettingsPage"),' +
  'RGcrmEnquire=fe(()=>ce(()=>import("./CrmEnquirePage-CRM1.js")),"CrmEnquirePage"),';
const ROUTES =
  'g.jsxs(P,{element:g.jsx(Ne,{permission:"CRM.VIEW"}),children:[g.jsx(P,{path:"/admin/crm",element:g.jsx(Os,{to:"/admin/crm/leads",replace:!0})}),' +
  'g.jsx(P,{path:"/admin/crm/leads",element:g.jsx(RGcrmLeads,{})}),g.jsx(P,{path:"/admin/crm/leads/:id",element:g.jsx(RGcrmLead,{})}),' +
  'g.jsx(P,{path:"/admin/crm/follow-ups",element:g.jsx(RGcrmFollow,{})})]}),' +
  'g.jsx(P,{element:g.jsx(Ne,{permission:"CRM.VISITORS.VIEW"}),children:g.jsx(P,{path:"/admin/crm/visitors",element:g.jsx(RGcrmVisitors,{})})}),' +
  'g.jsx(P,{element:g.jsx(Ne,{permission:"CRM.REPORTS.VIEW"}),children:g.jsx(P,{path:"/admin/crm/reports",element:g.jsx(RGcrmReports,{})})}),' +
  'g.jsx(P,{element:g.jsx(Ne,{permission:"CRM.SETTINGS"}),children:g.jsx(P,{path:"/admin/administration/crm-settings",element:g.jsx(RGcrmSettings,{})})}),';
const ICONS =
  'RG_target=[["circle",{cx:"12",cy:"12",r:"10",key:"crt1"}],["circle",{cx:"12",cy:"12",r:"6",key:"crt2"}],["circle",{cx:"12",cy:"12",r:"2",key:"crt3"}]],RGtarget=Oe("target",RG_target),' +
  'RG_userplus=[["path",{d:"M16 21v-2a4 4 0 0 0-4-4H6a4 4 0 0 0-4 4v2",key:"cru1"}],["circle",{cx:"9",cy:"7",r:"4",key:"cru2"}],["line",{x1:"19",x2:"19",y1:"8",y2:"14",key:"cru3"}],["line",{x1:"22",x2:"16",y1:"11",y2:"11",key:"cru4"}]],RGuserplus=Oe("user-plus",RG_userplus),' +
  'RG_globe=[["circle",{cx:"12",cy:"12",r:"10",key:"crg1"}],["path",{d:"M12 2a14.5 14.5 0 0 0 0 20 14.5 14.5 0 0 0 0-20",key:"crg2"}],["path",{d:"M2 12h20",key:"crg3"}]],RGglobe=Oe("globe",RG_globe);';
patch("index-DI5X4c9f.js", [
  ["CRM chunks", 'RGpayhist=fe(()=>ce(()=>import("./PaymentHistoryPage-PY1.js")),"PaymentHistoryPage"),', 'RGpayhist=fe(()=>ce(()=>import("./PaymentHistoryPage-PY1.js")),"PaymentHistoryPage"),' + LAZY],
  ["CRM admin routes", 'g.jsxs(P,{element:g.jsx(Ne,{permission:"PASS.VIEW"}),children:[g.jsx(P,{path:"/admin/waitlist"', ROUTES + 'g.jsxs(P,{element:g.jsx(Ne,{permission:"PASS.VIEW"}),children:[g.jsx(P,{path:"/admin/waitlist"'],
  ["public /enquire route", 'g.jsxs(P,{element:g.jsx(FS,{}),children:[g.jsx(P,{path:"/login"', 'g.jsx(P,{path:"/enquire",element:g.jsx(RGcrmEnquire,{})}),g.jsxs(P,{element:g.jsx(FS,{}),children:[g.jsx(P,{path:"/login"'],
  ["Book a tour link on sign-in", 'g.jsx(yl,{to:"/register",children:"Create Account"})]})', 'g.jsx(yl,{to:"/register",children:"Create Account"})]}),g.jsxs("p",{className:"auth__footer",children:["Want to see the space first? ",g.jsx(yl,{to:"/enquire",children:"Book a tour"})]})'],
  ["sidebar icons", 'BFcalcheck=Oe("calendar-check",BF_i6);const EO={', 'BFcalcheck=Oe("calendar-check",BF_i6),' + ICONS + "const EO={"],
  ["sidebar icon names", "CalendarCheck:BFcalcheck}", "CalendarCheck:BFcalcheck,Target:RGtarget,UserPlus:RGuserplus,Globe:RGglobe}"],
  ["CRM expands like Assets and Billing", '"/admin/access","/admin/users"])', '"/admin/access","/admin/users","/admin/crm/leads"])']
]);

// ---- quotation form: pre-fill from a lead -----------------------------------------------------
const qfile = fs.readdirSync(path.join(front, "assets")).find((f) => /^AdminQuotationPages-.*\.js$/.test(f));
const QFROM = '          setLines([{ ...emptyLine(), taxPercent: String(formOptions.defaultGstPercent) }]);\n        }\n\n      } catch (error) {\n        toast.error(errorMessage(error, "Could not open the quotation form."));';
const QTO =
  '          setLines([{ ...emptyLine(), taxPercent: String(formOptions.defaultGstPercent) }]);\n' +
  '          /* crm kit: "Send quotation" on a CRM lead opens this form as ?lead=<id>. The lead number goes in as\n' +
  '             the enquiry reference, which is how CRM links the quotation back to the lead. */\n' +
  '          const crmLeadId = new URLSearchParams(window.location.search).get("lead");\n' +
  "          if (crmLeadId) {\n" +
  "            try {\n" +
  "              const crm = (await unwrap(api.get(`/crm/leads/${Number(crmLeadId)}`))).row;\n" +
  '              const last10 = (s) => String(s || "").replace(/\\D/g, "").slice(-10);\n' +
  "              const match = customerPage.items.find((c) => last10(c.mobileNumber) && last10(c.mobileNumber) === last10(crm.mobile));\n" +
  "              setHeader((h) => ({\n" +
  "                ...h,\n" +
  '                customerId: match ? String(match.userId) : "",\n' +
  "                customerName: match ? match.fullName : crm.company || crm.name,\n" +
  "                referenceNumber: crm.leadNumber,\n" +
  "                subject: crm.lookingFor ? `${crm.lookingFor} at Regalis WorkSpace` : h.subject,\n" +
  '                internalNote: `From CRM lead ${crm.leadNumber}: ${crm.name}, ${crm.mobile}${crm.email ? ", " + crm.email : ""}.`,\n' +
  "              }));\n" +
  "            } catch {\n" +
  "              /* no CRM access, or the lead is not visible: an empty form, as before */\n" +
  "            }\n" +
  "          }\n" +
  '        }\n\n      } catch (error) {\n        toast.error(errorMessage(error, "Could not open the quotation form."));';
patch(qfile, [["prefill from ?lead=", QFROM, QTO]]);

// ---- dashboard: + Add lead -------------------------------------------------------------------
const dfile = fs.readdirSync(path.join(front, "assets")).find((f) => /^AdminDashboardPage-.*\.js$/.test(f));
const ADDLEAD =
  "\n/* crm kit: the front desk adds a lead from here without navigating two levels (CRM.LEAD.CREATE). */\n" +
  "function RGAddLead(){const[D,setD]=t.useState(null);return e.jsxs(\"div\",{style:{marginTop:\"var(--space-3)\"},children:[" +
  "e.jsx(\"button\",{type:\"button\",className:\"btn btn--primary btn--sm\",onClick:()=>import(\"./CrmPages-CRM1.js\").then(m=>setD(()=>m.AddLeadDialog)),children:\"+ Add lead\"})," +
  "D&&e.jsx(D,{onClose:()=>setD(null)})]})}\n";
patch(dfile, [
  ["Add lead button", '"no role assigned","."]})', '"no role assigned","."]}),UP("CRM.LEAD.CREATE")&&e.jsx(RGAddLead,{})'],
  ["Add lead component", "export{", ADDLEAD + "export{"]
]);

// ---- index.html: the visit tracker ------------------------------------------------------------
patchFile(path.join(front, "index.html"), "../index.html", [["crm-track.js", '<script src="/env.js"></script>', '<script src="/env.js"></script>\n    <script src="/crm-track.js" defer></script>']]);

// ---- sw.js -------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const f of changed) {
  const url = f.startsWith("../") ? f.slice(3) : `assets/${f}`;
  if (url === "index.html") continue;
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
