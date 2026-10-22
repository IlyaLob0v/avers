# Соответствие JACK → Аверс

Составлено по выгрузке структуры JACK от 21.10.2026 (`db/legacy/jack-export.sh`, база avers102, Oracle 10.2, CL8MSWIN1251).
Данных в выгрузке не было: всё, что помечено «на данных», проверяется после полной выгрузки (`expdp`).

Источник — схема `legacy` (зеркало JACK после Ora2Pg, имена в нижнем регистре). Приёмник — модель `src/Avers.Data`.
Старые ключи сохраняются в `legacy_id_map(entity, old_id, new_id)` (`docs/04-db-migration-plan.md`, раздел 3).

## Справочники

| Аверс | JACK | Правило |
|---|---|---|
| `Street.Name` | `STREETS.STREETNAME` + `NAMETYPE.NAMETYPE` | Тип улицы (ул., пр.) по `NAMETYPEID`; как склеивать — на данных |
| `Organization` | `OWNER` → `JURPERS` | `Name` = `JURPERS.NAME`; адрес — по `JURPERS.BUILDINGID`. Телефонов и руководителей в JACK нет |
| `Mechanic` | `WORKMAN` → `PERSONNEL_DEPARTMENT` | `FullName` = `SURNAME NAME PATRONYMICNAME`; `Position` = `JOBTITLE.JOBTITLENAME`; `Phone` = `PHONE`. Только с `SACKINGDATE is null` |
| `Material` | `DETPRICE`, `DETSTORE` | Только названия; цены и сметы (`COST_ESTIMATE*`) в Аверсе нет |

## Дома — `House` ← `BUILDING`

| Аверс | JACK | Правило |
|---|---|---|
| `StreetId` | `STREETID` | через `legacy_id_map` |
| `Number` | `BUILDINGNO` | `BUILDINGNUMBER` + `BUILDINGSUFFIX` — только для проверки |
| `Floors`, `Entrances`, `Risers`, `Apartments` | `FLOORSNUMBER`, `ENTRYNUMBER`, `STEMSCOUNT`, `FLATSNUMBER` | null → 0 |
| `Zone` | `SERVAREA.SERVAREANAME` | по `SERVAREAID` |
| `OrganizationId` | `OWNERID` | через `OWNER` → `legacy_id_map` |
| `Antennas` | `ANTENNSCOUNT` | число как текст |
| `OtherNotes` | `NOTE` | |
| `PzhreuIndex`, `PzhreuAccount` | — | в `BUILDING` нет; адрес ПЖРЭУ в `PGRU_BUILDING_ADRESS.ADDR` — на данных |
| `LastPpr` | `PPR_TABLE` | max(`FACTDATE`) по дому |
| `AerialCables`, `Amplifiers`, `PowerSupplies`, `TechFloorKey` | — | в JACK нет, заполняются в Аверсе |

## Абоненты — `Subscriber` ← `PHYSPERS`

| Аверс | JACK | Правило |
|---|---|---|
| `HouseId` | `BUILDINGID` | через `legacy_id_map` |
| `LastName`, `FirstName`, `Patronymic` | `SURNAME`, `NAME`, `PATRONYMICNAME` | trim |
| `Apartment` | `FLATNO` | число как текст |
| `Section` | `SECTION` | число как текст |
| `Phone`, `Notes` | `PHONE`, `NOTE` | |
| `InnerCode` | `INVENTNO` | |
| `Account` | `PHYSICALPERSONID` | допущение: лицевой счёт = ключ абонента; подтвердить на данных |
| `PzhreuIndex`, `PzhreuAccount` | `PHYSPERS_SCORE.PGRU_INDEX`, `PGRU_SCORE` | последняя запись по `SCOREDATE` |
| `ContractDate`, `ContractTerminated` | `CONTRACT.BEGINDATE`, `ENDDATE` | последний договор по `BEGINDATE` |
| `ContractNumber` | `CONTRACT.CONTRACTID` | допущение: отдельного номера в JACK нет |
| `ContractHolder` | — | в JACK нет; возможно, в `CONTRACT.NOTE` — на данных |
| `Benefit` | `PRIVROLL` → `SOCIAL.SOCIALPOSITIONNAME` | действующая на дату переноса (`ENDDATE is null`), только если это одна из трёх льгот АДС (`Subscriber.Benefits`: юрлицо, коллективная оплата, коллективный усилитель в квартире; названия сверить на данных). Прочие льготы оформляет МФЦ — «не применяется» |
| `LastPaymentDate` | `ABN_PAY.PAYDATE` | max по абоненту |

Пустые абоненты, которых JACK заводит для заявок на дом (`CreateEmptyClient`), не переносятся. Признак — на данных
(вероятно, `TYPEOFPHYSICALPERSON`).

