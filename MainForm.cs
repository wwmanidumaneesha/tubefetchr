using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Forms;
using YoutubeExplode;
using YoutubeExplode.Common;
using YoutubeExplode.Videos;
using YoutubeExplode.Videos.Streams;

namespace TubeFetchr
{
    public partial class MainForm : Form
    {
        private TextBox urlTextBox;
        private Button fetchButton;
        private Label titleLabel;
        private ComboBox qualityComboBox;
        private Button downloadButton;
        private ProgressBar downloadProgressBar;
        private Label statusLabel;
        private PictureBox thumbnailBox;
        private Button loadCookiesButton;
        private string importedCookies = null;


        // Lazy-loaded YoutubeClient; we'll create it with a custom HttpClient that has a real User-Agent.
        private YoutubeClient youtube;
        private Video currentVideo;
        private IStreamInfo[] availableStreams;
        private List<IStreamInfo[]> streamOptions = new(); // Mapping for ComboBox selections

        // Cache for ffmpeg.exe path to avoid repetitive extraction.
        private string _ffmpegPath;

        public MainForm()
        {
            InitializeComponent();
            // Defer UI building and heavy operations until form load
            this.Load += Form1_Load;
        }

        // In Form1_Load, build UI and perform deferred operations.
        private async void Form1_Load(object sender, EventArgs e)
        {
            BuildUI();

            // Defer clipboard reading so that UI appears fast.
            await Task.Delay(200);
            Task.Run(() =>
            {
                if (Clipboard.ContainsText())
                {
                    var text = Clipboard.GetText();
                    if (text.Contains("youtube.com/watch") || text.Contains("youtu.be"))
                    {
                        this.Invoke(() => urlTextBox.Text = text.Trim());
                    }
                }
            });

            // Optionally: Preload YoutubeExplode internals in the background.
            Task.Run(() =>
            {
                // Lazy-load youtube client with custom HttpClient with a real User-Agent.
                var handler = new HttpClientHandler();
                var client = new HttpClient(handler);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/122.0.0.0 Safari/537.36");
                var preload = new YoutubeClient(client);
                _ = preload.Videos; // Force loading internal caches.
            });
        }

