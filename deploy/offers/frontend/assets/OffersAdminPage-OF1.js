/**
 * Offers Management, admin portal (23 Nov 2026, kit deploy/offers): Sales > Offers, /admin/offers.
 * Overview (total / active / inactive / expired / scheduled), search, filters, sort, pagination;
 * create and edit offers (discount, modules and plans, validity, code, limits, terms, picture);
 * activate / deactivate; delete while unused; who used an offer.
 *
 * Data: /api/admin/offers/* (see deploy/offers/README.md). OFFER.VIEW opens the page, OFFER.MANAGE
 * changes things. Hand-maintained chunk: index-DI5X4c9f.js imports it by name.
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
  G as Spinner
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { E as EmptyState } from "./EmptyState-B4UbbVj4.js";
import { dealText } from "./OffersKit-OF1.js";

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
.oa-cards{display:grid;grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:var(--space-3);margin-bottom:var(--space-4)}
.oa-card{all:unset;box-sizing:border-box;cursor:pointer;background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3) var(--space-4);box-shadow:var(--shadow-sm);min-width:0}
.oa-card:focus-visible{outline:2px solid var(--accent);outline-offset:2px}
.oa-card--on{border-color:var(--accent);background:rgb(var(--accent-rgb) / 9%)}
.oa-card__label{font-size:var(--font-size-sm);color:var(--text-secondary)}
.oa-card__value{font-size:1.8rem;font-weight:800;font-family:var(--font-display);line-height:1.2}
.oa-card--green .oa-card__value{color:var(--color-success)}
.oa-card--grey .oa-card__value{color:var(--text-muted)}
.oa-card--red .oa-card__value{color:var(--color-danger)}
.oa-card--blue .oa-card__value{color:var(--color-info)}
.oa-panel{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);padding:var(--space-4);box-shadow:var(--shadow-sm);min-width:0}
.oa-filters{display:grid;grid-template-columns:2fr 1fr 1fr 1fr;gap:var(--space-3);align-items:end;margin-bottom:var(--space-3)}
@media (max-width:900px){.oa-filters{grid-template-columns:1fr 1fr}}
.oa-scroll{overflow-x:auto}
.oa-table{width:100%;border-collapse:collapse}
.oa-table th{text-align:left;font-size:var(--font-size-xs);text-transform:uppercase;letter-spacing:.05em;color:var(--text-secondary);padding:10px;background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.oa-table td{padding:12px 10px;border-bottom:1px solid var(--border-subtle);vertical-align:middle;font-size:var(--font-size-sm)}
.oa-table .badge{white-space:nowrap}
.oa-name{font-weight:700}
.oa-muted{color:var(--text-muted);font-size:var(--font-size-xs)}
.oa-mono{font-family:ui-monospace,SFMono-Regular,Consolas,monospace;letter-spacing:.04em}
.oa-deal{font-weight:800;color:var(--regalis-gold-dark);white-space:nowrap}
.oa-mods{display:flex;gap:4px;flex-wrap:wrap}
.oa-actions{display:flex;gap:6px;justify-content:flex-end;flex-wrap:wrap}
.oa-foot{display:flex;justify-content:space-between;align-items:center;gap:var(--space-3);margin-top:var(--space-3);flex-wrap:wrap}
@media (max-width:760px){
 .oa-table thead{display:none}
 .oa-table,.oa-table tbody,.oa-table tr,.oa-table td{display:block;width:100%;box-sizing:border-box}
 .oa-table tr{border:1px solid var(--border-subtle);border-radius:var(--radius-md);margin-bottom:var(--space-3);padding:6px 4px}
 .oa-table td{border:0;padding:6px 10px}
 .oa-table td[data-l]:before{content:attr(data-l);display:block;font-size:var(--font-size-xs);color:var(--text-muted);text-transform:uppercase;letter-spacing:.04em}
 .oa-actions{justify-content:flex-start}
}
.modal:has(.oa-wide){max-width:min(860px,calc(100vw - 32px))}
.oa-form{display:flex;flex-direction:column;gap:var(--space-4)}
.oa-form section{border:1px solid var(--border-subtle);border-radius:var(--radius-md);padding:var(--space-3) var(--space-4)}
.oa-form h3{font-size:var(--font-size-md);font-weight:800;margin:0 0 var(--space-2)}
.oa-grid2{display:grid;grid-template-columns:1fr 1fr;gap:var(--space-3)}
.oa-grid3{display:grid;grid-template-columns:repeat(3,1fr);gap:var(--space-3)}
@media (max-width:640px){.oa-grid2,.oa-grid3{grid-template-columns:1fr}}
.oa-grid2>*,.oa-grid3>*{min-width:0}
.oa-seg{display:inline-flex;border:1px solid var(--border-strong);border-radius:var(--radius-sm);overflow:hidden;margin-bottom:var(--space-2)}
.oa-seg button{all:unset;cursor:pointer;padding:7px 14px;font-weight:600;font-size:var(--font-size-sm)}
.oa-seg button[aria-pressed=true]{background:var(--regalis-black);color:var(--regalis-gold-light)}
.oa-mod{border-top:1px dashed var(--border-subtle);padding-top:var(--space-2);margin-top:var(--space-2)}
.oa-mod:first-of-type{border-top:0;margin-top:0}
.oa-refs{display:grid;grid-template-columns:repeat(auto-fill,minmax(210px,1fr));gap:4px 12px;margin:6px 0 0 28px}
.oa-textarea{width:100%;box-sizing:border-box;min-height:80px;font:inherit;padding:8px 10px;border:1px solid var(--border-strong);border-radius:var(--radius-sm);background:var(--surface-card);color:var(--text-primary)}
.oa-err{color:var(--color-danger);font-size:var(--font-size-xs);margin-top:4px}
.oa-img{display:flex;gap:var(--space-3);align-items:center;flex-wrap:wrap}
.oa-img img{width:220px;max-width:100%;aspect-ratio:16/6;object-fit:cover;border-radius:var(--radius-sm);border:1px solid var(--border-subtle)}
.oa-preview{max-width:340px}
`;
function useCss() {
  React.useEffect(() => {
    if (document.getElementById("rg-offers-admin-css")) return;
    const s = document.createElement("style");
    s.id = "rg-offers-admin-css";
    s.textContent = CSS;
    document.head.appendChild(s);
  }, []);
}

const MODULES = [
  [1, "My Bookings"],
  [2, "Passes"],
  [3, "Membership"]
];
const MOD_NAME = Object.fromEntries(MODULES);
const STATUS_TONE = { Active: "success", Inactive: "neutral", Expired: "danger", Scheduled: "info" };
const money = (n) => "₹" + Number(n || 0).toLocaleString("en-IN", { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const day = (d) => {
  if (!d) return "";
  const [y, m, dd] = String(d).slice(0, 10).split("-").map(Number);
  return new Date(y, m - 1, dd).toLocaleDateString("en-IN", { day: "numeric", month: "short", year: "numeric" });
};
const when = (iso) => (iso ? new Date(String(iso).endsWith("Z") ? iso : iso + "Z").toLocaleString("en-IN", { day: "numeric", month: "short", year: "numeric", hour: "2-digit", minute: "2-digit" }) : "");
const todayIso = () => {
  const n = new Date(Date.now() + 330 * 60000);
  return n.toISOString().slice(0, 10);
};
const errMsg = (e, fb) => (e instanceof ApiError ? e.message : fb);
const fieldErrors = (e) => {
  const out = {};
  for (const x of e?.response?.errors || []) if (x.field) out[x.field] = out[x.field] || x.message;
  return out;
};

function Pager({ page, onPage }) {
  if (!page || page.totalPages <= 1) return null;
  return el(
    "div",
    { style: { display: "flex", gap: 6, alignItems: "center" } },
    el("span", { className: "oa-muted" }, `Page ${page.page} of ${page.totalPages}`),
    el(Button, { size: "sm", variant: "outline", disabled: !page.hasPrevious, onClick: () => onPage(page.page - 1), "aria-label": "Previous page" }, "‹"),
    el(Button, { size: "sm", variant: "outline", disabled: !page.hasNext, onClick: () => onPage(page.page + 1), "aria-label": "Next page" }, "›")
  );
}

function Confirm({ title, children, okLabel, danger, onOk, onClose }) {
  const [busy, setBusy] = React.useState(false);
  return el(
    Modal,
    {
      open: true,
      title,
      onClose,
      footer: el(
        F,
        null,
        el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"),
        el(Button, { variant: danger ? "danger" : "primary", loading: busy, onClick: async () => (setBusy(true), await onOk(), setBusy(false)) }, okLabel)
      )
    },
    children
  );
}

/* ---------------------------------------------------------------------------------------- */
/* page                                                                                       */
/* ---------------------------------------------------------------------------------------- */

