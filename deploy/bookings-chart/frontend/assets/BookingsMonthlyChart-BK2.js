/**
 * Admin -> Bookings: "Bookings by month" chart, shown between the Search / Status
 * filters and the bookings table (AdminBookingsPage-BK1.js renders it).
 *
 * Data: GET /api/bookings/monthly?year=&search=&status= — twelve months by booking date
 * (IST), under the same filters as the table, so the counts always match it. Revenue
 * counts only bookings that were kept (Confirmed, Checked in, Completed, No-show).
 *
 * Hand-maintained (this repo ships the built SPA). A fix ships under a NEW name
 * (BK3, ...). BK2: count axis no longer overshoots (28 bookings -> 0-50, not 0-100). because /assets is cached for a year and this file is not precached.
 */
import { r as React, j as e, D as unwrap, E as http } from "./index-DI5X4c9f.js";
import { b as fmtMoney } from "./formatters-DFe5xCRi.js";

const MONTHS = ["Jan", "Feb", "Mar", "Apr", "May", "Jun", "Jul", "Aug", "Sep", "Oct", "Nov", "Dec"];
const MONTH_NAMES = ["January", "February", "March", "April", "May", "June", "July",
  "August", "September", "October", "November", "December"];

const compactMoney = (() => {
  const format = new Intl.NumberFormat("en-IN", { notation: "compact", maximumFractionDigits: 1 });
  return (value) => `₹${format.format(value)}`;
})();
const plural = (n) => `${n} ${n === 1 ? "booking" : "bookings"}`;

/** Smallest "nice" axis maximum (1, 2, 2.5, 5 × 10ⁿ) at or above the data maximum. */
function niceMax(max, integer) {
  if (max <= 0) return integer ? 4 : 1000;
  if (integer && max <= 4) return 4;
  const power = 10 ** Math.floor(Math.log10(max));
  for (const scale of [power, power * 10]) {
    for (const step of [1, 2, 2.5, 4, 5, 8]) {
      const candidate = step * scale;
      // Count gridlines must land on whole numbers: fifths for a 5 / 2.5 lead, else quarters.
      const parts = step === 5 || step === 2.5 ? 5 : 4;
      if (candidate >= max && (!integer || candidate % parts === 0)) return candidate;
    }
  }
  return 10 * power;
}

function gridTicks(max) {
  const lead = max / 10 ** Math.floor(Math.log10(max));
  const parts = lead === 5 || lead === 2.5 ? 5 : 4;
  return Array.from({ length: parts + 1 }, (_, i) => (max * i) / parts);
}

