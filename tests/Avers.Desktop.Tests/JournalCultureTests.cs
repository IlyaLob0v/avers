using System.Globalization;
using Avalonia.Headless.XUnit;
using Avers.Domain;

namespace Avers.Desktop.Tests;

public class JournalCultureTests
{
    [AvaloniaTheory]
    [InlineData("en-US")]
    [InlineData("ru-RU")]
    public void OpeningReadOnlyJournal_DoesNotRewriteTicketDates(string culture)
    {
        var previous = CultureInfo.CurrentCulture;
        var previousUi = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(culture);
            using var app = new DesktopApp("orlov");
            var dates = app.Vm.TicketsScreen.Tickets.Select(ticket => (ticket.Id, ticket.Date, ticket.Time)).ToArray();

            app.Go(AppModule.Disp);

            Assert.Equal(dates, app.Vm.TicketsScreen.Tickets.Select(ticket => (ticket.Id, ticket.Date, ticket.Time)).ToArray());
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
            CultureInfo.CurrentUICulture = previousUi;
        }
    }
}
