# 10. Трассировка требований

Статус: сверено с кодом ветки `docs/architecture-review` (последний коммит 7e59591), 06.11.2026.
Для каждого требования из [02-requirements.md](02-requirements.md) указано, где оно сделано и каким тестом проверено.

Как ставился статус:
- **Сделано** — в `src/` есть экран, обработчик сервера и операция, которые выполняют требование целиком.
- **Частично** — сделана часть; чего не хватает, сказано в примечании.
- **Не начато** — кода нет.
- **Ждёт ответа Q-xx** — делать нельзя, пока не ответят на вопрос из [07-open-questions.md](07-open-questions.md).

Статус ставился только по коду и тестам (поиск по `src/` и `tests/`), не по названиям веток. Ветки, которые не влиты
в эту (`feature/payments-model`, `feature/tickets-loading`, `fix/permissions`, `fix/production-install`), не учитывались.
Сокращения путей: `Desktop/` = `src/Avers.Desktop/`, `Server/` = `src/Avers.Server/`, `Data/` = `src/Avers.Data/`,
`Domain/` = `src/Avers.Domain/`; тесты — `класс.метод` из проектов `tests/Avers.*.Tests`.

## Дом и техника

| ID | Пр. | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|---|
| REQ-ENG-01 | M | Сделано | `Desktop/Views/AntenInfoView.axaml`, `AbonView.axaml` (Эт., Под.); `MainViewModel.HouseStats`; правка — `Data/Phonebook.cs`, `Server/Desktop/PhonebookEndpoints.cs`; телефон — `Server/wwwroot/m/index.html` | `PhonebookTests.EditHouse_IsLogged_AndDuplicateNumberRejected` (сервер) — правка; показ отдельным тестом не проверяется | Число антенн не показывается, как и просили |
| REQ-ENG-02 | M | Сделано | `Domain/Organization.cs` (адрес, телефоны, руководитель, инженер, филиал), `Domain/ServiceOrganizations.cs`; справочник — `Desktop/Views/PhonebookView.axaml`, `Server/Desktop/PhonebookEndpoints.cs` | `ApartmentTests.ServiceOrganization_Title`, `PhonebookTests.Organization_AddEditDelete_NotWhileServesHouses` (сервер), `PhonebookTests.Engineer_AddsOrganization_AndPicksItForHouse` (Desktop) | Название в программе — «обслуживающая организация» |
| REQ-ENG-03 | M | Сделано | `Desktop/Views/AntenView.axaml` (паспорт дома), `Data/HousePassport.cs`, `PUT /api/houses/{id}/passport` в `Server/Desktop/DesktopEndpoints.cs` | `SharedOperationsTests.SaveHousePassport_TrimsFields_AndReportsMissingHouse`, `DesktopTests.Passport_SavedByChiefEngineerOnly` | Техника — текстовые поля (антенны, воздушные линии, усилители, блоки питания, ключ, прочее). Защиты от одновременной правки нет — ADR-0011 |
| REQ-ENG-04 | M | Частично (ждёт Q-24) | `Desktop/Views/SchemeView.axaml`, `SchemeViewModel.cs`, `Data/SchemeFiles.cs`, `Server/Desktop/SchemeEndpoints.cs` | `SchemeTests` (сервер и Desktop): загрузка, замена, переименование, удаление, «не картинка» отклоняется | Только JPG/PNG (ADR-0009); SVG и XLS/XLSX нет |
| REQ-ENG-05 | M | Сделано | `House.LastPpr`; отметка ППР «вып.» сдвигает дату — `Data/PprPlan.cs`; показ — `AntenView`, `DispView` (`LastPprText`) | `PprTests.MarkDone_MovesLastPprOfTheHouse`, `ShellTests.Ppr_ClickOnMonthMarksItAndCountsPercent` | |
| REQ-ENG-06 | M | Сделано | `House.OpenFault*`; предупреждение `MainViewModel.FaultBanner` в `DispView.axaml` (приём заявки), `AbonView`, `SubscriberView`, `AntenView` | `SharedOperationsTests.SaveHousePassport_OpenFault_SetAndCleared`, `DesktopTests.Passport_SavesOpenFaultAndOrganization`, `ShellTests.Passport_EngineerSetsAndClearsOpenFault`, `ShellTests.Fault_PlannedFixInPast_MarkedOverdue` | Просроченный план устранения выделяется красным |
| REQ-ENG-07 | M | Сделано | Журнал заявок `Desktop/Views/DispView.axaml`, `TicketsViewModel.cs`; `Domain/TicketJournal.cs`; `GET /api/tickets`, `/api/houses/{id}/tickets` (`Server/Tickets/TicketEndpoints.cs`) | `TicketTests.List_ByHousePeriodAndKind`, `TicketJournalTests.Period_IncludesBothEnds`, `TicketJournalTests.HouseAndKind_FindAntennaTicketsOfHouse` | |
| REQ-ENG-08 | S | Частично | `Desktop/ViewModels/ReportsViewModel.cs`, `ReportsView.axaml`, `Desktop/Reports/XlsxTable.cs` | `ShellTests.Report_ExportsCheckedMetricsByGroupToExcel`, `ShellTests.Report_CountsDoneTicketsOfThePeriodOnly` | Метрики включаются и выключаются, отчёт по организациям или зонам — только XLSX, PDF нет. Метрики считаются на клиенте по заявкам из снимка (меняется по ADR-0015) |
| REQ-ENG-09 | S | Сделано | `Desktop/Views/PprView.axaml`, `PprViewModel.cs`, `Domain/PprSchedule.cs`, `Data/PprPlan.cs`, `Server/Ppr/PprEndpoints.cs` | `PprScheduleTests`, `PprTests` (сервер), `ShellTests.Ppr_*` | Отбор по организации или зоне, итоги по месяцам, процент, «долги ППР», копирование плана на следующий год |

