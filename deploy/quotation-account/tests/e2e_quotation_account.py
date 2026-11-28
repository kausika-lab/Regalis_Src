"""quotation-account kit (27 Nov 2026): API + SQL checks, LOCAL Dev stack only.

A quotation typed in for a lead / walk-in (no customer account) must, when approved or converted, link
the customer with the same email (else mobile) or create the account, then raise its one invoice. A staff
email never links: the approval stands, without an invoice, with the reason. Also: Invoices search finds
the quotation number / reference number.

  API  http://127.0.0.1:81  (D:\\Regalis\\local-dev\\api)    DB localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev
  Mail must go to the local SMTP sink on 127.0.0.1:2525 (the script stops first if nothing answers there);
  QA_MAIL_DIR = the sink's folder (optional; used to check no welcome mail is sent).

Creates: quotations with subject "QA-E2E <run> ...", customers qa-<run>-*@example.test, their invoices,
one manual payment. Removes at the end through the API: the payment is reversed, invoices cancelled,
open quotations cancelled, test customers deleted (soft). Approved quotation numbers stay (never reused).
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD.

usage: python -I e2e_quotation_account.py
"""
import datetime as dt, json, os, random, socket, subprocess, sys, threading, time, urllib.error, urllib.request

sys.stdout.reconfigure(encoding="utf-8")
API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
MAIL_DIR = os.environ.get("QA_MAIL_DIR", "")
RUN = str(random.randint(10000, 99999))
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
TODAY = dt.datetime.now(dt.timezone.utc).astimezone(IST).date()
ADMIN_EMAIL = os.environ["REGALIS_ADMIN_EMAIL"]
ADMIN_PW = os.environ["REGALIS_ADMIN_PASSWORD"]

