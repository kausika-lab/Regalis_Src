/**
 * CRM — Leads and Web Enquiries, admin portal (19 Nov 2026, kit deploy/crm).
 * Screens: Leads (list + pipeline), One lead, Follow-ups, Website Visitors, CRM Reports,
 * CRM Settings, and the Add-lead dialog (also opened from the Dashboard).
 *
 * Data: /api/crm/* (see deploy/crm/README.md). Hand-maintained chunk: the routes in
 * index-DI5X4c9f.js import it by name; bump its revision in sw.js when it changes.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as X,
  p as Button,
  I as Input,
  F as Select,
  D as unwrap,
  E as http,
  u as useNavigate,
  Y as useParams,
  g as Link,
  G as Spinner
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";

/* ---------------------------------------------------------------------------------------- */
/* helpers                                                                                     */
/* ---------------------------------------------------------------------------------------- */

function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  const flat = kids.flat(Infinity).filter((k) => k !== null && k !== void 0 && k !== false && k !== "");
  if (flat.length === 1) p.children = flat[0];
  else if (flat.length > 1) p.children = flat;
  return flat.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}
const F = React.Fragment;

const CSS = `
.crm-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(170px,1fr));gap:var(--space-3);margin-bottom:var(--space-4)}
.crm-card{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3) var(--space-4);text-align:left;cursor:pointer;font:inherit;color:inherit;box-shadow:var(--shadow-sm)}
.crm-card:hover{background:var(--surface-hover)}
.crm-card[disabled]{cursor:default}
.crm-card__label{font-size:var(--font-size-xs);color:var(--text-secondary);text-transform:uppercase;letter-spacing:.04em;font-weight:600}
.crm-card__value{font-size:1.6rem;font-weight:800;margin:4px 0 2px;font-family:var(--font-display)}
.crm-card__sub{font-size:var(--font-size-xs);color:var(--text-muted)}
.crm-card--red .crm-card__value{color:var(--color-danger)}
.crm-filters{display:grid;grid-template-columns:2fr repeat(4,1fr) auto;gap:var(--space-3);align-items:end;margin-bottom:var(--space-3)}
@media (max-width:900px){.crm-filters{grid-template-columns:1fr 1fr}}
.crm-table{width:100%;border-collapse:collapse}
.crm-table th{text-align:left;font-size:var(--font-size-xs);text-transform:uppercase;letter-spacing:.04em;color:var(--text-secondary);padding:8px 10px;border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.crm-table th button{all:unset;cursor:pointer}
.crm-table td{padding:10px;border-bottom:1px solid var(--border-subtle);vertical-align:top;font-size:var(--font-size-sm)}
.crm-row--new td{background:rgb(var(--accent-rgb) / 9%)}
.crm-row--late td:first-child{box-shadow:inset 4px 0 0 var(--color-danger)}
.crm-muted{color:var(--text-muted);font-size:var(--font-size-xs)}
.crm-red{color:var(--color-danger);font-weight:700}
.crm-pill{display:inline-block;padding:2px 9px;border-radius:999px;border:1px solid var(--border-strong);font-size:var(--font-size-xs);color:var(--text-secondary);white-space:nowrap}
.crm-note{font-size:var(--font-size-xs);color:var(--text-muted);margin-top:var(--space-2)}
.crm-chips{display:flex;flex-wrap:wrap;gap:8px}
.crm-chip{border:1px solid var(--border-strong);background:var(--surface-card);color:var(--text-primary);border-radius:999px;padding:7px 14px;font:inherit;font-size:var(--font-size-sm);cursor:pointer}
.crm-chip--on{background:var(--accent);border-color:var(--accent);color:var(--text-on-gold);font-weight:700}
.crm-panel{border-radius:var(--radius-md);padding:var(--space-3) var(--space-4);margin:var(--space-3) 0;font-size:var(--font-size-sm);line-height:1.5}
.crm-panel--amber{background:rgb(217 119 6 / 10%);border:1px solid rgb(217 119 6 / 45%)}
.crm-panel--green{background:rgb(22 163 74 / 9%);border:1px solid rgb(22 163 74 / 40%)}
.crm-panel--grey{background:var(--surface-inset);border:1px solid var(--border-subtle)}
.crm-panel--red{background:rgb(220 38 38 / 8%);border:1px solid rgb(220 38 38 / 40%)}
.crm-grid2{display:grid;grid-template-columns:1fr 1fr;gap:var(--space-3)}
.crm-grid3{display:grid;grid-template-columns:repeat(3,1fr);gap:var(--space-3)}
@media (max-width:700px){.crm-grid2,.crm-grid3{grid-template-columns:1fr}}
.crm-textarea{width:100%;box-sizing:border-box;min-height:80px;font:inherit;padding:8px 10px;border:1px solid var(--border-strong);border-radius:var(--radius-sm);background:var(--surface-card);color:var(--text-primary)}
.crm-section-title{font-size:var(--font-size-sm);font-weight:700;margin:var(--space-4) 0 var(--space-2)}
.crm-lead{display:grid;grid-template-columns:minmax(0,1fr) 320px;gap:var(--space-4);align-items:start}
@media (max-width:1050px){.crm-lead{grid-template-columns:1fr}}
.crm-tabs{display:flex;gap:4px;border-bottom:1px solid var(--border-subtle);margin-bottom:var(--space-3);overflow-x:auto}
.crm-tab{all:unset;cursor:pointer;padding:8px 14px;font-size:var(--font-size-sm);color:var(--text-secondary);border-bottom:2px solid transparent;white-space:nowrap}
.crm-tab--on{color:var(--text-primary);border-bottom-color:var(--accent);font-weight:700}
.crm-dl{display:grid;grid-template-columns:160px minmax(0,1fr);gap:6px 12px;font-size:var(--font-size-sm)}
.crm-dl dt{color:var(--text-secondary)}
.crm-dl dd{margin:0}
.crm-quote{background:rgb(var(--accent-rgb) / 10%);border-left:3px solid var(--accent);padding:8px 12px;border-radius:4px;white-space:pre-wrap}
.crm-timeline{list-style:none;margin:0;padding:0}
.crm-timeline li{position:relative;padding:0 0 12px 22px;font-size:var(--font-size-sm)}
.crm-timeline li:before{content:"";position:absolute;left:5px;top:6px;width:8px;height:8px;border-radius:50%;background:var(--border-strong)}
.crm-timeline li.crm-gold:before{background:var(--accent);width:10px;height:10px;left:4px}
.crm-timeline li.crm-gold{font-weight:700}
.crm-rail>section{margin-bottom:var(--space-3)}
.crm-board{display:grid;grid-auto-flow:column;grid-auto-columns:minmax(230px,1fr);gap:var(--space-3);overflow-x:auto;padding-bottom:8px}
.crm-col{background:var(--surface-inset);border-radius:var(--radius-md);padding:var(--space-2);min-height:200px}
.crm-col--over{outline:2px dashed var(--accent)}
.crm-col h3{margin:4px 6px 2px;font-size:var(--font-size-sm)}
.crm-kcard{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-sm);padding:8px 10px;margin:8px 0;cursor:grab;font-size:var(--font-size-sm);box-shadow:var(--shadow-sm)}
.crm-kcard--late{border-left:4px solid var(--color-danger)}
.crm-kcard--new{background:rgb(var(--accent-rgb) / 9%)}
.crm-fu{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:var(--space-3)}
@media (max-width:1000px){.crm-fu{grid-template-columns:1fr}}
.crm-fucard{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-sm);padding:10px 12px;margin-bottom:8px;font-size:var(--font-size-sm)}
.crm-fucard--late{background:rgb(220 38 38 / 7%);border-left:4px solid var(--color-danger)}
.crm-bar{height:10px;border-radius:5px;background:var(--accent);min-width:2px}
.crm-bar-track{background:var(--surface-inset);border-radius:5px;width:100%}
.crm-rowlist{list-style:none;margin:0;padding:0}
.crm-rowlist li{display:flex;gap:8px;align-items:center;padding:8px 0;border-bottom:1px solid var(--border-subtle);flex-wrap:wrap}
.crm-rowlist li>.crm-grow{flex:1;min-width:180px}
.crm-toggle{display:inline-flex;border:1px solid var(--border-strong);border-radius:999px;overflow:hidden}
.crm-toggle button{all:unset;cursor:pointer;padding:6px 14px;font-size:var(--font-size-sm)}
.crm-toggle button.on{background:var(--accent);color:var(--text-on-gold);font-weight:700}
.crm-scroll{overflow-x:auto}
.modal:has(.crm-wide){max-width:min(780px,calc(100vw - 32px))}
.modal:has(.crm-mid){max-width:min(600px,calc(100vw - 32px))}
.modal__body .crm-grid3,.modal__body .crm-grid2{min-width:0}
.modal__body .field{min-width:0}
`;
function useCss() {
  React.useEffect(() => {
    if (document.getElementById("rg-crm-css")) return;
    const s = document.createElement("style");
    s.id = "rg-crm-css";
    s.textContent = CSS;
    document.head.appendChild(s);
  }, []);
}

const IST = "Asia/Kolkata";
const fmtDT = (u) =>
  u ? new Intl.DateTimeFormat("en-IN", { timeZone: IST, day: "numeric", month: "short", hour: "2-digit", minute: "2-digit" }).format(new Date(u)) : "—";
