/**
 * Admin -> Assets -> Incoming Goods (/admin/assets/incoming) and Outgoing Goods
 * (/admin/assets/outgoing) — Assets spec §6 and §7.
 *
 * Incoming is the only way stock goes up and the only way an asset record is born: a
 * draft changes nothing, posting moves stock and creates the assets with their codes,
 * and a posted receipt is reversed, never edited. Outgoing is the only way stock goes
 * down and the only way an asset changes room or status; the reason chosen decides
 * the rest of the form. Nothing on either page is ever deleted (a draft excepted).
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js lazy-loads this
 * file for both routes. Shared pieces live in AssetsCommon-AS1.js.
 */
import { a as useToast, r as React, R as usePermission, p as Button, F as Select, I as Input, G as Spinner } from "./index-DI5X4c9f.js";
import {
  h, Frag, api, money, qty, fmtDay, fmtWhen, todayIst, daysAgoIst, errorText, isCostMissing, useDebounced, useAssetStyles,
  REASON, RECEIPT_STATUS, STATUS, CONDITION, KindTag, Pill, SkeletonRows, LoadFailed, Dialog, ReasonDialog, LabelSheetDialog, ItemPicker,
  IconSearch, IconX, IconChevronLeft, IconChevronRight
} from "./AssetsCommon-AS1.js";

const PERIODS = [{ value: "30", label: "Last 30 days" }, { value: "90", label: "Last 90 days" }, { value: "365", label: "Last 12 months" }, { value: "all", label: "All time" }];
const fromFor = (period) => (period === "all" ? undefined : daysAgoIst(Number(period)));
const roomOptions = (options) => (options?.locations ?? []).map((l) => ({ value: String(l.assetLocationId), label: l.locationName + (l.isStore ? " (store)" : "") }));
const numberOrNull = (v) => (v === "" || v == null ? null : Number(v));
let lineKey = 0;

function Pager({ result, loading, onPage }) {
  if (!result || result.totalCount === 0) return null;
  return h("div", { className: "as-footer" },
    h("span", null, `Page ${result.page} of ${result.totalPages} · ${result.totalCount} record${result.totalCount === 1 ? "" : "s"}`),
    h("div", { className: "as-pager" },
      h(Button, { variant: "outline", size: "sm", disabled: !result.hasPrevious || loading, onClick: () => onPage(result.page - 1), "aria-label": "Previous page" }, h(IconChevronLeft, { size: 16 })),
      h(Button, { variant: "outline", size: "sm", disabled: !result.hasNext || loading, onClick: () => onPage(result.page + 1), "aria-label": "Next page" }, h(IconChevronRight, { size: 16 }))));
}

/** "Post anyway?" for assets with no purchase cost (§14). */
function ConfirmCost({ message, onCancel, onConfirm, busy }) {
  return h(Dialog, {
    title: "Some assets have no purchase cost", onClose: onCancel,
    footer: h(Frag, null, h(Button, { variant: "ghost", onClick: onCancel }, "Go back"), h(Button, { onClick: onConfirm, loading: busy }, "Post anyway"))
  }, h("div", { className: "as-note as-note--amber" }, message));
}

// ═════════════════════════════════════════════════════════════════════════════
// Incoming Goods
// ═════════════════════════════════════════════════════════════════════════════

