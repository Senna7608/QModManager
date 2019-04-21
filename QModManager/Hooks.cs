﻿using Harmony;
using UnityEngine;
using UnityEngine.SceneManagement;
using Logger = QModManager.Utility.Logger;

namespace QModManager
{
    public static class Hooks
    {
        public static Delegates.Start Start;
        public static Delegates.FixedUpdate FixedUpdate;
        public static Delegates.LateStart LateStart;
        public static Delegates.Update Update;
        public static Delegates.LateUpdate LateUpdate;
        public static Delegates.OnApplicationQuit OnApplicationQuit;

        public static Delegates.SceneLoaded SceneLoaded;

        public static Delegates.OnLoadEnd OnLoadEnd;

        public static bool LateStartInvoked { get; internal set; } = false;

        internal static void Load()
        {
            SceneManager.sceneLoaded += (scene, loadSceneMode) => SceneLoaded?.Invoke(scene, loadSceneMode);
        }

        [HarmonyPatch(typeof(DevConsole), "Start")]
        internal static class AddComponentPatch
        {
            internal static bool hooksLoaded = false;

            [HarmonyPostfix]
            internal static void Postfix(DevConsole __instance)
            {
                if (hooksLoaded) return;
                hooksLoaded = true;
                
                __instance.gameObject.AddComponent<QMMHooks>();

                Logger.Debug("Hooks loaded");

                Start?.Invoke();
            }
        }

        internal class QMMHooks : MonoBehaviour
        {
            internal void FixedUpdate()
            {
                if (!LateStartInvoked)
                {
                    LateStart?.Invoke();
                    LateStartInvoked = true;
                }
                Hooks.FixedUpdate?.Invoke();
            }
            internal void Update() => Hooks.Update?.Invoke();
            internal void LateUpdate() => Hooks.LateUpdate?.Invoke();
            internal void OnApplicationQuit() => Hooks.OnApplicationQuit?.Invoke();
        }

        public class Delegates
        {
            public delegate void Start();
            public delegate void FixedUpdate();
            public delegate void LateStart();
            public delegate void Update();
            public delegate void LateUpdate();
            public delegate void OnApplicationQuit();

            public delegate void SceneLoaded(Scene scene, LoadSceneMode loadSceneMode);

            public delegate void OnLoadEnd();
        }
    }
}
