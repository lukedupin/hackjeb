﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Linq;
using UnityEngine;
using MuMech;

namespace MuMech
{
    public class VesselState
    {
        public double time;            //planetarium time
        public double deltaT;          //TimeWarp.fixedDeltaTime

        public Vector3d CoM;
        public Vector3d MoI;
        public Vector3d up;
        public Vector3d north;
        public Vector3d east;
        public Vector3d forward;      //the direction the vessel is pointing

        public Quaternion rotationSurface;
        public Quaternion rotationVesselSurface;

        public Vector3d velocityMainBodySurface;
        public Vector3d velocityVesselSurface;
        public Vector3d velocityVesselSurfaceUnit;
        public Vector3d velocityVesselOrbit;
        public Vector3d velocityVesselOrbitUnit;

        public Vector3d angularVelocity;
        public Vector3d angularMomentum;

        public Vector3d upNormalToVelSurface; //unit vector in the plane of up and velocityVesselSurface and perpendicular to velocityVesselSurface
        public Vector3d upNormalToVelOrbit;   //unit vector in the plane of up and velocityVesselOrbit and perpendicular to velocityVesselOrbit 
        public Vector3d leftSurface;  //unit vector perpendicular to up and velocityVesselSurface
        public Vector3d leftOrbit;    //unit vector perpendicular to up and velocityVesselOrbit

        public Vector3d gravityForce;
        public double localg;             //magnitude of gravityForce

        public MovingAverage speedOrbital = new MovingAverage();
        public MovingAverage speedSurface = new MovingAverage();
        public MovingAverage speedVertical = new MovingAverage();
        public MovingAverage speedHorizontal = new MovingAverage();
        public MovingAverage vesselHeading = new MovingAverage();
        public MovingAverage vesselPitch = new MovingAverage();
        public MovingAverage vesselRoll = new MovingAverage();
        public MovingAverage altitudeASL = new MovingAverage();
        public MovingAverage altitudeTrue = new MovingAverage();
        public double altitudeBottom = 0;
        public MovingAverage orbitApA = new MovingAverage();
        public MovingAverage orbitPeA = new MovingAverage();
        public MovingAverage orbitPeriod = new MovingAverage();
        public MovingAverage orbitTimeToAp = new MovingAverage();
        public MovingAverage orbitTimeToPe = new MovingAverage();
        public MovingAverage orbitLAN = new MovingAverage();
        public MovingAverage orbitArgumentOfPeriapsis = new MovingAverage();
        public MovingAverage orbitInclination = new MovingAverage();
        public MovingAverage orbitEccentricity = new MovingAverage();
        public MovingAverage orbitSemiMajorAxis = new MovingAverage();
        public MovingAverage latitude = new MovingAverage();
        public MovingAverage longitude = new MovingAverage();

        public double radius;  //distance from planet center

        private double _lastVel;
        private double _lastUpdate = -1;
        private double _lastAltitude;
        private double _updateTime = -1;
        public double surface_accel = 0;
        public double surface_accel_delta = 0;
        public double stop_distance;
        public double stop_time;
        public double fuelConsumption;
        public double fuelTotalAmount;
        public double fuelAmount;
        public double thrust;
        public double cost;
        public double mass;
        public double thrustAvailable;
        public double thrustMinimum;
        public double maxThrustAccel;      //thrustAvailable / mass
        public double minThrustAccel;      //some engines (particularly SRBs) have a minimum thrust so this may be nonzero
        public double torqueRAvailable;
        public double torquePYAvailable;
        public double torqueThrustPYAvailable;
        public double massDrag;
        public double atmosphericDensity;
        public double angleToPrograde;

