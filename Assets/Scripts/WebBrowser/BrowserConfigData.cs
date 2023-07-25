using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// This class is used to set up the browser configuration save data
/// for each set of web pages in a given data set
/// </summary>
[System.Serializable]
public class BrowserConfigData
{
    public string startingURL;
    public Vector3 startingPosition;
    public Quaternion startingRotation;
    public Vector3 startingScale;

}
