using Newtonsoft.Json.Linq;
using System;
using System.ComponentModel;
using System.IO;
using System.Management;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using TrimuiSmartHub.Application.Services.WMI;

namespace TrimuiSmartHub.Application.Services.Trimui
{
    public class TrimuiService : INotifyPropertyChanged, IDisposable
    {
        private string _status = "Disconnected";

        private Brush _statusColor = Brushes.Red;

        private string _diskLetter = string.Empty;

        private readonly Timer _timer;

        private readonly WMIService _WMI;

        private static TrimuiService? Instance;

        private static readonly object Lock = new object();

        public event PropertyChangedEventHandler PropertyChanged;

        private string EmulatorPath { get => $"{DiskLetter}Emus"; }
        public string Status
        {
            get => _status;
            private set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged(nameof(Status));
                    UpdateStatusColor();
                }
            }
        }

        public Brush StatusColor
        {
            get => _statusColor;
            private set
            {
                if (_statusColor != value)
                {
                    _statusColor = value;
                    OnPropertyChanged(nameof(StatusColor));
                }
            }
        }

        public string DiskLetter
        {
            get => _diskLetter;
            private set
            {
                if (_diskLetter != value)
                {
                    _diskLetter = value;
                    OnPropertyChanged(nameof(DiskLetter));
                }
            }
        }

        public static TrimuiService New()
        {
            if (Instance  == null)
            {
                lock (Lock)
                {
                    if ( Instance == null)
                    {
                        Instance = new TrimuiService();
                    }
                }
            }

            return Instance;
        }
        private TrimuiService()
        {
            _WMI = new WMIService();
            _timer = new Timer(StatusMonitor, null, 2000, 1000);
        }

        public void Dispose()
        {
            _timer?.Change(Timeout.Infinite, 0);
            _timer?.Dispose();
        }

        private void SetStatusDisconnected()
        {
            Status = "Disconnected";
            DiskLetter = string.Empty;
        }

        private void UpdateStatusColor()
        {
            StatusColor = Status == "Connected" ? Brushes.LightGreen : Brushes.Red;
        }

        public void StatusMonitor(object state)
        {
            try
            {
                var trimuiDevice = _WMI.Search("select * from Win32_PnPEntity where Name like '%Trimui%'");

                if (trimuiDevice == null || trimuiDevice.Count == 0)
                {
                    SetStatusDisconnected();
                    return;
                }

                foreach (ManagementObject item in trimuiDevice)
                {
                    if (item["DeviceID"] == null || !item["DeviceID"].ToString().Contains("USBSTOR"))
                    {
                        SetStatusDisconnected();
                        return;
                    }

                    var diskSearch = _WMI.Search($"SELECT * FROM Win32_DiskDrive WHERE PNPDeviceID = '{item["DeviceID"].ToString().Replace("\\", "\\\\")}'");

                    if (diskSearch == null || diskSearch.Count == 0)
                    {
                        SetStatusDisconnected();
                        return;
                    }

                    foreach (ManagementObject device in diskSearch)
                    {
                        var partitionSearch = _WMI.Search($"ASSOCIATORS OF {{Win32_DiskDrive.DeviceID='{device["DeviceID"]}'}} WHERE AssocClass=Win32_DiskDriveToDiskPartition");

                        if (partitionSearch == null || partitionSearch.Count == 0)
                        {
                            SetStatusDisconnected();
                            return;
                        }

                        foreach (ManagementObject partition in partitionSearch)
                        {
                            var logicalDiskSearch = _WMI.Search($"ASSOCIATORS OF {{Win32_DiskPartition.DeviceID='{partition["DeviceID"]}'}} WHERE AssocClass=Win32_LogicalDiskToPartition");

                            if (logicalDiskSearch == null || logicalDiskSearch.Count == 0)
                            {
                                SetStatusDisconnected();
                                return;
                            }

                            foreach (ManagementObject logicalDisk in logicalDiskSearch)
                            {
                                string driveLetter = logicalDisk["DeviceID"]?.ToString();

                                if (string.IsNullOrEmpty(driveLetter))
                                {
                                    SetStatusDisconnected();
                                    return;
                                }

                                DiskLetter = driveLetter;
                                Status = "Connected";
                                return;
                            }
                        }
                    }
                }

                SetStatusDisconnected();
            }
            catch (Exception ex)
            {
                SetStatusDisconnected();
                Console.WriteLine($"Error in StatusMonitor: {ex.Message}");
            }
        }

        public static List<string>? GetFilesSafe(string directoryPath)
        {
            List<string> files = new List<string>();

            try
            {
                files.AddRange(Directory.GetFiles(directoryPath));

                foreach (var subDirectory in Directory.GetDirectories(directoryPath))
                {
                    try
                    {
                        files.AddRange(GetFilesSafe(subDirectory));
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
            }
            catch (Exception)
            {
              // ignore
            }

            return files?.Select(x => Path.GetFileName(x)).ToList();
        }

        private static bool IsFile(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.Directory) != FileAttributes.Directory;
        }

        private string? EmulatorConfig(string emulator, string property)
        {
            string[] folders = Directory.GetDirectories(EmulatorPath, "*", SearchOption.TopDirectoryOnly);

            var root = folders.Select(x => Path.GetFileName(x)).FirstOrDefault(x => x.Equals(emulator));

            string configFilePath = Path.Combine(Path.Combine(EmulatorPath, root), "config.json");

            if (!File.Exists(configFilePath)) return string.Empty;

            string jsonContent = File.ReadAllText(configFilePath);

            JObject config = JObject.Parse(jsonContent);

            string configValue = config[property]?.ToString();

            return configValue;
        }

        public List<string> GetRomsByEmulator(string emulator)
        {
            var result = new List<string>();

            string romsPath = EmulatorConfig(emulator, "rompath");

            if (string.IsNullOrEmpty(romsPath)) return result;

            string emulatorRomPath = Path.GetFullPath(Path.Combine(Path.Combine(EmulatorPath, emulator), romsPath));

            var roms = GetFilesSafe(emulatorRomPath);

            if (roms != null && roms?.Count > 0) result.AddRange(roms);

            return result;
        }

        public string GetImageFolder(string emulator)
        {
            string imagePath = EmulatorConfig(emulator, "imgpath");

            if (string.IsNullOrEmpty(imagePath)) return string.Empty;

            string emulatorImagesPath = Path.GetFullPath(Path.Combine(Path.Combine(EmulatorPath, emulator), imagePath));

            return emulatorImagesPath;
        }

        public string GetRomsFolder(string emulator)
        {
            string romPath = EmulatorConfig(emulator, "rompath");

            if (string.IsNullOrEmpty(romPath)) return string.Empty;

            string emulatorImagesPath = Path.GetFullPath(Path.Combine(Path.Combine(EmulatorPath, emulator), romPath));

            return emulatorImagesPath;
        }

        public List<string> GetEmulators()
        {
            var result = new List<string>();

            if(!Directory.Exists(EmulatorPath)) return result;
   
            string[] folders = Directory.GetDirectories(EmulatorPath, "*", SearchOption.TopDirectoryOnly);

            result.AddRange(folders.Select(x => Path.GetFileName(x))); 

            return result.OrderBy(item => item).ToList();
        }

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
