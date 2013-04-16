using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using OrbitExtensions;

namespace MuMech
{
    public class MechJebModuleRendezvous : ComputerModule
    {
        public MechJebModuleRendezvous(MechJebCore core) : base(core) { }

        public override string getName()
        {
            return "Rendezvous Module";
        }

        String statusString = "";

        //        bool comeAlongside = false;
        bool autoAlign = false;
        bool autoAlignBurnTriggered = false;
        bool autoAlignDidBurn = false;
        Vector3 autoAlignBurnDirection;

        #region Rendezvous State

        //        private Vector3 _relativeVelocity;
        //private float _relativeInclination = 0;
        //        private Vector3 _vectorToTarget;
        //        private float _targetDistance;

        //private bool _killRelativeVelocity = false;
        //private Vector3 _localRelativeVelocity = Vector3.zero;

        //private bool _homeOnRelativePosition = false;
        //private Vector3 _localRelativePosition = Vector3.zero;

        #endregion

        double[] warpLookaheadTimes = new double[] { 0, 2.5, 5, 25, 50, 500, 10000, 100000 };


        public override GUILayoutOption[] windowOptions()
        {
            return new GUILayoutOption[] { GUILayout.Width(300), GUILayout.Height(200) };
        }

        protected override void WindowGUI(int windowID)
        {
            if (FlightGlobals.fetch.VesselTarget == null)
            {
                GUILayout.Label("Select a target via the map screen.");
                GUI.DragWindow();
                return;
            }

            if (core.targetOrbit().referenceBody != part.vessel.mainBody)
            {
                GUILayout.Label("Target (" + core.targetName() + ") is orbiting another body.");
                GUI.DragWindow();
                return;
            }

            //Now we can be sure that there is a target and it's in the same SOI as us.

            GUILayout.BeginVertical();

            GUILayout.Label("Distance: " + MuUtils.ToSI(core.distanceFromTarget(), 3) + "m", GUILayout.Width(300));

            GUILayout.Label("Relative Velocity: " + core.relativeVelocityToTarget().magnitude.ToString("F2") + " m/s");

            double closestApproachTime = ARUtils.timeOfClosestApproach(part.vessel.orbit, core.targetOrbit(), vesselState.time);
            GUILayout.Label("Closest approach in " + MuUtils.ToSI(closestApproachTime - vesselState.time, 3) + "s");

            double closestApproachDistance = (part.vessel.orbit.getAbsolutePositionAtUT(closestApproachTime) - core.targetOrbit().getAbsolutePositionAtUT(closestApproachTime)).magnitude;
            GUILayout.Label("Approach distance: " + MuUtils.ToSI(closestApproachDistance, 3) + "m");

            GUILayout.Label("Relative inclination: " + part.vessel.orbit.relativeInclination(core.targetOrbit()).ToString("F2") + "°");

            GUILayout.Label("Time to AN: " + MuUtils.ToSI(part.vessel.orbit.GetTimeToRelAN(core.targetOrbit()), 3) + "s");
            GUILayout.Label("Time to DN: " + MuUtils.ToSI(part.vessel.orbit.GetTimeToRelDN(core.targetOrbit()), 3) + "s");



            //                burnForCollisionCourse = GUILayout.Toggle(burnForCollisionCourse, "Burn to collide");

            /*                if (!comeAlongside && GUILayout.Button("Come alongside"))
                            {
                                comeAlongside = true;
                                core.controlClaim(this);
                            }
                            else if (comeAlongside && GUILayout.Button("Stop coming alongside"))
                            {
                                comeAlongside = false;
                                FlightInputHandler.SetNeutralControls();
                                core.controlRelease(this);
                            }*/

            if (!autoAlign && GUILayout.Button("Align orbits"))
            {
                autoAlign = true;
                autoAlignBurnTriggered = false;
                core.controlClaim(this);
            }
            else if (autoAlign && GUILayout.Button("Stop aligning orbits"))
            {
                autoAlign = false;
                FlightInputHandler.SetNeutralControls();
                core.controlRelease(this);
            }

            if (statusString.Length > 0) GUILayout.Label(statusString);


            GUILayout.EndVertical();

            GUI.DragWindow();
        }