## Абонент

| ID | Пр. | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|---|
| REQ-ADS-01 | M | Сделано | `Desktop/Views/AbonView.axaml`, `SubscriberView.axaml`; `GET /api/subscribers/{id}` (`Server/Subscribers/SubscriberEndpoints.cs`, `Card`); поиск — `Data/SubscriberSearch.cs` | `ApartmentCardTests.Card_ShowsContractAndLastPayment`, `AuthTests.SubscriberCard_DispatcherSeesApartmentData`, `SubscriberTests.Search_*` | |
| REQ-ADS-02 | M | Сделано | `GET /api/subscribers/{id}/tickets?from&to` (`TicketEndpoints.ForSubscriber`), `Domain/ApartmentTickets.cs` | `TicketTests.Subscriber_TicketsOfApartment`, `TicketTests.Subscriber_WithoutApartment_GetsOnlyOwnTickets`, `ApartmentTests.For_*` | |
| REQ-ADS-03 | M | Сделано | Как REQ-ENG-06 | Как REQ-ENG-06 | |
| REQ-ADS-04 | M | Ждёт ответа Q-19 | — | нет теста | Одна строка `YearPayment` на месяц: второй платёж отклоняется (`Data/Payments.cs:25-26`). Решение — ADR-0010 |
| REQ-ADS-05 | M | Сделано | `Desktop/Views/PrintView.axaml` (флажок «с датой оплаты»), `MainViewModel.Print.cs` (`PrintAccount`), `Desktop/Printing/PdfForms.cs`; `Subscriber.PrintAccountWithPayDate` | `ShellTests.Print_ReceiptAndAccountOpenAsPdf` — только что PDF открывается | Переключатель тестом не проверяется |
| REQ-ADS-06 | M | Сделано | `Subscriber.ContractDate/ContractNumber/ContractParty`; `Desktop/Views/ContractView.axaml`, `ContractsViewModel.cs`; `PUT /api/subscribers/{id}/contract` | `ApartmentCardTests.Card_ContractWithAnotherPerson`, `ApartmentCardTests.UpdateContract_*`, `ContractTests` (Desktop) | |
| REQ-ADS-07 | M | Сделано | `Desktop/Views/PrintView.axaml`, `PrDublView.axaml`, `ReceiptsViewModel.cs`, `Domain/UnifiedReceipt.cs` (N_OS, N_KASS, N_OPER, N_КВИТ) | `ShellTests.Print_ReceiptAndAccountOpenAsPdf`, `ReceiptTests.FindAnyAccount_ThenFilterByMonth` | |
| REQ-ADS-08 | M | Сделано | `MainViewModel.RecordPayment` (повторное нажатие от порога); `Data/Settings.cs`; `PUT /api/settings/payment-confirm` (администратор, начальник АДС) | `ShellTests.RecordPayment_FromThreshold_NeedsSecondPress`, `ApartmentCardTests.PaymentConfirm_HeadChanges_LawyerCannot`, `CardDataTests.PaymentConfirmFrom_Defaults_AndIsStored` | Q-01 закрыт |
| REQ-ADS-09 | M | Частично | `MainViewModel.OekWarning`, блок «Выгрузка для ОЭК» в `DispView.axaml` | нет теста | Есть только подсказка «принято N, выполнено M» и правило «принято больше — скрыто» (Q-02). Самого списка заявок для ОЭК и его выгрузки нет; дата — только рабочая, выбрать другую нельзя |
| REQ-ADS-10 | M | Частично (ждёт Q-29) | `Data/AversDbContext.cs:66-68` — фамилия, имя, отчество по 80 знаков; `ContractHolder` — 250 | нет теста | Ограничение длины есть. Сколько нужно — Q-29 и данные JACK |
| REQ-ADS-11 | M | Частично (ждёт Q-29) | `AppUser.CanEditAccount`, `Server/Auth/Permissions.cs` (`SubscribersEdit`, `RecordPaymentRequirement`); экран «Права доступа» — `Desktop/Views/AccessView.axaml` | `AuthTests.Me_ReturnsRoleAndPaymentRight`, `AuthTests.RecordPayment_ForbiddenForViewOnlyRoles`, `ApartmentCardTests.UpdateContract_LawyerIsForbidden` | Право даёт оплату, договор, примечание. Правки ФИО и телефона у существующего абонента нет совсем (только при заведении квартиры) — Q-29 |
| REQ-ADS-12 | S | Сделано | `Subscriber.Benefit`, список `Subscriber.Benefits`; `UpdateContractRequest.Benefit` | `ApartmentCardTests.UpdateContract_ByAdsHead_IsSaved`, `ApartmentCardTests.UpdateContract_Validates` | Q-03 закрыт: только пометка, на начисления не влияет |
| REQ-ADS-13 | M | Сделано | `DispView.axaml`, `TicketsViewModel.cs`, `Domain/TicketJournal.cs`, `GET /api/tickets` | `TicketTests.List_ByHousePeriodAndKind`, `TicketJournalTests.*` | Заявки пока целиком в снимке; переход на запросы — ADR-0015 |
| REQ-ADS-14 | M | Сделано | `TicketJournal.ByMonth`, `GET /api/tickets/by-month`, `TicketsViewModel.SelectTicketMonth` | `TicketJournalTests.ByMonth_AlwaysTwelveMonths_CountsAcceptedAndDone`, `TicketTests.ByMonth_GivesTwelveMonths` | Сводку Desktop считает сам по снимку; по ADR-0015 — сервер |
| REQ-ADS-15 | M | Сделано | Блок квартиры в `DispView.axaml` (л/с, ФИО, договор, последняя оплата, примечание), `TicketsViewModel.OpenTicketSubscriber/OpenTicketContract` | нет теста на блок квартиры и переходы | |
| REQ-ADS-16 | M | Сделано | `GET /api/houses/{id}/service-info` (`TicketEndpoints.ServiceInfo`), блок дома в `DispView.axaml` | `TicketTests.ServiceInfo_ShowsHomeownerAndOpenFault` | |
| REQ-ADS-17 | M | Сделано | `Domain/RoleCatalog.cs` (`AdsHead`, `Dispatcher`, `CanEditTicket`), `TicketEndpoints.Update` | `RoleCatalogTests.AdsRoles_AreHeadAndUser`, `RoleCatalogTests.CanEditTicket_*`, `TicketTests.Dispatcher_CannotEditSomeoneElsesTicket_HeadCan`, `ShellTests.AcceptTicket_AppearsInJournalAndOnlyAuthorEdits` | Q-17 закрыт |

