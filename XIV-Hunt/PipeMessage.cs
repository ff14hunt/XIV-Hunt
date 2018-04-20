using System.Runtime.InteropServices;

namespace FFXIV_GameSense
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    class PipeMessage
    {
        public int PID { get; private set; }
        public PMCommand Cmd { get; private set; }
        public byte Parameter { get; set; }

        public PipeMessage(int pid, PMCommand cmd)
        {
            PID = pid;
            Cmd = cmd;
        }
    }

    enum PMCommand : byte
    {
        Exit = 0,
        SlashInstance = 1,
        PlayNote = 2
    }
}
