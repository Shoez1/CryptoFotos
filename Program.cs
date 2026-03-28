using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace CryptoFotos
{
    static class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool IsDebuggerPresent();

        [STAThread]
        static void Main()
        {
#if !DEBUG
            if (Debugger.IsAttached || IsDebuggerPresent())
            {
                MessageBox.Show("Execução não permitida em modo de depuração.", "Proteção", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }

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
#endif

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            Application.ThreadException += (sender, args) =>
            {
                MessageBox.Show($"Erro fatal: {args.Exception.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                var exception = args.ExceptionObject as Exception;
                MessageBox.Show($"Erro fatal: {exception?.Message ?? "Erro inesperado."}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            };

#if DEBUG
            Application.Run(new MainForm());
            return;
#endif

            using var login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainForm());
            }
        }
    }
}
