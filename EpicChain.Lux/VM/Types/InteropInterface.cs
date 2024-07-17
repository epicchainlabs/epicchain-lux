﻿using Neo.Lux.Core;
using Neo.SmartContract.Framework;
using System;

namespace Neo.Lux.VM.Types
{
    public class InteropInterface : StackItem
    {
        private IApiInterface _object;

        public InteropInterface(IApiInterface value)
        {
            this._object = value;
        }

        public override bool Equals(StackItem other)
        {
            if (ReferenceEquals(this, other)) return true;
            if (ReferenceEquals(null, other)) return false;
            InteropInterface i = other as InteropInterface;
            if (i == null) return false;
            return _object.Equals(i._object);
        }

        public override bool GetBoolean()
        {
            return _object != null;
        }

        public override byte[] GetByteArray()
        {
            throw new NotSupportedException();
        }

        public Type GetInterfaceType()
        {
            return _object.GetType();
        }

        public T GetInterface<T>() where T : class, IApiInterface
        {
            return _object as T;
        }
    }
}
