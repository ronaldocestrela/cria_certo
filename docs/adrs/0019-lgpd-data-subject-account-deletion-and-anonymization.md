# ADR 0019: Direito de Eliminação, Encerramento de Conta e Anonimização (LGPD Art. 18, VI)

## Status
Aceito (Accepted)

## Contexto
O Artigo 18, inciso VI da Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) estabelece como direito potestativo do titular a **eliminação dos dados pessoais** tratados com base em seu consentimento.

No modelo de negócios do **CriaCerto** (SaaS B2B Agropecuário Multi-tenant), os titulares dos dados incluem produtores rurais, administradores de fazendas, veterinários, zootecnistas e operadores de curral. O exercício desse direito impõe um desafio de **harmonização regulatória** entre a privacidade do titular e a legislação agropecuária e civil:

1. **Dados Pessoais do Usuário (Art. 18, VI da LGPD)**:
   - Nome completo, telefone, e-mail pessoal e credenciais secretas de autenticação (hashes de senha, tokens de ativação e de recuperação) não possuem mais finalidade de tratamento após o encerramento do vínculo contratual do titular e devem ser definitivamente eliminados.
2. **Dados Agropecuários e Histórico Transacional (Art. 16, I e II da LGPD c/c Código Civil e Normas MAPA)**:
   - A rastreabilidade de rebanhos bovinos, vacinações compulsórias (Brucelose, Febre Aftosa), nascimentos, pesagens e movimentações entre pastos/lotes são patrimônio corporativo da fazenda (controladora) e exigência sanitária do Ministério da Agricultura e Pecuária (MAPA).
   - Registros de faturamento e faturas Stripe devem ser conservados pelo prazo prescricional de 5 anos (Código Civil Art. 206, § 5º).
3. **Salvaguarda Operacional de Fazendas Ativas**:
   - Um usuário não pode encerrar sua conta se for o **único Administrador ativo** de uma fazenda com assinatura ou status vigente, sob pena de deixar a organização rural inoperante e inacessível (*tenant lock-in* ou *orphaned tenant*).

## Decisão

1. **Estratégia de Desassociação e Anonimização Irreversível**:
   - Em conformidade com o Artigo 12 da LGPD, dados anonimizados não são considerados dados pessoais se não permitirem a reidentificação do titular.
   - Ao executar a exclusão da conta:
     - `FullName` é substituído pelo marcador canônico `"Titular Anonimizado"`.
     - `Email` é substituído por endereço sintético irreversível `anon_{guid}@deleted.criacerto.com.br`, mantendo a unicidade da restrição única no banco de dados sem possibilidade de contato ou reidentificação.
     - `PhoneNumber`, `EmailConfirmationToken`, `PasswordResetToken` são zerados (`null`).
     - `PasswordHash` é sobrescrito com `"ANONYMIZED_DISABLED_CREDENTIAL"`, impedindo qualquer autenticação futura.
     - `IsAnonymized` é marcado como `true`, com `AnonymizedAtUtc = DateTime.UtcNow` e justificativa opcional (`AnonymizationReason`).
     - Todas as associações ativas com fazendas na tabela `UserTenants` são removidas (`_dbContext.UserTenants.RemoveRange`), desvinculando o usuário de qualquer organização pecuária.

2. **Bloqueio no Pipeline de Autenticação (`LoginCommandHandler`)**:
   - Tentativas de login em contas com `IsAnonymized == true` são imediatamente rejeitadas com `Result.Failure(Error.Unauthorized("Auth.AccountAnonymized", ...))`.

3. **Salvaguarda do Administrador Único no CQRS (`AnonymizeUserAccountCommandHandler`)**:
   - Antes de efetivar a anonimização, o handler inspeciona todas as granjas vinculadas onde o usuário desempenha o papel `UserRole.Admin` e cujo status seja operacional (`TenantLifecycle.CanProducerAccess`).
   - Caso não exista nenhum outro administrador ativo (não-anonimizado) na fazenda, a operação é abortada retornando `Result.Failure(Error.Conflict("User.SoleAdministratorOfActiveTenant", ...))`, orientando o produtor a transferir a administração ou cancelar a assinatura da fazenda previamente.

4. **Trilha de Auditoria Forense SHA-256 (`TenancyBackofficeAuditSink`)**:
   - Toda solicitação bem-sucedida registra compulsoriamente uma entrada imutável no `AuditLog`:
     - `Action = "USER_ACCOUNT_ANONYMIZED"`
     - `Category = AuditCategory.Compliance`
     - `Severity = AuditSeverity.Critical`
     - `Resource = $"Users/{userId}"`
     - Hash de integridade SHA-256 encadeado (`RecordHash` / `PreviousRecordHash`).
     - Metadados estruturados em JSON com e-mail original, e-mail anonimizado e motivo de saída.

5. **Interface de Autoatendimento no Blazor com Duplo Aviso (`ProfileManagement.razor`)**:
   - Card **"Zona de Perigo: Eliminação de Conta & Anonimização (Art. 18, VI da LGPD)"** em `/settings/profile` e `/settings/privacy`.
   - Modal com duplo aviso explicando a regra de harmonização agropecuária.
   - Exigência de senha atual para autorização e digitação compulsória da palavra-chave `"EXCLUIR"`.
   - Em caso de sucesso, efetua logout seguro via `CustomAuthStateProvider.MarkUserAsLoggedOut()`, limpa tokens de sessão e redireciona para `/login?reason=account_deleted`.

## Consequências

- **Positivas**:
  - Plena conformidade com o Artigo 18, VI e Artigo 16 da LGPD sem vulnerabilidades legais perante a ANPD e o MAPA.
  - Salvaguarda contra abandono de granjas ativas garantindo governança aos titulares da fazenda.
  - Rastreabilidade forense total com severidade crítica e encadeamento criptográfico SHA-256.
  - Desconexão instantânea do titular de dados e revogação permanente de credenciais.
- **Negativas / Limitações**:
  - A operação é estritamente irreversível; o titular necessita realizar um novo cadastro se desejar utilizar a plataforma futuramente.
