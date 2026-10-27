/**
 * Admin -> Assets -> Items  (/admin/assets/items)   — Assets spec §4, §5, §9
 *
 * The master list of everything Regalis owns. An ASSET is one physical thing with its
 * own code and QR sticker; a CONSUMABLE is a quantity that goes up and down. Every
 * figure (the five cards, on hand, book value) is worked out on the server across all
 * records — this page never holds a number of its own and has no field that accepts a
 * stock figure.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js lazy-loads this
 * file for /admin/assets/items. Shared pieces live in AssetsCommon-AS1.js.
 */
import { a as useToast, r as React, R as usePermission, p as Button, F as Select, I as Input, G as Spinner } from "./index-DI5X4c9f.js";
import {
  h, Frag, api, money, qty, fmtDay, fmtWhen, todayIst, errorText, useDebounced, useAssetStyles,
  STATUS, CONDITION, DOCUMENT_TYPE, KindTag, StatusPill, Pill, SummaryCard, SkeletonRows, LoadFailed, KV, TextArea,
  Dialog, Drawer, QrImage, LabelSheetDialog, ScanDialog, ImportDialog,
  IconSearch, IconX, IconQr, IconScan, IconBox, IconCoffee, IconChevronLeft, IconChevronRight
} from "./AssetsCommon-AS1.js";

const PAGE_SIZE = 50;
const RETIRED = new Set([4, 5, 6]);
const FILTER_KEYS = ["search", "kind", "categoryId", "locationId", "status", "belowReorder", "noLabel", "costMissing", "includeInactive"];

/** Filters can arrive in the URL — Current Stock links a room here (?locationId=7). */
const filtersFromUrl = () => {
  const q = new URLSearchParams(window.location.search);
  const f = {};
  FILTER_KEYS.forEach((k) => { if (q.get(k)) f[k] = q.get(k); });
  return f;
};
const toParams = (f, page) => {
  const p = { page };
  Object.entries(f).forEach(([k, v]) => { if (v !== "" && v != null && v !== false) p[k] = v === true ? "true" : v; });
  return p;
};

export function AssetItemsPage() {
  useAssetStyles();
  useItemsTableStyles();
  const toast = useToast();
  const can = {
    create: usePermission("INVENTORY.CREATE"),
    edit: usePermission("INVENTORY.EDIT"),
    receive: usePermission("INVENTORY.RECEIVE"),
    issue: usePermission("INVENTORY.ISSUE"),
    labels: usePermission("INVENTORY.LABELS"),
    export: usePermission("INVENTORY.EXPORT")
  };
  const [options, setOptions] = React.useState(null);
  const [filters, setFilters] = React.useState(filtersFromUrl);
  const [searchText, setSearchText] = React.useState(filters.search ?? "");
  const search = useDebounced(searchText, 350);
  const [page, setPage] = React.useState(1);
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [failed, setFailed] = React.useState(false);
  const [selected, setSelected] = React.useState(() => new Map());
  const [drawer, setDrawer] = React.useState(null);
  const [modal, setModal] = React.useState(null);

  React.useEffect(() => { api.options().then(setOptions).catch((e) => toast.error(errorText(e, "Could not load the form lists."))); }, []);
  React.useEffect(() => { setFilters((f) => (f.search ?? "") === search ? f : { ...f, search }); setPage(1); }, [search]);

  const load = React.useCallback(async () => {
    setLoading(true);
    setFailed(false);
    try { setResult(await api.items(toParams(filters, page))); }
    catch (error) { setFailed(true); toast.error(errorText(error, "Could not load the items.")); }
    finally { setLoading(false); }
  }, [filters, page]);
  React.useEffect(() => { load(); }, [load]);

  // ?code=R-2W-E-EAC-001 opens that item straight away (a sticker scanned inside the app).
  React.useEffect(() => {
    const code = new URLSearchParams(window.location.search).get("code");
    if (code) api.byCode(code).then((item) => setDrawer({ id: item.inventoryItemId, position: null })).catch(() => toast.error(`No item has the code ${code}.`));
  }, []);

  // The edit form needs the full record (vendor, remarks, room…), which the list rows do not carry.
  const editRow = async (row) => {
    try { setModal({ kind: "edit", item: await api.item(row.inventoryItemId) }); }
    catch (error) { toast.error(errorText(error, "Could not open the item for editing.")); }
  };
  const setFilter = (key, value) => { setFilters((f) => ({ ...f, [key]: value })); setPage(1); };
  const clearFilters = () => { setFilters({}); setSearchText(""); setPage(1); };
  const toggleCard = (key, value) => {
    const on = String(filters[key] ?? "") === String(value);
    setFilters(on ? {} : { [key]: value });
    setSearchText("");
    setPage(1);
  };

  const rows = result?.page.items ?? [];
  const total = result?.page.totalCount ?? 0;
  const summary = result?.summary;
  const selectedRows = [...selected.values()];
  const toggleRow = (row) => setSelected((s) => { const n = new Map(s); n.has(row.inventoryItemId) ? n.delete(row.inventoryItemId) : n.set(row.inventoryItemId, row); return n; });
  const allOnPage = rows.length > 0 && rows.every((r) => selected.has(r.inventoryItemId));
  const togglePage = () => setSelected((s) => { const n = new Map(s); rows.forEach((r) => (allOnPage ? n.delete(r.inventoryItemId) : n.set(r.inventoryItemId, r))); return n; });

  /** ‹ › in the drawer walk the current filter across pages, without closing it. */
  const step = async (delta) => {
    const position = drawer.position + delta;
    if (position < 0 || position >= total) return;
    const targetPage = Math.floor(position / PAGE_SIZE) + 1;
    let list = rows;
    if (targetPage !== page) {
      try {
        const r = await api.items(toParams(filters, targetPage));
        setResult(r);
        setPage(targetPage);
        list = r.page.items;
      } catch (error) { toast.error(errorText(error, "Could not load the next item.")); return; }
    }
    const row = list[position % PAGE_SIZE];
    if (row) setDrawer({ id: row.inventoryItemId, position });
  };

  const chips = [];
  const optName = (list, id, key, label) => list?.find((x) => String(x[key]) === String(id))?.[label] ?? id;
  if (filters.search) chips.push(["search", `“${filters.search}”`]);
  if (filters.kind) chips.push(["kind", { 1: "Consumables", 2: "Assets", 3: "Services" }[filters.kind]]);
  if (filters.categoryId) chips.push(["categoryId", optName(options?.categories, filters.categoryId, "assetCategoryId", "categoryName")]);
  if (filters.locationId) chips.push(["locationId", optName(options?.locations, filters.locationId, "assetLocationId", "locationName")]);
  if (filters.status) chips.push(["status", STATUS[filters.status]?.label]);
  if (filters.belowReorder) chips.push(["belowReorder", "Below reorder level"]);
  if (filters.noLabel) chips.push(["noLabel", "No QR printed"]);
  if (filters.costMissing) chips.push(["costMissing", "Cost missing"]);
  if (filters.includeInactive) chips.push(["includeInactive", "Including inactive"]);

  const refresh = () => { load(); setSelected(new Map()); };
  const noFilters = chips.length === 0;
  const first = total === 0 ? 0 : (page - 1) * PAGE_SIZE + 1;
  const last = Math.min(page * PAGE_SIZE, total);

  return h("div", { className: "as-page" },
    h("div", { className: "as-head" },
      h("div", null,
        h("h2", null, "Items"),
        h("p", null, "Everything Regalis owns. An ", h("strong", null, "asset"), " is one physical thing with its own code and QR sticker. A ",
          h("strong", null, "consumable"), " is a quantity that goes up and down. Stock itself comes from the movement ledger.")),
      h("div", { className: "as-head__actions" },
        h(Button, { variant: "ghost", size: "sm", onClick: () => setModal({ kind: "scan" }) }, h(IconScan, { size: 16 }), " Scan"),
        can.edit ? h(Button, { variant: "ghost", size: "sm", onClick: () => setModal({ kind: "setup" }) }, "Categories & rooms") : null,
        can.labels ? h(Button, { variant: "outline", onClick: () => setModal({ kind: "labels", params: toParams(filters, 1), title: "QR labels — every item in the current filter" }) }, "QR labels") : null,
        can.export ? h(Button, { variant: "outline", onClick: async () => { if (!(await api.exportItems(toParams(filters, 1)))) toast.error("Nothing matches the filters."); } }, "Export Excel") : null,
        can.create && can.receive ? h(Button, { variant: "outline", onClick: () => setModal({ kind: "import" }) }, "Import Excel") : null,
        can.create ? h(Button, { onClick: () => setModal({ kind: "new" }) }, "+ New item") : null)),

    h("div", { className: "as-cards" },
      h(SummaryCard, { label: "Assets", value: summary ? summary.assetCount : "…", sub: summary ? `${money(summary.assetPurchaseValue)} recorded so far` : "", active: filters.kind === "2" && Object.keys(filters).length === 1, onClick: () => toggleCard("kind", "2") }),
      h(SummaryCard, { label: "Consumables", value: summary ? summary.consumableCount : "…", sub: "tracked by quantity", active: filters.kind === "1" && Object.keys(filters).length === 1, onClick: () => toggleCard("kind", "1") }),
      h(SummaryCard, { label: "Below reorder", value: summary ? summary.belowReorderCount : "…", tone: summary?.belowReorderCount ? "red" : undefined, sub: summary?.belowReorderNames.join(", ") || "nothing to order", active: !!filters.belowReorder && Object.keys(filters).length === 1, onClick: () => toggleCard("belowReorder", true) }),
      h(SummaryCard, { label: "In repair", value: summary ? summary.inRepairCount : "…", sub: summary?.inRepairText || "nothing away", active: filters.status === "3" && Object.keys(filters).length === 1, onClick: () => toggleCard("status", "3") }),
      h(SummaryCard, { label: "No QR printed", value: summary ? summary.noLabelCount : "…", tone: summary?.noLabelCount ? "gold" : undefined, sub: summary?.noLabelCount ? "assets still unlabelled" : "every asset has a sticker", active: !!filters.noLabel && Object.keys(filters).length === 1, onClick: () => toggleCard("noLabel", true) })),
    summary?.costMissingCount ? h("button", { type: "button", className: "as-note as-note--red", style: { textAlign: "left", cursor: "pointer", font: "inherit" }, onClick: () => toggleCard("costMissing", true) },
      h("strong", null, `${summary.costMissingCount} asset${summary.costMissingCount === 1 ? " has" : "s have"} no purchase cost. `), "They show “cost missing” and a dash for value until someone fills it in — click to list them.") : null,

    h("div", { className: "as-panel" },
      h("div", { className: "as-filters as-filters--items" },
        h("div", { className: "as-search" }, h(IconSearch, { size: 16 }),
          h("input", { className: "field__input", type: "search", value: searchText, placeholder: "Search name, code, serial number or brand…", "aria-label": "Search items", onChange: (ev) => setSearchText(ev.target.value) })),
        h(Select, { label: "Kind", value: filters.kind ?? "", placeholder: "All kinds", options: [{ value: "2", label: "Assets" }, { value: "1", label: "Consumables" }, { value: "3", label: "Services (sold on quotations)" }], onChange: (ev) => setFilter("kind", ev.target.value) }),
        h(Select, { label: "Category", value: filters.categoryId ?? "", placeholder: "All categories", options: (options?.categories ?? []).map((c) => ({ value: String(c.assetCategoryId), label: c.categoryName })), onChange: (ev) => setFilter("categoryId", ev.target.value) }),
        h(Select, { label: "Location", value: filters.locationId ?? "", placeholder: "All locations", options: (options?.locations ?? []).map((l) => ({ value: String(l.assetLocationId), label: l.locationName })), onChange: (ev) => setFilter("locationId", ev.target.value) }),
        h(Select, { label: "Status", value: filters.status ?? "", placeholder: "Any status", options: Object.entries(STATUS).map(([v, s]) => ({ value: v, label: s.label })), onChange: (ev) => setFilter("status", ev.target.value) })),
      h("div", { className: "as-checks" },
        h("label", null, h("input", { type: "checkbox", checked: !!filters.belowReorder, onChange: (ev) => setFilter("belowReorder", ev.target.checked) }), "Only below reorder level"),
        h("label", null, h("input", { type: "checkbox", checked: !!filters.noLabel, onChange: (ev) => setFilter("noLabel", ev.target.checked) }), "Only assets with no QR printed"),
        h("label", null, h("input", { type: "checkbox", checked: !!filters.includeInactive, onChange: (ev) => setFilter("includeInactive", ev.target.checked) }), "Include inactive"),
        h("span", { className: "as-checks__count" }, result ? (total ? `Showing ${first}–${last} of ${total}` : "Showing 0") : "")),
      chips.length ? h("div", { className: "as-chips" },
        chips.map(([key, label]) => h("span", { className: "as-chip", key }, label,
          h("button", { type: "button", "aria-label": `Remove ${label}`, onClick: () => (key === "search" ? setSearchText("") : setFilter(key, "")) }, h(IconX, { size: 14 })))),
        h("button", { type: "button", className: "as-link", onClick: clearFilters }, "Clear all")) : null,

      selectedRows.length ? h(BulkBar, { rows: selectedRows, can, onClear: () => setSelected(new Map()), onAction: setModal }) : null,

      failed ? h(LoadFailed, { onRetry: load })
        : result && total === 0 ? h(EmptyItems, { noFilters, can, onNew: () => setModal({ kind: "new" }), onImport: () => setModal({ kind: "import" }), onClear: clearFilters })
          : h("div", { className: "as-table-wrap" },
            h("table", { className: "as-table as-table--items" },
              // Fixed widths so every header sits over its values and short columns never wrap;
              // the Item column takes the rest. On hand is shown in the item drawer, not here.
              h("colgroup", null, ITEM_COLUMNS.map((c) => h("col", { key: c.key, style: c.width ? { width: c.width } : undefined }))),
              h("thead", null, h("tr", null,
                h("th", null, h("input", { type: "checkbox", "aria-label": "Select every row on this page", checked: allOnPage, onChange: togglePage })),
                h("th", null, "Item"), h("th", null, "Kind"), h("th", null, "Category"), h("th", null, "Location"),
                h("th", null, "Unit"), h("th", null, "Status"),
                h("th", { className: "num" }, "Unit cost"), h("th", { className: "num" }, "Value"), h("th", { className: "num" }, "Actions"))),
              loading && !result ? h(SkeletonRows, { rows: 8, cols: ITEM_COLUMNS.length })
                : h("tbody", { style: { opacity: loading ? 0.6 : 1 } }, rows.map((r, i) => h(ItemRow, {
                  key: r.inventoryItemId, row: r, checked: selected.has(r.inventoryItemId), onCheck: () => toggleRow(r),
                  onOpen: () => setDrawer({ id: r.inventoryItemId, position: (page - 1) * PAGE_SIZE + i }),
                  onEdit: can.edit ? () => editRow(r) : null
                }))))),
      result && total > 0 ? h("div", { className: "as-footer" },
        h("span", null, `Page ${page} of ${result.page.totalPages} · ${total} record${total === 1 ? "" : "s"} · the cards above count every record, not this page`),
        h("div", { className: "as-pager" },
          h(Button, { variant: "outline", size: "sm", disabled: page <= 1 || loading, onClick: () => setPage(page - 1), "aria-label": "Previous page" }, h(IconChevronLeft, { size: 16 })),
          h(Button, { variant: "outline", size: "sm", disabled: !result.page.hasNext || loading, onClick: () => setPage(page + 1), "aria-label": "Next page" }, h(IconChevronRight, { size: 16 })))) : null),

    drawer ? h(ItemDrawer, {
      id: drawer.id, position: drawer.position, total, can, options,
      onClose: () => setDrawer(null), onStep: step, onChanged: load, onAction: setModal
    }) : null,
    modal ? h(Modals, { modal, options, can, onClose: () => setModal(null), onDone: () => { setModal(null); refresh(); }, openItem: (id) => setDrawer({ id, position: null }), setModal, reloadOptions: () => api.options().then(setOptions) }) : null);
}

