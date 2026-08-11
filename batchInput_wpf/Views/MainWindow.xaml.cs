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
