/**
 * Public "Book a tour" page — /enquire (19 Nov 2026, kit deploy/crm, document sections 6 and 18).
 * Open to everyone, signed in or not. Three ways to hand over details: the enquiry form, the
 * "Busy? We will call you" bar, and the WhatsApp button (its message carries a W- reference that
 * staff type into Add lead). /enquire?kind=quote&looking=Cabin opens it as "Send me a quote".
 *
 * Posts to /api/public/enquiries (no login). The visitor id comes from /crm-track.js.
 */
import { r as React, j as X, E as http, D as unwrap, A as ApiError, g as Link } from "./index-DI5X4c9f.js";

function el(type, props, ...kids) {
  const p = props ? { ...props } : {};
  let key;
  if (p.key !== void 0) {
    key = p.key;
    delete p.key;
  }
  const flat = kids.flat(Infinity).filter((k) => k !== null && k !== void 0 && k !== false && k !== "");
  if (flat.length === 1) p.children = flat[0];
  else if (flat.length > 1) p.children = flat;
  return flat.length > 1 ? X.jsxs(type, p, key) : X.jsx(type, p, key);
}

const CSS = `
.enq{min-height:100vh;background:var(--surface-page);color:var(--text-primary);font-family:var(--font-sans)}
.enq__top{display:flex;justify-content:space-between;align-items:center;padding:14px 16px;max-width:1040px;margin:0 auto}
.enq__top img{height:38px}
.enq__wrap{max-width:1040px;margin:0 auto;padding:8px 16px 96px;display:grid;grid-template-columns:1fr 1.1fr;gap:28px;align-items:start}
@media (max-width:860px){.enq__wrap{grid-template-columns:1fr}}
.enq h1{font-family:var(--font-display);font-size:clamp(1.7rem,4vw,2.4rem);line-height:1.15;margin:12px 0}
.enq__lead{color:var(--text-secondary);font-size:1.05rem;line-height:1.55}
.enq__card{background:var(--surface-card);border:1px solid var(--border-subtle);border-radius:16px;padding:22px;box-shadow:var(--shadow-md)}
.enq__grid{display:grid;grid-template-columns:1fr 1fr;gap:12px}
@media (max-width:520px){.enq__grid{grid-template-columns:1fr}}
.enq label.f{display:flex;flex-direction:column;gap:4px;font-size:.875rem;font-weight:600}
.enq input,.enq select,.enq textarea{font:inherit;font-weight:400;padding:10px 12px;border:1px solid var(--border-strong);border-radius:10px;background:var(--surface-card);color:var(--text-primary);width:100%;box-sizing:border-box}
.enq textarea{min-height:84px}
.enq .tick{display:flex;gap:10px;align-items:flex-start;font-size:.875rem;line-height:1.45;margin-top:12px}
.enq .tick input{width:18px;height:18px;margin-top:2px;flex:0 0 auto}
.enq__btn{width:100%;margin-top:16px;padding:13px;border:0;border-radius:12px;background:var(--accent);color:var(--text-on-gold);font:700 1rem var(--font-sans);cursor:pointer}
.enq__btn[disabled]{opacity:.55;cursor:not-allowed}
.enq__err{color:var(--color-danger);font-size:.875rem;margin-top:10px}
.enq__ok{background:rgb(22 163 74 / 10%);border:1px solid rgb(22 163 74 / 40%);border-radius:12px;padding:16px;line-height:1.5}
.enq__bar{margin-top:18px;display:flex;gap:8px;flex-wrap:wrap;align-items:center;background:var(--surface-inset);border-radius:12px;padding:12px}
.enq__bar b{flex:1 1 100%}
.enq__bar input{flex:1 1 160px;width:auto}
.enq__bar button{padding:10px 16px;border-radius:10px;border:1px solid var(--accent);background:transparent;color:var(--text-primary);font:700 .9rem var(--font-sans);cursor:pointer}
.enq__wa{position:fixed;right:16px;bottom:16px;z-index:50;display:flex;align-items:center;gap:8px;background:#25d366;color:#0b0b0d;border-radius:999px;padding:12px 18px;font:700 .95rem var(--font-sans);box-shadow:0 8px 24px rgb(0 0 0 / 25%);border:0;cursor:pointer}
.enq__hp{position:absolute;left:-10000px;width:1px;height:1px;overflow:hidden}
.enq__points{list-style:none;padding:0;margin:18px 0;display:grid;gap:10px}
.enq__points li{padding-left:26px;position:relative;line-height:1.45}
.enq__points li:before{content:"✓";position:absolute;left:0;color:var(--accent-strong);font-weight:800}
.enq__small{font-size:.8rem;color:var(--text-muted);margin-top:10px;line-height:1.45}
`;

