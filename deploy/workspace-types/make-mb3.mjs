// Builds BookWorkspacePage-MB3.js from MB2 (14 Oct 2026, workspace types as data).
//   node make-mb3.mjs <frontend dir> <out dir>
// - type cards: only types with "Customers can book online" on (Hot Desk is off);
// - the monthly flow: any type whose booking modes include monthly (was /^(DESK|CABIN)$/);
// - Virtual Office: recognised by its fixed code, not by "virtual" in its (renameable) name.
import fs from "fs";
import path from "path";

const [front, out] = process.argv.slice(2);
if (!front || !out) throw new Error("usage: node make-mb3.mjs <frontend dir> <out dir>");
let s = fs.readFileSync(path.join(front, "assets", "BookWorkspacePage-MB2.js"), "utf8");
const swap = (from, to, what) => {
  const n = s.split(from).length - 1;
  if (n !== 1) throw new Error(`${what}: expected 1 match, found ${n}`);
  s = s.replace(from, to);
};
swap(`const MONTHLY_TYPES = /^(DESK|CABIN)$/i;`,
  `/* A type is booked by the month online whenever its booking modes include monthly (flag 2);
   "Both" (Desk, Cabin) keeps hourly for admin bookings, quotations and the Day pass check. */
const MONTHLY_MODE = 2;`, "monthly rule");
swap(`const isMonthlyType = (t) => !!t && MONTHLY_TYPES.test(t.typeCode ?? "");`,
  `const isMonthlyType = (t) => !!t && (t.bookingModes & MONTHLY_MODE) === MONTHLY_MODE;`, "isMonthlyType");
swap(`const VOT = T.find((t) => /virtual/i.test(t.typeName ?? "")) ?? null;`,
  `const VOT = T.find((t) => t.typeCode === "VIRTUAL_WORKSPACE") ?? null;`, "virtual office");
swap(`        R(t);
        const u = s.items.filter((B) => B.isActive);`,
  `        /* Only types customers can book online (the type level; each workspace's own
           "Open for customer booking" is applied by the server). */
        R(t.filter((B) => B.customerBookable));
        const u = s.items.filter((B) => B.isActive);`, "type cards");
fs.mkdirSync(path.join(out, "assets"), { recursive: true });
fs.writeFileSync(path.join(out, "assets", "BookWorkspacePage-MB3.js"), s, "utf8");
console.log("OK  - BookWorkspacePage-MB3.js");
