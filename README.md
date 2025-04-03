# Lambda Custom Authorizer e Infraestrutura do API Gateway

Este repositório contém os arquivos necessários para provisionar uma **função Lambda** que funciona como **Custom Authorizer** no **API Gateway**, além da criação da infraestrutura do próprio API Gateway.

---

## **Visão Geral**

A infraestrutura possui duas rotas principais no API Gateway:

1. **`/prod`** - Protegida, exige autenticação via **Token JWT** gerado pelo **AWS Cognito**.
   - Acesso restrito a **administradores**.

2. **`/prod/public`** - Rota pública, permite chamadas sem autenticação.

A **função Lambda** valida os Tokens JWT para a rota protegida `/prod` e garante o acesso apenas a usuários com a role **admin**.

---

## **Arquitetura**

### **1. AWS Cognito**
- Responsável pela geração e validação dos **Tokens JWT**.
- Configurado para emitir tokens com roles específicas, como `admin`.

### **2. Lambda Function**
- Implementa a lógica do **Custom Authorizer**.
- Valida o **Token JWT** enviado no cabeçalho da requisição.
- Gera políticas de autorização para permitir ou negar acesso às rotas.

### **3. API Gateway**
- Provisiona as seguintes rotas:
  - **`/prod`** - Protegida, exige autenticação.
  - **`/prod/public`** - Pública, sem autenticação.

---

## **Pré-requisitos**

- **Terraform** instalado (versão 1.8.0 ou superior).
- **AWS CLI** configurado com as credenciais apropriadas.
- **Node.js** (para a função Lambda).

---

## **Conteúdo do Repositório**

### **Infraestrutura**

1. **`main.tf`**:
   - Provisiona o **API Gateway** com as rotas protegidas e públicas.
   - Configura o **Custom Authorizer** apontando para a função Lambda.

2. **`variables.tf`**:
   - Declara variáveis, como região, nome da Lambda, e endpoint do backend.

3. **`outputs.tf`**:
   - Exporta os endpoints do API Gateway após a criação.

4. **`lambda/`**:
   - Contém o código da função Lambda que atua como **Custom Authorizer**.

### **Passos para Execução**
1. Clone o repositório:
   ```bash
   git clone <repo-url>
   cd <repo-folder>
   terraform init
   terraform plan 
   terraform apply 
