using System;
using System.Runtime.InteropServices;

namespace 新纵撕检测.Models
{
    class Algorithm
    {
        [DllImport("runEx.dll")]
        public static extern void svm_start();
        [DllImport("runEx.dll", EntryPoint = "runEx", CallingConvention = CallingConvention.Cdecl)]
        public static extern void DetectAlgorithm(IntPtr imageHandle, StDetectParam detectParam, int cameraNo, IntPtr alarmInfo);

        const int AlarmInfoCount = 10;        //算法返回报警信息的数目
        static int size = Marshal.SizeOf(typeof(AlgorithmResult)) * AlarmInfoCount;
        static IntPtr pResultBuff = Marshal.AllocHGlobal(size); //指向算法结果的内存指针

        public static AlgorithmResult[] DetectImage(IntPtr imageHandle, StDetectParam detectParam)
        {
            //清零存放算法结果的内存区域
            byte[] zeroBytes = new byte[size];
            zeroBytes.Initialize();
            Marshal.Copy(zeroBytes, 0, pResultBuff, size);
            DetectAlgorithm(imageHandle, detectParam, 1, pResultBuff);
            //用来存放算法返回的报警信息
            AlgorithmResult[] newAlarmInfos = new AlgorithmResult[AlarmInfoCount];
            for (int alarmInfoIndex = 0; alarmInfoIndex < AlarmInfoCount; alarmInfoIndex++)
            {
                //获取每一个报警信息
                newAlarmInfos[alarmInfoIndex] = new AlgorithmResult();
                IntPtr pAlarmInfo = new IntPtr(pResultBuff.ToInt64() + Marshal.SizeOf(typeof(AlgorithmResult)) * alarmInfoIndex);
                newAlarmInfos[alarmInfoIndex] = (AlgorithmResult)Marshal.PtrToStructure(pAlarmInfo, typeof(AlgorithmResult));
            }
            return newAlarmInfos;
        }
    }
}
