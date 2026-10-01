import {
  a as O,
  r as s,
  A as z,
  j as e,
  p as C,
  R as T,
  I as c,
  F as q,
  z as Loc,
} from "./index-DI5X4c9f.js";
import { i as N } from "./operations-BCRENM62.js";
import { s as B } from "./procurement-D0z-s4Dx.js";
import { B as M } from "./Badge-BU5iAxex.js";
import { C as A } from "./Card-CNXipofE.js";
import { C as CD } from "./ConfirmDialog-Ct1kLH-J.js";
import { M as Y } from "./Modal-BtdVDo1B.js";
import { D as F } from "./DataTable-7lkt-S8A.js";
import { P as G } from "./PageHeader-Y96MMVry.js";
import { u as Q } from "./useFieldErrors-C8qSSg0h.js";
import { c as V, b as $ } from "./formatters-DFe5xCRi.js";
import "./EmptyState-B4UbbVj4.js";
import "./chevron-right-BikENMwC.js";
/* Excel export/import shared by the Assets screens (IX1). */
import {
  u as useExcelExport,
  X as ExportExcelButton,
  M as ImportExcelButton,
  I as ImportExcelDialog,
  R as DateRange,
} from "./InventoryExcel-IX1.js";
const E = 1,
  K = 2,
  /* Stock status thresholds -- see the note in AdminInventoryPages; CRIT_RATIO
   belongs in MST_AppSetting once the API can expose it. */
  CRIT_RATIO = 0.5,
  StOf = (st, ro) =>
    ro > 0 && st <= ro * CRIT_RATIO
      ? "Critical"
      : ro > 0 && st <= ro
        ? "Low Stock"
        : "OK",
  StTone = (v) =>
    v === "Critical" ? "danger" : v === "Low Stock" ? "warning" : "success",
  Eff = (t) =>
    t.transactionType === 2 || t.transactionType === 3
      ? -Math.abs(t.quantity)
      : t.transactionType === 5
        ? t.quantity
        : Math.abs(t.quantity),
  Sb = ({ value: v, onChange: oc, placeholder: ph }) =>
    e.jsxs("div", {
      className: "inv-search",
      children: [
        e.jsxs("svg", {
          className: "inv-search__icon",
          width: 16,
          height: 16,
          viewBox: "0 0 24 24",
          fill: "none",
          stroke: "currentColor",
          strokeWidth: 2,
          strokeLinecap: "round",
          "aria-hidden": !0,
          children: [
            e.jsx("circle", { cx: 11, cy: 11, r: 8 }),
            e.jsx("path", { d: "m21 21-4.3-4.3" }),
          ],
        }),
        e.jsx("input", {
          className: "field__input",
          type: "search",
          value: v,
          placeholder: ph,
          "aria-label": ph,
          onChange: oc,
        }),
      ],
    }),
  KV = (k, v) =>
    e.jsxs(
      "div",
      {
        children: [
          e.jsx("div", { className: "inv-kv__k", children: k }),
          e.jsx("div", { className: "inv-kv__v", children: v ?? "—" }),
        ],
      },
      k,
    ),
  Stat = (icon, val, lab) =>
    e.jsxs("div", {
      className: "inv-stat",
      children: [
        e.jsx("div", { className: "inv-stat__icon", children: icon }),
        e.jsxs("div", {
          children: [
            e.jsx("div", { className: "inv-stat__v", children: val }),
            e.jsx("div", { className: "inv-stat__l", children: lab }),
          ],
        }),
      ],
    }),
  Ibox = e.jsxs("svg", {
    width: 20,
    height: 20,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinecap: "round",
    "aria-hidden": !0,
    children: [
      e.jsx("path", { d: "M21 8v13H3V8" }),
      e.jsx("path", { d: "M1 3h22v5H1zM10 12h4" }),
    ],
  }),
  Irs = e.jsxs("svg", {
    width: 20,
    height: 20,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinecap: "round",
    "aria-hidden": !0,
    children: [
      e.jsx("path", { d: "M6 3h12M6 8h12M6 13h5a5 5 0 0 0 0-10" }),
      e.jsx("path", { d: "m6 13 8 8" }),
    ],
  }),
  Iwarn = e.jsxs("svg", {
    width: 20,
    height: 20,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinecap: "round",
    "aria-hidden": !0,
    children: [
      e.jsx("circle", { cx: 12, cy: 12, r: 10 }),
      e.jsx("path", { d: "M12 8v5M12 16h.01" }),
    ],
  }),
  Idoc = e.jsxs("svg", {
    width: 16,
    height: 16,
    viewBox: "0 0 24 24",
    fill: "none",
    stroke: "currentColor",
    strokeWidth: 2,
    strokeLinecap: "round",
    "aria-hidden": !0,
    children: [
      e.jsx("path", {
        d: "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z",
      }),
      e.jsx("path", { d: "M14 2v6h6" }),
    ],
  });
