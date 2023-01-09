using System;
using System.Linq;
using System.Collections;
using System.Globalization;
using System.Collections.Generic;
using Unity.EditorCoroutines.Editor;
using UnityEditor;
using UnityEngine;

// CREDITS: https://forum.unity.com/threads/editor-utility-player-prefs-editor-edit-player-prefs-inside-the-unity-editor.370292/
namespace PlayerPrefsEditor
{
    public class PlayerPrefsEditor : EditorWindow
    {
        private static bool gotPrefs;

        private bool isSaving;

        private string message;
        private MessageType messageType = MessageType.None;

        private static List<PlayerPrefsExtension.PlayerPrefPair> keyValues;
        private static string[] keys;
        private static string[] values;

        private int addedPlayerPrefs;
        private string[] addedKeys = new string[0];
        private string[] addedValues = new string[0];

        private Vector2 scrollPos;

        // Buttons names and tooltips
        private readonly GUIContent btnAddPlayerPref = new GUIContent("", "Adds a new key-value pair for you to fill and save");
        private readonly GUIContent btnSavePlayerPref = new GUIContent("Save PlayerPrefs", "Saves the edited and added PlayerPrefs if you added any");
        private readonly GUIContent btnDeletePlayerPref = new GUIContent("Delete Selected PlayerPref", "Deletes the focused PlayerPref");
        private readonly GUIContent btnDeleteAllPlayerPref = new GUIContent("Delete All PlayerPrefs", "Deletes all the PlayerPrefs");
        private readonly GUIContent btnReset = new GUIContent("Reset", "Resets the window to default values");

        // ProgressBar
        private float progressBarFill;
        private string progressBarTitle;
        private readonly float waitSecondsForSaving = 10;

        [MenuItem("Edit/Player Prefs Editor", false)]
        public static void OpenWindow()
        {
            keyValues = PlayerPrefsExtension.GetAll().ToList();
            HidePrefs();
            SetupPrefs();

            var window = (PlayerPrefsEditor)GetWindow(typeof(PlayerPrefsEditor));
            window.titleContent = new GUIContent("Player Prefs");
            window.minSize = new Vector2(300, 300);
            window.Show();
        }

        private static void SetupPrefs()
        {
            keyValues = PlayerPrefsExtension.GetAll().ToList();
            HidePrefs();
            keys = new string[keyValues.Count];
            values = new string[keyValues.Count];

            gotPrefs = false;
        }

        private void OnGUI()
        {
            float width = position.width;
            float height = position.height;
            GUILayout.BeginArea(new Rect(0, 0, width, height));
            scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUIStyle.none, GUI.skin.verticalScrollbar);

            EditorGUILayout.LabelField("Player Prefs Editor", EditorStyles.boldLabel);
            EditorGUILayout.Separator();

            ShowPlayerPrefs();

            EditorGUILayout.Space();

            ShowAddedPlayerPref();

            ShowMessage();

            EditorGUI.BeginDisabledGroup(isSaving);

            btnAddPlayerPref.image = EditorGUIUtility.IconContent("d_CollabCreate Icon").image;
            if (GUILayout.Button(btnAddPlayerPref, EditorStyles.iconButton))
            {
                addedPlayerPrefs++;
                var tempKeys = addedKeys;
                var tempValues = addedValues;
                addedKeys = new string[addedPlayerPrefs];
                addedValues = new string[addedPlayerPrefs];
                for (int i = 0; i < tempKeys.Length; i++)
                {
                    addedKeys[i] = tempKeys[i];
                    addedValues[i] = tempValues[i];
                }
            }

            if (GUILayout.Button(btnSavePlayerPref))
            {
                SavePlayerPrefs();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(btnDeletePlayerPref))
            {
                Delete();
            }

            if (GUILayout.Button(btnDeleteAllPlayerPref))
            {
                DeleteAll();
            }

            EditorGUILayout.Space();

            if (GUILayout.Button(btnReset))
            {
                Reset();
            }

            EditorGUI.EndDisabledGroup();

            EditorGUILayout.Space();

            ShowProgressBar();

            EditorGUILayout.EndScrollView();
            GUILayout.EndArea();
        }

