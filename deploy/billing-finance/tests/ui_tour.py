"""Headless Chrome tour of the Billing & Finance pages over the DevTools protocol.
Logs in as the admin, opens each route, waits, screenshots, and reports JS errors and
error toasts. Test data only (disposable RegalisWorkSpace_BFTest)."""
import base64, json, os, subprocess, sys, time, urllib.request
sys.path.insert(0, r"C:\Users\santh\AppData\Local\Temp\claude\d--Regalis-Regalis-Src\9dddd6f3-c2eb-49bb-a57d-e0a249c6b507\scratchpad\pylib")
import websocket

ADMIN_EMAIL = os.environ.get("REGALIS_TEST_EMAIL", "admin@regalis.in")
ADMIN_PASSWORD = os.environ["REGALIS_TEST_PASSWORD"]  # local Dev admin password; never committed

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.path.join(HERE, "..", "shots")
os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8093"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
PORT = 9333
prof = os.path.join(HERE, "..", "chrome-prof")
proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={PORT}", f"--user-data-dir={prof}", "--window-size=1440,1100",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(50):
    try:
        targets = json.loads(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json").read())
        page = next(t for t in targets if t["type"] == "page")
        break
    except Exception:
        time.sleep(0.3)
ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60)
msg_id = 0
events = []
def send(method, **params):
    global msg_id
    msg_id += 1
    ws.send(json.dumps({"id": msg_id, "method": method, "params": params}))
    while True:
        m = json.loads(ws.recv())
        if m.get("id") == msg_id:
            return m.get("result", {})
        events.append(m)
def drain(seconds):
    end = time.time() + seconds
    ws.settimeout(0.3)
    while time.time() < end:
        try:
            events.append(json.loads(ws.recv()))
        except Exception:
            pass
    ws.settimeout(60)
def js(expr):
    r = send("Runtime.evaluate", expression=expr, awaitPromise=True, returnByValue=True)
    return r.get("result", {}).get("value")
send("Page.enable"); send("Runtime.enable"); send("Log.enable")

def errors_since(n):
    out = []
    for ev in events[n:]:
        m = ev.get("method")
        if m == "Runtime.exceptionThrown":
            d = ev["params"]["exceptionDetails"]
            out.append("EXC " + (d.get("exception", {}).get("description") or d.get("text", ""))[:300])
        elif m == "Runtime.consoleAPICalled" and ev["params"]["type"] == "error":
            out.append("CONSOLE " + " ".join(str(a.get("value", a.get("description", ""))) for a in ev["params"]["args"])[:300])
        elif m == "Log.entryAdded" and ev["params"]["entry"]["level"] == "error":
            e = ev["params"]["entry"]
            out.append(f"LOG {e.get('text','')[:200]} {e.get('url','')[-80:]}")
    return out

def go(path, wait=4.0):
    send("Page.navigate", url=BASE + path)
    drain(wait)

def shot(name):
    data = send("Page.captureScreenshot", format="png", captureBeyondViewport=True)["data"]
    open(os.path.join(SHOTS, name + ".png"), "wb").write(base64.b64decode(data))

# Login through the form.
go("/admin/login", 5)
js("""(() => {
  const set = (el, v) => { const p = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value'); p.set.call(el, v); el.dispatchEvent(new Event('input', { bubbles: true })); };
  set(document.querySelector('input[type=email]') || document.querySelector('input[name=email]') || document.querySelectorAll('input')[0], """ + json.dumps(ADMIN_EMAIL) + """);
  set(document.querySelector('input[type=password]'), """ + json.dumps(ADMIN_PASSWORD) + """);
  (document.querySelector('form button[type=submit]') || [...document.querySelectorAll('button')].find(b => /sign in/i.test(b.textContent))).click();
  return true; })()""")
drain(6)
print("after login:", js("location.pathname"))

ROUTES = sys.argv[1:] or [
    "/admin/billing/invoices", "/admin/billing/invoices/10003", "/admin/billing/payments", "/admin/billing/expenses",
    "/admin/billing/purchase-bills", "/admin/billing/purchase-bills/new", "/admin/billing/purchase-bills/1", "/admin/billing/vendor-payments",
    "/admin/billing/suppliers", "/admin/billing/transactions", "/admin/billing/reconciliation", "/admin/billing/bank-accounts",
    "/admin/billing/petty-cash", "/admin/billing/gst", "/admin/billing/tds", "/admin/billing/compliance", "/admin/billing/period-locks",
    "/admin/billing/ledgers", "/admin/billing/ledger/customer/10032", "/admin/billing/ledger/supplier/22", "/admin/billing/cash-collections",
    "/admin/billing/purchase-orders", "/admin/customers"]
problems = 0
for r in ROUTES:
    n = len(events)
    go(r, 5)
    name = r.strip("/").replace("/", "_")
    shot(name)
    title = js("(document.querySelector('.page-header__title')||{}).textContent || ''")
    toasts = js("[...document.querySelectorAll('[class*=toast]')].map(t => t.textContent).filter(Boolean).join(' | ')")
    errs = errors_since(n)
    blank = js("document.querySelector('main') ? document.querySelector('main').innerText.trim().length : document.body.innerText.trim().length")
    status = "OK " if not errs and blank else "BAD"
    if status == "BAD": problems += 1
    print(f"{status} {r} :: title={title!r} text={blank} toasts={toasts!r}")
    for e in errs:
        print("     ", e)
sidebar = js("[...document.querySelectorAll('.portal__subnav-band, .portal__subnav-link')].map(e => (e.classList.contains('portal__subnav-band') ? '## ' : '') + e.textContent.trim()).join(' / ')")
print("SIDEBAR:", sidebar)
ws.close()
proc.terminate()
print(f"{problems} pages with problems")
