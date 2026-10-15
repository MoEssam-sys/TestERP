using MudBlazor;

namespace One.Shared.Themes.MudBlazorThemes
{
    public static class AppTheme
    {
        public static MudTheme Light { get; } = new MudTheme
        {
            PaletteLight = new PaletteLight()
            {
                Primary = Colors.Indigo.Darken2,
                Secondary = Colors.BlueGray.Default,
                Tertiary = Colors.Blue.Default,
                Success = Colors.Green.Accent4,
                Warning = Colors.Amber.Darken2,
                Error = Colors.Red.Darken2,
                Info = Colors.Blue.Default,
                Background = "#f7f7fb"
            },
            Typography = new Typography()
            {
                Default = { FontFamily = ["Cairo", "Segoe UI", "Arial", "sans-serif"] }
            }
        };

        public static MudTheme Dark { get; } = new MudTheme
        {
            PaletteDark = new PaletteDark()
            {
                Primary = Colors.Indigo.Lighten2,
                Secondary = Colors.BlueGray.Lighten1,
                Background = "#121212",
                Surface = "#1e1e1e"
            },
            Typography = new Typography()
            {
                Default = { FontFamily = ["Cairo", "Segoe UI", "Arial", "sans-serif"] }
            }
        };
    }
}