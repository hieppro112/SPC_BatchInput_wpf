using batchInput_wpf.Helper;
using batchInput_wpf.Model;
using Microsoft.Win32;
using System.Diagnostics;
using System.Windows;
using System.Windows.Media;

namespace batchInput_wpf.Views
{
    /// <summary>
    /// Interaction logic for FolderSelectionDialog.xaml
    /// </summary>
    public partial class FolderSelectionDialog : Window
    {
        public string path_img;
        public string path_log;
        private FileHelper _myFile;
        private ItemSaveConfig itemSaveConfig;
        public FolderSelectionDialog()
        {
            InitializeComponent();
            _myFile = new FileHelper();
            itemSaveConfig = _myFile.ReadJson();
            txtFolder1.Text = itemSaveConfig.pathSaveImg;
            txtFolder2.Text = itemSaveConfig.pathLogErr;

        }

        private void BtnBrowse1_Click(object sender, RoutedEventArgs e)
        {
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
            var itemPath = new ItemSaveConfig();
            itemPath.pathLogErr = itemSaveConfig.pathLogErr;
            itemPath.pathSaveImg = itemSaveConfig.pathSaveImg;
            _myFile.WriteJson(itemPath);
            if (!string.IsNullOrEmpty(itemPath.pathLogErr) && !string.IsNullOrEmpty(itemPath.pathSaveImg))
            {
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
            this.Close();
        }
    }
}
