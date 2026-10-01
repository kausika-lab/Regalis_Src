/* Excel export + import for the Assets screens (Items, Incoming Goods, Outgoing Goods,
   Current Stock). Hand-written chunk shared by AdminInventoryPages-IX1.js and
   AdminInventoryGoodsPages-IX1.js so every screen uses the same code.

   The files are made and read on the server (ClosedXML) — there is no Excel library in
   the browser. The preview is the server's own check of the uploaded file; "Import" sends
   the same file again and the server checks it once more inside one transaction before
   saving. Nothing is kept between the calls. */
import {
  r as React,
  j as e,
  p as Button,
  a as useToast,
  E as Api,
  D as unwrap,
  ab as Dom,
  ac as XIcon,
} from "./index-DI5X4c9f.js";
import { B as Badge } from "./Badge-BU5iAxex.js";
import { D as DownloadIcon } from "./download-Dlifs1Q2.js";

const MAX_BYTES = 5 * 1024 * 1024;
const PREVIEW_LIMIT = 500;

/* Styles for the pieces the shared CSS bundle has no class for. Colours come from the
   theme tokens, so light and dark both work. */
(() => {
  if (typeof document === "undefined" || document.getElementById("ix-style")) return;
  const s = document.createElement("style");
  s.id = "ix-style";
  s.textContent = `
.ix-date{display:inline-flex;align-items:center;gap:var(--space-2);font-size:var(--font-size-sm);color:var(--text-secondary)}
.ix-date .inv-filter{min-width:0;cursor:text}
.ix-modal{max-width:min(1040px,100%)!important}
.ix-modal .modal__footer{flex-wrap:wrap;justify-content:flex-end;gap:var(--space-2)}
.ix-steps{display:grid;gap:var(--space-4)}
.ix-step{display:flex;gap:var(--space-3);align-items:flex-start}
.ix-step__n{flex:0 0 26px;height:26px;border-radius:50%;display:grid;place-items:center;font-weight:700;font-size:var(--font-size-xs);background:rgb(var(--accent-rgb) / 16%);color:var(--text-primary)}
.ix-step__body{flex:1;min-width:0;display:grid;gap:var(--space-2)}
.ix-step__title{font-weight:600}
.ix-muted{color:var(--text-muted);font-size:var(--font-size-xs);margin:0}
.ix-drop{display:flex;align-items:center;gap:var(--space-3);padding:var(--space-4);border:1px dashed var(--border-strong);border-radius:var(--radius-md);background:var(--surface-inset);cursor:pointer;flex-wrap:wrap}
.ix-drop:hover,.ix-drop--over{border-color:rgb(var(--accent-rgb));background:rgb(var(--accent-rgb) / 8%)}
.ix-drop:focus-within{outline:2px solid rgb(var(--accent-rgb));outline-offset:2px}
.ix-drop input{position:absolute;width:1px;height:1px;opacity:0;pointer-events:none}
.ix-choice{display:flex;gap:var(--space-4);flex-wrap:wrap;font-size:var(--font-size-sm)}
.ix-choice label{display:inline-flex;align-items:center;gap:var(--space-2);cursor:pointer}
.ix-choice label[aria-disabled="true"]{opacity:.55;cursor:not-allowed}
.ix-summary{display:flex;gap:var(--space-2);flex-wrap:wrap;align-items:center}
.ix-tabs{display:flex;gap:var(--space-1);flex-wrap:wrap}
.ix-table-wrap{position:relative;max-height:min(46vh,420px);overflow:auto;border:1px solid var(--border-subtle);border-radius:var(--radius-md)}
.ix-table-wrap .data-table th{position:sticky;top:0;background:var(--surface-card);z-index:1}
.ix-table-wrap .data-table td,.ix-table-wrap .data-table th{padding:var(--space-2) var(--space-3);white-space:nowrap}
.ix-row--ok td{background:color-mix(in srgb,var(--color-success) 7%,transparent)}
.ix-row--error td{background:color-mix(in srgb,var(--color-danger) 8%,transparent)}
.ix-row--skip td{color:var(--text-muted)}
.ix-issues{white-space:normal!important;min-width:260px;max-width:420px}
.ix-issues ul{margin:0;padding-left:1.1em}
.ix-issues .ix-err{color:var(--color-danger)}
.ix-done{padding:var(--space-4);border:1px solid var(--color-success);border-radius:var(--radius-md);background:color-mix(in srgb,var(--color-success) 8%,transparent)}
`;
  document.head.appendChild(s);
})();

