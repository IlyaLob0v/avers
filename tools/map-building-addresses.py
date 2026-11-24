"""Convert an Overpass JSON export with building geometry into offline GeoJSON."""
import argparse
import json
import math
from pathlib import Path


def coordinates(points):
    result = [[p.get("lon"), p.get("lat")] for p in points]
    if any(not isinstance(v, (int, float)) or isinstance(v, bool) or not math.isfinite(v)
           for point in result for v in point):
        return []
    return result if all(abs(x) <= 180 and abs(y) <= 90 for x, y in result) else []


def rings(members):
    pending = [coordinates(m.get("geometry", [])) for m in members]
    if any(len(part) < 2 for part in pending):
        return None
    result = []
    while pending:
        ring = pending.pop()
        while ring[0] != ring[-1]:
            for i, part in enumerate(pending):
                if part[0] == ring[-1] or part[-1] == ring[-1]:
                    ring.extend((part if part[0] == ring[-1] else part[::-1])[1:])
                    pending.pop(i)
                    break
            else:
                return None
        if len(ring) < 4:
            return None
        result.append(ring)
    return result


def inside(point, ring):
    x, y = point
    found = False
    for (xi, yi), (xj, yj) in zip(ring, ring[1:]):
        if (yi > y) != (yj > y) and x < (xj - xi) * (y - yi) / (yj - yi) + xi:
            found = not found
    return found


def geometry(element):
    if element.get("type") == "node":
        point = coordinates([element])
        return {"type": "Point", "coordinates": point[0]} if point else None
    if not element.get("tags", {}).get("building"):
        return None
    if element.get("type") == "relation":
        if element.get("tags", {}).get("type") != "multipolygon":
            return None
        members = [m for m in element.get("members", []) if m.get("type") == "way"]
        outer = rings([m for m in members if m.get("role", "") in ("", "outer")])
        inner = rings([m for m in members if m.get("role") == "inner"])
        if not outer or inner is None:
            return None
        polygons = [[ring] for ring in outer]
        for hole in inner:
            owners = [polygon for polygon in polygons if inside(hole[0], polygon[0])]
            if len(owners) != 1:
                return None
            owners[0].append(hole)
        return {"type": "MultiPolygon", "coordinates": polygons}
    ring = coordinates(element.get("geometry", []))
    return {"type": "Polygon", "coordinates": [ring]} if len(ring) >= 4 and ring[0] == ring[-1] else None


def building_features(elements):
    for element in elements:
        tags = element.get("tags", {})
        street = tags.get("addr:street") or tags.get("addr:place")
        number = tags.get("addr:housenumber")
        if not street or not number:
            continue
        shape = geometry(element)
        if not shape:
            continue
        address = ", ".join(filter(None, [tags.get("addr:city"), street, number]))
        yield {
            "type": "Feature",
            "geometry": shape,
            "properties": {"address": address, "housenumber": number},
        }


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("source", type=Path)
    parser.add_argument("destination", type=Path)
    args = parser.parse_args()
    data = json.loads(args.source.read_text(encoding="utf-8"))
    if data.get("remark"):
        raise SystemExit("Incomplete Overpass export. The existing catalogue was kept.")
    args.destination.parent.mkdir(parents=True, exist_ok=True)
    temporary = args.destination.with_suffix(".tmp")
    count = 0
    with temporary.open("w", encoding="utf-8") as stream:
        stream.write('{"type":"FeatureCollection","features":[')
        for feature in building_features(data.get("elements", [])):
            if count:
                stream.write(",")
            json.dump(feature, stream, ensure_ascii=False, separators=(",", ":"))
            count += 1
        stream.write("]}")
    if not count:
        temporary.unlink()
        raise SystemExit("No addressed buildings found. The existing catalogue was kept.")
    temporary.replace(args.destination)
    print(f"Saved {count} addressed buildings.")


if __name__ == "__main__":
    main()
