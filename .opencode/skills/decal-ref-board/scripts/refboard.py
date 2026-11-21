#!/usr/bin/env python
# -*- coding: utf-8 -*-
"""
refboard.py - build grouped reference boards for texture / decal work.

Commands
--------
fetch : pull CC-licensed images from Openverse (Wikimedia fallback)
fill  : pull images from Bing/Google via icrawler (no API key)
board : pack the finished folders into one PureRef (.pur) board

Typical use
-----------
    python refboard.py fetch --config groups.json
    python refboard.py fill  --config groups.json
    python refboard.py board --config groups.json
"""
import argparse
import hashlib
import io
import json
import os
import re
import sys
import time
import urllib.parse
import urllib.request

UA = "Sector13-RefBoard/1.0 (local tool)"


# ----------------------------------------------------------------------------- config
def load_config(path):
    with open(path, "r", encoding="utf-8-sig") as f:
        cfg = json.load(f)
    cfg.setdefault("per_group", 8)
    cfg.setdefault("min_width", 700)
    cfg.setdefault("board_name", "Refs")
    root = cfg["root"]
    if not os.path.isabs(root):
        root = os.path.join(os.path.dirname(os.path.abspath(path)), root)
    cfg["root"] = os.path.normpath(root)
    return cfg


# ----------------------------------------------------------------------------- helpers
def http_json(url, timeout=30, retries=4):
    last = None
    for attempt in range(retries):
        req = urllib.request.Request(url, headers={"User-Agent": UA, "Accept": "application/json"})
        try:
            with urllib.request.urlopen(req, timeout=timeout) as r:
                return json.loads(r.read().decode("utf-8", "replace"))
        except Exception as e:
            last = e
            code = getattr(e, "code", 0)
            if code in (429, 503, 424):
                time.sleep(4.0 * (attempt + 1))
                continue
            raise
    raise last


def http_bytes(url, timeout=45):
    req = urllib.request.Request(url, headers={"User-Agent": UA})
    with urllib.request.urlopen(req, timeout=timeout) as r:
        return r.read()


def ahash(im):
    from PIL import Image
    small = im.convert("L").resize((8, 8), Image.LANCZOS)
    px = list(small.getdata())
    avg = sum(px) / 64.0
    bits = "".join("1" if p > avg else "0" for p in px)
    return bits


def similar(a, b, tol=6):
    return sum(1 for x, y in zip(a, b) if x != y) <= tol


def next_index(folder):
    n = 0
    for f in os.listdir(folder):
        m = re.match(r"^(\d+)_", f)
        if m:
            n = max(n, int(m.group(1)))
    return n + 1


def is_ref(f):
    """A real reference image: not the title card, not a review sheet."""
    return (f.lower().endswith((".jpg", ".jpeg", ".png"))
            and not f.startswith("_") and not f.startswith("00_TITLE"))


def count_refs(folder):
    return len([f for f in os.listdir(folder) if is_ref(f)])


def save_image(raw, folder, idx, label, min_w, keep=None):
    from PIL import Image
    im = Image.open(io.BytesIO(raw))
    if min(im.size) < 400:
        return None, None
    im = im.convert("RGB")
    h = ahash(im)
    for k in (keep if keep is not None else []):
        if similar(h, k):
            return None, None
    if max(im.size) > 1600:
        im.thumbnail((1600, 1600), Image.LANCZOS)
    name = "%02d_%s.jpg" % (idx, re.sub(r"[^A-Za-z0-9]+", "_", label)[:32].strip("_") or "ref")
    im.save(os.path.join(folder, name), "JPEG", quality=88)
    return name, h


# ----------------------------------------------------------------------------- fetch (CC)
def openverse(q, n):
    params = urllib.parse.urlencode({
        "q": q, "page_size": str(n), "license_type": "all-cc",
        "size": "large", "mature": "false",
    })
    data = http_json("https://api.openverse.org/v1/images/?" + params)
    return [dict(url=r.get("url"), thumb=r.get("thumbnail"),
                 title=(r.get("title") or "")[:70],
                 creator=(r.get("creator") or "")[:40],
                 license=(r.get("license") or "").upper(),
                 page=(r.get("foreign_landing_url") or ""),
                 w=r.get("width") or 0, h=r.get("height") or 0)
            for r in data.get("results", [])]