        /*        private void RenderRendezvousUI(GUIStyle sty, GUIStyle but)
                {
                    if (!CheckVessel())
                    {
                        _flyByWire = false;
                        Mode = UIMode.SELECTED;
                    }

                    Vessel selectedVessel = FlightGlobals.Vessels[_selectedVesselIndex] as Vessel;

                    if (GUILayout.Button(selectedVessel.vesselName, but, GUILayout.ExpandWidth(true)))
                    {
                        _flyByWire = false;
                        Mode = UIMode.SELECTED;
                    }
                    if (_targetDistance > 10000)
                    {
                        GUILayout.Box("Distance: " + (_targetDistance / 1000).ToString("F1") + "km", GUILayout.Width(300));
                    }
                    else
                    {
                        GUILayout.Box("Distance: " + _targetDistance.ToString("F1") + "m", GUILayout.Width(300));
                    }
                    GUILayout.Box("Rel Inc : " + _relativeInclination.ToString("F3"));
                    GUILayout.Box("Rel VelM: " + _relativeVelocity.magnitude.ToString("F2"));


                    // Take the relative velocity and project into ship local space.
                    _localRelativeVelocity = part.vessel.GetTransform().worldToLocalMatrix.MultiplyVector(_relativeVelocity);
                    _localRelativePosition = part.vessel.GetTransform().worldToLocalMatrix.MultiplyPoint(selectedVessel.transform.position);

                    if (automation == true)
                    {
                        if (GUILayout.Button(_killRelativeVelocity == false ? "Kill Rel Vel" : "FIRING", but, GUILayout.ExpandWidth(true)))
                            _killRelativeVelocity = !_killRelativeVelocity;

                        if (GUILayout.Button(_homeOnRelativePosition == false ? "Home on Y+ 5m" : "HOMING", but, GUILayout.ExpandWidth(true)))
                            _homeOnRelativePosition = !_homeOnRelativePosition;

                    }
                    GUILayout.Box("Rel Vel : " + _localRelativeVelocity.x.ToString("F2") + ", " + _localRelativeVelocity.y.ToString("F2") + ", " + _localRelativeVelocity.z.ToString("F2"));
                    if (_targetDistance > 10000)
                    {
                        GUILayout.Box("Rel Pos : " + (_localRelativePosition.x / 1000).ToString("F2") + "km, " + (_localRelativePosition.y / 1000).ToString("F2") + "km, " + (_localRelativePosition.z / 1000).ToString("F2") + "km");
                    }
                    else
                    {
                        GUILayout.Box("Rel Pos : " + _localRelativePosition.x.ToString("F2") + ", " + _localRelativePosition.y.ToString("F2") + ", " + _localRelativePosition.z.ToString("F2"));
                    }

                    if (_flyByWire == false)
                    {
                        GUILayout.BeginHorizontal();

                        if (GUILayout.Button(ControlModeCaptions[0], but, GUILayout.ExpandWidth(true)))
                        {
                            _flyByWire = true;
                            PointAt = Orient.RelativeVelocity;
                            _modeChanged = true;
                            _selectedFlyMode = 0;
                        }


                        if (GUILayout.Button(ControlModeCaptions[1], but, GUILayout.ExpandWidth(true)))
                        {
                            _flyByWire = true;
                            PointAt = Orient.RelativeVelocityAway;
                            _modeChanged = true;
                            _selectedFlyMode = 1;
                        }


                        if (GUILayout.Button(ControlModeCaptions[2], but, GUILayout.ExpandWidth(true)))
                        {
                            _flyByWire = true;
                            PointAt = Orient.Target;
                            _modeChanged = true;
                            _selectedFlyMode = 2;
                        }

                        GUILayout.EndHorizontal();

                        GUILayout.BeginHorizontal();

                        if (GUILayout.Button(ControlModeCaptions[3], but, GUILayout.ExpandWidth(true)))
                        {
                            _flyByWire = true;
                            PointAt = Orient.TargetAway;
                            _modeChanged = true;
                            _selectedFlyMode = 3;
                        }

                        if (GUILayout.Button(ControlModeCaptions[4], but, GUILayout.ExpandWidth(true)))
                        {
                            _flyByWire = true;
                            PointAt = Orient.MatchTarget;
                            _modeChanged = true;
                            _selectedFlyMode = 4;
                        }

                        if (GUILayout.Button(ControlModeCaptions[5], but, GUILayout.ExpandWidth(true)))
                        {
                            _flyByWire = true;
                            PointAt = Orient.MatchTargetAway;
                            _modeChanged = true;
                            _selectedFlyMode = 5;
                        }

                        GUILayout.EndHorizontal();
                    }

                    if (_flyByWire)
                    {
                        if (GUILayout.Button("Disable " + ControlModeCaptions[_selectedFlyMode], but, GUILayout.ExpandWidth(true)))
                        {
                            FlightInputHandler.SetNeutralControls();
                            _flyByWire = false;
                            _modeChanged = true;
                        }
                    }
                }*/

