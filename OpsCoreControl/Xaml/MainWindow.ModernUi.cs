using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace OpsCoreControl
{
    public partial class MainWindow
    {
        private ResourceDictionary _modernResources;

        private void _toggleInterfaceMode_Click(object sender, RoutedEventArgs e)
        {
            ApplyInterfaceMode(!_useModernInterface);
            Properties.Settings.Default.UseModernInterface = _useModernInterface;
            Properties.Settings.Default.Save();
        }

        private void ApplyInterfaceMode(bool modern)
        {
            _useModernInterface = modern;

            if (_interfaceModeMenuItem != null)
                _interfaceModeMenuItem.Header = modern ? "Классический интерфейс" : "Современный интерфейс";

            if (modern)
            {
                if (_modernResources == null)
                {
                    _modernResources = new ResourceDictionary
                    {
                        Source = new Uri("/OpsCoreControl;component/Xaml/ModernStyles.xaml", UriKind.Relative)
                    };
                }

                if (!Resources.MergedDictionaries.Contains(_modernResources))
                    Resources.MergedDictionaries.Add(_modernResources);

                MinWidth = 980;
                FontFamily = new FontFamily("Segoe UI");
            }
            else
            {
                if (_modernResources != null)
                    Resources.MergedDictionaries.Remove(_modernResources);

                MinWidth = 760;
                ClearValue(FontFamilyProperty);
            }

            RefreshImplicitStyles(this);
        }

        private static void RefreshImplicitStyles(DependencyObject root)
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                DependencyObject child = VisualTreeHelper.GetChild(root, i);
                FrameworkElement element = child as FrameworkElement;

                if (element != null && element.ReadLocalValue(FrameworkElement.StyleProperty) == DependencyProperty.UnsetValue)
                {
                    element.Style = null;
                    element.SetResourceReference(FrameworkElement.StyleProperty, element.GetType());
                }

                RefreshImplicitStyles(child);
            }
        }
    }
}
