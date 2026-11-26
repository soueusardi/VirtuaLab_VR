using System;
using TMPro;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VirtuaLab
{
    /// <summary>
    /// Botão 3D dos menus. É acionado do mesmo jeito que se pega uma peça: apontando (ou tocando)
    /// e apertando o botão lateral do controle (grip). Assim o participante aprende um único botão.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class BotaoVR : MonoBehaviour
    {
        public Renderer fundo;
        public TextMeshPro rotulo;
        public Color corNormal = new Color(0.16f, 0.30f, 0.55f);
        public Color corHover = new Color(0.25f, 0.55f, 0.95f);

        /// <summary>Disparado quando o botão é pressionado.</summary>
        public event Action Pressionado;

        XRSimpleInteractable _interativo;
        MaterialPropertyBlock _bloco;
        float _ultimoClique = -1f;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _interativo = GetComponent<XRSimpleInteractable>();
            _bloco = new MaterialPropertyBlock();
            Pintar(corNormal);
        }

        void OnEnable()
        {
            _interativo.selectEntered.AddListener(AoSelecionar);
            _interativo.hoverEntered.AddListener(AoEntrar);
            _interativo.hoverExited.AddListener(AoSair);
            Pintar(corNormal);
        }

        void OnDisable()
        {
            _interativo.selectEntered.RemoveListener(AoSelecionar);
            _interativo.hoverEntered.RemoveListener(AoEntrar);
            _interativo.hoverExited.RemoveListener(AoSair);
        }

        public void DefinirTexto(string texto)
        {
            if (rotulo != null) rotulo.text = texto;
        }

        /// <summary>Aciona o botão por código (também usado pelos testes).</summary>
        public void Pressionar()
        {
            // Evita clique duplo quando a mesma pegada dispara mais de um evento.
            if (Time.unscaledTime - _ultimoClique < 0.35f) return;
            _ultimoClique = Time.unscaledTime;
            Pressionado?.Invoke();
        }

        void AoSelecionar(SelectEnterEventArgs args) => Pressionar();
        void AoEntrar(HoverEnterEventArgs args) => Pintar(corHover);
        void AoSair(HoverExitEventArgs args) { if (!_interativo.isHovered) Pintar(corNormal); }

        void Pintar(Color cor)
        {
            if (fundo == null) return;
            if (_bloco == null) _bloco = new MaterialPropertyBlock();
            fundo.GetPropertyBlock(_bloco);
            _bloco.SetColor(BaseColorId, cor);
            fundo.SetPropertyBlock(_bloco);
        }
    }
}
