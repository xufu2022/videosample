using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Windows.Forms;
using Newtonsoft.Json;

namespace VideoPlayer
{
    public partial class MainForm : Form
    {
        private const string ApiBase = "http://localhost:5000";
        private static readonly HttpClient _httpClient = new HttpClient();

        private bool _pendingPlay = false;

        public MainForm()
        {
            InitializeComponent();
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {
            await InitializeWebViewAsync();
            await RefreshVideoListAsync();
        }

        private async Task InitializeWebViewAsync()
        {
            try
            {
                await webView.EnsureCoreWebView2Async(null);
                webView.CoreWebView2.NavigationCompleted += CoreWebView2_NavigationCompleted;
                btnPlay.Enabled = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"WebView2 initialization failed: {ex.Message}");
            }
        }

        private async void CoreWebView2_NavigationCompleted(
            object sender, Microsoft.Web.WebView2.Core.CoreWebView2NavigationCompletedEventArgs e)
        {
            if (!_pendingPlay) return;
            _pendingPlay = false;
            await webView.CoreWebView2.ExecuteScriptAsync(
                "var v = document.querySelector('video'); if (v) v.play();");
        }

        private async void btnRefresh_Click(object sender, EventArgs e)
        {
            await RefreshVideoListAsync();
        }

        private void btnPlay_Click(object sender, EventArgs e)
        {
            if (listBoxVideos.SelectedItem == null) return;
            var blobName = listBoxVideos.SelectedItem.ToString();
            var url = $"{ApiBase}/api/video/{blobName}";
            _pendingPlay = true;
            PlayVideo(url);
        }

        private async Task RefreshVideoListAsync()
        {
            try
            {
                var json = await _httpClient.GetStringAsync($"{ApiBase}/api/video/list");
                var names = JsonConvert.DeserializeObject<List<string>>(json);
                listBoxVideos.Items.Clear();
                foreach (var name in names)
                    listBoxVideos.Items.Add(name);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Failed to fetch video list: {ex.Message}");
            }
        }

        private void PlayVideo(string url)
        {
            var html = $@"<!DOCTYPE html>
<html>
<body style=""margin:0;background:#000"">
  <video controls style=""width:100%;height:100vh""
         src=""{url}"">
  </video>
</body>
</html>";
            webView.NavigateToString(html);
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            // WebView2 is a managed control; disposed automatically with the form.
        }
    }
}
