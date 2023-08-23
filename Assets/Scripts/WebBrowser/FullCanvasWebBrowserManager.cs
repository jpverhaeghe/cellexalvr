using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using Vuplex.WebView;
using TMPro;
using CellexalVR.AnalysisObjects;
using CellexalVR.General;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit;
using System;
using System.Collections.Generic;

public class FullCanvasWebBrowserManager : MonoBehaviour
{
    private const int TIME_TO_WAIT_FOR_MESSAGE = 5;

    // Canvas prefabs in the resource folder for Vulpex WebView
    [Header("Prefabs for use in the browser window script")]
    [SerializeField] public CanvasWebViewPrefab _canvasWebViewPrefab;
    [SerializeField] CanvasWebViewPrefab _controlsWebViewPrefab;
    //[SerializeField] CanvasKeyboard _keyboard;
    [Header("Input field and cursor icon used by the browser window")]
    [SerializeField] public TMP_InputField urlInputField;
    [SerializeField] TMP_Text messageText;
    [SerializeField] public GameObject cursorIcon;

    // other public variables used by the browser window
    public int browserID;
    public bool activeBrowser;                      // to let the cursor system know if it should move

    // Popout data for this browser, used by web manager to save/load browser layouts
    [HideInInspector] public Dictionary<int, PopoutConfigData> popoutWindowData;
    [HideInInspector] public List<PopoutConfigData> loadedPopouts;
    [HideInInspector] public PopoutConfigData tempPopoutConfigData;
    [HideInInspector] public int lastPopoutID;
    [HideInInspector] public int currentPopoutID;

    // for click and drag events
    [HideInInspector] public IWithPointerDownAndUp webViewWithPointerDownUp;
    [HideInInspector] public IWithMovablePointer webViewWithMoveablePointer;

    // used by sub-classes
    protected WebManager webManagerScript;
    protected ReferenceManager referenceManager;
    protected XRGrabInteractable interactable;

    // private fields for this script
    private IWithPopups webViewWithPopups;
    private Vector2 previousPixelUV;
    private bool dragging;

