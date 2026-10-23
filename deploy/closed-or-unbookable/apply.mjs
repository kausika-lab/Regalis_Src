// Book Workspace: "Closed on this date" only when the location really is closed (23 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// The slots API returns an empty list both when the location is shut that day and when no
// workspace of the chosen type can be booked (all under maintenance or disabled). The page
// said "Closed on this date" for both, so a meeting room under maintenance looked like the
// office was closed every day. It now checks the location's working days: on a working day
// the message says no <type> is available to book and to contact the front desk.
// Patches BookWorkspacePage-MB2.js in place, bumps sw.js, and the staged MB3 copy. Idempotent.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "closed-or-unbookable-1";
const FROM = `                        ? e.jsx(E, {
                            title: "Closed on this date",
                            description:
                              "Pick another date — the location has no working hours that day.",
                          })`;
const TO = `                        ? (() => {
                            /* Empty slots: the location is shut that day, or nothing of this
                               type can be booked (under maintenance or disabled). */
                            const wd = ["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"][new Date(\`\${b}T12:00:00Z\`).getUTCDay()];
                            const open = !K?.workingDays || String(K.workingDays).toUpperCase().split(",").map((x) => x.trim()).includes(wd);
                            const tn = (T.find((x) => String(x.workspaceTypeId) === v)?.typeName ?? "workspace").toLowerCase();
                            return open
                              ? e.jsx(E, {
                                  title: \`No \${tn} available to book\`,
                                  description: \`Every \${tn} is out of service on this date. Please pick another type or contact the front desk.\`,
                                })
                              : e.jsx(E, {
                                  title: "Closed on this date",
                                  description: "Pick another date — the location has no working hours that day.",
                                });
                          })()`;

function patch(file) {
  const name = path.basename(file);
  let t = fs.readFileSync(file, "utf8");
  if (t.includes("No ${tn} available to book")) { console.log(`OK  - ${name}: already patched`); return; }
  const n = t.split(FROM).length - 1;
  if (n !== 1) throw new Error(`${name}: expected 1 match, found ${n}; nothing written`);
  fs.writeFileSync(file, t.replace(FROM, () => TO), "utf8");
  console.log(`OK  - ${name}: closed vs nothing bookable`);
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
