"""Headless Chrome checks for the coupons kit, desktop (1366 px) and phone (390 px).
LOCAL stack only: http://localhost:8080 -> API :81 (local-dev\\api with the kit), Dev DB with the
migration 20261125120000_Coupons, mail to a local SMTP sink on 127.0.0.1:2525 (checked before any
purchase; the script stops if nothing listens there).

Admin: Sales > Offers - the "Offers | Coupon codes" tabs and ?tab=coupons, overview cards, search,
status card filter, sort, the New coupon editor and its browser-side messages, a duplicate code
refused by the server, deactivate / activate, delete of an unused coupon, the Uses dialog, phone
layout; the Offers tab still works; a Sales user (OFFER.VIEW only) sees no change buttons. After the
customer part: Uses lists the purchase, a used coupon cannot be deleted or have its code changed.
Customer: coupon cards on Book Pass and Membership; at a day-pass checkout an unknown, an expired,
an inactive, a not-yet-started, a wrong-service and a below-minimum code are refused with the reason;
a valid code is priced (checked against our own arithmetic) and paid from the wallet (redemption and
invoice checked in SQL); the same code again is "already used"; on a 10-Day Pack a coupon replaces a
test offer and "Remove coupon" brings it back; membership chip; a meeting-room booking with a chip
paid from the wallet (booking, redemption and invoice checked in SQL).

Creates: coupons CPUI<run>..., one offer "CPUI-OFR <run>" (10-Day Pack only, deleted straight after
use), a customer cpui-c<run>@example.test and a Sales user cpui-s<run>@example.test.
Removes at the end (also after a failure): the coupons with their modules and redemptions, the
offer, the Wi-Fi rows of the test users; bookings are cancelled (Status 5), passes cancelled
(Status 4), the users soft-deleted. Their invoices stay (the app cannot delete invoices).
Env: PYLIB (websocket-client), SHOTS_DIR, REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD."""
import base64, datetime as dt, json, os, random, socket, subprocess, sys, time, traceback, urllib.request, urllib.error
from decimal import Decimal as D, ROUND_HALF_UP
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
GST = D("9")


def open_day(after):
    d = today + dt.timedelta(days=after)
    while d.weekday() == 6:
        d += dt.timedelta(days=1)
    return d


DAY = open_day(13)


def r2(x):
    return D(x).quantize(D("0.01"), rounding=ROUND_HALF_UP)


def price(base, pct=None, fixed=None, cap=None):
    """Coupon arithmetic as the brief states it: discount off the price before GST (a percentage
    rounded to the paisa, capped at the maximum; a fixed amount never above the price), then CGST and
    SGST 9% each on what is left, each rounded half away from zero."""
    base = D(base)
    disc = r2(base * D(pct) / 100) if pct is not None else min(D(fixed), base)
    if cap is not None:
        disc = min(disc, D(cap))
    taxable = base - disc
    cg = r2(taxable * GST / 100); sg = r2(taxable * GST / 100)
    return {"disc": disc, "taxable": taxable, "cgst": cg, "sgst": sg, "total": taxable + cg + sg, "orig": base + r2(base * GST / 100) * 2}


def rs(x):
    """₹ amount as the portal prints it (en-IN grouping, 2 decimals)."""
    x = D(x); s = f"{x:.2f}"; whole, frac = s.split(".")
    if len(whole) > 3:
        head, tail = whole[:-3], whole[-3:]
        parts = []
        while len(head) > 2:
            parts.insert(0, head[-2:]); head = head[:-2]
        if head: parts.insert(0, head)
        whole = ",".join(parts + [tail])
    return "₹" + whole + "." + frac


def sql(q):
    out = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:150] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q); return r[0][0] if r else None


CREDS = {}


def api(method, path, token=None, body=None):
    for _ in range(8):
        req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
        req.add_header("Content-Type", "application/json"); req.add_header("User-Agent", UA)
        if token: req.add_header("Authorization", "Bearer " + CREDS.get(token, {}).get("tok", token))
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                return r.status, json.loads(r.read() or b"{}")
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(15); continue
            if e.code == 401 and token in CREDS:
                c = CREDS[token]; c["tok"] = raw_login(c["email"], c["pw"], c["portal"]); continue
            return e.code, json.loads(e.read() or b"{}")
    raise SystemExit("rate limited")


def raw_login(email, pw, portal):
    for _ in range(6):
        s, b = api("POST", "/auth/login", body={"email": email, "password": pw, "portal": portal})
        if s == 200: return b["data"]["accessToken"]
        if s == 429: time.sleep(15); continue
        raise SystemExit(f"login {email}: {s} {b}")
    raise SystemExit("login rate limited")


def session(name, email, pw, portal):
    CREDS[name] = {"email": email, "pw": pw, "portal": portal, "tok": raw_login(email, pw, portal)}
    return name


def purge():
    sql(f"""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE Email LIKE 'cpui-%@example.test';
        DECLARE @c TABLE (id int); INSERT INTO @c SELECT CouponId FROM MST_Coupon WHERE Code LIKE 'CPUI%';
        DECLARE @o TABLE (id int); INSERT INTO @o SELECT OfferId FROM MST_Offer WHERE OfferName LIKE 'CPUI-OFR %';
        DELETE FROM TRN_CouponRedemption WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM MST_CouponModule WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM MST_Coupon WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM TRN_OfferRedemption WHERE OfferId IN (SELECT id FROM @o);
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
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'coupons ui cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE TRN_CustomerPass SET Status = 4 WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 5);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE Email LIKE 'cpui-%@example.test' AND IsDeleted = 0;""")


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as s:
            return s.recv(100).startswith(b"220")
    except OSError:
        return False


