/**
 * Coupon codes, admin portal (25 Nov 2026, kit deploy/coupons): the "Coupon codes" tab of
 * Sales > Offers (/admin/offers?tab=coupons). Overview (total / active / inactive / expired /
 * scheduled), search, filters, sort, pagination; create and edit coupons (code, discount, validity,
 * usage limits, minimum booking amount, services); activate / deactivate; delete while unused; who
 * used a coupon.
 *
 * Data: /api/admin/coupons/* (see deploy/coupons/README.md). OFFER.VIEW opens the tab, OFFER.MANAGE
 * changes things. Uses the Offers page's styles (oa-*), which OffersAdminPage injects. Hand-maintained
 * chunk, imported by OffersAdminPage-OF1.js.
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
import { couponDeal } from "./CouponsKit-CP1.js";

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
const todayIso = () => new Date(Date.now() + 330 * 60000).toISOString().slice(0, 10);
const errMsg = (e, fb) => (e instanceof ApiError ? e.message : fb);
const fieldErrors = (e) => {
  const out = {};
  for (const x of e?.response?.errors || []) if (x.field) out[x.field] = out[x.field] || x.message;
  return out;
};
const limitText = (c) =>
  [c.totalUses ? `${c.totalUses} in total` : null, c.usesPerCustomer ? `${c.usesPerCustomer} per customer` : null].filter(Boolean).join(" · ") || "No limit";

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
/* tab                                                                                        */
/* ---------------------------------------------------------------------------------------- */

