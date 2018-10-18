using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace 新纵撕检测.Models
{
    class AlarmRecord
    {
        public int XPos { get; set; }//撕伤距离左边界的位置
        public int YPos { get; set; }//撕伤在皮带上的圈数表示
        public float Length { get; set; }//撕伤长度,单位(m)
        public DateTime CreatedTime { get; set; }//第一次报警时间
        public DateTime LatestOccurTime { get; set; }//最新报警时间
    }
}