results = []
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:500]), flush=True)


# ---- fixtures through the API ------------------------------------------------------------------------
if not sink_up():
    raise SystemExit("No SMTP sink answering on 127.0.0.1:2525 - not running (purchases would send mail).")
purge()
ADMIN = session("admin", os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")


def make_user(tag, name, utype, role_code, gender):
    pw = "Coupons#Ui" + RUN + "x"
    em = f"cpui-{tag}{RUN}@example.test"
    s, b = api("POST", "/users", ADMIN, {"fullName": name, "email": em, "mobileNumber": f"+9193{RUN}{random.randint(100, 999)}", "password": pw, "userType": utype,
                                         "roleIds": [int(one(f"SELECT RoleId FROM MST_Role WHERE RoleCode='{role_code}'"))], "defaultLocationId": 2})
    if s not in (200, 201): raise SystemExit(f"create user {tag}: {s} {b}")
    uid = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru',"
        f" State='Karnataka', Country='India', PostalCode='560098', Gender={gender} WHERE UserId={uid}")
    return uid, em, pw


def coupon(code, **kw):
    body = {"code": code, "description": kw.get("desc"), "discountType": kw.get("type", 1), "discountValue": kw.get("value", 10),
            "maxDiscountAmount": kw.get("cap"), "minAmount": kw.get("min"), "modules": kw.get("modules", [1, 2, 3]),
            "validFrom": kw.get("start", today).isoformat(), "validUntil": kw.get("end", today + dt.timedelta(days=20)).isoformat(),
            "totalUses": kw.get("total"), "usesPerCustomer": kw.get("per"), "showInPortal": kw.get("show", True), "isActive": kw.get("active", True)}
    s, b = api("POST", "/admin/coupons", ADMIN, body)
    if s not in (200, 201): raise SystemExit(f"create coupon {code}: {s} {b}")
    return b["data"]["couponId"]


