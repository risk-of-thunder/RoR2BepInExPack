using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Threading;
using MonoMod.RuntimeDetour;
using RoR2;
using RoR2BepInExPack.Reflection;

namespace RoR2BepInExPack.VanillaFixes;

// Gearbox broke system initializer by making it look only at a Type[] in the ror2application behaviour.
// Simple fix really, just analyze everything and add it to that array.
internal class FixSystemInitializer
{
    private static Hook _hook;

    private static Hook _hook2;

    internal static void Init()
    {
        // We need this to call apply as soon as its created.
        _hook = new Hook(
            typeof(SystemInitializerAttribute).GetMethod(nameof(SystemInitializerAttribute.ExecuteStatic), ReflectionHelper.AllFlags),
            typeof(FixSystemInitializer).GetMethod(nameof(EnqueueAllInitializers), ReflectionHelper.AllFlags)
        );

        _hook2 = new Hook(
            typeof(SystemInitializerAttribute).GetMethod(nameof(SystemInitializerAttribute.ExecuteCoroutine), ReflectionHelper.AllFlags),
            typeof(FixSystemInitializer).GetMethod(nameof(LogBadCycle), ReflectionHelper.AllFlags)
        );
    }

    private static IEnumerator LogBadCycle()
    {
        SystemInitializerAttribute.initializerAttributes = new Queue<SystemInitializerAttribute>();
        SystemInitializerAttribute.initThread = new Thread(SystemInitializerAttribute.ExecuteStatic);
        SystemInitializerAttribute.initThread.Start();
        yield return null;
        while (SystemInitializerAttribute.initThread.IsAlive)
        {
            yield return null;
        }
        SystemInitializerAttribute.initThread = null;
        HashSet<Type> initializedTypes = [.. SystemInitializerAttribute.preInitializedTypes];
        SystemInitializerAttribute.preInitializedTypes.Clear();
        new Stopwatch().Start();
        Stopwatch multiLoopTimer = new Stopwatch();
        multiLoopTimer.Start();
        int yields = 0;
        int num = 0;

        var logBadCycle = new StringBuilder();

        while (SystemInitializerAttribute.initializerAttributes.Count > 0)
        {
            SystemInitializerAttribute initializerAttribute2 = SystemInitializerAttribute.initializerAttributes.Dequeue();
            if (!InitializerDependenciesMet(initializerAttribute2))
            {
                logBadCycle.Append($"Re-enqueuing initializer: Type={initializerAttribute2.associatedType.FullName}," +
                    $"Method={initializerAttribute2.methodInfo.Name}");

                SystemInitializerAttribute.initializerAttributes.Enqueue(initializerAttribute2);
                num++;
                if (num >= SystemInitializerAttribute.initializerAttributes.Count + 10)
                {
                    Log.Error($"SystemInitializerAttribute infinite loop detected." +
                        $"currentMethod={initializerAttribute2.associatedType.FullName}.{initializerAttribute2.methodInfo.Name}" +
                        $"\n{logBadCycle}");
                    break;
                }
                continue;
            }
            IEnumerator thisStep = initializerAttribute2.methodInfo.Invoke(null, Array.Empty<object>()) as IEnumerator;
            List<IEnumerator> coroutineStack = new List<IEnumerator> { thisStep };
            multiLoopTimer.Stop();
            long accumulatedTime = multiLoopTimer.ElapsedMilliseconds;
            multiLoopTimer.Start();
            if (accumulatedTime > SystemInitializerAttribute.FRAME_TIME_THRESHOLD_MS)
            {
                accumulatedTime = 0L;
                multiLoopTimer.Stop();
                multiLoopTimer.Reset();
                multiLoopTimer.Start();
                yields++;
                yield return null;
            }
            while (thisStep != null)
            {
                while (accumulatedTime < SystemInitializerAttribute.FRAME_TIME_THRESHOLD_MS && thisStep != null)
                {
                    if (!thisStep.MoveNext())
                    {
                        coroutineStack.RemoveAt(coroutineStack.Count - 1);
                        thisStep = ((coroutineStack.Count <= 0) ? null : coroutineStack[coroutineStack.Count - 1]);
                    }
                    else if (thisStep.Current != null)
                    {
                        coroutineStack.Add(thisStep.Current as IEnumerator);
                        thisStep = coroutineStack[coroutineStack.Count - 1];
                    }
                    multiLoopTimer.Stop();
                    accumulatedTime = multiLoopTimer.ElapsedMilliseconds;
                    multiLoopTimer.Start();
                }
                if (accumulatedTime > SystemInitializerAttribute.FRAME_TIME_THRESHOLD_MS)
                {
                    accumulatedTime = 0L;
                    multiLoopTimer.Stop();
                    multiLoopTimer.Reset();
                    multiLoopTimer.Start();
                }
                if (thisStep != null)
                {
                    yields++;
                    accumulatedTime = 0L;
                    multiLoopTimer.Stop();
                    multiLoopTimer.Reset();
                    multiLoopTimer.Start();
                    yield return null;
                }
            }
            initializedTypes.Add(initializerAttribute2.associatedType);
            num = 0;
        }
        SystemInitializerAttribute.initializerAttributes = null;
        SystemInitializerAttribute.hasExecuted = true;
        bool InitializerDependenciesMet(SystemInitializerAttribute initializerAttribute)
        {
            Type[] array = initializerAttribute.dependencies;
            foreach (Type item in array)
            {
                initializedTypes.Contains(item);
                if (!initializedTypes.Contains(item))
                {
                    return false;
                }
            }
            return true;
        }
    }

    // Do not call orig.
    private static void EnqueueAllInitializers(Action _)
    {
        var instances = HG.Reflection.SearchableAttribute.GetInstances<SystemInitializerAttribute>();
        foreach (var instance in instances)
        {
            var target = (MethodInfo)instance.target;
            if (!target.IsStatic)
            {
                continue;
            }

            var casted = (SystemInitializerAttribute)instance;
            casted.methodInfo = target;
            casted.associatedType = target.DeclaringType;

            SystemInitializerAttribute.initializerAttributes.Enqueue(casted);
        }
    }
}
