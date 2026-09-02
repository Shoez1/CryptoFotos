namespace CryptoFotos
{
    partial class LoginForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.Label lblUser;
        private System.Windows.Forms.Label lblPass;
        private System.Windows.Forms.TextBox txtUser;
        private System.Windows.Forms.TextBox txtPass;
        private System.Windows.Forms.CheckBox chkShowPass;
        private System.Windows.Forms.Button btnLogin;
        private System.Windows.Forms.Label lblHint;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblUser = new System.Windows.Forms.Label();
            this.lblPass = new System.Windows.Forms.Label();
            this.txtUser = new System.Windows.Forms.TextBox();
            this.txtPass = new System.Windows.Forms.TextBox();
            this.chkShowPass = new System.Windows.Forms.CheckBox();
            this.btnLogin = new System.Windows.Forms.Button();
            this.lblHint = new System.Windows.Forms.Label();
            this.SuspendLayout();
            // 
            // lblUser
            // 
            this.lblUser.AutoSize = true;
            this.lblUser.Location = new System.Drawing.Point(25, 25);
            this.lblUser.Name = "lblUser";
            this.lblUser.Size = new System.Drawing.Size(46, 13);
            this.lblUser.TabIndex = 0;
            this.lblUser.Text = "Usuário:";
            // 
            // lblPass
            // 
            this.lblPass.AutoSize = true;
            this.lblPass.Location = new System.Drawing.Point(25, 55);
            this.lblPass.Name = "lblPass";
            this.lblPass.Size = new System.Drawing.Size(41, 13);
            this.lblPass.TabIndex = 1;
            this.lblPass.Text = "Senha:";
            // 
            // txtUser
            // 
            this.txtUser.Location = new System.Drawing.Point(85, 22);
            this.txtUser.Name = "txtUser";
            this.txtUser.Size = new System.Drawing.Size(165, 20);
            this.txtUser.TabIndex = 2;
            // 
            // txtPass
            // 
            this.txtPass.Location = new System.Drawing.Point(85, 52);
            this.txtPass.Name = "txtPass";
            this.txtPass.PasswordChar = '*';
            this.txtPass.Size = new System.Drawing.Size(165, 20);
            this.txtPass.TabIndex = 3;
            // 
            // chkShowPass
            // 
            this.chkShowPass.AutoSize = true;
            this.chkShowPass.Font = new System.Drawing.Font("Segoe UI", 7.5F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point);
            this.chkShowPass.ForeColor = System.Drawing.Color.DimGray;
            this.chkShowPass.Location = new System.Drawing.Point(85, 78);
            this.chkShowPass.Name = "chkShowPass";
            this.chkShowPass.Size = new System.Drawing.Size(94, 16);
            this.chkShowPass.TabIndex = 4;
            this.chkShowPass.Text = "Mostrar senha";
            this.chkShowPass.UseVisualStyleBackColor = true;
            this.chkShowPass.CheckedChanged += new System.EventHandler(this.chkShowPass_CheckedChanged);
            // 
            // btnLogin
            // 
            this.btnLogin.Location = new System.Drawing.Point(85, 102);
            this.btnLogin.Name = "btnLogin";
            this.btnLogin.Size = new System.Drawing.Size(165, 30);
            this.btnLogin.TabIndex = 5;
            this.btnLogin.Text = "Entrar";
            this.btnLogin.UseVisualStyleBackColor = true;
            this.btnLogin.Click += new System.EventHandler(this.btnLogin_Click);
            // 
            // lblHint
            // 
            this.lblHint.AutoSize = false;
            this.lblHint.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblHint.Location = new System.Drawing.Point(15, 140);
            this.lblHint.Name = "lblHint";
            this.lblHint.Size = new System.Drawing.Size(250, 42);
            this.lblHint.TabIndex = 6;
            this.lblHint.ForeColor = System.Drawing.Color.Gray;
            // 
            // LoginForm
            // 
            this.AcceptButton = this.btnLogin;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(280, 190);
            this.Controls.Add(this.chkShowPass);
            this.Controls.Add(this.btnLogin);
            this.Controls.Add(this.txtPass);
            this.Controls.Add(this.txtUser);
            this.Controls.Add(this.lblPass);
            this.Controls.Add(this.lblUser);
            this.Controls.Add(this.lblHint);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "LoginForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Login - CryptoFotos";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
