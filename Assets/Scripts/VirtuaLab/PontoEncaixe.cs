using System.Collections.Generic;
using UnityEngine;

namespace VirtuaLab
{
    /// <summary>
    /// Ponto de encaixe ("Snap Validator") de UMA peça. Fica na posição/rotação exatas em que o
    /// pivô da peça deve terminar. Não usa colisores de gatilho: a detecção é feita por distância
    /// entre o ponto de referência da peça e o centro da zona, o que torna a tolerância explícita
    /// e calibrável pelo Inspector (campos raioDeteccao / raioEncaixe / toleranciaAngular).
    /// A decisão de encaixar ou rejeitar é tomada pelo <see cref="GerenciadorMontagem"/>.
    /// </summary>
    public class PontoEncaixe : MonoBehaviour
    {
        [Header("Identificação")]
        [Tooltip("Texto da etapa exibido no painel. Ex.: 'Instalar o processador'")]
        public string nomeEtapa = "Instalar peça";
        [Tooltip("Nome do local, usado nas mensagens. Ex.: 'soquete da placa-mãe'")]
        public string nomeLocal = "encaixe";
        public TipoComponente tipoAceito;
        [Tooltip("Se falso, a etapa não é exigida para concluir a montagem.")]
        public bool obrigatorio = true;

        [Header("Calibração (metros / graus)")]
        [Tooltip("Distância a partir da qual o guia do encaixe aparece para a peça compatível.")]
        public float raioDeteccao = 0.15f;
        [Tooltip("Tolerância de posição: soltar a peça dentro deste raio realiza o encaixe.")]
        public float raioEncaixe = 0.06f;
        [Tooltip("Desvio máximo de rotação aceito. 0 desativa a verificação de orientação.")]
        public float toleranciaAngular = 70f;
        [Tooltip("Aceita também a peça girada 180° em torno do eixo de inserção (eixo Y local do ponto).")]
        public bool aceitaGiro180 = false;
        [Tooltip("Centro da zona de encaixe no espaço local deste ponto (centro esperado da peça instalada).")]
        public Vector3 centroZonaLocal = Vector3.zero;

        [Header("Ordem de montagem")]
        [Tooltip("Etapas que precisam estar concluídas antes desta.")]
        public List<PontoEncaixe> preRequisitos = new List<PontoEncaixe>();
        [TextArea] public string dicaPreRequisito = "";
        [Tooltip("Etapas que precisam estar feitas para a peça CABER fisicamente (valem também no modo livre). " +
                 "Ex.: a pasta térmica precisa do processador instalado.")]
        public List<PontoEncaixe> requisitosFisicos = new List<PontoEncaixe>();
        [Tooltip("Se alguma destas etapas estiver feita, esta peça não cabe mais. Ex.: com o cooler instalado " +
                 "não dá para colocar o processador nem aplicar a pasta.")]
        public List<PontoEncaixe> bloqueadoPor = new List<PontoEncaixe>();

        [Header("Modo aplicação (ex.: pasta térmica)")]
        [Tooltip("Se verdadeiro, a peça não fica presa: a etapa é concluída e a peça volta à bancada.")]
        public bool modoAplicacao = false;
        [Tooltip("Objeto ativado quando a aplicação é concluída (ex.: gota de pasta sobre a CPU).")]
        public GameObject visualAplicado;

        [Header("Guia visual (criado pelo construtor de cena)")]
        public GameObject guia;

        public bool Concluido { get; private set; }
        /// <summary>
        /// Modo livre: problema da instalação percebido só ao ligar (ex.: "instalado sem pasta térmica").
        /// Vazio = instalação correta.
        /// </summary>
        public string Problema { get; set; } = "";
        public PecaHardware PecaInstalada { get; private set; }
        public EstadoGuia EstadoAtual { get; private set; } = EstadoGuia.Oculto;

        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        static readonly Color CorDestino = new Color(0.25f, 0.65f, 1f, 0.30f);
        static readonly Color CorValido = new Color(0.10f, 1f, 0.25f, 0.55f);
        static readonly Color CorInvalido = new Color(1f, 0.12f, 0.10f, 0.55f);

        Renderer[] _renderersGuia;
        MaterialPropertyBlock _bloco;

        public Vector3 CentroZonaMundo => transform.TransformPoint(centroZonaLocal);

        void Awake()
        {
            if (guia != null)
            {
                _renderersGuia = guia.GetComponentsInChildren<Renderer>(true);
                guia.SetActive(false);
            }
            if (visualAplicado != null) visualAplicado.SetActive(false);
            _bloco = new MaterialPropertyBlock();
        }

        /// <summary>Pré-requisitos ainda não concluídos (lista vazia = liberado).</summary>
        public List<PontoEncaixe> PreRequisitosPendentes()
        {
            var pendentes = new List<PontoEncaixe>();
            foreach (var p in preRequisitos)
                if (p != null && !p.Concluido) pendentes.Add(p);
            return pendentes;
        }

