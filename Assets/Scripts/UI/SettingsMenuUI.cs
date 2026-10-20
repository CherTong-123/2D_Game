using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SettingsMenuUI : MonoBehaviour
{
    public void MainVolumeChanged(float value)
    {
        Intera_Get.Instance.SetMainVolume(value);
        Debug.Log(Intera_Get.Instance.MainVolume);
    }
    public void BGMVolumeChanged(float value)
    {
        AudioManager.Instance.SetMusicVolume(value);
        Debug.Log("BGM音量: " + value);
    }
    public void SFXVolumeChanged(float value)
    {
        AudioManager.Instance.SetSFXVolume(value);
        Debug.Log("音效音量: " + value);
    }
    public void OnSpeedChanged(float value)
    {
        Intera_Get.Instance.SetGameSpeed(value);
        Debug.Log(Intera_Get.Instance.GameSpeed);
    }
}
