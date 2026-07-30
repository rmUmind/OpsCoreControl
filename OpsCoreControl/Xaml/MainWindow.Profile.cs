using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

// Часть главного окна: обработка вкладки Profiles —
// просмотр пользовательских профилей и удаление выбранных.
namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        // Загружает список профилей (в список они попадают через событие лога LogProfile).
        private async void _showUsersProfiles_ClickAsync(object sender, RoutedEventArgs e)
        {
            _usersProfilesListBox.Items.Clear();
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _userProfileManager.LoadUserProfiles());
        }

        // Удаляет выбранные профили (с подтверждением).
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
        }

        // Обновляет счётчик выбранных профилей.
        private void _usersProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _userProfilesCountLabel.Content = "Count: " + _usersProfilesListBox.SelectedItems.Count.ToString();
        }
    }
}