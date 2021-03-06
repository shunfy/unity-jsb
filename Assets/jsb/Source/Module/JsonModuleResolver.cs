using System;
using System.Collections.Generic;

namespace QuickJS.Module
{
    using Utils;
    using Native;

    public class JsonModuleResolver : PathBasedModuleResolver
    {
        public JsonModuleResolver()
        {
        }

        protected override bool OnValidating(string module_id)
        {
            // 必须指明后缀
            return module_id.EndsWith(".json") || module_id.EndsWith(".jsonc");
        }

        protected override bool OnResolvingFile(IFileSystem fileSystem, IPathResolver pathResolver, string fileName, out string searchPath, out string resolvedFileName)
        {
            if (pathResolver.ResolvePath(fileSystem, fileName, out searchPath, out resolvedFileName))
            {
                return true;
            }

            return false;
        }

        public override unsafe JSValue LoadModule(ScriptContext context, string resolved_id)
        {
            var fileSystem = context.GetRuntime().GetFileSystem();
            var resolved_id_bytes = Utils.TextUtils.GetNullTerminatedBytes(resolved_id);
            var dirname = PathUtils.GetDirectoryName(resolved_id);
            var source = fileSystem.ReadAllBytes(resolved_id);
            var ctx = (JSContext)context;

            if (source == null)
            {
                return JSApi.JS_ThrowInternalError(ctx, "require module load failed");
            }

            var input_bytes = TextUtils.GetNullTerminatedBytes(source);
            var input_bom = TextUtils.GetBomSize(source);

            fixed (byte* input_ptr = &input_bytes[input_bom])
            fixed (byte* filename_ptr = resolved_id_bytes)
            {
                var rval = JSApi.JS_ParseJSON(ctx, input_ptr, input_bytes.Length - 1 - input_bom, filename_ptr);
                if (rval.IsException())
                {
                    return rval;
                }

                var module_obj = context._new_commonjs_module(resolved_id, rval, true);
                JSApi.JS_FreeValue(ctx, module_obj);

                return rval;
            }
        }
    }
}
