using batchInput_wpf.Helper;
using System.Windows;

namespace batchInput_wpf
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public App()
        {
            CrashLogger.Init(this);
        }
    }
}
