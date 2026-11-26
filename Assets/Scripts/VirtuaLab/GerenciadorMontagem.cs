using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Events;

namespace VirtuaLab
{
    /// <summary>
    /// Controla o fluxo da montagem: decide encaixe/rejeição das peças soltas, atualiza os guias
    /// visuais, conta tempo/acertos/erros, libera o botão Power ao final e alimenta o painel e o
    /// registro da sessão. Existe uma única instância por cena.
    /// </summary>
    public class GerenciadorMontagem : MonoBehaviour
    {
        public static GerenciadorMontagem Instancia { get; private set; }

        [Header("Etapas (na ordem recomendada de montagem)")]
        public List<PontoEncaixe> pontos = new List<PontoEncaixe>();

        [Header("Referências")]
        public PainelInstrucoes painel;
        public BotaoPower botaoPower;
        public MonitorBoot monitor;
        public RegistroSessao registro;
        public PainelPeca painelPeca;
        public MenusVR menus;
        public DicaGrip dicaGrip;
        public AudioSource fonteAudio;

        [Header("Comportamento")]
        [Tooltip("Modo guiado: mostra o destino (guia azul) da peça segurada e a dica de instalação.")]
        public bool modoGuiado = true;
        [Tooltip("Velocidade (m/s) do empurrão aplicado à peça rejeitada.")]
        public float velocidadeRepulsao = 1.0f;
        [Tooltip("Raio (m) usado para acusar 'local incorreto' quando a peça é solta perto do encaixe de OUTRA peça. " +
                 "Menor que o raio de encaixe para não punir soltas acidentais nas proximidades.")]
        public float raioErroOutroTipo = 0.04f;
        [Tooltip("Peças abaixo desta altura (m) voltam à posição inicial.")]
        public float alturaMinimaPecas = 0.4f;
        [Tooltip("Peças mais distantes que isto (m) da posição inicial voltam à bancada.")]
        public float distanciaMaximaPecas = 2.5f;

        [Header("Eventos")]
        public UnityEvent aoConcluirMontagem = new UnityEvent();
        public UnityEvent aoLigarComputador = new UnityEvent();

        // ---- Métricas da sessão (somente leitura) ----
        public int Acertos { get; private set; }
        public int ErrosLocal { get; private set; }
        public int ErrosOrdem { get; private set; }
        public int ErrosOrientacao { get; private set; }
        public int ErrosTotal => ErrosLocal + ErrosOrdem + ErrosOrientacao;
        /// <summary>Vezes que o Power foi apertado com a montagem incompleta ou incorreta.</summary>
        public int FalhasPower { get; private set; }
        /// <summary>Peças retiradas para correção (modo livre).</summary>
        public int Retiradas { get; private set; }
        public bool ModoLivre => !modoGuiado;
        public int Tentativas => Acertos + ErrosTotal;
        public bool CronometroIniciado { get; private set; }
        public bool MontagemConcluida { get; private set; }
        public bool ComputadorLigado { get; private set; }
        /// <summary>Falso enquanto o menu inicial está aberto (peças bloqueadas).</summary>
        public bool SessaoIniciada { get; private set; }
        public bool Pausado { get; private set; }
        /// <summary>Tempo de montagem em segundos (congela ao concluir a última etapa).</summary>
        public float TempoMontagem => !CronometroIniciado ? 0f : (MontagemConcluida ? _tempoFinal : TempoCorrido());

        readonly List<PecaHardware> _seguradas = new List<PecaHardware>();
        readonly Dictionary<PontoEncaixe, EstadoGuia> _estadosDesejados = new Dictionary<PontoEncaixe, EstadoGuia>();
        float _tempoInicio, _tempoFinal, _tempoPausado, _inicioPausa;
        PecaHardware[] _todasPecas = new PecaHardware[0];
        AudioClip _somAcerto, _somErro, _somConclusao;

