//using batchInput_wpf.Model;
//using batchInput_wpf.Service;
//using LiveChartsCore;
//using LiveChartsCore.SkiaSharpView;
//using OpenQA.Selenium;
//using OpenQA.Selenium.Edge;
//using OpenQA.Selenium.Support.UI;
//using System.Collections.ObjectModel;
//using System.ComponentModel;
//using System.Data;
//using System.Diagnostics;
//using System.Runtime.CompilerServices;
//using System.Windows;

//namespace batchInput_wpf.ViewsModel
//{
//    public class MainViewModel2 : BaseViewModel
//    {
//        private readonly BatchAPIService _service;
//        public ObservableCollection<BatchInputHistory> BatchInputHistories { get; set; } = new ObservableCollection<BatchInputHistory>();
//        public ObservableCollection<ListPO> ListPos { get; set; } = new ObservableCollection<ListPO>();

//        public MainViewModel2()
//        {
//            _service = new BatchAPIService();
//            _ = LoadBatchInput();


//            // Khởi tạo dữ liệu biểu đồ
//            Series = new ISeries[]
//            {
//                new ColumnSeries<double>
//                {
//                    Name = "Số lượng",
//                    Values = new double[] { 100, 230, 302, 400, 212, 312, 422 }
//                }
//            };

//            XAxes = new Axis[]
//            {
//                new Axis
//                {
//                    Labels = new string[] { "Thứ 2", "Thứ 3", "Thứ 4", "Thứ 5", "Thứ 6", "Thứ 7", "Chủ nhật" },
//                    LabelsRotation = 0
//                }
//            };

//            YAxes = new Axis[]
//            {
//                new Axis
//                {
//                    MinLimit = 0
//                }
//            };
//        }

//        private async Task LoadBatchInput()
//        {
//            int itemCount = 0;
//            while (true)
//            {
//                var batchInputHistory = new BatchInputHistory();
//                var result = await _service.GetAllProductAsync();
//                //if (itemCount != result.Count)
//                //{
//                BatchInputHistories.Clear();
//                foreach (var item in result)
//                {
//                    if (!item.isComplate)
//                    {
//                        var startLog_at = AddLog("Bắt đầu AutoClick BatchInput item: " + item.ID);
//                        //AddLog("Đang xử lý: " + item.ID);
//                        await event_auto_web(item);
//                        //AddLog("Hoàn thành item: " + item.ID);
//                    }
//                    BatchInputHistories.Add(item);
//                    OnPropertyChanged();
//                }
//                itemCount = result.Count;
//                //}
//                Console.WriteLine("length: " + BatchInputHistories.Count);
//                await Task.Delay(10000);

//            }
//        }

//        //log 
//        private ObservableCollection<LogItem> _log = new ObservableCollection<LogItem>();
//        public ObservableCollection<LogItem> Log
//        {
//            get => _log;
//            set
//            {
//                _log = value;
//                OnPropertyChanged();
//            }
//        }

//        public LogItem AddLog(string message, LogStatus status = LogStatus.Running)
//        {
//            var item = new LogItem()
//            {
//                Message = message,
//                Status = status
//            };

//            Log.Add(item);

//            return item;
//        }

//        private ISeries[] _series;
//        private Axis[] _xAxes;
//        private Axis[] _yAxes;

//        public ISeries[] Series
//        {
//            get => _series;
//            set
//            {
//                _series = value;
//                OnPropertyChanged();
//            }
//        }

//        public Axis[] XAxes
//        {
//            get => _xAxes;
//            set
//            {
//                _xAxes = value;
//                OnPropertyChanged();
//            }
//        }

//        public Axis[] YAxes
//        {
//            get => _yAxes;
//            set
//            {
//                _yAxes = value;
//                OnPropertyChanged();
//            }
//        }


//        #region INotifyPropertyChanged Implementation
//        public event PropertyChangedEventHandler PropertyChanged;
//        protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
//        {
//            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
//        }
//        #endregion

//        #region bat dau autoclick
//        private async Task<bool> event_auto_web(BatchInputHistory itemBatch)
//        {
//            bool found_tab = false;

//            try
//            {
//                string appFolder = AppDomain.CurrentDomain.BaseDirectory;
//                //MessageBox.Show("App folder: " + appFolder);
//                // 2. Ép Selenium sử dụng file msedgedriver.exe nằm ngay trong thư mục này
//                EdgeDriverService service = EdgeDriverService.CreateDefaultService(appFolder);
//                service.HideCommandPromptWindow = true;

