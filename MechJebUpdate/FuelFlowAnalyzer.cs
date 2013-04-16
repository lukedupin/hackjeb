using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

//Fuel flow == (Thrust * 200)/(Isp * 9.81)

namespace MuMech
{
    class FuelFlowAnalyzer
    {

        int simStage;
        List<FuelNode> nodes; //a list of FuelNodes representing all the parts of the ship

        List<Part> parts;

        //Build up the representation of the vessel in terms of FuelNodes, and let them
        //figure out how to draw fuel from each other
        void rebuildVesselRepresenation(List<Part> parts, float atmospheres, bool current_thrust )
        {
            nodes = new List<FuelNode>();
            Dictionary<Part, FuelNode> nodeLookup = new Dictionary<Part, FuelNode>();
            foreach (Part p in parts)
            {
                FuelNode node = new FuelNode(p, atmospheres, current_thrust );
                nodes.Add(node);
                nodeLookup[p] = node;
            }
            foreach (FuelNode n in nodes)
            {
                n.findSourceNodes(nodeLookup);
            }
        }

        //analyze the whole ship and report a) burn time per stage and b) delta V per stage c) TWR per stage
        //given that g = gravity and atmospheric pressure = <atmospheres> atm
        public void analyze(List<Part> parts, float gravity, float atmospheres, bool current_thrust,
                            out float[] timePerStage, out float[] deltaVPerStage, out float[] twrPerStage)
        {
            this.parts = parts;

            //reinitialize our representation of the vessel
            rebuildVesselRepresenation(parts, atmospheres, current_thrust );

            simStage = Staging.lastStage;

            timePerStage = new float[simStage + 1];
            deltaVPerStage = new float[simStage + 1];
            twrPerStage = new float[simStage + 1];

            //simulate fuel consumption until all the stages have been executed
            while (simStage >= 0)
            {
                //print("starting stage " + simStage);

                //beginning of stage # simStage
                float stageTime = 0;
                float stageDeltaV = 0;
                float stageTWR;


                //sum up the thrust of all engines active during this stage
                List<FuelNode> engines = findActiveEngines();
                float totalStageThrust = 0;
                foreach (FuelNode engine in engines) totalStageThrust += engine.thrust;

                stageTWR = (float)(totalStageThrust / (totalShipMass() * gravity));

                int deadmanSwitch = 1000;
                while (!allowedToStage()) //simulate chunks of time until this stage burns out
                {
                    //recompute the list of active engines and their thrust, in case some burned out mid-stage:
                    engines = findActiveEngines();
                    totalStageThrust = 0;
                    foreach (FuelNode engine in engines) totalStageThrust += engine.thrust;

                    //figure the rate at which fuel is draining from each node:
                    assignResourceDrainRates();

                    //foreach (FuelNode n in nodes) n.debugDrainRates();

                    //find how long it will be until some node runs out of fuel:
                    float minResourceDrainTime = 999999999;
                    foreach (FuelNode n in nodes)
                    {
                        float nodeTime = n.minTimeToResourceDrained();
                        if (nodeTime < minResourceDrainTime) minResourceDrainTime = nodeTime;
                    }

                    //advance time until some fuel node is emptied (because nothing exciting happens before then)
                    float dt = minResourceDrainTime;
                    float startMass = totalShipMass();
                    foreach (FuelNode n in nodes) n.drainResources(dt);
                    float endMass = totalShipMass();
                    stageTime += dt;

                    //print("dt = " + dt);

                    //calculate how much dV was produced during this time step
                    if (dt > 0 && startMass > endMass && startMass > 0 && endMass > 0)
                    {
                        stageDeltaV += totalStageThrust * dt / (startMass - endMass) * Mathf.Log(startMass / endMass);
                    }

                    deadmanSwitch--;
                    if (deadmanSwitch <= 0)
                    {
                        //print("dead man switch activated at stage " + simStage + " !!!!");
                        break; //in case we get stuck in an infinite loop due to unanticipated staging logic
                    }
                }

                //record the stats computed for this stage
                timePerStage[simStage] = stageTime;
                deltaVPerStage[simStage] = stageDeltaV;
                twrPerStage[simStage] = stageTWR;

                //advance to the next stage
                simStage--;
                simulateStageActivation();
            }
        }