function EmptyItems({ noFilters, can, onNew, onImport, onClear }) {
  return noFilters
    ? h("div", { className: "as-empty" }, h("strong", null, "Nothing is in the register yet."),
      h("div", null, can.create && can.receive ? h(Button, { variant: "outline", onClick: onImport }, "Import from Excel") : null, can.create ? h(Button, { onClick: onNew }, "Add the first item") : null))
    : h("div", { className: "as-empty" }, h("strong", null, "No items match these filters."), h("div", null, h(Button, { variant: "outline", onClick: onClear }, "Clear filters")));
}

function ItemRow({ row: r, checked, onCheck, onOpen, onEdit }) {
  const isAsset = r.kind === 2;
  const retired = isAsset && RETIRED.has(r.status);
  return h("tr", { className: ["as-row--click", r.belowReorder ? "as-row--alert" : "", checked ? "as-row--on" : "", !r.isActive ? "as-row--muted" : ""].join(" "), onClick: (ev) => { if (!ev.target.closest("input,button,a")) onOpen(); } },
    h("td", null, h("input", { type: "checkbox", "aria-label": `Select ${r.itemCode}`, checked, onChange: onCheck })),
    h("td", null,
      h("div", { className: "as-cell-main" }, r.itemName),
      h("div", { className: "as-cell-sub as-mono" }, [r.itemCode, isAsset ? r.brandModel : null].filter(Boolean).join(" · "))),
    h("td", null, h(KindTag, { kind: r.kind, service: r.isService })),
    h("td", { className: "as-nowrap", title: r.categoryName ?? undefined }, r.categoryName ?? h("span", { className: "as-muted" }, "—")),
    h("td", { className: "as-nowrap", title: r.locationName ?? undefined }, r.locationName ?? h("span", { className: "as-muted" }, "—")),
    // An asset is one thing, so it has no unit; the on-hand count lives in the item drawer.
    h("td", { className: "as-nowrap" }, isAsset || r.isService || !r.unit ? h("span", { className: "as-muted" }, "—") : r.unit),
    h("td", null,
      isAsset ? h(Frag, null, h(StatusPill, { status: r.status }),
        r.warrantyExpiringSoon ? h("div", { className: "as-cell-sub" }, `Warranty ends ${fmtDay(r.warrantyUntil)}`) : null)
        : r.isService ? h("span", { className: "as-muted" }, "Not stocked")
          : r.belowReorder ? h(Pill, { cls: "order" }, `Below reorder (${qty(r.reorderLevel)})`)
            : Number(r.onHand) > 0 ? h(Pill, { cls: "fine" }, "In stock") : h(Pill, { cls: "order" }, "Out of stock"),
      !r.isActive ? h(Frag, null, " ", h(Pill, { cls: "inactive" }, "Inactive")) : null),
    h("td", { className: `num${r.unitCost == null && !r.isService ? " as-costless" : ""}` }, r.unitCost == null ? (r.isService ? "—" : "cost missing") : money(r.unitCost, { paise: !isAsset })),
    h("td", { className: "num" }, h("strong", null, r.value == null || retired ? (retired ? money(0) : "—") : money(Math.round(r.value)))),
    h("td", { className: "num" },
      onEdit ? h(Frag, null, h("button", { type: "button", className: "as-link", onClick: onEdit, "aria-label": `Edit ${r.itemCode}` }, "Edit"), " · ") : null,
      h("button", { type: "button", className: "as-link", onClick: onOpen }, "View")));
}

