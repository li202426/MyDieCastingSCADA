using MyDieCastingSCADA.Services;
using MySCADA.Models;
using MySCADA.Services;
using System.Windows;
namespace MyDieCastingSCADA;
public partial class LoginWindow : Window
{
        private readonly UserAccountService _users =
        new UserAccountService(DatabaseRuntime.Local);
        private readonly bool _firstRun;
        public LoginWindow()
    {
        InitializeComponent();
        _firstRun = !_users.HasUsers;
        if (_firstRun)
        {
            ModeText.Text = "首次运行，请创建第一个管理员账号";
        }
        else
        {
            ModeText.Text = "请使用账号登录";
        }
        if (_firstRun)
        {
            ConfirmPanel.Visibility = Visibility.Visible;
        }
        else
        {
            ConfirmPanel.Visibility = Visibility.Collapsed;
        }
        if (_firstRun)
        {
            SubmitButton.Content = "创建管理员并登录";
        }
        else
        {
            SubmitButton.Content = "登录";
        }
        UsernameBox.Focus();
    }
        private void Submit_Click(
        object sender,
        RoutedEventArgs e)
    {
        ErrorText.Text = "";
        if (_firstRun)
        {
            if (PasswordBox.Password != ConfirmPasswordBox.Password)
            {
                ErrorText.Text = "两次输入的密码不一致。";
                return;
            }
            var created = _users.CreateUser(
                UsernameBox.Text,
                PasswordBox.Password,
                UserRole.Administrator);
            if (!created.Success)
            {
                ErrorText.Text = created.Message;
                return;
            }
        }
        var result = _users.Authenticate(
            UsernameBox.Text,
            PasswordBox.Password);
        if (!result.Success || result.User == null)
        {
            ErrorText.Text = result.Message;
            return;
        }
        AuthorizationService.SignIn(result.User);
        OperationLogService.Success(
            "用户登录",
            "登录成功",
            $"角色={result.User.RoleText}");
        DialogResult = true;
    }
}