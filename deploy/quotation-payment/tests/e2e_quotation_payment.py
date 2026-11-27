"""quotation-payment kit (27 Nov 2026): API + SQL checks, LOCAL Dev stack only.

Quotation -> invoice -> payment, as one workflow:
  * approving a quotation raises exactly one invoice that carries the quotation's lines, quantities, prices,
    line and quotation discounts, GST, terms, customer and quotation number (plus the customer reference);
  * the Payments screen's Quotations view (GET /api/payments/quotations, /summary) lists approved quotations
    only, with the invoice's own paid / outstanding / Due / Partially Paid / Paid; recording a payment on
    the invoice moves it there, on the Invoices screen and in the payment register, with one payment row each;
  * rejected, sent-but-undecided, cancelled quotations never appear and never raise an invoice; an approved
    quotation without an invoice shows "Not invoiced" with the reason and no Record payment;
  * the security-deposit invoice is shown beside its quotation, not added into it;
  * customers cannot read the view; the existing payment / invoice lists still answer the same.

  API  http://127.0.0.1:81  (D:\\Regalis\\local-dev\\api)    DB localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev
  Mail must go to the local SMTP sink on 127.0.0.1:2525 (the script stops first if nothing answers there).

Creates: quotations with subject "QA-QP <run> ...", customers qp-<run>-*@example.test, their invoices and payments.
Removes at the end through the API: payments reversed, invoices cancelled, open / not-invoiced approved quotations
cancelled, test customers deleted (soft). Approved / rejected quotation numbers stay (never reused).
Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD.

usage: python -I e2e_quotation_payment.py
"""
import datetime as dt, json, os, random, socket, subprocess, sys, time, urllib.error, urllib.request

sys.stdout.reconfigure(encoding="utf-8")
API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
IST = dt.timezone(dt.timedelta(hours=5, minutes=30))
TODAY = dt.datetime.now(dt.timezone.utc).astimezone(IST).date()
ADMIN_EMAIL = os.environ["REGALIS_ADMIN_EMAIL"]
ADMIN_PW = os.environ["REGALIS_ADMIN_PASSWORD"]

results, CREATED_Q, CREATED_USERS = [], [], []


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
            raw = r.read() or b"{}"
            try:
                return r.status, json.loads(raw)
            except ValueError:
                return r.status, {"_bytes": len(raw), "_head": raw[:5].decode("latin-1")}
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


def dec(v):
    return round(float(v or 0), 2)


def sink_up():
    try:
        with socket.create_connection(("127.0.0.1", 2525), timeout=3) as so:
            return so.recv(100).startswith(b"220")
    except OSError:
        return False


def free_mobile():
    while True:
        m = "9" + "".join(random.choice("0123456789") for _ in range(9))
        if one(f"SELECT COUNT(*) FROM MST_User WHERE MobileNumber IN ('{m}','+91{m}')") == "0":
            return m


def new_customer(tag):
    em = f"qp-{RUN}-{tag}@example.test"
    s, b = call("POST", "/users", {"fullName": f"QA QP {RUN} {tag}", "email": em, "mobileNumber": "+91" + free_mobile(), "password": "Qa-Test#2026x",
                                    "userType": 1, "roleIds": [CUSTOMER_ROLE], "companyName": f"QP Test Co {RUN}", "defaultLocationId": 2})
    if not ok2(s):
        raise SystemExit(f"create customer {tag}: {s} {json.dumps(b)[:400]}")
    CREATED_USERS.append(data(b)["userId"])
    return data(b)["userId"], em


