"""Convert an Overpass JSON export with building geometry into offline GeoJSON."""
import argparse
import json
from pathlib import Path


def building_features(elements):
    for element in elements:
        tags = element.get("tags", {})
        street = tags.get("addr:street") or tags.get("addr:place")
        number = tags.get("addr:housenumber")
        if not tags.get("building") or not street or not number:
            continue
        coordinates = [[p["lon"], p["lat"]] for p in element.get("geometry", [])]
        if len(coordinates) < 4 or coordinates[0] != coordinates[-1]:
            continue
        address = ", ".join(filter(None, [tags.get("addr:city"), street, number]))
        yield {
            "type": "Feature",
            "geometry": {"type": "Polygon", "coordinates": [coordinates]},
            "properties": {"address": address},
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
