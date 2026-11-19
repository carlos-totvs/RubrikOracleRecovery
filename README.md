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