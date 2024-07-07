using System;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.NativeStructs;
using RoR2BepInExPack.Utilities;

namespace RoR2BepInExPack.UnityEngineHooks
{
    /// <summary>
    /// Some mods add prefabs loaded from asset bundles directly as children
    /// to runtime prefabs instantiated with PrefabsAPI.InstantiateClone.
    /// And it seems like these prefabs are destroyed before runtime prefabs when the game is closing,
    /// which results in a harmless, but certainly annoying crash.
    /// Adds a null check to PreDestroyRecursive native function.
    /// </summary>
    public class PreDestroyRecursiveNullCheck
    {
        /// <summary>
        /// Function offset inside UnityPlayer.dll
        /// </summary>
        private const long Offset = 0x78fea0;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void PreDestroyRecursiveDelegate(IntPtr gameObject, IntPtr destroyedObjectCount);

        private static PreDestroyRecursiveDelegate original;
        private static NativeDetour _detour;

        public static void Init(IntPtr baseAddress)
        {
            var hookPtr = Marshal.GetFunctionPointerForDelegate(new PreDestroyRecursiveDelegate(OnPreDestroyGameObjectRecursive));

            _detour = new NativeDetour(baseAddress.Add(Offset), hookPtr);
            original = _detour.GenerateTrampolineWithRecursionSupport<PreDestroyRecursiveDelegate>(14);
        }

        private static unsafe void OnPreDestroyGameObjectRecursive(IntPtr gameObject, IntPtr something)
        {
            var ptr = ((GameObjectStruct*)gameObject.ToPointer())->componentsPtr.ToInt64();
            if (ptr == 0)
            {
                return;
            }

            original(gameObject, something);
        }
    }
}