    /// <summary>
    /// Sets up all the variables, etc. used by this script when loaded for the first time
    /// </summary>
    async void Start()
    {
        // grab the reference manager
        referenceManager = GameObject.Find("InputReader").GetComponent<ReferenceManager>();

        // find the web manager script for adding other windows
        webManagerScript = GameObject.Find("WebManager").GetComponent<WebManager>();

        // grab the interactable script from this prefab for sending messages to other clients on position
        interactable = GetComponent<XRGrabInteractable>();

        // set the cursor icon as not active by default, clicking the main window of a browser prefab will activate it
        cursorIcon.SetActive(false);

        // Jim - attempting to create a CanvasWebViewPrefab for the controls
        _controlsWebViewPrefab.NativeOnScreenKeyboardEnabled = false;

        // Create a CanvasWebViewPrefab for the main window
        // https://developer.vuplex.com/webview/CanvasWebViewPrefab
        _canvasWebViewPrefab.NativeOnScreenKeyboardEnabled = false;

        // Wait for the prefabs to initialize because the WebView property of each is null until then.
        // https://developer.vuplex.com/webview/WebViewPrefab#WaitUntilInitialized
        await Task.WhenAll(new Task[] {
               _canvasWebViewPrefab.WaitUntilInitialized(),
               _controlsWebViewPrefab.WaitUntilInitialized()
            });

        // Now that the WebViewPrefabs are initialized, we can use the IWebView APIs via its WebView property.
        // https://developer.vuplex.com/webview/IWebView
        _canvasWebViewPrefab.WebView.UrlChanged += (sender, eventArgs) => {
            _setDisplayedUrl(eventArgs.Url);
            // Refresh the back / forward button state after 1 second.
            Invoke("_refreshBackForwardState", 1);
        };

        // After the prefab has initialized, you can use the IWebView APIs via its WebView property.
        // https://developer.vuplex.com/webview/IWebView
        _canvasWebViewPrefab.WebView.LoadUrl("https://google.com");

        // set up the popout list for this window
        popoutWindowData = new Dictionary<int, PopoutConfigData>();
        lastPopoutID = 0;

        // set up a listener for messages so we can save the popout data
        _canvasWebViewPrefab.WebView.MessageEmitted += MainCanvasOnMessageReceived;

        _controlsWebViewPrefab.WebView.MessageEmitted += Controls_MessageEmitted;
        _controlsWebViewPrefab.WebView.LoadHtml(CONTROLS_HTML);

        // Android Gecko and UWP w/ XR enabled don't support transparent webviews, so set the cutout
        // rect to the entire view so that the shader makes its black background pixels transparent.
        var pluginType = _controlsWebViewPrefab.WebView.PluginType;
        if (pluginType == WebPluginType.AndroidGecko || pluginType == WebPluginType.UniversalWindowsPlatform)
        {
            _controlsWebViewPrefab.SetCutoutRect(new Rect(0, 0, 1, 1));
        }

        // After the prefab has initialized, you can use the IWithPopups API via its WebView property.
        // https://developer.vuplex.com/webview/IWithPopups
        webViewWithPopups = _canvasWebViewPrefab.WebView as IWithPopups;
        if (webViewWithPopups == null)
        {
            // if the system can't handle pop-ups, let the user know
            _canvasWebViewPrefab.WebView.LoadHtml(NOT_SUPPORTED_HTML);
        }
        else 
        {

            // now attemtping to load pop-up mode into a new window
            webViewWithPopups.SetPopupMode(PopupMode.LoadInNewWebView);

            // set the message handler for the popup
            webViewWithPopups.PopupRequested += CreatePopoutWindow;
        }

        // testing to see if I can get input to work manually
        CellexalEvents.RightTriggerClick.AddListener(OnTriggerClick);
        CellexalEvents.RightTriggerPressed.AddListener(OnTriggerPressed);
        CellexalEvents.RightTriggerUp.AddListener(OnTriggerUp);

        // debugging log messages
        _canvasWebViewPrefab.WebView.ConsoleMessageLogged += (sender, eventArgs) => 
        {
            Debug.Log($"Console message logged: [{eventArgs.Level}] {eventArgs.Message}" + "from window: " + browserID);
        };

        // lastly, if this is the first window (ID 0) then activate it
        webManagerScript.ActivateBrowser(0);

        // testing pointer down/up events?
        webViewWithPointerDownUp = _canvasWebViewPrefab.WebView as IWithPointerDownAndUp;
        webViewWithMoveablePointer = _canvasWebViewPrefab.WebView as IWithMovablePointer;

    } // end Start

    /// <summary>
    /// Updates the URL being displayed with the data in the text window. 
    /// It doesn't check for errors, so assume it will fail if a website is entered incorrectly
    /// </summary>
    public void UpdateUrl()
    {
        _canvasWebViewPrefab.WebView.LoadUrl(urlInputField.text);
        _setDisplayedUrl(urlInputField.text);

    } // end UpdateUrl

    /// <summary>
    /// Creates a new browswer window
    /// </summary>
    public void CreateNewWindow()
    {
        // only create a new window if the web manager exists
        if (webManagerScript != null)
        {
            webManagerScript.CreateNewWindow(gameObject.transform, urlInputField.text);
        }
        else
        {
            Debug.Log("No web manager, so can't add a new browser window");
        }

    } // end CreateNewWindow

    /// <summary>
    /// Closes the window by destroying the game object associated with it
    /// </summary>
    public void CloseWindow()
    {
        // only close the window if the web manager exists
        if (webManagerScript != null)
        {
            // send a request to close the window (mainly for popouts)
            IWebView webView = 
                gameObject.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>().WebView;

            webView.ExecuteJavaScript("window.close()", null);
            webManagerScript.RemoveBrowserWindowFromScene(gameObject);
        }
        else
        {
            Debug.Log("No web manager, so can't add a new browser window");
        }

    } // end CloseWindow

    /// <summary>
    /// Refreshes the browser window
    /// </summary>
    public void RefreshWindow()
    {
        IWebView webView =
            gameObject.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>().WebView;

        // TODO: Should this clear the popout windows as well?

        webView.Reload();

    } // end RefreshWindow

    /// <summary>
    /// Turns on/off graphs for this data set - assumes referenceManager script is active
    /// </summary>
    public void ToggleGraphs()
    {
        Debug.Log("Toggle Graph button activated!");

        if (referenceManager != null)
        {
            referenceManager.graphManager.ToggleGraphs();
        }
        else
        {
            Debug.Log("No web manager, so can't add a new browser window");
        }

    } // ToggleGraphs

