// Admin → Workspaces (WT1, 14 Oct 2026). Hand-written successor of WorkspacesPage-MB1.js.
// Workspace types are data: the Type dropdown ends with "+ Add new type", which turns into
// an inline form in the same spot (the dialog keeps everything already filled in), and a
// "Workspace types" tab lists, renames, (de)activates, switches online booking and deletes
// types. The Code hint shows real next codes in the XXX-P-NN format, and the required
// price follows how the selected type is booked. Same components and classes as MB1.
import { a as useToast, R as useCan, r as React, y as workspaceTypesApi, z as locationsApi, a7 as amenitiesApi, a8 as workspacesApi, A as ApiError, j as jsxRuntime, p as Button, I as Input, F as Select, D as unwrap, E as http } from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { C as ConfirmDialog } from "./ConfirmDialog-Ct1kLH-J.js";
import { D as DataTable } from "./DataTable-7lkt-S8A.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { u as useDebounce } from "./useDebounce-DSDIQF2c.js";
import { a as statusLabels } from "./masters-Brhh_0gq.js";
import { h as maintenanceApi } from "./operations-BCRENM62.js";
import { b as money } from "./formatters-DFe5xCRi.js";
import "./EmptyState-B4UbbVj4.js";
import "./chevron-right-BikENMwC.js";

const { jsx, jsxs, Fragment } = jsxRuntime;
const { useState, useEffect, useCallback, useMemo, useId } = React;

const adminTypesApi = {
  list: () => unwrap(http.get("/admin/workspace-types")),
  codeExamples: (locationId) => unwrap(http.get("/admin/workspace-types/code-examples", { params: { locationId } })),
  create: (body) => unwrap(http.post("/admin/workspace-types", body)),
  update: (id, body) => unwrap(http.patch(`/admin/workspace-types/${id}`, body)),
  remove: (id) => http.delete(`/admin/workspace-types/${id}`),
};

// Booking modes are flags: 1 by the hour, 2 by the month, 3 both (4 daily is reserved).
const HOURLY = 1, MONTHLY = 2;
const MODE_OPTIONS = [
  { value: "1", label: "By the hour" },
  { value: "2", label: "By the month" },
  { value: "3", label: "Both" },
];
const modeLabel = (m) => MODE_OPTIONS.find((o) => Number(o.value) === m)?.label ?? "—";
const allowsHourly = (t) => !!t && (t.bookingModes & HOURLY) === HOURLY;
const monthOnly = (t) => !!t && (t.bookingModes & HOURLY) === 0 && (t.bookingModes & MONTHLY) === MONTHLY;

const CODE_PATTERN = /^[A-Z]{3}-[A-Z]-\d{2}$/;
const normaliseName = (s) => (s ?? "").replace(/\s+/g, " ").trim();
const NEW_TYPE = "__new_type__";
const errorText = (e, fallback) => (e instanceof ApiError ? e.message : fallback);

const statusTones = { 1: "success", 2: "info", 3: "warning", 4: "neutral" };
const parseUtc = (t) => new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(t) ? t : t + "Z");
/** Workspaces under maintenance right now from a scheduled task (shown as Maintenance). */
const loadMaintenanceIds = async () => {
  const ids = new Set(), now = Date.now(), day = (n) => new Date(now + n * 864e5).toISOString().slice(0, 10);
  try {
    const page = await maintenanceApi.list({ page: 1, pageSize: 200, from: day(-1), to: day(1) });
    for (const c of page?.items ?? []) if (c.blocksWorkspace && c.workspaceId && (c.status === 1 || c.status === 2) && c.startUtc && c.endUtc) {
      const s = parseUtc(c.startUtc).getTime(), e = parseUtc(c.endUtc).getTime();
      if (now >= s && now < e) ids.add(c.workspaceId);
    }
  } catch {
  }
  return ids;
};

