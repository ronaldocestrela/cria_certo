# ADR 0007: Impersonação Segura com Dupla Salvaguarda e Sessões Efêmeras

## Status
Aceito (Accepted)

## Contexto
Operações de suporte avançado (N2) e administração de plataforma exigem eventualmente que atendentes acessem o painel operacional sob a perspectiva de um tenant específico para diagnosticar anomalias cadastrais, falhas de sincronização ou comportamentos zootécnicos inesperados.

Sem salvaguardas rigorosas, a impersonação apresenta elevados riscos de segurança, violação de privacidade (LGPD), ações não auditadas ou modificações indevidas.

## Decisão
Implementar o fluxo de **Acesso por Impersonação com Dupla Salvaguarda**:

1. **Salvaguarda de Justificativa e Rastreabilidade**:
   - Vínculo obrigatório de um chamado de suporte (`SupportTicket`) válido.
   - Justificativa técnica mandatória com no mínimo 10 caracteres.
   - Permissão granular `impersonation.start` restrita a `PlatformOwner` e `SupportN2` com MFA ativo.
2. **Salvaguarda Operacional e Temporal**:
   - Bloqueio rígido de impersonação para tenants com status `Suspended`, `Cancelled`, `Archived` ou com flag `IsProtected`.
   - Sessão efêmera com TTL curto e não renovável (5 a 60 minutos, padrão 15 min).
   - Emissão de JWT assinado com claims explícitas de auditoria (`is_impersonation=true`, `impersonated_by_admin_id`, `impersonation_ticket`).
3. **Interface e Kill Switch**:
   - Banner persistente no topo do frontend Blazor (`ImpersonationBanner.razor`) com contador regressivo ao vivo.
   - Botão de encerramento instantâneo que revoga a sessão, restaura o token de administrador e retorna ao Backoffice.
4. **Auditoria Forense**:
   - Registro imutável em `AuditLog` no início (`Impersonation.Started`) e encerramento (`Impersonation.Stopped`).

## Consequências
- **Positivas**: Rastreabilidade forense total, eliminação do risco de sessões órfãs ou perpétuas, bloqueio de acesso a contas sensíveis ou suspensas, aderência a compliance e LGPD.
- **Negativas**: Operadores precisam obrigatoriamente abrir tickets formais antes de acessar o tenant, introduzindo uma pequena fricção intencional em prol da segurança.
