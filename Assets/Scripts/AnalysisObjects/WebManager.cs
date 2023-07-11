using CellexalVR.General;
using CellexalVR.Menu.Buttons;
using System.Collections.Generic;
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
        public const string default_url = "https://mdv.molbiol.ox.ac.uk/projects";
        //"https://datashare.molbiol.ox.ac.uk/public/project/Wellcome_Discovery/sergeant/pbmc1k";
        [Header("The prefab for all browser window instances")]
        [SerializeField] GameObject browserWindowPrefab;
        [SerializeField] GameObject popoutWindowPrefab;

        [Header("Buttons associated with the Web Manager")]
        [SerializeField] CellexalToolButton webBrowserVisibilityButton;
        [SerializeField] CellexalButton resetWebBrowserButton;

        public TMPro.TextMeshPro output;
        public ReferenceManager referenceManager;

        private Dictionary<int, GameObject> browserWindows;
        private int lastBrowserID = 0;
        private bool isVisible;

        private void OnValidate()
        {
            if (gameObject.scene.IsValid())
            {
                referenceManager = GameObject.Find("InputReader").GetComponent<ReferenceManager>();
            }
        }

        // Use this for initialization
        void Start()
        {
            // Use a desktop User-Agent to request the desktop versions of websites.
            // https://developer.vuplex.com/webview/Web#SetUserAgent
            Web.SetUserAgent(false);

            // set up a list to store the browser windows
            browserWindows = new Dictionary<int, GameObject>();

            //SetVisible(false);

            CellexalEvents.GraphsLoaded.AddListener(CreateBrowserSession);
            //CellexalEvents.GraphsUnloaded.AddListener(ClearBrowserSession);
        }

        /// <summary>
        /// Creates a new browser window relative to the window creating it
        /// </summary>
        public GameObject CreateNewWindow(Transform browserTransform, string url)
        {
            Vector3 newPos = browserTransform.position;
            newPos.z -= 0.01f;
            GameObject newWindow = Instantiate(browserWindowPrefab, newPos, browserTransform.rotation);

            // set up the initial url to see if it works without loading
            newWindow.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>().InitialUrl = url;

            // need to set the camera of the canvas object for this window
            newWindow.GetComponent<Canvas>().worldCamera = Camera.main;

            newWindow.GetComponent<FullCanvasWebBrowserManager>().browserID = lastBrowserID;
            browserWindows.Add(lastBrowserID++, newWindow);
            return newWindow;

        } // end CreateNewWindow

        /// <summary>
        /// Creates a new browser window relative to the window creating it
        /// </summary>
        public GameObject CreatePopOutWindow(Transform browserTransform, IWebView webView)
        {
            // set the position to be just in front of the last main window
            Vector3 newPos = browserTransform.position;
            newPos.z -= 0.01f;
            GameObject newWindow = Instantiate(popoutWindowPrefab, newPos, browserTransform.rotation);

            // need to set the camera of the canvas object for this window
            newWindow.GetComponent<Canvas>().worldCamera = Camera.main;

            // set up the pop out to look back at the previous web view
            CanvasWebViewPrefab popupPrefab =
                newWindow.GetNamedChild("CanvasMainWindow").GetComponent<CanvasWebViewPrefab>();
            popupPrefab.SetWebViewForInitialization(webView);

            // store the object so it can be set to invisible if the browser is turned off
            newWindow.GetComponent<FullCanvasWebBrowserManager>().browserID = lastBrowserID;
            browserWindows.Add(lastBrowserID++, newWindow);
            return newWindow;

        } // end CreateNewWindow

        /// <summary>
        /// Removes the browser window from the list of game objects being tracked by the browser manager
        /// </summary>
        /// <param name="browserWindowToRemove">The browser window to remove from the scene</param>
        public void RemoveBrowserWindowFromScene(GameObject browserWindowToRemove)
        {
            // remove the game object from browser list
            browserWindows.Remove(browserWindowToRemove.GetComponent<FullCanvasWebBrowserManager>().browserID);

            // Destroy the parent browser object which will destroy all the child objects as well
            Destroy(browserWindowToRemove);

        } // end RemoveBrowserWindowFromScene

        // TODO: Update this to use a different output text and use 3D Web View keyboard
        public void EnterKey()
        {
            print("Navigate to - " + output.text);
            // If url field does not contain '.' then may not be a url so google the output instead
            if (!output.text.Contains('.'))
            {
                output.text = "www.google.com/search?q=" + output.text;
            }
            //webBrowser.OnNavigate(output.text);
            referenceManager.multiuserMessageSender.SendMessageBrowserEnter();
        }

        /// <summary>
        /// Will reset the main window if all windows were closed, only if active is true
        /// </summary>
        /// <param name="active"></param>
        public void SetBrowserActive(bool active)
        {
            if (active && (browserWindows.Count <= 0))
            {
                // create a new window here
                CreateNewWindow(gameObject.transform, default_url);
            }

        } // end SetBrowserActive

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
        public void ResetBrowser()
        {
            // go through all currently open windows and close them
            foreach (KeyValuePair<int, GameObject> browserWindow in browserWindows)
            {
                Destroy(browserWindow.Value);
            }

            // remove all windows from the dictionary and reset the id
            browserWindows.Clear();
            lastBrowserID = 0;

            // create the initial window
            SetBrowserActive(true);

            // hide the browser windows if the browser was hidden before
            if (!isVisible)
            {
                SetVisible(false);
            }

        } // end ResetBrowser

        /// <summary>
        /// A listener of the web manager to call once the graphs have been loaded for a particular data set
        /// </summary>
        private void CreateBrowserSession()
        {
            // first set up the browser buttons as active
            webBrowserVisibilityButton.SetButtonActivated(true);
            resetWebBrowserButton.SetButtonActivated(true);

            // show the browser window(s)
            // TODO: add code to load the last session data from the Data folder for this selection
            webBrowserVisibilityButton.Click();

            // go through the graphs and mark them as hidden to begin with
            foreach (Graph graph in referenceManager.graphManager.Graphs)
            {
                graph.HideGraph();
            }

        } // end CreateBrowserSession

        public void ClearBrowserSession()
        {
            // to set the laser pointers back to being not in use for now, check to see if they are
            if (isVisible)
            {
                //webBrowserVisibilityButton.Click();
                SetVisible(false);
            }

            // first set up the browser buttons as inactive
            webBrowserVisibilityButton.SetButtonActivated(false);
            resetWebBrowserButton.SetButtonActivated(false);

            // TODO: leaving current state of web browsers for now, need to make it so it loads from data

        } // end ClearBrowserSession
    }
}