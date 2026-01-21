using System.Collections.Generic;
using Core.Item;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class Chat : MonoBehaviour
{
	public string userName;
	
	public int maxMessages = 32;

	public GameObject chatPanel, textObjet;
	public TMP_InputField chatBox;

	public Color playerMessage, info;
	
	[SerializeField]private List<Message> messageList = new List<Message>();

	private Settings settings;
	[SerializeField] private PlayerInventoryHolder playerHolder;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
	    settings = Settings.Instance;
	    if (settings != null)
	    {
		    userName = settings.userName;
	    }
	    else
	    {
		    userName = playerHolder.GetInventoryName();
	    }
    }

    // Update is called once per frame
    void Update()
    {
	    if (chatBox.text != "")
	    {
		    if (Input.GetKeyDown(KeyCode.Return))
		    {
			    if (chatBox.text.StartsWith("/"))
			    {
				    ChatCommands.HandleCommand(chatBox.text,this);
			    }
			    SendMessageToChat(userName + ": "+ chatBox.text,Message.MessageType.playerMessage);
			    chatBox.text = "";
		    }
	    }
	    else
	    {
		    if(!chatBox.isFocused && Input.GetKeyDown(KeyCode.Return))
			    chatBox.ActivateInputField();
	    }
	    
	    if (!chatBox.isFocused)
	    {
		    if (Input.GetKeyDown(KeyCode.B))
		    {
			    SendMessageToChat("You pressed B!",Message.MessageType.info);
		    }
	    }
	    
    }

    public void SendMessageToChat(string text, Message.MessageType messageType)
    {
	    if (messageList.Count >= maxMessages)
	    {
		    Destroy(messageList[0].textObject.gameObject);
		    messageList.Remove(messageList[0]);
	    }
	    Message newMessage = new Message();
	    newMessage.text = text;

	    GameObject newText = Instantiate(textObjet, chatPanel.transform);
	    newMessage.textObject = newText.GetComponent<TextMeshProUGUI>();

	    newMessage.textObject.text = newMessage.text;
	    newMessage.textObject.color = MessageTypeColor(messageType);
	    
	    messageList.Add(newMessage);
    }
    
    Color MessageTypeColor(Message.MessageType messageType)
    {
	    Color color = info;

	    switch (messageType)
	    {
		    case Message.MessageType.playerMessage:
			    color = playerMessage;
			    break;
		    
		    case Message.MessageType.server:
			    color = info;
			    break;
		    
		    case Message.MessageType.owner:
			    color = info;
			    break;
		    
		    case Message.MessageType.warning:
			    color = info;
			    break;
	    }

	    return color;
    }
}

[System.Serializable]
public class Message
{
	public string text;
	public TextMeshProUGUI textObject;
	public MessageType messageType;
	
	public enum MessageType
	{
		playerMessage,
		info,
		server,
		owner,
		warning
	}
}
