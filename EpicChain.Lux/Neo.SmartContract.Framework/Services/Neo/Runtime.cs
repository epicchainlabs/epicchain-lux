using System;
using System.Numerics;
using System.Text;
using Neo.Lux.Utils;

namespace Neo.SmartContract.Framework.Services.Neo
{
    public static class Runtime
    {
        public static TriggerType Trigger => TriggerType.Application;

        public static uint Time => DateTime.UtcNow.ToTimestamp();

        public static bool CheckWitness(byte[] hashOrPubkey) { return true; }

        public static void Notify(params object[] state) {
            var sb = new StringBuilder();
            foreach (var obj in state)
            {
                if (sb.Length > 0)
                {
                    sb.Append(", ");
                }

                if (obj is byte[])
                {
                    sb.Append(((byte[])obj).ByteToHex());
                }
                else
                {
                    sb.Append(obj.ToString());
                }
            }
            Console.WriteLine("NOTIFY: " + sb);
        }

        public static void Log(string message) {
            Console.WriteLine("LOG: " + message);
        }

        public static Func<string, object[], object> CallHandler = null;
        public static byte[] CallHash; // HACK!!

        public static object Call(string operation, object[] args)
        {
            byte[] hash = CallHash;

            var tempCall = System.ExecutionEngine.CallingScriptHash;
            var tempExecuting = System.ExecutionEngine.ExecutingScriptHash;
            System.ExecutionEngine.CallingScriptHash = System.ExecutionEngine.ExecutingScriptHash;
            System.ExecutionEngine.ExecutingScriptHash = hash;

            var result = CallHandler(operation, args);

            System.ExecutionEngine.CallingScriptHash = tempCall;
            System.ExecutionEngine.ExecutingScriptHash = tempExecuting;

            return result;
        }

        public static T Cast<T>(object src)
        {
            return (T)Cast(typeof(T), src);
        }

        public static object Cast(global::System.Type type, object src)
        {
            var obj = global::System.Activator.CreateInstance(type);

            var fields = type.GetFields();

            if (type == typeof(BigInteger))
            {
                var num = ((Lux.VM.StackItem)src).GetBigInteger();
                return num;
            }

            var str = (Lux.VM.Types.Struct)src;

            int index = 0;
            object box = obj;
            foreach (var field in fields)
            {
                var item = str[index];

                object val;

                if (item is Lux.VM.Types.ByteArray)
                {
                    val = item.GetByteArray();
                }
                else
                if (item is Lux.VM.Types.Integer)
                {
                    val = item.GetBigInteger();
                }
                else
                if (item is Lux.VM.Types.Boolean)
                {
                    val = item.GetBoolean();
                }
                else
                if (item is Lux.VM.Types.Struct && ((Lux.VM.Types.Struct)item).Count == 0)
                {
                    val = new BigInteger(0);
                }
                else
                if (item is Lux.VM.Types.Array)
                {
                    var array = (Lux.VM.Types.Array)item;
                    var arrayType = field.FieldType;
                    var elementType = arrayType.GetElementType();

                    var dest = global::System.Array.CreateInstance(elementType, array.Count);
                    for (int i = 0; i < array.Count; i++)
                    {
                        var arrayItem = Cast(elementType, array[i]);
                        dest.SetValue(arrayItem, i);
                    }
                    val = dest;
                }
                else
                {
                    throw new global::System.NotImplementedException();
                }

                field.SetValue(box, val);
                index++;
            }

            return box;
        }

    }
}
