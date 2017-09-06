﻿using UnityEngine;

/// <summary>
/// Abstract class for all buttons that do not rotate when pressed.
/// </summary>
public abstract class StationaryButton : MonoBehaviour
{
    public SteamVR_TrackedObject rightController;
    public TextMesh descriptionText;
    public Sprite standardTexture;
    public Sprite highlightedTexture;
    // all buttons must override this variable's get property
    /// <summary>
    /// A string that briefly explains what this button does.
    /// </summary>
    abstract protected string Description
    {
        get;
    }

    protected SteamVR_Controller.Device device;
    protected bool controllerInside;
    protected SpriteRenderer spriteRenderer;

    // virtual so other classes may override if needed
    protected virtual void Awake()
    {
        device = SteamVR_Controller.Input((int)rightController.index);
        spriteRenderer = gameObject.GetComponent<SpriteRenderer>();
    }

    protected virtual void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Controller"))
        {
            descriptionText.text = Description;
            spriteRenderer.sprite = highlightedTexture;
            controllerInside = true;
        }
    }

    protected virtual void OnTriggerExit(Collider other)
    {
        if (other.gameObject.CompareTag("Controller"))
        {
            // sometimes the controller moves to another button before exiting this one.
            // that other button will then (probably) change the description.
            // so we only change it back to nothing if that has not happened.
            if (descriptionText.text.Equals(Description))
            {
                descriptionText.text = "";
            }
            spriteRenderer.sprite = standardTexture;
            controllerInside = false;
        }
    }

}

