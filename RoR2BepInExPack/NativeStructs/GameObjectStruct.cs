using System;
using System.Runtime.InteropServices;

namespace RoR2BepInExPack.NativeStructs
{
    /// <summary>
    /// Part of a native struct for GameObject
    /// </summary>
    [StructLayout(LayoutKind.Explicit)]
    public struct GameObjectStruct
    {
        [FieldOffset(0x30)]
        public IntPtr componentsPtr;
    }
}
