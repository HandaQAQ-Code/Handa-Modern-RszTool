using System;

using System.ComponentModel;

using System.Linq;

using System.Windows;

using System.Windows.Media;

using Wpf.Ui.Appearance;

using Wpf.Ui.Controls;

using Wpf.Ui.Markup;

namespace RszTool.App.ViewModels

{

    public class ThemeManager : INotifyPropertyChanged

    {

        private ThemeE ThemeN;

        private static ThemeManager? instance;

        public static ThemeManager Instance

        {

            get => instance ??= new(ThemeE.Winui);

            private set => instance = value;

        }

        public SolidColorBrush LightForeground =>

            Application.Current.Resources["LightForeground"] as SolidColorBrush ?? Brushes.Black;

        public SolidColorBrush DarkForeground =>

            Application.Current.Resources["DarkForeground"] as SolidColorBrush ?? Brushes.White;

        public SolidColorBrush WinUIForeground =>

            Application.Current.Resources["DarkForeground"] as SolidColorBrush ?? Brushes.White;

        public SolidColorBrush MakaroForeground =>

            Application.Current.Resources["MakaroForeground"] as SolidColorBrush ?? Brushes.Black;

        public ThemeE CurrentTheme

        {

            get => ThemeN;

            set

            {

                if (ThemeN == value && instance != null) return;

                ThemeN = value;

                ApplyTheme(ThemeN);

                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentTheme)));

            }

        }

        public event PropertyChangedEventHandler? PropertyChanged;

        private ThemeManager(ThemeE userTheme)

        {

            ThemeN = userTheme;

            ApplyTheme(userTheme);

        }

        public static void Init(ThemeE userTheme)

        {

            Instance = new(userTheme);

        }

        private readonly struct Palette

        {

            public Color Background { get; init; }

            public Color Foreground { get; init; }

            public Color SecondaryForeground { get; init; }

            public Color TertiaryForeground { get; init; }

            public Color SelectedBackground { get; init; }

            public Color PressedBackground { get; init; }

            public Color ActiveBorder { get; init; }

            public Color InactiveBorder { get; init; }

            public Color CardBackgroundDefault { get; init; }

            public Color CardBackgroundSecondary { get; init; }

            public Color CardStroke { get; init; }

            public Color SubtleSecondary { get; init; }

            public Color SubtleTertiary { get; init; }

            public Color DisabledBackground { get; init; }

            public Color DisabledBorder { get; init; }

            public Color DisabledForeground { get; init; }

            public Color Accent { get; init; }

            public ApplicationTheme WpfUiTheme { get; init; }

        }

        private static Palette GetPalette(ThemeE theme) => theme switch

        {

            ThemeE.Winui => new Palette

            {

                Background = Color.FromRgb(0x2D, 0x2A, 0x3F),

                Foreground = Color.FromRgb(0xF5, 0xF5, 0xF5),

                SecondaryForeground = Color.FromRgb(0xD0, 0xCC, 0xE0),

                TertiaryForeground = Color.FromRgb(0xA8, 0xA3, 0xBD),

                SelectedBackground = Color.FromRgb(0x4A, 0x45, 0x66),

                PressedBackground = Color.FromRgb(0x6B, 0x64, 0x99),

                ActiveBorder = Color.FromRgb(0x9D, 0x8D, 0xF1),

                InactiveBorder = Color.FromRgb(0x8A, 0x82, 0xC2),

                CardBackgroundDefault = Color.FromRgb(0x35, 0x31, 0x4A),

                CardBackgroundSecondary = Color.FromRgb(0x3E, 0x3A, 0x56),

                CardStroke = Color.FromRgb(0x4E, 0x48, 0x6C),

                SubtleSecondary = Color.FromRgb(0x44, 0x3F, 0x5E),

                SubtleTertiary = Color.FromRgb(0x54, 0x4E, 0x74),

                DisabledBackground = Color.FromRgb(0x44, 0x40, 0x59),

                DisabledBorder = Color.FromRgb(0x5A, 0x55, 0x7A),

                DisabledForeground = Color.FromRgb(0x99, 0x95, 0xA8),

                Accent = Color.FromRgb(0x9D, 0x8D, 0xF1),

                WpfUiTheme = ApplicationTheme.Dark,

            },

            ThemeE.dark => new Palette

            {

                Background = Color.FromRgb(0x33, 0x33, 0x37),

                Foreground = Color.FromRgb(0xF1, 0xF1, 0xF1),

                SecondaryForeground = Color.FromRgb(0xCC, 0xCC, 0xCC),

                TertiaryForeground = Color.FromRgb(0x99, 0x99, 0x99),

                SelectedBackground = Color.FromRgb(0x2B, 0x3D, 0x4F),

                PressedBackground = Color.FromRgb(0x19, 0x56, 0x87),

                ActiveBorder = Color.FromRgb(0x00, 0x7A, 0xCC),

                InactiveBorder = Color.FromRgb(0xA3, 0xA5, 0xAB),

                CardBackgroundDefault = Color.FromRgb(0x2D, 0x2D, 0x30),

                CardBackgroundSecondary = Color.FromRgb(0x25, 0x25, 0x26),

                CardStroke = Color.FromRgb(0x3F, 0x3F, 0x46),

                SubtleSecondary = Color.FromRgb(0x3E, 0x3E, 0x42),

                SubtleTertiary = Color.FromRgb(0x48, 0x48, 0x4E),

                DisabledBackground = Color.FromRgb(0x55, 0x55, 0x55),

                DisabledBorder = Color.FromRgb(0x68, 0x68, 0x68),

                DisabledForeground = Color.FromRgb(0x80, 0x80, 0x80),

                Accent = Color.FromRgb(0x00, 0x7A, 0xCC),

                WpfUiTheme = ApplicationTheme.Dark,

            },

            ThemeE.light => new Palette

            {

                Background = Color.FromRgb(0xFF, 0xFF, 0xFF),

                Foreground = Color.FromRgb(0x18, 0x18, 0x18),

                SecondaryForeground = Color.FromRgb(0x5C, 0x5C, 0x5C),

                TertiaryForeground = Color.FromRgb(0x76, 0x76, 0x76),

                SelectedBackground = Color.FromRgb(0xE5, 0xF1, 0xFB),

                PressedBackground = Color.FromRgb(0xCC, 0xE4, 0xF7),

                ActiveBorder = Color.FromRgb(0x00, 0x78, 0xD4),

                InactiveBorder = Color.FromRgb(0xD4, 0xD4, 0xD4),

                CardBackgroundDefault = Color.FromRgb(0xF8, 0xF9, 0xFA),

                CardBackgroundSecondary = Color.FromRgb(0xF1, 0xF3, 0xF5),

                CardStroke = Color.FromRgb(0xE5, 0xE5, 0xE5),

                SubtleSecondary = Color.FromRgb(0xEA, 0xEA, 0xEA),

                SubtleTertiary = Color.FromRgb(0xDF, 0xDF, 0xDF),

                DisabledBackground = Color.FromRgb(0xF0, 0xF0, 0xF0),

                DisabledBorder = Color.FromRgb(0xDC, 0xDC, 0xDC),

                DisabledForeground = Color.FromRgb(0x88, 0x88, 0x88),

                Accent = Color.FromRgb(0x00, 0x78, 0xD4),

                WpfUiTheme = ApplicationTheme.Light,

            },

            ThemeE.Makaro => new Palette

            {

                Background = Color.FromRgb(0xF5, 0xF9, 0xF7),

                Foreground = Color.FromRgb(0x4B, 0x59, 0x55),

                SecondaryForeground = Color.FromRgb(0x66, 0x77, 0x73),

                TertiaryForeground = Color.FromRgb(0x82, 0x96, 0x92),

                SelectedBackground = Color.FromRgb(0xEF, 0xD9, 0xE1),

                PressedBackground = Color.FromRgb(0xDF, 0xC4, 0xCF),

                ActiveBorder = Color.FromRgb(0x5B, 0x97, 0xB5),

                InactiveBorder = Color.FromRgb(0xD4, 0xE2, 0xE0),

                CardBackgroundDefault = Color.FromRgb(0xEB, 0xF3, 0xF1),

                CardBackgroundSecondary = Color.FromRgb(0xE3, 0xEC, 0xE9),

                CardStroke = Color.FromRgb(0xD4, 0xE2, 0xE0),

                SubtleSecondary = Color.FromRgb(0xE6, 0xEF, 0xEA),

                SubtleTertiary = Color.FromRgb(0xDD, 0xE8, 0xE4),

                DisabledBackground = Color.FromRgb(0xF0, 0xF4, 0xF2),

                DisabledBorder = Color.FromRgb(0xD9, 0xE3, 0xE1),

                DisabledForeground = Color.FromRgb(0xAA, 0xB8, 0xB5),

                Accent = Color.FromRgb(0x5B, 0x97, 0xB5),

                WpfUiTheme = ApplicationTheme.Light,

            },

            _ => throw new ArgumentOutOfRangeException(nameof(theme))

        };

        public static void ApplyTheme(ThemeE theme)

        {

            var app = Application.Current;

            if (app == null) return;

            var p = GetPalette(theme);

            // 1. Sync WPF-UI appearance base

            try

            {

                ApplicationThemeManager.Apply(p.WpfUiTheme, WindowBackdropType.Mica, updateAccent: false);

                ApplicationAccentColorManager.Apply(p.Accent, p.WpfUiTheme);

                var themesDict = app.Resources.MergedDictionaries

                    .OfType<ThemesDictionary>()

                    .FirstOrDefault();

                if (themesDict != null)

                {

                    themesDict.Theme = p.WpfUiTheme;

                }

            }

            catch

            {

                // Fallback for environments without hardware Mica

            }

            // 2. Set Theme Brushes & Colors

            void SetOrUpdateBrush(object key, Color color)

            {

                if (app.Resources[key] is SolidColorBrush existing && !existing.IsFrozen)

                {

                    existing.Color = color;

                }

                else

                {

                    app.Resources[key] = new SolidColorBrush(color);

                }

            }

            // Retool compatibility brushes

            SetOrUpdateBrush("PrimaryBackground", p.Background);

            SetOrUpdateBrush("PrimaryForeground", p.Foreground);

            SetOrUpdateBrush("TextForeground", p.Foreground);

            SetOrUpdateBrush("SelectedBackground", p.SelectedBackground);

            SetOrUpdateBrush("PressedBackground", p.PressedBackground);

            SetOrUpdateBrush("ActiveBorder", p.ActiveBorder);

            SetOrUpdateBrush("InactiveBorder", p.InactiveBorder);

            SetOrUpdateBrush("DisabledBackgroundBrush", p.DisabledBackground);

            SetOrUpdateBrush("DisabledBorderBrush", p.DisabledBorder);

            SetOrUpdateBrush("DisabledForegroundBrush", p.DisabledForeground);

            // WPF-UI theme token overrides

            SetOrUpdateBrush("ApplicationBackgroundBrush", p.Background);

            SetOrUpdateBrush("CardBackgroundFillColorDefaultBrush", p.CardBackgroundDefault);

            SetOrUpdateBrush("CardBackgroundFillColorSecondaryBrush", p.CardBackgroundSecondary);

            SetOrUpdateBrush("CardStrokeColorDefaultBrush", p.CardStroke);

            SetOrUpdateBrush("SubtleFillColorSecondaryBrush", p.SubtleSecondary);

            SetOrUpdateBrush("SubtleFillColorTertiaryBrush", p.SubtleTertiary);

            SetOrUpdateBrush("TextFillColorPrimaryBrush", p.Foreground);

            SetOrUpdateBrush("TextFillColorSecondaryBrush", p.SecondaryForeground);

            SetOrUpdateBrush("TextFillColorTertiaryBrush", p.TertiaryForeground);

                        SetOrUpdateBrush("AccentFillColorDefaultBrush", p.Accent);

            SetOrUpdateBrush("AccentTextFillColorPrimaryBrush", p.Accent);

            SetOrUpdateBrush("TextOnAccentFillColorPrimaryBrush", Colors.White);

            SetOrUpdateBrush("ControlFillColorDefaultBrush", p.CardBackgroundDefault);

            SetOrUpdateBrush("ControlFillColorSecondaryBrush", p.CardBackgroundSecondary);

            SetOrUpdateBrush("ControlStrokeColorDefaultBrush", p.CardStroke);

            // SystemColors for standard WPF controls

            SetOrUpdateBrush(SystemColors.WindowBrushKey, p.Background);

            SetOrUpdateBrush(SystemColors.WindowTextBrushKey, p.Foreground);

            SetOrUpdateBrush(SystemColors.ControlBrushKey, p.CardBackgroundDefault);

            SetOrUpdateBrush(SystemColors.ControlTextBrushKey, p.Foreground);

            SetOrUpdateBrush(SystemColors.HighlightBrushKey, p.SelectedBackground);

            SetOrUpdateBrush(SystemColors.HighlightTextBrushKey, p.Foreground);

            SetOrUpdateBrush(SystemColors.MenuBrushKey, p.CardBackgroundDefault);

            SetOrUpdateBrush(SystemColors.MenuTextBrushKey, p.Foreground);

        }

    }

}

