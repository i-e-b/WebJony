using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;
using ShiftIt.Http;
using WrapperCommon.AssemblyLoading;
using HttpClient = ShiftIt.Http.HttpClient;

namespace VersionUploader
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void loadSourceButton_Click(object sender, EventArgs e)
        {
            var result = folderBrowserDialog.ShowDialog();

            switch (result){
                case DialogResult.OK:
                case DialogResult.Yes:
                    sourceFolderTxt.Text = folderBrowserDialog.SelectedPath;
                    return;
            }
        }

        private void testSourceButton_Click(object sender, EventArgs e)
        {
            var scan = new PluginScanner(sourceFolderTxt.Text);
            scan.RefreshPlugins();
            var table = new VersionTable<string>();
            sourceTestResult.Text = "Scanning";
            sourceTestResult.Refresh();

            string[] all = null;
            var task1 = new Thread(() =>
            {
                all = scan.CurrentlyAvailable.ToArray();
            });
            task1.Start();
            while (task1.IsAlive) { Application.DoEvents(); }

            if ( ! all.Any()) {
                sourceTestResult.Text = "No binaries found";
                return;
            }

            List<string> versions = null;
            var task2 = new Thread(() =>
            {
                foreach (var path in all)
                {
                    table.SubmitVersion(path, (typePath, versionName, major) => versionName.Replace("_", "."));
                }

                versions = table.AllVersions().ToList();
            });
            task2.Start();
            while(task2.IsAlive) { Application.DoEvents(); }

            if ( ! versions.Any()) {
                sourceFolderTxt.Text = "No versioned entry points found";
                return;
            }

            sourceTestResult.Text = "Found: "+string.Join("; ", versions);
        }

        public long BytesSent;
        public long BytesTotal = 1;

        private void uploadButton_Click(object sender, EventArgs e)
        {
            var src = sourceFolderTxt.Text;
            BytesSent = 0;

            uploadResult.Text = "Compressing...";
            uploadResult.Refresh();

            var tmpPath = Path.Combine(src, "temp.inpkg");
            if (File.Exists(tmpPath)) File.Delete(tmpPath);
            if (File.Exists(tmpPath + ".tmp")) File.Delete(tmpPath + ".tmp");

            Exception error = null;
            var task1 = new Thread(() =>
            {
                try
                {
                    SimpleCompress.Compress.FolderToFile(src, tmpPath, pfxPathBox.Text, pfxPassword.Text);
                }
                catch (Exception ex)
                {
                    error = ex;
                }
            });
            task1.Start();
            while (task1.IsAlive) { Application.DoEvents(); }
            if (error != null) {
                MessageBox.Show("Failed to compress and sign upload package:\r\n" + error.Message, "Failure", MessageBoxButtons.OK);
                return;
            }

            string finalMessage = "";
            uploadResult.Text = "Uploading...";
            uploadResult.Refresh();
            var url = uploadUrl.Text;
            var task2 = new Thread(() =>
            {
                finalMessage = UploadPackage(tmpPath, ref url);
            });
            task2.Start();
            while (task2.IsAlive) { Application.DoEvents(); }
            uploadUrl.Text = url;
            uploadResult.Text = finalMessage;

            File.Delete(tmpPath);
            BytesSent = 0;
        }

        private string UploadPackage(string tmpPath, ref string url)
        {

            var client = new HttpClient { Timeout = TimeSpan.FromHours(1) };
            using (var fs = File.OpenRead(tmpPath))
            {
                BytesTotal = fs.Length;
                var rq = new HttpRequestBuilder().Post(new Uri(url)).Build(fs, fs.Length);
                using (var response = client.Request(rq, UpdateProgress))
                {
                    if (response.StatusClass == StatusClass.Redirection)
                    {
                        // likely if we are being upgraded to https
                        url = response.Headers["Location"];
                        return "A redirect message was given. The target URL has been updated.\r\nPlease try again";
                    }
                    return response.StatusMessage + "\r\n" + response.BodyReader.ReadStringToLength();
                }
            }

        }

        private void UpdateProgress(long obj)
        {
            BytesSent = obj;
            Invoke(new EventHandler<EventArgs>(uploadTimer_Tick), null, null);
            Application.DoEvents();
        }

        private void pfxPathButton_Click(object sender, EventArgs e)
        {
            var result = openFileDialog.ShowDialog();

            switch (result) {
                case DialogResult.OK:
                case DialogResult.Yes:
                    pfxPathBox.Text = openFileDialog.FileName;
                    return;
            }
        }

        private void uploadTimer_Tick(object sender, EventArgs e)
        {
            if (BytesTotal < 1) {
                progressBar.Value = 0;
                return;
            }

            var percent = 100 * Volatile.Read(ref BytesSent) / ((double)BytesTotal);

            if (percent > 99.99) {
                uploadResult.Text = "Upload complete.\r\nWaiting for unpack and warm-up on server.";
            }

            progressBar.Value = (int)percent;
            progressBar.Refresh();
        }
    }
}
