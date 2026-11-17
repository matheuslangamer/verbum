# Layout e temas — VERBUM 0.11.0

## Cópia anterior

Backup criado antes das alterações em `.tools/backups/layout-before-redesign-20261113-214152`: código, documentação, ferramentas, executável 0.10.4 com SHA-256, componentes nativos e biblioteca completa. A subpasta `dados-pessoais` contém cópia da sessão, notas e arquivos de marcações existentes de `%LOCALAPPDATA%/Verbum`. A cópia preserva o estado gravado em disco naquele momento; não captura alterações ainda não gravadas por outro processo aberto.

## Direção visual

Os modelos fornecidos orientaram o contraste do modo escuro, os ícones de traço e a separação entre ferramentas e leitura. Foram mantidos os controles compactos que já funcionavam: pesquisa ao lado de VERBUM, seletores visíveis, seis ações diretas do leitor e divisão dos blocos. O tema claro usa branco e cinza frio; o escuro usa grafite e texto claro. Os acentos usam azul discreto. As fontes hebraicas e a tipografia de leitura foram preservadas.

O botão de lua/sol no rodapé da lateral alterna o tema imediatamente. A preferência fica em `appearance.txt`, separada da sessão. As cores são atualizadas nos objetos já existentes, sem reconstruir leitores, abas ou divisões. Seletores e barras de rolagem acompanham o tema. Marcações pessoais usam suas cores originais, inclusive códigos hexadecimais personalizados. Imagens e páginas originais de PDF permanecem intactas. Diálogos nativos do Windows seguem a aparência do sistema operacional.

## Ícones

SVGs do [repositório oficial Lucide](https://github.com/lucide-icons/lucide), baixados em 13/11/2026 e incorporados como recursos WPF locais. A [licença ISC](https://github.com/lucide-icons/lucide/blob/main/LICENSE) está junto dos arquivos em `src/Verbum/Assets/Icons/LICENSE` e na distribuição em `native/licenses/lucide-LICENSE.txt`. Os ícones não exigem internet durante o uso. O manifesto local registra SHA-256 dos arquivos baixados.

## Verificação

229 verificações aprovadas, nenhuma falha. Incluem troca de cores existentes claro/escuro/claro, preservação de cores pessoais, carregamento offline de todos os ícones e contraste mínimo de texto e botões. Os testes WPF foram integrados ao dispatcher da aplicação para validar os controles com os recursos reais do tema. Continuam as verificações de navegação, notas, marcações, sincronização, guia e barra responsiva.

As prévias realizam uma troca de tema de ida e volta depois que os leitores terminam de carregar, sem salvar preferências ou sessão. Foram inspecionadas visualmente:

- [Claro — 1440 px](previews/layout-light-1440.png)
- [Escuro — 1440 px](previews/layout-dark-1440.png)
- [Claro — 1050 px](previews/layout-light-1050.png)
- [Escuro — 1050 px](previews/layout-dark-1050.png)

A publicação utilizou dependências já restauradas. O serviço NuGet de auditoria de vulnerabilidades estava indisponível (aviso NU1900); compilação e testes foram concluídos. Nenhum dado pessoal de marcação ou nota foi modificado pelos testes.
