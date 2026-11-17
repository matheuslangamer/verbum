# Barra de leitura — revisão de usabilidade 0.10.2

## Problema corrigido

A versão 0.10.1 compactou a interface, mas colocou comandos frequentes do leitor dentro de um menu. Isso aumentou o número de cliques para mudar edição, livro, capítulo, grupo e tamanho de fonte. Esta revisão mantém a lateral compacta e devolve acesso direto a esses controles.

**Sempre visíveis no leitor bíblico:**

- Referência digitável por Enter, anterior e próximo capítulo.
- Edição, livro, capítulo e sincronização Livre/A/B/C.
- **A−**, **A+**, **≡**, **V**, **▰**, **ⓘ**.

Os seis botões têm altura uniforme de 28 px, alvos de clique de 29 px de largura, dicas e nomes acessíveis. O interlinear indica visualmente quando está ativo. Marcações continuam disponíveis também pelo botão direito. **ⓘ** reúne edição, avisos de versificação e créditos completos.

## Adaptação à largura

As três unidades — navegação, seletores, ações — permanecem no mesmo controle. O redimensionamento muda suas linhas/colunas; não recria os seletores, não perde a seleção e não os esconde no menu.

| Espaço disponível na barra | Disposição |
|---|---|
| 720 px ou mais | Referência, seletores e seis ações na mesma linha |
| 480–719 px | Navegação e seletores juntos; ações na linha seguinte |
| 350–479 px | Seletores na primeira linha; navegação e ações na segunda |
| Abaixo de 350 px | Seletores, navegação e ações em linhas curtas |

Em blocos muito estreitos, os quatro seletores passam para duas colunas, e as ações podem quebrar linha. Eles permanecem disponíveis. Nomes longos podem ocupar parcialmente o campo; a lista e a dica mantêm o valor completo. O tamanho do texto bíblico não é reduzido para fazer a barra caber.

Os limiares são uma decisão própria de layout WPF baseada nos mínimos de espaço dos grupos. Não são constantes recuperadas do Logos.

## O que foi verificado no Logos com REA

Artefato instalado: `C:\Users\mathe\AppData\Local\Logos\System\Faithlife.ResourceToolbarHost.dll`.

- Componente: `Faithlife.ResourceToolbarHost`, versão 12.71.0.0.
- SHA-256 do assembly: `69fc4103a4b511e139877c93d96db469a85f7dea44668c26e524e60bdfd95dcd`.
- MVID: `618639f1-711d-492b-bca7-7c66e7be687c`.
- O REA inventariou os recursos embutidos. Os arquivos selecionados foram lidos por offsets PE/CLI sem carregar o assembly, e seus hashes foram conferidos.
- Bundle principal: `dist/resource-toolbar-host.js`, SHA-256 `c93bdd4528d67bf05c04b1e76a7e94cd9c1600e8818b9b1a20d278489af241c8`.
- Há um componente próprio de referência (`ReferenceNavigator`) com texto, sugestões, envio, foco, rótulo de acessibilidade e índice de tabulação.
- O host compõe `ResourcePanelToolbar.Top` e `ResourcePanelToolbar.Secondary`, com grupos Home, Search, Notes, View, Tools, Share e Formatting.
- `homeTabProps` liga o conjunto de sincronização a `linkSetOption`/`onLinkSetClick`, além dos comandos de navegação.
- Os controles de apresentação recebem `contentZoomIndex` e configurações de interlinear separadas.

Esses são fatos estáticos dos recursos instalados. Não houve observação da sessão do Logos em execução nem comprovação do layout em todos os tamanhos. A análise JavaScript do REA foi parcial: o bundle depende de outros módulos, e limites de aliases e nós semânticos foram atingidos. A inspeção não recuperou uma especificação completa de overflow.

Registros selecionados: [evidências REA](logos-barra-leitura-rea-evidencias.json). Pai da análise JavaScript: `ev_10f1563f131a4052a088b20cb3bd7d92beef4465b5886f8cdd997dd28462cdfb`.

## Critério de engenharia aplicado

A separação do Logos orientou a distinção entre navegar, operar sobre o recurso e configurar ferramentas. O VERBUM não copiou o código do Logos e não reproduz suas abas de toolbar: mantém os comandos frequentes do fluxo do usuário diretamente acessíveis.

A orientação da Microsoft para barras de comandos prioriza tarefas frequentes na área principal, agrupamento lógico e adaptação ao espaço. Aqui isso foi traduzido em grupos WPF que reorganizam as linhas, com visibilidade, ordem de foco estável, dicas e testes em diferentes larguras.

Fontes públicas consultadas em 13/11/2026:

- [Logos — equivalências da barra dinâmica](https://support.logos.com/hc/en-us/articles/29439681440525-Where-do-I-find-on-the-dynamic-toolbar).
- [Microsoft — Command bar](https://learn.microsoft.com/en-us/windows/apps/develop/ui/controls/command-bar).

## Validação

O teste WPF mede a presença e os limites dos quatro seletores e seis botões em 170, 230, 300, 400, 600 e 820 px, incluindo a volta à largura maior. Também exercita aumento/redução de fonte e alternância do interlinear. As verificações de Bíblia, sincronização, notas, confirmação de saída, Biblioteca e Guia Exegético continuam no conjunto geral.

Nenhuma Bíblia, nota pessoal ou divisão de estudo foi modificada para executar a revisão ou as prévias.

Resultado: **229 testes aprovados, nenhuma falha**. As prévias foram geradas e inspecionadas com um bloco em 1440 px, dois blocos em 1440 px e três blocos em 1050 px:

- [Leitor amplo](previews/barra-leitura-ampla.png)
- [Dois leitores](previews/barra-leitura-dupla.png)
- [Três blocos](previews/barra-leitura-estreita.png)
