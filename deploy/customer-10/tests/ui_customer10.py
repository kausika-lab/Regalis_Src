"""Headless Chrome checks for the customer-10 kit, desktop (1366 px) and phone (390 px).
LOCAL stack only: http://localhost:8080 -> API :81, RegalisWorkSpace_Dev with the migration.
Run after e2e_customer10.py (it signs in as the newest c10-a test customer, whose passes,
bookings and payments that run created). Creates one more throwaway customer, buys a
pay-later Virtual Office and a ticket for it, and deletes it through the UI at the end.
Restore the Dev backup afterwards. Env: PYLIB (websocket-client), SHOTS_DIR,
REGALIS_CUST_EMAIL/PASSWORD (only to copy the password hash for test customers)."""
import base64, json, os, subprocess, sys, time, urllib.request, uuid
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket

SHOTS = os.environ.get("SHOTS_DIR", "."); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
PW = os.environ["REGALIS_CUST_PASSWORD"]
def sql(q):
    return [l.split("|") for l in subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True).stdout.strip().splitlines() if l.strip()]
def one(q):
    r = sql(q); return r[0][0] if r else None

proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9372", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-c10-prof",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9372/json").read()) if t["type"] == "page"); break
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
def go(p, w=5): send("Page.navigate", url=BASE + p); drain(w)
def shot(n): open(os.path.join(SHOTS, n + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png")["data"]))
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:400]), flush=True)
def click(sel, text):
    return js(f"(() => {{ const el=[...document.querySelectorAll({json.dumps(sel)})].find(e=>e.textContent.trim().includes({json.dumps(text)}) && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
def text(): return js("document.body.innerText") or ""
def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
def api(method, path, body=None):
    return js(f"""(async()=>{{const s=JSON.parse(sessionStorage.getItem('regalis-auth')||'{{}}').state||{{}};const r=await fetch('/api{path}',{{method:'{method}',headers:{{'Content-Type':'application/json',Authorization:'Bearer '+s.accessToken}},body:{json.dumps(json.dumps(body)) if body is not None else 'undefined'}}});return {{s:r.status,b:await r.json().catch(()=>null)}}}})()""")
errs = lambda n: [e for e in events[n:] if e.get("method") == "Runtime.exceptionThrown"]
SET = "const set=(el,v)=>{Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}))};"
def login(email):
    go("/login"); js("localStorage.clear(); sessionStorage.clear(); true"); go("/login", 3)
    js("(() => {" + SET + f"set(document.querySelector('input[type=email]'),{json.dumps(email)});set(document.querySelector('input[type=password]'),{json.dumps(PW)});document.querySelector('form button[type=submit]').click();return true}})()")
    drain(7)
    for _ in range(5):
        p = js("location.pathname")
        if p: return p
        drain(1)
    return p
def viewport(w):
    send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)

send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
viewport(1366)
a_email = one("SELECT TOP 1 Email FROM MST_User WHERE Email LIKE 'c10-a-%@example.test' AND Status=1 ORDER BY UserId DESC")
check("signed in as the e2e test customer", login(a_email) == "/", a_email)
n0 = len(events)

# ---- 4 / 7: menu and crown ----------------------------------------------------------------------
nav = js("[...document.querySelectorAll('aside a, nav a')].map(a=>a.getAttribute('href'))") or []
check("4  no QR / Access in the sidebar", "/qr" not in nav, nav)
check("10 Payment History in the sidebar", "/payments" in nav, nav)
for w in (1366, 390):
    viewport(w)
    for p in ("/passes", "/payments", "/support/tickets", "/profile"):
        go(p, 3)
        ok = js("(() => { const a=[...document.querySelectorAll('header a, .sidebar a, aside a')].find(a=>a.querySelector('img') && a.getAttribute('href')==='/'); if(!a) return false; a.click(); return true })()")
        drain(2)
        check(f"7  crown logo -> dashboard from {p} at {w}px", ok and js("location.pathname") == "/", js("location.pathname"))
viewport(1366)

