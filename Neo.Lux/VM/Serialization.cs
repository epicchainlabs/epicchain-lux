using Neo.Lux.Utils;
using Neo.Lux.VM.Types;
using System;
using System.IO;
using System.Numerics;
using VMArray = Neo.Lux.VM.Types.Array;
using VMBoolean = Neo.Lux.VM.Types.Boolean;

namespace Neo.Lux.VM
{
    internal enum StackItemType : byte
    {
        ByteArray = 0x00,
        Boolean = 0x01,
        Integer = 0x02,
        InteropInterface = 0x40,
        Array = 0x80,
        Struct = 0x81,
        Map = 0x82,
    }

    public static class Serialization
    {
        public static void SerializeStackItem(StackItem item, BinaryWriter writer)
        {
            if (item is ByteArray)
            {
                writer.Write((byte)StackItemType.ByteArray);
                writer.WriteVarBytes(item.GetByteArray());
            }
            else
            if (item is VMBoolean)
            {
                writer.Write((byte)StackItemType.Boolean);
                writer.Write(item.GetBoolean());
            }
            else
            if (item is Integer)
            {
                writer.Write((byte)StackItemType.Integer);
                writer.WriteVarBytes(item.GetByteArray());
            }
            else
            if (item is InteropInterface)
            {
                throw new NotSupportedException();
            }
            else
            if (item is VMArray)
            {
                var array = (VMArray)item;
                if (array is Struct)
                    writer.Write((byte)StackItemType.Struct);
                else
                    writer.Write((byte)StackItemType.Array);
                writer.WriteVarInt(array.Count);
                foreach (StackItem subitem in array)
                    SerializeStackItem(subitem, writer);
            }
            else
            if (item is Map)
            {
                var map = (Map)item;
                writer.Write((byte)StackItemType.Map);
                writer.WriteVarInt(map.Count);
                foreach (var pair in map)
                {
                    SerializeStackItem(pair.Key, writer);
                    SerializeStackItem(pair.Value, writer);
                }
            }
        }

        public static StackItem DeserializeStackItem(byte[] bytes)
        {
            using (var stream = new MemoryStream(bytes))
            {
                using (var reader = new BinaryReader(stream))
                {
                    return DeserializeStackItem(reader);
                }
            }
        }

        public static StackItem DeserializeStackItem(BinaryReader reader)
        {
            StackItemType type = (StackItemType)reader.ReadByte();
            switch (type)
            {
                case StackItemType.ByteArray:
                    return new ByteArray(reader.ReadVarBytes());
                case StackItemType.Boolean:
                    return new VMBoolean(reader.ReadBoolean());
                case StackItemType.Integer:
                    return new Integer(new BigInteger(reader.ReadVarBytes()));
                case StackItemType.Array:
                case StackItemType.Struct:
                    {
                        VMArray array = type == StackItemType.Struct ? new Struct() : new VMArray();
                        ulong count = reader.ReadVarInt();
                        while (count-- > 0)
                            array.Add(DeserializeStackItem(reader));
                        return array;
                    }
                case StackItemType.Map:
                    {
                        Map map = new Map();
                        ulong count = reader.ReadVarInt();
                        while (count-- > 0)
                        {
                            StackItem key = DeserializeStackItem(reader);
                            StackItem value = DeserializeStackItem(reader);
                            map[key] = value;
                        }
                        return map;
                    }
                default:
                    throw new FormatException();
            }
        }
    }
}
