"""End-to-end API checks for the CRM kit (19 Nov 2026), against the LOCAL Dev stack.

  API  http://127.0.0.1:81  (local-dev\\api with the CRM kit DLLs)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev, migration 20261119120000_Crm applied

Creates a throwaway Sales user (crm-e2e-sales@example.test), leads whose names start with
"CRM E2E" and mobiles 90000xxxxx, and one customer (crm-e2e-cust@example.test). Everything it
made is removed at the end (leads hard-deleted; the users are soft-deleted by the API).
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD.

usage: python -I e2e_crm.py
"""
import datetime as dt, json, os, random, subprocess, sys, time, urllib.request, urllib.error

API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
results = []
RUN = str(random.randint(10000, 99999))


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {detail}" if detail and not ok else ""), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:160] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


def call(method, path, token=None, body=None, params=None, headers=None):
    """The API allows 60 writes a minute per user: wait and retry on 429."""
    for _ in range(8):
        s, b = _call(method, path, token, body, params, headers)
        if s != 429:
            return s, b
        time.sleep(15)
    return s, b


def _call(method, path, token=None, body=None, params=None, headers=None):
    url = API + path
    if params:
        url += "?" + "&".join(f"{k}={v}" for k, v in params.items() if v is not None)
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("User-Agent", "Mozilla/5.0 (Linux; Android 14) Chrome/130.0 Mobile")
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            raw = r.read()
            try:
                return r.status, json.loads(raw or b"{}")
            except Exception:
                return r.status, {"_bytes": len(raw)}
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read() or b"{}")
        except Exception:
            return e.code, {}


def login(email, password, portal="admin"):
    for _ in range(6):
        s, b = call("POST", "/auth/login", body={"email": email, "password": password, "portal": portal})
        if s == 200:
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email} failed: {s} {b}")
    raise SystemExit("login rate limited")


def iso(d):
    return d.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def purge_leftovers():
    """Removes what an earlier interrupted run left behind (leads hard, users soft)."""
    sql("DECLARE @ids TABLE (id bigint); INSERT INTO @ids SELECT LeadId FROM TRN_CrmLead WHERE Name LIKE 'CRM E2E%';"
        "DELETE FROM TRN_CrmLeadActivity WHERE LeadId IN (SELECT id FROM @ids); DELETE FROM TRN_CrmFollowUp WHERE LeadId IN (SELECT id FROM @ids);"
        "DELETE FROM TRN_CrmLeadConsent WHERE LeadId IN (SELECT id FROM @ids); DELETE FROM TRN_CrmNotification WHERE LeadId IN (SELECT id FROM @ids);"
        "DELETE FROM TRN_CrmLead WHERE LeadId IN (SELECT id FROM @ids);"
        "DELETE FROM MST_CrmTeamMember WHERE UserId IN (SELECT UserId FROM MST_User WHERE Email LIKE 'crm-e2e-%@example.test');"
        "UPDATE MST_User SET IsDeleted=1, IsActive=0, Status=2 WHERE Email LIKE 'crm-e2e-%@example.test' AND IsDeleted=0;")


now = dt.datetime.now(dt.timezone.utc)
purge_leftovers()
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])
made_leads = []

# ---- options, permissions, menus --------------------------------------------------------
s, b = call("GET", "/crm/options", admin)
check("options load for admin", s == 200 and len(b["data"]["sources"]) >= 15, s)
opt = b["data"]
src = {x["code"]: x["sourceId"] for x in opt["sources"]}
st = {x["code"]: x["statusId"] for x in opt["statuses"]}
reasons = opt["lostReasons"]
looking = {x["name"]: x["lookingForId"] for x in opt["lookingFor"]}
check("admin has every CRM permission", all(opt["me"][k] for k in ["canViewAll", "canAssign", "canConvert", "canDelete", "canChangeSettings"]))
check("email notifications OFF by default", opt["settings"]["emailEnabled"] is False)

s, b = call("GET", "/menus/my", admin)
flat = json.dumps(b)
check("admin sidebar has SALES band and CRM rows", "ADM_SEC_SALES" in flat and "ADM_CRM_LEADS" in flat and "ADM_CRM_SETTINGS" in flat, s)

