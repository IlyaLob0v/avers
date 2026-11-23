// Офлайн-карта города: подложка OpenStreetMap из файла на сервере Аверса (PMTiles), точки домов из базы.
// Интернет не нужен: библиотеки, шрифты и карта лежат на сервере. Подключается из index.html при первом открытии карты.
"use strict";

const Omsk = [73.37, 54.99];
let addressesLoading = false;
let cityAddresses = [], addressesUnavailable = false, listedAddresses = [], mapSearchTimer, mapSearchActive = false;
const mapAttribution = '© <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noopener">участники OpenStreetMap</a>';
let map = null, mapHouses = [], canPlace = false, mapSession = 0;
let mapLibraries;

async function openMap(houseId) {
  const session = ++mapSession;
  show("map");
  $("map-note").textContent = "";
  try {
    await loadHouses(session);
    if (session !== mapSession) return;
    if (!map) await createMap(session);
    if (session !== mapSession) return;
    updateHousePoints();
    if (houseId) focusHouse(houseId);
    map?.resize();
  } catch (err) {
    if (session !== mapSession) return;
    $("map-catalog").textContent = "Доступны дома базы Аверса";
    $("map-note").textContent = mapHouses.length
      ? "Карта недоступна на этом устройстве. Сведения о доме можно открыть через поиск по адресу."
      : err.message;
    if (houseId && mapHouses.length) focusHouse(houseId);
  }
}

function closeMap() {
  mapSession++;
  stopPlacing();
  popup?.remove();
  popup = null;
  map?.remove();
  map = null;
  mapHouses = [];
  cityAddresses = [];
  addressesLoading = addressesUnavailable = false;
  mapSearchActive = false;
  clearTimeout(mapSearchTimer);
  canPlace = false;
  $("map-search").value = "";
  $("map-catalog").textContent = "Поиск по адресам города";
  $("map-missing").hidden = true;
  listHouses([]);
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

async function createMap(session) {
  if (!mapLibraries) {
    document.head.append(Object.assign(document.createElement("link"), { rel: "stylesheet", href: "map/maplibre-gl.css" }));
    mapLibraries = Promise.all(["map/maplibre-gl.js", "map/pmtiles.js", "map/basemaps.js"].map(loadScript))
      .catch(err => { mapLibraries = null; throw err; });
  }
  await mapLibraries;
  if (session !== mapSession) return;
  // Новый вход — новый токен: архив карты подключается заново с ним.
  const protocol = new pmtiles.Protocol();
  maplibregl.removeProtocol("pmtiles");
  maplibregl.addProtocol("pmtiles", protocol.tile);
  const archive = new pmtiles.PMTiles(new pmtiles.FetchSource("/api/mobile/map/tiles", new Headers({ Authorization: "Bearer " + token })));
  protocol.add(archive);

  let header = null;
  try { header = await archive.getHeader(); } catch { /* файла карты нет — показываем только точки домов */ }
  if (session !== mapSession) return;
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
    locale: {
      "NavigationControl.ZoomIn": "Приблизить",
      "NavigationControl.ZoomOut": "Отдалить",
      "ScaleControl.Meters": "м",
      "ScaleControl.Kilometers": "км"
    },
    style: {
      version: 8,
      glyphs: location.origin + "/m/map/fonts/{fontstack}/{range}.pbf",
      sources: header ? { protomaps: { type: "vector", url: "pmtiles:///api/mobile/map/tiles", attribution: mapAttribution } } : {},
      layers: base
    }
  });
  map.addControl(new maplibregl.NavigationControl({ showCompass: false }));
  map.addControl(new maplibregl.ScaleControl({ unit: "metric" }), "bottom-left");
  await new Promise(resolve => {
    map.once("load", resolve);
    map.once("remove", resolve);
  });
  if (session !== mapSession) return;

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
  void loadBuildingAddresses(session);
}