        private void ShowPlayerPrefs()
        {
            EditorGUILayout.LabelField(values.Length > 0 ? "Current PlayerPrefs" : "There is no PlayerPref found!", EditorStyles.label);

            for (int i = 0; i < values.Length; i++)
            {
                keys[i] = keyValues[i].Key;

                GUILayout.BeginHorizontal();
                // if a value changed, show a *
                if (values[i] != null && values[i].Equals(keyValues[i].Value.ToString()))
                    EditorGUILayout.LabelField(keyValues[i].Key, EditorStyles.label, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));
                else
                    EditorGUILayout.LabelField(keyValues[i].Key + "*", EditorStyles.boldLabel, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));

                const float height = 20;
                GUI.SetNextControlName(i.ToString());
                if (keyValues[i].Type.Equals(PlayerPrefsExtension.PlayerPrefPair.PrefType.String))
                    values[i] = EditorGUILayout.TextField(values[i], GUILayout.MinHeight(height));
                else if (keyValues[i].Type.Equals(PlayerPrefsExtension.PlayerPrefPair.PrefType.Float))
                {
                    float value = -1;
                    float.TryParse(values[i], NumberStyles.Float, CultureInfo.CurrentCulture.NumberFormat, out value);
                    values[i] = EditorGUILayout.FloatField(value, GUILayout.MinHeight(height)).ToString(CultureInfo.InvariantCulture);
                }
                else if (keyValues[i].Type.Equals(PlayerPrefsExtension.PlayerPrefPair.PrefType.Int))
                {
                    int value = -1;
                    int.TryParse(values[i], out value);
                    values[i] = EditorGUILayout.IntField(value, GUILayout.MinHeight(height)).ToString();
                }

                GUILayout.EndHorizontal();

