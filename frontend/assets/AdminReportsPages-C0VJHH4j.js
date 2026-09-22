import{a as C,r as a,j as e,G as I,g as T,ag as B,Y as G,R as H,A as b,z as U,y as X,p as x,I as N,F as k}from"./index-DI5X4c9f.js";import{r as u}from"./reports-CNtuzowc.js";import{C as Y}from"./Card-CNXipofE.js";import{P as _}from"./PageHeader-Y96MMVry.js";import{f as q,c as J,b as K}from"./formatters-DFe5xCRi.js";import{D as A}from"./download-Dlifs1Q2.js";

/**
 * Admin -> Reports & Analysis.
 *
 *   AdminReportsPage  the analytics overview + the report library (below, readable)
 *   ReportShellPage   the single report shell — left as built, further down
 *
 * This repo ships the built SPA only, so this chunk is hand-maintained: keep the
 * file name (the route in index-DI5X4c9f.js imports it by name) and bump its
 * revision in sw.js, or the service worker keeps serving the cached copy.
 */
import {
  W as IconWallet,
  q as IconCalendarDays,
  U as IconUserRound,
  H as IconCard,
  T as IconTicket,
  v as IconHelp,
  Q as IconQr,
  ae as IconClock,
  a1 as IconCoffee
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";

/* The build aliases every import to a single letter; name the ones used below. */
const JSX = e;
const React = a;
const useToast = C;
const usePermission = H;
const ApiError = b;
const Spinner = I;
const Button = x;
const Link = T;
const Card = Y;
const PageHeader = _;
const reportsApi = u;
const workspaceTypesApi = X;
const money = K;
const IconDownload = A;
const IconChartColumn = B;

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
  return kids.length > 1 ? JSX.jsxs(type, p, key) : JSX.jsx(type, p, key);
}

const F = JSX.Fragment;

const RIcons = {
  wallet: IconWallet,
  calendar: IconCalendarDays,
  chart: IconChartColumn,
  user: IconUserRound,
  card: IconCard,
  ticket: IconTicket,
  help: IconHelp,
  qr: IconQr,
  clock: IconClock,
  coffee: IconCoffee
};

/** "18 Aug" — bucket keys are plain dates, so read them as UTC. */
function formatDayLabel(dateString) {
  return new Date(`${dateString.slice(0, 10)}T00:00:00Z`).toLocaleDateString("en-IN", {
    timeZone: "UTC",
    day: "2-digit",
    month: "short"
  });
}

/** Axis ticks round to clean numbers: 1 / 2 / 2.5 / 5 x 10^n. */
function niceCeiling(value) {
  if (value <= 0) return 1;
  const magnitude = Math.pow(10, Math.floor(Math.log10(value)));
  for (const step of [1, 2, 2.5, 5, 10]) {
    if (value <= step * magnitude) return step * magnitude;
  }
  return 10 * magnitude;
}

/** Short money for an axis tick: 0, 12k, 2.4L (Indian scale). */
function compactMoney(value) {
  if (value === 0) return "0";
  if (value >= 1e7) return (value / 1e7).toFixed(value >= 1e8 ? 0 : 1) + "Cr";
  if (value >= 1e5) return (value / 1e5).toFixed(value >= 1e6 ? 0 : 1) + "L";
  if (value >= 1000) return Math.round(value / 1000) + "k";
  return String(Math.round(value));
}

/** "Aug 26" */
function formatMonth(dateString) {
  return new Date(`${dateString.slice(0, 10)}T00:00:00Z`).toLocaleDateString("en-IN", {
    timeZone: "UTC",
    month: "short",
    year: "2-digit"
  });
}

/* ------------------------------------------------- reports & analysis home */

/**
 * The analytics half of this page is derived from the report catalogue itself:
 * report keys are resolved by title, and every figure is read off the tabular
 * payload by COLUMN TYPE (money / percent / date / text) rather than by column
 * name, so a report can rename its columns without breaking the dashboard.
 * A report the build does not ship simply leaves its widget empty.
 */

const PERIODS = [
  { value: "7", label: "Last 7 Days" },
  { value: "30", label: "Last 30 Days" },
  { value: "90", label: "Last 90 Days" }
];

const IST_OFFSET_MS = 5.5 * 60 * 60 * 1000;
/** Categorical slots live in the stylesheet so dark mode gets its own steps. */
const CATEGORY_COLORS = ["var(--viz-1)", "var(--viz-2)", "var(--viz-3)", "var(--viz-4)", "var(--viz-5)"];
const OTHER_COLOR = "var(--viz-other)";
const MAX_SLICES = 5;

