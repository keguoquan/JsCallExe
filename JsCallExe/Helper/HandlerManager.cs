using JsCallExeClient.Handler;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using WebSocketSharp.Server;

namespace JsCallExeClient.Helper
{
    /// <summary>
    /// 处理器定义
    /// </summary>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    public class HandlerAttribute : Attribute
    {
        /// <summary>
        /// 路径名称
        /// </summary>
        public string PathName { get; set; }

        public HandlerAttribute()
        { }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="pathName">路径名称</param>
        public HandlerAttribute(string pathName)
        {
            PathName = pathName;
        }
    }

    /// <summary>
    /// 处理器管理
    /// </summary>
    public class HandlerManager
    {
        public static IDictionary<string, HandlerModel> _handlers =null;
        /// <summary>
        /// 处理器集合
        /// string 路径名称
        /// BaseHandler 对应处理器
        /// </summary>
        public static IDictionary<string, HandlerModel> Handlers
        {
            get
            {
                if (_handlers == null)
                {
                    _handlers = new Dictionary<string, HandlerModel>();
                    InitHandler();
                }
                return _handlers;
            }
        }

        /// <summary>
        /// 创建处理器
        /// </summary>
        /// <param name="key"></param>
        /// <returns></returns>
        public static BaseHandler CreateHandler(string key)
        {
            if (Handlers.ContainsKey(key))
            {
                HandlerModel hmodel = HandlerManager.Handlers[key];
                BaseHandler baseHandler = Activator.CreateInstance(hmodel.HandlerType) as BaseHandler;
                return baseHandler;
            }
            return null;
        }

        /// <summary>
        /// 初始化处理器
        /// </summary>
        /// <param name="assemblies"></param>
        private static void InitHandler()
        {
            if (Handlers.Count > 0) return;
            Type baseType = typeof(BaseHandler);
            Assembly assembly = baseType.Assembly;
            Type[] types = assembly.GetTypes();
            foreach (Type type in types)
            {
                if (type.BaseType != baseType) continue;

                string pathName = string.Empty;
                HandlerAttribute attr = null;

                //初始化表信息
                foreach (HandlerAttribute attribute in type.GetCustomAttributes(typeof(HandlerAttribute), false))
                {
                    pathName = string.IsNullOrEmpty(attribute.PathName) ? type.Name.Replace("Handler", "") : attribute.PathName;
                    attr = attribute;
                }
                //表示该类型未映射位 实体类
                if (string.IsNullOrWhiteSpace(pathName)) continue;

                //所有映射表只添加一次
                if (!Handlers.ContainsKey(pathName))
                {
                    Handlers.Add(pathName, new HandlerModel(pathName, type));
                }
            }
        }
    }

    /// <summary>
    /// 接口方法
    /// </summary>
    public class HandlerModel
    {
        /// <summary>
        /// 路径名称
        /// </summary>
        public string PathName { get; set; }

        /// <summary>
        /// 处理器类型
        /// </summary>
        public Type HandlerType { get; set; }

        private BaseHandler _baseHandler;
        /// <summary>
        /// 处理器
        /// </summary>
        public BaseHandler BaseHandler
        {
            get
            {
                if (_baseHandler == null)
                {
                    _baseHandler = Activator.CreateInstance(this.HandlerType) as BaseHandler;
                }
                return _baseHandler;
            }
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="method"></param>
        /// <param name="methodAttr"></param>
        /// <param name="type">方法所述类型</param>
        public HandlerModel(string pathName, Type type)
        {
            this.PathName = pathName;
            this.HandlerType = type;
        }
    }
}