        #region Control Logic

        /// <summary>
        /// Checks  if there is a target selected, and that the target is within the same SOI as our vessel.
        /// </summary>
        /// <returns>
        /// Whether the above condition holds.
        /// </returns>
        private bool CheckTarget()
        {
            return (FlightGlobals.fetch.VesselTarget != null) && (core.targetOrbit().referenceBody == part.vessel.mainBody);
        }


        public override void drive(FlightCtrlState controls)
        {
            if (!CheckTarget()) return;

            //            if (comeAlongside) driveComeAlongside(controls);



            if (autoAlign) driveAutoAlign(controls);

            /*
            if (_autoPhaser)
            {
                switch (_autoPhaserState)
                {
                    case AutoPhaserState.Step1WaitForTargetApsis:
                        double timeLeft = CalculateTimeTillNextTargetApsis();

                        // Set the PointAt based on who is faster at that point in time.
                        _flyByWire = true;
                        if (part.vessel.orbit.getOrbitalSpeedAt(timeLeft) > selectedVessel.orbit.getOrbitalSpeedAt(timeLeft))
                            PointAt = Orient.Retrograde;
                        else
                            PointAt = Orient.Prograde;

                        // Advance if it's time.
                        if (timeLeft < 5.0)
                        {
                            _autoPhaserState = AutoPhaserState.Step2BurnToMatchNextApsis;
                            _autoPhaserVelocityGoal = selectedVessel.orbit.getOrbitalSpeedAt(CalculateTimeTillFurtherTargetApsis());
                            _autoPhaseBurnComplete = false;
                        }
                        break;

                    case AutoPhaserState.Step2BurnToMatchNextApsis:
                        double predictedVelocity = part.vessel.orbit.getOrbitalSpeedAt(CalculateTimeTillFurtherTargetApsis());
                        if (_headingError.magnitude < 5.0 && !_autoPhaseBurnComplete)
                        {
                            controls.mainThrottle = 1;
                        }
                        else
                        {
                            controls.mainThrottle = 0;
                        }

                        // Advance to next state if we hit our goal.
                        if (Math.Abs(predictedVelocity - _autoPhaserVelocityGoal) < 10)
                        {
                            _autoPhaseBurnComplete = true;
                            controls.mainThrottle = 0;

                        }

                        // Wait till we pass the apsis so we don't double advance.
                        if (_autoPhaseBurnComplete && CalculateTimeTillNextTargetApsis() > 10.0)
                            _autoPhaserState = AutoPhaserState.Step3WaitForTargetApsis;
                        break;

                    case AutoPhaserState.Step3WaitForTargetApsis:
                        timeLeft = CalculateTimeTillNextTargetApsis();

                        // Set the PointAt based on who is faster at that point in time.
                        _flyByWire = true;
                        PointAt = Orient.Prograde;

                        // Advance if it's time.
                        if (timeLeft < 5.0)
                        {
                            _autoPhaserState = AutoPhaserState.Step4BurnToRendezvous;
                        }

                        break;

                    case AutoPhaserState.Step4BurnToRendezvous:

                        // TODO: Make sure we are only considering the apsis that
                        // is spatially similar to ours, otherwise we get in sync
                        // orbitally but go into step 5 super far away.
                        double timeToRendezvous = 0.0, minDeltaT = 0.0;
                        CalculateNearestRendezvousInSeconds(out timeToRendezvous, out minDeltaT);

                        if (minDeltaT > 5)
                            controls.mainThrottle = 0.25f;
                        else
                        {
                            controls.mainThrottle = 0.0f;
                            _autoPhaserState = AutoPhaserState.Step5WaitForRendezvous;
                        }
                        break;

                    case AutoPhaserState.Step5WaitForRendezvous:
                        timeToRendezvous = 0.0;
                        minDeltaT = 0.0;
                        CalculateNearestRendezvousInSeconds(out timeToRendezvous, out minDeltaT);

                        if (timeToRendezvous < 2)
                            _autoPhaserState = AutoPhaserState.Step6BurnToMatchVelocity;

                        break;

                    case AutoPhaserState.Step6BurnToMatchVelocity:
                        if (_relativeVelocity.magnitude > 5)
                        {
                            _flyByWire = true;
                            PointAt = Orient.RelativeVelocityAway;

                            if (_headingError.magnitude < 5)
                            {
                                if (_relativeVelocity.magnitude > 15)
                                    controls.mainThrottle = 1.0f;
                                else
                                    controls.mainThrottle = 0.2f;
                            }

                        }
                        else
                        {
                            // All done!
                            controls.mainThrottle = 0.0f;
                            _autoPhaser = false;
                        }
                        break;

                }
            }

            if (_killRelativeVelocity)
            {
                controls.X = Mathf.Clamp(-_localRelativeVelocity.x * 8.0f, -1.0f, 1.0f);
                controls.Y = Mathf.Clamp(-_localRelativeVelocity.z * 8.0f, -1.0f, 1.0f);
                controls.Z = Mathf.Clamp(-_localRelativeVelocity.y * 8.0f, -1.0f, 1.0f);

                if (_localRelativeVelocity.magnitude < 0.1)
                    _killRelativeVelocity = false;
            }
            else if (_homeOnRelativePosition)
            {
                Vector3 targetGoalPos = new Vector3(0.0f, 2.0f, 0.0f);
                targetGoalPos = selectedVessel.transform.localToWorldMatrix.MultiplyPoint(targetGoalPos);
                targetGoalPos = part.vessel.GetTransform().worldToLocalMatrix.MultiplyPoint(targetGoalPos);

                Vector3 relPos = targetGoalPos;
                Vector4 goalVel = Vector3.zero;

                float velGoal = 0.1f;

                if (_targetDistance > 2.0f)
                    velGoal = 0.3f;
                else if (_targetDistance > 10.0f)
                    velGoal = 0.5f;
                else if (_targetDistance > 50.0f)
                    velGoal = 1.0f;
                else if (_targetDistance > 150.0f)
                    velGoal = 3.0f;

                if (Mathf.Abs(relPos.x) > 0.01f)
                    goalVel.x = -Mathf.Sign(relPos.x) * velGoal;

                if (Mathf.Abs(relPos.y) > 0.01f)
                    goalVel.y = -Mathf.Sign(relPos.y) * velGoal;

                if (Mathf.Abs(relPos.z) > 0.01f)
                    goalVel.z = -Mathf.Sign(relPos.z) * velGoal;

                controls.X = Mathf.Clamp((goalVel.x - _localRelativeVelocity.x) * 8.0f, -1, 1);
                controls.Y = Mathf.Clamp((goalVel.z - _localRelativeVelocity.z) * 8.0f, -1, 1);
                controls.Z = Mathf.Clamp((goalVel.y - _localRelativeVelocity.y) * 8.0f, -1, 1);
            }

            if (!_flyByWire)
                return;

            Quaternion tgt = Quaternion.LookRotation(_tgtFwd, _tgtUp);
            Quaternion delta =
                Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(part.vessel.GetTransform().rotation) * tgt);

            _headingError =
                new Vector3((delta.eulerAngles.x > 180) ? (delta.eulerAngles.x - 360.0F) : delta.eulerAngles.x,
                            (delta.eulerAngles.y > 180) ? (delta.eulerAngles.y - 360.0F) : delta.eulerAngles.y,
                            (delta.eulerAngles.z > 180) ? (delta.eulerAngles.z - 360.0F) : delta.eulerAngles.z) / 180.0F;
            _integral += _headingError * TimeWarp.fixedDeltaTime;
            _deriv = (_headingError - _prevErr) / TimeWarp.fixedDeltaTime;
            _act = Kp * _headingError + Ki * _integral + Kd * _deriv;
            _prevErr = _headingError;

            controls.pitch = Mathf.Clamp(controls.pitch + _act.x, -1.0F, 1.0F);
            controls.yaw = Mathf.Clamp(controls.yaw - _act.y, -1.0F, 1.0F);
            controls.roll = Mathf.Clamp(controls.roll + _act.z, -1.0F, 1.0F);
           */

        }

