using System.Reflection;
using BepInEx;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Rewired;

namespace PerfectedJoystick;

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PerfectedJoystick : BaseUnityPlugin {
    // private Player playerRef;

    private void Awake() {
        // Plugin startup logic
        Logger.LogInfo($"Plugin {MyPluginInfo.PLUGIN_GUID} is loaded!");
    }

    private void OnEnable() {
        // Do patch
        // SceneManager.sceneLoaded += OnSceneLoad;
        Patch.Hook(Logger);
    }

    private void OnDisable() {
        // Undo patch
        // SceneManager.sceneLoaded -= OnSceneLoad;
        Patch.Unhook();
    }
}

// Current idea: set the joystick to absolute mode, then
// within PlayerControl::FixedUpdate get its position, 
// interpret it as a position in dio's local coordinates
// maybe multiply by some fixed coefficient to scale it
// set the cursor position to this coordinate every
internal static class Patch {
    private const BindingFlags Private = BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance;

    private static BepInEx.Logging.ManualLogSource _logger;
    private static ILHook _hook;

    internal static void Hook(BepInEx.Logging.ManualLogSource logger) {
        _logger = logger;
        _logger.LogDebug("Hooking `PlayerControl::FixedUpdate`");
        _hook = new ILHook(GetMethod<PlayerControl>("FixedUpdate", Private | Instance), FixedUpdate);
    }

    internal static void Unhook() {
        _hook?.Dispose();
    }

    private static void FixedUpdate(ILContext il) {
        var cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.After,
            x => x.MatchLdloca(6),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<PlayerControl>("player"),
            x => x.MatchLdstr("StickLX"),
            x => x.MatchCallvirt<Player>("GetAxis"),
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<PlayerControl>("player"),
            x => x.MatchLdstr("StickLY"),
            x => x.MatchCallvirt<Player>("GetAxis")
        )) {
            _logger.LogDebug("Hooked `PlayerControl::FixedUpdate` successfully");
            cursor.Index += 1;
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit<PlayerControl>(OpCodes.Ldfld, "player");
            _logger.LogDebug("Emitted the shit out of that local var loadsession");
            cursor.EmitDelegate((Player player) => {
                _logger.LogDebug($"Joystick input: [{player.GetAxis("StickLX")}, {player.GetAxis("StickLY")}]");
            });
            _logger.LogDebug($"Well no problems here {cursor.ToString()}");
        } else {
            _logger.LogError("Failed to hook `PlayerControl::FixedUpdate`");
        }
    }

    private static MethodInfo GetMethod<T>(string name, BindingFlags flags) {
        return typeof(T).GetMethod(name, flags);
    }
}
