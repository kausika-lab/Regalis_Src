"""End-to-end API checks for the Wi-Fi kit (19 Nov 2026), against the LOCAL Dev stack.

  API  http://127.0.0.1:81  (local-dev\\api with the Wi-Fi kit DLLs, simulated controller)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev, migration 20261119180000_Wifi applied
  Mail the API must send to a local SMTP sink (see README "Testing"): this script reads the saved
       .eml files from WIFI_MAIL_DIR to check the email and its QR attachment.

Creates customers wifi-e2e-*@example.test (one with company "E2E Labs"), a Support user, bookings on
hot desks for tomorrow, a monthly pass and a day pass bought through the sandbox gateway, guest access
and simulated devices. Wi-Fi rows of the test people are deleted at the end, bookings are cancelled,
users are soft-deleted by the API. Passes and invoices of the test customers stay (they cannot be
deleted through the app); they belong to soft-deleted users.

Some steps need time to pass: the script moves a test booking's start into the past, back-dates a test
invoice's due date, and clears the job's last-run marker, with SQL. Nothing else is written directly.
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD, WIFI_MAIL_DIR.

usage: python -I e2e_wifi.py
"""
import datetime as dt, email, email.policy, glob, json, os, random, subprocess, sys, time, urllib.request, urllib.error

API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
MAIL = os.environ["WIFI_MAIL_DIR"]
results = []
RUN = str(random.randint(10000, 99999))


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {str(detail)[:400]}" if detail and not ok else ""), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


CREDS = {}   # token -> (email, password, portal), to sign in again when a token expires mid-run
RENEWED = {}  # old token -> new token


def call(method, path, token=None, body=None, params=None, raw=False):
    while token in RENEWED:
        token = RENEWED[token]
    for _ in range(8):
        s, b = _call(method, path, token, body, params, raw)
        if s == 401 and token in CREDS:
            fresh = login(*CREDS[token])
            RENEWED[token] = fresh
            token = fresh
            continue
        if s != 429:
            return s, b
        time.sleep(15)
    return s, b


def _call(method, path, token, body, params, raw):
    url = API + path
    if params:
        url += "?" + "&".join(f"{k}={urllib.request.quote(str(v))}" for k, v in params.items() if v is not None)
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("User-Agent", "Mozilla/5.0 (Linux; Android 14) Chrome/130.0 Mobile")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            content = r.read()
            if raw:
                return r.status, {"_bytes": content, "_type": r.headers.get("Content-Type", "")}
            try:
                return r.status, json.loads(content or b"{}")
            except Exception:
                return r.status, {"_bytes": len(content)}
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read() or b"{}")
        except Exception:
            return e.code, {}


def login(email_, password, portal="admin"):
    for _ in range(6):
        s, b = call("POST", "/auth/login", body={"email": email_, "password": password, "portal": portal})
        if s == 200:
            CREDS[b["data"]["accessToken"]] = (email_, password, portal)
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email_} failed: {s} {b}")
    raise SystemExit("login rate limited")


def wait(what, fn, timeout=75):
    """The Wi-Fi job runs every 15 s: wait for fn() to be truthy."""
    end = time.time() + timeout
    while time.time() < end:
        v = fn()
        if v:
            return v
        time.sleep(3)
    return fn()


def iso(d):
    return d.astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def mails_to(addr):
    out = []
    for f in sorted(glob.glob(os.path.join(MAIL, "*.eml"))):
        with open(f, "rb") as h:
            raw = h.read()
        if addr.encode() in raw.split(b"\r\n", 1)[0]:
            out.append(email.message_from_bytes(raw.split(b"\r\n", 1)[1], policy=email.policy.default))
    return out


IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
now = dt.datetime.now(dt.timezone.utc)
tomorrow = (now.astimezone(IST) + dt.timedelta(days=1)).date()
while tomorrow.weekday() == 6:  # Sundays are closed
    tomorrow += dt.timedelta(days=1)


def at(day, hh, mm=0):
    return dt.datetime(day.year, day.month, day.day, hh, mm, tzinfo=IST)


TEST_USERS = "Email LIKE 'wifi-e2e-%@example.test'"


def purge():
    """Removes the Wi-Fi rows of test people (also from an interrupted earlier run)."""
    sql(f"""DECLARE @u TABLE (id bigint); INSERT INTO @u SELECT UserId FROM MST_User WHERE {TEST_USERS};
        DECLARE @a TABLE (id bigint); INSERT INTO @a SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId IN (SELECT id FROM @u) OR HolderName LIKE 'WiFi E2E%';
        DELETE k FROM TRN_WifiSimKey k JOIN TRN_WifiAccess a ON a.ControllerRef = k.ControllerRef WHERE a.AccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_WifiSimClient WHERE MacAddress LIKE '02:E2:%';
        DELETE FROM TRN_WifiSimEvent WHERE MacAddress LIKE '02:E2:%';
        DELETE FROM TRN_WifiBlock WHERE MacAddress LIKE '02:E2:%';
        DELETE FROM TRN_WifiSession WHERE AccessId IN (SELECT id FROM @a) OR MacAddress LIKE '02:E2:%';
        DELETE FROM TRN_WifiDevice WHERE AccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_WifiEvent WHERE AccessId IN (SELECT id FROM @a) OR Detail LIKE '%02:E2:%';
        DELETE FROM TRN_WifiAccessSource WHERE AccessId IN (SELECT id FROM @a)
            OR (SourceType = 1 AND RefId IN (SELECT BookingId FROM TRN_Booking WHERE CustomerId IN (SELECT id FROM @u)))
            OR (SourceType = 2 AND RefId IN (SELECT CustomerPassId FROM TRN_CustomerPass WHERE CustomerId IN (SELECT id FROM @u)));
        DELETE FROM TRN_WifiAccess WHERE ParentAccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_WifiAccess WHERE AccessId IN (SELECT id FROM @a);
        DELETE FROM TRN_AdminNotification WHERE ReferenceType = 'WifiAccess' AND (ReferenceId IN (SELECT id FROM @a) OR ReferenceLabel LIKE '02:E2:%');
        UPDATE TRN_Booking SET Status = 5, CancelledUtc = SYSUTCDATETIME(), CancellationReason = 'wifi e2e cleanup' WHERE CustomerId IN (SELECT id FROM @u) AND Status IN (1, 2, 3);
        UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE {TEST_USERS} AND IsDeleted = 0;""")


