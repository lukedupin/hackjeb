using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using OrbitExtensions;

namespace MuMech
{

    public static class ARUtils
    {
        public const double G = 6.674E-11; //this seems to be the value the game uses

        ///////////////////////////////////////////////////
        //ROCKET CHARACTERISTICS///////////////////////////
        ///////////////////////////////////////////////////

        //sums the thrust of ACTIVE engines under root, given hypothetical throttle setting
        public static double totalThrustOfActiveEngines(Vessel v, double throttle)
        {
            double thrust = 0;

            foreach (Part p in v.parts)
            {
                if (p.State == PartStates.ACTIVE)
                {
                    if (p is LiquidEngine)
                    {
                        thrust += (1.0 - throttle) * ((LiquidEngine)p).minThrust + throttle * ((LiquidEngine)p).maxThrust;
                    }
                    if (p is LiquidFuelEngine)
                    {
                        thrust += (1.0 - throttle) * ((LiquidFuelEngine)p).minThrust + throttle * ((LiquidFuelEngine)p).maxThrust;
                    }
                    if (p is SolidRocket)
                    {
                        thrust += ((SolidRocket)p).thrust;
                    }
                    if (p is AtmosphericEngine)
                    {
                        thrust += throttle * ((AtmosphericEngine)p).maximumEnginePower * ((AtmosphericEngine)p).totalEfficiency;
                    }
                    foreach (PartModule pm in p.Modules)
                    {
                        if ((pm is ModuleEngines) && (pm.isEnabled)) // TODO: needs to account for atmospheric effects?
                        {
                            thrust += (1.0 - throttle) * ((ModuleEngines)pm).minThrust + throttle * ((ModuleEngines)pm).maxThrust;
                        }
                    }
                }
            }

            return thrust;
        }

        public static float totalMass(this Part p)
        {
            return p.mass + p.GetResourceMass();
        }

        //sum of mass of parts under root
        public static double totalMass(Vessel v)
        {
            double ret = 0;
            foreach (Part p in v.parts)
            {
                ret += p.totalMass();
            }
            return ret;
        }

        //acceleration due to thrust of the rocket given a hypothetical throttle setting,
        //not accounting for external forces like drag and gravity
        public static double thrustAcceleration(Vessel v, double throttle)
        {
            return totalThrustOfActiveEngines(v, throttle) / totalMass(v);
        }

        public static bool engineHasFuel(Part p)
        {
            if (p is LiquidEngine || p is LiquidFuelEngine || p is AtmosphericEngine)
            {
                //I don't really know the details of how you're supposed to use RequestFuel, but this seems to work to
                //test whether something can get fuel.
                return p.RequestFuel(p, 0, Part.getFuelReqId());
            }
            else
            {
                return !p.Modules.OfType<ModuleEngines>().First().getFlameoutState;
            }
        }

        public static bool hasIdleEngineDescendant(Part p)
        {
            if ((p.State == PartStates.IDLE) && (p is SolidRocket || p is LiquidEngine || p is LiquidFuelEngine || p is AtmosphericEngine || p.Modules.Contains("ModuleEngines")) && !isSepratron(p)) return true;
            foreach (Part child in p.children)
            {
                if (hasIdleEngineDescendant(child)) return true;
            }
            return false;
        }

