using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using WebSocketSharp;
using JsCallExeClient.Helper;

namespace JsCallExeClient.Handler
{
    /// <summary>
    /// 消息处理器
    /// </summary>
    [HandlerAttribute("yb")]//对应websocket或者http请求路径，比如：ws:127.0.0.1:3964/yb   或者： http://127.0.0.1:3964/yb
    public class YBHandler : BaseHandler
    {
        /// <summary>
        /// 处理消息
        /// </summary>
        /// <param name="msg"></param>
        /// <returns></returns>
        public override void HandlerTask(string msg)
        {
            //Thread.Sleep(20000);
            FrmTest frm = new FrmTest();
            frm.TopMost = true;
            frm.ShowDialog();
            //在子类中通过调用父类的ReturnToClient方法将消息json对象返回给网页
            this.ReturnToClient(new WSResModel(ResCode.OK, DateTime.Now.ToString("HH:mm:ss") + "OK：" + msg));
        }
    }
}
