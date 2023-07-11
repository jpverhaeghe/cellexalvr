using CellexalVR.AnalysisObjects;

namespace CellexalVR.Menu.Buttons.Tools
{
    /// <summary>
    /// Resets the active browser windows to a single window. 
    /// This also activates the laser on the right hand so that one can interact with the browser.
    /// </summary>
    public class WebBrowserResetButton : CellexalButton //CellexalToolButton
    {
        private void Start()
        {
            SetButtonActivated(false);
        }

        protected override string Description
        {
            get { return "Reset Web Browsers to Default"; }
        }

        public override void Click()
        {
            // TODO: set up a message for Multi-player to reset browser windows
            //referenceManager.multiuserMessageSender.SendMessageActivateBrowser(toolActivated);

            // reset the browser on this machine to the original state
            referenceManager.webManager.GetComponent<WebManager>().ResetBrowser();
            //CellexalLog.Log("Reset web browser windows to oringial state - one window open");

        } // end Click

    }
}