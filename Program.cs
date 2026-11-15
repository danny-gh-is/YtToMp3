using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace YtToMp3
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }

    public sealed class MainForm : Form
    {
        private readonly TextBox _urlBox = new() { Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right };
        private readonly Button _downloadBtn = new() { Text = "Download → HQ MP3", Anchor = AnchorStyles.Top | AnchorStyles.Right };
        private readonly Button _downloadVideoBtn = new() { Text = "Download → Best MP4 (video)", Anchor = AnchorStyles.Top | AnchorStyles.Right };
        private readonly Button _openDownloadsBtn = new() { Text = "Open Downloads", Anchor = AnchorStyles.Top | AnchorStyles.Right };
        private readonly TextBox _logBox = new()
        {
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            ReadOnly = true,
            Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right
        };

        public MainForm()
        {
            Text = "YouTube → HQ MP3 (yt-dlp wrapper)";
            Width = 900;
            Height = 520;

            var urlLabel = new Label { Text = "YouTube link:", AutoSize = true, Top = 18, Left = 12 };

            _urlBox.Top = 14;
            _urlBox.Left = 110;
            _urlBox.Width = 560;

            _downloadBtn.Top = 12;
            _downloadBtn.Left = 680;
            _downloadBtn.Width = 190;
            _downloadBtn.Click += async (_, __) => await DownloadAsync(video: false);

            _downloadVideoBtn.Top = 44;
            _downloadVideoBtn.Left = 680;
            _downloadVideoBtn.Width = 190;
            _downloadVideoBtn.Click += async (_, __) => await DownloadAsync(video: true);

            _openDownloadsBtn.Top = 76;
            _openDownloadsBtn.Left = 680;
            _openDownloadsBtn.Width = 190;
            _openDownloadsBtn.Click += (_, __) =>
            {
                var dir = KnownFolders.GetDownloadsPath();
                Process.Start(new ProcessStartInfo("explorer.exe", $"\"{dir}\"") { UseShellExecute = true });
            };

            _logBox.Top = 112;
            _logBox.Left = 12;
            _logBox.Width = 858;
            _logBox.Height = 348;

            Controls.Add(urlLabel);
            Controls.Add(_urlBox);
            Controls.Add(_downloadBtn);
            Controls.Add(_downloadVideoBtn);
            Controls.Add(_openDownloadsBtn);
            Controls.Add(_logBox);

            _urlBox.Text = "https://www.youtube.com/watch?v=...";
        }

        private async Task DownloadAsync(bool video)
        {
            var url = _urlBox.Text.Trim();
            if (string.IsNullOrWhiteSpace(url))
            {
                MessageBox.Show("Please paste a YouTube URL.", "Missing URL", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _downloadBtn.Enabled = false;
            _downloadVideoBtn.Enabled = false;
            _openDownloadsBtn.Enabled = false;

            try
            {
                _logBox.Clear();
                Log("Starting...");

                var downloadsDir = KnownFolders.GetDownloadsPath();
                Log($"Downloads: {downloadsDir}");

                // We use yt-dlp to do download+conversion in one shot.
                // --no-config avoids any global config surprises.
                // --no-playlist ensures only the single video.
                // -x extracts audio; --audio-format mp3 converts; --audio-quality 0 is HQ.
                // -P sets output directory; -o sets filename template.
                var args = new StringBuilder();
                args.Append("--no-config ");
                args.Append("--no-playlist ");
                if (video)
                {
                    // Best video + best audio, merged into MP4 (merging requires ffmpeg).
                    // Selector order: 1) HLS (m3u8) video+audio, which downloads at full resolution
                    // without a PO token; 2) regular DASH streams; 3) best single combined file.
                    // YouTube returns 403 for the DASH https streams when no PO token is available.
                    // The default clients are used (no player_client override) so full-resolution formats are offered.
                    args.Append("-f \"bv*[protocol^=m3u8]+ba[protocol^=m3u8]/bv*+ba/b\" ");
                    args.Append("--merge-output-format mp4 ");
                    args.Append($"-P \"{downloadsDir}\" ");
                    args.Append("-o \"%(title)s.%(ext)s\" ");
                    // Report what was actually saved (--print implies --quiet, so re-enable normal output).
                    args.Append("--no-quiet ");
                    args.Append("--print \"after_move:Saved: %(filepath)s [%(width)sx%(height)s]\" ");
                }
                else
                {
                    args.Append("--extractor-args \"youtube:player_client=android\" ");
                    args.Append("-x ");
                    args.Append("--audio-format mp3 ");
                    args.Append("--audio-quality 0 ");
                    args.Append($"-P \"{downloadsDir}\" ");
                    args.Append("-o \"%(title)s (HQ).%(ext)s\" ");
                }
                args.Append($"\"{url}\"");

                var exitCode = await RunProcessAsync(
                    fileName: "yt-dlp",
                    arguments: args.ToString(),
                    onLine: Log
                );

                if (exitCode == 0)
                {
                    var kind = video ? "MP4" : "MP3";
                    Log($"Done ✅ {kind} should be in Downloads.");
                    MessageBox.Show($"Finished. {kind} saved in Downloads.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    Log($"Failed (exit code {exitCode}). Check log above.");
                    MessageBox.Show("Download/conversion failed. See log.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch (Exception ex)
            {
                Log("ERROR: " + ex.Message);
                MessageBox.Show(ex.Message, "Exception", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                _downloadBtn.Enabled = true;
                _downloadVideoBtn.Enabled = true;
                _openDownloadsBtn.Enabled = true;
            }
        }

        private void Log(string line)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action<string>(Log), line);
                return;
            }
            _logBox.AppendText(line + Environment.NewLine);
        }

        private static async Task<int> RunProcessAsync(string fileName, string arguments, Action<string> onLine)
        {
            var psi = new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var p = new Process { StartInfo = psi, EnableRaisingEvents = true };

            var tcs = new TaskCompletionSource<int>();

            p.OutputDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) onLine(e.Data); };
            p.ErrorDataReceived += (_, e) => { if (!string.IsNullOrWhiteSpace(e.Data)) onLine(e.Data); };

            if (!p.Start())
                throw new InvalidOperationException($"Failed to start process: {fileName}");

            p.BeginOutputReadLine();
            p.BeginErrorReadLine();

            await Task.Run(() =>
            {
                p.WaitForExit();
                tcs.TrySetResult(p.ExitCode);
            });

            return await tcs.Task;
        }
    }

    internal static class KnownFolders
    {
        // Downloads known folder GUID
        private static readonly Guid DownloadsFolderId = new("374DE290-123F-4565-9164-39C4925E467B");

        public static string GetDownloadsPath()
        {
            var hr = SHGetKnownFolderPath(DownloadsFolderId, 0, IntPtr.Zero, out var pPath);
            if (hr != 0 || pPath == IntPtr.Zero)
                throw new InvalidOperationException("Unable to resolve Downloads folder path.");

            try
            {
                return Marshal.PtrToStringUni(pPath) ?? throw new InvalidOperationException("Downloads path was null.");
            }
            finally
            {
                Marshal.FreeCoTaskMem(pPath);
            }
        }

        [DllImport("shell32.dll")]
        private static extern int SHGetKnownFolderPath(
            [MarshalAs(UnmanagedType.LPStruct)] Guid rfid,
            uint dwFlags,
            IntPtr hToken,
            out IntPtr ppszPath);
    }
}