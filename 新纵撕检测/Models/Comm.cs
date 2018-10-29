using System;
using System.Diagnostics;
using System.IO.Ports;
using System.Threading;

namespace 新纵撕检测.Models
{
    public class SerialComm
    {
        private SerialPort Serial { get; set; }
        public int LoopCount { get; private set; }
        public int TotalLength { get; private set; }
        public bool Alarm { get; set; }
        public int CameraNo { get; set; }
        private int pulse;
        public int Pulse
        {
            get
            {
                return pulse;
            }
            private set
            {
                pulse = value;
                PulseIndicate = DateTime.Now;
            }
        }
        public DateTime PulseIndicate { get; set; }
        public AlarmRecord AlarmRecord { get; set; }

        public SerialComm(SerialParam serialParam,int cameraNo)
        {
            Serial = new SerialPort(
                serialParam.PortName, 
                serialParam.BaudRate, 
                serialParam.Parity, 
                serialParam.DataBits, 
                serialParam.StopBits);
            CameraNo = cameraNo;
            Serial.ReceivedBytesThreshold = 8;
            Serial.DataReceived += Serial_DataReceived;
            Open();
        }

        private void Serial_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            int bufferSize = Serial.BytesToRead;
            byte[] buffer = new byte[bufferSize];
            Serial.Read(buffer, 0, bufferSize);
            //CRC校验
            if (bufferSize<2)
            {
                return;
            }
            byte[] data = new byte[bufferSize - 2];
            for (int i = 0; i < bufferSize - 2; i++)
            {
                data[i] = buffer[i];
            }
            byte[] crc = CRC16(data);
            if (crc[1]!=buffer[bufferSize - 2] ||crc[0]!=buffer[bufferSize - 1])
            {
                //CRC校验失败,不处理该数据
                return;
            }
            //校验包尾
            if (buffer[5] == 0x0A)
            {
                //判断不同包头
                switch (buffer[0])
                {
                    //case 0x84:
                    //    TotalLength = (buffer[1] << 8) + buffer[2];
                    //    break;
                    case 0x89:
                        //软关机指令
                        if (buffer[1]==0xCC && buffer[2]==0xDD && buffer[3]==0 && buffer[4]==0)
                        {
                            ShutDown();
                        }
                        break;
                    default:
                        break;
                }
            }
            if (Alarm)
            {
                for (int i = 0; i < 3; i++)
                {
                    SendAlarmInfo(AlarmRecord);
                    Thread.Sleep(5);
                }
                Pulse++;
            }
            else
            {
                if (buffer[0] == 0x87 && buffer[5]==CameraNo)
                {
                    //获取当前圈数
                    LoopCount = (buffer[1] << 24) + (buffer[2]<<16) + (buffer[3]<<8)+(buffer[4]) ;
                    //空报警信息,配合新的心跳机制
                    byte[] pulse = new byte[] { 0x86, 0x00, 0x00, (byte)CameraNo, 0x00, 0x0A };
                    
                    Pulse++;
                    SendData(pulse);
                }
            }
        }

        private void ShutDown()
        {
            //stopDetect();//停止检测
            //shutDown();//执行关机前的操作
            Thread.Sleep(2000);//停检测2s后关闭电脑
            RunCmd("shutdown.exe -s -t 0");//立即关机
        }

        ~SerialComm()
        {
            if (Serial.IsOpen)
            {
                try
                {
                    Serial.Close();
                    Serial.Dispose();
                }
                catch (Exception ex)
                {
                    //Logger.Log(ex.Message);
                }
            }
        }

        public void Open()
        {
            if (!Serial.IsOpen)
            {
                try
                {
                    Serial.Open();
                }
                catch (Exception ex)
                {
                    //Logger.Log(ex.Message);
                }
            }
        }

        public void Close()
        {
            try
            {
                Serial.Close();
            }
            catch (Exception ex)
            {

                throw;
            }
        }
        
