using TMPro;
using UnityEngine;

public class SettingsManager : MonoBehaviour
{
    private Settings settings;
    
    public TextMeshProUGUI userNameDisplayText;
    public TMP_InputField userNameChatBox;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        settings = Settings.Instance;

        UpdateUserNameDisplay();
    }

    // Update is called once per frame
    void Update()
    {
        if (userNameChatBox.text != "")
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                UpdateUserName(userNameChatBox.text);
                userNameChatBox.text = "";
            }
        }
    }

    public void SubmitUserNameChanges()
    {
        UpdateUserName(userNameChatBox.text);
        userNameChatBox.text = "";
    }

    public void UpdateUserName(string userName)
    {
        settings.userName = userName;
        UpdateUserNameDisplay();
    }

    public void UpdateUserNameDisplay()
    {
        userNameDisplayText.text = "Username: " + settings.userName;
    }
}
