using System;
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
                if (!texFile.CheckInt(0xe, "Expected version E! Failing.")) return;
                
                texFile.ReadBigPascalString(); // CONVDATE

                if (!texFile.CheckString("ROTV", "Expected ROTV! Failing.")) return;
                uint textureCount = texFile.ReadUint(true);

                DDSFile[] files = new DDSFile[textureCount];
                for (int textureId = 0; textureId < textureCount; textureId++)
                {
                    long checksum = texFile.ReadLong() + texFile.ReadLong(); // 16-byte checksum
                    if (checksum != 0)
                    {
                        texFile.Seek(3, SeekOrigin.Current); // not sure
                    }
                    string outputFile = texFile.ReadPascalString();
                    Console.WriteLine(outputFile);
                    files[textureId].Name = outputFile;

                    files[textureId].FirstByte = texFile.ReadByte();
                    texFile.Seek(8, SeekOrigin.Current); // Unknown
                }
                
                texFile.Seek(ddsOffset, SeekOrigin.Begin);
                Logger.Log("Extracting {0} textures:", textureCount);

                for (int textureId = 0; textureId < textureCount; textureId++)
                {
                    long ddsHeader = texFile.Position;

                    if (files[textureId].Name.StartsWith("/"))
                    {
                        Logger.Log("({0}) - {1} - Nothing to extract (external reference)", textureId, files[textureId]);
                        continue;
                    }

                    texFile.Seek(12, SeekOrigin.Current); // Skip past the DDS header
                    int height = texFile.ReadInt();
                    int width = texFile.ReadInt();
                    texFile.Seek(8, SeekOrigin.Current);
                    int mipmapCount = texFile.ReadInt();
                    texFile.Seek(52, SeekOrigin.Current);
                    string comType = texFile.ReadString(4);
                    bool isCubemap = files[textureId].FirstByte == 6;
                    int fileSize = GetDataSize(width, height, Math.Max(1, mipmapCount), comType == "DXT1", isCubemap) + 0x80; // 0x80 for header

                    if (!files[textureId].Name.Contains(@"\") && !files[textureId].Name.Contains("/")) files[textureId].Name += "." + textureId;
                    
                    string outputFile = Path.Combine(settings.OutputLocation, files[textureId].Name) + ".dds";
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    
                    using (ModFile ddsStream = texFile.LoadSegment(ddsHeader, fileSize))
                    {
                        if (settings.ShouldDeswizzle && IsPowerOfTwo(width) && IsPowerOfTwo(height) && width > 64 && height > 64)
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

        internal static int GetDataSize(int width, int height, int mipmapCount, bool isDXT1, bool isCubeMap)
        {
            int totalSize = 0;
            int blockSize = isDXT1 ? 8 : 16;
            for (int i = 0; i < mipmapCount; i++)
            {
                int mipmapSize = Math.Max(1, ((width + 3) / 4) * ((height + 3) / 4)) * blockSize;
                totalSize += mipmapSize;
                width /= 2;
                height /= 2;
            }
            return isCubeMap ? totalSize * 6 : totalSize;
        }

        internal static bool IsPowerOfTwo(int x)
        {
            return (x & (x - 1)) == 0;
        }
    }

    internal struct DDSFile
    {
        public string Name;
        public byte FirstByte;
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
