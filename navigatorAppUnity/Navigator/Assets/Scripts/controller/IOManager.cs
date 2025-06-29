using System;
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
    /// Saves data as JSON using NativeFilePicker for file selection
    /// </summary>
    /// <typeparam name="T">Type of data to serialize</typeparam>
    /// <param name="data">Data to save</param>
    /// <param name="filename">Default filename (optional)</param>
    /// <returns>True if save was successful, false otherwise</returns>
    public static bool SaveAsJson<T>(T data, string filename = null, bool silentSave = false)
        {
            try
            {
                // Set default filename if not provided
                if (string.IsNullOrEmpty(filename))
                {
                    filename = $"{typeof(T).Name}_{System.DateTime.Now:yyyyMMdd_HHmmss}.json";
                }
                
                // Ensure filename has .json extension
                if (!filename.EndsWith(".json"))
                {
                    filename += ".json";
                }

                // Serialize data to JSON
                string jsonData = JsonConvert.SerializeObject(data, Formatting.Indented);

                if (silentSave)
                {
                    // Save directly without user interaction
                    string savePath = GetSavePath();
                    
                    // Ensure directory exists
                    if (!Directory.Exists(savePath))
                    {
                        Directory.CreateDirectory(savePath);
                    }
                    
                    string fullPath = Path.Combine(savePath, filename);
                    File.WriteAllText(fullPath, jsonData);
                    
                    Debug.Log($"JSON file saved successfully to: {fullPath}");
                    return true;
                }
                else
                {
                    // Use NativeFilePicker for user file selection
                    string tempPath = Path.Combine(Application.temporaryCachePath, filename);
                    File.WriteAllText(tempPath, jsonData);
                    
                    // Use NativeFilePicker ExportFile to let user choose save location
                    NativeFilePicker.ExportFile(tempPath, (success) => 
                    {
                        if (success)
                        {
                            Debug.Log($"JSON file exported successfully");
                        }
                        else
                        {
                            Debug.LogWarning("File export was cancelled or failed");
                        }
                        
                        // Clean up temporary file
                        try
                        {
                            if (File.Exists(tempPath))
                                File.Delete(tempPath);
                        }
                        catch (System.Exception ex)
                        {
                            Debug.LogWarning($"Could not delete temporary file: {ex.Message}");
                        }
                    });

                    return true; // Export initiated successfully
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"Error in SaveAsJson: {ex.Message}");
                return false;
            }
        }

    /// <summary>
    /// Gets the Downloads folder path for different platforms
    /// </summary>
    private static string GetDownloadsPath()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // On Android, use the Downloads directory
        return "/storage/emulated/0/Download";
#elif UNITY_IOS && !UNITY_EDITOR
        // On iOS, use Documents directory (Downloads not accessible)
        return Application.persistentDataPath;
#else
        // On desktop platforms, use user's Downloads folder
        string userProfile = System.Environment.GetFolderPath(System.Environment.SpecialFolder.UserProfile);
        return Path.Combine(userProfile, "Downloads");
#endif
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
