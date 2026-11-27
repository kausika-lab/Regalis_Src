"""Headless Chrome checks for the admin-booking kit, desktop (1366 px) and phone (390 px).
LOCAL stack only: http://localhost:8080 (repo frontend/ with apply.mjs run) -> API :81
(D:\\Regalis\\local-dev\\api with the kit), Dev DB, mail to a local SMTP sink on 127.0.0.1:2525
(checked first; the script stops if nothing listens there).

Admin, Bookings > New booking: the old modal (Settlement cash / wallet / pay later) is gone; the
customer booking page opens in place of the list with Customer and Location fields and no
Customisation tile; nothing can be searched before a customer is chosen; a single slot, a monthly
desk, a Virtual Office (with a coupon typed for the customer) and a two-day range are booked with
one "Confirm booking" button and no wallet / online / pay-later choice; the confirmation says the
amount is due; every booking is checked in SQL (customer, source 2, pay later, created by the
staff user, nothing paid, no payment row); the list and the detail show them (Front desk, Pay later
due). Customer: the Book a Workspace page is as before (header, Customisation tile, no Customer or
Location field, Wallet / Pay online / Pay later at Review & pay) and a wallet booking still works;
My Bookings lists the staff bookings. Phone: no horizontal scroll on either page.

Booking.MaxMinutes is raised through the settings API for the two-day range only (a whole working
day on Dev is 780 minutes, above the Dev value 480) and put back afterwards, whatever happens.

Creates customer abui-c<run>@example.test (pay later OFF on the account) and coupon ABUI<run>.
Removes at the end (also after a failure): the coupon and its redemptions, the Wi-Fi rows of the
test user; bookings are cancelled (Status 5), passes cancelled (Status 4), the user soft-deleted.
Its invoices stay (the app cannot delete invoices).
Env: PYLIB (websocket-client), SHOTS_DIR, REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD."""
import base64, datetime as dt, json, os, random, socket, subprocess, sys, time, traceback, urllib.request, urllib.error
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


DAY = open_day(20 + random.randint(0, 5))
DAY2 = open_day((DAY - today).days + 1)
DAY3 = open_day((DAY2 - today).days + 1)


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
    sql(f"""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE Email LIKE 'abui-%@example.test';
        DECLARE @c TABLE (id int); INSERT INTO @c SELECT CouponId FROM MST_Coupon WHERE Code LIKE 'ABUI%';
        DELETE FROM TRN_CouponRedemption WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM MST_CouponModule WHERE CouponId IN (SELECT id FROM @c);
        DELETE FROM MST_Coupon WHERE CouponId IN (SELECT id FROM @c);
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
            DELETE FROM TRN_WifiAccess WHERE ParentAccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_WifiAccess WHERE AccessId IN (SELECT id FROM @a);
            DELETE FROM TRN_AdminNotification WHERE ReferenceType = 'WifiAccess' AND ReferenceId IN (SELECT id FROM @a);
        END
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'admin-booking ui cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE TRN_CustomerPass SET Status = 4 WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 5);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE Email LIKE 'abui-%@example.test' AND IsDeleted = 0;""")


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as s:
            return s.recv(100).startswith(b"220")
    except OSError:
        return False


results = []
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:600]), flush=True)


if not sink_up():
    raise SystemExit("No SMTP sink answering on 127.0.0.1:2525 - not running (bookings would send mail).")
