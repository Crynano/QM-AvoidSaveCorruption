using HarmonyLib;
using MGSC;
using SimpleJSON;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AvoidSaveCorruption
{
    [HarmonyPatch(typeof(LoadFromJSON), nameof(LoadFromJSON.CreateObjectFromJSON))]
    public static class LoadFromJSON_SkipObjectUponFailure_Patch
    {
        public static bool Prefix(JSONNode node, Type type, MainMenuScreen __instance, out object __result)
        {
            if (type == typeof(bool[]))
            {
                if (node.IsArray)
                {
                    JSONArray asArray = node.AsArray;
                    bool[] array = new bool[asArray.Count];
                    for (int i = 0; i < asArray.Count; i++)
                    {
                        array[i] = asArray[i].AsBool;
                    }
                    __result = array;
                    return false;
                }
                Debug.LogError($"Expected JSONArray for bool[], but got: {node}");
                __result = null;
                return false;
            }
            if (type.IsGenericType)
            {
                if (type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    JSONArray asArray2 = node.AsArray;
                    Type type2 = type.GetGenericArguments()[0];
                    IList list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(type2));
                    {
                        foreach (JSONNode child in asArray2.Children)
                        {
                            object obj = LoadFromJSON.CreateObjectFromJSON(child, type2);
                            if (obj != null)
                            {
                                list.Add(obj);
                            }
                        }
                        __result = list;
                        return false;
                    }
                }
                if (type.GetGenericTypeDefinition() == typeof(Dictionary<,>))
                {
                    JSONArray asArray3 = node.AsArray;
                    Type type3 = type.GetGenericArguments()[0];
                    Type type4 = type.GetGenericArguments()[1];
                    IDictionary dictionary = (IDictionary)Activator.CreateInstance(typeof(Dictionary<,>).MakeGenericType(type3, type4));
                    if (asArray3 != null)
                    {
                        foreach (JSONNode child2 in asArray3.Children)
                        {
                            JSONNode node2 = child2["Key"];
                            JSONNode node3 = child2["Value"];
                            dictionary.Add(LoadFromJSON.CreateObjectFromJSON(node2, type3), LoadFromJSON.CreateObjectFromJSON(node3, type4));
                        }
                        __result = dictionary;
                        return false;
                    }
                }
                Debug.LogError($"Failed load data of type {type} from JSON, unknown generic type.");
                __result = null;
                return false;
            }
            if (ParseHelper.HasParserForType(type) && !LoadFromJSON._ignoreParseHelperTypes.Contains(type))
            {
                if (string.IsNullOrEmpty(node.Value) && node.Count == 0)
                {
                    __result = null;
                    return false;
                }
                __result = ParseHelper.ParseByType(type, node.Value);
                return false;
            }
            if (type.IsClass || MGSC.SerializationHelper.IsStruct(type))
            {
                if (string.IsNullOrEmpty(node.Value) && node.Count == 0)
                {
                    __result = null;
                    return false;
                }
                bool flag = typeof(IWrapTypeOnSave).IsAssignableFrom(type);
                if (flag)
                {
                    JSONNode jSONNode = node["Type"];
                    foreach (Func<string, Type> wrappedTypeResolver in LoadFromJSON._wrappedTypeResolvers)
                    {
                        type = wrappedTypeResolver(jSONNode);
                        if (type != null)
                        {
                            break;
                        }
                    }
                    if (type == null)
                    {
                        Debug.LogError($"Failed load json, unable to resolve unknown type '{jSONNode}.'");
                        __result = null;
                        return false;
                    }
                    node = node["Content"];
                }
                object obj2 = null;
                if (LoadFromJSON._customValidators.TryGetValue(type, out var value) && !value(type, node))
                {
                    __result = null;
                    return false;
                }
                if (LoadFromJSON._customActivators.TryGetValue(type, out var value2))
                {
                    obj2 = value2(type, node);
                }
                else
                {
                    foreach (KeyValuePair<Type, Func<Type, JSONNode, object>> customActivator in LoadFromJSON._customActivators)
                    {
                        if (customActivator.Key.IsAssignableFrom(type))
                        {
                            obj2 = customActivator.Value(type, node);
                            break;
                        }
                    }
                    if (obj2 == null)
                    {
                        obj2 = Activator.CreateInstance(type);
                    }
                }
                if (obj2 is IManualLoad manualLoad)
                {
                    manualLoad.OnManualLoad(node);
                }
                else
                {
                    LoadFromJSON.LoadFieldsAndProperties(obj2, node, flag);
                }
                try
                {
                    if (obj2 is IAfterLoad afterLoad)
                    {
                        afterLoad.OnAfterLoad();
                    }
                }
                catch
                {
                    Debug.LogError($"Exception in OnAfterLoad. Object probably doesnt exist.");
                    __result = null;
                    return false;
                }
                __result = obj2;
                return false;
            }
            Debug.LogError($"Failed load data of type {type} from JSON.");
            __result = null;
            return false;
        }
    }
}
