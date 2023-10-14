using System;
using System.Collections.Generic;
using System.Text;
using MonoMod.RuntimeDetour;
using HG.GeneralSerializer;
using RoR2BepInExPack.Reflection;
using SerializationHandler = HG.GeneralSerializer.StringSerializer.SerializationHandler;
using Unity.Audio;
using BepInEx;
using static RoR2.Skills.SkillFamily;
using UnityEngine;
using System.Globalization;

namespace RoR2BepInExPack.Utilities;
internal static class ExtendedStringSerialization
{
    private static readonly Dictionary<Type, SerializationHandler> _serializationHandlers = new Dictionary<Type, StringSerializer.SerializationHandler>();

    private static readonly SerializationHandler _enumHandler;
    private static CultureInfo Invariant => CultureInfo.InvariantCulture;

    private static Hook _canSerializeTypeHook;
    private static Hook _deserializeHook;
    private static Hook _serializeHook;

    internal static void Init()
    {
        var onHookConfig = new HookConfig { ManualApply = true };
        _canSerializeTypeHook = new Hook(typeof(StringSerializer).GetMethod(nameof(StringSerializer.CanSerializeType), ReflectionHelper.AllFlags), CanSerializeType, onHookConfig);
        _deserializeHook = new Hook(typeof(StringSerializer).GetMethod(nameof(StringSerializer.Deserialize), ReflectionHelper.AllFlags), Deserialize, onHookConfig);
        _serializeHook = new Hook(typeof(StringSerializer).GetMethod(nameof(StringSerializer.Serialize), ReflectionHelper.AllFlags), Serialize, onHookConfig);
    }

    internal static void Enable()
    {
        _canSerializeTypeHook.Apply();
        _deserializeHook.Apply();
        _serializeHook.Apply();
    }

    internal static void Disable()
    {
        _canSerializeTypeHook.Undo();
        _deserializeHook.Undo();
        _serializeHook.Undo();
    }

    internal static void Destroy()
    {
        _canSerializeTypeHook.Free();
        _deserializeHook.Free();
        _serializeHook.Free();
    }

    private static bool CanSerializeType(Func<Type, bool> orig, Type type)
    {
        var canSerializeByDefault = orig(type);
        if(canSerializeByDefault)
        {
            return true;
        }
        return _serializationHandlers.ContainsKey(type) || type.IsEnum;
    }

    private static object Deserialize(Func<Type, string, object> orig, Type type, string str)
    {
        object value = orig(type, str);
        if (value != null)
            return value;

        if(type.IsEnum)
            return Deserialize(str, _enumHandler);


        if(_serializationHandlers.TryGetValue(type, out var handler))
            return Deserialize(str, handler);

        return null;
    }

    private static object Deserialize(string serializedValue, SerializationHandler handler)
    {
        try
        {
            return handler.deserializer(serializedValue);
        }
        catch (Exception exception)
        {
            throw new StringSerializerException(null, exception);
        }
    }

    private static string Serialize(Func<Type, object, string> orig, Type type, object value)
    {
        string serializedValue = orig(type, value);
        if (!serializedValue.IsNullOrWhiteSpace())
            return serializedValue;

        if(type.IsEnum)
        {
            return Serialize(value, _enumHandler);
        }

        if (_serializationHandlers.TryGetValue(type, out var handler))
            return Serialize(value, handler);

        return null;
    }

    private static string Serialize(object value, SerializationHandler handler)
    {
        try
        {
            return handler.serializer(value);
        }
        catch (Exception exception)
        {
            throw new StringSerializerException(null, exception);
        }
    }

    private static string[] SplitToComponents(string str, Type type, int minimumComponentcount)
    {
        return StringSerializer.SplitToComponents(str, type, minimumComponentcount);
    }