        bool allowedToStage()
        {
            List<FuelNode> activeEngines = findActiveEngines();

            //if no engines are active, we can always stage
            if (activeEngines.Count == 0) return true;

            //if staging would decouple an active engine or non-empty fuel tank, we're not allowed to stage
            foreach (FuelNode n in nodes)
            {
                if (n.decoupledInStage == (simStage - 1))
                {
                    if (n.containsResources() || (activeEngines.Contains(n) && !ARUtils.isSepratron(n.part)))
                    {
                        return false;
                    }
                }
            }

            //if this isn't the last stage, we're allowed to stage because doing so wouldn't drop anything important
            if (simStage > 0) return true;

            //if this is the last stage, we're not allowed to stage while there are still active engines
            return false;
        }

        //remove all nodes that get decoupled in the current stage
        void simulateStageActivation()
        {
            List<FuelNode> decoupledNodes = new List<FuelNode>();
            foreach (FuelNode n in nodes)
            {
                if (n.decoupledInStage == simStage) decoupledNodes.Add(n);
            }

            foreach (FuelNode d in decoupledNodes)
            {
                nodes.Remove(d); //remove the decoupled nodes from the simulated ship
            }
            foreach (FuelNode n in nodes)
            {
                foreach (FuelNode d in decoupledNodes) n.removeSourceNode(d); //remove the decoupled nodes from the remaining nodes' source lists
            }
        }

        //Sum the mass of all fuel nodes in the simulated ship.
        //FuelNodes dynamically recompute their mass as they lose fuel during the simulation.
        float totalShipMass()
        {
            float ret = 0;
            foreach (FuelNode node in nodes) ret += node.mass;
            return ret;
        }

        //Returns a list of engines that fire during the current simulated stage.
        List<FuelNode> findActiveEngines()
        {
            List<FuelNode> engines = new List<FuelNode>();
            foreach (FuelNode node in nodes)
            {
                if (node.isEngine && node.part.inverseStage >= simStage && node.canDrawNeededResources(nodes))
                {
                    if (HighLogic.LoadedSceneIsFlight && node.part.inverseStage >= Staging.CurrentStage && node.part.Modules.Contains("ModuleEngines") && !((ModuleEngines)node.part.Modules["ModuleEngines"]).getIgnitionState) { continue; } // skip if deactivated ModuleEngine in current stage
                    engines.Add(node);
                }
            }

            return engines;
        }

        //Figure out how much fuel drains from each node per unit time.
        //We do this by finding which engines are active, and then having
        //them figure out where they draw fuel from and at what rate.
        void assignResourceDrainRates()
        {
            foreach (FuelNode n in nodes) n.resetDrainRates();

            List<FuelNode> engines = findActiveEngines();

            foreach (FuelNode engine in engines) engine.assignResourceDrainRates(nodes);
        }


        public static void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }




    //A FuelNode is a compact summary of a Part, containing only the information needed to run the fuel flow simulation. 
    class FuelNode
    {
        //This class condenses a whole set of resources into one object:
        class NodeResources
        {
            public Dictionary<int, float> amounts = new Dictionary<int, float>();

            public void Add(int type, float amount)
            {
                if (amounts.ContainsKey(type)) amounts[type] += amount;
                else amounts[type] = amount;
            }

            public float Amount(int type)
            {
                if (amounts.ContainsKey(type)) return amounts[type];
                else return 0;
            }
        }



        NodeResources resources = new NodeResources();              //the resources contained in the part
        NodeResources resourceConsumptions = new NodeResources();   //the resources this part consumes per unit time when active at full throttle
        NodeResources resourceDrains = new NodeResources();         //the resources being drained from this part per unit time at the current simulation time

        HashSet<FuelNode> sourceNodes = new HashSet<FuelNode>();  //a set of FuelNodes that this node could draw fuel or resources from
        //(for resources that use the ResourceFlowMode STACK_PRIORITY_SEARCH).

        public Part part;               //the Part this FuelNode represents
        public float thrust;            //max thrust of this part
        public int decoupledInStage;    //the stage in which this part will be decoupled from the rocket