const UploadIcon = e.jsxs("svg", {
  width: 16,
  height: 16,
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 2,
  strokeLinecap: "round",
  strokeLinejoin: "round",
  "aria-hidden": !0,
  children: [
    e.jsx("path", { d: "M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4" }),
    e.jsx("path", { d: "m17 8-5-5-5 5" }),
    e.jsx("path", { d: "M12 3v12" }),
  ],
});

const SheetIcon = e.jsxs("svg", {
  width: 22,
  height: 22,
  viewBox: "0 0 24 24",
  fill: "none",
  stroke: "currentColor",
  strokeWidth: 1.8,
  strokeLinecap: "round",
  strokeLinejoin: "round",
  "aria-hidden": !0,
  children: [
    e.jsx("path", { d: "M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z" }),
    e.jsx("path", { d: "M14 2v6h6M8 13h8M8 17h8M10 9H8" }),
  ],
});

/* ── Helpers ─────────────────────────────────────────────────────────── */

const pad = (n) => String(n).padStart(2, "0");
/* screen_yyyy-MM-dd_HHmm.xlsx — only used if the server's file name is not readable. */
const stampName = (screen) => {
  const d = new Date();
  return `${screen}_${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}_${pad(d.getHours())}${pad(d.getMinutes())}.xlsx`;
};

const dropEmpty = (params) => {
  const out = {};
  for (const [k, v] of Object.entries(params || {}))
    if (v !== void 0 && v !== null && v !== "" && v !== !1) out[k] = v;
  return out;
};

/* The message from an API failure. A download (responseType blob) keeps its JSON
   envelope inside a Blob, so read it back first. */
async function errorText(err, fallback) {
  let body = err?.response;
  if (typeof Blob !== "undefined" && body instanceof Blob) {
    try {
      body = JSON.parse(await body.text());
    } catch {
      body = null;
    }
  }
  if (body && typeof body === "object") {
    const first =
      Array.isArray(body.errors) && body.errors.length ? body.errors[0]?.message : null;
    if (first || body.message) return first || body.message;
  }
  if (err?.status === 413) return "The file is too large to upload. The limit is 5 MB.";
  if (err?.status === 403) return "You do not have permission to do this.";
  return fallback;
}

function saveBlob(res, fallbackName) {
  const cd = res.headers?.["content-disposition"] ?? "";
  const m = /filename\*=UTF-8''([^;]+)/i.exec(cd) || /filename="?([^";]+)"?/i.exec(cd);
  const name = m ? decodeURIComponent(m[1]) : fallbackName;
  const url = URL.createObjectURL(res.data);
  const a = document.createElement("a");
  a.href = url;
  a.download = name;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1500);
  return name;
}

const isEmptyFile = (res) => res.status === 204 || !res.data || res.data.size === 0;

/* ── Export ──────────────────────────────────────────────────────────── */

/* [busy, run]: run(params) downloads `path` as .xlsx with the screen's filters. The
   server returns every matching row (not one page); 204 means nothing matched. */
function useExcelExport(path, screen) {
  const toast = useToast(),
    [busy, setBusy] = React.useState(!1),
    ref = React.useRef(!1);
  const run = async (params) => {
    if (ref.current) return;
    ref.current = !0;
    setBusy(!0);
    try {
      const res = await Api.get(path, { params: dropEmpty(params), responseType: "blob" });
      if (isEmptyFile(res)) {
        toast.warning("There are no records to export for the current filters.");
        return;
      }
      saveBlob(res, stampName(screen));
      toast.success("Excel file downloaded.");
    } catch (err) {
      toast.error(await errorText(err, "Export failed. Please try again."));
    } finally {
      ref.current = !1;
      setBusy(!1);
    }
  };
  return [busy, run];
}

function ExportExcelButton({ busy, onClick }) {
  return e.jsxs(Button, {
    variant: "outline",
    onClick,
    loading: busy,
    disabled: busy,
    children: [e.jsx(DownloadIcon, { size: 16 }), busy ? " Preparing…" : " Export Excel"],
  });
}

function ImportExcelButton({ onClick }) {
  return e.jsxs(Button, {
    variant: "outline",
    onClick,
    children: [UploadIcon, " Import Excel"],
  });
}