# Sales user
sales_role = one("SELECT RoleId FROM MST_Role WHERE RoleCode='SALES'")
pw = "Crm#E2e" + RUN + "x"
s, b = call("POST", "/users", admin, {"fullName": "CRM E2E Sales", "email": f"crm-e2e-sales{RUN}@example.test", "mobileNumber": f"+9190001{RUN}",
                                     "password": pw, "userType": 3, "roleIds": [int(sales_role)], "companyName": None, "defaultLocationId": None})
check("create a Sales / Front desk user", s in (200, 201), b)
sales_id = b.get("data", {}).get("userId")
sql(f"UPDATE MST_User SET MustChangePassword=0 WHERE UserId={sales_id}")
sales = login(f"crm-e2e-sales{RUN}@example.test", pw)
s, b = call("GET", "/crm/options", sales)
check("sales: CRM opens, no view-all/assign/convert/delete/settings", s == 200 and not any(b["data"]["me"][k] for k in ["canViewAll", "canAssign", "canConvert", "canDelete", "canChangeSettings"]))
s, _ = call("GET", "/crm/visitors", sales)
check("sales: Website Visitors refused (403)", s == 403, s)
s, _ = call("GET", "/crm/reports/sources", sales)
check("sales: reports refused (403)", s == 403, s)
s, _ = call("GET", "/crm/settings", sales)
check("sales: settings refused (403)", s == 403, s)
s, b = call("GET", "/menus/my", sales)
flat = json.dumps(b)
check("sales sidebar: Leads + Follow-ups, no Visitors/Reports/Settings", "ADM_CRM_LEADS" in flat and "ADM_CRM_FOLLOWUPS" in flat and "ADM_CRM_VISITORS" not in flat and "ADM_CRM_SETTINGS" not in flat)

# ---- add a lead by hand: validation --------------------------------------------------------
base = {"sourceId": src["PHONE"], "name": "CRM E2E Priya " + RUN, "mobile": f"+91 90000 {RUN[:5]}", "assignTo": "me"}
mob10 = "90000" + RUN[:5]
for label, patch, msg in [
    ("no chip", {"sourceId": None}, "Choose how they reached us."),
    ("no name", {"name": ""}, "Enter their name."),
    ("no mobile", {"mobile": ""}, "Enter a mobile number"),
    ("bad mobile", {"mobile": "12345"}, "does not look like a mobile number"),
    ("bad email", {"email": "x@"}, "Check the email address."),
    ("follow-up in the past", {"followUpUtc": iso(now - dt.timedelta(days=1)), "followUpWhat": "Call"}, "cannot be in the past"),
    ("follow-up with no description", {"followUpUtc": iso(now + dt.timedelta(days=1)), "followUpWhat": ""}, "Say what needs to be done"),
]:
    s, b = call("POST", "/crm/leads", sales, {**base, **patch})
    check(f"validation: {label}", s == 422 and msg in b.get("message", ""), f"{s} {b.get('message')}")

s, b = call("POST", "/crm/leads", sales, {**base, "company": "Nimbus Design", "lookingForId": looking["Cabin"], "peopleCount": 4, "budgetText": "around 45k",
                                         "whatTheySaid": "Wants a 4-seat cabin from December", "heardFrom": "Instagram",
                                         "followUpUtc": iso(now + dt.timedelta(days=1)), "followUpWhat": "Call back with cabin prices"})
check("phone lead created by Sales", s == 200, b)
lead = b["data"]
lid = lead["row"]["leadId"]
made_leads.append(lid)
check("lead number L-xxxxxx", lead["row"]["leadNumber"].startswith("L-"))
check("phone lead is Contacted at once (spoken on the call)", lead["row"]["status"] == "Contacted" and lead["firstContactUtc"])
check("owner = the Sales user", lead["row"]["ownerUserId"] == sales_id)
check("mobile normalised to last 10 digits", one(f"SELECT MobileNormalised FROM TRN_CrmLead WHERE LeadId={lid}") == mob10)
check("follow-up created", any(not f["isDone"] for f in lead["followUps"]))
check("consent says nothing automatic is sent", lead["consent"] and lead["consent"]["contactConsent"] is False)
check("no journey panel on a hand-entered lead", lead["journey"] is None)

