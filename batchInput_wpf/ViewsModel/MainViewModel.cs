using batchInput_wpf.Helper;
using batchInput_wpf.Model;
using batchInput_wpf.Service;
using LiveChartsCore;
using LiveChartsCore.SkiaSharpView;
using Microsoft.Playwright;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Windows;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace batchInput_wpf.ViewsModel
{
    public class MainViewModel : BaseViewModel
    {
        private readonly BatchAPIService _service;
        public ObservableCollection<BatchInputHistory> BatchInputHistories { get; set; } = new ObservableCollection<BatchInputHistory>();
        public ObservableCollection<ListPO> ListPos { get; set; } = new ObservableCollection<ListPO>();
        public ItemSaveConfig _itemSaveConfig;
        private FileHelper _myFileHelper = new FileHelper();

        public MainViewModel()
        {
            _service = new BatchAPIService();
            _itemSaveConfig = _myFileHelper.ReadJson();
            _ = LoadBatchInput();
            LoadChart();
        }

        public async Task InitializeAsync()
        {
            //LoadChart();
            //await LoadBatchInput();
            //Debug.WriteLine("INITIALIZE VM: " + GetHashCode());
            //await Task.CompletedTask;
        }

        //private void LoadChart()
        //{
        //    Debug.WriteLine("LOAD CHART VM: " + GetHashCode());
        //    try
        //    {
        //        DateTime date = DateTime.Now;
        //        //List<ItemGetCount> resultCount = await _service.GetCount();

        //        //lay so lieu 
        //        //var title = resultCount.Select(x => x.date.ToString("dd-MM")).ToArray();
        //        //var Soluong = resultCount.Select(x => (double)x.count).ToArray();
        //        var Soluong = new double[] { 0, 1, 5, 1, 3, 50, 6 };

        //        // Kh?i t?o d? li?u bi?u d?
        //        Series = new ISeries[]
        //        {
        //        new ColumnSeries<double>
        //        {
        //            Name = "Số lượng",
        //            //Values = new double[] { 100, 230, 302, 400, 212, 312, 422 }
        //            Values = Soluong
        //        }
        //        };

        //        XAxes = new Axis[]
        //        {
        //        new Axis
        //        {
        //            Labels = new string[] { date.AddDays(-6).ToString("dd/MM"), date.AddDays(-5).ToString("dd/MM"), date.AddDays(-4).ToString("dd/MM"), date.AddDays(-3).ToString("dd/MM"), date.AddDays(-2).ToString("dd/MM"), date.AddDays(-1).ToString("dd/MM"), date.ToString("dd/MM")},
        //            //Labels = title,
        //            LabelsRotation = 0
        //        }
        //        };

        //        YAxes = new Axis[]
        //        {
        //        new Axis
        //        {
        //            MinLimit = 0
        //        }
        //        };
        //    }
        //    catch(Exception ex)
        //    {
        //        MessageBox.Show(ex.Message);
        //    }
        //}

        private void LoadChart()
        {
            try
            {
                DateTime date = DateTime.Now;

                var Soluong = new double[]
                {
            0, 1, 5, 1, 3, 50, 6
                };

                Series = new ISeries[]
                {
            new ColumnSeries<double>
            {
                Name = "Số lượng",
                Values = Soluong
            }
                };

                XAxes = new Axis[]
                {
            new Axis
            {
                Labels = new string[]
                {
                    date.AddDays(-6).ToString("dd/MM"),
                    date.AddDays(-5).ToString("dd/MM"),
                    date.AddDays(-4).ToString("dd/MM"),
                    date.AddDays(-3).ToString("dd/MM"),
                    date.AddDays(-2).ToString("dd/MM"),
                    date.AddDays(-1).ToString("dd/MM"),
                    date.ToString("dd/MM")
                },
                LabelsRotation = 0
            }
                };

                YAxes = new Axis[]
                {
            new Axis
            {
                MinLimit = 0
            }
                };

                Debug.WriteLine("Series Count: " + Series.Length);
                Debug.WriteLine("XAxes Count: " + XAxes.Length);
                Debug.WriteLine("YAxes Count: " + YAxes.Length);

                OnPropertyChanged(nameof(Series));
                OnPropertyChanged(nameof(XAxes));
                OnPropertyChanged(nameof(YAxes));
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.ToString());
            }
        }

        private async Task LoadBatchInput()
        {
            int itemCount = 0;
            while (true)
            {
                //await LoadChart();
                var batchInputHistory = new BatchInputHistory();
                var result = await _service.GetAllProductAsync();
                //if (itemCount != result.Count)
                //{
                BatchInputHistories.Clear();
                foreach (var item in result)
                {
                    BatchInputHistories.Add(item);
                    OnPropertyChanged();

                }
                itemCount = result.Count;
                //}

                var result_notcomplate = await _service.GetProduct_notComplate_Async();
                foreach (var item in result_notcomplate)
                {
                    if (IsRunning)
                    {
                        var uiItem = BatchInputHistories
                        .FirstOrDefault(x => x.ID == item.ID);
                        if (uiItem != null)
                        {
                            uiItem.Status = ItemStatus.Running;
                        }

                        //await event_auto_web(item);
                        //item.Status = ItemStatus.Running;
                        bool result_run = false;
                        int count_err = 0;
                        do
                        {
                            var startLog_at = AddLog("Bắt đầu AutoClick BatchInput item: " + item.ID);
                            result_run = await event_auto_web(item);
                            if (result_run)
                            {
                                uiItem.Status = ItemStatus.Success;
                            }
                            else
                            {
                                uiItem.Status = ItemStatus.Error;
                                count_err++;
                            }
                        } while (result_run == false && count_err <= 2);
                        Debug.WriteLine("count err: " + count_err);
                        if (count_err >= 3)
                        {
                            string parse_json = JsonSerializer.Serialize(item);
                            File.WriteAllText($@"C:\Users\KVH_IT_Mem_Hiep\MISUMI Group Inc\IT Program - 28.Log_err_batchInput\{item.ID}_{DateTime.Now.ToString("yyyy-MM-dd-hh-mm-ss")}_err.json", parse_json);
                        }
                    }
                }

                Console.WriteLine("length: " + BatchInputHistories.Count);
                await Task.Delay(10000);

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

            Log.Insert(0, item);

            if (Log.Count >= 100) { Log.RemoveAt(50); }

            return item;
        }

        private ISeries[] _series;
        private Axis[] _xAxes;
        private Axis[] _yAxes;

        public ISeries[] Series
        {
            get => _series;
            set
            {
                _series = value;
                OnPropertyChanged();
            }
        }

        public Axis[] XAxes
        {
            get => _xAxes;
            set
            {
                _xAxes = value;
                OnPropertyChanged();
            }
        }

        public Axis[] YAxes
        {
            get => _yAxes;
            set
            {
                _yAxes = value;
                OnPropertyChanged();
            }
        }


        #region INotifyPropertyChanged Implementation
        public event PropertyChangedEventHandler PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
        #endregion

        #region bat dau autoclick
        private async Task<bool> event_auto_web(BatchInputHistory itemBatch)
        {
            bool found_tab = false;
            try
            {
                var playwright = await Playwright.CreateAsync();
                var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false
                });

                var page = await browser.NewPageAsync();
                await page.GotoAsync("http://10.4.24.117:8449/LG0100.aspx");
                if ((await page.TitleAsync()).Contains("Daily Report Login"))
                {
                    await Task.Delay(3000);
                    try
                    {
                        if (await AT_Login_terminalID(page, itemBatch.TerminalID) == true)
                        {
                            await Task.Delay(3000);
                            if (await AT_Operator_Shift(page, itemBatch.Msnv, itemBatch.Shift) == true)
                            {
                                await Task.Delay(3000);
                                if (await AT_ScanPO_own(page, itemBatch.ListPO))
                                {
                                    await Task.Delay(3000);
                                    bool IscheckList = await AT_Click_list_PO(page, itemBatch.ListPO,itemBatch);
                                    await Task.Delay(3000);
                                    if (IscheckList && itemBatch.start)
                                    {
                                        await Task.Delay(3000);
                                        if (await AT_Click_start(page))
                                        {
                                            await Task.Delay(3000);
                                            if (await AT_Click_startAll(page, itemBatch.Msnv))
                                            {
                                                AddLog("Hoàn thành Start item: " + itemBatch.ID, LogStatus.Success);
                                                await Deletefolder(itemBatch);
                                                return await AT_end_PO(page, itemBatch);
                                            }
                                            else
                                            {
                                                await ScreenShot(page, itemBatch);
                                                await page.CloseAsync();
                                                AddLog("Đã có lỗi khi chạy start All ", LogStatus.Error);
                                            }
                                        }
                                        else
                                        {
                                            ScreenShot(page, itemBatch);
                                            AddLog("Không vào được nơi start ", LogStatus.Error);
                                        }
                                    }
                                    else if (IscheckList && !itemBatch.start)
                                    {
                                        if (await AT_Click_Finish(page))
                                        {
                                            await Deletefolder(itemBatch);
                                            return await AT_end_PO(page, itemBatch);
                                        }
                                    }
                                    else
                                    {
                                        AddLog("Đã có lỗi tại nhập list PO ", LogStatus.Error);
                                        await ScreenShot(page, itemBatch);
                                        await page.CloseAsync();
                                        //AT_end_PO(wait, driver);
                                    }
                                }
                                else
                                {
                                    AddLog("Đã có lỗi tại nhập PO đại diện bước 3", LogStatus.Error);
                                    await ScreenShot(page, itemBatch);
                                    await page.CloseAsync();
                                }
                            }
                            else
                            {
                                AddLog("Đã có lỗi tại nhập Operator ID bước 2", LogStatus.Error);
                                await ScreenShot(page, itemBatch);
                                await page.CloseAsync();
                            }
                        }
                        else
                        {
                            AddLog("Đã có lỗi tại nhập Terminal ID bước 1", LogStatus.Error);
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

        private async Task<bool> AT_Click_Finish(IPage page)
        {
            try
            {
                AddLog("Tiến hành Finish All PO ", LogStatus.Success);
                await page.ClickAsync("#lstBatchSub_selButton_1");
                await page.WaitForFunctionAsync(
                @"() => {
                        const btn = document.getElementById('btnAreaFinishAll');
                        return btn && !btn.disabled;
                    }");
                await page.ClickAsync("#btnAreaFinishAll");
                await Task.Delay(2000);
                await page.ClickAsync("#btnBatchFinish");
                AddLog("Hoàn thành Finish All PO ", LogStatus.Success);
                return true;
            }
            catch (Exception ex)
            {
                AddLog($"Đã xảy ra lỗi trong quá trình vào nhập Operator: {ex.Message}", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Click_startAll(IPage page, string msnv)
        {
            try
            {
                AddLog("Tiến hành start All");
                await page.ClickAsync("#MyVersion");
                AddLog("Tiến hành nhập Operator");
                await page.Keyboard.TypeAsync(msnv);
                await page.Keyboard.PressAsync("Enter");
                await Task.Delay(2000);
                //await page.ClickAsync("#btnAreaStart");
                //await page.WaitForFunctionAsync(
                //"() => document.getElementById('txtEditAreaStartYmd').value.trim() !== ''");
                //await Task.Delay(2000);
                //await page.ClickAsync("#btnApply");
                //doi start all hiển thị
                await page.Locator("#btnAreaStartAll").WaitForAsync(new()
                {
                    State = WaitForSelectorState.Visible
                });

                await page.ClickAsync("#btnAreaStartAll");
                await page.WaitForFunctionAsync(
                "() => document.title.includes('Daily Report work input batch')");

                if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                {
                    AddLog("Hoàn thành Start ", LogStatus.Success);

                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog("Đã xảy ra lỗi trong quá trình vào nhập Operator: " + ex.Message, LogStatus.Error);
                //await page.CloseAsync();
                return false;
            }
        }

        private async Task<bool> AT_Click_start(IPage page)
        {
            try
            {
                AddLog("Tiến hành vào input Operator ");
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
                    AddLog("Hoàn thành vào input Operator ", LogStatus.Success);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog("Đã xảy ra lỗi trong quá trình vào nhập Operator", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Click_list_PO(IPage page, List<ListPO> listPo, BatchInputHistory item)
        {
            try
            {
                await page.ClickAsync("#btnInputPOList");
                await page.WaitForFunctionAsync(
                    "() => document.title.includes('Daily Report work input po list')");

                if ((await page.TitleAsync()).Contains("Daily Report work input po list"))
                {
                    AddLog("Đã vào trang nhập PO list");
                    await page.ClickAsync("#MyVersion");

                    AddLog("Tiến hành nhập PO list");

                    await page.Locator("#lstBatchPOList_itemPlaceholderContainer")
                              .WaitForAsync();

                    //quet cac PO vao
                    if (item.start)
                    {
                        for (int i = 1; i < listPo.Count; i++)
                        {
                            await Task.Delay(3000);
                            //lay gia tri count hien tai
                            int coutNow = await page.Locator("#txtPOCount").GetAttributeAsync("value").ContinueWith(t => int.Parse(t.Result ?? "0"));


                            await page.ClickAsync("#lblDebug");
                            AddLog($"Tiến hành nhập PO thứ {i + 1}: {listPo[i].Po}");
                            await page.Keyboard.TypeAsync(listPo[i].Po);
                            //await page.FillAsync("#txtEditPO", listPo[i].Po);
                            Debug.WriteLine($"{DateTime.Now} - Đã nhấn ENTER");
                            //await page.ClickAsync("#txtEditPO");
                            await page.Keyboard.PressAsync("Enter");


                            int coutNext = await page.Locator("#txtPOCount").GetAttributeAsync("value").ContinueWith(t => int.Parse(t.Result ?? "0"));
                            if (coutNow >= coutNext)
                            {
                                AddLog($"PO {listPo[i].Po} không được thêm vào danh sách vì đã tồn tại.", LogStatus.Error);
                                await page.ClickAsync("#btnCancel");
                                await Task.Delay(2000);
                            }

                            //kiem tra ket qua sau khi xoa 
                            await page.WaitForFunctionAsync(
                                "() => document.getElementById('txtMsg').innerText.trim() !== ''");

                            string messageInputPO = await page.Locator("#txtMsg").InnerTextAsync();

                            Debug.WriteLine(messageInputPO);
                            if (messageInputPO.Contains("is locked"))
                            {
                                AddLog(messageInputPO + listPo[i].Po, LogStatus.Error);
                                //return false;
                            }

                        }
                    }

                    AddLog("Tiến hành nhấn cho tất cả các item");

                    var btn = page.Locator("#btnAllQtyActCopytoTotal");

                    Debug.WriteLine("IsEnabledAsync: " + await btn.IsEnabledAsync());
                    if (await btn.IsEnabledAsync())
                    {
                        await page.ClickAsync("#btnAllQtyActCopytoTotal");
                    }
                    await Task.Delay(2000);
                    await page.ClickAsync("#btnPOReadFinish");
                    await page.WaitForFunctionAsync(
                        "() => document.title.includes('Daily Report work input batch')");
                    if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                    {
                        return true;
                    }
                    AddLog("Nhấn cho tất cả các item hoàn thành ", LogStatus.Success);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog("Đã xảy ra lỗi trong quá trình vào nhập List PO: "+ex.Message, LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_ScanPO_own(IPage page, List<ListPO> listPo)
        {
            try
            {
                if ((await page.TitleAsync()).Contains("Daily Report work input batch"))
                {
                    await page.ClickAsync("#txtPO");
                    AddLog($"Đã vào {await page.TitleAsync()}", LogStatus.Success);
                    await page.ClickAsync("#lblStatus");

                    string s_result = await page.Locator("#lblDebug").InnerTextAsync();

                    for (int i = 0; i <= s_result.Length; i++)
                    {
                        await page.Keyboard.PressAsync("Backspace");

                    }//xoa toàn bộ label trước đó

                    AddLog("Tiến hành Nhập 1 PO đại điện");

                    await page.Keyboard.TypeAsync(listPo[0].Po);
                    await page.Keyboard.PressAsync("Enter");

                    await Task.Delay(2000);
                    string message = await page.Locator("#txtMsg").InnerTextAsync();
                    if (message.Contains("is locked"))
                    {
                        AddLog(message, LogStatus.Error);
                        return false;
                    }
                    else
                    {
                        AddLog(message, LogStatus.Success);
                        return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog(ex.Message);
                return false;
            }
        }

        private async Task<bool> AT_Operator_Shift(IPage page, string operatorID, string shift)
        {
            try
            {
                AddLog("Tiến hành nhập Operator và Shift");
                if ((await page.TitleAsync()).Contains("Daily Report input Operator"))
                {
                    await Input_msnv_shift(page, operatorID, shift);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog("Đã có lỗi xảy ra ở Nhập Operator và Shift", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_Login_terminalID(IPage page, string s)
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
                AddLog("Tiến hành kiểm tra TerminalID ");

                //cho den khi machineID co gia tri 
                await page.WaitForFunctionAsync(
                         "() => document.getElementById('txtMachineID').value.trim() !== ''");
                string txt_result_machine = await page.Locator("#txtMachineID").InputValueAsync();
                AddLog("Kiểm tra kết quả");

                if (!string.IsNullOrWhiteSpace(txt_result_machine))
                {
                    AddLog("TerminalID oke tiến hành login", LogStatus.Success);
                    //await Task.Delay(2000);
                    await page.ClickAsync("#btnLogin");
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                AddLog("Đã có lỗi xảy ra trong quá trình nhập TerminalID!!", LogStatus.Error);
                return false;
            }
        }

        private async Task<bool> AT_end_PO(IPage page, BatchInputHistory ItemBatch)
        {
            try
            {
                await page.GotoAsync("http://10.4.24.117:8449/LG0100.aspx");
                await page.WaitForFunctionAsync(
                    "() => document.title.includes('Daily Report Login')");

                await _service.UpdateItem(ItemBatch.ID);

                await page.CloseAsync();
                return true;
            }
            catch (Exception ex)
            {
                AddLog("lỗi end PO : " + ex.Message);
                await page.CloseAsync();
                return false;
            }
        }

        private async Task Input_msnv_shift(IPage page, string operatorID, string shift)
        {
            await page.ClickAsync("#txtOperatorID");
            await page.ClickAsync("#txtShiftCD");
            await page.ClickAsync("#lblStatus");

            string s_result = await page.Locator("#lblDebug").InnerTextAsync();

            for (int i = 0; i <= s_result.Length; i++)
            {
                await page.Keyboard.PressAsync("Backspace");
            }//xoa toàn bộ label trước đó
            AddLog("Tiến hành nhập Operator");

            await page.Keyboard.TypeAsync($"{operatorID}");
            await page.Keyboard.PressAsync("Enter");
            AddLog("Tiến hành nhập Shift");
            await page.WaitForFunctionAsync(
            "() => document.getElementById('txtOperatorNM').value.trim() !== ''");

            await page.Keyboard.TypeAsync($"{shift}");
            await page.Keyboard.PressAsync("Enter");

            await page.WaitForFunctionAsync(
            "() => document.getElementById('txtDutyYmd').value.trim() !== ''");

            await page.ClickAsync("#btnArea");
            AddLog("Hoàn thành bước 2", LogStatus.Success);
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
                AddLog($"Đã chụp ảnh màn hình và lưu lại", LogStatus.Success);
            }
            catch (Exception ex)
            {
                AddLog($"Đã xảy ra lỗi khi chụp ảnh màn hình: {ex.Message}", LogStatus.Error);
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
