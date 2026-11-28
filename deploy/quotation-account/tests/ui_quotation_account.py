"""quotation-account kit (27 Nov 2026): headless Chrome checks, LOCAL stack only.
http://localhost:8080 -> API :81 (D:\\Regalis\\local-dev\\api), Dev DB RegalisWorkSpace_Dev; mail to the SMTP sink on
127.0.0.1:2525 (stops first if nothing answers there).

Admin signs in. A lead quotation sent to a STAFF email is marked approved on its page: a warning toast and a
message on the page say why no invoice was raised; Convert to invoice opens a dialog with the email, which is
corrected to the customer's own (qa-ui-<run>@example.test) -> account created, invoice raised, page shows it.
Billing > Invoices: the placeholder names the quotation, a partial quotation number finds the invoice, the row
shows the quotation number. 1366 px and 390 px (no page-wide horizontal scroll on the quotation page / dialog).

Creates quotations "QA-UI <run> ...", customer qa-ui-<run>@example.test and its invoice; removes them through the
API at the end (invoice cancelled, customer deleted; approved quotation numbers stay).
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
CUST_EMAIL = f"qa-ui-{RUN}@example.test"


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
            try: return e.code, json.loads(e.read() or b"{}")
            except Exception: return e.code, {}
    raise SystemExit("rate limited")


def ok2(s): return 200 <= s < 300


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so: return so.recv(100).startswith(b"220")
    except OSError: return False


results = []
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:400]), flush=True)


CREATED_Q = []


def lead(tag, email_to):
    body = {"customerId": None, "customerName": f"QA UI Lead {RUN}", "billingAddress": "1 UI Street, Bengaluru", "locationId": 2,
            "quotationDate": TODAY.isoformat(), "validUntil": (TODAY + dt.timedelta(days=15)).isoformat(), "subject": f"QA-UI {RUN} {tag}",
            "placeOfSupplyStateCode": "29",
            "lines": [{"lineType": 2, "serviceType": 5, "description": f"QA-UI {RUN} {tag}", "quantity": 1, "unitPrice": 1500, "discountAmount": 0, "taxPercent": 18}]}
    s, b = api("POST", "/quotations", body)
    if not ok2(s): raise SystemExit(f"create quotation: {s} {b}")
    qid = b["data"]["quotationId"]; CREATED_Q.append(qid)
    s, b = api("POST", f"/quotations/{qid}/send", {"sendEmail": True, "sendWhatsApp": False, "emailTo": email_to, "bccMe": False})
    if not ok2(s): raise SystemExit(f"send: {s} {b}")
    return qid, one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={qid}")


if not sink_up(): raise SystemExit("No SMTP sink answering on 127.0.0.1:2525.")
s, b = api("POST", "/auth/login", {"email": ADMIN_EMAIL, "password": ADMIN_PW, "portal": "admin"})
TOK["t"] = b["data"]["accessToken"]
ADMIN_NAME = one(f"SELECT FullName FROM MST_User WHERE Email='{ADMIN_EMAIL}' AND IsDeleted=0")
print(f"run {RUN}", flush=True)

proc = ws = None
try:
    QS, QS_NO = lead("STAFF", ADMIN_EMAIL)
    port = 9300 + random.randint(0, 80)
    proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={port}", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-qa-prof-{RUN}",
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
    def go(p, w=4): send("Page.navigate", url=BASE + p); drain(w)
    def shot(n): open(os.path.join(SHOTS, n + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png")["data"]))
    def click(sel, txt, exact=False):
        cond = f"e.textContent.trim()==={json.dumps(txt)}" if exact else f"e.textContent.trim().includes({json.dumps(txt)})"
        return js(f"(() => {{ const el=[...document.querySelectorAll({json.dumps(sel)})].find(e=>{cond} && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
    def text(): return (js("document.body.innerText") or "").replace("\u00a0", " ")
    def wait_for(cond, s=15):
        end = time.time() + s
        while time.time() < end:
            if js(cond): return True
            drain(0.4)
        return bool(js(cond))
    SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:HTMLInputElement.prototype;"
           "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}))};")
    def toasts(): return js("[...document.querySelectorAll('.toast-container .toast')].map(t=>t.className+' :: '+t.innerText).join(' | ')") or ""
    def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
    def viewport(w): send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)

    send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
    viewport(1366)
    js("localStorage.clear(); sessionStorage.clear(); true"); go("/admin/login", 3)
    js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(ADMIN_EMAIL)});setv(document.querySelector('input[type=password]'),{json.dumps(ADMIN_PW)});document.querySelector('form button[type=submit]').click();return true}})()")
    drain(5)
    check("admin signed in", "/admin" in (js("location.pathname") or "") and "login" not in (js("location.pathname") or ""), js("location.pathname"))

    # ---- Mark approved with a staff email -------------------------------------------------------------
    go(f"/admin/billing/quotations/{QS}", 5)
    check("quotation page opens", QS_NO in text(), text()[:200])
    check("Mark approved: button there", click("button", "Mark approved", exact=True)); drain(1.5)
    click(".modal button", "Record approval", exact=True)
    wait_for("!!document.querySelector('.toast-container .toast')", 20)
    t = toasts()
    shot("qa-01-approved-staff-email")
    check("Mark approved: a warning toast (not a success) names the staff account", "toast--warning" in t and "staff account" in t and "Recorded as approved" in t, t)
    wait_for("!!document.querySelector('[data-testid=qtn-no-invoice]')", 10)
    msg = js("document.querySelector('[data-testid=qtn-no-invoice]')?.innerText || ''") or ""
    check("page: message says why no invoice and what to do", "staff account" in msg and "Convert to invoice" in msg and (ADMIN_NAME or "") in msg, msg)
    check("page: no 'Invoiced as'", "Invoiced as" not in text())
    check("page 1366: no page-wide horizontal scroll", no_hscroll())

    # ---- Convert to invoice dialog -----------------------------------------------------------------------
    check("Convert to invoice: button there", click(".qtn-banner button", "Convert to invoice", exact=True)); drain(1.5)
    check("dialog opens with the quotation's email pre-filled",
          bool(js("!!document.querySelector('[data-testid=qtn-raise-invoice]')")) and ADMIN_EMAIL in (js("[...document.querySelectorAll('.modal input')].map(i=>i.value).join(' ')") or ""),
          js("document.querySelector('.modal')?.innerText?.slice(0,300)"))
    click(".modal button", "Raise invoice", exact=True)
    wait_for("!!document.querySelector('.modal [role=alert]')", 15)
    err = js("document.querySelector('.modal [role=alert]')?.innerText || ''") or ""
    check("dialog: unchanged staff email -> the refusal is shown in the dialog", "staff account" in err, err)
    viewport(390); drain(1)
    shot("qa-02-raise-dialog-390")
    check("dialog 390: no page-wide horizontal scroll", no_hscroll())
    viewport(1366); drain(1)
    mobile = "9" + str(random.randint(100000000, 999999999))
    js("(() => {" + SET + f"const ins=[...document.querySelectorAll('.modal input')]; setv(ins[0],{json.dumps(CUST_EMAIL)}); setv(ins[1],{json.dumps(mobile)}); return true}})()")
    drain(0.5)
    click(".modal button", "Raise invoice", exact=True)
    wait_for("!document.querySelector('[data-testid=qtn-raise-invoice]')", 20)
    drain(3)
    t = toasts()
    inv_no = one(f"SELECT i.InvoiceNumber FROM TRN_Invoice i WHERE i.QuotationId={QS} AND i.IsDeleted=0")
    shot("qa-03-invoiced")
    check("dialog: corrected email -> success toast names the new account and the invoice", "toast--success" in t and "created" in t and bool(inv_no) and inv_no in t, t)
    check("page: now 'Invoiced as' the invoice, message gone", bool(inv_no) and f"Invoiced as {inv_no}" in text() and not js("!!document.querySelector('[data-testid=qtn-no-invoice]')"), text()[:400])
    check("DB: one customer account with the corrected email", one(f"SELECT COUNT(*) FROM MST_User WHERE Email='{CUST_EMAIL}' AND UserType=1 AND IsDeleted=0") == "1")

    # ---- Invoices search by quotation number ----------------------------------------------------------
    go("/admin/billing/invoices", 5)
    ph = js("[...document.querySelectorAll('input')].map(i=>i.placeholder).find(p=>p && p.toLowerCase().includes('invoice')) || ''") or ""
    check("Invoices: search placeholder names the quotation", "quotation" in ph.lower(), ph)
    js("(() => {" + SET + "const i=[...document.querySelectorAll('input')].find(i=>(i.placeholder||'').toLowerCase().includes('quotation')); setv(i," + json.dumps(QS_NO.split("/", 1)[1]) + "); return true})()")
    drain(3)
    rows = js("[...document.querySelectorAll('table tbody tr')].map(r=>r.innerText)") or []
    shot("qa-04-invoices-search")
    check("Invoices: partial quotation number finds the invoice, row shows the quotation number",
          any(inv_no in r and QS_NO in r for r in rows), rows[:3])
    heads = [h.strip() for h in (js("[...document.querySelectorAll('table thead th')].map(t=>t.textContent)") or [])]
    check("Invoices: column headed 'Quotation / source'", "Quotation / source" in heads, heads)
    viewport(390); drain(1.5)
    check("Invoices 390: no page-wide horizontal scroll", no_hscroll())
    viewport(1366)
finally:
    left = []
    try:
        for r in sql(f"SELECT InvoiceId FROM TRN_Invoice WHERE QuotationId IN ({','.join(map(str, CREATED_Q)) or 0}) AND IsDeleted=0 AND Status<>4"):
            cs, _ = api("POST", f"/invoices/{r[0]}/cancel", {"reason": f"QA-UI {RUN} cleanup"})
            if not ok2(cs): left.append(f"invoice {r[0]} not cancelled ({cs})")
        for qid in CREATED_Q:
            if one(f"SELECT Status FROM TRN_Quotation WHERE QuotationId={qid}") in ("1", "2", "3"):
                api("POST", f"/quotations/{qid}/cancel", {"reason": f"QA-UI {RUN} cleanup"})
        for r in sql(f"SELECT UserId FROM MST_User WHERE Email='{CUST_EMAIL}' AND IsDeleted=0"):
            ds, _ = api("DELETE", f"/users/{r[0]}")
            if not ok2(ds): left.append(f"user {r[0]} not deleted ({ds})")
        for r in sql(f"SELECT QuotationId, QuotationNumber, Status FROM TRN_Quotation WHERE Subject LIKE 'QA-UI {RUN} %' AND Status<>7"):
            left.append(f"quotation {r[0]} {r[1]} status {r[2]}")
    except BaseException as e:
        left.append(f"clean-up stopped: {e}")
    try:
        if ws: ws.close()
    finally:
        if proc: proc.kill()
    print("\nTest data left in Dev (run " + RUN + "):\n  " + ("\n  ".join(left) if left else "none"))

print(f"\n{sum(results)}/{len(results)} passed, {len(results) - sum(results)} failed")
sys.exit(0 if results and all(results) else 1)
