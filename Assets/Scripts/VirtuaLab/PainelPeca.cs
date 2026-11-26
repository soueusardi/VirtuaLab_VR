using System.Text;
using TMPro;
using UnityEngine;

namespace VirtuaLab
{
    /// <summary>
    /// Pequeno painel semitransparente que acompanha a peça segurada, logo acima dela e virado
    /// para o usuário: nome, função, onde instalar (modo guiado) e um cuidado importante.
    /// </summary>
    public class PainelPeca : MonoBehaviour
    {
        public GameObject raizVisual;
        public Renderer fundo;
        public TextMeshPro texto;
        [Tooltip("Altura do painel acima do centro da peça (m).")]
        public float alturaAcimaDaPeca = 0.13f;
        [Tooltip("Quanto o painel se aproxima da câmera, para não ficar dentro da peça (m).")]
        public float avancoParaCamera = 0.06f;

        PecaHardware _peca;
        Transform _camera;
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        void Awake()
        {
            if (fundo != null)
            {
                var bloco = new MaterialPropertyBlock();
                bloco.SetColor(BaseColorId, new Color(0.02f, 0.04f, 0.09f, 0.78f));
                fundo.SetPropertyBlock(bloco);
            }
            if (raizVisual != null) raizVisual.SetActive(false);
        }

        public void Mostrar(PecaHardware peca, bool modoGuiado)
        {
            if (peca == null || texto == null) return;
            _peca = peca;
            var sb = new StringBuilder();
            sb.Append("<b>").Append(peca.nomeExibicao).Append("</b>\n");
            if (!string.IsNullOrEmpty(peca.descricao)) sb.Append(peca.descricao).Append('\n');
            if (modoGuiado && !string.IsNullOrEmpty(peca.explicacaoLocal))
                sb.Append("<color=#8FD3FF>Onde: ").Append(peca.explicacaoLocal).Append("</color>\n");
            // No modo livre o painel só lembra o que é a peça: onde vai e como orientar ficam por conta do participante.
            if (modoGuiado && !string.IsNullOrEmpty(peca.cuidado))
                sb.Append("<color=#FFD27A>Cuidado: ").Append(peca.cuidado).Append("</color>");
            texto.text = sb.ToString().TrimEnd('\n');
            if (raizVisual != null) raizVisual.SetActive(true);
            Posicionar();
        }

        /// <summary>Oculta o painel se ele estiver mostrando esta peça (ou qualquer uma, se nula).</summary>
        public void Ocultar(PecaHardware peca = null)
        {
            if (peca != null && peca != _peca) return;
            _peca = null;
            if (raizVisual != null) raizVisual.SetActive(false);
        }

        void LateUpdate()
        {
            if (_peca == null) return;
            if (!_peca.Segurada || _peca.Instalada) { Ocultar(); return; }
            Posicionar();
        }

        void Posicionar()
        {
            if (_camera == null && Camera.main != null) _camera = Camera.main.transform;
            if (raizVisual == null || _peca == null) return;
            Vector3 pos = _peca.PontoReferenciaMundo + Vector3.up * alturaAcimaDaPeca;
            if (_camera != null)
            {
                Vector3 paraCamera = _camera.position - pos;
                if (paraCamera.sqrMagnitude > 1e-4f) pos += paraCamera.normalized * avancoParaCamera;
                Vector3 olhar = pos - _camera.position;
                olhar.y *= 0.5f; // inclina menos, fica mais confortável de ler
                if (olhar.sqrMagnitude > 1e-4f) raizVisual.transform.rotation = Quaternion.LookRotation(olhar, Vector3.up);
            }
            raizVisual.transform.position = pos;
        }
    }
}
