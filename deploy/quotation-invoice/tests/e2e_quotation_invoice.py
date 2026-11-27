"""End-to-end API + SQL checks for quotation -> invoice -> payment (27 Nov 2026), LOCAL Dev stack only.

Written to the EXPECTED behaviour of the quotation/invoice brief (B1 email, B2 approve -> one invoice,
B3 reject, B4/B5/B7 payments, payment-list fields, acceptance scenarios 1-3). Run against a build that
does not have the fix, most checks fail: that is the reproduction.

  API  http://127.0.0.1:81  (D:\\Regalis\\local-dev\\api)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev
  Mail must go to the local SMTP sink on 127.0.0.1:2525; its .eml files are read from QI_MAIL_DIR.
       The script stops before creating anything if nothing answers on 2525.

Creates, for the Dev test customer (QI_CUSTOMER_EMAIL, default kausiraj21@gmail.com):
  quotations with subject "QI-E2E <run> ..." (A: Rs 10,000.00 incl. GST, R: rejected, D: duplicate-reference
  test, F: bad-address draft), the invoices they produce, manual payments with references QI<run>-*,
  one customer gateway order (Pending) on invoice A.
Removes at the end (also after a failure), through the API only: every manual payment it recorded is
reversed, its invoices are cancelled, draft quotations deleted, others cancelled where the app allows.
What the app does not let it remove is printed at the end (invoice/quotation numbers stay: numbers are
never reused, and payments are reversed, never deleted).
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD, QI_CUSTOMER_EMAIL / QI_CUSTOMER_PASSWORD, QI_MAIL_DIR.
Optional (added with the fix, 27 Nov 2026): QI_NOMAIL_API (e.g. http://localhost:82) = a second API instance over the
same database and JWT key, started with Smtp__Host and MicrosoftGraph__* blank and Payments__Razorpay__WebhookSecret set;
QI_WEBHOOK_SECRET = that secret. Used by one B1 check (send with no mail transport) and the B7 webhook checks;
skipped when unset.

usage: python -I e2e_quotation_invoice.py
"""
import datetime as dt, email, email.header, email.policy, json, os, random, socket, subprocess, sys, time, traceback, urllib.request, urllib.error
from decimal import Decimal as D, ROUND_HALF_UP

sys.stdout.reconfigure(encoding="utf-8")
API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
MAIL_DIR = os.environ.get("QI_MAIL_DIR", r"C:\Users\santh\AppData\Local\Temp\claude\4d6de0b18aaba066\e3d5794c-1d18-45a0-b861-5fc7ebb6dbc6\scratchpad\sink\mail")
CUST_EMAIL = os.environ.get("QI_CUSTOMER_EMAIL", "kausiraj21@gmail.com")
CUST_PW = os.environ.get("QI_CUSTOMER_PASSWORD")
RUN = str(random.randint(10000, 99999))
NOMAIL_API = (os.environ.get("QI_NOMAIL_API") or "").rstrip("/")
WEBHOOK_SECRET = os.environ.get("QI_WEBHOOK_SECRET") or ""
UA = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/131.0 Safari/537.36"
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
TODAY = dt.datetime.now(dt.timezone.utc).astimezone(IST).date()

# =====================================================================================================
# ONE PLACE for endpoints and field names. A fix that renames/adds a route or field is wired in here;
# the assertions below never name a route or a JSON key directly.
# =====================================================================================================
EP = {
    "quotations": "/quotations",
    "quotation": "/quotations/{id}",
    "q_send": "/quotations/{id}/send",
    "q_decision": "/quotations/{id}/record-decision",      # admin "Mark approved/rejected on behalf" (UI)
    "q_approve": "/quotations/{id}/approve",                # customer portal approve
    "q_reject": "/quotations/{id}/reject",
    "q_cancel": "/quotations/{id}/cancel",
    "q_summary": "/quotations/summary",
    "convert": "/invoices/from-quotation/{id}",             # Quotations > "Convert to invoice" (UI)
    "invoices": "/invoices",
    "invoice": "/invoices/{id}",
    "inv_cancel": "/invoices/{id}/cancel",
    "inv_payments": "/invoices/{id}/payments",              # GET list / POST record manual payment (Record payment dialog)
    "pay_register": "/payments/register",                   # Billing > Payments screen
    "pay_list": "/payments",                                # older payments list
    "pay_reverse": "/payments/{id}/reverse",
    "inv_order": "/payments/invoice-order",                 # customer pays an invoice online (gateway order -> Pending)
    "inv_verify": "/payments/invoice-verify",
    "sandbox_complete": "/payments/sandbox-complete",
    "webhook": "/payments/webhook",
    "outstanding": "/parties/customer/outstanding",
    "fin_dash": "/dashboard/finance",
}
Q_STATUS = {"Draft": 1, "Sent": 2, "Viewed": 3, "Approved": 4, "Rejected": 5, "Expired": 6, "Cancelled": 7, "Revised": 8}
DECISION_MODE_PHONE = 2
LINE_CUSTOM = 2
DISCOUNT_AMOUNT = 2
PAY_MODE_UPI = 2
EVENT_DELIVERY_FAILED = 5
# invoice status integers as the Billing screen labels them (1 Pending, 2 Paid, 3 Part paid, 4 Cancelled, 5 Refunded, 6 Sent)
INV_STATUS_TO_PAYMENT_LABEL = {1: "Due", 6: "Due", 3: "Partially Paid", 2: "Paid", 4: "Cancelled", 5: "Refunded"}
INV_CANCELLED = 4

# Payment list columns the brief requires -> acceptable JSON keys (first present wins)
PAY_FIELDS = {
    "Payment ID": ["paymentNumber", "receiptNumber", "paymentId"],
    "Customer Name": ["customerName"],
    "Quotation Number": ["quotationNumber"],
    "Invoice Number": ["invoiceNumber"],
    "Payment Date": ["paymentDate", "paidOn", "paidUtc"],
    "Payment Method": ["paymentMethodName", "modeName", "paymentMethod"],
    "Total Invoice Amount": ["totalInvoiceAmount", "invoiceTotal", "invoiceAmount", "invoiceTotalAmount"],
    "Amount Paid": ["amountPaid", "amount"],
    "Outstanding": ["outstandingAmount", "outstanding", "invoiceOutstanding", "balance"],
    "Payment Status": ["paymentStatus", "status"],
}


