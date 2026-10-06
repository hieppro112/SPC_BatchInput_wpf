using batchInput_wpf.ViewsModel;
using Microsoft.Win32;
using System.Diagnostics;
using System.IO;
using System.Windows;

namespace batchInput_wpf.Views
{
    public partial class MainWindow : Window
    {
        private MainViewModel _binding = new MainViewModel();
        public MainWindow()
        {
            InitializeComponent();
            DataContext = _binding;
            Debug.WriteLine("CONSTRUCTOR VM: " + _binding.GetHashCode());
            LoadData();

        }

        private void LoadData()
        {
            // Called from constructor, but InitializeAsync is called from Window_Loaded event
        }

        

        private void btn_path_img_Click(object sender, RoutedEventArgs e)
        {
            var dialog_selectt_folder = new OpenFolderDialog();
            if (dialog_selectt_folder.ShowDialog() == true)
            {
                _binding.SelectedFolder_logerr = dialog_selectt_folder.FolderName;
            }
        }

        private void btn_Pause_Click(object sender, RoutedEventArgs e)
        {
            _binding.IsRunning = false;
        }

        private void btn_config_Click(object sender, RoutedEventArgs e)
        {
            // Truyen thang instance _itemSaveConfig dang chay de setting co hieu luc ngay,
            // khong can khoi dong lai app.
            var configWindow = new ConfigWindow(_binding._itemSaveConfig)
            {
                Owner = this
            };
            configWindow.ShowDialog();
        }

        private void btn_start_Click(object sender, RoutedEventArgs e)
        {
            _binding.IsRunning = true;
        }

        private async void Window_Loaded(object sender, RoutedEventArgs e)
        {
            await _binding.InitializeAsync();   
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {

        }

        private async void btn_page_first_Click(object sender, RoutedEventArgs e)
        {
            await _binding.GoToFirstPage();
        }

        private async void btn_page_prev_Click(object sender, RoutedEventArgs e)
        {
            await _binding.GoToPreviousPage();
        }

        private async void btn_page_next_Click(object sender, RoutedEventArgs e)
        {
            await _binding.GoToNextPage();
        }

        private async void btn_page_last_Click(object sender, RoutedEventArgs e)
        {
            await _binding.GoToLastPage();
        }

        private void btn_lib_Click(object sender, RoutedEventArgs e)
        {
            string path_img = _binding._itemSaveConfig.pathSaveImg;
            if (Directory.Exists(path_img))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = path_img,
                    UseShellExecute = true,
                });
            }
            else
            {
                MessageBox.Show("Folder does not exist: " + path_img);
            }
        }
    }
}
