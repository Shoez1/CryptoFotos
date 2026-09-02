using System;
using System.Windows.Forms;
using CryptoFotos.Utils;

namespace CryptoFotos
{
    public partial class LoginForm : Form
    {
        private readonly LoginConfiguration loginConfiguration;
        private readonly string defaultHintText;
        private readonly bool defaultHintVisible;
        private int failedAttempts;
        private DateTime lockoutUntilUtc = DateTime.MinValue;

        public LoginForm()
        {
            InitializeComponent();
            Text = $"Login - CryptoFotos v{AppInfo.Version}";

            try
            {
                loginConfiguration = LoginConfigurationLoader.LoadFromEmbeddedResource();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Configuração de login inválida: {ex.Message}", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                throw;
            }

            ApplyLoginConfiguration();
            defaultHintText = lblHint.Text;
            defaultHintVisible = lblHint.Visible;
        }

        private void chkShowPass_CheckedChanged(object? sender, EventArgs e)
        {
            txtPass.PasswordChar = chkShowPass.Checked ? '\0' : '*';
        }

        private void ApplyLoginConfiguration()
        {
            if (!string.IsNullOrEmpty(loginConfiguration.Hint))
            {
                lblHint.Text = $"Dica de senha: {loginConfiguration.Hint}";
                lblHint.Visible = true;
                return;
            }

            lblHint.Visible = false;
        }

        private async void ApplyLockoutAsync()
        {
            btnLogin.Enabled = false;
            txtUser.Enabled = false;
            txtPass.Enabled = false;
            chkShowPass.Enabled = false;

            while (DateTime.UtcNow < lockoutUntilUtc)
            {
                TimeSpan remaining = lockoutUntilUtc - DateTime.UtcNow;
                lblHint.Text = $"Muitas tentativas. Aguarde {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))}s.";
                lblHint.Visible = true;
                await System.Threading.Tasks.Task.Delay(1000);
            }

            btnLogin.Enabled = true;
            txtUser.Enabled = true;
            txtPass.Enabled = true;
            chkShowPass.Enabled = true;
            lblHint.Text = defaultHintText;
            lblHint.Visible = defaultHintVisible;
            txtPass.Focus();
        }

        private void RegisterFailure(string message)
        {
            failedAttempts++;
            txtPass.Text = string.Empty;
            txtUser.Focus();

            if (failedAttempts >= 5)
            {
                failedAttempts = 0;
                lockoutUntilUtc = DateTime.UtcNow.AddSeconds(15);
                MessageBox.Show($"{message}\n\nO login foi bloqueado por 15 segundos.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                ApplyLockoutAsync();
                return;
            }

            MessageBox.Show(message, "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void btnLogin_Click(object? sender, EventArgs e)
        {
            if (DateTime.UtcNow < lockoutUntilUtc)
            {
                TimeSpan remaining = lockoutUntilUtc - DateTime.UtcNow;
                MessageBox.Show(
                    $"Muitas tentativas de login. Aguarde {Math.Max(1, (int)Math.Ceiling(remaining.TotalSeconds))} segundos.",
                    "Proteção",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                return;
            }

            string user = txtUser.Text.Trim();
            string pass = txtPass.Text;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(pass))
            {
                MessageBox.Show("Informe o usuário e a senha.", "Atenção", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (loginConfiguration.ValidateCredentials(user, pass))
            {
                failedAttempts = 0;
                txtPass.Text = string.Empty;
                DialogResult = DialogResult.OK;
                Close();
                return;
            }

            RegisterFailure("Usuário ou senha inválidos!");
        }
    }
}
