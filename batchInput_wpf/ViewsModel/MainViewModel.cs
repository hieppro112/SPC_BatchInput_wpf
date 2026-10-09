using batchInput_wpf.Helper;
using batchInput_wpf.Model;
using batchInput_wpf.Service;
using Microsoft.Playwright;
using OpenTK.Graphics.OpenGL;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Windows;
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
        public ItemSaveConfig _itemSaveConfig;
        private FileHelper _myFileHelper = new FileHelper();
        

        public MainViewModel()
        {
            _service = new BatchAPIService();
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

        private async Task LoadBatchInput()
        {
            _itemSaveConfig = _myFileHelper.ReadJson();
            while (true)
            {
                try
                {
                    // 1. Tải đúng trang đang hiển thị (không GetAll toàn bộ bảng)
                    await RefreshCurrentPageAsync();

                    // 2. Lấy danh sách các item chưa hoàn thành
                    var result_notcomplate = await _service.GetProduct_notComplate_Async();

                    if (IsRunning && result_notcomplate != null && result_notcomplate.Count > 0)
                    {
                        // Số luồng chạy song song lấy từ cấu hình (nút Config)
                        var parallelOptions = new ParallelOptions
                        {
                            MaxDegreeOfParallelism = Math.Max(1, _itemSaveConfig.MaxThreads)
                        };
                        await Parallel.ForEachAsync(result_notcomplate, parallelOptions, async (item, token) =>
                        {
                            if (!IsRunning) return;

                            // Chỉ có the cập nhật UI nếu item đang nằm trong trang hiển thị hiện tại
                            // (vì đã chuyển sang phân trang phía server, không còn giữ toàn bộ danh sách ở client).
                            // Dùng Dispatcher.Invoke (đồng bộ) để đọc PagedBatchInputHistories ngay trên UI thread,
                            // tránh truy cập ObservableCollection đồng thời từ background thread trong lúc
                            // UI thread có thể đang Clear()/Add() lại danh sách (gây InvalidOperationException).
                            BatchInputHistory uiItem = null;
                            App.Current.Dispatcher.Invoke(() =>
                            {
                                uiItem = PagedBatchInputHistories.FirstOrDefault(x => x.ID == item.ID);
                            });

                            // Cập nhật trạng thái Running (Không dùng Invoke chờ)
                            if (uiItem != null)
                            {
                                App.Current.Dispatcher.BeginInvoke(new Action(() => uiItem.Status = ItemStatus.Running));
                            }

                            bool result_run = false;
                            int count_err = 0;

                            do
                            {
                                // Thêm log bất đồng bộ lên UI
                                App.Current.Dispatcher.BeginInvoke(new Action(() =>
                                {
                                    AddLog("Bắt đầu AutoClick BatchInput item: " + item.ID);
                                }));

                                // Thực thi tác vụ Auto Web (Giải phóng hoàn toàn khỏi UI Thread)
                                result_run = await event_auto_web(item);
                                await _service.update_numRetry(item.ID);

                                // Cập nhật trạng thái Success / Error
                                if (uiItem != null)
                                {
                                    App.Current.Dispatcher.BeginInvoke(new Action(() =>
                                    {
                                        uiItem.Status = result_run ? ItemStatus.Success : ItemStatus.Error;
                                    }));
                                }

                                if (!result_run)
                                {
                                    count_err++;
                                }

                            } while (!result_run && count_err < 1);

                            Debug.WriteLine($"Item {item.ID} count err: {count_err}");

                            // Ghi log lỗi nếu thất bại
                            if (count_err >= 3)
                            {
                                string parse_json = JsonSerializer.Serialize(item);
                                string fileName = $"{item.ID}_{DateTime.Now:yyyy-MM-dd-HH-mm-ss}_err.json";
                                string filePath = Path.Combine(_itemSaveConfig.pathLogErr, fileName);

                                if (!Directory.Exists(_itemSaveConfig.pathLogErr))
                                {
                                    Directory.CreateDirectory(_itemSaveConfig.pathLogErr);
                                }

                                await File.WriteAllTextAsync(filePath, parse_json);
                                
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine("Lỗi trong vòng lặp BatchInput: " + ex.Message);
                    AddLog("Lỗi trong vòng lặp BatchInput: " + ex.Message, LogStatus.Error);

                }

                Console.WriteLine("length: " + TotalItems);
                await Task.Delay(_itemSaveConfig.PollDelaySeconds *1000);
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
                    string logFilePath = Path.Combine(logFolder, "log.txt");

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

        #region bat dau autoclick
        private async Task<bool> event_auto_web(BatchInputHistory itemBatch)
        {
            try
            {
                RunContext itemContext = new RunContext();
                // using/await using: dam bao driver Playwright, browser va context luon duoc
                // dong khi ham ket thuc (ke ca return som hay nem exception), tranh leak
                // tien trinh msedge.exe khi app chay lien tuc trong thoi gian dai.
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
                await using var context = await browser.NewContextAsync();
                // Xóa thu?c tính navigator.webdriver trên trang
                await context.AddInitScriptAsync(@"
                    Object.defineProperty(navigator, 'webdriver', {
                        get: () => undefined
                    });
                ");

                //var page = await browser.NewPageAsync();
                var page = await context.NewPageAsync();

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
                                    await Task.Delay(3000);
                                    bool IscheckList = await AT_Click_list_PO(page, itemBatch.ListPO, itemBatch, itemContext);
                                    await Task.Delay(3000);
                                    if (IscheckList && itemBatch.start)
                                    {
                                        await Task.Delay(3000);
                                        if (await AT_Click_start(page, itemBatch))
                                        {
                                            await Task.Delay(3000);
                                            if (await AT_Click_startAll(page, itemBatch))
                                            {
                                                AddLog($"ID:{itemBatch.ID} Hoàn thành Start item: " + itemBatch.ID, LogStatus.Success);
                                                await Deletefolder(itemBatch);
                                                return await AT_end_PO(page, itemBatch,itemContext);
                                            }
                                            else
                                            {
                                                await ScreenShot(page, itemBatch);
                                                await page.CloseAsync();
                                                AddLog($"ID:{itemBatch.ID} Đã có lỗi khi chạy start All ", LogStatus.Error);
                                            }
                                        }
                                        else
                                        {
                                            await ScreenShot(page, itemBatch);
                                            AddLog($"ID:{itemBatch.ID} Không vào được nơi start ", LogStatus.Error);
                                        }
                                    }
                                    else if (IscheckList && !itemBatch.start)
                                    {
                                        if (await AT_Click_Finish(page, itemBatch))
                                        {
                                            await Deletefolder(itemBatch);
                                            return await AT_end_PO(page, itemBatch,itemContext);
                                        }
                                    }
                                    else
                                    {
                                        AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập list PO ", LogStatus.Error);
                                        await ScreenShot(page, itemBatch);
                                        await _service.update_des(itemBatch.ID, "Ðã có lỗi tại nhập List PO.");
                                        await page.CloseAsync();
                                        //AT_end_PO(wait, driver);
                                    }
                                }
                                else
                                {
                                    AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập PO đại diện bước 3", LogStatus.Error);
                                    await _service.update_des(itemBatch.ID, "Ðã có lỗi tại nhập PO đại diện.");
                                    await ScreenShot(page, itemBatch);
                                    await page.CloseAsync();
                                }
                            }
                            else
                            {
                                AddLog($"ID:{itemBatch.ID} Đã có lỗi tại nhập Operator ID bước 2", LogStatus.Error);
                                await ScreenShot(page, itemBatch);
                                await page.CloseAsync();
                                await _service.update_des(itemBatch.ID, "Ðã có lỗi tại nhập Operator & Shift");

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
                    AddLog("lần này chưa tìm thăy");
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
                while (numcheck<=3)
                {

                    ////lay thoi gian finish 
                    //string txt_finish_tb = await page.Locator("#lstBatchSub_txtAreaFinishYmd_1").InnerTextAsync();
                    // 1. L?y toàn b? Text trong th? #txtMsg
                    string rawText = await page.Locator("#txtMsg").InnerTextAsync();

                    // 2. Ð?nh d?ng l?i van b?n (thay th? chu?i xu?ng dòng n?u c?n)
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

                await _service.update_des(itemBatch.ID, "Chưa hoàn thành Finish All PO: Đầy đủ thao tác nhưng dã bị treo");
                AddLog($"ID:{itemBatch.ID} Chưa hoàn thành Finish All PO: Đầy đủ thao tác nhưng đã bị treo  ", LogStatus.Success);
                return false;

                
            }
            catch (Exception ex)
            {
                AddLog($"ID:{itemBatch.ID} Đã xảy ra lỗi trong quá trình vào nhập Operator: {ex.Message}", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Click_startAll(IPage page, BatchInputHistory itemBatch)
        {
            try
            {
                AddLog($"ID:{itemBatch.ID} Tiến hành start All");
                await page.ClickAsync("#MyVersion");
                AddLog($"ID:{itemBatch.ID} Tiến hành nhập Operator");
                await page.Keyboard.TypeAsync(itemBatch.Msnv);
                await page.Keyboard.PressAsync("Enter");
                await Task.Delay(2000);
                //string messageInputPO = await page.Locator("#txtMsg").InnerTextAsync();

                // 1. lấy full message
                string rawText = await page.Locator("#txtMsg").InnerTextAsync();

                // 2. định dạng lại 
                string messageInputPO = rawText.Trim();

                if (messageInputPO.Contains("Start or Finish or Apply"))
                {
                    await page.ClickAsync("#btnCancel");
                    await Task.Delay(2000);
                }

                await page.Locator("#btnAreaStartAll").WaitForAsync(new()
                {
                    State = WaitForSelectorState.Visible
                });

                await page.ClickAsync("#btnAreaStartAll");
                //await page.WaitForFunctionAsync(
                //"() => document.title.includes('Daily Report work input batch')");


                int numcheck = 0;
                while (numcheck <= 3)
                {
                    if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                    {
                        await page.WaitForFunctionAsync(
                "() => document.title.includes('Daily Report work input batch')");
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
                AddLog($"ID:{itemBatch.ID} Đã xảy ra lỗi trong quá trình vào nhập Operator: " + ex.Message, LogStatus.Error);
                await _service.update_desPO(itemBatch.ID, $" Đã xảy ra lỗi trong quá trình vào nhập Operator:{ex.Message}");
                await ScreenShot(page, itemBatch);
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

        private async Task<bool> AT_Click_list_PO(IPage page, List<ListPO> listPo, BatchInputHistory item, RunContext runContext)
        {
            try
            {
                await page.ClickAsync("#btnInputPOList");
                await page.WaitForFunctionAsync(
                    "() => document.title.includes('Daily Report work input po list')");

                if ((await page.TitleAsync()).Contains("Daily Report work input po list"))
                {
                    AddLog($"ID:{item.ID} Đã vào trang nhập PO list");
                    await page.ClickAsync("#MyVersion");

                    AddLog($"ID:{item.ID} Tiến hành nhập PO list");

                    await page.Locator("#lstBatchPOList_itemPlaceholderContainer")
                              .WaitForAsync();

                    //quet cac PO vao
                    if (item.start)
                    {
                        //lay cac po đang co sẵn 
                        var poList = await page.EvaluateAsync<string[]>(@"
                                        () => {
                                            // L?y t?t c? các dòng d? li?u trong b?ng
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

                                                // N?u dòng này có ch?a PO thì m?i luu vào danh sách
                                                if (po.trim() !== '') {
                                                    results.push(`Row ${index}: PO=${po} | Plant=${plantID} | Process=${processID} | Item=${itemText} | Qty=${qty}`);
                                                }
                                            });

                                            return results;
                                        }
                                    ");
                        for (int i = 0; i < listPo.Count; i++)
                        {
                            if (!listPo[i].isComplate)
                            {
                                await Task.Delay(3000);
                                //lay gia tri count hien tai
                                int coutNow = await page.Locator("#txtPOCount").GetAttributeAsync("value").ContinueWith(t => int.Parse(t.Result ?? "0"));

                                await page.ClickAsync("#lblDebug");
                                AddLog($"ID:{item.ID} Tiến hành nhập PO thứ {i + 1}: {listPo[i].Po}");
                                await page.Keyboard.TypeAsync(listPo[i].Po);
                                //await page.FillAsync("#txtEditPO", listPo[i].Po);
                                //await page.ClickAsync("#txtEditPO");
                                await page.Keyboard.PressAsync("Enter");
                                await Task.Delay(10000);


                                //kiem tra ket qua sau khi xoa 
                                await page.WaitForFunctionAsync(
                                    "() => document.getElementById('txtMsg').innerText.trim() !== ''");

                                //string messageInputPO = await page.Locator("#txtMsg").InnerTextAsync();
                                // 1. Lấy toàn bộ Text trong thẻ #txtMsg
                                string rawText = await page.Locator("#txtMsg").InnerTextAsync();

                                // 2. Định dạng lại văn bản (thay thế chuỗi xuống dòng nếu cần)
                                string messageInputPO = rawText.Trim();

                                Debug.WriteLine(messageInputPO);


                                
                                if (messageInputPO.Contains("is locked") || messageInputPO.Contains("is not"))
                                {
                                    AddLog(messageInputPO + listPo[i].Po, LogStatus.Error);
                                    await _service.update_desPO(listPo[i].ID, messageInputPO);
                                    await _service.UpdateSTTPO(listPo[i].ID, false);
                                    //await _service.update(listPo[i].ID, messageInputPO);

                                    //return false;
                                }
                                else if (messageInputPO.Contains("or Apply"))
                                {
                                    //danh sách mã PO đã có trong bảng 
                                    List<string> existingPoList = poList.Select(x => x.ToString().Trim()).ToList();
                                    string currentPo = listPo[i].Po.Trim();

                                    if (existingPoList.Any(x => x.Contains(currentPo)))
                                    {
                                        AddLog($"PO {listPo[i].Po} không được thêm vào danh sách vì dã tồn tại.", LogStatus.Error);
                                        await _service.UpdateSTTPO(listPo[i].ID, true);
                                        await _service.update_desPO(listPo[i].ID, "đã tồn tại");
                                        await page.ClickAsync("#btnCancel");
                                        await Task.Delay(2000);
                                    }

                                }
                                else
                                {
                                    int numCheck = 0;
                                    bool isCheck = false;
                                    while (numCheck <= 3)
                                    {
                                        int coutNext = await page.Locator("#txtPOCount").GetAttributeAsync("value").ContinueWith(t => int.Parse(t.Result ?? "0"));
                                        if ((coutNow < coutNext) && (coutNow != 0) && (coutNext != 0))
                                        {
                                            await _service.UpdateSTTPO(listPo[i].ID, true);
                                            await _service.update_desPO(listPo[i].ID, "OK");
                                            await Task.Delay(1000);
                                            isCheck = true;
                                            break;
                                        }
                                        numCheck++;
                                        await Task.Delay(3000);
                                    }
                                    if (!isCheck)
                                    {
                                        await _service.UpdateSTTPO(listPo[i].ID, false);
                                        await _service.update_desPO(listPo[i].ID, "Loi khong xac dinh");
                                        await Task.Delay(1000);
                                    }
                                }
                            }
                        }
                    }

                    int numtry = 0;
                    while (runContext.PoCount == 0 && numtry < 5)
                    {
                        runContext.PoCount = await page.Locator("#txtPOCount").GetAttributeAsync("value").ContinueWith(t => int.Parse(t.Result ?? "0"));
                        await Task.Delay(5000);
                        numtry++;
                    }

                    AddLog($"ID:{item.ID} Tiến hành nhấn cho tất cả các item");

                    await Task.Delay(3000);
                    var btn = page.Locator("#btnAllQtyActCopytoTotal");

                    Debug.WriteLine("IsEnabledAsync: " + await btn.IsEnabledAsync());
                    if (await btn.IsEnabledAsync())
                    {
                        await page.ClickAsync("#btnAllQtyActCopytoTotal");
                    }
                    await Task.Delay(2000);
                    await page.ClickAsync("#btnPOReadFinish");
                    await Task.Delay(4000);

                    // 1. L?y toàn b? Text trong thẻ #txtMsg
                    string rawText2 = await page.Locator("#txtMsg").InnerTextAsync();

                    // 2. Ð?nh d?ng l?i van b?n (thay th? chu?i xu?ng dòng n?u c?n)
                    string messageInputPO2 = rawText2.Trim();
                    int numcheck = 0;
                    //if (messageInputPO2.Contains(" Please try again after some time."))
                    //{
                        
                        while (numcheck <= 3)
                        {
                            if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                            {
                                    await page.WaitForFunctionAsync(
                            "() => document.title.includes('Daily Report work input batch')");
                            AddLog($"ID:{item.ID} Nhấn cho tất cả các item hoàn thành ", LogStatus.Success);
                            return true;
                            }
                            else
                            {
                                await Task.Delay(2000);
                                //if (await btn.IsEnabledAsync())
                                //{
                                //    await page.ClickAsync("#btnAllQtyActCopytoTotal");
                                //}
                                //await Task.Delay(2000);
                                //await page.ClickAsync("#btnPOReadFinish");
                                //await Task.Delay(2000);
                                await page.ClickAsync("#btnCancel");
                            }
                            

                            numcheck++;
                            await Task.Delay(5000);
                        }
                        return false;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog($"ID:{item.ID} Đã xảy ra lỗi trong quá trình vào nhập List PO: "+ex.Message, LogStatus.Error);
                return false;
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

                    }//xoa toàn bộ label trước đó

                    AddLog($"ID:{itemBatch.ID} Tiến hành Nhập 1 PO đại điện");

                    for(int i = 0; i < itemBatch.ListPO.Count; i++)
                    {
                        await page.Keyboard.TypeAsync(itemBatch.ListPO[i].Po);
                        await page.Keyboard.PressAsync("Enter");
                        await Task.Delay(5000);
                        // 1. l?y full message
                        string rawText = await page.Locator("#txtMsg").InnerTextAsync();

                        // 2. d?nh d?ng l?i
                        string message = rawText.Trim();

                        if (message.Contains("is locked") || message.Contains("not exist") || message.Contains("PO(Select BatchID)"))
                        {
                            await page.ClickAsync("#txtPO");
                            AddLog($"ID:{itemBatch.ID} Ðã vào {await page.TitleAsync()}", LogStatus.Success);
                            await page.ClickAsync("#lblStatus");

                            //cap nhat trang thai PO vao DB item.st
                            await _service.update_desPO(itemBatch.ListPO[i].ID, message);
                            await _service.UpdateSTTPO(itemBatch.ListPO[i].ID, false);

                        }
                        else
                        {
                            await _service.update_desPO(itemBatch.ListPO[i].ID, "OK");
                            AddLog($"Item: {itemBatch.ID} Đã nhập PO đại diện thành công.", LogStatus.Success);
                            await _service.UpdateSTTPO(itemBatch.ListPO[i].ID, true);
                            return true;
                        }
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog(ex.Message,LogStatus.Error);
                return false;
            }
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
                AddLog($"ID:{itemBatch.ID} Đã có lỗi xảy ra ở Nhập Operator và Shift", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Login_terminalID(IPage page, string s,BatchInputHistory itemBatch)
        {
            try
            {
                await page.ClickAsync("#txtTerminalID");
                string s_result = await page.Locator("#lblDebug").InnerTextAsync();
                Console.WriteLine("s: " + s_result);


                for (int i = 0; i <= s_result.Length; i++)
                {
                    await page.PressAsync("#lblDebug", "Backspace");

                }//xoa toàn bộ label trước đó
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
                    AddLog($"ID:{itemBatch.ID} TerminalID oke tiến hành login", LogStatus.Success);
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

        private async Task<bool> AT_end_PO(IPage page, BatchInputHistory ItemBatch,RunContext runContext)
        {
            try
            {
                await page.GotoAsync("http://10.4.24.117:8449/LG0100.aspx");
                await page.WaitForFunctionAsync(
                    "() => document.title.includes('Daily Report Login')");

                if (ItemBatch.ListPO.Count <= runContext.PoCount&&ItemBatch.start)
                {
                    await _service.UpdateItem(ItemBatch.ID);
                    await _service.update_des(ItemBatch.ID, $"START DONE ({runContext.PoCount}/{ItemBatch.ListPO.Count})");
                    await page.CloseAsync();
                    return true;

                }
                else if (!ItemBatch.start)
                {
                    await _service.UpdateItem(ItemBatch.ID);
                    await _service.update_des(ItemBatch.ID, $"FINISH DONE ({runContext.PoCount}/{ItemBatch.ListPO.Count})");
                    await page.CloseAsync();
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
            }//xoa toàn bộ label trước đó
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

        private async Task Deletefolder(BatchInputHistory itemBatch)
        {
            string path = @$"D:\jobs\IT\pcx\batchInput_wpf\Image_err\{itemBatch.ID}";
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
    }
}
