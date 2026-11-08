using System.Windows;
using RszTool.App.Resources;
using RszTool.App.Views;

namespace RszTool.App.Common
{
    public static class MessageBoxUtils
    {
        public static MessageBoxResult Show(string message, string? title = null, MessageBoxButton button = MessageBoxButton.OK, MessageBoxImage icon = MessageBoxImage.None)
        {
            return ModernMessageBoxWindow.Show(message, title ?? Texts.Message, button, icon);
        }

        public static MessageBoxResult ShowOk(string message, MessageBoxImage icon)
        {
            return ModernMessageBoxWindow.Show(message, Texts.Message, MessageBoxButton.OK, icon);
        }

        public static MessageBoxResult Error(string message)
        {
            return ShowOk(message, MessageBoxImage.Error);
        }

        public static MessageBoxResult Info(string message)
        {
            return ShowOk(message, MessageBoxImage.Information);
        }

        public static MessageBoxResult Warning(string message)
        {
            return ShowOk(message, MessageBoxImage.Warning);
        }

        public static bool Confirm(string message)
        {
            return ModernMessageBoxWindow.Show(message, Texts.Message, MessageBoxButton.OKCancel, MessageBoxImage.Question) == MessageBoxResult.OK;
        }

        public static MessageBoxResult YesNoCancel(string message)
        {
            return ModernMessageBoxWindow.Show(message, Texts.Message, MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
        }
    }
}
