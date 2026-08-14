using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Management;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using static OpsCoreControl.Log;

// Класс дашборда: раз в секунду собирает данные о системе (CPU, RAM, диски, сеть, USB, батарея)
// и шлёт снапшот подписчикам через событие Updated. Тяжёлые запросы (WMI, netsh, веб)
// выполняет реже — раз в 5/10/60 секунд, чтобы не грузить систему.
namespace OpsCoreControl
{
    internal class DashBoard : IDisposable
    {
        private const int IntervalSeconds = 1;   // базовый интервал; тяжёлые запросы реже

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        // Счётчики производительности для быстрых метрик (CPU, RAM, диск).
        private readonly PerformanceCounter _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private readonly PerformanceCounter _ramAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");
        private readonly PerformanceCounter _vramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
        private readonly PerformanceCounter _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        private readonly PerformanceCounter _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        private readonly double _totalRamMb = GetTotalPhysicalMemoryBytes() / (1024.0 * 1024.0);
        private DateTime? _bootTime;
        private int _tick;

        // Кэш тяжёлых данных, обновляется не каждый тик.
        private WifiSnapshot _wifi = new WifiSnapshot();
        private List<AdapterSnapshot> _adapters = new List<AdapterSnapshot>();
        private List<UsbSnapshot> _usb = new List<UsbSnapshot>();
        private List<PageFileSnapshot> _pageFiles = new List<PageFileSnapshot>();
        private Dictionary<string, DiskMeta> _diskMeta = new Dictionary<string, DiskMeta>(StringComparer.OrdinalIgnoreCase);
        private string _battery = "—";
        private string _publicIp = "—";

        public event Action<DashboardData> Updated;

        // Служебные данные диска: UNC-путь и тип (берутся из WMI).
        private class DiskMeta
        {
            public string Unc;
            public string Type;
        }

        // Прогревает счётчики и запускает цикл сбора данных.
        public DashBoard()
        {
            _cpuCounter.NextValue();          // первый замер счётчиков даёт 0 — прогреваем
            _diskReadCounter.NextValue();
            _diskWriteCounter.NextValue();
            _ = Loop();
        }

