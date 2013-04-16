using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

public class MuMechModuleHullCamera : PartModule
{
    private const bool adjustMode = false;

    [KSPField]
    public Vector3 cameraPosition = Vector3.zero;

    [KSPField]
    public Vector3 cameraForward = Vector3.forward;

    [KSPField]
    public Vector3 cameraUp = Vector3.up;

    [KSPField]
    public string cameraTransformName = "";

    [KSPField]
    public float cameraFoV = 60;

    [KSPField(isPersistant = false)]
    public float cameraClip = 0.01f;

    [KSPField]
    public bool camActive = false;

    [KSPField]
    public bool camEnabled = true;

    [KSPField(isPersistant = false)]
    public string cameraName = "Hull";

    public static List<MuMechModuleHullCamera> cameras = new List<MuMechModuleHullCamera>();
    public static MuMechModuleHullCamera currentCamera = null;
    public static MuMechModuleHullCamera currentHandler = null;

    protected static FlightCamera cam = null;
    protected static Transform origParent = null;
    protected static float origFoV;
    protected static float origClip;
    protected static Texture2D overlayTex = null;

    protected static int globalInput = 0;

    public void toMainCamera()
    {
        if ((cam != null) && (cam.transform != null))
        {
            cam.transform.parent = origParent;
            Camera.mainCamera.nearClipPlane = origClip;
            foreach (Camera c in Camera.allCameras)
            {
                c.fov = origFoV;
            }
            cam.setTarget(FlightGlobals.ActiveVessel.transform);

            if (currentCamera != null)
            {
                currentCamera.camActive = false;
            }
            currentCamera = null;
            MapView.EnterMapView();
            MapView.ExitMapView();
        }
    }

    [KSPEvent(guiActive = true, guiName = "Activate Camera")]
    public void ActivateCamera()
    {
        if (part.State == PartStates.DEAD)
        {
            return;
        }

        camActive = !camActive;

        if (!camActive && (cam != null))
        {
            toMainCamera();
        }
        else
        {
            if ((currentCamera != null) && (currentCamera != this))
            {
                currentCamera.camActive = false;
            }
            currentCamera = this;
        }
    }

    [KSPEvent(guiActive = true, guiName = "Disable Camera")]
    public void EnableCamera()
    {
        if (part.State == PartStates.DEAD)
        {
            return;
        }

        camEnabled = !camEnabled;

        if (camEnabled)
        {
            if (!cameras.Contains(this))
            {
                cameras.Add(this);
            }
        }
        else
        {
            if (cameras.Contains(this))
            {
                cameras.Remove(this);
            }
        }

        if (!camEnabled && camActive)
        {
            toMainCamera();
        }
    }

    [KSPAction("Activate Camera")]
    public void ActivateCameraAction(KSPActionParam ap)
    {
        ActivateCamera();
    }

    [KSPAction("Deactivate Camera")]
    public void DeactivateCameraAction(KSPActionParam ap)
    {
        globalInput |= 1;
    }

    [KSPAction("Next Camera")]
    public void NextCameraAction(KSPActionParam ap)
    {
        globalInput |= 2;
    }

    [KSPAction("Previous Camera")]
    public void PreviousCameraAction(KSPActionParam ap)
    {
        globalInput |= 4;
    }

