using System.Diagnostics;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace batchInput_wpf.Helper
{
    // Ghi lai nguyen nhan app bi tat: exception chua bat, lenh dong cua so, Windows dang xuat/tat may,
    // va dau hieu "bi kill tu ben ngoai" (khong de lai exception nao) qua file heartbeat.
    public static class CrashLogger
    {
        private const string Dir = @"C:\BatchInput_config";
        private static readonly string LogPath = Path.Combine(Dir, "crash.log");
        private static readonly string HeartbeatPath = Path.Combine(Dir, "heartbeat.txt");
        private static readonly object FileLock = new();
        private static System.Threading.Timer? _heartbeat;
        private static volatile bool _exiting;

        public static void Init(Application app)
        {
            try { Directory.CreateDirectory(Dir); } catch { }

            CheckPreviousRun();

            using (var p = Process.GetCurrentProcess())
            {
                Write("START", $"pid={p.Id}, session={p.SessionId}, os={Environment.OSVersion}, user={Environment.UserName}, 64bit={Environment.Is64BitProcess}");
            }

            app.DispatcherUnhandledException += (s, e) =>
                Write("UI-EXCEPTION", e.Exception.ToString());

            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                Write(e.IsTerminating ? "FATAL (app dang bi tat)" : "UNHANDLED", e.ExceptionObject?.ToString() ?? "null");

            TaskScheduler.UnobservedTaskException += (s, e) =>
            {
                Write("TASK-UNOBSERVED", e.Exception.ToString());
                e.SetObserved();
            };

            app.SessionEnding += (s, e) =>
                Write("SESSION-ENDING", $"Windows dang xuat/tat may, reason={e.ReasonSessionEnding}");

            app.Exit += (s, e) => OnCleanExit(e.ApplicationExitCode);

            AppDomain.CurrentDomain.ProcessExit += (s, e) =>
                Write("PROCESS-EXIT", "Tien trinh dang ket thuc");

            _heartbeat = new System.Threading.Timer(_ => WriteHeartbeat(), null, TimeSpan.Zero, TimeSpan.FromSeconds(30));
        }

        public static void Write(string tag, string message)
        {
            try
            {
                lock (FileLock)
                {
                    Directory.CreateDirectory(Dir);
                    var fi = new FileInfo(LogPath);
                    if (fi.Exists && fi.Length > 5 * 1024 * 1024)
                    {
                        File.Move(LogPath, Path.Combine(Dir, "crash.old.log"), true);
                    }
                    File.AppendAllText(LogPath,
                        $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] [{tag}] {message}{Environment.NewLine}",
                        Encoding.UTF8);
                }
            }
            catch
            {
                // khong de viec ghi log lam hong app
            }
        }

        // Moi 30 giay ghi 1 dong "toi con song" kem thong so tai nguyen. Neu app bi tat dot ngot
        // (bi kill, mat dien, crash native) file nay con lai va lan mo sau se bao ra.
        private static void WriteHeartbeat()
        {
            if (_exiting) return;
            try
            {
                using var p = Process.GetCurrentProcess();
                int edge = Process.GetProcessesByName("msedge").Length;
                int node = Process.GetProcessesByName("node").Length;
                string line =
                    $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}|pid={p.Id}|ws={p.WorkingSet64 / 1048576}MB|private={p.PrivateMemorySize64 / 1048576}MB|" +
                    $"handles={p.HandleCount}|threads={p.Threads.Count}|gc={GC.GetTotalMemory(false) / 1048576}MB|msedge={edge}|node={node}";
                File.WriteAllText(HeartbeatPath, line, Encoding.UTF8);
            }
            catch
            {
            }
        }

        // Chup lai noi dung cua so cua CHINH app (ve lai cay giao dien bang RenderTargetBitmap),
        // khong chup man hinh / khong dung System.Drawing. Luu vao C:\BatchInput_config\CloseShots\.
        // Chi giu SnapshotKeep anh moi nhat de khong day o dia. Tra ve duong dan file, hoac null neu that bai.
        private const int SnapshotKeep = 50;

        public static string? SaveWindowSnapshot(Window window, string tag)
        {
            try
            {
                int width = (int)window.ActualWidth;
                int height = (int)window.ActualHeight;
                if (width <= 0 || height <= 0) return null;

                var dpi = VisualTreeHelper.GetDpi(window);
                var bitmap = new RenderTargetBitmap(
                    (int)(width * dpi.DpiScaleX), (int)(height * dpi.DpiScaleY),
                    dpi.PixelsPerInchX, dpi.PixelsPerInchY, PixelFormats.Pbgra32);
                bitmap.Render(window);

                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(bitmap));

                string folder = Path.Combine(Dir, "CloseShots");
                Directory.CreateDirectory(folder);
                string path = Path.Combine(folder, $"{tag}_{DateTime.Now:yyyy-MM-dd_HH-mm-ss}.png");
                using (var stream = File.Create(path))
                {
                    encoder.Save(stream);
                }

                foreach (var old in new DirectoryInfo(folder).GetFiles("*.png")
                                                              .OrderByDescending(f => f.CreationTime)
                                                              .Skip(SnapshotKeep))
                {
                    try { old.Delete(); } catch { }
                }

                return path;
            }
            catch (Exception ex)
            {
                Write("SNAPSHOT-FAIL", ex.Message);
                return null;
            }
        }

        private static void CheckPreviousRun()
        {
            try
            {
                if (!File.Exists(HeartbeatPath)) return;
                string last = File.ReadAllText(HeartbeatPath).Trim();
                if (string.IsNullOrEmpty(last)) return;

                var m = Regex.Match(last, @"pid=(\d+)");
                if (m.Success && int.TryParse(m.Groups[1].Value, out int oldPid))
                {
                    try
                    {
                        using var old = Process.GetProcessById(oldPid);
                        using var me = Process.GetCurrentProcess();
                        if (!old.HasExited && old.ProcessName.Equals(me.ProcessName, StringComparison.OrdinalIgnoreCase))
                        {
                            Write("WARN", $"Dang co ban app khac con chay (pid={oldPid}), khong ket luan lan chay truoc.");
                            return;
                        }
                    }
                    catch (ArgumentException)
                    {
                        // tien trinh cu khong con ton tai
                    }
                }

                Write("PREVIOUS-RUN-ABNORMAL",
                    "Lan chay truoc KHONG thoat binh thuong (bi kill tu ben ngoai / crash native / mat dien / ket thuc tien trinh). " +
                    "Heartbeat cuoi: " + last);
            }
            catch
            {
            }
        }

        private static void OnCleanExit(int code)
        {
            _exiting = true;
            Write("EXIT", $"Thoat binh thuong, code={code}");
            try { _heartbeat?.Dispose(); } catch { }
            try { File.Delete(HeartbeatPath); } catch { }
        }
    }
}