results, CREATED_Q, CREATED_INV, CREATED_USERS, CREATED_PAY = [], [], [], [], []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + ("" if ok else f"\n     -> {json.dumps(detail, default=str)[:700]}"), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [[c.strip() for c in l.split("|")] for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


TOK = {}


def _call(method, path, token, body):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
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


def login(em, pw, portal):
    for attempt in range(8):
        s, b = _call("POST", "/auth/login", None, {"email": em, "password": pw, "portal": portal})
        if s == 200:
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(15 + 5 * attempt)
            continue
        return None
    return None


def call(method, path, body=None, params=None, who="admin"):
    if params:
        path += "?" + "&".join(f"{k}={urllib.request.quote(str(v))}" for k, v in params.items() if v is not None)
    for attempt in range(8):
        s, b = _call(method, path, TOK.get(who), body)
        if s == 401 and who == "admin":
            TOK["admin"] = login(ADMIN_EMAIL, ADMIN_PW, "admin")
            continue
        if s == 429:
            time.sleep(10 + 5 * attempt)
            continue
        return s, b
    return s, b


def ok2(s):
    return 200 <= s < 300


def data(b):
    return (b or {}).get("data") or {}


def items(b):
    d = (b or {}).get("data")
    if isinstance(d, dict):
        return d.get("items") or []
    return d or []


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so:
            return so.recv(100).startswith(b"220")
    except OSError:
        return False


def mails_to(addr):
    if not MAIL_DIR or not os.path.isdir(MAIL_DIR):
        return None
    n = 0
    for fn in os.listdir(MAIL_DIR):
        head = open(os.path.join(MAIL_DIR, fn), "rb").read(400).lower()
        if addr.lower().encode() in head:
            n += 1
    return n


def free_mobile():
    while True:
        m = "9" + "".join(random.choice("0123456789") for _ in range(9))
        if one(f"SELECT COUNT(*) FROM MST_User WHERE MobileNumber IN ('{m}','+91{m}')") == "0":
            return m


def make_quote(tag, name, gstin=None, address=None, customer_id=None, advance=False, reference=None):
    body = {"customerId": customer_id, "locationId": 2, "quotationDate": TODAY.isoformat(), "validUntil": (TODAY + dt.timedelta(days=15)).isoformat(),
            "subject": f"QA-E2E {RUN} {tag}", "placeOfSupplyStateCode": "29", "referenceNumber": reference,
            "customerName": None if customer_id else name, "billingAddress": address, "customerGstin": gstin,
            "advanceInvoiceEnabled": advance or None, "advanceType": 2 if advance else None, "advanceValue": 5000 if advance else None,
            "lines": [{"lineType": 2, "serviceType": 5, "description": f"QA-E2E {RUN} {tag}", "quantity": 1, "unitPrice": 1000,
                       "discountAmount": 0, "taxPercent": 18, "isNonTaxable": False}]}
    s, b = call("POST", "/quotations", body)
    if not ok2(s):
        raise SystemExit(f"create quotation {tag}: {s} {json.dumps(b)[:600]}")
    CREATED_Q.append(data(b)["quotationId"])
    return data(b)["quotationId"], data(b)


def send(qid, email_to, mobile=None):
    return call("POST", f"/quotations/{qid}/send", {"sendEmail": True, "sendWhatsApp": bool(mobile), "emailTo": email_to, "emailCc": None, "bccMe": False,
                                                    "emailSubject": None, "emailMessage": None, "whatsAppTo": mobile, "whatsAppMessage": None})


def approve(qid, approved=True):
    return call("POST", f"/quotations/{qid}/record-decision", {"approved": approved, "mode": 2, "confirmedBy": "QA tester",
                                                               "note": f"QA-E2E {RUN}", "reasonCode": None if approved else "PRICE"})


def get_q(qid):
    return data(call("GET", f"/quotations/{qid}")[1])


def invoices_of(qid):
    return int(one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={qid} AND IsDeleted=0") or 0)


def users_with(email):
    return sql(f"SELECT UserId, UserType FROM MST_User WHERE LOWER(Email)=LOWER('{email}') AND IsDeleted=0")


def notes(q):
    return [e.get("summary") or "" for e in (q.get("events") or [])]


def track(q):
    if q.get("invoiceId") and q["invoiceId"] not in CREATED_INV:
        CREATED_INV.append(q["invoiceId"])
    if q.get("customerId") and q["customerId"] not in CREATED_USERS and one(f"SELECT COUNT(*) FROM MST_User WHERE UserId={q['customerId']} AND Email LIKE 'qa-{RUN}-%@example.test'") == "1":
        CREATED_USERS.append(q["customerId"])


def lead(tag, email_to, mobile=None, **kw):
    qid, q = make_quote(tag, f"QA Lead {RUN} {tag}", **kw)
    s, b = send(qid, email_to, mobile)
    if not ok2(s):
        raise SystemExit(f"send {tag}: {s} {json.dumps(b)[:400]}")
    return qid


if not sink_up():
    raise SystemExit("No SMTP sink on 127.0.0.1:2525: start deploy/customer-portal/tests/smtp_sink.py and point the API at it first.")
TOK["admin"] = login(ADMIN_EMAIL, ADMIN_PW, "admin")
if not TOK["admin"]:
    raise SystemExit("admin login failed")
ADMIN_NAME = one(f"SELECT FullName FROM MST_User WHERE Email='{ADMIN_EMAIL}' AND IsDeleted=0")
CUSTOMER_ROLE = int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))
print(f"run {RUN}  today(IST) {TODAY}", flush=True)