## Радиомеханики

| ID | Пр. | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|---|
| REQ-MECH-01 | M | Сделано | `Server/Mobile/MobileEndpoints.cs`, `Server/wwwroot/m/index.html`, `Contracts/Mobile.cs`; наружу — `deploy/server/avers-internet` (Caddy) | `MobileTests.Mechanic_FindsHouseByStreetAndNumber`, `MobileTests.Mechanic_SeesHomeownerTechnicsAndSchemes`, `MobileTests.Lawyer_HasNoAccess`, `AuthTests.InternetLogin_OnlyReader` | Кто ещё входит из интернета — Q-07 |
| REQ-MECH-02 | M | Частично | Права — `RoleCatalog` (Читатель только смотрит); водяной знак и холст вместо ссылки — `Server/wwwroot/m/index.html:203` | `RoleCatalogTests.Reader_CannotEditAnything`, `SchemeTests.Reader_CannotEditSchemes` (Desktop); водяной знак — нет теста | Водяной знак рисует страница в браузере. `GET /api/mobile/schemes/{id}` отдаёт картинку без знака любому, кто вошёл как Читатель, — «скачать нельзя» держится только на странице |

## Юристы

| ID | Пр. | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|---|
| REQ-LAW-01 | M | Сделано | `Billing.DebtHighlight` (450 ₽), `Desktop/Views/LegalView.axaml`, `LegalViewModel.cs`, `Desktop/Controls/GridTone.cs`; `GET /api/debts` (`Data/Debts.cs`) | `ShellTests.Debtors_AreColored`, `DesktopTests.Debts_MatchBillingOfEachSubscriber` | |
| REQ-LAW-02 | M | Сделано | `Billing.IsOldPayer`, `LegalViewModel` | `BillingTests.IsOldPayer_*`, `ShellTests.Debtors_OldPayerMarked_EvenInRedRow` | «Ни разу не платил» = давний неплательщик — REQ-CALC-04, Q-25 |
| REQ-LAW-03 | S | Частично | `Domain/CourtCase.cs`, `Data/CourtCases.cs`, `Desktop/Views/ClaimView.axaml`, `POST/PUT /api/subscribers/{id}/court-cases` | `CourtCasesTests.*`, `ApartmentCardTests.CourtCase_LawyerAddsAndEdits_AdsHeadIsForbidden`, `ShellTests.CourtCase_LawyerAddsCase_ShownInDebtors` | Судебное дело у абонента есть. Таблица юристов «Дела Аверс.xls» не перенесена (Q-04: после данных JACK) |
| REQ-LAW-04 | M | Частично | `LegalViewModel.ExportDebtors`, `GET /api/debts` | `ShellTests.Debtors_ExportToExcel` | Выгружается весь список должников в Excel. Своего отбора (сумма, месяцы, дом, есть ли дело) у юристов нет |
| REQ-LAW-05 | S | Сделано | `Domain/LegalDeadline.cs`, `Domain/WorkCalendar.cs`, `LegalViewModel.LegalRemaining` | `LegalDeadlineTests.*`, `ShellTests.LegalCalculator_ShowsDaysLeft` | Учитывает выходные, праздники и рабочие субботы; срок исковой давности |
| REQ-LAW-06 | S | Сделано | `LegalViewModel.PrintAllClaims` (от `Billing.ClaimUnpaidMonths` = 3 мес.), `Desktop/Printing/ClaimNotice.cs`, `WordForms.cs` | `ShellTests.Legal_PrintAllClaims_OneWordFileForDebtorsNotInCourt`, `ShellTests.Claim_FollowsLawyersSampleForSelectedDebtor` | Уведомления открываются в Word |
| REQ-LAW-07 | S | Частично | `LegalViewModel.ExportCourtPackage`, `PdfForms.CourtPackage`; сканы — `Data/SubscriberDocuments.cs`, `Server/Desktop/DocumentEndpoints.cs` | `ShellTests.Claim_OpensNoticeInWordAndCourtPackageAsPdf`, `DocumentTests.*` | В пакете справка о долге и история платежей. Копии договора в пакете нет — скан хранится отдельно |

