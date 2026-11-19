import fs from "fs";
// Loads the frontend QR module and exposes its bundled qrcode library (export list extended in a temp copy).
import os from "os";
import { pathToFileURL } from "url";
import path from "path";
const libSrc = fs.readFileSync(process.argv[4], "utf8").replace("export{gn as qrMatrix,Fn as qrPath}", "export{gn as qrMatrix,Fn as qrPath,pe as qrLib}");
const libFile = path.join(os.tmpdir(), "rg-qrlib-" + process.pid + ".mjs");
fs.writeFileSync(libFile, libSrc);
const { qrLib } = await import(pathToFileURL(libFile).href);

const lib = qrLib.default ?? qrLib;
const texts = fs.readFileSync(process.argv[2], "utf8").split(/\r?\n/);
const cs = fs.readFileSync(process.argv[3], "utf8").trim().split("\n");
let i = 0, ok = 0, bad = 0;
for (const t of texts) {
  for (let k = -1; k < 8; k++) {
    const line = cs[i++]; const [mk, bits] = line.split("|");
    if (k < 0) { // auto mask: just check it equals one of the fixed-mask results printed next
      const rest = cs.slice(i, i + 8).map((l) => l.split("|")[1]);
      if (rest.includes(bits)) ok++; else { bad++; console.log("auto mask not one of 0-7", t.slice(0, 30)); }
      continue;
    }
    const q = lib.create([{ data: t, mode: "byte" }], { errorCorrectionLevel: "M", maskPattern: k });
    const n = q.modules.size, d = q.modules.data;
    const rows = [];
    for (let y = 0; y < n; y++) { let r = ""; for (let x = 0; x < n; x++) r += d[y * n + x] ? "1" : "0"; rows.push(r); }
    if (rows.join("/") === bits) ok++; else { bad++; console.log(`MISMATCH mask ${k} v${q.version} '${t.slice(0, 30)}'`); }
  }
}
console.log(`QR check: ${ok} match, ${bad} differ`);
