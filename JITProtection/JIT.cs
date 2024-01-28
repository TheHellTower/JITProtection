using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;

namespace JITProtection
{
    internal class JIT
    {
        static Assembly Assembly = null;
        static ModuleDefMD LMFAO = null;
        static byte[] AssemblyByte = null;
        static List<MethodDef> MethodsJITed = new List<MethodDef>();

        internal static byte[] Execute(ModuleDefMD Module, bool x64, List<string> methods)
        {
            Assembly = Assembly.LoadFile(Module.Location);
            LMFAO = Module;
            AssemblyByte = null;

            #region "Inject Runtime"
            string tellMeMore = x64 ? "64" : "86";
            byte[] array = File.ReadAllBytes($"{Environment.CurrentDirectory}\\Runtime_x{tellMeMore}.dll");
            EmbeddedResource DLLResource = new EmbeddedResource("DLL", array, ManifestResourceAttributes.Public);
            Module.Resources.Add(DLLResource);

            Type RuntimeType = typeof(Runtime);
            ModuleDefMD RuntimeModule = ModuleDefMD.Load(RuntimeType.Module);
            TypeDef typeDef = RuntimeModule.ResolveTypeDef(MDToken.ToRID(RuntimeType.MetadataToken));
            IEnumerable<IDnlibDef> InjectedMembers = InjectHelper.Inject(typeDef, Module.GlobalType, Module);
            MethodDef Initialize = (MethodDef)InjectedMembers.Where(M => M.Name == "Initialize").First();
            Module.GlobalType.FindOrCreateStaticConstructor().Body.Instructions.Insert(0, Instruction.Create(OpCodes.Call, Initialize));
            UpdateModule(Module);
            #endregion

            #region "Prepare And Add Methods"
            foreach (TypeDef Type in Module.GetTypes())
                foreach (MethodDef methodDef in Type.Methods)
                {
                    if (!methods.Contains(methodDef.FullName)) continue;
                    MethodBase methodBase = FindReflectionMethod(methodDef);
                    if (methodBase != null)
                    {
                        IList<Instruction> instructions = methodDef.Body.Instructions;
                        for (int i = 0; i < 5; i++)
                            instructions.Insert(0, OpCodes.Nop.ToInstruction());

                        MethodsJITed.Add(methodDef);
                    }
                }
            #endregion

            UpdateModule(Module);

            #region "Protect Methods"
            foreach (MethodDef methodDef in MethodsJITed)
            {
                MethodBase methodBase = FindReflectionMethod(methodDef);
                if (methodBase == null) continue;

                System.Reflection.MethodBody methodBody = methodBase.GetMethodBody();
                if (methodBody != null)
                {
                    byte[] ilasByteArray = methodBody.GetILAsByteArray();
                    int num = ilasByteArray.Length;
                    int num2 = SearchArray(AssemblyByte, ilasByteArray);
                    if (num2 != -1)
                    {
                        EncryptDecrypt(ilasByteArray);
                        for (int i = 0; i < num; i++)
                        {
                            AssemblyByte[num2] = ilasByteArray[i];
                            num2++;
                        }
                    }
                }
            }
            #endregion

            return AssemblyByte;
        }

        public static void EncryptDecrypt(byte[] data)
        {
            //Hardcoded because gay
            byte[] array = Convert.FromBase64String("W2h0dHBzOi8vZ2l0aHViLmNvbS9UaGVIZWxsVG93ZXIgfCBodHRwczovL2NyYWNrZWQuaW8vVGhlSGVsbFRvd2VyXSB4NjQgc3VwcG9ydCArIE1ldGhvZCBTZWxlY3Rpb24=");
            if (data == null)
                throw new ArgumentNullException("data");

            for (int i = 0; i < data.Length; i++)
                data[i] ^= array[i % array.Length];
        }

        static int SearchArray(byte[] src, byte[] pattern)
        {
            int num = src.Length - pattern.Length + 1;
            for (int i = 0; i < num; i++)
                if (src[i] == pattern[0])
                {
                    int num2 = pattern.Length - 1;
                    while (num2 >= 1 && src[i + num2] == pattern[num2])
                        num2--;

                    if (num2 == 0)
                        return i;
                }
            return -1;
        }

        static MethodBase FindReflectionMethod(MethodDef method)
        {
            try
            {
                if (method.IsConstructor)
                {
                    foreach (Type type in Assembly.DefinedTypes)
                        foreach (ConstructorInfo constructorInfo in type.GetConstructors(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic))
                        {
                            ParameterInfo[] parameters = constructorInfo.GetParameters();
                            if (parameters.Length == method.Parameters.Count)
                            {
                                bool flag2 = true;
                                for (int j = 0; j < parameters.Length; j++)
                                    if (parameters[j].Name != method.Parameters[j].Name)
                                        flag2 = false;

                                if (flag2)
                                    return constructorInfo;
                            }
                        }
                }
                else
                {
                    foreach (TypeInfo typeInfo in Assembly.DefinedTypes)
                        foreach (MethodInfo methodInfo in typeInfo.DeclaredMethods)
                            if (methodInfo.Name == method.Name)
                                return methodInfo;
                }
            } catch (Exception ex)
            {
                //Need to see what happened
            }
            return null;
        }

        private static void UpdateModule(ModuleDefMD M)
        {
            ModuleWriterOptions moduleWriterOptions = new ModuleWriterOptions(M);
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            moduleWriterOptions.MetadataLogger = DummyLogger.NoThrowInstance;
            MemoryStream memoryStream = new MemoryStream();
            M.Write(memoryStream, moduleWriterOptions);
            AssemblyByte = memoryStream.ToArray();
            Assembly = Assembly.Load(AssemblyByte);
            LMFAO = ModuleDefMD.Load(memoryStream.ToArray());
        }
    }
}