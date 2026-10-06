using batchInput_wpf.Helper;
using batchInput_wpf.Model;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace batchInput_wpf.Views
{
    public partial class ConfigWindow : Window
    {
        private static readonly SolidColorBrush AccentOnBrush = new((Color)ColorConverter.ConvertFromString("#00E5FF"));
        private static readonly SolidColorBrush AccentOffBrush = new((Color)ColorConverter.ConvertFromString("#FF9800"));
        private static readonly SolidColorBrush IdleBackBrush = new((Color)ColorConverter.ConvertFromString("#0A0E17"));
        private static readonly SolidColorBrush IdleBorderBrush = new((Color)ColorConverter.ConvertFromString("#1E293B"));
        private static readonly SolidColorBrush IdleForeBrush = new((Color)ColorConverter.ConvertFromString("#64748B"));
        private static readonly SolidColorBrush DarkForeBrush = new((Color)ColorConverter.ConvertFromString("#07090E"));

        private readonly FileHelper _fileHelper;
        // Chinh sua truc tiep tren instance dang chay cua MainViewModel de setting co hieu luc
        // ngay lap tuc, khong can khoi dong lai app.
        private readonly ItemSaveConfig _config;
        private bool _headlessMode;

        public ConfigWindow(ItemSaveConfig config)
        {
            InitializeComponent();
            _fileHelper = new FileHelper();
            _config = config;
            txtDelaySeconds.Text = _config.PollDelaySeconds.ToString();
            txtThreads.Text = _config.MaxThreads.ToString();
            SetHeadlessSelection(_config.HeadlessMode);
        }

        private void txtDelaySeconds_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !e.Text.All(char.IsDigit);
        }

        private void btnHeadlessOn_Click(object sender, RoutedEventArgs e) => SetHeadlessSelection(true);

        private void btnHeadlessOff_Click(object sender, RoutedEventArgs e) => SetHeadlessSelection(false);

        private void SetHeadlessSelection(bool headless)
        {
            _headlessMode = headless;

            btnHeadlessOn.Background = headless ? AccentOnBrush : IdleBackBrush;
            btnHeadlessOn.Foreground = headless ? DarkForeBrush : IdleForeBrush;
            btnHeadlessOn.BorderBrush = headless ? AccentOnBrush : IdleBorderBrush;

            btnHeadlessOff.Background = !headless ? AccentOffBrush : IdleBackBrush;
            btnHeadlessOff.Foreground = !headless ? DarkForeBrush : IdleForeBrush;
            btnHeadlessOff.BorderBrush = !headless ? AccentOffBrush : IdleBorderBrush;
        }

        private void btnOK_Click(object sender, RoutedEventArgs e)
        {
            if (!int.TryParse(txtDelaySeconds.Text, out int seconds) || seconds < 5 || seconds > 21600)
            {
                txt_notify.Text = "Vui lòng nhập số giây hợp lệ (5 - 21600).";
                txt_notify.Foreground = Brushes.OrangeRed;
                return;
            }

            if (!int.TryParse(txtThreads.Text, out int threads) || threads < 1 || threads > 10)
            {
                txt_notify.Text = "Vui lòng nhập số luồng hợp lệ (1 - 10).";
                txt_notify.Foreground = Brushes.OrangeRed;
                return;
            }

            _config.PollDelaySeconds = seconds;
            _config.MaxThreads = threads;
            _config.HeadlessMode = _headlessMode;
            _fileHelper.WriteJson(_config);

            DialogResult = true;
            Close();
        }

        private void btnCancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
