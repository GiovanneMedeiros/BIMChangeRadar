# Relatório de auditoria e testes — BIM Change Radar

Data da auditoria: 25/11/2026 · Ambiente: macOS (Apple Silicon), .NET SDK 8.0.428, Avalonia 11.2.3.

> Tudo abaixo foi **executado**; os números vêm das saídas reais dos comandos. O que **não** foi possível testar está listado na seção 9.

## 1. Resumo
| Item | Resultado |
|---|---|
| Compilação (`dotnet build --no-incremental`) | **0 erros, 0 avisos** |
| Testes automatizados (xUnit) | **149 executados, 149 aprovados, 0 falhas** (eram 25 antes da auditoria) |
| Verificação de UI headless (`tools/Screenshots`) | **8 checagens, todas OK** (cliques reais simulados, navegação, 4 tamanhos de janela, 10.000 elementos) |
| Inicialização do app real (`dotnet run`) | Processo permaneceu em execução por 10 s; stdout/stderr vazios (nenhuma exceção) |
| Sanidade dos testes (mutação) | Ao quebrar de propósito 2 regras (impacto de remoção estrutural e tolerância numérica), **60 testes falharam**; revertido, 149/149 voltaram a passar |

## 2. Como foi testado (e limites do método)
- **Testes de unidade/integração (xUnit):** motor, regras, estimativas, importação, exportação, histórico e `MainViewModel` (filtros, busca, detalhes, comandos) — sem abrir janela.
- **UI headless:** a janela real (`MainWindow`) é renderizada com Avalonia.Headless + Skia; o script clica em botões/abas via eventos de mouse simulados, mede tempos e salva PNGs em `docs/screenshots/`. Comando: `dotnet run --project tools/Screenshots`.
- **Não** foi feito teste manual com mouse físico numa janela do macOS (a ferramenta de controle de tela não enxerga um executável .NET como "aplicativo"), nem uso dos diálogos nativos de arquivo.

## 3. Cenários solicitados
| # | Cenário | Esperado | Obtido | Teste |
|---|---|---|---|---|
| A | Elemento só na versão atual | ADDED, impacto MEDIUM, sem propriedades | ✔ | `ScenarioTests.A_*` |
| B | Elemento só na anterior | REMOVED, parede = HIGH | ✔ | `B_*` |
| C | Propriedade alterada | MODIFIED com nome/antigo/novo | ✔ (`Material: Concreto → Drywall`) | `C_*` |
| D | Sem alterações | UNCHANGED, fora do total de alterações | ✔ | `D_*` |
| E | Muitas propriedades | 13 diferenças, nenhuma perdida (inclui parâmetro novo/removido/alterado) | ✔ | `E_*` |
| F | Projetos idênticos | 0 alterações, tudo inalterado | ✔ (também via VM com o mesmo arquivo: 110 inalterados) | `F_*`, `Identical_files_*` |
| G | Arquivos/coleções vazios | sem crash e mensagem clara | ✔ comparador aceita coleções vazias; **o importador recusa** snapshot sem elementos com mensagem ("nenhum elemento encontrado em 'elements'") | `G_*`, `ImportTests` |

## 4. Dashboard (verificação matemática)
Os totais são recontados **de forma independente a partir dos snapshots brutos** (não do resultado do comparador) em `DashboardTests`:
total = |A ∪ B| = 122; adicionados = B∖A = 12; removidos = A∖B = 8; modificados = 21 (comparando a serialização de cada par); inalterados = 81; alterações = 12+8+21 = 41; distribuição por impacto soma 41 (5 crítico · 15 alto · 15 médio · 6 baixo); por categoria soma 41; "alto impacto" = alto + crítico = 20. O `MainViewModel` expõe exatamente esses números nos cards e barras (`Load_demo_compares_and_fills_dashboard_cards_from_real_data`).

## 5. Regras de impacto (documentação + verificação)
As regras **não foram alteradas**. Cada linha abaixo tem teste (`ImpactRulesTests`, 44 casos).

| Alteração | Impacto | Observação |
|---|---|---|
| Nome, Mark | BAIXO | textual |
| Parâmetro informativo | BAIXO | qualquer parâmetro fora da lista crítica |
| Parâmetro crítico (FireRating, LoadBearing, StructuralUsage, AcousticRating, FlowRate) | ALTO | |
| Material | MÉDIO; **ALTO** se estrutural | |
| Tipo | MÉDIO | |
| Categoria, Nível | ALTO | |
| Largura/Altura/Comprimento/Área/Volume | ALTO; **CRÍTICO** se estrutural | |
| Adicionado | MÉDIO (qualquer categoria) | |
| Removido | ALTO; **CRÍTICO** se estrutural | |
| Vários no mesmo elemento | máximo entre eles; todos os motivos são mantidos | |

Estruturais: Column, Beam, Floor, Foundation (comparação sem diferenciar maiúsculas).

