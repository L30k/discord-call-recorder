# Discord Call Recorder

[English](README.en.md) · Português (Brasil)

**Código disponível para uso não comercial — PolyForm Noncommercial 1.0.0.**
Permite estudar, modificar e compartilhar para os fins previstos na licença;
uso comercial exige autorização separada do titular. Não é uma licença open source
aprovada pela OSI. Consulte [LICENSE](LICENSE) e [NOTICE](NOTICE).

Grave apenas com a autorização necessária dos participantes. As gravações ficam
no seu computador; mute no Discord pode não silenciar o microfone independente do gravador.
Projeto independente, sem vínculo ou endosso do Discord.

Aplicativo pessoal para Windows, C#/.NET 10, sem bots, tokens, serviços externos ou APIs do Discord. Fica na bandeja e grava uma mistura mono do áudio **da árvore de processos do Discord** com o microfone escolhido. Não usa loopback do áudio global do Windows.

## Executar

Este repositório distribui código-fonte; ainda não há um executável em Releases.
Depois de [compilar](#compilar), abra `artifacts\Release\DiscordCallRecorder.exe`.
Mantenha a pasta Release inteira, incluindo `tools\ffmpeg.exe`, junto ao executável.
A compilação independente não precisa do runtime .NET instalado nem de administrador.

Na primeira execução, escolha uma pasta e confirme. Depois disso, use o ícone da bandeja (inclusive no menu de ícones ocultos do Windows):

- **Idioma / Language**: escolha **Português (Brasil)** ou **English** na primeira configuração ou no menu da bandeja. A troca é imediata e não interrompe uma gravação. A escolha fica salva; sem escolha anterior, o aplicativo usa português em sistemas em português e inglês nos demais. Nomes de dispositivos e mensagens nativas do Windows/FFmpeg podem permanecer no idioma de origem. A organização das pastas, com meses em português e `_gravando`, permanece igual nos dois idiomas.

- Azul: aguardando uma chamada.
- Vermelho: gravando; o tooltip mostra a duração.
- Amarelo: pausado ou falha indicada no menu.
- **Pausar** finaliza a gravação em andamento e desativa novas gravações; essa preferência persiste.
- **Retomar** reinicia a detecção, inclusive se você já estiver numa chamada.
- **Microfone**: por padrão, usa o dispositivo onde a sessão de captura do Discord foi detectada; também permite escolher outro dispositivo. A mudança vale na próxima gravação.
- Dentro de **Microfone**, **Tratamento de voz (Windows)** solicita o modo de comunicações: pode oferecer redução de ruído, controle de ganho e eco conforme os efeitos disponíveis no driver. Ativado por padrão; não equivale ao processamento/Krisp do Discord e não garante remoção de eco em qualquer dispositivo.
- **Microfone → Volume do microfone** permite de 50% a 300%, em passos de 25%. Padrão: 125%. Uma escolha já salva é preservada. A mudança vale na próxima gravação e não altera o volume do dispositivo no Windows nem o ganho do Discord. Se houver distorção, reduza para 100%; aumentar o ganho também aumenta ruído capturado.
- **Alterar local** vale para as próximas gravações, sem mover arquivos antigos.
- **Iniciar com o Windows** usa a chave Run do usuário, sem administrador; habilitada na primeira configuração.
- **Mostrar notificações** controla os avisos de início, fim e falhas. As notificações solicitam explicitamente ausência de som ao Windows.
- **Sair** finaliza o codificador e fecha o aplicativo.

Se mover a pasta do programa, desative e reative “Iniciar com o Windows” no novo local para atualizar o caminho. Para desinstalar, desative essa opção, saia e remova a pasta do programa; gravações e configurações são independentes.

## Requisitos

- Windows x64 build 20348 ou superior; Windows 11 recomendado.
- Discord desktop, Discord PTB ou Discord Canary. Discord no navegador não é detectado.
- Permissão do Windows para aplicativos desktop acessarem o microfone.
- Dispositivo de captura disponível em modo compartilhado e pasta local gravável.
- Headset recomendado: som de alto-falantes pode entrar **acusticamente** pelo microfone, mesmo que o áudio digital dos outros aplicativos esteja isolado.

## Detecção e arquitetura

Uma consulta por segundo enumera os endpoints de captura ativos e suas sessões Core Audio. Somente uma sessão `AudioSessionStateActive` pertencente a `Discord.exe`, `DiscordPTB.exe` ou `DiscordCanary.exe` dispara a detecção. Não há análise de volume, som de notificação, título de janela ou registro privado de privacidade do Windows.

A entrada precisa permanecer estável por 2 segundos; a saída, por 3 segundos. O primeiro trecho da conversa durante a confirmação não é gravado. Mute físico não interrompe a gravação enquanto o Discord mantiver sua sessão ativa. A identificação corresponde a **uso do microfone**, não à semântica interna de uma chamada: o teste de microfone nas configurações do Discord também pode disparar gravação.

O PID da sessão é associado ao processo Discord ancestral usando um snapshot de processos do Windows. `ActivateAudioInterfaceAsync` com inclusão da árvore desse processo captura apenas sua saída, independentemente do dispositivo de reprodução. Reinícios são identificados pelo PID e horário de criação. O gravador abre uma sessão de microfone própria, que não se confunde com a sessão do Discord.

Os buffers são fixos (4 segundos por fonte), alinhados aos timestamps QPC do WASAPI, com 250 ms de margem para entrega dos pacotes. Lacunas viram silêncio sem deslocar a conversa. O microfone é convertido do formato de captura para 48 kHz; as fontes são reduzidas a mono e recebem ganho base de 0,7, com multiplicador ajustável exclusivo do microfone e limite de amplitude na mistura. Não há monitoramento nos alto-falantes, cancelamento de eco próprio nem transcrição.

Um processo FFmpeg existe apenas durante a gravação. Recebe PCM por um pipe e escreve Ogg Opus continuamente, com páginas de aproximadamente um segundo. A memória não cresce com a duração da chamada. O pacote ocupa mais espaço por incluir .NET e FFmpeg; isso não representa o consumo de RAM do programa.

## Formato e arquivos

Ogg Opus (`.opus`), mono, 48 kHz, alvo de 48 kbps VBR, quadros de 20 ms, complexidade 5. Estimativa nominal: cerca de 22 MB por hora, variável conforme o conteúdo.

```text
Pasta escolhida\
  2026\
    09 - Setembro\
      21\
        18-42_20-17 [01h35m].opus
```

Durante a captura: `18-42_gravando.opus`. O nome final usa início, fim e duração; colisões recebem `(2)`, `(3)` etc. Chamadas que atravessam a meia-noite permanecem na pasta do dia de início. Não há divisão periódica em segmentos.

## Falhas e recuperação

- Fechar/crashar o Discord encerra a sessão após a tolerância de saída.
- Troca estável da sessão/dispositivo ou reinício do Discord finaliza a gravação e inicia outra quando a detecção estabiliza.
- Desconexão, erro de captura, codificador ou disco: fecha recursos, preserva o arquivo parcial e tenta novamente após 30 segundos. Pode haver um novo arquivo após recuperação; não há tentativa de costurar trechos perdidos.
- A pasta é recriada quando possível. Erros de permissão, volume ausente ou disco cheio aparecem na notificação e no log.
- Se o disco não acompanhar o áudio, o aplicativo encerra a gravação em vez de acumular buffers sem limite.
- Em fechamento normal, o pipe é fechado, o codificador finaliza e só então o arquivo é renomeado.
- Em queda de energia/encerramento forçado, `_gravando` permanece. Páginas Ogg já escritas costumam ser legíveis; os últimos pacotes podem faltar. Não há garantia de recuperação após falha do disco, nem renomeação automática de arquivos interrompidos.
- O Windows pode encerrar processos antes de terminarem o flush durante desligamento; nesse caso aplica-se o tratamento de arquivo parcial.

O mute **no aplicativo Discord** não necessariamente silencia a sessão de microfone independente do gravador. O mute físico que interrompe o sinal do dispositivo é respeitado. A captura inclui todo o áudio do processo Discord durante a sessão, inclusive notificações e outros sons dele. Não existe tentativa de separar participantes ou excluir sons internos específicos do Discord.

A V1 acompanha uma árvore de processos Discord por vez. Se houver chamadas simultâneas em instalações diferentes (por exemplo, Stable e Canary), a primeira sessão encontrada é utilizada.

## Configurações e logs

`%LOCALAPPDATA%\DiscordCallRecorder\settings.json`: pasta, dispositivo de microfone, notificações e pausa. A configuração é salva por substituição de arquivo temporário. Logs: `recorder.log`, limitado a aproximadamente 1 MB mais um arquivo anterior. Não registram conteúdo de conversas.

Inicialização automática: `HKCU\Software\Microsoft\Windows\CurrentVersion\Run\DiscordCallRecorder`.

## Compilar

Instale o [SDK .NET 10 x64](https://dotnet.microsoft.com/download/dotnet/10.0)
e [Git](https://git-scm.com/downloads). O arquivo `global.json` seleciona um SDK 10.0 estável.
As dependências NuGet ficam em `.tools\packages` quando o script é usado.

```powershell
git clone https://github.com/L30k/discord-call-recorder.git
cd discord-call-recorder
```

Obtenha FFmpeg Windows x64 com `libopus` pelos links oficiais em
[ffmpeg.org](https://ffmpeg.org/download.html) e coloque `ffmpeg.exe` em `tools`.
Confira a licença do build escolhido e [tools/README.md](tools/README.md).
O repositório não inclui esse binário nem o SDK.

```powershell
.\scripts\build.ps1 -Test
```

O script restaura dependências com versões travadas e publica em `artifacts\Release`.
Também aceita `-DotnetPath C:\caminho\dotnet.exe`. Os arquivos `packages.lock.json`
registram as versões resolvidas. Não há download de dependências quando o aplicativo está em uso.

## Testes

```powershell
dotnet restore tests\Recorder.Tests --locked-mode --configfile NuGet.Config
dotnet run --project tests\Recorder.Tests -c Release --no-restore
```

Os testes padrão não capturam áudio e podem ser executados sem FFmpeg.
São executados também pelo GitHub Actions em Windows. Para integração, compile
primeiro com FFmpeg e execute conscientemente (captura real de microfone):

```powershell
dotnet run --project tests\Recorder.Tests -c Release -- --integration "$PWD\artifacts\test-audio"
```

O teste de integração toca dois tons curtos em processos separados, mede a exclusão do processo não escolhido e captura alguns segundos do microfone junto com o alvo para finalizar um arquivo Opus. Precisa de acesso real aos dispositivos de áudio. Os resultados ficam em `artifacts\test-audio`.

Diagnóstico sem gravação, sem alterar preferências:

```powershell
.\artifacts\Release\DiscordCallRecorder.exe --diagnose "$PWD\artifacts\diagnostics.json"
```

Validação manual restante: entrar/sair de uma chamada real; mutar fisicamente; testar pausa/retomada; tocar YouTube durante a chamada; desconectar/reconectar o headset; verificar inicialização no próximo login. Testes sintéticos não substituem esses cenários no Discord e nos drivers reais.

## Dependências e referências

- .NET 10 / Windows Forms: runtime distribuído junto ao aplicativo.
- [NAudio.Wasapi e NAudio.Core 3.1.0](https://github.com/naudio/NAudio/releases): MIT; WASAPI e sessões Core Audio. O projeto usa apenas os componentes necessários.
- [FFmpeg](https://ffmpeg.org/download.html): codificador libopus e contêiner Ogg. O build usado no desenvolvimento é GPLv3; `tools\FFmpeg-LICENSE.txt`. O binário não acompanha o repositório. Distribuir executáveis exige cumprir as licenças e obrigações dos componentes incluídos; a licença não comercial do projeto não substitui as licenças de terceiros.
- [Opus](https://opus-codec.org/): codec de voz mantido pelo projeto Xiph.
- [Exemplo oficial de loopback por processo](https://learn.microsoft.com/en-us/samples/microsoft/windows-classic-samples/applicationloopbackaudio-sample/).
- [Estado das sessões de áudio](https://learn.microsoft.com/en-us/windows/win32/api/audiopolicy/nf-audiopolicy-iaudiosessioncontrol-getstate).

Não há rede em tempo de execução, telemetria do aplicativo, banco de dados nem integração com contas do Discord.

## Contribuição, segurança e histórico

Leia [CONTRIBUTING.md](CONTRIBUTING.md), [SECURITY.md](SECURITY.md) e
[CHANGELOG.md](CHANGELOG.md). Ao relatar problemas, remova caminhos pessoais e
identificadores de dispositivos; não envie gravações nem configurações pessoais.
