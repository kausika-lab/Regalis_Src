"""quotation-payment kit (27 Nov 2026), user requirement: a desk, cabin, meeting room or Virtual Office booked by
staff in the admin portal (Bookings > New booking, /api/admin/on-behalf/...) raises NO invoice and NO payment, and
is not tracked as pay later (it is billed through the customer's quotation invoice). LOCAL Dev stack only.

For a meeting-room slot, a monthly desk, a monthly cabin and a Virtual Office booked for a customer:
  * Confirmed / Active at once, priced exactly as the quote, source Front desk (2), created by the staff user;
  * zero invoices, payments, receipts, cash collections and wallet rows for the customer;
  * not pay later (no due date), so it is not in Cash Collections outstanding, Outstanding / ageing, the
    customer ledger or the Payments register; QR access token issued;
  * cancelling refunds nothing (nothing was paid) and writes no wallet row.
Unchanged: a customer-portal booking (wallet) and a customer pay-later booking still raise their invoice; a
customer-portal Virtual Office still raises its invoice; the old POST /api/bookings/admin (no screen uses it)
still invoices its cash booking.

Creates customers qpnb-<run>-*@example.test; cancels their bookings / passes and soft-deletes them at the end.
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD. Mail must go to the SMTP sink on 127.0.0.1:2525.
"""
import datetime as dt, json, os, random, socket, subprocess, sys, time, urllib.error, urllib.request
from decimal import Decimal as D

sys.stdout.reconfigure(encoding="utf-8")
API = "http://127.0.0.1:81/api"
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-b", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
today = dt.datetime.now(dt.timezone.utc).astimezone(IST).date()
LOC, DESK, CABIN, MEETING = 2, 1, 2, 3
results = []


def check(name, ok, detail=""):
    results.append(bool(ok))
    print(("PASS " if ok else "FAIL ") + name + ("" if ok else f"\n     -> {json.dumps(detail, default=str)[:600]}"), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [[c.strip() for c in l.split("|")] for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


def call(method, path, token=None, body=None, params=None):
    if params:
        path += "?" + "&".join(f"{k}={urllib.request.quote(str(v))}" for k, v in params.items() if v is not None)
    for attempt in range(8):
        req = urllib.request.Request(API + path, data=json.dumps(body).encode() if body is not None else None, method=method)
        req.add_header("Content-Type", "application/json")
        if token:
            req.add_header("Authorization", "Bearer " + token)
        try:
            with urllib.request.urlopen(req, timeout=90) as r:
                return r.status, json.loads(r.read() or b"{}")
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(10 + 5 * attempt)
                continue
            try:
                return e.code, json.loads(e.read() or b"{}")
            except Exception:
                return e.code, {}
    raise SystemExit("rate limited")


def login(email, pw, portal="admin"):
    s, b = call("POST", "/auth/login", None, {"email": email, "password": pw, "portal": portal})
    if s != 200:
        raise SystemExit(f"login {email}: {s} {b}")
    return b["data"]["accessToken"]


def dec(v):
    return D(str(v or 0)).quantize(D("0.01"))


def open_day(after):
    d = today + dt.timedelta(days=after)
    while d.weekday() == 6:
        d += dt.timedelta(days=1)
    return d


def at(d, hh):
    return dt.datetime(d.year, d.month, d.day, hh, 0, tzinfo=IST).astimezone(dt.timezone.utc).strftime("%Y-%m-%dT%H:%M:%SZ")


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so:
            return so.recv(100).startswith(b"220")
    except OSError:
        return False


if not sink_up():
    raise SystemExit("No SMTP sink on 127.0.0.1:2525.")
admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])
ADMIN_ID = int(one(f"SELECT UserId FROM MST_User WHERE Email='{os.environ['REGALIS_ADMIN_EMAIL']}' AND IsDeleted=0"))
ROLE = int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))
DAY = open_day(40 + random.randint(0, 15))
print(f"run {RUN}  day {DAY}", flush=True)


def make_customer(tag):
    pw = "QpNb#" + RUN + "x"
    em = f"qpnb-{RUN}-{tag}@example.test"
    s, b = call("POST", "/users", admin, {"fullName": f"QP NoInvoice {RUN} {tag}", "email": em, "mobileNumber": f"+9196{RUN}{random.randint(100, 999)}",
                                          "password": pw, "userType": 1, "roleIds": [ROLE], "defaultLocationId": LOC})
    if s not in (200, 201):
        raise SystemExit(f"create customer: {s} {b}")
    uid = b["data"]["userId"]
    sql(f"UPDATE MST_User SET MustChangePassword=0, Status=1, IsEmailVerified=1, IsMobileVerified=1, AddressLine1='1 Test Road', AddressLine2='Block A', City='Bengaluru', State='Karnataka',"
        f" Country='India', PostalCode='560098', Gender=1, PayLaterAllowed=1 WHERE UserId={uid}")
    return uid, em, pw


