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
		    case "give":
			    HandleGiveCommand(args, chat);
			    break;

		    default:
			    chat.SendMessageToChat("Unknown command.", Message.MessageType.warning);
			    break;
	    }
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

	    if (!InventoryHolder.Holders.TryGetValue(targetPlayer, out InventoryHolder holder))
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

	    bool success = holder.Inventory.AddItem(item.id, amount, item.itemName);

	    if (!success)
	    {
		    chat.SendMessageToChat($"{targetPlayer}'s inventory is full.", Message.MessageType.warning);
	    }
    }
}
