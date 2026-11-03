"""End-to-end checks of the Billing & Finance kit against a disposable database.
Run against the test API on :83 (RegalisWorkSpace_BFTest). Uses made-up data only."""
import datetime as dt, json, os, sys
import bf

ADMIN_EMAIL = os.environ.get("REGALIS_TEST_EMAIL", "admin@regalis.in")
ADMIN_PASSWORD = os.environ["REGALIS_TEST_PASSWORD"]  # local Dev admin password; never committed

ok = fail = 0
def check(name, cond, detail=""):
    global ok, fail
    if cond: ok += 1; print("PASS", name)
    else: fail += 1; print("FAIL", name, "--", str(detail)[:400])

def err(r):
    return (r.get("message") if isinstance(r, dict) else str(r)) or ""

today = dt.date.today()
iso = today.isoformat()
bf.login(ADMIN_EMAIL, ADMIN_PASSWORD)

# ── 0. Fixtures: a bank account and a supplier ────────────────────────────────────
s, r = bf.call("POST", "/bank/accounts", {"accountCode": "TEST-CA-01", "bankName": "Test Bank", "accountName": "Regalis WorkSpace",
    "accountNumberMasked": "XXXX1234", "ifsc": None, "branch": None, "accountType": "Current", "openingBalance": 100000, "openingBalanceAsOn": None, "locationId": None, "notes": None})
if s != 200:
    s, r = bf.call("GET", "/bank/accounts")
    acct = next(a for a in r["data"]["items" if isinstance(r["data"], dict) else None] if a["accountCode"] == "TEST-CA-01") if isinstance(r["data"], dict) else next(a for a in r["data"] if a["accountCode"] == "TEST-CA-01")
else:
    acct = r["data"]
acct_id = acct["bankAccountId"]
check("bank account fixture", acct_id > 0, r)

s, r = bf.call("GET", "/purchase-bills/form-options")
sup = r["data"]["suppliers"][0]
sup_id = sup["supplierId"]

# ── 1. Record a customer payment (doc §3) ─────────────────────────────────────────
INV = 10003
s, r = bf.call("GET", f"/invoices/{INV}/payment-context")
check("payment context", s == 200 and r["data"]["outstanding"] == 33040, r)
inv_date = r["data"]["invoiceDate"]
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 0, "paidOn": iso, "mode": 1})
check("amount required", s == 422 and "Enter the amount received" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 99999, "paidOn": iso, "mode": 1})
check("overpayment refused", s == 422 and "more than the" in err(r) and "outstanding" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 10, "paidOn": (today + dt.timedelta(days=2)).isoformat(), "mode": 1})
check("future date refused", s == 422 and "future" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 10, "paidOn": "2026-01-01", "mode": 1})
check("before invoice refused", s == 422 and "before the invoice date" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 10, "paidOn": iso, "mode": 2})
check("UPI needs reference", s == 422 and "reference" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 10, "paidOn": iso, "mode": 3, "reference": "UTR1"})
check("bank transfer needs account", s == 422 and "account" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 500, "paidOn": iso, "mode": 6})
check("wallet capped at balance", s == 422 and "wallet only has" in err(r), r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 100, "paidOn": iso, "mode": 6})
check("wallet payment", s == 200 and r["data"]["outstanding"] == 32940, r)
s, r = bf.call("POST", f"/invoices/{INV}/payments", {"amount": 10000, "paidOn": iso, "mode": 2, "reference": "upi-ref-77"})
check("UPI part payment", s == 200 and r["data"]["invoiceStatus"] == 3 and "still outstanding" in r["message"], r)
upi_payment_id = r["data"]["payment"]["paymentId"] if s == 200 else None
s, r = bf.call("POST", f"/payments/{upi_payment_id}/reverse", {"reason": "Entered against the wrong invoice"})
check("reverse payment", s == 200 and r["data"]["amount"] == -10000, r)
s, r = bf.call("POST", f"/payments/{upi_payment_id}/reverse", {"reason": "again"})
check("reverse twice refused", s == 422, r)
s, r = bf.call("GET", f"/invoices/{INV}/payment-context")
check("outstanding back after reversal", r["data"]["outstanding"] == 32940, r["data"])
s, r = bf.call("GET", "/payments/register?source=offline&pageSize=50")
rows = r["data"]["items"]
check("register shows offline modes", any(x["modeName"] == "UPI" and x["isReversed"] for x in rows) and any(x["modeName"] == "Wallet" for x in rows), rows[:3])
s, r = bf.call("GET", "/payments/register?source=online&pageSize=50")
check("register source filter", s == 200 and all(not x["isOffline"] for x in r["data"]["items"]), r)