purge()
ADMIN = session("admin", os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")
ADMIN_ID = int(one(f"SELECT UserId FROM MST_User WHERE Email = '{os.environ['REGALIS_ADMIN_EMAIL']}'"))
ORIG_MAX = one("SELECT SettingValue FROM MST_AppSetting WHERE SettingKey='Booking.MaxMinutes'")

proc = ws = None
raised = False
try:
    pw = "AdmBk#Ui" + RUN + "x"
    cust_email = f"abui-c{RUN}@example.test"
    cust_name = f"AdminBooking UI Meera {RUN}"
    s, b = api("POST", "/users", ADMIN, {"fullName": cust_name, "email": cust_email, "mobileNumber": f"+9194{RUN}{random.randint(100, 999)}", "password": pw, "userType": 1,
                                         "roleIds": [int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))], "defaultLocationId": 2})
    if s not in (200, 201): raise SystemExit(f"create customer: {s} {b}")
    CID = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru',"
        f" State='Karnataka', Country='India', PostalCode='560098', Gender=2, PayLaterAllowed=0 WHERE UserId={CID}")
    api("POST", f"/wallet/{CID}/adjust", ADMIN, {"direction": "credit", "amount": 20000, "reason": "admin-booking ui"})
    CODE = f"ABUI{RUN}"
    s, b = api("POST", "/admin/coupons", ADMIN, {"code": CODE, "description": "admin-booking ui", "discountType": 2, "discountValue": 250, "modules": [1, 3],
                                                "validFrom": today.isoformat(), "validUntil": (today + dt.timedelta(days=40)).isoformat(), "isActive": True})
    if s not in (200, 201): raise SystemExit(f"create coupon: {s} {b}")

    def money_rows():
        r = sql(f"SELECT (SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={CID}), (SELECT COUNT(*) FROM TRN_InvoicePayment WHERE CustomerId={CID}),"
                f" (SELECT COUNT(*) FROM TRN_CashCollection WHERE CustomerId={CID}), (SELECT COUNT(*) FROM TRN_WalletTransaction WHERE CustomerId={CID})")
        return tuple(int(x) for x in r[0])
    M0 = money_rows()

    # ---- browser ------------------------------------------------------------------------------------
    proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9377", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-adminbooking-prof-{RUN}",
                             "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(60):
        try:
            page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9377/json").read()) if t["type"] == "page"); break
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
    def wait_text(t, s=12): return wait_for(f"document.body.innerText.includes({json.dumps(t)})", s)
    SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:el.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype;"
           "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event(el.tagName==='SELECT'?'change':'input',{bubbles:true}))};"
           "const byLabel=(t,root)=>{const l=[...(root||document).querySelectorAll('label')].find(l=>l.textContent.trim().replace(/\\s*\\*$/,'').startsWith(t));if(!l)return null;return l.htmlFor?document.getElementById(l.htmlFor):l.querySelector('input,select,textarea')};")
    def fill(label, value, root="document"):
        return js(f"(() => {{{SET} const el=byLabel({json.dumps(label)},{root}); if(!el) return false; setv(el,{json.dumps(value)}); return true }})()")
    def field_exists(label): return js(f"(() => {{{SET} return !!byLabel({json.dumps(label)}, document) }})()")
    def viewport(w):
        send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)
    def login(email, pw_, path):
        js("localStorage.clear(); sessionStorage.clear(); true"); go(path, 3)
        js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(email)});setv(document.querySelector('input[type=password]'),{json.dumps(pw_)});document.querySelector('form button[type=submit]').click();return true}})()")
        drain(5)
    def tile(name, expect):
        for _ in range(4):
            click("button.choice-card", name); drain(0.8)
            if wait_text(expect, 4): return True
        return False
    def term(label):
        return js("(() => { const b=[...document.querySelectorAll('button.choice-card')].find(b=>b.querySelector('.choice-card__title')?.innerText.trim()===%s); if(!b) return false; b.click(); return true })()" % json.dumps(label))
    def find_workspaces():
        """Find available workspaces, then wait for the workspace step (its 'Change dates' button)."""
        wait_for("[...document.querySelectorAll('button')].some(b=>b.textContent.includes('Find available workspaces') && !b.disabled)", 8)
        click("button", "Find available workspaces")
        ok = wait_text("Change dates", 15); drain(1)
        return ok
    def main_text(): return (js("(document.querySelector('main')||document.body).innerText") or "").replace(" ", " ")
    def tiles(): return js("[...document.querySelectorAll('.choice-grid')[0]?.querySelectorAll('button.choice-card .choice-card__title')||[]].map(x=>x.innerText.trim())") or []
    def pick_slot(hhmm):
        return js("(() => { const b=[...document.querySelectorAll('.slot-chip')].find(b=>b.textContent.trim().startsWith(%s) && !b.disabled); if(!b) return false; b.click(); return true })()" % json.dumps(hhmm))
    def pick_workspace():
        return js("(() => { const b=[...document.querySelectorAll('.choice-grid button.choice-card')].find(b=>!b.disabled); if(!b) return null; const n=b.querySelector('.choice-card__title span')?.innerText||b.innerText; b.click(); return n })()")
    def buttons(): return js("[...document.querySelectorAll('button')].filter(b=>b.offsetParent!==null).map(b=>b.textContent.trim())") or []
    def choose_customer():
        fill("Find customer", cust_name)
        ok = wait_for(f"[...document.querySelectorAll('select option')].some(o=>o.value==='{CID}')", 10)
        fill("Customer", str(CID)); drain(1)
        return ok
    def last_booking():
        r = sql(f"SELECT TOP 1 BookingId, CustomerId, BookingSource, CAST(IsPayLater AS int), CreatedByUserId, CAST(AmountPaid AS decimal(12,2)), Status, ISNULL(BillingMonths,0), BookingNumber"
                f" FROM TRN_Booking WHERE CustomerId={CID} ORDER BY BookingId DESC")
        return r[0] if r else None
    def admin_row_ok(r):
        return r and int(r[1]) == CID and r[2] == "2" and r[3] == "1" and int(r[4]) == ADMIN_ID and float(r[5]) == 0 and r[6] == "2"

    send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("DOM.enable"); send("Network.setBypassServiceWorker", bypass=True)
    send("Network.setUserAgentOverride", userAgent=UA)
    viewport(1366)
    go("/admin/login", 3)
    login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "/admin/login")
    n0 = len(events)

    # ---- admin: New booking opens the customer booking page ------------------------------------------------
    go("/admin/bookings", 5)
    check("Admin Bookings: New booking button", "New booking" in buttons(), buttons())
    click("button", "New booking"); drain(3)
    t = text()
    check("New booking opens a full page (no modal) with Back to bookings", js("!document.querySelector('.modal')") and js("!!document.querySelector('[data-rg=admin-new-booking]')") and "Back to bookings" in t, t[:400])
    check("old modal gone: no Settlement, Cash at desk or Customer wallet choice", not any(x in t for x in ["Settlement", "Cash at desk", "Customer wallet", "New booking (front desk)"]), t[:600])
    check("Customer search, Customer and Location fields", field_exists("Find customer") and field_exists("Customer") and field_exists("Location"))
    check("same steps as the customer page: '1 · Choose your workspace' and the type cards", "1 · Choose your workspace" in t and all(x in tiles() for x in ["Desk", "Cabin", "Meeting Room", "Virtual Office"]), tiles())
    check("no Customisation tile on the admin screen", "Customisation" not in tiles(), tiles())
    check("no 'Book a Workspace' customer header", "Book a Workspace" not in t)
    shot("01-admin-new-booking")
    tile("Meeting Room", "2 · Choose your date and time")
    check("without a customer: 'Choose the customer to book for.' and the search button is off",
          "Choose the customer to book for." in text() and js("[...document.querySelectorAll('button')].find(b=>b.textContent.includes('Find available workspaces'))?.disabled === true"), text()[:500])
    check("customer found by name and chosen", choose_customer())
    check("customer chosen: the hint goes away", "Choose the customer to book for." not in text())
    fill("Location", "2"); drain(0.5)

    # ---- single slot ----------------------------------------------------------------------------------
    fill("From date", DAY.isoformat()); fill("To date", DAY.isoformat()); drain(2.5)
    check("time slots load (same slot grid as the customer page)", wait_for("document.querySelectorAll('.slot-chip').length > 0", 10), text()[:400])
    check("10:00 slot picked", pick_slot("10:00")); drain(0.5)
    check("workspace step reached", find_workspaces(), main_text()[:500])
    ws_name = pick_workspace()
    check("a workspace picked and priced", bool(ws_name) and wait_text("Review & confirm", 12), main_text()[:500])
    t = text(); b_ = buttons()
    check("Review & confirm: no Wallet, Pay online or Pay later button", not any(x in " | ".join(b_) for x in ["Wallet", "Pay online", "Pay later"]), b_)
    check("Review & confirm: one 'Confirm booking' button", b_.count("Confirm booking") == 1, b_)
    check("Review & confirm: no wallet balance or pay-at-desk wording", not any(x in t for x in ["Wallet balance", "Pay later at the front desk", "Review & pay", "held for 10 minutes while payment completes"]), t[-800:])
    check("Review & confirm: GST lines and total as on the customer page", all(x in t for x in ["Base amount", "CGST @ 9%", "SGST @ 9%", "Total"]), t[:800])
    check("Review & confirm: says nothing is collected and the invoice stays due", "Nothing is collected here" in t, t[-600:])
    shot("02-review-confirm")
    click("button", "Confirm booking")
    check("confirmed: 'Booking confirmed' + amount due from the customer", wait_text("Booking confirmed", 15) and "is due from the customer" in text(), text()[:600])
    check("confirmed: no 'your QR pass' customer wording, Back to bookings offered", "Your QR access pass is ready" not in text() and "Back to bookings" in buttons(), buttons())
    r1 = last_booking()
    check("SQL: single slot is the customer's, source 2, pay later, created by the staff user, nothing paid, Confirmed", admin_row_ok(r1) and r1[7] == "0", r1)
    shot("03-confirmed")

    # ---- monthly desk ----------------------------------------------------------------------------------
    click("button", "Book another"); drain(2)
    tile("Desk", "Desks and cabins are booked by the month")
    fill("Starts on", DAY.isoformat()); drain(0.5)
    check("monthly: 1-month term chosen", term("1 month")); drain(0.5)
    check("monthly: workspace step reached", find_workspaces(), main_text()[:600])
    ws_name = pick_workspace()
    ok = bool(ws_name) and wait_text("Review & confirm", 12)
    if not ok: shot("err-monthly")
    check("monthly: desk picked and priced for the term", ok and "Term" in text() and "Rate per month" in text(), (ws_name, main_text()[:700]))
    click("button", "Confirm booking")
    check("monthly confirmed", wait_text("Booking confirmed", 15) and "is due from the customer" in text(), text()[:400])
    r2 = last_booking()
    check("SQL: monthly booking is the customer's, 1 month, source 2, pay later, staff-created, nothing paid", admin_row_ok(r2) and r2[7] == "1" and r2[0] != r1[0], r2)

    # ---- Virtual Office with a coupon typed for the customer --------------------------------------------
    click("button", "Book another"); drain(2)
    tile("Virtual Office", "A Virtual Office is sold by the month")
    fill("Starts on", DAY.isoformat()); drain(0.5)
    wait_for("[...document.querySelectorAll('button.choice-card .choice-card__title')].some(x=>x.innerText.trim()==='1 month')", 8)
    term("1 month"); drain(0.5)
    check("Virtual Office: 'Continue to review' instead of 'Continue to payment'", "Continue to review" in buttons() and "Continue to payment" not in buttons(), buttons())
    click("button", "Continue to review"); drain(3)
    check("Virtual Office: Review & confirm with one 'Confirm Virtual Office' button, no wallet / pay-later",
          "Review & confirm" in text() and "Confirm Virtual Office" in buttons() and not any("Wallet" in x or "Pay" in x for x in buttons()), buttons())
    if not js("!!document.querySelector('.of-codeform')"):
        click("[data-rg=offer-picker] button", "Have a coupon or offer code?"); drain(0.5)
    fill("Coupon or offer code", CODE); drain(0.3)
    click(".of-codeform button", "Apply"); drain(3)
    pk = (js("document.querySelector('[data-rg=offer-picker]')?.innerText || ''") or "").replace("\u00a0", " ")
    check("coupon typed for the customer is applied (₹250 off), staff wording 'Total after discount'", "Coupon applied" in pk and "Total after discount" in pk and "You pay" not in pk, pk)
    shot("04-vo-review")
    click("button", "Confirm Virtual Office")
    check("Virtual Office reserved, due from the customer", wait_text("Virtual Office reserved", 15) and "due from the customer" in text(), text()[:500])
    vo = sql(f"SELECT TOP 1 Status, CAST(IsPayLater AS int), CAST(AmountPaid AS decimal(12,2)), ISNULL(PaymentId,0), CreatedBy FROM TRN_CustomerPass WHERE CustomerId={CID} ORDER BY CustomerPassId DESC")
    check("SQL: Virtual Office AwaitingPayment (5), pay later, nothing paid, no payment, staff-created", vo and vo[0][0] == "5" and vo[0][1] == "1" and float(vo[0][2]) == 0 and vo[0][3] == "0" and int(vo[0][4]) == ADMIN_ID, vo)
    disc = one(f"SELECT TOP 1 CAST(DiscountAmount AS decimal(12,2)) FROM TRN_Invoice WHERE CustomerId={CID} AND CustomerPassId IS NOT NULL ORDER BY InvoiceId DESC")
    check("SQL: the Virtual Office invoice carries the ₹250 coupon discount", disc is not None and float(disc) == 250, disc)

    # ---- two-day range (whole working day on each date) -----------------------------------------------------
    s_, sl = api("GET", f"/availability/slots?locationId=2&workspaceTypeId=3&date={DAY2.isoformat()}", ADMIN)
    whole = int((dt.datetime.fromisoformat(sl["data"][-1]["startUtc"].replace("Z", "+00:00")) - dt.datetime.fromisoformat(sl["data"][0]["startUtc"].replace("Z", "+00:00"))).total_seconds() // 60) + 30
    if int(ORIG_MAX) < whole:
        s_, b_ = api("PUT", "/settings", ADMIN, {"settings": [{"settingKey": "Booking.MaxMinutes", "settingValue": str(whole)}]})
        raised = s_ == 200
        check("test set-up: Booking.MaxMinutes raised for the two-day range", raised, (s_, b_))
    click("button", "Book another"); drain(2)
    tile("Meeting Room", "2 · Choose your date and time")
    fill("From date", DAY2.isoformat()); drain(0.3); fill("To date", DAY3.isoformat()); drain(1)
    check("two dates: 'booked as a whole day on each date'", "booked as a whole day on each date" in text(), main_text()[:500])
    check("range: workspace step reached", find_workspaces(), main_text()[:500])
    ws_name = pick_workspace()
    check("range priced: Review & confirm with 'Confirm 2 days'", wait_text("Review & confirm", 15) and "Confirm 2 days" in buttons(), buttons())
    click("button", "Confirm 2 days")
    check("two days confirmed", wait_text("2 days confirmed", 20) and "is due from the customer" in text(), text()[:500])
    rows = sql(f"SELECT TOP 2 BookingId, CustomerId, BookingSource, CAST(IsPayLater AS int), CreatedByUserId, CAST(AmountPaid AS decimal(12,2)), Status, ISNULL(BillingMonths,0), BookingNumber FROM TRN_Booking WHERE CustomerId={CID} ORDER BY BookingId DESC")
    check("SQL: both days are the customer's, source 2, pay later, staff-created, nothing paid", len(rows) == 2 and all(admin_row_ok(r) for r in rows), rows)
    if raised:
        s_, b_ = api("PUT", "/settings", ADMIN, {"settings": [{"settingKey": "Booking.MaxMinutes", "settingValue": ORIG_MAX}]})
        raised = not (s_ == 200)
        check(f"test tear-down: Booking.MaxMinutes back to {ORIG_MAX}", s_ == 200, (s_, b_))
    check("no payment, receipt, cash or wallet row was written for any of the staff bookings", money_rows() == M0, (money_rows(), M0))
    check("admin pages: no JavaScript errors", exceptions(n0) == [], exceptions(n0))

    # ---- list and detail --------------------------------------------------------------------------------
    click("button", "Back to bookings"); drain(3)
    check("Back to bookings: the list is back", "Every booking in the system" in text() and js("!document.querySelector('[data-rg=admin-new-booking]')"))
    js("(() => {" + SET + f"const i=[...document.querySelectorAll('input')].find(i=>i.placeholder&&i.placeholder.startsWith('Booking no.')); setv(i, {json.dumps(r1[8])}); return true}})()"); drain(3)
    t = text()
    check("list: the staff booking with 'Pay later · ₹… due'", r1[8] in t and "Pay later ·" in t and "due" in t, t[:800])
    click("button", "Detail"); drain(2)
    m = (js("document.querySelector('.modal')?.innerText || ''") or "")
    check("detail: Source 'Front desk', customer name", "Front desk" in m and cust_name in m, m[:600])
    shot("05-list-detail")
    click(".modal button", "Close") or js("document.querySelector('.modal button[aria-label]')?.click()"); drain(1)

    # ---- phone: admin page -------------------------------------------------------------------------------
    viewport(390); drain(1)
    go("/admin/bookings", 5)
    click("button", "New booking"); drain(3)
    check("phone 390: New booking page, no horizontal scroll", js("!!document.querySelector('[data-rg=admin-new-booking]')") and no_hscroll(), js("document.documentElement.scrollWidth"))
    shot("06-phone-admin")
    viewport(1366); drain(1)

    # ---- customer screen unchanged --------------------------------------------------------------------------
    login(cust_email, pw, "/login")
    n1 = len(events)
    go("/book", 5)
    wait_for("document.querySelectorAll('.choice-grid button.choice-card').length >= 5", 12)
    t = text()
    check("customer: 'Book a Workspace' header and the Customisation tile as before", "Book a Workspace" in t and "Customisation" in tiles(), tiles())
    check("customer: no Customer, Find customer or Location field, no admin hint", not field_exists("Find customer") and not field_exists("Customer") and js("!document.querySelector('[data-rg=admin-booking-for]')") and "Choose the customer" not in t)
    tile("Meeting Room", "2 · Choose your date and time")
    fill("From date", DAY.isoformat()); fill("To date", DAY.isoformat()); drain(2.5)
    check("customer: Meeting Room slots load", wait_for("document.querySelectorAll('.slot-chip').length > 0", 10), main_text()[:300])
    check("customer: 11:00 slot picked", pick_slot("11:00")); drain(0.5)
    check("customer: workspace step reached", find_workspaces(), main_text()[:500])
    pick_workspace(); wait_text("Review & pay", 12); drain(1)
    b_ = buttons(); t = text()
    check("customer: 'Review & pay' with Wallet and Pay online, no 'Confirm booking'", "Review & pay" in t and any(x.startswith("Wallet") for x in b_) and "Confirm booking" not in b_, b_)
    check("customer: 'held for 10 minutes while payment completes' note unchanged", "The slot is held for 10 minutes while payment completes." in t, t[-400:])
    check("customer: pay later not offered (off on this account), as before", not any("Pay later" in x for x in b_), b_)
    shot("07-customer-review-pay")
    click("button", "Wallet")
    check("customer: wallet booking confirmed as before", wait_text("Booking confirmed", 15) and "Your QR access pass is ready" in text(), text()[:400])
    rc = last_booking()
    check("SQL: customer booking source 1, created by the customer, paid", rc and rc[2] == "1" and int(rc[4]) == CID and rc[3] == "0", rc)
    go("/bookings", 5)
    t = text()
    check("customer My Bookings lists the staff bookings", r1[8] in t and r2[8] in t, t[:800])
    viewport(390); drain(1)
    go("/book", 4)
    check("phone 390: customer page, no horizontal scroll", no_hscroll(), js("document.documentElement.scrollWidth"))
    viewport(1366)
    check("customer pages: no JavaScript errors", exceptions(n1) == [], exceptions(n1))
except SystemExit:
    raise
except Exception:
    traceback.print_exc(); check("script ran to the end", False)
finally:
    if raised:
        api("PUT", "/settings", ADMIN, {"settings": [{"settingKey": "Booking.MaxMinutes", "settingValue": ORIG_MAX}]})
    try:
        if ws: ws.close()
    except Exception: pass
    if proc: proc.kill()
    purge()
    if one("SELECT SettingValue FROM MST_AppSetting WHERE SettingKey='Booking.MaxMinutes'") != ORIG_MAX:
        print("WARNING: Booking.MaxMinutes is not back to " + str(ORIG_MAX))

print(f"\n{sum(results)}/{len(results)} passed")
sys.exit(0 if all(results) else 1)
