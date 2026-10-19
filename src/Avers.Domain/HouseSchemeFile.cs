namespace Avers.Domain;

/// <summary>Картинка схемы СКПТ. Лежит отдельно от <see cref="HouseScheme"/>, чтобы список схем не тянул файлы.</summary>
public sealed class HouseSchemeFile
{
    public int HouseSchemeId { get; set; }
    public HouseScheme Scheme { get; set; } = null!;
    public byte[] Content { get; set; } = [];
}
