#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
Download a {group: [urls]} map scraped from Bing into <root>/<group>/NN_domain.jpg

    python download_urls.py urls.json [--target 8] [--append]

- skips watermarked stock domains unless the group is still short
- rejects images under --min-side pixels
- dedupes near-identical shots with an average hash
- `--append` keeps existing files and continues numbering
"""
import argparse
import io
import json
import os
import re
import time
import urllib.parse
import urllib.request

UA = ("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 "
      "(KHTML, like Gecko) Chrome/120.0 Safari/537.36")

BAD = ("dreamstime", "alamy", "shutterstock", "istockphoto", "ftcdn.net",
       "creativemarket", "freepik", "vecteezy", "123rf", "gettyimages",
       "depositphotos", "canstockphoto", "bigstock", "stock.adobe")


def get(u):
    req = urllib.request.Request(u, headers={
        "User-Agent": UA, "Referer": "https://www.bing.com/",
        "Accept": "image/avif,image/webp,image/apng,image/*,*/*;q=0.8"})
    with urllib.request.urlopen(req, timeout=25) as r:
        return r.read()


def ahash(im):
    s = im.convert("L").resize((8, 8), 3)
    px = list(s.getdata())
    avg = sum(px) / 64.0
    return "".join("1" if p > avg else "0" for p in px)


def near(a, b, tol=6):
    return sum(1 for x, y in zip(a, b) if x != y) <= tol


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("urls")
    ap.add_argument("--root", required=True)
    ap.add_argument("--target", type=int, default=8)
    ap.add_argument("--min-side", type=int, default=500)
    ap.add_argument("--append", action="store_true")
    a = ap.parse_args()

    from PIL import Image
    data = json.load(open(a.urls, "r", encoding="utf-8"))

    for group, urls in data.items():
        folder = os.path.join(a.root, group)
        os.makedirs(folder, exist_ok=True)
        files = [f for f in os.listdir(folder) if re.match(r"^\d+_", f)]
        if not a.append:
            for f in files:
                os.remove(os.path.join(folder, f))
            files = []
        have = len(files)
        if have >= a.target:
            print("%-24s full (%d)" % (group, have))
            continue
        idx = max([int(f[:2]) for f in files] or [0]) + 1
        seen = []
        for f in files:
            try:
                seen.append(ahash(Image.open(os.path.join(folder, f))))
            except Exception:
                pass

        strict = [u for u in urls if not any(b in u.lower() for b in BAD)]
        loose = [u for u in urls if any(b in u.lower() for b in BAD)]
        kept = 0
        for pool in (strict, loose):
            for u in pool:
                if have >= a.target:
                    break
                try:
                    raw = get(u)
                    im = Image.open(io.BytesIO(raw))
                    if min(im.size) < a.min_side:
                        continue
                    im = im.convert("RGB")
                    h = ahash(im)
                    if any(near(h, k) for k in seen):
                        continue
                    if max(im.size) > 1400:
                        im.thumbnail((1400, 1400), Image.LANCZOS)
                    name = "%02d_%s.jpg" % (idx, re.sub(
                        r"[^A-Za-z0-9]+", "_", urllib.parse.urlparse(u).netloc)[:24].strip("_"))
                    im.save(os.path.join(folder, name), "JPEG", quality=88)
                    seen.append(h)
                    idx += 1
                    have += 1
                    kept += 1
                    time.sleep(0.15)
                except Exception:
                    continue
            if have >= a.target:
                break
        print("%-24s +%d -> %d" % (group, kept, have))


if __name__ == "__main__":
    main()