def commons(q, n):
    params = urllib.parse.urlencode({
        "action": "query", "format": "json", "generator": "search",
        "gsrsearch": "filetype:bitmap " + q, "gsrnamespace": "6", "gsrlimit": str(n),
        "prop": "imageinfo", "iiprop": "url|size|extmetadata", "iiurlwidth": "1400",
    })
    data = http_json("https://commons.wikimedia.org/w/api.php?" + params)
    out = []
    for page in (data.get("query", {}).get("pages", {}) or {}).values():
        ii = (page.get("imageinfo") or [{}])[0]
        md = ii.get("extmetadata", {}) or {}
        out.append(dict(
            url=ii.get("url"), thumb=ii.get("thumburl"),
            title=(page.get("title") or "").replace("File:", "")[:70],
            creator=re.sub("<[^>]+>", "", (md.get("Artist", {}) or {}).get("value", ""))[:40],
            license=(md.get("LicenseShortName", {}) or {}).get("value", ""),
            page=ii.get("descriptionurl") or "",
            w=ii.get("width") or 0, h=ii.get("height") or 0))
    return out


def usable(c, min_w):
    w, h = c.get("w") or 0, c.get("h") or 0
    if w < min_w or h < min_w or not c.get("url"):
        return -1
    ar = w / float(h or 1)
    if not (0.55 <= ar <= 2.0):
        return -1
    return min(w, 4000) / 100.0 - abs(ar - 1.0) * 8


def cmd_fetch(cfg):
    creds = []
    for g in cfg["groups"]:
        folder = os.path.join(cfg["root"], g["folder"])
        os.makedirs(folder, exist_ok=True)
        have = count_refs(folder)
        if have >= cfg["per_group"]:
            print("[skip] %s (%d)" % (g["folder"], have))
            continue
        print("[fetch] %s" % g["folder"])
        seen, picks = set(), []
        for q in g["queries"][:5]:
            batch = []
            try:
                batch = openverse(q, 20)
            except Exception as e:
                print("   openverse: %s" % str(e)[:70])
            time.sleep(2.5)
            if len([c for c in batch if usable(c, cfg["min_width"]) >= 0]) < 4:
                try:
                    batch += commons(q, 10)
                except Exception as e:
                    print("   commons: %s" % str(e)[:70])
                time.sleep(1.5)
            batch.sort(key=lambda c: usable(c, cfg["min_width"]), reverse=True)
            for c in batch:
                if usable(c, cfg["min_width"]) < 0:
                    continue
                k = (c["url"] or "").split("?")[0]
                if k in seen:
                    continue
                seen.add(k)
                picks.append(c)
            if len(picks) >= cfg["per_group"] * 3:
                break
        idx, kept = next_index(folder), []
        for c in picks:
            if len(kept) >= cfg["per_group"] - have:
                break
            raw = None
            for src in (c.get("thumb"), c.get("url")):
                if not src:
                    continue
                try:
                    raw = http_bytes(src)
                    break
                except Exception:
                    continue
            if raw is None:
                continue
            try:
                name, h = save_image(raw, folder, idx, c["title"] or g["folder"], cfg["min_width"], kept)
                if name:
                    kept.append(h)
                    idx += 1
                    creds.append("| %s | %s | %s | %s | %s |" % (
                        g["folder"], name, c["creator"] or "-", c["license"] or "-",
                        c["page"] or c["url"]))
            except Exception as e:
                print("   skip: %s" % str(e)[:70])
            time.sleep(0.3)
        print("   +%d" % len(kept))
    write_credits(cfg, creds, append=True)


