// Testes automatizados da LÓGICA de montagem (sem headset).
// Simulam a soltura das peças chamando GerenciadorMontagem.ProcessarSoltura diretamente.
// NÃO substituem o teste com o Quest: não exercitam controles, pegada nem conforto.
//
// Executar: Window > General > Test Runner > PlayMode > Run All
// ou: Unity.exe -batchmode -projectPath <proj> -runTests -testPlatform PlayMode -testResults <arquivo.xml>
using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace VirtuaLab.Tests
{
    public class MontagemPlayModeTests
    {
        const string Cena = "VirtuaLab_Montagem";
        GerenciadorMontagem _g;
        string _pastaLogs;

        [UnitySetUp]
        public IEnumerator Preparar()
        {
            _pastaLogs = Path.Combine(Application.temporaryCachePath, "virtualab_testes_" + System.Guid.NewGuid().ToString("N"));
            RegistroSessao.PastaSaidaForcada = _pastaLogs;
            RegistroSessao.ParticipanteGlobal = "TESTE";
            RegistroSessao.TentativaGlobal = 1;
            MenusVR.PularMenuInicial = false;
            LogAssert.ignoreFailingMessages = true; // sem headset o OpenXR registra erros de inicialização
            yield return SceneManager.LoadSceneAsync(Cena, LoadSceneMode.Single);
            yield return null;
            yield return null;
            _g = Object.FindAnyObjectByType<GerenciadorMontagem>();
            Assert.IsNotNull(_g, "GerenciadorMontagem não encontrado na cena " + Cena);
        }

        [TearDown]
        public void Limpar()
        {
            RegistroSessao.PastaSaidaForcada = null;
            RegistroSessao.ParticipanteGlobal = null;
            try { if (Directory.Exists(_pastaLogs)) Directory.Delete(_pastaLogs, true); } catch { }
        }

        PecaHardware Peca(TipoComponente t) =>
            Object.FindObjectsByType<PecaHardware>(FindObjectsInactive.Include).First(p => p.tipo == t);

        PontoEncaixe Ponto(TipoComponente t) => _g.pontos.First(p => p.tipoAceito == t);

        /// <summary>Leva a peça à pose de encaixe com um desvio (m, graus) e simula a soltura.</summary>
        ResultadoEncaixe? Soltar(PecaHardware peca, PontoEncaixe ponto, Vector3 desvio = default, float giroGraus = 0f)
        {
            var rb = peca.GetComponent<Rigidbody>();
            // Gira em torno do ponto de referência da peça (como a mão faria), não do pivô do modelo.
            Quaternion rot = ponto.transform.rotation * Quaternion.AngleAxis(giroGraus, Vector3.right);
            Vector3 pos = ponto.CentroZonaMundo + desvio - rot * peca.pontoReferenciaLocal;
            peca.transform.SetPositionAndRotation(pos, rot);
            rb.position = peca.transform.position;
            rb.rotation = rot;
            Physics.SyncTransforms();
            return _g.ProcessarSoltura(peca);
        }

        [UnityTest]
        public IEnumerator Cena_TemSeteEtapasEPecasCorrespondentes()
        {
            Assert.AreEqual(7, _g.pontos.Count, "número de etapas");
            foreach (var p in _g.pontos)
            {
                Assert.IsNotNull(p.guia, "guia ausente em " + p.name);
                Assert.Greater(p.raioDeteccao, p.raioEncaixe, "raio de detecção deve ser maior que o de encaixe em " + p.name);
                Assert.IsNotNull(Peca(p.tipoAceito), "peça ausente para " + p.name);
            }
            Assert.IsFalse(_g.botaoPower.Liberado, "Power deve começar bloqueado");
            yield return null;
        }

        [UnityTest]
        public IEnumerator PecaErradaNoLocal_ERejeitadaEContaErroDeLocal()
        {
            var r = Soltar(Peca(TipoComponente.MemoriaRAM), Ponto(TipoComponente.PlacaMae));

            Assert.AreEqual(ResultadoEncaixe.TipoIncorreto, r);
            Assert.AreEqual(1, _g.ErrosLocal);
            Assert.IsFalse(Peca(TipoComponente.MemoriaRAM).Instalada);
            Assert.IsFalse(Ponto(TipoComponente.PlacaMae).Concluido);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrdemErrada_GpuAntesDaPlacaMaeNoGabinete_ERejeitada()
        {
            var r = Soltar(Peca(TipoComponente.PlacaDeVideo), Ponto(TipoComponente.PlacaDeVideo));
            Assert.AreEqual(ResultadoEncaixe.PreRequisitoPendente, r);
            Assert.AreEqual(1, _g.ErrosOrdem);
            Assert.IsFalse(Peca(TipoComponente.PlacaDeVideo).Instalada);
            yield return null;
        }

        [UnityTest]
        public IEnumerator CpuNaPlacaForaDoGabinete_EncaixaEAcompanhaAPlaca()
        {
            var placa = Peca(TipoComponente.PlacaMae);
            var cpu = Peca(TipoComponente.Processador);
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(cpu, Ponto(TipoComponente.Processador)));
            Assert.IsTrue(cpu.Instalada);
            yield return new WaitForSeconds(0.3f);
            // A placa não pode ser empurrada pelo processador encaixado nela.
            Assert.Less(placa.GetComponent<Rigidbody>().linearVelocity.magnitude, 0.05f);
            Vector3 local = placa.transform.InverseTransformPoint(cpu.transform.position);
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(placa, Ponto(TipoComponente.PlacaMae)));
            Assert.Less(Vector3.Distance(local, placa.transform.InverseTransformPoint(cpu.transform.position)), 1e-4f,
                "o processador deveria continuar no soquete depois de a placa ir para o gabinete");
        }

        [UnityTest]
        public IEnumerator RamESsdInvertidos_SaoRejeitados_CoolerGiradoEAceito()
        {
            Soltar(Peca(TipoComponente.PlacaMae), Ponto(TipoComponente.PlacaMae));
            var pRam = Ponto(TipoComponente.MemoriaRAM);
            Assert.IsFalse(pRam.aceitaGiro180, "RAM tem chanfro: só entra em um sentido");
            Assert.AreEqual(ResultadoEncaixe.OrientacaoIncorreta, SoltarGiradoNoEixoDoEncaixe(Peca(TipoComponente.MemoriaRAM), pRam, 180f));
            Assert.AreEqual(ResultadoEncaixe.OrientacaoIncorreta, SoltarGiradoNoEixoDoEncaixe(Peca(TipoComponente.SSD), Ponto(TipoComponente.SSD), 180f));
            Assert.AreEqual(2, _g.ErrosOrientacao);
            yield return null;
        }

        /// <summary>Gira a peça em torno do eixo de inserção (Y do ponto), como inverter o pente de RAM.</summary>
        ResultadoEncaixe? SoltarGiradoNoEixoDoEncaixe(PecaHardware peca, PontoEncaixe ponto, float graus)
        {
            Quaternion rot = ponto.transform.rotation * Quaternion.AngleAxis(graus, Vector3.up);
            Vector3 pos = ponto.CentroZonaMundo - rot * peca.pontoReferenciaLocal;
            peca.transform.SetPositionAndRotation(pos, rot);
            var rb = peca.GetComponent<Rigidbody>();
            rb.position = pos; rb.rotation = rot;
            Physics.SyncTransforms();
            return _g.ProcessarSoltura(peca);
        }

        [UnityTest]
        public IEnumerator ForaDaTolerancia_NaoEncaixaENaoContaErro()
        {
            var ponto = Ponto(TipoComponente.PlacaMae);
            var r = Soltar(Peca(TipoComponente.PlacaMae), ponto, Vector3.up * (ponto.raioEncaixe + 0.03f));
            Assert.AreEqual(ResultadoEncaixe.ForaDeAlcance, r);
            Assert.AreEqual(0, _g.ErrosTotal);
            Assert.IsFalse(ponto.Concluido);

            // Bem longe de qualquer encaixe: nenhum alvo, nenhum erro.
            var peca = Peca(TipoComponente.PlacaMae);
            peca.transform.position = ponto.transform.position + Vector3.up * 1.0f;
            Physics.SyncTransforms();
            Assert.IsNull(_g.ProcessarSoltura(peca));
            Assert.AreEqual(0, _g.ErrosTotal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator PlacaMaeSoltaNaMesa_NaoEComparadaComSeusPropriosEncaixes()
        {
            var peca = Peca(TipoComponente.PlacaMae);
            peca.transform.position += Vector3.up * 0.05f; // levanta e solta sobre a mesa, longe do gabinete
            Physics.SyncTransforms();
            Assert.IsNull(_g.ProcessarSoltura(peca));
            Assert.AreEqual(0, _g.ErrosTotal);
            yield return null;
        }

        [UnityTest]
        public IEnumerator OrientacaoErrada_ERejeitada_EDentroDaToleranciaEncaixa()
        {
            var ponto = Ponto(TipoComponente.PlacaMae);
            var peca = Peca(TipoComponente.PlacaMae);
            var r = Soltar(peca, ponto, Vector3.zero, ponto.toleranciaAngular + 25f);
            Assert.AreEqual(ResultadoEncaixe.OrientacaoIncorreta, r);
            Assert.AreEqual(1, _g.ErrosOrientacao);

            r = Soltar(peca, ponto, new Vector3(0.03f, 0.02f, -0.03f), ponto.toleranciaAngular - 15f);
            Assert.AreEqual(ResultadoEncaixe.Valido, r);
            Assert.IsTrue(peca.Instalada);
            // Depois do encaixe a peça fica exatamente na pose do ponto.
            Assert.Less(Vector3.Distance(peca.transform.position, ponto.transform.position), 1e-4f);
            Assert.Less(Quaternion.Angle(peca.transform.rotation, ponto.transform.rotation), 0.1f);
            yield return null;
        }

        [UnityTest]
        public IEnumerator MontagemCompleta_LiberaPowerEExecutaBoot()
        {
            // Etapas futuras ficam ocultas no telão.
            StringAssert.Contains("Etapa ainda não revelada", _g.painel.textoEtapas.text);
            // Power antes da hora: falha com diagnóstico no monitor.
            _g.TentarLigar();
            Assert.IsFalse(_g.ComputadorLigado);
            Assert.IsTrue(_g.monitor.MostrandoFalha);
            StringAssert.Contains("ausente", _g.monitor.textoResultado.text);

            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.PlacaMae), Ponto(TipoComponente.PlacaMae), new Vector3(0.02f, 0.01f, 0.02f), 20f));
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.Processador), Ponto(TipoComponente.Processador), new Vector3(0.01f, 0.01f, 0f), 30f));

            // Cooler antes da pasta térmica: erro de ordem.
            Assert.AreEqual(ResultadoEncaixe.PreRequisitoPendente, Soltar(Peca(TipoComponente.Cooler), Ponto(TipoComponente.Cooler)));
            Assert.AreEqual(1, _g.ErrosOrdem);
            yield return new WaitForSeconds(0.2f);

            // Pasta térmica: modo aplicação (a seringa não fica presa e volta à bancada).
            var pasta = Peca(TipoComponente.PastaTermica);
            Vector3 inicioPasta = pasta.transform.position;
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(pasta, Ponto(TipoComponente.PastaTermica), new Vector3(0.01f, 0.015f, 0f), 80f));
            Assert.IsTrue(Ponto(TipoComponente.PastaTermica).Concluido);
            Assert.IsFalse(pasta.Instalada);
            Assert.IsTrue(Ponto(TipoComponente.PastaTermica).visualAplicado.activeSelf);

            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.Cooler), Ponto(TipoComponente.Cooler), new Vector3(0f, 0.02f, 0.01f), 10f));
            // CPU e cooler aceitam giro de 180° (peças simétricas).
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.MemoriaRAM), Ponto(TipoComponente.MemoriaRAM), Vector3.zero, 0f));
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.SSD), Ponto(TipoComponente.SSD), new Vector3(0.01f, 0f, 0.01f), 15f));
            Assert.IsFalse(_g.MontagemConcluida, "ainda falta a placa de vídeo");
            Assert.IsFalse(_g.botaoPower.Liberado);
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.PlacaDeVideo), Ponto(TipoComponente.PlacaDeVideo), new Vector3(0.02f, 0.02f, 0f), 25f));

            Assert.IsTrue(_g.MontagemConcluida);
            Assert.AreEqual(7, _g.Acertos);
            Assert.AreEqual(1, _g.ErrosTotal);
            Assert.IsTrue(_g.botaoPower.Liberado);
            yield return null;
            Assert.Less(Vector3.Distance(pasta.transform.position, inicioPasta), 0.25f, "a seringa deveria ter voltado à bancada");

            _g.TentarLigar();
            Assert.IsTrue(_g.ComputadorLigado);
            Assert.IsTrue(_g.monitor.EmExecucao || _g.monitor.BootConcluido);
            float limite = Time.time + 15f;
            while (!_g.monitor.BootConcluido && Time.time < limite) yield return null;
            Assert.IsTrue(_g.monitor.BootConcluido, "o boot simulado não terminou");
            StringAssert.Contains("TREINAMENTO CONCLUÍDO", _g.monitor.textoResultado.text);

            // Registro em CSV.
            string resumo = Path.Combine(_pastaLogs, "resumo_sessoes.csv");
            Assert.IsTrue(File.Exists(resumo), "resumo_sessoes.csv não foi criado");
            var linhas = File.ReadAllLines(resumo);
            Assert.GreaterOrEqual(linhas.Length, 3, "cabeçalho + conclusão + power");
            StringAssert.Contains("TESTE", linhas[1]);
            Assert.IsTrue(Directory.GetFiles(_pastaLogs, "eventos_*.csv").Length == 1);

            // Estatísticas e exportação.
            Assert.GreaterOrEqual(RegistroSessao.LerResumo().Count, 2);
            string msg = RegistroSessao.Exportar();
            StringAssert.Contains("exportado", msg);
            Assert.IsTrue(Directory.Exists(Path.Combine(_pastaLogs, "Exportacoes")));
        }

        [UnityTest]
        public IEnumerator PecaQueCai_VoltaParaABancada()
        {
            var peca = Peca(TipoComponente.SSD);
            Vector3 inicio = peca.transform.position;
            peca.transform.position = new Vector3(0f, 0.1f, 1.0f); // abaixo do tampo
            peca.GetComponent<Rigidbody>().position = peca.transform.position;
            yield return null;
            yield return null;
            Assert.Less(Vector3.Distance(peca.transform.position, inicio), 0.05f);
        }

        [UnityTest]
        public IEnumerator MenuInicial_BloqueiaPecasAteIniciar()
        {
            Assert.IsNotNull(_g.menus, "menus ausentes na cena");
            Assert.IsTrue(_g.menus.MenuInicialAberto);
            Assert.IsFalse(_g.SessaoIniciada);
            var grab = Peca(TipoComponente.MemoriaRAM).GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            Assert.IsFalse(grab.enabled, "peças deveriam estar bloqueadas antes de iniciar");
            _g.menus.botaoIniciar.Pressionar();
            yield return null;
            Assert.IsTrue(_g.SessaoIniciada);
            Assert.IsFalse(_g.menus.MenuInicialAberto);
            Assert.IsTrue(grab.enabled);
        }

        [UnityTest]
        public IEnumerator Pausa_CongelaOCronometro()
        {
            _g.menus.Iniciar();
            _g.NotificarPecaPega(Peca(TipoComponente.SSD));
            yield return new WaitForSeconds(0.3f);
            _g.menus.AbrirPausa();
            Assert.IsTrue(_g.Pausado);
            float t0 = _g.TempoMontagem;
            yield return new WaitForSeconds(0.5f);
            Assert.Less(_g.TempoMontagem - t0, 0.05f, "o tempo não deveria contar durante a pausa");
            _g.menus.FecharPausa();
            Assert.IsFalse(_g.Pausado);
            yield return new WaitForSeconds(0.2f);
            Assert.Greater(_g.TempoMontagem, t0 + 0.1f);
        }

        [UnityTest]
        public IEnumerator PecaSoltaPertoDeOutroEncaixe_SoContaErroSeBemProxima()
        {
            Soltar(Peca(TipoComponente.PlacaMae), Ponto(TipoComponente.PlacaMae));
            var gpu = Ponto(TipoComponente.PlacaDeVideo);
            var ram = Peca(TipoComponente.MemoriaRAM);
            // 6 cm do centro do slot da GPU: antes contava como erro (raio de 7 cm); agora é só uma solta.
            Assert.IsNull(Soltar(ram, gpu, Vector3.up * 0.06f));
            Assert.AreEqual(0, _g.ErrosLocal);
            // Bem em cima do slot da GPU: continua sendo erro de local, com explicação.
            Assert.AreEqual(ResultadoEncaixe.TipoIncorreto, Soltar(ram, gpu, Vector3.up * 0.01f));
            Assert.AreEqual(1, _g.ErrosLocal);
            StringAssert.Contains("DIMM", _g.painel.textoFeedback.text);
            yield return null;
        }

        [UnityTest]
        public IEnumerator DicaDoGrip_DestacaAPecaGripDosDoisControles()
        {
            yield return null;
            Assert.IsNotNull(_g.dicaGrip);
            Assert.AreEqual(2, _g.dicaGrip.GripsEncontrados, "o botão de pegada (Bumper) do modelo do controle não foi encontrado");
        }

        [UnityTest]
        public IEnumerator ModoLivre_SemRespostaVisual_VerificaNoPowerEPermiteCorrigir()
        {
            _g.menus.Iniciar();
            _g.AlternarModoGuiado();
            Assert.IsFalse(_g.modoGuiado);
            yield return null; // o telão atualiza no Update
            StringAssert.Contains("POWER para verificar", _g.painel.textoStatus.text + " ");
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.PlacaMae), Ponto(TipoComponente.PlacaMae)));

            // RAM invertida: não encaixa, não é empurrada e o erro é contado em silêncio.
            var ram = Peca(TipoComponente.MemoriaRAM);
            Assert.AreEqual(ResultadoEncaixe.OrientacaoIncorreta, SoltarGiradoNoEixoDoEncaixe(ram, Ponto(TipoComponente.MemoriaRAM), 180f));
            Assert.IsFalse(ram.Instalada);
            Assert.Less(ram.GetComponent<Rigidbody>().linearVelocity.magnitude, 0.5f, "no modo livre não há repulsão (a repulsão é de 1 m/s)");
            Assert.AreEqual(1, _g.ErrosOrientacao);
            StringAssert.DoesNotContain("ORIENTAÇÃO", _g.painel.textoFeedback.text);

            // Cooler sem pasta: cabe fisicamente, é aceito, mas fica marcado com problema.
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.Processador), Ponto(TipoComponente.Processador)));
            var cooler = Peca(TipoComponente.Cooler);
            var pCooler = Ponto(TipoComponente.Cooler);
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(cooler, pCooler));
            StringAssert.Contains("pasta", pCooler.Problema);
            // Com o cooler em cima, a pasta não cabe mais.
            Assert.AreEqual(ResultadoEncaixe.PreRequisitoPendente, Soltar(Peca(TipoComponente.PastaTermica), Ponto(TipoComponente.PastaTermica)));

            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(ram, Ponto(TipoComponente.MemoriaRAM)));
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.SSD), Ponto(TipoComponente.SSD)));
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.PlacaDeVideo), Ponto(TipoComponente.PlacaDeVideo)));
            Assert.IsFalse(_g.MontagemConcluida, "no modo livre a conclusão é verificada no Power");

            // Power: falha com diagnóstico apontando o problema.
            _g.TentarLigar();
            Assert.IsFalse(_g.ComputadorLigado);
            Assert.AreEqual(1, _g.FalhasPower);
            StringAssert.Contains("sem pasta", _g.monitor.textoResultado.text);

            // Correção: retirar o cooler, aplicar a pasta e recolocar o cooler.
            var grabCooler = cooler.GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            Assert.IsTrue(grabCooler.enabled, "no modo livre o cooler instalado pode ser retirado");
            cooler.RemoverDoEncaixe();
            Assert.IsFalse(cooler.Instalada);
            Assert.IsFalse(pCooler.Concluido);
            Assert.AreEqual(1, _g.Retiradas);
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(Peca(TipoComponente.PastaTermica), Ponto(TipoComponente.PastaTermica)));
            var grabCpu = Peca(TipoComponente.Processador).GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRGrabInteractable>();
            Assert.IsFalse(grabCpu.enabled, "processador com pasta em cima não pode ser retirado");
            Assert.AreEqual(ResultadoEncaixe.Valido, Soltar(cooler, pCooler));
            Assert.AreEqual("", pCooler.Problema);

            _g.TentarLigar();
            Assert.IsTrue(_g.MontagemConcluida);
            Assert.IsTrue(_g.ComputadorLigado);
            yield return null;
        }

        [Test]
        public void ProximoCodigoDeParticipante()
        {
            Assert.AreEqual("P01", ControleSessao.ProximoCodigo("P00"));
            Assert.AreEqual("P10", ControleSessao.ProximoCodigo("P09"));
            Assert.AreEqual("P01", ControleSessao.ProximoCodigo(""));
        }
    }
}