/* From / To date filter for the goods screens (same look as the other toolbar filters). */
function DateRange({ from, to, onChange }) {
  return e.jsxs(e.Fragment, {
    children: [
      e.jsxs("label", {
        className: "ix-date",
        children: [
          e.jsx("span", { children: "From" }),
          e.jsx("input", {
            type: "date",
            className: "inv-filter",
            "aria-label": "From date",
            value: from,
            max: to || void 0,
            onChange: (t) => onChange(t.target.value, to),
          }),
        ],
      }),
      e.jsxs("label", {
        className: "ix-date",
        children: [
          e.jsx("span", { children: "To" }),
          e.jsx("input", {
            type: "date",
            className: "inv-filter",
            "aria-label": "To date",
            value: to,
            min: from || void 0,
            onChange: (t) => onChange(from, t.target.value),
          }),
        ],
      }),
      (from || to) &&
        e.jsx(Button, {
          size: "sm",
          variant: "ghost",
          onClick: () => onChange("", ""),
          children: "Clear dates",
        }),
    ],
  });
}

/* ── Import ──────────────────────────────────────────────────────────── */

const RULES = {
  items: [
    "Stock is not imported here — new items start at 0. Bring stock in with an Incoming Goods import.",
    "Item codes are matched ignoring case and spaces at either end. A code may appear only once in the file.",
    "Location must be an existing location. Tax must be Non-Taxable, 0%, 5%, 12%, 18% or 28%.",
  ],
  incoming: [
    "Each row must use an existing goods item code. Services hold no stock and are refused.",
    "Quantity must be greater than 0. Dates cannot be in the future.",
    "Every row is recorded on the ledger like an entry made on this screen, with you as the person who recorded it.",
  ],
  outgoing: [
    "Each row must use an existing goods item code. Services hold no stock and are refused.",
    "Quantity must be greater than 0. Dates cannot be in the future.",
    "Stock cannot go below zero: rows are applied to current stock in date order, together with the other rows in the file.",
  ],
};

const STATUS_TONE = { New: "success", Update: "info", Skip: "neutral", Error: "danger" };
const STATUS_LABEL = { New: "Valid", Update: "Update", Skip: "Skipped", Error: "Error" };
const rowClass = (s) => (s === "Error" ? "ix-row--error" : s === "Skip" ? "ix-row--skip" : "ix-row--ok");