def mac(n):
    return f"02:E2:{RUN[:2]}:{RUN[2:4]}:{n:02X}:{int(RUN[4]):02X}"


purge()
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])
sql("UPDATE MST_WifiSetting SET SettingValue = '1' WHERE SettingKey IN ('issue.auto', 'delivery.email', 'delivery.qr', 'rules.cancel', 'rules.overdue', 'rules.unknownDevice');"
    "UPDATE MST_WifiSetting SET SettingValue = 'simulated' WHERE SettingKey = 'controller.type'; UPDATE MST_WifiSetting SET SettingValue = 'per_customer' WHERE SettingKey = 'mode';"
    "UPDATE MST_WifiSetting SET SettingValue = '7' WHERE SettingKey = 'rules.overdueDays'; UPDATE MST_WifiSetting SET SettingValue = '0' WHERE SettingKey = 'issue.payLater';")

# ---- menus and permissions ---------------------------------------------------------------------
s, b = call("GET", "/menus/my", admin)
check("admin sidebar: Device / Access Control > Wi-Fi", "ADM_ACCESS_WIFI" in json.dumps(b) and "/admin/wifi" in json.dumps(b), s)


def make_user(tag, name, utype, role_code, company=None):
    pw = "WiFi#E2e" + RUN + "x"
    role = one(f"SELECT RoleId FROM MST_Role WHERE RoleCode='{role_code}'")
    em = f"wifi-e2e-{tag}{RUN}@example.test"
    mob = f"+9190{RUN}{random.randint(100, 999)}"
    s, b = call("POST", "/users", admin, {"fullName": name, "email": em, "mobileNumber": mob, "password": pw, "userType": utype,
                                          "roleIds": [int(role)], "companyName": company, "defaultLocationId": 2})
    if s not in (200, 201):
        raise SystemExit(f"create user {tag}: {s} {b}")
    uid = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka',"
        f" Country='India', PostalCode='560098', Gender=1 WHERE UserId={uid}")
    return uid, em, pw, mob


c1, c1_email, c1_pw, c1_mob = make_user("c1", "WiFi E2E Kausika RS", 1, "CUSTOMER", "E2E Labs")
c2, c2_email, c2_pw, c2_mob = make_user("c2", "WiFi E2E Arjun", 1, "CUSTOMER")
sup, sup_email, sup_pw, _ = make_user("sup", "WiFi E2E Support", 3, "SUPPORT")
cust1 = login(c1_email, c1_pw, "customer")
cust2 = login(c2_email, c2_pw, "customer")
support = login(sup_email, sup_pw)

s, b = call("GET", "/menus/my", cust1)
check("customer sidebar: My Wi-Fi (/wifi)", "CUST_WIFI" in json.dumps(b) and '"/wifi"' in json.dumps(b), s)
s, _ = call("GET", "/admin/wifi/connected", support)
check("support (WIFI.VIEW): Connected now opens", s == 200, s)
s, _ = call("POST", "/admin/wifi/access", support, {"name": "x", "email": "x@example.test", "validUntilUtc": iso(now + dt.timedelta(hours=2))})
check("support: issuing guest access refused (403, needs WIFI.MANAGE)", s == 403, s)
s, _ = call("PUT", "/admin/wifi/settings", support, {})
check("support: settings refused (403)", s == 403, s)
s, _ = call("GET", "/admin/wifi/access", cust1)
check("customer: admin Wi-Fi list refused (403)", s == 403, s)
s, b = call("GET", "/portal/wifi", admin)
check("admin: My Wi-Fi refused (it is for customers)", s == 403, s)
s, b = call("GET", "/portal/wifi", cust1)
check("customer with nothing paid: My Wi-Fi is empty", s == 200 and b["data"]["current"] == [] and "never records which websites" in b["data"]["recordedNotice"], b)

# ---- 1. monthly pass paid -> member access, delivered (spec 6.1) ----------------------------------
s, b = call("POST", "/passes/order", cust1, {"passPlanId": 5})
check("customer orders a monthly pass (sandbox gateway)", s == 200, b)
order = b["data"]["gatewayOrderId"]
s, b = call("POST", "/payments/sandbox-complete", cust1, {"gatewayOrderId": order})
pay = b["data"]
s, b = call("POST", "/passes/purchase", cust1, {"passPlanId": 5, "validFrom": now.astimezone(IST).date().isoformat(), "paymentMethod": "gateway",
                                                "gatewayOrderId": order, "gatewayPaymentId": pay["gatewayPaymentId"], "gatewaySignature": pay["gatewaySignature"]})