//                EdgeOptions options = new EdgeOptions();//cau hinh edg 
//                options.DebuggerAddress = "127.0.0.1:9222";
//                //Selenium.IWebDriver driver = new EdgeDriver(options);//cau hinh cho driver edg 
//                OpenQA.Selenium.IWebDriver driver = new EdgeDriver(service, options);//cau hinh cho driver edg 
//                //driver.Manage().Window.Minimize();

//                //lay cac cua so 
//                var windowHandles = driver.WindowHandles;
//                foreach (var handle in windowHandles)
//                {
//                    driver.SwitchTo().Window(handle);//chuyen sang tab
//                    //kiem tra title cua tab
//                    if (driver.Title.Contains("Daily Report Login"))
//                    {
//                        found_tab = true;
//                        WebDriverWait wait = new WebDriverWait(driver, TimeSpan.FromSeconds(10));//doi web load khoang 7s
//                        try
//                        {
//                            if (await AT_Login_terminalID(wait, driver, "TR" + itemBatch.TerminalID) == true)
//                            {
//                                Await_loadWeb(wait);
//                                if (await AT_Operator_Shift(wait, driver, itemBatch.Msnv, itemBatch.Shift) == true)
//                                {
//                                    Await_loadWeb(wait);
//                                    if (await AT_ScanPO_own(wait, driver, itemBatch.ListPO))
//                                    {
//                                        //Await_loadWeb(wait);
//                                        await Task.Delay(3000);
//                                        //await AT_Click_list_PO(wait, driver, itemBatch.ListPO);
//                                        bool IscheckList = await AT_Click_list_PO(wait, driver, itemBatch.ListPO);
//                                        if (IscheckList && itemBatch.start)
//                                        {
//                                            Await_loadWeb(wait);
//                                            if (await AT_Click_start(wait, driver, itemBatch.Msnv))
//                                            {
//                                                Await_loadWeb(wait);
//                                                if (await AT_Click_startAll(wait, driver, itemBatch.Msnv))
//                                                {
//                                                    AddLog("Hoàn thành Start item: " + itemBatch.ID, LogStatus.Success);
//                                                    await AT_end_PO(wait, driver,itemBatch);
//                                                }
//                                                else
//                                                {
//                                                    AddLog("Đã có lỗi khi chạy start All ", LogStatus.Error);
//                                                }
//                                            }
//                                            else
//                                            {
//                                                AddLog("Không vào được nơi start ", LogStatus.Error);
//                                            }
//                                        }
//                                        else if (IscheckList && !itemBatch.start)
//                                        {
//                                            if(await AT_Click_Finish(wait, driver)) {
//                                                await AT_end_PO(wait, driver,itemBatch);

//                                            }
//                                        }
//                                        else
//                                        {
//                                            AddLog("Đã có lỗi tại nhập list PO ", LogStatus.Error);
//                                            //AT_end_PO(wait, driver);
//                                        }
//                                    }
//                                    else
//                                    {
//                                        AddLog("Đã có lỗi tại nhập PO đại diện bước 3", LogStatus.Error);
//                                    }
//                                }
//                                else
//                                {
//                                    AddLog("Đã có lỗi tại nhập Operator ID bước 2", LogStatus.Error);
//                                }
//                            }
//                            else
//                            {
//                                AddLog("Đã có lỗi tại nhập Terminal ID bước 1",LogStatus.Error);
//                            }
//                            return false;
//                        }
//                        catch (Exception ex)
//                        {
//                            AddLog("Đã có lỗi xảy ra :" + ex);
//                        }
//                        finally
//                        {
//                            driver.Quit();
//                        }
//                    }
//                    else
//                    {
//                        //this.Activate();
//                        //MessageBox.Show("Không tìm  thấy cửa sổ ");
//                        AddLog("lần này chưa tìm thăy");

//                    }

//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                MessageBox.Show("Đã có lỗi xảy ra :" + ex);
//                return false;
//            }
//        }

//        private async Task<bool> AT_Click_Finish(WebDriverWait wait, IWebDriver driver)
//        {
//            try
//            {
//                _ = wait.Until(d =>
//                    ((IJavaScriptExecutor)d)
//                    .ExecuteScript("return document.readyState")
//                    .Equals("complete"));
//                await Task.Delay(5000);

