"""Headless Chrome checks for quotation -> invoice -> payment (27 Nov 2026), LOCAL stack only.
http://localhost:8080 -> API :81 (D:\\Regalis\\local-dev\\api), Dev DB RegalisWorkSpace_Dev, mail to the SMTP
sink on 127.0.0.1:2525 (the script stops before creating anything if nothing answers there).

Written to the EXPECTED behaviour of the quotation/invoice brief; against a build without the fix most
checks fail, which is the reproduction. Desktop 1366 px, plus a 390 px layout check of the two lists.

Admin signs in. Billing > Quotations: a test quotation is sent from its page with the Send popup ->
either a real success (toast, status Sent, a mail in the sink to the registered address) or a real error
in the popup; never a success with nothing sent. "Mark approved" -> the page names the invoice raised.
Billing > Invoices: the Status (payment status) column reads Due / Partially Paid / Paid with the paid
and outstanding amounts for the test invoices (4,000 of 10,000 then 10,000). Billing > Payments: the
ten columns of the brief, the test payment, and no row for the rejected quotation.

Creates (customer QI_CUSTOMER_EMAIL, default kausiraj21@gmail.com): quotations "QI-UI <run> ...",
their invoices, manual payments QIUI<run>-*. Removes through the API at the end (also after a failure):
payments reversed, invoices cancelled, open quotations cancelled; prints what stays.
Screenshots: SHOTS_DIR (default deploy\\quotation-invoice\\evidence\\before).
Env: PYLIB (websocket-client), REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD, QI_CUSTOMER_EMAIL / QI_CUSTOMER_PASSWORD, QI_MAIL_DIR."""
import base64, datetime as dt, json, os, random, socket, subprocess, sys, time, traceback, urllib.request, urllib.error
from decimal import Decimal as D
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket
sys.stdout.reconfigure(encoding="utf-8")

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.environ.get("SHOTS_DIR", os.path.join(HERE, "..", "evidence", "before")); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
API = "http://127.0.0.1:81/api"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
MAIL_DIR = os.environ.get("QI_MAIL_DIR", r"C:\Users\santh\AppData\Local\Temp\claude\4d6de0b18aaba066\e3d5794c-1d18-45a0-b861-5fc7ebb6dbc6\scratchpad\sink\mail")
CUST_EMAIL = os.environ.get("QI_CUSTOMER_EMAIL", "kausiraj21@gmail.com")
RUN = str(random.randint(10000, 99999))
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36"
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
TODAY = dt.datetime.now(IST).date()

# ---- ONE PLACE for routes, labels and selectors ------------------------------------------------------
EP = {"quotations": "/quotations", "quotation": "/quotations/{id}", "q_send": "/quotations/{id}/send", "q_decision": "/quotations/{id}/record-decision",
      "q_cancel": "/quotations/{id}/cancel", "convert": "/invoices/from-quotation/{id}", "invoice": "/invoices/{id}", "inv_cancel": "/invoices/{id}/cancel",
      "inv_payments": "/invoices/{id}/payments", "pay_reverse": "/payments/{id}/reverse"}
PAGE = {"quotation": "/admin/billing/quotations/{id}", "invoices": "/admin/billing/invoices", "payments": "/admin/billing/payments"}
INVOICE_STATUS_HEADERS = ("Payment Status", "Status")          # the column that must carry Due / Partially Paid / Paid
PAYMENT_COLUMNS = ["Payment ID", "Customer Name", "Quotation Number", "Invoice Number", "Payment Date", "Payment Method",
                   "Total Invoice Amount", "Amount Paid", "Outstanding", "Payment Status"]
Q_STATUS = {"Draft": 1, "Sent": 2, "Approved": 4, "Rejected": 5}


def ep(n, **kw): return EP[n].format(**kw)


def rs(x):
    """Rs amount as the portal prints it (en-IN grouping, 2 decimals)."""
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


def api(method, path, who=None, body=None):
    for attempt in range(10):
        req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
        req.add_header("Content-Type", "application/json"); req.add_header("User-Agent", UA)
        if who: req.add_header("Authorization", "Bearer " + CREDS[who]["tok"])
        try:
            with urllib.request.urlopen(req, timeout=60) as r:
                raw = r.read()
                try: return r.status, json.loads(raw or b"{}")
                except Exception: return r.status, {}
        except urllib.error.HTTPError as e:
            if e.code == 429: time.sleep(10 + 5 * attempt); continue
            if e.code == 401 and who: CREDS[who]["tok"] = raw_login(*CREDS[who]["login"]); continue
            try: return e.code, json.loads(e.read() or b"{}")
            except Exception: return e.code, {}
    raise SystemExit("rate limited")