        public bool PreRequisitosAtendidos()
        {
            foreach (var p in preRequisitos)
                if (p != null && !p.Concluido) return false;
            return true;
        }

        public float Distancia(PecaHardware peca)
        {
            return Vector3.Distance(peca.PontoReferenciaMundo, CentroZonaMundo);
        }

        /// <summary>Desvio angular (graus) entre a rotação da peça e a rotação de encaixe.</summary>
        public float DesvioAngular(PecaHardware peca)
        {
            float a = Quaternion.Angle(peca.transform.rotation, transform.rotation);
            if (aceitaGiro180)
            {
                float b = Quaternion.Angle(peca.transform.rotation, transform.rotation * Quaternion.Euler(0f, 180f, 0f));
                a = Mathf.Min(a, b);
            }
            return a;
        }

        /// <summary>
        /// Valida a peça contra este ponto. Ordem das verificações: tipo, distância,
        /// pré-requisitos, orientação (erros só ocorrem dentro do raio de encaixe). Não altera estado.
        /// </summary>
        public ResultadoEncaixe Avaliar(PecaHardware peca) => Avaliar(peca, false);

        /// <summary>
        /// Valida a peça. No modo livre a ordem "pedagógica" (preRequisitos) não bloqueia: só valem
        /// as restrições físicas (requisitosFisicos e bloqueadoPor). Erros de ordem que ainda "cabem"
        /// (ex.: cooler sem pasta) são aceitos e aparecem no diagnóstico ao ligar.
        /// </summary>
        public ResultadoEncaixe Avaliar(PecaHardware peca, bool modoLivre)
        {
            if (modoLivre)
            {
                if (peca.tipo != tipoAceito) return ResultadoEncaixe.TipoIncorreto;
                if (Distancia(peca) > raioEncaixe) return ResultadoEncaixe.ForaDeAlcance;
                if (!CabeFisicamente()) return ResultadoEncaixe.PreRequisitoPendente;
                if (toleranciaAngular > 0f && DesvioAngular(peca) > toleranciaAngular) return ResultadoEncaixe.OrientacaoIncorreta;
                return ResultadoEncaixe.Valido;
            }
            if (peca.tipo != tipoAceito) return ResultadoEncaixe.TipoIncorreto;
            if (Distancia(peca) > raioEncaixe) return ResultadoEncaixe.ForaDeAlcance;
            if (!PreRequisitosAtendidos() || !CabeFisicamente()) return ResultadoEncaixe.PreRequisitoPendente;
            if (toleranciaAngular > 0f && DesvioAngular(peca) > toleranciaAngular) return ResultadoEncaixe.OrientacaoIncorreta;
            return ResultadoEncaixe.Valido;
        }

        public bool CabeFisicamente()
        {
            foreach (var p in requisitosFisicos) if (p != null && !p.Concluido) return false;
            foreach (var p in bloqueadoPor) if (p != null && p.Concluido) return false;
            return true;
        }

        /// <summary>Desfaz a etapa (peça retirada no modo livre).</summary>
        public void Desfazer()
        {
            Concluido = false;
            PecaInstalada = null;
            Problema = "";
            if (visualAplicado != null) visualAplicado.SetActive(false);
        }

        /// <summary>Marca a etapa como concluída. Chamado apenas pelo GerenciadorMontagem.</summary>
        public void Concluir(PecaHardware peca)
        {
            Concluido = true;
            PecaInstalada = modoAplicacao ? null : peca;
            if (visualAplicado != null) visualAplicado.SetActive(true);
            DefinirGuia(EstadoGuia.Oculto);
        }

        public void DefinirGuia(EstadoGuia estado)
        {
            if (estado == EstadoAtual) return;
            EstadoAtual = estado;
            if (guia == null) return;
            if (estado == EstadoGuia.Oculto || Concluido)
            {
                guia.SetActive(false);
                return;
            }
            Color cor = estado == EstadoGuia.Valido ? CorValido : (estado == EstadoGuia.Invalido ? CorInvalido : CorDestino);
            if (_renderersGuia == null) _renderersGuia = guia.GetComponentsInChildren<Renderer>(true);
            if (_bloco == null) _bloco = new MaterialPropertyBlock();
            foreach (var r in _renderersGuia)
            {
                r.GetPropertyBlock(_bloco);
                _bloco.SetColor(BaseColorId, cor);
                r.SetPropertyBlock(_bloco);
            }
            guia.SetActive(true);
        }

#if UNITY_EDITOR
        void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(0.2f, 0.6f, 1f, 0.6f);
            Gizmos.DrawWireSphere(CentroZonaMundo, raioDeteccao);
            Gizmos.color = new Color(0.1f, 1f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(CentroZonaMundo, raioEncaixe);
            Gizmos.color = Color.yellow;
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.05f);
        }
#endif
    }
}