def money(cid):
    r = sql(f"""SELECT (SELECT COUNT(*) FROM TRN_Invoice WHERE CustomerId={cid}), (SELECT COUNT(*) FROM TRN_Payment WHERE CustomerId={cid}),
                       (SELECT COUNT(*) FROM TRN_InvoicePayment WHERE CustomerId={cid}), (SELECT COUNT(*) FROM TRN_CashCollection WHERE CustomerId={cid}),
                       (SELECT COUNT(*) FROM TRN_WalletTransaction WHERE CustomerId={cid})""")
    return tuple(int(x) for x in r[0])


def free_ws(type_id, start, minutes):
    s, b = call("GET", "/availability/workspaces", admin, params={"locationId": LOC, "workspaceTypeId": type_id, "startUtc": start, "durationMinutes": minutes, "includeBusy": "true"})
    return [w for w in (b.get("data") or []) if w.get("isAvailable") is not False]


def free_monthly(type_id, start):
    s, b = call("GET", "/availability/monthly", admin, params={"locationId": LOC, "workspaceTypeId": type_id, "startDate": start.isoformat(), "months": 1})
    return [w for w in (b.get("data") or []) if w.get("isAvailable") is not False and w.get("pricePerMonth")]


ca, ca_em, ca_pw = make_customer("a")
cb, cb_em, cb_pw = make_customer("b")
custB = login(cb_em, cb_pw, "customer")
call("POST", f"/wallet/{cb}/adjust", admin, {"direction": "credit", "amount": 20000, "reason": "qp e2e"})
made_b, made_p = [], []


def ob(path):
    return f"/admin/on-behalf/{ca}{path}"


