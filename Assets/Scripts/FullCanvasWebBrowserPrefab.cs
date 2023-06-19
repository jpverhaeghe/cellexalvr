﻿using System;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using Vuplex.WebView;
using TMPro;
using CellexalVR.Interaction;

public class FullCanvasWebBrowserPrefab : MonoBehaviour
{
    // Canvas prefabs in the resource folder for Vulpex WebView
    [SerializeField] CanvasWebViewPrefab _controlsWebViewPrefab;
    [SerializeField] CanvasWebViewPrefab _canvasWebViewPrefab;
    [SerializeField] CanvasKeyboard _keyboard;
    [SerializeField] TMP_InputField urlInputField;

    // private fields for this script
    private IWithPopups webViewWithPopups;
    private WebManager webManagerScript;

    async void Start()
    {
        // find the web manager script for adding other windows
        webManagerScript = GameObject.Find("WebManager").GetComponent<WebManager>();

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

        _controlsWebViewPrefab.WebView.MessageEmitted += Controls_MessageEmitted;
        _controlsWebViewPrefab.WebView.LoadHtml(CONTROLS_HTML);

        // Android Gecko and UWP w/ XR enabled don't support transparent webviews, so set the cutout
        // rect to the entire view so that the shader makes its black background pixels transparent.
        var pluginType = _controlsWebViewPrefab.WebView.PluginType;
        if (pluginType == WebPluginType.AndroidGecko || pluginType == WebPluginType.UniversalWindowsPlatform)
        {
            _controlsWebViewPrefab.SetCutoutRect(new Rect(0, 0, 1, 1));
        }

    // Jim - attempting to add pop-out windows (this may need to move around)

        // After the prefab has initialized, you can use the IWithPopups API via its WebView property.
        // https://developer.vuplex.com/webview/IWithPopups
        webViewWithPopups = _canvasWebViewPrefab.WebView as IWithPopups;
        if (webViewWithPopups == null)
        {
            // if the system can't handle pop-ups, let the user know
            _canvasWebViewPrefab.WebView.LoadHtml(NOT_SUPPORTED_HTML);
            return;
        }

        // now attemtping to load pop-up mode into a new window
        webViewWithPopups.SetPopupMode(PopupMode.LoadInNewWebView);

        // set the message handler for the popup
        webViewWithPopups.PopupRequested += async (webView, eventArgs) => {
            Debug.Log("Popup opened with URL: " + eventArgs.Url);

            // using the web manager code to instatiate instead of doing it here
            GameObject popupObject = webManagerScript.CreateNewWindow(gameObject.transform, eventArgs.Url);
            CanvasWebViewPrefab popupPrefab = 
                popupObject.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>();
            /*var popupPrefab = CanvasWebViewPrefab.Instantiate(eventArgs.WebView);
            popupPrefab.Resolution = _canvasWebViewPrefab.Resolution;
            //popupPrefab.transform.SetParent(canvas.transform, false);*/

            // This may not be necessary
            await popupPrefab.WaitUntilInitialized();
            /*popupPrefab.WebView.CloseRequested += (popupWebView, closeEventArgs) => {
                Debug.Log("Closing the popup");
                popupPrefab.Destroy();
            };*/
        };
    }

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
        // only create a new window if hte web manager exists
        if (webManagerScript != null)
        {
            webManagerScript.CreateNewWindow(gameObject.transform, "https://www.google.com/");
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
        // clean up the window resources for this window
        // (may be overkill but would rather have it cleaned up)
        _controlsWebViewPrefab.WebView.Dispose();
        _canvasWebViewPrefab.WebView.Dispose();
        _keyboard.WebViewPrefab.WebView.Dispose();

        // Destroy the parent object which will destroy all the child objects as well
        Destroy(gameObject);

    } // end CloseWindow

    async void _refreshBackForwardState()
    {
        // Get the main webview's back / forward state and then post a message
        // to the controls UI to update its buttons' state.
        var canGoBack = await _canvasWebViewPrefab.WebView.CanGoBack();
        var canGoForward = await _canvasWebViewPrefab.WebView.CanGoForward();
        var serializedMessage = $"{{ \"type\": \"SET_BUTTONS\", \"canGoBack\": {canGoBack.ToString().ToLowerInvariant()}, \"canGoForward\": {canGoForward.ToString().ToLowerInvariant()} }}";
        _controlsWebViewPrefab.WebView.PostMessage(serializedMessage);
    }

    void Controls_MessageEmitted(object sender, EventArgs<string> eventArgs)
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
    }

    void _setDisplayedUrl(string url)
    {
        if (_controlsWebViewPrefab.WebView != null)
        {
            //var serializedMessage = $"{{ \"type\": \"SET_URL\", \"url\": \"{url}\" }}";
            urlInputField.text = url;
            //_controlsWebViewPrefab.WebView.PostMessage(serializedMessage);
        }
    }

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
