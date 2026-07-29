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

namespace OpsCoreControl
{
    internal class DashBoard : IDisposable
    {
        private const int IntervalSeconds = 1;   // базовый интервал; тяжёлые запросы реже

        private readonly CancellationTokenSource _cts = new CancellationTokenSource();

        private readonly PerformanceCounter _cpuCounter = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        private readonly PerformanceCounter _ramAvailableCounter = new PerformanceCounter("Memory", "Available MBytes");
        private readonly PerformanceCounter _vramCounter = new PerformanceCounter("Memory", "% Committed Bytes In Use");
        private readonly PerformanceCounter _diskReadCounter = new PerformanceCounter("PhysicalDisk", "Disk Read Bytes/sec", "_Total");
        private readonly PerformanceCounter _diskWriteCounter = new PerformanceCounter("PhysicalDisk", "Disk Write Bytes/sec", "_Total");

        private readonly double _totalRamMb = GetTotalPhysicalMemoryBytes() / (1024.0 * 1024.0);
        private DateTime? _bootTime;
        private int _tick;

        // кэш тяжёлых данных (обновляется не каждый тик)
        private WifiSnapshot _wifi = new WifiSnapshot();
        private List<AdapterSnapshot> _adapters = new List<AdapterSnapshot>();
        private List<UsbSnapshot> _usb = new List<UsbSnapshot>();
        private Dictionary<string, DiskMeta> _diskMeta = new Dictionary<string, DiskMeta>(StringComparer.OrdinalIgnoreCase);
        private string _battery = "—";
        private string _publicIp = "—";

        public event Action<DashboardData> Updated;

        private class DiskMeta
        {
            public string Unc;
            public string Type;
        }

        public DashBoard()
        {
            _cpuCounter.NextValue();          // первый замер счётчиков даёт 0 — прогреваем
            _diskReadCounter.NextValue();
            _diskWriteCounter.NextValue();
            _ = Loop();
        }

        private static ulong GetTotalPhysicalMemoryBytes()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                return searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["TotalPhysicalMemory"] as ulong? ?? 0;
            }
        }

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
                catch { }

                try { await Task.Delay(TimeSpan.FromSeconds(IntervalSeconds), _cts.Token); }
                catch (TaskCanceledException) { break; }
            }
        }

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
            };

            return data;
        }

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
            catch { }
            return map;
        }

        private WifiSnapshot CollectWifi()
        {
            var snap = new WifiSnapshot { Connected = false, Ssid = "—", SignalPercent = 0 };
            try
            {
                string output = RunAndCapture("netsh", "wlan show interfaces");
                if (string.IsNullOrWhiteSpace(output)) return snap;

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
            catch { }
            return snap;
        }

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
                    if (nic.NetworkInterfaceType == NetworkInterfaceType.Wireless80211) type = "WiFi";
                    else if (nic.NetworkInterfaceType == NetworkInterfaceType.Ethernet) type = "Ethernet";
                    else type = nic.NetworkInterfaceType.ToString();

                    result.Add(new AdapterSnapshot
                    {
                        Name = nic.Name,
                        Type = type,
                        Status = nic.OperationalStatus == OperationalStatus.Up ? "Up" : "Down",
                        Ip = ip,
                        SpeedMbps = nic.Speed / 1000000
                    });
                }
            }
            catch { }
            return result;
        }

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
            catch { }
            return result;
        }

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
            catch { }
            return "нет";
        }

        private string CollectPublicIp()
        {
            try
            {
                using (var client = new WebClient())
                {
                    return client.DownloadString("https://api.ipify.org").Trim();
                }
            }
            catch { return "—"; }
        }

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
                catch { _bootTime = DateTime.Now; }
            }
            TimeSpan span = DateTime.Now - _bootTime.Value;
            return $"{(int)span.TotalDays}д {span.Hours}ч {span.Minutes}м";
        }

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

        public void Dispose()
        {
            _cts.Cancel();
            _cpuCounter.Dispose();
            _ramAvailableCounter.Dispose();
            _vramCounter.Dispose();
            _diskReadCounter.Dispose();
            _diskWriteCounter.Dispose();
        }
    }
}