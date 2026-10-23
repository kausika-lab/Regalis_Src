/**
 * Admin -> Assets -> Current Stock  (/admin/assets/stock) — Assets spec §8.
 *
 * What Regalis holds right now. Every figure is worked out on the server from the
 * movement ledger and stamped with the time it was worked out; nothing on this page is
 * stored or can be typed. A wrong figure is corrected by a stock count, which writes
 * one adjustment movement per difference.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js lazy-loads this
 * file for /admin/assets/stock. Shared pieces live in AssetsCommon-AS1.js.
 */
import { a as useToast, r as React, R as usePermission, u as useNavigate, p as Button, F as Select, I as Input, G as Spinner } from "./index-DI5X4c9f.js";
import {
  h, Frag, api, money, qty, fmtDay, fmtLongDay, fmtAsAt, fmtWhen, todayIst, errorText, useDebounced, useAssetStyles,
  Pill, SummaryCard, SkeletonRows, LoadFailed, Dialog, IconSearch, IconChevronLeft, IconChevronRight
} from "./AssetsCommon-AS1.js";

const monthStart = (offset = 0) => {
  const t = todayIst();
  const d = new Date(Number(t.slice(0, 4)), Number(t.slice(5, 7)) - 1 + offset, 1);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-01`;
};
const monthEnd = (offset) => {
  const d = new Date(Number(monthStart(offset + 1).slice(0, 4)), Number(monthStart(offset + 1).slice(5, 7)) - 1, 0);
  return `${d.getFullYear()}-${String(d.getMonth() + 1).padStart(2, "0")}-${String(d.getDate()).padStart(2, "0")}`;
};
const PERIODS = {
  month: { label: "This month", from: () => monthStart(0), to: () => todayIst() },
  last: { label: "Last month", from: () => monthStart(-1), to: () => monthEnd(-1) },
  quarter: { label: "Last 3 months", from: () => monthStart(-2), to: () => todayIst() },
  custom: { label: "Choose dates…" }
};
const weeksAgo = (d) => {
  if (!d) return "never counted";
  const days = Math.round((Date.parse(`${todayIst()}T00:00:00Z`) - Date.parse(`${d}T00:00:00Z`)) / 86400000);
  return days === 0 ? "today" : days < 14 ? `${days} day${days === 1 ? "" : "s"} ago` : days < 60 ? `${Math.round(days / 7)} weeks ago` : `${Math.round(days / 30)} months ago`;
};

export function AssetStockPage() {
  useAssetStyles();
  const toast = useToast();
  const can = { count: usePermission("INVENTORY.COUNT"), export: usePermission("INVENTORY.EXPORT") };
  const [tab, setTab] = React.useState("consumables");
  const [period, setPeriod] = React.useState("month");
  const [custom, setCustom] = React.useState({ from: monthStart(0), to: todayIst() });
  const [sheet, setSheet] = React.useState(null);
  const [failed, setFailed] = React.useState(false);
  const [counting, setCounting] = React.useState(null);
  const [counts, setCounts] = React.useState([]);

  const range = period === "custom" ? custom : { from: PERIODS[period].from(), to: PERIODS[period].to() };
  const load = React.useCallback(async () => {
    setFailed(false);
    try { setSheet(await api.stock(range)); } catch (error) { setFailed(true); toast.error(errorText(error, "Could not work out the stock.")); }
  }, [range.from, range.to]);
  React.useEffect(() => { load(); }, [load]);
  const loadCounts = () => api.counts().then(setCounts).catch(() => {});
  React.useEffect(() => { loadCounts(); }, []);
  const open = counts.find((c) => c.status === 1 || c.status === 2);

  return h("div", { className: "as-page" },
    h("div", { className: "as-head" },
      h("div", null, h("h2", null, "Current Stock"),
        h("p", null, "What Regalis holds right now, as at ", h("strong", null, sheet ? fmtAsAt(sheet.asAtUtc) : "…"),
          ". Every figure is opening balance plus everything received minus everything issued. Nothing on this page can be typed in.")),
      h("div", { className: "as-head__actions" },
        can.export ? h(Button, { variant: "outline", onClick: async () => { if (!(await api.exportStock(range))) toast.error("There are no consumables yet."); } }, "Export Excel") : null,
        can.count ? h(Button, { variant: "outline", onClick: () => setCounting(open ? { id: open.stockCountId } : { start: true }) }, open ? `Continue stock count ${open.countNumber}` : "Start a stock count") : null)),
    h("div", { className: "as-tabs", role: "tablist" },
      [["consumables", "Consumables"], ["locations", "Assets by location"], ["ledger", "Movement ledger"]].map(([k, l]) =>
        h("button", { key: k, type: "button", role: "tab", "aria-selected": tab === k, className: `as-tab${tab === k ? " as-tab--on" : ""}`, onClick: () => setTab(k) }, l))),
    h("div", { className: "as-cards as-cards--4" },
      h(SummaryCard, { label: "Value on hand", value: sheet ? money(sheet.valueOnHand) : "…", sub: "consumables only" }),
      h(SummaryCard, { label: "Below reorder", value: sheet ? sheet.belowReorderCount : "…", tone: sheet?.belowReorderCount ? "red" : undefined, sub: sheet?.belowReorderCount ? "order these today" : "nothing to order" }),
      h(SummaryCard, { label: "Movements this month", value: sheet ? sheet.movementsThisMonth : "…", sub: sheet ? `${sheet.inThisMonth} in · ${sheet.outThisMonth} out` : "" }),
      h(SummaryCard, { label: "Last stock count", value: sheet ? (sheet.lastCountDate ? fmtDay(sheet.lastCountDate).replace(/ \d{4}$/, "") : "Never") : "…", sub: sheet ? weeksAgo(sheet.lastCountDate) : "", tone: sheet && !sheet.lastCountDate ? "gold" : undefined })),
    failed ? h("div", { className: "as-panel" }, h(LoadFailed, { onRetry: load }))
      : tab === "consumables" ? h(ConsumablesTab, { sheet, period, setPeriod, custom, setCustom })
        : tab === "locations" ? h(LocationsTab)
          : h(LedgerTab),
    tab === "consumables" ? h("div", { className: "as-note as-note--red" }, h("strong", null, "If a figure looks wrong: "),
      "start a stock count — write what is actually on the shelf, and the app writes the difference as one adjustment movement with a reason. Nobody ever edits the on-hand number directly.") : null,
    counting ? h(StockCountDialog, { start: counting.start, id: counting.id, onClose: () => { setCounting(null); loadCounts(); load(); } }) : null);
}

function ConsumablesTab({ sheet, period, setPeriod, custom, setCustom }) {
  return h("div", { className: "as-panel" },
    h("div", { className: "as-filters" },
      h(Select, { label: "Period", value: period, options: Object.entries(PERIODS).map(([v, p]) => ({ value: v, label: p.label })), onChange: (ev) => setPeriod(ev.target.value) }),
      period === "custom" ? h(Input, { label: "From", type: "date", value: custom.from, max: custom.to, onChange: (ev) => setCustom({ ...custom, from: ev.target.value }) }) : null,
      period === "custom" ? h(Input, { label: "To", type: "date", value: custom.to, max: todayIst(), onChange: (ev) => setCustom({ ...custom, to: ev.target.value }) }) : null,
      sheet ? h("span", { className: "as-sub", style: { paddingBottom: 10 } }, `Opening is the balance on ${fmtLongDay(sheet.from)}; On hand is at the end of ${fmtLongDay(sheet.to)}.`) : null),
    h("div", { className: "as-table-wrap" }, h("table", { className: "as-table" },
      h("thead", null, h("tr", null, h("th", null, "Item"), h("th", null, "Kept in"), h("th", { className: "num" }, "Opening"), h("th", { className: "num" }, "In"), h("th", { className: "num" }, "Out"),
        h("th", { className: "num" }, "On hand"), h("th", { className: "num" }, "Reorder at"), h("th", { className: "num" }, "Value"), h("th", null, "Flag"))),
      !sheet ? h(SkeletonRows, { rows: 6, cols: 9 })
        : sheet.rows.length === 0 ? h("tbody", null, h("tr", null, h("td", { colSpan: 9 }, h("div", { className: "as-empty" }, h("strong", null, "No consumables yet."), "Add one on Items, then bring its first stock in on Incoming Goods."))))
          : h("tbody", null, sheet.rows.map((r) => h("tr", { key: r.inventoryItemId, className: r.orderNow ? "as-row--alert" : undefined },
            h("td", null, h("div", { className: "as-cell-main" }, r.itemName), h("div", { className: "as-cell-sub as-mono" }, `${r.itemCode} · ${r.unit}`)),
            h("td", null, r.keptIn ?? "—"),
            h("td", { className: "num" }, qty(r.opening)),
            h("td", { className: "num as-in" }, `+${qty(r.in)}`),
            h("td", { className: "num as-out" }, `−${qty(r.out)}`),
            h("td", { className: "num" }, h("strong", null, qty(r.onHand))),
            h("td", { className: "num" }, qty(r.reorderLevel)),
            h("td", { className: "num" }, r.value == null ? "—" : money(r.value)),
            h("td", null, r.orderNow ? h(Pill, { cls: "order" }, "Order now") : h(Pill, { cls: "fine" }, "Fine"))))),
      sheet && sheet.rows.length ? h("tfoot", null, h("tr", null,
        h("td", { colSpan: 7 }, `Total across all ${sheet.rows.length} consumable${sheet.rows.length === 1 ? "" : "s"}`),
        h("td", { className: "num" }, money(sheet.valueOnHand)), h("td", null, ""))) : null)),
    h("div", { className: "as-footer" }, "Opening, In and Out show the working: On hand = Opening + In − Out. An admin who doubts the figure can see the arithmetic without opening anything."));
}

function LocationsTab() {
  const navigate = useNavigate();
  const [rows, setRows] = React.useState(null);
  const [failed, setFailed] = React.useState(false);
  const load = () => { setFailed(false); api.assetsByLocation().then(setRows).catch(() => setFailed(true)); };
  React.useEffect(() => { load(); }, []);
  if (failed) return h("div", { className: "as-panel" }, h(LoadFailed, { onRetry: load }));
  const totals = rows ? rows.reduce((t, r) => ({ n: t.n + r.assetCount, v: t.v + r.bookValue, p: t.p + r.inRepair }), { n: 0, v: 0, p: 0 }) : null;
  return h("div", { className: "as-panel" },
    h("div", { className: "as-table-wrap" }, h("table", { className: "as-table" },
      h("thead", null, h("tr", null, h("th", null, "Room"), h("th", null, "Workspace"), h("th", { className: "num" }, "Assets"), h("th", { className: "num" }, "Worth today"), h("th", { className: "num" }, "Bought for"), h("th", { className: "num" }, "In repair"), h("th", { className: "num" }, "Cost missing"))),
      !rows ? h(SkeletonRows, { rows: 6, cols: 7 })
        : rows.length === 0 ? h("tbody", null, h("tr", null, h("td", { colSpan: 7 }, h("div", { className: "as-empty" }, h("strong", null, "No assets in the register yet.")))))
          : h("tbody", null, rows.map((r) => h("tr", { key: r.locationId ?? r.locationName, className: "as-row--click", title: "Open Items filtered to this room", onClick: () => navigate(`/admin/assets/items?kind=2&locationId=${r.locationId}`) },
            h("td", { className: "as-cell-main" }, r.locationName), h("td", null, r.workspaceName ?? "—"),
            h("td", { className: "num" }, r.assetCount), h("td", { className: "num" }, h("strong", null, money(Math.round(r.bookValue)))),
            h("td", { className: "num" }, money(r.purchaseValue)), h("td", { className: "num" }, r.inRepair || "—"),
            h("td", { className: `num${r.costMissing ? " as-costless" : ""}` }, r.costMissing || "—")))),
      totals && rows.length ? h("tfoot", null, h("tr", null, h("td", { colSpan: 2 }, `${rows.length} room${rows.length === 1 ? "" : "s"}`), h("td", { className: "num" }, totals.n),
        h("td", { className: "num" }, money(Math.round(totals.v))), h("td", null, ""), h("td", { className: "num" }, totals.p || "—"), h("td", null, ""))) : null)),
    h("div", { className: "as-footer" }, "One row per room. There is no “on hand” number for assets — each is counted individually. Click a room to see its items."));
}

function LedgerTab() {
  const toast = useToast();
  const [searchText, setSearchText] = React.useState("");
  const search = useDebounced(searchText, 350);
  const [kind, setKind] = React.useState("");
  const [from, setFrom] = React.useState("");
  const [to, setTo] = React.useState("");
  const [page, setPage] = React.useState(1);
  const [result, setResult] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [failed, setFailed] = React.useState(false);
  const load = React.useCallback(async () => {
    setLoading(true); setFailed(false);
    try { setResult(await api.ledger({ search: search || undefined, kind: kind || undefined, from: from || undefined, to: to || undefined, page, pageSize: 50 })); }
    catch (error) { setFailed(true); toast.error(errorText(error, "Could not load the ledger.")); }
    finally { setLoading(false); }
  }, [search, kind, from, to, page]);
  React.useEffect(() => { load(); }, [load]);
  React.useEffect(() => { setPage(1); }, [search, kind, from, to]);
  return h("div", { className: "as-panel" },
    h("div", { className: "as-filters" },
      h("div", { className: "as-search" }, h(IconSearch, { size: 16 }),
        h("input", { className: "field__input", type: "search", value: searchText, placeholder: "Search item, code or document…", "aria-label": "Search the ledger", onChange: (ev) => setSearchText(ev.target.value) })),
      h(Select, { label: "Kind", value: kind, placeholder: "All kinds", options: [{ value: "1", label: "Consumables" }, { value: "2", label: "Assets" }], onChange: (ev) => setKind(ev.target.value) }),
      h(Input, { label: "From", type: "date", value: from, onChange: (ev) => setFrom(ev.target.value) }),
      h(Input, { label: "To", type: "date", value: to, onChange: (ev) => setTo(ev.target.value) })),
    failed ? h(LoadFailed, { onRetry: load })
      : h("div", { className: "as-table-wrap" }, h("table", { className: "as-table" },
        h("thead", null, h("tr", null, h("th", null, "Date"), h("th", null, "Item"), h("th", null, "What"), h("th", null, "Document"), h("th", { className: "num" }, "Change"), h("th", { className: "num" }, "Balance"), h("th", null, "Room"), h("th", null, "By"))),
        loading && !result ? h(SkeletonRows, { rows: 8, cols: 8 })
          : result.items.length === 0 ? h("tbody", null, h("tr", null, h("td", { colSpan: 8 }, h("div", { className: "as-empty" }, h("strong", null, "No movements match.")))))
            : h("tbody", { style: { opacity: loading ? 0.6 : 1 } }, result.items.map((r) => h("tr", { key: r.inventoryTransactionId },
              h("td", null, fmtDay(r.transactionDate), h("div", { className: "as-cell-sub" }, fmtWhen(r.createdDate))),
              h("td", null, h("div", { className: "as-cell-main" }, r.itemName), h("div", { className: "as-cell-sub as-mono" }, r.itemCode)),
              h("td", null, r.what),
              h("td", { className: "as-mono" }, r.documentNumber ?? r.transactionNumber),
              h("td", { className: `num ${r.change < 0 ? "as-out" : "as-in"}` }, `${r.change > 0 ? "+" : "−"}${qty(Math.abs(r.change))}`),
              h("td", { className: "num" }, h("strong", null, `${qty(r.balance)} ${r.kind === 2 ? "" : r.unit}`)),
              h("td", null, r.locationName ?? "—"),
              h("td", null, r.enteredByName ?? "—")))))),
    result && result.totalCount > 0 ? h("div", { className: "as-footer" },
      h("span", null, `Page ${result.page} of ${result.totalPages} · ${result.totalCount} movements · the balance beside each row is that item's stock right after it`),
      h("div", { className: "as-pager" },
        h(Button, { variant: "outline", size: "sm", disabled: !result.hasPrevious || loading, onClick: () => setPage(page - 1), "aria-label": "Previous page" }, h(IconChevronLeft, { size: 16 })),
        h(Button, { variant: "outline", size: "sm", disabled: !result.hasNext || loading, onClick: () => setPage(page + 1), "aria-label": "Next page" }, h(IconChevronRight, { size: 16 })))) : null);
}

