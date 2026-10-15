using Avers.Domain;
using Microsoft.EntityFrameworkCore;

namespace Avers.Data;

/// <summary>
/// Учебные данные Омска, чтобы оболочка была живой до подключения настоящей базы.
/// При миграции со старого Аверса этот класс заменяется импортом.
/// </summary>
public static class DemoSeeder
{
    public static readonly DateOnly WorkDate = new(2026, 8, 31);

    /// <summary>С чего открывается учебная база: пользователь, адрес, лицевой счёт и заявка.</summary>
    public const string StartLogin = "sokolova";
    public const string StartStreet = "Волочаевская";
    public const string StartHouse = "19Д";
    public const string StartSection = "2";
    public const string StartApartment = "68";
    public const string StartAccount = "1932068";
    public const int StartTicket = 1001;

    public static void EnsureSeeded(AversDbContext db)
    {
        if (db.Users.AsNoTracking().Any())
            return;

        var zhil = Org("ООО «Жилсервис-Центр»", "Филиал Советский",
            "г. Омск, ул. Дианова, 7, каб. 12", "(3812) 45-12-30, (3812) 45-12-31",
            "Кузнецов Андрей Викторович", "8-913-645-1102",
            "Сидорова Елена Николаевна", "8-913-645-1188");
        var teplo = Org("ПЖРЭУ «Центральное»", "Участок № 4",
            "г. Омск, ул. Братская, 18", "(3812) 36-40-11",
            "Морозов Павел Ильич", "8-908-314-2290", "", "");

        Street S(string name) => new() { Name = name };
        var stAv = S("Авангардная");
        var stBr = S("Братская");
        var stVol = S("Волочаевская");
        var stLen = S("Ленина");
        db.Streets.AddRange(
            stAv, S("Авиагородок"), stBr, stVol, S("Дианова"), S("Красный Путь"),
            stLen, S("Магистральная"), S("Пушкина"), S("Серова"), S("Тарская"), S("Химиков"));

        var hAv12 = House(stAv, "12", "0402", "1200000", 5, 3, 3, 60, "Советский АО", zhil,
            "Кровля подъездов 1 и 3. Подъезд 2 питается от стояка 1.", "Нет.",
            "Чердак под. 1, справа от выхода на кровлю.", "Электрощитовая под. 1, ярус 2.",
            "Ключ от чердака в щитовой под. 1, на крюке.",
            "Старый кабель частично заменён в 2024 г. на под. 3.", new(2026, 2, 11));
        var hBr18 = House(stBr, "18", "0104", "1800000", 9, 6, 12, 216, "Центральный АО", teplo,
            "Кровля, по одной антенне на каждый подъезд.", "Воздушные линии между под. 3–4 и 5–6.",
            "Техэтаж, шкафы у нечётных стояков.", "Щитовая техэтажа, под. 1 и под. 4.",
            "Комплект ключей в диспетчерской ПЖРЭУ, участок № 4.",
            "Дом на балансе с 1996 г. Часть стояков — старый СКПТ «Стрела».", new(2025, 11, 20),
            "Шум и пропадание на стояке 9 (под. 5)", "Нет ЗИП на усилитель, ожидаем поставку", new(2026, 9, 4));
        var hVol19 = House(stVol, "19", "0402", "1931000", 5, 3, 3, 72, "Советский АО", zhil,
            "Кровля, стояки 1–3.", "Нет.", "Чердак под. 1.", "Щит под. 1.", "Консьерж под. 1.", "", new(2026, 3, 12));
        var hVol19a = House(stVol, "19А", "0402", "1931500", 5, 2, 2, 36, "Советский АО", zhil,
            "Кровля, стояки 1–2.", "Нет.", "Чердак.", "Щит под. 1.", "Консьерж под. 1.", "", new(2026, 3, 20));
        var hVol19d = House(stVol, "19Д", "0402", "1932000", 9, 4, 8, 144, "Советский АО", zhil,
            "Кровля, стояки 1–8. На каждом подъезде — коллективная антенна.",
            "Переход под. 2 — под. 3, пролёт 18 м, RG-11.",
            "Техэтаж: стояк 2 (под. 1–2) и стояк 6 (под. 3–4).",
            "Щит освещения техэтажа, подъезд 1, слева от люка.",
            "Консьерж под. 1. Дубликат — диспетчерская АДС, ячейка 14.",
            "Домовая разводка RG-11, абонентские отводы на 5 и 9 этажах. Шкаф СКПТ опломбирован 04.2026. Воздушный переход — осмотр при ППР сентября.",
            new(2026, 4, 18),
            "Нет сигнала на стояке 3 (подъезд 2), этажи 5–9",
            "Нет доступа на техэтаж — ключ у жильца кв. 41 до 26.08",
            new(2026, 8, 27));
        var hVol21 = House(stVol, "21", "0403", "1940000", 9, 3, 6, 96, "Советский АО", zhil,
            "Кровля, стояки 1–6.", "Нет.", "Техэтаж.", "Щит под. 1.", "АДС.", "", new(2026, 5, 8));
        var hVol23 = House(stVol, "23", "0403", "1941000", 9, 3, 6, 80, "Советский АО", zhil,
            "Кровля, стояки 1–6.", "Нет.", "Техэтаж.", "Щит под. 1.", "АДС.", "", new(2026, 5, 14));
        var hLen44 = House(stLen, "44", "0104", "440000", 5, 2, 2, 40, "Центральный АО", teplo,
            "Кровля под. 1, питание на оба подъезда.", "Нет.",
            "Чердак, один магистральный усилитель.", "Щит лестничной клетки под. 1, 1 этаж.",
            "У старшей по дому, кв. 7.", "Дом малоквартирный, договор на ТО действует.",
            new(2026, 6, 3));

        db.Houses.AddRange(hAv12, hBr18, hVol19, hVol19a, hVol19d, hVol21, hVol23, hLen44);

        Scheme(hVol19d, "Схема СКПТ Волочаевская 19Д", "SVG", new(2026, 4, 18));
        Scheme(hVol19d, "Ведомость отводов", "XLSX", new(2026, 3, 2));
        Scheme(hAv12, "Схема СКПТ Авангардная 12", "PNG", new(2026, 2, 11));
        Scheme(hBr18, "Схема СКПТ Братская 18", "JPEG", new(2025, 11, 20));
        Scheme(hBr18, "План воздушных переходов", "SVG", new(2025, 6, 14));
        Scheme(hLen44, "Схема СКПТ Ленина 44", "SVG", new(2026, 6, 3));

        AddPpr(hVol19d, "DDDDPEEEPEEE");
        AddPpr(hAv12, "DDEEEPEEEPEE");
        AddPpr(hBr18, "OEEEEPEEEDDE");
        AddPpr(hLen44, "EEEEEDEEPEEE");

        var ivanova = Sub(hVol19d, "104412", "1932068", "Иванова", "Мария", "Петровна", "8-983-623-3122", "68", "2",
            new(2014, 3, 12), "Льгота не применяется. Договор на техобслуживание СКПТ.", "0402", "0402-1932068", 2014, new(2026, 7, 9));
        var belousov = Sub(hVol19d, "104389", "1932041", "Белоусов", "Сергей", "Николаевич", "8-913-441-8807", "41", "2",
            new(2009, 11, 2), "Хранит ключ от техэтажа. С 20.08 по 26.08 в отъезде.", "0402", "0402-1932041", 2009, new(2026, 8, 3));
        var petrov = Sub(hAv12, "88102", "1200112", "Петров", "Алексей", "Иванович", "8-908-150-4411", "12", "1",
            new(2018, 6, 1), "", "0402", "0402-1200112", 2018, new(2026, 5, 22));
        var gromova = Sub(hLen44, "22007", "440007", "Громова", "Ольга", "Васильевна", "8-951-333-0190", "7", "1",
            new(2003, 4, 15), "Старшая по дому. Ключ от чердака.", "0104", "0104-440007", 2003, new(2026, 8, 1));
        var saveliev = Sub(hBr18, "55190", "180090", "Савельев", "Игорь", "Дмитриевич", "8-913-200-7744", "90", "3",
            new(2011, 9, 1), "Задолженность более трёх месяцев.", "0104", "0104-180090", 2011, new(2026, 3, 28));
        var chernova = Sub(hBr18, "55115", "180015", "Чернова", "Наталья", "Александровна", "8-904-329-6610", "15", "1",
            new(2001, 2, 20), "Долг свыше 450 ₽. Судебный приказ в Дело-Аверс.", "0104", "0104-180015", 2001, new(2025, 12, 12));
        var kozlov = Sub(hAv12, "88003", "1200003", "Козлов", "Виктор", "Семёнович", "8-3812-55-19-02", "3", "1",
            new(1998, 10, 1), "Последняя оплата более трёх лет назад.", "0402", "0402-1200003", 1996, new(2022, 6, 14));
        db.Subscribers.AddRange(ivanova, belousov, petrov, gromova, saveliev, chernova, kozlov);

        Contract(ivanova, "ТО-2014/1932068");
        Contract(belousov, "ТО-2009/1932041", "Белоусова Галина Ивановна");
        Contract(petrov, "ТО-2018/1200112");
        Contract(gromova, "ТО-2003/440007");
        Contract(saveliev, "ТО-2011/180090");
        Contract(chernova, "ТО-2001/180015", "Чернов Александр Петрович");
        Contract(kozlov, "ТО-1998/1200003");

        PayYear(ivanova, (1, 14), (2, 11), (3, 10), (4, 9), (5, 13), (6, 10), (7, 9));
        PayYear(belousov, (1, 20), (2, 18), (3, 19), (4, 17), (5, 16), (6, 18), (7, 15), (8, 3));
        PayYear(petrov, (1, 21), (2, 20), (3, 21), (4, 22), (5, 22));
        PayYear(gromova, (1, 8), (2, 5), (3, 6), (4, 7), (5, 6), (6, 5), (7, 7), (8, 1));
        PayYear(saveliev, (1, 27), (2, 26), (3, 28));
        PayYear(chernova);
        PayYear(kozlov);

        var julyIvanova = ivanova.Payments.First(p => p.Month == 7);
        julyIvanova.Source = "ОЭК";
        julyIvanova.RecordedBy = "Импорт реестра ОЭК";
        julyIvanova.PayerName = "Иванов Павел Сергеевич";

        chernova.CourtCases.Add(new CourtCase { DelaAversId = "ДА-88421", OrderNumber = "2-1140/2026", OrderDate = new(2026, 5, 21), Court = "Мировой судья с/у № 47", Amount = 1140, Status = "Выдан, направлен приставам" });
        kozlov.CourtCases.Add(new CourtCase { DelaAversId = "ДА-77109", OrderNumber = "2-0881/2025", OrderDate = new(2025, 11, 3), Court = "Мировой судья с/у № 12", Amount = 3420, Status = "В производстве" });

        ivanova.Receipts.Add(new UnifiedReceipt { InnerCode = "104412", PzhreuIndex = "0402", Account = "1932068", Moment = new(2026, 7, 9), Amount = 95, Paid = 95, NOs = "8812", NKass = "02", NOper = "44190", NKvit = "КВ-33811" });
        ivanova.Receipts.Add(new UnifiedReceipt { InnerCode = "104412", PzhreuIndex = "0402", Account = "1932068", Moment = new(2026, 6, 10), Amount = 95, Paid = 95, NOs = "8701", NKass = "02", NOper = "43902", NKvit = "КВ-33004" });

        db.Tickets.AddRange(
            T(1001, new(2026, 8, 31), new(8, 14), hVol19d, ivanova, "68", "2", "8-983-623-3122", TicketKind.Antenna,
                "Нет изображения, снег на всех каналах. Просит мастера после 15:00.", "", "", TicketStatus.Accepted, "Соколова Н.В.", "стояк 3, отвод 5 этажа"),
            T(1000, new(2026, 8, 31), new(7, 40), hBr18, saveliev, "90", "3", "8-913-200-7744", TicketKind.Amplifier,
                "Пропадание сигнала вечером", "", "Малышев Р.А.", TicketStatus.InProgress, "Соколова Н.В.", ""),
            T(994, new(2026, 8, 24), new(16, 5), hAv12, petrov, "12", "1", "8-908-150-4411", TicketKind.Cable,
                "Обрыв отвода на 3 этаже", "Заменён отвод, проверена розетка, сигнал в норме", "Малышев Р.А.", TicketStatus.Done, "Орлова Т.И.", ""),
            T(993, new(2026, 8, 24), new(11, 22), hVol19d, belousov, "41", "2", "8-913-441-8807", TicketKind.Antenna,
                "Нет сигнала на 5–9 эт. стояка 3", "Осмотр. Нет доступа на техэтаж", "Новиков Д.С.", TicketStatus.Deferred, "Орлова Т.И.", ""),
            T(980, new(2026, 8, 18), new(9, 10), hLen44, gromova, "7", "1", "8-951-333-0190", TicketKind.Antenna,
                "Слабый сигнал на 1 канале", "Подтяжка F-разъёма на чердаке, уровень восстановлен", "Малышев Роман Алексеевич", TicketStatus.Done, "Соколова Н.В.", ""),
            T(971, new(2026, 8, 12), new(13, 48), hVol19d, ivanova, "68", "2", "8-983-623-3122", TicketKind.Cable,
                "Искрит розетка ТВ", "Заменена абонентская розетка", "Новиков Дмитрий Сергеевич", TicketStatus.Done, "Орлова Т.И.", ""),
            // Заявки за прошлые месяцы и прошлый год — чтобы было что смотреть по году и по периоду.
            T(952, new(2026, 7, 21), new(10, 3), hVol19d, belousov, "41", "2", "8-913-441-8807", TicketKind.Antenna,
                "Рябь на 3–5 каналах", "Заменён делитель на техэтаже", "Малышев Р.А.", TicketStatus.Done, "Соколова Н.В.", "стояк 3"),
            T(931, new(2026, 6, 9), new(15, 30), hAv12, kozlov, "3", "1", "8-3812-55-19-02", TicketKind.Antenna,
                "Нет сигнала после грозы", "Заменён блок питания усилителя, сигнал в норме", "Новиков Д.С.", TicketStatus.Done, "Орлова Т.И.", ""),
            T(918, new(2026, 5, 27), new(9, 45), hBr18, chernova, "15", "1", "8-904-329-6610", TicketKind.Cable,
                "Повреждён кабель в подъезде", "", "Малышев Р.А.", TicketStatus.Deferred, "Соколова Н.В.", "под. 1"),
            T(890, new(2026, 3, 16), new(12, 10), hVol19d, ivanova, "68", "2", "8-983-623-3122", TicketKind.Antenna,
                "Плохое изображение на всех каналах", "Настроена антенна на кровле под. 2", "Новиков Д.С.", TicketStatus.Done, "Соколова Н.В.", ""),
            T(861, new(2026, 1, 19), new(11, 0), hLen44, gromova, "7", "1", "8-951-333-0190", TicketKind.Amplifier,
                "Пропал сигнал во всём подъезде", "Заменён магистральный усилитель на чердаке", "Малышев Р.А.", TicketStatus.Done, "Орлова Т.И.", ""),
            T(842, new(2025, 11, 25), new(14, 20), hVol19d, ivanova, "68", "2", "8-983-623-3122", TicketKind.Antenna,
                "Снег на изображении", "Подтянуты разъёмы на отводе 9 этажа", "Малышев Р.А.", TicketStatus.Done, "Соколова Н.В.", ""),
            T(815, new(2025, 9, 8), new(10, 40), hBr18, saveliev, "90", "3", "8-913-200-7744", TicketKind.Antenna,
                "Не показывает часть каналов", "Замена отвода", "Новиков Д.С.", TicketStatus.Done, "Орлова Т.И.", ""));

        // Заявка прошлого года, принятая по адресу без привязки к абоненту: карточка квартиры её тоже показывает.
        db.Tickets.Add(new WorkTicket
        {
            Number = 812,
            Date = new(2025, 11, 17),
            Time = new(10, 5),
            House = hVol19d,
            Apartment = "68",
            Section = "2",
            Phone = "8-983-623-3122",
            Kind = TicketKind.Antenna,
            Fault = "Рябь на всех каналах",
            WorkDone = "Заменён абонентский кабель до розетки",
            Mechanic = "Малышев Р.А.",
            Status = TicketStatus.Done,
            AcceptedBy = "Соколова Н.В.",
            RisersNote = ""
        });

        db.Mechanics.AddRange(
            new Mechanic { FullName = "Малышев Роман Алексеевич", Position = "Радиомеханик 5 р.", Phone = "8-913-777-1020", Address = "г. Омск, ул. Серова, 9-21" },
            new Mechanic { FullName = "Новиков Дмитрий Сергеевич", Position = "Радиомеханик 4 р.", Phone = "8-908-441-3391", Address = "г. Омск, ул. Тарская, 22-8" },
            new Mechanic { FullName = "Ершов Павел Юрьевич", Position = "Бригадир", Phone = "8-951-220-0188", Address = "г. Омск, ул. Химиков, 14-55" });

        db.Materials.AddRange(
            new Material { Name = "Антенна индивидуальная широкополосная «Стрела 0-1»", Unit = "шт" },
            new Material { Name = "Кабель RG-11, бухта 100 м", Unit = "м" },
            new Material { Name = "Усилитель магистральный SA-102", Unit = "шт" },
            new Material { Name = "Блок питания 24 В / 2 А", Unit = "шт" },
            new Material { Name = "Розетка ТВ проходная", Unit = "шт" },
            new Material { Name = "F-разъём компрессионный", Unit = "шт" });

        hVol19d.Intercoms.Add(new IntercomPanel
        {
            Entrance = 2,
            PanelType = "Vizit БВД-SM101",
            SwitchBlock = "КМ-100, ниша 1 эт.",
            Keys = "Dallas, 144 ключа · мастер — АДС яч. 9",
            Power = "Щит под. 2, автомат «домофон»"
        });
        db.IntercomTickets.AddRange(
            new IntercomTicket { House = hVol19d, Date = new(2026, 8, 28), Entrance = 2, Fault = "Не открывает дверь с ключа кв. 68", Mechanic = "Ершов П.Ю.", Done = true },
            new IntercomTicket { House = hVol19d, Date = new(2026, 8, 11), Entrance = 1, Fault = "Нет связи с панелью", Mechanic = "Ершов П.Ю.", Done = true });

        db.Users.AddRange(
            User("admin", "Администратор", UserRole.Administrator, "Служба АИТ", true),
            User("engineer", "Иванов П.С.", UserRole.ChiefEngineer, "Главный инженер", false),
            User("sokolova", "Соколова Н.В.", UserRole.Dispatcher, "АДС, диспетчерская", false),
            User("orlov", "Орлов С.А.", UserRole.AdsHead, "АДС", true),
            User("orlova", "Орлова Т.И.", UserRole.Dispatcher, "АДС, диспетчерская", false),
            User("lawyer", "Кравцова И.М.", UserRole.Lawyer, "Юридический отдел", false),
            User("novikov", "Новиков Д.С.", UserRole.Reader, "Радиомеханик", false));

        db.SaveChanges();

        // Кто принял заявку: учебные заявки записаны с ФИО диспетчера, связываем их с пользователями.
        var users = db.Users.ToDictionary(u => u.DisplayName);
        foreach (var ticket in db.Tickets.ToList())
        {
            if (users.TryGetValue(ticket.AcceptedBy, out var user))
                ticket.AcceptedByUserId = user.Id;
            if (ticket.Status == TicketStatus.Done)
                ticket.DoneDate ??= ticket.Date;
        }
        db.SaveChanges();
    }

