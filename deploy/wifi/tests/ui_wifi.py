"""Headless Chrome checks for the Wi-Fi kit, desktop (1366 px) and phone (390 px).
LOCAL stack only: http://localhost:8080 -> API :81 (local-dev\\api with the kit), Dev DB with the
migration, mail to a local SMTP sink. Sets up a customer with a paid monthly pass through the API, then
drives both portals: admin Connected now / Access issued / Usage log / Settings (guest access, team
member, revoke, simulated device) and the customer's My Wi-Fi (password, QR, devices, help ticket).
Removes what it created at the end (people named "WiFi UI ...", emails wifi-ui-*@example.test).
Env: PYLIB (websocket-client), SHOTS_DIR, REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD."""
import base64, json, os, random, subprocess, sys, time, urllib.request, urllib.error
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket
sys.stdout.reconfigure(encoding="utf-8")

SHOTS = os.environ.get("SHOTS_DIR", "."); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
API = "http://127.0.0.1:81/api"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36"


def sql(q):
    out = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:150] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q); return r[0][0] if r else None


def api(method, path, token=None, body=None):
    for _ in range(6):
        req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
        req.add_header("Content-Type", "application/json"); req.add_header("User-Agent", UA)
        if token: req.add_header("Authorization", "Bearer " + token)
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.status, json.loads(r.read() or b"{}")
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(15); continue
            return e.code, json.loads(e.read() or b"{}")
    raise SystemExit("rate limited")


def api_login(email, pw, portal):
    s, b = api("POST", "/auth/login", body={"email": email, "password": pw, "portal": portal})
    if s != 200: raise SystemExit(f"login {email}: {s} {b}")
    return b["data"]["accessToken"]


def purge():
    sql("""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE Email LIKE 'wifi-ui-%@example.test';
        DECLARE @a TABLE (id bigint); INSERT INTO @a SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId IN (SELECT id FROM @u) OR HolderName LIKE 'WiFi UI%';
        DELETE k FROM TRN_WifiSimKey k JOIN TRN_WifiAccess a ON a.ControllerRef = k.ControllerRef WHERE a.AccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_WifiSimClient WHERE MacAddress LIKE '02:0E:%' OR DeviceName LIKE 'UI %';
        DELETE FROM TRN_WifiSession WHERE AccessId IN (SELECT id FROM @a) OR MacAddress LIKE '02:0E:%' OR DeviceName LIKE 'UI %';
        DELETE FROM TRN_WifiDevice WHERE AccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_WifiEvent WHERE AccessId IN (SELECT id FROM @a) OR Detail LIKE '%02:0E:%' OR Detail LIKE '%UI %';
        DELETE FROM TRN_WifiAccessSource WHERE AccessId IN (SELECT id FROM @a) OR (SourceType = 2 AND RefId IN (SELECT CustomerPassId FROM TRN_CustomerPass WHERE CustomerId IN (SELECT id FROM @u)));
        DELETE FROM TRN_WifiAccess WHERE ParentAccessId IN (SELECT id FROM @a); DELETE FROM TRN_WifiAccess WHERE AccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_AdminNotification WHERE ReferenceType = 'WifiAccess' AND (ReferenceId IN (SELECT id FROM @a) OR ReferenceLabel LIKE '02:0E:%');
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE Email LIKE 'wifi-ui-%@example.test' AND IsDeleted = 0;""")


# ---- fixtures through the API ------------------------------------------------------------------------
purge()
admin_t = api_login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")
cust_email, cust_pw = f"wifi-ui-c{RUN}@example.test", "WiFi#Ui" + RUN + "x"
s, b = api("POST", "/users", admin_t, {"fullName": "WiFi UI Meera Iyer", "email": cust_email, "mobileNumber": f"+9191{RUN}123", "password": cust_pw, "userType": 1,
                                       "roleIds": [int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))], "companyName": "UI Labs", "defaultLocationId": 2})
cust_id = b["data"]["userId"]
sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka', Country='India', PostalCode='560098', Gender=2 WHERE UserId={cust_id}")
cust_t = api_login(cust_email, cust_pw, "customer")
s, b = api("POST", "/passes/order", cust_t, {"passPlanId": 5}); order = b["data"]["gatewayOrderId"]
s, b = api("POST", "/payments/sandbox-complete", cust_t, {"gatewayOrderId": order}); pay = b["data"]
today = time.strftime("%Y-%m-%d", time.gmtime(time.time() + 19800))
s, b = api("POST", "/passes/purchase", cust_t, {"passPlanId": 5, "validFrom": today, "paymentMethod": "gateway", "gatewayOrderId": order, "gatewayPaymentId": pay["gatewayPaymentId"], "gatewaySignature": pay["gatewaySignature"]})
acc = None
for _ in range(30):
    acc = one(f"SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId={cust_id} AND Status=2")
    if acc: break
    time.sleep(3)
