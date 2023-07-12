using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR.Interaction.Toolkit;

/// <summary>
/// An interactable that can be pressed by a direct interactor
/// </summary>
public class XRButton : XRBaseInteractable
{
    // constant values for this script
    private const float BUTTON_IMPULSE_STRENGTH = 0.5f;
    private const float BUTTON_IMPULSE_LENGTH = 0.15f;

    [Tooltip("The transform of the visual component of the button")]
    public Transform buttonTransform = null;

    [Tooltip("The distance the button can be pressed")]
    public float pressDistance = 0.1f;

    // When the button is pressed
    public UnityEvent OnPress = new UnityEvent();

    // When the button is released
    public UnityEvent OnRelease = new UnityEvent();

    private float yMin = 0.0f;
    private float yMax = 0.0f;

    private IXRHoverInteractor hoverInteractor = null;

    private float hoverHeight = 0.0f;
    private float startHeight = 0.0f;
    private bool previousPress = false;

    protected override void OnEnable()
    {
        base.OnEnable();
        hoverEntered.AddListener(StartPressing);
        hoverExited.AddListener(EndPress);
    }

    protected override void OnDisable()
    {
        base.OnDisable();
        hoverEntered.RemoveListener(StartPressing);
        hoverExited.RemoveListener(EndPress);
    }

    private void StartPressing(HoverEnterEventArgs eventArgs)
    {
        hoverInteractor = eventArgs.interactorObject;
        hoverHeight = GetLocalYPosition(hoverInteractor.transform.position);
        startHeight = buttonTransform.localPosition.y;
    }

    private void EndPress(HoverExitEventArgs eventArgs)
    {
        hoverInteractor = null;
        hoverHeight = 0.0f;
        startHeight = 0.0f;
        ApplyHeight(yMax);
    }

    private void Start()
    {
        SetMinMax();
    }

    private void SetMinMax()
    {
        yMin = buttonTransform.localPosition.y - pressDistance;
        yMax = buttonTransform.localPosition.y;
    }

    public override void ProcessInteractable(XRInteractionUpdateOrder.UpdatePhase updatePhase)
    {
        if(updatePhase == XRInteractionUpdateOrder.UpdatePhase.Dynamic)
        {
            if (hoverInteractor != null)
            {
                float height = FindButtonHeight();
                ApplyHeight(height);
            }
        }
    }

    private float FindButtonHeight()
    {
        float newHoverHeight = GetLocalYPosition(hoverInteractor.transform.position);
        float hoverDifference = hoverHeight - newHoverHeight;
        return startHeight - hoverDifference;
    }

    private float GetLocalYPosition(Vector3 position)
    {
        Vector3 localPosition = transform.InverseTransformPoint(position);
        return localPosition.y;
    }

    private void ApplyHeight(float position)
    {
        SetButtonPosition(position);
        CheckPress();
    }

    private void SetButtonPosition(float position)
    {
        Vector3 newPosition = buttonTransform.localPosition;
        newPosition.y = Mathf.Clamp(position, yMin, yMax);
        buttonTransform.localPosition = newPosition;
    }

    private void CheckPress()
    {
        bool inPosition = InPosition();

        if(inPosition != previousPress)
        {
            previousPress = inPosition;

            if(inPosition)
            {
                OnPress.Invoke();
                XRBaseControllerInteractor xrControllerUsed = (XRBaseControllerInteractor)hoverInteractor;
                xrControllerUsed.SendHapticImpulse(BUTTON_IMPULSE_STRENGTH, BUTTON_IMPULSE_LENGTH);
            }
            else
            {
                OnRelease.Invoke();
            }
        }
    }

    private bool InPosition()
    {
        float threshold = yMin + (pressDistance * 0.5f);
        return buttonTransform.localPosition.y < threshold;
    }

    /*public override bool IsSelectableBy(XRBaseInteractor interactor)
    {
        return false;
    }*/
}