# ── 2. Purchase bills (doc §4) ────────────────────────────────────────────────────
s, r = bf.call("PUT", f"/suppliers/{sup_id}/tax-profile", {"pan": "abc12", "defaultTdsSection": None, "paymentTermsDays": 30, "stateCode": "Karnataka", "isIndividual": False})
check("PAN format refused", s == 422 and "five letters" in err(r), r)
s, r = bf.call("PUT", f"/suppliers/{sup_id}/tax-profile", {"pan": "AABCI1234F", "defaultTdsSection": "194C", "paymentTermsDays": 30, "stateCode": "Karnataka", "isIndividual": False})
check("supplier profile saved", s == 200 and r["data"]["stateCode"] == "29", r)
bill_no = f"BS/{today:%m%d}/1"
line = {"description": "AC servicing", "hsnSac": "998719", "quantity": 1, "rate": 10000, "gstRate": 18}
body = {"supplierId": sup_id, "purchaseOrderId": None, "supplierBillNo": bill_no, "billDate": iso, "paymentTermsDays": 30,
        "dueDate": (today + dt.timedelta(days=30)).isoformat(), "locationId": 2, "tdsSection": "194C", "notes": None, "lines": [line], "submit": True, "acknowledgeNoGstin": True}
s, r = bf.call("POST", "/purchase-bills", body)
check("bill below limit auto-approved", s == 200 and r["data"]["status"] == 3, r)
bill = r["data"] if s == 200 else {}
check("bill GST split CGST+SGST", bill.get("cgstAmount") == 900 and bill.get("sgstAmount") == 900 and not bill.get("isInterState"), bill)
check("bill TDS 2% on taxable", bill.get("tdsAmount") == 200 and bill.get("payableToSupplier") == 11600, bill)
s, r = bf.call("POST", "/purchase-bills", {**body, "supplierBillNo": bill_no.lower().replace("/", " / ")})
check("duplicate bill refused 409", s == 409 and "already has a bill" in err(r), r)
s, r = bf.call("POST", "/purchase-bills", {**body, "supplierBillNo": bill_no + "X", "lines": [{**line, "hsnSac": None}]})
check("HSN required with GST", s == 422 and "HSN" in err(r), r)
s, r = bf.call("POST", "/purchase-bills", {**body, "supplierBillNo": bill_no + "B", "lines": [{**line, "rate": 40000}]})
big = r["data"] if s == 200 else {}
check("bill above limit awaits approval", s == 200 and big.get("status") == 2, r)
s, r = bf.call("POST", f"/purchase-bills/{big.get('purchaseBillId')}/approve", {"comment": "ok"})
check("cannot approve own bill", s == 422 and "You entered this bill" in err(r), r)

# ── 3. Vendor payment with TDS (doc §6, §10) ─────────────────────────────────────
s, r = bf.call("GET", f"/suppliers/{sup_id}/outstanding-bills")
check("outstanding bills feed", s == 200 and any(b["purchaseBillId"] == bill["purchaseBillId"] for b in r["data"]["bills"])
      and any(b["purchaseBillId"] == big.get("purchaseBillId") for b in r["data"]["notPayable"]), r)
pay = {"supplierId": sup_id, "paidOn": iso, "bankAccountId": acct_id, "fromPettyCash": False, "mode": 1, "reference": "NEFT-TEST-1", "note": None,
       "amount": 11500, "advanceToAdjust": 0, "allocations": [{"purchaseBillId": bill["purchaseBillId"], "amount": 11600, "tdsAmount": 200}]}