        /*        void driveComeAlongside(FlightCtrlState controls) {
            
                        if (core.targetOrbit() == null)
                        {
                            comeAlongside = false;
                            FlightInputHandler.SetNeutralControls();
                            core.controlRelease(this);
                            return;
                        }

                        Vector3d targetPosition = core.targetPosition();// (core.targetType == MechJebCore.TargetType.VESSEL ? (Vector3d)core.targetVessel.transform.position : core.targetBody.position);
                        Vector3d vectorToTarget = (targetPosition - vesselState.CoM).normalized;
                        Vector3d relativeVelocity = vesselState.velocityVesselOrbit - core.targetOrbit().GetVel();
                        Vector3d lateralVector = Vector3d.Exclude(vectorToTarget, relativeVelocity).normalized;
                        double lateralSpeed = Vector3d.Dot(relativeVelocity, lateralVector);
                        double closingSpeed = Vector3d.Dot(relativeVelocity, vectorToTarget);
                        double closingDistance = Vector3d.Dot(targetPosition - vesselState.CoM, relativeVelocity.normalized);
                        double lateralSpeedFraction = lateralSpeed / relativeVelocity.magnitude;

                        //print("lateralSpeedFraction = " + lateralSpeedFraction);

                        double maxClosingSpeed = Math.Sqrt(2 * Math.Max(closingDistance - 20, 0) * vesselState.maxThrustAccel);
                        double desiredClosingSpeed = 0.5 * maxClosingSpeed;

                        //print(String.Format("closingSpeed / desiredClosingSpeed = {0:F1} / {1:F1}", closingSpeed, desiredClosingSpeed)); 

                        double lateralWeight = lateralSpeed;
                        if (lateralSpeed < 1.0) lateralWeight *= lateralSpeed; //an attempt to suppress wiggles
                        double closingWeight = ARUtils.Clamp(1.1*closingSpeed - desiredClosingSpeed, 0, maxClosingSpeed);

                        //print(String.Format("lateralWeight / closingWeight = {0:F1} / {1:F1}", lateralWeight, closingWeight));

                        Vector3d desiredAttitude = (-lateralWeight * lateralVector - closingWeight * relativeVelocity.normalized).normalized;

                        core.attitudeTo(desiredAttitude, MechJebCore.AttitudeReference.INERTIAL, this);

                        if (Vector3d.Dot(relativeVelocity, vectorToTarget) < 0 || relativeVelocity.magnitude < 0.1)
                        {
                            comeAlongside = false;
                            FlightInputHandler.SetNeutralControls();
                            core.controlRelease(this);
                        }
                        else
                        {
                            if (closingDistance < 25)
                            {
                                //print("last few meters: closingDistance = " + closingDistance);
                                core.attitudeTo(Vector3d.back, MechJebCore.AttitudeReference.TARGET, this);
                                if (core.attitudeAngleFromTarget() < 5)
                                {
                                    controls.mainThrottle = Mathf.Clamp((float)(relativeVelocity.magnitude / vesselState.maxThrustAccel), 0.0F, 1.0F);
                                }
                            }
                            else if (core.attitudeAngleFromTarget() < 15 &&
                                (closingSpeed > desiredClosingSpeed || lateralSpeedFraction > 0.08))
                            {
                                float lateralThrottle = Mathf.Clamp((float)(20 * (lateralSpeedFraction - 0.08)), 0.0F, 1.0F);
                                float closingThrottle = Mathf.Clamp((float)(10 * (closingSpeed / desiredClosingSpeed - 1)), 0.0F, 1.0F);
                                controls.mainThrottle = Mathf.Max(lateralThrottle, closingThrottle);
                            }
                            else
                            {
                                controls.mainThrottle = 0.0F;
                            }
                        }
                }*/