//                Debug.WriteLine(driver.Title);
//                Debug.WriteLine(driver.Url);
//                AddLog("Tiến hành Finish All PO ", LogStatus.Success);
//                var list = driver.FindElements(By.Id("lstBatchSub_selButton_1"));
//                Debug.WriteLine($"Count = {list.Count}");
//                await Task.Delay(3000);
//                OpenQA.Selenium.IWebElement Click_po_list = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lstBatchSub_selButton_1")));//tim ô này
//                Click_po_list.Click();
//                await Task.Delay(3000);
//                OpenQA.Selenium.IWebElement ClickOperator = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnAreaFinishAll")));//tim ô này
//                ClickOperator.Click();
//                AddLog("Hoàn thành Finish All PO ");
//                return true;
//            }
//            catch (Exception ex)
//            {
//                AddLog($"Đã xảy ra lỗi trong quá trình vào nhập Operator: {ex.Message}", LogStatus.Error);
//                return false;
//            }
//        }

//        private async Task<bool> AT_Click_startAll(WebDriverWait wait, IWebDriver driver, string msnv)
//        {
//            try
//            {
//                _ = wait.Until(d =>
//                    ((IJavaScriptExecutor)d)
//                    .ExecuteScript("return document.readyState")
//                    .Equals("complete"));
//                AddLog("Tiến hành start All");
//                await Task.Delay(3000);
//                OpenQA.Selenium.IWebElement ClickMyver = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("MyVersion")));//tim ô này
//                ClickMyver.Click();
//                await Task.Delay(1000);
//                AddLog("Tiến hành nhập Operator");
//                driver.SwitchTo().ActiveElement().SendKeys(msnv);//nhap mã vao operator
//                driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);
                
//                await Task.Delay(2000);
//                OpenQA.Selenium.IWebElement ClickStartAll = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnAreaStart")));//tim ô này
//                ClickStartAll.Click();

//                await Task.Delay(2000);
//                OpenQA.Selenium.IWebElement ClickApply = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnApply")));//tim ô này
//                ClickApply.Click();

//                if (driver.Title.Contains("Daily Report work input batch"))
//                {
//                    AddLog("Hoàn thành Start ", LogStatus.Success);

//                    return true;
//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                AddLog("Đã xảy ra lỗi trong quá trình vào nhập Operator: "+ex.Message, LogStatus.Error);
//                return false;
//            }
//        }

//        private async Task<bool> AT_Click_start(WebDriverWait wait, IWebDriver driver, string msnv)
//        {
//            try
//            {
//                _ = wait.Until(d =>
//                    ((IJavaScriptExecutor)d)
//                    .ExecuteScript("return document.readyState")
//                    .Equals("complete"));

//                AddLog("Tiến hành vào input Operator ");
//                await Task.Delay(3000);
//                OpenQA.Selenium.IWebElement Click_po_list = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lstBatchSub_selButton_1")));//tim ô này
//                Click_po_list.Click();
//                await Task.Delay(3000);
//                OpenQA.Selenium.IWebElement ClickOperator = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnInputOperatorList")));//tim ô này
//                ClickOperator.Click();
//                await Task.Delay(3000);
//                if (driver.Title.Contains("Daily Report work input Operator List"))
//                {
//                    AddLog("Hoàn thành vào input Operator ", LogStatus.Success);
//                    return true;
//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                AddLog("Đã xảy ra lỗi trong quá trình vào nhập Operator", LogStatus.Error);
//                return false;
//            }
//        }

//        private async Task<bool> AT_Click_list_PO(WebDriverWait wait, OpenQA.Selenium.IWebDriver driver, List<ListPO> listPo)
//        {
//            try
//            {
//                Await_loadWeb(wait);
//                OpenQA.Selenium.IWebElement Click_po_list = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnInputPOList")));//tim ô này
//                Click_po_list.Click();
//                await Task.Delay(2000);
//                Await_loadWeb(wait);
//                if (driver.Title.Contains("Daily Report work input po list"))
//                {
//                    AddLog("Đã vào trang nhập PO list");
//                    Await_loadWeb(wait);
//                    OpenQA.Selenium.IWebElement ClickMyver = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("MyVersion")));//tim ô này
//                    ClickMyver.Click();
//                    Await_loadWeb(wait);
//                    AddLog("Tiến hành nhập PO list");

//                    for (int i = 1; i < listPo.Count; i++)
//                    {
//                        await Task.Delay(1000);
//                        AddLog($"Tiến hành nhập PO thứ {i}: {listPo[i].Po}" + i);
//                        driver.SwitchTo().ActiveElement().SendKeys(listPo[i].Po);
//                        driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);

//                        Await_loadWeb(wait);
//                        wait.Until(d =>
//                        {
//                            var list = d.FindElements(By.Id("txtMsg"));
//                            return list.Count > 0;
//                        });