export function CouponsTab({ tabs = null }) {
  const toast = useToast();
  const canManage = usePermission("OFFER.MANAGE");
  const [f, setF] = React.useState({ search: "", status: "all", module: "", sort: "newest" });
  const [page, setPage] = React.useState(1);
  const [data, setData] = React.useState(null);
  const [stamp, setStamp] = React.useState(0);
  const [dialog, setDialog] = React.useState(null);
  const [busy, setBusy] = React.useState(null);
  const reload = () => setStamp((s) => s + 1);

  React.useEffect(() => {
    let dead = false;
    const t = setTimeout(() => {
      unwrap(http.get("/admin/coupons", { params: { search: f.search || void 0, status: f.status, module: f.module || void 0, sort: f.sort, page, pageSize: 20 } }))
        .then((d) => dead || setData(d))
        .catch((e) => dead || (toast.error(errMsg(e, "Could not load the coupons.")), setData((d) => d || { summary: {}, page: { items: [] } })));
    }, 200);
    return () => {
      dead = true;
      clearTimeout(t);
    };
  }, [f, page, stamp]);

  const setStatus = async (c, on) => {
    setBusy(c.couponId);
    try {
      await unwrap(http.post(`/admin/coupons/${c.couponId}/${on ? "activate" : "deactivate"}`));
      toast.success(on ? `${c.code} can be used now.` : `${c.code} is switched off.`);
      reload();
    } catch (e) {
      toast.error(errMsg(e, "Could not change the coupon."));
    } finally {
      setBusy(null);
    }
  };
  const remove = async (c) => {
    try {
      await unwrap(http.delete(`/admin/coupons/${c.couponId}`));
      toast.success(`${c.code} deleted.`);
      setDialog(null);
      reload();
    } catch (e) {
      toast.error(errMsg(e, "Could not delete the coupon."));
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
  const filtered = f.search || f.status !== "all" || f.module;
  return el(
    F,
    null,
    el(PageHeader, {
      title: "Offers",
      description: "Coupon codes customers type at payment in My Bookings, Passes and Membership. A coupon replaces any automatic offer on that purchase; the discount comes off the price before GST and is checked again on the server before payment.",
      actions: canManage ? el(Button, { variant: "primary", onClick: () => setDialog({ kind: "edit", id: null }) }, "New coupon") : null
    }),
    tabs,
    el(
      "div",
      { className: "oa-cards", role: "group", "aria-label": "Coupons overview" },
      card("all", "Total coupons", s.total, "gold"),
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
        el(Input, { label: "Search", placeholder: "Code or description", value: f.search, onChange: (e) => (setF({ ...f, search: e.target.value }), setPage(1)) }),
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
          label: "Service",
          value: f.module,
          options: [{ value: "", label: "All services" }, ...MODULES.map(([v, l]) => ({ value: String(v), label: l }))],
          onChange: (e) => (setF({ ...f, module: e.target.value }), setPage(1))
        }),
        el(Select, {
          label: "Sort by",
          value: f.sort,
          options: [
            { value: "newest", label: "Newest first" },
            { value: "ending", label: "Ending soonest" },
            { value: "code", label: "Code" }
          ],
          onChange: (e) => setF({ ...f, sort: e.target.value })
        })
      ),
      !data
        ? el(Spinner, { center: true })
        : rows.length === 0
          ? el(EmptyState, {
              title: filtered ? "No coupon matches" : "No coupons yet",
              description: filtered ? "Try other filters." : canManage ? "Create a coupon code, choose where it can be used and activate it." : "Coupons created by an administrator are listed here."
            })
          : el(
              "div",
              { className: "oa-scroll" },
              el(
                "table",
                { className: "oa-table" },
                el("thead", null, el("tr", null, ["Coupon code", "Discount", "Services", "Validity", "Usage", "Status", "Created", ""].map((h) => el("th", { key: h }, h)))),
                el(
                  "tbody",
                  null,
                  rows.map((c) =>
                    el(
                      "tr",
                      { key: c.couponId, "data-coupon": c.code },
                      el(
                        "td",
                        { "data-l": "Coupon code" },
                        el("div", { className: "oa-name oa-mono" }, c.code),
                        c.description ? el("div", { className: "oa-muted" }, c.description) : null,
                        c.offerClash ? el("div", { className: "oa-err" }, "An offer also uses this code; customers get the coupon.") : null
                      ),
                      el(
                        "td",
                        { "data-l": "Discount" },
                        el("div", { className: "oa-deal" }, couponDeal(c)),
                        c.maxDiscountAmount ? el("div", { className: "oa-muted" }, `max ${money(c.maxDiscountAmount)}`) : null,
                        c.minAmount ? el("div", { className: "oa-muted" }, `min booking ${money(c.minAmount)}`) : null
                      ),
                      el("td", { "data-l": "Services" }, el("div", { className: "oa-mods" }, c.modules.map((m) => el(Badge, { key: m, tone: "gold" }, MOD_NAME[m])))),
                      el("td", { "data-l": "Validity" }, day(c.validFrom), " – ", day(c.validUntil)),
                      el(
                        "td",
                        { "data-l": "Usage" },
                        el("div", null, c.uses, c.totalUses ? ` of ${c.totalUses}` : "", " used"),
                        el("div", { className: "oa-muted" }, limitText(c)),
                        c.discountGiven > 0 ? el("div", { className: "oa-muted" }, money(c.discountGiven), " off") : null
                      ),
                      el("td", { "data-l": "Status" }, el(Badge, { tone: STATUS_TONE[c.status] || "neutral" }, c.status)),
                      el("td", { "data-l": "Created" }, day(String(c.createdDate).slice(0, 10))),
                      el(
                        "td",
                        null,
                        el(
                          "div",
                          { className: "oa-actions" },
                          el(Button, { size: "sm", variant: "outline", onClick: () => setDialog({ kind: "uses", coupon: c }) }, "Uses"),
                          canManage ? el(Button, { size: "sm", variant: "outline", onClick: () => setDialog({ kind: "edit", id: c.couponId }) }, "Edit") : null,
                          canManage && c.status !== "Expired"
                            ? c.isActive
                              ? el(Button, { size: "sm", variant: "outline", loading: busy === c.couponId, onClick: () => setStatus(c, false) }, "Deactivate")
                              : el(Button, { size: "sm", variant: "primary", loading: busy === c.couponId, onClick: () => setStatus(c, true) }, "Activate")
                            : null,
                          canManage ? el(Button, { size: "sm", variant: "danger-outline", onClick: () => setDialog({ kind: "delete", coupon: c }) }, "Delete") : null
                        )
                      )
                    )
                  )
                )
              )
            ),
      el("div", { className: "oa-foot" }, el("span", { className: "oa-muted" }, data?.page ? `${data.page.totalCount} coupon${data.page.totalCount === 1 ? "" : "s"}` : ""), el(Pager, { page: data?.page, onPage: setPage }))
    ),
    dialog?.kind === "edit" ? el(CouponEditor, { id: dialog.id, onClose: () => setDialog(null), onSaved: () => (setDialog(null), reload()) }) : null,
    dialog?.kind === "uses" ? el(UsesDialog, { coupon: dialog.coupon, onClose: () => setDialog(null) }) : null,
    dialog?.kind === "delete"
      ? el(
          Confirm,
          { title: "Delete this coupon?", okLabel: "Delete", danger: true, onClose: () => setDialog(null), onOk: () => remove(dialog.coupon) },
          el("p", null, el("b", { className: "oa-mono" }, dialog.coupon.code), " is removed from the admin list and stops working in the customer portal."),
          el("p", { className: "oa-muted" }, "A coupon customers have already used cannot be deleted; it is kept for the record. Deactivate it instead.")
        )
      : null
  );
}