        // Возвращает общий объём RAM в байтах через WMI.
        private static ulong GetTotalPhysicalMemoryBytes()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                return searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["TotalPhysicalMemory"] as ulong? ?? 0;
            }
        }

        // Фоновый цикл: собирает снапшот и уведомляет подписчиков, пока не отменят.
        private async Task Loop()
        {
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    DashboardData data = await Task.Run(() => Collect());
                    var dispatcher = Application.Current?.Dispatcher;
                    dispatcher?.BeginInvoke(new Action(() => Updated?.Invoke(data)));
                }
                catch (Exception ex)
                {
                    Log.Add($"Ошибка обновления дашборда: {ex.Message}", LogType.Error);
                }

                try { await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), _cts.Token); }
                catch (TaskCanceledException) { break; }
            }
        }

        // Собирает полный снапшот. Счётчики — каждый тик, тяжёлые запросы — по счётчику тиков.
        private DashboardData Collect()
        {
            _tick++;

            if (_tick == 1 || _tick % 5 == 0)      // ~раз в 5 с
            {
                _wifi = CollectWifi();
                _adapters = CollectAdapters();
                _diskMeta = CollectDiskMeta();
                _battery = CollectBattery();
            }
            if (_tick == 1 || _tick % 10 == 0)     // ~раз в 10 с
            {
                _usb = CollectUsb();
                _pageFiles = CollectPageFiles();
            }
            if (_tick == 1 || _tick % 60 == 0)     // ~раз в минуту
            {
                _publicIp = CollectPublicIp();
            }

            var data = new DashboardData
            {
                CpuPercent = _cpuCounter.NextValue(),
                VramPercent = _vramCounter.NextValue(),
                DiskReadMbSec = _diskReadCounter.NextValue() / (1024.0 * 1024.0),
                DiskWriteMbSec = _diskWriteCounter.NextValue() / (1024.0 * 1024.0)
            };

            double availableMb = _ramAvailableCounter.NextValue();
            data.RamTotalMb = _totalRamMb;
            data.RamUsedMb = _totalRamMb - availableMb;
            data.RamPercent = _totalRamMb > 0 ? (float)(data.RamUsedMb / _totalRamMb * 100.0) : 0f;

            data.Disks = CollectDisks();
            data.Wifi = _wifi;
            data.Adapters = _adapters;
            data.Usb = _usb;
            data.PageFiles = _pageFiles;

            Process[] procs = Process.GetProcesses();
            int processCount = procs.Length;
            foreach (Process p in procs) p.Dispose();   // не течём дескрипторами

            data.System = new SystemSnapshot
            {
                PcName = Environment.MachineName,
                UserName = Environment.UserName,
                Uptime = GetUptime(),
                ProcessCount = processCount,
                Battery = _battery,
                PublicIp = _publicIp
                ,OsVersion = Environment.OSVersion.VersionString
                ,Architecture = Environment.Is64BitOperatingSystem ? "64-разрядная" : "32-разрядная"
            };

            return data;
        }

        // Список логических дисков с типом и UNC для сетевых.
        private List<DiskSnapshot> CollectDisks()
        {
            var result = new List<DiskSnapshot>();
            foreach (DriveInfo drive in DriveInfo.GetDrives())
            {
                string letter = drive.Name.TrimEnd('\\');
                var snap = new DiskSnapshot { Letter = letter, Type = "Другой", Unc = "" };

                if (_diskMeta.TryGetValue(letter, out DiskMeta meta))
                {
                    snap.Type = meta.Type;
                    snap.Unc = meta.Unc ?? "";
                }
                else
                {
                    snap.Type = MapDriveInfoType(drive.DriveType);
                }

                if (drive.IsReady)
                {
                    // Диск мог отвалиться во время чтения — пропускаем молча, чтобы не спамить.
                    try
                    {
                        snap.Label = drive.VolumeLabel;
                        snap.TotalGb = drive.TotalSize / (1024.0 * 1024.0 * 1024.0);
                        snap.FreeGb = drive.TotalFreeSpace / (1024.0 * 1024.0 * 1024.0);
                        snap.FreePercent = drive.TotalSize > 0 ? (double)drive.TotalFreeSpace / drive.TotalSize * 100.0 : 0;
                    }
                    catch { }
                }
                result.Add(snap);
            }
            return result;
        }

        // Метаданные дисков из WMI: тип и UNC-путь для сетевых.
        private Dictionary<string, DiskMeta> CollectDiskMeta()
        {
            var map = new Dictionary<string, DiskMeta>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT Name, ProviderName, DriveType FROM Win32_LogicalDisk"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string name = mo["Name"]?.ToString();
                        if (name == null) continue;
                        map[name] = new DiskMeta
                        {
                            Unc = mo["ProviderName"]?.ToString(),
                            Type = MapWmiDriveType(mo["DriveType"]?.ToString())
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить метаданные дисков: {ex.Message}", LogType.Error);
            }
            return map;
        }

        // Данные WiFi из netsh: статус, SSID, уровень сигнала.
        private WifiSnapshot CollectWifi()
        {
            var snap = new WifiSnapshot { Connected = false, Ssid = "—", SignalPercent = 0 };
            try
            {
                string output = RunAndCapture("netsh", "wlan show interfaces");
                if (string.IsNullOrWhiteSpace(output)) return snap;

                // Разбираем вывод построчно: строки вида "Ключ : значение".
                foreach (string rawLine in output.Split('\n'))
                {
                    string line = rawLine.Trim();
                    int idx = line.IndexOf(':');
                    if (idx < 0) continue;
                    string key = line.Substring(0, idx).Trim();
                    string val = line.Substring(idx + 1).Trim();

                    if (key.Contains("Состояние") || key.IndexOf("State", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        snap.Connected = val.Contains("подключено") || val.IndexOf("connected", StringComparison.OrdinalIgnoreCase) >= 0;
                    }
                    else if ((key.Contains("Имя сети") || key.IndexOf("SSID", StringComparison.OrdinalIgnoreCase) >= 0)
                             && key.IndexOf("BSSID", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        snap.Ssid = val;
                    }
                    else if (key.Contains("Сигнал") || key.IndexOf("Signal", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string digits = new string(val.Where(char.IsDigit).ToArray());
                        if (digits.Length > 0) snap.SignalPercent = int.Parse(digits);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить данные WiFi: {ex.Message}", LogType.Error);
            }
            return snap;
        }

        // Сетевые адаптеры: статус, IP, скорость. Loopback и туннели пропускаем.
        private List<AdapterSnapshot> CollectAdapters()
        {
            var result = new List<AdapterSnapshot>();
            try
            {
                foreach (NetworkInterface nic in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Loopback) continue;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Tunnel) continue;

                    string ip = "—";
                    try
                    {
                        UnicastIPAddressInformation v4 = nic.GetIPProperties().UnicastAddresses
                            .FirstOrDefault(a => a.Address.AddressFamily == AddressFamily.InterNetwork);
                        if (v4 != null) ip = v4.Address.ToString();
                    }
                    catch { }

                    string type;
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) type = "Wi-Fi";
                    else if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) type = "Ethernet";
                    else type = nic.NetworkInterfaceType.ToString();

                    result.Add(new AdapterSnapshot
                    {
                        Name = nic.Name,
                        Type = type,
                        Status = nic.OperationalStatus == OperationalStatus.Up ? "Подключён" : "Отключён",
                        Ip = ip,
                        SpeedMbps = nic.Speed / 1000000
                    });
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить список сетевых адаптеров: {ex.Message}", LogType.Error);
            }
            return result;
        }

        // USB-устройства из WMI.
        private List<UsbSnapshot> CollectUsb()
        {
            var result = new List<UsbSnapshot>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Caption, Description FROM Win32_PnPEntity WHERE PNPDeviceID LIKE 'USB%'"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        result.Add(new UsbSnapshot
                        {
                            Name = mo["Caption"]?.ToString() ?? "—",
                            Description = mo["Description"]?.ToString() ?? ""
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить USB-устройства: {ex.Message}", LogType.Error);
            }
            return result;
        }

        // Фактически выделенный и используемый файл подкачки по каждому диску.
        private List<PageFileSnapshot> CollectPageFiles()
        {
            var result = new List<PageFileSnapshot>();
            try
            {
                using (var searcher = new ManagementObjectSearcher(
                    "SELECT Name, AllocatedBaseSize, CurrentUsage, PeakUsage FROM Win32_PageFileUsage"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        result.Add(new PageFileSnapshot
                        {
                            Path = mo["Name"]?.ToString() ?? "—",
                            AllocatedMb = Convert.ToUInt32(mo["AllocatedBaseSize"] ?? 0),
                            CurrentUsageMb = Convert.ToUInt32(mo["CurrentUsage"] ?? 0),
                            PeakUsageMb = Convert.ToUInt32(mo["PeakUsage"] ?? 0)
                        });
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить сведения о файле подкачки: {ex.Message}", LogType.Error);
            }
            return result;
        }

        // Заряд батареи (для ноутбуков); на ПК без батареи вернёт "нет".
        private string CollectBattery()
        {
            try
            {
                using (var searcher = new ManagementObjectSearcher("SELECT EstimatedChargeRemaining, BatteryStatus FROM Win32_Battery"))
                {
                    foreach (ManagementObject mo in searcher.Get())
                    {
                        string charge = mo["EstimatedChargeRemaining"]?.ToString() ?? "?";
                        string status = mo["BatteryStatus"]?.ToString();
                        bool charging = status == "6" || status == "7" || status == "8";
                        return $"{charge}% ({(charging ? "зарядка" : "от батареи")})";
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить статус батареи: {ex.Message}", LogType.Error);
            }
            return "нет";
        }

        // Публичный IP через внешний сервис.
        private string CollectPublicIp()
        {
            try
            {
                using (var client = new WebClient())
                {
                    return client.DownloadString("https://api.ipify.org").Trim();
                }
            }
            catch (Exception ex)
            {
                Log.Add($"Не удалось получить публичный IP: {ex.Message}", LogType.Error);
                return "—";
            }
        }

        // Время работы системы с последней загрузки (кэшируем время загрузки).
        private string GetUptime()
        {
            if (_bootTime == null)
            {
                try
                {
                    using (var searcher = new ManagementObjectSearcher("SELECT LastBootUpTime FROM Win32_OperatingSystem"))
                    {
                        foreach (ManagementObject mo in searcher.Get())
                        {
                            _bootTime = ManagementDateTimeConverter.ToDateTime(mo["LastBootUpTime"]?.ToString());
                        }
                    }
                }
                catch (Exception ex)
                {
                    _bootTime = DateTime.Now;
                    Log.Add($"Не удалось получить время загрузки системы: {ex.Message}", LogType.Error);
                }
            }
            TimeSpan span = DateTime.Now - _bootTime.Value;
            return $"{(int)span.TotalDays}д {span.Hours}ч {span.Minutes}м";
        }

        // Запускает команду и возвращает её вывод. Рассчитано на быстрые команды (netsh).
        private string RunAndCapture(string fileName, string args)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = args,
                UseShellExecute = false,
                RedirectStandardOutput = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage)
            };
            using (Process p = Process.Start(psi))
            {
                string output = p.StandardOutput.ReadToEnd();
                p.WaitForExit(5000);
                return output;
            }
        }

        // Перевод кода типа диска из WMI в понятное имя.
        private string MapWmiDriveType(string code)
        {
            switch (code)
            {
                case "2": return "Съёмный";
                case "3": return "Локальный";
                case "4": return "Сетевой";
                case "5": return "CD";
                case "6": return "RAM";
                default: return "Другой";
            }
        }

        // Перевод типа диска из DriveInfo в понятное имя.
        private string MapDriveInfoType(DriveType t)
        {
            switch (t)
            {
                case DriveType.Fixed: return "Локальный";
                case DriveType.Removable: return "Съёмный";
                case DriveType.CDRom: return "CD";
                case DriveType.Network: return "Сетевой";
                case DriveType.Ram: return "RAM";
                default: return "Другой";
            }
        }

        // Останавливает цикл сбора и освобождает счётчики.
        public void Dispose()
        {
            _cts.Cancel();
            _cpuCounter.Dispose();
            _ramAvailableCounter.Dispose();
            _vramCounter.Dispose();
            _diskReadCounter.Dispose();
            _diskWriteCounter.Dispose();
            Log.Add("Дашборд остановлен.", LogType.Info);
        }
    }
}

