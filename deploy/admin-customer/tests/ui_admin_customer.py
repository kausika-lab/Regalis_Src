"""Headless Chrome checks of the admin-customer kit on the LOCAL stack (localhost:8080 -> API :81),
at desktop (1366) and phone (390) widths. DOM assertions per requirement + screenshots in shots/.
Writes test data (calendar blocks, a booking, waiting-list rows): restore the Dev backup afterwards.
Credentials: REGALIS_ADMIN_EMAIL/PASSWORD, REGALIS_CUST_EMAIL/PASSWORD. Needs websocket-client
on sys.path (PYLIB env var) and Chrome."""
import base64, datetime as dt, json, os, subprocess, sys, time, urllib.request, urllib.error
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.environ.get("SHOTS_DIR", os.path.join(HERE, "shots")); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
API = "http://127.0.0.1:81/api"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
PORT = 9351
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]


def sql(q):
    out = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode:
        raise SystemExit("SQL failed: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def call(method, path, token=None, body=None):
    req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=60) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b"{}")


def api_login(email, pw, portal):
    for _ in range(4):
        s, b = call("POST", "/auth/login", body={"email": email, "password": pw, "portal": portal})
        if s == 200:
            d = b.get("data") or {}; return d.get("accessToken") or (d.get("tokens") or {}).get("accessToken")
        time.sleep(20)
    raise SystemExit("api login failed")


ADM_E, ADM_P = os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"]
CUS_E, CUS_P = os.environ["REGALIS_CUST_EMAIL"], os.environ["REGALIS_CUST_PASSWORD"]

# ---------- test data: a full day-pass date, a future booking to cancel ----------
admin_tok = api_login(ADM_E, ADM_P, "admin")
cust_id = int(sql(f"SELECT UserId FROM MST_User WHERE Email='{CUS_E}'")[0][0])
loc_id = int(sql("SELECT TOP 1 LocationId FROM MST_Location WHERE IsDefault=1 AND IsDeleted=0")[0][0])
today = dt.datetime.now(IST).date()
desk_type = int(sql("SELECT WorkspaceTypeId FROM MST_WorkspaceType WHERE TypeCode='DESK'")[0][0])
full_day = None
for k in range(8, 30):  # first open day from 8 days ahead (the office is closed on some days)
    cand = today + dt.timedelta(days=k)
    s_, b_ = call("GET", f"/availability/slots?locationId={loc_id}&workspaceTypeId={desk_type}&date={cand}", admin_tok)
    if s_ == 200 and b_.get("data"):
        full_day = cand
        break
d0 = dt.datetime.combine(full_day, dt.time(0, 0), IST).astimezone(dt.timezone.utc)
blocks = []
for (w,) in sql(f"SELECT w.WorkspaceId FROM MST_Workspace w JOIN MST_WorkspaceType t ON t.WorkspaceTypeId=w.WorkspaceTypeId WHERE t.TypeCode='DESK' AND w.LocationId={loc_id} AND w.IsBookable=1 AND w.IsActive=1 AND w.IsDeleted=0"):
    s, b = call("POST", "/calendar/block", admin_tok, {"workspaceId": int(w), "startUtc": d0.strftime("%Y-%m-%dT%H:%M:%SZ"), "endUtc": (d0 + dt.timedelta(days=1)).strftime("%Y-%m-%dT%H:%M:%SZ"), "blockReason": 1, "reasonNotes": "UI waitlist"})
    if s in (200, 201):
        blocks.append(b["data"]["workspaceBlockId"])
room = sql(f"SELECT TOP 1 w.WorkspaceId FROM MST_Workspace w JOIN MST_WorkspaceType t ON t.WorkspaceTypeId=w.WorkspaceTypeId WHERE t.TypeCode='MEETING_ROOM' AND w.LocationId={loc_id} AND w.IsBookable=1 AND w.IsDeleted=0")[0][0]
st = dt.datetime.combine(today + dt.timedelta(days=5), dt.time(15, 0), IST).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
s, b = call("POST", "/bookings/admin", admin_tok, {"customerId": cust_id, "workspaceId": int(room), "startUtc": st, "durationMinutes": 60, "paymentMethod": "cash"})
cancel_no = b["data"]["booking"]["bookingNumber"] if s == 200 else None

# ---------- browser ----------
proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={PORT}", f"--user-data-dir={os.path.join(os.environ.get('TEMP', HERE), 'rg-ui-prof')}",
                         "--window-size=1366,900", "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json").read()) if t["type"] == "page"); break
    except Exception:
        time.sleep(0.3)
ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60)
mid, events, results = 0, [], []