def raw_login(em, pw, portal):
    for attempt in range(8):
        s, b = api("POST", "/auth/login", body={"email": em, "password": pw, "portal": portal})
        if s == 200: return b["data"]["accessToken"]
        if s == 429: time.sleep(15 + 5 * attempt); continue
        raise SystemExit(f"login {em}: {s} {b}")
    raise SystemExit("login rate limited")


def ok2(s): return 200 <= s < 300


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so:
            return so.recv(100).startswith(b"220")
    except OSError:
        return False


def mail_files(): return set(os.listdir(MAIL_DIR)) if os.path.isdir(MAIL_DIR) else set()


def mentions(fn, needle):
    """True when the decoded subject or a text part of the mail contains needle."""
    import email, email.policy
    raw = open(os.path.join(MAIL_DIR, fn), "rb").read()
    msg = email.message_from_bytes(raw.split(b"\n", 1)[1] if raw.startswith(b"X-Sink-Rcpt:") else raw, policy=email.policy.default)
    if needle in str(msg.get("Subject", "")): return True
    for part in msg.walk():
        if not part.is_multipart() and part.get_content_type().startswith("text/"):
            if needle in (part.get_payload(decode=True) or b"").decode(part.get_content_charset() or "utf-8", errors="replace"): return True
    return False


def rcpts(fn):
    first = open(os.path.join(MAIL_DIR, fn), "rb").readline().decode(errors="replace")
    return [x.strip().strip("<>").lower() for x in first.partition(":")[2].split(",")] if first.startswith("X-Sink-Rcpt:") else []


results = []
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:400]), flush=True)


CREATED_Q, CREATED_INV = [], []


def make_quote(tag, lines, discount=None):
    body = {"customerId": CUST_ID, "locationId": 2, "quotationDate": TODAY.isoformat(), "validUntil": (TODAY + dt.timedelta(days=15)).isoformat(),
            "subject": f"QI-UI {RUN} {tag}", "placeOfSupplyStateCode": "29", "discountType": 2 if discount else None, "discountValue": float(discount) if discount else None,
            "lines": [{"lineType": 2, "serviceType": 5, "description": f"QI-UI {RUN} {d}", "quantity": q, "unitPrice": p, "discountAmount": 0, "taxPercent": 18}
                      for d, q, p in lines]}
    s, b = api("POST", ep("quotations"), "admin", body)
    if not ok2(s): raise SystemExit(f"create quotation {tag}: {s} {b}")
    CREATED_Q.append(b["data"]["quotationId"])
    return b["data"]["quotationId"], b["data"]["quotationNumber"]


def get_q(qid): return api("GET", ep("quotation", id=qid), "admin")[1].get("data") or {}


def send_api(qid):
    return api("POST", ep("q_send", id=qid), "admin", {"sendEmail": True, "sendWhatsApp": False, "emailTo": REG_EMAIL, "bccMe": False})


def decide_api(qid, approved):
    return api("POST", ep("q_decision", id=qid), "admin", {"approved": approved, "mode": 2, "confirmedBy": CUST_NAME, "note": f"QI-UI {RUN}",
                                                           "reasonCode": None if approved else "PRICE"})


def invoice_of(qid):
    iid = get_q(qid).get("invoiceId")
    if not iid:
        s, b = api("POST", ep("convert", id=qid), "admin")
        iid = (b.get("data") or {}).get("invoiceId") if ok2(s) else None
        print(f"INFO approval raised no invoice for quotation {qid}; 'Convert to invoice' -> {s} invoice {iid}", flush=True)
    if iid and iid not in CREATED_INV: CREATED_INV.append(iid)
    return iid


def pay_api(iid, amount, ref):
    return api("POST", ep("inv_payments", id=iid), "admin", {"amount": amount, "paidOn": TODAY.isoformat(), "mode": 2, "reference": ref, "note": f"QI-UI {RUN}"})


