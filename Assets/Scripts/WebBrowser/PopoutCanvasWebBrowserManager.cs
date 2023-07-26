using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

using CellexalVR.AnalysisObjects;
using CellexalVR.General;

public class PopoutCanvasWebBrowserManager : FullCanvasWebBrowserManager
{
    // This class only uses a small subset of the FullCanvasWebBrowserPrefab
    //  - the controls for clicking on the main window of the popout and the close button
    public int parentID;

    /// <summary>
    /// Start is called before the first frame update to set up this object
    /// </summary>
    void Start()
    {
        // grab the reference manager
        referenceManager = GameObject.Find("InputReader").GetComponent<ReferenceManager>();

        // find the web manager script for adding other windows
        webManagerScript = GameObject.Find("WebManager").GetComponent<WebManager>();

        // grab the interactable script from this prefab for sending messages to other clients on position
        interactable = GetComponent<XRGrabInteractable>();

        // testing to see if I can get input to work manually
        CellexalEvents.RightTriggerClick.AddListener(OnTriggerClick);
        CellexalEvents.RightTriggerUp.AddListener(OnTriggerUp);

    } // end Start
}
