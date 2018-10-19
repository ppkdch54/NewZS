using System.Windows;
using System.Windows.Input;
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

        private void SwitchSerialParam(object sender, RoutedEventArgs e)
        {
            if (!mainWindowViewModel.Biz.propertyGrid.SelectedObject.Equals(mainWindowViewModel.Biz.SerialParam))
            {
                mainWindowViewModel.Biz.propertyGrid.SelectedObject = mainWindowViewModel.Biz.SerialParam;
            }
        }

        private void SwitchDetectParam(object sender, RoutedEventArgs e)
        {
            if (!mainWindowViewModel.Biz.propertyGrid.SelectedObject.Equals( mainWindowViewModel.Biz.DetectParam))
            {
                mainWindowViewModel.Biz.propertyGrid.SelectedObject = mainWindowViewModel.Biz.DetectParam;
            }
        }

        private void SwitchAlarmParam(object sender, RoutedEventArgs e)
        {
            if (!mainWindowViewModel.Biz.propertyGrid.SelectedObject.Equals( mainWindowViewModel.Biz.AlarmParam))
            {
                mainWindowViewModel.Biz.propertyGrid.SelectedObject = mainWindowViewModel.Biz.AlarmParam;
            }
        }

        private void SwitchPBVisble()
        {
            switch (pgFormhost.Visibility)
            {
                case Visibility.Visible:
                    pgFormhost.Visibility = Visibility.Collapsed;
                    btnGroup.Visibility = Visibility.Collapsed;
                    break;
                case Visibility.Hidden:
                    break;
                case Visibility.Collapsed:
                    pgFormhost.Visibility = Visibility.Visible;
                    btnGroup.Visibility = Visibility.Visible;
                    break;
                default:
                    break;
            }
        }

        private void TextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            switch (CMD.Text)
            {
                case "123456":
                    SwitchPBVisble();
                    break;
                default:
                    break;
            }
        }
    }
}
