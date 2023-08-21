using CellexalVR.General;
using CellexalVR.Menu.Buttons;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Unity.XR.CoreUtils;
using UnityEngine;
using Vuplex.WebView;

namespace CellexalVR.AnalysisObjects
{
    /// <summary>
    /// Manages the browser windows for the data sets
    /// </summary>
    public class WebManager : MonoBehaviour
    {
        // Constant strings for this class
        //public const string default_url =
        //    "https://mdv-dev.netlify.app/?dir=https://mdvstatic.netlify.app/ATRTImages2&socket=http://localhost:5050";
        //public const string default_url = "http://localhost:5050/?view=default";
        //public const string default_url = "https://mdv.molbiol.ox.ac.uk/projects";
        //"https://datashare.molbiol.ox.ac.uk/public/project/Wellcome_Discovery/sergeant/pbmc1k";
        // TODO: Need to move this from data directories to user directories so each user can have their own config file
        public const string browserConfigFilename = "/BrowserConfigData.json";

        [Header("The prefab for all browser window instances")]
        [SerializeField] GameObject browserWindowPrefab;
        [SerializeField] GameObject popoutWindowPrefab;

        [Header("Buttons associated with the Web Manager")]
        [SerializeField] CellexalToolButton webBrowserVisibilityButton;
        [SerializeField] CellexalButton resetWebBrowserButton;

        public TMPro.TextMeshPro output;
        public ReferenceManager referenceManager;
        public int activeBrowserID = 0;

        private Dictionary<int, GameObject> browserWindows;
        private int lastBrowserID = 0;
        private bool isVisible;
        
        /// <summary>
        /// When Unity validates this object in the editor, update the input reader reference manager
        /// </summary>
        private void OnValidate()
        {
            if (gameObject.scene.IsValid())
            {
                referenceManager = GameObject.Find("InputReader").GetComponent<ReferenceManager>();
            }
        }

        /// <summary>
        /// Use this for initialization
        /// </summary>
        void Start()
        {
            // Use a desktop User-Agent to request the desktop versions of websites.
            // https://developer.vuplex.com/webview/Web#SetUserAgent
            Web.SetUserAgent(false);

            // set up a list to store the browser windows
            browserWindows = new Dictionary<int, GameObject>();

            CellexalEvents.GraphsLoaded.AddListener(CreateBrowserSession);
            //CellexalEvents.GraphsUnloaded.AddListener(ClearBrowserSession);
        }

        /// <summary>
        /// Creates a new browser window at the coordinates and rotation with the given url, 
        /// and offsets it by a little if the boolean is true
        /// </summary>
        /// <param name="browserPos">Where the browser window should appear in the world</param>
        /// <param name="browserRotation">The beginning rotation of the browser</param>
        /// <param name="url">the url to display</param>
        /// <param name="offsetZ">Whether or not to offset this window a little in front</param>
        /// <returns>The web browser canvas game object</returns>
        public GameObject CreateNewWindow(Vector3 browserPos, Quaternion browserRotation,
                                          string url, bool offsetZ)
        {
            GameObject newWindow = SetupBrowserPrefab(browserWindowPrefab, browserPos, browserRotation, offsetZ);

            // set up the initial url to see if it works without loading
            newWindow.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>().InitialUrl = url;

            return newWindow;

        } // end CreateNewWindow

        /// <summary>
        /// Creates a new browser window relative to the window creating it 
        /// </summary>
        /// <param name="browserTransform">Where the browser window should appear</param>
        /// <param name="url">the url to display</param>
        /// <returns>The web browser canvas game object</returns>
        public GameObject CreateNewWindow(Transform browserTransform, string url)
        {
            return (CreateNewWindow(browserTransform.position, browserTransform.rotation, url, true));
        }

        /// <summary>
        /// Creates a new browser popout window at the coordinates and rotation, 
        /// and offsets it by a little if the boolean is true
        /// </summary>
        /// <param name="browserPos">Where the browser window should appear in the world</param>
        /// <param name="browserRotation">The beginning rotation of the browser</param>
        /// <param name="webView">The webview to link this popout to</param>
        /// <param name="parentID">The parent ID of the main webView form which this was created</param>
        /// <param name="offsetZ">Whether or not to offset this window a little in front</param>
        /// <returns></returns>
        public GameObject CreatePopOutWindow(Vector3 browserPos, Quaternion browserRotation, 
                                             IWebView webView, int parentID, bool offsetZ)
        {
            // Create a popout prefab window game object
            GameObject newWindow = SetupBrowserPrefab(popoutWindowPrefab, browserPos, browserRotation, offsetZ);

            // set up the pop out to look back at the previous web view
            CanvasWebViewPrefab popupPrefab =
                newWindow.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>();
            popupPrefab.SetWebViewForInitialization(webView);

            // store the parent window id so it can be set linked to that parent when saving and deleting
            newWindow.GetComponent<PopoutCanvasWebBrowserManager>().parentID = parentID;

            // for now, if the graphs are not visible, make them visible.
            // TODO: This will need to know what pop-out was generated and what graph it is associated with
            /*if (!referenceManager.graphManager.GraphsVisible)
            {
                referenceManager.graphManager.ShowGraphs();
            }*/

            return newWindow;

        } // end CreatePopOutWindow