    /// <summary>
    /// Saves the current layout of all web browser windows - assumes webManager script is active
    /// </summary>
    public void SaveBrowserLayout()
    {
        Debug.Log("Save browser layout button activated!");

        if (webManagerScript != null)
        {
            webManagerScript.SaveBrowserSession();
            messageText.text = "Browser layout has been saved!";
            Invoke("ClearMessageText", TIME_TO_WAIT_FOR_MESSAGE);
        }
        else
        {
            Debug.Log("No web manager, so can't add a new browser window");
        }

    } // SaveBrowserLayout

    /// <summary>
    /// Removes the popout window from this browser window dictionary with the given id
    /// </summary>
    /// <param name="popoutID">the popout window id for this window</param>
    public void RemovePopoutWindow(int popoutID)
    {
        PopoutConfigData popoutData;

        if (popoutWindowData.TryGetValue(popoutID, out popoutData))
        {
            Debug.Log("Removing popout " + popoutData.startingPopoutMessage + " for window " + browserID);
            popoutWindowData.Remove(popoutID);
        }
        else
        {
            Debug.Log("Popout widow did not exist in this window");
        }

    } // end RemovePopoutWindow

    /// <summary>
    /// Update the other windows in multi-player
    /// TODO: Test this to make sure it works, may need more calls for other browser actions
    /// </summary>
    protected void Update()
    {
        // Open XR - update other clients
        if (interactable.isSelected)
        {
            referenceManager.multiuserMessageSender.SendMessageMoveBrowser(transform.localPosition, transform.localRotation, transform.localScale);
        }

    } // end Update

    /// <summary>
    /// Refreshes the back and forward button states that are controlled by JAVAScript
    /// </summary>
    async void _refreshBackForwardState()
    {
        // Get the main webview's back / forward state and then post a message
        // to the controls UI to update its buttons' state.
        var canGoBack = await _canvasWebViewPrefab.WebView.CanGoBack();
        var canGoForward = await _canvasWebViewPrefab.WebView.CanGoForward();
        var serializedMessage = $"{{ \"type\": \"SET_BUTTONS\", \"canGoBack\": {canGoBack.ToString().ToLowerInvariant()}, \"canGoForward\": {canGoForward.ToString().ToLowerInvariant()} }}";
        _controlsWebViewPrefab.WebView.PostMessage(serializedMessage);

    } // end _refreshBackForwardState

    /// <summary>
    /// Sets the current URL for this webpage to be displayed in the url input text field if it is updated
    /// </summary>
    /// <param name="url">The updated url</param>
    void _setDisplayedUrl(string url)
    {
        if (_controlsWebViewPrefab.WebView != null)
        {
            //var serializedMessage = $"{{ \"type\": \"SET_URL\", \"url\": \"{url}\" }}";
            urlInputField.text = url;
            //_controlsWebViewPrefab.WebView.PostMessage(serializedMessage);
        }

    } // end _setDisplayedUrl

    /// <summary>
    /// Gets the screen coordinates for a raycast hit from the right controller and the canvas object transform
    /// </summary>
    /// <param name="rt">the rect transform of the object needing to be checked</param>
    /// <returns>the canvas position within the rect transform if it was hit, (-1,-1) if not</returns>
    protected Vector2 GetScreenCoords(RectTransform rt)
    {
        RaycastHit hit;
        Vector2 pixelUV;

        // if we didn't hit the general area of the window, return negative values
        if (!Physics.Raycast(referenceManager.rightLaser.transform.position, referenceManager.rightLaser.transform.forward, out hit, 10f))
        {
            pixelUV = new Vector2(-1f, -1f);
        }
        else
        {
            // attempting to use the raw image and raycast position to get the coordinates instead of mesh collider
            Vector3 hitScreenCoord = rt.InverseTransformPoint(hit.point);

            bool inside = (hitScreenCoord.x >= 0f) && (hitScreenCoord.x <= rt.rect.width) &&
                          (hitScreenCoord.y >= 0f) && (hitScreenCoord.y <= rt.rect.height);

            if (inside)
            {
                pixelUV = hitScreenCoord;

                // reverse y
                pixelUV.y = rt.rect.height - pixelUV.y;
            }
            else
            {
                pixelUV = new Vector2(-1f, -1f);
            }
        }

        return pixelUV;

    } // end GetScreenCoords

