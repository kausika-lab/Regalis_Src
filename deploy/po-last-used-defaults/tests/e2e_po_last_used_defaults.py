"""End-to-end API checks for the po-last-used-defaults kit (28 Nov 2026), against the LOCAL Dev stack.

  API  http://127.0.0.1:81  (D:\\Regalis\\local-dev\\api with the po-last-used-defaults DLLs)
  DB   localhost\\SQLEXPRESS2025 / RegalisWorkSpace_Dev, migration 20261128100000_PurchaseShipToName applied

Rules under test
  A  Terms: saving a PO (POST / PUT /api/purchase-orders) with non-blank terms that differ from
     MST_AppSetting 'Purchase.TermsAndConditions' makes them the default form-options returns;
     blank terms never overwrite it; equal terms change nothing; Finance (no SETTING.EDIT) too.
  B  Delivery: the PO's LocationId + DeliveryAddress go to Purchase.LastDeliveryLocationId /
     Purchase.LastDeliveryAddress (created on first save, written only when changed, never blank)
     and come back from form-options as lastDeliveryLocationId / lastDeliveryAddress.
  C  Ship-to name: Purchase.ShipToName ('Rupesh') is returned as shipToName and printed in the PDF
     "Shipping to" box in place of the location name; blank -> location name again. MST_Location
     keeps its name. Also: PDF label "Shipping to" and header email info@regalis.co.in.

Creates a Finance staff user po-lud-fin-<run>@example.test and draft POs for supplier 1 / location 2.
Clean-up, whatever happens: test POs cancelled through the API then soft-deleted (their PO numbers
stay used), the Finance user soft-deleted, and every Purchase.* setting the test touches put back
exactly as found at the start (value and ModifiedDate / ModifiedBy; rows that did not exist are
deleted). The read cache is evicted through PUT /api/settings before the SQL restore.

Env: REGALIS_ADMIN_EMAIL / REGALIS_ADMIN_PASSWORD; PDFLIB = folder holding pypdf (optional if
pypdf is installed).

usage: python -I e2e_po_last_used_defaults.py
"""
import io, json, os, random, subprocess, sys, time, urllib.request, urllib.error

API = os.environ.get("REGALIS_API", "http://127.0.0.1:81/api")
SQL = ["sqlcmd", "-S", r"localhost\SQLEXPRESS2025", "-E", "-C", "-d", "RegalisWorkSpace_Dev", "-I", "-h", "-1", "-W", "-s", "|"]
RUN = str(random.randint(10000, 99999))
TERMS, LOCK, ADDR, SHIP = "Purchase.TermsAndConditions", "Purchase.LastDeliveryLocationId", "Purchase.LastDeliveryAddress", "Purchase.ShipToName"
KEYS = [TERMS, LOCK, ADDR, SHIP]
SUPPLIER, LOC = 1, 2
results = []


def check(name, ok, detail=""):
    results.append((name, bool(ok)))
    print(("PASS " if ok else "FAIL ") + name + (f"  -- {str(detail)[:600]}" if detail and not ok else ""), flush=True)


