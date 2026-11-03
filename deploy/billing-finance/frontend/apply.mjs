// Billing & Finance build (Nov 2026) - the SPA side.
//   node apply.mjs <frontend dir>
//
// 1. Installs the new chunks: BillingShared-BF1, BillingMoneyIn-BF1, BillingPayables-BF1,
//    BillingBank-BF1, BillingCompliance-BF1 and AdminExpensesPage-EX3.
// 2. index-DI5X4c9f.js:
//    - Invoices, Payments, Suppliers, Transactions and Reconciliation render the new pages
//      (same routes); Expenses loads EX3;
//    - new routes: invoices/:id, purchase-bills (+ new, :id, :id/edit), vendor-payments,
//      petty-cash, gst, tds, compliance, period-locks, ledgers, ledger/:type/:id;
//    - breadcrumb labels for the new sections, and the sidebar icons the new MST_Menu rows name;
//    - the Billing & Finance sidebar group draws route-less children as band labels
//      (MONEY IN, CASH DESK, MONEY OUT, BANK, COMPLIANCE); a band with nothing under it is hidden.
// 3. Purchase order detail: "Create bill" (approved / sent / received POs).
// 4. Customers list and customer profile: "Ledger".
// 5. Bank Accounts: "Import statement" on each account.
//
// Patches the existing chunks in place (keeps their names), bumps their sw.js revisions and adds
// no-cache entries to web.config. Idempotent: a file that has the marker is skipped.
import fs from "fs";
import path from "path";
import { fileURLToPath } from "url";

const [front] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const HERE = path.dirname(fileURLToPath(import.meta.url));
const REV = "billing-finance-1";
const MARK = "BF_BILLING";

function edit(file, edits) {
  const raw = fs.readFileSync(file, "utf8");
  const crlf = raw.includes("\r\n");
  let t = crlf ? raw.replace(/\r\n/g, "\n") : raw;
  const name = path.basename(file);
  if (t.includes(MARK)) { console.log(`OK  - ${name}: already patched`); return; }
  for (const [what, fn] of edits) { t = fn(t, `${name}: ${what}`); console.log(`OK  - ${name}: ${what}`); }
  fs.writeFileSync(file, crlf ? t.replace(/\n/g, "\r\n") : t, "utf8");
}
const once = (t, from, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.indexOf(from);
};
const swap = (from, to) => (t, where) => { once(t, from, where); return t.replace(from, to); };
const after = (anchor, insert) => (t, where) => { const at = once(t, anchor, where) + anchor.length; return t.slice(0, at) + insert + t.slice(at); };
const A = (f) => path.join(front, "assets", f);

// --- 1. New chunks ------------------------------------------------------------------------
const CHUNKS = ["BillingShared-BF1.js", "BillingMoneyIn-BF1.js", "BillingPayables-BF1.js", "BillingBank-BF1.js", "BillingCompliance-BF1.js", "AdminExpensesPage-EX3.js"];
for (const f of CHUNKS) {
  fs.copyFileSync(path.join(HERE, "assets", f), A(f));
  console.log(`OK  - ${f} installed`);
}