# ---- 2: passes wording + awaiting payment ---------------------------------------------------------
go("/passes", 5)
t = text()
check("2  no 'per day' / 'consecutive' wording on Passes", "per day" not in t.lower() and "consecutive" not in t.lower() and "per-day" not in t.lower(), [l for l in t.splitlines() if "day" in l.lower()][:8])
check("2  plans still listed (Single Day + packs)", all(x in t for x in ("Single Day", "5-Day Pack", "10-Day Pack", "20-Day Pack")), t[:400])
shot("passes_desktop")
click("a, button", "My passes") or go("/passes/my", 4)
drain(2); t = text()
check("2  unpaid pay-later pass shows 'Awaiting payment' and TO PAY", "Awaiting payment" in t and "TO PAY" in t and "becomes active once it is paid" in t, t[:800])
shot("passes_awaiting")

# ---- 3: membership --------------------------------------------------------------------------------
u_email = f"c10-u-{uuid.uuid4().hex[:6]}@example.test"
sql(f"""INSERT MST_User (UserType, FullName, Email, MobileNumber, PasswordHash, DefaultLocationId, IsEmailVerified, IsMobileVerified, Status, FailedLoginCount, MustChangePassword, CreatedDate, IsActive, IsDeleted, PayLaterAllowed, City, Address, CompanyName, AddressLine1, AddressLine2, State, Country, PostalCode, Gender, DateOfBirth)
        SELECT 1, 'C10 UI user', '{u_email}', '+9197{abs(hash(u_email)) % 10**8:08d}', PasswordHash, DefaultLocationId, 1, 1, 1, 0, 0, SYSUTCDATETIME(), 1, 0, 1, City, Address, 'Test Co', AddressLine1, AddressLine2, State, Country, PostalCode, Gender, DateOfBirth FROM MST_User WHERE Email='{os.environ["REGALIS_CUST_EMAIL"]}';
        INSERT MAP_UserRole (UserId, RoleId, AssignedDateUtc) SELECT UserId, (SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'), SYSUTCDATETIME() FROM MST_User WHERE Email='{u_email}';""")
check("signed in as a fresh UI customer", login(u_email) == "/")
go("/membership", 5)
click("button", "Choose a plan"); drain(2)
t = text()
check("3  no percentage anywhere on the plan cards", "%" not in t, [l for l in t.splitlines() if "%" in l][:6])
big = js("[...document.querySelectorAll('[data-rg=mem-price]')].map(e=>parseFloat(getComputedStyle(e).fontSize))")
check("3  plan prices are large (>= 24 px) and gold", big and min(big) >= 24 and "rgb" in (js("getComputedStyle(document.querySelector('[data-rg=mem-price]')).color") or ""), big)
shot("membership_plans_desktop")
click(".choice-card", "Open / Hot Desk"); drain(2)
t = text()
check("3  term cards: no '% off' badge", "% off" not in t, [l for l in t.splitlines() if "%" in l][:6])
shot("membership_terms_desktop")
click(".choice-card", "12 Months"); drain(2)
t = text()
chips = js("[...document.querySelectorAll('[data-rg=mem-benefits] li')].map(li=>getComputedStyle(li).backgroundColor)") or []
check("3  benefits shown as highlighted chips", len(chips) >= 6 and all(c not in ("rgba(0, 0, 0, 0)", "transparent") for c in chips), chips[:3])
check("3  benefit wording without %", "Discount on extra meeting room bookings" in t and "Bigger discount on extra bookings" in t and "%" not in t, [l for l in t.splitlines() if "%" in l][:6])
wrap = js("[...document.querySelectorAll('[data-rg=mem-price]')].every(p=>{const s=p.lastElementChild; return !s || Math.abs(s.getBoundingClientRect().top - p.firstChild.parentElement.getBoundingClientRect().top) < 40 && !/\\n/.test(s.innerText)})")
check("3  '/ month' stays in one piece next to or under the price", wrap)
shot("membership_term_selected_desktop")
viewport(390); drain(1); shot("membership_term_selected_phone")
check("3  term step: no sideways scroll at 390 px", no_hscroll())
viewport(1366); drain(1)
click("button", "Continue to payment"); drain(2)
t = text()
check("3  GST rate kept in the breakdown", "CGST + SGST (18%)" in t, "no gst line")
check("1  membership pay-later note: active once paid", "becomes active once it is paid" in t and "door opens" not in t, t[-500:])
shot("membership_payment_desktop")
viewport(390); drain(1)
check("3  membership payment step: no sideways scroll at 390 px", no_hscroll())
shot("membership_payment_phone")
viewport(1366)
vo = int(one("SELECT PassPlanId FROM MST_PassPlan WHERE PlanCode='MEM-VIRTUAL-1M'"))
r = api("POST", "/passes/purchase", {"passPlanId": vo, "validFrom": time.strftime("%Y-%m-%d"), "paymentMethod": "paylater"})
check("1  (setup) VO bought with pay later", r and r["s"] == 200 and r["b"]["data"]["pass"]["status"] == 5, r)
go("/membership", 5); t = text()
check("1/2 Membership shows the VO as current, 'Awaiting payment', amount to pay", "My membership plan" in t and "Awaiting payment" in t and "Total to pay at the front desk" in t and js("!!document.querySelector('[data-rg=mem-awaiting]')"), t[:600])
shot("membership_awaiting")

