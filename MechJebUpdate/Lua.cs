using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using SharpLua;
using SharpLua.LuaTypes;

namespace MuMech
{
    public class MuMechLua : PartModule
    {
        [KSPField(isPersistant = false)]
        public string loadFile = "";

        [KSPField(isPersistant = false)]
        public string onUpdate = "";

        [KSPField(isPersistant = false)]
        public string onFixedUpdate = "";

        [KSPField(isPersistant = false)]
        public string update = "";

        [KSPField(isPersistant = false)]
        public string fixedUpdate = "";

        [KSPField(isPersistant = false)]
        public string onActive = "";

        [KSPField(isPersistant = false)]
        public string onInactive = "";

        [KSPField(isPersistant = false)]
        public string onStart = "";

        [KSPField(isPersistant = false)]
        public string onFlyByWire = "";

        [KSPField(isPersistant = false)]
        public string onGUI = "";

        protected LuaTable luaEnv;
        protected bool fileLoaded = false;
        protected WWW loader;

        public override void OnStart(StartState state)
        {
            luaEnv = LuaRuntime.CreateGlobalEnviroment();

            vessel.OnFlyByWire += OnFlyByWire;

            fileLoaded = false;

            base.OnStart(state);
        }

        public void MaybeRunCode(string code)
        {
            if ((luaEnv == null) || (part.State == PartStates.DEAD))
            {
                return;
            }

            luaEnv.SetNameValue("module", ObjectToLua.ToLuaValue(this));
            luaEnv.SetNameValue("part", ObjectToLua.ToLuaValue(this.part));
            luaEnv.SetNameValue("vessel", ObjectToLua.ToLuaValue(this.vessel));

            if (!fileLoaded)
            {
                if (loadFile.Length > 0)
                {
                    if (loader == null)
                    {
                        if ((PartLoader.getPartInfoByName(part.partInfo.name).partPath != null) && (PartLoader.getPartInfoByName(part.partInfo.name).partPath.Length > 0))
                        {
                            print("MuMechLua - Loading script - file://" + PartLoader.getPartInfoByName(part.partInfo.name).partPath.Replace("\\", "/") + "/" + loadFile);
                            loader = new WWW("file://" + PartLoader.getPartInfoByName(part.partInfo.name).partPath.Replace("\\", "/") + "/" + loadFile);
                        }
                        else
                        {
                            print("MuMechLua - Null path");
                        }
                    }
                    else if (loader.isDone)
                    {
                        print("MuMechLua - Running script");
                        try
                        {
                            LuaRuntime.Run(loader.text, luaEnv);
                        }
                        catch (Exception e)
                        {
                            print("Exception " + e.Message + "\n" + e.StackTrace);
                        }

                        fileLoaded = true;
                    }
                }
                else
                {
                    fileLoaded = true;
                }

                if (fileLoaded && (onStart.Length > 0))
                {
                    try
                    {
                        LuaRuntime.Run(onStart, luaEnv);
                    }
                    catch (Exception e)
                    {
                        print("Exception " + e.Message + "\n" + e.StackTrace);
                    }
                }
            }

            if (code.Length > 0)
            {
                try
                {
                    LuaRuntime.Run(code, luaEnv);
                }
                catch (Exception e)
                {
                    print("Exception " + e.Message + "\n" + e.StackTrace);
                }
            }
        }

        public override void OnActive()
        {
            MaybeRunCode(onActive);
            base.OnActive();
        }

        public override void OnInactive()
        {
            MaybeRunCode(onInactive);
            base.OnInactive();
        }

        public override void OnUpdate()
        {
            MaybeRunCode(onUpdate);
            base.OnUpdate();
        }

        public override void OnFixedUpdate()
        {
            MaybeRunCode(onFixedUpdate);
            base.OnFixedUpdate();
        }

        public void Update()
        {
            MaybeRunCode(update);
        }

        public void FixedUpdate()
        {
            MaybeRunCode(fixedUpdate);
        }

        public void OnFlyByWire(FlightCtrlState state)
        {
            luaEnv.SetNameValue("state", ObjectToLua.ToLuaValue(state));

            MaybeRunCode(onFlyByWire);

            luaEnv.RemoveKey("state");
        }

        public void OnGUI()
        {
            MaybeRunCode(onGUI);
        }
    }
}