const fmtD = (u) => (u ? new Intl.DateTimeFormat("en-IN", { timeZone: IST, day: "numeric", month: "short", year: "numeric" }).format(new Date(u)) : "—");
const fmtDay = (d) => (d ? new Intl.DateTimeFormat("en-IN", { day: "numeric", month: "short", year: "numeric" }).format(new Date(d + "T00:00:00")) : "—");
const money = (n) => (n == null ? "—" : "₹" + Number(n).toLocaleString("en-IN", { maximumFractionDigits: 0 }));
const toIstInput = (u) => (u ? new Date(new Date(u).getTime() + 330 * 60000).toISOString().slice(0, 16) : "");
const fromIstInput = (v) => (v ? new Date(v + ":00+05:30").toISOString() : null);
const istTodayInput = (days, hh) => {
  const d = new Date(Date.now() + 330 * 60000 + days * 864e5);
  return d.toISOString().slice(0, 10) + "T" + (hh || "10:00");
};
const mins = (m) => {
  if (m == null) return "";
  if (m < 60) return `${m} min`;
  if (m < 1440) return `${Math.floor(m / 60)} h ${m % 60 ? (m % 60) + " min" : ""}`.trim();
  const d = Math.floor(m / 1440);
  return `${d} day${d === 1 ? "" : "s"}`;
};
const errMsg = (e, fallback) => (e instanceof ApiError ? e.message : fallback);
const errCode = (e) => (e instanceof ApiError ? e.errors?.[0]?.code : null);
const MUTED = { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" };
const digits = (s) => (s || "").replace(/\D/g, "");

let optionsCache = null;
function useOptions() {
  const toast = useToast();
  const [opt, setOpt] = React.useState(optionsCache);
  const load = React.useCallback(async () => {
    try {
      optionsCache = await unwrap(http.get("/crm/options"));
      setOpt(optionsCache);
    } catch (e) {
      toast.error(errMsg(e, "Could not load CRM."));
    }
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);
  return [opt, load];
}

function StatusPill({ row }) {
  return el(Badge, { tone: row.statusColour || "neutral" }, row.status);
}

function Textarea({ label, hint, value, onChange, rows, placeholder, required, maxLength }) {
  const id = React.useId();
  return el(
    "div",
    { className: "field" },
    el("label", { className: "field__label", htmlFor: id }, label, required ? el("span", { className: "field__required" }, " *") : null),
    el("textarea", { id, className: "crm-textarea", rows: rows || 3, value, onChange, placeholder, maxLength }),
    hint ? el("span", { className: "field__hint" }, hint) : null
  );
}

function Stat({ label, value, sub, red, onClick }) {
  return el(
    "button",
    { type: "button", className: "crm-card" + (red ? " crm-card--red" : ""), onClick, disabled: !onClick },
    el("div", { className: "crm-card__label" }, label),
    el("div", { className: "crm-card__value" }, value),
    sub ? el("div", { className: "crm-card__sub" }, sub) : null
  );
}

async function download(path, params, name) {
  const r = await http.get(path, { params, responseType: "blob" });
  const url = URL.createObjectURL(r.data);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  a.click();
  URL.revokeObjectURL(url);
}

/* ---------------------------------------------------------------------------------------- */
/* Add a lead by hand (section 5)                                                              */
/* ---------------------------------------------------------------------------------------- */

const HEARD = ["Google", "Instagram", "A friend or existing member", "Walked past", "JustDial", "Did not say"];

export function AddLeadDialog({ onClose, onSaved }) {
  useCss();
  const toast = useToast();
  const navigate = useNavigate();
  const [opt] = useOptions();
  const blank = () => ({
    sourceId: null, name: "", mobile: "", email: "", company: "", walkIn: toIstInput(new Date().toISOString()), shownBy: "",
    referredBy: null, referredText: "", waRef: "", lookingForId: "", people: "", budget: "", locationId: "", neededFrom: "",
    heard: "", said: "", assign: "me", followUp: istTodayInput(1, "10:00"), followWhat: "", followWhatTouched: false
  });
  const [f, setF] = React.useState(blank);
  const [dup, setDup] = React.useState(null);
  const [allowDup, setAllowDup] = React.useState(false);
  const [busy, setBusy] = React.useState("");
  const [custHits, setCustHits] = React.useState([]);
  const set = (patch) => setF((x) => ({ ...x, ...patch }));

  const sources = (opt?.sources || []).filter((s) => !s.isWebsite && s.isActive);
  const src = sources.find((s) => s.sourceId === f.sourceId);
  const looking = (opt?.lookingFor || []).filter((l) => l.isActive);
  const lfName = looking.find((l) => String(l.lookingForId) === String(f.lookingForId))?.name;
  const team = opt?.team || [];
  const me = opt?.me;

  React.useEffect(() => {
    if (!f.followWhatTouched) set({ followWhat: lfName ? `Call back about the ${lfName.toLowerCase()}` : "Call back" });
  }, [lfName]);

  // The duplicate check: as soon as a mobile is complete (10 digits), and on an email.
  const check = React.useCallback(async (mobile, email) => {
    if (digits(mobile).length < 10 && !(email || "").includes("@")) {
      setDup(null);
      return;
    }
    try {
      const r = await unwrap(http.post("/crm/leads/check-duplicate", { mobile, email: email || null }));
      setDup(r.leads.length || r.customers.length ? r : null);
      setAllowDup(false);
    } catch {
      /* the save re-checks */
    }
  }, []);
  React.useEffect(() => {
    const t = setTimeout(() => check(f.mobile, f.email), 400);
    return () => clearTimeout(t);
  }, [f.mobile, f.email]);

  React.useEffect(() => {
    if (src?.code !== "REFERRAL" || f.referredText.trim().length < 2 || f.referredBy) {
      setCustHits([]);
      return;
    }
    const t = setTimeout(async () => {
      try {
        setCustHits(await unwrap(http.get("/crm/customers", { params: { search: f.referredText } })));
      } catch {
        setCustHits([]);
      }
    }, 300);
    return () => clearTimeout(t);
  }, [f.referredText, src?.code]);

  const ownerName =
    f.assign === "me" ? "you" : f.assign === "rules" ? "whoever the assignment rules pick" : team.find((t) => String(t.userId) === f.assign)?.name || "—";
  const status = src && (src.code === "PHONE" || src.code === "WALKIN") ? "Contacted" : "New";

  const save = async (another) => {
    setBusy(another ? "another" : "save");
    try {
      const body = {
        sourceId: f.sourceId,
        name: f.name,
        mobile: f.mobile,
        email: f.email || null,
        company: f.company || null,
        walkInUtc: src?.code === "WALKIN" ? fromIstInput(f.walkIn) : null,
        shownByUserId: src?.code === "WALKIN" && f.shownBy ? Number(f.shownBy) : null,
        referredByCustomerId: src?.code === "REFERRAL" ? f.referredBy?.customerId ?? null : null,
        whatsAppRef: src?.code === "WHATSAPP" ? f.waRef || null : null,
        lookingForId: f.lookingForId ? Number(f.lookingForId) : null,
        peopleCount: f.people ? Number(f.people) : null,
        budgetText: f.budget || null,
        locationId: f.locationId ? Number(f.locationId) : null,
        neededFrom: f.neededFrom || null,
        heardFrom: f.heard || null,
        whatTheySaid: f.said || null,
        assignTo: f.assign,
        followUpUtc: f.followUp ? fromIstInput(f.followUp) : null,
        followUpWhat: f.followUp ? f.followWhat : null,
        allowDuplicate: allowDup
      };
      const lead = await unwrap(http.post("/crm/leads", body));
      toast.success(`Lead ${lead.row.leadNumber} created.`);
      onSaved && onSaved(lead);
      if (another) {
        setF((x) => ({ ...blank(), sourceId: x.sourceId, assign: x.assign }));
        setDup(null);
        setAllowDup(false);
      } else {
        onClose();
        navigate(`/admin/crm/leads/${lead.row.leadId}`);
      }
    } catch (e) {
      if (e instanceof ApiError && e.status === 409 && e.response?.data) {
        setDup(e.response.data);
        toast.error("We already have this number or email. Check the amber panel.");
      } else toast.error(errMsg(e, "Could not save the lead."));
    } finally {
      setBusy("");
    }
  };

  const canSave = f.sourceId && f.name.trim() && f.mobile.trim() && (!dup || allowDup);

  return el(
    Modal,
    {
      open: true,
      title: "Add a lead",
      onClose: () => busy || onClose(),
      footer: el(
        F,
        null,
        el(Button, { variant: "ghost", disabled: !!busy, onClick: onClose }, "Cancel"),
        el(Button, { variant: "outline", loading: busy === "another", disabled: !canSave || !!busy, onClick: () => save(true) }, "Save and add another"),
        el(Button, { variant: "primary", loading: busy === "save", disabled: !canSave || !!busy, onClick: () => save(false) }, "Save lead")
      )
    },
    !opt
      ? el(Spinner, { center: true })
      : el(
          "div",
          { className: "form-stack crm-wide" },
          el("div", { className: "crm-section-title", style: { marginTop: 0 } }, "How did they reach us", el("span", { className: "field__required" }, " *")),
          el(
            "div",
            { className: "crm-chips", role: "radiogroup", "aria-label": "How did they reach us" },
            sources.map((s) =>
              el(
                "button",
                { key: s.sourceId, type: "button", role: "radio", "aria-checked": f.sourceId === s.sourceId, className: "crm-chip" + (f.sourceId === s.sourceId ? " crm-chip--on" : ""), onClick: () => set({ sourceId: s.sourceId }) },
                s.name
              )
            )
          ),
          src?.code === "WALKIN" &&
            el(
              "div",
              { className: "crm-grid2" },
              el(Input, { label: "Visited on", type: "datetime-local", value: f.walkIn, onChange: (e) => set({ walkIn: e.target.value }) }),
              el(Select, { label: "Shown around by", placeholder: "—", value: f.shownBy, options: team.map((t) => ({ value: String(t.userId), label: t.name })), onChange: (e) => set({ shownBy: e.target.value }) })
            ),
          src?.code === "REFERRAL" &&
            el(
              "div",
              null,
              f.referredBy
                ? el("div", { className: "crm-panel crm-panel--grey" }, "Referred by ", el("b", null, f.referredBy.name), " ", el(Button, { size: "sm", variant: "ghost", onClick: () => set({ referredBy: null, referredText: "" }) }, "Change"))
                : el(Input, { label: "Referred by", hint: "Search existing customers — so you can later see which members bring you business.", value: f.referredText, onChange: (e) => set({ referredText: e.target.value }) }),
              custHits.length > 0 &&
                el(
                  "ul",
                  { className: "crm-rowlist" },
                  custHits.map((c) =>
                    el("li", { key: c.customerId }, el("span", { className: "crm-grow" }, c.name, c.company ? " · " + c.company : ""), el(Button, { size: "sm", variant: "outline", onClick: () => set({ referredBy: c }) }, "Pick"))
                  )
                )
            ),
          src?.code === "WHATSAPP" &&
            el(Input, {
              label: "Reference code in their message (optional)",
              placeholder: "W-7741",
              hint: "Messages sent from the website button end with “ref W-…”. Typing it here attaches what they read on the site.",
              value: f.waRef,
              onChange: (e) => set({ waRef: e.target.value })
            }),
          el("div", { className: "crm-section-title" }, "Who they are"),
          el(
            "div",
            { className: "crm-grid2" },
            el(Input, { label: "Name", required: true, value: f.name, maxLength: 150, onChange: (e) => set({ name: e.target.value }) }),
            el(Input, { label: "Mobile", required: true, value: f.mobile, inputMode: "tel", placeholder: "98450 12345", maxLength: 20, onChange: (e) => set({ mobile: e.target.value }) }),
            el(Input, { label: "Email", type: "email", value: f.email, hint: "Leave blank if they did not give one.", maxLength: 256, onChange: (e) => set({ email: e.target.value }) }),
            el(Input, { label: "Company", value: f.company, hint: "Blank for an individual.", maxLength: 150, onChange: (e) => set({ company: e.target.value }) })
          ),
          dup &&
            el(
              "div",
              { className: "crm-panel crm-panel--amber", role: "alert" },
              dup.leads.map((l) =>
                el(
                  "div",
                  { key: "l" + l.leadId, style: { marginBottom: 6 } },
                  el("b", null, "We already have this ", l.matchedOn, ". "),
                  `${l.name}${l.company ? " — " + l.company : ""}, lead ${l.leadNumber}, created ${fmtD(l.createdUtc)} ${l.howCreated}. Owner: ${l.ownerName || "Unassigned"}. Status: ${l.status}.`
                )
              ),
              dup.customers.map((c) =>
                el(
                  "div",
                  { key: "c" + c.customerId, style: { marginBottom: 6 } },
                  el("b", null, "This is already a customer — "),
                  `${c.name}${c.company ? " (" + c.company + ")" : ""}, customer #${c.customerId}. Open the customer, or create a lead for a second space.`
                )
              ),
              el(
                "div",
                { style: { display: "flex", gap: 8, flexWrap: "wrap", marginTop: 6 } },
                dup.leads[0] && el(Button, { size: "sm", variant: "primary", onClick: () => { onClose(); navigate(`/admin/crm/leads/${dup.leads[0].leadId}`); } }, "Open that lead"),
                !dup.leads[0] && dup.customers[0] && el(Button, { size: "sm", variant: "primary", onClick: () => { onClose(); navigate(`/admin/customers/${dup.customers[0].customerId}`); } }, "Open the customer"),
                allowDup
                  ? el("span", { className: "crm-muted", style: { alignSelf: "center" } }, "OK — saving as a separate lead.")
                  : el(Button, { size: "sm", variant: "outline", onClick: () => setAllowDup(true) }, dup.leads[0] ? "It is a different person" : "Create a lead for a second space")
              )
            ),
          el("div", { className: "crm-section-title" }, "What they want"),
          el(
            "div",
            { className: "crm-grid3" },
            el(Select, { label: "Looking for", placeholder: "—", value: f.lookingForId, options: looking.map((l) => ({ value: String(l.lookingForId), label: l.name })), onChange: (e) => set({ lookingForId: e.target.value }) }),
            el(Input, { label: "How many people", type: "number", min: 1, value: f.people, onChange: (e) => set({ people: e.target.value }) }),
            el(Input, { label: "Budget a month", placeholder: "around 45k", value: f.budget, maxLength: 100, onChange: (e) => set({ budget: e.target.value }) }),
            el(Select, { label: "Which location", placeholder: "—", value: f.locationId, options: (opt.locations || []).map((l) => ({ value: String(l.id), label: l.name })), onChange: (e) => set({ locationId: e.target.value }) }),
            el(Input, { label: "Needed from", type: "date", value: f.neededFrom, onChange: (e) => set({ neededFrom: e.target.value }) }),
            el(Select, { label: "How did they hear of us", placeholder: "—", value: f.heard, options: HEARD.map((h) => ({ value: h, label: h })), onChange: (e) => set({ heard: e.target.value }) })
          ),
          el(Textarea, {
            label: "What they said",
            hint: "Write what they actually said, not a summary — the person who calls back on Thursday will not be the person who took this call.",
            value: f.said,
            maxLength: 2000,
            onChange: (e) => set({ said: e.target.value })
          }),
          el("div", { className: "crm-section-title" }, "Who picks it up"),
          el(
            "div",
            { className: "crm-grid3" },
            el(Select, {
              label: "Assign to",
              value: f.assign,
              options: [
                { value: "me", label: "Me" },
                ...(me?.canAssign ? team.filter((t) => t.userId !== me.userId).map((t) => ({ value: String(t.userId), label: t.name + (t.isOnDuty || t.checkedInToday ? "" : " (not on duty)") })) : []),
                { value: "rules", label: "Let the rules decide" }
              ],
              onChange: (e) => set({ assign: e.target.value })
            }),
            el(Input, { label: "Next follow-up", type: "datetime-local", value: f.followUp, onChange: (e) => set({ followUp: e.target.value }) }),
            el(Input, { label: "What to do then", value: f.followWhat, maxLength: 500, onChange: (e) => set({ followWhat: e.target.value, followWhatTouched: true }) })
          ),
          el(
            "div",
            { className: "crm-panel crm-panel--green" },
            src
              ? `Saving creates the next lead number with source “${src.name}” and status ${status}, owned by ${ownerName}.` +
                  (f.followUp ? ` A reminder appears on ${fmtDT(fromIstInput(f.followUp))}: “${f.followWhat || "…"}”.` : " No follow-up is set.") +
                  " No email or message is sent to the person — they have not agreed to be contacted automatically."
              : "Choose how they reached us first."
          )
        )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* Leads list + pipeline (sections 3 and 9.1)                                                  */
/* ---------------------------------------------------------------------------------------- */

const DAYS = [
  { value: "7", label: "Last 7 days" },
  { value: "30", label: "Last 30 days" },
  { value: "90", label: "Last 90 days" },
  { value: "365", label: "Last 12 months" },
  { value: "0", label: "Any time" }
];

export function CrmLeadsPage() {
  useCss();
  const toast = useToast();
  const navigate = useNavigate();
  const [opt] = useOptions();
  const [view, setView] = React.useState(() => {
    try {
      return localStorage.getItem("rg-crm-view") || "list";
    } catch {
      return "list";
    }
  });
  const [filters, setFilters] = React.useState({ search: "", sourceId: "", statusId: "", ownerId: "", days: "30", preset: "" });
  const [sort, setSort] = React.useState({ by: "created", desc: true });
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [adding, setAdding] = React.useState(false);
  const [term, setTerm] = React.useState("");
  const [drop, setDrop] = React.useState(null);
  React.useEffect(() => {
    const t = setTimeout(() => setTerm(filters.search), 350);
    return () => clearTimeout(t);
  }, [filters.search]);
  const setFilter = (p) => {
    setFilters((f) => ({ ...f, preset: "", ...p }));
    setPage(1);
  };
  const pipeline = view === "pipeline";

  const params = {
    search: term || void 0,
    sourceId: filters.sourceId || void 0,
    statusId: pipeline ? void 0 : filters.statusId || void 0,
    ownerId: filters.ownerId === "" ? void 0 : filters.ownerId,
    days: filters.days,
    preset: pipeline ? "open" : filters.preset || void 0,
    sortBy: sort.by,
    isDescending: sort.desc,
    page: pipeline ? 1 : page,
    pageSize: pipeline ? 500 : 50
  };
  const load = React.useCallback(async () => {
    setLoading(true);
    try {
      setData(await unwrap(http.get("/crm/leads", { params })));
    } catch (e) {
      toast.error(errMsg(e, "Could not load the leads."));
    } finally {
      setLoading(false);
    }
  }, [JSON.stringify(params)]);
  React.useEffect(() => {
    load();
  }, [load]);

  const s = data?.summary;
  const rows = data?.page?.items || [];
  const me = opt?.me;
  const statuses = (opt?.statuses || []).filter((x) => x.isActive);

  const sortBy = (by) => setSort((x) => ({ by, desc: x.by === by ? !x.desc : by === "created" }));
  const head = (label, by) => el("th", { key: label }, by ? el("button", { onClick: () => sortBy(by) }, label, sort.by === by ? (sort.desc ? " ↓" : " ↑") : "") : label);

  const moveTo = async (lead, status) => {
    if (lead.statusId === status.statusId) return;
    if (status.code === "LOST") return setDrop({ kind: "lost", lead, status });
    if (status.code === "VISIT") return setDrop({ kind: "tour", lead, status });
    if (status.code === "WON") return navigate(`/admin/crm/leads/${lead.leadId}?convert=1`);
    try {
      await unwrap(http.post(`/crm/leads/${lead.leadId}/status`, { statusId: status.statusId }));
      toast.success(`${lead.name} → ${status.name}`);
      load();
    } catch (e) {
      toast.error(errMsg(e, "Could not move the lead."));
    }
  };

  const persistView = (v) => {
    setView(v);
    try {
      localStorage.setItem("rg-crm-view", v);
    } catch {}
  };

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Leads",
      description: "Everyone who has shown interest and is not a customer yet — from the website, the phone, WhatsApp or the front desk.",
      actions: el(
        F,
        null,
        el(Button, { variant: "outline", onClick: () => download("/crm/leads/export", { ...params, page: 1, pageSize: 5000 }, "crm_leads.xlsx").catch(() => toast.error("Export failed.")) }, "Export"),
        me?.canCreate && el(Button, { variant: "primary", onClick: () => setAdding(true) }, "+ Add lead")
      )
    }),
    el(
      "div",
      { className: "crm-cards" },
      el(Stat, { label: "New this week", value: s?.newThisWeek ?? "…", sub: s ? `${s.newThisWeekWebsite} from the website` : "", onClick: () => setFilters((f) => ({ ...f, preset: "new-week" })) }),
      el(Stat, {
        label: "Awaiting first contact",
        value: s?.awaitingFirstContact ?? "…",
        red: s && s.awaitingFirstContact > 0,
        sub: s?.oldestAwaitingMinutes != null ? `oldest waiting ${mins(s.oldestAwaitingMinutes)}` : "nobody waiting",
        onClick: () => setFilters((f) => ({ ...f, preset: "awaiting" }))
      }),
      el(Stat, { label: "Follow-up due today", value: s?.followUpDueToday ?? "…", sub: s ? `${s.followUpOverdue} already overdue` : "", onClick: () => navigate("/admin/crm/follow-ups") }),
      el(Stat, { label: "Converted this month", value: s?.convertedThisMonth ?? "…", sub: s ? `${money(s.convertedValue)} of business won` : "", onClick: () => setFilters((f) => ({ ...f, preset: "won-month" })) }),
      el(Stat, {
        label: "Conversion rate",
        value: s?.conversionRate90 != null ? s.conversionRate90 + "%" : "—",
        sub: s ? `${s.won90} won of ${s.closed90Leads} leads, last 90 days` : "",
        onClick: me?.canSeeReports ? () => navigate("/admin/crm/reports") : void 0
      })
    ),
    el(
      Card,
      null,
      el(
        "div",
        { className: "crm-filters" },
        el(Input, { label: "Search", placeholder: "Name, company, mobile or email…", value: filters.search, onChange: (e) => setFilter({ search: e.target.value }) }),
        el(Select, { label: "Source", value: filters.sourceId, options: [{ value: "", label: "All sources" }, ...(opt?.sources || []).map((x) => ({ value: String(x.sourceId), label: x.name }))], onChange: (e) => setFilter({ sourceId: e.target.value }) }),
        el(Select, {
          label: "Status",
          value: filters.statusId,
          disabled: pipeline,
          options: [{ value: "", label: "Any status" }, ...statuses.map((x) => ({ value: String(x.statusId), label: x.name }))],
          onChange: (e) => setFilter({ statusId: e.target.value })
        }),
        el(Select, {
          label: "Owner",
          value: filters.ownerId,
          options: [{ value: "", label: "All owners" }, { value: "0", label: "Unassigned" }, ...(opt?.team || []).map((t) => ({ value: String(t.userId), label: t.name }))],
          onChange: (e) => setFilter({ ownerId: e.target.value })
        }),
        el(Select, { label: "Created", value: filters.days, disabled: pipeline, options: DAYS, onChange: (e) => setFilter({ days: e.target.value }) }),
        el(
          "div",
          { className: "field" },
          el("span", { className: "field__label" }, "View"),
          el(
            "div",
            { className: "crm-toggle", role: "group", "aria-label": "View" },
            el("button", { type: "button", className: view === "list" ? "on" : "", onClick: () => persistView("list") }, "List"),
            el("button", { type: "button", className: pipeline ? "on" : "", onClick: () => persistView("pipeline") }, "Pipeline")
          )
        )
      ),
      filters.preset &&
        el(
          "div",
          { className: "crm-panel crm-panel--grey", style: { display: "flex", justifyContent: "space-between", alignItems: "center" } },
          { "new-week": "Showing leads created in the last 7 days.", awaiting: "Showing leads nobody has contacted yet.", "won-month": "Showing leads won this month." }[filters.preset],
          el(Button, { size: "sm", variant: "ghost", onClick: () => setFilters((f) => ({ ...f, preset: "" })) }, "Clear")
        ),
      loading && !data
        ? el(Spinner, { center: true })
        : pipeline
        ? el(Pipeline, { rows, statuses, onMove: moveTo, onOpen: (r) => navigate(`/admin/crm/leads/${r.leadId}`) })
        : rows.length === 0
        ? el(EmptyState, { title: "No leads match", description: me?.canCreate ? "Somebody rang or walked in? Use + Add lead." : void 0 })
        : el(
            F,
            null,
            el(
              "div",
              { className: "crm-scroll" },
              el(
                "table",
                { className: "crm-table", style: { opacity: loading ? 0.6 : 1 } },
                el("thead", null, el("tr", null, head("Lead", "name"), head("Contact"), head("Looking for", "lookingfor"), head("Source", "source"), head("Status", "status"), head("Owner", "owner"), head("Next follow-up", "followup"), head(""))),
                el(
                  "tbody",
                  null,
                  rows.map((r) =>
                    el(
                      "tr",
                      { key: r.leadId, className: (r.untouched ? "crm-row--new " : "") + (r.isOverdue ? "crm-row--late" : "") },
                      el("td", null, el(Link, { to: `/admin/crm/leads/${r.leadId}`, style: { fontWeight: 700, color: "inherit" } }, r.name), el("div", { style: MUTED }, [r.company, r.leadNumber].filter(Boolean).join(" · "))),
                      el("td", null, el("div", null, r.mobile), el("div", { style: MUTED }, r.email || "no email given")),
                      el("td", null, [r.lookingFor, r.peopleCount ? r.peopleCount + " seats" : null].filter(Boolean).join(" · ") || "—"),
                      el("td", null, el("span", { className: "crm-pill" }, r.source)),
                      el("td", null, el(StatusPill, { row: r })),
                      el("td", null, r.ownerName || el("span", { className: "crm-red" }, "Unassigned")),
                      el(
                        "td",
                        null,
                        r.isClosed
                          ? el("span", { style: MUTED }, "— ", r.isWon ? "won" : r.lostReason || "closed")
                          : r.nextFollowUpUtc
                          ? el("div", { className: r.isOverdue ? "crm-red" : "" }, fmtDT(r.nextFollowUpUtc), r.isOverdue ? el("div", null, r.overdueDays >= 1 ? `${r.overdueDays} day${r.overdueDays === 1 ? "" : "s"} late` : "late today") : null)
                          : r.untouched
                          ? el("span", { className: "crm-red" }, "never contacted")
                          : "—"
                      ),
                      el("td", null, el(Button, { size: "sm", variant: "outline", onClick: () => navigate(`/admin/crm/leads/${r.leadId}`) }, "Open"))
                    )
                  )
                )
              )
            ),
            el(
              "div",
              { className: "data-table__pager" },
              el("span", { className: "data-table__pager-info" }, `Page ${data.page.page} of ${data.page.totalPages} · ${data.page.totalCount} leads`),
              el(
                "div",
                { className: "data-table__pager-buttons" },
                el(Button, { variant: "outline", size: "sm", disabled: !data.page.hasPrevious || loading, onClick: () => setPage(page - 1) }, "‹"),
                el(Button, { variant: "outline", size: "sm", disabled: !data.page.hasNext || loading, onClick: () => setPage(page + 1) }, "›")
              )
            )
          ),
      el(
        "p",
        { className: "crm-note" },
        "The figures above count every lead in the current filter (search, source, owner), not only this page. A cream row has not been contacted yet; a red edge means its follow-up is late."
      )
    ),
    adding && el(AddLeadDialog, { onClose: () => setAdding(false), onSaved: () => load() }),
    drop?.kind === "lost" && el(LoseDialog, { lead: drop.lead, opt, onClose: () => setDrop(null), onDone: () => { setDrop(null); load(); } }),
    drop?.kind === "tour" && el(TourDialog, { lead: drop.lead, onClose: () => setDrop(null), onDone: () => { setDrop(null); load(); } })
  );
}

function Pipeline({ rows, statuses, onMove, onOpen }) {
  const [over, setOver] = React.useState(null);
  const byStatus = (id) => rows.filter((r) => r.statusId === id);
  return el(
    "div",
    { className: "crm-board" },
    statuses.map((st) => {
      const list = byStatus(st.statusId);
      const value = list.reduce((a, r) => a + (r.dealValue || 0), 0);
      return el(
        "div",
        {
          key: st.statusId,
          className: "crm-col" + (over === st.statusId ? " crm-col--over" : ""),
          onDragOver: (e) => {
            e.preventDefault();
            setOver(st.statusId);
          },
          onDragLeave: () => setOver(null),
          onDrop: (e) => {
            e.preventDefault();
            setOver(null);
            const id = Number(e.dataTransfer.getData("text/plain"));
            const lead = rows.find((r) => r.leadId === id);
            if (lead && !lead.isClosed) onMove(lead, st);
          }
        },
        el("h3", null, st.name, " ", el("span", { style: MUTED }, `(${list.length})`)),
        el("div", { style: { ...MUTED, margin: "0 6px" } }, st.description || "", st.isClosed ? ` · this month ${money(value)}` : ""),
        list.map((r) =>
          el(
            "div",
            {
              key: r.leadId,
              className: "crm-kcard" + (r.isOverdue ? " crm-kcard--late" : "") + (r.untouched ? " crm-kcard--new" : ""),
              draggable: !r.isClosed,
              onDragStart: (e) => e.dataTransfer.setData("text/plain", String(r.leadId)),
              onClick: () => onOpen(r),
              title: r.isClosed ? "Closed" : "Drag to another stage, or click to open"
            },
            el("div", { style: { fontWeight: 700 } }, r.name),
            el("div", { style: MUTED }, [r.lookingFor, r.peopleCount ? r.peopleCount + " seats" : null, r.dealValue ? money(r.dealValue) + "/mo" : null].filter(Boolean).join(" · ") || "—"),
            el("div", { className: r.isOverdue || r.untouched ? "crm-red" : "", style: { fontSize: "var(--font-size-xs)", marginTop: 2 } }, r.urgency)
          )
        )
      );
    })
  );
}

/* ---------------------------------------------------------------------------------------- */
/* dialogs used by the lead screen, the pipeline and the follow-ups board                      */
/* ---------------------------------------------------------------------------------------- */

function useAction(onDone) {
  const toast = useToast();
  const [busy, setBusy] = React.useState(false);
  const run = async (fn, ok) => {
    setBusy(true);
    try {
      const r = await fn();
      if (ok) toast.success(ok);
      onDone && onDone(r);
      return r;
    } catch (e) {
      toast.error(errMsg(e, "That did not work."));
      return null;
    } finally {
      setBusy(false);
    }
  };
  return [busy, run];
}

function Dialog({ title, onClose, busy, okLabel, onOk, okDisabled, danger, children }) {
  return el(
    Modal,
    {
      open: true,
      title,
      onClose: () => busy || onClose(),
      footer: el(
        F,
        null,
        el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"),
        el(Button, { variant: danger ? "danger" : "primary", loading: busy, disabled: okDisabled, onClick: onOk }, okLabel)
      )
    },
    el("div", { className: "form-stack crm-mid" }, children)
  );
}

function LoseDialog({ lead, opt, onClose, onDone }) {
  const [reason, setReason] = React.useState("");
  const [note, setNote] = React.useState("");
  const [busy, run] = useAction(onDone);
  const reasons = (opt?.lostReasons || []).filter((r) => r.isActive);
  const hint = reasons.find((r) => String(r.lostReasonId) === reason)?.hint;
  return el(
    Dialog,
    {
      title: `Close ${lead.name} as lost`,
      onClose,
      busy,
      danger: true,
      okLabel: "Close as lost",
      okDisabled: !reason,
      onOk: () => run(() => unwrap(http.post(`/crm/leads/${lead.leadId}/lose`, { lostReasonId: Number(reason), note: note || null })), "Closed as lost.")
    },
    el(Select, { label: "Why was it lost?", required: true, placeholder: "Choose a reason", value: reason, options: reasons.map((r) => ({ value: String(r.lostReasonId), label: r.name })), onChange: (e) => setReason(e.target.value), hint: hint || "Choose why this lead was lost. It is the only thing that makes the lost report useful." }),
    el(Input, { label: "Anything to add (optional)", value: note, maxLength: 500, placeholder: "e.g. went to the place next door", onChange: (e) => setNote(e.target.value) }),
    el("p", { className: "crm-note" }, "Every open follow-up on this lead is closed with it. A lost lead can be reopened later; reopening is recorded.")
  );
}

function TourDialog({ lead, onClose, onDone }) {
  const [at, setAt] = React.useState(istTodayInput(1, "11:00"));
  const [note, setNote] = React.useState("");
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    { title: `Book a tour — ${lead.name}`, onClose, busy, okLabel: "Book the tour", okDisabled: !at, onOk: () => run(() => unwrap(http.post(`/crm/leads/${lead.leadId}/tour`, { tourUtc: fromIstInput(at), note: note || null })), "Tour booked.") },
    el(Input, { label: "Date and time of the visit", type: "datetime-local", required: true, value: at, onChange: (e) => setAt(e.target.value) }),
    el(Input, { label: "Note (optional)", value: note, maxLength: 300, placeholder: "e.g. show the 4-seat cabin on the 2nd floor", onChange: (e) => setNote(e.target.value) }),
    el("p", { className: "crm-note" }, "The lead moves to Visit booked and the owner gets a follow-up at the tour time. Nothing is booked in Bookings — a tour is not a paid booking.")
  );
}