        void Awake()
        {
            if (Instancia != null && Instancia != this)
            {
                Debug.LogWarning("[VirtuaLab] Mais de um GerenciadorMontagem na cena; mantendo o primeiro.");
                enabled = false;
                return;
            }
            Instancia = this;
            _somAcerto = CriarTom("acerto", new[] { 660f, 880f }, 0.09f);
            _somErro = CriarTom("erro", new[] { 220f, 165f }, 0.14f);
            _somConclusao = CriarTom("conclusao", new[] { 523f, 659f, 784f, 1047f }, 0.13f);
        }

        void OnDestroy()
        {
            // Sessão interrompida (reinício, troca de participante): grava o resumo parcial.
            if (Instancia == this && registro != null && CronometroIniciado && !MontagemConcluida)
                registro.GravarResumo(this, false, "INCOMPLETA");
            if (Instancia == this) Instancia = null;
        }

        void Start()
        {
            if (registro != null) registro.IniciarSessao(this);
            if (botaoPower != null) botaoPower.DefinirLiberado(false);
            _todasPecas = FindObjectsByType<PecaHardware>(FindObjectsInactive.Include);
            if (menus != null && !MenusVR.PularMenuInicial)
            {
                foreach (var p in _todasPecas) p.DefinirManipulavel(false);
                menus.MostrarInicial();
            }
            else
            {
                IniciarMontagem();
            }
            MenusVR.PularMenuInicial = false;
            AtualizarPainel();
            if (painel != null)
                painel.MostrarFeedback("Pegue uma peça da bancada para começar.", TipoFeedback.Neutro, 0f);
        }

        void Update()
        {
            AtualizarGuias();
            if (painel != null) painel.AtualizarStatus(TempoMontagem, Acertos, ErrosTotal, ContarConcluidas(), ContarObrigatorias(), modoGuiado || MontagemConcluida);
        }

        // ------------------------------------------------------------------ Entrada das peças

        /// <summary>Libera as peças para manipulação (chamado pelo botão "Iniciar montagem").</summary>
        public void IniciarMontagem()
        {
            if (SessaoIniciada) return;
            SessaoIniciada = true;
            foreach (var p in _todasPecas) if (p != null) p.DefinirManipulavel(true);
            Registrar("MONTAGEM_INICIADA", null, null, modoGuiado ? "MODO_GUIADO" : "MODO_LIVRE", "");
        }

        /// <summary>Pausa ou retoma o cronômetro (menu de pausa).</summary>
        public void DefinirPausa(bool pausar)
        {
            if (pausar == Pausado) return;
            Pausado = pausar;
            if (pausar) _inicioPausa = Time.time;
            else _tempoPausado += Time.time - _inicioPausa;
            Registrar(pausar ? "PAUSA" : "RETOMADA", null, null, "", "");
        }

        float TempoCorrido()
        {
            float pausa = _tempoPausado + (Pausado ? Time.time - _inicioPausa : 0f);
            return Time.time - _tempoInicio - pausa;
        }

        public void NotificarPecaPega(PecaHardware peca)
        {
            if (!_seguradas.Contains(peca)) _seguradas.Add(peca);
            if (!CronometroIniciado)
            {
                CronometroIniciado = true;
                _tempoInicio = Time.time;
                Registrar("INICIO_CRONOMETRO", peca, null, "", "");
            }
            Registrar("PEGOU", peca, null, "", "");
            if (painelPeca != null) painelPeca.Mostrar(peca, modoGuiado);
            if (dicaGrip != null && dicaGrip.Visivel) dicaGrip.Ocultar();
        }

        public void NotificarPecaRecuperada(PecaHardware peca)
        {
            Registrar("PECA_RECUPERADA", peca, null, "", "caiu da bancada e voltou ao início");
            if (painel != null)
                painel.MostrarFeedback($"{peca.nomeExibicao} voltou para a bancada.", TipoFeedback.Neutro);
        }

