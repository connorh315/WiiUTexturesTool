using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ModLib;
using WiiUTexturesTool.Compressors;

namespace WiiUTexturesTool
{
    public static class WiiUTextures
    {
        public static void Extract(WUTExtractSettings settings)
        {
            using (ModFile wutFile = ModFile.Open(settings.InputLocation))
            {
                if (wutFile.Status != ModFileStatus.Success)
                {
                    Logger.Error("Failed to open file {0} due to {1}! Check that the file is not in use, and that the tool has access to the file!", settings.InputLocation, wutFile.Status);
                    return;
                }

                DDSFileAttributes[] files = ParseFileHeader(wutFile);

                Logger.Log("Extracting {0} textures:", files.Length);

                for (int textureId = 0; textureId < files.Length; textureId++)
                {
                    long ddsHeader = wutFile.Position;

                    if (files[textureId].Path != string.Empty)
                    {
                        Logger.Log("({0}) - {1} - Nothing to extract (external reference)", textureId, files[textureId].Path);
                        continue;
                    }

                    bool isCubemap = files[textureId].Type == TextureType.Cubemap;
                    DDSFileHeader file = GetFileInfo(wutFile, isCubemap);

                    if (!files[textureId].Name.Contains(@"\") && !files[textureId].Name.Contains(@"/")) files[textureId].Name += "." + textureId; // Lightmaps are just called "Lightmap" and will overwrite each other
                    
                    string outputFile = Path.Combine(settings.OutputLocation, files[textureId].Name) + ".dds";
                    Directory.CreateDirectory(Path.GetDirectoryName(outputFile));
                    
                    using (ModFile ddsStream = wutFile.LoadSegment(ddsHeader, file.Length))
                    {
                        if (settings.ShouldDeswizzle && IsPowerOfTwo(file.Width) && IsPowerOfTwo(file.Height) && file.Width > 64 && file.Height > 64 && !isCubemap)
                        {
                            Logger.Log(new LogSeg("({0}) - {1} - ", textureId.ToString(), outputFile), new LogSeg("Deswizzling...", ConsoleColor.DarkYellow));

                            ddsStream.Seek(0x80, SeekOrigin.Begin);

                            using (ModFile ddsFile = ModFile.Create())
                            {
                                if (file.ComType == 1)
                                {
                                    DXT1.Deswizzle(ddsFile, ddsStream, file.Width, file.Height, file.Mipmaps);
                                }
                                else if (file.ComType == 5)
                                {
                                    DXT5.Deswizzle(ddsFile, ddsStream, file.Width, file.Height, file.Mipmaps);
                                }
                                else
                                {
                                    Logger.Warn("DDS file uses unknown compression type - File may not have extracted correctly.");
                                }

                                ddsStream.fileStream.CopyTo(ddsFile.fileStream);

                                ddsStream.Seek(0, SeekOrigin.Begin);
                                ddsFile.Seek(0, SeekOrigin.Begin);
                                ddsStream.fileStream.SetLength(0x80);
                                ddsStream.fileStream.CopyTo(ddsFile.fileStream);

                                ddsFile.WriteToFile(outputFile); // Improvement over writing direct to disk due to random writes involved in deswizzling
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

                    wutFile.Seek(ddsHeader + file.Length, SeekOrigin.Begin);
                }

                Logger.Log("Done!");
            }
        }

        public static DDSFile[] RetrieveTextures(string fileLocation)
        {
            using (ModFile wutFile = ModFile.Open(fileLocation))
            {
                if (wutFile.Status != ModFileStatus.Success)
                {
                    Logger.Error("Failed to open file {0} due to {1}! Check that the file is not in use, and that the tool has access to the file!", fileLocation, wutFile.Status);
                    return null;
                }

                DDSFileAttributes[] attributes = ParseFileHeader(wutFile);

                DDSFile[] files = new DDSFile[attributes.Length];

                for (int textureId = 0; textureId < attributes.Length; textureId++)
                {
                    long ddsHeader = wutFile.Position;

                    files[textureId].Attributes = attributes[textureId];
                    if (attributes[textureId].Path != string.Empty) continue;

                    bool isCubemap = attributes[textureId].Type == TextureType.Cubemap;
                    DDSFileHeader file = GetFileInfo(wutFile, isCubemap);

                    ModFile ddsFile = ModFile.Create(file.Length);
                    files[textureId].File = ddsFile;

                    using (ModFile ddsStream = wutFile.LoadSegment(ddsHeader, file.Length))
                    {
                        if (IsPowerOfTwo(file.Width) && IsPowerOfTwo(file.Height) && file.Width > 64 && file.Height > 64 && !isCubemap)
                        {
                            ddsStream.Seek(0x80, SeekOrigin.Begin);
                            
                            if (file.ComType == 1)
                            {
                                DXT1.Deswizzle(ddsFile, ddsStream, file.Width, file.Height, file.Mipmaps);
                            }
                            else if (file.ComType == 5)
                            {
                                DXT5.Deswizzle(ddsFile, ddsStream, file.Width, file.Height, file.Mipmaps);
                            }

                            ddsStream.fileStream.CopyTo(ddsFile.fileStream);

                            ddsStream.Seek(0, SeekOrigin.Begin);
                            ddsFile.Seek(0, SeekOrigin.Begin);
                            ddsStream.fileStream.SetLength(0x80);
                            ddsStream.fileStream.CopyTo(ddsFile.fileStream);
                        }
                        else
                        {
                            ddsStream.fileStream.CopyTo(ddsFile.fileStream);
                        }

                    }

                    ddsFile.Seek(0, SeekOrigin.Begin);
                }

                return files;
            }
        }

        private static DDSFileAttributes[] ParseFileHeader(ModFile wutFile)
        {
            uint headerOffset = wutFile.ReadUint(true);
            wutFile.Seek(headerOffset, SeekOrigin.Current);

            uint headerSize = wutFile.ReadUint(true);
            long ddsOffset = headerSize + wutFile.Position;

            if (!wutFile.CheckString(".CC4TSXT", "Expected .CC4TSXT! Failing.")) return null;
            if (!wutFile.CheckInt(1, "Expected 0x1! Failing.")) return null;
            if (!wutFile.CheckString("TSXT", "Expected TSXT! Failing.")) return null;
            uint tsxtVersion = wutFile.ReadUint(true);
            if (tsxtVersion != 0xE && tsxtVersion != 0xC)
            {
                Logger.Error("Expected TSXT versions 0xC or 0xE!");
                return null;
            }

            wutFile.ReadBigPascalString(); // CONVDATE

            if (!wutFile.CheckString("ROTV", "Expected ROTV! Failing.")) return null;
            uint textureCount = wutFile.ReadUint(true);

            DDSFileAttributes[] files = new DDSFileAttributes[textureCount];
            for (int textureId = 0; textureId < textureCount; textureId++)
            {
                long checksum = wutFile.ReadLong() + wutFile.ReadLong(); // 16-byte checksum

                files[textureId].Path = wutFile.ReadPascalString();
                files[textureId].Name = wutFile.ReadPascalString();
                files[textureId].Type = (TextureType)wutFile.ReadByte();

                if (tsxtVersion == 0xE)
                {
                    files[textureId].Unknown1 = wutFile.ReadByte();
                    files[textureId].Unknown2 = wutFile.ReadByte();
                    files[textureId].Mipmaps = wutFile.ReadShort(true);
                    files[textureId].ObjectID = wutFile.ReadPascalString();
                    files[textureId].Unknown3 = wutFile.ReadByte();
                }
            }

            wutFile.Seek(ddsOffset, SeekOrigin.Begin);

            return files;
        }

        internal static DDSFileHeader GetFileInfo(ModFile wutFile, bool isCubemap)
        { // Because CalculateSize isn't always correct, this checks that it lands on the next "DDS " header, and if not we trawl through the file from the original offset until we find it.
            long origOffset = wutFile.Position;
            wutFile.Seek(12, SeekOrigin.Current); // Skip past the DDS header
            int height = wutFile.ReadInt();
            int width = wutFile.ReadInt();
            wutFile.Seek(8, SeekOrigin.Current);
            int mipmapCount = wutFile.ReadInt();
            wutFile.Seek(52, SeekOrigin.Current);

            string comSign = wutFile.ReadString(4);
            byte comType;
            switch (comSign)
            {
                case "DXT1":
                    comType = 1;
                    break;
                case "DXT5":
                    comType = 5;
                    break;
                default:
                    comType = 0xff;
                    break;
            }
            
            wutFile.Seek(0x1a, SeekOrigin.Current);
            byte fourty = wutFile.ReadByte();

            int calculatedSize = CalculateSize(width, height, Math.Max(1, mipmapCount), comType == 1, isCubemap) + 0x80 + (isCubemap && (fourty == 0x40) ? 0x70 : 0); // 0x80 for header and the 0x70 comes from the cubemaps with extra data...
            DDSFileHeader file = new DDSFileHeader(width, height, mipmapCount, calculatedSize, comType);

            wutFile.Seek(origOffset + calculatedSize, SeekOrigin.Begin);
            if (wutFile.Position != wutFile.Length && !wutFile.CheckString("DDS ", string.Empty))
            {
                // If we did not correctly calculate the file size, a more "brute" approach is required
                wutFile.Seek(origOffset + 1, SeekOrigin.Begin);

                int find = wutFile.Find("DDS ");
                file.Length = (int)((find != -1) ? find + 1 : (wutFile.Length - wutFile.Position));
            }

            wutFile.Seek(origOffset, SeekOrigin.Begin);

            return file;
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

    public struct DDSFileAttributes
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

    internal struct DDSFileHeader
    {
        public int Width;
        public int Height;
        public int Mipmaps;
        public int Length;
        public byte ComType;

        public DDSFileHeader(int width, int height, int mipmaps, int length, byte comType)
        {
            Width = width;
            Height = height;
            Mipmaps = mipmaps;
            Length = length;
            ComType = comType;
        }
    }

    public enum TextureType
    {
        Diffuse = 0,
        Normal = 1,
        Cubemap = 6,
        BRDF = 12
    }

    public struct WUTExtractSettings
    {
        public string InputLocation;
        public string OutputLocation;
        public bool ShouldDeswizzle;
    }

    public struct DDSFile
    {
        public DDSFileAttributes Attributes;
        public ModFile File;
    }

    internal class Pair
    {
        public long Part1;
        public long Part2;
    }
}