## Бухгалтерия

| ID | Пр. | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|---|
| REQ-ACC-01 | M | Частично (ждёт Q-28) | `Domain/AccountingMonth.Shipment`, `Data/AccountingReport.cs`, `GET /api/reports/accounting`, кнопка «Для 1С» в `ReportsView.axaml` | `AccountingMonthTests.Shipment_CountsMonthRowsByTariff`, `DesktopTests.Accounting_SameAsOverAllPayments` | Группировка по тарифу: число месяцев начисления × тариф. Признака физлицо/юрлицо и числа отводов в модели нет |
| REQ-ACC-02 | M | Ждёт ответа Q-28 | — | нет теста | Признака ТСЖ у абонента нет |
| REQ-ACC-03 | M | Частично (ждёт Q-12) | `AccountingMonth.Receipts` — группировка по полю «источник» платежа | `AccountingMonthTests.Receipts_GroupByPaymentDateNotByBilledMonth`, `DesktopTests.Accounting_SameAsOverAllPayments` | Источник — свободная строка (`Касса`, `ОЭК`…), справочника источников и разбивки как в сверке нет; справочник `PaymentSource` — ADR-0010. За период — нет, только месяц |
| REQ-ACC-04 | M | Ждёт ответа Q-20, Q-28 | — | нет теста | Долг обрезается до нуля (`Billing.cs:24-25`), аванса и сальдо 62.01/62.02 нет. Модель — ADR-0010 |
| REQ-ACC-05 | S | Частично | `Data/OekImport.cs`, `Desktop/Reports/OekFile.cs`, `ReceiptsViewModel.ImportOek` («сверка-оэк» в Excel) | `OekImportTests.Mismatches_AreListed_AndPaidMonthIsNotTouched`, `OekFileTests.*` | Есть построчные расхождения при загрузке файла ОЭК. Сверки итогов с актом ОЭК (август 2026: 4 914 739,57) нет; строки «зачёт», «перечислено» — Q-12 |
| REQ-ACC-06 | M | Частично (ждёт Q-05) | Файл Excel «Для 1С» (`ReportsViewModel.ExportForOneC`) | `DesktopTests.Accounting_SameAsOverAllPayments` | Файл с отгрузкой и поступлениями за месяц. Обмена с 1С (OData), взаимозачёта с ОЭК нет (ADR-0006). Итоги для 1С сейчас видит любой, кому открыт модуль «Абоненты» (политика `SubscribersView`), — см. «Расхождения» |

