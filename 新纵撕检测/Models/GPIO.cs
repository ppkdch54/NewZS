using System.Runtime.InteropServices;
using CameraHandle = System.Int32;

namespace 纵撕检测.Models
{
    class GPIO
    {
        [DllImport("SetIO.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int SetIo(int m_hCamera, int index, uint state);
        [DllImport("SetIO.dll", CallingConvention = CallingConvention.Cdecl)]
        static extern int GetIo(int m_hCamera, int index, ref uint state);


        protected static CameraHandle m_hCamera = 0;

        public static void Init(CameraHandle handle)
        {
            m_hCamera = handle;
            SetLow();
        }
        private static void SetIoState(int index, uint state)
        {
            SetIo(m_hCamera, index, state);
        }
        private static uint GetIoState(int index)
        {
            uint ret=0;
            GetIo(m_hCamera, index, ref ret);
            return ret;
        }
        public static void SetHigh()
        {
            SetIoState(1, 1);
            SetIoState(2, 1);
            SetIoState(3, 1);
        }
        public static void SetLow()
        {
            SetIoState(1, 0);
            SetIoState(2, 0);
            SetIoState(3, 0);
        }
    }
}