    /// <summary>
    /// Perfoms the on trigger click event for a full canvas web browser object
    /// </summary>
    protected void OnTriggerClick()
    {
        // need to add a way to check for each area of the prefab:
        bool eventTriggered = false;

        // - The main window (currently done)
        if (_canvasWebViewPrefab != null)
        {
            Vector2 pixelUV = CheckTriggerEventOnCanvasRawImage(_canvasWebViewPrefab);

            if (pixelUV.x > 0)
            {
                // for now if it isn't a pop-out and the main window was clicked
                // then set this window as the active browser, then do the click
                if ((this.GetType() != typeof(PopoutCanvasWebBrowserManager)) &&
                   (!webManagerScript.IsBrowserActive(browserID)))
                {
                    webManagerScript.ActivateBrowser(browserID);
                }

                previousPixelUV = pixelUV;
                dragging = true;
                eventTriggered = true;

                // attempting to add pointer down
                if (webViewWithPointerDownUp != null)
                {
                    Vector2 normailzedPoint = 
                        _canvasWebViewPrefab.WebView.PointToNormalized((int)pixelUV.x, (int)pixelUV.y);
                    webViewWithPointerDownUp.PointerDown(normailzedPoint);
                }
                else
                {
                    _canvasWebViewPrefab.WebView.Click((int)pixelUV.x, (int)pixelUV.y);
                }
            }
        }

        // Go through the controls section next
        if (!eventTriggered && (_controlsWebViewPrefab != null))
        {
            // - The forward and back buttons (currently on a canvas image and using JS
            Vector2 pixelUV = CheckTriggerEventOnCanvasRawImage(_controlsWebViewPrefab);

            if (pixelUV.x > 0)
            {
                _controlsWebViewPrefab.WebView.Click((int)pixelUV.x, (int)pixelUV.y);
                eventTriggered = true;
            }
        }

        // - Check the toggle graphs button (to fix the issue of them getting multiply clicked)
        if (!eventTriggered)
        {
            // Check to see if the toggle graphs button was pressed
            eventTriggered = IsCanvasButtonPressed(gameObject.GetNamedChild("ToggleGraphButton"));
        }

        // - Check the save web layout button (to fix the issue of them getting multiply clicked)
        if (!eventTriggered)
        {
            // Check to see if the save web layout button was pressed
            eventTriggered = IsCanvasButtonPressed(gameObject.GetNamedChild("SaveLayoutButton"));
        }

        // - Check the close window button (to fix the issue of them getting multiply clicked)
        if (!eventTriggered)
        {
            // Check to see if the close button was pressed
            eventTriggered = IsCanvasButtonPressed(gameObject.GetNamedChild("CloseWindowButton"));
        }

        // - Check the add window button (to fix the issue of them getting multiply clicked)
        if (!eventTriggered)
        {
            // Check to see if the add window button was pressed
            eventTriggered = IsCanvasButtonPressed(gameObject.GetNamedChild("AddWindowButton"));
        }

        // - Check the refresh window button (to fix the issue of them getting multiply clicked)
        if (!eventTriggered)
        {
            // Check to see if the refresh window button was pressed
            eventTriggered = IsCanvasButtonPressed(gameObject.GetNamedChild("RefreshWindowButton"));
        }

        // - The url input box
        if (!eventTriggered)
        {
            if (urlInputField != null)
            {
                Vector2 pixelUV = GetScreenCoords(urlInputField.GetComponent<RectTransform>());

                if (pixelUV.x > 0)
                {
                    TMP_InputField urlInput = urlInputField.GetComponent<TMP_InputField>();
                    urlInput.ActivateInputField();
                    //eventTriggered = true;
                }
            }
        }

        // Go through the keyboard section next
        /*if (!eventTriggered && (_keyboard != null))
        {
            // - The keyboard area
            Vector2 pixelUV = GetScreenCoords(_keyboard.GetComponent<RectTransform>());

            if (pixelUV.x > 0)
            {
                _keyboard.WebViewPrefab.WebView.Click((int)pixelUV.x, (int)pixelUV.y);                 
                eventTriggered = true;
            }
        }*/

    } // end OnTriggerClick