# ----------------------------------------------------------------------------- fill (icrawler)
def cmd_fill(cfg):
    try:
        from icrawler.builtin import BingImageCrawler
    except ImportError:
        print("icrawler missing:  pip install icrawler")
        return 1
    import tempfile
    from PIL import Image

    creds = []
    for g in cfg["groups"]:
        folder = os.path.join(cfg["root"], g["folder"])
        os.makedirs(folder, exist_ok=True)
        have = count_refs(folder)
        need = cfg["per_group"] - have
        if need <= 0:
            print("[skip] %s (%d)" % (g["folder"], have))
            continue
        print("[fill ] %s  need %d" % (g["folder"], need))
        idx = next_index(folder)
        kept = []
        for f in sorted(os.listdir(folder)):
            if f.lower().endswith(".jpg"):
                try:
                    kept.append(ahash(Image.open(os.path.join(folder, f))))
                except Exception:
                    pass
        for q in g["queries"]:
            if len(kept) >= have + need:
                break
            tmp = tempfile.mkdtemp(prefix="rb_")
            try:
                BingImageCrawler(storage={"root_dir": tmp}, downloader_threads=4).crawl(
                    keyword=q, max_num=need * 5,
                    filters={"type": "photo", "size": "large"})
            except Exception as e:
                print("   crawl: %s" % str(e)[:70])
                continue
            for f in sorted(os.listdir(tmp)):
                if len(kept) >= have + need:
                    break
                p = os.path.join(tmp, f)
                if not f.lower().endswith((".jpg", ".jpeg", ".png")):
                    continue
                try:
                    with open(p, "rb") as fh:
                        raw = fh.read()
                    name, h = save_image(raw, folder, idx, q, cfg["min_width"], kept)
                    if name:
                        kept.append(h)
                        idx += 1
                        creds.append("| %s | %s | Bing | - | verify |" % (g["folder"], name))
                except Exception:
                    pass
            if len(kept) < have + need:
                fill_group_scrape(g, folder, have + need, cfg["min_width"], idx, kept, creds)
                idx = next_index(folder)
        print("   +%d" % (len(kept) - have))
    write_credits(cfg, creds, append=True)
    return 0


def write_credits(cfg, rows, append=False):
    path = os.path.join(cfg["root"], "CREDITS.md")
    head = ("# Reference credits\n\nMixed sources: Openverse/Wikimedia (CC) and Bing image search\n"
            "(`license: verify` rows need a license check before a public build).\n\n"
            "| Group | File | Creator | License | Source |\n|---|---|---|---|---|\n")
    if append and os.path.exists(path):
        with open(path, "a", encoding="utf-8") as f:
            f.write("\n".join(rows) + ("\n" if rows else ""))
    else:
        with open(path, "w", encoding="utf-8") as f:
            f.write(head + "\n".join(rows) + "\n")


def bing_scrape(q, count=35):
    """Fallback: Bing image *async* endpoint. Returns raw murl list (no key, no browser)."""
    params = urllib.parse.urlencode({
        "q": q, "first": "1", "count": str(count), "mkt": "en-US", "adlt": "off",
        "qft": "+filterui:photo-photo+filterui:imagesize-large",
    })
    req = urllib.request.Request(
        "https://www.bing.com/images/async?" + params,
        headers={"User-Agent": "Mozilla/5.0 (Windows NT 10.0; Win64; x64) "
                               "AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120 Safari/537.36",
                 "Referer": "https://www.bing.com/images/search"})
    with urllib.request.urlopen(req, timeout=30) as r:
        html = r.read().decode("utf-8", "replace")
    urls = re.findall(r'murl(?:&quot;|\\?")\s*:\s*(?:&quot;|\\?")([^"&]+)', html)
    out = []
    for u in urls:
        u = u.replace("\\/", "/")
        if u.startswith("http") and u not in out:
            out.append(u)
    return out


def fill_group_scrape(g, folder, need, min_w, idx, kept, creds):
    for q in g["queries"]:
        if len(kept) >= need:
            break
        try:
            urls = bing_scrape(q, count=max(35, need * 4))
        except Exception as e:
            print("   bing: %s" % str(e)[:70])
            urls = []
        for u in urls:
            if len(kept) >= need:
                break
            try:
                raw = http_bytes(u, timeout=20)
                name, h = save_image(raw, folder, idx, q, min_w, kept)
                if name:
                    kept.append(h)
                    idx += 1
                    creds.append("| %s | %s | Bing | - | verify |" % (g["folder"], name))
            except Exception:
                pass
            time.sleep(0.15)


