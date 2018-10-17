using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace 新纵撕检测.Models
{
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
}
