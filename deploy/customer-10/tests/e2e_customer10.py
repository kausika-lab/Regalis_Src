"""End-to-end API checks for the customer-10 kit (14 Nov 2026), against a LOCAL test API.

  API  http://127.0.0.1:82  (local-dev\\api with the kit DLLs, started with
       Payments__Razorpay__WebhookSecret=<REGALIS_WEBHOOK_SECRET> and Payments:Provider=Sandbox)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev, migration 20261114150000 applied

It creates throwaway customers (c10-*@example.test, password copied from REGALIS_CUST_EMAIL's
row) and writes bookings, passes and payments for them. Restore the Dev backup afterwards.
Env: REGALIS_ADMIN_EMAIL/PASSWORD, REGALIS_CUST_EMAIL/PASSWORD, REGALIS_WEBHOOK_SECRET.

usage: python -I e2e_customer10.py
"""
import base64, datetime as dt, hashlib, hmac, json, os, subprocess, sys, time, urllib.request, urllib.error, uuid

API = os.environ.get("REGALIS_API", "http://127.0.0.1:82/api")
SECRET = os.environ["REGALIS_WEBHOOK_SECRET"]
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
results = []


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


def call(method, path, token=None, body=None, params=None, raw=None, headers=None):
    url = API + path
    if params:
        url += "?" + "&".join(f"{k}={v}" for k, v in params.items() if v is not None)
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    req = urllib.request.Request(url, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    for k, v in (headers or {}).items():
        req.add_header(k, v)
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            return r.status, json.loads(r.read() or b"{}")
    except urllib.error.HTTPError as e:
        try:
            return e.code, json.loads(e.read() or b"{}")
        except Exception:
            return e.code, {}


def login_full(email, password, portal):
    for _ in range(6):
        s, b = call("POST", "/auth/login", body={"email": email, "password": password, "portal": portal})
        if s == 200:
            return b["data"]
        if s == 429:
            time.sleep(20)
            continue
        return None
    return None


def login(email, password, portal="customer"):
    d = login_full(email, password, portal)
    if not d:
        raise SystemExit(f"login {email} failed")
    return d["accessToken"]


def jwt_claims(tok):
    p = tok.split(".")[1]
    return json.loads(base64.urlsafe_b64decode(p + "=" * (-len(p) % 4)))


def webhook(event, order_id, pay_id, amount_paise, status):
    body = json.dumps({"event": event, "payload": {"payment": {"entity": {
        "id": pay_id, "order_id": order_id, "status": status, "amount": amount_paise,
        "error_description": "Card declined (test)" if status == "failed" else None}}}}).encode()
    sig = hmac.new(SECRET.encode(), body, hashlib.sha256).hexdigest()
    return call("POST", "/payments/webhook", raw=body, headers={"X-Razorpay-Signature": sig})


def sandbox(order_id, tok):
    s, b = call("POST", "/payments/sandbox-complete", tok, {"gatewayOrderId": order_id})
    d = b.get("data") or {}
    return d.get("gatewayPaymentId") or d.get("paymentId"), d.get("signature") or d.get("gatewaySignature")


CUST_EMAIL, CUST_PW = os.environ["REGALIS_CUST_EMAIL"], os.environ["REGALIS_CUST_PASSWORD"]
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")
tag = uuid.uuid4().hex[:6]


def make_customer(label, google_only=False):
    email = f"c10-{label}-{tag}@example.test"
    mobile = "+9198" + str(abs(hash(email)) % 10**8).rjust(8, "0")
    pw = "NULL" if google_only else "src.PasswordHash"
    gid = f"'g-{tag}-{label}'" if google_only else "NULL"
    sql(f"""INSERT MST_User (UserType, FullName, Email, MobileNumber, PasswordHash, GoogleId, DefaultLocationId, IsEmailVerified, IsMobileVerified, Status,
            FailedLoginCount, MustChangePassword, CreatedDate, IsActive, IsDeleted, PayLaterAllowed, City, Address, CompanyName, AddressLine1, AddressLine2, State, Country, PostalCode, Gender, DateOfBirth)
            SELECT 1, 'C10 Test {label}', '{email}', '{mobile}', {pw}, {gid}, src.DefaultLocationId, 1, 1, 1, 0, 0, SYSUTCDATETIME(), 1, 0, 1, src.City, src.Address, 'Test Co', src.AddressLine1, src.AddressLine2, src.State, src.Country, src.PostalCode, src.Gender, src.DateOfBirth
            FROM MST_User src WHERE src.Email = '{CUST_EMAIL}';
            INSERT MAP_UserRole (UserId, RoleId, AssignedDateUtc) SELECT UserId, (SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'), SYSUTCDATETIME() FROM MST_User WHERE Email='{email}';""")
    uid = int(one(f"SELECT UserId FROM MST_User WHERE Email='{email}'"))
    return email, uid


a_email, a_id = make_customer("a")
b_email, b_id = make_customer("b")
d_email, d_id = make_customer("d")
g_email, g_id = make_customer("g", google_only=True)
a_login = login_full(a_email, CUST_PW, "customer")
A = a_login["accessToken"]
B = login(b_email, CUST_PW)
loc_id = int(one("SELECT TOP 1 LocationId FROM MST_Location WHERE IsDefault=1 AND IsDeleted=0"))
plan = lambda code: int(one(f"SELECT PassPlanId FROM MST_PassPlan WHERE PlanCode='{code}'"))
today = dt.datetime.now(IST).date()
monday = today + dt.timedelta(days=(7 - today.weekday()) % 7 or 7)

# ---- 4 / 10: menu ------------------------------------------------------------------------
s, b = call("GET", "/menus/my", A)
menus = json.dumps(b.get("data"))
check("4  customer menu has no /qr", s == 200 and '"/qr"' not in menus, menus[:120])
check("10 customer menu has Payment History (/payments)", '"/payments"' in menus and "Payment History" in menus)

# ---- 1 + 2: pay later -> Awaiting payment -> desk payment -> Active ------------------------
s, b = call("POST", "/passes/purchase", A, {"passPlanId": plan("MEM-VIRTUAL-1M"), "validFrom": str(today), "paymentMethod": "paylater"})
check("1  Virtual Office with pay later is accepted", s == 200, json.dumps(b)[:300])
vo = (b.get("data") or {}).get("pass") or {}
vo_id = vo.get("customerPassId")
check("2  pay-later VO is Awaiting payment (5) in the response", vo.get("status") == 5, vo)
check("2  ...and in the database, unpaid", sql(f"SELECT Status, CONVERT(varchar(20), AmountPaid), IsPayLater FROM TRN_CustomerPass WHERE CustomerPassId={vo_id}")[0] == ["5", "0.00", "1"])
check("1  invoice for the VO is pending", one(f"SELECT Status FROM TRN_Invoice WHERE CustomerPassId={vo_id}") == "1")
s, b = call("GET", "/passes/my", A, params={"page": 1, "pageSize": 20})
mine = next((p for p in b["data"]["items"] if p["customerPassId"] == vo_id), {})
check("1  portal lists it with status 5 and nothing paid (TO PAY)", mine.get("status") == 5 and mine.get("isPayLater") and mine.get("amountPaid") == 0, mine)
s, b = call("POST", "/attendance/check-in", A, {})
check("2  awaiting pass cannot be used to check in", s >= 400, f"{s} {b}")

s, b = call("POST", "/passes/purchase", A, {"passPlanId": plan("DAYPASS-1"), "validFrom": str(monday), "paymentMethod": "paylater"})
dp = ((b.get("data") or {}).get("pass") or {})
check("2  pay-later day pass is Awaiting payment", s == 200 and dp.get("status") == 5, json.dumps(b)[:300])

total = float(vo["totalAmount"])
s, b = call("POST", "/cash/collections", admin, {"customerPassId": vo_id, "amount": round(total / 2, 2), "paymentMethod": 6, "amountTendered": round(total / 2, 2)})
check("2  desk part payment recorded", s == 200, json.dumps(b)[:300])
check("2  part-paid pass stays Awaiting payment", one(f"SELECT Status FROM TRN_CustomerPass WHERE CustomerPassId={vo_id}") == "5")
rest = round(total - round(total / 2, 2), 2)
s, b = call("POST", "/cash/collections", admin, {"customerPassId": vo_id, "amount": rest, "paymentMethod": 6, "amountTendered": rest})
coll2 = (b.get("data") or {}).get("cashCollectionId")
check("2  paid in full at the desk -> Active (1)", s == 200 and one(f"SELECT Status FROM TRN_CustomerPass WHERE CustomerPassId={vo_id}") == "1", json.dumps(b)[:200])
check("2  customer is told 'Pass activated'", one(f"SELECT COUNT(*) FROM TRN_Notification WHERE UserId={a_id} AND Title='Pass activated' AND ReferenceId={vo_id}") == "1")
s, b = call("POST", f"/cash/collections/{coll2}/void", admin, {"reason": "C10 test void"})
check("2  voiding a receipt puts it back to Awaiting payment", s == 200 and one(f"SELECT Status FROM TRN_CustomerPass WHERE CustomerPassId={vo_id}") == "5", f"{s} {json.dumps(b)[:200]}")
call("POST", "/cash/collections", admin, {"customerPassId": vo_id, "amount": rest, "paymentMethod": 6, "amountTendered": rest})
check("2  paid again -> Active", one(f"SELECT Status FROM TRN_CustomerPass WHERE CustomerPassId={vo_id}") == "1")

# ---- 2: online pass only after verified payment -------------------------------------------
before = int(one(f"SELECT COUNT(*) FROM TRN_CustomerPass WHERE CustomerId={a_id}"))
s, b = call("POST", "/passes/order", A, {"passPlanId": plan("DAYPASS-1")})
o1 = b["data"]["gatewayOrderId"]
s, b = call("POST", "/passes/purchase", A, {"passPlanId": plan("DAYPASS-1"), "validFrom": str(monday), "paymentMethod": "gateway", "gatewayOrderId": o1, "gatewayPaymentId": "pay_forged", "gatewaySignature": "00" * 32})
check("2  bad signature: refused", s >= 400)
check("2  ...no pass created, payment not marked paid", int(one(f"SELECT COUNT(*) FROM TRN_CustomerPass WHERE CustomerId={a_id}")) == before and one(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{o1}'") != "2")
s, b = call("POST", "/passes/order", A, {"passPlanId": plan("DAYPASS-1")})
o_cancel = b["data"]["gatewayOrderId"]
check("2  cancelled checkout (never completed): no pass, payment still Pending", int(one(f"SELECT COUNT(*) FROM TRN_CustomerPass WHERE CustomerId={a_id}")) == before and one(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{o_cancel}'") == "1")
s, b = call("POST", "/passes/order", A, {"passPlanId": plan("DAYPASS-1")})
o_fail = b["data"]["gatewayOrderId"]
pay_amt_paise = int(round(float(one(f"SELECT Amount FROM TRN_Payment WHERE GatewayOrderId='{o_fail}'")) * 100))
s, _ = webhook("payment.failed", o_fail, "pay_failtest" + tag, pay_amt_paise, "failed")
check("2  payment.failed webhook marks the payment Failed (was Success before)", s == 200 and one(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{o_fail}'") == "3")
check("2  ...and no pass", int(one(f"SELECT COUNT(*) FROM TRN_CustomerPass WHERE CustomerId={a_id}")) == before)

s, b = call("POST", "/passes/order", A, {"passPlanId": plan("DAYPASS-1")})
o2 = b["data"]["gatewayOrderId"]
pid, sig = sandbox(o2, A)
paise = int(round(float(one(f"SELECT Amount FROM TRN_Payment WHERE GatewayOrderId='{o2}'")) * 100))
s, _ = webhook("payment.captured", o2, pid, paise, "captured")
check("2  webhook first: payment Success, not yet verified by the browser", one(f"SELECT CONCAT(Status,'/',IsSignatureVerified) FROM TRN_Payment WHERE GatewayOrderId='{o2}'") == "2/0")
s, b = call("POST", "/passes/purchase", A, {"passPlanId": plan("DAYPASS-1"), "validFrom": str(monday), "paymentMethod": "gateway", "gatewayOrderId": o2, "gatewayPaymentId": pid, "gatewaySignature": sig})
p2 = ((b.get("data") or {}).get("pass") or {})
check("2  browser verify after the webhook still creates the pass, Active", s == 200 and p2.get("status") == 1, json.dumps(b)[:300])
s, b = call("POST", "/passes/purchase", A, {"passPlanId": plan("DAYPASS-1"), "validFrom": str(monday), "paymentMethod": "gateway", "gatewayOrderId": o2, "gatewayPaymentId": pid, "gatewaySignature": sig})
check("2  the same payment cannot buy a second pass", s >= 400)

# ---- 6: 10-minute hold -------------------------------------------------------------------------
ws = int(one(f"SELECT TOP 1 WorkspaceId FROM MST_Workspace WHERE LocationId={loc_id} AND IsBookable=1 AND IsActive=1 AND IsDeleted=0 AND Status=1 ORDER BY WorkspaceId DESC"))
def slot(hour):
    return dt.datetime.combine(monday, dt.time(hour, 0), IST).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")
sql(f"UPDATE TRN_Booking SET Status=5 WHERE WorkspaceId={ws} AND BookingDate='{monday}' AND Status IN (1,2,3) AND CustomerId IN (SELECT UserId FROM MST_User WHERE Email LIKE 'c10-%@example.test')")
busy = one(f"SELECT COUNT(*) FROM TRN_Booking WHERE WorkspaceId={ws} AND BookingDate='{monday}' AND Status IN (1,2,3)")
if busy != "0":
    raise SystemExit(f"workspace {ws} has {busy} real bookings on {monday}; pick another")

def hold(tok, hour):
    return call("POST", "/bookings", tok, {"workspaceId": ws, "startUtc": slot(hour), "durationMinutes": 60})

s, b = hold(A, 10)
h1 = (b.get("data") or {}).get("bookingId")
check("6  hold created, Pending", s in (200, 201) and one(f"SELECT Status FROM TRN_Booking WHERE BookingId={h1}") == "1", json.dumps(b)[:300])
hold_until = one(f"SELECT DATEDIFF(second, CreatedDate, DATEADD(minute, CAST((SELECT SettingValue FROM MST_AppSetting WHERE SettingKey='Booking.PendingHoldMinutes') AS int), CreatedDate)) FROM TRN_Booking WHERE BookingId={h1}")
check("6  hold window is 10 minutes (setting)", hold_until == "600", hold_until)
s, b = hold(B, 10)
check("6  another customer cannot take the held slot", s == 409, f"{s} {json.dumps(b)[:200]}")
sql(f"UPDATE TRN_Booking SET CreatedDate = DATEADD(minute, -11, CreatedDate) WHERE BookingId={h1}")
s, b = call("GET", "/availability/slots", B, params={"locationId": loc_id, "workspaceTypeId": one(f"SELECT WorkspaceTypeId FROM MST_Workspace WHERE WorkspaceId={ws}"), "date": str(monday)})
check("6  availability no longer counts the lapsed hold", s == 200, f"{s}")
s, b = hold(B, 10)
hb = (b.get("data") or {}).get("bookingId")
check("6  after 10 min the slot is free for others at once (no waiting for the sweep)", s in (200, 201), f"{s} {json.dumps(b)[:200]}")
check("6  ...the lapsed hold is Expired with a history row", one(f"SELECT Status FROM TRN_Booking WHERE BookingId={h1}") == "6" and one(f"SELECT COUNT(*) FROM TRN_BookingStatusHistory WHERE BookingId={h1} AND ToStatus=6") == "1")
s, b = call("POST", f"/bookings/{h1}/confirm", A, {"paymentMethod": "wallet"})
check("6  an expired hold cannot be confirmed with wallet", s >= 400)
call("POST", f"/bookings/{hb}/cancel", B, {"reason": "test"})

# sweeper
s, b = hold(A, 12)
h_sw = b["data"]["bookingId"]
sql(f"UPDATE TRN_Booking SET CreatedDate = DATEADD(minute, -11, CreatedDate) WHERE BookingId={h_sw}")
for _ in range(16):
    if one(f"SELECT Status FROM TRN_Booking WHERE BookingId={h_sw}") == "6":
        break
    time.sleep(5)
check("6  background sweep expires a lapsed hold within a minute", one(f"SELECT Status FROM TRN_Booking WHERE BookingId={h_sw}") == "6")

# successful payment in time
s, b = hold(A, 13)
h_ok = b["data"]["bookingId"] if b.get("data") else sys.exit(f"hold 13 failed: {s} {b}")
s, b = call("POST", "/payments/order", A, {"bookingId": h_ok})
o_ok = b["data"]["gatewayOrderId"]
pid, sig = sandbox(o_ok, A)
s, b = call("POST", f"/bookings/{h_ok}/confirm", A, {"paymentMethod": "gateway", "gatewayOrderId": o_ok, "gatewayPaymentId": pid, "gatewaySignature": sig})
check("6  paid within 10 min -> Confirmed, Paid", s == 200 and sql(f"SELECT Status, PaymentStatus FROM TRN_Booking WHERE BookingId={h_ok}")[0] == ["2", "2"], json.dumps(b)[:300])

# late payment, slot still free -> confirmed after re-check
s, b = hold(A, 14)
h_late = b["data"]["bookingId"]
s, b = call("POST", "/payments/order", A, {"bookingId": h_late})
o_late = b["data"]["gatewayOrderId"]
pid, sig = sandbox(o_late, A)
sql(f"UPDATE TRN_Booking SET CreatedDate = DATEADD(minute, -12, CreatedDate), Status = 6 WHERE BookingId={h_late}")
s, b = call("POST", f"/bookings/{h_late}/confirm", A, {"paymentMethod": "gateway", "gatewayOrderId": o_late, "gatewayPaymentId": pid, "gatewaySignature": sig})
check("6  late payment, slot still free: re-checked and Confirmed", s == 200 and one(f"SELECT Status FROM TRN_Booking WHERE BookingId={h_late}") == "2", json.dumps(b)[:300])
check("6  ...history says it was re-validated", one(f"SELECT COUNT(*) FROM TRN_BookingStatusHistory WHERE BookingId={h_late} AND FromStatus=6 AND ToStatus=1") == "1")
s, b = call("POST", f"/bookings/{h_late}/confirm", A, {"paymentMethod": "gateway", "gatewayOrderId": o_late, "gatewayPaymentId": "pay_forged", "gatewaySignature": "00" * 32})
check("6  replaying confirm with forged values returns the existing booking, no second charge", s == 200 and one(f"SELECT COUNT(*) FROM TRN_Payment WHERE BookingId={h_late} AND Status=2") == "1", f"{s}")

# late payment, slot taken -> wallet credit
s, b = hold(A, 15)
h_lost = b["data"]["bookingId"]
s, b = call("POST", "/payments/order", A, {"bookingId": h_lost})
o_lost = b["data"]["gatewayOrderId"]
pid, sig = sandbox(o_lost, A)
sql(f"UPDATE TRN_Booking SET CreatedDate = DATEADD(minute, -12, CreatedDate) WHERE BookingId={h_lost}")
s, b = hold(B, 15)
check("6  (another customer takes the lapsed slot)", s in (200, 201), json.dumps(b)[:200])
wallet0 = float(one(f"SELECT ISNULL((SELECT Balance FROM MST_Wallet WHERE CustomerId={a_id}),0)"))
s, b = call("POST", f"/bookings/{h_lost}/confirm", A, {"paymentMethod": "gateway", "gatewayOrderId": o_lost, "gatewayPaymentId": pid, "gatewaySignature": sig})
amount_lost = float(one(f"SELECT Amount FROM TRN_Payment WHERE GatewayOrderId='{o_lost}'"))
wallet1 = float(one(f"SELECT ISNULL((SELECT Balance FROM MST_Wallet WHERE CustomerId={a_id}),0)"))
check("6  late payment, slot taken: 409 with a wallet message", s == 409 and "wallet" in json.dumps(b).lower(), f"{s} {json.dumps(b)[:200]}")
check("6  ...full amount credited to the wallet", abs(wallet1 - wallet0 - amount_lost) < 0.01, f"{wallet0} -> {wallet1}, {amount_lost}")
check("6  ...payment Refunded, booking Expired, customer notified", one(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{o_lost}'") == "4" and one(f"SELECT Status FROM TRN_Booking WHERE BookingId={h_lost}") == "6" and one(f"SELECT COUNT(*) FROM TRN_Notification WHERE UserId={a_id} AND Title='Payment credited to your wallet'") == "1")
s, b = call("POST", f"/bookings/{h_lost}/confirm", A, {"paymentMethod": "gateway", "gatewayOrderId": o_lost, "gatewayPaymentId": pid, "gatewaySignature": sig})
wallet2 = float(one(f"SELECT ISNULL((SELECT Balance FROM MST_Wallet WHERE CustomerId={a_id}),0)"))
check("6  ...retrying does not credit twice", s >= 400 and abs(wallet2 - wallet1) < 0.01)

# webhook confirms the booking without the browser
s, b = hold(A, 16)
h_wh = b["data"]["bookingId"]
s, b = call("POST", "/payments/order", A, {"bookingId": h_wh})
o_wh = b["data"]["gatewayOrderId"]
pid, sig = sandbox(o_wh, A)
paise = int(round(float(one(f"SELECT Amount FROM TRN_Payment WHERE GatewayOrderId='{o_wh}'")) * 100))
s, _ = webhook("payment.captured", o_wh, "pay_wrongamt" + tag, paise + 100, "captured")
check("6  webhook with the wrong amount is ignored", one(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{o_wh}'") == "1")
bad = json.dumps({"event": "payment.captured", "payload": {"payment": {"entity": {"id": "x", "order_id": o_wh, "status": "captured", "amount": paise}}}}).encode()
s, _ = call("POST", "/payments/webhook", raw=bad, headers={"X-Razorpay-Signature": "00" * 32})
check("6  webhook with a bad signature is rejected", s == 401 and one(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{o_wh}'") == "1")
s, _ = webhook("payment.captured", o_wh, pid, paise, "captured")
check("6  webhook alone confirms the booking (browser closed)", s == 200 and sql(f"SELECT Status, PaymentStatus FROM TRN_Booking WHERE BookingId={h_wh}")[0] == ["2", "2"])
check("6  ...with invoice and QR token", one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE BookingId={h_wh}") == "1" and one(f"SELECT COUNT(*) FROM TRN_AccessToken WHERE BookingId={h_wh}") == "1")
s, b = call("POST", f"/bookings/{h_wh}/confirm", A, {"paymentMethod": "gateway", "gatewayOrderId": o_wh, "gatewayPaymentId": pid, "gatewaySignature": sig})
check("6  browser confirm afterwards gets the confirmation, not an error", s == 200 and (b.get("data") or {}).get("booking", {}).get("bookingId") == h_wh, json.dumps(b)[:300])
s, _ = webhook("payment.captured", o_wh, pid, paise, "captured")
check("6  repeated webhook changes nothing", one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE BookingId={h_wh}") == "1")

# ---- 8: sessions --------------------------------------------------------------------------------
c = jwt_claims(A)
check("8  customer access token lives 5 minutes", c["exp"] - c["nbf"] == 300, c["exp"] - c["nbf"])
ca = jwt_claims(admin)
check("8  staff access token unchanged (15 minutes)", ca["exp"] - ca["nbf"] == 900, ca["exp"] - ca["nbf"])
mins = int(one(f"SELECT TOP 1 DATEDIFF(second, CreatedUtc, ExpiresUtc) FROM TRN_RefreshToken WHERE UserId={a_id} ORDER BY RefreshTokenId DESC"))
check("8  customer session ends on the server after 11 idle minutes (10 + warning minute)", mins == 660, mins)
adm_days = int(one("SELECT TOP 1 DATEDIFF(day, CreatedUtc, ExpiresUtc) FROM TRN_RefreshToken t JOIN MST_User u ON u.UserId=t.UserId WHERE u.UserType<>1 ORDER BY RefreshTokenId DESC"))
check("8  staff refresh token unchanged (7 days)", adm_days == 7, adm_days)
s, b = call("POST", "/auth/refresh", body={"refreshToken": a_login["refreshToken"]})
check("8  refresh while active slides the window", s == 200, f"{s}")
new_rt = b["data"]["refreshToken"]
sql(f"UPDATE TRN_RefreshToken SET ExpiresUtc = DATEADD(second, -1, SYSUTCDATETIME()) WHERE UserId={a_id} AND ConsumedUtc IS NULL AND RevokedUtc IS NULL")
s, b = call("POST", "/auth/refresh", body={"refreshToken": new_rt})
check("8  idle past the window: refresh refused (TOKEN_EXPIRED)", s == 401 and "TOKEN_EXPIRED" in json.dumps(b), f"{s} {json.dumps(b)[:200]}")
A = login(a_email, CUST_PW)

# ---- 10: payment history --------------------------------------------------------------------------
s, b = call("GET", "/account/payments", A, params={"page": 1, "pageSize": 100})
items = (b.get("data") or {}).get("items") or []
n_db = int(one(f"SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={a_id}"))
check("10 lists every payment of the customer", s == 200 and len(items) == n_db and b["data"]["totalCount"] == n_db, f"{len(items)} vs {n_db}")
check("10 newest first", [i["createdUtc"] for i in items] == sorted([i["createdUtc"] for i in items], reverse=True))
check("10 only the customer's own rows", int(one(f"SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={a_id} AND PaymentId IN ({','.join(str(i['paymentId']) for i in items)})")) == len(items))
st = {i["displayStatus"] for i in items}
check("10 shows successful, pending, failed and refunded rows", {"Successful", "Pending", "Failed", "Refunded"} <= st, st)
row = next(i for i in items if i["bookingId"] == h_ok)
check("10 row has transaction id, booking ref, date, amount, method, status", row["transactionId"].startswith("pay_") and row["referenceNumber"] and row["paidUtc"] and row["amount"] > 0 and row["methodLabel"] == "Online" and row["displayStatus"] == "Successful", row)
s, b = call("GET", f"/account/payments/{row['paymentId']}", A)
check("10 detail for one transaction", s == 200 and b["data"]["paymentId"] == row["paymentId"] and b["data"]["description"])
b_pay = one(f"SELECT TOP 1 PaymentId FROM TRN_Payment WHERE CustomerId <> {a_id}")
s, _ = call("GET", f"/account/payments/{b_pay}", A)
check("10 another customer's payment is not found", s == 404, s)
s, _ = call("GET", "/account/payments", admin)
check("10 staff are refused (they use Finance > Payments)", s == 403, s)
sql(f"UPDATE TRN_Payment SET CreatedDate = DATEADD(minute, -31, CreatedDate) WHERE GatewayOrderId='{o_cancel}'")
s, b = call("GET", "/account/payments", A, params={"page": 1, "pageSize": 100})
ab = next(i for i in b["data"]["items"] if i["gatewayOrderId"] == o_cancel)
check("10 unfinished checkout older than 30 min reads 'Not completed'", ab["displayStatus"] == "Not completed" and ab["status"] == 1, ab["displayStatus"])
s, b = call("GET", "/account/payments", A, params={"page": 1, "pageSize": 100, "status": "refunded"})
check("10 status filter", s == 200 and all(i["displayStatus"] in ("Refunded", "Partly refunded") for i in b["data"]["items"]) and b["data"]["items"])

# ---- 9: delete account -------------------------------------------------------------------------------
s, b = call("GET", "/account/deletion", A)
codes = {x["code"] for x in b["data"]["blockers"]}
check("9  blocked while there are dues / bookings / passes / wallet money", s == 200 and not b["data"]["canDelete"] and {"DUES", "BOOKINGS", "PASSES", "WALLET"} <= codes, codes)
s, b = call("POST", "/account/deletion", A, {"password": CUST_PW})
check("9  delete refused with open items", s >= 400 and "ACCOUNT_HAS_OPEN_ITEMS" in json.dumps(b))
D = login(d_email, CUST_PW)
s, b = call("GET", "/account/deletion", D)
check("9  clean account can be deleted, by password", s == 200 and b["data"]["canDelete"] and b["data"]["method"] == "password", b.get("data"))
s, b = call("POST", "/account/deletion", D, {"password": "Wrong#Pass1"})
check("9  wrong password refused", s >= 400 and one(f"SELECT Status FROM MST_User WHERE UserId={d_id}") == "1")
s, b = call("POST", "/account/deletion", D, {"password": CUST_PW})
check("9  correct password: deleted", s == 200, json.dumps(b)[:200])
r = sql(f"SELECT Status, FullName, Email, MobileNumber, ISNULL(PasswordHash,'-'), ISNULL(City,'-'), IsDeleted FROM MST_User WHERE UserId={d_id}")[0]
check("9  account closed and anonymised (row kept)", r == ["2", "Deleted customer", f"deleted-{d_id}@deleted.invalid", "", "-", "-", "0"], r)
check("9  sessions revoked", one(f"SELECT COUNT(*) FROM TRN_RefreshToken WHERE UserId={d_id} AND RevokedUtc IS NULL") == "0")
check("9  audit row written", one(f"SELECT COUNT(*) FROM TRN_AuditLog WHERE EntityId='{d_id}' AND Action='ACCOUNT_DELETED'") == "1")
check("9  deleted account cannot sign in", login_full(d_email, CUST_PW, "customer") is None)
s, _ = call("GET", "/account/deletion", D)
check("9  its old token no longer works on account pages", s in (401, 404), s)
check("9  admin records kept: A's payments untouched", int(one(f"SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={a_id}")) == n_db)

sql(f"UPDATE MST_User SET PasswordHash = (SELECT PasswordHash FROM MST_User WHERE Email='{CUST_EMAIL}') WHERE UserId={g_id}")
G = login(g_email, CUST_PW)
sql(f"UPDATE MST_User SET PasswordHash = NULL WHERE UserId={g_id}")
s, b = call("GET", "/account/deletion", G)
check("9  Google-only account confirms by emailed code", s == 200 and b["data"]["method"] == "otp" and "•" in b["data"]["maskedEmail"], b.get("data"))
s, b = call("POST", "/account/deletion/code", G)
check("9  code sent", s == 200 and one(f"SELECT COUNT(*) FROM TRN_Otp WHERE UserId={g_id} AND Purpose=4 AND ConsumedUtc IS NULL") == "1", f"{s} {b}")
code = "246810"
h = hashlib.sha256(f"{code}|{g_email.lower()}".encode()).hexdigest().upper()
sql(f"UPDATE TRN_Otp SET CodeHash='{h}' WHERE UserId={g_id} AND Purpose=4 AND ConsumedUtc IS NULL")
s, b = call("POST", "/account/deletion", G, {"code": "111111"})
check("9  wrong code refused", s >= 400 and one(f"SELECT Status FROM MST_User WHERE UserId={g_id}") == "1")
s, b = call("POST", "/account/deletion", G, {"code": code})
check("9  right code: deleted, Google link removed", s == 200 and sql(f"SELECT Status, ISNULL(GoogleId,'-') FROM MST_User WHERE UserId={g_id}")[0] == ["2", "-"], json.dumps(b)[:200])
s, _ = call("GET", "/account/deletion", admin)
check("9  staff cannot use customer deletion", s == 403, s)

# ---- other features still work ----------------------------------------------------------------------
s, b = call("POST", "/passes/purchase", A, {"passPlanId": plan("DAYPASS-1"), "validFrom": str(monday), "paymentMethod": "wallet"})
check("-- wallet day pass still Active at once", s == 200 and b["data"]["pass"]["status"] == 1, json.dumps(b)[:200])
s, b = call("GET", "/passes", admin, params={"page": 1, "pageSize": 5})
check("-- admin pass list still loads", s == 200)
s, b = call("GET", "/cash/outstanding", admin)
check("-- admin outstanding list includes awaiting-payment passes", s == 200 and dp.get("passNumber", "?") in json.dumps(b), json.dumps(b)[:200])
s, b = call("GET", "/payments", admin, params={"page": 1, "pageSize": 5})
check("-- admin payments grid still loads", s == 200)

failed = [n for n, ok in results if not ok]
print(f"\n{len(results) - len(failed)}/{len(results)} passed")
sys.exit(1 if failed else 0)
