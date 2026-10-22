/* Online payments switch: /env.js (deploy/offline-payments). */
const ONLINE_PAY = window.__REGALIS_ONLINE_PAY__ !== false;
import {
  a as ue,
  r as a,
  m as ye,
  y as ge,
  z as xe,
  D as fe,
  E as je,
  A as y,
  j as e,
  I as ee,
  F as ve,
  G as te,
  p as h,
  W as ke,
  H as we,
  Q as be,
  g as Se,
  J as Be,
  Z as PASS,
} from "./index-DI5X4c9f.js";
import { a as ae, i as Ne, b as k, p as se } from "./bookings-DrKATIWm.js";
import { C } from "./Card-CNXipofE.js";
import { E } from "./EmptyState-B4UbbVj4.js";
import { P as Ie } from "./PageHeader-Y96MMVry.js";
import { D as Ce } from "./bookings-CeZsOhV5.js";
import { a as L, b as p, c as DY, f as O } from "./formatters-DFe5xCRi.js";
import { C as Te } from "./circle-check-DvPmllJi.js";
import { D as ze } from "./download-Dlifs1Q2.js";

const MAX_RANGE_DAYS = 31;

function ne(o, T) {
  return (
    o.find((x) => x.locationId === T) ??
    o.find((x) => x.isDefault) ??
    o[0]
  ).locationId;
}

/* Date maths runs in UTC over the plain YYYY-MM-DD the date inputs produce, so a
   range never slides by a day regardless of the browser timezone. */
function dms(iso) {
  const [Y, M, D] = iso.split("-").map(Number);
  return Date.UTC(Y, M - 1, D);
}
function countDays(from, to) {
  return Math.round((dms(to) - dms(from)) / 864e5) + 1;
}
function eachDay(from, to) {
  const out = [],
    end = dms(to);
  for (let t = dms(from); t <= end; t += 864e5)
    out.push(new Date(t).toISOString().slice(0, 10));
  return out;
}
function addDaysIso(iso, n) {
  return new Date(dms(iso) + n * 864e5).toISOString().slice(0, 10);
}
const VO_CODE = /^MEM-VIRTUAL-(\d+)M$/;
function VOMONTHS(t) {
  const m = VO_CODE.exec(t?.planCode ?? "");
  return m ? Number(m[1]) : 0;
}
function VOTERM(t) {
  const m = VOMONTHS(t);
  return m === 1 ? "1 month" : `${m} months`;
}
/* Desks and cabins are booked by the month for one of these terms (MonthlyTerm on the
   server). A term of N months ends the day before the same date N months later; like
   .NET AddMonths, a day past the end of the target month is clamped to its last day. */
const MONTH_TERMS = [1, 3, 6, 12];
const MONTHLY_TYPES = /^(DESK|CABIN)$/i;
function termLabel(m) {
  return m === 1 ? "1 month" : `${m} months`;
}
function termEnd(iso, months) {
  const [Y, M, D] = iso.split("-").map(Number);
  const first = new Date(Date.UTC(Y, M - 1 + months, 1));
  const last = new Date(Date.UTC(first.getUTCFullYear(), first.getUTCMonth() + 1, 0)).getUTCDate();
  const same = Date.UTC(first.getUTCFullYear(), first.getUTCMonth(), Math.min(D, last));
  return new Date(same - 864e5).toISOString().slice(0, 10);
}
const TAKEN_LABEL = { Booked: "Booked", Blocked: "Not available", NoMonthlyPrice: "No monthly price" };
function parseUtc(v) {
  return v instanceof Date
    ? v
    : new Date(/[zZ]|[+-]\d{2}:?\d{2}$/.test(v) ? v : `${v}Z`);
}
function dayLabel(iso) {
  return DY(`${iso}T12:00:00Z`);
}

/* A whole day is the working window the location itself reports for that date,
   read back from the slots API rather than computed here, so the IST boundaries
   stay owned by the server (Design Invariant 1 - one calendar). */
function windowFromSlots(date, slots) {
  if (!slots || slots.length === 0) return { date, closed: !0 };
  const first = parseUtc(slots[0].startUtc).getTime();
  const last = parseUtc(slots[slots.length - 1].startUtc).getTime();
  const gran =
    slots.length > 1
      ? Math.round((parseUtc(slots[1].startUtc).getTime() - first) / 6e4)
      : 30;
  return {
    date,
    closed: !1,
    startUtc: slots[0].startUtc,
    minutes: Math.round((last - first) / 6e4) + gran,
  };
}

