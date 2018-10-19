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
        [Category("报警参数"),DisplayName("停带长度")]
        public float StopLength { get; set; } = 5;
        [Category("报警参数"), DisplayName("撕裂长度")]
        public float StrenchLength { get; set; } = 2;
        [Category("报警参数"), DisplayName("连续损伤间隔最大长度")]
        public float MaxErrorDistance { get; set; } = 1;
        [Category("报警参数"), DisplayName("损伤位置范围")]
        public int PixelRange { get; set; } = 30;
        [Category("报警参数"), DisplayName("皮带速度(m/s)")]
        public float Velocity { get; set; } = 5;
        [Category("报警参数"), DisplayName("皮带同伤圈数偏差")]
        public int YRange { get; set; } = 20;
        [Category("报警参数"), DisplayName("报警图片保留天数")]
        public int ReservedDays { get; set; } = 7;
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
    public struct DetectParam
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
    }
}
