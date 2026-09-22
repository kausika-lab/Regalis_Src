/* Tests for the admin dashboard's Workspace Availability calculation.
 *
 *   node --test frontend/tests/availability.test.mjs
 *
 * Guards the bug this was written for: the panel used to count only bookings
 * active at the current instant, so a 08:00-09:00 booking showed "0 desks
 * occupied" by lunchtime while the Bookings Today card said 1.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { computeAvailability, isCountable, occupiesDay, istDate } from "../assets/availability-Bq7nK2xR.js";

/* IST = UTC+5:30, so 02:30Z is 08:00 IST on the same day. */
const DAY = "2026-09-21";
const at = (utc) => `2026-09-21T${utc}:00Z`;

const TYPES = [
  { workspaceTypeId: 1, typeName: "Desk" },
  { workspaceTypeId: 2, typeName: "Cabin" },
  { workspaceTypeId: 3, typeName: "Meeting Room" },
  { workspaceTypeId: 4, typeName: "Virtual Office" },
];

/* Mirrors UAT: 11 live desks, 4 live cabins + 2 in maintenance,
   2 live meeting rooms + 1 in maintenance. */
function workspaces() {
  const out = [];
  let id = 1;
  const push = (typeId, n, extra = {}) => {
    for (let k = 0; k < n; k++) out.push({ workspaceId: id++, workspaceTypeId: typeId, status: 1, isBookable: true, isActive: true, ...extra });
  };
  push(1, 11);
  push(2, 4);
  push(2, 2, { status: 3 });          // cabins under maintenance
  push(3, 2);
  push(3, 1, { status: 3 });          // meeting room under maintenance
  return out;
}
const DESK_1 = 1, CABIN_1 = 12, ROOM_1 = 18;
const row = (rows, name) => rows.find((r) => r.name === name);

test("a finished booking still counts as occupied today", () => {
  // The exact failing case: 08:00-09:00 IST, viewed at 12:56 IST.
  const rows = computeAvailability({
    bookings: [{ workspaceId: DESK_1, status: 2, startUtc: at("02:30"), endUtc: at("03:30") }],
    workspaces: workspaces(), types: TYPES, today: DAY,
  });
  assert.equal(row(rows, "Desk").occupied, 1, "desk booked this morning must still count");
  assert.equal(row(rows, "Desk").total, 11);
  assert.equal(row(rows, "Cabin").occupied, 0);
  assert.equal(row(rows, "Meeting Room").occupied, 0);
});

test("denominator excludes maintenance, disabled, inactive and unbookable", () => {
  const rows = computeAvailability({ bookings: [], workspaces: workspaces(), types: TYPES, today: DAY });
  assert.equal(row(rows, "Desk").total, 11);
  assert.equal(row(rows, "Cabin").total, 4, "2 maintenance cabins must not be counted");
  assert.equal(row(rows, "Meeting Room").total, 2, "1 maintenance room must not be counted");

  assert.equal(isCountable({ status: 1, isActive: true, isBookable: true }), true);
  assert.equal(isCountable({ status: 3, isActive: true, isBookable: true }), false, "maintenance");
  assert.equal(isCountable({ status: 4, isActive: true, isBookable: true }), false, "disabled");
  assert.equal(isCountable({ status: 1, isActive: false, isBookable: true }), false, "inactive");
  assert.equal(isCountable({ status: 1, isActive: true, isBookable: false }), false, "not bookable");
});

test("cancelled and expired bookings do not occupy; every other status does", () => {
  const only = (status) => computeAvailability({
    bookings: [{ workspaceId: DESK_1, status, startUtc: at("02:30"), endUtc: at("03:30") }],
    workspaces: workspaces(), types: TYPES, today: DAY,
  });
  for (const s of [1, 2, 3, 4, 7]) assert.equal(only(s).find((r) => r.name === "Desk").occupied, 1, `status ${s} should occupy`);
  for (const s of [5, 6]) assert.equal(only(s).find((r) => r.name === "Desk").occupied, 0, `status ${s} should not occupy`);
});

test("check-in is irrelevant", () => {
  const rows = computeAvailability({
    bookings: [{ workspaceId: DESK_1, status: 2, checkInUtc: null, startUtc: at("02:30"), endUtc: at("03:30") }],
    workspaces: workspaces(), types: TYPES, today: DAY,
  });
  assert.equal(row(rows, "Desk").occupied, 1);
});