    /// <summary>
    /// Used for drag events...
    /// </summary>
    protected void OnTriggerPressed()
    {
        // need to add a way to check for each area of the prefab:
        Vector2 pixelUV;

        // - The main window (currently done)
        if ( (_canvasWebViewPrefab != null) && dragging)
        {
            pixelUV = CheckTriggerEventOnCanvasRawImage(_canvasWebViewPrefab);

            if (pixelUV.x > 0)
            {
                //int xChange = (int)previousPixelUV.x - (int)pixelUV.x;
                //int yChange = (int)previousPixelUV.y - (int)pixelUV.y;
                previousPixelUV = pixelUV;

                // attempting to add pointer move
                if (webViewWithMoveablePointer != null)
                {
                    Vector2 normailzedPoint =
                        _canvasWebViewPrefab.WebView.PointToNormalized((int)pixelUV.x, (int)pixelUV.y);
                    webViewWithMoveablePointer.MovePointer(normailzedPoint);
                }
            }
        }

    } // end OnTriggerPressed

    protected void OnTriggerUp()
    {
        // - The main window (currently done)
        if ((_canvasWebViewPrefab != null) && dragging)
        {
            if (previousPixelUV.x > 0)
            {
                // attempting to add pointer up
                if (webViewWithPointerDownUp != null)
                {
                    Vector2 normailzedPoint =
                        _canvasWebViewPrefab.WebView.PointToNormalized((int)previousPixelUV.x, (int)previousPixelUV.y);
                    webViewWithPointerDownUp.PointerUp(normailzedPoint);
                }
            }
        }

        dragging = false;
    
    }  // end OnTriggerUp

    /// <summary>
    /// When a message is received on the main window, handle the message
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="eventArgs"></param>
    private void MainCanvasOnMessageReceived(object sender, EventArgs<string> eventArgs)
    {
        // for now, there should only be popout messages, so just deal with those
        if (eventArgs.Value.Contains("popout"))
        {
            // create a popout config data to store this using the lastPopoutID and popout message
            // TODO: Think about the timing issue for this as the message is received
            // then the popout happens a little later (only on load browsers?)
            PopoutConfigData popoutConfig = new PopoutConfigData();
            currentPopoutID = lastPopoutID++;
            popoutConfig.startingPopoutMessage = eventArgs.Value;

            // Save the data to the main browser popout list
            popoutWindowData.Add(currentPopoutID, popoutConfig);

            // For now just print out the current popoutlist
            Debug.Log("Message received for window id " + this.browserID + 
                      ", creating popout window: " + eventArgs.Value);
        }
        else if (eventArgs.Value.Contains("vuplex_ready"))
        {
            CreateLoadedPopouts();
        }

    } // end MainCanvasOnMessageReceived

    private async void CreateLoadedPopouts()
    {
        // only load popouts if the data list is not null (set up in the web manager)
        if (loadedPopouts != null)
        {
            float waitTime = 2.5f;

            // attempting to wait a bit to let MDV initialize the charts for this page
            await Task.Delay(TimeSpan.FromSeconds(waitTime));

            // this wait time is to avoid issues with another popout overwriting the temp data (race condition)
            // TODO: Need to find a way to remove the race condition (if possible)
            waitTime = 0.1f;

            foreach (PopoutConfigData popoutData in loadedPopouts)
            {
                // store the popout data in the main window temp data for use in creating the window
                tempPopoutConfigData = popoutData;

                Debug.Log("Attempting to load a popout: " + popoutData.startingPopoutMessage +
                          " for window: " + browserID);

                // use task here to wait for the popouts to fix race condition of updating popoutIDs
                _canvasWebViewPrefab.WebView.PostMessage(popoutData.startingPopoutMessage);
                await Task.Delay(TimeSpan.FromSeconds(waitTime));
            }
        }

    } // end CreateLoadedPopouts

