﻿using AIChara;
using BepInEx.Logging;
using KKAPI.Chara;
using KKAPI.Studio;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;

namespace CharacterRandomizer
{
    public class CharacterRandomizerStudioGUI : MonoBehaviour
    {
        private static ManualLogSource Log => CharacterRandomizerPlugin.Instance.Log;

        private static Rect windowRect = new Rect(120, 75, 600, 800);
        private static readonly GUILayoutOption expandLayoutOption = GUILayout.ExpandWidth(true);

        private static GUIStyle labelStyle;
        private static GUIStyle selectedButtonStyle;

        private static bool guiLoaded = false;

        private Vector2 scrollPosition = Vector2.zero;

        public static CharacterRandomizerStudioGUI Instance;

        public static void Show()
        {
            CharacterRandomizerPlugin.Instance.StudioGUIToolbarToggle.Value = true;
        }

        public static void Hide()
        {
            CharacterRandomizerPlugin.Instance.StudioGUIToolbarToggle.Value = false;
        }


        private void Awake()
        {
            Instance = this;
            enabled = false;
        }

        private void Start()
        {
        }

        private void Update()
        {

        }

        private void OnEnable()
        {

        }

        private void OnDestroy()
        {
        }

        private ChaControl character;
        private CharacterRandomizerCharaController controller;

        private void OnGUI()
        {
            if (!guiLoaded)
            {
                labelStyle = new GUIStyle(UnityEngine.GUI.skin.label);
                selectedButtonStyle = new GUIStyle(UnityEngine.GUI.skin.button);

                selectedButtonStyle.fontStyle = FontStyle.Bold;
                selectedButtonStyle.normal.textColor = Color.red;

                labelStyle.alignment = TextAnchor.MiddleRight;
                labelStyle.normal.textColor = Color.white;

                windowRect.x = Mathf.Min(Screen.width - windowRect.width, Mathf.Max(0, windowRect.x));
                windowRect.y = Mathf.Min(Screen.height - windowRect.height, Mathf.Max(0, windowRect.y));

                guiLoaded = true;
            }

            IEnumerable<Studio.OCIChar> selectedCharacters = StudioAPI.GetSelectedCharacters();
            if (selectedCharacters.Count() > 0)
            {
                character = selectedCharacters.First().GetChaControl();
                controller = character.gameObject.GetComponent<CharacterRandomizerCharaController>();
            }
            else
            {
                character = null;
                controller = null;
            }

            KKAPI.Utilities.IMGUIUtils.DrawSolidBox(windowRect);

            string titleMessage = "Character Randomizer: ";
            if (character == null)
                titleMessage += "No Character Selected";
            else
                titleMessage += $"{character.chaFile.parameter.fullname}";


            var rect = GUILayout.Window(8820, windowRect, DoDraw, $"Character Randomizer: {titleMessage}");
            windowRect.x = rect.x;
            windowRect.y = rect.y;

            if (windowRect.Contains(new Vector2(Input.mousePosition.x, Screen.height - Input.mousePosition.y)))
                Input.ResetInputAxes();
        }

        private void PropagateSyncTiming()
        {
            if (!controller.UseSyncedTime)
                return;

            if (!CharacterApi.GetRegisteredBehaviour(CharacterRandomizerPlugin.GUID).Instances.Cast<CharacterRandomizerCharaController>().Any(cont => (cont != controller) && cont.Running))
                controller.ScheduleNextReplacement(true);


            foreach (CharacterRandomizerCharaController randomizer in CharacterApi.GetRegisteredBehaviour(CharacterRandomizerPlugin.GUID).Instances)
            {
                if (randomizer.UseSyncedTime)
                {
                    randomizer.Running = controller.Running;
                    randomizer.BaseDelaySeconds = controller.BaseDelaySeconds;
                    randomizer.DelayVarianceRange = controller.DelayVarianceRange;
                    randomizer.Rotation = controller.Rotation;
                }
            }
        }

        private void IncrementRotationOrder(CharacterRandomizerCharaController controller)
        {
            // increment me
            controller.RotationOrder++;

            // if anyone equal to me decrement them to swap them into my place
            foreach (CharacterRandomizerCharaController randomizer in CharacterApi.GetRegisteredBehaviour(CharacterRandomizerPlugin.GUID).Instances)
            {
                if (randomizer != controller && randomizer.ChaControl.sex == controller.ChaControl.sex && randomizer.RotationOrder == controller.RotationOrder)
                    randomizer.RotationOrder--;

                randomizer.UpdateCurrentCharacterRegistry(randomizer.LastReplacementFile);
            }
        }

