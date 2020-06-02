using System;
using AOT;
using QuickJS.Native;

namespace QuickJS
{
    using UnityEngine;

    // 暂时只做单实例
    public class ScriptEngine
    {
        public const uint VERSION = 0x723;

        private static ScriptRuntime _runtime;

        public static ScriptRuntime GetRuntime()
        {
            return _runtime;
        }

        public static ScriptRuntime GetRuntime(JSContext ctx)
        {
            return _runtime;
        }
        
        public static ScriptRuntime GetRuntime(JSRuntime rt)
        {
            return _runtime;
        }

        public static ScriptContext GetContext(JSContext ctx)
        {
            return _runtime.GetContext(ctx);
        }

        public static ScriptRuntime CreateRuntime()
        {
            _runtime = new ScriptRuntime();
            _runtime.OnDestroy += OnRuntimeDestroy;
            return _runtime;
        }

        public static void Destroy()
        {
            if (_runtime != null)
            {
                _runtime.Destroy();
            }
        }

        private static void OnRuntimeDestroy(ScriptRuntime runtime)
        {
            _runtime = null;
        }
    }
}
