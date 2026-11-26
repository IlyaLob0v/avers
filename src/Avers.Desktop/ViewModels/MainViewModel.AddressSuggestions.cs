using Avers.Domain;

namespace Avers.Desktop.ViewModels;

public sealed partial class MainViewModel
{
    public IEnumerable<string> FindStreetSuggestions => Streets.Select(street => street.Name);

    public IEnumerable<string> FindHouseSuggestions => string.IsNullOrWhiteSpace(FindStreet) ? []
        : _houses.Where(house => house.Street.Name.Contains(FindStreet.Trim(), StringComparison.CurrentCultureIgnoreCase))
            .Select(house => house.Number).Distinct().OrderBy(number => number, NaturalOrder.Instance);

    partial void OnFindStreetChanged(string value) => OnPropertyChanged(nameof(FindHouseSuggestions));
}
