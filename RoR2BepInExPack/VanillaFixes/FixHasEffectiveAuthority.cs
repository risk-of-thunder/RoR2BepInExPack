using System;
using System.Collections.Generic;
using System.Text;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;
using UnityEngine.Networking;

namespace RoR2BepInExPack.VanillaFixes;

/// <summary>
/// On a server there is a short time when authority is not assigned yet the object should have authority.
/// Which is the reason for Captain to sometimes not have supply beacon charges.
/// HasEffectiveAuthority makes additional checks to see if object should have authority but not enough.
/// fix: check if clientAuthorityOwner is of type ULocalConnectionToClient
/// </summary>
internal class FixHasEffectiveAuthority
{
    private static ILHook _ilHook;


    internal static void Init()
    {
        var ilHookConfig = new ILHookConfig() { ManualApply = true };
        _ilHook = new ILHook(
            typeof(Util).GetMethod(nameof(Util.HasEffectiveAuthority), ReflectionHelper.AllFlags, null, [typeof(NetworkIdentity)], null),
            FixHook,
            ref ilHookConfig);
    }

    internal static void Enable()
    {
        _ilHook.Apply();
    }

    internal static void Disable()
    {
        _ilHook.Undo();
    }

    internal static void Destroy()
    {
        _ilHook.Free();
    }

    private static void FixHook(ILContext il)
    {
        ILCursor c = new ILCursor(il);
        var ILFound = c.TryGotoNext(MoveType.After,
           x => x.MatchCallOrCallvirt<NetworkIdentity>("get_clientAuthorityOwner"),
           x => x.MatchLdnull(),
           x => x.MatchCeq());

        if (ILFound)
        {
            c.Emit(OpCodes.Dup);
            c.Emit(OpCodes.Brtrue, c.Next);
            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldarg_0);
            c.Emit<NetworkIdentity>(OpCodes.Callvirt, "get_clientAuthorityOwner");
            c.Emit(OpCodes.Isinst, typeof(ULocalConnectionToClient));
            c.Emit(OpCodes.Ldnull);
            c.Emit(OpCodes.Ceq);
            c.Emit(OpCodes.Ldc_I4_0);
            c.Emit(OpCodes.Ceq);
        }
        else
        {
            Log.Error("FixHasEffectiveAuthority TryGotoNext failed, not applying patch");
        }
    }
}
