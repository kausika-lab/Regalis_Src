/**
 * Admin -> Calendar: the one day-timeline both portals read (Design Invariant 1).
 * A block written here drops the slot out of customer availability immediately.
 *
 * This repo ships the built SPA only, so this chunk is hand-maintained: keep the
 * file name (the route in index-DI5X4c9f.js imports it by name) and bump its
 * revision in sw.js, or the service worker keeps serving the cached copy.
 */
import {
  a as useToast,
  R as usePermission,
  r as React,
  z as locationsApi,
  y as workspaceTypesApi,
  A as ApiError,
  j as X,
  p as Button,
  I as Input,
  F as Select,
  G as Spinner
} from "./index-DI5X4c9f.js";
import { c as calendarApi } from "./bookings-DrKATIWm.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { C as Card } from "./Card-CNXipofE.js";
import { M as Modal } from "./Modal-BtdVDo1B.js";
import { P as PageHeader } from "./PageHeader-Y96MMVry.js";
import { B as BOOKING_STATUS } from "./bookings-CeZsOhV5.js";
import { f as formatDateTime } from "./formatters-DFe5xCRi.js";
import { C as IconChevronLeft, a as IconChevronRight } from "./chevron-right-BikENMwC.js";

/* ---------------------------------------------------------------- helpers */

/** jsx(...) / jsxs(...) without a JSX compiler; `key` is lifted to the 3rd arg. */
function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  if (kids.length === 1) p.children = kids[0];
  else if (kids.length > 1) p.children = kids;
  return kids.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}

const F = X.Fragment;

const BLOCK_REASONS = {
  1: "Maintenance",
  2: "Internal use",
  3: "Cleaning",
  4: "Repair",
  5: "Private event",
  6: "Housekeeping",
  7: "Other"
};

const DAY_START_HOUR = 7;
const DAY_END_HOUR = 22;
const HOURS = Array.from({ length: DAY_END_HOUR - DAY_START_HOUR }, (_, i) => DAY_START_HOUR + i);
const SPAN_MINUTES = (DAY_END_HOUR - DAY_START_HOUR) * 60;
const IST_OFFSET_MS = 5.5 * 60 * 60 * 1000;
/** Keep in step with .cal__label width in the stylesheet — the NOW line offsets past it. */
const LABEL_WIDTH_PX = 160;

/** The API sends naked UTC timestamps; treat an offset-less string as UTC. */
function parseUtc(value) {
  if (value instanceof Date) return value;
  return new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(value) ? value : `${value}Z`);
}

/** Same instant shifted into IST, so the getUTC* accessors read as wall clock. */
function istClock(value) {
  return new Date(parseUtc(value).getTime() + IST_OFFSET_MS);
}

function istToday() {
  return new Date(Date.now() + IST_OFFSET_MS).toISOString().slice(0, 10);
}

function istMinutesNow() {
  const now = new Date(Date.now() + IST_OFFSET_MS);
  return now.getUTCHours() * 60 + now.getUTCMinutes();
}

