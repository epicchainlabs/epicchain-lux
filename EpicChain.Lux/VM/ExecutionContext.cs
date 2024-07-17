using Neo.Lux.Cryptography;
using Neo.Lux.Utils;
using System;
using System.IO;

namespace Neo.Lux.VM
{
    public class ExecutionContext : IDisposable
    {
        public readonly byte[] Script;
        internal readonly int RVCount;
        internal readonly BinaryReader OpReader;

        public RandomAccessStack<StackItem> EvaluationStack { get; } = new RandomAccessStack<StackItem>();
        public RandomAccessStack<StackItem> AltStack { get; } = new RandomAccessStack<StackItem>();

        public int InstructionPointer
        {
            get
            {
                return (int)OpReader.BaseStream.Position;
            }
            set
            {
                OpReader.BaseStream.Seek(value, SeekOrigin.Begin);
            }
        }

        public OpCode NextInstruction => (OpCode)Script[OpReader.BaseStream.Position];

        private UInt160 _script_hash = null;
        public UInt160 ScriptHash
        {
            get
            {
                if (_script_hash == null)
                    _script_hash = new UInt160(CryptoUtils.Hash160(Script));
                return _script_hash;
            }
        }

        internal ExecutionContext(ExecutionEngine engine, byte[] script, int rvcount)
        {
            this.Script = script;
            this.RVCount = rvcount;
            this.OpReader = new BinaryReader(new MemoryStream(script, false));
        }

        public void Dispose()
        {
            OpReader.Dispose();
        }
    }
}