function ae() {
  return e.jsx(L, {
    transactionType: E,
    title: "Incoming Goods",
    description:
      "Record stock received from suppliers. Each entry adds to the transaction ledger automatically.",
    addLabel: "New Incoming Entry",
    counterpartyLabel: "Supplier / source",
    emptyTitle: "No incoming goods yet",
  });
}
function ie() {
  return e.jsx(L, {
    transactionType: K,
    title: "Outgoing Goods",
    description:
      "Record stock issued or used internally. Each entry deducts from the transaction ledger automatically.",
    addLabel: "New Outgoing Entry",
    counterpartyLabel: "Issued to",
    emptyTitle: "No outgoing goods yet",
  });
}
function L({
  transactionType: d,
  title: u,
  description: v,
  addLabel: x,
  counterpartyLabel: m,
  emptyTitle: a,
}) {
  const h = O(),
    y = T("INVENTORY.CREATE"),
    j = T("INVENTORY.EXPORT"),
    inc = d === E,
    [r, l] = s.useState(null),
    [S, I] = s.useState(!0),
    [w, f] = s.useState(1),
    [o, P] = s.useState(!1),
    [sq, ssq] = s.useState(""),
    [dq, sdq] = s.useState(""),
    [sup, ssup] = s.useState(""),
    [sups, ssups] = s.useState([]),
    [itm, sitm] = s.useState(""),
    [itms, sitms] = s.useState([]),
    [vw, svw] = s.useState(null),
    [rev, srev] = s.useState(null),
    [edt, sedt] = s.useState(null),
    [stk, sstk] = s.useState({}),
    [fr, sfr] = s.useState(""),
    [tt, stt] = s.useState(""),
    [imp, simp] = s.useState(!1),
    [xb, xrun] = useExcelExport(
      inc ? "/inventory/incoming/export" : "/inventory/outgoing/export",
      inc ? "incoming_goods" : "outgoing_goods",
    );
  s.useEffect(() => {
    const id = setTimeout(() => {
      (sdq(sq), f(1));
    }, 350);
    return () => clearTimeout(id);
  }, [sq]);
  s.useEffect(() => {
    N.items({ page: 1, pageSize: 200 })
      .then((t) => sitms((t.items || []).filter((t2) => t2.itemType !== 2)))
      .catch(() => {});
    inc &&
      B.list({ page: 1, pageSize: 100 })
        .then((t) => ssups(t.items || []))
        .catch(() => {});
  }, [inc]);
  const R = {
      page: w,
      pageSize: 20,
      transactionType: d,
      search: dq || void 0,
      supplierId: inc && sup ? Number(sup) : void 0,
      inventoryItemId: !inc && itm ? Number(itm) : void 0,
      from: fr || void 0,
      to: tt || void 0,
    },
    /* Export uses exactly the on-screen filters; the server returns every matching row. */
    xl = () =>
      xrun({
        search: sq.trim() || void 0,
        supplierId: R.supplierId,
        inventoryItemId: R.inventoryItemId,
        from: R.from,
        to: R.to,
      }),
    k = s.useCallback(async () => {
      I(!0);
      try {
        /* Incoming shows each item's latest balance (opening + in + adjustments - out - damage), read from the stock view so it matches Current Stock. */ const [
          tx,
          st,
        ] = await Promise.all([
          N.transactions(R),
          inc ? N.stock().catch(() => null) : null,
        ]);
        if (st) {
          const m2 = {};
          for (const r2 of st) m2[r2.inventoryItemId] = r2;
          sstk(m2);
        }
        l(tx);
      } catch (t) {
        h.error(t instanceof z ? t.message : "Failed to load the ledger.");
      } finally {
        I(!1);
      }
    }, [w, d, dq, sup, itm, fr, tt]);
  s.useEffect(() => {
    k();
  }, [k]);
  const acts = (t) =>
      e.jsxs("div", {
        className: "row-actions",
        children: [
          e.jsx(C, {
            size: "sm",
            variant: "ghost",
            onClick: () => svw(t),
            children: "View",
          }),
          y &&
            e.jsx(C, {
              size: "sm",
              variant: "outline",
              onClick: () => sedt(t),
              children: "Edit",
            }),
          y &&
            e.jsx(C, {
              size: "sm",
              variant: "danger-outline",
              onClick: () => srev(t),
              children: "Delete",
            }),
        ],
      }),
    qty = (t) => {
      const g2 = Eff(t);
      return e.jsxs("span", {
        className: g2 < 0 ? "inv-neg" : "inv-pos",
        children: [g2 > 0 ? "+" : "", g2],
      });
    },
    base = [
      {
        key: "date",
        header: "Date",
        render: (t) =>
          e.jsxs("div", {
            children: [
              e.jsx("div", {
                style: { fontWeight: 600 },
                children: V(t.transactionDate),
              }),
              e.jsx("div", {
                style: {
                  color: "var(--text-muted)",
                  fontSize: "var(--font-size-xs)",
                },
                children: t.transactionNumber,
              }),
            ],
          }),
      },
      {
        key: "item",
        header: "Item",
        render: (t) =>
          e.jsxs("div", {
            children: [
              e.jsx("div", { children: t.itemName }),
              e.jsx("div", {
                style: {
                  color: "var(--text-muted)",
                  fontSize: "var(--font-size-xs)",
                },
                children: t.itemCode,
              }),
            ],
          }),
      },
      { key: "qty", header: "Quantity", render: qty },
    ],
    n = inc
      ? [
          ...base,
          {
            key: "current",
            header: "Current stock",
            render: (t) => {
              const b2 = stk[t.inventoryItemId];
              return b2 ? e.jsx("strong", { children: b2.currentStock }) : "—";
            },
          },
          {
            key: "status",
            header: "Status",
            render: (t) => {
              const b2 = stk[t.inventoryItemId];
              if (!b2) return "—";
              const v2 = StOf(b2.currentStock, b2.reorderLevel);
              return e.jsx(M, { tone: StTone(v2), children: v2 });
            },
          },
          {
            key: "sup",
            header: "Supplier",
            render: (t) => t.supplierName ?? t.sourceOrDestination ?? "—",
          },
          {
            key: "po",
            header: "PO reference",
            render: (t) =>
              e.jsxs("div", {
                children: [
                  e.jsx("div", { children: t.poNumber ?? "—" }),
                  e.jsx("div", {
                    style: {
                      color: "var(--text-muted)",
                      fontSize: "var(--font-size-xs)",
                    },
                    children: t.purchaseInvoiceNumber ?? "",
                  }),
                ],
              }),
          },
          {
            key: "rate",
            header: "Rate",
            render: (t) => (t.unitCost != null ? $(t.unitCost) : "—"),
          },
          {
            key: "total",
            header: "Total",
            render: (t) =>
              t.unitCost != null
                ? e.jsx("strong", {
                    children: $(Math.abs(t.quantity) * t.unitCost),
                  })
                : "—",
          },
          { key: "by", header: "Received by", render: (t) => t.enteredByName },
          {
            key: "doc",
            header: "",
            render: (t) =>
              t.attachmentPath
                ? e.jsx("span", {
                    title:
                      "An attachment is recorded on this entry. There is no download route for inventory attachments yet.",
                    style: { color: "var(--text-muted)" },
                    children: Idoc,
                  })
                : null,
          },
          { key: "actions", header: "Actions", render: acts },
        ]
      : [
          ...base,
          {
            key: "purpose",
            header: "Issued to / purpose",
            render: (t) => t.sourceOrDestination ?? "—",
          },
          { key: "remarks", header: "Remarks", render: (t) => t.notes ?? "—" },
          { key: "by", header: "Issued by", render: (t) => t.enteredByName },
          { key: "actions", header: "Actions", render: acts },
        ];
  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(G, {
        title: u,
        description: v,
        actions: e.jsxs("div", {
          className: "row-actions",
          children: [
            j && e.jsx(ExportExcelButton, { busy: xb, onClick: xl }),
            y && e.jsx(ImportExcelButton, { onClick: () => simp(!0) }),
            y && e.jsx(C, { onClick: () => P(!0), children: x }),
          ],
        }),
      }),
      e.jsxs(A, {
        children: [
          e.jsxs("div", {
            className: "inv-toolbar",
            children: [
              e.jsx(Sb, {
                value: sq,
                placeholder: inc
                  ? "Search by item, supplier or PO#…"
                  : "Search by item or purpose…",
                onChange: (t) => ssq(t.target.value),
              }),
              inc
                ? e.jsxs("select", {
                    className: "inv-filter",
                    "aria-label": "Supplier",
                    value: sup,
                    onChange: (t) => {
                      (ssup(t.target.value), f(1));
                    },
                    children: [
                      e.jsx(
                        "option",
                        { value: "", children: "All Suppliers" },
                        "all",
                      ),
                      sups.map((t) =>
                        e.jsx(
                          "option",
                          { value: t.supplierId, children: t.supplierName },
                          t.supplierId,
                        ),
                      ),
                    ],
                  })
                : e.jsxs("select", {
                    className: "inv-filter",
                    "aria-label": "Item",
                    value: itm,
                    onChange: (t) => {
                      (sitm(t.target.value), f(1));
                    },
                    children: [
                      e.jsx(
                        "option",
                        { value: "", children: "All Items" },
                        "all",
                      ),
                      itms.map((t) =>
                        e.jsx(
                          "option",
                          { value: t.inventoryItemId, children: t.itemName },
                          t.inventoryItemId,
                        ),
                      ),
                    ],
                  }),
              e.jsx(DateRange, {
                from: fr,
                to: tt,
                onChange: (a2, b2) => {
                  (sfr(a2), stt(b2), f(1));
                },
              }),
            ],
          }),
          e.jsx(F, {
            columns: n,
            result: r,
            loading: S,
            rowKey: (t) => t.inventoryTransactionId,
            onPageChange: f,
            emptyTitle: a,
            emptyDescription:
              "The ledger is append-only — a mistake is corrected with an Adjustment, never an edit.",
          }),
        ],
      }),
      o &&
        e.jsx(W, {
          transactionType: d,
          counterpartyLabel: m,
          onClose: () => P(!1),
          onSaved: () => {
            (P(!1), k());
          },
        }),
      edt &&
        e.jsx(W, {
          transactionType: d,
          counterpartyLabel: m,
          correct: edt,
          onClose: () => sedt(null),
          onSaved: () => {
            (sedt(null), k());
          },
        }),
      vw && e.jsx(GV, { txn: vw, incoming: inc, onClose: () => svw(null) }),
      imp &&
        e.jsx(ImportExcelDialog, {
          kind: inc ? "incoming" : "outgoing",
          title: u,
          canUpdate: !1,
          onClose: () => simp(!1),
          onImported: () => k(),
        }),
      e.jsx(CD, {
        open: rev !== null,
        danger: !0,
        confirmLabel: "Post reversal",
        title: `Delete ${rev?.transactionNumber ?? ""}?`,
        message:
          "The ledger is append-only, so this entry cannot be erased. Confirming posts a compensating Adjustment that cancels its effect on stock; both rows stay on the ledger.",
        onConfirm: async () => {
          if (rev)
            try {
              (await N.createTransaction({
                inventoryItemId: rev.inventoryItemId,
                transactionType: 5,
                quantity: -Eff(rev),
                transactionDate: new Date().toISOString().slice(0, 10),
                referenceNumber: rev.transactionNumber,
                notes: "Reversal of " + rev.transactionNumber,
              }),
                h.success(
                  "Reversal posted. Stock is back to its earlier value.",
                ),
                await k());
            } catch (t) {
              h.error(
                t instanceof z ? t.message : "Could not post the reversal.",
              );
            }
        },
        onClose: () => srev(null),
      }),
    ],
  });
}
function GV({ txn: t, incoming: inc, onClose: h }) {
  return e.jsx(Y, {
    open: !0,
    title: `${inc ? "Incoming" : "Outgoing"} — ${t.transactionNumber}`,
    onClose: h,
    footer: e.jsx(C, { variant: "ghost", onClick: h, children: "Close" }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsxs("div", {
          className: "inv-kv",
          children: [
            KV("Date", V(t.transactionDate)),
            KV("Item", `${t.itemName} (${t.itemCode})`),
            KV("Quantity", Eff(t)),
            inc
              ? KV("Supplier", t.supplierName ?? t.sourceOrDestination)
              : KV("Issued to / purpose", t.sourceOrDestination),
            inc ? KV("PO reference", t.poNumber) : KV("Remarks", t.notes),
            inc ? KV("Supplier invoice", t.purchaseInvoiceNumber) : null,
            inc ? KV("Rate", t.unitCost != null ? $(t.unitCost) : null) : null,
            inc
              ? KV(
                  "Total",
                  t.unitCost != null
                    ? $(Math.abs(t.quantity) * t.unitCost)
                    : null,
                )
              : null,
            KV("Reference", t.referenceNumber),
            KV(inc ? "Received by" : "Issued by", t.enteredByName),
          ],
        }),
        inc &&
          t.notes &&
          e.jsxs("div", {
            children: [
              e.jsx("div", { className: "inv-kv__k", children: "Remarks" }),
              e.jsx("div", { children: t.notes }),
            ],
          }),
        t.attachmentPath &&
          e.jsx("p", {
            style: {
              color: "var(--text-muted)",
              fontSize: "var(--font-size-xs)",
              margin: 0,
            },
            children:
              "An attachment is recorded on this entry, but the API has no inventory attachment download route yet.",
          }),
      ],
    }),
  });
}
function W({
  transactionType: d,
  counterpartyLabel: u,
  onClose: v,
  onSaved: x,
  correct: cr,
}) {
  const m = O(),
    { fieldErrors: a, summary: h, capture: y, clear: j, clearField: r } = Q(),
    l = d === E,
    [S, I] = s.useState([]),
    [w, f] = s.useState([]),
    [b, D] = s.useState(!1),
    [o, P] = s.useState({
      inventoryItemId: cr ? String(cr.inventoryItemId) : "",
      quantity: cr ? String(Math.abs(cr.quantity)) : "",
      transactionDate: cr
        ? String(cr.transactionDate).slice(0, 10)
        : new Date().toISOString().slice(0, 10),
      sourceOrDestination: cr?.sourceOrDestination ?? "",
      supplierId: cr?.supplierId ? String(cr.supplierId) : "",
      referenceNumber: cr?.referenceNumber ?? "",
      purchaseInvoiceNumber: cr?.purchaseInvoiceNumber ?? "",
      unitCost: cr?.unitCost != null ? String(cr.unitCost) : "",
      notes: cr?.notes ?? "",
    });
  s.useEffect(() => {
    (async () => {
      try {
        /* Services hold no stock, so they cannot be received or issued. */ const n =
          await N.items({ page: 1, pageSize: 200 });
        if ((I(n.items.filter((t2) => t2.itemType !== 2)), l)) {
          const t = await B.list({ page: 1, pageSize: 100 });
          f(
            t.items.map((g) => ({
              value: g.supplierId,
              label: g.supplierName,
            })),
          );
        }
      } catch {
        (I([]), f([]));
      }
    })();
  }, [l]);
  const p = S.find((n) => String(n.inventoryItemId) === o.inventoryItemId),
    i = (n, t) => {
      (r(n), P((g) => ({ ...g, [n]: t })));
    },
    R = async () => {
      (D(!0), j());
      const n = {
        inventoryItemId: Number(o.inventoryItemId),
        transactionType: d,
        quantity: Number(o.quantity),
        transactionDate: o.transactionDate || null,
        sourceOrDestination: o.sourceOrDestination || null,
        referenceNumber: o.referenceNumber || null,
        unitCost: o.unitCost ? Number(o.unitCost) : null,
        notes: o.notes || null,
        supplierId: l && o.supplierId ? Number(o.supplierId) : null,
        purchaseInvoiceNumber: (l && o.purchaseInvoiceNumber) || null,
      };
      try {
        /* Correcting an append-only row = reverse it, then post the corrected entry. */
        if (cr)
          await N.createTransaction({
            inventoryItemId: cr.inventoryItemId,
            transactionType: 5,
            quantity: -Eff(cr),
            transactionDate: new Date().toISOString().slice(0, 10),
            referenceNumber: cr.transactionNumber,
            notes: "Reversal of " + cr.transactionNumber + " (corrected)",
          });
        (await N.createTransaction(n),
          m.success(
            cr
              ? "Correction posted: the original was reversed and the new entry recorded."
              : "Movement recorded and stock updated.",
          ),
          x());
      } catch (t) {
        const g = y(t);
        g && m.error(g);
      } finally {
        D(!1);
      }
    },
    k = o.inventoryItemId !== "" && Number(o.quantity) > 0;
  return e.jsx(Y, {
    open: !0,
    title: cr
      ? `Edit — ${cr.transactionNumber}`
      : l
        ? "Record incoming goods"
        : "Record outgoing goods",
    onClose: v,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(C, { variant: "ghost", onClick: v, children: "Cancel" }),
        e.jsx(C, {
          onClick: R,
          loading: b,
          disabled: !k,
          children: cr ? "Post correction" : "Save",
        }),
      ],
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        cr &&
          e.jsx("p", {
            style: {
              color: "var(--text-muted)",
              fontSize: "var(--font-size-sm)",
              margin: 0,
            },
            children:
              "The ledger is append-only, so this does not rewrite the original entry. Saving posts a reversal of it plus the corrected entry below — stock ends up as if you had entered this the first time.",
          }),
        h &&
          e.jsx("div", { className: "form-error", role: "alert", children: h }),
        !l &&
          p &&
          e.jsxs("p", {
            style: { color: "var(--text-muted)" },
            children: [
              "In stock now: ",
              e.jsx("strong", { children: p.currentStock }),
            ],
          }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(q, {
              label: "Item",
              required: !0,
              value: o.inventoryItemId,
              placeholder: "Select an item",
              options: S.map((n) => ({
                value: n.inventoryItemId,
                label: `${n.itemCode} — ${n.itemName}`,
              })),
              error: a.inventoryItemId,
              onChange: (n) => i("inventoryItemId", n.target.value),
            }),
            e.jsx(c, {
              label: "Quantity",
              type: "number",
              min: "0",
              step: "0.001",
              required: !0,
              value: o.quantity,
              hint: p ? p.unit : void 0,
              error: a.quantity,
              onChange: (n) => i("quantity", n.target.value),
            }),
            e.jsx(c, {
              label: "Date",
              type: "date",
              required: !0,
              value: o.transactionDate,
              error: a.transactionDate,
              onChange: (n) => i("transactionDate", n.target.value),
            }),
            l
              ? e.jsx(q, {
                  label: "Supplier",
                  value: o.supplierId,
                  placeholder: "Not from a registered supplier",
                  options: w,
                  error: a.supplierId,
                  onChange: (n) => i("supplierId", n.target.value),
                })
              : e.jsx(c, {
                  label: u,
                  value: o.sourceOrDestination,
                  error: a.sourceOrDestination,
                  onChange: (n) => i("sourceOrDestination", n.target.value),
                }),
            e.jsx(c, {
              label: "Reference",
              value: o.referenceNumber,
              error: a.referenceNumber,
              onChange: (n) => i("referenceNumber", n.target.value),
            }),
            l &&
              e.jsxs(e.Fragment, {
                children: [
                  e.jsx(c, {
                    label: "Supplier invoice no.",
                    value: o.purchaseInvoiceNumber,
                    error: a.purchaseInvoiceNumber,
                    onChange: (n) => i("purchaseInvoiceNumber", n.target.value),
                  }),
                  e.jsx(c, {
                    label: "Rate",
                    type: "number",
                    min: "0",
                    step: "0.01",
                    value: o.unitCost,
                    error: a.unitCost,
                    onChange: (n) => i("unitCost", n.target.value),
                  }),
                ],
              }),
          ],
        }),
        e.jsx(c, {
          label: l ? "Remarks" : "Purpose / remarks",
          value: o.notes,
          error: a.notes,
          onChange: (n) => i("notes", n.target.value),
        }),
      ],
    }),
  });
}
function le() {
  const d = O(),
    j = T("INVENTORY.EXPORT"),
    [u, v] = s.useState(null),
    [x, m] = s.useState(!0),
    [a, h] = s.useState(!1),
    [items, sitems] = s.useState({}),
    [locs, slocs] = s.useState({}),
    [xb, xrun] = useExcelExport("/inventory/stock/export", "current_stock");
  s.useEffect(() => {
    N.items({ page: 1, pageSize: 200 })
      .then((t) => {
        const q2 = {};
        for (const it of t.items || []) q2[it.inventoryItemId] = it;
        sitems(q2);
      })
      .catch(() => {});
    Loc.list({ page: 1, pageSize: 100 })
      .then((t) => {
        const q2 = {};
        for (const lo of t.items || []) q2[lo.locationId] = lo.locationName;
        slocs(q2);
      })
      .catch(() => {});
  }, []);
  const y = s.useCallback(async () => {
    m(!0);
    try {
      v(await N.stock(void 0, a || void 0));
    } catch (r) {
      d.error(r instanceof z ? r.message : "Failed to load stock.");
    } finally {
      m(!1);
    }
  }, [a]);
  s.useEffect(() => {
    y();
  }, [y]);
  /* Unit cost and location are not on InventoryStockDto -- merged in from the item
   master. Stock value is derived, never stored. */
  const rows = (u || []).map((r) => {
      const it = items[r.inventoryItemId];
      return {
        ...r,
        _cost: it ? it.unitCost : null,
        _loc: it ? locs[it.locationId] : void 0,
        _val: it && it.unitCost != null ? r.currentStock * it.unitCost : null,
      };
    }),
    totVal = rows.reduce((t, r) => t + (r._val || 0), 0),
    lowN = rows.filter(
      (r) => StOf(r.currentStock, r.reorderLevel) !== "OK",
    ).length,
    j2 = [
      {
        key: "item",
        header: "Item",
        render: (r) =>
          e.jsxs("div", {
            children: [
              e.jsx("div", {
                style: { fontWeight: 600 },
                children: r.itemName,
              }),
              e.jsx("div", {
                style: {
                  color: "var(--text-muted)",
                  fontSize: "var(--font-size-xs)",
                },
                children: r.itemCode,
              }),
            ],
          }),
      },
      {
        key: "loc",
        header: "Location",
        render: (r) =>
          e.jsxs("div", {
            children: [
              e.jsx("div", { children: r._loc ?? "—" }),
              e.jsx("div", {
                style: {
                  color: "var(--text-muted)",
                  fontSize: "var(--font-size-xs)",
                },
                children: r.category ?? "",
              }),
            ],
          }),
      },
      {
        key: "current",
        header: "Current stock",
        render: (r) => e.jsx("strong", { children: r.currentStock }),
      },
      {
        key: "status",
        header: "Status",
        render: (r) => {
          const v2 = StOf(r.currentStock, r.reorderLevel);
          return e.jsx(M, { tone: StTone(v2), children: v2 });
        },
      },
      {
        key: "cost",
        header: "Unit cost",
        render: (r) => (r._cost != null ? $(r._cost) : "—"),
      },
      {
        key: "val",
        header: "Total cost",
        render: (r) =>
          r._val != null ? e.jsx("strong", { children: $(r._val) }) : "—",
      },
    ];
  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(G, {
        title: "Current Stock",
        description:
          "Live snapshot calculated from opening + in + adjustments − out − damage. Read-only — this view can never be edited directly.",
        actions: e.jsxs("div", {
          className: "row-actions",
          children: [
            e.jsx(C, {
              variant: a ? "primary" : "outline",
              onClick: () => h((r) => !r),
              children: a ? "Showing low stock" : "Show low stock only",
            }),
            j &&
              e.jsx(ExportExcelButton, {
                busy: xb,
                onClick: () => xrun({ belowReorder: a || void 0 }),
              }),
          ],
        }),
      }),
      e.jsxs("div", {
        className: "inv-stats",
        children: [
          Stat(Ibox, rows.length, "Total Items Tracked"),
          Stat(Irs, $(totVal), "Total Stock Value"),
          Stat(Iwarn, lowN, "Low Stock Items"),
        ],
      }),
      e.jsx(A, {
        children: e.jsx(F, {
          columns: j2,
          result: u
            ? {
                items: rows,
                page: 1,
                pageSize: rows.length || 1,
                totalCount: rows.length,
                totalPages: 1,
                hasNext: !1,
                hasPrevious: !1,
              }
            : null,
          loading: x,
          rowKey: (r) => r.inventoryItemId,
          onPageChange: () => {},
          emptyTitle: a
            ? "Nothing is below its reorder level"
            : "No stock items",
        }),
      }),
    ],
  });
}
export {
  le as AdminCurrentStockPage,
  ae as AdminIncomingGoodsPage,
  ie as AdminOutgoingGoodsPage,
};
