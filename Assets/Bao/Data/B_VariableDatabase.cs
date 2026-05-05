using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Newtonsoft.Json;
using UnityEngine;

public class B_VariableDatabase : MonoBehaviour
{
    private static B_VariableDatabase instance;
    private readonly TaskCompletionSource<bool> initializationTcs = new();
    private bool initialized = false;
    private string path;
    private Dictionary<string, object> data = new();

    public static B_VariableDatabase Instance => instance ??= FindOrCreate();

    private static B_VariableDatabase FindOrCreate()
    {
        var db = FindFirstObjectByType<B_VariableDatabase>();
        if (db == null)
        {
            var go = new GameObject("B_VariableDatabase");
            db = go.AddComponent<B_VariableDatabase>();
        }
        return db;
    }

    private void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        DontDestroyOnLoad(gameObject);

        path = Application.persistentDataPath + "/settings.json";
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (initialized) return;

        await LoadFromFileAsync();
        initialized = true;
        initializationTcs.SetResult(true);
    }

    private async Task LoadFromFileAsync()
    {
        if (!File.Exists(path))
        {
            data = new Dictionary<string, object>();
            return;
        }

        try
        {
            string json = await File.ReadAllTextAsync(path);
            data = JsonConvert.DeserializeObject<Dictionary<string, object>>(json)
                   ?? new Dictionary<string, object>();
        }
        catch
        {
            data = new Dictionary<string, object>();
        }

    }

    public async Task WaitUntilInitialized()
    {
        if (initialized) return;
        await initializationTcs.Task;
    }

    public T LoadOrCreate<T>(string key, T defaultValue)
    {
        if (data.TryGetValue(key, out var value))
        {
            try
            {
                return (value is Newtonsoft.Json.Linq.JToken token)
                    ? token.ToObject<T>()
                    : (T)System.Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                Save(key, defaultValue);
                return defaultValue;
            }
        }
        Save(key, defaultValue);
        return defaultValue;
    }

    public T Load<T>(string key, T defaultValue)
    {
        if (data.TryGetValue(key, out var value))
        {
            try
            {
                return (value is Newtonsoft.Json.Linq.JToken token)
                    ? token.ToObject<T>()
                    : (T)System.Convert.ChangeType(value, typeof(T));
            }
            catch
            {
                return defaultValue;
            }
        }
        return defaultValue;
    }

    public void Save<T>(string key, T value)
    {
        if (string.IsNullOrEmpty(key)) return;
        data[key] = value;
        File.WriteAllText(path, JsonConvert.SerializeObject(data, Formatting.Indented));
    }

    public bool Contains(string key) => data.ContainsKey(key);
}