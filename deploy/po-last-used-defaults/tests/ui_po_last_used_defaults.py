"""po-last-used-defaults kit (28 Nov 2026): headless Chrome checks, LOCAL stack only.
http://localhost:8080 (repo frontend\\, apply.mjs applied) -> API :81 (D:\\Regalis\\local-dev\\api), Dev DB RegalisWorkSpace_Dev.

Admin signs in.
  New PO   terms pre-filled from the default, the new hint text; after a PO saved through the API with
           an edited address, the new-PO screen pre-fills that location + address; when the stored last
           location is not an active location the screen falls back to the default location's address.
  Ship-to  PO detail "Shipping to" block and the "Change delivery address" picker show Purchase.ShipToName
           ('Rupesh') in place of the location name; blank setting -> location name again.
  Rest     Locations admin page and Bookings page still show the location name 'RR Nagar'.
  1366 px and 390 px: no page-wide horizontal scroll on the new-PO and detail pages.

Creates one draft PO (remarks "po-last-used-defaults ui <run>"), cancelled and soft-deleted at the end.
Every Purchase.* setting touched is put back exactly as found (rows that did not exist are deleted).
Env: PYLIB (websocket-client), REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD, SHOTS_DIR (optional)."""
import base64, json, os, random, subprocess, sys, time, urllib.error, urllib.request
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket
sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.environ.get("SHOTS_DIR", os.path.join(HERE, "..", "evidence")); os.makedirs(SHOTS, exist_ok=True)
BASE, API = "http://localhost:8080", "http://127.0.0.1:81/api"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-b"]
RUN = str(random.randint(10000, 99999))
ADMIN_EMAIL, ADMIN_PW = os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"]
TERMS, LOCK, ADDR, SHIP = "Purchase.TermsAndConditions", "Purchase.LastDeliveryLocationId", "Purchase.LastDeliveryAddress", "Purchase.ShipToName"
KEYS = [TERMS, LOCK, ADDR, SHIP]
PO_ROUTE = "/admin/billing/purchase-orders"


def sql(q):
    out = subprocess.run(SQL + ["-h", "-1", "-W", "-s", "|", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:150] + " :: " + out.stdout + out.stderr)
    return [[c.strip() for c in l.split("|")] for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q); return r[0][0] if r else None


def value_hex(key):
    # -y 0 (whole nvarchar(max)) cannot be combined with -h / -W.
    out = subprocess.run(SQL + ["-y", "0", "-Q", f"SET NOCOUNT ON; SELECT 'HEX:' + CONVERT(varchar(max), CAST(SettingValue AS varbinary(max)), 2)"
                                f" FROM MST_AppSetting WHERE SettingKey = '{key}'"], capture_output=True, text=True)
    rows = [l.strip() for l in out.stdout.splitlines() if l.strip().startswith("HEX:")]
    return rows[0][4:] if rows else None


def snap(key):
    r = sql(f"SELECT AppSettingId, ISNULL(CONVERT(varchar(30), ModifiedDate, 126), 'NULL'), ISNULL(CAST(ModifiedBy AS varchar(20)), 'NULL'),"
            f" DATALENGTH(SettingValue) / 2 FROM MST_AppSetting WHERE SettingKey = '{key}'")
    if not r:
        return None
    h = value_hex(key) or ""
    if len(h) // 4 != int(r[0][3]):
        raise SystemExit(f"could not read {key} whole")
    return [r[0][0], h, r[0][1], r[0][2]]


TOK = {}


def api(method, path, body=None):
    for attempt in range(8):
        req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
        req.add_header("Content-Type", "application/json")
        if TOK.get("t"): req.add_header("Authorization", "Bearer " + TOK["t"])
        try:
            with urllib.request.urlopen(req, timeout=60) as r: return r.status, json.loads(r.read() or b"{}")
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(10 + 5 * attempt); continue
            try: return e.code, json.loads(e.read() or b"{}")
            except Exception: return e.code, {}
    raise SystemExit("rate limited")


def put_setting(key, value):
    return api("PUT", "/settings", {"settings": [{"settingKey": key, "settingValue": value}]})


results = []
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:400]), flush=True)


FOUND = {k: snap(k) for k in KEYS}
s, b = api("POST", "/auth/login", {"email": ADMIN_EMAIL, "password": ADMIN_PW, "portal": "admin"})
if s != 200: raise SystemExit(f"login: {s}")
TOK["t"] = b["data"]["accessToken"]
s, b = api("GET", "/locations?page=1&pageSize=100")
LOCS = b["data"]["items"]
DEF = next((l for l in LOCS if l.get("isDefault")), LOCS[0] if len(LOCS) == 1 else None)
LOC_NAME = DEF["locationName"]
SHIP_NAME = (bytes.fromhex(FOUND[SHIP][1]).decode("utf-16-le").strip() if FOUND[SHIP] else "")