check("monthly pass paid", s == 200, b)
pass1 = one(f"SELECT TOP 1 CustomerPassId FROM TRN_CustomerPass WHERE CustomerId={c1} ORDER BY CustomerPassId DESC")

acc1 = wait("access for pass", lambda: one(f"SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId={c1} AND Kind=2"))
check("access created within seconds of the payment", acc1 is not None)
row = sql(f"SELECT AccessNo, Profile, Ssid, Username, DeviceLimit, Status, ControllerState, ControllerRef, PasswordEnc, EmailStatus, CustomerPassId FROM TRN_WifiAccess WHERE AccessId={acc1}")[0]
check("access number WIFI-000nnn", row[0].startswith("WIFI-") and len(row[0]) == 11, row[0])
check("monthly pass -> member profile on Regalis-Members, 3 devices", row[1] == "member" and row[2] == "Regalis-Members" and row[4] == "3", row)
check("username from the name (wifi.e2ekausikars style)", row[3].startswith("wifi.") and "@" not in row[3], row[3])
acc1 = int(acc1)
wait("delivered", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc1}") == "2")
row = sql(f"SELECT Status, ControllerState, ControllerRef, PasswordEnc, EmailStatus FROM TRN_WifiAccess WHERE AccessId={acc1}")[0]
check("written to the (simulated) controller", row[1] == "1" and row[2].startswith("SIM-"), row)
check("status Active once delivered", row[0] == "2", row)
check("password stored encrypted (v1: AES-GCM), not plain", row[3].startswith("v1:") and "-" not in row[3][3:12], row[3][:12])
check("email sent", row[4] == "sent", row)
ref1 = row[2]
check("simulated controller holds the key", one(f"SELECT COUNT(*) FROM TRN_WifiSimKey WHERE ControllerRef='{ref1}'") == "1")
check("customer notified in the app (route /wifi)", one(f"SELECT COUNT(*) FROM TRN_Notification WHERE UserId={c1} AND ReferenceType='WifiAccess' AND ActionRoute='/wifi'") == "1")
check("source row links the pass", one(f"SELECT COUNT(*) FROM TRN_WifiAccessSource WHERE AccessId={acc1} AND SourceType=2 AND RefId={pass1}") == "1")

s, b = call("GET", "/portal/wifi", cust1)
cur = b["data"]["current"][0] if s == 200 and b["data"]["current"] else {}
pw1 = cur.get("password") or ""
check("My Wi-Fi shows network, password and QR text", cur.get("ssid") == "Regalis-Members" and len(pw1) == 14 and cur.get("qrText", "").startswith("WIFI:T:WPA;S:Regalis-Members;P:"), cur)
check("password easy to read: 4 letters - 4 - 4", pw1[4] == "-" and pw1[9] == "-" and pw1[:4] == "wifi", pw1)
check("My Wi-Fi title names the membership", "membership" in cur.get("title", ""), cur.get("title"))
check("opening My Wi-Fi records 'seen in the app'", one(f"SELECT CASE WHEN SeenInAppUtc IS NULL THEN 0 ELSE 1 END FROM TRN_WifiAccess WHERE AccessId={acc1}") == "1")

mails = wait("email", lambda: mails_to(c1_email), 20)
m = mails[0] if mails else None
check("email reached the mail server", m is not None)
if m:
    body = m.get_body(preferencelist=("html",)).get_content()
    atts = [p for p in m.iter_attachments()]
    check("email subject 'Your Wi-Fi details for … — Regalis Workspace'", m["subject"].startswith("Your Wi-Fi details for ") and m["subject"].endswith("Regalis Workspace"), m["subject"])
    check("email carries network, password, validity and device limit", "Regalis-Members" in body and pw1 in body and "Valid until" in body and "Devices allowed" in body, body[:300])
    check("email states what is recorded, and not websites", "websites you visit are not" in body)
    check("QR code attached as a PNG", any(a.get_filename() == "wifi-qr.png" and a.get_content()[:4] == b"\x89PNG" for a in atts), [a.get_filename() for a in atts])

# ---- admin lists never show the password -------------------------------------------------------
s, b = call("GET", "/admin/wifi/access", admin, params={"search": "WiFi E2E"})
flat = json.dumps(b)
check("Access issued lists the access", s == 200 and any(r["accessId"] == acc1 for r in b["data"]["page"]["items"]), s)
check("the password appears nowhere in the admin list", pw1 not in flat and "passwordEnc" not in flat and "password" not in flat.lower().replace("passwordshared", ""))
r1 = next(r for r in b["data"]["page"]["items"] if r["accessId"] == acc1)
check("row: Active, 0 of 3 devices, email sent, pass number", r1["status"] == "active" and r1["devicesUsed"] == 0 and r1["deviceLimit"] == 3 and r1["emailStatus"] == "sent" and r1["bookingNumber"] and "membership" in r1["bookingLabel"], r1)
check("chip counts present", all(k in b["data"]["counts"] for k in ["all", "active", "expiringToday", "expired", "revoked", "notDelivered"]))
s, b = call("GET", f"/admin/wifi/access/{acc1}", admin)
evs = [e["event"] for e in b["data"]["events"]] if s == 200 else []
check("access log: issued, controller, delivered", {"issued", "controller", "delivered"} <= set(evs), evs)
check("detail never carries the password", pw1 not in json.dumps(b))

# ---- 2. connecting (spec 6.2) --------------------------------------------------------------------


