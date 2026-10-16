using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Intera_Get : MonoBehaviour
{
   
    private float MainVolume = 1.0f;
    //private bool isHUD = true;
    private float GameSpeed = 1.0f;

    public static Intera_Get Instance { get; private set; }
    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        DontDestroyOnLoad(gameObject);
    }

    //private void ChangeHUD()
    //{
    //    isHUD = isHUD ? false : true;
    //    if (!isHUD)
    //        
    //}

    public void SetMainVolume(float value)
    {
        MainVolume = value;
        AudioListener.volume = value;
        Debug.Log(MainVolume);
    }

    public void SetGameSpeed(float value)
    {
        if (value <= 0.5)
            GameSpeed = value + 0.5f;
        else
            GameSpeed = value * 2f;
        Time.timeScale = GameSpeed;
        Debug.Log(GameSpeed);
    }

}
