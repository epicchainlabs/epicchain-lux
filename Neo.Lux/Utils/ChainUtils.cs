using Neo.Lux.Core;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;

namespace Neo.Lux.Utils
{
    public static class ChainUtils
    {
        public static byte[] Compress(byte[] data)
        {
            using (var compressStream = new MemoryStream())
            using (var compressor = new DeflateStream(compressStream, CompressionMode.Compress))
            {
                compressor.Write(data, 0, data.Length);
                compressor.Close();
                return compressStream.ToArray();
            }
        }

        public static byte[] Decompress(byte[] data)
        {
            var output = new MemoryStream();
            using (var compressedStream = new MemoryStream(data))
            {
                using (var zipStream = new DeflateStream(compressedStream, CompressionMode.Decompress))
                {
                    var buffer = new byte[4096];
                    int read;
                    while ((read = zipStream.Read(buffer, 0, buffer.Length)) > 0)
                    {
                        output.Write(buffer, 0, read);
                    }
                    output.Position = 0;
                    return output.ToArray();
                }
            }
        }

        public static void ExportBlocks(this Chain chain, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Create))
            {
                using (var writer = new BinaryWriter(stream))
                {
                    uint height = (uint)chain.GetBlockHeight();
                    writer.Write(height);
                    for (uint i = 0; i < height; i++)
                    {
                        var block = chain.GetBlock(i);
                        var bytes = block.Serialize();
                        int len = bytes.Length;
                        writer.Write(len);
                        writer.Write(bytes);

                        var temp = Block.Unserialize(bytes);
                    }
                }
            }
        }

        public static void ImportBlocks(this Chain chain, string fileName)
        {
            using (var stream = new FileStream(fileName, FileMode.Open))
            {
                var blocks = new Dictionary<uint, Block>();

                using (var reader = new BinaryReader(stream))
                {
                    uint height = reader.ReadUInt32();                    
                    for (uint i = 0; i < height; i++)
                    {
                        int len = reader.ReadInt32();
                        var bytes = reader.ReadBytes(len);
                        var block = Block.Unserialize(bytes);
                        blocks[i] = block;
                    }

                    chain.SetBlocks(blocks);
                }
            }
        }


    }
}
