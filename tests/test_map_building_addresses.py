import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest

SCRIPT = Path(__file__).resolve().parents[1] / "tools" / "map-building-addresses.py"
spec = importlib.util.spec_from_file_location("map_addresses", SCRIPT)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


def points(values):
    return [{"lon": x, "lat": y} for x, y in values]


class AddressImportTests(unittest.TestCase):
    def test_address_node_does_not_need_building_tag(self):
        features = list(module.building_features([{
            "type": "node", "lat": 55, "lon": 73.4,
            "tags": {"addr:street": "улица Тестовая", "addr:housenumber": "12 к2"}
        }]))
        self.assertEqual(features[0]["geometry"], {"type": "Point", "coordinates": [73.4, 55]})
        self.assertEqual(features[0]["properties"], {"address": "улица Тестовая, 12 к2", "housenumber": "12 к2"})

    def test_building_preserves_full_address_and_geometry(self):
        ring = points([(73, 55), (73.1, 55), (73.1, 55.1), (73, 55)])
        feature, = module.building_features([{
            "type": "way", "geometry": ring,
            "tags": {"building": "yes", "addr:city": "Омск", "addr:place": "Тестовый посёлок", "addr:housenumber": "4/2"}
        }])
        self.assertEqual(feature["properties"]["address"], "Омск, Тестовый посёлок, 4/2")
        self.assertEqual(feature["geometry"]["type"], "Polygon")

    def test_relation_joins_reversed_segments_and_keeps_courtyard(self):
        outer = [(73, 55), (73.1, 55), (73.1, 55.1), (73, 55.1), (73, 55)]
        inner = [(73.02, 55.02), (73.03, 55.02), (73.03, 55.03), (73.02, 55.02)]
        other = [(74, 55), (74.1, 55), (74.1, 55.1), (74, 55)]
        relation = {"type": "relation", "tags": {"building": "yes", "type": "multipolygon"}, "members": [
            {"type": "way", "role": "outer", "geometry": points(outer[:3])},
            {"type": "way", "role": "outer", "geometry": points(outer[2:][::-1])},
            {"type": "way", "role": "inner", "geometry": points(inner)},
            {"type": "way", "role": "outer", "geometry": points(other)}
        ]}
        geometry = module.geometry(relation)
        self.assertEqual(geometry["type"], "MultiPolygon")
        self.assertEqual(len(geometry["coordinates"]), 2)
        self.assertEqual(sorted(map(len, geometry["coordinates"])), [1, 2])
        self.assertIn([list(point) for point in inner], [hole for polygon in geometry["coordinates"] for hole in polygon[1:]])
        relation["members"].pop(1)
        self.assertIsNone(module.geometry(relation))

    def test_missing_address_invalid_coordinate_and_unclosed_ring_are_skipped(self):
        tags = {"building": "yes", "addr:street": "Тестовая", "addr:housenumber": "1"}
        for element in [
            {"type": "node", "lat": 100, "lon": 73, "tags": tags},
            {"type": "node", "lat": float("nan"), "lon": 73, "tags": tags},
            {"type": "node", "lat": 55, "lon": 73, "tags": {"addr:housenumber": "1"}},
            {"type": "way", "geometry": points([(73, 55), (74, 55), (74, 56), (73, 56)]), "tags": tags}
        ]:
            with self.subTest(element=element):
                self.assertEqual(list(module.building_features([element])), [])

    def test_incomplete_or_empty_export_keeps_installed_catalogue(self):
        with tempfile.TemporaryDirectory() as directory:
            source, target = Path(directory) / "source.json", Path(directory) / "buildings.geojson"
            target.write_text("existing catalogue", encoding="utf-8")
            for data in [{"remark": "runtime timeout", "elements": []}, {"elements": []}]:
                source.write_text(json.dumps(data), encoding="utf-8")
                result = subprocess.run([sys.executable, str(SCRIPT), str(source), str(target)], capture_output=True)
                self.assertNotEqual(result.returncode, 0)
                self.assertEqual(target.read_text(encoding="utf-8"), "existing catalogue")


if __name__ == "__main__":
    unittest.main()