        public FuelNode(Part part, float atmospheres, bool current_thrust)
        {
            this.part = part;

            //note which resources this part has:
            foreach (PartResource r in part.Resources) resources.Add(r.info.id, (float)r.amount);

            //new-style engines
            thrust = 0;
            foreach (ModuleEngines engine in part.Modules.OfType<ModuleEngines>())
            {
                if (current_thrust)
                  thrust += engine.finalThrust;
                else
                  thrust += engine.maxThrust;

                //                print("" + part + " has thrust " + thrust);
                float Isp = engine.atmosphereCurve.Evaluate(atmospheres);
                float massFlowRate = thrust / (Isp * 9.81f);
                float sumRatioTimesDensity = 0;
                foreach (ModuleEngines.Propellant propellant in engine.propellants)
                {
                    sumRatioTimesDensity += propellant.ratio * ARUtils.resourceDensity(propellant.id);
                }
                foreach (ModuleEngines.Propellant propellant in engine.propellants)
                {
                    if (propellant.name == "ElectricCharge") continue; //assume that there's a supply of electricity (RTG or solar panels)
                    //float consumptionRate = propellant.ratio * thrust * 200 / (Isp * 9.81f); //is this correct?
                    float consumptionRate = propellant.ratio * massFlowRate / sumRatioTimesDensity;
                    //print("" + part + " consumes " + propellant.name + " at a rate " + consumptionRate);
                    resourceConsumptions.Add(propellant.id, consumptionRate);
                }
            }

            //figure out when this part gets decoupled. We do this by looking through this part and all this part's ancestors
            //and noting which one gets decoupled earliest (i.e., has the highest inverseStage). Some parts never get decoupled
            //and these are assigned decoupledInStage = -1.
            decoupledInStage = -1;
            Part p = part;
            while (true)
            {
                if (ARUtils.isDecoupler(p))
                {
                    if (p.inverseStage > decoupledInStage) decoupledInStage = p.inverseStage;
                }
                if (p.parent == null) break;
                else p = p.parent;
            }
            //print("" + part + " - fuelCrossFeed = " + part.fuelCrossFeed + " - will be decoupled in stage " + decoupledInStage);


        }


        //find the set of nodes from which we could draw fuel, or resources that use the
        //same flow scheme as fuel
        public void findSourceNodes(Dictionary<Part, FuelNode> nodeLookup)
        {
            //we can draw fuel from any fuel lines that point to this part
            foreach (FuelNode node in nodeLookup.Values)
            {
                if (node.part is FuelLine && ((FuelLine)node.part).target == this.part)
                {
                    //print("" + part + " can draw fuel from fuel line " + node.part);
                    sourceNodes.Add(node);
                }
            }

            //we can (sometimes) draw fuel from stacked parts
            foreach (AttachNode attachNode in this.part.attachNodes)
            {
                //decide if it's possible to draw fuel through this node:
                if (attachNode.attachedPart != null                                 //if there is a part attached here            
                    && attachNode.nodeType == AttachNode.NodeType.Stack             //and the attached part is stacked (rather than surface mounted)
                    && attachNode.attachedPart.fuelCrossFeed                        //and the attached part allows fuel flow
                    && !(this.part.NoCrossFeedNodeKey.Length > 0                    //and this part does not forbid fuel flow
                         && attachNode.id.Contains(this.part.NoCrossFeedNodeKey)))  //    through this particular node
                {
                    //print("" + part + " can draw fuel through stack node from " + attachNode.attachedPart);
                    sourceNodes.Add(nodeLookup[attachNode.attachedPart]);
                }
            }


            bool isFuelTank = false;
            foreach (PartResource r in part.Resources)
            {
                if (r.maxAmount > 0 && r.info.name != "ElectricCharge") isFuelTank = true;
            }

            if (!isFuelTank && part.parent != null) sourceNodes.Add(nodeLookup[part.parent]);
        }

        //call this when a node no longer exists, so that this node knows that it's no longer a valid source
        public void removeSourceNode(FuelNode n)
        {
            if (sourceNodes.Contains(n)) sourceNodes.Remove(n);
        }