try:
    # ---- 1. new email -> customer created through the Customers module, invoice raised ------------------
    e1, m1 = f"qa-{RUN}-new@example.test", free_mobile()
    q1 = lead("NEW", e1, m1, gstin="29ABCDE1234F1Z5", address="12 Test Street, Bengaluru 560001")
    before_mail = mails_to(e1)
    s, b = approve(q1)
    q = data(b)
    track(q)
    check("1 new email: Mark approved answers 200 with the invoice number", ok2(s) and bool(q.get("invoiceNumber")), (s, b))
    u = users_with(e1)
    check("1 new email: exactly one customer account (UserType 1) with that email", len(u) == 1 and u[0][1] == "1", u)
    uid = int(u[0][0]) if u else 0
    check("1 new email: quotation linked to it", q.get("customerId") == uid, q.get("customerId"))
    row = sql(f"SELECT FullName, MobileNumber, Gstin, ISNULL(Address,''), DefaultLocationId, MustChangePassword, IsEmailVerified, Status FROM MST_User WHERE UserId={uid}")
    check("1 new email: account carries name, +91 mobile, GSTIN, billing address, location 2; Active; must change password",
          bool(row) and row[0][0] == f"QA Lead {RUN} NEW" and row[0][1] == "+91" + m1 and row[0][2] == "29ABCDE1234F1Z5"
          and row[0][3].startswith("12 Test Street") and row[0][4] == "2" and row[0][5] == "1" and row[0][7] == "1", row)
    check("1 new email: CUSTOMER role and a wallet, as admin New user gives",
          one(f"SELECT COUNT(*) FROM MAP_UserRole WHERE UserId={uid} AND RoleId={CUSTOMER_ROLE}") == "1"
          and one(f"SELECT COUNT(*) FROM MST_Wallet WHERE CustomerId={uid}") == "1", uid)
    check("1 new email: exactly one invoice, to the new customer", invoices_of(q1) == 1
          and one(f"SELECT CustomerId FROM TRN_Invoice WHERE QuotationId={q1}") == str(uid), invoices_of(q1))
    check("1 new email: timeline says the account was created", any("created from the quotation's details" in n for n in notes(q)), notes(q))
    check("1 new email: audit row QUOTATION_CUSTOMER_CREATED",
          one(f"SELECT COUNT(*) FROM TRN_AuditLog WHERE Action='QUOTATION_CUSTOMER_CREATED' AND EntityName='Quotation' AND EntityId='{q1}'") == "1", q1)
    time.sleep(2)
    after_mail = mails_to(e1)
    if after_mail is not None:
        check("1 new email: no extra mail to the customer (the Customers path sends none)", after_mail == before_mail, (before_mail, after_mail))

    # ---- 2. existing customer by email (different case) -> linked, no second account ------------------
    e2, m2 = f"qa-{RUN}-exist@example.test", free_mobile()
    s, b = call("POST", "/users", {"fullName": f"QA Existing {RUN}", "email": e2, "mobileNumber": "+91" + m2, "password": "Qa-Test#2026x",
                                    "userType": 1, "roleIds": [CUSTOMER_ROLE], "companyName": None, "defaultLocationId": 2})
    check("2 setup: admin New user creates the existing customer", ok2(s), (s, b))
    exist_id = data(b).get("userId")
    if exist_id:
        CREATED_USERS.append(exist_id)
    q2 = lead("EXIST", e2.upper(), free_mobile())
    s, b = approve(q2)
    q = data(b)
    track(q)
    check("2 existing email: linked to the existing customer", ok2(s) and q.get("customerId") == exist_id, (s, q.get("customerId"), exist_id))
    check("2 existing email: still one account with that email", len(users_with(e2)) == 1, users_with(e2))
    check("2 existing email: one invoice to that customer", invoices_of(q2) == 1 and one(f"SELECT CustomerId FROM TRN_Invoice WHERE QuotationId={q2}") == str(exist_id))
    check("2 existing email: timeline says linked, no second account", any("No second account was created" in n for n in notes(q)), notes(q))

    # ---- 3. existing customer by mobile (10-digit form), new email -> linked --------------------------
    e3 = f"qa-{RUN}-other@example.test"
    q3 = lead("MOBILE", e3, m2)
    s, b = approve(q3)
    q = data(b)
    track(q)
    check("3 same mobile: linked to the existing customer, no account for the new email",
          ok2(s) and q.get("customerId") == exist_id and len(users_with(e3)) == 0 and invoices_of(q3) == 1, (s, q.get("customerId"), users_with(e3)))

    # ---- 4. staff email -> approved, no invoice, reason; Convert refused; corrected -> invoice -----------
    users_before = int(one("SELECT COUNT(*) FROM MST_User"))
    q4 = lead("STAFF", ADMIN_EMAIL)
    s, b = approve(q4)
    q = data(b)
    check("4 staff email: approval recorded (200, Approved)", ok2(s) and q.get("status") == 4, (s, b))
    check("4 staff email: no invoice and no customer link", not q.get("invoiceId") and not q.get("customerId") and invoices_of(q4) == 0, q)
    check("4 staff email: no account created", int(one("SELECT COUNT(*) FROM MST_User")) == users_before)
    reason = [n for n in notes(q) if n.startswith("No invoice raised:")]
    check("4 staff email: timeline note names the staff account and what to do",
          bool(reason) and "staff account" in reason[-1] and (ADMIN_NAME or "") in reason[-1] and "Convert to invoice" in reason[-1], reason)
    check("4 staff email: Convert to invoice is offered", (q.get("actions") or {}).get("canConvertToInvoice") is True, q.get("actions"))
    s, b = call("POST", f"/invoices/from-quotation/{q4}")
    check("4 staff email: Convert to invoice unchanged email -> 422 QUOTATION_NO_ACCOUNT with the reason",
          s == 422 and "staff account" in (b.get("message") or "") and any((x.get("code") == "QUOTATION_NO_ACCOUNT") for x in (b.get("errors") or [])), (s, b))
    s, b = call("POST", f"/quotations/{q4}/raise-invoice", {"customerEmail": "not-an-email", "customerMobile": None})
    check("4 corrected: an invalid email is refused (400) and nothing changes", s == 400 and invoices_of(q4) == 0, (s, b))
    e4, m4 = f"qa-{RUN}-fixed@example.test", free_mobile()
    s, b = call("POST", f"/quotations/{q4}/raise-invoice", {"customerEmail": e4, "customerMobile": m4})
    r = data(b)
    check("4 corrected: raise-invoice with the customer's email -> 201, account created, invoice raised",
          s == 201 and r.get("invoiceCreated") is True and r.get("customerLink") == "Created" and bool(r.get("invoiceNumber")), (s, b))
    q = get_q(q4)
    track(q)
    check("4 corrected: quotation now carries the corrected email and the link", q.get("customerEmail") == e4 and q.get("customerId") == r.get("customerId"), q.get("customerEmail"))
    check("4 corrected: timeline records the correction", any(n.startswith("Contact corrected before raising the invoice") for n in notes(q)), notes(q))
    s, b = call("POST", f"/quotations/{q4}/raise-invoice", {"customerEmail": None, "customerMobile": None})
    check("4 again: raise-invoice again -> 200 Already invoiced, same invoice", s == 200 and data(b).get("invoiceId") == r.get("invoiceId") and invoices_of(q4) == 1, (s, b))
    s, b = call("POST", f"/quotations/{q4}/raise-invoice", {"customerEmail": f"qa-{RUN}-late@example.test", "customerMobile": None})
    check("4 again: changing the email of an invoiced quotation is refused (422)", s == 422, (s, b))

    # ---- 5. no mobile -> no invoice with the reason; mobile added in Convert -> invoice ---------------
    e5 = f"qa-{RUN}-nomobile@example.test"
    q5 = lead("NOMOBILE", e5)
    s, b = approve(q5)
    q = data(b)
    reason = [n for n in notes(q) if n.startswith("No invoice raised:")]
    check("5 no mobile: approved without invoice, reason says a mobile number is needed",
          ok2(s) and not q.get("invoiceId") and bool(reason) and "mobile number" in reason[-1] and len(users_with(e5)) == 0, (s, reason))
    s, b = call("POST", f"/quotations/{q5}/raise-invoice", {"customerEmail": e5, "customerMobile": free_mobile()})
    q = get_q(q5)
    track(q)
    check("5 no mobile: raise-invoice with a mobile -> invoice and account", s == 201 and invoices_of(q5) == 1 and len(users_with(e5)) == 1, (s, b))

    # ---- 6. double approval -> one invoice, one account --------------------------------------------
    e6 = f"qa-{RUN}-twice@example.test"
    q6 = lead("TWICE", e6, free_mobile())
    answers = []
    threads = [threading.Thread(target=lambda: answers.append(approve(q6)[0])) for _ in range(2)]
    [t.start() for t in threads]
    [t.join() for t in threads]
    q = get_q(q6)
    track(q)
    check("6 concurrent approvals: one invoice, one account", invoices_of(q6) == 1 and len(users_with(e6)) == 1, (answers, invoices_of(q6), users_with(e6)))
    check("6 concurrent approvals: at least one answered 200", any(ok2(a) for a in answers), answers)
    s, b = approve(q6)
    check("6 approve again: 200 with the same invoice", ok2(s) and data(b).get("invoiceId") == q.get("invoiceId") and invoices_of(q6) == 1, (s, b))
    s, b = call("POST", f"/invoices/from-quotation/{q6}")
    check("6 convert again: 200 Already invoiced, same invoice", s == 200 and data(b).get("invoiceId") == q.get("invoiceId"), (s, b))

    # ---- 7. rejected lead quotation -> nothing -------------------------------------------------------
    e7 = f"qa-{RUN}-reject@example.test"
    q7 = lead("REJECT", e7, free_mobile())
    s, b = approve(q7, approved=False)
    check("7 rejected: no invoice, no account", ok2(s) and data(b).get("status") == 5 and invoices_of(q7) == 0 and len(users_with(e7)) == 0, (s, b))
    s, b = call("POST", f"/invoices/from-quotation/{q7}")
    s2, b2 = call("POST", f"/quotations/{q7}/raise-invoice", {})
    check("7 rejected: Convert and raise-invoice refused (422), still nothing", s == 422 and s2 == 422 and invoices_of(q7) == 0 and len(users_with(e7)) == 0, (s, s2))

    # ---- 8. quotation for a customer account: unchanged ---------------------------------------------
    if exist_id:
        q8, _ = make_quote("LINKED", None, customer_id=exist_id)
        send(q8, e2)
        events_before = int(one(f"SELECT COUNT(*) FROM TRN_AuditLog WHERE Action LIKE 'QUOTATION_CUSTOMER_%' AND EntityId='{q8}'"))
        s, b = approve(q8)
        q = data(b)
        track(q)
        check("8 customer quotation: invoice to that customer, no link note, no link audit",
              ok2(s) and invoices_of(q8) == 1 and q.get("customerId") == exist_id
              and not any("No second account" in n or "created from the quotation" in n for n in notes(q)) and events_before == 0
              and one(f"SELECT COUNT(*) FROM TRN_AuditLog WHERE Action LIKE 'QUOTATION_CUSTOMER_%' AND EntityId='{q8}'") == "0", (s, notes(q)))

    # ---- 9. advance invoice with a lead quotation: main + advance, both to the created customer -------
    e9 = f"qa-{RUN}-advance@example.test"
    q9 = lead("ADVANCE", e9, free_mobile(), advance=True)
    qno9 = one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={q9}")
    s, b = approve(q9)
    q = data(b)
    track(q)
    adv = sql(f"SELECT InvoiceId, CustomerId FROM TRN_Invoice WHERE QuotationId IS NULL AND Notes LIKE '%{qno9}%' AND IsDeleted=0")
    for a in adv:
        CREATED_INV.append(int(a[0]))
    check("9 advance: one invoice + one advance invoice, both to the created customer",
          ok2(s) and invoices_of(q9) == 1 and len(adv) == 1 and adv[0][1] == str(q.get("customerId")), (s, adv, q.get("customerId")))

    # ---- 10. Invoices search finds the quotation number / reference --------------------------------
    qs, _ = make_quote("SEARCH", f"QA Lead {RUN} SEARCH", reference=f"QA-REF-{RUN}")
    send(qs, f"qa-{RUN}-search@example.test", free_mobile())
    s, b = approve(qs)
    q = data(b)
    track(q)
    qno = q.get("quotationNumber")
    inv_no = q.get("invoiceNumber")
    tail = qno.split("/")[-1]

    def found(term):
        s, b = call("GET", "/invoices", params={"page": 1, "pageSize": 100, "search": term})
        return s, [i.get("invoiceNumber") for i in items(b)], items(b)

    s, nums, rows = found(qno)
    check("10 search: full quotation number finds its invoice", ok2(s) and inv_no in nums, (s, nums))
    check("10 search: the row carries the quotation number", any(r.get("invoiceNumber") == inv_no and r.get("quotationNumber") == qno for r in rows), rows[:1])
    s, nums, _ = found(qno.split("/", 1)[1])
    check("10 search: partial 'QTN/yyyy-yy/nnnnn' finds it", inv_no in nums, (qno.split("/", 1)[1], nums))
    s, nums, _ = found(tail)
    check("10 search: the number's last part finds it", inv_no in nums, (tail, len(nums)))
    s, nums, _ = found(f"QA-REF-{RUN}")
    check("10 search: the quotation reference number finds it", nums == [inv_no], nums)
    s, nums, _ = found(inv_no)
    check("10 search: invoice-number search unchanged", nums == [inv_no], nums)
    s, nums, _ = found(f"QA Lead {RUN} SEARCH")
    check("10 search: customer-name search unchanged", inv_no in nums, nums)
    s, nums, _ = found(f"QA-NOTHING-{RUN}")
    check("10 search: a term matching nothing finds nothing", ok2(s) and nums == [], nums)
    s, b = call("POST", f"/invoices/{q['invoiceId']}/payments", {"amount": 100.0, "paidOn": TODAY.isoformat(), "mode": 1, "reference": f"QA{RUN}",
                                                               "bankAccountId": None, "note": f"QA-E2E {RUN}"})
    check("10 setup: a manual payment on that invoice", ok2(s), (s, b))
    s, b = call("GET", "/payments/register", params={"Search": qno.split("/", 1)[1], "PageSize": 100})
    check("10 search: Payments register finds it by partial quotation number", ok2(s) and any(r.get("invoiceNumber") == inv_no for r in items(b)), (s, items(b)[:1]))
    # customer portal invoice list (own invoices) still works for the created customer
    cid = q.get("customerId")
    pw = "Qa-Portal#2026x"
    rs, _ = call("POST", f"/users/{cid}/reset-password", {"newPassword": pw})
    ctok = login(f"qa-{RUN}-search@example.test", pw, "customer") if ok2(rs) else None
    if ctok:
        TOK["cust"] = ctok
        s, b = call("GET", "/invoices/my", params={"page": 1, "pageSize": 50}, who="cust")
        check("10 portal: the customer's own invoice list shows the invoice with its quotation number",
              ok2(s) and any(i.get("invoiceNumber") == inv_no and i.get("quotationNumber") == qno for i in items(b)), (s, items(b)[:1]))
    else:
        check("10 portal: customer login after admin password reset", False, rs)
