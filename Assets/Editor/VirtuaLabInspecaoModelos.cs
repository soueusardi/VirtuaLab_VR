// Ferramenta de apoio (somente Editor): renderiza vistas ortográficas dos modelos de hardware
// para medir posições de slots/encaixes sem abrir o Editor.
// Uso em linha de comando:
//   Unity.exe -batchmode -projectPath <proj> -executeMethod VirtuaLab.EditorTools.VirtuaLabInspecaoModelos.RenderizarTudo -inspecaoSaida <pasta> -quit
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace VirtuaLab.EditorTools
{
    public static class VirtuaLabInspecaoModelos
    {
        static readonly string[] Modelos =
        {
            "Assets/Mainboard_1_91.obj", "Assets/CPU_1_110.obj", "Assets/CPU_Kühler_93.obj",
            "Assets/RAM_1_108.obj", "Assets/M2_1_92.obj", "Assets/Grafikkarte_95.obj",
            "Assets/Netzteil_98.obj", "Assets/Gehäuse_1_107.obj", "Assets/Tisch_klein_99.obj",
        };

        public static void RenderizarTudo()
        {
            string saida = Path.Combine(Path.GetTempPath(), "virtualab_inspecao");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-inspecaoSaida") saida = args[i + 1];
            Directory.CreateDirectory(saida);

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.3f, 0.3f, 0.3f);
            var luzGo = new GameObject("Luz");
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.0f;

            var sb = new StringBuilder();
            sb.AppendLine("[");
            bool primeiro = true;
            foreach (var caminho in Modelos)
            {
                var asset = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
                if (asset == null) { Debug.LogWarning("[Inspecao] Modelo nao encontrado: " + caminho); continue; }
                var go = (GameObject)UnityEngine.Object.Instantiate(asset);
                go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
                var rends = go.GetComponentsInChildren<Renderer>();
                if (rends.Length == 0) { UnityEngine.Object.DestroyImmediate(go); continue; }
                Color[] paleta = { new Color(0.2f,0.5f,0.25f), new Color(0.85f,0.3f,0.3f), new Color(0.3f,0.45f,0.9f), new Color(0.95f,0.8f,0.2f), new Color(0.8f,0.4f,0.9f), new Color(0.3f,0.85f,0.85f) };
                var shaderLit = Shader.Find("Universal Render Pipeline/Lit");
                for (int ri = 0; ri < rends.Length; ri++)
                {
                    var m = new Material(shaderLit); m.SetColor("_BaseColor", paleta[ri % paleta.Length]);
                    var arr = new Material[rends[ri].sharedMaterials.Length];
                    for (int k = 0; k < arr.Length; k++) arr[k] = m;
                    rends[ri].sharedMaterials = arr;
                }
                Bounds b = rends[0].bounds;
                foreach (var r in rends) b.Encapsulate(r.bounds);

                string nome = Path.GetFileNameWithoutExtension(caminho).Replace("ä", "a").Replace("ü", "u").Replace(" ", "_");
                var filhos = new List<string>();
                foreach (var r in rends)
                    filhos.Add($"{{\"nome\":\"{r.name}\",\"min\":[{F(r.bounds.min.x)},{F(r.bounds.min.y)},{F(r.bounds.min.z)}],\"max\":[{F(r.bounds.max.x)},{F(r.bounds.max.y)},{F(r.bounds.max.z)}]}}");

                var vistas = new (string id, Vector3 dir, Vector3 up)[]
                {
                    ("posX", Vector3.right, Vector3.up), ("negX", Vector3.left, Vector3.up),
                    ("posY", Vector3.up, Vector3.forward), ("negY", Vector3.down, Vector3.forward),
                    ("posZ", Vector3.forward, Vector3.up), ("negZ", Vector3.back, Vector3.up),
                };
                var vistasJson = new List<string>();
                foreach (var v in vistas)
                {
                    // Câmera posicionada em +dir olhando para o centro (vê a face voltada para +dir).
                    var camGo = new GameObject("Cam");
                    var cam = camGo.AddComponent<Camera>();
                    cam.orthographic = true;
                    cam.clearFlags = CameraClearFlags.SolidColor;
                    cam.backgroundColor = new Color(0.15f, 0.15f, 0.2f);
                    cam.transform.position = b.center + v.dir * 3f;
                    cam.transform.rotation = Quaternion.LookRotation(-v.dir, v.up);
                    luzGo.transform.rotation = cam.transform.rotation * Quaternion.Euler(40f, 30f, 0f);
                    Vector3 right = cam.transform.right, up = cam.transform.up;
                    float extR = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z))));
                    float extU = Mathf.Abs(Vector3.Dot(b.extents, new Vector3(Mathf.Abs(up.x), Mathf.Abs(up.y), Mathf.Abs(up.z))));
                    float meio = Mathf.Max(extR, extU) * 1.08f;
                    cam.orthographicSize = meio;
                    cam.aspect = 1f;
                    cam.nearClipPlane = 0.01f;
                    cam.farClipPlane = 10f;
                    const int px = 1024;
                    var rt = new RenderTexture(px, px, 24, RenderTextureFormat.ARGB32);
                    cam.targetTexture = rt;
                    cam.Render();
                    RenderTexture.active = rt;
                    var tex = new Texture2D(px, px, TextureFormat.RGB24, false);
                    tex.ReadPixels(new Rect(0, 0, px, px), 0, 0);
                    tex.Apply();
                    RenderTexture.active = null;
                    cam.targetTexture = null;
                    string arq = $"{nome}_{v.id}.png";
                    File.WriteAllBytes(Path.Combine(saida, arq), tex.EncodeToPNG());
                    vistasJson.Add($"{{\"id\":\"{v.id}\",\"arquivo\":\"{arq}\",\"meio\":{F(meio)},\"right\":[{F(right.x)},{F(right.y)},{F(right.z)}],\"up\":[{F(up.x)},{F(up.y)},{F(up.z)}]}}");
                    UnityEngine.Object.DestroyImmediate(tex);
                    rt.Release();
                    UnityEngine.Object.DestroyImmediate(camGo);
                }

                if (!primeiro) sb.AppendLine(",");
                primeiro = false;
                sb.Append($"{{\"modelo\":\"{nome}\",\"centro\":[{F(b.center.x)},{F(b.center.y)},{F(b.center.z)}],\"min\":[{F(b.min.x)},{F(b.min.y)},{F(b.min.z)}],\"max\":[{F(b.max.x)},{F(b.max.y)},{F(b.max.z)}],\"filhos\":[{string.Join(",", filhos)}],\"vistas\":[{string.Join(",", vistasJson)}]}}");
                UnityEngine.Object.DestroyImmediate(go);
            }
            sb.AppendLine();
            sb.AppendLine("]");
            File.WriteAllText(Path.Combine(saida, "modelos.json"), sb.ToString(), new UTF8Encoding(false));
            Debug.Log("[Inspecao] Concluido em " + saida);
        }

        /// <summary>
        /// Abre a cena de montagem e grava imagens de conferência (não salva a cena):
        /// visão do usuário, vista superior e close do gabinete com todos os guias de encaixe visíveis.
        /// </summary>
        [MenuItem("VirtuaLab/Renderizar prévia da cena")]
        public static void RenderizarPrevia()
        {
            string saida = Path.Combine(Path.GetTempPath(), "virtualab_previa");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-inspecaoSaida") saida = args[i + 1];
            Directory.CreateDirectory(saida);

            EditorSceneManager.OpenScene(VirtuaLabSceneBuilder.CaminhoCena, OpenSceneMode.Single);
            Foto(saida, "_aquecimento.png", new Vector3(0f, 1.55f, 1.02f), new Vector3(0f, 0.92f, 1.75f), 70f, false, 64, 64); // 1º quadro sai sem luz
            Foto(saida, "previa_usuario.png", new Vector3(0f, 1.55f, 1.25f), new Vector3(0f, 0.95f, 1.80f), 80f, false, 1600, 1000);
            Foto(saida, "previa_painel.png", new Vector3(0f, 1.55f, 1.02f), new Vector3(0f, 1.35f, 2.4f), 70f, false, 1600, 1000);
            Foto(saida, "previa_superior.png", new Vector3(0f, 2.6f, 1.72f), new Vector3(0f, 0.8f, 1.721f), 50f, false, 1600, 1000);
            Foto(saida, "previa_gabinete.png", new Vector3(0f, 1.45f, 1.35f), new Vector3(0f, 0.9f, 1.62f), 45f, false, 1400, 1000);

            // Segunda passada: guias de encaixe ligados em verde, para conferir o alinhamento.
            foreach (var p in UnityEngine.Object.FindObjectsByType<PontoEncaixe>(FindObjectsInactive.Include))
                if (p.guia != null)
                {
                    p.guia.SetActive(true);
                    foreach (var r in p.guia.GetComponentsInChildren<Renderer>(true))
                    {
                        var b = new MaterialPropertyBlock();
                        b.SetColor("_BaseColor", new Color(0.1f, 1f, 0.25f, 0.6f));
                        r.SetPropertyBlock(b);
                    }
                }
            // Leva a placa-mãe ao encaixe para ver os guias das peças montadas sobre ela.
            foreach (var p in UnityEngine.Object.FindObjectsByType<PontoEncaixe>(FindObjectsInactive.Include))
                if (p.tipoAceito == TipoComponente.PlacaMae)
                    foreach (var peca in UnityEngine.Object.FindObjectsByType<PecaHardware>(FindObjectsInactive.Include))
                        if (peca.tipo == TipoComponente.PlacaMae)
                            peca.transform.SetPositionAndRotation(p.transform.position, p.transform.rotation);
            Foto(saida, "previa_guias_gabinete.png", new Vector3(0f, 1.45f, 1.35f), new Vector3(0f, 0.9f, 1.62f), 45f, false, 1400, 1000);
            Foto(saida, "previa_guias_superior.png", new Vector3(0f, 1.75f, 1.63f), new Vector3(0f, 0.8f, 1.631f), 40f, false, 1400, 1400);
            Foto(saida, "previa_guias_lateral.png", new Vector3(-0.75f, 1.05f, 1.2f), new Vector3(0f, 0.93f, 1.62f), 40f, false, 1400, 1000);
            Debug.Log("[Inspecao] Previa concluida em " + saida);
        }

        /// <summary>Renderiza o controle direito com a parte "Grip" em vermelho e o "Trigger" em azul.</summary>
        public static void RenderizarControle()
        {
            string saida = Path.Combine(Path.GetTempPath(), "virtualab_previa");
            var args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length - 1; i++)
                if (args[i] == "-inspecaoSaida") saida = args[i + 1];
            Directory.CreateDirectory(saida);
            EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/Controllers/XR Controller Right.prefab");
            var go = (GameObject)UnityEngine.Object.Instantiate(prefab);
            go.transform.position = Vector3.zero;
            var lit = Shader.Find("Universal Render Pipeline/Lit");
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                Color cor = r.name == "Grip" ? Color.red : (r.name == "Trigger" ? Color.blue : (r.name == "Bumper" ? Color.green : new Color(0.6f, 0.6f, 0.6f)));
                var m = new Material(lit); m.SetColor("_BaseColor", cor);
                var arr = new Material[r.sharedMaterials.Length]; for (int k = 0; k < arr.Length; k++) arr[k] = m;
                r.sharedMaterials = arr;
                Debug.Log($"[Inspecao] parte {r.name} centro={r.bounds.center.ToString("F3")}");
            }
            Foto(saida, "_aq.png", new Vector3(0.25f, 0.05f, 0f), Vector3.zero, 30f, false, 64, 64);
            Foto(saida, "controle_esquerda.png", new Vector3(-0.25f, 0.03f, 0f), Vector3.zero, 30f, false, 900, 900);
            Foto(saida, "controle_direita.png", new Vector3(0.25f, 0.03f, 0f), Vector3.zero, 30f, false, 900, 900);
            Foto(saida, "controle_baixo.png", new Vector3(0f, -0.25f, 0.02f), Vector3.zero, 30f, false, 900, 900);
            Debug.Log("[Inspecao] Controle renderizado em " + saida);
        }

        static void Foto(string pasta, string arquivo, Vector3 pos, Vector3 alvo, float fov, bool orto, int w, int h)
        {
            var camGo = new GameObject("CamPrevia");
            var cam = camGo.AddComponent<Camera>();
            cam.transform.position = pos;
            cam.transform.rotation = Quaternion.LookRotation(alvo - pos, Mathf.Abs(Vector3.Dot((alvo - pos).normalized, Vector3.up)) > 0.99f ? Vector3.forward : Vector3.up);
            cam.fieldOfView = fov;
            cam.nearClipPlane = 0.05f;
            cam.farClipPlane = 30f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.45f, 0.55f, 0.7f);
            var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            RenderTexture.active = null;
            cam.targetTexture = null;
            File.WriteAllBytes(Path.Combine(pasta, arquivo), tex.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(tex);
            rt.Release();
            UnityEngine.Object.DestroyImmediate(camGo);
        }

        static string F(float v) => v.ToString("0.#####", System.Globalization.CultureInfo.InvariantCulture);
    }
}
