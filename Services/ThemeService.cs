using System;
using System.Windows;
using System.Windows.Media;

namespace HardwareMonitor.Services;

public enum ThemeType { Dark, Light, Cyberpunk, Ocean }

public static class ThemeService
{
    public static ThemeType Current { get; private set; } = ThemeType.Light;

    private static readonly ThemeDef[] Themes =
    [
        new("深色",
            "#FF0D1117", "#FF161B22", "#FF30363D",
            "#FFE6EDF3", "#FF8B949E",
            "#FF58A6FF", "#FF3FB950", "#FFD29922", "#FFF85149", "#FFBC8CFF",
            "#E80D1117", "#30FFFFFF", "#15FFFFFF", "#15FFFFFF", "#30FFFFFF"),

        new("浅色",
            "#FFFAFBFC", "#FFEEF1F5", "#FFD0D7DE",
            "#FF1F2328", "#FF57606A",
            "#FF0969DA", "#FF1A7F37", "#FF9A6700", "#FFCF222E", "#FF8250DF",
            "#E8FAFBFC", "#40000000", "#18000000", "#15000000", "#30000000"),

        new("赛博朋克",
            "#FF0A0A1A", "#FF12122A", "#FF2A2A4A",
            "#FFFF00FF", "#FF8888CC",
            "#FF00FFFF", "#FF00FF88", "#FFFFAA00", "#FFFF0066", "#FFAA00FF",
            "#E80A0A1A", "#30FF00FF", "#15FFFFFF", "#15FFFFFF", "#30FF00FF"),

        new("海洋",
            "#FF0B1929", "#FF0F2440", "#FF1A3A5C",
            "#FFD4E4F7", "#FF7BA3C9",
            "#FF4FC3F7", "#FF00E5A0", "#FFFFD54F", "#FFFF7043", "#FFCE93D8",
            "#E80B1929", "#304FC3F7", "#15FFFFFF", "#15FFFFFF", "#304FC3F7"),
    ];

    public static string[] ThemeNames
    {
        get
        {
            var names = new string[Themes.Length];
            for (int i = 0; i < Themes.Length; i++) names[i] = Themes[i].Name;
            return names;
        }
    }

    public static void Apply(int index)
    {
        if (index < 0 || index >= Themes.Length) return;
        Current = (ThemeType)index;
        var t = Themes[index];
        var res = Application.Current.Resources;

        SetColor(res, "BgDark", t.BgDark);
        SetColor(res, "BgCard", t.BgCard);
        SetColor(res, "TextPrimary", t.TextPrimary);
        SetColor(res, "TextSecondary", t.TextSecondary);
        SetColor(res, "AccentBlue", t.AccentBlue);
        SetColor(res, "AccentGreen", t.AccentGreen);
        SetColor(res, "AccentOrange", t.AccentOrange);
        SetColor(res, "AccentRed", t.AccentRed);
        SetColor(res, "AccentPurple", t.AccentPurple);

        SetBrush(res, "BgDarkBrush", t.BgDark);
        SetBrush(res, "BgCardBrush", t.BgCard);
        SetBrush(res, "TextPrimaryBrush", t.TextPrimary);
        SetBrush(res, "TextSecondaryBrush", t.TextSecondary);
        SetBrush(res, "AccentBlueBrush", t.AccentBlue);
        SetBrush(res, "AccentGreenBrush", t.AccentGreen);
        SetBrush(res, "AccentOrangeBrush", t.AccentOrange);
        SetBrush(res, "AccentRedBrush", t.AccentRed);
        SetBrush(res, "AccentPurpleBrush", t.AccentPurple);
        SetBrush(res, "BorderBrush", t.Border);

        // Mini window resources
        SetBrush(res, "MiniBgBrush", t.MiniBg);
        SetBrush(res, "MiniBorderBrush", t.MiniBorder);
        SetBrush(res, "MiniCardBrush", t.MiniCard);
        SetBrush(res, "MiniBarBgBrush", t.MiniBarBg);
        SetBrush(res, "MiniHintBrush", t.MiniHint);

        ThemeChanged?.Invoke(null, (ThemeType)index);
    }

    public static event EventHandler<ThemeType>? ThemeChanged;

    private static void SetColor(ResourceDictionary res, string key, string hex)
        => res[key] = ParseColor(hex);

    private static void SetBrush(ResourceDictionary res, string key, string hex)
        => res[key] = new SolidColorBrush(ParseColor(hex));

    private static Color ParseColor(string hex)
        => (Color)ColorConverter.ConvertFromString(hex);
}

public record ThemeDef(
    string Name,
    string BgDark, string BgCard, string Border,
    string TextPrimary, string TextSecondary,
    string AccentBlue, string AccentGreen, string AccentOrange, string AccentRed, string AccentPurple,
    string MiniBg, string MiniBorder, string MiniCard, string MiniBarBg, string MiniHint);
