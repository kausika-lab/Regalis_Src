"""Headless Chrome check of the Profile changes (14 Nov 2026): centred header, pencil badge on
the photo (upload shows at once and persists), Role field (saved, shown, visible to admins).
LOCAL stack only. It uploads a test photo and sets a role on the test customer, then puts both
back (PhotoPath / JobTitle to their old values). Env: PYLIB, SHOTS_DIR,
REGALIS_CUST_EMAIL/PASSWORD, REGALIS_ADMIN_EMAIL/PASSWORD."""
import base64, json, os, struct, subprocess, sys, tempfile, time, urllib.request, zlib
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket

SHOTS = os.environ.get("SHOTS_DIR", "."); os.makedirs(SHOTS, exist_ok=True)
BASE, API = "http://localhost:8080", "http://127.0.0.1:81/api"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
def sql(q):
    return [l.split("|") for l in subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True).stdout.strip().splitlines() if l.strip()]

def png(w, h, rgb):  # a plain solid-colour PNG, no imaging library needed
    raw = b"".join(b"\x00" + bytes(rgb) * w for _ in range(h))
    chunk = lambda t, d: struct.pack(">I", len(d)) + t + d + struct.pack(">I", zlib.crc32(t + d) & 0xffffffff)
    return b"\x89PNG\r\n\x1a\n" + chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0)) + chunk(b"IDAT", zlib.compress(raw)) + chunk(b"IEND", b"")
img_path = os.path.join(tempfile.gettempdir(), "rg_test_photo.png")
open(img_path, "wb").write(png(96, 96, (40, 120, 200)))

CE, CP = os.environ["REGALIS_CUST_EMAIL"], os.environ["REGALIS_CUST_PASSWORD"]
uid, old_photo, old_role = sql(f"SELECT UserId, ISNULL(PhotoPath,'NULL'), ISNULL(JobTitle,'NULL') FROM MST_User WHERE Email='{CE}'")[0]

proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9381", f"--user-data-dir={tempfile.gettempdir()}\\rg-prof-ui",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9381/json").read()) if t["type"] == "page"); break
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
    return js(f"(() => {{ const el=[...document.querySelectorAll({json.dumps(sel)})].find(e=>e.textContent.trim().includes({json.dumps(text)})); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
errs = lambda n: [e["params"]["exceptionDetails"].get("text") for e in events[n:] if e.get("method") == "Runtime.exceptionThrown"]
SET = "const set=(el,v)=>{Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(el,v);el.dispatchEvent(new Event('input',{bubbles:true}))};"
def login(path, e, p):
    go(path); js("localStorage.clear(); sessionStorage.clear(); true"); go(path)
    js("(() => {" + SET + f"set(document.querySelector('input[type=email]'),{json.dumps(e)});set(document.querySelector('input[type=password]'),{json.dumps(p)});document.querySelector('form button[type=submit]').click();return true}})()")
    drain(7)
HEAD = """(() => { const h=document.querySelector('.rg-prof-head'), ph=document.querySelector('.rg-prof-photo'), pen=document.querySelector('.rg-prof-photo__edit'),
  pan=document.querySelector('.auth__panel'), txt=document.querySelector('.rg-prof-head__text'); if(!h||!ph||!pen||!pan) return null;
  const a=ph.getBoundingClientRect(), b=pen.getBoundingClientRect(), p=pan.getBoundingClientRect(), t=txt.getBoundingClientRect();
  const c=x=>x.left+x.width/2;
  return { photoCentred: Math.abs(c(a)-c(p))<3, textCentred: Math.abs(c(t)-c(p))<3, textBelow: t.top>=a.bottom-1,
    penBottomRight: b.right>=a.right-6 && b.bottom>=a.bottom-8 && b.left>a.left+a.width/2 && b.top>a.top+a.height/2,
    penSize: Math.round(b.width), penLabel: pen.getAttribute('aria-label'), img: !!document.querySelector('.rg-prof-photo img'),
    overflow: document.documentElement.scrollWidth>innerWidth+1, text: txt.innerText } })()"""

send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True); send("DOM.enable")
send("Emulation.setDeviceMetricsOverride", width=1366, height=900, deviceScaleFactor=1, mobile=False)
login("/login", CE, CP)
check("customer signed in", js("location.pathname") == "/", js("location.pathname"))
try:
    for label, w, h, mob in [("desktop", 1366, 900, False), ("mobile", 390, 844, True)]:
        send("Emulation.setDeviceMetricsOverride", width=w, height=h, deviceScaleFactor=1, mobile=mob)
        n = len(events); go("/profile", 5)
        d = js(HEAD)
        check(f"[{label}] photo centred in the card", d and d["photoCentred"], d)
        check(f"[{label}] name/email centred under the photo", d and d["textCentred"] and d["textBelow"], d)
        check(f"[{label}] pencil button at the photo's bottom-right", d and d["penBottomRight"] and d["penLabel"] == "Change profile picture", d)
        check(f"[{label}] no horizontal scroll", d and not d["overflow"], d)
        check(f"[{label}] Role row on the profile", "Role" in js("document.querySelector('.auth__grid').innerText"))
        check(f"[{label}] no JS errors", not errs(n), errs(n))
        shot(f"{label}_profile_centred")

    send("Emulation.setDeviceMetricsOverride", width=1366, height=900, deviceScaleFactor=1, mobile=False)
    go("/profile", 5)
    # Upload through the pencil: the file input it opens is the one we fill.
    node = send("DOM.querySelector", nodeId=send("DOM.getDocument")["root"]["nodeId"], selector=".rg-prof-head input[type=file]")["nodeId"]
    send("DOM.setFileInputFiles", files=[img_path], nodeId=node); drain(4)
    t = js("document.body.innerText")
    check("upload: 'Photo updated.' and the new picture shows at once", "Photo updated" in t and js("!!document.querySelector('.rg-prof-photo img')"), t[:200])
    check("upload: top-bar avatar shows the picture too", js("!!document.querySelector('.account__avatar img')"))
    check("upload: saved (MST_User.PhotoPath set)", sql(f"SELECT CASE WHEN PhotoPath IS NULL THEN 0 ELSE 1 END FROM MST_User WHERE UserId={uid}")[0][0] == "1")
    go("/profile", 5)
    check("photo still shown after a full reload", js("!!document.querySelector('.rg-prof-photo img')"))
    shot("desktop_profile_photo")

    # Role
    click("button", "Edit Profile"); drain(2)
    has = js("[...document.querySelectorAll('.auth__label')].some(l=>l.textContent.trim().startsWith('Role'))")
    check("Edit Profile has an optional Role input", has)
    js("(() => {" + SET + "const i=[...document.querySelectorAll('input')].find(x=>x.placeholder&&x.placeholder.startsWith('e.g. Founder')); set(i,'Marketing Manager'); return true})()"); drain(0.3)
    shot("desktop_profile_edit_role")
    click("button[type=submit]", "Save") or click("button", "Save"); drain(3)
    check("Role saved in MST_User.JobTitle", sql(f"SELECT JobTitle FROM MST_User WHERE UserId={uid}")[0][0] == "Marketing Manager")
    go("/profile", 5)
    d = js(HEAD)
    check("after reload: role under the name and in the Role row", d and "Marketing Manager" in d["text"] and "Marketing Manager" in js("[...document.querySelectorAll('.auth__grid input')].map(i=>i.value).join('|')+document.querySelector('.auth__grid').innerText"), d)
    shot("desktop_profile_role")
    click("button", "Edit Profile"); drain(2)
    js("(() => {" + SET + "const i=[...document.querySelectorAll('input')].find(x=>x.placeholder&&x.placeholder.startsWith('e.g. Founder')); set(i,'x'.repeat(100)); return i.value.length})()")
    check("Role input stops at 100 characters", js("[...document.querySelectorAll('input')].find(x=>x.placeholder&&x.placeholder.startsWith('e.g. Founder')).maxLength") == 100)

    # Admin side: own role + the customer's role on Admin -> Customers
    login("/admin/login", os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])
    go("/admin/profile", 5)
    d = js(HEAD)
    check("admin profile: same centred header with pencil", d and d["photoCentred"] and d["penBottomRight"], d)
    shot("desktop_admin_profile")
    go(f"/admin/customers/{uid}", 6)
    t = js("document.body.innerText")
    check("Admin -> Customers -> profile shows the customer's Role", "Role" in t and "Marketing Manager" in t, t[:600])
finally:
    sql(f"UPDATE MST_User SET PhotoPath={'NULL' if old_photo == 'NULL' else repr(old_photo)}, JobTitle={'NULL' if old_role == 'NULL' else repr(old_role)} WHERE UserId={uid}")
    ws.close(); proc.terminate()
print(f"\n{sum(results)}/{len(results)} passed"); sys.exit(0 if all(results) else 1)