# Duplicate check
s, b = call("POST", "/crm/leads/check-duplicate", sales, {"mobile": f"0{mob10}"})
check("duplicate check finds the lead from 0-prefixed number", s == 200 and any(x["leadId"] == lid for x in b["data"]["leads"]))
s, b = call("POST", "/crm/leads", sales, {**base, "sourceId": src["EMAIL"], "mobile": f"({mob10[:3]}) {mob10[3:]}"})
check("second lead on same number refused with 409 + match", s == 409 and b["data"]["leads"][0]["leadId"] == lid, s)
s, b = call("POST", "/crm/leads", sales, {**base, "sourceId": src["EMAIL"], "mobile": mob10, "allowDuplicate": True, "assignTo": "rules"})
check("'It is a different person' creates it", s == 200)
lid2 = b["data"]["row"]["leadId"]
made_leads.append(lid2)
check("email lead starts New with a response clock", b["data"]["row"]["status"] == "New" and b["data"]["responseDueUtc"])

# Sales sees own + unassigned only
s, b = call("POST", "/crm/leads", admin, {**base, "name": "CRM E2E Other " + RUN, "mobile": "90001" + RUN[:5], "assignTo": str(one("SELECT TOP 1 UserId FROM MST_User WHERE Email='" + os.environ['REGALIS_ADMIN_EMAIL'] + "'"))})
other = b["data"]["row"]["leadId"]
made_leads.append(other)
s, _ = call("GET", f"/crm/leads/{other}", sales)
check("sales cannot open someone else's lead (404)", s == 404, s)
s, b = call("GET", "/crm/leads", sales, params={"days": 0, "pageSize": 200})
ids = [r["leadId"] for r in b["data"]["page"]["items"]]
check("sales list = own + unassigned only", lid in ids and other not in ids)
s, b = call("GET", "/crm/leads", admin, params={"search": mob10[-6:], "days": 0})
check("admin search by last digits finds both", s == 200 and {lid, lid2} <= {r["leadId"] for r in b["data"]["page"]["items"]})
check("summary present", "awaitingFirstContact" in b["data"]["summary"])

# ---- working the lead -------------------------------------------------------------------------
s, b = call("POST", f"/crm/leads/{lid2}/status", sales, {"statusId": st["CONTACTED"]})
check("Contacted without activity refused", s == 422 and "Log the call or email first" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid2}/activities", sales, {"kind": "call", "detail": "Called, asked about desks"})
check("logging a call moves New -> Contacted", s == 200 and b["data"]["row"]["status"] == "Contacted")
s, b = call("PUT", f"/crm/leads/{lid2}/activities/1", sales, {})
check("activity log edit refused with the doc message", s == 422 and "cannot be edited" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid}/status", sales, {"statusId": st["VISIT"]})
check("Visit booked without a date refused", s == 422 and "date and time of the visit" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid}/tour", sales, {"tourUtc": iso(now + dt.timedelta(days=2)), "note": "Bring the floor plan"})
check("Book a tour -> Visit booked + follow-up", s == 200 and b["data"]["row"]["status"] == "Visit booked" and b["data"]["tourUtc"])
s, b = call("POST", f"/crm/leads/{lid}/status", sales, {"statusId": st["QUOTE"]})
check("Quotation sent without quotation refused", s == 422 and "Link the quotation first" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid}/status", sales, {"statusId": st["WON"]})
check("Won by dropdown refused (use Convert)", s == 422)
s, b = call("POST", f"/crm/leads/{lid}/lose", sales, {})
check("Lost with no reason refused", s == 422 and "Choose why this lead was lost" in b.get("message", ""))

# Follow-up done needs a next one
fu = [f for f in call("GET", f"/crm/leads/{lid}", sales)[1]["data"]["followUps"] if not f["isDone"]]
first_fu = sorted(fu, key=lambda f: f["dueUtc"])[0]["followUpId"]
sql(f"DELETE FROM TRN_CrmFollowUp WHERE LeadId={lid} AND FollowUpId<>{first_fu}")
s, b = call("POST", f"/crm/follow-ups/{first_fu}/done", sales, {"outcome": "spoke"})
check("follow-up done without a next one refused", s == 422 and "never left without a next step" in b.get("message", ""))
s, b = call("POST", f"/crm/follow-ups/{first_fu}/done", sales, {"outcome": "spoke", "note": "Liked the cabin",
                                                                  "next": {"dueUtc": iso(now + dt.timedelta(days=3)), "whatToDo": "Send the quotation", "kind": "email"}})
check("follow-up done + next one set", s == 200 and sum(1 for f in b["data"]["followUps"] if not f["isDone"]) == 1)

