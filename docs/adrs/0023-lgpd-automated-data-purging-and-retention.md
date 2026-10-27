# ADR 0023: Rotinas Automatizadas de Expurgo e Ciclo de Retenção de Dados Temporários (LGPD Art. 16 e Art. 46)

## Status
Aceito (Accepted)

## Contexto
O ciclo de vida de dados pessoais em aplicações SaaS multi-tenant deve respeitar os princípios da finalidade, necessidade e minimização de dados (Art. 6º, I e III da LGPD - Lei nº 13.709/2018), bem como os critérios formais para término do tratamento e eliminação segura (Artigos 15 e 16).

No ecossistema do **CriaCerto Bovino SaaS**, existem dados temporários e credenciais com tempo de vida limitado (TTL) que, após a expiração de sua utilidade operacional, deixam de possuir base legal de retenção:
1. **Tokens de Confirmação de E-mail e Redefinição de Senha (`Users`)**: Gerados com validade estrita para fluxos de ativação de conta e recuperação de credenciais. Após o vencimento, sua permanência na base amplia a superfície de exposição sem qualquer benefício técnico.
2. **Convites de Equipe Não Aceitos (`TeamInvites`)**: Disparados pelo Produtor Rural para inclusão de vaqueiros, zootecnistas e médicos-veterinários. Caso o convite expire e não seja aceito após janela prudencial (30 dias), não se constitui relação contratual nem consentimento válido com o titular do e-mail convidado, caracterizando dado pessoal excessivo e desnecessário.
3. **Sessões Administrativas e Refresh Tokens Revogados/Expirados (`AdminSessions`)**: Registros de autenticação de operadores do Backoffice que foram explicitamente revogados ou cujos refresh tokens já expiraram e não devem permanecer armazenados no schema `backoffice`.

A ausência de rotinas automatizadas e auditáveis de expurgo (data purging) gerava acúmulo de dados obsoletos, descumprimento do princípio de minimização e risco desnecessário de vazamento em eventuais incidentes ou dumps de banco de dados.

## Decisão

Implementou-se uma arquitetura multicamada e modular de expurgo periódico e retenção automatizada de dados temporários:

### 1. Desacoplamento Modular e Encapsulamento de Domínio (Modular Monolith)
- **Módulo Tenancy**:
  - Adicionado método de domínio expressivo no agregado `User`: `ClearExpiredTokens(DateTime referenceDateUtc, int graceDays = 7)` que higieniza `EmailConfirmationToken` e `PasswordResetToken` anteriores à janela de tolerância.
  - Criado comando CQRS `PurgeExpiredTenancyTokensAndInvitesCommand` e handler `PurgeExpiredTenancyTokensAndInvitesCommandHandler`, responsável pela limpeza dos tokens de usuários e pelo descarte permanente (`RemoveRange`) de convites em `TeamInvites` com `!IsAccepted` expirados há mais de 30 dias.
  - Retorno via `Result<TenancyPurgeSummaryDto>` reportando com precisão a volumetria avaliada e descartada, com suporte nativo a simulação (`DryRun`).
- **Módulo Backoffice**:
  - Criado comando CQRS `PurgeExpiredAdminSessionsCommand` e handler `PurgeExpiredAdminSessionsCommandHandler`, responsável pela eliminação permanente de registros em `AdminSessions` revogados ou cujos refresh tokens expiraram além do período de carência (7 dias).
  - Geração compulsória de registro forense no `AuditLog` com `Action = "ADMIN_SESSIONS_PURGED"`, categoria `Compliance`, severidade `Low`, dados estruturados de volumetria em `DetailsJson` e integridade garantida por encadeamento de hash **SHA-256** (`RecordHash` / `PreviousRecordHash`).
  - Retorno via `Result<BackofficePurgeSummaryDto>` com suporte a `DryRun`.

### 2. Background Service Resiliente no Host (`CriaCerto.Api`)
- Criado o worker `DataRetentionPurgingBackgroundService` em `src/Host/CriaCerto.Api/BackgroundServices/`, derivado de `BackgroundService`.
- Execução programada diária (intervalo padrão de 24 horas) baseada em `PeriodicTimer`, com atraso inicial configurável para conclusão prévia de migrações e seeders de inicialização.
- Isolamento estrito de dependências por execução através de `IServiceScopeFactory`, resolvendo `ISender` e orquestrando ambos os módulos de maneira transacional e independente.
- Configuração externalizada via `IOptionsMonitor<DataRetentionPurgingOptions>` na seção `DataRetention` do `appsettings.json`, permitindo ajuste em tempo real de `IntervalHours`, `InitialDelaySeconds`, `TokenGraceDays` (7d), `InviteGraceDays` (30d), `AdminSessionGraceDays` (7d) e `DryRun`.
- Tratamento resiliente de cancelamento com graceful shutdown (`OperationCanceledException`) e supressão de exceções não tratadas com logs estruturados via Serilog, sem derrubar o processo host.

### 3. Operabilidade e Disparo sob Demanda
- Mapeado endpoint administrativo autenticado `POST /api/v1/backoffice/compliance/data-retention/run-purge` sob claim `users_admin.manage`, permitindo execuções manuais por operadores autorizados e auditorias de conformidade com suporte a parâmetro de simulação `?dryRun=true`.

## Consequências

- **Positivas**:
  - Conformidade estrita com o princípio da minimização e necessidade (Art. 6º, III) e determinação legal de eliminação de dados pessoais (Art. 16 da LGPD).
  - Redução drástica da superfície de risco de vazamento de e-mails de terceiros e tokens temporários expirados.
  - Eliminação contínua de credenciais de sessão obsoletas no Backoffice (Art. 46 da LGPD).
  - Rastreabilidade forense integral e imutável de todas as execuções de descarte no `AuditLog` via encadeamento SHA-256.
  - Flexibilidade de operação com suporte a execuções manuais e modo `DryRun`.
- **Negativas / Limitações**:
  - Expurgo físico de `TeamInvites` impede que convidados que ignoraram o convite por mais de 30 dias possam consultar o status original do link expirado; nesses casos, o produtor rural precisará emitir um novo convite de equipe no painel da fazenda.