    /// <summary>
    /// Creates a popout window asyncronously when it is requested by a main webview
    /// </summary>
    /// <param name="sender">The sender of the request</param>
    /// <param name="eventArgs">The popout data being sent down</param>
    private async void CreatePopoutWindow(object sender, PopupRequestedEventArgs eventArgs)
    {
        // using the web manager code to instatiate so it can keep track of all browser windows
        GameObject popoutObject =
            webManagerScript.CreatePopOutWindow(gameObject.transform, eventArgs.WebView, browserID);

        // save the popout index into the popout list so we can use it to find the popout window later
        // TODO: Think about the timing issue for this as the message is received
        // then the popout happens a little later (only on load browsers?)
        popoutObject.GetComponent<PopoutCanvasWebBrowserManager>().popoutID = currentPopoutID;

        // if it was created by load browser seesion, the popout config data has the data to update position, etc.
        if (tempPopoutConfigData != null)
        {
            // if we are loading the popout, grab the data from the config file
            popoutObject.transform.position = tempPopoutConfigData.startingPosition;
            popoutObject.transform.rotation = tempPopoutConfigData.startingRotation;
            popoutObject.transform.localScale = tempPopoutConfigData.startingScale;

            Debug.Log("Popout loaded: " + tempPopoutConfigData.startingPopoutMessage + ", for window: " + browserID);
        }
        // otherwise the popout config data was not set up for a load, so leave the game object as is

        // clear the tempData as we are done with it
        tempPopoutConfigData = null;

        CanvasWebViewPrefab popupPrefab =
            popoutObject.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>();

        // wait for the prefab to be initialized
        await popupPrefab.WaitUntilInitialized();

        // Set up the closing of the popup - may not be needed
        popupPrefab.WebView.CloseRequested += (popupWebView, closeEventArgs) => {
            Debug.Log("Closing the popup");
            CloseWindow();
        };

    } // end CreatePopoutWindow

    /// <summary>
    /// Controls messages to the JavaScript control panel, currently only forward and back buttons
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="eventArgs"></param>
    private void Controls_MessageEmitted(object sender, EventArgs<string> eventArgs)
    {
        if (eventArgs.Value == "CONTROLS_INITIALIZED")
        {
            // The controls UI won't be initialized in time to receive the first UrlChanged event,
            // so explicitly set the initial URL after the controls UI indicates it's ready.
            _setDisplayedUrl(_canvasWebViewPrefab.WebView.Url);
            return;
        }

        var message = eventArgs.Value;

        if (message == "GO_BACK")
        {
            _canvasWebViewPrefab.WebView.GoBack();
        }
        else if (message == "GO_FORWARD")
        {
            _canvasWebViewPrefab.WebView.GoForward();
        }

    } // end Controls_MessageEmitted

    /// <summary>
    /// Helper function to check canvases with raw images to see if they've been hit by a raycast
    /// </summary>
    /// <param name="canvasToCheck">The canvas to check against the raycast</param>
    /// <returns>the canvas position within the rect transform if it was hit, (-1,-1) if not</returns>
    private Vector2 CheckTriggerEventOnCanvasRawImage(CanvasWebViewPrefab canvasToCheck)
    {
        GameObject webView = canvasToCheck.gameObject.GetNamedChild("CanvasWebViewPrefabView");
        RawImage webViewImage = webView.GetComponent<RawImage>();
        Vector2 pixelUV = GetScreenCoords(webViewImage.rectTransform);

        return pixelUV;

    } // end CheckTriggerEventOnCanvasRawImage

    /// <summary>
    /// Checks to see if a canvas button has been pressed
    /// </summary>
    /// <param name="buttonToCheck">the button to check</param>
    /// <returns></returns>
    private bool IsCanvasButtonPressed(GameObject buttonToCheck)
    {
        bool eventTriggered = false; 
        
        if (buttonToCheck != null)
        {
            Vector2 pixelUV = GetScreenCoords(buttonToCheck.GetComponent<RectTransform>());

            if (pixelUV.x > 0)
            {
                Button buttonComponent = buttonToCheck.GetComponent<Button>();
                buttonComponent.onClick.Invoke();
                eventTriggered = true;
            }
        }

        return eventTriggered;

    } // end IsCanvasButtonPressed

    /// <summary>
    /// Clears the message text
    /// </summary>
    private void ClearMessageText()
    {
        messageText.text = "";

    } // end ClearMessageText

