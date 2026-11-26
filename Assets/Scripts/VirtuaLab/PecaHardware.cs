using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VirtuaLab
{
    /// <summary>
    /// Peça de hardware manipulável. Guarda a identidade (tipo) e o conteúdo educativo da peça e
    /// avisa o <see cref="GerenciadorMontagem"/> quando é pega ou solta. Não decide encaixes.
    /// Requer XRGrabInteractable + Rigidbody + ao menos um Collider no mesmo objeto.
    /// </summary>
    [RequireComponent(typeof(XRGrabInteractable))]
    public class PecaHardware : MonoBehaviour
    {
        [Header("Identificação")]
        public TipoComponente tipo;
        public string nomeExibicao = "Peça";
        [TextArea] public string descricao = "";
        [TextArea] public string dicaInstalacao = "";
        [Tooltip("Onde a peça vai e por quê. Usado no painel da mão e na mensagem de local incorreto.")]
        [TextArea] public string explicacaoLocal = "";
        [Tooltip("Cuidado importante ao manusear a peça (mostrado no painel da mão).")]
        [TextArea] public string cuidado = "";

        [Header("Encaixe")]
        [Tooltip("Ponto da peça (espaço local) comparado com o centro da zona de encaixe. " +
                 "Normalmente o centro do volume da peça; na pasta térmica é a ponta da seringa.")]
        public Vector3 pontoReferenciaLocal = Vector3.zero;

        public bool Instalada { get; private set; }
        public bool Segurada { get; private set; }
        public PontoEncaixe PontoAtual { get; private set; }
        /// <summary>Último interator (mão/controle) que segurou a peça; usado para vibração.</summary>
        public IXRSelectInteractor UltimoInterator { get; private set; }

        public Vector3 PontoReferenciaMundo => transform.TransformPoint(pontoReferenciaLocal);

        XRGrabInteractable _grab;
        Rigidbody _rb;
        Vector3 _posicaoInicial;
        Quaternion _rotacaoInicial;
        Transform _paiInicial;
        bool _retiradaNestaPegada;
        bool _permiteRetirar;

        void Awake()
        {
            _grab = GetComponent<XRGrabInteractable>();
            _rb = GetComponent<Rigidbody>();
            _posicaoInicial = transform.position;
            _rotacaoInicial = transform.rotation;
            _paiInicial = transform.parent;
        }

        void OnEnable()
        {
            _grab.selectEntered.AddListener(AoPegar);
            _grab.selectExited.AddListener(AoSoltar);
        }

        void OnDisable()
        {
            _grab.selectEntered.RemoveListener(AoPegar);
            _grab.selectExited.RemoveListener(AoSoltar);
        }

        void AoPegar(SelectEnterEventArgs args)
        {
            // Modo livre: pegar uma peça já instalada a retira do encaixe para corrigir a montagem.
            if (Instalada) RemoverDoEncaixe();
            Segurada = true;
            UltimoInterator = args.interactorObject;
            if (GerenciadorMontagem.Instancia != null) GerenciadorMontagem.Instancia.NotificarPecaPega(this);
        }

        void AoSoltar(SelectExitEventArgs args)
        {
            // Trocar de mão dispara exit+enter; só tratamos como soltura quando ninguém mais segura.
            if (_grab.isSelected) return;
            Segurada = false;
            if (isActiveAndEnabled) StartCoroutine(SolturaAdiada());
        }

        IEnumerator SolturaAdiada()
        {
            // Aguarda um quadro: o XRGrabInteractable restaura o Rigidbody (Drop/Detach) depois do
            // evento selectExited; validar antes disso faria a peça encaixada voltar a ser dinâmica.
            yield return null;
            if (_retiradaNestaPegada && !Segurada)
            {
                // O XR Toolkit devolve a peça ao "pai" e ao estado cinemático que ela tinha ao ser pega
                // (o encaixe). Aqui ela volta a ser uma peça solta normal, com gravidade.
                _retiradaNestaPegada = false;
                transform.SetParent(_paiInicial, true);
                _rb.isKinematic = false;
                _rb.useGravity = true;
            }
            if (!Segurada && !Instalada && GerenciadorMontagem.Instancia != null)
                GerenciadorMontagem.Instancia.ProcessarSoltura(this);
        }

        void Update()
        {
            if (Instalada || Segurada) return;
            var g = GerenciadorMontagem.Instancia;
            if (g == null) return;
            // Rede de segurança: peça que caiu da bancada ou foi arremessada volta ao lugar inicial.
            if (transform.position.y < g.alturaMinimaPecas ||
                (transform.position - _posicaoInicial).sqrMagnitude > g.distanciaMaximaPecas * g.distanciaMaximaPecas)
            {
                RetornarAoInicio();
                g.NotificarPecaRecuperada(this);
            }
        }

        /// <summary>Liga/desliga a possibilidade de pegar a peça (ex.: bloqueada até o menu inicial).</summary>
        public void DefinirManipulavel(bool manipulavel)
        {
            if (Instalada || _grab == null) return;
            if (!manipulavel && _grab.isSelected && _grab.interactionManager != null)
                _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
            _grab.enabled = manipulavel;
        }

        /// <summary>Liga/desliga a retirada de uma peça instalada (modo livre).</summary>
        public void DefinirRetiravel(bool retiravel)
        {
            _permiteRetirar = retiravel;
            if (Instalada && _grab != null && !_grab.isSelected) _grab.enabled = retiravel;
        }

        /// <summary>Tira a peça do encaixe (modo livre). Também usado pelos testes.</summary>
        public void RemoverDoEncaixe()
        {
            if (!Instalada || PontoAtual == null) return;
            var ponto = PontoAtual;
            var mae = ponto.GetComponentInParent<PecaHardware>();
            if (mae != null && mae != this)
                foreach (var meu in GetComponentsInChildren<Collider>())
                    foreach (var dela in mae.GetComponents<Collider>())
                        Physics.IgnoreCollision(meu, dela, false);
            Instalada = false;
            PontoAtual = null;
            _retiradaNestaPegada = true;
            if (!_grab.isSelected)
            {
                // Retirada por código (sem estar na mão): solta imediatamente.
                _retiradaNestaPegada = false;
                transform.SetParent(_paiInicial, true);
                _rb.isKinematic = false;
                _rb.useGravity = true;
            }
            if (GerenciadorMontagem.Instancia != null) GerenciadorMontagem.Instancia.NotificarRetirada(this, ponto);
        }

        /// <summary>Prende a peça ao ponto de encaixe e desativa a manipulação (exceto se a retirada estiver liberada).</summary>
        public void FixarEm(PontoEncaixe ponto)
        {
            Instalada = true;
            Segurada = false;
            PontoAtual = ponto;
            if (_grab.isSelected && _grab.interactionManager != null)
                _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
            _grab.enabled = _permiteRetirar;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            _rb.isKinematic = true;
            _rb.useGravity = false;
            transform.SetParent(ponto.transform, true);
            transform.SetPositionAndRotation(ponto.transform.position, ponto.transform.rotation);
            // Instalada sobre outra peça (ex.: CPU na placa-mãe ainda fora do gabinete): ignora a colisão
            // com ela, senão o colisor encostado empurraria a placa-mãe.
            var mae = ponto.GetComponentInParent<PecaHardware>();
            if (mae != null && mae != this)
                foreach (var meu in GetComponentsInChildren<Collider>())
                    foreach (var dela in mae.GetComponents<Collider>())
                        Physics.IgnoreCollision(meu, dela, true);
        }

        /// <summary>Devolve a peça solta à posição inicial na bancada.</summary>
        public void RetornarAoInicio()
        {
            if (Instalada) return;
            if (_grab.isSelected && _grab.interactionManager != null)
                _grab.interactionManager.CancelInteractableSelection((IXRSelectInteractable)_grab);
            Segurada = false;
            if (!_rb.isKinematic)
            {
                _rb.linearVelocity = Vector3.zero;
                _rb.angularVelocity = Vector3.zero;
            }
            transform.SetParent(_paiInicial, true);
            transform.SetPositionAndRotation(_posicaoInicial, _rotacaoInicial);
            _rb.position = _posicaoInicial;
            _rb.rotation = _rotacaoInicial;
        }

        /// <summary>Repulsão física: empurra a peça para longe do ponto que a rejeitou.</summary>
        public void Repelir(Vector3 origem, float velocidade)
        {
            if (Instalada || _rb.isKinematic) return;
            Vector3 dir = PontoReferenciaMundo - origem;
            dir.y = 0f;
            dir = dir.sqrMagnitude < 1e-6f ? Vector3.zero : dir.normalized * 0.6f;
            dir.y = 1f;
            _rb.linearVelocity = dir.normalized * velocidade;
            _rb.angularVelocity = Vector3.zero;
        }

        /// <summary>Vibração curta no controle que segurou a peça por último (se houver).</summary>
        public void Vibrar(float amplitude, float duracao)
        {
            if (UltimoInterator is XRBaseInputInteractor entrada)
                entrada.SendHapticImpulse(amplitude, duracao);
        }
    }
}
