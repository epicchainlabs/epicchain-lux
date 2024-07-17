using Neo.Lux.Core;
using Neo.Lux.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.IO.Compression;
using System.Numerics;
using System.Threading.Tasks;

namespace NeoBlocks
{
    class Program
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


        static void ExportBlocks(uint chunk, uint block, List<string> lines)
        {
            byte[] txData;

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    var curBlock = block;
                    foreach (var line in lines)
                    {
                        var bytes = line.HexToBytes();
                        writer.Write(curBlock);
                        writer.Write(bytes.Length);
                        writer.Write(bytes);
                        curBlock++;
                    }
                }

                txData = stream.ToArray();                
            }

            var compressed = Compress(txData);

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {

                    writer.WriteVarInt(lines.Count);
                    writer.Write(compressed.Length);
                    writer.Write(compressed);
                }

                var data = stream.ToArray();
                var fileName = "chain/chunk" + chunk;
                File.WriteAllBytes(fileName, data);

               // LoadChunk(fileName);
            }

            lines.Clear();
        }

        static List<Block> LoadChunk(string fileName)
        {
            var bytes = File.ReadAllBytes(fileName);

            byte[] txdata;
            int blockCount;
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    blockCount = (int)reader.ReadVarInt();
                    var len = reader.ReadInt32();
                    var compressed = reader.ReadBytes(len);

                    txdata = Decompress(compressed);
                }
            }

            uint currentBlock = 0;
            var blocks = new List<Block>();
            using (var stream = new MemoryStream(txdata))
            {
                using (var reader = new BinaryReader(stream))
                {
                    for (int i = 0; i < blockCount; i++)
                    {
                        currentBlock = reader.ReadUInt32();
                        var len = reader.ReadInt32();
                        var blockData = reader.ReadBytes(len);

                        var block = Block.Unserialize(blockData);
                        blocks.Add(block);
                    }
                }
            }

            return blocks;
        }

        const int chunkSize = 2500;

        static void ExportChunk(uint chunkID, uint maxBlock, NeoAPI api)
        {
            var fileName = "chain/chunk" + chunkID;
            if (File.Exists(fileName))
            {
                var blocks = LoadChunk(fileName);
                if (blocks.Count == chunkSize)
                {
                    return;
                }
            }


            var lines = new List<string>();

            uint startBlock = chunkID * chunkSize;
            uint endBlock = startBlock + (chunkSize-1);
            for (uint i=startBlock; i<=endBlock; i++)
            {
                if (i > maxBlock)
                {
                    break;
                }

                var response = api.QueryRPC("getblock", new object[] { i });
                var blockData = response.GetString("result");
                lines.Add(blockData);                
            }

            ExportBlocks(chunkID, startBlock, lines);
        }

        static void Main(string[] args)
        {

            var cc = "0040998ee2e92b6da874d1e7c574b63c561fbc5a08fa38b296a59cecabf0237e779274c11894241b8030215c7ef48943ecb999a0bf5fb3143f00e401ac1ff3d42d22".HexToBytes();
            var inst = Neo.Lux.Debugger.NeoTools.Disassemble(cc);
            foreach (var entry in inst)
            {
                var tt = "";

                if (entry.data != null && entry.data.Length>0) tt = entry.data.ByteToHex();

                Console.WriteLine(entry.opcode + " " + tt);
            }
            Console.ReadLine();
            return;

            /*var files = Directory.EnumerateFiles("chain").OrderBy(c =>
            {
                var temp = c.Replace("chain\\chunk", "");
                return int.Parse(temp);
            }).ToList();

            foreach (var file in files)
            {
                Console.WriteLine("Loading " + file);
                startBlock = LoadChunk(file) + 1;
                chunk++;
            }

            Console.WriteLine("Finished in "+delta+" seconds");
            Console.ReadLine();
            return;*/


            var api = new LocalRPCNode(10332, "http://neoscan.io");
            var blockCount = api.GetBlockHeight();
            var chunkCount = blockCount / chunkSize;

            var avg = 0;

            for (uint i=0; i<chunkCount; i++)
            {
                var startT = Environment.TickCount;
                ExportChunk(i, blockCount, api);
                var endT = Environment.TickCount;
                var delta = (endT - startT) / 1000;

                avg = (delta * 3 + avg) / 4;

                var left = (chunkCount - (i - 1));
                var estimated_time = left * avg;

                string ss;
                if (estimated_time < 60)
                {
                    ss = estimated_time + "s";
                }
                else
                if (estimated_time < 60*60)
                {
                    ss = estimated_time / 60 + "m";
                }
                else
                {
                    ss = (estimated_time / (60f*60f)).ToString("0.00") + "h";
                }

                Console.WriteLine($"{left} left, {delta}s block time, total estimated time: "+ss);
            }

            Console.WriteLine("Finished");
            Console.ReadLine();
        }
    }
}
