<p align="center">
ad  <img
    src="http://res.cloudinary.com/vidsy/image/upload/v1503160820/CoZ_Icon_DARKBLUE_200x178px_oq0gxm.png"
    width="125px"
  >
</p>

<h1 align="center">EpicChain Lux</h1>

<p align="center">
  EpicChain light wallet / blockchain API for C#.
</p>

## Contents

- [Description](#description)
- [Compatibility](#compatibility)
- [Installation](#installation)
- [Usage](#usage)
- [Console Demo](#console-demo)
- [Light Wallet Demo](#light-wallet-demo)
- [Unity Support](#unity-support)
- [Transaction Listening](#transaction-listening)
- [Advanced operations](#advanced-operations)
- [TODO](#todo)
- [Credits and License](#credits-and-license)

---

## Compatibility

Platform 		| Status
:---------------------- | :------------
.NET framework 		| Working
UWP 			| Working
Mono 			| Working
Xamarin / Mobile 	| Untested
Unity 			| Working


## Installation

    PM> Install-Package EpicChainLux

# Usage

Import the package:

```c#
using EpicChain.Lux;
```

For invoking a Smart Contract, e.g.:

```c#
	var privKey = "XXXXXXXXXXXXXXXXprivatekeyhereXXXXXXXXXXX".HexToBytes();	 // can be any valid private key
	var myKeys = new KeyPair(privKey);
	var scriptHash = "de1a53be359e8be9f3d11627bcca40548a2d5bc1"; // the scriptHash of the smart contract you want to use	
	// for now, contracts must be in the format Main(string operation, object[] args)
	var api = EpicChainDB.ForMainNet();
	var result = api.CallContract(myKeys, scriptHash, "registerMailbox", new object[] { "ABCDE", "demo@phantasma.io" });
```

For transfering assets (EpicChain or EpicPulse), e.g.:

```c#
	var privKey = "XXXXXXXXXXXXXXXXprivatekeyhereXXXXXXXXXXX".HexToBytes();	 
	// can be any valid private key in raw format, for WIF use KeyPair.FromWIF
	var myKeys = new KeyPair(privKey);
	// WARNING: For now use test net only, this code is experimental, you could lose real assets if using main net
	var api = EpicChainDB.ForTestNet();
	var result = api.SendAsset("AanTL6pTTEdnphXpyPMgb7PSE8ifSWpcXU" /*destination address*/, "EpicPulse", 3 /*amount to send */ , myKeys);
```

For getting the balance of an address:

```c#
	var api = EpicChainDB.ForTestNet();
	var balances = api.GetBalancesOf("AYpY8MKiJ9q5Fpt4EeQQmoYRHxdNHzwWHk");
	foreach (var entry in balances)
	{
		Console.WriteLine(entry.Key + " => " + entry.Value);
	}
```

# XEP5 Token support

EpicChain-Lux allows to abstract interaction with EpicChain tokens via the XEP5 C# class.

Here's an example of interaction with a XEP5 token:

```c#
	var api = EpicChainDB.ForMainNet(); 
	var redPulse_token = api.GetToken("RPX");	
	Console.WriteLine($"{redPulse_token.Name} ({redPulse_token.Symbol})");
	Console.WriteLine($"Total Supply: {redPulse_token.TotalSupply} ({redPulse_token.Symbol})");
	
	// you can also request transfers of tokens
	var privKey = "XXXXXXXXXXXXXXXXprivatekeyhereXXXXXXXXXXX".HexToBytes();	 // can be any valid private key
	var myKeys = new KeyPair(privKey);
	redPulse_token.Transfer(myKeys, "AanTL6pTTEdnphXpyPMgb7PSE8ifSWpcXU" /*destination*/, 123 /*amount to send*/); 
```


```c#
	var api = EpicChainDB.ForMainNet(); 	
	var redPulse_contractHash = "ecc6b20d3ccac1ee9ef109af5a7cdb85706b1df9";
	var redPulse_token = new XEP5(api, redPulse_contractHash);
```	

# Console Demo

A console program is included to demonstrate common features:
+ Loading private keys
+ Obtaining wallet address from private key
+ Query balance from an address
+ Invoking a XEP5 Smart Contract (query symbol and total supply)


# Light Wallet Demo

A winforms demo is included to showcase how simple is to create a light wallet.

The light wallet demo is able to login into any wallet by inserting a private key in either raw format or WIF format, and also to transfer funds (EpicChain or EpicPulse) to any address.

Please beware of using this wallet to transfer funds in EpicChain main net, as this wallet was created just for demonstration purpose and not exhaustively tested.



# Unity Support

EpicChainLux can be used together with Unity to make games that interact with the EpicChain blockchain.
A Unity demo showcasing loading a EpicChain wallet and querying the balance is included.

Use caution, as most EpicChainLux methods are blocking calls; in Unity the proper way to call them is using [Coroutines](https://docs.unity3d.com/Manual/Coroutines.html).
```c#
    IEnumerator SyncBalance()
    {
        var balances = EpicChainAPI.GetBalance(EpicChainAPI.Net.Test, this.keys.address);
        this.balance = balances["EpicChain"];
    }
	
	// Then you call the method like this
	StartCoroutine(SyncBalance());
```

Note: If you get compilation errors in Unity uou will need to go to Settings -> Player and change the Scripting Runtime version to .NET 4.6 equivalent

## Using with Unity



You should now be able to hit play and see the demo in action! 

If you're still receiving errors in the Console window, you'll want to go to Unity's Build Settings > Player Settings > and set the API compatibility level to .NET 4.6.


# Airdrop / Snapshots

Latest versions of EpicChainLux have support for doing snapshots of the blockchain. This can be useful for example to find every wallet with a certain token balance at a certain date.
Running a full local node using epicchain-cli full synced is extremely recommended when using snapshot features.

The following code extracts all transactions related to a specific XEP5 token.
```c#            
	var api = new LocalRPCNode(10332, "http://epicscan.io");

	uint startBlock = 2313827;
	uint endBlock = 2320681;

	var token = api.GetToken("SOUL");
	var soul_tx = SnapshotTools.GetTokenTransactions(token, startBlock, endBlock);
	var soul_lines = new List<string>();
	foreach (var tx in soul_tx)
	{
		soul_lines.Add(tx.Hash+","+tx.Serialize().ByteToHex());
	}
	File.AppendAllLines("soul_txs.txt", soul_lines.ToArray());
```

# Transaction Listening

Many applications will need to react to certain transactions once they appear on the chain. The ScriptInspector class can be used for decoding contract calls along with their arguments.

```c#            
// First declare a BlockIterator
var iterator = new BlockIterator(api);
var targetContractHash = "AY9o94nWrUCEJ29UWhAodaJjQ16byjH852".AddressToScriptHash();
var token = new XEP5(api, targetContractHash);
var targetAddress = "AY9o94nWrUCEJ29UWhAodaJjQ16byjH852";

// You can listen for specific transactions of a specific type
var tx = api.WaitForTransaction(iterator, x => x.type == TransactionType.ContractTransaction);
if (tx != null) {
	Console.WriteLine($"Transaction {tx.Hash} is a asset transfer");
}

// You can listen for specific transactions arriving to a specific address 
var tx = api.WaitForTransaction(iterator, x => x.HasOutput(targetAddress));
if (tx != null) {
	Console.WriteLine($"Transaction {tx.Hash} was sent to {targetAddress}");
}

// You can listen for specific transactions coming from a specific address 
var tx = api.WaitForTransaction(iterator, x => x.HasInput(targetAddress));
if (tx != null) {
	Console.WriteLine($"Transaction {tx.Hash} was sent from {targetAddress}");
}

// You can listen for specific transactions that contain certain contract operations
var tx = api.WaitForTransaction(iterator, x => new ScriptInspector(x.script, targetContractHash).Any(y => y.operation == "transfer"));
if (tx != null) {
	Console.WriteLine($"Transaction {tx.Hash} contains transfer for {token.Name}");
	var inspector = new ScriptInspector(tx.script);
	foreach (var call in inspector.Calls) {
		if (call.operation == "transfer") {
			var from = new UInt160(call.arguments[0]);
			var to = new UInt160(call.arguments[1]);
			var amount = new BigInteger(call.arguments[2]);
			Console.WriteLine($"Transfer of {amount} from {from.ToAddress()} to {to.ToAddress()});
		}
	}
}

// You can listen for specific transactions that contain certain new contract deployments
var tx = api.WaitForTransaction(iterator, x => new ScriptInspector(x.script, targetContractHash).Deployments.Any());
if (tx != null) {
	Console.WriteLine($"Transaction {tx.Hash} deployed a new contract");
}
```

# Advanced operations

EpicChainLux supports some advanced transaction operations specially useful for those doing an ICO in EpicChain.

## Withdrawing EpicChain from an ICO contract address

After an ICO finishes, it is necessary to withdraw the received funds outside of the contract address. 
Now the problem is, for smart contracts the private key is not known, since the smart contract address is derived from the hash of it's own code.
EpicChainLux provides some methods to do withdrawals from smart contracts. Once the funds are moved to a normal address, you have full access to them.

```c#            
// first read the AVM bytecode from the disk. This AVM must be exactly the same deployed in the main net
var bytes = System.IO.File.ReadAllBytes(@"d:\code\crypto\my_ico\MyICOContract.avm");

// the team keys must match what is written in the contract. Usually only a single address will have withdraw permissions
var team_keys = EpicChain.Lux.Cryptography.KeyPair.FromWIF("XXXXXXXXXXXXXX");

var ICO_address = "AY9o94nWrUCEJ29UWhAodaJjQ16byjH852"; // this should be the contract address, the one where the sale funds are stored

// finally, execute the withdraw. It is recommended to first try withdrawing a small amount and check if the transaction gets accepted
var amount = 50;
var tx = api.WithdrawAsset(team_keys, ICO_address, "EpicChain", amount, bytes);
if (tx != null){
	Console.WriteLine("Unconfirmed tx " + tx.Hash);
}
else {
	Console.WriteLine("Sorry, transaction failed");
}
```

## Claiming EpicPulse from an ICO contract address

After an ICO finishes, if the sale received tons of EpicChain but you don't withdraw it right away, then the address will have a large amount of unclaimed EpicPulse.
With EpicChain it is possible to claim it, and later send it to another address using the withdraw method.

```c#            
// first read the AVM bytecode from the disk. This AVM must be exactly the same deployed in the main net
var bytes = System.IO.File.ReadAllBytes(@"d:\code\crypto\my_ico\MyICOContract.avm");

// the team keys must match what is written in the contract. Usually only a single address will have withdraw permissions
var team_keys = EpicChain.Lux.Cryptography.KeyPair.FromWIF("XXXXXXXXXXXXXX");

var ICO_address = "AY9o94nWrUCEJ29UWhAodaJjQ16byjH852"; // this should be the contract address, the one where the sale funds are stored

// execute the claim. Note that this operation is quite slow and can take several seconds to execute the transaction
var amount = 50;
var tx = api.ClaimEpicPulse(team_keys, ICO_address, bytes);
if (tx != null){
	Console.WriteLine("Unconfirmed tx " + txw.Hash);
}
else {
	Console.WriteLine("Sorry, transaction failed");
}
```

After the EpicPulse is claimed it is available and you can use api.WithdrawAsset() to move it to other address.
			
# Using with Main net

The recommended way to do it by specifying a list of nodes:

```c#            
var api = new RemoteRPCNode("http://epicscan.io", "mainnet1-seed.epic-chain.org:10111", "mainnet1-seed.epic-beacon-chain.org:10111");
```

# Credits and License

Created by Sérgio Flores (<http://lunarlabs.pt/>).
his project started as a port of code from their [EpicVault wallet](https://github.com/CityOfZion/epicchain-desktop) from Javascript to C#.
Of course, credits also go to the EpicChain team(<http://epic-chain.org>), as I also used some code from their [EpicChain source](https://github.com/epicchainlabs/epicchain).

This project is released under the MIT license, see `LICENSE.md` for more details.
