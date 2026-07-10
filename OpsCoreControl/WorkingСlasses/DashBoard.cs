using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Diagnostics;
using System.Windows.Threading;
using System.Management;

namespace OpsCoreControl
{
    internal class DashBoard
    {
        private const int DashBoardIntervalRefresh = 1; // Интервал обнавления ДэшБорда

        public static ulong GetTotalPhysicalMemoryBytes()
        {
            using (var searcher = new ManagementObjectSearcher("SELECT TotalPhysicalMemory FROM Win32_ComputerSystem"))
            {
                return searcher.Get().Cast<ManagementObject>().FirstOrDefault()?["TotalPhysicalMemory"] as ulong? ?? 0;
            }
        }
        public ulong TotalRam = GetTotalPhysicalMemoryBytes();
        public event Action<ulong> totalRam;

        private PerformanceCounter RamUsage = new PerformanceCounter("Memory", "Available MBytes");
        public event Action<float> ramUsageUpdated;

        private static PerformanceCounter GetVirtualRamTotal = new PerformanceCounter("Memory", "Committed Bytes");
        public float VirtualRamTotal = GetVirtualRamTotal.NextValue();
        public event Action<float> virtualRamTotalUpdated;

        private PerformanceCounter VirtualRamUsage = new PerformanceCounter("Memory", "% Committed Bytes In Use");
        public event Action<float> virtualRamUsageUpdated;

        private PerformanceCounter CPUsage = new PerformanceCounter("Processor", "% Processor Time", "_Total");
        public event Action<float> cpUsageUpdated;

        private PerformanceCounter FreeSpace = new PerformanceCounter("LogicalDisk", "% Free Space", "C:");
        public event Action<float> freeSpaceUpdated;


        public DashBoard()
        {
            startDashBoard();
        }
        ~DashBoard()
        {
            Dispose();
        }
        public void Dispose()
        {
            RamUsage.Dispose();
            CPUsage.Dispose();
            VirtualRamUsage.Dispose();
        }
        public void startDashBoard()
        {
            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(DashBoardIntervalRefresh) };
            timer.Tick += (s, e) => RefreshData();
            timer.Start();
        }

        private void RefreshData()
        {
            // RAM
            totalRam?.Invoke(TotalRam);
            ramUsageUpdated?.Invoke(RamUsage.NextValue());

            // VRAM
            virtualRamTotalUpdated?.Invoke(VirtualRamTotal);
            virtualRamUsageUpdated?.Invoke(VirtualRamUsage.NextValue());

            // CPU
            cpUsageUpdated?.Invoke(CPUsage.NextValue());

            // Free space
            freeSpaceUpdated?.Invoke(FreeSpace.NextValue());
        }
    }
}