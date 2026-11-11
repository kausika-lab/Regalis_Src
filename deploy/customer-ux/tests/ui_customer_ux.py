"""Headless Chrome checks of the customer-ux changes on the LOCAL stack (localhost:8080), at
desktop (1366 px) and phone (390 px) widths. Screenshots go to shots/.

Rows the test customer does not have (bookings with each payment state, live passes, a booked
desk) are served by intercepting the API call in Chrome (DevTools Fetch), so nothing is written
to the database. Everything else is the real API.
  set REGALIS_TEST_EMAIL / REGALIS_TEST_PASSWORD, then run with python."""
import base64, datetime, json, os, subprocess, sys, time, urllib.request
sys.path.insert(0, r"C:\Users\santh\AppData\Local\Temp\claude\d--Regalis-Regalis-Src\9dddd6f3-c2eb-49bb-a57d-e0a249c6b507\scratchpad\pylib")
import websocket

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.path.join(HERE, "shots"); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
PORT = 9343
proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={PORT}", f"--user-data-dir={os.path.join(HERE, 'chrome-prof')}",
                         "--window-size=1366,900", "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json").read()) if t["type"] == "page"); break
    except Exception: time.sleep(0.3)
ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60)
mid, events, results = 0, [], []

# ---- synthetic API rows -------------------------------------------------------------------
now = datetime.datetime.now(datetime.timezone.utc)
iso = lambda d: d.strftime("%Y-%m-%dT%H:%M:%S.000Z")
day = lambda n: (now + datetime.timedelta(hours=5.5) + datetime.timedelta(days=n)).date().isoformat()
def booking(i, status, pay_later, paid, total=1180.0):
    s = now + datetime.timedelta(days=i + 1)
    return {"bookingId": 9000 + i, "bookingNumber": f"TEST-{i}", "customerId": 1, "customerName": "Test", "locationId": 1, "workspaceId": 1,
            "workspaceCode": "DDK-D-09", "workspaceName": "Dedicated Desk 09", "workspaceTypeName": "Desk", "startUtc": iso(s),
            "endUtc": iso(s + datetime.timedelta(hours=2)), "durationMinutes": 120, "baseAmount": 1000.0, "taxAmount": 180.0, "totalAmount": total,
            "status": status, "paymentStatus": 2, "bookingSource": 1, "notes": None, "createdDate": iso(now), "holdExpiresUtc": None,
            "billingMonths": None, "isPayLater": pay_later, "amountPaid": paid, "paymentDueUtc": None}
BOOKINGS = [booking(1, 2, False, 1180.0), booking(2, 2, True, 0.0), booking(3, 2, True, 1180.0)]
def cpass(i, pay_later, paid, valid_from, valid_to, plan="Single Day"):
    return {"customerPassId": 9100 + i, "passNumber": f"PASS-T{i}", "customerId": 1, "customerName": "Test", "passPlanId": 1, "planName": plan,
            "passType": 1, "locationId": 1, "validFrom": valid_from, "validTo": valid_to, "price": 800.0, "taxAmount": 144.0, "totalAmount": 944.0,
            "status": 1, "purchaseUtc": iso(now), "invoiceId": None, "isPayLater": pay_later, "amountPaid": paid, "paymentDueUtc": None}
PASSES = [cpass(1, True, 0.0, day(2), day(2)), cpass(2, False, 944.0, day(3), day(3)), cpass(3, True, 0.0, day(9), day(13), "5 Day Pack")]
def ws_row(i, name, avail, price):
    return {"workspaceId": 9200 + i, "workspaceCode": f"DDK-T-{i}", "workspaceName": name, "floorNumber": 1, "capacity": 1, "amenities": [],
            "pricePerHour": 100.0, "pricePerDay": 800.0, "pricePerMonth": price, "isAvailable": avail, "unavailableReason": None if avail else "Booked"}
MONTHLY = [ws_row(1, "Dedicated Desk T1", True, 9000.0), ws_row(2, "Dedicated Desk T2", False, None), ws_row(3, "Dedicated Desk T3", False, 9000.0)]
paged = lambda items: {"items": items, "totalCount": len(items), "page": 1, "pageSize": 100, "totalPages": 1}
MOCKS = {}  # url substring -> data, switched on per page