const STYLE_ID = "rg-bookings-bk2";
const STYLES = `
/* Search and Status in one aligned row: equal 40px controls, labels on one line. */
.bk-filters{display:grid;grid-template-columns:minmax(0,2fr) minmax(0,1fr) minmax(0,1fr);gap:var(--space-4);align-items:end;margin-bottom:var(--space-4)}
@media(max-width:700px){.bk-filters{grid-template-columns:minmax(0,1fr)}}
.bk-filters .field{min-width:0;flex:none;gap:6px}
.bk-filters .field__label{line-height:1.25;white-space:nowrap}
.bk-filters .field__input{box-sizing:border-box;height:40px;min-width:0;width:100%;padding-top:0;padding-bottom:0;line-height:38px}
.bk-filters select.field__input{padding-right:32px}

.bk-chart{--bk-bar:var(--regalis-gold-dark);position:relative;margin-bottom:var(--space-5);padding:var(--space-4) var(--space-5);border:1px solid var(--border-subtle);border-radius:var(--radius-lg);background:rgb(var(--accent-rgb) / 4%)}
.bk-chart__head{display:flex;flex-wrap:wrap;align-items:flex-start;justify-content:space-between;gap:var(--space-3);margin-bottom:var(--space-2)}
.bk-chart__title{font-family:var(--font-display);font-size:var(--font-size-md);font-weight:700;color:var(--text-primary)}
.bk-chart__sub{color:var(--text-muted);font-size:var(--font-size-xs);margin-top:2px}
.bk-chart__controls{display:flex;flex-wrap:wrap;align-items:center;gap:var(--space-2)}
.bk-chart__totals{display:flex;gap:var(--space-5);margin:var(--space-2) 0 var(--space-1)}
.bk-chart__figure{font-size:var(--font-size-lg);font-weight:600;color:var(--text-primary);font-variant-numeric:tabular-nums;line-height:1.2}
.bk-chart__label{color:var(--text-muted);font-size:var(--font-size-xs)}
.bk-seg{display:inline-flex;padding:3px;border:1px solid var(--border-subtle);border-radius:var(--radius-full);background:var(--surface-card)}
.bk-seg button{all:unset;cursor:pointer;padding:5px 12px;border-radius:var(--radius-full);font-size:var(--font-size-xs);font-weight:600;color:var(--text-secondary)}
.bk-seg button[aria-pressed=true]{background:var(--regalis-gold);color:var(--accent-contrast,#0b0b0d)}
.bk-seg button:focus-visible{outline:2px solid var(--regalis-gold);outline-offset:2px}
.bk-year{height:34px;padding:0 30px 0 12px;width:auto;min-width:90px}
.bk-chart svg{display:block;width:100%;height:240px;overflow:visible}
.bk-chart svg text{font-family:inherit;font-variant-numeric:tabular-nums}
.bk-chart__tip{position:absolute;z-index:5;pointer-events:none;min-width:150px;padding:8px 10px;border:1px solid var(--border-subtle);border-radius:var(--radius-md);background:var(--surface-card);box-shadow:var(--shadow-lg);font-size:var(--font-size-xs);color:var(--text-secondary);transform:translate(-50%,calc(-100% - 10px));white-space:nowrap}
.bk-chart__tip strong{display:block;color:var(--text-primary);font-size:var(--font-size-sm);font-variant-numeric:tabular-nums}
.bk-chart details{margin-top:var(--space-2);font-size:var(--font-size-sm)}
.bk-chart summary{cursor:pointer;color:var(--text-secondary);font-size:var(--font-size-xs)}
.bk-chart table{width:100%;margin-top:var(--space-2);border-collapse:collapse}
.bk-chart th{text-align:left;font-size:var(--font-size-xs);color:var(--text-muted);font-weight:600;padding:4px 6px}
.bk-chart td{padding:4px 6px;border-top:1px solid var(--border-subtle)}
.bk-chart td:not(:first-child),.bk-chart th:not(:first-child){text-align:right;font-variant-numeric:tabular-nums}
@media(max-width:560px){.bk-chart{padding:var(--space-3)}}
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

function useWidth(ref) {
  const [width, setWidth] = React.useState(0);
  React.useEffect(() => {
    if (!ref.current) return;
    const observer = new ResizeObserver(([entry]) => setWidth(entry.contentRect.width));
    observer.observe(ref.current);
    return () => observer.disconnect();
  }, [ref]);
  return width;
}

/** Injects the aligned-filter styles as soon as the page renders, before any data. */
export function useBookingsStyles() {
  useStyles();
}

export function BookingsMonthlyChart({ search, status }) {
  useStyles();
  const [year, setYear] = React.useState(() => new Date().getFullYear());
  const [measure, setMeasure] = React.useState("count");
  const [data, setData] = React.useState(null);
  const [failed, setFailed] = React.useState(false);
  const [active, setActive] = React.useState(null);
  const wrap = React.useRef(null);
  const width = useWidth(wrap);

  React.useEffect(() => {
    let live = true;
    setFailed(false);
    unwrap(http.get("/bookings/monthly", {
      params: { year, search: search || undefined, status: status ? Number(status) : undefined }
    }))
      .then((result) => live && setData(result))
      .catch(() => live && setFailed(true));
    return () => {
      live = false;
    };
  }, [year, search, status]);

  const now = new Date().getFullYear();
  const years = [];
  for (let y = now + 1; y >= now - 5; y--) years.push(y);

  const isCount = measure === "count";
  const months = data?.months ?? [];
  const value = (m) => (isCount ? m.count : m.revenue);
  const label = (v) => (isCount ? String(v) : compactMoney(v));

  const height = 240;
  const pad = { top: 22, right: 8, bottom: 26, left: isCount ? 36 : 52 };
  const plotW = Math.max(0, width - pad.left - pad.right);
  const plotH = height - pad.top - pad.bottom;
  const max = niceMax(Math.max(0, ...months.map(value)), isCount);
  // A 5 or 2.5 leading digit splits into fifths (1K, 2K ...), anything else into quarters.
  const ticks = gridTicks(max);
  const band = plotW / 12;
  const barW = Math.max(4, Math.min(24, band * 0.55));
  const y = (v) => pad.top + plotH - (v / max) * plotH;
  const current = active !== null ? months[active] : null;

  const bar = (m, i) => {
    const v = value(m);
    if (v <= 0) return null;
    const x = pad.left + band * i + (band - barW) / 2;
    const top = y(v);
    const h = pad.top + plotH - top;
    const r = Math.min(4, h, barW / 2);
    // Rounded data end, square at the baseline.
    return e.jsx("path", {
      d: `M${x} ${top + h}V${top + r}Q${x} ${top} ${x + r} ${top}H${x + barW - r}Q${x + barW} ${top} ${x + barW} ${top + r}V${top + h}Z`,
      fill: "var(--bk-bar)",
      opacity: active === null || active === i ? 1 : 0.45
    }, `bar-${i}`);
  };

  return e.jsxs("section", {
    className: "bk-chart",
    "aria-label": "Bookings by month",
    children: [
      e.jsxs("div", {
        className: "bk-chart__head",
        children: [
          e.jsxs("div", {
            children: [
              e.jsx("div", { className: "bk-chart__title", children: "Bookings by month" }),
              e.jsx("div", {
                className: "bk-chart__sub",
                children: `January – December ${year}, by booking date` +
                  (search || status ? " · follows the filters above" : "")
              })
            ]
          }),
          e.jsxs("div", {
            className: "bk-chart__controls",
            children: [
              e.jsxs("div", {
                className: "bk-seg",
                role: "group",
                "aria-label": "Measure",
                children: [
                  e.jsx("button", { type: "button", "aria-pressed": isCount, onClick: () => setMeasure("count"), children: "Booking count" }),
                  e.jsx("button", { type: "button", "aria-pressed": !isCount, onClick: () => setMeasure("revenue"), children: "Booking revenue" })
                ]
              }),
              e.jsx("select", {
                className: "field__input bk-year",
                "aria-label": "Year",
                value: year,
                onChange: (event) => setYear(Number(event.target.value)),
                children: years.map((yy) => e.jsx("option", { value: yy, children: yy }, yy))
              })
            ]
          })
        ]
      }),
      e.jsxs("div", {
        className: "bk-chart__totals",
        children: [
          e.jsxs("div", {
            children: [
              e.jsx("div", { className: "bk-chart__figure", children: data ? data.totalCount : "—" }),
              e.jsx("div", { className: "bk-chart__label", children: `Total bookings in ${year}` })
            ]
          }),
          e.jsxs("div", {
            children: [
              e.jsx("div", { className: "bk-chart__figure", children: data ? fmtMoney(data.totalRevenue) : "—" }),
              e.jsx("div", { className: "bk-chart__label", children: "Revenue (excludes cancelled and expired)" })
            ]
          })
        ]
      }),
      e.jsxs("div", {
        ref: wrap,
        style: { position: "relative" },
        children: [
          failed && e.jsx("div", { className: "bk-chart__sub", style: { padding: "40px 0", textAlign: "center" }, children: "The chart could not be loaded." }),
          !failed && width > 0 && data &&
            e.jsxs("svg", {
              viewBox: `0 0 ${width} ${height}`,
              role: "img",
              "aria-label": `${isCount ? "Bookings" : "Booking revenue"} per month in ${year}: ` +
                months.map((m) => `${MONTH_NAMES[m.month - 1]} ${isCount ? m.count : fmtMoney(m.revenue)}`).join(", "),
              onMouseLeave: () => setActive(null),
              children: [
                ticks.map((t, i) =>
                  e.jsxs("g", {
                    children: [
                      e.jsx("line", {
                        x1: pad.left, x2: width - pad.right, y1: y(t), y2: y(t),
                        stroke: i === 0 ? "var(--border-strong)" : "var(--border-subtle)", strokeWidth: 1
                      }),
                      e.jsx("text", {
                        x: pad.left - 8, y: y(t) + 4, textAnchor: "end", fontSize: 11, fill: "var(--text-muted)",
                        children: t === 0 ? (isCount ? "0" : "₹0") : label(t)
                      })
                    ]
                  }, `tick-${i}`)
                ),
                active !== null &&
                  e.jsx("rect", {
                    x: pad.left + band * active, y: pad.top, width: band, height: plotH,
                    fill: "rgb(var(--accent-rgb) / 8%)", rx: 4
                  }),
                months.map(bar),
                band >= 32 &&
                  months.map((m, i) =>
                    value(m) > 0 &&
                      e.jsx("text", {
                        x: pad.left + band * i + band / 2, y: y(value(m)) - 6, textAnchor: "middle",
                        fontSize: 10.5, fontWeight: 600, fill: "var(--text-secondary)",
                        children: label(value(m))
                      }, `val-${i}`)
                  ),
                months.map((m, i) =>
                  e.jsx("text", {
                    x: pad.left + band * i + band / 2, y: height - 8, textAnchor: "middle",
                    fontSize: 11, fill: active === i ? "var(--text-primary)" : "var(--text-muted)",
                    children: band < 30 ? MONTHS[m.month - 1][0] : MONTHS[m.month - 1]
                  }, `m-${i}`)
                ),
                // Hit targets: the whole column, not just the bar.
                months.map((m, i) =>
                  e.jsx("rect", {
                    x: pad.left + band * i, y: pad.top, width: band, height: plotH + pad.bottom,
                    fill: "transparent",
                    onMouseEnter: () => setActive(i)
                  }, `hit-${i}`)
                )
              ]
            }),
          current &&
            e.jsxs("div", {
              className: "bk-chart__tip",
              style: {
                left: Math.min(Math.max(pad.left + band * active + band / 2, 80), width - 80),
                top: Math.min(y(value(current)), pad.top + plotH - 10)
              },
              children: [
                `${MONTH_NAMES[current.month - 1]} ${year}`,
                e.jsx("strong", { children: isCount ? plural(current.count) : fmtMoney(current.revenue) }),
                isCount ? `Revenue ${fmtMoney(current.revenue)}` : plural(current.count)
              ]
            })
        ]
      }),
      data &&
        e.jsxs("details", {
          children: [
            e.jsx("summary", { children: "View as table" }),
            e.jsxs("table", {
              children: [
                e.jsx("thead", {
                  children: e.jsxs("tr", {
                    children: [e.jsx("th", { children: "Month" }), e.jsx("th", { children: "Bookings" }), e.jsx("th", { children: "Revenue" })]
                  })
                }),
                e.jsx("tbody", {
                  children: months.map((m) =>
                    e.jsxs("tr", {
                      children: [
                        e.jsx("td", { children: MONTH_NAMES[m.month - 1] }),
                        e.jsx("td", { children: m.count }),
                        e.jsx("td", { children: fmtMoney(m.revenue) })
                      ]
                    }, m.month)
                  )
                })
              ]
            })
          ]
        })
    ]
  });
}
