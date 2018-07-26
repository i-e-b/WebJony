namespace VersionUploader
{
    partial class MainForm
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.components = new System.ComponentModel.Container();
            this.label1 = new System.Windows.Forms.Label();
            this.sourceFolderTxt = new System.Windows.Forms.TextBox();
            this.loadSourceButton = new System.Windows.Forms.Button();
            this.testSourceButton = new System.Windows.Forms.Button();
            this.sourceTestResult = new System.Windows.Forms.Label();
            this.uploadUrl = new System.Windows.Forms.TextBox();
            this.targetUrl = new System.Windows.Forms.Label();
            this.uploadButton = new System.Windows.Forms.Button();
            this.progressBar = new System.Windows.Forms.ProgressBar();
            this.uploadResult = new System.Windows.Forms.Label();
            this.folderBrowserDialog = new System.Windows.Forms.FolderBrowserDialog();
            this.pfxPathButton = new System.Windows.Forms.Button();
            this.pfxPathBox = new System.Windows.Forms.TextBox();
            this.label2 = new System.Windows.Forms.Label();
            this.openFileDialog = new System.Windows.Forms.OpenFileDialog();
            this.pfxPassword = new System.Windows.Forms.TextBox();
            this.label3 = new System.Windows.Forms.Label();
            this.uploadTimer = new System.Windows.Forms.Timer(this.components);
            this.SuspendLayout();
            // 
            // label1
            // 
            this.label1.AutoSize = true;
            this.label1.Location = new System.Drawing.Point(14, 17);
            this.label1.Name = "label1";
            this.label1.Size = new System.Drawing.Size(73, 13);
            this.label1.TabIndex = 0;
            this.label1.Text = "Source Folder";
            // 
            // sourceFolderTxt
            // 
            this.sourceFolderTxt.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourceFolderTxt.Location = new System.Drawing.Point(93, 14);
            this.sourceFolderTxt.Name = "sourceFolderTxt";
            this.sourceFolderTxt.Size = new System.Drawing.Size(629, 20);
            this.sourceFolderTxt.TabIndex = 1;
            this.sourceFolderTxt.Text = "C:\\Temp\\WrappedSites_Disabled\\3_rolling";
            // 
            // loadSourceButton
            // 
            this.loadSourceButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.loadSourceButton.Location = new System.Drawing.Point(728, 12);
            this.loadSourceButton.Name = "loadSourceButton";
            this.loadSourceButton.Size = new System.Drawing.Size(54, 23);
            this.loadSourceButton.TabIndex = 2;
            this.loadSourceButton.Text = "...";
            this.loadSourceButton.UseVisualStyleBackColor = true;
            this.loadSourceButton.Click += new System.EventHandler(this.loadSourceButton_Click);
            // 
            // testSourceButton
            // 
            this.testSourceButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.testSourceButton.Location = new System.Drawing.Point(663, 41);
            this.testSourceButton.Name = "testSourceButton";
            this.testSourceButton.Size = new System.Drawing.Size(119, 23);
            this.testSourceButton.TabIndex = 3;
            this.testSourceButton.Text = "Test Source";
            this.testSourceButton.UseVisualStyleBackColor = true;
            this.testSourceButton.Click += new System.EventHandler(this.testSourceButton_Click);
            // 
            // sourceTestResult
            // 
            this.sourceTestResult.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.sourceTestResult.Location = new System.Drawing.Point(17, 41);
            this.sourceTestResult.Name = "sourceTestResult";
            this.sourceTestResult.Size = new System.Drawing.Size(640, 23);
            this.sourceTestResult.TabIndex = 4;
            this.sourceTestResult.Text = "?";
            this.sourceTestResult.TextAlign = System.Drawing.ContentAlignment.MiddleRight;
            // 
            // uploadUrl
            // 
            this.uploadUrl.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uploadUrl.Font = new System.Drawing.Font("Consolas", 9F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.uploadUrl.Location = new System.Drawing.Point(93, 156);
            this.uploadUrl.Name = "uploadUrl";
            this.uploadUrl.Size = new System.Drawing.Size(564, 22);
            this.uploadUrl.TabIndex = 6;
            this.uploadUrl.Text = "http://iebwraptest.cloudapp.net/upload";
            // 
            // targetUrl
            // 
            this.targetUrl.AutoSize = true;
            this.targetUrl.Location = new System.Drawing.Point(14, 159);
            this.targetUrl.Name = "targetUrl";
            this.targetUrl.Size = new System.Drawing.Size(63, 13);
            this.targetUrl.TabIndex = 5;
            this.targetUrl.Text = "Target URL";
            // 
            // uploadButton
            // 
            this.uploadButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.uploadButton.Location = new System.Drawing.Point(663, 154);
            this.uploadButton.Name = "uploadButton";
            this.uploadButton.Size = new System.Drawing.Size(119, 23);
            this.uploadButton.TabIndex = 7;
            this.uploadButton.Text = "Upload Version";
            this.uploadButton.UseVisualStyleBackColor = true;
            this.uploadButton.Click += new System.EventHandler(this.uploadButton_Click);
            // 
            // progressBar
            // 
            this.progressBar.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.progressBar.Location = new System.Drawing.Point(93, 183);
            this.progressBar.Name = "progressBar";
            this.progressBar.Size = new System.Drawing.Size(564, 23);
            this.progressBar.Style = System.Windows.Forms.ProgressBarStyle.Continuous;
            this.progressBar.TabIndex = 8;
            // 
            // uploadResult
            // 
            this.uploadResult.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.uploadResult.Location = new System.Drawing.Point(17, 209);
            this.uploadResult.Name = "uploadResult";
            this.uploadResult.Size = new System.Drawing.Size(640, 42);
            this.uploadResult.TabIndex = 9;
            this.uploadResult.Text = "Ready";
            // 
            // folderBrowserDialog
            // 
            this.folderBrowserDialog.Description = "Select the top level folder containing API binaries and resources";
            this.folderBrowserDialog.RootFolder = System.Environment.SpecialFolder.MyComputer;
            this.folderBrowserDialog.ShowNewFolderButton = false;
            // 
            // pfxPathButton
            // 
            this.pfxPathButton.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.pfxPathButton.Location = new System.Drawing.Point(728, 97);
            this.pfxPathButton.Name = "pfxPathButton";
            this.pfxPathButton.Size = new System.Drawing.Size(54, 23);
            this.pfxPathButton.TabIndex = 12;
            this.pfxPathButton.Text = "...";
            this.pfxPathButton.UseVisualStyleBackColor = true;
            this.pfxPathButton.Click += new System.EventHandler(this.pfxPathButton_Click);
            // 
            // pfxPathBox
            // 
            this.pfxPathBox.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pfxPathBox.Location = new System.Drawing.Point(93, 99);
            this.pfxPathBox.Name = "pfxPathBox";
            this.pfxPathBox.Size = new System.Drawing.Size(629, 20);
            this.pfxPathBox.TabIndex = 11;
            this.pfxPathBox.Text = "C:\\Temp\\WrappedSites\\deploy-certs\\WrapperSigning.pfx";
            // 
            // label2
            // 
            this.label2.AutoSize = true;
            this.label2.Location = new System.Drawing.Point(14, 102);
            this.label2.Name = "label2";
            this.label2.Size = new System.Drawing.Size(59, 13);
            this.label2.TabIndex = 10;
            this.label2.Text = "Signing pfx";
            // 
            // openFileDialog
            // 
            this.openFileDialog.AddExtension = false;
            this.openFileDialog.DefaultExt = "*.pfx";
            this.openFileDialog.FileName = "openFileDialog1";
            this.openFileDialog.Filter = "PFX Files|*.pfx";
            this.openFileDialog.Title = "Pick PFX file path";
            // 
            // pfxPassword
            // 
            this.pfxPassword.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.pfxPassword.Location = new System.Drawing.Point(93, 128);
            this.pfxPassword.Name = "pfxPassword";
            this.pfxPassword.Size = new System.Drawing.Size(629, 20);
            this.pfxPassword.TabIndex = 14;
            this.pfxPassword.UseSystemPasswordChar = true;
            // 
            // label3
            // 
            this.label3.AutoSize = true;
            this.label3.Location = new System.Drawing.Point(14, 131);
            this.label3.Name = "label3";
            this.label3.Size = new System.Drawing.Size(70, 13);
            this.label3.TabIndex = 13;
            this.label3.Text = "Pfx password";
            // 
            // uploadTimer
            // 
            this.uploadTimer.Enabled = true;
            this.uploadTimer.Interval = 500;
            this.uploadTimer.Tick += new System.EventHandler(this.uploadTimer_Tick);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(794, 260);
            this.Controls.Add(this.pfxPassword);
            this.Controls.Add(this.label3);
            this.Controls.Add(this.pfxPathButton);
            this.Controls.Add(this.pfxPathBox);
            this.Controls.Add(this.label2);
            this.Controls.Add(this.uploadResult);
            this.Controls.Add(this.progressBar);
            this.Controls.Add(this.uploadButton);
            this.Controls.Add(this.uploadUrl);
            this.Controls.Add(this.targetUrl);
            this.Controls.Add(this.sourceTestResult);
            this.Controls.Add(this.testSourceButton);
            this.Controls.Add(this.loadSourceButton);
            this.Controls.Add(this.sourceFolderTxt);
            this.Controls.Add(this.label1);
            this.MinimumSize = new System.Drawing.Size(810, 299);
            this.Name = "MainForm";
            this.Text = "MainForm";
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Label label1;
        private System.Windows.Forms.TextBox sourceFolderTxt;
        private System.Windows.Forms.Button loadSourceButton;
        private System.Windows.Forms.Button testSourceButton;
        private System.Windows.Forms.Label sourceTestResult;
        private System.Windows.Forms.TextBox uploadUrl;
        private System.Windows.Forms.Label targetUrl;
        private System.Windows.Forms.Button uploadButton;
        private System.Windows.Forms.ProgressBar progressBar;
        private System.Windows.Forms.Label uploadResult;
        private System.Windows.Forms.FolderBrowserDialog folderBrowserDialog;
        private System.Windows.Forms.Button pfxPathButton;
        private System.Windows.Forms.TextBox pfxPathBox;
        private System.Windows.Forms.Label label2;
        private System.Windows.Forms.OpenFileDialog openFileDialog;
        private System.Windows.Forms.TextBox pfxPassword;
        private System.Windows.Forms.Label label3;
        private System.Windows.Forms.Timer uploadTimer;
    }
}