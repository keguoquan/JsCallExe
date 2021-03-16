var SocketHost = "127.0.0.1:3964";//主机地址
var ClientVer = "1.1";//所需客户端版本号
//客户端未运行提示消息
var AlertClientRunMsg = "<div style='margin:15px;' id='div_AlertClientRunMsg'><font color='#FF00FF'>Web客户端未安装启动，点击这里<a href='JsCallExeClient.exe' target='_self'>下载执行安装</a>  <br>（若此前已安装过，可<a href='JsCallExeClient://' target='_self'>点这里直接再次启动</a>），成功后请刷新本页面。</font></div>";

/* 
* 本地资源调用封装
* url:			调用地址 不需要主机和端口，例如：/yb
* data:			输入参数
* callback:	    成功回调函数,函数签名为  function(data), data参数为服务端返回对象{"Code":1,"Msg":"","Data":null}
* output:	    日志输出函数,函数签名为  function(msg,isHeart), msg输出字符串  isHeart是否心跳，重试消息
*/
function SocketCall(url, data, callback, output) {
    //输入参数json序列号
    if (typeof data == "object" && typeof (data) != null) {
        data = JSON.stringify(data);
    }
    //发送请求
    if ('WebSocket' in window) {
        return new WSocket(url, data, callback, output);
    } else {
        return new HttpSocket(url, data, callback, output);
    }
}

