using Neo.Lux.Utils;
using System.Collections.Generic;
using System.Numerics;
using System.Text;

namespace Neo.SmartContract.Framework.Services.Neo
{
    public static class Storage
    {
        public static StorageContext CurrentContext
        {
            get
            {
                return StorageContext.Find(System.ExecutionEngine.ExecutingScriptHash);
            }
        }

        public static byte[] Get(StorageContext context, byte[] key) { return context.Get(key); }

        public static byte[] Get(StorageContext context, string key){ return Get(context, Encoding.UTF8.GetBytes(key)); }

        public static void Put(StorageContext context, byte[] key, byte[] value) { context.Put(key, value); }

        public static void Put(StorageContext context, byte[] key, BigInteger value){ Put(context, key, value.ToByteArray()); }

        public static void Put(StorageContext context, byte[] key, string value){ Put(context, key, Encoding.UTF8.GetBytes(value)); }

        public static void Put(StorageContext context, string key, byte[] value){ Put(context, Encoding.UTF8.GetBytes(key), value); }

        public static void Put(StorageContext context, string key, BigInteger value){ Put(context, Encoding.UTF8.GetBytes(key), value.ToByteArray()); }

        public static void Put(StorageContext context, string key, string value){ Put(context, Encoding.UTF8.GetBytes(key), Encoding.UTF8.GetBytes(value)); }

        public static void Delete(StorageContext context, byte[] key) { context.Delete(key); }

        public static void Delete(StorageContext context, string key){ Delete(context, Encoding.UTF8.GetBytes(key)); }

        public static Iterator<byte[], byte[]> Find(StorageContext context, byte[] prefix){ return null; }

        public static Iterator<string, byte[]> Find(StorageContext context, string prefix){ return null; }
    }
}
