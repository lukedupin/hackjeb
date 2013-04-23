using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using System.Diagnostics;


namespace MuMech
{
    public class MechJebModuleVesselInfo : ComputerModule
    {
        static MechJebModuleVesselInfo buildSceneDrawer = null;

        MechJebModulePlanitron _planitron = null;

        bool _buildSceneShow = true;
        bool buildSceneShow
        {
            get { return _buildSceneShow; }
            set
            {
                if (value != _buildSceneShow) core.settingsChanged = true;
                _buildSceneShow = value;
            }
        }

        bool _buildSceneMinimized = false;
        bool buildSceneMinimized
        {
            get { return _buildSceneMinimized; }
            set
            {
                if (value != _buildSceneMinimized) core.settingsChanged = true;
                _buildSceneMinimized = value;
            }
        }

        public override void onLoadGlobalSettings(SettingsManager settings)
        {
            base.onLoadGlobalSettings(settings);

            buildSceneShow = settings["VI_buildSceneShow"].valueBool(true);
            buildSceneMinimized = settings["VI_buildSceneMinimized"].valueBool(false);
        }

        public override void onSaveGlobalSettings(SettingsManager settings)
        {
            base.onSaveGlobalSettings(settings);

            settings["VI_buildSceneShow"].value_bool = buildSceneShow;
            settings["VI_buildSceneMinimized"].value_bool = buildSceneMinimized;
        }


        FuelFlowAnalyzer ffa = new FuelFlowAnalyzer();

        public MechJebModuleVesselInfo(MechJebCore core) : base(core) { }

//        float[] timePerStageAtmo = new float[0];
//        float[] deltaVPerStageAtmo = new float[0];
        float[] timePerStageVac = new float[0];
        float[] deltaVPerStageVac = new float[0];
        float[] twrPerStage = new float[0];
        float[] timePerStageCur = new float[0];
        float[] deltaVPerStageCur = new float[0];
        float[] twrPerStageCur = new float[0];

        Stopwatch nextSimulationTimer = new Stopwatch();
        double nextSimulationDelayMs = 0;
        public override void onPartFixedUpdate()
        {
            if (!this.enabled || !part.vessel.isActiveVessel) return;

            runSimulations();
        }

        void runSimulations()
        {
            if (((TimeWarp.WarpMode == TimeWarp.Modes.LOW) || (TimeWarp.CurrentRate <= TimeWarp.MaxPhysicsRate)) &&
                (nextSimulationDelayMs == 0 || nextSimulationTimer.ElapsedMilliseconds > nextSimulationDelayMs))
            {
                Stopwatch s = Stopwatch.StartNew();
                double surfaceGravity;
                double atmos = 1.0;
                List<Part> parts;
                bool cur_thrust = true;
                if (part.vessel == null)
                {
                  cur_thrust = false;
                    parts = EditorLogic.SortedShipList;
                    if (_planitron != null)
                    {
                      surfaceGravity = 9.81 * _planitron.gravity();
                      atmos = _planitron.atmos();
                    }
                    else
                      surfaceGravity = 9.81;
                }
                else
                {
                    parts = part.vessel.parts;
                    surfaceGravity = part.vessel.mainBody.GeeASL * 9.81;
                    atmos = vesselState.atmosphericDensity;
                }
//                ffa.analyze(parts, (float)surfaceGravity, 1.0F, false, out timePerStageAtmo, out deltaVPerStageAtmo, out twrPerStage);
                ffa.analyze(parts, (float)surfaceGravity, 0.0F, false, out timePerStageVac, out deltaVPerStageVac, out twrPerStage);
                ffa.analyze(parts, (float)surfaceGravity, (float)atmos, cur_thrust, out timePerStageCur, out deltaVPerStageCur, out twrPerStageCur);
                s.Stop();

                nextSimulationDelayMs = 1000;// 10 * s.ElapsedMilliseconds;
                nextSimulationTimer.Reset();
                nextSimulationTimer.Start();
            }
        }

        public override string getName()
        {
            return "Vessel Information";
        }


        public override void onPartStart()
        {
            //a bit of a hack to detect when we start up attached to a rocket
            //that just got loaded into the VAB:
            if (part.vessel == null && (part.parent != null || part is CommandPod))
            {
                RenderingManager.AddToPostDrawQueue(0, drawBuildSceneGUI);
            }
        }

        public override void onPartAttach(Part parent)
        {
            RenderingManager.AddToPostDrawQueue(0, drawBuildSceneGUI);
        }

