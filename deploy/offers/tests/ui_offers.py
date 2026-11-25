"""Headless Chrome checks for the offers kit, desktop (1366 px) and phone (390 px).
LOCAL stack only: http://localhost:8080 -> API :81 (local-dev\\api with the kit), Dev DB with the
migration, mail to a local SMTP sink (purchases trigger the Wi-Fi kit's emails).

Admin: Sales > Offers - overview cards, validation messages, create an offer with a picture,
deactivate / activate, the Uses dialog, phone layout.
Customer: offer cards on My Bookings / Passes / Membership; the picker at "Review & pay" for an
hourly desk booking, a day pass and a membership (typed code); what is charged is checked in SQL.
Removes what it created (offers "OFR-UI ...", people ofr-ui-*@example.test).
Env: PYLIB (websocket-client), SHOTS_DIR, REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD."""
import base64, datetime as dt, json, os, random, subprocess, sys, time, urllib.request, urllib.error
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
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
today = dt.datetime.now(IST).date()


def open_day(after):
    d = today + dt.timedelta(days=after)
    while d.weekday() == 6:
        d += dt.timedelta(days=1)
    return d


DAY = open_day(12)


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
    sql("""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE Email LIKE 'ofr-ui-%@example.test';
        DECLARE @o TABLE (id int); INSERT INTO @o SELECT OfferId FROM MST_Offer WHERE OfferName LIKE 'OFR-UI %';
        DELETE FROM TRN_OfferRedemption WHERE OfferId IN (SELECT id FROM @o) OR CustomerId IN (SELECT id FROM @u);
        DELETE FROM TRN_OfferImage WHERE OfferId IN (SELECT id FROM @o);
        DELETE FROM MST_OfferScope WHERE OfferId IN (SELECT id FROM @o);
        DELETE FROM MST_OfferModule WHERE OfferId IN (SELECT id FROM @o);
        DELETE FROM MST_Offer WHERE OfferId IN (SELECT id FROM @o);
        IF OBJECT_ID('dbo.TRN_WifiAccess','U') IS NOT NULL
        BEGIN
            DECLARE @a TABLE (id bigint); INSERT INTO @a SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId IN (SELECT id FROM @u);
            DELETE k FROM TRN_WifiSimKey k JOIN TRN_WifiAccess a ON a.ControllerRef = k.ControllerRef WHERE a.AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiSession WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiDevice WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiEvent WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiAccessSource WHERE AccessId IN (SELECT id FROM @a)
                OR (SourceType = 1 AND RefId IN (SELECT BookingId FROM TRN_Booking WHERE CustomerId IN (SELECT id FROM @u)))
                OR (SourceType = 2 AND RefId IN (SELECT CustomerPassId FROM TRN_CustomerPass WHERE CustomerId IN (SELECT id FROM @u)));
            DELETE FROM TRN_WifiAccess WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_AdminNotification WHERE ReferenceType = 'WifiAccess' AND ReferenceId IN (SELECT id FROM @a);
        END
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'offers ui cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE TRN_CustomerPass SET Status = 4 WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 5);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE Email LIKE 'ofr-ui-%@example.test' AND IsDeleted = 0;""")


# ---- fixtures through the API ------------------------------------------------------------------------
purge()
admin_t = api_login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")
cust_email, cust_pw = f"ofr-ui-c{RUN}@example.test", "Offers#Ui" + RUN + "x"
s, b = api("POST", "/users", admin_t, {"fullName": "Offers UI Kavya", "email": cust_email, "mobileNumber": f"+9192{RUN}123", "password": cust_pw, "userType": 1,
                                       "roleIds": [int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))], "defaultLocationId": 2})
