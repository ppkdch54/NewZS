using System.ComponentModel;

namespace 新纵撕检测.ViewModels
{
    class MainWindowViewModel : INotifyPropertyChanged
    {
        private Biz biz;
        public Biz Biz
        {
            get { return biz; }
            set
            {
                biz = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Biz"));
            }
        }

        public MainWindowViewModel(MainWindow mainWindow)
        {
            Biz = new Biz(mainWindow);
        }

        public event PropertyChangedEventHandler PropertyChanged;

        public void LoadParams()
        { }

        public void SaveParams()
        { }

        public void LoadResultList()
        { }

        public void SaveResultList()
        { }

        public void GetRectFromScreen()
        { }
    }
}
