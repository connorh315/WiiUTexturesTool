using System.Numerics;
using ModLib;

namespace WiiUTexturesTool.Extract
{
    internal static class DXT1
    {
        private static int pairsPerLine;

        internal static Pair[] Deswizzle(Pair[] pairs, int width, int height, int mipmapCount)
        {
            mipmapCount = Math.Max(mipmapCount, 1);
            pairsPerLine = width / 8;
            Pair[] result = new Pair[pairs.Length];
            Array.Copy(pairs, result, pairs.Length);

            int mipmapPairCount = pairsPerLine * (height / 4);
            int mipmapPairOffset = 0;
            for (int mipmapId = 0; mipmapId < mipmapCount; mipmapId++)
            {
                for (int pairPos = mipmapPairOffset; pairPos < mipmapPairOffset + mipmapPairCount; pairPos++) // < mipmapPairOffset + mipmapPairCount
                {
                    int i = pairPos - mipmapPairOffset;
                    int pos = GetArrayOffset(GetGlobalOffset(GetWOffset(GetChunkOffset(GetLocalOffset(i), i), i), i));
                    result[mipmapPairOffset + pos] = pairs[pairPos];
                }
                mipmapPairOffset += mipmapPairCount;
                mipmapPairCount /= 4;
                pairsPerLine /= 2;
                width /= 2;
                height /= 2;
                if (width <= 64 || height <= 64) break; // Swizzling does not occur once width/height reach 64 px
            }

            return result;
        }

        private static Vector2 GetLocalOffset(int i)
        { // Builds 4x2 blocks
            return new Vector2((i / 2) % 4, i % 2);
        }

        private static Vector2 GetChunkOffset(Vector2 localOffset, int i)
        { // Builds 4x4 blocks
            return (i % 16 > 7) ? localOffset + new Vector2(0, 2) : localOffset;
        }

        private static Vector2[] offsets = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(0, 8),
            new Vector2(4, 8),
            new Vector2(4, 0),
            new Vector2(8, 0),
            new Vector2(8, 8),
            new Vector2(12, 8),
            new Vector2(12, 0)
        };

        private static byte[] wArrangement1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };
        private static byte[] wArrangement2 = new byte[] { 4, 5, 6, 7, 0, 1, 2, 3 };
        private static byte[] wArrangement3 = new byte[] { 2, 3, 0, 1, 6, 7, 4, 5 };
        private static byte[] wArrangement4 = new byte[] { 6, 7, 4, 5, 2, 3, 0, 1 };

        private static Vector2 GetWOffset(Vector2 chunkOffset, int i)
        { // Translates 4x4 blocks based on the shape of a W
            int offset = (i / 16) % 8;
            byte[] arrangement;
            switch ((i / (pairsPerLine * 16)) % 4)
            {
                case 0:
                    arrangement = wArrangement1;
                    break;
                case 1:
                    arrangement = wArrangement2;
                    break;
                case 2:
                    arrangement = wArrangement3;
                    break;
                case 3:
                    arrangement = wArrangement4;
                    break;
                default:
                    arrangement = wArrangement1;
                    break;
            }
            return chunkOffset + offsets[arrangement[offset]];
        }

        private static Vector2 GetGlobalOffset(Vector2 wOffset, int i)
        { // Displaces W both down and right
            return wOffset + new Vector2(((i / 256) % Math.Max(pairsPerLine / 16, 1)) * 16, ((i / (pairsPerLine * 16)) * 16) + ((i / 128) % 2) * 4);
        }

        private static int GetArrayOffset(Vector2 pos)
        { // Converts an X,Y coordinate into a linear offset in the array
            return (int)pos.X + ((int)pos.Y * pairsPerLine);
        }
    }
}