const ACT_KINDS = [
  { value: "call", label: "Call" },
  { value: "email", label: "Email" },
  { value: "whatsapp", label: "WhatsApp" },
  { value: "meeting", label: "Meeting" },
  { value: "note", label: "Note" }
];

function ActivityDialog({ lead, kind, onClose, onDone }) {
  const [k, setK] = React.useState(kind || "call");
  const [detail, setDetail] = React.useState("");
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    { title: k === "note" ? "Add a note" : "Log what happened", onClose, busy, okLabel: "Add to the log", okDisabled: !detail.trim(), onOk: () => run(() => unwrap(http.post(`/crm/leads/${lead.leadId}/activities`, { kind: k, detail })), "Added to the activity log.") },
    el("div", { className: "crm-chips" }, ACT_KINDS.map((a) => el("button", { key: a.value, type: "button", className: "crm-chip" + (k === a.value ? " crm-chip--on" : ""), onClick: () => setK(a.value) }, a.label))),
    el(Textarea, { label: k === "note" ? "Note" : "What was said", required: true, value: detail, maxLength: 2000, onChange: (e) => setDetail(e.target.value), hint: "The log is append-only: entries are never edited or deleted. A correction is a new entry." }),
    k !== "note" && lead.row?.status === "New" && el("p", { className: "crm-note" }, "Logging the first call or email moves this lead from New to Contacted by itself.")
  );
}

function FollowUpDialog({ lead, opt, onClose, onDone }) {
  const [at, setAt] = React.useState(istTodayInput(1, "10:00"));
  const [what, setWhat] = React.useState("");
  const [kind, setKind] = React.useState("call");
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    {
      title: "Set a follow-up",
      onClose,
      busy,
      okLabel: "Set follow-up",
      okDisabled: !at || !what.trim(),
      onOk: () => run(() => unwrap(http.post(`/crm/leads/${lead.leadId}/follow-ups`, { dueUtc: fromIstInput(at), whatToDo: what, kind })), "Follow-up set.")
    },
    el("div", { className: "crm-grid2" }, el(Input, { label: "When", type: "datetime-local", required: true, value: at, onChange: (e) => setAt(e.target.value) }), el(Select, { label: "By", value: kind, options: [{ value: "call", label: "Call" }, { value: "email", label: "Email" }], onChange: (e) => setKind(e.target.value) })),
    el(Input, { label: "What to do", required: true, maxLength: 500, value: what, placeholder: "e.g. Call back with the cabin price for 4 people", hint: "“Follow up” on its own is not a task — say what needs to be done, so whoever picks it up knows.", onChange: (e) => setWhat(e.target.value) })
  );
}

const OUTCOMES = [
  { value: "spoke", label: "Spoke to them" },
  { value: "no-answer", label: "No answer" },
  { value: "left-message", label: "Left a message" },
  { value: "sent-email", label: "Sent an email" },
  { value: "other", label: "Other" }
];

