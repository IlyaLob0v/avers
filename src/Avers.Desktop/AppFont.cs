using Avalonia;
using Avalonia.Media;

namespace Avers.Desktop;

public static class AppFont
{
    /// <summary>
    /// Шрифт по умолчанию — IBM Plex Sans из сборки. Окна берут его из стиля, а всплывающие подсказки,
    /// списки и меню живут в своих окнах-попапах и без этой настройки рисовались бы другим шрифтом.
    /// </summary>
    public static AppBuilder WithAversFont(this AppBuilder builder) =>
        builder.With(new FontManagerOptions { DefaultFamilyName = "avares://Avers/Assets/Fonts#IBM Plex Sans" });
}
