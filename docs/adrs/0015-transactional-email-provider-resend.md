# ADR 0015: Provedor de Emails Transacionais (Resend) e Arquitetura de Notificações

## Status
Aceito (Accepted)

## Contexto
A plataforma CriaCerto necessita de disparos de emails transacionais para fluxos operacionais e de segurança, incluindo:
1. **Recuperação de Senha**: Envio de token de verificação e redefinição para produtores e operadores (`ForgotPasswordCommand`).
2. **Convites de Equipe**: Admissão e integração de novos colaboradores em fazendas/tenants (`InviteTeamMemberCommand`).
3. **Notificações Críticas e Alertas**: Futuras comunicações de conformidade, alertas zootécnicos e aprovações do Backoffice (4-Eyes Workflow).

O envio de e-mails deve respeitar estritamente as diretrizes de arquitetura modular do CriaCerto:
- Sem acoplamento direto dos módulos de domínio com serviços de terceiros.
- **Result Pattern estrito (`Result<T>`)**: Proibição de lançamento de exceções de rede ou falhas de HTTP para controle de fluxo.
- **Resiliência e Desenvolvimento Offline**: Capacidade de operar localmente ou em testes sem dependência de chaves de API ativas (`NullEmailSender`).
- **Conformidade LGPD**: Destinatários e credenciais protegidos contra vazamento em logs estruturados.

## Decisão
Adotar o **Resend** como provedor padrão de entrega transacional, integrado via arquitetura limpa nos Building Blocks:

1. **Abstrações Desacopladas (`CriaCerto.BuildingBlocks.Abstractions.Email`)**:
   - `IEmailSender`: Contrato unificado com método assíncrono `Task<Result<EmailSendResult>> SendAsync(EmailMessage message, CancellationToken cancellationToken)`.
   - `EmailMessage`: DTO imutável contendo lista de destinatários (`EmailRecipient`), assunto, corpo HTML/texto, remetente opcional, headers e tags.
   - `EmailErrors`: Catálogo padronizado de erros funcionais (`ApiKeyMissing`, `InvalidRecipient`, `RateLimited`, `ProviderError`, `NetworkFailure`).

2. **Implementação de Infraestrutura (`CriaCerto.BuildingBlocks.Infrastructure.Email`)**:
   - `ResendEmailSender`: Cliente REST nativo em .NET 10 consumindo a API oficial do Resend (`POST https://api.resend.com/emails`) com autenticação Bearer e serialização relaxed (`UnsafeRelaxedJsonEscaping`) para preservação de formatações de e-mail e HTML.
   - `NullEmailSender`: Provedor substituto (mock) ativado em desenvolvimento (`EnableMockInDevelopment = true`) ou quando a chave de API estiver ausente, registrando o envio estruturado sem falhas de execução.
   - `ResendOptions`: Configuração tipada com suporte a injeção via `appsettings.json` e variáveis de ambiente (`RESEND_API_KEY`, `RESEND_FROM_EMAIL`, `RESEND_FROM_NAME`, `RESEND_ENABLE_MOCK_DEV`).

3. **Invariantes e Tratamento de Falhas (Result Pattern)**:
   - Respostas de sucesso (200/201) retornam `Result.Success(EmailSendResult)`.
   - Rate limiting (HTTP 429) retorna `Result.Failure(EmailErrors.RateLimited)`.
   - Chave inválida ou payload rejeitado retorna `Result.Failure(EmailErrors.ProviderError)`.
   - Falhas de rede ou timeout retornam `Result.Failure(EmailErrors.NetworkFailure)`.
   - Nenhuma exceção não tratada é propagada aos chamadores.

4. **Integração de Domínio**:
   - Módulos de aplicação (ex: `Modules.Tenancy`) injetam `IEmailSender?` de forma opcional em seus handlers (`RegisterUserCommandHandler`, `ForgotPasswordCommandHandler`, `InviteTeamMemberCommandHandler`).

## Consequências
- **Positivas**:
  - Alta entregabilidade com latência mínima através da infraestrutura moderna do Resend.
  - Zero acoplamento entre os domínios de negócio e a API do Resend.
  - Testabilidade completa via suíte unitária isolada com `FakeHttpMessageHandler` e mock via `NullEmailSender`.
  - Conformidade com as regras de TDD e Result Pattern descritas em `agents.md`.
- **Negativas**:
  - Requer gestão de reputação do domínio remetente (`noreply@criacerto.com.br`) com registros DNS válidos (SPF, DKIM, DMARC) no painel do Resend em ambiente de produção.
