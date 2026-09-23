using BepInEx;
using HarmonyLib;
using UnityEngine;
using Rewired;

namespace PerfectedJoystick;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PerfectedJoystick : BaseUnityPlugin {
    private void Awake() {
        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnEnable() {
        // Do patch
    }

    private void OnDisable() {
        // Undo patch
    }
}

// Current idea: set the joystick to absolute mode, then
// within PlayerControl::FixedUpdate get its position, 
// interpret it as a position in dio's local coordinates
// maybe multiply by some fixed coefficient to scale it
// set the cursor position to this coordinate every
internal static class Patch {
    
}
