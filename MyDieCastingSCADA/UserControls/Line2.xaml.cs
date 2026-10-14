using System.Windows;
using System.Windows.Controls;
namespace MyDieCastingSCADA.UserControls
{
    public partial class Line2 : UserControl
    {
        public Line2()
        {
            InitializeComponent();
        }
        private void PlcButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is string plcTag)
            {
                MessageBox.Show($"正在打开 PLC 监控窗口\n\n设备标识: {plcTag}\n\n（这里以后替换成你真正的监控窗口）",
                              "PLC 连接",
                              MessageBoxButton.OK,
                              MessageBoxImage.Information);
            }
        }
    }
}