// --- 2. index -----------------------------------------------------------------------------
const DEPS = "__vite__mapDeps([52,17,29,6,7,23,12,13,10,14,41,25,8,20])";
const lazy = (name, file, exp) => `,${name}=fe(()=>ce(()=>import("./${file}"),${DEPS}),"${exp}")`;
const LAZY = [
  ["BF_invoices", "BillingMoneyIn-BF1.js", "AdminInvoicesPageBF"],
  ["BF_invoice", "BillingMoneyIn-BF1.js", "AdminInvoiceDetailPage"],
  ["BF_payments", "BillingMoneyIn-BF1.js", "AdminPaymentsPageBF"],
  ["BF_ledger", "BillingMoneyIn-BF1.js", "AdminPartyLedgerPage"],
  ["BF_ledgers", "BillingMoneyIn-BF1.js", "AdminLedgersPage"],
  ["BF_bills", "BillingPayables-BF1.js", "AdminPurchaseBillsPage"],
  ["BF_billForm", "BillingPayables-BF1.js", "AdminPurchaseBillFormPage"],
  ["BF_bill", "BillingPayables-BF1.js", "AdminPurchaseBillDetailPage"],
  ["BF_vendorPayments", "BillingPayables-BF1.js", "AdminVendorPaymentsPage"],
  ["BF_suppliers", "BillingPayables-BF1.js", "AdminSuppliersPageBF"],
  ["BF_transactions", "BillingBank-BF1.js", "AdminBankTransactionsPageBF"],
  ["BF_recon", "BillingBank-BF1.js", "AdminReconciliationPageBF"],
  ["BF_gst", "BillingCompliance-BF1.js", "AdminGstPage"],
  ["BF_tds", "BillingCompliance-BF1.js", "AdminTdsPage"],
  ["BF_compliance", "BillingCompliance-BF1.js", "AdminCompliancePage"],
  ["BF_locks", "BillingCompliance-BF1.js", "AdminPeriodLocksPage"],
  ["BF_petty", "BillingCompliance-BF1.js", "AdminPettyCashPage"]
].map(([n, f, x]) => lazy(n, f, x)).join("");

const guard = (perm, children) => `g.jsx(P,{element:g.jsx(Ne,{permission:"${perm}"}),children:${children}})`;
const route = (p, el) => `g.jsx(P,{path:"${p}",element:g.jsx(${el},{})})`;
const NEW_ROUTES = [
  guard("INVOICE.VIEW", route("invoices/:id", "BF_invoice")),
  `g.jsxs(P,{element:g.jsx(Ne,{permission:"BILL.VIEW"}),children:[${route("purchase-bills", "BF_bills")},${route("purchase-bills/:id", "BF_bill")},${route("vendor-payments", "BF_vendorPayments")},` +
    `${guard("BILL.CREATE", `[${route("purchase-bills/new", "BF_billForm")},${route("purchase-bills/:id/edit", "BF_billForm")}]`)}]})`,
  guard("PETTYCASH.MANAGE", route("petty-cash", "BF_petty")),
  guard("GST.VIEW", route("gst", "BF_gst")),
  guard("TDS.VIEW", route("tds", "BF_tds")),
  `g.jsxs(P,{element:g.jsx(Ne,{permission:"COMPLIANCE.VIEW"}),children:[${route("compliance", "BF_compliance")},${route("period-locks", "BF_locks")}]})`,
  route("ledgers", "BF_ledgers"),
  route("ledger/:type/:id", "BF_ledger")
].join(",");

const ICONS =
  `,BF_i1=[["circle",{cx:"8",cy:"8",r:"6",key:"bfc1"}],["path",{d:"M18.09 10.37A6 6 0 1 1 10.34 18",key:"bfc2"}],["path",{d:"M7 6h1v4",key:"bfc3"}],["path",{d:"m16.71 13.88.7.71-2.82 2.82",key:"bfc4"}]],BFcoins=Oe("coins",BF_i1)` +
  `,BF_i2=[["path",{d:"M15 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V7Z",key:"bff1"}],["path",{d:"M14 2v4a2 2 0 0 0 2 2h4",key:"bff2"}],["path",{d:"M10 13H8M16 17H8M16 13h-2",key:"bff3"}]],BFfile=Oe("file-text",BF_i2)` +
  `,BF_i3=[["path",{d:"M14.536 21.686a.5.5 0 0 0 .937-.024l6.5-19a.496.496 0 0 0-.635-.635l-19 6.5a.5.5 0 0 0-.024.937l7.93 3.18a2 2 0 0 1 1.112 1.11z",key:"bfs1"}],["path",{d:"m21.854 2.147-10.94 10.939",key:"bfs2"}]],BFsend=Oe("send",BF_i3)` +
  `,BF_i4=[["line",{x1:"19",x2:"5",y1:"5",y2:"19",key:"bfp1"}],["circle",{cx:"6.5",cy:"6.5",r:"2.5",key:"bfp2"}],["circle",{cx:"17.5",cy:"17.5",r:"2.5",key:"bfp3"}]],BFpercent=Oe("percent",BF_i4)` +
  `,BF_i5=[["path",{d:"m16 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1Z",key:"bfk1"}],["path",{d:"m2 16 3-8 3 8c-.87.65-1.92 1-3 1s-2.13-.35-3-1Z",key:"bfk2"}],["path",{d:"M7 21h10",key:"bfk3"}],["path",{d:"M12 3v18",key:"bfk4"}],["path",{d:"M3 7h2c2 0 5-1 7-2 2 1 5 2 7 2h2",key:"bfk5"}]],BFscale=Oe("scale",BF_i5)` +
  `,BF_i6=[["path",{d:"M8 2v4",key:"bfa1"}],["path",{d:"M16 2v4",key:"bfa2"}],["rect",{width:"18",height:"18",x:"3",y:"4",rx:"2",key:"bfa3"}],["path",{d:"M3 10h18",key:"bfa4"}],["path",{d:"m9 16 2 2 4-4",key:"bfa5"}]],BFcalcheck=Oe("calendar-check",BF_i6)`;

