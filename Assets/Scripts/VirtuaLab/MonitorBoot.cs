using System.Collections;
using TMPro;
using UnityEngine;

namespace VirtuaLab
{
    /// <summary>
    /// Simulação visual de inicialização no monitor virtual.
    ///  - Montagem completa: troca as imagens PNG da tela (POST → carregando → sistema) e mostra o resumo.
    ///  - Montagem incompleta: mostra "FALHA NA INICIALIZAÇÃO" com a lista do que está instalado ou ausente.
    /// Não há BIOS nem sistema operacional reais: é apenas o encerramento visual da atividade.
    /// </summary>
    public class MonitorBoot : MonoBehaviour
    {
        [Tooltip("Renderer do plano da tela (material Unlit).")]
        public Renderer tela;
        [Tooltip("Quadros exibidos em ordem durante o boot.")]
        public Texture2D[] quadros;
        [Tooltip("Duração (s) de cada quadro; o último permanece na tela.")]
        public float[] duracoes = { 2.5f, 2.5f, 0f };
        [Tooltip("Texto sobre a tela usado para o resumo final e para o diagnóstico de falha.")]
        public TextMeshPro textoResultado;
        [Tooltip("Tempo (s) que a tela de falha fica visível.")]
        public float duracaoFalha = 10f;

        public bool BootConcluido { get; private set; }
        public bool EmExecucao { get; private set; }
        public bool MostrandoFalha { get; private set; }

        static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        MaterialPropertyBlock _bloco;

        void Awake()
        {
            _bloco = new MaterialPropertyBlock();
            Desligar();
        }

        public void Desligar()
        {
            StopAllCoroutines();
            EmExecucao = false;
            BootConcluido = false;
            MostrandoFalha = false;
            Aplicar(Texture2D.blackTexture, Color.black);
            if (textoResultado != null) textoResultado.text = "";
        }

        public void IniciarBoot(float tempoMontagem, int acertos, int erros)
        {
            if (EmExecucao || BootConcluido) return;
            StopAllCoroutines();
            MostrandoFalha = false;
            StartCoroutine(Sequencia(tempoMontagem, acertos, erros));
        }

        /// <summary>Tela de falha com o diagnóstico dos componentes (montagem incompleta).</summary>
        public void MostrarFalha(string diagnostico)
        {
            if (EmExecucao || BootConcluido) return;
            StopAllCoroutines();
            StartCoroutine(Falha(diagnostico));
        }

        IEnumerator Falha(string diagnostico)
        {
            MostrandoFalha = true;
            Aplicar(Texture2D.whiteTexture, new Color(0.25f, 0.02f, 0.02f));
            if (textoResultado != null)
                textoResultado.text = "<color=#FF6B5E><b>FALHA NA INICIALIZAÇÃO</b></color>\n<size=80%>Não foi possível iniciar o computador.</size>\n" + diagnostico;
            yield return new WaitForSeconds(duracaoFalha);
            Aplicar(Texture2D.blackTexture, Color.black);
            if (textoResultado != null) textoResultado.text = "";
            MostrandoFalha = false;
        }

        IEnumerator Sequencia(float tempo, int acertos, int erros)
        {
            EmExecucao = true;
            if (textoResultado != null) textoResultado.text = "";
            if (quadros != null)
            {
                for (int i = 0; i < quadros.Length; i++)
                {
                    if (quadros[i] != null) Aplicar(quadros[i], Color.white);
                    float d = (duracoes != null && i < duracoes.Length) ? duracoes[i] : 2f;
                    if (i < quadros.Length - 1 && d > 0f) yield return new WaitForSeconds(d);
                }
            }
            if (textoResultado != null)
            {
                textoResultado.text =
                    "<b>TREINAMENTO CONCLUÍDO</b>\n" +
                    $"Tempo de montagem: {PainelInstrucoes.FormatarTempo(tempo)}\n" +
                    $"Encaixes corretos: {acertos}\n" +
                    $"Erros: {erros}\n" +
                    "Status: computador inicializado";
            }
            EmExecucao = false;
            BootConcluido = true;
        }

        void Aplicar(Texture textura, Color cor)
        {
            if (tela == null) return;
            if (_bloco == null) _bloco = new MaterialPropertyBlock();
            tela.GetPropertyBlock(_bloco);
            _bloco.SetTexture(BaseMapId, textura);
            _bloco.SetColor(BaseColorId, cor);
            tela.SetPropertyBlock(_bloco);
        }
    }
}
