# ADR 0020: Criptografia em Repouso no Módulo de Indicações e Blind Indexing (LGPD Art. 46)

## Status
Aceito (Accepted)

## Contexto
O Artigo 46 da Lei Geral de Proteção de Dados Pessoais (LGPD - Lei nº 13.709/2018) estabelece a obrigatoriedade de medidas de segurança, técnicas e administrativas aptas a proteger os dados pessoais de acessos não autorizados e de situações acidentais ou ilícitas de destruição, perda, alteração, comunicação ou qualquer forma de tratamento inadequado ou ilícito.

No módulo de indicações (`Referral`), os parceiros comerciais cadastram dados financeiros e cadastrais críticos:
1. **`PixKey`**: Chave Pix para liquidação financeira e repasses de comissões (D+30), correspondente frequentemente ao CPF, e-mail pessoal ou número de celular do parceiro.
2. **`LegalDocumentNumber`**: CPF do parceiro comercial autônomo ou CNPJ de parceiro PJ.

Até então, tais campos eram persistidos em texto plano no banco de dados. Um dump acidental ou acesso não autorizado à infraestrutura de dados exporia diretamente as chaves Pix e os documentos cadastrais dos parceiros.

### Desafio Técnico: Criptografia Probabilística vs. Buscas por Igualdade
Algoritmos simétricos robustos como **AES-256-GCM** utilizam vetor de inicialização / nonce aleatório (96 bits) gerado por CSPRNG a cada cifragem. Dois registros com o mesmo CPF geram textos cifrados completamente distintos. Consequentemente, consultas SQL tradicionais de validação de unicidade (`WHERE LegalDocumentNumber = @p0`) tornam-se inviáveis diretamente sobre a coluna cifrada sem forçar um *Full Table Scan* em memória.

## Decisão

1. **Abstração Centralizada em Building Blocks (`IDataEncryptionService`)**:
   - Criação da interface `IDataEncryptionService` em `CriaCerto.BuildingBlocks.Abstractions.Security`, permitindo injeção desacoplada de serviços criptográficos em toda a aplicação.
   - Implementação de fallback gracioso `NullDataEncryptionService` para ambientes de testes e design-time, garantindo que testes unitários continuem executando sem dependência externa.

2. **Motor Criptográfico AES-256-GCM (`AesGcmDataEncryptionService`)**:
   - Algoritmo de encriptação autenticada com dados associados (**AEAD**): **AES-256-GCM** (Galois/Counter Mode) via `System.Security.Cryptography.AesGcm`.
   - **Envelope Canônico com Prefixo de Versão (`$cc1$`)**:
     - O payload binário contém: `[KeyVersion (1B)][Nonce (12B)][AuthTag (16B)][Ciphertext (NB)]` codificado em Base64 com prefixo identificador `$cc1$`.
   - **Rotação de Chaves (Key Versioning & KeyRing)**:
     - O serviço cifra utilizando a chave ativa configurada (`CurrentKeyVersion`), mas mantém um chaveiro (`KeyRing`) com versões históricas para decifrar registros antigos de forma totalmente transparente.
   - **Tolerância a Migração de Dados Legados**:
     - Caso o valor lido do banco não possua o prefixo `$cc1$`, o serviço reconhece o dado como legado (texto puro) e o retorna sem crash, permitindo transição contínua.

3. **Padrão Blind Indexing (HMAC-SHA256)**:
   - Para viabilizar a verificação de unicidade em $O(1)$ sem expor a PII, adotou-se o padrão internacional de **Blind Index**:
     - `LegalDocumentNumber` permanece 100% criptografado com AES-256-GCM probabilístico na coluna do banco.
     - Uma coluna auxiliar indexada `LegalDocumentNumberHash` (`nvarchar(64)`) armazena o hash determinístico calculado via HMAC-SHA256 com chave secreta/pepper dedicada (`BlindIndexKey`).
     - A verificação de duplicidade em `RegisterCommercialPartnerCommandHandler` passa a consultar `c.LegalDocumentNumberHash == computedHash`.

4. **ValueConverter Transparente do EF Core (`EncryptedStringConverter`)**:
   - Implementado `EncryptedStringConverter` que intercepta leitura e escrita no `ReferralDbContext`.
   - Cifra na persistência (`SaveAsync`) e decifra na materialização de entidades em memória.
   - Colunas `PixKey` e `LegalDocumentNumber` expandidas para `nvarchar(512)` no SQL Server via migração EF Core `Add_PartnerContract_Encryption_And_Blind_Index`.

5. **Serviço e Endpoint de Re-encriptação Idempotente (`IReferralDataReencryptionService`)**:
   - Rotina administrativa e endpoint `POST /api/v1/backoffice/referral/reencrypt-contracts` que varre contratos legados, calcula o blind index e persiste os registros atualizados sob a permissão `referral.payouts.manage`.

## Consequências

- **Positivas**:
  - Plena conformidade com o Artigo 46 da LGPD, eliminando a exposição de PII financeira e cadastral em repouso no SQL Server.
  - Cifragem autenticada imune a ataques de *bit-flipping* e adulteração de dados (*tamper-evident*).
  - Consultas de unicidade e integridade preservadas com indexação $O(1)$ via HMAC Blind Index.
  - Suporte nativo a rotação de chaves sem downtime.
- **Negativas / Limitações**:
  - Armazenamento em Base64 com Nonce e Tag aumenta o consumo de bytes por registro nas colunas `PixKey` e `LegalDocumentNumber` (mitigado pela expansão para `nvarchar(512)`).
