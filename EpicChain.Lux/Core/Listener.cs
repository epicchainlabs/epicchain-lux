using Neo.Lux.Cryptography;
using Neo.Lux.Debugger;
using Neo.Lux.Utils;
using Neo.Lux.VM;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

namespace Neo.Lux.Core
{
    public class ListenerVM : InteropService, IScriptTable
    {
        private Dictionary<UInt160, byte[]> scripts = new Dictionary<UInt160, byte[]>();
        private Dictionary<UInt160, Storage> storage = new Dictionary<UInt160, Storage>();
        public Block currentBlock { get; private set; }

        private Dictionary<UInt256, List<Notification>> notifications = new Dictionary<UInt256, List<Notification>>();

        private IBlockchainProvider provider;

        public ListenerVM(IBlockchainProvider provider)
        {
            this.provider = provider;

            VMAPI.RegisterAPI(provider, this);

            Register("Neo.Storage.GetContext", engine => { var hash = engine.CurrentContext.ScriptHash; engine.CurrentContext.EvaluationStack.Push((new VM.Types.InteropInterface(storage[hash]))); return true; }, defaultGasCost);
            Register("Neo.Storage.Get", Storage_Get, 0.1m);
            Register("Neo.Storage.Put", Storage_Put, 0.1m);
            Register("Neo.Storage.Delete", Storage_Delete, 0.1m);

            Register("Neo.Runtime.GetTime", engine => { engine.CurrentContext.EvaluationStack.Push(currentBlock.Date.ToTimestamp()); return true; }, defaultGasCost);
            Register("Neo.Runtime.GetTrigger", engine => { engine.CurrentContext.EvaluationStack.Push((int)TriggerType.Application); return true; }, defaultGasCost);
            //Register("Neo.Runtime.Log", Runtime_Log, defaultGasCost);
            Register("Neo.Runtime.Notify", Runtime_Notify, defaultGasCost);
        }

        private bool Storage_Get(ExecutionEngine engine)
        {
            var storage = engine.GetInteropFromStack<Storage>();
            var key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            var key_name = FormattingUtils.OutputData(key, false);

            if (storage.entries.ContainsKey(key))
            {
                engine.CurrentContext.EvaluationStack.Push(storage.entries[key]);
            }
            else
            {
                engine.CurrentContext.EvaluationStack.Push(new byte[0] { });
            }

            return true;
        }

        private bool Storage_Put(ExecutionEngine engine)
        {
            var storage = engine.GetInteropFromStack<Storage>();

            var key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            var val = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            var key_name = FormattingUtils.OutputData(key, false);
            var val_name = FormattingUtils.OutputData(val, false);

            storage.entries[key] = val;
            return true;
        }

        private bool Storage_Delete(ExecutionEngine engine)
        {
            var storage = engine.GetInteropFromStack<Storage>();
            var key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            var key_name = FormattingUtils.OutputData(key, false);

            if (storage.entries.ContainsKey(key))
            {
                storage.entries.Remove(key);
            }

            return true;
        }
      
        private bool Runtime_Notify(ExecutionEngine engine)
        {
            var something = engine.CurrentContext.EvaluationStack.Pop();

            if (something is ICollection)
            {
                var items = (ICollection)something;

                string eventName = null;
                var eventArgs = new List<object>();

                int index = 0;

                foreach (StackItem item in items)
                {
                    if (index > 0)
                    {
                        eventArgs.Add(Chain.StackItemToObject(item));
                    }
                    else
                    {
                        eventName = item.GetString();
                    }

                    index++;
                }

                List<Notification> list;
                var tx = (Transaction)engine.ScriptContainer;

                if (notifications.ContainsKey(tx.Hash))
                {
                    list = notifications[tx.Hash];
                }
                else
                {
                    list = new List<Notification>();
                    notifications[tx.Hash] = list;
                }

                list.Add(new Notification(tx.Hash, eventName, eventArgs.ToArray()));

                return true;
            }
            else
            {
                return false;
            }
        }

