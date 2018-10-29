using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.InteropServices;

namespace 新纵撕检测.Models
{
    /// <summary>
    /// 检测结果
    /// </summary>
    public enum DetectResultState
    {
        撕伤, 撕裂, 皮带正常
    }
    /// <summary>
    /// 连续撕伤点位置统计
    /// </summary>
    public class DetectResultStatistic
    {
        public DateTime endTimeStamp;
        public DateTime beginTimeStamp;
    }
    public class AlarmParam
    {
        private float maxHurtDistance;
        private float maxDivDistance;
        private float maxErrorDistance;
        private float velocity = 5;

        private int totalLoopCount;
        [Category("报警参数"), DisplayName("皮带总圈数"),Description("总圈数如果为非法值,会重置成系统最大值.")]
        public int TotalLoopCount
        {
            get { return totalLoopCount; }
            set
            {
                totalLoopCount = value;
                if (totalLoopCount<=0)
                {
                    totalLoopCount = int.MaxValue;
                }
            }
        }

        [Category("报警参数"), DisplayName("皮带速度(m/s)"), Description("总圈数如果为非法值,会重置成5.")]
        public float Velocity
        {
            get { return velocity; }
            set
            {
                velocity = value;
                if (velocity<=0)
                {
                    velocity = 5;
                }
                MaxHurtTime = DateTime.MinValue.AddSeconds(maxHurtDistance / Velocity) - DateTime.MinValue;
                MaxDivTime = DateTime.MinValue.AddSeconds(maxDivDistance / Velocity) - DateTime.MinValue;
                MaxErrorTime = DateTime.MinValue.AddSeconds(maxErrorDistance / Velocity) - DateTime.MinValue;
            }
        }
        [Category("报警参数"), DisplayName("停带长度")]
        public float MaxHurtDistance
        {
            get { return maxHurtDistance; }
            set
            {
                maxHurtDistance = value;
                MaxHurtTime = DateTime.MinValue.AddSeconds(maxHurtDistance / Velocity) - DateTime.MinValue;
            }
        }
        [Category("报警参数"), DisplayName("撕裂长度")]
        public float MaxDivDistance
        {
            get { return maxDivDistance; }
            set
            {
                maxDivDistance = value;
                MaxDivTime = DateTime.MinValue.AddSeconds(maxDivDistance / Velocity) - DateTime.MinValue;
            }
        }
        [Category("报警参数"), DisplayName("连续损伤间隔最大长度")]
        public float MaxErrorDistance
        {
            get { return maxErrorDistance; }
            set
            {
                maxErrorDistance = value;
                MaxErrorTime = DateTime.MinValue.AddSeconds(maxErrorDistance / Velocity) - DateTime.MinValue;
            }
        }
        [Category("报警参数"), DisplayName("损伤位置范围")]
        public int PixelRange { get; set; }
        [Category("报警参数"), DisplayName("皮带同伤圈数偏差")]
        public int YRange { get; set; }
        [Category("报警参数"), DisplayName("报警图片保留天数")]
        public int ReservedDays { get; set; }
        [Browsable(false)]
        public TimeSpan MaxErrorTime { get; private set; }
        [Browsable(false)]
        public TimeSpan MaxHurtTime { get; private set; }
        [Browsable(false)]
        public TimeSpan MaxDivTime { get; private set; }
    }
    public class SerialParam
    {
        [Category("串口参数"), DisplayName("端口")]
        public string PortName { get; set; }
        [Category("串口参数"), DisplayName("波特率")]
        public int BaudRate { get; set; }
        [Category("串口参数"), DisplayName("奇偶校验")]
        public Parity Parity { get; set; }
        [Category("串口参数"), DisplayName("数据位")]
        public int DataBits { get; set; }
        [Category("串口参数"), DisplayName("停止位")]
        public StopBits StopBits { get; set; }
    }
    /// <summary>
    /// 检测参数
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct StDetectParam
    {
        public int CameraNo;           //相机编号
        public int Left;               //检测左边界
        public int Right;              //检测右边界
        public int Up;                 //检测上边界
        public int Down;               //检测下边界
        public int StartY;             //开始指定y坐标
        public float AlarmWidth;       //检测宽度
        public float AlarmDepth;       //检测深度 
    }
    public class DetectParam
    {
        [Category("检测参数"), DisplayName("相机编号")]
        public int CameraNo { get; set; }           //相机编号
        [Category("检测参数"), DisplayName("检测左边界")]
        public int Left { get; set; }               //检测左边界
        [Category("检测参数"), DisplayName("检测右边界")]
        public int Right { get; set; }              //检测右边界
        [Category("检测参数"), DisplayName("检测上边界")]
        public int Up { get; set; }                 //检测上边界
        [Category("检测参数"), DisplayName("检测下边界")]
        public int Down { get; set; }               //检测下边界
        [Category("检测参数"), DisplayName("开始指定y坐标")]
        public int StartY { get; set; }             //开始指定y坐标
        [Category("检测参数"), DisplayName("检测宽度")]
        public float AlarmWidth { get; set; }       //检测宽度
        [Category("检测参数"), DisplayName("检测深度")]
        public float AlarmDepth { get; set; }       //检测深度 

