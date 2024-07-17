using Neo.Lux.Core;
using Neo.Lux.Cryptography;
using Neo.Lux.VM;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Neo.Lux.Debugger
{
    public class ScriptCall
    {
        public UInt160 contractHash;
        public string operation;
        public object[] arguments;
    }

    public class ScriptDeployment
    {
        public UInt160 contractHash;
        public byte[] script;
        public string name;
        public string author;
        public string description;
        public string email;
        public string version;
        public ContractProperties properties;
    }

    public class ScriptInspector
    {
        private List<ScriptCall> _calls = new List<ScriptCall>();
        public IEnumerable<ScriptCall> Calls => _calls;

        private List<ScriptDeployment> _deploys = new List<ScriptDeployment>();
        public IEnumerable<ScriptDeployment> Deployments => _deploys;

        public ScriptInspector(byte[] script) : this(script, x=>true)
        {

        }

        public ScriptInspector(byte[] script, UInt160 targetContract) : this (script, x => x.Equals(targetContract))
        {

        }

        private object[] PackArguments(List<AVMInstruction> instructions, ref int index)
        {
            var argCount = 1 + ((byte)instructions[index].opcode - (byte)OpCode.PUSH1);
            var arguments = new List<object>();
            while (argCount > 0)
            {
                index--;
                if (instructions[index].opcode >= OpCode.PUSHBYTES1 && instructions[index].opcode <= OpCode.PUSHBYTES75)
                {
                    arguments.Add(instructions[index].data);
                }
                else
                if (instructions[index].opcode >= OpCode.PUSH1 && instructions[index].opcode <= OpCode.PUSH16)
                {
                    var n = new BigInteger(1 + (instructions[index].opcode - OpCode.PUSH1));
                    arguments.Add(n);
                }
                else
                if (instructions[index].opcode == OpCode.PUSH0)
                {
                    arguments.Add(new BigInteger(0));
                }
                else
                if (instructions[index].opcode == OpCode.PUSHM1)
                {
                    arguments.Add(new BigInteger(-1));
                }
                else
                if (instructions[index].opcode == OpCode.PACK)
                {
                    index--;
                    var array = PackArguments(instructions, ref index);
                    arguments.Add(array);
                }
                else
                {
                    throw new Exception("Invalid arg type");
                }

                argCount--;
            }

            return arguments.ToArray();
        }

        public ScriptInspector(byte[] script, Func<UInt160, bool> filter)
        {
            var instructions = NeoTools.Disassemble(script);

            for (int i = 1; i < instructions.Count; i++)
            {
                var op = instructions[i];

                // opcode data must contain the script hash to the Bluzelle contract, otherwise ignore it
                if (op.opcode == OpCode.APPCALL && op.data != null && op.data.Length == 20)
                {
                    var scriptHash = new UInt160(op.data);

                    if (filter != null && !filter(scriptHash))
                    {
                        continue;
                    }

                    var call = new ScriptCall();
                    call.contractHash = scriptHash;
                    call.operation = Encoding.ASCII.GetString(instructions[i - 1].data);


                    int index = i - 3;
                    call.arguments = PackArguments(instructions, ref index);

                    _calls.Add(call);
                }
                else
                if (op.opcode == OpCode.SYSCALL && Encoding.ASCII.GetString(op.data) == "Neo.Contract.Create")
                {
                    var deploy = new ScriptDeployment();

                    deploy.script = instructions[i - 1].data;
                    deploy.properties = (ContractProperties) (1 + ((byte)instructions[i - 4].opcode - OpCode.PUSH1));
                    deploy.name = Encoding.ASCII.GetString(instructions[i - 5].data);
                    deploy.version = Encoding.ASCII.GetString(instructions[i - 6].data);
                    deploy.author = Encoding.ASCII.GetString(instructions[i - 7].data);
                    deploy.email = Encoding.ASCII.GetString(instructions[i - 8].data);
                    deploy.description = Encoding.ASCII.GetString(instructions[i - 9].data);

                    _deploys.Add(deploy);
                }
            }
        }
    }
}
