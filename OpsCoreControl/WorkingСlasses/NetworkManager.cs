using OpsCoreControl.HelperClasses;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static OpsCoreControl.Log;

namespace OpsCoreControl.WorkingСlasses
{
    internal class NetworkManager
    {
        public async Task<bool> ClearNonPagedPool ()
        {
            var psi = ConsoleHelper.cmdConsoleCommand("\"/c netsh winsock reset & netsh int ip reset & ipconfig /release & ipconfig /renew & ipconfig /flushdns\"");
            return await ConsoleHelper.LookForProcessEnd(psi, "Невыгружаемый пул успешно удален", "Ошибка при удаление папки профился", "Исключение при удаление невыгружаемого пула: ");
        }
    }
}
