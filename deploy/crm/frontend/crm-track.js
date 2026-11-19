/*
 * Regalis CRM visit tracker (19 Nov 2026, kit deploy/crm, document sections 6.4 and 18 item 1).
 *
 * Records ANONYMOUS visits: a first-party cookie (rg_vid) holding a random id, plus the path of
 * each page and how long it stayed open. No name, no email, no third party; query strings are
 * never sent (only utm_source / utm_medium / utm_campaign are read from them). In the Regalis
 * app it runs on the public pages only (signed-in members and staff are never tracked).
 *
 * On another site (a future marketing site) add:
 *   <script src="https://myregalis.com/crm-track.js" data-api="https://myregalis.com/api" data-all="1" defer></script>
 * and allow that site's origin in the API's Cors:AllowedOrigins. window.RegalisCrm.visitorKey()
 * returns the id to send with an enquiry form (POST /api/public/enquiries, field visitorKey).
 */
(function () {
  "use strict";
  if (window.RegalisCrm) return;
  var me = document.currentScript;
  var API = (me && me.getAttribute("data-api")) || "/api";
  var ALL = me && me.getAttribute("data-all") === "1";
  // Public pages of the Regalis app. Anything else is a signed-in screen and is not tracked.
  var PUBLIC = /^\/(enquire|login|register|forgot-password|reset-password|verify-otp)?\/?$/;
  var COOKIE = "rg_vid";

  function readCookie() {
    var m = document.cookie.match(/(?:^|; )rg_vid=([^;]+)/);
    return m ? decodeURIComponent(m[1]) : null;
  }
  function writeCookie(v) {
    try {
      document.cookie = COOKIE + "=" + encodeURIComponent(v) + "; path=/; max-age=" + 3600 * 24 * 365 + "; SameSite=Lax" + (location.protocol === "https:" ? "; Secure" : "");
    } catch (e) {}
  }
  function ss(k, v) {
    try {
      if (v === undefined) return sessionStorage.getItem(k);
      sessionStorage.setItem(k, v);
    } catch (e) {
      return null;
    }
  }
  // Keep campaign tags for this browser session, so the enquiry form can send them.
  try {
    var q = new URLSearchParams(location.search);
    if (q.get("utm_source") || q.get("utm_campaign")) {
      ss("rg-utm", JSON.stringify({ utmSource: q.get("utm_source"), utmMedium: q.get("utm_medium"), utmCampaign: q.get("utm_campaign") }));
    }
  } catch (e) {}

  var current = null; // { id, at }
  var lastPath = null;

  function post(body, keepalive) {
    try {
      return fetch(API + "/public/visits", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify(body),
        keepalive: !!keepalive,
        credentials: "omit"
      }).then(function (r) {
        return r.ok ? r.json() : null;
      });
    } catch (e) {
      return Promise.resolve(null);
    }
  }
  function signedIn() {
    try {
      // The app keeps its session in sessionStorage "regalis-auth"; a member's or staff use is not a visit.
      return /"accessToken":"[^"]+"/.test(sessionStorage.getItem("regalis-auth") || "");
    } catch (e) {
      return false;
    }
  }
  function closeCurrent(keepalive) {
    if (!current || !current.id) return;
    var secs = Math.round((Date.now() - current.at) / 1000);
    if (secs > 0) post({ visitorKey: readCookie(), pageViewId: current.id, seconds: secs }, keepalive);
    current = null;
  }
  function track() {
    var path = location.pathname;
    if (path === lastPath) return;
    lastPath = path;
    closeCurrent(false);
    if (!ALL && (!PUBLIC.test(path) || signedIn())) return;
    var utm = {};
    try {
      utm = JSON.parse(ss("rg-utm") || "{}");
    } catch (e) {}
    var first = !ss("rg-vsess");
    ss("rg-vsess", "1");
    var started = { id: null, at: Date.now() };
    current = started;
    post({
      visitorKey: readCookie(),
      path: path,
      referrer: first ? document.referrer || null : null,
      utmSource: utm.utmSource || null,
      utmMedium: utm.utmMedium || null,
      utmCampaign: utm.utmCampaign || null,
      newVisit: first
    }).then(function (r) {
      var d = r && r.data;
      if (!d) return;
      if (d.visitorKey && d.visitorKey !== readCookie()) writeCookie(d.visitorKey);
      started.id = d.pageViewId;
    });
  }
  ["pushState", "replaceState"].forEach(function (m) {
    var orig = history[m];
    history[m] = function () {
      var r = orig.apply(this, arguments);
      setTimeout(track, 0);
      return r;
    };
  });
  window.addEventListener("popstate", track);
  window.addEventListener("pagehide", function () {
    closeCurrent(true);
  });
  document.addEventListener("visibilitychange", function () {
    if (document.visibilityState === "hidden") closeCurrent(true);
  });

  window.RegalisCrm = {
    visitorKey: readCookie,
    track: track
  };
  if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", track);
  else track();
})();