## Оплаты — `YearPayment` ← `ABN_PAY` + расчёт тарифа

В JACK одна строка `ABN_PAY` — один платёж; за месяц их может быть несколько. В Аверсе — одна строка на абонента и месяц.

| Аверс | JACK | Правило |
|---|---|---|
| `Year`, `Month` | `PAYFORDATE` | месяц, за который оплачено |
| `Amount` | `SUMMPAY` | сумма за месяц |
| `PaidAt` | `PAYDATE` | последняя дата за месяц |
| `RecordedBy` | `USERNAME` | у последнего платежа |
| `Source` | `PAY_TYPE.PAYTYPE` | по `PAYTYPEID`; для оплат через ПЖРЭУ (есть строка в `POST_PAY_ATTR` по `PAYID`) — «ПЖРЭУ» |
| `Tariff` | расчёт | см. ниже |

Начислений JACK не хранит, он их считает (`ClientMonthDebt`, `GetCurrentTax`, `GetConnectNumber`, `GetBuildTax`):

    начисление за месяц = тариф × (число отводов − льготные отводы)
    тариф          = ABN_TAX.SUMMTAX с последней TAXUPDATE <= дата, по CONNTYPEID
                     (для дома с действующей записью BUILD_TAX — её SUMMTAX)
    число отводов  = CONNLIST.CONNECTCOUNT с последней CHECKDATE <= дата
    льготы         = GetPrivateTaxFUNC2 (PRIVROLL, PRIVTAX)

`YearPayment.Tariff` = это начисление. Строки создаются за каждый месяц с начислением > 0, в том числе без оплаты.
Точную формулу (какую из версий `_NEW`/`_NEW2` вызывает программа, как считается `BUILD_TAX`) сверить с отчётами
старого Аверса на данных.

## Квитанции ПЖРЭУ — `UnifiedReceipt` ← `POST_PAY_ATTR`

| Аверс | JACK |
|---|---|
| `PzhreuIndex`, `Account` | `PGRU_INDEX`, `PGRU_SCORE` |
| `Moment` | `PAYDATE` |
| `Paid` | `SUMMPAY` |
| `NOs`, `NKass`, `NOper`, `NKvit` | `N_OS`, `N_KASS`, `N_OPER`, `N_KVIT` |
| `InnerCode` | `PHYSPERS.INVENTNO` по `PHYSICALPERSONID` |
| `Amount` | начисление за месяц `PERIOD` (расчёт выше) |

## Заявки — `WorkTicket` ← `DEMANDS`

| Аверс | JACK | Правило |
|---|---|---|
| `Number` | `DEMANDCODE` | null → `DEMANDID` |
| `Date`, `Time` | `RECEIVINGDATE` | |
| `SubscriberId`, `HouseId`, `Apartment`, `Section` | `PHYSICALPERSONID` → `PHYSPERS` | для пустого абонента — только дом |
| `Phone` | `PHONE` | |
| `Fault` | `FAULTDESCRIPTION` | |
| `WorkDone` | `NOTE`, `PGRUREPORTNOTE` | |
| `Kind` | `WORKTYPEID` → `WRKTYPES` | соответствие названий — на данных |
| `Mechanic` | `SERVAREA.RESPONSIBLEPERFORMERID` дома | в заявке исполнитель не хранится |
| `AcceptedBy` | `USERID` | |
| `Status`, `DoneDate` | `EXECUTIONDATE`, `ISCANCELED` | есть дата выполнения → `Done`; отменённые — на данных (статуса «отменена» в Аверсе нет) |

## ППР — `PprEntry` ← `PPR_TABLE`

Год и месяц — из `PLANDATE`. `FACTDATE` заполнена → `Done`; пусто и месяц прошёл → `Overdue`; иначе `Planned`.

## Чего в JACK нет

`CourtCase`, `HouseScheme`/`HouseSchemeFile`, `IntercomPanel`/`IntercomTicket`, `AppUser`, `AppSetting`, `ChangeLogEntry` —
заводятся в Аверсе.

## Не переносится

Буферы загрузки (`*_BUFF*`, `IMP_BUFF*`, `PAY_IMPORT*`, `TOTAL_ON_PAY_IMPORT*`), копии и архивы (`COPY_*`, `*_OTD`,
`DEMANDS_DEL`, `CONN_DEM_ARH`), служебные (`RF_*`, `CDDU_*`, `CDDL_*`, `PLAN_TABLE`, `TEST_TABLE`, `Z_GNS_*`, `gns_imp_*`)
и пустые таблицы 2011 года. В `legacy` они остаются для сверки.
