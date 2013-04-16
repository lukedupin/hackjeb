using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace MuMech
{
    public class MechJebModulePlanitron : ComputerModule
    {
      string _planetName = "Kerbin";
      bool _loaded = false;
      CelestialBody _kerbin = new CelestialBody();

        public MechJebModulePlanitron(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Planitron";
        }

        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(300) };
        }

        protected override void WindowGUI(int windowID)
        {
            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;
            GUIStyle txtR_Ex = new GUIStyle(GUI.skin.label);
            txtR_Ex.alignment = TextAnchor.UpperRight;
            txtR_Ex.stretchWidth = true;
            txtR_Ex.margin.right = 16;

            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = txtR.padding;
            sty.margin = txtR.margin;

              //Ensure we are still on Kerbin
            if (part.vessel.mainBody.GetName() != "Kerbin")
            {
              GUILayout.BeginVertical();
              GUILayout.Label("Planitron only works while on Kerbin", txtR);
              GUILayout.EndHorizontal();
              base.WindowGUI(windowID);
              return;
            }

              //If we haven't saved our default kerbin variables, do so
            if (!_loaded)
            {
              setPlanet(part.vessel.mainBody, _kerbin);
              _loaded = true;
            }

              //Go through each planet
            GUILayout.BeginHorizontal();

              //Planet selector
            GUILayout.BeginVertical();
            GUILayout.Label("Celestial Body", txtR);
            foreach (CelestialBody c in FlightGlobals.Bodies)
            {
              bool v_in = (c.GetName() == _planetName);
              bool v_out = GUILayout.Toggle( v_in, c.GetName(), sty);
              if (v_out == true)
              {
                _planetName = c.GetName();
                if (_planetName == "Kerbin")
                  setPlanet(_kerbin, part.vessel.mainBody);
                else
                  setPlanet(c, part.vessel.mainBody);
              }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("G's", txtR_Ex);
            foreach (CelestialBody c in FlightGlobals.Bodies)
              GUILayout.Label((("Kerbin" == c.GetName())? _kerbin: c).GeeASL.ToString("N3"), txtR_Ex);
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.Label("Atmosphere", txtR);
            foreach (CelestialBody c in FlightGlobals.Bodies)
              if ( (("Kerbin" == c.GetName())? _kerbin: c).atmosphere )
                GUILayout.Label((("Kerbin" == c.GetName())? _kerbin: c).atmosphereMultiplier.ToString("N3"), txtR);
              else
                GUILayout.Label( "None", txtR);
            GUILayout.EndVertical();

            GUILayout.EndHorizontal();

            base.WindowGUI(windowID);
        }

          //Lost the control
        public override void  onModuleDisabled()
        {
          if (_loaded)
            setPlanet(_kerbin, part.vessel.mainBody);
           base.onModuleDisabled();
        }

          //Copy planet data from one to another
        private void setPlanet(CelestialBody b, CelestialBody v)
        {
          v.atmoshpereTemperatureMultiplier = b.atmoshpereTemperatureMultiplier;
          v.atmosphere = b.atmosphere;
          v.atmosphereContainsOxygen = b.atmosphereContainsOxygen;
          v.atmosphereMultiplier = b.atmosphereMultiplier;
          v.atmosphereScaleHeight = b.atmosphereScaleHeight;
          v.atmosphericAmbientColor = b.atmosphericAmbientColor;

          v.GeeASL = b.GeeASL;
          v.gMagnitudeAtCenter = b.gMagnitudeAtCenter;
          v.gravParameter = b.gravParameter;
          v.Mass = b.Mass;
          v.pressureMultiplier = b.pressureMultiplier;
          v.staticPressureASL = b.staticPressureASL;
  }
    }
}
