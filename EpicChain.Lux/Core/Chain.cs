using Neo.Lux.Cryptography;
using Neo.Lux.Utils;
using Neo.Lux.VM;
using Neo.SmartContract.Framework;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.Lux.Core
{
    public class Storage: IApiInterface
    {
        public Dictionary<byte[], byte[]> entries = new Dictionary<byte[], byte[]>(new ByteArrayComparer());

        public byte[] Get(byte[] key)
        {
            return entries.ContainsKey(key) ? entries[key] : null;
        }

        public byte[] Get(string key)
        {
            return Get(Encoding.UTF8.GetBytes(key));
        }
    }

    public struct Notification
    {
        public readonly UInt256 Hash;
        public readonly string Name;
        public readonly object[] Args;

        public Notification(UInt256 hash, string name, object[] args)
        {
            this.Hash = hash;
            this.Name = name;
            this.Args = args;
        }
    }

    public class Account
    {
        public UInt160 hash;

        public Dictionary<byte[], BigInteger> balances = new Dictionary<byte[], BigInteger>(new ByteArrayComparer());

        public List<Transaction.Input> unspent = new List<Transaction.Input>();

        public Contract contract;
        public Storage storage;

        public void Deposit(byte[] asset, BigInteger amount, Transaction.Input input)
        {
            var balance = balances.ContainsKey(asset) ? balances[asset] : 0;
            balance += amount;
            balances[asset] = balance;

            unspent.Add(input);
        }

        public void Withdraw(byte[] asset, BigInteger amount, Transaction.Input input)
        {
            var balance = balances.ContainsKey(asset) ? balances[asset] : 0;
            balance -= amount;
            balances[asset] = balance;
            unspent.RemoveAll( x => x.prevHash == input.prevHash && x.prevIndex == input.prevIndex);
        }
    }

    public class ChainVM : ExecutionEngine
    {
        public readonly Chain Chain;

        public ChainVM(Chain chain, Transaction tx): base(tx, chain, chain)
        {
            this.Chain = chain;
        }

        public override ExecutionContext LoadScript(byte[] script, int rvcount = -1)
        {
            var result = base.LoadScript(script, rvcount);

            this.Chain.OnVMLoad(this, script);

            return result;
        }
    }

    public class Chain: InteropService, IScriptTable, IBlockchainProvider
    {
        protected Dictionary<uint, Block> _blocks = new Dictionary<uint, Block>();

        protected Dictionary<UInt256, Block> _blockMap = new Dictionary<UInt256, Block>();
        protected Dictionary<UInt256, Transaction> _txMap = new Dictionary<UInt256, Transaction>();
        protected Dictionary<byte[], Asset> _assetMap = new Dictionary<byte[], Asset>(new ByteArrayComparer());

        protected Dictionary<UInt160, Account> _accountMap = new Dictionary<UInt160, Account>();

        private Dictionary<UInt256, List<Notification>> _notifications = new Dictionary<UInt256, List<Notification>>();

        public bool VerificateTransactions = true;

        private Block lastBlock = null;
        public uint GetBlockHeight()
        {
            return lastBlock != null ? lastBlock.Height : 0;
        }

        protected Action<string> Logger { get; private set; }

        private TriggerType currentTrigger;
        private decimal currentGas = 0;

        public Chain()
        {
            Logger = DummyLog;
            RegisterVMMethods();
        }

        public void SetLogger(Action<string> logger)
        {
            this.Logger = logger != null ? logger : DummyLog;
        }

        protected virtual uint GetTime()
        {
            return DateTime.UtcNow.ToTimestamp();
        }

        private void DummyLog(string msg)
        {

        }

        public void SyncWithNode(NeoAPI api)
        {
            var max = api.GetBlockHeight();

            var min = (uint)_blocks.Count;

            for (uint i = min; i<=max; i++)
            {
                var block = api.GetBlock(i);
                AddBlock(block);
            }
        }

        public bool AddBlock(Block block)
        {
            foreach (var tx in block.transactions)
            {
                if (!ValidateTransaction(tx))
                {
                    ValidateTransaction(tx);
                    return false;
                }
            }

            _blocks[block.Height] = block;
            _blockMap[block.Hash] = block;

            foreach (var tx in block.transactions)
            {
                ExecuteTransaction(tx);
            }

            foreach (var tx in block.transactions)
            {
                _txMap[tx.Hash] = tx;
            }

            lastBlock = block;

            return true;
        }

        private bool ValidateTransaction(Transaction tx)
        {
            switch (tx.type)
            {
                case TransactionType.ContractTransaction:
                case TransactionType.InvocationTransaction:
                    {
                        if (VerificateTransactions && tx.script != null)
                        {
                            var vm = ExecuteVM(tx, TriggerType.Verification);
                            var stack = vm.ResultStack;
                            var result = stack != null && stack.Count >= 1 && stack.Peek(0).GetBoolean();

                            if (!result)
                            {
                                return false;
                            }
                        }
                        break;
                    }
            }

            return true;
        }

        private void ExecuteTransaction(Transaction tx)
        {
            Logger(".................................");
            Logger("Transaction " + tx.Hash);
            if (tx.inputs != null)
            {
                foreach (var input in tx.inputs)
                {
                    var input_tx = GetTransaction(input.prevHash);
                    var output = input_tx.outputs[input.prevIndex];

                    var account = GetAccount(output.scriptHash);
                    account.Withdraw(output.assetID, output.value.ToBigInteger(), input);

                    var asset = GetAsset(output.assetID);
                    if (asset != null)
                    {
                        var address = output.scriptHash.ToAddress();
                        Logger($"Withdrawing {output.value} {asset.name} from {address}");
                    }
                }
            }

            uint index = 0;
            if (tx.outputs != null)
            {
                foreach (var output in tx.outputs)
                {
                    var account = GetAccount(output.scriptHash);
                    var unspent = new Transaction.Input() { prevIndex = index, prevHash = tx.Hash };
                    account.Deposit(output.assetID, output.value.ToBigInteger(), unspent);

                    var asset = GetAsset(output.assetID);
                    var address = output.scriptHash.ToAddress();
                    Logger($"Depositing {output.value} {asset.name} to {address}");

                    index++;
                }
            }

            switch (tx.type)
            {
                case TransactionType.PublishTransaction:
                    {
                        var contract_hash = tx.contractRegistration.script.ToScriptHash();
                        var account = GetAccount(contract_hash);
                        account.contract = tx.contractRegistration;
                        account.storage = new Storage();
                        break;
                    }

                case TransactionType.InvocationTransaction:
                    {
                        ExecuteVM(tx, TriggerType.Application);
                        break;
                    }
            }
        }

        internal virtual void OnVMLoad(ExecutionEngine vm, byte[] script)
        {
            
        }

        protected virtual void OnVMStep(ExecutionEngine vm)
        {

        }

        protected virtual void OnVMBreak(ExecutionEngine vm)
        {

        }

        private ExecutionEngine ExecuteVM(Transaction tx, TriggerType trigger)
        {
            Logger("Executing VM with trigger " + trigger);
            var vm = new ChainVM(this, tx);
            currentTrigger = trigger;
            currentGas = 0;
            vm.LoadScript(tx.script);

            while (!vm.State.HasFlag(VMState.HALT) && !vm.State.HasFlag(VMState.FAULT))
            {
                vm.Execute(x => OnVMStep(x));
                if (vm.State.HasFlag(VMState.BREAK))
                {
                    OnVMBreak(vm);
                }
            }
            

            if (vm.State == VMState.HALT)
            {
                int index = 0;

                var sb = new StringBuilder();
                foreach (StackItem item in vm.ResultStack)
                {
                    if (index > 0)
                    {
                        sb.Append(" / ");
                    }

                    sb.Append(FormattingUtils.StackItemAsString(item, true));
                    index++;
                }
                Logger("Stack: " + sb);
            }
            else
            {
                Logger("State: " + vm.State);
            }

            return vm;
        }

        public Block GetBlock(uint height)
        {
            return _blocks.ContainsKey(height) ? _blocks[height] : null;
        }

        public Block GetBlock(UInt256 hash)
        {
            return _blockMap.ContainsKey(hash) ? _blockMap[hash] : null;
        }

        public Account GetAccount(UInt160 hash)
        {
            if (_accountMap.ContainsKey(hash))
            {
                return _accountMap[hash];
            }

            var account = new Account();
            account.hash = hash;
            _accountMap[hash] = account;
            return account;
        }

        public Asset GetAsset(byte[] id)
        {
            return _assetMap.ContainsKey(id) ? _assetMap[id] : null;
        }

        public Transaction GetTransaction(UInt256 hash)
        {
            return _txMap.ContainsKey(hash) ? _txMap[hash] : null;
        }


        public List<Notification> GetNotifications(Transaction tx)
        {
            if (tx != null && _notifications.ContainsKey(tx.Hash)){
                return _notifications[tx.Hash];
            }

            return null;
        }

        public virtual byte[] GetScript(byte[] script_hash)
        {
            var hash = new UInt160(script_hash);

            var address = hash.ToAddress();
            Logger($"Fetching contract at address {address}");

            var account = GetAccount(hash);
            return account != null && account.contract != null ? account.contract.script : null;
        }

        public InvokeResult InvokeScript(byte[] script)
        {
            var invocation = new InvokeResult();

            Transaction tx = new Transaction()
            {
                type = TransactionType.InvocationTransaction,
                version = 0,
                script = script,
                gas = 0,
                inputs = new Transaction.Input[] { },
                outputs = new Transaction.Output[] { }
            };

            var vm = ExecuteVM(tx, TriggerType.Application);

            invocation.gasSpent = currentGas;
            invocation.state = vm.State;
            var stack = vm.ResultStack;
            invocation.result = stack.Peek(0);

            return invocation;
        }

        public static object StackItemToObject(StackItem item)
        {
            if (item == null)
            {
                return null;
            }
            else
            if (item is VM.Types.ByteArray)
            {
                return item.GetByteArray();
            }
            else
            if (item is VM.Types.Integer)
            {
                return item.GetByteArray();
            }
            else
            if (item is VM.Types.Boolean)
            {
                return item.GetByteArray();
            }
			else
            if (item is VM.Types.Struct)
            {
                return item.GetByteArray();
            }
            else
            if (item is VM.Types.Array)
            {
                return item.GetByteArray();
            }
            else
            {
                var t = item.GetType();
                throw new Exception("Unknown type: " + t.Name);
            }
        }

        protected virtual void Reset()
        {
            _blocks.Clear();
            _blockMap.Clear();
            _txMap.Clear();
            _accountMap.Clear();
            _assetMap.Clear();

            _notifications.Clear();
        }

        internal void SetBlocks(Dictionary<uint, Block> blocks)
        {
            Reset();

            for (uint height =0; height<blocks.Count; height++)
            {
                AddBlock(blocks[height]);
            }
        }

        #region VM METHODS
        private static T GetInteropFromStack<T>(ExecutionEngine engine) where T : class, IApiInterface
        {
            if (engine.CurrentContext.EvaluationStack.Count == 0)
            {
                return default(T);
            }

            var obj = engine.CurrentContext.EvaluationStack.Pop() as VM.Types.InteropInterface;
            if (obj == null)
            {
                return default(T);

            }

            return obj.GetInterface<T>();
        }

        private void RegisterVMMethods()
        {
            VMAPI.RegisterAPI(this, this);

            Register("Neo.Contract.Create", Contract_Create, 500);

            Register("Neo.Storage.GetContext", engine => { var hash = engine.CurrentContext.ScriptHash; var account = GetAccount(hash); engine.CurrentContext.EvaluationStack.Push((new VM.Types.InteropInterface(account.storage))); return true; }, defaultGasCost);
            Register("Neo.Storage.Get", Storage_Get, 0.1m);
            Register("Neo.Storage.Put", Storage_Put, 0.1m);
            Register("Neo.Storage.Delete", Storage_Delete, 0.1m);

            Register("Neo.Runtime.GetTime", Runtime_GetTime, defaultGasCost);
            Register("Neo.Runtime.GetTrigger", Runtime_GetTrigger, defaultGasCost);
            Register("Neo.Runtime.CheckWitness", Runtime_CheckWitness, 0.2m);
            Register("Neo.Runtime.Log", Runtime_Log, defaultGasCost);
            Register("Neo.Runtime.Notify", Runtime_Notify, defaultGasCost);
        }

        public bool Runtime_Notify(ExecutionEngine engine)
        {
            //params object[] state
            var something = engine.CurrentContext.EvaluationStack.Pop();

            if (something is ICollection)
            {
                var items = (ICollection)something;

                string eventName = null;
                var eventArgs = new List<object>();

                int index = 0;

                var sb = new StringBuilder();
                sb.Append("Notification: ");
                foreach (StackItem item in items)
                {
                    if (index > 0)
                    {
                        sb.Append(" / ");
                        eventArgs.Add(StackItemToObject(item));
                    }
                    else
                    {
                        eventName = item.GetString();
                    }

                    sb.Append(FormattingUtils.StackItemAsString(item, true));
                    index++;
                }

                List<Notification> list;
                var tx = (Transaction)engine.ScriptContainer;

                if (_notifications.ContainsKey(tx.Hash))
                {
                    list = _notifications[tx.Hash];
                }
                else
                {
                    list = new List<Notification>();
                    _notifications[tx.Hash] = list;
                }

                list.Add(new Notification(tx.Hash, eventName, eventArgs.ToArray()));

                Logger(sb.ToString());
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool Runtime_Log(ExecutionEngine engine)
        {
            var msg = engine.CurrentContext.EvaluationStack.Pop();
            Logger(FormattingUtils.StackItemAsString(msg));
            return true;
        }

        private bool Storage_Get(ExecutionEngine engine)
        {
            var storage = GetInteropFromStack<Storage>(engine);
            var key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            var key_name = FormattingUtils.OutputData(key, false);
            Logger($"Storage.Get: {key_name}");

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
            var storage = GetInteropFromStack<Storage>(engine);

            var key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            var val = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            var key_name = FormattingUtils.OutputData(key, false);
            var val_name = FormattingUtils.OutputData(val, false);
            Logger($"Storage.Put: {key_name} => {val_name}");

            storage.entries[key] = val;
            return true;
        }

        private bool Storage_Delete(ExecutionEngine engine)
        {
            var storage = GetInteropFromStack<Storage>(engine);
            var key = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            var key_name = FormattingUtils.OutputData(key, false);
            Logger($"Storage.Delete: {key_name}");

            if (storage.entries.ContainsKey(key))
            {
                storage.entries.Remove(key);
            }

            return true;
        }

        private bool Runtime_GetTrigger(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)currentTrigger);
            return true;
        }

        private bool Runtime_GetTime(ExecutionEngine engine)
        {
            engine.CurrentContext.EvaluationStack.Push((int)this.GetTime());
            return true;
        }

        protected virtual bool ValidateWitness(UInt160 a, UInt160 b)
        {
            return a.Equals(b);
        }

        public bool Runtime_CheckWitness(ExecutionEngine engine)
        {
            byte[] hashOrPubkey = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();

            bool result;

            var tx = (Transaction)engine.ScriptContainer;

            if (hashOrPubkey.Length == 20) // script hash
            {
                var hash = new UInt160(hashOrPubkey);

                var address = hash.ToAddress();

                Logger($"Checking witness: {address}");

                result = false;

                foreach (var context in engine.InvocationStack)
                {
                    if (ValidateWitness(hash, context.ScriptHash))
                    {
                        result = true;
                        break;
                    }
                }

                if (!result)
                foreach (var input in tx.inputs)
                {
                    var reference = GetTransaction(input.prevHash);
                    var output = reference.outputs[input.prevIndex];
                    var other_address = output.scriptHash.ToAddress();

                    if (ValidateWitness(output.scriptHash, hash))
                    {
                        result = true;
                        break;
                    }
                }
            }
            else if (hashOrPubkey.Length == 33) // public key
            {
                //hash = ECPoint.DecodePoint(hashOrPubkey, ECCurve.Secp256r1);
                //result = CheckWitness(engine, Contract.CreateSignatureRedeemScript(pubkey).ToScriptHash());
                throw new Exception("ECPoint witness");
            }
            else
            {
                result = false;
            }

            //DoLog($"Checking Witness [{matchType}]: {FormattingUtils.OutputData(hashOrPubkey, false)} => {result}");

            engine.CurrentContext.EvaluationStack.Push(new VM.Types.Boolean(result));
            return true;
        }

        public bool Transaction_GetReferences(ExecutionEngine engine)
        {
            var tx = GetInteropFromStack<Transaction>(engine);

            if (tx == null)
            {
                return false;
            }

            var items = new List<StackItem>();

            var references = new List<Transaction.Output>();
            foreach (var input in tx.inputs)
            {
                var other_tx = GetTransaction(input.prevHash);
                references.Add(other_tx.outputs[input.prevIndex]);
            }

            foreach (var reference in references)
            {
                items.Add(new VM.Types.InteropInterface(reference));
            }

            var result = new VM.Types.Array(items.ToArray<StackItem>());
            engine.CurrentContext.EvaluationStack.Push(result);

            return true;
        }

        public bool Transaction_GetOutputs(ExecutionEngine engine)
        {
            var tx = GetInteropFromStack<Transaction>(engine);

            if (tx == null)
            {
                return false;
            }

            var items = new List<StackItem>();

            foreach (var output in tx.outputs)
            {
                items.Add(new VM.Types.InteropInterface(output));
            }

            var result = new VM.Types.Array(items.ToArray<StackItem>());
            engine.CurrentContext.EvaluationStack.Push(result);

            return true;
        }

        public bool Transaction_GetInputs(ExecutionEngine engine)
        {
            var tx = GetInteropFromStack<Transaction>(engine);

            if (tx == null)
            {
                return false;
            }

            var items = new List<StackItem>();

            foreach (var input in tx.inputs)
            {
                items.Add(new VM.Types.InteropInterface(input));
            }

            var result = new VM.Types.Array(items.ToArray<StackItem>());
            engine.CurrentContext.EvaluationStack.Push(result);

            return true;
        }

        private bool Contract_Create(ExecutionEngine engine)
        {
            var script = engine.CurrentContext.EvaluationStack.Pop().GetByteArray();
            var hash = script.ToScriptHash();
            var account = GetAccount(hash);

            var contract = new Contract();
            account.contract = contract;
            account.storage = new Storage();

            var stack = engine.CurrentContext.EvaluationStack;
            contract.parameterList = stack.Pop().GetByteArray();
            contract.returnType = stack.Pop().GetByte();
            contract.properties = (ContractProperties)stack.Pop().GetByte();
            contract.name = stack.Pop().GetString();
            contract.version = stack.Pop().GetString();
            contract.author = stack.Pop().GetString();
            contract.email = stack.Pop().GetString();
            contract.description = stack.Pop().GetString();
            contract.script = script;

            var address = hash.ToAddress();
            Logger($"Contract {contract.name} deployed at address {address}");

            stack.Push(new VM.Types.InteropInterface(contract));

            return true;
        }


        #endregion
    }

}
