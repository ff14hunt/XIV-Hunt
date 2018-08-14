using Splat;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace FFXIV_GameSense
{

    public static class FFXIVProcessHelper
    {
        internal const string DX9ExeName = "ffxiv.exe";
        internal const string DX11ExeName = "ffxiv_dx11.exe";
        public static IList<System.Diagnostics.Process> GetFFXIVProcessList()
        {
            return (from x in System.Diagnostics.Process.GetProcessesByName("ffxiv")
                    where ValidateProcess(DX9ExeName, x)
                    select x).Union(
                           from x in System.Diagnostics.Process.GetProcessesByName("ffxiv_dx11")
                           where ValidateProcess(DX11ExeName, x)
                           select x).ToList();
        }

        private static bool ValidateProcess(string exeName, System.Diagnostics.Process x)
        {
            try
            {
                return !x.HasExited && x.MainModule != null && x.MainModule.ModuleName == exeName;
            }
            catch (Win32Exception ex)
            {
                LogHost.Default.ErrorException($"One or more FFXIV processes could not be validated. {App.GetAppTitle()} might be lacking privileges.", ex);
            }
            return false;
        }

        public static System.Diagnostics.Process GetFFXIVProcess(int pid = 0)
        {
            System.Diagnostics.Process result;
            try
            {
                IList<System.Diagnostics.Process> list = GetFFXIVProcessList();
                if (pid == 0)
                {
                    if (list.Any())
                    {
                        result = (
                            from x in list
                            orderby x.Id
                            select x).FirstOrDefault();
                    }
                    else
                    {
                        result = null;
                    }
                }
                else
                {
                    result = list.FirstOrDefault((System.Diagnostics.Process x) => x.Id == pid);
                }
            }
            catch
            {
                result = null;
            }
            return result;
        }
    }
}