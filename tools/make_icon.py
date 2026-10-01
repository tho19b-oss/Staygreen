#!/usr/bin/env python3
"""Erzeugt src/StayGreen/Assets/icon.ico (gruener Kreis mit weissem Haken).

Reines Python (nur Standardbibliothek). Das Icon wird mit Supersampling
gerendert und als mehrgroessige ICO-Datei mit PNG-Frames geschrieben.

    python3 tools/make_icon.py [ausgabe.ico] [vorschau.png]
"""
import math
import struct
import sys
import zlib

SIZES = (16, 24, 32, 48, 64, 128, 256)

TOP = (0x4B, 0xD8, 0x7A)      # hellgruen
BOTTOM = (0x1B, 0x96, 0x4C)   # dunkelgruen
RING = (0x12, 0x6E, 0x37)

# Haken in normierten Koordinaten (0..1)
CHECK = ((0.27, 0.53), (0.43, 0.69), (0.74, 0.34))
CHECK_HALF_WIDTH = 0.068


def seg_dist(px, py, ax, ay, bx, by):
    dx, dy = bx - ax, by - ay
    t = ((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy)
    t = max(0.0, min(1.0, t))
    cx, cy = ax + t * dx, ay + t * dy
    return math.hypot(px - cx, py - cy)


def sample(x, y):
    """Farbe (r,g,b,a als 0..1-Floats) an normierter Position."""
    cx = cy = 0.5
    r = math.hypot(x - cx, y - cy)
    radius = 0.47
    if r > radius:
        return (0.0, 0.0, 0.0, 0.0)
    # Verlauf von oben nach unten
    t = min(1.0, max(0.0, (y - 0.03) / 0.94))
    col = [TOP[i] + (BOTTOM[i] - TOP[i]) * t for i in range(3)]
    # dunkler Rand
    if r > radius - 0.035:
        k = (r - (radius - 0.035)) / 0.035
        col = [col[i] + (RING[i] - col[i]) * k * 0.8 for i in range(3)]
    # weisser Haken
    d = min(seg_dist(x, y, *CHECK[0], *CHECK[1]), seg_dist(x, y, *CHECK[1], *CHECK[2]))
    if d <= CHECK_HALF_WIDTH:
        col = [255.0, 255.0, 255.0]
    return (col[0], col[1], col[2], 1.0)


def render(size):
    ss = 8 if size <= 64 else 4
    px = bytearray()
    for j in range(size):
        for i in range(size):
            ar = ag = ab = aa = 0.0
            for sj in range(ss):
                for si in range(ss):
                    x = (i + (si + 0.5) / ss) / size
                    y = (j + (sj + 0.5) / ss) / size
                    r, g, b, a = sample(x, y)
                    ar += r * a
                    ag += g * a
                    ab += b * a
                    aa += a
            n = ss * ss
            if aa > 0:
                px += bytes((round(ar / aa), round(ag / aa), round(ab / aa), round(255 * aa / n)))
            else:
                px += b"\x00\x00\x00\x00"
    return bytes(px)


def png(size, rgba):
    def chunk(tag, data):
        c = struct.pack(">I", len(data)) + tag + data
        return c + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

    raw = bytearray()
    stride = size * 4
    for row in range(size):
        raw.append(0)
        raw += rgba[row * stride:(row + 1) * stride]
    return (b"\x89PNG\r\n\x1a\n"
            + chunk(b"IHDR", struct.pack(">IIBBBBB", size, size, 8, 6, 0, 0, 0))
            + chunk(b"IDAT", zlib.compress(bytes(raw), 9))
            + chunk(b"IEND", b""))


def main():
    out = sys.argv[1] if len(sys.argv) > 1 else "src/StayGreen/Assets/icon.ico"
    preview = sys.argv[2] if len(sys.argv) > 2 else None
    frames = []
    for s in SIZES:
        data = png(s, render(s))
        frames.append((s, data))
        if preview and s == 256:
            with open(preview, "wb") as f:
                f.write(data)
    header = struct.pack("<HHH", 0, 1, len(frames))
    offset = 6 + 16 * len(frames)
    entries = b""
    blob = b""
    for s, data in frames:
        entries += struct.pack("<BBBBHHII", 0 if s >= 256 else s, 0 if s >= 256 else s,
                               0, 0, 1, 32, len(data), offset + len(blob))
        blob += data
    with open(out, "wb") as f:
        f.write(header + entries + blob)
    print("geschrieben:", out, "(%d Frames, %d Bytes)" % (len(frames), len(header + entries + blob)))


if __name__ == "__main__":
    main()
