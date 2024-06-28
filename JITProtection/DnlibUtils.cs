﻿using dnlib.DotNet;

namespace JITProtection
{
    internal static class DnlibUtils
    {
        internal static byte[] GetOriginalRawILBytes(this ModuleDefMD module, MethodDef methodDef)
        {
            var reader = module.Metadata.PEImage.CreateReader(methodDef.RVA);

            byte b = reader.ReadByte();

            uint codeSize = 0;
            switch (b & 7)
            {
                case 2:
                case 6:
                    codeSize = (uint)(b >> 2);
                    break;
                case 3:
                    var flags = (ushort)((reader.ReadByte() << 8) | b);
                    var headerSize = (byte)(flags >> 12);
                    reader.ReadUInt16();
                    codeSize = reader.ReadUInt32();
                    reader.ReadUInt32();

                    reader.Position = reader.Position - 12 + headerSize * 4U;
                    break;
            }

            byte[] ilBytes = new byte[codeSize];
            reader.ReadBytes(ilBytes, 0, ilBytes.Length);
            return ilBytes;
        }
    }
}