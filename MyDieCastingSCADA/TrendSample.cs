namespace MyDieCastingSCADA;
public sealed class TrendSample
{
 public DateTime Time {get;init;}
 public float[] Temperatures {get;init;}=Array.Empty<float>();
 public float AluminiumTemperature {get;init;}
 public float Pressure {get;init;}
}
