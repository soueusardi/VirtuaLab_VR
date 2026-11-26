using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace VirtuaLab
{
    /// <summary>
    /// Tutorial do botão de pegar. O modelo de controle do XR Interaction Toolkit
    /// (UniversalController) chama o botão lateral de pegada de "Bumper" (a parte "Trigger" é o gatilho).
    /// Este script pinta essa parte de amarelo piscando e mostra o rótulo "PEGAR" ao lado.
    /// Tudo some quando a primeira peça é pega.
    /// </summary>
    public class DicaGrip : MonoBehaviour
    {
        [Tooltip("Raiz do XR Origin (os controles são procurados pelos nomes abaixo).")]
        public Transform rig;
        public string nomeControleEsquerdo = "Left Controller";
        public string nomeControleDireito = "Right Controller";
        [Tooltip("Nome da parte do modelo do controle que representa o botão de pegada.")]
        public string nomeParteGrip = "Bumper";
        public Material materialMarcador;
        public Color cor = new Color(1f, 0.85f, 0.1f, 1f);

        public bool Visivel { get; private set; } = true;
        /// <summary>Quantos controles tiveram o botão de pegada encontrado (0 a 2).</summary>
        public int GripsEncontrados => _grips.Count;

        class Destaque
        {
            public Renderer[] renderers;
            public Material[][] originais;
            public Transform rotulo;
            public Transform alvo;
        }

        readonly List<Destaque> _grips = new List<Destaque>();
        readonly HashSet<Transform> _controlesProcessados = new HashSet<Transform>();
        MaterialPropertyBlock _bloco;
        float _procurarAte;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Start()
        {
            _bloco = new MaterialPropertyBlock();
            // O modelo do controle pode ser criado alguns quadros depois: procura por até 10 s.
            _procurarAte = Time.unscaledTime + 10f;
            Procurar();
        }

        void Procurar()
        {
            if (rig == null || _controlesProcessados.Count >= 2) return;
            foreach (var t in rig.GetComponentsInChildren<Transform>(true))
            {
                if ((t.name != nomeControleDireito && t.name != nomeControleEsquerdo) || _controlesProcessados.Contains(t)) continue;
                Transform grip = null;
                foreach (var filho in t.GetComponentsInChildren<Transform>(true))
                    if (string.Equals(filho.name, nomeParteGrip, System.StringComparison.OrdinalIgnoreCase)) { grip = filho; break; }
                if (grip == null) continue;
                _controlesProcessados.Add(t);
                _grips.Add(CriarDestaque(grip));
            }
        }

        Destaque CriarDestaque(Transform grip)
        {
            var d = new Destaque { alvo = grip, renderers = grip.GetComponentsInChildren<Renderer>(true) };
            d.originais = new Material[d.renderers.Length][];
            for (int i = 0; i < d.renderers.Length; i++)
            {
                d.originais[i] = d.renderers[i].sharedMaterials;
                if (materialMarcador == null) continue;
                var mats = new Material[d.originais[i].Length];
                for (int k = 0; k < mats.Length; k++) mats[k] = materialMarcador;
                d.renderers[i].sharedMaterials = mats;
            }

            var rotuloGo = new GameObject("Dica_Grip_Rotulo");
            rotuloGo.transform.SetParent(transform, false);
            rotuloGo.transform.localScale = Vector3.one * 0.1f;
            var t = rotuloGo.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = new Vector2(1.8f, 0.4f);
            t.fontSize = 1.3f;
            t.alignment = TextAlignmentOptions.Center;
            t.color = cor;
            t.text = "<b>PEGAR</b>\nbotão lateral (dedo médio)";
            d.rotulo = rotuloGo.transform;
            return d;
        }

        void Update()
        {
            if (!Visivel) return;
            if (_controlesProcessados.Count < 2 && Time.unscaledTime < _procurarAte) Procurar();

            float k = 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));
            Color c = Color.Lerp(cor * 0.4f, cor, k);
            c.a = 1f;
            var cam = Camera.main;
            foreach (var d in _grips)
            {
                foreach (var r in d.renderers)
                {
                    if (r == null) continue;
                    r.GetPropertyBlock(_bloco);
                    _bloco.SetColor(BaseColorId, c);
                    r.SetPropertyBlock(_bloco);
                }
                if (d.rotulo == null || d.alvo == null) continue;
                Vector3 centro = d.renderers.Length > 0 && d.renderers[0] != null ? d.renderers[0].bounds.center : d.alvo.position;
                d.rotulo.position = centro + Vector3.up * 0.07f;
                if (cam != null)
                    d.rotulo.rotation = Quaternion.LookRotation(d.rotulo.position - cam.transform.position, Vector3.up);
            }
        }

        public void Ocultar()
        {
            if (!Visivel) return;
            Visivel = false;
            foreach (var d in _grips)
            {
                for (int i = 0; i < d.renderers.Length; i++)
                {
                    if (d.renderers[i] == null) continue;
                    d.renderers[i].SetPropertyBlock(null);
                    d.renderers[i].sharedMaterials = d.originais[i];
                }
                if (d.rotulo != null) Destroy(d.rotulo.gameObject);
            }
        }
    }
}
