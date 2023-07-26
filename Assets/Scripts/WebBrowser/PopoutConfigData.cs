using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is used to set up the popout configuration save data
/// for each set of web page popouts in a given data set
/// </summary>
[System.Serializable]
public class PopoutConfigData
{
    public string startingPopoutMessage;
    public Vector3 startingPosition;
    public Quaternion startingRotation;
    public Vector3 startingScale;
}
