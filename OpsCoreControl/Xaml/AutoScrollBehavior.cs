using System.Collections.Specialized;
using System.Windows;
using System.Windows.Controls;

// Класс прикрепляемого поведения (attached behavior) для автоскролла.
// Если элементу задано AutoScroll="True", он автоматически прокручивается вниз
// при добавлении содержимого. Работает с ListBox и TextBox.
namespace OpsCoreControl
{
    public static class AutoScrollBehavior
    {
        // Прикрепляемое свойство AutoScroll (в XAML: local:AutoScrollBehavior.AutoScroll).
        public static readonly DependencyProperty AutoScrollProperty =
            DependencyProperty.RegisterAttached(
                "AutoScroll",
                typeof(bool),
                typeof(AutoScrollBehavior),
                new PropertyMetadata(false, OnAutoScrollChanged));

        // Геттер/сеттер прикрепляемого свойства — без них XAML не сможет его читать и писать.
        public static bool GetAutoScroll(DependencyObject obj) => (bool)obj.GetValue(AutoScrollProperty);
        public static void SetAutoScroll(DependencyObject obj, bool value) => obj.SetValue(AutoScrollProperty, value);

        // Срабатывает при установке AutoScroll. Подписывается на изменение содержимого
        // и прокручивает элемент вниз при каждом добавлении.
        private static void OnAutoScrollChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (!(bool)e.NewValue) return; // реагируем только на включение

            if (d is ListBox listBox)
            {
                // При изменении коллекции элементов прокручиваем к последнему.
                ((INotifyCollectionChanged)listBox.Items).CollectionChanged += (s, args) =>
                {
                    if (listBox.Items.Count > 0)
                        listBox.ScrollIntoView(listBox.Items[listBox.Items.Count - 1]);
                };
            }
            else if (d is TextBox textBox)
            {
                // При изменении текста прокручиваем в конец.
                textBox.TextChanged += (s, args) => textBox.ScrollToEnd();
            }
        }
    }
}