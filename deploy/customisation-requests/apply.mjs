// Customisation requests from Book Workspace (23 Oct 2026).
//   node apply.mjs <frontend dir> [<staged workspace-types assets dir>]
//
// Adds a fifth "Customisation" card beside Virtual Office on Book Workspace. Picking it
// swaps the availability area for a short form: what to customise (Desk, Cabin,
// Meeting Room, Virtual Office), a free-text description and a Submit button. The
// request is raised as a support ticket in the "Workspace Customisation" category
// (db/migrations/20261023000000_WorkspaceCustomisationTickets.sql), so it reaches
// Admin > Support tickets and the admin notification centre as "Needs action", and the
// team replies on the ticket. Without that category the form falls back to "Others".
//
// Patches BookWorkspacePage-MB2.js in place (keeps its name) and bumps sw.js. With a
// second argument it also patches the staged BookWorkspacePage-MB3.js, so deploying
// workspace types later keeps this change. Idempotent.
import fs from "fs";
import path from "path";

const [front, staged] = process.argv.slice(2);
if (!front) throw new Error("usage: node apply.mjs <frontend dir> [<staged assets dir>]");
const REV = "custom-req-1";
const MARK = "Customisation (custom-req-1)";

const once = (t, from, to, where) => {
  const n = t.split(from).length - 1;
  if (n !== 1) throw new Error(`${where}: expected 1 match, found ${n}; nothing written`);
  return t.replace(from, () => to);
};

const STATE = `  const isVO = !!VOT && v === String(VOT.workspaceTypeId);

  /* ${MARK}: a fifth card beside Virtual Office. The request is raised as
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
      const when = W ? dayLabel(b) : \`\${dayLabel(b)} to \${dayLabel(A)}\`;
      const t = await fe(
        je.post("/tickets", {
          ticketCategoryId: cat.ticketCategoryId,
          ticketIssueTypeId: issue.ticketIssueTypeId,
          subject: \`Customisation request · \${cKind}\`,
          description: \`\${text}\\n\\nRequested from Book Workspace · \${cKind}\${K ? \` · \${K.locationName}\` : ""} · \${when}\`,
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
        className: \`choice-card\${isCustom ? " choice-card--selected" : ""}\`,
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
          title: \`Request \${cDone.ticketNumber} sent to the Regalis team\`,
          description:
            "We'll set up the space as requested and reply on the request itself. You can follow it under Support & Contact.",
          action: e.jsxs("div", {
            style: { display: "flex", gap: "var(--space-2)", flexWrap: "wrap", justifyContent: "center" },
            children: [
              e.jsx(Se, {
                to: \`/support/tickets/\${cDone.ticketId}\`,
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
                        className: \`wizard-step\${cKind === t ? " wizard-step--active" : ""}\`,
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
`;

function patch(file) {
  const name = path.basename(file);
  let t = fs.readFileSync(file, "utf8");
  if (t.includes(MARK)) {
    console.log(`OK  - ${name}: already patched`);
    return;
  }
  const w = (what) => `${name}: ${what}`;

  // 1. state, request, card and panel — right after isVO
  t = once(t, `  const isVO = !!VOT && v === String(VOT.workspaceTypeId);\n`, STATE, w("state"));

  // 2. no slot lookup for the Customisation card
  t = once(t, `    if (!j || !v || !b || !W || isVO || isM) {`, `    if (!j || !v || !b || !W || isVO || isM || isCustom) {`, w("slots guard"));
  t = once(t, `  }, [j, v, b, W, isVO, isM]);`, `  }, [j, v, b, W, isVO, isM, isCustom]);`, w("slots deps"));

  // 3. duration is meaningless for a request; the date hint explains the form
  t = once(
    t,
    `                  !isVO &&\n                    !isM &&\n                    W &&\n                    e.jsx(ve, {\n                      label: "Duration",`,
    `                  !isVO &&\n                    !isM &&\n                    !isCustom &&\n                    W &&\n                    e.jsx(ve, {\n                      label: "Duration",`,
    w("duration"),
  );
  t = once(
    t,
    `                children: isVO\n                  ? "A Virtual Office is sold by the month, not by the hour — the minimum term is 1 month."`,
    `                children: isCustom\n                  ? "Tell us what you need for these dates — the Regalis team sets it up and replies on your request."\n                  : isVO\n                  ? "A Virtual Office is sold by the month, not by the hour — the minimum term is 1 month."`,
    w("date hint"),
  );

  // 4. the card, after the workspace types
  t = once(
    t,
    `                className: "choice-grid",\n                children: T.map((t) =>`,
    `                className: "choice-grid",\n                children: [\n                  ...T.map((t) =>`,
    w("card: open"),
  );
  t = once(
    t,
    `                    t.workspaceTypeId,\n                  ),\n                ),\n              }),\n\n              !v\n                ? e.jsx(E, {\n                    title: "Pick a workspace type",`,
    `                    t.workspaceTypeId,\n                  ),\n                ),\n                  customCard(),\n                ],\n              }),\n\n              !v\n                ? e.jsx(E, {\n                    title: "Pick a workspace type",`,
    w("card: close"),
  );

  // 5. the form in place of the availability area
  t = once(
    t,
    `                      "Choose Desk, Cabin or Meeting Room to see live availability for the dates above — or Virtual Office for a monthly business address.",\n                  })\n                : isVO`,
    `                      "Choose Desk, Cabin or Meeting Room to see live availability for the dates above — or Virtual Office for a monthly business address. Pick Customisation to tell us what you need.",\n                  })\n                : isCustom\n                  ? customPanel()\n                : isVO`,
    w("panel"),
  );

  // 6. no "Find available workspaces" button for a request
  t = once(
    t,
    `              e.jsx(h, {\n                size: "lg",\n                disabled: !canContinue || busy,`,
    `              !isCustom &&\n              e.jsx(h, {\n                size: "lg",\n                disabled: !canContinue || busy,`,
    w("find button"),
  );

  // 7. wizard steps
  t = once(
    t,
    `  const X = isVO\n    ? [\n        { key: "when", label: "1 · Term & start date" },`,
    `  const X = isCustom\n    ? [\n        { key: "when", label: "1 · What you need" },\n        { key: "done", label: "2 · Sent to the Regalis team" },\n      ]\n    : isVO\n    ? [\n        { key: "when", label: "1 · Term & start date" },`,
    w("steps"),
  );
  t = once(
    t,
    `  const Z = X.findIndex((t) => t.key === f);`,
    `  const Z = isCustom && cDone ? 1 : X.findIndex((t) => t.key === f);`,
    w("step index"),
  );

  fs.writeFileSync(file, t, "utf8");
  console.log(`OK  - ${name}: customisation card, form and request`);
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
