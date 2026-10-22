/*!
 * AssetQr-AS1.js - QR encoder for the Assets QR labels (Regalis).
 * Built from qrcode 1.5.4 (lib/core/qrcode.js), MIT License, Copyright (c) 2012 Ryan Day.
 * https://github.com/soldair/node-qrcode
 */
import QRCode from "C:/Users/Administrator/Downloads/Regalis/frontend/node_modules/qrcode/lib/core/qrcode.js";

/** The module matrix for a text: { size, dark(x, y) }. Error correction level Q by
 *  default, so a scratched or partly painted-over label still scans (spec §9.1). */
export function qrMatrix(text, level = "Q") {
  const code = QRCode.create(text, { errorCorrectionLevel: level });
  const size = code.modules.size;
  const data = code.modules.data;
  return { size, version: code.version, dark: (x, y) => data[y * size + x] === 1 };
}

/** One SVG path for every dark module, with a quiet zone of `margin` modules. */
export function qrPath(text, level = "Q", margin = 2) {
  const m = qrMatrix(text, level);
  let d = "";
  for (let y = 0; y < m.size; y++) {
    let run = 0;
    for (let x = 0; x <= m.size; x++) {
      if (x < m.size && m.dark(x, y)) { run++; continue; }
      if (run > 0) {
        d += `M${x - run + margin} ${y + margin}h${run}v1h-${run}z`;
        run = 0;
      }
    }
  }
  return { d, viewBox: m.size + margin * 2 };
}
