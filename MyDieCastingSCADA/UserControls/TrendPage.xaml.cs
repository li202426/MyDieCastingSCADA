using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
namespace MyDieCastingSCADA.UserControls;
public partial class TrendPage : UserControl
{
    private const double LeftMargin = 62;
    private const double TopMargin = 22;
    private const double RightMargin = 68;
    private const double BottomMargin = 38;
    private static readonly Color[] TemperatureColors =
    {
        Color.FromRgb(52, 152, 219), Color.FromRgb(231, 76, 60),
        Color.FromRgb(142, 68, 173), Color.FromRgb(39, 174, 96),
        Color.FromRgb(243, 156, 18), Color.FromRgb(22, 160, 133),
        Color.FromRgb(211, 84, 0), Color.FromRgb(52, 73, 94)
    };
    private static readonly Color AluminiumColor = Color.FromRgb(233, 30, 99);
    private static readonly Color PressureColor = Color.FromRgb(93, 63, 211);
    private readonly ViceWindow _owner;
    private readonly List<CheckBox> _temperatureChecks = new();
    private CheckBox _aluminiumCheck = null!;
    private CheckBox _pressureCheck = null!;
    public TrendPage(ViceWindow owner)
    {
        InitializeComponent();
        _owner = owner;
        BuildSeriesSelector();
        _owner.TrendSamples.CollectionChanged += TrendSamples_CollectionChanged;
        Unloaded += TrendPage_Unloaded;
        Loaded += (_, _) => Draw();
    }
    private void BuildSeriesSelector()
    {
        int count = Math.Max(1, _owner.Temperatures.Count);
        for (int index = 0; index < count; index++)
        {
            CheckBox check = CreateCheckBox(
                $"温度 {index + 1}", TemperatureColors[index % TemperatureColors.Length], index == 0);
            check.Tag = index;
            _temperatureChecks.Add(check);
            SeriesSelector.Children.Add(check);
        }
        _aluminiumCheck = CreateCheckBox("铝液温度", AluminiumColor, true);
        _pressureCheck = CreateCheckBox("压力", PressureColor, true);
        SeriesSelector.Children.Add(_aluminiumCheck);
        SeriesSelector.Children.Add(_pressureCheck);
    }
    private CheckBox CreateCheckBox(string text, Color color, bool isChecked)
    {
        var check = new CheckBox
        {
            Content = text,
            IsChecked = isChecked,
            Foreground = new SolidColorBrush(color),
            FontWeight = FontWeights.SemiBold,
            Margin = new Thickness(0, 1, 16, 1),
            VerticalAlignment = VerticalAlignment.Center
        };
        check.Checked += SeriesSelectionChanged;
        check.Unchecked += SeriesSelectionChanged;
        return check;
    }
    private void TrendSamples_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e) => Draw();
    private void TrendPage_Unloaded(object sender, RoutedEventArgs e)
        => _owner.TrendSamples.CollectionChanged -= TrendSamples_CollectionChanged;
    private void SeriesSelectionChanged(object sender, RoutedEventArgs e) => Draw();
    private void Chart_SizeChanged(object sender, SizeChangedEventArgs e) => Draw();
    private void SelectAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (CheckBox check in AllChecks()) check.IsChecked = true;
        Draw();
    }
    private void ClearAll_Click(object sender, RoutedEventArgs e)
    {
        foreach (CheckBox check in AllChecks()) check.IsChecked = false;
        Draw();
    }
    private IEnumerable<CheckBox> AllChecks()
        => _temperatureChecks.Concat(new[] { _aluminiumCheck, _pressureCheck });
    private void Draw()
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.BeginInvoke(new Action(Draw));
            return;
        }
        double width = Chart.ActualWidth;
        double height = Chart.ActualHeight;
        if (width <= LeftMargin + RightMargin || height <= TopMargin + BottomMargin) return;
        Chart.Children.Clear();
        int[] selectedTemperatures = _temperatureChecks
            .Where(x => x.IsChecked == true)
            .Select(x => (int)x.Tag)
            .ToArray();
        bool showAluminium = _aluminiumCheck.IsChecked == true;
        bool showPressure = _pressureCheck.IsChecked == true;
        bool hasSelection = selectedTemperatures.Length > 0 || showAluminium || showPressure;
        TrendSample[] samples = _owner.TrendSamples.ToArray();
        ChartMessage.Text = !hasSelection ? "请选择至少一条曲线" : "正在等待实时数据...";
        ChartMessage.Visibility = !hasSelection || samples.Length == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
        var temperatureValues = new List<float>();
        foreach (TrendSample sample in samples)
        {
            temperatureValues.AddRange(selectedTemperatures
                .Where(index => index < sample.Temperatures.Length)
                .Select(index => sample.Temperatures[index]));
            if (showAluminium) temperatureValues.Add(sample.AluminiumTemperature);
        }
        temperatureValues.RemoveAll(x => float.IsNaN(x) || float.IsInfinity(x));
        float[] pressureValues = showPressure
            ? samples.Select(x => x.Pressure)
                .Where(x => !float.IsNaN(x) && !float.IsInfinity(x)).ToArray()
            : Array.Empty<float>();
        AxisRange? temperatureAxis = temperatureValues.Count > 0
            ? CalculateAxis(temperatureValues)
            : null;
        AxisRange? pressureAxis = pressureValues.Length > 0
            ? CalculateAxis(pressureValues)
            : null;
        DrawGridAndAxes(width, height, temperatureAxis, pressureAxis, samples);
        if (samples.Length == 0) return;
        foreach (int index in selectedTemperatures)
        {
            DrawSeries(samples,
                sample => index < sample.Temperatures.Length ? sample.Temperatures[index] : null,
                temperatureAxis, TemperatureColors[index % TemperatureColors.Length], width, height);
        }
        if (showAluminium)
            DrawSeries(samples, sample => sample.AluminiumTemperature,
                temperatureAxis, AluminiumColor, width, height);
        if (showPressure)
            DrawSeries(samples, sample => sample.Pressure,
                pressureAxis, PressureColor, width, height);
    }
    private void DrawSeries(
        IReadOnlyList<TrendSample> samples,
        Func<TrendSample, float?> valueSelector,
        AxisRange? axis,
        Color color,
        double width,
        double height)
    {
        if (axis == null) return;
        double plotWidth = width - LeftMargin - RightMargin;
        double plotHeight = height - TopMargin - BottomMargin;
        var points = new PointCollection();
        for (int index = 0; index < samples.Count; index++)
        {
            float? value = valueSelector(samples[index]);
            if (!value.HasValue || float.IsNaN(value.Value) || float.IsInfinity(value.Value)) continue;
            double x = LeftMargin + (samples.Count < 2
                ? plotWidth
                : index * plotWidth / (samples.Count - 1));
            double y = TopMargin + (axis.Maximum - value.Value) /
                (axis.Maximum - axis.Minimum) * plotHeight;
            points.Add(new Point(x, y));
        }
        Chart.Children.Add(new Polyline
        {
            Points = points,
            Stroke = new SolidColorBrush(color),
            StrokeThickness = 2,
            StrokeLineJoin = PenLineJoin.Round
        });
    }
    private void DrawGridAndAxes(
        double width, double height,
        AxisRange? temperatureAxis,
        AxisRange? pressureAxis,
        IReadOnlyList<TrendSample> samples)
    {
        double plotWidth = width - LeftMargin - RightMargin;
        double plotHeight = height - TopMargin - BottomMargin;
        var gridBrush = new SolidColorBrush(Color.FromRgb(220, 225, 233));
        var axisBrush = new SolidColorBrush(Color.FromRgb(125, 132, 145));
        var pressureBrush = new SolidColorBrush(PressureColor);
        const int divisions = 5;
        for (int tick = 0; tick <= divisions; tick++)
        {
            double y = TopMargin + plotHeight - tick * plotHeight / divisions;
            AddLine(LeftMargin, y, width - RightMargin, y, gridBrush, 1, true);
            if (temperatureAxis != null)
            {
                double value = temperatureAxis.Minimum +
                    tick * (temperatureAxis.Maximum - temperatureAxis.Minimum) / divisions;
                AddText(value.ToString("0.#", CultureInfo.InvariantCulture),
                    4, y - 9, LeftMargin - 10, TextAlignment.Right, axisBrush);
            }
            if (pressureAxis != null)
            {
                double value = pressureAxis.Minimum +
                    tick * (pressureAxis.Maximum - pressureAxis.Minimum) / divisions;
                AddText(value.ToString("0.#", CultureInfo.InvariantCulture),
                    width - RightMargin + 7, y - 9, RightMargin - 10,
                    TextAlignment.Left, pressureBrush);
            }
        }
        const int timeDivisions = 6;
        for (int division = 0; division <= timeDivisions; division++)
        {
            double x = LeftMargin + division * plotWidth / timeDivisions;
            AddLine(x, TopMargin, x, height - BottomMargin, gridBrush, 1, true);
        }
        AddLine(LeftMargin, TopMargin, LeftMargin, height - BottomMargin, axisBrush, 1.2, false);
        if (pressureAxis != null)
            AddLine(width - RightMargin, TopMargin, width - RightMargin,
                height - BottomMargin, pressureBrush, 1.2, false);
        AddLine(LeftMargin, height - BottomMargin, width - RightMargin,
            height - BottomMargin, axisBrush, 1.2, false);
        if (temperatureAxis != null)
            AddText("温度 (℃)", 5, 1, 80, TextAlignment.Left, axisBrush);
        if (pressureAxis != null)
            AddText("压力", width - RightMargin + 7, 1,
                RightMargin - 10, TextAlignment.Left, pressureBrush);
        if (samples.Count == 0) return;
        for (int division = 0; division <= timeDivisions; division++)
        {
            int index = (int)Math.Round(division * (samples.Count - 1d) / timeDivisions);
            double x = LeftMargin + division * plotWidth / timeDivisions;
            AddText(samples[index].Time.ToString("HH:mm:ss"),
                x - 31, height - BottomMargin + 7, 62, TextAlignment.Center, axisBrush);
        }
    }
    private void AddLine(
        double x1, double y1, double x2, double y2,
        Brush stroke, double thickness, bool dashed)
    {
        var line = new Line
        {
            X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
            Stroke = stroke, StrokeThickness = thickness,
            IsHitTestVisible = false
        };
        if (dashed) line.StrokeDashArray = new DoubleCollection { 3, 4 };
        Chart.Children.Add(line);
    }
    private void AddText(
        string text, double left, double top, double width,
        TextAlignment alignment, Brush foreground)
    {
        var label = new TextBlock
        {
            Text = text, Width = width, FontSize = 11,
            Foreground = foreground, TextAlignment = alignment,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(label, left);
        Canvas.SetTop(label, top);
        Chart.Children.Add(label);
    }
    private static AxisRange CalculateAxis(IReadOnlyCollection<float> values)
    {
        double rawMinimum = values.Min();
        double rawMaximum = values.Max();
        if (Math.Abs(rawMaximum - rawMinimum) < 0.001)
        {
            double padding = Math.Max(5, Math.Abs(rawMaximum) * 0.1);
            rawMinimum -= padding;
            rawMaximum += padding;
        }
        else
        {
            double padding = (rawMaximum - rawMinimum) * 0.08;
            rawMinimum -= padding;
            rawMaximum += padding;
        }
        double step = NiceStep((rawMaximum - rawMinimum) / 5);
        double minimum = Math.Floor(rawMinimum / step) * step;
        double maximum = Math.Ceiling(rawMaximum / step) * step;
        if (Math.Abs(maximum - minimum) < step) maximum = minimum + step;
        return new AxisRange(minimum, maximum);
    }
    private static double NiceStep(double value)
    {
        if (value <= 0 || double.IsNaN(value) || double.IsInfinity(value)) return 1;
        double power = Math.Pow(10, Math.Floor(Math.Log10(value)));
        double normalized = value / power;
        double factor = normalized <= 1 ? 1 : normalized <= 2 ? 2 : normalized <= 5 ? 5 : 10;
        return factor * power;
    }
    private sealed record AxisRange(double Minimum, double Maximum);
}
