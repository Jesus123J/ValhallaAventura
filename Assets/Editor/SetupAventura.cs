using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;

/// <summary>
/// Crea la escena de la aventura automaticamente al abrir el proyecto.
/// Todo el mundo 3D se construye por codigo al dar Play (GestorAventura),
/// asi que la escena solo necesita la camara, la luz y el gestor.
/// Menu: Herramientas -> Crear Escena Aventura
/// </summary>
[InitializeOnLoad]
public static class SetupAventura
{
    const string RutaEscena = "Assets/Scenes/Aventura.unity";
    const string Marcador   = "ProjectSettings/EscenaVersion.txt";
    const string Version    = "1";

    static SetupAventura()
    {
        if (NecesitaRegenerar())
        {
            EditorApplication.delayCall += () =>
            {
                if (NecesitaRegenerar())
                    Crear();
            };
        }
    }

    static bool NecesitaRegenerar()
    {
        if (!System.IO.File.Exists(RutaEscena)) return true;
        if (!System.IO.File.Exists(Marcador)) return true;
        return System.IO.File.ReadAllText(Marcador).Trim() != Version;
    }

    [MenuItem("Herramientas/Crear Escena Aventura")]
    public static void Crear()
    {
        var escena = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);

        new GameObject("GestorAventura").AddComponent<GestorAventura>();

        System.IO.Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(escena, RutaEscena);
        EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(RutaEscena, true) };
        AssetDatabase.SaveAssets();
        System.IO.File.WriteAllText(Marcador, Version);

        Debug.Log("Escena Aventura lista. Dale PLAY: el mundo se construye solo.");
    }
}