cust_id = b["data"]["userId"]
sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka', Country='India', PostalCode='560098', Gender=2 WHERE UserId={cust_id}")
api("POST", f"/wallet/{cust_id}/adjust", admin_t, {"direction": "credit", "amount": 100000, "reason": "offers ui"})
HIDDEN = f"UIMEM{RUN}"
s, b = api("POST", "/admin/offers", admin_t, {"offerName": f"OFR-UI {RUN} hidden", "title": "Members: Rs 300 off", "code": HIDDEN, "showInPortal": False, "discountType": 2,
                                              "discountValue": 300, "modules": [3], "startDate": today.isoformat(), "endDate": (today + dt.timedelta(days=20)).isoformat(),
                                              "isActive": True, "canCombine": False, "sortOrder": 5})
HIDDEN_ID = b["data"]["offerId"]
PNG_PATH = os.path.join(SHOTS, "offer-banner.png")
def make_png(w, h):
    """A valid w x h PNG: a dark-to-gold gradient (no image library needed)."""
    import struct, zlib
    rows = b"".join(bytes([0]) + b"".join(bytes((40 + 160 * x // w, 31 + 130 * x // w, 25 + 40 * x // w)) for x in range(w)) for _ in range(h))
    chunk = lambda k, d: struct.pack(">I", len(d)) + k + d + struct.pack(">I", zlib.crc32(k + d) & 0xFFFFFFFF)
    return bytes.fromhex("89504E470D0A1A0A") + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(rows)) + chunk(b"IEND", b"")
open(PNG_PATH, "wb").write(make_png(480, 180))

# ---- browser -------------------------------------------------------------------------------------
proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9375", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-offers-prof",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9375/json").read()) if t["type"] == "page"); break
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
def wait_for(cond, s=12):
    end = time.time() + s
    while time.time() < end:
        if js(cond): return True
        drain(0.4)
    return bool(js(cond))
SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:el.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype;"
       "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event(el.tagName==='SELECT'?'change':'input',{bubbles:true}))};"
       "const byLabel=(t,root)=>{const l=[...(root||document).querySelectorAll('label')].find(l=>l.textContent.trim().replace(/\\s*\\*$/,'').startsWith(t));if(!l)return null;return l.htmlFor?document.getElementById(l.htmlFor):l.querySelector('input,select,textarea')};")
def fill(label, value, root="document.querySelector('.modal')||document"):
    return js(f"(() => {{{SET} const el=byLabel({json.dumps(label)},{root}); if(!el) return false; setv(el,{json.dumps(value)}); return true }})()")
def tick(label, root="document.querySelector('.modal')"):
    return js(f"(() => {{ const l=[...{root}.querySelectorAll('label')].find(l=>l.textContent.trim().startsWith({json.dumps(label)})); if(!l) return false; const i=l.querySelector('input')||document.getElementById(l.htmlFor); i.click(); return true }})()")
def viewport(w):
    send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)

send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("DOM.enable"); send("Network.setBypassServiceWorker", bypass=True)
send("Network.setUserAgentOverride", userAgent=UA)
viewport(1366)

# ---- admin -----------------------------------------------------------------------------------------
go("/admin/login", 3)
js("localStorage.clear(); sessionStorage.clear(); true"); go("/admin/login", 3)
js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(os.environ['REGALIS_ADMIN_EMAIL'])});setv(document.querySelector('input[type=password]'),{json.dumps(os.environ['REGALIS_ADMIN_PASSWORD'])});document.querySelector('form button[type=submit]').click();return true}})()")
drain(5)
n1 = len(events)
go("/admin/offers", 5)
t = text()
check("sidebar: Offers under Sales", js("!!document.querySelector('a[href=\"/admin/offers\"]')") and "sales" in t.lower(), js("[...document.querySelectorAll('nav *, aside *')].filter(e=>e.children.length<3 && e.textContent.trim()==='Offers').map(e=>e.outerHTML.slice(0,300)).join(' || ')"))
check("overview cards: Total, Active, Inactive, Expired", all(x in t for x in ["Total offers", "Active", "Inactive", "Expired"]), t[:500])
check("filters: search, status, module, sort", all(x in t for x in ["Search", "Status", "Module", "Sort by"]))
click("button", "New offer"); drain(2)
check("editor opens with every section", all(x in text() for x in ["What customers see", "Discount", "Where it applies", "Validity and status", "Code, display and limits", "Terms & conditions"]), text()[:400])
click(".modal button", "Create offer"); drain(2)
t = text()
check("empty form: field messages shown", "Give the offer a name." in t and "Choose where the offer applies" in t, t[-800:])
NAME = f"OFR-UI {RUN} ten"
fill("Offer name", NAME); fill("Offer title", "25% off this week")
fill("Description", "Save on desks, passes and membership.")
fill("Discount %", "25"); fill("End date", (today + dt.timedelta(days=14)).isoformat())
fill("Offer code", f"UITEN{RUN}"); fill("Terms shown to customers", "Valid on online bookings only.")
for lab in ["My Bookings", "Passes", "Membership"]:
    tick(lab)
