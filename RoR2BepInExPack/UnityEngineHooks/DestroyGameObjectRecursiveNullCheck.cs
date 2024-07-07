using System;
using System.Runtime.InteropServices;
using MonoMod.RuntimeDetour;
using RoR2BepInExPack.NativeStructs;
using RoR2BepInExPack.Utilities;

namespace RoR2BepInExPack.UnityEngineHooks
{
    /// <summary>
    /// <see cref="PreDestroyRecursiveNullCheck"/> for explanation.
    /// Adds a null check to DestroyGameObjectRecursive native function.
    /// </summary>
    public class DestroyGameObjectRecursiveNullCheck
    {
        /// <summary>
        /// Function offset inside UnityPlayer.dll
        /// </summary>
        private const long Offset = 0x78e4d0;

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate void DestroyGameObjectRecursiveDelegate(IntPtr gameObject, IntPtr batchDelete);

        private static DestroyGameObjectRecursiveDelegate original;
        private static NativeDetour _detour;

        public static void Init(IntPtr baseAddress)
        {
            var hookPtr = Marshal.GetFunctionPointerForDelegate(new DestroyGameObjectRecursiveDelegate(OnDestroyGameObjectRecursive));

            _detour = new NativeDetour(baseAddress.Add(Offset), hookPtr);
            original = _detour.GenerateTrampolineWithRecursionSupport<DestroyGameObjectRecursiveDelegate>(15);
        }

        private static unsafe void OnDestroyGameObjectRecursive(IntPtr gameObject, IntPtr something)
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
