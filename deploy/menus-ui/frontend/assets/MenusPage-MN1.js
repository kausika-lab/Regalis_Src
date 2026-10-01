/**
 * Admin -> Administration -> Menu Management   (/admin/menus)
 *
 * The sidebar is data (MST_Menu). This screen shows it as the full tree, every level
 * (the original only drew two, so level-3 menus such as Billing & Finance -> Invoices
 * could not be edited here), with the same icons the sidebar draws, search, a
 * visibility filter, collapsible groups and inline actions.
 *
 * API (unchanged): GET/POST /api/menus, PUT/DELETE /api/menus/{id}.
 *
 * Hand-maintained (this repo ships the built SPA): index-DI5X4c9f.js imports this file
 * by name. It replaces MenusPage-BVE5QAf-.js; a fix ships under a NEW name (MN2 ...)
 * because /assets is cached for a year and this file is not precached.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  A as ApiError,
  j as e,
  G as Spinner,
  p as Button,
  F as Select,
  I as Input,
  a3 as resolveIcon
} from "./index-DI5X4c9f.js";
import { m as menusApi } from "./admin-DtGep2Hs.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { C as Checkbox } from "./Checkbox-CO1-Cn20.js";
import { C as ConfirmDialog } from "./ConfirmDialog-Ct1kLH-J.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";

const PORTALS = [
  { value: 2, label: "Admin" },
  { value: 1, label: "Customer" }
];

// The names the sidebar can draw (its icon map in index-DI5X4c9f.js). Anything else
// renders as the fallback grid icon, which the screen points out.
const SIDEBAR_ICONS = ["Armchair", "ArrowLeftRight", "BarChart3", "Bell", "Boxes", "Building2",
  "CalendarDays", "CalendarRange", "ClipboardList", "Clock", "Coffee", "CreditCard", "DoorClosed",
  "FileSpreadsheet", "History", "Image", "IndianRupee", "KeyRound", "Landmark", "LayoutDashboard",
  "LayoutGrid", "LifeBuoy", "ListChecks", "MapPin", "MessageSquarePlus", "Package", "PanelLeft",
  "Phone", "Presentation", "QrCode", "Receipt", "ReceiptText", "ScanLine", "Settings",
  "ShieldCheck", "ShoppingCart", "Sparkles", "Ticket", "Tickets", "UserCog", "UserRound",
  "UsersRound", "Wallet"];
const KNOWN_ICONS = new Set(SIDEBAR_ICONS);

// The deepest level a menu may sit at; a parent must therefore be above it.
const MAX_LEVEL = 4;

const errorText = (error, fallback) => (error instanceof ApiError ? error.message : fallback);

const STYLE_ID = "rg-menus-mn1";
const STYLES = `
.mn-top{display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:var(--space-3);margin-bottom:var(--space-4)}
.mn-seg{display:inline-flex;padding:3px;border:1px solid var(--border-subtle);border-radius:var(--radius-full);background:var(--surface-inset)}
.mn-seg button{all:unset;cursor:pointer;display:inline-flex;align-items:center;gap:6px;padding:7px 16px;border-radius:var(--radius-full);font-size:var(--font-size-sm);font-weight:600;color:var(--text-secondary)}
.mn-seg button[aria-pressed=true]{background:var(--regalis-gold);color:var(--accent-contrast,#0b0b0d);box-shadow:var(--shadow-sm)}
.mn-seg button:focus-visible{outline:2px solid var(--regalis-gold);outline-offset:2px}
.mn-seg__count{font-size:var(--font-size-xs);font-weight:600;opacity:.75}
.mn-stats{display:flex;flex-wrap:wrap;gap:var(--space-2)}
.mn-stat{padding:4px 12px;border-radius:var(--radius-full);background:rgb(var(--accent-rgb) / 9%);font-size:var(--font-size-xs);color:var(--text-secondary)}
.mn-stat strong{color:var(--text-primary);font-variant-numeric:tabular-nums}

/* Filters: one aligned row of equal 40px controls, labels on one line. */
.mn-filters{display:grid;grid-template-columns:minmax(0,2fr) minmax(0,1fr) auto;gap:var(--space-4);align-items:end;margin-bottom:var(--space-4)}
@media(max-width:760px){.mn-filters{grid-template-columns:minmax(0,1fr)}}
.mn-filters .field{min-width:0;flex:none;gap:6px}
.mn-filters .field__label{line-height:1.25;white-space:nowrap}
.mn-filters .field__input{box-sizing:border-box;height:40px;min-width:0;width:100%;padding-top:0;padding-bottom:0;line-height:38px}
.mn-filters select.field__input{padding-right:32px}
.mn-filters__btns{display:flex;gap:var(--space-2)}

