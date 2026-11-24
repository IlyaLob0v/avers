const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const vm = require('node:vm');
const path = require('node:path');

const source = fs.readFileSync(path.join(__dirname, '../src/Avers.Server/wwwroot/m/map/map.js'), 'utf8');

function building(address, longitude = 73.4, latitude = 55) {
  return { type: 'Feature', properties: { address }, geometry: { type: 'Polygon', coordinates: [[
    [longitude, latitude], [longitude + .002, latitude], [longitude + .002, latitude + .002],
    [longitude, latitude + .002], [longitude, latitude]
  ]] } };
}

function browser(features, houses = [], response) {
  class Element {
    constructor(tagName) { this.tagName = tagName; this.children = []; this.value = ''; this.textContent = ''; this.hidden = true; this.events = {}; }
    append(...nodes) { this.children.push(...nodes); }
    replaceChildren(...nodes) { this.children = nodes; }
    addEventListener(name, action) { this.events[name] = action; }
    setAttribute(name, value) { this[name] = value; }
    focus() { this.focused = true; }
  }
  const elements = new Map();
  const context = vm.createContext({
    console,
    $: id => { if (!elements.has(id)) elements.set(id, new Element(id)); return elements.get(id); },
    document: { addEventListener() {}, createElement: tag => new Element(tag), head: new Element('head') },
    setTimeout: action => { action(); return 1; }, clearTimeout() {},
    fetch: response ?? (async () => ({ ok: true, json: async () => ({ type: 'FeatureCollection', features }) })),
    token: 'учебный токен', houses,
    api: async () => ({ houses, canPlace: false }),
    openHouse(id) { context.openedHouse = id; },
    logout() { context.signedOut = true; },
    maplibregl: { Popup: class {
      constructor() { context.lastPopup = this; }
      remove() {} setLngLat(point) { this.point = point; return this; }
      setDOMContent(box) { this.box = box; return this; } addTo() { return this; }
    } },
    fakeMap: {
      addSource() {}, addLayer() {}, on() {}, remove() {}, getCanvas: () => ({ style: {} }),
      getLayer: () => true, getSource: () => ({ setData() {} }), getZoom: () => 12,
      flyTo(options) { context.destination = options; },
      queryRenderedFeatures: (_, options) => options?.layers?.[0] === 'houses' ? []
        : [{ properties: { address: 'улица Ильичёва, 4А' } }]
    }
  });
  vm.runInContext(source, context);
  vm.runInContext('map = fakeMap; mapHouses = houses;', context);
  return {
    context, elements,
    load: () => vm.runInContext('loadBuildingAddresses(mapSession)', context),
    search(query) { const input = context.$('map-search'); input.value = query; input.oninput(); },
    buttons: () => context.$('map-results').children.flatMap(li => li.children).filter(e => e.tagName === 'button'),
    note: () => context.$('map-results').children.map(e => e.textContent).join(' ')
  };
}

test('Находит городской адрес вне базы Аверса и перемещает карту к зданию', async () => {
  const page = browser([building('улица Ильичёва, 4А')]);
  await page.load();
  page.search('г. Омск, ул. Ильичева, д. 4А');
  const buttons = page.buttons();
  assert.equal(buttons.length, 1);
  assert.equal(buttons[0].textContent, 'улица Ильичёва, 4А');
  buttons[0].onclick();
  assert.ok(Math.abs(page.context.destination.center[0] - 73.401) < 1e-8);
  assert.ok(Math.abs(page.context.destination.center[1] - 55.001) < 1e-8);
  assert.equal(page.context.lastPopup.box.children[0].textContent, 'улица Ильичёва, 4А');
  assert.equal(page.context.lastPopup.box.children.length, 1);
});

test('Часть улицы, буква, дробь и корпус в номере дома; 4 не превращается в 40', async () => {
  const page = browser([building('улица Ильичёва, 4А'), building('улица Ильичёва, 40'),
    building('улица Ильичёва, 4/2'), building('улица Ильичёва, 4 корпус 1')]);
  await page.load();
  page.search('ильич 4');
  assert.equal(page.buttons().length, 3);
  page.search('Ильичёва 4/2');
  assert.equal(page.buttons().length, 1);
  assert.match(page.buttons()[0].textContent, /4\/2$/);
  page.search('ильичева 4 к 1');
  assert.equal(page.buttons().length, 1);
  assert.match(page.buttons()[0].textContent, /корпус 1$/);
});

