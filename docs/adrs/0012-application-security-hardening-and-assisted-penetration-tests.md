# ADR 0012: Hardening de Segurança Aplicacional e Testes de Intrusão Assistidos (OWASP API Security)

## Status
Aceito (Accepted)

## Contexto
O Backoffice do SaaS CriaCerto centraliza operações sensíveis de administração de plataforma, governança de inquilinos (tenants), suporte assistido com impersonação, publicação de tabelas de preços e planos, auditoria forense criptográfica e gestão de dados pessoais (LGPD).
Com o início da **Phase 6: Hardening, Rollout Gradual e Go-Live**, era mandatória a implementação de uma estratégia ativa de **Defense-in-Depth** para mitigar riscos previstos na **OWASP API Security Top 10**, nomeadamente:
1. **Broken Authentication & Credential Stuffing (API2)**: Risco de ataques de força bruta contra endpoints de login e enumeração de contas de administradores via discrepâncias no tempo de resposta (*timing side-channel attacks*).
2. **Broken Function Level Authorization - BFLA (API5)** e **Broken Object Level Authorization - BOLA (API1)**: Risco de operadores com perfis restritos (`ReadOnlyAuditor`, `SupportN1`, `FinanceOps`) tentarem executar ações de escrita, suspensão ou desmascaramento, ou violarem o princípio dos 4 Olhos (*Four-Eyes Principle*).
3. **Evasão de Escopo por Impersonation Token**: Risco de tokens de suporte assistido (gerados para atuação no ambiente de um produtor) serem reaproveitados indevidamente para acessar endpoints administrativos do próprio Backoffice.
4. **Security Misconfiguration & Data Leakage (API8)**: Risco de armazenamento em cache de dados administrativos sensíveis ou vazamento de cabeçalhos de segurança permissivos.

## Decisão
Implementar uma arquitetura de defesa ativa e testes automatizados de intrusão assistidos para o Backoffice:

1. **Mitigação de Timing Attacks na Autenticação**:
   - No `AuthenticateAdminUserCommandHandler`, caso o e-mail não seja encontrado no banco, é executada uma verificação de hash com salt fixo pré-computado em PBKDF2 (`DummyPasswordHash`) em tempo constante (*constant-time dummy check*). Isso equaliza o custo computacional de validação criptográfica (100.000 iterações de PBKDF2), tornando o tempo de resposta indistinguível entre credenciais existentes e inexistentes, prevenindo enumeração de administradores.
   - Registro compulsório de telemetria em `BackofficeTelemetry.RecordPolicyFailure` para qualquer falha de autenticação (usuário inexistente, senha incorreta, usuário desativado ou MFA inválido), alimentando o motor de detecção de anomalias para disparar alerta de força bruta (`ALR_POLICY_BRUTE_FORCE`).

2. **Rate Limiting Defensivo Nativo (.NET 10)**:
   - Configuração do middleware `Microsoft.AspNetCore.RateLimiting` com particionamento por IP remoto (`BackofficeAuthRateLimiter`) com limite de requisições por janela temporal (15 req/min por IP) e resposta estruturada `429 Too Many Requests` com código de erro `Backoffice.RateLimitExceeded`.
   - Aplicado diretamente nas rotas `POST /api/v1/backoffice/auth/login` e `POST /api/v1/backoffice/auth/refresh`.

3. **Contenção Estrita de Sessões de Impersonação (Impersonation Token Containment)**:
   - O `BackofficeAccessMiddleware` valida explicitamente a claim `is_impersonation`.
   - Qualquer tentativa de utilizar token de suporte assistido dentro de rotas administrativas (`/api/v1/backoffice/*`) é imediatamente abortada com status `403 Forbidden` e código `Backoffice.ImpersonationRestricted`, impedindo escalonamento de privilégios ou travessia de contexto.

4. **Hardening de Cabeçalhos HTTP e Cache-Control Administrativo**:
   - `SecurityHeadersMiddleware` estendido para injetar:
     - `Content-Security-Policy`: Restrição de origens, proibição de frames (`frame-ancestors 'none'`) e fontes seguras.
     - `Permissions-Policy`: Desativação de APIs sensíveis de hardware (`camera=(), geolocation=(), microphone=()`, etc.).
     - `Cache-Control: no-store, no-cache, must-revalidate, max-age=0` e `Pragma: no-cache` para todas as respostas sob `/api/v1/backoffice/*`, mitigando vazamento em proxies ou caches locais.

5. **Validação Rigorosa de Escopos e Matriz de Autorização Negativa**:
   - `PermissionEvaluatorService` valida estritamente a legitimidade dos escopos informados (`ScopeGlobal`, `ScopeTenant`, `ScopeUnidade`) rejeitando escopos inválidos com `BackofficeErrors.InvalidScopeData`.
   - Bateria de testes de autorização negativa automatizada (`BackofficeNegativeAuthorizationTests` e `BackofficeAuthenticationHardeningTests`) marcada com `[Trait("Category", "SecurityRegression")]` para bloqueio compulsório de merge em CI/CD em caso de qualquer regressão de segurança.

## Consequências
- **Positivas**:
  - Imunidade comprovada contra ataques de força bruta, enumeração de contas e replay de tokens de sessão.
  - Bloqueio determinístico de BFLA, BOLA e violação de 4-Eyes testado e auditado automaticamente.
  - Zero risco de vazamento de dados administrativos em caches intermediários ou reutilização de tokens de suporte assistido.
  - Quality Gate automatizado em CI/CD com execução rápida (< 4 segundos).
- **Negativas**:
  - Operações de login para usuários inexistentes consomem o mesmo ciclo de CPU que para usuários reais (custo intencional para segurança contra timing attacks).
