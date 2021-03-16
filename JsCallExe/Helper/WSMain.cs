using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using WebSocketSharp.Server;
using System.Threading;
using WebSocketSharp.Net;
using System.Collections.Concurrent;
using JsCallExeClient;
using System.ComponentModel;
using JsCallExeClient.Handler;
using System.Reflection;
using System.IO;
using System.Threading.Tasks;
using System.Diagnostics;

namespace JsCallExeClient.Helper
{
    /// <summary>
    /// WebSocket主类
    /// </summary>
    public class WSMain
    {
        #region 字段

        /// <summary>
        /// 检测客户端连接 处理线程
        /// </summary>
        private BackgroundWorker _doWork_checkClient;
        /// <summary>
        /// 检测线程 信号灯
        /// </summary>
        private AutoResetEvent _doWorkARE = new AutoResetEvent(true);

        private int _heartBeatSecond = 8000;
        /// <summary>
        /// 获取或设置 心跳检测时间间隔(单位毫秒)  默认值(8000毫秒)
        /// 此值必须与客户端设置相同值 最小值1000毫秒
        /// </summary>
        public int HeartBeatSecond
        {
            get { return _heartBeatSecond; }
            set
            {
                if (value < 1000)
                {
                    throw new Exception("心跳检测时间间隔必须大于等于1秒");
                }
                _heartBeatSecond = value;
            }
        }
        /// <summary>
        /// 服务器是否正在运行
        /// </summary>
        public bool IsRunning { get; private set; }

        /// <summary>
        /// WebSocketServer
        /// </summary>
        private HttpServer _httpServer;
        /// <summary>
        /// WebSocketServer对象
        /// </summary>
        public HttpServer HttpServer
        {
            get
            {
                return _httpServer;
            }
        }

        #endregion

        /// <summary>
        /// 启动WebSocket
        /// </summary>
        public void Start()
        {
            _httpServer = new HttpServer(CommonInfo.WsPort);
            _httpServer.OnGet += _httpServer_Handler;
            _httpServer.OnPost += _httpServer_Handler;
            foreach (KeyValuePair<string, HandlerModel> item in HandlerManager.Handlers)
            {
                _httpServer.AddWebSocketService<MyWebSocketBehavior>(string.Format("/{0}", item.Key));
            }
            _httpServer.Start();

            //启动检测
            IsRunning = true;
            _doWork_checkClient = new BackgroundWorker();
            _doWork_checkClient.DoWork += DoWorkCheckMethod;
            _doWork_checkClient.RunWorkerAsync();
        }

        #region Http请求处理

