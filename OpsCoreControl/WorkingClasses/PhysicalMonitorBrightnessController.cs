using OpsCoreControl;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using static OpsCoreControl.Log;

// Класс управления яркостью мониторов через Win32 API (dxva2.dll).
// Работает только с мониторами, поддерживающими DDC/CI (как правило, ноутбуки).
// Основано на: https://stackoverflow.com/questions/4013622/adjust-screen-brightness-using-c-sharp
namespace OpsCoreControl.WorkingClasses
{
public class PhysicalMonitorBrightnessController : IDisposable
{
    #region DllImport
    [DllImport("dxva2.dll", EntryPoint = "GetNumberOfPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetNumberOfPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, ref uint pdwNumberOfPhysicalMonitors);

    [DllImport("dxva2.dll", EntryPoint = "GetPhysicalMonitorsFromHMONITOR")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetPhysicalMonitorsFromHMONITOR(IntPtr hMonitor, uint dwPhysicalMonitorArraySize, [Out] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("dxva2.dll", EntryPoint = "GetMonitorBrightness")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetMonitorBrightness(IntPtr handle, ref uint minimumBrightness, ref uint currentBrightness, ref uint maxBrightness);

    [DllImport("dxva2.dll", EntryPoint = "SetMonitorBrightness")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetMonitorBrightness(IntPtr handle, uint newBrightness);

    [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitor")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyPhysicalMonitor(IntPtr hMonitor);

    [DllImport("dxva2.dll", EntryPoint = "DestroyPhysicalMonitors")]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool DestroyPhysicalMonitors(uint dwPhysicalMonitorArraySize, [In] PHYSICAL_MONITOR[] pPhysicalMonitorArray);

    [DllImport("user32.dll")]
    static extern bool EnumDisplayMonitors(IntPtr hdc, IntPtr lprcClip, EnumMonitorsDelegate lpfnEnum, IntPtr dwData);
    delegate bool EnumMonitorsDelegate(IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData);
    #endregion

    private IReadOnlyCollection<MonitorInfo> Monitors { get; set; }

    // При создании сразу получаем список мониторов.
    public PhysicalMonitorBrightnessController()
    {
        UpdateMonitors();
    }

    #region Get & Set
    // Ставит яркость (0-100%) на все мониторы.
    public bool Set(uint brightness)
    {
        return Set(brightness, true);
    }

    private bool Set(uint brightness, bool refreshMonitorsIfNeeded)
    {
        try
        {
            bool isSomeFail = false;
            foreach (var monitor in Monitors)
            {
                // Пересчитываем проценты в реальные значения конкретного монитора.
                uint realNewValue = (monitor.MaxValue - monitor.MinValue) * brightness / 100 + monitor.MinValue;
                if (SetMonitorBrightness(monitor.Handle, realNewValue))
                {
                    monitor.CurrentValue = realNewValue;
                }
                else
                {
                    isSomeFail = true;
                    if (refreshMonitorsIfNeeded) break; // выходим, чтобы обновить список и повторить
                }
            }

            // Не получилось или мониторов нет — обновляем список и пробуем ещё раз (уже без обновления).
            if (refreshMonitorsIfNeeded && (isSomeFail || !Monitors.Any()))
            {
                UpdateMonitors();
                return Set(brightness, false);
            }

            if (isSomeFail)
            {
                Log.Add($"Не удалось выставить яркость {brightness}% на всех мониторах.", LogType.Error);
                return false;
            }

            Log.Add($"Яркость выставлена на {brightness}%.", LogType.Success);
            return true;
        }
        catch (Exception ex)
        {
            Log.Add($"Ошибка при выставлении яркости: {ex.Message}", LogType.Error);
            return false;
        }
    }

    #endregion

    // Перечисляет мониторы и считывает их диапазон и текущую яркость.
    private void UpdateMonitors()
    {
        DisposeMonitors(this.Monitors);

        var monitors = new List<MonitorInfo>();
        EnumDisplayMonitors(IntPtr.Zero, IntPtr.Zero, (IntPtr hMonitor, IntPtr hdcMonitor, ref Rect lprcMonitor, IntPtr dwData) =>
        {
            uint physicalMonitorsCount = 0;
            if (!GetNumberOfPhysicalMonitorsFromHMONITOR(hMonitor, ref physicalMonitorsCount))
            {
                // Не удалось получить количество мониторов.
                return true;
            }

            var physicalMonitors = new PHYSICAL_MONITOR[physicalMonitorsCount];
            if (!GetPhysicalMonitorsFromHMONITOR(hMonitor, physicalMonitorsCount, physicalMonitors))
            {
                // Не удалось получить дескрипторы мониторов.
                return true;
            }

            foreach (PHYSICAL_MONITOR physicalMonitor in physicalMonitors)
            {
                uint minValue = 0, currentValue = 0, maxValue = 0;
                if (!GetMonitorBrightness(physicalMonitor.hPhysicalMonitor, ref minValue, ref currentValue, ref maxValue))
                {
                    DestroyPhysicalMonitor(physicalMonitor.hPhysicalMonitor);
                    continue;
                }

                var info = new MonitorInfo
                {
                    Handle = physicalMonitor.hPhysicalMonitor,
                    MinValue = minValue,
                    CurrentValue = currentValue,
                    MaxValue = maxValue,
                };
                monitors.Add(info);
            }

            return true;
        }, IntPtr.Zero);

        this.Monitors = monitors;
        Log.Add($"Найдено мониторов с управлением яркостью: {monitors.Count}", LogType.Debug);
    }

    // Освобождает дескрипторы мониторов.
    public void Dispose()
    {
        DisposeMonitors(Monitors);
        GC.SuppressFinalize(this);
    }

    private static void DisposeMonitors(IEnumerable<MonitorInfo> monitors)
    {
        if (monitors?.Any() == true)
        {
            PHYSICAL_MONITOR[] monitorArray = monitors.Select(m => new PHYSICAL_MONITOR { hPhysicalMonitor = m.Handle }).ToArray();
            DestroyPhysicalMonitors((uint)monitorArray.Length, monitorArray);
        }
    }

    #region Classes
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)]
    public struct PHYSICAL_MONITOR
    {
        public IntPtr hPhysicalMonitor;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 128)]
        public string szPhysicalMonitorDescription;
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect
    {
        public int left;
        public int top;
        public int right;
        public int bottom;
    }

    // Данные одного монитора: дескриптор и диапазон яркости.
    public class MonitorInfo
    {
        public uint MinValue { get; set; }
        public uint MaxValue { get; set; }
        public IntPtr Handle { get; set; }
        public uint CurrentValue { get; set; }
    }
    #endregion
}
}