    public void Update()
    {
        if (vessel == null)
        {
            return;
        }

        Events["ActivateCamera"].guiName = camActive ? "Deactivate Camera" : "Activate Camera";
        Events["EnableCamera"].guiName = camEnabled ? "Disable Camera" : "Enable Camera";

        if (currentHandler == null)
        {
            currentHandler = this;
        }

        if (currentHandler == this)
        {
            cameras.RemoveAll(item => item == null);
        }

        if ((globalInput & 1) != 0)
        {
            toMainCamera();
            globalInput -= 1;
        }
        if (((globalInput & 2) != 0) || Input.GetKeyDown(KeyCode.F7))
        {
            if (currentCamera != null)
            {
                int curCam = cameras.IndexOf(currentCamera);
                if (curCam + 1 >= cameras.Count)
                {
                    toMainCamera();
                }
                else
                {
                    cameras[curCam + 1].ActivateCamera();
                }
            }
            else
            {
                cameras.First().ActivateCamera();
            }
            globalInput -= 2;
        }
        if (((globalInput & 4) != 0) || Input.GetKeyDown(KeyCode.F8))
        {
            if (currentCamera != null)
            {
                int curCam = cameras.IndexOf(currentCamera);
                if (curCam < 1)
                {
                    toMainCamera();
                }
                else
                {
                    cameras[curCam - 1].ActivateCamera();
                }
            }
            else
            {
                cameras.Last().ActivateCamera();
            }
            globalInput -= 4;
        }
        
        if ((currentCamera == this) && adjustMode) {
            if (Input.GetKeyUp(KeyCode.Keypad8))
            {
                cameraPosition += cameraUp * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad2))
            {
                cameraPosition -= cameraUp * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad6))
            {
                cameraPosition += cameraForward * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad4))
            {
                cameraPosition -= cameraForward * 0.1f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad7))
            {
                cameraClip += 0.05f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad1))
            {
                cameraClip -= 0.05f;
            }

            if (Input.GetKeyUp(KeyCode.Keypad9))
            {
                cameraFoV += 5;
            }

            if (Input.GetKeyUp(KeyCode.Keypad3))
            {
                cameraFoV -= 5;
            }

            if (Input.GetKeyUp(KeyCode.KeypadMinus))
            {
                print("Position: " + cameraPosition + " - Clip = " + cameraClip + " - FoV = " + cameraFoV);
            }
        }
    }

    public void FixedUpdate()
    {
        if (vessel == null)
        {
            return;
        }

        if (cam == null)
        {
            cam = FlightCamera.fetch;
        }

        if ((cam != null) && (origParent == null))
        {
            origParent = cam.transform.parent;
            origClip = Camera.mainCamera.nearClipPlane;
            origFoV = Camera.mainCamera.fov;
        }

        if (camActive && (part.State == PartStates.DEAD))
        {
            CleanUp();
        }

        if (part.State == PartStates.DEAD)
        {
            camEnabled = false;
        }

        if ((part.State == PartStates.DEAD) && cameras.Contains(this))
        {
            CleanUp();
        }

        if (!cameras.Contains(this) && (part.State != PartStates.DEAD))
        {
            cameras.Add(this);
        }

        if ((origParent != null) && (cam != null) && camActive)
        {
            cam.setTarget(null);
            cam.transform.parent = (cameraTransformName.Length > 0) ? part.FindModelTransform(cameraTransformName) : part.transform;
            cam.transform.localPosition = cameraPosition;
            cam.transform.localRotation = Quaternion.LookRotation(cameraForward, cameraUp);
            foreach (Camera c in Camera.allCameras)
            {
                c.fov = cameraFoV;
            }
            Camera.mainCamera.nearClipPlane = cameraClip;
        }

        base.OnFixedUpdate();
    }

    public override void OnStart(StartState state)
    {
        if (camEnabled && (state != StartState.None) && (state != StartState.Editor))
        {
            if (!cameras.Contains(this))
            {
                cameras.Add(this);
            }
            vessel.OnJustAboutToBeDestroyed += CleanUp;
        }
        part.OnJustAboutToBeDestroyed += CleanUp;
        part.OnEditorDestroy += CleanUp;

        base.OnStart(state);
    }

    public void CleanUp()
    {
        if (camActive)
        {
            toMainCamera();
        }

        if (currentCamera == this)
        {
            currentCamera = null;
        }

        if (currentHandler == this)
        {
            currentHandler = null;
        }

        if (cameras.Contains(this))
        {
            cameras.Remove(this);
        }
    }

    public void OnDestroy()
    {
        CleanUp();
    }
}
