using batchInput_wpf.Helper;
using batchInput_wpf.Model;
using batchInput_wpf.Service;
using Microsoft.Playwright;
using OpenTK.Graphics.OpenGL;
using System.Collections.Concurrent;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using static MaterialDesignThemes.Wpf.Theme.ToolBar;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace batchInput_wpf.ViewsModel
{
    public class MainViewModel : BaseViewModel
    {
        private readonly BatchAPIService _service;
        public ObservableCollection<ListPO> ListPos { get; set; } = new ObservableCollection<ListPO>();

        #region Phan trang (Pagination)
        // Kich thuoc trang gui cho API - chi tai dung so luong nay moi lan, khong GetAll toan bo bang
        private const int PageSize = 10;

        private readonly SemaphoreSlim _pageLoadLock = new SemaphoreSlim(1, 1);

        private ObservableCollection<BatchInputHistory> _pagedBatchInputHistories = new ObservableCollection<BatchInputHistory>();
        public ObservableCollection<BatchInputHistory> PagedBatchInputHistories
        {
            get => _pagedBatchInputHistories;
            private set
            {
                _pagedBatchInputHistories = value;
                OnPropertyChanged();
            }
        }

        private int _currentPage = 1;
        public int CurrentPage
        {
            get => _currentPage;
            private set
            {
                if (_currentPage == value) return;
                _currentPage = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoPreviousPage));
                OnPropertyChanged(nameof(CanGoNextPage));
            }
        }

        private int _totalPages = 1;
        public int TotalPages
        {
            get => _totalPages;
            private set
            {
                if (_totalPages == value) return;
                _totalPages = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CanGoPreviousPage));
                OnPropertyChanged(nameof(CanGoNextPage));
            }
        }

        private int _totalItems = 0;
        public int TotalItems
        {
            get => _totalItems;
            private set
            {
                if (_totalItems == value) return;
                _totalItems = value;
                OnPropertyChanged();
            }
        }

        public bool CanGoPreviousPage => CurrentPage > 1;
        public bool CanGoNextPage => CurrentPage < TotalPages;

        // Goi API lay dung 1 trang (pageNumber, pageSize) thay vi keo toan bo bang ve moi lan,
        // tranh hao ton tai nguyen va giam rui ro voi bang du lieu lon (big data).
        private async Task RefreshCurrentPageAsync()
        {
            await _pageLoadLock.WaitAsync();
            try
            {
                var page = await _service.GetAllProductAsync(CurrentPage, PageSize);

                App.Current.Dispatcher.Invoke(() =>
                {
                    PagedBatchInputHistories.Clear();
                    foreach (var item in page.Items)
                    {
                        // Luong khac co the dang chay item nay: gan lai nhan trang thai sau khi lam moi luoi
                        if (_statusById.TryGetValue(item.ID, out var status)) item.Status = status;
                        PagedBatchInputHistories.Add(item);
                    }

                    TotalItems = page.TotalItems;
                    TotalPages = Math.Max(1, page.TotalPages);

                    if (CurrentPage > TotalPages)
                    {
                        CurrentPage = TotalPages;
                    }
                });
            }
            finally
            {
                _pageLoadLock.Release();
            }
        }

        public async Task GoToFirstPage()
        {
            CurrentPage = 1;
            await RefreshCurrentPageAsync();
        }

        public async Task GoToPreviousPage()
        {
            if (CurrentPage <= 1) return;
            CurrentPage--;
            await RefreshCurrentPageAsync();
        }

        public async Task GoToNextPage()
        {
            if (CurrentPage >= TotalPages) return;
            CurrentPage++;
            await RefreshCurrentPageAsync();
        }

        public async Task GoToLastPage()
        {
            CurrentPage = TotalPages;
            await RefreshCurrentPageAsync();
        }
        #endregion

        #region Thong ke phien lam viec (Session stats)
        // Dem theo ID item (ket qua cuoi cung trong phien): item loi roi chay lai thanh cong se tinh la thanh cong.
        // Khong dem tu ItemStatus tren luoi vi cac item tren luoi bi tao moi moi lan lam moi trang.
        private readonly ConcurrentDictionary<int, bool> _sessionResults = new();

        public int SessionSuccess => _sessionResults.Count(r => r.Value);
        public int SessionError => _sessionResults.Count(r => !r.Value);
        // Dang chay / dang doi lay truc tiep tu hang doi chung (xem vung "Hang doi chung")
        public int SessionRunning { get { lock (_queueLock) { return _runningIds.Count; } } }
        public int SessionWaiting { get { lock (_queueLock) { return _queue.Count; } } }
        public string SessionSuccessRate
        {
            get
            {
                int done = SessionSuccess + SessionError;
                return done == 0 ? "--" : $"{SessionSuccess * 100 / done}%";
            }
        }

        private void NotifySessionStats()
        {
            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                OnPropertyChanged(nameof(SessionSuccess));
                OnPropertyChanged(nameof(SessionError));
                OnPropertyChanged(nameof(SessionRunning));
                OnPropertyChanged(nameof(SessionWaiting));
                OnPropertyChanged(nameof(SessionSuccessRate));
            }));
        }
        #endregion

        public ItemSaveConfig _itemSaveConfig;
        private FileHelper _myFileHelper = new FileHelper();


        public MainViewModel()
        {
            _service = new BatchAPIService((msg, status) => AddLog(msg, status));
            _itemSaveConfig = _myFileHelper.ReadJson();
            _ = LoadBatchInput();
        }

        public async Task InitializeAsync()
        {
            //LoadChart();
            //await LoadBatchInput();
            //Debug.WriteLine("INITIALIZE VM: " + GetHashCode());
            //await Task.CompletedTask;
        }

        #region Hang doi chung & cac luong chay (Work queue)
        // Y tuong: cac luong KHONG cho nhau. Moi luong tu lay item ke tiep trong hang doi chung;
        // hang doi het thi 1 luong (co khoa) goi API lam moi, cac luong khac van chay tiep roi dung ket qua do.

        // Chay du so item nay thi ket thuc luot de thay browser moi (tranh phinh RAM)
        private const int MaxItemsPerBrowser = 30;

        private readonly object _queueLock = new();
        private readonly List<BatchInputHistory> _queue = new();                 // item dang cho
        private readonly HashSet<int> _runningIds = new();                       // item dang chay (de khong nhan trung)
        private readonly HashSet<string> _runningTerminals = new(StringComparer.OrdinalIgnoreCase); // terminal dang chay
        private readonly HashSet<int> _doneThisCycle = new();                    // item da xu ly trong luot nay
        private readonly SemaphoreSlim _refillLock = new(1, 1);                  // moi luc chi 1 luong goi API lam moi
        private readonly ConcurrentDictionary<int, ItemStatus> _statusById = new(); // nhan trang thai theo ID, khong mat khi luoi bi tao lai
        private int _itemsTakenThisCycle;
        private bool _continueImmediately;                                       // luot ket thuc nhung con viec -> chay luot ke tiep ngay

        // Thoi gian cho (giay) khi luong ranh ma luong khac van dang chay
        private int IdleWaitSeconds => Math.Clamp(_itemSaveConfig.PollDelaySeconds, 5, 60);

        // Chi goi trong lock(_queueLock): terminal cua item nay dang duoc luong khac dung?
        private bool IsBlockedByTerminal(BatchInputHistory item) =>
            !string.IsNullOrEmpty(item.TerminalID) && _runningTerminals.Contains(item.TerminalID);

        // Dong bo giao dien: luu trang thai theo ID, roi tim dong HIEN TAI tren luoi de cap nhat.
        // Khong giu tham chieu dong cu nen luong khac lam moi luoi cung khong lam mat nhan.
        private void SetItemStatus(int id, ItemStatus status)
        {
            _statusById[id] = status;
            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                var row = PagedBatchInputHistories.FirstOrDefault(x => x.ID == id);
                if (row != null) row.Status = status;
            }));
        }

        private bool HasRunnableItem()
        {
            lock (_queueLock)
            {
                return _queue.Any(i => !IsBlockedByTerminal(i));
            }
        }

        // Lay danh sach moi tu API va GOP vao hang doi (bo qua item dang chay / da co trong hang doi / da xu ly trong luot nay).
        // Tra ve true neu co item chay duoc.
        private async Task<bool> RefillQueueAsync()
        {
            await _refillLock.WaitAsync();
            try
            {
                // Luong khac vua lam moi xong thi dung ket qua cua no, khong goi API them
                if (HasRunnableItem()) return true;

                var list = await _service.GetProduct_notComplate_Async();

                lock (_queueLock)
                {
                    foreach (var item in list)
                    {
                        if (_runningIds.Contains(item.ID) || _doneThisCycle.Contains(item.ID) || _queue.Any(q => q.ID == item.ID)) continue;
                        _queue.Add(item);
                    }
                }
                NotifySessionStats();

                return HasRunnableItem();
            }
            finally
            {
                _refillLock.Release();
            }
        }

        // Lay item ke tiep de chay. Tra ve null khi: het viec (khong con luong nao dang chay), dang Pause,
        // hoac da chay du so item cua 1 browser.
        private async Task<BatchInputHistory?> TakeNextItemAsync()
        {
            while (IsRunning)
            {
                BatchInputHistory? next = null;
                bool limitReached = false;

                lock (_queueLock)
                {
                    if (_itemsTakenThisCycle >= MaxItemsPerBrowser)
                    {
                        limitReached = true;
                        _continueImmediately = true;
                    }
                    else
                    {
                        // Item dau tien chua bi chan boi terminal dang chay
                        next = _queue.FirstOrDefault(i => !IsBlockedByTerminal(i));
                        if (next != null)
                        {
                            _queue.Remove(next);
                            _runningIds.Add(next.ID);
                            if (!string.IsNullOrEmpty(next.TerminalID)) _runningTerminals.Add(next.TerminalID);
                            _itemsTakenThisCycle++;
                        }
                    }
                }

                if (limitReached) return null;
                if (next != null)
                {
                    NotifySessionStats();
                    return next;
                }

                // Hang doi het: lam moi (1 luong lam, luong khac cho roi dung ket qua)
                if (await RefillQueueAsync()) continue;

                // Khong co gi moi. Neu khong con luong nao dang chay thi het viec -> ket thuc luot (dong browser)
                bool someoneRunning;
                lock (_queueLock)
                {
                    someoneRunning = _runningIds.Count > 0;
                }
                if (!someoneRunning) return null;

                // Con luong khac dang chay: cho 1 luc roi hoi lai (de nhan item moi, hoac item het bi chan do trung terminal)
                await Task.Delay(IdleWaitSeconds * 1000);
            }

            return null;
        }

        private void ReleaseItem(BatchInputHistory item)
        {
            lock (_queueLock)
            {
                _runningIds.Remove(item.ID);
                if (!string.IsNullOrEmpty(item.TerminalID)) _runningTerminals.Remove(item.TerminalID);
                _doneThisCycle.Add(item.ID);
            }
            NotifySessionStats();
        }

        // Vong lap cua 1 luong: lay item -> chay -> lam moi giao dien -> lay item tiep. Khong cho luong nao khac.
        private async Task WorkerLoopAsync(IBrowser browser)
        {
            while (IsRunning)
            {
                if (!browser.IsConnected)
                {
                    AddLog("Trình duyệt đã bị đóng hoặc chết, kết thúc lượt này để mở trình duyệt mới.", LogStatus.Error);
                    _continueImmediately = true;
                    break;
                }

                var item = await TakeNextItemAsync();
                if (item == null) break;

                try
                {
                    await ProcessItemAsync(browser, item);
                }
                catch (Exception ex)
                {
                    AddLog($"ID:{item.ID} Lỗi không mong muốn khi chạy item: {ex.Message}", LogStatus.Error);
                    _sessionResults[item.ID] = false;
                    SetItemStatus(item.ID, ItemStatus.Error);
                }
                finally
                {
                    ReleaseItem(item);
                }

                // Dong bo giao dien: tai lai trang dang xem de thay du lieu moi nhat (mo ta, so lan chay...)
                try
                {
                    await RefreshCurrentPageAsync();
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi làm mới lưới: " + ex.Message);
                }
            }
        }

        // Chay 1 item (kem chay lai khi gap "Loi khong xac dinh").
        private async Task ProcessItemAsync(IBrowser browser, BatchInputHistory item)
        {
            // So lan chay LAI toi da khi gap "Loi khong xac dinh" (tong so lan chay = 1 + MaxRetry)
            const int MaxRetry = 3;
            bool result_run = false;
            int retryCount = 0;
            RunContext runContext;
            do
            {
                // Moi lan chay dung context moi de co loi duoc reset
                runContext = new RunContext();

                SetItemStatus(item.ID, ItemStatus.Running);
                AddLog("Bắt đầu AutoClick BatchInput item: " + item.ID);

                // Danh dau "toi dang chay item nay" ngay lap tuc
                await _service.update_numRetry(item.ID);
                await _service.update_runTime(item.ID);

                result_run = await event_auto_web(browser, item, runContext);
                RenameItemVideos(item.ID);

                SetItemStatus(item.ID, result_run ? ItemStatus.Success : ItemStatus.Error);

                // Chi chay lai khi batch that bai VA co PO "Loi khong xac dinh";
                // batch da thanh cong thi khong chay lai de tranh nhap trung.
            } while (!result_run && runContext.HasUnknownError && ++retryCount <= MaxRetry);

            _sessionResults[item.ID] = result_run;
            NotifySessionStats();

            Debug.WriteLine($"Item {item.ID} retry: {Math.Min(retryCount, MaxRetry)}/{MaxRetry}");

            // Ghi log loi neu da chay lai du so lan ma van con "Loi khong xac dinh"
            if (!result_run && runContext.HasUnknownError)
            {
                AddLog($"ID:{item.ID} Đã chạy lại {MaxRetry} lần nhưng vẫn còn PO 'Loi khong xac dinh'", LogStatus.Error);
                string parse_json = JsonSerializer.Serialize(item);
                string fileName = $"{item.ID}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}_err.json";
                string filePath = Path.Combine(_itemSaveConfig.pathLogErr, fileName);

                if (!Directory.Exists(_itemSaveConfig.pathLogErr))
                {
                    Directory.CreateDirectory(_itemSaveConfig.pathLogErr);
                }

                await File.WriteAllTextAsync(filePath, parse_json);
            }
        }

        // Chay 1 "luot": mo 1 browser, cac luong cung lay viec tu hang doi cho den khi het viec.
        // Tra ve true neu luot bi ket thuc de thay browser trong luc van con viec (chay luot ke tiep ngay).
        private async Task<bool> RunCycleAsync()
        {
            lock (_queueLock)
            {
                _queue.Clear();
                _doneThisCycle.Clear();
                _itemsTakenThisCycle = 0;
                _continueImmediately = false;
            }

            // Khong co viec thi khong mo browser
            if (!await RefillQueueAsync())
            {
                NotifySessionStats();
                return false;
            }

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Channel = "msedge",
                Headless = _itemSaveConfig.HeadlessMode,//lay trong file config gan vao
                Args = new[]
                {
                    "--disable-blink-features=AutomationControlled", // Tắt cờ báo Automation
                    "--no-sandbox",
                    "--disable-setuid-sandbox",
                    "--disable-gpu",
                    "--disable-extensions",
                    "--mute-audio",
                    "--disable-background-timer-throttling",
                    "--disable-backgrounding-occluded-windows",
                    "--disable-renderer-backgrounding"
                }
            });

            // So luong chay song song lay tu cau hinh (nut Config)
            int threads = Math.Max(1, _itemSaveConfig.MaxThreads);
            await Task.WhenAll(Enumerable.Range(0, threads).Select(_ => WorkerLoopAsync(browser)));

            lock (_queueLock)
            {
                _queue.Clear();
            }
            NotifySessionStats();

            return _continueImmediately;
        }
        #endregion

        private async Task LoadBatchInput()
        {
            _itemSaveConfig = _myFileHelper.ReadJson();
            while (true)
            {
                bool continueNow = false;
                try
                {
                    // Lam moi luoi danh sach (dau moi luot)
                    await RefreshCurrentPageAsync();

                    if (IsRunning)
                    {
                        continueNow = await RunCycleAsync();
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi trong vòng lặp BatchInput: " + ex.Message);
                    AddLog("Lỗi trong vòng lặp BatchInput: " + ex.Message, LogStatus.Error);
                }

                // Luot bi cat de thay browser nhung con viec: chay luot ke tiep sau it giay
                if (continueNow)
                {
                    await Task.Delay(3000);
                    continue;
                }

                Console.WriteLine("length: " + TotalItems);
                await Task.Delay(_itemSaveConfig.PollDelaySeconds * 1000);
                AddLog("Reload List", LogStatus.Running);
            }
        }

        //log 
        private ObservableCollection<LogItem> _log = new ObservableCollection<LogItem>();
        public ObservableCollection<LogItem> Log
        {
            get => _log;
            set
            {
                _log = value;
                OnPropertyChanged();
            }
        }

        public LogItem AddLog(string message, LogStatus status = LogStatus.Running)
        {
            var item = new LogItem()
            {
                Message = message,
                Status = status
            };

            // 1. Cập nhật UI Collection trên UI Thread (Bất đồng bộ)
            App.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                Log.Insert(0, item);

                if (Log.Count >= 100)
                {
                    Log.RemoveAt(Log.Count - 1); // Xóa phần tử cũ nhất ở cuối danh sách
                }
            }));

            // 2. Ghi File Log chạy ngầm (Background Thread) - Nối chuỗi vào cuối file để tối ưu
            Task.Run(async () =>
            {
                try
                {
                    string logFolder = Path.Combine(_itemSaveConfig.pathSaveImg, "LOG");
                    string logFilePath = Path.Combine(logFolder, $"log-{DateTime.Now.ToString("yyyy-MM-dd")}.txt");

                    Directory.CreateDirectory(logFolder);

                    string messageLog = $"[{DateTime.Now:HH:mm:ss}] - {message}{Environment.NewLine}";

                    // Append chuỗi mới vào cuối file (Nhanh gấp hàng trăm lần việc ReadAllLines rồi ghi đè)
                    await File.AppendAllTextAsync(logFilePath, messageLog, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine("Lỗi ghi file log: " + ex.Message);
                }
            });

            // Trả về item ngay lập tức cho hàm gọi
            return item;
        }

        #region quay video tung batch
        private string GetVideoDir(int id) => Path.Combine(_itemSaveConfig.pathSaveImg ?? "", "Video", id.ToString());

        private const string HideWebdriverScript = @"
            Object.defineProperty(navigator, 'webdriver', {
                get: () => undefined
            });";

        // Moi item 1 context + 1 trang rieng; khi bat RecordVideo se quay video vao {pathSaveImg}\Video\{ID}.
        // Thieu ffmpeg cua Playwright thi loi nem ra o buoc tao trang (khong phai luc tao context),
        // nen bat o day va chay tiep KHONG quay video thay vi de item that bai.
        private async Task<(IBrowserContext context, IPage page)> CreateItemContextAndPageAsync(IBrowser browser, BatchInputHistory itemBatch)
        {
            if (_itemSaveConfig.RecordVideo)
            {
                IBrowserContext? videoContext = null;
                try
                {
                    videoContext = await browser.NewContextAsync(new BrowserNewContextOptions
                    {
                        RecordVideoDir = GetVideoDir(itemBatch.ID),
                        RecordVideoSize = new RecordVideoSize { Width = 1024, Height = 576 }
                    });
                    await videoContext.AddInitScriptAsync(HideWebdriverScript);
                    var videoPage = await videoContext.NewPageAsync();
                    return (videoContext, videoPage);
                }
                catch (PlaywrightException ex) when (ex.Message.Contains("ffmpeg", StringComparison.OrdinalIgnoreCase))
                {
                    AddLog($"ID:{itemBatch.ID} Không quay được video: thiếu ffmpeg của Playwright, chạy tiếp không có video. Cài bằng lệnh: playwright install ffmpeg", LogStatus.Error);
                    if (videoContext != null)
                    {
                        try { await videoContext.CloseAsync(); } catch { }
                    }
                }
            }

            var context = await browser.NewContextAsync();
            await context.AddInitScriptAsync(HideWebdriverScript);
            var page = await context.NewPageAsync();
            return (context, page);
        }

        // Video chi hoan tat khi context dong -> goi sau khi event_auto_web tra ve. Doi ten theo ID + gio de de tim.
        private void RenameItemVideos(int id)
        {
            try
            {
                if (!_itemSaveConfig.RecordVideo) return;

                string dir = GetVideoDir(id);
                if (!Directory.Exists(dir)) return;

                foreach (string file in Directory.GetFiles(dir, "*.webm"))
                {
                    if (Path.GetFileName(file).StartsWith(id + "_")) continue;

                    string target = Path.Combine(dir, $"{id}_{File.GetLastWriteTime(file):yyyy-MM-dd-HH-mm-ss}.webm");
                    if (File.Exists(target)) continue;
                    File.Move(file, target);
                }
            }
            catch (Exception ex)
            {
                AddLog($"ID:{id} Không đổi tên được video: {ex.Message}", LogStatus.Error);
            }
        }
        #endregion

        #region bat dau autoclick
        private async Task<bool> event_auto_web(IBrowser browser, BatchInputHistory itemBatch, RunContext itemContext)
        {
            try
            {
                // using/await using: dam bao driver Playwright, browser va context luon duoc
                // dong khi ham ket thuc (ke ca return som hay nem exception), tranh leak
                // tien trinh msedge.exe khi app chay lien tuc trong thoi gian dai.

                var (itemBrowserContext, page) = await CreateItemContextAndPageAsync(browser, itemBatch);
                await using var context = itemBrowserContext;

                await page.GotoAsync("http://10.4.24.117:8449/LG0100.aspx");
                if ((await page.TitleAsync()).Contains("Daily Report Login"))
                {
                    await _service.update_runTime(itemBatch.ID);
                    await Task.Delay(3000);
                    try
                    {
                        if (await AT_Login_terminalID(page, itemBatch.TerminalID, itemBatch) == true)
                        {
                            await Task.Delay(3000);
                            if (await AT_Operator_Shift(page, itemBatch))
                            {
                                await Task.Delay(3000);
                                if (await AT_ScanPO_own(page, itemBatch))
                                {
                                    bool checkReload = false;
                                    //kiem tra co can chay lai khong 
                                    if (itemBatch.numRetry > 1)
                                    {
                                        checkReload = await AT_Check_Current_PO(page, itemBatch, itemContext);
                                    }

                                    if (checkReload)
                                    {
                                        return await AT_end_PO(page, itemBatch, itemContext);
                                    }
                                    else
                                    {
                                        await Task.Delay(3000);
                                        bool IscheckList = await AT_Click_list_PO(page, itemBatch.ListPO, itemBatch, itemContext);
                                        AddLog($"ID:{itemBatch.ID} [DIAG] AT_Click_list_PO trả về {IscheckList}, PoCount={itemContext.PoCount}/{itemBatch.ListPO.Count}", IscheckList ? LogStatus.Running : LogStatus.Error);
                                        await Task.Delay(3000);



                                        //cho truong hoop start
                                        if (IscheckList && itemBatch.start)
                                        {
                                            await Task.Delay(3000);
                                            if (await AT_Click_start(page, itemBatch))
                                            {
                                                await Task.Delay(3000);
                                                if (await AT_Click_startAll(page, itemBatch,itemContext))
                                                {
                                                    AddLog($"ID:{itemBatch.ID} Hoàn thành Start item: " + itemBatch.ID, LogStatus.Success);

                                                    return await AT_end_PO(page, itemBatch, itemContext);
                                                }
                                                else
                                                {
                                                    await ScreenShot(page, itemBatch);
                                                    await page.CloseAsync();
                                                    AddLog($"ID:{itemBatch.ID} Đã có lỗi khi chạy start All ", LogStatus.Error);
                                                    await _service.update_des(itemBatch.ID, "Đã có lỗi tại chạy START ALL.");
                                                }
                                            }
                                            else
                                            {
                                                await ScreenShot(page, itemBatch);
                                                AddLog($"ID:{itemBatch.ID} Không vào được nơi start ", LogStatus.Error);
                                                await _service.update_des(itemBatch.ID, "Đã có lỗi tại thao tác vào START ALL.");
                                            }
                                        }
                                        //truong hop finish
                                        else if (IscheckList && !itemBatch.start)
                                        {
                                            if (await AT_Click_Finish(page, itemBatch))
                                            {
                                                await Deletefolder(itemBatch.ID.ToString());
                                                return await AT_end_PO(page, itemBatch, itemContext);
                                            }
                                        }
                                        else
                                        {
                                            AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập list PO ", LogStatus.Error);
                                            await ScreenShot(page, itemBatch);
                                            await _service.update_des(itemBatch.ID, "Đã có lỗi tại nhập List PO.");
                                            await page.CloseAsync();
                                            //AT_end_PO(wait, driver);
                                        }
                                    }




                                }
                                else
                                {
                                    AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập PO đại diện bước 3", LogStatus.Error);
                                    await _service.update_des(itemBatch.ID, "Đã có lỗi tại nhập PO đại diện.");
                                    await ScreenShot(page, itemBatch);
                                    await page.CloseAsync();
                                }
                            }
                            else
                            {
                                AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập Operator ID bước 2", LogStatus.Error);
                                await ScreenShot(page, itemBatch);
                                await page.CloseAsync();
                                await _service.update_des(itemBatch.ID, "Đã có lỗi tại nhập Operator & Shift");

                            }
                        }
                        else
                        {
                            AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập Terminal ID bước 1", LogStatus.Error);
                            await _service.update_des(itemBatch.ID, "Đã có lỗi tại nhập Terminal ID bước 1");
                            await ScreenShot(page, itemBatch);
                            await page.CloseAsync();
                        }
                    }
                    catch (Exception ex)
                    {
                        await ScreenShot(page, itemBatch);
                        MessageBox.Show("Đã có lỗi: " + ex);
                    }
                }
                else
                {
                    AddLog("Lần này chưa tìm thấy trang đăng nhập");
                }

                //}
                return false;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Đã có lỗi xảy ra :" + ex);
                return false;
            }

        }

        private async Task<bool> AT_Click_Finish(IPage page, BatchInputHistory itemBatch)
        {
            try
            {
                AddLog($"ID:{itemBatch.ID} Tiến hành Finish All PO ", LogStatus.Success);
                await page.ClickAsync("#lstBatchSub_selButton_1");
                await page.WaitForFunctionAsync(
                @"() => {
                        const btn = document.getElementById('btnAreaFinishAll');
                        return btn && !btn.disabled;
                    }");
                await page.ClickAsync("#btnAreaFinishAll");
                await Task.Delay(3000);
                await page.ClickAsync("#btnBatchFinish");

                //kiem tra ket qua sau khi xoa 
                await page.WaitForFunctionAsync(
                    "() => document.getElementById('txtMsg').innerText.trim() !== ''");

                int numcheck = 0;
                while (numcheck <= 3)
                {

                    ////lay thoi gian finish 
                    //string txt_finish_tb = await page.Locator("#lstBatchSub_txtAreaFinishYmd_1").InnerTextAsync();
                    // 1. Lấy toàn bộ Text trong thẻ #txtMsg
                    string rawText = await page.Locator("#txtMsg").InnerTextAsync();

                    // 2. Định dạng lại văn bản (thay thế chuỗi xuống dòng nếu cần)
                    string messageInputPO = rawText.Trim();
                    //await page.ClickAsync("#btnBatchFinish");
                    if (messageInputPO.Contains("finished"))
                    {
                        AddLog($"ID:{itemBatch.ID} Hoàn thành Finish All PO ", LogStatus.Success);
                        return true;
                    }
                    else
                    {
                        //nhan lai
                        await page.ClickAsync("#btnBatchFinish");
                    }
                    numcheck++;

                    await Task.Delay(20000);
                }

                await _service.update_des(itemBatch.ID, "Chưa hoàn thành Finish All PO: đầy đủ thao tác nhưng bị treo");
                AddLog($"ID:{itemBatch.ID} Chưa hoàn thành Finish All PO: Đầy đủ thao tác nhưng bị treo  ", LogStatus.Success);
                return true;


            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã xảy ra lỗi trong quá trình vào nhập Operator: {ex.Message}", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Click_startAll(IPage page, BatchInputHistory itemBatch,RunContext runContext)
        {
            try
            {
                AddLog($"ID:{itemBatch.ID} Tiến hành start All");
                await page.ClickAsync("#MyVersion");
                AddLog($"ID:{itemBatch.ID} Tiến hành nhập Operator");
                await page.Keyboard.TypeAsync(itemBatch.Msnv);
                await page.Keyboard.PressAsync("Enter");
                await Task.Delay(5000);
                //string messageInputPO = await page.Locator("#txtMsg").InnerTextAsync();

                // 1. lấy full message
                string rawText = await page.Locator("#txtMsg").InnerTextAsync();

                // 2. định dạng lại 
                string messageInputPO = rawText.Trim();


                // 1. lấy title 
                string rawTitle = await page.Locator(".header").InnerTextAsync();

                // 2. định dạng lại title
                string messageInputPOTitle = rawTitle.Trim();

                if (messageInputPO.Contains("Start or Finish or Apply"))
                {
                    await page.ClickAsync("#btnCancel");
                    await Task.Delay(2000);
                }

                //await page.Locator("#btnAreaStartAll").WaitForAsync(new()
                //{
                //    State = WaitForSelectorState.Visible
                //});

                //await page.ClickAsync("#btnAreaStartAll");


                //await page.WaitForFunctionAsync(
                //"() => document.title.includes('Daily Report work input batch')");


                int numcheck = 0;
                while (numcheck <= 3)
                {
                    // 1. lấy title 
                    rawTitle = await page.Locator(".header").InnerTextAsync();

                    // 2. định dạng lại title
                    messageInputPOTitle = rawTitle.Trim();
                    //if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                    if (messageInputPOTitle.Contains("daily report work time input batch"))
                    {
                        //        await page.WaitForFunctionAsync(
                        //"() => document.title.includes('Daily Report work input batch')");
                        AddLog($"ID:{itemBatch.ID} Hoàn thành Start ", LogStatus.Success);
                        await _service.update_desPO(itemBatch.ID, $"START DONE");
                        return true;
                    }
                    else
                    {
                        await page.ClickAsync("#btnAreaStartAll");

                        if (numcheck > 1)
                        {
                            await page.ClickAsync("#btnCancel");
                        }
                    }
                    numcheck++;
                    await Task.Delay(10000);
                }
                await _service.update_desPO(itemBatch.ID, $" Đã xảy ra lỗi trong quá trình vào nhập Operator Start All");
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã xảy ra lỗi trong quá trình nhập Operator: " + ex.Message, LogStatus.Error);
                await _service.update_desPO(itemBatch.ID, $" Đã xảy ra lỗi trong quá trình vào nhập Operator:{ex.Message}");
                await ScreenShot(page, itemBatch);
                runContext.HasUnknownError = true;
                //await page.CloseAsync();
                return false;
            }
        }

        private async Task<bool> AT_Click_start(IPage page, BatchInputHistory itemBatch)
        {
            try
            {
                AddLog($"ID:{itemBatch.ID} Tiến hành vào input Operator ");
                await page.ClickAsync("#lstBatchSub_selButton_1");
                await page.WaitForFunctionAsync(
                @"() => {
                        const btn = document.getElementById('btnInputOperatorList');
                        return btn && !btn.disabled;
                    }");

                await page.ClickAsync("#btnInputOperatorList");

                await page.WaitForFunctionAsync(
                    "() => document.title.includes('Daily Report work input Operator List')");

                if ((await page.TitleAsync()).Contains("Daily Report work input Operator List"))
                {
                    AddLog($"ID:{itemBatch.ID} Hoàn thành vào input Operator ", LogStatus.Success);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã xảy ra lỗi trong quá trình vào nhập Operator", LogStatus.Error);
                return false;
            }
        }

        // kiểm tra nó đã được start từ trước chưa 
        private async Task<bool> AT_Check_Current_PO(IPage page, BatchInputHistory item, RunContext runcontext)
        {
            try
            {
                await Task.Delay(10000);
                string startTime = (await page.Locator("#lstBatchSub_txtAreaStartYmd_1").InputValueAsync()).Trim();
                int.TryParse((await page.Locator("#txtPoRecCount").InputValueAsync()).Trim(), out int poRec);
                int.TryParse((await page.Locator("#txtQtyUnRegisterdCount").InputValueAsync()).Trim(), out int unRegist);
                string header = await page.Locator(".header").First.InnerTextAsync();

                AddLog($"ID:{item.ID} [DIAG] Check hiện tại: startTime=\"{startTime}\", PO={poRec}/{item.ListPO.Count}, chưa đăng ký={unRegist}, header=\"{header.Trim()}\"");

                if (item.start && header.Contains("daily report work time input batch") && !string.IsNullOrWhiteSpace(startTime)
                    && poRec == item.ListPO.Count && unRegist == 0)
                {
                    AddLog($"ID:{item.ID} ĐÃ ĐƯỢC START TRƯỚC ĐÓ KHÔNG CẦN CHẠY LẠI ", LogStatus.Success);
                    runcontext.PoCount = poRec;
                    return true;
                }
            }
            catch (Exception ex)
            {
                AddLog($"ID:{item.ID} Không kiểm tra được trạng thái PO hiện tại: {ex.Message}", LogStatus.Error);
            }
            return false;
        }

        private async Task<bool> AT_Click_list_PO(IPage page, List<ListPO> listPo, BatchInputHistory item, RunContext runContext)
        {
            // Ghi lai dang o buoc nao de log loi noi ro cho bi loi (truoc day chi co "Timeout 30000ms exceeded")
            string step = "bắt đầu";
            try
            {
                step = "mở trang nhập PO list";
                await page.ClickAsync("#btnInputPOList");

                if (await WaitForTitleContainsAsync(page, "Daily Report work input po list", 30000))
                {
                    AddLog($"ID:{item.ID} Đã vào trang nhập PO list");
                    await page.ClickAsync("#MyVersion");

                    AddLog($"ID:{item.ID} Tiến hành nhập PO list");

                    await page.Locator("#lstBatchPOList_itemPlaceholderContainer")
                              .WaitForAsync();

                    //quet cac PO vao
                    if (item.start)
                    {
                        //lấy các PO đang có sẵn 
                        var poList = await page.EvaluateAsync<string[]>(@"
                                        () => {
                                            // lấy tất cả các PO trong bảng dữ liệu
                                            const rows = document.querySelectorAll('tr.inlayout.coltitle');
                                            const results = [];

                                            rows.forEach((row, index) => {
                                                const plantID = row.querySelector('[id*=""txtPlantID""]')?.value || '';
                                                const po = row.querySelector('[id*=""txtPO""]')?.value || '';
                                                const processID = row.querySelector('[id*=""txtProcessID""]')?.value || '';
                                                const stepID = row.querySelector('[id*=""txtStepID""]')?.value || '';
                                                const itemText = row.querySelector('[id*=""txtItemText""]')?.value || '';
                                                const qty = row.querySelector('[id*=""txtQty""]')?.value || '';
                                                const qtyAct = row.querySelector('[id*=""txtQtyAct""]')?.value || '';
                                                const total = row.querySelector('[id*=""txtTotal""]')?.value || '';
                                                const badQty = row.querySelector('[id*=""txtBadQty""]')?.value || '';

                                                // Nếu dòng này có chứa PO thì mới lưu vào danh sách
                                                if (po.trim() !== '') {
                                                    results.push(`Row ${index}: PO=${po} | Plant=${plantID} | Process=${processID} | Item=${itemText} | Qty=${qty}`);
                                                }
                                            });

                                            return results;
                                        }
                                    ");
                        //lay cac so PO tu danh sach tren web
                        var poOnWeb = new HashSet<string>(
                        poList.Select(r => Regex.Match(r, @"PO=(\S+)").Groups[1].Value.Trim())
                              .Where(s => s != ""));

                        // Chia 2 danh sach:
                        //  - ListPOOnWeb: PO da co san tren web -> chi can danh dau trong DB, khong nhap lai
                        //  - ListPORun  : PO chua complete trong DB va chua co tren web -> can nhap
                        // PO da complete tu lan chay truoc nhung khong con tren web (da start o batch truoc) -> bo qua, khong nhap lai
                        var ListPOOnWeb = listPo.Where(p => poOnWeb.Contains((p.Po ?? "").Trim())).ToList();
                        var ListPORun = listPo.Where(p => !poOnWeb.Contains((p.Po ?? "").Trim())).ToList();

                        int cntOk = 0, cntWebErr = 0, cntExist = 0, cntUnknown = 0;
                        int cntSkipped = listPo.Count(p => p.isComplate);
                        AddLog($"ID:{item.ID} [DIAG] Trước khi nhập: DB có {listPo.Count} PO ({cntSkipped} đã complete trước đó), web đang có sẵn {poList.Length} dòng PO -> cần nhập {ListPORun.Count} PO, đánh dấu {ListPOOnWeb.Count} PO đã có trên web, txtPOCount: {await ReadPoCountDiagAsync(page)}");
                        await ScreenShot(page, item);


                        //danh dau cac PO da co tren wweb 
                        foreach (var poOnWebItem in ListPOOnWeb)
                        {
                            cntExist++;
                            if (!poOnWebItem.isComplate)
                            {
                                AddLog($"ID:{item.ID} PO {poOnWebItem.Po} đã có sẵn trên web, đánh dấu hoàn thành không nhập lại.");
                                await _service.UpdateSTTPO(poOnWebItem.ID, true);
                                await _service.update_desPO(poOnWebItem.ID, "đã tồn tại");
                            }
                        }

                        for (int i = 0; i < ListPORun.Count; i++)
                        {
                            var po = ListPORun[i];
                            step = $"nhập PO thứ {i + 1} ({po.Po})";
                            await Task.Delay(3000);
                            //lay gia tri count hien tai
                            int coutNow = await ReadPoCountAsync(page);

                            await page.ClickAsync("#lblDebug");
                            AddLog($"ID:{item.ID} Tiến hành nhập PO thứ {i + 1}: {po.Po}");
                            await page.Keyboard.TypeAsync(po.Po);
                            await page.Keyboard.PressAsync("Enter");

                            // 1. Lay message cua dialog (cho toi da 3 giay); khong co dialog thi lay o #txtMsg
                            string messageInputPO = (await SearchElement(page, "dlgMessage")).Trim();

                            await Task.Delay(3000);
                            if (string.IsNullOrWhiteSpace(messageInputPO))
                            {
                                messageInputPO = await ReadElementTextAsync(page, "#txtMsg");
                            }

                            Debug.WriteLine(messageInputPO);
                            string msgOneLine = messageInputPO.Replace("\r", " ").Replace("\n", " ");
                            int coutAfter = await ReadPoCountAsync(page);
                            AddLog($"ID:{item.ID} [DIAG] PO {i + 1}/{ListPORun.Count} {po.Po}: coutNow={coutNow}, coutAfter={coutAfter}, txtMsg=\"{msgOneLine}\"");

                            // Dua vao web truoc: so PO tren bang tang len nghia la PO da duoc nhan,
                            // ke ca khi #txtMsg van con thong bao cu cua PO truoc (tranh danh dau loi nham).
                            bool added = coutNow > 0 && coutAfter > coutNow;

                            if (!added && (messageInputPO.Contains("is locked") || messageInputPO.Contains("not") || messageInputPO.Contains("ALL NG")))
                            {
                                cntWebErr++;
                                AddLog($"ID:{item.ID} PO {po.Po} bị web từ chối: " + msgOneLine, LogStatus.Error);
                                await _service.update_desPO(po.ID, messageInputPO);
                                await _service.UpdateSTTPO(po.ID, false);
                            }
                            else
                            {
                                // Chua thay so PO tang thi cho them (toi da 4 lan x 3 giay)
                                int numCheck = 0;
                                while (!added && numCheck <= 3)
                                {
                                    AddLog($"ID:{item.ID} [DIAG] PO {po.Po}: chờ count tăng lần {numCheck + 1}, coutNow={coutNow}, coutAfter={coutAfter}");
                                    await Task.Delay(3000);
                                    coutAfter = await ReadPoCountAsync(page);
                                    added = coutNow > 0 && coutAfter > coutNow;
                                    numCheck++;
                                }

                                if (added)
                                {
                                    cntOk++;
                                    await _service.UpdateSTTPO(po.ID, true);
                                    await _service.update_desPO(po.ID, "OK");
                                    await Task.Delay(1000);
                                }
                                else
                                {
                                    cntUnknown++;
                                    AddLog($"ID:{item.ID} [DIAG] PO {po.Po}: count không tăng (coutNow={coutNow}, coutAfter={coutAfter}) -> đánh dấu 'Loi khong xac dinh'", LogStatus.Error);
                                    await _service.UpdateSTTPO(po.ID, false);
                                    await _service.update_desPO(po.ID, $"Loi : {messageInputPO}");
                                    runContext.HasUnknownError = true;
                                    await ScreenShot(page, item);
                                    await Task.Delay(1000);
                                }
                            }
                        }
                        AddLog($"ID:{item.ID} [DIAG] Tổng kết nhập PO (tổng {listPo.Count} PO, {cntSkipped} PO đã complete trong DB từ trước): đã có sẵn trên web={cntExist} | trong {ListPORun.Count} PO cần nhập: OK={cntOk}, web từ chối={cntWebErr}, count không tăng={cntUnknown}");
                    }

                    step = "đọc số PO trên bảng";
                    int numtry = 0;
                    while (runContext.PoCount == 0 && numtry < 5)
                    {
                        runContext.PoCount = await ReadPoCountAsync(page);
                        AddLog($"ID:{item.ID} [DIAG] Đọc PoCount lần {numtry + 1}: gán PoCount={runContext.PoCount} (txtPOCount: {await ReadPoCountDiagAsync(page)})");
                        await Task.Delay(5000);
                        numtry++;
                    }
                    AddLog($"ID:{item.ID} [DIAG] Chốt PoCount={runContext.PoCount}, DB={listPo.Count} PO, số dòng PO thật trên bảng web={await CountPoRowsDiagAsync(page)}, txtPOCount: {await ReadPoCountDiagAsync(page)}");

                    step = "nhấn cho tất cả các item";
                    AddLog($"ID:{item.ID} Tiến hành nhấn cho tất cả các item");

                    await Task.Delay(3000);
                    var btn = page.Locator("#btnAllQtyActCopytoTotal");
                    if (await btn.IsEnabledAsync())
                    {
                        await page.ClickAsync("#btnAllQtyActCopytoTotal");
                    }
                    await Task.Delay(2000);
                    AddLog($"ID:{item.ID} [DIAG] Chốt lần 2 PoCount={runContext.PoCount}, DB={listPo.Count} PO, số dòng PO thật trên bảng web={await CountPoRowsDiagAsync(page)}, txtPOCount: {await ReadPoCountDiagAsync(page)}");
                    runContext.PoCount = await ReadPoCountAsync(page);

                    step = "nhấn PO read finish";
                    await page.ClickAsync("#btnPOReadFinish");
                    await Task.Delay(4000);

                    step = "chờ quay về trang batch";
                    for (int numcheck = 0; numcheck <= 3; numcheck++)
                    {
                        // 1. Doi toi da 30 giay cho tieu de thanh trang batch
                        if (await WaitForTitleContainsAsync(page, "Daily Report work input batch", 30000))
                        {
                            AddLog($"ID:{item.ID} Nhấn cho tất cả các item hoàn thành ", LogStatus.Success);
                            return true;
                        }

                        // 2. Qua 30 giay ma van chua ve trang batch: luc nay moi nhan Cancel, roi cho lai o vong sau
                        AddLog($"ID:{item.ID} Quá 30 giây chưa thấy trang batch, nhấn Cancel (lần {numcheck + 1}).", LogStatus.Error);
                        await ClickCancelAsync(page, item);
                    }

                    AddLog($"ID:{item.ID} Đã nhấn Cancel 4 lần mà vẫn chưa về được trang batch.", LogStatus.Error);
                    return false;
                }

                AddLog($"ID:{item.ID} Quá 30 giây chưa vào được trang nhập PO list.", LogStatus.Error);
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"ID:{item.ID} Đã xảy ra lỗi trong quá trình vào nhập List PO (bước: {step}): " + ex.Message.Split('\n')[0], LogStatus.Error);
                return false;
            }
        }

        // Nhan nut Cancel: thu bang chuot (cho nut toi da 5 giay); neu nut bi che / chua san sang thi bam bang script tren trang.
        private async Task ClickCancelAsync(IPage page, BatchInputHistory item)
        {
            try
            {
                await page.ClickAsync("#btnCancel", new PageClickOptions { Timeout = 5000 });
                return;
            }
            catch (Exception ex) when (ex is TimeoutException || ex is PlaywrightException)
            {
                AddLog($"ID:{item.ID} Không nhấn được Cancel bằng chuột ({ex.Message.Split('\n')[0]}), thử nhấn bằng script.", LogStatus.Error);
            }

            try
            {
                bool clicked = await page.EvaluateAsync<bool>(
                    "() => { const b = document.getElementById('btnCancel'); if (!b) return false; b.click(); return true; }");
                if (!clicked)
                {
                    AddLog($"ID:{item.ID} Trang hiện tại không có nút #btnCancel.", LogStatus.Error);
                }
            }
            catch (Exception ex) when (ex is TimeoutException || ex is PlaywrightException)
            {
                AddLog($"ID:{item.ID} Nhấn Cancel bằng script cũng lỗi: {ex.Message.Split('\n')[0]}", LogStatus.Error);
            }
        }

        private async Task<bool> AT_ScanPO_own(IPage page, BatchInputHistory itemBatch)
        {
            try
            {
                if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                {
                    await page.ClickAsync("#txtPO");
                    AddLog($"ID:{itemBatch.ID} Đã vào {await page.TitleAsync()}", LogStatus.Success);
                    await page.ClickAsync("#lblStatus");

                    string s_result = await page.Locator("#lblDebug").InnerTextAsync();

                    for (int i = 0; i <= s_result.Length; i++)
                    {
                        await page.Keyboard.PressAsync("Backspace");

                    }//xóa toàn bộ label trước đó

                    AddLog($"ID:{itemBatch.ID} Tiến hành nhập 1 PO đại diện");

                    // Chay lai (numRetry >= 1): web gom theo nhom, PO dai dien quyet dinh phien vao nhom nao.
                    // nen chi thu cac PO DA complete (thuoc nhom cu) de vao lai dung nhom, chay 1 luot duy nhat.
                    bool isRetry = itemBatch.numRetry >= 1;

                    for (int i = 0; i < itemBatch.ListPO.Count; i++)
                    {
                        bool alreadyComplete = itemBatch.ListPO[i].isComplate;
                        if (isRetry && !alreadyComplete && itemBatch.description.Contains("SL")) continue;
                        string message = "";
                        string? rawText = "";
                        int checkint = 0;
                        do
                        {
                            await event_PoOwn(page, itemBatch, i);

                            // 1. lấy full message
                            //rawText = await page.Locator("#dlgMessage").InnerTextAsync()??"";
                            rawText = await SearchElement(page, "dlgMessage");
                            // 2. Định dạng lại
                            message = rawText.Trim();

                            await Task.Delay(3000);
                            //lấy message ở trên khi không lấy được của dialog
                            if (string.IsNullOrWhiteSpace(message))
                            {
                                await Task.Delay(1000);

                                // 1. lấy full message
                                rawText = await page.Locator("#txtMsg").InnerTextAsync();
                                // 2. Định dạng lại
                                message = rawText.Trim();
                            }
                            checkint++;
                        } while (message.Contains("PO(Select BatchID)") && checkint < 3);




                        if (message.Contains("is locked") || message.Contains("not") || message.Contains("PO(Select BatchID)"))
                        {
                            await page.ClickAsync("#txtPO");
                            AddLog($"ID {itemBatch.ID}: PO {itemBatch.ListPO[i].Po} không dùng được làm PO đại diện: {message.Replace("\r", " ").Replace("\n", " ")}", LogStatus.Success);
                            await page.ClickAsync("#lblStatus");

                            // PO da complete tu lan truoc thi khong ghi de trang thai trong DB
                            if (!alreadyComplete)
                            {
                                await _service.update_desPO(itemBatch.ListPO[i].ID, message);
                                await _service.UpdateSTTPO(itemBatch.ListPO[i].ID, false);
                            }
                        }
                        else
                        {
                            await _service.update_desPO(itemBatch.ListPO[i].ID, "OK");
                            AddLog($"Item: {itemBatch.ID} Đã nhập PO đại diện thành công (PO {itemBatch.ListPO[i].Po}{(isRetry ? ", PO đã complete từ lần trước" : "")}).", LogStatus.Success);
                            await _service.UpdateSTTPO(itemBatch.ListPO[i].ID, true);
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog(ex.Message, LogStatus.Error);
                return false;
            }
        }

        private async Task event_PoOwn(IPage page, BatchInputHistory itemBatch, int i)
        {
            await page.Keyboard.TypeAsync(itemBatch.ListPO[i].Po);
            await page.Keyboard.PressAsync("Enter");
            //await Task.Delay(5000);
        }

        private async Task<bool> AT_Operator_Shift(IPage page, BatchInputHistory itemBatch)
        {
            try
            {
                AddLog($"ID:{itemBatch.ID} Tiến hành nhập Operator và Shift");
                if ((await page.TitleAsync()).Contains("Daily Report input Operator"))
                {
                    await Input_msnv_shift(page, itemBatch);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã có lỗi xảy ra ở nhập Operator và Shift", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Login_terminalID(IPage page, string s, BatchInputHistory itemBatch)
        {
            try
            {
                await page.ClickAsync("#txtTerminalID");
                string s_result = await page.Locator("#lblDebug").InnerTextAsync();
                Console.WriteLine("s: " + s_result);


                for (int i = 0; i <= s_result.Length; i++)
                {
                    await page.PressAsync("#lblDebug", "Backspace");

                }//xóa toàn bộ label trước đó
                await page.ClickAsync("#lblStatus");
                await page.Keyboard.TypeAsync($"{s}");
                await page.Keyboard.PressAsync("Enter");
                AddLog($"ID:{itemBatch.ID} Tiến hành kiểm tra TerminalID ");

                //cho den khi machineID co gia tri 
                await page.WaitForFunctionAsync(
                         "() => document.getElementById('txtMachineID').value.trim() !== ''");
                string txt_result_machine = await page.Locator("#txtMachineID").InputValueAsync();
                AddLog("Kiểm tra kết quả");

                if (!string.IsNullOrWhiteSpace(txt_result_machine))
                {
                    AddLog($"ID:{itemBatch.ID} TerminalID oke, tiến hành login", LogStatus.Success);
                    //await Task.Delay(2000);
                    await page.ClickAsync("#btnLogin");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã có lỗi xảy ra trong quá trình nhập TerminalID!!", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_end_PO(IPage page, BatchInputHistory ItemBatch, RunContext runContext)
        {
            try
            {
                //await page.GotoAsync("http://10.4.24.117:8449/LG0100.aspx");
                //await page.WaitForFunctionAsync(
                //    "() => document.title.includes('Daily Report Login')");

                AddLog($"ID:{ItemBatch.ID} [DIAG] AT_end_PO: PoCount={runContext.PoCount}, ListPO.Count={ItemBatch.ListPO.Count}, start={ItemBatch.start}, số dòng PO thật trên bảng web={await CountPoRowsDiagAsync(page)}");
                if (ItemBatch.ListPO.Count <= runContext.PoCount && ItemBatch.start)
                {
                    await _service.UpdateItem(ItemBatch.ID);
                    await _service.update_des(ItemBatch.ID, $"START DONE ({runContext.PoCount}/{ItemBatch.ListPO.Count})");
                    await page.CloseAsync();
                    //delete video
                    await Deletefolder(ItemBatch.ID.ToString());
                    //await Deletefolder(@"Video\"+ItemBatch.ID.ToString());

                    return true;

                }
                else if (!ItemBatch.start)
                {
                    await _service.UpdateItem(ItemBatch.ID);
                    await _service.update_des(ItemBatch.ID, $"FINISH DONE ({runContext.PoCount}/{ItemBatch.ListPO.Count})");
                    await page.CloseAsync();
                    await Deletefolder(ItemBatch.ID.ToString());
                    await Deletefolder(@"Video\" + ItemBatch.ID.ToString());
                    return true;
                }
                else
                {
                    await ScreenShot(page, ItemBatch);
                    await _service.update_des(ItemBatch.ID, $"Không Đủ SL PO ({runContext.PoCount}/{ItemBatch.ListPO.Count})");
                    await page.CloseAsync();
                    return false;
                }
            }
            catch (Exception ex)
            {
                AddLog($"ID:{ItemBatch.ID} lỗi end PO : " + ex.Message);
                await page.CloseAsync();
                return false;
            }
        }

        private async Task Input_msnv_shift(IPage page, BatchInputHistory itemBatch)
        {
            await page.ClickAsync("#txtOperatorID");
            await page.ClickAsync("#txtShiftCD");
            await page.ClickAsync("#lblStatus");

            string s_result = await page.Locator("#lblDebug").InnerTextAsync();

            for (int i = 0; i <= s_result.Length; i++)
            {
                await page.Keyboard.PressAsync("Backspace");
            }//xóa toàn bộ label trước đó
            AddLog($"ID:{itemBatch.ID} Tiến hành nhập Operator");

            await page.Keyboard.TypeAsync($"{itemBatch.Msnv}");
            await page.Keyboard.PressAsync("Enter");
            AddLog("Tiến hành nhập Shift");
            await page.WaitForFunctionAsync(
            "() => document.getElementById('txtOperatorNM').value.trim() !== ''");

            await page.Keyboard.TypeAsync($"{itemBatch.Shift}");
            await page.Keyboard.PressAsync("Enter");

            await page.WaitForFunctionAsync(
            "() => document.getElementById('txtDutyYmd').value.trim() !== ''");

            await page.ClickAsync("#btnArea");
            AddLog($"ID:{itemBatch.ID} Hoàn thành bước 2", LogStatus.Success);
        }
        #endregion


        // [DIAG] Doc #txtPOCount theo 2 cach de so sanh: attribute (HTML ban dau) va value song (gia tri hien tai).
        private async Task<string> ReadPoCountDiagAsync(IPage page)
        {
            try
            {
                string? attr = await page.Locator("#txtPOCount").GetAttributeAsync("value");
                string live = await page.Locator("#txtPOCount").InputValueAsync();
                return $"attr={attr ?? "null"} | live={live}";
            }
            catch (Exception ex)
            {
                return "loi doc: " + ex.Message;
            }
        }

        // [DIAG] Dem so dong PO that su dang co trong bang tren trang.
        private async Task<string> CountPoRowsDiagAsync(IPage page)
        {
            try
            {
                int rows = await page.EvaluateAsync<int>(@"() =>
                    Array.from(document.querySelectorAll('tr.inlayout.coltitle'))
                         .filter(r => ((r.querySelector('[id*=""txtPO""]') || {}).value || '').trim() !== '').length");
                return rows.ToString();
            }
            catch (Exception ex)
            {
                return "loi dem: " + ex.Message;
            }
        }

        private async Task ScreenShot(IPage page, BatchInputHistory itemBatch)
        {
            try
            {
                //string path = @$"{SelectedFolder}\{itemBatch.ID}\{itemBatch.ID}_{DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")}.png";
                string path = @$"{_itemSaveConfig.pathSaveImg}\{itemBatch.ID}\{itemBatch.ID}_{DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")}.png";


                await page.ScreenshotAsync(new()
                {
                    Path = path,
                    FullPage = true
                });
                AddLog($"ID:{itemBatch.ID} Đã chụp ảnh màn hình và lưu lại", LogStatus.Success);
            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã xảy ra lỗi khi chụp ảnh màn hình: {ex.Message}", LogStatus.Error);
            }
        }

        private async Task<string> SearchElement(IPage page, string idDiv)
        {
            try
            {
                string rawcontent = await page.Locator($"#{idDiv}").InnerTextAsync(new() { Timeout = 3000 });
                return rawcontent ?? "";
            }
            catch (System.TimeoutException)
            {
                return "";
            }
            catch (PlaywrightException)
            {
                return "";
            }
        }

        private async Task Deletefolder(string itemBatchID)
        {
            //string path = @$"D:\jobs\IT\pcx\batchInput_wpf\Image_err\{itemBatchID}";
            string path = @$"{_itemSaveConfig.pathSaveImg}\{itemBatchID}";

            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }

        private string _selectedFolder;
        public string SelectedFolder
        {
            get => _selectedFolder;
            set
            {
                _selectedFolder = value;
                Debug.WriteLine("OnPropertyChanged SelectedFolder: " + value);
                OnPropertyChanged();
            }
        }

        private int _CountList;
        public int CountList
        {
            get => _CountList;
            set
            {
                _CountList = value;
                OnPropertyChanged();
            }
        }

        private string _selectedFolder_logerr;
        public string SelectedFolder_logerr
        {
            get => _selectedFolder_logerr;
            set
            {
                _selectedFolder_logerr = value;
                Debug.WriteLine("OnPropertyChanged SelectedFolder: " + value);
                OnPropertyChanged();
            }
        }

        private bool _isRunning = true;
        public bool IsRunning
        {
            get => _isRunning;
            set
            {
                _isRunning = value;
                OnPropertyChanged();
            }
        }

        // Chờ tối đa timeoutMs cho đến khi tiêu đề trang chứa chữ text. true nếu thấy, false nếu hết giờ.
        private async Task<bool> WaitForTitleContainsAsync(IPage page, string text, int timeoutMs)
        {
            try
            {
                await page.WaitForFunctionAsync(
                    "t => document.title.includes(t)",
                    text,
                    new PageWaitForFunctionOptions { Timeout = timeoutMs, PollingInterval = 500 });
                return true;
            }
            catch (TimeoutException)
            {
                // Playwright .NET bao het gio bang System.TimeoutException
                return false;
            }
            catch (PlaywrightException ex) when (ex.Message.Contains("Timeout", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        // Doc so PO tren bang (#txtPOCount). Khong doc duoc thi tra 0 thay vi nem loi (int.Parse("") se nem loi).
        private async Task<int> ReadPoCountAsync(IPage page)
        {
            try
            {
                string? value = await page.Locator("#txtPOCount").GetAttributeAsync("value", new LocatorGetAttributeOptions { Timeout = 5000 });
                return int.TryParse(value, out int count) ? count : 0;
            }
            catch (Exception ex) when (ex is TimeoutException || ex is PlaywrightException)
            {
                return 0;
            }
        }

        // Doc noi dung 1 element (cho toi da 3 giay), khong co / loi thi tra ve "" thay vi lam hong luong chay
        private async Task<string> ReadElementTextAsync(IPage page, string selector)
        {
            try
            {
                return (await page.Locator(selector).InnerTextAsync(new LocatorInnerTextOptions { Timeout = 3000 })).Trim();
            }
            catch (Exception ex) when (ex is TimeoutException || ex is PlaywrightException)
            {
                return "";
            }
        }
    }
}