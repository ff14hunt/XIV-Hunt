using System.Runtime.InteropServices;

namespace FFXIV_GameSense
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    class PipeMessage
    {
        public int PID { get; set; }
        public PMCommand Cmd { get; set; }
        public byte Parameter { get; set; }
    }

    enum PMCommand : byte
    {
        Exit = 0,
        SlashInstance = 1,
        PlayNote = 2
    }
}
