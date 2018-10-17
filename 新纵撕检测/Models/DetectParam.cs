using System.Runtime.InteropServices;

namespace 新纵撕检测.Models
{
    /// <summary>
    /// 检测参数
    /// </summary>
    [StructLayout(LayoutKind.Sequential)]
    public struct DetectParam
    {
        public int CameraNo { get; set; }           //相机编号
        public int Left { get; set; }               //检测左边界
        public int Right { get; set; }              //检测右边界
        public int Up { get; set; }                 //检测上边界
        public int Down { get; set; }               //检测下边界
        public int StartY { get; set; }             //开始指定y坐标
        public float AlarmWidth { get; set; }       //检测宽度
        public float AlarmDepth { get; set; }       //检测深度 
    }
}
