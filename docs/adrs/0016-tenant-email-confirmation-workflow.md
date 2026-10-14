# ADR 0016: Fluxo de Confirmação e Ativação de E-mail de Produtor e Tenant

## Status
Aceito (Accepted)

## Contexto
Durante o onboarding e cadastro de novos produtores na plataforma CriaCerto:
1. O usuário cria suas credenciais (`RegisterUserCommand`) e configura a fazenda/unidade de produção (`CreateTenantCommand`).
2. Para mitigar o registro de e-mails inválidos, combater fraudes, assegurar a titularidade do operador rural e evitar acessos não autorizados a dados pecuários, faz-se mandatório o fluxo de verificação de propriedade do endereço de e-mail antes da liberação do login completo.
3. O fluxo precisa seguir estritamente as diretrizes de engenharia do CriaCerto:
   - **Result Pattern estrito (`Result<T>`)**: sem lançamento de exceções para fluxo de controle.
   - **Arquitetura Desacoplada de E-mail**: uso do serviço transacional via `IEmailSender` (Resend), conforme estabelecido no ADR 0015.
   - **Proteção contra Enumeração e Timing Attacks**: respostas neutras em endpoints públicos de reenvio de confirmação para não expor a existência de contas.
   - **Idempotência**: cliques repetidos em links já confirmados devem retornar sucesso sem quebrar o estado da aplicação.
   - **TDD (Test-Driven Development)**: cobertura abrangente com testes unitários em SQLite in-memory e mocks funcionais.

## Decisão
1. **Extensão da Entidade `User`**:
   - `IsEmailConfirmed (bool)`: Flag indicadora de ativação da conta.
   - `EmailConfirmedAtUtc (DateTime?)`: Timestamp da confirmação.
   - `EmailConfirmationToken (string?)`: Token criptográfico gerado na criação ou reenvio.
   - `EmailConfirmationTokenExpiresAt (DateTime?)`: Expiração de segurança (24 horas).

2. **Geração e Envio de Token no Cadastro**:
   - No `RegisterUserCommandHandler`, o usuário é registrado com `IsEmailConfirmed = false` e recebe o e-mail de boas-vindas com botão CTA e link contendo o token de ativação (`/confirm-email?token={token}&email={email}`).

3. **Gating de Acesso no Login (`LoginCommandHandler`)**:
   - O login valida se `user.IsEmailConfirmed == true`.
   - Caso `false`, retorna `Result.Failure(Error.Unauthorized("Auth.EmailNotConfirmed", "Seu e-mail ainda não foi confirmado."))`.
   - Na interface do Blazor (`Login.razor`), um card dedicado orienta o produtor e disponibiliza o botão de reenvio imediato do e-mail de ativação.

4. **Comandos CQRS Dedicados (`Modules.Tenancy.Application`)**:
   - `ConfirmEmailCommand(Email, Token)`: Valida token, vigência e ativa a conta (`IsEmailConfirmed = true`), limpando o token persistido.
   - `ResendConfirmationEmailCommand(Email)`: Reemite novo token de 24h e dispara o e-mail transacional via `IEmailSender`. Retorna `Result.Success(string.Empty)` para e-mails inexistentes, evitando enumeração de usuários.

5. **Interface Web em Blazor (`ConfirmEmail.razor`)**:
   - Validação automática de query parameters (`token` e `email`).
   - Apresentação de status: Validação com spinner, Sucesso com redirecionamento ao login, e Tratamento de token expirado com input para reenvio imediato.
   - Suporte a digitação/inserção manual de token.

## Consequências
- **Positivas**:
  - Garantia de legitimidade cadastral de todos os produtores que operam o sistema.
  - Alinhamento pleno ao Result Pattern e à arquitetura de notificações do Resend.
  - Experiência fluida e amigável (UX) no Blazor Web App com mensagens claras e reenvio de e-mails em um clique.
  - Cobertura 100% testada via suíte unitária de TDD.
- **Negativas**:
  - Adiciona uma etapa de verificação antes do primeiro login do produtor rural.
