using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Win32;
using NetFwTypeLib;

namespace JsCallExeClient.Helper
{
    /// <summary>
    /// 返回对象
    /// </summary>
    public class WSResModel
    {
        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pmt"></param>
        /// <param name="msgTime"></param>
        /// <param name="data"></param>
        public WSResModel(ResCode code, string msg = null, object data = null)
        {
            this.Code = code;
            this.Msg = msg;
            this.Data = data;
        }

        /// <summary>
        /// 结果
        /// </summary>
        public ResCode Code { get; set; }

        /// <summary>
        /// 消息
        /// </summary>
        public string Msg { get; set; }

        /// <summary>
        /// 数据
        /// </summary>
        public object Data { get; set; }
    }

    /// <summary>
    /// 返回状态码
    /// </summary>
    public enum ResCode
    {
        OK = 1,
        Wait = 2,//需等待响应
        ClientAutoUpgrade = 3,//客户端版本需升级
        HeartBeatRes = 8,//心跳响应
        Err = 9//错误
    }
}