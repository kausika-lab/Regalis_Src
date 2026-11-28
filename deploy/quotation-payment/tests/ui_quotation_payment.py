"""quotation-payment kit (27 Nov 2026): headless Chrome checks, LOCAL stack only, 1366 px and 390 px.
http://localhost:8080 (repo frontend/ with apply.mjs run) -> API :81 (D:\\Regalis\\local-dev\\api with the kit),
Dev DB RegalisWorkSpace_Dev; mail to the SMTP sink on 127.0.0.1:2525 (stops first if nothing answers there).

Payments screen: tabs "All payments" / "Quotations". The Quotations tab lists an approved quotation with its
invoice, total, paid, outstanding and Due; a rejected quotation is not found; "+ Record payment" opens the
usual Record payment dialog on the quotation's invoice, and after a part payment the row says Partially Paid
with the new figures (one payment row in the database); the "All payments" tab finds that payment by the
quotation number and shows it. Quotation page: "Open invoice" opens that invoice, "Payment status" opens the
Payments screen on the Quotations tab for that quotation. Admin Bookings > New booking: a meeting-room slot is
booked for a customer; no invoice wording or invoice button on the review and done steps, and no invoice or payment
row for it (user requirement: staff bookings are billed through the quotation); the cancel dialog
in the bookings list has no invoice wording. No page-wide horizontal scroll at 390 px.

Creates customer qpui-<run>@example.test, quotations "QA-QPUI <run> ...", one invoice payment, one booking.
Removes at the end through the API: payment reversed, invoices cancelled, booking cancelled, customer deleted.
Env: PYLIB (websocket-client), REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD, SHOTS_DIR (optional)."""
import base64, datetime as dt, json, os, random, socket, subprocess, sys, time, urllib.error, urllib.request
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket
sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.environ.get("SHOTS_DIR", os.path.join(HERE, "..", "evidence")); os.makedirs(SHOTS, exist_ok=True)
BASE, API = "http://localhost:8080", "http://127.0.0.1:81/api"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
TODAY = dt.datetime.now(IST).date()
ADMIN_EMAIL, ADMIN_PW = os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"]
CUST_EMAIL = f"qpui-{RUN}@example.test"
CUST_NAME = f"QP UI Customer {RUN}"


def open_day(after):
    d = TODAY + dt.timedelta(days=after)
    while d.weekday() == 6:
        d += dt.timedelta(days=1)
    return d


DAY = open_day(30 + random.randint(0, 10))


def sql(q):
    out = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:150] + " :: " + out.stdout + out.stderr)
    return [[c.strip() for c in l.split("|")] for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q); return r[0][0] if r else None


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
            if e.code == 401 and path != "/auth/login":
                TOK["t"] = admin_token(); continue
            try: return e.code, json.loads(e.read() or b"{}")
            except Exception: return e.code, {}
    raise SystemExit("rate limited")


def admin_token():
    req = urllib.request.Request(API + "/auth/login", data=json.dumps({"email": ADMIN_EMAIL, "password": ADMIN_PW, "portal": "admin"}).encode(), method="POST")
    req.add_header("Content-Type", "application/json")
    with urllib.request.urlopen(req, timeout=60) as r: return json.loads(r.read())["data"]["accessToken"]


def ok2(s): return 200 <= s < 300


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so: return so.recv(100).startswith(b"220")
    except OSError: return False


results = []
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:500]), flush=True)


CREATED_Q = []


