using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;

namespace VirtuaLab
{
    public enum TipoFeedback { Neutro, Sucesso, Erro }

    /// <summary>
    /// Painel de instruções fixo atrás da bancada (o "telão"): título, objetivo, etapas reveladas
    /// aos poucos, mensagem de feedback (verde/vermelho) e indicadores de tempo/acertos/erros.
    /// A explicação da peça segurada fica no painel junto da mão (PainelPeca), não aqui.
    /// Só exibe; toda a lógica fica no <see cref="GerenciadorMontagem"/>.
    /// </summary>
    public class PainelInstrucoes : MonoBehaviour
    {
        public TextMeshPro textoTitulo;
        public TextMeshPro textoEtapas;
        public TextMeshPro textoPeca;
        public TextMeshPro textoFeedback;
        public TextMeshPro textoStatus;

        static readonly Color CorNeutra = new Color(0.92f, 0.92f, 0.92f);
        static readonly Color CorSucesso = new Color(0.25f, 1f, 0.35f);
        static readonly Color CorErro = new Color(1f, 0.32f, 0.28f);

        float _feedbackExpiraEm = -1f;
        string _ultimoStatus;

        void Update()
        {
            if (_feedbackExpiraEm > 0f && Time.time >= _feedbackExpiraEm && textoFeedback != null)
            {
                textoFeedback.text = "";
                _feedbackExpiraEm = -1f;
            }
        }

        /// <summary>
        /// Lista de etapas. Etapas concluídas aparecem marcadas; no modo guiado aparece também a
        /// próxima etapa sugerida; as demais ficam ocultas ("Etapa ainda não revelada"), para o
        /// participante aprender a sequência aos poucos.
        /// </summary>
        public void AtualizarEtapas(List<PontoEncaixe> pontos, PontoEncaixe sugerida, bool modoGuiado)
        {
            if (textoEtapas == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("<b>ETAPAS DA MONTAGEM</b>");
            int i = 1;
            foreach (var p in pontos)
            {
                if (p == null) continue;
                if (p.Concluido)
                    sb.AppendLine($"<color=#55FF66>[OK] {i}. {p.nomeEtapa}</color>");
                else if (modoGuiado && p == sugerida)
                    sb.AppendLine($"<color=#FFE066><b>[ > ] {i}. {p.nomeEtapa}</b></color>");
                else
                    sb.AppendLine($"<color=#777777>[   ] {i}. Etapa ainda não revelada</color>");
                i++;
            }
            if (!modoGuiado)
                sb.AppendLine("<size=80%><color=#AAAAAA>Modo livre: sem indicação da próxima etapa nem do local.</color></size>");
            textoEtapas.text = sb.ToString();
        }

        /// <param name="duracao">Segundos até apagar a mensagem; 0 mantém até a próxima.</param>
        public void MostrarFeedback(string mensagem, TipoFeedback tipo, float duracao = 8f)
        {
            if (textoFeedback == null) return;
            textoFeedback.text = mensagem;
            textoFeedback.color = tipo == TipoFeedback.Sucesso ? CorSucesso : (tipo == TipoFeedback.Erro ? CorErro : CorNeutra);
            _feedbackExpiraEm = duracao > 0f ? Time.time + duracao : -1f;
        }

        /// <param name="mostrarErros">Falso no modo livre: acertos e erros só aparecem no fim, para não entregar a resposta.</param>
        public void AtualizarStatus(float tempo, int acertos, int erros, int concluidas, int total, bool mostrarErros = true)
        {
            if (textoStatus == null) return;
            string texto = mostrarErros
                ? $"Tempo: {FormatarTempo(tempo)}    Etapas: {concluidas}/{total}    Acertos: {acertos}    Erros: {erros}"
                : $"Tempo: {FormatarTempo(tempo)}    Modo livre: aperte POWER para verificar a montagem";
            if (texto == _ultimoStatus) return;
            _ultimoStatus = texto;
            textoStatus.text = texto;
        }

        public void MostrarConclusao(float tempo, int acertos, int erros)
        {
            if (textoTitulo != null) textoTitulo.text = "MONTAGEM CONCLUÍDA";
            if (textoPeca != null)
                textoPeca.text = $"<b>RESULTADO</b>\nTempo de montagem: {FormatarTempo(tempo)}\nEncaixes corretos: {acertos}\nErros cometidos: {erros}\n\n<size=85%>Ligue o computador pelo botão POWER.\nMenu: botão B.</size>";
        }

        public static string FormatarTempo(float segundos)
        {
            int s = Mathf.Max(0, Mathf.FloorToInt(segundos));
            return $"{s / 60:00}:{s % 60:00}";
        }
    }
}