        public void Update(Vessel vessel)
        {
            time = Planetarium.GetUniversalTime();
            deltaT = TimeWarp.fixedDeltaTime;

              //Only update 
            if (time - _updateTime < 0.25)
              return;
            _updateTime = time;

            CoM = vessel.findWorldCenterOfMass();
            up = (CoM - vessel.mainBody.position).normalized;

            north = Vector3d.Exclude(up, (vessel.mainBody.position + vessel.mainBody.transform.up * (float)vessel.mainBody.Radius) - CoM).normalized;
            east = vessel.mainBody.getRFrmVel(CoM).normalized;
            forward = vessel.GetTransform().up;
            rotationSurface = Quaternion.LookRotation(north, up);
            rotationVesselSurface = Quaternion.Inverse(Quaternion.Euler(90, 0, 0) * Quaternion.Inverse(vessel.GetTransform().rotation) * rotationSurface);

            velocityVesselOrbit = vessel.orbit.GetVel();
            velocityVesselOrbitUnit = velocityVesselOrbit.normalized;
            velocityVesselSurface = velocityVesselOrbit - vessel.mainBody.getRFrmVel(CoM);
            velocityVesselSurfaceUnit = velocityVesselSurface.normalized;
            velocityMainBodySurface = rotationSurface * velocityVesselSurface;

            angularVelocity = Quaternion.Inverse(vessel.GetTransform().rotation) * vessel.rigidbody.angularVelocity;

            upNormalToVelSurface = Vector3d.Exclude(velocityVesselSurfaceUnit, up).normalized;
            upNormalToVelOrbit = Vector3d.Exclude(velocityVesselOrbit, up).normalized;
            leftSurface = -Vector3d.Cross(upNormalToVelSurface, velocityVesselSurfaceUnit);
            leftOrbit = -Vector3d.Cross(upNormalToVelOrbit, velocityVesselOrbitUnit); ;

            gravityForce = FlightGlobals.getGeeForceAtPosition(CoM);
            localg = gravityForce.magnitude;

            speedOrbital.value = velocityVesselOrbit.magnitude;
            speedSurface.value = velocityVesselSurface.magnitude;
            speedVertical.value = Vector3d.Dot(velocityVesselSurface, up);
            speedHorizontal.value = (velocityVesselSurface - (speedVertical * up)).magnitude;

            vesselHeading.value = rotationVesselSurface.eulerAngles.y;
            vesselPitch.value = (rotationVesselSurface.eulerAngles.x > 180) ? (360.0 - rotationVesselSurface.eulerAngles.x) : -rotationVesselSurface.eulerAngles.x;
            vesselRoll.value = (rotationVesselSurface.eulerAngles.z > 180) ? (rotationVesselSurface.eulerAngles.z - 360.0) : rotationVesselSurface.eulerAngles.z;

            altitudeASL.value = vessel.mainBody.GetAltitude(CoM);
            RaycastHit sfc;
            if (Physics.Raycast(CoM, -up, out sfc, (float)altitudeASL + 10000.0F, 1 << 15))
            {
                altitudeTrue.value = sfc.distance;
            }
            else if (vessel.mainBody.pqsController != null)
            {
                // from here: http://kerbalspaceprogram.com/forum/index.php?topic=10324.msg161923#msg161923
                altitudeTrue.value = vessel.mainBody.GetAltitude(CoM) - (vessel.mainBody.pqsController.GetSurfaceHeight(QuaternionD.AngleAxis(vessel.mainBody.GetLongitude(CoM), Vector3d.down) * QuaternionD.AngleAxis(vessel.mainBody.GetLatitude(CoM), Vector3d.forward) * Vector3d.right) - vessel.mainBody.pqsController.radius);
            }
            else
            {
                altitudeTrue.value = vessel.mainBody.GetAltitude(CoM);
            }

            double surfaceAltitudeASL = altitudeASL - altitudeTrue;
            altitudeBottom = altitudeTrue;
            foreach (Part p in vessel.parts)
            {
                if (p.collider != null)
                {
                    Vector3d bottomPoint = p.collider.ClosestPointOnBounds(vessel.mainBody.position);
                    double partBottomAlt = vessel.mainBody.GetAltitude(bottomPoint) - surfaceAltitudeASL;
                    altitudeBottom = Math.Max(0, Math.Min(altitudeBottom, partBottomAlt));
                }
            }

            atmosphericDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(altitudeASL, vessel.mainBody));

