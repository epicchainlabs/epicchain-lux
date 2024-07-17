using Neo.Lux.Core;
using Neo.Lux.Cryptography;
using Neo.Lux.VM;
using Neo.Lux.Emulator;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;

namespace Neo.Lux.Utils
{
    public static class FormattingUtils
    {
        public static string StackItemAsString(StackItem item, bool addQuotes = false, VMType hintType = VMType.Unknown)
        {
            if (item is ICollection)
            {
                var bytes = item.GetByteArray();
                if (bytes != null && bytes.Length == 20)
                {
                    var signatureHash = new UInt160(bytes);
                    return CryptoUtils.ToAddress(signatureHash);
                }

                var s = new StringBuilder();
                var items = (ICollection)item;

                s.Append('[');
                int i = 0;
                foreach (StackItem element in items)
                {
                    if (i > 0)
                    {
                        s.Append(',');
                    }
                    s.Append(StackItemAsString(element));

                    i++;
                }
                s.Append(']');


                return s.ToString();
            }

            if (item is VM.Types.Boolean && hintType == VMType.Unknown)
            {
                return item.GetBoolean().ToString();
            }

            if (item is VM.Types.Integer && hintType == VMType.Unknown)
            {
                return item.GetBigInteger().ToString();
            }

            if (item is VM.Types.InteropInterface)
            {
                var type = ((VM.Types.InteropInterface)item).GetInterfaceType();
                return $"{type.Name}";
            }

            byte[] data = null;

            try
            {
                data = item.GetByteArray();
            }
            catch
            {

            }

            if ((data == null || data.Length == 0) && hintType == VMType.Unknown)
            {
                return "Null";
            }

            if (hintType == VMType.Array)
            {
                var s = new StringBuilder();
                s.Append('[');
                int count = 0;
                if (data.Length > 0)
                {
                    var array = (VM.Types.Array)item;
                    foreach (var entry in array)
                    {
                        if (count > 0)
                        {
                            s.Append(", ");
                        }
                        count++;

                        s.Append(StackItemAsString(entry, addQuotes, VMType.Unknown));
                    }
                }
                s.Append(']');

                return s.ToString();
            }

            return FormattingUtils.OutputData(data, addQuotes, hintType);
        }

        private enum ContractParameterTypeLocal : byte
        {
            Signature = 0,
            Boolean = 1,
            Integer = 2,
            Hash160 = 3,
            Hash256 = 4,
            ByteArray = 5,
            PublicKey = 6,
            String = 7,
            Array = 16,
            InteropInterface = 240,
            Void = 255
        };

        public static string OutputData(byte[] data, bool addQuotes, VMType hintType = VMType.Unknown)
        {
            if (data == null)
            {
                return "Null";
            }

            var dataLen = data.Length;

            if (hintType != VMType.Unknown)
            {
                switch (hintType)
                {
                    case VMType.String:
                        {
                            var val = System.Text.Encoding.UTF8.GetString(data);
                            if (addQuotes)
                            {
                                val = '"' + val + '"';
                            }
                            return val;
                        }

                    case VMType.Boolean:
                        {
                            return (data != null && data.Length > 0 && data[0] != 0) ? "True" : "False";
                        }

                    case VMType.Integer:
                        {
                            return new BigInteger(data).ToString();
                        }
                }
            }

            for (int i = 0; i < dataLen; i++)
            {
                var c = (char)data[i];


                var isValidText = char.IsLetterOrDigit(c) || char.IsPunctuation(c) || char.IsWhiteSpace(c)
                                                            || "!@#$%^&*()-=_+[]{}|;':,./<>?".Contains(c.ToString());
                if (!isValidText)
                {
                    string prefix = null;

                    if (data.Length == 23)
                    {
                        prefix = Encoding.ASCII.GetString(data.Take(3).ToArray());
                        data = data.Skip(3).ToArray();
                    }

                    if (data.Length == 20)
                    {
                        var signatureHash = new UInt160(data);
                        var output = CryptoUtils.ToAddress(signatureHash);
                        if (prefix != null)
                        {
                            output = prefix + "." + output;
                        }
                        return output;
                    }

                    return data.ByteToHex();
                }
            }

            var result = System.Text.Encoding.ASCII.GetString(data);

            if (addQuotes)
            {
                result = '"' + result + '"';
            }

            return result;
        }

        // Separate() from: https://stackoverflow.com/questions/9755090/split-a-byte-array-at-a-delimiter
        public static byte[][] ByteArraySplit(byte[] source, byte[] separator)
        {
            var parts = new List<byte[]>();
            var index = 0;
            byte[] part;
            for (var i = 0; i < source.Length; ++i)
            {
                if (Equals(source, separator, i))
                {
                    part = new byte[i - index];
                    Array.Copy(source, index, part, 0, part.Length);
                    parts.Add(part);
                    index = i + separator.Length;
                    i += separator.Length - 1;
                }
            }
            part = new byte[source.Length - index];
            Array.Copy(source, index, part, 0, part.Length);
            parts.Add(part);
            return parts.ToArray();
        }

        // https://stackoverflow.com/questions/9755090/split-a-byte-array-at-a-delimiter
        private static bool Equals(byte[] source, byte[] separator, int index)
        {
            for (int i = 0; i < separator.Length; ++i)
            {
                if (index + i >= source.Length || source[index + i] != separator[i]) return false;
            }
            return true;
        }
    }
}