# Assign rules
s, b = call("POST", f"/crm/leads/{lid}/assign", sales, {"userId": None})
check("sales cannot reassign (403)", s == 403, s)
s, b = call("POST", f"/crm/leads/{other}/assign", admin, {"userId": sales_id})
check("assign to someone not on duty asks first", s == 422 and "not on duty today. Assign anyway?" in b.get("message", ""), b.get("message"))
s, b = call("POST", f"/crm/leads/{other}/assign", admin, {"userId": sales_id, "force": True})
check("assign anyway works and is logged", s == 200 and any("Reassigned" in a["detail"] for a in b["data"]["activities"]))

# Board
s, b = call("GET", "/crm/follow-ups", sales)
check("follow-ups board loads with three columns", s == 200 and all(k in b["data"] for k in ["overdue", "today", "week"]))

# Pipeline
s, b = call("GET", "/crm/leads", admin, params={"preset": "open", "pageSize": 500})
check("pipeline (open preset) loads", s == 200)

# Edit closed lead refused; lose + reopen
r0 = reasons[0]["lostReasonId"]
s, b = call("POST", f"/crm/leads/{lid2}/lose", sales, {"lostReasonId": r0, "note": "Too pricey"})
check("lose with reason closes, follow-ups closed", s == 200 and b["data"]["row"]["isClosed"] and all(f["isDone"] for f in b["data"]["followUps"]))
s, b = call("PUT", f"/crm/leads/{lid2}", sales, {"name": "x", "mobile": mob10})
check("editing a closed lead refused", s == 422 and "Reopen it first" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid2}/reopen", sales, {"reason": "They called back"})
check("reopen -> Contacted and logged", s == 200 and b["data"]["row"]["status"] == "Contacted" and any(a["kindName"] == "Reopened" for a in b["data"]["activities"]))

# ---- conversion ------------------------------------------------------------------------------
s, _ = call("POST", f"/crm/leads/{lid}/convert", sales, {})
check("sales cannot convert (403)", s == 403, s)
s, b = call("GET", f"/crm/leads/{lid}/convert", admin)
check("convert preview: no existing customer", s == 200 and b["data"]["existingCustomer"] is None)
s, b = call("POST", f"/crm/leads/{lid}/convert", admin, {"customerType": "company", "createCustomer": True})
check("convert without email refused, lead unchanged", s == 422 and "has not been changed" in b.get("message", ""))
cust_email = f"crm-e2e-cust{RUN}@example.test"
s, b = call("POST", f"/crm/leads/{lid}/convert", admin, {"email": cust_email, "customerType": "company", "createCustomer": True, "carryQuotation": True})
check("convert creates customer through the Customers module", s == 200 and b["data"]["customerId"] > 0 and not b["data"]["usedExistingCustomer"], b)
cust_id = b["data"]["customerId"] if s == 200 else None
check("customer row is a customer with a wallet", one(f"SELECT COUNT(*) FROM MST_User u JOIN MST_Wallet w ON w.CustomerId=u.UserId WHERE u.UserId={cust_id} AND u.UserType=1") == "1")
s, b = call("GET", f"/crm/leads/{lid}", admin)
check("lead closed as Won, kept, linked to customer", b["data"]["row"]["isWon"] and b["data"]["row"]["customerId"] == cust_id)
s, b = call("POST", f"/crm/leads/{lid}/convert", admin, {"email": cust_email})
check("already converted message", s == 422 and "was converted on" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid}/reopen", admin, {})
check("converting cannot be undone", s == 422 and "cannot be undone" in b.get("message", ""))
s, b = call("POST", f"/crm/leads/{lid2}/convert", admin, {"email": cust_email})
check("second lead on the same number attaches to the existing customer", s == 200 and b["data"]["usedExistingCustomer"] and b["data"]["customerId"] == cust_id)
s, b = call("POST", "/crm/leads/check-duplicate", admin, {"mobile": mob10})
check("duplicate check now lists the customer too", s == 200 and any(c["customerId"] == cust_id for c in b["data"]["customers"]))