def join(how, name, n, access=None, ap=None, ssid=None):
    s, b = call("POST", "/admin/wifi/simulator/join", admin, {"accessId": access, "how": how, "deviceName": name, "mac": mac(n), "accessPoint": ap, "ssid": ssid})
    return (b.get("data") or {}).get("outcome") if s == 200 else f"http {s} {b}"


check("device 1 joins with the right password", join("right", "MacBook Air", 1, acc1, "Floor 2 · AP-04") == "connected")
check("device 2 joins", join("right", "iPhone 14", 2, acc1) == "connected")
s, b = call("GET", "/admin/wifi/connected", admin, params={"search": "WiFi E2E"})
rows = b["data"]["page"]["items"] if s == 200 else []
check("Connected now names the customer on each device", len(rows) == 2 and all(r["holderName"] == "WiFi E2E Kausika RS" and r["company"] == "E2E Labs" for r in rows), rows)
check("second device marked as another device on the same access", sorted(r["otherDevice"] for r in rows) == [False, True], rows)
check("row shows MAC, network, area, booking", any(r["mac"] == mac(1) and r["ssid"] == "Regalis-Members" and r["accessPoint"] == "Floor 2 · AP-04" and r["bookingNumber"] for r in rows), rows)
check("cards: online people and devices", b["data"]["cards"]["onlineDevices"] >= 2 and b["data"]["cards"]["onlinePeople"] >= 1, b["data"]["cards"])
check("footer: last refresh from the controller", b["data"]["lastPollUtc"] is not None and b["data"]["controllerType"] == "simulated")
check("in use: status In use", call("GET", f"/admin/wifi/access/{acc1}", admin)[1]["data"]["row"]["status"] == "inuse")
check("device 3 joins (3 of 3)", join("right", "iPad", 3, acc1) == "connected")
join("right", "Kindle", 4, acc1)
check("device 4 refused: device limit, session recorded", one(f"SELECT COUNT(*) FROM TRN_WifiSession WHERE MacAddress='{mac(4)}' AND Result=4") == "1")
check("customer told to remove a device first", one(f"SELECT COUNT(*) FROM TRN_Notification WHERE UserId={c1} AND Title='Wi-Fi device limit reached'") == "1")
check("simulated controller dropped device 4", one(f"SELECT COUNT(*) FROM TRN_WifiSimClient WHERE MacAddress='{mac(4)}' AND ConnectedUtc IS NOT NULL") == "0")
check("wrong password refused", join("wrong", "Phone typo", 5, acc1) == "refused-password")
s, b = call("GET", "/admin/wifi/connected", admin)
c = b["data"]["cards"]
check("failed logins card counts wrong password and device limit", c["failedPassword"] >= 1 and c["failedLimit"] >= 1, c)
check("unrecognised device joins (password of nobody)", join("shared", "Unknown", 6, ssid="Regalis-Guest") == "connected")
s, b = call("GET", "/admin/wifi/connected", admin, params={"who": "unknown"})
unk = [r for r in b["data"]["page"]["items"] if r["mac"] == mac(6)]
check("unrecognised device listed with no customer", len(unk) == 1 and unk[0]["holderName"] is None and unk[0]["who"] == "unknown", unk)
check("admin notified about the unrecognised device", one(f"SELECT COUNT(*) FROM TRN_AdminNotification WHERE EventCode='wifi.unknown_device' AND ReferenceLabel='{mac(6)}'") == "1")
s, b = call("POST", f"/admin/wifi/clients/{mac(6)}/disconnect", admin, {"block": True, "reason": "Not ours"})
check("block the unrecognised device", s == 200, b)
check("blocked device cannot join again", join("shared", "Unknown", 6, ssid="Regalis-Guest") == "blocked")
s, _ = call("POST", f"/admin/wifi/clients/{mac(6)}/unblock", admin)
check("unblock", s == 200 and one(f"SELECT COUNT(*) FROM TRN_WifiBlock WHERE MacAddress='{mac(6)}' AND UnblockedUtc IS NULL") == "0")
s, _ = call("POST", f"/admin/wifi/clients/{mac(3)}/disconnect", admin, {"block": False})
check("admin disconnects a device", s == 200 and one(f"SELECT COUNT(*) FROM TRN_WifiSession WHERE MacAddress='{mac(3)}' AND EndedUtc IS NULL") == "0")
s, _ = call("POST", "/admin/wifi/clients/not-a-mac/disconnect", admin, {"block": False})
check("bad MAC address refused (422)", s == 422, s)

s, b = call("GET", "/portal/wifi", cust1)
devs = b["data"]["current"][0]["devices"]
check("My Wi-Fi lists the customer's devices, online ones marked", len(devs) == 3 and any(d["online"] for d in devs), devs)
dev2 = next(d for d in devs if d["mac"] == mac(2))
s, b = call("DELETE", f"/portal/wifi/devices/{dev2['deviceId']}", cust1)
check("customer removes a device to free a slot", s == 200 and one(f"SELECT COUNT(*) FROM TRN_WifiDevice WHERE AccessId={acc1} AND RemovedUtc IS NULL") == "2", b)
s, _ = call("DELETE", f"/portal/wifi/devices/{dev2['deviceId']}", cust2)
check("another customer cannot remove it (404)", s == 404, s)
check("freed slot: the Kindle can join now", join("right", "Kindle", 4, acc1) == "connected")