.mn-wrap{overflow-x:auto;border:1px solid var(--border-subtle);border-radius:var(--radius-lg)}
.mn-table{width:100%;border-collapse:collapse;font-size:var(--font-size-sm);min-width:760px}
.mn-table th{padding:10px 12px;text-align:left;font-size:var(--font-size-xs);font-weight:700;letter-spacing:.05em;text-transform:uppercase;color:var(--text-secondary);background:rgb(var(--accent-rgb) / 7%);border-bottom:1px solid var(--border-subtle);white-space:nowrap}
.mn-table td{padding:8px 12px;border-bottom:1px solid var(--border-subtle);vertical-align:middle}
.mn-table tbody tr:last-child td{border-bottom:0}
.mn-table tbody tr:hover td{background:var(--surface-hover)}
.mn-table tr.mn-group td{background:rgb(var(--accent-rgb) / 6%)}
.mn-table tr.mn-group:hover td{background:rgb(var(--accent-rgb) / 10%)}
.mn-table tr.mn-hidden .mn-name,.mn-table tr.mn-hidden .mn-icon{opacity:.55}
.mn-num{text-align:right!important;font-variant-numeric:tabular-nums;width:64px}

.mn-cell{display:flex;align-items:center;gap:8px;min-width:0}
.mn-indent{flex:none;align-self:stretch;display:flex}
.mn-indent span{width:22px;border-left:1px solid var(--border-strong);margin-left:10px;opacity:.6}
.mn-toggle{all:unset;flex:none;display:grid;place-items:center;width:22px;height:22px;border-radius:var(--radius-sm);cursor:pointer;color:var(--text-secondary);transition:transform var(--transition-fast)}
.mn-toggle:hover,.mn-toggle:focus-visible{background:var(--surface-hover);color:var(--text-primary)}
.mn-toggle[aria-expanded=false]{transform:rotate(-90deg)}
.mn-toggle--none{visibility:hidden}
.mn-icon{flex:none;display:grid;place-items:center;width:30px;height:30px;border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 12%);color:var(--regalis-gold-dark)}
.mn-group .mn-icon{background:transparent;border:1px dashed var(--border-strong);color:var(--text-muted)}
.mn-name{min-width:0}
.mn-name__title{font-weight:600;color:var(--text-primary);white-space:nowrap;overflow:hidden;text-overflow:ellipsis}
.mn-group .mn-name__title{text-transform:uppercase;letter-spacing:.06em;font-size:var(--font-size-xs);font-weight:700}
.mn-name__code{white-space:nowrap;font-size:var(--font-size-xs);color:var(--text-muted);font-family:ui-monospace,SFMono-Regular,Consolas,monospace}
.mn-route{display:inline-block;max-width:240px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;padding:2px 8px;border-radius:var(--radius-sm);background:var(--surface-inset);font-family:ui-monospace,SFMono-Regular,Consolas,monospace;font-size:var(--font-size-xs);color:var(--text-secondary);vertical-align:middle}
.mn-muted{color:var(--text-muted);font-size:var(--font-size-xs)}
.mn-iconname{display:flex;align-items:center;gap:6px;font-size:var(--font-size-xs);color:var(--text-secondary);white-space:nowrap}
.mn-warn{color:var(--color-warning,#b45309);font-weight:600}
.mn-actions{display:flex;gap:6px;justify-content:flex-end;flex-wrap:nowrap;white-space:nowrap}
.mn-count{margin-left:6px;padding:0 7px;border-radius:var(--radius-full);background:var(--surface-inset);font-size:var(--font-size-xs);font-weight:600;color:var(--text-muted)}
.mn-mark{background:rgb(var(--accent-rgb) / 30%);color:inherit;border-radius:2px;padding:0 1px}
.mn-empty{padding:40px 16px;text-align:center;color:var(--text-muted)}

.mn-preview{display:flex;align-items:center;gap:var(--space-3);padding:var(--space-3) var(--space-4);border-radius:var(--radius-md);background:rgb(var(--accent-rgb) / 7%);font-size:var(--font-size-sm)}
.mn-preview__path{color:var(--text-muted);font-size:var(--font-size-xs)}
`;

function useStyles() {
  React.useEffect(() => {
    if (document.getElementById(STYLE_ID)) return;
    const style = document.createElement("style");
    style.id = STYLE_ID;
    style.textContent = STYLES;
    document.head.appendChild(style);
  }, []);
}

function Icon({ name, size = 16 }) {
  const Component = resolveIcon(name);
  return e.jsx(Component, { size, "aria-hidden": true });
}

const Chevron = () =>
  e.jsx("svg", {
    width: 14, height: 14, viewBox: "0 0 24 24", fill: "none", stroke: "currentColor",
    strokeWidth: 2.5, strokeLinecap: "round", strokeLinejoin: "round", "aria-hidden": true,
    children: e.jsx("path", { d: "m6 9 6 6 6-6" })
  });

function Highlight({ text, term }) {
  if (!term || !text) return text ?? null;
  const at = text.toLowerCase().indexOf(term.toLowerCase());
  if (at < 0) return text;
  return e.jsxs(e.Fragment, {
    children: [
      text.slice(0, at),
      e.jsx("mark", { className: "mn-mark", children: text.slice(at, at + term.length) }),
      text.slice(at + term.length)
    ]
  });
}

/** Parent -> children index for one portal, each child list sorted by display order. */
function buildTree(menus) {
  const children = new Map();
  for (const m of menus) {
    const key = m.parentMenuId ?? 0;
    if (!children.has(key)) children.set(key, []);
    children.get(key).push(m);
  }
  for (const list of children.values()) {
    list.sort((a, b) => a.displayOrder - b.displayOrder || a.displayName.localeCompare(b.displayName));
  }
  // A menu whose parent is in another portal (or gone) still shows, at the top level.
  const ids = new Set(menus.map((m) => m.menuId));
  const roots = menus.filter((m) => m.parentMenuId === null || !ids.has(m.parentMenuId))
    .sort((a, b) => a.displayOrder - b.displayOrder || a.displayName.localeCompare(b.displayName));
  return { roots, childrenOf: (id) => children.get(id) ?? [] };
}

function descendantIds(menuId, childrenOf) {
  const out = new Set();
  const walk = (id) => childrenOf(id).forEach((c) => { out.add(c.menuId); walk(c.menuId); });
  walk(menuId);
  return out;
}

function pathOf(menu, byId) {
  const names = [];
  let current = menu;
  const seen = new Set();
  while (current && !seen.has(current.menuId)) {
    seen.add(current.menuId);
    names.unshift(current.displayName);
    current = current.parentMenuId ? byId.get(current.parentMenuId) : null;
  }
  return names.join(" › ");
}

// ── Page ────────────────────────────────────────────────────────────────────

function MenusPage() {
  useStyles();
  const toast = useToast();
  const canCreate = usePermission("MENU.CREATE");
  const canEdit = usePermission("MENU.EDIT");
  const canDelete = usePermission("MENU.DELETE");

  const [menus, setMenus] = React.useState(null);
  const [portal, setPortal] = React.useState(2);
  const [search, setSearch] = React.useState("");
  const [visibility, setVisibility] = React.useState("");
  const [collapsed, setCollapsed] = React.useState(() => new Set());
  const [editing, setEditing] = React.useState(null); // { menu } | { parentId } | null
  const [deleting, setDeleting] = React.useState(null);

  const load = React.useCallback(async () => {
    try {
      setMenus(await menusApi.list());
    } catch (error) {
      toast.error(errorText(error, "Failed to load menus."));
    }
  }, []);
  React.useEffect(() => {
    load();
  }, [load]);

  const portalMenus = React.useMemo(
    () => (menus ?? []).filter((m) => m.portalType === portal),
    [menus, portal]
  );
  const byId = React.useMemo(() => new Map((menus ?? []).map((m) => [m.menuId, m])), [menus]);
  const tree = React.useMemo(() => buildTree(portalMenus), [portalMenus]);

  const term = search.trim().toLowerCase();
  const filtering = term.length > 0 || visibility !== "";
  const matches = (m) =>
    (!term ||
      m.displayName.toLowerCase().includes(term) ||
      m.menuCode.toLowerCase().includes(term) ||
      (m.route ?? "").toLowerCase().includes(term)) &&
    (visibility === "" || (visibility === "visible" ? m.isVisible : !m.isVisible));

  // Rows to draw: a match keeps its ancestors so it is shown in context; while
  // filtering every branch is open.
  const rows = React.useMemo(() => {
    const out = [];
    const visit = (menu, depth, guides) => {
      const kids = tree.childrenOf(menu.menuId);
      const childRows = [];
      const open = filtering || !collapsed.has(menu.menuId);
      const saved = out.length;
      out.push(null); // placeholder for this row
      let anyChild = false;
      kids.forEach((kid, index) => {
        const before = out.length;
        visit(kid, depth + 1, [...guides, index < kids.length - 1]);
        if (out.length > before) anyChild = true;
      });
      const selfMatch = matches(menu);
      if (!selfMatch && !anyChild) {
        out.length = saved;
        return;
      }
      if (!open) out.length = saved + 1;
      out[saved] = { menu, depth, kids: kids.length, open, dim: filtering && !selfMatch };
      void childRows;
    };
    tree.roots.forEach((root) => visit(root, 0, []));
    return out;
  }, [tree, collapsed, term, visibility]);

  if (!menus) return e.jsx(Spinner, { center: true });

  const counts = {
    total: portalMenus.length,
    visible: portalMenus.filter((m) => m.isVisible).length,
    groups: portalMenus.filter((m) => !m.route).length
  };
  const portalCount = (value) => menus.filter((m) => m.portalType === value).length;
  const parentsWithKids = portalMenus.filter((m) => tree.childrenOf(m.menuId).length > 0);

  const toggle = (id) =>
    setCollapsed((current) => {
      const next = new Set(current);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(PageHeader, {
        title: "Menu Management",
        description: "The sidebar is data: adding a screen is an insert here plus a role mapping — no redeploy.",
        actions: canCreate && e.jsx(Button, { onClick: () => setEditing({ parentId: null }), children: "New menu" })
      }),
      e.jsxs(Card, {
        children: [
          e.jsxs("div", {
            className: "mn-top",
            children: [
              e.jsx("div", {
                className: "mn-seg",
                role: "group",
                "aria-label": "Portal",
                children: PORTALS.map((p) =>
                  e.jsxs("button", {
                    type: "button",
                    "aria-pressed": portal === p.value,
                    onClick: () => setPortal(p.value),
                    children: [`${p.label} portal`, e.jsx("span", { className: "mn-seg__count", children: portalCount(p.value) })]
                  }, p.value)
                )
              }),
              e.jsxs("div", {
                className: "mn-stats",
                children: [
                  e.jsxs("span", { className: "mn-stat", children: [e.jsx("strong", { children: counts.total }), " menus"] }),
                  e.jsxs("span", { className: "mn-stat", children: [e.jsx("strong", { children: counts.groups }), " groups"] }),
                  e.jsxs("span", { className: "mn-stat", children: [e.jsx("strong", { children: counts.visible }), " visible"] }),
                  e.jsxs("span", { className: "mn-stat", children: [e.jsx("strong", { children: counts.total - counts.visible }), " hidden"] })
                ]
              })
            ]
          }),
          e.jsxs("div", {
            className: "mn-filters",
            children: [
              e.jsx(Input, { label: "Search", placeholder: "Name, code or route", value: search, onChange: (ev) => setSearch(ev.target.value) }),
              e.jsx(Select, {
                label: "Visibility",
                value: visibility,
                placeholder: "All menus",
                options: [{ value: "visible", label: "Visible only" }, { value: "hidden", label: "Hidden only" }],
                onChange: (ev) => setVisibility(ev.target.value)
              }),
              e.jsxs("div", {
                className: "mn-filters__btns",
                children: [
                  e.jsx(Button, { variant: "outline", disabled: filtering, onClick: () => setCollapsed(new Set()), children: "Expand all" }),
                  e.jsx(Button, { variant: "outline", disabled: filtering, onClick: () => setCollapsed(new Set(parentsWithKids.map((m) => m.menuId))), children: "Collapse all" })
                ]
              })
            ]
          }),
          e.jsx("div", {
            className: "mn-wrap",
            children: e.jsxs("table", {
              className: "mn-table",
              children: [
                e.jsx("thead", {
                  children: e.jsxs("tr", {
                    children: [
                      e.jsx("th", { children: "Menu" }),
                      e.jsx("th", { children: "Route" }),
                      e.jsx("th", { className: "mn-num", children: "Order" }),
                      e.jsx("th", { children: "Status" }),
                      e.jsx("th", { style: { textAlign: "right" }, children: "Actions" })
                    ]
                  })
                }),
                e.jsx("tbody", {
                  children: rows.length === 0
                    ? e.jsx("tr", { children: e.jsx("td", { colSpan: 5, className: "mn-empty", children: filtering ? "No menus match the filters." : "No menus in this portal yet." }) })
                    : rows.map(({ menu, depth, kids, open, dim }) =>
                        e.jsx(MenuRow, {
                          menu, depth, kids, open, dim, term,
                          onToggle: () => toggle(menu.menuId),
                          canAdd: canCreate && menu.menuLevel < MAX_LEVEL,
                          canEdit, canDelete,
                          onAdd: () => setEditing({ parentId: menu.menuId }),
                          onEdit: () => setEditing({ menu }),
                          onDelete: () => setDeleting(menu)
                        }, menu.menuId)
                      )
                })
              ]
            })
          })
        ]
      }),
      editing &&
        e.jsx(MenuForm, {
          menu: editing.menu ?? null,
          presetParentId: editing.parentId ?? null,
          presetPortal: portal,
          allMenus: menus,
          byId,
          onClose: () => setEditing(null),
          onSaved: (saved) => {
            setEditing(null);
            if (saved?.parentMenuId) {
              setCollapsed((current) => {
                const next = new Set(current);
                next.delete(saved.parentMenuId);
                return next;
              });
            }
            load();
          }
        }),
      e.jsx(ConfirmDialog, {
        open: deleting !== null,
        danger: true,
        confirmLabel: "Delete",
        title: `Delete "${deleting?.displayName ?? ""}"?`,
        message: "The menu disappears from every sidebar and its role mappings go with it.",
        onConfirm: async () => {
          if (!deleting) return;
          try {
            await menusApi.remove(deleting.menuId);
            toast.success("Menu deleted.");
            await load();
          } catch (error) {
            toast.error(errorText(error, "Could not delete the menu."));
          }
        },
        onClose: () => setDeleting(null)
      })
    ]
  });
}

function MenuRow({ menu, depth, kids, open, dim, term, onToggle, canAdd, canEdit, canDelete, onAdd, onEdit, onDelete }) {
  const isGroup = !menu.route;
  const knownIcon = !menu.icon || KNOWN_ICONS.has(menu.icon);
  const className = [isGroup && "mn-group", !menu.isVisible && "mn-hidden"].filter(Boolean).join(" ") || undefined;
  return e.jsxs("tr", {
    className,
    style: dim ? { opacity: 0.6 } : undefined,
    children: [
      e.jsx("td", {
        children: e.jsxs("div", {
          className: "mn-cell",
          children: [
            depth > 0 && e.jsx("span", { className: "mn-indent", "aria-hidden": true, children: Array.from({ length: depth }, (_, i) => e.jsx("span", {}, i)) }),
            e.jsx("button", {
              type: "button",
              className: `mn-toggle${kids ? "" : " mn-toggle--none"}`,
              "aria-expanded": kids ? open : undefined,
              "aria-label": kids ? `${open ? "Collapse" : "Expand"} ${menu.displayName}` : undefined,
              tabIndex: kids ? 0 : -1,
              onClick: kids ? onToggle : undefined,
              children: e.jsx(Chevron, {})
            }),
            e.jsx("span", { className: "mn-icon", title: menu.icon ? `Icon: ${menu.icon}` : "No icon", children: e.jsx(Icon, { name: menu.icon, size: isGroup ? 14 : 16 }) }),
            e.jsxs("div", {
              className: "mn-name",
              children: [
                e.jsxs("div", {
                  className: "mn-name__title",
                  children: [e.jsx(Highlight, { text: menu.displayName, term }), kids > 0 && e.jsx("span", { className: "mn-count", title: `${kids} sub-menus`, children: kids })]
                }),
                e.jsxs("div", {
                  className: "mn-name__code",
                  children: [
                    e.jsx(Highlight, { text: menu.menuCode, term }),
                    !knownIcon && e.jsx("span", { className: "mn-warn", title: `Icon "${menu.icon}" is not in the sidebar's icon set, so the sidebar shows a grid icon. Pick one from the list in Edit.`, children: " · ⚠ icon" })
                  ]
                })
              ]
            })
          ]
        })
      }),
      e.jsx("td", {
        children: menu.route
          ? e.jsx("span", { className: "mn-route", title: menu.route, children: e.jsx(Highlight, { text: menu.route, term }) })
          : e.jsx("span", { className: "mn-muted", children: "Group — no page" })
      }),
      e.jsx("td", { className: "mn-num", children: menu.displayOrder }),
      e.jsx("td", {
        children: e.jsx(Badge, { tone: menu.isVisible ? "success" : "neutral", children: menu.isVisible ? "Visible" : "Hidden" })
      }),
      e.jsx("td", {
        children: e.jsxs("div", {
          className: "mn-actions",
          children: [
            canAdd && e.jsx(Button, { size: "sm", variant: "ghost", onClick: onAdd, "aria-label": `Add a sub-menu under ${menu.displayName}`, children: "+ Add" }),
            canEdit && e.jsx(Button, { size: "sm", variant: "outline", onClick: onEdit, children: "Edit" }),
            canDelete && e.jsx(Button, {
              size: "sm",
              variant: "ghost",
              disabled: kids > 0,
              title: kids > 0 ? "Move or delete its sub-menus first" : undefined,
              onClick: onDelete,
              children: "Delete"
            })
          ]
        })
      })
    ]
  });
}