export function AssetIncomingPage() {
  useAssetStyles();
  const toast = useToast();
  const can = { receive: usePermission("INVENTORY.RECEIVE"), reverse: usePermission("INVENTORY.REVERSE"), export: usePermission("INVENTORY.EXPORT"), labels: usePermission("INVENTORY.LABELS") };
  const [options, setOptions] = React.useState(null);
  const [searchText, setSearchText] = React.useState("");
  const search = useDebounced(searchText, 350);
  const [period, setPeriod] = React.useState("90");
  const [supplierId, setSupplierId] = React.useState("");
  const [status, setStatus] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [failed, setFailed] = React.useState(false);
  const [side, setSide] = React.useState(null);

  React.useEffect(() => { api.options().then(setOptions).catch(() => {}); }, []);
  const params = { search: search || undefined, from: fromFor(period), supplierId: supplierId || undefined, status: status || undefined };
  const load = React.useCallback(async () => {
    setLoading(true); setFailed(false);
    try { setResult(await api.receipts({ ...params, page, pageSize: 20 })); }
    catch (error) { setFailed(true); toast.error(errorText(error, "Could not load the receipts.")); }
    finally { setLoading(false); }
  }, [search, period, supplierId, status, page]);
  React.useEffect(() => { load(); }, [load]);
  React.useEffect(() => { setPage(1); }, [search, period, supplierId, status]);

  const rows = result?.items ?? [];
  return h("div", { className: "as-page" },
    h("div", { className: "as-head" },
      h("div", null, h("h2", null, "Incoming Goods"),
        h("p", null, "Everything that arrives at Regalis is recorded here first. This is the only way stock goes up, and the only way a new asset gets created with its code and QR label.")),
      h("div", { className: "as-head__actions" },
        can.export ? h(Button, { variant: "outline", onClick: async () => { if (!(await api.exportReceipts(params))) toast.error("No receipts match."); } }, "Export Excel") : null,
        can.receive ? h(Button, { onClick: () => setSide({ kind: "form" }) }, "+ Record incoming") : null)),
    h("div", { className: side ? "as-split" : undefined },
      h("div", { className: "as-panel" },
        h("div", { className: "as-filters" },
          h("div", { className: "as-search" }, h(IconSearch, { size: 16 }),
            h("input", { className: "field__input", type: "search", value: searchText, placeholder: "Search receipt no., supplier, invoice or item…", "aria-label": "Search receipts", onChange: (ev) => setSearchText(ev.target.value) })),
          h(Select, { label: "Period", value: period, options: PERIODS, onChange: (ev) => setPeriod(ev.target.value) }),
          h(Select, { label: "Supplier", value: supplierId, placeholder: "All suppliers", options: (options?.suppliers ?? []).map((s) => ({ value: String(s.id), label: s.name })), onChange: (ev) => setSupplierId(ev.target.value) }),
          h(Select, { label: "Status", value: status, placeholder: "Any status", options: Object.entries(RECEIPT_STATUS).map(([v, s]) => ({ value: v, label: s.label })), onChange: (ev) => setStatus(ev.target.value) })),
        failed ? h(LoadFailed, { onRetry: load })
          : result && rows.length === 0 ? h("div", { className: "as-empty" }, h("strong", null, search || supplierId || status || period !== "all" ? "No receipts match these filters." : "Nothing has been received yet."),
            can.receive ? h("div", null, h(Button, { onClick: () => setSide({ kind: "form" }) }, "+ Record incoming")) : null)
            : h("div", { className: "as-table-wrap" }, h("table", { className: "as-table" },
              h("thead", null, h("tr", null, h("th", null, "Receipt"), h("th", null, "Date"), h("th", null, "Supplier"), h("th", null, "Lines"), h("th", { className: "num" }, "Value"), h("th", null, "Status"))),
              loading && !result ? h(SkeletonRows, { rows: 6, cols: 6 })
                : h("tbody", { style: { opacity: loading ? 0.6 : 1 } }, rows.map((r) => h("tr", {
                  key: r.goodsReceiptId, className: `as-row--click${side?.id === r.goodsReceiptId ? " as-row--on" : ""}`, onClick: () => setSide({ kind: "view", id: r.goodsReceiptId })
                },
                  h("td", null, h("div", { className: "as-cell-main as-mono" }, r.receiptNumber),
                    h("div", { className: "as-cell-sub" }, r.isReversal ? `reverses ${r.reversalNumber}` : r.invoiceNumber ? `Invoice ${r.invoiceNumber}` : r.isOpeningBalance ? "opening balance" : r.poNumber ? `PO ${r.poNumber}` : "no invoice yet")),
                  h("td", null, fmtDay(r.receiptDate)),
                  h("td", null, r.isOpeningBalance ? "—" : r.supplierName ?? "—"),
                  h("td", null, `${r.lineCount} line${r.lineCount === 1 ? "" : "s"}${r.assetCount ? ` · ${r.assetCount} asset${r.assetCount === 1 ? "" : "s"}` : r.consumableLines ? " · consumables" : ""}`),
                  h("td", { className: "num" }, h("strong", { className: r.totalAmount < 0 ? "as-out" : undefined }, money(r.totalAmount))),
                  h("td", null, r.isReversed ? h(Pill, { cls: "reversal" }, `Reversed by ${r.reversalNumber}`) : r.isReversal ? h(Pill, { cls: "reversal" }, "Reversal") : h(Pill, { cls: RECEIPT_STATUS[r.status].cls }, RECEIPT_STATUS[r.status].label))))))),
        h(Pager, { result, loading, onPage: setPage }),
        h("div", { className: "as-footer" }, "A draft changes nothing. Stock moves and asset records are created only when the receipt is posted, and a posted receipt can never be edited — only reversed.")),
      side?.kind === "form" ? h(ReceiptForm, { key: side.draft?.goodsReceiptId ?? "new", options, draft: side.draft, onClose: () => setSide(null), onSaved: (r) => { load(); setSide({ kind: "view", id: r.goodsReceiptId }); } })
        : side?.kind === "view" ? h(ReceiptView, { key: side.id, id: side.id, can, onClose: () => setSide(null), onEdit: (draft) => setSide({ kind: "form", draft }), onChanged: (id) => { load(); setSide(id ? { kind: "view", id } : null); } })
          : null));
}

