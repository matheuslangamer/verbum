# Interface compacta — VERBUM 0.10.1

> A barra bíblica foi revista na [versão 0.10.2](barra-leitura-0.10.2.md): os seletores e seis ações principais voltaram a ficar sempre visíveis. Este documento registra a revisão anterior.

Esta revisão recupera espaço para trabalhar com vários blocos simultâneos. As posições, divisões, notas, fontes de leitura e referências existentes são mantidas; o esquema anterior da sessão continua aceito.

## Comandos gerais

- A faixa superior ficou menor e conserva os comandos de novo bloco, originais, notas e salvamento.
- A pesquisa fica sempre visível e aceita Enter e Ctrl+F. O botão que mostra **Inteligente**, **Strong**, **Lema**, etc. abre as opções de modo, escopo e inclusão de livros/PDFs. O tooltip informa os critérios ativos.
- A lateral começa recolhida, com ícones e dicas ao passar o mouse. **☰** alterna para os nomes completos. A largura é de 52 px recolhida e 190 px expandida, contra os 220 px fixos anteriores. A escolha fica salva na sessão.
- Os títulos das abas e a barra de cada bloco ficaram menores. Títulos longos recebem reticências; a aba mantém sua referência e seu conteúdo completos. Arrastar abas e redimensionar divisões continua funcionando.

## Bíblia

A barra fixa contém **anterior**, **referência**, **próximo** e **⋯**. Digite a referência e pressione Enter. Edição, livro, capítulo e grupo ficam em **⋯ → Edição, livro e sincronização…**, que abre/recolhe os seletores dentro da aba. A preferência de abertura fica salva por aba.

O mesmo menu reúne aumento/redução de fonte, interlinear lexical, variantes/notas, marcadores e créditos. O botão direito sobre o texto continua oferecendo cópia e marcação. A faixa inferior identifica a edição, o grupo e a existência de avisos; seu tooltip e o painel de opções permitem consultar os detalhes. Os avisos de versificação não foram eliminados.

## Guia Exegético

O cabeçalho fixo conserva o título curto, a referência, **Gerar** e **⋯**. O menu oferece **Fontes e filtros…**, **Notas da passagem** e **Copiar resumo Markdown**. O painel de opções é recolhido inicialmente; sua abertura é salva na aba. Os filtros continuam aplicados mesmo quando o painel está recolhido.

As seções de texto, palavras, léxicos, sintaxe e variantes mantêm seu conteúdo e seu estado de expansão. A referência e as fontes continuam indicadas no resumo do guia.

## Documentos e ferramentas

- Nos livros/PDFs, título curto, seletor de página/seção e navegação ficam visíveis. **⋯** reúne pesquisa/modo de texto, zoom/fonte, marcações e nota da seção. Abrir um resultado de pesquisa continua exibindo os controles de busca e, em PDF, o texto extraído.
- No Estudo da palavra, a consulta e **Estudar** ficam juntos; **Fontes e modo** abre os seletores. Esse painel guarda seu estado na aba.
- O Léxico hebraico mantém fonte e consulta com cabeçalho menor, sem o título decorativo repetido.

## Verificação

As prévias WPF da própria área foram geradas com dois leitores e um guia, em [1440 px](previews/area-compacta-1440.png) e [1050 px](previews/area-compacta-1050.png). Os três blocos foram carregados sem fechar nenhum deles. As prévias usam uma sessão de teste e não salvam a sessão pessoal.

Os testes adicionais verificam a restauração da lateral e dos painéis de opções, além da abertura de sessões antigas com os novos padrões compactos. O conjunto completo preserva os testes de notas, saída com confirmação, Biblioteca, dados exegéticos e sincronização.

O comando de QA `--workspace-preview ARQUIVO.png --width 1440 --library CAMINHO` gera a área de teste sem alterar o layout salvo do usuário. Ele não substitui uma revisão manual de todos os menus.

Publicação final: 0.10.1. Foram aprovados 226 testes. As prévias de três blocos em 1440 e 1050 px e do guia em 360 px concluíram sem erro. Executáveis da raiz e de dist conferidos com SHA-256 idênticos. A cópia anterior em uso foi preservada em .tools/backups, sem criar outro executável na pasta principal.
