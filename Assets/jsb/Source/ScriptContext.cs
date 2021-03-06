﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using QuickJS.Binding;
using QuickJS.Native;
using QuickJS.Utils;

namespace QuickJS
{
    public partial class ScriptContext
    {
        public event Action<ScriptContext> OnDestroy;
        public event Action<int> OnAfterDestroy;

        private ScriptRuntime _runtime;
        private int _contextId;
        private JSContext _ctx;
        private AtomCache _atoms;
        private JSStringCache _stringCache;

        // 保存已加载模块的信息
        private Dictionary<string, string> _loadedModuleHash;
        private JSValue _moduleCache; // commonjs module cache
        private JSValue _require; // require function object 
        private bool _isValid;
        private Regex _stRegex;

        private JSValue _globalObject;
        private JSValue _operatorCreate;
        private JSValue _proxyConstructor;
        private JSValue _objectConstructor;
        private JSValue _numberConstructor;
        private JSValue _stringConstructor;
        private JSValue _functionConstructor;

        // id = context slot index + 1
        public int id { get { return _contextId; } }

        public ScriptContext(ScriptRuntime runtime, int contextId)
        {
            _isValid = true;
            _runtime = runtime;
            _contextId = contextId;
            _ctx = JSApi.JS_NewContext(_runtime);
            JSApi.JS_SetContextOpaque(_ctx, (IntPtr)_contextId);
            JSApi.JS_AddIntrinsicOperators(_ctx);
            _atoms = new AtomCache(_ctx);
            _stringCache = new JSStringCache(_ctx);
            _moduleCache = JSApi.JS_NewObject(_ctx);
            _loadedModuleHash = new Dictionary<string, string>();

            _globalObject = JSApi.JS_GetGlobalObject(_ctx);
            _objectConstructor = JSApi.JS_GetProperty(_ctx, _globalObject, JSApi.JS_ATOM_Object);
            _numberConstructor = JSApi.JS_GetProperty(_ctx, _globalObject, JSApi.JS_ATOM_Number);
            _proxyConstructor = JSApi.JS_GetProperty(_ctx, _globalObject, JSApi.JS_ATOM_Proxy);
            _stringConstructor = JSApi.JS_GetProperty(_ctx, _globalObject, JSApi.JS_ATOM_String);
            _functionConstructor = JSApi.JS_GetProperty(_ctx, _globalObject, JSApi.JS_ATOM_Function);
            _operatorCreate = JSApi.JS_UNDEFINED;

            var operators = JSApi.JS_GetProperty(_ctx, _globalObject, JSApi.JS_ATOM_Operators);
            if (!operators.IsNullish())
            {
                if (operators.IsException())
                {
                    _ctx.print_exception();
                }
                else
                {
                    var create = JSApi.JS_GetProperty(_ctx, operators, GetAtom("create"));
                    JSApi.JS_FreeValue(_ctx, operators);
                    if (create.IsException())
                    {
                        _ctx.print_exception();
                    }
                    else
                    {
                        if (JSApi.JS_IsFunction(_ctx, create) == 1)
                        {
                            _operatorCreate = create;

                            // Function.prototype[Symbol.operatorSet] = Operators.create();
                            CreateDefaultOperators(_functionConstructor);
                        }
                        else
                        {
                            JSApi.JS_FreeValue(_ctx, create);
                        }
                    }
                }
            }
        }

        private unsafe void CreateDefaultOperators(JSValue constructor)
        {
            var rval = JSApi.JS_Call(_ctx, _operatorCreate);
            if (rval.IsException())
            {
                var ex = _ctx.GetExceptionString();
                GetLogger()?.Write(LogLevel.Error, ex);
            }
            else
            {
                JSApi.JS_DefinePropertyValue(_ctx, constructor, JSApi.JS_ATOM_Symbol_operatorSet, rval, JSPropFlags.DEFAULT);
            }
        }

        public bool IsValid()
        {
            lock (this)
            {
                return _isValid;
            }
        }

        public IAsyncManager GetAsyncManager()
        {
            return _isValid ? _runtime.GetAsyncManager() : null;
        }

