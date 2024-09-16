using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading.Tasks;


namespace TrimuiSmartHub.Application.Services.WMI
{
    internal class WMIService
    {
        public WMIService() { }

        public ManagementObjectCollection Search(string query) {
            try
            {
                var searcher = new ManagementObjectSearcher(query);

                var result = searcher.Get();

                return result;
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