const REPORT_ICONS = [
  [/revenue|expense/i, RIcons.wallet],
  [/booking|calendar/i, RIcons.calendar],
  [/occupancy/i, RIcons.chart],
  [/customer/i, RIcons.user],
  [/invoice|payment/i, RIcons.card],
  [/pass|membership/i, RIcons.ticket],
  [/support|ticket/i, RIcons.help],
  [/access|qr/i, RIcons.qr],
  [/check-?in|attendance/i, RIcons.clock],
  [/service|coffee/i, RIcons.coffee]
];

function reportIcon(title) {
  for (const [pattern, icon] of REPORT_ICONS) if (pattern.test(title)) return icon;
  return RIcons.chart;
}

function istToday() {
  return new Date(Date.now() + IST_OFFSET_MS).toISOString().slice(0, 10);
}

function shiftDays(dateString, days) {
  const date = new Date(`${dateString}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

/** The window the filters describe, plus the one immediately before it. */
function periodRange(days) {
  const to = istToday();
  const from = shiftDays(to, -(days - 1));
  return { from, to, previousFrom: shiftDays(from, -days), previousTo: shiftDays(from, -1) };
}

function findReport(catalogue, pattern) {
  const hit = catalogue.find((report) => report.isAvailable && pattern.test(report.title));
  return hit ? hit.key : null;
}

/** First column of a type, preferring one whose header matches. */
function columnOf(report, type, prefer) {
  if (!report) return null;
  const typed = report.columns.filter((column) => column.type === type);
  if (typed.length === 0) return null;
  if (prefer) {
    const preferred = typed.find((column) => prefer.test(column.header));
    if (preferred) return preferred;
  }
  return typed[0];
}

function numberAt(row, column) {
  if (!column) return 0;
  const value = Number(row[column.key]);
  return Number.isFinite(value) ? value : 0;
}

function sumColumn(report, column) {
  if (!report || !column) return null;
  return report.rows.reduce((total, row) => total + numberAt(row, column), 0);
}

function averageColumn(report, column) {
  if (!report || !column || report.rows.length === 0) return null;
  return sumColumn(report, column) / report.rows.length;
}

/** Bucket key for the trend toggle: a day, the week's Monday, or the month. */
function bucketOf(dateString, mode) {
  const date = new Date(`${dateString.slice(0, 10)}T00:00:00Z`);
  if (mode === "monthly") return dateString.slice(0, 7) + "-01";
  if (mode === "weekly") {
    const weekday = (date.getUTCDay() + 6) % 7;
    date.setUTCDate(date.getUTCDate() - weekday);
    return date.toISOString().slice(0, 10);
  }
  return date.toISOString().slice(0, 10);
}

/** Money summed per time bucket, oldest first. */
function trendSeries(report, mode) {
  const dateColumn = columnOf(report, "date") ?? columnOf(report, "datetime");
  const moneyColumn = columnOf(report, "money", /total|amount|revenue|net/i);
  if (!report || !dateColumn || !moneyColumn) return [];
  const buckets = new Map();
  for (const row of report.rows) {
    const raw = row[dateColumn.key];
    if (!raw) continue;
    const bucket = bucketOf(String(raw), mode);
    buckets.set(bucket, (buckets.get(bucket) ?? 0) + numberAt(row, moneyColumn));
  }
  return [...buckets.entries()]
    .sort((a, b) => (a[0] < b[0] ? -1 : 1))
    .map(([date, value]) => ({ date, value }));
}

/** Top slices by value with the tail folded into Other — never a generated hue. */
function breakdown(report, valueColumn, labelPreference) {
  const labelColumn = columnOf(report, "text", labelPreference);
  if (!report || !labelColumn) return [];
  const totals = new Map();
  for (const row of report.rows) {
    const label = String(row[labelColumn.key] ?? "—");
    const value = valueColumn ? numberAt(row, valueColumn) : 1;
    totals.set(label, (totals.get(label) ?? 0) + value);
  }
  const sorted = [...totals.entries()]
    .map(([label, value]) => ({ label, value }))
    .sort((a, b) => b.value - a.value);
  if (sorted.length <= MAX_SLICES) return sorted;
  const head = sorted.slice(0, MAX_SLICES);
  const tail = sorted.slice(MAX_SLICES).reduce((total, slice) => total + slice.value, 0);
  return tail > 0 ? head.concat([{ label: "Other", value: tail, isOther: true }]) : head;
}

function percentDelta(current, previous) {
  if (current == null || previous == null || previous === 0) return null;
  return ((current - previous) / previous) * 100;
}

/** Width of the plot area, so marks stay crisp instead of being scaled. */
function useElementWidth(fallback) {
  const ref = React.useRef(null);
  const [width, setWidth] = React.useState(fallback);
  React.useEffect(() => {
    const node = ref.current;
    if (!node || typeof ResizeObserver === "undefined") return;
    const observer = new ResizeObserver((entries) => {
      const next = Math.round(entries[0].contentRect.width);
      if (next > 0) setWidth(next);
    });
    observer.observe(node);
    return () => observer.disconnect();
  }, []);
  return [ref, width];
}

/* ------------------------------------------------------------------ charts */

function ChartTooltip({ point }) {
  if (!point) return null;
  return el(
    "div",
    {
      className: "viz-tip",
      style: { left: point.x + "px", top: point.y + "px" },
      role: "status"
    },
    el("div", { className: "viz-tip__label" }, point.label),
    el("div", { className: "viz-tip__value" }, point.value)
  );
}

function TrendChart({ series, mode }) {
  const [ref, width] = useElementWidth(680);
  const [hover, setHover] = React.useState(null);
  const height = 210;
  const padding = { top: 16, right: 12, bottom: 26, left: 58 };

  if (series.length === 0) {
    return el("div", { className: "viz-empty" }, "No revenue in this period.");
  }

  const plotWidth = Math.max(60, width - padding.left - padding.right);
  const plotHeight = height - padding.top - padding.bottom;
  // Zero-based: an area fill that starts anywhere else overstates the movement.
  const max = niceCeiling(Math.max(...series.map((point) => point.value), 1));
  const step = series.length > 1 ? plotWidth / (series.length - 1) : 0;
  const pointAt = (index) => ({
    x: padding.left + index * step,
    y: padding.top + plotHeight - (series[index].value / max) * plotHeight
  });
  const points = series.map((_, index) => pointAt(index));
  const line = points.map((point, i) => `${i === 0 ? "M" : "L"}${point.x} ${point.y}`).join(" ");
  const area =
    `${line} L${points[points.length - 1].x} ${padding.top + plotHeight}` +
    ` L${points[0].x} ${padding.top + plotHeight} Z`;

  const ticks = [0, 0.25, 0.5, 0.75, 1]
    .map((fraction) => Math.round((series.length - 1) * fraction))
    .filter((index, position, all) => all.indexOf(index) === position);

  const label = (point) =>
    mode === "monthly"
      ? formatMonth(point.date)
      : formatDayLabel(point.date);

  const onMove = (event) => {
    const box = event.currentTarget.getBoundingClientRect();
    const x = event.clientX - box.left;
    const index = Math.max(
      0,
      Math.min(series.length - 1, step === 0 ? 0 : Math.round((x - padding.left) / step))
    );
    const point = pointAt(index);
    setHover({
      index,
      x: point.x,
      y: point.y,
      label: label(series[index]),
      value: money(series[index].value)
    });
  };

  return el(
    "div",
    { className: "viz", ref },
    el(
      "div",
      { className: "viz-plot", onMouseMove: onMove, onMouseLeave: () => setHover(null) },
      el(
        "svg",
        {
          width: "100%",
          height,
          viewBox: `0 0 ${width} ${height}`,
          role: "img",
          "aria-label": "Revenue trend"
        },
        [0, 0.5, 1].map((fraction) =>
          el(
            "g",
            { key: fraction },
            el("line", {
              x1: padding.left,
              x2: padding.left + plotWidth,
              y1: padding.top + plotHeight * (1 - fraction),
              y2: padding.top + plotHeight * (1 - fraction),
              className: "viz-grid"
            }),
            el(
              "text",
              {
                x: padding.left - 8,
                y: padding.top + plotHeight * (1 - fraction) + 4,
                textAnchor: "end",
                className: "viz-ytick"
              },
              compactMoney(max * fraction)
            )
          )
        ),
        el("path", { d: area, className: "viz-area" }),
        el("path", { d: line, className: "viz-line" }),
        hover
          ? el(
              "g",
              null,
              el("line", {
                x1: hover.x,
                x2: hover.x,
                y1: padding.top,
                y2: padding.top + plotHeight,
                className: "viz-crosshair"
              }),
              el("circle", { cx: hover.x, cy: hover.y, r: 5, className: "viz-dot" })
            )
          : null
      ),
      el(ChartTooltip, { point: hover })
    ),
    el(
      "div",
      { className: "viz-axis" },
      ticks.map((index) =>
        el(
          "span",
          {
            key: index,
            style: {
              left: `${((padding.left + index * step) / width) * 100}%`,
              transform:
                index === 0
                  ? "none"
                  : index === series.length - 1
                    ? "translateX(-100%)"
                    : "translateX(-50%)"
            }
          },
          label(series[index])
        )
      )
    ),
    el(
      "div",
      { className: "viz-max" },
      "Period total " + money(series.reduce((total, point) => total + point.value, 0))
    )
  );
}

function DonutChart({ slices, total }) {
  const [hover, setHover] = React.useState(null);
  const size = 150;
  const radius = 58;
  const thickness = 22;
  const centre = size / 2;
  if (slices.length === 0 || total <= 0) {
    return el("div", { className: "viz-empty" }, "Nothing to break down yet.");
  }

  // 2px of surface between segments, expressed as an angle at this radius.
  const gap = (2 / (2 * Math.PI * radius)) * 360;
  let angle = -90;
  const arcs = slices.map((slice, index) => {
    const sweep = (slice.value / total) * 360;
    const start = angle + gap / 2;
    const end = angle + sweep - gap / 2;
    angle += sweep;
    const large = end - start > 180 ? 1 : 0;
    const point = (degrees) => {
      const radians = (degrees * Math.PI) / 180;
      return [centre + radius * Math.cos(radians), centre + radius * Math.sin(radians)];
    };
    const [x1, y1] = point(Math.max(start, angle - sweep));
    const [x2, y2] = point(Math.max(end, start + 0.1));
    return {
      slice,
      index,
      d: `M${x1} ${y1} A${radius} ${radius} 0 ${large} 1 ${x2} ${y2}`,
      color: slice.isOther ? OTHER_COLOR : CATEGORY_COLORS[index % CATEGORY_COLORS.length]
    };
  });

  return el(
    "div",
    { className: "viz-donut" },
    el(
      "div",
      { className: "viz-plot viz-plot--donut" },
      el(
        "svg",
        { width: size, height: size, viewBox: `0 0 ${size} ${size}`, role: "img", "aria-label": "Revenue by category" },
        arcs.map((arc) =>
          el("path", {
            key: arc.slice.label,
            d: arc.d,
            stroke: arc.color,
            strokeWidth: hover === arc.index ? thickness + 4 : thickness,
            fill: "none",
            className: "viz-arc",
            onMouseEnter: () => setHover(arc.index),
            onMouseLeave: () => setHover(null)
          })
        )
      ),
      hover != null
        ? el(
            "div",
            { className: "viz-tip viz-tip--centre" },
            el("div", { className: "viz-tip__label" }, slices[hover].label),
            el("div", { className: "viz-tip__value" }, money(slices[hover].value))
          )
        : null
    ),
    el(
      "ul",
      { className: "viz-legend" },
      slices.map((slice, index) =>
        el(
          "li",
          { key: slice.label, className: "viz-legend__row" },
          el("i", {
            className: "viz-legend__dot",
            style: {
              background: slice.isOther ? OTHER_COLOR : CATEGORY_COLORS[index % CATEGORY_COLORS.length]
            }
          }),
          el("span", { className: "viz-legend__label" }, slice.label),
          el("span", { className: "viz-legend__value" }, Math.round((slice.value / total) * 100) + "%")
        )
      )
    )
  );
}

function BarChart({ bars }) {
  const [hover, setHover] = React.useState(null);
  const height = 190;
  if (bars.length === 0) return el("div", { className: "viz-empty" }, "No bookings in this period.");
  const max = Math.max(...bars.map((bar) => bar.value), 1);

  return el(
    "div",
    { className: "viz-bars", style: { height: height + "px" } },
    bars.map((bar, index) =>
      el(
        "div",
        {
          key: bar.label,
          className: "viz-bars__col",
          onMouseEnter: () => setHover(index),
          onMouseLeave: () => setHover(null)
        },
        el(
          "div",
          { className: "viz-bars__track" },
          hover === index
            ? el(
                "div",
                { className: "viz-tip viz-tip--bar" },
                el("div", { className: "viz-tip__label" }, bar.label),
                el("div", { className: "viz-tip__value" }, bar.value)
              )
            : null,
          el("div", {
            className: "viz-bars__bar",
            style: { height: `${Math.max(2, (bar.value / max) * 100)}%` }
          })
        ),
        el("div", { className: "viz-bars__label" }, bar.label)
      )
    )
  );
}

/* ------------------------------------------------------------------- tiles */

function KpiTile({ icon, value, label, delta, hint }) {
  const direction = delta == null ? null : delta >= 0 ? "up" : "down";
  return el(
    "div",
    { className: "kpi" },
    el(
      "div",
      { className: "kpi__top" },
      el("span", { className: "kpi__icon" }, el(icon, { size: 16 })),
      direction
        ? el(
            "span",
            { className: `kpi__delta kpi__delta--${direction}` },
            (direction === "up" ? "▲ " : "▼ ") + Math.abs(delta).toFixed(1) + "%"
          )
        : null
    ),
    el("div", { className: "kpi__value" }, value),
    el("div", { className: "kpi__label" }, label),
    hint ? el("div", { className: "kpi__hint" }, hint) : null
  );
}

/* -------------------------------------------------------------------- page */

function AdminReportsPage() {
  const toast = useToast();
  const canExport = usePermission("REPORT.EXPORT");

  const [catalogue, setCatalogue] = React.useState(null);
  const [types, setTypes] = React.useState([]);
  const [days, setDays] = React.useState("30");
  const [workspaceTypeId, setWorkspaceTypeId] = React.useState("");
  const [trendMode, setTrendMode] = React.useState("daily");
  const [reports, setReports] = React.useState(null);
  const [previous, setPrevious] = React.useState(null);
  const [loading, setLoading] = React.useState(true);
  const [exporting, setExporting] = React.useState(null);

  React.useEffect(() => {
    reportsApi
      .catalogue()
      .then(setCatalogue)
      .catch(() => toast.error("Failed to load the report catalogue."));
    workspaceTypesApi.list().then(setTypes).catch(() => {});
  }, []);

  const range = periodRange(Number(days));
  const keys = React.useMemo(() => {
    if (!catalogue) return null;
    return {
      revenue: findReport(catalogue, /revenue/i),
      booking: findReport(catalogue, /^booking/i),
      customer: findReport(catalogue, /customer/i),
      occupancy: findReport(catalogue, /occupancy/i)
    };
  }, [catalogue]);

  const filters = (from, to) => ({
    from,
    to,
    workspaceTypeId: workspaceTypeId ? Number(workspaceTypeId) : void 0
  });

  /** One report, or null when the build does not ship it / it fails. */
  const runReport = React.useCallback(
    (key, from, to) => (key ? reportsApi.get(key, filters(from, to)).catch(() => null) : Promise.resolve(null)),
    [workspaceTypeId]
  );

  React.useEffect(() => {
    if (!keys) return;
    let cancelled = false;
    setLoading(true);
    setPrevious(null);
    Promise.all([
      runReport(keys.revenue, range.from, range.to),
      runReport(keys.booking, range.from, range.to),
      runReport(keys.customer, range.from, range.to),
      runReport(keys.occupancy, range.from, range.to)
    ])
      .then(([revenue, booking, customer, occupancy]) => {
        if (cancelled) return;
        setReports({ revenue, booking, customer, occupancy });
        setLoading(false);
        // The comparison window is a second round trip; the page is usable first.
        return Promise.all([
          runReport(keys.revenue, range.previousFrom, range.previousTo),
          runReport(keys.booking, range.previousFrom, range.previousTo),
          runReport(keys.customer, range.previousFrom, range.previousTo),
          runReport(keys.occupancy, range.previousFrom, range.previousTo)
        ]).then(([revenue2, booking2, customer2, occupancy2]) => {
          if (!cancelled) {
            setPrevious({ revenue: revenue2, booking: booking2, customer: customer2, occupancy: occupancy2 });
          }
        });
      })
      .catch(() => {
        if (!cancelled) setLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [keys, days, workspaceTypeId]);

  const revenueMoneyColumn = columnOf(reports?.revenue, "money", /total|amount|revenue|net/i);
  const occupancyPercentColumn = columnOf(reports?.occupancy, "percent");
  const occupancyUnitsColumn = columnOf(reports?.occupancy, "number", /unit|total|count/i);

  const metrics = {
    revenue: sumColumn(reports?.revenue, revenueMoneyColumn),
    bookings: reports?.booking ? reports.booking.totalRows : null,
    customers: reports?.customer ? reports.customer.totalRows : null,
    occupancy: averageColumn(reports?.occupancy, occupancyPercentColumn)
  };
  const before = {
    revenue: sumColumn(previous?.revenue, columnOf(previous?.revenue, "money", /total|amount|revenue|net/i)),
    bookings: previous?.booking ? previous.booking.totalRows : null,
    customers: previous?.customer ? previous.customer.totalRows : null,
    occupancy: averageColumn(previous?.occupancy, columnOf(previous?.occupancy, "percent"))
  };

  const series = reports?.revenue ? trendSeries(reports.revenue, trendMode) : [];
  const categories = reports?.revenue
    ? breakdown(reports.revenue, revenueMoneyColumn, /type|category|service|plan|source/i)
    : [];
  const categoryTotal = categories.reduce((total, slice) => total + slice.value, 0);
  const bookingBars = reports?.booking
    ? breakdown(reports.booking, null, /type|workspace|category/i)
    : [];
  const occupancyRows = reports?.occupancy ? reports.occupancy.rows : [];
  const occupancyLabelColumn = columnOf(reports?.occupancy, "text", /type|workspace/i);
  const truncated = Boolean(reports?.revenue?.truncated);

  const available = catalogue ? catalogue.filter((report) => report.isAvailable) : [];

  const exportReport = async (key, title) => {
    setExporting(key);
    try {
      await reportsApi.export(key, "xlsx", filters(range.from, range.to));
      toast.success(`${title} downloaded.`);
    } catch (error) {
      toast.error(error instanceof ApiError ? error.message : "Export failed.");
    } finally {
      setExporting(null);
    }
  };

  /** The figures on this page, as a CSV — there is no server-side summary export. */
  const exportSummary = () => {
    const lines = [["Regalis WorkSpace — summary", `${range.from} to ${range.to}`], []];
    lines.push(["Metric", "Value"]);
    lines.push(["Total revenue", metrics.revenue == null ? "" : metrics.revenue]);
    lines.push(["Total bookings", metrics.bookings ?? ""]);
    lines.push(["New customers", metrics.customers ?? ""]);
    lines.push(["Average occupancy %", metrics.occupancy == null ? "" : metrics.occupancy.toFixed(1)]);
    if (categories.length > 0) {
      lines.push([], ["Revenue by category", "Amount", "Share %"]);
      for (const slice of categories) {
        lines.push([
          slice.label,
          slice.value,
          categoryTotal > 0 ? ((slice.value / categoryTotal) * 100).toFixed(1) : ""
        ]);
      }
    }
    if (bookingBars.length > 0) {
      lines.push([], ["Bookings by type", "Count"]);
      for (const bar of bookingBars) lines.push([bar.label, bar.value]);
    }
    if (occupancyRows.length > 0 && occupancyLabelColumn) {
      lines.push([], ["Workspace type", "Occupancy %", "Total units"]);
      for (const row of occupancyRows) {
        lines.push([
          row[occupancyLabelColumn.key],
          occupancyPercentColumn ? row[occupancyPercentColumn.key] : "",
          occupancyUnitsColumn ? row[occupancyUnitsColumn.key] : ""
        ]);
      }
    }
    const csv = lines
      .map((row) => row.map((cell) => `"${String(cell ?? "").replaceAll('"', '""')}"`).join(","))
      .join("\r\n");
    const url = URL.createObjectURL(new Blob([csv], { type: "text/csv;charset=utf-8" }));
    const anchor = document.createElement("a");
    anchor.href = url;
    anchor.download = `regalis-summary-${range.from}-to-${range.to}.csv`;
    anchor.click();
    URL.revokeObjectURL(url);
    toast.success("Summary downloaded.");
  };

  if (!catalogue) return el(Spinner, { center: true });

  return el(
    F,
    null,
    el(PageHeader, {
      title: "Reports & Analysis",
      description: "Performance overview and exportable reports across the workspace.",
      actions: el(
        "div",
        { className: "rep-filters" },
        el(
          "select",
          {
            className: "field__input rep-filters__select",
            "aria-label": "Period",
            value: days,
            onChange: (event) => setDays(event.target.value)
          },
          PERIODS.map((period) => el("option", { key: period.value, value: period.value }, period.label))
        ),
        el(
          "select",
          {
            className: "field__input rep-filters__select",
            "aria-label": "Workspace type",
            value: workspaceTypeId,
            onChange: (event) => setWorkspaceTypeId(event.target.value)
          },
          el("option", { value: "" }, "All Workspace Types"),
          types.map((type) =>
            el("option", { key: type.workspaceTypeId, value: type.workspaceTypeId }, type.typeName)
          )
        ),
        canExport
          ? el(
              Button,
              { onClick: exportSummary, disabled: loading },
              el(IconDownload, { size: 16 }),
              " Export Summary"
            )
          : null
      )
    }),

    el(
      "div",
      { className: "kpi-grid" },
      el(KpiTile, {
        icon: RIcons.wallet,
        value: metrics.revenue == null ? "—" : money(metrics.revenue),
        label: `Total Revenue (${days}d)`,
        delta: percentDelta(metrics.revenue, before.revenue),
        hint: truncated ? "Partial — the revenue report truncated its rows" : null
      }),
      el(KpiTile, {
        icon: RIcons.calendar,
        value: metrics.bookings == null ? "—" : String(metrics.bookings),
        label: `Total Bookings (${days}d)`,
        delta: percentDelta(metrics.bookings, before.bookings)
      }),
      el(KpiTile, {
        icon: RIcons.user,
        value: metrics.customers == null ? "—" : String(metrics.customers),
        label: `New Customers (${days}d)`,
        delta: percentDelta(metrics.customers, before.customers)
      }),
      el(KpiTile, {
        icon: RIcons.chart,
        value: metrics.occupancy == null ? "—" : Math.round(metrics.occupancy) + "%",
        label: `Avg. Occupancy (${days}d)`,
        delta: percentDelta(metrics.occupancy, before.occupancy)
      })
    ),

    el(
      "div",
      { className: "rep-row rep-row--trend" },
      el(
        Card,
        {
          title: "Revenue Trend",
          actions: el(
            "div",
            { className: "seg" },
            ["daily", "weekly", "monthly"].map((mode) =>
              el(
                "button",
                {
                  key: mode,
                  type: "button",
                  className: "seg__opt" + (trendMode === mode ? " seg__opt--active" : ""),
                  "aria-pressed": trendMode === mode,
                  onClick: () => setTrendMode(mode)
                },
                mode[0].toUpperCase() + mode.slice(1)
              )
            )
          )
        },
        loading ? el(Spinner, { center: true }) : el(TrendChart, { series, mode: trendMode })
      ),
      el(
        Card,
        { title: "Revenue by Category" },
        loading
          ? el(Spinner, { center: true })
          : el(DonutChart, { slices: categories, total: categoryTotal })
      )
    ),

    el(
      "div",
      { className: "rep-row rep-row--split" },
      el(
        Card,
        { title: "Bookings by Type" },
        loading ? el(Spinner, { center: true }) : el(BarChart, { bars: bookingBars })
      ),
      el(
        Card,
        {
          title: "Occupancy by Workspace Type",
          actions: el(Badge, { tone: "success" }, "Live")
        },
        loading
          ? el(Spinner, { center: true })
          : occupancyRows.length === 0 || !occupancyLabelColumn
            ? el("div", { className: "viz-empty" }, "The occupancy report is not available in this build.")
            : el(
                "table",
                { className: "data-table occ-table" },
                el(
                  "thead",
                  null,
                  el(
                    "tr",
                    null,
                    el("th", null, "Workspace type"),
                    el("th", null, "Occupancy"),
                    el("th", null, "Total units")
                  )
                ),
                el(
                  "tbody",
                  null,
                  occupancyRows.map((row, index) => {
                    const percent = occupancyPercentColumn ? numberAt(row, occupancyPercentColumn) : null;
                    return el(
                      "tr",
                      { key: index },
                      el("td", { style: { fontWeight: 600 } }, String(row[occupancyLabelColumn.key] ?? "—")),
                      el(
                        "td",
                        null,
                        el(
                          "div",
                          { className: "meter" },
                          el(
                            "div",
                            { className: "meter__track" },
                            el("div", {
                              className: "meter__fill",
                              style: { width: `${Math.max(0, Math.min(100, percent ?? 0))}%` }
                            })
                          ),
                          el("span", { className: "meter__value" }, percent == null ? "—" : Math.round(percent) + "%")
                        )
                      ),
                      el("td", null, occupancyUnitsColumn ? String(row[occupancyUnitsColumn.key] ?? "—") : "—")
                    );
                  })
                )
              )
      )
    ),

    el(
      "div",
      { className: "rep-library__head" },
      el("h3", { className: "rep-library__title" }, "Report Library"),
      el("span", { className: "rep-library__count" }, `${available.length} reports available`)
    ),
    el(
      "div",
      { className: "rep-library" },
      catalogue.map((report) =>
        el(
          "div",
          { key: report.key, className: "rep-card" + (report.isAvailable ? "" : " rep-card--soon") },
          el("span", { className: "rep-card__icon" }, el(reportIcon(report.title), { size: 18 })),
          el(
            "div",
            { className: "rep-card__body" },
            report.isAvailable
              ? el(Link, { to: `/admin/reports/${report.key}`, className: "rep-card__title" }, report.title)
              : el("span", { className: "rep-card__title" }, report.title),
            el("p", { className: "rep-card__desc" }, report.description),
            report.isAvailable
              ? canExport
                ? el(
                    Button,
                    {
                      size: "sm",
                      variant: "outline",
                      loading: exporting === report.key,
                      onClick: () => exportReport(report.key, report.title)
                    },
                    "Export"
                  )
                : el(Link, { to: `/admin/reports/${report.key}`, className: "rep-card__open" }, "Open report")
              : el("span", { className: "rep-card__soon" }, "Not in this build yet")
          )
        )
      )
    )
  );
}


