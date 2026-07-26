using OpsCoreControl.WorkingСlasses;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static OpsCoreControl.Log;

namespace OpsCoreControl
{
    public partial class MainWindow : Window
    {
        private async void _startCustomProcessButton_Click(object sender, RoutedEventArgs e)
        {
            await _serviceManager.startCustomProcess(_startCustomProcessTextBox.Text);
        }

        private void CopySelected_Click(object sender, RoutedEventArgs e)
        {
            if (_mainChatListBox.SelectedItems.Count == 0) return;

            var lines = _mainChatListBox.SelectedItems
                .Cast<object>()
                .Select(item => item.ToString());

            Clipboard.SetText(string.Join(Environment.NewLine, lines));
        }
    }
}