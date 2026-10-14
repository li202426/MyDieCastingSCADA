using MahApps.Metro.IconPacks;
using MyDieCastingSCADA.ViewModels;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using MySCADA.Services;
namespace MyDieCastingSCADA
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainViewModel();   
            MainContent.Content = new UserControls.Line1();
            Title = $"压铸监控系统 - {AuthorizationService.CurrentUser?.Username}（{AuthorizationService.CurrentUser?.RoleText}）";
            txtLoggedUser.Text = $"{AuthorizationService.CurrentUser?.Username}（{AuthorizationService.CurrentUser?.RoleText}）";
        }
        private void NavigationButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag != null)
            {
                string pageTag = btn.Tag.ToString();
                switch (pageTag)
                {
                    case "Dashboard":
                        MainContent.Content = new UserControls.Line1();
                        break;
                    case "Line1":
                        MainContent.Content = new UserControls.Line1();
                        break;
                    case "Line2":
                        MainContent.Content = new UserControls.Line2();
                        break;
                }
                ResetButtonStyles();
                btn.Background = (SolidColorBrush)new BrushConverter().ConvertFrom("#5a5080");
            }
        }
        private void ResetButtonStyles()
        {
            btnHome.Background = Brushes.Transparent;
            btnLine1.Background = Brushes.Transparent;
            btnLine2.Background = Brushes.Transparent;
        }
        private void Border_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private void MaxRestoreButton_Click(object sender, RoutedEventArgs e)
        {
            if (this.WindowState == WindowState.Maximized)
            {
                this.WindowState = WindowState.Normal;
                MaxIcon.Kind = PackIconMaterialKind.WindowMaximize;   
            }
            else
            {
                this.WindowState = WindowState.Maximized;
                MaxIcon.Kind = PackIconMaterialKind.WindowRestore;    
            }
        }
        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                this.DragMove();
            }
        }
        private void ExitButton_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }
        private void LogoutButton_Click(object sender,RoutedEventArgs e)
        {
            if(MessageBox.Show("确定退出当前账号吗？","退出登录",MessageBoxButton.YesNo,MessageBoxImage.Question)!=MessageBoxResult.Yes)return;
            MyDieCastingSCADA.Services.OperationLogService.Info("用户退出","退出登录",AuthorizationService.CurrentUser?.Username??"");
            App.Relogin();
        }
    }
}