        public void SendData(byte[] data)
        {
            Open();
            try
            {
                byte[] crc = CRC16(data);
                byte[] sendData = new byte[data.Length + 2];
                data.CopyTo(sendData, 0);
                sendData[sendData.Length - 2] = crc[1];
                sendData[sendData.Length - 1] = crc[0];
                Serial.Write(sendData, 0, sendData.Length);
            }
            catch (Exception ex)
            {
                //Logger.Log(ex.Message);
            }
        }

        /// <summary>
        /// 报警信息发送
        /// </summary>
        public void SendAlarmInfo(AlarmRecord alarmRecord)
        {
            if (alarmRecord != null)
            {
                byte[] protocol = new byte[6];
                protocol[0] = 0x86;
                protocol[1] = (byte)alarmRecord.XPos;
                protocol[2] = (byte)alarmRecord.YPos;
                protocol[3] = (byte)CameraNo;
                protocol[4] = 0;//报警类型
                protocol[5] = 0x0a;
                if (Serial != null)
                {
                    SendData(protocol);
                }
            }
        }

        private static string CmdPath = @"C:\Windows\System32\cmd.exe";

        /// <summary>
        /// 执行cmd命令
        /// 多命令请使用批处理命令连接符：
        /// <![CDATA[
        /// &:同时执行两个命令
        /// |:将上一个命令的输出,作为下一个命令的输入
        /// &&：当&&前的命令成功时,才执行&&后的命令
        /// ||：当||前的命令失败时,才执行||后的命令]]>
        /// </summary>
        /// <param name="cmd"></param>
        public static void RunCmd(string cmd)
        {
            //cmd = cmd.Trim().TrimEnd('&') + "&exit";//说明：不管命令是否成功均执行exit命令，否则当调用ReadToEnd()方法时，会处于假死状态
            using (Process p = new Process())
            {
                p.StartInfo.FileName = CmdPath;
                p.StartInfo.UseShellExecute = false;        //是否使用操作系统shell启动
                p.StartInfo.RedirectStandardInput = true;   //接受来自调用程序的输入信息
                p.StartInfo.RedirectStandardOutput = true;  //由调用程序获取输出信息
                p.StartInfo.RedirectStandardError = true;   //重定向标准错误输出
                p.StartInfo.CreateNoWindow = true;          //不显示程序窗口
                p.Start();//启动程序

                //向cmd窗口写入命令
                p.StandardInput.WriteLine(cmd);
                p.StandardInput.AutoFlush = true;

                //获取cmd窗口的输出信息
                //output = p.StandardOutput.ReadToEnd();
                p.WaitForExit();//等待程序执行完退出进程
                p.Close();
            }
        }

        public static byte[] CRC16(byte[] data)
        {
            uint IX, IY;
            ushort crc = 0xFFFF;//set all 1

            int len = data.Length;
            if (len <= 0)
                crc = 0;
            else
            {
                len--;
                for (IX = 0; IX <= len; IX++)
                {
                    crc = (ushort)(crc ^ (data[IX]));
                    for (IY = 0; IY <= 7; IY++)
                    {
                        if ((crc & 1) != 0)
                            crc = (ushort)((crc >> 1) ^ 0xA001);
                        else
                            crc = (ushort)(crc >> 1); //
                    }
                }
            }

            byte buf1 = (byte)((crc & 0xff00) >> 8);//高位置
            byte buf2 = (byte)(crc & 0x00ff); //低位置

            crc = (ushort)(buf1 << 8);
            crc += buf2;

            return new byte[] { buf1, buf2 };
        }

        public static string BytesToHex(byte[] bytes)
        {
            string byteStr = string.Empty;
            if (bytes != null || bytes.Length > 0)
            {
                foreach (var item in bytes)
                {
                    byteStr += " 0x" + item.ToString("X2");
                }
            }
            return byteStr;
        }
    }
}
