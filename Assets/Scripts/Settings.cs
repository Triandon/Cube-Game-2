using System;
using UnityEngine;

public class Settings : MonoBehaviour
{
    public static Settings Instance { get; private set; }
    
    public string userName;
    public int lodDistance = 32;

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }
}
