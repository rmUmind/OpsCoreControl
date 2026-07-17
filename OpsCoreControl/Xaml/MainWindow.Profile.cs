using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _showUsersProfiles_ClickAsync(object sender, RoutedEventArgs e)
        {
            _usersProfilesListBox.Items.Clear();
            await ButtonHelper.ExecuteWithColorAsync((Button)sender, () => _userProfileManager.LoadUserProfiles());
        }
        private async void _deleteProfile_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var toDelete = _usersProfilesListBox.SelectedItems;
                foreach (string item in toDelete)
                {
                    await _userProfileManager.DeleteProfileFolderAsync(item);
                }
            }
            catch (Exception ex)
            {
                Log.Add(ex.Message, LogType.Error);
            }
            Log.Add("успешно удалено", LogType.Success);
        }
        private void _usersProfilesListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _userProfilesCountLabel.Content = "Count: " + _usersProfilesListBox.SelectedItems.Count.ToString();
        }
    }
}