using System;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

// Класс для кнопок: на время операции блокирует кнопку и подсвечивает её по результату.
// Жёлтый — выполняется, зелёный — успех, красный — ошибка.
public static class ButtonHelper
{
    // Расширение для Button: выполняет операцию и красит кнопку по итогу.
    public static async Task ExecuteWithColorAsync(this Button button, Func<Task<bool>> operation)
    {
        // блокируем кнопку, чтобы её не нажали повторно во время операции
        button.IsEnabled = false;
        button.Background = Brushes.Yellow; // жёлтый — операция в процессе

        try
        {
            bool success = await operation();
            // зелёный — успех, красный — неудача
            button.Background = success ? Brushes.Green : Brushes.Red;
        }
        catch
        {
            // операция упала с исключением — красим в красный и пробрасываем дальше,
            // исключение поймает глобальный обработчик и запишет в лог
            button.Background = Brushes.Red;
            throw;
        }
        finally
        {
            // разблокируем в любом случае, иначе кнопка останется намертво выключенной
            button.IsEnabled = true;
        }
    }
}