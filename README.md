# DualSense Reader 🎮

Um utilitário em **C# / .NET** para leitura e análise em tempo real dos dados enviados pelo controle **PlayStation 5 DualSense** via protocolo **HID (Human Interface Device)** por conexão USB ou Bluetooth.

---

## 🎯 Objetivos do Projeto

- [x] Conexão e detecção do dispositivo HID (Sony DualSense).
- [x] Leitura contínua dos relatórios de entrada (*Input Reports*).
- [ ] Mapeamento completo dos analógicos (LX, LY, RX, RY).
- [ ] Mapeamento dos botões (D-Pad, Ações, Ombro, Triggers e Touchpad).
- [ ] Leitura do giroscópio e acelerômetro.
- [ ] Envio de relatórios de funcionalidade (*Feature Reports*) para recalibração dos sensores e analógicos.
- [ ] Interface gráfica (GUI) dedicada.

---

## 🛠️ Tecnologias Utilizadas

- **Linguagem:** C# (.NET 8.0+)
- **Biblioteca HID:** [HidSharp](https://www.nuget.org/packages/HidSharp)
- **Plataforma Target:** Windows (suporte a Linux/macOS via .NET Cross-Platform)

---

## 🎮 Dispositivos Suportados

| Dispositivo | Vendor ID (VID) | Product ID (PID) |
| :--- | :---: | :---: |
| DualSense Standard (PS5) | `0x054C` | `0x0CE6` |
| DualSense Edge (PS5) | `0x054C` | `0x0DF2` |

---

## 🚀 Como Executar o Projeto

### Pré-requisitos
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download) ou superior instalado.
- Um controle DualSense conectado via cabo USB ou Bluetooth.

> **Nota:** Certifique-se de fechar softwares como **Steam** ou **DS4Windows** antes de executar, pois eles podem tomar posse exclusiva da comunicação HID com o controle.

### Passo a Passo

1. **Clonar o repositório:**
   ```bash
   git clone https://github.com/fabinhose12/dualsense-reader.git
   cd dualsense-reader/DualSenseReader
   ```

2. **Restaurar dependências:**
   ```bash
   dotnet restore
   ```

3. **Executar a aplicação:**
   ```bash
   dotnet run
   ```

---

## 📂 Estrutura do Projeto

```text
DualSenseReader/
├── Program.cs           # Ponto de entrada e loop de leitura HID
├── DualSenseReader.csproj # Arquivo de configuração e dependências do .NET
└── .gitignore           # Filtro de arquivos compilados e temporários
```

---

## 📝 Licença

Este projeto é de uso livre para fins de aprendizado e desenvolvimento pessoal.
