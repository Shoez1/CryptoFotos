using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;

namespace CryptoFotos
{
    public partial class LoginForm : Form
    {
        private string validUser = null;
        private string validPass = null;
        private string senhaHint = null;
        private bool senhaPadraoDinamica = false;

        public LoginForm()
        {
            InitializeComponent();
            this.Text = "Login - CryptoFotos v1.0.2";
            LoadLoginInfo();
            if (senhaPadraoDinamica)
            {
                lblHint.Text = "Dica de senha: +d-h";
                lblHint.Visible = true;
                lblUser.Visible = false;
                txtUser.Visible = false;
                lblPass.Location = new System.Drawing.Point(lblPass.Location.X, lblUser.Location.Y);
                txtPass.Location = new System.Drawing.Point(txtPass.Location.X, txtUser.Location.Y);
            }
            else if (!string.IsNullOrEmpty(senhaHint))
            {
                lblHint.Text = $"Dica de senha: {senhaHint}";
                lblHint.Visible = true;
            }
            else
            {
                lblHint.Visible = false;
            }
        }

        private void LoadLoginInfo()
        {
            // Lê login.txt como recurso embutido de forma robusta
            var assembly = Assembly.GetExecutingAssembly();
            string resourceName = null;
            foreach (var res in assembly.GetManifestResourceNames())
            {
                if (res.EndsWith("login.txt", StringComparison.OrdinalIgnoreCase))
                {
                    resourceName = res;
                    break;
                }
            }
            if (resourceName == null)
            {
                MessageBox.Show("Erro: login.txt não encontrado como recurso embutido.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
                return;
            }
            using (var stream = assembly.GetManifestResourceStream(resourceName))
            using (var reader = new StreamReader(stream, Encoding.UTF8))
            {
                string line;
                bool foundLogin = false;
                bool checkedSenhaPadrao = false;
                while ((line = reader.ReadLine()) != null)
                {
                    if (!checkedSenhaPadrao && line.StartsWith("senhapadrao:", StringComparison.OrdinalIgnoreCase))
                    {
                        senhaPadraoDinamica = line.Trim().ToLower().EndsWith(":sim");
                        checkedSenhaPadrao = true;
                        continue;
                    }
                    if (!foundLogin && !string.IsNullOrEmpty(line) && line.Contains(":"))
                {
                    var parts = line.Split(':');
                        if (parts.Length == 2 && !string.IsNullOrWhiteSpace(parts[0]) && !string.IsNullOrWhiteSpace(parts[1]))
                        {
                    validUser = parts[0];
                    validPass = parts[1];
                            foundLogin = true;
                            continue;
                        }
                    }
                    if (line.StartsWith("dicadesenha:", StringComparison.OrdinalIgnoreCase))
                    {
                        senhaHint = line.Substring("dicadesenha:".Length).Trim();
                    }
                }
                if (!foundLogin)
                {
                    MessageBox.Show("Erro: login.txt embutido está vazio ou mal formatado.", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    Application.Exit();
                }
            }
        }

        private void btnLogin_Click(object sender, EventArgs e)
        {
            if (senhaPadraoDinamica)
            {
                // Senha dinâmica: (dia+1)(hora-1)
                var now = DateTime.Now;
                string senhaDinamica = $"{now.Day + 1}{now.Hour - 1}";
                if (txtPass.Text == senhaDinamica || txtUser.Text == senhaDinamica)
                {
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                    return;
                }
                else
                {
                    MessageBox.Show("Senha dinâmica inválida!", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    txtPass.Text = "";
                    txtUser.Focus();
                    return;
                }
            }
            if (txtUser.Text == validUser && txtPass.Text == validPass)
            {
                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Usuário ou senha inválidos!", "Erro", MessageBoxButtons.OK, MessageBoxIcon.Error);
                txtPass.Text = "";
                txtUser.Focus();
            }
        }
    }
}
