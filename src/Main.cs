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

        if (cursor.TryGotoNext(MoveType.Before,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<PlayerControl>("fakeCursorRB"),
            x => x.MatchLdloc(9),
            x => x.MatchCallvirt<Rigidbody2D>("MovePosition")
        )) {
            _logger.LogDebug("Hooked successfully");
            cursor.RemoveRange(12);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit<PlayerControl>(OpCodes.Ldfld, "fakeCursorRB");
            cursor.Emit(OpCodes.Ldarg_0);
            cursor.Emit<PlayerControl>(OpCodes.Ldfld, "player");
            cursor.EmitDelegate((PlayerControl self, Rigidbody2D rb, Player player) => {
                Vector2 stick = new Vector2(player.GetAxis("StickLX"), player.GetAxis("StickLY"));
                stick = Vector2.ClampMagnitude(stick, 1f);
                bool inMotion = stick.sqrMagnitude > 0.01f;
                stick *= 3.5f; // scale to normal cursor range limit
                Vector2 target = inMotion?
                    (Vector2)self.transform.TransformPoint(new Vector3(stick.x, stick.y, 0f)) :
                    Vector2.Lerp(rb.position, (Vector2)self.tip.position, 0.5f);
                rb.MovePosition(target);
            });
        } else {
            _logger.LogError("Failed to hook");
        }
    }

    private static MethodInfo GetMethod<T>(string name, BindingFlags flags) {
        return typeof(T).GetMethod(name, flags);
    }
}
