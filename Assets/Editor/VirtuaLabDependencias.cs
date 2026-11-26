// Lista os assets realmente usados pela cena do VirtuaLab (dependências recursivas) e grava em arquivo.
// Serve para decidir com segurança o que pode sair do projeto.
// CLI: Unity.exe -batchmode -projectPath <proj> -executeMethod VirtuaLab.EditorTools.VirtuaLabDependencias.Listar -inspecaoSaida <arquivo.txt> -quit
using System;
using System.IO;
using System.Linq;
using UnityEditor;

namespace VirtuaLab.EditorTools
{
    public static class VirtuaLabDependencias
    {
        public static void Listar()
        {
            string saida = Path.Combine(Path.GetTempPath(), "virtualab_dependencias.txt");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-inspecaoSaida") saida = args[i + 1];
            var raizes = new[] { VirtuaLabSceneBuilder.CaminhoCena }
                .Concat(AssetDatabase.FindAssets("t:Script", new[] { "Assets/Scripts", "Assets/Editor", "Assets/Tests" }).Select(AssetDatabase.GUIDToAssetPath))
                .ToArray();
            var deps = AssetDatabase.GetDependencies(raizes, true).Where(p => p.StartsWith("Assets/")).OrderBy(p => p).ToArray();
            File.WriteAllLines(saida, deps);
            UnityEngine.Debug.Log($"[Dependencias] {deps.Length} assets em {saida}");
        }
    }
}