# ----------------------------------------------------------------------------- board
def title_card(path, en, th, note, look, num):
    from PIL import Image, ImageDraw, ImageFont
    W, H = 620, 430
    img = Image.new("RGB", (W, H), (24, 22, 20))
    d = ImageDraw.Draw(img)

    def f(name, size):
        p = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", name)
        try:
            return ImageFont.truetype(p, size)
        except Exception:
            return ImageFont.load_default()

    d.rectangle([0, 0, W - 1, H - 1], outline=(210, 176, 96), width=3)
    d.text((22, 16), "%02d" % num, font=f("tahomabd.ttf", 26), fill=(210, 176, 96))
    d.text((22, 58), en, font=f("tahomabd.ttf", 40), fill=(245, 240, 230))
    d.text((22, 110), th, font=f("tahoma.ttf", 34), fill=(210, 176, 96))
    d.text((22, 166), note, font=f("tahoma.ttf", 22), fill=(180, 180, 180))
    d.line([22, 204, W - 22, 204], fill=(90, 84, 76), width=2)
    d.text((22, 220), "LOOK FOR", font=f("tahoma.ttf", 22), fill=(140, 200, 160))
    y = 250
    for line in [x.strip() for x in look.split("/") if x.strip()]:
        d.text((22, y), "- " + line, font=f("tahoma.ttf", 22), fill=(220, 220, 220))
        y += 30
    img.save(path, "PNG")


def cmd_board(cfg):
    tools = os.environ.get("PUREREF_FORMAT_PATH") or cfg.get("purformat_path")
    if tools and tools not in sys.path:
        sys.path.insert(0, tools)
    try:
        from purformat import purformat, items
    except ImportError:
        print("Need FyorDev/PureRef-format on sys.path:")
        print("  git clone https://github.com/FyorDev/PureRef-format")
        print("  set PUREREF_FORMAT_PATH=<repo>")
        return 1
    from PIL import Image

    root = cfg["root"]
    pur = purformat.PurFile()
    pur.images = []
    ROW_H, GAP, TITLE_W = 380, 46, 620
    y, max_x = 0.0, 0.0
    for i, g in enumerate(cfg["groups"], 1):
        folder = os.path.join(root, g["folder"])
        if not os.path.isdir(folder):
            continue
        tcard = os.path.join(folder, "00_TITLE.png")
        if not os.path.exists(tcard):
            title_card(tcard, g["en"], g.get("th", ""), g.get("note", ""), g.get("look", ""), i)
        files = sorted(f for f in os.listdir(folder) if is_ref(f) or f.startswith("00_TITLE"))
        x = 0.0
        for fn in files:
            p = os.path.join(folder, fn)
            im = Image.open(p).convert("RGB")
            pi = items.PurImage()
            buf = io.BytesIO()
            im.save(buf, "PNG", compress_level=6)
            pi.pngBinary = buf.getvalue()
            tr = items.PurGraphicsImageItem()
            tr.reset_crop(im.width, im.height)
            tr.name = os.path.splitext(fn)[0]
            tr.source = fn
            if fn.startswith("00_TITLE"):
                tr.scale_to_height(ROW_H)
                tr.scale_to_width(TITLE_W)
            else:
                tr.scale_to_height(ROW_H)
                if tr.width > ROW_H * 1.6:
                    tr.scale_to_height(ROW_H * 0.92)
            tr.x = x + tr.width / 2.0
            tr.y = y + ROW_H / 2.0
            x += tr.width + GAP
            pi.transforms = [tr]
            pur.images.append(pi)
        max_x = max(max_x, x)
        y += ROW_H + GAP * 2.4

    pur.canvas = [-max_x / 2.0, -y / 2.0, max_x / 2.0, y / 2.0]
    pur.zoom = 0.28
    pur.xCanvas, pur.yCanvas = 0, 0
    pur.folderLocation = root
    out = os.path.join(root, cfg["board_name"] + ".pur")
    pur.write(out)
    print("board: %s  (%.1f MB, %d images)" % (out, os.path.getsize(out) / 1048576.0, len(pur.images)))
    return 0


