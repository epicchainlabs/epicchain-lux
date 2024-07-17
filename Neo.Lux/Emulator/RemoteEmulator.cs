using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using LunarLabs.Parser;
using LunarLabs.Parser.JSON;
using Neo.Lux.Core;
using Neo.Lux.Cryptography;
using Neo.Lux.Utils;
using Neo.Lux.VM;

namespace Neo.Lux.Emulator
{
    public class RemoteEmulator : NeoAPI
    {
        private string host;
        private int port;

        private Action<string> logger;

        public RemoteEmulator(string host, int port, Action<string> logger)
        {
            this.host = host;
            this.port = port;
            this.logger = logger;
        }

        private DataNode DoRequest(string method, string arg)
        {
            try
            {
                logger($"Request: {method}/{arg}");
                var client = new TcpClient(host, port);
                client.NoDelay = true;

                var msg = Encoding.UTF8.GetBytes($"{method}/{arg}\r\n");

                NetworkStream stream = client.GetStream();

                stream.Write(msg, 0, msg.Length);

                var data = new byte[1024*64];

                var bytes = stream.Read(data, 0, data.Length);

                var responseData = Encoding.UTF8.GetString(data, 0, bytes);

                stream.Close();
                client.Close();

                var root = JSONReader.ReadFromString(responseData);
                return root["response"];
            }
            catch (Exception e)
            {
                throw new EmulatorException(e.ToString());
            }
        }

        public override Dictionary<string, decimal> GetAssetBalancesOf(UInt160 hash)
        {
            var response = DoRequest("GetAssetBalancesOf", hash.ToAddress());
            var result = new Dictionary<string, decimal>();
            foreach (var node in response.Children)
            {
                result[node.GetString("symbol")] = node.GetDecimal("value");
            }
            return result;
        }

        public override Block GetBlock(UInt256 hash)
        {
            try
            {
                var response = DoRequest("GetBlockByHash", hash.ToString());
                var bytes = response.GetString("block").HexToBytes();
                return Block.Unserialize(bytes);
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public override Block GetBlock(uint height)
        {
            try
            {
                var response = DoRequest("GetBlockByHeight", height.ToString());
                if (response == null)
                {
                    return null;
                }

                var bytes = response.GetString("block").HexToBytes();
                return Block.Unserialize(bytes);
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public override uint GetBlockHeight()
        {
            try
            {
                var response = DoRequest("GetChainHeight", "");
                if (response == null)
                {
                    return 0;
                }

                return response.GetUInt32("height");
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public override List<UnspentEntry> GetClaimable(UInt160 hash, out decimal amount)
        {
            try
            {
                var response = DoRequest("GetClaimable", hash.ToAddress());
                var result = new List<UnspentEntry>();
                amount = 0;
                foreach (var node in response.Children)
                {
                    var entry = new UnspentEntry()
                    {
                        hash = new UInt256(node.GetString("hash").HexToBytes()),
                        index = node.GetUInt32("index"),
                        value = node.GetDecimal("value")
                    };
                    amount += entry.value;
                    result.Add(entry);
                }
                return result;
            }
            catch (EmulatorException e)
            {
                throw e;
            }

        }

        public override byte[] GetStorage(string scriptHash, byte[] key)
        {
            try
            {
                var response = DoRequest("GetStorage", key.ByteToHex());
                return response.GetString("value").HexToBytes();
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public override Transaction GetTransaction(UInt256 hash)
        {
            try
            {
                var response = DoRequest("GetTransaction", hash.ToString());
                var bytes = response.GetString("rawtx").HexToBytes();
                return Transaction.Unserialize(bytes);
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public override Dictionary<string, List<UnspentEntry>> GetUnspent(UInt160 hash)
        {
            try
            {
                var response = DoRequest("GetUnspent", hash.ToAddress());
                var result = new Dictionary<string, List<UnspentEntry>>();

                foreach (var node in response.Children)
                {
                    var asset = node.GetString("asset");
                    var entries = node.GetNode("unspents");

                    var list = new List<UnspentEntry>();
                    foreach (var child in entries.Children)
                    {
                        var entry = new UnspentEntry()
                        {
                            hash = new UInt256(child.GetString("hash").HexToBytes()),
                            index = child.GetUInt32("index"),
                            value = child.GetDecimal("value")
                        };
                        list.Add(entry);
                    }

                    result[asset] = list;
                }
                return result;
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public override InvokeResult InvokeScript(byte[] script)
        {
            try
            {
                var response = DoRequest("InvokeScript", script.ByteToHex());

                var bytes = response.GetString("stack").HexToBytes();
                using (var stream = new MemoryStream(bytes))
                {
                    using (var reader = new BinaryReader(stream))
                    {
                        var result = new InvokeResult()
                        {
                            result = Serialization.DeserializeStackItem(reader),
                            state = (VMState)Enum.Parse(typeof(VMState), response.GetString("state"), true),
                            gasSpent = response.GetDecimal("gas"),
                        };

                        return result;
                    }
                }
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public UInt160 GetContractHash(string name)
        {
            try
            {
                var response = DoRequest("GetContractHash", name);
                var address = response.GetString("address");
                return new UInt160(address.AddressToScriptHash());
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public void AddBreakpoint(UInt160 contractHash, uint offset)
        {
            try
            {
                var response = DoRequest("AddBreakpoint", $"{contractHash.ToArray().ByteToHex()}:{offset}");
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        public void RemoveBreakpoint(UInt160 contractHash, uint offset)
        {
            try
            {
                var response = DoRequest("RemoveBreakpoint", $"{contractHash}:{offset}");
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }

        protected override bool SendTransaction(Transaction tx)
        {
            try
            {
                var hextx = tx.Serialize(true).ByteToHex();
                var response = DoRequest("SendTransaction", hextx);
                return response.GetBool("success");
            }
            catch (EmulatorException e)
            {
                throw e;
            }
        }
    }
}
