using System.Collections.Generic;

// Модели данных для дашборда.
// Класс DashBoard периодически собирает их в DashboardData и шлёт подписчикам через событие Updated.
namespace OpsCoreControl
{
    // Снимок состояния логического диска.
    public class DiskSnapshot
    {
        public string Letter { get; set; }
        public string Label { get; set; }
        public string Type { get; set; }      // Локальный / Сетевой / Съёмный / CD
        public double TotalGb { get; set; }
        public double FreeGb { get; set; }
        public double FreePercent { get; set; }
        public string Unc { get; set; }       // только для сетевых
    }

    // Снимок состояния сетевого адаптера.
    public class AdapterSnapshot
    {
        public string Name { get; set; }
        public string Type { get; set; }      // WiFi / Ethernet
        public string Status { get; set; }    // Up / Down
        public string Ip { get; set; }
        public long SpeedMbps { get; set; }
    }

    // Снимок USB-устройства.
    public class UsbSnapshot
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class PageFileSnapshot
    {
        public string Path { get; set; }
        public uint AllocatedMb { get; set; }
        public uint CurrentUsageMb { get; set; }
        public uint PeakUsageMb { get; set; }
    }

    // Снимок состояния WiFi.
    public class WifiSnapshot
    {
        public bool Connected { get; set; }
        public string Ssid { get; set; } = "—";
        public int SignalPercent { get; set; }
    }

    // Снимок общих сведений о системе.
    public class SystemSnapshot
    {
        public string PcName { get; set; }
        public string UserName { get; set; }
        public string Uptime { get; set; }
        public int ProcessCount { get; set; }
        public string Battery { get; set; }
        public string PublicIp { get; set; }
        public string OsVersion { get; set; }
        public string Architecture { get; set; }
    }

    // Полный снапшот дашборда: всё, что показывается на панели мониторинга.
    public class DashboardData
    {
        public float CpuPercent { get; set; }
        public double RamTotalMb { get; set; }
        public double RamUsedMb { get; set; }
        public float RamPercent { get; set; }
        public float VramPercent { get; set; }
        public double DiskReadMbSec { get; set; }
        public double DiskWriteMbSec { get; set; }
        public List<DiskSnapshot> Disks { get; set; } = new List<DiskSnapshot>();
        public WifiSnapshot Wifi { get; set; } = new WifiSnapshot();
        public List<AdapterSnapshot> Adapters { get; set; } = new List<AdapterSnapshot>();
        public List<UsbSnapshot> Usb { get; set; } = new List<UsbSnapshot>();
        public List<PageFileSnapshot> PageFiles { get; set; } = new List<PageFileSnapshot>();
        public SystemSnapshot System { get; set; } = new SystemSnapshot();
    }
}