        /// <summary>
        /// Chamado um quadro após a peça ser solta. É o ponto central do validador: encaixa,
        /// rejeita com repulsão ou ignora, conforme o ponto de encaixe alvo.
        /// Público para permitir testes automatizados sem headset.
        /// </summary>
        public ResultadoEncaixe? ProcessarSoltura(PecaHardware peca)
        {
            _seguradas.Remove(peca);
            if (painelPeca != null) painelPeca.Ocultar(peca);
            if (peca == null || peca.Instalada) return null;

            PontoEncaixe alvo = EncontrarAlvo(peca);
            if (alvo == null)
            {
                Registrar("SOLTOU", peca, null, "", "fora de qualquer encaixe");
                return null;
            }

            ResultadoEncaixe resultado = alvo.Avaliar(peca, ModoLivre);
            string detalhe = $"dist={alvo.Distancia(peca):0.000}m ang={alvo.DesvioAngular(peca):0}°";
            if (ModoLivre)
            {
                ProcessarSolturaLivre(peca, alvo, resultado, detalhe);
                return resultado;
            }
            switch (resultado)
            {
                case ResultadoEncaixe.Valido:
                    Instalar(peca, alvo, detalhe);
                    break;

                case ResultadoEncaixe.ForaDeAlcance:
                    Registrar("SOLTOU", peca, alvo, "FORA_DE_ALCANCE", detalhe);
                    Feedback($"Quase! Aproxime mais {Artigo(peca)} do guia e solte quando ele ficar VERDE.", TipoFeedback.Neutro);
                    break;

                case ResultadoEncaixe.TipoIncorreto:
                    ErrosLocal++;
                    Rejeitar(peca, alvo, "ERRO_LOCAL", detalhe,
                        $"LOCAL INCORRETO: {peca.nomeExibicao} não vai em {alvo.nomeLocal}. " +
                        (string.IsNullOrEmpty(peca.explicacaoLocal) ? peca.dicaInstalacao : peca.explicacaoLocal));
                    break;

                case ResultadoEncaixe.PreRequisitoPendente:
                    ErrosOrdem++;
                    string msg = !string.IsNullOrEmpty(alvo.dicaPreRequisito)
                        ? alvo.dicaPreRequisito
                        : "Antes desta etapa: " + ListarEtapas(alvo.PreRequisitosPendentes()) + ".";
                    Rejeitar(peca, alvo, "ERRO_ORDEM", detalhe, "ORDEM INCORRETA: " + msg);
                    break;

                case ResultadoEncaixe.OrientacaoIncorreta:
                    ErrosOrientacao++;
                    Rejeitar(peca, alvo, "ERRO_ORIENTACAO", detalhe,
                        $"ORIENTAÇÃO INCORRETA: gire {Artigo(peca)} até ficar igual ao guia e tente de novo.");
                    break;
            }
            return resultado;
        }

        /// <summary>
        /// Modo livre ("demonstrar o que aprendeu"): sem guia, sem repulsão e sem mensagem dizendo o que está
        /// errado. Como numa bancada real, a peça só encaixa se couber; se não couber, simplesmente fica solta.
        /// Os erros são contados no registro. Problemas que "cabem" (ex.: cooler sem pasta) só aparecem ao ligar.
        /// </summary>
        void ProcessarSolturaLivre(PecaHardware peca, PontoEncaixe alvo, ResultadoEncaixe resultado, string detalhe)
        {
            switch (resultado)
            {
                case ResultadoEncaixe.Valido:
                    Instalar(peca, alvo, detalhe);
                    return;
                case ResultadoEncaixe.ForaDeAlcance:
                    Registrar("SOLTOU", peca, alvo, "FORA_DE_ALCANCE", detalhe);
                    return;
                case ResultadoEncaixe.TipoIncorreto: ErrosLocal++; Registrar("ERRO_LOCAL", peca, alvo, "ERRO_LOCAL", detalhe + " (modo livre)"); break;
                case ResultadoEncaixe.PreRequisitoPendente: ErrosOrdem++; Registrar("ERRO_ORDEM", peca, alvo, "ERRO_ORDEM", detalhe + " (modo livre)"); break;
                case ResultadoEncaixe.OrientacaoIncorreta: ErrosOrientacao++; Registrar("ERRO_ORIENTACAO", peca, alvo, "ERRO_ORIENTACAO", detalhe + " (modo livre)"); break;
            }
            Feedback("A peça não encaixou nessa posição.", TipoFeedback.Neutro, 3f);
        }

