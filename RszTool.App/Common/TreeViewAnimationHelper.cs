using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;

namespace RszTool.App.Common
{
    public static class TreeViewAnimationHelper
    {
        private static bool _initialized;

        public static void Initialize()
        {
            if (_initialized) return;
            _initialized = true;

            EventManager.RegisterClassHandler(
                typeof(ToggleButton),
                ToggleButton.CheckedEvent,
                new RoutedEventHandler(OnExpanderCheckedChanged));

            EventManager.RegisterClassHandler(
                typeof(ToggleButton),
                ToggleButton.UncheckedEvent,
                new RoutedEventHandler(OnExpanderCheckedChanged));

            EventManager.RegisterClassHandler(
                typeof(TreeViewItem),
                TreeViewItem.ExpandedEvent,
                new RoutedEventHandler(OnTreeViewItemExpanded));
        }

        private static void OnExpanderCheckedChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (sender is not ToggleButton toggleButton) return;
                if (toggleButton.Name != "Expander") return;

                double speed = App.Instance?.SaveData?.AnimationSpeed ?? 1.0;
                bool isChecked = toggleButton.IsChecked == true;

                if (toggleButton.Template?.FindName("ExpandPath", toggleButton) is Path path)
                {
                    double toAngle = isChecked ? 90.0 : 0.0;
                    if (speed <= 0)
                    {
                        path.RenderTransform = new RotateTransform(toAngle);
                        return;
                    }

                    double durationSec = Math.Max(0.02, 0.16 / speed);
                    var rotate = new RotateTransform(isChecked ? 0.0 : 90.0);
                    path.RenderTransform = rotate;

                    var anim = new DoubleAnimation(toAngle, TimeSpan.FromSeconds(durationSec))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    rotate.BeginAnimation(RotateTransform.AngleProperty, anim);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TreeViewAnimationHelper] Expander animation error: {ex}");
            }
        }

        private static void OnTreeViewItemExpanded(object sender, RoutedEventArgs e)
        {
            try
            {
                if (e.OriginalSource != sender || sender is not TreeViewItem tvi) return;

                double speed = App.Instance?.SaveData?.AnimationSpeed ?? 1.0;
                if (speed <= 0) return; // 0x: instant!

                if (tvi.Template?.FindName("ItemsHost", tvi) is FrameworkElement itemsHost)
                {
                    double durationSec = Math.Max(0.02, 0.18 / speed);

                    var animOpacity = new DoubleAnimation(0.2, 1.0, TimeSpan.FromSeconds(durationSec))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    itemsHost.BeginAnimation(UIElement.OpacityProperty, animOpacity);

                    var trans = new TranslateTransform(0, -6.0);
                    itemsHost.RenderTransform = trans;
                    var animY = new DoubleAnimation(-6.0, 0.0, TimeSpan.FromSeconds(durationSec))
                    {
                        EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut }
                    };
                    trans.BeginAnimation(TranslateTransform.YProperty, animY);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[TreeViewAnimationHelper] TreeViewItem expand error: {ex}");
            }
        }
    }
}
