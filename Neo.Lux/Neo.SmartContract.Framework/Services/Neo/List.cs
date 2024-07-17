using Neo.SmartContract.Framework.Services.System;
using System.Numerics;

namespace Neo.SmartContract.Framework.Services.Neo
{
    public class List
    {
        private static readonly byte[] count_prefix = "{count}".AsByteArray();
        private static readonly byte[] global_prefix = "{global}".AsByteArray();
        public static readonly byte[] element_begin_prefix = new byte[] { (byte)'<' };
        public static readonly byte[] element_end_prefix = new byte[] { (byte)'>' };
        private readonly byte[] BaseKey;

        private List(byte[] name, byte[] hash)
        {
            this.BaseKey = hash.Concat(name);
        }

        public static List Find(byte[] name, byte[] hash)
        {
            return new List(name, hash);
        }

        public static List FindGlobal(byte[] name)
        {
            //return new List(name, ExecutionEngine.ExecutingScriptHash);
            return new List(name, global_prefix);
        }

        private byte[] CountKey()
        {
            return BaseKey.Concat(count_prefix);
        }

        private byte[] ElementKey(BigInteger index)
        {
            byte[] right;

            if (index == 0)
            {
                right = element_begin_prefix.Concat(new byte[] { 0 });
            }
            else
            {
                right = element_begin_prefix.Concat(index.AsByteArray());
            }

            right = right.Concat(element_end_prefix);

            return BaseKey.Concat(right);
        }

        public BigInteger Count()
        {
            return Storage.Get(Storage.CurrentContext, CountKey()).AsBigInteger();
        }

        public void Add(BigInteger element)
        {
            Add(element.AsByteArray());
        }

        public void Add(byte[] element)
        {
            var count = Count();
            Set(element, count);

            count = count + 1;
            Storage.Put(Storage.CurrentContext, CountKey(), count);
        }

        public void Set(byte[] element, BigInteger index)
        {
            var key = ElementKey(index);
            Storage.Put(Storage.CurrentContext, key, element);
        }

        public byte[] Get(BigInteger index)
        {
            var size = Count();
            if (index < 0 || index >= size)
            {
                return new byte[0];
            }

            var key = ElementKey(index);
            return Storage.Get(Storage.CurrentContext, key);
        }

        public void Delete(BigInteger index)
        {
            var size = Count();
            if (index < 0 || index >= size)
            {
                return;
            }

            var indexKey = ElementKey(index);

            size = size - 1;

            if (size > index)
            {
                var last = Get(size);
                Set(last, index);
            }

            var key = ElementKey(size);
            Storage.Delete(Storage.CurrentContext, key);

            Storage.Put(Storage.CurrentContext, CountKey(), size);
        }

        public object Range(BigInteger minIndex, BigInteger maxIndex)
        {
            if (minIndex > maxIndex)
            {
                Runtime.Notify("invalid min index");
                return new byte[0];
            }

            int total = 1 + (int)(maxIndex - minIndex);
            //Runtime.Notify("total", total);
            var result = new object[total];

            BigInteger offset = 0;
            BigInteger index = minIndex;
            while (offset < total)
            {
                //Runtime.Notify("fetching", index);
                var bytes = Get(index);
                result[(int)offset] = bytes;
                offset = offset + 1;
                index = index + 1;

                //Runtime.Notify("got", bytes);
            }
            return result;
        }

    }
}
