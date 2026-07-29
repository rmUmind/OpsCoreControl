using System.Collections.Generic;

namespace OpsCoreControl
{
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

    public class AdapterSnapshot
    {
        public string Name { get; set; }
        public string Type { get; set; }      // WiFi / Ethernet
        public string Status { get; set; }    // Up / Down
        public string Ip { get; set; }
        public long SpeedMbps { get; set; }
    }

    public class UsbSnapshot
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }

    public class WifiSnapshot
    {
        public bool Connected { get; set; }
        public string Ssid { get; set; } = "—";
        public int SignalPercent { get; set; }
    }

    public class SystemSnapshot
    {
        public string PcName { get; set; }
        public string UserName { get; set; }
        public string Uptime { get; set; }
        public int ProcessCount { get; set; }
        public string Battery { get; set; }
        public string PublicIp { get; set; }
    }

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
        public SystemSnapshot System { get; set; } = new SystemSnapshot();
    }
}