def ep(name, **kw):
    return EP[name].format(**kw)


def f(obj, *keys, default=None):
    """First present key of a DTO."""
    for k in keys:
        if isinstance(obj, dict) and k in obj and obj[k] is not None:
            return obj[k]
    return default


def q_invoice_id(q):      return f(q, "invoiceId")
def q_status(q):          return f(q, "status")
def q_events(q):          return f(q, "events", default=[]) or []
def q_number(q):          return f(q, "quotationNumber")
def inv_paid(i):          return dec(f(i, "paidAmount", "amountPaid", default=0))
def inv_out(i):           return dec(f(i, "outstandingAmount", "outstanding", default=-1))
def inv_total(i):         return dec(f(i, "totalAmount", "invoiceTotal", default=-1))
def inv_lines(i):         return f(i, "lines", default=[]) or []


def inv_pay_label(i):
    """The invoice's payment status as a label: a string field if the API has one, else the status code."""
    s = f(i, "paymentStatus", "paymentStatusName")
    if isinstance(s, str):
        return {"partiallypaid": "Partially Paid", "partpaid": "Partially Paid"}.get(s.replace(" ", "").lower(), s.strip().title())
    return INV_STATUS_TO_PAYMENT_LABEL.get(f(i, "status"))


def pay_field(row, label):
    """(key, value) of a required payment-list column, or (None, None) when the row has none of its keys."""
    for k in PAY_FIELDS[label]:
        if isinstance(row, dict) and k in row:
            return k, row[k]
    return None, None


def items(b):
    d = (b or {}).get("data")
    return d.get("items", []) if isinstance(d, dict) else (d or [])


# =====================================================================================================
results, groups = [], {}


def check(name, ok, detail="", group=None):
    results.append((name, bool(ok)))
    for g in ([group] if isinstance(group, str) else (group or [])):
        groups.setdefault(g, []).append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {str(detail)[:400]}" if detail and not ok else ""), flush=True)


skips = []


