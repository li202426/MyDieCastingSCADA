using System.Configuration;
using System.Data;
using System.Windows;
using MyDieCastingSCADA.Services;
using MySCADA.Services;
namespace MyDieCastingSCADA
{
        public partial class App : Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            if (!ShowLogin())
            {
                Shutdown();
                return;
            }
            new MainWindow().Show();
        }
        private static bool ShowLogin()
            => new LoginWindow().ShowDialog() == true;
        public static void Relogin()
        {
            AuthorizationService.SignOut();
            Window[] oldWindows =
                Current.Windows.Cast<Window>().ToArray();
            foreach (Window window in oldWindows)
            {
                window.Close();
            }
            if (!ShowLogin())
            {
                Current.Shutdown();
                return;
            }
            new MainWindow().Show();
        }
        protected override void OnExit(ExitEventArgs e)
        {
            DatabaseRuntime.Shutdown();
            base.OnExit(e);
        }
    }
}
