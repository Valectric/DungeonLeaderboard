"""Serves Builds/ with the headers a Unity WebGL build needs, so it can be played locally.

The shipped build is **brotli-compressed with no decompression fallback**, so it relies on the server
sending `Content-Encoding: br`. Python's stock `http.server` does not, and the loader fails with a
decompression error that reads like a corrupt build.

Why this exists at all: the itch page runs the game in a **cross-origin iframe**, which synthetic
input cannot reach — verified, and already recorded in HANDOVER.md. So automation can confirm the
build boots and can photograph the title screen, and cannot press a single button. Served from
localhost the page is same-origin and the whole loop is drivable, which is the only way anything but
a human has ever played the artefact that actually ships.

Run:  python Tools/serve-build.py [port]        # default 8777
"""

import functools
import http.server
import os
import socketserver
import sys

ROOT = os.path.join(
    os.path.dirname(os.path.dirname(os.path.abspath(__file__))), "Builds")

# Unity names every compressed payload `.unityweb` regardless of what is inside it, so the encoding
# cannot be inferred from the extension -- it is decided at build time. Brotli is this project's
# setting; a gzip build wants "gzip" here instead.
ENCODING = "br"


class UnityHandler(http.server.SimpleHTTPRequestHandler):
    """Adds the Content-Encoding and Content-Type a Unity WebGL loader expects."""

    def end_headers(self):
        """Tags compressed payloads before the response is committed."""
        path = self.path.split("?", 1)[0]

        if path.endswith(".unityweb"):
            self.send_header("Content-Encoding", ENCODING)
            if path.endswith(".wasm.unityweb"):
                self.send_header("Content-Type", "application/wasm")
            elif path.endswith(".js.unityweb"):
                self.send_header("Content-Type", "application/javascript")

        # A build is rebuilt often and a cached loader against fresh data is a confusing failure.
        self.send_header("Cache-Control", "no-store")
        super().end_headers()

    def log_message(self, fmt, *args):
        """Quieter than the default, which prints a line per asset."""
        if "unityweb" in (args[0] if args else "") or "index.html" in (args[0] if args else ""):
            sys.stderr.write(f"  {args[0]}\n")


def main():
    port = int(sys.argv[1]) if len(sys.argv) > 1 else 8777

    if not os.path.exists(os.path.join(ROOT, "index.html")):
        print(f"no build in {ROOT} -- run the WebGL build first")
        return 1

    handler = functools.partial(UnityHandler, directory=ROOT)
    socketserver.TCPServer.allow_reuse_address = True

    with socketserver.TCPServer(("127.0.0.1", port), handler) as server:
        print(f"serving {ROOT} at http://127.0.0.1:{port}/  (Content-Encoding: {ENCODING})")
        sys.stdout.flush()
        server.serve_forever()

    return 0


if __name__ == "__main__":
    sys.exit(main())
