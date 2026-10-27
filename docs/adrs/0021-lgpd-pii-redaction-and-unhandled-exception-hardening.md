# ADR 0021: Sanitização e Prevenção de Vazamento de PII em Logs, Telemetria e Exceções (LGPD Art. 46)

## Status
Aceito (Accepted)

## Contexto
O Artigo 46 da Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) estipula que os agentes de tratamento devem adotar medidas de segurança, técnicas e administrativas aptas a proteger os dados pessoais de acessos não autorizados e de situações acidentais ou ilícitas de destruição, perda, alteração, comunicação ou difusão.

Em aplicações distribuídas baseadas em microsserviços ou monólitos modulares, duas das maiores fontes de vazamento acidental de dados pessoais e credenciais secretas ocorrem em:
1. **Logs de Requisições e Respostas HTTP**: Gravação inadvertida de cabeçalhos de autenticação (`Authorization: Bearer <token>`, `Cookie`, `X-Api-Key`), query strings contendo tokens e bodies de requisição em endpoints sensíveis (ex: senhas em texto puro de login, dados de cartão de crédito e chaves Pix).
2. **Exceções Não Tratadas (Unhandler Exceptions)**: Respostas HTTP 500 nativas expondo stack traces completos, nomes de tabelas, consultas SQL e strings de conexão com senhas de banco (`Server=...;Password=...`).
3. **Telemetria e Distributed Tracing (ActivitySource / Spans)**: Injeção de mensagens brutas de erro de exceções em atributos/tags de spans OpenTelemetry, propagando segredos e PII para agregadores e APMs externos.

## Decisão

Adotou-se uma arquitetura multicamada de **Sanitização Proativa e Redaction por Padrão (Privacy by Design and by Default)** composta por quatro pilares:

### 1. Motor Centralizado de Redaction (`IPiiRedactor` e `PiiRedactor`)
- Localizado em `CriaCerto.BuildingBlocks.Abstractions.Security` e `CriaCerto.BuildingBlocks.Infrastructure.Security`.
- **Cabeçalhos Sensíveis**: Mascaramento determinístico de `Authorization` (gerando `Bearer [REDACTED]` ou `[REDACTED]`), `Cookie`, `Set-Cookie`, `X-Api-Key` e `Proxy-Authorization`.
- **Query Strings**: Redaction de parâmetros sensíveis como `token`, `password`, `key`, `pixKey`, `cpf`, `code`, `cvv` para `[REDACTED]`.
- **Navegação em JSON**: Varredura recursiva de nós `System.Text.Json.Nodes.JsonNode`, aplicando redaction em credenciais e mascaramento parcial em documentos (CPF no padrão `***.456.789-**` e CNPJ no padrão `12.***.***/0001-**`), e-mails e telefones.
- **Sanitização de Connection Strings**: Expressão regular compilada eliminando qualquer ocorrência de `(?i)((?:password|pwd)\s*=)[^;]+` e tokens JWT em mensagens de log ou exceções.

### 2. Middleware de Auditoria e Logging Estruturado (`RequestLoggingMiddleware`)
- Configurado com `Serilog.AspNetCore` (10.0.0).
- Intercepta requisições HTTP medindo latência, código de status e TraceId.
- **Blocklist de Endpoints Confidenciais**: Endpoints de autenticação e credenciais (`/api/v1/auth/login`, `/api/v1/backoffice/auth/login`, `/api/v1/auth/change-password`, `/api/v1/auth/reset-password`, `/api/v1/auth/account/delete`, `/api/v1/backoffice/referral/payouts`) têm o body sumariamente substituído por `[REDACTED_SENSITIVE_ENDPOINT]`.
- Omissão de rotas de monitoramento frequente (`/health`) para prevenir poluição sonora nos coletores de log.

### 3. Tratamento Global de Exceções com Result Pattern (`GlobalExceptionHandlerMiddleware`)
- Posicionado como o middleware mais externo no pipeline HTTP do ASP.NET Core.
- Captura qualquer exceção não tratada na aplicação.
- Registra a exceção completa, higienizada, nos logs internos protegidos com o nível `Error` e identificador de correlação (`TraceIdentifier` / `Activity.Current?.Id`).
- Retorna resposta HTTP 500 padronizada no padrão `Result Pattern`:
  ```json
  {
    "code": "Server.UnexpectedError",
    "message": "Ocorreu um erro interno inesperado. Identificador de rastreamento: {traceId}.",
    "type": 0
  }
  ```
- Garante que nenhum detalhe de infraestrutura, conexão ou stack trace seja exposto ao cliente HTTP.

### 4. Blindagem de Telemetria e Spans (`ActivitySource`)
- No MediatR pipeline behavior de observabilidade (`BackofficeObservabilityBehavior`), os blocos de captura de erro deixam de injetar `ex.Message` crua nas tags de span.
- Em vez disso, registram `ex.GetType().Name` como `backoffice.exception_type` e status seguro, impedindo vazamento de connection strings ou dados cadastrais para sistemas de tracing distribuído.

## Consequências

- **Positivas**:
  - Blindagem completa contra vazamento acidental de PII em logs e telemetria (LGPD Art. 46).
  - Conformidade estrita com o princípio de transparência e segurança de dados do OWASP API Security Top 10 (A01/A05/A09).
  - Erros 500 previsíveis, auditáveis e rastreáveis sem risco de exposição técnica para o usuário final.
  - Alinhamento total ao padrão `Result Pattern` institucional.
- **Negativas / Limitações**:
  - Leve sobrecarga de computação no parsing de JSON de payloads grandes durante inspeção em ambientes de desenvolvimento (mitigado por limite de captura de 16 KB e supressão instantânea de rotas confidenciais).
