using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net.Http;
using System.IO;
using static OpsCoreControl.Logger;

namespace OpsCoreControl.WorkingСlasses
{
    internal class SoftwareUpdateManager
    {
        public async Task<bool> GetLatestVersionInfoAsync(string url, string directory, IProgress<int> progress = null)
        {
            try
            {
                await Task.Run(async () =>  { 
                HttpClient client = new HttpClient();
                    Logger.Log($"Отправка get запроса по адресу: {url}", LogType.Debug);
                    HttpResponseMessage response = await client.GetAsync(url);

                });
            }
            catch (Exception ex)
            {
                Logger.Log("Исключение при скичивание файла: " + ex.Message.ToString(), Logger.LogEntryType.Error);
                
            }
            return true;
        }
    }
}