def quote(tag, cid, approve):
    body = {"customerId": cid, "locationId": 2, "quotationDate": TODAY.isoformat(), "validUntil": (TODAY + dt.timedelta(days=15)).isoformat(),
            "subject": f"QA-QPUI {RUN} {tag}", "placeOfSupplyStateCode": "29",
            "lines": [{"lineType": 2, "serviceType": 5, "description": f"QA-QPUI {RUN} {tag}", "quantity": 1, "unitPrice": 2000, "discountAmount": 0, "taxPercent": 18}]}
    s, b = api("POST", "/quotations", body)
    if not ok2(s): raise SystemExit(f"create quotation: {s} {b}")
    qid = b["data"]["quotationId"]; CREATED_Q.append(qid)
    s, b = api("POST", f"/quotations/{qid}/send", {"sendEmail": True, "sendWhatsApp": False, "emailTo": CUST_EMAIL, "bccMe": False})
    if not ok2(s): raise SystemExit(f"send: {s} {b}")
    s, b = api("POST", f"/quotations/{qid}/record-decision", {"approved": approve, "mode": 2, "confirmedBy": "QA UI", "note": f"QA-QPUI {RUN}", "reasonCode": None if approve else "PRICE"})
    if not ok2(s): raise SystemExit(f"decide: {s} {b}")
    return qid, b["data"]["quotationNumber"], b["data"].get("invoiceId"), b["data"].get("invoiceNumber")


if not sink_up(): raise SystemExit("No SMTP sink answering on 127.0.0.1:2525.")
TOK["t"] = admin_token()
print(f"run {RUN}  booking day {DAY}", flush=True)