        /// <summary>Peça retirada de um encaixe (modo livre).</summary>
        public void NotificarRetirada(PecaHardware peca, PontoEncaixe ponto)
        {
            Retiradas++;
            ponto.Desfazer();
            Registrar("RETIROU", peca, ponto, "", "peça retirada para correção");
            AtualizarRetiraveis();
            AtualizarPainel();
        }

        /// <summary>
        /// No modo livre as peças instaladas podem ser retiradas para corrigir a montagem, exceto a placa-mãe
        /// e peças que ficam presas debaixo de outra (processador com pasta ou cooler em cima).
        /// </summary>
        void AtualizarRetiraveis()
        {
            foreach (var peca in _todasPecas)
            {
                if (peca == null) continue;
                bool retiravel = ModoLivre && !MontagemConcluida && peca.tipo != TipoComponente.PlacaMae;
                if (retiravel && peca.Instalada && peca.PontoAtual != null)
                    foreach (var p in pontos)
                        if (p != null && p.Concluido && p.requisitosFisicos.Contains(peca.PontoAtual))
                            retiravel = false;
                if (retiravel && peca.tipo == TipoComponente.Processador)
                    foreach (var p in pontos)
                        if (p != null && p.Concluido && (p.tipoAceito == TipoComponente.Cooler || p.tipoAceito == TipoComponente.PastaTermica))
                            retiravel = false;
                peca.DefinirRetiravel(retiravel);
            }
        }

        /// <summary>
        /// Escolhe o ponto de encaixe ao qual a peça se refere:
        /// 1) o ponto livre compatível mais próximo dentro do raio de detecção;
        /// 2) senão, o ponto livre (de outro tipo) mais próximo dentro do raio de encaixe dele.
        /// A prioridade 1 evita falsos erros quando encaixes vizinhos se sobrepõem.
        /// </summary>
        public PontoEncaixe EncontrarAlvo(PecaHardware peca)
        {
            PontoEncaixe melhor = null;
            float melhorDist = float.MaxValue;
            foreach (var p in pontos)
            {
                if (!PontoElegivel(p, peca) || p.tipoAceito != peca.tipo) continue;
                float d = p.Distancia(peca);
                if (d <= p.raioDeteccao && d < melhorDist) { melhor = p; melhorDist = d; }
            }
            if (melhor != null) return melhor;

            foreach (var p in pontos)
            {
                if (!PontoElegivel(p, peca) || p.tipoAceito == peca.tipo) continue;
                float d = p.Distancia(peca);
                if (d <= Mathf.Min(p.raioEncaixe, raioErroOutroTipo) && d < melhorDist) { melhor = p; melhorDist = d; }
            }
            return melhor;
        }

        /// <summary>
        /// Ponto livre, ativo e que NÃO pertence à própria peça. Sem a última condição a placa-mãe
        /// seria validada contra os encaixes que ficam sobre ela (falso erro de local).
        /// </summary>
        static bool PontoElegivel(PontoEncaixe p, PecaHardware peca)
        {
            return p != null && !p.Concluido && p.isActiveAndEnabled && !p.transform.IsChildOf(peca.transform);
        }

