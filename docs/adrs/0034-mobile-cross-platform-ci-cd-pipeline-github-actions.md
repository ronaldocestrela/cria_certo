# ADR 0034: Pipeline CI/CD Multiplataforma para .NET MAUI Mobile (GitHub Actions)

## Status
Implementado na Fase 6 (Implemented in Phase 6 - Tarefa 6.1)

## Contexto
Com a criação do projeto hospedeiro .NET MAUI Blazor Hybrid `CriaCerto.Mobile` ([ADR 0030](0030-hybrid-maui-blazor-mobile-host-project.md)), a implementação dos serviços de plataforma nativa ([ADR 0031](0031-hybrid-maui-blazor-native-platform-services.md)), a configuração de segurança por SO ([ADR 0032](0032-mobile-platform-security-and-configuration-android-ios.md)) e a verificação de interoperabilidade Bearer JWT stateless ([ADR 0033](0033-api-endpoint-bearer-jwt-authorization-verification.md)), a solução mobile passou a suportar compilação para duas plataformas primárias: `net10.0-android` e `net10.0-ios`.

Entretanto, há assimetrias essenciais de ambiente:
1. **Ambiente de Desenvolvimento Local em Linux:** O time de engenharia e os contêineres de desenvolvimento operam primordialmente sobre estações Linux. No Linux, a compilação do alvo `net10.0-android` é viabilizada através do Android SDK e do workload `maui-android`. Contudo, a compilação para `net10.0-ios` depende estritamente do toolchain da Apple (Xcode, Apple SDKs e simuladores), indisponível de forma nativa em distribuições Linux.
2. **Prevenção de Regressões em iOS:** Sem um pipeline automatizado com runners macOS dedicados, alterações na Razor Class Library `CriaCerto.Shared.UI` ou no projeto `CriaCerto.Mobile` poderiam introduzir quebras ou incompatibilidades em `net10.0-ios` que só seriam descobertas tardiamente.
3. **Custo e Cota de Runners no GitHub Actions:** Runners com sistema operacional `macos-latest` possuem consumo tarifário e de cotas aproximadamente 10 vezes maior que os runners padrão Linux `ubuntu-latest`. Executar a esteira mobile a cada commit de backend geraria desperdício significativo de recursos de CI/CD.
4. **Assinatura Digital (Code Signing) e Living Documentation:** O processo de distribuição para lojas (Google Play Store e Apple App Store Connect) requer certificados e perfis de provisionamento confidenciais. O pipeline precisa documentar formalmente os segredos necessários e possuir Living Documentation executável que previna *documentation drift* na esteira de CI.

## Decisão

1. **Criação do Workflow Dedicado `.github/workflows/mobile-build.yml`:**
   - **Gatilhos com Filtragem Estrita de Caminhos (`paths`):** O workflow é acionado exclusivamente por eventos de `push` e `pull_request` nos branches principais (`main`, `master`, `develop`) ou sob demanda (`workflow_dispatch`), quando houver mudanças em:
     - `src/Mobile/**`
     - `src/Web/CriaCerto.Shared.UI/**`
     - `.github/workflows/mobile-build.yml`
     Isso isola as alterações exclusivas do backend no pipeline geral (`ci.yml`), otimizando cotas de execução.
   - **Job `build-android` em `ubuntu-latest`:**
     - Configura o Java SDK 17 Temurin (`actions/setup-java@v4`).
     - Configura o .NET SDK 10 (`actions/setup-dotnet@v4`).
     - Instala sob demanda o workload móvel (`dotnet workload install maui-android`).
     - Executa restauração, compilação Release (`dotnet build CriaCerto.Mobile.csproj -f net10.0-android -c Release --no-restore`) e geração de pacote APK (`dotnet publish -p:AndroidPackageFormat=apk`).
     - Disponibiliza o artefato compilado via `actions/upload-artifact@v4` com retenção de 14 dias para testes de campo.
   - **Job `build-ios` em `macos-latest`:**
     - Configura o .NET SDK 10 (`actions/setup-dotnet@v4`).
     - Instala sob demanda o workload iOS (`dotnet workload install maui-ios`).
     - Executa compilação Release (`dotnet build CriaCerto.Mobile.csproj -f net10.0-ios -c Release --no-restore`).
     - Formaliza em código a documentação dos segredos exigidos para assinatura e publicação em App Store/TestFlight (`APPLE_CERTIFICATE_BASE64`, `APPLE_CERTIFICATE_PASSWORD` e `APPLE_PROVISIONING_PROFILE_BASE64`).

2. **Living Documentation & Automação de Testes de Arquitetura:**
   - Criada a classe `MobileCiCdWorkflowTests.cs` na suíte `CriaCerto.Web.Client.UnitTests` validando:
     - Existência e integridade do arquivo `.github/workflows/mobile-build.yml`;
     - Presença dos gatilhos (`push`, `pull_request`, `workflow_dispatch`) e filtros de caminhos de código compartilhado e mobile;
     - Configuração dos runners corretos (`ubuntu-latest` para Android e `macos-latest` para iOS);
     - Instalação dos workloads `maui-android` e `maui-ios` com alvos de compilação Release `net10.0-android` e `net10.0-ios`;
     - Existência física no repositório de todos os caminhos `.csproj` referenciados nas diretivas do workflow;
     - Presença documentada dos segredos para assinatura Apple.

## Consequências

### Positivas
- **Garantia Contínua de Compilação iOS:** O alvo `net10.0-ios` é continuamente compilado e validado em hardware macOS real na nuvem a cada PR que toque na RCL ou no projeto mobile, eliminando a dependência de máquinas Mac locais para desenvolvedores em Linux.
- **Eficiência de Recursos e Custos:** A filtragem por caminhos (`paths`) previne que builds caros de macOS sejam executados quando commits alteram apenas o banco de dados, a API ou módulos isolados de backend.
- **Living Documentation Automatizada:** Qualquer alteração inadvertida nos caminhos de projeto, remoção de gatilhos ou supressão de workloads falha imediatamente os testes unitários da aplicação (`MobileCiCdWorkflowTests`), prevenindo drift entre a documentação/código e o pipeline de CI.
- **Geração Ágil de Builds para Testes de Campo:** O job de Android gera automaticamente APKs publicadas como artefatos de build para validação rápida por técnicos e pecuaristas em campo.

### Negativas / Trade-offs
- O tempo total de execução do workflow no GitHub Actions inclui a etapa de instalação dos workloads MAUI (`maui-android` e `maui-ios`), o que adiciona alguns minutos à esteira; isso é mitigado pela filtragem cirúrgica de paths, rodando apenas quando código mobile ou da RCL é modificado.

