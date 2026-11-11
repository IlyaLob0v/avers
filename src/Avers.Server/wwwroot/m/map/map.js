// Офлайн-карта города: подложка OpenStreetMap из файла на сервере Аверса (PMTiles), точки домов из базы.
// Интернет не нужен: библиотеки, шрифты и карта лежат на сервере. Подключается из index.html при первом открытии карты.
"use strict";

const Omsk = [73.37, 54.99];
let map = null, mapHouses = [], canPlace = false;

async function openMap(houseId) {
  show("map");
  $("map-note").textContent = "";
  try {
    if (!map) await createMap();
    await loadHouses();
    if (houseId) focusHouse(houseId);
  } catch (err) { $("map-note").textContent = err.message; }
  map?.resize();
}

function closeMap() {
  map?.remove();
  map = null;
}

function loadScript(src) {
  return new Promise((resolve, reject) => {
    const s = document.createElement("script");
    s.src = src;
    s.onload = resolve;
    s.onerror = () => reject(new Error("Не удалось загрузить карту с сервера."));
    document.head.append(s);
  });
}

async function createMap() {
  if (!window.maplibregl) {
    document.head.append(Object.assign(document.createElement("link"), { rel: "stylesheet", href: "map/maplibre-gl.css" }));
    await Promise.all(["map/maplibre-gl.js", "map/pmtiles.js", "map/basemaps.js"].map(loadScript));
  }
  // Новый вход — новый токен: архив карты подключается заново с ним.
  const protocol = new pmtiles.Protocol();
  maplibregl.removeProtocol("pmtiles");
  maplibregl.addProtocol("pmtiles", protocol.tile);
  const archive = new pmtiles.PMTiles(new pmtiles.FetchSource("/api/mobile/map/tiles", new Headers({ Authorization: "Bearer " + token })));
  protocol.add(archive);

  let header = null;
  try { header = await archive.getHeader(); } catch { /* файла карты нет — показываем только точки домов */ }
  if (!header)
    $("map-note").textContent = "Карта города ещё не загружена на сервер, видны только точки домов. Как загрузить — в инструкции администратора (avers-map).";

  const base = header
    ? JSON.parse(JSON.stringify(basemaps.layers("protomaps", basemaps.namedFlavor("light"), { lang: "ru" })).replaceAll("Noto Sans ", ""))
        .map(l => { if (l.layout) delete l.layout["icon-image"]; return l; })
    : [{ id: "background", type: "background", paint: { "background-color": "#e8e4dc" } }];

  map = new maplibregl.Map({
    container: "map-canvas",
    center: header ? [header.centerLon, header.centerLat] : Omsk,
    zoom: header ? Math.max(header.centerZoom, 11) : 11,
    attributionControl: { compact: true },
    style: {
      version: 8,
      glyphs: location.origin + "/m/map/fonts/{fontstack}/{range}.pbf",
      sources: header ? { protomaps: { type: "vector", url: "pmtiles:///api/mobile/map/tiles", attribution: "© участники OpenStreetMap" } } : {},
      layers: base
    }
  });
  map.addControl(new maplibregl.NavigationControl({ showCompass: false }));
  await new Promise(resolve => map.once("load", resolve));

  map.addSource("houses", { type: "geojson", data: { type: "FeatureCollection", features: [] } });
  map.addLayer({
    id: "houses", type: "circle", source: "houses",
    paint: {
      "circle-radius": ["interpolate", ["linear"], ["zoom"], 11, 4, 17, 9],
      "circle-color": ["case", ["get", "fault"], "#c62828", "#2b5cb0"],
      "circle-stroke-color": "#fff", "circle-stroke-width": 1.5
    }
  });
  map.addLayer({
    id: "houses-labels", type: "symbol", source: "houses", minzoom: 15,
    layout: { "text-field": ["get", "address"], "text-font": ["Medium"], "text-size": 12, "text-offset": [0, 1.1], "text-anchor": "top" },
    paint: { "text-color": "#0a2f5c", "text-halo-color": "#fff", "text-halo-width": 1.5 }
  });
  map.on("click", onMapClick);
  map.on("click", "houses", e => placing || showPopup(mapHouses.find(h => h.id === e.features[0].properties.id)));
  map.on("mouseenter", "houses", () => map.getCanvas().style.cursor = "pointer");
  map.on("mouseleave", "houses", () => map.getCanvas().style.cursor = "");
}

