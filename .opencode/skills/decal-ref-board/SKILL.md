---
name: decal-ref-board
description: Collect reference images for textures/decals/motifs and assemble them into a grouped PureRef (.pur) board with per-group title cards, a credits file and a decal-to-ref mapping document. Use when the user asks for reference images, a ref board, a moodboard, "find refs for X", or wants refs organised in PureRef.
metadata:
  generated-by: local
---

# Decal / Texture Reference Board

Turns a list of subjects into **one grouped PureRef board** plus a folder of images and a
credits file, without the user scrolling Pinterest.

## Hard rules (learned the hard way)

1. **Queries must be in English** — the CC sources index English captions. Thai or mixed
   queries return almost nothing.
2. **Query the *subject*, not just the effect.** `"oil stain"` returns abstract textures;
   `"oil stain cardboard box"` returns stains on a parcel, which is the thing being
   modelled. Always append the host surface: `cardboard box` / `parcel` / `package` /
   `carton` / `packaging`.
3. **Never deliver unviewed images.** Run `sheet`, look at every group, then `prune` the
   ones that do not match. A board of 8 wrong images is worse than a board of 3 right ones.
4. **Delete, don't keep "maybe".** A ref that needs a caption to explain why it belongs is
   a wrong ref.
5. **One group = one subject = one row** in the board, with a title card stating what to
   look for.

## Pipeline

```
groups.json  ──► refboard.py fetch   ──►  <root>/<NN_Group>/00_TITLE.png + NN_*.jpg
                                      └──►  <root>/CREDITS.md
                 refboard.py sheet    ──►  <root>/<NN_Group>/_sheet.jpg (review grid)
                 refboard.py prune    ──►  delete the numbers you reject
                 refboard.py board    ──►  <root>/<Name>.pur  (open in PureRef)
```

## Commands

```powershell
$S = ".opencode/skills/decal-ref-board/scripts"

# generic CC sources (thin; good for material studies)
python $S/refboard.py fetch --config groups.json

# specific subjects: browser scrape -> sink -> downloader
python $S/post_sink.py urls.json            # keep running
python $S/download_urls.py urls.json --root <root> --append

# review + pack
python $S/refboard.py sheet --config groups.json
python $S/refboard.py prune --config groups.json --group 01_WaterStain --reject 03,05,11
python $S/refboard.py board --config groups.json
```

All commands accept `--config`. `fetch` skips groups that are already full, so it is safe
to re-run.

## groups.json schema

```json
{
  "root": "D:/path/to/Refs",
  "board_name": "DecalRefs",
  "per_group": 8,
  "min_width": 600,
  "purformat_path": "C:/path/to/PureRef-format",
  "groups": [
    {
      "folder": "01_WaterStain",
      "en": "WATER STAIN",
      "th": "คราบน้ำ",
      "note": "Atlas A [0,0] - NakedEye | stain ON a parcel",
      "look": "tide rim / soaked fiber swell / no hard outline",
      "queries": ["water stain cardboard box", "water damaged cardboard box", "wet cardboard box stain"]
    }
  ]
}
```

- `queries` — 3–5 English phrases, each naming the host surface. The fetcher tries them in
  order and stops at `per_group`.
- `look` — the 2–4 observations that make the ref useful. Rendered on the title card and in
  the mapping doc. This is what turns a board into a spec.
- `min_width` — 600 is the floor for stain-on-cardboard photography; drop to 500 only if a
  group stays empty.

## Sources

| Source | Key needed | Verdict |
|---|---|---|
| **Bing Images via the agent's browser** | no | ✅ **Best hit rate.** Returns real photos of the exact subject. Use this for anything specific ("stain on a parcel"). |
| Openverse API (`api.openverse.org/v1/images`) | no | ⚠️ CC-licensed, but thin and museum-skewed: `"water stain cardboard box"` returns a 1908 shipping trunk. Fallback only. |
| Wikimedia Commons API | no | ⚠️ Same problem, auto-fallback in `fetch`. |
| `icrawler` (Bing) | no | ⚠️ Stalls on some queries; the browser path replaced it. |
| DuckDuckGo `i.js` | no | ❌ Serves a bot challenge. Do not bother. |

### Bing-via-browser procedure (the one that works)

1. Open a tab on `https://www.bing.com/images/search?q=<anything>` so the origin has cookies.
2. Scrape many queries in **one** `evaluate` call — the page itself fetches same-origin, which
   the plain HTTP endpoint refuses:

```js
(async () => {
  const G = { "01_WaterStain": ["water stain cardboard box", "water damaged cardboard box"] };
  const out = {};
  for (const g in G) {
    const acc = [];
    for (const q of G[g]) {
      const r = await fetch('https://www.bing.com/images/search?q=' +
        encodeURIComponent(q) + '&form=HDRSC2&first=1&count=35', {credentials:'include'});
      const html = await r.text();
      const re = /murl(?:&quot;|")\s*:\s*(?:&quot;|")([^"&]+)/g;
      let m;
      while ((m = re.exec(html)) !== null) if (m[1].startsWith('http')) acc.push(m[1]);
    }
    out[g] = [...new Set(acc)].slice(0, 24);
  }
  window.__refs = out;
  await fetch('http://127.0.0.1:8765/save', {method:'POST', body: JSON.stringify(out)});
  return 'ok';
})()
```

3. `python scripts/post_sink.py urls.json` must be running — it catches the POST so the URL
   list never has to travel back through the conversation.
4. `python scripts/download_urls.py urls.json --root <root> [--append]`

Watermarked stock (dreamstime/alamy/shutterstock/…) is skipped first and only used to fill a
starved group — a watermark covers the very stain you are trying to study.

Rate limits are real: anonymous Openverse throttles around 20 req/min, and Commons rejects
bursts. The script retries 429/503/424 with backoff; do not lower the sleeps to "speed it
up", it just produces empty groups.

If a group stays empty after two passes, the query is wrong — not the source. Widen the
host (`parcel` instead of `package`), or drop one qualifier at a time and re-run.

## Reviewing

`sheet` renders one labelled grid per group (`_sheet.jpg`, numbers match file prefixes).
Look at it, then `prune --reject 03,05` for anything that is not the right subject, the
right surface, or readable at decal scale.

Reject if: wrong material (metal/wood instead of board), illustration instead of photo,
subject is off-frame, or the effect is invisible at 300 px.

Files starting with `_` are review-only and are never packed into the board.

## PureRef output

- Header is written as format `1.10`. PureRef 2.x opens it directly (verified on 2.1.3).
- Layout: one row per group, title card first, images scaled to a fixed row height.
- If PureRef ever refuses the file, drag the group folders in by hand — folder order *is*
  the grouping.

## Attribution

`CREDITS.md` records group, file, creator, licence and source page. `License: verify` rows
come from Bing and were not licence-filtered: fine as shape study, not fine to ship.
