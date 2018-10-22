using System;
using System.Drawing;
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
        private System.Drawing.Point endPoint;
        private System.Drawing.Point startPoint;
        private bool isDrawing;

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

        private void PropertyGrid_PropertyValueChanged(object s, System.Windows.Forms.PropertyValueChangedEventArgs e)
        {
            mainWindowViewModel.Biz.AsyncSaveObject("AlarmParam", mainWindowViewModel.Biz.AlarmParam);
            mainWindowViewModel.Biz.AsyncSaveObject("DetectParam", mainWindowViewModel.Biz.DetectParam);
            mainWindowViewModel.Biz.AsyncSaveObject("SerialParam", mainWindowViewModel.Biz.SerialParam);
        }

        private void CMD_KeyUp(object sender, KeyEventArgs e)
        {
            if (CMD.Password == "123456")
                SwitchPBVisble();
        }

        private void HideParamWindow(object sender, RoutedEventArgs e)
        {
            pgFormhost.Visibility = Visibility.Collapsed;
            btnGroup.Visibility = Visibility.Collapsed;
            CMD.Password = "";
        }

        private void PreviewBox_MouseMove(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            int x = e.X / 3 * 2;
            int y = e.Y / 3 * 2;
            lbMousePos.Content = "鼠标位置: "+ x+ ", " + y;
            endPoint = new System.Drawing.Point(e.X, e.Y);
            DrawSelectedRect();
        }

        private void PreviewBox_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            startPoint = new System.Drawing.Point(e.X, e.Y);
            isDrawing = true;
        }

        private void PreviewBox_MouseUp(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            isDrawing = false;
            if (CMD.Password != "123456" || Math.Abs(startPoint.Y-endPoint.Y)<=50|| Math.Abs(startPoint.X - endPoint.X) <= 50)
            {
                return;
            }
            if (startPoint.X<endPoint.X)
            {
                mainWindowViewModel.Biz.DetectParam.Left = startPoint.X/3*2;
                mainWindowViewModel.Biz.DetectParam.Right = endPoint.X / 3 * 2;
            }
            else
            {
                mainWindowViewModel.Biz.DetectParam.Left = endPoint.X / 3 * 2;
                mainWindowViewModel.Biz.DetectParam.Right = startPoint.X / 3 * 2;
            }
            if (startPoint.Y<endPoint.Y)
            {
                mainWindowViewModel.Biz.DetectParam.Up = startPoint.Y / 3 * 2;
                mainWindowViewModel.Biz.DetectParam.Down = endPoint.Y / 3 * 2;
            }
            else
            {
                mainWindowViewModel.Biz.DetectParam.Up = endPoint.Y / 3 * 2;
                mainWindowViewModel.Biz.DetectParam.Down = startPoint.Y / 3 * 2;
            }
            mainWindowViewModel.Biz.DetectParam.StartY = (startPoint.Y + endPoint.Y) / 3;
            MessageBox.Show("检测参数已根据框选范围更新到系统中!");
            mainWindowViewModel.Biz.AsyncSaveObject("DetectParam", mainWindowViewModel.Biz.DetectParam);

        }

        void DrawSelectedRect()
        {
            if (!isDrawing)
            {
                return;
            }
            Pen pen = new Pen(Color.LightSkyBlue, 5);
            Rectangle rect = new Rectangle
  (
      Math.Min(startPoint.X, endPoint.X),
      Math.Min(startPoint.Y, endPoint.Y),
      Math.Abs(startPoint.X - endPoint.X),
      Math.Abs(startPoint.Y - endPoint.Y)
  );
            Graphics.FromHwnd(PreviewBox.Handle).DrawRectangle(pen, rect);
        }
    }
}
