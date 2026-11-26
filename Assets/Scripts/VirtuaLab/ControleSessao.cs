using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace VirtuaLab
{
    /// <summary>
    /// Controles do PESQUISADOR pelo teclado do PC (janela Game com foco), para conduzir as
    /// sessões de teste sem tirar o headset do participante:
    ///   N = próximo participante (começa na FAMILIARIZAÇÃO, tentativa 0, não avaliada)
    ///   R = reiniciar a cena como nova tentativa do mesmo participante (T1, T2, ...)
    ///   G = alternar modo guiado/livre                         P = acionar Power (contingência)
    ///   PageUp/PageDown = subir/descer o usuário 2 cm          Setas = mover o usuário 5 cm
    ///   Home = desfazer os ajustes de posição                  H = mostrar/ocultar este quadro
    /// O quadro de status aparece apenas no monitor do PC (OnGUI), não dentro do headset.
    /// </summary>
    public class ControleSessao : MonoBehaviour
    {
        [Tooltip("Raiz do XR Origin, movida pelos ajustes de altura/posição.")]
        public Transform rigXR;
        public GerenciadorMontagem gerenciador;
        public RegistroSessao registro;
        public bool mostrarQuadro = true;

        // Ajuste de posição preservado entre reinícios da cena.
        static Vector3 _ajusteGlobal;
        // Modo guiado/livre escolhido pelo pesquisador, preservado entre reinícios (null = padrão da cena).
        static bool? _modoGuiadoGlobal;
        Vector3 _posicaoBase;
        GUIStyle _estilo;

        void Awake()
        {
            // Em Awake (antes de qualquer Start) para o registro da sessão já sair com o modo certo.
            if (gerenciador != null && _modoGuiadoGlobal.HasValue) gerenciador.modoGuiado = _modoGuiadoGlobal.Value;
        }

        void Start()
        {
            if (rigXR != null)
            {
                _posicaoBase = rigXR.position;
                rigXR.position = _posicaoBase + _ajusteGlobal;
            }
        }

        void Update()
        {
            var k = Keyboard.current;
            if (k == null) return;

            if (k.rKey.wasPressedThisFrame) Reiniciar(false);
            if (k.nKey.wasPressedThisFrame) Reiniciar(true);
            if (k.gKey.wasPressedThisFrame && gerenciador != null) { gerenciador.AlternarModoGuiado(); _modoGuiadoGlobal = gerenciador.modoGuiado; }
            if (k.pKey.wasPressedThisFrame && gerenciador != null) gerenciador.TentarLigar();
            if (k.hKey.wasPressedThisFrame) mostrarQuadro = !mostrarQuadro;

            Vector3 d = Vector3.zero;
            if (k.pageUpKey.wasPressedThisFrame) d.y += 0.02f;
            if (k.pageDownKey.wasPressedThisFrame) d.y -= 0.02f;
            if (k.upArrowKey.wasPressedThisFrame) d.z += 0.05f;
            if (k.downArrowKey.wasPressedThisFrame) d.z -= 0.05f;
            if (k.rightArrowKey.wasPressedThisFrame) d.x += 0.05f;
            if (k.leftArrowKey.wasPressedThisFrame) d.x -= 0.05f;
            if (k.homeKey.wasPressedThisFrame) { _ajusteGlobal = Vector3.zero; d = Vector3.zero; AplicarAjuste(); }
            if (d != Vector3.zero) { _ajusteGlobal += d; AplicarAjuste(); }
        }

        void AplicarAjuste()
        {
            if (rigXR != null) rigXR.position = _posicaoBase + _ajusteGlobal;
        }

        void Reiniciar(bool proximoParticipante) => ReiniciarCena(proximoParticipante);

        /// <summary>Memoriza o modo guiado/livre para as próximas cenas (usado pelo menu VR).</summary>
        public static void LembrarModo(bool guiado) => _modoGuiadoGlobal = guiado;

        /// <summary>Passa ao próximo código de participante SEM recarregar a cena (menu inicial).</summary>
        public static void AvancarParticipante(RegistroSessao registro)
        {
            RegistroSessao.ParticipanteGlobal = ProximoCodigo(RegistroSessao.ParticipanteGlobal);
            RegistroSessao.TentativaGlobal = 0;
            _modoGuiadoGlobal = true;
            if (registro != null) registro.TrocarParticipante(RegistroSessao.ParticipanteGlobal, 0);
        }

        /// <summary>Recarrega a cena: nova tentativa do mesmo participante ou próximo participante.</summary>
        public static void ReiniciarCena(bool proximoParticipante)
        {
            if (proximoParticipante)
            {
                RegistroSessao.ParticipanteGlobal = ProximoCodigo(RegistroSessao.ParticipanteGlobal);
                RegistroSessao.TentativaGlobal = 0;   // familiarização
                _modoGuiadoGlobal = true;             // todo participante começa no modo guiado
            }
            else
            {
                RegistroSessao.TentativaGlobal = Mathf.Max(0, RegistroSessao.TentativaGlobal) + 1;
            }
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex >= 0
                ? SceneManager.GetActiveScene().buildIndex : 0);
        }

        /// <summary>P00 → P01 → P02 ... (mantém o prefixo não numérico).</summary>
        public static string ProximoCodigo(string atual)
        {
            if (string.IsNullOrEmpty(atual)) return "P01";
            int i = atual.Length;
            while (i > 0 && char.IsDigit(atual[i - 1])) i--;
            string prefixo = atual.Substring(0, i);
            string digitos = atual.Substring(i);
            int n = digitos.Length > 0 ? int.Parse(digitos) : 0;
            return prefixo + (n + 1).ToString(new string('0', Mathf.Max(2, digitos.Length)));
        }

        void OnGUI()
        {
            if (!mostrarQuadro || gerenciador == null) return;
            if (_estilo == null)
            {
                _estilo = new GUIStyle(GUI.skin.box) { alignment = TextAnchor.UpperLeft, fontSize = 15, richText = true };
                _estilo.normal.textColor = Color.white;
            }
            string part = registro != null ? $"{registro.codigoParticipante}  " + (registro.tentativa == 0 ? "FAMILIARIZAÇÃO (não avaliada)" : "tentativa " + registro.tentativa) : "-";
            string texto =
                $"<b>VirtuaLab VR — pesquisador</b>\n" +
                $"Participante: {part}\n" +
                $"Modo: {(gerenciador.modoGuiado ? "GUIADO" : "LIVRE")}\n" +
                $"Tempo: {PainelInstrucoes.FormatarTempo(gerenciador.TempoMontagem)}   Etapas: {gerenciador.ContarConcluidas()}/{gerenciador.ContarObrigatorias()}\n" +
                $"Acertos: {gerenciador.Acertos}   Erros: {gerenciador.ErrosTotal} (local {gerenciador.ErrosLocal}, ordem {gerenciador.ErrosOrdem}, orientação {gerenciador.ErrosOrientacao})\n" +
                $"Ajuste do usuário: {_ajusteGlobal.x:+0.00;-0.00} / {_ajusteGlobal.y:+0.00;-0.00} / {_ajusteGlobal.z:+0.00;-0.00} m\n" +
                "R reiniciar | N próximo participante | G guiado/livre | P power\n" +
                "PgUp/PgDn altura | Setas posição | Home zera | H oculta";
            GUI.Box(new Rect(10, 10, 560, 190), texto, _estilo);
        }
    }
}