export function OffersAdminPage() {
  useCss();
  const toast = useToast();
  const canManage = usePermission("OFFER.MANAGE");
  const [f, setF] = React.useState({ search: "", status: "all", module: "", sort: "priority" });
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [stamp, setStamp] = React.useState(0);
  const [dialog, setDialog] = React.useState(null);
  const [busy, setBusy] = React.useState(null);
  const reload = () => setStamp((s) => s + 1);

  React.useEffect(() => {
    let dead = false;
    const t = setTimeout(() => {
      unwrap(http.get("/admin/offers", { params: { search: f.search || void 0, status: f.status, module: f.module || void 0, sort: f.sort, page, pageSize: 20 } }))
        .then((d) => dead || setData(d))
        .catch((e) => dead || (toast.error(errMsg(e, "Could not load the offers.")), setData((d) => d || { summary: {}, page: { items: [] } })));
    }, 200);
    return () => {
      dead = true;
      clearTimeout(t);
    };
  }, [f, page, stamp]);

  const setStatus = async (o, on) => {
    setBusy(o.offerId);
    try {
      await unwrap(http.post(`/admin/offers/${o.offerId}/${on ? "activate" : "deactivate"}`));
      toast.success(on ? `${o.offerName} is live in the customer portal.` : `${o.offerName} is switched off.`);
      reload();
    } catch (e) {
      toast.error(errMsg(e, "Could not change the offer."));
    } finally {
      setBusy(null);
    }
  };
  const remove = async (o) => {
    try {
      await unwrap(http.delete(`/admin/offers/${o.offerId}`));
      toast.success(`${o.offerName} deleted.`);
      setDialog(null);
      reload();
    } catch (e) {
      toast.error(errMsg(e, "Could not delete the offer."));
      setDialog(null);
    }
  };

  const s = data?.summary || {};
  const card = (key, label, value, tone) =>
    el(
      "button",
      { type: "button", key, className: `oa-card oa-card--${tone}${f.status === key ? " oa-card--on" : ""}`, "aria-pressed": f.status === key ? "true" : "false", onClick: () => (setF({ ...f, status: key }), setPage(1)) },
      el("div", { className: "oa-card__label" }, label),
      el("div", { className: "oa-card__value" }, value ?? "–")
    );
  const rows = data?.page?.items || [];
  return el(
    F,
    null,
    el(PageHeader, {
      title: "Offers",
      description: "Promotions customers see and apply in My Bookings, Passes and Membership. An active offer inside its dates appears in the modules you pick; the discount comes off the price before GST and is checked again on the server before payment.",
      actions: canManage ? el(Button, { variant: "primary", onClick: () => setDialog({ kind: "edit", id: null }) }, "New offer") : null
    }),
    el(
      "div",
      { className: "oa-cards", role: "group", "aria-label": "Offers overview" },
      card("all", "Total offers", s.total, "gold"),
      card("active", "Active", s.active, "green"),
      card("inactive", "Inactive", s.inactive, "grey"),
      card("expired", "Expired", s.expired, "red"),
      card("scheduled", "Scheduled", s.scheduled, "blue")
    ),
    el(
      "div",
      { className: "oa-panel" },
      el(
        "div",
        { className: "oa-filters" },
        el(Input, { label: "Search", placeholder: "Name, title or code", value: f.search, onChange: (e) => (setF({ ...f, search: e.target.value }), setPage(1)) }),
        el(Select, {
          label: "Status",
          value: f.status,
          options: [
            { value: "all", label: "All" },
            { value: "active", label: "Active" },
            { value: "inactive", label: "Inactive" },
            { value: "expired", label: "Expired" },
            { value: "scheduled", label: "Scheduled" }
          ],
          onChange: (e) => (setF({ ...f, status: e.target.value }), setPage(1))
        }),
        el(Select, {
          label: "Module",
          value: f.module,
          options: [{ value: "", label: "All modules" }, ...MODULES.map(([v, l]) => ({ value: String(v), label: l }))],
          onChange: (e) => (setF({ ...f, module: e.target.value }), setPage(1))
        }),
        el(Select, {
          label: "Sort by",
          value: f.sort,
          options: [
            { value: "priority", label: "Display priority" },
            { value: "newest", label: "Newest first" },
            { value: "ending", label: "Ending soonest" },
            { value: "name", label: "Name" }
          ],
          onChange: (e) => setF({ ...f, sort: e.target.value })
        })
      ),
      !data
        ? el(Spinner, { center: true })
        : rows.length === 0
          ? el(EmptyState, {
              title: f.search || f.status !== "all" || f.module ? "No offer matches" : "No offers yet",
              description: f.search || f.status !== "all" || f.module ? "Try other filters." : canManage ? "Create an offer, choose where it applies and activate it. Customers see it straight away." : "Offers created by an administrator are listed here."
            })
          : el(
              "div",
              { className: "oa-scroll" },
              el(
                "table",
                { className: "oa-table" },
                el("thead", null, el("tr", null, ["Offer", "Discount", "Applies to", "Validity", "Status", "Used", "Created", ""].map((h) => el("th", { key: h }, h)))),
                el(
                  "tbody",
                  null,
                  rows.map((o) =>
                    el(
                      "tr",
                      { key: o.offerId, "data-offer": o.offerId },
                      el(
                        "td",
                        { "data-l": "Offer" },
                        el("div", { className: "oa-name" }, o.offerName),
                        el("div", { className: "oa-muted" }, o.title),
                        o.code ? el("div", { className: "oa-mono oa-muted" }, o.code, o.showInPortal ? "" : " · code only") : null
                      ),
                      el(
                        "td",
                        { "data-l": "Discount" },
                        el("div", { className: "oa-deal" }, dealText(o)),
                        o.maxDiscountAmount ? el("div", { className: "oa-muted" }, `max ${money(o.maxDiscountAmount)}`) : null,
                        o.minAmount ? el("div", { className: "oa-muted" }, `min ${money(o.minAmount)}`) : null
                      ),
                      el("td", { "data-l": "Applies to" }, el("div", { className: "oa-mods" }, o.modules.map((m) => el(Badge, { key: m, tone: "gold" }, MOD_NAME[m]))), o.canCombine ? el("div", { className: "oa-muted" }, "Can combine") : null),
                      el("td", { "data-l": "Validity" }, day(o.startDate), " – ", day(o.endDate)),
                      el("td", { "data-l": "Status" }, el(Badge, { tone: STATUS_TONE[o.status] || "neutral" }, o.status)),
                      el("td", { "data-l": "Used" }, el("div", null, o.uses, " time", o.uses === 1 ? "" : "s"), o.discountGiven > 0 ? el("div", { className: "oa-muted" }, money(o.discountGiven), " off") : null),
                      el("td", { "data-l": "Created" }, day(String(o.createdDate).slice(0, 10))),
                      el(
                        "td",
                        null,
                        el(
                          "div",
                          { className: "oa-actions" },
                          el(Button, { size: "sm", variant: "outline", onClick: () => setDialog({ kind: "uses", offer: o }) }, "Uses"),
                          canManage ? el(Button, { size: "sm", variant: "outline", onClick: () => setDialog({ kind: "edit", id: o.offerId }) }, "Edit") : null,
                          canManage && o.status !== "Expired"
                            ? o.isActive
                              ? el(Button, { size: "sm", variant: "outline", loading: busy === o.offerId, onClick: () => setStatus(o, false) }, "Deactivate")
                              : el(Button, { size: "sm", variant: "primary", loading: busy === o.offerId, onClick: () => setStatus(o, true) }, "Activate")
                            : null,
                          canManage ? el(Button, { size: "sm", variant: "danger-outline", onClick: () => setDialog({ kind: "delete", offer: o }) }, "Delete") : null
                        )
                      )
                    )
                  )
                )
              )
            ),
      el("div", { className: "oa-foot" }, el("span", { className: "oa-muted" }, data?.page ? `${data.page.totalCount} offer${data.page.totalCount === 1 ? "" : "s"}` : ""), el(Pager, { page: data?.page, onPage: setPage }))
    ),
    dialog?.kind === "edit" ? el(OfferEditor, { id: dialog.id, onClose: () => setDialog(null), onSaved: () => (setDialog(null), reload()) }) : null,
    dialog?.kind === "uses" ? el(UsesDialog, { offer: dialog.offer, onClose: () => setDialog(null) }) : null,
    dialog?.kind === "delete"
      ? el(
          Confirm,
          { title: "Delete this offer?", okLabel: "Delete", danger: true, onClose: () => setDialog(null), onOk: () => remove(dialog.offer) },
          el("p", null, el("b", null, dialog.offer.offerName), " is removed from the admin list and the customer portal."),
          el("p", { className: "oa-muted" }, "An offer customers have already used cannot be deleted; it is kept for the record. Deactivate it instead.")
        )
      : null
  );
}