/* 
* webscoket调用封装
* url:			调用地址
* data:			输入参数
* callback:	    成功回调函数,函数签名为  function(data), data参数为服务端返回对象{"Code":1,"Msg":"","Data":null}
* output:	    日志输出函数,函数签名为  function(msg,isHeart), msg输出字符串  isHeart是否心跳，重试消息
*/
var WSocket = function (url, data, callback, output) {
    this.url = url;
    this.data = data;
    this.callback = callback;
    this.output = output;
    this.outMsg = function (msg, isHeart) {
        if (!isHeart) isHeart = false;
        if (this.output) {
            this.output(msg, isHeart);
        }
        if (!isHeart) {
            console.log(msg);
        }
    };
    if (this.data == "HeartBeat" || this.data == "TaskIsExist") {
        this.outMsg("发送内容不能是关键字：HeartBeat或TaskIsExist");
        return;
    }
    //初始化
    this.taskId = GetTaskId();//任务Id
    this.url = "ws://" + SocketHost + GetUrlQueryString(this.url, this.taskId);//连接地址    
    this.IsComplate = false;//任务是否已完成
    this.lockReconnect = false;//是否锁定重连
    this.rcTimeoutObj = null;//重连定时器
    this.heartCheck = new WSHeartCheck(this);//心跳检测对象
    this.webSocket = null;//webSocket链接对象
    this.taskIsSend = false;//任务请求是否已发送
    this.IsUpgrade = false;//当前是否版本升级

    this.Open = function () {//连接
        this.webSocket = new WebSocket(this.url);
        this.webSocket.SrcWSocketSelf = this;
        if (!window.WSocketObjs) {
            window.WSocketObjs = new Array();
        }
        window.WSocketObjs.push(this);
        //连接成功建立的回调方法
        this.webSocket.onopen = function (event) {
            this.SrcWSocketSelf.IsUpgrade = false;
            RemoveAlertClientRunMsg();
            //心跳检测
            this.SrcWSocketSelf.heartCheck.reset().start();
            //发送任务数据
            if (!this.SrcWSocketSelf.taskIsSend) {
                this.SrcWSocketSelf.webSocket.send(this.SrcWSocketSelf.data);
                this.SrcWSocketSelf.taskIsSend = true;
                this.SrcWSocketSelf.outMsg("ws发送任务成功，等待返回...");
            } else {
                //判断任务是否存在
                this.SrcWSocketSelf.webSocket.send("TaskIsExist");
            }
        };
        //接收到消息的回调方法
        this.webSocket.onmessage = function (event) {
            var resModel = JSON.parse(event.data);
            //处理消息
            if (resModel.Code == 8) {
                this.SrcWSocketSelf.outMsg("ws接收心跳响应：" + event.data, true);
                this.SrcWSocketSelf.heartCheck.reset().start();//重置并开始下一个心跳检测
            } else if (resModel.Code == 2) {//继续等待
                this.SrcWSocketSelf.outMsg("重连成功，继续等待返回...");
            } else if (resModel.Code == 3) {//版本升级等待
                this.SrcWSocketSelf.outMsg("客户端版本升级中，请等待...");
                this.SrcWSocketSelf.taskIsSend = false;
                this.SrcWSocketSelf.IsUpgrade = true;
                this.SrcWSocketSelf.Reconnect();//启动重连
            } else if (this.SrcWSocketSelf.callback) {
                this.SrcWSocketSelf.IsComplate = true;
                this.SrcWSocketSelf.Close();
                this.SrcWSocketSelf.callback(resModel);
            } else {
                this.SrcWSocketSelf.IsComplate = true;
                this.SrcWSocketSelf.Close();
                this.SrcWSocketSelf.outMsg("ws接收数据：" + event.data);
            }
        };
        //连接发生错误的回调方法
        this.webSocket.onerror = function (event) {
            //启动应用
            if (this.SrcWSocketSelf.IsUpgrade != true) {
                StartLocalExe();
            }
            //重新链接
            this.SrcWSocketSelf.Reconnect();
        };
        //连接关闭的回调方法
        this.webSocket.onclose = function (event) {
            this.SrcWSocketSelf.outMsg("ws连接已关闭", true);
            this.SrcWSocketSelf.heartCheck.reset();//心跳检测
            this.SrcWSocketSelf.Reconnect();
        };
    };
    this.Open();

    //以下定义公共方法
    this.Reconnect = function () {//重连
        if (this.lockReconnect) {
            return;
        };
        if (this.IsComplate) {
            return;
        }
        var self = this;
        this.lockReconnect = true;
        this.rcTimeoutObj && clearTimeout(this.rcTimeoutObj);
        this.rcTimeoutObj = setTimeout(function () {
            self.lockReconnect = false;
            self.outMsg('ws开始重试...', true);
            self.Open();
        }, 1000);
    };
    this.SendData = function (data) {//发送数据
        if (this.webSocket == null) {
            return "链接未初始化！";
        }
        if (this.webSocket.readyState != 1) {
            return "链接不是正常状态";
        }
        this.webSocket.send(data);
        return "";
    };
    this.Close = function (data) {//关闭连接
        if (this.webSocket != null) {
            this.heartCheck.reset();
            this.webSocket.close();
        }
    };
};

//监听窗口关闭事件，当窗口关闭时，主动去关闭websocket连接或者HttpSocket。
window.onbeforeunload = function () {
    if (window.WSocketObjs) {
        for (var i in window.WSocketObjs) {
            window.WSocketObjs[i].Close();
        }
    }
};

//websocket心跳检测
var WSHeartCheck = function (wSocket) {
    this.timeout = 8000;//心跳间隔 8秒
    this.timeoutObj = null;
    this.serverTimeoutObj = null;
    this.wSocket = wSocket;

    this.reset = function () {//重置
        clearTimeout(this.timeoutObj);
        clearTimeout(this.serverTimeoutObj);
        return this;
    };

    this.start = function () {//开始心跳
        var self = this;
        this.timeoutObj && clearTimeout(this.timeoutObj);
        this.serverTimeoutObj && clearTimeout(this.serverTimeoutObj);
        this.timeoutObj = setTimeout(function () {
            //这里发送一个心跳，后端收到后，返回一个心跳消息，onmessage拿到返回的心跳就说明连接正常
            self.wSocket.SendData("HeartBeat");
            self.serverTimeoutObj = setTimeout(function () { // 如果超过5秒还没重置，说明链接断开了
                self.wSocket.Close();//onclose会执行reconnect
            }, 5000);
        }, this.timeout);
    };
}

