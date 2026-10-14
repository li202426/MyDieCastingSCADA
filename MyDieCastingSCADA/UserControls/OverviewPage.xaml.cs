using MySCADA.Models;
using MySCADA.Services;
using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using System.Windows.Controls;
namespace MyDieCastingSCADA.UserControls
{
    public partial class OverviewPage : UserControl
    {
        public OverviewPage(PlcDevice device)
        {
            InitializeComponent();
        }
    }
}