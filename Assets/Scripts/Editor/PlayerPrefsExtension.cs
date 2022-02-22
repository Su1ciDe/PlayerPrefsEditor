using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace VP.Nest.System.Editor.PlayerPrefsEditor
{
	public static class PlayerPrefsExtension
	{
		private static string plistFilename;
		private static string playerPrefsPath;
		public static DateTime previousSaveTime;

		[Serializable]
		public struct PlayerPrefPair
		{
			public enum PrefType
			{
				Int,
				Float,
				String
			}

			public string Key { get; set; }
			public object Value { get; set; }
			public PrefType Type { get; set; }
		}

		public static PlayerPrefPair[] GetAll()
		{
			return GetAll(PlayerSettings.companyName, PlayerSettings.productName);
		}

		public static PlayerPrefPair[] GetAll(string companyName, string productName)
		{
			if (Application.platform == RuntimePlatform.OSXEditor)
			{
				// From Unity docs: On Mac OS X PlayerPrefs are stored in ~/Library/Preferences folder, in a file named unity.[company name].[product name].plist, where company and product names are the names set up in Project Settings. The same .plist file is used for both Projects run in the Editor and standalone players.

				// Construct the plist filename from the project's settings
				plistFilename = $"unity.{companyName}.{productName}.plist";
				// Now construct the fully qualified path
				playerPrefsPath = Path.Combine(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "Library/Preferences"), plistFilename);

				// Parse the player prefs file if it exists
				if (File.Exists(playerPrefsPath))
				{
					// Parse the plist then cast it to a Dictionary
					object plist = Plist.ReadPlist(playerPrefsPath);
					Dictionary<string, object> parsed = plist as Dictionary<string, object>;

					// Convert the dictionary data into an array of PlayerPrefPairs
					PlayerPrefPair[] tempPlayerPrefs = new PlayerPrefPair[parsed.Count];

					int i = 0;
					foreach (KeyValuePair<string, object> pair in parsed)
					{
						if (pair.Value is int _)
							tempPlayerPrefs[i] = new PlayerPrefPair { Key = pair.Key, Value = pair.Value, Type = PlayerPrefPair.PrefType.Int };
						else if (pair.Value is double)
						{
							double _double = double.Parse(pair.Value.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture.NumberFormat);
							tempPlayerPrefs[i] = new PlayerPrefPair { Key = pair.Key, Value = (float)_double };
						}
						else if (float.TryParse(pair.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float value))
							tempPlayerPrefs[i] = new PlayerPrefPair { Key = pair.Key, Value = value, Type = PlayerPrefPair.PrefType.Float };
						else if (pair.Value is string _)
							tempPlayerPrefs[i] = new PlayerPrefPair { Key = pair.Key, Value = pair.Value, Type = PlayerPrefPair.PrefType.String };
						else
							tempPlayerPrefs[i] = tempPlayerPrefs[i];

						i++;
					}

					// Return the results
					return tempPlayerPrefs;
				}
				else
				{
					// No existing player prefs saved (which is valid), so just return an empty array
					return new PlayerPrefPair[0];
				}
			}
			else if (Application.platform == RuntimePlatform.WindowsEditor)
			{
				// From Unity docs: On Windows, PlayerPrefs are stored in the registry under HKCU\Software\[company name]\[product name] key, where company and product names are the names set up in Project Settings.
#if UNITY_5_5_OR_NEWER
				// From Unity 5.5 editor player prefs moved to a specific location
				Microsoft.Win32.RegistryKey registryKey =
					Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\Unity\\UnityEditor\\" + companyName + "\\" + productName);
#else
                Microsoft.Win32.RegistryKey registryKey = Microsoft.Win32.Registry.CurrentUser.OpenSubKey("Software\\" + companyName + "\\" + productName);
#endif

				// Parse the registry if the specified registryKey exists
				if (registryKey != null)
				{
					// Get an array of what keys (registry value names) are stored
					string[] valueNames = registryKey.GetValueNames();

					// Create the array of the right size to take the saved player prefs
					PlayerPrefPair[] tempPlayerPrefs = new PlayerPrefPair[valueNames.Length];

					// Parse and convert the registry saved player prefs into our array
					int i = 0;
					foreach (string valueName in valueNames)
					{
						string key = valueName;

						// Remove the _h193410979 style suffix used on player pref keys in Windows registry
						int index = key.LastIndexOf("_");
						key = key.Remove(index, key.Length - index);

						// Get the value from the registry
						object ambiguousValue = registryKey.GetValue(valueName);

						// Unfortunately floats will come back as an int (at least on 64 bit) because the float is stored as
						// 64 bit but marked as 32 bit - which confuses the GetValue() method greatly! 
						if (ambiguousValue is int)
						{
							// If the player pref is not actually an int then it must be a float, this will evaluate to true
							// (impossible for it to be 0 and -1 at the same time)
							if (PlayerPrefs.GetInt(key, -1) == -1 && PlayerPrefs.GetInt(key, 0) == 0)
							{
								// Fetch the float value from PlayerPrefs in memory
								ambiguousValue = PlayerPrefs.GetFloat(key);
							}
						}
						else if (ambiguousValue.GetType() == typeof(byte[]))
						{
							// On Unity 5 a string may be stored as binary, so convert it back to a string
							ambiguousValue = global::System.Text.Encoding.Default.GetString((byte[])ambiguousValue);
						}

						// Assign the key and value into the respective record in our output array
						tempPlayerPrefs[i] = new PlayerPrefPair { Key = key, Value = ambiguousValue };

						i++;
					}

					int x = 0;
					foreach (var pair in tempPlayerPrefs)
					{
						if (pair.Value is int _)
							tempPlayerPrefs[x] = new PlayerPrefPair { Key = pair.Key, Value = pair.Value, Type = PlayerPrefPair.PrefType.Int };
						else if (pair.Value is double)
						{
							double _double = double.Parse(pair.Value.ToString(), NumberStyles.Float, CultureInfo.CurrentCulture.NumberFormat);
							tempPlayerPrefs[x] = new PlayerPrefPair { Key = pair.Key, Value = (float)_double };
						}
						else if (float.TryParse(pair.Value.ToString(), NumberStyles.Float, CultureInfo.InvariantCulture.NumberFormat, out float value))
							tempPlayerPrefs[x] = new PlayerPrefPair { Key = pair.Key, Value = value, Type = PlayerPrefPair.PrefType.Float };
						else if (pair.Value is string _)
							tempPlayerPrefs[x] = new PlayerPrefPair { Key = pair.Key, Value = pair.Value, Type = PlayerPrefPair.PrefType.String };
						else
							tempPlayerPrefs[x] = tempPlayerPrefs[i];

						x++;
					}

					// Return the results
					return tempPlayerPrefs;
				}
				else
				{
					// No existing player prefs saved (which is valid), so just return an empty array
					return new PlayerPrefPair[0];
				}
			}
			else
			{
				throw new NotSupportedException("PlayerPrefsEditor doesn't support this Unity Editor platform");
			}
		}

		public static bool CheckIfFileSaved()
		{
			if (Application.platform == RuntimePlatform.OSXEditor)
				return previousSaveTime < File.GetLastWriteTime(playerPrefsPath);
			else
				return true;
		}
	}
}