using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

namespace RoslynCompilerFix
{
    internal class CSharpCompilerFix
    {
        public static void Process(AssemblyDefinition assemblyDef)
        {
            var hitSet = new HashSet<string>();

            foreach (var module in assemblyDef.Modules)
            {
                var generatedNames = module.GetType("Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedNames");
                if (generatedNames != null)
                {
                    foreach (var method in generatedNames.Methods)
                    {
                        if (method.Name == "MakeHoistedLocalFieldName")
                        {
                            hitSet.Add(method.Name);
                            Fix_GeneratedNames_MakeHoistedLocalFieldName(method);
                        }
                        else if (method.Name == "MakeMethodScopedSynthesizedName")
                        {
                            hitSet.Add(method.Name);
                            Fix_GeneratedNames_MakeMethodScopedSynthesizedName(method);
                        }
                        else if (method.Name == "ThisProxyFieldName")
                        {
                            hitSet.Add(method.Name);
                            Fix_GeneratedNames_ThisProxyFieldName(method);
                        }
                        else if (method.Name == "MakeLambdaMethodName")
                        {
                            hitSet.Add(method.Name);
                            Fix_GeneratedNames_MakeLambdaMethodName(method);
                        }
                    }
                }

                var lambdaFrame = module.GetType("Microsoft.CodeAnalysis.CSharp.LambdaFrame");
                if (lambdaFrame != null)
                {
                    foreach (var method in lambdaFrame.Methods)
                    {
                        if (method.Name == ".ctor")
                        {
                            hitSet.Add(lambdaFrame.FullName);
                            Fix_LambdaFrame_Constructor(method);
                        }
                    }
                }

                var sourceAssemblySymbol = module.GetType("Microsoft.CodeAnalysis.CSharp.Symbols.SourceAssemblySymbol");
                if (sourceAssemblySymbol != null)
                {
                    foreach (var method in sourceAssemblySymbol.Methods)
                    {
                        if (method.Name == "GetUnusedFieldWarnings")
                        {
                            hitSet.Add(method.Name);
                            Fix_SourceAssemblySymbol_GetUnusedFieldWarnings(method);
                        }
                    }
                }
            }

            foreach (var hit in hitSet.ToList().OrderBy(x => x))
            {
                Console.WriteLine(hit);
            }
        }

        private static void Fix_GeneratedNames_MakeHoistedLocalFieldName(MethodDefinition method)
        {
            // Goal:
            //   Rename <v>4__1 to <v>__1 for local variables in iterator
            //
            // How for Roslyn:
            //   Remove following code from GeneratedNames.MakeHoistedLocalFieldName
            //     builder.Append((char)GeneratedNameKind.HoistedLocalField);
            //
            // How for IL:
            //   Remove following code from body of method
            //     IL_003c: ldloc.0
            //     IL_003d: ldc.i4.s 53
            //     IL_003f: callvirt System.Text.StringBuilder System.Text.StringBuilder::Append(System.Char)
            //     IL_0044: pop

            var il = method.Body.GetILProcessor();
            for (var i = 0; i < il.Body.Instructions.Count; i++)
            {
                var inst = il.Body.Instructions[i];
                if (inst.OpCode.Code == Code.Ldc_I4_S && (sbyte)inst.Operand == 53)
                {
                    for (int j = 0; j < 4; j++)
                        il.Remove(il.Body.Instructions[i + 2 - j]);
                    break;
                }
            }
        }

