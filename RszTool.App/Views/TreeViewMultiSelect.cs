using System.Windows;

namespace RszTool.App.Views
{
    public static class TreeViewMultiSelect
    {
        public static readonly DependencyProperty IsMultiSelectedProperty =
            DependencyProperty.RegisterAttached(
                "IsMultiSelected",
                typeof(bool),
                typeof(TreeViewMultiSelect),
                new FrameworkPropertyMetadata(false));

        public static bool GetIsMultiSelected(DependencyObject obj)
        {
            return (bool)obj.GetValue(IsMultiSelectedProperty);
        }

        public static void SetIsMultiSelected(DependencyObject obj, bool value)
        {
            obj.SetValue(IsMultiSelectedProperty, value);
        }
    }
}