export function DoneDialog({ followUp, opt, onClose, onDone }) {
  const [outcome, setOutcome] = React.useState("");
  const [note, setNote] = React.useState("");
  const [mode, setMode] = React.useState("next");
  const [at, setAt] = React.useState(istTodayInput(2, "10:00"));
  const [what, setWhat] = React.useState("");
  const [kind, setKind] = React.useState("call");
  const [lose, setLose] = React.useState(false);
  const [busy, run] = useAction(onDone);
  if (lose) return el(LoseDialog, { lead: { leadId: followUp.leadId, name: followUp.leadName }, opt, onClose, onDone });
  const ok = outcome && (mode === "lost" || (at && what.trim()));
  return el(
    Dialog,
    {
      title: `Done: ${followUp.leadName}`,
      onClose,
      busy,
      okLabel: mode === "lost" ? "Next: close as lost" : "Mark as done",
      okDisabled: !ok,
      onOk: async () => {
        if (mode === "lost") {
          // Close the lead first: losing closes every open follow-up with it.
          setLose(true);
          return;
        }
        await run(() => unwrap(http.post(`/crm/follow-ups/${followUp.followUpId}/done`, { outcome, note: note || null, next: { dueUtc: fromIstInput(at), whatToDo: what, kind } })), "Done. The next follow-up is set.");
      }
    },
    el("div", { style: MUTED }, followUp.whatToDo),
    el("div", { className: "crm-section-title" }, "What happened?"),
    el("div", { className: "crm-chips" }, OUTCOMES.map((o) => el("button", { key: o.value, type: "button", className: "crm-chip" + (outcome === o.value ? " crm-chip--on" : ""), onClick: () => setOutcome(o.value) }, o.label))),
    el(Input, { label: "Note (optional)", value: note, maxLength: 400, onChange: (e) => setNote(e.target.value) }),
    el("div", { className: "crm-section-title" }, "And next?"),
    el(
      "div",
      { className: "crm-toggle" },
      el("button", { type: "button", className: mode === "next" ? "on" : "", onClick: () => setMode("next") }, "Set the next follow-up"),
      el("button", { type: "button", className: mode === "lost" ? "on" : "", onClick: () => setMode("lost") }, "Close the lead as lost")
    ),
    mode === "next" &&
      el(
        F,
        null,
        el("div", { className: "crm-grid2" }, el(Input, { label: "When", type: "datetime-local", required: true, value: at, onChange: (e) => setAt(e.target.value) }), el(Select, { label: "By", value: kind, options: [{ value: "call", label: "Call" }, { value: "email", label: "Email" }], onChange: (e) => setKind(e.target.value) })),
        el(Input, { label: "What to do", required: true, maxLength: 500, value: what, onChange: (e) => setWhat(e.target.value) })
      ),
    el("p", { className: "crm-note" }, "A follow-up is never closed without the next one being set, unless the lead is being closed too.")
  );
}

function AssignDialog({ lead, opt, onClose, onDone }) {
  const toast = useToast();
  const [userId, setUserId] = React.useState(lead.row.ownerUserId ? String(lead.row.ownerUserId) : "");
  const [confirm, setConfirm] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const go = async (force) => {
    setBusy(true);
    try {
      const r = await unwrap(http.post(`/crm/leads/${lead.row.leadId}/assign`, { userId: userId ? Number(userId) : null, force }));
      toast.success("Owner changed. It is written to the activity log.");
      onDone(r);
    } catch (e) {
      if (errCode(e) === "NOT_ON_DUTY") setConfirm(e.message);
      else toast.error(errMsg(e, "Could not change the owner."));
    } finally {
      setBusy(false);
    }
  };
  return el(
    Dialog,
    { title: "Assign to someone", onClose, busy, okLabel: confirm ? "Assign anyway" : "Assign", onOk: () => go(!!confirm) },
    el(Select, {
      label: "Owner",
      value: userId,
      options: [{ value: "", label: "Unassigned" }, ...(opt?.team || []).map((t) => ({ value: String(t.userId), label: t.name + (t.isOnDuty || t.checkedInToday ? " · on duty" : "") }))],
      onChange: (e) => {
        setUserId(e.target.value);
        setConfirm(null);
      }
    }),
    confirm && el("div", { className: "crm-panel crm-panel--amber" }, confirm),
    el("p", { className: "crm-note" }, "Reassigning is recorded with who did it — leads quietly changing hands is how they get lost. Open follow-ups move to the new owner.")
  );
}

function ReopenDialog({ lead, onClose, onDone }) {
  const [reason, setReason] = React.useState("");
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    { title: "Reopen this lead", onClose, busy, okLabel: "Reopen", onOk: () => run(() => unwrap(http.post(`/crm/leads/${lead.row.leadId}/reopen`, { reason: reason || null })), "Lead reopened.") },
    el(Input, { label: "Why (optional)", value: reason, maxLength: 300, placeholder: "e.g. they rang back", onChange: (e) => setReason(e.target.value) }),
    el("p", { className: "crm-note" }, "Reopening is written to the activity log.")
  );
}

function DeleteDialog({ lead, onClose, onDone }) {
  const [typed, setTyped] = React.useState("");
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    {
      title: "Delete this lead on request",
      onClose,
      busy,
      danger: true,
      okLabel: "Delete for good",
      okDisabled: typed.trim().toUpperCase() !== lead.row.leadNumber.toUpperCase(),
      onOk: () => run(() => unwrap(http.delete(`/crm/leads/${lead.row.leadId}`)), "The lead and everything attached to it were deleted.")
    },
    el(
      "div",
      { className: "crm-panel crm-panel--red" },
      "Use this only when the person has asked for their details to be deleted. It removes the lead, its activity log, follow-ups, consent record and website visit history together. It cannot be undone, and nothing is kept in the audit trail."
    ),
    el(Input, { label: `Type ${lead.row.leadNumber} to confirm`, value: typed, onChange: (e) => setTyped(e.target.value) })
  );
}

function LinkQuotationDialog({ lead, onClose, onDone }) {
  const [number, setNumber] = React.useState("");
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    { title: "Link a quotation", onClose, busy, okLabel: "Link", okDisabled: !number.trim(), onOk: () => run(() => unwrap(http.post(`/crm/leads/${lead.row.leadId}/quotation`, { quotationNumber: number.trim() })), "Quotation linked.") },
    el(Input, { label: "Quotation number", placeholder: "REG/QTN/2026-27/00012", value: number, onChange: (e) => setNumber(e.target.value) }),
    el("p", { className: "crm-note" }, "The quotation itself lives in Billing & Finance; CRM only links to it. A quotation made with “Send quotation” on this lead is linked automatically within a minute.")
  );
}

function EditDialog({ lead, opt, onClose, onDone }) {
  const r = lead.row;
  const [f, setF] = React.useState({
    name: r.name, mobile: r.mobile, email: r.email || "", company: r.company || "", lookingForId: r.lookingForId ? String(r.lookingForId) : "",
    peopleCount: r.peopleCount || "", budgetText: lead.budgetText || "", locationId: lead.locationId ? String(lead.locationId) : "", neededFrom: lead.neededFrom || "", dealValue: r.dealValue || ""
  });
  const set = (p) => setF((x) => ({ ...x, ...p }));
  const [busy, run] = useAction(onDone);
  return el(
    Dialog,
    {
      title: "Edit lead details",
      onClose,
      busy,
      okLabel: "Save",
      onOk: () =>
        run(
          () =>
            unwrap(
              http.put(`/crm/leads/${r.leadId}`, {
                ...f,
                email: f.email || null,
                company: f.company || null,
                lookingForId: f.lookingForId ? Number(f.lookingForId) : null,
                peopleCount: f.peopleCount ? Number(f.peopleCount) : null,
                budgetText: f.budgetText || null,
                locationId: f.locationId ? Number(f.locationId) : null,
                neededFrom: f.neededFrom || null,
                dealValue: f.dealValue ? Number(f.dealValue) : null
              })
            ),
          "Lead saved."
        )
    },
    el(
      "div",
      { className: "crm-grid2" },
      el(Input, { label: "Name", required: true, value: f.name, onChange: (e) => set({ name: e.target.value }) }),
      el(Input, { label: "Mobile", required: true, value: f.mobile, onChange: (e) => set({ mobile: e.target.value }) }),
      el(Input, { label: "Email", value: f.email, onChange: (e) => set({ email: e.target.value }) }),
      el(Input, { label: "Company", value: f.company, onChange: (e) => set({ company: e.target.value }) }),
      el(Select, { label: "Looking for", placeholder: "—", value: f.lookingForId, options: (opt?.lookingFor || []).filter((l) => l.isActive).map((l) => ({ value: String(l.lookingForId), label: l.name })), onChange: (e) => set({ lookingForId: e.target.value }) }),
      el(Input, { label: "How many people", type: "number", value: f.peopleCount, onChange: (e) => set({ peopleCount: e.target.value }) }),
      el(Input, { label: "Budget", value: f.budgetText, onChange: (e) => set({ budgetText: e.target.value }) }),
      el(Select, { label: "Location wanted", placeholder: "—", value: f.locationId, options: (opt?.locations || []).map((l) => ({ value: String(l.id), label: l.name })), onChange: (e) => set({ locationId: e.target.value }) }),
      el(Input, { label: "Needed from", type: "date", value: f.neededFrom, onChange: (e) => set({ neededFrom: e.target.value }) }),
      el(Input, { label: "Deal value a month (₹, optional)", type: "number", value: f.dealValue, onChange: (e) => set({ dealValue: e.target.value }) })
    ),
    el("p", { className: "crm-note" }, "What they wrote or said is never edited. Changes are written to the activity log.")
  );
}