test("adding a cabin booking moves cabins to 1/4; cancelling it moves them back", () => {
  const base = { workspaceId: CABIN_1, startUtc: at("05:00"), endUtc: at("06:00") };
  const added = computeAvailability({ bookings: [{ ...base, status: 2 }], workspaces: workspaces(), types: TYPES, today: DAY });
  assert.equal(row(added, "Cabin").occupied, 1);
  assert.equal(row(added, "Cabin").total, 4);
  const cancelled = computeAvailability({ bookings: [{ ...base, status: 5 }], workspaces: workspaces(), types: TYPES, today: DAY });
  assert.equal(row(cancelled, "Cabin").occupied, 0);
});

test("two bookings on one workspace count it once", () => {
  const rows = computeAvailability({
    bookings: [
      { workspaceId: ROOM_1, status: 2, startUtc: at("04:00"), endUtc: at("05:00") },
      { workspaceId: ROOM_1, status: 4, startUtc: at("06:00"), endUtc: at("07:00") },
    ],
    workspaces: workspaces(), types: TYPES, today: DAY,
  });
  assert.equal(row(rows, "Meeting Room").occupied, 1);
});

test("a booking on another day does not count", () => {
  const rows = computeAvailability({
    bookings: [{ workspaceId: DESK_1, status: 2, startUtc: "2026-09-20T02:30:00Z", endUtc: "2026-09-20T03:30:00Z" }],
    workspaces: workspaces(), types: TYPES, today: DAY,
  });
  assert.equal(row(rows, "Desk").occupied, 0);
});

test("a booking running through midnight counts for today", () => {
  // 20 Sep 23:00 IST -> 21 Sep 01:00 IST  ==  20 Sep 17:30Z -> 20 Sep 19:30Z
  assert.equal(occupiesDay({ startUtc: "2026-09-20T17:30:00Z", endUtc: "2026-09-20T19:30:00Z" }, DAY), true);
  const rows = computeAvailability({
    bookings: [{ workspaceId: DESK_1, status: 2, startUtc: "2026-09-20T17:30:00Z", endUtc: "2026-09-20T19:30:00Z" }],
    workspaces: workspaces(), types: TYPES, today: DAY,
  });
  assert.equal(row(rows, "Desk").occupied, 1);
});

test("timestamps without a zone are read as UTC, not local", () => {
  assert.equal(istDate("2026-09-20T18:30:00"), DAY, "18:30Z is 00:00 IST the next day");
  assert.equal(istDate("2026-09-20T18:30:00Z"), DAY);
});

test("Virtual Office reports active count, never an occupancy ratio", () => {
  const ws = workspaces().concat([{ workspaceId: 99, workspaceTypeId: 4, status: 1, isBookable: true, isActive: true }]);
  const rows = computeAvailability({ bookings: [], workspaces: ws, types: TYPES, today: DAY });
  const vo = row(rows, "Virtual Office");
  assert.equal(vo.total, null, "no seat cap");
  assert.equal(vo.active, 1);
  assert.equal(rows.at(-1).name, "Virtual Office", "uncapped rows sort last");
});

test("a capped type with no live workspaces is dropped; an uncapped one still reports 0 active", () => {
  // UAT's four Virtual Office rows are all soft-deleted, so the API returns none
  // and the panel shows "0 active" -- the row stays because an address service
  // with nothing sold is meaningful, unlike a desk type with no desks.
  const rows = computeAvailability({ bookings: [], workspaces: workspaces(), types: TYPES, today: DAY });
  assert.equal(row(rows, "Virtual Office").active, 0);
  assert.equal(row(rows, "Virtual Office").total, null);

  const capped = computeAvailability({
    bookings: [], workspaces: [], types: [{ workspaceTypeId: 1, typeName: "Desk" }], today: DAY,
  });
  assert.equal(capped.length, 0, "a capped type with no live workspaces is dropped");
});

test("empty inputs do not throw", () => {
  assert.deepEqual(computeAvailability(), []);
  assert.deepEqual(computeAvailability({ bookings: [], workspaces: [], types: [] }), []);
});