/** Items table columns, left to right. A width of null is the Item column, which takes the rest. */
const ITEM_COLUMNS = [
  { key: "pick", width: 48 }, { key: "item", width: null }, { key: "kind", width: 116 }, { key: "category", width: 172 },
  { key: "location", width: 124 }, { key: "unit", width: 72 }, { key: "status", width: 150 }, { key: "cost", width: 112 },
  { key: "value", width: 100 }, { key: "actions", width: 116 }
];
const ITEMS_STYLE_ID = "asset-items-table-styles";
const ITEMS_STYLES = `
.as-filters--items .field{flex:1 1 150px}
@media(max-width:1440px){.as-filters--items .as-search{flex-basis:100%}.as-filters--items .field{flex:1 1 0}}
@media(max-width:640px){.as-filters--items .field{flex:1 1 100%}}
.as-table--items{table-layout:fixed;min-width:1240px}
.as-table--items td,.as-table--items th{overflow:hidden;text-overflow:ellipsis}
.as-table--items th:first-child,.as-table--items td:first-child{padding-inline:18px 0;text-overflow:clip}
.as-table--items .as-nowrap{white-space:nowrap}
.as-table--items .as-cell-main{overflow-wrap:anywhere}
.as-table--items .as-cell-sub{white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
`;
function useItemsTableStyles() {
  React.useEffect(() => {
    if (document.getElementById(ITEMS_STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = ITEMS_STYLE_ID;
    style.textContent = ITEMS_STYLES;
    document.head.appendChild(style);
  }, []);
}

/** Units are typed as a number. Older items carry text such as "1Nos" or "Nos":
 *  the leading number is kept, anything else starts blank (and blank keeps it as is). */
const unitNumber = (unit) => /^\s*(\d+(?:\.\d+)?)/.exec(unit ?? "")?.[1] ?? "";

/** "Selecting rows opens a bar" (§4.5). Actions that make no sense for consumables
 *  skip them, and the message says how many — never a silent partial action. */
function BulkBar({ rows, can, onClear, onAction }) {
  const assets = rows.filter((r) => r.kind === 2 && !RETIRED.has(r.status) && r.isActive);
  const skipped = rows.length - assets.length;
  const ids = rows.map((r) => r.inventoryItemId).join(",");
  return h("div", { className: "as-bulk", role: "region", "aria-label": "Selected rows" },
    h("strong", null, `${rows.length} selected`),
    can.labels ? h(Button, { size: "sm", onClick: () => onAction({ kind: "labels", params: { ids }, title: "QR labels — selected items" }) }, "Print QR labels") : null,
    can.issue ? h(Button, { size: "sm", variant: "outline", disabled: assets.length === 0, onClick: () => onAction({ kind: "move", rows: assets, skipped }) }, "Move to another location") : null,
    can.issue ? h(Button, { size: "sm", variant: "outline", disabled: assets.length === 0, onClick: () => onAction({ kind: "status", rows: assets, skipped }) }, "Change status") : null,
    can.export ? h(Button, { size: "sm", variant: "outline", onClick: () => api.exportItems({ ids, includeInactive: true }) }, "Export selected") : null,
    skipped ? h("span", { className: "as-sub" }, `${skipped} consumable${skipped === 1 ? "" : "s"} or retired item${skipped === 1 ? "" : "s"} will be skipped by moves and status changes.`) : null,
    h("button", { type: "button", className: "as-link", style: { marginLeft: "auto" }, onClick: onClear }, "Clear selection"));
}

// ── The drawer (§4.6) ───────────────────────────────────────────────────────

function ItemDrawer({ id, position, total, can, options, onClose, onStep, onChanged, onAction }) {
  const toast = useToast();
  const [item, setItem] = React.useState(null);
  const [tab, setTab] = React.useState("overview");
  const [failed, setFailed] = React.useState(false);
  const load = React.useCallback(async () => {
    setFailed(false);
    try { setItem(await api.item(id)); } catch (error) { setFailed(true); toast.error(errorText(error, "Could not load the item.")); }
  }, [id]);
  React.useEffect(() => { setItem(null); load(); }, [load]);
  const changed = () => { load(); onChanged(); };

  const isAsset = item?.kind === 2;
  const inRegister = isAsset && !RETIRED.has(item.status) && item.isActive;
  const tabs = [["overview", "Overview"], ["movements", "Movements"], ...(isAsset ? [["repairs", "Repairs"]] : []), ["value", "Value"], ["documents", "Documents"]];
  const canDeactivate = item && item.isActive && (item.isService || (isAsset ? RETIRED.has(item.status) : item.onHand === 0));

  return h(Drawer, { label: item ? item.itemName : "Item", onClose },
    !item ? (failed ? h(LoadFailed, { onRetry: load }) : h("div", { style: { padding: 40 } }, h(Spinner, { center: true })))
      : h(Frag, null,
        h("div", { className: "as-drawer__head" },
          h("div", { className: "as-drawer__icon" }, h(isAsset ? IconBox : IconCoffee, { size: 24 })),
          h("div", { style: { minWidth: 0 } },
            h("h3", { className: "as-drawer__title" }, item.itemName),
            h("div", { className: "as-sub" }, h("span", { className: "as-mono" }, item.itemCode), [item.brandModel, item.siblingLabel].filter(Boolean).map((t) => ` · ${t}`).join(""))),
          h("div", { className: "as-drawer__actions" },
            isAsset ? h(StatusPill, { status: item.status }) : item.isService ? h(KindTag, { service: true }) : item.belowReorder ? h(Pill, { cls: "order" }, "Order now") : h(Pill, { cls: "fine" }, "Fine"),
            !item.isActive ? h(Pill, { cls: "inactive" }, "Inactive") : null,
            inRegister && can.issue && item.status !== 3 ? h(Button, { size: "sm", variant: "outline", onClick: () => onAction({ kind: "move", rows: [item], onDone: changed }) }, "Move") : null,
            inRegister && can.issue && item.status !== 3 ? h(Button, { size: "sm", variant: "outline", onClick: () => onAction({ kind: "repair", rows: [item], onDone: changed }) }, "Log repair") : null,
            can.edit ? h(Button, { size: "sm", variant: "outline", onClick: () => onAction({ kind: "edit", item, onDone: changed }) }, "Edit") : null,
            inRegister && can.labels ? h(Button, { size: "sm", onClick: () => onAction({ kind: "labels", params: { ids: String(item.inventoryItemId) }, title: `QR label — ${item.itemCode}`, onDone: changed }) }, "Print label") : null,
            h("button", { type: "button", className: "as-close", onClick: onClose, "aria-label": "Close" }, h(IconX, { size: 20 })))),
        h("div", { className: "as-drawer__body" },
          h("div", { className: "as-tabs", role: "tablist" }, tabs.map(([key, label]) =>
            h("button", { key, type: "button", role: "tab", "aria-selected": tab === key, className: `as-tab${tab === key ? " as-tab--on" : ""}`, onClick: () => setTab(key) }, label))),
          tab === "overview" ? h(Overview, { item, options })
            : tab === "movements" ? h(Movements, { id })
              : tab === "repairs" ? h(Repairs, { item, can, onChanged: changed })
                : tab === "value" ? h(ValueTab, { item })
                  : h(Documents, { item, can })),
        h("div", { className: "as-drawer__foot" },
          h("div", { className: "as-pager" },
            h(Button, { variant: "outline", size: "sm", disabled: position == null || position <= 0, onClick: () => onStep(-1), "aria-label": "Previous item" }, h(IconChevronLeft, { size: 16 })),
            h(Button, { variant: "outline", size: "sm", disabled: position == null || position >= total - 1, onClick: () => onStep(1), "aria-label": "Next item" }, h(IconChevronRight, { size: 16 }))),
          can.edit && (canDeactivate || !item.isActive) ? h("button", { type: "button", className: "as-link", onClick: async () => {
            try {
              if (item.isActive) await api.deactivate(item.inventoryItemId, null); else await api.activate(item.inventoryItemId);
              toast.success(item.isActive ? "Made inactive." : "Made active again.");
              changed();
            } catch (error) { toast.error(errorText(error, "That did not work.")); }
          } }, item.isActive ? "Make inactive" : "Make active") : null,
          h("span", null, position == null ? "" : `Item ${position + 1} of ${total} in the current filter`))));
}

function Overview({ item, options }) {
  const isAsset = item.kind === 2;
  const vendor = item.supplierName ?? item.vendorName;
  const fields = isAsset ? [
    h(KV, { key: "k", k: "Kind", v: h(KindTag, { kind: 2 }) }),
    h(KV, { key: "c", k: "Category", v: item.categoryName }),
    h(KV, { key: "b", k: "Brand / model", v: item.brandModel }),
    h(KV, { key: "s", k: "Serial number", v: item.serialNumber, missing: item.serialNumber ? undefined : "Not captured" }),
    h(KV, { key: "l", k: "Location", v: item.locationName }),
    h(KV, { key: "co", k: "Condition", v: CONDITION[item.condition] }),
    h(KV, { key: "p", k: "Purchase date", v: item.purchaseDate ? fmtDay(item.purchaseDate) : null, missing: item.purchaseDate ? undefined : "Not captured" }),
    h(KV, { key: "v", k: "Vendor", v: vendor }),
    h(KV, { key: "pc", k: "Purchase cost", v: item.unitCost == null ? null : money(item.unitCost), missing: item.unitCost == null ? "Cost missing" : undefined }),
    h(KV, { key: "bv", k: "Book value today", v: item.value.bookValue == null ? "—" : h("strong", null, money(Math.round(item.value.bookValue))) }),
    h(KV, { key: "w", k: "Warranty until", v: item.warrantyUntil ? `${fmtDay(item.warrantyUntil)}${item.warrantyExpiringSoon ? " — expiring soon" : ""}` : null }),
    h(KV, { key: "st", k: "Status", v: h(StatusPill, { status: item.status }) })
  ] : [
    h(KV, { key: "k", k: "Kind", v: h(KindTag, { kind: 1, service: item.isService }) }),
    h(KV, { key: "c", k: "Category", v: item.categoryName }),
    h(KV, { key: "u", k: "Unit", v: item.unit }),
    ...(item.isService ? [] : [
      h(KV, { key: "o", k: "On hand", v: h("strong", { className: item.belowReorder ? "as-out" : undefined }, `${qty(item.onHand)} ${item.unit}`) }),
      h(KV, { key: "r", k: "Reorder level", v: `${qty(item.reorderLevel)} ${item.unit}` }),
      h(KV, { key: "kept", k: "Kept in", v: item.keptInLocationName })]),
    h(KV, { key: "uc", k: "Unit cost", v: item.unitCost == null ? null : money(item.unitCost, { paise: true }), missing: item.unitCost == null && !item.isService ? "Cost missing" : undefined }),
    h(KV, { key: "v", k: "Vendor", v: vendor }),
    item.hsnSacCode || item.sellingRate ? h(KV, { key: "hs", k: "Sold on quotations", v: `${item.hsnSacCode ? `HSN/SAC ${item.hsnSacCode} · ` : ""}${money(item.sellingRate, { paise: true })} · ${item.isNonTaxable ? "non-taxable" : `GST ${item.taxPercent}%`}` }) : null
  ];
  return h("div", { className: isAsset ? "as-overview" : undefined },
    h("div", null,
      h("div", { className: "as-grid" }, fields),
      item.retiredReason || item.retiredUtc ? h("div", { className: "as-note as-note--red", style: { marginTop: "var(--space-4)" } },
        h("strong", null, isAsset && RETIRED.has(item.status) ? `${STATUS[item.status].label}. ` : "Taken out of the register. "),
        [item.retiredReason, item.retiredValue != null ? `${money(item.retiredValue)} recovered` : null, item.retiredUtc ? fmtWhen(item.retiredUtc) : null].filter(Boolean).join(" · "),
        " The record stays for audit.") : null,
      item.description ? h(Frag, null, h("div", { className: "as-kv__k", style: { marginTop: "var(--space-4)" } }, "Description"), h("div", { className: "as-kv__v" }, item.description)) : null,
      item.remarks ? h(Frag, null, h("div", { className: "as-kv__k", style: { marginTop: "var(--space-3)" } }, "Remarks"), h("div", { className: "as-kv__v" }, item.remarks)) : null,
      h("h4", { className: "as-section-title" }, "Last movements"),
      h(EventTable, { events: item.recentEvents }),
      h("p", { className: "as-sub" }, "Every row here is a movement or an action on the record. Nothing about this item changes without one.")),
    isAsset ? h("div", null,
      h("div", { className: "as-qrbox" },
        h("div", { className: "as-kv__k" }, "Its QR code"),
        h(QrImage, { text: item.qrText, title: `QR code for ${item.itemCode}` }),
        h("div", { className: "as-sub", style: { textAlign: "center" } }, item.labelPrintedUtc ? `Printed ${fmtDay(item.labelPrintedUtc.slice(0, 10))} · level Q` : "Never printed — no sticker on it yet · level Q")),
      h("div", { className: "as-qrbox" },
        h("div", { className: "as-kv__k" }, "What the code says when scanned"),
        h("pre", { className: "as-qrtext" }, item.qrText),
        h("div", { className: "as-sub" }, "Plain text, not a link. Any phone camera reads it with no internet and no login."))) : null);
}

function EventTable({ events }) {
  if (!events) return h(Spinner, { center: true });
  if (events.length === 0) return h("p", { className: "as-muted" }, "Nothing recorded yet.");
  return h("div", { className: "as-panel as-table-wrap" },
    h("table", { className: "as-table" },
      h("thead", null, h("tr", null, h("th", null, "When"), h("th", null, "What"), h("th", null, "From → to"), h("th", null, "Document"), h("th", null, "By"))),
      h("tbody", null, events.map((e) => h("tr", { key: e.itemEventId },
        h("td", null, fmtDay(e.eventDate)),
        h("td", null, e.summary),
        h("td", null, e.fromText || e.toText ? `${e.fromText ?? "—"} → ${e.toText ?? "—"}` : "—"),
        h("td", { className: "as-mono" }, e.documentNumber ?? "—"),
        h("td", null, e.actorName ?? "—"))))));
}

function Movements({ id }) {
  const [events, setEvents] = React.useState(null);
  React.useEffect(() => { api.events(id).then(setEvents).catch(() => setEvents([])); }, [id]);
  return h(EventTable, { events });
}

function Repairs({ item, can, onChanged }) {
  const toast = useToast();
  const [repairs, setRepairs] = React.useState(null);
  const [closing, setClosing] = React.useState(null);
  const [form, setForm] = React.useState({ closedOn: todayIst(), cost: "", note: "", condition: "" });
  const [busy, setBusy] = React.useState(false);
  const load = () => api.repairs(item.inventoryItemId).then(setRepairs).catch(() => setRepairs([]));
  React.useEffect(() => { load(); }, [item.inventoryItemId]);
  const close = async () => {
    setBusy(true);
    try {
      await api.closeRepair(item.inventoryItemId, closing, {
        closedOn: form.closedOn || null, cost: form.cost === "" ? null : Number(form.cost), note: form.note || null,
        condition: form.condition ? Number(form.condition) : null
      });
      toast.success("Repair closed. It is back in use.");
      setClosing(null);
      load();
      onChanged();
    } catch (error) { toast.error(errorText(error, "Could not close the repair.")); } finally { setBusy(false); }
  };
  if (!repairs) return h(Spinner, { center: true });
  return h("div", null,
    repairs.length === 0 ? h("p", { className: "as-muted" }, "No repairs recorded. Use Log repair when it goes out to be fixed.")
      : h("div", { className: "as-panel as-table-wrap" }, h("table", { className: "as-table" },
        h("thead", null, h("tr", null, h("th", null, "Sent"), h("th", null, "To"), h("th", null, "What was done"), h("th", { className: "num" }, "Cost"), h("th", null, "State"), h("th", null, ""))),
        h("tbody", null, repairs.map((r) => h("tr", { key: r.assetRepairId },
          h("td", null, fmtDay(r.sentOn), h("div", { className: "as-cell-sub as-mono" }, r.issueNumber)),
          h("td", null, r.repairVendor),
          h("td", null, [r.description, r.closedNote].filter(Boolean).join(" — ") || "—"),
          h("td", { className: "num" }, r.cost != null ? money(r.cost) : r.expectedCost != null ? `${money(r.expectedCost)} expected` : "—"),
          h("td", null, r.isOpen ? h(Pill, { cls: "repair" }, "Open") : `Back ${fmtDay(r.closedOn)}`),
          h("td", { className: "num" }, r.isOpen && can.issue ? h("button", { type: "button", className: "as-link", onClick: () => setClosing(r.assetRepairId) }, "Close repair") : null)))))),
    closing ? h("div", { className: "as-panel as-panel__pad", style: { marginTop: "var(--space-4)" } },
      h("h4", { style: { margin: "0 0 var(--space-3)" } }, "It is back — close the repair"),
      h("div", { className: "as-form" },
        h(Input, { label: "Back on", type: "date", value: form.closedOn, max: todayIst(), onChange: (ev) => setForm({ ...form, closedOn: ev.target.value }) }),
        h(Input, { label: "Repair cost (₹)", type: "number", min: 0, step: "0.01", value: form.cost, onChange: (ev) => setForm({ ...form, cost: ev.target.value }) }),
        h(Select, { label: "Condition now", value: form.condition, placeholder: "Unchanged", options: Object.entries(CONDITION).map(([v, l]) => ({ value: v, label: l })), onChange: (ev) => setForm({ ...form, condition: ev.target.value }) }),
        h(Input, { label: "What was done", value: form.note, onChange: (ev) => setForm({ ...form, note: ev.target.value }) })),
      h("div", { style: { display: "flex", gap: 8, justifyContent: "flex-end", marginTop: "var(--space-3)" } },
        h(Button, { variant: "ghost", onClick: () => setClosing(null) }, "Cancel"), h(Button, { onClick: close, loading: busy }, "Close repair"))) : null);
}

function ValueTab({ item }) {
  const v = item.value;
  if (item.kind !== 2) {
    return h("div", { className: "as-grid" },
      h(KV, { k: "On hand", v: item.onHand == null ? "—" : `${qty(item.onHand)} ${item.unit}` }),
      h(KV, { k: "Unit cost", v: money(item.unitCost, { paise: true }) }),
      h(KV, { k: "Value", v: h("strong", null, v.onHandValue == null ? "—" : money(v.onHandValue, { paise: true })) }),
      h(KV, { k: "Method", v: "Quantity × unit cost" }));
  }
  return h("div", null,
    h("div", { className: "as-grid" },
      h(KV, { k: "Purchase cost", v: v.purchaseCost == null ? null : money(v.purchaseCost), missing: v.purchaseCost == null ? "Cost missing" : undefined }),
      h(KV, { k: "Book value today", v: v.bookValue == null ? "—" : h("strong", null, money(Math.round(v.bookValue))) }),
      h(KV, { k: "Depreciation", v: v.method }),
      h(KV, { k: "Useful life", v: v.usefulLifeYears ? `${qty(v.usefulLifeYears)} years (from the category)` : "—" }),
      h(KV, { k: "Age", v: v.ageYears == null ? null : `${qty(v.ageYears)} years`, missing: v.ageYears == null ? "No purchase date" : undefined }),
      h(KV, { k: "Life used", v: v.lifeUsedPercent == null ? "—" : `${qty(v.lifeUsedPercent)}%` }),
      h(KV, { k: "Total repair spend", v: money(v.repairSpend) })),
    h("p", { className: "as-sub", style: { marginTop: "var(--space-4)" } },
      "Book value = purchase cost × (1 − age ÷ useful life), never below 5% of cost while the asset is in the register. A scrapped, lost or returned asset is worth 0. Worked out when you open it — never stored."));
}

function Documents({ item, can }) {
  const toast = useToast();
  const [docs, setDocs] = React.useState(null);
  const [type, setType] = React.useState("1");
  const [file, setFile] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const load = () => api.documents(item.inventoryItemId).then(setDocs).catch(() => setDocs([]));
  React.useEffect(() => { load(); }, [item.inventoryItemId]);
  const upload = async () => {
    if (!file) { toast.error("Choose a file."); return; }
    setBusy(true);
    try { await api.addDocument(item.inventoryItemId, file, Number(type)); toast.success("Document added."); setFile(null); load(); }
    catch (error) { toast.error(errorText(error, "Could not upload the file.")); } finally { setBusy(false); }
  };
  return h("div", null,
    !docs ? h(Spinner, { center: true }) : docs.length === 0 ? h("p", { className: "as-muted" }, "No purchase invoice, warranty card or photo yet.")
      : h("div", { className: "as-panel as-table-wrap" }, h("table", { className: "as-table" },
        h("thead", null, h("tr", null, h("th", null, "Document"), h("th", null, "File"), h("th", null, "Added"), h("th", null, ""))),
        h("tbody", null, docs.map((d) => h("tr", { key: d.itemDocumentId },
          h("td", null, DOCUMENT_TYPE[d.documentType]),
          h("td", null, d.fileName, h("div", { className: "as-cell-sub" }, `${Math.max(1, Math.round(d.fileSizeBytes / 1024))} KB`)),
          h("td", null, fmtWhen(d.uploadedUtc), h("div", { className: "as-cell-sub" }, d.uploadedByName ?? "")),
          h("td", { className: "num" }, h("button", { type: "button", className: "as-link", onClick: () => api.openDocument(item.inventoryItemId, d.itemDocumentId) }, "Open"))))))),
    can.edit ? h("div", { className: "as-panel as-panel__pad", style: { marginTop: "var(--space-4)" } },
      h("div", { className: "as-form" },
        h(Select, { label: "What is it", value: type, options: Object.entries(DOCUMENT_TYPE).map(([v, l]) => ({ value: v, label: l })), onChange: (ev) => setType(ev.target.value) }),
        h(Input, { label: "File (.pdf .jpg .png, up to 10 MB)", type: "file", accept: ".pdf,.jpg,.jpeg,.png", onChange: (ev) => setFile(ev.target.files?.[0] ?? null) })),
      h("div", { style: { display: "flex", justifyContent: "flex-end", marginTop: "var(--space-3)" } }, h(Button, { onClick: upload, loading: busy }, "Upload"))) : null);
}

// ── Dialogs ─────────────────────────────────────────────────────────────────

function Modals({ modal, options, can, onClose, onDone, openItem, setModal, reloadOptions }) {
  const done = () => { modal.onDone?.(); onDone(); };
  switch (modal.kind) {
    case "new": return h(NewItemDialog, { options, onClose, onCreated: (result, print) => {
      onDone();
      if (print && result.inventoryItemIds.length && can.labels && result.receiptNumber) {
        setModal({ kind: "labels", params: { ids: result.inventoryItemIds.join(",") }, title: `QR labels — ${result.codes.length} new asset${result.codes.length === 1 ? "" : "s"}` });
      } else if (result.inventoryItemIds.length === 1) openItem(result.inventoryItemIds[0]);
    } });
    case "edit": return h(EditItemDialog, { item: modal.item, options, onClose, onSaved: done });
    case "labels": return h(LabelSheetDialog, { params: modal.params, title: modal.title, onClose, onPrinted: () => modal.onDone?.() });
    case "move": return h(MoveDialog, { rows: modal.rows, skipped: modal.skipped, options, onClose, onDone: done });
    case "repair": return h(StatusDialog, { rows: modal.rows, fixedReason: 3, onClose, onDone: done });
    case "status": return h(StatusDialog, { rows: modal.rows, skipped: modal.skipped, onClose, onDone: done });
    case "import": return h(ImportDialog, { onClose, onDone: () => { reloadOptions(); onDone(); } });
    case "setup": return h(SetupDialog, { onClose: () => { reloadOptions(); onDone(); } });
    case "scan": return h(ScanDialog, { onClose, onFound: (item) => { onClose(); openItem(item.inventoryItemId); } });
    default: return null;
  }
}

const roomOptions = (options) => (options?.locations ?? []).map((l) => ({ value: String(l.assetLocationId), label: l.locationName + (l.isStore ? " (store)" : "") }));
const numberOrNull = (v) => (v === "" || v == null ? null : Number(v));

/** §5: the first question changes the rest of the form, and is never asked again. */
function NewItemDialog({ options, onClose, onCreated }) {
  const toast = useToast();
  const [kind, setKind] = React.useState(2);
  const [service, setService] = React.useState(false);
  const [f, setF] = React.useState({ itemName: "", categoryId: "", codePrefix: "", quantity: "1", brandModel: "", serialNumber: "", locationId: "", condition: "1",
    purchaseDate: todayIst(), unitCost: "", supplierId: "", vendorName: "", warrantyUntil: "", description: "", remarks: "",
    itemCode: "", unit: "", reorderLevel: "", keptInLocationId: "", hsnSacCode: "", sellingRate: "", taxPercent: "", sold: false });
  const [prefixTouched, setPrefixTouched] = React.useState(false);
  const [print, setPrint] = React.useState(true);
  const [errors, setErrors] = React.useState({});
  const [codes, setCodes] = React.useState(null);
  const [busy, setBusy] = React.useState(false);
  const set = (k) => (ev) => setF((s) => ({ ...s, [k]: ev.target.type === "checkbox" ? ev.target.checked : ev.target.value }));
  const categories = (options?.categories ?? []).filter((c) => c.isActive);
  const sorted = [...categories].sort((a, b) => (a.defaultKind === kind ? 0 : 1) - (b.defaultKind === kind ? 0 : 1));

  const prefix = useDebounced(f.codePrefix.trim(), 300);
  const count = useDebounced(f.quantity, 300);
  React.useEffect(() => {
    if (kind !== 2 || !prefix) { setCodes(null); return; }
    let live = true;
    api.nextCodes(prefix, Math.max(1, Math.min(500, Number(count) || 1))).then((r) => { if (live) setCodes(r); }).catch(() => live && setCodes(null));
    return () => { live = false; };
  }, [kind, prefix, count]);

  const onCategory = (ev) => {
    const id = ev.target.value;
    const cat = categories.find((c) => String(c.assetCategoryId) === id);
    setF((s) => ({ ...s, categoryId: id, codePrefix: prefixTouched || !cat ? s.codePrefix : cat.codePrefix }));
  };
  const n = Math.max(0, Math.floor(Number(f.quantity) || 0));
  const totalCost = f.unitCost === "" ? null : n * Number(f.unitCost);

  const submit = async () => {
    const e = {};
    if (!f.itemName.trim()) e.itemName = "Item name is required.";
    if (kind === 2) {
      if (!f.categoryId) e.categoryId = "Choose a category.";
      if (!f.codePrefix.trim()) e.codePrefix = "Code prefix is required.";
      if (!(n >= 1 && n <= 500)) e.quantity = "Enter 1 to 500.";
      if (!f.locationId) e.locationId = "Choose the room it is in.";
      if (!f.purchaseDate) e.purchaseDate = "Purchase date is required.";
      if (f.unitCost === "") e.unitCost = "Purchase cost each is required.";
    } else {
      if (!f.itemCode.trim()) e.itemCode = "Code is required.";
      if (!service) {
        if (!f.categoryId) e.categoryId = "Choose a category.";
        if (!f.unit) e.unit = "Unit is required.";
        if (f.reorderLevel === "") e.reorderLevel = "Reorder level is required.";
        if (f.unitCost === "") e.unitCost = "Unit cost is required.";
        if (!f.keptInLocationId) e.keptInLocationId = "Choose where it is kept.";
      }
    }
    setErrors(e);
    if (Object.keys(e).length) return;
    const body = kind === 2 ? {
      kind: 2, itemName: f.itemName.trim(), categoryId: Number(f.categoryId), description: f.description || null, brandModel: f.brandModel || null,
      unitCost: Number(f.unitCost), supplierId: numberOrNull(f.supplierId), vendorName: f.vendorName || null, remarks: f.remarks || null,
      codePrefix: f.codePrefix.trim(), quantity: n, serialNumber: n === 1 ? f.serialNumber || null : null, locationId: Number(f.locationId),
      condition: Number(f.condition), purchaseDate: f.purchaseDate, warrantyUntil: f.warrantyUntil || null
    } : {
      kind: 1, itemName: f.itemName.trim(), categoryId: numberOrNull(f.categoryId), description: f.description || null, brandModel: f.brandModel || null,
      unitCost: numberOrNull(f.unitCost), supplierId: numberOrNull(f.supplierId), vendorName: f.vendorName || null, remarks: f.remarks || null,
      itemCode: f.itemCode.trim(), unit: f.unit || (service ? "Nos" : null), reorderLevel: service ? 0 : Number(f.reorderLevel),
      keptInLocationId: service ? null : numberOrNull(f.keptInLocationId), isService: service,
      hsnSacCode: service || f.sold ? f.hsnSacCode || null : null, sellingRate: service || f.sold ? numberOrNull(f.sellingRate) : null,
      taxPercent: service || f.sold ? numberOrNull(f.taxPercent) : null, isNonTaxable: service || f.sold ? f.taxPercent === "" : null
    };
    setBusy(true);
    try {
      const result = await api.createItem(body);
      toast.success(result.codes.length > 1 ? `${result.codes.length} assets created: ${codes?.range ?? result.codes[0]}.` : `${result.codes[0]} created.`);
      onCreated(result, kind === 2 && print);
    } catch (error) {
      const fieldErrors = {};
      (error?.response?.errors ?? []).forEach((x) => { if (x.field) fieldErrors[x.field] = x.message; });
      setErrors(fieldErrors);
      toast.error(errorText(error, "Could not create the item."));
    } finally { setBusy(false); }
  };

  const kindCard = (k, title, text) => h("button", { type: "button", className: `as-kind__opt${kind === k && !service ? " as-kind__opt--on" : ""}`, onClick: () => { setKind(k); setService(false); } },
    h("strong", null, title), h("span", null, text));
  const vendorFields = [
    h(Select, { key: "sup", label: "Vendor", value: f.supplierId, placeholder: "Not in the Suppliers list", options: (options?.suppliers ?? []).map((s) => ({ value: String(s.id), label: s.name })), onChange: set("supplierId") }),
    f.supplierId ? null : h(Input, { key: "vn", label: "Vendor name", value: f.vendorName, onChange: set("vendorName"), placeholder: "Who it was bought from" })
  ];

  return h(Dialog, {
    title: "New item", onClose, wide: kind === 2,
    footer: h(Frag, null,
      kind === 2 ? h("span", { className: "as-grow" }, n > 0 && totalCost != null ? `${n} asset${n === 1 ? "" : "s"}, ${money(totalCost)} in total` : "") : null,
      kind === 2 ? h("label", { style: { display: "inline-flex", gap: 8, alignItems: "center", fontSize: "var(--font-size-sm)" } },
        h("input", { type: "checkbox", checked: print, onChange: (ev) => setPrint(ev.target.checked) }), "Print QR labels straight away") : null,
      h(Button, { variant: "ghost", onClick: onClose }, "Cancel"),
      h(Button, { onClick: submit, loading: busy }, kind === 2 ? (n > 1 ? `Create ${n} assets` : "Create asset") : "Create item"))
  },
    h("div", { className: "as-kind" },
      h("div", { className: "as-kind__q" }, "What kind of item is this? ", h("span", { className: "field__required" }, "*")),
      h("div", { className: "as-kind__opts" },
        kindCard(2, "Asset", "One physical thing. Gets its own code and QR sticker."),
        kindCard(1, "Consumable", "A quantity that is used up. No sticker.")),
      h("div", { className: "as-sub", style: { marginTop: 8 } }, "It cannot be changed afterwards. ",
        kind === 1 ? h("button", { type: "button", className: "as-link", onClick: () => setService(!service) }, service ? "Back to a consumable" : "Adding a service that is only sold on quotations?") : null)),
    kind === 2 ? h("div", { className: "as-form" },
      h(Input, { label: "Item name", required: true, value: f.itemName, error: errors.itemName, onChange: set("itemName"), placeholder: "Air Conditioner 2.0 TR" }),
      h(Select, { label: "Category", required: true, value: f.categoryId, error: errors.categoryId, placeholder: "Choose", options: sorted.map((c) => ({ value: String(c.assetCategoryId), label: c.categoryName })), onChange: onCategory }),
      h(Input, { label: "Code", required: true, value: f.codePrefix, error: errors.codePrefix, onChange: (ev) => { setPrefixTouched(true); set("codePrefix")(ev); },
        hint: codes ? `Next: ${codes.codes[0]}. Suggested from the category; the last three digits are given out one at a time and never reused.` : "The prefix, e.g. R-2W-E-EAC. The running number is added for you." }),
      h(Input, { label: "How many are you adding?", required: true, type: "number", min: 1, max: 500, value: f.quantity, error: errors.quantity, onChange: set("quantity"),
        hint: n > 1 ? `Creates ${n} separate records${codes ? `: ${codes.range.replace(codes.prefix, "")}` : ""} — not one record with a quantity of ${n}.` : "One record, one code, one sticker." }),
      h(Input, { label: "Brand / model", value: f.brandModel, onChange: set("brandModel"), hint: "Applies to all of them." }),
      h(Input, { label: "Serial number", value: n > 1 ? "" : f.serialNumber, disabled: n > 1, onChange: set("serialNumber"), hint: "Per unit. Fill in later from the item page if you do not have them now." }),
      h(Select, { label: "Location", required: true, value: f.locationId, error: errors.locationId, placeholder: "Choose the room", options: roomOptions(options), onChange: set("locationId") }),
      h(Select, { label: "Condition", value: f.condition, options: Object.entries(CONDITION).map(([v, l]) => ({ value: v, label: l })), onChange: set("condition") }),
      h(Input, { label: "Purchase date", required: true, type: "date", max: todayIst(), value: f.purchaseDate, error: errors.purchaseDate, onChange: set("purchaseDate") }),
      h(Input, { label: "Purchase cost each (₹)", required: true, type: "number", min: 0, step: "0.01", value: f.unitCost, error: errors.unitCost, onChange: set("unitCost"), hint: "Per unit, not the total. Before GST." }),
      ...vendorFields,
      h(Input, { label: "Warranty until", type: "date", value: f.warrantyUntil, onChange: set("warrantyUntil") }),
      h("div", { className: "as-span2" }, h(TextArea, { label: "Description", value: f.description, onChange: (v) => setF((s) => ({ ...s, description: v })), placeholder: "Blue Star make 2.0 TR inverter, with copper piping" })),
      h("div", { className: "as-span2" }, h(Input, { label: "Remarks", value: f.remarks, onChange: set("remarks") })),
      h("div", { className: "as-span2 as-note as-note--blue" }, "Adding assets here posts an Incoming Goods receipt for them — every asset is born from a receipt, with a movement behind it."))
      : h("div", { className: "as-form" },
        h("div", { className: "as-span2" }, h(Input, { label: "Item name", required: true, value: f.itemName, error: errors.itemName, onChange: set("itemName"), placeholder: service ? "Meeting room setup" : "Coffee" })),
        h(Input, { label: "Code", required: true, value: f.itemCode, error: errors.itemCode, onChange: set("itemCode"), placeholder: service ? "SVC-01" : "COFFEE-07" }),
        h(Select, { label: "Category", required: !service, value: f.categoryId, error: errors.categoryId, placeholder: service ? "None" : "Choose", options: sorted.map((c) => ({ value: String(c.assetCategoryId), label: c.categoryName })), onChange: set("categoryId") }),
        service ? null : h(Input, { label: "Unit", required: true, type: "number", min: 0, step: "any", inputMode: "decimal", value: f.unit, error: errors.unit, onChange: set("unit"), hint: "Numbers only. Shown everywhere beside the quantity." }),
        service ? null : h(Input, { label: "Reorder level", required: true, type: "number", min: 0, step: "0.001", value: f.reorderLevel, error: errors.reorderLevel, onChange: set("reorderLevel"), hint: "At or below this it is flagged red everywhere." }),
        service ? null : h(Input, { label: "Unit cost (₹)", required: true, type: "number", min: 0, step: "0.01", value: f.unitCost, error: errors.unitCost, onChange: set("unitCost") }),
        service ? null : h(Select, { label: "Kept in", required: true, value: f.keptInLocationId, error: errors.keptInLocationId, placeholder: "Choose", options: roomOptions(options), onChange: set("keptInLocationId") }),
        ...(service ? [] : vendorFields),
        service || f.sold ? h(Input, { label: "HSN / SAC code", value: f.hsnSacCode, onChange: set("hsnSacCode") }) : null,
        service || f.sold ? h(Input, { label: "Selling rate (₹)", type: "number", min: 0, step: "0.01", value: f.sellingRate, onChange: set("sellingRate") }) : null,
        service || f.sold ? h(Input, { label: "GST %", type: "number", min: 0, max: 100, value: f.taxPercent, onChange: set("taxPercent"), hint: "Leave empty for non-taxable." }) : null,
        service ? null : h("label", { className: "as-span2", style: { display: "flex", gap: 8, alignItems: "center", fontSize: "var(--font-size-sm)" } },
          h("input", { type: "checkbox", checked: f.sold, onChange: set("sold") }), "Also sold on quotations (HSN, selling rate, GST)"),
        h("div", { className: "as-span2" }, h(TextArea, { label: "Description", value: f.description, onChange: (v) => setF((s) => ({ ...s, description: v })) })),
        service ? h("div", { className: "as-span2 as-note" }, "A service is only sold on quotations. It holds no stock and never appears on Current Stock.")
          : h("div", { className: "as-span2 as-note as-note--blue" }, h("strong", null, "No opening quantity here. "),
            "Stock only ever comes from the movement ledger. To put the first stock in, record it on Incoming Goods as an opening balance.")));
}

function EditItemDialog({ item, options, onClose, onSaved }) {
  const toast = useToast();
  const isAsset = item.kind === 2;
  const [f, setF] = React.useState({
    // GET /admin/items/{id} can return categoryName with a null categoryId; match by name then.
    itemName: item.itemName, categoryId: item.categoryId ? String(item.categoryId) : String(options?.categories?.find((c) => c.categoryName === item.categoryName)?.assetCategoryId ?? ""), description: item.description ?? "", brandModel: item.brandModel ?? "",
    unitCost: item.unitCost ?? "", supplierId: item.supplierId ? String(item.supplierId) : "", vendorName: item.vendorName ?? "", remarks: item.remarks ?? "",
    serialNumber: item.serialNumber ?? "", condition: item.condition ? String(item.condition) : "1", purchaseDate: item.purchaseDate ?? "", warrantyUntil: item.warrantyUntil ?? "",
    unit: unitNumber(item.unit), reorderLevel: item.reorderLevel ?? "", keptInLocationId: item.keptInLocationId ? String(item.keptInLocationId) : "",
    hsnSacCode: item.hsnSacCode ?? "", sellingRate: item.sellingRate || "", taxPercent: item.isNonTaxable ? "" : item.taxPercent
  });
  const [busy, setBusy] = React.useState(false);
  const [serialWarning, setSerialWarning] = React.useState(null);
  const set = (k) => (ev) => setF((s) => ({ ...s, [k]: ev.target.value }));
  const serial = useDebounced(f.serialNumber.trim(), 400);
  React.useEffect(() => {
    if (!isAsset || !serial || serial === (item.serialNumber ?? "")) { setSerialWarning(null); return; }
    api.serialCheck(serial, item.inventoryItemId).then((r) => setSerialWarning(r.duplicate ? `This serial number is already on ${r.existingCode}.` : null)).catch(() => {});
  }, [serial]);
  const save = async () => {
    setBusy(true);
    try {
      const sold = item.isService || f.hsnSacCode || f.sellingRate;
      const result = await api.updateItem(item.inventoryItemId, {
        itemName: f.itemName, categoryId: numberOrNull(f.categoryId), description: f.description || null, brandModel: f.brandModel || null,
        unitCost: numberOrNull(f.unitCost), supplierId: numberOrNull(f.supplierId), vendorName: f.supplierId ? null : f.vendorName || null, remarks: f.remarks || null,
        serialNumber: isAsset ? f.serialNumber || null : null, condition: isAsset ? Number(f.condition) : null,
        purchaseDate: isAsset ? f.purchaseDate || null : null, warrantyUntil: isAsset ? f.warrantyUntil || null : null,
        unit: isAsset ? null : f.unit === "" ? item.unit : f.unit, reorderLevel: isAsset ? null : numberOrNull(f.reorderLevel), keptInLocationId: isAsset ? null : numberOrNull(f.keptInLocationId),
        hsnSacCode: isAsset ? null : f.hsnSacCode, sellingRate: isAsset || !sold ? null : numberOrNull(f.sellingRate),
        taxPercent: isAsset || !sold ? null : numberOrNull(f.taxPercent), isNonTaxable: isAsset || !sold ? null : f.taxPercent === ""
      });
      result.warnings.forEach((w) => toast.info ? toast.info(w) : toast.success(w));
      toast.success("Saved.");
      onSaved();
    } catch (error) { toast.error(errorText(error, "Could not save.")); } finally { setBusy(false); }
  };
  return h(Dialog, {
    title: `Edit — ${item.itemCode}`, onClose,
    footer: h(Frag, null, h("span", { className: "as-grow" }, "Kind, code, room and status change only through movements."), h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { onClick: save, loading: busy }, "Save"))
  },
    h("div", { className: "as-form" },
      // Shown from the saved item, read-only: the kind is fixed at creation (New item says
      // "It cannot be changed afterwards") and PUT /admin/items/{id} takes no kind.
      h(Input, { label: "Entry kind", value: item.isService ? "Service (sold on quotations)" : isAsset ? "Asset" : "Consumable", readOnly: true, disabled: true,
        hint: "Chosen when the item was created; it cannot be changed afterwards." }),
      h("div", null),
      h("div", { className: "as-span2" }, h(Input, { label: "Item name", required: true, value: f.itemName, onChange: set("itemName") })),
      h(Select, { label: "Category", value: f.categoryId, placeholder: item.isService ? "None" : "Choose", options: (options?.categories ?? []).map((c) => ({ value: String(c.assetCategoryId), label: c.categoryName })), onChange: set("categoryId") }),
      h(Input, { label: "Brand / model", value: f.brandModel, onChange: set("brandModel") }),
      isAsset ? h(Input, { label: "Serial number", value: f.serialNumber, onChange: set("serialNumber"), error: serialWarning ?? undefined, hint: "A duplicate warns but is allowed — cheap items sometimes repeat." }) : null,
      isAsset ? h(Select, { label: "Condition", value: f.condition, options: Object.entries(CONDITION).map(([v, l]) => ({ value: v, label: l })), onChange: set("condition") }) : null,
      isAsset ? h(Input, { label: "Purchase date", type: "date", max: todayIst(), value: f.purchaseDate, onChange: set("purchaseDate") }) : null,
      isAsset ? h(Input, { label: "Warranty until", type: "date", value: f.warrantyUntil, onChange: set("warrantyUntil") }) : null,
      h(Input, { label: isAsset ? "Purchase cost (₹)" : "Unit cost (₹)", type: "number", min: 0, step: "0.01", value: f.unitCost, onChange: set("unitCost") }),
      !isAsset && !item.isService ? h(Input, { label: "Unit", type: "number", min: 0, step: "any", inputMode: "decimal", value: f.unit, onChange: set("unit"),
        hint: f.unit === "" && item.unit ? `Numbers only. Leave blank to keep "${item.unit}".` : "Numbers only." }) : null,
      !isAsset && !item.isService ? h(Input, { label: "Reorder level", type: "number", min: 0, step: "0.001", value: f.reorderLevel, onChange: set("reorderLevel") }) : null,
      !isAsset && !item.isService ? h(Select, { label: "Kept in", value: f.keptInLocationId, placeholder: "Choose", options: roomOptions(options), onChange: set("keptInLocationId") }) : null,
      h(Select, { label: "Vendor", value: f.supplierId, placeholder: "Not in the Suppliers list", options: (options?.suppliers ?? []).map((s) => ({ value: String(s.id), label: s.name })), onChange: set("supplierId") }),
      f.supplierId ? null : h(Input, { label: "Vendor name", value: f.vendorName, onChange: set("vendorName") }),
      !isAsset ? h(Input, { label: "HSN / SAC code", value: f.hsnSacCode, onChange: set("hsnSacCode") }) : null,
      !isAsset ? h(Input, { label: "Selling rate (₹)", type: "number", min: 0, step: "0.01", value: f.sellingRate, onChange: set("sellingRate") }) : null,
      !isAsset ? h(Input, { label: "GST %", type: "number", min: 0, max: 100, value: f.taxPercent, onChange: set("taxPercent"), hint: "Empty = non-taxable." }) : null,
      h("div", { className: "as-span2" }, h(TextArea, { label: "Description", value: f.description, onChange: (v) => setF((s) => ({ ...s, description: v })) })),
      h("div", { className: "as-span2" }, h(Input, { label: "Remarks", value: f.remarks, onChange: set("remarks") }))));
}

/** Moved (§7.2): one movement for every selected asset, written to each history. */
function MoveDialog({ rows, skipped, options, onClose, onDone }) {
  const toast = useToast();
  const [to, setTo] = React.useState("");
  const [date, setDate] = React.useState(todayIst());
  const [note, setNote] = React.useState("");
  const [busy, setBusy] = React.useState(false);
  const submit = async () => {
    if (!to) { toast.error("Choose the room it is going to."); return; }
    setBusy(true);
    try {
      const result = await api.createIssue({ reason: 2, issueDate: date, toLocationId: Number(to), note: note || null, skipInapplicable: true,
        lines: rows.map((r) => ({ inventoryItemId: r.inventoryItemId, quantity: 1 })) });
      toast.success(`${result.issue.issueNumber}: ${result.issue.lines.length} moved.${result.message ? ` ${result.message}` : ""}${skipped ? ` ${skipped} consumable or retired item${skipped === 1 ? " was" : "s were"} skipped.` : ""}`);
      onDone();
    } catch (error) { toast.error(errorText(error, "Could not move them.")); } finally { setBusy(false); }
  };
  return h(Dialog, {
    title: rows.length === 1 ? `Move — ${rows[0].itemCode}` : `Move ${rows.length} assets`, onClose,
    footer: h(Frag, null, h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { onClick: submit, loading: busy }, "Post movement"))
  },
    h("div", { className: "as-form" },
      h(Select, { label: "Destination room", required: true, value: to, placeholder: "Choose", options: roomOptions(options), onChange: (ev) => setTo(ev.target.value) }),
      h(Input, { label: "Date", type: "date", max: todayIst(), value: date, onChange: (ev) => setDate(ev.target.value) }),
      h("div", { className: "as-span2" }, h(Input, { label: "Note", value: note, onChange: (ev) => setNote(ev.target.value) }))),
    h("p", { className: "as-sub" }, "Changes the location and writes the move to each asset's history. An asset away for repair cannot be moved until it is back."));
}

