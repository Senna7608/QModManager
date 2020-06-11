﻿namespace QModManager.Utility
{
    using System.Reflection;
    using System.Reflection.Emit;
    using System.Collections;
    using System.Collections.Generic;

    using Harmony;
    using UnityEngine;
    using UnityEngine.SceneManagement;

    /// <summary>
    /// Allows to add critical messages to the main menu.
    /// Messages will stay in the main menu and on loading screen.
    /// </summary>
    internal static class MainMenuMessages
    {
        internal const int defaultSize = 25;
        internal const string defaultColor = "red";

        private static readonly Vector2 newOffset = new Vector2(20f, 20f);
        private static Vector2 prevOffset;

        private static Dictionary<string, float> messageQueue;
        private static List<ErrorMessage._Message> messages;

        private static bool inited => messageQueue != null;

        /// <summary>Adds an error message to the main menu.</summary>
        /// <param name="msg">The message to add.</param>
        /// <param name="callerID">The ID of the caller (or null for "QModManager").</param>
        /// <param name="size">The size of the text.</param>
        /// <param name="color">The color of the text.</param>
        /// <param name="autoformat">Whether or not to apply formatting tags to the message, or show it as it is.</param>
        /// <param name="timeEnd">The number of seconds the message will stay on screen.</param>
        internal static void Add(string msg, string callerID = null, int size = defaultSize, string color = defaultColor, bool autoformat = true, float timeEnd = 1e6f)
        {
            if (Patches.hInstance == null) // just in case
            {
                Logger.Error($"Tried to add main menu message before Harmony was initialized. (Message: \"{msg}\")");
                return;
            }

            if (SceneManager.GetSceneByName("Main").isLoaded) // it works just like regular ErrorMessage outside of main menu
            {
                ErrorMessage.AddDebug(msg);
                return;
            }

            Init();
            Logger.Debug($"Created message: \"{msg}\"");

            if (autoformat)
                msg = $"<size={size}><color={color}><b>[{callerID ?? "QModManager"}]:</b> {msg}</color></size>";

            if (ErrorMessage.main != null)
                AddInternal(msg, timeEnd);
            else
                messageQueue.Add(msg, timeEnd);
        }

        private static void Init()
        {
            if (inited)
                return;

            messageQueue = new Dictionary<string, float>();
            messages = new List<ErrorMessage._Message>();
            Patches.Patch();

            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        private static void AddInternal(string msg, float timeEnd = 1e6f)
        {
            ErrorMessage.AddDebug(msg);

            var message = ErrorMessage.main.GetExistingMessage(msg);
            messages.Add(message);
            message.timeEnd += timeEnd;
            message.entry.rectTransform.sizeDelta = new Vector2(1920f - ErrorMessage.main.offset.x * 2f, 0f);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode _)
        {
            if (scene.name != "Main")
                return;

            SceneManager.sceneLoaded -= OnSceneLoaded;
            ErrorMessage.main.StartCoroutine(_waitForLoad());

            static IEnumerator _waitForLoad()
            {
                yield return new WaitForSeconds(1f);

                while (SaveLoadManager.main.isLoading)
                    yield return null;

                messages.ForEach(msg => msg.timeEnd = Time.time + 1f);
                yield return new WaitForSeconds(1.1f); // wait for messages to dissapear

                Vector2 originalSize = ErrorMessage.main.prefabMessage.rectTransform.sizeDelta;
                messages.ForEach(msg => msg.entry.rectTransform.sizeDelta = originalSize);
                messages.Clear();

                Patches.Unpatch();

                yield return new WaitForSeconds(0.5f);
                ErrorMessage.main.offset = prevOffset;
            }
        }

        private static class Patches
        {
            public static HarmonyInstance hInstance { get; private set; }

            public static void Patch()
            {
                Logger.Debug("Patching ErrorMessage");

                // patching it only if we need to (transpilers take time)
                hInstance.Patch(AccessTools.Method(typeof(ErrorMessage), nameof(ErrorMessage.OnUpdate)),
                    transpiler: new HarmonyMethod(AccessTools.Method(typeof(Patches), nameof(UpdateMessages))));
            }

            public static void Unpatch()
            {
                Logger.Debug("Unpatching ErrorMessage");

                hInstance.Unpatch(AccessTools.Method(typeof(ErrorMessage), nameof(ErrorMessage.Awake)),
                    AccessTools.Method(typeof(AddMessages), nameof(AddMessages.Postfix)));

                hInstance.Unpatch(AccessTools.Method(typeof(ErrorMessage), nameof(ErrorMessage.OnUpdate)),
                    AccessTools.Method(typeof(Patches), nameof(UpdateMessages)));
            }

            [HarmonyPatch(typeof(ErrorMessage), nameof(ErrorMessage.Awake))]
            private static class AddMessages
            {
                // workaround to get harmony instance
                private static MethodBase TargetMethod(HarmonyInstance instance)
                {
                    hInstance = instance;
                    return null; // using target method from attribute
                }

                public static void Postfix()
                {
                    prevOffset = ErrorMessage.main.offset;

                    if (!inited)
                        return;

                    ErrorMessage.main.offset = newOffset;

                    messageQueue.ForEach(msg => AddInternal(msg.Key, msg.Value));
                    messageQueue.Clear();
                }
            }

            private static float _getVal(float val, ErrorMessage._Message message) => messages.Contains(message)? 1f: val;

            // we changing result for 'float value = Mathf.Clamp01(MathExtensions.EvaluateLine(...' to 1.0f
            // so text don't stay in the center of the screen (because of changed 'timeEnd')
            private static IEnumerable<CodeInstruction> UpdateMessages(IEnumerable<CodeInstruction> cins)
            {
                var list = new List<CodeInstruction>(cins);
                int index = list.FindIndex(cin => cin.opcode == OpCodes.Stloc_S && (cin.operand as LocalBuilder)?.LocalIndex == 11);

                list.InsertRange(index, new List<CodeInstruction>
                {
                    new CodeInstruction(OpCodes.Ldloc_S, 6),
                    new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(Patches), nameof(_getVal)))
                });

                return list;
            }
        }
    }
}