        /// <summary>
        /// Http请求处理
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void _httpServer_Handler(object sender, HttpRequestEventArgs e)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(e.Request.Url.AbsolutePath))
                {
                    HttpResponse(e, new WSResModel(ResCode.Err, "请求路径不存在！"));
                    return;
                }
                string path = e.Request.Url.AbsolutePath.TrimStart('/');
                string taskId = e.Request.QueryString["taskId"];
                string clientVer = e.Request.QueryString["clientVer"];//客户端版本号
                if (path == "TestConnect")
                {
                    HttpResponse(e, new WSResModel(ResCode.OK));
                    return;
                }
                if (!string.IsNullOrWhiteSpace(clientVer) && CommonInfo.Version != clientVer)
                {
                    HttpResponse(e, new WSResModel(ResCode.ClientAutoUpgrade, string.Format("请将客户端版本升级至{0}", clientVer)));
                    Program.ExitAndStartAutoUpgrade();
                    return;
                }
                if (string.IsNullOrWhiteSpace(taskId))
                {
                    HttpResponse(e, new WSResModel(ResCode.Err, "请求参数TaskId必须！"));
                    return;
                }
                if (path == "_getTaskReslut")
                {
                    Stopwatch sw = new Stopwatch();
                    sw.Start();
                    GetTaskWaitReslut(taskId, e, sw);
                }
                else
                {
                    BaseHandler handler = HandlerManager.CreateHandler(path);
                    if (handler == null)
                    {
                        HttpResponse(e, new WSResModel(ResCode.Err, string.Format("请求路径{0}未找到对应Handler", path)));
                        return;
                    }
                    //参数解析
                    string msg = string.Empty;
                    if (e.Request.ContentLength64 > 0)
                    {
                        using (System.IO.StreamReader stream = new System.IO.StreamReader(e.Request.InputStream, Encoding.UTF8))
                        {
                            msg = stream.ReadToEnd();
                        }
                    }
                    //断开并返回客户端
                    HttpResponse(e, new WSResModel(ResCode.Wait));
                    //线程继续处理消息
                    handler.Path = path;
                    handler.TaskId = taskId;
                    handler.ReqIsWebSocket = false;
                    handler.InPara = msg;
                    if (HandlerTaskManager.AddTask(handler))
                    {
                        handler.HandlerTask(msg);
                    }
                }
            }
            catch (Exception ex)
            {
                HttpResponse(e, new WSResModel(ResCode.Err, string.Format("服务器异常:{0}", ex.Message)));
            }
        }

        /// <summary>
        /// 等待并获取任务结果
        /// </summary>
        /// <param name="taskId"></param>
        /// <param name="e"></param>
        private void GetTaskWaitReslut(string taskId, HttpRequestEventArgs e, Stopwatch sw)
        {
            try
            {
                if (sw.ElapsedMilliseconds > HeartBeatSecond)
                {
                    //超过心跳 则返回给客户端，以便下次继续请求
                    HttpResponse(e, new WSResModel(ResCode.Wait));
                    return;
                }
                //获取任务状态
                WSResModel resModel = HandlerTaskManager.GetTaskReslut(taskId);
                if (resModel != null)
                {
                    HttpResponse(e, resModel);
                }
                else
                {
                    Thread.Sleep(50);
                    GetTaskWaitReslut(taskId, e, sw);
                }
            }
            catch (Exception ex)
            {
                HttpResponse(e, new WSResModel(ResCode.Err, string.Format("服务器内部异常:{0}", ex.Message)));
            }
        }

        /// <summary>
        /// Http响应客户端
        /// </summary>
        /// <param name="e"></param>
        /// <param name="resMsg"></param>
        private void HttpResponse(HttpRequestEventArgs e, WSResModel resModel)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(resModel);
                byte[] content = System.Text.Encoding.UTF8.GetBytes(json);

                e.Response.StatusCode = (int)HttpStatusCode.OK;
                e.Response.AddHeader("Access-Control-Allow-Origin", "*");//允许跨域访问
                e.Response.ContentEncoding = Encoding.UTF8;
                e.Response.ContentLength64 = content.Length;
                e.Response.OutputStream.Write(content, 0, content.Length);
                e.Response.OutputStream.Close();
                e.Response.Close();
            }
            catch (Exception ex)
            {
                CommonInfo.Output("Http响应客户端异常：{0}", ex.Message);
            }
        }

        #endregion

        /// <summary>
        /// 后台线程方法
        /// 主要处理客户端连接
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void DoWorkCheckMethod(object sender, DoWorkEventArgs e)
        {
            int timeout = HeartBeatSecond * 3;//连续3次检测无心跳 则表示客户端已死掉了
            while (IsRunning)
            {
                try
                {
                    _doWorkARE.Reset();
                    _doWorkARE.WaitOne(HeartBeatSecond);

                    //定时清理任务
                    HandlerTaskManager.ClearTask();

                    foreach (WebSocketServiceHost host in _httpServer.WebSocketServices.Hosts)
                    {
                        List<IWebSocketSession> tempList = host.Sessions.Sessions.ToList();
                        foreach (var item in tempList)
                        {
                            MyWebSocketBehavior handler = item as MyWebSocketBehavior;
                            if (handler != null)
                            {
                                //检测连接
                                if ((DateTime.Now - handler.LastRecvTime).TotalSeconds > timeout)
                                {
                                    //断开客户端连接
                                    CloseClientSocket(handler);
                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    CommonInfo.Output("检测连接异常：{0}", ex.Message);
                }
            }
        }

        /// <summary>
        /// 关闭客户端连接
        /// </summary>
        /// <param name="session"></param>
        public void CloseClientSocket(MyWebSocketBehavior handler)
        {
            try
            {
                handler.Context.WebSocket.Close();
            }
            catch (Exception ex)
            {
                CommonInfo.Output("断开客户端异常：{0}", ex.Message);
            }
        }

        /// <summary>
        /// 退出WebSocket
        /// </summary>
        public void Stop()
        {
            //关闭WebScoket
            IsRunning = false;
            _doWorkARE.Set();
            HandlerTaskManager.TaskQueue.Clear();
            _httpServer.Stop(WebSocketSharp.CloseStatusCode.Abnormal, "服务退出");
        }

        #region 单例对象

        private static WSMain _instance;
        /// <summary>
        /// 单例对象
        /// </summary>
        public static WSMain Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new WSMain();
                }
                return _instance;
            }
        }
        #endregion
    }

    /// <summary>
    /// WebSocket消息处理类
    /// </summary>
    public class MyWebSocketBehavior : WebSocketBehavior
    {
        #region 字段

        /// <summary>
        /// 最后接受数据时间
        /// </summary>
        public DateTime LastRecvTime = DateTime.Now;

        /// <summary>
        /// 任务Id
        /// </summary>
        public string TaskId
        {
            get
            {
                return this.Context.QueryString["taskId"];
            }
        }

        /// <summary>
        /// 客户端版本号
        /// </summary>
        public string ClientVer
        {
            get
            {
                return this.Context.QueryString["clientVer"];
            }
        }

        /// <summary>
        /// 请求路径
        /// </summary>
        public string PathName
        {
            get
            {
                return this.Context.RequestUri.AbsolutePath.TrimStart('/');
            }
        }

        /// <summary>
        /// 连接数发生变化事件
        /// </summary>
        public static event Action<MyWebSocketBehavior> ConnectCountChange;

        #endregion

        /// <summary>
        /// 新连接
        /// </summary>
        protected override void OnOpen()
        {
            if (ConnectCountChange != null)
            {
                ConnectCountChange(this);
            }
            if (!string.IsNullOrWhiteSpace(this.ClientVer) && CommonInfo.Version != this.ClientVer)
            {
                SendAndClose(new WSResModel(ResCode.ClientAutoUpgrade, string.Format("请将客户端版本升级至{0}", this.ClientVer)));
                Program.ExitAndStartAutoUpgrade();
            }
            //CommonInfo.Output("新连接{0}", this.TaskId);
        }

        /// <summary>
        /// 新消息
        /// </summary>
        /// <param name="e"></param>
        protected override void OnMessage(WebSocketSharp.MessageEventArgs e)
        {
            try
            {
                LastRecvTime = DateTime.Now;
                if (e.IsText)
                {
                    switch (e.Data)
                    {
                        case "HeartBeat":
                            this.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new WSResModel(ResCode.HeartBeatRes)));
                            return;
                        case "TaskIsExist":
                            if (HandlerTaskManager.ContainsTask(this.TaskId))
                            {
                                this.Send(Newtonsoft.Json.JsonConvert.SerializeObject(new WSResModel(ResCode.Wait)));
                            }
                            else
                            {
                                SendAndClose(new WSResModel(ResCode.Err, "任务不存在！"));
                            }
                            return;
                    }
                }
                BaseHandler handler = HandlerManager.CreateHandler(PathName);
                if (handler == null)
                {
                    SendAndClose(new WSResModel(ResCode.Err, string.Format("请求路径{0}未找到对应Handler", PathName)));
                    return;
                }
                handler.Path = this.PathName;
                handler.TaskId = this.TaskId;
                handler.ReqIsWebSocket = true;
                handler.InPara = e.Data;
                //添加任务并执行
                if (HandlerTaskManager.AddTask(handler))
                {
                    
                    ThreadPool.QueueUserWorkItem((state) =>
                    {
                        BaseHandler bh = state as BaseHandler;
                        try
                        {
                            bh.HandlerTask(bh.InPara);
                        }
                        catch (Exception ex)
                        {
                            bh.ReturnToClient(new WSResModel(ResCode.Err, ex.Message));
                        }
                    }, handler);
                }
            }
            catch (Exception ex)
            {
                CommonInfo.Output("WS处理消息异常：{0}", ex.Message);
                SendAndClose(new WSResModel(ResCode.Err, string.Format("服务器WS处理消息异常", ex.Message)));
            }
        }

        /// <summary>
        /// 错误
        /// </summary>
        /// <param name="e"></param>
        protected override void OnError(WebSocketSharp.ErrorEventArgs e)
        {
            if (e.Exception != null)
            {
                CommonInfo.Output("连接{0}错误：{1}", this.TaskId, e.Exception.GetBaseException().Message);
            }
            else
            {
                CommonInfo.Output("连接{0}错误：{1}", this.TaskId, e.Message);
            }
        }

        /// <summary>
        /// 关闭
        /// </summary>
        /// <param name="e"></param>
        protected override void OnClose(WebSocketSharp.CloseEventArgs e)
        {
            if (ConnectCountChange != null)
            {
                ConnectCountChange(this);
            }
            //CommonInfo.Output("断开连接{0}", this.TaskId);
        }

        /// <summary>
        /// 结果返回给客户端并断开链接
        /// </summary>
        /// <param name="msg"></param>
        public void SendAndClose(WSResModel resModel)
        {
            try
            {
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(resModel);
                this.Send(json);
                try
                {
                    this.Context.WebSocket.Close();
                }
                catch (Exception ex) { }
            }
            catch (Exception ex)
            {
                CommonInfo.Output("发送消息异常：{0}", ex.Message);
            }
        }
    }

}