async function loadBuildingAddresses(session) {
  addressesLoading = true;
  addressesUnavailable = false;
  $("map-catalog").textContent = "Загружаем адреса города…";
  searchMapAddresses();
  try {
    const response = await fetch("/api/mobile/map/buildings", { cache: "no-cache", headers: { Authorization: "Bearer " + token } });
    if (session !== mapSession) return;
    if (response.status === 401) { logout(); return; }
    if (response.ok) {
      const buildings = await response.json();
      if (session !== mapSession) return;
      const unique = new Map();
      for (const feature of buildings.features ?? []) {
        const address = feature.properties?.address;
        const center = buildingCenter(feature.geometry?.coordinates);
        if (typeof address !== "string" || !address || !center) continue;
        const entry = addressEntry(address, center);
        if (!unique.has(entry.key)) unique.set(entry.key, entry);
      }
      cityAddresses = [...unique.values()];
      map.addSource("address-buildings", { type: "geojson", data: buildings, attribution: mapAttribution });
      map.addLayer({
        id: "address-buildings", type: "fill", source: "address-buildings", minzoom: 13,
        paint: { "fill-color": "#2b5cb0", "fill-opacity": 0.04 }
      }, "houses");
      map.on("mouseenter", "address-buildings", () => map.getCanvas().style.cursor = "pointer");
      map.on("mouseleave", "address-buildings", () => map.getCanvas().style.cursor = "");
    } else addressesUnavailable = true;
  } catch { if (session === mapSession) addressesUnavailable = true; }
  finally {
    if (session === mapSession) {
      addressesLoading = false;
      $("map-catalog").textContent = addressesUnavailable ? "Справочник города недоступен" : `Адресов города: ${cityAddresses.length.toLocaleString("ru")} · офлайн`;
      searchMapAddresses();
    }
  }
}

async function loadHouses(session) {
  const data = await api("/api/mobile/map/houses");
  if (session !== mapSession) return;
  mapHouses = data.houses;
  canPlace = data.canPlace;
  const missing = mapHouses.filter(h => h.latitude == null).length;
  $("map-missing").hidden = !canPlace || missing === 0;
  $("map-missing").textContent = `Дома без точки: ${missing}`;
  searchMapAddresses();
}

function updateHousePoints() {
  map?.getSource("houses")?.setData({
    type: "FeatureCollection",
    features: mapHouses.filter(h => h.latitude != null).map(h => ({
      type: "Feature",
      geometry: { type: "Point", coordinates: [h.longitude, h.latitude] },
      properties: { id: h.id, address: h.address.replace(/^ул\. /, "").replace(", д. ", " "), fault: h.hasOpenFault }
    }))
  });
}

const simple = s => s.toLowerCase().replaceAll("ё", "е").replace(/[.,]/g, " ").replace(/\s+/g, " ").trim();

const streetTypes = {
  ул: "улица", улица: "улица", проспект: "проспект", просп: "проспект", "пр-т": "проспект", "пр-кт": "проспект",
  пер: "переулок", переулок: "переулок", бульвар: "бульвар", бул: "бульвар", "б-р": "бульвар",
  пл: "площадь", площадь: "площадь", мкр: "микрорайон", микрорайон: "микрорайон"
};
const addressWords = new Set(["город", "омск", ...Object.keys(streetTypes)]);
function addressText(text) {
  return simple(text).replace(/(^|\s)г\s+(?=омск(?:\s|$))/g, "$1").replace(/№/g, " ").replace(/([а-яa-z])(\d)/g, "$1 $2")
    .replace(/(\d)([а-яa-z])/g, "$1 $2")
    .replace(/(^|\s)(?:дом|д)\s+(?=\d)/g, "$1")
    .split(" ").filter(word => word && !addressWords.has(word)).join(" ");
}
const streetText = text => addressText(text).replace(/(\d+)\s*-?\s*[яй](?=\s|$)/g, "$1");
const houseNumber = text => simple(text).replace(/корпус|корп/g, "к").replace(/строение|стр/g, "с").replace(/\s+/g, "");