proc = ws = None
CID = None
BOOKING = None
try:
    s, b = api("POST", "/users", {"fullName": CUST_NAME, "email": CUST_EMAIL, "mobileNumber": "+919" + RUN + str(random.randint(1000, 9999)), "password": "Qa-Ui#2026" + RUN,
                                  "userType": 1, "roleIds": [int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))], "defaultLocationId": 2})
    if not ok2(s): raise SystemExit(f"create customer: {s} {b}")
    CID = b["data"]["userId"]
    QA, QA_NO, INV, INV_NO = quote("APPROVED", CID, True)
    QR, QR_NO, _, _ = quote("REJECTED", CID, False)
    TOTAL = float(one(f"SELECT TotalAmount FROM TRN_Invoice WHERE InvoiceId={INV}"))
    money = lambda v: "₹" + f"{v:,.2f}"

    port = 9300 + random.randint(0, 80)
    proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={port}", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-qp-prof-{RUN}",
                             "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    page = None
    for _ in range(60):
        try:
            page = next(t for t in json.loads(urllib.request.urlopen(f"http://127.0.0.1:{port}/json").read()) if t["type"] == "page"); break
        except Exception: time.sleep(0.3)
    if not page: raise SystemExit("Chrome debugging port did not answer")
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
    def click(sel, txt, exact=False):
        cond = f"e.textContent.trim()==={json.dumps(txt)}" if exact else f"e.textContent.trim().includes({json.dumps(txt)})"
        return js(f"(() => {{ const el=[...document.querySelectorAll({json.dumps(sel)})].find(e=>{cond} && !e.disabled && e.offsetParent!==null); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
    def text(): return (js("document.body.innerText") or "").replace("\u00a0", " ")
    def main_text(): return (js("(document.querySelector('main')||document.body).innerText") or "").replace("\u00a0", " ")
    def wait_for(cond, s=15):
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
    def search(placeholder_part, value):
        return js("(() => {" + SET + f"const i=[...document.querySelectorAll('input')].find(i=>(i.placeholder||'').includes({json.dumps(placeholder_part)}) && i.offsetParent!==null); if(!i) return false; setv(i,{json.dumps(value)}); return true}})()")
    def rows(): return js("[...document.querySelectorAll('table tbody tr')].filter(r=>r.offsetParent!==null).map(r=>r.innerText.replace(/\\u00a0/g,' '))") or []
    def toasts(): return js("[...document.querySelectorAll('.toast-container .toast')].map(t=>t.className+' :: '+t.innerText).join(' | ')") or ""
    def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
    def viewport(w): send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)
    def exceptions(): return [e["params"]["exceptionDetails"].get("exception", {}).get("description", "")[:200] for e in events if e.get("method") == "Runtime.exceptionThrown"]
    def buttons(): return js("[...document.querySelectorAll('button')].filter(b=>b.offsetParent!==null).map(b=>b.textContent.trim())") or []

    send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
    viewport(1366)
    go("/admin/login", 3)
    js("localStorage.clear(); sessionStorage.clear(); true"); go("/admin/login", 3)
    js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(ADMIN_EMAIL)});setv(document.querySelector('input[type=password]'),{json.dumps(ADMIN_PW)});document.querySelector('form button[type=submit]').click();return true}})()")
    drain(5)
    check("admin signed in", "/admin" in (js("location.pathname") or "") and "login" not in (js("location.pathname") or ""), js("location.pathname"))

    # ---- Payments: tabs, Quotations view ---------------------------------------------------------------------
    go("/admin/billing/payments", 5)
    tabs = js("[...document.querySelectorAll('.bf-tab')].map(t=>t.textContent.trim())") or []
    check("Payments: tabs 'All payments' and 'Quotations'", tabs[:2] == ["All payments", "Quotations"], tabs)
    heads = [h.strip() for h in (js("[...document.querySelectorAll('table thead th')].map(t=>t.textContent)") or [])]
    check("Payments: All payments is the default and keeps its columns", "Payment ID" in heads and "Quotation Number" in heads, heads)
    click(".bf-tab", "Quotations", exact=True)
    check("Quotations tab opens the panel with its summary", wait_for("!!document.querySelector('[data-testid=qp-panel]')", 10) and wait_text("Approved quotations", 10), text()[:400])
    wait_for("document.querySelectorAll('[data-testid=qp-panel] table thead th').length > 0", 10)
    heads = [h.strip() for h in (js("[...document.querySelectorAll('[data-testid=qp-panel] table thead th')].map(t=>t.textContent)") or [])]
    check("Quotations tab: columns Quotation, Customer, Invoice, Total, Paid, Outstanding, Payment Status",
          heads[:7] == ["Quotation", "Customer", "Invoice", "Total", "Paid", "Outstanding", "Payment Status"], heads)
    search("Quotation, reference", QA_NO); drain(3)
    rr = rows()
    check("Quotations tab: the approved quotation, its invoice, total, outstanding and Due",
          len(rr) == 1 and QA_NO in rr[0] and INV_NO in rr[0] and money(TOTAL) in rr[0] and "Due" in rr[0] and "+ Record payment" in rr[0], rr)
    shot("qp-01-quotations-tab")
    search("Quotation, reference", QR_NO); drain(3)
    check("Quotations tab: the rejected quotation is not found", "No approved quotations found" in text() and not any(QR_NO in r for r in rows()), rows())

    # ---- Record payment from the Quotations tab ---------------------------------------------------------------
    search("Quotation, reference", QA_NO); drain(3)
    check("Record payment: button there", click("[data-testid=qp-panel] button", "+ Record payment")); drain(2)
    check("Record payment: the usual dialog on the quotation's invoice", wait_text(f"Record payment — {INV_NO}", 10), text()[:300])
    fill("Amount received", "1000", "document.querySelector('.modal')||document"); drain(0.5)
    click(".modal button", "Save payment", exact=True)
    wait_for("!document.querySelector('.modal')", 15); drain(3)
    rr = rows()
    check("after a part payment: Partially Paid, paid ₹1,000.00, outstanding = total - 1,000, 1 payment",
          len(rr) == 1 and "Partially Paid" in rr[0] and money(1000) in rr[0] and money(TOTAL - 1000) in rr[0] and "1 payment" in rr[0], rr)
    check("DB: one payment and one receipt on the invoice", one(f"SELECT COUNT(*) FROM TRN_Payment WHERE InvoiceId={INV} AND Status=2") == "1"
          and one(f"SELECT COUNT(*) FROM TRN_InvoicePayment WHERE InvoiceId={INV} AND ReversalOfId IS NULL") == "1")
    shot("qp-02-partially-paid")
    viewport(390); drain(1.5)
    check("Quotations tab 390: no page-wide horizontal scroll", no_hscroll())
    shot("qp-03-quotations-390")
    viewport(1366); drain(1)
    click("[data-testid=qp-panel] button", "Payments", exact=True); drain(4)
    rr = rows()
    check("'Payments' on the row opens All payments filtered by the quotation: the payment with the quotation and invoice number",
          js("document.querySelector('.bf-tab[aria-selected=true]')?.textContent") == "All payments" and any(QA_NO in r and INV_NO in r and "Partially Paid" in r for r in rr), rr[:2])

    # ---- Quotation page: Open invoice, Payment status ---------------------------------------------------------
    go(f"/admin/billing/quotations/{QA}", 5)
    check("quotation page: 'Invoiced as' with Open invoice and Payment status", f"Invoiced as {INV_NO}" in text()
          and js("!!document.querySelector('[data-testid=qtn-open-invoice]') && !!document.querySelector('[data-testid=qtn-payment-status]')"), text()[:300])
    js("document.querySelector('[data-testid=qtn-open-invoice]').click()"); drain(4)
    check("Open invoice opens that invoice", js("location.pathname") == f"/admin/billing/invoices/{INV}" and INV_NO in text(), js("location.pathname"))
    go(f"/admin/billing/quotations/{QA}", 5)
    js("document.querySelector('[data-testid=qtn-payment-status]').click()"); drain(5)
    rr = rows()
    check("Payment status opens Payments on the Quotations tab for this quotation",
          js("location.pathname") == "/admin/billing/payments" and js("!!document.querySelector('[data-testid=qp-panel]')") and len(rr) == 1 and QA_NO in rr[0], (js("location.href"), rr))
    go(f"/admin/billing/quotations/{QR}", 5)
    check("rejected quotation page: no Open invoice / Payment status", not js("!!document.querySelector('[data-testid=qtn-open-invoice]')"))
    viewport(390); drain(1)
    go(f"/admin/billing/quotations/{QA}", 5)
    check("quotation page 390: no page-wide horizontal scroll", no_hscroll())
    viewport(1366); drain(1)

    # ---- Admin Bookings: New booking without invoice details ------------------------------------------------
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, PayLaterAllowed=0 WHERE UserId={CID}")
    go("/admin/bookings", 5)
    click("button", "New booking"); drain(3)
    t = main_text()
    check("New booking: header has no invoice wording", wait_text("Back to bookings", 8) and "invoice" not in t.lower(), t[:400])
    for _ in range(4):
        click("button.choice-card", "Meeting Room"); drain(0.8)
        if wait_text("2 · Choose your date and time", 4): break
    fill("Find customer", CUST_NAME)
    wait_for(f"[...document.querySelectorAll('select option')].some(o=>o.value==='{CID}')", 10)
    fill("Customer", str(CID)); drain(1); fill("Location", "2"); drain(0.5)
    fill("From date", DAY.isoformat()); fill("To date", DAY.isoformat()); drain(2.5)
    wait_for("document.querySelectorAll('.slot-chip').length > 0", 10)
    slot = js("(() => { const b=[...document.querySelectorAll('.slot-chip')].find(b=>!b.disabled); if(!b) return null; b.click(); return b.textContent.trim() })()")
    drain(0.5)
    wait_for("[...document.querySelectorAll('button')].some(b=>b.textContent.includes('Find available workspaces') && !b.disabled)", 8)
    click("button", "Find available workspaces"); wait_text("Change dates", 15); drain(1)
    picked = js("(() => { const b=[...document.querySelectorAll('.choice-grid button.choice-card')].find(b=>!b.disabled); if(!b) return null; b.click(); return b.innerText })()")
    check("New booking: a slot and a meeting room picked", bool(slot) and bool(picked) and wait_text("Review & confirm", 12), (slot, main_text()[:500]))
    t = main_text()
    check("Review & confirm: nothing collected, billed through the quotation, no invoice wording", "Nothing is collected here" in t and "billed through the customer's quotation" in t and "invoice" not in t.lower(), t[-700:])
    shot("qp-04-admin-review")
    click("button", "Confirm booking")
    ok = wait_text("Booking confirmed", 15)
    t = main_text()
    BOOKING = one(f"SELECT TOP 1 BookingId FROM TRN_Booking WHERE CustomerId={CID} ORDER BY BookingId DESC")
    n_inv = one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE BookingId={BOOKING or 0}")
    n_pay = one(f"SELECT COUNT(*) FROM TRN_Payment WHERE BookingId={BOOKING or 0}")
    check("Booking confirmed: 'billed through the customer's quotation', no invoice button or wording, no amount due",
          ok and "billed through the customer's quotation" in t and "invoice" not in t.lower() and "is due from the customer" not in t
          and not any("Invoice" in x for x in buttons()), (t[:600], buttons()))
    check("the staff booking has no invoice and no payment; it is Confirmed and not pay later (user requirement)",
          n_inv == "0" and n_pay == "0" and one(f"SELECT CONCAT(Status,'-',CAST(IsPayLater AS int)) FROM TRN_Booking WHERE BookingId={BOOKING}") == "2-0", (n_inv, n_pay))
    shot("qp-05-admin-confirmed")
    viewport(390); drain(1)
    check("New booking done 390: no page-wide horizontal scroll", no_hscroll())
    viewport(1366); drain(1)
    go("/admin/bookings", 5)
    bno = one(f"SELECT BookingNumber FROM TRN_Booking WHERE BookingId={BOOKING}")
    js("(() => {" + SET + f"const i=[...document.querySelectorAll('input')].find(i=>(i.placeholder||'').includes('Booking no')); setv(i,{json.dumps(bno)}); return true}})()"); drain(3)
    click("table button", "Detail"); drain(1.5)
    check("Bookings detail: no invoice details", wait_text(bno, 5) and "invoice" not in (js("document.querySelector('.modal')?.innerText") or "").lower(), js("document.querySelector('.modal')?.innerText"))
    click(".modal button", "Cancel booking"); drain(1.5)
    mt = js("[...document.querySelectorAll('.modal')].map(m=>m.innerText).join(' ')") or ""
    check("Bookings cancel dialog: no invoice wording", "Cancel " + bno in mt and "invoice" not in mt.lower(), mt[:600])
    check("no script errors on the pages visited", not exceptions(), exceptions()[:3])