# ---- 3. a booking covered by the membership; renewal; reset ----------------------------------------
s, b = call("POST", "/bookings/admin", admin, {"customerId": c1, "workspaceId": 3, "startUtc": iso(at(tomorrow, 10)), "durationMinutes": 120, "notes": "wifi e2e", "paymentMethod": "cash"})
check("admin books a desk for the member, paid in cash", s == 200, b)
bk_cov = one(f"SELECT TOP 1 BookingId FROM TRN_Booking WHERE CustomerId={c1} ORDER BY BookingId DESC")
wait("covered", lambda: one(f"SELECT COUNT(*) FROM TRN_WifiAccessSource WHERE SourceType=1 AND RefId={bk_cov}") == "1")
check("booking inside the membership: no new access, same password", one(f"SELECT COUNT(*) FROM TRN_WifiAccess WHERE CustomerId={c1} AND ParentAccessId IS NULL") == "1"
      and one(f"SELECT AccessId FROM TRN_WifiAccessSource WHERE SourceType=1 AND RefId={bk_cov}") == str(acc1))

old_ref = one(f"SELECT ControllerRef FROM TRN_WifiAccess WHERE AccessId={acc1}")
s, b = call("POST", f"/admin/wifi/access/{acc1}/reset", admin)
check("reset password for this customer only", s == 200, b)
s, b = call("GET", "/portal/wifi", cust1)
pw1b = b["data"]["current"][0]["password"]
check("new password differs; old key out of the controller", pw1b != pw1 and one(f"SELECT COUNT(*) FROM TRN_WifiSimKey WHERE ControllerRef='{old_ref}'") == "0", (pw1, pw1b))
check("reset signs every device out", one(f"SELECT COUNT(*) FROM TRN_WifiDevice WHERE AccessId={acc1} AND RemovedUtc IS NULL") == "0"
      and one(f"SELECT COUNT(*) FROM TRN_WifiSimClient WHERE ControllerRef='{old_ref}' AND ConnectedUtc IS NOT NULL") == "0")
check("a device joins with the new password", join("right", "Old laptop", 7, acc1) == "connected")
check("reset email sent", wait("reset mail", lambda: any("changed" in x.get_body(preferencelist=("html",)).get_content() for x in mails_to(c1_email)), 20))

# ---- 4. a short booking for a new customer: issued ahead, written to the controller at the start -------
s, b = call("POST", "/bookings/admin", admin, {"customerId": c2, "workspaceId": 4, "startUtc": iso(at(tomorrow, 11)), "durationMinutes": 180, "notes": "wifi e2e", "paymentMethod": "cash"})
check("desk booking for tomorrow, paid", s == 200, b)
bk2 = one(f"SELECT TOP 1 BookingId FROM TRN_Booking WHERE CustomerId={c2} ORDER BY BookingId DESC")
acc2 = wait("access for booking", lambda: one(f"SELECT AccessId FROM TRN_WifiAccess WHERE CustomerId={c2} AND Kind=1"))
row = sql(f"SELECT Profile, Ssid, Username, Status, ControllerState, DeviceLimit, BookingId FROM TRN_WifiAccess WHERE AccessId={acc2}")[0]
check("short desk booking -> Regalis-Guest, 2 devices, day- username", row[0] == "short" and row[1] == "Regalis-Guest" and row[5] == "2" and row[2].startswith("day-"), row)
check("starts tomorrow: Issued, not yet in the controller", row[3] == "1" and row[4] == "0", row)
s, b = call("GET", "/portal/wifi", cust2)
cur = b["data"]["current"][0]
check("My Wi-Fi: no password yet, 'ready on …' notice", cur["password"] is None and not cur["ready"] and "ready on" in (cur["notice"] or ""), cur)
s, b = call("GET", "/admin/wifi/access", admin, params={"search": "WiFi E2E Arjun"})
check("admin row says 'Starts …'", s == 200 and b["data"]["page"]["items"][0]["statusLabel"].startswith("Starts"), b["data"]["page"]["items"][0]["statusLabel"] if s == 200 else s)
check("no email before the access works", not mails_to(c2_email))

sql(f"UPDATE TRN_Booking SET StartUtc = DATEADD(minute, -5, SYSUTCDATETIME()), EndUtc = DATEADD(hour, 3, SYSUTCDATETIME()) WHERE BookingId={bk2}")  # time passes
wait("moved + live", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc2}") == "2")
row = sql(f"SELECT Status, ControllerState, DATEDIFF(minute, ValidFromUtc, SYSUTCDATETIME()) FROM TRN_WifiAccess WHERE AccessId={acc2}")[0]
check("dates follow the booking; written to the controller and delivered", row[0] == "2" and row[1] == "1" and 3 <= int(row[2]) <= 8, row)
check("email delivered at the start", len(mails_to(c2_email)) == 1)
pw2 = call("GET", "/portal/wifi", cust2)[1]["data"]["current"][0]["password"]
pass_enc2 = one(f"SELECT PasswordEnc FROM TRN_WifiAccess WHERE AccessId={acc2}")

# booking ends -> expires; a new booking renews the same access with the same password
sql(f"UPDATE TRN_Booking SET EndUtc = DATEADD(minute, -1, SYSUTCDATETIME()) WHERE BookingId={bk2}")
wait("expired", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc2}") == "4")
check("end date passed: Expired, key out of the controller", one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc2}") == "4"
      and one(f"SELECT ControllerState FROM TRN_WifiAccess WHERE AccessId={acc2}") == "3")
check("expired password refused (expired)", join("right", "Late laptop", 8, int(acc2)) == "refused-expired"
      and wait("ref exp", lambda: one(f"SELECT COUNT(*) FROM TRN_WifiSession WHERE MacAddress='{mac(8)}' AND Result=3") == "1", 20))
