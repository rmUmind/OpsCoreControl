using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

namespace OpsCoreControl
{
    public static class AutoScrollBehavior
    {
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
        public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return;

            if (d is ListBox listBox)
            {
                ((INotifyCollectionChanged)listBox.Items).CollectionChanged += (s, args) =>
                {
                    if (listBox.Items.Count > 0)
                        listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                };
            }
            else if (d is TextBox textBox)
            {
                textBox.TextChanged += (s, args) => textBox.ScrollToEnd();
            }
        }
    }
}