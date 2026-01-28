# 🎤 Tradução em Tempo Real - TraducaoRealtime

Um aplicativo .NET avançado que realiza tradução de voz em tempo real do português para inglês com suporte a saída em alto-falante ou microfone virtual.

## 📋 Sobre o Projeto

Este projeto utiliza os serviços de inteligência artificial da Azure Cognitive Services para capturar áudio em português em tempo real, traduzir para inglês e reproduzir com uma voz sintetizada de alta qualidade (Dragon HD Neural).

### Principais Funcionalidades

- 🎙️ **Captura de Áudio em Tempo Real** - Reconhecimento de fala contínuo em português (pt-BR)
- 🔄 **Tradução Automática** - Tradução de português para inglês usando Azure Translator
- 🔊 **Síntese de Voz** - Reprodução com voz sintetizada de alta qualidade (Brian Dragon HD)
- 🎧 **Múltiplas Opções de Saída**:
  - Enviar para alto-falante (você ouve o áudio traduzido)
  - Enviar para microfone virtual (outras pessoas ouvem a tradução)
- 📱 **Gerenciamento de Dispositivos** - Seleção automática e manual de dispositivos de áudio

## 🖼️ Interface

![Aplicação TraducaoRealtime](image.png)

## 🛠️ Tecnologias Utilizadas

- **.NET 9.0** - Framework principal
- **Azure Cognitive Services** - Reconhecimento e síntese de fala
- **NAudio** - Manipulação de áudio
- **C#** - Linguagem de programação

## 📋 Pré-requisitos

- .NET 9.0 ou superior
- Conta Azure com serviço Speech ativo
- Chave de subscrição do Azure

## 🚀 Configuração

### 1. Obtenha sua Chave Azure

1. Acesse o [Portal Azure](https://portal.azure.com)
2. Crie um recurso "Speech" (Serviço de Fala)
3. Copie sua chave de subscrição

### 2. Configure o Projeto

Crie um arquivo `.env` na raiz do projeto:

```
AZURE_SUBSCRIPTION_KEY=sua_chave_aqui
```

Ou defina a variável de ambiente:

```powershell
$env:AZURE_SUBSCRIPTION_KEY="sua_chave_aqui"
```

## 💻 Como Usar

1. **Instale as dependências:**
   ```bash
   dotnet restore
   ```

2. **Execute o aplicativo:**
   ```bash
   dotnet run
   ```

3. **Siga as instruções na tela:**
   - Escolha entre enviar áudio para alto-falante ou microfone virtual
   - Se escolher alto-falante, selecione o dispositivo de saída desejado
   - O aplicativo começará a capturar e traduzir áudio

## 📦 Estrutura do Projeto

```
.
├── Program.cs              # Código principal do aplicativo
├── TraducaoRealtime.csproj # Definição do projeto
├── traducao.sln            # Solução Visual Studio
├── .env                    # Variáveis de ambiente (não comitar)
├── .env.example            # Exemplo de configuração
└── image.png              # Imagem da interface
```

## ⚙️ Configurações Principais

No arquivo `Program.cs` você pode customizar:

- `RecognitionLanguage` - Idioma de entrada (padrão: pt-BR)
- `TranslationTargetLanguage` - Idioma de tradução (padrão: en-US)
- `SynthesisLanguage` - Idioma de síntese (padrão: en-US)
- `VoiceName` - Voz Neural (padrão: Brian Dragon HD)
- `ServiceRegion` - Região do Azure (padrão: eastus)

## 🔒 Segurança

⚠️ **IMPORTANTE**: Nunca comite sua chave Azure no repositório!

- A chave é lida do arquivo `.env` ou variável de ambiente
- O arquivo `.env` está no `.gitignore`
- Use `.env.example` como template para outros desenvolvedores

## 📝 Exemplo de Uso

```csharp
// O aplicativo:
// 1. Captura: "Olá, como você está?"
// 2. Traduz para: "Hello, how are you?"
// 3. Sintetiza e reproduz em voz Dragon HD
```

## 🤝 Contribuindo

Sinta-se à vontade para fazer fork, criar branches e submeter pull requests!

## 📄 Licença

Este projeto é fornecido como está. Consulte os termos de serviço da Azure Cognitive Services.

## 📞 Suporte

Para problemas com a Azure Cognitive Services, consulte:
- [Documentação Azure Speech](https://learn.microsoft.com/pt-br/azure/ai-services/speech-service/)
- [NAudio Documentation](https://github.com/naudio/NAudio)

---

**Desenvolvido com ❤️ usando .NET e Azure AI**
