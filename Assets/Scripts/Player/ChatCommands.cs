using System.Collections.Generic;
using System.Text;
using Core.Block;
using Core.Item;
using UnityEngine;

public static class ChatCommands
{
    public static void HandleCommand(string input, Chat chat)
    {
	    // Remove the leading "/"
	    string commandLine = input.Substring(1);

	    // Split by spaces
	    string[] args = commandLine.Split(' ');

	    if (args.Length == 0)
		    return;

	    string command = args[0].ToLower();

	    switch (command)
	    {
		    case "help":
			    HandleHelpCommand(args, chat);
			    break;
		    
		    case "give":
			    HandleGiveCommand(args, chat);
			    break;

		    case "ids":
			    HandleIdsCommand(args, chat);
			    break;
		    
		    default:
			    chat.SendMessageToChat("Unknown command.", Message.MessageType.warning);
			    break;
	    }
    }

    private static void HandleHelpCommand(string[] args, Chat chat)
    {
	    // Expected: /help
	    
	    chat.SendMessageToChat("Here is the command lists!:\n" +
	                           "/give <player> <itemId> <amount>\n" +
	                           "/ids", Message.MessageType.info);
    }

    private static void HandleGiveCommand(string[] args, Chat chat)
    {
	    // Expected: /give player itemId amount
	    if (args.Length != 4)
	    {
		    chat.SendMessageToChat("Usage: /give <player> <itemId> <amount>", Message.MessageType.warning);
		    return;
	    }

	    string targetPlayer = args[1];
	    string itemId = args[2];

	    if (!int.TryParse(args[3], out int amount))
	    {
		    chat.SendMessageToChat("Amount must be a number.", Message.MessageType.warning);
		    return;
	    }
	    
	    // Run your actual game logic here
	    GiveItem(targetPlayer, itemId, amount,chat);

	    chat.SendMessageToChat(
		    $"Gave {amount} of {itemId} to {targetPlayer}.",
		    Message.MessageType.server
	    );
    }

    private static void GiveItem(string targetPlayer, string itemIdString, int amount, Chat chat)
    {
	    if (!int.TryParse(itemIdString, out int itemId))
	    {
		    chat.SendMessageToChat("ItemId must be a number.", Message.MessageType.warning);
		    return;
	    }

	    InventoryHolder[] holders = Object.FindObjectsOfType<InventoryHolder>();
	    InventoryHolder target = null;

	    foreach (var h in holders)
	    {
		    if (h.GetInventoryName() == targetPlayer)
		    {
			    target = h;
			    break;
		    }
	    }

	    if (target == null)
	    {
		    chat.SendMessageToChat($"Player '{targetPlayer}' not found.", Message.MessageType.warning);
		    return;
	    }

	    Item item = ItemRegistry.GetItem(itemId);
	    if (item == null)
	    {
		    chat.SendMessageToChat($"Item with ID {itemId} does not exist.", Message.MessageType.warning);
		    return;
	    }

	    bool success = target.Inventory.AddItem(item.id, amount, item.itemName, null);

	    if (!success)
	    {
		    chat.SendMessageToChat($"{targetPlayer}'s inventory is full.", Message.MessageType.warning);
	    }
    }

    private static void HandleIdsCommand(string[] args, Chat chat)
    {
	    // Expected: /ids
	    List<Block> allBlocks = new List<Block>(BlockRegistry.GetAllBlocks());
	    List<Item> allItems = new List<Item>(ItemRegistry.getAllItems());

	    StringBuilder sb = new StringBuilder();

	    sb.AppendLine("=== Blocks ===");
	    foreach (Block block in allBlocks)
	    {
		    sb.AppendLine($"{block.blockName} : {block.id}");
	    }

	    sb.AppendLine();
	    sb.AppendLine("=== Items ===");
	    foreach (Item item in allItems)
	    {
		    sb.AppendLine($"{item.itemName} : {item.id}");
	    }

	    chat.SendMessageToChat(sb.ToString(), Message.MessageType.info);
    }

}
