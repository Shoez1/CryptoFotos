using System;
using System.Threading;
using System.Windows.Forms;

namespace CryptoFotos
{
    static class Program
    {
        [STAThread]
        static void Main()
        {
#if !DEBUG
            Security.AntiDebug.EnforceAtStartup();
            Security.IntegrityGuard.EnforceAtStartup();

            var watchdog = new Thread(SecurityWatchdog)
            {
                IsBackground = true,
                Name = "SecurityWatchdog"
            };
            watchdog.Start();
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

            using var login = new LoginForm();
            if (login.ShowDialog() == DialogResult.OK)
            {
                Application.Run(new MainForm());
            }
        }

#if !DEBUG
        private static void SecurityWatchdog()
        {
            var random = new Random();
            int tick = 0;

            while (true)
            {
                if (Security.AntiDebug.IsBeingDebugged())
                {
                    Environment.Exit(1);
                }

                tick++;
                if (tick % 20 == 0 && Security.IntegrityGuard.IsTampered())
                {
                    Environment.Exit(1);
                }

                Thread.Sleep(500 + random.Next(300));
            }
        }
#endif
    }
}