//                        string messageInputPO = wait.Until(d =>
//                        {
//                            try
//                            {
//                                var elements = d.FindElements(By.Id("txtMsg"));

//                                if (elements.Count == 0)
//                                    return null;

//                                string text = elements[0].Text.Trim();

//                                return string.IsNullOrWhiteSpace(text) ? null : text;
//                            }
//                            catch (StaleElementReferenceException)
//                            {
//                                return null;
//                            }
//                        });

//                        Debug.WriteLine(messageInputPO);
//                        if (messageInputPO.Contains("is locked"))
//                        {
//                            AddLog(messageInputPO + listPo[i].Po, LogStatus.Error);
//                            return false;
//                        }
//                        else
//                        {
//                            continue;
//                        }
//                    }

//                    AddLog("Tiến hành nhấn cho tất cả các item");
//                    OpenQA.Selenium.IWebElement ClickAll = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnAllQtyActCopytoTotal")));//tim ô này
//                    ClickAll.Click();
//                    Await_loadWeb(wait);
//                    OpenQA.Selenium.IWebElement ClickReadAll = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnPOReadFinish")));//tim ô này
//                    ClickReadAll.Click();
//                    //await Task.Delay(2000);
//                    //OpenQA.Selenium.IWebElement ClickHome = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnCancel")));//tim ô này
//                    //ClickHome.Click();
//                    Await_loadWeb(wait);
//                    if (driver.Title.Contains("Daily Report work input batch"))
//                    {
//                        return true;
//                    }
//                    AddLog("Nhấn cho tất cả các item hoàn thành ", LogStatus.Success);
//                    return true;
//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                AddLog("Đã xảy ra lỗi trong quá trình vào nhập List PO", LogStatus.Error);
//                return false;
//            }
//            finally
//            {
//                //AddLog("Nhấn cho tất cả các item hoàn thành ", LogStatus.Success);
//            }
//        }

//        private async Task<bool> AT_ScanPO_own(WebDriverWait wait, OpenQA.Selenium.IWebDriver driver, List<ListPO> listPo)
//        {
//            try
//            {
//                if (driver.Title.Contains("Daily Report work input batch"))
//                {
//                    AddLog($"Đã vào {driver.Title}", LogStatus.Success);
//                    OpenQA.Selenium.IWebElement clickVer = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lblStatus")));//tim ô này
//                    clickVer.Click();

//                    OpenQA.Selenium.IWebElement label_result = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lblDebug")));//tim ô này
//                    string s_result = label_result.Text;

//                    for (int i = 0; i <= s_result.Length; i++)
//                    {
//                        driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Backspace);
//                        await Task.Delay(50);

//                    }//xoa toàn bộ label trước đó

//                    await Task.Delay(2000);
//                    AddLog("Tiến hành Nhập 1 PO đại điện");

//                    driver.SwitchTo().ActiveElement().SendKeys(listPo[0].Po);//nhap PO dai dien           
//                    driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);

//                    string message = wait.Until(driver =>
//                    {
//                        var text = driver.FindElement(By.Id("txtMsg")).Text;
//                        return string.IsNullOrWhiteSpace(text) ? null : text;
//                    });
//                    if (message.Contains("is locked"))
//                    {
//                        AddLog(message, LogStatus.Error);
//                        return false;
//                    }
//                    else
//                    {
//                        AddLog(message, LogStatus.Success);
//                        return true;
//                    }
//                    Console.WriteLine(message);
//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                AddLog(ex.Message);
//                return false;
//            }
//        }

//        private async Task<bool> AT_Operator_Shift(WebDriverWait wait, OpenQA.Selenium.IWebDriver driver, string operatorID, string shift)
//        {
//            try
//            {
//                AddLog("Tiến hành nhập Operator và Shift");
//                if (driver.Title.Contains("Daily Report input Operator"))
//                {
//                    await Input_msnv_shift(wait, driver, operatorID, shift);
//                    return true;
//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                AddLog("Đã có lỗi xảy ra ở Nhập Operator và Shift", LogStatus.Error);
//                return false;
//            }
//        }

//        private async Task<bool> AT_Login_terminalID(WebDriverWait wait, OpenQA.Selenium.IWebDriver driver, string s)
//        {
//            try
//            {
//               var checkLoadingWeb = wait.Until(d =>
//                    ((IJavaScriptExecutor)d)
//                    .ExecuteScript("return document.readyState")
//                    .Equals("complete"));

//                OpenQA.Selenium.IWebElement clickVer = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lblStatus")));//tim ô này
//                clickVer.Click();

