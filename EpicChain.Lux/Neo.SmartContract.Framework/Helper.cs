using Neo.Lux.Utils;
using Neo.Lux.VM;
using System;
using System.IO;
using System.Numerics;
using System.Text;

namespace Neo.SmartContract.Framework
{
    public static class Helper
    {
        public static BigInteger AsBigInteger(this byte[] source) { return (source == null || source.Length == 0) ? new BigInteger(0): new BigInteger(source); }

        public static byte[] AsByteArray(this BigInteger source) { return source.ToByteArray(); }

        public static byte[] AsByteArray(this string source) { return Encoding.UTF8.GetBytes(source); }

        public static string AsString(this byte[] source) { return Encoding.UTF8.GetString(source); }

        public static byte[] HexToBytes(this string hex) { return LuxUtils.HexToBytes(hex); }

        public static byte[] Concat(this byte[] first, byte[] second)
        {
            var result = new byte[first.Length + second.Length];
            for (int i = 0; i < first.Length; i++)
            {
                result[i] = first[i];
            }

            for (int i = 0; i < second.Length; i++)
            {
                result[i + first.Length] = second[i];
            }

            return result;
        }

        public static byte[] Range(this byte[] source, int index, int count) {
            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = source[i + index];
            }
            return result;
        }

        public static byte[] Take(this byte[] source, int count) {
            var result = new byte[count];
            for (int i = 0; i < count; i++)
            {
                result[i] = source[i];
            }
            return result;
        }

        public static Delegate ToDelegate(this byte[] source) { return null; }

        public static byte[] ToScriptHash(this string address) { return LuxUtils.AddressToScriptHash(address); }

        public static byte[] Serialize(this object source) {

            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    Serialization.SerializeStackItem(source.ToStackItem(), writer);
                }

                return stream.ToArray();
            }
        }

        public static object Deserialize(this byte[] source) {
            return Serialization.DeserializeStackItem(source);
        }
    }
}