                // For showing a warning when entering a wrong value
                if (keyValues[i].Type.Equals(PlayerPrefsExtension.PlayerPrefPair.PrefType.Int))
                    if (!int.TryParse(values[i], out _))
                        EditorGUILayout.HelpBox("Invalid input \"" + values[i] + "\"", MessageType.Warning);
                if (keyValues[i].Type.Equals(PlayerPrefsExtension.PlayerPrefPair.PrefType.Float))
                    if (!float.TryParse(values[i], out _))
                        EditorGUILayout.HelpBox("Invalid input \"" + values[i] + "\"", MessageType.Warning);
            }

            if (gotPrefs)
                return;

            for (int i = 0; i < values.Length; i++)
                values[i] = keyValues[i].Value.ToString();

            gotPrefs = true;
        }

        #region Button Actions

        private void ShowAddedPlayerPref()
        {
            if (addedPlayerPrefs > 0)
            {
                EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);
                EditorGUILayout.LabelField("Added PlayerPrefs", EditorStyles.label);
                GUILayout.BeginHorizontal();
                EditorGUILayout.LabelField("Key", EditorStyles.miniLabel, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));
                EditorGUILayout.LabelField("Value", EditorStyles.miniLabel, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));
                GUILayout.EndHorizontal();
            }

            for (int i = 0; i < addedPlayerPrefs; i++)
            {
                GUILayout.BeginHorizontal();

                addedKeys[i] = EditorGUILayout.TextField(addedKeys[i], EditorStyles.miniTextField, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));
                addedValues[i] = EditorGUILayout.TextField(addedValues[i], EditorStyles.miniTextField, GUILayout.Width(EditorGUIUtility.currentViewWidth / 2));

                GUILayout.EndHorizontal();
            }
        }

        private void SavePlayerPrefs()
        {
            // For edited Prefs
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i].Equals(keyValues[i].Value.ToString()))
                    continue;

                switch (keyValues[i].Type)
                {
                    case PlayerPrefsExtension.PlayerPrefPair.PrefType.Int when int.TryParse(values[i], out int _int):
                        PlayerPrefs.SetInt(keys[i], _int);
                        break;
                    case PlayerPrefsExtension.PlayerPrefPair.PrefType.Int:
                        Message("Invalid input \"" + values[i] + "\"", MessageType.Error);
                        return;
                    case PlayerPrefsExtension.PlayerPrefPair.PrefType.Float
                        when float.TryParse(values[i], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float _float):
                        PlayerPrefs.SetFloat(keys[i], _float);
                        break;
                    case PlayerPrefsExtension.PlayerPrefPair.PrefType.Float:
                        Message("Invalid input \"" + values[i] + "\"", MessageType.Error);
                        return;
                    case PlayerPrefsExtension.PlayerPrefPair.PrefType.String:
                        PlayerPrefs.SetString(keys[i], values[i]);
                        break;
                    default:
                        Message("Invalid input \"" + values[i] + "\"", MessageType.Error);
                        return;
                }
            }

            // For newly added Prefs
            for (int i = 0; i < addedPlayerPrefs; i++)
            {
                if (addedKeys[i] == null || addedValues[i] == null)
                    continue;

                if (PlayerPrefs.HasKey(addedKeys[i]))
                {
                    Message("\"" + addedKeys[i] + "\" has already exist!", MessageType.Error);
                    return;
                }

                if (int.TryParse(addedValues[i], out int intValue))
                {
                    PlayerPrefs.SetInt(addedKeys[i], intValue);
                }
                else if (float.TryParse(addedValues[i], NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float floatValue))
                {
                    PlayerPrefs.SetFloat(addedKeys[i], floatValue);
                }
                else
                {
                    PlayerPrefs.SetString(addedKeys[i], addedValues[i]);
                }
            }

            PlayerPrefs.Save();
            isSaving = true;

            this.StartCoroutine(ProgressBarCoroutine("Saving...", "Saved!", "PlayerPrefs are successfully saved!"));
        }

        private void Delete()
        {
            if (!int.TryParse(GUI.GetNameOfFocusedControl(), out int result))
            {
                Message("You need to select a key in order to delete!", MessageType.Error);
                return;
            }

            string keyToDelete = keyValues[result].Key;
            PlayerPrefs.DeleteKey(keyToDelete);
            PlayerPrefs.Save();

            isSaving = true;
            this.StartCoroutine(ProgressBarCoroutine("Deleting...", "Deleted!", "\"" + keyToDelete + "\" key is deleted!"));
        }

        private void DeleteAll()
        {
            PlayerPrefs.DeleteAll();
            PlayerPrefs.Save();

            isSaving = true;
            this.StartCoroutine(ProgressBarCoroutine("Deleting...", "Deleted!", "All PlayerPrefs are deleted"));
        }

        #endregion

        private void ShowProgressBar()
        {
            if (!isSaving) return;

            EditorGUI.ProgressBar(new Rect(3, 5, position.width - 6, 20), progressBarFill / waitSecondsForSaving, progressBarTitle);
        }

        private IEnumerator ProgressBarCoroutine(string startProgressTitle, string doneProgressTitle, string doneMessage)
        {
            progressBarFill = 0;
            PlayerPrefsExtension.previousSaveTime = DateTime.Now;
            progressBarTitle = startProgressTitle;

            var waitForOneSecond = new EditorWaitForSeconds(0.1f);

            while (true)
            {
                progressBarFill += .1f;
                yield return waitForOneSecond;

                if (PlayerPrefsExtension.CheckIfFileSaved())
                {
                    progressBarFill = waitSecondsForSaving;
                    progressBarTitle = doneProgressTitle;
                    Repaint();
                    yield return new EditorWaitForSeconds(1);

                    isSaving = false;

                    Reset();
                    Repaint();
                    Message(doneMessage, MessageType.Info);

                    break;
                }

                Repaint();

                if (progressBarFill >= waitSecondsForSaving)
                {
                    isSaving = false;

                    Reset();
                    Repaint();
                    Message(doneMessage, MessageType.Info);

                    break;
                }
            }
        }

        private void Reset()
        {
            GUI.FocusControl(null);

            SetupPrefs();

            GUI.FocusControl(null);
            gotPrefs = false;
            addedPlayerPrefs = 0;
            addedKeys = new string[0];
            addedValues = new string[0];

            message = null;
        }

        // Hide unwanted PlayerPrefs
        private static void HidePrefs()
        {
            int removedPrefsCount = 0;
            for (int i = 0; i < keyValues.Count + removedPrefsCount; i++)
            {
                if (keyValues[i - removedPrefsCount].Key.ToLower().Contains("unity"))
                {
                    keyValues.RemoveAt(i - removedPrefsCount);
                    removedPrefsCount++;
                }
            }
        }

        #region Message

        private void ShowMessage()
        {
            if (message != null)
                EditorGUILayout.HelpBox(message, messageType);
        }

        private void Message(string messageText, MessageType _messageType)
        {
            message = messageText;
            messageType = _messageType;
        }

        #endregion
    }
}