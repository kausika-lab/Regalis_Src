// Admin Invoices page: "Terms & Conditions" editor (02 Oct 2026).
//   node apply.mjs <frontend dir>
// A toolbar button (INVOICE.EDIT holders) opens a dialog that reads and saves the one
// Terms & Conditions text every invoice PDF prints at the end (GET/PUT /api/invoices/terms).
// Writes AdminFinancePages-IT1.js (the old chunk stays), points the index at it and
// bumps sw.js. Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "invoice-terms-1";
const OLD = "AdminFinancePages-azEGKuoi.js", NEW = "AdminFinancePages-IT1.js";
const A = (f) => path.join(front, "assets", f);

let page = fs.readFileSync(A(OLD), "utf8");
function edit(name, from, to) {
  const n = page.split(from).length - 1;
  if (n !== 1) throw new Error(`${OLD}: '${name}' expected 1 match, found ${n}`);
  page = page.replace(from, to);
  console.log(`OK  - ${NEW}: ${name}`);
}
edit("dialog state", "function xe(){const d=P(),", "function xe(){const[RGt,RGsetT]=n.useState(!1),d=P(),");
edit("toolbar button",
  'children:g?"Showing overdue":"Overdue only"})]})',
  'children:g?"Showing overdue":"Overdue only"}),E&&e.jsx(f,{variant:"outline",onClick:()=>RGsetT(!0),children:"Terms & Conditions"})]})');
edit("dialog",
  "y&&e.jsx(ee,{invoice:y,onClose:()=>a(null),onCancelled:()=>{a(null),o()}})]})}",
  "y&&e.jsx(ee,{invoice:y,onClose:()=>a(null),onCancelled:()=>{a(null),o()}}),RGt&&e.jsx(RGInvoiceTerms,{onClose:()=>RGsetT(!1)})]})}");
page += `
function RGInvoiceTerms({onClose:c}){const h=P(),[v,sv]=n.useState(""),[ld,sl]=n.useState(!0),[sg,ss]=n.useState(!1);n.useEffect(()=>{UW(HTTP.get("/invoices/terms")).then(r=>sv(r?.termsAndConditions??"")).catch(t=>h.error(t instanceof A?t.message:"Could not load the terms.")).finally(()=>sl(!1))},[]);const save=async()=>{ss(!0);try{await UW(HTTP.put("/invoices/terms",{termsAndConditions:v})),h.success("Terms saved. Every invoice PDF now prints them."),c()}catch(t){h.error(t instanceof A?t.message:"Could not save the terms.")}finally{ss(!1)}};return e.jsx(R,{open:!0,title:"Invoice Terms & Conditions",onClose:c,footer:e.jsxs(e.Fragment,{children:[e.jsx(f,{variant:"ghost",onClick:c,children:"Cancel"}),e.jsx(f,{onClick:save,loading:sg,disabled:ld,children:"Save"})]}),children:e.jsxs("div",{className:"form-stack",children:[e.jsx("p",{style:{margin:0,color:"var(--text-muted)",fontSize:"var(--font-size-sm)"},children:"Printed at the end of every invoice PDF, for every customer. Invoices raised from a quotation print that quotation's own terms. Leave empty to print none."}),e.jsx("textarea",{id:"invoice-terms",rows:10,maxLength:4000,className:"field__input",disabled:ld,placeholder:ld?"Loading…":"1. Payment is due on receipt of this invoice.",value:v,onChange:t=>sv(t.target.value)}),e.jsx("div",{style:{textAlign:"right",color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:\`\${v.length} / 4000\`})]})})}
`;
fs.writeFileSync(A(NEW), page, "utf8");

const idx = "index-DI5X4c9f.js";
const t = fs.readFileSync(A(idx), "utf8");
if (t.includes(OLD)) {
  const n = t.split(OLD).length - 1;
  if (n < 2) throw new Error(`${idx}: expected import + mapDeps references to ${OLD}, found ${n}`);
  fs.writeFileSync(A(idx), t.replaceAll(OLD, NEW), "utf8");
  console.log(`OK  - ${idx}: import + mapDeps -> ${NEW}`);
} else if (t.includes(NEW)) console.log(`OK  - ${idx}: already points at ${NEW}`);
else throw new Error(`${idx}: no reference to ${OLD}`);

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
sw = sw.replace(/\{url:"assets\/index-DI5X4c9f\.js",revision:(null|"[^"]*")\}/, `{url:"assets/index-DI5X4c9f.js",revision:"${REV}"}`);
if (!sw.includes(`assets/${NEW}`)) {
  sw = sw.replace(/(\{url:"assets\/index-DI5X4c9f\.js",revision:"[^"]*"\})/, `$1,{url:"assets/${NEW}",revision:"${REV}"}`);
}
if (!sw.includes(`assets/${NEW}`) || !sw.includes(`index-DI5X4c9f.js",revision:"${REV}"`)) throw new Error("sw.js not updated");
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
