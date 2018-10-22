using System;
using System.ComponentModel;

namespace 新纵撕检测.Models
{
    public class AlarmRecord: INotifyPropertyChanged
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
        public DateTime CreatedTime { get;}//第一次报警时间
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

        static int xRange=0;
        static int yRange=0;

        public static int LoopOffset { get; set; } = 0;
        public static int TotalLoopCount { get; set; } = int.MaxValue;

        public AlarmRecord()
        {
            CreatedTime = DateTime.Now;
        }
        public event PropertyChangedEventHandler PropertyChanged;

        public override bool Equals(object obj)
        {
            var tmp = obj as AlarmRecord;
            if (tmp==null)
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

        public static void SetRange(int _x,int _y)
        {
            xRange = _x;
            yRange = _y;
        }
    }
}
