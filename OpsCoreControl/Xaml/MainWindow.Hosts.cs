using System.Windows;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Hosts.
// Просмотр содержимого файла hosts и открытие папки с ним.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // Загружает содержимое hosts в текстовое поле.
        private void _loadHostsButton_Click(object sender, RoutedEventArgs e)
        {
            string content = _hostsManager.ReadHosts();
            _hostsTextBox.Text = content;

            // Если пусто — чтение упало (ошибку уже залогировал ReadHosts), успех не пишем.
            if (!string.IsNullOrEmpty(content))
                Log.Add("Файл hosts загружен.", LogType.Info);
        }

        // Открывает папку с файлом hosts в Проводнике для ручной правки.
        private void _openHostsFolderButton_Click(object sender, RoutedEventArgs e)
        {
            _hostsManager.OpenHostsFolder();
        }
    }
}