        public TimerManager GetTimerManager()
        {
            return _runtime.GetTimerManager();
        }

        public IScriptLogger GetLogger()
        {
            return _runtime.GetLogger();
        }

        public TypeDB GetTypeDB()
        {
            return _runtime.GetTypeDB();
        }

        public ObjectCache GetObjectCache()
        {
            return _runtime.GetObjectCache();
        }

        public ScriptRuntime GetRuntime()
        {
            return _runtime;
        }

        public bool IsContext(JSContext ctx)
        {
            return ctx == _ctx;
        }

        //NOTE: 返回值不需要释放, context 销毁时会自动释放所管理的 Atom
        public JSAtom GetAtom(string name)
        {
            return _atoms.GetAtom(name);
        }

        public JSStringCache GetStringCache()
        {
            return _stringCache;
        }

        public void Destroy()
        {
            lock (this)
            {
                if (!_isValid)
                {
                    return;
                }
                _isValid = false;
            }

            try
            {
                OnDestroy?.Invoke(this);
            }
            catch (Exception e)
            {
                _runtime.GetLogger()?.WriteException(e);
            }
            _stringCache.Destroy();
            _atoms.Clear();

            JSApi.JS_FreeValue(_ctx, _proxyConstructor);
            JSApi.JS_FreeValue(_ctx, _objectConstructor);
            JSApi.JS_FreeValue(_ctx, _numberConstructor);
            JSApi.JS_FreeValue(_ctx, _stringConstructor);
            JSApi.JS_FreeValue(_ctx, _functionConstructor);
            JSApi.JS_FreeValue(_ctx, _globalObject);
            JSApi.JS_FreeValue(_ctx, _operatorCreate);

            JSApi.JS_FreeValue(_ctx, _moduleCache);
            JSApi.JS_FreeValue(_ctx, _require);
            JSApi.JS_FreeContext(_ctx);
            var id = _contextId;
            _contextId = -1;
            _ctx = JSContext.Null;
            try
            {
                OnAfterDestroy?.Invoke(id);
            }
            catch (Exception e)
            {
                _runtime.GetLogger()?.WriteException(e);
            }
        }

        public void FreeValue(JSValue value)
        {
            _runtime.FreeValue(value);
        }

        public void FreeValues(JSValue[] values)
        {
            _runtime.FreeValues(values);
        }

        public unsafe void FreeValues(int argc, JSValue* values)
        {
            _runtime.FreeValues(argc, values);
        }

        ///<summary>
        /// 获取全局对象 (增加引用计数)
        ///</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue GetGlobalObject()
        {
            return JSApi.JS_DupValue(_ctx, _globalObject);
        }

