using HarmonyLib;
using MGSC;
using SimpleJSON;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace AvoidSaveCorruption
{
    [HarmonyPatch(typeof(LoadFromJSON), nameof(LoadFromJSON.CreateObjectFromJSON))]
    public static class LoadFromJSON_SkipObjectUponFailure_Patch
    {
        public static bool Prefix(JSONNode node, Type type, ref object __result)
        {
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
            return true;
        }
    }
}
