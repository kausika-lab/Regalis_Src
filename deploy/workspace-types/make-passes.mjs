// Builds PassesPage-WT1.js from PassesPage-CxAkHUg-.js (14 Oct 2026).
//   node make-passes.mjs <frontend dir> <out dir>
// The Day pass "is a desk free?" check read the Desk type's slots. Hot desks now have a
// type of their own, so it reads Desk and Hot Desk together: free if either has a free
// slot, closed if neither has slots. The same nine desks as before, the same answer.
import fs from "fs";
import path from "path";

const [front, out] = process.argv.slice(2);
if (!front || !out) throw new Error("usage: node make-passes.mjs <frontend dir> <out dir>");
let s = fs.readFileSync(path.join(front, "assets", "PassesPage-CxAkHUg-.js"), "utf8");
const swap = (from, to, what) => {
  const n = s.split(from).length - 1;
  if (n !== 1) throw new Error(`${what}: expected 1 match, found ${n}`);
  s = s.replace(from, to);
};
swap(`const J=Q.find(oe=>String(oe.typeCode).toUpperCase()==="DESK");RGsetDesk(J?J.workspaceTypeId:null)`,
  `const J=Q.filter(oe=>["DESK","HOT_DESK"].includes(String(oe.typeCode).toUpperCase())).map(oe=>oe.workspaceTypeId);RGsetDesk(J.length?J.join(","):null)`,
  "desk type ids");
swap(`RGavail.slots(u.locationId,RGdesk,K).then(M=>{if(dead)return;const Q=Array.isArray(M)?M:[];`,
  `Promise.all(String(RGdesk).split(",").map(id=>RGavail.slots(u.locationId,Number(id),K))).then(Ms=>{if(dead)return;const Q=Ms.flatMap(M=>Array.isArray(M)?M:[]);`,
  "slot check");
fs.mkdirSync(path.join(out, "assets"), { recursive: true });
fs.writeFileSync(path.join(out, "assets", "PassesPage-WT1.js"), s, "utf8");
console.log("OK  - PassesPage-WT1.js");
