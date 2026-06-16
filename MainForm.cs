using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using CryptoFotos.Utils;

namespace CryptoFotos
{
    public partial class MainForm : Form
    {
        private static readonly HashSet<string> PhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jpe", ".jfif", ".pjpeg", ".pjp",
            ".png", ".apng",
            ".bmp", ".dib",
            ".gif",
            ".tif", ".tiff",
            ".webp",
            ".heic", ".heif", ".heics", ".heifs", ".hif",
            ".avif", ".avifs",
            ".jp2", ".j2k", ".jpf", ".jpx", ".jpm", ".mj2",
            ".jxl",
            ".dng", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2",
            ".orf", ".rw2", ".raf", ".pef", ".srw", ".x3f", ".erf", ".kdc",
            ".dcr", ".mos", ".mrw", ".mef", ".iiq", ".3fr", ".fff", ".rwl",
            ".psd", ".psb"
        };

        private static readonly HashSet<string> DecodableImageExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".jpg", ".jpeg", ".jpe", ".jfif", ".pjpeg", ".pjp",
            ".png", ".apng",
            ".bmp", ".dib",
            ".gif",
            ".tif", ".tiff"
        };

        private static readonly HashSet<string> CameraRawPhotoExtensions = new(StringComparer.OrdinalIgnoreCase)
        {
            ".dng", ".cr2", ".cr3", ".nef", ".nrw", ".arw", ".srf", ".sr2",
            ".orf", ".rw2", ".raf", ".pef", ".srw", ".x3f", ".erf", ".kdc",
            ".dcr", ".mos", ".mrw", ".mef", ".iiq", ".3fr", ".fff", ".rwl"
        };

        private static readonly string[] IsoBaseMediaPhotoBrands =
        {
            "heic", "heix", "hevc", "hevx", "heim", "heis", "hevm", "hevs",
            "mif1", "msf1", "avif", "avis", "crx "
        };

        private static readonly EnumerationOptions SafeRecursiveEnumerationOptions = new()
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        private const string AppVersion = "1.5";
        private const int MaxFilesPerOperation = 5000;
        private const long MaxPlainImageSizeBytes = 100L * 1024L * 1024L;
        private const long MaxEncryptedFileSizeBytes = 140L * 1024L * 1024L;
        private const int MaxImageDimension = 20000;
        private const long MaxImagePixels = 80000000L;
        private byte[]? activeKey;
        private byte[]? activeIV;
        private bool importedKeyActive;
        private volatile bool cancelRequested;

        public MainForm()
        {
            InitializeComponent();
            LoadLocalKeyMaterial();
            Text = $"CryptoFotos - v{AppVersion}";
            FormClosed += MainForm_FormClosed;
        }

        private void MainForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            ClearGallery();
            ClearActiveKey();
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

            if (e.Data.GetData(DataFormats.FileDrop) is string[] items &&
                items.Length > 0 &&
                IsSafeDirectory(items[0]))
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
            if (!IsSafeDirectory(folder))
            {
                MessageBox.Show("Selecione uma pasta válida e sem redirecionamentos.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            folder = GetCanonicalDirectoryPath(folder);
            string? parentFolder = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                MessageBox.Show("Não foi possível identificar a pasta pai do diretório selecionado.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string folderName = Path.GetFileName(folder);
            string[] files;
            try
            {
                files = GetSafeFiles(folder, "*.*", IsSupportedPhotoPath, MaxPlainImageSizeBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível listar os arquivos com segurança: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (files.Length == 0)
            {
                MessageBox.Show("Nenhuma foto suportada foi encontrada na pasta selecionada.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outputFolder;
            try
            {
                outputFolder = CreateAvailableOutputDirectory(Path.Combine(parentFolder, $"{folderName}-cry"));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível criar a pasta de saída com segurança: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            BeginProgress(files.Length, "Criptografando fotos...");
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

                        byte[] photoBytes = File.ReadAllBytes(file);
                        ValidatePhotoBytes(file, photoBytes);
                        byte[] encryptedBytes = EncryptSelectedBytes(photoBytes);
                        string relativePath = Path.GetRelativePath(folder, file);
                        string encryptedPath = CombineInsideRoot(outputFolder, $"{relativePath}.enc");
                        string? encryptedDirectory = Path.GetDirectoryName(encryptedPath);

                        if (!string.IsNullOrWhiteSpace(encryptedDirectory))
                        {
                            Directory.CreateDirectory(encryptedDirectory);
                            EnsureSafeDirectory(encryptedDirectory);
                        }

                        WriteAllBytesCreateNew(encryptedPath, encryptedBytes);
                        encryptedCount++;
                        UpdateProgress(encryptedCount, files.Length, "Criptografando");
                    }
                });

                MessageBox.Show(
                    cancelRequested
                        ? $"Processo cancelado. {encryptedCount} foto(s) foram criptografadas em:\n{outputFolder}"
                        : $"{encryptedCount} foto(s) foram criptografadas em:\n{outputFolder}",
                    "CryptoFotos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao criptografar as fotos: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                EndProgress();
            }
        }

        private async void btnDecryptFolder_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text.Trim();
            if (!IsSafeDirectory(folder) || !folder.EndsWith("-cry", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Selecione uma pasta criptografada válida, terminando com -cry e sem redirecionamentos.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            folder = GetCanonicalDirectoryPath(folder);
            string? parentFolder = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parentFolder))
            {
                MessageBox.Show("Não foi possível identificar a pasta pai do diretório selecionado.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            string encryptedFolderName = Path.GetFileName(folder);
            string originalFolderName = encryptedFolderName.Substring(0, encryptedFolderName.Length - 4);
            string[] files;
            try
            {
                files = GetSafeFiles(folder, "*.enc", _ => true, MaxEncryptedFileSizeBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível listar os arquivos com segurança: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (files.Length == 0)
            {
                MessageBox.Show("Nenhuma foto criptografada foi encontrada na pasta selecionada.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string outputFolder;
            try
            {
                outputFolder = CreateAvailableOutputDirectory(Path.Combine(parentFolder, originalFolderName));
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível criar a pasta de saída com segurança: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            BeginProgress(files.Length, "Descriptografando fotos...");
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
                            string relativeOriginalPath = ResolveOriginalRelativePath(folder, file);
                            ValidatePhotoBytes(relativeOriginalPath, decryptedBytes);
                            string outputPath = CombineInsideRoot(outputFolder, relativeOriginalPath);
                            string? outputDirectory = Path.GetDirectoryName(outputPath);

                            if (!string.IsNullOrWhiteSpace(outputDirectory))
                            {
                                Directory.CreateDirectory(outputDirectory);
                                EnsureSafeDirectory(outputDirectory);
                            }

                            WriteAllBytesCreateNew(outputPath, decryptedBytes);
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
                    MessageBox.Show($"Processo cancelado. {decryptedCount} foto(s) foram restauradas em:\n{outputFolder}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (operationError != null)
                {
                    string message = operationError is CryptographicException
                        ? "Falha ao validar ou descriptografar um dos arquivos. A senha pode estar incorreta ou o conteúdo pode ter sido alterado."
                        : operationError.Message;

                    MessageBox.Show($"Erro ao descriptografar {Path.GetFileName(failedFile)}:\n{message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show($"{decryptedCount} foto(s) foram restauradas em:\n{outputFolder}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            finally
            {
                EndProgress();
            }
        }

        private async void btnPreviewGallery_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text.Trim();
            if (!IsSafeDirectory(folder) || !folder.EndsWith("-cry", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Selecione uma pasta criptografada válida, terminando com -cry e sem redirecionamentos.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            folder = GetCanonicalDirectoryPath(folder);
            string[] files;
            try
            {
                files = GetSafeFiles(folder, "*.enc", _ => true, MaxEncryptedFileSizeBytes);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Não foi possível listar os arquivos com segurança: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            if (files.Length == 0)
            {
                MessageBox.Show("Nenhuma foto criptografada foi encontrada na pasta selecionada.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
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
                            string relativeOriginalPath = ResolveOriginalRelativePath(folder, file);
                            ValidatePhotoBytes(relativeOriginalPath, decryptedBytes);
                            string originalFileName = Path.GetFileName(relativeOriginalPath);
                            int currentCol = col;
                            int currentRow = row;

                            Image thumbnail = CreateGalleryThumbnail(decryptedBytes, originalFileName, itemWidth, itemHeight);
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
                        ? "Falha ao validar ou descriptografar uma das fotos. A senha pode estar incorreta ou o conteúdo pode ter sido alterado."
                        : operationError.Message;

                    MessageBox.Show($"Erro ao abrir {Path.GetFileName(failedFile)}:\n{message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                lblStatus.Text = $"Carregado: {loadedCount}/{files.Length}";
            }
            finally
            {
                progressBar.Visible = false;
                btnCancel.Visible = false;
                btnExportKey.Visible = true;
                btnImportKey.Visible = true;
                btnGenerateKey.Visible = true;
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
                ValidatePhotoBytes(originalFileName, imageBytes);
                Bitmap displayImage;

                try
                {
                    using var imageStream = new MemoryStream(imageBytes);
                    using var loadedImage = Image.FromStream(imageStream, false, true);
                    ValidateImageDimensions(loadedImage);
                    displayImage = new Bitmap(loadedImage);
                }
                catch (Exception ex) when (IsUnsupportedImageDecodeException(ex))
                {
                    MessageBox.Show(
                        $"A foto {originalFileName} foi reconhecida, mas este formato não pode ser visualizado pela galeria interna. Descriptografe a pasta para abrir o arquivo no visualizador/editor compatível.",
                        "CryptoFotos",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                using var previewForm = new Form
                {
                    Text = $"Visualizacao - {originalFileName}",
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
                MessageBox.Show($"Erro ao abrir a foto: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private byte[] DecryptSelectedBytes(string encryptedFilePath)
        {
            EnsureSafeFile(encryptedFilePath, MaxEncryptedFileSizeBytes);
            byte[] encryptedBytes = File.ReadAllBytes(encryptedFilePath);
            if (importedKeyActive)
            {
                (byte[] keyBytes, byte[] ivBytes) = GetActiveKeyMaterial();
                return CryptoUtils.DecryptBytesWithKey(encryptedBytes, keyBytes, ivBytes);
            }

            return CryptoUtils.DecryptBytes(encryptedBytes);
        }

        private byte[] EncryptSelectedBytes(byte[] imageBytes)
        {
            (byte[] keyBytes, byte[] ivBytes) = GetActiveKeyMaterial();
            return CryptoUtils.EncryptBytesWithKey(imageBytes, keyBytes, ivBytes);
        }

        private (byte[] Key, byte[] IV) GetActiveKeyMaterial()
        {
            if (activeKey == null || activeIV == null)
            {
                throw new InvalidOperationException("Nenhuma chave de criptografia está carregada.");
            }

            return (activeKey, activeIV);
        }

        private static Image CreateImageThumbnail(byte[] imageBytes, int width, int height)
        {
            using var memoryStream = new MemoryStream(imageBytes);
            using var loadedImage = Image.FromStream(memoryStream, false, true);
            ValidateImageDimensions(loadedImage);
            return loadedImage.GetThumbnailImage(width, height, () => false, IntPtr.Zero);
        }

        private static Image CreateGalleryThumbnail(byte[] imageBytes, string originalFileName, int width, int height)
        {
            try
            {
                return CreateImageThumbnail(imageBytes, width, height);
            }
            catch (Exception ex) when (IsUnsupportedImageDecodeException(ex))
            {
                return CreatePhotoPlaceholderThumbnail(originalFileName, width, height);
            }
        }

        private static Image CreatePhotoPlaceholderThumbnail(string originalFileName, int width, int height)
        {
            var bitmap = new Bitmap(width, height);
            using Graphics graphics = Graphics.FromImage(bitmap);
            graphics.Clear(Color.WhiteSmoke);

            using var borderPen = new Pen(Color.Silver);
            graphics.DrawRectangle(borderPen, 0, 0, width - 1, height - 1);

            string extension = Path.GetExtension(originalFileName).TrimStart('.').ToUpperInvariant();
            string label = string.IsNullOrWhiteSpace(extension)
                ? "FOTO"
                : extension.Length > 5 ? extension.Substring(0, 5) : extension;

            using var font = new Font(SystemFonts.DefaultFont.FontFamily, 8F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.DimGray);
            using var format = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                Trimming = StringTrimming.EllipsisCharacter
            };

            graphics.DrawString(label, font, brush, new RectangleF(2, 2, width - 4, height - 4), format);
            return bitmap;
        }

        private static bool IsUnsupportedImageDecodeException(Exception ex)
        {
            return ex is ArgumentException || ex is OutOfMemoryException;
        }

        private static string[] GetSafeFiles(
            string folder,
            string searchPattern,
            Func<string, bool> predicate,
            long maxFileSizeBytes)
        {
            string root = GetCanonicalDirectoryPath(folder);
            string rootWithSeparator = EnsureTrailingSeparator(root);
            var files = new List<string>();

            foreach (string file in Directory.EnumerateFiles(root, searchPattern, SafeRecursiveEnumerationOptions))
            {
                string fullPath = Path.GetFullPath(file);
                if (!IsPathInsideRoot(rootWithSeparator, fullPath))
                {
                    continue;
                }

                if (!predicate(fullPath))
                {
                    continue;
                }

                EnsureSafeFile(fullPath, maxFileSizeBytes);
                files.Add(fullPath);

                if (files.Count > MaxFilesPerOperation)
                {
                    throw new InvalidOperationException($"A operacao excede o limite de {MaxFilesPerOperation} arquivos.");
                }
            }

            return files.ToArray();
        }

        private static string ResolveOriginalRelativePath(string folder, string encryptedFilePath)
        {
            string relativeEncryptedPath = Path.GetRelativePath(folder, encryptedFilePath);
            string relativeOriginalPath = Path.ChangeExtension(relativeEncryptedPath, null) ?? relativeEncryptedPath;

            if (!IsSupportedPhotoPath(relativeOriginalPath))
            {
                throw new InvalidOperationException("O arquivo criptografado não representa uma extensão de foto permitida.");
            }

            return relativeOriginalPath;
        }

        private static bool IsSupportedPhotoPath(string filePath) => PhotoExtensions.Contains(Path.GetExtension(filePath));

        private static void ValidatePhotoBytes(string filePath, byte[] photoBytes)
        {
            if (!IsSupportedPhotoPath(filePath))
            {
                throw new InvalidOperationException("Extensão de foto não permitida.");
            }

            if (photoBytes.Length == 0)
            {
                throw new InvalidOperationException("Arquivo de foto vazio.");
            }

            string extension = Path.GetExtension(filePath);
            if (DecodableImageExtensions.Contains(extension))
            {
                ValidateImageBytes(photoBytes);
                return;
            }

            if (HasKnownPhotoSignature(photoBytes) || CameraRawPhotoExtensions.Contains(extension))
            {
                TryValidateImageDimensions(photoBytes);
                return;
            }

            throw new InvalidOperationException("O arquivo não foi identificado como uma foto válida.");
        }

        private static void ValidateImageBytes(byte[] imageBytes)
        {
            using var memoryStream = new MemoryStream(imageBytes);
            using var loadedImage = Image.FromStream(memoryStream, false, true);
            ValidateImageDimensions(loadedImage);
        }

        private static void TryValidateImageDimensions(byte[] imageBytes)
        {
            try
            {
                ValidateImageBytes(imageBytes);
            }
            catch (ArgumentException)
            {
            }
            catch (OutOfMemoryException)
            {
            }
        }

        private static bool HasKnownPhotoSignature(byte[] bytes)
        {
            ReadOnlySpan<byte> data = bytes;

            return StartsWith(data, stackalloc byte[] { 0xFF, 0xD8, 0xFF }) ||
                StartsWith(data, stackalloc byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }) ||
                StartsWithAscii(data, "GIF87a") ||
                StartsWithAscii(data, "GIF89a") ||
                StartsWithAscii(data, "BM") ||
                StartsWith(data, stackalloc byte[] { 0x49, 0x49, 0x2A, 0x00 }) ||
                StartsWith(data, stackalloc byte[] { 0x4D, 0x4D, 0x00, 0x2A }) ||
                IsRiffWebP(data) ||
                HasIsoBaseMediaPhotoBrand(data) ||
                IsJpeg2000(data) ||
                StartsWith(data, stackalloc byte[] { 0xFF, 0x0A }) ||
                StartsWith(data, stackalloc byte[] { 0x00, 0x00, 0x00, 0x0C, 0x4A, 0x58, 0x4C, 0x20, 0x0D, 0x0A, 0x87, 0x0A }) ||
                StartsWithAscii(data, "FUJIFILMCCD-RAW") ||
                StartsWithAscii(data, "FOVb") ||
                StartsWithAscii(data, "8BPS");
        }

        private static bool IsRiffWebP(ReadOnlySpan<byte> data)
        {
            return data.Length >= 12 &&
                StartsWithAscii(data, "RIFF") &&
                AsciiEquals(data.Slice(8, 4), "WEBP");
        }

        private static bool HasIsoBaseMediaPhotoBrand(ReadOnlySpan<byte> data)
        {
            if (data.Length < 12 || !AsciiEquals(data.Slice(4, 4), "ftyp"))
            {
                return false;
            }

            ReadOnlySpan<byte> brands = data.Slice(8, Math.Min(data.Length - 8, 96));
            foreach (string brand in IsoBaseMediaPhotoBrands)
            {
                if (ContainsAscii(brands, brand))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsJpeg2000(ReadOnlySpan<byte> data)
        {
            return StartsWith(data, stackalloc byte[] { 0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A }) ||
                StartsWith(data, stackalloc byte[] { 0xFF, 0x4F, 0xFF, 0x51 });
        }

        private static bool StartsWith(ReadOnlySpan<byte> data, ReadOnlySpan<byte> expected)
        {
            return data.Length >= expected.Length && data.Slice(0, expected.Length).SequenceEqual(expected);
        }

        private static bool StartsWithAscii(ReadOnlySpan<byte> data, string expected)
        {
            return data.Length >= expected.Length && AsciiEquals(data.Slice(0, expected.Length), expected);
        }

        private static bool ContainsAscii(ReadOnlySpan<byte> data, string value)
        {
            if (value.Length == 0 || data.Length < value.Length)
            {
                return false;
            }

            for (int i = 0; i <= data.Length - value.Length; i++)
            {
                if (AsciiEquals(data.Slice(i, value.Length), value))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool AsciiEquals(ReadOnlySpan<byte> data, string value)
        {
            if (data.Length != value.Length)
            {
                return false;
            }

            for (int i = 0; i < value.Length; i++)
            {
                if (data[i] != (byte)value[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static void ValidateImageDimensions(Image image)
        {
            if (image.Width <= 0 || image.Height <= 0)
            {
                throw new InvalidOperationException("Imagem inválida.");
            }

            if (image.Width > MaxImageDimension || image.Height > MaxImageDimension)
            {
                throw new InvalidOperationException("Imagem excede a dimensao maxima permitida.");
            }

            long pixels = (long)image.Width * image.Height;
            if (pixels > MaxImagePixels)
            {
                throw new InvalidOperationException("Imagem excede o limite de pixels permitido.");
            }
        }

        private static string CreateAvailableOutputDirectory(string baseDirectory)
        {
            string fullBaseDirectory = Path.GetFullPath(baseDirectory);

            for (int suffix = 0; suffix <= MaxFilesPerOperation; suffix++)
            {
                string candidate = suffix == 0
                    ? fullBaseDirectory
                    : $"{fullBaseDirectory}-{suffix}";

                if (Directory.Exists(candidate))
                {
                    continue;
                }

                Directory.CreateDirectory(candidate);
                EnsureSafeDirectory(candidate);

                if (!Directory.EnumerateFileSystemEntries(candidate).Any())
                {
                    return GetCanonicalDirectoryPath(candidate);
                }
            }

            throw new IOException("Não foi possível criar uma pasta de saída exclusiva.");
        }

        private static string CombineInsideRoot(string root, string relativePath)
        {
            if (Path.IsPathRooted(relativePath))
            {
                throw new InvalidOperationException("Caminho relativo inválido.");
            }

            string[] segments = relativePath.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            if (segments.Any(segment => segment == ".."))
            {
                throw new InvalidOperationException("Caminho relativo inválido.");
            }

            string rootWithSeparator = EnsureTrailingSeparator(GetCanonicalDirectoryPath(root));
            string combined = Path.GetFullPath(Path.Combine(root, relativePath));
            if (!IsPathInsideRoot(rootWithSeparator, combined))
            {
                throw new InvalidOperationException("Caminho de saída fora da pasta esperada.");
            }

            return combined;
        }

        private static bool IsSafeDirectory(string directoryPath)
        {
            try
            {
                return Directory.Exists(directoryPath) && !IsReparsePoint(directoryPath);
            }
            catch
            {
                return false;
            }
        }

        private static void EnsureSafeDirectory(string directoryPath)
        {
            if (!Directory.Exists(directoryPath) || IsReparsePoint(directoryPath))
            {
                throw new InvalidOperationException("Diretorio inseguro ou redirecionado.");
            }
        }

        private static void EnsureSafeFile(string filePath, long maxFileSizeBytes)
        {
            if (IsReparsePoint(filePath))
            {
                throw new InvalidOperationException("Arquivo redirecionado não permitido.");
            }

            var fileInfo = new FileInfo(filePath);
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException("Arquivo não encontrado.", filePath);
            }

            if (fileInfo.Length > maxFileSizeBytes)
            {
                throw new InvalidOperationException("Arquivo excede o tamanho maximo permitido.");
            }
        }

        private static bool IsReparsePoint(string path)
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == FileAttributes.ReparsePoint;
        }

        private static string GetCanonicalDirectoryPath(string path)
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static string EnsureTrailingSeparator(string path)
        {
            return path.EndsWith(Path.DirectorySeparatorChar.ToString(), StringComparison.Ordinal)
                ? path
                : path + Path.DirectorySeparatorChar;
        }

        private static bool IsPathInsideRoot(string rootWithSeparator, string fullPath)
        {
            string normalizedFullPath = Path.GetFullPath(fullPath);
            return normalizedFullPath.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
        }

        private static void WriteAllBytesCreateNew(string filePath, byte[] bytes)
        {
            using var output = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None);
            output.Write(bytes, 0, bytes.Length);
        }

        private void LoadLocalKeyMaterial()
        {
            ClearActiveKey();
            (byte[] keyBytes, byte[] ivBytes) = CryptoUtils.GetOrCreateUserKeyMaterial();
            activeKey = keyBytes;
            activeIV = ivBytes;
            importedKeyActive = false;
            UpdateKeyButtonState();
        }

        private void ClearActiveKey()
        {
            CryptoUtils.ClearSensitiveBytes(activeKey);
            CryptoUtils.ClearSensitiveBytes(activeIV);
            activeKey = null;
            activeIV = null;
            importedKeyActive = false;
        }

        private void UpdateKeyButtonState()
        {
            if (btnImportKey == null)
            {
                return;
            }

            btnImportKey.Text = importedKeyActive ? "Chave Importada" : "Importar Chave";
            btnImportKey.BackColor = importedKeyActive ? Color.FromArgb(40, 167, 69) : Color.LightGray;
        }

        private static byte[] CreateProtectedKeyFileBytes(byte[] keyBytes, byte[] ivBytes)
        {
            return CryptoUtils.CreateProtectedKeyFileBytes(keyBytes, ivBytes);
        }

        private static bool IsProtectedKeyFile(byte[] data)
        {
            return CryptoUtils.IsProtectedKeyFile(data);
        }

        private static (byte[] Key, byte[] IV) ParseImportedKeyFile(byte[] importedBytes)
        {
            return CryptoUtils.ParseProtectedKeyFile(importedBytes);
        }

        private void btnExportKey_Click(object sender, EventArgs e)
        {
            using var saveFileDialog = new SaveFileDialog
            {
                Filter = "Arquivo de Chave (*.txt)|*.txt",
                Title = "Exportar Chave de Criptografia",
                FileName = "chave_cryptofotos.txt"
            };

            if (saveFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            try
            {
                (byte[] keyBytes, byte[] ivBytes) = GetActiveKeyMaterial();
                byte[]? keyFileBytes = null;

                try
                {
                    keyFileBytes = CreateProtectedKeyFileBytes(keyBytes, ivBytes);
                    string base64 = Convert.ToBase64String(keyFileBytes);
                    File.WriteAllText(saveFileDialog.FileName, base64, new UTF8Encoding(false));
                }
                finally
                {
                    CryptoUtils.ClearSensitiveBytes(keyFileBytes);
                }

                MessageBox.Show(
                    $"Chave exportada com sucesso em:\n{saveFileDialog.FileName}\n\nGuarde esse arquivo com cuidado. Ele permite acesso aos dados criptografados.",
                    "CryptoFotos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao exportar chave: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnImportKey_Click(object sender, EventArgs e)
        {
            if (importedKeyActive)
            {
                LoadLocalKeyMaterial();
                MessageBox.Show("A chave ativa foi desativada. O programa voltou a usar a chave padrão embutida.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var openFileDialog = new OpenFileDialog
            {
                Filter = "Arquivo de Chave (*.txt)|*.txt",
                Title = "Importar Chave de Criptografia"
            };

            if (openFileDialog.ShowDialog() != DialogResult.OK)
            {
                return;
            }

            byte[]? importedBytes = null;

            try
            {
                EnsureSafeFile(openFileDialog.FileName, 4096);
                string content = File.ReadAllText(openFileDialog.FileName).Trim();

                try
                {
                    importedBytes = Convert.FromBase64String(content);
                }
                catch (FormatException)
                {
                    importedBytes = File.ReadAllBytes(openFileDialog.FileName);
                }

                if (!IsProtectedKeyFile(importedBytes))
                {
                    throw new InvalidOperationException("Arquivo de chave inválido. Use somente o padrão CSK3 exportado por CryptoFotos, CryptoMulti ou CryptoTxt.");
                }

                (byte[] keyBytes, byte[] ivBytes) = ParseImportedKeyFile(importedBytes);
                ClearActiveKey();
                activeKey = keyBytes;
                activeIV = ivBytes;
                importedKeyActive = true;
                UpdateKeyButtonState();

                MessageBox.Show("Chave importada com sucesso. Ela será usada até ser desativada ou até o programa fechar.", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao importar chave: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                CryptoUtils.ClearSensitiveBytes(importedBytes);
            }
        }

        private void btnGenerateKey_Click(object sender, EventArgs e)
        {
            try
            {
                (byte[] keyBytes, byte[] ivBytes) = CryptoUtils.GenerateNewKeyMaterial();
                ClearActiveKey();
                activeKey = keyBytes;
                activeIV = ivBytes;
                importedKeyActive = true;
                btnImportKey.Text = "Chave Gerada";
                btnImportKey.BackColor = Color.FromArgb(40, 167, 69);

                MessageBox.Show(
                    "Nova chave gerada e carregada. Use Exportar Chave para salvar essa chave em arquivo.",
                    "CryptoFotos",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Erro ao gerar chave: {ex.Message}", "CryptoFotos", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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
            btnExportKey.Visible = false;
            btnImportKey.Visible = false;
            btnGenerateKey.Visible = false;
        }

        private void EndProgress()
        {
            progressBar.Visible = false;
            btnCancel.Visible = false;
            lblStatus.Visible = false;
            btnExportKey.Visible = true;
            btnImportKey.Visible = true;
            btnGenerateKey.Visible = true;
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