        internal StDetectParam GetSt()
        {
            StDetectParam stDetectParam = new StDetectParam
            {
                CameraNo = CameraNo,
                Left = Left,
                Right = Right,
                Up = Up,
                Down = Down,
                StartY = StartY,
                AlarmWidth = AlarmWidth,
                AlarmDepth = AlarmDepth
            };
            return stDetectParam;
        }
    }
    /// <summary>
    /// 返回报警信息
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct AlgorithmResult
    {
        public int camNumber;         //哪个相机，0左，2右，1中
        public int xPos;              //距左边的像素点
        public int yPos;              //像素点纵坐标
        public int type;              //1表示断，2表示撕，//改成帧号
        public int alarmWidth;        //报警的 宽度和高度
        public byte bStop;            //撕裂中表示衰减阈值是否达到，撕伤中表示深度是否达到停带，true表示停带数据，false表示报警数据
        public byte bWidthReachStop;  //宽度是否达到停带，true表示达到停带，false表示未达到
    }
    public class AlarmRecord : INotifyPropertyChanged
    {
        private int xPos;
        public int XPos
        {
            get { return xPos; }
            set
            {
                xPos = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("XPos"));
            }
        }//撕伤距离左边界的位置
        public int YPic { get; set; }
        private int yPos;
        public int YPos
        {
            get { return yPos; }
            set
            {
                yPos = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("YPos"));
            }
        }//撕伤在皮带上的圈数表示
        private float length;
        public float Length
        {
            get { return length; }
            set
            {
                length = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Length"));
            }
        }//撕伤长度,单位(m)
        public DateTime CreatedTime { get; set; }//第一次报警时间
        private DateTime latestOccurTime;
        public DateTime LatestOccurTime
        {
            get { return latestOccurTime; }
            set
            {
                latestOccurTime = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("LatestOccurTime"));
            }
        }//最新报警时间

        static int xRange = 0;
        static int yRange = 0;

        public static int LoopOffset { get; set; } = 0;
        public static int TotalLoopCount { get; set; } = int.MaxValue;

        public event PropertyChangedEventHandler PropertyChanged;

        public override bool Equals(object obj)
        {
            var tmp = obj as AlarmRecord;
            if (tmp == null)
            {
                return false;
            }
            if (Math.Abs(tmp.XPos - XPos) <= xRange &&
                (
                (Math.Abs(tmp.YPos - YPos - LoopOffset) % TotalLoopCount <= yRange) ||
                Math.Abs(tmp.YPos - YPos - LoopOffset) % TotalLoopCount >= TotalLoopCount - yRange)
                )
            {
                return true;
            }
            return false;
        }

        public static void SetRange(int _x, int _y)
        {
            xRange = _x;
            yRange = _y;
        }
    }
}