        void Instalar(PecaHardware peca, PontoEncaixe alvo, string detalhe)
        {
            Acertos++;
            // Modo livre: etapas pedagógicas que ainda faltavam viram "problema" (ex.: cooler sem pasta).
            var pendentes = ModoLivre ? alvo.PreRequisitosPendentes() : new List<PontoEncaixe>();
            alvo.Concluir(peca);
            if (pendentes.Count > 0)
            {
                var nomes = new List<string>();
                foreach (var p in pendentes) nomes.Add(NomeComponente(p.tipoAceito).ToLowerInvariant());
                alvo.Problema = "instalado(a) sem " + string.Join(" e ", nomes);
            }
            if (alvo.modoAplicacao) peca.RetornarAoInicio();
            else peca.FixarEm(alvo);
            peca.Vibrar(0.6f, 0.12f);
            Tocar(_somAcerto);
            Registrar("ENCAIXE_OK", peca, alvo, string.IsNullOrEmpty(alvo.Problema) ? "VALIDO" : "COM_PROBLEMA", detalhe + " " + alvo.Problema);
            if (ModoLivre)
                Feedback(alvo.modoAplicacao ? $"{peca.nomeExibicao} aplicada." : $"{peca.nomeExibicao} encaixada.", TipoFeedback.Neutro, 3f);
            else
                Feedback(alvo.modoAplicacao
                    ? $"{peca.nomeExibicao} aplicada corretamente."
                    : $"Peça instalada corretamente: {peca.nomeExibicao}.", TipoFeedback.Sucesso);
            AtualizarRetiraveis();
            AtualizarPainel();
            // No modo livre a montagem só é dada como concluída quando o participante aperta o Power.
            if (!ModoLivre) VerificarConclusao();
        }

        void Rejeitar(PecaHardware peca, PontoEncaixe alvo, string codigo, string detalhe, string mensagem)
        {
            alvo.DefinirGuia(EstadoGuia.Oculto);
            peca.Repelir(alvo.CentroZonaMundo, velocidadeRepulsao);
            peca.Vibrar(1.0f, 0.25f);
            Tocar(_somErro);
            Registrar(codigo, peca, alvo, codigo, detalhe);
            Feedback(mensagem, TipoFeedback.Erro);
        }

        // ------------------------------------------------------------------ Conclusão e Power

        /// <summary>Todas as etapas obrigatórias feitas e nenhuma com problema.</summary>
        public bool MontagemCorreta()
        {
            foreach (var p in pontos)
                if (p != null && p.obrigatorio && (!p.Concluido || !string.IsNullOrEmpty(p.Problema))) return false;
            return true;
        }

        void VerificarConclusao()
        {
            if (MontagemConcluida || !MontagemCorreta()) return;

            MontagemConcluida = true;
            _tempoFinal = TempoCorrido();
            Tocar(_somConclusao);
            Registrar("MONTAGEM_CONCLUIDA", null, null, "", $"tempo={_tempoFinal:0.0}s acertos={Acertos} erros={ErrosTotal}");
            if (botaoPower != null) botaoPower.DefinirLiberado(true);
            AtualizarRetiraveis();
            if (painel != null) painel.MostrarConclusao(_tempoFinal, Acertos, ErrosTotal);
            Feedback("MONTAGEM CONCLUÍDA! Aponte para o botão POWER do gabinete e segure o botão lateral (grip) para ligar. Menu: botão B.", TipoFeedback.Sucesso, 0f);
            if (registro != null) registro.GravarResumo(this, false);
            aoConcluirMontagem.Invoke();
        }

        /// <summary>Chamado pelo botão Power (ou pela tecla P do pesquisador).</summary>
        public void TentarLigar()
        {
            if (ComputadorLigado) return;
            // Modo livre: o Power é a verificação da montagem.
            if (!MontagemConcluida && MontagemCorreta()) VerificarConclusao();
            if (!MontagemConcluida)
            {
                FalhasPower++;
                Tocar(_somErro);
                string diagnostico = Diagnostico(out string faltando);
                Registrar("POWER_FALHA", null, null, "", "problemas: " + faltando);
                if (monitor != null) monitor.MostrarFalha(diagnostico);
                Feedback("FALHA NA INICIALIZAÇÃO: " + faltando + ". Veja o monitor" +
                         (ModoLivre ? " e corrija: no modo livre dá para retirar uma peça pegando-a de novo." : "."), TipoFeedback.Erro);
                return;
            }
            ComputadorLigado = true;
            Registrar("POWER_LIGADO", null, null, "", "");
            if (botaoPower != null) botaoPower.DefinirLigado();
            if (monitor != null) monitor.IniciarBoot(_tempoFinal, Acertos, ErrosTotal);
            Feedback("Computador ligado! Observe o monitor.", TipoFeedback.Sucesso, 0f);
            if (registro != null) registro.GravarResumo(this, true);
            aoLigarComputador.Invoke();
        }