def cleanup():
    left = []
    for iid in CREATED_INV:
        s, b = api("GET", ep("inv_payments", id=iid), "admin")
        for p in (b.get("data") or []):
            if p.get("paymentId") and not p.get("isReversed") and p.get("canReverse", True):
                rs_, _ = api("POST", ep("pay_reverse", id=p["paymentId"]), "admin", {"reason": f"QI-UI {RUN} cleanup"})
                if not ok2(rs_): left.append(f"payment {p['paymentId']} not reversed ({rs_})")
        cs, _ = api("POST", ep("inv_cancel", id=iid), "admin", {"reason": f"QI-UI {RUN} cleanup"})
        if not ok2(cs): left.append(f"invoice {iid} not cancelled ({cs})")
    for qid in CREATED_Q:
        st = get_q(qid).get("status")
        if st == Q_STATUS["Draft"] and ok2(api("DELETE", ep("quotation", id=qid), "admin")[0]): continue
        if st in (1, 2, 3): api("POST", ep("q_cancel", id=qid), "admin", {"reason": f"QI-UI {RUN} cleanup"})
    for r in sql(f"SELECT q.QuotationId, q.QuotationNumber, q.Status, ISNULL(i.InvoiceNumber,'-'), ISNULL(CAST(i.Status AS varchar),'-') FROM TRN_Quotation q"
                 f" LEFT JOIN TRN_Invoice i ON i.QuotationId=q.QuotationId WHERE q.Subject LIKE 'QI-UI {RUN} %' AND q.IsDeleted=0"):
        left.append(f"quotation {r[0]} {r[1]} status {r[2]}, invoice {r[3]} status {r[4]}")
    print("\nTest data left in Dev (run " + RUN + "):\n  " + ("\n  ".join(left) if left else "none"))


