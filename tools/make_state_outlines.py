"""Builds Assets/Geo/us-states.geojson from the county files.

A state's outline is the set of county edges that belong to exactly one county polygon (shared edges
between neighbouring counties are interior and dropped). The result follows the same detailed coastline
as the county lines, unlike the coarse stock states file. State id/name are kept from the existing file.

Run from the repo root:  python tools/make_state_outlines.py
"""
import json
import os
from collections import defaultdict

GEO = os.path.join("src", "AstroValley.Presentation", "Assets", "Geo")
STATES_FILE = os.path.join(GEO, "us-states.geojson")
COUNTY_DIR = os.path.join(GEO, "counties")


def rings(geometry):
    polys = [geometry["coordinates"]] if geometry["type"] == "Polygon" else geometry["coordinates"]
    for poly in polys:
        yield from poly


def boundary_lines(counties):
    """Chain the once-only edges of all county rings into polylines."""
    count = defaultdict(int)
    for f in counties["features"]:
        for ring in rings(f["geometry"]):
            for a, b in zip(ring, ring[1:]):
                a, b = tuple(a), tuple(b)
                if a != b:
                    count[(a, b) if a < b else (b, a)] += 1

    adj = defaultdict(list)
    for (a, b), n in count.items():
        if n == 1:
            adj[a].append(b)
            adj[b].append(a)

    used = set()
    lines = []
    for start in list(adj):
        for first in adj[start]:
            if frozenset((start, first)) in used:
                continue
            line = [start, first]
            used.add(frozenset((start, first)))
            while True:  # extend forward until a junction/dead end/loop closes
                tail, prev = line[-1], line[-2]
                nxt = [n for n in adj[tail] if frozenset((tail, n)) not in used]
                if not nxt:
                    break
                line.append(nxt[0])
                used.add(frozenset((tail, nxt[0])))
            lines.append([list(p) for p in line])
    return lines


def main():
    with open(STATES_FILE, encoding="utf-8") as fh:
        states = json.load(fh)

    for feature in states["features"]:
        path = os.path.join(COUNTY_DIR, feature["id"] + ".geojson")
        with open(path, encoding="utf-8") as fh:
            lines = boundary_lines(json.load(fh))
        feature["geometry"] = {"type": "MultiLineString", "coordinates": lines}
        print(feature["properties"]["name"], len(lines), "lines")

    with open(STATES_FILE, "w", encoding="utf-8") as fh:
        json.dump(states, fh, separators=(",", ":"))


if __name__ == "__main__":
    main()
