"""End-to-end test of Pay Later against the test API (localhost:5099, DB RegalisWorkSpace_PayLaterTest)."""
import json, os, subprocess, sys, urllib.request, urllib.error, datetime as dt

# Needs: the API with this kit on BASE, DB a disposable copy with both migrations applied,
# PayLater.Enabled = true and Wallet.DeskAddMoneyEnabled = false, CUSTOMER_ID / the two logins
# below having password PWD, workspaces 1-4 bookable at location 2. Never point it at UAT data.
HERE = os.path.dirname(os.path.abspath(__file__))
BASE = os.environ.get("PAYLATER_API", "http://localhost:5099/api")
DB = "RegalisWorkSpace_PayLaterTest"
PWD = "PayLater#Test1"
CUSTOMER_ID = 10032
results = []


def sql(q):
    out = subprocess.run(["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-I", "-d", DB, "-b", "-h", "-1", "-W",
                          "-s", "|", "-Q", "SET NOCOUNT ON; " + q], capture_output=True, text=True)
    if out.returncode:
        raise RuntimeError(out.stdout + out.stderr)
    return [l.strip() for l in out.stdout.splitlines() if l.strip()]


def call(method, path, token=None, body=None, raw=False):
    req = urllib.request.Request(BASE + path, method=method,
                                 data=None if body is None else json.dumps(body).encode())
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req) as r:
            data = r.read()
            if raw:
                return r.status, r.headers.get("Content-Type"), data
            return (200 if r.status in (200, 201) else r.status), json.loads(data or b"null")
    except urllib.error.HTTPError as e:
        data = e.read()
        try:
            return e.code, json.loads(data)
        except Exception:
            return e.code, data.decode(errors="replace")


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + ("" if ok else f"  -> {detail}"))


def data(resp):
    return resp[1].get("data") if isinstance(resp[1], dict) else None


def err(resp):
    b = resp[1]
    if isinstance(b, dict):
        return json.dumps(b.get("errors") or b.get("message") or b)[:300]
    return str(b)[:300]


def login(email, portal):
    r = call("POST", "/auth/login", body={"email": email, "password": PWD, "portal": portal})
    assert r[0] == 200, r
    return data(r)["accessToken"]


cust = login("kausiraj21@gmail.com", "customer")
admin = login("admin@amstag.in", "admin")
check("logins", cust and admin)


def slot(days, hour_ist):
    d = (dt.datetime.now(dt.timezone.utc).replace(tzinfo=None) + dt.timedelta(days=days)).date()
    return (dt.datetime(d.year, d.month, d.day, hour_ist, 0) - dt.timedelta(hours=5, minutes=30)).strftime("%Y-%m-%dT%H:%M:%SZ")


def hold(ws, start, minutes=60):
    r = call("POST", "/bookings", cust, {"workspaceId": ws, "startUtc": start, "durationMinutes": minutes, "notes": "pay later e2e"})
    assert r[0] == 200, ("hold", r)
    return data(r)


# 1. Eligibility + confirm with pay later
b1 = hold(1, slot(2, 11))
r = call("GET", f"/bookings/{b1['bookingId']}/pay-later", cust)
check("eligibility allowed for first booking", r[0] == 200 and data(r)["allowed"], r)
r = call("POST", f"/bookings/{b1['bookingId']}/confirm", cust, {"paymentMethod": "paylater"})
check("confirm with paylater", r[0] == 200 and data(r)["isPayLater"], err(r))
conf = data(r) or {}
row = sql(f"SELECT b.Status, b.PaymentStatus, b.IsPayLater, b.AmountPaid, CONVERT(varchar, b.PaymentDueUtc, 126), i.Status, i.PaidAmount, i.PaymentId "
          f"FROM TRN_Booking b JOIN TRN_Invoice i ON i.BookingId = b.BookingId WHERE b.BookingId = {b1['bookingId']}")[0].split("|")
check("booking Confirmed, payment Pending, IsPayLater, nothing paid", row[:3] == ["2", "1", "1"] and float(row[3]) == 0, row)
check("invoice issued Pending with 0 paid and no payment", row[5] == "1" and float(row[6]) == 0 and row[7] == "NULL", row)
check("access token issued", bool(conf.get("qrPayload")))
total = float(conf["booking"]["totalAmount"])