# ---- public website ---------------------------------------------------------------------------
s, b = call("GET", "/public/crm-config")
check("public config without login", s == 200 and "consentText" in b["data"])
s, b = call("POST", "/public/visits", None, {"path": "/enquire?token=SECRET", "referrer": "https://www.google.com/search?q=coworking", "newVisit": True})
key = b["data"]["visitorKey"]
check("anonymous visit recorded", s == 200 and key and b["data"]["pageViewId"])
vid = one(f"SELECT VisitorId FROM TRN_CrmVisitor WHERE VisitorKey='{key}'")
check("query string never stored", one(f"SELECT COUNT(*) FROM TRN_CrmPageView WHERE VisitorId={vid} AND Path LIKE '%token%'") == "0")
check("source from referrer = Google search", one(f"SELECT s.Code FROM TRN_CrmVisitor v JOIN MST_CrmSource s ON s.SourceId=v.SourceId WHERE v.VisitorId={vid}") == "GOOGLE")
pv = b["data"]["pageViewId"]
call("POST", "/public/visits", None, {"visitorKey": key, "pageViewId": pv, "seconds": 95})
check("time on page recorded", one(f"SELECT SecondsOnPage FROM TRN_CrmPageView WHERE PageViewId={pv}") == "95")
call("POST", "/public/visits", None, {"visitorKey": key, "path": "/login", "newVisit": True, "utmSource": "instagram"})
check("return visit counted, original source kept", one(f"SELECT CAST(VisitCount AS varchar)+s.Code FROM TRN_CrmVisitor v JOIN MST_CrmSource s ON s.SourceId=v.SourceId WHERE v.VisitorId={vid}") == "2GOOGLE")
s, b = call("POST", "/public/visits", None, {"path": "/x", "newVisit": True}, headers={"User-Agent": "Googlebot/2.1"})
check("bots not recorded", s == 200 and b["data"]["pageViewId"] is None)

wmob = "90002" + RUN[:5]
enq = {"kind": "form", "name": "CRM E2E Web " + RUN, "mobile": "+91" + wmob, "lookingForId": looking["Hot desk"], "contactConsent": True,
       "visitorKey": key, "pagePath": "/enquire", "elapsedMs": 9000, "message": "Need 2 desks"}
s, b = call("POST", "/public/enquiries", None, {**enq, "contactConsent": False})
check("public: consent required message", s == 422 and "tick the box" in b.get("message", ""))
s, b = call("POST", "/public/enquiries", None, {**enq, "name": ""})
check("public: name required message", s == 422 and "tell us your name" in b.get("message", ""))
s, b = call("POST", "/public/enquiries", None, {**enq, "website": "http://spam"})
check("public: honeypot gets the same success message, no lead", s == 200 and "we will call you back today" in b.get("message", "") and one(f"SELECT COUNT(*) FROM TRN_CrmLead WHERE MobileNormalised='{wmob}'") == "0")
s, b = call("POST", "/public/enquiries", None, {**enq, "elapsedMs": 500})
check("public: too-fast submit dropped silently", s == 200 and one(f"SELECT COUNT(*) FROM TRN_CrmLead WHERE MobileNormalised='{wmob}'") == "0")
s, b = call("POST", "/public/enquiries", None, enq)
check("public enquiry creates a lead", s == 200 and one(f"SELECT COUNT(*) FROM TRN_CrmLead WHERE MobileNormalised='{wmob}'") == "1")
wl = int(one(f"SELECT LeadId FROM TRN_CrmLead WHERE MobileNormalised='{wmob}'"))
made_leads.append(wl)
s, b = call("GET", f"/crm/leads/{wl}", admin)
d = b["data"]
check("web lead: source Google search, channel web form, New", d["row"]["source"] == "Google search" and d["row"]["channel"] == 2 and d["row"]["status"] == "New")
check("web lead: journey attached with page views", d["journey"] and len(d["journey"]["pageViews"]) >= 2 and d["journey"]["visitorCode"] == f"V-{vid}")
check("web lead: consent copied", d["consent"]["contactConsent"] and "contact me" in d["consent"]["consentTextShown"])
check("web lead: response due set (working hours)", d["responseDueUtc"] is not None)
check("visitor no longer scheduled for deletion", one(f"SELECT CASE WHEN DeleteAfterUtc IS NULL AND LeadId={wl} THEN 1 ELSE 0 END FROM TRN_CrmVisitor WHERE VisitorId={vid}") == "1")
s, b = call("POST", "/public/enquiries", None, {**enq, "kind": "callback", "name": ""})
check("same number again: no second lead, activity added", s == 200 and one(f"SELECT COUNT(*) FROM TRN_CrmLead WHERE MobileNormalised='{wmob}'") == "1"
      and one(f"SELECT COUNT(*) FROM TRN_CrmLeadActivity WHERE LeadId={wl} AND Detail LIKE 'Enquired again%'") == "1")
