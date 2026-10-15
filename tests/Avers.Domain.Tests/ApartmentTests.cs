using Avers.Domain;

namespace Avers.Domain.Tests;

public class ApartmentTests
{
    private static readonly Subscriber Ivanova = new()
    {
        Id = 1,
        HouseId = 10,
        Apartment = "68",
        LastName = "Иванова",
        FirstName = "Мария",
        Patronymic = "Петровна"
    };

    private static WorkTicket Ticket(int number, int house, string apt, int? subscriber, DateOnly date) =>
        new() { Number = number, HouseId = house, Apartment = apt, SubscriberId = subscriber, Date = date };

    [Fact]
    public void For_TakesLinkedAndUnlinkedOfSameApartment_NewestFirst()
    {
        var tickets = new[]
        {
            Ticket(1, 10, "68", 1, new(2026, 8, 12)),
            Ticket(2, 10, "68", null, new(2025, 11, 17)),
            Ticket(3, 10, "41", 2, new(2026, 8, 24)),
            Ticket(4, 11, "68", null, new(2026, 8, 1)),
            Ticket(5, 10, "68 ", null, new(2026, 8, 30)),
            Ticket(6, 10, "68", 7, new(2026, 8, 29))
        };

        var found = ApartmentTickets.For(tickets, Ivanova).Select(t => t.Number).ToList();

        Assert.Equal([5, 1, 2], found);
    }

    [Fact]
    public void For_FiltersByYear()
    {
        var tickets = new[]
        {
            Ticket(1, 10, "68", 1, new(2026, 8, 12)),
            Ticket(2, 10, "68", null, new(2025, 11, 17))
        };

        Assert.Equal([2], ApartmentTickets.For(tickets, Ivanova, 2025).Select(t => t.Number));
    }

    [Fact]
    public void ContractParty_DefaultsToSubscriber()
    {
        Assert.Equal("Иванова Мария Петровна", Ivanova.ContractParty);
        var other = new Subscriber { LastName = "Белоусов", ContractHolder = "  Белоусова Галина Ивановна " };
        Assert.Equal("Белоусова Галина Ивановна", other.ContractParty);
    }

    [Fact]
    public void ServiceOrganization_Title()
    {
        Assert.Equal("не указана", ServiceOrganizations.Title(null));
        Assert.Equal("ООО «Жилсервис-Центр», Филиал Советский",
            ServiceOrganizations.Title(new Organization { Name = "ООО «Жилсервис-Центр»", Branch = "Филиал Советский" }));
        Assert.Equal("ТСЖ «Рассвет»", ServiceOrganizations.Title(new Organization { Name = "ТСЖ «Рассвет»" }));
    }
}
