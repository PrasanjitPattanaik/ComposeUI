using System.Windows;

namespace XmlEditorChatApp
{
    public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // Delay showing main window
            System.Threading.Thread.Sleep(3000); // 3 seconds

            var main = new MainWindow();
            main.Show();
        }
    }
}