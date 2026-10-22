// Virtual Office term cards show the price per month only (23 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// The term cards (1 / 3 / 6 / 12 months) showed the GST-inclusive total with the
// per-month rate underneath. They now show just "<rate> / month" before GST; the
// Review & pay step keeps the full breakdown (base amount, CGST + SGST, total) and
// gains a "Per month" row. Patches BookWorkspacePage-MB2.js in place and bumps sw.js;
// with a second argument also patches the staged BookWorkspacePage-MB3.js. Idempotent.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "vo-term-price-1";

const once = (t, from, to, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.replace(from, () => to);
};

const CARD_OLD = `                                      e.jsxs("div", {
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
`;
const CARD_NEW = `                                      e.jsxs("div", {
                                        className: "choice-card__price",
                                        children: [
                                          p(t.price / VOMONTHS(t)),
                                          " ",
                                          e.jsx("span", {
                                            style: {
                                              fontWeight: 400,
                                              color: "var(--text-muted)",
                                              fontSize: "var(--font-size-xs)",
                                            },
                                            children: "/ month",
                                          }),
                                        ],
                                      }),
`;

const PAY_OLD = `                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Base amount" }),
                      e.jsx("span", { children: p(voPlan.price) }),
                    ],
                  }),
`;
const PAY_NEW = `                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Per month" }),
                      e.jsx("span", { children: p(voPlan.price / (VOMONTHS(voPlan) || 1)) }),
                    ],
                  }),
                  e.jsxs("div", {
                    children: [
                      e.jsx("span", { children: "Base amount" }),
                      e.jsx("span", { children: p(voPlan.price) }),
                    ],
                  }),
`;

function patch(file) {
  const name = path.basename(file);
  let t = fs.readFileSync(file, "utf8");
  if (t.includes(CARD_NEW)) {
    console.log(`OK  - ${name}: already patched`);
    return;
  }
  t = once(t, CARD_OLD, CARD_NEW, `${name}: term card`);
  t = once(t, PAY_OLD, PAY_NEW, `${name}: pay step`);
  fs.writeFileSync(file, t, "utf8");
  console.log(`OK  - ${name}: term cards show the rate per month; pay step gains a Per month row`);
}

patch(path.join(front, "assets", "BookWorkspacePage-MB2.js"));

const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
const entry = /\{url:"assets\/BookWorkspacePage-MB2\.js",revision:(null|"[^"]*")\}/;
if (!entry.test(sw)) throw new Error("sw.js: no precache entry for BookWorkspacePage-MB2.js");
sw = sw.replace(entry, `{url:"assets/BookWorkspacePage-MB2.js",revision:"${REV}"}`);
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js: BookWorkspacePage-MB2.js revision ${REV}`);

if (staged) {
  const mb3 = path.join(staged, "BookWorkspacePage-MB3.js");
  if (fs.existsSync(mb3)) patch(mb3);
  else console.log(`--  - ${mb3}: not found, skipped`);
}
