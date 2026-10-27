/* Tests for the admin dashboard's stat-card labels and chips.
 *
 *   node --test frontend/tests/dashboard-cards.test.mjs
 *
 * The period lives in the chip, so the label must not repeat it: the server's
 * "Bookings today" shows a Today chip over "Bookings". Also covers the Total
 * revenue card the page adds after "Revenue this month".
 *
 * The page chunk imports the whole SPA, so the helpers are lifted out of the
 * published file's text and evaluated on their own — the test runs the shipped code.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const chunk = readFileSync(new URL("../assets/AdminDashboardPage-DgTMoJ1J.js", import.meta.url), "utf8");
const lift = (start) => {
  const at = chunk.indexOf(start);
  assert.ok(at >= 0, `${start} not found in the dashboard chunk`);
  let depth = 0;
  for (let k = chunk.indexOf("{", at); k < chunk.length; k++) {
    if (chunk[k] === "{") depth++;
    else if (chunk[k] === "}" && --depth === 0) return chunk.slice(at, k + 1);
  }
  throw new Error(`${start} is not balanced`);
};
const MON = chunk.slice(chunk.indexOf("const MON="), chunk.indexOf(";", chunk.indexOf("const MON=")) + 1);
const { labelFor, badgeFor, shortLabel } = new Function(
  `${MON}${lift("function badgeFor(")}${lift("function shortLabel(")}${lift("function labelFor(")}return{labelFor,badgeFor,shortLabel};`
)();
const card = (serverLabel) => {
  const L = labelFor(serverLabel);
  return { chip: badgeFor(L), label: shortLabel(L) };
};

/* The labels DashboardService.GetAdminAsync sends. */
test("Today cards keep the Today chip and drop Today from the label", () => {
  assert.deepEqual(card("Bookings today"), { chip: "Today", label: "Bookings" });
  assert.deepEqual(card("Occupancy today"), { chip: "Today", label: "Occupancy" });
  assert.deepEqual(card("Checked in now"), { chip: "Today", label: "Check In" });
  assert.deepEqual(card("Revenue today"), { chip: "Today", label: "Revenue" });
  assert.deepEqual(card("Housekeeping today"), { chip: "Today", label: "Maintenance" });
});

test("cards without Today are unchanged", () => {
  assert.equal(card("Revenue this month").label, "Revenue This Month");
  assert.deepEqual(card("Active passes"), { chip: "Active", label: "Active Passes" });
  assert.deepEqual(card("Open tickets"), { chip: "Open", label: "Open Tickets" });
  assert.deepEqual(card("Customers"), { chip: "Total", label: "Customers" });
});

test("Total revenue reads as an all-time figure", () => {
  assert.deepEqual(card("Total revenue"), { chip: "All time", label: "Total Revenue" });
});