const STATUS_REASONS = [
  [3, "Sent for repair"], [4, "Returned to supplier"], [5, "Scrapped"], [6, "Lost"]
];

/** Change status / Log repair: an Outgoing Goods movement with the reason chosen. */
function StatusDialog({ rows, skipped, fixedReason, onClose, onDone }) {
  const toast = useToast();
  const [reason, setReason] = React.useState(fixedReason ?? 3);
  const [f, setF] = React.useState({ date: todayIst(), repairVendor: "", expectedCost: "", reasonText: "", credit: "", amountRecovered: "", reportedBy: "", note: "" });
  const [busy, setBusy] = React.useState(false);
  const set = (k) => (ev) => setF((s) => ({ ...s, [k]: ev.target.value }));
  const submit = async () => {
    setBusy(true);
    try {
      const result = await api.createIssue({
        reason, issueDate: f.date, skipInapplicable: true, note: f.note || null,
        lines: rows.map((r) => ({ inventoryItemId: r.inventoryItemId, quantity: 1 })),
        repairVendor: reason === 3 ? f.repairVendor : null, expectedCost: reason === 3 ? numberOrNull(f.expectedCost) : null,
        reasonText: reason === 4 || reason === 5 ? f.reasonText : null, creditNoteRaised: reason === 4 ? (f.credit === "" ? null : f.credit === "yes") : null,
        amountRecovered: reason === 5 ? numberOrNull(f.amountRecovered) : null, reportedBy: reason === 6 ? f.reportedBy : null
      });
      toast.success(`${result.issue.issueNumber} posted.${result.message ? ` ${result.message}` : ""}${skipped ? ` ${skipped} consumable or retired item${skipped === 1 ? " was" : "s were"} skipped.` : ""}`);
      onDone();
    } catch (error) { toast.error(errorText(error, "That did not work.")); } finally { setBusy(false); }
  };
  return h(Dialog, {
    title: fixedReason === 3 ? `Log repair — ${rows[0].itemCode}` : rows.length === 1 ? `Change status — ${rows[0].itemCode}` : `Change status of ${rows.length} assets`, onClose,
    footer: h(Frag, null, h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { variant: reason >= 5 ? "danger" : "primary", onClick: submit, loading: busy }, "Post movement"))
  },
    fixedReason ? null : h("div", { className: "as-reasons", style: { marginBottom: "var(--space-4)" } },
      STATUS_REASONS.map(([r, l]) => h("button", { key: r, type: "button", className: `as-reason${reason === r ? " as-reason--on" : ""}`, onClick: () => setReason(r) }, l))),
    h("div", { className: "as-form" },
      h(Input, { label: "Date", type: "date", max: todayIst(), value: f.date, onChange: set("date") }),
      reason === 3 ? h(Input, { label: "Who it went to", required: true, value: f.repairVendor, onChange: set("repairVendor"), placeholder: "Blue Star Service" }) : null,
      reason === 3 ? h(Input, { label: "Expected cost (₹)", type: "number", min: 0, value: f.expectedCost, onChange: set("expectedCost") }) : null,
      reason === 4 || reason === 5 ? h(Input, { label: "Reason", required: true, value: f.reasonText, onChange: set("reasonText"), placeholder: reason === 4 ? "Faulty on arrival" : "Beyond repair" }) : null,
      reason === 4 ? h(Select, { label: "Credit note raised?", value: f.credit, placeholder: "Not known", options: [{ value: "yes", label: "Yes" }, { value: "no", label: "No" }], onChange: set("credit") }) : null,
      reason === 5 ? h(Input, { label: "Amount recovered (₹)", type: "number", min: 0, value: f.amountRecovered, onChange: set("amountRecovered") }) : null,
      reason === 6 ? h(Input, { label: "Who reported it", required: true, value: f.reportedBy, onChange: set("reportedBy") }) : null,
      h("div", { className: "as-span2" }, h(Input, { label: reason === 6 ? "Note" : "Note", required: false, value: f.note, onChange: set("note") }))),
    h("p", { className: "as-sub" }, reason === 3 ? "Sets it to In repair, so it cannot be moved or issued until the repair is closed from its item page."
      : "Takes it out of the register and sets its book value to 0. The record stays, with its full history."));
}