function ImportExcelDialog({ kind, title, canUpdate, onClose, onImported }) {
  const toast = useToast(),
    [file, setFile] = React.useState(null),
    [mode, setMode] = React.useState("skip"),
    [result, setResult] = React.useState(null),
    [problem, setProblem] = React.useState(""),
    [checking, setChecking] = React.useState(!1),
    [saving, setSaving] = React.useState(!1),
    [done, setDone] = React.useState(null),
    [tplBusy, setTplBusy] = React.useState(!1),
    [errBusy, setErrBusy] = React.useState(!1),
    [filter, setFilter] = React.useState("all"),
    [over, setOver] = React.useState(!1),
    inputRef = React.useRef(null),
    items = kind === "items",
    base = `/inventory/${kind}/import`,
    busy = checking || saving;

  const close = React.useCallback(() => {
    if (!saving) onClose();
  }, [saving, onClose]);

  React.useEffect(() => {
    const k = (t) => t.key === "Escape" && close();
    document.addEventListener("keydown", k);
    return () => document.removeEventListener("keydown", k);
  }, [close]);

  const form = (f, m) => {
    const fd = new FormData();
    fd.append("file", f, f.name);
    if (items) fd.append("existing", m);
    return fd;
  };
  const multipart = { headers: { "Content-Type": "multipart/form-data" } };

  const check = async (f, m) => {
    setChecking(!0);
    setProblem("");
    setResult(null);
    setDone(null);
    setFilter("all");
    try {
      setResult(await unwrap(Api.post(`${base}/preview`, form(f, m), multipart)));
    } catch (err) {
      setProblem(await errorText(err, "The file could not be checked. Please try again."));
    } finally {
      setChecking(!1);
    }
  };

  /* Checks done in the browser before anything is uploaded; the server repeats them. */
  const pick = (f) => {
    if (!f) return;
    const name = (f.name || "").toLowerCase();
    setResult(null);
    setDone(null);
    if (name.endsWith(".xls")) {
      setFile(null);
      setProblem("Old .xls files are not supported. Open the file in Excel, save it as .xlsx and upload it again.");
      return;
    }
    if (!name.endsWith(".xlsx")) {
      setFile(null);
      setProblem("Only Excel .xlsx files can be imported.");
      return;
    }
    if (f.size === 0) {
      setFile(null);
      setProblem("The file is empty.");
      return;
    }
    if (f.size > MAX_BYTES) {
      setFile(null);
      setProblem(`The file is ${(f.size / 1048576).toFixed(1)} MB. The limit is 5 MB.`);
      return;
    }
    setFile(f);
    check(f, mode);
  };

  const changeMode = (m) => {
    setMode(m);
    if (file) check(file, m);
  };

  const download = async (req, fallback, setBusy, failText) => {
    setBusy(!0);
    try {
      const res = await req();
      saveBlob(res, fallback);
    } catch (err) {
      toast.error(await errorText(err, failText));
    } finally {
      setBusy(!1);
    }
  };

  const template = () =>
    download(
      () => Api.get(`${base}/template`, { responseType: "blob" }),
      `${kind}_import_template.xlsx`,
      setTplBusy,
      "The template could not be downloaded."
    );

  const errorReport = () =>
    download(
      () => Api.post(`${base}/errors`, form(file, mode), { ...multipart, responseType: "blob" }),
      stampName(`${kind}_import_errors`),
      setErrBusy,
      "The error report could not be downloaded."
    );

  const save = async () => {
    if (!file || !result) return;
    setSaving(!0);
    setProblem("");
    try {
      const r = await unwrap(Api.post(base, form(file, mode), multipart));
      setResult(r);
      setDone(r);
      const n = r.createdCount + r.updatedCount;
      toast.success(
        items
          ? `Imported ${n} item${n === 1 ? "" : "s"}: ${r.createdCount} new, ${r.updatedCount} updated.`
          : `Imported ${n} ${kind} entr${n === 1 ? "y" : "ies"}. Current stock is updated.`
      );
      onImported && onImported(r);
    } catch (err) {
      const msg = await errorText(err, "The import failed. Nothing was saved.");
      setProblem(msg + " Nothing was saved.");
      toast.error(msg);
    } finally {
      setSaving(!1);
    }
  };

  const reset = () => {
    setFile(null);
    setResult(null);
    setDone(null);
    setProblem("");
    if (inputRef.current) inputRef.current.value = "";
  };

  const rows = result
    ? result.rows.filter((r) =>
        filter === "valid" ? r.status !== "Error" : filter === "errors" ? r.status === "Error" : !0
      )
    : [];
  const shown = rows.slice(0, PREVIEW_LIMIT);
  const importable = result ? result.validRows : 0;

  /* ── pieces ── */
  const step = (n, t, body) =>
    e.jsxs("div", {
      className: "ix-step",
      children: [
        e.jsx("div", { className: "ix-step__n", "aria-hidden": !0, children: n }),
        e.jsxs("div", {
          className: "ix-step__body",
          children: [e.jsx("div", { className: "ix-step__title", children: t }), body],
        }),
      ],
    });

  const templateStep = step(
    1,
    "Download the template",
    e.jsxs(e.Fragment, {
      children: [
        e.jsx("div", {
          children: e.jsxs(Button, {
            size: "sm",
            variant: "outline",
            onClick: template,
            loading: tplBusy,
            disabled: tplBusy,
            children: [e.jsx(DownloadIcon, { size: 14 }), " Download template (.xlsx)"],
          }),
        }),
        e.jsx("p", {
          className: "ix-muted",
          children:
            "It has the correct headers, one example row, and an Instructions sheet listing the required columns, allowed values and date format.",
        }),
      ],
    })
  );

  const uploadStep = step(
    2,
    "Upload your file",
    e.jsxs(e.Fragment, {
      children: [
        e.jsxs("label", {
          className: "ix-drop" + (over ? " ix-drop--over" : ""),
          onDragOver: (t) => {
            t.preventDefault();
            setOver(!0);
          },
          onDragLeave: () => setOver(!1),
          onDrop: (t) => {
            t.preventDefault();
            setOver(!1);
            !busy && pick(t.dataTransfer.files?.[0]);
          },
          children: [
            SheetIcon,
            e.jsxs("div", {
              style: { flex: 1, minWidth: 0 },
              children: [
                e.jsx("div", {
                  style: { fontWeight: 600, overflowWrap: "anywhere" },
                  children: file ? file.name : "Choose an .xlsx file or drop it here",
                }),
                e.jsx("div", {
                  className: "ix-muted",
                  children: file
                    ? `${(file.size / 1024).toFixed(0)} KB`
                    : "Excel .xlsx only · up to 5 MB and 5,000 rows",
                }),
              ],
            }),
            e.jsx("span", {
              className: "btn btn--outline btn--sm",
              "aria-hidden": !0,
              children: file ? "Change file" : "Browse…",
            }),
            e.jsx("input", {
              ref: inputRef,
              type: "file",
              accept: ".xlsx,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
              disabled: busy,
              "aria-label": "Excel file to import",
              onChange: (t) => pick(t.target.files?.[0]),
            }),
          ],
        }),
        items &&
          e.jsxs("div", {
            className: "ix-choice",
            role: "radiogroup",
            "aria-label": "When an item code already exists",
            children: [
              e.jsx("span", { className: "ix-muted", style: { alignSelf: "center" }, children: "When an item code already exists:" }),
              e.jsxs("label", {
                children: [
                  e.jsx("input", {
                    type: "radio",
                    name: "ix-mode",
                    checked: mode === "skip",
                    disabled: busy || !!done,
                    onChange: () => changeMode("skip"),
                  }),
                  "Skip existing",
                ],
              }),
              e.jsxs("label", {
                "aria-disabled": !canUpdate,
                title: canUpdate ? void 0 : "Needs the Inventory Edit permission",
                children: [
                  e.jsx("input", {
                    type: "radio",
                    name: "ix-mode",
                    checked: mode === "update",
                    disabled: !canUpdate || busy || !!done,
                    onChange: () => changeMode("update"),
                  }),
                  "Update existing",
                ],
              }),
            ],
          }),
        e.jsx("ul", {
          className: "ix-muted",
          style: { paddingLeft: "1.1em", display: "grid", gap: 2 },
          children: RULES[kind].map((t) => e.jsx("li", { children: t }, t)),
        }),
      ],
    })
  );

  const summary =
    result &&
    e.jsxs("div", {
      className: "ix-summary",
      role: "status",
      children: [
        e.jsx("strong", {
          children: `${result.validRows} valid, ${result.errorRows} with errors`,
        }),
        result.skippedRows > 0 && e.jsx(Badge, { tone: "neutral", children: `${result.skippedRows} skipped (already exist)` }),
        items &&
          mode === "update" &&
          result.existingRows > 0 &&
          e.jsx(Badge, { tone: "info", children: `${result.existingRows} will update existing items` }),
        e.jsx("span", { className: "ix-muted", children: `· ${result.totalRows} rows read` }),
      ],
    });

  const previewStep =
    result &&
    step(
      3,
      done ? "Result" : "Check the preview",
      e.jsxs(e.Fragment, {
        children: [
          done &&
            e.jsxs("div", {
              className: "ix-done",
              role: "status",
              children: [
                e.jsx("strong", {
                  children: items
                    ? `Saved: ${done.createdCount} new item(s), ${done.updatedCount} updated.`
                    : `Saved: ${done.createdCount} ledger entr${done.createdCount === 1 ? "y" : "ies"}. Current stock is updated.`,
                }),
                done.errorRows > 0 &&
                  e.jsx("div", {
                    className: "ix-muted",
                    style: { marginTop: 4 },
                    children: `${done.errorRows} row(s) were not imported. Download the error report, fix them and upload that file again.`,
                  }),
              ],
            }),
          summary,
          result.ignoredColumns?.length > 0 &&
            e.jsx("p", {
              className: "ix-muted",
              children: `Ignored columns (not used by this import): ${result.ignoredColumns.join(", ")}.`,
            }),
          e.jsx("div", {
            className: "ix-tabs",
            role: "tablist",
            children: [
              ["all", `All (${result.totalRows})`],
              ["valid", `No errors (${result.totalRows - result.errorRows})`],
              ["errors", `Errors (${result.errorRows})`],
            ].map(([k, l]) =>
              e.jsx(
                Button,
                {
                  size: "sm",
                  variant: filter === k ? "primary" : "ghost",
                  role: "tab",
                  "aria-selected": filter === k,
                  onClick: () => setFilter(k),
                  children: l,
                },
                k
              )
            ),
          }),
          rows.length === 0
            ? e.jsx("p", { className: "ix-muted", children: "No rows in this view." })
            : e.jsx("div", {
                className: "ix-table-wrap",
                children: e.jsxs("table", {
                  className: "data-table",
                  children: [
                    e.jsx("thead", {
                      children: e.jsxs("tr", {
                        children: [
                          e.jsx("th", { children: "Row" }),
                          e.jsx("th", { children: "Status" }),
                          e.jsx("th", { children: "Issues" }),
                          result.columns.map((c) => e.jsx("th", { children: c }, c)),
                        ],
                      }),
                    }),
                    e.jsx("tbody", {
                      children: shown.map((r) =>
                        e.jsxs(
                          "tr",
                          {
                            className: rowClass(r.status),
                            children: [
                              e.jsx("td", { children: r.rowNumber }),
                              e.jsx("td", {
                                children: e.jsx(Badge, {
                                  tone: STATUS_TONE[r.status] || "neutral",
                                  children: STATUS_LABEL[r.status] || r.status,
                                }),
                              }),
                              e.jsx("td", {
                                className: "ix-issues",
                                children:
                                  r.errors.length || r.notes.length
                                    ? e.jsxs("ul", {
                                        children: [
                                          r.errors.map((t, i) => e.jsx("li", { className: "ix-err", children: t }, "e" + i)),
                                          r.notes.map((t, i) => e.jsx("li", { className: "ix-muted", children: t }, "n" + i)),
                                        ],
                                      })
                                    : e.jsx("span", { className: "ix-muted", children: "—" }),
                              }),
                              result.columns.map((c) =>
                                e.jsx("td", { children: r.values[c] ?? e.jsx("span", { className: "ix-muted", children: "—" }) }, c)
                              ),
                            ],
                          },
                          r.rowNumber
                        )
                      ),
                    }),
                  ],
                }),
              }),
          rows.length > PREVIEW_LIMIT &&
            e.jsx("p", {
              className: "ix-muted",
              children: `Showing the first ${PREVIEW_LIMIT} of ${rows.length} rows. Every row is checked and imported; the error report lists every failed row.`,
            }),
        ],
      })
    );

  const footer = e.jsxs(e.Fragment, {
    children: [
      e.jsx(Button, { variant: "ghost", onClick: close, disabled: saving, children: done ? "Close" : "Cancel" }),
      result &&
        result.errorRows > 0 &&
        e.jsxs(Button, {
          variant: "outline",
          onClick: errorReport,
          loading: errBusy,
          disabled: errBusy || busy,
          children: [e.jsx(DownloadIcon, { size: 14 }), " Download error report"],
        }),
      done
        ? e.jsx(Button, { variant: "outline", onClick: reset, children: "Import another file" })
        : e.jsx(Button, {
            onClick: save,
            loading: saving,
            disabled: !result || importable === 0 || busy,
            children: importable > 0 ? `Import ${importable} valid row${importable === 1 ? "" : "s"}` : "Import",
          }),
    ],
  });

  return Dom.createPortal(
    e.jsx("div", {
      className: "modal-overlay",
      onClick: (t) => {
        t.target === t.currentTarget && close();
      },
      children: e.jsxs("div", {
        className: "modal ix-modal",
        role: "dialog",
        "aria-modal": "true",
        "aria-label": `Import ${title} from Excel`,
        children: [
          e.jsxs("header", {
            className: "modal__header",
            children: [
              e.jsx("h2", { className: "modal__title", children: `Import ${title} from Excel` }),
              e.jsx("button", {
                type: "button",
                className: "btn btn--ghost btn--sm",
                onClick: close,
                disabled: saving,
                "aria-label": "Close",
                children: e.jsx(XIcon, { size: 18 }),
              }),
            ],
          }),
          e.jsx("div", {
            className: "modal__body",
            children: e.jsxs("div", {
              className: "ix-steps",
              children: [
                !done && templateStep,
                !done && uploadStep,
                checking &&
                  e.jsx("p", { className: "ix-muted", role: "status", children: "Checking every row…" }),
                saving &&
                  e.jsx("p", { className: "ix-muted", role: "status", children: "Saving — this can take a moment for large files…" }),
                problem && e.jsx("div", { className: "form-error", role: "alert", children: problem }),
                previewStep,
              ],
            }),
          }),
          e.jsx("footer", { className: "modal__footer", children: footer }),
        ],
      }),
    }),
    document.body
  );
}

export {
  useExcelExport as u,
  ExportExcelButton as X,
  ImportExcelButton as M,
  ImportExcelDialog as I,
  DateRange as R,
};