finally:
    left = []
    try:
        for iid in CREATED_INV:
            s, b = call("GET", f"/invoices/{iid}/payments")
            for p in items(b):
                pid = p.get("paymentId")
                if pid and not p.get("isReversed") and p.get("canReverse", True):
                    rs, _ = call("POST", f"/payments/{pid}/reverse", {"reason": f"QA-E2E {RUN} cleanup"})
                    if not ok2(rs):
                        left.append(f"payment {pid} not reversed ({rs})")
            cs, cb = call("POST", f"/invoices/{iid}/cancel", {"reason": f"QA-E2E {RUN} cleanup"})
            if not ok2(cs):
                left.append(f"invoice {iid} not cancelled ({cs} {(cb or {}).get('message')})")
        for qid in CREATED_Q:
            st = one(f"SELECT Status FROM TRN_Quotation WHERE QuotationId={qid}")
            if st in ("1", "2", "3"):
                call("POST", f"/quotations/{qid}/cancel", {"reason": f"QA-E2E {RUN} cleanup"})
        for r in sql(f"SELECT UserId FROM MST_User WHERE Email LIKE 'qa-{RUN}-%@example.test' AND IsDeleted=0"):
            ds, db_ = call("DELETE", f"/users/{r[0]}")
            if not ok2(ds):
                left.append(f"user {r[0]} not deleted ({ds} {(db_ or {}).get('message')})")
        for r in sql(f"SELECT QuotationId, QuotationNumber, Status FROM TRN_Quotation WHERE Subject LIKE 'QA-E2E {RUN} %' AND IsDeleted=0 AND Status NOT IN (7)"):
            left.append(f"quotation {r[0]} {r[1]} status {r[2]} (approved / rejected quotations cannot be cancelled)")
    except BaseException as e:
        left.append(f"clean-up stopped: {e}")
    print("\nTest data left in Dev (run " + RUN + "):\n  " + ("\n  ".join(left) if left else "none"))

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed, {len(results) - passed} failed")
sys.exit(0 if results and passed == len(results) else 1)