//                OpenQA.Selenium.IWebElement label_result = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lblDebug")));//tim ô này
//                string s_result = label_result.Text;
//                Console.WriteLine("s: " + s_result);

//                for (int i = 0; i <= s_result.Length; i++)
//                {
//                    driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Backspace);
//                    System.Threading.Thread.Sleep(50);

//                }//xoa toàn bộ label trước đó

//                driver.SwitchTo().ActiveElement().SendKeys(s);//nhap mã máy
//                //driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);
//                driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);
//                AddLog("Tiến hành nhập TerminalID ");
//                string machineId = wait.Until(driver =>
//                {
//                    var elements = driver.FindElements(By.Id("txtMachineID"));

//                    if (elements.Count == 0)
//                        return null;
//                    var value = elements[0].GetAttribute("value");
//                    return string.IsNullOrWhiteSpace(value) ? null : value;
//                });
//                AddLog("Kiểm tra kết quả");

//                string txt_result_machine = machineId;
//                if (!string.IsNullOrWhiteSpace(txt_result_machine))
//                {
//                    AddLog("TerminalID oke tiến hành login", LogStatus.Success);
//                    OpenQA.Selenium.IWebElement btn_login = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnLogin")));//nếu terminal id nhập đúng thì ô này sẽ có giá trị
//                    btn_login.Click();
//                    return true;
//                }
//                return false;
//            }
//            catch (Exception ex)
//            {
//                AddLog("Đã có lỗi xảy ra trong quá trình nhập TerminalID!!", LogStatus.Error);
//                return false;
//            }
//        }

//        private async Task<bool> AT_end_PO(WebDriverWait wait, OpenQA.Selenium.IWebDriver driver, BatchInputHistory ItemBatch)
//        {
//            try
//            {
//                //OpenQA.Selenium.IWebElement clickReturn = wait.Until(d =>
//                //{
//                //    var element = d.FindElements(OpenQA.Selenium.By.Id("btnReturn"));
//                //    return element.Count > 0 ? element[0] : null;
//                //});
//                //clickReturn.Click();
//                //Await_loadWeb(wait);


//                //if (driver.Title.Contains("Daily Report work input batch"))
//                //{
//                //    clickReturn.Click();
//                //}
//                //else if (driver.Title.Contains("Daily Report input Operator"))//btnReturn
//                //{
//                //    await driver.Navigate().RefreshAsync();
//                //}
//                //await _service.UpdateItem(ItemBatch.ID);
//                driver.Navigate().GoToUrl("http://10.4.24.117:8449/LG0100.aspx");

//                wait.Until(d =>
//                    ((IJavaScriptExecutor)d)
//                        .ExecuteScript("return document.readyState")
//                        .Equals("complete"));
//                return true;
//            }
//            catch (Exception ex)
//            {
//                AddLog("lỗi b2: " + ex.Message);
//                return false;
//            }
//        }

//        private async Task Input_msnv_shift(WebDriverWait wait, OpenQA.Selenium.IWebDriver driver, string operatorID, string shift)
//        {
//            OpenQA.Selenium.IWebElement clickVer = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lblStatus")));//tim ô này
//            clickVer.Click();

//            OpenQA.Selenium.IWebElement label_result = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("lblDebug")));//tim ô này
//            string s_result = label_result.Text;

//            for (int i = 0; i <= s_result.Length; i++)
//            {
//                driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Backspace);
//                await Task.Delay(500);

//            }//xoa toàn bộ label trước đó
//            await Task.Delay(1000);
//            AddLog("Tiến hành nhập Operator");
//            driver.SwitchTo().ActiveElement().SendKeys(operatorID);//nhap mã vao operator
//            driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);

//            clickVer.Click();
//            await Task.Delay(2000);
//            AddLog("Tiến hành nhập Shift");
//            driver.SwitchTo().ActiveElement().SendKeys(shift);//nhap mã vào Shift
//            driver.SwitchTo().ActiveElement().SendKeys(OpenQA.Selenium.Keys.Enter);

//            Await_loadWeb(wait);
//            await Task.Delay(3000);
//            OpenQA.Selenium.IWebElement btn_login = wait.Until(d => d.FindElement(OpenQA.Selenium.By.Id("btnArea")));
//            btn_login.Click();
//            AddLog("Tiến hành nhấn vào Batch", LogStatus.Success);
//        }

//        private async void Await_loadWeb(WebDriverWait wait)
//        {
//            wait.Until(d =>
//                    ((IJavaScriptExecutor)d)
//                    .ExecuteScript("return document.readyState")
//                    .Equals("complete"));
//        }

//        #endregion
//    }

//}
