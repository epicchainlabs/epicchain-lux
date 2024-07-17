using LunarParser;
using LunarParser.JSON;
using LunarParser.XML;
using Neo.Lux.Core;
using Neo.Lux.Cryptography;
using Neo.Lux.Debugger;
using Neo.Lux.Utils;
using Neo.Lux.VM;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace DemoSnapshot
{
    class Program
    {
        static void Main(string[] pargs)
        {

            //var api = new LocalRPCNode(10332, "http://neoscan.io");
            var api = new RemoteRPCNode(10332, "http://neoscan.io");

            {
                Console.WriteLine("amount "+ new BigInteger("00943577".HexToBytes()));
                Console.WriteLine("to " + new UInt160("fc3f33cb3e2d79f82fadef5f407ac1576d304bc1".HexToBytes()).ToAddress());
                Console.WriteLine("from " + new UInt160("a30cadcc858aa4b89d9db098ef154c5e1ab74464".HexToBytes()).ToAddress());

                return;
            }

            {
                var snap_data = File.ReadAllLines("souls_snap.csv");
                var snap = Snapshot.Import(snap_data);

                var avm_script = File.ReadAllBytes(@"D:\code\Crypto\PhantasmaNeo\PhantasmaContract\bin\Debug\PhantasmaContract.avm");
                var soul_token = api.GetToken("SOUL");

                var lines = new List<string>();

                uint ico_war_time = 1527465600;
                uint ico_start_time = 1527379200;

                Console.WriteLine("Block,Stage,Tx Hash,Address,Action,NEO sent,NEO refund");
                snap.Execute(soul_token, avm_script, vm =>
                {
                    var storage = vm.GetStorage(soul_token);
                    var block = vm.currentBlock;

                    int stage = (block.Timestamp < ico_war_time) ? ((block.Timestamp < ico_start_time) ? 0 : 1) : 2;

                    //var bytes = storage.Get("totalSupply");
                    //var n = new BigInteger(bytes);

                    foreach (var tx in block.transactions)
                    {
                        UInt160 src = null;
                        
                        /*foreach (var input in tx.inputs)
                        {
                            var input_tx = snap.GetTransaction(input.prevHash);
                            if (input_tx == null)
                            {
                                input_tx = api.GetTransaction(input.prevHash);
                            }
                            var output = input_tx.outputs[input.prevIndex];
                            src = output.scriptHash;
                        }

                        if (src == null)
                        {
                            continue;
                        }*/

                        decimal neo_sent = 0;
                        foreach (var output in tx.outputs)
                        {
                            if (output.scriptHash == soul_token.ScriptHash)
                            {
                                neo_sent += output.value;
                            }
                        }

                        var src_address = src!=null ? src.ToAddress(): "???";

                        if (tx.Hash.ToString() == "0xc39fb2304f721382a40142ed8af9d4470850f39fabeef9bb8bad892a6b63f4d6")
                        {
                            src_address += "";
                        }

                        if (tx.type == TransactionType.ContractTransaction)
                        {
                            decimal refund = 0;
                            foreach (var output in tx.outputs)
                            {
                                if (output.scriptHash == soul_token.ScriptHash)
                                {
                                    refund += output.value;
                                }
                            }
                            Console.WriteLine($"Block #{block.Height},{stage},{tx.Hash},{src_address},contract,{neo_sent},{refund}");
                        }
                        else
                        {
                            var notifications = vm.GetNotifications(tx);

                            if (notifications != null)
                            {
                                foreach (var entry in notifications)
                                {
                                    decimal refund = 0;
                                    if (entry.Name == "refund")
                                    {
                                        var bytes = (byte[])entry.Args[1];
                                        var n = new BigInteger(bytes);
                                        refund = (decimal)(n / 100000000);
                                    }
                                    Console.WriteLine($"Block #{block.Height},{stage},{entry.Hash},{src_address},{entry.Name},{neo_sent},{refund}");
                                }
                            }
                        }
                    }


                });

                return;
            }
            
            uint roundBlock = 2320640;
            uint startBlock = 2313827;
            uint endBlock = 2320681;

            {
                var snap = new Snapshot(api.GetToken("SOUL"), startBlock, endBlock);
                var export = snap.Export();
                File.WriteAllLines("souls_snap.csv", export.ToArray());
                return;
            }

            //startBlock = roundBlock;

            var soul_hash = LuxUtils.ReverseHex("ed07cffad18f1308db51920d99a2af60ac66a7b3").HexToBytes();
            var soul_hash_int = new UInt160(soul_hash);

            var startT = Environment.TickCount;

            uint maxblock = 0;

            var blockCount = api.GetBlockHeight();

            var soul_balances = new Dictionary<UInt160, BigInteger>();
            var bought = new Dictionary<UInt160, decimal>();

            BigInteger max_supply = 91136510; // total token amount
            BigInteger team_supply = 14500000; // team token amount
            BigInteger advisor_supply = 5500000; // advisor token amount
            BigInteger platform_supply = 15000000; // company token amount
            BigInteger presale_supply = 43503435; // presale token amount

            BigInteger total_supply = team_supply + advisor_supply + presale_supply + platform_supply;

            var whitelist = new HashSet<UInt160>();

            var maybe = new HashSet<UInt160>();
            var sure = new HashSet<UInt160>();
            var expected = new Dictionary<UInt160, decimal>();

            var txlist = new List<string>();

            for (uint height = startBlock; height <= endBlock; height++)
            {
                var block = api.GetBlock(height);

                //Console.WriteLine(height + " " + block.Timestamp.ToString());

                foreach (var tx in block.transactions)
                {
                    if (tx.type == TransactionType.ContractTransaction)
                    {
                        foreach (var output in tx.outputs)
                        {
                            if (output.scriptHash == soul_hash_int)
                            {
                                /*foreach (var input in tx.inputs)
                                {
                                    var input_tx = api.GetTransaction(input.prevHash);
                                    var outp = input_tx.outputs[input.prevIndex];
                                    var sender = outp.scriptHash;
                                    Console.WriteLine($"refund,{tx.Hash},{sender.ToAddress()},{output.value}");
                                    break;
                                }*/
                            }
                        }

                        continue;
                    }

                    if (tx.type != TransactionType.InvocationTransaction)
                    {
                        continue;
                    }

                    List<AVMInstruction> ops;
                    try
                    {
                        ops = NeoTools.Disassemble(tx.script);
                    }
                    catch
                    {
                        continue;
                    }

                    if (tx.Hash.ToString() == "0xcf530159dc7fa7d0ea38ca210b479b04da20039b3b6639cf4c06bf528d415339")
                    {
                        tx.gas += 0;
                    }

                    for (int i = 0; i < ops.Count; i++)
                    {
                        var op = ops[i];

                        if (op.opcode == OpCode.APPCALL && op.data.SequenceEqual(soul_hash))
                        {
                            var engine = new ExecutionEngine(null);
                            engine.LoadScript(tx.script);
                            engine.Execute(null);

                            var operation = engine.EvaluationStack.Peek().GetString();
                            var args = ((IEnumerable<StackItem>) engine.EvaluationStack.Peek(1)).ToList();

                            var witnesses = new HashSet<UInt160>();
                            foreach (var input in tx.inputs)
                            {
                                var input_tx = api.GetTransaction(input.prevHash);
                                witnesses.Add(input_tx.outputs[input.prevIndex].scriptHash);
                            }

                            switch (operation)
                            {
                                case "mintTokens":
                                    {
                                        decimal neo_amount = 0;

                                        foreach (var output in tx.outputs)
                                        {
                                            if (output.scriptHash == soul_hash_int)
                                            {
                                                neo_amount += output.value;
                                            }
                                        }

                                        var sender = witnesses.First();
                                        var cur_bought = bought.ContainsKey(sender) ? bought[sender] : 0;

                                        decimal refund = 0;

                                        if (block.Height >= roundBlock)
                                        {
                                            maybe.Add(sender);
                                            expected[sender] = neo_amount * 273;
                                        }

                                        if (!whitelist.Contains(sender))
                                        {
                                            refund = neo_amount;
                                            neo_amount = 0;
                                        }
                                        else
                                        if (cur_bought + neo_amount > 10)
                                        {
                                            var temp = neo_amount;
                                            neo_amount = 10 - cur_bought;

                                            refund = temp - neo_amount;
                                        }

                                        if (neo_amount > 0)
                                        {
                                            BigInteger souls = (int)neo_amount * 273;
                                            /*if (souls + total_supply > max_supply)
                                            {
                                                souls = max_supply - total_supply;
                                            }*/

                                            total_supply += souls * 2;

                                            if (soul_balances.ContainsKey(sender))
                                            {
                                                soul_balances[sender] += new BigInteger((long)(souls * 100000000));
                                            }
                                            else
                                            {
                                                soul_balances[sender] = new BigInteger((long)(souls * 100000000));
                                            }

                                            Console.WriteLine(block.Height + "," + tx.Hash + ",mint," + soul_hash_int.ToAddress() + "," + sender.ToAddress() + "," + souls);
                                            cur_bought += neo_amount;
                                            bought[sender] = cur_bought;
                                        }
                                        else
                                        if (refund > 0)
                                        {
                                            Console.WriteLine(block.Height + "," + tx.Hash + ",refund," + soul_hash_int.ToAddress() + "," + sender.ToAddress() + "," + refund);
                                        }

                                        break;
                                    }

                                case "whitelistAddFree":
                                    {
                                        foreach (var addr in args)
                                        {
                                            var hash = new UInt160(addr.GetByteArray());
                                            whitelist.Add(hash);
                                        }
                                        break;
                                    }

                                case "whitelistAddFilled":
                                    {
                                        foreach (var addr in args)
                                        {
                                            var hash = new UInt160(addr.GetByteArray());
                                            whitelist.Add(hash);

                                            bought[hash] = 2730;
                                        }
                                        break;
                                    }

                                case "whitelistAddCap":
                                    {
                                        decimal cap = 0;

                                        int index = 0;

                                        foreach (var addr in args)
                                        {
                                            if (index == 0)
                                            {
                                                var amount = addr.GetBigInteger();
                                                cap = (decimal)(amount / 100000000);
                                            }
                                            else
                                            {
                                                var hash = new UInt160(addr.GetByteArray());
                                                whitelist.Add(hash);

                                                bought[hash] = cap;
                                            }

                                            index++;
                                        }
                                        break;
                                    }

                                case "chainSwap":
                                    {
                                        throw new Exception("Exploit found");
                                        break;
                                    }

                                case "deploy":
                                    {
                                        soul_balances[new UInt160("Abyd4BcStNksGLmfdHtyyPbS1xzhceDKLs".AddressToScriptHash())] = new BigInteger((long)6316538 * (long)100000000);
                                        soul_balances[new UInt160("ARWHJefSbhayC2gurKkpjMHm5ReaJZLLJ3".AddressToScriptHash())] = new BigInteger((long)43503435 * (long)100000000);
                                        soul_balances[new UInt160("AQFQmVQi9VReLhym1tF3UfPk4EG3VKbAwN".AddressToScriptHash())] = new BigInteger((long)15000000 * (long)100000000);

                                        break;
                                    }

                                case "transfer":
                                    {
                                        if (args.Count == 3)
                                        {
                                            var from = new UInt160(args[0].GetByteArray());

                                            if (!witnesses.Contains(from))
                                            {
                                                //throw new Exception("Invalid");
                                            }

                                            if (maybe.Contains(from))
                                            {
                                                sure.Add(from);
                                            }

                                            var to = new UInt160(args[1].GetByteArray());
                                            var value = args[2].GetBigInteger();

                                            var from_addr = from.ToAddress();
                                            var to_addr = to.ToAddress();

                                            /*if (from == watch)
                                            {
                                                Console.WriteLine("WATCH " + tx.Hash);
                                            }
                                            if (to == watch)
                                            {
                                                Console.WriteLine("WATCH " + tx.Hash);
                                            }*/


                                            decimal amount = (long)value;
                                            int places = 8;
                                            while (places > 0)
                                            {
                                                amount *= 0.1m;
                                                places--;
                                            }

                                            Console.WriteLine(block.Height + "," + tx.Hash + ",transfer," + from.ToAddress() + "," + to.ToAddress() + "," + amount);
                                        }

                                        break;
                                    }
                            }

                        }

                    }
                }
            }

            /*var lines = new List<string>();
            foreach (var entry in soul_balances)
            {
                var k = (long)entry.Value;
                if (k == 0)
                {
                    continue;
                }
                var val = (decimal)k / 100000000m;
                lines.Add(entry.Key.ToAddress() + "," + val);
            }
            File.WriteAllLines("sale_soul.csv", lines.ToArray());*/

            var token = api.GetToken("SOUL");
            foreach (var entry in maybe)
            {
                var addr = entry.ToAddress();
                var balance = token.BalanceOf(addr);

                if (balance >= expected[entry])
                {
                    sure.Add(entry);
                }
            }

            foreach (var entry in sure)
            {
                maybe.Remove(entry);
                Console.WriteLine(entry.ToAddress()+","+expected[entry]);

                if (expected[entry] > 50 * 273)
                {
                    throw new Exception("AAAAAAAAA");
                }
            }

            foreach (var entry in maybe)
            {
                Console.WriteLine( entry.ToAddress()+",0");
            }

            decimal total = 0;
            foreach (var entry in sure)
            {
                total += expected[entry];
            }
            Console.WriteLine("TOTAL " + total);

            var endT = Environment.TickCount;
            var delta = (endT - startT) / 1000;
            Console.WriteLine("Finished in " + delta + " seconds, loaded " + maxblock + " blocks");

            Console.ReadLine();
            return;


            /*            Console.WriteLine(tkk.BalanceOf("AYxnCZePKhrijk2TQymYYUqm74nuwCftwq"));
                        //var token = api.GetToken("SOUL");
                        //var keys = KeyPair.FromWIF("KxnjzXUvK9BLojMrWVJF7jjdbHs37aXvHyFog4rARGNrAQ7LFjLP");

                        var token = api.GetToken("OBT");
                        var keys = KeyPair.FromWIF("L5YiR4AdUibLeFf48W3P5P36aBTWANcDu6oQNkvpaQrrHreg4RZC");

                        var balance = token.BalanceOf(keys);
                        Console.WriteLine(balance);

                        var transfers = new Dictionary<string, decimal>();
                        transfers["AHXWzaYCNYBYvhSypfi2XpAiWz2cCXrDJr"] = 16.7m;
                        transfers["AHxXMh9cPjE3cYrA9DhWNSBY2hC3a62PcH"] = 16.7m;
                        transfers["AHY7SidKpLuNj881Mc4Vjsj1Y4jBbX5EPS"] = 16.7m;

                        var txx  = token.Transfer(keys, transfers);
                        Console.WriteLine(txx.Hash);*/

            /*
            uint startBlock = 2298101;
            uint endblock = 2313169;

            var soul_hash = LuxUtils.ReverseHex("4b4f63919b9ecfd2483f0c72ff46ed31b5bbb7a4").HexToBytes();
            var soul_hash_int = new UInt160(soul_hash);


            var startT = Environment.TickCount;

            uint maxblock = 0;

            var blockCount = api.GetBlockHeight();

            var soul_balances = new Dictionary<UInt160, BigInteger>();
            var bought = new Dictionary<UInt160, decimal>();

            BigInteger max_supply = 91136510; // total token amount
            BigInteger team_supply = 14500000; // team token amount
            BigInteger advisor_supply = 5500000; // advisor token amount
            BigInteger platform_supply = 15000000; // company token amount
            BigInteger presale_supply = 43503435; // presale token amount

            BigInteger total_supply = team_supply + advisor_supply + presale_supply + platform_supply;

            var watch = new UInt160( "AQkiyWfwxMT31epRRxWXbR7wvZJH944jqh".AddressToScriptHash());

            for (uint height = startBlock; height<=endblock; height++)            
            {
                var block = api.GetBlock(height);

                //Console.WriteLine(height + " " + block.Timestamp.ToString());

                foreach (var tx in block.transactions)
                {
                    if (tx.type != TransactionType.InvocationTransaction)
                    {
                        continue;
                    }

                    List<AVMInstruction> ops;
                    try
                    {
                        ops = NeoTools.Disassemble(tx.script);
                    }
                    catch
                    {
                        continue;
                    }

                    if (tx.Hash.ToString()== "0xcf530159dc7fa7d0ea38ca210b479b04da20039b3b6639cf4c06bf528d415339")
                    {
                        tx.gas += 0;
                    }

                    for (int i = 0; i < ops.Count; i++)
                    {
                        var op = ops[i];

                        if (op.opcode == OpCode.APPCALL && op.data.SequenceEqual(soul_hash))
                        {
                            var engine = new ExecutionEngine(null);
                            engine.LoadScript(tx.script);
                            engine.Execute(null);

                            var operation = engine.EvaluationStack.Peek().GetString();

                            var witnesses = new HashSet<UInt160>();
                            foreach (var input in tx.inputs)
                            {
                                var input_tx = api.GetTransaction(input.prevHash);
                                witnesses.Add(input_tx.outputs[input.prevIndex].scriptHash);                            
                            }

                            switch (operation)
                            {
                                case "mintTokens":
                                    {
                                        decimal neo_amount = 0;

                                        if (block.Timestamp.ToTimestamp() < 1526947200)
                                        {
                                            break;
                                        }

                                        if (block.Height> 2298690)
                                        {
                                            break;
                                        }

                                        foreach (var output in tx.outputs)
                                        {
                                            if (output.scriptHash == soul_hash_int)
                                            {
                                                neo_amount += output.value;
                                            }
                                        }

                                        var sender = witnesses.First();
                                        var cur_bought = bought.ContainsKey(sender) ? bought[sender] : 0;

                                        if (cur_bought + neo_amount > 10)
                                        {
                                            neo_amount = 10 - cur_bought;
                                        }

                                        foreach (var wit in witnesses)
                                        {
                                            if (wit == watch)
                                            {
                                                Console.WriteLine("WATCH " + tx.Hash);
                                                break;
                                            }
                                        }

                                        if (neo_amount > 0)
                                        {
                                            BigInteger souls = (int)neo_amount * 273;
                                            //if (souls + total_supply > max_supply)
                                            //{
                                              //  souls = max_supply - total_supply;
                                            //}

                                            total_supply += souls * 2;

                                            if (soul_balances.ContainsKey(sender))
                                            {
                                                soul_balances[sender] += new BigInteger((long)(souls * 100000000));
                                            }
                                            else
                                            {
                                                soul_balances[sender] = new BigInteger((long)(souls * 100000000));
                                            }

                                            Console.WriteLine(tx.Hash + ",mint," + soul_hash_int.ToAddress() + "," + sender.ToAddress() + "," + souls);
                                            cur_bought += neo_amount;
                                            bought[sender] = cur_bought;
                                        }

                                        break;
                                    }

                                case "chainSwap":
                                    {
                                        throw new Exception("Exploit found");
                                        break;
                                    }

                                case "transfer":
                                    {
                                        var args = (Neo.Lux.VM.Types.Array)engine.EvaluationStack.Peek(1);

                                        if (args.Count == 3)
                                        {
                                            var from = new UInt160(args[0].GetByteArray());

                                            if (!witnesses.Contains(from))
                                            {
                                                //throw new Exception("Invalid");
                                            }

                                            var to = new UInt160(args[1].GetByteArray());
                                            var value = args[2].GetBigInteger();

                                            var from_addr = from.ToAddress();
                                            var to_addr = to.ToAddress();

                                            if (from == watch)
                                            {
                                                Console.WriteLine("WATCH " + tx.Hash);
                                            }
                                            if (to == watch)
                                            {
                                                Console.WriteLine("WATCH " + tx.Hash);
                                            }

                                            if (from_addr == "AdkLubeJgL3PCKc1Xv6CEv9PrzB4c5AKNk")
                                            {
                                                value += 0;
                                            }

                                            if (soul_balances.ContainsKey(from))
                                            {
                                                var src_balance = soul_balances[from];

                                                if (src_balance >= value)
                                                {
                                                    src_balance -= value;
                                                    soul_balances[from] = src_balance;

                                                    if (soul_balances.ContainsKey(to))
                                                    {
                                                        soul_balances[to] += value;
                                                    }
                                                    else
                                                    {
                                                        soul_balances[to] = value;
                                                    }
                                                }
                                            }
                                            else
                                            {
                                                throw new Exception("Invalid balance");
                                            }

                                            decimal amount = (long)value;
                                            int places = 8;
                                            while (places > 0)
                                            {
                                                amount *= 0.1m;
                                                places--;
                                            }

                                            Console.WriteLine(tx.Hash + ",transfer," + from.ToAddress() + "," + to.ToAddress() + "," + amount);
                                        }

                                        break;
                                    }
                            }

                        }

                    }
                }
            }

            var lines = new List<string>();
            foreach(var entry in soul_balances)
            {
                var k = (long)entry.Value;
                if (k == 0)
                {
                    continue;
                }
                var val = (decimal)k / 100000000m;
                lines.Add(entry.Key.ToAddress() + "," + val);
            }
            File.WriteAllLines("soul_snapshop.csv", lines.ToArray());

            var endT = Environment.TickCount;
            var delta = (endT - startT) / 1000;
            Console.WriteLine("Finished in " + delta + " seconds, loaded "+maxblock+" blocks");

            Console.ReadLine();
    */
        }
    }
}