s, r = bf.call("POST", "/vendor-payments", pay)
check("allocation mismatch refused", s == 422 and "must match exactly" in err(r), r)
s, r = bf.call("POST", "/vendor-payments", {**pay, "allocations": [{"purchaseBillId": big.get("purchaseBillId"), "amount": 100, "tdsAmount": 0}], "amount": 100})
check("unapproved bill cannot be paid", s == 422 and "awaiting approval" in err(r), r)
s, r = bf.call("POST", "/vendor-payments", {**pay, "amount": 11600})
check("vendor payment posted", s == 200 and "now Paid" in r["message"] and "TDS payable" in r["message"], r)
vp = r["data"]["payment"] if s == 200 else {}
s, r = bf.call("GET", "/tds/payable")
tds_rows = [t for t in r["data"] if t["sourceId"] == vp.get("vendorPaymentId")]
check("TDS payable written at payment", len(tds_rows) == 1 and tds_rows[0]["tdsAmount"] == 200 and tds_rows[0]["partyTaxId"] == "AABCI1234F", r["data"][:2])
s, r = bf.call("POST", "/tds/challans", {"bsrCode": "0510308", "challanNo": "12871", "paidOn": iso, "amount": 150, "tdsEntryIds": [tds_rows[0]["tdsEntryId"]]})
check("challan short refused", s == 422 and "deductions ticked come to" in err(r), r)
s, r = bf.call("POST", "/tds/challans", {"bsrCode": "0510308", "challanNo": "12871", "paidOn": iso, "amount": 200, "tdsEntryIds": [tds_rows[0]["tdsEntryId"]]})
check("challan recorded", s == 200 and r["data"]["covered"] == 200, r)
s, r = bf.call("POST", "/tds/challans", {"bsrCode": "0510308", "challanNo": "12872", "paidOn": iso, "amount": 200, "tdsEntryIds": [tds_rows[0]["tdsEntryId"]]})
check("deduction on one challan only", s == 422 and "already on challan" in err(r), r)
s, r = bf.call("POST", f"/vendor-payments/{vp.get('vendorPaymentId')}/reverse", {"reason": "test"})
check("reversal blocked once TDS is on a challan", s == 422 and "challan" in err(r), r)
s, r = bf.call("GET", f"/parties/supplier/{sup_id}/ledger")
led = r["data"]
check("supplier ledger balances", s == 200 and led["closingBalance"] == 0 and len(led["rows"]) >= 3, led)
s, r = bf.call("GET", "/parties/supplier/ageing")
check("supplier ageing (awaiting bill not due)", s == 200 and r["data"]["totals"]["notDue"] == 47200, r["data"]["totals"])

# ── 4. Bank import and matching (doc §8, §9) ─────────────────────────────────────
csv = ("Statement of account\nAccount: XXXX1234\n\nTxn Date,Narration,Ref No,Withdrawal Amt,Deposit Amt,Closing Balance\n"
       f"{today:%d/%m/%Y},NEFT-TEST-1 SUPPLIER,NEFT-TEST-1,11610.00,,88390.00\n"
       f"{today:%d/%m/%Y},NEFT CR KAUSIKA R INV 00004,UTR-CUST-9,,29000.00,117390.00\n"
       f"{today:%d/%m/%Y},BAD ROW,UTR-X,,,117390.00\n"
       f"31/13/2026,BAD DATE,UTR-Y,10,,117380.00\n").encode()