## Правила расчёта

| ID | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|
| REQ-CALC-01 | Сделано | `Domain/Billing.cs:13` (`LastDueMonth`) | `BillingTests.Due_SkipsCurrentAndFutureMonths`, `DebtsTests.Load_SkipsCurrentMonth`, `SubscriberTests.Card_PaidThroughCurrentMonth_HasNoDebt` | |
| REQ-CALC-02 | Сделано (временно, Q-26) | `Data/Payments.cs:13-14`, `:25-26` | `PaymentTests.Record_ZeroAmount_CountsAsPaidMonth` | |
| REQ-CALC-03 | Сделано (временно, Q-26) | `Billing.cs:27-28`, `Penalty.cs:16-20` | `BillingTests.UnpaidMonths_CountsMonthsWithoutAmount`, `PenaltyTests.Calculate_MatchesLawyersSheet` | |
| REQ-CALC-04 | Сделано (временно, Q-25) | `Billing.cs:30-31` | `BillingTests.IsOldPayer_TrueWhenNeverPaid` | |
| REQ-CALC-05 | Сделано (временно, Q-25) | `Penalty.cs:9`, `:14-24`, `:27-28` | `PenaltyTests.*`, `ShellTests.Claim_PrintsPenaltyOnChosenDate` | |
| REQ-CALC-06 | Сделано | `Data/OekImport.cs:60` | нет теста | В тестах есть строки без даты, но дата 1 числа не проверяется |
| REQ-CALC-07 | Ждёт ответа Q-20 | — (сейчас `Billing.cs:24-25`, `Math.Max(0, …)`) | `BillingTests.Debt_IsZeroWhenOverpaid` закрепляет старое поведение | Сальдо со знаком и показа переплаты в карточке нет. ADR-0010 |
| REQ-CALC-08 | Ждёт ответа Q-19 | — (сейчас отказ, `Payments.cs:25-26`) | `PaymentTests.Record_SameMonthTwice_Returns409` закрепляет старое поведение | ADR-0010: второй платёж — с подтверждением в запросе |

## Нефункциональные требования

| ID | Статус | Где в коде | Чем проверено | Примечание |
|---|---|---|---|---|
| NFR-01 | Сделано | `src/Avers.Desktop` (сборки win-x64 и linux-x64 в CI), `deploy/xrdp/`, запасной вариант `deploy/rds/` | нет теста (CI собирает и пробно запускает программу) | ADR-0008 |
| NFR-02 | Сделано | `Server/Auth/Permissions.cs`, `Auth/FreshUser.cs`, `Domain/RoleCatalog.cs`; журнал — `Data/ChangeLog.cs`, `Desktop/Views/ChangeLogView.axaml` | `AuthTests.*`, `RoleCatalogTests.*`, `ChangeLogTests.*`, `UserTests.DisableAndRoleChange_ApplyToOpenSession` | Одновременная правка без защиты — ADR-0011 |
| NFR-03 | Частично | Экраны повторяют старые модули (`AppModule`), плотные таблицы, раскладка 1366 | `AppearanceTests.*`, `ShellTests.EveryModule_OpensForEveryRole`; горячие клавиши — нет теста | Горячих клавиш почти нет (`ContractView`, `PrDublView`); список нужных клавиш не собран |
| NFR-04 | Частично | Выгрузка JACK — `db/legacy/`, сверка — `db/migration/reconcile/`, соответствие полей — `db/migration/mapping.md`, стенд — `db/stand/` | `db/migration/reconcile/test/run-tests.sh` | Загрузки данных JACK в базу Аверса нет; выгрузки схемы нет (Q-09, Q-28). Платежи `ABN_PAY` ложатся только на модель ADR-0010 |
| NFR-05 | Не начато | — | нет теста | Ночная сверка со старым Аверсом — этап 6 |
| NFR-06 | Частично | `deploy/server/avers-backup` (ночной `pg_dump` по таймеру, срок `AVERS_BACKUP_DAYS`, перенос на другой компьютер, копия перед обновлением), `deploy/server/avers-restore-check` (еженедельное восстановление во временную базу) | нет теста | WAL-архива нет, допустимые потеря и простой не названы — Q-27 |
| NFR-07 | Частично | Роли — как NFR-02; журнал доступа — `Data/AccessLog.cs`, `Auth/AccessLogFilter.cs`; HTTPS наружу — `deploy/server/avers-internet` | `AccessLogTests.ViewsOfPersonalData_AreLogged_ForAdministratorOnly`, `ServerModeTests.AccessLog_ShowsWhoOpenedSubscribers`, `AuthTests.InternetLogin_OnlyReader` | В офисе HTTPS через Caddy (`tls internal`), Kestrel только на `127.0.0.1:5115` (`deploy/server/install.sh`, ADR-0012). Проверки шифрования тестом нет |
| NFR-08 | Частично | XLSX — `Desktop/Reports/XlsxTable.cs` (ClosedXML); PDF — `Desktop/Printing/PdfForms.cs` (SkiaSharp); Word — `WordForms.cs` | `ShellTests.Report_ExportsCheckedMetricsByGroupToExcel`, `ShellTests.Debtors_ExportToExcel`, `ShellTests.Print_*` | Печатные формы — PDF, отчёты (по заявкам, должники, «Для 1С») — только XLSX |