function WorkspacesPage({ typeCode }) {
  const toast = useToast();
  const canCreate = useCan("WORKSPACE.CREATE"), canEdit = useCan("WORKSPACE.EDIT"), canDelete = useCan("WORKSPACE.DELETE");
  const [types, setTypes] = useState([]);
  const [locations, setLocations] = useState([]);
  const [amenities, setAmenities] = useState([]);
  const [result, setResult] = useState(null);
  const [loading, setLoading] = useState(true);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const [locationId, setLocationId] = useState("");
  const [typeId, setTypeId] = useState("");
  const debounced = useDebounce(search, 350);
  const [editing, setEditing] = useState(null);
  const [deleting, setDeleting] = useState(null);
  const [maintenanceIds, setMaintenanceIds] = useState(() => new Set());
  const [tab, setTab] = useState("workspaces");
  const fixedType = typeCode ? types.find((t) => t.typeCode === typeCode) : void 0;

  const reloadTypes = useCallback(() => workspaceTypesApi.list().then(setTypes), []);
  useEffect(() => {
    Promise.all([workspaceTypesApi.list(), locationsApi.list({ pageSize: 100 }), amenitiesApi.list()]).then(([t, l, a]) => {
      setTypes(t), setLocations(l.items), setAmenities(a);
    }).catch(() => toast.error("Failed to load reference data."));
  }, []);

  const load = useCallback(async () => {
    if (typeCode && !fixedType) return;
    setLoading(true);
    try {
      setResult(await workspacesApi.list({
        page, pageSize: 20, search: debounced || void 0,
        locationId: locationId ? Number(locationId) : void 0,
        workspaceTypeId: fixedType?.workspaceTypeId ?? (typeId ? Number(typeId) : void 0),
      }));
    } catch (e) {
      toast.error(errorText(e, "Failed to load workspaces."));
    } finally {
      setLoading(false);
    }
    loadMaintenanceIds().then(setMaintenanceIds).catch(() => {});
  }, [page, debounced, locationId, typeId, fixedType?.workspaceTypeId, typeCode]);
  useEffect(() => { load(); }, [load]);

  const columns = [
    { key: "name", header: "Workspace", render: (w) => jsxs("div", { children: [
      jsx("div", { style: { fontWeight: 600 }, children: w.workspaceName }),
      jsxs("div", { style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" }, children: [w.workspaceCode, " · ", w.workspaceTypeName, " · ", w.locationName] }),
    ] }) },
    { key: "capacity", header: "Capacity", render: (w) => {
      const t = types.find((x) => x.workspaceTypeId === w.workspaceTypeId);
      return t && t.requiresCapacity === false ? jsx("span", { style: { color: "var(--text-muted)" }, children: "No limit" }) : w.capacity;
    } },
    { key: "price", header: "Price / hour", render: (w) => money(w.pricePerHour) },
    { key: "status", header: "Status", render: (w) => {
      const inMaintenance = maintenanceIds.has(w.workspaceId), status = inMaintenance ? 3 : w.status;
      return jsxs("div", { style: { display: "flex", gap: "var(--space-2)", alignItems: "center" }, children: [
        jsx("span", { title: inMaintenance ? "Under maintenance from a scheduled task — manage it on the Maintenance screen." : void 0,
          children: jsx(Badge, { tone: statusTones[status] ?? "neutral", children: statusLabels[status] }) }),
        !w.isBookable && jsx(Badge, { tone: "neutral", children: "Not bookable" }),
      ] });
    } },
    { key: "actions", header: "", render: (w) => jsxs("div", { className: "row-actions", children: [
      canEdit && jsx(Button, { size: "sm", variant: "outline", onClick: () => setEditing(w), children: "Edit" }),
      canDelete && jsx(Button, { size: "sm", variant: "danger-outline", onClick: () => setDeleting(w), children: "Delete" }),
    ] }) },
  ];
  const title = fixedType ? `${fixedType.typeName}s` : "Workspaces";
  const showTabs = !typeCode;

  return jsxs(Fragment, { children: [
    jsx(PageHeader, { title, description: "Bookable inventory. Maintenance status removes a workspace from customer availability immediately.",
      actions: canCreate && tab === "workspaces" && jsx(Button, { onClick: () => setEditing("new"), children: "New workspace" }) }),
    jsxs(Card, { children: [
      showTabs && jsxs("div", { className: "qtn-tabs", role: "tablist", children: [
        jsx("button", { type: "button", role: "tab", "aria-selected": tab === "workspaces", className: `qtn-tab${tab === "workspaces" ? " qtn-tab--active" : ""}`, onClick: () => setTab("workspaces"), children: "Workspaces" }),
        jsxs("button", { type: "button", role: "tab", "aria-selected": tab === "types", className: `qtn-tab${tab === "types" ? " qtn-tab--active" : ""}`, onClick: () => setTab("types"),
          children: ["Workspace types", jsx("span", { className: "qtn-tab__count", children: types.length })] }),
      ] }),
      tab === "types" && showTabs
        ? jsx(WorkspaceTypesPanel, { canCreate, canEdit, canDelete, onChanged: () => { reloadTypes(); load(); } })
        : jsxs(Fragment, { children: [
          jsxs("div", { className: "toolbar", children: [
            jsx(Input, { label: "Search", placeholder: "Name or code…", value: search, onChange: (e) => { setSearch(e.target.value), setPage(1); } }),
            jsx(Select, { label: "Location", value: locationId, placeholder: "All locations", options: locations.map((l) => ({ value: l.locationId, label: l.locationName })),
              onChange: (e) => { setLocationId(e.target.value), setPage(1); } }),
            !fixedType && jsx(Select, { label: "Type", value: typeId, placeholder: "All types", options: types.map((t) => ({ value: t.workspaceTypeId, label: t.isActive ? t.typeName : `${t.typeName} (inactive)` })),
              onChange: (e) => { setTypeId(e.target.value), setPage(1); } }),
          ] }),
          jsx(DataTable, { columns, result, loading, rowKey: (w) => w.workspaceId, onPageChange: setPage, emptyTitle: "No workspaces yet", emptyDescription: "Create desks, cabins and meeting rooms to open bookings." }),
        ] }),
    ] }),
    editing && jsx(WorkspaceDialog, { workspace: editing === "new" ? null : editing, types, locations, amenities, defaultTypeId: fixedType?.workspaceTypeId,
      canAddType: canCreate, onTypeAdded: reloadTypes, onClose: () => setEditing(null), onSaved: () => { setEditing(null), load(), reloadTypes(); } }),
    jsx(ConfirmDialog, { open: deleting !== null, danger: true, confirmLabel: "Delete", title: `Delete ${deleting?.workspaceName ?? ""}?`,
      message: "A workspace with upcoming bookings cannot be deleted. Booking history is preserved.",
      onConfirm: async () => {
        if (deleting) try {
          await workspacesApi.remove(deleting.workspaceId), toast.success("Workspace deleted."), await load(), reloadTypes();
        } catch (e) {
          toast.error(errorText(e, "Could not delete."));
        }
      }, onClose: () => setDeleting(null) }),
  ] });
}

/** The Type field: the shared Select's markup, plus a divider and "+ Add new type", which
 *  the shared component cannot render. */
function TypeSelect({ value, types, canAddType, onChange }) {
  const id = useId();
  return jsxs("div", { className: "field", children: [
    jsxs("label", { className: "field__label", htmlFor: id, children: ["Type", jsxs("span", { className: "field__required", "aria-hidden": true, children: [" ", "*"] })] }),
    jsxs("select", { id, className: "field__input", required: true, value, onChange: (e) => onChange(e.target.value), children: [
      jsx("option", { value: "", children: "Select type" }),
      types.map((t) => jsx("option", { value: t.workspaceTypeId, children: t.isActive ? t.typeName : `${t.typeName} (inactive)` }, t.workspaceTypeId)),
      canAddType && jsx("option", { disabled: true, value: "__divider__", children: "──────────" }),
      canAddType && jsx("option", { value: NEW_TYPE, children: "+ Add new type" }),
    ] }),
  ] });
}

/** "+ Add new type": name, code prefix, how it is booked. Used inline in the workspace
 *  dialog and in the Workspace types tab. A new type starts hidden from customers. */
function NewTypeForm({ existing, onCancel, onAdded, framed = true }) {
  const toast = useToast();
  const [name, setName] = useState("");
  const [prefix, setPrefix] = useState("");
  const [modes, setModes] = useState("1");
  const [saving, setSaving] = useState(false);
  const [serverError, setServerError] = useState(null);
  const normalised = normaliseName(name);
  const duplicate = normalised && existing.some((t) => normaliseName(t.typeName).toLowerCase() === normalised.toLowerCase());
  const nameError = serverError ?? (duplicate ? "A type with this name already exists."
    : name && (normalised.length < 2 || normalised.length > 40) ? "Use 2 to 40 characters." : null);
  const prefixError = prefix && !/^[A-Z]$/.test(prefix) ? "One letter, A to Z." : null;
  const ready = normalised.length >= 2 && normalised.length <= 40 && !duplicate && /^[A-Z]$/.test(prefix) && !!modes;

  const add = async () => {
    setSaving(true), setServerError(null);
    try {
      const created = await adminTypesApi.create({ typeName: normalised, codePrefix: prefix, bookingModes: Number(modes) });
      toast.success(`${created.typeName} added.`);
      await onAdded(created);
    } catch (e) {
      const message = errorText(e, "Could not add the type.");
      e instanceof ApiError && e.hasCode("DUPLICATE_RECORD") ? setServerError(message) : toast.error(message);
    } finally {
      setSaving(false);
    }
  };

  return jsxs("div", { className: "form-stack", style: framed ? { gap: "var(--space-3)", padding: "var(--space-3)", border: "1px solid var(--border-subtle)", borderRadius: "var(--radius-md)" } : void 0, children: [
    jsx(Input, { label: "Type name", required: true, autoFocus: true, maxLength: 60, value: name, error: nameError ?? void 0,
      onChange: (e) => { setName(e.target.value), setServerError(null); } }),
    jsx(Input, { label: "Code prefix", required: true, maxLength: 1, value: prefix, error: prefixError ?? void 0, hint: "One letter for the middle of the code, e.g. D, C, M, V",
      onChange: (e) => setPrefix(e.target.value.toUpperCase().replace(/[^A-Z]/g, "")) }),
    jsx(Select, { label: "How is it booked?", required: true, value: modes, options: MODE_OPTIONS, onChange: (e) => setModes(e.target.value) }),
    jsxs("div", { className: "row-actions", style: { justifyContent: "flex-end" }, children: [
      jsx(Button, { size: "sm", variant: "ghost", onClick: onCancel, disabled: saving, children: "Cancel" }),
      jsx(Button, { size: "sm", onClick: add, loading: saving, disabled: !ready, children: "Add type" }),
    ] }),
  ] });
}

function WorkspaceDialog({ workspace, types, locations, amenities, defaultTypeId, canAddType, onTypeAdded, onClose, onSaved }) {
  const toast = useToast();
  const [form, setForm] = useState({
    locationId: workspace?.locationId.toString() ?? locations[0]?.locationId.toString() ?? "",
    workspaceTypeId: workspace?.workspaceTypeId.toString() ?? defaultTypeId?.toString() ?? "",
    workspaceCode: workspace?.workspaceCode ?? "", workspaceName: workspace?.workspaceName ?? "",
    floorNumber: workspace?.floorNumber ?? "", capacity: workspace?.capacity ?? 1,
    pricePerHour: workspace?.pricePerHour ?? 0, pricePerDay: workspace?.pricePerDay ?? "", pricePerMonth: workspace?.pricePerMonth ?? "",
    equipment: workspace?.equipment ?? "", description: workspace?.description ?? "",
    isBookable: workspace?.isBookable ?? true, amenityIds: workspace?.amenities.map((a) => a.amenityId) ?? [],
  });
  const [saving, setSaving] = useState(false);
  const [addingType, setAddingType] = useState(false);
  const [added, setAdded] = useState([]); // types added here, until the page's list has them
  const [examples, setExamples] = useState({});
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));

  // New workspaces get active types only; an existing workspace keeps its retired type.
  const allTypes = useMemo(() => [...types, ...added.filter((a) => !types.some((t) => t.workspaceTypeId === a.workspaceTypeId))], [types, added]);
  const selectable = allTypes.filter((t) => t.isActive || t.workspaceTypeId === workspace?.workspaceTypeId);
  const type = allTypes.find((t) => String(t.workspaceTypeId) === String(form.workspaceTypeId));

  const loadExamples = useCallback(() => {
    if (!form.locationId) return;
    adminTypesApi.codeExamples(Number(form.locationId))
      .then((rows) => setExamples(Object.fromEntries(rows.map((r) => [r.workspaceTypeId, r.examples]))))
      .catch(() => setExamples({}));
  }, [form.locationId]);
  useEffect(() => { loadExamples(); }, [loadExamples]);

  const code = form.workspaceCode.trim().toUpperCase();
  const prefix = type?.codePrefix;
  const codeHint = !type ? "Format XXX-P-NN: three letters, the type letter, two digits"
    : examples[type.workspaceTypeId]?.length ? `e.g. ${examples[type.workspaceTypeId].join(", ")}`
    : prefix ? `e.g. XXX-${prefix}-01` : "Format XXX-P-NN";
  const codeError = !code ? null
    : !CODE_PATTERN.test(code) ? "Use the format XXX-P-NN, e.g. MER-M-01."
    : prefix && code[4] !== prefix ? `The middle letter must be ${prefix} for ${type.typeName}.` : null;

  const hourRequired = !type || allowsHourly(type);
  const monthRequired = monthOnly(type);
  const monthHint = monthRequired ? "Required: customers book by the month at this rate."
    : type && !(type.bookingModes & MONTHLY) ? "Optional: this type is booked by the hour."
    : type && !type.isSystem ? "Customers book by the month at this rate. Leave empty to keep it off monthly booking."
    : "Desks and cabins: customers book by the month at this rate. Leave empty to keep it off monthly booking.";
  const pricesOk = (!hourRequired || Number(form.pricePerHour) > 0) && (!monthRequired || Number(form.pricePerMonth) > 0);

  const save = async () => {
    setSaving(true);
    const body = {
      locationId: Number(form.locationId), workspaceTypeId: Number(form.workspaceTypeId), workspaceCode: code,
      workspaceName: form.workspaceName.trim(), floorNumber: form.floorNumber || null, capacity: Number(form.capacity),
      pricePerHour: Number(form.pricePerHour) || 0,
      pricePerDay: form.pricePerDay === "" ? null : Number(form.pricePerDay),
      pricePerMonth: form.pricePerMonth === "" ? null : Number(form.pricePerMonth),
      equipment: form.equipment || null, description: form.description || null, isBookable: form.isBookable, amenityIds: form.amenityIds,
    };
    try {
      workspace ? await workspacesApi.update(workspace.workspaceId, body) : await workspacesApi.create(body);
      toast.success(workspace ? "Workspace updated." : "Workspace created.");
      onSaved();
    } catch (e) {
      toast.error(errorText(e, "Could not save the workspace."));
    } finally {
      setSaving(false);
    }
  };

  const typeAdded = async (created) => {
    setAdded((a) => [...a, { ...created, workspaceCount: 0 }]);
    set({ workspaceTypeId: String(created.workspaceTypeId) }); // nothing else in the form changes
    setAddingType(false);
    loadExamples();
    await onTypeAdded();
  };

  return jsx(Modal, { open: true, title: workspace ? `Edit — ${workspace.workspaceName}` : "New workspace", onClose,
    footer: jsxs(Fragment, { children: [
      jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
      jsx(Button, { onClick: save, loading: saving,
        disabled: addingType || !code || !!codeError || !form.workspaceName.trim() || !form.locationId || !form.workspaceTypeId || !pricesOk, children: "Save" }),
    ] }),
    children: jsxs("div", { className: "form-stack", children: [
      jsxs("div", { className: "form-grid", children: [
        jsx(Select, { label: "Location", required: true, value: form.locationId, options: locations.map((l) => ({ value: l.locationId, label: l.locationName })),
          onChange: (e) => set({ locationId: e.target.value }) }),
        addingType
          ? jsx(NewTypeForm, { existing: allTypes, onCancel: () => setAddingType(false), onAdded: typeAdded })
          : jsx(TypeSelect, { value: form.workspaceTypeId, types: selectable, canAddType,
            onChange: (v) => (v === NEW_TYPE ? setAddingType(true) : set({ workspaceTypeId: v })) }),
        jsx(Input, { label: "Code", required: true, value: form.workspaceCode, hint: codeHint, error: codeError ?? void 0,
          onChange: (e) => set({ workspaceCode: e.target.value.toUpperCase() }) }),
        jsx(Input, { label: "Name", required: true, value: form.workspaceName, onChange: (e) => set({ workspaceName: e.target.value }) }),
        jsx(Input, { label: "Floor", value: form.floorNumber, onChange: (e) => set({ floorNumber: e.target.value }) }),
        type?.requiresCapacity === false ? null
          : jsx(Input, { label: "Capacity", type: "number", min: 1, required: true, value: form.capacity, onChange: (e) => set({ capacity: Number(e.target.value) }) }),
        jsx(Input, { label: "Price / hour (₹)", type: "number", min: 0, step: "0.01", required: hourRequired, value: form.pricePerHour,
          onChange: (e) => set({ pricePerHour: e.target.value === "" ? "" : Number(e.target.value) }) }),
        jsx(Input, { label: "Price / day (₹)", type: "number", min: 0, step: "0.01", value: form.pricePerDay, hint: "Optional — used by day passes",
          onChange: (e) => set({ pricePerDay: e.target.value === "" ? "" : Number(e.target.value) }) }),
        jsx(Input, { label: "Price / month (₹)", type: "number", min: 0, step: "0.01", required: monthRequired, value: form.pricePerMonth, hint: monthHint,
          onChange: (e) => set({ pricePerMonth: e.target.value === "" ? "" : Number(e.target.value) }) }),
        jsx(Input, { label: "Equipment", value: form.equipment, hint: "Comma list, e.g. Projector,Whiteboard,VC", onChange: (e) => set({ equipment: e.target.value }) }),
      ] }),
      amenities.length > 0 && jsxs("div", { className: "field", children: [
        jsx("span", { className: "field__label", children: "Amenities" }),
        jsx("div", { className: "perm-matrix__actions", children: amenities.map((a) => jsx(Checkbox, { label: a.amenityName, checked: form.amenityIds.includes(a.amenityId),
          onChange: (e) => set({ amenityIds: e.target.checked ? [...form.amenityIds, a.amenityId] : form.amenityIds.filter((x) => x !== a.amenityId) }) }, a.amenityId)) }),
      ] }),
      jsx(Checkbox, { label: "Open for customer booking", checked: form.isBookable, onChange: (e) => set({ isBookable: e.target.checked }) }),
    ] }) });
}

