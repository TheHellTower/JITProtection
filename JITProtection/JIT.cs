using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace JITProtection
{
    internal class JIT
    {
        static ModuleDefMD LMFAO = null;
        static byte[] AssemblyByte = null;
        static List<MethodDef> MethodsJITed = new List<MethodDef>();

        public static byte[] PrependBytes(byte[] originalArray, byte[] bytesToPrepend)
        {
            byte[] newArray = new byte[originalArray.Length + bytesToPrepend.Length];

            // Copy bytesToPrepend to the start of newArray
            Array.Copy(bytesToPrepend, 0, newArray, 0, bytesToPrepend.Length);

            // Copy originalArray to newArray after bytesToPrepend
            Array.Copy(originalArray, 0, newArray, bytesToPrepend.Length, originalArray.Length);

            return newArray;
        }
        internal static byte[] Execute(ModuleDefMD Module, bool x64, List<string> methods)
        {
            LMFAO = Module;
            AssemblyByte = null;

            #region "Inject Runtime"
            foreach (string path in new string[] { $"{Environment.CurrentDirectory}\\Runtime_x86.dll", $"{Environment.CurrentDirectory}\\Runtime_x64.dll" })
            {
                byte[] array = File.ReadAllBytes(path);
                EmbeddedResource DLLResource = new EmbeddedResource(Path.GetFileName(path), array, ManifestResourceAttributes.Public);
                Module.Resources.Add(DLLResource);
            }

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
                    IList<Instruction> instructions = methodDef.Body.Instructions;
                    for (int i = 0; i < 5; i++)
                        instructions.Insert(0, OpCodes.Nop.ToInstruction());

                    MethodsJITed.Add(methodDef);
                }
            #endregion

            UpdateModule(Module);

            #region "Protect Methods"
            foreach (MethodDef methodDef in MethodsJITed)
            {
                byte[] ilasByteArray = Module.GetOriginalRawILBytes(methodDef);
                ilasByteArray = PrependBytes(ilasByteArray, new byte[] { 0, 0, 0, 0, 0 });
                int size = ilasByteArray.Length;
                int num2 = SearchArray(AssemblyByte, ilasByteArray);
                if (num2 != -1)
                {
                    EncryptDecrypt(ilasByteArray);
                    for (int i = 0; i < size; i++)
                    {
                        AssemblyByte[num2] = ilasByteArray[i];
                        num2++;
                    }
                }
            }
            #endregion

            return AssemblyByte;
        }

        public static void EncryptDecrypt(byte[] data)
        {
            //Hardcoded because gay
            byte[] array = Convert.FromBase64String("W2h0dHBzOi8vZ2l0aHViLmNvbS9UaGVIZWxsVG93ZXIgfCBodHRwczovL2NyYWNrZWQuaW8vVGhlSGVsbFRvd2VyXSB4NjQgc3VwcG9ydCAmIGEgZmV3IG90aGVyIHRoaW5ncyB8IDA2LzI2LzIwMjQ=");
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

        private static void UpdateModule(ModuleDefMD M)
        {
            ModuleWriterOptions moduleWriterOptions = new ModuleWriterOptions(M);
            moduleWriterOptions.MetadataOptions.Flags |= MetadataFlags.PreserveAll;
            moduleWriterOptions.MetadataLogger = DummyLogger.NoThrowInstance;
            MemoryStream memoryStream = new MemoryStream();
            M.Write(memoryStream, moduleWriterOptions);
            AssemblyByte = memoryStream.ToArray();
            LMFAO = ModuleDefMD.Load(memoryStream.ToArray());
        }
    }
}