        // BuildUI creates and adds controls to the form.
        private void BuildUI()
        {
            this.Text = "TubeFetchr - YouTube Downloader";
            this.Size = new Size(700, 500);
            this.StartPosition = FormStartPosition.CenterScreen;
            this.BackColor = Color.FromArgb(245, 247, 250);
            this.Font = new Font("Segoe UI", 10);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // URL TextBox
            urlTextBox = new TextBox
            {
                Size = new Size(500, 32),
                Location = new Point(30, 40),
                Font = new Font("Segoe UI", 10),
                PlaceholderText = "🎥 Paste YouTube video URL here...",
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(urlTextBox);

            // Fetch Button
            fetchButton = new Button
            {
                Text = "🎯 Fetch Info",
                Size = new Size(130, 32),
                Location = new Point(540, 40),
                BackColor = Color.FromArgb(33, 150, 243),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            fetchButton.FlatAppearance.BorderSize = 0;
            fetchButton.Click += FetchButton_Click;
            this.Controls.Add(fetchButton);

            // Load Cookies Button
            loadCookiesButton = new Button
            {
                Text = "🍪 Load Cookies",
                Size = new Size(130, 32),
                Location = new Point(540, 80),
                BackColor = Color.FromArgb(255, 160, 0),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand
            };
            loadCookiesButton.FlatAppearance.BorderSize = 0;
            loadCookiesButton.Click += LoadCookiesButton_Click;
            this.Controls.Add(loadCookiesButton);



            // Title Label
            titleLabel = new Label
            {
                Text = "📄 Video Title: [Not fetched]",
                Size = new Size(620, 40),
                Location = new Point(30, 90),
                Font = new Font("Segoe UI Semibold", 10, FontStyle.Bold),
                ForeColor = Color.DimGray
            };
            this.Controls.Add(titleLabel);

            // Quality ComboBox
            qualityComboBox = new ComboBox
            {
                Size = new Size(300, 32),
                Location = new Point(30, 150),
                Font = new Font("Segoe UI", 10),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            this.Controls.Add(qualityComboBox);

            // Download Button
            downloadButton = new Button
            {
                Text = "⬇ Download",
                Size = new Size(130, 32),
                Location = new Point(350, 150),
                BackColor = Color.FromArgb(76, 175, 80),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Cursor = Cursors.Hand,
                Enabled = false
            };
            downloadButton.FlatAppearance.BorderSize = 0;
            downloadButton.Click += DownloadButton_Click;
            this.Controls.Add(downloadButton);

            // Progress Bar
            downloadProgressBar = new ProgressBar
            {
                Size = new Size(640, 25),
                Location = new Point(30, 210),
                Style = ProgressBarStyle.Continuous
            };
            this.Controls.Add(downloadProgressBar);

            // Status Label
            statusLabel = new Label
            {
                Text = "💬 Status: Waiting for URL...",
                Size = new Size(640, 30),
                Location = new Point(30, 250),
                Font = new Font("Segoe UI", 9),
                ForeColor = Color.DarkSlateGray
            };
            this.Controls.Add(statusLabel);

            // Thumbnail PictureBox
            thumbnailBox = new PictureBox
            {
                Size = new Size(160, 90),
                Location = new Point(500, 90),
                SizeMode = PictureBoxSizeMode.Zoom,
                BorderStyle = BorderStyle.FixedSingle,
                Visible = false
            };
            this.Controls.Add(thumbnailBox);
        }

        // Helper: Sanitize file name for Windows.
        private string SanitizeFileName(string name)
        {
            foreach (char c in Path.GetInvalidFileNameChars())
                name = name.Replace(c, '_');
            return name;
        }

        // Helper: Extract and cache ffmpeg.exe from embedded resources.
        private string ExtractFfmpegToTemp()
        {
            if (!string.IsNullOrEmpty(_ffmpegPath) && File.Exists(_ffmpegPath))
                return _ffmpegPath;

            var tempPath = Path.Combine(Path.GetTempPath(), "ffmpeg.exe");
            if (!File.Exists(tempPath))
            {
                var resourceName = "TubeFetchr.Resources.ffmpeg.exe"; // Ensure correct namespace and path
                using var stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName);
                if (stream == null)
                    throw new Exception("Embedded resource 'ffmpeg.exe' not found. Check resource name and location.");
                using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write);
                stream.CopyTo(fileStream);
            }
            _ffmpegPath = tempPath;
            return _ffmpegPath;
        }

        // Helper: Retry an asynchronous operation (attempts it twice).
        private async Task<T> RetryOnceAsync<T>(Func<ValueTask<T>> action)
        {
            try
            {
                return await action().AsTask();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("⚠️ First attempt failed: " + ex.Message + " — Retrying...");
                await Task.Delay(1000);
                return await action().AsTask();
            }
        }

        // Helper: Randomized delay between actions.
        private async Task RandomDelayAsync()
        {
            var rand = new Random();
            int delay = rand.Next(800, 2200); // 0.8 to 2.2 seconds delay
            await Task.Delay(delay);
        }

        // FetchButton_Click: Fetch video info and available streams.
        private async void FetchButton_Click(object sender, EventArgs e)
        {
            try
            {
                string url = urlTextBox.Text.Trim();
                if (string.IsNullOrWhiteSpace(url))
                {
                    MessageBox.Show("Please enter a valid YouTube video URL.");
                    return;
                }

                statusLabel.Text = "🔄 Fetching video info...";
                fetchButton.Enabled = false;
                qualityComboBox.Items.Clear();
                downloadButton.Enabled = false;
                thumbnailBox.Visible = false;
                streamOptions.Clear();

                // Lazy-load youtube with a custom HttpClient and real User-Agent.
                if (youtube == null)
                {
                    var handler = new HttpClientHandler();
                    var client = new HttpClient(handler);
                    client.DefaultRequestHeaders.UserAgent
                          .ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/122.0.0.0 Safari/537.36");

                    if (!string.IsNullOrWhiteSpace(importedCookies))
                    {
                        client.DefaultRequestHeaders.Add("Cookie", importedCookies);
                    }

                    youtube = new YoutubeClient(client);

                }

                // Fetch video details.
                try
                {
                    currentVideo = await RetryOnceAsync(() => youtube.Videos.GetAsync(url));
                }
                catch (Exception ex)
                {
                    if (!string.IsNullOrWhiteSpace(importedCookies))
                    {
                        MessageBox.Show("⚠️ Failed to fetch video info. The provided cookies may be invalid or expired.\n\n" +
                                        $"Error: {ex.Message}", "Invalid Cookies", MessageBoxButtons.OK, MessageBoxIcon.Warning);

                        // ⛔ Clear cookies to avoid reuse
                        importedCookies = null;
                        statusLabel.Text = "⚠️ Cookies cleared due to error.";
                    }
                    else
                    {
                        MessageBox.Show("❌ Failed to fetch video info.\n\n" +
                                        $"Error: {ex.Message}", "Fetch Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        statusLabel.Text = "❌ Failed to fetch info.";
                    }

                    fetchButton.Enabled = true;
                    return;
                }


                // Optional: add a random delay to simulate natural behavior.
                await RandomDelayAsync();

                // Fetch available streams.
                var streamManifest = await RetryOnceAsync(() => youtube.Videos.Streams.GetManifestAsync(currentVideo.Id));

                // Get muxed streams (remove duplicate resolutions by grouping on label).
                var muxedStreams = streamManifest.GetMuxedStreams()
                    .OrderBy(s => s.VideoQuality.MaxHeight)
                    .GroupBy(s => s.VideoQuality.Label)
                    .Select(g => g.First())
                    .ToList();

                // Get video-only streams in mp4 format.
                var videoOnlyStreams = streamManifest.GetVideoOnlyStreams()
                    .Where(s => s.Container.Name.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(s => s.VideoQuality.MaxHeight)
                    .GroupBy(s => s.VideoQuality.Label)
                    .Select(g => g.First())
                    .ToList();

                // Get audio-only streams in mp4 format.
                var audioOnlyStreams = streamManifest.GetAudioOnlyStreams()
                    .Where(s => s.Container.Name.Equals("mp4", StringComparison.OrdinalIgnoreCase))
                    .OrderByDescending(s => s.Bitrate)
                    .ToList();

                // Add muxed streams to the ComboBox and options.
                foreach (var stream in muxedStreams)
                {
                    qualityComboBox.Items.Add($"{stream.VideoQuality.Label} (muxed)");
                    streamOptions.Add(new IStreamInfo[] { stream });
                }

                // Add merged (video-only + best audio) streams.
                var bestAudio = audioOnlyStreams.FirstOrDefault();
                if (bestAudio != null)
                {
                    foreach (var video in videoOnlyStreams)
                    {
                        qualityComboBox.Items.Add($"{video.VideoQuality.Label} (video + audio) [merge]");
                        streamOptions.Add(new IStreamInfo[] { video, bestAudio });
                    }
                    // Add MP3-only option.
                    qualityComboBox.Items.Add("🎵 Audio Only (MP3)");
                    streamOptions.Add(new IStreamInfo[] { bestAudio });
                }

                if (streamOptions.Count == 0)
                {
                    MessageBox.Show("No downloadable streams found.");
                    statusLabel.Text = "❌ No available streams.";
                    return;
                }

                availableStreams = streamOptions.SelectMany(s => s).Distinct().ToArray();

                // Update UI with video title.
                titleLabel.Text = $"📄 Video Title: {currentVideo.Title}";

                // Fetch and display thumbnail asynchronously, using a real User-Agent.
                try
                {
                    string thumbUrl = currentVideo.Thumbnails.GetWithHighestResolution().Url;
                    using var http = new HttpClient();
                    http.DefaultRequestHeaders.UserAgent.ParseAdd("Mozilla/5.0 (Windows NT 10.0; Win64; x64) Chrome/122.0.0.0 Safari/537.36");
                    var imageBytes = await http.GetByteArrayAsync(thumbUrl);
                    using var ms = new MemoryStream(imageBytes);
                    thumbnailBox.Image = Image.FromStream(ms);
                    thumbnailBox.Visible = true;
                }
                catch
                {
                    thumbnailBox.Visible = false;
                }

                qualityComboBox.SelectedIndex = 0;
                downloadButton.Enabled = true;
                statusLabel.Text = "✅ Video info loaded. Choose quality to download.";
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message);
                statusLabel.Text = "❌ Failed to fetch info.";
            }
            finally
            {
                fetchButton.Enabled = true;
            }
        }

        //Cookie Button Handler
        private void LoadCookiesButton_Click(object sender, EventArgs e)
        {
            using var dialog = new OpenFileDialog
            {
                Title = "Select cookie.txt file",
                Filter = "Text Files (*.txt)|*.txt"
            };

            if (dialog.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    importedCookies = ParseCookiesFromTxt(dialog.FileName);

                    if (!string.IsNullOrWhiteSpace(importedCookies))
                    {
                        MessageBox.Show("✅ Cookies loaded successfully!", "Cookies", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        statusLabel.Text = "🍪 Cookies ready for use.";
                    }
                    else
                    {
                        MessageBox.Show("⚠️ No valid cookies found in file.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show("❌ Failed to load cookies:\n" + ex.Message, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
        }

        private string ParseCookiesFromTxt(string path)
        {
            var lines = File.ReadAllLines(path);
            var cookieParts = lines
                .Where(line => !string.IsNullOrWhiteSpace(line) && !line.StartsWith("#"))
                .Select(line => line.Split('\t'))
                .Where(parts => parts.Length >= 7)
                .Select(parts => $"{parts[5]}={parts[6]}");

            return string.Join("; ", cookieParts);
        }



        // DownloadButton_Click: Downloads the selected stream(s) and merges if required.
        private async void DownloadButton_Click(object sender, EventArgs e)
        {
            try
            {
                if (qualityComboBox.SelectedIndex < 0 || streamOptions == null || streamOptions.Count == 0)
                {
                    MessageBox.Show("Please select a stream.");
                    return;
                }

                var selectedStreams = streamOptions[qualityComboBox.SelectedIndex];
                bool isMp3 = qualityComboBox.SelectedItem.ToString().Contains("MP3");

                string defaultExt = isMp3
                    ? "mp3"
                    : (selectedStreams.Length == 1 && selectedStreams[0] is MuxedStreamInfo mux
                        ? mux.Container.Name
                        : "mp4");

                using var dialog = new SaveFileDialog
                {
                    FileName = $"{SanitizeFileName(currentVideo.Title)}.{defaultExt}",
                    Filter = isMp3
                        ? "Audio Files (*.mp3)|*.mp3"
                        : $"Video Files (*.{defaultExt})|*.{defaultExt}"
                };

                if (dialog.ShowDialog() != DialogResult.OK)
                    return;

                string filePath = dialog.FileName;

                // Reset progress and disable buttons during processing.
                downloadProgressBar.Value = 0;
                fetchButton.Enabled = false;
                downloadButton.Enabled = false;

                var progress = new Progress<double>(p =>
                {
                    downloadProgressBar.Value = (int)(p * 100);
                });

                if (isMp3)
                {
                    var audioStream = selectedStreams[0];
                    string tempFile = Path.Combine(Path.GetTempPath(), "temp_audio.webm");

                    statusLabel.Text = "📥 Downloading audio...";
                    await youtube.Videos.Streams.DownloadAsync(audioStream, tempFile, progress);

                    statusLabel.Text = "🔄 Converting to MP3...";
                    var ffmpeg = new ProcessStartInfo
                    {
                        FileName = ExtractFfmpegToTemp(),
                        Arguments = $"-y -i \"{tempFile}\" -vn -ab 192k -ar 44100 -f mp3 \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true
                    };

                    using var process = Process.Start(ffmpeg);
                    await process.WaitForExitAsync();

                    File.Delete(tempFile);

                    statusLabel.Text = "✅ MP3 Download completed!";
                    MessageBox.Show("Download finished successfully (MP3)!", "Success");
                }
                else if (selectedStreams.Length == 1)
                {
                    statusLabel.Text = "📥 Downloading video...";
                    await youtube.Videos.Streams.DownloadAsync(selectedStreams[0], filePath, progress);

                    statusLabel.Text = "✅ Download completed!";
                    MessageBox.Show("Download finished successfully!", "Success");
                }
                else
                {
                    // Video + Audio: Merge without re-encoding.
                    string tempVideo = Path.Combine(Path.GetTempPath(), "video_temp.mp4");
                    string tempAudio = Path.Combine(Path.GetTempPath(), "audio_temp.mp4");

                    statusLabel.Text = "📥 Downloading video...";
                    await youtube.Videos.Streams.DownloadAsync(selectedStreams[0], tempVideo, progress);

                    statusLabel.Text = "📥 Downloading audio...";
                    await youtube.Videos.Streams.DownloadAsync(selectedStreams[1], tempAudio);

                    statusLabel.Text = "🔄 Merging video + audio...";
                    var ffmpeg = new ProcessStartInfo
                    {
                        FileName = ExtractFfmpegToTemp(),
                        Arguments = $"-y -i \"{tempVideo}\" -i \"{tempAudio}\" -c:v copy -c:a copy \"{filePath}\"",
                        UseShellExecute = false,
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true
                    };

                    using var process = Process.Start(ffmpeg);
                    await process.WaitForExitAsync();

                    File.Delete(tempVideo);
                    File.Delete(tempAudio);

                    statusLabel.Text = "✅ Download completed!";
                    MessageBox.Show("Download finished successfully (merged)!", "Success");
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Download error: " + ex.Message);
                statusLabel.Text = "❌ Download failed.";
            }
            finally
            {
                fetchButton.Enabled = true;
                downloadButton.Enabled = true;
            }
        }

        private async void MainForm_Load(object sender, EventArgs e)
        {

        }

    }
}