# ---- 5: tickets -----------------------------------------------------------------------------------
cat = sql("SELECT TOP 1 c.TicketCategoryId, i.TicketIssueTypeId FROM MST_TicketCategory c JOIN MST_TicketIssueType i ON i.TicketCategoryId=c.TicketCategoryId WHERE c.IsActive=1 AND i.IsActive=1")[0]
r = api("POST", "/tickets", {"ticketCategoryId": int(cat[0]), "ticketIssueTypeId": int(cat[1]), "subject": "C10 test ticket", "description": "UI test of the ticket page layout.", "priority": 2})
tid = (r or {}).get("b", {}).get("data", {}).get("ticketId")
check("5  (setup) ticket raised", tid, r)
for w in (1366, 390):
    viewport(w)
    for p, label in (("/support/tickets", "list"), ("/support/raise", "raise"), (f"/support/tickets/{tid}", "detail")):
        go(p, 4)
        head = js("(() => { const h=document.querySelector('[data-rg=ticket-head]'); if(!h) return null; const s=getComputedStyle(h); return {bl:s.borderLeftWidth, bg:s.backgroundImage, title:h.innerText.split('\\n')[0]} })()")
        check(f"5  {label} heading highlighted at {w}px", head and head["bl"] == "5px" and "gradient" in head["bg"], head)
        if label != "list":
            back = js("(() => { const b=document.querySelector('[data-rg=ticket-back]'); if(!b) return null; const s=getComputedStyle(b); const r=b.getBoundingClientRect(); return {bw:s.borderTopWidth, bs:s.borderTopStyle, h:r.height, vis:r.width>0, text:b.innerText} })()")
            check(f"5  {label} Back is a boxed button at {w}px", back and back["bs"] == "solid" and float(back["bw"][:-2]) >= 1 and back["h"] >= 32 and back["vis"], back)
        check(f"5  {label}: no sideways scroll at {w}px", no_hscroll())
        shot(f"ticket_{label}_{w}")
viewport(1366)
go(f"/support/tickets/{tid}", 4); js("document.querySelector('[data-rg=ticket-back]').click()"); drain(2)
check("5  detail Back goes to the ticket list", js("location.pathname") == "/support/tickets", js("location.pathname"))

# ---- 9: delete account (UI) ---------------------------------------------------------------------------
go("/profile", 4)
check("9  Delete Account button on the profile", js("!!document.querySelector('[data-rg=delete-account]')"))
js("document.querySelector('[data-rg=delete-account]').click()"); drain(3); t = text()
check("9  popup warns about bookings, memberships, passes and payment history", all(x in t for x in ("cannot be undone", "bookings", "memberships", "passes", "payment history")), t[-700:])
check("9  blocked while the VO is unpaid (shows what to settle)", js("!!document.querySelector('[data-rg^=blocker-]')") and not js("[...document.querySelectorAll('button')].some(b=>b.innerText.includes('Delete my account'))"), t[-500:])
shot("delete_blocked")
click("button", "Keep my account"); drain(1)
u_id = one(f"SELECT UserId FROM MST_User WHERE Email='{u_email}'")
pid = one(f"SELECT TOP 1 CustomerPassId FROM TRN_CustomerPass WHERE CustomerId=(SELECT UserId FROM MST_User WHERE Email='{u_email}')")
sql(f"UPDATE TRN_CustomerPass SET Status=4 WHERE CustomerPassId={pid}; UPDATE TRN_Invoice SET Status=4 WHERE CustomerPassId={pid}")  # customers lack PASS.EDIT on Dev
js("document.querySelector('[data-rg=delete-account]').click()"); drain(3)
js("(() => {" + SET + f"set(document.querySelector('[data-rg=delete-dialog] input[type=password]'),{json.dumps(PW)});return true}})()")
drain(1)
check("9  confirm button stays disabled until the box is ticked", js("[...document.querySelectorAll('button')].find(b=>b.innerText.includes('Delete my account'))?.disabled") is True)
js("document.querySelector('[data-rg=delete-dialog] input[type=checkbox]').click()"); drain(1)
shot("delete_confirm")
click("button", "Delete my account"); drain(5)
check("9  after deleting: back on the sign-in page, signed out", js("location.pathname") == "/login" and not js("JSON.parse(sessionStorage.getItem('regalis-auth')||'{}').state?.accessToken"), js("location.pathname"))
check("9  DB: account closed and anonymised", sql(f"SELECT Status, FullName, Email FROM MST_User WHERE UserId={u_id}")[0] == ["2", "Deleted customer", f"deleted-{u_id}@deleted.invalid"])
check("9  the deleted account cannot sign in", login(u_email) == "/login")

