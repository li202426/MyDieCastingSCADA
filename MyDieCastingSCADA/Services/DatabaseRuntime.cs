using MySCADA.Services;
namespace MyDieCastingSCADA.Services;
public static class DatabaseRuntime
{
    private static readonly Lazy<ScadaDatabaseService> LocalValue=new(()=>new ScadaDatabaseService());
    private static readonly Lazy<SqlServerDatabaseService> RemoteValue=new(()=>new SqlServerDatabaseService());
    private static readonly Lazy<SqlSyncService> SyncValue=new(()=>new SqlSyncService(Local,Remote));
    public static ScadaDatabaseService Local=>LocalValue.Value;
    public static SqlServerDatabaseService Remote=>RemoteValue.Value;
    public static SqlSyncService Sync=>SyncValue.Value;
    public static void Shutdown()
    {
        if(SyncValue.IsValueCreated)SyncValue.Value.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