## Итог по M-требованиям

Всего M-требований — 33 (ENG 7, ADS 16, MECH 2, LAW 3, ACC 5). Правила расчёта и NFR приоритета не имеют и в счёт не входят.

| Статус | Сколько | Какие |
|---|---|---|
| Сделано | 21 | ENG-01, 02, 03, 05, 06, 07; ADS-01, 02, 03, 05, 06, 07, 08, 13, 14, 15, 16, 17; MECH-01; LAW-01, 02 |
| Частично | 9 | ENG-04 (Q-24), ADS-09, ADS-10 (Q-29), ADS-11 (Q-29), MECH-02, LAW-04, ACC-01 (Q-28), ACC-03 (Q-12), ACC-06 (Q-05) |
| Не начато | 0 | — |
| Ждёт ответа | 3 | ADS-04 (Q-19), ACC-02 (Q-28), ACC-04 (Q-20, Q-28) |

**M-требования без теста:** REQ-ADS-04, REQ-ADS-09, REQ-ADS-10, REQ-ADS-15, REQ-ACC-02, REQ-ACC-04.

Тест есть, но главное не проверяет: REQ-ADS-05 (переключатель «с датой оплаты»), REQ-MECH-02 (водяной знак, отдача схемы без знака),
REQ-ENG-01 (показ этажности и подъездов — проверена только правка).

## Сделано в коде, но нет в требованиях

Функции есть в программе, но своего REQ-ID у них нет. Источник требования по истории коммитов
не проверялся, поэтому в `02-requirements.md` они не внесены. Статус всех строк — **предложено, подтвердить**:
подтверждает Илья или роль из колонки «Кто подтверждает», после этого строка переносится в `02-requirements.md`.

