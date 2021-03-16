using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using JsCallExeClient.Helper;

namespace JsCallExeClient
{
    static class Program
    {
        /// <summary>
        /// 应用程序的主入口点。
        /// </summary>
        [STAThread]
        static void Main()
        {
            if (OnlyRunOneCheck())
            {
                //程序路径
                string processPath = Application.ExecutablePath;

                //创建js调用的注册表
                CommonHelper.CreateJsExecReged("JsCallExeClient", processPath);
                //检查防火墙，并添加例外
                CommonHelper.NetFwAddPorts("JsCallExeClient_Port", CommonInfo.WsPort, "TCP");
                CommonHelper.NetFwAddApps("JsCallExeClient_App", processPath);

                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Application.Run(new FrmMain());
            }
        }

        /// <summary>
        /// 只允许一个程序运行
        /// </summary>
        /// <returns>false 已有一个程序在运行，请勿重复运行程序</returns>
        public static bool OnlyRunOneCheck()
        {
            bool isRun;
            System.Threading.Mutex mutex = new System.Threading.Mutex(true, Application.ProductName, out isRun);
            return isRun;
        }

        /// <summary>
        /// 退出当前程序 运行自动升级程序
        /// </summary>
        public static void ExitAndStartAutoUpgrade()
        {
            string upgradePath = Path.Combine(Application.StartupPath, "AutoUpgrade.exe");
            if (File.Exists(upgradePath))
            {
                ProcessStartInfo main = new ProcessStartInfo(upgradePath);
                Process.Start(upgradePath);
                Application.Exit();
            }
        }
    }
}