def send(method, **params):
    global mid; mid += 1
    ws.send(json.dumps({"id": mid, "method": method, "params": params}))
    while True:
        m = json.loads(ws.recv())
        if m.get("id") == mid:
            return m.get("result", {})
        events.append(m)


def drain(s):
    end = time.time() + s; ws.settimeout(0.3)
    while time.time() < end:
        try: events.append(json.loads(ws.recv()))
        except Exception: pass
    ws.settimeout(60)


def js(expr):
    return send("Runtime.evaluate", expression=expr, awaitPromise=True, returnByValue=True).get("result", {}).get("value")


def go(path, wait=4.0):
    send("Page.navigate", url=BASE + path); drain(wait)


def shot(name):
    open(os.path.join(SHOTS, name + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png", captureBeyondViewport=True)["data"]))


def check(name, ok, detail=""):
    results.append((name, bool(ok))); print(("PASS " if ok else "FAIL ") + name + ("" if ok else "  -> " + str(detail)[:300]), flush=True)


def errs(n):
    return [(e["params"]["exceptionDetails"].get("exception", {}).get("description") or e["params"]["exceptionDetails"].get("text", ""))[:200]
            for e in events[n:] if e.get("method") == "Runtime.exceptionThrown"]


def size(w, h, mob):
    send("Emulation.setDeviceMetricsOverride", width=w, height=h, deviceScaleFactor=1, mobile=mob)


def click_text(sel, text):
    return js(f"""(() => {{ const el = [...document.querySelectorAll({json.dumps(sel)})].find(e => e.textContent.trim().includes({json.dumps(text)}));
      if (!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()""")


SET = "const set = (el, v) => { const p = el.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype; Object.getOwnPropertyDescriptor(p, 'value').set.call(el, v); el.dispatchEvent(new Event(el.tagName==='SELECT'?'change':'input', { bubbles: true })); };"


def ui_login(path, email, pw):
    go(path, 5)
    js("(() => {" + SET + f"set(document.querySelector('input[type=email]'), {json.dumps(email)}); set(document.querySelector('input[type=password]'), {json.dumps(pw)}); document.querySelector('form button[type=submit]').click(); return true; }})()")
    drain(7)


send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
size(1366, 900, False)
js("localStorage.clear(); sessionStorage.clear(); true")

# ============ logged out: Register (item 2) ============
for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
    size(w, h, mob); n = len(events); go("/register", 5)
    r = js("""(() => { const s = document.querySelector('.rg-phone select'), i = document.querySelector('.rg-phone input');
      if (!s || !i) return null; const sr = s.getBoundingClientRect(), ir = i.getBoundingClientRect();
      const pr = document.querySelector('.auth__panel').getBoundingClientRect().right;
      return { cc: s.value, sameRow: Math.abs(sr.top - ir.top) < 12 && sr.right <= ir.left + 2, maxLen: i.maxLength, mode: i.inputMode,
               fits: [...document.querySelectorAll('.auth__input')].every(x => x.getBoundingClientRect().right <= pr + 0.5) }; })()""")
    check(f"[{label}] register: every field fits inside the form panel (no widened column)", r and r["fits"], r)
    check(f"[{label}] register: country code dropdown (default +91) beside the number", r and r["cc"] == "91" and r["sameRow"] and r["maxLen"] == 10 and r["mode"] == "numeric", r)
    if label == "desktop":
        js("(() => {" + SET + "const i=document.querySelector('.rg-phone input'); set(i,'98ab76-5'); return i.value; })()")
        drain(0.5)
        v = js("document.querySelector('.rg-phone input').value")
        check("[desktop] register: letters and symbols are dropped from the number", v == "98765", v)
        js("(() => {" + SET + "set(document.querySelector('input[type=email]'),'name@@example'); set(document.querySelector('.rg-phone input'),'12345'); document.querySelector('form button[type=submit]').click(); return true; })()")
        drain(1.5)
        t = js("document.body.innerText")
        check("[desktop] register: invalid email message", "Enter a valid email address" in t, t[:300])
        check("[desktop] register: India length/start message", "10-digit mobile number for India" in t or "starts with 6, 7, 8 or 9" in t, t[:300])
        js("(() => {" + SET + "set(document.querySelector('.rg-phone select'),'971'); return true; })()"); drain(0.5)
        check("[desktop] register: another country changes the allowed length (UAE 9)", js("document.querySelector('.rg-phone input').maxLength") == 9)
    check(f"[{label}] register: no JS errors", not errs(n), errs(n))
    shot(f"{label}_register")

# ============ customer ============
size(1366, 900, False)
ui_login("/login", CUS_E, CUS_P)
check("customer signed in", js("location.pathname") == "/", js("location.pathname"))
for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
    size(w, h, mob)
    n = len(events); go("/passes", 5)
    nav = js("[...document.querySelectorAll('.portal__nav a')].map(a => a.getAttribute('href'))")
    check(f"[{label}] sidebar: My QR / Access hidden (8)", nav and "/qr" not in nav and "/passes" in nav, nav)
    if mob:
        js("(document.querySelector('.portal__menu, .portal__burger, button[aria-label*=menu i]')||{click(){}}).click(); true"); drain(0.5)
    a = js("(() => { const b = document.querySelector('.portal__brand'); return b ? {tag: b.tagName, href: b.getAttribute('href')} : null; })()")
    check(f"[{label}] crown logo is a link to the dashboard (13)", a and a["tag"] == "A" and a["href"] == "/", a)
    js("document.querySelector('.portal__brand').click(); true"); drain(2)
    check(f"[{label}] clicking the crown opens the customer dashboard", js("location.pathname") == "/", js("location.pathname"))
    check(f"[{label}] customer pages: no JS errors", not errs(n), errs(n))

    # Profile (15, 16, 2)
    n = len(events); go("/profile", 5)
    p = js("""(() => { const h = document.querySelector('.rg-prof-head'); if (!h) return null;
      const av = h.querySelector('button'), name = h.querySelector('.rg-prof-head__text');
      const a = av.getBoundingClientRect(), t = name.getBoundingClientRect();
      return { row: getComputedStyle(h).flexDirection, photoLeft: a.right <= t.left + 1, sameBand: a.top < t.bottom && t.top < a.bottom,
               text: name.innerText, phone: [...document.querySelectorAll('.auth__grid')].map(g => g.innerText + ' ' + [...g.querySelectorAll('input')].map(i => i.value).join(' ')).join(' ') }; })()""")
    check(f"[{label}] profile: photo on the left, name beside it (15)", p and p["row"] == "row" and p["photoLeft"] and p["sameBand"], p)
    check(f"[{label}] profile: no 'Customer' label by the name (16)", p and "Customer" not in p["text"], p and p["text"])
    check(f"[{label}] profile: phone shown as '+91 <number>'", p and "+91 " in p["phone"], p and p["phone"][:200])
    shot(f"{label}_profile")
    if label == "desktop":
        click_text("button", "Edit Profile"); drain(2)
        e = js("(() => { const s=document.querySelector('.rg-phone select'), i=document.querySelector('.rg-phone input'); return s&&i?{cc:s.value,num:i.value}:null; })()")
        check("[desktop] profile edit: phone with country code (2)", e and e["cc"] == "91" and len(e["num"]) == 10, e)
        js("(() => {" + SET + "set(document.querySelector('.rg-phone input'),'12345'); return true; })()"); drain(0.3)
        click_text("button[type=submit], button", "Save"); drain(1.5)
        t = js("document.body.innerText")
        check("[desktop] profile edit: invalid number blocked with a message", "10-digit mobile number for India" in t or "starts with 6" in t, t[:200])
        shot("desktop_profile_edit")
    check(f"[{label}] profile: no JS errors", not errs(n), errs(n))

    # Membership (7, 18, 19)
    n = len(events); go("/membership", 5)
    if not click_text(".choice-card", "Open / Hot Desk"):
        click_text("button", "Change plan"); drain(1); click_text("button", "Choose a plan"); drain(1); click_text(".choice-card", "Open / Hot Desk")
    drain(1.5)
    click_text(".choice-card", "6 Months"); drain(1)
    inc = js("[...document.querySelectorAll('.mem-incl .card__title, .mem-incl h3, .mem-incl h2')].map(e=>e.textContent.trim())")
    check(f"[{label}] membership: 'What's included' and 'Amenities' cards (19)", inc and "What's included" in inc and "Amenities" in inc, inc)
    am = js("(() => { const c=[...document.querySelectorAll('.mem-incl > *')]; return c.length===2 ? [c[0].innerText, c[1].innerText] : null; })()")
    check(f"[{label}] membership: Wi-Fi under Amenities, plan perks under What's included", am and "Wi-Fi" in am[1] and "Wi-Fi" not in am[0] and "Priority booking" in am[0], am)
    shot(f"{label}_membership_term")
    click_text("button", "Continue to payment"); drain(1.5)
    m = js("""(() => { const g=document.querySelector('.mem-pay'); if(!g) return null; const k=[...g.children];
      const a=k[0].getBoundingClientRect(), b=k[1].getBoundingClientRect();
      const btns=[...g.querySelectorAll('.pay-options .btn')].map(x=>Math.round(x.getBoundingClientRect().width));
      return { cols: getComputedStyle(g).gridTemplateColumns.split(' ').length, side: b.left >= a.right - 1, stacked: b.top >= a.bottom - 1,
               text: g.innerText, btns, panelW: Math.round(k[1].getBoundingClientRect().width), overflow: document.documentElement.scrollWidth > innerWidth + 1 }; })()""")
    if mob:
        check("[mobile] membership payment: one column, panel under the summary (7)", m and m["cols"] == 1 and m["stacked"], m)
    else:
        check("[desktop] membership payment: summary left, 'Pay with' right (7)", m and m["cols"] == 2 and m["side"], m)
    check(f"[{label}] membership payment: buttons stacked full width", m and m["btns"] and min(m["btns"]) >= m["panelW"] - 80, m and (m["btns"], m["panelW"]))
    check(f"[{label}] membership payment: 'Discount' without a percentage (18)", m and "Discount\n" in m["text"] or (m and "Discount\t" in m["text"]) or (m and "Discount (" not in m["text"] and "Discount" in m["text"]), m and m["text"][:400])
    check(f"[{label}] membership payment: no horizontal scroll", m and not m["overflow"], m)
    shot(f"{label}_membership_payment")
    check(f"[{label}] membership: no JS errors", not errs(n), errs(n))

    # Passes + waiting list (11)
    n = len(events); go(f"/passes?date={full_day}", 5)
    click_text(".choice-card", ""); drain(3)
    t = js("document.body.innerText")
    check(f"[{label}] passes: full date says so and offers the waiting list", "Every desk is taken that day" in t and ("Join the waiting list" in t or "You are on the waiting list" in t), t[:400])
    if label == "desktop":
        click_text("button", "Join the waiting list"); drain(3)
        t = js("document.body.innerText")
        check("[desktop] passes: joined -> 'My waiting list' card + position", "My waiting list" in t and "#1 in line" in t, t[:500])
    else:
        check("[mobile] passes: 'My waiting list' card visible", "My waiting list" in t, t[:300])
    shot(f"{label}_passes_waitlist")
    check(f"[{label}] passes: no JS errors", not errs(n), errs(n))

# customer removes, re-joins (so admin has a row)
size(1366, 900, False); go("/passes", 5)
click_text(".rg-wl-list button", "Remove"); drain(1); click_text(".modal button, [role=dialog] button", "Remove"); drain(2)
check("customer removes the entry from 'My waiting list'", "My waiting list" not in js("document.body.innerText"))
cust_tok = api_login(CUS_E, CUS_P, "customer")
call("POST", "/waitlist", cust_tok, {"waitDate": str(full_day), "locationId": loc_id})

# ============ admin ============
js("localStorage.clear(); sessionStorage.clear(); true")
ui_login("/admin/login", ADM_E, ADM_P)
check("admin signed in", js("location.pathname").startswith("/admin"), js("location.pathname"))
for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
    size(w, h, mob)
    n = len(events); go("/admin", 6)
    d = js("""(() => ({ loc: (document.querySelector('.locpick__name')||{}).textContent || null,
      brand: (() => { const b=document.querySelector('.portal__brand'); return b && b.tagName==='A' ? b.getAttribute('href') : null; })(),
      occ: [...document.querySelectorAll('.stat-card')].map(c=>c.innerText).find(t=>/Occupancy/.test(t)) || null,
      overflow: document.documentElement.scrollWidth > innerWidth + 1 }))()""")
    check(f"[{label}] admin header: location picker shown (4)", d and d["loc"], d)
    check(f"[{label}] admin crown links to /admin (13)", d and d["brand"] == "/admin", d)
    check(f"[{label}] admin dashboard: occupancy card with booked/open hours (12)", d and d["occ"] and " h booked" in d["occ"], d and d["occ"])
    check(f"[{label}] admin dashboard: no horizontal scroll", d and not d["overflow"], d)
    shot(f"{label}_admin_dashboard")

    n2 = len(events); go("/admin/bookings", 6)
    hdr = js("[...document.querySelectorAll('.data-table th')].map(t=>t.textContent.trim())")
    check(f"[{label}] admin bookings: Location column (5)", hdr and "Location" in hdr, hdr)
    shot(f"{label}_admin_bookings")
    if label == "desktop" and cancel_no:
        js(f"""(() => {{ const s=document.querySelector('input[placeholder*=Booking]'); {SET} set(s, {json.dumps(cancel_no)}); return true; }})()"""); drain(2.5)
        click_text(".data-table button", "Detail"); drain(1.5)
        t = js("document.querySelector('[role=dialog], .modal') ? document.querySelector('[role=dialog], .modal').innerText : ''")
        check("[desktop] admin booking detail shows Location (5)", "Location" in t, t[:300])
        ok = click_text("[role=dialog] button, .modal button", "Cancel booking"); drain(1)
        check("[desktop] admin booking detail has 'Cancel booking' (3)", ok)
        t = js("document.body.innerText")
        check("[desktop] cancel dialog explains the refund policy and asks for a reason", "same cancellation policy" in t and "Reason" in t, t[-600:])
        shot("desktop_admin_cancel_dialog")
        js("(() => { " + SET + " const i=[...document.querySelectorAll('[role=dialog] input, .modal input')].pop(); set(i,'UI test cancel'); return true; })()"); drain(0.5)
        btns = js("[...document.querySelectorAll('[role=dialog] button, .modal button')].filter(b=>b.textContent.trim()==='Cancel booking').length")
        js("(() => { const b=[...document.querySelectorAll('[role=dialog] button, .modal button')].filter(b=>b.textContent.trim()==='Cancel booking').pop(); b.click(); return true; })()"); drain(3)
        st_ = sql(f"SELECT Status FROM TRN_Booking WHERE BookingNumber='{cancel_no}'")[0][0]
        check("[desktop] booking cancelled from the admin screen -> DB status 5", st_ == "5", st_)
    check(f"[{label}] admin bookings: no JS errors", not errs(n2), errs(n2))

    n3 = len(events); go("/admin/waitlist", 6)
    t = js("document.body.innerText")
    nav = js("[...document.querySelectorAll('.portal__nav a')].map(a=>a.getAttribute('href'))")
    check(f"[{label}] admin sidebar has Waiting List (11)", nav and "/admin/waitlist" in nav, nav)
    if label == "desktop":
        check("[desktop] admin waiting list shows the customer's entry", "Waiting List" in t and "#1 in line" in t, t[:400])
    else:  # the desktop pass removed the entry; the phone view lists closed entries under "All statuses"
        js("(() => { " + SET + " const s=[...document.querySelectorAll('select')].find(x=>[...x.options].some(o=>o.value==='0')); set(s,'0'); return true; })()"); drain(2)
        t = js("document.body.innerText")
        check("[mobile] admin waiting list (all statuses) shows the removed entry and its reason", "Removed by staff" in t and "UI test removal" in t, t[:400])
    shot(f"{label}_admin_waitlist")
    if label == "desktop":
        click_text(".data-table button", "Remove"); drain(1)
        js("(() => { " + SET + " const i=[...document.querySelectorAll('[role=dialog] input, .modal input')].pop(); set(i,'UI test removal'); return true; })()"); drain(0.4)
        js("(() => { const b=[...document.querySelectorAll('[role=dialog] button, .modal button')].filter(b=>b.textContent.trim()==='Remove').pop(); b.click(); return true; })()"); drain(3)
        r = sql(f"SELECT TOP 1 Status, CloseReason FROM TRN_SeatWaitlist WHERE CustomerId={cust_id} ORDER BY SeatWaitlistId DESC")[0]
        check("[desktop] admin removes the entry with a reason", r == ["4", "UI test removal"], r)
    check(f"[{label}] admin waiting list: no JS errors", not errs(n3), errs(n3))

    n4 = len(events); go("/admin/calendar", 6)
    cal = js("(() => { const s=document.querySelector('select[aria-label=Location]'); return s ? s.value : null; })()")
    store = js("(() => { try { return JSON.parse(localStorage.getItem('regalis-ui')).state.locationId } catch { return null } })()")
    check(f"[{label}] admin calendar follows the header location (4)", cal is not None and str(store) == cal, (cal, store))
    check(f"[{label}] admin calendar: no JS errors", not errs(n4), errs(n4))

    if label == "desktop":
        n5 = len(events); go("/admin/users", 6)
        click_text("button", "New user") or click_text("button", "Add user") or click_text("button", "New"); drain(1.5)
        f = js("(() => { const s=document.querySelector('[role=dialog] .rg-phone select, .modal .rg-phone select'); return s ? s.value : null; })()")
        check("[desktop] admin new user form: country code + number (2)", f == "91", f)
        shot("desktop_admin_user_form")
        check("[desktop] admin users: no JS errors", not errs(n5), errs(n5))

for bid in blocks:
    call("POST", f"/calendar/unblock/{bid}", admin_tok)
ws.close(); proc.terminate()
ok = sum(1 for _, k in results if k)
print(f"\n{ok}/{len(results)} passed")
sys.exit(0 if ok == len(results) else 1)
