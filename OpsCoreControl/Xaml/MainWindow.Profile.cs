using OpsCoreControl.WorkingClasses;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Профили —
// просмотр пользовательских профилей и удаление выбранных.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // Загружает профили пользователей (строки приходят в список через событие лога LogProfile).
        // ВАЖНО: метод должен быть определён только в одном partial-файле. Если у тебя он есть
        // в MainWindow.Init.cs — удали его оттуда, иначе будет ошибка «уже содержит определение».
        private async Task LoadProfilesAsync()
        {
            _usersProfilesListBox.Items.Clear();
            await _userProfileManager.LoadUserProfiles();
        }

        // Показывает профили (кнопка на вкладке).
        private async void _showUsersProfiles_ClickAsync(object sender, RoutedEventArgs e)
        {
            await LoadProfilesAsync();
        }

        // Удаляет выбранные профили (с подтверждением) и перезагружает список.
        private async void _deleteProfile_Click(object sender, RoutedEventArgs e)
        {
            // Копируем выборку в отдельный список, чтобы не менять коллекцию во время обхода.
            var toDelete = new List<string>();
            foreach (string item in _usersProfilesListBox.SelectedItems)
                toDelete.Add(item);

            if (toDelete.Count == 0)
            {
                Log.Add("Профили не выбраны.", LogType.Error);
                return;
            }

            MessageBoxResult confirm = MessageBox.Show($"Удалить выбранные профили ({toDelete.Count} шт.)? Действие необратимо.", "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (confirm != MessageBoxResult.Yes) return;

            try
            {
                foreach (string item in toDelete)
                {
                    await _userProfileManager.DeleteProfileFolderAsync(item);
                }
                Log.Add("Выбранные профили удалены.", LogType.Success);
            }
            catch (Exception ex)
            {
                Log.Add($"Ошибка при удалении профилей: {ex.Message}", LogType.Error);
            }

            // Перечитываем список, чтобы оставшиеся профили отобразились актуально.
            await LoadProfilesAsync();
        }

        // Обновляет счётчик выбранных профилей.
        private void _usersProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _userProfilesCountLabel.Content = "Выбрано: " + _usersProfilesListBox.SelectedItems.Count.ToString();
            _deleteProfileButton.IsEnabled = _usersProfilesListBox.SelectedItems.Count > 0;
        }
    }
}
