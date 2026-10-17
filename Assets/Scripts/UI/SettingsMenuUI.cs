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
    public void OnSpeedChanged(float value)
    {
        Intera_Get.Instance.SetGameSpeed(value);
        Debug.Log(Intera_Get.Instance.GameSpeed);
    }
}
