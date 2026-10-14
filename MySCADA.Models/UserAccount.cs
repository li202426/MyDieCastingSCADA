namespace MySCADA.Models;
public enum UserRole { Operator, Engineer, Administrator }
public sealed class UserAccount
{
    public long Id { get; set; }
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string PasswordSalt { get; set; } = "";
    public UserRole Role { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTime CreatedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime PasswordChangedAt { get; set; }
    public string RoleText => Role switch { UserRole.Administrator=>"管理员",UserRole.Engineer=>"工艺员",_=>"操作员" };
}
public enum AppPermission { AcknowledgeAlarm,ManageProcess,ManageDatabase,ManageMolds,ManageUsers }
