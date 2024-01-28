using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;

namespace JITProtection
{
    public static class Runtime
    {
        [DllImport("kernel32.dll")]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string procedureName);
        [DllImport("kernel32.dll")]
        private static extern IntPtr LoadLibrary(string dllToLoad);
        [DllImport("kernel32.dll")]
        public static extern bool FreeLibrary(IntPtr hModule);

        [DllImport("kernel32.dll", ExactSpelling = true, SetLastError = true)]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll", EntryPoint = "ExitProcess")]
        internal static extern void ExitProcess(int exitCode);

        private static IntPtr DLLHandle;
        private static string DLLPath;

        public static void Initialize()
        {
            int ec = -1;
            IntPtr intPtr = LoadLibrary("kernel32.dll");
            IntPtr procAddress = GetProcAddress(intPtr, "IsDebuggerPresent");
            byte[] array = new byte[1];
            
            Marshal.Copy(procAddress, array, 0, 1);
            if (array[0] == 233)
                ExitProcess(ec);

            procAddress = GetProcAddress(intPtr, "CheckRemoteDebuggerPresent");
            Marshal.Copy(procAddress, array, 0, 1);
            if (array[0] == 233)
                ExitProcess(ec);

            Type typeFromHandle = typeof(Debugger);
            MethodInfo method = typeFromHandle.GetMethod("get_IsAttached");
            IntPtr functionPointer = method.MethodHandle.GetFunctionPointer();
            Marshal.Copy(functionPointer, array, 0, 1);
            if (array[0] == 51 || IsDebuggerPresent())
                ExitProcess(ec);

            AppDomain.CurrentDomain.ProcessExit += new EventHandler(ProcessExit);
            AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(ProcessExit);

            DLLPath = $"{Path.Combine(Path.GetTempPath(), $"[{Guid.NewGuid().ToString().ToUpper()}]".Replace("[", "{").Replace("]", "}"))}.dll";
            var s = typeof(Runtime).Assembly.GetManifestResourceStream("DLL");
            var m = new MemoryStream();
            s.CopyTo(m);
            File.WriteAllBytes(DLLPath, m.ToArray());
            DLLHandle = Runtime.LoadLibrary(DLLPath);

            ((Invoke_)Marshal.GetDelegateForFunctionPointer(Runtime.GetProcAddress(DLLHandle, "Invoke"), typeof(Runtime.Invoke_)))();
        }

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void Invoke_();

        private static void ProcessExit(object sender, EventArgs e)
        {
            try
            {
                FreeLibrary(DLLHandle);
                File.Delete(DLLPath);
                /*Process.Start(new ProcessStartInfo("cmd.exe", $"/C \"del {Path.Combine(Path.GetTempPath(), $"{DLL}.dll")}")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                });*/
            }
            catch {
                File.Delete(DLLPath);
            }
        }
    }
}