# ---- fixtures ---------------------------------------------------------------------------------------------
if not os.environ.get("QI_CUSTOMER_PASSWORD"): raise SystemExit("Set QI_CUSTOMER_PASSWORD.")
if not sink_up(): raise SystemExit("No SMTP sink answering on 127.0.0.1:2525 - not running (sending would email for real).")
CREDS["admin"] = {"login": (os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")}
CREDS["admin"]["tok"] = raw_login(*CREDS["admin"]["login"])
r = sql(f"SELECT UserId, Email, FullName FROM MST_User WHERE Email='{CUST_EMAIL}' AND IsDeleted=0")
CUST_ID, REG_EMAIL, CUST_NAME = int(r[0][0]), r[0][1].strip().lower(), r[0][2]
print(f"run {RUN}  customer {CUST_ID}", flush=True)

proc = ws = None
try:
    QU, QU_NO = make_quote("A", [("Cabin month", 2, 4000.00), ("Setup", 1, 1474.58)], discount=1000)   # Rs 10,000.00 incl. GST
    QDUE, QDUE_NO = make_quote("Due", [("Desk month", 1, 2000.00)])
    QREJ, QREJ_NO = make_quote("Rejected", [("Meeting room", 1, 1500.00)])
    for q in (QDUE, QREJ):
        s, b = send_api(q)
        if not ok2(s): raise SystemExit(f"send fixture {q}: {s} {b}")
    if not ok2(decide_api(QDUE, True)[0]) or not ok2(decide_api(QREJ, False)[0]): raise SystemExit("decision fixtures failed")
    INV_DUE = invoice_of(QDUE)
    INV_DUE_NO = (api("GET", ep("invoice", id=INV_DUE), "admin")[1].get("data") or {}).get("invoiceNumber") if INV_DUE else None

    # ---- browser ------------------------------------------------------------------------------------------
    port = 9300 + random.randint(0, 80)
    proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={port}", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-qi-prof-{RUN}",
                             "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
    for _ in range(60):
        try:
            page = next(t for t in json.loads(urllib.request.urlopen(f"http://127.0.0.1:{port}/json").read()) if t["type"] == "page"); break
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
    def click(sel, txt, root="document", exact=False):
        cond = f"e.textContent.trim()==={json.dumps(txt)}" if exact else f"e.textContent.trim().includes({json.dumps(txt)})"
        return js(f"(() => {{ const el=[...{root}.querySelectorAll({json.dumps(sel)})].find(e=>{cond} && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
    def text(): return (js("document.body.innerText") or "").replace("\u00a0", " ")
    def wait_for(cond, s=12):
        end = time.time() + s
        while time.time() < end:
            if js(cond): return True
            drain(0.4)
        return bool(js(cond))
    SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:el.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype;"
           "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event(el.tagName==='SELECT'?'change':'input',{bubbles:true}))};")
    def login(em, pw, path):
        js("localStorage.clear(); sessionStorage.clear(); true"); go(path, 3)
        js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(em)});setv(document.querySelector('input[type=password]'),{json.dumps(pw)});document.querySelector('form button[type=submit]').click();return true}})()")
        drain(5)
    def search(term):
        js("(() => {" + SET + "const i=[...document.querySelectorAll('input')].find(i=>(i.placeholder||'').length>0 && i.type!=='date'); if(!i) return false; setv(i," + json.dumps(term) + "); return true})()")
        drain(3)
    def headers(): return [h.strip() for h in (js("[...document.querySelectorAll('table thead th')].map(t=>t.textContent)") or [])]
    def row_cells(needle):
        """{header: cell text} of the first body row containing needle."""
        return js("(() => { const hs=[...document.querySelectorAll('table thead th')].map(t=>t.textContent.trim()); const tr=[...document.querySelectorAll('table tbody tr')]"
                  f".find(r=>r.innerText.includes({json.dumps(needle)})); if(!tr) return null; const o={{}}; [...tr.children].forEach((td,i)=>o[hs[i]||('#'+i)]=td.innerText.replace(/\\u00a0/g,' ').trim()); return o }})()")
    def status_cell(cells):
        for h in INVOICE_STATUS_HEADERS:
            if cells and h in cells: return h, cells[h]
        return None, None
    def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
    def viewport(w): send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)

    send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
    send("Network.setUserAgentOverride", userAgent=UA)
    viewport(1366)
    login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "/admin/login")
    check("admin signed in (admin shell visible)", "/admin" in (js("location.pathname") or "") and "login" not in (js("location.pathname") or ""), js("location.pathname"))

    # ---- Quotation screen: Send --------------------------------------------------------------------------
    go(PAGE["quotation"].format(id=QU), 5)
    check("quotation page opens on the test quotation", QU_NO in text(), text()[:300])
    shot("01-quotation-draft")
    before = mail_files()
    click("button", "Send", exact=True); drain(1.5)
    check("Send popup opens with To pre-filled with the registered email",
          bool(js("!!document.querySelector('.modal')")) and REG_EMAIL in (js("[...document.querySelectorAll('.modal input')].map(i=>i.value).join(' ')") or "").lower(),
          js("document.querySelector('.modal')?.innerText?.slice(0,300)"))
    js("(() => { const l=[...document.querySelectorAll('.modal label')].find(l=>l.textContent.trim().startsWith('WhatsApp')); const i=l&&(l.querySelector('input')||document.getElementById(l.htmlFor)); if(i&&i.checked) i.click(); return true })()")
    drain(0.3)
    click(".modal button", "Send", exact=True)
    wait_for("!!document.querySelector('.toast-container .toast__message') || !!document.querySelector('.modal .form-error')", 25)
    toast = (js("[...document.querySelectorAll('.toast-container .toast__message')].map(t=>t.innerText).join(' | ')") or "")
    err = (js("document.querySelector('.modal .form-error')?.innerText || ''") or "")
    shot("02-quotation-send-result")
    time.sleep(3)
    new = [fn for fn in sorted(mail_files() - before) if mentions(fn, QU_NO)]   # other jobs share the sink
    to_reg = [fn for fn in new if REG_EMAIL in rcpts(fn)]
    st = get_q(QU).get("status")
    real_success = "sent" in toast.lower() and st == Q_STATUS["Sent"] and len(to_reg) == 1
    real_error = bool(err) and st == Q_STATUS["Draft"] and not to_reg
    check("Send shows a real outcome: success toast with status Sent and one mail to the registered email, or an error in the popup with nothing sent",
          real_success or real_error, f"toast={toast!r} error={err!r} status={st} new mails={[(fn, rcpts(fn)) for fn in new]}")
    check("Send did not report success while nothing was delivered", not ("sent" in toast.lower() and not to_reg), f"toast={toast!r} mails to registered={to_reg}")

    # ---- Quotation screen: Mark approved -> invoice named ------------------------------------------------
    go(PAGE["quotation"].format(id=QU), 4)
    if click("button", "Mark approved", exact=True):
        drain(1.2); click(".modal button", "Record approval", exact=True); drain(4)
    go(PAGE["quotation"].format(id=QU), 4)
    q = get_q(QU)
    shot("03-quotation-approved")
    check("Mark approved: quotation shows Approved", q.get("status") == Q_STATUS["Approved"] and "Approved" in text(), q.get("status"))
    check("Mark approved: the page names the invoice raised for it (REG/INV/...)", bool(q.get("invoiceId")) and "REG/INV/" in text(),
          f"invoiceId={q.get('invoiceId')}; page has REG/INV: {'REG/INV/' in text()}")
    INV = invoice_of(QU)
    inv = api("GET", ep("invoice", id=INV), "admin")[1].get("data") or {} if INV else {}
    INV_NO = inv.get("invoiceNumber")
    if not ok2(pay_api(INV, 4000.00, f"QIUI{RUN}-1")[0]): raise SystemExit("payment 4000 fixture failed")

    # ---- Invoice screen --------------------------------------------------------------------------------------
    go(PAGE["invoices"], 4); search(INV_NO); drain(1)
    cells = row_cells(INV_NO)
    h, val = status_cell(cells)
    shot("04-invoices-partially-paid")
    check("Invoices: test invoice row listed", bool(cells), headers())
    check("Invoices: payment status column reads 'Partially Paid' after 4,000 of 10,000", h is not None and val.split("\n")[0].strip() == "Partially Paid", (h, val))
    check("Invoices: Paid ₹4,000.00 and Outstanding ₹6,000.00 on the row", bool(cells) and cells.get("Paid") == rs("4000") and cells.get("Outstanding") == rs("6000"),
          cells and {k: cells.get(k) for k in ("Total", "Paid", "Outstanding")})
    go(PAGE["invoices"], 4); search(INV_DUE_NO or "none"); drain(1)
    cells = row_cells(INV_DUE_NO or "none")
    h, val = status_cell(cells)
    shot("05-invoices-due")
    check("Invoices: unpaid quotation invoice reads 'Due' in the payment status column, Paid ₹0.00, Outstanding ₹2,360.00",
          h is not None and val.split("\n")[0].strip() == "Due" and cells.get("Paid") == rs("0") and cells.get("Outstanding") == rs("2360"), cells)
    pay_api(INV, 6000.00, f"QIUI{RUN}-2")
    go(PAGE["invoices"], 4); search(INV_NO); drain(1)
    cells = row_cells(INV_NO)
    h, val = status_cell(cells)
    shot("06-invoices-paid")
    check("Invoices: after 6,000 more the status reads 'Paid', Paid ₹10,000.00, Outstanding ₹0.00",
          h is not None and val.split("\n")[0].strip() == "Paid" and cells.get("Paid") == rs("10000") and cells.get("Outstanding") == rs("0"), cells)
    viewport(390); go(PAGE["invoices"], 4)
    shot("07-invoices-390")
    check("Invoices at 390 px: no page-level horizontal scroll", no_hscroll(), js("[document.documentElement.scrollWidth, window.innerWidth]"))
    viewport(1366)

    # ---- Payment screen ----------------------------------------------------------------------------------
    go(PAGE["payments"], 4); search(INV_NO); drain(1)
    hs = headers()
    missing = [c for c in PAYMENT_COLUMNS if c not in hs]
    shot("08-payments-columns")
    check("Payments: the ten columns of the brief are shown", not missing, f"missing {missing}; shown {hs}")
    rows = js("[...document.querySelectorAll('table tbody tr')].map(r=>r.innerText.replace(/\\u00a0/g,' '))") or []
    check("Payments: the test payment of ₹4,000.00 against the test invoice is listed", any(INV_NO in r and rs("4000") in r for r in rows), rows[:3])
    cells = row_cells(rs("4000")) or {}
    check("Payments: the row shows the quotation number", QU_NO in " ".join(cells.values()), cells)
    go(PAGE["payments"], 4); search(QREJ_NO); drain(1)
    rows = js("[...document.querySelectorAll('table tbody tr')].map(r=>r.innerText)") or []
    shot("09-payments-rejected-search")
    check("Payments: no row for the rejected quotation", not [r for r in rows if QREJ_NO in r] and one(
        f"SELECT COUNT(*) FROM TRN_InvoicePayment p JOIN TRN_Invoice i ON i.InvoiceId=p.InvoiceId WHERE i.QuotationId={QREJ}") in (None, "0"), rows[:3])
    viewport(390); go(PAGE["payments"], 4)
    shot("10-payments-390")
    check("Payments at 390 px: no page-level horizontal scroll", no_hscroll(), js("[document.documentElement.scrollWidth, window.innerWidth]"))
    errs = [e["params"]["exceptionDetails"].get("exception", {}).get("description", "")[:200] for e in events if e.get("method") == "Runtime.exceptionThrown"]
    check("no JavaScript errors on the admin screens", not errs, errs[:3])
except SystemExit:
    raise
except Exception:
    check("script ran to the end", False, traceback.format_exc())
finally:
    try:
        if ws: ws.close()
        if proc: proc.terminate()
    finally:
        try: cleanup()
        except BaseException as e: print("clean-up stopped:", e)

print(f"\n{sum(results)}/{len(results)} passed, {len(results) - sum(results)} failed")
sys.exit(0 if results and all(results) else 1)
