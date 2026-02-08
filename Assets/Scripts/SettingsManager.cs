using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SettingsManager : MonoBehaviour
{
    private Settings settings;
    
    public TextMeshProUGUI userNameDisplayText, currentLodDistanceText;
    public TMP_InputField userNameChatBox;
    [SerializeField] private Slider slider;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        settings = Settings.Instance;
        
        UpdateUserNameDisplay();

        slider.value = settings.lodDistance;
        
        slider.onValueChanged.AddListener((v) =>
        {
            currentLodDistanceText.text = v.ToString();
            settings.lodDistance = (int)v;
        });
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

        currentLodDistanceText.text = settings.lodDistance.ToString();
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