/** Workspaces → Workspace types. */
function WorkspaceTypesPanel({ canCreate, canEdit, canDelete, onChanged }) {
  const toast = useToast();
  const [rows, setRows] = useState(null);
  const [adding, setAdding] = useState(false);
  const [renaming, setRenaming] = useState(null);
  const [newName, setNewName] = useState("");
  const [busy, setBusy] = useState(false);
  const [confirm, setConfirm] = useState(null); // { title, message, label, danger, run }

  const load = useCallback(() => adminTypesApi.list().then(setRows).catch((e) => toast.error(errorText(e, "Failed to load workspace types."))), []);
  useEffect(() => { load(); }, [load]);
  const changed = async () => { await load(); onChanged(); };

  const patch = async (type, body, done) => {
    try {
      await adminTypesApi.update(type.workspaceTypeId, body);
      toast.success(done);
      await changed();
    } catch (e) {
      toast.error(errorText(e, "Could not update the type."));
    }
  };

  const rename = async () => {
    setBusy(true);
    try {
      await adminTypesApi.update(renaming.workspaceTypeId, { typeName: normaliseName(newName) });
      toast.success("Type renamed.");
      setRenaming(null);
      await changed();
    } catch (e) {
      toast.error(errorText(e, "Could not rename the type."));
    } finally {
      setBusy(false);
    }
  };

  const columns = [
    { key: "name", header: "Name", render: (t) => jsxs("div", { style: { display: "flex", gap: "var(--space-2)", alignItems: "center" }, children: [
      jsx("span", { style: { fontWeight: 600 }, children: t.typeName }),
      t.isSystem && jsx(Badge, { tone: "neutral", children: "System" }),
    ] }) },
    { key: "prefix", header: "Code prefix", render: (t) => t.codePrefix ?? "—" },
    { key: "modes", header: "How it is booked", render: (t) => modeLabel(t.bookingModes) },
    { key: "count", header: "Workspaces", render: (t) => t.workspaceCount },
    { key: "online", header: "Customers can book online", render: (t) => jsx(Checkbox, {
      label: t.customerBookable ? "On" : "Off", checked: t.customerBookable, disabled: !canEdit,
      onChange: (e) => {
        const on = e.target.checked;
        setConfirm({
          title: on ? `Show ${t.typeName} to customers?` : `Hide ${t.typeName} from customers?`,
          message: on
            ? `Customers will see ${t.typeName} on the booking page, for its workspaces that are open for customer booking.`
            : `${t.typeName} disappears from the customer booking page. Bookings already made are not affected.`,
          label: on ? "Show to customers" : "Hide from customers", danger: !on,
          run: () => patch(t, { customerBookable: on }, on ? `${t.typeName} is now bookable online.` : `${t.typeName} is hidden from customers.`),
        });
      } }) },
    { key: "status", header: "Status", render: (t) => jsx(Badge, { tone: t.isActive ? "success" : "neutral", children: t.isActive ? "Active" : "Inactive" }) },
    { key: "actions", header: "", render: (t) => jsxs("div", { className: "row-actions", children: [
      canEdit && jsx(Button, { size: "sm", variant: "outline", onClick: () => { setRenaming(t), setNewName(t.typeName); }, children: "Rename" }),
      canEdit && jsx(Button, { size: "sm", variant: "outline", onClick: () => setConfirm(t.isActive
        ? { title: `Deactivate ${t.typeName}?`, label: "Deactivate", danger: true,
          message: `${t.typeName} disappears from the Type list for new workspaces. Its ${t.workspaceCount} existing workspace${t.workspaceCount === 1 ? "" : "s"} keep it and still show it.`,
          run: () => patch(t, { isActive: false }, `${t.typeName} deactivated.`) }
        : { title: `Activate ${t.typeName}?`, label: "Activate", message: `${t.typeName} is offered again for new workspaces.`,
          run: () => patch(t, { isActive: true }, `${t.typeName} activated.`) }), children: t.isActive ? "Deactivate" : "Activate" }),
      canDelete && !t.isSystem && jsx(Button, { size: "sm", variant: "danger-outline", onClick: () => setConfirm({
        title: `Delete ${t.typeName}?`, label: "Delete", danger: true, message: "A type that any workspace uses cannot be deleted; deactivate it instead.",
        run: async () => {
          try {
            await adminTypesApi.remove(t.workspaceTypeId);
            toast.success(`${t.typeName} deleted.`);
            await changed();
          } catch (e) {
            toast.error(errorText(e, "Could not delete the type."));
          }
        } }), children: "Delete" }),
    ] }) },
  ];
  const result = rows && { items: rows, page: 1, pageSize: rows.length, totalCount: rows.length, totalPages: 1, hasPrevious: false, hasNext: false };

  return jsxs(Fragment, { children: [
    jsxs("div", { className: "toolbar", style: { justifyContent: "space-between", alignItems: "center" }, children: [
      jsx("span", { style: { color: "var(--text-muted)", fontSize: "var(--font-size-sm)" }, children: "A type in use is retired by deactivating it. System types can be renamed but not deleted." }),
      canCreate && jsx(Button, { size: "sm", onClick: () => setAdding(true), children: "Add type" }),
    ] }),
    jsx(DataTable, { columns, result, loading: rows === null, rowKey: (t) => t.workspaceTypeId, onPageChange: () => {}, emptyTitle: "No workspace types" }),
    adding && jsx(Modal, { open: true, title: "New workspace type", onClose: () => setAdding(false),
      children: jsx(NewTypeForm, { framed: false, existing: rows ?? [], onCancel: () => setAdding(false), onAdded: async () => { setAdding(false); await changed(); } }) }),
    renaming && jsx(Modal, { open: true, title: `Rename — ${renaming.typeName}`, onClose: () => setRenaming(null),
      footer: jsxs(Fragment, { children: [
        jsx(Button, { variant: "ghost", onClick: () => setRenaming(null), children: "Cancel" }),
        jsx(Button, { onClick: rename, loading: busy, disabled: normaliseName(newName).length < 2 || normaliseName(newName).length > 40 || normaliseName(newName) === renaming.typeName, children: "Save" }),
      ] }),
      children: jsx(Input, { label: "Type name", required: true, autoFocus: true, value: newName, hint: "The new name shows on every workspace and screen at once.", onChange: (e) => setNewName(e.target.value) }) }),
    jsx(ConfirmDialog, { open: confirm !== null, danger: !!confirm?.danger, confirmLabel: confirm?.label ?? "Confirm", title: confirm?.title ?? "", message: confirm?.message ?? "",
      onConfirm: async () => { await confirm?.run(); }, onClose: () => setConfirm(null) }),
  ] });
}

export { WorkspacesPage };