        /// <summary>
        /// Creates a new browser popout window relative to the window creating it
        /// </summary>
        /// <param name="browserTransform">The transform of the window creating this window</param>
        /// <param name="webView">The webview to link this popout to</param>
        /// <param name="parentID">The parent ID of the main webView form which this was created</param>
        /// <returns></returns>
        public GameObject CreatePopOutWindow(Transform browserTransform, IWebView webView, int parentID)
        {
            return (CreatePopOutWindow(browserTransform.position, browserTransform.rotation, 
                                       webView, parentID, true));

        } // end CreatePopOutWindow

        /// <summary>
        /// Checks to see if the browser with the given id is currently the active browser
        /// </summary>
        /// <param name="browserID"></param>
        /// <returns></returns>
        public bool IsBrowserActive(int  browserID)
        {
            return (browserID == activeBrowserID);

        } // end IsBrowserActive

        /// <summary>
        /// Activates the browser with the given ID, de-activating the previous one
        /// </summary>
        /// <param name="browserID"></param>
        public void ActivateBrowser(int browserID)
        {
            // de-activate the previous browser
            GameObject currentActiveBrowser;

            if (browserWindows.TryGetValue(activeBrowserID, out currentActiveBrowser))
            {
                FullCanvasWebBrowserManager canvasWebBrowserManager =
                    currentActiveBrowser.GetComponent<FullCanvasWebBrowserManager>();

                canvasWebBrowserManager.activeBrowser = false;
                canvasWebBrowserManager.cursorIcon.SetActive(false);
            }

            activeBrowserID = browserID;

            // now set up the newly clicked browser window to be the active one
            if (browserWindows.TryGetValue(browserID, out currentActiveBrowser))
            {
                FullCanvasWebBrowserManager canvasWebBrowserManager = 
                    currentActiveBrowser.GetComponent<FullCanvasWebBrowserManager>();

                canvasWebBrowserManager.activeBrowser = true;
                canvasWebBrowserManager.cursorIcon.SetActive(true);
            }

        } // end ActivateBrowser

        /// <summary>
        /// Removes the browser window from the list of game objects being tracked by the browser manager
        /// </summary>
        /// <param name="browserWindowToRemove">The browser window to remove from the scene</param>
        public void RemoveBrowserWindowFromScene(GameObject browserWindowToRemove)
        {
            // remove the game object from browser list
            FullCanvasWebBrowserManager browserCanvas = 
                browserWindowToRemove.GetComponent<FullCanvasWebBrowserManager>();

            // if this is a main window, remove any popouts associated with it
            if (browserCanvas.GetType() != typeof(PopoutCanvasWebBrowserManager))
            {
                RemovePopoutWindowsForMainBrowser(browserCanvas.browserID);
            }
            // since this is a popout, we need to remove it from its parent window
            else
            {
                PopoutCanvasWebBrowserManager popoutCanvas = (PopoutCanvasWebBrowserManager)browserCanvas;

                GameObject parentWindow;
                
                if (browserWindows.TryGetValue(popoutCanvas.parentID, out parentWindow)) 
                {
                    parentWindow.GetComponent<FullCanvasWebBrowserManager>().RemovePopoutWindow(popoutCanvas.popoutID);
                }
            }

            // Destroy the parent browser object which will destroy all the child objects as well
            browserWindows.Remove(browserCanvas.browserID);
            Destroy(browserWindowToRemove);

        } // end RemoveBrowserWindowFromScene

        /// <summary>
        /// Will reset the main window if all windows were closed, only if active is true
        /// </summary>
        /// <param name="active"></param>
        public void ResetIfNoActiveBrowser(bool active)
        {
            if (active && (browserWindows.Count <= 0))
            {
                // create a new window here
                //CreateNewWindow(gameObject.transform, default_url);
                CreateNewWindow(gameObject.transform, CellexalConfig.Config.defaultWebPage);
            }

        } // end ResetIfNoActiveBrowser

