#!/usr/bin/env node
/* Local dev website for the Regalis SPA — the stand-in for the IIS site.
 *
 * It does the two things frontend/web.config does on a real server, because the
 * SPA is built with axios baseURL "/api" and therefore only works from an origin
 * that proxies the API onto itself:
 *
 *   1. /api/..., /health..., /swagger...  ->  proxied to the API (127.0.0.1:81)
 *   2. anything else                      ->  a file under frontend/, else index.html
 *
 * Zero dependencies on purpose: this machine has no npm install step and the
 * stack has to come up offline.
 *
 * Usage:  node local-dev/web-server.mjs [webroot]      (default: frontend)
 * Env:    REGALIS_WEB_PORT (8080), REGALIS_API_PORT (81), REGALIS_API_HOST (127.0.0.1)
 */
import { createServer, request as upstreamRequest } from "node:http";
import { createReadStream } from "node:fs";
import { stat } from "node:fs/promises";
import { extname, join, resolve, sep } from "node:path";

const ROOT = resolve(process.argv[2] ?? "frontend");
const WEB_PORT = Number(process.env.REGALIS_WEB_PORT ?? 8080);
const API_HOST = process.env.REGALIS_API_HOST ?? "127.0.0.1";
const API_PORT = Number(process.env.REGALIS_API_PORT ?? 81);
const SERVE_SW = process.env.REGALIS_SERVE_SW === "1";

/* The app ships a Workbox service worker that precaches every bundle. On a real
 * site that is the point; on localhost it means the browser keeps serving the
 * copy it cached earlier and your edit never shows up, however hard you reload.
 * So in dev we answer /sw.js with this instead: it deletes every cache, removes
 * itself, and reloads the open tabs once. After that nothing is cached at all,
 * because this server sends no-store on everything.
 * Set REGALIS_SERVE_SW=1 to serve the real worker and test PWA behaviour. */
const SW_STUB = `/* Regalis local dev: service worker disabled, see local-dev/web-server.mjs */
self.addEventListener("install", () => self.skipWaiting());
self.addEventListener("activate", (event) => {
  event.waitUntil((async () => {
    for (const key of await caches.keys()) await caches.delete(key);
    await self.registration.unregister();
    for (const client of await self.clients.matchAll({ type: "window" })) {
      try { client.navigate(client.url); } catch {}
    }
  })());
});
`;

/* web.config rule "api": ^(api/.*|health.*|swagger.*)$ */
const PROXIED = /^\/(?:api\/|api$|health|swagger)/i;

const MIME = {
  ".html": "text/html; charset=utf-8",
  ".js": "text/javascript; charset=utf-8",
  ".mjs": "text/javascript; charset=utf-8",
  ".css": "text/css; charset=utf-8",
  ".json": "application/json; charset=utf-8",
  ".map": "application/json; charset=utf-8",
  ".webmanifest": "application/manifest+json",
  ".svg": "image/svg+xml",
  ".png": "image/png",
  ".jpg": "image/jpeg",
  ".jpeg": "image/jpeg",
  ".gif": "image/gif",
  ".webp": "image/webp",
  ".ico": "image/x-icon",
  ".woff": "font/woff",
  ".woff2": "font/woff2",
  ".ttf": "font/ttf",
  ".txt": "text/plain; charset=utf-8",
  ".pdf": "application/pdf",
  ".xml": "text/xml; charset=utf-8",
};

const log = (req, status, note = "") =>
  console.log(
    `${new Date().toTimeString().slice(0, 8)}  ${String(status)}  ${req.method} ${req.url}${note ? "  " + note : ""}`,
  );

/** Hand the request to the API and stream its answer straight back. */
function proxy(req, res, pathWithQuery) {
  const headers = { ...req.headers, host: `${API_HOST}:${API_PORT}` };
  delete headers["accept-encoding"]; // keep the dev window's bodies readable

  const up = upstreamRequest(
    { host: API_HOST, port: API_PORT, method: req.method, path: pathWithQuery, headers },
    (apiRes) => {
      log(req, apiRes.statusCode ?? 0, "-> api");
      res.writeHead(apiRes.statusCode ?? 502, apiRes.headers);
      apiRes.pipe(res);
    },
  );

  up.on("error", (err) => {
    log(req, 502, `api unreachable (${err.code ?? err.message})`);
    if (res.headersSent) return res.destroy();
    res.writeHead(502, { "content-type": "application/json; charset=utf-8", "cache-control": "no-store" });
    res.end(
      JSON.stringify({
        success: false,
        message: `The Regalis API is not answering on http://${API_HOST}:${API_PORT}. Check its window, or run local-dev\\start-local.cmd again.`,
      }),
    );
  });

  req.pipe(up);
}

