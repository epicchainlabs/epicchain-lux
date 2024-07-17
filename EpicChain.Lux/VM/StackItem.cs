﻿using Neo.Lux.Core;
using Neo.Lux.VM.Types;
using Neo.SmartContract.Framework;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using Array = Neo.Lux.VM.Types.Array;
using Boolean = Neo.Lux.VM.Types.Boolean;

namespace Neo.Lux.VM
{
    public abstract class StackItem : IEquatable<StackItem>
    {
        public abstract bool Equals(StackItem other);

        public sealed override bool Equals(object obj)
        {
            if (obj == null) return false;
            if (obj == this) return true;
            if (obj is StackItem)
                return Equals((StackItem)obj);
            return false;
        }

        public static StackItem FromInterface(IApiInterface value)
        {
            return new InteropInterface(value);
        }

        public virtual BigInteger GetBigInteger()
        {
            var temp = GetByteArray();
            if (temp == null)
            {
                return 0;
            }
            return new BigInteger(temp);
        }

        public virtual bool GetBoolean()
        {
            var temp = GetByteArray();
            if (temp == null)
            {
                return false;
            }
            return temp.Any(p => p != 0);
        }

        public abstract byte[] GetByteArray();

        public override int GetHashCode()
        {
            unchecked
            {
                int hash = 17;
                foreach (byte element in GetByteArray())
                    hash = hash * 31 + element;
                return hash;
            }
        }

        public virtual string GetString()
        {
            var temp = GetByteArray();
            return (temp != null && temp.Length > 0 ? Encoding.UTF8.GetString(GetByteArray()) : null);
        }

        public virtual byte GetByte()
        {
            var temp = GetByteArray();
            return (byte)(temp!=null && temp.Length > 0 ? temp[0] : 0);
        }

        public static implicit operator StackItem(int value)
        {
            return (BigInteger)value;
        }

        public static implicit operator StackItem(uint value)
        {
            return (BigInteger)value;
        }

        public static implicit operator StackItem(long value)
        {
            return (BigInteger)value;
        }

        public static implicit operator StackItem(ulong value)
        {
            return (BigInteger)value;
        }

        public static implicit operator StackItem(BigInteger value)
        {
            return new Integer(value);
        }

        public static implicit operator StackItem(bool value)
        {
            return new Boolean(value);
        }

        public static implicit operator StackItem(byte[] value)
        {
            return new ByteArray(value);
        }

        public static implicit operator StackItem(StackItem[] value)
        {
            return new Array(value);
        }

        public static implicit operator StackItem(List<StackItem> value)
        {
            return new Array(value);
        }

    }
}