s, b = call("GET", "/portal/wifi", cust2)
check("My Wi-Fi: earlier access shows Expired", s == 200 and b["data"]["current"] == [] and b["data"]["earlier"][0]["statusLabel"] == "Expired", b["data"])
s, b = call("POST", "/bookings/admin", admin, {"customerId": c2, "workspaceId": 5, "startUtc": iso(at(tomorrow, 14)), "durationMinutes": 120, "notes": "wifi e2e", "paymentMethod": "cash"})
bk3 = one(f"SELECT TOP 1 BookingId FROM TRN_Booking WHERE CustomerId={c2} ORDER BY BookingId DESC")
wait("renewed", lambda: one(f"SELECT COUNT(*) FROM TRN_WifiAccessSource WHERE SourceType=1 AND RefId={bk3}") == "1")
check("renewal: same access, same password (not a new one)", one(f"SELECT COUNT(*) FROM TRN_WifiAccess WHERE CustomerId={c2}") == "1"
      and one(f"SELECT PasswordEnc FROM TRN_WifiAccess WHERE AccessId={acc2}") == pass_enc2)
check("renewed access waits for its start (Issued/Active, not Expired)", one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc2}") in ("1", "2"))

# cancel -> revoked at once
s, b = call("POST", f"/bookings/{bk3}/cancel", admin, {"reason": "wifi e2e cancel"})
check("cancel the booking", s == 200, b)
wait("revoked", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc2}") == "5")
check("cancelled: access revoked with the booking named", one(f"SELECT RevokedReason FROM TRN_WifiAccess WHERE AccessId={acc2}").startswith("Booking "), one(f"SELECT RevokedReason FROM TRN_WifiAccess WHERE AccessId={acc2}"))

# pay later -> nothing until paid
s, b = call("POST", "/bookings/admin", admin, {"customerId": c2, "workspaceId": 5, "startUtc": iso(at(tomorrow, 17)), "durationMinutes": 60, "notes": "wifi e2e", "paymentMethod": "paylater"})
check("pay-later booking made", s == 200, b)
bk4 = one(f"SELECT TOP 1 BookingId FROM TRN_Booking WHERE CustomerId={c2} ORDER BY BookingId DESC")
time.sleep(35)
check("unpaid pay-later booking gets no Wi-Fi (payment first, spec 4.4)", one(f"SELECT COUNT(*) FROM TRN_WifiAccessSource WHERE SourceType=1 AND RefId={bk4}") == "0")

