using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace WpfAutoUpdate
{
    /// <summary>
    /// ParameterInfo.xaml 的交互逻辑
    /// </summary>
    public partial class ParameterInfo : Window
    {
        private Dictionary<string, string> _dicData;

        public ParameterInfo(string title)
        {
            InitializeComponent();
            Title = title;
        }

        public Dictionary<string, string> DicData
        {
            get { return _dicData; }
            set
            {
                _dicData = value;
                DataGridMain.ItemsSource = _dicData;
            }
        }
    }
}
