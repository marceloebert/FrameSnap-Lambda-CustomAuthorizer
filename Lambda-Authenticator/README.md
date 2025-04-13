# Lambda Authenticator

Este projeto é um AWS Lambda Custom Authorizer desenvolvido em .NET 8.0 para autenticação de requisições na API do FrameSnap. Ele valida tokens JWT emitidos pelo Amazon Cognito e gera políticas de acesso para controlar o acesso aos recursos da API.

## 🚀 Funcionalidades

- Validação de tokens JWT do Amazon Cognito
- Geração de políticas IAM para controle de acesso
- Suporte a múltiplos endpoints da API
- Tratamento de erros e exceções de segurança
- Cobertura de testes unitários

## 📋 Pré-requisitos

- .NET 8.0 SDK
- AWS CLI configurado
- Conta AWS com acesso ao:
  - AWS Lambda
  - Amazon Cognito
  - API Gateway

## 🔧 Configuração

1. Clone o repositório:
```bash
git clone [URL_DO_REPOSITORIO]
cd Lambda-Authenticator
```

2. Restaure as dependências:
```bash
dotnet restore
```

3. Configure as variáveis de ambiente:
- `COGNITO_USER_POOL_ID`: ID do User Pool do Cognito
- `COGNITO_CLIENT_ID`: ID do Client do Cognito
- `AWS_REGION`: Região da AWS onde o Cognito está configurado

## 🏗️ Estrutura do Projeto

```
Lambda-Authenticator/
├── Lambda-Authenticator/
│   ├── Function.cs           # Classe principal do Lambda
│   └── Lambda-Authenticator.csproj
├── Lambda-Authenticator.Tests/
│   ├── FunctionTests.cs      # Testes unitários
│   └── Lambda-Authenticator.Tests.csproj
└── README.md
```

## 🔍 Como Funciona

1. O Lambda recebe uma requisição do API Gateway contendo:
   - Token JWT no header `Authorization`
   - ARN do método sendo acessado

2. O authorizer:
   - Valida o formato do token
   - Busca as chaves públicas do Cognito (JWKS)
   - Valida a assinatura e claims do token
   - Verifica o `client_id`

3. Se válido:
   - Gera uma política IAM permitindo acesso
   - Retorna a política para o API Gateway

4. Se inválido:
   - Lança `UnauthorizedAccessException`
   - API Gateway retorna 401 Unauthorized

## 🧪 Testes

Execute os testes unitários:
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=cobertura
```

Para gerar o relatório de cobertura:
```bash
dotnet tool install -g dotnet-reportgenerator-globaltool
reportgenerator -reports:"Lambda-Authenticator.Tests/TestResults/coverage.cobertura.xml" -targetdir:"coveragereport" -reporttypes:Html
```

## 📦 Deploy

1. Publique o projeto:
```bash
dotnet publish -c Release
```

2. Faça o deploy para AWS Lambda:
```bash
aws lambda update-function-code --function-name [NOME_DA_FUNCAO] --zip-file fileb://bin/Release/net8.0/publish/Lambda-Authenticator.zip
```

## 🔐 Segurança

- Tokens JWT são validados usando chaves públicas do Cognito
- Políticas IAM são geradas com escopo limitado
- Erros de autenticação são tratados adequadamente
- Logs de segurança são gerados para auditoria

## 🤝 Contribuindo

1. Faça um fork do projeto
2. Crie uma branch para sua feature (`git checkout -b feature/AmazingFeature`)
3. Commit suas mudanças (`git commit -m 'Add some AmazingFeature'`)
4. Push para a branch (`git push origin feature/AmazingFeature`)
5. Abra um Pull Request

## 📝 Licença

Este projeto está sob a licença MIT. Veja o arquivo [LICENSE](LICENSE) para mais detalhes.

## ✨ Autores

* **[SEU_NOME]** - *Trabalho inicial*

## 📄 Versionamento

Usamos [SemVer](http://semver.org/) para controle de versão. Para as versões disponíveis, veja as [tags neste repositório](https://github.com/seu/projeto/tags). 