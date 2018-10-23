using Emgu.CV;
using Emgu.CV.Structure;
using MVSDK;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using System.Windows.Media.Imaging;
using 新纵撕检测.Models;
using 纵撕检测.Models;

namespace 新纵撕检测.ViewModels
{
    class Biz:INotifyPropertyChanged
    {
        private VideoCapture capture;
        private IntPtr m_Grabber;

        static Size ImageSize = new Size(640, 480);
        pfnCameraGrabberFrameCallback m_FrameCallback;
        public event PropertyChangedEventHandler PropertyChanged;

        public bool DetectFlag { get; set; } = true;
        public bool IsAlarm { get; set; }
        private Image<Bgr, Byte> Image = new Image<Bgr, Byte>(ImageSize);
        public ConcurrentQueue<Image<Bgr, Byte>> ImageQueue { get; set; } = new ConcurrentQueue<Image<Bgr, byte>>();
        private class AlarmPos
        {
            public int XPos { get; set; }
            public int YPos { get; set; }
            int pixRange;
            public AlarmPos(int xPos, int yPos, int range)
            {
                XPos = xPos;
                YPos = yPos;
                pixRange = range;
            }
            public override bool Equals(object obj)
            {
                var tmp = obj as AlarmPos;
                if (Math.Abs(tmp.XPos - XPos) <= pixRange)
                {
                    return true;
                }
                return false;
            }
        }
        ConcurrentDictionary<AlarmPos, DetectResultStatistic> detectResultStatistics = new ConcurrentDictionary<AlarmPos, DetectResultStatistic>();