| Предлагаемый ID | Формулировка | Где в коде | Чем проверено | Откуда, вероятно | Кто подтверждает |
|---|---|---|---|---|---|
| REQ-ENG-10 | Домофоны дома: панели по подъездам (тип панели, коммутатор, ключи, питание); заводит главный инженер | `Domain/IntercomPanel.cs`, `Data/Intercoms.cs`, `Server/Desktop/IntercomEndpoints.cs`, `Desktop/Views/DomofoView.axaml` | `IntercomTests.Engineer_AddsAndEditsPanel_Logged` (сервер), `IntercomTests.Admin_AddsPanelForNextEntrance` (Desktop) | Модуль domofo старой системы (`01-current-system.md`) | Главный инженер |
| REQ-ADS-18 | Заявки по домофону: принять по подъезду, закрыть, напечатать | `Intercoms.AddTicket/MarkDone`, `IntercomViewModel.cs` | `IntercomTests.Dispatcher_AcceptsAndClosesTicket`, `IntercomTests.AcceptThenClose_AndPrint` | domofo | Начальник АДС |
| REQ-ENG-11 | Справочники: улицы, дома (этажи, подъезды, стояки, квартиры, зона, организация), обслуживающие организации, материалы, радиомеханики; дом с абонентами и организацию с домами не удалить | `Data/Phonebook.cs`, `Server/Desktop/PhonebookEndpoints.cs`, `Desktop/Views/PhonebookView.axaml` | `PhonebookTests.*` (сервер и Desktop) | Справочники AversAnten (`01-current-system.md`); роль «Администратор — справочники» | Главный инженер |
| REQ-ACC-07 | Загрузка файла начислений ОЭК (nachMMYYYY_an) за месяц: оплаты ложатся в месяц файла с источником «ОЭК», л/с ОЭК запоминается у квартиры, расхождения — в Excel, повторная загрузка не задваивает | `Data/OekImport.cs`, `Desktop/Reports/OekFile.cs`, `ReceiptsViewModel.cs`, `POST /api/oek/import` | `OekImportTests.*`, `OekFileTests.*`, `ApartmentCardTests.OekImport_HeadLoads_LawyerCannot`, `ReceiptTests.OekFile_OfAnotherMonth_IsNotLoaded` | Сверка ОЭК, ImportPay | Бухгалтерия, начальник АДС |
| REQ-LAW-08 | Судебное дело должника: номер приказа, дата, суд, сумма, госпошлина, кому и когда направлен исполнительный лист, поступления, примечание; видно в списке должников | `Domain/CourtCase.cs`, `Data/CourtCases.cs`, `ClaimView.axaml` | `CourtCasesTests.*`, `ShellTests.CourtCase_LawyerAddsCase_ShownInDebtors` | Q-04: замена «Дела Аверс.xls» | Юристы |
| REQ-LAW-09 | Заявление о выдаче судебного приказа и заявление в ФССП — в Word по данным должника и дела | `Desktop/Printing/CourtOrderApplication.cs`, `EnforcementApplication.cs` | `ShellTests.Claim_OpensCourtOrderApplicationInWord`, `ShellTests.Claim_OpensEnforcementApplicationWithCourtCase` | Образцы юристов | Юристы |
| REQ-LAW-10 | Сканы документов должника (договор и др.): загрузить, открыть, удалить; просмотр пишется в журнал доступа | `Data/SubscriberDocuments.cs`, `Server/Desktop/DocumentEndpoints.cs`, `LegalViewModel` | `DocumentTests.*`, `ShellTests.Claim_LawyerAddsOpensAndDeletesScan` | — | Юристы |
| REQ-ADS-19 | Отмена ошибочной оплаты — администратор и начальник АДС; месяц снова неоплачен, в журнале старые значения | `Data/PaymentCancellation.cs`, `DELETE /api/subscribers/{id}/payments/{paymentId}` | `PaymentCancellationTests.*`, `ApartmentCardTests.CancelPayment_OnlyAdminAndAdsHead_MonthBecomesUnpaid`, `ShellTests.CancelPayment_SelectedPaidMonthBecomesUnpaid` | — (по ADR-0010 меняется на пометку) | Начальник АДС |
| REQ-ADS-20 | Новая квартира (абонент): начисления от месяца договора до декабря, дубль л/с отклоняется | `Data/Apartments.cs`, `POST /api/subscribers` | `ApartmentsTests.*`, `ApartmentCardTests.AddApartment_CreatesSubscriber_LawyerIsForbidden`, `ContractTests.NewApartment_*` | — | Начальник АДС |
| REQ-ADS-21 | Расторжение договора: неоплаченные месяцы после него снимаются; новый договор возвращает начисления | `Data/SubscriberContracts.cs` | `SubscriberContractsTests.*`, `ApartmentCardTests.UpdateContract_Termination_StopsAccrualsAfterItsMonth`, `ContractTests.Terminate_ThenNewContract` | Q-18 (месяц расторжения) | Бухгалтерия, юристы |
| REQ-ADS-22 | Бланк договора и реестр договоров дома: печать в Word/Excel по данным Аверса | `Desktop/Printing/ContractForm.cs`, `ContractsViewModel.cs` | `ContractTests.PrintContract_FillsWhatAversKnows_LeavesPassportBlank`, `ContractTests.Registry_ExportsHouseContracts` | Модуль aversContract (`01-current-system.md`) | Начальник АДС |
| REQ-ADS-23 | Отметка «давно не платил» в карточке: от N месяцев без оплаты (N по умолчанию 2, меняют администратор и начальник АДС) | `Domain/PaymentAge.cs`, `Data/Settings.cs`, `PUT /api/settings/payment-warning` | `PaymentAgeTests.*`, `CardDataTests.PaymentWarningMonths_*`, `ApartmentCardTests.Card_ThreeMonthsWithoutPayment_IsLate_UntilThresholdRaised` | — | Начальник АДС |
| REQ-ADS-24 | У оплаты видно, кто внёс (кассир) и кто оплатил, если не сам абонент | `YearPayment.RecordedBy/PayerName`, `MainViewModel.Card.cs` | `ApartmentCardTests.RecordPayment_RemembersWhoRecordedAndWhoPaid`, `CardDataTests.ContractAndPaymentAuthor_RoundTrip` | — | Начальник АДС |
| REQ-ADS-25 | Печать наряд-заказа по выбранной заявке (PDF) | `TicketsViewModel.PrintWorkOrder`, `PdfForms.cs` | `ShellTests.Print_WorkOrderOfSelectedTicketOpensAsPdf` | AversDisp | Начальник АДС |
| REQ-ACC-08 | Смена тарифа с месяца — для квартиры, дома или всех; оплаченные месяцы не меняются | `Data/Tariffs.cs`, `PUT /api/tariff` | `TariffsTests.*`, `ApartmentCardTests.ChangeTariff_ForOneApartment_OnlyUnpaidMonths_LawyerIsForbidden`, `ShellTests.ChangeTariff_ForThisHouse_ChangesUnpaidMonths` | Q-22 (кто меняет) | Бухгалтерия |
| REQ-ACC-09 | Открытие начислений на новый год по последнему тарифу для действующих абонентов | `Data/Accruals.cs`, `POST /api/accruals/{year}` | `AccrualsTests.*`, `ApartmentCardTests.OpenYear_NextYearMonthsAppear_OnlyForSettingsEditors`, `ShellTests.OpenYear_ReportsOpenedSubscribers` | Q-22 | Бухгалтерия |
| REQ-ADM-01 | Сотрудники: завести, сменить роль и пароль, отключить; отключение и смена пароля действуют сразу; себя отключить нельзя | `Server/Auth/UserEndpoints.cs`, `Data/UserAccounts.cs`, `Desktop/Views/AccessView.axaml`; первый администратор — `Server/Hosting/AdminCommands.cs` | `UserTests.*`, `UsersTests.*` (Desktop), `AdminCommandsTests.*` | Роль «Администратор — пользователи, роли» | Илья |
| REQ-ADM-02 | Экран журнала изменений (кто, когда, что было и стало) — только администратор | `ChangeLogView.axaml`, `GET /api/change-log` | `ChangeLogTests.Log_OnlyAdministrator`, `UsersTests.ChangeLog_ShowsEmployeeChange` | Уточняет NFR-02 | Илья |
| REQ-ADM-03 | Журнал доступа: кто открывал карточки, оплаты, списки должников, сканы — только администратор | `Data/AccessLog.cs`, `GET /api/access-log` | `AccessLogTests.*`, `ServerModeTests.AccessLog_ShowsWhoOpenedSubscribers` | Уточняет NFR-07 | Илья |
| NFR-09 | Старое рабочее место, которое может испортить данные, сервер не пускает: «Обновите программу» (426) | `Contracts/DesktopClient.cs`, `Server/Desktop/DesktopVersionCheck.cs` | `ClientVersionTests.*` | Правило 8 CLAUDE.md, ADR-0014 | Илья |
| NFR-10 | Ограничение попыток входа: из интернета 10 в минуту с адреса, в офисе 60; блокировка логина после неверных паролей | `Server/Auth/LoginFailures.cs`, `AuthEndpoints.cs` | `AuthTests.LoginLimit_*`, `AuthTests.InternetWrongPasswords_DoNotLockOfficeLogin` | NFR-07 | Илья |