        /// <summary>
        /// Lista, para a tela de falha, o que está instalado e o que falta.
        /// </summary>
        public string Diagnostico(out string faltando)
        {
            var sb = new StringBuilder();
            var falta = new List<string>();
            foreach (var p in pontos)
            {
                if (p == null || !p.obrigatorio) continue;
                string nome = NomeComponente(p.tipoAceito);
                if (p.Concluido && string.IsNullOrEmpty(p.Problema))
                    sb.AppendLine($"<color=#6BFF7A>OK</color>  {nome}: {(p.modoAplicacao ? "aplicada" : "instalado(a)")}");
                else if (p.Concluido)
                {
                    sb.AppendLine($"<color=#FFB347>!</color>  {nome}: {p.Problema}");
                    falta.Add($"{nome.ToLowerInvariant()} {p.Problema}");
                }
                else
                {
                    sb.AppendLine($"<color=#FF6B5E>X</color>  {nome}: {(p.modoAplicacao ? "não aplicada" : "ausente")}");
                    falta.Add(nome.ToLowerInvariant() + (p.modoAplicacao ? " não aplicada" : " ausente"));
                }
            }
            faltando = falta.Count == 0 ? "nenhum problema" : string.Join("; ", falta);
            return "<size=75%>" + sb.ToString().TrimEnd('\n') + "</size>";
        }

        public static string NomeComponente(TipoComponente t)
        {
            switch (t)
            {
                case TipoComponente.PlacaMae: return "Placa-mãe";
                case TipoComponente.Processador: return "Processador";
                case TipoComponente.PastaTermica: return "Pasta térmica";
                case TipoComponente.Cooler: return "Cooler";
                case TipoComponente.MemoriaRAM: return "Memória RAM";
                case TipoComponente.SSD: return "SSD";
                case TipoComponente.PlacaDeVideo: return "Placa de vídeo";
                case TipoComponente.Fonte: return "Fonte";
                default: return t.ToString();
            }
        }

        // ------------------------------------------------------------------ Guias visuais

        void AtualizarGuias()
        {
            _estadosDesejados.Clear();
            if (ModoLivre)
            {
                // Modo livre: nenhum indicador (azul, verde ou vermelho) antes de o participante decidir.
                foreach (var p in pontos) if (p != null) p.DefinirGuia(EstadoGuia.Oculto);
                return;
            }
            for (int i = _seguradas.Count - 1; i >= 0; i--)
            {
                var peca = _seguradas[i];
                if (peca == null || !peca.Segurada || peca.Instalada) { _seguradas.RemoveAt(i); continue; }

                if (modoGuiado)
                {
                    // Destino sugerido: todo ponto livre compatível com pré-requisitos atendidos.
                    foreach (var p in pontos)
                        if (p != null && !p.Concluido && p.tipoAceito == peca.tipo && p.PreRequisitosAtendidos())
                            Desejar(p, EstadoGuia.Destino);
                }

                var alvo = EncontrarAlvo(peca);
                if (alvo == null) continue;
                switch (alvo.Avaliar(peca, false))
                {
                    case ResultadoEncaixe.Valido: Desejar(alvo, EstadoGuia.Valido); break;
                    case ResultadoEncaixe.ForaDeAlcance: Desejar(alvo, EstadoGuia.Destino); break;
                    default: Desejar(alvo, EstadoGuia.Invalido); break;
                }
            }
            foreach (var p in pontos)
            {
                if (p == null) continue;
                p.DefinirGuia(_estadosDesejados.TryGetValue(p, out var e) ? e : EstadoGuia.Oculto);
            }
        }

