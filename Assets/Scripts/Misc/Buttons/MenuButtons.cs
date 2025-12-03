using UnityEngine;
using UnityEngine.SceneManagement;

public class MenuButtons : MonoBehaviour
{
    public void GoToPlayGameScene()
    {
        SceneManager.LoadScene("Scenes/SampleScene");
    }

    public void GoToSettingsScene()
    {
        SceneManager.LoadScene("Scenes/Settings");
    }

    public void Exit()
    {
        SceneManager.LoadScene("");
    }

}
