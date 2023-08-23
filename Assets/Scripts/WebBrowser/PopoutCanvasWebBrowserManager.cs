using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

using CellexalVR.AnalysisObjects;
using CellexalVR.General;
using Vuplex.WebView;

public class PopoutCanvasWebBrowserManager : FullCanvasWebBrowserManager
{
    // This class only uses a small subset of the FullCanvasWebBrowserPrefab
    //  - the controls for clicking on the main window of the popout and the close button
    public int parentID;
    public int popoutID;

    /// <summary>
    /// Start is called before the first frame update to set up this object
    /// </summary>
    async void Start()
    {
        // grab the reference manager
        referenceManager = GameObject.Find("InputReader").GetComponent<ReferenceManager>();

        // find the web manager script for adding other windows
        webManagerScript = GameObject.Find("WebManager").GetComponent<WebManager>();

        // grab the interactable script from this prefab for sending messages to other clients on position
        interactable = GetComponent<XRGrabInteractable>();

        // wait for the main web canvas to initialize
        await (_canvasWebViewPrefab.WaitUntilInitialized());

        // testing to see if I can get input to work manually
        CellexalEvents.RightTriggerClick.AddListener(OnTriggerClick);
        CellexalEvents.RightTriggerPressed.AddListener(OnTriggerPressed);
        CellexalEvents.RightTriggerUp.AddListener(OnTriggerUp);

        // testing pointer down/up events?
        webViewWithPointerDownUp = _canvasWebViewPrefab.WebView as IWithPointerDownAndUp;
        webViewWithMoveablePointer = _canvasWebViewPrefab.WebView as IWithMovablePointer;

    } // end Start
}