// ── Create / edit form ──────────────────────────────────────────────────────

function MenuForm({ menu, presetParentId, presetPortal, allMenus, byId, onClose, onSaved }) {
  const toast = useToast();
  const [saving, setSaving] = React.useState(false);
  const presetParent = presetParentId ? byId.get(presetParentId) : null;
  const portalOf = menu?.portalType ?? presetParent?.portalType ?? presetPortal;

  const nextOrder = (parentId, portalType) => {
    const siblings = allMenus.filter((m) => m.portalType === portalType && (m.parentMenuId ?? null) === (parentId ?? null));
    return siblings.length ? Math.max(...siblings.map((m) => m.displayOrder)) + 10 : 10;
  };

  const [form, setForm] = React.useState({
    menuCode: menu?.menuCode ?? "",
    displayName: menu?.displayName ?? "",
    route: menu?.route ?? "",
    icon: menu?.icon ?? "",
    displayOrder: menu?.displayOrder ?? nextOrder(presetParentId, portalOf),
    parentMenuId: menu?.parentMenuId?.toString() ?? presetParentId?.toString() ?? "",
    portalType: String(portalOf),
    isVisible: menu?.isVisible ?? true
  });
  const set = (field, value) => setForm((f) => ({ ...f, [field]: value }));

  // A parent must be in the same portal, above the deepest level, and never the menu
  // itself or one of its own descendants (that would make a loop).
  const portalMenus = allMenus.filter((m) => m.portalType === Number(form.portalType));
  const tree = buildTree(portalMenus);
  const blocked = menu ? new Set([menu.menuId, ...descendantIds(menu.menuId, tree.childrenOf)]) : new Set();
  const parentOptions = [];
  const walk = (list) =>
    list.forEach((m) => {
      if (!blocked.has(m.menuId) && m.menuLevel < MAX_LEVEL) {
        parentOptions.push({ value: String(m.menuId), label: pathOf(m, byId) });
      }
      walk(tree.childrenOf(m.menuId));
    });
  walk(tree.roots);

  const iconOptions = [
    ...(form.icon && !KNOWN_ICONS.has(form.icon) ? [{ value: form.icon, label: `${form.icon} (not drawn — shows a grid)` }] : []),
    ...SIDEBAR_ICONS.map((name) => ({ value: name, label: name }))
  ];

  const parent = form.parentMenuId ? byId.get(Number(form.parentMenuId)) : null;
  const previewPath = [parent ? pathOf(parent, byId) : null, form.displayName.trim() || "New menu"].filter(Boolean).join(" › ");

  const codeValid = /^[A-Z0-9_]+$/.test(form.menuCode.trim());
  const valid = form.displayName.trim().length > 0 && (menu || codeValid) && Number.isFinite(Number(form.displayOrder));

  const save = async () => {
    setSaving(true);
    const body = {
      parentMenuId: form.parentMenuId ? Number(form.parentMenuId) : null,
      menuName: form.displayName.trim(),
      displayName: form.displayName.trim(),
      route: form.route.trim() || null,
      icon: form.icon.trim() || null,
      displayOrder: Number(form.displayOrder),
      permissionId: menu?.permissionId ?? null,
      portalType: Number(form.portalType),
      isVisible: form.isVisible
    };
    try {
      const saved = menu
        ? await menusApi.update(menu.menuId, body)
        : await menusApi.create({ ...body, menuCode: form.menuCode.trim().toUpperCase() });
      toast.success(menu ? "Menu updated." : "Menu created. Map it to roles to make it visible.");
      onSaved(saved ?? body);
    } catch (error) {
      toast.error(errorText(error, "Could not save the menu."));
    } finally {
      setSaving(false);
    }
  };

  return e.jsx(Modal, {
    open: true,
    title: menu ? `Edit — ${menu.displayName}` : presetParent ? `New sub-menu under ${presetParent.displayName}` : "New menu",
    onClose,
    footer: e.jsxs(e.Fragment, {
      children: [
        e.jsx(Button, { variant: "ghost", onClick: onClose, children: "Cancel" }),
        e.jsx(Button, { onClick: save, loading: saving, disabled: !valid, children: "Save" })
      ]
    }),
    children: e.jsxs("div", {
      className: "form-stack",
      children: [
        e.jsxs("div", {
          className: "mn-preview",
          children: [
            e.jsx("span", { className: "mn-icon", children: e.jsx(Icon, { name: form.icon, size: 16 }) }),
            e.jsxs("div", {
              children: [
                e.jsx("div", { style: { fontWeight: 600 }, children: form.displayName.trim() || "New menu" }),
                e.jsx("div", { className: "mn-preview__path", children: `${form.portalType === "1" ? "Customer" : "Admin"} sidebar: ${previewPath}` })
              ]
            })
          ]
        }),
        e.jsxs("div", {
          className: "form-grid",
          children: [
            e.jsx(Input, { label: "Display name", required: true, value: form.displayName, onChange: (ev) => set("displayName", ev.target.value) }),
            menu
              ? e.jsx(Input, { label: "Menu code", value: form.menuCode, disabled: true, readOnly: true, hint: "Fixed once created" })
              : e.jsx(Input, {
                  label: "Menu code",
                  required: true,
                  value: form.menuCode,
                  hint: "Letters, digits and _ — e.g. ADM_REPORTS",
                  error: form.menuCode && !codeValid ? "Use A–Z, 0–9 and _ only." : undefined,
                  onChange: (ev) => set("menuCode", ev.target.value.toUpperCase().replace(/\s+/g, "_"))
                }),
            e.jsx(Select, {
              label: "Portal",
              value: form.portalType,
              options: PORTALS,
              disabled: Boolean(menu),
              onChange: (ev) => setForm((f) => ({ ...f, portalType: ev.target.value, parentMenuId: "", displayOrder: nextOrder(null, Number(ev.target.value)) }))
            }),
            e.jsx(Select, {
              label: "Parent",
              value: form.parentMenuId,
              placeholder: "— top level —",
              options: parentOptions,
              onChange: (ev) => setForm((f) => ({
                ...f,
                parentMenuId: ev.target.value,
                displayOrder: menu ? f.displayOrder : nextOrder(ev.target.value ? Number(ev.target.value) : null, Number(f.portalType))
              }))
            }),
            e.jsx(Input, { label: "Route", value: form.route, placeholder: "/admin/…", hint: "Leave empty for a group heading", onChange: (ev) => set("route", ev.target.value) }),
            e.jsx(Select, { label: "Icon", value: form.icon, placeholder: "No icon", options: iconOptions, onChange: (ev) => set("icon", ev.target.value) }),
            e.jsx(Input, { label: "Display order", type: "number", step: "10", value: form.displayOrder, hint: "Lower shows first among its siblings", onChange: (ev) => set("displayOrder", ev.target.value) })
          ]
        }),
        e.jsx(Checkbox, { label: "Visible in the sidebar", checked: form.isVisible, onChange: (ev) => set("isVisible", ev.target.checked) })
      ]
    })
  });
}

export { MenusPage };
