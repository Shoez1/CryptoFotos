using System;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace CryptoFotos
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [STAThread]
        static void Main()
        {
            // Anti-debug simples
            if (Debugger.IsAttached || IsDebuggerPresent())
            {
                MessageBox.Show("Execução não permitida em modo de depuração.", "Proteção", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
            // Anti-debug contínuo
            System.Threading.Tasks.Task.Run(() =>
            {
                while (true)
                {
                    if (Debugger.IsAttached || IsDebuggerPresent())
                    {
                        Environment.Exit(1);
                    }
                    System.Threading.Thread.Sleep(1000);
                }
            });
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show($"Erro fatal: {args.Exception.Message}\n\n{args.Exception}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            };
            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var ex = args.ExceptionObject as Exception;
                MessageBox.Show($"Erro fatal: {ex?.Message}\n\n{ex}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            };
            using (var login = new LoginForm())
            {
                if (login.ShowDialog() == DialogResult.OK)
                {
                    Application.Run(new MainForm());
                }
            }
        }
    }
}
