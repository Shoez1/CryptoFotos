using System;
using System.IO;
using System.Windows.Forms;
using System.Drawing;
using System.Collections.Generic;
using System.Linq;

namespace CryptoFotos
{
    public partial class MainForm : Form
    {
        private static readonly string[] ImageExtensions = new[] { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        public MainForm()
        {
            InitializeComponent();
        }

        private void btnSelectFolder_Click(object sender, EventArgs e)
        {
            using (var fbd = new FolderBrowserDialog())
            {
                if (fbd.ShowDialog() == DialogResult.OK)
                {
                    txtFolderPath.Text = fbd.SelectedPath;
                }
            }
        }

        private void txtFolderPath_DragEnter(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
                e.Effect = DragDropEffects.Copy;
            else
                e.Effect = DragDropEffects.None;
        }

        private void txtFolderPath_DragDrop(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                string[] files = (string[])e.Data.GetData(DataFormats.FileDrop);
                if (files.Length > 0 && Directory.Exists(files[0]))
                {
                    txtFolderPath.Text = files[0];
                }
            }
        }

        private void btnEncryptFolder_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text;
            if (!Directory.Exists(folder))
            {
                MessageBox.Show("Selecione uma pasta válida.");
                return;
            }
            string parent = Path.GetDirectoryName(folder);
            string name = Path.GetFileName(folder);
            string outDir = Path.Combine(parent, name + "-cry");
            Directory.CreateDirectory(outDir);
            int count = 0;
            foreach (var file in Directory.GetFiles(folder))
            {
                if (!ImageExtensions.Contains(Path.GetExtension(file).ToLower())) continue;
                byte[] imgBytes = File.ReadAllBytes(file);
                byte[] enc = Utils.CryptoUtils.EncryptBytes(imgBytes);
                File.WriteAllBytes(Path.Combine(outDir, Path.GetFileName(file) + ".enc"), enc);
                count++;
            }
            MessageBox.Show($"{count} imagens criptografadas em {outDir}");
        }

        private void btnDecryptFolder_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text;
            if (!Directory.Exists(folder) || !folder.EndsWith("-cry"))
            {
                MessageBox.Show("Selecione uma pasta criptografada válida (terminando com -cry).");
                return;
            }
            string parent = Path.GetDirectoryName(folder);
            string name = Path.GetFileName(folder);
            string origName = name.Substring(0, name.Length - 4); // remove -cry
            string outDir = Path.Combine(parent, origName);
            Directory.CreateDirectory(outDir);
            int count = 0;
            foreach (var file in Directory.GetFiles(folder, "*.enc"))
            {
                try
                {
                    byte[] enc = File.ReadAllBytes(file);
                    byte[] imgBytes = Utils.CryptoUtils.DecryptBytes(enc);
                    string origFile = Path.GetFileNameWithoutExtension(file);
                    string ext = GuessImageExtension(imgBytes);
                    if (ext == null) ext = ".jpg";
                    File.WriteAllBytes(Path.Combine(outDir, origFile + ext), imgBytes);
                    count++;
                }
                catch { }
            }
            MessageBox.Show($"{count} imagens descriptografadas em {outDir}");
        }

        private string GuessImageExtension(byte[] imgBytes)
        {
            try
            {
                using (var ms = new MemoryStream(imgBytes))
                using (var img = Image.FromStream(ms, false, true))
                {
                    if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Jpeg)) return ".jpg";
                    if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Png)) return ".png";
                    if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Bmp)) return ".bmp";
                    if (img.RawFormat.Equals(System.Drawing.Imaging.ImageFormat.Gif)) return ".gif";
                }
            }
            catch { }
            return null;
        }

        private void btnPreviewGallery_Click(object sender, EventArgs e)
        {
            string folder = txtFolderPath.Text;
            if (!Directory.Exists(folder) || !folder.EndsWith("-cry"))
            {
                MessageBox.Show("Selecione uma pasta criptografada válida (terminando com -cry).");
                return;
            }
            var files = Directory.GetFiles(folder, "*.enc");
            panelGallery.Controls.Clear();
            int x = 10, y = 10, w = 60, h = 60, margin = 10, perRow = 5;
            int col = 0, row = 0;
            List<Image> thumbs = new List<Image>();
            foreach (var file in files)
            {
                try
                {
                    byte[] enc = File.ReadAllBytes(file);
                    byte[] imgBytes = Utils.CryptoUtils.DecryptBytes(enc);
                    using (var ms = new MemoryStream(imgBytes))
                    {
                        Image img = Image.FromStream(ms);
                        Image thumb = img.GetThumbnailImage(w, h, () => false, IntPtr.Zero);
                        thumbs.Add(thumb);
                        var pb = new PictureBox();
                        pb.Image = thumb;
                        pb.SizeMode = PictureBoxSizeMode.Zoom;
                        pb.Width = w;
                        pb.Height = h;
                        pb.Left = x + col * (w + margin);
                        pb.Top = y + row * (h + margin);
                        pb.Cursor = Cursors.Hand;
                        pb.Click += (s, ev) => ShowImagePreview(img);
                        panelGallery.Controls.Add(pb);
                        col++;
                        if (col >= perRow)
                        {
                            col = 0;
                            row++;
                        }
                    }
                }
                catch { }
            }
        }

        private void ShowImagePreview(Image img)
        {
            using (var f = new Form())
            {
                f.Text = "Visualização";
                f.Width = 800;
                f.Height = 600;
                var pb = new PictureBox();
                pb.Dock = DockStyle.Fill;
                pb.Image = img;
                pb.SizeMode = PictureBoxSizeMode.Zoom;
                f.Controls.Add(pb);
                f.StartPosition = FormStartPosition.CenterParent;
                f.ShowDialog(this);
            }
        }
    }
}
