using MySCADA.Models;
using System.Security.Cryptography;
namespace MySCADA.Services;
public sealed class UserAccountService
{
    private const int Iterations=210000;
    private readonly ScadaDatabaseService _database;
    public UserAccountService(ScadaDatabaseService database)=>_database=database;
    public bool HasUsers=>_database.GetUserCount()>0;
    public IReadOnlyList<UserAccount> GetUsers()=>_database.GetUsers();
    public (bool Success,string Message) CreateUser(string username,string password,UserRole role)
    {
        username=username.Trim();
        if(username.Length<3)return(false,"用户名至少需要3个字符。");
        if(password.Length<8)return(false,"密码至少需要8个字符。");
        if(_database.FindUser(username)!=null)return(false,"用户名已经存在。");
        byte[] salt=RandomNumberGenerator.GetBytes(32);
        byte[] hash=Rfc2898DeriveBytes.Pbkdf2(password,salt,Iterations,HashAlgorithmName.SHA256,32);
        var user=new UserAccount{Username=username,PasswordSalt=Convert.ToBase64String(salt),PasswordHash=Convert.ToBase64String(hash),Role=role,IsEnabled=true,CreatedAt=DateTime.Now,PasswordChangedAt=DateTime.Now};
        _database.InsertUser(user);return(true,"用户创建成功。");
    }
    public (bool Success,string Message,UserAccount? User) Authenticate(string username,string password)
    {
        UserAccount? user=_database.FindUser(username.Trim());
        if(user==null)return(false,"用户名或密码错误。",null);
        if(!user.IsEnabled)return(false,"该用户已被停用。",null);
        byte[] actual=Rfc2898DeriveBytes.Pbkdf2(password,Convert.FromBase64String(user.PasswordSalt),Iterations,HashAlgorithmName.SHA256,32);
        if(!CryptographicOperations.FixedTimeEquals(actual,Convert.FromBase64String(user.PasswordHash)))return(false,"用户名或密码错误。",null);
        user.LastLoginAt=DateTime.Now;_database.UpdateUserLogin(user.Id,user.LastLoginAt.Value);return(true,"登录成功。",user);
    }
    public void SetEnabled(long id,bool enabled)=>_database.SetUserEnabled(id,enabled);
}