if not acc: raise SystemExit("no Wi-Fi access was issued for the UI customer")
password = api("GET", "/portal/wifi", cust_t)[1]["data"]["current"][0]["password"]
mac1 = f"02:0E:{RUN[:2]}:{RUN[2:4]}:01:0{RUN[4]}"
api("POST", "/admin/wifi/simulator/join", admin_t, {"accessId": int(acc), "how": "right", "deviceName": "UI MacBook Air", "mac": mac1, "accessPoint": "Floor 2 · AP-04"})
api("POST", "/admin/wifi/simulator/join", admin_t, {"accessId": int(acc), "how": "right", "deviceName": "UI iPhone", "mac": f"02:0E:{RUN[:2]}:{RUN[2:4]}:02:0{RUN[4]}"})
api("POST", "/admin/wifi/simulator/join", admin_t, {"how": "shared", "deviceName": "UI Stranger", "mac": f"02:0E:{RUN[:2]}:{RUN[2:4]}:09:0{RUN[4]}", "ssid": "Regalis-Guest"})

# ---- browser -------------------------------------------------------------------------------------
proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9374", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-wifi-prof",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9374/json").read()) if t["type"] == "page"); break
    except Exception: time.sleep(0.3)
ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60); mid, events, results = 0, [], []
def send(m, **p):
    global mid; mid += 1; ws.send(json.dumps({"id": mid, "method": m, "params": p}))
    while True:
        r = json.loads(ws.recv())
        if r.get("id") == mid: return r.get("result", {})
        events.append(r)
def drain(s):
    end = time.time() + s; ws.settimeout(0.3)
    while time.time() < end:
        try: events.append(json.loads(ws.recv()))
        except Exception: pass
    ws.settimeout(60)
def js(x): return send("Runtime.evaluate", expression=x, awaitPromise=True, returnByValue=True).get("result", {}).get("value")
def go(p, w=4): send("Page.navigate", url=BASE + p); drain(w)
def shot(n): open(os.path.join(SHOTS, n + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png")["data"]))
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:400]), flush=True)
def click(sel, txt, root="document"):
    return js(f"(() => {{ const el=[...{root}.querySelectorAll({json.dumps(sel)})].find(e=>e.textContent.trim().includes({json.dumps(txt)}) && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
def text(): return js("document.body.innerText") or ""
def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
def exceptions(n): return [e["params"]["exceptionDetails"].get("exception", {}).get("description", "")[:200] for e in events[n:] if e.get("method") == "Runtime.exceptionThrown"]
SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:el.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype;"
       "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event(el.tagName==='SELECT'?'change':'input',{bubbles:true}))};"
       "const byLabel=(t,root)=>{const l=[...(root||document).querySelectorAll('label')].find(l=>l.textContent.trim().replace(/\\s*\\*$/,'').startsWith(t));if(!l)return null;return l.htmlFor?document.getElementById(l.htmlFor):l.querySelector('input,select,textarea')};")
def fill(label, value, root="document.querySelector('.modal')||document"):
    return js(f"(() => {{{SET} const el=byLabel({json.dumps(label)},{root}); if(!el) return false; setv(el,{json.dumps(value)}); return true }})()")
def viewport(w):
    send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)
def row_menu(name, item):
    """Open the ⋯ menu of the access row whose text contains name, and pick item."""
    ok = js(f"(() => {{ const tr=[...document.querySelectorAll('tbody tr')].find(t=>t.textContent.includes({json.dumps(name)})); if(!tr) return false;"
            f" tr.querySelector('button[aria-label=\"More actions\"]').click(); return true }})()")
    drain(0.5)
    return ok and click(".wf-menu__list button", item)

send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
send("Network.setUserAgentOverride", userAgent=UA)
viewport(1366)

