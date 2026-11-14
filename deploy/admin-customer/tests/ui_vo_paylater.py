"""Headless Chrome check: Book Workspace -> Virtual Office -> Review & pay offers
"Pay later at the front desk", and using it reserves the VO as an unpaid pay-later pass.
LOCAL stack only (localhost:8080 -> API :81, RegalisWorkSpace_Dev). Writes a pass + invoice:
restore the Dev backup afterwards. Env: PYLIB, SHOTS_DIR, REGALIS_CUST_EMAIL/PASSWORD."""
import base64, json, os, subprocess, sys, time, urllib.request
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket

SHOTS = os.environ.get("SHOTS_DIR", "."); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
def sql(q):
    return [l.split("|") for l in subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True).stdout.strip().splitlines() if l.strip()]

proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9371", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-vo-prof",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9371/json").read()) if t["type"] == "page"); break
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
errs = lambda n: [e for e in events[n:] if e.get("method") == "Runtime.exceptionThrown"]

send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
send("Emulation.setDeviceMetricsOverride", width=1366, height=900, deviceScaleFactor=1, mobile=False)
go("/login"); js("localStorage.clear(); sessionStorage.clear(); true"); go("/login")
SET = "const set=(el,v)=>{Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}))};"
js("(() => {" + SET + f"set(document.querySelector('input[type=email]'),{json.dumps(os.environ['REGALIS_CUST_EMAIL'])});set(document.querySelector('input[type=password]'),{json.dumps(os.environ['REGALIS_CUST_PASSWORD'])});document.querySelector('form button[type=submit]').click();return true}})()")
drain(7)
check("customer signed in", js("location.pathname") == "/", js("location.pathname"))
cust = int(sql(f"SELECT UserId FROM MST_User WHERE Email='{os.environ['REGALIS_CUST_EMAIL']}'")[0][0])
before = int(sql(f"SELECT COUNT(*) FROM TRN_CustomerPass WHERE CustomerId={cust}")[0][0])

n = len(events); go("/book", 6)
check("Virtual Office card on Book Workspace", click(".choice-card, button", "Virtual Office")); drain(2)
click(".choice-card, button", "6 Month") or click(".choice-card, button", "6 months"); drain(1)
for label in ["Continue", "Review", "Next"]:
    if click("button", label): break
drain(3)
t = js("document.body.innerText")
check("Review & pay step for the Virtual Office", "Review & pay" in t and "Virtual Office" in t, t[:500])
btns = js("[...document.querySelectorAll('.pay-options button')].map(b=>b.textContent.trim())")
check("pay options: Wallet, Pay online and Pay later at the front desk", btns and any("Pay later at the front desk" in b for b in btns) and any("Pay online" in b for b in btns), btns)
check("pay-later note shown", "Pay later reserves your Virtual Office now" in t, t[-600:])
shot("vo_review_pay")
click(".pay-options button", "Pay later at the front desk"); drain(4)
t = js("document.body.innerText")
check("confirmation says 'Virtual Office reserved' and 'to pay at the front desk'", "Virtual Office reserved" in t and "to pay at the front desk" in t, t[:600])
shot("vo_reserved")
row = sql(f"SELECT TOP 1 p.PassNumber, p.Status, p.IsPayLater, p.AmountPaid, pl.PlanCode FROM TRN_CustomerPass p JOIN MST_PassPlan pl ON pl.PassPlanId=p.PassPlanId WHERE p.CustomerId={cust} ORDER BY p.CustomerPassId DESC")[0]
check("DB: one new pass, a VO plan, active, IsPayLater=1, nothing paid",
      int(sql(f"SELECT COUNT(*) FROM TRN_CustomerPass WHERE CustomerId={cust}")[0][0]) == before + 1 and row[4].startswith("MEM-VIRTUAL") and row[1] == "1" and row[2] == "1" and float(row[3]) == 0, row)
check("no JS errors", not errs(n), errs(n))
ws.close(); proc.terminate()
print(f"\n{sum(results)}/{len(results)} passed"); sys.exit(0 if all(results) else 1)