finally:
    left = []
    try:
        if BOOKING and one(f"SELECT Status FROM TRN_Booking WHERE BookingId={BOOKING}") in ("1", "2"):
            cs, _ = api("POST", f"/bookings/{BOOKING}/cancel", {"reason": f"QA-QPUI {RUN} cleanup"})
            if not ok2(cs): left.append(f"booking {BOOKING} not cancelled ({cs})")
        for r in sql(f"SELECT InvoiceId FROM TRN_Invoice WHERE QuotationId IN ({','.join(map(str, CREATED_Q)) or 0}) AND IsDeleted=0 AND Status NOT IN (4,5)"):
            s, b = api("GET", f"/invoices/{r[0]}/payments")
            for p in (b.get("data") or []):
                if p.get("paymentId") and not p.get("isReversed") and not p.get("reversalOfId") and p.get("amount", 0) > 0:
                    rs, _ = api("POST", f"/payments/{p['paymentId']}/reverse", {"reason": f"QA-QPUI {RUN} cleanup"})
                    if not ok2(rs): left.append(f"payment {p['paymentId']} not reversed ({rs})")
            cs, _ = api("POST", f"/invoices/{r[0]}/cancel", {"reason": f"QA-QPUI {RUN} cleanup"})
            if not ok2(cs): left.append(f"invoice {r[0]} not cancelled ({cs})")
        if CID:
            ds, _ = api("DELETE", f"/users/{CID}")
            if not ok2(ds): left.append(f"user {CID} not deleted ({ds})")
        for r in sql(f"SELECT QuotationId, QuotationNumber, Status FROM TRN_Quotation WHERE Subject LIKE 'QA-QPUI {RUN} %' AND Status<>7"):
            left.append(f"quotation {r[0]} {r[1]} status {r[2]} (decided quotations stay)")
    except BaseException as e:
        left.append(f"clean-up stopped: {e}")
    try:
        if ws: ws.close()
    finally:
        if proc: proc.kill()
    print("\nTest data left in Dev (run " + RUN + "):\n  " + ("\n  ".join(left) if left else "none"))

print(f"\n{sum(results)}/{len(results)} passed, {len(results) - sum(results)} failed")
sys.exit(0 if results and all(results) else 1)
