using Emgu.CV;
using Emgu.CV.Structure;
using MVSDK;
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
using System.Windows.Forms;
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

        public bool DetectFlag { get; private set; } = true;
        public bool IsAlarm { get; set; }
        public ConcurrentQueue<Image<Bgr, Byte>> ImageQueue { get; set; } = new ConcurrentQueue<Image<Bgr, byte>>();
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

        private PictureBox pictureBox;
        private PropertyGrid propertyGrid;

        public DetectParam detectParam { get; set; }
        public AlarmParam alarmParam { get; set; } = new AlarmParam { PixelRange=30 };
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

        public Biz(MainWindow mainWindow)
        {
            pictureBox = mainWindow.PreviewBox;
            propertyGrid = mainWindow.PropertyGrid;
            if (InitCamera())
            {
                
            }
            else
            {
                InitLocalCamera();
            }
            Algorithm.svm_start();
            Alarms = new ObservableCollection<AlarmRecord>();
            StartDetect();
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
                    while (true)
                    {
                        Console.WriteLine("计算: "+ FrameCount + " 采集: "+ CapCount + " 图片队列: " + ImageQueue.Count);
                        FrameCount = 0;
                        CapCount = 0;
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
                            algorithmResults = Algorithm.DetectImage(image.Ptr, new DetectParam { Left = 50, Right = 500, Up = 200, Down = 300, StartY = 260, AlarmWidth = 1.5f, AlarmDepth = 1.5f });

                            List<AlgorithmResult> results = new List<AlgorithmResult>(algorithmResults);
                            results.ForEach(result =>
                            {
                                if (result.bStop != 0 || result.bWidthReachStop != 0)
                                {
                                    DrawRectOnScreen(result.xPos, result.yPos);
                                    AddResultToStatistics(result.xPos);
                                }
                            });

                            if (detectResultStatistics.Count > 0)
                            {
                                int avgX = 0;
                                TimeSpan maxDuration = FindMaxDuration(out avgX);
                                DateTime latestTime = FindLatestTime();

                                TimeSpan maxErrorTime = TimeSpan.FromSeconds(1);
                                TimeSpan maxHurtTime = TimeSpan.FromSeconds(1.0);
                                TimeSpan maxDivTime = TimeSpan.FromSeconds(0.5);
                                if (DateTime.Now - latestTime > maxErrorTime)
                                {
                                    DetectState = DetectResultState.皮带正常;
                                    detectResultStatistics.Clear();
                                    //DrawRectOnScreen(-100, -100);
                                }
                                else if (maxDuration > maxHurtTime)//逻辑判断有超过maxHurtTime时间,此时发生撕伤警报
                                {
                                    DetectState = DetectResultState.撕伤;
                                    //AddAlarmToStatistics(avgX);
                                }
                                else if (maxDuration > maxDivTime)//逻辑判断有超过maxDivTime时间,此时发生撕裂警报
                                {
                                    DetectState = DetectResultState.撕裂;
                                }
                                FrameCount++;
                            }
                        }
                    });

                    Thread.Sleep(10);
                }
            });
        }

        private void AddAlarmToStatistics(int avgX)
        {
            App.Current.Dispatcher.Invoke(() =>
            {
                Alarms.Add(new AlarmRecord { CreatedTime = DateTime.Now, XPos = avgX });
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

        private TimeSpan FindMaxDuration(out int avgX)
        {
            //撕裂点最长的时间
            TimeSpan maxDuration = TimeSpan.Zero;
            int xSum = 0;
            //遍历查找计时最长的撕裂点记录
            DetectResultStatistic detectResultStatistic = null;
            foreach (var item in detectResultStatistics)
            {
                xSum += item.Value.xPos;
                if (DateTime.Now - item.Value.beginTimeStamp > maxDuration)
                {
                    maxDuration = DateTime.Now - item.Value.beginTimeStamp;
                    detectResultStatistic = item.Value;
                }
            }
            avgX = xSum / detectResultStatistics.Count;
            return maxDuration;
        }

        private class MyInt
        {
            int xPos;
            int pixRange;
            public MyInt(int pos,int range)
            {
                xPos = pos;
                pixRange = range;
            }
            public override bool Equals(object obj)
            {
                var tmp = obj as MyInt;
                if (Math.Abs(tmp.xPos-xPos)<=pixRange)
                {
                    return true;
                }
                return false;
            }
        }
        ConcurrentDictionary<MyInt, DetectResultStatistic> detectResultStatistics = new ConcurrentDictionary<MyInt, DetectResultStatistic>();
        public void AddResultToStatistics(int xPos)
        {
            MyInt myInt = new MyInt(xPos, alarmParam.PixelRange);
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

        public void Alarm()
        {
            SendGPIOSignal();
            SendAlarmInfo();
            SaveCurrentImage();
        }

        private void SendAlarmInfo()
        {
            
        }

        private void SendGPIOSignal()
        {
            throw new NotImplementedException();
        }

        private void SaveCurrentImage()
        {
            DrawRectOnImage();

        }

        private void DrawRectOnImage()
        {

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

        Image<Bgr, Byte> Image = new Image<Bgr, Byte>(ImageSize);
        private void CameraGrabberFrameCallback(IntPtr Grabber, IntPtr pFrameBuffer, ref tSdkFrameHead pFrameHead, IntPtr Context)
        {
            if (DetectFlag)
            {
                using (Image image = MvApi.CSharpImageFromFrame(pFrameBuffer, ref pFrameHead))
                {
                    Image.Bitmap = (Bitmap)image;
                    using (MemoryStream stream = new MemoryStream())
                    {
                        Image.Bitmap.Save(stream, ImageFormat.Bmp);
                        //App.Current.Dispatcher.Invoke(() =>
                        //{
                        //    //SetPreviewImage(stream);
                        //});
                    }
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
            Margin = new System.Windows.Thickness(posX-32, posY-32, 0, 0);
            Rectangle rect = new Rectangle((int)posX - 32, (int)posY - 32, 64, 64);
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
            App.Current.Dispatcher.Invoke(() => {
                Graphics.FromHwnd(pictureBox.Handle).DrawRectangle(pen, rect);//paintHandle对象提供了画图形的方法，我们只需调用即可
            });
        }
    }
}
