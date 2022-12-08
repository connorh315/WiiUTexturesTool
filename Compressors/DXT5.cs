using System.Numerics;
using ModLib;

namespace WiiUTexturesTool.Compressors
{
    internal static class DXT5
    {
        internal static Pair[] Deswizzle(Pair[] pairs, int width, int height, int mipmapCount)
        {
            mipmapCount = Math.Max(mipmapCount, 1);

            pairsPerLine = width / 4;
            Pair[] result = new Pair[pairs.Length];
            Array.Copy(pairs, result, pairs.Length);

            int mipmapPairCount = pairsPerLine * (height / 4);
            int mipmapPairOffset = 0;
            for (int mipmapId = 0; mipmapId < mipmapCount; mipmapId++)
            {
                for (int pairPos = mipmapPairOffset; pairPos < mipmapPairOffset + mipmapPairCount; pairPos++) // < mipmapPairOffset + mipmapPairCount
                {
                    int i = pairPos - mipmapPairOffset;
                    int pos = GetArrayOffset(GetInverse(GetDisplacement(GetBlockOffset(GetStripOffset(GetLocalOffset(i), i), i), i), i));
                    result[mipmapPairOffset + pos] = pairs[pairPos];
                }
                mipmapPairOffset += mipmapPairCount;
                mipmapPairCount /= 4;
                pairsPerLine /= 2;
                width /= 2;
                height /= 2;
                if (width <= 64 || height <= 64) break;
            }

            return result;
        }

        private static int pairsPerLine;

        private static Vector2 GetLocalOffset(int i)
        { // Builds 4x2 blocks
            return new Vector2((i / 2) % 4, i % 2);
        }

        private static Vector2[] offsets = new Vector2[]
        {
            new Vector2(0, 0),
            new Vector2(4, 0),
            new Vector2(0, 8),
            new Vector2(4, 8),
            new Vector2(8, 8),
            new Vector2(12, 8),
            new Vector2(8, 0),
            new Vector2(12, 0)
        };

        private static byte[] stripArrangement1 = new byte[] { 0, 1, 2, 3, 4, 5, 6, 7 };

        private static byte[] stripArrangement2 = new byte[] { 4, 5, 6, 7, 0, 1, 2, 3 };

        private static Vector2 GetStripOffset(Vector2 chunkOffset, int i)
        { // Translates blocks based on strip offset
            int offset = (i / 8) % 8;
            byte[] arrangement;
            switch ((i / (pairsPerLine * 32)) % 2)
            {
                case 0:
                    arrangement = stripArrangement1;
                    break;
                case 1:
                    arrangement = stripArrangement2;
                    break;
                default:
                    arrangement = stripArrangement1;
                    break;
            }
            return chunkOffset + offsets[arrangement[offset]];
        }

        private static Vector2 GetBlockOffset(Vector2 wOffset, int i)
        { // Builds a 128 x 64 px block
            return wOffset + new Vector2(((i / 64) % 2) * 16, ((i / 128) % 4) * 2);
        }

        private static Vector2 GetDisplacement(Vector2 bOffset, int i)
        { // Moves blocks right if there's the room, otherwise down
            return bOffset + new Vector2(((i / 512) % (pairsPerLine / 32)) * 32, (i / (512 * (pairsPerLine / 32))) * 16);
        }

        private static Vector2 GetInverse(Vector2 dOffset, int i)
        { // Inverses blocks if row needs to be inversed
            if ((i / (pairsPerLine * 16) % 2) == 0) return dOffset; // Row is 1st, 3rd, 5th..., so no inverse is required

            return dOffset + new Vector2((i / 64) % 2 == 0 ? 16 : -16, 0); // If block is 1st, 3rd, 5th..., then we move it to the right, otherwise, left
        }

        private static int GetArrayOffset(Vector2 pos)
        { // Converts an X,Y coordinate into a linear offset in the array
            return (int)pos.X + ((int)pos.Y * pairsPerLine);
        }
    }
}