/* 
* http调用封装
* url:			调用地址
* data:			输入参数
* callback:	    成功回调函数,函数签名为  function(data), data参数为服务端返回对象{"Code":1,"Msg":"","Data":null}
* output:	    日志输出函数,函数签名为  function(msg,isHeart), msg输出字符串  isHeart是否心跳，重试消息
*/
var HttpSocket = function (url, data, callback, output) {
    this.url = url;
    this.data = data;
    this.callback = callback;
    this.output = output;
    this.taskId = GetTaskId();//任务Id
    this.url = "http://" + SocketHost + GetUrlQueryString(this.url, this.taskId);
    this.taskIsSend = false;//任务请求是否已发送
    this.IsComplate = false;//任务是否已完成
    this.lockReconnect = false;//是否锁定重连
    this.rcTimeoutObj = null;//重连定时器
    this.IsUpgrade = false;//当前是否版本升级
    this.outMsg = function (msg, isHeart) {
        if (!isHeart) isHeart = false;
        if (this.output) {
            this.output(msg, isHeart);
        }
        if (!isHeart) {
            console.log(msg);
        }
    };
    //发送任务请求
    this.Open = function () {
        if (this.taskIsSend) {
            return;
        }
        WAjax.post(this.url, this.data, function (resData, state) {
            //请求成功
            RemoveAlertClientRunMsg();
            state.outMsg("htp发送任务成功，等待返回...");
            state.taskIsSend = true;
            state.IsUpgrade = false;
            if (resData.Code == 2) {//开始获取任务结果
                state.IsComplate = false;
                state.GetTaskReslut();
            } else if (resData.Code == 3) {//版本升级等待
                state.outMsg("客户端版本升级中，请等待...");
                state.taskIsSend = false;
                state.IsUpgrade = true;
                //重新发送
                state.Reconnect();
            } else {
                //返回结果
                state.IsComplate = true;
                if (state.callback) {
                    state.callback(resData);
                }
            }
        }, function (err, state) {//ajax调用失败
            state.outMsg("htp发送任务失败：" + err, true);
            //重新发送
            state.Reconnect();
        }, this);
    };
    this.Open();

    //重新发送
    this.Reconnect = function () {
        if (this.lockReconnect) {
            return;
        };
        if (this.IsComplate) {
            return;
        }
        var self = this;
        this.lockReconnect = true;
        this.rcTimeoutObj && clearTimeout(this.rcTimeoutObj);
        this.rcTimeoutObj = setTimeout(function () {
            self.lockReconnect = false;
            self.outMsg('htp开始重试...', true);
            self.Open();
        }, 1000);
    };

    //获取任务结果
    this.GetTaskReslut = function () {
        //地址
        var gUrl = "http://" + SocketHost + "/_getTaskReslut" + GetUrlQueryString("", this.taskId);
        //get请求
        WAjax.get(gUrl, function (resData, state) {
            state.outMsg("htp获取任务状态：" + JSON.stringify(resData), true);
            if (resData.Code == 2) {//继续获取        
                state.IsComplate = false;
                state.GetTaskReslut();
            } else {//返回结果
                state.IsComplate = true;
                if (state.callback) {
                    state.callback(resData);
                }
            }
        }, function (err, state) {
            state.outMsg("htp获取任务状态失败：" + err, true);//输出错误
            state.GetTaskReslut();//继续获取
        }, this);
    }
}

//获取任务Id
function GetTaskId() {
    var now = new Date();
    var taskId = (now.getMonth() + 1).toString();
    taskId += now.getDate();
    taskId += now.getHours();
    taskId += now.getMinutes();
    taskId += now.getSeconds();
    taskId += now.getMilliseconds();
    taskId += Math.random().toString().substr(3, 5);//3位随机数
    return taskId;
}