# ----------------------------------------------------------------------------- review
def cmd_sheet(cfg, only=None):
    """Build a labelled contact sheet per group so the refs can be eyeballed in one image."""
    from PIL import Image, ImageDraw, ImageFont
    cell, cols, pad = 300, 4, 6

    def f(size):
        p = os.path.join(os.environ.get("WINDIR", r"C:\Windows"), "Fonts", "tahomabd.ttf")
        try:
            return ImageFont.truetype(p, size)
        except Exception:
            return ImageFont.load_default()

    font = f(20)
    for g in cfg["groups"]:
        if only and g["folder"] != only:
            continue
        folder = os.path.join(cfg["root"], g["folder"])
        if not os.path.isdir(folder):
            continue
        files = sorted(f for f in os.listdir(folder) if is_ref(f))
        if not files:
            print("[sheet] %s empty" % g["folder"])
            continue
        rows = (len(files) + cols - 1) // cols
        W, H = cols * (cell + pad) + pad, rows * (cell + pad) + pad
        sheet = Image.new("RGB", (W, H), (16, 16, 16))
        d = ImageDraw.Draw(sheet)
        for i, fn in enumerate(files):
            im = Image.open(os.path.join(folder, fn)).convert("RGB")
            im.thumbnail((cell, cell), Image.LANCZOS)
            cx, cy = i % cols, i // cols
            ox = pad + cx * (cell + pad)
            oy = pad + cy * (cell + pad)
            sheet.paste(im, (ox + (cell - im.width) // 2, oy + (cell - im.height) // 2))
            num = re.match(r"^(\d+)", fn).group(1)
            d.rectangle([ox, oy, ox + cell - 1, oy + cell - 1], outline=(70, 70, 70))
            d.rectangle([ox, oy, ox + 34, oy + 26], fill=(0, 0, 0))
            d.text((ox + 6, oy + 3), num, font=font, fill=(255, 220, 120))
        out = os.path.join(folder, "_sheet.jpg")
        sheet.save(out, "JPEG", quality=90)
        print("[sheet] %s -> %s (%d imgs)" % (g["folder"], out, len(files)))


def cmd_prune(cfg, group, reject):
    """Delete refs whose file number is listed, e.g. --reject 03,05,11"""
    folder = os.path.join(cfg["root"], group)
    if not os.path.isdir(folder):
        print("no such group: %s" % group)
        return 1
    drop = {r.strip().lstrip("0") or "0" for r in reject.split(",") if r.strip()}
    removed = 0
    for fn in sorted(os.listdir(folder)):
        if not is_ref(fn):
            continue
        m = re.match(r"^(\d+)_", fn)
        if m and (m.group(1).lstrip("0") or "0") in drop:
            os.remove(os.path.join(folder, fn))
            print("  deleted %s" % fn)
            removed += 1
    print("[prune] %s: removed %d, left %d" % (group, removed, count_refs(folder)))
    return 0


# ----------------------------------------------------------------------------- main
def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("cmd", choices=["fetch", "fill", "sheet", "prune", "board"])
    ap.add_argument("--config", required=True)
    ap.add_argument("--group", default=None, help="limit to one group folder")
    ap.add_argument("--reject", default="", help="comma separated file numbers to delete")
    a = ap.parse_args()
    cfg = load_config(a.config)
    os.makedirs(cfg["root"], exist_ok=True)
    if a.cmd == "sheet":
        return cmd_sheet(cfg, a.group) or 0
    if a.cmd == "prune":
        if not a.group:
            print("--group required")
            return 1
        return cmd_prune(cfg, a.group, a.reject)
    return {"fetch": cmd_fetch, "fill": cmd_fill, "board": cmd_board}[a.cmd](cfg) or 0


if __name__ == "__main__":
    sys.exit(main())