files = {"file": ("hdfc.csv", csv, "text/csv")}
s, r = bf.call("POST", "/bank-import/parse", {"bankAccountId": acct_id}, files=files)
prev = r["data"] if s == 200 else {}
check("parse finds header after preamble", s == 200 and prev.get("headers", [None])[0] == "Txn Date" and prev["appliedMapping"]["outColumn"] == "Withdrawal Amt", r)
check("preview counts", prev.get("newCount") == 2 and prev.get("errorCount") == 2, prev.get("rows"))
mapping = json.dumps(prev.get("appliedMapping"))
s, r = bf.call("POST", "/bank-import/commit", {"bankAccountId": acct_id, "mapping": mapping}, files=files)
check("import commits 2 lines", s == 200 and r["data"]["imported"] == 2, r)
batch_id = r["data"]["bankImportBatchId"] if s == 200 else None
s, r = bf.call("POST", "/bank-import/commit", {"bankAccountId": acct_id, "mapping": mapping}, files=files)
check("same file twice adds nothing", s == 200 and r["data"]["imported"] == 0 and r["data"]["skipped"] == 2, r)
s, r = bf.call("GET", f"/bank-lines?bankAccountId={acct_id}&status=open")
lines = r["data"]["page"]["items"]
debit = next(l for l in lines if l["direction"] == 2)
credit = next(l for l in lines if l["direction"] == 1)
s, r = bf.call("GET", f"/bank-lines/{debit['bankTransactionId']}/suggestions")
check("debit suggestions rank the vendor payment", s == 200 and r["data"]["candidates"][0]["docType"] == 4, r["data"]["candidates"][:2] if s == 200 else r)
A = [{"docType": 4, "docId": vp.get("vendorPaymentId"), "amount": vp.get("amount")}]
s, r = bf.call("POST", f"/bank-lines/{debit['bankTransactionId']}/match", {"allocations": A, "differences": []})
check("unbalanced match refused", s == 422 and "still unexplained" in err(r), r)
s, r = bf.call("POST", f"/bank-lines/{debit['bankTransactionId']}/match", {"allocations": A, "differences": [{"reason": 4, "amount": 10}, {"reason": 4, "amount": 50}]})
check("rounding over cap refused", s == 422 and "rounding cap" in err(r), r)
s, r = bf.call("POST", f"/bank-lines/{debit['bankTransactionId']}/match", {"allocations": A, "differences": [{"reason": 2, "amount": 10, "note": "NEFT charge"}]})
check("vendor payment matched with bank charge", s == 200 and r["data"]["status"] == 3 and r["data"]["differences"][0]["signedAmount"] == 10, r)
s, r = bf.call("GET", "/expenses?search=NEFT%20charge")
check("bank charge expense created", s == 200 and any(x["category"] == "Bank charges" for x in r["data"]["items"]), r)
s, r = bf.call("GET", f"/bank-lines/{credit['bankTransactionId']}/suggestions")
cand = [c for c in r["data"]["candidates"] if c["docType"] == 1 and c["docId"] == INV]
check("credit suggests the invoice", len(cand) == 1, r["data"]["candidates"][:3])
# Customer paid 29,000 and deducted 3,940 TDS against 32,940 outstanding.
s, r = bf.call("POST", f"/bank-lines/{credit['bankTransactionId']}/match", {"allocations": [{"docType": 1, "docId": INV, "amount": 32940}],
    "differences": [{"reason": 1, "amount": 3940, "tdsSection": "194J", "targetDocType": 1, "targetDocId": INV}]})
check("credit matched with TDS difference", s == 200 and r["data"]["status"] == 3 and len(r["data"]["differences"]) == 1, r)
s, r = bf.call("GET", f"/invoices/{INV}/payment-context")
check("invoice fully paid by bank + TDS", r["data"]["outstanding"] == 0 and r["data"]["invoiceStatus"] == 2, r["data"])
s, r = bf.call("GET", "/tds/receivable")
check("TDS receivable raised", any(t["tdsAmount"] == 3940 and t["section"] == "194J" for t in r["data"]), r["data"])
s, r = bf.call("POST", f"/bank-import/{batch_id}/undo")
check("undo blocked once matched", s == 422 and "already been matched" in err(r), r)
s, r = bf.call("POST", f"/bank/transactions/{credit['bankTransactionId']}/unmatch")
check("old unmatch blocked by trigger", s == 422 and "Use Unmatch there" in err(r), r)
s, r = bf.call("POST", f"/bank-lines/{credit['bankTransactionId']}/unmatch", {"reason": "Testing unmatch"})
check("unmatch reverses receipts", s == 200 and r["data"]["status"] == 1, r)
s, r = bf.call("GET", f"/invoices/{INV}/payment-context")
check("invoice outstanding restored", r["data"]["outstanding"] == 32940, r["data"])
s, r = bf.call("GET", "/tds/receivable")
check("TDS receivable removed", not any(t["tdsAmount"] == 3940 for t in r["data"]), r["data"])

# ── 5. Expenses and petty cash (doc §12) ─────────────────────────────────────────
s, r = bf.call("POST", "/expenses", {"expenseDate": iso, "category": "Repairs", "description": "Plumber", "amount": 3000, "paymentMethod": 6,
    "remarks": "Test", "attachmentPath": None, "locationId": 2, "supplierId": None, "purchaseOrderId": None})
