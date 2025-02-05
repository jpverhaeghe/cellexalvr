using UnityEngine;
namespace CellexalVR.Menu.Buttons.Attributes
{
    /// <summary>
    /// Represents the button that closes the attribute menu.
    /// </summary>
    public class CloseAttributeMenuButton : CellexalButton
    {
        private GameObject attributeMenu;
        private GameObject buttons;

        protected override string Description
        {
            get { return "Close attribute menu"; }
        }

        private void Start()
        {
            attributeMenu = referenceManager.attributeSubMenu.gameObject;
            buttons = referenceManager.leftButtons;
        }


        public override void Click()
        {
            spriteRenderer.sprite = standardTexture;
            controllerInside = false;
            attributeMenu.SetActive(false);
            buttons.SetActive(true);
        }
    }

}