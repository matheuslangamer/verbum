# Notas e vínculos — 0.12.0

## Acesso às notas

O botão Notas da lateral abre uma aba com a lista pesquisável por título e conteúdo. As notas abertas prevalecem sobre suas cópias salvas, evitando mostrar uma versão antiga durante a edição. A abertura reutiliza a aba existente em qualquer bloco; notas fechadas podem ser reabertas dos arquivos JSON. Arquivos ilegíveis são preservados e reportados na barra de status.

## Editor

Mantém Markdown como fonte editável, sem converter ou reescrever as notas antigas. Inclui comandos de negrito, itálico, título, listas, citação, desfazer/refazer e criação de vínculos. A prévia oferece títulos, ênfase, listas simples, citações, código e links. Não pretende implementar toda a especificação CommonMark. Prévia e edição podem ser vistas lado a lado a partir de 620 px de largura útil; em blocos menores, ficam disponíveis separadamente. Atualizações da prévia são agrupadas por 250 ms para evitar uma renderização a cada tecla.

O modo e a seleção do editor são propriedades adicionais da aba; notas anteriores recebem os padrões de edição, sem migração destrutiva. O salvamento automático existente de JSON e MD permanece ativo.

## Vínculos

- Bíblia: edição e referência explícitas, incluindo intervalo do mesmo capítulo, validadas contra a biblioteca. Abre o início da passagem, respeitando a numeração da edição escolhida.
- Nota/comentário pessoal: identidade da nota, preservada mesmo quando seu título muda.
- Livro/comentário importado: identidade do documento e índice de página/seção. Requer o documento na biblioteca atual.
- Web: http/https, aberto no navegador somente quando o usuário clica.

Os destinos internos usam URIs `verbum://` guardadas no Markdown e tratadas pelo próprio leitor de prévia. Não exigem instalar protocolo no Windows. Outros programas podem exibir esses links sem saber abri-los. Destinos ausentes produzem uma mensagem, mantendo o texto da nota. URLs de arquivo e scripts não são executadas. A renderização não acessa a rede.

## Proteção e verificação

Backup anterior: `.tools/backups/before-notes-editor-20261113-222841`, incluindo código, README, executável e dados pessoais gravados em disco. Nenhum teste de escrita utilizou as notas pessoais.

235 testes aprovados, nenhuma falha. Novas verificações cobrem identidades dos destinos, referências com edição, seções de livros, esquemas inválidos, serialização do catálogo e estado do editor, renderização e clique de links, formatação da seleção e inserção de vínculo sem apagar o texto ao redor. Os controles WPF foram exercitados no dispatcher da aplicação.

Prévias com notas sintéticas, sem salvar sessão ou preferências pessoais:

- [Editor e prévia — claro](previews/notas-light.png)
- [Editor e prévia — escuro](previews/notas-dark.png)
- [Editor em bloco menor — escuro](previews/notas-dark-1050.png)

A publicação utilizou dependências já restauradas. O aviso NU1900 indica que a auditoria NuGet online estava indisponível; compilação e testes foram concluídos.
