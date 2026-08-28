/**
 * Generates public/icons/icon-192.png and icon-512.png for the PWA manifest by
 * rasterising the brand "R" mark (dark tile, gold letter) into an RGBA buffer
 * and encoding a PNG with node's built-in zlib — no image dependencies needed.
 *
 * Run: npm run icons
 */
import { deflateSync } from 'node:zlib';
import { writeFileSync, mkdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const here = dirname(fileURLToPath(import.meta.url));
const outDir = join(here, '..', 'public', 'icons');
mkdirSync(outDir, { recursive: true });

const BLACK = [0x0b, 0x0b, 0x0d, 255];
const GOLD = [0xc9, 0xa2, 0x27, 255];

/** Renders the icon at the given size. All geometry is proportional to 512. */
function render(size) {
  const px = new Uint8Array(size * size * 4);
  const s = size / 512;

  const put = (x, y, [r, g, b, a]) => {
    if (x < 0 || y < 0 || x >= size || y >= size) return;
    const i = (y * size + x) * 4;
    px[i] = r;
    px[i + 1] = g;
    px[i + 2] = b;
    px[i + 3] = a;
  };

  // Background: rounded dark tile.
  const radius = 96 * s;
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const cx = Math.min(Math.max(x, radius), size - radius);
      const cy = Math.min(Math.max(y, radius), size - radius);
      const inside = (x - cx) ** 2 + (y - cy) ** 2 <= radius ** 2;
      put(x, y, inside ? BLACK : [0, 0, 0, 0]);
    }
  }

  // Gold border ring following the rounded rectangle at inset 40, width 20.
  const inset = 40 * s;
  const ringRadius = 72 * s;
  const ringWidth = 20 * s;
  for (let y = 0; y < size; y++) {
    for (let x = 0; x < size; x++) {
      const lo = inset;
      const hi = size - inset;
      if (x < lo - ringWidth || x > hi + ringWidth || y < lo - ringWidth || y > hi + ringWidth) continue;
      const cx = Math.min(Math.max(x, lo + ringRadius), hi - ringRadius);
      const cy = Math.min(Math.max(y, lo + ringRadius), hi - ringRadius);
      const d = Math.sqrt((x - cx) ** 2 + (y - cy) ** 2);
      // Distance from the rounded-rect outline (its corner circle of radius ringRadius).
      const edge =
        x >= lo && x <= hi && y >= lo && y <= hi && (cx !== x || cy !== y)
          ? Math.abs(d - ringRadius)
          : Math.abs(Math.max(Math.abs(x - (lo + hi) / 2) - (hi - lo) / 2, Math.abs(y - (lo + hi) / 2) - (hi - lo) / 2));
      if (edge <= ringWidth / 2 && d >= ringRadius - ringWidth / 2) put(x, y, GOLD);
      // Straight segments of the ring.
      const nearVertical = (Math.abs(x - lo) <= ringWidth / 2 || Math.abs(x - hi) <= ringWidth / 2) && y >= lo + ringRadius && y <= hi - ringRadius;
      const nearHorizontal = (Math.abs(y - lo) <= ringWidth / 2 || Math.abs(y - hi) <= ringWidth / 2) && x >= lo + ringRadius && x <= hi - ringRadius;
      if (nearVertical || nearHorizontal) put(x, y, GOLD);
    }
  }

  // Letter R built from strokes (proportional to the SVG mark).
  const rect = (x0, y0, x1, y1) => {
    for (let y = Math.round(y0 * s); y < Math.round(y1 * s); y++)
      for (let x = Math.round(x0 * s); x < Math.round(x1 * s); x++) put(x, y, GOLD);
  };
  // Stem
  rect(170, 140, 216, 372);
  // Top bar
  rect(170, 140, 278, 178);
  // Bowl right side
  rect(310, 168, 356, 248);
  // Bowl bottom bar
  rect(216, 210, 310, 248);
  // Bowl top connector
  rect(262, 140, 310, 178);
  // Leg (diagonal, drawn as stepped strokes)
  for (let t = 0; t <= 1; t += 0.004) {
    const x = 250 + t * 106;
    const y = 248 + t * 124;
    rect(x, y, x + 46, y + 8);
  }
  return px;
}

/** Minimal PNG encoder: 8-bit RGBA, no filtering. */
function encodePng(pixels, size) {
  const crcTable = [];
  for (let n = 0; n < 256; n++) {
    let c = n;
    for (let k = 0; k < 8; k++) c = c & 1 ? 0xedb88320 ^ (c >>> 1) : c >>> 1;
    crcTable[n] = c >>> 0;
  }
  const crc32 = (buf) => {
    let c = 0xffffffff;
    for (const byte of buf) c = crcTable[(c ^ byte) & 0xff] ^ (c >>> 8);
    return (c ^ 0xffffffff) >>> 0;
  };
  const chunk = (type, data) => {
    const len = Buffer.alloc(4);
    len.writeUInt32BE(data.length);
    const body = Buffer.concat([Buffer.from(type, 'ascii'), data]);
    const crc = Buffer.alloc(4);
    crc.writeUInt32BE(crc32(body));
    return Buffer.concat([len, body, crc]);
  };

  const ihdr = Buffer.alloc(13);
  ihdr.writeUInt32BE(size, 0);
  ihdr.writeUInt32BE(size, 4);
  ihdr[8] = 8; // bit depth
  ihdr[9] = 6; // color type RGBA
  // raw scanlines, each prefixed with filter byte 0
  const raw = Buffer.alloc(size * (size * 4 + 1));
  for (let y = 0; y < size; y++) {
    raw[y * (size * 4 + 1)] = 0;
    Buffer.from(pixels.buffer, y * size * 4, size * 4).copy(raw, y * (size * 4 + 1) + 1);
  }
  return Buffer.concat([
    Buffer.from([0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a]),
    chunk('IHDR', ihdr),
    chunk('IDAT', deflateSync(raw, { level: 9 })),
    chunk('IEND', Buffer.alloc(0)),
  ]);
}

for (const size of [192, 512]) {
  const file = join(outDir, `icon-${size}.png`);
  writeFileSync(file, encodePng(render(size), size));
  console.log(`wrote ${file}`);
}