/* ---------------------------------------------------------------------------------------- */
/* create / edit                                                                              */
/* ---------------------------------------------------------------------------------------- */

const BLANK = () => ({
  code: "",
  description: "",
  discountType: 1,
  discountValue: "",
  maxDiscountAmount: "",
  minAmount: "",
  modules: [],
  validFrom: todayIso(),
  validUntil: "",
  totalUses: "",
  usesPerCustomer: "1",
  isActive: true
});

function CouponEditor({ id, onClose, onSaved }) {
  const toast = useToast();
  const [v, setV] = React.useState(null);
  const [detail, setDetail] = React.useState(null);
  const [errs, setErrs] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => {
    if (id == null) {
      setV(BLANK());
      return;
    }
    unwrap(http.get(`/admin/coupons/${id}`))
      .then((d) => {
        setDetail(d);
        setV({
          code: d.code,
          description: d.description || "",
          discountType: d.discountType,
          discountValue: String(d.discountValue),
          maxDiscountAmount: d.maxDiscountAmount != null ? String(d.maxDiscountAmount) : "",
          minAmount: d.minAmount != null ? String(d.minAmount) : "",
          modules: d.modules,
          validFrom: String(d.validFrom).slice(0, 10),
          validUntil: String(d.validUntil).slice(0, 10),
          totalUses: d.totalUses != null ? String(d.totalUses) : "",
          usesPerCustomer: d.usesPerCustomer != null ? String(d.usesPerCustomer) : "",
          isActive: d.isActive
        });
      })
      .catch((e) => (toast.error(errMsg(e, "Could not open the coupon.")), onClose()));
  }, [id]);

  const title = id == null ? "New coupon" : `Edit coupon · ${detail?.code || ""}`;
  if (!v) return el(Modal, { open: true, title, onClose }, el(Spinner, { center: true }));
  const set = (patch) => setV({ ...v, ...patch });
  const num = (s) => (String(s).trim() === "" ? null : Number(s));
  const pct = Number(v.discountType) === 1;

  /* Checked in the browser first so the admin sees every problem at once; the server checks again. */
  const localErrors = () => {
    const e = {};
    if (!/^[A-Z0-9][A-Z0-9_-]{2,29}$/.test(v.code.trim())) e.code = "Use 3 to 30 letters, digits, - or _ for the code.";
    const val = Number(v.discountValue);
    if (!(val > 0)) e.discountValue = "The discount must be more than zero.";
    else if (pct && val > 100) e.discountValue = "A percentage cannot be more than 100.";
    if (v.modules.length === 0) e.modules = "Choose where the coupon can be used: My Bookings, Passes and/or Membership.";
    if (!v.validFrom) e.validFrom = "Choose the Valid from date.";
    if (!v.validUntil) e.validUntil = "Choose the Valid until date.";
    else if (v.validFrom && v.validUntil < v.validFrom) e.validUntil = "Valid until must be on or after Valid from.";
    for (const k of ["totalUses", "usesPerCustomer"]) if (v[k].trim() !== "" && !(Number.isInteger(Number(v[k])) && Number(v[k]) >= 1)) e[k] = "Leave it empty for no limit, or use 1 or more.";
    if (!e.totalUses && !e.usesPerCustomer && num(v.totalUses) && num(v.usesPerCustomer) > num(v.totalUses)) e.usesPerCustomer = "Uses per customer cannot be more than the total uses.";
    if (v.minAmount.trim() !== "" && Number(v.minAmount) < 0) e.minAmount = "The minimum cannot be negative.";
    if (pct && v.maxDiscountAmount.trim() !== "" && !(Number(v.maxDiscountAmount) > 0)) e.maxDiscountAmount = "Leave the maximum empty, or make it more than zero.";
    return e;
  };

  const save = async () => {
    const le = localErrors();
    setErrs(le);
    if (Object.keys(le).length) {
      toast.error("Check the highlighted fields.");
      return;
    }
    setBusy(true);
    const body = {
      code: v.code.trim(),
      description: v.description.trim() || null,
      discountType: Number(v.discountType),
      discountValue: Number(v.discountValue),
      maxDiscountAmount: pct ? num(v.maxDiscountAmount) : null,
      minAmount: num(v.minAmount),
      modules: v.modules,
      validFrom: v.validFrom || null,
      validUntil: v.validUntil || null,
      totalUses: num(v.totalUses),
      usesPerCustomer: num(v.usesPerCustomer),
      isActive: v.isActive
    };
    try {
      const saved = id == null ? await unwrap(http.post("/admin/coupons", body)) : await unwrap(http.put(`/admin/coupons/${id}`, body));
      toast.success(id == null ? (saved.isActive ? `Coupon ${saved.code} created and live.` : `Coupon ${saved.code} created (inactive).`) : "Coupon saved.");
      onSaved();
    } catch (e) {
      const fe = fieldErrors(e);
      setErrs(fe);
      toast.error(Object.keys(fe).length ? "Check the highlighted fields." : errMsg(e, "Could not save the coupon."));
    } finally {
      setBusy(false);
    }
  };

  const modBox = (m, label) => {
    const on = v.modules.includes(m);
    return el(Checkbox, { key: m, label, checked: on, onChange: () => set({ modules: on ? v.modules.filter((x) => x !== m) : [...v.modules, m] }) });
  };
  return el(
    Modal,
    {
      open: true,
      title,
      onClose,
      footer: el(F, null, el(Button, { variant: "ghost", disabled: busy, onClick: onClose }, "Cancel"), el(Button, { variant: "primary", loading: busy, onClick: save }, id == null ? "Create coupon" : "Save"))
    },
    el(
      "div",
      { className: "oa-form oa-wide", "data-rg": "coupon-editor" },
      el(
        "section",
        null,
        el("h3", null, "Coupon"),
        el(
          "div",
          { className: "oa-grid2" },
          el(Input, {
            label: "Coupon code",
            required: true,
            value: v.code,
            maxLength: 30,
            error: errs.code,
            hint: "3 to 30 letters, digits, - or _. Customers type it at payment.",
            onChange: (e) => set({ code: e.target.value.toUpperCase().replace(/\s/g, "") })
          }),
          el(Input, { label: "Description (optional)", value: v.description, maxLength: 300, error: errs.description, hint: "Shown to customers next to the code.", onChange: (e) => set({ description: e.target.value }) })
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
          el(Input, { label: pct ? "Discount value (%)" : "Discount value (₹)", required: true, type: "number", min: 0, step: "0.01", value: v.discountValue, error: errs.discountValue, onChange: (e) => set({ discountValue: e.target.value }) }),
          pct ? el(Input, { label: "Maximum discount (₹)", type: "number", min: 0, step: "0.01", value: v.maxDiscountAmount, error: errs.maxDiscountAmount, hint: "Optional cap.", onChange: (e) => set({ maxDiscountAmount: e.target.value }) }) : el("div"),
          el(Input, { label: "Minimum booking amount (₹)", type: "number", min: 0, step: "0.01", value: v.minAmount, error: errs.minAmount, hint: "Optional, before GST.", onChange: (e) => set({ minAmount: e.target.value }) })
        ),
        el("div", { className: "oa-muted" }, "The discount comes off the price before GST; GST is charged on what is left. A fixed amount never takes a price below zero.")
      ),
      el(
        "section",
        null,
        el("h3", null, "Services it applies to"),
        errs.modules ? el("div", { className: "oa-err" }, errs.modules) : null,
        modBox(1, "My Bookings (hourly and monthly workspace bookings)"),
        modBox(2, "Passes (day passes)"),
        modBox(3, "Membership (monthly plans, Virtual Office included)")
      ),
      el(
        "section",
        null,
        el("h3", null, "Validity and status"),
        el(
          "div",
          { className: "oa-grid2" },
          el(Input, { label: "Valid from", required: true, type: "date", value: v.validFrom, error: errs.validFrom, onChange: (e) => set({ validFrom: e.target.value }) }),
          el(Input, { label: "Valid until", required: true, type: "date", value: v.validUntil, min: v.validFrom || void 0, error: errs.validUntil, hint: "Last day it can be used (inclusive).", onChange: (e) => set({ validUntil: e.target.value }) })
        ),
        el(Checkbox, { label: "Active (customers can use it while inside its dates)", checked: v.isActive, onChange: (e) => set({ isActive: e.target.checked }) })
      ),
      el(
        "section",
        null,
        el("h3", null, "Usage limit"),
        el(
          "div",
          { className: "oa-grid2" },
          el(Input, { label: "Usage limit (total)", type: "number", min: 1, value: v.totalUses, error: errs.totalUses, hint: "Purchases by everyone together. Empty = no limit.", onChange: (e) => set({ totalUses: e.target.value }) }),
          el(Input, { label: "Uses per customer", type: "number", min: 1, value: v.usesPerCustomer, error: errs.usesPerCustomer, hint: "Empty = no limit.", onChange: (e) => set({ usesPerCustomer: e.target.value }) })
        ),
        el("div", { className: "oa-muted" }, "Coupon codes are never shown to customers. Share the code yourself; it works only when a customer types it at payment.")
      ),
      detail && detail.uses > 0 ? el("div", { className: "oa-muted" }, `Used ${detail.uses} time${detail.uses === 1 ? "" : "s"} so far; the code can no longer change.`) : null
    )
  );
}