// ── Categories and rooms (§3.4, §3.5) ───────────────────────────────────────

function SetupDialog({ onClose }) {
  const [tab, setTab] = React.useState("categories");
  return h(Dialog, { title: "Categories & rooms", onClose, wide: true, footer: h(Button, { variant: "ghost", onClick: onClose }, "Done") },
    h("div", { className: "as-tabs" },
      h("button", { type: "button", className: `as-tab${tab === "categories" ? " as-tab--on" : ""}`, onClick: () => setTab("categories") }, "Categories"),
      h("button", { type: "button", className: `as-tab${tab === "rooms" ? " as-tab--on" : ""}`, onClick: () => setTab("rooms") }, "Rooms")),
    tab === "categories" ? h(CategoriesEditor) : h(RoomsEditor));
}

function CategoriesEditor() {
  const toast = useToast();
  const [list, setList] = React.useState(null);
  const [edit, setEdit] = React.useState(null);
  const load = () => api.categories().then(setList).catch((e) => toast.error(errorText(e, "Could not load the categories.")));
  React.useEffect(() => { load(); }, []);
  const save = async () => {
    try {
      await api.saveCategory(edit.assetCategoryId, { categoryName: edit.categoryName, defaultKind: Number(edit.defaultKind), codePrefix: edit.codePrefix,
        usefulLifeYears: numberOrNull(edit.usefulLifeYears), displayOrder: numberOrNull(edit.displayOrder), isActive: edit.isActive });
      toast.success("Category saved.");
      setEdit(null);
      load();
    } catch (error) { toast.error(errorText(error, "Could not save the category.")); }
  };
  if (!list) return h(Spinner, { center: true });
  return h("div", null,
    h("p", { className: "as-sub", style: { marginTop: 0 } }, "Adding a category never needs a code change. The default kind and prefix only pre-fill the item form; useful life drives book value."),
    h("div", { className: "as-panel as-table-wrap" }, h("table", { className: "as-table" },
      h("thead", null, h("tr", null, h("th", null, "Category"), h("th", null, "Default kind"), h("th", null, "Code prefix"), h("th", { className: "num" }, "Useful life"), h("th", { className: "num" }, "Items"), h("th", null, ""), h("th", null, ""))),
      h("tbody", null, list.map((c) => h("tr", { key: c.assetCategoryId, className: c.isActive ? undefined : "as-row--muted" },
        h("td", { className: "as-cell-main" }, c.categoryName), h("td", null, h(KindTag, { kind: c.defaultKind })), h("td", { className: "as-mono" }, c.codePrefix),
        h("td", { className: "num" }, c.usefulLifeYears ? `${qty(c.usefulLifeYears)} years` : "—"), h("td", { className: "num" }, c.itemCount),
        h("td", null, c.isActive ? "" : "Inactive"),
        h("td", { className: "num" }, h("button", { type: "button", className: "as-link", onClick: () => setEdit({ ...c, defaultKind: String(c.defaultKind), usefulLifeYears: c.usefulLifeYears ?? "" }) }, "Edit"))))))),
    edit ? h(SetupForm, { title: edit.assetCategoryId ? `Edit ${edit.categoryName}` : "New category", onCancel: () => setEdit(null), onSave: save },
      h(Input, { label: "Name", required: true, value: edit.categoryName, onChange: (ev) => setEdit({ ...edit, categoryName: ev.target.value }) }),
      h(Select, { label: "Default kind", value: edit.defaultKind, options: [{ value: "2", label: "Asset" }, { value: "1", label: "Consumable" }], onChange: (ev) => setEdit({ ...edit, defaultKind: ev.target.value }) }),
      h(Input, { label: "Code prefix", required: true, value: edit.codePrefix, onChange: (ev) => setEdit({ ...edit, codePrefix: ev.target.value }) }),
      h(Input, { label: "Useful life (years)", type: "number", min: 0.5, step: "0.5", value: edit.usefulLifeYears, onChange: (ev) => setEdit({ ...edit, usefulLifeYears: ev.target.value }), hint: "Empty: not depreciated." }),
      h("label", { style: { display: "flex", gap: 8, alignItems: "center" } }, h("input", { type: "checkbox", checked: edit.isActive, onChange: (ev) => setEdit({ ...edit, isActive: ev.target.checked }) }), "Active"))
      : h("div", { style: { marginTop: "var(--space-3)" } }, h(Button, { variant: "outline", onClick: () => setEdit({ assetCategoryId: null, categoryName: "", defaultKind: "2", codePrefix: "", usefulLifeYears: "", isActive: true }) }, "+ New category")));
}

