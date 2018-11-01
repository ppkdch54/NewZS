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
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using 新纵撕检测.Models;

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
        private Image<Bgr, byte> Image = new Image<Bgr, byte>(ImageSize);
        public ConcurrentQueue<Image<Bgr, byte>> ImageQueue { get; set; } = new ConcurrentQueue<Image<Bgr, byte>>();
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
        Random random = new Random();

        private PropertyGrid propertyGrid;

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

        private bool SaveFlag;
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
        private MainWindow window;
        private int m_hCamera;
        private ColorPalette m_GrayPal;
        private tSdkCameraDevInfo m_DevInfo;
        private int DispCount;

        public Biz(MainWindow mainWindow)
        {
            propertyGrid = mainWindow.PropertyGrid;
            window = mainWindow;
            LoadParams();
            propertyGrid.SelectedObject = DetectParam;
            serialComm = new SerialComm(SerialParam, DetectParam.CameraNo);
            AlarmRecord.TotalLoopCount = AlarmParam.TotalLoopCount;
            m_FrameCallback = new pfnCameraGrabberFrameCallback(CameraGrabberFrameCallback);
            if (InitCamera())
            {
                mainWindow.RectBorder.Visibility = System.Windows.Visibility.Collapsed;
                GPIO.Init(m_hCamera);
            }
            else
            {
                InitLocalCamera();
            }

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
                        Console.WriteLine("计算: "+ FrameCount + " 采集: "+ CapCount +"显示: " + DispCount + " 图片队列: " + ImageQueue.Count);
                        FrameCount = 0;
                        CapCount = 0;
                        DispCount = 0;
                        CurrentLoopCount = serialComm.LoopCount;
                        Thread.Sleep(1000);
                    }
                });
                AlgorithmResult[] algorithmResults;
                while (DetectFlag)
                {
                    Task.Run(() =>
                    {
                        Image<Bgr, byte> image;
                        if (ImageQueue.TryDequeue(out image))
                        {
                            stDetectParam = DetectParam.GetSt();
                            AlarmRecord.SetRange(AlarmParam.PixelRange,AlarmParam.YRange);

                            App.Current?.Dispatcher?.Invoke(() =>
                            {
                                if (AlarmRecord.TotalLoopCount != AlarmParam.TotalLoopCount)
                                {
                                    AlarmRecord.TotalLoopCount = AlarmParam.TotalLoopCount;
                                    Alarms.Clear();
                                    AsyncSaveObject("Alarms", Alarms);
                                }
                            });
                            algorithmResults = Algorithm.DetectImage(image.Ptr, stDetectParam);
                            List<AlgorithmResult> results = new List<AlgorithmResult>(algorithmResults);
                            results.ForEach(result =>
                            {
                                if (result.bStop != 0 || result.bWidthReachStop != 0)
                                {
                                    DrawRectOnScreen(result.xPos, result.yPos);
                                    AddResultToStatistics(result.xPos,result.yPos,image.Clone());
                                }
                            });

                            lock (this)
                            {
                                if (detectResultStatistics.Count > 0)
                                {
                                    int avgX = 0;
                                    int avgY = 0;
                                    DateTime beginTime = DateTime.MinValue;
                                    DetectResultStatistic detectResultStatistic = null;
                                    TimeSpan maxDuration = FindMaxDuration(out avgX, out avgY, out beginTime,out detectResultStatistic);
                                    if (avgX == -1)
                                    {
                                        return;
                                    }
                                    float length = (float)((maxDuration.Seconds + maxDuration.Milliseconds / 1000.0) * AlarmParam.Velocity);
                                    DateTime latestTime = FindLatestTime();

                                    if (DateTime.Now - latestTime > AlarmParam.MaxErrorTime)
                                    {
                                        DetectState = DetectResultState.皮带正常;
                                        AlarmHeadFlag = true;
                                        AlarmRecord.LoopOffset = LoopOffset;
                                        if (SaveFlag)
                                        {
                                            SaveFlag = false;
                                            SaveAlarmVideo();
                                        }
                                        detectResultStatistics.Clear();
                                    }
                                    else if (maxDuration > AlarmParam.MaxHurtTime)//逻辑判断有超过maxHurtTime时间,此时发生撕伤警报
                                    {
                                        DetectState = DetectResultState.撕伤;
                                        AddAlarmToStatistics(avgX, avgY, length, detectResultStatistic);
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

        private void AddAlarmToStatistics(int avgX, int yPic, float length, DetectResultStatistic detectResultStatistic)
        {
            int xPos = avgX;
            int yPos = serialComm.LoopCount % AlarmRecord.TotalLoopCount;
            AlarmRecord alarmRecord = new AlarmRecord { XPos = xPos, YPos = yPos, YPic = yPic, LatestOccurTime = DateTime.Now, CreatedTime = DateTime.Now, Length = length };

            App.Current?.Dispatcher.Invoke(() =>
            {
                if (Alarms.Contains(alarmRecord))
                {
                    int index = Alarms.IndexOf(alarmRecord);
                    var alarm = Alarms[index];
                    //alarm.XPos = alarmRecord.XPos;
                    if (AlarmHeadFlag)
                    {
                        if (firstAlarmRecord == null)
                        {
                            firstAlarmRecord = alarmRecord;
                        }
                        if (firstAlarmRecord == alarmRecord)
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

        private TimeSpan FindMaxDuration(out int avgX, out int avgY, out DateTime beginTime,out DetectResultStatistic detectResultStatistic)
        {
            //撕裂点最长的时间
            TimeSpan maxDuration = TimeSpan.Zero;
            beginTime = DateTime.MinValue;
            int xSum = 0;
            int ySum = 0;
            avgX = 0;
            avgY = 0;
            detectResultStatistic = null;
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

        public void AddResultToStatistics(int xPos, int yPos, Image<Bgr, byte> image)
        {
            AlarmPos myInt = new AlarmPos(xPos, yPos, AlarmParam.PixelRange);
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
            detectResultStatistics[myInt].ImageStore.Add(image);
        }

        public void Alarm(AlarmRecord alarmRecord)
        {
            SaveFlag = true;
            SendGPIOSignal();
            SendAlarmInfo(alarmRecord);
            SaveAlarmImage(alarmRecord);
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

        private void SaveAlarmImage(AlarmRecord alarmRecord)
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

        private void SaveAlarmVideo()
        {
            AlarmRecord alarmRecord = new AlarmRecord()
            {
                CreatedTime = DateTime.Now,
                LatestOccurTime = DateTime.Now,
            };
            string picName = alarmRecord.XPos + "_" + alarmRecord.YPic + "_";
            string destFolder = "C:\\Alarm_Video\\pic_" + alarmRecord.CreatedTime.ToString("yyyy_MM_dd") + "\\下位机_" + DetectParam.CameraNo + "_报警截图\\"+ picName + alarmRecord.CreatedTime.ToString("hh_mm_ss.fff") + "_AlarmPic\\";
            if (!Directory.Exists(destFolder))
            {
                Directory.CreateDirectory(destFolder);
            }
            int num = random.Next();
            ConcurrentDictionary<AlarmPos, DetectResultStatistic> temp = new ConcurrentDictionary<AlarmPos, DetectResultStatistic>(detectResultStatistics);
            Task.Run(() =>
            {
                foreach (var detectResultStatistic in temp.Values)
                {
                    foreach (var image in detectResultStatistic.ImageStore)
                    {
                        string destFileName = DateTime.Now.ToString("hh_mm_ss.fff") + num++ + ".bmp";
                        string destFile = destFolder + destFileName;
                        //image.Draw(new Rectangle(alarmRecord.XPos - 32, alarmRecord.YPic - 32, 64, 64), new Bgr(Color.Red), 3);
                        image.Bitmap.Save(destFile);
                    }
                }
            });
        }

        private bool InitCamera()
        {
            CameraSdkStatus status = 0;

            tSdkCameraDevInfo[] DevList;
            MvApi.CameraEnumerateDevice(out DevList);
            int NumDev = (DevList != null ? DevList.Length : 0);
            if (NumDev < 1)
            {
                System.Windows.MessageBox.Show("未扫描到相机");
                return false;
            }
            else if (NumDev == 1)
            {
                status = MvApi.CameraGrabber_Create(out m_Grabber, ref DevList[0]);
            }
            else
            {
                status = MvApi.CameraGrabber_CreateFromDevicePage(out m_Grabber);
            }

            if (status == 0)
            {
                MvApi.CameraGrabber_GetCameraDevInfo(m_Grabber, out m_DevInfo);
                MvApi.CameraGrabber_GetCameraHandle(m_Grabber, out m_hCamera);

                var handle = (new WindowInteropHelper(window)).Handle;
                MvApi.CameraCreateSettingPage(m_hCamera, handle, m_DevInfo.acFriendlyName, null, (IntPtr)0, 0);
                
                MvApi.CameraGrabber_SetRGBCallback(m_Grabber, m_FrameCallback, IntPtr.Zero);

                // 黑白相机设置ISP输出灰度图像
                // 彩色相机ISP默认会输出BGR24图像
                tSdkCameraCapbility cap;
                MvApi.CameraGetCapability(m_hCamera, out cap);
                if (cap.sIspCapacity.bMonoSensor != 0)
                {
                    MvApi.CameraSetIspOutFormat(m_hCamera, (uint)MVSDK.emImageFormat.CAMERA_MEDIA_TYPE_MONO8);

                    // 创建灰度调色板
                    Bitmap Image = new Bitmap(1, 1, System.Drawing.Imaging.PixelFormat.Format8bppIndexed);
                    m_GrayPal = Image.Palette;
                    for (int Y = 0; Y < m_GrayPal.Entries.Length; Y++)
                        m_GrayPal.Entries[Y] = System.Drawing.Color.FromArgb(255, Y, Y, Y);
                }

                // 设置VFlip，由于SDK输出的数据默认是从底到顶的，打开VFlip后就可以直接转换为Bitmap
                MvApi.CameraSetMirror(m_hCamera, 1, 1);


                // 设置相机预设分辨率
                tSdkImageResolution t;
                MvApi.CameraGetImageResolution(m_hCamera, out t);
                t.iIndex = 8;//切换预设分辨率， 只需要设定index值就行了。640*480 mono bin
                MvApi.CameraSetImageResolution(m_hCamera, ref t);

                // 为了演示如何在回调中使用相机数据创建Bitmap并显示到PictureBox中，这里不使用SDK内置的绘制操作
                //MvApi.CameraGrabber_SetHWnd(m_Grabber, this.DispWnd.Handle);

                MvApi.CameraGrabber_StartLive(m_Grabber);
                return true;
            }
            else
            {
                System.Windows.MessageBox.Show(String.Format("打开相机失败，原因：{0}", status));
            }
            return false;
        }

        private void CameraGrabberFrameCallback(IntPtr Grabber, IntPtr pFrameBuffer, ref tSdkFrameHead pFrameHead, IntPtr Context)
        {
            int w = pFrameHead.iWidth;
            int h = pFrameHead.iHeight;
            bool gray = (pFrameHead.uiMediaType == (uint)emImageFormat.CAMERA_MEDIA_TYPE_MONO8);
            Bitmap ImageDisp = new Bitmap(w, h,
                gray ? w : w * 3,
                gray ? PixelFormat.Format8bppIndexed : PixelFormat.Format24bppRgb,
                pFrameBuffer);

            // 如果是灰度图要设置调色板
            if (gray)
            {
                ImageDisp.Palette = m_GrayPal;
            }

            using (var stream = new MemoryStream())
            {
                ImageDisp.Save(stream, ImageFormat.Bmp);
                App.Current.Dispatcher.InvokeAsync(new Action(() =>
                {
                    SetPreviewImage(stream);
                }));
            }

            if (DetectFlag)
            {
                Image.Bitmap = ImageDisp;
                ImageQueue.Enqueue(Image);
                CapCount++;
            }
        }

        [System.Runtime.InteropServices.DllImport("gdi32")]
        static extern int DeleteObject(IntPtr o);

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
            DispCount++;
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
            App.Current?.Dispatcher?.Invoke(() => {
                //if (pictureBox!=null)
                //{
                //    Graphics.FromHwnd(pictureBox.Handle).DrawRectangle(pen, rect);//paintHandle对象提供了画图形的方法，我们只需调用即可
                //}
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
