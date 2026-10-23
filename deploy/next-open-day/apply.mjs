// Book Workspace: the date starts on the first day a booking can still start (23 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// The page took "today" from the browser's UTC date, ignored working hours and offered
// days the location is closed. Now, in Indian time:
//   - once the working day is over (its last half-hour slot has started, e.g. 20:30 for a
//     21:00 close), "today" is no longer offered and the date moves to the next day;
//   - non-working days (the location's WorkingDays, e.g. no SUN) are skipped;
//   - the From / To / Starts-on pickers cannot go earlier than that day;
//   - the check re-runs every minute, so a page left open past closing moves on by itself.
// A date the member picked later than that is left alone.
// Patches BookWorkspacePage-MB2.js in place, bumps sw.js, and the staged MB3. Idempotent.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "next-open-day-2";
const MARK = "function firstOpenDay(";

const HELPERS = `/* The first day a booking can still start, in IST: today until the last half-hour slot
   of the working day has begun, otherwise the next working day (deploy/next-open-day). */
function istNow() {
  const p = {};
  for (const x of new Intl.DateTimeFormat("en-GB", { timeZone: "Asia/Kolkata", year: "numeric", month: "2-digit", day: "2-digit", hour: "2-digit", minute: "2-digit", hourCycle: "h23" }).formatToParts(new Date()))
    p[x.type] = x.value;
  return { date: \`\${p.year}-\${p.month}-\${p.day}\`, minutes: Number(p.hour) * 60 + Number(p.minute) };
}
function firstOpenDay(loc) {
  const now = istNow();
  const days = String(loc?.workingDays ?? "").toUpperCase().split(",").map((s) => s.trim()).filter(Boolean);
  /* No closing time known (location not loaded yet): today stays open. */
  const m = /^(\\d{1,2}):(\\d{2})/.exec(String(loc?.workingHoursEnd ?? ""));
  const endMin = m ? Number(m[1]) * 60 + Number(m[2]) : 24 * 60;
  for (let i = 0; i < 14; i++) {
    const d = addDaysIso(now.date, i);
    const wd = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"][new Date(\`\${d}T12:00:00Z\`).getUTCDay()];
    if (days.length && !days.includes(wd)) continue;
    if (i === 0 && now.minutes >= endMin - 30) continue;
    return d;
  }
  return now.date;
}
`;

const EFFECT = `
  /* Move the dates on when today is over or closed (deploy/next-open-day). */
  const [openTick, setOpenTick] = a.useState(0);
  a.useEffect(() => {
    const id = setInterval(() => setOpenTick((x) => x + 1), 60000);
    return () => clearInterval(id);
  }, []);
  const minDay = firstOpenDay(K);
  a.useEffect(() => {
    if (!K) return;
    if (b < minDay) ie(minDay);
    if (A < minDay) q(minDay);
  }, [K?.locationId, openTick, minDay]);
`;

function patch(file) {
  const name = path.basename(file);
  let t = fs.readFileSync(file, "utf8");
  if (t.includes(MARK)) { console.log(`OK  - ${name}: already patched`); return; }
  const edits = [
    ["helpers", "const VO_CODE = /^MEM-VIRTUAL-(\\d+)M$/;", HELPERS + "const VO_CODE = /^MEM-VIRTUAL-(\\d+)M$/;"],
    ["initial date", "  const w = new Date().toISOString().slice(0, 10);", "  const w = firstOpenDay(null);"],
    ["effect", "  const K = g.find((t) => String(t.locationId) === j) ?? null;\n",
      "  const K = g.find((t) => String(t.locationId) === j) ?? null;\n" + EFFECT],
    ["from min", "                    min: w,\n", "                    min: minDay,\n"],
    ["to min", "                      min: b || w,\n", "                      min: b || minDay,\n"],
  ];
  for (const [what, from, to] of edits) {
    const n = t.split(from).length - 1;
    if (n !== 1) throw new Error(`${name}: ${what}: expected 1 match, found ${n}; nothing written`);
    t = t.replace(from, () => to);
  }
  fs.writeFileSync(file, t, "utf8");
  console.log(`OK  - ${name}: dates start on the next open day`);
}

patch(path.join(front, "assets", "BookWorkspacePage-MB2.js"));
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const entry = /\{url:"assets\/BookWorkspacePage-MB2\.js",revision:(null|"[^"]*")\}/;
if (!entry.test(sw)) throw new Error("sw.js: no precache entry for BookWorkspacePage-MB2.js");
fs.writeFileSync(swf, sw.replace(entry, `{url:"assets/BookWorkspacePage-MB2.js",revision:"${REV}"}`), "utf8");
console.log(`OK  - sw.js: BookWorkspacePage-MB2.js revision ${REV}`);
if (staged) {
  const mb3 = path.join(staged, "BookWorkspacePage-MB3.js");
  if (fs.existsSync(mb3)) patch(mb3);
}
