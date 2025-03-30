using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Threading;
using Microsoft.Win32;
using System.Windows.Forms;

namespace ModemInjector
{
    public partial class MainWindow : Window
    {

        private static string DllPath = null;

        public MainWindow()
        {
            InitializeComponent();
        }
        private void CloseWindow(object sender, MouseButtonEventArgs e)
        {
            this.Close();
        }

        private void InjectM(string status)
        {
            Dispatcher.Invoke(() => InjectButton.Content = status);
        }

        private void DLLM(string status)
        {
            ChooseDLL.Content = status;
        }

        private void DragWindow(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }

        private void ChooseDLL_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Forms.OpenFileDialog openFileDialog = new System.Windows.Forms.OpenFileDialog
            {
                Filter = "DLL Files (*.dll)|*.dll|All Files (*.*)|*.*",
                Title = "Select DLL"
            };
            if (openFileDialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                DllPath = openFileDialog.FileName;
                DLLM($"{DllPath}");
            }
        }

        private void InjectButton_Left(object sender, MouseButtonEventArgs e)
        {
            if (string.IsNullOrEmpty(DllPath))
            {
                InjectM("No DLL.");
                return;
            }
            if (!IsMinecraftRunning())
            {
                InjectM("Minecraft not open.");
                LaunchMinecraft();
                return;
            }
            Inject();
        }

        private bool IsMinecraftRunning()
        {
            return Process.GetProcessesByName($"Minecraft.Windows").Length > 0;
        }

        private void LaunchMinecraft()
        {
            string minecraftPath = @"C:\Program Files\WindowsApps\Microsoft.MinecraftUWP_1.21.7003.0_x64__8wekyb3d8bbwe\Minecraft.Windows.exe";

            try
            {
                Process.Start(minecraftPath);
                InjectM("Inject");
            }
            catch (Exception ex)
            {
                InjectM($"Error launching Minecraft: {ex.Message}");
                System.Windows.Forms.MessageBox.Show(ex.Message);
            }
        }

        [DllImport("kernel32.dll")]
        public static extern IntPtr OpenProcess(IntPtr dwDesiredAccess, bool bInheritHandle, uint processId);
        [DllImport("kernel32.dll")]
        public static extern bool CloseHandle(IntPtr hObject);
        [DllImport("kernel32.dll")]
        public static extern IntPtr VirtualAllocEx(IntPtr hProcess, IntPtr lpAddress, uint dwSize, uint flAllocationType, uint flProtect);
        [DllImport("kernel32.dll")]
        public static extern bool WriteProcessMemory(IntPtr hProcess, IntPtr lpBaseAddress, char[] lpBuffer, int nSize, out IntPtr lpNumberOfBytesWritten);
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetProcAddress(IntPtr hModule, string procName);
        [DllImport("kernel32.dll")]
        public static extern IntPtr GetModuleHandle(string lpModuleName);
        [DllImport("kernel32.dll")]
        public static extern IntPtr CreateRemoteThread(IntPtr hProcess, IntPtr lpThreadAttributes, uint dwStackSize, IntPtr lpStartAddress, IntPtr lpParameter, uint dwCreationFlags, ref IntPtr lpThreadId);
        [DllImport("kernel32.dll")]
        public static extern uint WaitForSingleObject(IntPtr handle, uint milliseconds);
        [DllImport("kernel32.dll")]
        public static extern bool VirtualFreeEx(IntPtr hProcess, IntPtr lpAddress, int dwSize, IntPtr dwFreeType);
        [DllImport("user32.dll")]
        public static extern IntPtr FindWindow(String lpClassName, String lpWindowName);
        [DllImport("user32.dll")]
        public static extern bool SetForegroundWindow(IntPtr hWnd);
        private void Inject()
        {
            try
            {
                var fileInfo = new FileInfo(DllPath);
                var accessControl = fileInfo.GetAccessControl();
                accessControl.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier("S-1-15-2-1"), FileSystemRights.FullControl, InheritanceFlags.None, PropagationFlags.NoPropagateInherit, AccessControlType.Allow));
                fileInfo.SetAccessControl(accessControl);
                var processes = Process.GetProcessesByName("Minecraft.Windows");
                if (processes.Length == 0)
                {
                    Task.Run(() =>
                    {
                        int t = 0;
                        while (processes.Length == 0)
                        {
                            if (++t > 200)
                            {
                                System.Windows.Forms.MessageBox.Show("Minecraft launch took too long.");
                                return;
                            }
                            processes = Process.GetProcessesByName("Minecraft.Windows");
                            Thread.Sleep(10);
                        }
                        Thread.Sleep(3000);
                    }).Wait();
                }
                var process = processes.First(p => p.Responding);
                for (int i = 0; i < process.Modules.Count; i++)
                {
                    if (process.Modules[i].FileName == DllPath)
                    {
                        System.Windows.Forms.MessageBox.Show("Already injected!");
                        return;
                    }
                }
                IntPtr handle = OpenProcess((IntPtr)2035711, false, (uint)process.Id);
                IntPtr p1 = VirtualAllocEx(handle, IntPtr.Zero, (uint)(DllPath.Length + 1), 12288U, 64U);
                WriteProcessMemory(handle, p1, DllPath.ToCharArray(), DllPath.Length, out IntPtr p2);
                IntPtr procAddress = GetProcAddress(GetModuleHandle("kernel32.dll"), "LoadLibraryA");
                IntPtr p3 = CreateRemoteThread(handle, IntPtr.Zero, 0U, procAddress, p1, 0U, ref p2);
                uint n = WaitForSingleObject(p3, 5000);
                Stats();
                if (n == 128L || n == 258L)
                {
                    CloseHandle(p3);
                }
                else
                {
                    VirtualFreeEx(handle, p1, 0, (IntPtr)32768);
                    if (p3 != IntPtr.Zero)
                        CloseHandle(p3);
                    if (handle != IntPtr.Zero)
                        CloseHandle(handle);
                }
                IntPtr windowH = FindWindow(null, "Minecraft");
                if (windowH == IntPtr.Zero)
                    System.Windows.Forms.MessageBox.Show("Couldn't get window handle");
                else
                    SetForegroundWindow(windowH);
            }
            catch (Exception ex)
            {
                System.Windows.Forms.MessageBox.Show($"Error: {ex.Message}");
            }
        }
        private async void Stats()
        {
            InjectButton.Content = "Attached.";
            await Task.Delay(2000);
            InjectButton.Content = "Deattached.";
            await Task.Delay(1000);
            InjectButton.Content = "Injected!";
        }
    }
}
