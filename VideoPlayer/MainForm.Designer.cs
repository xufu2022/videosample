namespace VideoPlayer
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        private System.Windows.Forms.ListBox listBoxVideos;
        private System.Windows.Forms.Button btnRefresh;
        private System.Windows.Forms.Button btnPlay;
        private Microsoft.Web.WebView2.WinForms.WebView2 webView;
        private System.Windows.Forms.Panel controlPanel;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.listBoxVideos = new System.Windows.Forms.ListBox();
            this.btnRefresh = new System.Windows.Forms.Button();
            this.btnPlay = new System.Windows.Forms.Button();
            this.webView = new Microsoft.Web.WebView2.WinForms.WebView2();
            this.controlPanel = new System.Windows.Forms.Panel();

            // controlPanel
            this.controlPanel.Dock = System.Windows.Forms.DockStyle.Top;
            this.controlPanel.Height = 40;
            this.controlPanel.Controls.Add(this.btnPlay);
            this.controlPanel.Controls.Add(this.btnRefresh);

            // btnRefresh
            this.btnRefresh.Text = "Refresh";
            this.btnRefresh.Width = 80;
            this.btnRefresh.Location = new System.Drawing.Point(4, 8);
            this.btnRefresh.Click += new System.EventHandler(this.btnRefresh_Click);

            // btnPlay
            this.btnPlay.Text = "Play";
            this.btnPlay.Width = 80;
            this.btnPlay.Location = new System.Drawing.Point(92, 8);
            this.btnPlay.Enabled = false;
            this.btnPlay.Click += new System.EventHandler(this.btnPlay_Click);

            // listBoxVideos
            this.listBoxVideos.Dock = System.Windows.Forms.DockStyle.Top;
            this.listBoxVideos.Height = 160;

            // webView
            this.webView.Dock = System.Windows.Forms.DockStyle.Fill;

            // MainForm
            this.Text = "Video Player";
            this.ClientSize = new System.Drawing.Size(800, 560);
            this.Controls.Add(this.webView);
            this.Controls.Add(this.listBoxVideos);
            this.Controls.Add(this.controlPanel);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
        }
    }
}
