using System;
using System.Windows;
using System.Windows.Media;
using Wpf.Ui.Controls;
using WpfMessageBoxResult = System.Windows.MessageBoxResult;
using WpfMessageBoxButton = System.Windows.MessageBoxButton;
using WpfMessageBoxImage = System.Windows.MessageBoxImage;

namespace RszTool.App.Views
{
    public partial class ModernMessageBoxWindow : FluentWindow
    {
        public WpfMessageBoxResult Result { get; private set; } = WpfMessageBoxResult.None;

        public ModernMessageBoxWindow()
        {
            InitializeComponent();
        }

        public static WpfMessageBoxResult Show(
            string message,
            string title = "提示",
            WpfMessageBoxButton buttons = WpfMessageBoxButton.OK,
            WpfMessageBoxImage icon = WpfMessageBoxImage.None,
            Window? owner = null)
        {
            var app = Application.Current;
            if (app?.Dispatcher != null && !app.Dispatcher.CheckAccess())
            {
                return app.Dispatcher.Invoke(() => Show(message, title, buttons, icon, owner));
            }

            var dialog = new ModernMessageBoxWindow();
            dialog.Title = title;
            dialog.TitleBarControl.Title = title;
            dialog.MessageText.Text = message ?? string.Empty;

            // Setup Icon
            switch (icon)
            {
                case WpfMessageBoxImage.Error:
                    dialog.DialogIcon.Symbol = SymbolRegular.DismissCircle28;
                    dialog.DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0x11, 0x23));
                    break;
                case WpfMessageBoxImage.Warning:
                    dialog.DialogIcon.Symbol = SymbolRegular.Warning28;
                    dialog.DialogIcon.Foreground = new SolidColorBrush(Color.FromRgb(0xF7, 0xAD, 0x01));
                    break;
                case WpfMessageBoxImage.Question:
                    dialog.DialogIcon.Symbol = SymbolRegular.QuestionCircle28;
                    dialog.DialogIcon.Foreground = dialog.FindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;
                    break;
                case WpfMessageBoxImage.Information:
                default:
                    dialog.DialogIcon.Symbol = SymbolRegular.Info28;
                    dialog.DialogIcon.Foreground = dialog.FindResource("AccentFillColorDefaultBrush") as Brush ?? Brushes.DodgerBlue;
                    break;
            }

            // Setup Buttons
            dialog.BtnOk.Visibility = Visibility.Collapsed;
            dialog.BtnCancel.Visibility = Visibility.Collapsed;
            dialog.BtnYes.Visibility = Visibility.Collapsed;
            dialog.BtnNo.Visibility = Visibility.Collapsed;

            switch (buttons)
            {
                case WpfMessageBoxButton.OK:
                    dialog.BtnOk.Visibility = Visibility.Visible;
                    dialog.BtnOk.IsDefault = true;
                    dialog.Result = WpfMessageBoxResult.OK;
                    break;
                case WpfMessageBoxButton.OKCancel:
                    dialog.BtnOk.Visibility = Visibility.Visible;
                    dialog.BtnCancel.Visibility = Visibility.Visible;
                    dialog.BtnOk.IsDefault = true;
                    dialog.BtnCancel.IsCancel = true;
                    dialog.Result = WpfMessageBoxResult.Cancel;
                    break;
                case WpfMessageBoxButton.YesNo:
                    dialog.BtnYes.Visibility = Visibility.Visible;
                    dialog.BtnNo.Visibility = Visibility.Visible;
                    dialog.BtnYes.IsDefault = true;
                    dialog.BtnNo.IsCancel = true;
                    dialog.Result = WpfMessageBoxResult.No;
                    break;
                case WpfMessageBoxButton.YesNoCancel:
                    dialog.BtnYes.Visibility = Visibility.Visible;
                    dialog.BtnNo.Visibility = Visibility.Visible;
                    dialog.BtnCancel.Visibility = Visibility.Visible;
                    dialog.BtnYes.IsDefault = true;
                    dialog.BtnCancel.IsCancel = true;
                    dialog.Result = WpfMessageBoxResult.Cancel;
                    break;
            }

            // Setup Owner
            var resolvedOwner = owner ?? (app?.MainWindow != null && app.MainWindow.IsVisible ? app.MainWindow : null);
            if (resolvedOwner != null)
            {
                dialog.Owner = resolvedOwner;
                dialog.WindowStartupLocation = WindowStartupLocation.CenterOwner;
            }
            else
            {
                dialog.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }

            dialog.ShowDialog();
            return dialog.Result;
        }

        private void BtnOk_Click(object sender, RoutedEventArgs e)
        {
            Result = WpfMessageBoxResult.OK;
            Close();
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        {
            Result = WpfMessageBoxResult.Cancel;
            Close();
        }

        private void BtnYes_Click(object sender, RoutedEventArgs e)
        {
            Result = WpfMessageBoxResult.Yes;
            Close();
        }

        private void BtnNo_Click(object sender, RoutedEventArgs e)
        {
            Result = WpfMessageBoxResult.No;
            Close();
        }
    }
}
