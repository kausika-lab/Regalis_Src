"""Headless Chrome checks for the CRM kit, desktop (1366 px) and phone (390 px).
LOCAL stack only: http://localhost:8080 -> API :81 (local-dev\\api with the kit), Dev DB with
the migration. Signs in as the admin, adds a lead through the dialog, works it on every
screen, submits the public /enquire form, and removes what it created (leads named
"CRM UI ...", hard-deleted at the end).
Env: PYLIB (websocket-client), SHOTS_DIR, REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD."""
import base64, json, os, random, subprocess, sys, time, urllib.request
sys.path.insert(0, os.environ.get("PYLIB", ""))
import websocket

SHOTS = os.environ.get("SHOTS_DIR", "."); os.makedirs(SHOTS, exist_ok=True)
BASE = "http://localhost:8080"
CHROME = r"C:\Program Files\Google\Chrome\Application\chrome.exe"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
def sql(q):
    return [l.split("|") for l in subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True).stdout.strip().splitlines() if l.strip()]
def one(q):
    r = sql(q); return r[0][0] if r else None

proc = subprocess.Popen([CHROME, "--headless=new", "--remote-debugging-port=9373", f"--user-data-dir={os.environ.get('TEMP', '.')}\\rg-crm-prof",
                         "--no-first-run", "--disable-gpu", "about:blank"], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
for _ in range(60):
    try:
        page = next(t for t in json.loads(urllib.request.urlopen("http://127.0.0.1:9373/json").read()) if t["type"] == "page"); break
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
def go(p, w=4): send("Page.navigate", url=BASE + p); drain(w)
def shot(n): open(os.path.join(SHOTS, n + ".png"), "wb").write(base64.b64decode(send("Page.captureScreenshot", format="png")["data"]))
def check(n, ok, d=""): results.append(bool(ok)); print(("PASS " if ok else "FAIL ") + n + ("" if ok else "  -> " + str(d)[:400]), flush=True)
def click(sel, text):
    return js(f"(() => {{ const el=[...document.querySelectorAll({json.dumps(sel)})].find(e=>e.textContent.trim().includes({json.dumps(text)}) && !e.disabled); if(!el) return false; el.scrollIntoView({{block:'center'}}); el.click(); return true; }})()")
def text(): return js("document.body.innerText") or ""
def no_hscroll(): return js("document.documentElement.scrollWidth <= window.innerWidth + 1")
def exceptions(n): return [e["params"]["exceptionDetails"].get("exception", {}).get("description", "")[:200] for e in events[n:] if e.get("method") == "Runtime.exceptionThrown"]
SET = ("const setv=(el,v)=>{const P=el.tagName==='TEXTAREA'?HTMLTextAreaElement.prototype:el.tagName==='SELECT'?HTMLSelectElement.prototype:HTMLInputElement.prototype;"
       "Object.getOwnPropertyDescriptor(P,'value').set.call(el,v);el.dispatchEvent(new Event(el.tagName==='SELECT'?'change':'input',{bubbles:true}))};"
       "const byLabel=(t,root)=>{const l=[...(root||document).querySelectorAll('label')].find(l=>l.textContent.trim().replace(/\\s*\\*$/,'').startsWith(t));if(!l)return null;return l.htmlFor?document.getElementById(l.htmlFor):l.querySelector('input,select,textarea')};")
def fill(label, value, root="document.querySelector('.modal')||document"):
    return js(f"(() => {{{SET} const el=byLabel({json.dumps(label)},{root}); if(!el) return false; setv(el,{json.dumps(value)}); return true }})()")
def viewport(w):
    send("Emulation.setDeviceMetricsOverride", width=w, height=900 if w > 500 else 844, deviceScaleFactor=1, mobile=w < 500)

send("Page.enable"); send("Runtime.enable"); send("Network.enable"); send("Network.setBypassServiceWorker", bypass=True)
# The API drops "HeadlessChrome" agents as bots (by design), so look like an ordinary browser.
send("Network.setUserAgentOverride", userAgent="Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36")
viewport(1366)

# ---- public /enquire (signed out) ---------------------------------------------------------------
go("/login", 3); js("localStorage.clear(); sessionStorage.clear(); document.cookie='rg_vid=; max-age=0; path=/'; true")
go("/login?utm_source=instagram&utm_medium=bio", 4)
n0 = len(events)
check("sign-in page shows 'Book a tour' link", "Book a tour" in text())
check("tracker set the visitor cookie", bool(js("document.cookie.includes('rg_vid=')")), js("document.cookie"))
click("a", "Book a tour"); drain(4)
check("/enquire opens from the sign-in page", js("location.pathname") == "/enquire" and "See the space before you decide" in text(), text()[:300])
shot("enquire_desktop")
mob = "90003" + RUN
js("(() => {" + SET + f"""const f=document.querySelector('form[aria-label="Book a tour"]');
  setv(byLabel('Your name',f),'CRM UI Web {RUN}');setv(byLabel('Mobile number',f),'{mob}');
  setv(byLabel('Anything else',f),'Two desks from next month');return true}})()""")
check("submit disabled until consent ticked", js("document.querySelector('.enq__btn').disabled") is True)
js("document.getElementById('enq-consent').click(); true"); drain(3.5)  # also passes the 2.5 s timing check
js("document.querySelector('.enq__btn').click(); true"); drain(3)
check("enquiry accepted with the doc's message", "we will call you back today" in text(), text()[:400])
check("web lead created with journey (visitor attached, source Instagram)",
      one(f"SELECT s.Code+':'+CAST(CASE WHEN l.VisitorId IS NULL THEN 0 ELSE 1 END AS varchar) FROM TRN_CrmLead l JOIN MST_CrmSource s ON s.SourceId=l.SourceId WHERE l.MobileNormalised='{mob}'") == "INSTAGRAM:1")
viewport(390); go("/enquire", 3)
check("/enquire fits a phone (no sideways scroll)", no_hscroll()); shot("enquire_phone"); viewport(1366)
check("no JS errors on public pages", not exceptions(n0), exceptions(n0))

# ---- admin sign-in ------------------------------------------------------------------------------
go("/admin/login", 3)
js("(() => {" + SET + f"setv(document.querySelector('input[type=email]'),{json.dumps(os.environ['REGALIS_ADMIN_EMAIL'])});setv(document.querySelector('input[type=password]'),{json.dumps(os.environ['REGALIS_ADMIN_PASSWORD'])});document.querySelector('form button[type=submit]').click();return true}})()")
drain(7)
check("admin signed in", (js("location.pathname") or "").startswith("/admin"), js("location.pathname"))
n1 = len(events)
t = text()
check("sidebar shows the SALES band with CRM", "SALES" in t.upper() and "CRM" in t, t[:600])
check("CRM expands to Leads / Follow-ups / Website Visitors / CRM Reports", js("[...document.querySelectorAll('a')].map(a=>a.getAttribute('href')).filter(h=>h&&h.startsWith('/admin/crm/')).length") >= 4, js("[...document.querySelectorAll('a')].map(a=>a.getAttribute('href'))"))
check("Dashboard has + Add lead", "+ Add lead" in t)

# ---- add a lead by hand from the Leads screen ---------------------------------------------------
go("/admin/crm/leads", 5)
t = text()
check("Leads screen: five figures", all(x.upper() in t.upper() for x in ["New this week", "Awaiting first contact", "Follow-up due today", "Converted this month", "Conversion rate"]), t[:500])
check("Leads list shows the web lead", f"CRM UI Web {RUN}" in t)
shot("leads_list")
click("button", "+ Add lead"); drain(2)
check("Add lead dialog opens with chips", "How did they reach us" in text() and "Walk-in" in text())
click(".crm-chip", "Walk-in"); drain(0.5)
check("Walk-in reveals 'Visited on' and 'Shown around by'", "Visited on" in text() and "Shown around by" in text())
click(".crm-chip", "Phone call"); drain(0.5)
check("other chips hide them again", "Shown around by" not in text())
fill("Name", f"CRM UI Phone {RUN}"); fill("Mobile", f"+91 {mob[:5]} {mob[5:]}"); drain(2)
check("duplicate panel appears for the web lead's number", "We already have this mobile" in text() and "It is a different person" in text(), text()[-900:])
click("button", "It is a different person"); drain(0.5)
fill("What they said", "Asked about a 4-seat cabin")
js("(() => {" + SET + "const s=byLabel('Looking for',document.querySelector('.modal'));const o=[...s.options].find(o=>o.text==='Cabin');setv(s,o.value);return true})()"); drain(0.5)
t = text()
check("green panel explains what happens, nothing sent", "No email or message is sent" in t and "Contacted" in t, t[-600:])
shot("add_lead_dialog")
check("Add lead dialog has no sideways scroll", js("(()=>{const b=document.querySelector('.modal__body');return b.scrollWidth<=b.clientWidth+1})()"))
viewport(390); drain(1)
check("Add lead dialog fits a phone", js("(()=>{const b=document.querySelector('.modal__body');return b.scrollWidth<=b.clientWidth+1})()") and no_hscroll())
shot("add_lead_phone"); viewport(1366); drain(1)
click(".modal button", "Save lead"); drain(4)
check("saved and opened the lead", "/admin/crm/leads/" in (js("location.pathname") or ""), js("location.pathname"))
lead_id = (js("location.pathname") or "").rsplit("/", 1)[-1]
t = text()
check("lead screen: tabs, rail and 'no website history' line", all(x in t for x in ["Overview", "Consent & data", "Next follow-up", "Linked records", "No website history"]), t[:900])
shot("lead_hand_entered")

# ---- work the lead -------------------------------------------------------------------------------
click("button", "Book a tour"); drain(1.5)
click(".modal button", "Book the tour"); drain(3)
check("Book a tour -> Visit booked", "Visit booked" in text())
click("button", "Send quotation"); drain(5)
check("Send quotation opens the quotation form prefilled with the lead number",
      "/admin/billing/quotations/new" in js("location.pathname + location.search") and js(f"[...document.querySelectorAll('input')].some(i=>i.value.startsWith('L-'))"), js("location.pathname + location.search"))
shot("quotation_prefill")
go(f"/admin/crm/leads/{lead_id}", 4)
click("button", "Convert to customer"); drain(3)
check("convert dialog checks Customers first", "Checked the Customers module" in text() or "already a customer" in text(), text()[-500:])
click(".modal button", "Cancel"); drain(1)

# web lead journey
web_id = one(f"SELECT LeadId FROM TRN_CrmLead WHERE MobileNormalised='{mob}' AND Channel=2")
go(f"/admin/crm/leads/{web_id}", 5)
t = text()
check("web lead shows the journey and the honest limitation line", "Where this lead came from" in t and "recorded anonymously against visitor" in t and "Instagram" in t, t[:1200])
shot("lead_web_journey")

# ---- pipeline, follow-ups, visitors, reports, settings -----------------------------------------
go("/admin/crm/leads", 4); click(".crm-toggle button", "Pipeline"); drain(3)
t = text()
check("pipeline shows the stages", all(x in t for x in ["New", "Contacted", "Visit booked", "Quotation sent", "Won", "Lost"]))
shot("pipeline")
js("localStorage.setItem('rg-crm-view','list'); true")
go("/admin/crm/follow-ups", 5)
t = text()
check("Follow-ups: three columns and four figures", all(x.upper() in t.upper() for x in ["Overdue", "Today", "Next 7 days", "Average first reply"]), t[:500])
shot("followups")
go("/admin/crm/visitors", 5)
t = text()
check("Website Visitors: honest panel, sources and sessions", "What a visit can and cannot tell you" in t and "Where the visits came from" in t and "V-" in t, t[:600])
shot("visitors")
go("/admin/crm/reports", 6)
t = text()
check("CRM Reports: four reports", all(x in t for x in ["Which source actually brings paying customers", "Where people drop out", "How fast we reply", "Owner performance"]), t[:600])
shot("reports")
go("/admin/administration/crm-settings", 6)
t = text()
check("CRM Settings: every section", all(x in t for x in ["Lead sources", "Statuses", "Lost reasons", "Looking for", "Response targets", "Assignment rules", "Who handles leads", "Consent and retention", "Email notifications"]), t[:600])
shot("settings")
check("no JS errors on the admin CRM screens", not exceptions(n1), exceptions(n1))

# ---- phone width ---------------------------------------------------------------------------------
viewport(390)
for p in ["/admin/crm/leads", f"/admin/crm/leads/{lead_id}", "/admin/crm/follow-ups", "/admin/crm/visitors", "/admin/crm/reports", "/admin/administration/crm-settings"]:
    go(p, 4)
    check(f"390px: {p} no sideways page scroll", no_hscroll(), js("document.documentElement.scrollWidth"))
shot("lead_phone")
viewport(1366)

# ---- existing screens still load ----------------------------------------------------------------
n2 = len(events)
for p, needle in [("/admin", "Today's Bookings"), ("/admin/customers", "Customers"), ("/admin/billing/quotations", "Quotations"), ("/admin/waitlist", "Waiting List")]:
    go(p, 4)
    check(f"existing page still works: {p}", needle in text(), text()[:200])
go("/admin/billing/quotations/new", 5)
check("new quotation without ?lead= opens empty, as before", not js("[...document.querySelectorAll('input')].some(i=>i.value.startsWith('L-'))"))
check("no JS errors on existing pages", not exceptions(n2), exceptions(n2))

# ---- clean up -------------------------------------------------------------------------------------
sql(f"""DECLARE @ids TABLE (id bigint); INSERT INTO @ids SELECT LeadId FROM TRN_CrmLead WHERE Name LIKE 'CRM UI%{RUN}';
DECLARE @v TABLE (id bigint); INSERT INTO @v SELECT VisitorId FROM TRN_CrmLead WHERE LeadId IN (SELECT id FROM @ids) AND VisitorId IS NOT NULL;
DELETE FROM TRN_CrmLeadActivity WHERE LeadId IN (SELECT id FROM @ids); DELETE FROM TRN_CrmFollowUp WHERE LeadId IN (SELECT id FROM @ids);
DELETE FROM TRN_CrmLeadConsent WHERE LeadId IN (SELECT id FROM @ids); DELETE FROM TRN_CrmNotification WHERE LeadId IN (SELECT id FROM @ids);
DELETE FROM TRN_CrmWhatsAppRef WHERE VisitorId IN (SELECT id FROM @v); DELETE FROM TRN_CrmPageView WHERE VisitorId IN (SELECT id FROM @v);
DELETE FROM TRN_CrmVisitor WHERE VisitorId IN (SELECT id FROM @v); DELETE FROM TRN_CrmLead WHERE LeadId IN (SELECT id FROM @ids);""")
ws.close(); proc.terminate()
print(f"\n{sum(results)}/{len(results)} passed")
sys.exit(0 if all(results) else 1)
