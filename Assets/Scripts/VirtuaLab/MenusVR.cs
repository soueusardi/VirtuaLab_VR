using System.Collections.Generic;
using System.Linq;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace VirtuaLab
{
    /// <summary>
    /// Menus usados dentro do headset, sem depender do teclado do PC:
    ///  - Menu inicial: participante, modo guiado, estatísticas e "Iniciar montagem".
    ///  - Menu de pausa (botão B do controle direito ou Menu do esquerdo): retomar, reiniciar,
    ///    ligar/desligar o modo guiado, próximo participante e estatísticas.
    ///  - Estatísticas: últimas sessões gravadas e botão para exportar os CSV.
    /// </summary>
    public class MenusVR : MonoBehaviour
    {
        public GerenciadorMontagem gerenciador;
        public RegistroSessao registro;

        [Header("Menu inicial")]
        public GameObject menuInicial;
        public TextMeshPro textoInicial;
        public BotaoVR botaoIniciar;
        public BotaoVR botaoModoInicial;
        public BotaoVR botaoProximoInicial;
        public BotaoVR botaoEstatisticasInicial;

        [Header("Menu de pausa")]
        public GameObject menuPausa;
        public TextMeshPro textoPausa;
        public BotaoVR botaoRetomar;
        public BotaoVR botaoReiniciar;
        public BotaoVR botaoModoPausa;
        public BotaoVR botaoProximoPausa;
        public BotaoVR botaoEstatisticasPausa;
        [Tooltip("Distância (m) do menu de pausa à frente do usuário.")]
        public float distanciaPausa = 0.75f;

        [Header("Estatísticas")]
        public GameObject menuEstatisticas;
        public TextMeshPro textoEstatisticas;
        public BotaoVR botaoExportar;
        public BotaoVR botaoVoltarEstatisticas;
        [Tooltip("Quantas sessões aparecem na lista (as mais recentes).")]
        public int sessoesNaLista = 8;

        /// <summary>Ao reiniciar pelo menu de pausa, a nova cena começa direto na montagem.</summary>
        public static bool PularMenuInicial;

        public bool MenuInicialAberto => menuInicial != null && menuInicial.activeSelf;
        public bool MenuPausaAberto => menuPausa != null && menuPausa.activeSelf;
        public bool EstatisticasAbertas => menuEstatisticas != null && menuEstatisticas.activeSelf;

        InputAction _abrirMenu;
        GameObject _origemEstatisticas;
        string _mensagemExportacao = "";

        void Awake()
        {
            // B do controle direito ou botão Menu do esquerdo (Esc no teclado, para testar no PC).
            _abrirMenu = new InputAction("AbrirMenu", InputActionType.Button);
            _abrirMenu.AddBinding("<XRController>{RightHand}/secondaryButton");
            _abrirMenu.AddBinding("<XRController>{LeftHand}/menu");
            _abrirMenu.AddBinding("<XRController>{LeftHand}/menuButton");
            _abrirMenu.AddBinding("<Keyboard>/escape");

            Ligar(botaoIniciar, Iniciar);
            Ligar(botaoModoInicial, AlternarModo);
            Ligar(botaoProximoInicial, ProximoParticipanteNoInicio);
            Ligar(botaoEstatisticasInicial, () => AbrirEstatisticas(menuInicial));
            Ligar(botaoRetomar, FecharPausa);
            Ligar(botaoReiniciar, Reiniciar);
            Ligar(botaoModoPausa, AlternarModo);
            Ligar(botaoProximoPausa, ProximoParticipante);
            Ligar(botaoEstatisticasPausa, () => AbrirEstatisticas(menuPausa));
            Ligar(botaoExportar, Exportar);
            Ligar(botaoVoltarEstatisticas, FecharEstatisticas);

            if (menuPausa != null) menuPausa.SetActive(false);
            if (menuEstatisticas != null) menuEstatisticas.SetActive(false);
        }

        void OnEnable() => _abrirMenu.Enable();
        void OnDisable() => _abrirMenu.Disable();
        void OnDestroy() => _abrirMenu.Dispose();

        static void Ligar(BotaoVR b, System.Action acao)
        {
            if (b != null) b.Pressionado += acao;
        }

        void Update()
        {
            if (!_abrirMenu.WasPressedThisFrame()) return;
            if (EstatisticasAbertas) FecharEstatisticas();
            else if (MenuInicialAberto) return;
            else if (MenuPausaAberto) FecharPausa();
            else AbrirPausa();
        }

        // ------------------------------------------------------------------ Menu inicial

        public void MostrarInicial()
        {
            if (menuInicial != null) menuInicial.SetActive(true);
            AtualizarTextos();
        }

        public void Iniciar()
        {
            if (menuInicial != null) menuInicial.SetActive(false);
            if (gerenciador != null) gerenciador.IniciarMontagem();
        }

        void ProximoParticipanteNoInicio()
        {
            ControleSessao.AvancarParticipante(registro);
            AtualizarTextos();
        }

        // ------------------------------------------------------------------ Menu de pausa

        public void AbrirPausa()
        {
            if (menuPausa == null) return;
            var cam = Camera.main;
            if (cam != null)
            {
                Vector3 frente = cam.transform.forward;
                frente.y = 0f;
                if (frente.sqrMagnitude < 1e-4f) frente = Vector3.forward;
                frente.Normalize();
                menuPausa.transform.position = cam.transform.position + frente * distanciaPausa + Vector3.down * 0.15f;
                menuPausa.transform.rotation = Quaternion.LookRotation(frente, Vector3.up);
            }
            menuPausa.SetActive(true);
            if (gerenciador != null) gerenciador.DefinirPausa(true);
            AtualizarTextos();
        }

        public void FecharPausa()
        {
            if (menuPausa != null) menuPausa.SetActive(false);
            if (gerenciador != null) gerenciador.DefinirPausa(false);
        }

        void Reiniciar()
        {
            PularMenuInicial = true;
            ControleSessao.ReiniciarCena(false);
        }

        void ProximoParticipante()
        {
            PularMenuInicial = false;
            ControleSessao.ReiniciarCena(true);
        }

        void AlternarModo()
        {
            if (gerenciador == null) return;
            gerenciador.AlternarModoGuiado();
            ControleSessao.LembrarModo(gerenciador.modoGuiado);
            AtualizarTextos();
        }

        // ------------------------------------------------------------------ Estatísticas

        /// <summary>Abre a tela de estatísticas no lugar do menu de origem (inicial ou pausa).</summary>
        public void AbrirEstatisticas(GameObject origem)
        {
            if (menuEstatisticas == null) return;
            _origemEstatisticas = origem;
            if (origem != null)
            {
                menuEstatisticas.transform.SetPositionAndRotation(origem.transform.position, origem.transform.rotation);
                origem.SetActive(false);
            }
            _mensagemExportacao = "";
            menuEstatisticas.SetActive(true);
            AtualizarEstatisticas();
        }

        public void FecharEstatisticas()
        {
            if (menuEstatisticas != null) menuEstatisticas.SetActive(false);
            if (_origemEstatisticas != null) _origemEstatisticas.SetActive(true);
            _origemEstatisticas = null;
            AtualizarTextos();
        }

        void Exportar()
        {
            _mensagemExportacao = RegistroSessao.Exportar();
            AtualizarEstatisticas();
        }

        void AtualizarEstatisticas()
        {
            if (textoEstatisticas == null) return;
            var linhas = RegistroSessao.LerResumo();
            // Uma linha por sessão: prefere a linha POWER; senão a última gravada.
            var porSessao = new Dictionary<string, Dictionary<string, string>>();
            foreach (var l in linhas)
            {
                if (!l.TryGetValue("sessao", out var s) || string.IsNullOrEmpty(s)) continue;
                if (!porSessao.ContainsKey(s) || l.GetValueOrDefault("momento") == "POWER" || porSessao[s].GetValueOrDefault("momento") != "POWER")
                    porSessao[s] = l;
            }
            var sessoes = porSessao.Values.OrderByDescending(l => l.GetValueOrDefault("inicio")).ToList();
            int participantes = sessoes.Select(l => l.GetValueOrDefault("participante")).Distinct().Count();

            var sb = new StringBuilder();
            sb.AppendLine("<b>ESTATÍSTICAS</b>");
            sb.AppendLine($"<size=85%>Sessões gravadas: {sessoes.Count}   Participantes: {participantes}</size>");
            sb.AppendLine("<size=80%><color=#AAAAAA>Part.  Tent.  Modo     Tempo   Acertos  Erros  Situação</color>");
            foreach (var l in sessoes.Take(sessoesNaLista))
            {
                float.TryParse(l.GetValueOrDefault("tempo_montagem_s"), System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out float tempo);
                string situacao = l.GetValueOrDefault("momento") == "POWER" ? "ligou" :
                                  l.GetValueOrDefault("momento") == "CONCLUSAO" ? "concluiu" : "incompleta";
                sb.AppendLine($"{l.GetValueOrDefault("participante"),-6} T{l.GetValueOrDefault("tentativa"),-4} {l.GetValueOrDefault("modo"),-8} " +
                              $"{PainelInstrucoes.FormatarTempo(tempo)}   {l.GetValueOrDefault("acertos"),-7}  {l.GetValueOrDefault("erros_total"),-5}  {situacao}");
            }
            if (sessoes.Count == 0) sb.AppendLine("Nenhuma sessão gravada ainda.");
            sb.Append("</size>");
            if (!string.IsNullOrEmpty(_mensagemExportacao))
                sb.Append("\n<size=70%><color=#8FD3FF>").Append(_mensagemExportacao).Append("</color></size>");
            textoEstatisticas.text = sb.ToString();
        }

        // ------------------------------------------------------------------ Textos

        void AtualizarTextos()
        {
            bool guiado = gerenciador == null || gerenciador.modoGuiado;
            string modo = guiado ? "Modo guiado: LIGADO" : "Modo guiado: DESLIGADO";
            if (botaoModoInicial != null) botaoModoInicial.DefinirTexto(modo);
            if (botaoModoPausa != null) botaoModoPausa.DefinirTexto(modo);

            string part = registro == null ? "" :
                $"Participante: <b>{registro.codigoParticipante}</b>   " +
                (registro.tentativa == 0 ? "Tentativa 0 (familiarização)" : $"Tentativa {registro.tentativa}");

            if (textoInicial != null)
                textoInicial.text =
                    "<b>VIRTUALAB VR</b>\n<size=70%>Montagem de computador</size>\n\n" +
                    "<size=60%>Para pegar uma peça ou apertar um botão, aponte ou encoste o controle e " +
                    "segure o <color=#FFD84D><b>botão lateral (dedo médio)</b></color>. Solte o botão para soltar a peça.\n" +
                    "Aperte <b>B</b> a qualquer momento para abrir o menu.\n\n" + part + "</size>";

            if (textoPausa != null && gerenciador != null)
                textoPausa.text =
                    "<b>PAUSA</b>\n<size=60%>" + part + "\n" +
                    $"Tempo: {PainelInstrucoes.FormatarTempo(gerenciador.TempoMontagem)}   " +
                    $"Etapas: {gerenciador.ContarConcluidas()}/{gerenciador.ContarObrigatorias()}" +
                    (gerenciador.modoGuiado ? $"   Erros: {gerenciador.ErrosTotal}" : "") + "</size>";
        }
    }
}
