using UnityEngine;

public class MenuToggler : MonoBehaviour {

public SteamVR_TrackedObject trackedObject;
private SteamVR_Controller.Device device;
public GameObject menu;

// Use this for initialization
void Start () {
	device = SteamVR_Controller.Input((int)trackedObject.index);
}

// Update is called once per frame
void Update () {
	if (device.GetPressDown(SteamVR_Controller.ButtonMask.Trigger)) {
		menu.SetActive(!menu.activeSelf);
	}
}
}