## Расхождения документов с кодом

Найдены при сверке; документы, кроме этого файла, не правились.

1. **Роль «Бухгалтерия».** В `02-requirements.md` (роли) и Q-21 написано, что роль заведена и итоги для 1С видят только
   бухгалтерия, администратор и начальник АДС. В коде роли нет (`Domain/Enums.cs`, `RoleCatalog`), а `GET /api/reports/accounting`
   закрыт только политикой `SubscribersView` — его видят и юристы, и диспетчеры, и инженеры. Возможно, это сделано в невлитой ветке `fix/permissions`.
2. **Переплата в карточке.** REQ-CALC-07 и Q-20: «переплата показывается в карточке». В коде её нет — долг `Math.Max(0, …)`.
3. **Номера вопросов в ADR-0010.** В «Контексте» и «Решении» 0 ₽ отнесён к Q-20, а оплата ОЭК за оплаченный месяц — к Q-26;
   в `07-open-questions.md` наоборот (Q-26 — 0 ₽, Q-20 — переплата и ОЭК).
4. **Водяной знак.** `03-architecture.md`: «Телефону схема отдаётся с водяным знаком». Знак рисует страница `/m/`, API отдаёт картинку без знака.
5. **ADR-0002** до сих пор «Предложено», хотя сервер и база уже на PostgreSQL (`docs/09-server.md`).
