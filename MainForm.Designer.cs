namespace CryptoFotos
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;
        private System.Windows.Forms.TextBox txtFolderPath;
        private System.Windows.Forms.Button btnSelectFolder;
        private System.Windows.Forms.Button btnEncryptFolder;
        private System.Windows.Forms.Button btnDecryptFolder;
        private System.Windows.Forms.Button btnPreviewGallery;
        private System.Windows.Forms.Panel panelGallery;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label lblStatus;
        private System.Windows.Forms.Button btnCancel;
        private System.Windows.Forms.Button btnExportKey;
        private System.Windows.Forms.Button btnImportKey;
        private System.Windows.Forms.Button btnGenerateKey;
        private System.Windows.Forms.Label lblSignature;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.txtFolderPath = new System.Windows.Forms.TextBox();
            this.btnSelectFolder = new System.Windows.Forms.Button();
            this.btnEncryptFolder = new System.Windows.Forms.Button();
            this.btnDecryptFolder = new System.Windows.Forms.Button();
            this.btnPreviewGallery = new System.Windows.Forms.Button();
            this.panelGallery = new System.Windows.Forms.Panel();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.lblStatus = new System.Windows.Forms.Label();
            this.btnCancel = new System.Windows.Forms.Button();
            this.btnExportKey = new System.Windows.Forms.Button();
            this.btnImportKey = new System.Windows.Forms.Button();
            this.btnGenerateKey = new System.Windows.Forms.Button();
            this.lblSignature = new System.Windows.Forms.Label();
            this.txtFolderPath.AllowDrop = true;
            this.txtFolderPath.DragEnter += new System.Windows.Forms.DragEventHandler(this.txtFolderPath_DragEnter);
            this.txtFolderPath.DragDrop += new System.Windows.Forms.DragEventHandler(this.txtFolderPath_DragDrop);
            this.SuspendLayout();
            // 
            // txtFolderPath
            // 
            this.txtFolderPath.Location = new System.Drawing.Point(20, 20);
            this.txtFolderPath.Name = "txtFolderPath";
            this.txtFolderPath.Size = new System.Drawing.Size(250, 20);
            this.txtFolderPath.TabIndex = 0;
            // 
            // btnSelectFolder
            // 
            this.btnSelectFolder.Location = new System.Drawing.Point(280, 18);
            this.btnSelectFolder.Name = "btnSelectFolder";
            this.btnSelectFolder.Size = new System.Drawing.Size(90, 23);
            this.btnSelectFolder.TabIndex = 1;
            this.btnSelectFolder.Text = "Selecionar Pasta...";
            this.btnSelectFolder.UseVisualStyleBackColor = true;
            this.btnSelectFolder.Click += new System.EventHandler(this.btnSelectFolder_Click);
            // 
            // btnEncryptFolder
            // 
            this.btnEncryptFolder.Location = new System.Drawing.Point(20, 60);
            this.btnEncryptFolder.Name = "btnEncryptFolder";
            this.btnEncryptFolder.Size = new System.Drawing.Size(170, 30);
            this.btnEncryptFolder.TabIndex = 2;
            this.btnEncryptFolder.Text = "Criptografar Pasta";
            this.btnEncryptFolder.UseVisualStyleBackColor = true;
            this.btnEncryptFolder.Click += new System.EventHandler(this.btnEncryptFolder_Click);
            // 
            // btnDecryptFolder
            // 
            this.btnDecryptFolder.Location = new System.Drawing.Point(200, 60);
            this.btnDecryptFolder.Name = "btnDecryptFolder";
            this.btnDecryptFolder.Size = new System.Drawing.Size(170, 30);
            this.btnDecryptFolder.TabIndex = 3;
            this.btnDecryptFolder.Text = "Descriptografar Pasta";
            this.btnDecryptFolder.UseVisualStyleBackColor = true;
            this.btnDecryptFolder.Click += new System.EventHandler(this.btnDecryptFolder_Click);
            // 
            // btnPreviewGallery
            // 
            this.btnPreviewGallery.Location = new System.Drawing.Point(20, 100);
            this.btnPreviewGallery.Name = "btnPreviewGallery";
            this.btnPreviewGallery.Size = new System.Drawing.Size(350, 30);
            this.btnPreviewGallery.TabIndex = 4;
            this.btnPreviewGallery.Text = "Visualizar Galeria";
            this.btnPreviewGallery.UseVisualStyleBackColor = true;
            this.btnPreviewGallery.Click += new System.EventHandler(this.btnPreviewGallery_Click);
            // 
            // panelGallery
            // 
            this.panelGallery.AutoScroll = true;
            this.panelGallery.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelGallery.Location = new System.Drawing.Point(20, 140);
            this.panelGallery.Name = "panelGallery";
            this.panelGallery.Size = new System.Drawing.Size(350, 180);
            this.panelGallery.TabIndex = 5;
            // 
            // progressBar
            // 
            this.progressBar.Location = new System.Drawing.Point(20, 330);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(250, 20);
            this.progressBar.TabIndex = 6;
            this.progressBar.Visible = false;
            // 
            // lblStatus
            // 
            this.lblStatus.Location = new System.Drawing.Point(20, 310);
            this.lblStatus.Name = "lblStatus";
            this.lblStatus.Size = new System.Drawing.Size(350, 17);
            this.lblStatus.TabIndex = 7;
            this.lblStatus.Text = "";
            this.lblStatus.Visible = false;
            // 
            // btnCancel
            // 
            this.btnCancel.Location = new System.Drawing.Point(280, 330);
            this.btnCancel.Name = "btnCancel";
            this.btnCancel.Size = new System.Drawing.Size(90, 23);
            this.btnCancel.TabIndex = 8;
            this.btnCancel.Text = "Cancelar";
            this.btnCancel.UseVisualStyleBackColor = true;
            this.btnCancel.Visible = false;
            this.btnCancel.Click += new System.EventHandler(this.btnCancel_Click);
            //
            // btnExportKey
            //
            this.btnExportKey.Location = new System.Drawing.Point(20, 330);
            this.btnExportKey.Name = "btnExportKey";
            this.btnExportKey.Size = new System.Drawing.Size(125, 23);
            this.btnExportKey.TabIndex = 9;
            this.btnExportKey.Text = "Exportar Chave";
            this.btnExportKey.UseVisualStyleBackColor = true;
            this.btnExportKey.Click += new System.EventHandler(this.btnExportKey_Click);
            //
            // btnImportKey
            //
            this.btnImportKey.BackColor = System.Drawing.Color.LightGray;
            this.btnImportKey.Location = new System.Drawing.Point(155, 330);
            this.btnImportKey.Name = "btnImportKey";
            this.btnImportKey.Size = new System.Drawing.Size(115, 23);
            this.btnImportKey.TabIndex = 10;
            this.btnImportKey.Text = "Importar Chave";
            this.btnImportKey.UseVisualStyleBackColor = true;
            this.btnImportKey.Click += new System.EventHandler(this.btnImportKey_Click);
            //
            // btnGenerateKey
            //
            this.btnGenerateKey.Location = new System.Drawing.Point(280, 330);
            this.btnGenerateKey.Name = "btnGenerateKey";
            this.btnGenerateKey.Size = new System.Drawing.Size(90, 23);
            this.btnGenerateKey.TabIndex = 11;
            this.btnGenerateKey.Text = "Gerar Chave";
            this.btnGenerateKey.UseVisualStyleBackColor = true;
            this.btnGenerateKey.Click += new System.EventHandler(this.btnGenerateKey_Click);
            //
            // lblSignature
            //
            this.lblSignature.Font = new System.Drawing.Font("Segoe UI", 8F, System.Drawing.FontStyle.Italic, System.Drawing.GraphicsUnit.Point);
            this.lblSignature.ForeColor = System.Drawing.Color.DimGray;
            this.lblSignature.Location = new System.Drawing.Point(20, 360);
            this.lblSignature.Name = "lblSignature";
            this.lblSignature.Size = new System.Drawing.Size(350, 14);
            this.lblSignature.TabIndex = 12;
            this.lblSignature.Text = "MTSproductions@2026";
            this.lblSignature.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            // 
            // MainForm
            // 
            this.AllowDrop = true;
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(390, 380);
            this.Controls.Add(this.lblSignature);
            this.Controls.Add(this.btnGenerateKey);
            this.Controls.Add(this.btnImportKey);
            this.Controls.Add(this.btnExportKey);
            this.Controls.Add(this.btnCancel);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.lblStatus);
            this.Controls.Add(this.panelGallery);
            this.Controls.Add(this.btnPreviewGallery);
            this.Controls.Add(this.btnDecryptFolder);
            this.Controls.Add(this.btnEncryptFolder);
            this.Controls.Add(this.btnSelectFolder);
            this.Controls.Add(this.txtFolderPath);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.Name = "MainForm";
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "CryptoFotos - Criptografar Fotos";
            this.ResumeLayout(false);
            this.PerformLayout();
        }
    }
}