        private static void Fix_GeneratedNames_MakeMethodScopedSynthesizedName(MethodDefinition method)
        {
            // Goal:
            //   Rename <TestCoroutine>d__1 to <TestCoroutine>c__Iterator0 for iterator class name
            //
            // How for Roslyn:
            //   Append code following code at GeneratedNames.MakeMethodScopedSynthesizedName
            //     builder.Append('>');
            //     builder.Append((char)kind);
            //     >> if (kind == GeneratedNameKind.StateMachineType) {
            //     >>   builder.Append("__Iterator")
            //     >> }
            //
            // How for IL:
            //   IL_0033: ldloc.0
            //   IL_0034: ldc.i4.s 62
            //   IL_0036: callvirt instance class [System.Runtime]System.Text.StringBuilder [System.Runtime]System.Text.StringBuilder::Append(char)
            //   IL_003b: pop
            //   IL_003c: ldloc.0
            //   IL_003d: ldarg.0
            //   IL_003e: conv.u2
            //   IL_003f: callvirt instance class [System.Runtime]System.Text.StringBuilder [System.Runtime]System.Text.StringBuilder::Append(char)
            //   IL_0044: pop
            //   >> IL_?: ldarg.0
            //   >> IL_?: ldc.i4.s IL_BASE
            //   >> IL_?: bne.un.s OUT
            //   >> IL_?: ldloc.0
            //   >> IL_?: ldstr "__Iterator"
            //   >> IL_?: callvirt instance class [mscorlib]System.Text.StringBuilder [mscorlib]System.Text.StringBuilder::Append(string)
            //   >> IL_?: pop
            //   IL_BASE:

            var il = method.Body.GetILProcessor();
            for (var i = 0; i < il.Body.Instructions.Count; i++)
            {
                var inst = il.Body.Instructions[i];
                if (inst.OpCode.Code == Code.Ldc_I4_S && (sbyte)inst.Operand == 62)
                {
                    var baseInst = il.Body.Instructions[i + 8];

                    var appendStringMethod = typeof(StringBuilder).GetMethod("Append", new Type[] { typeof(string) });
                    var appendString = method.Module.Import(appendStringMethod);

                    il.InsertBefore(baseInst, il.Create(OpCodes.Ldarg_0));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Ldc_I4_S, (sbyte)100));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Bne_Un_S, baseInst));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Ldloc_0));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Ldstr, "__Iterator"));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Callvirt, appendString));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Pop));
                    break;
                }
            }
        }

        private static void Fix_GeneratedNames_ThisProxyFieldName(MethodDefinition method)
        {
            // Goal:
            //   Rename <>4__this to <>f__this for this in iterator class
            //
            // How for Roslyn:
            //   Modify return statement of GeneratedNames.ThisProxyFieldNam from
            //     return "<>4__this";
            //   to
            //     return "<>f__this";
            //
            // How for IL:
            //   Change literal "<>4__this" to "<>f__this" at ThisProxyFieldName()
            //     IL_0000: ldstr "<>4__this"
            //     IL_0005: ret

            var il = method.Body.GetILProcessor();
            for (var i = 0; i < il.Body.Instructions.Count; i++)
            {
                var inst = il.Body.Instructions[i];
                if (inst.OpCode.Code == Code.Ldstr && (string)inst.Operand == "<>4__this")
                {
                    il.Replace(inst, il.Create(OpCodes.Ldstr, "<>f__this"));
                    break;
                }
            }
        }

        private static void Fix_LambdaFrame_Constructor(MethodDefinition method)
        {
            // Goal:
            //   Rename <>c__DisplayClass0_0 to <*MethodName*>c__AnonStorey* for lambda class name
            //
            // How for Roslyn:
            //   Modify constructor of LambdaFrame from
            //      LambdaFrame(MethodSymbol topLevelMethod, ...)
            //       : base(MakeName(...
            //   to
            //      LambdaFrame(MethodSymbol topLevelMethod, ...)
            //       : base("<" + topLevelMethod.Name + ">c__AnonStorey" + MakeName(...
            //
            // How for IL:
            //   IL_0000: ldarg.0
            //   >> IL_?: ldstr "<"
            //   >> IL_?: ldarg.1
            //   >> IL_?: callvirt instance string Microsoft.CodeAnalysis.CSharp.Symbol::get_Name()
            //   >> IL_?: ldstr ">c__AnonStorey"
            //   IL_0001: ldarg.s scopeSyntaxOpt
            //   IL_0003: ldarg.s methodId
            //   IL_0005: ldarg.s closureId
            //   IL_0007: call string Microsoft.CodeAnalysis.CSharp.LambdaFrame::MakeName(...)
            //   >> IL_?: call string [mscorlib]System.String::Concat(string, string, string, string)
            //   IL_000c: ldarg.2
            //   IL_000d: call instance void Microsoft.CodeAnalysis.CSharp.Symbols.SynthesizedContainer::.ctor(string, class Microsoft.CodeAnalysis.CSharp.Symbols.MethodSymbol)

            var il = method.Body.GetILProcessor();
            var baseInst = il.Body.Instructions[1];
            var baseInst2 = il.Body.Instructions[4];
            if (baseInst.OpCode.Code != Code.Ldarg_S)
                throw new InvalidOperationException();
            if (baseInst2.OpCode.Code != Code.Call)
                throw new InvalidOperationException();

            var symbolType = method.Module.GetType("Microsoft.CodeAnalysis.CSharp.Symbol");
            var symbolGetNameMethod = symbolType.Methods.FirstOrDefault(m => m.Name == "get_Name");
            var symbolGetName = method.Module.Import(symbolGetNameMethod);

            il.InsertBefore(baseInst, il.Create(OpCodes.Ldstr, "<"));
            il.InsertBefore(baseInst, il.Create(OpCodes.Ldarg_1));
            il.InsertBefore(baseInst, il.Create(OpCodes.Callvirt, symbolGetName));
            il.InsertBefore(baseInst, il.Create(OpCodes.Ldstr, ">c__AnonStorey"));

            var concat4Method = typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string), typeof(string), typeof(string) });
            var concat4 = method.Module.Import(concat4Method);

            il.InsertAfter(baseInst2, il.Create(OpCodes.Call, concat4));
        }

        private static void Fix_GeneratedNames_MakeLambdaMethodName(MethodDefinition method)
        {
            // Goal:
            //   Rename <Start>b__0 to <>m__<Start>b__0 for method name in lambda class
            //
            // How for Roslyn:
            //   Modify return statement of GeneratedNames.MakeLambdaMethodName from
            //     return MakeMethodScopedSynthesizedName(GeneratedNameKind.LambdaMethod, ...
            //   to
            //     return "<>m__" + MakeMethodScopedSynthesizedName(GeneratedNameKind.LambdaMethod, ...
            //
            // How for IL:
            //   >> IL_?: ldstr "<>m__"
            //   IL_0000: ldc.i4.s 57
            //   IL_0002: ldarg.0
            //   IL_0003: ldarg.1
            //   IL_0004: ldnull
            //   IL_0005: ldnull
            //   IL_0006: ldarg.2
            //   IL_0007: ldarg.3
            //   IL_0008: ldc.i4.0
            //   IL_0009: call string Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedNames::MakeMethodScopedSynthesizedName(valuetype Microsoft.CodeAnalysis.CSharp.Symbols.GeneratedNameKind, int32, int32, string, string, int32, int32, bool)
            //   >> IL_?: call string [mscorlib]System.String::Concat(string, string)
            //   IL_000e: ret

            var il = method.Body.GetILProcessor();
            var baseInst = il.Body.Instructions[0];
            var baseInst2 = il.Body.Instructions[il.Body.Instructions.Count - 1];

            var concatMethod = typeof(string).GetMethod("Concat", new Type[] { typeof(string), typeof(string) });
            var concat = method.Module.Import(concatMethod);

            il.InsertBefore(baseInst, il.Create(OpCodes.Ldstr, "<>m__"));
            il.InsertBefore(baseInst2, il.Create(OpCodes.Call, concat));
        }

        private static void Fix_SourceAssemblySymbol_GetUnusedFieldWarnings(MethodDefinition method)
        {
            // Goal:
            //   Add "Skip CS0649 warning for a field has attributes" to GetUnusedFieldWarnings in SourceAssemblySymbol
            //
            // How for Roslyn:
            //   Add following statements after "if (!field.CanBeReferencedByName) { continue; }"
            //     if (!field.GetAttributes().IsEmpty) { continue; }
            //
            // How for IL:
            //   IL_0078: ldloc.s 4
            //   IL_007a: callvirt instance bool Microsoft.CodeAnalysis.CSharp.Symbol::get_CanBeReferencedByName()
            //   IL_007f: brfalse IL_018b
            //   >> IL_?: ldloc.s 4
            //   >> IL_?: callvirt instance valuetype ImmutableArray<CSharpAttributeData> Microsoft.CodeAnalysis.CSharp.Symbol::GetAttributes()
            //   >> IL_?: stloc 13
            //   >> IL_?: ldloca.s 13
            //   >> IL_?: call instance bool valuetype ImmutableArray<CSharpAttributeData>::get_IsEmpty()
            //   >> IL_?: brfalse IL_018b

            var il = method.Body.GetILProcessor();
            var attributeDataType = method.Module.GetType("Microsoft.CodeAnalysis.CSharp.Symbols.CSharpAttributeData");

            // find Methods
            var symbolType = method.Module.GetType("Microsoft.CodeAnalysis.CSharp.Symbol");
            var symbolGetAttributesMethod = symbolType.GetMethod("GetAttributes");
            var symbolGetAttributes = method.Module.Import(symbolGetAttributesMethod);
            var symbolReturnType = (GenericInstanceType)symbolGetAttributesMethod.ReturnType;
            var symbolIsEmptyMethod = symbolReturnType.Resolve().GetMethod("get_IsEmpty");
            var symbolIsEmptyMethodRef = method.Module.Import(symbolIsEmptyMethod);
            var symbolIsEmpty = symbolIsEmptyMethodRef.MakeGeneric(symbolReturnType.GenericArguments.ToArray());

            // Add local var of ImmutableArray<CSharpAttributeData>
            var attributeArrayType = new GenericInstanceType(symbolReturnType.ElementType);
            var attributeArrayLocalIndex = il.Body.Variables.Count;
            attributeArrayType.GenericArguments.Add(attributeDataType);
            il.Body.Variables.Add(new VariableDefinition(attributeArrayType));

            for (var i = 0; i < il.Body.Instructions.Count; i++)
            {
                var inst = il.Body.Instructions[i];
                if (inst.OpCode.Code == Code.Callvirt && ((MethodReference)inst.Operand).Name == "get_CanBeReferencedByName")
                {
                    if (inst.Previous.OpCode.Code != Code.Ldloc_S)
                        throw new InvalidOperationException();
                    if (inst.Next.OpCode.Code != Code.Brfalse)
                        throw new InvalidOperationException();

                    var baseInst = inst.Next.Next;

                    il.InsertBefore(baseInst, il.Create(OpCodes.Ldloc_S, (VariableDefinition)inst.Previous.Operand));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Callvirt, symbolGetAttributes));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Stloc, il.Body.Variables[attributeArrayLocalIndex]));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Ldloca_S, il.Body.Variables[attributeArrayLocalIndex]));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Call, symbolIsEmpty));
                    il.InsertBefore(baseInst, il.Create(OpCodes.Brfalse, (Instruction)inst.Next.Operand));
                    break;
                }
            }
        }
    }
}
