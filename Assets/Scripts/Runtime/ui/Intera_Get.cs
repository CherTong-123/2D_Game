using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Intera_Get
{
    private static Intera_Get _instance;
    public static Intera_Get Instance
    {
        get
        {
            if (_instance == null)
                _instance = new Intera_Get();
            return _instance;
        }
    }
    public float MainVolume = 1.0f;
    //private bool isHUD = true;
    public float GameSpeed = 1.0f;
    private Intera_Get() { }



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
    }

    public void SetGameSpeed(float value)
    {
        if (value <= 0.5)
            GameSpeed = value + 0.5f;
        else
            GameSpeed = value * 2f;
        Time.timeScale = GameSpeed;
    }
}