// ── Stock count (§8.4) ──────────────────────────────────────────────────────

/** Start → the counter writes what is on the shelf (the recorded figure stays hidden)
 *  → submit shows the differences → confirm writes one adjustment per difference. */
function StockCountDialog({ start, id, onClose }) {
  const toast = useToast();
  const [count, setCount] = React.useState(null);
  const [options, setOptions] = React.useState(null);
  const [setup, setSetup] = React.useState({ locationId: "", countedBy: "", countDate: todayIst() });
  const [counted, setCounted] = React.useState({});
  const [busy, setBusy] = React.useState(false);
  React.useEffect(() => {
    if (start) api.options().then(setOptions).catch(() => {});
    else api.count(id).then((c) => { setCount(c); setCounted(Object.fromEntries(c.lines.filter((l) => l.countedQuantity != null).map((l) => [l.inventoryItemId, String(l.countedQuantity)]))); })
      .catch((e) => toast.error(errorText(e, "Could not open the count.")));
  }, []);
  const act = async (work, success) => {
    setBusy(true);
    try { const c = await work(); setCount(c); if (success) toast.success(success); return c; }
    catch (error) { toast.error(errorText(error, "That did not work.")); } finally { setBusy(false); }
  };
  const begin = () => act(() => api.startCount({ countDate: setup.countDate, locationId: setup.locationId ? Number(setup.locationId) : null, countedBy: setup.countedBy || null }), "Count started.");
  const submit = () => {
    const missing = count.lines.filter((l) => counted[l.inventoryItemId] === undefined || counted[l.inventoryItemId] === "");
    if (missing.length) { toast.error(`${missing.length} item${missing.length === 1 ? " has" : "s have"} no count. Write 0 for anything not on the shelf.`); return; }
    act(() => api.submitCount(count.stockCountId, count.lines.map((l) => ({ inventoryItemId: l.inventoryItemId, countedQuantity: Number(counted[l.inventoryItemId]) }))));
  };

  if (start && !count) {
    return h(Dialog, {
      title: "Start a stock count", onClose,
      footer: h(Frag, null, h(Button, { variant: "ghost", onClick: onClose }, "Cancel"), h(Button, { onClick: begin, loading: busy }, "Start counting"))
    },
      h("p", { className: "as-sub", style: { marginTop: 0 } }, "The sheet lists every consumable with its recorded quantity hidden, so the counter writes what is actually on the shelf."),
      h("div", { className: "as-form" },
        h(Select, { label: "Count", value: setup.locationId, placeholder: "Every consumable", options: (options?.locations ?? []).map((l) => ({ value: String(l.assetLocationId), label: `Only what is kept in ${l.locationName}` })), onChange: (ev) => setSetup({ ...setup, locationId: ev.target.value }) }),
        h(Input, { label: "Date", type: "date", max: todayIst(), value: setup.countDate, onChange: (ev) => setSetup({ ...setup, countDate: ev.target.value }) }),
        h("div", { className: "as-span2" }, h(Input, { label: "Counted by", value: setup.countedBy, placeholder: "You, unless someone else counts", onChange: (ev) => setSetup({ ...setup, countedBy: ev.target.value }) }))));
  }
  if (!count) return h(Dialog, { title: "Stock count", onClose }, h(Spinner, { center: true }));

  const submitted = count.status === 2;
  const finished = count.status === 3 || count.status === 4;
  return h(Dialog, {
    title: `Stock count ${count.countNumber}`, onClose, wide: true,
    footer: h(Frag, null,
      h("span", { className: "as-grow" }, `${fmtDay(count.countDate)} · ${count.locationName ?? "every consumable"} · counted by ${count.countedBy}`),
      !finished ? h(Button, { variant: "ghost", onClick: async () => { if (window.confirm("Cancel this count? Nothing will be written.")) { await act(() => api.cancelCount(count.stockCountId), "Count cancelled."); onClose(); } } }, "Cancel count") : null,
      count.status === 1 ? h(Button, { onClick: submit, loading: busy }, "Submit the count") : null,
      submitted ? h(Button, { variant: "outline", onClick: () => setCount({ ...count, status: 1 }) }, "Change counts") : null,
      submitted ? h(Button, { onClick: async () => { if (await act(() => api.confirmCount(count.stockCountId), count.differences ? "Adjustments written." : "Count confirmed — nothing to adjust.")) onClose(); }, loading: busy }, count.differences ? `Write ${count.differences} adjustment${count.differences === 1 ? "" : "s"}` : "Confirm — no differences") : null,
      finished ? h(Button, { onClick: onClose }, "Close") : null)
  },
    submitted ? h("div", { className: "as-note as-note--amber", style: { marginBottom: "var(--space-3)" } },
      count.differences ? `${count.differences} difference${count.differences === 1 ? "" : "s"} against the recorded figures. Confirming writes one adjustment movement for each, with the reason "stock count". The recorded figure is never overwritten.` : "Every count matches the recorded figure.") : null,
    h("div", { className: "as-panel as-table-wrap" }, h("table", { className: "as-table" },
      h("thead", null, h("tr", null, h("th", null, "Item"), h("th", null, "Kept in"), h("th", { className: "num" }, "Counted"), submitted || finished ? h("th", { className: "num" }, "Recorded") : null, submitted || finished ? h("th", null, "Difference") : null)),
      h("tbody", null, count.lines.map((l) => h("tr", { key: l.inventoryItemId, className: l.difference ? "as-row--alert" : undefined },
        h("td", null, h("div", { className: "as-cell-main" }, l.itemName), h("div", { className: "as-cell-sub as-mono" }, `${l.itemCode} · ${l.unit}`)),
        h("td", null, l.keptIn ?? "—"),
        h("td", { className: "num", style: { width: 160 } }, count.status === 1
          ? h("input", { className: "field__input", type: "number", min: 0, step: "0.001", style: { textAlign: "right", maxWidth: 140 }, "aria-label": `Counted ${l.itemName}`, value: counted[l.inventoryItemId] ?? "", onChange: (ev) => setCounted({ ...counted, [l.inventoryItemId]: ev.target.value }) })
          : qty(l.countedQuantity)),
        submitted || finished ? h("td", { className: "num" }, qty(l.recordedQuantity)) : null,
        submitted || finished ? h("td", null, l.difference == null || l.difference === 0 ? h("span", { className: "as-muted" }, "matches")
          : h("strong", { className: l.difference < 0 ? "as-out" : "as-in" }, `${l.itemName} — recorded ${qty(l.recordedQuantity)}, counted ${qty(l.countedQuantity)}, ${l.difference < 0 ? `short ${qty(-l.difference)}` : `over ${qty(l.difference)}`}`)) : null))))));
}