s, b = call("POST", "/public/whatsapp-ref", None, {"visitorKey": key, "path": "/enquire"})
check("WhatsApp ref issued", s == 200 and b["data"]["code"].startswith("W-") and b["data"]["code"] in b["data"]["message"])

# visitors and reports
s, b = call("GET", "/crm/visitors", admin, params={"days": 7})
check("visitors screen data", s == 200 and b["data"]["summary"]["visits"] >= 2 and any(r["visitorId"] == int(vid) for r in b["data"]["page"]["items"]))
s, b = call("GET", f"/crm/visitors/{vid}", admin)
check("one visitor with page views", s == 200 and len(b["data"]["pageViews"]) >= 2)
for rep in ["sources", "funnel", "response-times", "owners"]:
    s, b = call("GET", f"/crm/reports/{rep}", admin)
    check(f"report {rep}", s == 200 and isinstance(b["data"]["rows"], list))
s, b = call("GET", "/crm/reports/export", admin)
check("reports export (xlsx bytes)", s == 200 and b.get("_bytes", 0) > 1000)
s, b = call("GET", "/crm/leads/export", admin, params={"days": 0})
check("leads export (xlsx bytes)", s == 200 and b.get("_bytes", 0) > 1000)

# settings
s, b = call("GET", "/crm/settings", admin)
cfg = b["data"]
s, b = call("PUT", "/crm/settings", admin, {**cfg, "emailEnabled": True})
check("email cannot be switched on before a test email", s == 422 and "test email first" in b.get("message", ""))
s, b = call("PUT", "/crm/settings", admin, {**cfg, "workEnd": "08:00"})
check("bad working hours refused", s == 422)
s, b = call("PUT", "/crm/settings", admin, cfg)
check("settings save round-trips", s == 200 and b["data"]["targetWebMinutes"] == cfg["targetWebMinutes"])
s, b = call("GET", "/crm/assignment-rules", admin)
check("five assignment rules seeded", s == 200 and len(b["data"]) == 5)
fallback = [r for r in b["data"] if r["conditionType"] == 5][0]
s, b = call("PUT", f"/crm/assignment-rules/{fallback['ruleId']}", admin, {**fallback, "isActive": False})
check("safety-net rule cannot be switched off", s == 422)

# ---- delete on request -----------------------------------------------------------------------
s, _ = call("DELETE", f"/crm/leads/{wl}", sales)
check("sales cannot delete (403)", s == 403, s)
s, b = call("DELETE", f"/crm/leads/{wl}", admin)
check("delete on request", s == 200)
check("lead, activities, consent, visitor, page views all gone",
      one(f"SELECT (SELECT COUNT(*) FROM TRN_CrmLead WHERE LeadId={wl})+(SELECT COUNT(*) FROM TRN_CrmLeadActivity WHERE LeadId={wl})+(SELECT COUNT(*) FROM TRN_CrmLeadConsent WHERE LeadId={wl})+(SELECT COUNT(*) FROM TRN_CrmVisitor WHERE VisitorId={vid})+(SELECT COUNT(*) FROM TRN_CrmPageView WHERE VisitorId={vid})") == "0")
check("no copy left in the audit log", one(f"SELECT COUNT(*) FROM TRN_AuditLog WHERE NewValue LIKE '%{wmob}%' OR OldValue LIKE '%{wmob}%'") == "0")
made_leads.remove(wl)

# existing screens untouched
for path in ["/customers", "/bookings", "/waitlist", "/dashboard/summary"]:
    s, _ = call("GET", path, admin, params={"page": 1, "pageSize": 5})
    check(f"existing endpoint still works: {path}", s in (200, 404) and s != 500, s)

# ---- clean up ---------------------------------------------------------------------------------
for l in list(made_leads):
    call("DELETE", f"/crm/leads/{l}", admin)
sql(f"DELETE FROM TRN_CrmLead WHERE Name LIKE 'CRM E2E%{RUN}'")
if cust_id:
    call("DELETE", f"/users/{cust_id}", admin)
call("DELETE", f"/users/{sales_id}", admin)
purge_leftovers()

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