proc = ws = None
try:
    cust_id, cust_email, cust_pw = make_user("c", "Coupons UI Anjali", 1, "CUSTOMER", 2)
    sales_id, sales_email, sales_pw = make_user("s", "Coupons UI Sales", 3, "SALES", 1)
    api("POST", f"/wallet/{cust_id}/adjust", ADMIN, {"direction": "credit", "amount": 100000, "reason": "coupons ui"})
    CUST = session("cust", cust_email, cust_pw, "customer")
    def wallet(): return D(str(api("GET", "/wallet/me", CUST)[1]["data"]["balance"]))

    OK = f"CPUIOK{RUN}"        # typed, hidden: 20% capped at ₹61.50, Passes only, 1 use per customer
    LIST = f"CPUILIST{RUN}"    # listed on the portal: 12.34%, every service, 1 use per customer
    EXP = f"CPUIEXP{RUN}"      # expired yesterday (dates moved back with SQL; the API refuses past dates)
    OFF = f"CPUIOFF{RUN}"      # inactive
    MIN = f"CPUIMIN{RUN}"      # minimum ₹1,000 before GST
    SOON = f"CPUISOON{RUN}"    # starts tomorrow
    BKONLY = f"CPUIBK{RUN}"    # My Bookings only
    DEL = f"CPUIDEL{RUN}"      # unused, deleted through the screen
    OK_ID = coupon(OK, value=20, cap=61.50, modules=[2], per=1, show=False, desc="UI test: typed")
    LIST_ID = coupon(LIST, value=12.34, modules=[1, 2, 3], per=1, desc="UI test: listed coupon")
    EXP_ID = coupon(EXP, value=10, modules=[2], show=True)
    sql(f"UPDATE MST_Coupon SET ValidFrom='{(today - dt.timedelta(days=10)).isoformat()}', ValidUntil='{(today - dt.timedelta(days=1)).isoformat()}' WHERE CouponId={EXP_ID}")
    OFF_ID = coupon(OFF, value=10, modules=[2], active=False, show=True)
    MIN_ID = coupon(MIN, type=2, value=100, min=1000, modules=[2], show=False)
    SOON_ID = coupon(SOON, value=10, modules=[2], start=today + dt.timedelta(days=1), show=True)
    BK_ID = coupon(BKONLY, value=10, modules=[1], show=False)
    DEL_ID = coupon(DEL, type=2, value=50, modules=[3], show=False)

    # ---- browser -------------------------------------------------------------------------------------
    proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9376", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-coupons-prof-{RUN}",
                             "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(60):
        try:
            page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9376/json").read()) if t["type"] == "page"); break
        except Exception: time.sleep(0.3)
    ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60); mid, events = 0, []
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
    def click(sel, txt, root="document"):
        return js(f"(() => {{ const el=[...{root}.querySelectorAll({json.dumps(sel)})].find(e=>e.textContent.trim().includes({json.dumps(txt)}) && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
    def text(): return (js("document.body.innerText") or "").replace("\u00a0", " ")
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
    def row_btn(code, label):
        return js("(() => { const tr=document.querySelector('tr[data-coupon=\"%s\"]'); if(!tr) return false; const b=[...tr.querySelectorAll('button')].find(b=>b.textContent.trim()===%s && !b.disabled); if(!b) return false; b.scrollIntoView({block:'center'}); b.click(); return true })()" % (code, json.dumps(label)))
    def row_text(code): return (js("document.querySelector('tr[data-coupon=\"%s\"]')?.innerText || ''" % code) or "").replace("\u00a0", " ")
    def picker(): return (js("document.querySelector('[data-rg=offer-picker]')?.innerText || ''") or "").replace("\u00a0", " ")
    def alert(): return (js("document.querySelector('[data-rg=offer-picker] .of-msg[role=alert]')?.innerText || ''") or "").replace("\u00a0", " ")
    def login(email, pw, path):
        js("localStorage.clear(); sessionStorage.clear(); true"); go(path, 3)
        js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(email)});setv(document.querySelector('input[type=password]'),{json.dumps(pw)});document.querySelector('form button[type=submit]').click();return true}})()")
        drain(5)
    def type_code(code):
        if not js("!!document.querySelector('.of-codeform')"):
            click("[data-rg=offer-picker] button", "Have a coupon or offer code?"); drain(0.5)
        fill("Coupon or offer code", code, "document"); drain(0.3)
        click(".of-codeform button", "Apply"); drain(3)
    def cards(): return [c.get("label", "") + "=" + str(c.get("value")) for c in (js("[...document.querySelectorAll('.oa-card')].map(c=>({label:c.querySelector('.oa-card__label')?.innerText, value:c.querySelector('.oa-card__value')?.innerText}))") or [])]

    send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("DOM.enable"); send("Network.setBypassServiceWorker", bypass=True)
    send("Network.setUserAgentOverride", userAgent=UA)
    viewport(1366)
    go("/admin/login", 3)

    # ---- admin: tabs ------------------------------------------------------------------------------------
    login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "/admin/login")
    n1 = len(events)
    go("/admin/offers", 5)
    check("admin Offers page opens on the Offers tab with both tabs", js("document.querySelector('[role=tab][data-tab=offers]')?.getAttribute('aria-selected')==='true' && !!document.querySelector('[role=tab][data-tab=coupons]')") and "Total offers" in text(), text()[:500])
    check("Offers tab unchanged: overview cards Total, Active, Inactive, Expired; filters", all(x in text() for x in ["Total offers", "Active", "Inactive", "Expired", "Search", "Status", "Module", "Sort by"]), text()[:600])
    click("button", "New offer"); drain(2)
    check("Offers tab: New offer still opens the offer editor", all(x in text() for x in ["What customers see", "Discount", "Where it applies"]), text()[:400])
    click(".modal button", "Cancel"); drain(1)
    js("document.querySelector('[role=tab][data-tab=coupons]').click()"); drain(3)
    check("clicking 'Coupon codes' switches tab and sets ?tab=coupons", "tab=coupons" in (js("location.search") or "") and "Total coupons" in text() and "New coupon" in text(), (js("location.search"), text()[:300]))
    js("document.querySelector('[role=tab][data-tab=offers]').click()"); drain(2)
    check("clicking 'Offers' goes back and drops ?tab", "tab=" not in (js("location.search") or "") and "Total offers" in text())
    go("/admin/offers?tab=coupons", 5)
    t = text()
    check("?tab=coupons opens the Coupon codes tab directly", js("document.querySelector('[role=tab][data-tab=coupons]')?.getAttribute('aria-selected')==='true'") and "Total coupons" in t, t[:400])
    check("coupon overview cards: Total, Active, Inactive, Expired, Scheduled", all(x in t for x in ["Total coupons", "Active", "Inactive", "Expired", "Scheduled"]), cards())
    cv = {c.split("=")[0]: c.split("=")[1] for c in cards()}
    try:
        summ = int(cv["Active"]) + int(cv["Inactive"]) + int(cv["Expired"]) + int(cv["Scheduled"])
        check("overview: Active + Inactive + Expired + Scheduled = Total", summ == int(cv["Total coupons"]), cv)
    except Exception as e:
        check("overview: card values are numbers", False, (cv, e))
    check("list shows the seeded coupons with their status", "Expired" in row_text(EXP) and "Inactive" in row_text(OFF) and "Scheduled" in row_text(SOON) and "Active" in row_text(LIST),
          [row_text(c)[:120] for c in (EXP, OFF, SOON, LIST)])
    check("expired coupon: no Activate / Deactivate button", not row_btn(EXP, "Activate") and not js("[...(document.querySelector('tr[data-coupon=\"%s\"]')?.querySelectorAll('button')||[])].some(b=>b.textContent.trim()==='Deactivate')" % EXP))
    check("row: 12.34% OFF, three services, 1 per customer", all(x in row_text(LIST) for x in ["12.34% OFF", "My Bookings", "Passes", "Membership", "1 per customer"]), row_text(LIST))
    check("row: min booking ₹1,000.00 shown for the minimum coupon", "min booking ₹1,000.00" in row_text(MIN), row_text(MIN))
    check("hidden coupon marked 'Not listed in the portal'", "Not listed in the portal" in row_text(OK), row_text(OK))

    # search / filter / sort
    fill("Search", f"CPUIOFF{RUN}", "document"); drain(2)
    codes = js("[...document.querySelectorAll('tbody tr[data-coupon]')].map(r=>r.dataset.coupon)") or []
    check("search by code narrows the list to that coupon", codes == [OFF], codes)
    fill("Search", f"{RUN}", "document"); drain(2)
    fill("Sort by", "code", "document"); drain(2)
    codes = js("[...document.querySelectorAll('tbody tr[data-coupon]')].map(r=>r.dataset.coupon)") or []
    check("sort by Code: rows in code order", len(codes) >= 8 and codes == sorted(codes), codes)
    click(".oa-card", "Inactive"); drain(2)
    codes = js("[...document.querySelectorAll('tbody tr[data-coupon]')].map(r=>r.dataset.coupon)") or []
    check("Inactive card filters to the inactive coupon only", codes == [OFF], codes)
    click(".oa-card", "Total coupons"); drain(1)
    fill("Service", "1", "document"); drain(2)
    codes = js("[...document.querySelectorAll('tbody tr[data-coupon]')].map(r=>r.dataset.coupon)") or []
    check("Service filter My Bookings: only coupons for bookings", set(codes) == {LIST, BKONLY}, codes)
    fill("Service", "", "document"); fill("Search", "", "document"); fill("Sort by", "newest", "document"); drain(2)

    # editor: browser validation
    click("button", "New coupon"); drain(2)
    check("New coupon editor opens with every section", all(x in text() for x in ["Coupon code", "Discount", "Services it applies to", "Validity and status", "Usage limit and display"]), text()[-800:])
    n_before = one("SELECT COUNT(*) FROM MST_Coupon")
    fill("Coupon code", "a!"); fill("Discount value", "10")
    fill("Valid from", (today + dt.timedelta(days=5)).isoformat()); drain(0.3); fill("Valid until", (today + dt.timedelta(days=2)).isoformat())
    fill("Usage limit (total)", "2"); fill("Uses per customer", "3"); drain(0.3)
    click(".modal button", "Create coupon"); drain(2)
    t = text()
    check("browser validation: bad code", "Use 3 to 30 letters, digits, - or _ for the code." in t, t[-1500:])
    check("browser validation: no service chosen", "Choose where the coupon can be used" in t)
    check("browser validation: until before from", "Valid until must be on or after Valid from." in t)
    check("browser validation: per customer > total", "Uses per customer cannot be more than the total uses." in t)
    check("code field upper-cases what is typed", js("(() => { const l=[...document.querySelectorAll('.modal label')].find(l=>l.textContent.trim().startsWith('Coupon code')); const i=l.htmlFor?document.getElementById(l.htmlFor):l.querySelector('input'); return i.value })()") == "A!")
    shot("admin_coupon_editor_errors")
    fill("Coupon code", "AB"); fill("Discount value", "101"); drain(0.3)
    click(".modal button", "Create coupon"); drain(1.5)
    t = text()
    check("browser validation: 2-character code refused", "Use 3 to 30 letters" in t)
    check("browser validation: percentage above 100 refused", "A percentage cannot be more than 100." in t, t[-1200:])
    fill("Discount value", "0"); drain(0.3); click(".modal button", "Create coupon"); drain(1.5)
    check("browser validation: zero discount refused", "The discount must be more than zero." in text())
    check("nothing saved while the form has errors", one("SELECT COUNT(*) FROM MST_Coupon") == n_before)

    ADM = f"CPUIADM{RUN}"
    fill("Coupon code", ADM.lower()); fill("Description (optional)", "UI created coupon")
    fill("Discount value", "10"); fill("Maximum discount", "200"); fill("Minimum booking amount", "500")
    tick("Passes (day passes)"); tick("Membership (")
    fill("Valid from", today.isoformat()); drain(0.3); fill("Valid until", (today + dt.timedelta(days=30)).isoformat())
    fill("Usage limit (total)", "3"); fill("Uses per customer", "3"); drain(0.5)
    click(".modal button", "Create coupon"); drain(4)
    row = sql(f"SELECT CouponId, DiscountType, DiscountValue, ISNULL(CAST(MaxDiscountAmount AS varchar),'-'), ISNULL(CAST(MinAmount AS varchar),'-'), ValidFrom, ValidUntil, TotalUses, UsesPerCustomer, ShowInPortal, IsActive, Description,"
              f" (SELECT STRING_AGG(CAST(Module AS varchar),',') WITHIN GROUP (ORDER BY Module) FROM MST_CouponModule m WHERE m.CouponId=c.CouponId) FROM MST_Coupon c WHERE Code='{ADM}' AND IsDeleted=0")
    check("coupon created from the editor (typed lower case -> stored upper case)", len(row) == 1, (row, text()[-600:]))
    if row:
        ADM_ID = row[0][0]
        check("saved exactly: 10%, max 200, min 500, Passes+Membership, dates, 3 total / 3 per customer, listed, active",
              row[0][1:] == ["1", "10.00", "200.00", "500.00", today.isoformat(), (today + dt.timedelta(days=30)).isoformat(), "3", "3", "1", "1", "UI created coupon", "2,3"], row[0])
    else:
        ADM_ID = None
    check("new coupon listed as Active, '0 of 3 used'", "Active" in row_text(ADM) and "0 of 3 used" in row_text(ADM), row_text(ADM))
    shot("admin_coupons_desktop")

    # duplicate code
    click("button", "New coupon"); drain(2)
    fill("Coupon code", ADM); js("[...document.querySelectorAll('.modal .oa-seg button')].find(b=>b.textContent.trim()==='Fixed amount').click()"); drain(0.3)
    check("Fixed amount hides the maximum-discount field", not js("[...document.querySelectorAll('.modal label')].some(l=>l.textContent.includes('Maximum discount'))"))
    fill("Discount value", "50"); tick("My Bookings ("); fill("Valid until", (today + dt.timedelta(days=5)).isoformat()); drain(0.3)
    click(".modal button", "Create coupon"); drain(3)
    t = text()
    check("duplicate code refused by the server, message on the code field", f"Another coupon already uses the code {ADM}." in t and js("!!document.querySelector('.modal')"), t[-900:])
    check("duplicate not saved", one(f"SELECT COUNT(*) FROM MST_Coupon WHERE Code='{ADM}' AND IsDeleted=0") == "1")
    click(".modal button", "Cancel"); drain(1)

    # deactivate / activate / uses / delete
    row_btn(ADM, "Deactivate"); drain(3)
    check("Deactivate -> Inactive (screen and DB)", one(f"SELECT IsActive FROM MST_Coupon WHERE Code='{ADM}' AND IsDeleted=0") == "0" and "Inactive" in row_text(ADM), row_text(ADM))
    row_btn(ADM, "Activate"); drain(3)
    check("Activate -> Active again", one(f"SELECT IsActive FROM MST_Coupon WHERE Code='{ADM}' AND IsDeleted=0") == "1" and "Active" in row_text(ADM), row_text(ADM))
    row_btn(ADM, "Uses"); drain(2)
    check("Uses dialog: 'Not used yet'", "Not used yet" in text() and f"Uses · {ADM}" in text(), text()[-400:])
    click(".modal button", "Close"); drain(1)
    row_btn(DEL, "Delete"); drain(1.5)
    check("Delete asks for confirmation", "Delete this coupon?" in text())
    click(".modal button", "Delete"); drain(3)
    check("unused coupon deleted (soft) and gone from the list", one(f"SELECT IsDeleted FROM MST_Coupon WHERE CouponId={DEL_ID}") == "1" and not js("!!document.querySelector('tr[data-coupon=\"%s\"]')" % DEL))

    # phone
    viewport(390); go("/admin/offers?tab=coupons", 5)
    check("phone 390: coupon tab fits (no sideways scroll)", no_hscroll(), js("document.documentElement.scrollWidth"))
    shot("admin_coupons_phone")
    click("button", "New coupon"); drain(2)
    check("phone 390: coupon editor fits", no_hscroll() and "Services it applies to" in text(), js("document.documentElement.scrollWidth"))
    shot("admin_coupon_editor_phone")
    click(".modal button", "Cancel"); drain(1)
    viewport(1366)
    check("no JS errors on admin Offers / Coupon codes", not exceptions(n1), exceptions(n1))

    # Sales: OFFER.VIEW only
    login(sales_email, sales_pw, "/admin/login")
    go("/admin/offers?tab=coupons", 5)
    t = text()
    check("Sales (view only): coupon list opens", "Total coupons" in t and bool(row_text(LIST)), t[:400])
    check("Sales: no New coupon / Edit / Deactivate / Delete, Uses still there",
          "New coupon" not in t and not js("[...document.querySelectorAll('tbody button')].some(b=>['Edit','Delete','Deactivate','Activate'].includes(b.textContent.trim()))") and row_btn(LIST, "Uses"), t[:400])
    drain(1); click(".modal button", "Close"); drain(0.5)

    # ---- customer -----------------------------------------------------------------------------------------
    if not sink_up():
        raise SystemExit("SMTP sink stopped answering on 127.0.0.1:2525 - stopping before any purchase.")
    login(cust_email, cust_pw, "/login")
    n2 = len(events)
    go("/passes", 5)
    strip = js("[...document.querySelectorAll('[data-rg=coupon-strip] .cp-card')].map(c=>c.dataset.coupon)") or []
    check("Book Pass: coupon cards show the listed coupon", LIST in strip and "Coupon codes" in text(), strip)
    check("Book Pass: hidden, expired, inactive, not-started and bookings-only coupons not shown", not ({OK, EXP, OFF, SOON, BKONLY, MIN} & set(strip)), strip)
    card = (js("document.querySelector('.cp-card[data-coupon=\"%s\"]')?.innerText || ''" % LIST) or "").replace("\u00a0", " ")
    check("coupon card: deal, description, '1 use left for you', valid till", all(x in card for x in ["12.34% OFF", "UI test: listed coupon", "1 use left for you", "Valid till"]), card)
    shot("customer_pass_coupon_cards")

    click(".choice-card", "Single Day"); drain(1.5)
    fill("First visit date", DAY.isoformat(), "document"); drain(4)
    check("day pass checkout shows the picker", wait_for("!!document.querySelector('[data-rg=offer-picker]')"), text()[-600:])
    drain(2)
    check("picker link reads 'Have a coupon or offer code?'", "Have a coupon or offer code?" in picker(), picker())
    chips = js("[...document.querySelectorAll('[data-rg=coupon-chips] .cp-chip')].map(c=>c.dataset.coupon)") or []
    check("Available coupons chips at checkout: listed coupon only", LIST in chips and not ({OK, EXP, OFF, SOON, BKONLY} & set(chips)), chips)
    type_code(f"NOPE{RUN}")
    check("unknown code: 'is not valid'", f"The coupon code NOPE{RUN} is not valid" in alert(), alert())
    type_code(EXP)
    check("expired coupon refused with the date", f"The coupon {EXP} expired on" in alert(), alert())
    type_code(OFF)
    check("inactive coupon refused", f"The coupon {OFF} is not active." in alert(), alert())
    type_code(SOON)
    check("not-yet-started coupon refused with its start date", f"The coupon {SOON} can be used from" in alert(), alert())
    type_code(BKONLY)
    check("bookings-only coupon refused on a pass", f"The coupon {BKONLY} cannot be used on" in alert(), alert())
    type_code(MIN)
    check("below minimum: 'Spend at least ₹1,000.00'", f"Spend at least ₹1,000.00 (before GST) to use the coupon {MIN}." in alert(), alert())
    shot("customer_pass_coupon_refused")
    check("refused codes leave the price untouched (no 'Coupon applied')", "Coupon applied" not in picker())
    type_code(" " + OK.lower() + " ")
    p = price(800, pct=20, cap="61.50")
    t = picker()
    check("valid coupon (typed lower case, spaces): 'Coupon applied' replaces the auto offer", "Coupon applied" in t and "Offer applied" not in t and js("!!document.querySelector('[data-rg=coupon-remove]')"), t)
    check(f"day pass priced by hand: {rs(p['orig'])} - {rs(p['disc'])} -> taxable {rs(p['taxable'])}, GST {rs(p['cgst'] + p['sgst'])}, You pay {rs(p['total'])}",
          all(x in t for x in [rs(p["orig"]), "− " + rs(p["disc"]), rs(p["taxable"]), rs(p["cgst"] + p["sgst"]), rs(p["total"])]) and "You pay" in t, (t, p))
    check("pay buttons charge the coupon price, not the full price", js("!document.querySelector('.pay-options') || ![...document.querySelectorAll('.pay-options button')].some(b=>b.textContent.includes('944'))"))
    summ = js("(() => { const r=document.querySelector('[data-rg=pass-discount-row]'); return r ? r.parentElement.innerText : '' })()") or ""
    check(f"order summary card: 'Coupon discount (before GST) − {rs(p['disc'])}' and the GST charged {rs(p['cgst'] + p['sgst'])}",
          "Coupon discount (before GST)" in summ and rs(p["disc"]) in summ and rs(p["cgst"] + p["sgst"]) in summ and rs(p["total"]) in summ, summ)
    shot("customer_pass_coupon_applied")
    viewport(390); drain(1)
    check("phone 390: day-pass checkout with coupon fits", no_hscroll(), js("document.documentElement.scrollWidth"))
    shot("customer_pass_coupon_phone")
    viewport(1366); drain(1)
    bal0 = wallet()
    last_pass = one(f"SELECT ISNULL(MAX(CustomerPassId),0) FROM TRN_CustomerPass WHERE CustomerId={cust_id}")
    click(".pay-options button", "Wallet"); drain(7)
    ps = sql(f"SELECT TOP 1 CustomerPassId, Price, TotalAmount FROM TRN_CustomerPass WHERE CustomerId={cust_id} AND CustomerPassId>{last_pass} ORDER BY CustomerPassId DESC")
    check("day pass bought from the wallet", bool(ps), text()[:400])
    if ps:
        pid = ps[0][0]
        check(f"pass TotalAmount = {p['total']}", D(ps[0][2]) == p["total"], ps)
        red = sql(f"SELECT CouponId, Module, BaseAmount, DiscountAmount, CouponCode, ISNULL(CAST(BookingId AS varchar),'-') FROM TRN_CouponRedemption WHERE CustomerPassId={pid}")
        check("TRN_CouponRedemption row: coupon, Passes, base 800.00, discount 61.50, code", red == [[str(OK_ID), "2", "800.00", str(p["disc"]), OK, "-"]], red)
        check("no offer redemption on the coupon purchase (coupon replaces the offer)", one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE CustomerPassId={pid}") == "0")
        inv = sql(f"SELECT SubTotal, DiscountAmount, TaxableAmount, CgstAmount, SgstAmount, TotalAmount FROM TRN_Invoice WHERE CustomerPassId={pid}")
        check(f"invoice: discount {p['disc']}, taxable {p['taxable']}, CGST/SGST {p['cgst']}, total {p['total']}",
              len(inv) == 1 and [D(x) for x in inv[0][1:]] == [p["disc"], p["taxable"], p["cgst"], p["sgst"], p["total"]], inv)
        check("invoice SubTotal is the price before the discount (800.00)", inv and D(inv[0][0]) == D("800.00"), inv)
        check(f"wallet debited exactly {p['total']}", wallet() == bal0 - p["total"], (bal0, wallet()))

    # the same coupon again
    go("/passes", 5)
    click(".choice-card", "Single Day"); drain(1.5)
    fill("First visit date", DAY.isoformat(), "document"); drain(4)
    wait_for("!!document.querySelector('[data-rg=offer-picker]')"); drain(2)
    type_code(OK)
    check("same coupon again (1 per customer): 'already used'", f"You have already used the coupon {OK}." in alert() and "Coupon applied" not in picker(), alert())
    shot("customer_pass_coupon_already_used")

    # 10-Day Pack: a test offer, replaced by the coupon, back after "Remove coupon"
    s, b = api("POST", "/admin/offers", ADMIN, {"offerName": f"CPUI-OFR {RUN}", "title": f"CPUI test offer {RUN}", "showInPortal": True, "discountType": 1, "discountValue": 20,
                                               "modules": [2], "passPlanIds": [3], "membershipPlanIds": [], "startDate": today.isoformat(), "endDate": (today + dt.timedelta(days=1)).isoformat(),
                                               "isActive": True, "canCombine": False, "sortOrder": 1})
    OFR_ID = b.get("data", {}).get("offerId") if s in (200, 201) else None
    check("test offer created (10-Day Pack only)", OFR_ID, (s, b))
    try:
        go("/passes", 5)
        click(".choice-card", "10-Day Pack"); drain(1.5)
        fill("First visit date", DAY.isoformat(), "document"); drain(4)
        wait_for("!!document.querySelector('[data-rg=offer-picker] [data-applied]')"); drain(2)
        po = price(6800, pct=20); pc = price(6800, pct="12.34")
        check(f"10-Day Pack: the test offer applies by itself ({rs(po['total'])})", js("!!document.querySelector('[data-applied][data-offer=\"%s\"]')" % OFR_ID) and rs(po["total"]) in picker(), picker())
        clicked = js("(() => { const c=document.querySelector('[data-rg=coupon-chips] .cp-chip[data-coupon=\"%s\"]'); if(!c) return false; c.click(); return true })()" % LIST); drain(3)
        t = picker()
        check(f"chip applies the listed coupon and replaces the offer ({rs(pc['disc'])} off, You pay {rs(pc['total'])})",
              clicked and "Coupon applied" in t and not js("!!document.querySelector('[data-applied][data-offer=\"%s\"]')" % OFR_ID) and "− " + rs(pc["disc"]) in t and rs(pc["total"]) in t
              and rs(pc["cgst"] + pc["sgst"]) in t, (clicked, t, pc))
        check("chip of the applied coupon is no longer offered", not js("!!document.querySelector('.cp-chip[data-coupon=\"%s\"]')" % LIST))
        shot("customer_coupon_replaces_offer")
        js("document.querySelector('[data-rg=coupon-remove]').click()"); drain(3)
        t = picker()
        check("Remove coupon brings the automatic offer back", js("!!document.querySelector('[data-applied][data-offer=\"%s\"]')" % OFR_ID) and "Offer applied" in t and rs(po["total"]) in t and "Coupon applied" not in t, t)
        check("after removing, the coupon chip is offered again", js("!!document.querySelector('.cp-chip[data-coupon=\"%s\"]')" % LIST))
        shot("customer_remove_coupon_offer_back")
    finally:
        if OFR_ID:
            api("DELETE", f"/admin/offers/{OFR_ID}", ADMIN)
        sql(f"""DECLARE @o TABLE (id int); INSERT INTO @o SELECT OfferId FROM MST_Offer WHERE OfferName = 'CPUI-OFR {RUN}';
            DELETE FROM TRN_OfferRedemption WHERE OfferId IN (SELECT id FROM @o); DELETE FROM MST_OfferScope WHERE OfferId IN (SELECT id FROM @o);
            DELETE FROM MST_OfferModule WHERE OfferId IN (SELECT id FROM @o); DELETE FROM MST_Offer WHERE OfferId IN (SELECT id FROM @o);""")

    # Membership: cards and a chip
    go("/membership", 5)
    strip = js("[...document.querySelectorAll('[data-rg=coupon-strip] .cp-card')].map(c=>c.dataset.coupon)") or []
    check("Membership: coupon cards show the listed coupons", LIST in strip and ADM in strip, strip)
    check("Membership: passes-only / hidden / expired / inactive coupons not shown", not ({OK, EXP, OFF, SOON, BKONLY, MIN} & set(strip)), strip)
    shot("customer_membership_coupon_cards")
    js("[...document.querySelectorAll('.choice-card')][0]?.click()"); drain(1.5)
    js("[...document.querySelectorAll('.choice-card')][0]?.click()"); drain(1)
    click("button", "Continue to payment"); drain(3)
    wait_for("!!document.querySelector('[data-rg=offer-picker]')"); drain(2)
    clicked = js("(() => { const c=document.querySelector('[data-rg=coupon-chips] .cp-chip[data-coupon=\"%s\"]'); if(!c) return false; c.click(); return true })()" % LIST); drain(3)
    t = picker()
    check("membership checkout: chip applies the coupon", clicked and "Coupon applied" in t and f"{LIST} discount (before GST)" in t, (clicked, t))
    m = js("(() => { const p=document.querySelector('[data-rg=offer-summary]'); if(!p) return null; return [...p.children].map(r=>r.innerText) })()") or []
    try:
        orig = D([x for x in m if x.startswith("Original")][0].split("₹")[-1].replace(",", ""))
        base = r2(orig / D("1.18"))
        pm = price(base, pct="12.34")
        check(f"membership: amounts match our arithmetic for a {rs(base)} plan", any(rs(pm["total"]) in x for x in m) and any(("− " + rs(pm["disc"])) in x for x in m), (m, pm))
    except Exception as e:
        check("membership: summary readable", False, (m, e))
    check("membership: 'Total payable' follows the coupon", js("!!document.querySelector('.mem-pay__due .of-strike')"), js("document.querySelector('.mem-pay__due')?.innerText"))
    shot("customer_membership_coupon_applied")

    # Meeting-room booking with a chip, paid from the wallet
    go("/book", 5)
    ok = js("(() => { const c=[...document.querySelectorAll('.choice-card')].find(e=>e.querySelector('.choice-card__title')?.textContent.trim()==='Meeting Room'); if(!c) return false; c.click(); return true })()"); drain(1.5)
    fill("From date", DAY.isoformat(), "document"); drain(0.5); fill("To date", DAY.isoformat(), "document"); drain(3)
    wait_for("[...document.querySelectorAll('.slot-chip')].some(b=>!b.disabled)")
    js("(() => { const b=[...document.querySelectorAll('.slot-chip')].filter(b=>!b.disabled); b[Math.min(6,b.length-1)].click(); return true })()"); drain(1)
    click("button", "Find available workspaces"); drain(4)
    picked = js("(() => { const c=[...document.querySelectorAll('.choice-card')].find(e=>/Meeting Room/.test(e.textContent) && !e.disabled && !/^Meeting Room$/.test(e.querySelector('.choice-card__title')?.textContent.trim()||'')); if(!c) return false; c.click(); return true })()"); drain(4)
    check("booking wizard reaches Review & pay for a meeting room (1 hour)", ok and picked and wait_for("!!document.querySelector('[data-rg=offer-picker]')"), text()[:500])
    drain(2)
    pb = price(800, pct="12.34")
    check("original price shown is ₹944.00 (800/h + GST)", rs(D("944")) in text(), picker())
    clicked = js("(() => { const c=document.querySelector('[data-rg=coupon-chips] .cp-chip[data-coupon=\"%s\"]'); if(!c) return false; c.click(); return true })()" % LIST); drain(3)
    t = picker()
    check(f"booking: chip applies the coupon, You pay {rs(pb['total'])} ({rs(pb['disc'])} off, GST {rs(pb['cgst'] + pb['sgst'])})",
          clicked and "Coupon applied" in t and all(x in t for x in ["− " + rs(pb["disc"]), rs(pb["taxable"]), rs(pb["cgst"] + pb["sgst"]), rs(pb["total"])]), (clicked, t, pb))
    shot("customer_booking_coupon_applied")
    bal1 = wallet()
    last_bk = one(f"SELECT ISNULL(MAX(BookingId),0) FROM TRN_Booking WHERE CustomerId={cust_id}")
    click("button", "Wallet"); drain(7)
    bk = sql(f"SELECT BookingId, BaseAmount, DiscountAmount, TaxAmount, TotalAmount, Status FROM TRN_Booking WHERE CustomerId={cust_id} AND BookingId>{last_bk} ORDER BY BookingId")
    check("one booking made and confirmed from the wallet", len(bk) == 1 and bk[0][5] == "2", (bk, text()[:300]))
    shot("customer_booking_paid")
    if bk:
        bid = bk[0][0]
        check(f"booking amounts: base 800, discount {pb['disc']}, tax {pb['cgst'] + pb['sgst']}, total {pb['total']}",
              [D(x) for x in bk[0][1:5]] == [D("800"), pb["disc"], pb["cgst"] + pb["sgst"], pb["total"]], bk)
        red = sql(f"SELECT CouponId, Module, BaseAmount, DiscountAmount, CouponCode FROM TRN_CouponRedemption WHERE BookingId={bid}")
        check("TRN_CouponRedemption row for the booking", red == [[str(LIST_ID), "1", "800.00", str(pb["disc"]), LIST]], red)
        check("no offer redemption on the coupon booking", one(f"SELECT COUNT(*) FROM TRN_OfferRedemption WHERE BookingId={bid}") == "0")
        inv = sql(f"SELECT DiscountAmount, TaxableAmount, CgstAmount, SgstAmount, TotalAmount FROM TRN_Invoice WHERE BookingId={bid}")
        check(f"wallet debited exactly {pb['total']} for the booking", wallet() == bal1 - pb["total"], (bal1, wallet()))
        check("booking invoice matches the coupon price", len(inv) == 1 and [D(x) for x in inv[0]] == [pb["disc"], pb["taxable"], pb["cgst"], pb["sgst"], pb["total"]], inv)
    go("/passes", 5)
    card = (js("document.querySelector('.cp-card[data-coupon=\"%s\"]')?.innerText || ''" % LIST) or "").replace("\u00a0", " ")
    check("after using it, the coupon card says 'Already used'", "Already used" in card, card)
    click(".choice-card", "Single Day"); drain(1.5)
    fill("First visit date", DAY.isoformat(), "document"); drain(4)
    wait_for("!!document.querySelector('[data-rg=offer-picker]')"); drain(2)
    check("used-up coupon not offered as a chip", not js("!!document.querySelector('.cp-chip[data-coupon=\"%s\"]')" % LIST))
    check("no JS errors in the customer flows", not exceptions(n2), exceptions(n2))

    # ---- admin after use ----------------------------------------------------------------------------
    login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "/admin/login")
    n3 = len(events)
    go("/admin/offers?tab=coupons", 5)
    check("list: used coupon shows '1 used' and the discount given", "1 used" in row_text(OK) and f"{rs(price(800, pct=20, cap='61.50')['disc'])} off" in row_text(OK), row_text(OK))
    row_btn(OK, "Uses"); drain(2)
    t = text()
    check("Uses dialog lists the purchase: customer, Passes, 800.00, − 61.50, Used", all(x in t for x in ["Coupons UI Anjali", "Passes", "₹800.00", "− ₹61.50", "Used"]), t[-700:])
    shot("admin_coupon_uses")
    click(".modal button", "Close"); drain(1)
    row_btn(OK, "Delete"); drain(1.5); click(".modal button", "Delete"); drain(3)
    check("a used coupon cannot be deleted (stays, still listed)", one(f"SELECT IsDeleted FROM MST_Coupon WHERE CouponId={OK_ID}") == "0" and bool(row_text(OK)), text()[-400:])
    row_btn(OK, "Edit"); drain(2)
    check("editing a used coupon says the code can no longer change", "the code can no longer change" in text(), text()[-500:])
    fill("Coupon code", OK + "X"); drain(0.3); click(".modal button", "Save"); drain(3)
    check("changing the code of a used coupon is refused", one(f"SELECT Code FROM MST_Coupon WHERE CouponId={OK_ID}") == OK and "cannot change" in text(), text()[-500:])
    click(".modal button", "Cancel"); drain(1)
    check("no JS errors on admin after use", not exceptions(n3), exceptions(n3))
except SystemExit:
    raise
except Exception:
    check("script ran to the end", False, traceback.format_exc())
finally:
    try:
        if ws: ws.close()
        if proc: proc.terminate()
    finally:
        purge()

print(f"\n{sum(results)}/{len(results)} passed")
sys.exit(0 if results and all(results) else 1)