drain(0.5)
check("module ticked -> 'Every … (tick some to narrow it down)' with plan list", "tick some to narrow it down" in text() and "Single Day" in text(), text()[-900:])
doc = send("DOM.getDocument", depth=-1)
node = send("DOM.querySelector", nodeId=doc["root"]["nodeId"], selector=".modal input[type=file]")
send("DOM.setFileInputFiles", nodeId=node["nodeId"], files=[PNG_PATH])
drain(0.5)
shot("admin_offer_editor")
click(".modal button", "Create offer"); drain(4)
OID = one(f"SELECT OfferId FROM MST_Offer WHERE OfferName='{NAME}' AND IsDeleted=0")
t = text()
check("offer created and listed as Active", OID is not None and NAME in t and js("[...document.querySelectorAll('tr')].some(r=>r.textContent.includes(%s) && r.textContent.includes('Active'))" % json.dumps(NAME)), t[:600])
check("picture stored with the offer", one(f"SELECT HasImage FROM MST_Offer WHERE OfferId={OID}") == "1")
check("row: 10% OFF, max, three modules", js("[...document.querySelectorAll('tr')].some(r=>r.textContent.includes(%s) && r.textContent.includes('25%% OFF') && r.textContent.includes('My Bookings') && r.textContent.includes('Membership'))" % json.dumps(NAME)))
shot("admin_offers_desktop")
js("(() => { const tr=[...document.querySelectorAll('tbody tr')].find(t=>t.textContent.includes(%s)); [...tr.querySelectorAll('button')].find(b=>b.textContent.trim()==='Deactivate').click(); return true })()" % json.dumps(NAME)); drain(3)
check("deactivate -> Inactive", one(f"SELECT IsActive FROM MST_Offer WHERE OfferId={OID}") == "0" and js("[...document.querySelectorAll('tr')].some(r=>r.textContent.includes(%s) && r.textContent.includes('Inactive'))" % json.dumps(NAME)))
js("(() => { const tr=[...document.querySelectorAll('tbody tr')].find(t=>t.textContent.includes(%s)); [...tr.querySelectorAll('button')].find(b=>b.textContent.trim()==='Activate').click(); return true })()" % json.dumps(NAME)); drain(3)
check("activate -> Active again", one(f"SELECT IsActive FROM MST_Offer WHERE OfferId={OID}") == "1")
js("(() => { const tr=[...document.querySelectorAll('tbody tr')].find(t=>t.textContent.includes(%s)); [...tr.querySelectorAll('button')].find(b=>b.textContent.trim()==='Uses').click(); return true })()" % json.dumps(NAME)); drain(2)
check("Uses dialog: not used yet", "Not used yet" in text())
click(".modal button", "Close"); drain(1)
click(".oa-card", "Inactive"); drain(2)
check("clicking the Inactive card filters the list", NAME not in text())
click(".oa-card", "Total offers"); drain(2)
viewport(390); go("/admin/offers", 4)
check("phone: admin Offers fits (cards instead of a wide table)", no_hscroll())
shot("admin_offers_phone")
viewport(1366)
check("no JS errors on admin Offers", not exceptions(n1), exceptions(n1))