            orbitApA.value = vessel.orbit.ApA;
            orbitPeA.value = vessel.orbit.PeA;
            orbitPeriod.value = vessel.orbit.period;
            orbitTimeToAp.value = vessel.orbit.timeToAp;
            if (vessel.orbit.eccentricity < 1) orbitTimeToPe.value = vessel.orbit.timeToPe;
            else orbitTimeToPe.value = -vessel.orbit.meanAnomaly / (2 * Math.PI / vessel.orbit.period); //orbit.timeToPe is bugged for ecc > 1 and timewarp > 2x
            orbitLAN.value = vessel.orbit.LAN;
            orbitArgumentOfPeriapsis.value = vessel.orbit.argumentOfPeriapsis;
            orbitInclination.value = vessel.orbit.inclination;
            orbitEccentricity.value = vessel.orbit.eccentricity;
            orbitSemiMajorAxis.value = vessel.orbit.semiMajorAxis;
            latitude.value = vessel.mainBody.GetLatitude(CoM);
            longitude.value = ARUtils.clampDegrees(vessel.mainBody.GetLongitude(CoM));

            if (vessel.mainBody != Planetarium.fetch.Sun)
            {
                Vector3d delta = vessel.mainBody.getPositionAtUT(Planetarium.GetUniversalTime() + 1) - vessel.mainBody.getPositionAtUT(Planetarium.GetUniversalTime() - 1);
                Vector3d plUp = Vector3d.Cross(vessel.mainBody.getPositionAtUT(Planetarium.GetUniversalTime()) - vessel.mainBody.referenceBody.getPositionAtUT(Planetarium.GetUniversalTime()), vessel.mainBody.getPositionAtUT(Planetarium.GetUniversalTime() + vessel.mainBody.orbit.period / 4) - vessel.mainBody.referenceBody.getPositionAtUT(Planetarium.GetUniversalTime() + vessel.mainBody.orbit.period / 4)).normalized;
                angleToPrograde = ARUtils.clampDegrees360((((vessel.orbit.inclination > 90) || (vessel.orbit.inclination < -90)) ? 1 : -1) * ((Vector3)up).AngleInPlane(plUp, delta));
            }
            else
            {
                angleToPrograde = 0;
            }

            radius = (CoM - vessel.mainBody.position).magnitude;

