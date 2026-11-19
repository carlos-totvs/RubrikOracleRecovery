
# Rubrik Oracle Recovery (.NET)

Este projeto é uma ferramenta desenvolvida em .NET (C#) para realizar a coleta em massa do último ponto de recuperação (Recovery Point) de instâncias Oracle protegidas pela Rubrik (RSC). O projeto foi portado de sua versão original em Python, mantendo a mesma estrutura de relatórios e lógica de negócios.

## 📋 Pré-requisitos

- [.NET SDK](https://dotnet.microsoft.com/download) instalado na sua máquina (recomendado .NET 6.0 ou superior).
- Acesso à internet/rede para comunicação com a API da Rubrik RSC.

## ⚙️ Configuração

Por questões de segurança, as credenciais de acesso à Rubrik não são armazenadas em texto plano. O arquivo de configuração deve conter um JSON codificado em **Base64**.

1. Crie um arquivo chamado `config.json` dentro da pasta `Configuration/`.
2. O formato original das suas credenciais (em JSON) deve ser o seguinte:
   ```json
   {
     "ClientId": "seu-client-id",
     "ClientSecret": "seu-secret",
     "Url": "https://sua-url.my.rubrik.com"
   }

```

3. Converta esse JSON completo para uma string **Base64**.
4. Cole **apenas a string Base64** dentro do seu arquivo `Configuration/config.json`. Exemplo do conteúdo final do arquivo:
```text
ewogICJDbGllbnRJZCI6ICJzZXUtY2xpZW50LWlkIiwKICAiQ2xpZW50U2VjcmV0IjogInNldS1zZWNyZXQiLAogICJVcmwiOiAiaHR0cHM6Ly9zdWEtdXJsLm15LnJ1YnJpay5jb20iCn0=

```



## 📝 Preparação do Arquivo de Entrada

O sistema requer uma lista de servidores (hosts) para consultar.

1. Certifique-se de que a estrutura de pastas `reports/input/` existe na raiz do projeto.
2. Crie ou edite o arquivo `hosts.csv` dentro dessa pasta.
3. O arquivo não deve conter cabeçalho. Insira apenas o nome do servidor, um por linha:
```csv
ORCL-SHARED-PRT-0066-PP-C-CE
ORCL-C6YV7P-130957-S-0939-CE
ORCL-C6P3YB-167826-A-0938-CE

```



## ▶️ Como Executar

Abra o terminal (PowerShell, CMD ou terminal do VS Code) na **raiz do projeto** e execute o comando abaixo, referenciando o arquivo de entrada:

```bash
dotnet run --input hosts.csv

```

> **Nota:** Caso seja a primeira execução, o comando `dotnet run` irá automaticamente compilar o projeto, baixar os pacotes necessários (como `System.Text.Json` ou outros) e em seguida executar a aplicação.

## 📊 Saída Esperada (Output)

Após a execução com sucesso, o sistema gerará um relatório CSV dentro do diretório `reports/` com o nome no formato `Oracle_Recovery_Points_DD-MM-YYYY HHmmss.csv`.

O relatório final aplica as seguintes tratativas automatizadas:

* **Filtro de Colunas:** Retorna apenas `hostOrRacName` e `latest_end_time`.
* **Limpeza de Nomes:** O prefixo padrão *"Oracle Host "* retornado pela API é removido.
* **Remoção de Duplicidades e Ordenação:** Mantém apenas o backup mais recente de cada servidor, ignorando backups antigos ou em estado "RELIC", e ordena a lista de forma decrescente (do mais recente para o mais antigo).