        //return the mass of the simulated FuelNode. This is not the same as the mass of the Part,
        //because the simulated node may have lost fuel, and thus mass, during the simulation.
        public float mass
        {
            get
            {
                //some parts have no physical significance and KSP ignores their mass parameter:
                if (part.physicalSignificance == Part.PhysicalSignificance.NONE) return 0.0F;

                float mass = part.mass;

                //part.mass is the dry mass. Need to include the mass of resources too:
                foreach (int type in resources.amounts.Keys)
                {
                    mass += (float)(resources.amounts[type] * ARUtils.resourceDensity(type));
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

                return mass;
            }
        }

        public bool isEngine
        {
            get { return thrust > 0; }
        }



        public void resetDrainRates()
        {
            resourceDrains = new NodeResources();
        }

        public float minTimeToResourceDrained()
        {
            float ret = float.MaxValue;
            foreach (int type in resourceDrains.amounts.Keys)
            {
                if (resourceDrains.Amount(type) > 0) ret = Math.Min(ret, resources.Amount(type) / resourceDrains.Amount(type));
            }
            return ret;
        }

        public void drainResources(float dt)
        {
            foreach (int type in resourceDrains.amounts.Keys) resources.Add(type, -dt * resourceDrains.Amount(type));
        }

        public bool containsResources()
        {
            foreach (int type in resources.amounts.Keys)
            {
                if (resources.Amount(type) > 1.0F) return true;
            }
            return false;
        }

        public bool canDrawNeededResources(List<FuelNode> vessel)
        {
            foreach (int type in resourceConsumptions.amounts.Keys)
            {
                switch (PartResourceLibrary.Instance.GetDefinition(type).resourceFlowMode)
                {
                    case ResourceFlowMode.NO_FLOW:
                        //check if we contain the needed resource:
                        if (this.resources.Amount(type) < 1.0F) return false;
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        //check if any part contains the needed resource:
                        if (!vessel.Any<FuelNode>(n => n.resources.Amount(type) > 1.0F)) return false;
                        break;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        if (!this.canSupplyResourceRecursive(type, new List<FuelNode>())) return false;
                        break;

                    default:
                        //do nothing. there's an EVEN_FLOW scheme but nothing seems to use it
                        return false;
                }
            }
            return true; //we didn't find ourselves lacking for any resource
        }

        public void debugDrainRates()
        {
            foreach (int type in resourceDrains.amounts.Keys)
            {
                print("" + part + "'s drain rate of " + PartResourceLibrary.Instance.GetDefinition(type).name + " is " + resourceDrains.Amount(type));
            }
        }

        public void assignResourceDrainRates(List<FuelNode> vessel)
        {
            foreach (int type in resourceConsumptions.amounts.Keys)
            {
                float amount = resourceConsumptions.Amount(type);

                switch (PartResourceLibrary.Instance.GetDefinition(type).resourceFlowMode)
                {
                    case ResourceFlowMode.NO_FLOW:
                        this.resourceDrains.Add(type, amount);
                        break;

                    case ResourceFlowMode.ALL_VESSEL:
                        assignFuelDrainRateAllVessel(type, amount, vessel);
                        break;

                    case ResourceFlowMode.STACK_PRIORITY_SEARCH:
                        //print("======= starting assignResourceDrainRateRecursive(" + type + ", " + amount + ") for " + part);
                        this.assignFuelDrainRateRecursive(type, amount, new List<FuelNode>());
                        break;

                    default:
                        //do nothing. there's an EVEN_FLOW scheme but nothing seems to use it
                        break;
                }
            }
        }


        void assignFuelDrainRateAllVessel(int type, float amount, List<FuelNode> vessel)
        {
            //print("Assigning " + amount + " drain of " + PartResourceLibrary.Instance.GetDefinition(type).name + " from " + part);
            //I don't know how this flow scheme actually works but I'm going to assume
            //that it drains from the part with the highest possible inverseStage
            FuelNode source = null;
            foreach (FuelNode n in vessel)
            {
                //print("  -- looking at " + n.part + " - amount = " + n.resources.Amount(type));
                if (n.resources.Amount(type) > 1.0F)
                {
                    if (source == null || n.part.inverseStage > source.part.inverseStage) source = n;
                }
            }
            //print("draining from source = " + part);
            if (source != null) source.resourceDrains.Add(type, amount);
        }



        //We need to drain <totalDrainRate> of resource <type> per second from somewhere.
        //We're not allowed to drain it through any of the nodes in <visited>.
        //Decide whether to drain it from this node, or pass the recursive buck
        //and drain it from some subset of the sources of this node.
        void assignFuelDrainRateRecursive(int type, float amount, List<FuelNode> visited)
        {
            //print("assignFuelDrainRateRecursive(" + type + ", " + amount + " at " + part + " - visited.Count = " + visited.Count);
            //if we drain from our sources, newVisted is the set of nodes that those sources
            //aren't allowed to drain from. We add this node to that list to prevent loops.
            List<FuelNode> newVisited = new List<FuelNode>(visited);
            newVisited.Add(this);

            //First see if we can drain fuel through fuel lines. If we can, drain equally through
            //all active fuel lines that point to this part. 
            List<FuelNode> fuelLines = new List<FuelNode>();
            foreach (FuelNode n in sourceNodes)
            {
                if (n.part is FuelLine && !visited.Contains(n))
                {
                    if (n.canSupplyResourceRecursive(type, newVisited))
                    {
                        //print("found a fuel line to supply the resource");
                        fuelLines.Add(n);
                    }
                    else
                    {
                        //print("Fuel line can't supply resource");
                    }
                }
            }
            if (fuelLines.Count > 0)
            {
                foreach (FuelNode fuelLine in fuelLines)
                {
                    //print("passing the recursive buck to one or more fuel lines");
                    fuelLine.assignFuelDrainRateRecursive(type, amount / fuelLines.Count, newVisited);
                }
                return;
            }

            //If there are no incoming fuel lines, try other sources.
            //I think there may actually be more structure to the fuel source priority system here. 
            //For instance, can't a fuel tank drain fuel simultaneously from its top and bottom stack nodes?
            foreach (FuelNode n in sourceNodes)
            {
                if (!visited.Contains(n) && n.canSupplyResourceRecursive(type, newVisited))
                {
                    if (drainFromSourceBeforeSelf(type, n))
                    {
                        //print("passing to " + n.part);
                        n.assignFuelDrainRateRecursive(type, amount, newVisited);
                        return;
                    }
                }
            }

            //print("" + part + " in final extremity");
            //in the final extremity, drain the resource from this part
            if (this.resources.Amount(type) > 0)
            {
                this.resourceDrains.Add(type, amount);
            }
        }


        //If we still have fuel, don't drain through the parent unless the parent node is a stack node.
        //For example, a fuel tank radial mounted to a parent fuel tank will drain itself before starting to drain its parent.
        //This is in contrast to the stacked case, where a fuel tank will drain the parent it is stacked under before draining itself.
        //This just seems to be an idiosyncracy of the KSP fuel flow system, which we faithfully simulate.
        bool drainFromSourceBeforeSelf(int type, FuelNode source)
        {
            if (this.resources.Amount(type) < 1.0F) return true;   //if we have no fuel, then obviously we are allowed to drain any source node before this one
            if (source.part != this.part.parent) return true; //we have no qualms about draining non-parents before ourselves
            if (this.part.parent == null) return true; //so that the next line doesn't cause an error

            foreach (AttachNode attachNode in this.part.parent.attachNodes)
            {
                //if we are attached to the parent by a non-stack node, it's not cool to draw fuel from them
                //(unless we have no fuel of our own. that case was handled above.) 
                if (attachNode.attachedPart == this.part && attachNode.nodeType != AttachNode.NodeType.Stack) return false;
            }

            //we only reach here if we have fuel, and source == parent, and parent != null, and we are not attached to the parent by a non-stack node
            //that is, we only reach here if we have fuel, and source == parent, and we are attached to the parent by a stack node
            //In this case it's OK to draw fuel from the parent.
            return true;
        }


        //determine if this FuelNode can supply fuel itself, or can supply fuel by drawing
        //from other sources, without drawing through any node in <visited>
        bool canSupplyResourceRecursive(int type, List<FuelNode> visited)
        {
            if (this.resources.Amount(type) > 1.0F) return true;

            //if we drain from our sources, newVisted is the set of nodes that those sources
            //aren't allowed to drain from. We add this node to that list to prevent loops.
            List<FuelNode> newVisited = new List<FuelNode>(visited);
            newVisited.Add(this);

            foreach (FuelNode n in sourceNodes)
            {
                if (!visited.Contains(n))
                {
                    if (n.canSupplyResourceRecursive(type, newVisited)) return true;
                }
            }

            return false;
        }


        void print(String s)
        {
            MonoBehaviour.print(s);
        }
    }




}
