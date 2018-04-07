using System;
using System.Diagnostics;
using System.IO.Pipes;
using System.Reflection;
using System.Runtime.InteropServices;

namespace FFXIV_GameSense
{
    class PersistentNamedPipeServer
    {
        private static readonly string pipename = Assembly.GetExecutingAssembly().GetName().Name;
        private static volatile NamedPipeServerStream NPS;
        private static object NPSlock = new object();

        internal static NamedPipeServerStream Instance
        {
            get
            {
                if(NPS==null)
                {
                    lock(NPSlock)
                    {
                        if (NPS == null)
                        {
                            Initialize();
                        }
                    }
                }
                return NPS;
            }
        }

        private static void Initialize()
        {
            NPS = new NamedPipeServerStream(pipename, PipeDirection.Out, 254, PipeTransmissionMode.Message, PipeOptions.Asynchronous, 128, 128);
            NPS.WaitForConnectionAsync();
        }

        internal static void Restart()
        {
            if(NPS!=null)
            {
                if(NPS.IsConnected)
                    NPS.Disconnect();
                NPS.Dispose();
            }
            Initialize();
        }

        internal static bool SendPipeMessage(PipeMessage pipeMessage)
        {
            if(Instance.IsConnected)
            {
                int size = Marshal.SizeOf(pipeMessage);
                // Both managed and unmanaged buffers required.
                byte[] bytes = new byte[size];
                IntPtr ptr = Marshal.AllocHGlobal(size);
                // Copy object byte-to-byte to unmanaged memory.
                Marshal.StructureToPtr(pipeMessage, ptr, false);
                // Copy data from unmanaged memory to managed buffer.
                Marshal.Copy(ptr, bytes, 0, size);
                // Release unmanaged memory.
                Marshal.FreeHGlobal(ptr);
                Debug.WriteLine("Sending: " + BitConverter.ToString(bytes));
                Instance.Write(bytes, 0, bytes.Length);
                Instance.WaitForPipeDrain();
                return true;
            }
            return false;
        }
    }
}
