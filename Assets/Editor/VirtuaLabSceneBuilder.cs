// Construtor da cena de treinamento do VirtuaLab VR (somente Editor).
//
// Gera do zero a cena Assets/Scenes/VirtuaLab_Montagem.unity a partir dos modelos OBJ do projeto,
// com todas as peças, pontos de encaixe calibrados, painel, botão Power e monitor de boot.
// É idempotente: pode ser executado quantas vezes for preciso (a cena é recriada).
// A cena original Assets/VR_PC_Demo1.unity NÃO é alterada.
//
// Menu:   VirtuaLab > Construir cena de montagem
// CLI:    Unity.exe -batchmode -projectPath <proj> -executeMethod VirtuaLab.EditorTools.VirtuaLabSceneBuilder.ConstruirCena -quit
//
// TODA a calibração de posições/tolerâncias fica na região "CALIBRAÇÃO" abaixo. Ajustes feitos à
// mão na cena são PERDIDOS quando o construtor roda de novo: prefira alterar as constantes aqui.
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Attachment;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace VirtuaLab.EditorTools
{
    public static class VirtuaLabSceneBuilder
    {
        public const string CaminhoCena = "Assets/Scenes/VirtuaLab_Montagem.unity";
        const string PastaMateriais = "Assets/VirtuaLab/Materiais";
        const string PastaTexturas = "Assets/VirtuaLab/Texturas";
        const string PrefabRig = "Assets/Samples/XR Interaction Toolkit/3.5.1/Starter Assets/Prefabs/XR Origin (XR Rig).prefab";

        // Modelos (OBJ em escala real, metros).
        const string ObjMesa = "Assets/Tisch_klein_99.obj";
        const string ObjGabinete = "Assets/Gehäuse_1_107.obj";
        const string ObjPlacaMae = "Assets/Mainboard_1_91.obj";
        const string ObjCpu = "Assets/CPU_1_110.obj";
        const string ObjCooler = "Assets/CPU_Kühler_93.obj";
        const string ObjRam = "Assets/RAM_1_108.obj";
        const string ObjSsd = "Assets/M2_1_92.obj";
        const string ObjGpu = "Assets/Grafikkarte_95.obj";
        const string ObjFonte = "Assets/Netzteil_98.obj";

        #region CALIBRAÇÃO
        // ---- Bancada (pose da cena original) ----
        static readonly Vector3 MesaPos = new Vector3(0.0618f, 0.5022f, 1.9015f);
        static readonly Quaternion MesaRot = new Quaternion(-0.00031686135f, 0.6977663f, 0.00031629394f, 0.71632534f);

        // ---- Usuário ----
        static readonly Vector3 RigPos = new Vector3(0f, 0f, 1.20f);   // ~1 passo mais perto que a 1ª versão (0,95)

        // ---- Base fixa de montagem (referência espacial fixa na mesa) ----
        // Gabinete deitado, lado aberto para cima, placa-mãe do lado do usuário.
        // Eixos locais do gabinete no mundo: X -> baixo, Y -> usuário (-Z), Z -> direita (+X).
        static readonly Vector2 BaseXZ = new Vector2(0.238f, 1.863f);
        static readonly Quaternion BaseRot = Quaternion.LookRotation(Vector3.right, Vector3.back);

        // ---- Encaixes no espaço local do GABINETE ----
        static readonly Vector3 EncaixePlacaMae = new Vector3(-0.064f, 0.141f, -0.011f);   // identidade
        static readonly Vector3 EncaixeFonte = new Vector3(-0.175f, 0.0775f, -0.065f);   // fonte pré-instalada (visual)
        static readonly Quaternion EncaixeFonteRot = Quaternion.Euler(0f, 0f, 180f);
        static readonly Vector3 BotaoPowerLocal = new Vector3(-0.116f, 0.4655f, -0.42f);

        // ---- Encaixes no espaço local da PLACA-MÃE (lado dos componentes = -X) ----
        // Todas as peças entram com Euler(0,0,90): Y local da peça aponta para fora da placa (-X).
        static readonly Quaternion RotNaPlaca = Quaternion.Euler(0f, 0f, 90f);
        static readonly Vector3 EncaixeCpu = new Vector3(-0.0056f, 0.214f, -0.134f);
        static readonly Vector3 EncaixePasta = new Vector3(-0.0099f, 0.214f, -0.134f);
        static readonly Vector3 EncaixeCooler = new Vector3(-0.053f, 0.2135f, -0.143f);
        static readonly Vector3 EncaixeRam = new Vector3(-0.005f, 0.271f, -0.1885f);       // 2º slot (A2)
        static readonly Vector3 EncaixeSsd = new Vector3(-0.004f, 0.142f, -0.160f);        // conector M.2
        // Marca do canto do soquete (corresponde ao triângulo da CPU na pose de encaixe).
        static readonly Vector3 MarcaSoquete = new Vector3(-0.0059f, 0.2405f, -0.1125f);
        // Travas dos 4 slots DIMM: posição do chanfro da RAM (a 51,8 mm da ponta do pente) e centro de cada slot.
        const float TravaDimmY = 0.2192f;
        static readonly float[] SlotsDimmZ = { -0.2110f, -0.1999f, -0.1888f, -0.1776f };
        static readonly Vector3 EncaixeGpu = new Vector3(-0.0446f, 0.1008f, -0.070f);      // slot PCIe x16 superior

        // ---- Tolerâncias: (raioDeteccao, raioEncaixe, toleranciaAngular, aceitaGiro180) ----
        struct Tol { public float det, enc, ang; public bool giro; public Tol(float d, float e, float a, bool g) { det = d; enc = e; ang = a; giro = g; } }
        // 2ª calibração (25/11, após testes no headset): o guia verde/vermelho só aparece bem perto do local
        // (raio de detecção ~1,5 x o raio de encaixe) e as peças com encaixe assimétrico não aceitam giro de 180°.
        //  - Processador: marca de canto (triângulo) na CPU e no soquete -> um único sentido.
        //  - RAM: chanfro fora do centro no modelo (a 51 mm de uma ponta) -> um único sentido.
        //  - SSD M.2: chave "M" no conector e parafuso na outra ponta -> um único sentido.
        //  - Cooler: furos de fixação simétricos -> monta nos dois sentidos (aceita 180°).
        static readonly Tol TolPlacaMae = new Tol(0.15f, 0.08f, 50f, false);
        static readonly Tol TolCpu = new Tol(0.06f, 0.035f, 50f, false);
        static readonly Tol TolPasta = new Tol(0.06f, 0.035f, 0f, false);
        static readonly Tol TolCooler = new Tol(0.08f, 0.05f, 50f, true);
        static readonly Tol TolRam = new Tol(0.07f, 0.045f, 50f, false);
        static readonly Tol TolSsd = new Tol(0.07f, 0.045f, 50f, false);
        static readonly Tol TolGpu = new Tol(0.10f, 0.06f, 50f, false);

        // ---- Posição inicial das peças sobre a mesa (centro X,Z no mundo) ----
        static readonly Vector2 MesaPlacaMae = new Vector2(-0.45f, 1.56f);
        static readonly Vector2 MesaGpu = new Vector2(-0.73f, 1.56f);
        static readonly Vector2 MesaCpu = new Vector2(0.34f, 1.46f);
        static readonly Vector2 MesaPasta = new Vector2(0.50f, 1.46f);
        static readonly Vector2 MesaCooler = new Vector2(0.68f, 1.48f);
        static readonly Vector2 MesaRam = new Vector2(0.40f, 1.63f);
        static readonly Vector2 MesaSsd = new Vector2(0.60f, 1.66f);
        #endregion

        static float _topoMesa;
        static Material _matGuia;

        [MenuItem("VirtuaLab/Construir cena de montagem")]
        public static void ConstruirCena()
        {
            AssetDatabase.Refresh();
            Directory.CreateDirectory(PastaMateriais);
            Directory.CreateDirectory("Assets/Scenes");
            AssetDatabase.Refresh();

            var cena = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // ------------------------------------------------------------ Materiais
            _matGuia = MaterialGuia();
            Material pcb = Lit("VL_PCB", new Color(0.05f, 0.22f, 0.13f), 0.1f, 0.5f);
            Material preto = Lit("VL_Preto", new Color(0.06f, 0.06f, 0.07f), 0.2f, 0.4f);
            Material cinzaEscuro = Lit("VL_CinzaEscuro", new Color(0.20f, 0.21f, 0.24f), 0.5f, 0.5f);
            Material prata = Lit("VL_Prata", new Color(0.72f, 0.74f, 0.78f), 0.9f, 0.6f);
            Material aluminio = Lit("VL_Aluminio", new Color(0.60f, 0.63f, 0.68f), 0.8f, 0.45f);
            Material dourado = Lit("VL_Dourado", new Color(0.85f, 0.65f, 0.15f), 0.9f, 0.6f);
            Material cobre = Lit("VL_Cobre", new Color(0.75f, 0.42f, 0.22f), 0.9f, 0.6f);
            Material vermelho = Lit("VL_Vermelho", new Color(0.65f, 0.10f, 0.10f), 0.1f, 0.4f);
            Material azulDissipador = Lit("VL_Dissipador", new Color(0.35f, 0.42f, 0.60f), 0.7f, 0.5f);
            Material piso = Lit("VL_Piso", new Color(0.32f, 0.33f, 0.36f), 0f, 0.2f);
            Material parede = Lit("VL_Parede", new Color(0.78f, 0.80f, 0.82f), 0f, 0.1f);
            Material painelFundo = Lit("VL_PainelFundo", new Color(0.05f, 0.07f, 0.12f), 0f, 0.1f);
            Material pastaMat = Lit("VL_PastaTermica", new Color(0.80f, 0.80f, 0.82f), 0.2f, 0.3f);
            Material seringa = Lit("VL_Seringa", new Color(0.20f, 0.45f, 0.85f), 0f, 0.6f);
            Material botaoMat = Lit("VL_BotaoPower", new Color(0.75f, 0.08f, 0.08f), 0f, 0.6f);
            Material telaMat = MaterialTela();
            Material matMesa = AssetDatabase.LoadAssetAtPath<Material>("Assets/Mat_Mesa.mat");
            Material matGabinete = AssetDatabase.LoadAssetAtPath<Material>("Assets/Mat_Gabinete.mat");

            // ------------------------------------------------------------ Ambiente
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.56f, 0.60f);

            var luzGo = new GameObject("Luz_Direcional");
            var luz = luzGo.AddComponent<Light>();
            luz.type = LightType.Directional;
            luz.intensity = 1.1f;
            luz.shadows = LightShadows.Soft;
            luzGo.transform.rotation = Quaternion.Euler(55f, -25f, 0f);

            var ambiente = new GameObject("Ambiente");
            var chao = GameObject.CreatePrimitive(PrimitiveType.Plane);
            chao.name = "Piso";
            chao.transform.SetParent(ambiente.transform);
            chao.transform.localScale = new Vector3(1f, 1f, 1f);
            chao.transform.position = new Vector3(0f, 0f, 1.5f);
            chao.GetComponent<Renderer>().sharedMaterial = piso;
            var paredeGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            paredeGo.name = "Parede_Fundo";
            paredeGo.transform.SetParent(ambiente.transform);
            paredeGo.transform.position = new Vector3(0f, 1.5f, 3.2f);
            paredeGo.transform.localScale = new Vector3(10f, 3f, 0.1f);
            paredeGo.GetComponent<Renderer>().sharedMaterial = parede;

            // Mesa (mesmo modelo e pose da cena original).
            var mesa = Instanciar(ObjMesa, "Mesa", ambiente.transform);
            mesa.transform.SetPositionAndRotation(MesaPos, MesaRot);
            Pintar(mesa, matMesa, matMesa);
            Bounds bMesa = BoundsLocais(mesa);
            var colMesa = mesa.AddComponent<BoxCollider>();
            colMesa.center = bMesa.center;
            colMesa.size = bMesa.size;
            _topoMesa = BoundsMundo(mesa).max.y;

            // ------------------------------------------------------------ Base fixa + gabinete
            var baseMontagem = new GameObject("Base_Montagem");
            baseMontagem.transform.SetPositionAndRotation(new Vector3(BaseXZ.x, _topoMesa + 0.001f, BaseXZ.y), BaseRot);
            var gabinete = Instanciar(ObjGabinete, "Gabinete", baseMontagem.transform);
            gabinete.transform.localPosition = Vector3.zero;
            gabinete.transform.localRotation = Quaternion.identity;
            Pintar(gabinete, matGabinete, preto);
            // Colisores estáticos: bandeja (chapa sob a placa-mãe) + 4 paredes. Sem Rigidbody: o gabinete é fixo.
            CaixaEstatica(baseMontagem, "Col_Bandeja", new Vector3(-0.025f, 0.2315f, -0.2378f), new Vector3(0.05f, 0.463f, 0.4755f));
            CaixaEstatica(baseMontagem, "Col_ParedeInferior", new Vector3(-0.116f, 0.0175f, -0.2378f), new Vector3(0.232f, 0.035f, 0.4755f));
            CaixaEstatica(baseMontagem, "Col_ParedeSuperior", new Vector3(-0.116f, 0.454f, -0.2378f), new Vector3(0.232f, 0.018f, 0.4755f));
            CaixaEstatica(baseMontagem, "Col_ParedeTraseira", new Vector3(-0.116f, 0.2315f, -0.006f), new Vector3(0.232f, 0.463f, 0.012f));
            CaixaEstatica(baseMontagem, "Col_ParedeFrontal", new Vector3(-0.116f, 0.2315f, -0.4695f), new Vector3(0.232f, 0.463f, 0.012f));
            // Chapa visual da bandeja (o modelo do gabinete não tem faces internas).
            var bandeja = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bandeja.name = "Bandeja_Visual";
            Object.DestroyImmediate(bandeja.GetComponent<Collider>());
            bandeja.transform.SetParent(baseMontagem.transform, false);
            bandeja.transform.localPosition = new Vector3(-0.049f, 0.2415f, -0.2378f);
            bandeja.transform.localScale = new Vector3(0.003f, 0.42f, 0.455f);
            bandeja.GetComponent<Renderer>().sharedMaterial = cinzaEscuro;

            // ------------------------------------------------------------ Peças
            var pecasRaiz = new GameObject("Pecas");
            var rotulos = new GameObject("Rotulos");

            Quaternion rotDeitada = BaseRot; // mesma orientação de quando instalada no gabinete
            var placaMae = CriarPeca(ObjPlacaMae, "Peca_PlacaMae", pecasRaiz.transform, TipoComponente.PlacaMae,
                "Placa-mãe",
                "Placa principal do computador: interliga processador, memória, armazenamento e placas de expansão.",
                "dentro do gabinete, sobre a bandeja, com os conectores voltados para a traseira.",
                MesaPlacaMae, rotDeitada, 0.9f,
                // Colisor fino (só a placa e os slots), para que peças soltas repousem na altura certa.
                new Bounds(new Vector3(0.0005f, 0.153f, -0.122f), new Vector3(0.0216f, 0.305f, 0.244f)),
                preto, cinzaEscuro, azulDissipador, dourado, pcb);

            var cpu = CriarPeca(ObjCpu, "Peca_Processador", pecasRaiz.transform, TipoComponente.Processador,
                "Processador (CPU)",
                "O \"cérebro\" do computador: executa as instruções dos programas. É frágil: nunca force o encaixe.",
                "no soquete quadrado no centro da placa-mãe.",
                MesaCpu, Quaternion.identity, 0.05f, null, pcb, prata);

            var pasta = CriarPastaTermica(pecasRaiz.transform, MesaPasta, seringa, prata, pastaMat);

            var cooler = CriarPeca(ObjCooler, "Peca_Cooler", pecasRaiz.transform, TipoComponente.Cooler,
                "Cooler do processador",
                "Dissipador com ventoinha: retira o calor do processador. Sem ele a CPU superaquece em segundos.",
                "sobre o processador, depois de aplicar a pasta térmica.",
                MesaCooler, Quaternion.identity, 0.5f, null, prata, aluminio, cobre, preto);

            var ram = CriarPeca(ObjRam, "Peca_MemoriaRAM", pecasRaiz.transform, TipoComponente.MemoriaRAM,
                "Memória RAM",
                "Memória de trabalho: guarda temporariamente os dados dos programas em execução.",
                "em um dos slots longos e estreitos ao lado do processador.",
                MesaRam, Quaternion.Euler(90f, 0f, 0f), 0.05f, null, pcb, dourado, preto);

            var ssd = CriarPeca(ObjSsd, "Peca_SSD", pecasRaiz.transform, TipoComponente.SSD,
                "SSD M.2 NVMe",
                "Armazenamento permanente: guarda o sistema operacional e os arquivos, mesmo com o PC desligado.",
                "no conector M.2 da placa-mãe, deitado sobre a placa.",
                MesaSsd, Quaternion.identity, 0.03f, null, dourado, pcb, preto);

            var gpu = CriarPeca(ObjGpu, "Peca_PlacaDeVideo", pecasRaiz.transform, TipoComponente.PlacaDeVideo,
                "Placa de vídeo (GPU)",
                "Gera as imagens enviadas ao monitor. Encaixa no slot de expansão mais longo (PCI Express x16).",
                "em um slot PCI Express x16, o maior slot de expansão da placa-mãe.",
                MesaGpu, Quaternion.Euler(0f, 0f, -90f), 0.6f, null, dourado, aluminio, preto, prata, cinzaEscuro);

            // Fonte de alimentação: já vem instalada (o gabinete tem uma cobertura fixa sobre o
            // compartimento da fonte, que impede a instalação pelo lado aberto). É apenas visual.
            var fonte = Instanciar(ObjFonte, "Fonte_PreInstalada", baseMontagem.transform);
            fonte.transform.localPosition = EncaixeFonte;
            fonte.transform.localRotation = EncaixeFonteRot;
            Pintar(fonte, cinzaEscuro, vermelho, prata, preto, aluminio);

            // Marcações físicas que definem o sentido de encaixe (aparecem também no "fantasma" do guia).
            Primitiva(PrimitiveType.Cube, "Marca_Triangulo", cpu.transform, new Vector3(0.0195f, 0.0036f, 0.0155f), new Vector3(0.0045f, 0.0006f, 0.0045f), dourado);
            Primitiva(PrimitiveType.Cube, "Marca_Soquete", placaMae.transform, MarcaSoquete, new Vector3(0.0008f, 0.0045f, 0.0045f), dourado);
            foreach (float z in SlotsDimmZ)
                Primitiva(PrimitiveType.Cube, "Trava_DIMM", placaMae.transform, new Vector3(-0.0055f, TravaDimmY, z), new Vector3(0.0105f, 0.0015f, 0.0016f), prata);

            // Onde cada peça vai (painel da mão e mensagem de "local incorreto") e um cuidado importante.
            placaMae.explicacaoLocal = "na bandeja dentro do gabinete, com os conectores (portas USB, vídeo, rede) voltados para a traseira.";
            placaMae.cuidado = "segure pelas bordas; é a base onde as outras peças são instaladas.";
            cpu.explicacaoLocal = "no soquete quadrado no centro da placa-mãe, antes da pasta térmica e do cooler.";
            cpu.cuidado = "alinhe o triângulo dourado do canto com a marca do soquete; o encaixe é sem força.";
            pasta.explicacaoLocal = "sobre o processador já instalado, antes de colocar o cooler.";
            pasta.cuidado = "uma pequena porção no centro do processador é suficiente.";
            cooler.explicacaoLocal = "sobre o processador, depois da pasta térmica.";
            cooler.cuidado = "a ventoinha fica voltada para a frente do gabinete, empurrando o ar para a traseira; sem cooler o processador superaquece.";
            ram.explicacaoLocal = "nos slots DIMM: longos e estreitos, ao lado do processador.";
            ram.cuidado = "o pente tem um chanfro fora do centro: alinhe com a trava do slot; só encaixa em um sentido.";
            ssd.explicacaoLocal = "no conector M.2: pequeno, deitado sobre a placa, abaixo do processador.";
            ssd.cuidado = "os contatos dourados com o recorte vão no conector; a ponta com o semicírculo fica do lado do parafuso.";
            gpu.explicacaoLocal = "no slot PCI Express x16, o maior slot de expansão da placa-mãe (neste treino, o mais próximo do processador).";
            gpu.cuidado = "segure pelas bordas e empurre até a trava do slot fechar.";

            foreach (var p in new[] { placaMae, cpu, pasta, cooler, ram, ssd, gpu })
                CriarRotulo(rotulos.transform, p);

            // ------------------------------------------------------------ Pontos de encaixe
            var pPlaca = CriarPonto(baseMontagem.transform, "Encaixe_PlacaMae", EncaixePlacaMae, Quaternion.identity,
                placaMae, "Fixar a placa-mãe no gabinete", "bandeja do gabinete", TolPlacaMae);


            Transform tPlaca = placaMae.transform;
            var pCpu = CriarPonto(tPlaca, "Encaixe_Processador", EncaixeCpu, RotNaPlaca,
                cpu, "Instalar o processador (CPU)", "soquete do processador", TolCpu);
            var pPasta = CriarPontoPasta(tPlaca, pasta, pastaMat);
            var pCooler = CriarPonto(tPlaca, "Encaixe_Cooler", EncaixeCooler, RotNaPlaca,
                cooler, "Instalar o cooler sobre o processador", "suporte do cooler (sobre o processador)", TolCooler);
            var pRam = CriarPonto(tPlaca, "Encaixe_MemoriaRAM", EncaixeRam, RotNaPlaca,
                ram, "Instalar a memória RAM", "slot de memória RAM", TolRam);
            var pSsd = CriarPonto(tPlaca, "Encaixe_SSD", EncaixeSsd, RotNaPlaca,
                ssd, "Instalar o SSD M.2", "conector M.2", TolSsd);
            var pGpu = CriarPonto(tPlaca, "Encaixe_PlacaDeVideo", EncaixeGpu, RotNaPlaca,
                gpu, "Instalar a placa de vídeo (GPU)", "slot PCI Express x16", TolGpu);

            // Processador, pasta, cooler, RAM e SSD podem ser instalados com a placa-mãe ainda na bancada
            // (é como se faz na prática). A placa de vídeo exige a placa-mãe já fixada no gabinete.
            pGpu.preRequisitos.Add(pPlaca);
            pGpu.dicaPreRequisito = "a placa de vídeo é instalada com a placa-mãe já fixada no gabinete, porque também é presa na traseira dele.";
            pPasta.preRequisitos.Add(pCpu);
            pPasta.dicaPreRequisito = "a pasta térmica é aplicada sobre o processador já instalado no soquete.";
            pCooler.preRequisitos.Add(pCpu);
            pCooler.preRequisitos.Add(pPasta);
            pCooler.dicaPreRequisito = "instale o processador e aplique a pasta térmica antes de colocar o cooler.";

            // Restrições FÍSICAS (valem também no modo livre): sem processador não há onde aplicar a pasta;
            // com o cooler instalado o soquete fica coberto; a placa de vídeo também é presa no gabinete.
            // Já "cooler sem pasta" cabe fisicamente: no modo livre é aceito e aparece no diagnóstico do Power.
            pPasta.requisitosFisicos.Add(pCpu);
            pPasta.bloqueadoPor.Add(pCooler);
            pCpu.bloqueadoPor.Add(pCooler);
            pGpu.requisitosFisicos.Add(pPlaca);

            // ------------------------------------------------------------ Painel de instruções
            var painel = CriarPainel(painelFundo);

            // ------------------------------------------------------------ Botão Power
            var botao = CriarBotaoPower(baseMontagem.transform, botaoMat, cinzaEscuro);

            // ------------------------------------------------------------ Monitor
            var monitor = CriarMonitor(preto, telaMat);

            // ------------------------------------------------------------ XR
            Transform rig = null;
            var prefabRig = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabRig);
            if (prefabRig != null)
            {
                var rigGo = (GameObject)PrefabUtility.InstantiatePrefab(prefabRig);
                rigGo.transform.SetPositionAndRotation(RigPos, Quaternion.identity);
                rig = rigGo.transform;
                // Tarefa de bancada: locomoção desativada para evitar deslocamentos acidentais e enjoo.
                foreach (var t in rigGo.GetComponentsInChildren<Transform>(true))
                    if (t.name == "Locomotion") t.gameObject.SetActive(false);
            }
            else
            {
                Debug.LogError("[VirtuaLab] Prefab do XR Origin não encontrado em: " + PrefabRig);
            }
            var xrim = new GameObject("XR Interaction Manager");
            xrim.AddComponent<UnityEngine.XR.Interaction.Toolkit.XRInteractionManager>();

            // ------------------------------------------------------------ Sistema
            var sistema = new GameObject("VirtuaLab_Sistema");
            var audio = sistema.AddComponent<AudioSource>();
            audio.playOnAwake = false;
            audio.spatialBlend = 0f;
            var registro = sistema.AddComponent<RegistroSessao>();
            var gerenciador = sistema.AddComponent<GerenciadorMontagem>();
            gerenciador.pontos = new List<PontoEncaixe> { pPlaca, pCpu, pPasta, pCooler, pRam, pSsd, pGpu };
            gerenciador.painel = painel;
            gerenciador.botaoPower = botao;
            gerenciador.monitor = monitor;
            gerenciador.registro = registro;
            gerenciador.fonteAudio = audio;
            gerenciador.alturaMinimaPecas = _topoMesa - 0.35f;
            gerenciador.painelPeca = CriarPainelPeca();
            gerenciador.menus = CriarMenus(gerenciador, registro);
            var dica = sistema.AddComponent<DicaGrip>();
            dica.rig = rig;
            dica.materialMarcador = MaterialUnlit("VL_Destaque", new Color(1f, 0.85f, 0.1f));
            gerenciador.dicaGrip = dica;
            var controle = sistema.AddComponent<ControleSessao>();
            controle.rigXR = rig;
            controle.gerenciador = gerenciador;
            controle.registro = registro;

            painel.AtualizarEtapas(gerenciador.pontos, pPlaca, true);

            // ------------------------------------------------------------ Salvar
            EditorSceneManager.MarkSceneDirty(cena);
            EditorSceneManager.SaveScene(cena, CaminhoCena);
            RegistrarNoBuild();
            AssetDatabase.SaveAssets();
            Debug.Log("[VirtuaLab] Cena construída: " + CaminhoCena + " (topo da mesa em y=" + _topoMesa.ToString("0.000") + ")");
        }

        // =================================================================== Peças

        static PecaHardware CriarPeca(string obj, string nome, Transform pai, TipoComponente tipo, string exibicao,
            string descricao, string dica, Vector2 centroXZ, Quaternion rot, float massa, Bounds? colisor, params Material[] mats)
        {
            var go = Instanciar(obj, nome, pai);
            Pintar(go, mats);
            Bounds b = BoundsLocais(go);
            var col = go.AddComponent<BoxCollider>();
            Bounds bc = colisor ?? b;
            col.center = bc.center;
            // Espessura mínima de 6 mm para peças muito finas (CPU, SSD) não atravessarem a mesa.
            col.size = Vector3.Max(bc.size, new Vector3(0.006f, 0.006f, 0.006f));
            return FinalizarPeca(go, tipo, exibicao, descricao, dica, b.center, centroXZ, rot, massa);
        }

        static PecaHardware FinalizarPeca(GameObject go, TipoComponente tipo, string exibicao, string descricao, string dica,
            Vector3 referenciaLocal, Vector2 centroXZ, Quaternion rot, float massa)
        {
            var rb = go.AddComponent<Rigidbody>();
            rb.mass = massa;
            rb.linearDamping = 1.5f;
            rb.angularDamping = 3f;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            var grab = go.AddComponent<XRGrabInteractable>();
            grab.movementType = XRBaseInteractable.MovementType.Instantaneous; // segue a mão sem atraso nem tremor
            grab.throwOnDetach = false;                                        // soltar não arremessa
            grab.useDynamicAttach = true;                                      // mantém a pegada onde a mão tocou
            grab.retainTransformParent = true;
            grab.farAttachMode = InteractableFarAttachMode.Near;               // pegada à distância traz a peça até a mão

            var peca = go.AddComponent<PecaHardware>();
            peca.tipo = tipo;
            peca.nomeExibicao = exibicao;
            peca.descricao = descricao;
            peca.dicaInstalacao = dica;
            peca.pontoReferenciaLocal = referenciaLocal;

            PousarNaMesa(go, rot, centroXZ);
            return peca;
        }

        static PecaHardware CriarPastaTermica(Transform pai, Vector2 centroXZ, Material corpo, Material embolo, Material bico)
        {
            // Seringa feita de primitivas; o pivô (origem) é a PONTA do bico, usada como referência de encaixe.
            var go = new GameObject("Peca_PastaTermica");
            go.transform.SetParent(pai);
            Primitiva(PrimitiveType.Cylinder, "Bico", go.transform, new Vector3(0f, 0.008f, 0f), new Vector3(0.006f, 0.008f, 0.006f), bico);
            Primitiva(PrimitiveType.Cylinder, "Corpo", go.transform, new Vector3(0f, 0.051f, 0f), new Vector3(0.022f, 0.035f, 0.022f), corpo);
            Primitiva(PrimitiveType.Cylinder, "Embolo", go.transform, new Vector3(0f, 0.098f, 0f), new Vector3(0.008f, 0.012f, 0.008f), embolo);
            Primitiva(PrimitiveType.Cylinder, "Apoio", go.transform, new Vector3(0f, 0.111f, 0f), new Vector3(0.026f, 0.0015f, 0.026f), embolo);
            var col = go.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, 0.056f, 0f);
            col.size = new Vector3(0.024f, 0.113f, 0.024f);
            return FinalizarPeca(go, TipoComponente.PastaTermica, "Pasta térmica",
                "Preenche as imperfeições entre o processador e o cooler, melhorando a transferência de calor.",
                "encoste a ponta da seringa sobre o processador já instalado e solte.",
                Vector3.zero, centroXZ, Quaternion.Euler(0f, 0f, 90f), 0.03f);
        }

        // =================================================================== Pontos de encaixe

        static PontoEncaixe CriarPonto(Transform pai, string nome, Vector3 posLocal, Quaternion rotLocal,
            PecaHardware peca, string etapa, string local, Tol tol)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = posLocal;
            go.transform.localRotation = rotLocal;
            var p = go.AddComponent<PontoEncaixe>();
            p.nomeEtapa = etapa;
            p.nomeLocal = local;
            p.tipoAceito = peca.tipo;
            p.raioDeteccao = tol.det;
            p.raioEncaixe = tol.enc;
            p.toleranciaAngular = tol.ang;
            p.aceitaGiro180 = tol.giro;
            p.centroZonaLocal = peca.pontoReferenciaLocal;
            p.guia = CriarGuia(peca.gameObject, go.transform);
            return p;
        }

        static PontoEncaixe CriarPontoPasta(Transform placa, PecaHardware pasta, Material pastaMat)
        {
            var go = new GameObject("Encaixe_PastaTermica");
            go.transform.SetParent(placa, false);
            go.transform.localPosition = EncaixePasta;
            go.transform.localRotation = RotNaPlaca;
            var p = go.AddComponent<PontoEncaixe>();
            p.nomeEtapa = "Aplicar a pasta térmica no processador";
            p.nomeLocal = "superfície do processador";
            p.tipoAceito = TipoComponente.PastaTermica;
            p.raioDeteccao = TolPasta.det;
            p.raioEncaixe = TolPasta.enc;
            p.toleranciaAngular = TolPasta.ang;
            p.modoAplicacao = true;
            p.centroZonaLocal = Vector3.zero;

            var gota = Primitiva(PrimitiveType.Cylinder, "Pasta_Aplicada", go.transform, new Vector3(0f, 0.0008f, 0f), new Vector3(0.014f, 0.0008f, 0.014f), pastaMat);
            gota.SetActive(false);
            p.visualAplicado = gota;

            var guia = Primitiva(PrimitiveType.Cylinder, "Guia", go.transform, new Vector3(0f, 0.002f, 0f), new Vector3(0.03f, 0.002f, 0.03f), _matGuia);
            guia.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            guia.SetActive(false);
            p.guia = guia;
            return p;
        }

        /// <summary>Cria o "fantasma" translúcido da peça na pose de encaixe (cópia só das malhas).</summary>
        static GameObject CriarGuia(GameObject peca, Transform ponto)
        {
            var guia = new GameObject("Guia");
            guia.transform.SetParent(ponto, false);
            Matrix4x4 raizInv = peca.transform.worldToLocalMatrix;
            foreach (var mf in peca.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                var g = new GameObject("G_" + mf.name);
                g.transform.SetParent(guia.transform, false);
                Matrix4x4 m = raizInv * mf.transform.localToWorldMatrix;
                g.transform.localPosition = m.GetColumn(3);
                g.transform.localRotation = m.rotation;
                g.transform.localScale = m.lossyScale;
                g.AddComponent<MeshFilter>().sharedMesh = mf.sharedMesh;
                var r = g.AddComponent<MeshRenderer>();
                var mats = new Material[mf.sharedMesh.subMeshCount];
                for (int i = 0; i < mats.Length; i++) mats[i] = _matGuia;
                r.sharedMaterials = mats;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
            }
            guia.SetActive(false);
            return guia;
        }

        // =================================================================== Interface

        static PainelInstrucoes CriarPainel(Material fundo)
        {
            var raiz = new GameObject("Painel_Instrucoes");
            raiz.transform.position = new Vector3(-0.25f, 1.43f, 2.42f);
            var quad = Primitiva(PrimitiveType.Cube, "Fundo", raiz.transform, new Vector3(0f, 0f, 0.012f), new Vector3(1.36f, 0.80f, 0.01f), fundo);

            var p = raiz.AddComponent<PainelInstrucoes>();
            p.textoTitulo = Texto("Titulo", raiz.transform, new Vector3(0f, 0.345f, 0f), 1.30f, 0.07f, 5.0f,
                "VIRTUALAB VR - MONTAGEM DE COMPUTADOR", TextAlignmentOptions.Center, Color.white);
            Texto("Objetivo", raiz.transform, new Vector3(0f, 0.275f, 0f), 1.30f, 0.07f, 2.5f,
                "Objetivo: instalar todas as peças no gabinete. Pegue uma peça segurando o botão lateral do controle (dedo médio), " +
                "leve até o encaixe e solte quando o guia ficar <color=#55FF66>VERDE</color>. <color=#FF5548>VERMELHO</color> = peça, local ou ordem incorretos.",
                TextAlignmentOptions.Center, new Color(0.8f, 0.85f, 0.95f));
            p.textoEtapas = Texto("Etapas", raiz.transform, new Vector3(-0.335f, 0.045f, 0f), 0.62f, 0.36f, 2.9f,
                "ETAPAS", TextAlignmentOptions.TopLeft, Color.white);
            p.textoPeca = Texto("InfoPeca", raiz.transform, new Vector3(0.335f, 0.045f, 0f), 0.62f, 0.36f, 2.7f,
                "<b>COMO USAR</b>\nCada peça tem um rótulo na bancada.\nAo pegar uma peça, um painel junto dela explica o que é, onde vai e os cuidados.\nMenu: botão B.",
                TextAlignmentOptions.TopLeft, new Color(0.92f, 0.92f, 0.92f));
            p.textoFeedback = Texto("Feedback", raiz.transform, new Vector3(0f, -0.235f, 0f), 1.30f, 0.14f, 3.4f,
                "", TextAlignmentOptions.Center, Color.white);
            p.textoStatus = Texto("Status", raiz.transform, new Vector3(0f, -0.355f, 0f), 1.30f, 0.05f, 2.8f,
                "Tempo: 00:00", TextAlignmentOptions.Center, new Color(0.75f, 0.85f, 1f));
            return p;
        }

        static BotaoPower CriarBotaoPower(Transform baseMontagem, Material botaoMat, Material aroMat)
        {
            var raiz = new GameObject("Botao_Power");
            raiz.transform.SetParent(baseMontagem, false);
            raiz.transform.localPosition = BotaoPowerLocal;
            raiz.transform.localRotation = Quaternion.identity; // eixo Y local = normal do painel superior do gabinete
            Primitiva(PrimitiveType.Cylinder, "Aro", raiz.transform, new Vector3(0f, 0.002f, 0f), new Vector3(0.056f, 0.002f, 0.056f), aroMat);
            var disco = Primitiva(PrimitiveType.Cylinder, "Indicador", raiz.transform, new Vector3(0f, 0.007f, 0f), new Vector3(0.042f, 0.005f, 0.042f), botaoMat);
            var col = raiz.AddComponent<SphereCollider>();
            col.radius = 0.04f;                    // alvo generoso para apontar/tocar
            col.center = new Vector3(0f, 0.01f, 0f);
            raiz.AddComponent<XRSimpleInteractable>();
            var b = raiz.AddComponent<BotaoPower>();
            b.indicador = disco.GetComponent<Renderer>();

            Texto("Rotulo_Power", raiz.transform, Vector3.zero,
                new Pose(raiz.transform.position + new Vector3(0f, 0.05f, -0.004f), Quaternion.identity),
                0.16f, 0.03f, 2.2f, "POWER", TextAlignmentOptions.Center, Color.white);
            return b;
        }

        static MonitorBoot CriarMonitor(Material corpoMat, Material telaMat)
        {
            var raiz = new GameObject("Monitor");
            raiz.transform.SetPositionAndRotation(new Vector3(0.70f, _topoMesa, 2.18f), Quaternion.Euler(0f, 32f, 0f));
            Primitiva(PrimitiveType.Cube, "Base", raiz.transform, new Vector3(0f, 0.0075f, 0.03f), new Vector3(0.24f, 0.015f, 0.17f), corpoMat);
            Primitiva(PrimitiveType.Cube, "Haste", raiz.transform, new Vector3(0f, 0.09f, 0.04f), new Vector3(0.04f, 0.16f, 0.03f), corpoMat);
            Primitiva(PrimitiveType.Cube, "Corpo", raiz.transform, new Vector3(0f, 0.34f, 0f), new Vector3(0.58f, 0.35f, 0.025f), corpoMat);
            var tela = Primitiva(PrimitiveType.Quad, "Tela", raiz.transform, new Vector3(0f, 0.34f, -0.0135f), new Vector3(0.544f, 0.306f, 1f), telaMat);
            tela.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            var m = raiz.AddComponent<MonitorBoot>();
            m.tela = tela.GetComponent<Renderer>();
            m.quadros = new[]
            {
                AssetDatabase.LoadAssetAtPath<Texture2D>(PastaTexturas + "/boot_1_post.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(PastaTexturas + "/boot_2_carregando.png"),
                AssetDatabase.LoadAssetAtPath<Texture2D>(PastaTexturas + "/boot_3_sistema.png"),
            };
            foreach (var q in m.quadros)
                if (q == null) Debug.LogError("[VirtuaLab] Imagem de boot ausente em " + PastaTexturas);
            m.duracoes = new[] { 3.0f, 2.5f, 0f };
            m.textoResultado = Texto("Texto_Resultado", raiz.transform, new Vector3(0f, 0.315f, -0.016f), 0.30f, 0.16f, 1.9f,
                "", TextAlignmentOptions.Center, Color.white);
            return m;
        }

        static PainelPeca CriarPainelPeca()
        {
            var raiz = new GameObject("Painel_Peca");
            var visual = new GameObject("Visual");
            visual.transform.SetParent(raiz.transform, false);
            var fundo = Primitiva(PrimitiveType.Quad, "Fundo", visual.transform, new Vector3(0f, 0f, 0.002f), new Vector3(0.26f, 0.13f, 1f), _matGuia);
            fundo.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var t = Texto("Texto", visual.transform, Vector3.zero, 0.245f, 0.12f, 1.05f, "", TextAlignmentOptions.TopLeft, Color.white);
            var p = raiz.AddComponent<PainelPeca>();
            p.raizVisual = visual;
            p.fundo = fundo.GetComponent<Renderer>();
            p.texto = t;
            return p;
        }

        static MenusVR CriarMenus(GerenciadorMontagem g, RegistroSessao registro)
        {
            Material fundoPainel = AssetDatabase.LoadAssetAtPath<Material>(PastaMateriais + "/VL_PainelFundo.mat");
            Material fundoBotao = Lit("VL_Botao", new Color(0.16f, 0.30f, 0.55f), 0f, 0.4f);
            var raiz = new GameObject("Menus");
            var m = raiz.AddComponent<MenusVR>();
            m.gerenciador = g;
            m.registro = registro;

            // Menu inicial: fixo à frente do usuário, sobre a bancada.
            var ini = new GameObject("Menu_Inicial");
            ini.transform.SetParent(raiz.transform, false);
            ini.transform.SetPositionAndRotation(new Vector3(0f, 1.47f, 1.95f), Quaternion.Euler(8f, 0f, 0f));   // acima do gabinete
            Primitiva(PrimitiveType.Cube, "Fundo", ini.transform, new Vector3(0f, -0.045f, 0.012f), new Vector3(0.62f, 0.62f, 0.01f), fundoPainel);
            m.menuInicial = ini;
            m.textoInicial = Texto("Texto", ini.transform, new Vector3(0f, 0.11f, 0f), 0.56f, 0.28f, 3.6f, "", TextAlignmentOptions.Center, Color.white);
            m.botaoIniciar = CriarBotao(ini.transform, "Botao_Iniciar", new Vector3(0f, -0.075f, -0.005f), new Vector2(0.40f, 0.075f), "INICIAR MONTAGEM", fundoBotao, 3.2f);
            m.botaoModoInicial = CriarBotao(ini.transform, "Botao_Modo", new Vector3(-0.145f, -0.19f, -0.005f), new Vector2(0.27f, 0.06f), "Modo guiado", fundoBotao, 2.2f);
            m.botaoProximoInicial = CriarBotao(ini.transform, "Botao_Proximo", new Vector3(0.145f, -0.19f, -0.005f), new Vector2(0.27f, 0.06f), "Próximo participante", fundoBotao, 2.2f);
            m.botaoEstatisticasInicial = CriarBotao(ini.transform, "Botao_Estatisticas", new Vector3(0f, -0.27f, -0.005f), new Vector2(0.40f, 0.055f), "Estatísticas e exportação", fundoBotao, 2.2f);

            // Menu de pausa: posicionado à frente da cabeça quando aberto (B / Menu).
            var pau = new GameObject("Menu_Pausa");
            pau.transform.SetParent(raiz.transform, false);
            Primitiva(PrimitiveType.Cube, "Fundo", pau.transform, new Vector3(0f, -0.04f, 0.012f), new Vector3(0.56f, 0.60f, 0.01f), fundoPainel);
            m.menuPausa = pau;
            m.textoPausa = Texto("Texto", pau.transform, new Vector3(0f, 0.17f, 0f), 0.52f, 0.12f, 3.6f, "PAUSA", TextAlignmentOptions.Center, Color.white);
            m.botaoRetomar = CriarBotao(pau.transform, "Botao_Retomar", new Vector3(0f, 0.065f, -0.005f), new Vector2(0.42f, 0.06f), "Retomar", fundoBotao, 2.6f);
            m.botaoReiniciar = CriarBotao(pau.transform, "Botao_Reiniciar", new Vector3(0f, -0.015f, -0.005f), new Vector2(0.42f, 0.06f), "Reiniciar montagem (nova tentativa)", fundoBotao, 2.2f);
            m.botaoModoPausa = CriarBotao(pau.transform, "Botao_Modo", new Vector3(0f, -0.095f, -0.005f), new Vector2(0.42f, 0.06f), "Modo guiado", fundoBotao, 2.2f);
            m.botaoProximoPausa = CriarBotao(pau.transform, "Botao_Proximo", new Vector3(0f, -0.175f, -0.005f), new Vector2(0.42f, 0.06f), "Próximo participante", fundoBotao, 2.2f);
            m.botaoEstatisticasPausa = CriarBotao(pau.transform, "Botao_Estatisticas", new Vector3(0f, -0.255f, -0.005f), new Vector2(0.42f, 0.06f), "Estatísticas e exportação", fundoBotao, 2.2f);
            pau.SetActive(false);

            // Tela de estatísticas (abre no lugar do menu de onde foi chamada).
            var est = new GameObject("Menu_Estatisticas");
            est.transform.SetParent(raiz.transform, false);
            Primitiva(PrimitiveType.Cube, "Fundo", est.transform, new Vector3(0f, -0.02f, 0.012f), new Vector3(0.74f, 0.64f, 0.01f), fundoPainel);
            m.menuEstatisticas = est;
            m.textoEstatisticas = Texto("Texto", est.transform, new Vector3(0f, 0.06f, 0f), 0.68f, 0.42f, 2.4f, "", TextAlignmentOptions.TopLeft, Color.white);
            m.botaoExportar = CriarBotao(est.transform, "Botao_Exportar", new Vector3(-0.17f, -0.27f, -0.005f), new Vector2(0.32f, 0.06f), "Exportar resultados (CSV)", fundoBotao, 2.2f);
            m.botaoVoltarEstatisticas = CriarBotao(est.transform, "Botao_Voltar", new Vector3(0.17f, -0.27f, -0.005f), new Vector2(0.32f, 0.06f), "Voltar", fundoBotao, 2.2f);
            est.SetActive(false);
            return m;
        }

        static BotaoVR CriarBotao(Transform pai, string nome, Vector3 posLocal, Vector2 tamanho, string rotulo, Material mat, float tamanhoTexto)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = posLocal;
            var fundo = Primitiva(PrimitiveType.Cube, "Fundo", go.transform, Vector3.zero, new Vector3(tamanho.x, tamanho.y, 0.012f), mat);
            var col = go.AddComponent<BoxCollider>();
            col.size = new Vector3(tamanho.x, tamanho.y, 0.03f);
            go.AddComponent<XRSimpleInteractable>();
            var b = go.AddComponent<BotaoVR>();
            b.fundo = fundo.GetComponent<Renderer>();
            b.rotulo = Texto("Rotulo", go.transform, new Vector3(0f, 0f, -0.008f), tamanho.x * 0.95f, tamanho.y, tamanhoTexto, rotulo, TextAlignmentOptions.Center, Color.white);
            return b;
        }

        static void CriarRotulo(Transform pai, PecaHardware peca)
        {
            Bounds b = BoundsMundo(peca.gameObject);
            // Deitado na mesa, legível de cima, logo à frente da peça (lado do usuário).
            var pose = new Pose(new Vector3(b.center.x, _topoMesa + 0.002f, b.min.z - 0.022f), Quaternion.Euler(90f, 0f, 0f));
            Texto("Rotulo_" + peca.name, pai, Vector3.zero, pose, 0.26f, 0.03f, 1.7f, peca.nomeExibicao, TextAlignmentOptions.Center, Color.white);
        }

        /// <summary>Texto 3D. Escala 0,1: 'tamanhoCm' equivale aproximadamente à altura da linha em centímetros.</summary>
        static TextMeshPro Texto(string nome, Transform pai, Vector3 posLocal, float largura, float altura, float tamanhoCm,
            string conteudo, TextAlignmentOptions alinhamento, Color cor)
        {
            return Texto(nome, pai, posLocal, null, largura, altura, tamanhoCm, conteudo, alinhamento, cor);
        }

        /// <param name="poseMundo">Se informado, ignora posLocal e usa esta posição/rotação de mundo.</param>
        static TextMeshPro Texto(string nome, Transform pai, Vector3 posLocal, Pose? poseMundo, float largura, float altura, float tamanhoCm,
            string conteudo, TextAlignmentOptions alinhamento, Color cor)
        {
            // ATENÇÃO: a pose precisa ser definida ANTES de AddComponent<TextMeshPro>(). Depois que o
            // Transform vira RectTransform, alterar transform.position perde X e Y ao salvar a cena.
            var go = new GameObject(nome);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = posLocal;
            if (poseMundo.HasValue) go.transform.SetPositionAndRotation(poseMundo.Value.position, poseMundo.Value.rotation);
            go.transform.localScale = Vector3.one * 0.1f;
            var t = go.AddComponent<TextMeshPro>();
            t.rectTransform.sizeDelta = new Vector2(largura * 10f, altura * 10f);
            t.fontSize = tamanhoCm;
            t.alignment = alinhamento;
            t.color = cor;
            t.richText = true;
            t.textWrappingMode = TextWrappingModes.Normal;
            t.overflowMode = TextOverflowModes.Overflow;
            t.text = conteudo;
            return t;
        }

        // =================================================================== Utilidades

        static GameObject Instanciar(string caminho, string nome, Transform pai)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(caminho);
            if (asset == null) throw new FileNotFoundException("[VirtuaLab] Modelo não encontrado: " + caminho);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            go.name = nome;
            if (pai != null) go.transform.SetParent(pai, true);
            return go;
        }

        static GameObject Primitiva(PrimitiveType tipo, string nome, Transform pai, Vector3 posLocal, Vector3 escala, Material mat)
        {
            var go = GameObject.CreatePrimitive(tipo);
            go.name = nome;
            var c = go.GetComponent<Collider>();
            if (c != null) Object.DestroyImmediate(c);
            go.transform.SetParent(pai, false);
            go.transform.localPosition = posLocal;
            go.transform.localScale = escala;
            if (mat != null) go.GetComponent<Renderer>().sharedMaterial = mat;
            return go;
        }

        static void CaixaEstatica(GameObject raiz, string nome, Vector3 centro, Vector3 tamanho)
        {
            var go = new GameObject(nome);
            go.transform.SetParent(raiz.transform, false);
            var c = go.AddComponent<BoxCollider>();
            c.center = centro;
            c.size = tamanho;
        }

        /// <summary>Aplica materiais aos sub-objetos do OBJ pelo sufixo numérico do nome (xxx_0, xxx_1...).</summary>
        static void Pintar(GameObject raiz, params Material[] porIndice)
        {
            if (porIndice == null || porIndice.Length == 0) return;
            foreach (var r in raiz.GetComponentsInChildren<Renderer>(true))
            {
                int idx = 0;
                int us = r.name.LastIndexOf('_');
                if (us >= 0) int.TryParse(r.name.Substring(us + 1), out idx);
                Material m = porIndice[Mathf.Clamp(idx, 0, porIndice.Length - 1)];
                if (m == null) continue;
                var arr = new Material[r.sharedMaterials.Length];
                for (int i = 0; i < arr.Length; i++) arr[i] = m;
                r.sharedMaterials = arr;
            }
        }

        /// <summary>Limites das malhas no espaço local da raiz.</summary>
        static Bounds BoundsLocais(GameObject raiz)
        {
            bool tem = false;
            Bounds b = new Bounds();
            Matrix4x4 inv = raiz.transform.worldToLocalMatrix;
            foreach (var mf in raiz.GetComponentsInChildren<MeshFilter>(true))
            {
                if (mf.sharedMesh == null) continue;
                Matrix4x4 m = inv * mf.transform.localToWorldMatrix;
                Bounds mb = mf.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    Vector3 canto = mb.center + Vector3.Scale(mb.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                    Vector3 p = m.MultiplyPoint3x4(canto);
                    if (!tem) { b = new Bounds(p, Vector3.zero); tem = true; }
                    else b.Encapsulate(p);
                }
            }
            return b;
        }

        static Bounds BoundsMundo(GameObject raiz)
        {
            var rs = raiz.GetComponentsInChildren<Renderer>(true);
            Bounds b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Coloca a peça apoiada no tampo da mesa, com o centro do volume em (x,z).</summary>
        static void PousarNaMesa(GameObject go, Quaternion rot, Vector2 centroXZ)
        {
            go.transform.SetPositionAndRotation(Vector3.zero, rot);
            Physics.SyncTransforms();
            Bounds b;
            var col = go.GetComponent<BoxCollider>();
            if (go.GetComponentInChildren<Renderer>() != null) b = BoundsMundo(go);
            else b = col.bounds;
            if (col != null) { Bounds cb = col.bounds; if (cb.min.y < b.min.y) b.min = new Vector3(b.min.x, cb.min.y, b.min.z); }
            go.transform.position = new Vector3(centroXZ.x - b.center.x, _topoMesa + 0.003f - b.min.y, centroXZ.y - b.center.z);
        }

        static Material Lit(string nome, Color cor, float metalico, float suavidade)
        {
            string caminho = PastaMateriais + "/" + nome + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", cor);
            m.SetFloat("_Metallic", metalico);
            m.SetFloat("_Smoothness", suavidade);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MaterialGuia()
        {
            string caminho = PastaMateriais + "/VL_Guia.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            // URP Unlit transparente (alpha blend), sem escrita de profundidade.
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_SrcBlendAlpha", (float)UnityEngine.Rendering.BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetFloat("_Cull", (float)UnityEngine.Rendering.CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.DisableKeyword("_ALPHATEST_ON");
            m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            m.SetColor("_BaseColor", new Color(0.25f, 0.65f, 1f, 0.30f));
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MaterialUnlit(string nome, Color cor)
        {
            string caminho = PastaMateriais + "/" + nome + ".mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", cor);
            EditorUtility.SetDirty(m);
            return m;
        }

        static Material MaterialTela()
        {
            string caminho = PastaMateriais + "/VL_Tela.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(caminho);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
                AssetDatabase.CreateAsset(m, caminho);
            }
            m.SetColor("_BaseColor", Color.black);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void RegistrarNoBuild()
        {
            var lista = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            lista.RemoveAll(s => s.path == CaminhoCena);
            lista.Insert(0, new EditorBuildSettingsScene(CaminhoCena, true));
            EditorBuildSettings.scenes = lista.ToArray();
        }
    }
}