async function loadHouses() {
  const data = await api("/api/mobile/map/houses");
  mapHouses = data.houses;
  canPlace = data.canPlace;
  map.getSource("houses").setData({
    type: "FeatureCollection",
    features: mapHouses.filter(h => h.latitude != null).map(h => ({
      type: "Feature",
      geometry: { type: "Point", coordinates: [h.longitude, h.latitude] },
      properties: { id: h.id, address: h.address.replace(/^ул\. /, "").replace(", д. ", " "), fault: h.hasOpenFault }
    }))
  });
  const missing = mapHouses.filter(h => h.latitude == null).length;
  $("map-missing").hidden = !canPlace || missing === 0;
  $("map-missing").textContent = `Дома без точки: ${missing}`;
}

const simple = s => s.toLowerCase().replaceAll("ё", "е").replace(/[.,]/g, " ").replace(/\s+/g, " ").trim();

function listHouses(houses, note) {
  const list = $("map-results");
  list.replaceChildren();
  list.hidden = houses.length === 0 && !note;
  if (note) list.append(Object.assign(document.createElement("li"), { className: "muted", textContent: note }));
  for (const h of houses.slice(0, 30)) {
    const b = document.createElement("button");
    b.textContent = h.address + (h.latitude == null ? " · нет на карте" : "");
    b.onclick = () => { list.hidden = true; focusHouse(h.id); };
    const li = document.createElement("li");
    li.append(b);
    list.append(li);
  }
}

$("map-search").oninput = () => {
  const words = simple($("map-search").value).split(" ").filter(w => w);
  if (words.length === 0) { listHouses([]); return; }
  const found = mapHouses.filter(h => { const a = simple(h.address); return words.every(w => a.includes(w)); });
  listHouses(found, found.length ? "" : "Ничего не найдено.");
};

$("map-missing").onclick = () => listHouses(mapHouses.filter(h => h.latitude == null),
  "Нажмите дом, затем место на карте, где он стоит.");

function focusHouse(id) {
  const h = mapHouses.find(x => x.id === id);
  if (!h) return;
  if (h.latitude == null) {
    if (canPlace) startPlacing(h);
    else $("map-note").textContent = `Дом «${h.address}» ещё не отмечен на карте. Отметить его может главный инженер или администратор.`;
    return;
  }
  map.flyTo({ center: [h.longitude, h.latitude], zoom: Math.max(map.getZoom(), 16) });
  showPopup(h);
}

let popup = null;
function showPopup(h) {
  popup?.remove();
  const box = document.createElement("div");
  box.append(Object.assign(document.createElement("b"), { textContent: h.address }));
  if (h.hasOpenFault) box.append(Object.assign(document.createElement("div"), { className: "fault-note", textContent: "Есть неустранённая неисправность" }));
  const card = Object.assign(document.createElement("button"), { textContent: "Сведения о доме" });
  card.onclick = () => openHouse(h.id, true);
  box.append(card);
  if (canPlace) {
    const move = Object.assign(document.createElement("button"), { textContent: "Перенести точку" });
    move.onclick = () => startPlacing(h);
    box.append(move);
  }
  popup = new maplibregl.Popup({ offset: 10, maxWidth: "260px" }).setLngLat([h.longitude, h.latitude]).setDOMContent(box).addTo(map);
}

let placing = null;
function startPlacing(h) {
  popup?.remove();
  placing = h;
  $("map-note").textContent = `Нажмите на карте место дома «${h.address}». Отмена — кнопка Esc или снова поиск.`;
  map.getCanvas().style.cursor = "crosshair";
}

function stopPlacing() {
  placing = null;
  map.getCanvas().style.cursor = "";
}

document.addEventListener("keydown", e => { if (e.key === "Escape" && placing) { stopPlacing(); $("map-note").textContent = ""; } });
$("map-search").addEventListener("focus", () => { if (placing) { stopPlacing(); $("map-note").textContent = ""; } });

function onMapClick(e) {
  if (!placing) return;
  const h = placing;
  stopPlacing();
  if (!confirm(`Поставить дом «${h.address}» в это место?`)) { $("map-note").textContent = ""; return; }
  fetch(`/api/mobile/map/houses/${h.id}`, {
    method: "PUT",
    headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
    body: JSON.stringify({ latitude: e.lngLat.lat, longitude: e.lngLat.lng })
  }).then(async r => {
    if (r.status === 401) { logout(); return; }
    if (r.status === 403) throw new Error("Ставить дома на карту может главный инженер или администратор.");
    if (!r.ok) throw new Error("Ошибка сервера: " + r.status);
    await loadHouses();
    $("map-note").textContent = `Дом «${h.address}» отмечен на карте.`;
    showPopup(mapHouses.find(x => x.id === h.id));
  }).catch(err => $("map-note").textContent = err.message);
}
