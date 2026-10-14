using MySCADA.Models;
namespace MySCADA.Services;
public static class AuthorizationService
{
    public static UserAccount? CurrentUser { get; private set; }
    public static event Action? UserChanged;
    public static bool IsAuthenticated=>CurrentUser is { IsEnabled:true };
    public static void SignIn(UserAccount user){CurrentUser=user;UserChanged?.Invoke();}
    public static void SignOut(){CurrentUser=null;UserChanged?.Invoke();}
    public static bool Has(AppPermission permission)
    {
        if(CurrentUser is not { IsEnabled:true } user)return false;
        if(user.Role==UserRole.Administrator)return true;
        return permission switch
        {
            AppPermission.AcknowledgeAlarm=>true,
            AppPermission.ManageProcess=>user.Role==UserRole.Engineer,
            _=>false
        };
    }
    public static bool Require(AppPermission permission,out string error)
    {
        if(Has(permission)){error="";return true;}
        error=$"当前用户“{CurrentUser?.Username??"未登录"}”没有执行此操作的权限。";return false;
    }
}