test('Сохраняет собственную точку Аверса и убирает дубли городского адреса', async () => {
  const page = browser([building('Омск, улица Ильичёва, 4А'), building('улица Ильичёва, 4А')],
    [{ id: 7, address: 'ул. Ильичёва, д. 4А', latitude: 55.1, longitude: 73.3 }]);
  await page.load();
  page.search('ильичева 4а');
  assert.equal(page.buttons().length, 1);
  page.buttons()[0].onclick();
  assert.equal(page.context.destination.center[0], 73.3);
  assert.equal(page.context.lastPopup.box.children[1].textContent, 'Сведения о доме');
});

test('Повторяет запрос после загрузки адресов, введённых пользователем заранее', async () => {
  let complete;
  const response = new Promise(resolve => { complete = resolve; });
  const page = browser([], [], () => response);
  const loading = page.load();
  page.search('ильичева 4а');
  assert.match(page.note(), /Загружаются адреса/);
  complete({ ok: true, json: async () => ({ type: 'FeatureCollection', features: [building('улица Ильичёва, 4А')] }) });
  await loading;
  assert.equal(page.buttons().length, 1);
});

test('Ограничивает список, поясняет лимит и не ищет 10 как часть 100', async () => {
  const page = browser(Array.from({ length: 40 }, (_, i) => building(`улица Тестовая, ${i + 1}`)));
  await page.load();
  page.search('тестовая');
  assert.equal(page.buttons().length, 30);
  assert.match(page.note(), /40/);
  page.search('тестовая 10');
  assert.equal(page.buttons().length, 1);
  page.search('');
  assert.equal(page.context.$('map-results').hidden, true);
});

test('Удаляет только плашку источника у адреса здания', () => {
  const page = browser([]);
  vm.runInContext('showBuildingAddress({ point: {}, lngLat: [73.4, 55] })', page.context);
  assert.equal(page.context.lastPopup.box.children.length, 1);
  assert.equal(page.context.lastPopup.box.children[0].textContent, 'улица Ильичёва, 4А');
});

test('Выбор по Enter закрывает подсказки; фокус снова открывает поиск', async () => {
  const page = browser([building('улица Ильичёва, 4А')]);
  await page.load();
  page.search('ильичева 4а');
  let prevented = false;
  page.context.$('map-search').events.keydown({ key: 'Enter', preventDefault() { prevented = true; } });
  assert.equal(prevented, true);
  assert.equal(page.context.$('map-results').hidden, true);
  assert.ok(page.context.destination);
  page.context.$('map-search').events.focus();
  assert.equal(page.context.$('map-results').hidden, false);
});

test('Загрузка города после выбора не раскрывает список повторно', async () => {
  let complete;
  const page = browser([], [{ id: 7, address: 'ул. Ильичёва, д. 4А', latitude: 55, longitude: 73.4 }],
    () => new Promise(resolve => { complete = resolve; }));
  const loading = page.load();
  page.search('ильичева 4а');
  page.buttons()[0].onclick();
  complete({ ok: true, json: async () => ({ type: 'FeatureCollection', features: [building('улица Ильичёва, 4А')] }) });
  await loading;
  assert.equal(page.context.$('map-results').hidden, true);
});

test('Выход во время загрузки не возвращает адреса старого сеанса', async () => {
  let complete;
  const page = browser([], [], () => new Promise(resolve => { complete = resolve; }));
  const loading = page.load();
  vm.runInContext('closeMap()', page.context);
  complete({ ok: true, json: async () => ({ type: 'FeatureCollection', features: [building('улица Ильичёва, 4А')] }) });
  await loading;
  page.search('ильичева 4а');
  assert.equal(page.buttons().length, 0);
});