def sql(q):
    out = subprocess.run(SQL + ["-b", "-Q", "SET NOCOUNT ON;" + q], capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed: " + q[:200] + " :: " + out.stdout + out.stderr)
    return [l.split("|") for l in out.stdout.strip().splitlines() if l.strip()]


def one(q):
    r = sql(q)
    return r[0][0] if r else None


def value_hex(key):
    """The whole value as UTF-16LE hex. -W cannot be combined with -y 0, and without -y 0 sqlcmd
    cuts variable-length columns at 256 characters, so this read has its own call."""
    cmd = [a for a in SQL if a not in ("-W", "-s", "|", "-h", "-1")] + ["-y", "0", "-b", "-Q",
           f"SET NOCOUNT ON; SELECT 'HEX:' + CONVERT(varchar(max), CAST(SettingValue AS varbinary(max)), 2) FROM MST_AppSetting WHERE SettingKey = '{key}'"]
    out = subprocess.run(cmd, capture_output=True, text=True)
    if out.returncode != 0:
        raise SystemExit("SQL failed (value_hex): " + out.stdout + out.stderr)
    rows = [l.strip() for l in out.stdout.splitlines() if l.strip().startswith("HEX:")]
    return rows[0][4:] if rows else None


def snap(key):
    """Exact state of one setting row: id, value as UTF-16LE hex, ModifiedDate, ModifiedBy; None if missing."""
    r = sql(f"SELECT AppSettingId, ISNULL(CONVERT(varchar(30), ModifiedDate, 126), 'NULL'), ISNULL(CAST(ModifiedBy AS varchar(20)), 'NULL')"
            f" FROM MST_AppSetting WHERE SettingKey = '{key}'")
    if not r:
        return None
    return [r[0][0], value_hex(key) or "", r[0][1], r[0][2]]


def val(key):
    s = snap(key)
    return None if s is None else bytes.fromhex(s[1] or "").decode("utf-16-le")


def meta(key):
    s = snap(key)
    return None if s is None else (s[0], s[2], s[3])


CREDS = {}


def _raw(method, path, token, body):
    data = json.dumps(body).encode() if body is not None else None
    req = urllib.request.Request(API + path, data=data, method=method)
    req.add_header("Content-Type", "application/json")
    if token:
        req.add_header("Authorization", "Bearer " + token)
    try:
        with urllib.request.urlopen(req, timeout=90) as r:
            return r.status, r.read()
    except urllib.error.HTTPError as e:
        return e.code, e.read()


def call(method, path, token=None, body=None):
    for _ in range(8):
        s, raw = _raw(method, path, token, body)
        if s == 401 and token in CREDS:
            token = login(*CREDS[token])
            continue
        if s == 429:
            time.sleep(15)
            continue
        try:
            return s, json.loads(raw or b"{}")
        except Exception:
            return s, {"_raw": raw}
    return s, {}


def login(email_, password, portal="admin"):
    for _ in range(6):
        s, raw = _raw("POST", "/auth/login", None, {"email": email_, "password": password, "portal": portal})
        if s == 200:
            tok = json.loads(raw)["data"]["accessToken"]
            CREDS[tok] = (email_, password, portal)
            return tok
        if s == 429:
            time.sleep(20)
            continue
        raise SystemExit(f"login {email_} failed: {s} {raw[:300]}")
    raise SystemExit("login rate limited")


def opts(token):
    s, b = call("GET", "/purchase-orders/form-options", token)
    return b["data"] if s == 200 else {"_status": s}


def po_body(terms="__omit__", supplier=SUPPLIER, address="__omit__"):
    body = {"supplierId": supplier, "locationId": LOC, "remarks": f"po-last-used-defaults e2e {RUN}",
            "lines": [{"itemName": f"E2E defaults item {RUN}", "unit": "Nos", "quantity": 1, "rate": 100,
                       "discountAmount": 0, "taxPercent": 18}]}
    if terms != "__omit__":
        body["termsAndConditions"] = terms
    if address != "__omit__":
        body["deliveryAddress"] = address
    return body


CREATED = []


def create_po(token, terms="__omit__", address="__omit__"):
    s, b = call("POST", "/purchase-orders", token, po_body(terms, address=address))
    if s in (200, 201):
        CREATED.append(b["data"]["purchaseOrderId"])
    return s, b


def audit_count(action, entity="PurchaseOrder", entity_id=None):
    w = f"Action = '{action}' AND EntityName = '{entity}'" + (f" AND EntityId = '{entity_id}'" if entity_id is not None else "")
    return int(one(f"SELECT COUNT(*) FROM TRN_AuditLog WHERE {w}"))


def put_setting(token, key, value):
    return call("PUT", "/settings", token, {"settings": [{"settingKey": key, "settingValue": value}]})


def pdf_text(token, po_id):
    s, raw = _raw("GET", f"/purchase-orders/{po_id}/pdf", token, None)
    if s != 200 or raw[:4] != b"%PDF":
        return s, ""
    return s, "".join(p.extract_text() or "" for p in pypdf.PdfReader(io.BytesIO(raw)).pages)


sys.path.insert(0, os.environ.get("PDFLIB", ""))
import pypdf  # noqa: E402

# ---------------------------------------------------------------- set-up
FOUND = {k: snap(k) for k in KEYS}
for k, f in FOUND.items():
    # Safety: the restore writes this hex back, so it must hold the whole value.
    if f is not None and len(f[1]) // 4 != int(one(f"SELECT DATALENGTH(SettingValue) / 2 FROM MST_AppSetting WHERE SettingKey = '{k}'")):
        raise SystemExit(f"could not read {k} whole; refusing to run (the restore would truncate it)")
if FOUND[TERMS] is None or FOUND[SHIP] is None:
    raise SystemExit(f"{TERMS} / {SHIP} missing on Dev; apply 20261128100000_PurchaseShipToName.sql first")
ORIG_TERMS = val(TERMS)
print("as found: " + ", ".join(f"{k}={'missing' if v is None else 'id ' + v[0] + ' modified ' + v[2]}" for k, v in FOUND.items()))

admin = login(os.environ["REGALIS_ADMIN_EMAIL"], os.environ["REGALIS_ADMIN_PASSWORD"])
ADMIN_ID = one(f"SELECT UserId FROM MST_User WHERE Email = '{os.environ['REGALIS_ADMIN_EMAIL']}'")
LOC_NAME = one(f"SELECT LocationName FROM MST_Location WHERE LocationId = {LOC}")
FIN_PW = "PoLud#E2e" + RUN + "x"
FIN_EMAIL = f"po-lud-fin-{RUN}@example.test"
role = one("SELECT RoleId FROM MST_Role WHERE RoleCode = 'FINANCE'")
s, b = call("POST", "/users", admin, {"fullName": f"PO Defaults Finance {RUN}", "email": FIN_EMAIL, "mobileNumber": f"+9194{RUN}{random.randint(100, 999)}",
                                       "password": FIN_PW, "userType": 3, "roleIds": [int(role)], "defaultLocationId": LOC})
if s not in (200, 201):
    raise SystemExit(f"create finance user: {s} {b}")
FIN_ID = str(b["data"]["userId"])
sql(f"UPDATE MST_User SET MustChangePassword = 0, Status = 1, IsEmailVerified = 1, IsMobileVerified = 1 WHERE UserId = {FIN_ID}")

HIDDEN = TERMS + ".e2e-hidden-" + RUN
try:
    finance = login(FIN_EMAIL, FIN_PW)
    t_a = f"A{RUN}. Goods to the delivery address by the delivery date.\n2. Invoice must quote the PO number.\n3. Net 30 days."
    t_b = f"B{RUN}. Edited on update.\n2. Payment within 15 days of a correct invoice."
    t_c = f"C{RUN}. Saved by Finance.\n2. Returns at the supplier's cost."
    t_d = f"D{RUN}. Finance edit."
    a_1 = f"E2E dock {RUN}, 12 Test Street, Bengaluru 560098"
    a_2 = f"E2E store room {RUN}, 3rd floor, Bengaluru"
    a_f = f"E2E Finance gate {RUN}, Bengaluru"

    # ------------------------------------------------ 0. premise and starting point
    o = opts(admin)
    check("0.1 form-options returns the stored terms default", o.get("defaultTermsAndConditions") == ORIG_TERMS, o.get("defaultTermsAndConditions"))
    check("0.2 form-options returns shipToName = the stored Purchase.ShipToName", o.get("shipToName") == ((val(SHIP) or "").strip() or None), o.get("shipToName"))
    s, _ = put_setting(finance, TERMS, "x")
    check("0.3 Finance cannot use PUT /api/settings (403) - why the change is in the backend", s == 403, s)
    check("0.4 the refused settings call changed nothing", val(TERMS) == ORIG_TERMS)

    # ------------------------------------------------ A1. create with edited terms
    upd_before = audit_count("UPDATE", "AppSetting", FOUND[TERMS][0])
    s, b = create_po(admin, t_a, a_1)
    po1 = b["data"]["purchaseOrderId"] if s in (200, 201) else None
    check("A1.1 admin creates a draft PO with edited terms and address", s in (200, 201) and b["data"]["termsAndConditions"] == t_a, f"{s} {b}")
    check("A1.2 MST_AppSetting holds exactly the PO's terms", val(TERMS) == t_a, repr(val(TERMS)))
    o = opts(admin)
    check("A1.3 form-options returns them at once (read cache evicted)", o.get("defaultTermsAndConditions") == t_a, o.get("defaultTermsAndConditions"))
    m = meta(TERMS)
    check("A1.4 same row (id unchanged), ModifiedBy = admin, ModifiedDate set", m[0] == FOUND[TERMS][0] and m[2] == ADMIN_ID and m[1] != "NULL", m)
    check("A1.5 named audit row PO_DEFAULT_TERMS_UPDATED", audit_count("PO_DEFAULT_TERMS_UPDATED", entity_id=po1) == 1)
    check("A1.6 interceptor wrote the AppSetting UPDATE audit row", audit_count("UPDATE", "AppSetting", FOUND[TERMS][0]) == upd_before + 1)

    # ------------------------------------------------ B1. delivery address from the same create
    check("B1.1 Purchase.LastDeliveryLocationId = the PO's location", val(LOCK) == str(LOC), val(LOCK))
    check("B1.2 Purchase.LastDeliveryAddress = the PO's address exactly", val(ADDR) == a_1, repr(val(ADDR)))
    r = sql(f"SELECT SettingKey, SettingGroup, DataType, IsEditableByAdmin, IsSecret FROM MST_AppSetting WHERE SettingKey IN ('{LOCK}', '{ADDR}') ORDER BY SettingKey")
    check("B1.3 rows are group Billing (int / string), editable, not secret",
          r == [[ADDR, "Billing", "string", "1", "0"], [LOCK, "Billing", "int", "1", "0"]], r)
    check("B1.4 form-options returns lastDeliveryLocationId / lastDeliveryAddress",
          o.get("lastDeliveryLocationId") == LOC and o.get("lastDeliveryAddress") == a_1, (o.get("lastDeliveryLocationId"), o.get("lastDeliveryAddress")))
    check("B1.5 named audit row PO_DEFAULT_DELIVERY_UPDATED", audit_count("PO_DEFAULT_DELIVERY_UPDATED", entity_id=po1) == 1)

    s, b = create_po(admin)  # next new PO: terms and address not sent -> service defaults
    po2 = b["data"]["purchaseOrderId"] if s in (200, 201) else None
    check("A1.7 next new PO (terms not sent) gets the new default terms", s in (200, 201) and b["data"]["termsAndConditions"] == t_a, f"{s} {b}")
    check("A1.8 a PO saved with the default terms writes no PO_DEFAULT_TERMS_UPDATED row", audit_count("PO_DEFAULT_TERMS_UPDATED", entity_id=po2) == 0)
    loc_addr = b["data"]["deliveryAddress"] if s in (200, 201) else None
    check("B1.6 a PO sent without an address gets the location's address (existing behaviour) and that becomes the last address",
          bool(loc_addr) and val(ADDR) == loc_addr and val(LOCK) == str(LOC), (loc_addr, val(ADDR)))

    # ------------------------------------------------ A2 / B2. update
    s, b = call("PUT", f"/purchase-orders/{po1}", admin, po_body(t_b, address=a_2))
    check("A2.1 update the draft PO's terms and address", s == 200 and b["data"]["termsAndConditions"] == t_b and b["data"]["deliveryAddress"] == a_2, f"{s} {b}")
    o = opts(admin)
    check("A2.2 terms default follows the update (DB + form-options)", val(TERMS) == t_b and o.get("defaultTermsAndConditions") == t_b)
    check("B2.1 last address follows the update (DB + form-options)", val(ADDR) == a_2 and o.get("lastDeliveryAddress") == a_2, (val(ADDR), o.get("lastDeliveryAddress")))

    # ------------------------------------------------ A3 / B3. blank never overwrites
    m_t, m_a, m_l = meta(TERMS), meta(ADDR), meta(LOCK)
    s, b = call("PUT", f"/purchase-orders/{po2}", admin, po_body("   \n  ", address="   "))
    check("A3.1 draft saved with whitespace-only terms (PO terms empty)", s == 200 and not b["data"]["termsAndConditions"], f"{s} {b}")
    check("A3.2 terms default unchanged after the whitespace save", val(TERMS) == t_b and meta(TERMS) == m_t, meta(TERMS))
    check("B3.1 blank address on the request: the PO gets the location address; the last address is never blank",
          s == 200 and bool(b["data"]["deliveryAddress"]) and bool((val(ADDR) or "").strip()), (b.get("data", {}).get("deliveryAddress"), val(ADDR)))
    s, b = create_po(admin, "")
    check("A3.3 new draft with empty terms is saved", s in (200, 201) and not b["data"]["termsAndConditions"], f"{s} {b}")
    check("A3.4 terms default unchanged after the empty-terms create", val(TERMS) == t_b and meta(TERMS) == m_t)
    check("A3.5 no setting row has an empty value", int(one(f"SELECT COUNT(*) FROM MST_AppSetting WHERE SettingKey IN ('{TERMS}','{LOCK}','{ADDR}') AND LTRIM(RTRIM(SettingValue)) = ''")) == 0)

    # ------------------------------------------------ A4 / B4. same values: no change
    s, b = call("PUT", f"/purchase-orders/{po1}", admin, po_body(t_b, address=a_2))  # puts address a_2 back as last
    m_t, m_a, m_l = meta(TERMS), meta(ADDR), meta(LOCK)
    upd_t, upd_a = audit_count("UPDATE", "AppSetting", m_t[0]), audit_count("UPDATE", "AppSetting", m_a[0])
    dlv = audit_count("PO_DEFAULT_DELIVERY_UPDATED", entity_id=po1)
    time.sleep(1.1)
    s, b = call("PUT", f"/purchase-orders/{po1}", admin, po_body(t_b, address=a_2))
    check("A4.1 PO re-saved with terms and address equal to the stored ones", s == 200, f"{s} {b}")
    check("A4.2 terms row untouched (ModifiedDate / ModifiedBy unchanged)", meta(TERMS) == m_t, (m_t, meta(TERMS)))
    check("B4.1 address + location rows untouched", meta(ADDR) == m_a and meta(LOCK) == m_l, (m_a, meta(ADDR), m_l, meta(LOCK)))
    check("A4.3 no AppSetting audit row and no named rows for the no-op save",
          audit_count("UPDATE", "AppSetting", m_t[0]) == upd_t and audit_count("UPDATE", "AppSetting", m_a[0]) == upd_a
          and audit_count("PO_DEFAULT_TERMS_UPDATED", entity_id=po1) == 2 and audit_count("PO_DEFAULT_DELIVERY_UPDATED", entity_id=po1) == dlv)

    # ------------------------------------------------ A5 / B5. Finance (no SETTING.EDIT)
    s, b = create_po(finance, t_c, a_f)
    po_f = b["data"]["purchaseOrderId"] if s in (200, 201) else None
    check("A5.1 Finance creates a PO with edited terms and address", s in (200, 201), f"{s} {b}")
    o = opts(finance)
    check("A5.2 terms default is now Finance's terms", val(TERMS) == t_c and o.get("defaultTermsAndConditions") == t_c, repr(val(TERMS)))
    check("B5.1 last address is now Finance's address", val(ADDR) == a_f and o.get("lastDeliveryAddress") == a_f, repr(val(ADDR)))
    check("A5.3 ModifiedBy = the Finance user", meta(TERMS)[2] == FIN_ID and meta(ADDR)[2] == FIN_ID, (meta(TERMS), meta(ADDR)))
    s, b = call("PUT", f"/purchase-orders/{po_f}", finance, po_body(t_d, address=a_f))
    check("A5.4 Finance updates the PO's terms; default follows", s == 200 and val(TERMS) == t_d and opts(admin).get("defaultTermsAndConditions") == t_d, f"{s} {repr(val(TERMS))}")

    # ------------------------------------------------ A6. exact text
    s, b = create_po(admin, f"  \n  E{RUN} trimmed terms\n\n", f"  F{RUN} trimmed address  ")
    check("A6.1 surrounding whitespace trimmed on the PO (existing behaviour)", s in (200, 201) and b["data"]["termsAndConditions"] == f"E{RUN} trimmed terms", f"{s} {b}")
    check("A6.2 defaults equal the PO's stored text exactly", val(TERMS) == f"E{RUN} trimmed terms" and val(ADDR) == b["data"]["deliveryAddress"], (repr(val(TERMS)), repr(val(ADDR))))
    long_t = f"L{RUN} " + ("x" * 3990)
    s, b = create_po(admin, long_t)
    check("A6.3 a 4000-character terms text is stored whole", s in (200, 201) and val(TERMS) == long_t, f"{s} {len(val(TERMS) or '')}")

    # ------------------------------------------------ A7. failed saves leave the defaults alone
    m_t, m_a = meta(TERMS), meta(ADDR)
    s, b = call("PUT", f"/purchase-orders/{po1}", admin, po_body(f"F{RUN} never saved", supplier=987654, address=f"never {RUN}"))
    check("A7.1 PUT with an unknown supplier is refused", s == 404, s)
    check("A7.2 defaults unchanged after the refused update", meta(TERMS) == m_t and meta(ADDR) == m_a and val(TERMS) == long_t)
    s, b = call("POST", "/purchase-orders", admin, {**po_body(f"G{RUN} never saved", address=f"never {RUN}"), "lines": []})
    check("A7.3 POST with no lines is refused", s in (400, 422), f"{s} {b}")
    check("A7.4 defaults unchanged after the refused create", meta(TERMS) == m_t and meta(ADDR) == m_a)
    frozen = one("SELECT TOP 1 PurchaseOrderId FROM TRN_PurchaseOrder WHERE Status <> 1 AND IsDeleted = 0 ORDER BY PurchaseOrderId")
    if frozen:
        s, b = call("PUT", f"/purchase-orders/{frozen}", admin, po_body(f"H{RUN} never saved", address=f"never {RUN}"))
        check("A7.5 editing a non-draft PO is still refused; defaults unchanged", s in (400, 409, 422) and val(TERMS) == long_t and meta(ADDR) == m_a, f"{s} {b}")

    # ------------------------------------------------ A8. missing terms row is created like the migration does
    sql(f"UPDATE MST_AppSetting SET SettingKey = '{HIDDEN}' WHERE SettingKey = '{TERMS}'")
    try:
        s, b = create_po(admin, t_a)
        r = sql(f"SELECT SettingGroup, DataType, IsEditableByAdmin, IsSecret, IsActive, IsDeleted, CAST(CreatedBy AS varchar(20)) FROM MST_AppSetting WHERE SettingKey = '{TERMS}'")
        check("A8.1 PO saved while the terms row was missing", s in (200, 201), f"{s} {b}")
        check("A8.2 row created: Billing / string / editable / not secret / active, CreatedBy = admin",
              r and r[0] == ["Billing", "string", "1", "0", "1", "0", ADMIN_ID], r)
        check("A8.3 created row holds the PO's terms", val(TERMS) == t_a)
    finally:
        sql(f"DELETE FROM MST_AppSetting WHERE SettingKey = '{TERMS}' AND AppSettingId <> {FOUND[TERMS][0]};"
            f" UPDATE MST_AppSetting SET SettingKey = '{TERMS}' WHERE SettingKey = '{HIDDEN}'")

    # ------------------------------------------------ B6. last location no longer active
    s, _ = put_setting(admin, LOCK, "987654")  # an id that is not an active location (evicts the cache)
    o = opts(admin)
    check("B6.1 form-options passes the stored id through (the screen checks it against active locations)",
          s == 200 and o.get("lastDeliveryLocationId") == 987654, (s, o.get("lastDeliveryLocationId")))
    s, b = create_po(admin, address=f"B6 {RUN} back to a real location")
    check("B6.2 the next PO save puts a real location back", s in (200, 201) and val(LOCK) == str(LOC), val(LOCK))

    # ------------------------------------------------ C. ship-to name, PDF
    pdf_po = one("SELECT TOP 1 PurchaseOrderId FROM TRN_PurchaseOrder WHERE IsDeleted = 0 AND ISNULL(Remarks, '') NOT LIKE 'po-last-used-defaults e2e%' ORDER BY PurchaseOrderId DESC") \
        or (CREATED[0] if CREATED else None)
    ship = (val(SHIP) or "").strip()
    s, text = pdf_text(admin, pdf_po)
    check(f"C1 PDF of existing PO {pdf_po} downloads", s == 200 and text, s)
    check("C2 PDF box reads 'Shipping to', not 'Deliver to'", "Shipping to" in text and "Deliver to" not in text, text[:400])
    check("C3 PDF header email is info@regalis.co.in", "info@regalis.co.in" in text, text[:400])
    check(f"C4 PDF prints the ship-to name '{ship}' and not the location name '{LOC_NAME}'", ship and ship in text and LOC_NAME not in text, text[:600])
    s, _ = put_setting(admin, SHIP, "")
    o = opts(admin)
    check("C5 ship-to name blanked through the Settings API", s == 200 and o.get("shipToName") is None, (s, o.get("shipToName")))
    s, text = pdf_text(admin, pdf_po)
    check(f"C6 blank ship-to name: PDF prints the location name '{LOC_NAME}' again", LOC_NAME in text, text[:600])
    s, _ = put_setting(admin, SHIP, ship)
    check("C7 ship-to name put back", s == 200 and opts(admin).get("shipToName") == ship)
    s, b = call("GET", "/locations?page=1&pageSize=100", admin)
    names = {l["locationId"]: l["locationName"] for l in (b.get("data") or {}).get("items", [])}
    check(f"C8 the location itself is still named '{LOC_NAME}' (Locations API)", names.get(LOC) == LOC_NAME == "RR Nagar", names)
    s, b = call("GET", f"/purchase-orders/{CREATED[0]}", admin)
    check("C9 PO detail API still carries the real locationName (screens swap it)", s == 200 and b["data"].get("locationName") == LOC_NAME, b.get("data", {}).get("locationName"))

finally:
    # ------------------------------------------------ clean-up: Dev as found
    for pid in CREATED:
        call("POST", f"/purchase-orders/{pid}/cancel", admin, {"reason": "po-last-used-defaults e2e clean-up"})
    if CREATED:
        sql(f"UPDATE TRN_PurchaseOrder SET IsDeleted = 1, IsActive = 0, DeletedDate = SYSUTCDATETIME() WHERE PurchaseOrderId IN ({','.join(map(str, CREATED))})")
    sql(f"IF EXISTS (SELECT 1 FROM MST_AppSetting WHERE SettingKey = '{HIDDEN}') BEGIN"
        f" DELETE FROM MST_AppSetting WHERE SettingKey = '{TERMS}' AND AppSettingId <> {FOUND[TERMS][0]};"
        f" UPDATE MST_AppSetting SET SettingKey = '{TERMS}' WHERE SettingKey = '{HIDDEN}' END")
    for k, f in FOUND.items():
        if f is None:
            if snap(k) is not None:
                put_setting(admin, k, "0" if k == LOCK else "")  # evicts the read cache before the row goes
            sql(f"DELETE FROM MST_AppSetting WHERE SettingKey = '{k}'")
            continue
        put_setting(admin, k, bytes.fromhex(f[1] or "").decode("utf-16-le"))  # evicts the read cache
        md = "NULL" if f[2] == "NULL" else f"'{f[2]}'"
        sql(f"UPDATE MST_AppSetting SET SettingValue = CAST(0x{f[1]} AS nvarchar(max)), ModifiedDate = {md}, ModifiedBy = {f[3]} WHERE SettingKey = '{k}'")
    sql(f"UPDATE MST_User SET IsDeleted = 1, IsActive = 0, Status = 2 WHERE UserId = {FIN_ID}")
    check("Z.1 all four settings back exactly as found", all(snap(k) == FOUND[k] for k in KEYS), {k: snap(k) for k in KEYS})
    check("Z.2 test POs cancelled and soft-deleted", not CREATED or int(one(
        f"SELECT COUNT(*) FROM TRN_PurchaseOrder WHERE PurchaseOrderId IN ({','.join(map(str, CREATED))}) AND (IsDeleted = 0 OR Status <> 5)")) == 0)

passed = sum(1 for _, ok in results if ok)
print(f"\n{passed}/{len(results)} passed")
sys.exit(0 if passed == len(results) else 1)
