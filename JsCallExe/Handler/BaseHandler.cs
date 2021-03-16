using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using WebSocketSharp.Server;
using JsCallExeClient.Helper;

namespace JsCallExeClient.Handler
{
    /// <summary>
    /// 基类处理器
    /// </summary>
    public abstract class BaseHandler
    {
        public BaseHandler()
        {
            this.CreateTime = DateTime.Now;
            this.OutPara = null;
        }

        #region 字段

        /// <summary>
        /// 任务Id
        /// </summary>
        public string TaskId { get; set; }

        /// <summary>
        /// 请求来源 是否WebSocket
        /// true WebSocket
        /// false Http
        /// </summary>
        public bool ReqIsWebSocket { get; set; }

        /// <summary>
        /// 请求路径
        /// </summary>
        public string Path { get; set; }

        /// <summary>
        /// 输入参数
        /// </summary>
        public string InPara { get; set; }

        /// <summary>
        /// 输出参数
        /// </summary>
        public WSResModel OutPara { get; set; }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreateTime { get; set; }

        /// <summary>
        /// 返回时间
        /// </summary>
        public DateTime ReturnTime { get; set; }

        #endregion

        /// <summary>
        /// 子类重写处理任务主方法
        /// </summary>
        /// <param name="msg">输入消息</param>
        public abstract void HandlerTask(string msg);
        
        /// <summary>
        /// 返回给客户端
        /// </summary>
        /// <param name="resModel"></param>
        public void ReturnToClient(WSResModel resModel)
        {
            try
            {
                if (resModel == null)
                {
                    return;
                }
                this.OutPara = resModel;
                this.ReturnTime = DateTime.Now;
                if (ReqIsWebSocket)
                {
                    //WebSocket 根据任务Id（TaskId）查找链接  进行返回
                    WebSocketServiceHost host;
                    if (WSMain.Instance.HttpServer.WebSocketServices.TryGetServiceHost(string.Format("/{0}", this.Path), out host) && host != null)
                    {
                        foreach (IWebSocketSession item in host.Sessions.Sessions)
                        {
                            MyWebSocketBehavior session = item as MyWebSocketBehavior;
                            if (session != null && session.TaskId == this.TaskId)
                            {
                                session.SendAndClose(this.OutPara);
                                //从队列中移除
                                HandlerTaskManager.Remove(this.TaskId);
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                CommonInfo.Output("WS返回给客户端异常：{0}", ex.Message);
            }
        }
    }

    public class HandlerTaskManager
    {
        /// <summary>
        /// 任务数量变化
        /// </summary>
        public static event Action<BaseHandler> TaskChange;

        /// <summary>
        /// 任务队列
        /// </summary>
        public static ConcurrentDictionary<string, BaseHandler> TaskQueue = new ConcurrentDictionary<string, BaseHandler>();

        /// <summary>
        /// 是否包含任务
        /// </summary>
        /// <param name="taskId"></param>
        /// <returns></returns>
        public static bool ContainsTask(string taskId)
        {
            return TaskQueue.ContainsKey(taskId);
        }

        /// <summary>
        /// 添加一个新任务
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="msg"></param>
        /// <param name="reqIsWebSocket"></param>
        public static bool AddTask(BaseHandler handler)
        {
            if (!TaskQueue.ContainsKey(handler.TaskId))
            {
                TaskQueue.TryAdd(handler.TaskId, handler);
                if (TaskChange != null)
                {
                    TaskChange(handler);
                }
                return true;
            }
            return false;
        }

        /// <summary>
        /// 移除一个任务
        /// </summary>
        /// <param name="taskId"></param>
        /// <returns></returns>
        public static BaseHandler Remove(string taskId)
        {
            BaseHandler hc;
            if (TaskQueue.TryRemove(taskId, out hc))
            {
                if (TaskChange != null)
                {
                    TaskChange(hc);
                }
            }
            return hc;
        }

        /// <summary>
        /// 获取任务结果 如果任务已完成 则直接移除
        /// </summary>
        /// <param name="taskId"></param>
        /// <returns>返回null表示任务未完成</returns>
        public static WSResModel GetTaskReslut(string taskId)
        {
            if (TaskQueue.ContainsKey(taskId))
            {
                if (TaskQueue[taskId].OutPara != null)
                {
                    return Remove(taskId).OutPara;
                }
                return null;
            }
            return new WSResModel(ResCode.Err, "任务不存在~");
        }

        /// <summary>
        /// 定时清理未返回任务
        /// </summary>
        public static void ClearTask()
        {
            try
            {
                List<BaseHandler> tempList = TaskQueue.Values.ToList();
                foreach (BaseHandler item in tempList)
                {
                    if (item.OutPara != null && item.ReturnTime != DateTime.MinValue && (DateTime.Now - item.ReturnTime).TotalSeconds > 20)
                    {
                        Remove(item.TaskId);
                    }
                }
            }
            catch (Exception ex)
            {
                CommonInfo.Output("定时清理未返回任务异常：{0}", ex.Message);
            }
        }
    }
}