        private ObservableCollection<AlarmRecord> alarms;
        public ObservableCollection<AlarmRecord> Alarms
        {
            get { return alarms; }
            set
            {
                alarms = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Alarms"));
            }
        }
        private BitmapImage previewImage;
        public BitmapImage PreviewImage {
            get { return previewImage; }
            set {
                previewImage = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("PreviewImage"));
            }
        }
        private System.Windows.Thickness margin;
        public System.Windows.Thickness Margin
        {
            get { return margin; }
            set
            {
                margin = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Margin"));
            }
        }
        private int frameCount;
        public int FrameCount
        {
            get
            {
                return frameCount;
            }
            set
            {
                frameCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("FrameCount"));
            }
        }

        private int CapCount;
        private int LoopOffset = 0;
        private bool AlarmHeadFlag = true;
        private AlarmRecord firstAlarmRecord = null;
        private SerialComm serialComm;

        private PictureBox pictureBox;
        public PropertyGrid propertyGrid;

        private int currentLoopCount;
        public int CurrentLoopCount
        {
            get { return currentLoopCount; }
            set
            {
                currentLoopCount = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("CurrentLoopCount"));
            }
        }
        public DetectParam DetectParam { get; set; }
        private StDetectParam stDetectParam = new StDetectParam();
        public SerialParam SerialParam { get; set; }
        public AlarmParam AlarmParam { get; set; }
        private DetectResultState detectState;
        public DetectResultState DetectState
        {
            get { return detectState; }
            set
            {
                detectState = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("DetectState"));
            }
        }
        private int selectedAlarmIndex;
        public int SelectedAlarmIndex
        {
            get { return selectedAlarmIndex; }
            set
            {
                selectedAlarmIndex = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("SelectedAlarmIndex"));
            }
        }

        public Biz(MainWindow mainWindow)
        {
            pictureBox = mainWindow.PreviewBox;
            propertyGrid = mainWindow.PropertyGrid;

            if (InitCamera())
            {
                mainWindow.imageC.Visibility = System.Windows.Visibility.Collapsed;
                mainWindow.RectBorder.Visibility= System.Windows.Visibility.Collapsed;
            }
            else
            {
                InitLocalCamera();
                mainWindow.pbFormhost.Visibility = System.Windows.Visibility.Collapsed;
            }
            
            LoadParams();
            AlarmRecord.TotalLoopCount = AlarmParam.TotalLoopCount;
            serialComm = new SerialComm(SerialParam,DetectParam.CameraNo);
            propertyGrid.SelectedObject = DetectParam;
            StartDetect();
            Task.Run(() =>
            {
                Algorithm.svm_start();
                AutoDeletePics();
            });

        }

        private void AutoDeletePics()
        {
            System.Timers.Timer pTimer = new System.Timers.Timer(24 * 3600 * 1000);
            pTimer.Elapsed += PTimer_Elapsed;
            pTimer.Start();
            PTimer_Elapsed(null, null);
        }

        private void PTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            List<string> excludePath = new List<string>();
            for (int i = 0; i < AlarmParam.ReservedDays; i++)
            {
                excludePath.Add("C:\\Alarm_Pic\\pic_" + DateTime.Today.AddDays(-i).ToString("yyyy_MM_dd"));
            }
            string srcPath = "C:\\Alarm_Pic\\";
            if (!Directory.Exists(srcPath))
            {
                return;
            }
            try
            {
                DirectoryInfo dir = new DirectoryInfo(srcPath);
                FileSystemInfo[] fileinfo = dir.GetFileSystemInfos();  //返回目录中所有文件和子目录
                foreach (FileSystemInfo i in fileinfo)
                {
                    if (i is DirectoryInfo)            //判断是否文件夹
                    {
                        bool deleteFlag = true;
                        foreach (var path in excludePath)
                        {
                            if (i.FullName.Contains(path))
                            {
                                deleteFlag = false;
                                break;
                            }
                        }
                        if (deleteFlag)
                        {
                            DirectoryInfo subdir = new DirectoryInfo(i.FullName);
                            subdir.Delete(true);          //删除子目录和文件
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        private void LoadParams()
        {
            {
                string fp = Application.StartupPath + "\\AlarmParam.json";
                if (!File.Exists(fp))  // 判断是否已有相同文件 
                {
                    FileStream fs = new FileStream(fp, FileMode.Create, FileAccess.ReadWrite);
                    fs.Close();
                }
                AlarmParam = JsonConvert.DeserializeObject<AlarmParam>(File.ReadAllText(fp));
                if (AlarmParam == null)
                {
                    //读取bak文件
                    string fp1 = Application.StartupPath + "\\AlarmParam_bak.json";
                    if (!File.Exists(fp1))  // 判断是否已有相同文件 
                    {
                        FileStream fs = new FileStream(fp1, FileMode.Create, FileAccess.ReadWrite);
                        fs.Close();
                    }
                    AlarmParam = JsonConvert.DeserializeObject<AlarmParam>(File.ReadAllText(fp1));
                    if (AlarmParam == null)
                    {
                        AlarmParam = new AlarmParam { MaxHurtDistance = 5, MaxDivDistance = 2, MaxErrorDistance = 1.5f, PixelRange=20, ReservedDays=7, TotalLoopCount=0, Velocity=5, YRange=10 };
                    }
                }
            }
            {
                string fp = Application.StartupPath + "\\SerialParam.json";
                if (!File.Exists(fp))  // 判断是否已有相同文件 
                {
                    FileStream fs = new FileStream(fp, FileMode.Create, FileAccess.ReadWrite);
                    fs.Close();
                }
                SerialParam = JsonConvert.DeserializeObject<SerialParam>(File.ReadAllText(fp));
                if (SerialParam == null)
                {
                    //读取bak文件
                    string fp1 = Application.StartupPath + "\\SerialParam_bak.json";
                    if (!File.Exists(fp1))  // 判断是否已有相同文件 
                    {
                        FileStream fs = new FileStream(fp1, FileMode.Create, FileAccess.ReadWrite);
                        fs.Close();
                    }
                    SerialParam = JsonConvert.DeserializeObject<SerialParam>(File.ReadAllText(fp1));
                    if (SerialParam == null)
                    {
                        SerialParam = new SerialParam
                        {
                            PortName = "COM1",
                            BaudRate = 9600,
                            Parity = System.IO.Ports.Parity.None,
                            StopBits = System.IO.Ports.StopBits.One,
                            DataBits = 8
                        };
                    }
                }
            }
            {
                string fp = Application.StartupPath + "\\DetectParam.json";
                if (!File.Exists(fp))  // 判断是否已有相同文件 
                {
                    FileStream fs = new FileStream(fp, FileMode.Create, FileAccess.ReadWrite);
                    fs.Close();
                }
                DetectParam = JsonConvert.DeserializeObject<DetectParam>(File.ReadAllText(fp));
                if (DetectParam == null)
                {
                    //读取bak文件
                    string fp1 = Application.StartupPath + "\\SerialParam_bak.json";
                    if (!File.Exists(fp1))  // 判断是否已有相同文件 
                    {
                        FileStream fs = new FileStream(fp1, FileMode.Create, FileAccess.ReadWrite);
                        fs.Close();
                    }
                    DetectParam = JsonConvert.DeserializeObject<DetectParam>(File.ReadAllText(fp1));
                    if (DetectParam == null)
                    {
                        DetectParam = new DetectParam { Left = 50, Right = 500, Up = 200, Down = 300, StartY = 260, AlarmWidth = 1.5f, AlarmDepth = 1.5f, CameraNo = 1 };
                    }
                }
            }
            {
                string fp = Application.StartupPath + "\\Alarms.json";
                if (!File.Exists(fp))  // 判断是否已有相同文件 
                {
                    FileStream fs = new FileStream(fp, FileMode.Create, FileAccess.ReadWrite);
                    fs.Close();
                }
                Alarms = JsonConvert.DeserializeObject<ObservableCollection<AlarmRecord>>(File.ReadAllText(fp));
                if (Alarms == null)
                {
                    //读取bak文件
                    string fp1 = Application.StartupPath + "\\Alarms_bak.json";
                    if (!File.Exists(fp1))  // 判断是否已有相同文件 
                    {
                        FileStream fs = new FileStream(fp1, FileMode.Create, FileAccess.ReadWrite);
                        fs.Close();
                    }
                    Alarms = JsonConvert.DeserializeObject<ObservableCollection<AlarmRecord>>(File.ReadAllText(fp1));
                    if (Alarms == null)
                    {
                        Alarms = new ObservableCollection<AlarmRecord>();
                    }
                }
            }

        }

        ~Biz()
        {
            if (m_Grabber != IntPtr.Zero)
                MvApi.CameraGrabber_StopLive(m_Grabber);
        }

        private void InitLocalCamera()
        {
            try
            {
                capture = new VideoCapture();
                //Svm.svm_start();
            }
            catch (Exception ex)
            {
                //MessageBox.Show(ex.Message);
            }

            System.Windows.Application.Current.Dispatcher.Hooks.DispatcherInactive += Hooks_DispatcherInactive;
        }

        public void StartDetect()
        {
            Task.Run(() =>
            {
                Task.Run(() =>
                {
                    while(true)
                    {
                        Console.WriteLine("计算: "+ FrameCount + " 采集: "+ CapCount + " 图片队列: " + ImageQueue.Count);
                        FrameCount = 0;
                        CapCount = 0;
                        CurrentLoopCount = serialComm.LoopCount;
                        Thread.Sleep(1000);
                    }
                });
                AlgorithmResult[] algorithmResults;
                AlarmRecord beginRecord = null;
                while (DetectFlag)
                {
                    Task.Run(() =>
                    {
                        Image<Bgr, byte> image;
                        if (ImageQueue.TryDequeue(out image))
                        {
                            stDetectParam = DetectParam.GetSt();
                            AlarmRecord.SetRange(AlarmParam.PixelRange,AlarmParam.YRange);

                            App.Current?.Dispatcher.Invoke(() =>
                            {
                                if (AlarmRecord.TotalLoopCount != AlarmParam.TotalLoopCount)
                                {
                                    AlarmRecord.TotalLoopCount = AlarmParam.TotalLoopCount;
                                    Alarms.Clear();
                                }
                            });
                            algorithmResults = Algorithm.DetectImage(image.Ptr, stDetectParam);
                            List<AlgorithmResult> results = new List<AlgorithmResult>(algorithmResults);
                            results.ForEach(result =>
                            {
                                if (result.bStop != 0 || result.bWidthReachStop != 0)
                                {
                                    DrawRectOnScreen(result.xPos, result.yPos);
                                    AddResultToStatistics(result.xPos,result.yPos);
                                }
                            });

                            lock (this)
                            {
                                if (detectResultStatistics.Count > 0)
                                {
                                    int avgX = 0;
                                    int avgY = 0;
                                    DateTime beginTime = DateTime.MinValue;
                                    TimeSpan maxDuration = FindMaxDuration(out avgX, out avgY, out beginTime);
                                    if (avgX == -1)
                                    {
                                        return;
                                    }
                                    float length = (float)((maxDuration.Seconds + maxDuration.Milliseconds / 1000.0) * AlarmParam.Velocity);
                                    DateTime latestTime = FindLatestTime();

                                    if (DateTime.Now - latestTime > AlarmParam.MaxErrorTime)
                                    {
                                        DetectState = DetectResultState.皮带正常;
                                        detectResultStatistics.Clear();
                                        AlarmHeadFlag = true;
                                        AlarmRecord.LoopOffset = LoopOffset;
                                        beginRecord = null;
                                        //DrawRectOnScreen(-100, -100);
                                    }
                                    else if (maxDuration > AlarmParam.MaxHurtTime)//逻辑判断有超过maxHurtTime时间,此时发生撕伤警报
                                    {
                                        DetectState = DetectResultState.撕伤;
                                        AddAlarmToStatistics(avgX, avgY, length);
                                    }
                                    else if (maxDuration > AlarmParam.MaxDivTime)//逻辑判断有超过maxDivTime时间,此时发生撕裂警报
                                    {
                                        DetectState = DetectResultState.撕裂;
                                    }
                                }
                            }
                            FrameCount++;
                        }
                    });

                    Thread.Sleep(10);
                }
            });
        }

        private void AddAlarmToStatistics(int avgX, int yPic, float length)
        {
            int xPos = avgX;
            int yPos = serialComm.LoopCount%AlarmRecord.TotalLoopCount;
            AlarmRecord alarmRecord = new AlarmRecord { XPos = xPos, YPos = yPos,YPic=yPic, LatestOccurTime = DateTime.Now, CreatedTime=DateTime.Now, Length = length };

            App.Current?.Dispatcher.Invoke(() =>
            {
                if (Alarms.Contains(alarmRecord))
                {
                    int index = Alarms.IndexOf(alarmRecord);
                    var alarm = Alarms[index];
                    //alarm.XPos = alarmRecord.XPos;
                    if (AlarmHeadFlag)
                    {
                        if (firstAlarmRecord==null)
                        {
                            firstAlarmRecord = alarmRecord;
                        }
                        if (firstAlarmRecord==alarmRecord)
                        {
                            LoopOffset = alarmRecord.YPos - alarm.YPos;
                        }
                        alarm.YPos = alarmRecord.YPos;
                        AlarmHeadFlag = false;
                    }
                    alarm.Length = alarmRecord.Length;
                    alarm.LatestOccurTime = alarmRecord.LatestOccurTime;
                }
                else
                {
                    Alarms.Add(alarmRecord);
                    Alarm(alarmRecord);
                }
                SelectedAlarmIndex = Alarms.IndexOf(alarmRecord);
                AsyncSaveObject("Alarms", Alarms);
            });
        }

        private DateTime FindLatestTime()
        {
            //遍历查找计时最新的撕裂点记录
            DateTime latestTime = DateTime.MinValue;
            foreach (var item in detectResultStatistics)
            {
                if (item.Value.endTimeStamp > latestTime)
                {
                    latestTime = item.Value.endTimeStamp;
                }
            }
            return latestTime;
        }

        private TimeSpan FindMaxDuration(out int avgX, out int avgY, out DateTime beginTime)
        {
            //撕裂点最长的时间
            TimeSpan maxDuration = TimeSpan.Zero;
            beginTime = DateTime.MinValue;
            int xSum = 0;
            int ySum = 0;
            avgX = 0;
            avgY = 0;
            //遍历查找计时最长的撕裂点记录
            DetectResultStatistic detectResultStatistic = null;
            foreach (var item in detectResultStatistics)
            {
                xSum += item.Key.XPos;
                ySum += item.Key.YPos;
                if (DateTime.Now - item.Value.beginTimeStamp > maxDuration)
                {
                    maxDuration = DateTime.Now - item.Value.beginTimeStamp;
                    beginTime = item.Value.beginTimeStamp;
                    detectResultStatistic = item.Value;
                    avgX = item.Key.XPos;
                    avgY = item.Key.YPos;
                }
            }
            if (detectResultStatistics.Count>0)
            {
                //avgX = xSum / detectResultStatistics.Count;
                //avgY = ySum / detectResultStatistics.Count;
            }
            else
            {
                avgX = -1;
                avgY = -1;
            }

            return maxDuration;
        }

        public void AddResultToStatistics(int xPos, int yPos)
        {
            AlarmPos myInt = new AlarmPos(xPos,yPos, AlarmParam.PixelRange);
            TimeSpan maxErrorTime = TimeSpan.FromSeconds(0.5);
            detectResultStatistics.AddOrUpdate(myInt,
                new DetectResultStatistic
                {
                    beginTimeStamp = DateTime.Now,
                    endTimeStamp = DateTime.Now
                },
                (x, y) =>
                {
                    if (DateTime.Now - y.endTimeStamp > maxErrorTime)
                    {
                        y.endTimeStamp = DateTime.Now;
                        y.beginTimeStamp = DateTime.Now;
                    }
                    else
                    {
                        y.endTimeStamp = DateTime.Now;
                    }
                    return y;
                }
                );
        }

        public void SetLoopCountOfBelt()
        { }

        public bool IsOldRecord()
        {
            return false;
        }

        public void Alarm(AlarmRecord alarmRecord)
        {
            SendGPIOSignal();
            SendAlarmInfo(alarmRecord);
            SaveCurrentImage(alarmRecord);
        }

        private void SendAlarmInfo(AlarmRecord alarmRecord)
        {
            serialComm.Alarm = true;
            serialComm.AlarmRecord = alarmRecord;
        }

        private void SendGPIOSignal()
        {
            Task.Run(() =>
            {
                GPIO.SetHigh();
                Thread.Sleep(5000);
                GPIO.SetLow();
                serialComm.Alarm = false;
            });
        }

        private void SaveCurrentImage(AlarmRecord alarmRecord)
        {
            string picName = alarmRecord.XPos + "_" + alarmRecord.YPic + "_";
            string destFolder = "C:\\Alarm_Pic\\pic_" + alarmRecord.CreatedTime.ToString("yyyy_MM_dd") + "\\下位机_" + DetectParam.CameraNo + "_报警截图\\";
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            string destFileName = picName + alarmRecord.CreatedTime.ToString("hh_mm_ss.fff") + "_AlarmPic.bmp";
            string destFile = destFolder + destFileName;

            var image = Image.Clone();
            image.Draw(new Rectangle(alarmRecord.XPos - 32, alarmRecord.YPic - 32, 64, 64), new Bgr(Color.Red), 3);
            image.Bitmap.Save(destFolder + destFileName);
        }

        private void SaveCurrentImageVideo(AlarmRecord alarmRecord, DateTime beginTime,AlarmRecord beginRecord)
        {
            string picName = alarmRecord.XPos + "_" + alarmRecord.YPic + "_";
            string destFolder = "C:\\Alarm_Video\\pic_" + beginRecord.CreatedTime.ToString("yyyy_MM_dd") + "\\下位机_" + DetectParam.CameraNo + "_报警截图\\"+ picName + beginRecord.CreatedTime.ToString("hh_mm_ss.fff") + "_AlarmPic\\";
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            string destFileName = alarmRecord.CreatedTime.ToString("hh_mm_ss.fff")+".bmp";
            string destFile = destFolder + destFileName;

            var image = Image.Clone();
            image.Draw(new Rectangle(alarmRecord.XPos - 32, alarmRecord.YPic - 32, 64, 64), new Bgr(Color.Red), 3);
            image.Bitmap.Save(destFile);
        }

        private bool InitCamera()
        {
            tSdkCameraDevInfo[] m_DevInfo = new tSdkCameraDevInfo[] { new tSdkCameraDevInfo() };
            m_FrameCallback = new pfnCameraGrabberFrameCallback(CameraGrabberFrameCallback);
            MvApi.CameraEnumerateDevice(out m_DevInfo);
            if (m_DevInfo != null)
            {
                m_Grabber = new IntPtr();
                int hCamera = 0;
                
                if (MvApi.CameraGrabber_Create(out m_Grabber, ref m_DevInfo[0]) == CameraSdkStatus.CAMERA_STATUS_SUCCESS)
                {
                    MvApi.CameraGrabber_GetCameraHandle(m_Grabber, out hCamera);
                    GPIO.Init(hCamera);
                    MvApi.CameraGrabber_SetRGBCallback(m_Grabber, m_FrameCallback, IntPtr.Zero);

                    // 黑白相机设置ISP输出灰度图像
                    // 彩色相机ISP默认会输出BGR24图像
                    tSdkCameraCapbility cap;
                    
                    MvApi.CameraGetCapability(hCamera, out cap);

                    // 设置相机预设分辨率
                    tSdkImageResolution t;
                    MvApi.CameraGetImageResolution(hCamera, out t);
                    t.iIndex = 8;//切换预设分辨率， 只需要设定index值就行了。640*480 mono bin
                    MvApi.CameraSetImageResolution(hCamera, ref t);

                    if (cap.sIspCapacity.bMonoSensor != 0)
                        MvApi.CameraSetIspOutFormat(hCamera, (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);
                    MvApi.CameraGrabber_SetHWnd(m_Grabber, pictureBox.Handle);
                }
                if (m_Grabber != IntPtr.Zero)
                    MvApi.CameraGrabber_StartLive(m_Grabber);
                return true;
            }
            return false;
        }

        private void CameraGrabberFrameCallback(IntPtr Grabber, IntPtr pFrameBuffer, ref tSdkFrameHead pFrameHead, IntPtr Context)
        {
            if (DetectFlag)
            {
                using (Image image = MvApi.CSharpImageFromFrame(pFrameBuffer, ref pFrameHead))
                {
                    Image.Bitmap = (Bitmap)image;
                    ImageQueue.Enqueue(Image);
                    CapCount++;
                }
            }
        }

        private void Hooks_DispatcherInactive(object sender, EventArgs e)
        {
            using (Mat source = capture.QueryFrame())
            {
                var frame = new Mat();
                CvInvoke.Resize(source, frame, ImageSize);
                if (frame != null && DetectFlag && !frame.IsEmpty)
                {
                    Image<Bgr, Byte> Image = new Image<Bgr, Byte>(ImageSize);
                    Image.Bitmap = (Bitmap)frame.Bitmap.Clone();
                    ImageQueue.Enqueue(Image);
                    using (var stream = new MemoryStream())
                    {
                        frame.Bitmap.Save(stream, ImageFormat.Png);
                        SetPreviewImage(stream);
                    }
                }
            }
        }

        private void SetPreviewImage(MemoryStream stream)
        {
            var PreviewImage = new BitmapImage();
            PreviewImage.BeginInit();
            PreviewImage.StreamSource = new MemoryStream(stream.ToArray());
            PreviewImage.EndInit();
            this.PreviewImage = PreviewImage;
        }

        public void DrawRectOnScreen(double posX, double posY)
        {
            Margin = new System.Windows.Thickness(posX-32+200, posY-32, 0, 0);
            Rectangle rect = new Rectangle((int)(posX - 32)*3/2, (int)(posY - 32)*3/2, 64*3/2, 64*3/2);
            Pen pen= new Pen(Color.WhiteSmoke, 3);
            switch (DetectState)
            {
                case DetectResultState.撕伤:
                    pen = new Pen(Color.Red, 5);
                    break;
                case DetectResultState.撕裂:
                    pen = new Pen(Color.Yellow, 4);
                    break;
                case DetectResultState.皮带正常:
                    break;
                default:
                    break;
            }
            App.Current?.Dispatcher.Invoke(() => {
                if (pictureBox!=null)
                {
                    Graphics.FromHwnd(pictureBox.Handle).DrawRectangle(pen, rect);//paintHandle对象提供了画图形的方法，我们只需调用即可
                }
            });
        }

        internal void ReConnectSerial()
        {
            serialComm.Close();
            serialComm = new SerialComm(SerialParam, DetectParam.CameraNo);
        }
        /// <summary>
        /// 异步保存数据
        /// </summary>
        public void AsyncSaveObject(string fileName, object target)
        {
            Task.Run(() =>
            {
                //保存警报信息
                string fp = Application.StartupPath + "\\"+ fileName + ".json";
                string fpBak = Application.StartupPath + "\\"+ fileName + "_bak.json";
                lock (this)
                {
                    File.WriteAllText(fp, JsonConvert.SerializeObject(target));
                    File.WriteAllText(fpBak, JsonConvert.SerializeObject(target));
                }
            });
        }
    }
}