def address_of_location(loc):  # same formula as the chunk's addressOfLocation
    parts = [loc.get("addressLine1"), loc.get("addressLine2"), " ".join(p for p in [loc.get("city"), loc.get("postalCode")] if p), loc.get("state")]
    return ", ".join(p.strip() for p in parts if p and p.strip())


DEF_ADDR = address_of_location(DEF) or SHIP_NAME or LOC_NAME
print(f"run {RUN}; default location {DEF['locationId']} '{LOC_NAME}', ship-to name '{SHIP_NAME}'", flush=True)

CREATED, proc, ws = [], None, None
UI_ADDR = f"UI dock {RUN}, 7 Sample Road, Bengaluru 560098"
try:
    if not SHIP_NAME: raise SystemExit("Purchase.ShipToName is blank; apply 20261128100000_PurchaseShipToName.sql first")
    body = {"supplierId": 1, "locationId": DEF["locationId"], "remarks": f"po-last-used-defaults ui {RUN}", "deliveryAddress": UI_ADDR,
            "lines": [{"itemName": f"UI defaults item {RUN}", "unit": "Nos", "quantity": 1, "rate": 100, "discountAmount": 0, "taxPercent": 18}]}
    s, b = api("POST", "/purchase-orders", body)
    if s not in (200, 201): raise SystemExit(f"create PO: {s} {b}")
    PO_ID = b["data"]["purchaseOrderId"]; CREATED.append(PO_ID)

    port = 9300 + random.randint(0, 80)
    proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={port}", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-lud-prof-{RUN}",
                             "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    page = None
    for _ in range(60):
        try:
            page = next(t for t in json.loads(urllib.request.urlopen(f"http://127.0.0.1:{port}/json").read()) if t["type"] == "page"); break
        except Exception: time.sleep(0.3)
    if not page: raise SystemExit("Chrome debugging port did not answer")
    ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60); mid = 0
    def send(m, **p):
        global mid; mid += 1; ws.send(json.dumps({"id": mid, "method": m, "params": p}))
        while True:
            r = json.loads(ws.recv())
            if r.get("id") == mid: return r.get("result", {})
    def drain(s):
        end = time.time() + s; ws.settimeout(0.3)
        while time.time() < end:
            try: ws.recv()
            except Exception: pass
        ws.settimeout(60)
    def js(x): return send("Runtime.evaluate", expression=x, awaitPromise=True, returnByValue=True).get("result", {}).get("value")
    def go(p, w=3): send("Page.navigate", url=BASE + p); drain(w)
    def shot(n): open(os.path.join(SHOTS, n + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png")["data"]))
    def click(sel, txt):
        return js(f"(() => {{ const el=[...document.querySelectorAll({json.dumps(sel)})].find(e=>e.textContent.trim().includes({json.dumps(txt)}) && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
    def text(): return (js("document.body.innerText") or "").replace("\u00a0", " ")
    def wait_for(cond, s=20):
        end = time.time() + s
        while time.time() < end:
            if js(cond): return True
            drain(0.4)
        return bool(js(cond))
    SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;"
           "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}))};")
    def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
    def viewport(w): send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)
    SHIP_LINE = "(() => { const d=[...document.querySelectorAll('.pof__deliver div')].find(x=>x.textContent.startsWith('Shipping to:')); return d ? d.textContent : null })()"
    DETAIL_SHIP = "(() => { const h=[...document.querySelectorAll('h3.po-block-title')].find(x=>x.textContent.trim()==='Shipping to'); return h && h.nextElementSibling ? h.nextElementSibling.textContent.trim() : null })()"

    send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
    viewport(1366)
    js("localStorage.clear(); sessionStorage.clear(); true"); go("/admin/login", 3)
    js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(ADMIN_EMAIL)});setv(document.querySelector('input[type=password]'),{json.dumps(ADMIN_PW)});document.querySelector('form button[type=submit]').click();return true}})()")
    check("U0 admin signs in", wait_for("!location.pathname.startsWith('/admin/login')", 20), js("location.pathname"))

    # ---------------------------------------------- new PO: last address + terms
    go(PO_ROUTE + "/new", 3)
    wait_for(SHIP_LINE + " !== null && document.querySelector('#po-terms') !== null", 25)
    drain(1)
    line = js(SHIP_LINE) or ""
    check("U1 new PO pre-fills the last PO's delivery address", UI_ADDR in line, line)
    terms_default = api("GET", "/purchase-orders/form-options")[1]["data"]["defaultTermsAndConditions"] or ""
    check("U2 terms box pre-filled with the default terms", js("document.querySelector('#po-terms').value") == terms_default)
    check("U3 new hint under the terms box", "The terms you save here become the default for the next new PO." in text())
    check("U4 1366 px: no page-wide horizontal scroll on the new-PO page", no_hscroll())
    shot("lud_new_po_1366")

    # ---------------------------------------------- delivery picker shows the ship-to name
    check("U5 'Change delivery address' opens", click("button", "Change delivery address") and wait_for("!!document.querySelector('input[name=po-location]')", 10))
    labels = js("[...document.querySelectorAll('label.pof-radio strong')].map(s=>s.textContent)") or []
    check(f"U6 picker labels the location '{SHIP_NAME}', not '{LOC_NAME}'", labels and all(l == SHIP_NAME for l in labels), labels)
    shot("lud_delivery_picker")
    click("button", "Cancel"); drain(1)

    viewport(390); drain(1)
    check("U7 390 px: no page-wide horizontal scroll on the new-PO page", no_hscroll())
    shot("lud_new_po_390"); viewport(1366)

    # ---------------------------------------------- last location not active -> default
    put_setting(LOCK, "987654")
    go(PO_ROUTE + "/new", 3); wait_for(SHIP_LINE + " !== null", 25); drain(1)
    line = js(SHIP_LINE) or ""
    check("U8 last location not active: falls back to the default location's address", DEF_ADDR in line and UI_ADDR not in line, (line, DEF_ADDR))

    # ---------------------------------------------- PO detail
    go(f"{PO_ROUTE}/{PO_ID}", 3); wait_for(DETAIL_SHIP + " !== null", 25); drain(1)
    blk = js(DETAIL_SHIP)
    check(f"U9 PO detail 'Shipping to' shows '{SHIP_NAME}'", blk == SHIP_NAME, blk)
    # The page content only: the header's location switcher is the app's location context, not the PO.
    content = js("(document.querySelector('.form-stack') || document.body).innerText") or ""
    check(f"U10 '{LOC_NAME}' not shown in the PO detail content (header location switcher excluded)", content and LOC_NAME not in content, content[:300])
    viewport(390); drain(1)
    check("U11 390 px: no page-wide horizontal scroll on the PO detail page", no_hscroll())
    shot("lud_detail_390"); viewport(1366)
    shot("lud_detail_1366")

    put_setting(SHIP, "")
    time.sleep(0.5)
    go(f"{PO_ROUTE}/{PO_ID}", 3); wait_for(DETAIL_SHIP + " !== null", 25); drain(1)
    blk = js(DETAIL_SHIP)
    check(f"U12 blank ship-to name: detail shows the location name '{LOC_NAME}' again", blk == LOC_NAME, blk)
    put_setting(SHIP, SHIP_NAME)

    # ---------------------------------------------- the location keeps its name elsewhere
    go("/admin/locations", 3); wait_for(f"document.body.innerText.includes({json.dumps(LOC_NAME)})", 20)
    check(f"U13 Locations admin page still shows '{LOC_NAME}'", LOC_NAME in text() and SHIP_NAME not in text())
    go("/admin/bookings", 3); wait_for(f"document.body.innerText.includes({json.dumps(LOC_NAME)})", 25)
    check(f"U14 Bookings page still shows '{LOC_NAME}'", LOC_NAME in text(), text()[:300])
    shot("lud_bookings")