edit(A("index-DI5X4c9f.js"), [
  ["lazy pages", after(`"AdminCashRegisterPage")`, `/*${MARK}*/${LAZY}`)],
  ["expenses EX3", swap(`import("./AdminExpensesPage-EX2.js")`, `import("./AdminExpensesPage-EX3.js")`)],
  ["invoices page", swap(`g.jsx(P,{path:"invoices",element:g.jsx(c3,{})})`, `g.jsx(P,{path:"invoices",element:g.jsx(BF_invoices,{})})`)],
  ["payments page", swap(`g.jsx(P,{path:"payments",element:g.jsx(f3,{})})`, `g.jsx(P,{path:"payments",element:g.jsx(BF_payments,{})})`)],
  ["suppliers page", swap(`g.jsx(P,{path:"suppliers",element:g.jsx(A3,{})})`, `g.jsx(P,{path:"suppliers",element:g.jsx(BF_suppliers,{})})`)],
  ["transactions page", swap(`g.jsx(P,{path:"transactions",element:g.jsx(j3,{})})`, `g.jsx(P,{path:"transactions",element:g.jsx(BF_transactions,{})})`)],
  ["reconciliation page + new routes", swap(`g.jsx(P,{path:"reconciliation",element:g.jsx(N3,{})})]})]})`,
    `g.jsx(P,{path:"reconciliation",element:g.jsx(BF_recon,{})})]}),${NEW_ROUTES}]})`)],
  ["breadcrumb labels", swap(`reconciliation:"Reconciliation"}`,
    `reconciliation:"Reconciliation","purchase-bills":"Purchase Bills","vendor-payments":"Vendor Payments","petty-cash":"Petty Cash",gst:"GST",tds:"TDS",compliance:"Compliance Calendar","period-locks":"Period lock",ledgers:"Ledgers",ledger:"Ledgers"}`)],
  ["icons", after(`PLcalc=Oe("calculator",PAY_LATER_i2)`, ICONS)],
  ["icon map", swap(`Banknote:PLbanknote,Calculator:PLcalc}`, `Banknote:PLbanknote,Calculator:PLcalc,Coins:BFcoins,FileText:BFfile,Send:BFsend,Percent:BFpercent,Scale:BFscale,CalendarCheck:BFcalcheck}`)],
  ["sidebar bands", swap(
    `children:s.map(n=>{const o=SO(n.icon);return g.jsxs(y0,{to:n.route,className:"portal__subnav-link"`,
    `children:(t.children||[]).filter((n,ix,all)=>n.route||(all[ix+1]&&all[ix+1].route)).map(n=>{if(!n.route)return g.jsx("div",{className:"portal__subnav-band",role:"presentation",style:{padding:"10px 12px 2px",fontSize:"10px",fontWeight:700,letterSpacing:".08em",textTransform:"uppercase",color:"var(--text-muted)"},children:n.displayName},n.menuId);const o=SO(n.icon);return g.jsxs(y0,{to:n.route,className:"portal__subnav-link"`)]
]);

