using System;
using System.Reflection;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// ResourceAvailability.MakeAvailable call an event without any try catch,
// which can cascade very badly (due to how SystemInitializer.Execute works) and
// kill a lot of code, ranging from game code to other mods code
// even though they were entirely unrelated / could've worked just fine.
internal class SaferResourceAvailability
{
    private static Hook _hook;
    private static FieldInfo _onAvailableBackingFieldInfo;

    internal static void Init()
    {
        var hookConfig = new HookConfig() { ManualApply = true };

        _hook = new Hook(
                        typeof(ResourceAvailability).GetMethod(nameof(ResourceAvailability.MakeAvailable), ReflectionHelper.AllFlags),
                        typeof(SaferResourceAvailability).GetMethod(nameof(TryCatchEachLoopIteration), ReflectionHelper.AllFlags),
                        hookConfig
                    );

        _onAvailableBackingFieldInfo = typeof(ResourceAvailability).GetField(nameof(ResourceAvailability.onAvailable), BindingFlags.Instance | BindingFlags.NonPublic);
    }

    internal static void Enable()
    {
        _hook.Apply();
    }

    internal static void Disable()
    {
        _hook.Undo();
    }

    internal static void Destroy()
    {
        _hook.Free();
    }

    private delegate void orig_MakeAvailable(ref ResourceAvailability self);
    private static void TryCatchEachLoopIteration(orig_MakeAvailable orig, ref ResourceAvailability self)
    {
        if (self.available)
        {
            return;
        }

        self.available = true;

        Action onAvailable = (Action)_onAvailableBackingFieldInfo.GetValue(self);

        if (onAvailable != null)
        {
            foreach (Action item in onAvailable.GetInvocationList())
            {
                try
                {
                    item();
                }
                catch (Exception e)
                {
                    Log.Error(e);
                }
            }

            _onAvailableBackingFieldInfo.SetValue(self, null);
        }
    }
}