def skip(name, reason):
    skips.append(name)
    print(f"SKIP {name}  -- {reason}", flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


def r2(x):
    return D(str(x)).quantize(D("0.01"), rounding=ROUND_HALF_UP)


def dec(v):
    try:
        return D(str(v)).quantize(D("0.01"))
    except Exception:
        return D("-999999")


CREDS = {}


def _call(method, path, token, body):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    req.add_header("User-Agent", UA)
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


def call(method, path, who, body=None, params=None):
    if params:
        path += "?" + "&".join(f"{k}={urllib.request.quote(str(v))}" for k, v in params.items() if v is not None)
    for attempt in range(10):
        s, b = _call(method, path, CREDS[who]["tok"] if who else None, body)
        if s == 401 and who:
            CREDS[who]["tok"] = raw_login(*CREDS[who]["login"])
            continue
        if s == 429:
            time.sleep(10 + 5 * attempt)
            continue
        return s, b
    return s, b


ALT_TOK = {}


def alt_login(base, em, pw, portal):
    for attempt in range(8):
        req = urllib.request.Request(base + "/api/auth/login", data=json.dumps({"email": em, "password": pw, "portal": portal}).encode(), method="POST")
        req.add_header("Content-Type", "application/json")
        req.add_header("User-Agent", UA)
        try:
            with urllib.request.urlopen(req, timeout=90) as r:
                return json.loads(r.read())["data"]["accessToken"]
        except urllib.error.HTTPError as e:
            if e.code == 429:
                time.sleep(15 + 5 * attempt)
                continue
            raise SystemExit(f"login {em} ({portal}) on {base} failed: {e.code}")
    raise SystemExit("login rate limited on " + base)


def alt_call(base, method, path, who=None, body=None, raw=None, headers=None):
    """One call to another API instance (QI_NOMAIL_API) over the same database; raw = exact body bytes."""
    data = raw if raw is not None else (json.dumps(body).encode() if body is not None else None)
    for attempt in range(6):
        req = urllib.request.Request(base + "/api" + path, data=data, method=method)
        req.add_header("Content-Type", "application/json")
        req.add_header("User-Agent", UA)
        for k, v in (headers or {}).items():
            req.add_header(k, v)
        if who:
            # A token is bound to the instance that issued it ("The server restarted since this session began"),
            # so sign in on the other instance itself rather than reusing the :81 token.
            if (base, who) not in ALT_TOK:
                ALT_TOK[(base, who)] = alt_login(base, *CREDS[who]["login"])
            req.add_header("Authorization", "Bearer " + ALT_TOK[(base, who)])
        try:
            with urllib.request.urlopen(req, timeout=90) as r:
                return r.status, json.loads(r.read() or b"{}")
        except urllib.error.HTTPError as e:
            if e.code == 401 and who and attempt < 5:
                ALT_TOK.pop((base, who), None)
                continue
            if e.code == 429:
                time.sleep(10 + 5 * attempt)
                continue
            try:
                return e.code, json.loads(e.read() or b"{}")
            except Exception:
                return e.code, {}
        except OSError as e:
            return 0, {"message": str(e)}
    return 429, {}


def raw_login(em, pw, portal):
    for attempt in range(8):
        s, b = _call("POST", "/auth/login", None, {"email": em, "password": pw, "portal": portal})
        if s == 200:
            return b["data"]["accessToken"]
        if s == 429:
            time.sleep(15 + 5 * attempt)
            continue
        raise SystemExit(f"login {em} ({portal}) failed: {s} {b}")
    raise SystemExit("login rate limited")


def session(name, em, pw, portal):
    CREDS[name] = {"login": (em, pw, portal), "tok": raw_login(em, pw, portal)}
    return name


def ok2(s):
    return 200 <= s < 300


def refused(s):
    return 400 <= s < 500


# ---- mail sink ---------------------------------------------------------------------------------------
def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so:
            return so.recv(100).startswith(b"220")
    except OSError:
        return False


def mail_files():
    return set(os.listdir(MAIL_DIR)) if os.path.isdir(MAIL_DIR) else set()


def read_mail(fn):
    raw = open(os.path.join(MAIL_DIR, fn), "rb").read()
    rcpts, rest = [], raw
    while rest.startswith(b"X-Sink-Rcpt:"):
        line, _, rest = rest.partition(b"\n")
        rcpts += [x.strip().strip("<>").lower() for x in line[len(b"X-Sink-Rcpt:"):].decode(errors="replace").split(",") if x.strip()]
    msg = email.message_from_bytes(rest, policy=email.policy.default)
    subject = str(msg.get("Subject", ""))
    body, pdfs = "", []
    for part in msg.walk():
        ct = part.get_content_type()
        if part.is_multipart():
            continue
        payload = part.get_payload(decode=True) or b""
        fname = part.get_filename()
        if ct == "application/pdf" or (fname and fname.lower().endswith(".pdf")):
            pdfs.append((ct, fname, payload[:5]))
        elif ct.startswith("text/"):
            body += payload.decode(part.get_content_charset() or "utf-8", errors="replace")
    return {"file": fn, "rcpt": rcpts, "subject": subject, "body": body, "pdfs": pdfs}


def wait_new_mail(before, seconds=25):
    end = time.time() + seconds
    new = set()
    while time.time() < end:
        new = mail_files() - before
        if new:
            time.sleep(2)  # let a second message (if any) land too
            return sorted(mail_files() - before)
        time.sleep(0.5)
    return sorted(new)


# ---- fixtures ----------------------------------------------------------------------------------------
CREATED_Q, CREATED_INV, CREATED_PAYMENTS = [], [], []


def make_quote(tag, lines, discount=None):
    body = {"customerId": CUST_ID, "locationId": 2, "quotationDate": TODAY.isoformat(), "validUntil": (TODAY + dt.timedelta(days=15)).isoformat(),
            "subject": f"QI-E2E {RUN} {tag}", "placeOfSupplyStateCode": "29",
            "discountType": DISCOUNT_AMOUNT if discount else None, "discountValue": float(discount) if discount else None,
            "lines": [{"lineType": LINE_CUSTOM, "serviceType": 5, "description": f"QI-E2E {RUN} {d}", "quantity": float(q), "unitPrice": float(p),
                       "discountAmount": 0, "taxPercent": 18, "isNonTaxable": False} for d, q, p in lines]}
    s, b = call("POST", ep("quotations"), "admin", body)
    if not ok2(s):
        raise SystemExit(f"create quotation {tag}: {s} {json.dumps(b)[:600]}")
    qid = b["data"]["quotationId"]
    CREATED_Q.append(qid)
    return qid, b["data"]


def get_q(qid):
    s, b = call("GET", ep("quotation", id=qid), "admin")
    return b.get("data") or {}


def get_inv(iid):
    s, b = call("GET", ep("invoice", id=iid), "admin")
    return b.get("data") or {}


def send_email(qid, to):
    return call("POST", ep("q_send", id=qid), "admin", {"sendEmail": True, "sendWhatsApp": False, "emailTo": to, "emailCc": None, "bccMe": False,
                                                        "emailSubject": None, "emailMessage": None, "whatsAppTo": None, "whatsAppMessage": None})


def decide(qid, approved, who="admin"):
    if who == "admin":
        return call("POST", ep("q_decision", id=qid), "admin", {"approved": approved, "mode": DECISION_MODE_PHONE, "confirmedBy": CUST_NAME,
                                                                "note": f"QI-E2E {RUN}", "reasonCode": None if approved else "PRICE"})
    return call("POST", ep("q_approve" if approved else "q_reject", id=qid), "cust", {"reasonCode": None, "reason": None if approved else "QI-E2E", "acceptedTerms": True})


def pay(iid, amount, ref, mode=PAY_MODE_UPI):
    s, b = call("POST", ep("inv_payments", id=iid), "admin", {"amount": float(amount), "paidOn": TODAY.isoformat(), "mode": mode, "reference": ref,
                                                              "bankAccountId": None, "note": f"QI-E2E {RUN}"})
    return s, b


def sql_invoices_for(qid):
    return int(one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={qid} AND IsDeleted=0") or 0)


def register_rows(search):
    s, b = call("GET", ep("pay_register"), "admin", params={"Search": search, "PageSize": 100})
    return s, items(b)


def ensure_invoice(qid, group):
    """The invoice of an approved quotation: the one approval made, else the one 'Convert to invoice' makes
    (so the payment checks can still run when approval did not raise one)."""
    q = get_q(qid)
    iid = q_invoice_id(q)
    if iid:
        return iid
    s, b = call("POST", ep("convert", id=qid), "admin")
    iid = (b.get("data") or {}).get("invoiceId") if ok2(s) else None
    print(f"INFO approval raised no invoice for quotation {qid}; 'Convert to invoice' -> {s} invoice {iid}", flush=True)
    return iid


# =====================================================================================================
if not CUST_PW:
    raise SystemExit("Set QI_CUSTOMER_PASSWORD (the Dev test customer's password).")
if not sink_up():
    raise SystemExit("No SMTP sink on 127.0.0.1:2525 - not running (sending a quotation would email for real).")
if not os.path.isdir(MAIL_DIR):
    raise SystemExit(f"Mail sink directory not found: {MAIL_DIR}")
session("admin", os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"], "admin")
session("cust", CUST_EMAIL, CUST_PW, "customer")
row = sql(f"SELECT UserId, Email, FullName FROM MST_User WHERE Email='{CUST_EMAIL}' AND IsDeleted=0")
if not row:
    raise SystemExit("test customer not found")
CUST_ID, REG_EMAIL, CUST_NAME = int(row[0][0]), row[0][1].strip().lower(), row[0][2]
print(f"run {RUN}  customer {CUST_ID}  registered email {REG_EMAIL}  today(IST) {TODAY}", flush=True)

# Hand-worked figures for quotation A: 2 x 4,000.00 + 1 x 1,474.58 = 9,474.58; discount 1,000.00 (amount)
# -> taxable 8,474.58; CGST 9% = 762.7122 -> 762.71; SGST 762.71; IGST 0 (place of supply 29 = home state);
# total 8,474.58 + 1,525.42 = 10,000.00, round-off 0.00.
A_SUB, A_DISC = D("9474.58"), D("1000.00")
A_TAX = A_SUB - A_DISC
A_CGST = r2(A_TAX * 9 / 100)
A_SGST = r2(A_TAX * 9 / 100)
A_TOTAL = A_TAX + A_CGST + A_SGST
assert A_TOTAL == D("10000.00"), A_TOTAL

try:
    # ---- quotation A: totals ---------------------------------------------------------------------------
    QA, qa = make_quote("A", [("Cabin month", 2, "4000.00"), ("Setup", 1, "1474.58")], discount=A_DISC)
    QA_NO = q_number(qa)
    check("A: quotation totals worked by hand (sub 9474.58, disc 1000, taxable 8474.58, CGST/SGST 762.71, IGST 0, total 10000.00)",
          (dec(qa.get("subTotal")), dec(qa.get("discountAmount")), dec(qa.get("taxableAmount")), dec(qa.get("cgstAmount")), dec(qa.get("sgstAmount")),
           dec(qa.get("igstAmount")), dec(qa.get("totalAmount"))) == (A_SUB, A_DISC, A_TAX, A_CGST, A_SGST, D("0.00"), A_TOTAL),
          {k: qa.get(k) for k in ("subTotal", "discountAmount", "taxableAmount", "cgstAmount", "sgstAmount", "igstAmount", "roundOffAmount", "totalAmount")})

    # ---- B1 email ---------------------------------------------------------------------------------------
    before = mail_files()
    s, b = send_email(QA, qa.get("customerEmail") or REG_EMAIL)   # what the Send popup posts: To pre-filled from the quotation
    check("B1 send: API accepts the send (2xx)", ok2(s), (s, b), group=["S1", "S3"])
    q = get_q(QA)
    check("B1 send: quotation status is Sent", q_status(q) == Q_STATUS["Sent"], q_status(q), group=["S1", "S3"])
    ev = [e for e in q_events(q) if (e.get("channel") or "").lower() == "email"]
    check("B1 send: an Email event is logged and not marked failed", bool(ev) and ev[-1].get("deliveryStatus") != EVENT_DELIVERY_FAILED, ev[-1:] or q_events(q)[-3:])
    check("B1 send: the Email event does not say the mail was not sent",
          bool(ev) and "not sent" not in (ev[-1].get("summary") or "").lower() and "no mail server" not in (ev[-1].get("summary") or "").lower(), ev[-1:])
    new = wait_new_mail(before)
    # other jobs (reminders, receipts) write to the same sink: only mails that name this quotation count
    mails = [m for m in (read_mail(fn) for fn in new) if QA_NO and (QA_NO in m["subject"] or QA_NO in m["body"])]
    to_cust = [m for m in mails if REG_EMAIL in m["rcpt"]]
    check("B1 mail: exactly one new mail for this quotation in the sink", len(mails) == 1,
          [(m["file"], m["rcpt"], m["subject"]) for m in mails] or f"no new .eml naming {QA_NO} in {MAIL_DIR} ({len(new)} other new)", group=["S1", "S3"])
    check("B1 mail: addressed to the customer's registered email (MST_User.Email)", len(to_cust) == 1, [m["rcpt"] for m in mails] or "no mail", group=["S1", "S3"])
    m = to_cust[0] if to_cust else (mails[0] if mails else None)
    check("B1 mail: a PDF attachment (application/pdf, starts with %PDF)", bool(m) and any(ct == "application/pdf" and head.startswith(b"%PDF") for ct, _, head in m["pdfs"]),
          m and m["pdfs"] or "no mail")
    check("B1 mail: the quotation number is in the subject or body", bool(m) and bool(QA_NO) and (QA_NO in m["subject"] or QA_NO in m["body"]),
          m and (m["subject"], m["body"][:200]) or "no mail")

    # B1 failure path: a bad recipient must not be reported as sent
    QF, _ = make_quote("F-badaddress", [("Bad address", 1, "100.00")])
    before = mail_files()
    s, b = send_email(QF, "not-an-email")
    qf = get_q(QF)
    fev = [e for e in q_events(qf) if (e.get("channel") or "").lower() == "email"]
    check("B1 failure: send to an invalid address is not reported as success (4xx)", refused(s), (s, (b or {}).get("message")))
    check("B1 failure: after an invalid address the quotation is still Draft and no delivered Email event",
          q_status(qf) == Q_STATUS["Draft"] and not [e for e in fev if e.get("deliveryStatus") != EVENT_DELIVERY_FAILED], (q_status(qf), fev))
    check("B1 failure: nothing reached the sink for the invalid address", not (mail_files() - before), sorted(mail_files() - before))
    skip("B1 failure: unreachable mail transport reports a failure",
         "cannot be provoked from the API without changing the stack's mail config (the sink accepts every RCPT); see the next check for the code path")
    bad = sql("SELECT QuotationEventId, DeliveryStatus FROM TRN_QuotationEvent WHERE Channel='Email' AND (Summary LIKE '%not sent%' OR Summary LIKE '%No mail server%')"
              f" AND ISNULL(DeliveryStatus,0) <> {EVENT_DELIVERY_FAILED}")
    check("B1 failure (code path, Dev history): no Email event that says 'not sent' carries a non-failed delivery status", not bad, f"events {bad} (id|DeliveryStatus)")

    # B1 failure path with no mail transport at all (added with the fix): a second API instance with Smtp__Host and
    # MicrosoftGraph__* blank must refuse the send (4xx with the reason), log a Failed Email event and leave the status.
    if NOMAIL_API:
        QN, qn = make_quote("N-nomail", [("No mail transport", 1, "100.00")])
        s, b = alt_call(NOMAIL_API, "POST", ep("q_send", id=QN), "admin", {"sendEmail": True, "sendWhatsApp": False, "emailTo": qn.get("customerEmail") or REG_EMAIL,
                        "emailCc": None, "bccMe": False, "emailSubject": None, "emailMessage": None, "whatsAppTo": None, "whatsAppMessage": None})
        qn2 = get_q(QN)
        nev = [e for e in q_events(qn2) if (e.get("channel") or "").lower() == "email"]
        check(f"B1 no mail transport ({NOMAIL_API}): send refused (4xx) with the reason, a Failed Email event, status still Draft",
              refused(s) and "mail" in ((b or {}).get("message") or "").lower() and bool(nev) and all(e.get("deliveryStatus") == EVENT_DELIVERY_FAILED for e in nev)
              and q_status(qn2) == Q_STATUS["Draft"], (s, (b or {}).get("message"), q_status(qn2), [(e.get("deliveryStatus"), e.get("summary")) for e in nev]))
    else:
        skip("B1 no mail transport", "QI_NOMAIL_API not set (needs a second API instance with Smtp__Host and MicrosoftGraph__* blank)")

    # ---- B2 approve -> exactly one invoice -------------------------------------------------------------
    s, b = decide(QA, True)
    check("B2 approve (admin record-decision): accepted (2xx)", ok2(s), (s, b), group=["S1", "S3"])
    q = get_q(QA)
    check("B2 approve: quotation status Approved", q_status(q) == Q_STATUS["Approved"], q_status(q), group=["S1", "S3"])
    inv_on_approve = q_invoice_id(q)
    check("B2 approve: an invoice is linked to the quotation (InvoiceId set)", bool(inv_on_approve), f"InvoiceId {inv_on_approve}", group=["S1", "S3"])
    check("B2 approve: exactly one invoice in SQL for the quotation", sql_invoices_for(QA) == 1, f"{sql_invoices_for(QA)} invoices with QuotationId={QA}", group=["S1", "S3"])
    INV_A = ensure_invoice(QA, "B2")
    if INV_A:
        CREATED_INV.append(INV_A)
    # approve again / convert again: same invoice, still one
    s2, b2 = decide(QA, True)
    q2 = get_q(QA)
    check("B2 approve twice: still exactly one invoice (SQL) and the same InvoiceId",
          sql_invoices_for(QA) == 1 and q_invoice_id(q2) == INV_A and INV_A is not None, (s2, (b2 or {}).get("message"), q_invoice_id(q2), INV_A, sql_invoices_for(QA)), group="S3")
    s3, b3 = call("POST", ep("convert", id=QA), "admin")
    check("B2 convert again: returns the same invoice (2xx, same invoiceId)", ok2(s3) and (b3.get("data") or {}).get("invoiceId") == INV_A,
          (s3, (b3 or {}).get("message"), (b3.get("data") or {}).get("invoiceId"), INV_A), group="S3")
    check("B2 convert again: invoice count for the quotation still 1 (SQL)", sql_invoices_for(QA) == 1, sql_invoices_for(QA), group="S3")
    hist = {(r[0], r[1]) for r in sql(f"SELECT ISNULL(FromStatus,0), ToStatus FROM TRN_QuotationStatusHistory WHERE QuotationId={QA}")}
    check("B2 status history kept: Draft->Sent and Sent->Approved rows", {("1", "2"), ("2", "4")} <= hist, sorted(hist), group=["S1", "S3"])

    inv = get_inv(INV_A) if INV_A else {}
    check("B2 invoice: issued (has a number, not cancelled)", bool(inv.get("invoiceNumber")) and inv.get("status") not in (None, INV_CANCELLED), (inv.get("invoiceNumber"), inv.get("status")), group="S1")
    check("B2 invoice: payment status Due, paid 0.00, outstanding 10000.00", inv_pay_label(inv) == "Due" and inv_paid(inv) == 0 and inv_out(inv) == A_TOTAL,
          (inv_pay_label(inv), inv.get("status"), inv.get("paidAmount"), inv.get("outstandingAmount")), group="S1")
    check("B2 invoice copies customer", inv.get("customerId") == CUST_ID, inv.get("customerId"), group="S1")
    MONEY = ("subTotal", "discountAmount", "taxableAmount", "cgstAmount", "sgstAmount", "igstAmount", "totalAmount")
    qa_now = get_q(QA)
    check("B2 invoice copies subtotal, discount, taxable, CGST, SGST, IGST and total from the quotation",
          bool(inv) and all(dec(inv.get(k)) == dec(qa_now.get(k)) for k in MONEY),
          {k: (qa_now.get(k), inv.get(k)) for k in MONEY if dec(inv.get(k)) != dec(qa_now.get(k))}, group="S1")
    check("B2 invoice GST worked by hand: discount 1000.00, CGST 762.71, SGST 762.71, IGST 0.00, total 10000.00",
          (dec(inv.get("discountAmount")), dec(inv.get("cgstAmount")), dec(inv.get("sgstAmount")), dec(inv.get("igstAmount")), inv_total(inv)) == (A_DISC, A_CGST, A_SGST, D("0.00"), A_TOTAL),
          {k: inv.get(k) for k in ("discountAmount", "cgstAmount", "sgstAmount", "igstAmount", "totalAmount")}, group="S1")
    ql = [(dec(l["quantity"]), dec(l["unitPrice"])) for l in (get_q(QA).get("lines") or [])]
    il = [(dec(l.get("quantity")), dec(l.get("unitPrice"))) for l in inv_lines(inv)]
    check("B2 invoice copies lines (qty, unit price)", il == ql and len(il) == 2, (ql, il), group="S1")
    srow = sql(f"SELECT QuotationId, CustomerId, TotalAmount, CgstAmount, SgstAmount, IgstAmount, DiscountAmount, Status FROM TRN_Invoice WHERE InvoiceId={INV_A or 0}")
    check("B2 invoice row in SQL matches the quotation (quotation link, customer, total, CGST, SGST, IGST, discount)",
          bool(srow) and srow[0][0] == str(QA) and srow[0][1] == str(CUST_ID)
          and tuple(dec(x) for x in srow[0][2:7]) == tuple(dec(qa_now.get(k)) for k in ("totalAmount", "cgstAmount", "sgstAmount", "igstAmount", "discountAmount")), srow)

    # ---- B3 reject ------------------------------------------------------------------------------------
    QR, qr = make_quote("R-reject", [("Meeting room day", 1, "2000.00")])
    QR_NO = q_number(qr)
    s, b = send_email(QR, qr.get("customerEmail") or REG_EMAIL)
    check("B3 rejected quotation sent first (2xx)", ok2(s), (s, b), group="S2")
    summ0 = (call("GET", ep("q_summary"), "admin")[1].get("data") or {})
    out0 = {r["partyId"]: dec(r.get("balance")) for r in items(call("GET", ep("outstanding"), "admin")[1])}
    s, b = decide(QR, False)
    check("B3 reject (admin record-decision): accepted (2xx)", ok2(s), (s, b), group="S2")
    q = get_q(QR)
    check("B3 rejected: status Rejected and no InvoiceId", q_status(q) == Q_STATUS["Rejected"] and not q_invoice_id(q), (q_status(q), q_invoice_id(q)), group="S2")
    check("B3 rejected: no invoice in SQL", sql_invoices_for(QR) == 0, sql_invoices_for(QR), group="S2")
    s, b = call("POST", ep("convert", id=QR), "admin")
    check("B3 rejected: convert to invoice refused (4xx)", refused(s), (s, (b or {}).get("message")), group="S2")
    s, b = decide(QR, True)
    check("B3 rejected: admin approve afterwards refused (4xx)", refused(s), (s, (b or {}).get("message")), group="S2")
    s, b = decide(QR, True, who="cust")
    check("B3 rejected: customer approve afterwards refused (4xx)", refused(s), (s, (b or {}).get("message")), group="S2")
    check("B3 rejected: still no invoice after the refused attempts (SQL)", sql_invoices_for(QR) == 0 and not q_invoice_id(get_q(QR)), sql_invoices_for(QR), group="S2")
    npay = one(f"SELECT (SELECT COUNT(*) FROM TRN_Payment p JOIN TRN_Invoice i ON i.InvoiceId=p.InvoiceId WHERE i.QuotationId={QR})"
               f" + (SELECT COUNT(*) FROM TRN_InvoicePayment p JOIN TRN_Invoice i ON i.InvoiceId=p.InvoiceId WHERE i.QuotationId={QR})")
    check("B3 rejected: no payment rows (SQL)", npay == "0", npay, group="S2")
    s, rows = register_rows(QR_NO)
    hit = [r for r in rows if QR_NO in json.dumps(r)]
    check("B3 rejected: absent from the payment list (register search by quotation number)", s == 200 and not hit, (s, hit[:2]), group="S2")
    summ1 = (call("GET", ep("q_summary"), "admin")[1].get("data") or {})
    check("B3 rejected: not counted in the quotation summary's approved value", dec(summ1.get("approvedValue")) == dec(summ0.get("approvedValue")),
          (summ0.get("approvedValue"), summ1.get("approvedValue")), group="S2")
    out1 = {r["partyId"]: dec(r.get("balance")) for r in items(call("GET", ep("outstanding"), "admin")[1])}
    check("B3 rejected: customer outstanding (receivables report) unchanged by the rejection", out1.get(CUST_ID) == out0.get(CUST_ID), (out0.get(CUST_ID), out1.get(CUST_ID)), group="S2")

    # ---- B4/B5/B7 payments on invoice A ----------------------------------------------------------------
    if not INV_A:
        check("B4 payments: an invoice exists for the approved quotation to pay against", False, "no invoice from approve nor from convert", group=["S1", "S3"])
    else:
        # pending / failed never change Paid
        s, b = call("POST", ep("inv_order"), "cust", {"invoiceId": INV_A, "amount": 1000})
        order = (b.get("data") or {}) if ok2(s) else {}
        oid = f(order, "gatewayOrderId", "orderId")
        print(f"INFO customer gateway order on invoice A -> {s} order {oid}", flush=True)
        inv = get_inv(INV_A)
        check("B7 pending payment: an open gateway order does not change Paid/Outstanding", inv_paid(inv) == 0 and inv_out(inv) == A_TOTAL,
              (s, inv.get("paidAmount"), inv.get("outstandingAmount")))
        if oid:
            s, b = call("POST", ep("inv_verify"), "cust", {"gatewayOrderId": oid, "gatewayPaymentId": f"pay_QI{RUN}", "gatewaySignature": "bad-signature"})
            inv = get_inv(INV_A)
            check("B7 failed payment: a bad gateway signature is refused (4xx)", refused(s), (s, (b or {}).get("message")))
            check("B7 failed payment: Paid/Outstanding unchanged", inv_paid(inv) == 0 and inv_out(inv) == A_TOTAL, (inv.get("paidAmount"), inv.get("outstandingAmount")))
        else:
            skip("B7 failed payment", f"no gateway order could be opened on the invoice ({s} {(b or {}).get('message')})")

        for amt, label in ((0, "zero"), (-1, "negative")):
            s, b = pay(INV_A, amt, f"QI{RUN}-X{label}")
            check(f"B4 boundary: a {label} payment amount is refused (4xx)", refused(s), (s, (b or {}).get("message")))

        s, b = pay(INV_A, D("4000.00"), f"QI{RUN}-UTR1")
        check("B4 manual payment 4,000.00 recorded (2xx)", ok2(s), (s, b), group="S3")
        if ok2(s):
            CREATED_PAYMENTS.append(INV_A)
        inv = get_inv(INV_A)
        check("B4 after 4,000: invoice Paid 4000.00, Outstanding 6000.00", inv_paid(inv) == D("4000.00") and inv_out(inv) == D("6000.00"),
              (inv.get("paidAmount"), inv.get("outstandingAmount")), group="S3")
        check("B4 after 4,000: invoice payment status Partially Paid", inv_pay_label(inv) == "Partially Paid", (inv_pay_label(inv), inv.get("status")), group="S3")
        srow = sql(f"SELECT PaidAmount, OutstandingAmount, Status FROM TRN_Invoice WHERE InvoiceId={INV_A}")
        check("B4 after 4,000: SQL PaidAmount 4000.00 / OutstandingAmount 6000.00", tuple(dec(x) for x in srow[0][:2]) == (D("4000.00"), D("6000.00")), srow, group="S3")

        s, rows = register_rows(inv.get("invoiceNumber") or "")
        mine = [r for r in rows if r.get("invoiceId") == INV_A and dec(f(r, "amount", "amountPaid")) == D("4000.00")]
        check("B5 payment list shows the 4,000 payment against invoice A", s == 200 and len(mine) == 1, (s, len(rows), [(r.get("invoiceId"), r.get("amount")) for r in rows][:5]), group="S3")
        prow = mine[0] if mine else (rows[0] if rows else {})
        missing = [lbl for lbl in PAY_FIELDS if pay_field(prow, lbl)[0] is None]
        check("B5 payment list row exposes Payment ID, Customer Name, Quotation Number, Invoice Number, Payment Date, Payment Method, "
              "Total Invoice Amount, Amount Paid, Outstanding, Payment Status", prow and not missing, f"missing: {missing}; keys: {sorted(prow)}", group="S3")
        for lbl, expect in (("Customer Name", CUST_NAME), ("Quotation Number", QA_NO), ("Invoice Number", inv.get("invoiceNumber"))):
            k, v = pay_field(prow, lbl)
            check(f"B5 payment list: {lbl} = {expect}", k is not None and str(v or "").strip() == str(expect), (k, v))
        for lbl, expect in (("Total Invoice Amount", A_TOTAL), ("Amount Paid", D("4000.00")), ("Outstanding", D("6000.00"))):
            k, v = pay_field(prow, lbl)
            check(f"B5 payment list: {lbl} = {expect}", k is not None and dec(v) == expect, (k, v), group="S3")
        s, b = call("GET", ep("pay_list"), "admin", params={"CustomerId": CUST_ID, "PageSize": 100})
        lrow = next((r for r in items(b) if r.get("invoiceId") == INV_A), {})
        missing = [lbl for lbl in PAY_FIELDS if pay_field(lrow, lbl)[0] is None]
        check("B5 /payments list row (older endpoint) exposes the same ten columns", lrow and not missing, f"row found: {bool(lrow)}; missing: {missing}")

        s, b = pay(INV_A, D("6000.01"), f"QI{RUN}-OVER")
        inv = get_inv(INV_A)
        check("B4 overpayment: 6,000.01 on 6,000.00 outstanding refused (4xx)", refused(s), (s, (b or {}).get("message")), group="S3")
        check("B4 overpayment: Paid still 4000.00", inv_paid(inv) == D("4000.00"), inv.get("paidAmount"), group="S3")

        s, b = pay(INV_A, D("6000.00"), f"QI{RUN}-UTR2")
        check("B4 manual payment 6,000.00 (exactly the outstanding) recorded (2xx)", ok2(s), (s, b), group="S1")
        inv = get_inv(INV_A)
        check("B4 after 6,000: Paid 10000.00, Outstanding 0.00", inv_paid(inv) == A_TOTAL and inv_out(inv) == 0, (inv.get("paidAmount"), inv.get("outstandingAmount")), group="S1")
        check("B4 after 6,000: invoice payment status Paid", inv_pay_label(inv) == "Paid", (inv_pay_label(inv), inv.get("status")), group="S1")
        s, b = pay(INV_A, D("1.00"), f"QI{RUN}-AFTER")
        check("B4 overpayment: any amount on a fully paid invoice refused (4xx)", refused(s), (s, (b or {}).get("message")), group="S1")
        tot = one(f"SELECT SUM(Amount) FROM TRN_InvoicePayment WHERE InvoiceId={INV_A} AND IsReversed=0 AND IsDeleted=0")
        check("B4 SQL: unreversed manual payments on invoice A sum to 10000.00", dec(tot) == A_TOTAL, tot, group="S1")

    # ---- B4 duplicate receipt reference (invoice D) ------------------------------------------------------
    QD, qd = make_quote("D-dupref", [("Day pass pack", 1, "2000.00")])
    send_email(QD, qd.get("customerEmail") or REG_EMAIL)
    s, b = decide(QD, True, who="cust")
    check("B2 customer approve (portal): accepted (2xx)", ok2(s), (s, (b or {}).get("message")))
    qd = get_q(QD)
    check("B2 customer approve: an invoice is linked (InvoiceId set), exactly one in SQL", bool(q_invoice_id(qd)) and sql_invoices_for(QD) == 1,
          (q_invoice_id(qd), sql_invoices_for(QD)))
    INV_D = ensure_invoice(QD, "B4dup")
    if INV_D:
        CREATED_INV.append(INV_D)
        ref = f"QI{RUN}-DUP"
        s1, b1 = pay(INV_D, D("500.00"), ref)
        s2, b2 = pay(INV_D, D("500.00"), ref)
        n = one(f"SELECT COUNT(*) FROM TRN_InvoicePayment WHERE InvoiceId={INV_D} AND Reference='{ref}' AND IsReversed=0 AND IsDeleted=0")
        inv = get_inv(INV_D)
        check("B4 duplicate reference: first 500.00 recorded (2xx)", ok2(s1), (s1, b1))
        check("B4 duplicate reference: same reference again refused (4xx) or returned idempotently", refused(s2) or (ok2(s2) and n == "1"), (s2, (b2 or {}).get("message")))
        check("B4 duplicate reference: one payment row, Paid 500.00", n == "1" and inv_paid(inv) == D("500.00"), (n, inv.get("paidAmount")))

        # ---- B7 gateway webhook on an invoice payment (added with the fix) -----------------------------------
        if NOMAIL_API and WEBHOOK_SECRET:
            import hashlib, hmac
            s, b = call("POST", ep("inv_order"), "cust", {"invoiceId": INV_D, "amount": 100})
            woid = f((b.get("data") or {}) if ok2(s) else {}, "gatewayOrderId", "orderId")
            s, b = call("POST", ep("sandbox_complete"), "cust", {"gatewayOrderId": woid}) if woid else (0, {})
            wpid, wsig = ((b.get("data") or {}).get("gatewayPaymentId"), (b.get("data") or {}).get("gatewaySignature")) if ok2(s) else (None, None)
            if woid and wpid:
                raw = json.dumps({"event": "payment.captured", "payload": {"payment": {"entity": {"id": wpid, "order_id": woid, "status": "captured", "amount": 10000}}}}).encode()
                sig = hmac.new(WEBHOOK_SECRET.encode(), raw, hashlib.sha256).hexdigest()
                s, b = alt_call(NOMAIL_API, "POST", ep("webhook"), None, raw=raw, headers={"X-Razorpay-Signature": sig})
                srow = sql(f"SELECT Status FROM TRN_Payment WHERE GatewayOrderId='{woid}'")
                inv = get_inv(INV_D)
                check("B7 webhook: a captured invoice payment (valid signature) is Success and moves the invoice: Paid 600.00, Partially Paid",
                      ok2(s) and bool(srow) and srow[0][0] == "2" and inv_paid(inv) == D("600.00") and inv_pay_label(inv) == "Partially Paid",
                      (s, (b or {}).get("message"), srow, inv.get("paidAmount"), inv.get("status")))
                s, b = call("POST", ep("inv_verify"), "cust", {"gatewayOrderId": woid, "gatewayPaymentId": wpid, "gatewaySignature": wsig})
                inv = get_inv(INV_D)
                srow = sql(f"SELECT PaidAmount FROM TRN_Invoice WHERE InvoiceId={INV_D}")
                check("B7 webhook then browser verify: the money is applied once (Paid still 600.00 in API and SQL)",
                      inv_paid(inv) == D("600.00") and dec(srow[0][0]) == D("600.00"), (s, (b or {}).get("message"), inv.get("paidAmount"), srow))
            else:
                check("B7 webhook: a sandbox order and payment could be opened on invoice D", False, (woid, wpid, s, (b or {}).get("message")))
        else:
            skip("B7 webhook", "QI_NOMAIL_API / QI_WEBHOOK_SECRET not set")
    else:
        check("B4 duplicate reference: an invoice exists to test on", False, "no invoice from approve nor convert")

    # ---- acceptance scenarios ---------------------------------------------------------------------------
    for g, title in (("S1", "Scenario 1 (approved): send -> approve -> one Due invoice copying the quotation -> paid in full -> Paid"),
                     ("S2", "Scenario 2 (rejected): send -> reject -> no invoice, no payment, refused convert/approve, absent from payments and reports"),
                     ("S3", "Scenario 3 (partial, approve twice): one invoice, 4,000 of 10,000 -> Partially Paid 4000/6000 in invoice and payment list")):
        bad = [n for n, ok in groups.get(g, []) if not ok]
        check(title, groups.get(g) and not bad, f"{len(bad)} failing step(s): " + "; ".join(bad[:4]))
except SystemExit:
    raise
except Exception:
    check("script ran to the end", False, traceback.format_exc())
finally:
    # ---- clean-up through the API --------------------------------------------------------------------
    left = []
    try:
        for iid in CREATED_INV:
            s, b = call("GET", ep("inv_payments", id=iid), "admin")
            for p in items(b):
                pid = f(p, "paymentId")
                if pid and not p.get("isReversed") and f(p, "canReverse", default=True):
                    rs, _ = call("POST", ep("pay_reverse", id=pid), "admin", {"reason": f"QI-E2E {RUN} cleanup"})
                    if not ok2(rs):
                        left.append(f"payment {pid} on invoice {iid} not reversed ({rs})")
            cs, cb = call("POST", ep("inv_cancel", id=iid), "admin", {"reason": f"QI-E2E {RUN} cleanup"})
            if not ok2(cs):
                left.append(f"invoice {iid} not cancelled ({cs} {(cb or {}).get('message')})")
        for qid in CREATED_Q:
            q = get_q(qid)
            if q_status(q) == Q_STATUS["Draft"]:
                ds, _ = call("DELETE", ep("quotation", id=qid), "admin")
                if ok2(ds):
                    continue
            if q_status(q) in (Q_STATUS["Sent"], Q_STATUS["Viewed"], Q_STATUS["Draft"]):
                call("POST", ep("q_cancel", id=qid), "admin", {"reason": f"QI-E2E {RUN} cleanup"})
        for r in sql(f"SELECT QuotationId, QuotationNumber, Status, ISNULL(CAST(InvoiceId AS varchar),'-') FROM TRN_Quotation WHERE Subject LIKE 'QI-E2E {RUN} %' AND IsDeleted=0"):
            left.append(f"quotation {r[0]} {r[1]} status {r[2]} invoice {r[3]}")
        for r in sql(f"SELECT i.InvoiceId, i.InvoiceNumber, i.Status, i.PaidAmount FROM TRN_Invoice i JOIN TRN_Quotation q ON q.QuotationId=i.QuotationId WHERE q.Subject LIKE 'QI-E2E {RUN} %'"):
            left.append(f"invoice {r[0]} {r[1]} status {r[2]} paid {r[3]}")
        for r in sql(f"SELECT p.PaymentId, p.Status, p.Amount FROM TRN_Payment p JOIN TRN_Invoice i ON i.InvoiceId=p.InvoiceId JOIN TRN_Quotation q ON q.QuotationId=i.QuotationId"
                     f" WHERE q.Subject LIKE 'QI-E2E {RUN} %'"):
            left.append(f"TRN_Payment {r[0]} status {r[1]} amount {r[2]}")
    except BaseException as e:
        left.append(f"clean-up stopped: {e}")
    print("\nTest data left in Dev (run " + RUN + "):\n  " + ("\n  ".join(left) if left else "none"))

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed, {len(results) - passed} failed, {len(skips)} skipped")
sys.exit(0 if results and passed == len(results) else 1)