function RoomsEditor() {
  const toast = useToast();
  const [list, setList] = React.useState(null);
  const [workspaces, setWorkspaces] = React.useState([]);
  const [edit, setEdit] = React.useState(null);
  const load = () => api.locations().then(setList).catch((e) => toast.error(errorText(e, "Could not load the rooms.")));
  React.useEffect(() => { load(); api.workspaces().then((r) => setWorkspaces(r.items ?? r)).catch(() => {}); }, []);
  const save = async () => {
    try {
      await api.saveLocation(edit.assetLocationId, { locationName: edit.locationName, workspaceId: numberOrNull(edit.workspaceId), isStore: edit.isStore, displayOrder: null, isActive: edit.isActive });
      toast.success("Room saved.");
      setEdit(null);
      load();
    } catch (error) { toast.error(errorText(error, "Could not save the room.")); }
  };
  if (!list) return h(Spinner, { center: true });
  return h("div", null,
    h("p", { className: "as-sub", style: { marginTop: 0 } }, "Rooms inside the building. A room that is also a workspace links to it, so “what is in Private Cabin 3” can be answered from either side. A store room makes things In store rather than In use."),
    h("div", { className: "as-panel as-table-wrap" }, h("table", { className: "as-table" },
      h("thead", null, h("tr", null, h("th", null, "Room"), h("th", null, "Workspace"), h("th", null, "Store"), h("th", { className: "num" }, "Assets"), h("th", null, ""), h("th", null, ""))),
      h("tbody", null, list.map((l) => h("tr", { key: l.assetLocationId, className: l.isActive ? undefined : "as-row--muted" },
        h("td", { className: "as-cell-main" }, l.locationName), h("td", null, l.workspaceName ?? "—"), h("td", null, l.isStore ? "Yes" : ""), h("td", { className: "num" }, l.assetCount),
        h("td", null, l.isActive ? "" : "Inactive"),
        h("td", { className: "num" }, h("button", { type: "button", className: "as-link", onClick: () => setEdit({ ...l, workspaceId: l.workspaceId ? String(l.workspaceId) : "" }) }, "Edit"))))))),
    edit ? h(SetupForm, { title: edit.assetLocationId ? `Edit ${edit.locationName}` : "New room", onCancel: () => setEdit(null), onSave: save },
      h(Input, { label: "Name", required: true, value: edit.locationName, onChange: (ev) => setEdit({ ...edit, locationName: ev.target.value }) }),
      h(Select, { label: "Same place as workspace", value: edit.workspaceId, placeholder: "Not a workspace", options: workspaces.map((w) => ({ value: String(w.workspaceId), label: w.workspaceName })), onChange: (ev) => setEdit({ ...edit, workspaceId: ev.target.value }) }),
      h("label", { style: { display: "flex", gap: 8, alignItems: "center" } }, h("input", { type: "checkbox", checked: edit.isStore, onChange: (ev) => setEdit({ ...edit, isStore: ev.target.checked }) }), "This is a store room"),
      h("label", { style: { display: "flex", gap: 8, alignItems: "center" } }, h("input", { type: "checkbox", checked: edit.isActive, onChange: (ev) => setEdit({ ...edit, isActive: ev.target.checked }) }), "Active"))
      : h("div", { style: { marginTop: "var(--space-3)" } }, h(Button, { variant: "outline", onClick: () => setEdit({ assetLocationId: null, locationName: "", workspaceId: "", isStore: false, isActive: true }) }, "+ New room")));
}

function SetupForm({ title, onCancel, onSave, children }) {
  const [busy, setBusy] = React.useState(false);
  return h("div", { className: "as-panel as-panel__pad", style: { marginTop: "var(--space-4)" } },
    h("h4", { style: { margin: "0 0 var(--space-3)" } }, title),
    h("div", { className: "as-form" }, children),
    h("div", { style: { display: "flex", gap: 8, justifyContent: "flex-end", marginTop: "var(--space-3)" } },
      h(Button, { variant: "ghost", onClick: onCancel }, "Cancel"),
      h(Button, { loading: busy, onClick: async () => { setBusy(true); try { await onSave(); } finally { setBusy(false); } } }, "Save")));
}