# ---- admin -----------------------------------------------------------------------------------------
go("/admin/login", 3)
js("localStorage.clear(); sessionStorage.clear(); true"); go("/admin/login", 3)
js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(os.environ['REGALIS_ADMIN_EMAIL'])});setv(document.querySelector('input[type=password]'),{json.dumps(os.environ['REGALIS_ADMIN_PASSWORD'])});document.querySelector('form button[type=submit]').click();return true}})()")
drain(5)
n0 = len(events)
go("/admin/wifi", 5)
t = text()
check("sidebar: Wi-Fi under Device / Access Control", js("!!document.querySelector('a[href=\"/admin/wifi\"]')") and "Device / Access Control" in t, t[:300])
check("Wi-Fi page: four tabs", all(x in t for x in ["Connected now", "Access issued", "Usage log", "Settings"]), t[:300])
check("five summary cards", all(x in t for x in ["Online now", "Customers today", "Guests online", "Data today", "Failed logins"]))
check("the customer is named on her device", "WiFi UI Meera Iyer" in t and "UI MacBook Air" in t and mac1 in t, t[:600])
check("second device marked on the same access", "Another device on the same access" in t)
check("unrecognised device in amber with Block", "Unrecognised device" in t and "No customer matched" in t and js("[...document.querySelectorAll('.wf-row--unknown button')].some(b=>b.textContent.trim()==='Block')"))
check("footer: live from the controller, refreshed …", "Live from the Wi-Fi controller" in t and "refreshed" in t)
check("simulated controller is said plainly", "Simulated Wi-Fi controller" in t)
shot("admin_connected_desktop")
click("button", "Block"); drain(1); fill("Why", "UI test"); click(".modal button", "Block device"); drain(3)
check("block an unrecognised device", "UI Stranger" not in text() or "Unrecognised device" not in text(), text()[:300])

click("button.wf-tab", "Access issued"); drain(3)
t = text()
check("Access issued: chips with counts", all(x in t for x in ["All", "Active", "Expiring today", "Expired", "Revoked", "Not delivered"]))
check("row: username, network, 'password hidden', devices, Active", "Regalis-Members · password hidden" in t and "2 of 3 used" in t and "Active" in t, t[:800])
check("the password is never on the admin screen", password not in t and password not in (js("document.documentElement.outerHTML") or ""))
shot("admin_access_desktop")
row_menu("WiFi UI Meera", "See devices and history"); drain(2)
t = text()
check("detail: devices, paid-for-by and the access log", "UI MacBook Air" in t and "Paid for by" in t and "Access log" in t and "Issued on payment" in t, t[:500])
check("detail never shows the password", password not in t)
click(".modal button", "+ Add a team member"); drain(0.5)
fill("Name", f"WiFi UI Rahul {RUN}"); fill("Email", f"wifi-ui-m{RUN}@example.test")
click(".modal button", "Add and send their details"); drain(3)
check("team member added from the detail", f"WiFi UI Rahul {RUN}" in text() and "ui.wifi" in text(), text()[-600:])
click(".modal button", "Close"); drain(2)
check("company row now shows Members", js("[...document.querySelectorAll('tbody tr')].some(t=>t.textContent.includes('UI Labs') && [...t.querySelectorAll('button')].some(b=>b.textContent.trim()==='Members'))"))

click("button", "Issue guest access"); drain(1)
fill("Guest name", "WiFi UI Guest"); fill("Email", f"wifi-ui-g{RUN}@example.test"); fill("Mobile", "98450 33221"); fill("Note", "Interview")
click(".modal button", "Issue access"); drain(4)
t = text()
check("guest access issued from the dialog", "WiFi UI Guest" in t and "guest-" in t, t[:600])
row_menu("WiFi UI Guest", "Revoke now"); drain(1)
fill("Reason", "Interview over"); click(".modal button", "Revoke now"); drain(3)
check("revoke from the ⋯ menu", js("[...document.querySelectorAll('tbody tr')].some(t=>t.textContent.includes('WiFi UI Guest') && t.textContent.includes('Revoked'))"), text()[:500])

click("button.wf-tab", "Usage log"); drain(3)
check("Usage log lists connections with the customer", "WiFi UI Meera Iyer" in text() and "started" in text().lower(), text()[:400])

click("button.wf-tab", "Settings"); drain(3)
t = text()
check("Settings: controller, networks, how access is created, delivery, rules, privacy", all(x in t for x in [
    "Wi-Fi controller", "Test connection", "Networks", "Regalis-Members", "How access is created", "One password per customer",
    "Login page with username and password", "One shared password for everyone", "Delivery to the customer", "Rules", "What is kept, and for how long"]), t[:500])