function Ee() {
  const o = ue();
  const [T, R] = a.useState([]);
  const [g, x] = a.useState([]);
  const [c, oe] = a.useState(null);
  const [f, r] = a.useState("when");
  const z = ye((t) => t.locationId);
  const [j, M] = a.useState("");
  const [v, Q] = a.useState("");
  const w = new Date().toISOString().slice(0, 10);
  const [b, ie] = a.useState(w);
  const [A, q] = a.useState(w);
  const W = b === A;
  const nDays = b && A && A >= b ? countDays(b, A) : 0;
  const [D, re] = a.useState(null);
  const [le, G] = a.useState(!1);
  const [i, H] = a.useState(null);
  const [m, ce] = a.useState(60);
  const [_, Y] = a.useState(null);
  const [d, $] = a.useState(null);
  const [n, J] = a.useState(null);
  const [S, N] = a.useState(null);
  const [l, F] = a.useState(null);
  const [plan, setPlan] = a.useState(null);
  const [busy, setBusy] = a.useState(!1);
  const [multi, setMulti] = a.useState(null);
  const [voPlans, setVoPlans] = a.useState(null);
  const [voPlan, setVoPlan] = a.useState(null);
  const [voDone, setVoDone] = a.useState(null);
  const K = g.find((t) => String(t.locationId) === j) ?? null;

  /* A Virtual Office is a business address, not a room: no slots, no workspace to
     pick, and it is sold by the month as a MEM-VIRTUAL-* pass plan. */
  const VOT = T.find((t) => /virtual/i.test(t.typeName ?? "")) ?? null;
  const isVO = !!VOT && v === String(VOT.workspaceTypeId);

  /* Customisation (custom-req-1): a fifth card beside Virtual Office. The request is raised as
     a support ticket in the "Workspace Customisation" category (migration
     20261023000000), so it reaches Admin > Support tickets and the admin notification
     centre as "Needs action", and the team replies on the ticket itself. */
  const CUSTOM = "custom";
  const isCustom = v === CUSTOM;
  const CUSTOM_KINDS = ["Desk", "Cabin", "Meeting Room", "Virtual Office"];
  const [cKind, setCKind] = a.useState(CUSTOM_KINDS[0]);
  const [cText, setCText] = a.useState("");
  const [cBusy, setCBusy] = a.useState(!1);
  const [cDone, setCDone] = a.useState(null);
  const [cCats, setCCats] = a.useState(null);
  a.useEffect(() => {
    if (!isCustom || cCats !== null) return;
    fe(je.get("/ticket-categories"))
      .then((t) => setCCats(Array.isArray(t) ? t : []))
      .catch(() => setCCats([]));
  }, [isCustom, cCats]);
  const cCat =
    (cCats ?? []).find((t) => /customi[sz]/i.test(t.categoryName ?? "")) ?? null;
  const cKinds =
    cCat && cCat.issueTypes?.length
      ? cCat.issueTypes.map((t) => t.issueTypeName)
      : CUSTOM_KINDS;
  a.useEffect(() => {
    cKinds.includes(cKind) || setCKind(cKinds[0]);
  }, [cKinds.join("|")]);
  const sendCustom = async () => {
    const text = cText.trim();
    if (text.length < 10) {
      o.error("Tell us a little more about the customisation you need.");
      return;
    }
    const cat =
      cCat ??
      (cCats ?? []).find((t) => /other/i.test(t.categoryName ?? "")) ??
      (cCats ?? [])[0] ??
      null;
    const issue =
      cat?.issueTypes?.find((t) => t.issueTypeName === cKind) ??
      cat?.issueTypes?.find((t) => /other|general|misc/i.test(t.issueTypeName ?? "")) ??
      cat?.issueTypes?.[0] ??
      null;
    if (!cat || !issue) {
      o.error("Customisation requests can't be sent right now — please raise it under Support & Contact.");
      return;
    }
    setCBusy(!0);
    try {
      const when = W ? dayLabel(b) : `${dayLabel(b)} to ${dayLabel(A)}`;
      const t = await fe(
        je.post("/tickets", {
          ticketCategoryId: cat.ticketCategoryId,
          ticketIssueTypeId: issue.ticketIssueTypeId,
          subject: `Customisation request · ${cKind}`,
          description: `${text}\n\nRequested from Book Workspace · ${cKind}${K ? ` · ${K.locationName}` : ""} · ${when}`,
          priority: 2,
        }),
      );
      setCDone(t);
      setCText("");
    } catch (t) {
      o.error(t instanceof y ? t.message : "The request could not be sent. Please try again.");
    } finally {
      setCBusy(!1);
    }
  };
  const customCard = () =>
    e.jsxs(
      "button",
      {
        type: "button",
        className: `choice-card${isCustom ? " choice-card--selected" : ""}`,
        style: { borderStyle: "dashed" },
        onClick: () => Q(CUSTOM),
        children: [
          e.jsx("div", { className: "choice-card__title", children: "Customisation" }),
          e.jsx("div", {
            className: "choice-card__meta",
            children: "Need something different? Tell us what you need",
          }),
        ],
      },
      CUSTOM,
    );
  const customPanel = () =>
    cDone
      ? e.jsx(E, {
          icon: e.jsx(Te, { size: 44 }),
          title: `Request ${cDone.ticketNumber} sent to the Regalis team`,
          description:
            "We'll set up the space as requested and reply on the request itself. You can follow it under Support & Contact.",
          action: e.jsxs("div", {
            style: { display: "flex", gap: "var(--space-2)", flexWrap: "wrap", justifyContent: "center" },
            children: [
              e.jsx(Se, {
                to: `/support/tickets/${cDone.ticketId}`,
                children: e.jsx(h, { variant: "outline", children: "View request" }),
              }),
              e.jsx(h, { variant: "ghost", onClick: () => setCDone(null), children: "Send another" }),
            ],
          }),
        })
      : e.jsxs("div", {
          className: "form-stack",
          children: [
            e.jsxs("div", {
              children: [
                e.jsx("div", { className: "field__label", children: "What would you like customised?" }),
                e.jsx("div", {
                  className: "wizard-steps",
                  style: { marginBottom: 0 },
                  children: cKinds.map((t) =>
                    e.jsx(
                      "button",
                      {
                        type: "button",
                        className: `wizard-step${cKind === t ? " wizard-step--active" : ""}`,
                        style: { border: "none", cursor: "pointer" },
                        onClick: () => setCKind(t),
                        children: t,
                      },
                      t,
                    ),
                  ),
                }),
              ],
            }),
            e.jsxs("div", {
              className: "field",
              children: [
                e.jsxs("label", {
                  className: "field__label",
                  htmlFor: "custom-request",
                  children: [
                    "Describe the customisation",
                    e.jsx("span", { className: "field__required", "aria-hidden": !0, children: " *" }),
                  ],
                }),
                e.jsx("textarea", {
                  id: "custom-request",
                  className: "field__input",
                  rows: 5,
                  maxLength: 3500,
                  value: cText,
                  placeholder:
                    "e.g. A cabin for four with a whiteboard, two extra monitors and our logo on the door, ready by Monday 9 am.",
                  style: { height: "auto", minHeight: 120, resize: "vertical" },
                  onChange: (t) => setCText(t.target.value),
                }),
                e.jsx("span", {
                  className: "field__hint",
                  children:
                    "Layout, seating, equipment, branding, timings — anything you need. The team replies on the request.",
                }),
              ],
            }),
            e.jsx(h, {
              size: "lg",
              loading: cBusy,
              disabled: cBusy || cText.trim().length < 10,
              onClick: sendCustom,
              children: "Submit customisation request",
            }),
          ],
        });

  /* A Desk or Cabin is booked by the month: a term and a start date, then one workspace
     that is free for the whole term. No time slots. */
  const isMonthlyType = (t) => !!t && MONTHLY_TYPES.test(t.typeCode ?? "");
  const isM = isMonthlyType(T.find((t) => String(t.workspaceTypeId) === v));
  const [mMonths, setMMonths] = a.useState(null);
  const mEnd = mMonths && b ? termEnd(b, mMonths) : null;

  a.useEffect(() => {
    g.length !== 0 && M(String(ne(g, z)));
  }, [z, g]);
  a.useEffect(() => {
    Promise.all([ge.list(), xe.list({ pageSize: 100 })])
      .then(([t, s]) => {
        R(t);
        const u = s.items.filter((B) => B.isActive);
        x(u);
        u.length > 0 && M(String(ne(u, z)));
      })
      .catch(() => o.error("Failed to load booking options."));
    fe(je.get("/wallet/me"))
      .then(oe)
      .catch(() => {});
  }, []);

  /* Slots are fetched only once BOTH a date and a workspace type are chosen. */
  const I = a.useCallback(async () => {
    if (!j || !v || !b || !W || isVO || isM || isCustom) {
      re(null);
      return;
    }
    G(!0);
    H(null);
    try {
      re(await ae.slots(Number(j), Number(v), b));
    } catch (t) {
      o.error(t instanceof y ? t.message : "Failed to load slots.");
    } finally {
      G(!1);
    }
  }, [j, v, b, W, isVO, isM, isCustom]);
  a.useEffect(() => {
    I();
  }, [I]);
  a.useEffect(() => {
    setPlan(null);
    Y(null);
    $(null);
    J(null);
  }, [b, A, v, j, mMonths]);
  a.useEffect(() => {
    setVoPlan(null);
  }, [v, j]);
  a.useEffect(() => {
    if (!isVO || voPlans !== null) return;
    PASS.activePlans(2)
      .then((t) => {
        const s = Array.isArray(t) ? t : (t?.items ?? []);
        setVoPlans(
          s
            .filter((u) => VOMONTHS(u) > 0)
            .sort((u, B2) => VOMONTHS(u) - VOMONTHS(B2)),
        );
      })
      .catch(() => o.error("Failed to load Virtual Office plans."));
  }, [isVO, voPlans]);

  /* Every workspace of the type for a window, taken ones included (isAvailable false),
     so the pick list can grey them out instead of hiding them. */
  const workspacesAt = (startUtc, minutes) =>
    fe(
      je.get("/availability/workspaces", {
        params: {
          locationId: Number(j),
          workspaceTypeId: Number(v),
          startUtc,
          durationMinutes: minutes,
          includeBusy: !0,
        },
      }),
    );

  /* Builds the whole-day window for every date in the range and marks the workspaces
     that are not free on every one of them. */
  const planRange = async () => {
    const dates = eachDay(b, A);
    const slotSets = await Promise.all(
      dates.map((t) => ae.slots(Number(j), Number(v), t)),
    );
    const windows = dates.map((t, ix) => windowFromSlots(t, slotSets[ix]));
    const closed = windows.filter((t) => t.closed);
    if (closed.length > 0) {
      const names = closed.map((t) => dayLabel(t.date)).join(", ");
      throw new Error(
        `${K ? K.locationName : "This location"} is closed on ${names}. Pick a range without ${closed.length === 1 ? "that date" : "those dates"}.`,
      );
    }
    const perDay = await Promise.all(
      windows.map((t) => workspacesAt(t.startUtc, t.minutes)),
    );
    /* Every workspace stays on the list; one that is taken on any date of the range is
       marked unavailable (shown greyed out) rather than dropped. */
    const seen = new Map();
    perDay.forEach((list, ix) => {
      (list || []).forEach((u) => {
        const cur = seen.get(u.workspaceId) ?? { ws: u, days: new Set() };
        u.isAvailable !== !1 && cur.days.add(windows[ix].date);
        seen.set(u.workspaceId, cur);
      });
    });
    const free = [...seen.values()]
      .map((t) => ({ ...t.ws, isAvailable: t.days.size === windows.length }))
      .sort((x2, y2) => String(x2.workspaceCode).localeCompare(String(y2.workspaceCode)));
    return { windows, free };
  };

  const de = async () => {
    if (!v) {
      o.error("Pick a workspace type first.");
      return;
    }
    if (isM) {
      if (!mMonths) return;
      setBusy(!0);
      try {
        Y(
          await fe(
            je.get("/availability/monthly", {
              params: {
                locationId: Number(j),
                workspaceTypeId: Number(v),
                startDate: b,
                months: mMonths,
              },
            }),
          ),
        );
        $(null);
        r("workspace");
      } catch (t) {
        o.error(t instanceof y ? t.message : "Failed to check availability.");
      } finally {
        setBusy(!1);
      }
      return;
    }
    if (A < b) {
      o.error("The To date cannot be before the From date.");
      return;
    }
    if (nDays > MAX_RANGE_DAYS) {
      o.error(
        `Ranges are limited to ${MAX_RANGE_DAYS} days. Buy a monthly pass for a longer stay.`,
      );
      return;
    }
    if (W) {
      if (!i) return;
      r("workspace");
      Y(null);
      $(null);
      try {
        Y(await workspacesAt(i, m));
      } catch (t) {
        (o.error(t instanceof y ? t.message : "Failed to load workspaces."),
          r("when"));
      }
      return;
    }
    setBusy(!0);
    try {
      const res = await planRange();
      setPlan(res.windows);
      Y(res.free);
      $(null);
      r("workspace");
    } catch (t) {
      o.error(
        t instanceof y
          ? t.message
          : t.message || "Failed to check availability.",
      );
    } finally {
      setBusy(!1);
    }
  };

  /* Every amount on screen comes from the server quote - one per booking day - so
     the summary can never disagree with what payment actually charges. */
  const he = async (t) => {
    if (t.isAvailable === !1) return;
    $(t);
    J(null);
    if (isM) {
      setBusy(!0);
      try {
        const q2 = await fe(
          je.post("/bookings/monthly/quote", {
            workspaceId: t.workspaceId,
            startDate: b,
            months: mMonths,
          }),
        );
        J({ ...q2, monthly: !0 });
        r("pay");
      } catch (s) {
        (o.error(s instanceof y ? s.message : "Could not price the booking."),
          $(null));
      } finally {
        setBusy(!1);
      }
      return;
    }
    if (W) {
      try {
        J(await k.quote(t.workspaceId, i, m));
        r("pay");
      } catch (s) {
        (o.error(s instanceof y ? s.message : "Could not price the booking."),
          $(null));
      }
      return;
    }
    setBusy(!0);
    try {
      const quotes = [];
      for (const win of plan)
        quotes.push(await k.quote(t.workspaceId, win.startUtc, win.minutes));
      const sum = (u) => quotes.reduce((acc, qq) => acc + (qq[u] ?? 0), 0);
      const first = quotes[0];
      const uniform = quotes.every(
        (qq) => qq.totalAmount === first.totalAmount,
      );
      J({
        multi: !0,
        quotes,
        days: quotes.length,
        perDayTotal: uniform ? first.totalAmount : null,
        perDayMinutes: plan[0].minutes,
        workspaceName: first.workspaceName,
        cgstPercent: first.cgstPercent,
        sgstPercent: first.sgstPercent,
        baseAmount: sum("baseAmount"),
        cgstAmount: sum("cgstAmount"),
        sgstAmount: sum("sgstAmount"),
        totalAmount: sum("totalAmount"),
      });
      r("pay");
    } catch (s) {
      (o.error(s instanceof y ? s.message : "Could not price the booking."),
        $(null));
    } finally {
      setBusy(!1);
    }
  };

  const V = async (t) =>
    t instanceof y && t.status === 409
      ? (o.warning(
          "That slot was just taken. Availability has been refreshed.",
        ),
        r("when"),
        await I(),
        !0)
      : !1;

  const settle = async (bookingId, method) => {
    if (method === "wallet")
      return k.confirm(bookingId, { paymentMethod: "wallet" });
    const order = await se.createOrder(bookingId);
    const paid = await se.sandboxComplete(order.gatewayOrderId);
    return k.confirm(bookingId, {
      paymentMethod: "gateway",
      gatewayOrderId: order.gatewayOrderId,
      gatewayPaymentId: paid.gatewayPaymentId,
      gatewaySignature: paid.gatewaySignature,
    });
  };

  const paySingle = async (method) => {
    if (!d || !i) return;
    N(method);
    try {
      const t = await k.hold(d.workspaceId, i, m);
      F(await settle(t.bookingId, method));
      r("done");
    } catch (t) {
      (await V(t)) || o.error(t instanceof y ? t.message : "Payment failed.");
    } finally {
      N(null);
    }
  };

  /* Monthly desk or cabin: one hold covering the whole term, paid like any booking. */
  const payMonthly = async (method) => {
    if (!d || !mMonths) return;
    N(method);
    try {
      const t = await fe(
        je.post("/bookings/monthly", {
          workspaceId: d.workspaceId,
          startDate: b,
          months: mMonths,
        }),
      );
      F(await settle(t.bookingId, method));
      r("done");
    } catch (t) {
      t instanceof y && t.status === 409
        ? (o.warning(
            "That workspace was just booked for part of this term. Pick another one.",
          ),
          await de())
        : o.error(t instanceof y ? t.message : "Payment failed.");
    } finally {
      N(null);
    }
  };

  /* Multi-day: take every hold FIRST, so a range that cannot be fully booked is
     abandoned before any money moves. Only then settle them one at a time. */
  const payMulti = async (method) => {
    if (!d || !plan) return;
    N(method);
    const holds = [];
    const release = async (list) => {
      for (const held of list) {
        try {
          await Be.cancel(
            held.bookingId,
            "Multi-day booking could not be completed",
          );
        } catch (err) {}
      }
    };
    try {
      for (let ix = 0; ix < plan.length; ix++) {
        const win = plan[ix];
        holds.push(
          await k.hold(
            d.workspaceId,
            win.startUtc,
            win.minutes,
            `Whole-day booking ${dayLabel(b)} to ${dayLabel(A)} (day ${ix + 1} of ${plan.length})`,
          ),
        );
      }
    } catch (t) {
      await release(holds);
      (await V(t)) ||
        o.error(
          t instanceof y
            ? t.message
            : "Could not hold every day in the range. Nothing was charged.",
        );
      N(null);
      return;
    }
    const done = [];
    try {
      for (const held of holds) done.push(await settle(held.bookingId, method));
    } catch (t) {
      await release(holds.slice(done.length));
      if (done.length === 0) {
        (await V(t)) ||
          o.error(
            t instanceof y ? t.message : "Payment failed. Nothing was charged.",
          );
        N(null);
        return;
      }
      o.warning(
        `${done.length} of ${holds.length} days were booked. The remaining days were released - please book them again.`,
      );
      setMulti(done);
      r("done");
      N(null);
      return;
    }
    setMulti(done);
    r("done");
    N(null);
  };

  const payVO = async (method) => {
    if (!voPlan) return;
    N(method);
    try {
      let res;
      if (method === "wallet")
        res = await PASS.purchase({
          passPlanId: voPlan.passPlanId,
          validFrom: b,
          paymentMethod: "wallet",
        });
      else {
        const ord = await PASS.order(voPlan.passPlanId);
        const paid = await se.sandboxComplete(ord.gatewayOrderId);
        res = await PASS.purchase({
          passPlanId: voPlan.passPlanId,
          validFrom: b,
          paymentMethod: "gateway",
          gatewayOrderId: ord.gatewayOrderId,
          gatewayPaymentId: paid.gatewayPaymentId,
          gatewaySignature: paid.gatewaySignature,
        });
      }
      setVoDone(res);
      r("done");
    } catch (t) {
      o.error(t instanceof y ? t.message : "Payment failed.");
    } finally {
      N(null);
    }
  };

  const X = isCustom
    ? [
        { key: "when", label: "1 · What you need" },
        { key: "done", label: "2 · Sent to the Regalis team" },
      ]
    : isVO
    ? [
        { key: "when", label: "1 · Term & start date" },
        { key: "pay", label: "2 · Review & pay" },
        { key: "done", label: "3 · Confirmed" },
      ]
    : [
        { key: "when", label: isM ? "1 · Term & start date" : "1 · Date, type & time" },
        { key: "workspace", label: "2 · Pick a workspace" },
        { key: "pay", label: "3 · Review & pay" },
        { key: "done", label: "4 · Confirmed" },
      ];
  const Z = isCustom && cDone ? 1 : X.findIndex((t) => t.key === f);
  const payTotal = isVO
    ? (voPlan?.totalAmount ?? null)
    : (n?.totalAmount ?? null);
  const U = a.useMemo(
    () => c !== null && payTotal !== null && c.balance >= payTotal,
    [c, payTotal],
  );
  const dayRateOf = (t) =>
    plan && plan.length > 0 ? (t.pricePerHour * plan[0].minutes) / 60 : null;
  const canContinue = isVO
    ? !!voPlan && !!b
    : isM
      ? !!mMonths && !!b
      : !!v && A >= b && nDays > 0 && (W ? !!i : !0);
  const reset = () => {
    F(null);
    setMulti(null);
    setPlan(null);
    Y(null);
    $(null);
    J(null);
    setVoPlan(null);
    setVoDone(null);
    r("when");
    I();
  };

  return e.jsxs(e.Fragment, {
    children: [
      e.jsx(Ie, {
        title: "Book a Workspace",
        description: K
          ? `Live availability at ${K.locationName} — the calendar you see is the calendar the front desk sees.`
          : "Live availability — the calendar you see is the calendar the front desk sees.",
      }),
      e.jsx("div", {
        className: "wizard-steps",
        children: X.map((t, s) =>
          e.jsx(
            "span",
            {
              className: `wizard-step${s === Z ? " wizard-step--active" : ""}${s < Z ? " wizard-step--done" : ""}`,
              children: t.label,
            },
            t.key,
          ),
        ),
      }),

      f === "when" &&
        e.jsx(C, {
          title: "What, where and when",
          children: e.jsxs("div", {
            className: "form-stack",
            children: [
              e.jsxs("div", {
                className: "toolbar toolbar--when",
                style: { marginBottom: 0 },
                children: [
                  e.jsx(ee, {
                    label: isVO || isM ? "Starts on" : "From date",
                    type: "date",
                    value: b,
                    min: w,
                    onChange: (t) => {
                      const s = t.target.value;
                      ie(s);
                      s > A && q(s);
                    },
                  }),
                  !isVO &&
                    !isM &&
                    e.jsx(ee, {
                      label: "To date",
                      type: "date",
                      value: A,
                      min: b || w,
                      onChange: (t) => q(t.target.value),
                    }),
                  !isVO &&
                    !isM &&
                    !isCustom &&
                    W &&
                    e.jsx(ve, {
                      label: "Duration",
                      value: String(m),
                      options: Ce.map((t) => ({
                        value: t,
                        label:
                          t < 60
                            ? `${t} min`
                            : `${t / 60} hour${t > 60 ? "s" : ""}`,
                      })),
                      onChange: (t) => ce(Number(t.target.value)),
                    }),
                ],
              }),
              e.jsx("p", {
                className: "choice-card__meta",
                style: { margin: 0 },
                children: isCustom
                  ? "Tell us what you need for these dates — the Regalis team sets it up and replies on your request."
                  : isVO
                  ? "A Virtual Office is sold by the month, not by the hour — the minimum term is 1 month."
                  : isM
                    ? "Desks and cabins are booked by the month. Pick a term, then choose a workspace that is free for the whole term."
                    : nDays > 0
                    ? `${nDays} day${nDays === 1 ? "" : "s"}${W ? " · pick a start time below" : " · booked as a whole day on each date"}`
                    : "The To date cannot be before the From date.",
              }),
              e.jsx("div", {
                className: "field__label",
                children: "Workspace",
              }),
              e.jsx("div", {
                className: "choice-grid",
                children: [
                  ...T.map((t) =>
                  e.jsxs(
                    "button",
                    {
                      type: "button",
                      className: `choice-card${v === String(t.workspaceTypeId) ? " choice-card--selected" : ""}`,
                      onClick: () => Q(String(t.workspaceTypeId)),
                      children: [
                        e.jsx("div", {
                          className: "choice-card__title",
                          children: t.typeName,
                        }),
                        e.jsx("div", {
                          className: "choice-card__meta",
                          children:
                            VOT && t.workspaceTypeId === VOT.workspaceTypeId
                              ? "Business address · from 1 month"
                              : `${t.workspaceCount} workspace${t.workspaceCount === 1 ? "" : "s"}${isMonthlyType(t) ? " · by the month" : ""}`,
                        }),
                      ],
                    },
                    t.workspaceTypeId,
                  ),
                ),
                  customCard(),
                ],
              }),

              !v
                ? e.jsx(E, {
                    title: "Pick a workspace type",
                    description:
                      "Choose Desk, Cabin or Meeting Room to see live availability for the dates above — or Virtual Office for a monthly business address. Pick Customisation to tell us what you need.",
                  })
                : isCustom
                  ? customPanel()
                : isVO
                  ? voPlans === null
                    ? e.jsx(te, { center: !0 })
                    : voPlans.length === 0
                      ? e.jsx(E, {
                          title: "No Virtual Office plans on sale",
                          description:
                            "Please check back soon, or ask the front desk.",
                        })
                      : e.jsxs("div", {
                          children: [
                            e.jsx("div", {
                              className: "field__label",
                              style: { marginBottom: "var(--space-2)" },
                              children: "Term",
                            }),
                            e.jsx("div", {
                              className: "choice-grid",
                              children: voPlans.map((t) =>
                                e.jsxs(
                                  "button",
                                  {
                                    type: "button",
                                    className: `choice-card${voPlan?.passPlanId === t.passPlanId ? " choice-card--selected" : ""}`,
                                    onClick: () => setVoPlan(t),
                                    children: [
                                      e.jsx("div", {
                                        className: "choice-card__title",
                                        children: VOTERM(t),
                                      }),
                                      e.jsxs("div", {
                                        className: "choice-card__meta",
                                        children: [
                                          t.validityDays,
                                          " days from your start date",
                                        ],
                                      }),
                                      e.jsxs("div", {
                                        className: "choice-card__price",
                                        children: [
                                          p(t.totalAmount),
                                          " ",
                                          e.jsx("span", {
                                            style: {
                                              fontWeight: 400,
                                              color: "var(--text-muted)",
                                              fontSize: "var(--font-size-xs)",
                                            },
                                            children: "incl. GST",
                                          }),
                                        ],
                                      }),
                                      e.jsxs("div", {
                                        className: "choice-card__meta",
                                        children: [
                                          p(t.price / VOMONTHS(t)),
                                          " per month before GST",
                                        ],
                                      }),
                                    ],
                                  },
                                  t.passPlanId,
                                ),
                              ),
                            }),
                          ],
                        })
                  : isM
                    ? e.jsxs("div", {
                        children: [
                          e.jsx("div", {
                            className: "field__label",
                            style: { marginBottom: "var(--space-2)" },
                            children: "Term",
                          }),
                          e.jsx("div", {
                            className: "choice-grid",
                            children: MONTH_TERMS.map((t) =>
                              e.jsxs(
                                "button",
                                {
                                  type: "button",
                                  className: `choice-card${mMonths === t ? " choice-card--selected" : ""}`,
                                  onClick: () => setMMonths(t),
                                  children: [
                                    e.jsx("div", {
                                      className: "choice-card__title",
                                      children: termLabel(t),
                                    }),
                                    e.jsx("div", {
                                      className: "choice-card__meta",
                                      children: b
                                        ? `${dayLabel(b)} to ${dayLabel(termEnd(b, t))}`
                                        : "Pick a start date",
                                    }),
                                  ],
                                },
                                t,
                              ),
                            ),
                          }),
                        ],
                      })
                  : W
                    ? le
                      ? e.jsx(te, { center: !0 })
                      : !D || D.length === 0
                        ? e.jsx(E, {
                            title: "Closed on this date",
                            description:
                              "Pick another date — the location has no working hours that day.",
                          })
                        : e.jsxs("div", {
                            children: [
                              e.jsx("div", {
                                className: "field__label",
                                style: { marginBottom: "var(--space-2)" },
                                children: "Start time (IST)",
                              }),
                              e.jsx("div", {
                                className: "slot-grid",
                                children: D.map((t) =>
                                  e.jsx(
                                    "button",
                                    {
                                      type: "button",
                                      disabled:
                                        !t.isAvailable ||
                                        parseUtc(t.startUtc).getTime() <=
                                          Date.now(),
                                      className: `slot-chip${i === t.startUtc ? " slot-chip--selected" : ""}`,
                                      onClick: () => H(t.startUtc),
                                      children: L(t.startUtc),
                                    },
                                    t.startUtc,
                                  ),
                                ),
                              }),
                            ],
                          })
                    : e.jsx(E, {
                        title: `Whole-day booking · ${nDays} days`,
                        description: `${dayLabel(b)} to ${dayLabel(A)} — both dates included. Every date is booked for the full working day, and the range is only offered if the same workspace is free on all ${nDays} days.`,
                      }),

              !isCustom &&
              e.jsx(h, {
                size: "lg",
                disabled: !canContinue || busy,
                loading: busy,
                onClick: isVO ? () => r("pay") : de,
                children: isVO
                  ? "Continue to payment"
                  : "Find available workspaces",
              }),
            ],
          }),
        }),

      f === "workspace" &&
        e.jsx(C, {
          title: isM
            ? `${termLabel(mMonths)} · ${dayLabel(b)} to ${dayLabel(mEnd)}`
            : W
              ? `Available ${O(i)} · ${m} min`
              : `Available all ${nDays} days · ${dayLabel(b)} to ${dayLabel(A)}`,
          actions: e.jsx(h, {
            variant: "ghost",
            onClick: () => r("when"),
            children: "Change dates",
          }),
          children: _
            ? _.length === 0
              ? e.jsx(E, {
                  title: "No workspaces of this type here",
                  description: "Try another workspace type or location.",
                })
              : e.jsxs("div", {
                  className: "form-stack",
                  children: [
                  !_.some((t) => t.isAvailable !== !1) &&
                    e.jsx("p", {
                      className: "choice-card__meta",
                      style: { margin: 0 },
                      children: isM
                        ? "Every workspace of this type is taken for part of this term. Try a later start date or a shorter term."
                        : W
                          ? "Every workspace of this type is taken in that window. Try a different time, duration or workspace type."
                          : "No workspace of this type is free on every date. Try a shorter range or another workspace type.",
                    }),
                  e.jsx("div", {
                  className: "choice-grid",
                  children: _.map((t) =>
                    e.jsxs(
                      "button",
                      {
                        type: "button",
                        className: `choice-card${d?.workspaceId === t.workspaceId ? " choice-card--selected" : ""}`,
                        onClick: () => he(t),
                        /* A taken workspace stays on the list, greyed out and unselectable. */
                        disabled: busy || t.isAvailable === !1,
                        "aria-disabled": t.isAvailable === !1 || void 0,
                        title:
                          t.isAvailable === !1
                            ? (TAKEN_LABEL[t.unavailableReason] ?? "Booked")
                            : void 0,
                        style:
                          t.isAvailable === !1
                            ? { filter: "grayscale(1)", background: "var(--surface-inset)", boxShadow: "none" }
                            : void 0,
                        children: [
                          e.jsxs("div", {
                            className: "choice-card__title",
                            style: { display: "flex", justifyContent: "space-between", gap: "var(--space-2)", alignItems: "baseline" },
                            children: [
                              e.jsx("span", { children: t.workspaceName }),
                              t.isAvailable === !1 &&
                                e.jsx("span", {
                                  className: "badge badge--neutral",
                                  style: { fontSize: "var(--font-size-xs)", fontWeight: 600, whiteSpace: "nowrap" },
                                  children: TAKEN_LABEL[t.unavailableReason] ?? "Booked",
                                }),
                            ],
                          }),
                          e.jsxs("div", {
                            className: "choice-card__meta",
                            children: [
                              t.workspaceCode,
                              t.floorNumber ? ` · Floor ${t.floorNumber}` : "",
                              " · seats",
                              " ",
                              t.capacity,
                            ],
                          }),
                          t.amenities.length > 0 &&
                            e.jsx("div", {
                              className: "choice-card__meta",
                              children: t.amenities.join(" · "),
                            }),
                          e.jsxs("div", {
                            className: "choice-card__price",
                            children: isM
                              ? t.pricePerMonth != null
                                ? [p(t.pricePerMonth), "/month"]
                                : [t.unavailableReason === "Booked" ? "Booked" : "Monthly price not set"]
                              : [
                                  W ? p(t.pricePerHour) : p(dayRateOf(t) ?? 0),
                                  W ? "/hour" : "/day",
                                ],
                          }),
                        ],
                      },
                      t.workspaceId,
                    ),
                  ),
                }),
                  ],
                })
            : e.jsx(te, { center: !0 }),
        }),

      f === "pay" &&
        isVO &&
        voPlan &&
        e.jsx(C, {
          title: "Review & pay",
          actions: e.jsx(h, {
            variant: "ghost",
            onClick: () => r("when"),
            children: "Back",
          }),
          children: e.jsxs("div", {
            className: "form-stack",
            children: [
              e.jsxs("div", {
                className: "summary-rows",
                children: [
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Service" }),
                      e.jsx("strong", { children: "Virtual Office" }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Location" }),
                      e.jsx("strong", { children: K ? K.locationName : "—" }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Term" }),
                      e.jsx("strong", { children: VOTERM(voPlan) }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Starts on" }),
                      e.jsx("strong", { children: dayLabel(b) }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Renews on" }),
                      e.jsx("strong", {
                        children: dayLabel(
                          addDaysIso(b, Number(voPlan.validityDays) || 30),
                        ),
                      }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Base amount" }),
                      e.jsx("span", { children: p(voPlan.price) }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "CGST + SGST" }),
                      e.jsx("span", {
                        children: p(
                          (voPlan.totalAmount ?? 0) - (voPlan.price ?? 0),
                        ),
                      }),
                    ],
                  }),
                  e.jsxs("div", {
                    className: "summary-total",
                    children: [
                      e.jsx("span", { children: "Total" }),
                      e.jsx("span", { children: p(voPlan.totalAmount) }),
                    ],
                  }),
                ],
              }),
              e.jsxs("div", {
                className: "pay-options",
                children: [
                  e.jsxs(h, {
                    size: "lg",
                    variant: U ? "primary" : "outline",
                    disabled: !U || S !== null,
                    loading: S === "wallet",
                    onClick: () => payVO("wallet"),
                    children: [
                      e.jsx(ke, { size: 18 }),
                      " Wallet",
                      c && ` (${p(c.balance)})`,
                    ],
                  }),
                  ONLINE_PAY && e.jsxs(h, {
                    size: "lg",
                    variant: "primary",
                    disabled: S !== null,
                    loading: S === "gateway",
                    onClick: () => payVO("gateway"),
                    children: [e.jsx(we, { size: 18 }), " Pay online"],
                  }),
                ],
              }),
              !U &&
                c &&
                e.jsxs("p", {
                  style: {
                    color: "var(--text-muted)",
                    fontSize: "var(--font-size-xs)",
                  },
                  children: [
                    "Wallet balance ",
                    p(c.balance),
                    ONLINE_PAY ? " doesn't cover this — pay online or top up from the Wallet page." : " doesn't cover this — add money at the front desk (cash, UPI or card), then pay from your wallet.",
                  ],
                }),
              e.jsx("p", {
                style: {
                  color: "var(--text-muted)",
                  fontSize: "var(--font-size-xs)",
                },
                children:
                  "No desk or room is reserved — a Virtual Office is a business address with mail handling. A GST invoice is raised as soon as the payment goes through.",
              }),
            ],
          }),
        }),

      f === "pay" &&
        !isVO &&
        n &&
        d &&
        e.jsx(C, {
          title: "Review & pay",
          actions: e.jsx(h, {
            variant: "ghost",
            onClick: () => r("workspace"),
            children: "Back",
          }),
          children: e.jsxs("div", {
            className: "form-stack",
            children: [
              e.jsxs("div", {
                className: "summary-rows",
                children: [
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Workspace" }),
                      e.jsxs("strong", {
                        children: [n.workspaceName, " (", d.workspaceCode, ")"],
                      }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Location" }),
                      e.jsx("strong", { children: K ? K.locationName : "—" }),
                    ],
                  }),
                  n.monthly
                    ? e.jsxs(e.Fragment, {
                        children: [
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Term" }),
                              e.jsx("strong", { children: termLabel(n.months) }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Starts on" }),
                              e.jsx("strong", { children: dayLabel(n.startDate) }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Ends on" }),
                              e.jsx("strong", { children: dayLabel(n.endDate) }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Rate per month" }),
                              e.jsx("strong", { children: p(n.pricePerMonth) }),
                            ],
                          }),
                        ],
                      })
                  : n.multi
                    ? e.jsxs(e.Fragment, {
                        children: [
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "From date" }),
                              e.jsx("strong", { children: dayLabel(b) }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "To date" }),
                              e.jsx("strong", { children: dayLabel(A) }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Number of days" }),
                              e.jsxs("strong", {
                                children: [
                                  n.days,
                                  " day",
                                  n.days === 1 ? "" : "s",
                                ],
                              }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Rate per day" }),
                              e.jsx("strong", {
                                children:
                                  n.perDayTotal !== null
                                    ? `${p(n.perDayTotal)} · ${n.perDayMinutes} min`
                                    : "varies by day",
                              }),
                            ],
                          }),
                        ],
                      })
                    : e.jsxs(e.Fragment, {
                        children: [
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "When" }),
                              e.jsxs("strong", {
                                children: [O(n.startUtc), " – ", L(n.endUtc)],
                              }),
                            ],
                          }),
                          e.jsxs("div", {
                            children: [
                              e.jsx("span", { children: "Duration" }),
                              e.jsxs("strong", {
                                children: [n.durationMinutes, " minutes"],
                              }),
                            ],
                          }),
                        ],
                      }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Base amount" }),
                      e.jsx("span", { children: p(n.baseAmount) }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsxs("span", {
                        children: ["CGST @ ", n.cgstPercent, "%"],
                      }),
                      e.jsx("span", { children: p(n.cgstAmount) }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsxs("span", {
                        children: ["SGST @ ", n.sgstPercent, "%"],
                      }),
                      e.jsx("span", { children: p(n.sgstAmount) }),
                    ],
                  }),
                  e.jsxs("div", {
                    className: "summary-total",
                    children: [
                      e.jsx("span", { children: "Total" }),
                      e.jsx("span", { children: p(n.totalAmount) }),
                    ],
                  }),
                ],
              }),
              n.multi &&
                e.jsx("div", {
                  className: "summary-rows",
                  children: n.quotes.map((qq, ix) =>
                    e.jsxs(
                      "div",
                      {
                        children: [
                          e.jsx("span", { children: dayLabel(plan[ix].date) }),
                          e.jsxs("span", {
                            children: [
                              L(qq.startUtc),
                              " – ",
                              L(qq.endUtc),
                              " · ",
                              p(qq.totalAmount),
                            ],
                          }),
                        ],
                      },
                      plan[ix].date,
                    ),
                  ),
                }),
              e.jsxs("div", {
                className: "pay-options",
                children: [
                  e.jsxs(h, {
                    size: "lg",
                    variant: U ? "primary" : "outline",
                    disabled: !U || S !== null,
                    loading: S === "wallet",
                    onClick: () => (isM ? payMonthly : W ? paySingle : payMulti)("wallet"),
                    children: [
                      e.jsx(ke, { size: 18 }),
                      " Wallet",
                      c && ` (${p(c.balance)})`,
                    ],
                  }),
                  ONLINE_PAY && e.jsxs(h, {
                    size: "lg",
                    variant: "primary",
                    disabled: S !== null,
                    loading: S === "gateway",
                    onClick: () => (isM ? payMonthly : W ? paySingle : payMulti)("gateway"),
                    children: [e.jsx(we, { size: 18 }), " Pay online"],
                  }),
                ],
              }),
              !U &&
                c &&
                e.jsxs("p", {
                  style: {
                    color: "var(--text-muted)",
                    fontSize: "var(--font-size-xs)",
                  },
                  children: [
                    "Wallet balance ",
                    p(c.balance),
                    ONLINE_PAY ? " doesn't cover this booking — pay online or top up from the Wallet page." : " doesn't cover this booking — add money at the front desk (cash, UPI or card), then pay from your wallet.",
                  ],
                }),
              e.jsx("p", {
                style: {
                  color: "var(--text-muted)",
                  fontSize: "var(--font-size-xs)",
                },
                children: n.monthly
                  ? `The workspace is yours for every day of the term, ${dayLabel(n.startDate)} to ${dayLabel(n.endDate)}. It is held for 10 minutes while payment completes; one GST invoice covers the whole term.`
                  : n.multi
                    ? `Each of the ${n.days} days is held for 10 minutes while payment completes. If any day cannot be held, the whole range is released and nothing is charged.`
                    : "The slot is held for 10 minutes while payment completes.",
              }),
            ],
          }),
        }),

      f === "done" &&
        isVO &&
        voDone &&
        e.jsx(C, {
          children: e.jsxs("div", {
            style: { textAlign: "center", padding: "var(--space-6) 0" },
            children: [
              e.jsx(Te, { size: 56, color: "var(--color-success)" }),
              e.jsx("h2", {
                style: {
                  fontSize: "var(--font-size-2xl)",
                  fontWeight: 800,
                  marginTop: "var(--space-3)",
                },
                children: "Virtual Office active",
              }),
              e.jsxs("p", {
                style: {
                  color: "var(--text-secondary)",
                  marginTop: "var(--space-2)",
                },
                children: [
                  e.jsx("strong", { children: voDone.pass?.passNumber }),
                  " · ",
                  VOTERM(voPlan),
                  " from ",
                  dayLabel(b),
                  " · ",
                  p(voDone.pass?.totalAmount ?? voPlan.totalAmount),
                  " paid, including GST",
                ],
              }),
              e.jsx("p", {
                style: {
                  color: "var(--text-muted)",
                  marginTop: "var(--space-2)",
                  fontSize: "var(--font-size-sm)",
                },
                children:
                  "Your GST invoice is under Invoices, and the address service shows under Passes. No desk or room was reserved.",
              }),
              e.jsx("div", {
                style: {
                  display: "flex",
                  gap: "var(--space-3)",
                  justifyContent: "center",
                  marginTop: "var(--space-6)",
                  flexWrap: "wrap",
                },
                children: e.jsx(h, { onClick: reset, children: "Done" }),
              }),
            ],
          }),
        }),

      f === "done" &&
        !isVO &&
        (l || multi) &&
        e.jsx(C, {
          children: e.jsxs("div", {
            style: { textAlign: "center", padding: "var(--space-6) 0" },
            children: [
              e.jsx(Te, { size: 56, color: "var(--color-success)" }),
              e.jsx("h2", {
                style: {
                  fontSize: "var(--font-size-2xl)",
                  fontWeight: 800,
                  marginTop: "var(--space-3)",
                },
                children: multi
                  ? `${multi.length} day${multi.length === 1 ? "" : "s"} confirmed`
                  : "Booking confirmed",
              }),
              l &&
                e.jsxs("p", {
                  style: {
                    color: "var(--text-secondary)",
                    marginTop: "var(--space-2)",
                  },
                  children: l.booking.billingMonths
                    ? [
                        e.jsx("strong", { children: l.booking.bookingNumber }),
                        " · ",
                        l.booking.workspaceName,
                        " · ",
                        termLabel(l.booking.billingMonths),
                        ", ",
                        DY(l.booking.startUtc),
                        " to ",
                        DY(l.booking.endUtc),
                      ]
                    : [
                        e.jsx("strong", { children: l.booking.bookingNumber }),
                        " · ",
                        l.booking.workspaceName,
                        " ·",
                        " ",
                        O(l.booking.startUtc),
                        " – ",
                        L(l.booking.endUtc),
                      ],
                }),
              multi &&
                e.jsx("div", {
                  className: "summary-rows",
                  style: { marginTop: "var(--space-4)", textAlign: "left" },
                  children: multi.map((t) =>
                    e.jsxs(
                      "div",
                      {
                        children: [
                          e.jsx("span", { children: O(t.booking.startUtc) }),
                          e.jsxs("strong", {
                            children: [
                              t.booking.bookingNumber,
                              " · ",
                              L(t.booking.startUtc),
                              " – ",
                              L(t.booking.endUtc),
                            ],
                          }),
                        ],
                      },
                      t.booking.bookingNumber,
                    ),
                  ),
                }),
              e.jsxs("p", {
                style: {
                  color: "var(--text-muted)",
                  marginTop: "var(--space-2)",
                  fontSize: "var(--font-size-sm)",
                },
                children: [
                  e.jsx(be, {
                    size: 16,
                    style: { verticalAlign: "text-bottom" },
                  }),
                  " Your QR access pass is ready — find it under ",
                  e.jsx(Se, { to: "/qr", children: "My QR / Access" }),
                  " when the access module opens.",
                ],
              }),
              e.jsxs("div", {
                style: {
                  display: "flex",
                  gap: "var(--space-3)",
                  justifyContent: "center",
                  marginTop: "var(--space-6)",
                  flexWrap: "wrap",
                },
                children: [
                  l &&
                    e.jsxs(h, {
                      variant: "outline",
                      onClick: () =>
                        Ne.downloadPdf(l.invoiceId, l.invoiceNumber),
                      children: [
                        e.jsx(ze, { size: 16 }),
                        " Invoice ",
                        l.invoiceNumber,
                      ],
                    }),
                  multi &&
                    multi.map((t) =>
                      e.jsxs(
                        h,
                        {
                          variant: "outline",
                          onClick: () =>
                            Ne.downloadPdf(t.invoiceId, t.invoiceNumber),
                          children: [
                            e.jsx(ze, { size: 16 }),
                            " ",
                            t.invoiceNumber,
                          ],
                        },
                        t.invoiceId,
                      ),
                    ),
                  e.jsx(h, { onClick: reset, children: "Book another" }),
                ],
              }),
            ],
          }),
        }),
    ],
  });
}

export { Ee as BookWorkspacePage };
