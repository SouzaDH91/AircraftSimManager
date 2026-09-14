[![GitHub release](https://img.shields.io/github/v/release/SouzaDH91/AircraftSimManager?style=for-the-badge)](https://github.com/SouzaDH91/AircraftSimManager/releases/latest)


# ✈️ Aircraft Sim Manager

[Português](#português) | [English](#english)

---

<a name="português"></a>
## 🇧🇷 Português

O **Aircraft Sim Manager** é uma suíte modular desktop desenvolvida para entusiastas e pilotos virtuais de simuladores de voo (**Microsoft Flight Simulator 2020 / 2024, Lockheed Martin Prepar3D e FSX**).

---

### ⚠️ Compatibilidade e Suporte de Aeronaves

- **Aeronaves Suportadas Atualmente**: O programa foi desenvolvido e otimizado especificamente para **aeronaves da PMDG**.
- **Testado e Homologado em**: **PMDG 737 NGXu no Lockheed Martin Prepar3D v5**.
- **Outros Simuladores / Modelos**: O funcionamento no MSFS 2020, MSFS 2024, FSX e outros modelos da PMDG requer validação adicional da comunidade.
- **Expansão Futura**: Pretendemos expandir o suporte para aeronaves de outras desenvolvedoras (como Fenix, FlyByWire, iFly, Leonardo, entre outras) em atualizações futuras.

---

### 💡 Por que este projeto foi desenvolvido?

A ideia do projeto surgiu quando a **PMDG** descontinuou/removeu o **Operations Center v2 (OC2)** em determinadas transições de softwares, deixando os usuários sem uma ferramenta oficial simples e acessível para instalar *liveries* distribuídas no formato proprietário `.PTP`. 

Inicialmente, o software foi concebido como uma solução rápida para descompactar, converter e instalar arquivos `.PTP` e `.ZIP` nos simuladores. 

Com a evolução do código, o projeto foi reestruturado para se tornar uma **suíte completa para simulação de voo**, onde ferramentas como calculadoras de combustível, peso e balanceamento (CG), performance de decolagem e integração de dados meteorológicos (METAR) serão integradas gradualmente.

---

### 🚀 Funcionalidades

#### 🟢 Disponível na Versão Atual (v1.0.0):
- **🖌️ Gerenciador de Liveries (Livery Manager)**:
  - Escaneamento automático de pinturas instaladas.
  - **Instalação direta e automatizada** de pacotes PMDG (`.PTP`) e pacotes compactados (`.ZIP`) via *drag-and-drop* (arrastar e soltar).
  - Ferramenta de conversão dedicada de pacotes criptografados `.PTP` para arquivos `.ZIP`.
  - Edição de títulos de aeronaves no `aircraft.cfg` e remoção limpa de texturas sem desorganizar as seções `[fltsim.X]`.
  - Atualização automática do arquivo `layout.json` (para MSFS 2020/2024).

#### 🟡 Em Desenvolvimento (In Coming):
- **⛽ Fuel & Route Calculator**: Cálculo de combustível por rota (Trip, Alternate, Holding, Contingência) com injeção via SimConnect.
- **🛫 Performance Calculator**: Cálculo de velocidades de decolagem ($V_1$, $V_R$, $V_2$), ajustes de Trim, Flaps e Temperatura Assumida/FLEX.

---

### 📖 Como Instalar Texturas PTP ou ZIP usando o Livery Manager

1. Abra o **Aircraft Sim Manager**.
2. No menu superior, acesse **Ferramentas (Tools)** ➔ **Gerenciador de Liveries**.
3. Selecione o seu simulador (ex: *Prepar3D v5*, *MSFS 2020*, *FSX*, etc.).
4. **Instalação Direta**:
   - Arraste o arquivo `.PTP` ou `.ZIP` diretamente para a área indicada na tela (**"Arraste o arquivo .PTP ou .ZIP aqui para instalar"**).
   - O aplicativo descompactará e instalará a pintura automaticamente.
5. **Opção de Conversão**:
   - Para apenas converter um arquivo `.PTP` para `.ZIP` sem instalar imediatamente, use o botão **"Converter .PTP para .ZIP"**.

---

### 🛠️ Tecnologias Utilizadas

- **Linguagem**: C# (.NET 8)
- **Interface**: WPF (Windows Presentation Foundation) com arquitetura **MVVM**.
- **Bibliotecas**: `System.IO.Compression`, `Microsoft.Win32.Registry`, `SimConnect SDK`.

---

### ☕ Apoie o Desenvolvimento (Doações)

Se o **Aircraft Sim Manager** facilitou a sua simulação de voo e você gostaria de apoiar o desenvolvimento das próximas ferramentas (Fuel & Performance Calculators), considere fazer uma doação!

#### 🇧🇷 Doação via PIX (Brasil)

> **Chave PIX**: `35bc9f24-7e9a-4b6b-94a8-2474a62efe39`

---

<a name="english"></a>
## 🇬🇧 English

**Aircraft Sim Manager** is a modular desktop suite designed for flight simulation enthusiasts using **Microsoft Flight Simulator 2020 / 2024, Lockheed Martin Prepar3D, and FSX**.

---

### ⚠️ Compatibility & Supported Aircraft

- **Currently Supported Aircraft**: Tailored specifically to work with **PMDG aircraft**.
- **Tested & Validated On**: **PMDG 737 NGXu on Lockheed Martin Prepar3D v5**.
- **Other Simulators / Models**: Functionality on MSFS 2020, MSFS 2024, FSX, and other PMDG models will require community testing.
- **Future Expansion**: Support for third-party aircraft (Fenix, FlyByWire, iFly, Leonardo, etc.) is planned for future releases.

---

### 🚀 Features

#### 🟢 Available in Current Version (v1.0.0):
- **🖌️ Livery Manager**:
  - Automatic scanning of installed liveries.
  - **Direct installation** of PMDG packages (`.PTP`) and `.ZIP` files via **drag-and-drop**.
  - Built-in converter from encrypted `.PTP` to `.ZIP` archives.
  - Aircraft title editing inside `aircraft.cfg` and clean removal of texture folders.
  - Automatic rebuild of `layout.json` (for MSFS 2020/2024).

#### 🟡 Under Development (In Coming):
- **⛽ Fuel & Route Calculator**: Fuel estimation and SimConnect integration.
- **🛫 Performance Calculator**: $V_1, V_R, V_2$ takeoff speeds, Trim, Flaps, and FLEX/Assumed Temperature calculations.

---

### 🌐 International Donations (Ko-fi)

[![Ko-Fi](https://img.shields.io/badge/Ko--fi-Donate%20a%20Coffee-red?style=for-the-badge&logo=ko-fi)](https://ko-fi.com/aircraftsimmanager)