        void Desejar(PontoEncaixe p, EstadoGuia estado)
        {
            // Estados mais fortes (Valido/Invalido) prevalecem sobre Destino.
            if (!_estadosDesejados.TryGetValue(p, out var atual) || (int)estado > (int)atual)
                _estadosDesejados[p] = estado;
        }

        // ------------------------------------------------------------------ Apoio

        public int ContarConcluidas()
        {
            int n = 0;
            foreach (var p in pontos) if (p != null && p.obrigatorio && p.Concluido) n++;
            return n;
        }

        public int ContarObrigatorias()
        {
            int n = 0;
            foreach (var p in pontos) if (p != null && p.obrigatorio) n++;
            return n;
        }

        /// <summary>Próxima etapa sugerida: primeira não concluída cujos pré-requisitos estão atendidos.</summary>
        public PontoEncaixe ProximaEtapaSugerida()
        {
            foreach (var p in pontos)
                if (p != null && !p.Concluido && p.PreRequisitosAtendidos()) return p;
            return null;
        }

        public void AlternarModoGuiado()
        {
            modoGuiado = !modoGuiado;
            Registrar("MODO_GUIADO", null, null, modoGuiado ? "LIGADO" : "DESLIGADO", "");
            AtualizarRetiraveis();
            AtualizarPainel();
        }

        void AtualizarPainel()
        {
            if (painel != null) painel.AtualizarEtapas(pontos, modoGuiado ? ProximaEtapaSugerida() : null, modoGuiado);
        }

        void Feedback(string mensagem, TipoFeedback tipo, float duracao = 8f)
        {
            if (painel != null) painel.MostrarFeedback(mensagem, tipo, duracao);
            Debug.Log("[VirtuaLab] " + mensagem);
        }

        void Registrar(string evento, PecaHardware peca, PontoEncaixe ponto, string resultado, string detalhe)
        {
            if (registro != null)
                registro.RegistrarEvento(TempoMontagem, evento, peca != null ? peca.nomeExibicao : "", ponto != null ? ponto.nomeLocal : "", resultado, detalhe);
        }

        static string Artigo(PecaHardware peca) => "a peça (" + peca.nomeExibicao + ")";

        static string ListarEtapas(List<PontoEncaixe> etapas)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < etapas.Count; i++)
            {
                if (i > 0) sb.Append(i == etapas.Count - 1 ? " e " : ", ");
                sb.Append(etapas[i].nomeEtapa.ToLowerInvariant());
            }
            return sb.ToString();
        }

        void Tocar(AudioClip clip)
        {
            if (fonteAudio != null && clip != null) fonteAudio.PlayOneShot(clip, 0.6f);
        }

        /// <summary>Gera um bipe simples (sequência de notas senoidais) sem depender de arquivos de áudio.</summary>
        static AudioClip CriarTom(string nome, float[] frequencias, float duracaoNota)
        {
            const int taxa = 44100;
            int porNota = Mathf.CeilToInt(taxa * duracaoNota);
            var dados = new float[porNota * frequencias.Length];
            for (int n = 0; n < frequencias.Length; n++)
                for (int i = 0; i < porNota; i++)
                {
                    float t = i / (float)taxa;
                    float envelope = Mathf.Clamp01(i / 200f) * Mathf.Clamp01((porNota - i) / 800f);
                    dados[n * porNota + i] = Mathf.Sin(2f * Mathf.PI * frequencias[n] * t) * envelope * 0.5f;
                }
            var clip = AudioClip.Create("VirtuaLab_" + nome, dados.Length, 1, taxa, false);
            clip.SetData(dados, 0);
            return clip;
        }
    }
}
