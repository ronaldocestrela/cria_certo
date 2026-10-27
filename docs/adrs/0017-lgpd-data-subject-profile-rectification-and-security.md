# ADR 0017: Direito de Retificação e Gestão de Perfil Pessoal (LGPD Art. 18, III)

## Status
Aceito (Accepted)

## Contexto
O Artigo 18, inciso III da Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) estabelece como direito potestativo do titular a retificação de dados incompletos, inexatos ou desatualizados. 

No CriaCerto (SaaS B2B Agropecuário), os usuários cadastrados (produtores rurais, parceiros comerciais, veterinários e colaboradores de fazendas) necessitam de autonomia para:
1. Consultar de forma transparente seus dados mantidos pela plataforma, vínculos a fazendas/tenants e carimbos de consentimento prévio.
2. Atualizar seu Nome Completo e Telefone de contato de forma instantânea.
3. Gerenciar suas credenciais de acesso (troca de senha) com imposição de regras estritas de entropia e validação em tempo constante.
4. Registrar evidências imutáveis de cada alteração em trilha de auditoria forense criptográfica SHA-256 no `AuditLog`.

## Decisão
1. **Evolução do Domínio `User` (`Modules.Tenancy`)**:
   - Adicionada propriedade `CreatedAtUtc (DateTime)` com mapeamento no EF Core e migração `20261026210040_AddUserCreatedAtUtc` no schema `tenancy`.
   - Adicionados métodos de domínio na entidade `User`: `UpdateProfile(string fullName, string? phoneNumber)` e `ChangePassword(string newPasswordHash)` retornando `Result`.

2. **Arquitetura CQRS no Módulo Tenancy**:
   - `GetUserProfileQuery(Guid UserId)`: Retorna `UserProfileDto` contendo dados cadastrais, data de cadastro, status do e-mail, metadados de aceite dos Termos de Uso e Política de Privacidade e lista de granjas associadas (`UserTenantAffiliationDto`).
   - `UpdateUserProfileCommand(Guid UserId, string FullName, string? PhoneNumber, string? ClientIpAddress)`: Valida os dados via FluentValidation (`UpdateUserProfileCommandValidator`), atualiza os campos e aciona `ITenancyAuditSink.RecordUserProfileUpdateAuditAsync` gerando entrada forense `USER_PROFILE_RECTIFIED` (severidade `Medium`, categoria `Compliance`).
   - `ChangeUserPasswordCommand(Guid UserId, string CurrentPassword, string NewPassword, string ConfirmNewPassword, string? ClientIpAddress)`: Valida entropia via `ChangeUserPasswordCommandValidator` (mín. 8 caracteres, maiúsculas, minúsculas, números e símbolos). Valida senha atual em tempo constante via `PasswordHasher.Verify`, impede reutilização da mesma senha, aplica novo hash PBKDF2 e aciona `ITenancyAuditSink.RecordUserPasswordChangeAuditAsync` gerando entrada forense `USER_PASSWORD_CHANGED` (severidade `High`, categoria `Security`).

3. **Endpoints RESTful na API (`CriaCerto.Api`)**:
   - `GET /api/v1/auth/profile`: Requer autenticação JWT; extrai `UserId` da claim de identidade e executa `GetUserProfileQuery`.
   - `PUT /api/v1/auth/profile`: Requer autenticação JWT; mescla `UserId` autenticado com o payload e executa `UpdateUserProfileCommand`.
   - `POST /api/v1/auth/change-password`: Requer autenticação JWT; executa `ChangeUserPasswordCommand`.
   - As rotas `/api/v1/auth/*` são isentas da verificação de `TenantId` no `TenantAccessMiddleware`, viabilizando autoatendimento para usuários em qualquer estado de vinculação.

4. **Interface Blazor Web App (WASM) - `/settings/profile`**:
   - Implementada página `ProfileManagement.razor` seguindo o design system do CriaCerto.
   - Painel de Dados Cadastrais com avatar dinâmico e badges de confirmação de e-mail.
   - Painel de Segurança com medidor interativo de força de senha (*Password Strength Meter*), checklist visual de requisitos e alternância de visibilidade de senha.
   - Painel de Fazendas Vinculadas com badges de papel (RBAC) e planos de assinatura.
   - Central de Privacidade & DPO com dados de contato do Encarregado (`dpo@criacerto.com.br`) e atalhos aos Termos e Políticas públicas.
   - Acesso disponível pelo menu lateral (`NavigationCatalog.cs`) e botão de atalho no cabeçalho da aplicação (`MainLayout.razor`).

5. **Estratégia de Testes (TDD & Regressão de Segurança)**:
   - Testes unitários cobrindo todos os fluxos de sucesso e falha em `GetUserProfileQueryHandlerTests`, `UpdateUserProfileCommandHandlerTests`, `UpdateUserProfileCommandValidatorTests`, `ChangeUserPasswordCommandHandlerTests` e `ChangeUserPasswordCommandValidatorTests`.
   - Testes de integração forense em `UserProfileAndSecurityAuditIntegrationTests` validando a geração correta de registros SHA-256 encadeados no `BackofficeDbContext`.

## Consequências
- **Positivas**:
  - Conformidade plena com o Artigo 18, III da LGPD sem fricção operacional.
  - Segurança aprimorada contra sequestro de conta e reutilização de senhas fracas.
  - Rastreabilidade forense completa com não-repúdio das alterações cadastrais.
  - Experiência de usuário premium, responsiva e alinhada ao design system.
- **Negativas**:
  - Exige validação de senha atual antes de qualquer alteração de credencial.
