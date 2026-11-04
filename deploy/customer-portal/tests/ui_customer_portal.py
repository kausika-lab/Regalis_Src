"""Headless Chrome checks of the customer-portal changes on the LOCAL stack (localhost:8080),
at desktop and phone widths. DOM assertions per requirement + screenshots in shots/."""
import base64, json, os, subprocess, sys, time, urllib.request
sys.path.insert(0, r"C:\Users\santh\AppData\Local\Temp\claude\d--Regalis-Regalis-Src\9dddd6f3-c2eb-49bb-a57d-e0a249c6b507\scratchpad\pylib")
import websocket

HERE = os.path.dirname(os.path.abspath(__file__))
SHOTS = os.path.join(HERE, "shots"); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
PORT = 9341
proc = subprocess.Popen([CHROME, "--headless=new", f"--remote-debugging-port={PORT}", f"--user-data-dir={os.path.join(HERE, 'chrome-prof')}",
                         "--window-size=1366,900", "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen(f"http://127.0.0.1:{PORT}/json").read()) if t["type"] == "page"); break
    except Exception: time.sleep(0.3)
ws = websocket.create_connection(page["webSocketDebuggerUrl"], timeout=60)
mid, events, results = 0, [], []
def send(method, **params):
    global mid; mid += 1
    ws.send(json.dumps({"id": mid, "method": method, "params": params}))
    while True:
        m = json.loads(ws.recv())
        if m.get("id") == mid: return m.get("result", {})
        events.append(m)
def drain(s):
    end = time.time() + s; ws.settimeout(0.3)
    while time.time() < end:
        try: events.append(json.loads(ws.recv()))
        except Exception: pass
    ws.settimeout(60)
def js(expr):
    return send("Runtime.evaluate", expression=expr, awaitPromise=True, returnByValue=True).get("result", {}).get("value")
def go(path, wait=4.0): send("Page.navigate", url=BASE + path); drain(wait)
def shot(name):
    open(os.path.join(SHOTS, name + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png", captureBeyondViewport=True)["data"]))
def check(name, ok, detail=""):
    results.append((name, bool(ok))); print(("PASS " if ok else "FAIL ") + name + ("" if ok else "  -> " + str(detail)[:300]), flush=True)
def errors_since(n):
    out = []
    for ev in events[n:]:
        if ev.get("method") == "Runtime.exceptionThrown":
            d = ev["params"]["exceptionDetails"]; out.append((d.get("exception", {}).get("description") or d.get("text", ""))[:200])
    return out
def size(w, h, mobile):
    send("Emulation.setDeviceMetricsOverride", width=w, height=h, deviceScaleFactor=1, mobile=mobile)
send("Page.enable"); send("Runtime.enable")

GOOGLE = """(() => { const g = document.querySelector('.auth__google');
  if (!g) return {present:false};
  const r = g.getBoundingClientRect(), f = g.querySelector('iframe, div[role=button], div > div');
  const fr = f ? f.getBoundingClientRect() : null;
  return {present:true, visible: r.width > 0 && r.height > 0 && getComputedStyle(g).display !== 'none',
          rendered: !!(fr && fr.width > 0 && fr.height > 0), w: Math.round(r.width), h: Math.round(r.height)}; })()"""

for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
    size(w, h, mob)
    js("localStorage.clear(); sessionStorage.clear(); true")
    go("/login", 7)
    g = js(GOOGLE)
    check(f"[{label}] login: Google sign-in block present and visible", g and g.get("present") and g.get("visible"), g)
    check(f"[{label}] login: Google button rendered by Google's script", g and g.get("rendered"), g)
    shot(f"{label}_login")
    go("/register", 5)
    r = js("""(() => ({ other: [...document.querySelectorAll('option')].some(o => /^other$/i.test(o.textContent.trim())),
        genders: [...document.querySelectorAll('select option')].map(o => o.textContent.trim()),
        dobOptional: [...document.querySelectorAll('.auth__label')].some(l => /date of birth/i.test(l.textContent) && /optional/i.test(l.textContent)),
        google: !!document.querySelector('.auth__google') }))()""")
    check(f"[{label}] register: no 'Other' gender", r and not r["other"], r)
    check(f"[{label}] register: Date of Birth marked optional", r and r["dobOptional"], r)
    shot(f"{label}_register")

size(1366, 900, False)
go("/login", 5)
js("""(() => { const set = (el, v) => { Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value').set.call(el, v); el.dispatchEvent(new Event('input', { bubbles: true })); };
  set(document.querySelector('input[type=email]'), """ + json.dumps(os.environ['REGALIS_TEST_EMAIL']) + """); set(document.querySelector('input[type=password]'), """ + json.dumps(os.environ['REGALIS_TEST_PASSWORD']) + """);
  document.querySelector('form button[type=submit]').click(); return true; })()""")
drain(7)
check("customer signed in through the form", js("location.pathname") == "/", js("location.pathname"))

for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
    size(w, h, mob)
    n = len(events); go("/", 6)
    d = js("""(() => ({ glance: /at a glance/i.test(document.body.innerText),
        nav: [...document.querySelectorAll('.portal__nav a')].map(a => a.getAttribute('href')),
        bell: !!document.querySelector('.portal__bell'),
        prices: [...document.querySelectorAll('.rg-price')].map(e => [e.textContent, getComputedStyle(e).color, getComputedStyle(e).fontWeight]) }))()""")
    check(f"[{label}] dashboard: no 'At a glance'", d and not d["glance"], d)
    check(f"[{label}] sidebar: no /notifications link", d and "/notifications" not in d["nav"] and "/book" in d["nav"], d and d["nav"])
    check(f"[{label}] top bar bell kept", d and d["bell"], d)
    check(f"[{label}] dashboard: wallet balance in dark price style", d and any(c == "rgb(11, 11, 13)" for _, c, _ in d["prices"]), d and d["prices"])
    check(f"[{label}] dashboard: no JS errors", not errors_since(n), errors_since(n))
    shot(f"{label}_dashboard")
    n = len(events); go("/book", 7)
    b = js("""(() => ({ steps: !!document.querySelector('.wizard-steps, .wizard-step'), title: !!document.querySelector('h1,h2'),
        forms: document.querySelectorAll('.form-stack').length, buttons: [...document.querySelectorAll('button')].map(x => x.textContent.trim()).filter(Boolean).slice(0, 12) }))()""")
    check(f"[{label}] book: step indicators gone", b and not b["steps"], b)
    check(f"[{label}] book: first step still rendered", b and b["forms"] > 0, b)
    check(f"[{label}] book: no JS errors", not errors_since(n), errors_since(n))
    shot(f"{label}_book")
    for path in ["/wallet", "/membership", "/day-pass", "/bookings", "/invoices", "/services", "/quotations", "/profile"]:
        n = len(events); go(path, 5)
        p = js("""(() => ({ prices: [...document.querySelectorAll('.rg-price')].slice(0, 4).map(e => [e.textContent, getComputedStyle(e).color]),
            crashed: /something went wrong/i.test(document.body.innerText) }))()""")
        check(f"[{label}] {path}: renders without JS errors", not errors_since(n) and p and not p["crashed"], (errors_since(n), p))
        print("      prices:", p and p["prices"], flush=True)
        shot(f"{label}_{path.strip('/')}")

size(1366, 900, False)
go("/profile", 5)
js("(() => { const b = [...document.querySelectorAll('button')].find(x => /edit profile/i.test(x.textContent)); b && b.click(); return !!b; })()")
drain(2)
pr = js("""(() => ({ other: [...document.querySelectorAll('option')].some(o => /^other$/i.test(o.textContent.trim())),
    dobOptional: [...document.querySelectorAll('.auth__label')].some(l => /date of birth/i.test(l.textContent) && /optional/i.test(l.textContent)) }))()""")
check("profile edit: no 'Other' gender", pr and not pr["other"], pr)
check("profile edit: Date of Birth marked optional", pr and pr["dobOptional"], pr)
shot("desktop_profile_edit")

ws.close(); proc.terminate()
print(f"\n{sum(ok for _, ok in results)}/{len(results)} passed")