/* ------------- the single-report shell, as built ------------- */
function M(i,s){if(s==null||s==="")return"—";switch(i){case"money":return K(Number(s));case"percent":return`${Number(s).toFixed(1)}%`;case"date":return J(String(s));case"datetime":return q(String(s));case"number":return String(s);default:return String(s)}}function re(){const{key:i}=G(),s=C(),h=H("REPORT.EXPORT"),o=new Date().toISOString().slice(0,10),E=new Date(Date.now()-696*36e5).toISOString().slice(0,10),[p,F]=a.useState(E),[m,D]=a.useState(o),[n,R]=a.useState(""),[l,P]=a.useState(""),[z,L]=a.useState([]),[O,W]=a.useState([]),[r,$]=a.useState(null),[g,y]=a.useState(!0),[j,f]=a.useState(null),v=()=>({from:p,to:m,locationId:n?Number(n):void 0,workspaceTypeId:l?Number(l):void 0}),w=a.useCallback(async()=>{y(!0);try{$(await u.get(i,v()))}catch(t){s.error(t instanceof b?t.message:"Failed to run the report.")}finally{y(!1)}},[i,p,m,n,l]);a.useEffect(()=>{w()},[w]),a.useEffect(()=>{U.list({page:1,pageSize:50}).then(t=>L(t.items)).catch(()=>{}),X.list().then(W).catch(()=>{})},[]);const S=async t=>{f(t);try{await u.export(i,t,v()),s.success(`${t.toUpperCase()} downloaded.`)}catch(c){s.error(c instanceof b?c.message:"Export failed.")}finally{f(null)}};return e.jsxs(e.Fragment,{children:[e.jsx(_,{title:r?.title??"Report",description:r?.description??"",actions:e.jsxs("div",{className:"row-actions",children:[e.jsx(T,{to:"/admin/reports",children:e.jsx(x,{variant:"ghost",children:"All reports"})}),h&&e.jsxs(e.Fragment,{children:[e.jsxs(x,{variant:"outline",loading:j==="xlsx",onClick:()=>S("xlsx"),children:[e.jsx(A,{size:16})," Excel"]}),e.jsxs(x,{variant:"outline",loading:j==="pdf",onClick:()=>S("pdf"),children:[e.jsx(A,{size:16})," PDF"]})]})]})}),e.jsxs(Y,{children:[e.jsxs("div",{className:"toolbar",children:[e.jsx(N,{label:"From",type:"date",value:p,onChange:t=>F(t.target.value)}),e.jsx(N,{label:"To",type:"date",value:m,onChange:t=>D(t.target.value)}),e.jsx(k,{label:"Location",value:n,placeholder:"All locations",options:z.map(t=>({value:t.locationId,label:t.locationName})),onChange:t=>R(t.target.value)}),e.jsx(k,{label:"Type",value:l,placeholder:"All types",options:O.map(t=>({value:t.workspaceTypeId,label:t.typeName})),onChange:t=>P(t.target.value)})]}),g&&!r?e.jsx(I,{center:!0}):r&&e.jsxs(e.Fragment,{children:[e.jsx("div",{className:"table-scroll",children:e.jsxs("table",{className:"data-table",style:{opacity:g?.6:1},children:[e.jsx("thead",{children:e.jsx("tr",{children:r.columns.map(t=>e.jsx("th",{style:["money","number","percent"].includes(t.type)?{textAlign:"right"}:void 0,children:t.header},t.key))})}),e.jsx("tbody",{children:r.rows.length===0?e.jsx("tr",{children:e.jsx("td",{colSpan:r.columns.length,style:{textAlign:"center",color:"var(--text-muted)"},children:"Nothing in this window — widen the filters."})}):r.rows.map((t,c)=>e.jsx("tr",{children:r.columns.map(d=>e.jsx("td",{style:["money","number","percent"].includes(d.type)?{textAlign:"right"}:void 0,children:M(d.type,t[d.key])},d.key))},c))})]})}),r.truncated&&e.jsxs("p",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:["Showing the first ",r.rows.length," of ",r.totalRows," rows — narrow the filters, or export for the full set."]}),e.jsx("div",{className:"toolbar",style:{marginTop:"var(--space-4)",marginBottom:0,flexWrap:"wrap"},children:r.summary.map(t=>e.jsxs("div",{className:"choice-card",style:{cursor:"default",minWidth:140},children:[e.jsx("div",{className:"choice-card__meta",children:t.label}),e.jsx("div",{className:"choice-card__title",children:t.value})]},t.label))})]})]})]})}
export{AdminReportsPage,re as ReportShellPage};
