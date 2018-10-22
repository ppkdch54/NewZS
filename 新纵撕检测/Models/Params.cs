using System;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.InteropServices;
using System.Xml.Serialization;

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
        //[Browsable(false)]
        public TimeSpan MaxErrorTime { get; private set; }
        //[Browsable(false)]
        public TimeSpan MaxHurtTime { get; private set; }
        //[Browsable(false)]
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
}
