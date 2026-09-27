using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Rewired;

namespace PerfectedJoystick;

internal enum Stick {
    Left,
    Right
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public class PerfectedJoystick : BaseUnityPlugin {
    private ConfigEntry<Stick> _stick;

    private void Awake() {
        _stick = Config.Bind("", "Stick Selection", Stick.Left,
            "Which joystick on the controller to read input from."
        );
        Config.SettingChanged += OnSettingChanged;
        Logger.LogInfo("Your joystick has been perfected.");
    }

    private void OnEnable() {
        Patch.Hook(Logger, _stick.Value);
    }

    private void OnDisable() {
        Patch.Unhook();
    }

    private void OnSettingChanged(object sender, SettingChangedEventArgs args) {
        // Re-hook if the stick changes, if it's not hooked and the setting is
        // changed it'll get the right value when it's next enabled
        if (Patch.IsAlive())
            Patch.Unhook();
            Patch.Hook(Logger, _stick.Value);
    }
}

internal static class Patch {
    private const BindingFlags Private = BindingFlags.NonPublic;
    private const BindingFlags Instance = BindingFlags.Instance;

    private enum Axis {
        X,
        Y
    }

    private static BepInEx.Logging.ManualLogSource _logger;
    private static Stick _stick;
    private static ILHook _hook;

    internal static void Hook(BepInEx.Logging.ManualLogSource logger, Stick stick) {
        _stick = stick;
        _logger = logger;
        _logger.LogInfo("Hooking `PlayerControl::FixedUpdate`");
        _hook = new ILHook(GetMethod<PlayerControl>("FixedUpdate", Private | Instance), FixedUpdate);
    }

    internal static void Unhook() {
        _hook?.Dispose();
    }

    internal static bool IsAlive() {
        return _hook != null;
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
                Vector2 stick = new Vector2(player.GetAxis(StickAxis(_stick, Axis.X)), player.GetAxis(StickAxis(_stick, Axis.Y)));
                stick = Vector2.ClampMagnitude(stick, 1f);
                bool inMotion = stick.sqrMagnitude > 0.01f;
                stick *= 3.5f; // scale to normal cursor range limit
                Vector2 target = inMotion?
                    (Vector2)self.transform.TransformPoint(new Vector3(stick.x, stick.y, 0f)) :
                    Vector2.Lerp(rb.position, (Vector2)self.tip.position, 0.5f);
                rb.MovePosition(target);
            });
            _logger.LogDebug("Injected IL without error");
        } else {
            _logger.LogError("Failed to hook");
        }
    }

    // resolves names corresponding to the configured rewired axes for joysticks in goi
    private static string StickAxis(Stick stick, Axis axis) {
        return "Stick" + (stick == Stick.Left? "L" : "R") +
            (axis == Axis.X? "X" : "Y");
    }

    private static MethodInfo GetMethod<T>(string name, BindingFlags flags) {
        return typeof(T).GetMethod(name, flags);
    }
}