test('Сообщает о недоступном городском справочнике, сохраняя поиск Аверса', async () => {
  const page = browser([], [{ id: 7, address: 'ул. Ильичёва, д. 4А', latitude: 55, longitude: 73.4 }],
    async () => ({ ok: false, status: 404 }));
  await page.load();
  page.search('ильичева 4а');
  assert.equal(page.buttons().length, 1);
  assert.match(page.note(), /недоступен/);
});

test('Различает номера улиц, находит слитный адрес и составной номер', async () => {
  const page = browser([building('1-я Северная улица, 15'), building('11-я Северная улица, 15'),
    building('улица 10 лет Октября, 12-14')]);
  await page.load();
  page.search('1 Северная 15');
  assert.equal(page.buttons().length, 1);
  page.search('10летоктября12-14');
  assert.equal(page.buttons().length, 1);
});

test('Неотмеченный дом Аверса находится по зданию без записи новых координат', async () => {
  const house = { id: 7, address: 'ул. Ильичёва, д. 4А', latitude: null, longitude: null };
  const page = browser([building('улица Ильичёва, 4А')], [house]);
  await page.load();
  page.search('ильичева4а');
  assert.equal(page.buttons().length, 1);
  page.buttons()[0].onclick();
  assert.ok(page.context.destination);
  assert.equal(house.latitude, null);
  assert.equal(page.context.lastPopup.box.children[1].textContent, 'Сведения о доме');
});

test('Ищет адрес в конце каталога из 56 тысяч зданий', async () => {
  const page = browser(Array.from({ length: 56000 }, (_, i) => building(`улица Тестовая, ${i + 1}`)));
  await page.load();
  page.search('тестовая 56000');
  assert.equal(page.buttons().length, 1);
  assert.equal(page.buttons()[0].textContent, 'улица Тестовая, 56000');
});

test('Не теряет букву Г и разные улицы с одинаковым названием', async () => {
  const page = browser([building('бульвар Победы, 5'), building('улица Победы, 5'),
    building('улица Серова, 8Г'), building('улица Серова, 8'),
    building('4-я Путевая улица, 4Я'), building('4-я Путевая улица, 4')]);
  await page.load();
  page.search('победы 5');
  assert.equal(page.buttons().length, 2);
  page.search('серова 8г');
  assert.equal(page.buttons().length, 1);
  assert.equal(page.buttons()[0].textContent, 'улица Серова, 8Г');
  page.search('4-я путевая 4');
  assert.equal(page.buttons().length, 2);
  page.search('4-я путевая 4я');
  assert.equal(page.buttons().length, 1);
  assert.equal(page.buttons()[0].textContent, '4-я Путевая улица, 4Я');
});

test('Находит букву в номере корпуса', async () => {
  const page = browser([building('Индустриальная улица, 11 к2А')]);
  await page.load();
  page.search('Индустриальная 11к2а');
  assert.equal(page.buttons().length, 1);
  assert.equal(page.buttons()[0].textContent, 'Индустриальная улица, 11 к2А');
});

test('Находит полный адрес с нестандартным обозначением литера', async () => {
  const page = browser([building('улица 70 лет Октября, 25 литЛ'), building('Партизанская улица, 5А лит С')]);
  await page.load();
  for (const address of ['улица 70 лет Октября, 25 литЛ', 'Партизанская улица, 5А лит С']) {
    page.search(address);
    assert.equal(page.buttons().length, 1);
    assert.equal(page.buttons()[0].textContent, address);
  }
});

test('Enter после быстрого изменения запроса выбирает новый адрес', async () => {
  const page = browser([building('улица Серова, 8'), building('улица Серова, 10')]);
  await page.load();
  page.search('серова 8');
  page.context.setTimeout = () => 1;
  page.search('серова 10');
  page.context.$('map-search').events.keydown({ key: 'Enter', preventDefault() {} });
  assert.equal(page.context.lastPopup.box.children[0].textContent, 'улица Серова, 10');
});

test('Показывает готовность городского справочника до ввода запроса', async () => {
  const page = browser([building('улица Серова, 8'), building('улица Серова, 10')]);
  await page.load();
  assert.match(page.context.$('map-catalog').textContent, /Адресов города: 2/);
  const missing = browser([], [], async () => ({ ok: false, status: 404 }));
  await missing.load();
  assert.match(missing.context.$('map-catalog').textContent, /недоступен/);
});

