using System;
using System.Reflection;
using System.ComponentModel;
using BepInEx;
using BepInEx.Configuration;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using UnityEngine;
using Rewired;

namespace PerfectedJoystick;

public enum Stick {
    // Hopefully this should display as left and right in the config file
    [Description("Left")]
    StickL,
    [Description("Right")]
    StickR
}

[BepInPlugin(MyPluginInfo.PLUGIN_GUID, MyPluginInfo.PLUGIN_NAME, MyPluginInfo.PLUGIN_VERSION)]
public sealed class Plugin : BaseUnityPlugin {
    public static Plugin Instance { get; private set; }

    public ConfigEntry<Stick> Stick { get; private set; }
    private ILHook _hook;

    private void Awake() {
        Instance = this;
        Stick = Config.Bind("", "Stick Selection", PerfectedJoystick.Stick.StickL,
            "Which joystick on the controller to read input from.");
        Logger.LogInfo("Your joystick has been perfected.");
    }

    private void OnEnable() {
        Logger.LogInfo("Hooking `PlayerControl::FixedUpdate`");
        _hook = new ILHook(
            typeof(PlayerControl).GetMethod("FixedUpdate", BindingFlags.NonPublic | BindingFlags.Instance),
            PlayerControl_FixedUpdate
        );
    }

    private void OnDisable() {
        Logger.LogInfo("Releasing `PlayerControl::FixedUpdate`");
        _hook?.Dispose();
    }

    private static void PlayerControl_FixedUpdate(ILContext il) {
        var cursor = new ILCursor(il);

        if (cursor.TryGotoNext(MoveType.Before,
            x => x.MatchLdarg(0),
            x => x.MatchLdfld<PlayerControl>("fakeCursorRB"),
            x => x.MatchLdloc(9),
            x => x.MatchCallvirt<Rigidbody2D>("MovePosition")
        )) {
            Instance.Logger.LogDebug("Hooked successfully");
            try {
                cursor.RemoveRange(12);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit<PlayerControl>(OpCodes.Ldfld, "fakeCursorRB");
                cursor.Emit(OpCodes.Ldarg_0);
                cursor.Emit<PlayerControl>(OpCodes.Ldfld, "player");
                cursor.EmitDelegate((PlayerControl self, Rigidbody2D rb, Player player) => {
                    Vector2 stick = new Vector2(
                        player.GetAxis($"{Instance.Stick.Value.ToString()}X"),
                        player.GetAxis($"{Instance.Stick.Value.ToString()}Y")
                    );
                    stick = Vector2.ClampMagnitude(stick, 1f);
                    bool inMotion = stick.sqrMagnitude > 0.01f;
                    stick *= 3.5f; // scale to normal cursor range limit
                    Vector2 target = inMotion?
                        (Vector2)self.transform.TransformPoint(new Vector3(stick.x, stick.y, 0f)) :
                        Vector2.Lerp(rb.position, (Vector2)self.tip.position, 0.5f);
                    rb.MovePosition(target);
                });
            } catch (Exception err) {
                Instance.Logger.LogError($"Error occurred in IL injection: {err}");
            } finally {
                Instance.Logger.LogDebug("Injected IL without error");
            }
        } else {
            Instance.Logger.LogError("Failed to hook");
        }
    }
}