def fulfil(ev):
    p = ev["params"]; url = p["request"]["url"]
    hit = next((v for k, v in MOCKS.items() if k in url), None)
    global mid; mid += 1
    if hit is None:
        ws.send(json.dumps({"id": mid, "method": "Fetch.continueRequest", "params": {"requestId": p["requestId"]}}))
    else:
        body = base64.b64encode(json.dumps({"success": True, "message": None, "data": hit, "errors": []}).encode()).decode()
        ws.send(json.dumps({"id": mid, "method": "Fetch.fulfillRequest", "params": {"requestId": p["requestId"], "responseCode": 200,
                 "responseHeaders": [{"name": "Content-Type", "value": "application/json"}], "body": body}}))

def handle(m):
    if m.get("method") == "Fetch.requestPaused": fulfil(m)
    else: events.append(m)
def send(method, **params):
    global mid; mid += 1; me = mid
    ws.send(json.dumps({"id": me, "method": method, "params": params}))
    while True:
        m = json.loads(ws.recv())
        if m.get("id") == me: return m.get("result", {})
        if "id" not in m: handle(m)
def drain(s):
    end = time.time() + s; ws.settimeout(0.3)
    while time.time() < end:
        try:
            m = json.loads(ws.recv())
            if "id" not in m: handle(m)
        except Exception: pass
    ws.settimeout(60)
def js(expr):
    return send("Runtime.evaluate", expression=expr, awaitPromise=True, returnByValue=True).get("result", {}).get("value")
