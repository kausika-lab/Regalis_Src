// past-dates kit (25 Nov 2026): customers can pick past dates wherever the customer portal has a
// date field. Frontend only; the server rules are unchanged.
//   node apply.mjs <frontend dir>
// String edits that must each match exactly once; idempotent (an edit already present is skipped).
// Expects the offers kit's patches, which are in the repo frontend.
//
//   BookWorkspacePage-MB2.js   From / Starts on / To date: no earliest date. A past date the
//                              customer picked is no longer moved forward (the default dates still
//                              are, deploy/next-open-day). A past start shows a note and keeps
//                              "Find available workspaces" / "Continue to payment" disabled.
//   PassesPage-CxAkHUg-.js     Starts on / First visit date: no earliest date. A past date shows a
//                              note and keeps the pay buttons disabled.
//   sw.js                      revisions for the two files and index.html
import fs from "fs";
import path from "path";

const front = process.argv[2];
if (!front) throw new Error("usage: node apply.mjs <frontend dir>");
const REV = "past-dates-1";
const A = (f) => path.join(front, "assets", f);
const changed = new Set();

function patchFile(p, label, edits) {
  let t = fs.readFileSync(p, "utf8");
  const eol = (s) => (t.includes("\r\n") ? s.replace(/\r?\n/g, "\r\n") : s);
  for (const [name, rawFrom, rawTo] of edits) {
    const from = eol(rawFrom);
    const to = eol(rawTo);
    if (t.includes(to)) {
      console.log(`OK  - ${label}: ${name} (already done)`);
      continue;
    }
    const n = t.split(from).length - 1;
    if (n === 1) {
      t = t.replace(from, () => to);
      console.log(`OK  - ${label}: ${name}`);
      changed.add(label);
    } else throw new Error(`${label}: '${name}' expected 1 match, found ${n}; nothing written for this file`);
  }
  fs.writeFileSync(p, t, "utf8");
}

// ---- Book a Workspace ---------------------------------------------------------------------------
patchFile(A("BookWorkspacePage-MB2.js"), "BookWorkspacePage-MB2.js", [
  [
    "a past date the customer picked is not moved forward",
    `  const minDay = firstOpenDay(K);
  a.useEffect(() => {
    if (!K) return;
    if (b < minDay) ie(minDay);
    if (A < minDay) q(minDay);
  }, [K?.locationId, openTick, minDay]);
`,
    `  const minDay = firstOpenDay(K);
  /* past-dates kit (25 Nov 2026): a customer may pick a past date to look at it. A past date
     the customer chose is left alone; the default dates are still moved on. Nothing can be
     booked on a past date (pastStart keeps the continue button off; the server refuses too). */
  const pastPick = a.useRef(!1);
  const todayIst = istNow().date;
  const pastStart = !!b && b < todayIst;
  a.useEffect(() => {
    if (!K) return;
    if (b < minDay && !(pastPick.current && b < todayIst)) ie(minDay);
    if (A < minDay && !(pastPick.current && A < todayIst)) q(minDay);
  }, [K?.locationId, openTick, minDay]);
`,
  ],
  [
    "From date: no earliest date",
    `                    value: b,
                    min: minDay,
                    onChange: (t) => {
                      const s = t.target.value;
                      ie(s);
`,
    `                    value: b,
                    onChange: (t) => {
                      const s = t.target.value;
                      pastPick.current = !0;
                      ie(s);
`,
  ],
  [
    "To date: only limited by the From date",
    `                      min: b || minDay,
                      onChange: (t) => q(t.target.value),
`,
    `                      min: b,
                      onChange: (t) => {
                        pastPick.current = !0;
                        q(t.target.value);
                      },
`,
  ],
  [
    "past start: note under the dates",
    `              !!v &&
              e.jsx("p", {
                className: "choice-card__meta",
                style: { margin: 0 },
                children: isCustom
`,
    `              !!v &&
              !isCustom &&
              pastStart &&
              e.jsx("p", {
                className: "choice-card__meta",
                "data-past-date": "1",
                style: { margin: 0, color: "var(--color-danger)" },
                children: "This date has passed. You can look at it, but a booking can only start today or later. Pick today or a later date to book.",
              }),
              !!v &&
              e.jsx("p", {
                className: "choice-card__meta",
                style: { margin: 0 },
                children: isCustom
`,
  ],
  [
    "past start: continue stays off",
    `  const canContinue = isVO
    ? !!voPlan && !!b
    : isM
      ? !!mMonths && !!b
      : !!v && A >= b && nDays > 0 && (W ? !!i : !0);
`,
    `  const canContinue = !pastStart && (isVO
    ? !!voPlan && !!b
    : isM
      ? !!mMonths && !!b
      : !!v && A >= b && nDays > 0 && (W ? !!i : !0));
`,
  ],
]);

// ---- Passes -------------------------------------------------------------------------------------
patchFile(A("PassesPage-CxAkHUg-.js"), "PassesPage-CxAkHUg-.js", [
  [
    "today in IST",
    `function TODAY(){`,
    `function RGtodayIst(){return new Date(Date.now()+198e5).toISOString().slice(0,10)} function TODAY(){`,
  ],
  [
    "availability panel: past date",
    `if (state.status === "checking") {`,
    `if (state.status === "past") {
    return line("var(--color-danger)", "This date has passed. A pass can only start today or later. Pick today or a later date.", "past");
  }
  if (state.status === "checking") {`,
  ],
  [
    "availability check: past date is not looked up",
    `if(d!==1||!o||!K||!u||!RGdesk){RGsetAv({status:"idle"});return}let dead=!1;`,
    `if(d!==1||!o||!K||!u||!RGdesk){RGsetAv({status:"idle"});return}if(K<RGtodayIst()){RGsetAv({status:"past"});return}let dead=!1;`,
  ],
  [
    "past date: pay buttons stay off",
    `const RGready=(!(d===1)||(K!==""&&RGav.status==="ok"))&&!(RGopt&&!RGopt.allowed);`,
    `const RGpast=K!==""&&K<RGtodayIst(),RGready=!RGpast&&(!(d===1)||(K!==""&&RGav.status==="ok"))&&!(RGopt&&!RGopt.allowed);`,
  ],
  [
    "past date: refused before paying",
    `if(ee&&!K){a.error("Pick your first visit date before paying.");return}`,
    `if(RGpast){a.error("That date has passed. Pick today or a later date.");return}if(ee&&!K){a.error("Pick your first visit date before paying.");return}`,
  ],
  [
    "Starts on: no earliest date",
    `value:K,min:new Date().toISOString().slice(0,10),hint:ee?(`,
    `value:K,hint:RGpast?(ee?void 0:"This date has passed. A pass can only start today or later."):ee?(`,
  ],
]);

// ---- sw.js --------------------------------------------------------------------------------------
const swf = path.join(front, "sw.js");
let sw = fs.readFileSync(swf, "utf8");
for (const url of ["assets/BookWorkspacePage-MB2.js", "assets/PassesPage-CxAkHUg-.js", "index.html"]) {
  const re = new RegExp(`\\{url:"${url.replace(/[.\-]/g, (c) => "\\" + c)}",revision:(null|"[^"]*")\\}`);
  if (!re.test(sw)) throw new Error(`sw.js: no entry for ${url}`);
  sw = sw.replace(re, `{url:"${url}",revision:"${REV}"}`);
}
fs.writeFileSync(swf, sw, "utf8");
console.log(`OK  - sw.js revisions ${REV}`);