# ---- customer -----------------------------------------------------------------------------------------
js("localStorage.clear(); sessionStorage.clear(); true")
go("/login", 3)
js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(cust_email)});setv(document.querySelector('input[type=password]'),{json.dumps(cust_pw)});document.querySelector('form button[type=submit]').click();return true}})()")
drain(5)
n2 = len(events)
go("/bookings", 5)
check("My Bookings: no offer box (offers apply at checkout instead)", not js("!!document.querySelector('[data-rg=offer-strip]')") and "Booking offers" not in text(), text()[:400])
viewport(1366)

# hourly desk booking through the wizard
go("/book", 5)
ok = js("(() => { const c=[...document.querySelectorAll('.choice-card')].find(e=>e.querySelector('.choice-card__title')?.textContent.trim()==='Meeting Room'); if(!c) return false; c.click(); return true })()"); drain(1.5)
fill("From date", DAY.isoformat(), "document"); drain(0.5); fill("To date", DAY.isoformat(), "document"); drain(3)
wait_for("[...document.querySelectorAll('.slot-chip')].some(b=>!b.disabled)")
js("(() => { const b=[...document.querySelectorAll('.slot-chip')].filter(b=>!b.disabled); b[Math.min(4,b.length-1)].click(); return true })()"); drain(1)
click("button", "Find available workspaces"); drain(4)
picked = js("(() => { const c=[...document.querySelectorAll('.choice-card')].find(e=>/Meeting Room 01/.test(e.textContent) && !e.disabled); if(!c) return false; c.click(); return true })()"); drain(4)
check("booking wizard reaches Review & pay with Meeting Room 01 (1 hour)", ok and picked and wait_for("!!document.querySelector('[data-rg=offer-picker]')"), text()[:500])
drain(2)
check("best offer applied by itself and shown, with nothing to tap", js("!!document.querySelector('[data-applied][data-offer=\"%s\"]')" % OID) and "Offer applied" in text() and not js("!!document.querySelector('[data-rg=offer-picker] .of-opt__tick, [data-rg=offer-picker] button.of-opt')") and "Remove offer" not in text(), text()[-800:])
t = text()
check("offer applied: original, discount, GST, You pay (₹944.00 -> ₹708.00)", "You pay" in t and "₹708.00" in t and "₹944.00" in t and "− ₹200.00" in t, t[-1200:])
check("summary Total shows the old price struck through", js("!!document.querySelector('.summary-total .of-strike')"))
shot("book_pay_with_offer")
click("button", "Wallet"); drain(6)
bk = sql(f"SELECT TOP 1 BookingId, BaseAmount, DiscountAmount, TotalAmount, Status FROM TRN_Booking WHERE CustomerId={cust_id} ORDER BY BookingId DESC")
check("booking paid from the wallet at the discounted ₹708.00", bk and bk[0][2] == "200.00" and bk[0][3] == "708.00" and bk[0][4] == "2", bk)
check("redemption recorded for the booking", bk and one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE BookingId={bk[0][0]} AND OfferId={OID}") == "1")

# day pass
go("/passes", 5)
check("Passes: offer cards", "Pass offers" in text() and "25% off this week" in text())
click(".choice-card", "Single Day"); drain(1.5)
fill("First visit date", DAY.isoformat(), "document"); drain(4)
wait_for("!!document.querySelector('[data-rg=offer-picker]')")
drain(2)
t = text()
check("pass: offer applied by itself", js("!!document.querySelector('[data-applied][data-offer=\"%s\"]')" % OID))
check("pass: 800 -> 600 + GST = ₹708.00 (was ₹944.00)", "₹708.00" in t and "₹944.00" in t, t[-900:])
check("pass: Pay online / wallet amounts follow the offer", js("!document.querySelector('.pay-options') || ![...document.querySelectorAll('.pay-options button')].some(b=>b.textContent.includes('944'))"))
viewport(390); drain(1)
check("phone: pass picker fits", no_hscroll())
shot("pass_picker_phone")
viewport(1366); drain(1)
click(".pay-options button", "Wallet"); drain(6)
ps = sql(f"SELECT TOP 1 CustomerPassId, Price, TotalAmount FROM TRN_CustomerPass WHERE CustomerId={cust_id} ORDER BY CustomerPassId DESC")
check("day pass bought at ₹708.00 (price 600)", ps and ps[0][1] == "600.00" and ps[0][2] == "708.00", ps)

# membership with a typed code
go("/membership", 5)
check("Membership: offer cards (the 25% offer, not the hidden one)", "Membership offers" in text() and "25% off this week" in text() and "Members: Rs 300 off" not in text())
js("[...document.querySelectorAll('.choice-card')][0].click()"); drain(1.5)
js("[...document.querySelectorAll('.choice-card')][0].click()"); drain(1)
click("button", "Continue to payment"); drain(3)
wait_for("!!document.querySelector('[data-rg=offer-picker]')")
drain(2)
check("membership: the 25% offer applied by itself", js("!!document.querySelector('[data-applied][data-offer=\"%s\"]')" % OID))
# The coupons kit (25 Nov 2026) renames the link and field to "Have a coupon or offer code?" / "Coupon or offer code".
click("button", "offer code?"); drain(0.5)
fill("Offer code", HIDDEN.lower(), "document") or fill("Coupon or offer code", HIDDEN.lower(), "document"); drain(0.3)
click(".of-codeform button", "Apply"); drain(3)
t = text()
check("typed code applies the hidden ₹300 offer", "Members: Rs 300 off" in t and "You pay" in t and "− ₹300.00" in t, t[-1000:])
due = js("document.querySelector('.mem-pay__due')?.innerText || ''")
check("'Total payable' shows the discounted amount next to the struck old one", js("!!document.querySelector('.mem-pay__due .of-strike')"), due)
check("Order summary shows the offer row and the struck old total", "Offer discount (before GST)" in t and js("[...document.querySelectorAll('.summary-rows .of-strike')].length > 0"))
shot("membership_code_desktop")
plan_total = js("(() => { const s=document.querySelector('[data-rg=offer-summary] .of-final span:last-child'); return s ? s.innerText : '' })()")
click(".pay-options button", "Wallet"); drain(6)
ms = sql(f"SELECT TOP 1 p.CustomerPassId, p.TotalAmount, r.OfferId FROM TRN_CustomerPass p LEFT JOIN TRN_OfferRedemption r ON r.CustomerPassId=p.CustomerPassId WHERE p.CustomerId={cust_id} AND p.PassType=2 ORDER BY p.CustomerPassId DESC")
check("membership bought with the code offer", ms and ms[0][2] == str(HIDDEN_ID) and ("₹" + format(float(ms[0][1]), ",.2f")) in (plan_total or "").replace("\u00a0", " "), (ms, plan_total))
check("no JS errors in the customer flows", not exceptions(n2), exceptions(n2))

n3 = len(events)
for p, needle in [("/", "Book Workspace"), ("/wallet", "Wallet"), ("/passes/my", "My Passes"), ("/payments", "Payment")]:
    go(p, 4)
    check(f"existing customer page still works: {p}", needle.lower() in text().lower(), text()[:200])
check("no JS errors on existing customer pages", not exceptions(n3), exceptions(n3))

# ---- clean up -------------------------------------------------------------------------------------
ws.close(); proc.terminate()
purge()
print(f"\n{sum(results)}/{len(results)} passed")
sys.exit(0 if all(results) else 1)