        public void AddScript(byte[] script)
        {
            UInt160 scriptHash = script.ToScriptHash();
            scripts[scriptHash] = script;
            storage[scriptHash] = new Storage();
        }

        public byte[] GetScript(byte[] script_hash)
        {
            var hash = new UInt160(script_hash);
            return scripts[hash];
        }

        public Storage GetStorage(UInt160 scriptHash)
        {
            return storage.ContainsKey(scriptHash) ? storage[scriptHash] : null;
        }

        public Storage GetStorage(NEP5 token)
        {
            return GetStorage(token.ScriptHash);
        }

        public IEnumerable<Notification> GetNotifications(UInt256 hash)
        {
            return notifications.ContainsKey(hash) ? notifications[hash] : null;
        }

        public IEnumerable<Notification> GetNotifications(Transaction tx)
        {
            return GetNotifications(tx.Hash);
        }

        public void SetCurrentBlock(Block block)
        {
            this.currentBlock = block;
        }
    }

    public class Snapshot: IBlockchainProvider
    {
        private Dictionary<UInt256, Transaction> transactions = new Dictionary<UInt256, Transaction>();
        private Dictionary<UInt256, Block> blocks = new Dictionary<UInt256, Block>();
        private HashSet<UInt256> external_txs = new HashSet<UInt256>();

        public uint GetBlockHeight() {
            throw new NotImplementedException();
        }

        public Block GetBlock(UInt256 hash)
        {
            return blocks[hash];
        }

        public Block GetBlock(uint height)
        {
            throw new NotImplementedException();
        }

        public Transaction GetTransaction(UInt256 hash)
        {
            return transactions.ContainsKey(hash) ? transactions[hash] : null;
        }

        internal Snapshot(IEnumerable<string> lines)
        {
            foreach (var line in lines)
            {
                var temp = line.Split(',');
                switch (temp[0])
                {
                    case "B":
                        {
                            var data = temp[1].HexToBytes();
                            var block = Block.Unserialize(data);
                            blocks[block.Hash] = block;

                            foreach (var tx in block.transactions)
                            {
                                transactions[tx.Hash] = tx;
                            }

                            break;
                        }

                    case "T":
                        {
                            var data = temp[1].HexToBytes();
                            var tx = Transaction.Unserialize(data);
                            transactions[tx.Hash] = tx;
                            external_txs.Add(tx.Hash);
                            break;
                        }
                }
            }
        }

        public Snapshot(NEP5 token, uint startBlock, uint endBlock = 0) : this(token.ScriptHash, token.api, startBlock, endBlock)
        {
        }

        public Snapshot(UInt160 scriptHash, NeoAPI api, uint startBlock, uint endBlock = 0)
        {
            if (endBlock == 0)
            {
                endBlock = api.GetBlockHeight();
            }

            if (endBlock < startBlock)
            {
                throw new ArgumentException("End block cannot be smaller than start block");
            }

            for (uint height = startBlock; height <= endBlock; height++)
            {
                var block = api.GetBlock(height);

                var snapCount = 0;

                foreach (var tx in block.transactions)
                {
                    switch (tx.type)
                    {
                        case TransactionType.ContractTransaction:
                            {

                                foreach (var output in tx.outputs)
                                {
                                    if (output.scriptHash == scriptHash)
                                    {
                                        MergeTransaction(api, tx);
                                        snapCount++;
                                        break;
                                    }
                                }

                                break;
                            }

                        case TransactionType.InvocationTransaction:
                            {
                                List<AVMInstruction> ops;
                                try
                                {
                                    ops = NeoTools.Disassemble(tx.script);
                                }
                                catch
                                {
                                    continue;
                                }

                                for (int i = 0; i < ops.Count; i++)
                                {
                                    var op = ops[i];

                                    if (op.opcode == OpCode.APPCALL && op.data != null && op.data.Length == 20)
                                    {
                                        var otherScriptHash = new UInt160(op.data);

                                        if (otherScriptHash == scriptHash)
                                        {
                                            MergeTransaction(api, tx);
                                            snapCount++;
                                            break;
                                        }
                                    }
                                }

                                break;
                            }
                    }
                }

                if (snapCount > 0)
                {
                    blocks[block.Hash] = block;
                }
            }
        }