function shiftDate(dateString, days) {
  const date = new Date(`${dateString}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** An IST wall-clock date + time as the true UTC instant the API expects. */
function istToUtcIso(dateString, timeString) {
  return new Date(new Date(`${dateString}T${timeString}:00Z`).getTime() - IST_OFFSET_MS).toISOString();
}

function hourOffset(hour) {
  return ((hour - DAY_START_HOUR) / (DAY_END_HOUR - DAY_START_HOUR)) * 100;
}

/** Where a booking or block sits on the 07:00–22:00 IST track. */
function itemStyle(item) {
  const start = istClock(item.startUtc);
  const end = istClock(item.endUtc);
  const dayStart = Date.UTC(
    start.getUTCFullYear(),
    start.getUTCMonth(),
    start.getUTCDate(),
    DAY_START_HOUR
  );
  const from = Math.max(0, (start.getTime() - dayStart) / 60000);
  const to = Math.min(SPAN_MINUTES, (end.getTime() - dayStart) / 60000);
  return {
    left: `${(from / SPAN_MINUTES) * 100}%`,
    width: `${Math.max(1.5, ((to - from) / SPAN_MINUTES) * 100)}%`
  };
}

/** Workspaces banded by type, in the order the API returned them. */
function groupByType(workspaces) {
  const groups = [];
  const byName = new Map();
  for (const workspace of workspaces) {
    const name = workspace.workspaceTypeName || "Workspaces";
    let group = byName.get(name);
    if (!group) {
      group = { name, workspaces: [] };
      byName.set(name, group);
      groups.push(group);
    }
    group.workspaces.push(workspace);
  }
  return groups;
}

function message(error, fallback) {
  return error instanceof ApiError ? error.message : fallback;
}

/* ------------------------------------------------------------------- page */

function AdminCalendarPage() {
  const toast = useToast();
  const canBlock = usePermission("CALENDAR.CREATE");
  const canRelease = usePermission("CALENDAR.EDIT");

  const [locations, setLocations] = React.useState([]);
  const [types, setTypes] = React.useState([]);
  const [date, setDate] = React.useState(() => istToday());
  const [locationId, setLocationId] = React.useState("");
  const [typeId, setTypeId] = React.useState("");
  const [calendar, setCalendar] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [selected, setSelected] = React.useState(null);
  const [blocking, setBlocking] = React.useState(false);

  React.useEffect(() => {
    Promise.all([locationsApi.list({ pageSize: 100 }), workspaceTypesApi.list()])
      .then(([locationPage, typeList]) => {
        setLocations(locationPage.items);
        setTypes(typeList);
        const preferred = locationPage.items.find((l) => l.isDefault) ?? locationPage.items[0];
        if (preferred) setLocationId(String(preferred.locationId));
      })
      .catch(() => toast.error("Failed to load reference data."));
  }, []);

  const load = React.useCallback(async () => {
    if (!locationId) return;
    setLoading(true);
    try {
      setCalendar(
        await calendarApi.get("day", date, {
          locationId: Number(locationId),
          workspaceTypeId: typeId ? Number(typeId) : void 0
        })
      );
    } catch (error) {
      toast.error(message(error, "Failed to load the calendar."));
    } finally {
      setLoading(false);
    }
  }, [date, locationId, typeId]);

  React.useEffect(() => {
    load();
  }, [load]);

  // The NOW marker only means anything on today's column.
  const [nowMinutes, setNowMinutes] = React.useState(() => istMinutesNow());
  React.useEffect(() => {
    const timer = setInterval(() => setNowMinutes(istMinutesNow()), 60000);
    return () => clearInterval(timer);
  }, []);
  const showNow =
    date === istToday() && nowMinutes >= DAY_START_HOUR * 60 && nowMinutes <= DAY_END_HOUR * 60;
  const nowRatio = (nowMinutes - DAY_START_HOUR * 60) / SPAN_MINUTES;

  const groups = calendar ? groupByType(calendar.workspaces) : [];

  const legend = el(
    "div",
    { className: "cal-legend" },
    el(
      "span",
      { className: "cal-legend__item" },
      el("i", { className: "cal-legend__dot cal-legend__dot--booking" }),
      "Confirmed"
    ),
    el(
      "span",
      { className: "cal-legend__item" },
      el("i", { className: "cal-legend__dot cal-legend__dot--block" }),
      "Blocked"
    ),
    el(
      "span",
      { className: "cal-legend__item" },
      el("i", { className: "cal-legend__dot cal-legend__dot--free" }),
      "Available"
    )
  );

  const grid = el(
    "div",
    { className: "table-scroll" },
    el(
      "div",
      { className: "cal" },
      showNow
        ? el(
            "div",
            {
              className: "cal__now",
              style: {
                left: `calc(${LABEL_WIDTH_PX}px + (100% - ${LABEL_WIDTH_PX}px) * ${nowRatio})`
              },
              "aria-hidden": true
            },
            el("span", { className: "cal__now-label" }, "NOW")
          )
        : null,
      el(
        "div",
        { className: "cal__row cal__row--head" },
        el("div", { className: "cal__label" }),
        el(
          "div",
          { className: "cal__timeline" },
          HOURS.map((hour) =>
            el("span", { key: hour, className: "cal__hour", style: { left: `${hourOffset(hour)}%` } }, hour + ":00")
          )
        )
      ),
      groups.map((group) =>
        el(
          F,
          { key: group.name },
          el("div", { className: "cal__group" }, group.name),
          group.workspaces.map((workspace) =>
            el(
              "div",
              { key: workspace.workspaceId, className: "cal__row" },
              el(
                "div",
                { className: "cal__label" },
                el("div", { style: { fontWeight: 600 } }, workspace.workspaceCode),
                el(
                  "div",
                  { style: { color: "var(--text-muted)", fontSize: "var(--font-size-xs)" } },
                  workspace.workspaceName
                )
              ),
              el(
                "div",
                { className: "cal__timeline" },
                HOURS.map((hour) =>
                  el("span", {
                    key: hour,
                    className: "cal__grid-line",
                    style: { left: `${hourOffset(hour)}%` }
                  })
                ),
                workspace.items.map((item) =>
                  el(
                    "button",
                    {
                      key: `${item.kind}-${item.referenceId}`,
                      type: "button",
                      className:
                        `cal__item cal__item--${item.kind}` +
                        (item.bookingStatus === 1 ? " cal__item--pending" : ""),
                      style: itemStyle(item),
                      title: item.title,
                      onClick: () => setSelected(item)
                    },
                    item.title
                  )
                )
              )
            )
          )
        )
      ),
      groups.length === 0
        ? el(
            "p",
            { style: { padding: "var(--space-6)", color: "var(--text-muted)" } },
            "No workspaces match the filters."
          )
        : null
    )
  );

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Calendar",
      description:
        "One calendar for both portals — a block here removes the slot from customer availability instantly.",
      actions: canBlock
        ? el(Button, { onClick: () => setBlocking(true) }, "+ Block Workspace")
        : null
    }),

    el(
      Card,
      null,
      el(
        "div",
        { className: "calbar" },
        el(
          "div",
          { className: "calbar__field" },
          el("span", { className: "calbar__label" }, "Date"),
          el(
            "div",
            { className: "calbar__date" },
            el(
              "button",
              {
                type: "button",
                className: "calbar__step",
                "aria-label": "Previous day",
                onClick: () => setDate((current) => shiftDate(current, -1))
              },
              el(IconChevronLeft, { size: 16 })
            ),
            el("input", {
              className: "field__input",
              type: "date",
              "aria-label": "Calendar date",
              value: date,
              onChange: (event) => setDate(event.target.value)
            }),
            el(
              "button",
              {
                type: "button",
                className: "calbar__step",
                "aria-label": "Next day",
                onClick: () => setDate((current) => shiftDate(current, 1))
              },
              el(IconChevronRight, { size: 16 })
            )
          )
        ),
        el(
          "div",
          { className: "calbar__field" },
          el("span", { className: "calbar__label" }, "Location"),
          el(
            "select",
            {
              className: "field__input",
              "aria-label": "Location",
              value: locationId,
              onChange: (event) => setLocationId(event.target.value)
            },
            locations.map((location) =>
              el("option", { key: location.locationId, value: location.locationId }, location.locationName)
            )
          )
        ),
        el(
          "div",
          { className: "calbar__field" },
          el("span", { className: "calbar__label" }, "Type"),
          el(
            "select",
            {
              className: "field__input",
              "aria-label": "Workspace type",
              value: typeId,
              onChange: (event) => setTypeId(event.target.value)
            },
            el("option", { value: "" }, "All types"),
            types.map((type) =>
              el("option", { key: type.workspaceTypeId, value: type.workspaceTypeId }, type.typeName)
            )
          )
        ),
        legend
      )
    ),

    el(Card, null, loading && !calendar ? el(Spinner, { center: true }) : grid),

    el(
      Modal,
      {
        open: selected !== null,
        title: selected?.kind === "block" ? "Workspace block" : "Booking",
        onClose: () => setSelected(null),
        footer:
          selected?.kind === "block" && canRelease
            ? el(
                Button,
                {
                  variant: "danger",
                  onClick: async () => {
                    try {
                      await calendarApi.unblock(selected.referenceId);
                      toast.success("Block released. The slot is bookable again.");
                      setSelected(null);
                      await load();
                    } catch (error) {
                      toast.error(message(error, "Unblock failed."));
                    }
                  }
                },
                "Release block"
              )
            : void 0
      },
      selected
        ? el(
            "div",
            { className: "summary-rows" },
            el("div", null, el("span", null, "What"), el("strong", null, selected.title)),
            el("div", null, el("span", null, "From"), el("span", null, formatDateTime(selected.startUtc))),
            el("div", null, el("span", null, "To"), el("span", null, formatDateTime(selected.endUtc))),
            selected.bookingStatus !== null
              ? el(
                  "div",
                  null,
                  el("span", null, "Status"),
                  el(Badge, { tone: "info" }, BOOKING_STATUS[selected.bookingStatus])
                )
              : null,
            selected.blockReason !== null
              ? el(
                  "div",
                  null,
                  el("span", null, "Reason"),
                  el("span", null, BLOCK_REASONS[selected.blockReason])
                )
              : null
          )
        : null
    ),

    blocking && calendar
      ? el(BlockDialog, {
          date,
          workspaces: calendar.workspaces,
          onClose: () => setBlocking(false),
          onDone: () => {
            setBlocking(false);
            load();
          }
        })
      : null
  );
}

/* --------------------------------------------------------------- blocking */

function BlockDialog({ date, workspaces, onClose, onDone }) {
  const toast = useToast();
  const [workspaceId, setWorkspaceId] = React.useState("");
  const [from, setFrom] = React.useState("09:00");
  const [to, setTo] = React.useState("12:00");
  const [reason, setReason] = React.useState("1");
  const [notes, setNotes] = React.useState("");
  const [saving, setSaving] = React.useState(false);

  const save = async () => {
    setSaving(true);
    try {
      await calendarApi.block({
        workspaceId: Number(workspaceId),
        startUtc: istToUtcIso(date, from),
        endUtc: istToUtcIso(date, to),
        blockReason: Number(reason),
        reasonNotes: notes || null
      });
      toast.success("Workspace blocked. Customer availability updated immediately.");
      onDone();
    } catch (error) {
      toast.error(message(error, "Block failed — check for overlapping bookings."));
    } finally {
      setSaving(false);
    }
  };

  return el(
    Modal,
    {
      open: true,
      title: `Block workspace · ${date}`,
      onClose,
      footer: el(
        F,
        null,
        el(Button, { variant: "ghost", onClick: onClose }, "Cancel"),
        el(Button, { onClick: save, loading: saving, disabled: !workspaceId }, "Block")
      )
    },
    el(
      "div",
      { className: "form-stack" },
      el(Select, {
        label: "Workspace",
        required: true,
        value: workspaceId,
        placeholder: "Select workspace",
        options: workspaces.map((workspace) => ({
          value: workspace.workspaceId,
          label: `${workspace.workspaceName} (${workspace.workspaceCode})`
        })),
        onChange: (event) => setWorkspaceId(event.target.value)
      }),
      el(
        "div",
        { className: "form-grid" },
        el(Input, {
          label: "From (IST)",
          type: "time",
          value: from,
          onChange: (event) => setFrom(event.target.value)
        }),
        el(Input, {
          label: "To (IST)",
          type: "time",
          value: to,
          onChange: (event) => setTo(event.target.value)
        }),
        el(Select, {
          label: "Reason",
          value: reason,
          options: Object.entries(BLOCK_REASONS).map(([value, label]) => ({ value, label })),
          onChange: (event) => setReason(event.target.value)
        })
      ),
      el(Input, {
        label: "Notes",
        value: notes,
        onChange: (event) => setNotes(event.target.value)
      }),
      el(
        "p",
        { style: { fontSize: "var(--font-size-xs)", color: "var(--text-muted)" } },
        "A window with confirmed bookings cannot be blocked — cancel or reschedule them first."
      )
    )
  );
}

export { AdminCalendarPage };
