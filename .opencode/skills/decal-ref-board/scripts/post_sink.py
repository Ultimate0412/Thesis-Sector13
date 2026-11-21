#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Tiny local HTTP sink: the browser POSTs its scraped URL map here, so the list
never has to pass through the chat.

    python post_sink.py [out.json]
    # then in the browser page:
    #   await fetch('http://127.0.0.1:8765/save', {method:'POST', body: JSON.stringify(window.__refs)})
"""
import os
import sys
from http.server import BaseHTTPRequestHandler, HTTPServer

OUT = sys.argv[1] if len(sys.argv) > 1 else os.path.join(
    os.environ.get("TEMP", "."), "refs_urls.json")


class H(BaseHTTPRequestHandler):
    def _cors(self):
        self.send_header("Access-Control-Allow-Origin", "*")
        self.send_header("Access-Control-Allow-Methods", "POST, OPTIONS")
        self.send_header("Access-Control-Allow-Headers", "Content-Type")

    def do_OPTIONS(self):
        self.send_response(204)
        self._cors()
        self.end_headers()

    def do_POST(self):
        n = int(self.headers.get("Content-Length", 0))
        body = self.rfile.read(n)
        with open(OUT, "wb") as f:
            f.write(body)
        self.send_response(200)
        self._cors()
        self.send_header("Content-Type", "text/plain")
        self.end_headers()
        self.wfile.write(b"ok %d" % len(body))

    def log_message(self, *a):
        pass


print("listening on http://127.0.0.1:8765/save ->", OUT)
HTTPServer(("127.0.0.1", 8765), H).serve_forever()
