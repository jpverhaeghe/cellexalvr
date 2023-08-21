using CellexalVR.AnalysisObjects;
using CellexalVR.General;
using CellexalVR.Interaction;

namespace CellexalVR.Menu.Buttons.Tools
{
    /// <summary>
    /// Toggle on/off the web browser windows. 
    /// This also activates the laser on the right hand so that one can interact with the browser.
    /// </summary>
    public class WebBrowserButton : CellexalToolButton
    {
        private void Start()
        {
            SetButtonActivated(false);
            CellexalEvents.GraphsUnloaded.RemoveListener(TurnOff);
        }

        protected override string Description
        {
            get { return "Hide/Show Web Browser"; }
        }

        protected override ControllerModelSwitcher.Model ControllerModel
        {
            //get { return ControllerModelSwitcher.Model.WebBrowser; }
            get { return ControllerModelSwitcher.Model.TwoLasers; }
        }

        public override void Click()
        {
            base.Click();
            referenceManager.multiuserMessageSender.SendMessageActivateBrowser(toolActivated);
            referenceManager.webManager.GetComponent<WebManager>().ResetIfNoActiveBrowser(toolActivated);
            referenceManager.webManager.GetComponent<WebManager>().SetVisible(toolActivated);
            //CellexalLog.Log("Web client should start now!");

        } // end Click

    }
}