function addressEntry(address, center, house) {
  const comma = address.lastIndexOf(",");
  const streetAddress = comma < 0 ? address : address.slice(0, comma);
  const street = streetText(streetAddress);
  const type = simple(streetAddress).split(" ").map(word => streetTypes[word]).find(Boolean) ?? "улица";
  const number = comma < 0 ? "" : houseNumber(addressText(address.slice(comma + 1)));
  const compact = street.replaceAll(" ", "");
  return { address, center, house, street, compact, number, full: addressText(address), key: type + "|" + compact + "|" + number };
}

function buildingCenter(coordinates) {
  let minX = Infinity, minY = Infinity, maxX = -Infinity, maxY = -Infinity;
  function visit(values) {
    if (!Array.isArray(values)) return;
    if (typeof values[0] === "number" && typeof values[1] === "number") {
      const [x, y] = values;
      if (!Number.isFinite(x) || !Number.isFinite(y) || Math.abs(x) > 180 || Math.abs(y) > 90) return;
      minX = Math.min(minX, x); minY = Math.min(minY, y);
      maxX = Math.max(maxX, x); maxY = Math.max(maxY, y);
    } else for (const child of values) visit(child);
  }
  visit(coordinates);
  return minX === Infinity ? null : [(minX + maxX) / 2, (minY + maxY) / 2];
}

function listHouses(houses, note) {
  listedAddresses = houses.slice(0, 30);
  const list = $("map-results");
  list.replaceChildren();
  list.hidden = houses.length === 0 && !note;
  if (note) list.append(Object.assign(document.createElement("li"), { className: "muted", textContent: note }));
  if (houses.length > 30) list.append(Object.assign(document.createElement("li"), {
    className: "muted", textContent: `Показаны первые 30 из ${houses.length}. Уточните улицу и номер дома.`
  }));
  $("map-search").setAttribute("aria-expanded", String(!list.hidden));
  for (const h of listedAddresses) {
    const b = document.createElement("button");
    b.textContent = h.address + (h.latitude == null && !h.center ? " · нет на карте" : "");
    b.onclick = () => selectAddress(h);
    const li = document.createElement("li");
    li.append(b);
    list.append(li);
  }
}

function searchMapAddresses() {
  if (!mapSearchActive) return;
  const text = addressText($("map-search").value);
  if (!text) { listHouses([]); return; }
  const match = /(?:^|\s)(\d+\s*[а-яa-z]?(?:[/-]\d+\s*[а-яa-z]?)*(?:\s*(?:к|корпус|корп|строение|стр|с)\s*\d+\s*[а-яa-z]?)?)$/.exec(text);
  const number = match ? houseNumber(match[1]) : "";
  const words = streetText(match ? text.slice(0, match.index) : text).split(" ").filter(Boolean);
  const matches = entry => entry.full === text || words.every(word => /^\d+$/.test(word)
    ? (" " + entry.street + " ").includes(" " + word + " ") : entry.compact.includes(word)) && (!number ||
    entry.number.startsWith(number) && !/\d/.test(entry.number.charAt(number.length)));
  const found = new Map(cityAddresses.filter(matches).map(entry => [entry.key, entry]));
  for (const h of mapHouses) {
    const entry = addressEntry(h.address, h.latitude == null ? null : [h.longitude, h.latitude], h);
    if (!matches(entry)) continue;
    entry.center ??= found.get(entry.key)?.center;
    found.set(entry.key, entry);
  }
  const rows = [...found.values()].sort((a, b) => Number(b.number === number) - Number(a.number === number)
    || a.address.localeCompare(b.address, "ru", { numeric: true }));
  const note = addressesLoading ? "Загружаются адреса города…" : addressesUnavailable
    ? "Адресный справочник города недоступен. Доступны дома Аверса; обратитесь к администратору."
    : rows.length ? "" : "Ничего не найдено. Проверьте улицу и номер дома.";
  listHouses(rows, note);
}

$("map-search").oninput = () => {
  mapSearchActive = true;
  clearTimeout(mapSearchTimer);
  mapSearchTimer = setTimeout(searchMapAddresses, 150);
};

