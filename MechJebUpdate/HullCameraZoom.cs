using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace MuMech
{
    public class MuMechModuleHullCameraZoom : MuMechModuleHullCamera
    {
        [KSPField]
        public float cameraFoVMax = 120;

        [KSPField]
        public float cameraFoVMin = 5;

        [KSPField]
        public float cameraZoomMult = 1.25f;

        [KSPAction("Zoom In")]
        public void ZoomInAction(KSPActionParam ap)
        {
            globalInput |= 1024;
        }

        [KSPAction("Zoom Out")]
        public void ZoomOutAction(KSPActionParam ap)
        {
            globalInput |= 2048;
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            
            if (vessel == null)
            {
                return;
            }

            if (((globalInput & 1024) != 0) || GameSettings.ZOOM_IN.GetKeyDown() || (Input.GetAxis("Mouse ScrollWheel") > 0))
            {
                cameraFoV = Mathf.Clamp(cameraFoV / cameraZoomMult, cameraFoVMin, cameraFoVMax);
                globalInput -= 1024;
            }
            if ((globalInput & 2048) != 0 || GameSettings.ZOOM_OUT.GetKeyDown() || (Input.GetAxis("Mouse ScrollWheel") < 0))
            {
                cameraFoV = Mathf.Clamp(cameraFoV * cameraZoomMult, cameraFoVMin, cameraFoVMax);
                globalInput -= 2048;
            }
        }
    }
}