        void driveAutoAlign(FlightCtrlState controls)
        {
            if (!autoAlignBurnTriggered)
            {
                // Is it time to burn? Find soonest node.
                double timeToBurnAN = part.vessel.orbit.GetTimeToRelAN(core.targetOrbit());
                double timeToBurnDN = part.vessel.orbit.GetTimeToRelDN(core.targetOrbit());

                bool ascendingSoonest = timeToBurnAN < timeToBurnDN;
                double timeToBurnNode = ascendingSoonest ? timeToBurnAN : timeToBurnDN;

                autoAlignBurnDirection = ascendingSoonest ? Vector3.right : Vector3.left;

                double burnDV = part.vessel.orbit.relativeInclination(core.targetOrbit()) * Math.PI / 180 * vesselState.speedOrbital;
                double burnTime = burnDV / vesselState.maxThrustAccel;
                double leadTime = Math.Max(burnTime / 2, 1.0); //min lead time of 1 second so we don't miss the burn if the burn time is short

                statusString = "Align Orbits: Burning in " + (int)(timeToBurnNode - leadTime) + " s";

                if (timeToBurnNode > leadTime + 30)
                {
                    core.warpTo(this, timeToBurnNode - burnTime / 2 - 30, warpLookaheadTimes);
                }
                else if (timeToBurnNode < leadTime)
                {
                    autoAlignBurnTriggered = true;
                    autoAlignDidBurn = false;
                }
            }

            core.attitudeTo(autoAlignBurnDirection, MechJebCore.AttitudeReference.ORBIT, this);

            if (autoAlignBurnTriggered)
            {
                statusString = "Align Orbits: Burning to match planes";
                if (core.attitudeAngleFromTarget() < 5.0)
                {
                    double maxDegreesPerSecond = (180 / Math.PI) * vesselState.maxThrustAccel / vesselState.speedOrbital;
                    if (Math.Abs(part.vessel.orbit.relativeInclination(core.targetOrbit())) > maxDegreesPerSecond / 2)
                    {
                        controls.mainThrottle = 1.0f;
                    }
                    else
                    {
                        controls.mainThrottle = (float)Math.Max(part.vessel.orbit.relativeInclination(core.targetOrbit()) / maxDegreesPerSecond, 0.05F);
                    }
                    autoAlignDidBurn = true;
                }
                else
                {
                    controls.mainThrottle = 0.0f;
                }

                //stop burn if it's no longer pushing the orbit normals closer:
                Vector3d torqueNeeded = ARUtils.swapYZ(part.vessel.orbit.GetOrbitNormal()).normalized - ARUtils.swapYZ(core.targetOrbit().GetOrbitNormal()).normalized;
                Vector3d torqueDir = Vector3d.Cross(vesselState.CoM - part.vessel.mainBody.transform.position, vesselState.forward).normalized;
                double torqueDotProduct = Vector3d.Dot(torqueDir, torqueNeeded);

                if (autoAlignDidBurn && (part.vessel.orbit.relativeInclination(core.targetOrbit()) < 0.005 || torqueDotProduct < 0))
                {
                    statusString = "Align Orbits: Finished";
                    autoAlignBurnTriggered = false;
                    autoAlign = false;
                    FlightInputHandler.SetNeutralControls();
                    controls.mainThrottle = 0;
                    core.controlRelease(this);
                }
            }
        }