/* ---------------------------------------------------------------------------------------- */
/* who used it                                                                                */
/* ---------------------------------------------------------------------------------------- */

function UsesDialog({ coupon, onClose }) {
  const toast = useToast();
  const [rows, setRows] = React.useState(null);
  React.useEffect(() => {
    unwrap(http.get(`/admin/coupons/${coupon.couponId}/redemptions`))
      .then(setRows)
      .catch((e) => (toast.error(errMsg(e, "Could not load the uses.")), setRows([])));
  }, [coupon.couponId]);
  const TONE = { Used: "success", Held: "warning", Released: "neutral" };
  return el(
    Modal,
    { open: true, title: `Uses · ${coupon.code}`, onClose, footer: el(Button, { onClick: onClose }, "Close") },
    el(
      "div",
      { className: "oa-wide" },
      el("p", { className: "oa-muted", style: { marginTop: 0 } }, "Held: a booking awaiting payment or an online order in progress. Released: cancelled, expired or abandoned; the use is given back."),
      !rows
        ? el(Spinner, { center: true })
        : rows.length === 0
          ? el(EmptyState, { title: "Not used yet", description: "Each booking, pass or membership bought with this coupon is listed here." })
          : el(
              "div",
              { className: "oa-scroll" },
              el(
                "table",
                { className: "oa-table" },
                el("thead", null, el("tr", null, ["When", "Customer", "Service", "Reference", "Price before GST", "Discount", "State"].map((h) => el("th", { key: h }, h)))),
                el(
                  "tbody",
                  null,
                  rows.map((r) =>
                    el(
                      "tr",
                      { key: r.redemptionId },
                      el("td", { "data-l": "When" }, when(r.createdUtc)),
                      el("td", { "data-l": "Customer" }, r.customerName),
                      el("td", { "data-l": "Service" }, MOD_NAME[r.module]),
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
