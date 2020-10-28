using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace Jackdaw.Unity
{
    public static class InterfaceInfos
    {
        public static readonly string Version = "0.0.1-dev";
    }

    public static class Extensions
    {
        public static string ToJson(this RequestData data)
        {
            return JsonUtility.ToJson(data);
        }

        public static void FromJson(this RequestData data, string json)
        {
            data = JsonUtility.FromJson<RequestData>(json);
        }

        public static string ToJson(this RequestCommand data)
        {
            return JsonUtility.ToJson(data);
        }

        public static void FromJson(this RequestCommand data, string json)
        {
            data = JsonUtility.FromJson<RequestCommand>(json);
        }

        public static string ToJson(this List<RequestCommand> data)
        {
            return JsonUtility.ToJson(data);
        }
    }
}