exp_id = r["data"]["expenseId"] if s in (200, 201) else None
check("expense created", s in (200, 201), r)
s, r = bf.call("GET", f"/expense-controls?ids={exp_id}")
ctrl = r["data"][0] if s == 200 and r["data"] else {}
check("trigger made it awaiting approval", ctrl.get("approvalStatus") == 1 and ctrl.get("approvalLevel") == 1 and ctrl.get("receiptMissing"), ctrl)
s, r = bf.call("POST", f"/expense-controls/{exp_id}/approve", {"comment": None})
check("cannot approve own expense", s == 422 and "You raised this expense" in err(r), r)
s, r = bf.call("PUT", f"/expense-controls/{exp_id}", {"departmentId": None, "paidFrom": 2, "isReimbursement": False, "reimburseToUserId": None})
check("department/paid-from saved", s == 200 and r["data"]["paidFrom"] == 2, r)
s, r = bf.call("POST", "/petty-cash/top-up", {"amount": 5000, "happenedOn": iso, "bankAccountId": acct_id, "note": None})
check("petty cash top-up", s == 200 and r["data"]["balanceAfter"] == 5000, r)
s, r = bf.call("POST", "/petty-cash/count", {"countedAmount": 4800, "reason": None})
check("count difference needs reason", s == 422 and "Say what happened to the" in err(r), r)
s, r = bf.call("POST", "/petty-cash/count", {"countedAmount": 4800, "reason": "Courier paid, slip lost"})
check("count adjustment", s == 200 and r["data"]["balanceAfter"] == 4800, r)

# ── 6. Period lock (doc §13.3) ───────────────────────────────────────────────────
prev_month = (today.replace(day=1) - dt.timedelta(days=1))
period = prev_month.strftime("%Y-%m")
s, r = bf.call("POST", f"/period-locks/{period}/lock")
check("lock last month", s == 200 and r["data"]["isLocked"], r)
s, r = bf.call("POST", "/expenses", {"expenseDate": prev_month.isoformat(), "category": "Repairs", "description": "Back-dated", "amount": 100, "paymentMethod": 6,
    "remarks": "Test", "attachmentPath": None, "locationId": 2, "supplierId": None, "purchaseOrderId": None})
check("trigger refuses back-dated expense", s == 422 and "is locked" in err(r), r)
s, r = bf.call("POST", f"/period-locks/{period}/unlock", {"reason": "x"})
check("unlock needs a reason", s == 422, r)
s, r = bf.call("POST", f"/period-locks/{period}/unlock", {"reason": "Correcting a missed bill"})
check("unlock with reason", s == 200 and not r["data"]["isLocked"] and r["data"]["unlockReason"], r)

# ── 7. GST, compliance, export ──────────────────────────────────────────────────
twob = {"data": {"docdata": {"b2b": [{"ctin": "29AABCI1234F1Z5", "trdnm": "Someone Else", "inv": [{"inum": "ZZ-1", "dt": today.strftime("%d-%m-%Y"),
        "items": [{"txval": 1000, "cgst": 90, "sgst": 90, "igst": 0}]}]}]}}}
s, r = bf.call("POST", "/gst/gstr2b/upload", {"period": today.strftime("%Y-%m")}, files={"file": ("2b.json", json.dumps(twob).encode(), "application/json")})
check("GSTR-2B compared", s == 200 and r["data"]["missingHere"] == 1, r)
s, r = bf.call("GET", f"/gst/summary?period={today:%Y-%m}")
check("GST summary", s == 200 and "cashToPay" in r["data"], r)
s, r = bf.call("GET", f"/gst/purchase-register?period={today:%Y-%m}")
check("purchase register by rate", s == 200 and isinstance(r["data"]["byRate"], list), r)
s, r = bf.call("GET", "/compliance/calendar")
check("compliance calendar", s == 200 and any(i["name"].startswith("GSTR-3B") for i in r["data"]), r)
s, b = bf.call("GET", f"/export/accounting?from={today.replace(month=4, day=1) if today.month >= 4 else today.replace(year=today.year-1, month=4, day=1)}&to={iso}", raw=True)
check("accounting export xlsx", s == 200 and b[:2] == b"PK", b[:50])
s, b = bf.call("GET", f"/parties/supplier/{sup_id}/ledger/export?format=pdf", raw=True)
check("statement PDF", s == 200 and b[:4] == b"%PDF", b[:20])

print(f"\n{ok} passed, {fail} failed")
sys.exit(1 if fail else 0)