function ReceiptView({ id, can, onClose, onEdit, onChanged }) {
  const toast = useToast();
  const [r, setR] = React.useState(null);
  const [modal, setModal] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const load = () => api.receipt(id).then(setR).catch((e) => toast.error(errorText(e, "Could not load the receipt.")));
  React.useEffect(() => { load(); }, [id]);
  if (!r) return h("div", { className: "as-panel as-side" }, h("div", { style: { padding: 40 } }, h(Spinner, { center: true })));
  const draft = r.status === 1;
  const reversible = !draft && !r.reversedByReceiptId && !r.reversesReceiptId;
  const assets = r.lines.filter((l) => l.kind === 2 && l.firstCode).reduce((n, l) => n + l.quantity, 0);
  const post = async (confirm) => {
    setBusy(true);
    try { const posted = await api.postReceipt(r.goodsReceiptId, confirm); toast.success(`${posted.receiptNumber} posted.`); setModal(null); onChanged(posted.goodsReceiptId); load(); }
    catch (error) { if (isCostMissing(error)) setModal({ kind: "cost", message: errorText(error, "") }); else toast.error(errorText(error, "Could not post the receipt.")); }
    finally { setBusy(false); }
  };
  return h("div", { className: "as-panel as-side" },
    h("div", { className: "as-side__head" },
      h("div", null, h("h3", { className: "as-mono" }, r.receiptNumber),
        h("div", { className: "as-sub" }, [fmtDay(r.receiptDate), r.isOpeningBalance ? "Opening balance" : r.supplierName, r.invoiceNumber ? `Invoice ${r.invoiceNumber}` : null].filter(Boolean).join(" · "))),
      h("button", { type: "button", className: "as-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 20 }))),
    h("div", { className: "as-side__body" },
      h("div", null, r.reversedByReceiptId ? h(Pill, { cls: "reversal" }, `Reversed by ${r.reversedByNumber}`) : r.reversesReceiptId ? h(Pill, { cls: "reversal" }, `Reverses ${r.reversesNumber}`) : h(Pill, { cls: RECEIPT_STATUS[r.status].cls }, RECEIPT_STATUS[r.status].label),
        r.poNumber ? h("span", { className: "as-sub" }, ` · against ${r.poNumber}`) : null,
        r.intoLocationName ? h("span", { className: "as-sub" }, ` · into ${r.intoLocationName}`) : null),
      draft ? h("div", { className: "as-note as-note--amber" }, "A draft. Nothing has changed yet: no stock has moved and no asset exists.") : null,
      r.lines.map((l) => h("div", { className: "as-line", key: l.goodsReceiptLineId },
        h("div", { className: "as-line__head" },
          h("div", null, h("div", { className: "as-cell-main" }, l.itemName), h("div", { className: "as-cell-sub" }, h("span", { className: "as-mono" }, l.kind === 2 ? l.codePrefix : l.itemCode), " · ", l.kind === 2 ? "Asset" : "Consumable", l.locationName ? ` · ${l.locationName}` : "")),
          h("strong", { className: l.amount < 0 ? "as-out" : undefined }, money(l.amount))),
        h("div", { className: "as-sub" }, `${qty(l.quantity)} ${l.unit} × ${l.rate == null ? "no cost" : money(l.rate, { paise: l.kind === 1 })}`),
        l.firstCode ? h("div", { className: "as-line__says as-line__says--asset as-mono" }, l.firstCode === l.lastCode ? l.firstCode : `${l.firstCode} to ${l.lastCode.slice(l.lastCode.lastIndexOf("-"))}`) : null)),
      h("div", { className: "as-total" }, h("span", null, "Total"), h("strong", null, money(r.totalAmount))),
      r.notes ? h("div", { className: "as-sub" }, r.notes) : null,
      r.postedUtc ? h("div", { className: "as-sub" }, `Posted ${fmtWhen(r.postedUtc)}${r.postedByName ? ` by ${r.postedByName}` : ""}`) : null),
    h("div", { className: "as-side__foot" },
      draft && can.receive ? h(Button, { variant: "ghost", onClick: async () => {
        if (!window.confirm(`Delete draft ${r.receiptNumber}? It changed nothing.`)) return;
        try { await api.deleteReceipt(r.goodsReceiptId); toast.success("Draft deleted."); onChanged(null); } catch (error) { toast.error(errorText(error, "Could not delete the draft.")); }
      } }, "Delete draft") : null,
      draft && can.receive ? h(Button, { variant: "outline", onClick: () => onEdit(r) }, "Edit") : null,
      draft && can.receive ? h(Button, { onClick: () => post(false), loading: busy }, "Post receipt") : null,
      !draft && assets > 0 && can.labels && !r.reversedByReceiptId && !r.reversesReceiptId ? h(Button, { variant: "outline", onClick: () => setModal({ kind: "labels" }) }, `Print ${assets} QR label${assets === 1 ? "" : "s"}`) : null,
      reversible && can.reverse ? h(Button, { variant: "danger", onClick: () => setModal({ kind: "reverse" }) }, "Reverse") : null),
    modal?.kind === "cost" ? h(ConfirmCost, { message: modal.message, busy, onCancel: () => setModal(null), onConfirm: () => post(true) }) : null,
    modal?.kind === "labels" ? h(LabelSheetDialog, { params: { receiptId: r.goodsReceiptId }, title: `QR labels — ${r.receiptNumber}`, onClose: () => setModal(null) }) : null,
    modal?.kind === "reverse" ? h(ReasonDialog, {
      title: `Reverse ${r.receiptNumber}`, confirm: "Write the reversing receipt",
      intro: "This writes the opposite receipt and keeps both in the record. Consumables go back out — never below zero. Assets it created leave the register and their codes stay retired; it is refused if any of them has moved since.",
      onClose: () => setModal(null),
      onSubmit: async (reason) => { const rev = await api.reverseReceipt(r.goodsReceiptId, reason); toast.success(`Reversed by ${rev.receiptNumber}.`); setModal(null); onChanged(rev.goodsReceiptId); }
    }) : null);
}

/** One line per thing that arrived (§6.2). Under each, in plain words, what posting
 *  will do — worked out by the server before anything is saved (§6.3). */
function ReceiptForm({ options, draft, onClose, onSaved }) {
  const toast = useToast();
  const fromDraft = (l) => ({
    key: ++lineKey, kind: l.kind, item: l.inventoryItemId ? { inventoryItemId: l.inventoryItemId, itemName: l.itemName, itemCode: l.itemCode ?? l.codePrefix, kind: l.kind, unit: l.unit } : null,
    quantity: String(l.quantity), rate: l.rate == null ? "" : String(l.rate), poLineId: l.purchaseOrderLineId,
    asset: l.kind === 2 ? { itemName: l.itemName, categoryId: l.assetCategoryId ? String(l.assetCategoryId) : "", codePrefix: l.codePrefix ?? "", brandModel: l.brandModel ?? "", locationId: l.locationId ? String(l.locationId) : "", condition: l.condition ? String(l.condition) : "1" } : null
  });
  const [f, setF] = React.useState(() => ({
    date: draft?.receiptDate ?? todayIst(),
    supplier: draft ? (draft.isOpeningBalance ? "opening" : draft.supplierId ? String(draft.supplierId) : "") : "",
    invoiceNumber: draft?.invoiceNumber ?? "", poId: draft?.purchaseOrderId ? String(draft.purchaseOrderId) : "",
    into: draft?.intoLocationId ? String(draft.intoLocationId) : "", notes: draft?.notes ?? ""
  }));
  const [lines, setLines] = React.useState(() => (draft ? draft.lines.map(fromDraft) : []));
  const [orders, setOrders] = React.useState([]);
  const [preview, setPreview] = React.useState(null);
  const [adding, setAdding] = React.useState(lines.length === 0);
  const [busy, setBusy] = React.useState(false);
  const [confirm, setConfirm] = React.useState(null);
  const set = (k) => (ev) => setF((s) => ({ ...s, [k]: ev.target.value }));
  const opening = f.supplier === "opening";

  React.useEffect(() => {
    if (!f.supplier || opening) { setOrders([]); return; }
    api.openPurchaseOrders(Number(f.supplier)).then(setOrders).catch(() => setOrders([]));
  }, [f.supplier]);

  const body = (post, confirmMissingCost = false) => ({
    receiptDate: f.date, isOpeningBalance: opening, supplierId: opening ? null : numberOrNull(f.supplier), invoiceNumber: f.invoiceNumber || null,
    purchaseOrderId: opening ? null : numberOrNull(f.poId), intoLocationId: numberOrNull(f.into), notes: f.notes || null, post, confirmMissingCost,
    lines: lines.map((l) => l.kind === 1
      ? { kind: 1, inventoryItemId: l.item?.inventoryItemId ?? null, quantity: Number(l.quantity) || 0, rate: numberOrNull(l.rate), purchaseOrderLineId: l.poLineId ?? null }
      : { kind: 2, inventoryItemId: l.item?.inventoryItemId ?? null, quantity: Number(l.quantity) || 0, rate: numberOrNull(l.rate), purchaseOrderLineId: l.poLineId ?? null,
        itemName: l.asset?.itemName || null, assetCategoryId: numberOrNull(l.asset?.categoryId), codePrefix: l.asset?.codePrefix || null, brandModel: l.asset?.brandModel || null,
        locationId: numberOrNull(l.asset?.locationId), condition: numberOrNull(l.asset?.condition) })
  });
  const signature = JSON.stringify(body(false));
  const debounced = useDebounced(signature, 400);
  React.useEffect(() => {
    if (lines.length === 0) { setPreview(null); return; }
    let live = true;
    api.previewReceipt(JSON.parse(debounced)).then((p) => live && setPreview(p)).catch(() => live && setPreview(null));
    return () => { live = false; };
  }, [debounced]);

  const update = (key, patch) => setLines((ls) => ls.map((l) => (l.key === key ? { ...l, ...patch } : l)));
  const addItem = (row) => {
    setAdding(false);
    if (row.kind === 2) {
      setLines((ls) => [...ls, { key: ++lineKey, kind: 2, item: row, quantity: "1", rate: row.unitCost == null ? "" : String(row.unitCost),
        asset: { itemName: row.itemName, categoryId: row.categoryId ? String(row.categoryId) : "", codePrefix: "", brandModel: row.brandModel ?? "", locationId: "", condition: "1" } }]);
    } else {
      setLines((ls) => [...ls, { key: ++lineKey, kind: 1, item: row, quantity: "", rate: row.unitCost == null ? "" : String(row.unitCost) }]);
    }
  };
  const addNewAsset = () => { setAdding(false); setLines((ls) => [...ls, { key: ++lineKey, kind: 2, item: null, quantity: "1", rate: "", asset: { itemName: "", categoryId: "", codePrefix: "", brandModel: "", locationId: "", condition: "1" } }]); };
  const fillFromPo = (poId) => {
    setF((s) => ({ ...s, poId }));
    const po = orders.find((o) => String(o.purchaseOrderId) === poId);
    if (!po) return;
    setLines(po.lines.map((pl) => pl.kind === 2
      ? { key: ++lineKey, kind: 2, item: { inventoryItemId: pl.inventoryItemId, itemName: pl.itemName, itemCode: pl.itemCode, kind: 2 }, quantity: String(pl.pending), rate: String(pl.rate), poLineId: pl.purchaseOrderLineId,
        asset: { itemName: pl.itemName, categoryId: "", codePrefix: "", brandModel: "", locationId: "", condition: "1" } }
      : pl.inventoryItemId
        ? { key: ++lineKey, kind: 1, item: { inventoryItemId: pl.inventoryItemId, itemName: pl.itemName, itemCode: pl.itemCode, kind: 1, unit: pl.unit }, quantity: String(pl.pending), rate: String(pl.rate), poLineId: pl.purchaseOrderLineId }
        : { key: ++lineKey, kind: 2, item: null, quantity: String(pl.pending), rate: String(pl.rate), poLineId: pl.purchaseOrderLineId,
          asset: { itemName: pl.itemName, categoryId: "", codePrefix: "", brandModel: "", locationId: "", condition: "1" } }));
    setAdding(false);
  };

  const save = async (post, confirmMissingCost = false) => {
    if (!opening && !f.supplier) { toast.error("Choose the supplier, or Opening balance."); return; }
    if (!f.into) { toast.error("Choose where it went."); return; }
    if (lines.length === 0) { toast.error("Add at least one line: what arrived."); return; }
    setBusy(true);
    try {
      const b = body(post, confirmMissingCost);
      const saved = draft ? await api.updateReceipt(draft.goodsReceiptId, b) : await api.createReceipt(b);
      toast.success(post ? `${saved.receiptNumber} posted.` : `${saved.receiptNumber} saved as a draft. Nothing has changed yet.`);
      setConfirm(null);
      onSaved(saved);
    } catch (error) {
      if (isCostMissing(error)) setConfirm(errorText(error, ""));
      else toast.error(errorText(error, "Could not save the receipt."));
    } finally { setBusy(false); }
  };

  const categories = options?.categories ?? [];
  return h("div", { className: "as-panel as-side" },
    h("div", { className: "as-side__head" }, h("h3", null, draft ? `Edit ${draft.receiptNumber}` : "Record incoming"),
      h("button", { type: "button", className: "as-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 20 }))),
    h("div", { className: "as-side__body" },
      h("div", { className: "as-form" },
        h(Input, { label: "Date", required: true, type: "date", max: todayIst(), value: f.date, onChange: set("date"), hint: "The day the goods arrived." }),
        h(Select, { label: "Supplier", required: true, value: f.supplier, placeholder: "Choose",
          options: [{ value: "opening", label: "Opening balance (no supplier)" }, ...(options?.suppliers ?? []).map((s) => ({ value: String(s.id), label: s.name }))],
          onChange: (ev) => setF((s) => ({ ...s, supplier: ev.target.value, poId: "" })) }),
        h(Input, { label: "Invoice number", value: f.invoiceNumber, onChange: set("invoiceNumber"), hint: "Optional — goods often arrive first." }),
        h(Select, { label: "Against purchase order", value: f.poId, disabled: opening || !f.supplier, placeholder: orders.length ? "None" : "No open order", options: orders.map((o) => ({ value: String(o.purchaseOrderId), label: `${o.poNumber} · ${fmtDay(o.poDate)}` })), onChange: (ev) => fillFromPo(ev.target.value) }),
        h("div", { className: "as-span2" }, h(Select, { label: "Receive into", required: true, value: f.into, placeholder: "Where it physically went", options: roomOptions(options), onChange: set("into") }))),
      h("div", { className: "as-kv__k", style: { marginTop: "var(--space-2)" } }, "What arrived"),
      lines.map((l, index) => {
        const p = preview?.lines?.find((x) => x.lineIndex === index);
        const amount = (Number(l.quantity) || 0) * (Number(l.rate) || 0);
        const says = p ? (p.error ? "bad" : l.kind === 2 ? (p.costMissing ? "warn" : "asset") : "stock") : null;
        return h("div", { className: "as-line", key: l.key },
          h("div", { className: "as-line__head" },
            h("div", null,
              h("div", { className: "as-cell-main" }, l.kind === 2 ? (l.asset?.itemName || "New asset") : l.item?.itemName ?? "Choose the item"),
              h("div", { className: "as-cell-sub" }, l.item ? h("span", { className: "as-mono" }, l.item.itemCode) : null, l.item ? " · " : "", l.kind === 2 ? (l.item ? "More of this asset" : "A new asset") : "Consumable")),
            h("button", { type: "button", className: "as-remove", onClick: () => setLines((ls) => ls.filter((x) => x.key !== l.key)) }, "Remove")),
          l.kind === 1 && !l.item ? h(ItemPicker, { label: "Item", kind: 1, onPick: (row) => update(l.key, { item: row, rate: l.rate || (row.unitCost == null ? "" : String(row.unitCost)) }) }) : null,
          l.kind === 2 ? h("div", { className: "as-form", style: { marginTop: 8 } },
            h(Input, { label: "Item name", value: l.asset.itemName, onChange: (ev) => update(l.key, { asset: { ...l.asset, itemName: ev.target.value } }) }),
            h(Select, { label: "Category", value: l.asset.categoryId, placeholder: l.item ? "Same as before" : "Choose", options: categories.map((c) => ({ value: String(c.assetCategoryId), label: c.categoryName })),
              onChange: (ev) => { const cat = categories.find((c) => String(c.assetCategoryId) === ev.target.value); update(l.key, { asset: { ...l.asset, categoryId: ev.target.value, codePrefix: l.asset.codePrefix || (!l.item && cat ? cat.codePrefix : "") } }); } }),
            h(Input, { label: "Code prefix", value: l.asset.codePrefix, placeholder: l.item ? "Same as before" : "R-2W-E-EGC", onChange: (ev) => update(l.key, { asset: { ...l.asset, codePrefix: ev.target.value } }) }),
            h(Select, { label: "Room", value: l.asset.locationId, placeholder: "Where it was received into", options: roomOptions(options), onChange: (ev) => update(l.key, { asset: { ...l.asset, locationId: ev.target.value } }) })) : null,
          h("div", { className: "as-line__grid" },
            h(Input, { label: l.kind === 1 && l.item?.unit ? `Qty (${l.item.unit})` : "Qty", type: "number", min: 0, step: l.kind === 2 ? 1 : "0.001", value: l.quantity, onChange: (ev) => update(l.key, { quantity: ev.target.value }) }),
            h(Input, { label: "Rate each (₹)", type: "number", min: 0, step: "0.01", value: l.rate, onChange: (ev) => update(l.key, { rate: ev.target.value }) }),
            h("div", null, h("div", { className: "as-kv__k" }, "Amount"), h("div", { className: "as-line__amount" }, money(amount)))),
          p ? h("div", { className: `as-line__says as-line__says--${says}` }, p.message, p.costMissing && !p.error ? " — no purchase cost: they will show a dash for book value." : "") : null);
      }),
      adding ? h("div", { className: "as-line" },
        h(ItemPicker, { label: "Add a line", placeholder: "Type a consumable or an asset to receive more of", onPick: addItem }),
        h("div", { style: { display: "flex", gap: 12, marginTop: 8 } },
          h("button", { type: "button", className: "as-link", onClick: addNewAsset }, "+ A new kind of asset"),
          lines.length ? h("button", { type: "button", className: "as-link", onClick: () => setAdding(false) }, "Cancel") : null))
        : h("button", { type: "button", className: "as-link", style: { alignSelf: "flex-start" }, onClick: () => setAdding(true) }, "+ Add another line"),
      preview?.missingCostWarning ? h("div", { className: "as-note as-note--amber" }, preview.missingCostWarning) : null,
      h("div", { className: "as-total" }, h("span", null, "Total"), h("strong", null, money(preview?.total ?? lines.reduce((s, l) => s + (Number(l.quantity) || 0) * (Number(l.rate) || 0), 0)))),
      h(Input, { label: "Notes", value: f.notes, onChange: set("notes") })),
    h("div", { className: "as-side__foot" },
      h(Button, { variant: "ghost", onClick: () => save(false), disabled: busy }, "Save as draft"),
      h(Button, { onClick: () => save(true), loading: busy }, "Post receipt")),
    confirm ? h(ConfirmCost, { message: confirm, busy, onCancel: () => setConfirm(null), onConfirm: () => save(true, true) }) : null);
}

// ═════════════════════════════════════════════════════════════════════════════
// Outgoing Goods
// ═════════════════════════════════════════════════════════════════════════════

export function AssetOutgoingPage() {
  useAssetStyles();
  const toast = useToast();
  const can = { issue: usePermission("INVENTORY.ISSUE"), reverse: usePermission("INVENTORY.REVERSE"), export: usePermission("INVENTORY.EXPORT") };
  const [options, setOptions] = React.useState(null);
  const [searchText, setSearchText] = React.useState("");
  const search = useDebounced(searchText, 350);
  const [reason, setReason] = React.useState("");
  const [period, setPeriod] = React.useState("90");
  const [page, setPage] = React.useState(1);
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [failed, setFailed] = React.useState(false);
  const [side, setSide] = React.useState(null);

  React.useEffect(() => { api.options().then(setOptions).catch(() => {}); }, []);
  const params = { search: search || undefined, reason: reason || undefined, from: fromFor(period) };
  const load = React.useCallback(async () => {
    setLoading(true); setFailed(false);
    try { setResult(await api.issues({ ...params, page, pageSize: 20 })); }
    catch (error) { setFailed(true); toast.error(errorText(error, "Could not load the movements.")); }
    finally { setLoading(false); }
  }, [search, reason, period, page]);
  React.useEffect(() => { load(); }, [load]);
  React.useEffect(() => { setPage(1); }, [search, reason, period]);

  const rows = result?.items ?? [];
  return h("div", { className: "as-page" },
    h("div", { className: "as-head" },
      h("div", null, h("h2", null, "Outgoing Goods"),
        h("p", null, "Anything that is used up, moved, sent for repair, returned to the supplier or thrown away. This is the only way stock goes down and the only way an asset changes room or status.")),
      h("div", { className: "as-head__actions" },
        can.export ? h(Button, { variant: "outline", onClick: async () => { if (!(await api.exportIssues(params))) toast.error("No movements match."); } }, "Export Excel") : null,
        can.issue ? h(Button, { onClick: () => setSide({ kind: "form" }) }, "+ Record outgoing") : null)),
    h("div", { className: side ? "as-split" : undefined },
      h("div", { className: "as-panel" },
        h("div", { className: "as-filters" },
          h("div", { className: "as-search" }, h(IconSearch, { size: 16 }),
            h("input", { className: "field__input", type: "search", value: searchText, placeholder: "Search note no., item or person…", "aria-label": "Search movements", onChange: (ev) => setSearchText(ev.target.value) })),
          h(Select, { label: "Reason", value: reason, placeholder: "All reasons", options: Object.entries(REASON).map(([v, r]) => ({ value: v, label: r.label })), onChange: (ev) => setReason(ev.target.value) }),
          h(Select, { label: "Period", value: period, options: PERIODS, onChange: (ev) => setPeriod(ev.target.value) })),
        failed ? h(LoadFailed, { onRetry: load })
          : result && rows.length === 0 ? h("div", { className: "as-empty" }, h("strong", null, "No movements match these filters."))
            : h("div", { className: "as-table-wrap" }, h("table", { className: "as-table" },
              h("thead", null, h("tr", null, h("th", null, "Note"), h("th", null, "Date"), h("th", null, "What"), h("th", null, "Reason"), h("th", null, "Where it went"))),
              loading && !result ? h(SkeletonRows, { rows: 6, cols: 5 })
                : h("tbody", { style: { opacity: loading ? 0.6 : 1 } }, rows.map((r) => h("tr", {
                  key: r.goodsIssueId, className: `as-row--click${side?.id === r.goodsIssueId ? " as-row--on" : ""}`, onClick: () => setSide({ kind: "view", id: r.goodsIssueId })
                },
                  h("td", null, h("div", { className: "as-cell-main as-mono" }, r.issueNumber), r.isReversed ? h("div", { className: "as-cell-sub" }, `reversed by ${r.reversalNumber}`) : null),
                  h("td", null, fmtDay(r.issueDate)),
                  h("td", null, h("div", null, r.firstItemName, r.lineCount > 1 ? ` and ${r.lineCount - 1} more` : ""),
                    h("div", { className: "as-cell-sub" }, r.firstItemKind === 2 ? h("span", { className: "as-mono" }, r.firstItemCode) : `${qty(r.firstQuantity)} ${r.firstUnit ?? ""}`)),
                  h("td", null, h(Pill, { cls: REASON[r.reason].cls }, REASON[r.reason].label), r.repairOpen ? h("div", { className: "as-cell-sub" }, "still away") : null),
                  h("td", null, r.whereItWent ?? "—")))))),
        h(Pager, { result, loading, onPage: setPage }),
        h("div", { className: "as-footer" }, "Nothing here is ever deleted. A mistake is corrected with a reversing entry, so the trail stays complete for audit.")),
      side?.kind === "form" ? h(IssueForm, { options, preset: side.preset, onClose: () => setSide(null), onPosted: (issue) => { load(); setSide({ kind: "view", id: issue.goodsIssueId }); } })
        : side?.kind === "view" ? h(IssueView, { key: side.id, id: side.id, can, onClose: () => setSide(null), onChanged: (id) => { load(); setSide({ kind: "view", id }); } })
          : null));
}

function IssueView({ id, can, onClose, onChanged }) {
  const toast = useToast();
  const [g, setG] = React.useState(null);
  const [reverse, setReverse] = React.useState(false);
  React.useEffect(() => { api.issue(id).then(setG).catch((e) => toast.error(errorText(e, "Could not load the movement."))); }, [id]);
  if (!g) return h("div", { className: "as-panel as-side" }, h("div", { style: { padding: 40 } }, h(Spinner, { center: true })));
  const details = [
    g.fromLocationName ? ["Taken from", g.fromLocationName] : null,
    g.toLocationName ? ["Moved to", g.toLocationName] : null,
    g.repairVendor ? ["Sent to", `${g.repairVendor}${g.expectedCost != null ? ` · ${money(g.expectedCost)} expected` : ""}`] : null,
    g.reasonText ? ["Reason", g.reasonText] : null,
    g.creditNoteRaised != null ? ["Credit note", g.creditNoteRaised ? "Raised" : "Not raised"] : null,
    g.amountRecovered != null ? ["Recovered", money(g.amountRecovered)] : null,
    g.reportedBy ? ["Reported by", g.reportedBy] : null,
    g.note ? ["Note", g.note] : null
  ].filter(Boolean);
  return h("div", { className: "as-panel as-side" },
    h("div", { className: "as-side__head" },
      h("div", null, h("h3", { className: "as-mono" }, g.issueNumber), h("div", { className: "as-sub" }, `${fmtDay(g.issueDate)} · posted ${fmtWhen(g.postedUtc)}${g.postedByName ? ` by ${g.postedByName}` : ""}`)),
      h("button", { type: "button", className: "as-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 20 }))),
    h("div", { className: "as-side__body" },
      h("div", null, h(Pill, { cls: REASON[g.reason].cls }, REASON[g.reason].label),
        g.reversedByNumber ? h("span", { className: "as-sub" }, ` · reversed by ${g.reversedByNumber}`) : g.reversesNumber ? h("span", { className: "as-sub" }, ` · reverses ${g.reversesNumber}`) : null),
      details.map(([k, v]) => h("div", { key: k }, h("div", { className: "as-kv__k" }, k), h("div", { className: "as-kv__v" }, v))),
      g.lines.map((l) => h("div", { className: "as-line", key: l.goodsIssueLineId },
        h("div", { className: "as-cell-main" }, l.itemName, " ", h(KindTag, { kind: l.kind })),
        h("div", { className: "as-cell-sub" }, h("span", { className: "as-mono" }, l.itemCode), l.kind === 1 ? ` · ${qty(l.quantity)} ${l.unit}` : ""),
        l.kind === 2 && (l.previousStatus || l.newStatus) ? h("div", { className: "as-sub", style: { marginTop: 4 } },
          `${STATUS[l.previousStatus]?.label ?? "—"}${l.previousLocationName ? `, ${l.previousLocationName}` : ""} → ${STATUS[l.newStatus]?.label ?? "—"}${l.newLocationName ? `, ${l.newLocationName}` : ""}`) : null))),
    h("div", { className: "as-side__foot" },
      can.reverse && !g.reversedByIssueId && g.reason !== 7 ? h(Button, { variant: "danger", onClick: () => setReverse(true) }, "Reverse") : null),
    reverse ? h(ReasonDialog, {
      title: `Reverse ${g.issueNumber}`, confirm: "Write the reversing entry",
      intro: "This posts the opposite movement and keeps both. Consumables come back in; an asset goes back to the room and status it had — only while it is still where this movement left it.",
      onClose: () => setReverse(false),
      onSubmit: async (reason) => { const rev = await api.reverseIssue(g.goodsIssueId, reason); toast.success(`Reversed by ${rev.issueNumber}.`); setReverse(false); onChanged(rev.goodsIssueId); }
    }) : null);
}

const REASON_CHIPS = [[1, "Used up"], [2, "Moved"], [3, "Sent for repair"], [4, "Returned to supplier"], [5, "Scrapped"], [6, "Lost"]];

/** §7.2: the reason chosen at the top changes the fields below it. */
function IssueForm({ options, preset, onClose, onPosted }) {
  const toast = useToast();
  const [reason, setReason] = React.useState(preset?.reason ?? 1);
  const [item, setItem] = React.useState(preset?.item ?? null);
  const [f, setF] = React.useState({ date: todayIst(), from: "", to: "", quantity: "", repairVendor: "", expectedCost: "", reasonText: "", credit: "", amountRecovered: "", reportedBy: "", note: "" });
  const [preview, setPreview] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const set = (k) => (ev) => setF((s) => ({ ...s, [k]: ev.target.value }));
  const isAsset = item?.kind === 2;
  const pickKind = reason === 1 ? 1 : reason === 2 || reason === 3 ? 2 : undefined;

  React.useEffect(() => { if (item && pickKind && item.kind !== pickKind) setItem(null); }, [reason]);
  React.useEffect(() => { if (item && !isAsset && !f.from && item.locationId) setF((s) => ({ ...s, from: String(item.locationId) })); }, [item]);

  const lineQty = isAsset ? 1 : Number(f.quantity) || 0;
  const body = {
    reason, issueDate: f.date, fromLocationId: isAsset ? null : numberOrNull(f.from), toLocationId: reason === 2 ? numberOrNull(f.to) : null,
    lines: item ? [{ inventoryItemId: item.inventoryItemId, quantity: lineQty }] : [],
    repairVendor: reason === 3 ? f.repairVendor : null, expectedCost: reason === 3 ? numberOrNull(f.expectedCost) : null,
    creditNoteRaised: reason === 4 ? (f.credit === "" ? null : f.credit === "yes") : null, amountRecovered: reason === 5 ? numberOrNull(f.amountRecovered) : null,
    reportedBy: reason === 6 ? f.reportedBy : null, reasonText: reason === 4 || reason === 5 ? f.reasonText : null, note: f.note || null
  };
  const signature = JSON.stringify({ reason, item: item?.inventoryItemId, lineQty });
  const debounced = useDebounced(signature, 300);
  React.useEffect(() => {
    if (!item || (!isAsset && lineQty <= 0)) { setPreview(null); return; }
    let live = true;
    api.previewIssue(body).then((p) => live && setPreview(p.lines[0] ?? null)).catch(() => live && setPreview(null));
    return () => { live = false; };
  }, [debounced]);

  const post = async () => {
    if (!item) { toast.error("Choose the item."); return; }
    setBusy(true);
    try {
      const result = await api.createIssue(body);
      toast.success(`${result.issue.issueNumber} posted.`);
      onPosted(result.issue);
    } catch (error) { toast.error(errorText(error, "Could not post the movement.")); } finally { setBusy(false); }
  };

  const blocked = !!preview?.error;
  const tone = !preview ? null : preview.error ? "red" : preview.goesBelowReorder ? "red" : "blue";
  return h("div", { className: "as-panel as-side" },
    h("div", { className: "as-side__head" }, h("h3", null, "Record outgoing"), h("button", { type: "button", className: "as-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 20 }))),
    h("div", { className: "as-side__body" },
      h("div", { className: "as-kv__k" }, "Why is it going out? ", h("span", { className: "field__required" }, "*")),
      h("div", { className: "as-reasons" }, REASON_CHIPS.map(([r, l]) => h("button", { key: r, type: "button", className: `as-reason${reason === r ? " as-reason--on" : ""}`, onClick: () => setReason(r) }, l))),
      h("div", { className: "as-form" },
        h(Input, { label: "Date", required: true, type: "date", max: todayIst(), value: f.date, onChange: set("date") }),
        isAsset ? h(Input, { label: "Taken from", value: item.locationName ?? "", disabled: true, hint: "Where the asset is now." })
          : h(Select, { label: "Taken from", value: f.from, placeholder: "Where it is kept", options: roomOptions(options), onChange: set("from") })),
      h(ItemPicker, { label: "Item", required: true, kind: pickKind, value: item, onPick: setItem, hint: "Type a name or code, or scan the QR sticker with the phone camera." }),
      item && !isAsset ? h("div", { className: "as-form" },
        h(Input, { label: "Quantity", required: true, type: "number", min: 0, step: "0.001", value: f.quantity, onChange: set("quantity") }),
        h(Input, { label: "Unit", value: item.unit ?? "", disabled: true })) : null,
      reason === 2 ? h(Select, { label: "Destination room", required: true, value: f.to, placeholder: "Choose", options: roomOptions(options), onChange: set("to") }) : null,
      reason === 3 ? h("div", { className: "as-form" },
        h(Input, { label: "Who it went to", required: true, value: f.repairVendor, onChange: set("repairVendor"), placeholder: "Blue Star Service" }),
        h(Input, { label: "Expected cost (₹)", type: "number", min: 0, value: f.expectedCost, onChange: set("expectedCost") })) : null,
      reason === 4 ? h("div", { className: "as-form" },
        h(Input, { label: "Reason", required: true, value: f.reasonText, onChange: set("reasonText"), placeholder: "Faulty on arrival" }),
        h(Select, { label: "Credit note raised?", value: f.credit, placeholder: "Not known", options: [{ value: "yes", label: "Yes" }, { value: "no", label: "No" }], onChange: set("credit") })) : null,
      reason === 5 ? h("div", { className: "as-form" },
        h(Input, { label: "Reason", required: true, value: f.reasonText, onChange: set("reasonText"), placeholder: "Beyond repair" }),
        h(Input, { label: "Amount recovered (₹)", type: "number", min: 0, value: f.amountRecovered, onChange: set("amountRecovered") })) : null,
      reason === 6 ? h(Input, { label: "Who reported it", required: true, value: f.reportedBy, onChange: set("reportedBy") }) : null,
      preview ? h("div", { className: `as-note as-note--${tone}` }, preview.onHand != null && !preview.error
        ? h(Frag, null, h("strong", null, `${qty(preview.onHand)} ${preview.unit} in stock`), ` — after this, ${qty(preview.after)} ${preview.unit}. `,
          preview.goesBelowReorder ? `That is at or below the reorder level of ${qty(preview.reorderLevel)} — order more.` : `Still above the reorder level of ${qty(preview.reorderLevel)}.`)
        : preview.message) : null,
      h(Input, { label: "Note", value: f.note, onChange: set("note"), placeholder: reason === 1 ? "Pantry refill for the week" : "" }),
      h("p", { className: "as-sub", style: { margin: 0 } },
        h("strong", null, "Moved"), " swaps quantity for a destination room and changes the asset's location. ",
        h("strong", null, "Sent for repair"), " sets it to In repair, so it cannot be moved again until it comes back. ",
        h("strong", null, "Scrapped"), " and ", h("strong", null, "Lost"), " ask for a reason and set the book value to zero; the record stays in the register for audit.")),
    h("div", { className: "as-side__foot" },
      h(Button, { variant: "ghost", onClick: onClose }, "Cancel"),
      h(Button, { onClick: post, loading: busy, disabled: blocked || !item }, "Post movement")));
}
