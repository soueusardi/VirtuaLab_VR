# 🖥️ VirtuaLab VR — Módulo Hardware

> **Simulador Imersivo em Realidade Virtual para Treinamento Prático de Montagem de Computadores**

O **VirtuaLab VR** é um projeto de simulação educacional desenvolvido como protótipo (MVP) para Trabalho de Conclusão de Curso (TCC) em Ciência da Computação. O objetivo é proporcionar um ambiente controlado e seguro onde iniciantes possam praticar o manuseio e a sequência lógica de montagem de componentes de hardware sem o risco de danificar peças reais.

---

## 🚀 Funcionalidades da Versão Atual (MVP)

- [x] **Ambiente 3D Interativo:** Bancada de trabalho laboratorial com gabinete e componentes de hardware.
- [x] **Manipulação em VR (Grab & Movement):** Suporte a pegar, mover e posicionar objetos em tempo real utilizando os controladores.
- [x] **Mecânica de Snap & Sockets:** Pontos de encaixe magnético no gabinete e na placa-mãe via *XR Socket Interactor*.
- [x] **Validação Pedagógica e Sequenciamento de Erros:**
  - 🟢 **Sucesso:** Confirmação visual de instalação correta de peças em tempo real.
  - 🟡 **Aviso:** Sinalização de remoção e alteração de estado dos componentes.
  - 🔴 **Rejeição/Erro de Ordem:** Bloqueio e alerta visual caso a placa-mãe seja inserida no gabinete sem a prévia instalação do processador (CPU).

---

## 🛠️ Tecnologias e Ferramentas Utilizadas

* **Engine:** Unity Hub 6 (v3.22.4)
* **Linguagem:** C#
* **Framework VR:** Unity XR Interaction Toolkit (XRI)
* **Render Pipeline:** Universal Render Pipeline (URP)
* **Dispositivo Alvo:** Meta Quest 2 / Quest 3S (via Virtual Desktop / OpenXR)

---

## 📂 Estrutura do Projeto

```text
Assets/
├── Assets/                # Modelos 3D de hardware, móveis e materiais
├── Scripts/
│   └── ValidadorEncaixe.cs # Script de validação de Sockets, cores de texto UI e regras de ordem
├── Scenes/
│   └── VR_PC_Demo1        # Cena principal do laboratório VR
└── XR/                    # Configurações de input e rig do XR Origin
