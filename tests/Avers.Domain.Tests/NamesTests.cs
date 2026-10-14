using Avers.Domain;

namespace Avers.Domain.Tests;

public class NamesTests
{
    [Fact]
    public void FullName_JoinsAllParts()
    {
        var s = new Subscriber { LastName = "Иванова", FirstName = "Мария", Patronymic = "Петровна" };

        Assert.Equal("Иванова Мария Петровна", s.FullName);
    }

    [Fact]
    public void FullName_SkipsEmptyParts()
    {
        var s = new Subscriber { LastName = "Петров", FirstName = "", Patronymic = " " };

        Assert.Equal("Петров", s.FullName);
    }

    [Fact]
    public void ShortName_UsesInitials()
    {
        var s = new Subscriber { LastName = "Белоусов", FirstName = "Игорь", Patronymic = "Олегович" };

        Assert.Equal("Белоусов И.О.", s.ShortName);
    }

    [Fact]
    public void ShortName_WithoutFirstNameIsLastNameOnly()
    {
        var s = new Subscriber { LastName = "Громова" };

        Assert.Equal("Громова", s.ShortName);
    }

    [Fact]
    public void AddressLine_IncludesStreet()
    {
        var house = new House { Number = "19Д", Street = new Street { Name = "Братская" } };

        Assert.Equal("ул. Братская, д. 19Д", house.AddressLine);
    }

    [Fact]
    public void AddressLine_WithoutStreetIsNumberOnly()
    {
        var house = new House { Number = "72/1" };

        Assert.Equal("д. 72/1", house.AddressLine);
    }
}
