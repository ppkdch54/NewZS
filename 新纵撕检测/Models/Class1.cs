using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
        public int xPos;
        public int yPos;
        public DateTime endTimeStamp;
        public DateTime beginTimeStamp;
    }
    public  class AlarmParam
    {
        public int PixelRange { get; set; }
        public int MyProperty { get; set; }
    }

}
