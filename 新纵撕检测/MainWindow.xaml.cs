using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using 新纵撕检测.ViewModels;

namespace 新纵撕检测
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        MainWindowViewModel mainWindowViewModel;
        public MainWindow()
        {
            InitializeComponent();
            mainWindowViewModel = new MainWindowViewModel(this);
            DataContext = mainWindowViewModel;
        }

        private void Image_MouseMove(object sender, MouseEventArgs e)
        {
            //mainWindowViewModel.Biz.DrawRectOnScreen(e.GetPosition(imageC).X, e.GetPosition(imageC).Y);
        }
    }
}