function ConvertDialog({ lead, onClose, onDone }) {
  const toast = useToast();
  const r = lead.row;
  const [pre, setPre] = React.useState(null);
  const [f, setF] = React.useState({ name: r.name, company: r.company || "", mobile: r.mobile, email: r.email || "", customerType: r.company ? "company" : "individual", createCustomer: true, carryQuotation: true, booking: false });
  const [busy, setBusy] = React.useState(false);
  const [result, setResult] = React.useState(null);
  const navigate = useNavigate();
  const set = (p) => setF((x) => ({ ...x, ...p }));
  React.useEffect(() => {
    unwrap(http.get(`/crm/leads/${r.leadId}/convert`)).then(setPre).catch((e) => toast.error(errMsg(e, "Could not check the Customers module.")));
  }, []);
  const go = async () => {
    setBusy(true);
    try {
      const res = await unwrap(http.post(`/crm/leads/${r.leadId}/convert`, f));
      setResult(res);
      onDone && onDone(res, true);
    } catch (e) {
      toast.error(errMsg(e, "The customer could not be created. The lead has not been changed."));
    } finally {
      setBusy(false);
    }
  };
  if (result)
    return el(
      Modal,
      {
        open: true,
        title: "Converted",
        onClose,
        footer: el(
          F,
          null,
          f.booking && el(Button, { variant: "outline", onClick: () => navigate("/admin/bookings") }, "Open Bookings"),
          el(Button, { variant: "outline", onClick: () => navigate(`/admin/customers/${result.customerId}`) }, "Open the customer"),
          el(Button, { variant: "primary", onClick: onClose }, "Done")
        )
      },
      el("div", { className: "crm-panel crm-panel--green" }, result.message, result.quotationCarried ? " The quotation now belongs to the customer, so Billing can invoice from it." : "")
    );
  return el(
    Dialog,
    { title: `Convert ${r.name} to a customer`, onClose, busy, okLabel: "Convert to customer", okDisabled: !pre || !!pre.alreadyConvertedMessage, onOk: go },
    !pre
      ? el(Spinner, { center: true })
      : el(
          F,
          null,
          pre.alreadyConvertedMessage
            ? el("div", { className: "crm-panel crm-panel--grey" }, pre.alreadyConvertedMessage)
            : el(
                "div",
                { className: "crm-panel crm-panel--amber" },
                pre.existingCustomer
                  ? `${pre.existingCustomer.name} is already a customer (#${pre.existingCustomer.customerId}, matched on ${pre.existingCustomer.matchedOn}). This lead will be attached to that customer — no second record will be created.`
                  : "Checked the Customers module: no customer has this mobile or email. A new customer record will be created by Customers, which issues the customer id."
              ),
          !pre.existingCustomer &&
            el(
              "div",
              { className: "crm-grid2" },
              el(Input, { label: "Name", value: f.name, onChange: (e) => set({ name: e.target.value }) }),
              el(Input, { label: "Company", value: f.company, onChange: (e) => set({ company: e.target.value }) }),
              el(Input, { label: "Mobile", value: f.mobile, onChange: (e) => set({ mobile: e.target.value }) }),
              el(Input, { label: "Email", required: true, value: f.email, hint: "Customers need an email to sign in.", onChange: (e) => set({ email: e.target.value }) }),
              el(Select, { label: "Customer type", value: f.customerType, options: [{ value: "individual", label: "Individual" }, { value: "company", label: "Company" }], onChange: (e) => set({ customerType: e.target.value }) })
            ),
          el(Checkbox, { label: "Create the customer record now", checked: f.createCustomer || !!pre.existingCustomer, disabled: !!pre.existingCustomer, onChange: (e) => set({ createCustomer: e.target.checked }) }),
          el(Checkbox, {
            label: pre.quotation ? `Carry quotation ${pre.quotation.quotationNumber} across, so Billing can invoice from it` : "Carry the quotation across (no quotation is linked)",
            checked: f.carryQuotation && !!pre.quotation,
            disabled: !pre.quotation,
            onChange: (e) => set({ carryQuotation: e.target.checked })
          }),
          el(Checkbox, { label: "Create the booking as well — opens Bookings afterwards", checked: f.booking, onChange: (e) => set({ booking: e.target.checked }) }),
          el("p", { className: "crm-note" }, "The lead is never deleted: it closes as Won and keeps a link to the customer. Converting cannot be undone — a wrong customer record is handled in Customers.")
        )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* One lead (section 4)                                                                         */
/* ---------------------------------------------------------------------------------------- */

export function CrmLeadPage() {
  useCss();
  const { id } = useParams();
  const navigate = useNavigate();
  const toast = useToast();
  const [opt, reloadOpt] = useOptions();
  const canQuote = usePermission("QUOTATION.CREATE");
  const [lead, setLead] = React.useState(null);
  const [tab, setTab] = React.useState("overview");
  const [dlg, setDlg] = React.useState(null);
  const [missing, setMissing] = React.useState(false);
  const load = React.useCallback(async () => {
    try {
      setLead(await unwrap(http.get(`/crm/leads/${id}`)));
    } catch (e) {
      if (e instanceof ApiError && e.status === 404) setMissing(true);
      else toast.error(errMsg(e, "Could not load the lead."));
    }
  }, [id]);
  React.useEffect(() => {
    load();
  }, [load]);
  React.useEffect(() => {
    if (lead && new URLSearchParams(window.location.search).get("convert") === "1" && !lead.row.isClosed && opt?.me?.canConvert) setDlg({ k: "convert" });
  }, [!!lead, !!opt]);
  const done = (r) => {
    setDlg(null);
    if (r && r.row) setLead(r);
    else load();
  };

  if (missing) return el(EmptyState, { title: "Lead not found", description: "It may have been deleted on request, or it belongs to someone else.", action: el(Link, { to: "/admin/crm/leads" }, "Back to leads") });
  if (!lead || !opt) return el(Spinner, { center: true });
  const r = lead.row;
  const me = opt.me;
  const closed = r.isClosed;
  const open = lead.followUps.filter((f) => !f.isDone);
  const next = open[0];
  const summary = [
    r.lookingFor ? `wants ${/^[aeiou]/i.test(r.lookingFor) ? "an" : "a"} ${r.lookingFor.toLowerCase()}${r.peopleCount ? " for " + r.peopleCount : ""}` : r.peopleCount ? `${r.peopleCount} people` : null,
    lead.neededFrom ? "from " + fmtDay(lead.neededFrom) : null,
    lead.budgetText ? "budget " + lead.budgetText : null,
    lead.locationName
  ].filter(Boolean);
  const waiting = r.untouched && !closed;

  const tabs = [
    ["overview", "Overview"],
    ["activity", `Activity (${lead.activities.length})`],
    ["quotations", "Quotations"],
    ["notes", "Notes"],
    ["consent", "Consent & data"]
  ];

  const sendQuotation = () => {
    if (canQuote) navigate(`/admin/billing/quotations/new?lead=${r.leadId}`);
    else setDlg({ k: "link" });
  };

  const activityLog = (list) =>
    el(
      "div",
      { className: "crm-scroll" },
      el(
        "table",
        { className: "crm-table" },
        el("thead", null, el("tr", null, el("th", null, "When"), el("th", null, "What"), el("th", null, "Detail"), el("th", null, "By"))),
        el(
          "tbody",
          null,
          list.map((a) => el("tr", { key: a.activityId }, el("td", { style: { whiteSpace: "nowrap" } }, fmtDT(a.happenedUtc)), el("td", { style: { whiteSpace: "nowrap" } }, a.kindName), el("td", null, a.detail), el("td", null, a.by))),
          waiting &&
            el(
              "tr",
              { key: "wait" },
              el(
                "td",
                { colSpan: 4, className: "crm-red", style: { background: "rgb(220 38 38 / 7%)" } },
                `Waiting for first contact — ${lead.targetMinutes ? "target was " + mins(lead.targetMinutes) + ". " : ""}It has been ${mins(r.waitingMinutes)}.`
              )
            )
        )
      )
    );

  const about = el(
    Card,
    { title: "About this lead", actions: !closed && me.canEdit ? el(Button, { size: "sm", variant: "ghost", onClick: () => setDlg({ k: "edit" }) }, "Edit") : null },
    el(
      "dl",
      { className: "crm-dl" },
      el("dt", null, "Name"), el("dd", null, r.name),
      el("dt", null, "Company"), el("dd", null, r.company || el("span", { style: MUTED }, "an individual")),
      el("dt", null, "Mobile"), el("dd", null, r.mobile),
      el("dt", null, "Email"), el("dd", null, r.email || el("span", { style: MUTED }, "no email given")),
      el("dt", null, "Looking for"), el("dd", null, r.lookingFor || "—", r.peopleCount ? ` · ${r.peopleCount} people` : ""),
      el("dt", null, "Needed from"), el("dd", null, lead.neededFrom ? fmtDay(lead.neededFrom) : "—"),
      el("dt", null, "Budget"), el("dd", null, lead.budgetText || "—"),
      el("dt", null, "Location wanted"), el("dd", null, lead.locationName || "—"),
      el("dt", null, "Source"), el("dd", null, r.source, lead.sourceDetail ? el("div", { style: MUTED }, lead.sourceDetail) : null),
      lead.referredByName && el(F, null, el("dt", null, "Referred by"), el("dd", null, lead.referredByName)),
      lead.walkInUtc && el(F, null, el("dt", null, "Walked in"), el("dd", null, fmtDT(lead.walkInUtc), lead.shownByName ? " · shown around by " + lead.shownByName : "")),
      r.dealValue && el(F, null, el("dt", null, "Deal value"), el("dd", null, money(r.dealValue), " a month"))
    ),
    el("div", { className: "crm-section-title" }, lead.channelName === "Entered by hand" ? "What they said" : "What they wrote in the form"),
    lead.firstMessage ? el("div", { className: "crm-quote" }, lead.firstMessage) : el("div", { style: MUTED }, "Nothing written down.")
  );

  const journey = el(
    Card,
    { title: "Where this lead came from" },
    lead.journey
      ? el(
          F,
          null,
          el(
            "ul",
            { className: "crm-timeline" },
            journeyLines(lead.journey).map((l, i) => el("li", { key: i }, el("b", null, l.when), " — ", l.text)),
            el("li", { className: "crm-gold" }, el("span", null, fmtDT(lead.journey.enquiredUtc), " — ", lead.channelName, ". This is the moment they stopped being anonymous, and lead ", r.leadNumber, " was created."))
          ),
          el(
            "dl",
            { className: "crm-dl", style: { marginTop: 8 } },
            el("dt", null, "Source"), el("dd", null, lead.journey.source || "Direct"),
            el("dt", null, "Landing page"), el("dd", null, lead.journey.landingPage || "—"),
            el("dt", null, "Visits before enquiring"), el("dd", null, lead.journey.visitsBeforeEnquiring),
            el("dt", null, "Days considering"), el("dd", null, lead.journey.daysConsidering),
            el("dt", null, "Campaign"), el("dd", null, lead.journey.campaign || "—"),
            el("dt", null, "Device"), el("dd", null, [lead.journey.device, lead.journey.browser].filter(Boolean).join(" · ") || "—"),
            el("dt", null, "City"), el("dd", null, lead.journey.city || "not known"),
            el("dt", null, "Visitor record"), el("dd", null, me.canSeeVisitors ? el(Link, { to: `/admin/crm/visitors?v=${lead.journey.visitorId}` }, lead.journey.visitorCode) : lead.journey.visitorCode)
          ),
          el(
            "p",
            { className: "crm-note" },
            `Everything above the gold row was recorded anonymously against visitor ${lead.journey.visitorCode}. Nobody knew who they were. The history was attached to this lead at the moment they submitted the form — not before.`
          )
        )
      : el(
          "p",
          { style: { margin: 0 } },
          lead.channelName === "Entered by hand"
            ? `Entered by ${lead.enteredBy}. No website history — this lead did not come through the site.`
            : "No website history was recorded for this enquiry (the visitor's browser did not keep the tracking cookie)."
        )
  );

  const notes = lead.activities.filter((a) => a.kind === 6);

  return el(
    F,
    null,
    el("nav", { style: { ...MUTED, marginBottom: 8 } }, el(Link, { to: "/admin/crm/leads" }, "CRM"), " / ", el(Link, { to: "/admin/crm/leads" }, "Leads"), " / ", r.leadNumber),
    el(PageHeader, {
      title: r.name + (r.company ? " — " + r.company : ""),
      description: el(F, null, el(StatusPill, { row: r }), " ", summary.join(" · ")),
      actions: closed
        ? el(
            F,
            null,
            !r.isWon && me.canEdit && el(Button, { variant: "outline", onClick: () => setDlg({ k: "reopen" }) }, "Reopen"),
            r.isWon && r.customerId && el(Button, { variant: "outline", onClick: () => navigate(`/admin/customers/${r.customerId}`) }, "Open the customer")
          )
        : el(
            F,
            null,
            me.canEdit && el(Button, { variant: "outline", onClick: () => setDlg({ k: "act", kind: "call" }) }, "Log a call"),
            me.canEdit && el(Button, { variant: "outline", onClick: () => setDlg({ k: "tour" }) }, "Book a tour"),
            me.canEdit && el(Button, { variant: "outline", onClick: sendQuotation }, "Send quotation"),
            me.canConvert && el(Button, { variant: "primary", onClick: () => setDlg({ k: "convert" }) }, "Convert to customer")
          )
    }),
    el(
      "div",
      { className: "crm-tabs", role: "tablist" },
      tabs.map(([k, label]) => el("button", { key: k, role: "tab", "aria-selected": tab === k, className: "crm-tab" + (tab === k ? " crm-tab--on" : ""), onClick: () => setTab(k) }, label))
    ),
    el(
      "div",
      { className: "crm-lead" },
      el(
        "div",
        null,
        tab === "overview" && el(F, null, about, el("div", { style: { height: 12 } }), journey, el("div", { style: { height: 12 } }), el(Card, { title: "Everything that has happened", actions: !closed && me.canEdit ? el(Button, { size: "sm", variant: "outline", onClick: () => setDlg({ k: "act" }) }, "+ Add an activity") : null }, activityLog(lead.activities))),
        tab === "activity" && el(Card, { title: "Activity log", actions: me.canEdit ? el(Button, { size: "sm", variant: "outline", onClick: () => setDlg({ k: "act" }) }, "+ Add an activity") : null }, activityLog(lead.activities)),
        tab === "quotations" &&
          el(
            Card,
            { title: "Quotation" },
            lead.quotation
              ? el("dl", { className: "crm-dl" }, el("dt", null, "Number"), el("dd", null, el(Link, { to: `/admin/billing/quotations/${lead.quotation.quotationId}` }, lead.quotation.quotationNumber)), el("dt", null, "Status"), el("dd", null, lead.quotation.status), el("dt", null, "Total"), el("dd", null, money(lead.quotation.totalAmount)), el("dt", null, "Sent"), el("dd", null, fmtDT(lead.quotation.sentUtc)))
              : el("p", null, "Not sent yet. The quotation is made and priced in Billing & Finance; CRM only links to it."),
            !closed && me.canEdit && el("div", { style: { display: "flex", gap: 8, marginTop: 12, flexWrap: "wrap" } }, canQuote && el(Button, { variant: "primary", onClick: sendQuotation }, "Send quotation"), el(Button, { variant: "outline", onClick: () => setDlg({ k: "link" }) }, "Link an existing quotation"))
          ),
        tab === "notes" &&
          el(
            Card,
            { title: "Notes", actions: me.canEdit ? el(Button, { size: "sm", variant: "outline", onClick: () => setDlg({ k: "act", kind: "note" }) }, "+ Add a note") : null },
            notes.length ? el("ul", { className: "crm-rowlist" }, notes.map((n) => el("li", { key: n.activityId }, el("div", { className: "crm-grow" }, n.detail, el("div", { style: MUTED }, fmtDT(n.happenedUtc), " · ", n.by))))) : el("p", { style: MUTED }, "No notes yet.")
          ),
        tab === "consent" && el(ConsentCard, { lead, me, full: true, onDelete: () => setDlg({ k: "delete" }) })
      ),
      el(
        "div",
        { className: "crm-rail" },
        el(
          Card,
          { title: "Next follow-up" },
          closed
            ? el("p", { style: MUTED }, "Closed — no reminders.")
            : next
            ? el(
                F,
                null,
                el("div", { className: next.isOverdue ? "crm-red" : "", style: { fontWeight: 700 } }, fmtDT(next.dueUtc), next.isOverdue ? ` · ${mins(next.lateMinutes)} late` : ""),
                el("p", { style: { margin: "4px 0 8px" } }, next.whatToDo),
                el("div", { style: MUTED }, "Owner: ", next.ownerName || "Unassigned", open.length > 1 ? ` · ${open.length - 1} more later` : ""),
                me.canEdit && el("div", { style: { display: "flex", gap: 8, marginTop: 8 } }, el(Button, { size: "sm", variant: "primary", onClick: () => setDlg({ k: "done", f: next }) }, "Mark as done"), el(Button, { size: "sm", variant: "ghost", onClick: () => setDlg({ k: "fu" }) }, "+ Another"))
              )
            : el(F, null, el("p", { className: "crm-red", style: { margin: "0 0 8px" } }, "No follow-up set."), me.canEdit && el(Button, { size: "sm", variant: "primary", onClick: () => setDlg({ k: "fu" }) }, "Set a follow-up"))
        ),
        el(
          Card,
          { title: "Owner" },
          el("div", { style: { fontWeight: 700 } }, r.ownerName || el("span", { className: "crm-red" }, "Unassigned")),
          !closed && me.canAssign && el(Button, { size: "sm", variant: "ghost", onClick: () => setDlg({ k: "assign" }) }, "Assign to someone"),
          !closed && !me.canAssign && !r.ownerUserId && me.canEdit && el(Button, { size: "sm", variant: "primary", onClick: () => unwrap(http.post(`/crm/leads/${r.leadId}/take`)).then(setLead).catch((e) => toast.error(errMsg(e, "Could not take it."))) }, "Take it")
        ),
        el(
          Card,
          { title: "Linked records" },
          el(
            "dl",
            { className: "crm-dl", style: { gridTemplateColumns: "110px 1fr" } },
            el("dt", null, "Quotation"), el("dd", null, lead.quotation ? el(Link, { to: `/admin/billing/quotations/${lead.quotation.quotationId}` }, lead.quotation.quotationNumber) : el("span", { style: MUTED }, "not sent yet")),
            el("dt", null, "Tour"), el("dd", null, lead.tourUtc ? fmtDT(lead.tourUtc) : el("span", { style: MUTED }, "not booked")),
            el("dt", null, "Customer"), el("dd", null, r.customerId ? el(Link, { to: `/admin/customers/${r.customerId}` }, lead.customerName || "#" + r.customerId) : el("span", { style: MUTED }, "created on conversion")),
            el("dt", null, "Visitor session"), el("dd", null, lead.journey ? (me.canSeeVisitors ? el(Link, { to: `/admin/crm/visitors?v=${lead.journey.visitorId}` }, lead.journey.visitorCode) : lead.journey.visitorCode) : "—")
          )
        ),
        el(ConsentCard, { lead, me, onDelete: () => setDlg({ k: "delete" }) })
      )
    ),
    dlg?.k === "act" && el(ActivityDialog, { lead, kind: dlg.kind, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "tour" && el(TourDialog, { lead: { leadId: r.leadId, name: r.name }, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "fu" && el(FollowUpDialog, { lead: { leadId: r.leadId }, opt, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "done" && el(DoneDialog, { followUp: dlg.f, opt, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "assign" && el(AssignDialog, { lead, opt, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "reopen" && el(ReopenDialog, { lead, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "edit" && el(EditDialog, { lead, opt, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "link" && el(LinkQuotationDialog, { lead, onClose: () => setDlg(null), onDone: done }),
    dlg?.k === "convert" && el(ConvertDialog, { lead, onClose: () => { setDlg(null); load(); }, onDone: () => load() }),
    dlg?.k === "delete" && el(DeleteDialog, { lead, onClose: () => setDlg(null), onDone: () => navigate("/admin/crm/leads") })
  );
}

function journeyLines(j) {
  // Group page views by visit: "14 Nov, 10:22 — First visit, arrived from Google search, landed on /enquire."
  const visits = {};
  for (const p of j.pageViews.filter((p) => new Date(p.viewedUtc) <= new Date(j.enquiredUtc))) (visits[p.visitNumber] = visits[p.visitNumber] || []).push(p);
  const out = [];
  Object.keys(visits)
    .map(Number)
    .sort((a, b) => a - b)
    .forEach((n, i) => {
      const v = visits[n];
      const pages = v.map((p) => p.path + (p.secondsOnPage ? ` (${Math.max(1, Math.round(p.secondsOnPage / 60))} min)` : "")).join(", ");
      out.push({
        when: fmtDT(v[0].viewedUtc),
        text:
          i === 0
            ? `First visit, arrived from ${j.source || "Direct"}${j.campaign ? " (campaign " + j.campaign + ")" : ""}, landed on ${v[0].path}. ${[j.device, j.city].filter(Boolean).join(", ")}. Still anonymous. Read: ${pages}.`
            : `Came back (visit ${n}). Read: ${pages}.`
      });
    });
  return out;
}

function ConsentCard({ lead, me, full, onDelete }) {
  const c = lead.consent;
  return el(
    Card,
    { title: "Consent & data" },
    c
      ? el(
          "dl",
          { className: "crm-dl", style: full ? null : { gridTemplateColumns: "110px 1fr" } },
          el("dt", null, "How obtained"), el("dd", null, c.howObtained),
          el("dt", null, "Agreed to"), el("dd", null, c.contactConsent ? "Being contacted about workspace availability" + (c.consentedUtc ? ` (${fmtDT(c.consentedUtc)})` : "") : "Nothing — no consent form was shown"),
          el("dt", null, "Marketing"), el("dd", null, c.marketingConsent ? "Yes, separately ticked" : "No"),
          full && c.consentTextShown && el(F, null, el("dt", null, "Wording shown"), el("dd", null, el("div", { className: "crm-quote" }, c.consentTextShown))),
          el("dt", null, "Deletion"), el("dd", null, c.retentionText)
        )
      : el("p", { style: MUTED }, "No consent record."),
    me.canDelete && el("div", { style: { marginTop: 10 } }, el(Button, { size: "sm", variant: "danger-outline", onClick: onDelete }, "Delete this lead on request")),
    full && el("p", { className: "crm-note" }, "This screen is the answer when a person asks what we hold about them. Export it from the Leads list if they want a copy.")
  );
}

/* ---------------------------------------------------------------------------------------- */
/* Follow-ups (section 8)                                                                       */
/* ---------------------------------------------------------------------------------------- */

export function CrmFollowUpsPage() {
  useCss();
  const toast = useToast();
  const navigate = useNavigate();
  const [opt] = useOptions();
  const [scope, setScope] = React.useState("mine");
  const [data, setData] = React.useState(null);
  const [dlg, setDlg] = React.useState(null);
  const load = React.useCallback(async () => {
    try {
      setData(await unwrap(http.get("/crm/follow-ups", { params: { scope } })));
    } catch (e) {
      toast.error(errMsg(e, "Could not load the follow-ups."));
    }
  }, [scope]);
  React.useEffect(() => {
    load();
  }, [load]);
  const me = opt?.me;
  const take = async (leadId) => {
    try {
      await unwrap(http.post(`/crm/leads/${leadId}/take`));
      toast.success("It is yours now.");
      load();
    } catch (e) {
      toast.error(errMsg(e, "Could not take it."));
    }
  };
  const readAll = async () => {
    try {
      await unwrap(http.post("/crm/notifications/read"));
      load();
    } catch {}
  };

  const card = (f) =>
    el(
      "div",
      { key: (f.neverContacted ? "n" : "f") + (f.followUpId || f.leadId), className: "crm-fucard" + (f.isOverdue ? " crm-fucard--late" : "") },
      el("div", { style: { display: "flex", justifyContent: "space-between", gap: 8 } }, el(Link, { to: `/admin/crm/leads/${f.leadId}`, style: { fontWeight: 700, color: "inherit" } }, f.leadName), el("span", { className: f.isOverdue ? "crm-red" : "", style: { fontSize: "var(--font-size-xs)", whiteSpace: "nowrap" } }, f.neverContacted ? "never contacted" : fmtDT(f.dueUtc))),
      el("div", { style: MUTED }, [f.company, f.lookingFor].filter(Boolean).join(" · ") || f.leadNumber),
      el("p", { style: { margin: "6px 0" } }, f.whatToDo),
      el(
        "div",
        { style: { display: "flex", justifyContent: "space-between", alignItems: "center", gap: 8 } },
        el("span", { style: MUTED }, f.ownerName ? "Owner: " + f.ownerName : el("span", { className: "crm-red" }, "Unassigned"), f.isOverdue && f.lateMinutes ? ` · ${mins(f.lateMinutes)} late` : ""),
        !f.ownerUserId && me?.canEdit
          ? el(Button, { size: "sm", variant: "primary", onClick: () => take(f.leadId) }, "Take it")
          : f.neverContacted
          ? el(Button, { size: "sm", variant: "outline", onClick: () => navigate(`/admin/crm/leads/${f.leadId}`) }, "Call now")
          : me?.canEdit && (f.ownerUserId === me.userId || me.canAssign)
          ? el(Button, { size: "sm", variant: "primary", onClick: () => setDlg(f) }, "Done")
          : null
      )
    );

  const col = (title, list, empty) => el(Card, { title: `${title} (${list.length})` }, list.length ? list.map(card) : el("p", { style: MUTED }, empty));
  const unread = (data?.alerts || []).filter((a) => !a.isRead).length;

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Follow-ups",
      description: "What is overdue, what is due today, and what is coming this week. Leads are rarely lost on price — they are lost because nobody rang back.",
      actions: me?.canViewAll
        ? el("div", { className: "crm-toggle" }, el("button", { type: "button", className: scope === "mine" ? "on" : "", onClick: () => setScope("mine") }, "Mine + unassigned"), el("button", { type: "button", className: scope === "all" ? "on" : "", onClick: () => setScope("all") }, "Everyone"))
        : null
    }),
    !data
      ? el(Spinner, { center: true })
      : el(
          F,
          null,
          el(
            "div",
            { className: "crm-cards" },
            el(Stat, { label: "Overdue", value: data.overdueCount, red: data.overdueCount > 0, sub: data.oldestOverdueMinutes != null ? `oldest ${mins(data.oldestOverdueMinutes)} late` : "nothing late" }),
            el(Stat, { label: "Due today", value: data.dueToday, sub: `${data.dueTodayCalls} calls · ${data.dueTodayEmails} emails` }),
            el(Stat, { label: "Next 7 days", value: data.next7, sub: `across ${data.next7Owners} owner${data.next7Owners === 1 ? "" : "s"}` }),
            el(Stat, {
              label: "Average first reply",
              value: data.averageFirstReplyMinutes != null ? mins(data.averageFirstReplyMinutes) : "—",
              red: data.averageFirstReplyMinutes != null && data.averageFirstReplyMinutes > data.targetMinutes,
              sub: `target ${mins(data.targetMinutes)} · working hours, last 30 days`
            })
          ),
          el("div", { className: "crm-fu" }, col("Overdue", data.overdue, "Nothing overdue."), col("Today", data.today, "Nothing else due today."), col("Next 7 days", data.week, "Nothing planned this week.")),
          el("div", { style: { height: 12 } }),
          el(
            Card,
            { title: `Your CRM alerts${unread ? ` (${unread} new)` : ""}`, actions: unread ? el(Button, { size: "sm", variant: "ghost", onClick: readAll }, "Mark all read") : null },
            data.alerts.length
              ? el("ul", { className: "crm-rowlist" }, data.alerts.map((a) => el("li", { key: a.notificationId }, el("div", { className: "crm-grow" }, el("span", { style: { fontWeight: a.isRead ? 400 : 700 } }, a.title), a.body ? el("div", { style: MUTED }, a.body) : null), el("span", { style: MUTED }, fmtDT(a.createdUtc)), a.leadId && el(Link, { to: `/admin/crm/leads/${a.leadId}` }, "Open"))))
              : el("p", { style: MUTED }, "No alerts in the last 14 days. New leads, nudges and due follow-ups appear here.")
          )
        ),
    dlg && el(DoneDialog, { followUp: dlg, opt, onClose: () => setDlg(null), onDone: () => { setDlg(null); load(); } })
  );
}

/* ---------------------------------------------------------------------------------------- */
/* Website Visitors (section 7)                                                                */
/* ---------------------------------------------------------------------------------------- */

export function CrmVisitorsPage() {
  useCss();
  const toast = useToast();
  const [days, setDays] = React.useState("7");
  const [sourceId, setSourceId] = React.useState("");
  const [enquired, setEnquired] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [detail, setDetail] = React.useState(null);
  React.useEffect(() => {
    const v = new URLSearchParams(window.location.search).get("v");
    if (v) unwrap(http.get(`/crm/visitors/${v}`)).then(setDetail).catch(() => {});
  }, []);
  React.useEffect(() => {
    unwrap(http.get("/crm/visitors", { params: { days, sourceId: sourceId === "" ? void 0 : sourceId, enquired: enquired || void 0, page, pageSize: 50 } }))
      .then(setData)
      .catch((e) => toast.error(errMsg(e, "Could not load the visitors.")));
  }, [days, sourceId, enquired, page]);
  const open = (id) => unwrap(http.get(`/crm/visitors/${id}`)).then(setDetail).catch((e) => toast.error(errMsg(e, "Could not open it.")));
  const s = data?.summary;
  const maxVisits = Math.max(1, ...(data?.sources || []).map((x) => x.visits));
  return el(
    F,
    null,
    el(PageHeader, {
      title: "Website Visitors",
      description: "Every visit to the site and where it came from. Most rows are strangers and will stay strangers — that is normal."
    }),
    el(
      "div",
      { className: "crm-panel crm-panel--grey" },
      el("b", null, "What a visit can and cannot tell you. "),
      "A visit gives where they came from (Google, Instagram, a tagged link, or Direct), the pages they read and for how long, their device, and whether they have been before. It never gives a name, an email, a phone number or the words they typed into Google. A visitor becomes a person only when they fill in the enquiry form, ask for a callback, tap the WhatsApp button or book."
    ),
    !data
      ? el(Spinner, { center: true })
      : el(
          F,
          null,
          el(
            "div",
            { className: "crm-cards" },
            el(Stat, { label: "Visits", value: s.visits, sub: `${s.devices} distinct browsers` }),
            el(Stat, { label: "Became leads", value: s.becameLeads, sub: "gave a name and a number" }),
            el(Stat, { label: "Enquiry rate", value: s.enquiryRate != null ? s.enquiryRate + "%" : "—", sub: "3–6% is healthy" }),
            el(Stat, { label: "Most-read page", value: el("span", { style: { fontSize: "1rem" } }, s.topPage || "—"), sub: s.topPage ? `${s.topPageViews} views` : "" }),
            el(Stat, { label: "Returning", value: s.returning, sub: "browsers that came back" })
          ),
          el(
            Card,
            { title: "Where the visits came from" },
            data.sources.length === 0
              ? el("p", { style: MUTED }, "No visits in this period.")
              : el(
                  "table",
                  { className: "crm-table" },
                  el("thead", null, el("tr", null, el("th", null, "Source"), el("th", { style: { width: "45%" } }, "Share of visits"), el("th", null, "Visits"), el("th", null, "Enquiries"))),
                  el(
                    "tbody",
                    null,
                    data.sources.map((x) =>
                      el(
                        "tr",
                        { key: x.source, style: { cursor: "pointer" }, onClick: () => { setSourceId(x.sourceId == null ? "0" : String(x.sourceId)); setPage(1); } },
                        el("td", null, x.source),
                        el("td", null, el("div", { className: "crm-bar-track" }, el("div", { className: "crm-bar", style: { width: `${(100 * x.visits) / maxVisits}%` } }))),
                        el("td", null, x.visits),
                        el("td", null, x.enquiries)
                      )
                    )
                  )
                ),
            el("p", { className: "crm-note" }, "Read the two numbers together: a source can bring a tenth of the traffic and a fiftieth of the enquiries. Click a source to filter the sessions below.")
          ),
          el("div", { style: { height: 12 } }),
          el(
            Card,
            { title: "Sessions" },
            el(
              "div",
              { className: "crm-filters", style: { gridTemplateColumns: "repeat(3, minmax(0, 220px))" } },
              el(Select, { label: "Period", value: days, options: [{ value: "1", label: "Today" }, { value: "7", label: "Last 7 days" }, { value: "30", label: "Last 30 days" }, { value: "90", label: "Last 90 days" }], onChange: (e) => { setDays(e.target.value); setPage(1); } }),
              el(Select, { label: "Came from", value: sourceId, options: [{ value: "", label: "All sources" }, { value: "0", label: "Direct" }, ...(data.sources || []).filter((x) => x.sourceId != null).map((x) => ({ value: String(x.sourceId), label: x.source }))], onChange: (e) => { setSourceId(e.target.value); setPage(1); } }),
              el(Select, { label: "Did they enquire?", value: enquired, options: [{ value: "", label: "Everyone" }, { value: "yes", label: "Yes" }, { value: "no", label: "Not yet" }], onChange: (e) => { setEnquired(e.target.value); setPage(1); } })
            ),
            data.page.items.length === 0
              ? el(EmptyState, { title: "No sessions", description: "Visits are recorded on the public pages (sign-in, sign-up, Book a tour) and on any site that loads the tracking script." })
              : el(
                  "div",
                  { className: "crm-scroll" },
                  el(
                    "table",
                    { className: "crm-table" },
                    el("thead", null, el("tr", null, ["Visitor", "First seen", "Came from", "Landed on", "Pages", "Device · city", "Visits", "Did they enquire?"].map((h) => el("th", { key: h }, h)))),
                    el(
                      "tbody",
                      null,
                      data.page.items.map((v) =>
                        el(
                          "tr",
                          { key: v.visitorId, className: v.highlight ? "crm-row--new" : "", style: { cursor: "pointer" }, onClick: (e) => { if (!e.target.closest("a")) open(v.visitorId); } },
                          el("td", null, v.visitorCode),
                          el("td", null, fmtDT(v.firstSeenUtc)),
                          el("td", null, v.cameFrom),
                          el("td", null, v.landingPage || "—"),
                          el("td", null, v.pages),
                          el("td", null, [v.device, v.city].filter(Boolean).join(" · ") || "—"),
                          el("td", null, v.visits),
                          el("td", null, v.leadId ? el(F, null, "Yes — ", el(Link, { to: `/admin/crm/leads/${v.leadId}` }, v.leadName)) : v.highlight ? el("b", null, `Not yet — visit ${v.visits}`) : el("span", { style: MUTED }, "Anonymous"))
                        )
                      )
                    )
                  )
                ),
            el(
              "div",
              { className: "data-table__pager" },
              el("span", { className: "data-table__pager-info" }, `Page ${data.page.page} of ${data.page.totalPages || 1} · ${data.page.totalCount} sessions`),
              el("div", { className: "data-table__pager-buttons" }, el(Button, { variant: "outline", size: "sm", disabled: !data.page.hasPrevious, onClick: () => setPage(page - 1) }, "‹"), el(Button, { variant: "outline", size: "sm", disabled: !data.page.hasNext, onClick: () => setPage(page + 1) }, "›"))
            ),
            el(
              "p",
              { className: "crm-note" },
              "These are browser sessions, not people: the same person on a laptop and a phone is two rows, and clearing cookies makes a third. A cream row has come back three or more times without asking — that is a signal about the page they keep reading, not a person anyone can ring. Anonymous sessions are deleted after the period set in CRM Settings. This is not Google Analytics: it answers only where a particular lead came from."
            )
          )
        ),
    detail &&
      el(
        Modal,
        { open: true, title: `Visitor ${detail.row.visitorCode}`, onClose: () => setDetail(null) },
        el(
          "dl",
          { className: "crm-dl" },
          el("dt", null, "First seen"), el("dd", null, fmtDT(detail.row.firstSeenUtc)),
          el("dt", null, "Came from"), el("dd", null, detail.row.cameFrom),
          el("dt", null, "Tags"), el("dd", null, [detail.utmSource, detail.utmMedium, detail.utmCampaign].filter(Boolean).join(" · ") || "none"),
          el("dt", null, "Referring page"), el("dd", null, detail.referrer || "—"),
          el("dt", null, "Device"), el("dd", null, [detail.row.device, detail.browser, detail.row.city].filter(Boolean).join(" · ")),
          el("dt", null, "Lead"), el("dd", null, detail.row.leadId ? el(Link, { to: `/admin/crm/leads/${detail.row.leadId}` }, detail.row.leadName) : "Anonymous")
        ),
        el("div", { className: "crm-section-title" }, "Pages read"),
        el("ul", { className: "crm-timeline" }, detail.pageViews.map((p, i) => el("li", { key: i }, fmtDT(p.viewedUtc), " · visit ", p.visitNumber, " · ", el("b", null, p.path), p.secondsOnPage ? ` · ${p.secondsOnPage < 60 ? p.secondsOnPage + " s" : Math.round(p.secondsOnPage / 60) + " min"}` : "")))
      )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* CRM Reports (section 10)                                                                     */
/* ---------------------------------------------------------------------------------------- */

export function CrmReportsPage() {
  useCss();
  const toast = useToast();
  const today = new Date(Date.now() + 330 * 60000).toISOString().slice(0, 10);
  const ago = new Date(Date.now() + 330 * 60000 - 89 * 864e5).toISOString().slice(0, 10);
  const [range, setRange] = React.useState({ from: ago, to: today });
  const [rep, setRep] = React.useState(null);
  React.useEffect(() => {
    const p = { params: range };
    Promise.all(["sources", "funnel", "response-times", "owners"].map((k) => unwrap(http.get(`/crm/reports/${k}`, p))))
      .then(([sources, funnel, response, owners]) => setRep({ sources, funnel, response, owners }))
      .catch((e) => toast.error(errMsg(e, "Could not load the reports.")));
  }, [range.from, range.to]);
  const maxConv = Math.max(1, ...(rep?.sources.rows || []).map((r) => Number(r.conversion)));
  const maxResp = Math.max(1, ...(rep?.response.rows || []).map((r) => Number(r.wonPercent)));
  const table = (heads, rows) =>
    el("div", { className: "crm-scroll" }, el("table", { className: "crm-table" }, el("thead", null, el("tr", null, heads.map((h) => el("th", { key: h }, h)))), el("tbody", null, rows)));
  return el(
    F,
    null,
    el(PageHeader, {
      title: "CRM Reports",
      description: "Which source brings paying customers, where people drop out, how fast we reply, and how each person is doing.",
      actions: el(Button, { variant: "outline", onClick: () => download("/crm/reports/export", range, "crm_reports.xlsx").catch(() => toast.error("Export failed.")) }, "Export to Excel")
    }),
    el(
      "div",
      { className: "crm-filters", style: { gridTemplateColumns: "repeat(2, minmax(0, 200px))" } },
      el(Input, { label: "From", type: "date", value: range.from, onChange: (e) => setRange((r) => ({ ...r, from: e.target.value })) }),
      el(Input, { label: "To", type: "date", value: range.to, onChange: (e) => setRange((r) => ({ ...r, to: e.target.value })) })
    ),
    !rep
      ? el(Spinner, { center: true })
      : el(
          F,
          null,
          el(
            Card,
            { title: "Which source actually brings paying customers" },
            rep.sources.rows.length === 0
              ? el("p", { style: MUTED }, "No leads in this period.")
              : table(
                  ["Source", "Leads", "Won", "Conversion", "Revenue won", "What to do"],
                  rep.sources.rows.map((r) =>
                    el(
                      "tr",
                      { key: r.source },
                      el("td", null, r.source),
                      el("td", null, r.leads),
                      el("td", null, r.won),
                      el("td", { style: { minWidth: 140 } }, el("div", { style: { display: "flex", alignItems: "center", gap: 8 } }, el("div", { className: "crm-bar-track", style: { width: 80 } }, el("div", { className: "crm-bar", style: { width: `${(100 * r.conversion) / maxConv}%` } })), r.conversion + "%")),
                      el("td", null, money(r.revenueWon)),
                      el("td", null, r.advice)
                    )
                  )
                ),
            el("p", { className: "crm-note" }, "Read the last two columns together, never the Leads column alone. Revenue won is what the converted customers have been invoiced since they converted (or the deal value typed on the lead). Without tagged links almost everything reads as “Direct”.")
          ),
          el("div", { style: { height: 12 } }),
          el(
            Card,
            { title: "Where people drop out" },
            table(["Step", "Where the number comes from", "Count", "Lost at this step"], rep.funnel.rows.map((r) => el("tr", { key: r.step }, el("td", null, r.step), el("td", { style: MUTED }, r.basis), el("td", null, r.count), el("td", null, r.lostPercent != null ? r.lostPercent + "%" : "—")))),
            el("p", { className: "crm-note" }, "The third step matters most: leads that never got a reply had already asked. It is the cheapest number on this page to fix.")
          ),
          el("div", { style: { height: 12 } }),
          el(
            Card,
            { title: "How fast we reply" },
            table(
              ["Replied within", "Leads", "Of those, won"],
              rep.response.rows.map((r) =>
                el("tr", { key: r.band }, el("td", null, r.band), el("td", null, r.leads), el("td", null, el("div", { style: { display: "flex", alignItems: "center", gap: 8 } }, el("div", { className: "crm-bar-track", style: { width: 120 } }, el("div", { className: "crm-bar", style: { width: `${(100 * r.wonPercent) / maxResp}%` } })), `${r.wonPercent}% (${r.won})`)))
              )
            ),
            el("p", { className: "crm-note" }, "Counted in working hours, for leads that had a reply target (website, callback, WhatsApp and email enquiries). Walk-ins and phone calls were answered on the spot.")
          ),
          el("div", { style: { height: 12 } }),
          el(
            Card,
            { title: "Owner performance" },
            table(
              ["Owner", "Leads held", "Replied in time", "Visits arranged", "Won", "Lost", "Avg days to a decision"],
              rep.owners.rows.map((r) => el("tr", { key: r.owner }, el("td", null, r.owner), el("td", null, r.leads), el("td", null, r.repliedInTimePercent != null ? `${r.repliedInTimePercent}% (${r.repliedInTime})` : "—"), el("td", null, r.visits), el("td", null, r.won), el("td", null, r.lost), el("td", null, r.avgDaysToDecision ?? "—")))
            ),
            el(
              "div",
              { className: "crm-panel crm-panel--amber" },
              "Judge people on “replied in time”, which they control. Do not rank them on conversion rate: whoever gets the cabin enquiries will always look better than whoever gets the day passes, and ranking on it teaches staff to quietly mark hard leads as Lost."
            )
          )
        )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* CRM Settings (section 11)                                                                    */
/* ---------------------------------------------------------------------------------------- */

const TONES = ["neutral", "info", "warning", "success", "danger", "gold"].map((t) => ({ value: t, label: t }));
const REQUIRES = ["—", "one logged call or email", "a date and time", "a linked quotation", "a customer record", "a reason"];
const CONDITIONS = [
  { value: "1", label: "They are an existing customer" },
  { value: "2", label: "They asked for a specific location" },
  { value: "3", label: "It is a cabin, or more than N people" },
  { value: "4", label: "Anything else" },
  { value: "5", label: "Nobody on duty (safety net)" }
];
const TARGETS = [
  { value: "1", label: "The person who already looks after them" },
  { value: "2", label: "The manager" },
  { value: "3", label: "Whoever is on duty" },
  { value: "4", label: "Round robin among whoever is on duty" },
  { value: "5", label: "Unassigned, and tell the manager" },
  { value: "6", label: "A named person" }
];
const DAYNAMES = ["Mon", "Tue", "Wed", "Thu", "Fri", "Sat", "Sun"];

function ListEditor({ title, note, list, items, idKey, fields, onChanged, canAdd = true, reorder, canDisable = () => true }) {
  const toast = useToast();
  const [edit, setEdit] = React.useState(null);
  const save = async () => {
    try {
      const body = { ...edit.v };
      if (edit.id) await unwrap(http.put(`/crm/${list}/${edit.id}`, body));
      else await unwrap(http.post(`/crm/${list}`, body));
      toast.success("Saved.");
      setEdit(null);
      onChanged();
    } catch (e) {
      toast.error(errMsg(e, "Could not save."));
    }
  };
  const toggle = async (it) => {
    try {
      await unwrap(http.put(`/crm/${list}/${it[idKey]}`, { ...it, isActive: !it.isActive }));
      onChanged();
    } catch (e) {
      toast.error(errMsg(e, "Could not change it."));
    }
  };
  const move = async (i, d) => {
    const ids = items.map((x) => x[idKey]);
    const j = i + d;
    if (j < 0 || j >= ids.length) return;
    [ids[i], ids[j]] = [ids[j], ids[i]];
    try {
      await unwrap(http.put(`/crm/${list}/order`, ids));
      onChanged();
    } catch (e) {
      toast.error(errMsg(e, "Could not reorder."));
    }
  };
  return el(
    Card,
    { title, actions: canAdd ? el(Button, { size: "sm", variant: "outline", onClick: () => setEdit({ id: null, v: { name: "", isActive: true } }) }, "+ Add") : null },
    note && el("div", { className: "crm-panel crm-panel--grey", style: { marginTop: 0 } }, note),
    el(
      "ul",
      { className: "crm-rowlist" },
      items.map((it, i) =>
        el(
          "li",
          { key: it[idKey], style: { opacity: it.isActive ? 1 : 0.55 } },
          reorder && el("span", null, el(Button, { size: "sm", variant: "ghost", "aria-label": "Move up", disabled: i === 0, onClick: () => move(i, -1) }, "↑"), el(Button, { size: "sm", variant: "ghost", "aria-label": "Move down", disabled: i === items.length - 1, onClick: () => move(i, 1) }, "↓")),
          el("div", { className: "crm-grow" }, fields.render(it)),
          el(Button, { size: "sm", variant: "ghost", onClick: () => setEdit({ id: it[idKey], v: { ...it } }) }, "Edit"),
          canDisable(it) && el(Button, { size: "sm", variant: "ghost", onClick: () => toggle(it) }, it.isActive ? "Switch off" : "Switch on")
        )
      )
    ),
    edit &&
      el(
        Dialog,
        { title: edit.id ? "Edit" : "Add", onClose: () => setEdit(null), busy: false, okLabel: "Save", okDisabled: !edit.v.name?.trim(), onOk: save },
        fields.form(edit.v, (p) => setEdit((x) => ({ ...x, v: { ...x.v, ...p } })), !edit.id)
      )
  );
}

export function CrmSettingsPage() {
  useCss();
  const toast = useToast();
  const [opt, reload] = useOptions();
  const [s, setS] = React.useState(null);
  const [rules, setRules] = React.useState(null);
  const [saving, setSaving] = React.useState(false);
  const [testTo, setTestTo] = React.useState("");
  const [testing, setTesting] = React.useState(false);
  const [ruleEdit, setRuleEdit] = React.useState(null);
  const loadRules = () => unwrap(http.get("/crm/assignment-rules")).then(setRules).catch(() => {});
  React.useEffect(() => {
    unwrap(http.get("/crm/settings")).then(setS).catch((e) => toast.error(errMsg(e, "Could not load the settings.")));
    loadRules();
  }, []);
  const set = (p) => setS((x) => ({ ...x, ...p }));
  const save = async () => {
    setSaving(true);
    try {
      setS(await unwrap(http.put("/crm/settings", { ...s, targetWebMinutes: Number(s.targetWebMinutes), targetWhatsAppMinutes: Number(s.targetWhatsAppMinutes), targetCallbackMinutes: Number(s.targetCallbackMinutes), targetEmailMinutes: Number(s.targetEmailMinutes), escalateMinutes: Number(s.escalateMinutes), visitRetentionDays: Number(s.visitRetentionDays), lostRetentionMonths: Number(s.lostRetentionMonths) })));
      toast.success("CRM settings saved.");
      reload();
    } catch (e) {
      toast.error(errMsg(e, "Could not save the settings."));
    } finally {
      setSaving(false);
    }
  };
  const test = async () => {
    setTesting(true);
    try {
      toast.success(await unwrap(http.post("/crm/settings/test-email", { to: testTo })));
      setS(await unwrap(http.get("/crm/settings")));
    } catch (e) {
      toast.error(errMsg(e, "The test email was not sent."));
    } finally {
      setTesting(false);
    }
  };
  const saveTeam = async (t, patch) => {
    try {
      await unwrap(http.put(`/crm/team/${t.userId}`, { isMember: t.isMember, isOnDuty: t.isOnDuty, isManager: t.isManager, locationId: t.locationId, ...patch }));
      reload();
    } catch (e) {
      toast.error(errMsg(e, "Could not save."));
    }
  };
  const moveRule = async (i, d) => {
    const ids = rules.map((r) => r.ruleId);
    const j = i + d;
    if (j < 0 || j >= ids.length) return;
    [ids[i], ids[j]] = [ids[j], ids[i]];
    try {
      await unwrap(http.put("/crm/assignment-rules/order", ids));
      loadRules();
    } catch (e) {
      toast.error(errMsg(e, "Could not reorder."));
    }
  };
  const saveRule = async () => {
    try {
      const v = ruleEdit.v;
      const body = { ...v, conditionType: Number(v.conditionType), targetType: Number(v.targetType), locationId: v.locationId ? Number(v.locationId) : null, minPeople: v.minPeople ? Number(v.minPeople) : null, lookingForId: v.lookingForId ? Number(v.lookingForId) : null, targetUserId: v.targetUserId ? Number(v.targetUserId) : null };
      if (ruleEdit.id) await unwrap(http.put(`/crm/assignment-rules/${ruleEdit.id}`, body));
      else await unwrap(http.post("/crm/assignment-rules", body));
      setRuleEdit(null);
      loadRules();
      toast.success("Rule saved.");
    } catch (e) {
      toast.error(errMsg(e, "Could not save the rule."));
    }
  };

  if (!s || !opt) return el(Spinner, { center: true });
  const locName = (id) => opt.locations.find((l) => l.id === id)?.name;
  const nameField = (v, set) => el(Input, { label: "Name", required: true, value: v.name || "", maxLength: 100, onChange: (e) => set({ name: e.target.value }) });

  return el(
    F,
    null,
    el(PageHeader, {
      title: "CRM Settings",
      description: "Everything here is a row in a table, never a value in the code — the business can change it without a developer.",
      actions: el(Button, { variant: "primary", loading: saving, onClick: save }, "Save settings")
    }),
    el(
      ListEditor,
      {
        title: "Lead sources",
        note: "A source is never deleted, only switched off: last year's leads must keep the source they actually came from. Website sources are matched from the link tag (utm_source) or the referring site.",
        list: "sources",
        items: opt.sources,
        idKey: "sourceId",
        reorder: true,
        onChanged: reload,
        fields: {
          render: (x) => el(F, null, el("b", null, x.name), " ", el("span", { style: MUTED }, x.isWebsite ? `Website${x.utmSource ? " · tag " + x.utmSource : ""}${x.utmMedium ? " / " + x.utmMedium : ""}${x.referrerMatch ? " · site " + x.referrerMatch : ""}${!x.utmSource && !x.referrerMatch ? (x.code === "DIRECT" ? " · when there is nothing to go on" : "") : ""}` : "Chosen by hand")),
          form: (v, set, isNew) =>
            el(
              F,
              null,
              nameField(v, set),
              isNew && el(Checkbox, { label: "Set by the website (not a chip on the Add-lead form)", checked: !!v.isWebsite, onChange: (e) => set({ isWebsite: e.target.checked }) }),
              v.isWebsite && el(F, null, el(Input, { label: "Link tags (utm_source), comma separated", value: v.utmSource || "", onChange: (e) => set({ utmSource: e.target.value }) }), el(Input, { label: "Only with utm_medium (optional)", value: v.utmMedium || "", onChange: (e) => set({ utmMedium: e.target.value }) }), el(Input, { label: "Referring site contains (optional)", placeholder: "linkedin.com", value: v.referrerMatch || "", onChange: (e) => set({ referrerMatch: e.target.value }) }))
            )
        }
      }
    ),
    el("div", { style: { height: 12 } }),
    el(ListEditor, {
      title: "Statuses (pipeline stages)",
      note: "Closed statuses stop every reminder and drop the lead off the pipeline board. A new stage is added before Won and Lost. The six built-in stages can be renamed but keep their rules.",
      list: "statuses",
      items: opt.statuses,
      idKey: "statusId",
      reorder: true,
      canDisable: (x) => !x.isBuiltIn,
      onChanged: reload,
      fields: {
        render: (x) => el(F, null, el(Badge, { tone: x.colour }, x.name), " ", el("span", { style: MUTED }, `${x.isClosed ? "closed" : "open"} · cannot be set without ${REQUIRES[x.requires]}${x.description ? " · " + x.description : ""}`)),
        form: (v, set) => el(F, null, nameField(v, set), el(Input, { label: "One-line explanation", value: v.description || "", onChange: (e) => set({ description: e.target.value }) }), el(Select, { label: "Colour", value: v.colour || "neutral", options: TONES, onChange: (e) => set({ colour: e.target.value }) }))
      }
    }),
    el("div", { style: { height: 12 } }),
    el(
      "div",
      { className: "crm-grid2" },
      el(ListEditor, {
        title: "Lost reasons",
        note: "A short fixed list: a free-text box would give six hundred answers and no report.",
        list: "lost-reasons",
        items: opt.lostReasons,
        idKey: "lostReasonId",
        reorder: true,
        onChanged: reload,
        fields: { render: (x) => el(F, null, el("b", null, x.name), x.hint ? el("div", { style: MUTED }, x.hint) : null), form: (v, set) => el(F, null, nameField(v, set), el(Input, { label: "What it tells the business", value: v.hint || "", onChange: (e) => set({ hint: e.target.value }) })) }
      }),
      el(ListEditor, {
        title: "Looking for",
        note: "Keep this in step with the Workspaces module: when a new workspace type is added there, add it here too.",
        list: "looking-for",
        items: opt.lookingFor,
        idKey: "lookingForId",
        reorder: true,
        onChanged: reload,
        fields: { render: (x) => el(F, null, el("b", null, x.name), x.isLarge ? el("span", { style: MUTED }, " · counts as a large enquiry (manager rule)") : null), form: (v, set) => el(F, null, nameField(v, set), el(Checkbox, { label: "Counts as a large enquiry (goes to the manager)", checked: !!v.isLarge, onChange: (e) => set({ isLarge: e.target.checked }) })) }
      })
    ),
    el("div", { style: { height: 12 } }),
    el(
      Card,
      { title: "Response targets and working hours" },
      el(
        "div",
        { className: "crm-grid3" },
        el(Input, { label: "Website enquiry (minutes)", type: "number", value: s.targetWebMinutes, onChange: (e) => set({ targetWebMinutes: e.target.value }) }),
        el(Input, { label: "WhatsApp (minutes)", type: "number", value: s.targetWhatsAppMinutes, onChange: (e) => set({ targetWhatsAppMinutes: e.target.value }) }),
        el(Input, { label: "Callback request (minutes)", type: "number", value: s.targetCallbackMinutes, onChange: (e) => set({ targetCallbackMinutes: e.target.value }) }),
        el(Input, { label: "Email enquiry (minutes)", type: "number", value: s.targetEmailMinutes, hint: "240 = 4 working hours", onChange: (e) => set({ targetEmailMinutes: e.target.value }) }),
        el(Input, { label: "Escalate to the manager after (minutes)", type: "number", value: s.escalateMinutes, onChange: (e) => set({ escalateMinutes: e.target.value }) }),
        el("div", null),
        el(Input, { label: "Working hours from", type: "time", value: s.workStart, onChange: (e) => set({ workStart: e.target.value }) }),
        el(Input, { label: "to", type: "time", value: s.workEnd, onChange: (e) => set({ workEnd: e.target.value }) })
      ),
      el("div", { className: "crm-section-title" }, "Working days"),
      el("div", { className: "crm-chips" }, DAYNAMES.map((d, i) => el("button", { key: d, type: "button", className: "crm-chip" + (s.workDays.includes(i + 1) ? " crm-chip--on" : ""), onClick: () => set({ workDays: s.workDays.includes(i + 1) ? s.workDays.filter((x) => x !== i + 1) : [...s.workDays, i + 1] }) }, d))),
      el("p", { className: "crm-note" }, "Every clock in the CRM pauses outside these hours: an enquiry at 11 PM is not late at 11:30 PM. Walk-ins and phone calls have no first-reply clock — they were answered on the spot.")
    ),
    el("div", { style: { height: 12 } }),
    el(
      Card,
      { title: "Assignment rules", actions: el(Button, { size: "sm", variant: "outline", onClick: () => setRuleEdit({ id: null, v: { label: "", conditionType: "4", targetType: "3", isActive: true } }) }, "+ Add rule") },
      el("p", { style: { marginTop: 0 } }, "Tried in order; the first rule that matches wins."),
      el(
        "ul",
        { className: "crm-rowlist" },
        (rules || []).map((r, i) =>
          el(
            "li",
            { key: r.ruleId, style: { opacity: r.isActive ? 1 : 0.55, ...(r.conditionType === 5 ? { background: "rgb(220 38 38 / 6%)" } : null) } },
            el("span", null, el(Button, { size: "sm", variant: "ghost", disabled: i === 0, onClick: () => moveRule(i, -1) }, "↑"), el(Button, { size: "sm", variant: "ghost", disabled: i === rules.length - 1, onClick: () => moveRule(i, 1) }, "↓")),
            el("b", null, i + 1),
            el("div", { className: "crm-grow" }, r.label, el("div", { style: MUTED }, CONDITIONS.find((c) => c.value === String(r.conditionType))?.label, r.locationId ? ` (${locName(r.locationId)})` : "", r.minPeople ? ` (more than ${r.minPeople})` : "", " → ", TARGETS.find((t) => t.value === String(r.targetType))?.label, r.targetUserId ? ` (${opt.team.find((t) => t.userId === r.targetUserId)?.name || "?"})` : "", r.isActive ? "" : " · switched off")),
            el(Button, { size: "sm", variant: "ghost", onClick: () => setRuleEdit({ id: r.ruleId, v: { ...r, conditionType: String(r.conditionType), targetType: String(r.targetType) } }) }, "Edit")
          )
        )
      ),
      el("p", { className: "crm-note" }, "The safety net is styled in red: a lead reaching it means nobody is on duty. It cannot be switched off.")
    ),
    el("div", { style: { height: 12 } }),
    el(
      Card,
      { title: "Who handles leads" },
      el("p", { style: { marginTop: 0 } }, "Staff whose role includes CRM. Tick who is on the lead team, who is on duty today and who is a manager. Anyone checked in through Attendance today also counts as on duty."),
      el(
        "div",
        { className: "crm-scroll" },
        el(
          "table",
          { className: "crm-table" },
          el("thead", null, el("tr", null, ["Name", "Role", "On the team", "On duty", "Manager", "Location"].map((h) => el("th", { key: h }, h)))),
          el(
            "tbody",
            null,
            opt.team.map((t) =>
              el(
                "tr",
                { key: t.userId },
                el("td", null, t.name, el("div", { style: MUTED }, t.email)),
                el("td", null, t.roles),
                el("td", null, el("input", { type: "checkbox", "aria-label": "On the team", checked: t.isMember, onChange: (e) => saveTeam(t, { isMember: e.target.checked }) })),
                el("td", null, el("input", { type: "checkbox", "aria-label": "On duty", disabled: !t.isMember, checked: t.isOnDuty, onChange: (e) => saveTeam(t, { isOnDuty: e.target.checked }) }), t.checkedInToday ? el("span", { style: MUTED }, " checked in") : null),
                el("td", null, el("input", { type: "checkbox", "aria-label": "Manager", disabled: !t.isMember, checked: t.isManager, onChange: (e) => saveTeam(t, { isManager: e.target.checked }) })),
                el("td", null, el("select", { className: "field__input", disabled: !t.isMember, value: t.locationId ?? "", onChange: (e) => saveTeam(t, { locationId: e.target.value ? Number(e.target.value) : null }) }, el("option", { value: "" }, "Any"), opt.locations.map((l) => el("option", { key: l.id, value: l.id }, l.name))))
              )
            )
          )
        )
      )
    ),
    el("div", { style: { height: 12 } }),
    el(
      Card,
      { title: "Consent and retention" },
      el(Textarea, { label: "Wording beside the tick box on the website", required: true, value: s.consentText, onChange: (e) => set({ consentText: e.target.value }), hint: "The wording shown at the time is copied onto each lead, so you can always prove what the person agreed to." }),
      el(Textarea, { label: "Marketing tick box (a separate box, never bundled with the first)", value: s.marketingText, onChange: (e) => set({ marketingText: e.target.value }) }),
      el("div", { className: "crm-grid3" }, el(Input, { label: "Anonymous visits kept (days)", type: "number", value: s.visitRetentionDays, onChange: (e) => set({ visitRetentionDays: e.target.value }) }), el(Input, { label: "A lost lead kept (months)", type: "number", value: s.lostRetentionMonths, onChange: (e) => set({ lostRetentionMonths: e.target.value }) }), el(Input, { label: "Privacy notice link", placeholder: "https://… or /privacy", value: s.privacyUrl || "", onChange: (e) => set({ privacyUrl: e.target.value }) })),
      el("p", { className: "crm-note" }, "A scheduled job deletes anonymous sessions and lost leads when these periods run out. Delete on request is one button on each lead.")
    ),
    el("div", { style: { height: 12 } }),
    el(
      Card,
      { title: "Website buttons" },
      el("div", { className: "crm-grid2" }, el(Input, { label: "WhatsApp number for the website button", placeholder: "+91 98450 00000", value: s.whatsAppNumber || "", onChange: (e) => set({ whatsAppNumber: e.target.value }), hint: "Leave blank to hide the WhatsApp button." }), el(Input, { label: "Phone number shown when something goes wrong", value: s.publicPhone || "", onChange: (e) => set({ publicPhone: e.target.value }) }))
    ),
    el("div", { style: { height: 12 } }),
    el(
      Card,
      { title: "Email notifications" },
      el(
        "div",
        { className: s.emailTestedUtc ? "crm-panel crm-panel--green" : "crm-panel crm-panel--amber", style: { marginTop: 0 } },
        s.emailTestedUtc ? `A test email was sent to ${s.emailTestedTo} on ${fmtDT(s.emailTestedUtc)}. Check that it arrived before switching notifications on.` : "Before switching on a single notification, prove that email actually leaves this server: send a test to an outside address and look at it. A reminder that silently fails is worse than no reminder at all."
      ),
      el("div", { style: { display: "flex", gap: 8, alignItems: "end", flexWrap: "wrap" } }, el("div", { style: { flex: "1 1 260px" } }, el(Input, { label: "Send a test email to", type: "email", value: testTo, onChange: (e) => setTestTo(e.target.value) })), el(Button, { variant: "outline", loading: testing, disabled: !testTo, onClick: test }, "Send test")),
      el("div", { style: { height: 8 } }),
      el(Checkbox, { label: "Send CRM emails (new lead to its owner, escalation to the manager)", checked: s.emailEnabled, disabled: !s.emailTestedUtc && !s.emailEnabled, onChange: (e) => set({ emailEnabled: e.target.checked }) }),
      el(Checkbox, { label: "Acknowledge website enquiries by email (“thanks, we will call you”) — only people who ticked the consent box", checked: s.ackEmailEnabled, disabled: !s.emailEnabled, onChange: (e) => set({ ackEmailEnabled: e.target.checked }) }),
      el(Checkbox, { label: "09:00 digest of each person's day, on working days", checked: s.digestEnabled, disabled: !s.emailEnabled, onChange: (e) => set({ digestEnabled: e.target.checked }) }),
      el("p", { className: "crm-note" }, "In-app alerts (on the Follow-ups page) always work. Nothing automatic is ever sent to a lead entered by hand. Press Save settings at the top to keep changes.")
    ),
    ruleEdit &&
      el(
        Dialog,
        { title: ruleEdit.id ? "Edit rule" : "Add rule", onClose: () => setRuleEdit(null), busy: false, okLabel: "Save", okDisabled: !ruleEdit.v.label?.trim(), onOk: saveRule },
        (() => {
          const v = ruleEdit.v;
          const setV = (p) => setRuleEdit((x) => ({ ...x, v: { ...x.v, ...p } }));
          const fallback = ruleEdit.id && rules.find((r) => r.ruleId === ruleEdit.id)?.conditionType === 5;
          return el(
            F,
            null,
            el(Input, { label: "Describe the rule", required: true, value: v.label || "", onChange: (e) => setV({ label: e.target.value }) }),
            el(Select, { label: "When", value: v.conditionType, disabled: fallback, options: fallback ? CONDITIONS : CONDITIONS.filter((c) => c.value !== "5"), onChange: (e) => setV({ conditionType: e.target.value }) }),
            v.conditionType === "2" && el(Select, { label: "Location", value: v.locationId ? String(v.locationId) : "", options: [{ value: "", label: "The one they asked for" }, ...opt.locations.map((l) => ({ value: String(l.id), label: l.name }))], onChange: (e) => setV({ locationId: e.target.value }) }),
            v.conditionType === "3" && el(F, null, el(Input, { label: "More than how many people", type: "number", value: v.minPeople || 5, onChange: (e) => setV({ minPeople: e.target.value }) }), el(Select, { label: "Or looking for", placeholder: "—", value: v.lookingForId ? String(v.lookingForId) : "", options: opt.lookingFor.map((l) => ({ value: String(l.lookingForId), label: l.name })), onChange: (e) => setV({ lookingForId: e.target.value }) })),
            el(Select, { label: "The lead goes to", value: v.targetType, disabled: fallback, options: TARGETS, onChange: (e) => setV({ targetType: e.target.value }) }),
            v.targetType === "6" && el(Select, { label: "Person", placeholder: "—", value: v.targetUserId ? String(v.targetUserId) : "", options: opt.team.map((t) => ({ value: String(t.userId), label: t.name })), onChange: (e) => setV({ targetUserId: e.target.value }) }),
            !fallback && el(Checkbox, { label: "Switched on", checked: v.isActive, onChange: (e) => setV({ isActive: e.target.checked }) })
          );
        })()
      )
  );
}
