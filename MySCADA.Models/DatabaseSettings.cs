namespace MySCADA.Models;
public sealed class DatabaseSettings
{
 public bool SqlServerEnabled{get;set;}=false;
 public string SqlServerConnectionString{get;set;}="Server=.;Database=DATA.1;Integrated Security=True;TrustServerCertificate=True;";
 public int CommandTimeoutSeconds{get;set;}=5;
 public int RunningDataRetentionDays{get;set;}=0;
}