function selectAddress(entry) {
  mapSearchActive = false;
  listHouses([]);
  const house = entry.house ?? (entry.id ? entry : null);
  if (house?.latitude != null || !entry.center) { if (house) focusHouse(house.id); return; }
  if (!map?.getSource("houses")) {
    if (house) openHouse(house.id, true);
    else $("map-note").textContent = entry.address + ". Карта недоступна на этом устройстве.";
    return;
  }
  map.flyTo({ center: entry.center, zoom: Math.max(map.getZoom(), 17) });
  showAddressPopup(entry.address, entry.center, house);
}

$("map-search").addEventListener("keydown", e => {
  if (e.key === "Enter" && !e.isComposing) {
    clearTimeout(mapSearchTimer);
    searchMapAddresses();
    if (listedAddresses.length) { e.preventDefault(); selectAddress(listedAddresses[0]); }
  }
  if (e.key === "Escape") { mapSearchActive = false; listHouses([]); }
});

$("map-missing").onclick = () => {
  mapSearchActive = false;
  listHouses(mapHouses.filter(h => h.latitude == null), "Нажмите дом, затем место на карте, где он стоит.");
};

function focusHouse(id) {
  const h = mapHouses.find(x => x.id === id);
  if (!h) return;
  if (!map?.getSource("houses")) {
    openHouse(id, true);
    return;
  }
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
  if (map) map.getCanvas().style.cursor = "";
}

document.addEventListener("keydown", e => { if (e.key === "Escape" && placing) { stopPlacing(); $("map-note").textContent = ""; } });
$("map-search").addEventListener("focus", () => {
  if (placing) { stopPlacing(); $("map-note").textContent = ""; }
  mapSearchActive = true;
  searchMapAddresses();
});

function showBuildingAddress(e) {
  if (map.queryRenderedFeatures(e.point, { layers: ["houses"] }).length) return;
  const addressed = map.getLayer("address-buildings")
    ? map.queryRenderedFeatures(e.point, { layers: ["address-buildings"] })[0] : null;
  const building = addressed ?? map.queryRenderedFeatures(e.point).find(f => f.sourceLayer === "buildings");
  if (!building) return;
  const address = addressed?.properties.address;
  const number = building.properties?.addr_housenumber;
  showAddressPopup(address || (number ? "Дом № " + number : "Здание"), e.lngLat, null,
    address ? "" : addressesLoading ? "Адреса зданий загружаются… Нажмите ещё раз." : "Полный адрес отсутствует в загруженных данных.");
}

function showAddressPopup(address, point, house, note) {
  const box = document.createElement("div");
  box.append(Object.assign(document.createElement("b"), { textContent: address }));
  if (note) box.append(Object.assign(document.createElement("div"), { textContent: note }));
  if (house) {
    const card = Object.assign(document.createElement("button"), { textContent: "Сведения о доме" });
    card.onclick = () => openHouse(house.id, true);
    box.append(card);
  }
  popup?.remove();
  popup = new maplibregl.Popup({ maxWidth: "280px" }).setLngLat(point).setDOMContent(box).addTo(map);
}

function onMapClick(e) {
  if (!placing) { showBuildingAddress(e); return; }
  const h = placing;
  const session = mapSession;
  stopPlacing();
  if (!confirm(`Поставить дом «${h.address}» в это место?`)) { $("map-note").textContent = ""; return; }
  fetch(`/api/mobile/map/houses/${h.id}`, {
    method: "PUT",
    headers: { Authorization: "Bearer " + token, "Content-Type": "application/json" },
    body: JSON.stringify({ latitude: e.lngLat.lat, longitude: e.lngLat.lng })
  }).then(async r => {
    if (session !== mapSession) return;
    if (r.status === 401) { logout(); return; }
    if (r.status === 403) throw new Error("Ставить дома на карту может главный инженер или администратор.");
    if (!r.ok) throw new Error("Ошибка сервера: " + r.status);
    await loadHouses(session);
    if (session !== mapSession) return;
    updateHousePoints();
    $("map-note").textContent = `Дом «${h.address}» отмечен на карте.`;
    showPopup(mapHouses.find(x => x.id === h.id));
  }).catch(err => { if (session === mapSession) $("map-note").textContent = err.message; });
}
