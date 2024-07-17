using System.Collections.Generic;

namespace Neo.SmartContract.Framework.Services.Neo
{
    public static class Blockchain
    {
        public static uint Height = 0;
        public static Dictionary<uint, Header> Headers = new Dictionary<uint, Header>();
        public static Dictionary<uint, Block> Blocks = new Dictionary<uint, Block>();

        public static uint GetHeight() => Height;

        public static Header GetHeader(uint height) => Headers[height];

        public static Header GetHeader(byte[] hash) { return null; }

        public static Block GetBlock(uint height) => Blocks[height];

        public static Block GetBlock(byte[] hash) { return null; }

        public static Transaction GetTransaction(byte[] hash) { return null; }

        public static Account GetAccount(byte[] script_hash) { return null; }

        public static byte[][] GetValidators() { return null; }

        public static Asset GetAsset(byte[] asset_id) { return null; }

        public static Contract GetContract(byte[] script_hash) { return null; }
    }
}