// --- 3. Purchase order detail: Create bill ------------------------------------------------
edit(A("AdminPurchaseOrderPages-C1FTHC0p.js"), [
  ["create bill", after(
    `            can.receive && e.jsx(x, { onClick: () => run.receive(order), children: "Receive goods" }),\n`,
    `            /* ${MARK}: a supplier bill against this PO (deploy/billing-finance) */\n` +
    `            [S.approved, S.sent, S.partial, S.received].includes(order.status) && e.jsx(x, { variant: "outline", onClick: () => navigate(\`/admin/billing/purchase-bills/new?po=\${order.purchaseOrderId}\`), children: "Create bill" }),\n`)]
]);

// --- 4. Customers: Ledger -----------------------------------------------------------------
edit(A("AdminCustomersPages-IA5T7i_C.js"), [
  ["ledger on the list", swap(
    `                      "aria-label": "View " + row.fullName
                    },
                    "View"
                  )`,
    `                      "aria-label": "View " + row.fullName
                    },
                    "View"
                  ),
                  /* ${MARK}: the customer's ledger (deploy/billing-finance) */
                  el(
                    Button,
                    {
                      size: "sm",
                      variant: "ghost",
                      onClick: (event) => {
                        event.stopPropagation();
                        navigate("/admin/billing/ledger/customer/" + row.userId);
                      }
                    },
                    "Ledger"
                  )`)],
  ["ledger on the profile", swap(
    `      actions: el(
        Button,
        { variant: "outline", onClick: () => navigate("/admin/customers") },
        el(IconArrowLeft, { size: 16 }),
        " Back to Customers"
      )`,
    `      actions: el(
        F,
        null,
        el(Button, { variant: "outline", onClick: () => navigate("/admin/billing/ledger/customer/" + customerId) }, "Ledger"),
        el(
          Button,
          { variant: "outline", onClick: () => navigate("/admin/customers") },
          el(IconArrowLeft, { size: 16 }),
          " Back to Customers"
        )
      )`)]
]);

// --- 5. Bank accounts: Import statement ---------------------------------------------------
edit(A("AdminBankPages-BZwouxUO.js"), [
  ["import statement", swap(
    `children:[x&&e.jsx(p,{size:"sm",variant:"outline",onClick:()=>b(o),children:"Edit"})`,
    `children:[/*${MARK}*/e.jsx(p,{size:"sm",variant:"outline",onClick:()=>{window.history.pushState({},"","/admin/billing/transactions?import="+o.bankAccountId);window.dispatchEvent(new PopStateEvent("popstate"))},children:"Import statement"}),x&&e.jsx(p,{size:"sm",variant:"outline",onClick:()=>b(o),children:"Edit"})`)]
]);

// --- sw.js: re-fetch what changed in place (entries that exist) -----------------------
const PATCHED = ["assets/index-DI5X4c9f.js", "assets/AdminPurchaseOrderPages-C1FTHC0p.js", "assets/AdminCustomersPages-IA5T7i_C.js", "assets/AdminBankPages-BZwouxUO.js"];
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const url of PATCHED) {
  const entry = new RegExp(`\\{url:"${url.replace(/[.\-/]/g, "\\$&")}",revision:(null|"[^"]*")\\}`);
  if (!entry.test(sw)) { console.log(`OK  - sw.js: ${url} not precached`); continue; }
  sw = sw.replace(entry, `{url:"${url}",revision:"${REV}"}`);
}
for (const [, url] of sw.matchAll(/\{url:"([^"]+)"/g)) {
  if (!fs.existsSync(path.join(front, url))) throw new Error(`sw.js lists ${url}, which is not on disk`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);

// --- web.config: files patched in place must revalidate --------------------------------
const cfgFile = path.join(front, "web.config");
let cfg = fs.readFileSync(cfgFile, "utf8");
for (const p of PATCHED) {
  if (cfg.includes(`<location path="${p}">`)) continue;
  cfg = cfg.replace("</configuration>",
    `  <location path="${p}">\n    <system.webServer>\n      <staticContent>\n        <clientCache cacheControlMode="DisableCache" />\n      </staticContent>\n    </system.webServer>\n  </location>\n</configuration>`);
  console.log(`OK  - web.config: no-cache ${p}`);
}
fs.writeFileSync(cfgFile, cfg, "utf8");
console.log("DONE");