click("button", "Test connection"); drain(2)
check("test connection answers", "Simulated controller" in text())
click("button", "Save settings"); drain(2)
check("save settings", "Wi-Fi settings saved" in text() or js("!!document.querySelector('.toast')"))
js("(() => {" + SET + f"const s=[...document.querySelectorAll('select')].find(x=>[...x.options].some(o=>o.text.includes('WiFi UI Meera'))); const o=[...s.options].find(o=>o.text.includes('WiFi UI Meera')); setv(s,o.value); return true}})()")
fill("Device name", "UI Pixel", "document"); drain(0.5)
click("button", "Join the Wi-Fi"); drain(4)
check("simulate a device joining from Settings", "UI Pixel" in text() and "joined" in text(), text()[-400:])
shot("admin_settings_desktop")
check("no JS errors on the admin Wi-Fi screens", not exceptions(n0), exceptions(n0))

viewport(390)
go("/admin/wifi", 5)
check("phone: Connected now fits (no sideways scroll)", no_hscroll())
shot("admin_connected_phone")
go("/admin/wifi/access", 4)
check("phone: Access issued fits", no_hscroll())
viewport(1366)

n1 = len(events)
for p, needle in [("/admin", "Today's Bookings"), ("/admin/access/logs", "Access"), ("/admin/customers", "Customers"), ("/admin/crm/leads", "Leads")]:
    go(p, 4)
    check(f"existing page still works: {p}", needle in text(), text()[:200])
check("no JS errors on existing admin pages", not exceptions(n1), exceptions(n1))

# ---- customer -----------------------------------------------------------------------------------------
js("localStorage.clear(); sessionStorage.clear(); true")
go("/login", 3)
js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(cust_email)});setv(document.querySelector('input[type=password]'),{json.dumps(cust_pw)});document.querySelector('form button[type=submit]').click();return true}})()")
drain(5)
n2 = len(events)
go("/portal/wifi", 5)
t = text()
check("/portal/wifi opens My Wi-Fi at /wifi", js("location.pathname") == "/wifi" and "My Wi-Fi" in t, js("location.pathname"))
check("customer sidebar: My Wi-Fi", js("!!document.querySelector('a[href=\"/wifi\"]')"))
check("card: membership, Active, network and her password", "membership" in t and "Regalis-Members" in t and password in t, t[:500])
check("QR code drawn", js("!!document.querySelector('.wf-qr svg path')"))
check("valid until, devices used, your devices, earlier and house rules", all(x in t for x in ["Valid until", "used", "Your devices", "UI MacBook Air", "House rules", "never records which websites"]), t[:800])
check("buttons: email again, WhatsApp, trouble connecting", all(x in t for x in ["Email this to me again", "Send on WhatsApp", "Having trouble connecting?"]))
shot("my_wifi_desktop")
click("button", "Hide"); drain(0.5)
check("Hide hides the password", password not in text() and "••••" in text())
click("button", "Show"); drain(0.5)
click("button", "Email this to me again"); drain(3)
check("Email this to me again", f"Sent to {cust_email}" in text(), text()[-300:])
click("button", "Having trouble connecting?"); drain(1)
fill("What happens?", "UI test: laptop will not join")
click(".modal button", "Raise a support ticket"); drain(3)
check("help raises a support ticket", "Support ticket" in text() and one(f"SELECT COUNT(*) FROM TRN_Ticket WHERE CustomerId={cust_id} AND Subject LIKE 'Wi-Fi:%'") == "1", text()[-300:])
js("(() => { const d=[...document.querySelectorAll('.wf-device')].find(x=>x.textContent.includes('UI iPhone')); d.querySelector('button').click(); return true })()"); drain(1)
click(".modal button", "Remove device"); drain(3)
check("customer removes a device", "UI iPhone" not in text() and "slot" in text(), text()[:500])
check("no JS errors on My Wi-Fi", not exceptions(n2), exceptions(n2))
viewport(390)
go("/wifi", 5)
check("phone: My Wi-Fi fits, password readable", no_hscroll() and password in text())
shot("my_wifi_phone")
viewport(1366)
n3 = len(events)
for p, needle in [("/bookings", "Bookings"), ("/payments", "Payment"), ("/support", "Support")]:
    go(p, 4)
    check(f"existing customer page still works: {p}", needle in text(), text()[:200])
check("no JS errors on existing customer pages", not exceptions(n3), exceptions(n3))

# ---- clean up -------------------------------------------------------------------------------------
ws.close(); proc.terminate()
purge()
print(f"\n{sum(results)}/{len(results)} passed")
sys.exit(0 if all(results) else 1)
