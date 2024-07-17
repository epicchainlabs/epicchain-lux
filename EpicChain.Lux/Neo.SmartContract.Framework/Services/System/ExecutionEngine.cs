namespace Neo.SmartContract.Framework.Services.System
{
    public static class ExecutionEngine
    {
        public static IScriptContainer ScriptContainer = null;

        public static byte[] ExecutingScriptHash = null;

        public static byte[] CallingScriptHash = null;

        public static byte[] EntryScriptHash = null;
    }
}