        /// <summary>
        /// Hides or shows browser windows based on visible parameter
        /// </summary>
        /// <param name="visible">True to show windows, false to hide them</param>
        public void SetVisible(bool visible)
        {
            foreach (KeyValuePair<int, GameObject> browserWindow in browserWindows)
            {
                browserWindow.Value.SetActive(visible);
            }

            isVisible = visible;

        } // end SetVisible

        /// <summary>
        /// Resets the browser to the base browser state - original window open
        /// </summary>
        public void ResetBrowserSession()
        {
            DestroyCurrentBrowsers();

            // create the initial window
            ResetIfNoActiveBrowser(true);

            // hide the browser windows if the browser was hidden before
            if (!isVisible)
            {
                SetVisible(false);
            }

        } // end ResetBrowserSession

        /// <summary>
        /// Clears the current browser session, deleting all the browser windows
        /// </summary>
        public void ClearBrowserSession()
        {
            // remove browsers as they will be loaded from data next time
            DestroyCurrentBrowsers();

            // first set up the browser buttons as inactive
            webBrowserVisibilityButton.SetButtonActivated(false);
            resetWebBrowserButton.SetButtonActivated(false);

        } // end ClearBrowserSession

        private GameObject SetupBrowserPrefab(GameObject prefabToUse,Vector3 browserPos, 
                                              Quaternion browserRotation, bool offsetZ)
        {
            if (offsetZ)
            {
                browserPos.z -= 0.01f;
            }

            GameObject newWindow = Instantiate(prefabToUse, browserPos, browserRotation);

            // need to set the camera of the canvas object for this window
            newWindow.GetComponent<Canvas>().worldCamera = Camera.main;

            // save the browser id and store the game object in the dictionary for access later
            newWindow.GetComponent<FullCanvasWebBrowserManager>().browserID = lastBrowserID;
            browserWindows.Add(lastBrowserID++, newWindow);

            return newWindow;

        } // end GameObject

        /// <summary>
        /// A listener of the web manager to call once the graphs have been loaded for a particular data set
        /// </summary>
        private void CreateBrowserSession()
        {
            // first set up the browser buttons as active
            webBrowserVisibilityButton.SetButtonActivated(true);
            resetWebBrowserButton.SetButtonActivated(true);

            // load the last session data from the Data folder for this selection
            LoadBrowserSession();

            // show the browser window(s) and set up the double lasers
            webBrowserVisibilityButton.Click();

            // go through the graphs and mark them as hidden to begin with
            referenceManager.graphManager.HideGraphs();

        } // end CreateBrowserSession

        /// <summary>
        /// Saves the current browser session to a JSON file in the project data folder, 
        /// creating it if it doesn't exist
        /// </summary>
        public void SaveBrowserSession()
        {
            string path = CellexalUser.DatasetFullPath + browserConfigFilename;

            // set up the web browser configuration save data
            BrowserSaveData browserSaveData = new BrowserSaveData();

            // create the json data using the current browser windows
            foreach (KeyValuePair<int, GameObject> browserWindow in browserWindows)
            {
                FullCanvasWebBrowserManager browserCanvas = 
                    browserWindow.Value.GetComponent<FullCanvasWebBrowserManager>();

                // adding the main windows as pop-outs are connected by main window
                if (browserCanvas.GetType() != typeof(PopoutCanvasWebBrowserManager))
                {
                    // create the browser config data for this window
                    BrowserConfigData browserData = new BrowserConfigData();

                    // store the url, position, rotation and scale
                    browserData.startingURL = browserCanvas.urlInputField.text;
                    browserData.startingPosition = browserCanvas.transform.position;
                    browserData.startingRotation = browserCanvas.transform.rotation;
                    browserData.startingScale = browserCanvas.transform.localScale;

                    // find all popouts that belong to this window if there are any
                    List<GameObject> popoutsToSave = FindAllPopoutWindowsForThisWindow(browserCanvas.browserID);
                    
                    // store the popout data if there are any
                    foreach (GameObject popout in popoutsToSave)
                    {
                        // get the popout manager - we should only have popouts if the find all did its job well
                        PopoutCanvasWebBrowserManager popoutCanvas = 
                            popout.GetComponent<PopoutCanvasWebBrowserManager>();

                        // create a variable to store the popout string data into
                        PopoutConfigData popoutData;

                        // storing the popout data string if it exists
                        if (browserCanvas.popoutWindowData.TryGetValue(popoutCanvas.popoutID, out popoutData))
                        {
                            //popoutData.startingPopoutMessage = popoutData.startingPopoutMessage;

                            // store the position, rotation and scale
                            popoutData.startingPosition = popout.transform.position;
                            popoutData.startingRotation = popout.transform.rotation;
                            popoutData.startingScale = popout.transform.localScale;

                            // add the popout to the popout data list
                            browserData.popoutWindows.Add(popoutData);
                        }
                        else
                        {
                            Debug.Log("Error: popout was in the window list, " +
                                "but did not exist in the this browser window list!");
                        }
                    }

                    // finally save the browser config data to the list of browser data
                    browserSaveData.browserConfigData.Add(browserData);
                }
            }

            // write the file to the data directory to be used later
            string json = JsonUtility.ToJson(browserSaveData);
            File.WriteAllText(path, json);

            Debug.Log("Saving data to: " + path);

        } // end SaveBrowserSession

