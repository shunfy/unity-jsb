using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Reflection;

namespace QuickJS.Unity
{
    using UnityEngine;
    using UnityEditor;

    public class PlainMethodCodeGen : IDisposable
    {
        protected CodeGenerator cg;

        public PlainMethodCodeGen(CodeGenerator cg, string sig)
        {
            this.cg = cg;
            this.cg.cs.AppendLine(sig);
            this.cg.cs.AppendLine("{");
            this.cg.cs.AddTabLevel();
        }

        public void Dispose()
        {
            this.cg.cs.DecTabLevel();
            this.cg.cs.AppendLine("}");
        }

        public void AddStatement(string fmt, params object[] args)
        {
            this.cg.cs.AppendLine(fmt, args);
        }

        public void AddModuleEntry(string moduleName, string runtimeVarName, string moduleVarName, TypeBindingInfo typeBindingInfo)
        {
            var csType = this.cg.bindingManager.GetCSTypeFullName(typeBindingInfo.type);
            var csNamespace = this.cg.bindingManager.prefs.ns;
            var csBindingName = typeBindingInfo.csBindingName;
            var elements = typeBindingInfo.tsTypeNaming.jsNamespace.Split('.');
            var jsNameNormalized = CodeGenUtils.Normalize(typeBindingInfo.tsTypeNaming.jsName);
            var jsNamespace = CodeGenUtils.Concat(", ", CodeGenUtils.ConcatAsLiteral(", ", elements), $"\"{jsNameNormalized}\"");

            AddStatement($"{runtimeVarName}.AddTypeReference({moduleVarName}, typeof({csType}), {csNamespace}.{csBindingName}.Bind, {jsNamespace});");
            this.cg.bindingManager.callback.AddTypeReference(moduleName, typeBindingInfo, elements, jsNameNormalized);
        }
    }
}
