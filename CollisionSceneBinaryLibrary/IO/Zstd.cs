using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ZstdSharp;

namespace CollisionSceneBinaryTool
{
    public class Zstd
    {
        public static Stream Decompress(string src)
        {
            using var decompressor = new Decompressor();
            {
                return new MemoryStream(decompressor.Unwrap(File.ReadAllBytes(src)).ToArray());
            }
        }


        public static byte[] Decompress(byte[] src)
        {
            using var decompressor = new Decompressor();
            {
                return decompressor.Unwrap(src).ToArray();
            }
        }

        public static byte[] Compress(byte[] src)
        {
            using var compressor = new Compressor();
            {
                var compressedData = compressor.Wrap(src);
                return compressedData.ToArray();
            }
        }
    }
}