def go(path, wait=4.0): send("Page.navigate", url=BASE + path); drain(wait)
def shot(name):
    open(os.path.join(SHOTS, name + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png", captureBeyondViewport=True)["data"]))
def check(name, ok, detail=""):
    results.append((name, bool(ok))); print(("PASS " if ok else "FAIL ") + name + ("" if ok else "  -> " + str(detail)[:400]), flush=True)
def js_errors(): return [e["params"]["exceptionDetails"].get("exception", {}).get("description", "")[:200] for e in events if e.get("method") == "Runtime.exceptionThrown"]
def size(w, h, mobile): send("Emulation.setDeviceMetricsOverride", width=w, height=h, deviceScaleFactor=1, mobile=mobile)
def click_text(sel, text):
    return js(f"""(() => {{ const el = [...document.querySelectorAll({json.dumps(sel)})].find(x => x.textContent.trim().startsWith({json.dumps(text)}));
      if (!el) return false; el.click(); return true; }})()""")
NO_HSCROLL = "document.documentElement.scrollWidth <= window.innerWidth + 1"

send("Page.enable"); send("Runtime.enable"); send("Network.enable")
send("Network.setBypassServiceWorker", bypass=True)  # else the PWA worker fetches and Fetch never sees it
send("Fetch.enable", patterns=[{"urlPattern": "*/api/*", "requestStage": "Request"}])

# ---- sign in ---------------------------------------------------------------------------------
size(1366, 900, False)
go("/login", 5)
js("""(() => { const set = (el, v) => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, v); el.dispatchEvent(new Event('input', { bubbles: true })); };
  set(document.querySelector('input[type=email]'), """ + json.dumps(os.environ['REGALIS_TEST_EMAIL']) + """); set(document.querySelector('input[type=password]'), """ + json.dumps(os.environ['REGALIS_TEST_PASSWORD']) + """);
  document.querySelector('form button[type=submit]').click(); return true; })()""")
drain(8)
check("customer signed in", js("location.pathname") == "/", js("location.pathname"))

for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
    size(w, h, mob)

    # 1. Dashboard: Custom tile beside Membership, no separate banner
    go("/", 5)
    d = js("""(() => { const t = [...document.querySelectorAll('.home__quick .home__tile')];
      const r = x => x.getBoundingClientRect();
      const m = t.find(x => /Membership/.test(x.textContent)), c = t.find(x => /^Custom/.test(x.querySelector('.home__tile-title')?.textContent || ''));
      return { titles: t.map(x => x.querySelector('.home__tile-title').textContent), href: c?.getAttribute('href'),
               sameRow: !!(m && c && Math.abs(r(m).top - r(c).top) < 2), sameSize: !!(m && c && Math.abs(r(m).height - r(c).height) < 2 && c.className === m.className),
               promo: !!document.querySelector('.home__promo') }; })()""")
    check(f"[{label}] dashboard: Custom is the tile right after Membership", d and d["titles"][-2:] == ["Membership", "Custom"], d)
    check(f"[{label}] dashboard: Custom uses the same tile as Membership", d and d["sameSize"], d)
    if label == "desktop": check(f"[{label}] dashboard: Custom sits next to Membership (same row)", d and d["sameRow"], d)
    check(f"[{label}] dashboard: separate customisation banner gone", d and not d["promo"], d)
    check(f"[{label}] dashboard: no sideways scroll", js(NO_HSCROLL))
    shot(f"{label}_1_dashboard")
    js("[...document.querySelectorAll('.home__tile')].find(x => /Custom/.test(x.textContent)).click(); true"); drain(4)
    s = js("({ path: location.pathname + location.search, sel: document.querySelector('.choice-card--selected .choice-card__title')?.textContent })")
    check(f"[{label}] Custom tile opens Book Workspace with Customisation chosen", s and s["path"] == "/book?type=custom" and s["sel"] == "Customisation", s)

    # 4. Book Workspace: workspace first, then dates and time
    go("/book", 5)
    b = js("({ dates: document.querySelectorAll('input[type=date]').length, prompt: /Pick a workspace first/.test(document.body.textContent), find: [...document.querySelectorAll('button')].some(x => /Find available/.test(x.textContent)) })")
    check(f"[{label}] book: no date/time inputs before a workspace is picked", b and b["dates"] == 0 and not b["find"] and b["prompt"], b)
    shot(f"{label}_4a_book_start")
    click_text(".choice-card", "Meeting Room"); drain(3)
    b = js("({ dates: document.querySelectorAll('input[type=date]').length, sel: document.querySelector('.choice-card--selected .choice-card__title')?.textContent, slots: document.querySelectorAll('.slot-chip').length })")
    check(f"[{label}] book: picking a workspace shows the date and time", b and b["dates"] >= 1 and b["sel"] == "Meeting Room", b)
    js("""(() => { const el = document.querySelector('input[type=date]'); const d = new Date(Date.now() + 5 * 864e5).toISOString().slice(0, 10);
      Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, d); el.dispatchEvent(new Event('input', { bubbles: true })); el.dispatchEvent(new Event('change', { bubbles: true })); return true; })()""")
    drain(3)
    b = js("({ sel: document.querySelector('.choice-card--selected .choice-card__title')?.textContent })")
    check(f"[{label}] book: workspace stays chosen after changing the date", b and b["sel"] == "Meeting Room", b)
    shot(f"{label}_4b_book_after_pick")

    # 5 + 6. Desk results: Booked shown once, no seat count on desks
    click_text(".choice-card", "Desk"); drain(2)
    click_text(".choice-card", "1 month"); drain(1)
    MOCKS["/availability/monthly"] = MONTHLY
    js("[...document.querySelectorAll('button')].find(x => /Find available/.test(x.textContent))?.click(); true"); drain(4)
    MOCKS.pop("/availability/monthly", None)
    r = js("""(() => { const cards = [...document.querySelectorAll('.choice-card')].filter(c => /Dedicated Desk T/.test(c.textContent));
      return cards.map(c => ({ name: c.querySelector('.choice-card__title span')?.textContent, booked: (c.textContent.match(/Booked/g) || []).length,
        seats: /seat/i.test(c.textContent), title: c.getAttribute('title') })); })()""")
    check(f"[{label}] desks: three result cards", r and len(r) == 3, r)
    check(f"[{label}] desks: a booked desk says Booked exactly once", r and all(x["booked"] == (0 if i == 0 else 1) for i, x in enumerate(r)), r)
    check(f"[{label}] desks: no seat count on desk cards", r and not any(x["seats"] for x in r), r)
    check(f"[{label}] desks: no duplicate Booked tooltip", r and not any(x["title"] for x in r), r)
    shot(f"{label}_5_desks")

    # 2. Membership type sizes
    go("/membership", 5)
    click_text("button", "Choose a plan"); drain(2)
    m = js("""(() => { const px = s => s ? parseFloat(getComputedStyle(s).fontSize) : null;
      return { wrap: !!document.querySelector('.mem-page'), cardTitle: px(document.querySelector('.mem-page .card__title')),
               choiceTitle: px(document.querySelector('.mem-page .choice-card__title')), choiceMeta: px(document.querySelector('.mem-page .choice-card__meta')),
               h2: px(document.querySelector('.mem-page h2:not(.card__title)')) }; })()""")
    check(f"[{label}] membership: larger type (card title 22px, plan title 16px, details 14px)",
          m and m["wrap"] and m["cardTitle"] == 22 and (m["choiceTitle"] in (16, None)) and (m["choiceMeta"] in (14, None)), m)
    check(f"[{label}] membership: no sideways scroll", js(NO_HSCROLL))
    shot(f"{label}_2_membership")

    # 3. My Bookings: PAID / TO PAY
    MOCKS["/bookings/my"] = paged(BOOKINGS)
    go("/bookings", 5)
    MOCKS.pop("/bookings/my", None)
    p = js("""(() => { const tags = [...document.querySelectorAll('.pay-tag')];
      return { tags: tags.map(t => t.textContent), size: tags[0] ? parseFloat(getComputedStyle(tags[0]).fontSize) : null,
               weight: tags[0] ? getComputedStyle(tags[0]).fontWeight : null, payLater: /Pay later ·/.test(document.body.textContent) }; })()""")
    check(f"[{label}] bookings: PAID, TO PAY, PAID for paid / pay-later due / pay-later settled", p and p["tags"] == ["PAID", "TO PAY", "PAID"], p)
    check(f"[{label}] bookings: tag is larger and bold (14px, 800)", p and p["size"] == 14 and p["weight"] == "800", p)
    check(f"[{label}] bookings: old 'Pay later ·' line replaced", p and not p["payLater"], p)
    shot(f"{label}_3_bookings")

    # 7 + 8 + 9. My Passes
    MOCKS["/passes/my"] = paged(PASSES)
    go("/passes/my", 5)
    MOCKS.pop("/passes/my", None)
    q = js("""(() => { const cards = [...document.querySelectorAll('.choice-card')].filter(c => /PASS-T/.test(c.textContent));
      return cards.map(c => ({ no: (c.textContent.match(/PASS-T\\d/) || [])[0], badge: c.querySelector('.badge')?.textContent,
        rows: [...c.querySelectorAll('.summary-rows > div > span')].map(s => s.textContent), tag: c.querySelector('.pay-tag')?.textContent,
        dots: [...c.querySelectorAll('span')].filter(s => getComputedStyle(s).borderRadius === '50%' && s.getBoundingClientRect().width <= 14).length,
        used: /used ·|remaining/.test(c.textContent) })); })()""")
    by = {x["no"]: x for x in (q or [])}
    check(f"[{label}] passes: three bookings shown as separate cards", len(by) == 3, q)
    check(f"[{label}] passes: pay-later pass ending in 2 days is NOT 'Expiring soon'", by.get("PASS-T1", {}).get("badge") == "Active", by.get("PASS-T1"))
    check(f"[{label}] passes: paid pass keeps its expiry warning", by.get("PASS-T2", {}).get("badge") == "Expiring soon", by.get("PASS-T2"))
    check(f"[{label}] passes: details are only Purchase date and Due date", all(x["rows"] == ["Purchase date", "Due date"] for x in by.values()), q)
    check(f"[{label}] passes: no circles, no used/remaining line", all(x["dots"] == 0 and not x["used"] for x in by.values()), q)
    check(f"[{label}] passes: own payment status each (TO PAY / PAID / TO PAY)",
          [by.get(k, {}).get("tag") for k in ("PASS-T1", "PASS-T2", "PASS-T3")] == ["TO PAY", "PAID", "TO PAY"], q)
    check(f"[{label}] passes: no sideways scroll", js(NO_HSCROLL))
    shot(f"{label}_8_passes")

    # 9. Buy page: real API, no one-pass banner, nothing greyed out
    go("/passes", 6)
    v = js("({ banner: /One pass at a time/.test(document.body.textContent), disabled: [...document.querySelectorAll('.choice-card')].filter(c => c.disabled).length, cards: document.querySelectorAll('.choice-card').length })")
    check(f"[{label}] buy pass: no 'one pass at a time' banner and no greyed-out packs", v and not v["banner"] and v["disabled"] == 0 and v["cards"] >= 1, v)
    shot(f"{label}_9_buy_pass")

errs = js_errors()
check("no JavaScript errors on any page", not errs, errs)
proc.terminate()
print(f"\n{sum(ok for _, ok in results)}/{len(results)} passed")