**Pontos de atenção / propostas (não implementadas, pois alterariam o comportamento):**
1. *Área e Volume derivados.* Quando o comprimento muda, Área e Volume também mudam e aparecem como linhas separadas, todas ALTO. Proposta: marcar propriedades derivadas e agrupá-las, para reduzir ruído.
2. *Limiar de relevância.* Qualquer mudança acima de 1e-6 dispara ALTO (ex.: 3,200 → 3,205). Proposta: tolerância configurável (ex.: 1 mm).
3. *Adição estrutural.* Pelo critério atual, adicionar um pilar é MÉDIO e removê-lo é CRÍTICO. É coerente com o enunciado ("adicionado = médio"), mas poderia ser ALTO para estruturais.
4. As mensagens dizem "pode"/"exige revisão"; as regras são **heurísticas**, não análise de engenharia — a UI e o README dizem isso.

## 6. Quantitativos estimados
Testes em `QuantityTests` (14 casos), com conferência matemática:
- Aumento: 3,2×2,8 = 8,96 → 4,8×2,8 = 13,44; Δ = **+4,48 m²** (mensagem: "aumentou aproximadamente 4,48 m²").
- Redução: altura 2,8 → 2,0 com comprimento 3,2: 8,96 → 6,40; Δ = **−2,56 m²** ("reduziu…").
- Área explícita tem precedência (10 → 12: Δ = +2,0); volume 1,5 → 2,25: +0,75 m³.
- **Sem estimativa** (retorna nulo): valores iguais, sem dimensões, só comprimento, zero, zero em ambos, negativo, dimensão presente só no atual, adicionados/removidos.
- Texto sempre contém: "Estimativa baseada nos dados do snapshot; **não são quantitativos oficiais extraídos do Revit**" (ajustado nesta auditoria).
- Valores não numéricos no JSON (`"length":"abc"`) são rejeitados na importação com mensagem que cita o campo (seção 8).

## 7. Busca, filtros e detalhes
Testados via `MainViewModel` (`ViewModelTests`): busca por ID, nome, parcial, sem diferenciar maiúsculas, com espaços, sem resultado (`0 de 41 alterações`); filtros ADICIONADO (12), REMOVIDO (8), MODIFICADO (21), INALTERADO (81), categoria (Parede = 10), impacto (Crítico = 5); combinação tipo+impacto+categoria+busca (`W-110`, `W-118`); botão Limpar; contador sempre `N de 41 alterações`. Painel de detalhes: W-104 lista Nível/Material/Comprimento/Área/Volume com valores antigo/novo, motivo e estimativa (+4,48 m²); adicionados e removidos não têm propriedades nem estimativa e não exibem valores inventados.

## 8. Importação, exportação, histórico
**Importação (16 testes):** arquivo de 0 byte, JSON malformado, raiz como array, `{}`, `elements` vazio/nulo/ausente, item nulo, tipo incompatível (cita `length`), `parameters` como array, IDs duplicados (cita o ID), elemento sem ID, propriedades ausentes ou `null`, arquivo inexistente, acentos/Unicode, arquivo com 20.000 elementos. Todos geram `InvalidDataException`/mensagem em português, sem texto técnico do .NET; no app a mensagem aparece na faixa vermelha e o estado anterior é preservado.

**CSV (`ExportTests`):** arquivo criado; **UTF-8 com BOM** (EF BB BF); parser RFC 4180 confirma que toda linha tem as 13 colunas; 1 linha por alteração (41 + cabeçalho); inalterados ausentes; W-104 com valores anteriores/atuais corretos (`3.2 → 4.8`, `Concreto → Drywall`); C-112 `PILAR/REMOVIDO/CRÍTICO` sem valores por propriedade; acentos, `;`, aspas e quebra de linha preservados via escape; proteção contra CSV injection. **Exporta sempre a análise completa; filtros da tabela não afetam o arquivo** (comportamento definido e testado). Abertura no Excel real **não** foi testada; a compatibilidade se apoia no BOM + separador `;` (padrão pt-BR).

**HTML:** estrutura (`<!DOCTYPE>`, charset UTF-8), tags principais balanceadas, totais iguais aos do dashboard, 1 linha por alteração, **sem recursos externos** (sem `http(s)://`, `<script>`, `<link>`, `src=`), conteúdo escapado. Abertura em navegador **não** foi feita.

**Histórico (`HistoryStoreTests` + VM):** cada comparação cria um registro (projeto, versões, data/hora, totais), mais recente primeiro; análises distintas têm títulos distintos; permanece ao navegar entre abas (checado na UI headless). **Persistência implementada nesta auditoria**: `history.json` em `~/Library/Application Support/BIMChangeRadar` (variável `BIMCHANGERADAR_DATA_DIR` redireciona), escrita atômica, máximo 50 entradas, tolerante a arquivo ausente/corrompido. Ao reabrir o app o histórico volta como resumo; ao clicar, o app **recarrega os arquivos pelos caminhos salvos e refaz a comparação** (mesma data). Se os arquivos sumiram, mostra erro claro.

## 9. Performance
Medido nesta máquina (os limites dos testes são folgados; os tempos abaixo são os observados):

