"""Small client for the Billing & Finance smoke tests against the test API on :83."""
import json, sys, urllib.request, urllib.error
BASE = "http://127.0.0.1:83/api"
TOKEN = None
def call(method, path, body=None, raw=False, files=None):
    url = BASE + path
    headers = {"Authorization": f"Bearer {TOKEN}"} if TOKEN else {}
    data = None
    if files is not None:
        boundary = "----bf123456"
        parts = []
        for k, v in (body or {}).items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"\r\n\r\n{v}\r\n'.encode())
        for k, (name, content, ctype) in files.items():
            parts.append(f'--{boundary}\r\nContent-Disposition: form-data; name="{k}"; filename="{name}"\r\nContent-Type: {ctype}\r\n\r\n'.encode() + content + b"\r\n")
        parts.append(f"--{boundary}--\r\n".encode())
        data = b"".join(parts)
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    elif body is not None:
        data = json.dumps(body).encode()
        headers["Content-Type"] = "application/json"
    req = urllib.request.Request(url, data=data, method=method, headers=headers)
    try:
        with urllib.request.urlopen(req, timeout=120) as r:
            content = r.read()
            if raw: return r.status, content
            return r.status, json.loads(content or b"null")
    except urllib.error.HTTPError as e:
        content = e.read()
        try: return e.code, json.loads(content)
        except Exception: return e.code, content[:300]
def login(email, password):
    global TOKEN
    s, r = call("POST", "/auth/login", {"email": email, "password": password, "portal": "admin"})
    assert s == 200, (s, r)
    TOKEN = r["data"]["accessToken"]
    return r["data"]
