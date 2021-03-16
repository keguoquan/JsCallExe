using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using NetFwTypeLib;

namespace JsCallExeClient.Helper
{
    public class CommonHelper
    {
        #region 防火墙操作

        /// <summary>
        /// 添加防火墙例外端口
        /// </summary>
        /// <param name="name">名称</param>
        /// <param name="port">端口</param>
        /// <param name="protocol">协议(TCP、UDP)</param>
        public static void NetFwAddPorts(string name, int port, string protocol)
        {
            //创建firewall管理类的实例
            INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));

            INetFwOpenPort objPort = (INetFwOpenPort)Activator.CreateInstance(
                Type.GetTypeFromProgID("HNetCfg.FwOpenPort"));

            objPort.Name = name;
            objPort.Port = port;
            objPort.Scope = NET_FW_SCOPE_.NET_FW_SCOPE_ALL;
            if (protocol.ToUpper() == "TCP")
            {
                objPort.Protocol = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP;
            }
            else
            {
                objPort.Protocol = NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP;
            }
            objPort.Scope = NET_FW_SCOPE_.NET_FW_SCOPE_ALL;
            objPort.Enabled = true;

            bool exist = false;
            //加入到防火墙的管理策略
            foreach (INetFwOpenPort mPort in netFwMgr.LocalPolicy.CurrentProfile.GloballyOpenPorts)
            {
                if (objPort.Name == mPort.Name)
                {
                    exist = true;
                    break;
                }
            }
            if (!exist)
            {
                netFwMgr.LocalPolicy.CurrentProfile.GloballyOpenPorts.Add(objPort);
            }
        }

        /// <summary>
        /// 将应用程序添加到防火墙例外
        /// </summary>
        /// <param name="name">应用程序名称</param>
        /// <param name="executablePath">应用程序可执行文件全路径</param>
        public static void NetFwAddApps(string name, string executablePath)
        {
            //创建firewall管理类的实例
            INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));

            INetFwAuthorizedApplication app = (INetFwAuthorizedApplication)Activator.CreateInstance(
                Type.GetTypeFromProgID("HNetCfg.FwAuthorizedApplication"));

            //在例外列表里，程序显示的名称
            app.Name = name;

            //程序的路径及文件名
            app.ProcessImageFileName = executablePath;

            //是否启用该规则
            app.Enabled = true;

            //加入到防火墙的管理策略
            //netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Add(app);

            bool exist = false;
            //加入到防火墙的管理策略
            foreach (INetFwAuthorizedApplication mApp in netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications)
            {
                if (app.Name == mApp.Name)
                {
                    exist = true;
                    break;
                }
            }
            if (!exist)
            {
                netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Add(app);
            }
        }

        /// <summary>
        /// 删除防火墙例外端口
        /// </summary>
        /// <param name="port">端口</param>
        /// <param name="protocol">协议（TCP、UDP）</param>
        public static void NetFwDelApps(int port, string protocol)
        {
            INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));
            if (protocol == "TCP")
            {
                netFwMgr.LocalPolicy.CurrentProfile.GloballyOpenPorts.Remove(port, NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_TCP);
            }
            else
            {
                netFwMgr.LocalPolicy.CurrentProfile.GloballyOpenPorts.Remove(port, NET_FW_IP_PROTOCOL_.NET_FW_IP_PROTOCOL_UDP);
            }
        }

        /// <summary>
        /// 删除防火墙例外中应用程序
        /// </summary>
        /// <param name="executablePath">程序的绝对路径</param>
        public static void NetFwDelApps(string executablePath)
        {
            INetFwMgr netFwMgr = (INetFwMgr)Activator.CreateInstance(Type.GetTypeFromProgID("HNetCfg.FwMgr"));

            netFwMgr.LocalPolicy.CurrentProfile.AuthorizedApplications.Remove(executablePath);

        }

        #endregion

        #region 注册表操作

        /// <summary>
        /// 创建js调用的注册表
        /// html中调用方法：<a href="XhyClinicClient://">执行可执行文件</a>
        /// </summary>
        /// <param name="protocolName">注册表中Key  Protocol启动名称</param>
        /// <param name="processPath">.exe路径</param>
        public static void CreateJsExecReged(string protocolName, string processPath)
        {
            if (!JsExecRegedIsExist(protocolName))
            {
                RegistryKey key = Registry.ClassesRoot;
                RegistryKey kyeXcc = key.CreateSubKey(protocolName);
                kyeXcc.SetValue("", string.Format("{0} Protocol", protocolName));
                kyeXcc.SetValue("URL Protocol", "");
                RegistryKey kye_defaultIco = kyeXcc.CreateSubKey("DefaultIcon");
                kye_defaultIco.SetValue("", processPath);
                RegistryKey kye_shell = kyeXcc.CreateSubKey("shell");
                RegistryKey kye_shell_open = kye_shell.CreateSubKey("open");
                RegistryKey kye_shell_open_command = kye_shell_open.CreateSubKey("command");
                kye_shell_open_command.SetValue("", processPath);

                kye_shell_open_command.Close();
                kye_shell_open.Close();
                kye_shell.Close();
                kye_defaultIco.Close();
                kyeXcc.Close();
                key.Close();
            }
        }

        /// <summary>
        /// js调用的注册表项是否存在
        /// </summary>
        /// <returns></returns>
        private static bool JsExecRegedIsExist(string protocolName)
        {
            string[] subkeyNames;
            RegistryKey hkml = Registry.ClassesRoot;
            subkeyNames = hkml.GetSubKeyNames();
            //取得该项下所有子项的名称的序列，并传递给预定的数组中  
            foreach (string keyName in subkeyNames)
            //遍历整个数组  
            {
                if (keyName == protocolName)
                //判断子项的名称  
                {
                    hkml.Close();
                    return true;
                }
            }
            hkml.Close();
            return false;
        }

        #endregion
    }
}