| Cenário | Resultado |
|---|---|
| Comparação 1.000 / 5.000 / 10.000 / 50.000 elementos | 3 ms / 19 ms / 58 ms / 498 ms; memória retida +1,7 / +5,1 / +10,6 / +38,4 MB |
| Exportação de 10.000 elementos (4.000 alterações) | CSV 17 ms (701 KB), HTML 10 ms (1,6 MB) |
| ViewModel com 10.000 elementos | carga 142 ms, comparar + dashboard 70 ms, busca 1 ms, filtro 1 ms |
| **UI headless** com 10.000 elementos (5.000 alterações na tabela) | carga 407 ms, comparar 298 ms, renderizar tabela 220 ms, busca 175 ms, filtro 211 ms (inclui pausa fixa de 100 ms do script) |

Nenhum gargalo relevante; a tabela usa virtualização do DataGrid. A única otimização feita foi **compartilhar os pincéis das badges** (antes alocava 2 por linha a cada filtro); o ganho não foi medido isoladamente.

## 10. Problemas encontrados e corrigidos
| # | Problema | Causa | Correção | Verificação |
|---|---|---|---|---|
| 1 | Janela 1100×680: caminhos dos arquivos sumiam, contador de filtros cortado, tabela espremida em 4 colunas, cards e barras cortados pela barra de rolagem | Layout de largura fixa em linha única | Seletor em duas linhas; filtros e botões em `WrapPanel`; cards com largura fixa e quebra de linha; em janela < 1360 px o painel de detalhes vai para baixo da tabela; título oculto em altura < 780 px | Screenshots 1100×680, 1280×720, 1500×880, 1920×1080 revisadas |
| 2 | Colunas/cabeçalhos truncados, nomes cortados | Larguras insuficientes e sem reticências | Larguras ajustadas; estilo de reticências nas células | Screenshots |
| 3 | `NullReference` possível ao importar `"elements": null`, item `null` ou `"name": null` | Validação não cobria null do desserializador | Normalização e validação no carregamento | `ImportTests` (identificado por revisão do código; **não reproduzi o crash antes de corrigir**) |
| 4 | Mensagens técnicas em inglês do serializador; arquivo vazio sem mensagem própria | `JsonException` repassada | Mensagem amigável em português com campo/linha | `ImportTests` |
| 5 | ID duplicado só era detectado ao comparar | Validação só no comparador | Também no carregamento, com o nome do arquivo | `Duplicate_ids_*` |
| 6 | Histórico não persistia | Só em memória | `HistoryStore` + reabertura recomparando | `HistoryStoreTests`, `History_survives_restart_*` |
| 7 | Relatório HTML escrevia acentos como entidades (`&iacute;`) | `WebUtility.HtmlEncode` | `HtmlEncoder` com `UnicodeRanges.All` | Teste falhou antes (2 falhas) e passa agora |
| 8 | Aviso "projetos com nomes diferentes" era sobrescrito pelo status final | Duas atribuições a `StatusMessage` | Mensagens combinadas | `ViewModelWarningTests` |
| 9 | Texto da estimativa não dizia que não é quantitativo oficial do Revit | — | Frase explícita | `QuantityTests` |
| 10 | `ComboBox` pode devolver `null` ao trocar a lista de categorias e esvaziar a tabela | Comparação direta com a seleção | `null`/vazio tratado como "Todos" | `Null_combo_selection_is_treated_as_all` (**risco por revisão; não reproduzido na UI**) |
| 11 | 2 avisos de build (CS1998 na ferramenta, xUnit1031) | Código de teste | Corrigidos | build: 0 avisos |
| 12 | A ferramenta de QA gravaria no histórico real do usuário | Caminho fixo | Variável `BIMCHANGERADAR_DATA_DIR`; arquivo gerado por mim apagado | pasta de dados inexistente após a execução |

## 11. Problemas/limitações que permanecem
- **Revit:** não há integração; apenas snapshots JSON.
- Mudança **somente** do `id` de exibição (com o mesmo `uniqueId`) não é reportada (o `id` não é comparado; o casamento é por `uniqueId`). Elemento recriado com novo ID aparece como removido + adicionado.
- Exportação ignora os filtros da tabela (por definição).
- Regras de impacto fixas e heurísticas; propostas na seção 5.
- Dimensão ≤ 0 ou ausente simplesmente não gera estimativa (sem aviso específico na tela).
- Unidades assumidas em metros; sem conversão.
- Histórico guarda caminhos; se o arquivo for movido/apagado, a análise antiga não reabre.
- Em janelas pequenas (1100×680) o layout é utilizável, mas a tabela fica com poucas linhas visíveis (rolagem).
- Sem persistência de filtros/ordenação; sem testes de acessibilidade (leitor de tela, contraste formal).

## 12. Não testado
- Execução em Windows ou Linux.
- Mouse físico/diálogos nativos de abrir e salvar arquivo (substituídos por `FakeDialogs`).
- Abertura real do CSV no Excel e do HTML em navegador.
- Interação por teclado/acessibilidade; DPI alto em monitores externos.
- Memória do processo inteiro com a UI (medi apenas a memória retida pelo motor).