        #endregion

        #region KSP Interface

        /*
        private void PerformSyncPartLogic()
        {
            // What anomaly are we trying to rendezvous at?
            switch (SyncMode)
            {
                case SynchronizationType.ShipApoapsis:
                    _rendezvousAnomaly = 180;
                    break;
                case SynchronizationType.ShipPeriapsis:
                    _rendezvousAnomaly = 0;
                    break;
                case SynchronizationType.TargetApoapsis:
                    _rendezvousAnomaly = FlightGlobals.Vessels[_selectedVesselIndex].orbit.TranslateAnomaly(part.vessel.orbit, 180);
                    break;
                case SynchronizationType.TargetPeriapsis:
                    _rendezvousAnomaly = FlightGlobals.Vessels[_selectedVesselIndex].orbit.TranslateAnomaly(part.vessel.orbit, 0);
                    break;
            }

            // Only recalculate if enough time has elapsed.
            if (_rendezvousRecalculationTimer < .1)
                return;

            // Find the time away from the anomaly we'll be at rendezvous.
            for (int i = 0; i < 4; i++)
            {
                _shipTimeToRendezvous[i] = (float)part.vessel.orbit.GetTimeToTrue(_rendezvousAnomaly) + (float)part.vessel.orbit.period * i;
                _targetTimeToRendezvous[i] = (float)part.vessel.orbit.Syncorbits(FlightGlobals.Vessels[_selectedVesselIndex].orbit, _rendezvousAnomaly, i);

                if (i == 0)
                    _minimumPredictedTimeFromTarget = Math.Abs(_shipTimeToRendezvous[i] - _targetTimeToRendezvous[i]);

                if (_minimumPredictedTimeFromTarget > Math.Abs(_shipTimeToRendezvous[i] - _targetTimeToRendezvous[i]))
                    _closestApproachOrbit = i;
            }

            double junk;
            CalculateNearestRendezvousInSeconds(out junk, out _minimumPredictedTimeFromTarget);

            // Update the display.
            for (int i = 0; i < 4; i++)
            {
                _syncString[i] = i.ToString() + "			" + _shipTimeToRendezvous[i].ToString("f0") + "			" + _targetTimeToRendezvous[i].ToString("f0");
            }

            // Reset the timer.
            _rendezvousRecalculationTimer = 0;
        }*/


        #endregion
    }
}

