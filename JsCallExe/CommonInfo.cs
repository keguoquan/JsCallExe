using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace JsCallExeClient
{
    public class CommonInfo
    {
        /// <summary>
        /// 版本信息
        /// </summary>
        public static string Version = "1.1";

        /// <summary>
        /// 监听端口
        /// </summary>
        public static int WsPort = 3964;
        
        /// <summary>
        /// 向主窗体输出日志
        /// </summary>
        /// <param name="msg"></param>
        public static void Output(string msg)
        {
            if (FrmMain != null)
            {
                FrmMain.Output(msg);
            }
        }

        /// <summary>
        /// 向主窗体输出日志
        /// </summary>
        /// <param name="format"></param>
        /// <param name="args"></param>
        public static void Output(String format, params object[] args)
        {
            if (FrmMain != null)
            {
                FrmMain.Output(string.Format(format, args));
            }
        }

        /// <summary>
        /// 主窗体
        /// </summary>
        public static FrmMain FrmMain { get; set; }        
    }
}