        /// <summary>
        /// Loads the last web browser state if it was saved before
        /// </summary>
        private void LoadBrowserSession()
        {
            string path = CellexalUser.DatasetFullPath + browserConfigFilename;

            // set up the web browser configuration save data
            BrowserSaveData browserSaveData = new BrowserSaveData();

            // only load the previous configuration if it has been saved
            if (File.Exists(path))
            {
                Debug.Log("Loading data from: " + path);

                // read the file and parse it into the json variable
                string json = File.ReadAllText(path);
                browserSaveData = JsonUtility.FromJson<BrowserSaveData>(json);

                // load each page and set up the transforms
                foreach (BrowserConfigData browserData in browserSaveData.browserConfigData)
                {
                    // create the web page with the saved url and transform data
                    GameObject browserCanvas = CreateNewWindow(browserData.startingPosition, 
                                                               browserData.startingRotation,
                                                               browserData.startingURL, false);

                    browserCanvas.transform.localScale = browserData.startingScale;

                    // wait for the prefab to be initialized so we can send popout messages
                    FullCanvasWebBrowserManager mainBrowserManager = 
                        browserCanvas.GetComponent<FullCanvasWebBrowserManager>();

                    mainBrowserManager.loadedPopouts = browserData.popoutWindows;

                    // testing to see if the popoutWindows are stored correctly as json
                    if (browserData.popoutWindows.Count > 0)
                    {
                        MDVMessageData popoutMessageAsJson =
                            JsonUtility.FromJson<MDVMessageData>(browserData.popoutWindows[0].startingPopoutMessage);

                        //Debug.Log("A popout message as json for window " + mainBrowserManager.browserID + 
                        //          " - type: " + popoutMessageAsJson.type + ", chartID: " + popoutMessageAsJson.chartID);
                    }
                }
            }
            else
            {
                Debug.Log("No configuration file found, loading standard setup");
            }

        } // end LoadBrowserSession

        /// <summary>
        /// Destroys all current browswer windows, clears the dictionary and resets the browser id counter
        /// </summary>
        private void DestroyCurrentBrowsers()
        {
            // go through all currently open windows and close them
            foreach (KeyValuePair<int, GameObject> browserWindow in browserWindows)
            {
                Destroy(browserWindow.Value);
            }

            // remove all windows from the dictionary and reset the id
            browserWindows.Clear();
            lastBrowserID = 0;

        } // end DestroyCurrentBrowsers

        /// <summary>
        /// Removes all the popouts associated with this browser
        /// </summary>
        /// <param name="parentID">the parent id for the browser to remove popouts</param>
        private void RemovePopoutWindowsForMainBrowser(int parentID)
        {
            // store the pop-outs in a list so they can be removed
            List<GameObject> popoutWindowsToRemove = FindAllPopoutWindowsForThisWindow(parentID);

            // go through all the popouts associated with this browser window
            foreach (GameObject popoutWindow in popoutWindowsToRemove)
            {
                browserWindows.Remove(popoutWindow.GetComponent<FullCanvasWebBrowserManager>().browserID);
                Destroy(popoutWindow.gameObject);
            }

        } // RemovePopoutWindowsForMainBrowser

        private List<GameObject> FindAllPopoutWindowsForThisWindow(int parentID)
        {
            // store the pop-outs in a list so they can be removed
            List<GameObject> popoutWindows = new List<GameObject>();

            // need to go through the browser dictionary and remove any pop-outs associated with this window
            foreach (KeyValuePair<int, GameObject> browserWindow in browserWindows)
            {
                FullCanvasWebBrowserManager browserCanvas =
                    browserWindow.Value.GetComponent<FullCanvasWebBrowserManager>();

                if (browserCanvas.GetType() == typeof(PopoutCanvasWebBrowserManager))
                {
                    PopoutCanvasWebBrowserManager popOutCanvas = (PopoutCanvasWebBrowserManager)browserCanvas;

                    if (parentID == popOutCanvas.parentID)
                    {
                        popoutWindows.Add(popOutCanvas.gameObject);
                    }
                }
            }

            return popoutWindows;

        } // end FindAllPopoutWindowsForThisWindow
    }
}