def make_quote(tag, customer_id=None, name=None, lines=None, discount_pct=None, reference=None, state="29", advance=None, terms=None):
    body = {"customerId": customer_id, "locationId": 2, "quotationDate": TODAY.isoformat(), "validUntil": (TODAY + dt.timedelta(days=15)).isoformat(),
            "subject": f"QA-QP {RUN} {tag}", "placeOfSupplyStateCode": state, "referenceNumber": reference,
            "customerName": None if customer_id else (name or f"QA QP Lead {RUN} {tag}"), "billingAddress": None if customer_id else "1 QP Street, Bengaluru",
            "discountType": 1 if discount_pct else None, "discountValue": discount_pct,
            "termsAndConditions": terms,
            "advanceInvoiceEnabled": True if advance else None, "advanceType": 2 if advance else None, "advanceValue": advance,
            "lines": lines or [{"lineType": 2, "serviceType": 5, "description": f"QA-QP {RUN} {tag}", "quantity": 1, "unitPrice": 1000,
                                "discountAmount": 0, "taxPercent": 18, "isNonTaxable": False}]}
    s, b = call("POST", "/quotations", body)
    if not ok2(s):
        raise SystemExit(f"create quotation {tag}: {s} {json.dumps(b)[:600]}")
    CREATED_Q.append(data(b)["quotationId"])
    return data(b)["quotationId"]


def send(qid, email_to):
    s, b = call("POST", f"/quotations/{qid}/send", {"sendEmail": True, "sendWhatsApp": False, "emailTo": email_to, "emailCc": None, "bccMe": False,
                                                    "emailSubject": None, "emailMessage": None, "whatsAppTo": None, "whatsAppMessage": None})
    if not ok2(s):
        raise SystemExit(f"send {qid}: {s} {json.dumps(b)[:400]}")


def decide(qid, approved=True):
    return call("POST", f"/quotations/{qid}/record-decision", {"approved": approved, "mode": 2, "confirmedBy": "QA tester",
                                                               "note": f"QA-QP {RUN}", "reasonCode": None if approved else "PRICE"})


def qrow(qno, status=None):
    s, b = call("GET", "/payments/quotations", params={"search": qno, "status": status, "pageSize": 100})
    rows = [r for r in items(b) if r.get("quotationNumber") == qno]
    return s, (rows[0] if rows else None), b


def summary(search=None):
    return data(call("GET", "/payments/quotations/summary", params={"search": search})[1])


def pay(inv_id, amount, mode=1, ref=None):
    return call("POST", f"/invoices/{inv_id}/payments", {"amount": amount, "paidOn": TODAY.isoformat(), "mode": mode, "reference": ref,
                                                          "bankAccountId": None, "note": f"QA-QP {RUN}"})


def invoice(inv_id):
    return data(call("GET", f"/invoices/{inv_id}")[1])


def payment_rows(inv_id):
    return int(one(f"SELECT COUNT(*) FROM TRN_Payment WHERE InvoiceId={inv_id} AND Status=2") or 0)


if not sink_up():
    raise SystemExit("No SMTP sink on 127.0.0.1:2525: start deploy/customer-portal/tests/smtp_sink.py and point the API at it first.")
TOK["admin"] = login(ADMIN_EMAIL, ADMIN_PW, "admin")
if not TOK["admin"]:
    raise SystemExit("admin login failed")
CUSTOMER_ROLE = int(one("SELECT RoleId FROM MST_Role WHERE RoleCode='CUSTOMER'"))
print(f"run {RUN}  today(IST) {TODAY}", flush=True)

