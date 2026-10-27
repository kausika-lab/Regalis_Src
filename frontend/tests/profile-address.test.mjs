/* Tests for the Profile page's address save and what the Audit Trail records for it.
 *
 *   node --test frontend/tests/profile-address.test.mjs
 *
 * Guards the bug this was written for: changing only Full Name on the Profile page
 * produced an UPDATE audit row with Address changed too. The page split the stored
 * address into boxes and joined them back as "…, Karnataka 560098, India", while the
 * server (ProfileService.ComposeAddress, used by registration and onboarding) stores
 * "…, Karnataka, 560098, India". ProfileService.UpdateAsync copies request.Address as
 * sent, so every save rewrote the column and the audit faithfully reported it.
 *
 * The page chunk imports the whole SPA, so the address helpers are lifted out of the
 * published file's text and evaluated on their own — the test runs the shipped code.
 */
import test from "node:test";
import assert from "node:assert/strict";
import { readFileSync } from "node:fs";

const chunk = readFileSync(new URL("../assets/ProfilePage-DRLk3DkQ.js", import.meta.url), "utf8");
const lift = (name) => {
  const start = chunk.indexOf(`function ${name}(`);
  assert.ok(start >= 0, `${name} not found in the Profile chunk`);
  let depth = 0;
  for (let k = chunk.indexOf("{", start); k < chunk.length; k++) {
    if (chunk[k] === "{") depth++;
    else if (chunk[k] === "}" && --depth === 0) return chunk.slice(start, k + 1);
  }
  throw new Error(`${name} is not balanced`);
};
const { q1: parseAddress, q2: composeAddress, q3: addressToSave } =
  new Function(`${lift("q1")}${lift("q2")}${lift("q3")}return{q1,q2,q3};`)();

/* Regalis.Infrastructure.Services.ProfileService.ComposeAddress. */
const serverCompose = (u) =>
  [u.addressLine1, u.addressLine2, u.city, u.state, u.postalCode, u.country]
    .filter((p) => p != null && p.trim() !== "").join(", ");

/* ProfileService.UpdateAsync writes these four columns from the request, then
   AuditSaveChangesInterceptor lists a property only when it is modified and
   !Equals(OriginalValue, CurrentValue). */
function saveProfile(row, request) {
  const after = {
    ...row,
    FullName: request.fullName.trim(),
    MobileNumber: request.mobileNumber.trim(),
    CompanyName: request.companyName?.trim() ?? null,
    Address: request.address?.trim() ?? null,
  };
  const changed = Object.keys(after).filter((k) => !Object.is(row[k], after[k]));
  return { after, changed };
}

/* What the page does: load the profile into the form, let the member edit, submit. */
function formFor(profile) {
  return { fullName: profile.FullName, mobileNumber: profile.MobileNumber, companyName: profile.CompanyName ?? "", ...parseAddress(profile.Address) };
}
function submit(profile, form) {
  return {
    fullName: form.fullName.trim(),
    mobileNumber: form.mobileNumber.trim(),
    companyName: form.companyName.trim() || null,
    address: addressToSave(form, profile.Address) || null,
  };
}

/* Mirrors user #10032 in the screenshot: address composed by onboarding. */
const PARTS = {
  addressLine1: "217, 8th Cross Rd, BHEL Layout, Pattanagere",
  addressLine2: "Rajarajeshwari Nagar",
  city: "Bengaluru", state: "Karnataka", postalCode: "560098", country: "India",
};
const kausika = () => ({
  UserId: 10032, FullName: "Kausika RS", MobileNumber: "9876543210", CompanyName: null,
  Address: serverCompose(PARTS),
});

test("the stored address is the one in the screenshot's Before column", () => {
  assert.equal(kausika().Address,
    "217, 8th Cross Rd, BHEL Layout, Pattanagere, Rajarajeshwari Nagar, Bengaluru, Karnataka, 560098, India");
});

test("name-only update: only FullName changes, Address is sent back untouched", () => {
  const row = kausika();
  const form = { ...formFor(row), fullName: "Kausika" };
  const request = submit(row, form);
  assert.equal(request.address, row.Address);
  const { after, changed } = saveProfile(row, request);
  assert.deepEqual(changed, ["FullName"]);
  assert.equal(after.FullName, "Kausika");
  assert.equal(after.Address, row.Address);
});

test("address-only update: only Address changes, with exact before and after", () => {
  const row = kausika();
  const form = { ...formFor(row), line2: "RR Nagar" };
  const { after, changed } = saveProfile(row, submit(row, form));
  assert.deepEqual(changed, ["Address"]);
  assert.equal(after.Address, serverCompose({ ...PARTS, addressLine2: "RR Nagar" }));
});

test("no-change update: nothing is recorded as changed", () => {
  const row = kausika();
  const { changed } = saveProfile(row, submit(row, formFor(row)));
  assert.deepEqual(changed, []);
});

test("whitespace typed around unchanged values is not a change", () => {
  const row = kausika();
  const form = { ...formFor(row), fullName: "  Kausika RS ", city: " Bengaluru  " };
  assert.deepEqual(saveProfile(row, submit(row, form)).changed, []);
});

test("an address saved by the old page format is not rewritten by a name-only save", () => {
  const row = { ...kausika(), Address: "217, 8th Cross Rd, BHEL Layout, Pattanagere, Rajarajeshwari Nagar, Bengaluru, Karnataka 560098, India" };
  const form = { ...formFor(row), fullName: "Kausika" };
  assert.deepEqual(saveProfile(row, submit(row, form)).changed, ["FullName"]);
});

test("a free-text legacy address that does not split cleanly is kept as typed", () => {
  const row = { ...kausika(), Address: "No 12 MG Road Bangalore - 560001" };
  const form = { ...formFor(row), fullName: "Kausika" };
  assert.deepEqual(saveProfile(row, submit(row, form)).changed, ["FullName"]);
});

test("no address on file and none entered stays null", () => {
  const row = { ...kausika(), Address: null };
  const request = submit(row, formFor(row));
  assert.equal(request.address, null);
  assert.deepEqual(saveProfile(row, request).changed, []);
});

test("the page composes an address exactly as the server does", () => {
  const form = { line1: PARTS.addressLine1, line2: PARTS.addressLine2, city: PARTS.city, state: PARTS.state, postalCode: PARTS.postalCode, country: PARTS.country };
  assert.equal(composeAddress(form), serverCompose(PARTS));
  assert.equal(composeAddress({ ...form, line2: "", country: "  " }), serverCompose({ ...PARTS, addressLine2: "", country: "  " }));
  assert.equal(composeAddress(parseAddress(serverCompose(PARTS))), serverCompose(PARTS));
});