function houseListPage(response) {
  const page = browser([], [], response);
  const elementById = page.context.$;
  Object.assign(page.context.document, {
    getElementById: elementById,
    documentElement: { style: { setProperty() {} } },
    querySelector: () => ({ offsetHeight: 48 })
  });
  page.context.window = { closeMap: () => { page.context.closedMap = true; } };
  page.context.sessionStorage = { getItem: () => null, removeItem() {} };
  page.context.location = { hash: '', search: '' };
  const html = fs.readFileSync(path.join(__dirname, '../src/Avers.Server/wwwroot/m/index.html'), 'utf8');
  vm.runInContext(html.match(/<script>([\s\S]*?)<\/script>/)[1], page.context);
  page.html = html;
  page.houseButtons = () => elementById('houses').children.flatMap(li => li.children);
  page.filter = text => { elementById('house-filter').value = text; elementById('house-filter').oninput(); };
  return page;
}

const ownHouses = [
  { id: 1, address: 'ул. Волочаевская, д. 19' },
  { id: 2, address: 'ул. Волочаевская, д. 19Д' },
  { id: 3, address: 'ул. Братская, д. 18' }
];
const houseResponse = houses => async () => ({ ok: true, json: async () => ({ houses }) });

test('Дома Аверса сразу открывает список, без верхней кнопки Поиск', async () => {
  const page = houseListPage(houseResponse(ownHouses));
  vm.runInContext('show("map")', page.context);
  await page.context.$('houses-open').onclick();
  assert.equal(page.context.$('map').hidden, true);
  assert.equal(page.context.$('search').hidden, false);
  assert.equal(page.houseButtons().length, 3);
  assert.doesNotMatch(page.html, /id="list-open"/);
  assert.match(page.html, /id="houses-open"[^>]*>Дома Аверса</);
});

test('Поиск над списком фильтрует дома Аверса; очистка возвращает список', async () => {
  const page = houseListPage(houseResponse(ownHouses));
  await page.context.$('houses-open').onclick();
  page.filter('ВОЛОЧ 19');
  assert.equal(page.houseButtons().length, 2);
  page.filter('братская');
  assert.equal(page.houseButtons()[0].textContent, 'ул. Братская, д. 18');
  page.filter('');
  assert.equal(page.houseButtons().length, 3);
});

test('В списке домов нет прежнего ограничения поиска до 30 результатов', async () => {
  const page = houseListPage(houseResponse(Array.from({ length: 150 }, (_, id) => ({ id, address: `ул. Тестовая, д. ${id + 1}` }))));
  await page.context.$('houses-open').onclick();
  assert.equal(page.houseButtons().length, 150);
});

test('Запрос, введённый во время загрузки домов, применяется к полученному списку', async () => {
  let complete;
  const page = houseListPage(() => new Promise(resolve => { complete = resolve; }));
  const loading = page.context.$('houses-open').onclick();
  page.filter('братская');
  assert.match(page.context.$('search-note').textContent, /Загружаем/);
  complete({ ok: true, json: async () => ({ houses: ownHouses }) });
  await loading;
  assert.equal(page.houseButtons().length, 1);
});

test('Выход во время загрузки не показывает дома прошлого сеанса', async () => {
  let complete;
  const page = houseListPage(() => new Promise(resolve => { complete = resolve; }));
  const loading = page.context.$('houses-open').onclick();
  vm.runInContext('logout()', page.context);
  complete({ ok: true, json: async () => ({ houses: ownHouses }) });
  await loading;
  assert.equal(page.context.$('login').hidden, false);
  assert.equal(page.houseButtons().length, 0);
});

test('Сбой загрузки домов показан явно и не маскируется поиском', async () => {
  const page = houseListPage(async () => ({ ok: false, status: 503 }));
  await page.context.$('houses-open').onclick();
  page.filter('братская');
  assert.match(page.context.$('search-note').textContent, /Ошибка сервера: 503/);
});