# 2. Check-in blocked while money is due (token window opened for the test)
sql(f"UPDATE TRN_AccessToken SET ValidFromUtc = DATEADD(MINUTE,-5,SYSUTCDATETIME()) WHERE BookingId = {b1['bookingId']}")
r = call("POST", "/access/validate", admin, {"payload": conf["qrPayload"], "workspaceId": 1, "direction": 1, "deviceId": None, "accessMethod": 1})
d = data(r) or {}
check("gate refuses check-in: PaymentDue", r[0] == 200 and not d.get("isGranted") and d.get("denyReason") == "PaymentDue", r)
r = call("POST", "/access/manual-override", admin, {"userId": CUSTOMER_ID, "locationId": 2, "workspaceId": 1, "direction": 1, "bookingId": b1["bookingId"], "reason": "test"})
check("manual override refused while money is due", r[0] in (400, 409, 422) and "PAYMENT_DUE" in json.dumps(r[1]), r)

# 3. Open-bookings limit (2): second allowed, third refused
b2 = hold(2, slot(2, 13))
r = call("POST", f"/bookings/{b2['bookingId']}/confirm", cust, {"paymentMethod": "paylater"})
check("second pay-later booking confirmed", r[0] == 200, err(r))
b3 = hold(3, slot(2, 15))
r = call("GET", f"/bookings/{b3['bookingId']}/pay-later", cust)
check("third refused by open-bookings limit (eligibility)", r[0] == 200 and not data(r)["allowed"] and "unpaid" in data(r)["reason"], r)
r = call("POST", f"/bookings/{b3['bookingId']}/confirm", cust, {"paymentMethod": "paylater"})
check("third refused by open-bookings limit (confirm)", r[0] in (400, 422) and "PAY_LATER_NOT_ALLOWED" in json.dumps(r[1]), r)

# 4. Desk collections
part = round(total / 2, 2)
r = call("POST", "/cash/collections", admin, {"bookingId": b1["bookingId"], "amount": part, "paymentMethod": 6, "amountTendered": part + 100, "referenceNumber": None, "notes": None})
c1 = data(r) or {}
check("cash part payment recorded", r[0] == 200 and c1.get("changeReturned") == 100 and c1.get("receiptNumber", "").startswith("REG/RCPT"), r)
row = sql(f"SELECT b.PaymentStatus, b.AmountPaid, i.Status, i.PaidAmount FROM TRN_Booking b JOIN TRN_Invoice i ON i.BookingId=b.BookingId WHERE b.BookingId={b1['bookingId']}")[0].split("|")
check("booking PartiallyPaid(6) and invoice PartiallyPaid(3)", row[0] == "6" and row[2] == "3" and float(row[1]) == part, row)
r = call("POST", "/cash/collections", admin, {"bookingId": b1["bookingId"], "amount": total, "paymentMethod": 6, "amountTendered": None, "referenceNumber": None, "notes": None})
check("over-payment refused", r[0] in (400, 422) and "due" in json.dumps(r[1]), r)
r = call("POST", "/cash/collections", admin, {"bookingId": b1["bookingId"], "amount": 1, "paymentMethod": 2, "amountTendered": None, "referenceNumber": None, "notes": None})
check("UPI without reference refused", r[0] == 400, r)
rest = round(total - part, 2)
r = call("POST", "/cash/collections", admin, {"bookingId": b1["bookingId"], "amount": rest, "paymentMethod": 2, "amountTendered": None, "referenceNumber": "UPI123456", "notes": "test"})
c2 = data(r) or {}
check("UPI balance recorded", r[0] == 200 and c2.get("bookingBalance") == 0, r)
row = sql(f"SELECT b.PaymentStatus, i.Status, i.PaidAmount, CASE WHEN i.PaymentId IS NULL THEN 0 ELSE 1 END FROM TRN_Booking b JOIN TRN_Invoice i ON i.BookingId=b.BookingId WHERE b.BookingId={b1['bookingId']}")[0].split("|")
check("booking Paid(2), invoice Paid(2) with a payment", row[0] == "2" and row[1] == "2" and row[3] == "1", row)
r = call("POST", "/access/validate", admin, {"payload": conf["qrPayload"], "workspaceId": 1, "direction": 1, "deviceId": None, "accessMethod": 1})
check("gate no longer says PaymentDue once paid", r[0] == 200 and (data(r) or {}).get("denyReason") != "PaymentDue", r)

