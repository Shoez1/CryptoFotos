using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using System.Windows.Forms;
using CryptoFotos.Utils;

namespace CryptoFotos
{
    public partial class MainForm : Form
    {
        private static readonly HashSet<string> ImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".png", ".bmp", ".gif"
        };

        private const string AppVersion = "1.5";
        private bool cancelRequested;

        public MainForm()
        {
            InitializeComponent();
            Text = $"CryptoFotos - v{AppVersion}";
            FormClosed += MainForm_FormClosed;
        }

        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            ClearGallery();
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using var folderBrowser = new FolderBrowserDialog();
            if (folderBrowser.ShowDialog() == DialogResult.OK)
            {
                txtFolderPath.Text = folderBrowser.SelectedPath;
            }
        }

        private void txtFolderPath_DragEnter(object sender, DragEventArgs e)
        {
            e.Effect = e.Data != null && e.Data.GetDataPresent(DataFormats.FileDrop)
                ? DragDropEffects.Copy
                : DragDropEffects.None;
        }

        private void txtFolderPath_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data == null || !e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                return;
            }

            if (e.Data.GetData(DataFormats.FileDrop) is string[] items && items.Length > 0 && Directory.Exists(items[0]))
            {
                txtFolderPath.Text = items[0];
            }
        }

        private void btnCancel_Click(object sender, EventArgs e)
        {
            cancelRequested = true;
            btnCancel.Enabled = false;
            lblStatus.Text = "Cancelando... Aguarde.";
        }

        private async void btnEncryptFolder_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text.Trim();
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("Selecione uma pasta válida.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? parentFolder = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                MessageBox.Show("Não foi possível identificar a pasta pai do diretório selecionado.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string folderName = Path.GetFileName(folder);
            string outputFolder = GetAvailableOutputDirectory(Path.Combine(parentFolder, $"{folderName}-cry"));
            string[] files = Directory
                .GetFiles(folder, "*.*", SearchOption.AllDirectories)
                .Where(IsSupportedImage)
                .ToArray();

            if (files.Length == 0)
            {
                MessageBox.Show("Nenhuma imagem suportada foi encontrada na pasta selecionada.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Directory.CreateDirectory(outputFolder);
            BeginProgress(files.Length, "Criptografando imagens...");

            int encryptedCount = 0;

            try
            {
                await Task.Run(() =>
                {
                    foreach (string file in files)
                    {
                        if (cancelRequested)
                        {
                            UpdateStatus("Processo cancelado pelo usuário.");
                            break;
                        }

                        byte[] imageBytes = File.ReadAllBytes(file);
                        byte[] encryptedBytes = CryptoUtils.EncryptBytes(imageBytes);
                        string relativePath = Path.GetRelativePath(folder, file);
                        string encryptedPath = Path.Combine(outputFolder, $"{relativePath}.enc");
                        string? encryptedDirectory = Path.GetDirectoryName(encryptedPath);

                        if (!string.IsNullOrWhiteSpace(encryptedDirectory))
                        {
                            Directory.CreateDirectory(encryptedDirectory);
                        }

                        File.WriteAllBytes(encryptedPath, encryptedBytes);
                        encryptedCount++;
                        UpdateProgress(encryptedCount, files.Length, "Criptografando");
                    }
                });

                if (cancelRequested)
                {
                    MessageBox.Show($"Processo cancelado. {encryptedCount} imagem(ns) foram criptografadas em:\n{outputFolder}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MessageBox.Show($"{encryptedCount} imagem(ns) foram criptografadas em:\n{outputFolder}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao criptografar as imagens: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndProgress();
            }
        }

        private async void btnDecryptFolder_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text.Trim();
            if (!Directory.Exists(folder) || !folder.EndsWith("-cry", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Selecione uma pasta criptografada válida, terminando com -cry.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string? parentFolder = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                MessageBox.Show("Não foi possível identificar a pasta pai do diretório selecionado.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string encryptedFolderName = Path.GetFileName(folder);
            string originalFolderName = encryptedFolderName.Substring(0, encryptedFolderName.Length - 4);
            string outputFolder = GetAvailableOutputDirectory(Path.Combine(parentFolder, originalFolderName));
            string[] files = Directory.GetFiles(folder, "*.enc", SearchOption.AllDirectories);

            if (files.Length == 0)
            {
                MessageBox.Show("Nenhuma imagem criptografada foi encontrada na pasta selecionada.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            Directory.CreateDirectory(outputFolder);
            BeginProgress(files.Length, "Descriptografando imagens...");

            int decryptedCount = 0;
            string? failedFile = null;
            Exception? operationError = null;

            try
            {
                await Task.Run(() =>
                {
                    foreach (string file in files)
                    {
                        if (cancelRequested)
                        {
                            UpdateStatus("Processo cancelado pelo usuário.");
                            break;
                        }

                        try
                        {
                            byte[] decryptedBytes = DecryptSelectedBytes(file);
                            string relativeEncryptedPath = Path.GetRelativePath(folder, file);
                            string relativeOriginalPath = Path.ChangeExtension(relativeEncryptedPath, null) ?? relativeEncryptedPath;
                            string outputPath = Path.Combine(outputFolder, relativeOriginalPath);
                            string? outputDirectory = Path.GetDirectoryName(outputPath);

                            if (!string.IsNullOrWhiteSpace(outputDirectory))
                            {
                                Directory.CreateDirectory(outputDirectory);
                            }

                            File.WriteAllBytes(outputPath, decryptedBytes);
                            decryptedCount++;
                            UpdateProgress(decryptedCount, files.Length, "Descriptografando");
                        }
                        catch (Exception ex)
                        {
                            failedFile = file;
                            operationError = ex;
                            UpdateStatus("Processo interrompido por erro.");
                            break;
                        }
                    }
                });

                if (cancelRequested)
                {
                    MessageBox.Show($"Processo cancelado. {decryptedCount} imagem(ns) foram restauradas em:\n{outputFolder}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (operationError != null)
                {
                    string message = operationError is CryptographicException
                        ? "Falha ao validar ou descriptografar um dos arquivos. A chave pode estar incorreta ou o conteúdo pode ter sido alterado."
                        : operationError.Message;

                    MessageBox.Show($"Erro ao descriptografar {failedFile}:\n{message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show($"{decryptedCount} imagem(ns) foram restauradas em:\n{outputFolder}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                EndProgress();
            }
        }

        private async void btnPreviewGallery_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text.Trim();
            if (!Directory.Exists(folder) || !folder.EndsWith("-cry", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Selecione uma pasta criptografada válida, terminando com -cry.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            string[] files = Directory.GetFiles(folder, "*.enc", SearchOption.AllDirectories);
            if (files.Length == 0)
            {
                MessageBox.Show("Nenhuma imagem criptografada foi encontrada na pasta selecionada.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ClearGallery();
            BeginProgress(files.Length, "Carregando galeria...");

            const int startX = 10;
            const int startY = 10;
            const int itemWidth = 60;
            const int itemHeight = 60;
            const int margin = 10;
            const int perRow = 5;

            int loadedCount = 0;
            int col = 0;
            int row = 0;
            string? failedFile = null;
            Exception? operationError = null;

            try
            {
                await Task.Run(() =>
                {
                    foreach (string file in files)
                    {
                        if (cancelRequested)
                        {
                            UpdateStatus("Processo cancelado pelo usuário.");
                            break;
                        }

                        try
                        {
                            byte[] decryptedBytes = DecryptSelectedBytes(file);
                            string relativeEncryptedPath = Path.GetRelativePath(folder, file);
                            string relativeOriginalPath = Path.ChangeExtension(relativeEncryptedPath, null) ?? relativeEncryptedPath;
                            string originalFileName = Path.GetFileName(relativeOriginalPath);
                            int currentCol = col;
                            int currentRow = row;

                            Image thumbnail = CreateImageThumbnail(decryptedBytes, itemWidth, itemHeight);
                            AddImagePreviewControl(thumbnail, file, originalFileName, startX, startY, itemWidth, itemHeight, margin, currentCol, currentRow);

                            loadedCount++;
                            col++;
                            if (col >= perRow)
                            {
                                col = 0;
                                row++;
                            }

                            UpdateProgress(loadedCount, files.Length, "Carregando");
                        }
                        catch (Exception ex)
                        {
                            failedFile = file;
                            operationError = ex;
                            UpdateStatus("Carregamento interrompido por erro.");
                            break;
                        }
                    }
                });

                if (cancelRequested)
                {
                    lblStatus.Text = "Processo cancelado pelo usuário.";
                    return;
                }

                if (operationError != null)
                {
                    string message = operationError is CryptographicException
                        ? "Falha ao validar ou descriptografar uma das imagens. A chave pode estar incorreta ou o conteúdo pode ter sido alterado."
                        : operationError.Message;

                    MessageBox.Show($"Erro ao abrir {failedFile}:\n{message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                lblStatus.Text = $"Carregado: {loadedCount}/{files.Length}";
            }
            finally
            {
                progressBar.Visible = false;
                btnCancel.Visible = false;
            }
        }

        private void AddImagePreviewControl(
            Image thumbnail,
            string encryptedFilePath,
            string originalFileName,
            int startX,
            int startY,
            int itemWidth,
            int itemHeight,
            int margin,
            int col,
            int row)
        {
            RunOnUiThread(() =>
            {
                var pictureBox = new PictureBox
                {
                    Image = thumbnail,
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Width = itemWidth,
                    Height = itemHeight,
                    Left = startX + col * (itemWidth + margin),
                    Top = startY + row * (itemHeight + margin),
                    Cursor = Cursors.Hand
                };

                pictureBox.Click += (sender, args) => ShowImagePreview(encryptedFilePath, originalFileName);
                panelGallery.Controls.Add(pictureBox);
            });
        }

        private void ShowImagePreview(string encryptedFilePath, string originalFileName)
        {
            try
            {
                byte[] imageBytes = DecryptSelectedBytes(encryptedFilePath);
                using var imageStream = new MemoryStream(imageBytes);
                using var loadedImage = Image.FromStream(imageStream);
                var displayImage = new Bitmap(loadedImage);

                using var previewForm = new Form
                {
                    Text = $"Visualização - {originalFileName}",
                    Width = 900,
                    Height = 700,
                    StartPosition = FormStartPosition.CenterParent
                };

                var pictureBox = new PictureBox
                {
                    Dock = DockStyle.Fill,
                    Image = displayImage,
                    SizeMode = PictureBoxSizeMode.Zoom
                };

                previewForm.FormClosed += (sender, args) => pictureBox.Image?.Dispose();
                previewForm.Controls.Add(pictureBox);
                previewForm.ShowDialog(this);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao abrir a imagem: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private byte[] DecryptSelectedBytes(string encryptedFilePath)
        {
            byte[] encryptedBytes = File.ReadAllBytes(encryptedFilePath);
            return CryptoUtils.DecryptBytes(encryptedBytes);
        }

        private static Image CreateImageThumbnail(byte[] imageBytes, int width, int height)
        {
            using var memoryStream = new MemoryStream(imageBytes);
            using var loadedImage = Image.FromStream(memoryStream);
            return loadedImage.GetThumbnailImage(width, height, () => false, IntPtr.Zero);
        }

        private static bool IsSupportedImage(string filePath) => ImageExtensions.Contains(Path.GetExtension(filePath));

        private static string GetAvailableOutputDirectory(string baseDirectory)
        {
            if (!Directory.Exists(baseDirectory))
            {
                return baseDirectory;
            }

            int suffix = 1;
            string candidate;
            do
            {
                candidate = $"{baseDirectory}-{suffix}";
                suffix++;
            }
            while (Directory.Exists(candidate));

            return candidate;
        }

        private void BeginProgress(int total, string initialStatus)
        {
            cancelRequested = false;
            progressBar.Minimum = 0;
            progressBar.Maximum = Math.Max(total, 1);
            progressBar.Value = 0;
            progressBar.Visible = true;
            lblStatus.Text = initialStatus;
            lblStatus.Visible = true;
            btnCancel.Visible = true;
            btnCancel.Enabled = true;
        }

        private void EndProgress()
        {
            progressBar.Visible = false;
            btnCancel.Visible = false;
            lblStatus.Visible = false;
        }

        private void UpdateProgress(int current, int total, string action)
        {
            RunOnUiThread(() =>
            {
                progressBar.Maximum = Math.Max(total, 1);
                progressBar.Value = Math.Min(current, progressBar.Maximum);
                lblStatus.Text = $"{action}: {current}/{total}";
            });
        }

        private void UpdateStatus(string status)
        {
            RunOnUiThread(() => lblStatus.Text = status);
        }

        private void RunOnUiThread(Action action)
        {
            if (IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                Invoke(action);
                return;
            }

            action();
        }

        private void ClearGallery()
        {
            while (panelGallery.Controls.Count > 0)
            {
                Control control = panelGallery.Controls[0];
                panelGallery.Controls.RemoveAt(0);

                if (control is PictureBox pictureBox)
                {
                    pictureBox.Image?.Dispose();
                }

                control.Dispose();
            }
        }
    }
}
