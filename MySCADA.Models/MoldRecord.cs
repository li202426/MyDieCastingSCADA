namespace MySCADA.Models;
public class MoldRecord
{
    public long Id { get; set; }
    public string MoldNumber { get; set; } = "";
    public string ProductName { get; set; } = "";
    public string DrawingNumber { get; set; } = "";
    public string Remark { get; set; } = "";
    public DateTime UpdatedAt { get; set; } = DateTime.Now;
    public string UpdatedAtText => UpdatedAt.ToString("yyyy-MM-dd HH:mm:ss");
}