# 5. Void, receipt PDF, lists
r = call("POST", f"/cash/collections/{c2['cashCollectionId']}/void", admin, {"reason": "Wrong UPI reference"})
check("void UPI receipt", r[0] == 200 and data(r)["status"] == 2, r)
row = sql(f"SELECT b.PaymentStatus, b.AmountPaid, i.Status, p.Status FROM TRN_Booking b JOIN TRN_Invoice i ON i.BookingId=b.BookingId JOIN TRN_Payment p ON p.PaymentId={c2['paymentId']} WHERE b.BookingId={b1['bookingId']}")[0].split("|")
check("void reverses totals: booking PartiallyPaid, invoice PartiallyPaid, payment Cancelled(5)", row[0] == "6" and row[2] == "3" and row[3] == "5" and float(row[1]) == part, row)
r = call("POST", f"/cash/collections/{c2['cashCollectionId']}/void", admin, {"reason": "again"})
check("second void refused", r[0] in (400, 422), r)
st, ctype, pdf = call("GET", f"/cash/collections/{c2['cashCollectionId']}/receipt", admin, raw=True)
check("receipt PDF", st == 200 and ctype.startswith("application/pdf") and pdf[:4] == b"%PDF", (st, ctype))
open(os.path.join(HERE, "sample-receipt.pdf"), "wb").write(pdf)
r = call("GET", "/cash/outstanding?pageSize=50", admin)
ids = [x["bookingId"] for x in data(r)["items"]]
check("outstanding lists both unpaid pay-later bookings", b1["bookingId"] in ids and b2["bookingId"] in ids, ids)
r = call("GET", f"/cash/collections?bookingId={b1['bookingId']}", admin)
check("collections list shows 2 receipts (1 voided)", r[0] == 200 and data(r)["totalCount"] == 2, r)
r = call("GET", "/cash/collections", cust)
check("customer cannot reach cash endpoints", r[0] in (401, 403), r[0])

# 6. Register: close needs a reason for a difference; approve; closed register refuses more cash
r = call("GET", "/cash/register/current?locationId=2", admin)
sess = data(r)
check("register session opened automatically", r[0] == 200 and sess and sess["cashCollected"] == part and sess["expectedCash"] == part, r)
r = call("POST", f"/cash/register/{sess['cashRegisterSessionId']}/close", admin, {"countedCash": part - 10, "varianceReason": None})
check("close with a difference and no reason refused", r[0] in (400, 422), r)
r = call("POST", f"/cash/register/{sess['cashRegisterSessionId']}/close", admin, {"countedCash": part - 10, "varianceReason": "Rs 10 short, change error"})
check("close with reason: variance -10", r[0] == 200 and data(r)["variance"] == -10 and data(r)["status"] == 2, r)
r = call("POST", f"/cash/register/{sess['cashRegisterSessionId']}/approve", admin)
check("Admin approves: HandedOver", r[0] == 200 and data(r)["status"] == 3, r)
r = call("POST", "/cash/collections", admin, {"bookingId": b1["bookingId"], "amount": 1, "paymentMethod": 6, "amountTendered": None, "referenceNumber": None, "notes": None})
check("closed register refuses more collections today", r[0] in (400, 422) and "closed" in json.dumps(r[1]), r)

# 7. Cancel an unpaid pay-later booking: no refund, invoice cancelled
r = call("POST", f"/bookings/{b2['bookingId']}/cancel", cust, {"reason": "plans changed"})
check("cancel unpaid pay-later booking", r[0] == 200 and data(r)["refundAmount"] == 0, r)
row = sql(f"SELECT b.Status, i.Status FROM TRN_Booking b JOIN TRN_Invoice i ON i.BookingId=b.BookingId WHERE b.BookingId={b2['bookingId']}")[0].split("|")
check("booking Cancelled(5), invoice Cancelled(4)", row == ["5", "4"], row)

# 8. Wallet desk top-up switched off; admin booking with pay later
r = call("POST", f"/admin/wallets/{CUSTOMER_ID}/add-money", admin, {"amount": 100, "paymentMethod": 6, "reference": None, "receivedOn": dt.date.today().isoformat(), "narration": None, "notifyCustomer": False})
check("desk wallet top-up refused (DESK_TOPUP_DISABLED)", r[0] in (400, 422) and "DESK_TOPUP_DISABLED" in json.dumps(r[1]), r)
r = call("POST", "/bookings/admin", admin, {"customerId": CUSTOMER_ID, "workspaceId": 4, "startUtc": slot(3, 11), "durationMinutes": 60, "notes": None, "paymentMethod": "paylater"})
check("admin booking with paylater", r[0] == 200 and data(r)["isPayLater"], err(r))

# 9. A normal wallet/gateway path still validates as before
r = call("POST", f"/bookings/{b3['bookingId']}/confirm", cust, {"paymentMethod": "cash"})
check("customer cash still refused", r[0] == 403, r)

print(f"\n{sum(ok for _, ok in results)}/{len(results)} passed")
json.dump({"b1": b1["bookingId"], "b2": b2["bookingId"], "b3": b3["bookingId"]}, open(os.path.join(HERE, "e2e_ids.json"), "w"))
sys.exit(0 if all(ok for _, ok in results) else 1)