# ---- 10: payment history -----------------------------------------------------------------------------
check("signed in again as the e2e customer", login(a_email) == "/")
a_id = one(f"SELECT UserId FROM MST_User WHERE Email='{a_email}'")
for w in (1366, 390):
    viewport(w); go("/payments", 5)
    rows = js("document.querySelectorAll('[data-rg=payment-row]').length")
    check(f"10 page lists the customer's payments at {w}px", rows == int(one(f"SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={a_id}")), rows)
    check(f"10 no sideways scroll at {w}px", no_hscroll())
    shot(f"payments_{w}")
t = text()
check("10 statuses shown: Successful, Pending/Not completed, Failed, Refunded", all(x in t for x in ("Successful", "Failed", "Refunded")) and ("Not completed" in t or "Pending" in t), t[:600])
viewport(1366); go("/payments", 5)
js("document.querySelector('[data-rg=payment-row]').click()"); drain(3); t = text()
check("10 detail shows transaction id, payment no., method, date", all(x in t for x in ("Transaction ID", "Payment no.", "Method", "Started on")), t[-600:])
shot("payment_detail")
click("button", "Close"); drain(1)
click("[role=tab]", "Refunded"); drain(3)
check("10 filter: Refunded shows only refunded rows", js("[...document.querySelectorAll('[data-rg=payment-row]')].every(r=>/Refunded/.test(r.innerText))") and js("document.querySelectorAll('[data-rg=payment-row]').length") >= 1)

# ---- 8: idle sign-out --------------------------------------------------------------------------------
go("/payments", 4)
js("sessionStorage.setItem('rg-last-activity', String(Date.now() - 9.5*60*1000)); true"); go("/payments", 3)
w = js("(() => { const b=document.querySelector('[data-rg=idle-warning]'); return b ? b.innerText : null })()")
check("8  warning after 9 idle minutes, with a countdown", w and "Stay signed in" in w and "signed out in" in w, w)
shot("idle_warning")
tok0 = js("JSON.parse(sessionStorage.getItem('regalis-auth')).state.accessToken")
click("[data-rg=idle-warning] button", "Stay signed in"); drain(3)
check("8  'Stay signed in' closes it and renews the session on the server", not js("!!document.querySelector('[data-rg=idle-warning]')") and js("JSON.parse(sessionStorage.getItem('regalis-auth')).state.accessToken") != tok0)
js("sessionStorage.setItem('rg-last-activity', String(Date.now() - 10.5*60*1000)); true"); go("/payments", 4)
check("8  after 10 idle minutes: signed out, on the sign-in page, told why", js("location.pathname") == "/login" and js("!!document.querySelector('[data-rg=idle-note]')") and not js("JSON.parse(sessionStorage.getItem('regalis-auth')||'{}').state?.accessToken"), js("location.pathname"))
shot("idle_signed_out")
go("/payments", 3)
check("8  protected page after sign-out redirects to sign-in", js("location.pathname") == "/login")
check("8  closing the tab ends the session (auth kept only in sessionStorage)", js("localStorage.getItem('regalis-auth')") is None)

check("no JS errors on any page", not errs(n0), [e["params"]["exceptionDetails"].get("exception", {}).get("description", "")[:200] for e in errs(n0)][:3])
ws.close(); proc.terminate()
print(f"\n{sum(results)}/{len(results)} passed"); sys.exit(0 if all(results) else 1)
