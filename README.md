# VirtuaLab VR — Módulo Hardware

> Ambiente imersivo em Realidade Virtual para treinamento prático de montagem de computadores.

Protótipo educacional desenvolvido como Trabalho de Conclusão de Curso em Ciência da Computação (UNIJORGE).
Oferece uma bancada virtual segura na qual iniciantes identificam, manipulam e instalam componentes de
hardware, sem custo de peças nem risco de dano.

## Funcionalidades

- Bancada com gabinete aberto, peças rotuladas e telão de instruções em português.
- Montagem em 7 etapas: placa-mãe, processador, pasta térmica, cooler, memória RAM, SSD M.2 e placa de vídeo
  (a fonte já vem instalada).
- Pegar e soltar com os controles do Meta Quest (botão lateral *grip*, dedo médio), com destaque do botão no
  controle até a primeira pegada.
- Validador de encaixe próprio: confere tipo da peça, distância, ordem de montagem e orientação física
  (triângulo da CPU, chanfro da RAM, chave M do SSD), com tolerâncias calibráveis.
- Painel junto à mão com nome e função da peça segurada.
- **Modo guiado (aprender):** guia azul do destino, verde/vermelho ao aproximar, peça rejeitada é repelida com
  som e explicação, etapas reveladas uma a uma.
- **Modo livre (demonstrar):** sem indicadores. A peça só encaixa no lugar e na orientação certos; ao final o
  participante aperta o **Power**, que liga o computador ou mostra o diagnóstico no monitor. Peças instaladas
  podem ser retiradas para corrigir.
- Menu inicial, menu de pausa (botão **B**) e tela de estatísticas com exportação dos CSV, tudo dentro do headset.
- Cronômetro, acertos, erros por categoria (local, ordem, orientação), falhas no Power e peças retiradas,
  gravados em CSV por sessão.

Fora do escopo: cabos, parafusos e sistema operacional real.

## Requisitos

- Unity **6000.6.6f1** (URP), XR Interaction Toolkit 3.6.1, OpenXR 1.18.0, Meta OpenXR 2.6.1, Input System 1.20.0
- Meta Quest 3S + PC com o aplicativo Meta Quest Link (cabo ou Air Link)
- Git LFS (os modelos `.obj` são versionados com LFS)

## Como executar (Quest Link)

1. Ligue o Quest e ative o **Quest Link**. No aplicativo do PC, defina o Meta Quest Link como runtime OpenXR.
2. Abra este projeto no Unity Hub. A cena `Assets/Scenes/VirtuaLab_Montagem.unity` abre sozinha
   (na primeira abertura, aguarde a importação terminar).
3. Pressione **Play**. No headset, escolha o modo no menu inicial e aperte **Iniciar montagem**.

Teclas do pesquisador (clique antes na janela *Game*):

| Tecla | Ação |
|---|---|
| `N` | Próximo participante (inicia em familiarização, não avaliada) |
| `R` | Reiniciar como nova tentativa do mesmo participante |
| `G` | Alternar modo guiado / livre |
| `P` | Acionar o Power (contingência) |
| `PgUp` / `PgDn` | Subir / descer o usuário 2 cm |
| Setas | Mover o usuário 5 cm |
| `Home` | Zerar os ajustes |
| `H` | Mostrar / ocultar o quadro do pesquisador |
| `Esc` | Menu de pausa (no headset: botão **B**) |

Os registros ficam em `RegistrosSessoes/` (`resumo_sessoes.csv` e `eventos_*.csv`). Essa pasta não é
versionada, pois contém dados de participantes.

## Estrutura

```text
Assets/
├── Scenes/VirtuaLab_Montagem.unity   # cena principal (gerada pelo construtor)
├── Scripts/VirtuaLab/                # peças, pontos de encaixe, gerenciador, menus, painéis, boot, registro
├── Editor/VirtuaLabSceneBuilder.cs   # constrói a cena e concentra a calibração
├── Tests/PlayMode/                   # testes automatizados da lógica de montagem
├── VirtuaLab/                        # materiais e imagens do boot
└── *.obj                             # modelos 3D das peças, gabinete e mesa
```

## Desenvolvimento

- Não é preciso reconstruir a cena para jogar: ela já está salva no repositório.
- A cena é gerada por código. **Só** para mudar posições, tolerâncias, textos ou etapas, edite
  `Assets/Editor/VirtuaLabSceneBuilder.cs` e use o menu **VirtuaLab > Construir cena de montagem**.
  Alterações feitas à mão na cena são perdidas na reconstrução.
- Testes: *Window > General > Test Runner > PlayMode > Run All*.

## Situação

Avaliação com usuários em andamento
(novembro/2026).