            cost = mass = thrustAvailable = thrustMinimum = massDrag = torqueRAvailable = torquePYAvailable = torqueThrustPYAvailable = 0;
            MoI = vessel.findLocalMOI(CoM);
            foreach (Part p in vessel.parts)
            {
              cost += p.partInfo.cost;

                if (p.physicalSignificance != Part.PhysicalSignificance.NONE)
                {
                    double partMass = p.totalMass();
                    mass += partMass;
                    massDrag += partMass * p.maximum_drag;
                }

                MoI += p.Rigidbody.inertiaTensor;
                if ((p.State == PartStates.ACTIVE) || ((Staging.CurrentStage > Staging.lastStage) && (p.inverseStage == Staging.lastStage)))
                {
                    if (p is LiquidEngine && ARUtils.engineHasFuel(p))
                    {
                        double usableFraction = Vector3d.Dot((p.transform.rotation * ((LiquidEngine)p).thrustVector).normalized, forward);
                        thrustAvailable += ((LiquidEngine)p).maxThrust * usableFraction;
                        thrustMinimum += ((LiquidEngine)p).minThrust * usableFraction;
                        if (((LiquidEngine)p).thrustVectoringCapable)
                        {
                            torqueThrustPYAvailable += Math.Sin(Math.Abs(((LiquidEngine)p).gimbalRange) * Math.PI / 180) * ((LiquidEngine)p).maxThrust * (p.Rigidbody.worldCenterOfMass - CoM).magnitude;
                        }
                    }
                    else if (p is LiquidFuelEngine && ARUtils.engineHasFuel(p))
                    {
                        double usableFraction = Vector3d.Dot((p.transform.rotation * ((LiquidFuelEngine)p).thrustVector).normalized, forward);
                        thrustAvailable += ((LiquidFuelEngine)p).maxThrust * usableFraction;
                        thrustMinimum += ((LiquidFuelEngine)p).minThrust * usableFraction;
                        if (((LiquidFuelEngine)p).thrustVectoringCapable)
                        {
                            torqueThrustPYAvailable += Math.Sin(Math.Abs(((LiquidFuelEngine)p).gimbalRange) * Math.PI / 180) * ((LiquidFuelEngine)p).maxThrust * (p.Rigidbody.worldCenterOfMass - CoM).magnitude;
                        }
                    }
                    else if (p is SolidRocket && !p.ActivatesEvenIfDisconnected)
                    {
                        double usableFraction = Vector3d.Dot((p.transform.rotation * ((SolidRocket)p).thrustVector).normalized, forward);
                        thrustAvailable += ((SolidRocket)p).thrust * usableFraction;
                        thrustMinimum += ((SolidRocket)p).thrust * usableFraction;
                    }
                    else if (p is AtmosphericEngine && ARUtils.engineHasFuel(p))
                    {
                        double usableFraction = Vector3d.Dot((p.transform.rotation * ((AtmosphericEngine)p).thrustVector).normalized, forward);
                        thrustAvailable += ((AtmosphericEngine)p).maximumEnginePower * ((AtmosphericEngine)p).totalEfficiency * usableFraction;
                        if (((AtmosphericEngine)p).thrustVectoringCapable)
                        {
                            torqueThrustPYAvailable += Math.Sin(Math.Abs(((AtmosphericEngine)p).gimbalRange) * Math.PI / 180) * ((AtmosphericEngine)p).maximumEnginePower * ((AtmosphericEngine)p).totalEfficiency * (p.Rigidbody.worldCenterOfMass - CoM).magnitude;
                        }
                    }
                    else if (p.Modules.Contains("ModuleEngines"))
                    {
                        foreach (PartModule pm in p.Modules)
                        {
                            if ((pm is ModuleEngines) && (pm.isEnabled) && ARUtils.engineHasFuel(p) && ((ModuleEngines)p.Modules["ModuleEngines"]).getIgnitionState)
                            {
                                ModuleEngines e = (ModuleEngines)pm;
                                double usableFraction = 1; // Vector3d.Dot((p.transform.rotation * e.thrustTransform.forward).normalized, forward); // TODO: Fix usableFraction
                                thrustAvailable += e.maxThrust * usableFraction;

                                if (e.throttleLocked) thrustMinimum += e.maxThrust * usableFraction;
                                else thrustMinimum += e.minThrust * usableFraction;

                                if (p.Modules.OfType<ModuleGimbal>().Count() > 0)
                                {
                                    torqueThrustPYAvailable += Math.Sin(Math.Abs(p.Modules.OfType<ModuleGimbal>().First().gimbalRange) * Math.PI / 180) * e.maxThrust * (p.Rigidbody.worldCenterOfMass - CoM).magnitude; // TODO: close enough?
                                }
                            }
                        }
                    }
                    else
                    {
                        foreach (PartModule pm in p.Modules)
                        {
                            CenterOfThrustQuery ctq = new CenterOfThrustQuery();
                            pm.BroadcastMessage("OnCenterOfThrustQuery", ctq, SendMessageOptions.DontRequireReceiver);
                            if (ctq.thrust > 0)
                            {
                                double usableFraction = 1; // Vector3d.Dot((p.transform.rotation * e.thrustTransform.forward).normalized, forward); // TODO: Fix usableFraction
                                thrustAvailable += ctq.thrust * usableFraction;
                            }
                        }
                    }
                }

                if (vessel.ActionGroups[KSPActionGroup.RCS])
                {
                    if (p is RCSModule)
                    {
                        double maxT = 0;
                        for (int i = 0; i < 6; i++)
                        {
                            if (((RCSModule)p).thrustVectors[i] != Vector3.zero)
                            {
                                maxT = Math.Max(maxT, ((RCSModule)p).thrusterPowers[i]);
                            }
                        }
                        torqueRAvailable += maxT;
                        torquePYAvailable += maxT * (p.Rigidbody.worldCenterOfMass - CoM).magnitude;
                    }

                    if (p.Modules.Contains("ModuleRCS"))
                    {
                        foreach (ModuleRCS pm in p.Modules.OfType<ModuleRCS>())
                        {
                            double maxT = pm.thrustForces.Max();

                            if ((pm.isEnabled) && (!pm.isJustForShow))
                            {
                                torqueRAvailable += maxT;
                                torquePYAvailable += maxT * (p.Rigidbody.worldCenterOfMass - CoM).magnitude;
                            }
                        }
                    }
                }
                if (p is CommandPod)
                {
                    torqueRAvailable += Math.Abs(((CommandPod)p).rotPower);
                    torquePYAvailable += Math.Abs(((CommandPod)p).rotPower);
                }
            }