/* ---------------------------------------------------------------------------------------- */
/* create / edit                                                                              */
/* ---------------------------------------------------------------------------------------- */

const BLANK = () => ({
  offerName: "",
  title: "",
  description: "",
  terms: "",
  code: "",
  showInPortal: true,
  discountType: 1,
  discountValue: "",
  maxDiscountAmount: "",
  minAmount: "",
  modules: [],
  workspaceTypeIds: [],
  passPlanIds: [],
  membershipPlanIds: [],
  startDate: todayIso(),
  endDate: "",
  isActive: true,
  canCombine: false,
  sortOrder: "100",
  usesPerCustomer: "",
  totalUses: ""
});

function Textarea({ label, hint, value, onChange, maxLength, error }) {
  const id = React.useId();
  return el(
    "div",
    { className: "field" },
    el("label", { className: "field__label", htmlFor: id }, label),
    el("textarea", { id, className: "oa-textarea", value, onChange, maxLength }),
    error ? el("div", { className: "oa-err" }, error) : hint ? el("span", { className: "field__hint" }, hint) : null
  );
}

function OfferEditor({ id, onClose, onSaved }) {
  const toast = useToast();
  const [v, setV] = React.useState(null);
  const [opts, setOpts] = React.useState(null);
  const [errs, setErrs] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  const [file, setFile] = React.useState(null);
  const [detail, setDetail] = React.useState(null);
  const fileRef = React.useRef(null);
  React.useEffect(() => {
    unwrap(http.get("/admin/offers/options")).then(setOpts).catch(() => setOpts({ workspaceTypes: [], passPlans: [], membershipPlans: [] }));
    if (id == null) {
      setV(BLANK());
      return;
    }
    unwrap(http.get(`/admin/offers/${id}`))
      .then((d) => {
        setDetail(d);
        setV({
          offerName: d.offerName,
          title: d.title,
          description: d.description || "",
          terms: d.terms || "",
          code: d.code || "",
          showInPortal: d.showInPortal,
          discountType: d.discountType,
          discountValue: String(d.discountValue),
          maxDiscountAmount: d.maxDiscountAmount != null ? String(d.maxDiscountAmount) : "",
          minAmount: d.minAmount != null ? String(d.minAmount) : "",
          modules: d.modules,
          workspaceTypeIds: d.workspaceTypeIds,
          passPlanIds: d.passPlanIds,
          membershipPlanIds: d.membershipPlanIds,
          startDate: String(d.startDate).slice(0, 10),
          endDate: String(d.endDate).slice(0, 10),
          isActive: d.isActive,
          canCombine: d.canCombine,
          sortOrder: String(d.sortOrder),
          usesPerCustomer: d.usesPerCustomer != null ? String(d.usesPerCustomer) : "",
          totalUses: d.totalUses != null ? String(d.totalUses) : ""
        });
      })
      .catch((e) => (toast.error(errMsg(e, "Could not open the offer.")), onClose()));
  }, [id]);

  if (!v || !opts) return el(Modal, { open: true, title: id == null ? "New offer" : "Edit offer", onClose }, el(Spinner, { center: true }));
  const set = (patch) => setV({ ...v, ...patch });
  const num = (s) => (String(s).trim() === "" ? null : Number(s));
  const toggleIn = (key, idv) => set({ [key]: v[key].includes(idv) ? v[key].filter((x) => x !== idv) : [...v[key], idv] });

  const save = async () => {
    setBusy(true);
    setErrs({});
    const body = {
      ...v,
      code: v.code.trim() || null,
      discountType: Number(v.discountType),
      discountValue: Number(v.discountValue || 0),
      maxDiscountAmount: Number(v.discountType) === 1 ? num(v.maxDiscountAmount) : null,
      minAmount: num(v.minAmount),
      startDate: v.startDate || null,
      endDate: v.endDate || null,
      sortOrder: Number(v.sortOrder || 0),
      usesPerCustomer: num(v.usesPerCustomer),
      totalUses: num(v.totalUses)
    };
    try {
      const saved = id == null ? await unwrap(http.post("/admin/offers", body)) : await unwrap(http.put(`/admin/offers/${id}`, body));
      if (file) {
        const fd = new FormData();
        fd.append("file", file, file.name);
        try {
          await unwrap(http.post(`/admin/offers/${saved.offerId}/image`, fd, { headers: { "Content-Type": "multipart/form-data" } }));
        } catch (e) {
          toast.error(errMsg(e, "The offer was saved, but the picture was not."));
        }
      }
      toast.success(id == null ? (saved.isActive ? "Offer created and live in the customer portal." : "Offer created (inactive).") : "Offer saved.");
      onSaved();
    } catch (e) {
      const fe = fieldErrors(e);
      setErrs(fe);
      toast.error(Object.keys(fe).length ? "Check the highlighted fields." : errMsg(e, "Could not save the offer."));
    } finally {
      setBusy(false);
    }
  };
  const removeImage = async () => {
    try {
      const d = await unwrap(http.delete(`/admin/offers/${id}/image`));
      setDetail(d);
      toast.success("Picture removed.");
    } catch (e) {
      toast.error(errMsg(e, "Could not remove the picture."));
    }
  };

  const refList = (key, items) =>
    items.length === 0
      ? null
      : el(
          "div",
          { className: "oa-refs" },
          items.map((it) => el(Checkbox, { key: it.id, label: it.name, checked: v[key].includes(it.id), onChange: () => toggleIn(key, it.id) }))
        );
  const modBlock = (m, label, key, items, noun) => {
    const on = v.modules.includes(m);
    return el(
      "div",
      { className: "oa-mod", key: m },
      el(Checkbox, { label, checked: on, onChange: () => set({ modules: on ? v.modules.filter((x) => x !== m) : [...v.modules, m] }) }),
      on
        ? el(
            F,
            null,
            el("div", { className: "oa-muted", style: { margin: "4px 0 0 28px" } }, v[key].length ? `Only the ticked ${noun}:` : `Every ${noun.replace(/s$/, "")} (tick some to narrow it down):`),
            refList(key, items)
          )
        : null
    );
  };
  const pct = Number(v.discountType) === 1;
  const imgUrl = detail?.hasImage ? `/api/offers/${detail.offerId}/image?v=${detail.imageVersion}` : null;
  return el(
    Modal,
    {
      open: true,
      title: id == null ? "New offer" : `Edit offer · ${detail?.offerName || ""}`,
      onClose,
      footer: el(F, null, el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"), el(Button, { variant: "primary", loading: busy, onClick: save }, id == null ? "Create offer" : "Save"))
    },
    el(
      "div",
      { className: "oa-form oa-wide" },
      el(
        "section",
        null,
        el("h3", null, "What customers see"),
        el(
          "div",
          { className: "oa-grid2" },
          el(Input, { label: "Offer name (internal)", required: true, value: v.offerName, maxLength: 120, error: errs.offerName, onChange: (e) => set({ offerName: e.target.value }) }),
          el(Input, { label: "Offer title", required: true, value: v.title, maxLength: 150, error: errs.title, hint: "The headline on the offer card.", onChange: (e) => set({ title: e.target.value }) })
        ),
        el(Textarea, { label: "Description", value: v.description, maxLength: 1000, error: errs.description, onChange: (e) => set({ description: e.target.value }) }),
        el(
          "div",
          { className: "field" },
          el("span", { className: "field__label" }, "Banner picture (optional)"),
          el(
            "div",
            { className: "oa-img" },
            imgUrl && !file ? el("img", { src: imgUrl, alt: "Current offer picture" }) : null,
            el("input", { ref: fileRef, type: "file", accept: "image/png,image/jpeg,image/webp", onChange: (e) => setFile(e.target.files?.[0] || null) }),
            imgUrl && !file ? el(Button, { size: "sm", variant: "ghost", onClick: removeImage }, "Remove picture") : null
          ),
          el("span", { className: "field__hint" }, "PNG, JPG or WebP under 2 MB; shown wide (about 16:6) at the top of the card.")
        )
      ),
      el(
        "section",
        null,
        el("h3", null, "Discount"),
        el(
          "div",
          { className: "oa-seg", role: "group", "aria-label": "Discount type" },
          el("button", { type: "button", "aria-pressed": pct ? "true" : "false", onClick: () => set({ discountType: 1 }) }, "Percentage"),
          el("button", { type: "button", "aria-pressed": pct ? "false" : "true", onClick: () => set({ discountType: 2 }) }, "Fixed amount")
        ),
        el(
          "div",
          { className: "oa-grid3" },
          el(Input, { label: pct ? "Discount %" : "Discount (₹)", required: true, type: "number", min: 0, step: "0.01", value: v.discountValue, error: errs.discountValue, onChange: (e) => set({ discountValue: e.target.value }) }),
          pct ? el(Input, { label: "Maximum discount (₹)", type: "number", min: 0, step: "0.01", value: v.maxDiscountAmount, error: errs.maxDiscountAmount, hint: "Optional cap.", onChange: (e) => set({ maxDiscountAmount: e.target.value }) }) : el("div"),
          el(Input, { label: "Minimum purchase (₹)", type: "number", min: 0, step: "0.01", value: v.minAmount, error: errs.minAmount, hint: "Optional, before GST.", onChange: (e) => set({ minAmount: e.target.value }) })
        ),
        el("div", { className: "oa-muted" }, "The discount comes off the price before GST; GST is charged on what is left. A fixed amount never takes a price below zero.")
      ),
      el(
        "section",
        null,
        el("h3", null, "Where it applies"),
        errs.modules ? el("div", { className: "oa-err" }, errs.modules) : null,
        modBlock(1, "My Bookings (hourly and monthly workspace bookings)", "workspaceTypeIds", opts.workspaceTypes, "workspace types"),
        modBlock(2, "Passes (day passes)", "passPlanIds", opts.passPlans, "pass plans"),
        modBlock(3, "Membership (monthly plans, Virtual Office included)", "membershipPlanIds", opts.membershipPlans, "membership plans"),
        errs.workspaceTypeIds || errs.passPlanIds || errs.membershipPlanIds ? el("div", { className: "oa-err" }, errs.workspaceTypeIds || errs.passPlanIds || errs.membershipPlanIds) : null
      ),
      el(
        "section",
        null,
        el("h3", null, "Validity and status"),
        el(
          "div",
          { className: "oa-grid2" },
          el(Input, { label: "Start date", required: true, type: "date", value: v.startDate, error: errs.startDate, onChange: (e) => set({ startDate: e.target.value }) }),
          el(Input, { label: "End date", required: true, type: "date", value: v.endDate, min: v.startDate || void 0, error: errs.endDate, hint: "Last day it can be used (inclusive).", onChange: (e) => set({ endDate: e.target.value }) })
        ),
        el(Checkbox, { label: "Active (shown and usable while inside its dates)", checked: v.isActive, onChange: (e) => set({ isActive: e.target.checked }) })
      ),
      el(
        "section",
        null,
        el("h3", null, "Code, display and limits"),
        el(
          "div",
          { className: "oa-grid3" },
          el(Input, { label: "Offer code (optional)", value: v.code, maxLength: 30, error: errs.code, hint: "Letters, digits, - or _.", onChange: (e) => set({ code: e.target.value.toUpperCase().replace(/\s/g, "") }) }),
          el(Input, { label: "Display priority", type: "number", min: 0, max: 9999, value: v.sortOrder, error: errs.sortOrder, hint: "Lower shows first.", onChange: (e) => set({ sortOrder: e.target.value }) }),
          el("div")
        ),
        el(Checkbox, { label: "Show on the customer portal (untick to make it work only when the code is typed)", checked: v.showInPortal, onChange: (e) => set({ showInPortal: e.target.checked }) }),
        el(Checkbox, { label: "Can be combined with other offers that also allow it", checked: v.canCombine, onChange: (e) => set({ canCombine: e.target.checked }) }),
        el(
          "div",
          { className: "oa-grid2", style: { marginTop: "var(--space-2)" } },
          el(Input, { label: "Uses per customer", type: "number", min: 1, value: v.usesPerCustomer, error: errs.usesPerCustomer, hint: "Empty = no limit.", onChange: (e) => set({ usesPerCustomer: e.target.value }) }),
          el(Input, { label: "Total uses", type: "number", min: 1, value: v.totalUses, error: errs.totalUses, hint: "Empty = no limit.", onChange: (e) => set({ totalUses: e.target.value }) })
        )
      ),
      el("section", null, el("h3", null, "Terms & conditions"), el(Textarea, { label: "Terms shown to customers", value: v.terms, maxLength: 4000, error: errs.terms, onChange: (e) => set({ terms: e.target.value }) }))
    )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* who used it                                                                                */
/* ---------------------------------------------------------------------------------------- */

function UsesDialog({ offer, onClose }) {
  const toast = useToast();
  const [rows, setRows] = React.useState(null);
  React.useEffect(() => {
    unwrap(http.get(`/admin/offers/${offer.offerId}/redemptions`))
      .then(setRows)
      .catch((e) => (toast.error(errMsg(e, "Could not load the uses.")), setRows([])));
  }, [offer.offerId]);
  const TONE = { Used: "success", Held: "warning", Released: "neutral" };
  return el(
    Modal,
    { open: true, title: `Uses · ${offer.offerName}`, onClose, footer: el(Button, { onClick: onClose }, "Close") },
    el(
      "div",
      { className: "oa-wide" },
      el("p", { className: "oa-muted", style: { marginTop: 0 } }, "Held: a booking awaiting payment or an online order in progress. Released: cancelled, expired or abandoned; the use is given back."),
      !rows
        ? el(Spinner, { center: true })
        : rows.length === 0
          ? el(EmptyState, { title: "Not used yet", description: "Each booking, pass or membership bought with this offer is listed here." })
          : el(
              "div",
              { className: "oa-scroll" },
              el(
                "table",
                { className: "oa-table" },
                el("thead", null, el("tr", null, ["When", "Customer", "Module", "Reference", "Price before GST", "Discount", "State"].map((h) => el("th", { key: h }, h)))),
                el(
                  "tbody",
                  null,
                  rows.map((r) =>
                    el(
                      "tr",
                      { key: r.redemptionId },
                      el("td", { "data-l": "When" }, when(r.createdUtc)),
                      el("td", { "data-l": "Customer" }, r.customerName),
                      el("td", { "data-l": "Module" }, MOD_NAME[r.module]),
                      el("td", { "data-l": "Reference", className: "oa-mono" }, r.reference),
                      el("td", { "data-l": "Price before GST" }, money(r.baseAmount)),
                      el("td", { "data-l": "Discount" }, "− ", money(r.discountAmount)),
                      el("td", { "data-l": "State" }, el(Badge, { tone: TONE[r.state] || "neutral" }, r.state))
                    )
                  )
                )
              )
            )
    )
  );
}
