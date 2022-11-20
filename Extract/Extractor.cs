﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModLib;

namespace WiiUTexturesTool.Extract
{
    public static class Extractor
    {
        public static void Extract(ExtractSettings settings)
        {
            using (ModFile texFile = ModFile.Open(settings.InputLocation))
            {
                long originalFileLength = texFile.Length;
                if (texFile.Status != ModFileStatus.Success)
                {
                    Logger.Error("Failed to open file {0} due to {1}! Check that the file is not in use, and that the tool has access to the file!", settings.InputLocation, texFile.Status);
                    return;
                }

                uint headerOffset = texFile.ReadUint(true);
                texFile.Seek(headerOffset, SeekOrigin.Current);

                uint headerSize = texFile.ReadUint(true);
                long ddsOffset = headerSize + texFile.Position;

                if (!texFile.CheckString(".CC4TSXT", "Expected .CC4TSXT! Failing.")) return;
                if (!texFile.CheckInt(1, "Expected 0x1! Failing.")) return;
                if (!texFile.CheckString("TSXT", "Expected TSXT! Failing.")) return;
                uint tsxtVersion = texFile.ReadUint(true);
                if (tsxtVersion != 0xE && tsxtVersion != 0xC)
                {
                    Logger.Error("Expected TSXT versions 0xC or 0xE!");
                    return;
                }
                
                texFile.ReadBigPascalString(); // CONVDATE

                if (!texFile.CheckString("ROTV", "Expected ROTV! Failing.")) return;
                uint textureCount = texFile.ReadUint(true);

                DDSFile[] files = new DDSFile[textureCount];
                for (int textureId = 0; textureId < textureCount; textureId++)
                {
                    long checksum = texFile.ReadLong() + texFile.ReadLong(); // 16-byte checksum

                    files[textureId].Path = texFile.ReadPascalString();
                    files[textureId].Name = texFile.ReadPascalString();
                    files[textureId].Type = (TextureType)texFile.ReadByte();
                    
                    if (tsxtVersion == 0xE)
                    {
                        files[textureId].Unknown1 = texFile.ReadByte();
                        files[textureId].Unknown2 = texFile.ReadByte();
                        files[textureId].Mipmaps = texFile.ReadShort(true);
                        files[textureId].ObjectID = texFile.ReadPascalString();
                        files[textureId].Unknown3 = texFile.ReadByte();
                    }
                }

                texFile.Seek(ddsOffset, SeekOrigin.Begin);
                Logger.Log("Extracting {0} textures:", textureCount);

                for (int textureId = 0; textureId < textureCount; textureId++)
                {
                    if (textureId == 35)
                    {
                        Console.WriteLine();
                    }
                    long ddsHeader = texFile.Position;

                    if (files[textureId].Path != string.Empty)
                    {
                        Logger.Log("({0}) - {1} - Nothing to extract (external reference)", textureId, files[textureId].Path);
                        continue;
                    }

                    bool isCubemap = files[textureId].Type == TextureType.Cubemap;
                    int fileSize = CalculateSize(width, height, Math.Max(1, mipmapCount), comType == "DXT1", isCubemap) + 0x80 + (isCubemap && (fourty == 0x40) ? 0x70 : 0); // 0x80 for header and the 0x70 comes from the cubemaps with extra data...
                    if (fileSize > 32000000 || fileSize <= 0)
                    {
                        Logger.Error("Calculated DDS file seems wrong! Most likely an error.");
                        return;
                    }

                    if (!files[textureId].Name.Contains(@"\") && !files[textureId].Name.Contains(@"/")) files[textureId].Name += "." + textureId; // Lightmaps are just called "Lightmap" and will overwrite each other
                    
                    string outputFile = Path.Combine(settings.OutputLocation, files[textureId].Name) + ".dds";
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    
                    using (ModFile ddsStream = texFile.LoadSegment(ddsHeader, fileSize))
                    {
                        if (settings.ShouldDeswizzle && IsPowerOfTwo(width) && IsPowerOfTwo(height) && width > 64 && height > 64 && !isCubemap)
                        {
                            Logger.Log(new LogSeg("({0}) - {1} - ", textureId.ToString(), outputFile), new LogSeg("Deswizzling...", ConsoleColor.DarkYellow));

                            ddsStream.Seek(0x80, SeekOrigin.Begin);
                            Pair[] pairs = new Pair[(int)Math.Ceiling(((float)(ddsStream.Length - ddsStream.Position)) / 16)];
                            for (int i = 0; i < pairs.Length; i++)
                            {
                                pairs[i] = new Pair()
                                {
                                    Part1 = ddsStream.ReadLong(),
                                    Part2 = ddsStream.ReadLong()
                                };
                            }

                            if (comType == "DXT1")
                            {
                                pairs = DXT1.Deswizzle(pairs, width, height, mipmapCount);
                            }
                            else
                            {
                                pairs = DXT5.Deswizzle(pairs, width, height, mipmapCount);
                            }

                            using (ModFile ddsFile = ModFile.Create(outputFile))
                            {
                                ddsStream.Seek(0, SeekOrigin.Begin);
                                ddsStream.fileStream.SetLength(0x80);
                                ddsStream.fileStream.CopyTo(ddsFile.fileStream);

                                for (int id = 0; id < pairs.Length; id++)
                                {
                                    ddsFile.WriteLong(pairs[id].Part1);
                                    ddsFile.WriteLong(pairs[id].Part2);
                                }
                            }
                        }
                        else
                        {
                            Logger.Log(new LogSeg("({0}) - {1}", textureId.ToString(), outputFile));
                            ModFileStatus status = ddsStream.WriteToFile(outputFile);
                            if (status != ModFileStatus.Success)
                            {
                                Logger.Error("Failed to write to file {0} due to {1}! Terminating...", outputFile, status);
                                return;
                            } 
                        }
                    }

                    texFile.Seek(ddsHeader + fileSize, SeekOrigin.Begin);
                }

                Logger.Log("Done!");
            }
        }

        internal static int GetFileSize(ModFile texFile, bool isCubemap)
        { // Because CalculateSize isn't always correct, this checks that it lands on the next "DDS " header, and if not we trawl through the file from the original offset until we find it.
            long origOffset = texFile.Position;
            texFile.Seek(12, SeekOrigin.Current); // Skip past the DDS header
            int height = texFile.ReadInt();
            int width = texFile.ReadInt();
            texFile.Seek(8, SeekOrigin.Current);
            int mipmapCount = texFile.ReadInt();
            texFile.Seek(52, SeekOrigin.Current);
            string comType = texFile.ReadString(4);
            texFile.Seek(0x1a, SeekOrigin.Current);
            byte fourty = texFile.ReadByte();

            int calculatedSize = CalculateSize(width, height, Math.Max(1, mipmapCount), comType == "DXT1", isCubemap) + 0x80 + (isCubemap && (fourty == 0x40) ? 0x70 : 0); // 0x80 for header and the 0x70 comes from the cubemaps with extra data...
            texFile.Seek(origOffset + calculatedSize, SeekOrigin.Begin);
            if (texFile.CheckString("DDS ", string.Empty))
            {
                texFile.Seek(origOffset, SeekOrigin.Begin);
                return 
            }

        }

        internal static int CalculateSize(int width, int height, int mipmapCount, bool isDXT1, bool isCubeMap)
        { // Works fine 99% of the time, but that 1% just isn't good enough
            int totalSize = 0;
            int blockSize = isDXT1 ? 8 : 16;
            for (int i = 0; i < mipmapCount; i++)
            {
                int mipmapSize = Math.Max(1, ((width + 3) / 4) * ((height + 3) / 4)) * blockSize;
                totalSize += mipmapSize;
                width /= 2;
                height /= 2;
            }
            return isCubeMap ? totalSize * 6 : totalSize; // high-five for anyone who can tell me why DXT5 requires an extra 0x70 bytes for cubemaps
        }

        internal static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }
    }

    internal struct DDSFile
    {
        public string Path;
        public string Name;
        public TextureType Type;
        public byte Unknown1;
        public byte Unknown2;
        public short Mipmaps;
        public string ObjectID;
        public byte Unknown3;
    }

    internal enum TextureType
    {
        Diffuse = 0,
        Normal = 1,
        Cubemap = 6,
        BRDF = 12
    }

    public struct ExtractSettings
    {
        public string InputLocation;
        public string OutputLocation;
        public bool ShouldDeswizzle;
    }

    internal class Pair
    {
        public long Part1;
        public long Part2;
    }
}