        public override void onPartDetach()
        {
            if (buildSceneDrawer == this) buildSceneDrawer = null;
            RenderingManager.RemoveFromPostDrawQueue(0, drawBuildSceneGUI);
        }

        public override void onPartDestroy()
        {
            if (buildSceneDrawer == this) buildSceneDrawer = null;
            RenderingManager.RemoveFromPostDrawQueue(0, drawBuildSceneGUI);
        }

        public override void onPartDelete()
        {
            if (buildSceneDrawer == this) buildSceneDrawer = null;
            RenderingManager.RemoveFromPostDrawQueue(0, drawBuildSceneGUI);
        }


        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(300), GUILayout.Height(100) };
        }

        GUILayoutOption[] minimizedWindowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(100), GUILayout.Height(30) };
        }

        protected override void WindowGUI(int windowID)
        {
            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;

            GUILayout.BeginVertical();

            buildSceneShow = GUILayout.Toggle(buildSceneShow, "Show in VAB");

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Total mass", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.mass.ToString("F2") + " tons", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Total thrust", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.thrust.ToString("F0") + " kN", txtR);
            //GUILayout.Label(vesselState.thrustAvailable.ToString("F0") + " kN", txtR);
            GUILayout.EndHorizontal();

            //double gravity = part.vessel.mainBody.gravParameter / Math.Pow(part.vessel.mainBody.Radius + vesselState.altitudeASL, 2);
            double TWR = -1;// vesselState.thrust / (vesselState.mass * part.vessel.mainBody.GeeASL);
            for (int stage = timePerStageVac.Length - 1; TWR < 0 && stage >= 0; stage--)
                if (timePerStageVac[stage] > 0)
                  TWR = twrPerStageCur[stage];
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Current TWR", GUILayout.ExpandWidth(true));
            GUILayout.Label(TWR.ToString("F2"), txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Acceleration", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.surface_accel.ToString("F2"), txtR);
            GUILayout.EndHorizontal();                 

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Acceleration Δa", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.surface_accel_delta.ToString("F2"), txtR);
            GUILayout.EndHorizontal();                 

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Time to zero velocity", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.stop_time.ToString("F2"), txtR);
            GUILayout.EndHorizontal();                 

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Distance to zero velocity", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.stop_distance.ToString("F2"), txtR);
            GUILayout.EndHorizontal();                 

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("True Altitude", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.altitudeTrue.value.ToString("F2"), txtR);
            GUILayout.EndHorizontal();

            //Get how long I will burn currently
            double timed = 1;
            for (int stage = timePerStageVac.Length - 1; stage >= 0; stage--)
              if (timePerStageVac[stage] > 0)
              {
                timed = timePerStageCur[stage];
                stage = -1;
              }

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Engine Efficiency", GUILayout.ExpandWidth(true));
            if ( vesselState.fuelConsumption != 0)
              GUILayout.Label((vesselState.surface_accel * timed / vesselState.fuelConsumption).ToString("F2") +" a/f", txtR);
            else
              GUILayout.Label("N/A", txtR);
            GUILayout.EndHorizontal();                 

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Fuel Flow", GUILayout.ExpandWidth(true));
            GUILayout.Label(vesselState.fuelConsumption.ToString("F2"), txtR);
            GUILayout.EndHorizontal();                 

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Time Scale "+ Time.timeScale.ToString("N2") +"x", GUILayout.ExpandWidth(true));
            float val = GUILayout.HorizontalSlider(Time.timeScale, 0.05F, 1.0F, GUILayout.ExpandWidth(true));
            if (Time.timeScale <= 1.0F) 
              Time.timeScale = val;
            GUILayout.EndHorizontal();                 

            doStagingAnalysisGUI();

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }

        void drawBuildSceneGUI()
        {
            //in the VAB, onPartFixedUpdate doesn't get called, so
            //settings changes don't get saved unless we do this:
            if (core.settingsChanged) core.saveSettings();

            if (buildSceneDrawer == null)
            {
                buildSceneDrawer = this;
            }

            if (buildSceneDrawer == this && buildSceneShow)
            {
                runSimulations();

                GUI.skin = MuUtils.DefaultSkin;
                if (buildSceneMinimized)
                {
                    windowPos = GUILayout.Window(872035, windowPos, buildSceneWindowGUI, "Vessel Info", minimizedWindowOptions());
                }
                else
                {
                    windowPos = GUILayout.Window(872035, windowPos, buildSceneWindowGUI, getName(), windowOptions());
                }

                  //Show the planitron
                if ( _planitron != null && _planitron.enabled )
                  _planitron.drawVAB(917);
            }
        }

        protected void buildSceneWindowGUI(int windowID)
        {
            preventEditorClickthroughs();

            if (buildSceneMinimized)
            {
                GUILayout.BeginHorizontal();
                if (GUILayout.Button("Max")) buildSceneMinimized = false;
                if (GUILayout.Button("X", ARUtils.buttonStyle(Color.red)))
                {
                    if (weLockedEditor && EditorLogic.editorLocked)
                    {
                        EditorLogic.fetch.Unlock();
                    }
                    buildSceneShow = false;
                }
                GUILayout.EndHorizontal();
                base.WindowGUI(windowID);
                return;
            }


            double mass = 0;
            int cost = 0;
            foreach (Part part in EditorLogic.SortedShipList)
            {
                cost += part.partInfo.cost;

                if (part.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    mass += part.totalMass();
                }

                //In the VAB, ModuleJettison (which adds fairings) forgets to subtract the fairing mass from
                //the part mass if the engine does have a fairing, so we have to do this manually
                if (part.vessel == null //hacky way to tell whether we're in the VAB
                    && (part.Modules.OfType<ModuleJettison>().Count() > 0))
                {
                    ModuleJettison jettison = part.Modules.OfType<ModuleJettison>().First();
                    if (part.findAttachNode(jettison.bottomNodeName).attachedPart == null)
                    {
                        mass -= jettison.jettisonedObjectMass;
                    }
                }

            }

            double TWR = 0;
            if (twrPerStage.Length > 0) TWR = twrPerStage[twrPerStage.Length - 1];

            int partCount = EditorLogic.SortedShipList.Count;

            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;

            GUIStyle sty = new GUIStyle(GUI.skin.button);
            sty.normal.textColor = sty.focused.textColor = Color.white;
            sty.hover.textColor = sty.active.textColor = Color.yellow;
            sty.onNormal.textColor = sty.onFocused.textColor = sty.onHover.textColor = sty.onActive.textColor = Color.green;
            sty.padding = txtR.padding;
            sty.margin = txtR.margin;


            GUILayout.BeginVertical();


            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Minimize"))
            {
                buildSceneMinimized = true;
            }
            if (GUILayout.Button("Close", ARUtils.buttonStyle(Color.red)))
            {
                if (weLockedEditor && EditorLogic.editorLocked)
                {
                    EditorLogic.fetch.Unlock();
                }
                buildSceneShow = false;
            }
            GUILayout.EndHorizontal();

                //Alloc my planitron
            if (_planitron == null)
              _planitron = new MechJebModulePlanitron(core);

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Show planitron", GUILayout.ExpandWidth(true));
            _planitron.enabled = GUILayout.Toggle(_planitron.enabled, _planitron.getPlanet(), sty);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Total mass", GUILayout.ExpandWidth(true));
            GUILayout.Label(mass.ToString("F2") + " tons", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Surface weight", GUILayout.ExpandWidth(true));
            GUILayout.Label((mass * _planitron.gravity()).ToString("F2") + " tons", txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Surface TWR", GUILayout.ExpandWidth(true));
            GUILayout.Label(TWR.ToString("F2"), txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Part count", GUILayout.ExpandWidth(true));
            GUILayout.Label(partCount.ToString(), txtR);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Total Cost", GUILayout.ExpandWidth(true));
            GUILayout.Label(cost.ToString(), txtR);
            GUILayout.EndHorizontal();

            doStagingAnalysisGUI( true );

            GUILayout.EndVertical();

            base.WindowGUI(windowID);
        }


        protected void doStagingAnalysisGUI( bool vab = false )
        {
            GUIStyle txtR = new GUIStyle(GUI.skin.label);
            txtR.alignment = TextAnchor.UpperRight;

            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label("Staging analysis:", GUILayout.ExpandWidth(true));
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Stage");
            GUILayout.EndHorizontal();
            for (int stage = timePerStageVac.Length - 1; stage >= 0; stage--)
            {
                if (timePerStageVac[stage] > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(String.Format("{0:0}", stage), txtR);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("TWR");
            GUILayout.EndHorizontal();
            for (int stage = timePerStageVac.Length - 1; stage >= 0; stage--)
            {
                if (timePerStageVac[stage] > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(String.Format("{0:0.00}", twrPerStage[stage]), txtR);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            if (vab)
            {
              GUILayout.BeginVertical();
              GUILayout.BeginHorizontal();
              GUILayout.FlexibleSpace();
              GUILayout.Label("Atmo. Δv");
              GUILayout.EndHorizontal();
              for (int stage = timePerStageCur.Length - 1; stage >= 0; stage--)
              {
                if (timePerStageCur[stage] > 0)
                {
                  GUILayout.BeginHorizontal();
                  GUILayout.FlexibleSpace();
                  GUILayout.Label(String.Format("{0:0} m/s", deltaVPerStageCur[stage]), txtR);
                  GUILayout.EndHorizontal();
                }
              }
              GUILayout.EndVertical();

              GUILayout.BeginVertical();
              GUILayout.BeginHorizontal();
              GUILayout.FlexibleSpace();
              GUILayout.Label("Atmo T");
              GUILayout.EndHorizontal();
              for (int stage = timePerStageCur.Length - 1; stage >= 0; stage--)
              {
                if (timePerStageCur[stage] > 0)
                {
                  GUILayout.BeginHorizontal();
                  GUILayout.FlexibleSpace();
                  GUILayout.Label(formatTime(timePerStageCur[stage]), txtR);
                  GUILayout.EndHorizontal();
                }
              }
              GUILayout.EndVertical();
            }

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Vac. Δv");
            GUILayout.EndHorizontal();
            for (int stage = timePerStageVac.Length - 1; stage >= 0; stage--)
            {
                if (timePerStageVac[stage] > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(String.Format("{0:0} m/s", deltaVPerStageVac[stage]), txtR);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            GUILayout.BeginVertical();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Min T");
            GUILayout.EndHorizontal();
            for (int stage = timePerStageVac.Length - 1; stage >= 0; stage--)
            {
                if (timePerStageVac[stage] > 0)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.FlexibleSpace();
                    GUILayout.Label(formatTime(timePerStageVac[stage]), txtR);
                    GUILayout.EndHorizontal();
                }
            }
            GUILayout.EndVertical();

            if (!vab)
            {
              GUILayout.BeginVertical();
              GUILayout.BeginHorizontal();
              GUILayout.FlexibleSpace();
              GUILayout.Label("Current T");
              GUILayout.EndHorizontal();
              for (int stage = timePerStageVac.Length - 1; stage >= 0; stage--)
              {
                if (timePerStageVac[stage] > 0)
                {
                  GUILayout.BeginHorizontal();
                  GUILayout.FlexibleSpace();
                  GUILayout.Label(formatTime(timePerStageCur[stage]), txtR);
                  GUILayout.EndHorizontal();
                }
              }
              GUILayout.EndVertical();
            }

            GUILayout.EndHorizontal();
        }


        static String formatTime(float seconds)
        {
            if (seconds < 300)
            {
                return String.Format("{0:0} s", seconds);
            }
            else if (seconds < 3600)
            {
                int minutes = (int)(seconds / 60);
                float remainingSeconds = seconds - 60 * minutes;
                return String.Format("{0:0}:{1:00}", minutes, remainingSeconds);
            }
            else if (seconds < 3600 * 24)
            {
                int hours = (int)(seconds / 3600);
                int minutes = (int)((seconds - 3600 * hours) / 60);
                float remainingSeconds = seconds - 3600 * hours - 60 * minutes;
                return String.Format("{0:0}:{1:00}:{2:00}", hours, minutes, remainingSeconds);
            }
            else
            {
                int days = (int)(seconds / (3600 * 24));
                int hours = (int)((seconds - days*3600*24) / 3600);
                int minutes = (int)((seconds - days*3600*24 - 3600 * hours) / 60);
                float remainingSeconds = seconds - days*3600*24 - 3600 * hours - 60 * minutes;
                return String.Format("{0:0}:{1:00}:{2:00}:{3:00}", days, hours, minutes, remainingSeconds);
            }
        }


        //Lifted this more or less directly from the Kerbal Engineer source. Thanks cybutek!
        bool weLockedEditor = false;
        void preventEditorClickthroughs()
        {
            Vector2 mousePos = Input.mousePosition;
            mousePos.y = Screen.height - mousePos.y;
            if (windowPos.Contains(mousePos) && !EditorLogic.editorLocked)
            {
                EditorLogic.fetch.Lock(true, true, true);
                weLockedEditor = true;
            }
            if (weLockedEditor && !windowPos.Contains(mousePos) && EditorLogic.editorLocked)
            {
                EditorLogic.fetch.Unlock();
            }
            if (!EditorLogic.editorLocked) weLockedEditor = false;
        }
    }
}