    private static Organization Org(string name, string branch, string address, string phones,
        string head, string headPhone, string eng, string engPhone) => new()
        {
            Name = name,
            Branch = branch,
            Address = address,
            Phones = phones,
            HeadName = head,
            HeadPhone = headPhone,
            EngineerName = eng,
            EngineerPhone = engPhone
        };

    private static House House(Street street, string number, string index, string account,
        int floors, int entrances, int risers, int apts, string zone, Organization org,
        string antennas, string aerial, string amp, string psu, string key, string other,
        DateOnly? lastPpr, string? fault = null, string? reason = null, DateOnly? fix = null) => new()
        {
            Street = street,
            Number = number,
            PzhreuIndex = index,
            PzhreuAccount = account,
            Floors = floors,
            Entrances = entrances,
            Risers = risers,
            Apartments = apts,
            Zone = zone,
            Serviced = true,
            Organization = org,
            Antennas = antennas,
            AerialCables = aerial,
            Amplifiers = amp,
            PowerSupplies = psu,
            TechFloorKey = key,
            OtherNotes = other,
            LastPpr = lastPpr,
            OpenFault = fault,
            OpenFaultReason = reason,
            OpenFaultPlannedFix = fix
        };

    private static Subscriber Sub(House house, string inner, string account, string last, string first, string pat,
        string phone, string apt, string section, DateOnly contract, string notes, string idx, string pzAcc,
        int from, DateOnly lastPay) => new()
        {
            House = house,
            InnerCode = inner,
            Account = account,
            LastName = last,
            FirstName = first,
            Patronymic = pat,
            Phone = phone,
            Apartment = apt,
            Section = section,
            ContractDate = contract,
            Notes = notes,
            PzhreuIndex = idx,
            PzhreuAccount = pzAcc,
            SettlementFrom = from,
            LastPaymentDate = lastPay
        };

