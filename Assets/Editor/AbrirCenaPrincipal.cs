// Abre a cena do VirtuaLab automaticamente quando o projeto é aberto no Unity
// (uma vez por sessão do Editor), para quem baixar o repositório encontrar o jogo direto.
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace VirtuaLab.EditorTools
{
    [InitializeOnLoad]
    static class AbrirCenaPrincipal
    {
        const string Chave = "VirtuaLab.CenaPrincipalAberta";

        static AbrirCenaPrincipal()
        {
            if (SessionState.GetBool(Chave, false) || UnityEditorInternal.InternalEditorUtility.inBatchMode) return;
            // Na primeira abertura o Unity ainda importa e recompila (com recargas de domínio que apagam
            // callbacks pendentes). Por isso tenta a cada atualização do Editor e só marca como feito
            // quando a importação terminou de fato.
            EditorApplication.update -= Tentar;
            EditorApplication.update += Tentar;
        }

        static void Tentar()
        {
            if (EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            EditorApplication.update -= Tentar;
            SessionState.SetBool(Chave, true);

            if (EditorApplication.isPlayingOrWillChangePlaymode) return;
            var atual = SceneManager.GetActiveScene();
            if (atual.path == VirtuaLabSceneBuilder.CaminhoCena) return;
            if (atual.isDirty) return; // não descarta alterações não salvas
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(VirtuaLabSceneBuilder.CaminhoCena) == null) return;
            EditorSceneManager.OpenScene(VirtuaLabSceneBuilder.CaminhoCena, OpenSceneMode.Single);
        }
    }
}
