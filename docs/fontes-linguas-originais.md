# Línguas originais, Strong e aparato — pesquisa para o VERBUM

Data: 12 de novembro de 2026. Finalidade informada pelo usuário: uso pessoal e estudo.

Este documento registra a pesquisa das fontes. Atualização: os recursos BHS-A-Strong, WLC-OSHB e SBLGNT foram incorporados à biblioteca da versão 0.2.0, separados do EXE, com metadados de origem. O manifesto `original-resources-import.json` registra revisões, contagens e hashes. As propostas abaixo descrevem o contexto da pesquisa; consulte o README para as funções efetivamente disponíveis. O PDF BHS e o aparato manuscrito BHS/NA28 não foram incorporados.

## Fontes fornecidas e verificadas

### BHS-Strong-no / OpenHebrewBible

- https://github.com/eliranwong/BHS-Strong-no
- https://github.com/eliranwong/OpenHebrewBible
- https://github.com/ETCBC/bhsa

O BHS-Strong-no mapeia Strong para a base hebraica identificada como BHS/BHSA e encaminha para o OpenHebrewBible como continuidade do trabalho. O README distingue números convencionais e seis códigos estendidos (H9003, H9004, H9005, H9008, H9000 e H9009). Esses códigos precisam conservar a identificação da convenção usada.

O OpenHebrewBible descreve alinhamentos BHSA–WLC, dados ETCBC, Strong e morfologia do Open Scriptures, alinhamentos com traduções específicas, transliteração e segmentação. Suas obras de mapeamento e a base ETCBC/BHSA citada declaram CC BY-NC 4.0. O repositório antigo também exibe um LICENSE GPL, enquanto o README atribui condições próprias aos dados. A identificação de licença precisa acompanhar cada componente, sem tratar o arquivo LICENSE da raiz como uma autorização única para todas as fontes.

A disponibilidade de alinhamento com Berean/KJV não constitui um alinhamento palavra a palavra com ALM1911, TB ou as outras traduções portuguesas locais.

### Open Scriptures Hebrew Bible

- https://github.com/openscriptures/morphhb
- https://raw.githubusercontent.com/openscriptures/morphhb/master/LICENSE.md

Oferece WLC em OSIS XML, atributos de lema com Strong ampliado, morfologia e identificadores de palavra. O projeto declara o texto WLC em domínio público e as anotações de lema/morfologia sob CC BY 4.0. É uma alternativa estruturada para leitura hebraica, palavras clicáveis e buscas por códigos, mas WLC deve ser identificado como WLC, sem receber indevidamente o nome BHS.

O README alerta para particularidades de normalização hebraica e oferece opções explícitas de remapeamento de versificação. O VERBUM deve guardar a sequência Unicode original integralmente; normalizações são apenas chaves de busca. Deve também distinguir a referência original da referência remapeada.

### Recursos digitais da SBL / Sociedade Bíblica Alemã

- https://www.sbl-site.org/resources/digital-texts/
- https://www.sbl-site.org/wp-content/uploads/2024/06/BHS_SBL.pdf

A página da SBL descreve os recursos da Sociedade Bíblica Alemã para membros como textos de leitura sem aparato: BHS, Vulgata, UBS Greek New Testament e Septuaginta. A disponibilidade para leitura/download não estabelece uma licença irrestrita para redistribuir cada edição.

O link específico do PDF retornou HTTP 403 na ferramenta de navegação. Não foram inspecionadas suas páginas, integralidade, edição ou notas de direitos. O enunciado sobre ausência de aparato vem da página da SBL, não de inspeção direta desse PDF.

Para processamento, os dados estruturados são preferíveis ao PDF: preservam referências e permitem associar palavras a anotações. O PDF pode permanecer como material de consulta visual.

### SBL Greek New Testament

- https://github.com/Faithlife/SBLGNT
- https://sblgnt.com/license/
- https://github.com/Faithlife/SBLGNT/tree/master/data/sblgnt
- https://github.com/Faithlife/SBLGNT/tree/master/data/sblgntapp

O repositório oficial contém texto e aparato em XML e texto. A versão pública atual consultada declara CC BY 4.0, com atualização de licença registrada em 2022; alguns PDFs antigos ainda contêm condições anteriores. Ao importar, registrar a revisão e os termos que acompanham a versão efetivamente obtida.

O aparato SBLGNT compara leituras de outras edições publicadas. A própria SBL o distingue de um aparato que relaciona leituras diretamente aos testemunhos manuscritos. É um candidato concreto para um primeiro painel de variantes, com identificação precisa do seu alcance; não deve ser apresentado como aparato NA28, UBS ou BHS.

## Camadas que o programa precisa distinguir

| Camada | Dados necessários | Uso |
|---|---|---|
| Texto | Edição, idioma, livro, capítulo, referência e Unicode original | Leitura e busca da forma escrita |
| Palavra/segmento | Identificador estável, ordem, grafia, lema, Strong e morfologia | Seleção de palavra e busca lexical |
| Léxico | Identificadores e verbetes com fonte e idioma | Definições e informações lexicais |
| Interlinear | Alinhamentos explícitos entre segmentos originais e traduções | Apresentação palavra a palavra |
| Aparato | Edição base, local, lema citado, leituras, fontes/testemunhos e notas | Comparação textual documentada |

Strong é um identificador lexical, não um significado fixo nem uma tradução automática. Uma forma de palavra pode envolver prefixos, sufixos ou múltiplos segmentos/códigos. Também é necessário distinguir leituras qere/ketiv quando a base as registrar.

## Modelo de integração proposto

1. Um manifesto de recurso registra título, identificador da edição, idioma, fontes, atribuições, licença, revisão do repositório, hashes e sistema de versificação.
2. Livros recebem identificadores canônicos explícitos. O modelo atual usa posições de uma coleção de 66 livros; precisa ser ampliado para recursos de 39 e 27 livros e para ordens diferentes.
3. O texto original conserva os sinais e sua ordem original. Chaves de busca separadas podem representar forma exata, forma sem cantilação, forma sem sinais, lema e código Strong.
4. Cada palavra ou segmento guarda uma lista de Strong, não um único número obrigatório. Códigos estendidos ficam marcados como extensão da fonte.
5. O painel lexical mostra apenas campos realmente fornecidos: grafia, lema, transliteração, análise e verbete, com ausência explícita quando não disponíveis.
6. A seleção de Strong oferece busca de ocorrências por identificador. Essa busca é distinta da pesquisa de consoantes sem sinais e da busca por lema.
7. Alinhamentos preservam relações um-para-muitos, muitos-para-um e lacunas. Uma tradução portuguesa sem alinhamento editorial continua disponível em paralelo por referência, sem vínculos de palavra inventados.
8. O aparato é um recurso separado ligado à sua edição base. Primeiro candidato grego: SBLGNT, com rótulo de comparação entre edições. Um aparato hebraico requer uma fonte própria adicional; os materiais examinados não fornecem automaticamente o aparato crítico impresso da BHS.

## Ordem de trabalho recomendada

Primeiro, leitura hebraica com Strong e seleção de palavras, usando uma revisão fixada e uma passagem piloto. Depois, busca por Strong/lema e expansão ao corpus completo, com testes de versificação e qere/ketiv. Em seguida, grego SBLGNT e painel de variantes entre edições. Interlinear em português depende de uma base alinhada específica ou de trabalho de alinhamento próprio identificado e revisado.