const visitorKey = () => {
  try {
    if (window.RegalisCrm?.visitorKey) return window.RegalisCrm.visitorKey();
    const m = document.cookie.match(/(?:^|; )rg_vid=([^;]+)/);
    return m ? decodeURIComponent(m[1]) : null;
  } catch {
    return null;
  }
};
const utm = () => {
  try {
    return JSON.parse(sessionStorage.getItem("rg-utm") || "{}");
  } catch {
    return {};
  }
};
const msg = (e) => (e instanceof ApiError && e.status && e.status < 500 ? e.message : "Something went wrong at our end. Please call us — we would rather you reached us than tried again.");

export function CrmEnquirePage() {
  React.useEffect(() => {
    if (!document.getElementById("rg-enq-css")) {
      const s = document.createElement("style");
      s.id = "rg-enq-css";
      s.textContent = CSS;
      document.head.appendChild(s);
    }
    document.title = "Book a tour — Regalis WorkSpace";
  }, []);
  const q = new URLSearchParams(window.location.search);
  const kindQ = q.get("kind") === "quote" ? "quote" : "form";
  const [cfg, setCfg] = React.useState(null);
  const shownAt = React.useRef(Date.now());
  const [f, setF] = React.useState({ name: "", mobile: "", email: "", lookingForId: "", people: "", fromWhen: "", message: "", consent: false, marketing: false, website: "" });
  const [cb, setCb] = React.useState({ mobile: "", consent: false });
  const [busy, setBusy] = React.useState("");
  const [err, setErr] = React.useState("");
  const [cbErr, setCbErr] = React.useState("");
  const [done, setDone] = React.useState("");
  const set = (p) => setF((x) => ({ ...x, ...p }));

  React.useEffect(() => {
    unwrap(http.get("/public/crm-config"))
      .then((c) => {
        setCfg(c);
        const want = (q.get("looking") || "").toLowerCase();
        const hit = c.lookingFor.find((l) => l.name.toLowerCase() === want);
        if (hit) set({ lookingForId: String(hit.id) });
      })
      .catch(() => setCfg({ consentText: "I agree Regalis WorkSpace may contact me about workspace availability.", marketingText: "", lookingFor: [] }));
  }, []);

  const send = async (kind, body, onErr) => {
    setBusy(kind);
    onErr("");
    try {
      await unwrap(
        http.post("/public/enquiries", {
          kind,
          ...body,
          visitorKey: visitorKey(),
          pagePath: window.location.pathname,
          referrer: document.referrer || null,
          ...utm(),
          website: f.website,
          elapsedMs: Date.now() - shownAt.current
        })
      );
      setDone(kind === "callback" ? "callback" : "form");
    } catch (e) {
      onErr(msg(e));
    } finally {
      setBusy("");
    }
  };

  const submit = (e) => {
    e.preventDefault();
    if (!f.name.trim()) return setErr("Please tell us your name.");
    if ((f.mobile.match(/\d/g) || []).length < 10) return setErr("Please give a mobile number so we can call you back.");
    if (!f.consent) return setErr("Please tick the box so we know we may contact you.");
    send(kindQ, { name: f.name, mobile: f.mobile, email: f.email || null, lookingForId: f.lookingForId ? Number(f.lookingForId) : null, people: f.people ? Number(f.people) : null, fromWhen: f.fromWhen || null, message: f.message || null, contactConsent: true, marketingConsent: f.marketing }, setErr);
  };
  const callback = (e) => {
    e.preventDefault();
    if ((cb.mobile.match(/\d/g) || []).length < 10) return setCbErr("Please give a mobile number so we can call you back.");
    if (!cb.consent) return setCbErr("Please tick the box so we know we may contact you.");
    send("callback", { mobile: cb.mobile, contactConsent: true }, setCbErr);
  };
  const whatsapp = async () => {
    // Open the tab inside the click (pop-up blockers), then point it at WhatsApp with the reference.
    const tab = window.open("about:blank", "_blank");
    try {
      const r = await unwrap(http.post("/public/whatsapp-ref", { visitorKey: visitorKey(), path: window.location.pathname }));
      if (r.url && tab) tab.location.href = r.url;
      else if (r.url) window.location.href = r.url;
      else tab && tab.close();
    } catch {
      tab && tab.close();
    }
  };

  const consentTick = (checked, onChange, id) =>
    el("label", { className: "tick", htmlFor: id }, el("input", { id, type: "checkbox", checked, onChange }), el("span", null, cfg?.consentText, cfg?.privacyUrl ? " " : null, cfg?.privacyUrl ? el("a", { href: cfg.privacyUrl, target: "_blank", rel: "noopener" }, "Privacy notice") : null));

  return el(
    "div",
    { className: "enq" },
    el("header", { className: "enq__top" }, el(Link, { to: "/login" }, el("img", { src: "/brand/regalis-logo-v2.png", alt: "Regalis WorkSpace" })), el(Link, { to: "/login", style: { color: "var(--text-secondary)", fontSize: ".9rem" } }, "Members: sign in")),
    el(
      "main",
      { className: "enq__wrap" },
      el(
        "section",
        null,
        el("h1", null, kindQ === "quote" ? "Get a quote for your workspace" : "See the space before you decide"),
        el("p", { className: "enq__lead" }, "Cabins, desks, meeting rooms and virtual offices. Tell us what you need and we will call you back today to arrange a tour or send a price."),
        el("ul", { className: "enq__points" }, el("li", null, "Only your name and mobile are needed."), el("li", null, "A real person calls you back — usually within half an hour in working hours."), el("li", null, "We use your details only to talk to you about workspace. We never pass them to anyone else.")),
        !done &&
          el(
            "form",
            { className: "enq__bar", onSubmit: callback, "aria-label": "Callback request" },
            el("b", null, "Busy? We will call you."),
            el("input", { type: "tel", inputMode: "tel", placeholder: "Your mobile number", "aria-label": "Mobile number for a callback", value: cb.mobile, maxLength: 20, onChange: (e) => setCb((x) => ({ ...x, mobile: e.target.value })) }),
            el("button", { type: "submit", disabled: !!busy }, busy === "callback" ? "Sending…" : "Call me"),
            el("div", { style: { flex: "1 1 100%" } }, consentTick(cb.consent, (e) => setCb((x) => ({ ...x, consent: e.target.checked })), "enq-cb-consent")),
            cbErr && el("div", { className: "enq__err", role: "alert" }, cbErr)
          )
      ),
      el(
        "section",
        { className: "enq__card" },
        done
          ? el(
              "div",
              { className: "enq__ok", role: "status" },
              el("b", null, "Thanks — we have your enquiry and we will call you back today."),
              el("p", { style: { margin: "8px 0 0" } }, cfg?.publicPhone ? `If it is urgent, call us on ${cfg.publicPhone}.` : "You can close this page.")
            )
          : el(
              "form",
              { onSubmit: submit, noValidate: true, "aria-label": "Book a tour" },
              el("h2", { style: { margin: "0 0 14px", fontSize: "1.25rem" } }, kindQ === "quote" ? "Send me a quote" : "Book a tour"),
              el(
                "div",
                { className: "enq__grid" },
                el("label", { className: "f" }, "Your name *", el("input", { autoComplete: "name", value: f.name, maxLength: 150, onChange: (e) => set({ name: e.target.value }) })),
                el("label", { className: "f" }, "Mobile number *", el("input", { type: "tel", inputMode: "tel", autoComplete: "tel", value: f.mobile, maxLength: 20, onChange: (e) => set({ mobile: e.target.value }) })),
                el("label", { className: "f" }, "Email (optional)", el("input", { type: "email", autoComplete: "email", value: f.email, maxLength: 256, onChange: (e) => set({ email: e.target.value }) })),
                el("label", { className: "f" }, "What are you looking for", el("select", { value: f.lookingForId, onChange: (e) => set({ lookingForId: e.target.value }) }, el("option", { value: "" }, "Choose (optional)"), (cfg?.lookingFor || []).map((l) => el("option", { key: l.id, value: l.id }, l.name)))),
                el("label", { className: "f" }, "People", el("input", { type: "number", min: 1, value: f.people, onChange: (e) => set({ people: e.target.value }) })),
                el("label", { className: "f" }, "From when", el("input", { type: "date", value: f.fromWhen, onChange: (e) => set({ fromWhen: e.target.value }) }))
              ),
              el("label", { className: "f", style: { marginTop: 12 } }, "Anything else we should know", el("textarea", { value: f.message, maxLength: 2000, onChange: (e) => set({ message: e.target.value }) })),
              el("div", { className: "enq__hp", "aria-hidden": "true" }, el("label", null, "Website", el("input", { tabIndex: -1, autoComplete: "off", value: f.website, onChange: (e) => set({ website: e.target.value }) }))),
              consentTick(f.consent, (e) => set({ consent: e.target.checked }), "enq-consent"),
              cfg?.marketingText && el("label", { className: "tick", htmlFor: "enq-mkt" }, el("input", { id: "enq-mkt", type: "checkbox", checked: f.marketing, onChange: (e) => set({ marketing: e.target.checked }) }), el("span", null, cfg.marketingText)),
              el("button", { type: "submit", className: "enq__btn", disabled: !!busy || !f.consent }, busy && busy !== "callback" ? "Sending…" : kindQ === "quote" ? "Send me a quote" : "Book a tour"),
              err && el("div", { className: "enq__err", role: "alert" }, err),
              el("p", { className: "enq__small" }, "The button works once the box is ticked. Nothing is sent to anyone else.")
            )
      )
    ),
    cfg?.whatsAppNumber && el("button", { type: "button", className: "enq__wa", onClick: whatsapp, title: "Chat on WhatsApp " + cfg.whatsAppNumber }, "WhatsApp us")
  );
}
