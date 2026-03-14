using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace EasyDeliveryCoUltrawide
{
    public partial class Plugin
    {
        private static string GetHierarchyPath(Transform transform)
        {
            if (transform == null)
            {
                return "(null)";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static bool Approximately(Vector2 a, Vector2 b)
        {
            return Mathf.Abs(a.x - b.x) < 0.001f && Mathf.Abs(a.y - b.y) < 0.001f;
        }

        private static Camera GetRacePlayerCamera(object racePlayer)
        {
            if (racePlayer == null)
            {
                return null;
            }

            var camField = AccessTools.Field(racePlayer.GetType(), "cam");
            if (camField == null)
            {
                return null;
            }

            return camField.GetValue(racePlayer) as Camera;
        }

        private static void PatchByName(Harmony harmony, string typeName, string methodName, string prefix = null, string postfix = null)
        {
            var type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                if (_debugMode != null && _debugMode.Value)
                {
                    _log.LogWarning($"Type '{typeName}' not found for patch {methodName}.");
                }
                return;
            }

            MethodInfo method;
            try
            {
                method = AccessTools.Method(type, methodName);
            }
            catch (AmbiguousMatchException)
            {
                method = ResolveAmbiguousMethod(type, methodName);
            }
            if (method == null)
            {
                if (_debugMode != null && _debugMode.Value)
                {
                    _log.LogWarning($"Method '{typeName}.{methodName}' not found for patch.");
                }
                return;
            }

            HarmonyMethod prefixMethod = null;
            HarmonyMethod postfixMethod = null;

            if (!string.IsNullOrWhiteSpace(prefix))
            {
                prefixMethod = new HarmonyMethod(typeof(Plugin), prefix);
            }

            if (!string.IsNullOrWhiteSpace(postfix))
            {
                postfixMethod = new HarmonyMethod(typeof(Plugin), postfix);
            }

            harmony.Patch(method, prefixMethod, postfixMethod);
        }

        private static MethodInfo ResolveAmbiguousMethod(Type type, string methodName)
        {
            MethodInfo best = null;
            int bestParamCount = int.MaxValue;
            for (var current = type; current != null; current = current.BaseType)
            {
                var methods = current.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                foreach (var candidate in methods)
                {
                    if (!string.Equals(candidate.Name, methodName, StringComparison.Ordinal))
                    {
                        continue;
                    }

                    int paramCount = candidate.GetParameters().Length;
                    if (paramCount == 0)
                    {
                        return candidate;
                    }

                    if (paramCount < bestParamCount)
                    {
                        bestParamCount = paramCount;
                        best = candidate;
                    }
                }
            }

            if (best == null && _debugMode != null && _debugMode.Value)
            {
                _log.LogWarning($"Ambiguous match for '{type.Name}.{methodName}', but no overload resolved.");
            }

            return best;
        }
    }
}
