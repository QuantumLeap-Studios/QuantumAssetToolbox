using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.IO;
using UnityEngine.Networking;

public class ToolboxWindow : EditorWindow
{
    private List<AssetInfo> assets = new List<AssetInfo>();
    private List<AssetInfo> filteredAssets = new List<AssetInfo>();
    private string uploadUrl = "https://quantumleapstudios.org/upload.php";
    private string fetchUrl = "https://quantumleapstudios.org/assets.php";
    private Vector2 scrollPos;

    private string searchQuery = string.Empty; // Search query variable
    private GUIStyle headerStyle;
    private GUIStyle descriptionStyle;
    private Dictionary<string, Texture2D> assetIcons = new Dictionary<string, Texture2D>(); // To store asset icons

    [MenuItem("Tools/Quantum Toolbox")]
    public static void ShowWindow()
    {
        GetWindow<ToolboxWindow>("Quantum Toolbox");
    }

    public void OnEnable()
    {
        headerStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 19,
            alignment = TextAnchor.MiddleCenter,
            normal = { textColor = Color.white },
            fontStyle = FontStyle.Bold
        };

        descriptionStyle = new GUIStyle(EditorStyles.label)
        {
            fontSize = 12,
            wordWrap = true,
            normal = { textColor = Color.gray }
        };
    }

    private void OnGUI()
    {
        GUILayout.Space(10);
        GUILayout.Label("Quantum Asset Toolbox", headerStyle);
        GUILayout.Label("Upload and download assets easily within the editor.", descriptionStyle);
        GUILayout.Space(20);

        DrawUploadSection();
        GUILayout.Space(20);

        // Search bar
        GUILayout.Label("Search Assets", EditorStyles.boldLabel);
        searchQuery = GUILayout.TextField(searchQuery, GUILayout.Height(25));

        // Filter assets based on the search query
        filteredAssets.Clear();
        foreach (var asset in assets)
        {
            if (asset.name.ToLower().Contains(searchQuery.ToLower()))
            {
                filteredAssets.Add(asset);
            }
        }

        DrawAssetListSection();
    }

    private void DrawUploadSection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Upload Asset", EditorStyles.boldLabel);

        GUILayout.Label("Select a file from your computer to upload it to the server.", descriptionStyle);
        GUILayout.Space(5);

        if (GUILayout.Button("Upload Asset", GUILayout.Height(30)))
        {
            string path = EditorUtility.OpenFilePanel("Select Asset to Upload", "", "");
            if (!string.IsNullOrEmpty(path))
            {
                UploadAsset(path);
            }
        }
        EditorGUILayout.EndVertical();
    }

    private void DrawAssetListSection()
    {
        EditorGUILayout.BeginVertical("box");
        GUILayout.Label("Available Assets", EditorStyles.boldLabel);

        GUILayout.Label("Browse and download assets directly into your project.", descriptionStyle);
        GUILayout.Space(5);

        if (GUILayout.Button("Refresh Asset List", GUILayout.Height(25)))
        {
            FetchAssets();
        }

        GUILayout.Space(10);

        // Improved scrollable area with flexible height
        scrollPos = EditorGUILayout.BeginScrollView(scrollPos, GUILayout.ExpandHeight(true));
        foreach (var asset in filteredAssets)
        {
            EditorGUILayout.BeginHorizontal("box");

            // Show the asset's icon if available
            if (assetIcons.ContainsKey(asset.name))
            {
                GUILayout.Label(assetIcons[asset.name], GUILayout.Width(30), GUILayout.Height(30));
            }

            EditorGUILayout.LabelField(asset.name);

            GUILayout.FlexibleSpace();

            if (GUILayout.Button("Download", GUILayout.Width(100)))
            {
                DownloadAsset(asset);
            }
            EditorGUILayout.EndHorizontal();
        }
        EditorGUILayout.EndScrollView();

        EditorGUILayout.EndVertical();
    }

    private void UploadAsset(string filePath)
    {
        byte[] fileData = File.ReadAllBytes(filePath);
        string fileName = Path.GetFileName(filePath);

        WWWForm form = new WWWForm();
        form.AddBinaryData("file", fileData, fileName);

        UnityWebRequest www = UnityWebRequest.Post(uploadUrl, form);
        var operation = www.SendWebRequest();

        operation.completed += (asyncOp) =>
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                Debug.Log("Upload successful");
                FetchAssets();
            }
            else
            {
                Debug.LogError("Upload failed: " + www.error);
            }
        };
    }

    private void FetchAssets()
    {
        UnityWebRequest www = UnityWebRequest.Get(fetchUrl);
        var operation = www.SendWebRequest();

        operation.completed += (asyncOp) =>
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    AssetList assetList = JsonUtility.FromJson<AssetList>(www.downloadHandler.text);
                    assets = new List<AssetInfo>(assetList.assets);
                    filteredAssets.Clear(); // Reset filtered assets when new assets are fetched
                    filteredAssets.AddRange(assets); // Add all assets to the filtered list initially
                }
                catch (System.Exception e)
                {
                    Debug.LogError("JSON Parsing Error: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("Failed to fetch assets: " + www.error);
            }
        };
    }

    private Texture2D LoadTexture(string path)
    {
        byte[] fileData = File.ReadAllBytes(path);
        Texture2D texture = new Texture2D(2, 2);
        texture.LoadImage(fileData); // Load image data
        return texture;
    }

    private void DownloadAsset(AssetInfo asset)
    {
        UnityWebRequest www = UnityWebRequest.Get(asset.url);
        var operation = www.SendWebRequest();

        operation.completed += (asyncOp) =>
        {
            if (www.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    string savePath = Path.Combine(Application.dataPath + "/QuantumAssetToolbox", "DownloadedAssets", asset.name);
                    Directory.CreateDirectory(Path.GetDirectoryName(savePath));

                    if (asset.name.EndsWith(".unitypackage") || asset.name.EndsWith(".zip"))
                    {
                        string tempPath = Path.Combine(Path.GetTempPath(), asset.name);
                        File.WriteAllBytes(tempPath, www.downloadHandler.data);

                        if (asset.name.EndsWith(".unitypackage"))
                        {
                            AssetDatabase.ImportPackage(tempPath, true);
                            Debug.Log($"Unity package imported: {asset.name}");
                        }
                        else if (asset.name.EndsWith(".zip"))
                        {
                            string extractPath = Path.Combine(Application.dataPath + "/QuantumAssetToolbox", Path.GetFileNameWithoutExtension(asset.name));
                            System.IO.Compression.ZipFile.ExtractToDirectory(tempPath, extractPath);
                            Debug.Log($"Zip file extracted to: {extractPath}");
                        }

                        File.Delete(tempPath);
                    }
                    else
                    {
                        File.WriteAllBytes(savePath, www.downloadHandler.data);
                        Debug.Log($"Asset downloaded and saved to: {savePath}");
                    }

                    AssetDatabase.Refresh();
                }
                catch (System.Exception e)
                {
                    Debug.LogError("Error processing asset: " + e.Message);
                }
            }
            else
            {
                Debug.LogError("Download failed: " + www.error);
            }
        };
    }

    [System.Serializable]
    public class AssetInfo
    {
        public string name;
        public string url;
    }

    [System.Serializable]
    public class AssetList
    {
        public AssetInfo[] assets;
    }
}