        ///<summary>
        /// 获取 string.constructor (增加引用计数)
        ///</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue GetStringConstructor()
        {
            return JSApi.JS_DupValue(_ctx, _stringConstructor);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue GetFunctionConstructor()
        {
            return JSApi.JS_DupValue(_ctx, _functionConstructor);
        }

        ///<summary>
        /// 获取 number.constructor (增加引用计数)
        ///</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue GetNumberConstructor()
        {
            return JSApi.JS_DupValue(_ctx, _numberConstructor);
        }

        public JSValue GetObjectConstructor()
        {
            return JSApi.JS_DupValue(_ctx, _objectConstructor);
        }

        public JSValue GetProxyConstructor()
        {
            return JSApi.JS_DupValue(_ctx, _proxyConstructor);
        }

        public bool CheckNumberType(JSValue jsValue)
        {
            //TODO: 是否成立? 否则需要使用 jsapi equals
            if (jsValue == _numberConstructor)
            {
                return true;
            }

            return false;
        }

        public bool CheckStringType(JSValue jsValue)
        {
            //TODO: 是否成立? 否则需要使用 jsapi equals
            if (jsValue == _stringConstructor)
            {
                return true;
            }

            return false;
        }

        ///<summary>
        /// 获取 operator.create (增加引用计数)
        ///</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue GetOperatorCreate()
        {
            return JSApi.JS_DupValue(_ctx, _operatorCreate);
        }

        //NOTE: 返回值需要调用者 free 
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue _get_commonjs_module(string module_id)
        {
            var prop = GetAtom(module_id);
            return JSApi.JS_GetProperty(_ctx, _moduleCache, prop);
        }

        public JSValue _new_commonjs_module(string module_id, JSValue exports_obj, bool loaded)
        {
            return _new_commonjs_module(module_id, module_id, exports_obj, loaded);
        }

        //NOTE: 返回值需要调用者 free
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public JSValue _new_commonjs_module(string module_id, string filename, JSValue exports_obj, bool loaded)
        {
            var module_obj = JSApi.JS_NewObject(_ctx);
            var module_id_atom = GetAtom(module_id);
            var module_id_obj = JSApi.JS_AtomToString(_ctx, module_id_atom);
            var filename_atom = GetAtom(filename);

            JSApi.JS_SetProperty(_ctx, _moduleCache, module_id_atom, JSApi.JS_DupValue(_ctx, module_obj));
            JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("id"), JSApi.JS_DupValue(_ctx, module_id_obj));
            JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("filename"), JSApi.JS_AtomToString(_ctx, filename_atom));
            JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("cache"), JSApi.JS_DupValue(_ctx, _moduleCache));
            JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("loaded"), JSApi.JS_NewBool(_ctx, loaded));
            JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("exports"), JSApi.JS_DupValue(_ctx, exports_obj));
            JSApi.JS_FreeValue(_ctx, module_id_obj);

            _loadedModuleHash[module_id] = module_id;
            return module_obj;
        }

        // retrn the main module value (commonjs module) directly
        public JSValue _dup_commonjs_main_module()
        {
            return JSApi.JS_GetProperty(_ctx, _require, GetAtom("main"));
        }

        public bool LoadModuleCache(string module_id, out JSValue value)
        {
            var prop = GetAtom(module_id);
            var mod = JSApi.JS_GetProperty(_ctx, _moduleCache, prop);
            if (mod.IsObject())
            {
                value = mod;
                return true;
            }
            value = JSApi.JS_UNDEFINED;
            JSApi.JS_FreeValue(_ctx, mod);
            return false;
        }

        public string[] GetModuleCacheList()
        {
            var keys = new string[_loadedModuleHash.Count];
            _loadedModuleHash.Keys.CopyTo(keys, 0);
            return keys;
        }

        /// <summary>
        /// 清除模块缓存
        /// </summary>
        public void UnloadModuleCache(string module_id)
        {
            var prop = GetAtom(module_id);
            JSApi.JS_SetProperty(_ctx, _moduleCache, prop, JSApi.JS_UNDEFINED);
            _loadedModuleHash.Remove(module_id);
        }

        /// <summary>
        /// 添加全局函数
        /// </summary>
        public void AddFunction(string name, JSCFunction func, int length)
        {
            AddFunction(_globalObject, name, func, length);
        }

        public void AddFunction(JSValue thisObject, string name, JSCFunction func, int length)
        {
            var nameAtom = GetAtom(name);
            var cfun = JSApi.JSB_NewCFunction(_ctx, func, nameAtom, length, JSCFunctionEnum.JS_CFUNC_generic, 0);
            JSApi.JS_DefinePropertyValue(_ctx, thisObject, nameAtom, cfun, JSPropFlags.JS_PROP_C_W_E);
        }

        public static ClassDecl Bind(TypeRegister register)
        {
            var ns_jsb = register.CreateClass("JSBObject");

            ns_jsb.AddFunction("DoFile", _DoFile, 1);
            ns_jsb.AddFunction("AddSearchPath", _AddSearchPath, 1);
            ns_jsb.AddFunction("Yield", yield_func, 1);
            ns_jsb.AddFunction("ToArray", to_js_array, 1);
            ns_jsb.AddFunction("ToArrayBuffer", to_js_array_buffer, 1);
            ns_jsb.AddFunction("ToBytes", to_cs_bytes, 1);
            ns_jsb.AddFunction("ToFunction", to_js_function, 1);
            ns_jsb.AddFunction("ToDelegate", to_cs_delegate, 1);
            ns_jsb.AddFunction("Import", js_import_type, 2);
            ns_jsb.AddFunction("GC", _gc, 0);
            ns_jsb.AddFunction("SetDisposable", _set_disposable, 2);
            ns_jsb.AddFunction("AddCacheString", _add_cache_string, 1);
            ns_jsb.AddFunction("RemoveCacheString", _remove_cache_string, 1);
            ns_jsb.AddFunction("Sleep", _sleep, 1);
            ns_jsb.AddFunction("AddModule", _add_module, 2);
            ns_jsb.AddFunction("Now", _now, 0);
            {
                var ns_hotfix = register.CreateClass("JSBHotfix");
                ns_hotfix.AddFunction("replace_single", hotfix_replace_single, 2);
                ns_hotfix.AddFunction("before_single", hotfix_before_single, 2);
                // ns_hotfix.AddFunction("replace", hotfix_replace, 2);
                // ns_hotfix.AddFunction("before", hotfix_before);
                // ns_hotfix.AddFunction("after", hotfix_after);

                ns_jsb.AddValue("hotfix", ns_hotfix.GetConstructor());
            }
            return ns_jsb;
        }

        public unsafe void EvalMain(byte[] source, string fileName)
        {
            EvalMain(source, fileName, fileName, typeof(void));
        }

        public unsafe T EvalMain<T>(byte[] source, string fileName)
        {
            return (T)EvalMain(source, fileName, fileName, typeof(T));
        }

        public unsafe void EvalMain(byte[] source, string fileName, string fullPath)
        {
            EvalMain(source, fileName, fullPath, typeof(void));
        }

        public unsafe T EvalMain<T>(byte[] source, string fileName, string fullPath)
        {
            return (T)EvalMain(source, fileName, fullPath, typeof(T));
        }

        public unsafe object EvalMain(byte[] source, string module_id, string fullPath, Type expectedReturnType)
        {
            var tagValue = ScriptRuntime.TryReadByteCodeTagValue(source);
            if (tagValue == ScriptRuntime.BYTECODE_ES6_MODULE_TAG)
            {
                throw new Exception("es6 module bytecode as main is unsupported");
            }

            object csValue = null;
            var dirname = PathUtils.GetDirectoryName(module_id);
            var filename_bytes = TextUtils.GetNullTerminatedBytes(module_id);
            var module_id_atom = GetAtom(module_id);
            var dirname_atom = GetAtom(dirname);
            var full_path_atom = GetAtom(fullPath);

            var exports_obj = JSApi.JS_NewObject(_ctx);
            var require_obj = JSApi.JS_DupValue(_ctx, _require);
            var module_obj = _new_commonjs_module(module_id, fullPath, exports_obj, false);
            var module_id_obj = JSApi.JS_AtomToString(_ctx, module_id_atom);
            var filename_obj = JSApi.JS_AtomToString(_ctx, full_path_atom);
            var dirname_obj = JSApi.JS_AtomToString(_ctx, dirname_atom);
            var require_argv = new JSValue[5] { exports_obj, require_obj, module_obj, filename_obj, dirname_obj };
            JSApi.JS_SetProperty(_ctx, require_obj, GetAtom("moduleId"), JSApi.JS_DupValue(_ctx, module_id_obj));
            JSApi.JS_SetProperty(_ctx, require_obj, GetAtom("main"), JSApi.JS_DupValue(_ctx, module_obj));

            if (tagValue == ScriptRuntime.BYTECODE_COMMONJS_MODULE_TAG)
            {
                // bytecode
                fixed (byte* intput_ptr = source)
                {
                    var bytecodeFunc = JSApi.JS_ReadObject(_ctx, intput_ptr + sizeof(uint), source.Length - sizeof(uint), JSApi.JS_READ_OBJ_BYTECODE);

                    if (bytecodeFunc.tag == JSApi.JS_TAG_FUNCTION_BYTECODE)
                    {
                        var func_val = JSApi.JS_EvalFunction(_ctx, bytecodeFunc); // it's CallFree (bytecodeFunc)
                        if (JSApi.JS_IsFunction(_ctx, func_val) != 1)
                        {
                            JSApi.JS_FreeValue(_ctx, func_val);
                            FreeValues(require_argv);
                            throw new Exception("failed to eval bytecode module");
                        }

                        var rval = JSApi.JS_Call(_ctx, func_val, JSApi.JS_UNDEFINED);
                        JSApi.JS_FreeValue(_ctx, func_val);
                        if (rval.IsException())
                        {
                            _ctx.print_exception();
                            JSApi.JS_FreeValue(_ctx, rval);
                            FreeValues(require_argv);
                            throw new Exception("failed to eval bytecode module");
                        }

                        // success
                        Values.js_get_var(_ctx, rval, expectedReturnType, out csValue);
                        JSApi.JS_FreeValue(_ctx, rval);
                        JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("loaded"), JSApi.JS_NewBool(_ctx, true));
                        FreeValues(require_argv);
                        return csValue;
                    }

                    JSApi.JS_FreeValue(_ctx, bytecodeFunc);
                    FreeValues(require_argv);
                    throw new Exception("failed to eval bytecode module");
                }
            }
            else
            {
                // source
                var input_bytes = TextUtils.GetShebangNullTerminatedCommonJSBytes(source);
                fixed (byte* input_ptr = input_bytes)
                fixed (byte* resolved_id_ptr = filename_bytes)
                {
                    var input_len = (size_t)(input_bytes.Length - 1);
                    var func_val = JSApi.JS_Eval(_ctx, input_ptr, input_len, resolved_id_ptr, JSEvalFlags.JS_EVAL_TYPE_GLOBAL | JSEvalFlags.JS_EVAL_FLAG_STRICT);
                    if (func_val.IsException())
                    {
                        FreeValues(require_argv);
                        _ctx.print_exception();
                        throw new Exception("failed to eval module");
                    }

                    if (JSApi.JS_IsFunction(_ctx, func_val) == 1)
                    {
                        var rval = JSApi.JS_Call(_ctx, func_val, JSApi.JS_UNDEFINED, require_argv.Length, require_argv);
                        if (rval.IsException())
                        {
                            JSApi.JS_FreeValue(_ctx, func_val);
                            FreeValues(require_argv);
                            _ctx.print_exception();
                            throw new Exception("failed to eval module");
                        }
                        Values.js_get_var(_ctx, rval, expectedReturnType, out csValue);
                        JSApi.JS_FreeValue(_ctx, rval);
                    }

                    JSApi.JS_FreeValue(_ctx, func_val);
                    JSApi.JS_SetProperty(_ctx, module_obj, GetAtom("loaded"), JSApi.JS_NewBool(_ctx, true));
                    FreeValues(require_argv);
                    return csValue;
                }
            }
        }

        public void EvalSource(string source, string fileName)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(source);
            EvalSource(bytes, fileName, typeof(void));
        }

        public T EvalSource<T>(string source, string fileName)
        {
            var bytes = System.Text.Encoding.UTF8.GetBytes(source);
            return (T)EvalSource(bytes, fileName, typeof(T));
        }

        public void EvalSource(byte[] source, string fileName)
        {
            EvalSource(source, fileName, typeof(void));
        }

        public T EvalSource<T>(byte[] source, string fileName)
        {
            return (T)EvalSource(source, fileName, typeof(T));
        }

        public object EvalSource(byte[] source, string fileName, Type returnType)
        {
            var jsValue = ScriptRuntime.EvalSource(_ctx, source, fileName, false);
            if (JSApi.JS_IsException(jsValue))
            {
                var ex = _ctx.GetExceptionString();
                throw new JSException(ex, fileName);
            }
            object retObject;
            Values.js_get_var(_ctx, jsValue, returnType, out retObject);
            JSApi.JS_FreeValue(_ctx, jsValue);
            return retObject;
        }

        public void RegisterBuiltins()
        {
            var ctx = (JSContext)this;
            var global_object = this.GetGlobalObject();
            {
                _require = JSApi.JSB_NewCFunction(ctx, ScriptRuntime.module_require, GetAtom("require"), 1, JSCFunctionEnum.JS_CFUNC_generic, 0);
                JSApi.JS_SetProperty(ctx, _require, GetAtom("moduleId"), JSApi.JS_NewString(ctx, ""));
                JSApi.JS_SetProperty(ctx, _require, GetAtom("cache"), JSApi.JS_DupValue(ctx, _moduleCache));
                JSApi.JS_SetProperty(ctx, global_object, GetAtom("require"), JSApi.JS_DupValue(ctx, _require));

                JSApi.JS_SetPropertyStr(ctx, global_object, "print", JSApi.JS_NewCFunctionMagic(ctx, _print, "print", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 0));
                var console = JSApi.JS_NewObject(ctx);
                {
                    JSApi.JS_SetPropertyStr(ctx, console, "log", JSApi.JS_NewCFunctionMagic(ctx, _print, "log", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 0));
                    JSApi.JS_SetPropertyStr(ctx, console, "info", JSApi.JS_NewCFunctionMagic(ctx, _print, "info", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 0));
                    JSApi.JS_SetPropertyStr(ctx, console, "debug", JSApi.JS_NewCFunctionMagic(ctx, _print, "debug", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 0));
                    JSApi.JS_SetPropertyStr(ctx, console, "warn", JSApi.JS_NewCFunctionMagic(ctx, _print, "warn", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 1));
                    JSApi.JS_SetPropertyStr(ctx, console, "error", JSApi.JS_NewCFunctionMagic(ctx, _print, "error", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 2));
                    JSApi.JS_SetPropertyStr(ctx, console, "assert", JSApi.JS_NewCFunctionMagic(ctx, _print, "assert", 1, JSCFunctionEnum.JS_CFUNC_generic_magic, 3));
                    JSApi.JS_SetPropertyStr(ctx, console, "trace", JSApi.JS_NewCFunctionMagic(ctx, _print, "trace", 0, JSCFunctionEnum.JS_CFUNC_generic_magic, -1));
                }
                JSApi.JS_SetPropertyStr(ctx, global_object, "console", console);
            }
            JSApi.JS_FreeValue(ctx, global_object);
        }

        private string js_source_position(JSContext ctx, string funcName, string fileName, int lineNumber)
        {
            return $"{funcName} ({fileName}:{lineNumber})";
        }

        public void AppendStacktrace(StringBuilder sb)
        {
            var ctx = _ctx;
            var globalObject = JSApi.JS_GetGlobalObject(ctx);
            var errorConstructor = JSApi.JS_GetProperty(ctx, globalObject, JSApi.JS_ATOM_Error);
            var errorObject = JSApi.JS_CallConstructor(ctx, errorConstructor);
            var stackValue = JSApi.JS_GetProperty(ctx, errorObject, JSApi.JS_ATOM_stack);
            var stack = JSApi.GetString(ctx, stackValue);

            if (!string.IsNullOrEmpty(stack))
            {
                var errlines = stack.Split('\n');
                if (_stRegex == null)
                {
                    _stRegex = new Regex(@"^\s+at\s(.+)\s\((.+\.js):(\d+)\)(.*)$", RegexOptions.Compiled);
                }
                for (var i = 0; i < errlines.Length; i++)
                {
                    var line = errlines[i];
                    var matches = _stRegex.Matches(line);
                    if (matches.Count == 1)
                    {
                        var match = matches[0];
                        if (match.Groups.Count >= 4)
                        {
                            var funcName = match.Groups[1].Value;
                            var fileName = match.Groups[2].Value;
                            var lineNumber = 0;
                            int.TryParse(match.Groups[3].Value, out lineNumber);
                            var extra = match.Groups.Count >= 5 ? match.Groups[4].Value : "";
                            var sroucePosition = (_runtime.OnSourceMap ?? js_source_position)(ctx, funcName, fileName, lineNumber);
                            sb.AppendLine($"    at {sroucePosition}{extra}");
                            continue;
                        }
                    }
                    sb.AppendLine(line);
                }
            }

            JSApi.JS_FreeValue(ctx, stackValue);
            JSApi.JS_FreeValue(ctx, errorObject);
            JSApi.JS_FreeValue(ctx, errorConstructor);
            JSApi.JS_FreeValue(ctx, globalObject);
        }

        public static implicit operator JSContext(ScriptContext sc)
        {
            return sc._ctx;
        }
    }
}