    /// <summary>
    /// Controls HTML code that is inserted into the controls part of the browser and is used for forward/back states
    /// </summary>
    const string CONTROLS_HTML = @"
            <!DOCTYPE html>
            <html>
                <head>
                    <!-- This transparent meta tag instructs 3D WebView to allow the page to be transparent. -->
                    <meta name='transparent' content='true'>
                    <meta charset='UTF-8'>
                    <style>
                        body {
                            font-family: Helvetica, Arial, Sans-Serif;
                            margin: 0;
                            height: 100vh;
                            color: white;
                        }
                        .controls {
                            display: flex;
                            justify-content: space-between;
                            align-items: center;
                            height: 100%;
                        }
                        .controls > div {
                            background-color: #283237;
                            border-radius: 8px;
                            height: 100%;
                        }
                        .url-display {
                            flex: 0 0 0%;
                            width: 0%;
                            display: flex;
                            align-items: center;
                            overflow: hidden;
                            cursor: default;
                        }
                        #url {
                            width: 100%;
                            white-space: nowrap;
                            overflow: hidden;
                            text-overflow: ellipsis;
                            padding: 0 15px;
                            font-size: 18px;
                        }
                        .buttons {
                            flex: 0 0 95%;
                            width: 95%;
                            display: flex;
                            justify-content: space-around;
                            align-items: center;
                        }
                        .buttons > button {
                            font-size: 40px;
                            background: none;
                            border: none;
                            outline: none;
                            color: white;
                            margin: 0;
                            padding: 0;
                        }
                        .buttons > button:disabled {
                            color: rgba(255, 255, 255, 0.3);
                        }
                        .buttons > button:last-child {
                            transform: scaleX(-1);
                        }
                        /* For Gecko only, set the background color
                        to black so that the shader's cutout rect
                        can translate the black pixels to transparent.*/
                        @supports (-moz-appearance:none) {
                            body {
                                background-color: black;
                            }
                        }
                    </style>
                </head>
                <body>
                    <div class='controls'>
                        <div class='url-display'>
                            <div id='url'></div>
                        </div>
                        <div class='buttons'>
                            <button id='back-button' disabled='true' onclick='vuplex.postMessage(""GO_BACK"")'>←</button>
                            <button id='forward-button' disabled='true' onclick='vuplex.postMessage(""GO_FORWARD"")'>←</button>
                        </div>
                    </div>
                    <script>
                        // Handle messages sent from C#
                        function handleMessage(message) {
                            var data = JSON.parse(message.data);
                            if (data.type === 'SET_URL') {
                                document.getElementById('url').innerText = data.url;
                            } else if (data.type === 'SET_BUTTONS') {
                                document.getElementById('back-button').disabled = !data.canGoBack;
                                document.getElementById('forward-button').disabled = !data.canGoForward;
                            }
                        }

                        function attachMessageListener() {
                            window.vuplex.addEventListener('message', handleMessage);
                            window.vuplex.postMessage('CONTROLS_INITIALIZED');
                        }

                        if (window.vuplex) {
                            attachMessageListener();
                        } else {
                            window.addEventListener('vuplexready', attachMessageListener);
                        }
                    </script>
                </body>
            </html>
        ";

    const string NOT_SUPPORTED_HTML = @"
            <body>
                <style>
                    body {
                        font-family: sans-serif;
                        display: flex;
                        justify-content: center;
                        align-items: center;
                        line-height: 1.25;
                    }
                    div {
                        max-width: 80%;
                    }
                    li {
                        margin: 10px 0;
                    }
                </style>
                <div>
                    <p>
                        Sorry, but this 3D WebView package doesn't support yet the <a href='https://developer.vuplex.com/webview/IWithPopups'>IWithPopups</a> interface. Current packages that support popups:
                    </p>
                    <ul>
                        <li>
                            <a href='https://developer.vuplex.com/webview/StandaloneWebView'>3D WebView for Windows and macOS</a>
                        </li>
                        <li>
                            <a href='https://developer.vuplex.com/webview/AndroidWebView'>3D WebView for Android</a>
                        </li>
                        <li>
                            <a href='https://developer.vuplex.com/webview/AndroidGeckoWebView'>3D WebView for Android with Gecko Engine</a>
                        </li>
                    </ul>
                </div>
            </body>
        ";
}