    static ExtendedStringSerialization()
    {
        _serializationHandlers.Add(typeof(LayerMask), new SerializationHandler
        {
            serializer = (obj) =>
            {
                LayerMask mask = (LayerMask)obj;
                return mask.value.ToString(Invariant);
            },
            deserializer = (str) =>
            {
                LayerMask mask = new LayerMask { value = int.Parse(str, Invariant) };
                return mask;
            }
        });

        _serializationHandlers.Add(typeof(Vector4), new SerializationHandler
        {
            serializer = (obj) =>
            {
                Vector4 vector = (Vector4)obj;
                return $"{vector.x.ToString(Invariant)} {vector.y.ToString(Invariant)} {vector.z.ToString(Invariant)} {vector.w.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(Vector4), 4);
                return new Vector4(float.Parse(components[0], Invariant), float.Parse(components[1], Invariant), float.Parse(components[2], Invariant), float.Parse(components[3], Invariant));
            }
        });

        _serializationHandlers.Add(typeof(Rect), new SerializationHandler
        {
            serializer = (obj) =>
            {
                Rect rect = (Rect)obj;
                return $"{rect.x.ToString(Invariant)} {rect.y.ToString(Invariant)} {rect.width.ToString(Invariant)} {rect.height.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(Rect), 4);
                return new Rect(float.Parse(components[0], Invariant), float.Parse(components[1], Invariant), float.Parse(components[2], Invariant), float.Parse(components[3], Invariant));
            }
        });

        _serializationHandlers.Add(typeof(RectInt), new SerializationHandler
        {
            serializer = (obj) =>
            {
                RectInt rect = (RectInt)obj;
                return $"{rect.x.ToString(Invariant)} {rect.y.ToString(Invariant)} {rect.width.ToString(Invariant)} {rect.height.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(RectInt), 4);
                return new RectInt(int.Parse(components[0], Invariant), int.Parse(components[1], Invariant), int.Parse(components[2], Invariant), int.Parse(components[3], Invariant));
            }
        });
        _serializationHandlers.Add(typeof(char), new SerializationHandler
        {
            serializer = (obj) =>
            {
                char character = (char)obj;
                return character.ToString(Invariant);
            },
            deserializer = (str) =>
            {
                return char.Parse(str);
            }
        });

        _serializationHandlers.Add(typeof(Bounds), new SerializationHandler
        {
            serializer = (obj) =>
            {
                Bounds bounds = (Bounds)obj;
                return $"{bounds.center.x.ToString(Invariant)} {bounds.center.y.ToString(Invariant)} {bounds.center.z.ToString(Invariant)} " +
                $"{bounds.size.x.ToString(Invariant)} {bounds.size.y.ToString(Invariant)} {bounds.size.z.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(Bounds), 6);
                Vector3 center = new Vector3(float.Parse(components[0], Invariant), float.Parse(components[1], Invariant), float.Parse(components[2], Invariant));
                Vector3 size = new Vector3(float.Parse(components[3], Invariant), float.Parse(components[4], Invariant), float.Parse(components[5], Invariant));
                return new Bounds(center, size);
            }
        });

        _serializationHandlers.Add(typeof(BoundsInt), new SerializationHandler
        {
            serializer = (obj) =>
            {
                BoundsInt bounds = (BoundsInt)obj;
                return $"{bounds.position.x.ToString(Invariant)} {bounds.position.y.ToString(Invariant)} {bounds.position.z.ToString(Invariant)} " +
                $"{bounds.size.x.ToString(Invariant)} {bounds.size.y.ToString(Invariant)} {bounds.size.z.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(BoundsInt), 6);
                Vector3Int position = new Vector3Int(int.Parse(components[0], Invariant), int.Parse(components[1], Invariant), int.Parse(components[2], Invariant));
                Vector3Int size = new Vector3Int(int.Parse(components[3], Invariant), int.Parse(components[4], Invariant), int.Parse(components[5], Invariant));
                return new BoundsInt
                {
                    position = position,
                    size = size,
                };
            }
        });

        _serializationHandlers.Add(typeof(Quaternion), new SerializationHandler
        {
            serializer = (obj) =>
            {
                Quaternion quat = (Quaternion)obj;
                return $"{quat.x.ToString(Invariant)} {quat.y.ToString(Invariant)} {quat.z.ToString(Invariant)} {quat.w.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(Quaternion), 4);
                return new Quaternion(float.Parse(components[0], Invariant), float.Parse(components[1], Invariant), float.Parse(components[2], Invariant), float.Parse(components[3], Invariant));
            }
        });

        _serializationHandlers.Add(typeof(Vector2Int), new SerializationHandler
        {
            serializer = (obj) =>
            {
                Vector2Int vector = (Vector2Int)obj;
                return $"{vector.x.ToString(Invariant)} {vector.y.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(Vector2Int), 2);
                return new Vector2Int(int.Parse(components[0], Invariant), int.Parse(components[1], Invariant));
            }
        });

        _serializationHandlers.Add(typeof(Vector3Int), new SerializationHandler
        {
            serializer = (obj) =>
            {
                Vector3Int vector = (Vector3Int)obj;
                return $"{vector.x.ToString(Invariant)} {vector.y.ToString(Invariant)} {vector.z.ToString(Invariant)}";
            },
            deserializer = (str) =>
            {
                string[] components = SplitToComponents(str, typeof(Vector3Int), 3);
                return new Vector3Int(int.Parse(components[0], Invariant), int.Parse(components[1], Invariant), int.Parse(components[2], Invariant));
            }
        });

        _enumHandler = new SerializationHandler
        {
            serializer = (obj) => JsonUtility.ToJson(EnumJSONIntermediate.ToJSON((Enum)obj)),
            deserializer = (str) =>
            {
                if (string.IsNullOrEmpty(str))
                    return default(Enum);
                EnumJSONIntermediate intermediate = JsonUtility.FromJson<EnumJSONIntermediate>(str);
                return EnumJSONIntermediate.ToEnum(in intermediate);
            }
        };
    }

    private struct EnumJSONIntermediate
    {
        public string assemblyQualifiedName;
        public string values;

        public static Enum ToEnum(in EnumJSONIntermediate intermediate)
        {
            return (Enum)Enum.Parse(Type.GetType(intermediate.assemblyQualifiedName), intermediate.values);
        }

        public static EnumJSONIntermediate ToJSON(Enum @enum)
        {
            EnumJSONIntermediate result = new EnumJSONIntermediate
            {
                assemblyQualifiedName = @enum.GetType().AssemblyQualifiedName,
                values = @enum.ToString()
            };
            return result;
        }
    }
}