//启动本地exe
function StartLocalExe() {
    var ifrm = document.getElementById("ifrm_StartLocalExe");
    if (!ifrm) {
        ifrm = document.createElement('iframe');
        ifrm.id = "ifrm_StartLocalExe";
        ifrm.src = "JsCallExeClient://";
        ifrm.width = 0;
        ifrm.height = 0;
        document.body.appendChild(ifrm);
    }
    document.getElementById('ifrm_StartLocalExe').contentWindow.location.reload();

    //var linkTmp = document.createElement('a');
    //linkTmp.href = 'JsCallExeClient://';
    //linkTmp.click();
};

//获取地址请求参数
function GetUrlQueryString(url, taskId) {
    //参数中添加time 以兼容在Ie9以下浏览器中 相同http地址，浏览器的缓存不处理问题
    if (url.indexOf("?") > 0) {
        return url + "&taskId=" + taskId + "&clientVer=" + ClientVer + "&time" + new Date().getTime();
    } else {
        return url + "?taskId=" + taskId + "&clientVer=" + ClientVer + "&time" + new Date().getTime();
    }
}

//Ajax调用
var WAjax = {
    get: function (url, onSuccess, onError, state) {
        var xhr = new XMLHttpRequest();
        if (window.XMLHttpRequest) {
            xhr = new XMLHttpRequest();
        } else {
            xhr = new ActiveXObject("Microsoft.XMLHTTP");
        }
        xhr.open('GET', url, true);
        xhr.onreadystatechange = function () {
            if (xhr.readyState == 4) {
                if (xhr.status == 200) {
                    //请求成功
                    var model = JSON.parse(xhr.responseText);
                    if (onSuccess) {
                        onSuccess(model, state);
                    }
                } else {
                    if (onError) {
                        onError("http状态码" + xhr.status, state);
                    }
                }
            }
        }
        xhr.onerror = function (evt) {
            //启动应用
            if (state.IsUpgrade != true) {
                StartLocalExe();
            }
            //此处不执行错误回调，因为在onreadystatechange里面也会执行
        }
        xhr.send();
    },

    post: function (url, data, onSuccess, onError, state) {
        var xhr = new XMLHttpRequest();
        if (window.XMLHttpRequest) {
            xhr = new XMLHttpRequest();
        } else {
            xhr = new ActiveXObject("Microsoft.XMLHTTP");
        }
        xhr.open('POST', url, true);
        xhr.onreadystatechange = function () {
            if (xhr.readyState == 4) {
                if (xhr.status == 200) {
                    //请求成功
                    var model = JSON.parse(xhr.responseText);
                    if (onSuccess) {
                        onSuccess(model, state);
                    }
                } else {
                    if (onError) {
                        onError("http状态码" + xhr.status, state);
                    }
                }
            }
        }
        xhr.onerror = function (evt) {
            //启动应用
            if (state.IsUpgrade != true) {
                StartLocalExe();
            }
            //此处不执行错误回调，因为在onreadystatechange里面也会执行
        }
        xhr.send(data);
    }
}

//初始化时判断客户端是否安装启用
function TestClientIsRun() {
    var url = "http://" + SocketHost + GetUrlQueryString("/TestConnect", GetTaskId());
    WAjax.get(url, function (resModel) {
        if (resModel.Code != 1) {
            document.body.innerHTML = AlertClientRunMsg + document.body.innerHTML;
        }
    }, function () {
        document.body.innerHTML = AlertClientRunMsg + document.body.innerHTML;
    }, {});
}
//删除提示消息
function RemoveAlertClientRunMsg() {
    var alDiv = document.getElementById("div_AlertClientRunMsg")
    if (alDiv) {
        try {
            alDiv.remove();
        } catch (e) {
            alDiv.removeNode(true);//IE9及以下浏览器
        }
    }
}
TestClientIsRun();//加载