        //detect if a part is above an active or idle engine in the part tree
        public static bool hasActiveOrIdleEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.ACTIVE || p.State == PartStates.IDLE)
                && (p is SolidRocket || p is LiquidEngine || p is LiquidFuelEngine || p is AtmosphericEngine || p.Modules.Contains("ModuleEngines"))
                && !isSepratron(p) && engineHasFuel(p))
            {
                return true; // TODO: properly check if ModuleEngines is active
            }
            if (((p is FuelTank) && (((FuelTank)p).fuel > 0))) return true;
            if (!isSepratron(p))
            {
                foreach (PartResource r in p.Resources)
                {
                    if (r.amount > 0 && r.info.name != "ElectricCharge")
                    {
                        return true;
                    }
                }
            }
            foreach (Part child in p.children)
            {
                if (hasActiveOrIdleEngineOrTankDescendant(child)) return true;
            }
            return false;
        }

        //detect if a part is above a deactivated engine or fuel tank
        public static bool hasDeactivatedEngineOrTankDescendant(Part p)
        {
            if ((p.State == PartStates.DEACTIVATED) && (p is SolidRocket || p is LiquidEngine || p is LiquidFuelEngine || p is AtmosphericEngine || p is FuelTank || p.Modules.Contains("ModuleEngines") && !isSepratron(p)))
            {
                return true; // TODO: yet more ModuleEngine lazy checks
            }

            //check if this is a new-style fuel tank that's run out of resources:
            bool hadResources = false;
            bool hasResources = false;
            foreach (PartResource r in p.Resources)
            {
                if (r.name == "ElectricCharge") continue;
                if (r.maxAmount > 0) hadResources = true;
                if (r.amount > 0) hasResources = true;
            }
            if (hadResources && !hasResources) return true;
            
            if (((p is LiquidEngine) || (p is LiquidFuelEngine) || (p is AtmosphericEngine) || p.Modules.Contains("ModuleEngines")) && !engineHasFuel(p)) return true;

            foreach (Part child in p.children)
            {
                if (hasDeactivatedEngineOrTankDescendant(child)) return true;
            }
            return false;
        }

        //determine whether it's safe to activate inverseStage
        public static bool inverseStageDecouplesActiveOrIdleEngineOrTank(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && isDecoupler(p) && hasActiveOrIdleEngineOrTankDescendant(p))
                {
                    return true;
                }
            }
            return false;
        }

        //determine whether inverseStage sheds a dead engine
        public static bool inverseStageDecouplesDeactivatedEngineOrTank(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && isDecoupler(p) && hasDeactivatedEngineOrTankDescendant(p)) return true;
            }
            return false;
        }

        //determine whether activating inverseStage will fire any sort of decoupler. This
        //is used to tell whether we should delay activating the next stage after activating inverseStage
        public static bool inverseStageFiresDecoupler(int inverseStage, Vessel v)
        {
            foreach (Part p in v.parts)
            {
                if (p.inverseStage == inverseStage && isDecoupler(p)) return true;
            }
            return false;
        }

        //determine the maximum value of temperature/maxTemperature for any part in the ship
        //as a way of determining how close we are to blowing something up from overheating
        public static double maxTemperatureRatio(Vessel v)
        {
            double maxTempRatio = 0; //rootPart.temperature / rootPart.maxTemp;
            foreach (Part p in v.parts)
            {
                double tempRatio = p.temperature / p.maxTemp;
                if (tempRatio > maxTempRatio)
                {
                    maxTempRatio = tempRatio;
                }
            }
            return maxTempRatio;
        }

        //used in calculating the drag force on the ship:
        public static double totalDragCoefficient(Vessel v)
        {
            double ret = 0;
            foreach (Part p in v.parts)
            {
                ret += p.maximum_drag * p.totalMass();
            }
            return ret;
        }



        //formula for drag seems to be drag force = (1/2) * DragMultiplier * (air density) * (mass * max_drag) * (airspeed)^2
        //so drag acceleration is (1/2) * DragMultiplier * (air density) * (average max_drag) * (airspeed)^2
        //where the max_drag average over parts is weighted by the part mass
        public static Vector3d computeDragAccel(Vector3d pos, Vector3d orbitVel, double dragCoeffOverMass, CelestialBody body)
        {
            double airDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(pos, body));
            Vector3d airVel = orbitVel - body.getRFrmVel(pos);
            return -0.5 * FlightGlobals.DragMultiplier * airDensity * dragCoeffOverMass * airVel.magnitude * airVel;
        }

        //The KSP drag law is dv/dt = -b * v^2 where b is proportional to the air density and
        //the ship's drag coefficient. In this equation b has units of inverse length. So 1/b
        //is a characteristic length: a ship that travels this distance through air will lose a significant
        //fraction of its initial velocity
        public static double characteristicDragLength(Vector3d pos, double dragCoeffOverMass, CelestialBody body) {
            double airDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(pos, body));
            if (airDensity <= 0) return Double.MaxValue;
            return 1.0 / (0.5 * FlightGlobals.DragMultiplier * airDensity * dragCoeffOverMass);
        }

        public static double terminalVelocity(Vessel v, Vector3d position)
        {
            double alt = FlightGlobals.getAltitudeAtPos(position);
            double localg = FlightGlobals.getGeeForceAtPosition(position).magnitude;

            if (alt > v.mainBody.maxAtmosphereAltitude) return Double.MaxValue;

            double airDensity = FlightGlobals.getAtmDensity(FlightGlobals.getStaticPressure(position, v.mainBody));
            return Math.Sqrt(2 * localg * totalMass(v) / (totalDragCoefficient(v) * FlightGlobals.DragMultiplier * airDensity));
        }


        public static Vector3d computeTotalAccel(Vector3d pos, Vector3d orbitVel, double dragCoeffOverMass, CelestialBody body)
        {
            return FlightGlobals.getGeeForceAtPosition(pos) + computeDragAccel(pos, orbitVel, dragCoeffOverMass, body);
        }



        public static double computeApoapsis(Vector3d pos, Vector3d vel, CelestialBody body)
        {
            double r = (pos - body.position).magnitude;
            double v = vel.magnitude;
            double GM = FlightGlobals.getGeeForceAtPosition(pos).magnitude * r * r;
            double E = -GM / r + 0.5 * v * v;
            double L = Vector3d.Cross(pos - body.position, vel).magnitude;
            double apoapsis = (-GM - Math.Sqrt(GM * GM + 2 * E * L * L)) / (2 * E);
            return apoapsis - body.Radius;
        }

        public static double computePeriapsis(Vector3d pos, Vector3d vel, CelestialBody body)
        {
            double r = (pos - body.position).magnitude;
            double v = vel.magnitude;
            double GM = FlightGlobals.getGeeForceAtPosition(pos).magnitude * r * r;
            double E = -GM / r + 0.5 * v * v;
            double L = Vector3d.Cross(pos - body.position, vel).magnitude;
            double periapsis = (-GM + Math.Sqrt(GM * GM + 2 * E * L * L)) / (2 * E);
            return periapsis - body.Radius;
        }


        //utility function that displays a horizontal row of label-textbox-label and parses the number in the textbox
        public static double doGUITextInput(String leftText, float leftWidth, String currentText, float textWidth,
                                            String rightText, float rightWidth, out String newText, double defaultValue, double multiplier = 1.0)
        {
            GUIStyle leftLabelStyle = new GUIStyle(GUI.skin.label);

            double value;
            if (Double.TryParse(currentText, out value))
            {
                leftLabelStyle.normal.textColor = Color.white;
                value *= multiplier;
            }
            else
            {
                leftLabelStyle.normal.textColor = Color.yellow;
                value = defaultValue;
            }
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(leftText, leftLabelStyle, GUILayout.Width(leftWidth));
            newText = GUILayout.TextField(currentText, GUILayout.MinWidth(textWidth));
            GUILayout.Label(rightText, GUILayout.Width(rightWidth));
            GUILayout.EndHorizontal();

            return value;
        }


        //utility function that displays a horizontal row of label-textbox-label and parses the number in the textbox
        public static int doGUITextInput(String leftText, float leftWidth, String currentText, float textWidth, String rightText, float rightWidth, out String newText, int defaultValue)
        {
            GUIStyle leftLabelStyle = new GUIStyle(GUI.skin.label);

            int value;
            if (int.TryParse(currentText, out value))
            {
                leftLabelStyle.normal.textColor = Color.white;
            }
            else
            {
                leftLabelStyle.normal.textColor = Color.yellow;
                value = defaultValue;
            }
            GUILayout.BeginHorizontal(GUILayout.ExpandWidth(true));
            GUILayout.Label(leftText, leftLabelStyle, GUILayout.Width(leftWidth));
            newText = GUILayout.TextField(currentText, GUILayout.MinWidth(textWidth));
            GUILayout.Label(rightText, GUILayout.Width(rightWidth));
            GUILayout.EndHorizontal();

            return value;
        }

        public static double PQSSurfaceHeight(double latitude, double longitude, CelestialBody body)
        {
            if (body.pqsController != null)
            {
                Vector3d pqsRadialVector = QuaternionD.AngleAxis(longitude, Vector3d.down) * QuaternionD.AngleAxis(latitude, Vector3d.forward) * Vector3d.right;
                double ret = body.pqsController.GetSurfaceHeight(pqsRadialVector) - body.pqsController.radius;
                if (ret < 0) ret = 0;
                return ret;
            }
            else
            {
                return 0;
            }
        }

        public static double PQSAltitude(Vector3d pos, CelestialBody body)
        {
            return (pos - body.position).magnitude - body.Radius - PQSSurfaceHeight(body.GetLatitude(pos), body.GetLongitude(pos), body);
        }


        public static GUIStyle buttonStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.button);
            style.onNormal.textColor = style.onFocused.textColor = style.onHover.textColor = style.onActive.textColor = color;
            style.normal.textColor = color;
            return style;
        }

        public static GUIStyle labelStyle(Color color)
        {
            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = color;
            return style;
        }

        public static double Clamp(double x, double min, double max) {
            return Math.Min(Math.Max(x, min), max);
        }

        //keeps angles in the range -180 to 180
        public static double clampDegrees(double angle)
        {
            angle = angle + ((int)(2 + Math.Abs(angle) / 360)) * 360.0; //should be positive
            angle = angle % 360.0;
            if (angle > 180.0) return angle - 360.0;
            else return angle;
        }

        //keeps angles in the range 0 to 360
        public static double clampDegrees360(double angle)
        {
            angle = angle % 360.0;
            if (angle < 0) return angle + 360.0;
            else return angle;
        }

        public static void debugPartStates(Part root, int level)
        {
            String space = "";
            for (int i = 0; i < level; i++) space += " ";
            MonoBehaviour.print(space + root + " - " + root.State);
            foreach (Part child in root.children)
            {
                debugPartStates(child, level + 2);
            }
        }


        public static Orbit computeOrbit(Vessel vessel, Vector3d deltaV, double UT)
        {
            Orbit ret = new Orbit();
            ret.UpdateFromStateVectors(vessel.findWorldCenterOfMass() - vessel.mainBody.position, vessel.orbit.GetVel() + deltaV, vessel.mainBody, UT);
            return ret;
        }

        public static Orbit computeOrbit(Vector3d pos, Vector3d vel, CelestialBody body, double UT)
        {
            Orbit ret = new Orbit();
            ret.UpdateFromStateVectors(pos - body.position, vel, body, UT);
            return ret;
        }

        public static double timeOfClosestApproach(Orbit a, Orbit b, double time)
        {
            double closestApproachTime = time;
            double closestApproachDistance = Double.MaxValue;
            double minTime = time;
            double maxTime = time + a.period;
            int numDivisions = 20;

            for (int iter = 0; iter < 8; iter++)
            {
                double dt = (maxTime - minTime) / numDivisions;
                for (int i = 0; i < numDivisions; i++)
                {
                    double t = minTime + i * dt;
                    double distance = (a.getAbsolutePositionAtUT(t) - b.getAbsolutePositionAtUT(t)).magnitude;
                    if (distance < closestApproachDistance)
                    {
                        closestApproachDistance = distance;
                        closestApproachTime = t;
                    }
                }
                minTime = Clamp(closestApproachTime - dt, time, time + a.period);
                maxTime = Clamp(closestApproachTime + dt, time, time + a.period);
            }

            return closestApproachTime;
        }

        //Gives an estimate of the burn needed to put the vessel on a collision course with targetOrbit.
        //Will only work well if the vessel is already roughly on a collision course.
        public static Vector3d estimateCourseCorrectionDeltaV(Vessel vessel, Orbit targetOrbit, double time)
        {
            //compute the time at which the vessel's orbit crosses the target orbit, or, if
            //there's no intersection, the time of periapsis or apoapsis 
            double closestApproachTime = timeOfClosestApproach(vessel.orbit, targetOrbit, time);

            Vector3d vesselApproachPosition = ARUtils.computeOrbit(vessel, Vector3d.zero, time).getAbsolutePositionAtUT(closestApproachTime);
            Vector3d targetApproachPosition = targetOrbit.getAbsolutePositionAtUT(closestApproachTime);
            double approachSeparation = (targetApproachPosition - vesselApproachPosition).magnitude;

            Vector3d[] burnDirections = new Vector3d[] { Vector3d.up, Vector3d.left, Vector3d.forward };
            double[] separationGradient = new double[3]; //change in separation per dV expended in each direction

            //simulate a small burn with a dV of 0.5 m/s in each of 3 directions and see how the approach separation changes
            double perturbationDeltaV = 0.5; 
            for (int i = 0; i < 3; i++)
            {
                Orbit perturbedOrbit = ARUtils.computeOrbit(vessel, perturbationDeltaV * burnDirections[i], time);

                double perturbedApproachTime = timeOfClosestApproach(perturbedOrbit, targetOrbit, time);
                Vector3d perturbedVesselAppraochPosition = perturbedOrbit.getAbsolutePositionAtUT(perturbedApproachTime);
                Vector3d perturbedTargetApproachPosition = targetOrbit.getAbsolutePositionAtUT(perturbedApproachTime);
                double perturbedApproachSeparation = (perturbedTargetApproachPosition - perturbedVesselAppraochPosition).magnitude;

                separationGradient[i] = (approachSeparation - perturbedApproachSeparation) / perturbationDeltaV;
            }

            double gradientMagnitude = Math.Sqrt(Math.Pow(separationGradient[0], 2) + Math.Pow(separationGradient[1], 2) + Math.Pow(separationGradient[2], 2));

            Vector3d desiredBurnDirection = (separationGradient[0] * burnDirections[0]
                + separationGradient[1] * burnDirections[1]
                + separationGradient[2] * burnDirections[2]).normalized;

            double totalDeltaVRequired = approachSeparation / gradientMagnitude;

            return totalDeltaVRequired * desiredBurnDirection;
        }


        public static float resourceDensity(int type)
        {
            return PartResourceLibrary.Instance.GetDefinition(type).density;
        }

        public static bool isDecoupler(Part p)
        {
            return (p is Decoupler ||
                p is DecouplerGUI ||
                p is RadialDecoupler ||
                p.Modules.OfType<ModuleDecouple>().Count() > 0 ||
                p.Modules.OfType<ModuleAnchoredDecoupler>().Count() > 0);
        }

        //we assume that any SRB with ActivatesEvenIfDisconnected = True is a sepratron:
        public static bool isSepratron(Part p)
        {
            if (!p.ActivatesEvenIfDisconnected) return false; //sepratrons have ActivateEvenIfDisconnected = True

            //old-style SRBs:
            if (p is SolidRocket) return true;

            //new-style SRBs:
            if (p.Modules.OfType<ModuleEngines>().Count() == 0) return false; //sepratrons are motors
            ModuleEngines engine = p.Modules.OfType<ModuleEngines>().First();
            if (engine.throttleLocked) return true;

            return false;
        }

        public static Vector3d swapYZ(Vector3d v)
        {
            return new Vector3d(v.x, v.z, v.y);
        }

        public static void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }


}
