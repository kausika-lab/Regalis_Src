# Minimal SMTP sink for local testing: accepts every message on 127.0.0.1:2525 and writes it
# to mail/<n>.eml next to this script. No auth, no TLS, never relays anything.
import asyncio, os, itertools

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "mail")
os.makedirs(OUT, exist_ok=True)
counter = itertools.count(len(os.listdir(OUT)) + 1)

async def handle(reader, writer):
    send = lambda s: writer.write((s + "\r\n").encode())
    send("220 sink ESMTP")
    rcpt, data = [], None
    while True:
        line = await reader.readline()
        if not line:
            break
        cmd = line.decode(errors="replace").rstrip("\r\n")
        up = cmd.upper()
        if up.startswith("EHLO") or up.startswith("HELO"):
            send("250-sink"); send("250 8BITMIME")
        elif up.startswith("MAIL FROM"):
            rcpt = []; send("250 OK")
        elif up.startswith("RCPT TO"):
            rcpt.append(cmd[8:].strip()); send("250 OK")
        elif up == "DATA":
            send("354 end with ."); await writer.drain()
            buf = []
            while True:
                l = await reader.readline()
                if l in (b".\r\n", b".\n", b""):
                    break
                buf.append(l[1:] if l.startswith(b"..") else l)
            n = next(counter)
            with open(os.path.join(OUT, f"{n:04d}.eml"), "wb") as f:
                f.write(("X-Sink-Rcpt: " + ", ".join(rcpt) + "\r\n").encode())
                f.write(b"".join(buf))
            print("stored", n, rcpt, flush=True)
            send("250 stored")
        elif up == "QUIT":
            send("221 bye"); await writer.drain(); break
        else:
            send("250 OK")
        await writer.drain()
    writer.close()

async def main():
    server = await asyncio.start_server(handle, "127.0.0.1", 2525)
    print("smtp sink on 127.0.0.1:2525 ->", OUT, flush=True)
    async with server:
        await server.serve_forever()

asyncio.run(main())
