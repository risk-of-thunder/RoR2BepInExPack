using System;
using System.Globalization;
using System.Reflection;
using MonoMod.RuntimeDetour;
using MonoMod.Utils;

namespace RoR2BepInExPack.Utilities
{
    public static class NativeDetourExtensions
    {
        private static readonly FieldInfo backupNativeField = typeof(NativeDetour).GetField("_BackupNative", (BindingFlags)(-1));

        /// <summary>
        /// Generate a new delegate with which you can invoke the previous state.
        /// Instead of an "undo-call-redo" trampoline generated with <see cref="NativeDetour.GenerateTrampoline{T}()"/>
        /// copies bytesCount bytes from original method into the trampoline and then puts detour back to the original method,
        /// effectively splitting the method.
        /// <para/> This way of doing trampoline has restrictions:
        /// <para/>    Part of the method that is in trampoline can't have jumps.
        /// <para/>    The method split should happen between two instructions,
        ///     so you need to supply the amount of bytes that is >= 14 (max size of a detour) and is on the edge of an instruction.
        /// </summary>
        /// <param name="detour">Native detour</param>
        /// <param name="bytesCount">The amount of bytes to copy from original method</param>
        public static T GenerateTrampolineWithRecursionSupport<T>(this NativeDetour detour, uint bytesCount) where T : Delegate
        {
            if (!typeof(Delegate).IsAssignableFrom(typeof(T)))
            {
                throw new InvalidOperationException($"Type {typeof(T)} not a delegate type.");
            }

            return detour.GenerateNativeProxy(typeof(T).GetMethod("Invoke"), bytesCount).CreateDelegate(typeof(T)) as T;
        }

        /// <summary>
        /// Generate a method that executes part of the detoured method then jumps to the original method to continue execution
        /// </summary>
        /// <param name="detour">Native detour</param>
        /// <param name="signature">A MethodBase with the target function's signature.</param>
        /// <param name="bytesCount">The amount of bytes to copy from original method</param>
        /// <returns>The detoured DynamicMethod.</returns>
        public static MethodInfo GenerateNativeProxy(this NativeDetour detour, MethodBase signature, uint bytesCount)
        {
            var returnType = (signature as MethodInfo)?.ReturnType ?? typeof(void);
            var args = signature.GetParameters();
            var argTypes = new Type[args.Length];
            for (var i = 0; i < args.Length; i++)
            {
                argTypes[i] = args[i].ParameterType;
            }

            MethodInfo dm;
            using (var dmd = new DynamicMethodDefinition(
                $"Native<{((long)detour.Data.Method).ToString("X16", CultureInfo.InvariantCulture)}>",
                returnType, argTypes))
            {
                dm = dmd.StubCriticalDetour().Generate().Pin();
            }

            var start = dm.GetNativeStart();
            var detourData = DetourHelper.Native.Create(start.Add(bytesCount), detour.Data.Method.Add(bytesCount));
            var fakeDetourData = new NativeDetourData
            {
                Method = start,
                Size = bytesCount + detourData.Size,
            };

            DetourHelper.Native.MakeWritable(fakeDetourData);

            var backupNative = (IntPtr)backupNativeField.GetValue(detour);
            var extraBytes = bytesCount - detour.Data.Size;

            Write(backupNative, start, detour.Data.Size);
            if (extraBytes > 0)
            {
                Write(detour.Data.Method.Add(detour.Data.Size), start.Add(detour.Data.Size), extraBytes);
            }

            DetourHelper.Native.Apply(detourData);
            DetourHelper.Native.MakeExecutable(fakeDetourData);
            DetourHelper.Native.FlushICache(fakeDetourData);
            DetourHelper.Native.Free(fakeDetourData);

            return dm;
        }

        private static unsafe void Write(IntPtr from, IntPtr to, uint size)
        {
            var fromPtr = (byte*)from.ToPointer();

            var offs = 0;
            for (var i = 0; i < size; i++)
            {
                to.Write(ref offs, fromPtr[i]);
            }
        }

        public static IntPtr Add(this IntPtr ptr, long offset)
        {
            return new IntPtr(ptr.ToInt64() + offset);
        }

        public static IntPtr Add(this IntPtr ptr, uint offset)
        {
            return new IntPtr(ptr.ToInt64() + offset);
        }
    }
}