        private void MergeTransaction(NeoAPI api, Transaction tx)
        {
            transactions[tx.Hash] = tx;

            foreach (var input in tx.inputs)
            {
                if (!transactions.ContainsKey(input.prevHash))
                {
                    var other = api.GetTransaction(input.prevHash);
                    transactions[other.Hash] = other;
                    external_txs.Add(other.Hash);
                }
            }
        }

        public IEnumerable<string> Export()
        {
            var result = new List<string>();

            foreach (var block in blocks.Values)
            {
                var data = block.Serialize().ByteToHex();
                result.Add("B," + data);
            }

            foreach (var hash in external_txs)
            {
                var tx = transactions[hash];
                var data = tx.Serialize().ByteToHex();
                result.Add("T," + data);
            }

            return result;
        }

        public static Snapshot Import(IEnumerable<string> lines)
        {
            return new Snapshot(lines);
        }

        private static uint FindBlock(NeoAPI api, uint timestamp, uint min, uint max)
        {
            var mid = (1 + max - min) / 2;
            do
            {
                var block = api.GetBlock(mid);
                var blockTime = block.Date.ToTimestamp();

                if (blockTime == timestamp)
                {
                    return block.Height;
                }
                else 
                if (blockTime < timestamp)
                {
                    var next = api.GetBlock(mid + 1);
                    var nextTime = next.Date.ToTimestamp();
                    if (nextTime == timestamp)
                    {
                        return next.Height;
                    }
                    else 
                    if  (nextTime > timestamp)
                    {
                        return block.Height;
                    }
                    else
                    {
                        return FindBlock(api, timestamp, mid + 1, max);
                    }
                }
                else
                {
                    return FindBlock(api, timestamp, min, mid - 1);
                }

            } while (true);
        }

        public static uint FindBlock(NeoAPI api, DateTime date)
        {
            uint min = 0;
            var max = api.GetBlockHeight();

            var timestamp = date.ToTimestamp();
            return FindBlock(api, timestamp, min, max);
        }

        public void Execute(NeoAPI api, UInt160 script_hash, Action<ListenerVM> visitor)
        {
            var balances = new Dictionary<UInt160, decimal>();

            var vm = new ListenerVM(this);
            /*vm.AddScript(token_script);

            var debugger = new DebugClient();
            debugger.SendScript(token_script);
            */
            throw new NotImplementedException();

            IEnumerable<Block> sorted_blocks = blocks.Values.OrderBy(x => x.Date);

            foreach (var block in sorted_blocks)
            {
                vm.SetCurrentBlock(block);

                bool executed = false;

                foreach (var tx in block.transactions)
                {
                    switch (tx.type)
                    {
                        case TransactionType.InvocationTransaction:
                            {
                                List<AVMInstruction> ops;
                                try
                                {
                                    ops = NeoTools.Disassemble(tx.script);
                                }
                                catch
                                {
                                    continue;
                                }

                                for (int i = 0; i < ops.Count; i++)
                                {
                                    var op = ops[i];

                                    if (op.opcode == OpCode.APPCALL && op.data != null && op.data.Length == 20)
                                    {
                                        var otherScriptHash = new UInt160(op.data);

                                        if (otherScriptHash != script_hash)
                                        {
                                            continue;
                                        }

                                        var engine = new ExecutionEngine(tx, vm, vm);
                                        engine.LoadScript(tx.script);

                                        engine.Execute(
                                            x =>
                                            {
                                                //debugger.Step(x);
                                            }
                                            );

                                        executed = true;
                                    }
                                }

                                break;
                            }
                    }
                }

                if (executed)
                {
                    visitor(vm);
                }
            }
        }

    }
}