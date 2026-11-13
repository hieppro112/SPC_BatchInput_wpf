using batchInput_wpf.Helper;
using batchInput_wpf.Model;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;

namespace batchInput_wpf.Views
{
    /// <summary>
    /// Interaction logic for FolderSelectionDialog.xaml
    /// </summary>
    public partial class FolderSelectionDialog : Window
    {
        // Da co san 2 duong dan thi tu dong vao chuong trinh sau so giay nay (bam XAC NHAN de vao ngay)
        private const int AutoEnterSeconds = 30;

        public string path_img;
        public string path_log;
        private FileHelper _myFile;
        private ItemSaveConfig itemSaveConfig;

        private readonly DispatcherTimer _countdownTimer = new() { Interval = TimeSpan.FromSeconds(1) };
        private int _secondsLeft = AutoEnterSeconds;
        private bool _entered;

        public FolderSelectionDialog()
        {
            InitializeComponent();
            _myFile = new FileHelper();
            itemSaveConfig = _myFile.ReadJson();
            txtFolder1.Text = itemSaveConfig.pathSaveImg;
            txtFolder2.Text = itemSaveConfig.pathLogErr;

            _countdownTimer.Tick += CountdownTimer_Tick;
            if (HasValidPaths())
            {
                StartCountdown();
            }
        }

        private bool HasValidPaths() =>
            !string.IsNullOrEmpty(itemSaveConfig.pathLogErr) && !string.IsNullOrEmpty(itemSaveConfig.pathSaveImg);

        #region Dem nguoc tu dong vao
        private void StartCountdown()
        {
            _secondsLeft = AutoEnterSeconds;
            ShowCountdown();
            _countdownTimer.Start();
        }

        private void StopCountdown()
        {
            _countdownTimer.Stop();
        }

        private void ShowCountdown()
        {
            txt_notify.Text = $"⏱ Tự động vào chương trình sau {_secondsLeft} giây — bấm XÁC NHẬN để vào ngay, hoặc chọn lại thư mục để dừng đếm ngược";
            txt_notify.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#00E5FF"));
        }

        private void CountdownTimer_Tick(object? sender, EventArgs e)
        {
            _secondsLeft--;
            if (_secondsLeft <= 0)
            {
                StopCountdown();
                EnterMainWindow();
            }
            else
            {
                ShowCountdown();
            }
        }

        // Nguoi dung dang chon lai thu muc thi dung dem nguoc, de khong tu vao giua chung
        private void CancelCountdownByUser()
        {
            if (!_countdownTimer.IsEnabled) return;
            StopCountdown();
            txt_notify.Text = "Đã dừng đếm ngược. Bấm XÁC NHẬN khi chọn xong.";
            txt_notify.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#64748B"));
        }
        #endregion

        private void BtnBrowse1_Click(object sender, RoutedEventArgs e)
        {
            CancelCountdownByUser();
            var dialog_select_folder = new OpenFolderDialog();
            if (dialog_select_folder.ShowDialog() == true)
            {
                txtFolder1.Text = dialog_select_folder.FolderName;
                path_img = txtFolder1.Text;
                itemSaveConfig.pathSaveImg = path_img;
            }
        }

        private void btnBrowse2_Click(object sender, RoutedEventArgs e)
        {
            CancelCountdownByUser();
            var dialog_select_folder = new OpenFolderDialog();
            if (dialog_select_folder.ShowDialog() == true)
            {
                txtFolder2.Text = dialog_select_folder.FolderName;
                path_log = txtFolder2.Text;
                itemSaveConfig.pathLogErr = path_log;
            }
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();
            EnterMainWindow();
        }

        // Luu duong dan roi vao cua so chinh (dung chung cho nut XAC NHAN va tu dong vao khi het dem nguoc)
        private void EnterMainWindow()
        {
            if (_entered) return;

            var itemPath = new ItemSaveConfig();
            itemPath.pathLogErr = itemSaveConfig.pathLogErr;
            itemPath.pathSaveImg = itemSaveConfig.pathSaveImg;
            itemPath.PollDelaySeconds = itemSaveConfig.PollDelaySeconds;
            itemPath.HeadlessMode = itemSaveConfig.HeadlessMode;
            itemPath.MaxThreads = itemSaveConfig.MaxThreads;
            itemPath.RecordVideo = itemSaveConfig.RecordVideo;
            _myFile.WriteJson(itemPath);
            if (!string.IsNullOrEmpty(itemPath.pathLogErr) && !string.IsNullOrEmpty(itemPath.pathSaveImg))
            {
                _entered = true;
                Debug.WriteLine($"path save IMG: {itemPath.pathSaveImg}\n path log err: {itemPath.pathLogErr}");
                var newScreen = new MainWindow();
                newScreen.Show();
                this.Close();

            }
            else
            {
                txt_notify.Text = "Vui lòng chọn lại đường dẫn.";
                txt_notify.Foreground = new SolidColorBrush(Colors.Red);
            }
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            StopCountdown();
            this.Close();
        }

        protected override void OnClosed(EventArgs e)
        {
            StopCountdown();
            base.OnClosed(e);
        }
    }
}