def staff_booking_checks(label, bk_id, quote_total):
    r = sql(f"SELECT Status, BookingSource, CAST(IsPayLater AS int), CAST(AmountPaid AS decimal(12,2)), PaymentStatus, ISNULL(CONVERT(varchar(30), PaymentDueUtc), ''),"
            f" CreatedByUserId, CAST(TotalAmount AS decimal(12,2)) FROM TRN_Booking WHERE BookingId={bk_id}")[0]
    check(f"{label}: Confirmed, source Front desk (2), created by the staff user", r[0] == "2" and r[1] == "2" and int(r[6]) == ADMIN_ID, r)
    check(f"{label}: priced as the quote and stored on the booking", dec(r[7]) == dec(quote_total), (r[7], quote_total))
    check(f"{label}: not pay later, nothing paid, no due date", r[2] == "0" and dec(r[3]) == 0 and r[5] == "", r)
    check(f"{label}: no invoice for the booking", one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE BookingId={bk_id}") == "0")
    check(f"{label}: no payment for the booking", one(f"SELECT COUNT(*) FROM TRN_Payment WHERE BookingId={bk_id}") == "0")
    check(f"{label}: QR access token issued", one(f"SELECT COUNT(*) FROM TRN_AccessToken WHERE BookingId={bk_id} AND Status=1") == "1")
    h = one(f"SELECT TOP 1 Reason FROM TRN_BookingStatusHistory WHERE BookingId={bk_id} AND ToStatus=2 ORDER BY BookingStatusHistoryId DESC") or ""
    check(f"{label}: status history says billed through the quotation, no invoice or payment", "no invoice or payment" in h, h)


M0 = money(ca)
# ---- meeting room slot ------------------------------------------------------------------------------
st = at(DAY, 11)
rooms = free_ws(MEETING, st, 60)
check("setup: a free meeting room", bool(rooms), rooms)
body = {"workspaceId": rooms[0]["workspaceId"], "startUtc": st, "durationMinutes": 60}
s, q = call("POST", "/bookings/quote", admin, body)
s, b = call("POST", ob("/bookings"), admin, body)
h1 = b.get("data") or {}
made_b.append(h1.get("bookingId"))
s, b = call("POST", ob(f"/bookings/{h1.get('bookingId')}/confirm"), admin, {})
c = b.get("data") or {}
check("meeting room: confirm 200 with no invoice and no payment in the reply",
      s == 200 and c.get("invoiceId") == 0 and c.get("invoiceNumber") == "" and c.get("paymentId") == 0 and c["booking"]["isPayLater"] is False, (s, b))
staff_booking_checks("meeting room", h1["bookingId"], q["data"]["totalAmount"])
note = sql(f"SELECT Message FROM TRN_Notification WHERE UserId={ca} AND ReferenceType='Booking' AND ReferenceId={h1['bookingId']}")
check("meeting room: customer notified 'Booking confirmed' without 'pay at the front desk'", len(note) == 1 and "front desk" not in note[0][0], note)

# ---- monthly desk and monthly cabin ------------------------------------------------------------------
for label, tid in (("monthly desk", DESK), ("monthly cabin", CABIN)):
    ws = free_monthly(tid, DAY)
    if not ws:
        s, allb = call("GET", "/availability/monthly", admin, params={"locationId": LOC, "workspaceTypeId": tid, "startDate": DAY.isoformat(), "months": 1})
        reasons = {w.get("unavailableReason") for w in (allb.get("data") or [])}
        if reasons == {"NoMonthlyPrice"}:
            print(f"SKIP {label}: no workspace of this type has a monthly price on this database, so nobody can book it (same code path as the monthly desk)", flush=True)
        else:
            check(f"{label}: a free workspace for the term", False, reasons)
        continue
    mreq = {"workspaceId": ws[0]["workspaceId"], "startDate": DAY.isoformat(), "months": 1}
    s, mq = call("POST", "/bookings/monthly/quote", admin, mreq)
    s, b = call("POST", ob("/bookings/monthly"), admin, mreq)
    mh = b.get("data") or {}
    made_b.append(mh.get("bookingId"))
    s, b = call("POST", ob(f"/bookings/{mh.get('bookingId')}/confirm"), admin, {})
    check(f"{label}: confirm 200, no invoice in the reply", s == 200 and (b.get("data") or {}).get("invoiceId") == 0, (s, b))
    staff_booking_checks(label, mh["bookingId"], mq["data"]["totalAmount"])

# ---- Virtual Office ------------------------------------------------------------------------------------
vo_plan = int(one("SELECT TOP 1 PassPlanId FROM MST_PassPlan WHERE PlanCode='MEM-VIRTUAL-1M' AND IsActive=1"))
s, b = call("POST", ob("/virtual-office"), admin, {"passPlanId": vo_plan, "validFrom": DAY.isoformat()})
vo = (b.get("data") or {}).get("pass") or {}
made_p.append(vo.get("customerPassId"))
check("Virtual Office: 200, Active at once (1), not pay later, nothing paid, no invoice / payment in the reply",
      s == 200 and vo.get("status") == 1 and vo.get("isPayLater") is False and dec(vo.get("amountPaid")) == 0 and vo.get("invoiceId") in (None, 0)
      and b["data"]["invoiceId"] == 0 and b["data"]["paymentNumber"] == "", (s, b))
pid = vo.get("customerPassId", 0)
r = sql(f"SELECT ISNULL(InvoiceId,0), ISNULL(PaymentId,0), ISNULL(CONVERT(varchar(30),PaymentDueUtc),''), CAST(TotalAmount AS decimal(12,2)) FROM TRN_CustomerPass WHERE CustomerPassId={pid}")
check("Virtual Office: no invoice, no payment, no due date on the pass; price stored", bool(r) and r[0][:3] == ["0", "0", ""] and dec(r[0][3]) > 0, r)
check("Virtual Office: no invoice row for the pass", one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE CustomerPassId={pid}") == "0")
check("Virtual Office: QR / access token issued", one(f"SELECT COUNT(*) FROM TRN_AccessToken WHERE CustomerPassId={pid} AND Status=1") == "1")

# ---- nothing on the money side --------------------------------------------------------------------------
check("customer A: zero invoices, payments, receipts, cash collections, wallet rows", money(ca) == M0 == (0, 0, 0, 0, M0[4]), (M0, money(ca)))
s, b = call("GET", "/cash/outstanding", admin, params={"pageSize": 100, "search": f"QP NoInvoice {RUN} a"})
rows = (b.get("data") or {}).get("items") or []
check("Cash Collections outstanding: nothing for the staff bookings", s == 200 and not any(x.get("customerId") == ca for x in rows), rows[:2])
s, b = call("GET", "/payments/register", admin, params={"search": f"QP NoInvoice {RUN} a"})
check("Payments register: nothing for the customer", s == 200 and ((b.get("data") or {}).get("items") or []) == [], b)
s, b = call("GET", "/parties/customer/outstanding", admin, params={"search": f"QP NoInvoice {RUN} a"})
txt = json.dumps(b)
check("Outstanding: the customer owes nothing (no row, or 0)", s == 200 and (str(ca) not in txt or '"outstanding": 0' in txt.replace(".0", "")), (s, txt[:300]))
s, b = call("GET", f"/parties/customer/{ca}/ledger", admin)
check("customer ledger answers without error", s == 200, (s, b))

# ---- cancelling refunds nothing --------------------------------------------------------------------------
W0 = money(ca)[4]
s, b = call("POST", f"/bookings/{h1['bookingId']}/cancel", admin, {"reason": "qp e2e"})
check("cancel the meeting room: 200, refund 0, no wallet row", s == 200 and dec((b.get("data") or {}).get("refundAmount")) == 0 and money(ca)[4] == W0, (s, b))
s, b = call("POST", f"/passes/{pid}/cancel", admin)
check("cancel the Virtual Office: 200, no refund (no wallet row)", s == 200 and money(ca)[4] == W0, (s, b, money(ca)))

# ---- unchanged: customer portal and the old endpoint -----------------------------------------------------
B0 = money(cb)
st2 = at(DAY, 15)
r2 = free_ws(MEETING, st2, 60)
s, h = call("POST", "/bookings", custB, {"workspaceId": r2[0]["workspaceId"], "startUtc": st2, "durationMinutes": 60})
hb = h.get("data") or {}
made_b.append(hb.get("bookingId"))
s, b = call("POST", f"/bookings/{hb.get('bookingId')}/confirm", custB, {"paymentMethod": "wallet"})
check("customer portal, wallet: Paid, its invoice raised (Paid), one payment", s == 200 and b["data"]["invoiceId"] > 0
      and one(f"SELECT Status FROM TRN_Invoice WHERE BookingId={hb.get('bookingId')}") == "2" and one(f"SELECT COUNT(*) FROM TRN_Payment WHERE BookingId={hb.get('bookingId')}") == "1", (s, b))
st3 = at(DAY, 17)
r3 = free_ws(MEETING, st3, 60)
s, h = call("POST", "/bookings", custB, {"workspaceId": r3[0]["workspaceId"], "startUtc": st3, "durationMinutes": 60})
hb2 = h.get("data") or {}
made_b.append(hb2.get("bookingId"))
s, b = call("POST", f"/bookings/{hb2.get('bookingId')}/confirm", custB, {"paymentMethod": "paylater"})
check("customer portal, pay later: pay later, its invoice raised and due, no payment", s == 200 and b["data"]["booking"]["isPayLater"] is True
      and one(f"SELECT CONCAT(Status,'-',CASE WHEN PaidAmount=0 THEN 'zero' ELSE 'paid' END) FROM TRN_Invoice WHERE BookingId={hb2.get('bookingId')}") == "1-zero", (s, b))
s, b = call("POST", "/passes/purchase", custB, {"passPlanId": vo_plan, "validFrom": DAY.isoformat(), "paymentMethod": "wallet"})
pv = ((b.get("data") or {}).get("pass") or {}).get("customerPassId")
made_p.append(pv)
check("customer portal Virtual Office (wallet): Active, invoice raised", s == 200 and b["data"]["invoiceId"] > 0 and one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE CustomerPassId={pv}") == "1", (s, b))
st4 = at(DAY, 9)
r4 = free_ws(MEETING, st4, 60)
s, b = call("POST", "/bookings/admin", admin, {"customerId": cb, "workspaceId": r4[0]["workspaceId"], "startUtc": st4, "durationMinutes": 60, "paymentMethod": "cash"})
old = ((b.get("data") or {}).get("booking") or {}).get("bookingId")
made_b.append(old)
check("old POST /bookings/admin (cash, no screen uses it) unchanged: paid, payment and invoice", s == 200 and b["data"]["paymentId"] > 0 and b["data"]["invoiceId"] > 0, (s, b))

# ---- clean-up -----------------------------------------------------------------------------------------------
left = []
for bid in [x for x in made_b if x]:
    if one(f"SELECT Status FROM TRN_Booking WHERE BookingId={bid}") in ("1", "2"):
        s, _ = call("POST", f"/bookings/{bid}/cancel", admin, {"reason": "qp e2e clean-up"})
        if s != 200:
            left.append(f"booking {bid} ({s})")
for p in [x for x in made_p if x]:
    if one(f"SELECT Status FROM TRN_CustomerPass WHERE CustomerPassId={p}") in ("1", "5"):
        s, _ = call("POST", f"/passes/{p}/cancel", admin)
        if s != 200:
            left.append(f"pass {p} ({s})")
for uid in (ca, cb):
    s, _ = call("DELETE", f"/users/{uid}", admin)
    if s not in (200, 204):
        left.append(f"user {uid} ({s})")
print("\nTest data left in Dev (run " + RUN + "): " + (", ".join(left) if left else "none (invoices of the customer-portal bookings stay, cancelled)"))
print(f"\n{sum(results)}/{len(results)} passed, {len(results) - sum(results)} failed")
sys.exit(0 if results and all(results) else 1)
