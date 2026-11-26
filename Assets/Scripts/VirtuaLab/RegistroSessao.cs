using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace VirtuaLab
{
    /// <summary>
    /// Grava as métricas da sessão em CSV (separador ';', UTF-8 com BOM, abre direto no Excel):
    ///  - eventos_&lt;sessao&gt;.csv : um evento por linha (pegou, encaixe, erro, conclusão...).
    ///  - resumo_sessoes.csv     : uma linha por sessão concluída (tempo, acertos, erros).
    /// No Editor/Quest Link os arquivos ficam em &lt;projeto&gt;/RegistrosSessoes/.
    /// Não grava nenhum dado pessoal: apenas o código do participante (ex.: P01).
    /// </summary>
    public class RegistroSessao : MonoBehaviour
    {
        [Tooltip("Código anônimo do participante (P01, P02...). Pode ser trocado com a tecla N.")]
        public string codigoParticipante = "P00";
        [Tooltip("Número da tentativa deste participante (tecla R incrementa). 0 = familiarização, não avaliada.")]
        public int tentativa = 1;
        public bool gravarEmDisco = true;

        // Persistem entre recarregamentos da cena (teclas R / N do ControleSessao).
        public static string ParticipanteGlobal;
        public static int TentativaGlobal;
        /// <summary>Se definido, substitui a pasta de saída (usado pelos testes automatizados).</summary>
        public static string PastaSaidaForcada;

        public string IdSessao { get; private set; }
        public string PastaSaida { get; private set; }

        string _arquivoEventos;
        GerenciadorMontagem _gerenciador;
        DateTime _inicio;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        void Awake()
        {
            if (!string.IsNullOrEmpty(ParticipanteGlobal))
            {
                codigoParticipante = ParticipanteGlobal;
                tentativa = Mathf.Max(0, TentativaGlobal);
            }
            else
            {
                ParticipanteGlobal = codigoParticipante;
                TentativaGlobal = tentativa;
            }
        }

        public void IniciarSessao(GerenciadorMontagem g)
        {
            _gerenciador = g;
            _inicio = DateTime.Now;
            IdSessao = $"{codigoParticipante}_T{tentativa}_{_inicio:yyyyMMdd_HHmmss}";
            if (!gravarEmDisco) return;
            try
            {
#if UNITY_EDITOR
                PastaSaida = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "RegistrosSessoes"));
#else
                PastaSaida = Path.Combine(Application.persistentDataPath, "RegistrosSessoes");
#endif
                if (!string.IsNullOrEmpty(PastaSaidaForcada)) PastaSaida = PastaSaidaForcada;
                Directory.CreateDirectory(PastaSaida);
                _arquivoEventos = Path.Combine(PastaSaida, $"eventos_{IdSessao}.csv");
                File.WriteAllText(_arquivoEventos,
                    "data_hora;sessao;participante;tentativa;tempo_montagem_s;evento;peca;local;resultado;detalhe\n",
                    new UTF8Encoding(true));
                RegistrarEvento(0f, "SESSAO_INICIADA", "", "", g.modoGuiado ? "MODO_GUIADO" : "MODO_LIVRE", "");
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VirtuaLab] Não foi possível criar o registro da sessão: " + e.Message);
                gravarEmDisco = false;
            }
        }

        /// <summary>
        /// Troca o código do participante antes de a montagem começar (menu inicial). O arquivo de
        /// eventos aberto com o código anterior é apagado se ainda não tiver nenhuma ação registrada.
        /// </summary>
        public void TrocarParticipante(string codigo, int novaTentativa)
        {
            try
            {
                if (!string.IsNullOrEmpty(_arquivoEventos) && File.Exists(_arquivoEventos) && File.ReadAllLines(_arquivoEventos).Length <= 2)
                    File.Delete(_arquivoEventos);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VirtuaLab] Não foi possível apagar o registro vazio: " + e.Message);
            }
            codigoParticipante = codigo;
            tentativa = novaTentativa;
            if (_gerenciador != null) IniciarSessao(_gerenciador);
        }

        public void RegistrarEvento(float tempoMontagem, string evento, string peca, string local, string resultado, string detalhe)
        {
            if (!gravarEmDisco || string.IsNullOrEmpty(_arquivoEventos)) return;
            try
            {
                string linha = string.Join(";",
                    DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff", Inv), IdSessao, codigoParticipante,
                    tentativa.ToString(Inv), tempoMontagem.ToString("0.00", Inv), evento,
                    Limpar(peca), Limpar(local), Limpar(resultado), Limpar(detalhe)) + "\n";
                File.AppendAllText(_arquivoEventos, linha, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VirtuaLab] Falha ao gravar evento: " + e.Message);
            }
        }

        /// <summary>Acrescenta/atualiza a linha de resumo desta sessão.</summary>
        public void GravarResumo(GerenciadorMontagem g, bool ligou, string momento = null)
        {
            if (!gravarEmDisco || string.IsNullOrEmpty(PastaSaida)) return;
            try
            {
                string arq = Path.Combine(PastaSaida, "resumo_sessoes.csv");
                if (!File.Exists(arq))
                    File.WriteAllText(arq,
                        "sessao;participante;tentativa;inicio;modo;momento;tempo_montagem_s;etapas_concluidas;etapas_total;acertos;erros_total;erros_local;erros_ordem;erros_orientacao;tentativas_encaixe;taxa_erro;computador_ligado;falhas_power;retiradas\n",
                        new UTF8Encoding(true));
                float taxa = g.Tentativas > 0 ? g.ErrosTotal / (float)g.Tentativas : 0f;
                string linha = string.Join(";",
                    IdSessao, codigoParticipante, tentativa.ToString(Inv), _inicio.ToString("yyyy-MM-dd HH:mm:ss", Inv),
                    g.modoGuiado ? "GUIADO" : "LIVRE", momento ?? (ligou ? "POWER" : "CONCLUSAO"),
                    g.TempoMontagem.ToString("0.00", Inv), g.ContarConcluidas().ToString(Inv), g.ContarObrigatorias().ToString(Inv),
                    g.Acertos.ToString(Inv), g.ErrosTotal.ToString(Inv), g.ErrosLocal.ToString(Inv), g.ErrosOrdem.ToString(Inv),
                    g.ErrosOrientacao.ToString(Inv), g.Tentativas.ToString(Inv), taxa.ToString("0.000", Inv), ligou ? "1" : "0", g.FalhasPower.ToString(Inv), g.Retiradas.ToString(Inv)) + "\n";
                File.AppendAllText(arq, linha, new UTF8Encoding(false));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VirtuaLab] Falha ao gravar resumo: " + e.Message);
            }
        }

        /// <summary>Pasta onde as sessões são gravadas (PC: projeto/RegistrosSessoes; Quest: armazenamento do app).</summary>
        public static string CaminhoPasta()
        {
            if (!string.IsNullOrEmpty(PastaSaidaForcada)) return PastaSaidaForcada;
#if UNITY_EDITOR
            return Path.GetFullPath(Path.Combine(Application.dataPath, "..", "RegistrosSessoes"));
#else
            return Path.Combine(Application.persistentDataPath, "RegistrosSessoes");
#endif
        }

        /// <summary>Lê resumo_sessoes.csv (uma entrada por linha, colunas pelo nome do cabeçalho).</summary>
        public static List<Dictionary<string, string>> LerResumo()
        {
            var lista = new List<Dictionary<string, string>>();
            try
            {
                string arq = Path.Combine(CaminhoPasta(), "resumo_sessoes.csv");
                if (!File.Exists(arq)) return lista;
                var linhas = File.ReadAllLines(arq, Encoding.UTF8);
                if (linhas.Length < 2) return lista;
                var cab = linhas[0].TrimStart('\uFEFF').Split(';');
                for (int i = 1; i < linhas.Length; i++)
                {
                    if (string.IsNullOrWhiteSpace(linhas[i])) continue;
                    var v = linhas[i].Split(';');
                    var d = new Dictionary<string, string>();
                    for (int k = 0; k < cab.Length && k < v.Length; k++) d[cab[k]] = v[k];
                    lista.Add(d);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VirtuaLab] Falha ao ler o resumo: " + e.Message);
            }
            return lista;
        }

        /// <summary>
        /// Copia resumo_sessoes.csv e todos os eventos_*.csv para uma pasta de exportação com data e hora.
        /// No Quest também tenta copiar para Download/VirtuaLab (fácil de achar pelo cabo USB).
        /// Devolve uma mensagem com os caminhos, para exibir na tela.
        /// </summary>
        public static string Exportar()
        {
            string origem = CaminhoPasta();
            if (!Directory.Exists(origem)) return "Nenhum registro para exportar.";
            string nome = "VirtuaLab_resultados_" + DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var arquivos = new List<string>();
            string resumo = Path.Combine(origem, "resumo_sessoes.csv");
            if (File.Exists(resumo)) arquivos.Add(resumo);
            arquivos.AddRange(Directory.GetFiles(origem, "eventos_*.csv"));
            if (arquivos.Count == 0) return "Nenhum registro para exportar.";

            var msg = new StringBuilder();
            string interno = Path.Combine(origem, "Exportacoes", nome);
            if (CopiarTodos(arquivos, interno, out string erro)) msg.Append($"{arquivos.Count} arquivo(s) exportado(s) para: {interno}");
            else msg.Append("Falha ao exportar: " + erro);
#if UNITY_ANDROID && !UNITY_EDITOR
            string publico = Path.Combine("/storage/emulated/0/Download/VirtuaLab", nome);
            if (CopiarTodos(arquivos, publico, out string erroPub)) msg.Append($"\nCópia em: {publico}");
            else msg.Append("\n(Não foi possível copiar para Download: " + erroPub + ")");
#endif
            return msg.ToString();
        }

        static bool CopiarTodos(List<string> arquivos, string destino, out string erro)
        {
            erro = "";
            try
            {
                Directory.CreateDirectory(destino);
                foreach (var a in arquivos) File.Copy(a, Path.Combine(destino, Path.GetFileName(a)), true);
                return true;
            }
            catch (Exception e)
            {
                erro = e.Message;
                return false;
            }
        }

        static string Limpar(string s) => string.IsNullOrEmpty(s) ? "" : s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
    }
}
