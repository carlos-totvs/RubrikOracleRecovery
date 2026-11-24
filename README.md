# Rubrik Oracle Recovery (.NET)

Aplicação de console em C# que consulta a API GraphQL da Rubrik Security Cloud (RSC) e gera um relatório CSV com os últimos pontos de recuperação das bases Oracle informadas.

Para cada host do arquivo de entrada, a aplicação:

1. autentica na RSC usando credenciais OAuth;
2. pesquisa o workload Oracle na GraphQL;
3. consulta os intervalos recuperáveis;
4. consolida os pontos de recuperação por host; e
5. gera o CSV em `reports/`.

## Tecnologias e dependências

- .NET 11 (`net11.0`);
- C#;
- `System.Net.Http` para autenticação e GraphQL;
- `System.Text.Json` para JSON;
- `System.Security.Cryptography.ProtectedData` para proteção das credenciais no Windows;
- `CsvHelper` como dependência do projeto;
- xUnit e Coverlet para testes.

## Pré-requisitos

- SDK do .NET 11 instalado;
- acesso de rede aos endpoints configurados da Rubrik RSC;
- Windows, quando a proteção automática de credenciais com DPAPI será utilizada;
- um arquivo CSV contendo os hosts Oracle/RAC a consultar.

## Estrutura do projeto

```text
Configuration/
  ConfigManager.cs       Carrega, protege e desprotege as credenciais
  config.txt             Configuração inicial em Base64
FileManager/
  ReportManager.cs       Gera o relatório CSV
Model/
  OracleRecoveryPoint.cs Modelo de host e horário de recuperação
Services/
  SnapshotService.cs     Autenticação e consultas GraphQL
Tests/
  SnapshotServiceTests.cs Testes com HttpClient simulado
reports/
  input/
    hosts.csv            Lista padrão de hosts
Program.cs               Entrada da aplicação
```

## Configuração

O arquivo `Configuration/config.txt` deve conter as configurações em JSON codificado em Base64 na primeira execução.

O JSON deve possuir os seguintes campos:

```json
{
  "client_id": "SEU_CLIENT_ID",
  "client_secret": "SEU_CLIENT_SECRET",
  "name": "SEU_CLIENT_NAME",
  "access_token_uri": "https://sua-instancia.rubrik.com/api/client_token",
  "graphql_url": "https://sua-instancia.rubrik.com/api/graphql"
}
```

Para gerar o conteúdo Base64 no PowerShell:

```powershell
$json = Get-Content ".\config.json" -Raw
[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
```

Salve apenas a string Base64 no arquivo `Configuration/config.txt`.

### Proteção das credenciais no Windows

Na primeira execução:

1. o conteúdo Base64 é decodificado;
2. o JSON é protegido com DPAPI no contexto do usuário Windows atual (`CurrentUser`);
3. o arquivo original é sobrescrito com zeros antes de receber o novo conteúdo; e
4. o arquivo passa a usar o formato `DPAPI:<conteudo-protegido-em-base64>`.

Nas execuções seguintes, a aplicação desprotege o valor no mesmo usuário do Windows. Como a chave fica vinculada ao usuário e ao perfil do Windows, a versão protegida não deve ser considerada portável entre outros usuários ou máquinas.

Quando executada pelo Agendador de Tarefas, use o mesmo usuário do Windows que realizou a primeira execução. O caminho absoluto de `Configuration/config.txt` é calculado a partir do diretório base da aplicação, permitindo execução fora do diretório atual.

> Não confirme credenciais reais no repositório. O `config.txt` contém dados sensíveis e deve ser protegido conforme a política da organização.

## Preparação do arquivo de entrada

O arquivo padrão é `reports/input/hosts.csv`.

- não deve ter linha de cabeçalho;
- cada linha representa um nome de host/Oracle RAC;
- linhas vazias ou compostas apenas por espaços são ignoradas;
- espaços no início e no final de cada linha são removidos.

Exemplo:

```csv
orcl-c4ovmw-225496-p-2123-ce
ORCL-SHARED-PRT-0066-PP-C-CE
```

É possível informar outro arquivo dentro da pasta `reports/input/` com o argumento `--input`:

```powershell
dotnet run --project .\RubrikOracleRecovery.csproj -- --input outro-arquivo.csv
```

## Executar

Na raiz do repositório:

```powershell
dotnet run --project .\RubrikOracleRecovery.csproj
```

Para executar o arquivo padrão:

```powershell
dotnet run
```

Para compilar e publicar uma versão executável:

```powershell
dotnet build .\RubrikOracleRecovery.slnx
dotnet publish .\RubrikOracleRecovery.csproj -c Release -o .\publish
```

O executável publicado deve ficar no diretório cuja estrutura de `Configuration/` e `reports/input/` esteja disponível.

## Consultas Rubrik

A autenticação envia `client_id`, `client_secret` e `name` ao endpoint informado em `access_token_uri`. A resposta deve conter `access_token`, que é usado como bearer token nas chamadas GraphQL.

Para localizar um host, a aplicação usa `globalSearchResults` com os filtros:

- `LOCATION` igual ao nome informado no CSV;
- `IS_GHOST=false`;
- `IS_ACTIVE=true`;
- ordenação por nome ascendente.

A consulta usa o primeiro workload encontrado e chama `oracleRecoverableRanges` com `shouldIncludeDbSnapshotSummaries=true`.

Comportamento em caso de falha:

- um host não encontrado é informado no console e ignorado;
- falha ou resposta inválida em um host não interrompe os demais hosts;
- a aplicação retorna uma lista vazia se nenhum ponto puder ser processado;
- erros HTTP de autenticação incluem status e corpo da resposta.

## Saída CSV

Os relatórios são gravados em `reports/`.

Nome do arquivo:

```text
Oracle_Recovery_Points_DD-MM-YYYY HHmmss.csv
```

Exemplo:

```text
reports/Oracle_Recovery_Points_24-11-2026 102024.csv
```

O relatório contém estas colunas:

```text
hostOrRacName,latest_end_time
```

O processamento:

- remove o prefixo literal `Oracle Host ` do nome retornado pela API;
- chama `Trim()` no nome;
- converte `endTime` para texto no formato `yyyy-MM-ddTHH:mm:sszzz`;
- ordena os registros pelo campo `latest_end_time` em ordem decrescente;
- mantém apenas o primeiro registro para cada `hostOrRacName` após a ordenação.

Exemplo:

```csv
hostOrRacName,latest_end_time
orcl-c4ovmw-225496-p-2123-ce,2026-11-24T09:55:02-03:00
```

Arquivos gerados em `reports/*.csv` são ignorados pelo Git. Apenas `reports/input/hosts.csv` é mantido como arquivo de entrada versionado.

## Testes

Os testes simulam o `HttpClient` e cobrem:

- retorno de ponto de recuperação para um host encontrado;
- retorno vazio para host inexistente;
- continuidade do processamento quando um host gera erro.

Execute todos os testes com:

```powershell
dotnet test .\RubrikOracleRecovery.slnx
```