    private static void Contract(Subscriber sub, string number, string holder = "")
    {
        sub.ContractNumber = number;
        sub.ContractHolder = holder;
    }

    private static WorkTicket T(int number, DateOnly date, TimeOnly time, House house, Subscriber sub,
        string apt, string section, string phone, TicketKind kind, string fault, string work, string mech,
        TicketStatus status, string accepted, string risers) => new()
        {
            Number = number,
            Date = date,
            Time = time,
            House = house,
            Subscriber = sub,
            Apartment = apt,
            Section = section,
            Phone = phone,
            Kind = kind,
            Fault = fault,
            WorkDone = work,
            Mechanic = mech,
            Status = status,
            AcceptedBy = accepted,
            RisersNote = risers
        };

    private static AppUser User(string login, string name, UserRole role, string dept, bool editAccount) => new()
    {
        Login = login,
        DisplayName = name,
        PasswordHash = Password.Hash(login, "avers"),
        Role = role,
        Department = dept,
        CanEditAccount = editAccount
    };

    private static void Scheme(House house, string name, string format, DateOnly updated) =>
        house.Schemes.Add(new HouseScheme { Name = name, Format = format, UpdatedAt = updated });

    private static void PayYear(Subscriber sub, params (int Month, int Day)[] paid)
    {
        var map = paid.ToDictionary(x => x.Month, x => x.Day);
        for (var m = 1; m <= 12; m++)
        {
            var isPaid = map.TryGetValue(m, out var day);
            sub.Payments.Add(new YearPayment
            {
                Year = 2026,
                Month = m,
                Tariff = 95,
                Source = isPaid ? "Касса" : null,
                RecordedBy = isPaid ? "Орлов С.А." : null,
                Amount = isPaid ? 95 : null,
                PaidAt = isPaid ? new DateOnly(2026, m, day) : null
            });
        }
    }

    private static void AddPpr(House house, string code)
    {
        for (var i = 0; i < 12; i++)
        {
            house.Ppr.Add(new PprEntry
            {
                Year = 2026,
                Month = i + 1,
                Status = code[i] switch
                {
                    'D' => PprStatus.Done,
                    'P' => PprStatus.Planned,
                    'O' => PprStatus.Overdue,
                    _ => PprStatus.Empty
                }
            });
        }
    }
}
