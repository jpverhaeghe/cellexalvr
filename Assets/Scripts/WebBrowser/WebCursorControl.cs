using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Vuplex.WebView;

public class WebCursorControl : MonoBehaviour
{
    // Offsets for where the pointer tip is in the 32x32 icon (from bottom-left)
    private const int POINTER_X_OFFSET = 11;
    private const int POINTER_Y_OFFSET = 27;

    // used to get access to the entire canvas browser - though only doing the main window for now
    [SerializeField] FullCanvasWebBrowserManager canvasBrowser;

    // a reference to the main canvas web view to make referencing it easier (may not need)
    private CanvasWebViewPrefab canvasWebView;

    // don't want to update the mouse position if the browser isn't active yet
    private bool canUpdate;

    /// <summary>
    /// Start is called before the first frame update to set up the variables, etc.
    /// </summary>
    async void Start()
    {
        canUpdate = false;
        canvasWebView = canvasBrowser._canvasWebViewPrefab;
        await canvasWebView.WaitUntilInitialized();
        canUpdate = true;

    } // end Start

    /// <summary>
    /// Called once per frame, using for input handling
    /// </summary>
    private void Update()
    {
        if (canUpdate)
        {
            // set up click functionality based on this position using iWebView click
            if (Input.GetMouseButtonDown(0))
            {
                // get the cursor position in relation to the window position
                Vector2Int screenPos = new Vector2Int((int)transform.localPosition.x, (int)transform.localPosition.y);

                // as the rect transform starts at the bottom left, we have to convert it to the top left
                screenPos.y = (int)canvasWebView.gameObject.GetComponent<RectTransform>().rect.height - screenPos.y;

                // now offset for where the tip of the pointer is
                screenPos.x += POINTER_X_OFFSET;
                screenPos.y -= POINTER_Y_OFFSET;

                // send a click event at the cursor local position
                canvasWebView.WebView.Click(screenPos.x, screenPos.y);
            }
        }

    } // end Update

    /// <summary>
    /// Fixed Update is used to do movement of data
    /// </summary>
    void FixedUpdate()
    {
        if (canUpdate)
        {
            Vector2 mousePosition = Input.mousePosition;

            // contrain the mouse cursor to the window
            if (mousePosition.x < 0)
                mousePosition.x = 0;

            if (mousePosition.y < 0)
                mousePosition.y = 0;

            if (mousePosition.x > canvasWebView.WebView.Size.x)
                mousePosition.x = canvasWebView.WebView.Size.x;

            if (mousePosition.y > canvasWebView.WebView.Size.y)
                mousePosition.y = canvasWebView.WebView.Size.y;

            // set the new mouse position based on the canvas web view prefab
            Vector3 position = new Vector3(mousePosition.x + canvasWebView.transform.position.x,
                                           mousePosition.y + canvasWebView.transform.position.y,
                                           canvasWebView.transform.position.z);
            transform.localPosition = position;
        }

    } // end FixedUpdate

}
