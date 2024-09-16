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
        private Timer _timer;
        private WMIService _WMI;
        private static TrimuiService Instance;
        private static readonly object Lock = new object();

        public event PropertyChangedEventHandler PropertyChanged;

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

        public List<string> GetEmulators()
        {
            var emusPath = $"{DiskLetter}Emus";

            var result = new List<string>();

            try
            {
                if (!Directory.Exists(emusPath))
                {
                    throw new Exception("Emulators folder not found.");
                }

                string[] folders = Directory.GetDirectories(emusPath, "*", SearchOption.TopDirectoryOnly);

                result.AddRange(folders.Select(x => Path.GetFileName(x)));

                // remover depois

                //foreach (var item in folders)
                //{
                //    string configFilePath = Path.Combine(item, "config.json");

                //    var projectBasePath = AppDomain.CurrentDomain.BaseDirectory;
                //    var relativeDestinationPath = Path.Combine(projectBasePath, @"..\..\..\Resources\Images\Emulators");

                //    var destinationPath = Path.GetFullPath(relativeDestinationPath);

                //    if (File.Exists(configFilePath))
                //    {
                //        string jsonContent = File.ReadAllText(configFilePath);

                //        JObject config = JObject.Parse(jsonContent);

                //        string iconFileName = config["icon"]?.ToString();

                //        if (!string.IsNullOrEmpty(iconFileName))
                //        {
                //            string sourceImagePath = Path.Combine(item, iconFileName);

                //            if (File.Exists(sourceImagePath))
                //            {
                //                string destinationImagePath = Path.Combine(destinationPath, $"{Path.GetFileName(item)}{Path.GetExtension(iconFileName)}");

                //                File.Copy(sourceImagePath, destinationImagePath, overwrite: true);
                //                Console.WriteLine($"Imagem '{iconFileName}' copiada para '{destinationImagePath}'.");
                //            }
                //            else
                //            {
                //                Console.WriteLine($"Imagem '{iconFileName}' não encontrada em '{item}'.");
                //            }
                //        }
                //    }
                //}
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }

            return result.OrderBy(item => item).ToList();
        }
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
