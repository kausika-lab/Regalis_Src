// My Bookings: From date / To date columns with check-in and check-out times (23 Oct 2026).
//   node apply.mjs <frontend dir>
//
// The single "When (IST)" column ("02 Oct 2026, 08:00 am – 09:00 pm") hid the end date of a
// multi-day or monthly booking. It becomes two columns: From date with the check-in time
// and To date with the check-out time, both in IST. Patches MyBookingsPage-Jm2rP9kD.js in
// place and bumps its sw.js revision. Idempotent.
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "bookings-fromto-1";

const once = (t, from, to, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.replace(from, () => to);
};

const file = path.join(front, "assets", "MyBookingsPage-Jm2rP9kD.js");
let t = fs.readFileSync(file, "utf8");
if (t.includes('key:"fromDate"')) {
  console.log("OK  - MyBookingsPage-Jm2rP9kD.js: already patched");
} else {
  t = once(
    t,
    'import{b as S,f as R,a as O}from"./formatters-DFe5xCRi.js"',
    'import{b as S,f as R,a as O,c as RGdate}from"./formatters-DFe5xCRi.js"',
    "formatters import",
  );
  const cell = (when, label) =>
    `e.jsxs("div",{style:{whiteSpace:"nowrap"},children:[e.jsx("div",{style:{fontWeight:600},children:RGdate(${when})}),` +
    `e.jsxs("div",{style:{color:"var(--text-muted)",fontSize:"var(--font-size-xs)"},children:["${label} ",O(${when})," IST"]})]})`;
  t = once(
    t,
    '{key:"when",header:"When (IST)",render:t=>`${R(t.startUtc)} – ${O(t.endUtc)}`},',
    `{key:"fromDate",header:"From date",render:t=>${cell("t.startUtc", "Check-in")}},` +
      `{key:"toDate",header:"To date",render:t=>${cell("t.endUtc", "Check-out")}},`,
    "when column",
  );
  fs.writeFileSync(file, t, "utf8");
  console.log("OK  - MyBookingsPage-Jm2rP9kD.js: From date / To date with check-in and check-out");
}

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const entry = /\{url:"assets\/MyBookingsPage-Jm2rP9kD\.js",revision:(null|"[^"]*")\}/;
if (!entry.test(sw)) throw new Error("sw.js: no precache entry for MyBookingsPage-Jm2rP9kD.js");
sw = sw.replace(entry, `{url:"assets/MyBookingsPage-Jm2rP9kD.js",revision:"${REV}"}`);
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js: MyBookingsPage-Jm2rP9kD.js revision ${REV}`);