finally:
    try:
        if ws: ws.close()
    except Exception: pass
    if proc: proc.kill()
    for pid in CREATED:
        api("POST", f"/purchase-orders/{pid}/cancel", {"reason": "po-last-used-defaults ui clean-up"})
    if CREATED:
        sql(f"UPDATE TRN_PurchaseOrder SET IsDeleted = 1, IsActive = 0, DeletedDate = SYSUTCDATETIME() WHERE PurchaseOrderId IN ({','.join(map(str, CREATED))})")
    for k, f in FOUND.items():
        if f is None:
            if snap(k) is not None:
                put_setting(k, "0" if k == LOCK else "")  # evict the read cache before the row goes
            sql(f"DELETE FROM MST_AppSetting WHERE SettingKey = '{k}'")
            continue
        put_setting(k, bytes.fromhex(f[1] or "").decode("utf-16-le"))
        md = "NULL" if f[2] == "NULL" else f"'{f[2]}'"
        sql(f"UPDATE MST_AppSetting SET SettingValue = CAST(0x{f[1]} AS nvarchar(max)), ModifiedDate = {md}, ModifiedBy = {f[3]} WHERE SettingKey = '{k}'")
    check("Z1 all four settings back exactly as found", all(snap(k) == FOUND[k] for k in KEYS))
    check("Z2 test PO cancelled and soft-deleted", not CREATED or one(f"SELECT COUNT(*) FROM TRN_PurchaseOrder WHERE PurchaseOrderId IN ({','.join(map(str, CREATED))}) AND (IsDeleted = 0 OR Status <> 5)") == "0")

print(f"\n{sum(results)}/{len(results)} passed")
sys.exit(0 if all(results) else 1)