try:
    s0, b0 = call("GET", "/payments/quotations", params={"pageSize": 1})
    check("0 GET /payments/quotations answers 200 for admin", s0 == 200 and "totalCount" in data(b0), (s0, b0))
    before = summary()
    s, b = call("GET", "/payments/quotations", params={"pageSize": 100})
    check("0 the view lists approved quotations only (every listed quotation is Approved in the database)",
          ok2(s) and all(one(f"SELECT Status FROM TRN_Quotation WHERE QuotationId={r['quotationId']}") == "4" for r in items(b)), items(b)[:2])
    approved_db = int(one("SELECT COUNT(*) FROM TRN_Quotation WHERE Status=4 AND IsDeleted=0"))
    check("0 summary counts every approved quotation (same number as the database)", before.get("approved") == approved_db, (before, approved_db))

    # ---- 1. approval -> one invoice carrying the quotation's details ------------------------------------------
    cid, cem = new_customer("a")
    lines = [
        {"lineType": 3, "serviceType": 5, "description": f"QA-QP {RUN} Workspace", "quantity": 0, "unitPrice": 0, "discountAmount": 0},
        {"lineType": 2, "serviceType": 5, "description": f"QA-QP {RUN} Cabin rent", "hsnSacCode": "997212", "quantity": 2, "unitPrice": 5000,
         "discountAmount": 500, "taxPercent": 18, "isNonTaxable": False},
        {"lineType": 2, "serviceType": 5, "description": f"QA-QP {RUN} Setup", "quantity": 1, "unitPrice": 2000, "discountAmount": 0, "taxPercent": 18,
         "isNonTaxable": False}]
    q1 = make_quote("FULL", customer_id=cid, lines=lines, discount_pct=10, reference=f"PO-{RUN}", terms=f"QA terms {RUN}: pay within 7 days.")
    send(q1, cem)
    q1no = one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={q1}")
    s, r, _ = qrow(q1no)
    check("1 a sent (not yet approved) quotation is not in the Payments view", ok2(s) and r is None, r)
    s, b = decide(q1)
    qd = data(b)
    check("1 Mark approved: 200 with the invoice number", ok2(s) and bool(qd.get("invoiceNumber")), (s, b))
    inv1 = qd.get("invoiceId")
    check("1 exactly one invoice for the quotation", one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={q1} AND IsDeleted=0") == "1")
    hdr = sql(f"SELECT q.SubTotal, i.SubTotal, q.DiscountAmount, i.DiscountAmount, q.TaxableAmount, i.TaxableAmount, q.CgstAmount, i.CgstAmount, "
              f"q.SgstAmount, i.SgstAmount, q.IgstAmount, i.IgstAmount, q.RoundOffAmount, i.RoundOffAmount, q.TotalAmount, i.TotalAmount, "
              f"CASE WHEN q.TermsAndConditions = i.TermsAndConditions THEN 1 ELSE 0 END, CASE WHEN q.CustomerName = i.CustomerName AND q.CustomerEmail = i.CustomerEmail THEN 1 ELSE 0 END, "
              f"i.CustomerId, ISNULL(i.Notes,''), i.Status, i.PaidAmount "
              f"FROM TRN_Quotation q JOIN TRN_Invoice i ON i.QuotationId = q.QuotationId WHERE q.QuotationId={q1}")
    h = hdr[0] if hdr else []
    check("1 invoice totals = quotation totals (sub total, discount, taxable, CGST, SGST, IGST, round-off, total)",
          bool(h) and all(h[k] == h[k + 1] for k in range(0, 16, 2)), h)
    check("1 invoice carries the quotation discount (10 % of the sub total)", bool(h) and dec(h[3]) > 0 and abs(dec(h[3]) - round(dec(h[1]) * 0.10, 2)) < 0.011, h[:4])
    check("1 invoice carries the terms and the customer (snapshot and account)", bool(h) and h[16] == "1" and h[17] == "1" and h[18] == str(cid), h[16:19])
    check("1 invoice notes name the quotation number and the customer reference", bool(h) and q1no in h[19] and f"PO-{RUN}" in h[19], h[19:20])
    check("1 invoice is issued and due: status Pending (shown Due), paid 0", bool(h) and h[20] == "1" and dec(h[21]) == 0, h[20:])
    ql = sql(f"SELECT LineNumber, Quantity, UnitPrice, LineAmount, TaxPercent, TaxAmount, TotalAmount, ISNULL(HsnSacCode,''), Description FROM TRN_QuotationLine WHERE QuotationId={q1} ORDER BY LineNumber")
    il = sql(f"SELECT LineNumber, Quantity, UnitPrice, LineAmount, TaxPercent, TaxAmount, TotalAmount, ISNULL(HsnSacCode,''), Description FROM TRN_InvoiceLine WHERE InvoiceId={inv1} ORDER BY LineNumber")
    check("1 invoice lines = quotation lines (count, quantity, unit price, amount, GST %, GST, total, HSN/SAC)",
          len(ql) == len(il) == 3 and all(a[:8] == b[:8] for a, b in zip(ql, il)), (ql, il))
    check("1 the line discount is named on the invoice line (its amount is already net of it)",
          len(il) == 3 and "less discount Rs 500.00" in il[1][8] and il[1][8].startswith(f"QA-QP {RUN} Cabin rent") and il[2][8] == f"QA-QP {RUN} Setup", il)
    qdto = data(call("GET", f"/quotations/{q1}")[1])
    check("1 quotation page data: invoice id and number on the quotation", qdto.get("invoiceId") == inv1 and qdto.get("invoiceNumber") == qd.get("invoiceNumber"), qdto.get("invoiceId"))
    s, b = decide(q1)
    check("1 approving again: 200, the same invoice, still one", ok2(s) and data(b).get("invoiceId") == inv1
          and one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={q1} AND IsDeleted=0") == "1", (s, data(b).get("invoiceId")))
    s, b = call("POST", f"/invoices/from-quotation/{q1}")
    check("1 Convert to invoice again: 200 with the same invoice", s == 200 and data(b).get("invoiceId") == inv1, (s, b))
    total1 = dec(h[15]) if h else 0

    # ---- 2. Payments view: Due ----------------------------------------------------------------------------------
    s, r, _ = qrow(q1no)
    check("2 approved quotation is in the Payments view", ok2(s) and r is not None, r)
    r = r or {}
    check("2 row: quotation, customer, invoice number", r.get("invoiceId") == inv1 and r.get("invoiceNumber") == qd.get("invoiceNumber")
          and r.get("customerId") == cid and r.get("customerEmail") == cem and r.get("referenceNumber") == f"PO-{RUN}", r)
    check("2 row: total = invoice total = quotation total, paid 0, outstanding = total, Due",
          dec(r.get("invoiceTotal")) == total1 and dec(r.get("quotationTotal")) == total1 and dec(r.get("paidAmount")) == 0
          and dec(r.get("outstandingAmount")) == total1 and r.get("paymentStatus") == "Due", r)
    check("2 row: Record payment offered (admin has PAYMENT.RECORD), no payments yet", r.get("canRecordPayment") is True and r.get("paymentCount") == 0, r)
    s, rd, _ = qrow(q1no, "due")
    s2, rp, _ = qrow(q1no, "paid")
    s3, ro, _ = qrow(q1no, "open")
    check("2 filters: in Due and Awaiting payment, not in Paid", rd is not None and ro is not None and rp is None, (rd is not None, ro is not None, rp is not None))
    s, b = call("GET", "/payments/quotations", params={"search": f"PO-{RUN}"})
    check("2 search finds it by the customer reference", s == 200 and any(x.get("quotationId") == q1 for x in items(b)), items(b)[:1])
    s, b = call("GET", "/payments/quotations", params={"search": qd.get("invoiceNumber")})
    check("2 search finds it by the invoice number", any(x.get("quotationId") == q1 for x in items(b)), items(b)[:1])

    # ---- 3. partial payment on the invoice -> Partially Paid everywhere, one payment row ----------------------
    s, b = pay(inv1, 4000, mode=2, ref=f"UPI{RUN}")
    check("3 record ₹4,000 by UPI on the quotation's invoice", ok2(s), (s, b))
    s, r, _ = qrow(q1no)
    r = r or {}
    check("3 row: paid 4,000, outstanding = total - 4,000, Partially Paid, 1 payment",
          dec(r.get("paidAmount")) == 4000 and dec(r.get("outstandingAmount")) == round(total1 - 4000, 2) and r.get("paymentStatus") == "Partially Paid"
          and r.get("paymentCount") == 1 and r.get("lastPaymentUtc"), r)
    iv = invoice(inv1)
    check("3 Invoices screen shows the same paid / outstanding", dec(iv.get("paidAmount")) == 4000 and dec(iv.get("outstandingAmount")) == dec(r.get("outstandingAmount")), iv.get("paidAmount"))
    s, b = call("GET", "/payments/register", params={"Search": q1no, "PageSize": 100})
    reg = [x for x in items(b) if x.get("invoiceId") == inv1]
    check("3 Payments register: one row, with the quotation number, Partially Paid, same outstanding",
          len(reg) == 1 and reg[0].get("quotationNumber") == q1no and reg[0].get("paymentStatus") == "Partially Paid"
          and dec(reg[0].get("outstandingAmount")) == dec(r.get("outstandingAmount")) and dec(reg[0].get("amount")) == 4000, reg)
    check("3 database: exactly one successful payment and one receipt on the invoice", payment_rows(inv1) == 1
          and one(f"SELECT COUNT(*) FROM TRN_InvoicePayment WHERE InvoiceId={inv1} AND ReversalOfId IS NULL") == "1")
    check("3 no payment is tied to the quotation outside its invoice (TRN_Payment has no quotation link; all on invoice)",
          one(f"SELECT COUNT(*) FROM TRN_Payment p WHERE p.CustomerId={cid} AND p.InvoiceId IS NULL") == "0")
    s, b = pay(inv1, 1000, mode=2, ref=f"UPI{RUN}")
    check("3 the same UPI reference again is refused (409), still one payment", s == 409 and payment_rows(inv1) == 1, (s, b))
    s, b = pay(inv1, round(total1 - 4000 + 1, 2), mode=1)
    check("3 more than the outstanding is refused, still one payment", not ok2(s) and payment_rows(inv1) == 1, (s, (b or {}).get("message")))
    s, rp, _ = qrow(q1no, "partial")
    check("3 filter Partially paid finds it", rp is not None)

    # ---- 4. the rest -> Paid -------------------------------------------------------------------------------------
    s, b = pay(inv1, round(total1 - 4000, 2), mode=1)
    check("4 record the rest in cash", ok2(s), (s, b))
    s, r, _ = qrow(q1no)
    r = r or {}
    check("4 row: Paid, outstanding 0, 2 payments, no Record payment", r.get("paymentStatus") == "Paid" and dec(r.get("outstandingAmount")) == 0
          and dec(r.get("paidAmount")) == total1 and r.get("paymentCount") == 2 and r.get("canRecordPayment") is False, r)
    iv = invoice(inv1)
    check("4 invoice: Paid (status 2), outstanding 0", iv.get("status") == 2 and dec(iv.get("outstandingAmount")) == 0, (iv.get("status"), iv.get("outstandingAmount")))
    s, rp, _ = qrow(q1no, "paid")
    s, ro, _ = qrow(q1no, "open")
    check("4 filters: in Paid, not in Awaiting payment", rp is not None and ro is None)
    s, b = pay(inv1, 1, mode=1)
    check("4 a payment on the paid invoice is refused", not ok2(s) and payment_rows(inv1) == 2, (s, (b or {}).get("message")))

    # ---- 5. rejected / undecided / cancelled quotations never appear ----------------------------------------------
    cid_b, cem_b = new_customer("b")
    q_rej = make_quote("REJECT", customer_id=cid_b)
    send(q_rej, cem_b)
    s, b = decide(q_rej, approved=False)
    q_rej_no = one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={q_rej}")
    check("5 rejected: recorded", ok2(s) and one(f"SELECT Status FROM TRN_Quotation WHERE QuotationId={q_rej}") == "5", (s, b))
    s, r, b = qrow(q_rej_no)
    check("5 rejected quotation is not in the Payments view (any status)", ok2(s) and r is None, items(b)[:1])
    for st in ("noinvoice", "cancelled", "due"):
        s, r, _ = qrow(q_rej_no, st)
        check(f"5 rejected quotation not under filter {st}", r is None)
    check("5 rejected quotation has no invoice", one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={q_rej}") == "0")
    s, b = call("POST", f"/invoices/from-quotation/{q_rej}")
    check("5 Convert to invoice on the rejected quotation is refused (422)", s == 422, (s, b))
    s, b = decide(q_rej, approved=True)
    check("5 approving the rejected quotation is refused, still no invoice", not ok2(s) and one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={q_rej}") == "0", (s, b))
    s, b = call("GET", "/payments/register", params={"Search": q_rej_no})
    check("5 Payments register has nothing for the rejected quotation", ok2(s) and items(b) == [], items(b)[:1])
    q_draft = make_quote("DRAFT", customer_id=cid_b)
    q_draft_no = one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={q_draft}")
    call("POST", f"/quotations/{q_draft}/cancel", {"reason": f"QA-QP {RUN}"})
    s, r, _ = qrow(q_draft_no)
    check("5 a cancelled quotation is not in the Payments view", one(f"SELECT Status FROM TRN_Quotation WHERE QuotationId={q_draft}") == "7" and r is None)

    # ---- 6. approved without an invoice (staff email) -> Not invoiced, reason, no Record payment; cancel ---------
    q_staff = make_quote("STAFF")
    send(q_staff, ADMIN_EMAIL)
    q_staff_no = one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={q_staff}")
    s, b = decide(q_staff)
    check("6 staff email: approval recorded, no invoice", ok2(s) and not data(b).get("invoiceId"), (s, data(b).get("invoiceId")))
    s, r, _ = qrow(q_staff_no)
    r = r or {}
    check("6 row: Not invoiced, the reason from the timeline, nothing payable", r.get("paymentStatus") == "Not invoiced" and r.get("invoiceId") is None
          and "staff account" in (r.get("noInvoiceReason") or "") and r.get("canRecordPayment") is False and r.get("outstandingAmount") is None, r)
    s, rn, _ = qrow(q_staff_no, "noinvoice")
    check("6 filter Not invoiced finds it", rn is not None)
    s, b = call("POST", f"/quotations/{q_staff}/cancel", {"reason": f"QA-QP {RUN} approved by mistake"})
    s2, r, _ = qrow(q_staff_no)
    check("6 an approved quotation without an invoice can be cancelled; it then leaves the Payments view", ok2(s) and r is None, (s, b))

    # ---- 7. security deposit shown beside the quotation, not added into it ---------------------------------------
    cid_c, cem_c = new_customer("c")
    q_adv = make_quote("ADVANCE", customer_id=cid_c, advance=3000)
    send(q_adv, cem_c)
    q_adv_no = one(f"SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={q_adv}")
    s, b = decide(q_adv)
    inv_adv = data(b).get("invoiceId")
    s, r, _ = qrow(q_adv_no)
    r = r or {}
    dep = r.get("deposit") or {}
    dep_db = sql(f"SELECT InvoiceId, TotalAmount FROM TRN_Invoice WHERE Notes='Refundable security deposit against quotation {q_adv_no}.' AND IsDeleted=0")
    check("7 deposit invoice raised on approval (existing behaviour)", len(dep_db) == 1, dep_db)
    check("7 row shows the deposit invoice beside the quotation's invoice", bool(dep_db) and dep.get("invoiceId") == int(dep_db[0][0])
          and dec(dep.get("totalAmount")) == dec(dep_db[0][1]) and dep.get("paymentStatus") == "Due" and dep.get("canRecordPayment") is True, r)
    check("7 the deposit is not added into the quotation's figures", dec(r.get("invoiceTotal")) == dec(one(f"SELECT TotalAmount FROM TRN_Invoice WHERE InvoiceId={inv_adv}"))
          and dec(r.get("invoiceTotal")) == dec(r.get("quotationTotal")), r)
    if dep_db:
        s, b = pay(int(dep_db[0][0]), 3000, mode=1)
        s2, r2, _ = qrow(q_adv_no)
        r2 = r2 or {}
        check("7 paying the deposit leaves the quotation's invoice Due", ok2(s) and r2.get("paymentStatus") == "Due" and (r2.get("deposit") or {}).get("paymentStatus") == "Paid", r2)

    # ---- 8. inter-state quotation: IGST carried; invoice PDF still renders ---------------------------------------
    cid_d, cem_d = new_customer("d")
    q_igst = make_quote("IGST", customer_id=cid_d, state="33")
    send(q_igst, cem_d)
    s, b = decide(q_igst)
    inv_igst = data(b).get("invoiceId")
    g = sql(f"SELECT q.IgstAmount, i.IgstAmount, i.CgstAmount, q.TotalAmount, i.TotalAmount FROM TRN_Quotation q JOIN TRN_Invoice i ON i.QuotationId=q.QuotationId WHERE q.QuotationId={q_igst}")
    check("8 inter-state: invoice carries IGST and no CGST, totals equal", bool(g) and dec(g[0][0]) > 0 and g[0][0] == g[0][1] and dec(g[0][2]) == 0 and g[0][3] == g[0][4], g)
    s, b = call("GET", f"/invoices/{inv_igst}/pdf")
    check("8 inter-state invoice PDF renders", s == 200 and b.get("_head") == "%PDF-", (s, b))
    s, b = call("GET", f"/invoices/{inv1}/pdf")
    check("8 intra-state invoice PDF renders", s == 200 and b.get("_head") == "%PDF-", (s, b))
    if os.environ.get("PDFLIB"):  # optional: a folder holding pypdf, to read the printed place of supply
        sys.path.insert(0, os.environ["PDFLIB"])
        import io, pypdf

        def pdf_text(iid):
            req = urllib.request.Request(f"{API}/invoices/{iid}/pdf")
            req.add_header("Authorization", "Bearer " + TOK["admin"])
            with urllib.request.urlopen(req, timeout=60) as rr:
                return " ".join(p.extract_text() or "" for p in pypdf.PdfReader(io.BytesIO(rr.read())).pages)
        t_igst, t_intra = pdf_text(inv_igst), pdf_text(inv1)
        check("8 inter-state invoice PDF prints the quotation's place of supply (Tamil Nadu (33))", "Tamil Nadu (33)" in t_igst, t_igst[:600])
        check("8 intra-state invoice PDF prints the home state as before (Karnataka (29))", "Karnataka (29)" in t_intra and "Tamil Nadu" not in t_intra, t_intra[:600])

    # ---- 9. consistency with the Invoices screen; access ---------------------------------------------------------
    s, b = call("GET", "/payments/quotations", params={"pageSize": 30})
    bad = []
    for x in items(b):
        if x.get("invoiceId"):
            iv = invoice(x["invoiceId"])
            live = iv.get("status") not in (4, 5)
            if dec(iv.get("totalAmount")) != dec(x.get("invoiceTotal")) or dec(iv.get("paidAmount")) != dec(x.get("paidAmount")) \
                    or (live and dec(iv.get("outstandingAmount")) != dec(x.get("outstandingAmount"))) or (not live and x.get("outstandingAmount") is not None):
                bad.append((x["quotationNumber"], x.get("invoiceTotal"), x.get("paidAmount"), x.get("outstandingAmount"), iv.get("totalAmount"), iv.get("paidAmount"), iv.get("outstandingAmount")))
    check("9 every listed row (first 30) has the Invoices screen's total / paid / outstanding", ok2(s) and not bad, bad)
    after = summary()
    check("9 summary moved by this run's approvals (FULL, STAFF cancelled again, ADVANCE, IGST = +3)", after.get("approved") == before.get("approved", 0) + 3, (before, after))
    s, b = _call("GET", "/payments/quotations", None, None)
    check("9 no token: 401", s == 401, s)
    pw = "Qa-Portal#2026x"
    rs, _ = call("POST", f"/users/{cid}/reset-password", {"newPassword": pw})
    ctok = login(cem, pw, "customer") if ok2(rs) else None
    TOK["cust"] = ctok
    s, b = call("GET", "/payments/quotations", who="cust")
    s2, b2 = call("GET", "/payments/quotations/summary", who="cust")
    check("9 customer token: 403 on the view and its summary (staff only)", bool(ctok) and s == 403 and s2 == 403, (s, s2))
    s, b = call("GET", "/invoices/my", params={"page": 1, "pageSize": 50}, who="cust")
    check("9 customer portal: own invoice list still shows the quotation invoice, Paid", ok2(s) and any(i.get("invoiceId") == inv1 and i.get("status") == 2 for i in items(b)), items(b)[:1])
    s, b = call("GET", "/payments", params={"page": 1, "pageSize": 5})
    s2, b2 = call("GET", "/invoices", params={"page": 1, "pageSize": 5})
    s3, b3 = call("GET", "/payments/register", params={"page": 1, "pageSize": 5})
    check("9 existing lists still answer 200 (payments, invoices, register)", s == 200 and s2 == 200 and s3 == 200, (s, s2, s3))
finally:
    left = []
    try:
        for qid in CREATED_Q:
            for r in sql(f"SELECT InvoiceId FROM TRN_Invoice WHERE IsDeleted=0 AND Status NOT IN (4,5) AND (QuotationId={qid} OR Notes = 'Refundable security deposit against quotation ' + (SELECT QuotationNumber FROM TRN_Quotation WHERE QuotationId={qid}) + '.')"):
                iid = r[0]
                s, b = call("GET", f"/invoices/{iid}/payments")
                for p in items(b):
                    pid = p.get("paymentId")
                    if pid and not p.get("isReversed") and not p.get("reversalOfId") and p.get("amount", 0) > 0:
                        rs, _ = call("POST", f"/payments/{pid}/reverse", {"reason": f"QA-QP {RUN} cleanup"})
                        if not ok2(rs):
                            left.append(f"payment {pid} not reversed ({rs})")
                cs, cb = call("POST", f"/invoices/{iid}/cancel", {"reason": f"QA-QP {RUN} cleanup"})
                if not ok2(cs):
                    left.append(f"invoice {iid} not cancelled ({cs} {(cb or {}).get('message')})")
            st = one(f"SELECT Status FROM TRN_Quotation WHERE QuotationId={qid}")
            if st in ("1", "2", "3") or (st == "4" and one(f"SELECT COUNT(*) FROM TRN_Invoice WHERE QuotationId={qid}") == "0"):
                call("POST", f"/quotations/{qid}/cancel", {"reason": f"QA-QP {RUN} cleanup"})
        for r in sql(f"SELECT UserId FROM MST_User WHERE Email LIKE 'qp-{RUN}-%@example.test' AND IsDeleted=0"):
            ds, db_ = call("DELETE", f"/users/{r[0]}")
            if not ok2(ds):
                left.append(f"user {r[0]} not deleted ({ds} {(db_ or {}).get('message')})")
        for r in sql(f"SELECT QuotationId, QuotationNumber, Status FROM TRN_Quotation WHERE Subject LIKE 'QA-QP {RUN} %' AND IsDeleted=0 AND Status NOT IN (7)"):
            left.append(f"quotation {r[0]} {r[1]} status {r[2]} (approved with an invoice / rejected quotations cannot be cancelled)")
    except BaseException as e:
        left.append(f"clean-up stopped: {e}")
    print("\nTest data left in Dev (run " + RUN + "):\n  " + ("\n  ".join(left) if left else "none"))

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed, {len(results) - passed} failed")
sys.exit(0 if results and passed == len(results) else 1)
