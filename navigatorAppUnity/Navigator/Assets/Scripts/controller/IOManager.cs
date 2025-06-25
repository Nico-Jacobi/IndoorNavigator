using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

namespace controller
{
    public static class IOManager
    {
        /// <summary>
        /// Save any object as JSON file with optional filename and folder.
        /// Requests permission on Android if needed.
        /// </summary>
        public static bool SaveAsJson<T>(T data, string filename = null, string customFolder = null, bool useDownloadsFolder = true)
        {
#if UNITY_ANDROID
            if (useDownloadsFolder && !Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
            {
                Debug.Log("Requesting WRITE_EXTERNAL_STORAGE permission");
                Permission.RequestUserPermission(Permission.ExternalStorageWrite);
                return false; // Permission not granted yet
            }
#endif

            try
            {
                string finalFilename = filename ?? $"data_{DateTime.Now:yyyyMMdd_HHmmssfff}";
                if (!finalFilename.EndsWith(".json"))
                    finalFilename += ".json";

                string json = JsonConvert.SerializeObject(data, Formatting.Indented);
                string folderPath = GetSavePath(useDownloadsFolder, customFolder);

                if (!Directory.Exists(folderPath))
                    Directory.CreateDirectory(folderPath);

                string fullPath = Path.Combine(folderPath, finalFilename);
                File.WriteAllText(fullPath, json);

                Debug.Log($"Data saved successfully to: {fullPath}");
                return true;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save data: {e}");
                return false;
            }
        }

        /// <summary>
        /// Load JSON data from file.
        /// </summary>
        public static T LoadFromJson<T>(string filename, string customFolder = null, bool useDownloadsFolder = true)
        {
            try
            {
                if (!filename.EndsWith(".json"))
                    filename += ".json";

                string folderPath = GetSavePath(useDownloadsFolder, customFolder);
                string fullPath = Path.Combine(folderPath, filename);

                if (!File.Exists(fullPath))
                {
                    Debug.LogWarning($"File not found: {fullPath}");
                    return default;
                }

                string json = File.ReadAllText(fullPath);
                var data = JsonConvert.DeserializeObject<T>(json);

                Debug.Log($"Data loaded successfully from: {fullPath}");
                return data;
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to load data: {e}");
                return default;
            }
        }

        /// <summary>
        /// Returns save path. Downloads folder on Android if requested, else persistentDataPath.
        /// </summary>
        public static string GetSavePath(bool useDownloadsFolder = true, string customFolder = null)
        {
            string basePath;

#if UNITY_ANDROID && !UNITY_EDITOR
            if (useDownloadsFolder)
            {
                try
                {
                    using var envClass = new AndroidJavaClass("android.os.Environment");
                    var downloadsDir = envClass.CallStatic<AndroidJavaObject>(
                        "getExternalStoragePublicDirectory", envClass.GetStatic<string>("DIRECTORY_DOWNLOADS"));
                    basePath = downloadsDir.Call<string>("getAbsolutePath");
                }
                catch (Exception e)
                {
                    Debug.LogWarning("Failed to get Android Downloads folder, falling back to persistentDataPath. " + e);
                    basePath = Application.persistentDataPath;
                }
            }
            else
            {
                basePath = Application.persistentDataPath;
            }
#else
            basePath = Application.persistentDataPath;
#endif

            return string.IsNullOrEmpty(customFolder) ? basePath : Path.Combine(basePath, customFolder);
        }

        /// <summary>
        /// Check if storage permission granted (Android).
        /// </summary>
        public static bool HasStoragePermission()
        {
#if UNITY_ANDROID
            return Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite);
#else
            return true;
#endif
        }

        /// <summary>
        /// Request storage permission (Android).
        /// </summary>
        public static void RequestStoragePermission()
        {
#if UNITY_ANDROID
            if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageWrite))
                Permission.RequestUserPermission(Permission.ExternalStorageWrite);
#endif
        }
    }
}
