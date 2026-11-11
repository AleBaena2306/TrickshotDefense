using System.IO;
using TrickshotDefense;
using UnityEditor;
using UnityEngine;

// TRICKSHOT > Setup: crea el asset de equilibrado para ajustar los números desde el Inspector
// (Assets/Trickshot/Resources/TrickshotBalance.asset). Si no existe, el juego usa los valores por defecto del código.
public static class TrickshotSetup
{
    private const string BalancePath = "Assets/Trickshot/Resources/TrickshotBalance.asset";

    [MenuItem("TRICKSHOT/Setup/Crear el asset de equilibrado", priority = 200)]
    public static void CreateBalance()
    {
        if (File.Exists(BalancePath))
        {
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TrickshotBalance>(BalancePath);
            Debug.Log("[Trickshot] El asset de equilibrado ya existe: " + BalancePath);
            return;
        }
        var bal = ScriptableObject.CreateInstance<TrickshotBalance>();
        AssetDatabase.CreateAsset(bal, BalancePath);
        AssetDatabase.SaveAssets();
        Selection.activeObject = bal;
        Debug.Log("[Trickshot] Asset de equilibrado creado: " + BalancePath);
    }

    [MenuItem("TRICKSHOT/Setup/Borrar la partida guardada", priority = 210)]
    public static void ClearSave()
    {
        PlayerPrefs.DeleteKey(SaveData.Key);
        PlayerPrefs.Save();
        Debug.Log("[Trickshot] Partida guardada borrada.");
    }
}
