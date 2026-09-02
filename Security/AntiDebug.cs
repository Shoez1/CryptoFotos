using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace CryptoFotos.Security
{
    internal static class AntiDebug
    {
        private const string DeniedMessage = "Execução não permitida em modo de depuração.";

        private const uint ThreadGetContext = 0x0008;

        private const int ProcessDebugPort = 7;
        private const int ProcessDebugObjectHandle = 30;
        private const int ProcessDebugFlags = 31;

        private const uint FlgHeapEnableTailCheck = 0x10;
        private const uint FlgHeapEnableFreeCheck = 0x20;
        private const uint FlgHeapValidateParameters = 0x40;
        private const uint DebugHeapMask = FlgHeapEnableTailCheck | FlgHeapEnableFreeCheck | FlgHeapValidateParameters;

        private static readonly string[] DebuggerProcessNames =
        {
            "x64dbg", "x32dbg", "x64_dbg", "x96dbg",
            "ollydbg", "ollydbg2", "ollyice",
            "ida", "ida32", "ida64", "idaq", "idaq64",
            "windbg", "kd", "cdb",
            "dnspy", "cheatengine", "cheat_engine", "xdbg"
        };

        private static readonly string[] DebuggerWindowTokens =
        {
            "x64dbg", "x32dbg", "x96dbg", "ollydbg", "ida - ", "ida: ",
            "windbg", "dnspy", "cheat engine", "x64_dbg"
        };

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetCurrentProcess();

        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [DllImport("kernel32.dll")]
        private static extern bool CheckRemoteDebuggerPresent(IntPtr hProcess, ref bool pbDebuggerPresent);

        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentThreadId();

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenThread(uint dwDesiredAccess, bool bInheritHandle, uint dwThreadId);

        [DllImport("kernel32.dll")]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("kernel32.dll")]
        private static extern bool GetThreadContext(IntPtr hThread, IntPtr lpContext);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr hProcess, int processInformationClass, ref IntPtr processInformation, int processInformationLength, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern int NtQueryInformationProcess(IntPtr hProcess, int processInformationClass, ref int processInformation, int processInformationLength, out int returnLength);

        [DllImport("ntdll.dll")]
        private static extern IntPtr NtCurrentPeb();

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern bool IsWindowVisible(IntPtr hWnd);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        public static void EnforceAtStartup()
        {
            if (IsBeingDebugged())
            {
                Terminate();
            }
        }

        public static bool IsBeingDebugged()
        {
            try
            {
                if (Debugger.IsAttached)
                {
                    return true;
                }

                if (IsDebuggerPresent())
                {
                    return true;
                }

                if (IsRemoteDebuggerPresent())
                {
                    return true;
                }

                if (HasDebugPort())
                {
                    return true;
                }

                if (HasDebugObjectHandle())
                {
                    return true;
                }

                if (DebugFlagsCleared())
                {
                    return true;
                }

                if (PebBeingDebuggedSet())
                {
                    return true;
                }

                if (PebHeapFlagsSet())
                {
                    return true;
                }

                if (HardwareBreakpointsSet())
                {
                    return true;
                }

                if (DebuggerProcessRunning())
                {
                    return true;
                }

                if (DebuggerWindowPresent())
                {
                    return true;
                }
            }
            catch
            {
                return false;
            }

            return false;
        }

        public static void Terminate()
        {
            MessageBox.Show(DeniedMessage, "Proteção", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Environment.Exit(1);
        }

        private static bool IsRemoteDebuggerPresent()
        {
            bool present = false;
            return CheckRemoteDebuggerPresent(GetCurrentProcess(), ref present) && present;
        }

        private static bool HasDebugPort()
        {
            IntPtr debugPort = IntPtr.Zero;
            int returnLength;
            int status = NtQueryInformationProcess(
                GetCurrentProcess(),
                ProcessDebugPort,
                ref debugPort,
                IntPtr.Size,
                out returnLength);
            return status == 0 && debugPort != IntPtr.Zero;
        }

        private static bool HasDebugObjectHandle()
        {
            int debugHandle = 0;
            int returnLength;
            int status = NtQueryInformationProcess(
                GetCurrentProcess(),
                ProcessDebugObjectHandle,
                ref debugHandle,
                sizeof(int),
                out returnLength);
            return status == 0 && debugHandle != 0;
        }

        private static bool DebugFlagsCleared()
        {
            int debugFlags = -1;
            int returnLength;
            int status = NtQueryInformationProcess(
                GetCurrentProcess(),
                ProcessDebugFlags,
                ref debugFlags,
                sizeof(int),
                out returnLength);
            return status == 0 && debugFlags == 0;
        }

        private static bool PebBeingDebuggedSet()
        {
            IntPtr peb = NtCurrentPeb();
            return peb != IntPtr.Zero && Marshal.ReadByte(peb, 0x02) != 0;
        }

        private static bool PebHeapFlagsSet()
        {
            IntPtr peb = NtCurrentPeb();
            if (peb == IntPtr.Zero)
            {
                return false;
            }

            int offset = IntPtr.Size == 8 ? 0xBC : 0x68;
            uint globalFlag = unchecked((uint)Marshal.ReadInt32(peb, offset));
            return (globalFlag & DebugHeapMask) == DebugHeapMask;
        }

        private static bool HardwareBreakpointsSet()
        {
            IntPtr threadHandle = IntPtr.Zero;
            IntPtr rawMemory = IntPtr.Zero;

            try
            {
                threadHandle = OpenThread(ThreadGetContext, false, GetCurrentThreadId());
                if (threadHandle == IntPtr.Zero)
                {
                    return false;
                }

                int contextSize = IntPtr.Size == 8 ? 0x4D0 : 0x2CC;
                // Win64 GetThreadContext requires 16-byte alignment
                rawMemory = Marshal.AllocHGlobal(contextSize + 15);
                long aligned = (rawMemory.ToInt64() + 15) & ~15L;
                IntPtr context = new IntPtr(aligned);

                // Zero out the context memory
                for (int i = 0; i < contextSize; i += IntPtr.Size)
                {
                    Marshal.WriteIntPtr(context, i, IntPtr.Zero);
                }

                if (IntPtr.Size == 8)
                {
                    // CONTEXT_AMD64 (0x00100000) | CONTEXT_DEBUG_REGISTERS (0x00000010)
                    Marshal.WriteInt32(context, 0x30, 0x00100010);
                }
                else
                {
                    // CONTEXT_i386 (0x00010000) | CONTEXT_DEBUG_REGISTERS (0x00000010)
                    Marshal.WriteInt32(context, 0x00, 0x00010010);
                }

                if (!GetThreadContext(threadHandle, context))
                {
                    return false;
                }

                if (IntPtr.Size == 8)
                {
                    ulong dr0 = unchecked((ulong)Marshal.ReadInt64(context, 0x48));
                    ulong dr1 = unchecked((ulong)Marshal.ReadInt64(context, 0x50));
                    ulong dr2 = unchecked((ulong)Marshal.ReadInt64(context, 0x58));
                    ulong dr3 = unchecked((ulong)Marshal.ReadInt64(context, 0x60));
                    return dr0 != 0 || dr1 != 0 || dr2 != 0 || dr3 != 0;
                }
                else
                {
                    uint dr0 = unchecked((uint)Marshal.ReadInt32(context, 0x04));
                    uint dr1 = unchecked((uint)Marshal.ReadInt32(context, 0x08));
                    uint dr2 = unchecked((uint)Marshal.ReadInt32(context, 0x0C));
                    uint dr3 = unchecked((uint)Marshal.ReadInt32(context, 0x10));
                    return dr0 != 0 || dr1 != 0 || dr2 != 0 || dr3 != 0;
                }
            }
            finally
            {
                if (rawMemory != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(rawMemory);
                }

                if (threadHandle != IntPtr.Zero)
                {
                    CloseHandle(threadHandle);
                }
            }
        }

        private static bool DebuggerProcessRunning()
        {
            bool found = false;
            Process[] processes = Process.GetProcesses();

            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        string name = process.ProcessName;
                        foreach (string debuggerName in DebuggerProcessNames)
                        {
                            if (string.Equals(debuggerName, name, StringComparison.OrdinalIgnoreCase))
                            {
                                found = true;
                                break;
                            }
                        }
                    }
                    catch
                    {
                        // processo sem acesso ao nome (morrendo/privilegiado)
                    }

                    if (found)
                    {
                        break;
                    }
                }
            }
            finally
            {
                foreach (Process process in processes)
                {
                    process.Dispose();
                }
            }

            return found;
        }

        private static bool DebuggerWindowPresent()
        {
            bool found = false;

            EnumWindows((hWnd, lParam) =>
            {
                if (!IsWindowVisible(hWnd))
                {
                    return true;
                }

                var title = new StringBuilder(256);
                if (GetWindowText(hWnd, title, title.Capacity) == 0)
                {
                    return true;
                }

                string text = title.ToString();
                foreach (string token in DebuggerWindowTokens)
                {
                    if (text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        found = true;
                        return false;
                    }
                }

                return true;
            }, IntPtr.Zero);

            return found;
        }
    }
}
