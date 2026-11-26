using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VirtuaLab
{
    /// <summary>
    /// Botão Power do gabinete. Fica vermelho (bloqueado) até a montagem ser concluída; depois
    /// pisca em verde. O acionamento é por seleção (gatilho/grip apontando ou tocando o botão).
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class BotaoPower : MonoBehaviour
    {
        public Renderer indicador;
        public Color corBloqueado = new Color(0.75f, 0.08f, 0.08f);
        public Color corLiberado = new Color(0.1f, 1f, 0.3f);
        public Color corLigado = new Color(0.2f, 0.6f, 1f);

        public bool Liberado { get; private set; }
        bool _ligado;
        XRSimpleInteractable _interativo;
        MaterialPropertyBlock _bloco;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            _interativo = GetComponent<XRSimpleInteractable>();
            _bloco = new MaterialPropertyBlock();
            Pintar(corBloqueado);
        }

        void OnEnable() { _interativo.selectEntered.AddListener(AoPressionar); }
        void OnDisable() { _interativo.selectEntered.RemoveListener(AoPressionar); }

        void AoPressionar(SelectEnterEventArgs args)
        {
            if (GerenciadorMontagem.Instancia != null) GerenciadorMontagem.Instancia.TentarLigar();
        }

        public void DefinirLiberado(bool liberado)
        {
            Liberado = liberado;
            if (!_ligado) Pintar(liberado ? corLiberado : corBloqueado);
        }

        public void DefinirLigado()
        {
            _ligado = true;
            Pintar(corLigado);
        }

        void Update()
        {
            if (Liberado && !_ligado)
            {
                float k = 0.55f + 0.45f * Mathf.Sin(Time.time * 6f);
                Pintar(Color.Lerp(corLiberado * 0.35f, corLiberado, k));
            }
        }

        void Pintar(Color cor)
        {
            if (indicador == null) return;
            if (_bloco == null) _bloco = new MaterialPropertyBlock();
            indicador.GetPropertyBlock(_bloco);
            _bloco.SetColor(BaseColorId, cor);
            indicador.SetPropertyBlock(_bloco);
        }
    }
}