        private void DecrementRotationOrder(CharacterRandomizerCharaController controller)
        {
            //decrement me
            controller.RotationOrder--;

            // if anyone equal to me increment them to swap them into my place
            foreach (CharacterRandomizerCharaController randomizer in CharacterApi.GetRegisteredBehaviour(CharacterRandomizerPlugin.GUID).Instances)
            {
                if (randomizer != controller && randomizer.ChaControl.sex == controller.ChaControl.sex && randomizer.RotationOrder == controller.RotationOrder)
                    randomizer.RotationOrder++;

                randomizer.UpdateCurrentCharacterRegistry(randomizer.LastReplacementFile);
            }
        }

        private void DoDraw(int id)
        {
            GUILayout.BeginVertical();
            {

                // Header
                GUILayout.BeginHorizontal(expandLayoutOption);
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Close Me", GUILayout.ExpandWidth(false))) Hide();
                GUILayout.EndHorizontal();

                if (controller != null)
                {
                    GUILayout.Label($"Replace the selected character either randomly or in sequence with the options below:");
                    GUILayout.Space(5);

                    GUILayout.BeginHorizontal();

                    bool newRunning = GUILayout.Toggle(controller.Running, "  Running");
                    if (newRunning != controller.Running)
                    {
                        controller.Running = newRunning;
                        PropagateSyncTiming();
                    }
                    if (controller.Running)
                    {
                        GUILayout.Space(5);
                        GUILayout.Label($"Next Replacement In: { ((int)(controller.NextTime - Time.time))} s");
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);

                    controller.PreserveOutfit = GUILayout.Toggle(controller.PreserveOutfit, "  Preserve Outfit");
                    GUILayout.Space(3);

                    if (!controller.PreserveOutfit)
                    {
                        controller.RandomOutfit = GUILayout.Toggle(controller.RandomOutfit, " Random Outfit");
                        if (!controller.RandomOutfit)
                        {
                            GUILayout.Label("Will Use Outfit of Replacement Character");
                            GUILayout.Space(3);
                        }
                        else
                        {
                            GUILayout.BeginHorizontal();
                            string coordNamePatternText = controller.OutfitFile;
                            GUILayout.Label("Coord Name Regexp: ");
                            coordNamePatternText = GUILayout.TextField(coordNamePatternText, GUILayout.ExpandWidth(true));
                            if (IsValidRegex(coordNamePatternText))
                                controller.OutfitFile = coordNamePatternText;
                            GUILayout.EndHorizontal();

                            GUILayout.Space(2);
                
                            GUILayout.Label("Coord Subdir (blank for default, | delimited, * to include children): ");
                            controller.OutfitDirectory = GUILayout.TextField(controller.OutfitDirectory, GUILayout.ExpandWidth(true));

                            GUILayout.Space(3);
                        }
                    }

                    GUILayout.BeginVertical();
                    GUILayout.Label("Suppress Accessories in Attachment Areas (use to suppress accessories that collide with scene items):");
                    GUILayout.BeginHorizontal();
                    {
                        bool neck = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.NECK);
                        bool wrist = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.WRIST);
                        bool arm = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.ARM);
                        bool ankle = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.ANKLE);
                        bool leg = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.LEG);
                        bool glasses = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.GLASSES);
                        bool breasts = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.BREASTS);
                        bool hat = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.HAT);
                        bool waist = controller.AccessorySuppressions.Contains(CharacterRandomizerCharaController.AccessorySuppressionSlots.WAIST);

                        neck = GUILayout.Toggle(neck, " Neck"); GUILayout.Space(2);
                        wrist = GUILayout.Toggle(wrist, " Wrist"); GUILayout.Space(2);
                        arm = GUILayout.Toggle(arm, " Arm"); GUILayout.Space(2);
                        ankle = GUILayout.Toggle(ankle, " Ankle"); GUILayout.Space(2);
                        leg = GUILayout.Toggle(leg, " Leg"); GUILayout.Space(2);
                        glasses = GUILayout.Toggle(glasses, " Glasses"); GUILayout.Space(2);
                        breasts = GUILayout.Toggle(breasts, " Breasts"); GUILayout.Space(2);
                        hat = GUILayout.Toggle(hat, " Hat"); GUILayout.Space(2);
                        waist = GUILayout.Toggle(waist, " Waist"); GUILayout.Space(2);

                        controller.AccessorySuppressions.Clear();
                        if (neck)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.NECK);
                        if (wrist)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.WRIST);
                        if (arm)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.ARM);
                        if (ankle)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.ANKLE);
                        if (leg)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.LEG);
                        if (glasses)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.GLASSES);
                        if (breasts)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.BREASTS);
                        if (hat)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.HAT);
                        if (waist)
                            controller.AccessorySuppressions.Add(CharacterRandomizerCharaController.AccessorySuppressionSlots.WAIST);
                    }
                    GUILayout.EndHorizontal();
                    GUILayout.EndVertical();

                    GUILayout.Space(3);

                    controller.NoDupes = GUILayout.Toggle(controller.NoDupes, "  No Duplicate Characters in Scene");
                    GUILayout.Space(3);

                    bool syncTime = controller.UseSyncedTime;
                    syncTime = GUILayout.Toggle(syncTime, "  Use Sync Timers - All Sync'd Characters Share a Timer");
                    GUILayout.Space(3);

                    if (syncTime != controller.UseSyncedTime)
                    {
                        controller.UseSyncedTime = syncTime;
                        if (syncTime)
                        {
                            // Sync Delay Settings
                            CharacterRandomizerCharaController[] randomizers = GameObject.FindObjectsOfType<CharacterRandomizerCharaController>();
                            foreach (CharacterRandomizerCharaController randomizer in randomizers)
                            {
                                if (randomizer.UseSyncedTime)
                                {
                                    controller.BaseDelaySeconds = randomizer.BaseDelaySeconds;
                                    controller.DelayVarianceRange = randomizer.DelayVarianceRange;
                                    break;
                                }
                            }
                        }
                        else
                        {
                            controller.UseSyncedTime = syncTime;
                        }
                    }


                    GUILayout.Label("Random is random, Cyclic cycles characters in the specified order.");
                    GUILayout.Label("Rotation swaps slot 1. Next cycle swaps 1 to 2 and replaces 1 again, etc. Or from last slot forward.");
                    GUILayout.Space(3);
                    controller.CharReplacementMode = (CharacterRandomizerCharaController.ReplacementMode)GUILayout.SelectionGrid((int)controller.CharReplacementMode, new string[] { "Random", "Cyclic - Last Update", "Cyclic - Last Update Desc", "Cyclic - File Name", "Cyclic - File Name Desc", "Cyclic - Chara Name", "Cyclic - Chara Name Desc", "Sync to Slot" }, 3);
                    GUILayout.Space(3);
                    CharacterRandomizerCharaController.RotationMode newRotation = (CharacterRandomizerCharaController.RotationMode)GUILayout.SelectionGrid((int)controller.Rotation, new string[] { "None", "Forward", "Reverse", "Wrap Fwd", "Wrap Rev" }, 5);
                    if (newRotation != controller.Rotation)
                    {
                        controller.Rotation = newRotation;
                        PropagateSyncTiming();
                    }
                    if (controller.CharReplacementMode == CharacterRandomizerCharaController.ReplacementMode.SYNC_TO_SLOT)
                    {
                        GUILayout.Space(3);
                        GUILayout.BeginHorizontal();
                        GUILayout.Label($"Sync To Slot (Use Same Character as Rotation Order): {controller.SyncToSlot}");
                        GUILayout.Space(3);
                        if (controller.SyncToSlot != CharacterApi.GetRegisteredBehaviour(CharacterRandomizerPlugin.GUID).Instances.Where(cont => cont.ChaControl.sex == controller.ChaControl.sex).Count())
                        {
                            if (GUILayout.Button("+"))
                            {
                                controller.SyncToSlot++;
                            };
                            GUILayout.Space(3);
                        }
                        if (controller.SyncToSlot != 1)
                        {
                            if (GUILayout.Button("-"))
                            {
                                controller.SyncToSlot--;
                            };
                        }
                        GUILayout.FlexibleSpace();
                        GUILayout.EndHorizontal();
                    }
                    GUILayout.Space(3);
                    GUILayout.BeginHorizontal();
                    GUILayout.Label($"Rotation Order: {controller.RotationOrder}");
                    GUILayout.Space(3);
                    if (controller.RotationOrder != CharacterApi.GetRegisteredBehaviour(CharacterRandomizerPlugin.GUID).Instances.Where(cont => cont.ChaControl.sex == controller.ChaControl.sex).Count())
                    {
                        if (GUILayout.Button("+"))
                        {
                            IncrementRotationOrder(controller);
                        };
                        GUILayout.Space(3);
                    }
                    if (controller.RotationOrder != 1)
                    {
                        if (GUILayout.Button("-"))
                        {
                            DecrementRotationOrder(controller);
                        };
                    }
                    GUILayout.FlexibleSpace();
                    GUILayout.EndHorizontal();

                    GUILayout.Space(3);


                    GUILayout.Label($"Replacement Time is {CharacterRandomizerPlugin.MinimumDelay.Value} seconds + Base + Random seconds");
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Base Time (secs): ");
                    GUILayout.Space(5);
                    string baseDelaySecsText = controller.BaseDelaySeconds.ToString();
                    baseDelaySecsText = GUILayout.TextField(baseDelaySecsText, 8, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Random Time (secs) From 0 to: ");
                    GUILayout.Space(5);
                    string randomDelaySecsText = controller.DelayVarianceRange.ToString();
                    randomDelaySecsText = GUILayout.TextField(randomDelaySecsText, 8, GUILayout.Width(100));
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);

                    bool successParse = int.TryParse(baseDelaySecsText, out int baseDelaySecs);
                    if (successParse && baseDelaySecs != controller.BaseDelaySeconds)
                    {
                        controller.BaseDelaySeconds = baseDelaySecs;
                        PropagateSyncTiming();
                    }
                    successParse = int.TryParse(randomDelaySecsText, out int randomDelaySecs);
                    if (successParse && randomDelaySecs != controller.DelayVarianceRange)
                    {
                        controller.DelayVarianceRange = randomDelaySecs;
                        PropagateSyncTiming();
                    }

                    GUILayout.Label("Included Subdirectories (of your Userdata/chara/(Male/Female), | delimited: ");
                    controller.Subdirectory = GUILayout.TextField(controller.Subdirectory, GUILayout.ExpandWidth(true));
                    GUILayout.Space(3);

                    controller.IncludeChildDirectories = GUILayout.Toggle(controller.IncludeChildDirectories, " Include all children of subdirectories");
                    GUILayout.Space(3);

                    GUILayout.BeginHorizontal();
                    GUILayout.Label("Character Name RegExp condition: ");
                    GUILayout.Space(5);
                    string namePatternText = controller.NamePattern;
                    namePatternText = GUILayout.TextField(namePatternText, GUILayout.ExpandWidth(true));
                    GUILayout.EndHorizontal();
                    GUILayout.Space(3);

                    if (IsValidRegex(namePatternText))
                        controller.NamePattern = namePatternText;

                    GUILayout.Space(5);
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button("Refresh Char Lists"))
                        CharacterRandomizerPlugin.Instance.RefreshLists();

                    GUILayout.Space(5);
                    if (GUILayout.Button("Replace Me"))
                        controller.ReplaceCharacter(true);

                    GUILayout.Space(5);
                    if (GUILayout.Button("Replace All Sync'd"))
                    {
                        CharacterRandomizerPlugin.ReplaceAll(true);
                    }

                    GUILayout.EndHorizontal();
                }
                else
                {
                    GUILayout.Label($"Select a character to set replacement options.");
                    GUILayout.FlexibleSpace();
                    if (GUILayout.Button("Replace All Sync'd"))
                    {
                        CharacterRandomizerPlugin.ReplaceAll(true);
                    }
                    GUILayout.Space(20);
                }
            }
            GUILayout.EndVertical();
            GUI.DragWindow();
        }

        public static bool IsValidRegex(string pattern)
        {
            if (string.IsNullOrWhiteSpace(pattern)) return false;
            try
            {
                Regex.Match("", pattern);
            }
            catch (ArgumentException)
            {
                return false;
            }

            return true;
        }

    }
}
