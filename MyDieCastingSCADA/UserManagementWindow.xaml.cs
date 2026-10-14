using MyDieCastingSCADA.Services;
using MySCADA.Models;
using MySCADA.Services;
using System.Windows;
using System.Windows.Controls;
namespace MyDieCastingSCADA;
public partial class UserManagementWindow:Window
{
    private readonly UserAccountService _users=new(DatabaseRuntime.Local);
    public UserManagementWindow(){InitializeComponent();Reload();}
    private void Reload()=>UserGrid.ItemsSource=_users.GetUsers();
    private void Create_Click(object sender,RoutedEventArgs e)
    {
        if(!AuthorizationService.Require(AppPermission.ManageUsers,out string error)){MessageText.Text=error;return;}
        string tag=(RoleBox.SelectedItem as ComboBoxItem)?.Tag?.ToString()??"Operator";
        Enum.TryParse(tag,out UserRole role);var result=_users.CreateUser(UsernameBox.Text,PasswordBox.Password,role);
        MessageText.Text=result.Message;if(result.Success){OperationLogService.Success("用户管理","创建用户",$"用户名={UsernameBox.Text.Trim()}；角色={role}");UsernameBox.Clear();PasswordBox.Clear();Reload();}
    }
    private void Toggle_Click(object sender,RoutedEventArgs e)
    {
        if(sender is not Button{Tag:UserAccount user})return;
        if(!AuthorizationService.Require(AppPermission.ManageUsers,out string error)){MessageText.Text=error;return;}
        if(user.Id==AuthorizationService.CurrentUser?.Id){MessageText.Text="不能停用当前登录账号。";return;}
        _users.SetEnabled(user.Id,!user.IsEnabled);OperationLogService.Info("用户管理",user.IsEnabled?"停用用户":"启用用户",user.Username);Reload();
    }
}