# ---- 5. overdue invoice pauses, paying restores (same password) -----------------------------------------
s, b = call("POST", "/bookings/admin", admin, {"customerId": c1, "workspaceId": 3, "startUtc": iso(at(tomorrow, 15)), "durationMinutes": 60, "notes": "wifi e2e", "paymentMethod": "paylater"})
inv = one(f"SELECT TOP 1 InvoiceId FROM TRN_Invoice WHERE CustomerId={c1} AND Status IN (1,6) ORDER BY InvoiceId DESC")
check("pay-later booking leaves an open invoice", inv is not None, b)
sql(f"UPDATE TRN_Invoice SET DueDate = DATEADD(day, -10, CAST(SYSDATETIME() AS date)) WHERE InvoiceId={inv}; DELETE FROM MST_WifiSetting WHERE SettingKey='overdue.lastUtc'")
wait("paused", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc1}") == "3")
row = sql(f"SELECT Status, PauseReason, ControllerState FROM TRN_WifiAccess WHERE AccessId={acc1}")[0]
check("invoice 7+ days overdue: access Paused, key out of the controller", row[0] == "3" and "overdue" in row[1] and row[2] == "3", row)
s, b = call("GET", "/portal/wifi", cust1)
cur = b["data"]["current"][0]
check("My Wi-Fi: paused notice, password hidden", cur["status"] == "paused" and cur["password"] is None and "overdue" in cur["notice"], cur)
sql(f"UPDATE TRN_Invoice SET PaidAmount = TotalAmount, Status = 2, PaidUtc = SYSUTCDATETIME() WHERE InvoiceId={inv}; DELETE FROM MST_WifiSetting WHERE SettingKey='overdue.lastUtc'")
wait("restored", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={acc1}") == "2")
check("invoice paid: Active again, back in the controller", one(f"SELECT CONCAT(Status, ControllerState) FROM TRN_WifiAccess WHERE AccessId={acc1}") == "21")
check("same password after the pause", call("GET", "/portal/wifi", cust1)[1]["data"]["current"][0]["password"] == pw1b)

# ---- 6. guest access, team members, extend, revoke, resend --------------------------------------------
guest_mail = f"wifi-e2e-guest{RUN}@example.test"
s, b = call("POST", "/admin/wifi/access", admin, {"name": "WiFi E2E Guest", "email": None, "mobile": None, "validUntilUtc": iso(now + dt.timedelta(hours=3))})
check("guest without email or mobile refused", s == 422, s)
s, b = call("POST", "/admin/wifi/access", admin, {"name": "WiFi E2E Guest", "email": guest_mail, "mobile": "98450 22110", "validUntilUtc": iso(now - dt.timedelta(hours=1))})
check("guest end in the past refused", s == 422, s)
s, b = call("POST", "/admin/wifi/access", admin, {"name": "WiFi E2E Guest", "email": guest_mail, "mobile": "98450 22110", "validUntilUtc": iso(now + dt.timedelta(hours=3)), "note": "Interview"})
check("issue guest access by hand (3 h: shows Expiring)", s == 200 and b["data"]["kind"] == "guest" and b["data"]["username"].startswith("guest-") and b["data"]["status"] == "expiring"
      and b["data"]["deliveredUtc"], {k: b.get("data", {}).get(k) for k in ("kind", "username", "status", "statusLabel", "emailStatus", "deliveredUtc")})
guest = b["data"]["accessId"] if s == 200 else 0
check("guest emailed (no account)", len(wait("guest mail", lambda: mails_to(guest_mail), 20)) == 1)
check("issued by the admin, not System", b["data"]["issuedBy"] != "System" if s == 200 else False)
s, b = call("POST", f"/admin/wifi/access/{guest}/resend", admin, {"channel": "whatsapp"})
url = (b.get("data") or {}).get("url") or ""
check("Send by WhatsApp gives a click-to-chat link to the guest's number", s == 200 and url.startswith("https://wa.me/919845022110?text="), url[:60])
s, b = call("POST", f"/admin/wifi/access/{guest}/resend", admin, {"channel": "email"})
check("resend by email", s == 200 and b["data"]["ok"], b)
s, b = call("PATCH", f"/admin/wifi/access/{guest}", admin, {"validUntilUtc": iso(now + dt.timedelta(hours=6)), "deviceLimit": 3, "reason": None})
check("extend without a reason refused", s == 422, s)
s, b = call("PATCH", f"/admin/wifi/access/{guest}", admin, {"validUntilUtc": iso(now + dt.timedelta(hours=6)), "deviceLimit": 3, "reason": "Interview runs late"})
check("extend validity and change the device limit", s == 200 and b["data"]["deviceLimit"] == 3, b)
s, b = call("POST", f"/admin/wifi/access/{guest}/revoke", admin, {"reason": ""})
check("revoke without a reason refused", s == 422, s)
gref = one(f"SELECT ControllerRef FROM TRN_WifiAccess WHERE AccessId={guest}")
s, b = call("POST", f"/admin/wifi/access/{guest}/revoke", admin, {"reason": "Visit over"})
check("revoke: Revoked, reason kept, key removed", s == 200 and b["data"]["status"] == "revoked" and b["data"]["revokedReason"] == "Visit over"
      and one(f"SELECT COUNT(*) FROM TRN_WifiSimKey WHERE ControllerRef='{gref}'") == "0", b)
s, b = call("POST", f"/admin/wifi/access/{guest}/resend", admin, {"channel": "email"})
check("revoked access cannot be resent", s == 422, s)
check("access is never deleted: revoked row stays", one(f"SELECT COUNT(*) FROM TRN_WifiAccess WHERE AccessId={guest}") == "1")

member_mail = f"wifi-e2e-member{RUN}@example.test"
s, b = call("POST", f"/admin/wifi/access/{acc1}/members", admin, {"name": "WiFi E2E Divya R", "email": member_mail, "mobile": None})
check("company booking: a named team member gets their own access", s == 200 and b["data"]["kind"] == "team" and b["data"]["username"].startswith("e2e."), b)
mem = b["data"]["accessId"] if s == 200 else 0
check("team member emailed their own details", len(wait("member mail", lambda: mails_to(member_mail), 20)) == 1)
s, b = call("GET", "/admin/wifi/access", admin, params={"search": "WiFi E2E Kausika"})
r = next((x for x in b["data"]["page"]["items"] if x["accessId"] == acc1), {})
check("company row shows Members (1) and the member username", r.get("memberCount") == 1 and r.get("membersPreview", "").startswith("e2e."), r)
check("team member device is named on Connected now", join("right", "Android phone", 9, mem) == "connected"
      and any(x["holderName"] == "WiFi E2E Divya R" and x["who"] == "team" for x in call("GET", "/admin/wifi/connected", admin, params={"search": "Divya"})[1]["data"]["page"]["items"]))

# ---- 7. controller unreachable: retries, then "access not created"; never told ready ----------------------
sql("UPDATE MST_WifiSetting SET SettingValue='unifi' WHERE SettingKey='controller.type'")
time.sleep(16)
s, b = call("POST", "/admin/wifi/access", admin, {"name": "WiFi E2E Guest Two", "email": f"wifi-e2e-g2{RUN}@example.test", "validUntilUtc": iso(now + dt.timedelta(hours=2))})
g2 = b["data"]["accessId"] if s == 200 else 0
check("controller down: first try fails, access waits (Issued, pending)", s == 200 and one(f"SELECT CONCAT(Status, ControllerState, ControllerAttempts) FROM TRN_WifiAccess WHERE AccessId={g2}") == "101", b)
sql(f"UPDATE TRN_WifiAccess SET ControllerAttempts=2, ControllerNextTryUtc=SYSUTCDATETIME() WHERE AccessId={g2}")
wait("failed", lambda: one(f"SELECT ControllerState FROM TRN_WifiAccess WHERE AccessId={g2}") == "2")
check("after 3 tries: Failed, admin told 'access not created'", one(f"SELECT ControllerState FROM TRN_WifiAccess WHERE AccessId={g2}") == "2"
      and one(f"SELECT COUNT(*) FROM TRN_AdminNotification WHERE EventCode='wifi.not_created' AND ReferenceId={g2}") == "1")
check("the guest was NOT told it is ready", not mails_to(f"wifi-e2e-g2{RUN}@example.test") and one(f"SELECT CASE WHEN DeliveredUtc IS NULL THEN 1 ELSE 0 END FROM TRN_WifiAccess WHERE AccessId={g2}") == "1")
sql("UPDATE MST_WifiSetting SET SettingValue='simulated' WHERE SettingKey='controller.type'")
time.sleep(16)
s, b = call("POST", f"/admin/wifi/access/{g2}/retry", admin)
check("retry after the controller is back: live and delivered", s == 200 and b["data"]["controllerState"] == "live" and len(wait("g2", lambda: mails_to(f"wifi-e2e-g2{RUN}@example.test"), 20)) == 1, b)

# expiry by the job
sql(f"UPDATE TRN_WifiAccess SET ValidUntilUtc = DATEADD(minute, -1, SYSUTCDATETIME()) WHERE AccessId={g2}")
check("job expires access past its end", wait("g2 exp", lambda: one(f"SELECT Status FROM TRN_WifiAccess WHERE AccessId={g2}") == "4"))

# ---- 8. help ticket, usage log, exports, settings ---------------------------------------------------------
s, b = call("POST", "/portal/wifi/help", cust1, {"message": "My laptop says wrong password", "passwordShared": True})
tno = (b.get("data") or {}).get("ticketNumber")
check("'Having trouble connecting?' raises a support ticket", s == 200 and tno and one(f"SELECT COUNT(*) FROM TRN_Ticket WHERE TicketNumber='{tno}' AND CustomerId={c1} AND Subject LIKE 'Wi-Fi:%'") == "1", b)
s, b = call("POST", "/portal/wifi/resend", cust1, {"channel": "email"})
check("customer asks for the details again by email", s == 200 and b["data"]["ok"], b)
s, b = call("POST", "/portal/wifi/resend", cust1, {"channel": "whatsapp"})
check("customer Send on WhatsApp: link to their own number", s == 200 and b["data"]["url"].startswith("https://wa.me/91"), b)

s, b = call("GET", "/admin/wifi/sessions", admin, params={"search": "WiFi E2E", "fromUtc": iso(now - dt.timedelta(hours=1))})
res = {x["result"] for x in b["data"]["items"]} if s == 200 else set()
check("usage log: connected and refused rows with the customer", s == 200 and any(r.startswith("connected") for r in res) and any(r.startswith("refused") for r in res), res)
s, b = call("GET", "/admin/wifi/sessions/export", admin, params={"fromUtc": iso(now - dt.timedelta(hours=1))}, raw=True)
check("usage log exports to Excel", s == 200 and b["_bytes"][:2] == b"PK" and "spreadsheetml" in b["_type"], s)
s, b = call("GET", "/admin/wifi/connected/export", admin, raw=True)
check("connected list exports to Excel", s == 200 and b["_bytes"][:2] == b"PK", s)

s, b = call("GET", "/admin/wifi/settings", admin)
st = b["data"]
check("settings: simulated controller, per-customer mode, spec defaults", st["controllerType"] == "simulated" and st["mode"] == "per_customer"
      and next(p for p in st["profiles"] if p["key"] == "member")["deviceLimit"] == 3 and next(p for p in st["profiles"] if p["key"] == "daypass")["deviceLimit"] == 2
      and st["overdueDays"] == 7 and st["retentionMonths"] == 12 and st["dayPassUntil"] == "closing", st)
check("settings never return the controller key or shared password", st["controllerKey"] is None and st["sharedPassword"] is None)
s, b = call("PUT", "/admin/wifi/settings", admin, {**st, "controllerType": "unifi"})
check("an adapter that is not installed cannot be chosen", s == 422 and "adapter" in b.get("message", ""), b)
s, b = call("PUT", "/admin/wifi/settings", admin, {**st, "mode": "shared", "sharedPassword": None})
check("shared mode needs the shared password", s == 422, s)
s, b = call("PUT", "/admin/wifi/settings", admin, {**st, "profiles": [{**p, "ssid": "Nope"} if p["key"] == "member" else p for p in st["profiles"]]})
check("a profile must use a listed network", s == 422, s)
s, b = call("PUT", "/admin/wifi/settings", admin, {**st, "reminder": True})
check("save settings", s == 200 and b["data"]["reminder"] is True, b)
call("PUT", "/admin/wifi/settings", admin, {**st, "reminder": False})
s, b = call("POST", "/admin/wifi/settings/test", admin)
check("test connection (simulated) answers", s == 200 and b["data"]["connected"] is True, b)

# ---- 9. existing screens unchanged -------------------------------------------------------------------
for path in ["/bookings", "/passes", "/tickets", "/admin/notifications", "/crm/leads"]:
    s, _ = call("GET", path, admin)
    check(f"existing endpoint still answers: GET {path}", s == 200, s)
for path, prm in [("/bookings/my", {"tab": "upcoming"}), ("/passes/my", None), ("/notifications/my", None)]:
    s, _ = call("GET", path, cust1, params=prm)
    check(f"customer endpoint still answers: GET {path}", s == 200, s)

# ---- cleanup -------------------------------------------------------------------------------------------
for bk in [bk_cov, bk4]:
    call("POST", f"/bookings/{bk}/cancel", admin, {"reason": "wifi e2e cleanup"})
time.sleep(2)
purge()
check("cleanup: no Wi-Fi rows left for test people", one(f"SELECT COUNT(*) FROM TRN_WifiAccess WHERE CustomerId IN (SELECT UserId FROM MST_User WHERE {TEST_USERS}) OR HolderName LIKE 'WiFi E2E%'") == "0")

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