/** Serve a file under ROOT; fall back to index.html for SPA routes. */
async function serveFile(req, res, pathname) {
  let rel;
  try {
    rel = decodeURIComponent(pathname);
  } catch {
    res.writeHead(400).end("Bad request");
    return log(req, 400, "bad escape");
  }

  let full = resolve(ROOT, "." + rel);
  if (full !== ROOT && !full.startsWith(ROOT + sep)) {
    res.writeHead(403).end("Forbidden");
    return log(req, 403, "outside the web root");
  }

  let info = await stat(full).catch(() => null);
  if (info?.isDirectory()) {
    full = join(full, "index.html");
    info = await stat(full).catch(() => null);
  }

  if (!info?.isFile()) {
    /* A missing asset stays a 404 here, unlike the IIS rule, which rewrites
       everything to index.html: a chunk name typo should show up in the Network
       tab as a 404, not as HTML being parsed as JavaScript. Extension-less paths
       are SPA routes and do fall back. */
    if (extname(rel)) {
      res.writeHead(404, { "content-type": "text/plain; charset=utf-8", "cache-control": "no-store" });
      res.end(`Not found under ${ROOT}: ${rel}`);
      return log(req, 404);
    }
    full = join(ROOT, "index.html");
    info = await stat(full).catch(() => null);
    if (!info?.isFile()) {
      res.writeHead(500).end("index.html is missing from the web root");
      return log(req, 500, "no index.html");
    }
  }

  res.writeHead(200, {
    "content-type": MIME[extname(full).toLowerCase()] ?? "application/octet-stream",
    "content-length": info.size,
    /* Nothing is cached in dev. Several bundles are patched in place without a
       new file name, so a cached copy is always the wrong copy. */
    "cache-control": "no-store, must-revalidate",
  });
  log(req, 200);

  if (req.method === "HEAD") return res.end();
  createReadStream(full).on("error", () => res.destroy()).pipe(res);
}

const server = createServer((req, res) => {
  const { pathname, search } = new URL(req.url ?? "/", "http://localhost");

  if (PROXIED.test(pathname)) return proxy(req, res, pathname + (search ?? ""));

  if (!SERVE_SW && pathname === "/sw.js") {
    res.writeHead(200, {
      "content-type": "text/javascript; charset=utf-8",
      "cache-control": "no-store, must-revalidate",
      "service-worker-allowed": "/",
    });
    log(req, 200, "service worker disabled in dev");
    return res.end(req.method === "HEAD" ? undefined : SW_STUB);
  }

  if (req.method !== "GET" && req.method !== "HEAD") {
    res.writeHead(405, { allow: "GET, HEAD" }).end("Only GET and HEAD are served from the web root");
    return log(req, 405);
  }

  serveFile(req, res, pathname).catch((err) => {
    log(req, 500, err.message);
    if (!res.headersSent) res.writeHead(500).end("Server error");
  });
});

server.on("error", (err) => {
  console.error(
    err.code === "EADDRINUSE"
      ? `\nPort ${WEB_PORT} is already taken. Close whatever is on it, or set REGALIS_WEB_PORT.\n`
      : `\n${err.message}\n`,
  );
  process.exit(1);
});

server.listen(WEB_PORT, () => {
  console.log(`Regalis website  http://localhost:${WEB_PORT}`);
  console.log(`  web root       ${ROOT}`);
  console.log(`  /api  /health  /swagger  ->  http://${API_HOST}:${API_PORT}`);
  console.log(
    SERVE_SW
      ? "  service worker  served from frontend/sw.js (REGALIS_SERVE_SW=1)"
      : "  service worker  disabled, caches purged on first load, so edits show on reload",
  );
  console.log("  Ctrl+C to stop.\n");
});
