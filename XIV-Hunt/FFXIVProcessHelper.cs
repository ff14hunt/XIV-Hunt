using System.Collections.Generic;
using System.Linq;

namespace FFXIV_GameSense
{

    public static class FFXIVProcessHelper
    {
        public static IList<System.Diagnostics.Process> GetFFXIVProcessList()
            => (from x in System.Diagnostics.Process.GetProcessesByName("ffxiv")
                where !x.HasExited && x.MainModule != null && x.MainModule.ModuleName == "ffxiv.exe"
                select x).Union(
                from x in System.Diagnostics.Process.GetProcessesByName("ffxiv_dx11")
                where !x.HasExited && x.MainModule != null && x.MainModule.ModuleName == "ffxiv_dx11.exe"
                select x).ToList();

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