            angularMomentum = new Vector3d(angularVelocity.x * MoI.x, angularVelocity.y * MoI.y, angularVelocity.z * MoI.z);

            maxThrustAccel = thrustAvailable / mass;
            minThrustAccel = thrustMinimum / mass;

            double throttle = vessel.ctrlState.mainThrottle;
            thrust = ((1.0 - throttle) * thrustMinimum + throttle * thrustAvailable);

            //Update the acceleration (1 is one 1Hz update, 0.1 is 10Hz)
            if (time - _lastUpdate > 0.5)
            {
              double mag = vessel.srf_velocity.magnitude;

                //Don't update on first pass
              if (_lastUpdate > 0)
              {
                double new_accel = (mag - _lastVel) / (time - _lastUpdate);
                surface_accel_delta = new_accel - surface_accel;
                surface_accel = new_accel;
                if (surface_accel < 0 && _lastAltitude > altitudeTrue.value )
                  stop_distance = -(mag * mag) / (2.0 * surface_accel);
                else
                  stop_distance = 0;

                  //How long will we burn to stop
                stop_time = 2.0 * stop_distance / mag;
              }

                //Calc how much fuel we are using
              fuelAmount = 0;
              fuelConsumption = 0;
              fuelTotalAmount = 0;
              foreach (Part p in vessel.GetActiveParts() )
              {
                foreach (ModuleEngines engine in p.Modules.OfType<ModuleEngines>())
                {
                  if (!engine.isEnabled || !engine.EngineIgnited || engine.engineShutdown )
                    continue;

                  double thr = engine.finalThrust;
                  double Isp = engine.realIsp;
                  double massFlowRate = thr / (Isp * 9.81);
                  double sumRatioTimesDensity = 0;
                  foreach (ModuleEngines.Propellant propellant in engine.propellants) 
                    sumRatioTimesDensity += propellant.ratio * PartResourceLibrary.Instance.GetDefinition(propellant.id).density;
                  foreach (ModuleEngines.Propellant propellant in engine.propellants)
                    if (propellant.name != "ElectricCharge")
                    {
                      fuelConsumption += propellant.ratio * massFlowRate / sumRatioTimesDensity;
                      fuelAmount += propellant.currentAmount;                      
                    }
                }

                  //Total amount
                fuelTotalAmount = 0;
                foreach (PartResource r in p.Resources)
                  if ( r.resourceName != "ElectricCharge")
                    fuelTotalAmount += r.maxAmount;
              }

                //Update my info
              _lastAltitude = altitudeTrue.value;
              _lastUpdate = time;
              _lastVel = mag;
            }
        }

        public double TerminalVelocity()
        {
            return Math.Sqrt((2 * gravityForce.magnitude * mass) / (0.009785 * Math.Exp(-altitudeASL / 5000.0) * massDrag));
        }

        public double thrustAccel(double throttle)
        {
            return (1.0 - throttle) * minThrustAccel + throttle * maxThrustAccel;
        }


    }
}
